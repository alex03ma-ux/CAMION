using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP7;

namespace Camion.Api.Tests;

/// <summary>
/// D-67 — anular un pago deshace lo que ese pago dio: renovación (vuelve la vigencia anterior), asignación (CANCELADA),
/// cambio de plan (vuelve el plan y la vigencia anteriores), posterior (solo el pago); orden (solo el movimiento más
/// reciente, 409 si no); fecha restaurada ya pasada (vencida, fuera del portal, contactos a Administración); y la ruta
/// GET /api/admin/pagos/{id}/efecto-anulacion. SQLite en memoria y reloj fijo; nunca MariaDB.
/// </summary>
public sealed class AnulacionD67Tests(P7Fixture fixture) : IClassFixture<P7Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    private const string MsgPosterior =
        "Después de este pago hubo otra asignación, renovación o cambio de plan en esta suscripción. Anula primero el pago de ese movimiento.";

    private Task<HttpResponseMessage> Efecto(long pagoId, string? token = "") =>
        _api.Enviar(HttpMethod.Get, $"/api/admin/pagos/{pagoId}/efecto-anulacion", token == "" ? _api.Admin.Token : token);

    private async Task<JsonElement> EfectoOk(long pagoId) => await Esperar(await Efecto(pagoId), HttpStatusCode.OK);

    private async Task<long> UltimoPago(long sus) => (await _api.PagosDb(sus)).Last().Id;

    private static string? Texto(JsonElement e, string campo) =>
        e.GetProperty(campo).ValueKind == JsonValueKind.Null ? null : e.GetProperty(campo).GetString();

    [Fact(DisplayName = "D-67: efecto-anulacion -> 401 sin sesión, 403 asesor y comprador, 404 si no existe")]
    public async Task Efecto_permisos()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Efecto(1, null)).StatusCode);
        foreach (var token in new[] { _api.A.Token, _api.TokenComprador })
            await Error(await Efecto(1, token), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await Efecto(999_999), HttpStatusCode.NotFound, MsgPagoNoEncontrado);
    }

    [Fact(DisplayName = "D-67: anular el pago de una renovación -> vuelven la fecha y el plan de antes; el efecto lo anticipa sin cambiar nada")]
    public async Task Renovacion()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor(Unico("Asesora Renovacion D67"));
        var planA = await _api.PlanConPrecio(100_000);
        var planB = await _api.PlanConPrecio(200_000, meses: 2);
        var sus = IdDe(await _api.Asignada(asesor.PerfilId, planA));
        _api.Hora(T.AddDays(1));
        await _api.AccionOk(sus, "renovar", new { planId = planB, pago = PagoValido() });
        Assert.Equal(T.AddDays(90), (await _api.SusDb(sus)).FinVigencia);
        var pago = await UltimoPago(sus);

        var efecto = await EfectoOk(pago);
        Assert.Equal("DESHACER_RENOVACION", Texto(efecto, "tipo"));
        Assert.Equal(T.AddDays(30), Fecha(efecto, "nuevaFinVigencia"));
        Assert.Equal(planA, efecto.GetProperty("nuevoPlan").GetProperty("id").GetInt64());
        Assert.False(efecto.GetProperty("quedaraVencida").GetBoolean());
        Assert.Null(Texto(efecto, "bloqueadoPor"));
        Assert.Equal(sus, efecto.GetProperty("suscripcionId").GetInt64());
        Assert.Equal(T.AddDays(90), (await _api.SusDb(sus)).FinVigencia);   // GET no cambia nada

        await Esperar(await _api.AnularPago(pago), HttpStatusCode.OK);
        var s = await _api.SusDb(sus);
        Assert.Equal(("ACTIVA", T.AddDays(30), planA, T), (s.Estado, s.FinVigencia!.Value, s.PlanId, s.InicioVigencia!.Value));

        var deshacer = (await _api.Auditoria("Suscripcion", sus.ToString())).Last();
        Assert.Equal("DESHACER_PAGO", deshacer.Accion);
        var nuevos = JsonDe(deshacer.DatosNuevos);
        Assert.Equal(pago, nuevos["pagoId"].GetInt64());
        Assert.Equal("DESHACER_RENOVACION", nuevos["tipo"].GetString());
        Assert.Equal("Valor mal escrito (prueba)", nuevos["motivo"].GetString());
        Assert.Equal(planB, JsonDe(deshacer.DatosAnteriores)["planId"].GetInt64());
        var anular = (await _api.Auditoria("Pago", pago.ToString())).Last();
        Assert.Equal("DESHACER_RENOVACION", JsonDe(anular.DatosNuevos)["efecto"].GetString());
        // Ya anulado: el efecto lo dice y la anulación da 409.
        Assert.Equal("YA_ANULADO", Texto(await EfectoOk(pago), "bloqueadoPor"));
        _api.Hora(T);
    }

    [Fact(DisplayName = "D-67: anular el pago de una asignación -> suscripción CANCELADA y sus publicaciones fuera del portal")]
    public async Task Asignacion()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor(Unico("Asesor Asignacion D67"));
        var plan = await _api.PlanConPrecio(100_000);
        var sus = IdDe(await Esperar(await _api.AsignarConPago(asesor.PerfilId, plan, PagoValido()), HttpStatusCode.Created));
        var pub = await _api.Publicada(asesor);
        Assert.Contains(Slug(pub), await _api.SlugsPortal());
        var pago = await UltimoPago(sus);

        _api.Hora(T.AddHours(2));
        var efecto = await EfectoOk(pago);
        Assert.Equal(("CANCELAR_ASIGNACION", "CANCELADA"), (Texto(efecto, "tipo"), Texto(efecto, "nuevoEstado")));
        Assert.Equal(T.AddHours(2), Fecha(efecto, "nuevaFinVigencia"));

        await Esperar(await _api.AnularPago(pago), HttpStatusCode.OK);
        var s = await _api.SusDb(sus);
        Assert.Equal(("CANCELADA", T.AddHours(2)), (s.Estado, s.FinVigencia!.Value));
        Assert.DoesNotContain(Slug(pub), await _api.SlugsPortal());
        _api.Hora(T);
    }

    [Fact(DisplayName = "D-67: anular el pago de un cambio de plan -> la nueva queda CANCELADA y vuelven el plan y la fecha anteriores")]
    public async Task Cambio_de_plan()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor(Unico("Asesora Cambio D67"));
        var planA = await _api.PlanConPrecio(100_000);
        var planB = await _api.PlanConPrecio(300_000, meses: 3);
        var vieja = IdDe(await _api.Asignada(asesor.PerfilId, planA));
        _api.Hora(T.AddHours(1));
        var nueva = IdDe(await _api.AccionOk(vieja, "cambiar-plan", new { planId = planB, pago = PagoValido() }));
        var pago = await UltimoPago(nueva);

        var efecto = await EfectoOk(pago);
        Assert.Equal("DESHACER_CAMBIO_PLAN", Texto(efecto, "tipo"));
        Assert.Equal((vieja, nueva), (efecto.GetProperty("suscripcionId").GetInt64(), efecto.GetProperty("suscripcionCanceladaId").GetInt64()));
        Assert.Equal(planA, efecto.GetProperty("nuevoPlan").GetProperty("id").GetInt64());
        Assert.Equal(T.AddDays(30), Fecha(efecto, "nuevaFinVigencia"));

        _api.Hora(T.AddHours(3));
        await Esperar(await _api.AnularPago(pago), HttpStatusCode.OK);
        var v = await _api.SusDb(vieja);
        var n = await _api.SusDb(nueva);
        Assert.Equal(("ACTIVA", planA, T.AddDays(30)), (v.Estado, v.PlanId, v.FinVigencia!.Value));
        Assert.Equal("CANCELADA", n.Estado);
        var mi = await Esperar(await _api.MiSuscripcion(asesor.Token), HttpStatusCode.OK);
        Assert.Equal(30, mi.GetProperty("plan").GetProperty("diasVigencia").GetInt32());
        Assert.True(mi.GetProperty("vigente").GetBoolean());
        Assert.Contains((await _api.Auditoria("Suscripcion", vieja.ToString())), e => e.Accion == "DESHACER_PAGO");
        Assert.Contains((await _api.Auditoria("Suscripcion", nueva.ToString())), e => e.Accion == "DESHACER_PAGO");
        _api.Hora(T);
    }

    [Fact(DisplayName = "D-67: un pago POSTERIOR solo se anula (efecto SOLO_PAGO)")]
    public async Task Posterior()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor(Unico("Asesor Posterior D67"));
        var sus = IdDe(await _api.Asignada(asesor.PerfilId, await _api.PlanConPrecio(100_000)));
        await Esperar(await _api.RegistrarPago(sus, PagoValido()), HttpStatusCode.Created);
        var pago = await UltimoPago(sus);
        var efecto = await EfectoOk(pago);
        Assert.Equal("SOLO_PAGO", Texto(efecto, "tipo"));
        Assert.Equal(JsonValueKind.Null, efecto.GetProperty("nuevoPlan").ValueKind);
        var antes = await _api.SusDb(sus);
        await Esperar(await _api.AnularPago(pago), HttpStatusCode.OK);
        var despues = await _api.SusDb(sus);
        Assert.Equal((antes.Estado, antes.FinVigencia, antes.PlanId), (despues.Estado, despues.FinVigencia, despues.PlanId));
        Assert.DoesNotContain(await _api.Auditoria("Suscripcion", sus.ToString()), e => e.Accion == "DESHACER_PAGO");
    }

    [Fact(DisplayName = "D-67: solo el movimiento más reciente -> 409 con mensaje claro; tras anular el posterior, el anterior se puede anular")]
    public async Task Orden()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor(Unico("Asesora Orden D67"));
        var plan = await _api.PlanConPrecio(100_000);
        var sus = IdDe(await Esperar(await _api.AsignarConPago(asesor.PerfilId, plan, PagoValido()), HttpStatusCode.Created));
        var pagoAsignacion = await UltimoPago(sus);
        _api.Hora(T.AddDays(1));
        await _api.AccionOk(sus, "renovar", new { pago = PagoValido() });
        var pagoRenovacion = await UltimoPago(sus);

        var efecto = await EfectoOk(pagoAsignacion);
        Assert.Equal(("MOVIMIENTO_POSTERIOR", MsgPosterior), (Texto(efecto, "bloqueadoPor"), Texto(efecto, "mensaje")));
        var antes = await _api.SusDb(sus);
        await Error(await _api.AnularPago(pagoAsignacion), HttpStatusCode.Conflict, MsgPosterior);
        Assert.Equal("REGISTRADO", (await _api.PagosDb(sus)).Single(p => p.Id == pagoAsignacion).Estado);
        Assert.Equal(antes.FinVigencia, (await _api.SusDb(sus)).FinVigencia);

        await Esperar(await _api.AnularPago(pagoRenovacion), HttpStatusCode.OK);
        Assert.Equal(T.AddDays(30), (await _api.SusDb(sus)).FinVigencia);
        Assert.Null(Texto(await EfectoOk(pagoAsignacion), "bloqueadoPor"));
        await Esperar(await _api.AnularPago(pagoAsignacion), HttpStatusCode.OK);
        Assert.Equal("CANCELADA", (await _api.SusDb(sus)).Estado);
        _api.Hora(T);
    }

    [Fact(DisplayName = "D-67: una renovación sin pago posterior bloquea la anulación del pago anterior")]
    public async Task Orden_renovacion_sin_pago()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor(Unico("Asesor Sin Pago D67"));
        var sus = IdDe(await Esperar(await _api.AsignarConPago(asesor.PerfilId, await _api.PlanConPrecio(100_000), PagoValido()),
            HttpStatusCode.Created));
        var pago = await UltimoPago(sus);
        await _api.AccionOk(sus, "renovar");
        await Error(await _api.AnularPago(pago), HttpStatusCode.Conflict, MsgPosterior);
    }

    [Fact(DisplayName = "D-67, D-41, D-46: si la fecha restaurada ya pasó -> vencida, fuera del portal y sus contactos a Administración")]
    public async Task Fecha_pasada()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        api.Hora(T);
        var asesor = await api.CrearAsesor("Asesora Vencida D67");
        var sus = IdDe(await api.Asignada(asesor.PerfilId, await api.PlanConPrecio(100_000)));
        var pub = await api.Publicada(asesor);

        // Vence (T+30) y se renueva con pago a los 40 días: vuelve a estar visible y recibe un contacto.
        api.Hora(T.AddDays(40));
        Assert.DoesNotContain(Slug(pub), await api.SlugsPortal());
        await api.AccionOk(sus, "renovar", new { pago = PagoValido(fechaPago: "2026-12-05") });
        Assert.Contains(Slug(pub), await api.SlugsPortal());
        var lead = await api.LeadNuevo(Slug(pub));
        Assert.Equal(asesor.PerfilId, lead.ResponsableAsesorId);
        var pago = (await api.PagosDb(sus)).Last().Id;

        api.Hora(T.AddDays(41));
        var efecto = await Esperar(await api.Enviar(HttpMethod.Get, $"/api/admin/pagos/{pago}/efecto-anulacion", api.Admin.Token),
            HttpStatusCode.OK);
        Assert.True(efecto.GetProperty("quedaraVencida").GetBoolean());
        Assert.Equal(T.AddDays(30), Fecha(efecto, "nuevaFinVigencia"));

        await Esperar(await api.AnularPago(pago), HttpStatusCode.OK);
        var s = await api.SusDb(sus);
        Assert.Equal((T, T.AddDays(30)), (s.InicioVigencia!.Value, s.FinVigencia!.Value));
        var mi = await Esperar(await api.MiSuscripcion(asesor.Token), HttpStatusCode.OK);
        Assert.Equal("VENCIDA", mi.GetProperty("estadoEfectivo").GetString());
        Assert.DoesNotContain(Slug(pub), await api.SlugsPortal());
        Assert.Null((await api.LeadDb(lead.Id)).ResponsableAsesorId);   // barrido D-46 "Plan vencido"
        var panel = await Esperar(await api.Panel(), HttpStatusCode.OK);
        Assert.Equal(1, panel.GetProperty("suscripcionesVencidas").GetInt32());
    }

    private static long IdDe(JsonElement e) => e.GetProperty("id").GetInt64();
}
