using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Camion.Api.Data;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Camion.Api.Tests.Infraestructura.Ayudas;
using static Camion.Api.Tests.Infraestructura.AyudasP2;

namespace Camion.Api.Tests;

/// <summary>
/// P2 — POST /api/admin/usuarios/{id}/aprobar|rechazar|bloquear|desbloquear|reconsiderar (HU-043, HU-044, HU-045,
/// HU-047, D-15), auditoría (HU-061), control de accesos (RN-SE-006) y cierre de sesión (D-12).
/// </summary>
public sealed class AdminUsuariosTests(CamionApiFactory factory) : IClassFixture<CamionApiFactory>
{
    private readonly HttpClient _c = factory.CreateClient();

    // ---------- Preparación de sujetos ----------

    private async Task<(string Id, string Email)> Sujeto(string adminToken, string tipo, string estado)
    {
        if (tipo == "asesor")
        {
            var (id, email, _) = await CrearAsesorPendiente(_c);
            switch (estado)
            {
                case "PENDIENTE": break;
                case "ACTIVA":
                    await Ok(await AccionMotivo(_c, adminToken, id, "aprobar", null)); break;
                case "RECHAZADA":
                    await Ok(await AccionMotivo(_c, adminToken, id, "rechazar", MotivoValido)); break;
                case "BLOQUEADA":
                    await Ok(await AccionMotivo(_c, adminToken, id, "aprobar", null));
                    await Ok(await AccionMotivo(_c, adminToken, id, "bloquear", MotivoValido)); break;
                default: throw new ArgumentOutOfRangeException(nameof(estado));
            }
            return (id, email);
        }
        var (cid, cemail) = await CrearComprador(_c);
        switch (estado)
        {
            case "ACTIVA": break;
            case "BLOQUEADA":
                await Ok(await AccionMotivo(_c, adminToken, cid, "bloquear", MotivoValido)); break;
            default:
                // Estado imposible por la API para un comprador: se fuerza en la base para probar "solo asesores".
                await CambiarEstado(factory, cid, estado); break;
        }
        return (cid, cemail);
    }

    private sealed record Foto(string Estado, int SessionVersion, string? EstadoAsesor, string? AutorizadoPor,
        DateTime? AutorizadoEn, int Auditorias, int Historial);

    private async Task<Foto> Fotografiar(string id)
    {
        var u = await Usuario(factory, id);
        var p = await Perfil(factory, id);
        return new Foto(u.EstadoCuenta, u.SessionVersion, p?.EstadoAutorizacion, p?.AutorizadoPor, p?.AutorizadoEn,
            (await AuditoriaDe(factory, id)).Count, (await HistorialDe(factory, id)).Count);
    }

    // Transiciones del contrato P2 (+ D-15). Clave: acción -> (desde, hacia, estado D-10, solo asesores, cierra sesión).
    private static readonly Dictionary<string, (string Desde, string Hacia, string Asesor, bool SoloAsesores, bool Cierra)> Tabla = new()
    {
        ["aprobar"] = ("PENDIENTE", "ACTIVA", "APROBADO", true, false),
        ["rechazar"] = ("PENDIENTE", "RECHAZADA", "RECHAZADO", true, true),
        ["bloquear"] = ("ACTIVA", "BLOQUEADA", "BLOQUEADO", false, true),
        ["desbloquear"] = ("BLOQUEADA", "ACTIVA", "APROBADO", false, false),
        ["reconsiderar"] = ("RECHAZADA", "ACTIVA", "APROBADO", true, false),
    };

    private static readonly Dictionary<string, string> NombreD10 = new()
    {
        ["PENDIENTE"] = "PENDIENTE", ["ACTIVA"] = "APROBADO", ["RECHAZADA"] = "RECHAZADO", ["BLOQUEADA"] = "BLOQUEADO"
    };

    public static IEnumerable<object[]> Matriz()
    {
        foreach (var accion in Acciones)
        foreach (var (tipo, estado) in new[]
                 {
                     ("asesor", "PENDIENTE"), ("asesor", "ACTIVA"), ("asesor", "RECHAZADA"), ("asesor", "BLOQUEADA"),
                     ("comprador", "ACTIVA"), ("comprador", "BLOQUEADA"), ("comprador", "PENDIENTE"), ("comprador", "RECHAZADA")
                 })
            yield return [accion, tipo, estado];
    }

    [Theory(DisplayName = "CA-043-01/03/04/10, CA-044-01/03/04/07, CA-047-01/02/05/07/09, D-15, CA-061-08..12, CA-D12-05: matriz de transiciones")]
    [MemberData(nameof(Matriz))]
    public async Task Transicion(string accion, string tipo, string estado)
    {
        var (adminId, _, admin) = await CrearAdministrador(factory, _c);
        var (id, _) = await Sujeto(admin, tipo, estado);
        var antes = await Fotografiar(id);
        var svAdmin = (await Usuario(factory, adminId)).SessionVersion;
        var t = Tabla[accion];
        var valida = estado == t.Desde && (!t.SoloAsesores || tipo == "asesor");
        var inicio = DateTime.UtcNow.AddSeconds(-1);

        var r = await AccionMotivo(_c, admin, id, accion, MotivoValido);
        var despues = await Fotografiar(id);

        if (!valida)
        {
            await AfirmarError(r, HttpStatusCode.Conflict, $"No se puede {accion} una cuenta en estado {estado.ToLowerInvariant()}.");
            Assert.Equal(antes, despues); // CA-061-12: nada cambia, sin auditoría ni historial
            return;
        }

        var json = await Ok(r);
        await AfirmarSinCamposInternos(r);
        Assert.Equal(id, json.GetProperty("usuarioId").GetString());
        Assert.Equal(t.Hacia, json.GetProperty("estado").GetString());
        Assert.Equal(t.Hacia, despues.Estado);
        Assert.Equal(antes.SessionVersion + (t.Cierra ? 1 : 0), despues.SessionVersion);
        Assert.Equal(svAdmin, (await Usuario(factory, adminId)).SessionVersion); // CA-D12-05
        Assert.Equal(HttpStatusCode.OK, (await Me(_c, admin)).StatusCode);

        if (tipo == "asesor")
        {
            var asesor = json.GetProperty("asesor");
            Assert.Equal(t.Asesor, asesor.GetProperty("estadoAutorizacion").GetString());
            Assert.Equal(t.Asesor, despues.EstadoAsesor); // CA-043-10: ambos estados a la vez
            if (accion is "aprobar" or "reconsiderar")
            {
                Assert.Equal(adminId, despues.AutorizadoPor);
                Assert.NotNull(despues.AutorizadoEn);
                Assert.InRange(despues.AutorizadoEn!.Value, inicio, DateTime.UtcNow.AddSeconds(1));
                Assert.Equal(JsonValueKind.String, asesor.GetProperty("autorizadoEn").ValueKind);
            }
            // CA-061-11: una fila de historial con los nombres de D-10.
            var h = (await HistorialDe(factory, id)).Last();
            Assert.Equal(antes.Historial + 1, despues.Historial);
            Assert.Equal(NombreD10[estado], h.EstadoAnterior);
            Assert.Equal(t.Asesor, h.EstadoNuevo);
            Assert.Equal(adminId, h.AdministradorId);
            Assert.Equal(MotivoValido, h.Motivo);
            Assert.Equal((await Perfil(factory, id))!.Id, h.AsesorId);
        }
        else
        {
            Assert.Equal(JsonValueKind.Null, json.GetProperty("asesor").ValueKind);
            Assert.Equal(0, despues.Historial); // CA-061-10: comprador sin historial
        }

        // CA-061-08/09: fila de auditoría con quién, cuándo, a quién y acción.
        Assert.Equal(antes.Auditorias + 1, despues.Auditorias);
        var a = (await AuditoriaDe(factory, id)).Last();
        Assert.Equal(adminId, a.UsuarioId);
        Assert.Equal("Usuario", a.Entidad);
        Assert.Equal(id, a.EntidadId);
        Assert.Equal(accion.ToUpperInvariant(), a.Accion);
        Assert.Equal(estado, JsonPlano(a.DatosAnteriores)["estado"]);
        var nuevos = JsonPlano(a.DatosNuevos);
        Assert.Equal(t.Hacia, nuevos["estado"]);
        Assert.Equal(MotivoValido, nuevos["motivo"]);
        Assert.InRange(a.CreadoEn, inicio, DateTime.UtcNow.AddSeconds(1));
    }

    // ---------- Motivo ----------

    public static IEnumerable<object?[]> MotivosInvalidos()
    {
        foreach (var accion in new[] { "rechazar", "bloquear", "reconsiderar" })
        foreach (var motivo in new[] { "(sin cuerpo)", "(sin campo)", null, "", "     ", "abcd", "  abcd  ", new string('m', 501) })
            yield return [accion, motivo];
    }

    private async Task<string> SujetoPara(string admin, string accion) => accion switch
    {
        "rechazar" or "aprobar" => (await Sujeto(admin, "asesor", "PENDIENTE")).Id,
        "bloquear" => (await Sujeto(admin, "comprador", "ACTIVA")).Id,
        "desbloquear" => (await Sujeto(admin, "comprador", "BLOQUEADA")).Id,
        _ => (await Sujeto(admin, "asesor", "RECHAZADA")).Id
    };

    [Theory(DisplayName = "CA-044-02, CA-047-04, D-15: motivo obligatorio (5 a 500 tras recortar) en rechazar, bloquear y reconsiderar -> 400; nada cambia")]
    [MemberData(nameof(MotivosInvalidos))]
    public async Task Motivo_obligatorio(string accion, string? motivo)
    {
        var (_, _, admin) = await CrearAdministrador(factory, _c);
        var id = await SujetoPara(admin, accion);
        var antes = await Fotografiar(id);

        HttpResponseMessage r;
        if (motivo == "(sin cuerpo)")
            r = await _c.SendAsync(ConToken(HttpMethod.Post, $"/api/admin/usuarios/{id}/{accion}", admin));
        else if (motivo == "(sin campo)")
            r = await Accion(_c, admin, id, accion, new { });
        else
            r = await AccionMotivo(_c, admin, id, accion, motivo);

        await AfirmarError(r, HttpStatusCode.BadRequest, "Revisa los datos marcados.");
        Assert.Equal([MsgMotivo], await ErroresDe(r, "motivo"));
        Assert.Equal(antes, await Fotografiar(id));
    }

    [Theory(DisplayName = "CA-044-02, CA-047-04, D-15: motivo de 5 y de 500 caracteres -> 200 (se guarda recortado)")]
    [InlineData("rechazar")]
    [InlineData("bloquear")]
    [InlineData("reconsiderar")]
    public async Task Motivo_en_los_limites(string accion)
    {
        var (_, _, admin) = await CrearAdministrador(factory, _c);
        foreach (var motivo in new[] { "  abcde ", new string('m', 500) })
        {
            var id = await SujetoPara(admin, accion);
            await Ok(await AccionMotivo(_c, admin, id, accion, motivo));
            Assert.Equal(motivo.Trim(), JsonPlano((await AuditoriaDe(factory, id)).Last().DatosNuevos)["motivo"]);
        }
    }

    [Theory(DisplayName = "CA-043-01/02, CA-047-07: en aprobar y desbloquear el motivo es opcional (sin cuerpo, {}, 500) y 501 -> 400")]
    [InlineData("aprobar")]
    [InlineData("desbloquear")]
    public async Task Motivo_opcional(string accion)
    {
        var (_, _, admin) = await CrearAdministrador(factory, _c);

        var id1 = await SujetoPara(admin, accion);
        await Ok(await _c.SendAsync(ConToken(HttpMethod.Post, $"/api/admin/usuarios/{id1}/{accion}", admin)));
        Assert.Null(JsonPlano((await AuditoriaDe(factory, id1)).Last().DatosNuevos)["motivo"]);

        var id2 = await SujetoPara(admin, accion);
        await Ok(await Accion(_c, admin, id2, accion, new { }));

        var id3 = await SujetoPara(admin, accion);
        var largo = new string('m', 500);
        await Ok(await AccionMotivo(_c, admin, id3, accion, largo));
        Assert.Equal(largo, JsonPlano((await AuditoriaDe(factory, id3)).Last().DatosNuevos)["motivo"]);
        var h = await HistorialDe(factory, id3);
        if (accion == "aprobar") Assert.Equal(largo, h.Last().Motivo);

        var id4 = await SujetoPara(admin, accion);
        var antes = await Fotografiar(id4);
        var r = await AccionMotivo(_c, admin, id4, accion, new string('m', 501));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.NotEmpty(await ErroresDe(r, "motivo"));
        Assert.Equal(antes, await Fotografiar(id4));
    }

    // ---------- 404 y 403 ----------

    [Fact(DisplayName = "CA-043-06, CA-044-05, CA-047-10: id inexistente (o sin formato) -> 404 en las 5 acciones")]
    public async Task Inexistente_da_404()
    {
        var (_, _, admin) = await CrearAdministrador(factory, _c);
        foreach (var accion in Acciones)
        foreach (var id in new[] { Guid.NewGuid().ToString(), "no-existe", "id con espacios" })
            await AfirmarError(await AccionMotivo(_c, admin, id, accion, MotivoValido), HttpStatusCode.NotFound, MsgNoEncontrado);
    }

    [Fact(DisplayName = "CA-043-07, CA-044-05, CA-047-06: sobre otro administrador o sobre la propia cuenta -> 403; nada cambia ni se audita")]
    public async Task Sobre_administrador_da_403()
    {
        var (adminId, _, admin) = await CrearAdministrador(factory, _c);
        var (otroId, _, otroToken) = await CrearAdministrador(factory, _c);
        foreach (var accion in Acciones)
        foreach (var objetivo in new[] { otroId, adminId })
        {
            var antes = await Fotografiar(objetivo);
            await AfirmarError(await AccionMotivo(_c, admin, objetivo, accion, MotivoValido), HttpStatusCode.Forbidden, MsgCuentaAdmin);
            Assert.Equal(antes, await Fotografiar(objetivo));
        }
        Assert.Empty(await AuditoriaDe(factory, otroId));
        Assert.Empty(await AuditoriaDe(factory, adminId));
        Assert.Equal(HttpStatusCode.OK, (await Me(_c, otroToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Me(_c, admin)).StatusCode);
    }

    // ---------- Permisos (RN-SE-006) ----------

    private IEnumerable<Func<string?, Task<HttpResponseMessage>>> EndpointsAdmin(string objetivo) =>
        new Func<string?, Task<HttpResponseMessage>>[] { tk => Lista(_c, tk) }
            .Concat(Acciones.Select(a => (Func<string?, Task<HttpResponseMessage>>)(tk => AccionMotivo(_c, tk, objetivo, a, MotivoValido))));

    [Fact(DisplayName = "CA-SE006-07/08/09/10/12, CA-N03-R04: sin token o token inválido 401; comprador y asesor 403 { error } en los 6 endpoints admin; nada cambia")]
    public async Task Permisos_de_los_endpoints_admin()
    {
        var (_, _, admin) = await CrearAdministrador(factory, _c);
        var (pendiente, _, _) = await CrearAsesorPendiente(_c);
        var (_, emailComprador) = await CrearComprador(_c);
        var comprador = await ObtenerToken(_c, emailComprador);
        var (asesorId, asesorEmail) = await Sujeto(admin, "asesor", "ACTIVA");
        var asesor = await ObtenerToken(_c, asesorEmail);
        var antesPendiente = await Fotografiar(pendiente);
        var antesAsesor = await Fotografiar(asesorId);

        foreach (var objetivo in new[] { pendiente, asesorId })
        foreach (var llamada in EndpointsAdmin(objetivo))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await llamada(null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await llamada("x.y.z")).StatusCode);
            foreach (var token in new[] { comprador, asesor })
            {
                var r = await llamada(token);
                await AfirmarError(r, HttpStatusCode.Forbidden, MsgSinPermiso);
                Assert.StartsWith("application/json", r.Content.Headers.ContentType?.MediaType);
            }
        }
        Assert.Equal(antesPendiente, await Fotografiar(pendiente));
        Assert.Equal(antesAsesor, await Fotografiar(asesorId));
        Assert.Equal(HttpStatusCode.OK, (await Lista(_c, admin)).StatusCode); // CA-SE006-10
        // El comprador sigue teniendo su logout (único endpoint que modifica datos que puede usar con éxito).
        Assert.Equal(HttpStatusCode.NoContent, (await Logout(_c, comprador)).StatusCode);
    }

    [Fact(DisplayName = "CA-SE006-11, CA-D12-03: el logout del administrador invalida sus otros tokens también en /api/admin")]
    public async Task Logout_del_administrador()
    {
        var (_, email, token1) = await CrearAdministrador(factory, _c);
        var token2 = await ObtenerToken(_c, email);
        var (objetivo, _, _) = await CrearAsesorPendiente(_c);
        Assert.Equal(HttpStatusCode.OK, (await Lista(_c, token2)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Logout(_c, token1)).StatusCode);
        foreach (var llamada in EndpointsAdmin(objetivo))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await llamada(token1)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await llamada(token2)).StatusCode);
        }
        Assert.Equal("PENDIENTE", (await Usuario(factory, objetivo)).EstadoCuenta);
    }

    // ---------- Sesiones afectadas (D-12) ----------

    [Theory(DisplayName = "CA-047-03/07/08, CA-D12-01/06: bloquear invalida todos los tokens; desbloquear no revive los viejos y un login nuevo funciona")]
    [InlineData("asesor")]
    [InlineData("comprador")]
    public async Task Bloquear_y_desbloquear_sesiones(string tipo)
    {
        var (_, _, admin) = await CrearAdministrador(factory, _c);
        var (id, email) = await Sujeto(admin, tipo, "ACTIVA");
        var equipo1 = await ObtenerToken(_c, email);
        var equipo2 = await ObtenerToken(_c, email);
        var otro = await ObtenerToken(_c, (await CrearComprador(_c)).Email);

        await Ok(await AccionMotivo(_c, admin, id, "bloquear", "Uso indebido del portal"));
        foreach (var tk in new[] { equipo1, equipo2 })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, tk)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Logout(_c, tk)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Lista(_c, tk)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await Me(_c, otro)).StatusCode); // CA-D12-06
        var login = await Login(_c, email, PasswordValida);
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal("BLOQUEADA", (await Json(login)).GetProperty("estado").GetString());

        var sv = (await Usuario(factory, id)).SessionVersion;
        var desbloqueo = await Ok(await AccionMotivo(_c, admin, id, "desbloquear", null));
        Assert.Equal("ACTIVA", desbloqueo.GetProperty("estado").GetString());
        if (tipo == "asesor")
            Assert.Equal("APROBADO", desbloqueo.GetProperty("asesor").GetProperty("estadoAutorizacion").GetString());
        Assert.Equal(sv, (await Usuario(factory, id)).SessionVersion);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, equipo1)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, equipo2)).StatusCode);
        var nuevo = await ObtenerToken(_c, email);
        var me = await Json(await Me(_c, nuevo));
        Assert.Equal("ACTIVA", me.GetProperty("estado").GetString());
        Assert.Equal([tipo == "asesor" ? "ASESOR" : "COMPRADOR"], me.GetProperty("roles").EnumerateArray().Select(x => x.GetString()!).ToArray());
        Assert.Equal(HttpStatusCode.OK, (await Me(_c, otro)).StatusCode);
    }

    [Fact(DisplayName = "CA-044-03, CA-045-04, D-15: rechazar sube SessionVersion (token previo 401); reconsiderar no revive tokens y el login nuevo funciona")]
    public async Task Rechazar_y_reconsiderar()
    {
        var (adminId, _, admin) = await CrearAdministrador(factory, _c);
        var (id, email, numero) = await CrearAsesorPendiente(_c);
        var otro = await ObtenerToken(_c, (await CrearComprador(_c)).Email);
        var sv0 = (await Usuario(factory, id)).SessionVersion;
        // Un asesor PENDIENTE no puede obtener token: se simula una sesión previa firmada con la clave de pruebas.
        var previo = Firmar(factory, id, sv0, "ASESOR");

        var rechazo = await Ok(await AccionMotivo(_c, admin, id, "rechazar", "Documento ilegible"));
        Assert.Equal("RECHAZADA", rechazo.GetProperty("estado").GetString());
        Assert.Equal("RECHAZADO", rechazo.GetProperty("asesor").GetProperty("estadoAutorizacion").GetString());
        Assert.Equal(sv0 + 1, (await Usuario(factory, id)).SessionVersion);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, previo)).StatusCode);
        var login = await Login(_c, email, PasswordValida);
        await AfirmarError(login, HttpStatusCode.Forbidden, MsgRechazada);
        Assert.Equal("RECHAZADA", (await Json(login)).GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Me(_c, otro)).StatusCode);

        var rec = await Ok(await AccionMotivo(_c, admin, id, "reconsiderar", "Documento corregido"));
        Assert.Equal("ACTIVA", rec.GetProperty("estado").GetString());
        Assert.Equal("APROBADO", rec.GetProperty("asesor").GetProperty("estadoAutorizacion").GetString());
        Assert.Equal(sv0 + 1, (await Usuario(factory, id)).SessionVersion); // reconsiderar no toca SessionVersion
        var p = (await Perfil(factory, id))!;
        Assert.Equal(adminId, p.AutorizadoPor);
        Assert.NotNull(p.AutorizadoEn);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, previo)).StatusCode);

        var token = await ObtenerToken(_c, email);
        var me = await Json(await Me(_c, token));
        Assert.Equal("ACTIVA", me.GetProperty("estado").GetString());
        Assert.Equal(["ASESOR"], me.GetProperty("roles").EnumerateArray().Select(x => x.GetString()!).ToArray());

        var hist = await HistorialDe(factory, id);
        Assert.Equal(new[] { ("PENDIENTE", "RECHAZADO"), ("RECHAZADO", "APROBADO") },
            hist.Select(h => (h.EstadoAnterior!, h.EstadoNuevo)).ToArray());
        Assert.Equal(new[] { "REGISTRO_ASESOR", "RECHAZAR", "RECONSIDERAR" }, (await AuditoriaDe(factory, id)).Select(a => a.Accion).ToArray());
        Assert.Equal("Documento corregido", JsonPlano((await AuditoriaDe(factory, id)).Last().DatosNuevos)["motivo"]);
        Assert.Equal("RECHAZADA", JsonPlano((await AuditoriaDe(factory, id)).Last().DatosAnteriores)["estado"]);
        _ = numero;
    }

    [Fact(DisplayName = "CA-043-01/09, CA-043R-03, CA-045-03, CA-D12-06: aprobar con {} -> 200; ya no sale en PENDIENTE; entra con su contraseña; no invalida tokens ajenos")]
    public async Task Aprobar_activa_el_acceso()
    {
        var (adminId, _, admin) = await CrearAdministrador(factory, _c);
        var (id, email, _) = await CrearAsesorPendiente(_c, "Aprobado Prueba");
        var otro = await ObtenerToken(_c, (await CrearComprador(_c)).Email);
        var inicio = DateTime.UtcNow.AddSeconds(-1);

        var json = await Ok(await Accion(_c, admin, id, "aprobar", new { }));
        var autorizadoEn = json.GetProperty("asesor").GetProperty("autorizadoEn").GetDateTime().ToUniversalTime();
        Assert.InRange(autorizadoEn, inicio, DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(adminId, (await Perfil(factory, id))!.AutorizadoPor);
        Assert.Equal(HttpStatusCode.OK, (await Me(_c, otro)).StatusCode);

        var pend = await Ok(await Lista(_c, admin, "?estado=PENDIENTE&tamano=100"));
        Assert.DoesNotContain(pend.GetProperty("items").EnumerateArray(), i => i.GetProperty("usuarioId").GetString() == id);
        var act = await Ok(await Lista(_c, admin, "?estado=ACTIVA&buscar=Aprobado%20Prueba&tamano=100"));
        var item = Assert.Single(act.GetProperty("items").EnumerateArray(), i => i.GetProperty("usuarioId").GetString() == id);
        Assert.Equal("APROBADO", item.GetProperty("asesor").GetProperty("estadoAutorizacion").GetString());

        var login = await Login(_c, email, PasswordValida);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(["ASESOR"], (await Json(login)).GetProperty("roles").EnumerateArray().Select(x => x.GetString()!).ToArray());
    }

    [Fact(DisplayName = "CA-044-06: un asesor rechazado no puede volver a registrarse con el mismo correo ni con el mismo documento")]
    public async Task Rechazado_no_puede_volver_a_registrarse()
    {
        var (_, _, admin) = await CrearAdministrador(factory, _c);
        var (id, email, numero) = await CrearAsesorPendiente(_c);
        await Ok(await AccionMotivo(_c, admin, id, "rechazar", MotivoValido));
        await AfirmarError(await RegistrarAsesor(_c, RegistroAsesor(email, DocumentoUnico())), HttpStatusCode.Conflict, MsgCorreoDuplicado);
        await AfirmarError(await RegistrarAsesor(_c, RegistroAsesor(CorreoUnico("otro"), numero)), HttpStatusCode.Conflict, MsgDocumentoDuplicado);
        Assert.Equal("RECHAZADA", (await Usuario(factory, id)).EstadoCuenta);
    }

    [Fact(DisplayName = "CA-061-13: tras un recorrido completo, ninguna fila de auditoría ni historial contiene contraseña, hash, token ni documento")]
    public async Task Auditoria_sin_datos_sensibles()
    {
        var (_, _, admin) = await CrearAdministrador(factory, _c);
        var (id, email, numero) = await CrearAsesorPendiente(_c);
        await Ok(await AccionMotivo(_c, admin, id, "aprobar", null));
        var token = await ObtenerToken(_c, email);
        await Ok(await AccionMotivo(_c, admin, id, "bloquear", MotivoValido));
        await Ok(await AccionMotivo(_c, admin, id, "desbloquear", null));
        var hash = (await Usuario(factory, id)).PasswordHash!;

        var textos = (await AuditoriaDe(factory, id)).SelectMany(a => new[] { a.DatosAnteriores ?? "", a.DatosNuevos ?? "" })
            .Concat((await HistorialDe(factory, id)).Select(h => h.Motivo ?? "")).ToArray();
        Assert.Equal(4, (await AuditoriaDe(factory, id)).Count);
        foreach (var t in textos)
        {
            Assert.DoesNotContain(numero, t);
            Assert.DoesNotContain(PasswordValida, t);
            Assert.DoesNotContain(hash, t);
            Assert.DoesNotContain(token, t);
            Assert.DoesNotContain(admin, t);
        }
    }

    // ---------- Concurrencia ----------

    private async Task<HttpResponseMessage[]> Simultaneas(string admin, string id, params string[] acciones)
    {
        using var barrera = new Barrier(acciones.Length);
        var tareas = acciones.Select(a => Task.Run(async () =>
        {
            var cliente = factory.CreateClient();
            barrera.SignalAndWait(TimeSpan.FromSeconds(10));
            return await AccionMotivo(cliente, admin, id, a, MotivoValido);
        })).ToArray();
        return await Task.WhenAll(tareas);
    }

    [Fact(DisplayName = "CA-043-05: dos aprobaciones simultáneas -> una 200 y otra 409; una sola fila de historial y de auditoría")]
    public async Task Aprobaciones_simultaneas()
    {
        var (_, _, admin) = await CrearAdministrador(factory, _c);
        var (id, _, _) = await CrearAsesorPendiente(_c);
        var r = await Simultaneas(admin, id, "aprobar", "aprobar");
        var codigos = string.Join(",", r.Select(x => (int)x.StatusCode).OrderBy(x => x));
        Assert.Equal("200,409", codigos);
        Assert.Single(await HistorialDe(factory, id));
        Assert.Equal(2, (await AuditoriaDe(factory, id)).Count); // REGISTRO_ASESOR + APROBAR
    }

    [Fact(DisplayName = "CA-044-08: aprobación y rechazo simultáneos -> una 200 y otra 409; estado final coherente")]
    public async Task Aprobar_y_rechazar_simultaneos()
    {
        var (_, _, admin) = await CrearAdministrador(factory, _c);
        var (id, _, _) = await CrearAsesorPendiente(_c);
        var r = await Simultaneas(admin, id, "aprobar", "rechazar");
        var codigos = string.Join(",", r.Select(x => (int)x.StatusCode).OrderBy(x => x));
        Assert.Equal("200,409", codigos);
        var u = await Usuario(factory, id);
        var p = (await Perfil(factory, id))!;
        var h = Assert.Single(await HistorialDe(factory, id));
        Assert.Equal(u.EstadoCuenta == "ACTIVA" ? "APROBADO" : "RECHAZADO", p.EstadoAutorizacion);
        Assert.Equal(p.EstadoAutorizacion, h.EstadoNuevo);
    }

    // ---------- Transacción ----------

    [Fact(DisplayName = "CA-043-08: si falla la escritura del historial, la aprobación no deja cambios (una sola transacción)")]
    public async Task Aprobar_es_atomico()
    {
        await using var f = new CamionApiFactory();
        var c = f.CreateClient();
        var (_, _, admin) = await CrearAdministrador(f, c);
        var (id, _, _) = await CrearAsesorPendiente(c);
        using (var scope = f.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<CamionDbContext>().Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER fallo_historial BEFORE INSERT ON historial_autorizaciones BEGIN SELECT RAISE(ABORT, 'fallo simulado'); END;");

        var r = await AccionMotivo(c, admin, id, "aprobar", null);
        Assert.Equal(HttpStatusCode.InternalServerError, r.StatusCode);
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True((await Json(r)).TryGetProperty("error", out _), texto);
        Assert.DoesNotContain("fallo simulado", texto);

        var u = await Usuario(f, id);
        var p = (await Perfil(f, id))!;
        Assert.Equal("PENDIENTE", u.EstadoCuenta);
        Assert.Equal("PENDIENTE", p.EstadoAutorizacion);
        Assert.Null(p.AutorizadoPor);
        Assert.Null(p.AutorizadoEn);
        Assert.Equal(new[] { "REGISTRO_ASESOR" }, (await AuditoriaDe(f, id)).Select(a => a.Accion).ToArray());
    }
}
