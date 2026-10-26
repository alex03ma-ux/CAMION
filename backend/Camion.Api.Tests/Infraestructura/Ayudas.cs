using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Camion.Api.Tests.Infraestructura;

/// <summary>Datos ficticios y llamadas comunes a la API de autenticación.</summary>
public static class Ayudas
{
    // Contraseña ficticia de prueba (cumple las reglas del contrato). No es una credencial real.
    public const string PasswordValida = "Camion2026x";
    public const string TelefonoValido = "300 123 4567";

    public static string CorreoUnico(string prefijo = "comprador") =>
        $"{prefijo}.{Guid.NewGuid():N}@ejemplo.test";

    public static object Registro(string? email = null, string? nombre = "Ana Prueba",
        string? telefono = TelefonoValido, string? password = PasswordValida) =>
        new { nombreCompleto = nombre, email = email ?? CorreoUnico(), telefono, password };

    public static Task<HttpResponseMessage> Registrar(HttpClient c, object cuerpo) =>
        c.PostAsJsonAsync("/api/auth/registro/comprador", cuerpo);

    public static Task<HttpResponseMessage> Login(HttpClient c, string? email, string? password) =>
        c.PostAsJsonAsync("/api/auth/login", new { email, password });

    public static async Task<string> RegistrarYObtenerToken(HttpClient c, string? email = null)
    {
        email ??= CorreoUnico();
        var r = await Registrar(c, Registro(email));
        Assert.Equal(System.Net.HttpStatusCode.Created, r.StatusCode);
        return await ObtenerToken(c, email);
    }

    public static async Task<string> ObtenerToken(HttpClient c, string email, string password = PasswordValida)
    {
        var r = await Login(c, email, password);
        Assert.Equal(System.Net.HttpStatusCode.OK, r.StatusCode);
        var json = await Json(r);
        return json.GetProperty("accessToken").GetString()!;
    }

    public static HttpRequestMessage ConToken(HttpMethod metodo, string url, string? token)
    {
        var req = new HttpRequestMessage(metodo, url);
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    public static Task<HttpResponseMessage> Me(HttpClient c, string? token) =>
        c.SendAsync(ConToken(HttpMethod.Get, "/api/auth/me", token));

    public static Task<HttpResponseMessage> Logout(HttpClient c, string? token) =>
        c.SendAsync(ConToken(HttpMethod.Post, "/api/auth/logout", token));

    public static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        var texto = await r.Content.ReadAsStringAsync();
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    public static async Task<string[]> ErroresDe(HttpResponseMessage r, string campo)
    {
        var json = await Json(r);
        Assert.True(json.TryGetProperty("error", out _), "Falta 'error' en la respuesta 400.");
        Assert.True(json.TryGetProperty("errores", out var errores), "Falta 'errores' en la respuesta 400.");
        Assert.True(errores.TryGetProperty(campo, out var lista), $"Falta errores.{campo}.");
        return lista.EnumerateArray().Select(x => x.GetString()!).ToArray();
    }

    /// <summary>CA-062-01: ninguna respuesta incluye datos internos de Identity ni la contraseña enviada.</summary>
    public static async Task AfirmarSinDatosSensibles(HttpResponseMessage r, params string[] valoresProhibidos)
    {
        var texto = await r.Content.ReadAsStringAsync();
        foreach (var campo in new[] { "passwordHash", "securityStamp", "concurrencyStamp", "PasswordHash", "SecurityStamp" })
            Assert.DoesNotContain(campo, texto, StringComparison.OrdinalIgnoreCase);
        foreach (var valor in valoresProhibidos)
            Assert.DoesNotContain(valor, texto, StringComparison.Ordinal);
    }
}
