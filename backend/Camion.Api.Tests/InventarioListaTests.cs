using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.InventarioApi;
using static Camion.Api.Tests.Infraestructura.InventarioJson;

namespace Camion.Api.Tests;

/// <summary>
/// P4a — GET /api/inventario/vehiculos (HU-021 inventario propio, HU-022 inventario general): filtros, búsqueda,
/// paginación, orden por actualizadoEn descendente y forma de ItemInventario. Base propia para controlar los conteos.
/// </summary>
public sealed class InventarioListaTests : IAsyncLifetime
{
    private InventarioApi _api = null!;
    private long _camionA, _carroA, _maquinaA, _carroceriaA, _camionB;
    private string _placaCamionA = null!;

    public async Task InitializeAsync()
    {
        _api = await InventarioApi.Crear();
        _placaCamionA = PlacaUnica();
        _camionA = await _api.CrearId(_api.A, "camion", ("placa", _placaCamionA), ("descripcionTecnica", "Furgón refrigerado de prueba"));
        _carroA = await _api.CrearId(_api.A, "carro", ("marcaId", _api.Kenworth), ("lineaId", _api.T800));
        _maquinaA = await _api.CrearId(_api.A, "maquinaria", ("horasUso", 3400));
        _carroceriaA = await _api.CrearId(_api.A, "carroceria");
        _camionB = await _api.CrearId(_api.B, "camion");
    }

    public Task DisposeAsync()
    {
        _api.Dispose();
        return Task.CompletedTask;
    }

    private async Task<long[]> Ids(string? token, string query = "")
    {
        var json = await Json(await _api.Lista(token, query), HttpStatusCode.OK);
        return json.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray();
    }

    private async Task Afirmar400(string query, string campo, string mensaje)
    {
        var r = await _api.Lista(_api.Admin.Token, query);
        Assert.Equal(new[] { mensaje }, await Errores(r, HttpStatusCode.BadRequest, campo, MsgFiltros));
    }

    [Fact(DisplayName = "HU-021: el asesor ve solo sus vehículos; HU-022: el administrador ve todos (total, pagina, tamano por defecto 20)")]
    public async Task Alcance_por_rol()
    {
        var a = await Json(await _api.Lista(_api.A.Token), HttpStatusCode.OK);
        Assert.Equal(4, a.GetProperty("total").GetInt32());
        Assert.Equal(1, a.GetProperty("pagina").GetInt32());
        Assert.Equal(20, a.GetProperty("tamano").GetInt32());
        Assert.Equal(new[] { _camionA, _carroA, _maquinaA, _carroceriaA }.OrderBy(x => x),
            a.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).OrderBy(x => x));

        Assert.Equal(new[] { _camionB }, await Ids(_api.B.Token));
        var admin = await Json(await _api.Lista(_api.Admin.Token), HttpStatusCode.OK);
        Assert.Equal(5, admin.GetProperty("total").GetInt32());
    }

    [Fact(DisplayName = "HU-021/HU-022: forma de ItemInventario; 'asesor' solo para el administrador; kilometraje null en carrocería y maquinaria")]
    public async Task Forma_del_item()
    {
        // D-30: el camión nace con una foto (la portada) y se le suben dos más.
        var foto = (await Json(await _api.Detalle(_api.A.Token, _camionA), HttpStatusCode.OK)).GetProperty("fotos")[0];
        await _api.FotoSubida(_api.A.Token, _camionA);
        await _api.FotoSubida(_api.A.Token, _camionA, Png());
        // El carro se deja sin fotos, como un vehículo anterior a D-30 (DEMO): portada null y 0 fotos.
        await _api.QuitarFotosDb(_carroA);

        var a = await Json(await _api.Lista(_api.A.Token), HttpStatusCode.OK);
        var items = a.GetProperty("items").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetInt64());
        var camion = items[_camionA];
        Assert.Equal(new[] { "actualizadoEn", "estado", "fotoPortada", "horasUso", "id", "kilometraje", "linea", "marca",
            "modeloAnio", "municipio", "notaReserva", "placa", "precioCop", "tipo", "totalFotos" }, CatalogoJson.Claves(camion));
        Assert.Equal("Camión", camion.GetProperty("tipo").GetString());
        Assert.Equal("Chevrolet", camion.GetProperty("marca").GetString());
        Assert.Equal("NPR", camion.GetProperty("linea").GetString());
        Assert.Equal(2019, camion.GetProperty("modeloAnio").GetInt32());
        Assert.Equal(_placaCamionA, camion.GetProperty("placa").GetString());
        Assert.Equal(100_000_000, camion.GetProperty("precioCop").GetInt64());
        Assert.Equal("ACTIVO", camion.GetProperty("estado").GetString());
        Assert.Equal(120_000, camion.GetProperty("kilometraje").GetInt32());
        Assert.Equal("Medellín", camion.GetProperty("municipio").GetString());
        Assert.Equal(foto.GetProperty("url").GetString(), camion.GetProperty("fotoPortada").GetString());
        Assert.Equal(3, camion.GetProperty("totalFotos").GetInt32());

        Assert.True(EsNulo(items[_maquinaA], "kilometraje"));
        Assert.Equal(3400, items[_maquinaA].GetProperty("horasUso").GetInt32());
        Assert.True(EsNulo(items[_carroceriaA], "kilometraje"));
        Assert.True(EsNulo(items[_carroceriaA], "marca"));
        Assert.True(EsNulo(items[_carroA], "fotoPortada"));
        Assert.Equal(0, items[_carroA].GetProperty("totalFotos").GetInt32());

        var admin = await Json(await _api.Lista(_api.Admin.Token, "?tamano=100"), HttpStatusCode.OK);
        foreach (var item in admin.GetProperty("items").EnumerateArray())
        {
            var id = item.GetProperty("id").GetInt64();
            var asesor = item.GetProperty("asesor");
            Assert.Equal(id == _camionB ? _api.B.PerfilId : _api.A.PerfilId, asesor.GetProperty("id").GetInt64());
            Assert.Equal(id == _camionB ? _api.B.Nombre : _api.A.Nombre, asesor.GetProperty("nombre").GetString());
        }
    }

    [Fact(DisplayName = "HU-021: orden por actualizadoEn descendente; editar un vehículo lo sube al primer lugar")]
    public async Task Orden_actualizado_desc()
    {
        var ids = await Ids(_api.A.Token);
        Assert.Equal(new[] { _carroceriaA, _maquinaA, _carroA, _camionA }, ids);
        await Task.Delay(20);
        Assert.Equal(HttpStatusCode.OK, (await _api.Editar(_api.A.Token, _camionA,
            Con(_api.CuerpoCamion(), ("placa", _placaCamionA), ("descripcionTecnica", "Otra descripción")))).StatusCode);
        Assert.Equal(_camionA, (await Ids(_api.A.Token))[0]);

        var fechas = (await Json(await _api.Lista(_api.Admin.Token), HttpStatusCode.OK)).GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("actualizadoEn").GetDateTime()).ToArray();
        Assert.Equal(fechas.OrderByDescending(x => x), fechas);
    }

    [Fact(DisplayName = "HU-021: filtros por estado (sin distinguir mayúsculas), tipoId y marcaId")]
    public async Task Filtros()
    {
        await Json(await _api.Accion(_api.A.Token, _carroA, "reservar"), HttpStatusCode.OK);
        await Json(await _api.Accion(_api.A.Token, _maquinaA, "inactivar", "Motivo de prueba"), HttpStatusCode.OK);

        Assert.Equal(new[] { _carroA }, await Ids(_api.A.Token, "?estado=RESERVADO"));
        Assert.Equal(new[] { _carroA }, await Ids(_api.A.Token, "?estado=reservado"));
        Assert.Equal(new[] { _maquinaA }, await Ids(_api.A.Token, "?estado=INACTIVO"));
        Assert.Empty(await Ids(_api.A.Token, "?estado=VENDIDO"));
        Assert.Equal(new[] { _camionA, _carroceriaA }.OrderBy(x => x), (await Ids(_api.A.Token, "?estado=ACTIVO")).OrderBy(x => x));

        Assert.Equal(new[] { _camionA }, await Ids(_api.A.Token, $"?tipoId={_api.Camion}"));
        Assert.Equal(new[] { _camionA, _camionB }.OrderBy(x => x), (await Ids(_api.Admin.Token, $"?tipoId={_api.Camion}")).OrderBy(x => x));
        Assert.Equal(new[] { _carroA, _maquinaA }.OrderBy(x => x), (await Ids(_api.A.Token, $"?marcaId={_api.Kenworth}")).OrderBy(x => x));
        Assert.Equal(new[] { _carroA }, await Ids(_api.A.Token, $"?marcaId={_api.Kenworth}&estado=RESERVADO"));
        Assert.Empty(await Ids(_api.A.Token, "?tipoId=987654"));
    }

    [Fact(DisplayName = "HU-021: q busca en placa (normalizada), marca, línea y descripción; el asesor no encuentra lo ajeno")]
    public async Task Busqueda()
    {
        Assert.Equal(new[] { _camionA }, await Ids(_api.A.Token, "?q=" + Uri.EscapeDataString(_placaCamionA)));
        var conGuion = $"{_placaCamionA[..3].ToLowerInvariant()}-{_placaCamionA[3..]}";
        Assert.Equal(new[] { _camionA }, await Ids(_api.A.Token, "?q=" + Uri.EscapeDataString(conGuion)));
        Assert.Equal(new[] { _camionA }, await Ids(_api.A.Token, "?q=refrigerado"));
        Assert.Equal(new[] { _camionA }, await Ids(_api.A.Token, "?q=npr"));
        Assert.Equal(new[] { _carroA, _maquinaA }.OrderBy(x => x), (await Ids(_api.A.Token, "?q=KENWORTH")).OrderBy(x => x));
        Assert.Empty(await Ids(_api.B.Token, "?q=" + Uri.EscapeDataString(_placaCamionA)));
        Assert.Empty(await Ids(_api.A.Token, "?q=nada-coincide-xyz"));
        Assert.Equal(4, (await Ids(_api.A.Token, "?q=%20%20")).Length); // solo espacios = sin búsqueda
        Assert.Equal(new[] { _camionA }, await Ids(_api.A.Token, "?q=" + Uri.EscapeDataString(_placaCamionA[..4]))); // placa parcial
    }

    [Fact(DisplayName = "HU-022: asesorId filtra para el administrador; el asesor no puede usarlo para ver lo ajeno")]
    public async Task Filtro_asesor()
    {
        Assert.Equal(new[] { _camionB }, await Ids(_api.Admin.Token, $"?asesorId={_api.B.PerfilId}"));
        Assert.Equal(4, (await Ids(_api.Admin.Token, $"?asesorId={_api.A.PerfilId}")).Length);
        Assert.Empty(await Ids(_api.Admin.Token, "?asesorId=987654"));
        // Para el asesor el parámetro se ignora: sigue viendo solo lo suyo.
        Assert.Equal(new[] { _camionB }, await Ids(_api.B.Token, $"?asesorId={_api.A.PerfilId}"));
    }

    [Fact(DisplayName = "HU-021: paginación (tamano, pagina, página fuera de rango vacía con el total)")]
    public async Task Paginacion()
    {
        var p1 = await Json(await _api.Lista(_api.A.Token, "?tamano=3&pagina=1"), HttpStatusCode.OK);
        var p2 = await Json(await _api.Lista(_api.A.Token, "?tamano=3&pagina=2"), HttpStatusCode.OK);
        Assert.Equal(3, p1.GetProperty("items").GetArrayLength());
        Assert.Equal(1, p2.GetProperty("items").GetArrayLength());
        Assert.Equal(4, p2.GetProperty("total").GetInt32());
        Assert.Equal(2, p2.GetProperty("pagina").GetInt32());
        Assert.Equal(3, p2.GetProperty("tamano").GetInt32());
        var todos = p1.GetProperty("items").EnumerateArray().Concat(p2.GetProperty("items").EnumerateArray())
            .Select(x => x.GetProperty("id").GetInt64()).ToArray();
        Assert.Equal(4, todos.Distinct().Count());

        var fuera = await Json(await _api.Lista(_api.A.Token, "?pagina=99"), HttpStatusCode.OK);
        Assert.Empty(fuera.GetProperty("items").EnumerateArray());
        Assert.Equal(4, fuera.GetProperty("total").GetInt32());
        Assert.Equal(100, (await Json(await _api.Lista(_api.A.Token, "?tamano=100"), HttpStatusCode.OK)).GetProperty("tamano").GetInt32());
    }

    [Theory(DisplayName = "HU-021: parámetros inválidos -> 400 \"Revisa los filtros.\" con el campo, nunca 500")]
    [InlineData("?estado=VENDIDA", "estado", "Estado no válido.")]
    [InlineData("?estado=xyz", "estado", "Estado no válido.")]
    [InlineData("?tipoId=abc", "tipoId", "Categoría no válida.")]
    [InlineData("?tipoId=0", "tipoId", "Categoría no válida.")]
    [InlineData("?tipoId=-1", "tipoId", "Categoría no válida.")]
    [InlineData("?marcaId=1.5", "marcaId", "Marca no válida.")]
    [InlineData("?asesorId=x", "asesorId", "Asesor no válido.")]
    [InlineData("?pagina=0", "pagina", "Página o tamaño no válidos.")]
    [InlineData("?pagina=abc", "pagina", "Página o tamaño no válidos.")]
    [InlineData("?tamano=0", "tamano", "Página o tamaño no válidos.")]
    [InlineData("?tamano=101", "tamano", "Página o tamaño no válidos.")]
    [InlineData("?pagina=99999999999", "pagina", "Página o tamaño no válidos.")]
    [InlineData("?q=LARGA", "q", "La búsqueda no puede tener más de 100 caracteres.")]
    public async Task Parametros_invalidos(string query, string campo, string mensaje)
    {
        if (query == "?q=LARGA") query = "?q=" + new string('q', 101);
        await Afirmar400(query, campo, mensaje);
    }

    [Fact(DisplayName = "HU-021: q de exactamente 100 caracteres es válida")]
    public async Task Busqueda_100() => Assert.Empty(await Ids(_api.A.Token, "?q=" + new string('q', 100)));
}
