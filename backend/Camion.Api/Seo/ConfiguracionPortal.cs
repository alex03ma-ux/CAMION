using Camion.Api.Auth;

namespace Camion.Api.Seo;

/// <summary>
/// P10 (CA-P10-58, CA-P10-70, CA-P10-77; D-88 A, D-89 A): configuración pública del portal para buscadores y redes.
/// Ninguna clave es secreta. Se valida al arrancar (como Recuperacion:UrlPortal): si algo no es válido, la API no arranca y
/// el mensaje solo nombra la clave, nunca su valor ni rutas del servidor.
/// </summary>
public sealed record OpcionesPortal(string UrlPublica, bool PermitirIndexacion, string? RutaIndexHtml);

public static class ConfiguracionPortal
{
    /// <summary>URL pública (sitemap, robots, canonical y Open Graph). Si falta, se usa Recuperacion:UrlPortal.</summary>
    public const string ClaveUrlPublica = "Portal:UrlPublica";
    /// <summary>D-89 A: false por defecto (robots.txt "Disallow: /", sin sitemap y fichas con noindex).</summary>
    public const string ClavePermitirIndexacion = "Portal:PermitirIndexacion";
    /// <summary>D-88 A: ruta del index.html publicado del frontend; vacía = la API no atiende /vehiculo/{slug}.</summary>
    public const string ClaveRutaIndexHtml = "Portal:RutaIndexHtml";

    public static OpcionesPortal Leer(IConfiguration c, bool esDesarrollo, string contentRoot) => new(
        UrlPublica(c, esDesarrollo),
        PermitirIndexacion(c[ClavePermitirIndexacion]),
        RutaIndexHtml(c[ClaveRutaIndexHtml], esDesarrollo, contentRoot));

    /// <summary>
    /// CA-P10-58: Portal:UrlPublica o, si falta, Recuperacion:UrlPortal (por defecto http://localhost:5173). Absoluta, http(s),
    /// sin ruta, consulta, fragmento ni usuario; se devuelve sin barra final. Fuera de Development debe ser https://.
    /// No se usa Portal:Origen, que activa CORS.
    /// </summary>
    public static string UrlPublica(IConfiguration c, bool esDesarrollo)
    {
        var propia = c[ClaveUrlPublica]?.Trim();
        var (clave, valor) = string.IsNullOrEmpty(propia)
            ? (ConfiguracionRecuperacion.ClaveUrlPortal,
                (c[ConfiguracionRecuperacion.ClaveUrlPortal] ?? new OpcionesRecuperacion().UrlPortal).Trim())
            : (ClaveUrlPublica, propia);
        if (!Uri.TryCreate(valor, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException(
                $"{clave} debe ser una dirección absoluta sin ruta, consulta ni fragmento (https://dominio), " +
                $"porque se usa como URL pública del portal ({ClaveUrlPublica}).");
        if (!esDesarrollo && uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"{clave} debe empezar por https:// fuera de Development ({ClaveUrlPublica}).");
        return uri.GetLeftPart(UriPartial.Authority);
    }

    public static bool PermitirIndexacion(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return false;
        return bool.TryParse(valor.Trim(), out var si)
            ? si
            : throw new InvalidOperationException($"{ClavePermitirIndexacion} debe ser true o false.");
    }

    /// <summary>
    /// CA-P10-70, CA-P10-77: ruta absoluta o relativa a la carpeta de la API. Fuera de Development, si está puesta y el archivo
    /// no existe, la API no arranca. En Development arranca igual y la ruta responde 503 hasta que exista (npm run build).
    /// </summary>
    public static string? RutaIndexHtml(string? valor, bool esDesarrollo, string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var ruta = Path.GetFullPath(Path.Combine(contentRoot, valor.Trim()));
        // B-2 (revisión de seguridad P10): solo un .html, en cualquier entorno, para no servir por error otro archivo
        // (por ejemplo appsettings.json) en /vehiculo/*.
        if (!string.Equals(Path.GetExtension(ruta), ".html", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{ClaveRutaIndexHtml} debe apuntar a un archivo .html (el index.html publicado).");
        if (!esDesarrollo && !File.Exists(ruta))
            throw new InvalidOperationException(
                $"{ClaveRutaIndexHtml}: no existe el index.html publicado del frontend. Revisa la clave o quítala para " +
                "desactivar la vista previa de las fichas.");
        if (File.Exists(ruta) && new FileInfo(ruta).Length > MaxBytesIndexHtml)
            throw new InvalidOperationException(
                $"{ClaveRutaIndexHtml}: el archivo pasa de {MaxBytesIndexHtml / 1024} KB; no parece el index.html del frontend.");
        return ruta;
    }

    /// <summary>B-2: tope de tamaño de la plantilla, al arrancar y al releerla (el index.html de Vite pesa unos pocos KB).</summary>
    public const long MaxBytesIndexHtml = 512 * 1024;
}
