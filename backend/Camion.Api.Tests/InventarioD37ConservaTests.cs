using System.Net;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.InventarioApi;
using static Camion.Api.Tests.Infraestructura.InventarioJson;

namespace Camion.Api.Tests;

/// <summary>
/// D-37: desactivar una marca o un modelo no impide editar el vehículo que ya lo usa (lo conserva);
/// lo que sí se rechaza es elegir un elemento desactivado que el vehículo no tenía.
/// </summary>
public sealed class InventarioD37ConservaTests : IAsyncLifetime
{
    private InventarioApi _api = null!;

    public async Task InitializeAsync() => _api = await InventarioApi.Crear();

    public Task DisposeAsync() => Task.CompletedTask;

    private Task Desactivar(long marcaId, long lineaId) => _api.Leer(async db =>
    {
        await db.Marcas.Where(m => m.Id == marcaId).ExecuteUpdateAsync(s => s.SetProperty(m => m.Activo, false));
        return await db.Lineas.Where(l => l.Id == lineaId).ExecuteUpdateAsync(s => s.SetProperty(l => l.Activo, false));
    });

    [Fact(DisplayName = "D-37: con su marca y modelo desactivados, el vehículo se edita sin cambiarlos (200)")]
    public async Task Conserva_marca_y_modelo_desactivados()
    {
        var id = await _api.CrearId(_api.A, "camion"); // Chevrolet NPR
        await Desactivar(_api.Chevrolet, _api.Npr);
        var r = await _api.Editar(_api.A.Token, id, Sin(Con(_api.CuerpoCamion(), ("kilometraje", 123)), "asesorId"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(123, (await _api.VehiculoDb(id)).Kilometraje);
    }

    [Fact(DisplayName = "D-37: elegir una marca desactivada que el vehículo no tenía sigue dando 400")]
    public async Task Elegir_marca_desactivada_nueva_400()
    {
        var id = await _api.CrearId(_api.A, "maquinaria"); // Kenworth T800
        await Desactivar(_api.Chevrolet, _api.Npr);
        var r = await _api.Editar(_api.A.Token, id,
            Sin(Con(_api.CuerpoCamion(), ("marcaId", _api.Chevrolet), ("lineaId", _api.Npr)), "asesorId"));
        await Afirmar400(r, "marcaId", "Marca no válida.");
    }

    [Fact(DisplayName = "D-37: al crear no se acepta una marca desactivada")]
    public async Task Crear_con_marca_desactivada_400()
    {
        await Desactivar(_api.Chevrolet, _api.Npr);
        await Afirmar400(await _api.Crear(_api.A.Token, _api.CuerpoCamion()), "marcaId", "Marca no válida.");
    }
}
