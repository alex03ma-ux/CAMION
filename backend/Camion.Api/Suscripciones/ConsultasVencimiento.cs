using Camion.Api.Data;
using Camion.Api.Models;
using Camion.Api.Publicaciones;

namespace Camion.Api.Suscripciones;

// P7 (O-5, CA-P7-46): la misma consulta para el contador del panel y para la lista
// GET /api/admin/suscripciones?vista=..., de modo que el número y el total de la lista coinciden.
public static class ConsultasVencimiento
{
    public const string VistaPorVencer = "porVencer";
    public const string VistaVencidasSinRenovar = "vencidasSinRenovar";
    public static readonly string[] Vistas = [VistaPorVencer, VistaVencidasSinRenovar];

    /// <summary>Vigentes con fin dentro de los próximos 7 días (diasRestantes &lt;= 7).</summary>
    public static IQueryable<Suscripcion> PorVencer(CamionDbContext db, IQueryable<Suscripcion> consulta, DateTime ahora)
    {
        var limite = ahora.AddDays(ReglasP4p.DiasAvisoVencimiento);
        return consulta.Where(s => (s.Estado == Suscripcion.Activa || s.Estado == Suscripcion.Reactivada) &&
                                   s.InicioVigencia != null && s.InicioVigencia <= ahora &&
                                   s.FinVigencia != null && s.FinVigencia > ahora && s.FinVigencia <= limite);
    }

    /// <summary>Estado efectivo VENCIDA y el asesor no tiene hoy una suscripción vigente.</summary>
    public static IQueryable<Suscripcion> VencidasSinRenovar(CamionDbContext db, IQueryable<Suscripcion> consulta, DateTime ahora)
    {
        var vigentes = DatosP4b.SuscripcionesVigentes(db, ahora);
        return consulta.Where(s =>
            (s.Estado == Suscripcion.Vencida ||
             ((s.Estado == Suscripcion.Activa || s.Estado == Suscripcion.Reactivada) &&
              s.FinVigencia != null && s.FinVigencia <= ahora)) &&
            !vigentes.Any(v => v.AsesorId == s.AsesorId));
    }

    /// <summary>Nombre canónico de la vista (sin distinguir mayúsculas) o null si no es válida.</summary>
    public static string? Normalizar(string valor) =>
        Vistas.FirstOrDefault(v => string.Equals(v, valor.Trim(), StringComparison.OrdinalIgnoreCase));
}
