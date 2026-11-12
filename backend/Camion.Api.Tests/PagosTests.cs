using System.Net;
using System.Text.Json;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP7;

namespace Camion.Api.Tests;

/// <summary>
/// P7 — registro de pagos del plan (D-61 B, FN-04; CA-P7-27 a CA-P7-33): pago al asignar, renovar o cambiar de plan,
/// historial por suscripción, pago posterior y anulación con motivo. Solo el administrador. SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class PagosTests(P7Fixture fixture) : IClassFixture<P7Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    private async Task<(AsesorPrueba Asesor, long Plan)> AsesorYPlan(string nombre, long precio = 100_000)
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor(nombre);
        return (asesor, await _api.PlanConPrecio(precio));
    }

    private Task<string> NombreAdmin() =>
        _api.Leer(db => db.Users.AsNoTracking().Where(u => u.Id == _api.Admin.Id).Select(u => u.NombreCompleto).SingleAsync());

    public static TheoryData<string, string> Rutas => new()
    {
        { "GET", "/api/admin/suscripciones/1/pagos" },
        { "POST", "/api/admin/suscripciones/1/pagos" },
        { "POST", "/api/admin/pagos/1/anular" },
    };

    [Theory(DisplayName = "CA-P7-32: rutas de pagos -> 401 sin sesión; 403 asesor y comprador")]
    [MemberData(nameof(Rutas))]
    public async Task Permisos(string metodo, string ruta)
    {
        var cuerpo = new { fechaPago = HoyT, medio = "EFECTIVO", motivo = "Motivo de prueba" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Enviar(new HttpMethod(metodo), ruta, null, cuerpo)).StatusCode);
        foreach (var token in new[] { _api.A.Token, _api.TokenComprador })
            await Error(await _api.Enviar(new HttpMethod(metodo), ruta, token, cuerpo), HttpStatusCode.Forbidden, MsgSinPermiso);
    }

    [Fact(DisplayName = "CA-P7-27, CA-P7-30, CA-P7-54: asignar con pago -> suscripción y pago REGISTRADO auditados; historial y total")]
    public async Task Asignar_con_pago()
    {
        var (asesor, plan) = await AsesorYPlan("Asesora Pago Ficticia");
        var sus = await Esperar(await _api.AsignarConPago(asesor.PerfilId, plan,
            new { fechaPago = HoyT, medio = "transferencia", referencia = "  TRX-001  " }), HttpStatusCode.Created);
        var susId = IdDe(sus);

        var fila = Assert.Single(await _api.PagosDb(susId));
        Assert.Equal((100_000m, new DateOnly(2026, 10, 26), "TRANSFERENCIA", "TRX-001", "ASIGNACION", "REGISTRADO", _api.Admin.Id),
            (fila.ValorCop, fila.FechaPago!.Value, fila.Medio, fila.Referencia, fila.Concepto, fila.Estado, fila.RegistradoPor));

        var pagos = await _api.PagosOk(susId);
        Assert.Equal(100_000, pagos.GetProperty("totalPagadoCop").GetInt64());
        var item = Assert.Single(pagos.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal(new[]
            {
                "anuladoEn", "anuladoPor", "concepto", "estado", "fechaPago", "id", "medio", "motivoAnulacion", "referencia",
                "registradoEn", "registradoPor", "suscripcionId", "valorCop"
            },
            item.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(HoyT, item.GetProperty("fechaPago").GetString());
        Assert.Equal(await NombreAdmin(), item.GetProperty("registradoPor").GetString());
        Assert.Equal(T, Fecha(item, "registradoEn"));

        var audit = Assert.Single(await _api.Auditoria("Pago", fila.Id.ToString()));
        Assert.Equal(("REGISTRAR", _api.Admin.Id), (audit.Accion, audit.UsuarioId));
        var datos = JsonDe(audit.DatosNuevos);
        Assert.Equal(100_000, datos["valorCop"].GetInt64());
        Assert.Equal("ASIGNACION", datos["concepto"].GetString());
        Assert.Equal(susId, datos["suscripcionId"].GetInt64());
        Assert.Null(audit.DatosAnteriores);
    }

    public static TheoryData<string, object, string, string> PagosInvalidos => new()
    {
        { "valor 0", new { valorCop = 0, fechaPago = "2026-10-26", medio = "EFECTIVO" }, "valorCop", MsgValor },
        { "valor con decimales", new { valorCop = 1.5, fechaPago = "2026-10-26", medio = "EFECTIVO" }, "valorCop", MsgValor },
        { "fecha futura", new { fechaPago = "2026-10-27", medio = "EFECTIVO" }, "fechaPago", MsgFechaPago },
        { "fecha mal escrita", new { fechaPago = "26/10/2026", medio = "EFECTIVO" }, "fechaPago", MsgFechaPago },
        { "sin fecha", new { medio = "EFECTIVO" }, "fechaPago", MsgFechaPago },
        { "medio fuera de lista", new { fechaPago = "2026-10-26", medio = "TARJETA" }, "medio", MsgMedio },
        { "sin medio", new { fechaPago = "2026-10-26" }, "medio", MsgMedio },
        { "Otro sin referencia", new { fechaPago = "2026-10-26", medio = "OTRO", referencia = "  " }, "referencia", MsgReferenciaOtro },
        { "referencia larga", new { fechaPago = "2026-10-26", medio = "EFECTIVO", referencia = new string('R', 101) }, "referencia", MsgReferencia },
        { "referencia con HTML", new { fechaPago = "2026-10-26", medio = "EFECTIVO", referencia = "<b>x</b>" }, "referencia", MsgReferencia },
    };

    [Theory(DisplayName = "CA-P7-29: pago inválido -> 400 con el campo; no se crea la suscripción ni el pago")]
    [MemberData(nameof(PagosInvalidos))]
    public async Task Pago_invalido(string caso, object pago, string campo, string mensaje)
    {
        Assert.NotNull(caso);
        var (asesor, plan) = await AsesorYPlan(Unico("Asesor Invalido"));
        var pagosAntes = await _api.TotalPagos();
        await Error400(await _api.AsignarConPago(asesor.PerfilId, plan, pago), campo, mensaje);
        Assert.Equal(0, await _api.TotalSuscripciones(asesor.PerfilId));
        Assert.Equal(pagosAntes, await _api.TotalPagos());
    }

    [Fact(DisplayName = "CA-P7-28: plan de precio 0 -> sin pago se asigna; con pago, 400 errores.pago y nada cambia")]
    public async Task Plan_sin_precio()
    {
        var (asesor, plan) = await AsesorYPlan("Asesor Plan Cero Ficticio", precio: 0);
        await Error400(await _api.AsignarConPago(asesor.PerfilId, plan, PagoValido()), "pago", MsgPlanSinPrecio);
        Assert.Equal(0, await _api.TotalSuscripciones(asesor.PerfilId));

        var sus = IdDe(await Esperar(await _api.AsignarConPago(asesor.PerfilId, plan, null), HttpStatusCode.Created));
        Assert.Empty(await _api.PagosDb(sus));
        var pagos = await _api.PagosOk(sus);
        Assert.Equal(0, pagos.GetProperty("totalPagadoCop").GetInt64());
        // Pago posterior sobre un plan sin precio: 400.
        await Error400(await _api.RegistrarPago(sus, PagoValido()), "pago", MsgPlanSinPrecio);
    }

    [Fact(DisplayName = "P4p (regresión): asignar sin pago sigue igual y no crea pago")]
    public async Task Asignar_sin_pago()
    {
        var (asesor, plan) = await AsesorYPlan("Asesora Sin Pago Ficticia");
        var sus = IdDe(await _api.Asignada(asesor.PerfilId, plan));
        Assert.Empty(await _api.PagosDb(sus));
    }

    [Fact(DisplayName = "CA-P7-27: renovar y cambiar de plan con pago -> RENOVACION y CAMBIO_PLAN (en la suscripción nueva)")]
    public async Task Renovar_y_cambiar_con_pago()
    {
        var (asesor, plan) = await AsesorYPlan("Asesor Renueva Pago Ficticio");
        var sus = IdDe(await Esperar(await _api.AsignarConPago(asesor.PerfilId, plan, PagoValido(valorCop: 90_000)),
            HttpStatusCode.Created));
        await _api.AccionOk(sus, "renovar", new { pago = PagoValido(medio: "CONSIGNACION", referencia: null) });
        var pagos = await _api.PagosOk(sus);
        Assert.Equal(190_000, pagos.GetProperty("totalPagadoCop").GetInt64());
        Assert.Equal(new[] { "ASIGNACION", "RENOVACION" }, (await _api.PagosDb(sus)).Select(p => p.Concepto).ToArray());
        Assert.Null((await _api.PagosDb(sus))[1].Referencia);

        // Renovar con un pago inválido no extiende la vigencia.
        var fin = (await _api.SusDb(sus)).FinVigencia;
        await Error400(await _api.AccionSus(sus, "renovar", new { pago = new { fechaPago = HoyT } }), "medio", MsgMedio);
        Assert.Equal(fin, (await _api.SusDb(sus)).FinVigencia);

        var otroPlan = await _api.PlanConPrecio(250_000, meses: 3);
        var nueva = IdDe(await _api.AccionOk(sus, "cambiar-plan", new { planId = otroPlan, pago = PagoValido(medio: "EFECTIVO") }));
        Assert.NotEqual(sus, nueva);
        var fila = Assert.Single(await _api.PagosDb(nueva));
        Assert.Equal(("CAMBIO_PLAN", 250_000m), (fila.Concepto, fila.ValorCop));
        Assert.Equal(2, (await _api.PagosDb(sus)).Count);
    }

    [Fact(DisplayName = "CA-P7-31: anular con motivo -> ANULADO auditado, fuera del total; la suscripción no cambia; segunda vez 409")]
    public async Task Anular()
    {
        var (asesor, plan) = await AsesorYPlan("Asesora Anula Ficticia");
        var sus = IdDe(await Esperar(await _api.AsignarConPago(asesor.PerfilId, plan, PagoValido()), HttpStatusCode.Created));
        var pago = Assert.Single(await _api.PagosDb(sus));
        var antes = await _api.SusDb(sus);
        _api.Hora(T.AddHours(1));

        await Error400(await _api.AnularPago(pago.Id, "abcd"), "motivo", MsgMotivo);
        await Error400(await _api.AnularPago(pago.Id, null), "motivo", MsgMotivo);
        await Error(await _api.AnularPago(999_999), HttpStatusCode.NotFound, MsgPagoNoEncontrado);

        var json = await Esperar(await _api.AnularPago(pago.Id, "  Valor mal escrito (prueba)  "), HttpStatusCode.OK);
        Assert.Equal("ANULADO", json.GetProperty("estado").GetString());
        Assert.Equal("Valor mal escrito (prueba)", json.GetProperty("motivoAnulacion").GetString());
        Assert.Equal(await NombreAdmin(), json.GetProperty("anuladoPor").GetString());
        Assert.Equal(T.AddHours(1), Fecha(json, "anuladoEn"));

        var pagos = await _api.PagosOk(sus);
        Assert.Equal(0, pagos.GetProperty("totalPagadoCop").GetInt64());
        Assert.Single(pagos.GetProperty("items").EnumerateArray());   // la fila no se borra
        var despues = await _api.SusDb(sus);
        Assert.Equal((antes.Estado, antes.FinVigencia, antes.PlanId), (despues.Estado, despues.FinVigencia, despues.PlanId));

        var eventos = await _api.Auditoria("Pago", pago.Id.ToString());
        Assert.Equal(new[] { "REGISTRAR", "ANULAR" }, eventos.Select(e => e.Accion).ToArray());
        Assert.Equal("Valor mal escrito (prueba)", JsonDe(eventos[1].DatosNuevos)["motivo"].GetString());
        Assert.Equal("REGISTRADO", JsonDe(eventos[1].DatosAnteriores)["estado"].GetString());

        await Error(await _api.AnularPago(pago.Id), HttpStatusCode.Conflict, MsgYaAnulado);
        Assert.Equal(2, (await _api.Auditoria("Pago", pago.Id.ToString())).Count);
        _api.Hora(T);
    }

    [Fact(DisplayName = "Contrato P7 §2 (PROPUESTO): pago posterior -> 201 POSTERIOR con el precio del plan por defecto; 404 y 400")]
    public async Task Pago_posterior()
    {
        var (asesor, plan) = await AsesorYPlan("Asesor Posterior Ficticio", precio: 120_000);
        var sus = IdDe(await _api.Asignada(asesor.PerfilId, plan));
        var json = await Esperar(await _api.RegistrarPago(sus, PagoValido(medio: "OTRO", referencia: "Nequi 123")),
            HttpStatusCode.Created);
        Assert.Equal(("POSTERIOR", 120_000, "OTRO", "REGISTRADO"), (json.GetProperty("concepto").GetString(),
            json.GetProperty("valorCop").GetInt64(), json.GetProperty("medio").GetString(), json.GetProperty("estado").GetString()));
        Assert.Equal(sus, json.GetProperty("suscripcionId").GetInt64());
        Assert.Single(await _api.Auditoria("Pago", json.GetProperty("id").GetInt64().ToString()));

        await Error(await _api.RegistrarPago(999_999, PagoValido()), HttpStatusCode.NotFound, MsgNoEncontrado);
        await Error(await _api.Pagos(999_999), HttpStatusCode.NotFound, MsgNoEncontrado);
        await Error400(await _api.RegistrarPago(sus, new { fechaPago = HoyT }), "medio", MsgMedio);
        await Error400(await _api.RegistrarPago(sus, null), "fechaPago", MsgFechaPago);
    }

    [Fact(DisplayName = "CA-P7-30: el historial viene por fecha de pago descendente")]
    public async Task Orden_por_fecha()
    {
        var (asesor, plan) = await AsesorYPlan("Asesora Orden Pagos Ficticia");
        var sus = IdDe(await _api.Asignada(asesor.PerfilId, plan));
        var viejo = (await Esperar(await _api.RegistrarPago(sus, PagoValido(fechaPago: "2026-10-01")), HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        var nuevo = (await Esperar(await _api.RegistrarPago(sus, PagoValido(fechaPago: "2026-10-20")), HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        var medio = (await Esperar(await _api.RegistrarPago(sus, PagoValido(fechaPago: "2026-10-10")), HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        Assert.Equal(new[] { nuevo, medio, viejo }, Ids(await _api.PagosOk(sus)));
    }

    [Fact(DisplayName = "CA-P7-32: el asesor no ve pagos en Mi suscripción")]
    public async Task Asesor_no_ve_pagos()
    {
        var (asesor, plan) = await AsesorYPlan("Asesor Mi Plan Pagos Ficticio");
        await Esperar(await _api.AsignarConPago(asesor.PerfilId, plan, PagoValido()), HttpStatusCode.Created);
        var texto = await (await _api.MiSuscripcion(asesor.Token)).Content.ReadAsStringAsync();
        Assert.DoesNotContain("pago", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRX-PRUEBA", texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "CA-P7-31: dos anulaciones simultáneas -> una 200 y otra 409; un solo evento ANULAR")]
    public async Task Anular_concurrente()
    {
        var (asesor, plan) = await AsesorYPlan("Asesor Anula Doble Ficticio");
        var sus = IdDe(await Esperar(await _api.AsignarConPago(asesor.PerfilId, plan, PagoValido()), HttpStatusCode.Created));
        var pago = Assert.Single(await _api.PagosDb(sus));
        var respuestas = await Task.WhenAll(_api.AnularPago(pago.Id), _api.AnularPago(pago.Id));
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
            respuestas.Select(r => r.StatusCode).OrderBy(c => (int)c).ToArray());
        Assert.Single(await _api.Auditoria("Pago", pago.Id.ToString()), e => e.Accion == "ANULAR");
    }

    private static long IdDe(JsonElement e) => e.GetProperty("id").GetInt64();
}
