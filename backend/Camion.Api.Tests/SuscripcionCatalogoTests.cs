using System.Net;
using System.Net.Http.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;

namespace Camion.Api.Tests;

/// <summary>
/// P4p en el catálogo público (condición 6 de P3): solo cuentan las suscripciones ACTIVA o REACTIVADA. Suspender oculta
/// las publicaciones del asesor y reactivar las vuelve a mostrar. Un elemento de catálogo desactivado deja de estar en
/// los filtros, pero el vehículo publicado lo conserva y lo sigue mostrando. Datos de referencia de P3 (reloj fijo en T).
/// </summary>
public sealed class SuscripcionCatalogoTests : IAsyncLifetime
{
    private CatalogoApi _api = null!;
    private string _admin = null!;

    public async Task InitializeAsync()
    {
        _api = await CatalogoApi.Crear(noVisibles: false);
        _admin = (await AyudasP2.CrearAdministrador(_api.Base, _api.Cliente)).Token;
    }

    public Task DisposeAsync()
    {
        _api.Dispose();
        return Task.CompletedTask;
    }

    private Task<HttpResponseMessage> Post(string url, object cuerpo)
    {
        var req = Ayudas.ConToken(HttpMethod.Post, url, _admin);
        req.Content = JsonContent.Create(cuerpo);
        return _api.Cliente.SendAsync(req);
    }

    [Fact(DisplayName = "HU-068/HU-069: suspender la suscripción oculta las publicaciones del asesor; reactivar (REACTIVADA) las muestra")]
    public async Task Suspender_oculta_y_reactivar_muestra()
    {
        const string slug = "chevrolet-npr-2020";
        var plan = _api.Datos.A1.Plan.Id;
        Assert.Contains(slug, await _api.SlugsBusqueda("?tamano=48"));

        await Esperar(await Post($"/api/admin/suscripciones/{plan}/suspender", new { motivo = MotivoValido }), HttpStatusCode.OK);
        Assert.DoesNotContain(slug, await _api.SlugsBusqueda("?tamano=48"));
        Assert.Equal(HttpStatusCode.NotFound, (await _api.Detalle(slug)).StatusCode);
        var inicio = await _api.Inicio();
        Assert.DoesNotContain(slug, inicio.ToString());

        var json = await Esperar(await Post($"/api/admin/suscripciones/{plan}/reactivar", new { }), HttpStatusCode.OK);
        Assert.Equal("REACTIVADA", json.GetProperty("estado").GetString());
        Assert.Contains(slug, await _api.SlugsBusqueda("?tamano=48"));
        Assert.Equal(HttpStatusCode.OK, (await _api.Detalle(slug)).StatusCode);
    }

    [Fact(DisplayName = "D-37: marca desactivada -> fuera de los filtros públicos; la publicación sigue visible con su marca")]
    public async Task Marca_desactivada_en_el_catalogo()
    {
        const string slug = "chevrolet-npr-2020";
        var chevrolet = _api.Datos.Chevrolet.Id;
        Assert.Contains((await _api.Filtros()).GetProperty("marcas").EnumerateArray(), m => m.GetProperty("id").GetInt64() == chevrolet);

        await Esperar(await Post($"/api/admin/catalogos/marcas/{chevrolet}/desactivar", new { }), HttpStatusCode.OK);
        Assert.DoesNotContain((await _api.Filtros()).GetProperty("marcas").EnumerateArray(),
            m => m.GetProperty("id").GetInt64() == chevrolet);
        Assert.Contains(slug, await _api.SlugsBusqueda("?tamano=48"));
        var detalle = await Esperar(await _api.Detalle(slug), HttpStatusCode.OK);
        Assert.Equal("Chevrolet", detalle.GetProperty("marca").GetString());
    }
}
