using System.Net;
using System.Text.Json;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.InventarioApi;
using static Camion.Api.Tests.Infraestructura.InventarioJson;

namespace Camion.Api.Tests;

/// <summary>
/// P4a — D-32: cada modelo (línea) pertenece a una o varias categorías (tabla lineas_tipos). La API solo acepta
/// un modelo clasificado si tiene la categoría del vehículo; uno sin clasificar se acepta en todas.
/// Base propia (no comparte la clasificación con otras clases de prueba).
/// </summary>
public sealed class InventarioD32Tests : IAsyncLifetime
{
    private InventarioApi _api = null!;

    public async Task InitializeAsync()
    {
        _api = await InventarioApi.Crear();
        // NPR (Chevrolet) solo Camión; T800 (Kenworth) Camión y Maquinaria. Las demás quedan sin clasificar.
        await _api.Leer(async db =>
        {
            db.LineasTipos.AddRange(
                new LineaTipo { LineaId = _api.Npr, TipoVehiculoId = _api.Camion },
                new LineaTipo { LineaId = _api.T800, TipoVehiculoId = _api.Camion },
                new LineaTipo { LineaId = _api.T800, TipoVehiculoId = _api.Maquinaria });
            return await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "D-32: un modelo de camión no se acepta en un carro -> 400 en lineaId")]
    public async Task Modelo_de_otra_categoria_400()
    {
        var r = await _api.Crear(_api.A.Token, _api.CuerpoCarro()); // carro con NPR (solo Camión)
        await Afirmar400(r, "lineaId", "Este modelo no corresponde a la categoría elegida.");
    }

    [Fact(DisplayName = "D-32: el mismo modelo en su categoría se acepta")]
    public async Task Modelo_de_su_categoria_201() =>
        await _api.Creado(_api.A.Token, _api.CuerpoCamion());

    [Theory(DisplayName = "D-32: un modelo con varias categorías se acepta en cada una de ellas")]
    [InlineData("camion")]
    [InlineData("maquinaria")]
    public async Task Modelo_multicategoria(string categoria) =>
        await _api.Creado(_api.A.Token, Con(_api.Cuerpo(categoria), ("marcaId", _api.Kenworth), ("lineaId", _api.T800)));

    [Fact(DisplayName = "D-32: al editar tampoco se acepta un modelo de otra categoría")]
    public async Task Editar_con_modelo_de_otra_categoria_400()
    {
        var id = await _api.CrearId(_api.A, "maquinaria");
        var r = await _api.Enviar(HttpMethod.Put, $"/api/inventario/vehiculos/{id}", _api.A.Token,
            Sin(Con(_api.Cuerpo("maquinaria"), ("marcaId", _api.Chevrolet), ("lineaId", _api.Npr)), "asesorId"));
        await Afirmar400(r, "lineaId", "Este modelo no corresponde a la categoría elegida.");
    }

    [Fact(DisplayName = "D-32: los catálogos del formulario traen las categorías de cada modelo")]
    public async Task Catalogos_traen_tipoIds()
    {
        var r = await _api.Enviar(HttpMethod.Get, "/api/inventario/catalogos", _api.A.Token);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        var lineas = doc.RootElement.GetProperty("marcas").EnumerateArray()
            .SelectMany(m => m.GetProperty("lineas").EnumerateArray())
            .ToDictionary(l => l.GetProperty("id").GetInt64(), l => l.GetProperty("tipoIds").EnumerateArray().Select(x => x.GetInt64()).ToArray());
        Assert.Equal(new[] { _api.Camion }, lineas[_api.Npr]);
        Assert.Equal(new[] { _api.Camion, _api.Maquinaria }.Order().ToArray(), lineas[_api.T800]);
    }
}
