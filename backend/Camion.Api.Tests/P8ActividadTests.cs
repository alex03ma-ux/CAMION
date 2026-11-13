using System.Net;
using System.Text.Json;
using Camion.Api.Reportes;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP7;

namespace Camion.Api.Tests;

/// <summary>
/// P8 — "Actividad" de un vehículo y de una suscripción (HU-061, RN-SE-003, D-62 d, D-72 A/A; CA-P8-56 a CA-P8-62).
/// Solo el administrador, solo lectura, acciones en palabras, sin datos de clientes ni nota de reserva. SQLite en memoria.
/// </summary>
public sealed class P8ActividadTests(P7Fixture fixture) : IClassFixture<P7Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    private static Task<HttpResponseMessage> Actividad(InventarioApi api, string sobre, long id, string? token, string query = "") =>
        api.Enviar(HttpMethod.Get, $"/api/admin/{sobre}/{id}/actividad{query}", token);

    private static JsonElement[] Items(JsonElement e) => e.GetProperty("items").EnumerateArray().ToArray();

    [Fact(DisplayName = "CA-P8-56, 58: actividad de un vehículo con sus publicaciones y ventas, de la más reciente a la más antigua")]
    public async Task Actividad_vehiculo()
    {
        _api.Hora(T);
        var (asesor, _) = await _api.AsesorConPlan("Asesora Actividad Ficticia");
        var (destino, _) = await _api.AsesorConPlan("Asesor Destino Ficticio");
        var id = await _api.CrearId(asesor);
        await Esperar(await _api.Editar(asesor.Token, id, InventarioApi.Con(_api.Cuerpo("camion"), ("precioCop", 120_000_000))),
            HttpStatusCode.OK);
        await Esperar(await _api.Accion(asesor.Token, id, "reservar"), HttpStatusCode.OK);
        await Esperar(await _api.Accion(asesor.Token, id, "liberar"), HttpStatusCode.OK);
        await Esperar(await _api.Publicar(asesor.Token, id), HttpStatusCode.Created);
        await Esperar(await _api.Vender(asesor.Token, id, new
        {
            fechaVenta = HoyT, precioFinalCop = 95_000_000, origen = "PORTAL", observaciones = "Lo compró Pedro Comprador (prueba)"
        }), HttpStatusCode.OK);
        await Esperar(await _api.RevertirVenta(asesor.Token, id), HttpStatusCode.OK);
        await Esperar(await _api.Reasignar(_api.Admin.Token, id, destino.PerfilId), HttpStatusCode.OK);

        var r = await Actividad(_api, "vehiculos", id, _api.Admin.Token);
        var texto = await r.Content.ReadAsStringAsync();
        var json = await Esperar(r, HttpStatusCode.OK);
        Assert.Equal(8, json.GetProperty("total").GetInt32());
        var items = Items(json);
        Assert.Equal(new[] { "REASIGNAR", "REVERTIR", "VENDER", "CREAR", "LIBERAR", "RESERVAR", "EDITAR", "CREAR" },
            items.Select(x => x.GetProperty("accion").GetString()).ToArray());
        Assert.Equal(new[] { "VEHICULO", "VENTA", "VENTA", "PUBLICACION", "VEHICULO", "VEHICULO", "VEHICULO", "VEHICULO" },
            items.Select(x => x.GetProperty("sobre").GetString()).ToArray());

        var reasignar = items[0];
        Assert.Equal("Reasignó de Asesora Actividad Ficticia a Asesor Destino Ficticio", reasignar.GetProperty("descripcion").GetString());
        Assert.Equal("Cambio de zona del asesor (prueba)", reasignar.GetProperty("motivo").GetString());
        Assert.Equal("Admin Prueba", reasignar.GetProperty("quien").GetString());
        Assert.Equal("Revirtió la venta", items[1].GetProperty("descripcion").GetString());
        Assert.Equal("Se cayó el negocio (prueba)", items[1].GetProperty("motivo").GetString());
        Assert.Equal("Marcó vendido por $ 95.000.000 (Portal web)", items[2].GetProperty("descripcion").GetString());
        Assert.Equal("Creó la publicación", items[3].GetProperty("descripcion").GetString());
        Assert.Equal("Reservó el vehículo", items[5].GetProperty("descripcion").GetString());
        Assert.Equal(JsonValueKind.Null, items[5].GetProperty("motivo").ValueKind); // la nota de reserva no se muestra (D-28)
        var editar = items[6];
        Assert.Equal("Editó el vehículo", editar.GetProperty("descripcion").GetString());
        var precio = editar.GetProperty("cambios").EnumerateArray().Single(c => c.GetProperty("campo").GetString() == "Precio");
        Assert.Equal(("$ 100.000.000", "$ 120.000.000"), (precio.GetProperty("anterior").GetString(), precio.GetProperty("nuevo").GetString()));
        Assert.Equal("Registró el vehículo", items[7].GetProperty("descripcion").GetString());
        Assert.Equal(asesor.Nombre, items[7].GetProperty("quien").GetString());
        Assert.All(items.Where(x => x.GetProperty("accion").GetString() != "EDITAR"), x => Assert.Empty(x.GetProperty("cambios").EnumerateArray()));

        // CA-P8-58: nunca la nota de reserva ni las observaciones de la venta (pueden nombrar al comprador).
        Assert.DoesNotContain(InventarioApi.NotaReservaPrueba, texto);
        Assert.DoesNotContain("Pedro Comprador", texto);

        // CA-P8-62: paginación (20 por defecto, máximo 100).
        var pagina = await Esperar(await Actividad(_api, "vehiculos", id, _api.Admin.Token, "?tamano=3&pagina=2"), HttpStatusCode.OK);
        Assert.Equal((8, 2, 3), (pagina.GetProperty("total").GetInt32(), pagina.GetProperty("pagina").GetInt32(), pagina.GetProperty("tamano").GetInt32()));
        Assert.Equal(new[] { "CREAR", "LIBERAR", "RESERVAR" }, Items(pagina).Select(x => x.GetProperty("accion").GetString()).ToArray());
        Assert.Equal(20, (await Esperar(await Actividad(_api, "vehiculos", id, _api.Admin.Token), HttpStatusCode.OK)).GetProperty("tamano").GetInt32());
        await Error400(await Actividad(_api, "vehiculos", id, _api.Admin.Token, "?tamano=101"), "tamano", MensajesP8.Pagina, MensajesP8.Filtros);

        // CA-P8-59: consultar no cambia nada.
        var eventos = await _api.Leer(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.AuditoriaEventos));
        await Actividad(_api, "vehiculos", id, _api.Admin.Token);
        Assert.Equal(eventos, await _api.Leer(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.AuditoriaEventos)));

        // CA-P8-60: el asesor dueño no la ve (403), tampoco el comprador; sin sesión, 401.
        await Error(await Actividad(_api, "vehiculos", id, destino.Token), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await Actividad(_api, "vehiculos", id, _api.TokenComprador), HttpStatusCode.Forbidden, MsgSinPermiso);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Actividad(_api, "vehiculos", id, null)).StatusCode);
    }

    [Fact(DisplayName = "CA-P8-57: actividad de una suscripción con sus pagos y lo que deshizo D-67")]
    public async Task Actividad_suscripcion()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor("Asesor Plan Actividad Ficticio");
        var plan = await _api.PlanConPrecio(100_000);
        var sus = (await Esperar(await _api.AsignarConPago(asesor.PerfilId, plan, PagoValido()), HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        await _api.AccionOk(sus, "renovar", new { pago = PagoValido(medio: "EFECTIVO", referencia: null) });
        var pagos = await _api.PagosDb(sus);
        var (pagoAsignacion, pagoRenovacion) = (pagos[0].Id, pagos[1].Id);
        await Esperar(await _api.AnularPago(pagoRenovacion), HttpStatusCode.OK);

        var json = await Esperar(await Actividad(_api, "suscripciones", sus, _api.Admin.Token), HttpStatusCode.OK);
        var items = Items(json);
        Assert.Equal(6, json.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "ANULAR", "DESHACER_PAGO" }, items[..2].Select(x => x.GetProperty("accion").GetString()).Order(StringComparer.Ordinal).ToArray());
        string Descripcion(string accion, long? sobreId = null) => items.Single(x => x.GetProperty("accion").GetString() == accion &&
            (sobreId is null || x.GetProperty("sobreId").GetInt64() == sobreId)).GetProperty("descripcion").GetString()!;
        Assert.StartsWith("Asignó el plan ", Descripcion("ASIGNAR"));
        Assert.EndsWith(" hasta el 2026-11-25", Descripcion("ASIGNAR"));
        Assert.Equal("Renovó el plan hasta el 2026-12-25", Descripcion("RENOVAR"));
        Assert.Equal($"Registró el pago #{pagoAsignacion} de $ 100.000 por Transferencia (Asignación)", Descripcion("REGISTRAR", pagoAsignacion));
        Assert.Equal($"Registró el pago #{pagoRenovacion} de $ 100.000 por Efectivo (Renovación)", Descripcion("REGISTRAR", pagoRenovacion));
        Assert.Equal($"Anuló el pago #{pagoRenovacion}", Descripcion("ANULAR"));
        Assert.Equal($"Deshizo la renovación al anular el pago #{pagoRenovacion} hasta el 2026-11-25", Descripcion("DESHACER_PAGO"));
        Assert.Equal("Valor mal escrito (prueba)",
            items.Single(x => x.GetProperty("accion").GetString() == "ANULAR").GetProperty("motivo").GetString());
        Assert.DoesNotContain("TRX-PRUEBA-1", json.GetRawText()); // la referencia del pago no se muestra

        await Error(await Actividad(_api, "suscripciones", sus, asesor.Token), HttpStatusCode.Forbidden, MsgSinPermiso);
    }

    [Fact(DisplayName = "CA-P8-61: vehículo o suscripción inexistente -> 404 con su mensaje")]
    public async Task No_encontrado()
    {
        await Error(await Actividad(_api, "vehiculos", 999_999, _api.Admin.Token), HttpStatusCode.NotFound, MensajesP8.VehiculoNoEncontrado);
        await Error(await Actividad(_api, "suscripciones", 999_999, _api.Admin.Token), HttpStatusCode.NotFound,
            MensajesP8.SuscripcionNoEncontrada);
    }
}
