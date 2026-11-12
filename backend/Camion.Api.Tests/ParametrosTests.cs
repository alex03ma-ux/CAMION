using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP7;

namespace Camion.Api.Tests;

/// <summary>
/// P7 — parámetros de contacto del portal (HU-081, RN-PA-001, D-42, D-47, D-63 A; CA-P7-35 a CA-P7-43): lectura pública,
/// edición del administrador con validación y auditoría, y WhatsApp de la ficha. SQLite en memoria; nunca MariaDB.
/// Esta clase no guarda parámetros en la base del fixture: el flujo completo usa una API propia.
/// </summary>
public sealed class ParametrosTests(P7Fixture fixture) : IClassFixture<P7Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    public static TheoryData<string, string> RutasAdmin => new()
    {
        { "GET", "/api/admin/parametros/contacto" },
        { "PUT", "/api/admin/parametros/contacto" },
    };

    [Theory(DisplayName = "CA-P7-43: parámetros del administrador -> 401 sin sesión; 403 asesor y comprador")]
    [MemberData(nameof(RutasAdmin))]
    public async Task Permisos(string metodo, string ruta)
    {
        var cuerpo = CuerpoContacto();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Enviar(new HttpMethod(metodo), ruta, null, cuerpo)).StatusCode);
        foreach (var token in new[] { _api.A.Token, _api.TokenComprador })
            await Error(await _api.Enviar(new HttpMethod(metodo), ruta, token, cuerpo), HttpStatusCode.Forbidden, MsgSinPermiso);
        Assert.Equal(0, await _api.Leer(db => db.ConfiguracionesSistema.CountAsync()));
    }

    public static TheoryData<string, Dictionary<string, object?>, string, string> Invalidos => new()
    {
        { "whatsapp fijo", CuerpoContacto(whatsapp: "604 444 1234"), "whatsapp", MsgWhatsapp },
        { "whatsapp corto", CuerpoContacto(whatsapp: "316615"), "whatsapp", MsgWhatsapp },
        { "whatsapp extranjero", CuerpoContacto(whatsapp: "+1 316 615 2907"), "whatsapp", MsgWhatsapp },
        { "sin whatsapp", CuerpoContacto(whatsapp: null), "whatsapp", MsgWhatsapp },
        { "correo inválido", CuerpoContacto(correo: "contacto@"), "correo", MsgCorreo },
        { "sin correo", CuerpoContacto(correo: " "), "correo", MsgCorreo },
        { "horario vacío", CuerpoContacto(horario: Array.Empty<string>()), "horario", MsgHorario },
        { "horario de 5 líneas", CuerpoContacto(horario: new[] { "a", "b", "c", "d", "e" }), "horario", MsgHorario },
        { "línea vacía", CuerpoContacto(horario: new[] { "Lunes", "  " }), "horario", MsgHorario },
        { "línea larga", CuerpoContacto(horario: new[] { new string('h', 101) }), "horario", MsgHorario },
        { "línea con HTML", CuerpoContacto(horario: new[] { "<script>alert(1)</script>" }), "horario", MsgHorario },
        { "instagram http", CuerpoContacto(instagram: "http://www.instagram.com/cuenta"), "instagram", MsgInstagram },
        { "instagram otro dominio", CuerpoContacto(instagram: "https://www.instagram.com.evil.test/cuenta"), "instagram", MsgInstagram },
        { "instagram javascript", CuerpoContacto(instagram: "javascript:alert(1)"), "instagram", MsgInstagram },
        { "instagram sin ruta", CuerpoContacto(instagram: "https://www.instagram.com/"), "instagram", MsgInstagram },
        { "instagram con usuario", CuerpoContacto(instagram: "https://x@www.instagram.com/cuenta"), "instagram", MsgInstagram },
        { "instagram con puerto", CuerpoContacto(instagram: "https://www.instagram.com:8443/cuenta"), "instagram", MsgInstagram },
        { "instagram con comillas", CuerpoContacto(instagram: "https://www.instagram.com/a\"onclick=x"), "instagram", MsgInstagram },
        { "tiktok de instagram", CuerpoContacto(tiktok: "https://www.instagram.com/cuenta"), "tiktok", MsgTiktok },
        { "tiktok otro dominio", CuerpoContacto(tiktok: "https://www.tiktok.co/@cuenta"), "tiktok", MsgTiktok },
    };

    [Theory(DisplayName = "CA-P7-37, CA-P7-41: dato inválido -> 400 con el campo y el mensaje; nada cambia")]
    [MemberData(nameof(Invalidos))]
    public async Task Invalido(string caso, Dictionary<string, object?> cuerpo, string campo, string mensaje)
    {
        Assert.NotNull(caso);
        await Error400(await _api.GuardarContacto(cuerpo), campo, mensaje);
        Assert.Equal(0, await _api.Leer(db => db.ConfiguracionesSistema.CountAsync()));
        Assert.Empty(await _api.Leer(db => db.AuditoriaEventos.AsNoTracking().Where(a => a.Entidad == "Parametro").ToListAsync()));
    }

    [Fact(DisplayName = "CA-P7-42: GET /api/portal/contacto es anónimo y solo trae los datos públicos de contacto")]
    public async Task Publico_solo_contacto()
    {
        var json = await _api.ContactoPublico();
        Assert.Equal(new[] { "correo", "horario", "instagram", "tiktok", "whatsapp", "whatsappEnlace" },
            json.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await _api.Enviar(HttpMethod.Put, "/api/portal/contacto", null, CuerpoContacto())).StatusCode);
    }

    [Fact(DisplayName = "CA-P7-35, 36, 38, 39, 40: valores de D-42 por defecto; guardar -> 200, pie y ficha usan los nuevos, auditoría CAMBIAR")]
    public async Task Flujo_completo()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        api.Hora(T);

        // CA-P7-38: sin guardar nada, los valores confirmados de D-42 (WhatsApp de appsettings).
        var publico = await api.ContactoPublico();
        Assert.Equal("3166152907", publico.GetProperty("whatsapp").GetString());
        Assert.Equal("https://wa.me/573166152907", publico.GetProperty("whatsappEnlace").GetString());
        Assert.Equal("vc.automotores@gmail.com", publico.GetProperty("correo").GetString());
        Assert.Equal(HorarioD42, Lista(publico, "horario"));
        Assert.Equal(JsonValueKind.Null, publico.GetProperty("instagram").ValueKind);
        var admin = await Esperar(await api.Enviar(HttpMethod.Get, "/api/admin/parametros/contacto", api.Admin.Token), HttpStatusCode.OK);
        Assert.False(admin.GetProperty("configurado").GetBoolean());
        Assert.Equal(JsonValueKind.Null, admin.GetProperty("actualizadoEn").ValueKind);

        // Ficha de un asesor con teléfono fijo: usa el WhatsApp del portal (D-49).
        var (asesor, _, slug) = await api.AsesorPublicado("Asesora Fijo Parametros");
        await api.Db(async db => (await db.Users.SingleAsync(u => u.Id == asesor.UsuarioId)).PhoneNumber = "+576044441234");
        Assert.Equal("https://wa.me/573166152907",
            (await Esperar(await api.DetallePortal(slug), HttpStatusCode.OK)).GetProperty("whatsapp").GetString());

        // CA-P7-36: guardar.
        api.Hora(T.AddMinutes(5));
        var guardado = await Esperar(await api.GuardarContacto(CuerpoContacto(whatsapp: "+57 317 000 0001",
            correo: "  Contacto@Prueba.TEST ", horario: new[] { "  Lunes a viernes: 8 a 5  ", "Sábados: 8 a 12" },
            instagram: "https://www.instagram.com/cuenta_prueba", tiktok: "  ")), HttpStatusCode.OK);
        Assert.Equal("3170000001", guardado.GetProperty("whatsapp").GetString());
        Assert.Equal("contacto@prueba.test", guardado.GetProperty("correo").GetString());
        Assert.Equal(new[] { "Lunes a viernes: 8 a 5", "Sábados: 8 a 12" }, Lista(guardado, "horario"));
        Assert.Equal("https://www.instagram.com/cuenta_prueba", guardado.GetProperty("instagram").GetString());
        Assert.Equal(JsonValueKind.Null, guardado.GetProperty("tiktok").ValueKind);   // CA-P7-39: vacío = no se muestra
        Assert.True(guardado.GetProperty("configurado").GetBoolean());
        Assert.Equal(T.AddMinutes(5), Fecha(guardado, "actualizadoEn"));
        Assert.False(string.IsNullOrEmpty(guardado.GetProperty("actualizadoPor").GetString()));

        publico = await api.ContactoPublico();
        Assert.Equal("https://wa.me/573170000001", publico.GetProperty("whatsappEnlace").GetString());
        Assert.Equal("contacto@prueba.test", publico.GetProperty("correo").GetString());
        // CA-P7-40: la ficha usa el número nuevo; un asesor con celular sigue con el suyo.
        Assert.Equal("https://wa.me/573170000001",
            (await Esperar(await api.DetallePortal(slug), HttpStatusCode.OK)).GetProperty("whatsapp").GetString());
        var (_, _, slugCelular) = await api.AsesorPublicado("Asesor Celular Parametros");
        Assert.Equal("https://wa.me/573001234567",
            (await Esperar(await api.DetallePortal(slugCelular), HttpStatusCode.OK)).GetProperty("whatsapp").GetString());

        var eventos = await api.Leer(db => db.AuditoriaEventos.AsNoTracking().Where(a => a.Entidad == "Parametro")
            .OrderBy(a => a.Id).ToListAsync());
        var primero = Assert.Single(eventos);
        Assert.Equal(("portal.contacto", "CAMBIAR", api.Admin.Id), (primero.EntidadId, primero.Accion, primero.UsuarioId));
        Assert.Null(primero.DatosAnteriores);
        Assert.Equal("3170000001", JsonDe(primero.DatosNuevos)["whatsapp"].GetString());

        // Mismos valores: 200 sin evento nuevo.
        await Esperar(await api.GuardarContacto(CuerpoContacto(whatsapp: "317 000 0001", correo: "contacto@prueba.test",
            horario: new[] { "Lunes a viernes: 8 a 5", "Sábados: 8 a 12" }, instagram: "https://www.instagram.com/cuenta_prueba")),
            HttpStatusCode.OK);
        Assert.Single(await api.Leer(db => db.AuditoriaEventos.AsNoTracking().Where(a => a.Entidad == "Parametro").ToListAsync()));

        // Cambio: evento con los valores anteriores y nuevos; TikTok con @.
        await Esperar(await api.GuardarContacto(CuerpoContacto(whatsapp: "3180000002", tiktok: "https://www.tiktok.com/@cuenta")),
            HttpStatusCode.OK);
        var ultimo = (await api.Leer(db => db.AuditoriaEventos.AsNoTracking().Where(a => a.Entidad == "Parametro")
            .OrderBy(a => a.Id).ToListAsync())).Last();
        Assert.Equal("3170000001", JsonDe(ultimo.DatosAnteriores)["whatsapp"].GetString());
        Assert.Equal("3180000002", JsonDe(ultimo.DatosNuevos)["whatsapp"].GetString());
        Assert.Equal("https://www.tiktok.com/@cuenta", (await api.ContactoPublico()).GetProperty("tiktok").GetString());
        Assert.Equal(1, await api.Leer(db => db.ConfiguracionesSistema.CountAsync()));
    }

    [Fact(DisplayName = "Robustez: un valor guardado mal formado no rompe el portal (se usan los de D-42)")]
    public async Task Valor_mal_formado()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        await api.Db(db =>
        {
            db.ConfiguracionesSistema.Add(new Camion.Api.Models.ConfiguracionSistema { Clave = "portal.contacto", ValorJson = "{\"x\":1}" });
            return Task.CompletedTask;
        });
        Assert.Equal("3166152907", (await api.ContactoPublico()).GetProperty("whatsapp").GetString());
    }
}
