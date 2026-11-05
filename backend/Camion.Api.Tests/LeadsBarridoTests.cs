using System.Net;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;

namespace Camion.Api.Tests;

/// <summary>
/// P5 — D-46 (B): los leads siguen al plan. Plan vencido o asesor bloqueado -> Administración (SISTEMA, auditado); no
/// vuelven solos; vehículo reasignado -> sus leads abiertos lo siguen (D-34). Barrido bajo demanda e idempotente.
/// SQLite en memoria y reloj fijo; nunca MariaDB. Datos ficticios.
/// </summary>
public sealed class LeadsBarridoTests(P5Fixture fixture) : IClassFixture<P5Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    [Fact(DisplayName = "CA-P5-50: al vencer el plan, la siguiente consulta pasa el lead ABIERTO a Administración (SISTEMA, motivo 'Plan vencido'), una sola vez; el cerrado no cambia")]
    public async Task Plan_vencido()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Plan Vence");
        var abierto = await _api.LeadNuevo(slug);
        var cerrado = await _api.LeadNuevo(slug);
        await Esperar(await _api.AccionLead(asesor.Token, cerrado.Id, "cerrar", new { resultado = "NO_RESPONDIO" }), HttpStatusCode.OK);
        Assert.Contains(abierto.Id, await _api.IdsLeads(asesor.Token));

        _api.Hora(T.AddDays(31));
        Assert.Equal(asesor.PerfilId, (await _api.LeadDb(abierto.Id)).ResponsableAsesorId); // aún no se consultó
        Assert.DoesNotContain(abierto.Id, await _api.IdsLeads(asesor.Token));
        Assert.Contains(cerrado.Id, await _api.IdsLeads(asesor.Token));
        Assert.Null((await _api.LeadDb(abierto.Id)).ResponsableAsesorId);
        Assert.Equal(asesor.PerfilId, (await _api.LeadDb(cerrado.Id)).ResponsableAsesorId);

        var ev = Assert.Single(await _api.EventosDb(abierto.Id), e => e.Tipo == EventoLead.Reasignado);
        Assert.Null(ev.ActorUsuarioId);
        Assert.True(ev.Automatico);
        Assert.Equal("Plan vencido", ev.Texto);
        Assert.Equal(asesor.PerfilId, ev.ResponsableAnteriorAsesorId);
        Assert.Null(ev.ResponsableNuevoAsesorId);
        Assert.Equal(T.AddDays(31), ev.CreadoEn);
        var audit = Assert.Single(await _api.AuditoriaLead(abierto.Id), a => a.Accion == "REASIGNAR");
        Assert.Null(audit.UsuarioId);
        Assert.True(JsonDe(audit.DatosNuevos)["automatico"].GetBoolean());

        // Idempotente: más consultas no repiten el evento.
        await _api.IdsLeads(_api.Admin.Token);
        await _api.Resumen(_api.Admin.Token);
        Assert.Single(await _api.EventosDb(abierto.Id), e => e.Tipo == EventoLead.Reasignado);
        var detalle = await Esperar(await _api.DetalleLead(_api.Admin.Token, abierto.Id), HttpStatusCode.OK);
        Assert.Equal("ADMINISTRACION", detalle.GetProperty("responsable").GetProperty("tipo").GetString());
        var ultimo = detalle.GetProperty("eventos").EnumerateArray().Last();
        Assert.Equal("SISTEMA", ultimo.GetProperty("actor").GetProperty("tipo").GetString());
    }

    [Fact(DisplayName = "CA-P5-51: bloquear al asesor pasa sus leads abiertos a Administración en la misma acción (SISTEMA, 'Asesor bloqueado')")]
    public async Task Asesor_bloqueado()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Se Bloquea");
        var lead = await _api.LeadNuevo(slug);
        Assert.Equal(HttpStatusCode.OK, (await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, asesor.UsuarioId, "bloquear",
            "Incumplimiento (prueba)")).StatusCode);
        // Sin consultar /api/leads: el bloqueo ya lo movió.
        Assert.Null((await _api.LeadDb(lead.Id)).ResponsableAsesorId);
        var ev = Assert.Single(await _api.EventosDb(lead.Id), e => e.Tipo == EventoLead.Reasignado);
        Assert.Equal("Asesor bloqueado", ev.Texto);
        Assert.Null(ev.ActorUsuarioId);
        Assert.Single(await _api.AuditoriaLead(lead.Id), a => a.Accion == "REASIGNAR" && a.UsuarioId == null);
        await _api.IdsLeads(_api.Admin.Token);
        Assert.Single(await _api.EventosDb(lead.Id), e => e.Tipo == EventoLead.Reasignado);
    }

    [Fact(DisplayName = "CA-P5-53: al renovar el plan o desbloquear al asesor los leads no vuelven solos; el administrador puede reasignarlos")]
    public async Task No_vuelven_solos()
    {
        _api.Hora(T);
        var (asesor, sus) = await _api.AsesorConPlan("Asesor Vuelve");
        var pub = await _api.Publicada(asesor);
        var porPlan = await _api.LeadNuevo(Slug(pub));
        _api.Hora(T.AddDays(31));
        await _api.IdsLeads(_api.Admin.Token);
        Assert.Null((await _api.LeadDb(porPlan.Id)).ResponsableAsesorId);
        await _api.AccionOk(sus, "renovar");
        Assert.Empty(await _api.IdsLeads(asesor.Token));
        Assert.Null((await _api.LeadDb(porPlan.Id)).ResponsableAsesorId);

        var (otro, _, slugOtro) = await _api.AsesorPublicado("Asesor Desbloqueado");
        _api.Hora(T.AddDays(31));
        var porBloqueo = await _api.LeadNuevo(slugOtro);
        Assert.Equal(HttpStatusCode.OK, (await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, otro.UsuarioId, "bloquear",
            "Incumplimiento (prueba)")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, otro.UsuarioId, "desbloquear", null)).StatusCode);
        await _api.IdsLeads(_api.Admin.Token);
        Assert.Null((await _api.LeadDb(porBloqueo.Id)).ResponsableAsesorId);

        await Esperar(await _api.ReasignarLead(_api.Admin.Token, porPlan.Id, asesor.PerfilId), HttpStatusCode.OK);
        Assert.Contains(porPlan.Id, await _api.IdsLeads(asesor.Token));
    }

    [Fact(DisplayName = "CA-P5-47 frente a D-46 B: reasignar a un asesor sin plan se permite y el barrido por plan no lo deshace; bloquearlo sí")]
    public async Task Manual_sin_plan()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Origen Manual");
        var sinPlan = await _api.CrearAsesor("Asesor Sin Plan Manual");
        var lead = await _api.LeadNuevo(slug);
        await Esperar(await _api.ReasignarLead(_api.Admin.Token, lead.Id, sinPlan.PerfilId), HttpStatusCode.OK);
        Assert.Contains(lead.Id, await _api.IdsLeads(sinPlan.Token));
        _api.Hora(T.AddDays(60));
        Assert.Contains(lead.Id, await _api.IdsLeads(sinPlan.Token));
        Assert.Equal(sinPlan.PerfilId, (await _api.LeadDb(lead.Id)).ResponsableAsesorId);

        Assert.Equal(HttpStatusCode.OK, (await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, sinPlan.UsuarioId, "bloquear",
            "Incumplimiento (prueba)")).StatusCode);
        Assert.Null((await _api.LeadDb(lead.Id)).ResponsableAsesorId);
    }

    [Fact(DisplayName = "CA-P5-52 (D-34): al reasignar el vehículo, los leads abiertos del dueño anterior lo siguen con su entrada auditada; los cerrados y los de Administración no cambian")]
    public async Task Siguen_al_vehiculo()
    {
        _api.Hora(T);
        var (origen, pub, slug) = await _api.AsesorPublicado("Asesor Vehiculo Origen");
        var (destino, _) = await _api.AsesorConPlan("Asesor Vehiculo Destino");
        var abierto = await _api.LeadNuevo(slug);
        var cerrado = await _api.LeadNuevo(slug);
        await Esperar(await _api.AccionLead(origen.Token, cerrado.Id, "cerrar", new { resultado = "NO_INTERESADO" }), HttpStatusCode.OK);
        var enAdmin = await _api.LeadNuevo(slug);
        await Esperar(await _api.ReasignarLead(_api.Admin.Token, enAdmin.Id, null, administracion: true), HttpStatusCode.OK);

        _api.Hora(T.AddHours(1));
        await Esperar(await _api.Reasignar(_api.Admin.Token, pub.GetProperty("vehiculoId").GetInt64(), destino.PerfilId), HttpStatusCode.OK);

        Assert.Equal(destino.PerfilId, (await _api.LeadDb(abierto.Id)).ResponsableAsesorId);
        Assert.Equal(origen.PerfilId, (await _api.LeadDb(cerrado.Id)).ResponsableAsesorId);
        Assert.Null((await _api.LeadDb(enAdmin.Id)).ResponsableAsesorId);
        var ev = Assert.Single(await _api.EventosDb(abierto.Id), e => e.Tipo == EventoLead.Reasignado);
        Assert.Equal(_api.Admin.Id, ev.ActorUsuarioId);
        Assert.True(ev.Automatico);
        Assert.Equal(origen.PerfilId, ev.ResponsableAnteriorAsesorId);
        Assert.Equal(destino.PerfilId, ev.ResponsableNuevoAsesorId);
        Assert.Equal("El vehículo se reasignó a otro asesor.", ev.Texto);
        var audit = Assert.Single(await _api.AuditoriaLead(abierto.Id), a => a.Accion == "REASIGNAR");
        Assert.Equal(_api.Admin.Id, audit.UsuarioId);
        Assert.Equal(T.AddHours(1), audit.CreadoEn);
        Assert.Contains(abierto.Id, await _api.IdsLeads(destino.Token));
        Assert.DoesNotContain(abierto.Id, await _api.IdsLeads(origen.Token));
        Assert.Equal(1, JsonDe(Assert.Single(await _api.AuditoriaVehiculo(pub.GetProperty("vehiculoId").GetInt64()),
            a => a.Accion == "REASIGNAR").DatosNuevos)["leadsMovidos"].GetInt32());
        // Un contacto nuevo después de la reasignación va al nuevo dueño.
        Assert.Equal(destino.PerfilId, (await _api.LeadNuevo(slug)).ResponsableAsesorId);
    }
}
