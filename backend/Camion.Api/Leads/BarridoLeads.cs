using System.Globalization;
using Camion.Api.Auth;
using Camion.Api.Data;
using Camion.Api.Endpoints;
using Camion.Api.Models;
using Camion.Api.Publicaciones;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Leads;

/// <summary>
/// P5: a quién va un lead (D-7, CA-P5-25 a CA-P5-28) y cómo cambia después (D-46 B, D-34, CA-P5-50 a CA-P5-53).
/// Un único lugar para la regla, usado por el contacto público, las rutas de /api/leads, el bloqueo de un asesor y la
/// reasignación de un vehículo. El barrido es bajo demanda (no hay proceso en segundo plano) e idempotente: cada lead se
/// mueve con un UPDATE condicionado a su responsable y estado actuales, y solo si ese UPDATE afecta a la fila se escriben
/// el evento y la auditoría.
/// </summary>
public static class BarridoLeads
{
    /// <summary>
    /// CA-P5-25 a CA-P5-27: el asesor dueño si su perfil está APROBADO, su cuenta ACTIVA con rol ASESOR, no es
    /// administrador ("publicación a cargo del administrador", D-47) y tiene suscripción vigente (D-41). Si no, null
    /// (Administración).
    /// </summary>
    public static async Task<long?> ResponsableAlCrearAsync(CamionDbContext db, long asesorId, DateTime ahora, CancellationToken ct)
    {
        var dueno = await db.PerfilesAsesor.AsNoTracking().Where(a => a.Id == asesorId)
            .Select(a => new { a.UsuarioId, a.EstadoAutorizacion }).FirstOrDefaultAsync(ct);
        if (dueno is null || dueno.EstadoAutorizacion != PerfilAsesor.Aprobado) return null;
        if (!await InventarioEndpoints.AsesorActivoAsync(db, asesorId, ct)) return null;
        if (await EsAdministradorAsync(db, dueno.UsuarioId, ct)) return null;
        if (await DatosP4b.SuscripcionVigenteAsync(db, asesorId, ahora, ct) is null) return null;
        return asesorId;
    }

    public static Task<bool> EsAdministradorAsync(CamionDbContext db, string usuarioId, CancellationToken ct) =>
        db.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == usuarioId &&
            db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == Roles.Administrador), ct);

    /// <summary>
    /// D-46 (B), CA-P5-50, CA-P5-51: pasa a Administración los leads ABIERTO de asesores con la cuenta no ACTIVA o el
    /// perfil no APROBADO ("Asesor bloqueado") o sin suscripción vigente ("Plan vencido"; salvo los que llegaron por
    /// reasignación manual, CA-P5-47). Actor SISTEMA. Abre y confirma su propia transacción solo si hay algo que mover.
    /// </summary>
    public static async Task<int> BarrerAsync(CamionDbContext db, DateTime ahora, CancellationToken ct)
    {
        var vigentes = DatosP4b.SuscripcionesVigentes(db, ahora);
        var candidatos = await db.Leads.AsNoTracking()
            .Where(l => l.Estado == Lead.Abierto && l.ResponsableAsesorId != null)
            .Select(l => new
            {
                l.Id,
                Asesor = l.ResponsableAsesorId!.Value,
                l.AsignacionManual,
                CuentaActiva = db.PerfilesAsesor.Any(a => a.Id == l.ResponsableAsesorId &&
                    a.EstadoAutorizacion == PerfilAsesor.Aprobado &&
                    db.Users.Any(u => u.Id == a.UsuarioId && u.EstadoCuenta == EstadosCuenta.Activa)),
                ConPlan = vigentes.Any(s => s.AsesorId == l.ResponsableAsesorId)
            })
            .Where(x => !x.CuentaActiva || (!x.AsignacionManual && !x.ConPlan))
            .ToListAsync(ct);
        if (candidatos.Count == 0) return 0;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var movidos = 0;
        foreach (var c in candidatos)
            movidos += await PasarAAdministracionAsync(db, c.Id, c.Asesor,
                c.CuentaActiva ? ReglasLeads.MotivoPlanVencido : ReglasLeads.MotivoAsesorBloqueado, null, ahora, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return movidos;
    }

    /// <summary>
    /// CA-P5-51: al bloquear a un asesor, todos sus leads ABIERTO pasan a Administración (actor SISTEMA), dentro de la
    /// transacción del llamador. El llamador guarda (SaveChanges) el evento y la auditoría.
    /// </summary>
    public static async Task<int> PasarAsesorAAdministracionAsync(CamionDbContext db, long asesorId, string motivo,
        DateTime ahora, CancellationToken ct)
    {
        var ids = await db.Leads.AsNoTracking()
            .Where(l => l.Estado == Lead.Abierto && l.ResponsableAsesorId == asesorId)
            .Select(l => l.Id).ToListAsync(ct);
        var movidos = 0;
        foreach (var id in ids)
            movidos += await PasarAAdministracionAsync(db, id, asesorId, motivo, null, ahora, ct);
        return movidos;
    }

    /// <summary>
    /// CA-P5-52 (D-34, D-46 B): los leads ABIERTO de las publicaciones del vehículo que estaban con el dueño anterior pasan
    /// al nuevo dueño, con su evento (actor = el administrador que reasignó el vehículo) y su auditoría. Los CERRADOS, los
    /// de Administración y los reasignados a mano a otro asesor no cambian. Dentro de la transacción del llamador.
    /// </summary>
    public static async Task<int> SeguirVehiculoAsync(CamionDbContext db, long vehiculoId, long anterior, long destino,
        string actorId, DateTime ahora, CancellationToken ct)
    {
        var ids = await db.Leads.AsNoTracking()
            .Where(l => l.Estado == Lead.Abierto && l.ResponsableAsesorId == anterior &&
                        db.Publicaciones.Any(p => p.Id == l.PublicacionId && p.VehiculoId == vehiculoId))
            .Select(l => l.Id).ToListAsync(ct);
        var movidos = 0;
        foreach (var id in ids)
        {
            var n = await db.Leads.Where(l => l.Id == id && l.ResponsableAsesorId == anterior && l.Estado == Lead.Abierto)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(l => l.ResponsableAsesorId, (long?)destino)
                    .SetProperty(l => l.AsignacionManual, false)
                    .SetProperty(l => l.VistoPorResponsable, false)
                    .SetProperty(l => l.ActualizadoEn, ahora), ct);
            if (n == 0) continue;
            AgregarReasignacion(db, id, actorId, anterior, destino, ReglasLeads.TextoVehiculoReasignado, automatico: true, ahora,
                extra: new() { ["vehiculoId"] = vehiculoId });
            movidos++;
        }
        return movidos;
    }

    private static async Task<int> PasarAAdministracionAsync(CamionDbContext db, long leadId, long anterior, string motivo,
        string? actorId, DateTime ahora, CancellationToken ct)
    {
        var n = await db.Leads.Where(l => l.Id == leadId && l.ResponsableAsesorId == anterior && l.Estado == Lead.Abierto)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.ResponsableAsesorId, (long?)null)
                .SetProperty(l => l.AsignacionManual, false)
                .SetProperty(l => l.VistoPorResponsable, false)
                .SetProperty(l => l.ActualizadoEn, ahora), ct);
        if (n == 0) return 0;
        AgregarReasignacion(db, leadId, actorId, anterior, null, motivo, automatico: true, ahora);
        return 1;
    }

    /// <summary>Evento REASIGNADO del historial y fila Lead / REASIGNAR de auditoría (sin datos del visitante).</summary>
    public static void AgregarReasignacion(CamionDbContext db, long leadId, string? actorId, long? anterior, long? nuevo,
        string motivo, bool automatico, DateTime ahora, Dictionary<string, object?>? extra = null)
    {
        db.EventosLead.Add(new EventoLead
        {
            LeadId = leadId,
            Tipo = EventoLead.Reasignado,
            ActorUsuarioId = actorId,
            Automatico = automatico,
            ResponsableAnteriorAsesorId = anterior,
            ResponsableNuevoAsesorId = nuevo,
            Texto = motivo,
            CreadoEn = ahora
        });
        var nuevos = new Dictionary<string, object?>
        {
            ["responsableAsesorId"] = nuevo,
            ["motivo"] = motivo,
            ["automatico"] = automatico
        };
        if (extra is not null)
            foreach (var (k, v) in extra) nuevos[k] = v;
        Auditoria.AgregarEvento(db, actorId, Auditoria.EntidadLead, Id(leadId), Auditoria.Reasignar,
            new Dictionary<string, object?> { ["responsableAsesorId"] = anterior }, nuevos, ahora);
    }

    public static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);
}
