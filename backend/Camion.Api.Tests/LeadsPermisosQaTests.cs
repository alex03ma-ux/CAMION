using System.Net;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;

namespace Camion.Api.Tests;

/// <summary>
/// QA P5 — matriz de permisos de /api/leads que faltaba (RN-SE-006, D-3, D-7, D-10): asesor rechazado, asesor sin perfil,
/// asesor ajeno en las 7 rutas (sin efectos), asesor frente a un lead de Administración y administrador gestionando el
/// lead de un asesor. SQLite en memoria y reloj fijo; nunca MariaDB. Datos ficticios.
/// </summary>
public sealed class LeadsPermisosQaTests(P5Fixture fixture) : IClassFixture<P5Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    [Fact(DisplayName = "CA-P5-34: un asesor Rechazado (aunque conserve el rol en el token) no entra a /api/leads: 401")]
    public async Task Asesor_rechazado_sin_acceso()
    {
        _api.Hora(T);
        var (id, _, _) = await AyudasP2.CrearAsesorPendiente(_api.C, "Asesor Rechazado Ficticio");
        Assert.Equal(HttpStatusCode.OK,
            (await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, id, "rechazar", "Documentos incompletos (prueba)")).StatusCode);
        var usuario = await AyudasP2.Usuario(_api.F, id);
        Assert.NotEqual("ACTIVA", usuario.EstadoCuenta);
        var token = AyudasP2.Firmar(_api.F, id, usuario.SessionVersion, "ASESOR");
        foreach (var url in new[] { "/api/leads", "/api/leads/resumen", "/api/leads/1" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Enviar(HttpMethod.Get, url, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.AccionLead(token, 1, "notas", new { texto = "Intento (prueba)" })).StatusCode);
    }

    [Fact(DisplayName = "contrato P5: un usuario con rol ASESOR sin perfil de asesor -> 403 en lista, resumen, detalle y acciones")]
    public async Task Asesor_sin_perfil_403()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Con Perfil Ficticio");
        var lead = await _api.LeadNuevo(slug);
        var (compradorId, _) = await AyudasP2.CrearComprador(_api.C, "Comprador Sin Perfil");
        var usuario = await AyudasP2.Usuario(_api.F, compradorId);
        var token = AyudasP2.Firmar(_api.F, compradorId, usuario.SessionVersion, "ASESOR");
        foreach (var url in new[] { "/api/leads", "/api/leads/resumen", $"/api/leads/{lead.Id}" })
            await Error(await _api.Enviar(HttpMethod.Get, url, token), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.AccionLead(token, lead.Id, "notas", new { texto = "Intento sin perfil (prueba)" }),
            HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.AccionLead(token, lead.Id, "cerrar", new { resultado = "NO_RESPONDIO" }),
            HttpStatusCode.Forbidden, MsgSinPermiso);
        var db = await _api.LeadDb(lead.Id);
        Assert.Equal(Lead.Abierto, db.Estado);
        Assert.Equal(asesor.PerfilId, db.ResponsableAsesorId);
    }

    [Fact(DisplayName = "CA-P5-29, CA-P5-30, CA-P5-45: asesor ajeno en las 7 rutas: no lo lista ni lo cuenta, detalle/nota/cerrar -> 404, reabrir/reasignar -> 403; nada cambia")]
    public async Task Asesor_ajeno_en_todas_las_rutas()
    {
        _api.Hora(T);
        var (duenio, pub, slug) = await _api.AsesorPublicado("Asesora Duenia Matriz");
        var (ajeno, _, _) = await _api.AsesorPublicado("Asesor Ajeno Matriz");
        var lead = await _api.LeadNuevo(slug);
        var eventosAntes = (await _api.EventosDb(lead.Id)).Count;

        Assert.DoesNotContain(lead.Id, await _api.IdsLeads(ajeno.Token, "?tamano=100"));
        Assert.Empty(await _api.IdsLeads(ajeno.Token, "?publicacionId=" + IdDe(pub)));
        Assert.Empty(await _api.IdsLeads(ajeno.Token, $"?responsable={duenio.PerfilId}")); // el filtro se ignora para el asesor
        var resumen = await _api.Resumen(ajeno.Token);
        Assert.Equal(0, resumen.GetProperty("nuevos").GetInt32());
        Assert.Equal(0, resumen.GetProperty("abiertos").GetInt32());

        await Error(await _api.DetalleLead(ajeno.Token, lead.Id), HttpStatusCode.NotFound, MsgLeadNoEncontrado);
        await Error(await _api.AccionLead(ajeno.Token, lead.Id, "notas", new { texto = "Nota ajena (prueba)" }),
            HttpStatusCode.NotFound, MsgLeadNoEncontrado);
        await Error(await _api.AccionLead(ajeno.Token, lead.Id, "cerrar", new { resultado = "NO_INTERESADO" }),
            HttpStatusCode.NotFound, MsgLeadNoEncontrado);
        await Error(await _api.AccionLead(ajeno.Token, lead.Id, "reabrir", new { motivo = MotivoP5 }),
            HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.ReasignarLead(ajeno.Token, lead.Id, ajeno.PerfilId), HttpStatusCode.Forbidden, MsgSinPermiso);

        var db = await _api.LeadDb(lead.Id);
        Assert.Equal(Lead.Abierto, db.Estado);
        Assert.Equal(duenio.PerfilId, db.ResponsableAsesorId);
        Assert.False(db.VistoPorResponsable); // el detalle ajeno no lo marca como visto
        Assert.Equal(eventosAntes, (await _api.EventosDb(lead.Id)).Count);
        Assert.Contains(lead.Id, await _api.IdsLeads(duenio.Token));
    }

    [Fact(DisplayName = "CA-P5-30: el asesor dueño del vehículo no puede anotar ni cerrar un lead que está en Administración (404)")]
    public async Task Lead_de_administracion_sin_acciones_del_asesor()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Frente A Administracion");
        var lead = await _api.LeadNuevo(slug);
        await Esperar(await _api.ReasignarLead(_api.Admin.Token, lead.Id, null, administracion: true), HttpStatusCode.OK);
        await Error(await _api.AccionLead(asesor.Token, lead.Id, "notas", new { texto = "Intento del asesor (prueba)" }),
            HttpStatusCode.NotFound, MsgLeadNoEncontrado);
        await Error(await _api.AccionLead(asesor.Token, lead.Id, "cerrar", new { resultado = "NO_RESPONDIO" }),
            HttpStatusCode.NotFound, MsgLeadNoEncontrado);
        Assert.Equal(Lead.Abierto, (await _api.LeadDb(lead.Id)).Estado);
        Assert.Equal(0, (await _api.Resumen(asesor.Token)).GetProperty("abiertos").GetInt32());
    }

    [Fact(DisplayName = "CA-P5-37, CA-P5-39: el administrador también gestiona el lead de un asesor: lo cierra con su nombre y auditoría; el asesor lo ve CERRADO")]
    public async Task Admin_cierra_lead_de_asesor()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Cierre Por Admin");
        var lead = await _api.LeadNuevo(slug);
        var json = await Esperar(await _api.AccionLead(_api.Admin.Token, lead.Id, "cerrar", new { resultado = "DUPLICADO_INVALIDO" }),
            HttpStatusCode.OK);
        Assert.Equal("CERRADO", json.GetProperty("estado").GetString());
        Assert.Equal("DUPLICADO_INVALIDO", json.GetProperty("resultadoCierre").GetString());
        Assert.Contains(await _api.AuditoriaLead(lead.Id), a => a.Accion == "CERRAR" && a.UsuarioId == _api.Admin.Id);
        Assert.Equal(asesor.PerfilId, (await _api.LeadDb(lead.Id)).ResponsableAsesorId); // no cambia el responsable
        var visto = await Esperar(await _api.DetalleLead(asesor.Token, lead.Id), HttpStatusCode.OK);
        Assert.Equal("CERRADO", visto.GetProperty("estado").GetString());
    }

    [Fact(DisplayName = "CA-P5-32: un comprador puede contactar (POST /api/contactos) pero no consultar el lead que él mismo creó")]
    public async Task Comprador_contacta_pero_no_lee()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Comprador Lee");
        var lead = await _api.LeadNuevo(slug, token: _api.TokenComprador);
        Assert.NotNull(lead.UsuarioId);
        await Error(await _api.DetalleLead(_api.TokenComprador, lead.Id), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.ListaLeads(_api.TokenComprador, "?publicacionId=" + lead.PublicacionId), HttpStatusCode.Forbidden, MsgSinPermiso);
    }
}
