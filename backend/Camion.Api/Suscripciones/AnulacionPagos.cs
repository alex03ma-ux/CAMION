using System.Globalization;
using System.Text.Json;
using Camion.Api.Auth;
using Camion.Api.Data;
using Camion.Api.Models;
using Camion.Api.Publicaciones;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Suscripciones;

// P7 (D-67): anular un pago deshace lo que ese pago dio, en la misma transacción que la anulación.
//   RENOVACION  -> la suscripción vuelve a la vigencia (y plan) de antes de esa renovación;
//   ASIGNACION  -> la suscripción queda CANCELADA;
//   CAMBIO_PLAN -> la suscripción nueva queda CANCELADA y la anterior vuelve a su plan, estado y fin de antes;
//   POSTERIOR   -> solo se anula el pago.
// Solo se deshace el movimiento más reciente de la suscripción. El "antes" sale de la auditoría del movimiento
// (Suscripcion / RENOVAR o CAMBIAR_PLAN, datos_anteriores), sin columnas nuevas. Cada deshecho deja un evento
// Suscripcion / DESHACER_PAGO con el id del movimiento, para que el movimiento anterior vuelva a ser "el más reciente".
// Si la suscripción se canceló después del movimiento, no hay nada que deshacer: solo se anula el pago.
public static class AnulacionPagos
{
    public const string TipoDeshacerRenovacion = "DESHACER_RENOVACION";
    public const string TipoCancelarAsignacion = "CANCELAR_ASIGNACION";
    public const string TipoDeshacerCambioPlan = "DESHACER_CAMBIO_PLAN";
    public const string TipoSoloPago = "SOLO_PAGO";

    public const string BloqueoYaAnulado = "YA_ANULADO";
    public const string BloqueoSinDatos = "SIN_DATOS";
    public const string BloqueoMovimientoPosterior = "MOVIMIENTO_POSTERIOR";
    public const string BloqueoSinMovimiento = "SIN_MOVIMIENTO";

    public const string MensajeMovimientoPosterior =
        "Después de este pago hubo otra asignación, renovación o cambio de plan en esta suscripción. Anula primero el pago de ese movimiento.";
    public const string MensajeSinMovimiento =
        "No se encontró el movimiento de este pago en la auditoría: no se puede deshacer automáticamente.";

    private static readonly string[] Movimientos = [Auditoria.Asignar, Auditoria.Renovar, Auditoria.CambiarPlan];

    /// <summary>Lo que hará la anulación. Bloqueo != null: la anulación responde 409 con Mensaje.</summary>
    public sealed class Plan
    {
        public Pago Pago { get; init; } = null!;
        public string Tipo { get; set; } = TipoSoloPago;
        public string? Bloqueo { get; set; }
        public string? Mensaje { get; set; }
        /// <summary>Suscripción que queda vigente o restaurada (en un cambio de plan, la anterior).</summary>
        public long SuscripcionId { get; set; }
        public string? NuevoEstado { get; set; }
        public DateTime? NuevoInicio { get; set; }
        public DateTime? NuevaFin { get; set; }
        public long? NuevoPlanId { get; set; }
        public string? NuevoPlanNombre { get; set; }
        public bool QuedaraVencida { get; set; }
        /// <summary>En un cambio de plan: la suscripción nueva, que se cancela.</summary>
        public long? SuscripcionCanceladaId { get; set; }
        public DateTime? FinCancelada { get; set; }
        /// <summary>Eventos de movimiento que quedan deshechos (por suscripción).</summary>
        public List<(long SuscripcionId, long EventoId)> Deshechos { get; } = [];
    }

    public static async Task<Plan?> PlanearAsync(CamionDbContext db, long pagoId, DateTime ahora, CancellationToken ct)
    {
        var pago = await db.Pagos.AsNoTracking().SingleOrDefaultAsync(p => p.Id == pagoId, ct);
        if (pago is null) return null;
        var plan = new Plan { Pago = pago, SuscripcionId = pago.SuscripcionId };
        if (pago.Estado == Pago.Anulado) return Bloquear(plan, BloqueoYaAnulado, ReglasPagos.MensajeYaAnulado);
        if (pago.FechaPago is null || pago.Medio is null) return Bloquear(plan, BloqueoSinDatos, ReglasPagos.MensajeNoAnulable);

        var accion = pago.Concepto switch
        {
            Pago.ConceptoAsignacion => Auditoria.Asignar,
            Pago.ConceptoRenovacion => Auditoria.Renovar,
            Pago.ConceptoCambioPlan => Auditoria.CambiarPlan,
            _ => null
        };
        var sus = await db.Suscripciones.AsNoTracking().SingleAsync(s => s.Id == pago.SuscripcionId, ct);
        Describir(plan, sus, ahora);
        if (accion is null) return plan;   // POSTERIOR (o sin concepto): solo el pago.

        var eventos = await EventosAsync(db, sus.Id, ct);
        var movimiento = BuscarMovimiento(eventos, accion, pago.CreadoEn);
        if (movimiento is null) return Bloquear(plan, BloqueoSinMovimiento, MensajeSinMovimiento);
        if (HayPosterior(eventos, movimiento.Id))
            return Bloquear(plan, BloqueoMovimientoPosterior, MensajeMovimientoPosterior);
        // Cancelada después del movimiento: no hay nada que deshacer (D-67, solo el pago).
        if (sus.Estado == Suscripcion.Cancelada) return plan;

        switch (accion)
        {
            case Auditoria.Asignar:
                plan.Tipo = TipoCancelarAsignacion;
                plan.NuevoEstado = Suscripcion.Cancelada;
                plan.NuevoInicio = sus.InicioVigencia;
                plan.NuevaFin = ReglasP4p.FinAlTerminar(sus, ahora);
                plan.NuevoPlanId = sus.PlanId;
                plan.QuedaraVencida = false;
                plan.Deshechos.Add((sus.Id, movimiento.Id));
                break;

            case Auditoria.Renovar:
            {
                var antes = Leer(movimiento.DatosAnteriores);
                if (antes is null) return Bloquear(plan, BloqueoSinMovimiento, MensajeSinMovimiento);
                plan.Tipo = TipoDeshacerRenovacion;
                // Una suspensión o reactivación posterior se conserva; si no, vuelve el estado de antes.
                plan.NuevoEstado = sus.Estado == Suscripcion.Activa ? antes.Estado ?? sus.Estado : sus.Estado;
                plan.NuevoInicio = antes.Inicio;
                plan.NuevaFin = antes.Fin;
                plan.NuevoPlanId = antes.PlanId ?? sus.PlanId;
                plan.Deshechos.Add((sus.Id, movimiento.Id));
                break;
            }

            case Auditoria.CambiarPlan:
            {
                // El pago está en la suscripción nueva; su evento dice cuál era la anterior.
                var anteriorId = LeerLong(movimiento.DatosNuevos, "suscripcionAnteriorId");
                if (anteriorId is null) return Bloquear(plan, BloqueoSinMovimiento, MensajeSinMovimiento);
                var anterior = await db.Suscripciones.AsNoTracking().SingleOrDefaultAsync(s => s.Id == anteriorId, ct);
                var eventosAnterior = await EventosAsync(db, anteriorId.Value, ct);
                var movAnterior = eventosAnterior.LastOrDefault(e => e.Accion == Auditoria.CambiarPlan &&
                                                                     LeerLong(e.DatosNuevos, "suscripcionNuevaId") == sus.Id);
                var antes = movAnterior is null ? null : Leer(movAnterior.DatosAnteriores);
                if (anterior is null || movAnterior is null || antes is null)
                    return Bloquear(plan, BloqueoSinMovimiento, MensajeSinMovimiento);
                plan.Tipo = TipoDeshacerCambioPlan;
                plan.SuscripcionId = anterior.Id;
                plan.NuevoEstado = sus.Estado == Suscripcion.Suspendida ? Suscripcion.Suspendida : antes.Estado ?? Suscripcion.Activa;
                plan.NuevoInicio = antes.Inicio;
                plan.NuevaFin = antes.Fin;
                plan.NuevoPlanId = antes.PlanId ?? anterior.PlanId;
                plan.SuscripcionCanceladaId = sus.Id;
                plan.FinCancelada = ReglasP4p.FinAlTerminar(sus, ahora);
                plan.Deshechos.Add((sus.Id, movimiento.Id));
                plan.Deshechos.Add((anterior.Id, movAnterior.Id));
                break;
            }
        }

        plan.QuedaraVencida = plan.Tipo != TipoCancelarAsignacion &&
            (plan.NuevoEstado == Suscripcion.Vencida ||
             (ReglasP4p.EstadoVigente(plan.NuevoEstado!) && plan.NuevaFin is { } fin && fin <= ahora));
        plan.NuevoPlanNombre = await db.Planes.AsNoTracking().Where(p => p.Id == plan.NuevoPlanId)
            .Select(p => p.Nombre).FirstOrDefaultAsync(ct);
        return plan;
    }

    /// <summary>
    /// Aplica el plan dentro de la transacción del llamador (que ya bloqueó al asesor) y agrega la auditoría
    /// Suscripcion / DESHACER_PAGO. Devuelve el id del asesor afectado.
    /// </summary>
    public static async Task AplicarAsync(CamionDbContext db, Plan plan, string? adminId, string motivo, DateTime ahora,
        CancellationToken ct)
    {
        if (plan.Tipo == TipoSoloPago) return;
        var restaurada = await db.Suscripciones.SingleAsync(s => s.Id == plan.SuscripcionId, ct);
        if (plan.SuscripcionCanceladaId is { } canceladaId)
        {
            // Primero se cancela la nueva: así nunca hay dos vivas del mismo asesor.
            var nueva = await db.Suscripciones.SingleAsync(s => s.Id == canceladaId, ct);
            var antesNueva = Datos(nueva);
            nueva.Estado = Suscripcion.Cancelada;
            nueva.FinVigencia = plan.FinCancelada;
            Auditar(db, adminId, nueva, antesNueva, plan, motivo, ahora);
            await db.SaveChangesAsync(ct);
        }

        var antes = Datos(restaurada);
        restaurada.Estado = plan.NuevoEstado!;
        restaurada.InicioVigencia = plan.NuevoInicio;
        restaurada.FinVigencia = plan.NuevaFin;
        if (plan.NuevoPlanId is { } planId) restaurada.PlanId = planId;
        Auditar(db, adminId, restaurada, antes, plan, motivo, ahora);
        // D-38, D-41: los destacados duran hasta el fin del plan; si el fin restaurado es anterior, se acortan.
        if (plan.Tipo != TipoCancelarAsignacion && plan.NuevaFin is { } fin)
            await AcortarDestacadosAsync(db, restaurada.AsesorId, fin, ahora, ct);
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------------------------------------------------------

    private static Plan Bloquear(Plan plan, string bloqueo, string mensaje)
    {
        plan.Bloqueo = bloqueo;
        plan.Mensaje = mensaje;
        return plan;
    }

    // Sin cambios en la suscripción (POSTERIOR o bloqueado): describe el estado actual.
    private static void Describir(Plan plan, Suscripcion s, DateTime ahora)
    {
        plan.NuevoEstado = s.Estado;
        plan.NuevoInicio = s.InicioVigencia;
        plan.NuevaFin = s.FinVigencia;
        plan.NuevoPlanId = s.PlanId;
        plan.QuedaraVencida = ReglasP4p.Vencida(s, ahora);
    }

    private static Task<List<AuditoriaEvento>> EventosAsync(CamionDbContext db, long suscripcionId, CancellationToken ct)
    {
        var id = suscripcionId.ToString(CultureInfo.InvariantCulture);
        return db.AuditoriaEventos.AsNoTracking()
            .Where(a => a.Entidad == Auditoria.EntidadSuscripcion && a.EntidadId == id)
            .OrderBy(a => a.Id).ToListAsync(ct);
    }

    // El pago y el evento del movimiento se escriben en la misma transacción con la misma hora.
    private static AuditoriaEvento? BuscarMovimiento(List<AuditoriaEvento> eventos, string accion, DateTime creadoEn) =>
        eventos.LastOrDefault(e => e.Accion == accion && e.CreadoEn == creadoEn) ??
        eventos.LastOrDefault(e => e.Accion == accion && Math.Abs((e.CreadoEn - creadoEn).TotalSeconds) < 1);

    private static bool HayPosterior(List<AuditoriaEvento> eventos, long movimientoId)
    {
        var deshechos = eventos.Where(e => e.Accion == Auditoria.DeshacerPago)
            .Select(e => LeerLong(e.DatosNuevos, "movimientoId")).OfType<long>().ToHashSet();
        return eventos.Any(e => e.Id > movimientoId && Movimientos.Contains(e.Accion) && !deshechos.Contains(e.Id));
    }

    private sealed record Antes(string? Estado, long? PlanId, DateTime? Inicio, DateTime? Fin);

    private static Antes? Leer(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var d = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (d is null) return null;
            string? Texto(string k) => d.TryGetValue(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            DateTime? Fecha(string k) => Texto(k) is { } t &&
                DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var f)
                    ? DateTime.SpecifyKind(f, DateTimeKind.Unspecified)
                    : null;
            long? planId = d.TryGetValue("planId", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt64() : null;
            var estado = Texto("estado");
            return estado is null || !Suscripcion.Estados.Contains(estado) ? null
                : new Antes(estado, planId, Fecha("inicioVigencia"), Fecha("finVigencia"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long? LeerLong(string? json, string clave)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var d = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            return d is not null && d.TryGetValue(clave, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, object?> Datos(Suscripcion s) => new()
    {
        ["asesorId"] = s.AsesorId,
        ["estado"] = s.Estado,
        ["planId"] = s.PlanId,
        ["inicioVigencia"] = s.InicioVigencia is { } i ? DateTime.SpecifyKind(i, DateTimeKind.Utc) : null,
        ["finVigencia"] = s.FinVigencia is { } f ? DateTime.SpecifyKind(f, DateTimeKind.Utc) : null
    };

    private static void Auditar(CamionDbContext db, string? adminId, Suscripcion s, Dictionary<string, object?> antes, Plan plan,
        string motivo, DateTime ahora)
    {
        var nuevos = Datos(s);
        nuevos["pagoId"] = plan.Pago.Id;
        nuevos["tipo"] = plan.Tipo;
        nuevos["movimientoId"] = plan.Deshechos.Where(d => d.SuscripcionId == s.Id).Select(d => (long?)d.EventoId).FirstOrDefault();
        nuevos["motivo"] = motivo;
        Auditoria.AgregarEvento(db, adminId, Auditoria.EntidadSuscripcion, s.Id.ToString(CultureInfo.InvariantCulture),
            Auditoria.DeshacerPago, antes, nuevos, ahora);
    }

    // Destacados ACTIVA del asesor que terminaban después del nuevo fin: terminan en ese fin, o ahora si ya pasó.
    private static async Task AcortarDestacadosAsync(CamionDbContext db, long asesorId, DateTime nuevoFin, DateTime ahora,
        CancellationToken ct)
    {
        var destacados = await db.PublicacionesDestacadas.Where(d =>
                d.Estado == PublicacionDestacada.Activa && d.Fin > nuevoFin &&
                db.Publicaciones.Any(p => p.Id == d.PublicacionId && p.AsesorId == asesorId))
            .ToListAsync(ct);
        foreach (var d in destacados)
        {
            if (nuevoFin > ahora && nuevoFin > d.Inicio)
            {
                d.Fin = nuevoFin;
                continue;
            }
            d.Estado = PublicacionDestacada.Finalizada;
            d.Fin = ReglasP4b.FinAlCerrar(d.Inicio, d.Fin, ahora < nuevoFin ? ahora : nuevoFin);
        }
    }
}
