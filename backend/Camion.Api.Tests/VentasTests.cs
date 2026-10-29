using System.Net;
using System.Text.Json;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;

namespace Camion.Api.Tests;

/// <summary>
/// P4b — marcar vendido (HU-064, RN-GC-001, RN-VE-014, D-22, D-39) y revertir la venta (minuta): datos de la venta,
/// efecto en el vehículo, la publicación, el destacado y el portal; tabla ventas_vehiculo y auditoría Venta.
/// SQLite en memoria y reloj fijo (T = 2026-10-26 15:00 UTC, hoy en Colombia 2026-10-26); nunca MariaDB.
/// </summary>
public sealed class VentasTests(P4bFixture fixture) : IClassFixture<P4bFixture>
{
    private readonly InventarioApi _api = fixture.Api;

    private const string MsgRevisar = "Revisa los datos marcados.";

    public static TheoryData<string, string> Rutas => new()
    {
        { "POST", "/api/inventario/vehiculos/1/vender" },
        { "POST", "/api/inventario/vehiculos/1/revertir-venta" },
    };

    [Theory(DisplayName = "RN-SE-006: vender y revertir son AsesorOAdministrador (401 sin token; 403 comprador)")]
    [MemberData(nameof(Rutas))]
    public async Task Permisos(string metodo, string ruta)
    {
        var cuerpo = new { fechaVenta = HoyT, precioFinalCop = 1, origen = "PORTAL", motivo = "Motivo de prueba" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Enviar(new HttpMethod(metodo), ruta, null, cuerpo)).StatusCode);
        await Error(await _api.Enviar(new HttpMethod(metodo), ruta, _api.TokenComprador, cuerpo), HttpStatusCode.Forbidden, MsgSinPermiso);
    }

    [Fact(DisplayName = "HU-064: vender -> VENDIDO, nota de reserva borrada, publicación VENDIDA fuera del portal, destacado quitado, fila de venta y auditoría")]
    public async Task Vender_200()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesora Vende Ficticia");
        var pub = await _api.Publicada(asesor);
        var vehiculo = pub.GetProperty("vehiculoId").GetInt64();
        await Esperar(await _api.Accion(asesor.Token, vehiculo, "reservar"), HttpStatusCode.OK);
        await _api.AccionPubOk(asesor.Token, IdDe(pub), "destacar");
        Assert.Contains(Slug(pub), await _api.SlugsPortal());

        var json = await Esperar(await _api.Vender(asesor.Token, vehiculo, new
        {
            fechaVenta = HoyT, precioFinalCop = 97_500_000, origen = "contacto_asesor", observaciones = "  Pagó de contado  "
        }), HttpStatusCode.OK);
        Assert.Equal("VENDIDO", json.GetProperty("estado").GetString());
        Assert.True(InventarioJson.EsNulo(json, "notaReserva"));
        var venta = json.GetProperty("venta");
        Assert.Equal(HoyT, venta.GetProperty("fechaVenta").GetString());
        Assert.Equal(97_500_000, venta.GetProperty("precioFinalCop").GetInt64());
        Assert.Equal("CONTACTO_ASESOR", venta.GetProperty("origen").GetString());
        Assert.Equal("Pagó de contado", venta.GetProperty("observaciones").GetString());
        Assert.Equal("VENDIDA", json.GetProperty("publicacion").GetProperty("estado").GetString());

        var fila = Assert.Single(await _api.VentasDb(vehiculo));
        Assert.Equal((IdDe(pub), new DateOnly(2026, 10, 26), 97_500_000m, asesor.UsuarioId, T),
            (fila.PublicacionId!.Value, fila.FechaVenta, fila.PrecioFinalCop, fila.RegistradaPor, fila.RegistradaEn));
        Assert.Null(fila.RevertidaEn);
        var p = await _api.PubDb(IdDe(pub));
        Assert.Equal(Publicacion.Vendido, p.EstadoComercial);
        Assert.Equal(PublicacionDestacada.Finalizada, (await _api.DestacadosDb(IdDe(pub))).Single().Estado);
        Assert.DoesNotContain(Slug(pub), await _api.SlugsPortal());
        Assert.Equal(HttpStatusCode.NotFound, (await _api.DetallePortal(Slug(pub))).StatusCode);
        var item = await Esperar(await _api.DetallePub(asesor.Token, IdDe(pub)), HttpStatusCode.OK);
        Assert.Equal(("VENDIDA", "VENDIDA"), (Texto(item, "estado"), Texto(item, "estadoEfectivo")));
        Assert.Equal(new[] { IdDe(pub) }, await _api.IdsLista(asesor.Token, "?estado=VENDIDA"));

        var audit = Assert.Single(await _api.Auditoria("Venta", fila.Id.ToString()));
        Assert.Equal("VENDER", audit.Accion);
        Assert.Equal("RESERVADO", JsonDe(audit.DatosAnteriores)["estadoVehiculo"].GetString());
        Assert.Equal("VENDIDO", JsonDe(audit.DatosNuevos)["estadoVehiculo"].GetString());
        Assert.Equal("CONTACTO_ASESOR", JsonDe(audit.DatosNuevos)["origen"].GetString());

        // Vendido -> 409; las fotos y la edición del vehículo siguen bloqueadas (P4a).
        await Error(await _api.Vender(asesor.Token, vehiculo), HttpStatusCode.Conflict, "No se puede vender un vehículo en estado vendido.");
        Assert.Single(await _api.VentasDb(vehiculo));
    }

    [Fact(DisplayName = "HU-064: un vehículo sin publicación también se vende (publicacionId nulo); uno INACTIVO -> 409")]
    public async Task Vender_sin_publicacion_e_inactivo()
    {
        _api.Hora(T);
        var vehiculo = await _api.CrearId(_api.A);
        await Esperar(await _api.Vender(_api.A.Token, vehiculo), HttpStatusCode.OK);
        Assert.Null(Assert.Single(await _api.VentasDb(vehiculo)).PublicacionId);

        var inactivo = await _api.CrearId(_api.A);
        await Esperar(await _api.Accion(_api.A.Token, inactivo, "inactivar", "Fuera de servicio"), HttpStatusCode.OK);
        await Error(await _api.Vender(_api.A.Token, inactivo), HttpStatusCode.Conflict, "No se puede vender un vehículo en estado inactivo.");
        Assert.Empty(await _api.VentasDb(inactivo));
    }

    public static TheoryData<string, object, string> Invalidos => new()
    {
        { "fechaVenta", new { fechaVenta = "2026-10-27", precioFinalCop = 1, origen = "PORTAL" }, "Escribe la fecha de la venta (AAAA-MM-DD), de hoy o anterior." },
        { "fechaVenta", new { fechaVenta = "26/10/2026", precioFinalCop = 1, origen = "PORTAL" }, "Escribe la fecha de la venta (AAAA-MM-DD), de hoy o anterior." },
        { "fechaVenta", new { precioFinalCop = 1, origen = "PORTAL" }, "Escribe la fecha de la venta (AAAA-MM-DD), de hoy o anterior." },
        { "fechaVenta", new { fechaVenta = "2026-10-25", precioFinalCop = 1, origen = "PORTAL" }, "La fecha de la venta no puede ser anterior al registro del vehículo." },
        { "precioFinalCop", new { fechaVenta = "2026-10-26", precioFinalCop = 0, origen = "PORTAL" }, "Escribe el precio final en pesos (número entero mayor que 0)." },
        { "precioFinalCop", new { fechaVenta = "2026-10-26", precioFinalCop = 10.5, origen = "PORTAL" }, "Escribe el precio final en pesos (número entero mayor que 0)." },
        { "precioFinalCop", new { fechaVenta = "2026-10-26", origen = "PORTAL" }, "Escribe el precio final en pesos (número entero mayor que 0)." },
        { "origen", new { fechaVenta = "2026-10-26", precioFinalCop = 1, origen = "FERIA" }, "Elige el origen de la venta." },
        { "origen", new { fechaVenta = "2026-10-26", precioFinalCop = 1 }, "Elige el origen de la venta." },
        { "observaciones", new { fechaVenta = "2026-10-26", precioFinalCop = 1, origen = "OTRO", observaciones = "   " }, "Escribe las observaciones: son obligatorias si el origen es Otro." },
        { "observaciones", new { fechaVenta = "2026-10-26", precioFinalCop = 1, origen = "REFERIDO", observaciones = new string('o', 1001) }, "Las observaciones no pueden tener más de 1000 caracteres." },
    };

    [Theory(DisplayName = "D-39: fecha (hoy o anterior, no antes del registro), precio entero > 0, origen de la lista y observaciones (obligatorias con OTRO) -> 400")]
    [MemberData(nameof(Invalidos))]
    public async Task Vender_400(string campo, object cuerpo, string mensaje)
    {
        _api.Hora(T);
        var vehiculo = await _api.CrearId(_api.A);
        await Error400(await _api.Vender(_api.A.Token, vehiculo, cuerpo), campo, mensaje, MsgRevisar);
        Assert.Equal("ACTIVO", (await _api.VehiculoDb(vehiculo)).Estado);
        Assert.Empty(await _api.VentasDb(vehiculo));
    }

    [Fact(DisplayName = "D-39: origen OTRO con observaciones y fecha anterior válida -> 200")]
    public async Task Vender_otro_con_observaciones()
    {
        _api.Hora(T);
        var vehiculo = await _api.CrearId(_api.A);
        _api.Hora(T.AddDays(3));
        await Esperar(await _api.Vender(_api.A.Token, vehiculo, new
        {
            fechaVenta = "2026-10-27", precioFinalCop = 5_000_000, origen = "OTRO", observaciones = "Feria de transporte"
        }), HttpStatusCode.OK);
        Assert.Equal(VentaVehiculo.OrigenOtro, Assert.Single(await _api.VentasDb(vehiculo)).Origen);
    }

    [Fact(DisplayName = "RN-GC-001: el asesor no vende ni revierte lo ajeno (404); el administrador sí")]
    public async Task Ajeno_y_admin()
    {
        _api.Hora(T);
        var vehiculo = await _api.CrearId(_api.A);
        await Error(await _api.Vender(_api.B.Token, vehiculo), HttpStatusCode.NotFound, MsgVehiculoNoEncontrado);
        await Esperar(await _api.Vender(_api.Admin.Token, vehiculo), HttpStatusCode.OK);
        Assert.Equal(_api.Admin.Id, Assert.Single(await _api.VentasDb(vehiculo)).RegistradaPor);
        await Error(await _api.RevertirVenta(_api.B.Token, vehiculo), HttpStatusCode.NotFound, MsgVehiculoNoEncontrado);
        await Esperar(await _api.RevertirVenta(_api.Admin.Token, vehiculo), HttpStatusCode.OK);
        Assert.Equal(_api.Admin.Id, Assert.Single(await _api.VentasDb(vehiculo)).RevertidaPor);
    }

    [Fact(DisplayName = "Revertir: motivo obligatorio; VENDIDO -> ACTIVO, publicación DISPONIBLE pero OCULTA (fuera del portal), venta marcada; reactivar la vuelve a mostrar")]
    public async Task Revertir()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesor Revierte Ficticio");
        var pub = await _api.Publicada(asesor);
        var vehiculo = pub.GetProperty("vehiculoId").GetInt64();
        await Error(await _api.RevertirVenta(asesor.Token, vehiculo), HttpStatusCode.Conflict,
            "No se puede revertir la venta de un vehículo en estado activo.");
        await Esperar(await _api.Vender(asesor.Token, vehiculo), HttpStatusCode.OK);
        await Error400(await _api.RevertirVenta(asesor.Token, vehiculo, "abc"), "motivo", "Escribe el motivo (5 a 500 caracteres).", MsgRevisar);

        _api.Hora(T.AddHours(1));
        var json = await Esperar(await _api.RevertirVenta(asesor.Token, vehiculo, "El comprador desistió"), HttpStatusCode.OK);
        Assert.Equal("ACTIVO", json.GetProperty("estado").GetString());
        Assert.True(InventarioJson.EsNulo(json, "venta"));
        Assert.Equal("OCULTA", json.GetProperty("publicacion").GetProperty("estado").GetString());
        var p = await _api.PubDb(IdDe(pub));
        Assert.Equal((Publicacion.Disponible, Publicacion.Oculta, "El comprador desistió"),
            (p.EstadoComercial, p.EstadoPublicacion, p.MotivoOcultamiento));
        var venta = Assert.Single(await _api.VentasDb(vehiculo));
        Assert.Equal((asesor.UsuarioId, T.AddHours(1), "El comprador desistió"), (venta.RevertidaPor, venta.RevertidaEn!.Value, venta.MotivoReversion));
        Assert.DoesNotContain(Slug(pub), await _api.SlugsPortal());
        var audit = (await _api.Auditoria("Venta", venta.Id.ToString())).Last();
        Assert.Equal("REVERTIR", audit.Accion);
        Assert.Equal("ACTIVO", JsonDe(audit.DatosNuevos)["estadoVehiculo"].GetString());

        await _api.AccionPubOk(asesor.Token, IdDe(pub), "reactivar");
        Assert.Contains(Slug(pub), await _api.SlugsPortal());
        // Se puede volver a vender: queda una venta revertida y otra vigente.
        await Esperar(await _api.Vender(asesor.Token, vehiculo), HttpStatusCode.OK);
        Assert.Equal(2, (await _api.VentasDb(vehiculo)).Count);
    }

    [Fact(DisplayName = "Revertir un VENDIDO sin fila de venta (DEMO/anterior a P4b) -> 200, auditoría sobre el vehículo")]
    public async Task Revertir_sin_fila()
    {
        _api.Hora(T);
        var vehiculo = await _api.CrearId(_api.A);
        await _api.MarcarVendido(vehiculo);
        await Esperar(await _api.RevertirVenta(_api.A.Token, vehiculo), HttpStatusCode.OK);
        Assert.Equal("ACTIVO", (await _api.VehiculoDb(vehiculo)).Estado);
        Assert.Contains(await _api.AuditoriaVehiculo(vehiculo), a => a.Accion == "REVERTIR");
    }
}
