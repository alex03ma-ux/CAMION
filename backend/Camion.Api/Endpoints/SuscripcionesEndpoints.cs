using System.Globalization;
using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Models;
using Camion.Api.Suscripciones;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Endpoints;

// P4p: suscripciones (HU-066 consultar, HU-067 renovar, HU-068 suspender, HU-069 reactivar; RN-SU-001 a RN-SU-005,
// D-4, D-26, D-35, D-36). Solo el administrador las gestiona (activación manual al confirmar el pago); el asesor
// consulta la suya. Vencida se calcula al leer (estadoEfectivo), sin proceso automático.
public static class SuscripcionesEndpoints
{
    private const int TamanoPorDefecto = 20;
    private const int TamanoMaximo = 100;

    private static readonly string[] EstadosFiltro = Suscripcion.Estados;

    public static IEndpointRouteBuilder MapSuscripcionesEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(Roles.PoliticaSoloAdministrador);

        // Lista filtrable. estado filtra por el estado efectivo (VENCIDA incluye las ACTIVA/REACTIVADA ya vencidas).
        admin.MapGet("/suscripciones", async (string? asesorId, string? estado, string? pagina, string? tamano,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var errores = new Dictionary<string, string[]>();
            var asesor = LeerEntero(asesorId, 1, long.MaxValue, "asesorId", ReglasP4p.MensajeAsesorId, errores);
            var estadoFiltro = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
            if (estadoFiltro is not null && !EstadosFiltro.Contains(estadoFiltro)) errores["estado"] = [ReglasP4p.MensajeEstado];
            var numeroPagina = (int)(LeerEntero(pagina, 1, int.MaxValue, "pagina", ReglasP4p.MensajePagina, errores) ?? 1);
            var tamanoPagina = (int)(LeerEntero(tamano, 1, TamanoMaximo, "tamano", ReglasP4p.MensajePagina, errores) ?? TamanoPorDefecto);
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasP4p.MensajeFiltros, errores));

            var ahora = reloj.GetUtcNow().UtcDateTime;
            var consulta = db.Suscripciones.AsNoTracking();
            if (asesor is not null) consulta = consulta.Where(s => s.AsesorId == asesor);
            consulta = estadoFiltro switch
            {
                null => consulta,
                Suscripcion.Vencida => consulta.Where(s => s.Estado == Suscripcion.Vencida ||
                    ((s.Estado == Suscripcion.Activa || s.Estado == Suscripcion.Reactivada) &&
                     s.FinVigencia != null && s.FinVigencia <= ahora)),
                Suscripcion.Activa or Suscripcion.Reactivada => consulta.Where(s => s.Estado == estadoFiltro &&
                    (s.FinVigencia == null || s.FinVigencia > ahora)),
                _ => consulta.Where(s => s.Estado == estadoFiltro)
            };

            var total = await consulta.CountAsync(ct);
            var saltar = (long)(numeroPagina - 1) * tamanoPagina;
            var filas = saltar >= total
                ? []
                : await Filas(db, consulta.OrderByDescending(s => s.CreadoEn).ThenByDescending(s => s.Id)
                    .Skip((int)saltar).Take(tamanoPagina)).ToListAsync(ct);
            var items = filas.OrderByDescending(f => f.S.CreadoEn).ThenByDescending(f => f.S.Id)
                .Select(f => Item(f, ahora)).ToArray();
            return Results.Ok(new SuscripcionesPaginaResponse(items, total, numeroPagina, tamanoPagina));
        });

        // La suscripción viva del asesor o, si no tiene, la última. 404 si el asesor no existe o nunca tuvo.
        admin.MapGet("/asesores/{asesorId}/suscripcion", async (long asesorId, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
        {
            var ahora = reloj.GetUtcNow().UtcDateTime;
            var fila = await Filas(db, db.Suscripciones.AsNoTracking().Where(s => s.AsesorId == asesorId)
                    .OrderByDescending(s => Suscripcion.EstadosVivos.Contains(s.Estado))
                    .ThenByDescending(s => s.CreadoEn).ThenByDescending(s => s.Id).Take(1))
                .FirstOrDefaultAsync(ct);
            return fila is null ? NoEncontrado() : Results.Ok(Item(fila, ahora));
        });

        // Asignar (HU-067 / D-26): crea una ACTIVA desde ahora por los días del plan. El asesor debe estar ACTIVA.
        admin.MapPost("/asesores/{asesorId}/suscripcion", async (long asesorId, AsignarPlanRequest? request,
            ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            if (request?.PlanId is not { } planId) return ErrorPlan(ReglasP4p.MensajePlan);
            var adminId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var ahora = reloj.GetUtcNow().UtcDateTime;

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Bloquea la fila del asesor hasta el COMMIT (MariaDB): dos asignaciones a la vez no crean dos vivas.
            if (await BloquearAsesorAsync(db, asesorId, ct) == 0) return NoEncontrado();
            var plan = await db.Planes.AsNoTracking().SingleOrDefaultAsync(p => p.Id == planId && p.Activo, ct);
            if (plan is null) return ErrorPlan(ReglasP4p.MensajePlanActivo);
            if (!await AsesorActivoAsync(db, asesorId, ct))
                return Results.Conflict(new ErrorResponse(ReglasP4p.MensajeAsesorNoActivo));
            if (await db.Suscripciones.AnyAsync(s => s.AsesorId == asesorId && Suscripcion.EstadosVivos.Contains(s.Estado), ct))
                return Results.Conflict(new ErrorResponse(ReglasP4p.MensajeYaTieneViva));

            var nueva = Nueva(asesorId, plan, adminId, ahora);
            db.Suscripciones.Add(nueva);
            await db.SaveChangesAsync(ct);
            Auditar(db, adminId, nueva, Auditoria.Asignar, null, Datos(nueva), null, ahora);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/admin/asesores/{asesorId}/suscripcion", await ItemAsync(db, nueva.Id, ahora, ct));
        });

        // HU-067, D-36 (solo admin): fin = max(ahora, fin) + días del plan. Vencida: vuelve a ACTIVA desde ahora.
        admin.MapPost("/suscripciones/{id}/renovar", async (long id, RenovarRequest? request, ClaimsPrincipal principal,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var adminId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var ahora = reloj.GetUtcNow().UtcDateTime;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var s = await CargarBloqueadaAsync(db, id, ct);
            if (s is null) return NoEncontrado();
            var vencida = ReglasP4p.Vencida(s, ahora);
            if (!ReglasP4p.EstadoVigente(s.Estado) && s.Estado != Suscripcion.Vencida)
                return Conflicto("renovar", s, ahora);
            var planElegido = request?.PlanId ?? s.PlanId;
            var plan = await db.Planes.AsNoTracking().SingleOrDefaultAsync(p => p.Id == planElegido && p.Activo, ct);
            if (plan is null) return ErrorPlan(ReglasP4p.MensajePlanActivo);
            // Una VENCIDA (columna) no es viva: al renovarla no puede quedar junto a otra viva del mismo asesor.
            if (!ReglasP4p.EsViva(s.Estado) &&
                await db.Suscripciones.AnyAsync(x => x.AsesorId == s.AsesorId && x.Id != s.Id &&
                                                     Suscripcion.EstadosVivos.Contains(x.Estado), ct))
                return Results.Conflict(new ErrorResponse(ReglasP4p.MensajeYaTieneViva));

            var antes = Datos(s);
            var dias = ReglasP4p.DiasVigencia(plan.DuracionMeses);
            if (vencida || s.FinVigencia is null)
            {
                s.Estado = Suscripcion.Activa;
                if (vencida || s.InicioVigencia is null) s.InicioVigencia = ahora;
                s.FinVigencia = (s.FinVigencia is { } f && f > ahora ? f : ahora).AddDays(dias);
            }
            else
            {
                s.FinVigencia = s.FinVigencia.Value.AddDays(dias);
            }
            s.PlanId = plan.Id;
            s.AprobadoPor = adminId;
            s.AprobadoEn = ahora;
            Auditar(db, adminId, s, Auditoria.Renovar, antes, Datos(s), null, ahora);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(await ItemAsync(db, s.Id, ahora, ct));
        });

        // Cambiar de plan: termina la actual (CANCELADA, fin = ahora) y crea otra ACTIVA con el plan nuevo, en una transacción.
        admin.MapPost("/suscripciones/{id}/cambiar-plan", async (long id, AsignarPlanRequest? request,
            ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            if (request?.PlanId is not { } planId) return ErrorPlan(ReglasP4p.MensajePlan);
            var adminId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var ahora = reloj.GetUtcNow().UtcDateTime;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var s = await CargarBloqueadaAsync(db, id, ct);
            if (s is null) return NoEncontrado();
            if (!ReglasP4p.EsViva(s.Estado)) return Conflicto("cambiar el plan de", s, ahora);
            var plan = await db.Planes.AsNoTracking().SingleOrDefaultAsync(p => p.Id == planId && p.Activo, ct);
            if (plan is null) return ErrorPlan(ReglasP4p.MensajePlanActivo);

            var antes = Datos(s);
            s.Estado = Suscripcion.Cancelada;
            s.FinVigencia = ReglasP4p.FinAlTerminar(s, ahora);
            var nueva = Nueva(s.AsesorId, plan, adminId, ahora);
            db.Suscripciones.Add(nueva);
            await db.SaveChangesAsync(ct);
            var nuevos = Datos(s);
            nuevos["suscripcionNuevaId"] = nueva.Id;
            Auditar(db, adminId, s, Auditoria.CambiarPlan, antes, nuevos, null, ahora);
            var datosNueva = Datos(nueva);
            datosNueva["suscripcionAnteriorId"] = s.Id;
            Auditar(db, adminId, nueva, Auditoria.CambiarPlan, null, datosNueva, null, ahora);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(await ItemAsync(db, nueva.Id, ahora, ct));
        });

        // HU-068, HU-069, cancelar.
        MapEstado(admin, "suspender", "suspender", Auditoria.Suspender, motivoObligatorio: true,
            puede: s => ReglasP4p.EstadoVigente(s.Estado),
            aplicar: (s, _) => s.Estado = Suscripcion.Suspendida);
        // Reactivar NO extiende la fecha de fin (PROPUESTO en el contrato).
        MapEstado(admin, "reactivar", "reactivar", Auditoria.Reactivar, motivoObligatorio: false,
            puede: s => s.Estado == Suscripcion.Suspendida,
            aplicar: (s, _) => s.Estado = Suscripcion.Reactivada);
        MapEstado(admin, "cancelar", "cancelar", Auditoria.Cancelar, motivoObligatorio: true,
            puede: s => ReglasP4p.EsViva(s.Estado),
            aplicar: (s, ahora) =>
            {
                s.Estado = Suscripcion.Cancelada;
                if (s.FinVigencia is null || s.FinVigencia > ahora) s.FinVigencia = ReglasP4p.FinAlTerminar(s, ahora);
            });

        // HU-066 (RN-SU-001): el asesor consulta su suscripción (viva o, si no, la última vencida).
        app.MapGet("/api/asesor/suscripcion", async (ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
        {
            var usuarioId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var ahora = reloj.GetUtcNow().UtcDateTime;
            var perfilId = await db.PerfilesAsesor.AsNoTracking().Where(p => p.UsuarioId == usuarioId)
                .Select(p => (long?)p.Id).FirstOrDefaultAsync(ct);
            var fila = perfilId is null
                ? null
                : await Filas(db, db.Suscripciones.AsNoTracking()
                        .Where(s => s.AsesorId == perfilId &&
                                    (Suscripcion.EstadosVivos.Contains(s.Estado) || s.Estado == Suscripcion.Vencida))
                        .OrderByDescending(s => Suscripcion.EstadosVivos.Contains(s.Estado))
                        .ThenByDescending(s => s.CreadoEn).ThenByDescending(s => s.Id).Take(1))
                    .FirstOrDefaultAsync(ct);
            if (fila is null) return Results.Ok(new SinSuscripcionResponse(null, ReglasP4p.MensajeSinSuscripcion));
            var i = Item(fila, ahora);
            return Results.Ok(new MiSuscripcionResponse(
                new MiPlan(i.Plan.Nombre, i.Plan.LimitePublicaciones, i.Plan.DiasVigencia),
                i.Estado, i.EstadoEfectivo, i.InicioVigencia, i.FinVigencia, i.DiasRestantes, i.Vigente));
        }).RequireAuthorization(Roles.PoliticaSoloAsesor);

        return app;
    }

    private static void MapEstado(RouteGroupBuilder admin, string ruta, string verbo, string accion, bool motivoObligatorio,
        Func<Suscripcion, bool> puede, Action<Suscripcion, DateTime> aplicar)
    {
        admin.MapPost($"/suscripciones/{{id}}/{ruta}", async (long id, MotivoSuscripcionRequest? request,
            ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var errores = ValidacionAuth.ValidarMotivo(request?.Motivo, motivoObligatorio);
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasP4p.MensajeRevisar, errores));
            var motivo = ValidacionAuth.NormalizarOpcional(request?.Motivo);
            var adminId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var ahora = reloj.GetUtcNow().UtcDateTime;

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var s = await CargarBloqueadaAsync(db, id, ct);
            if (s is null) return NoEncontrado();
            if (!puede(s)) return Conflicto(verbo, s, ahora);
            var antes = Datos(s);
            aplicar(s, ahora);
            Auditar(db, adminId, s, accion, antes, Datos(s), motivo, ahora);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(await ItemAsync(db, s.Id, ahora, ct));
        });
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Ayudas
    // ---------------------------------------------------------------------------------------------------------------

    private sealed class Fila
    {
        public Suscripcion S { get; init; } = null!;
        public Plan Plan { get; init; } = null!;
        public string AsesorNombre { get; init; } = "";
    }

    private static IQueryable<Fila> Filas(CamionDbContext db, IQueryable<Suscripcion> consulta) =>
        from s in consulta
        join pl in db.Planes on s.PlanId equals pl.Id
        join a in db.PerfilesAsesor on s.AsesorId equals a.Id
        join u in db.Users on a.UsuarioId equals u.Id
        select new Fila { S = s, Plan = pl, AsesorNombre = u.NombreCompleto };

    private static SuscripcionItem Item(Fila f, DateTime ahora) => new(
        f.S.Id,
        new AsesorSuscripcion(f.S.AsesorId, f.AsesorNombre),
        new PlanSuscripcion(f.Plan.Id, f.Plan.Nombre, f.Plan.LimitePublicaciones, ReglasP4p.DiasVigencia(f.Plan.DuracionMeses)),
        f.S.Estado,
        ReglasP4p.EstadoEfectivo(f.S, ahora),
        Utc(f.S.InicioVigencia),
        Utc(f.S.FinVigencia),
        ReglasP4p.DiasRestantes(f.S, ahora),
        ReglasP4p.Vigente(f.S, ahora),
        Utc(f.S.AprobadoEn),
        ConsultasCatalogo.Utc(f.S.CreadoEn));

    private static async Task<SuscripcionItem> ItemAsync(CamionDbContext db, long id, DateTime ahora, CancellationToken ct) =>
        Item(await Filas(db, db.Suscripciones.AsNoTracking().Where(s => s.Id == id)).FirstAsync(ct), ahora);

    private static DateTime? Utc(DateTime? valor) => valor is null ? null : ConsultasCatalogo.Utc(valor.Value);

    private static Suscripcion Nueva(long asesorId, Plan plan, string? adminId, DateTime ahora) => new()
    {
        AsesorId = asesorId,
        PlanId = plan.Id,
        Estado = Suscripcion.Activa,
        InicioVigencia = ahora,
        FinVigencia = ahora.AddDays(ReglasP4p.DiasVigencia(plan.DuracionMeses)),
        AprobadoPor = adminId,
        AprobadoEn = ahora,
        CreadoEn = ahora
    };

    // Toma la suscripción (con seguimiento) después de bloquear la fila de su asesor dentro de la transacción del llamador.
    private static async Task<Suscripcion?> CargarBloqueadaAsync(CamionDbContext db, long id, CancellationToken ct)
    {
        var asesorId = await db.Suscripciones.AsNoTracking().Where(s => s.Id == id).Select(s => (long?)s.AsesorId)
            .FirstOrDefaultAsync(ct);
        if (asesorId is null) return null;
        await BloquearAsesorAsync(db, asesorId.Value, ct);
        return await db.Suscripciones.SingleOrDefaultAsync(s => s.Id == id, ct);
    }

    // UPDATE sin cambio real: en MariaDB (InnoDB) deja la fila de perfiles_asesor bloqueada hasta el COMMIT.
    private static Task<int> BloquearAsesorAsync(CamionDbContext db, long asesorId, CancellationToken ct) =>
        db.PerfilesAsesor.Where(p => p.Id == asesorId)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.EstadoAutorizacion, p => p.EstadoAutorizacion), ct);

    // Cuenta ACTIVA con rol ASESOR (id = perfiles_asesor.id).
    private static Task<bool> AsesorActivoAsync(CamionDbContext db, long asesorId, CancellationToken ct) =>
        (from a in db.PerfilesAsesor
         join u in db.Users on a.UsuarioId equals u.Id
         where a.Id == asesorId && u.EstadoCuenta == EstadosCuenta.Activa &&
               db.UserRoles.Any(ur => ur.UserId == u.Id &&
                   db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == Roles.Asesor))
         select a.Id).AnyAsync(ct);

    private static Dictionary<string, object?> Datos(Suscripcion s) => new()
    {
        ["asesorId"] = s.AsesorId,
        ["estado"] = s.Estado,
        ["planId"] = s.PlanId,
        ["inicioVigencia"] = Utc(s.InicioVigencia),
        ["finVigencia"] = Utc(s.FinVigencia)
    };

    private static void Auditar(CamionDbContext db, string? adminId, Suscripcion s, string accion,
        Dictionary<string, object?>? antes, Dictionary<string, object?> despues, string? motivo, DateTime ahora)
    {
        if (motivo is not null) despues["motivo"] = motivo;
        Auditoria.AgregarEvento(db, adminId, Auditoria.EntidadSuscripcion, s.Id.ToString(CultureInfo.InvariantCulture),
            accion, antes, despues, ahora);
    }

    private static IResult Conflicto(string accion, Suscripcion s, DateTime ahora) =>
        Results.Conflict(new ErrorResponse(ReglasP4p.MensajeTransicion(accion, ReglasP4p.EstadoEfectivo(s, ahora))));

    private static IResult ErrorPlan(string mensaje) =>
        Results.BadRequest(new ErrorResponse(ReglasP4p.MensajeRevisar,
            new Dictionary<string, string[]> { ["planId"] = [mensaje] }));

    private static IResult NoEncontrado() => Results.NotFound(new ErrorResponse(ReglasP4p.MensajeNoEncontrado));

    private static long? LeerEntero(string? valor, long minimo, long maximo, string campo, string mensaje,
        Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (long.TryParse(valor.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= minimo && n <= maximo)
            return n;
        errores[campo] = [mensaje];
        return null;
    }
}
