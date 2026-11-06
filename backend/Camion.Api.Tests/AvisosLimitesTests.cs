using System.Net;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;
using static Camion.Api.Tests.Infraestructura.AyudasP5b;

namespace Camion.Api.Tests;

/// <summary>
/// P5b — anti-abuso compartido con el contacto de P5 (CA-P5b-24, CA-P5b-25) y transacción del aviso (CA-P5b-20). Cada prueba
/// levanta su propia API con límites bajos. SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class AvisosLimitesTests
{
    private static Task<InventarioApi> Api(int corta, int diario, int telefono) =>
        InventarioApi.Crear(NuevoReloj(), b =>
        {
            b.UseSetting("Contactos:LimiteVentanaCorta", corta.ToString());
            b.UseSetting("Contactos:LimiteDiario", diario.ToString());
            b.UseSetting("Contactos:LimiteTelefonoDia", telefono.ToString());
        });

    [Fact(DisplayName = "CA-P5b-24: el cupo por IP se comparte entre el contacto de vehículo y los dos avisos; al superarlo -> 429 en las tres rutas; /api/leads no se limita")]
    public async Task Cupo_por_ip_compartido()
    {
        using var api = await Api(corta: 3, diario: 1000, telefono: 1000);
        var (_, _, slug) = await api.AsesorPublicado("Asesor Cupo Compartido");
        await api.ContactoOk(CuerpoContacto(slug));
        await api.AvisoOk(RutaVende, CuerpoVende());
        await api.AvisoOk(RutaSeguro, CuerpoSeguro());
        var leads = await api.TotalLeads();
        await Error(await api.Aviso(RutaVende, CuerpoVende()), HttpStatusCode.TooManyRequests, MsgMuchas);
        await Error(await api.Aviso(RutaSeguro, CuerpoSeguro()), HttpStatusCode.TooManyRequests, MsgMuchas);
        await Error(await api.Contactar(CuerpoContacto(slug)), HttpStatusCode.TooManyRequests, MsgMuchas);
        Assert.Equal(leads, await api.TotalLeads());
        Assert.Equal(HttpStatusCode.OK, (await api.Enviar(HttpMethod.Get, "/api/leads", api.Admin.Token)).StatusCode);
    }

    [Fact(DisplayName = "CA-P5b-24: el límite diario por IP también cuenta los avisos")]
    public async Task Diario_por_ip()
    {
        using var api = await Api(corta: 1000, diario: 2, telefono: 1000);
        await api.AvisoOk(RutaVende, CuerpoVende());
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Aviso(RutaSeguro, CuerpoSeguro(tipo: null))).StatusCode); // también cuenta
        await Error(await api.Aviso(RutaSeguro, CuerpoSeguro()), HttpStatusCode.TooManyRequests, MsgMuchas);
    }

    [Fact(DisplayName = "CA-P5b-25: el límite por teléfono suma contactos, avisos y repetidos; al superarlo -> 429 sin lead, evento ni consentimiento; pasadas 24 h vuelve a aceptar")]
    public async Task Por_telefono_compartido()
    {
        using var api = await Api(corta: 1000, diario: 1000, telefono: 3);
        var (_, _, slug) = await api.AsesorPublicado("Asesor Telefono Compartido");
        var t = TelefonoUnico();
        api.Hora(T);
        await api.ContactoOk(CuerpoContacto(slug, t));      // 1: contacto de vehículo
        await api.AvisoOk(RutaVende, CuerpoVende(t));       // 2: aviso nuevo
        await api.AvisoOk(RutaVende, CuerpoVende(t, "Otra descripción (prueba)")); // 3: repetido
        var vende = await api.Leer(db => db.Leads.AsNoTracking().SingleAsync(l => l.Telefono == "+57" + t && l.Origen == "VENDE_TU_CAMION"));
        var antes = (await api.TotalLeads(), (await api.EventosDb(vende.Id)).Count, await api.Leer(db => db.ConsentimientosDatos.CountAsync()));

        await Error(await api.Aviso(RutaSeguro, CuerpoSeguro(t)), HttpStatusCode.TooManyRequests, MsgMuchas);
        await Error(await api.Aviso(RutaVende, CuerpoVende(t)), HttpStatusCode.TooManyRequests, MsgMuchas);
        await Error(await api.Contactar(CuerpoContacto(slug, t)), HttpStatusCode.TooManyRequests, MsgMuchas);
        Assert.Equal(antes, (await api.TotalLeads(), (await api.EventosDb(vende.Id)).Count,
            await api.Leer(db => db.ConsentimientosDatos.CountAsync())));
        await api.AvisoOk(RutaSeguro, CuerpoSeguro(TelefonoUnico()));   // otro teléfono sí

        api.Hora(T.AddDays(1).AddMinutes(1));
        await api.AvisoOk(RutaSeguro, CuerpoSeguro(t));
    }

    private const string VersionQueFalla = "FALLA-PRUEBA-P5B";

    [Theory(DisplayName = "CA-P5b-20: si falla la escritura del consentimiento no queda ni el aviso, ni su historial, ni la auditoría; 500 sin detalles")]
    [InlineData(RutaVende)]
    [InlineData(RutaSeguro)]
    public async Task Falla_al_guardar_no_deja_nada(string ruta)
    {
        using var api = await InventarioApi.Crear(NuevoReloj(), b => b.UseSetting("PoliticaDatos:Version", VersionQueFalla));
        await api.Db(db => db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER falla_consentimiento_p5b BEFORE INSERT ON consentimientos_datos " +
            $"WHEN NEW.version_politica = '{VersionQueFalla}' BEGIN SELECT RAISE(ABORT, 'falla de prueba'); END;"));
        var antes = (await api.TotalLeads(), await api.Leer(db => db.EventosLead.CountAsync()),
            await api.Leer(db => db.ConsentimientosDatos.CountAsync()),
            await api.Leer(db => db.AuditoriaEventos.CountAsync(a => a.Entidad == "Lead")));

        var t = TelefonoUnico();
        var r = await api.Aviso(ruta, ruta == RutaVende ? CuerpoVende(t) : CuerpoSeguro(t));
        await Error(r, HttpStatusCode.InternalServerError, "No se pudo completar la operación. Intenta de nuevo en unos minutos.");

        Assert.Equal(antes, (await api.TotalLeads(), await api.Leer(db => db.EventosLead.CountAsync()),
            await api.Leer(db => db.ConsentimientosDatos.CountAsync()),
            await api.Leer(db => db.AuditoriaEventos.CountAsync(a => a.Entidad == "Lead"))));
        Assert.Equal(0, await api.LeadsDeTelefono(t));
    }
}
