using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Camion.Api.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

namespace Camion.Api.Seguridad;

/// <summary>
/// INF-2: configuración del servidor para publicar el portal, sin atarse a un proveedor (D-81 pendiente).
/// Todo se lee de la configuración (variables de entorno en el servidor, user-secrets en local); nada es secreto
/// salvo la clave del certificado de DataProtection, que nunca se escribe en logs ni en mensajes de error.
/// </summary>
public static class ConfiguracionServidor
{
    // M-1 de P5: proxies de confianza. Sin ninguno configurado no se lee X-Forwarded-For (nunca se confía en cualquiera).
    public const string ClaveProxies = "Proxy:KnownProxies";
    public const string ClaveRedes = "Proxy:KnownNetworks";
    public const string ClaveLimiteReenvios = "Proxy:ForwardLimit";
    // Fuera de Development se redirige http -> https salvo que el proxy ya lo haga y se desactive aquí.
    public const string ClaveRedirigirHttps = "Https:RedirigirHttp";
    // CORS solo si el frontend se sirve desde otro origen (hoy usa rutas relativas: mismo origen, sin CORS).
    public const string ClaveOrigenPortal = "Portal:Origen";
    public const string ClaveRutaMedia = "Media:Ruta";
    // M-1 de P8: certificado para cifrar las claves de DataProtection en Linux o en un contenedor.
    public const string ClaveCertificadoRuta = "DataProtection:Certificado:Ruta";
    public const string ClaveCertificadoClave = "DataProtection:Certificado:Clave";
    public const string PoliticaCors = "PortalOrigen";

    /// <summary>
    /// Fuera de Development la API no arranca sin estas claves (ConnectionStrings y Jwt:Key ya se exigen en todos los
    /// entornos). Así no se usan en el servidor las rutas por defecto de Windows ni el enlace a localhost.
    /// El mensaje solo lleva nombres de claves, nunca valores.
    /// </summary>
    public static readonly string[] ObligatoriasFueraDeDesarrollo =
        [ClaveRutaMedia, ConfiguracionRecuperacion.ClaveRutaClaves, ConfiguracionRecuperacion.ClaveUrlPortal];

    public static void ValidarObligatorias(IConfiguration c, bool esDesarrollo)
    {
        if (esDesarrollo) return;
        var faltan = ObligatoriasFueraDeDesarrollo.Where(k => string.IsNullOrWhiteSpace(c[k])).ToArray();
        if (faltan.Length > 0)
            throw new InvalidOperationException(
                "Faltan claves obligatorias fuera de Development: " + string.Join(", ", faltan) +
                ". Configúralas como variables de entorno (por ejemplo Media__Ruta). Ver docs/GUIA-PUBLICACION.md.");
    }

    // ------------------------------------------------------------------ proxy inverso (M-1 de P5)

    /// <summary>
    /// Opciones de ForwardedHeaders con los proxies y redes de confianza de la configuración, o null si no hay ninguno
    /// (entonces no se llama a UseForwardedHeaders y todo queda como antes). Solo X-Forwarded-For y X-Forwarded-Proto;
    /// el Host no se toma del encabezado. Una IP o red mal escrita impide arrancar.
    /// </summary>
    public static ForwardedHeadersOptions? OpcionesProxy(IConfiguration c)
    {
        var proxies = Lista(c, ClaveProxies);
        var redes = Lista(c, ClaveRedes);
        if (proxies.Length == 0 && redes.Length == 0) return null;

        var o = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = LimiteReenvios(c[ClaveLimiteReenvios]),
            RequireHeaderSymmetry = false
        };
        // Por defecto ASP.NET confía en 127.0.0.1 y ::1; aquí solo vale lo configurado.
        o.KnownProxies.Clear();
        o.KnownNetworks.Clear();
        foreach (var p in proxies)
        {
            if (!IPAddress.TryParse(p, out var ip))
                throw new InvalidOperationException($"{ClaveProxies}: \"{p}\" no es una dirección IP válida.");
            o.KnownProxies.Add(ip);
        }
        foreach (var r in redes) o.KnownNetworks.Add(Red(r));
        return o;
    }

    private static int LimiteReenvios(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? 1
        : int.TryParse(valor, out var n) && n is >= 1 and <= 10 ? n
        : throw new InvalidOperationException($"{ClaveLimiteReenvios} debe ser un número entre 1 y 10.");

    /// <summary>Red en notación CIDR (10.0.0.0/8, fd00::/8).</summary>
    public static Microsoft.AspNetCore.HttpOverrides.IPNetwork Red(string cidr)
    {
        var partes = cidr.Split('/');
        if (partes.Length == 2 && IPAddress.TryParse(partes[0], out var prefijo) && int.TryParse(partes[1], out var largo) &&
            largo >= 0 && largo <= (prefijo.AddressFamily == AddressFamily.InterNetwork ? 32 : 128))
            return new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefijo, largo);
        throw new InvalidOperationException($"{ClaveRedes}: \"{cidr}\" no es una red válida (ejemplo: 10.0.0.0/8).");
    }

    /// <summary>
    /// Clave de partición del límite por IP. IPv4 (también la IPv4 mapeada en IPv6) tal cual; IPv6 agrupada por su
    /// prefijo /64, porque un mismo cliente suele tener todo un /64 y podría rotar direcciones para saltarse el límite.
    /// </summary>
    public static string ClaveIp(IPAddress? ip)
    {
        if (ip is null) return "sin-ip";
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily != AddressFamily.InterNetworkV6) return ip.ToString();
        var bytes = ip.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }

    // ------------------------------------------------------------------ CORS

    /// <summary>Origen del frontend si se sirve en otro dominio; null si no está configurado (sin CORS).</summary>
    public static string? OrigenPortal(IConfiguration c, bool esDesarrollo)
    {
        var valor = c[ClaveOrigenPortal]?.Trim();
        if (string.IsNullOrEmpty(valor)) return null;
        if (!Uri.TryCreate(valor, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            uri.PathAndQuery != "/" || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException($"{ClaveOrigenPortal} debe ser un origen sin ruta (https://dominio).");
        if (!esDesarrollo && uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"{ClaveOrigenPortal} debe empezar por https:// fuera de Development.");
        return uri.GetLeftPart(UriPartial.Authority);
    }

    // ------------------------------------------------------------------ DataProtection (M-1 de P8)

    /// <summary>
    /// Protege las claves de DataProtection en reposo:
    /// 1) con el certificado de DataProtection:Certificado:Ruta (.pfx) si está configurado (Linux, contenedor o Windows);
    /// 2) si no, con DPAPI en Windows (D-78);
    /// 3) si no (Linux sin certificado), las claves quedan sin cifrar y solo las protegen los permisos de la carpeta:
    ///    devuelve un aviso para el log.
    /// </summary>
    public static string? ProtegerClaves(IDataProtectionBuilder proteccion, IConfiguration c)
    {
        var ruta = c[ClaveCertificadoRuta]?.Trim();
        if (!string.IsNullOrEmpty(ruta))
        {
            var certificado = CargarCertificado(ruta, c[ClaveCertificadoClave]);
            proteccion.ProtectKeysWithCertificate(certificado).UnprotectKeysWithAnyCertificate(certificado);
            return null;
        }
        if (OperatingSystem.IsWindows())
        {
            proteccion.ProtectKeysWithDpapi();
            return null;
        }
        return "Las claves de DataProtection se guardan sin cifrar: configura " + ClaveCertificadoRuta +
               " o deja la carpeta " + ConfiguracionRecuperacion.ClaveRutaClaves + " solo para la cuenta del servicio (chmod 700).";
    }

    public static X509Certificate2 CargarCertificado(string ruta, string? clave)
    {
        if (!File.Exists(ruta))
            throw new InvalidOperationException($"{ClaveCertificadoRuta}: no existe el archivo del certificado.");
        X509Certificate2 certificado;
        try
        {
            // En Linux la clave privada queda solo en memoria; en Windows se usa el almacén del usuario del servicio.
            var opciones = OperatingSystem.IsWindows() ? X509KeyStorageFlags.UserKeySet : X509KeyStorageFlags.EphemeralKeySet;
            certificado = new X509Certificate2(ruta, clave, opciones);
        }
        catch (CryptographicException)
        {
            // Sin la excepción original ni la clave: solo se indica qué revisar.
            throw new InvalidOperationException(
                $"No se pudo abrir el certificado de {ClaveCertificadoRuta}; revisa el archivo y {ClaveCertificadoClave}.");
        }
        if (!certificado.HasPrivateKey)
            throw new InvalidOperationException($"El certificado de {ClaveCertificadoRuta} debe incluir la clave privada (.pfx).");
        return certificado;
    }

    // ------------------------------------------------------------------ encabezados

    /// <summary>Encabezados de seguridad de todas las respuestas de la API (JSON e imágenes; nunca HTML).</summary>
    public static readonly IReadOnlyDictionary<string, string> Encabezados = new Dictionary<string, string>
    {
        // D-78 (B-4 de P8): el enlace de recuperación nunca sale en un Referer.
        ["Referrer-Policy"] = "no-referrer",
        ["X-Content-Type-Options"] = "nosniff",
        ["X-Frame-Options"] = "DENY",
        ["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'"
    };

    /// <summary>Acepta una lista JSON (Proxy:KnownProxies:0, :1...) o un texto separado por comas o punto y coma.</summary>
    private static string[] Lista(IConfiguration c, string clave)
    {
        var hijos = c.GetSection(clave).GetChildren().Select(x => x.Value).Where(v => v is not null).Select(v => v!);
        return hijos.Concat([c[clave] ?? ""])
            .SelectMany(v => v.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct()
            .ToArray();
    }
}
