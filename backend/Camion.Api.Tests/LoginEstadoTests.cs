using System.Net;
using System.Net.Http.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.Ayudas;
using static Camion.Api.Tests.Infraestructura.AyudasP2;

namespace Camion.Api.Tests;

/// <summary>P2 — HU-045 Activar acceso (RN-US-003): login y /me según el estado de la cuenta; no regresión del comprador.</summary>
public sealed class LoginEstadoTests(CamionApiFactory factory) : IClassFixture<CamionApiFactory>
{
    private readonly HttpClient _c = factory.CreateClient();
    private const string MsgCredenciales = "Correo o contraseña incorrectos.";

    [Fact(DisplayName = "CA-045-01/02: asesor PENDIENTE: contraseña correcta -> 403 con estado y sin token; incorrecta -> 401 igual que un correo inexistente")]
    public async Task Pendiente()
    {
        var (id, email, _) = await CrearAsesorPendiente(_c);
        var r = await Login(_c, email, PasswordValida);
        await AfirmarError(r, HttpStatusCode.Forbidden, MsgPendiente);
        var json = await Json(r);
        Assert.Equal("PENDIENTE", json.GetProperty("estado").GetString());
        Assert.False(json.TryGetProperty("accessToken", out _));
        // ultimoAccesoEn solo cambia en un login correcto con la cuenta ACTIVA.
        Assert.Null((await Usuario(factory, id)).UltimoAccesoEn);

        var mala = await Login(_c, email, "Incorrecta2026");
        var inexistente = await Login(_c, CorreoUnico("nadie"), "Incorrecta2026");
        Assert.Equal(HttpStatusCode.Unauthorized, mala.StatusCode);
        Assert.Equal(await inexistente.Content.ReadAsStringAsync(), await mala.Content.ReadAsStringAsync());
        Assert.Equal(MsgCredenciales, (await Json(mala)).GetProperty("error").GetString());
    }

    [Fact(DisplayName = "CA-045-03/10, CA-043R-03, CA-N03-R01/R03: /me con estado ACTIVA y roles para comprador, asesor aprobado y administrador")]
    public async Task Me_con_estado()
    {
        var (_, _, admin) = await CrearAdministrador(factory, _c);
        var (asesorId, asesorEmail, _) = await CrearAsesorPendiente(_c);
        await Ok(await AccionMotivo(_c, admin, asesorId, "aprobar", null));
        var (compradorId, compradorEmail) = await CrearComprador(_c);
        Assert.Equal("ACTIVA", (await Usuario(factory, compradorId)).EstadoCuenta); // acceso inmediato (D-3)

        foreach (var (token, rol) in new[]
                 {
                     (await ObtenerToken(_c, compradorEmail), "COMPRADOR"),
                     (await ObtenerToken(_c, asesorEmail), "ASESOR"),
                     (admin, "ADMINISTRADOR")
                 })
        {
            var me = await Json(await Me(_c, token));
            Assert.Equal("ACTIVA", me.GetProperty("estado").GetString());
            Assert.Equal([rol], me.GetProperty("roles").EnumerateArray().Select(x => x.GetString()!).ToArray());
            foreach (var campo in new[] { "usuarioId", "nombreCompleto", "email", "phoneNumber" })
                Assert.True(me.TryGetProperty(campo, out _), campo);
        }
        Assert.NotNull((await Usuario(factory, asesorId)).UltimoAccesoEn);
    }

    [Fact(DisplayName = "CA-045-04/05/06: rechazado y bloqueado por la API: correcta -> 403 con estado; incorrecta -> 401 sin estado")]
    public async Task Rechazado_y_bloqueado()
    {
        var (_, _, admin) = await CrearAdministrador(factory, _c);
        var (rech, rechEmail, _) = await CrearAsesorPendiente(_c);
        await Ok(await AccionMotivo(_c, admin, rech, "rechazar", MotivoValido));
        var (comp, compEmail) = await CrearComprador(_c);
        await Ok(await AccionMotivo(_c, admin, comp, "bloquear", MotivoValido));
        var (ase, aseEmail, _) = await CrearAsesorPendiente(_c);
        await Ok(await AccionMotivo(_c, admin, ase, "aprobar", null));
        await Ok(await AccionMotivo(_c, admin, ase, "bloquear", MotivoValido));

        foreach (var (email, estado, mensaje) in new[]
                 {
                     (rechEmail, "RECHAZADA", MsgRechazada), (compEmail, "BLOQUEADA", MsgBloqueada), (aseEmail, "BLOQUEADA", MsgBloqueada)
                 })
        {
            var r = await Login(_c, email, PasswordValida);
            await AfirmarError(r, HttpStatusCode.Forbidden, mensaje);
            Assert.Equal(estado, (await Json(r)).GetProperty("estado").GetString());
            var mala = await Login(_c, email, "Incorrecta2026");
            Assert.Equal(HttpStatusCode.Unauthorized, mala.StatusCode);
            Assert.False((await Json(mala)).TryGetProperty("estado", out _));
        }
    }

    [Fact(DisplayName = "CA-045-07: asesor PENDIENTE con 5 fallos -> 423 aun con la contraseña correcta")]
    public async Task Pendiente_con_bloqueo_por_intentos()
    {
        var (_, email, _) = await CrearAsesorPendiente(_c);
        for (var i = 0; i < 5; i++) await Login(_c, email, "Incorrecta2026");
        var r = await Login(_c, email, PasswordValida);
        Assert.Equal(423, (int)r.StatusCode);
        Assert.False((await Json(r)).TryGetProperty("estado", out _));
    }

    [Fact(DisplayName = "CA-045-08 (aclaración del coordinador): el 403 por estado no suma fallo y reinicia el contador")]
    public async Task Forbidden_no_suma_intento()
    {
        var (id, email, _) = await CrearAsesorPendiente(_c);
        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login(_c, email, "Incorrecta2026")).StatusCode);
        Assert.Equal(4, (await Usuario(factory, id)).AccessFailedCount);

        Assert.Equal(HttpStatusCode.Forbidden, (await Login(_c, email, PasswordValida)).StatusCode);
        Assert.Equal(0, (await Usuario(factory, id)).AccessFailedCount);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(_c, email, "Incorrecta2026")).StatusCode);
        Assert.Equal(1, (await Usuario(factory, id)).AccessFailedCount);
        Assert.Equal(HttpStatusCode.Forbidden, (await Login(_c, email, PasswordValida)).StatusCode);
    }

    [Fact(DisplayName = "CA-045-09: las respuestas 400, 401, 409 y 423 no incluyen la clave 'estado'")]
    public async Task Estado_solo_en_el_403()
    {
        var (_, email, _) = await CrearAsesorPendiente(_c);
        var respuestas = new List<HttpResponseMessage>
        {
            await Login(_c, "", ""),                                                     // 400
            await _c.PostAsJsonAsync("/api/auth/registro/asesor", RegistroAsesor(email: "malo")), // 400
            await Login(_c, email, "Incorrecta2026"),                                    // 401
            await RegistrarAsesor(_c, RegistroAsesor(email)),                            // 409
        };
        var (_, bloqueado) = await CrearComprador(_c);
        for (var i = 0; i < 5; i++) await Login(_c, bloqueado, "Incorrecta2026");
        respuestas.Add(await Login(_c, bloqueado, PasswordValida));                      // 423

        Assert.Equal(new[] { 400, 400, 401, 409, 423 }, respuestas.Select(r => (int)r.StatusCode));
        foreach (var r in respuestas)
            Assert.False((await Json(r)).TryGetProperty("estado", out _), $"{(int)r.StatusCode} incluye 'estado'");
    }
}
