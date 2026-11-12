using System.Text.Json;
using Camion.Api.Auth;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Parametros;

// P7 (HU-081, RN-PA-001, D-42, D-47, D-63 A): datos de contacto del portal guardados en configuracion_sistema
// (clave "portal.contacto"). Mientras no se guarden, valen los confirmados en D-42 y el WhatsApp de appsettings.json.
// Solo datos de contacto: ningún valor de negocio es editable (D-63 C descartada).
public static class ParametrosPortal
{
    public const string ClaveContacto = "portal.contacto";

    public const string MensajeWhatsapp = "Escribe un celular colombiano de 10 dígitos, por ejemplo 316 615 2907.";
    public const string MensajeCorreo = "Escribe un correo válido.";
    public const string MensajeHorario =
        "Escribe de 1 a 4 líneas de horario, de hasta 100 caracteres cada una y sin los signos < >.";
    public const string MensajeInstagram = "Escribe el enlace de Instagram (https://www.instagram.com/...) o deja el campo vacío.";
    public const string MensajeTiktok = "Escribe el enlace de TikTok (https://www.tiktok.com/@...) o deja el campo vacío.";

    public const string CorreoPorDefecto = "vc.automotores@gmail.com";
    public static readonly string[] HorarioPorDefecto =
        ["Lunes a viernes: 9:00 a. m. a 5:00 p. m.", "Sábados: 9:00 a. m. a 3:00 p. m."];

    public const int MaxLineasHorario = 4;
    public const int MaxLineaHorario = 100;
    public const int MaxCorreo = 254;
    public const int MaxEnlace = 200;

    private static readonly string[] HostsInstagram = ["www.instagram.com", "instagram.com"];
    private static readonly string[] HostsTiktok = ["www.tiktok.com", "tiktok.com"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Valor guardado en valor_json. Whatsapp: 10 dígitos sin indicativo.</summary>
    public sealed record Contacto(string Whatsapp, string Correo, string[] Horario, string? Instagram, string? Tiktok);

    public sealed record Leido(Contacto Valor, bool Configurado, DateTime? ActualizadoEn, string? ActualizadoPor);

    /// <summary>Valores por defecto (D-42). whatsappPortal es el de appsettings (57 + 10 dígitos).</summary>
    public static Contacto PorDefecto(string whatsappPortal)
    {
        var digitos = new string((whatsappPortal ?? "").Where(char.IsAsciiDigit).ToArray());
        if (digitos.Length == 12 && digitos.StartsWith("57", StringComparison.Ordinal)) digitos = digitos[2..];
        return new Contacto(digitos, CorreoPorDefecto, HorarioPorDefecto, null, null);
    }

    public static async Task<Leido> LeerAsync(CamionDbContext db, string whatsappPortal, CancellationToken ct)
    {
        var fila = await db.ConfiguracionesSistema.AsNoTracking().FirstOrDefaultAsync(c => c.Clave == ClaveContacto, ct);
        var guardado = fila is null ? null : Deserializar(fila.ValorJson);
        var defecto = PorDefecto(whatsappPortal);
        if (fila is null || guardado is null) return new Leido(defecto, false, null, null);
        return new Leido(Revalidar(guardado, defecto), true, DateTime.SpecifyKind(fila.ActualizadoEn, DateTimeKind.Utc),
            fila.ActualizadoPor);
    }

    /// <summary>
    /// B-1 (defensa en profundidad): el valor guardado pasa otra vez por las reglas del PUT, campo a campo. Un campo que ya
    /// no cumple (p. ej. editado a mano en la base) no se devuelve: WhatsApp, correo y horario vuelven al respaldo
    /// (D-42 / appsettings) e Instagram y TikTok quedan en null.
    /// </summary>
    public static Contacto Revalidar(Contacto g, Contacto defecto)
    {
        var telefono = g.Whatsapp is { Length: 10 } w && w.All(char.IsAsciiDigit) ? "+57" + w : null;
        var whatsapp = ValidacionAuth.EsCelularColombia(telefono) ? g.Whatsapp : defecto.Whatsapp;
        var correo = g.Correo is { } c && c.Length <= MaxCorreo && c == ValidacionAuth.NormalizarEmail(c) &&
                     ValidacionAuth.EmailEstrictoValido(c)
            ? c
            : defecto.Correo;
        var horario = HorarioValido(g.Horario) ? g.Horario : defecto.Horario;
        var instagram = Enlace(g.Instagram, HostsInstagram, out var okInstagram) is { } i && okInstagram && i == g.Instagram ? i : null;
        var tiktok = Enlace(g.Tiktok, HostsTiktok, out var okTiktok) is { } t && okTiktok && t == g.Tiktok ? t : null;
        return new Contacto(whatsapp, correo, horario, instagram, tiktok);
    }

    private static bool HorarioValido(string[]? horario) =>
        horario is { Length: >= 1 and <= MaxLineasHorario } &&
        horario.All(l => l is not null && l.Trim() == l && l.Length is > 0 and <= MaxLineaHorario &&
                         l.IndexOfAny(['<', '>']) < 0 && !ValidacionAuth.TieneControl(l));

    /// <summary>D-47 (CA-P7-40): WhatsApp del portal con indicativo (57 + 10 dígitos) para el enlace wa.me de la ficha.</summary>
    public static async Task<string> WhatsappConIndicativoAsync(CamionDbContext db, string whatsappPortal, CancellationToken ct) =>
        "57" + (await LeerAsync(db, whatsappPortal, ct)).Valor.Whatsapp;

    public static string Enlace(Contacto c) => "https://wa.me/57" + c.Whatsapp;

    public static ContactoPortalResponse Publico(Contacto c) =>
        new(c.Whatsapp, Enlace(c), c.Correo, c.Horario, c.Instagram, c.Tiktok);

    public static string Serializar(Contacto c) => JsonSerializer.Serialize(c, Json);

    // Un valor guardado mal formado (editado a mano) no rompe el portal: se usan los valores por defecto.
    private static Contacto? Deserializar(string json)
    {
        try
        {
            var c = JsonSerializer.Deserialize<Contacto>(json, Json);
            return c is null || string.IsNullOrEmpty(c.Whatsapp) || string.IsNullOrEmpty(c.Correo) || c.Horario is null
                ? null
                : c;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static Dictionary<string, object?> Datos(Contacto c) => new()
    {
        ["whatsapp"] = c.Whatsapp,
        ["correo"] = c.Correo,
        ["horario"] = c.Horario,
        ["instagram"] = c.Instagram,
        ["tiktok"] = c.Tiktok
    };

    public static bool Iguales(Contacto a, Contacto b) =>
        a.Whatsapp == b.Whatsapp && a.Correo == b.Correo && a.Horario.SequenceEqual(b.Horario) &&
        a.Instagram == b.Instagram && a.Tiktok == b.Tiktok;

    // ---------------------------------------------------------------- validación (CA-P7-37, CA-P7-41)

    public static Dictionary<string, string[]> Validar(ContactoPortalRequest? r, out Contacto? valor)
    {
        var errores = new Dictionary<string, string[]>();
        valor = null;

        var telefono = ValidacionAuth.NormalizarTelefonoColombia(r?.Whatsapp);
        if (!ValidacionAuth.EsCelularColombia(telefono)) errores["whatsapp"] = [MensajeWhatsapp];

        var correo = ValidacionAuth.NormalizarEmail(r?.Correo);
        // B-2: regla estricta (el correo se publica en un enlace mailto:).
        if (correo.Length is 0 or > MaxCorreo || !ValidacionAuth.EmailEstrictoValido(correo)) errores["correo"] = [MensajeCorreo];

        var horario = (r?.Horario ?? []).Select(l => (l ?? "").Trim()).ToArray();
        if (horario.Length is < 1 or > MaxLineasHorario ||
            horario.Any(l => l.Length is 0 or > MaxLineaHorario || l.IndexOfAny(['<', '>']) >= 0 || ValidacionAuth.TieneControl(l)))
            errores["horario"] = [MensajeHorario];

        var instagram = Enlace(r?.Instagram, HostsInstagram, out var instagramValido);
        if (!instagramValido) errores["instagram"] = [MensajeInstagram];
        var tiktok = Enlace(r?.Tiktok, HostsTiktok, out var tiktokValido);
        if (!tiktokValido) errores["tiktok"] = [MensajeTiktok];

        if (errores.Count == 0)
            valor = new Contacto(telefono![3..], correo, horario, instagram, tiktok);
        return errores;
    }

    /// <summary>
    /// Enlace opcional de red social: vacío = null. Si no, https, host oficial, sin usuario ni puerto, ruta no vacía y
    /// sin espacios ni comillas ni &lt; &gt; (nunca javascript: ni HTML, CA-P7-41).
    /// </summary>
    private static string? Enlace(string? bruto, string[] hosts, out bool valido)
    {
        var texto = (bruto ?? "").Trim();
        valido = true;
        if (texto.Length == 0) return null;
        valido = texto.Length <= MaxEnlace &&
                 !texto.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '<' or '>' or '"' or '\'' or '\\') &&
                 texto.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                 Uri.TryCreate(texto, UriKind.Absolute, out var uri) &&
                 uri.Scheme == Uri.UriSchemeHttps &&
                 hosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase) &&
                 uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
                 uri.AbsolutePath.Trim('/').Length > 0;
        return valido ? texto : null;
    }
}
