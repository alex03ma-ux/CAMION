using System.Net;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;
using static Camion.Api.Tests.Infraestructura.AyudasP5b;

namespace Camion.Api.Tests;

/// <summary>
/// P5b (QA) — huecos de la matriz CA-P5b: avisos frente a la reasignación de vehículo y al bloqueo del asesor (CA-P5b-36),
/// sin edición ni borrado ni lectura pública (CA-P5b-45, D-53), comprador sin acceso a avisos ajenos (CA-P5b-48), asesor
/// Rechazado o Bloqueado que envía (CA-P5b-51), versión de política del cliente ignorada (CA-P5b-10, 20), filtro por
/// vehículo (CA-P5b-44), prueba de autorización en cada envío del comprador (CA-P5b-21) y reasignar un aviso cerrado o
/// reabierto (CA-P5b-43, D-52 A). SQLite en memoria; nunca MariaDB. Datos ficticios.
/// </summary>
public sealed class AvisosPermisosQaTests(P5Fixture fixture) : IClassFixture<P5Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    [Fact(DisplayName = "CA-P5b-36, 61: reasignar el vehículo (D-34) y bloquear al asesor mueven su contacto de vehículo pero no el aviso que ese asesor envió")]
    public async Task Reasignacion_de_vehiculo_y_bloqueo_no_mueven_avisos()
    {
        _api.Hora(T);
        var (origen, pub, slug) = await _api.AsesorPublicado("Asesor QA Origen Aviso");
        var (destino, _) = await _api.AsesorConPlan("Asesor QA Destino Aviso");
        var contacto = await _api.LeadNuevo(slug);
        var aviso = await _api.AvisoNuevo(RutaVende, token: origen.Token);
        Assert.Equal(origen.UsuarioId, aviso.UsuarioId);

        _api.Hora(T.AddHours(1));
        await Esperar(await _api.Reasignar(_api.Admin.Token, pub.GetProperty("vehiculoId").GetInt64(), destino.PerfilId), HttpStatusCode.OK);
        Assert.Equal(destino.PerfilId, (await _api.LeadDb(contacto.Id)).ResponsableAsesorId);   // regresión P5 (CA-P5-52)

        Assert.Equal(HttpStatusCode.OK, (await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, destino.UsuarioId, "bloquear",
            "Incumplimiento de prueba QA")).StatusCode);
        Assert.Null((await _api.LeadDb(contacto.Id)).ResponsableAsesorId);                      // regresión P5 (CA-P5-51)

        var db = await _api.LeadDb(aviso.Id);
        Assert.Null(db.ResponsableAsesorId);
        Assert.False(db.AsignacionManual);
        Assert.Equal(Lead.Abierto, db.Estado);
        Assert.Equal(new[] { EventoLead.Creado, EventoLead.Asignado }, (await _api.EventosDb(aviso.Id)).Select(e => e.Tipo).ToArray());
        Assert.DoesNotContain(await _api.AuditoriaLead(aviso.Id), a => a.Accion == "REASIGNAR");
        _api.Hora(T);
    }

    [Fact(DisplayName = "CA-P5b-45, D-53: no hay rutas para editar, borrar ni leer avisos (PUT/PATCH/DELETE en /api/leads/{id} y en /api/avisos/*; GET en /api/avisos/*)")]
    public async Task Sin_edicion_borrado_ni_lectura_de_avisos()
    {
        _api.Hora(T);
        var aviso = await _api.AvisoNuevo(RutaSeguro);
        foreach (var token in new[] { _api.Admin.Token, _api.TokenComprador, null })
        {
            foreach (var metodo in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
                foreach (var url in new[] { $"/api/leads/{aviso.Id}", RutaVende, RutaSeguro, $"{RutaSeguro}/{aviso.Id}" })
                {
                    var r = await _api.Enviar(metodo, url, token, new { nombre = "Cambiado QA", telefono = "3009999999", tipoVehiculo = "OTRO" });
                    Assert.True(r.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound
                            or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                        $"{metodo} {url}: {(int)r.StatusCode}");
                }
            foreach (var url in new[] { RutaVende, RutaSeguro, $"{RutaSeguro}/{aviso.Id}", "/api/avisos" })
            {
                var r = await _api.Enviar(HttpMethod.Get, url, token);
                Assert.True(r.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound, $"GET {url}: {(int)r.StatusCode}");
            }
        }
        var actual = await _api.LeadDb(aviso.Id);
        Assert.Equal(aviso.Nombre, actual.Nombre);
        Assert.Equal(aviso.Telefono, actual.Telefono);
        Assert.Equal("CAMION", actual.TipoVehiculo);
        Assert.Single(await _api.ConsentimientosDb(aviso.Id));
    }

    [Fact(DisplayName = "CA-P5b-48: un comprador con sesión no puede leer ni gestionar el aviso de otra persona (403 en todas las rutas) y nada cambia")]
    public async Task Comprador_no_ve_ni_toca_avisos_ajenos()
    {
        _api.Hora(T);
        var ajeno = await _api.AvisoNuevo(RutaVende);
        var c = _api.TokenComprador;
        await Error(await _api.ListaLeads(c, "?origen=VENDE_TU_CAMION"), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.DetalleLead(c, ajeno.Id), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.AccionLead(c, ajeno.Id, "notas", new { texto = "Nota de comprador QA" }), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.AccionLead(c, ajeno.Id, "cerrar", new { resultado = "ATENDIDA" }), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.AccionLead(c, ajeno.Id, "reabrir", new { motivo = MotivoP5 }), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.ReasignarLead(c, ajeno.Id, null, administracion: true), HttpStatusCode.Forbidden, MsgSinPermiso);
        var db = await _api.LeadDb(ajeno.Id);
        Assert.Equal(Lead.Abierto, db.Estado);
        Assert.False(db.VistoPorResponsable);
        Assert.Equal(2, (await _api.EventosDb(ajeno.Id)).Count);
    }

    [Theory(DisplayName = "CA-P5b-51: un asesor Rechazado o Bloqueado envía un aviso como cualquier visitante (201, en Administración) y sigue sin acceso al panel")]
    [InlineData("rechazar")]
    [InlineData("bloquear")]
    public async Task Asesor_rechazado_o_bloqueado_envia_como_visitante(string accion)
    {
        _api.Hora(T);
        var (id, _, _) = await AyudasP2.CrearAsesorPendiente(_api.C, "Asesor QA " + accion);
        if (accion == "bloquear")
            Assert.Equal(HttpStatusCode.OK, (await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, id, "aprobar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, id, accion, "Motivo de prueba QA")).StatusCode);
        var usuario = await AyudasP2.Usuario(_api.F, id);
        var token = AyudasP2.Firmar(_api.F, id, usuario.SessionVersion, "ASESOR");

        var aviso = await _api.AvisoNuevo(RutaVende, token: token);
        Assert.Null(aviso.ResponsableAsesorId);
        Assert.Equal(Lead.Abierto, aviso.Estado);
        Assert.Null(aviso.UsuarioId);   // su sesión no es válida: se trata como anónimo (contrato P5b, "Quién envía")
        foreach (var url in new[] { "/api/leads", "/api/leads/resumen", $"/api/leads/{aviso.Id}" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Enviar(HttpMethod.Get, url, token)).StatusCode);
    }

    [Fact(DisplayName = "CA-P5b-10, 20: versionPolitica, usuarioId y vistoPorResponsable enviados por el cliente se ignoran; la prueba usa la versión del servidor")]
    public async Task Version_de_politica_del_cliente_se_ignora()
    {
        _api.Hora(T);
        var t = TelefonoUnico();
        var cuerpo = CuerpoSeguro(t, "MAQUINARIA");
        cuerpo["versionPolitica"] = "CLIENTE-QA-1";
        cuerpo["usuarioId"] = _api.Admin.Id;
        cuerpo["vistoPorResponsable"] = true;
        await _api.AvisoOk(RutaSeguro, cuerpo);
        var lead = await _api.LeadDeTelefono(t);
        Assert.Null(lead.UsuarioId);
        Assert.False(lead.VistoPorResponsable);
        var prueba = Assert.Single(await _api.ConsentimientosDb(lead.Id));
        Assert.Equal(VersionPolitica, prueba.VersionPolitica);
        Assert.Equal(ConsentimientoDatos.SeguroTodoRiesgo, prueba.Formulario);
        Assert.Equal("Lead", prueba.Entidad);
    }

    [Fact(DisplayName = "CA-P5b-44: el filtro por vehiculoId excluye los avisos; sin filtro el administrador los ve")]
    public async Task Filtro_por_vehiculo_excluye_avisos()
    {
        _api.Hora(T);
        var (_, pub, slug) = await _api.AsesorPublicado("Asesor QA Filtro Vehiculo");
        var contacto = await _api.LeadNuevo(slug);
        var aviso = await _api.AvisoNuevo(RutaVende);
        var vehiculoId = pub.GetProperty("vehiculoId").GetInt64();
        Assert.Equal(new[] { contacto.Id }, await _api.IdsLeads(_api.Admin.Token, $"?vehiculoId={vehiculoId}"));
        Assert.Empty(await _api.IdsLeads(_api.Admin.Token, $"?vehiculoId={vehiculoId}&origen=VENDE_TU_CAMION"));
        Assert.Contains(aviso.Id, await _api.IdsLeads(_api.Admin.Token, "?tamano=100"));
    }

    [Fact(DisplayName = "CA-P5b-21: el comprador debe marcar la casilla en cada aviso (400 sin ella aunque tenga sesión) y cada envío aceptado guarda su prueba ligada a su cuenta")]
    public async Task Comprador_autoriza_en_cada_envio()
    {
        _api.Hora(T);
        var t = TelefonoUnico();
        var sin = CuerpoVende(t);
        sin["autorizacionDatos"] = false;
        await Error400(await _api.Aviso(RutaVende, sin, _api.TokenComprador), "autorizacionDatos", MsgAutorizacion);
        Assert.Equal(0, await _api.LeadsDeTelefono(t));

        await _api.AvisoOk(RutaVende, CuerpoVende(t), _api.TokenComprador);
        await _api.AvisoOk(RutaVende, CuerpoVende(t, "Segunda descripción QA"), _api.TokenComprador);
        var lead = await _api.LeadDeTelefono(t);
        var pruebas = await _api.ConsentimientosDb(lead.Id);
        Assert.Equal(2, pruebas.Count);
        Assert.All(pruebas, p => Assert.Equal(lead.UsuarioId, p.UsuarioId));
        Assert.NotNull(lead.UsuarioId);
    }

    [Fact(DisplayName = "CA-P5b-43 (D-52 A): reasignar un aviso CERRADO o reabierto también da 409 y no deja rastro")]
    public async Task Reasignar_aviso_cerrado_o_reabierto_409()
    {
        _api.Hora(T);
        var aviso = await _api.AvisoNuevo(RutaVende);
        var admin = _api.Admin.Token;
        await Esperar(await _api.AccionLead(admin, aviso.Id, "cerrar", new { resultado = "ATENDIDA" }), HttpStatusCode.OK);
        await Error(await _api.ReasignarLead(admin, aviso.Id, _api.A.PerfilId), HttpStatusCode.Conflict, MsgAvisoAdmin);
        await Esperar(await _api.AccionLead(admin, aviso.Id, "reabrir", new { motivo = MotivoP5 }), HttpStatusCode.OK);
        await Error(await _api.ReasignarLead(admin, aviso.Id, _api.A.PerfilId), HttpStatusCode.Conflict, MsgAvisoAdmin);
        Assert.Null((await _api.LeadDb(aviso.Id)).ResponsableAsesorId);
        Assert.DoesNotContain(await _api.EventosDb(aviso.Id), e => e.Tipo == EventoLead.Reasignado);
        Assert.DoesNotContain(await _api.AuditoriaLead(aviso.Id), a => a.Accion == "REASIGNAR");
        Assert.DoesNotContain(aviso.Id, await _api.IdsLeads(_api.A.Token, "?tamano=100"));
    }
}
