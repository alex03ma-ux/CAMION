using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Camion.Api.Endpoints;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using static Camion.Api.Tests.Infraestructura.Ayudas;
using static Camion.Api.Tests.Infraestructura.AyudasP2;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;

namespace Camion.Api.Tests;

/// <summary>
/// P8 — recuperación asistida de contraseña (HU-063, RN-US-006, D-73 D, D-12; CA-P8-67). El administrador genera un enlace
/// de un solo uso que vence en 60 minutos; al usarlo se cierran todas las sesiones. Sin correo ni servicios externos.
/// SQLite en memoria; las claves de DataProtection son efímeras.
/// </summary>
public sealed class P8RecuperacionTests(P7Fixture fixture) : IClassFixture<P7Fixture>
{
    private readonly InventarioApi _api = fixture.Api;
    private const string NuevaClave = "NuevaClave2027";

    private static Task<HttpResponseMessage> Generar(InventarioApi api, string usuarioId, string? token) =>
        api.Enviar(HttpMethod.Post, $"/api/admin/usuarios/{Uri.EscapeDataString(usuarioId)}/enlace-recuperacion", token);

    private static Task<HttpResponseMessage> Restablecer(InventarioApi api, string? codigo, string? password = NuevaClave) =>
        api.C.PostAsJsonAsync("/api/auth/restablecer-contrasena", new { codigo, password });

    private static async Task<string> Codigo(InventarioApi api, string usuarioId)
    {
        var json = await Esperar(await Generar(api, usuarioId, api.Admin.Token), HttpStatusCode.OK);
        var enlace = json.GetProperty("enlace").GetString()!;
        Assert.StartsWith(CamionApiFactory.UrlPortalPruebas + RecuperacionEndpoints.RutaFrontend, enlace);
        return enlace[(enlace.IndexOf("codigo=", StringComparison.Ordinal) + "codigo=".Length)..];
    }

    [Fact(DisplayName = "CA-P8-67: enlace de 60 minutos; al usarlo cambia la contraseña, cierra las sesiones y deja de valer")]
    public async Task Generar_y_usar()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor("Asesora Olvidadiza Ficticia");
        var r = await Generar(_api, asesor.UsuarioId, _api.Admin.Token);
        var json = await Esperar(r, HttpStatusCode.OK);
        Assert.True(r.Headers.CacheControl?.NoStore, "Falta Cache-Control: no-store");
        Assert.Equal(T.AddMinutes(60), json.GetProperty("venceEn").GetDateTime().ToUniversalTime());
        Assert.Equal(RecuperacionEndpoints.MensajeEnlace, json.GetProperty("mensaje").GetString());
        var enlace = json.GetProperty("enlace").GetString()!;
        var codigo = enlace[(enlace.IndexOf("codigo=", StringComparison.Ordinal) + 7)..];
        Assert.Matches("^[A-Za-z0-9_-]+$", codigo); // seguro en una URL, sin + / =

        // Generar no cambia la contraseña ni cierra sesiones.
        Assert.Equal(HttpStatusCode.OK, (await Me(_api.C, asesor.Token)).StatusCode);
        var version = (await Usuario(_api.F, asesor.UsuarioId)).SessionVersion;

        var ok = await Esperar(await Restablecer(_api, codigo), HttpStatusCode.OK);
        Assert.Equal(RecuperacionEndpoints.MensajeListo, ok.GetProperty("mensaje").GetString());
        Assert.Equal(version + 1, (await Usuario(_api.F, asesor.UsuarioId)).SessionVersion);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_api.C, asesor.Token)).StatusCode);       // D-12
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(_api.C, asesor.Email, PasswordValida)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Me(_api.C, await ObtenerToken(_api.C, asesor.Email, NuevaClave))).StatusCode);

        // Un solo uso.
        await Error400(await Restablecer(_api, codigo, "OtraClave2028"), "codigo", RecuperacionEndpoints.MensajeCodigo);
        Assert.Equal(HttpStatusCode.OK, (await Login(_api.C, asesor.Email, NuevaClave)).StatusCode);

        // Auditoría de la generación y del uso, sin el código ni la contraseña; tampoco en los logs.
        var auditoria = await AuditoriaDe(_api.F, asesor.UsuarioId);
        var generar = Assert.Single(auditoria, a => a.Accion == "GENERAR_ENLACE_RECUPERACION");
        Assert.Equal(_api.Admin.Id, generar.UsuarioId);
        var usar = Assert.Single(auditoria, a => a.Accion == "RESTABLECER_CONTRASENA");
        Assert.Equal(asesor.UsuarioId, usar.UsuarioId);
        foreach (var a in auditoria)
        {
            Assert.DoesNotContain(codigo, (a.DatosAnteriores ?? "") + a.DatosNuevos);
            Assert.DoesNotContain(NuevaClave, (a.DatosAnteriores ?? "") + a.DatosNuevos);
        }
        Assert.DoesNotContain(_api.F.Logs, l => l.Contains(codigo, StringComparison.Ordinal) || l.Contains(NuevaClave, StringComparison.Ordinal));
        Assert.Contains(_api.F.Logs, l => l.Contains("Auditoria restablecer_contrasena") && l.Contains(asesor.UsuarioId));
    }

    [Fact(DisplayName = "CA-P8-67: generar otro enlace invalida el anterior; un comprador también puede recuperar")]
    public async Task Solo_vale_el_ultimo()
    {
        _api.Hora(T);
        var (id, email) = await CrearComprador(_api.C, "Compradora Olvidadiza");
        var primero = await Codigo(_api, id);
        var segundo = await Codigo(_api, id);
        Assert.NotEqual(primero, segundo);
        await Error400(await Restablecer(_api, primero), "codigo", RecuperacionEndpoints.MensajeCodigo);
        await Esperar(await Restablecer(_api, segundo), HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, (await Login(_api.C, email, NuevaClave)).StatusCode);
    }

    [Fact(DisplayName = "CA-P8-67: el enlace vence a los 60 minutos (a los 59 todavía sirve)")]
    public async Task Vencimiento()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var (id1, _) = await CrearComprador(api.C, "Comprador Vence Uno");
        var (id2, email2) = await CrearComprador(api.C, "Comprador Vence Dos");
        api.Hora(T);
        var vencido = await Codigo(api, id1);
        var vigente = await Codigo(api, id2);
        api.Hora(T.AddMinutes(59));
        await Esperar(await Restablecer(api, vigente), HttpStatusCode.OK);
        api.Hora(T.AddMinutes(60));
        await Error400(await Restablecer(api, vencido), "codigo", RecuperacionEndpoints.MensajeCodigo);
        Assert.Equal(HttpStatusCode.OK, (await Login(api.C, email2, NuevaClave)).StatusCode);
    }

    [Fact(DisplayName = "CA-P8-67: contraseña con las reglas de P1; código vacío, alterado o de otra clave -> 400 sin revelar la causa")]
    public async Task Validaciones()
    {
        _api.Hora(T);
        var (id, email) = await CrearComprador(_api.C, "Comprador Valida");
        var codigo = await Codigo(_api, id);
        var debil = await Esperar(await Restablecer(_api, codigo, "corta"), HttpStatusCode.BadRequest);
        Assert.Equal(RecuperacionEndpoints.MensajeRevisar, debil.GetProperty("error").GetString());
        Assert.Contains(ValidacionMensajes.Minimo, debil.GetProperty("errores").GetProperty("password").EnumerateArray().Select(x => x.GetString()));
        foreach (var malo in new[] { null, "", "no-es-un-codigo", codigo[..^4] + "AAAA", new string('A', 3000) })
            await Error400(await Restablecer(_api, malo), "codigo", RecuperacionEndpoints.MensajeCodigo);
        await Error400(await _api.C.PostAsJsonAsync("/api/auth/restablecer-contrasena", new { }), "codigo", RecuperacionEndpoints.MensajeCodigo);
        // El código bueno sigue valiendo después de los intentos fallidos.
        await Esperar(await Restablecer(_api, codigo), HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, (await Login(_api.C, email, NuevaClave)).StatusCode);
    }

    [Fact(DisplayName = "CA-P8-67, HU-062: restablecer quita el bloqueo temporal por intentos fallidos")]
    public async Task Quita_bloqueo_temporal()
    {
        _api.Hora(T);
        var (id, email) = await CrearComprador(_api.C, "Comprador Bloqueado Temporal");
        for (var i = 0; i < 5; i++) await Login(_api.C, email, "Incorrecta2026");
        Assert.Equal((HttpStatusCode)423, (await Login(_api.C, email, PasswordValida)).StatusCode);
        await Esperar(await Restablecer(_api, await Codigo(_api, id)), HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, (await Login(_api.C, email, NuevaClave)).StatusCode);
    }

    [Fact(DisplayName = "D-73 D: generar -> 401 sin sesión; 403 asesor, comprador, propia cuenta y otro administrador; 404; 409 no activa")]
    public async Task Permisos_y_casos_de_generacion()
    {
        _api.Hora(T);
        var (id, _) = await CrearComprador(_api.C, "Comprador Permisos");
        Assert.Equal(HttpStatusCode.Unauthorized, (await Generar(_api, id, null)).StatusCode);
        await Error(await Generar(_api, id, _api.A.Token), HttpStatusCode.Forbidden, AyudasP2.MsgSinPermiso);
        await Error(await Generar(_api, id, _api.TokenComprador), HttpStatusCode.Forbidden, AyudasP2.MsgSinPermiso);
        await Error(await Generar(_api, _api.Admin.Id, _api.Admin.Token), HttpStatusCode.Forbidden, RecuperacionEndpoints.MensajePropia);
        var otroAdmin = await CrearAdministrador(_api.F, _api.C);
        await Error(await Generar(_api, otroAdmin.Id, _api.Admin.Token), HttpStatusCode.Forbidden,
            RecuperacionEndpoints.MensajeCuentaAdministrador);
        await Error(await Generar(_api, "no-existe", _api.Admin.Token), HttpStatusCode.NotFound, RecuperacionEndpoints.MensajeNoEncontrado);
        await Error(await Generar(_api, _api.Pendiente.Id, _api.Admin.Token), HttpStatusCode.Conflict, RecuperacionEndpoints.MensajeNoActiva);

        // Un código emitido antes de bloquear la cuenta ya no sirve.
        var asesor = await _api.CrearAsesor("Asesor Luego Bloqueado Ficticio");
        var codigo = await Codigo(_api, asesor.UsuarioId);
        await Ok(await AccionMotivo(_api.C, _api.Admin.Token, asesor.UsuarioId, "bloquear", AyudasP2.MotivoValido));
        await Error(await Generar(_api, asesor.UsuarioId, _api.Admin.Token), HttpStatusCode.Conflict, RecuperacionEndpoints.MensajeNoActiva);
        await Error400(await Restablecer(_api, codigo), "codigo", RecuperacionEndpoints.MensajeCodigo);
        Assert.DoesNotContain(await AuditoriaDe(_api.F, otroAdmin.Id), a => a.Accion == "GENERAR_ENLACE_RECUPERACION");
    }

    [Fact(DisplayName = "D-73 D: la ruta pública tiene su propio límite por IP (429 con mensaje en español)")]
    public async Task Limite_de_peticiones()
    {
        using var api = await InventarioApi.Crear(NuevoReloj(), b => b.UseSetting("Recuperacion:LimiteVentana", "3"));
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await Restablecer(api, "no-es-un-codigo")).StatusCode);
        var r = await Restablecer(api, "no-es-un-codigo");
        Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode);
        Assert.Equal(RecuperacionEndpoints.MensajeMuchos, (await Json(r)).GetProperty("error").GetString());
        // El cupo es aparte: el inicio de sesión no se limita.
        Assert.Equal(HttpStatusCode.OK, (await Login(api.C, api.Admin.Email, PasswordValida)).StatusCode);
    }

    private static class ValidacionMensajes
    {
        public const string Minimo = "Debe tener al menos 8 caracteres.";
    }
}
