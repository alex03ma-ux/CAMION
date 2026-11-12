using System.Net;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;

namespace Camion.Api.Tests;

/// <summary>
/// P5 — CA-P5-15: el lead, su historial y la prueba de autorización se guardan en una sola transacción. Un disparador
/// de SQLite (solo en esta base de prueba en memoria; nunca MariaDB) hace fallar la inserción del consentimiento.
/// </summary>
public sealed class ContactosTransaccionTests
{
    private const string VersionQueFalla = "FALLA-PRUEBA";

    [Fact(DisplayName = "CA-P5-15: si falla la escritura del consentimiento no queda ni el lead, ni su historial, ni la auditoría; 500 sin detalles")]
    public async Task Falla_al_guardar_no_deja_nada()
    {
        using var api = await InventarioApi.Crear(NuevoReloj(), b => b.UseSetting("PoliticaDatos:Version", VersionQueFalla));
        var (_, _, slug) = await api.AsesorPublicado("Asesor Transaccion");
        await api.Db(db => db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER falla_consentimiento BEFORE INSERT ON consentimientos_datos " +
            $"WHEN NEW.version_politica = '{VersionQueFalla}' BEGIN SELECT RAISE(ABORT, 'falla de prueba'); END;"));
        var antes = (await api.TotalLeads(), await api.Leer(db => db.EventosLead.CountAsync()),
            await api.Leer(db => db.ConsentimientosDatos.CountAsync()),
            await api.Leer(db => db.AuditoriaEventos.CountAsync(a => a.Entidad == "Lead")));

        var telefono = TelefonoUnico();
        var r = await api.Contactar(CuerpoContacto(slug, telefono));
        await Error(r, HttpStatusCode.InternalServerError, "No se pudo completar la operación. Intenta de nuevo en unos minutos.");

        Assert.Equal(antes, (await api.TotalLeads(), await api.Leer(db => db.EventosLead.CountAsync()),
            await api.Leer(db => db.ConsentimientosDatos.CountAsync()),
            await api.Leer(db => db.AuditoriaEventos.CountAsync(a => a.Entidad == "Lead"))));
        Assert.False(await api.Leer(db => db.Leads.AnyAsync(l => l.Telefono == "+57" + telefono)));
    }
}
