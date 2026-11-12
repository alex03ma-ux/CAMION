using Camion.Api.Data;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Publicaciones;

// P4b: consultas compartidas de plan vigente, cupo (D-40) y destacados (D-38). Un único lugar para cada regla, usado por
// las publicaciones, la venta, la reasignación, el inventario, el catálogo público y la renovación de P4p.
public static class DatosP4b
{
    /// <summary>
    /// D-21, D-41: suscripción vigente = ACTIVA o REACTIVADA, con inicio &lt;= ahora y fin &gt; ahora (igual que
    /// ReglasP4p.Vigente). Se usa como subconsulta: EF la inserta en la consulta que la referencia.
    /// </summary>
    public static IQueryable<Suscripcion> SuscripcionesVigentes(CamionDbContext db, DateTime ahora) =>
        db.Suscripciones.AsNoTracking().Where(s =>
            (s.Estado == Suscripcion.Activa || s.Estado == Suscripcion.Reactivada) &&
            s.InicioVigencia != null && s.InicioVigencia <= ahora &&
            s.FinVigencia != null && s.FinVigencia > ahora);

    /// <summary>La suscripción vigente del asesor (con su plan) o null. P4p garantiza como máximo una viva.</summary>
    public static Task<Suscripcion?> SuscripcionVigenteAsync(CamionDbContext db, long asesorId, DateTime ahora,
        CancellationToken ct) =>
        SuscripcionesVigentes(db, ahora).Include(s => s.Plan).Where(s => s.AsesorId == asesorId)
            .OrderByDescending(s => s.FinVigencia).ThenByDescending(s => s.Id).FirstOrDefaultAsync(ct);

    /// <summary>D-40: cuentan todas las publicaciones del asesor salvo las ELIMINADA.</summary>
    public static Task<int> CupoUsadoAsync(CamionDbContext db, long asesorId, CancellationToken ct) =>
        db.Publicaciones.AsNoTracking().CountAsync(p => p.AsesorId == asesorId && p.EstadoPublicacion != Publicacion.Eliminada, ct);

    /// <summary>Destacados que cuentan (D-38): ACTIVA, ya iniciados y sin vencer, de publicaciones no eliminadas del asesor.</summary>
    public static IQueryable<PublicacionDestacada> DestacadosVigentes(CamionDbContext db, long asesorId, DateTime ahora) =>
        db.PublicacionesDestacadas.AsNoTracking().Where(d =>
            d.Estado == PublicacionDestacada.Activa && d.Inicio <= ahora && d.Fin > ahora &&
            db.Publicaciones.Any(p => p.Id == d.PublicacionId && p.AsesorId == asesorId &&
                                      p.EstadoPublicacion != Publicacion.Eliminada));

    /// <summary>
    /// Quita (FINALIZADA, fin = ahora) los destacados ACTIVA de la publicación, en el contexto del llamador. Devuelve cuántos.
    /// </summary>
    public static async Task<int> QuitarDestacadosAsync(CamionDbContext db, long publicacionId, DateTime ahora, CancellationToken ct)
    {
        var activos = await db.PublicacionesDestacadas
            .Where(d => d.PublicacionId == publicacionId && d.Estado == PublicacionDestacada.Activa).ToListAsync(ct);
        foreach (var d in activos)
        {
            d.Estado = PublicacionDestacada.Finalizada;
            d.Fin = ReglasP4b.FinAlCerrar(d.Inicio, d.Fin, ahora);
        }
        return activos.Count;
    }

    /// <summary>
    /// D-38, D-41: el destacado dura hasta el fin del plan. Al renovar el plan (o cambiarlo) los destacados que siguen
    /// vigentes se extienden al nuevo fin, igual que las publicaciones siguen visibles sin hacer nada.
    /// </summary>
    public static Task<int> ExtenderDestacadosAsync(CamionDbContext db, long asesorId, DateTime nuevoFin, DateTime ahora,
        CancellationToken ct) =>
        db.PublicacionesDestacadas.Where(d =>
                d.Estado == PublicacionDestacada.Activa && d.Inicio <= ahora && d.Fin > ahora && d.Fin < nuevoFin &&
                db.Publicaciones.Any(p => p.Id == d.PublicacionId && p.AsesorId == asesorId &&
                                          p.EstadoPublicacion != Publicacion.Eliminada))
            .ExecuteUpdateAsync(x => x.SetProperty(d => d.Fin, nuevoFin), ct);

    /// <summary>UPDATE sin cambio real: en MariaDB (InnoDB) deja la fila del asesor bloqueada hasta el COMMIT (cupo y destacados).</summary>
    public static Task<int> BloquearAsesorAsync(CamionDbContext db, long asesorId, CancellationToken ct) =>
        db.PerfilesAsesor.Where(p => p.Id == asesorId)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.EstadoAutorizacion, p => p.EstadoAutorizacion), ct);

    /// <summary>UPDATE sin cambio real sobre el vehículo: bloquea su fila hasta el COMMIT (una publicación por vehículo, venta).</summary>
    public static Task<int> BloquearVehiculoAsync(CamionDbContext db, long vehiculoId, CancellationToken ct) =>
        db.Vehiculos.Where(v => v.Id == vehiculoId)
            .ExecuteUpdateAsync(x => x.SetProperty(v => v.Estado, v => v.Estado), ct);
}
