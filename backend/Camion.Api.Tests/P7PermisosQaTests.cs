using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP7;

namespace Camion.Api.Tests;

/// <summary>
/// P7 — pruebas de QA sobre permisos, IDOR y validaciones de borde de ventas, pagos, parámetros y panel
/// (docs/criterios/criterios-P7-ventas-admin.md; D-61 B, D-62 A, D-63 A, D-64 A). SQLite en memoria; nunca MariaDB.
/// Datos ficticios. Las pruebas que guardan parámetros o miran contadores globales usan una API propia.
/// </summary>
public sealed class P7PermisosQaTests(P7Fixture fixture) : IClassFixture<P7Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    // ---------------------------------------------------------------- pagos

    [Fact(DisplayName = "QA CA-P7-32: el asesor no lee ni anula los pagos de su propia suscripción ni registra uno; nada cambia")]
    public async Task Pagos_asesor_ni_siquiera_lo_suyo()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor(Unico("Asesora Pagos Propios QA"));
        var plan = await _api.PlanConPrecio(110_000);
        var sus = (await Esperar(await _api.AsignarConPago(asesor.PerfilId, plan, PagoValido()), HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        var pago = Assert.Single(await _api.PagosDb(sus));

        await Error(await _api.Pagos(sus, asesor.Token), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.AnularPago(pago.Id, token: asesor.Token), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.RegistrarPago(sus, PagoValido(), asesor.Token), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.AsignarConPago(asesor.PerfilId, plan, PagoValido(), asesor.Token), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.AccionSus(sus, "renovar", new { pago = PagoValido() }, asesor.Token), HttpStatusCode.Forbidden,
            MsgSinPermiso);

        var despues = Assert.Single(await _api.PagosDb(sus));
        Assert.Equal(("REGISTRADO", (DateTime?)null), (despues.Estado, despues.AnuladoEn));
        Assert.Single(await _api.Auditoria("Pago", pago.Id.ToString()));
    }

    [Fact(DisplayName = "QA CA-P7-31: anular con motivo > 500 -> 400; la segunda anulación -> 409 y conserva quién, cuándo y motivo originales")]
    public async Task Anular_bordes()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor(Unico("Asesor Anula Bordes QA"));
        var plan = await _api.PlanConPrecio(80_000);
        var sus = (await Esperar(await _api.AsignarConPago(asesor.PerfilId, plan, PagoValido()), HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        var pago = Assert.Single(await _api.PagosDb(sus));

        await Error400(await _api.AnularPago(pago.Id, new string('m', 501)), "motivo", MsgMotivo);
        await Error400(await _api.AnularPago(pago.Id, "    "), "motivo", MsgMotivo);
        Assert.Equal("REGISTRADO", Assert.Single(await _api.PagosDb(sus)).Estado);

        await Esperar(await _api.AnularPago(pago.Id, new string('m', 500)), HttpStatusCode.OK);
        var anulado = Assert.Single(await _api.PagosDb(sus));

        _api.Hora(T.AddHours(2));
        await Error(await _api.AnularPago(pago.Id, "Otro motivo distinto (QA)"), HttpStatusCode.Conflict, MsgYaAnulado);
        // Un 409 con motivo inválido también es 409 o 400, nunca 200.
        Assert.NotEqual(HttpStatusCode.OK, (await _api.AnularPago(pago.Id, "x")).StatusCode);
        var final = Assert.Single(await _api.PagosDb(sus));
        Assert.Equal((anulado.Estado, anulado.AnuladoEn, anulado.MotivoAnulacion), (final.Estado, final.AnuladoEn, final.MotivoAnulacion));
        Assert.Equal(new[] { "REGISTRAR", "ANULAR" }, (await _api.Auditoria("Pago", pago.Id.ToString())).Select(e => e.Accion).ToArray());
        _api.Hora(T);
    }

    [Fact(DisplayName = "QA CA-P7-48, 51: el panel no expone pagos (valores, referencias, medios ni totales)")]
    public async Task Panel_sin_datos_de_pagos()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        api.Hora(T);
        var antes = await (await api.Panel()).Content.ReadAsStringAsync();
        var asesor = await api.CrearAsesor("Asesora Panel Pagos QA");
        var plan = await api.PlanConPrecio(123_457);
        await Esperar(await api.AsignarConPago(asesor.PerfilId, plan,
            PagoValido(valorCop: 123_457, medio: "CONSIGNACION", referencia: "REF-QA-778899")), HttpStatusCode.Created);

        var r = await api.Panel();
        var texto = await r.Content.ReadAsStringAsync();
        foreach (var prohibido in new[] { "123457", "123.457", "REF-QA-778899", "CONSIGNACION", "pago", "valor", "precio", "total" })
            Assert.DoesNotContain(prohibido, texto, StringComparison.OrdinalIgnoreCase);
        // Mismas claves que antes del pago.
        var claves = (string s) => JsonDocument.Parse(s).RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(claves(antes), claves(texto));
    }

    // ---------------------------------------------------------------- ventas

    public static TheoryData<string> IdsRaros => new() { "0", "-1", "abc", "1.5", "99999999999999999999" };

    [Theory(DisplayName = "QA CA-P7-07: historial de ventas con ids no válidos -> 404 o 400, nunca datos")]
    [MemberData(nameof(IdsRaros))]
    public async Task Ventas_ids_raros(string id)
    {
        var r = await _api.Enviar(HttpMethod.Get, $"/api/inventario/vehiculos/{id}/ventas", _api.A.Token);
        Assert.Contains(r.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest });
        Assert.DoesNotContain("precioFinalCop", await r.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "QA CA-P7-02, 05, 07: IDOR; ningún filtro deja ver al asesor las ventas (vigentes o revertidas) de otro")]
    public async Task Ventas_idor_por_vehiculo_y_filtros()
    {
        _api.Hora(T);
        var uno = await _api.CrearAsesor(Unico("Asesor Uno IDOR QA"));
        var dos = await _api.CrearAsesor(Unico("Asesora Dos IDOR QA"));
        var vigenteDos = await _api.VehiculoVendido(dos, "REFERIDO", precio: 77_700_000);
        var revertidoDos = await _api.VehiculoVendido(dos, "OTRO", precio: 66_600_000, observaciones: "Permuta (QA)");
        await Esperar(await _api.RevertirVenta(dos.Token, revertidoDos, "Desistió el cliente (QA)"), HttpStatusCode.OK);
        var propio = await _api.VehiculoVendido(uno);

        foreach (var q in new[]
                 {
                     "", "?estado=REVERTIDA", "?estado=VIGENTE", "?origen=REFERIDO", "?origen=OTRO", $"?asesorId={dos.PerfilId}",
                     $"?asesorId={dos.PerfilId}&estado=REVERTIDA", "?tamano=100", $"?vehiculoId={vigenteDos}", $"?desde={HoyT}&hasta={HoyT}"
                 })
        {
            var r = await _api.Ventas(uno.Token, q);
            var texto = await r.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            foreach (var prohibido in new[] { "77700000", "66600000", "Desistió el cliente (QA)", "Permuta (QA)", dos.Nombre })
                Assert.DoesNotContain(prohibido, texto, StringComparison.Ordinal);
            var vehiculos = JsonDocument.Parse(texto).RootElement.GetProperty("items").EnumerateArray()
                .Select(i => i.GetProperty("vehiculo").GetProperty("id").GetInt64()).ToArray();
            Assert.All(vehiculos, v => Assert.Equal(propio, v));
        }
        foreach (var ajeno in new[] { vigenteDos, revertidoDos })
            await Error(await _api.VentasVehiculo(uno.Token, ajeno), HttpStatusCode.NotFound, MsgVehiculoNoEncontrado);
        // El administrador sí los ve.
        Assert.Equal(2, (await _api.VentasOk(_api.Admin.Token, $"?asesorId={dos.PerfilId}")).GetProperty("total").GetInt32());
        // Comprador: 403 también sobre un vehículo real.
        await Error(await _api.VentasVehiculo(_api.TokenComprador, vigenteDos), HttpStatusCode.Forbidden, MsgSinPermiso);
    }

    [Fact(DisplayName = "QA CA-P7-23: dos reversiones simultáneas de la misma venta -> una 200 y otra 409; un solo evento REVERTIR")]
    public async Task Reversion_concurrente()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor(Unico("Asesora Revierte Doble QA"));
        var vehiculo = await _api.VehiculoVendido(asesor);
        var respuestas = await Task.WhenAll(
            _api.RevertirVenta(asesor.Token, vehiculo), _api.RevertirVenta(_api.Admin.Token, vehiculo));
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
            respuestas.Select(r => r.StatusCode).OrderBy(c => (int)c).ToArray());
        var venta = Assert.Single(await _api.VentasDb(vehiculo));
        Assert.NotNull(venta.RevertidaEn);
        // Auditoria.RevertirVenta = "REVERTIR" sobre la entidad Venta.
        Assert.Single(await _api.Auditoria("Venta", venta.Id.ToString()), e => e.Accion == "REVERTIR");
    }

    // ---------------------------------------------------------------- parámetros

    public static TheoryData<string, Dictionary<string, object?>, string, string> RedesInvalidas => new()
    {
        { "instagram JAVASCRIPT:", CuerpoContacto(instagram: "JAVASCRIPT:alert(1)"), "instagram", MsgInstagram },
        { "instagram javascript con espacios", CuerpoContacto(instagram: "  javascript:alert(document.cookie)"), "instagram", MsgInstagram },
        { "instagram data:", CuerpoContacto(instagram: "data:text/html,<script>alert(1)</script>"), "instagram", MsgInstagram },
        { "instagram sin esquema", CuerpoContacto(instagram: "//www.instagram.com/cuenta"), "instagram", MsgInstagram },
        { "instagram en la ruta de otro dominio", CuerpoContacto(instagram: "https://evil.test/www.instagram.com/cuenta"), "instagram", MsgInstagram },
        { "instagram arroba hacia otro host", CuerpoContacto(instagram: "https://www.instagram.com@evil.test/cuenta"), "instagram", MsgInstagram },
        { "instagram subdominio falso", CuerpoContacto(instagram: "https://instagram.com.evil.test/cuenta"), "instagram", MsgInstagram },
        { "instagram ftp", CuerpoContacto(instagram: "ftp://www.instagram.com/cuenta"), "instagram", MsgInstagram },
        { "instagram con espacio", CuerpoContacto(instagram: "https://www.instagram.com/cuenta falsa"), "instagram", MsgInstagram },
        { "instagram con <", CuerpoContacto(instagram: "https://www.instagram.com/<script>"), "instagram", MsgInstagram },
        { "instagram larga", CuerpoContacto(instagram: "https://www.instagram.com/" + new string('a', 180)), "instagram", MsgInstagram },
        { "tiktok javascript:", CuerpoContacto(tiktok: "javascript:alert(1)"), "tiktok", MsgTiktok },
        { "tiktok http", CuerpoContacto(tiktok: "http://www.tiktok.com/@cuenta"), "tiktok", MsgTiktok },
        { "tiktok subdominio falso", CuerpoContacto(tiktok: "https://www.tiktok.com.evil.test/@cuenta"), "tiktok", MsgTiktok },
        { "tiktok de otro dominio", CuerpoContacto(tiktok: "https://evil.test/@cuenta"), "tiktok", MsgTiktok },
        { "tiktok arroba hacia otro host", CuerpoContacto(tiktok: "https://www.tiktok.com@evil.test/@cuenta"), "tiktok", MsgTiktok },
        { "tiktok con comilla simple", CuerpoContacto(tiktok: "https://www.tiktok.com/@a'onmouseover=x"), "tiktok", MsgTiktok },
        { "tiktok sin ruta", CuerpoContacto(tiktok: "https://www.tiktok.com"), "tiktok", MsgTiktok },
    };

    [Theory(DisplayName = "QA CA-P7-37, 41: Instagram y TikTok solo https de su dominio; rechaza javascript:, data:, otros hosts -> 400, nada cambia")]
    [MemberData(nameof(RedesInvalidas))]
    public async Task Redes_invalidas(string caso, Dictionary<string, object?> cuerpo, string campo, string mensaje)
    {
        Assert.NotNull(caso);
        await Error400(await _api.GuardarContacto(cuerpo), campo, mensaje);
        Assert.Equal(0, await _api.Leer(db => db.ConfiguracionesSistema.CountAsync()));
        Assert.Empty(await _api.Leer(db => db.AuditoriaEventos.AsNoTracking().Where(a => a.Entidad == "Parametro").ToListAsync()));
    }

    [Fact(DisplayName = "QA CA-P7-37, 42: hosts sin www se aceptan; la lectura pública tras guardar sigue sin datos internos")]
    public async Task Publico_sin_datos_internos_tras_guardar()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        api.Hora(T);
        var guardado = await Esperar(await api.GuardarContacto(CuerpoContacto(whatsapp: "3191112233", correo: "portal@prueba.test",
            instagram: "https://instagram.com/cuenta_qa", tiktok: "https://tiktok.com/@cuenta_qa")), HttpStatusCode.OK);
        Assert.Equal("https://instagram.com/cuenta_qa", guardado.GetProperty("instagram").GetString());

        var nombreAdmin = await api.Leer(db => db.Users.AsNoTracking().Where(u => u.Id == api.Admin.Id)
            .Select(u => u.NombreCompleto).SingleAsync());
        foreach (var token in new[] { null, api.TokenComprador, api.A.Token, api.Admin.Token })
        {
            var r = await api.Enviar(HttpMethod.Get, "/api/portal/contacto", token);
            var texto = await r.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var json = JsonDocument.Parse(texto).RootElement;
            Assert.Equal(new[] { "correo", "horario", "instagram", "tiktok", "whatsapp", "whatsappEnlace" },
                json.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
            foreach (var prohibido in new[] { api.Admin.Id, api.Admin.Email, nombreAdmin, "actualizado", "configurado", "clave", "portal.contacto" })
                Assert.DoesNotContain(prohibido, texto, StringComparison.OrdinalIgnoreCase);
        }
        foreach (var metodo in new[] { HttpMethod.Post, HttpMethod.Delete, HttpMethod.Patch })
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await api.Enviar(metodo, "/api/portal/contacto", null, CuerpoContacto())).StatusCode);
    }

    [Fact(DisplayName = "QA CA-P7-40: publicación cuyo dueño es administrador -> WhatsApp de la ficha = el número configurado")]
    public async Task Whatsapp_ficha_duenio_administrador()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        api.Hora(T);
        var (asesor, _, slug) = await api.AsesorPublicado("Asesor Promovido Parametros QA");
        await api.Db(async db =>
        {
            var rol = await db.Roles.SingleAsync(r => r.Name == "ADMINISTRADOR");
            db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = asesor.UsuarioId, RoleId = rol.Id });
        });
        // Sin parámetro: el de appsettings.json (respaldo).
        Assert.Equal("https://wa.me/573166152907",
            (await Esperar(await api.DetallePortal(slug), HttpStatusCode.OK)).GetProperty("whatsapp").GetString());

        await Esperar(await api.GuardarContacto(CuerpoContacto(whatsapp: "+57 320 555 0101")), HttpStatusCode.OK);
        Assert.Equal("https://wa.me/573205550101",
            (await Esperar(await api.DetallePortal(slug), HttpStatusCode.OK)).GetProperty("whatsapp").GetString());
        // Y coincide con el enlace público del pie.
        Assert.Equal("https://wa.me/573205550101", (await api.ContactoPublico()).GetProperty("whatsappEnlace").GetString());
    }

    // ---------------------------------------------------------------- panel

    [Fact(DisplayName = "QA CA-P7-49: panel solo para el administrador; asesor y comprador 403 también con parámetros en la consulta")]
    public async Task Panel_solo_admin()
    {
        foreach (var token in new[] { _api.A.Token, _api.B.Token, _api.TokenComprador })
        {
            await Error(await _api.Panel(token), HttpStatusCode.Forbidden, MsgSinPermiso);
            await Error(await _api.Enviar(HttpMethod.Get, "/api/admin/panel?asesorId=1&rol=ADMINISTRADOR", token),
                HttpStatusCode.Forbidden, MsgSinPermiso);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Enviar(HttpMethod.Get, "/api/admin/panel", "token-falso-qa")).StatusCode);
        foreach (var metodo in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
            Assert.Contains((await _api.Enviar(metodo, "/api/admin/panel", _api.Admin.Token, new { })).StatusCode,
                new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });
        Assert.Equal(HttpStatusCode.OK, (await _api.Panel()).StatusCode);
    }
}
