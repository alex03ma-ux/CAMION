using Camion.Api.Auth;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;
using static Camion.Api.Tests.Infraestructura.AyudasP5b;
using static Camion.Api.Tests.Infraestructura.AyudasP6;

namespace Camion.Api.Tests;

/// <summary>
/// P9 (RN-SE-004, D-44; CA-P9-62 a CA-P9-65): versión de la política de datos leída con un único ayudante en las seis rutas
/// que guardan consentimientos, aviso en el log si es un borrador fuera de Development y formato validado al arrancar.
/// SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class PoliticaDatosTests
{
    [Fact(DisplayName = "CA-P9-63: con PoliticaDatos:Version = PRUEBA-1, contacto, avisos, crédito, búsqueda, registro de comprador e inscripción de asesor guardan PRUEBA-1")]
    public async Task Seis_rutas_misma_version()
    {
        using var api = await InventarioApi.Crear(NuevoReloj(), b => b.UseSetting(PoliticaDatos.Clave, "PRUEBA-1"));
        // Crear la API ya registra asesores y un comprador.
        var (_, _, slug) = await api.AsesorPublicado("Asesor Version Politica");
        await api.ContactoOk(CuerpoContacto(slug));
        await api.AvisoOk(RutaVende, CuerpoVende());
        await api.AvisoOk(RutaSeguro, CuerpoSeguro());
        await api.SolicitudOk(RutaCredito, CuerpoCredito());
        await api.SolicitudOk(RutaBusqueda, CuerpoBusqueda(api.Camion));

        var filas = await api.Leer(db => db.ConsentimientosDatos.AsNoTracking().ToListAsync());
        Assert.All(filas, c => Assert.Equal("PRUEBA-1", c.VersionPolitica));
        Assert.Equal(new[]
            {
                ConsentimientoDatos.Busqueda, ConsentimientoDatos.ContactoVehiculo, ConsentimientoDatos.Credito,
                ConsentimientoDatos.InscripcionAsesor, ConsentimientoDatos.RegistroComprador,
                ConsentimientoDatos.SeguroTodoRiesgo, ConsentimientoDatos.VendeTuCamion
            },
            filas.Select(c => c.Formulario).Distinct().Order(StringComparer.Ordinal).ToArray());
    }

    [Fact(DisplayName = "CA-P9-62, 64: sin configuración propia se usa BORRADOR-2026-11-04 y, fuera de Development, se avisa en el log al arrancar")]
    public async Task Borrador_avisa_en_el_log()
    {
        using var f = new CamionApiFactory();
        using var c = f.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.OK, (await c.GetAsync("/health")).StatusCode);
        Assert.Contains(f.Logs, l => l.Contains(PoliticaDatos.AvisoBorrador, StringComparison.Ordinal) && l.Contains("|Warning|"));
        Assert.Equal("BORRADOR-2026-11-04", PoliticaDatos.VersionPorDefecto);
    }

    [Fact(DisplayName = "CA-P9-64: una versión aprobada (V1-2027-01-15) no produce el aviso de borrador")]
    public async Task Version_aprobada_sin_aviso()
    {
        using var f = new CamionApiFactory();
        using var w = f.WithWebHostBuilder(b => b.UseSetting(PoliticaDatos.Clave, "V1-2027-01-15"));
        using var c = w.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.OK, (await c.GetAsync("/health")).StatusCode);
        Assert.DoesNotContain(f.Logs, l => l.Contains(PoliticaDatos.AvisoBorrador, StringComparison.Ordinal));
    }

    [Theory(DisplayName = "CA-P9-65: versión con minúsculas, espacios, otros signos o más de 40 caracteres -> la API no arranca")]
    [InlineData("borrador-2026")]
    [InlineData("V1 2027")]
    [InlineData("V1_2027")]
    [InlineData("V1/2027")]
    [InlineData("LARGA41")]
    public void Version_invalida_no_arranca(string version)
    {
        if (version == "LARGA41") version = new string('A', 41);
        using var f = new CamionApiFactory();
        using var w = f.WithWebHostBuilder(b => b.UseSetting(PoliticaDatos.Clave, version));
        var ex = Assert.ThrowsAny<Exception>(() => w.CreateClient());
        var mensajes = string.Join(" | ", Cadena(ex).Select(e => e.Message));
        Assert.Contains(PoliticaDatos.Clave, mensajes);
        Assert.DoesNotContain(version, mensajes);
    }

    [Theory(DisplayName = "CA-P9-65 (UNIT): formato de la versión")]
    [InlineData("BORRADOR-2026-11-04", true)]
    [InlineData("V1-2027-01-15", true)]
    [InlineData("V1.2", true)]
    [InlineData("v1-2027", false)]
    [InlineData("", false)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", true)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", false)]
    public void Formato(string version, bool valida) => Assert.Equal(valida, PoliticaDatos.EsValida(version));

    private static IEnumerable<Exception> Cadena(Exception? e)
    {
        for (; e is not null; e = e.InnerException) yield return e;
    }
}
