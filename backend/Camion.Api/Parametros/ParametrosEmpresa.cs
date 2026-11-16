using System.Text.Json;
using Camion.Api.Auth;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Parametros;

// P9 (HU-085, RN-PA-005, D-63 B, D-44; CA-P9-24 a CA-P9-37): razón social, NIT y dirección de notificaciones del portal,
// guardados en configuracion_sistema (clave "portal.empresa"). No hay valores por defecto: los entrega la responsable.
// Cada campo es opcional; el portal solo los publica cuando están los tres (la forma de mostrarlos depende de D-83).
public static class ParametrosEmpresa
{
    public const string Clave = "portal.empresa";

    public const string MensajeRazonSocial = "Escribe la razón social (entre 2 y 150 caracteres, sin los signos < >).";
    public const string MensajeNit = "Escribe el NIT con su dígito de verificación, por ejemplo 900.123.456-7.";
    public const string MensajeDireccion =
        "Escribe la dirección de notificaciones (entre 5 y 200 caracteres, sin los signos < >).";

    public const int MinRazonSocial = 2;
    public const int MaxRazonSocial = 150;
    public const int MinDireccion = 5;
    public const int MaxDireccion = 200;
    // Entrada bruta del NIT (con puntos y espacios); evita procesar textos enormes.
    private const int MaxNitBruto = 40;

    // Pesos de la DIAN para el dígito de verificación, aplicados de derecha a izquierda.
    private static readonly int[] PesosDian = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Valor guardado en valor_json.</summary>
    public sealed record Empresa(string? RazonSocial, string? Nit, string? Direccion);

    public sealed record Leido(Empresa Valor, bool Configurado, DateTime? ActualizadoEn, string? ActualizadoPor);

    public static readonly Empresa Vacia = new(null, null, null);

    public static bool Completa(Empresa e) => e.RazonSocial is not null && e.Nit is not null && e.Direccion is not null;

    public static async Task<Leido> LeerAsync(CamionDbContext db, CancellationToken ct)
    {
        var fila = await db.ConfiguracionesSistema.AsNoTracking().FirstOrDefaultAsync(c => c.Clave == Clave, ct);
        var guardado = fila is null ? null : Deserializar(fila.ValorJson);
        if (fila is null || guardado is null) return new Leido(Vacia, false, null, null);
        return new Leido(Revalidar(guardado), true, DateTime.SpecifyKind(fila.ActualizadoEn, DateTimeKind.Utc),
            fila.ActualizadoPor);
    }

    /// <summary>
    /// Defensa en profundidad (como B-1 de P7): un campo guardado que ya no cumple las reglas del PUT (p. ej. editado a mano
    /// en la base) no se devuelve: queda en null y, por tanto, la empresa deja de estar completa.
    /// </summary>
    public static Empresa Revalidar(Empresa g) => new(
        Texto(g.RazonSocial, MinRazonSocial, MaxRazonSocial, out var okRazon) is { } r && okRazon && r == g.RazonSocial ? r : null,
        g.Nit is { } n && NormalizarNit(n) == n ? n : null,
        Texto(g.Direccion, MinDireccion, MaxDireccion, out var okDir) is { } d && okDir && d == g.Direccion ? d : null);

    /// <summary>CA-P9-33: los tres datos para el pie del portal, o null si falta alguno.</summary>
    public static EmpresaPortalPublica? Publica(Empresa e) =>
        Completa(e) ? new EmpresaPortalPublica(e.RazonSocial!, e.Nit!, e.Direccion!) : null;

    public static string Serializar(Empresa e) => JsonSerializer.Serialize(e, Json);

    private static Empresa? Deserializar(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Empresa>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static Dictionary<string, object?> Datos(Empresa e) => new()
    {
        ["razonSocial"] = e.RazonSocial,
        ["nit"] = e.Nit,
        ["direccion"] = e.Direccion
    };

    public static bool Iguales(Empresa a, Empresa b) =>
        a.RazonSocial == b.RazonSocial && a.Nit == b.Nit && a.Direccion == b.Direccion;

    // ---------------------------------------------------------------- validación (CA-P9-27 a CA-P9-31)

    /// <summary>Valida los tres campos antes de escribir; vacío o solo espacios = null (CA-P9-27).</summary>
    public static Dictionary<string, string[]> Validar(EmpresaPortalRequest r, out Empresa? valor)
    {
        var errores = new Dictionary<string, string[]>();
        valor = null;

        var razon = Texto(r.RazonSocial, MinRazonSocial, MaxRazonSocial, out var razonValida);
        if (!razonValida) errores["razonSocial"] = [MensajeRazonSocial];

        string? nit = null;
        if (!string.IsNullOrWhiteSpace(r.Nit))
        {
            nit = NormalizarNit(r.Nit);
            if (nit is null) errores["nit"] = [MensajeNit];
        }

        var direccion = Texto(r.Direccion, MinDireccion, MaxDireccion, out var direccionValida);
        if (!direccionValida) errores["direccion"] = [MensajeDireccion];

        if (errores.Count == 0) valor = new Empresa(razon, nit, direccion);
        return errores;
    }

    /// <summary>Texto opcional recortado: vacío = null (válido); si no, longitud y sin &lt; &gt; ni caracteres de control.</summary>
    private static string? Texto(string? bruto, int min, int max, out bool valido)
    {
        var texto = (bruto ?? "").Trim();
        valido = true;
        if (texto.Length == 0) return null;
        valido = texto.Length >= min && texto.Length <= max && texto.IndexOfAny(['<', '>']) < 0 &&
                 !ValidacionAuth.TieneControl(texto);
        return valido ? texto : null;
    }

    /// <summary>
    /// CA-P9-29: NIT colombiano con dígito de verificación (módulo 11 de la DIAN). Base de 6 a 10 dígitos (con puntos o
    /// espacios como separadores, que se quitan), un guion obligatorio y 1 dígito de verificación. Base solo de ceros no vale.
    /// Devuelve el NIT normalizado ("800197268-4") o null si no es válido.
    /// </summary>
    public static string? NormalizarNit(string? bruto)
    {
        var texto = (bruto ?? "").Trim();
        if (texto.Length is 0 or > MaxNitBruto) return null;
        var partes = texto.Split('-');
        if (partes.Length != 2) return null;
        var baseNit = partes[0];
        var dvTexto = partes[1].Trim();
        if (baseNit.Any(c => !char.IsAsciiDigit(c) && c is not '.' and not ' ')) return null;
        var digitos = new string(baseNit.Where(char.IsAsciiDigit).ToArray());
        if (digitos.Length is < 6 or > 10 || digitos.All(c => c == '0')) return null;
        if (dvTexto.Length != 1 || !char.IsAsciiDigit(dvTexto[0])) return null;
        return DigitoVerificacion(digitos) == dvTexto[0] - '0' ? digitos + "-" + dvTexto : null;
    }

    /// <summary>Dígito de verificación de la DIAN para una base de hasta 15 dígitos.</summary>
    public static int DigitoVerificacion(string digitos)
    {
        var suma = 0;
        for (var i = 0; i < digitos.Length; i++)
            suma += (digitos[digitos.Length - 1 - i] - '0') * PesosDian[i];
        var r = suma % 11;
        return r is 0 or 1 ? r : 11 - r;
    }
}
