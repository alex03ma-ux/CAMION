using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;

namespace Camion.Api.Tests;

/// <summary>
/// P4p — suscripciones: asignar, HU-067 renovar (D-36), cambiar de plan, HU-068 suspender, HU-069 reactivar, cancelar y
/// HU-066 consultar la propia. Vigencia de 30 días por mes (D-35); vencida calculada al leer. Reloj fijo (TimeProvider).
/// </summary>
public sealed class SuscripcionesTests(P4pFixture fixture) : IClassFixture<P4pFixture>
{
    private readonly InventarioApi _api = fixture.Api;

    private async Task<(AsesorPrueba Asesor, long PlanId, long SusId)> Asignado(int meses = 1)
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor("Asesor Suscripción Ficticio");
        var plan = await _api.PlanId(meses: meses);
        var sus = (await _api.Asignada(asesor.PerfilId, plan)).GetProperty("id").GetInt64();
        return (asesor, plan, sus);
    }

    public static TheoryData<string, string> RutasAdmin => new()
    {
        { "GET", "/api/admin/suscripciones" },
        { "GET", "/api/admin/asesores/1/suscripcion" },
        { "POST", "/api/admin/asesores/1/suscripcion" },
        { "POST", "/api/admin/suscripciones/1/renovar" },
        { "POST", "/api/admin/suscripciones/1/cambiar-plan" },
        { "POST", "/api/admin/suscripciones/1/suspender" },
        { "POST", "/api/admin/suscripciones/1/reactivar" },
        { "POST", "/api/admin/suscripciones/1/cancelar" },
    };

    [Theory(DisplayName = "RN-SE-006: gestión de suscripciones solo para el administrador (401 sin token; 403 asesor y comprador)")]
    [MemberData(nameof(RutasAdmin))]
    public async Task Permisos_admin(string metodo, string ruta)
    {
        var m = new HttpMethod(metodo);
        object? cuerpo = metodo == "GET" ? null : new { planId = 1, motivo = MotivoValido };
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Enviar(m, ruta, null, cuerpo)).StatusCode);
        await Error(await _api.Enviar(m, ruta, _api.A.Token, cuerpo), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.Enviar(m, ruta, _api.TokenComprador, cuerpo), HttpStatusCode.Forbidden, MsgSinPermiso);
    }

    [Fact(DisplayName = "HU-066: GET /api/asesor/suscripcion es SoloAsesor (401 sin token; 403 administrador y comprador)")]
    public async Task Permisos_mi_suscripcion()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.MiSuscripcion(null)).StatusCode);
        await Error(await _api.MiSuscripcion(_api.Admin.Token), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.MiSuscripcion(_api.TokenComprador), HttpStatusCode.Forbidden, MsgSinPermiso);
    }

    [Fact(DisplayName = "Asignar -> 201 ACTIVA desde ahora por 30 x meses, con aprobado_por/en y auditoría ASIGNAR")]
    public async Task Asignar_201()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor("Asesora Asignar Ficticia");
        var plan = await _api.PlanId(meses: 3, limite: 7);
        var json = await _api.Asignada(asesor.PerfilId, plan);
        Assert.Equal("ACTIVA", json.GetProperty("estado").GetString());
        Assert.Equal("ACTIVA", json.GetProperty("estadoEfectivo").GetString());
        Assert.Equal(T, Fecha(json, "inicioVigencia"));
        Assert.Equal(T.AddDays(90), Fecha(json, "finVigencia"));
        Assert.Equal(90, json.GetProperty("diasRestantes").GetInt32());
        Assert.True(json.GetProperty("vigente").GetBoolean());
        Assert.Equal(T, Fecha(json, "aprobadoEn"));
        Assert.Equal(asesor.PerfilId, json.GetProperty("asesor").GetProperty("id").GetInt64());
        Assert.Equal(asesor.Nombre, json.GetProperty("asesor").GetProperty("nombre").GetString());
        Assert.Equal(plan, json.GetProperty("plan").GetProperty("id").GetInt64());
        Assert.Equal(7, json.GetProperty("plan").GetProperty("limitePublicaciones").GetInt32());
        Assert.Equal(90, json.GetProperty("plan").GetProperty("diasVigencia").GetInt32());

        var id = json.GetProperty("id").GetInt64();
        var db = await _api.SusDb(id);
        Assert.Equal(_api.Admin.Id, db.AprobadoPor);
        var audit = Assert.Single(await _api.Auditoria("Suscripcion", id.ToString()));
        Assert.Equal("ASIGNAR", audit.Accion);
        Assert.Equal(_api.Admin.Id, audit.UsuarioId);
        Assert.Equal("ACTIVA", JsonDe(audit.DatosNuevos)["estado"].GetString());
        Assert.Equal(plan, JsonDe(audit.DatosNuevos)["planId"].GetInt64());
    }

    [Fact(DisplayName = "Una suscripción viva por asesor: asignar otra -> 409 (también si está suspendida o vencida sin renovar)")]
    public async Task Una_viva_409()
    {
        var (asesor, plan, sus) = await Asignado();
        await Error(await _api.Asignar(asesor.PerfilId, plan), HttpStatusCode.Conflict, MsgYaTieneViva);
        await _api.AccionOk(sus, "suspender", new { motivo = MotivoValido });
        await Error(await _api.Asignar(asesor.PerfilId, plan), HttpStatusCode.Conflict, MsgYaTieneViva);
        await _api.AccionOk(sus, "reactivar", new { motivo = (string?)null });
        _api.Hora(T.AddDays(45)); // vencida (estadoEfectivo) pero la columna sigue REACTIVADA: viva
        await Error(await _api.Asignar(asesor.PerfilId, plan), HttpStatusCode.Conflict, MsgYaTieneViva);
    }

    [Fact(DisplayName = "Asignar: planId falta/no existe/inactivo -> 400; asesor inexistente 404; asesor no ACTIVA 409")]
    public async Task Asignar_validaciones()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor("Asesor Validaciones Ficticio");
        await Error400(await _api.Asignar(asesor.PerfilId, null), "planId", MsgPlan);
        await Error400(await _api.Asignar(asesor.PerfilId, 999_999), "planId", MsgPlanActivo);
        var inactivo = await _api.PlanId();
        await Esperar(await _api.Enviar(HttpMethod.Post, $"/api/admin/planes/{inactivo}/inactivar", _api.Admin.Token),
            HttpStatusCode.OK);
        await Error400(await _api.Asignar(asesor.PerfilId, inactivo), "planId", MsgPlanActivo);

        var plan = await _api.PlanId();
        await Error(await _api.Asignar(999_999, plan), HttpStatusCode.NotFound, MsgNoEncontrado);
        await Error(await _api.Asignar(_api.Pendiente.PerfilId, plan), HttpStatusCode.Conflict, MsgAsesorNoActivo);
        Assert.Empty(await _api.Leer(db => Task.FromResult(db.Suscripciones.Where(s => s.AsesorId == asesor.PerfilId).ToList())));
    }

    [Fact(DisplayName = "HU-066: el asesor ve solo su suscripción; sin suscripción -> 200 { suscripcion: null, mensaje }")]
    public async Task Mi_suscripcion()
    {
        var (asesor, plan, _) = await Asignado(meses: 2);
        _api.Hora(T.AddDays(10).AddHours(1));
        var json = await Esperar(await _api.MiSuscripcion(asesor.Token), HttpStatusCode.OK);
        Assert.Equal(new[] { "diasRestantes", "estado", "estadoEfectivo", "finVigencia", "inicioVigencia", "plan", "vigente" },
            json.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "diasVigencia", "limitePublicaciones", "nombre" },
            json.GetProperty("plan").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(60, json.GetProperty("plan").GetProperty("diasVigencia").GetInt32());
        Assert.Equal(T.AddDays(60), Fecha(json, "finVigencia"));
        Assert.Equal(50, json.GetProperty("diasRestantes").GetInt32()); // 49 días y 23 horas -> 50
        Assert.True(json.GetProperty("vigente").GetBoolean());

        var otro = await _api.CrearAsesor("Asesor Sin Plan Ficticio");
        var vacio = await Esperar(await _api.MiSuscripcion(otro.Token), HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, vacio.GetProperty("suscripcion").ValueKind);
        Assert.Equal(MsgSinSuscripcion, vacio.GetProperty("mensaje").GetString());
        Assert.DoesNotContain(plan.ToString(), vacio.ToString());
    }

    [Fact(DisplayName = "HU-067 (D-36): renovar antes de vencer -> fin = fin + días; con otro plan usa sus días; auditoría RENOVAR")]
    public async Task Renovar_antes_de_vencer()
    {
        var (_, _, sus) = await Asignado(meses: 1);
        _api.Hora(T.AddDays(10));
        var json = await _api.AccionOk(sus, "renovar");
        Assert.Equal(T, Fecha(json, "inicioVigencia"));
        Assert.Equal(T.AddDays(60), Fecha(json, "finVigencia"));
        Assert.Equal("ACTIVA", json.GetProperty("estado").GetString());

        var trimestral = await _api.PlanId(meses: 3);
        json = await _api.AccionOk(sus, "renovar", new { planId = trimestral });
        Assert.Equal(T.AddDays(150), Fecha(json, "finVigencia"));
        Assert.Equal(trimestral, json.GetProperty("plan").GetProperty("id").GetInt64());

        var audit = (await _api.Auditoria("Suscripcion", sus.ToString())).Where(a => a.Accion == "RENOVAR").ToList();
        Assert.Equal(2, audit.Count);
        Assert.Equal(T.AddDays(30), JsonDe(audit[0].DatosAnteriores)["finVigencia"].GetDateTime().ToUniversalTime());
        Assert.Equal(T.AddDays(60), JsonDe(audit[0].DatosNuevos)["finVigencia"].GetDateTime().ToUniversalTime());
    }

    [Fact(DisplayName = "HU-067: vencida (calculada) -> estadoEfectivo VENCIDA; renovar la vuelve ACTIVA desde ahora")]
    public async Task Renovar_despues_de_vencer()
    {
        var (asesor, _, sus) = await Asignado(meses: 1);
        _api.Hora(T.AddDays(30)); // fin exacto: ya no vigente
        var mia = await Esperar(await _api.MiSuscripcion(asesor.Token), HttpStatusCode.OK);
        Assert.Equal("ACTIVA", mia.GetProperty("estado").GetString());
        Assert.Equal("VENCIDA", mia.GetProperty("estadoEfectivo").GetString());
        Assert.False(mia.GetProperty("vigente").GetBoolean());
        Assert.Equal(0, mia.GetProperty("diasRestantes").GetInt32());

        var ahora = T.AddDays(40);
        _api.Hora(ahora);
        var json = await _api.AccionOk(sus, "renovar");
        Assert.Equal("ACTIVA", json.GetProperty("estadoEfectivo").GetString());
        Assert.Equal(ahora, Fecha(json, "inicioVigencia"));
        Assert.Equal(ahora.AddDays(30), Fecha(json, "finVigencia"));
        Assert.True(json.GetProperty("vigente").GetBoolean());
    }

    [Fact(DisplayName = "HU-067: renovar una vencida que estaba REACTIVADA la deja ACTIVA")]
    public async Task Renovar_reactivada_vencida()
    {
        var (_, _, sus) = await Asignado();
        await _api.AccionOk(sus, "suspender", new { motivo = MotivoValido });
        await _api.AccionOk(sus, "reactivar");
        _api.Hora(T.AddDays(31));
        var json = await _api.AccionOk(sus, "renovar");
        Assert.Equal("ACTIVA", json.GetProperty("estado").GetString());
        Assert.Equal(T.AddDays(61), Fecha(json, "finVigencia"));
    }

    [Fact(DisplayName = "Renovar: SUSPENDIDA o CANCELADA -> 409; plan inactivo (el actual por defecto o el pedido) -> 400")]
    public async Task Renovar_invalidos()
    {
        var (_, plan, sus) = await Asignado();
        await Esperar(await _api.Enviar(HttpMethod.Post, $"/api/admin/planes/{plan}/inactivar", _api.Admin.Token), HttpStatusCode.OK);
        await Error400(await _api.AccionSus(sus, "renovar"), "planId", MsgPlanActivo);
        var otroInactivo = await _api.PlanId();
        await Esperar(await _api.Enviar(HttpMethod.Post, $"/api/admin/planes/{otroInactivo}/inactivar", _api.Admin.Token), HttpStatusCode.OK);
        await Error400(await _api.AccionSus(sus, "renovar", new { planId = otroInactivo }), "planId", MsgPlanActivo);

        await _api.AccionOk(sus, "suspender", new { motivo = MotivoValido });
        await Error(await _api.AccionSus(sus, "renovar", new { planId = await _api.PlanId() }), HttpStatusCode.Conflict,
            "No se puede renovar una suscripción en estado suspendida.");
        await _api.AccionOk(sus, "cancelar", new { motivo = MotivoValido });
        await Error(await _api.AccionSus(sus, "renovar", new { planId = await _api.PlanId() }), HttpStatusCode.Conflict,
            "No se puede renovar una suscripción en estado cancelada.");
    }

    [Fact(DisplayName = "Cambiar de plan: la actual pasa a CANCELADA con fin = ahora y se crea otra ACTIVA, con auditoría en ambas")]
    public async Task Cambiar_plan()
    {
        var (asesor, _, sus) = await Asignado(meses: 1);
        var ahora = T.AddDays(5);
        _api.Hora(ahora);
        var nuevoPlan = await _api.PlanId(meses: 2, limite: 20);
        var json = await _api.AccionOk(sus, "cambiar-plan", new { planId = nuevoPlan });
        var nueva = json.GetProperty("id").GetInt64();
        Assert.NotEqual(sus, nueva);
        Assert.Equal("ACTIVA", json.GetProperty("estado").GetString());
        Assert.Equal(nuevoPlan, json.GetProperty("plan").GetProperty("id").GetInt64());
        Assert.Equal(ahora, Fecha(json, "inicioVigencia"));
        Assert.Equal(ahora.AddDays(60), Fecha(json, "finVigencia"));

        var vieja = await _api.SusDb(sus);
        Assert.Equal("CANCELADA", vieja.Estado);
        Assert.Equal(ahora, vieja.FinVigencia);
        Assert.Equal("CAMBIAR_PLAN", Assert.Single(await _api.Auditoria("Suscripcion", sus.ToString()), a => a.Accion != "ASIGNAR").Accion);
        Assert.Equal("CAMBIAR_PLAN", Assert.Single(await _api.Auditoria("Suscripcion", nueva.ToString())).Accion);

        var mia = await Esperar(await _api.MiSuscripcion(asesor.Token), HttpStatusCode.OK);
        Assert.Equal(20, mia.GetProperty("plan").GetProperty("limitePublicaciones").GetInt32());

        await Error(await _api.AccionSus(sus, "cambiar-plan", new { planId = nuevoPlan }), HttpStatusCode.Conflict,
            "No se puede cambiar el plan de una suscripción en estado cancelada.");
        await Error400(await _api.AccionSus(nueva, "cambiar-plan", new { }), "planId", MsgPlan);
    }

    [Fact(DisplayName = "HU-068/HU-069: suspender (motivo) -> SUSPENDIDA no vigente; reactivar -> REACTIVADA sin extender el fin")]
    public async Task Suspender_reactivar()
    {
        var (asesor, _, sus) = await Asignado();
        await Error400(await _api.AccionSus(sus, "suspender", new { motivo = "" }), "motivo", MsgMotivo);
        await Error400(await _api.AccionSus(sus, "suspender", new { motivo = new string('x', 501) }), "motivo", MsgMotivo);

        _api.Hora(T.AddDays(3));
        var json = await _api.AccionOk(sus, "suspender", new { motivo = MotivoValido });
        Assert.Equal("SUSPENDIDA", json.GetProperty("estado").GetString());
        Assert.Equal("SUSPENDIDA", json.GetProperty("estadoEfectivo").GetString());
        Assert.False(json.GetProperty("vigente").GetBoolean());
        Assert.False((await Esperar(await _api.MiSuscripcion(asesor.Token), HttpStatusCode.OK)).GetProperty("vigente").GetBoolean());
        await Error(await _api.AccionSus(sus, "suspender", new { motivo = MotivoValido }), HttpStatusCode.Conflict,
            "No se puede suspender una suscripción en estado suspendida.");

        _api.Hora(T.AddDays(10));
        json = await _api.AccionOk(sus, "reactivar");
        Assert.Equal("REACTIVADA", json.GetProperty("estado").GetString());
        Assert.Equal(T.AddDays(30), Fecha(json, "finVigencia"));
        Assert.True(json.GetProperty("vigente").GetBoolean());
        await Error(await _api.AccionSus(sus, "reactivar"), HttpStatusCode.Conflict,
            "No se puede reactivar una suscripción en estado reactivada.");
        // Una REACTIVADA se puede volver a suspender.
        await _api.AccionOk(sus, "suspender", new { motivo = MotivoValido });

        var audit = await _api.Auditoria("Suscripcion", sus.ToString());
        Assert.Equal(new[] { "ASIGNAR", "SUSPENDER", "REACTIVAR", "SUSPENDER" }, audit.Select(a => a.Accion).ToArray());
        Assert.Equal(MotivoValido, JsonDe(audit[1].DatosNuevos)["motivo"].GetString());
        Assert.Equal("ACTIVA", JsonDe(audit[1].DatosAnteriores)["estado"].GetString());
        Assert.Equal("SUSPENDIDA", JsonDe(audit[1].DatosNuevos)["estado"].GetString());
    }

    [Fact(DisplayName = "Cancelar (motivo) -> CANCELADA con fin = ahora; luego se puede asignar otra; cancelar otra vez -> 409")]
    public async Task Cancelar()
    {
        var (asesor, plan, sus) = await Asignado();
        await Error400(await _api.AccionSus(sus, "cancelar", new { motivo = "abc" }), "motivo", MsgMotivo);
        var ahora = T.AddDays(7);
        _api.Hora(ahora);
        var json = await _api.AccionOk(sus, "cancelar", new { motivo = MotivoValido });
        Assert.Equal("CANCELADA", json.GetProperty("estado").GetString());
        Assert.Equal(ahora, Fecha(json, "finVigencia"));
        Assert.Equal(0, json.GetProperty("diasRestantes").GetInt32());
        await Error(await _api.AccionSus(sus, "cancelar", new { motivo = MotivoValido }), HttpStatusCode.Conflict,
            "No se puede cancelar una suscripción en estado cancelada.");
        await Error(await _api.AccionSus(sus, "suspender", new { motivo = MotivoValido }), HttpStatusCode.Conflict,
            "No se puede suspender una suscripción en estado cancelada.");
        await Error(await _api.AccionSus(sus, "reactivar"), HttpStatusCode.Conflict,
            "No se puede reactivar una suscripción en estado cancelada.");

        var vacio = await Esperar(await _api.MiSuscripcion(asesor.Token), HttpStatusCode.OK);
        Assert.Equal(MsgSinSuscripcion, vacio.GetProperty("mensaje").GetString());
        await _api.Asignada(asesor.PerfilId, plan);
    }

    [Fact(DisplayName = "CK_Sus_Fechas: cancelar en el mismo instante en que empezó no rompe la restricción fin > inicio")]
    public async Task Cancelar_en_el_mismo_instante()
    {
        var (_, _, sus) = await Asignado();
        var json = await _api.AccionOk(sus, "cancelar", new { motivo = MotivoValido });
        Assert.True(Fecha(json, "finVigencia") > Fecha(json, "inicioVigencia"));
    }

    [Fact(DisplayName = "Acciones sobre una suscripción que no existe -> 404 'No encontrado.'")]
    public async Task No_existe_404()
    {
        var plan = await _api.PlanId();
        foreach (var accion in new[] { "renovar", "cambiar-plan", "suspender", "reactivar", "cancelar" })
            await Error(await _api.AccionSus(999_999, accion, new { planId = plan, motivo = MotivoValido }),
                HttpStatusCode.NotFound, MsgNoEncontrado);
    }

    [Fact(DisplayName = "GET /api/admin/asesores/{id}/suscripcion: la viva, si no la última; 404 si nunca tuvo")]
    public async Task Suscripcion_del_asesor()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor("Asesor Consulta Ficticio");
        await Error(await _api.Enviar(HttpMethod.Get, $"/api/admin/asesores/{asesor.PerfilId}/suscripcion", _api.Admin.Token),
            HttpStatusCode.NotFound, MsgNoEncontrado);
        var plan = await _api.PlanId();
        var primera = (await _api.Asignada(asesor.PerfilId, plan)).GetProperty("id").GetInt64();
        await _api.AccionOk(primera, "cancelar", new { motivo = MotivoValido });
        var json = await Esperar(await _api.Enviar(HttpMethod.Get, $"/api/admin/asesores/{asesor.PerfilId}/suscripcion",
            _api.Admin.Token), HttpStatusCode.OK);
        Assert.Equal(primera, json.GetProperty("id").GetInt64());

        _api.Hora(T.AddHours(1));
        var segunda = (await _api.Asignada(asesor.PerfilId, plan)).GetProperty("id").GetInt64();
        json = await Esperar(await _api.Enviar(HttpMethod.Get, $"/api/admin/asesores/{asesor.PerfilId}/suscripcion",
            _api.Admin.Token), HttpStatusCode.OK);
        Assert.Equal(segunda, json.GetProperty("id").GetInt64());
    }

    [Fact(DisplayName = "Lista: filtros asesorId y estado efectivo (VENCIDA / ACTIVA), paginación y 400 en filtros inválidos")]
    public async Task Lista()
    {
        var (asesor, _, sus) = await Asignado();
        string Url(string q) => $"/api/admin/suscripciones?asesorId={asesor.PerfilId}{q}";
        var json = await Esperar(await _api.Enviar(HttpMethod.Get, Url(""), _api.Admin.Token), HttpStatusCode.OK);
        Assert.Equal(1, json.GetProperty("total").GetInt32());
        Assert.Equal(sus, json.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(1, json.GetProperty("pagina").GetInt32());
        Assert.Equal(20, json.GetProperty("tamano").GetInt32());

        _api.Hora(T.AddDays(31));
        Assert.Equal(0, (await Esperar(await _api.Enviar(HttpMethod.Get, Url("&estado=activa"), _api.Admin.Token),
            HttpStatusCode.OK)).GetProperty("total").GetInt32());
        var vencidas = await Esperar(await _api.Enviar(HttpMethod.Get, Url("&estado=VENCIDA"), _api.Admin.Token), HttpStatusCode.OK);
        Assert.Equal(1, vencidas.GetProperty("total").GetInt32());
        Assert.Equal("VENCIDA", vencidas.GetProperty("items")[0].GetProperty("estadoEfectivo").GetString());
        Assert.Equal("ACTIVA", vencidas.GetProperty("items")[0].GetProperty("estado").GetString());

        await Error400(await _api.Enviar(HttpMethod.Get, "/api/admin/suscripciones?estado=OTRO", _api.Admin.Token), "estado",
            "Estado no válido.", MsgFiltros);
        await Error400(await _api.Enviar(HttpMethod.Get, "/api/admin/suscripciones?pagina=0", _api.Admin.Token), "pagina",
            "Página o tamaño no válidos.", MsgFiltros);
        await Error400(await _api.Enviar(HttpMethod.Get, "/api/admin/suscripciones?tamano=101", _api.Admin.Token), "tamano",
            "Página o tamaño no válidos.", MsgFiltros);
        await Error400(await _api.Enviar(HttpMethod.Get, "/api/admin/suscripciones?asesorId=x", _api.Admin.Token), "asesorId",
            "Asesor no válido.", MsgFiltros);
    }
}
