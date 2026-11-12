using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP7;

namespace Camion.Api.Tests;

/// <summary>
/// P7 — historial de ventas (minuta §6, M11, HU-064, D-62 A): GET /api/ventas y GET /api/inventario/vehiculos/{id}/ventas.
/// CA-P7-01 a CA-P7-14 y CA-P7-21. SQLite en memoria y reloj fijo en T; nunca MariaDB.
/// </summary>
public sealed class VentasHistorialTests(P7Fixture fixture) : IClassFixture<P7Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    public static TheoryData<string> Rutas => new() { "/api/ventas", "/api/inventario/vehiculos/1/ventas" };

    [Theory(DisplayName = "CA-P7-08: rutas de ventas -> 401 sin sesión y 403 comprador")]
    [MemberData(nameof(Rutas))]
    public async Task Permisos(string ruta)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Enviar(HttpMethod.Get, ruta, null)).StatusCode);
        await Error(await _api.Enviar(HttpMethod.Get, ruta, _api.TokenComprador), HttpStatusCode.Forbidden, MsgSinPermiso);
    }

    [Fact(DisplayName = "CA-P7-01, CA-P7-06, CA-P7-21: venta revertida y venta nueva; el administrador ve las dos con su estado y orden")]
    public async Task Admin_ve_vigente_y_revertida()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor("Asesora Historial Ficticia");
        var vehiculo = await _api.VehiculoVendido(asesor, precio: 90_000_000);
        _api.Hora(T.AddMinutes(10));
        await Esperar(await _api.RevertirVenta(asesor.Token, vehiculo, "El comprador desistió (prueba)"), HttpStatusCode.OK);
        _api.Hora(T.AddMinutes(20));
        await Esperar(await _api.Vender(_api.Admin.Token, vehiculo,
            new { fechaVenta = HoyT, precioFinalCop = 88_000_000, origen = "REFERIDO" }), HttpStatusCode.OK);
        _api.Hora(T);

        var lista = await _api.VentasOk(_api.Admin.Token, $"?asesorId={asesor.PerfilId}");
        Assert.Equal(2, lista.GetProperty("total").GetInt32());
        var items = lista.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new[] { "VIGENTE", "REVERTIDA" }, items.Select(i => i.GetProperty("estado").GetString()).ToArray());
        var vigente = items[0];
        Assert.Equal(88_000_000, vigente.GetProperty("precioFinalCop").GetInt64());
        Assert.Equal("REFERIDO", vigente.GetProperty("origen").GetString());
        Assert.Equal(vehiculo, vigente.GetProperty("vehiculo").GetProperty("id").GetInt64());
        Assert.Equal("VENDIDO", vigente.GetProperty("vehiculo").GetProperty("estado").GetString());
        Assert.Equal("Chevrolet NPR 2019", vigente.GetProperty("vehiculo").GetProperty("titulo").GetString());
        Assert.Equal(asesor.PerfilId, vigente.GetProperty("asesor").GetProperty("id").GetInt64());
        Assert.Equal(asesor.Nombre, vigente.GetProperty("asesor").GetProperty("nombre").GetString());
        Assert.Equal(JsonValueKind.Null, vigente.GetProperty("revertidaEn").ValueKind);
        var revertida = items[1];
        Assert.Equal("El comprador desistió (prueba)", revertida.GetProperty("motivoReversion").GetString());
        Assert.Equal(asesor.Nombre, revertida.GetProperty("registradaPor").GetString());
        Assert.Equal(asesor.Nombre, revertida.GetProperty("revertidaPor").GetString());
        Assert.Equal(T.AddMinutes(10), Fecha(revertida, "revertidaEn"));
        Assert.Equal(T, Fecha(revertida, "registradaEn"));

        // CA-P7-12: sin datos de compradores ni de leads.
        Assert.Equal(new[]
            {
                "asesor", "estado", "fechaVenta", "id", "motivoReversion", "observaciones", "origen", "precioFinalCop",
                "registradaEn", "registradaPor", "revertidaEn", "revertidaPor", "vehiculo"
            },
            vigente.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());

        // CA-P7-06: el historial del vehículo trae las dos, la vigente primero (dueño y administrador).
        foreach (var token in new[] { asesor.Token, _api.Admin.Token })
        {
            var hist = await Esperar(await _api.VentasVehiculo(token, vehiculo), HttpStatusCode.OK);
            Assert.Equal(Ids(lista), Ids(hist));
        }
        // El asesor no recibe el bloque asesor (como en el inventario).
        var propio = await Esperar(await _api.VentasVehiculo(asesor.Token, vehiculo), HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, propio.GetProperty("items")[0].GetProperty("asesor").ValueKind);
        // CA-P7-14: el total coincide con las filas de ventas_vehiculo.
        Assert.Equal((await _api.VentasDb(vehiculo)).Count, lista.GetProperty("total").GetInt32());
    }

    [Fact(DisplayName = "CA-P7-02, CA-P7-05: el asesor solo ve sus ventas; su asesorId se ignora aunque sea de otro o no válido")]
    public async Task Asesor_solo_lo_suyo()
    {
        _api.Hora(T);
        var uno = await _api.CrearAsesor("Asesor Uno Ventas Ficticio");
        var dos = await _api.CrearAsesor("Asesor Dos Ventas Ficticio");
        var deUno = await _api.VehiculoVendido(uno);
        var deDos = await _api.VehiculoVendido(dos);

        foreach (var query in new[] { "", $"?asesorId={dos.PerfilId}", "?asesorId=abc" })
        {
            var lista = await _api.VentasOk(uno.Token, query);
            var vehiculos = lista.GetProperty("items").EnumerateArray()
                .Select(i => i.GetProperty("vehiculo").GetProperty("id").GetInt64()).ToArray();
            Assert.Equal(new[] { deUno }, vehiculos);
            Assert.Equal(1, lista.GetProperty("total").GetInt32());
        }
        // CA-P7-07: el historial de un vehículo ajeno o inexistente da 404 sin revelar que existe.
        await Error(await _api.VentasVehiculo(uno.Token, deDos), HttpStatusCode.NotFound, MsgVehiculoNoEncontrado);
        await Error(await _api.VentasVehiculo(uno.Token, 999_999), HttpStatusCode.NotFound, MsgVehiculoNoEncontrado);
        await Error(await _api.VentasVehiculo(_api.Admin.Token, 999_999), HttpStatusCode.NotFound, MsgVehiculoNoEncontrado);
    }

    [Fact(DisplayName = "D-62 c A: tras reasignar el vehículo (D-34), sus ventas pasan al nuevo dueño")]
    public async Task Reasignado_sigue_al_dueno()
    {
        _api.Hora(T);
        var origen = await _api.CrearAsesor("Asesora Origen Ventas Ficticia");
        var destino = await _api.CrearAsesor("Asesor Destino Ventas Ficticio");
        var vehiculo = await _api.VehiculoVendido(origen);
        await Esperar(await _api.Reasignar(_api.Admin.Token, vehiculo, destino.PerfilId), HttpStatusCode.OK);

        Assert.Empty(Ids(await _api.VentasOk(origen.Token)));
        Assert.Single(Ids(await _api.VentasOk(destino.Token)));
        await Error(await _api.VentasVehiculo(origen.Token, vehiculo), HttpStatusCode.NotFound, MsgVehiculoNoEncontrado);
        var admin = await _api.VentasOk(_api.Admin.Token, $"?asesorId={destino.PerfilId}");
        Assert.Equal(destino.PerfilId, admin.GetProperty("items")[0].GetProperty("asesor").GetProperty("id").GetInt64());
    }

    [Fact(DisplayName = "CA-P7-03: filtros desde, hasta, origen, estado y asesorId se combinan")]
    public async Task Filtros()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor("Asesora Filtros Ventas Ficticia");
        var portal = await _api.VehiculoVendido(asesor, "PORTAL");
        var otro = await _api.VehiculoVendido(asesor, "OTRO", observaciones: "Permuta por otro camión (prueba)");
        await Esperar(await _api.RevertirVenta(asesor.Token, otro), HttpStatusCode.OK);
        var a = $"?asesorId={asesor.PerfilId}";

        async Task<long[]> Vehiculos(string q) => (await _api.VentasOk(_api.Admin.Token, a + q)).GetProperty("items")
            .EnumerateArray().Select(i => i.GetProperty("vehiculo").GetProperty("id").GetInt64()).Order().ToArray();

        Assert.Equal(new[] { portal, otro }.Order().ToArray(), await Vehiculos(""));
        Assert.Equal(new[] { portal }, await Vehiculos("&origen=portal"));
        Assert.Equal(new[] { otro }, await Vehiculos("&estado=REVERTIDA"));
        Assert.Equal(new[] { portal }, await Vehiculos("&estado=vigente"));
        Assert.Equal(new[] { portal, otro }.Order().ToArray(), await Vehiculos($"&desde={HoyT}&hasta={HoyT}"));
        Assert.Empty(await Vehiculos("&desde=2026-10-27"));
        Assert.Empty(await Vehiculos("&hasta=2026-10-25"));
        Assert.Empty(await Vehiculos("&origen=REFERIDO&estado=VIGENTE"));
        Assert.Empty(Ids(await _api.VentasOk(_api.Admin.Token, "?asesorId=999999")));
        // Paginación.
        var pagina = await _api.VentasOk(_api.Admin.Token, a + "&tamano=1&pagina=2");
        Assert.Equal(2, pagina.GetProperty("total").GetInt32());
        Assert.Single(Ids(pagina));
        Assert.Empty(Ids(await _api.VentasOk(_api.Admin.Token, a + "&tamano=1&pagina=3")));
    }

    public static TheoryData<string, string, string> FiltrosInvalidos => new()
    {
        { "?desde=2026-13-01", "desde", MsgFecha },
        { "?hasta=26-10-2026", "hasta", MsgFecha },
        { "?desde=2026-10-27&hasta=2026-10-26", "hasta", MsgRango },
        { "?origen=WEB", "origen", MsgOrigen },
        { "?estado=ANULADA", "estado", MsgEstado },
        { "?asesorId=0", "asesorId", MsgAsesor },
        { "?pagina=0", "pagina", MsgPagina },
        { "?tamano=101", "tamano", MsgPagina },
    };

    [Theory(DisplayName = "CA-P7-04: filtro inválido -> 400 { error, errores } con el campo y el mensaje")]
    [MemberData(nameof(FiltrosInvalidos))]
    public async Task Filtro_invalido(string query, string campo, string mensaje) =>
        await Error400(await _api.Ventas(_api.Admin.Token, query), campo, mensaje, MsgFiltros);

    [Fact(DisplayName = "CA-P7-09: vehículo VENDIDO sin fila de venta (DEMO) -> lista vacía")]
    public async Task Vendido_sin_fila()
    {
        _api.Hora(T);
        var vehiculo = await _api.CrearId(_api.A);
        await _api.MarcarVendido(vehiculo);
        var hist = await Esperar(await _api.VentasVehiculo(_api.A.Token, vehiculo), HttpStatusCode.OK);
        Assert.Empty(Ids(hist));
    }

    [Fact(DisplayName = "CA-P7-13: un vehículo vendido no aparece en el portal ni expone su precio final")]
    public async Task Vendido_fuera_del_portal()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesora Portal Ventas Ficticia");
        var pub = await _api.Publicada(asesor);
        Assert.Contains(Slug(pub), await _api.SlugsPortal());
        await Esperar(await _api.Vender(asesor.Token, pub.GetProperty("vehiculoId").GetInt64()), HttpStatusCode.OK);
        Assert.DoesNotContain(Slug(pub), await _api.SlugsPortal());
        Assert.Equal(HttpStatusCode.NotFound, (await _api.DetallePortal(Slug(pub))).StatusCode);
    }
}
