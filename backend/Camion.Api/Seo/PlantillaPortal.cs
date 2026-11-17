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
    // Última versión vista del archivo (fecha y tamaño) y su resultado: la plantilla o null si se rechazó (sin marcas o
    // demasiado grande). Así el aviso del log sale una vez por versión del archivo y no en cada petición.
    private (DateTime Fecha, long Largo, Leida? Resultado)? _vista;

    /// <summary>Plantilla leída, siempre con las dos marcas.</summary>
    public sealed record Leida(DateTime Fecha, long Largo, string Completo, string Antes, string? Bloque, string Despues)
    {
        /// <summary>El archivo con el bloque reemplazado.</summary>
        public string Con(Func<string, string> bloqueNuevo) =>
            Bloque is null ? Completo : Antes + MarcaInicio + bloqueNuevo(Bloque) + MarcaFin + Despues;
    }

    /// <summary>
    /// La plantilla vigente, o null (la ruta responde 503) si no existe, no se puede leer, pasa de
    /// <see cref="ConfiguracionPortal.MaxBytesIndexHtml"/> o no tiene las dos marcas (B-2: nunca se sirve otro archivo tal cual).
    /// Los mensajes del log nombran la clave, nunca la ruta.
    /// </summary>
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
                if (_vista is { } v && v.Fecha == info.LastWriteTimeUtc && v.Largo == info.Length) return v.Resultado;
                Leida? resultado = null;
                if (info.Length > ConfiguracionPortal.MaxBytesIndexHtml)
                    log.LogError("El index.html del frontend ({Clave}) pasa de {Kb} KB; /vehiculo/ responde 503.",
                        ConfiguracionPortal.ClaveRutaIndexHtml, ConfiguracionPortal.MaxBytesIndexHtml / 1024);
                else
                {
                    var leida = Partir(info.LastWriteTimeUtc, info.Length, File.ReadAllText(ruta, Encoding.UTF8));
                    if (leida.Bloque is null)
                        log.LogWarning("El index.html del frontend ({Clave}) no tiene las marcas {Inicio} y {Fin}; " +
                                       "/vehiculo/ responde 503.", ConfiguracionPortal.ClaveRutaIndexHtml, MarcaInicio, MarcaFin);
                    else
                        resultado = leida;
                }
                _vista = (info.LastWriteTimeUtc, info.Length, resultado);
                return resultado;
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
