using System.Net;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;

namespace Camion.Api.Tests;

/// <summary>
/// P5 — anti-abuso del contacto público (CA-P5-20, CA-P5-21): límites por IP (ventana corta y diaria, con
/// Microsoft.AspNetCore.RateLimiting) y por teléfono. Cada prueba levanta su propia API con límites bajos (los demás
/// archivos usan límites altos para no volverse frágiles). SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class ContactosLimitesTests
{
    private static Task<InventarioApi> Api(int corta, int diario, int telefono) =>
        InventarioApi.Crear(NuevoReloj(), b =>
        {
            b.UseSetting("Contactos:LimiteVentanaCorta", corta.ToString());
            b.UseSetting("Contactos:LimiteDiario", diario.ToString());
            b.UseSetting("Contactos:LimiteTelefonoDia", telefono.ToString());
        });

    [Fact(DisplayName = "CA-P5-20: más contactos que el límite de la ventana corta por IP -> 429 con el mensaje; también cuentan los rechazados; otras rutas no se limitan")]
    public async Task Ventana_corta_por_ip()
    {
        using var api = await Api(corta: 3, diario: 1000, telefono: 1000);
        var (_, _, slug) = await api.AsesorPublicado("Asesor Limite Ip");
        await api.ContactoOk(CuerpoContacto(slug));
        await api.ContactoOk(CuerpoContacto(slug));
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Contactar(CuerpoContacto(null))).StatusCode);
        var leads = await api.TotalLeads();
        await Error(await api.Contactar(CuerpoContacto(slug)), HttpStatusCode.TooManyRequests, MsgMuchas);
        await Error(await api.Contactar(CuerpoContacto(slug)), HttpStatusCode.TooManyRequests, MsgMuchas);
        Assert.Equal(leads, await api.TotalLeads());
        // El límite es solo del contacto: el catálogo y el login siguen respondiendo.
        Assert.Equal(HttpStatusCode.OK, (await api.Enviar(HttpMethod.Get, "/api/catalogo/inicio", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Enviar(HttpMethod.Get, "/api/leads", api.Admin.Token)).StatusCode);
    }

    [Fact(DisplayName = "CA-P5-20: más contactos que el límite diario por IP -> 429 aunque la ventana corta lo permita")]
    public async Task Diario_por_ip()
    {
        using var api = await Api(corta: 1000, diario: 2, telefono: 1000);
        var (_, _, slug) = await api.AsesorPublicado("Asesor Limite Diario");
        await api.ContactoOk(CuerpoContacto(slug));
        await api.ContactoOk(CuerpoContacto(slug));
        await Error(await api.Contactar(CuerpoContacto(slug)), HttpStatusCode.TooManyRequests, MsgMuchas);
    }

    [Fact(DisplayName = "CA-P5-21: más leads nuevos del mismo teléfono en 24 h que el límite -> 429; el contacto repetido no cuenta; pasadas 24 h vuelve a aceptar")]
    public async Task Por_telefono()
    {
        using var api = await Api(corta: 1000, diario: 1000, telefono: 2);
        var (_, _, slug1) = await api.AsesorPublicado("Asesor Telefono Uno");
        var (_, _, slug2) = await api.AsesorPublicado("Asesor Telefono Dos");
        var (_, _, slug3) = await api.AsesorPublicado("Asesor Telefono Tres");
        var telefono = TelefonoUnico();
        api.Hora(T);
        await api.ContactoOk(CuerpoContacto(slug1, telefono));
        await api.ContactoOk(CuerpoContacto(slug2, "+57 " + telefono));
        await api.ContactoOk(CuerpoContacto(slug1, telefono)); // repetido: no es un lead nuevo
        var leads = await api.TotalLeads();
        await Error(await api.Contactar(CuerpoContacto(slug3, telefono)), HttpStatusCode.TooManyRequests, MsgMuchas);
        Assert.Equal(leads, await api.TotalLeads());
        await api.ContactoOk(CuerpoContacto(slug3, TelefonoUnico())); // otro teléfono sí
        api.Hora(T.AddDays(1).AddMinutes(1));
        await api.ContactoOk(CuerpoContacto(slug3, telefono));
    }
}
