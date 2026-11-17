using System.Text;

namespace Camion.Api.Seo;

/// <summary>
/// P10 (D-88 A; CA-P10-75, CA-P10-77): el index.html publicado del frontend como plantilla de /vehiculo/{slug}.
/// Se vuelve a leer cuando cambia la fecha o el tamaño del archivo (una publicación nueva del frontend se ve sin reiniciar).
/// Solo se cambia lo que hay entre <see cref="MarcaInicio"/> y <see cref="MarcaFin"/>; el resto queda igual.
/// </summary>
public sealed class PlantillaPortal(OpcionesPortal opciones, ILogger<PlantillaPortal> log)
{
    public const string MarcaInicio = "<!--camion:meta:inicio-->";
    public const string MarcaFin = "<!--camion:meta:fin-->";

    private readonly object _candado = new();
    private Leida? _actual;

    /// <summary>Plantilla leída. <see cref="Bloque"/> es null si el archivo no tiene las dos marcas.</summary>
    public sealed record Leida(DateTime Fecha, long Largo, string Completo, string Antes, string? Bloque, string Despues)
    {
        /// <summary>El archivo con el bloque reemplazado; sin marcas, el archivo tal cual.</summary>
        public string Con(Func<string, string> bloqueNuevo) =>
            Bloque is null ? Completo : Antes + MarcaInicio + bloqueNuevo(Bloque) + MarcaFin + Despues;
    }

    /// <summary>La plantilla vigente, o null si no se puede leer (se escribe el error en el log, sin la ruta).</summary>
    public Leida? Obtener()
    {
        var ruta = opciones.RutaIndexHtml;
        if (ruta is null) return null;
        try
        {
            var info = new FileInfo(ruta);
            if (!info.Exists)
            {
                log.LogError("No se encuentra el index.html del frontend ({Clave}); /vehiculo/ responde 503.",
                    ConfiguracionPortal.ClaveRutaIndexHtml);
                return null;
            }
            lock (_candado)
            {
                if (_actual is { } a && a.Fecha == info.LastWriteTimeUtc && a.Largo == info.Length) return a;
                var texto = File.ReadAllText(ruta, Encoding.UTF8);
                _actual = Partir(info.LastWriteTimeUtc, info.Length, texto);
                if (_actual.Bloque is null)
                    log.LogWarning("El index.html del frontend no tiene las marcas {Inicio} y {Fin}: /vehiculo/ lo entrega " +
                                   "sin las etiquetas del vehículo.", MarcaInicio, MarcaFin);
                return _actual;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.LogError("No se pudo leer el index.html del frontend ({Clave}): {Tipo}; /vehiculo/ responde 503.",
                ConfiguracionPortal.ClaveRutaIndexHtml, ex.GetType().FullName);
            return null;
        }
    }

    public static Leida Partir(DateTime fecha, long largo, string texto)
    {
        var i = texto.IndexOf(MarcaInicio, StringComparison.Ordinal);
        var f = i < 0 ? -1 : texto.IndexOf(MarcaFin, i + MarcaInicio.Length, StringComparison.Ordinal);
        if (i < 0 || f < 0) return new(fecha, largo, texto, "", null, "");
        return new(fecha, largo, texto, texto[..i], texto[(i + MarcaInicio.Length)..f], texto[(f + MarcaFin.Length)..]);
    }
}
