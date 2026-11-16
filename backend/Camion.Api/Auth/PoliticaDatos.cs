using System.Text.RegularExpressions;

namespace Camion.Api.Auth;

/// <summary>
/// P9 (RN-SE-004, D-44; CA-P9-62 a CA-P9-65): versión de la política de tratamiento de datos que se guarda en cada fila de
/// consentimientos_datos. Único punto de lectura para las seis rutas que la guardan (contacto, avisos, crédito, búsqueda,
/// registro de comprador e inscripción de asesor). Mientras D-44 no esté aprobado vale el borrador del 2026-11-04; el
/// cambio al aprobarse se documenta en docs/GUIA-PUBLICACION.md (variable de entorno PoliticaDatos__Version).
/// </summary>
public static partial class PoliticaDatos
{
    public const string Clave = "PoliticaDatos:Version";
    public const string VersionPorDefecto = "BORRADOR-2026-11-04";
    // Columna consentimientos_datos.version_politica VARCHAR(40).
    public const int MaxLongitud = 40;
    public const string AvisoBorrador =
        "La política de datos configurada es un borrador: no abrir el portal al público (PoliticaDatos:Version).";

    [GeneratedRegex(@"^[A-Z0-9.\-]+$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Formato();

    /// <summary>Versión vigente: la configurada o, si no hay ninguna, la del borrador (CA-P9-62, CA-P9-63).</summary>
    public static string Version(IConfiguration configuracion)
    {
        var version = configuracion[Clave];
        return string.IsNullOrWhiteSpace(version) ? VersionPorDefecto : version;
    }

    public static bool EsValida(string version) =>
        version.Length is > 0 and <= MaxLongitud && Formato().IsMatch(version);

    public static bool EsBorrador(string version) => version.StartsWith("BORRADOR", StringComparison.Ordinal);

    /// <summary>
    /// CA-P9-65: si la versión configurada no cumple el formato (máx. 40 caracteres; mayúsculas, dígitos, punto y guion),
    /// la API no arranca, como con Jwt:Key. El mensaje no repite el valor configurado.
    /// </summary>
    public static string ValidarAlIniciar(IConfiguration configuracion)
    {
        var version = Version(configuracion);
        if (!EsValida(version))
            throw new InvalidOperationException(
                $"{Clave} debe tener entre 1 y {MaxLongitud} caracteres, solo mayúsculas, dígitos, '.' y '-'. " +
                "Ver docs/GUIA-PUBLICACION.md.");
        return version;
    }
}
