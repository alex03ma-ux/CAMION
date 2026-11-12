using System.Net;
using Camion.Api.Tests.Infraestructura;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact(DisplayName = "CA-P5-21, B-1: el límite por teléfono cuenta leads nuevos y contactos repetidos, desde cualquier IP; al superarlo -> 429 sin evento ni consentimiento nuevos; pasadas 24 h vuelve a aceptar")]
    public async Task Por_telefono_desde_varias_ip()
    {
        // Cada IP envía una sola vez (límite por IP 2), así que solo el límite por teléfono (3) puede frenar.
        using var api = await InventarioApi.Crear(NuevoReloj(), b =>
        {
            b.UseSetting("Contactos:LimiteVentanaCorta", "2");
            b.UseSetting("Contactos:LimiteDiario", "2");
            b.UseSetting("Contactos:LimiteTelefonoDia", "3");
            b.ConfigureServices(s => s.AddSingleton<IStartupFilter, IpDePrueba>());
        });
        var (_, _, slug1) = await api.AsesorPublicado("Asesor Telefono Uno");
        var (_, _, slug2) = await api.AsesorPublicado("Asesor Telefono Dos");
        var telefono = TelefonoUnico();
        var ip = 0;
        Task<HttpResponseMessage> Desde(object cuerpo)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/contactos") { Content = JsonContent.Create(cuerpo) };
            req.Headers.Add(IpDePrueba.Cabecera, $"10.0.0.{++ip}");
            return api.C.SendAsync(req);
        }
        api.Hora(T);
        Assert.Equal(HttpStatusCode.Created, (await Desde(CuerpoContacto(slug1, telefono))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Desde(CuerpoContacto(slug1, "+57 " + telefono))).StatusCode); // repetido
        Assert.Equal(HttpStatusCode.Created, (await Desde(CuerpoContacto(slug2, telefono))).StatusCode);
        var lead = await api.Leer(db => db.Leads.AsNoTracking().FirstAsync(l => l.Publicacion!.SlugPublico == slug1));
        var (leads, eventos, pruebas) = (await api.TotalLeads(), (await api.EventosDb(lead.Id)).Count,
            await api.Leer(db => db.ConsentimientosDatos.CountAsync()));

        for (var i = 0; i < 5; i++)
        {
            await Error(await Desde(CuerpoContacto(slug1, telefono)), HttpStatusCode.TooManyRequests, MsgMuchas); // repetido
            await Error(await Desde(CuerpoContacto(slug2, telefono)), HttpStatusCode.TooManyRequests, MsgMuchas);
        }
        Assert.Equal(leads, await api.TotalLeads());
        Assert.Equal(eventos, (await api.EventosDb(lead.Id)).Count);
        Assert.Equal(pruebas, await api.Leer(db => db.ConsentimientosDatos.CountAsync()));
        Assert.Equal(HttpStatusCode.Created, (await Desde(CuerpoContacto(slug1, TelefonoUnico()))).StatusCode); // otro teléfono sí

        api.Hora(T.AddDays(1).AddMinutes(1));
        Assert.Equal(HttpStatusCode.Created, (await Desde(CuerpoContacto(slug1, telefono))).StatusCode);
    }

    /// <summary>Solo en pruebas: toma la IP de la conexión de una cabecera para simular visitantes distintos.</summary>
    private sealed class IpDePrueba : IStartupFilter
    {
        public const string Cabecera = "X-Ip-Prueba";
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (http, siguiente) =>
            {
                if (http.Request.Headers.TryGetValue(Cabecera, out var valor))
                    http.Connection.RemoteIpAddress = IPAddress.Parse(valor.ToString());
                await siguiente();
            });
            next(app);
        };
    }
}
