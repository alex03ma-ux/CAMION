using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Catalogo;

// P3: ÚNICO lugar con la regla de visibilidad pública (RN-VE-013, RN-VE-014, D-11) y la de destacado
// (RN-VE-011, RN-VE-012). Todos los endpoints del catálogo parten de aquí.
public static class ConsultasCatalogo
{
    /// <summary>
    /// Publicaciones visibles en el instante <paramref name="ahora"/> (UTC):
    /// 1. PUBLICADA; 2. DISPONIBLE o RESERVADO (un vendido nunca se ve); 3. vigente (historial_vigencias con
    /// inicio &lt;= ahora &lt; fin); 4. asesor dueño con cuenta ACTIVA; 5. precio &gt; 0 y título no vacío
    /// (y publicada_en con valor, que la CHECK CK_Pub_Publicable ya exige); 6. la suscripción de la fila
    /// vigente está ACTIVA con fin_vigencia nula o posterior a ahora; 7 (P4a, D-4/D-11): el vehículo está
    /// ACTIVO o RESERVADO (un vehículo INACTIVO o VENDIDO nunca se ve, aunque su publicación siga PUBLICADA).
    /// </summary>
    public static IQueryable<Publicacion> Visibles(CamionDbContext db, DateTime ahora) =>
        db.Publicaciones.AsNoTracking().Where(p =>
            p.EstadoPublicacion == Publicacion.Publicada &&
            (p.EstadoComercial == Publicacion.Disponible || p.EstadoComercial == Publicacion.Reservado) &&
            p.Vigencias.Any(v => v.Inicio <= ahora && v.Fin > ahora &&
                // 6 (aclaración C-P3-2): la suscripción de esa vigencia sigue ACTIVA y sin vencer.
                db.Suscripciones.Any(s => s.Id == v.SuscripcionId && s.Estado == Suscripcion.Activa &&
                                          (s.FinVigencia == null || s.FinVigencia > ahora))) &&
            db.PerfilesAsesor.Any(a => a.Id == p.AsesorId &&
                db.Users.Any(u => u.Id == a.UsuarioId && u.EstadoCuenta == EstadosCuenta.Activa)) &&
            p.PrecioCop != null && p.PrecioCop > 0 &&
            p.Titulo != null && p.Titulo.Trim() != "" &&
            p.PublicadaEn != null &&
            (p.Vehiculo!.Estado == Vehiculo.Activo || p.Vehiculo.Estado == Vehiculo.Reservado));

    /// <summary>Visibles con la marca de destacado: fila ACTIVA en publicaciones_destacadas con inicio &lt;= ahora &lt; fin.</summary>
    public static IQueryable<PublicacionCatalogo> VisiblesConDestacado(CamionDbContext db, DateTime ahora) =>
        Visibles(db, ahora).Select(p => new PublicacionCatalogo
        {
            Publicacion = p,
            Destacado = p.Destacados.Any(d => d.Estado == PublicacionDestacada.Activa && d.Inicio <= ahora && d.Fin > ahora)
        });

    /// <summary>Proyección directa a los campos de la tarjeta (sin cargar entidades ni datos de usuarios).</summary>
    public static IQueryable<FilaTarjeta> ATarjetas(IQueryable<PublicacionCatalogo> consulta) =>
        consulta.Select(x => new FilaTarjeta
        {
            Id = x.Publicacion.Id,
            Slug = x.Publicacion.SlugPublico,
            Titulo = x.Publicacion.Titulo!,
            PrecioCop = (decimal)x.Publicacion.PrecioCop!,
            Negociable = x.Publicacion.Negociable,
            // P4a: un vehículo RESERVADO se muestra como reservado aunque la publicación diga DISPONIBLE.
            EstadoComercial = x.Publicacion.Vehiculo!.Estado == Vehiculo.Reservado
                ? Publicacion.Reservado
                : x.Publicacion.EstadoComercial,
            Destacado = x.Destacado,
            Tipo = x.Publicacion.Vehiculo.TipoVehiculo!.Nombre,
            // P4a (D-19): una carrocería puede no tener marca ni año; la placa nunca sale en el catálogo.
            Marca = x.Publicacion.Vehiculo.Marca == null ? null : x.Publicacion.Vehiculo.Marca.Nombre,
            Linea = x.Publicacion.Vehiculo.Linea!.Nombre,
            ModeloAnio = x.Publicacion.Vehiculo.ModeloAnio,
            Kilometraje = x.Publicacion.Vehiculo.Kilometraje,
            Municipio = x.Publicacion.Vehiculo.Municipio!.Nombre,
            Departamento = x.Publicacion.Vehiculo.Municipio.Departamento!.Nombre,
            // Portada: la marcada con es_portada; si ninguna, la de menor orden; si no hay fotos, null.
            FotoClave = x.Publicacion.Fotos
                .OrderByDescending(f => f.EsPortada).ThenBy(f => f.Orden)
                .Select(f => f.ArchivoClave).FirstOrDefault(),
            PublicadaEn = (DateTime)x.Publicacion.PublicadaEn!
        });

    public static TarjetaVehiculo Tarjeta(FilaTarjeta f) => new(
        f.Slug, f.Titulo, Pesos(f.PrecioCop), f.Negociable, f.EstadoComercial, f.Destacado,
        f.Tipo, f.Marca, f.Linea, f.ModeloAnio, f.Kilometraje, f.Municipio, f.Departamento,
        UrlFoto(f.FotoClave), Utc(f.PublicadaEn));

    // precioCop es un entero en COP: se redondea el DECIMAL(15,2).
    public static long Pesos(decimal valor) => (long)Math.Round(valor, MidpointRounding.AwayFromZero);

    public static DateTime Utc(DateTime valor) => DateTime.SpecifyKind(valor, DateTimeKind.Utc);

    // /media/ + archivo_clave con cada segmento codificado para URL (el servicio de archivos llega en P4).
    public static string? UrlFoto(string? archivoClave) =>
        string.IsNullOrWhiteSpace(archivoClave)
            ? null
            : "/media/" + string.Join('/', archivoClave.Replace('\\', '/').Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
}

public sealed class PublicacionCatalogo
{
    public Publicacion Publicacion { get; init; } = null!;
    public bool Destacado { get; init; }
}

public sealed class FilaTarjeta
{
    public long Id { get; init; }
    public string Slug { get; init; } = "";
    public string Titulo { get; init; } = "";
    public decimal PrecioCop { get; init; }
    public bool Negociable { get; init; }
    public string EstadoComercial { get; init; } = "";
    public bool Destacado { get; init; }
    public string Tipo { get; init; } = "";
    public string? Marca { get; init; }
    public string? Linea { get; init; }
    public int? ModeloAnio { get; init; }
    public int Kilometraje { get; init; }
    public string Municipio { get; init; } = "";
    public string Departamento { get; init; } = "";
    public string? FotoClave { get; init; }
    public DateTime PublicadaEn { get; init; }
}
