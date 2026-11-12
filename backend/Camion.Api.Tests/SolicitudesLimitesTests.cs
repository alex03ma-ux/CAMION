using System.Net;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;
using static Camion.Api.Tests.Infraestructura.AyudasP5b;
using static Camion.Api.Tests.Infraestructura.AyudasP6;

namespace Camion.Api.Tests;

/// <summary>
/// P6 — anti-abuso compartido con P5 y P5b (CA-P6-30) y transacción de crédito y búsqueda (CA-P6-27). Cada prueba levanta su
/// propia API con límites bajos. SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class SolicitudesLimitesTests
{
    private static Task<InventarioApi> Api(int corta, int diario, int telefono) =>
        InventarioApi.Crear(NuevoReloj(), b =>
        {
            b.UseSetting("Contactos:LimiteVentanaCorta", corta.ToString());
            b.UseSetting("Contactos:LimiteDiario", diario.ToString());
            b.UseSetting("Contactos:LimiteTelefonoDia", telefono.ToString());
        });

    [Fact(DisplayName = "CA-P6-30: el cupo por IP se comparte entre contacto, avisos, crédito y búsqueda; al superarlo -> 429 en todas sin guardar")]
    public async Task Cupo_por_ip_compartido()
    {
        using var api = await Api(corta: 3, diario: 1000, telefono: 1000);
        var (_, _, slug) = await api.AsesorPublicado("Asesor Cupo P6");
        await api.ContactoOk(CuerpoContacto(slug));
        await api.SolicitudOk(RutaCredito, CuerpoCredito());
        await api.SolicitudOk(RutaBusqueda, CuerpoBusqueda(api.Camion));
        var leads = await api.TotalLeads();
        await Error(await api.Solicitar(RutaCredito, CuerpoCredito()), HttpStatusCode.TooManyRequests, MsgMuchas);
        await Error(await api.Solicitar(RutaBusqueda, CuerpoBusqueda(api.Camion)), HttpStatusCode.TooManyRequests, MsgMuchas);
        await Error(await api.Aviso(RutaVende, CuerpoVende()), HttpStatusCode.TooManyRequests, MsgMuchas);
        Assert.Equal(leads, await api.TotalLeads());
    }

    [Fact(DisplayName = "CA-P6-30: el límite por teléfono suma avisos, créditos, búsquedas y repetidos; al superarlo -> 429 sin lead, criterios, evento ni consentimiento")]
    public async Task Por_telefono_compartido()
    {
        using var api = await Api(corta: 1000, diario: 1000, telefono: 3);
        var t = TelefonoUnico();
        api.Hora(T);
        await api.AvisoOk(RutaVende, CuerpoVende(t));                      // 1
        await api.SolicitudOk(RutaCredito, CuerpoCredito(telefono: t));    // 2
        await api.SolicitudOk(RutaCredito, CuerpoCredito(telefono: t));    // 3: repetido
        var antes = (await api.TotalLeads(), await api.Leer(db => db.EventosLead.CountAsync()),
            await api.Leer(db => db.ConsentimientosDatos.CountAsync()), await api.Leer(db => db.LeadsBusqueda.CountAsync()));
        await Error(await api.Solicitar(RutaBusqueda, CuerpoBusqueda(api.Camion, t)), HttpStatusCode.TooManyRequests, MsgMuchas);
        await Error(await api.Solicitar(RutaCredito, CuerpoCredito(telefono: t)), HttpStatusCode.TooManyRequests, MsgMuchas);
        Assert.Equal(antes, (await api.TotalLeads(), await api.Leer(db => db.EventosLead.CountAsync()),
            await api.Leer(db => db.ConsentimientosDatos.CountAsync()), await api.Leer(db => db.LeadsBusqueda.CountAsync())));
        await api.SolicitudOk(RutaBusqueda, CuerpoBusqueda(api.Camion));   // otro teléfono sí
        api.Hora(T.AddDays(1).AddMinutes(1));
        await api.SolicitudOk(RutaBusqueda, CuerpoBusqueda(api.Camion, t));
    }

    private const string VersionQueFalla = "FALLA-PRUEBA-P6";

    [Theory(DisplayName = "CA-P6-27: si falla la escritura del consentimiento no queda el lead, ni los criterios, ni el historial, ni la auditoría; 500 sin detalles")]
    [InlineData(RutaCredito)]
    [InlineData(RutaBusqueda)]
    public async Task Falla_al_guardar_no_deja_nada(string ruta)
    {
        using var api = await InventarioApi.Crear(NuevoReloj(), b => b.UseSetting("PoliticaDatos:Version", VersionQueFalla));
        await api.Db(db => db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER falla_consentimiento_p6 BEFORE INSERT ON consentimientos_datos " +
            $"WHEN NEW.version_politica = '{VersionQueFalla}' BEGIN SELECT RAISE(ABORT, 'falla de prueba'); END;"));
        var antes = (await api.TotalLeads(), await api.Leer(db => db.EventosLead.CountAsync()),
            await api.Leer(db => db.LeadsBusqueda.CountAsync()),
            await api.Leer(db => db.AuditoriaEventos.CountAsync(a => a.Entidad == "Lead")));

        var t = TelefonoUnico();
        var r = await api.Solicitar(ruta, ruta == RutaCredito ? CuerpoCredito(telefono: t) : CuerpoBusqueda(api.Camion, t));
        await Error(r, HttpStatusCode.InternalServerError, "No se pudo completar la operación. Intenta de nuevo en unos minutos.");
        Assert.Equal(antes, (await api.TotalLeads(), await api.Leer(db => db.EventosLead.CountAsync()),
            await api.Leer(db => db.LeadsBusqueda.CountAsync()),
            await api.Leer(db => db.AuditoriaEventos.CountAsync(a => a.Entidad == "Lead"))));
        Assert.Equal(0, await api.LeadsDeTelefono(t));
    }
}
