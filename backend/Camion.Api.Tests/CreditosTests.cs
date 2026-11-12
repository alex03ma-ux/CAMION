using System.Net;
using System.Text;
using System.Text.Json;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;
using static Camion.Api.Tests.Infraestructura.AyudasP5b;
using static Camion.Api.Tests.Infraestructura.AyudasP6;

namespace Camion.Api.Tests;

/// <summary>
/// P6 — solicitud de crédito (HU-039, HU-040, HU-041, HU-042; D-7, D-45, D-54 A, D-55 B, D-57, D-58 A): desde la portada y
/// desde la ficha, sin datos financieros, siempre en Administración, repetidos, gestión y permisos. Criterios:
/// docs/criterios/criterios-P6-creditos.md. Contrato: docs/criterios/P6-contrato-api.md. SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class CreditosTests(P5Fixture fixture) : IClassFixture<P5Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    [Fact(DisplayName = "CA-P6-01, 10, 37: crédito anónimo desde la portada -> 201 solo con el mensaje; lead ABIERTO CREDITO sin publicación ni tipo, en Administración; CREADO y ASIGNADO (SISTEMA); consentimiento CREDITO; auditoría sin datos personales")]
    public async Task Portada_anonimo()
    {
        _api.Hora(T);
        var nuevosAntes = (await _api.Resumen(_api.Admin.Token)).GetProperty("nuevos").GetInt32();
        var telefono = TelefonoUnico();
        var r = await _api.Solicitar(RutaCredito, CuerpoCredito(telefono: telefono));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.Equal($"{{\"mensaje\":\"{MsgExitoSolicitud}\"}}", await r.Content.ReadAsStringAsync());

        var lead = await _api.LeadDeTelefono(telefono);
        Assert.Equal(Lead.OrigenCredito, lead.Origen);
        Assert.Null(lead.PublicacionId);
        Assert.Null(lead.TipoVehiculo);
        Assert.Null(lead.ResponsableAsesorId);
        Assert.Null(lead.UsuarioId);
        Assert.Equal(Lead.Abierto, lead.Estado);
        Assert.Equal(MsgCredito, lead.Mensaje);
        Assert.Equal("+57" + telefono, lead.Telefono);

        var eventos = await _api.EventosDb(lead.Id);
        Assert.Equal(new[] { EventoLead.Creado, EventoLead.Asignado }, eventos.Select(e => e.Tipo).ToArray());
        Assert.All(eventos, e => Assert.Null(e.ActorUsuarioId));
        Assert.True(eventos[1].Automatico);
        Assert.Null(eventos[1].ResponsableNuevoAsesorId);

        var prueba = Assert.Single(await _api.ConsentimientosDb(lead.Id));
        Assert.Equal(ConsentimientoDatos.Credito, prueba.Formulario);
        Assert.Equal(VersionPolitica, prueba.VersionPolitica);
        Assert.Equal(T, prueba.AceptadoEn);

        var audit = Assert.Single(await _api.AuditoriaLead(lead.Id));
        Assert.Equal("CREAR", audit.Accion);
        Assert.Null(audit.UsuarioId);
        var datos = JsonDe(audit.DatosNuevos);
        Assert.Equal("CREDITO", datos["origen"].GetString());
        Assert.False(datos["conCuenta"].GetBoolean());
        Assert.False(datos["conVehiculo"].GetBoolean());
        Assert.Equal(JsonValueKind.Null, datos["responsableAsesorId"].ValueKind);
        foreach (var dato in new[] { telefono, "Solicitante Ficticio" })
            Assert.DoesNotContain(dato, audit.DatosNuevos!);

        // CA-P6-57: cuenta como nuevo para el administrador.
        Assert.Equal(nuevosAntes + 1, (await _api.Resumen(_api.Admin.Token)).GetProperty("nuevos").GetInt32());
    }

    [Fact(DisplayName = "CA-P6-03, 58 a 60, D-58 A: crédito desde la ficha -> guarda la publicación pero queda en Administración, no con el asesor dueño; el asesor no lo ve ni le cuenta; texto fijo de la ficha")]
    public async Task Ficha_en_administracion()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor Ficha Credito");
        var resumenAsesor = await _api.Resumen(asesor.Token);
        var lead = await _api.CreditoNuevo(slug);
        Assert.Equal(IdDe(pub), lead.PublicacionId);
        Assert.Null(lead.ResponsableAsesorId);
        Assert.Null(lead.TipoVehiculo);
        Assert.Equal(MsgCreditoVehiculo, lead.Mensaje);
        Assert.True(JsonDe(Assert.Single(await _api.AuditoriaLead(lead.Id)).DatosNuevos)["conVehiculo"].GetBoolean());

        Assert.DoesNotContain(lead.Id, await _api.IdsLeads(asesor.Token));
        Assert.Equal(resumenAsesor.ToString(), (await _api.Resumen(asesor.Token)).ToString());
        await Error(await _api.DetalleLead(asesor.Token, lead.Id), HttpStatusCode.NotFound, MsgLeadNoEncontrado);
        await Error(await _api.Nota(asesor.Token, lead.Id), HttpStatusCode.NotFound, MsgLeadNoEncontrado);
        await Error(await _api.Cerrar(asesor.Token, lead.Id, "ATENDIDA"), HttpStatusCode.NotFound, MsgLeadNoEncontrado);

        // CA-P6-40: el administrador lo ve con el filtro por vehículo y por origen.
        Assert.Contains(lead.Id, await _api.IdsLeads(_api.Admin.Token, $"?publicacionId={IdDe(pub)}"));
        Assert.Contains(lead.Id, await _api.IdsLeads(_api.Admin.Token, "?origen=credito"));
        Assert.DoesNotContain(lead.Id, await _api.IdsLeads(_api.Admin.Token, "?origen=CONTACTO_VEHICULO"));
        Assert.Contains(lead.Id, await _api.IdsLeads(_api.Admin.Token, "?responsable=ADMINISTRACION"));
    }

    [Fact(DisplayName = "CA-P6-09, 12, 15, D-55 B: monto, cuota, ingresos, tipo de vehículo, documento, origen, estado, responsable, publicacionId y usuarioId se ignoran: misma respuesta y nada se guarda en ninguna tabla")]
    public async Task Campos_financieros_y_manipulados_ignorados()
    {
        _api.Hora(T);
        var (asesor, pub, _) = await _api.AsesorPublicado("Asesor Manipulado Credito");
        var telefono = TelefonoUnico();
        var cuerpo = CuerpoCredito(telefono: telefono, mensaje: "Quiero financiar un camión (prueba)");
        cuerpo["monto"] = 987654321;
        cuerpo["cuotaInicial"] = 123456789;
        cuerpo["ingresos"] = 7654321;
        cuerpo["tipoVehiculo"] = "CAMION";
        cuerpo["documento"] = "1029384756";
        cuerpo["origen"] = "CONTACTO_VEHICULO";
        cuerpo["estado"] = "CERRADO";
        cuerpo["responsable"] = asesor.PerfilId;
        cuerpo["asesorId"] = asesor.PerfilId;
        cuerpo["publicacionId"] = IdDe(pub);
        cuerpo["usuarioId"] = _api.Admin.Id;
        cuerpo["versionPolitica"] = "VERSION-DEL-CLIENTE";
        await _api.SolicitudOk(RutaCredito, cuerpo);

        var lead = await _api.LeadDeTelefono(telefono);
        Assert.Equal(Lead.OrigenCredito, lead.Origen);
        Assert.Equal(Lead.Abierto, lead.Estado);
        Assert.Null(lead.PublicacionId);
        Assert.Null(lead.TipoVehiculo);
        Assert.Null(lead.ResponsableAsesorId);
        Assert.Null(lead.UsuarioId);
        Assert.Equal(VersionPolitica, Assert.Single(await _api.ConsentimientosDb(lead.Id)).VersionPolitica);
        var todo = await _api.TodoLoGuardado(lead.Id);
        foreach (var dato in new[] { "987654321", "123456789", "7654321", "1029384756", "VERSION-DEL-CLIENTE" })
            Assert.DoesNotContain(dato, todo);
    }

    [Fact(DisplayName = "CA-P6-04: slug inexistente, de más de 200, publicación inactiva o con el plan vencido -> 404 sin guardar nada")]
    public async Task Publicacion_no_visible()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor No Visible Credito");
        var (_, pub2, slug2) = await _api.AsesorPublicado("Asesor Vencido Credito");
        await _api.Inactivada(asesor.Token, IdDe(pub));
        var antes = await _api.TotalLeads();
        foreach (var s in new[] { "no-existe-este-vehiculo", new string('s', 201), slug })
            await Error(await _api.Solicitar(RutaCredito, CuerpoCredito(s)), HttpStatusCode.NotFound, MsgNoDisponible);

        // Plan vencido (D-41): deja de ser visible y el crédito desde la ficha da 404.
        _api.Hora(T.AddDays(45));
        await Error(await _api.Solicitar(RutaCredito, CuerpoCredito(slug2)), HttpStatusCode.NotFound, MsgNoDisponible);
        Assert.Equal(antes, await _api.TotalLeads());
        _api.Hora(T);
        Assert.Equal(IdDe(pub2), (await _api.CreditoNuevo(slug2)).PublicacionId);
    }

    [Fact(DisplayName = "CA-P6-05, 06, 64: el asesor dueño -> 409 sin guardar; otro asesor, un comprador y el administrador con sesión -> ligado a su cuenta, siempre en Administración")]
    public async Task Propio_y_con_sesion()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Propio Credito");
        var antes = await _api.TotalLeads();
        await Error(await _api.Solicitar(RutaCredito, CuerpoCredito(slug), asesor.Token), HttpStatusCode.Conflict, MsgPropio);
        Assert.Equal(antes, await _api.TotalLeads());

        var otro = await _api.CreditoNuevo(slug, _api.A.Token);
        Assert.Equal(_api.A.UsuarioId, otro.UsuarioId);
        Assert.Null(otro.ResponsableAsesorId);

        var (tokenComprador, idComprador) = await _api.CompradorNuevo();
        var comprador = await _api.CreditoNuevo(token: tokenComprador);
        Assert.Equal(idComprador, comprador.UsuarioId);
        Assert.Equal(idComprador, Assert.Single(await _api.ConsentimientosDb(comprador.Id)).UsuarioId);
        Assert.True(JsonDe(Assert.Single(await _api.AuditoriaLead(comprador.Id)).DatosNuevos)["conCuenta"].GetBoolean());

        var admin = await _api.CreditoNuevo(slug, _api.Admin.Token);
        Assert.Equal(_api.Admin.Id, admin.UsuarioId);
        Assert.Null(admin.ResponsableAsesorId);

        // Un token inválido se trata como anónimo.
        Assert.Null((await _api.CreditoNuevo(token: "token.invalido.prueba")).UsuarioId);
    }

    [Theory(DisplayName = "CA-P6-07, 11, 27: nombre, teléfono, correo, comentario de 501 o sin autorización -> 400 en su campo; no se guarda nada")]
    [InlineData("nombre", "x", MsgNombre)]
    [InlineData("nombre", null, MsgNombre)]
    [InlineData("telefono", "3001234", MsgTelefono)]
    [InlineData("telefono", "2001234567", MsgTelefono)]
    [InlineData("correo", "no-es-correo", MsgCorreo)]
    [InlineData("mensaje", "501", MsgComentarioLargo)]
    [InlineData("autorizacionDatos", false, MsgAutorizacion)]
    [InlineData("autorizacionDatos", null, MsgAutorizacion)]
    public async Task Validaciones(string campo, object? valor, string mensaje)
    {
        _api.Hora(T);
        var cuerpo = CuerpoCredito();
        cuerpo[campo] = valor is "501" ? new string('m', 501) : valor;
        var antes = (await _api.TotalLeads(), await _api.Leer(db => db.ConsentimientosDatos.CountAsync()));
        await Error400(await _api.Solicitar(RutaCredito, cuerpo), campo, mensaje);
        Assert.Equal(antes, (await _api.TotalLeads(), await _api.Leer(db => db.ConsentimientosDatos.CountAsync())));
    }

    [Fact(DisplayName = "CA-P6-07, 11: comentario de 500 se guarda recortado; correo en minúsculas; teléfono con +57 normalizado")]
    public async Task Comentario_500_y_normalizacion()
    {
        _api.Hora(T);
        var telefono = TelefonoUnico();
        var cuerpo = CuerpoCredito(telefono: "+57 " + telefono[..3] + " " + telefono[3..], mensaje: "  " + new string('c', 500) + "  ");
        cuerpo["correo"] = "Cliente.Ficticio@Ejemplo.TEST";
        await _api.SolicitudOk(RutaCredito, cuerpo);
        var lead = await _api.LeadDeTelefono(telefono);
        Assert.Equal(new string('c', 500), lead.Mensaje);
        Assert.Equal("cliente.ficticio@ejemplo.test", lead.Correo);
    }

    [Fact(DisplayName = "CA-P6-29: campo trampa -> 201 sin guardar; más de 8 KB -> 413; JSON malformado o vacío -> 400 formato")]
    public async Task Trampa_tamano_formato()
    {
        _api.Hora(T);
        var antes = await _api.TotalLeads();
        var trampa = CuerpoCredito();
        trampa["sitioWeb"] = "https://spam.example";
        await _api.SolicitudOk(RutaCredito, trampa);
        Assert.Equal(antes, await _api.TotalLeads());

        var grande = CuerpoCredito();
        grande["nombre"] = new string('n', 8300);
        await Error(await _api.Solicitar(RutaCredito, grande), HttpStatusCode.RequestEntityTooLarge, MsgGrande);
        foreach (var crudo in new[] { "{\"nombre\": ", "{\"autorizacionDatos\": \"si\"}", "[1,2]" })
        {
            var malo = new HttpRequestMessage(HttpMethod.Post, RutaCredito) { Content = new StringContent(crudo, Encoding.UTF8, "application/json") };
            await Error(await _api.C.SendAsync(malo), HttpStatusCode.BadRequest, InventarioApi.MsgFormato);
        }
        await Error(await _api.C.SendAsync(new HttpRequestMessage(HttpMethod.Post, RutaCredito)), HttpStatusCode.BadRequest, InventarioApi.MsgFormato);
        Assert.Equal(antes, await _api.TotalLeads());
    }

    [Fact(DisplayName = "CA-P6-31, 33: mismo teléfono y misma publicación (o ambos sin ella) con crédito ABIERTO -> CONTACTO_REPETIDO y consentimiento, sin lead nuevo; otra publicación, la portada, otro origen o un crédito cerrado -> lead nuevo")]
    public async Task Repetidos()
    {
        _api.Hora(T);
        var (_, pub1, slug1) = await _api.AsesorPublicado("Asesor Repetido Credito 1");
        var (_, _, slug2) = await _api.AsesorPublicado("Asesor Repetido Credito 2");
        var t = TelefonoUnico();
        var portada = await _api.CreditoNuevo(telefono: t);
        await _api.SolicitudOk(RutaCredito, CuerpoCredito(telefono: t, mensaje: "Ahora con cuota (prueba)"));
        Assert.Equal(1, await _api.LeadsDeTelefono(t));
        var eventos = await _api.EventosDb(portada.Id);
        Assert.Equal(EventoLead.ContactoRepetido, eventos[^1].Tipo);
        Assert.Equal("Ahora con cuota (prueba)", eventos[^1].Texto);
        Assert.Equal(2, (await _api.ConsentimientosDb(portada.Id)).Count);

        var ficha = await _api.CreditoNuevo(slug1, telefono: t);
        Assert.NotEqual(portada.Id, ficha.Id);
        Assert.Equal(IdDe(pub1), ficha.PublicacionId);
        await _api.SolicitudOk(RutaCredito, CuerpoCredito(slug1, t));
        Assert.Equal(2, await _api.LeadsDeTelefono(t));
        Assert.Equal(MsgCreditoVehiculo, (await _api.EventosDb(ficha.Id))[^1].Texto);

        var otraFicha = await _api.CreditoNuevo(slug2, telefono: t);
        Assert.NotEqual(ficha.Id, otraFicha.Id);
        // Otro origen con el mismo teléfono: lead aparte.
        await _api.AvisoOk(RutaVende, CuerpoVende(t));
        Assert.Equal(4, await _api.LeadsDeTelefono(t));

        // Cerrado: el siguiente crea uno nuevo.
        await Esperar(await _api.Cerrar(_api.Admin.Token, portada.Id, "ATENDIDA"), HttpStatusCode.OK);
        var nuevo = await _api.CreditoNuevo(telefono: t);
        Assert.NotEqual(portada.Id, nuevo.Id);
        // El repetido no vuelve a marcar como nuevo (D-49).
        Assert.False((await _api.LeadDb(nuevo.Id)).VistoPorResponsable);
    }

    [Fact(DisplayName = "CA-P6-41, 43 a 45, 49: detalle sin datos financieros y con vehículo (precio y visible) o null; estado Registrado -> En gestión -> Cerrado; resultados del crédito y 400 a los que no aplican; nota sobre cerrado 409; reabrir")]
    public async Task Gestion_por_el_administrador()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor Gestion Credito");
        var ficha = await _api.CreditoNuevo(slug);
        var portada = await _api.CreditoNuevo();

        var d = await _api.DetalleAdmin(ficha.Id);
        Assert.Equal("CREDITO", d.GetProperty("origen").GetString());
        Assert.Equal("REGISTRADO", d.GetProperty("estadoGestion").GetString());
        Assert.Equal(JsonValueKind.Null, d.GetProperty("tipoVehiculo").ValueKind);
        Assert.Equal(JsonValueKind.Null, d.GetProperty("busqueda").ValueKind);
        Assert.Equal(JsonValueKind.Null, d.GetProperty("vehiculosRelacionados").ValueKind);
        var v = d.GetProperty("vehiculo");
        Assert.Equal(IdDe(pub), v.GetProperty("publicacionId").GetInt64());
        Assert.Equal(slug, v.GetProperty("slug").GetString());
        Assert.Equal(100_000_000, v.GetProperty("precioCop").GetInt64());
        Assert.True(v.GetProperty("visible").GetBoolean());
        Assert.Equal("CREDITO", d.GetProperty("consentimientos")[0].GetProperty("formulario").GetString());
        foreach (var campo in new[] { "monto", "cuotaInicial", "ingresos" })
            Assert.False(d.TryGetProperty(campo, out _));
        // Abrir el detalle lo marca como visto (CA-P6-57).
        Assert.True((await _api.LeadDb(ficha.Id)).VistoPorResponsable);
        Assert.Equal(JsonValueKind.Null, (await _api.DetalleAdmin(portada.Id)).GetProperty("vehiculo").ValueKind);

        // La lista trae estadoGestion y vehiculo sin precio ni visible.
        var lista = await Esperar(await _api.ListaLeads(_api.Admin.Token, "?origen=CREDITO&tamano=100"), HttpStatusCode.OK);
        var item = lista.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetInt64() == ficha.Id);
        Assert.Equal("REGISTRADO", item.GetProperty("estadoGestion").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("vehiculo").GetProperty("visible").ValueKind);

        // Con el vehículo inactivo el detalle lo marca como no visible.
        await _api.Inactivada(asesor.Token, IdDe(pub));
        Assert.False((await _api.DetalleAdmin(ficha.Id)).GetProperty("vehiculo").GetProperty("visible").GetBoolean());

        var conNota = await Esperar(await _api.Nota(_api.Admin.Token, ficha.Id), HttpStatusCode.Created);
        Assert.Equal("EN_GESTION", conNota.GetProperty("estadoGestion").GetString());
        await Error400(await _api.Nota(_api.Admin.Token, ficha.Id, "abc"), "texto", MsgNota);

        foreach (var malo in new[] { "VENTA_CONCRETADA", "VEHICULO_NO_DISPONIBLE", "VEHICULO_ENCONTRADO", "SIN_VEHICULO" })
            await Error400(await _api.Cerrar(_api.Admin.Token, ficha.Id, malo), "resultado", MsgNoAplica);
        await Error400(await _api.Cerrar(_api.Admin.Token, ficha.Id, "APROBADO"), "resultado", MsgResultado);
        await Error400(await _api.Cerrar(_api.Admin.Token, ficha.Id, "OTRO"), "observacion", MsgObservacionOtro);
        var cerrado = await Esperar(await _api.Cerrar(_api.Admin.Token, ficha.Id, "REMITIDO_ENTIDAD"), HttpStatusCode.OK);
        Assert.Equal("CERRADO", cerrado.GetProperty("estadoGestion").GetString());
        Assert.Equal("REMITIDO_ENTIDAD", cerrado.GetProperty("resultadoCierre").GetString());
        await Error(await _api.Cerrar(_api.Admin.Token, ficha.Id, "ATENDIDA"), HttpStatusCode.Conflict, MsgYaCerrado);
        await Error(await _api.Nota(_api.Admin.Token, ficha.Id), HttpStatusCode.Conflict, MsgCerrado);

        var reabierto = await Esperar(await _api.AccionLead(_api.Admin.Token, ficha.Id, "reabrir",
            new { motivo = "El cliente volvió a llamar (prueba)" }), HttpStatusCode.OK);
        Assert.Equal("EN_GESTION", reabierto.GetProperty("estadoGestion").GetString());
        Assert.Equal("ADMINISTRACION", reabierto.GetProperty("responsable").GetProperty("tipo").GetString());

        foreach (var bueno in new[] { "ATENDIDA", "NO_INTERESADO" })
        {
            var otro = await _api.CreditoNuevo();
            await Esperar(await _api.Cerrar(_api.Admin.Token, otro.Id, bueno), HttpStatusCode.OK);
        }
    }

    [Fact(DisplayName = "CA-P6-39, D-58 A: reasignar un crédito (a un asesor o a Administración) -> 409 sin cambios; CA-P6-54: relacionar vehículos o ver coincidencias en un crédito -> 409")]
    public async Task No_se_reasigna_ni_relaciona()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor Reasignar Credito");
        var lead = await _api.CreditoNuevo(slug);
        await Error(await _api.ReasignarLead(_api.Admin.Token, lead.Id, asesor.PerfilId), HttpStatusCode.Conflict, MsgAvisoAdmin);
        await Error(await _api.ReasignarLead(_api.Admin.Token, lead.Id, null, true), HttpStatusCode.Conflict, MsgAvisoAdmin);
        Assert.Null((await _api.LeadDb(lead.Id)).ResponsableAsesorId);
        await Error(await _api.Relacionar(_api.Admin.Token, lead.Id, IdDe(pub)), HttpStatusCode.Conflict, MsgSoloBusqueda);
        await Error(await _api.Coincidencias(_api.Admin.Token, lead.Id), HttpStatusCode.Conflict, MsgSoloBusqueda);
        Assert.Empty(await _api.RelacionesDb(lead.Id));

        // Un contacto y un aviso tampoco admiten vehículos relacionados.
        var contacto = await _api.LeadNuevo(slug);
        await Error(await _api.Relacionar(_api.Admin.Token, contacto.Id, IdDe(pub)), HttpStatusCode.Conflict, MsgSoloBusqueda);
        var aviso = await _api.AvisoNuevo(RutaVende);
        await Error(await _api.Relacionar(_api.Admin.Token, aviso.Id, IdDe(pub)), HttpStatusCode.Conflict, MsgSoloBusqueda);
        // El asesor responsable del contacto no puede relacionar (403).
        Assert.Equal(asesor.PerfilId, contacto.ResponsableAsesorId);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.Relacionar(asesor.Token, contacto.Id, IdDe(pub))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.Coincidencias(asesor.Token, contacto.Id)).StatusCode);
    }

    [Fact(DisplayName = "CA-P6-38: el barrido D-46 (plan vencido) no mueve un crédito pedido desde la ficha; sigue en Administración sin eventos nuevos")]
    public async Task Barrido_no_lo_mueve()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Barrido Credito");
        var lead = await _api.CreditoNuevo(slug);
        var eventos = (await _api.EventosDb(lead.Id)).Count;
        _api.Hora(T.AddDays(45));
        await _api.IdsLeads(_api.Admin.Token);
        Assert.Null((await _api.LeadDb(lead.Id)).ResponsableAsesorId);
        Assert.Equal(eventos, (await _api.EventosDb(lead.Id)).Count);
        _api.Hora(T);
    }

    [Fact(DisplayName = "CA-P6-62, 63, 65: comprador -> 403 en /api/leads; sin token -> 401; el catálogo público no expone créditos")]
    public async Task Permisos_y_portal()
    {
        _api.Hora(T);
        var lead = await _api.CreditoNuevo();
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.ListaLeads(_api.TokenComprador)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.DetalleLead(_api.TokenComprador, lead.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.ListaLeads(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Relacionar(null, lead.Id, 1)).StatusCode);
        var portal = await (await _api.Enviar(HttpMethod.Get, "/api/catalogo/inicio", null)).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Solicitante Ficticio", portal);
        Assert.DoesNotContain("CREDITO", portal);
    }

    [Fact(DisplayName = "CA-P6-79: la base rechaza un crédito con tipo de vehículo o con asesor, una búsqueda con publicación o tipo, y admite los consentimientos y resultados nuevos")]
    public async Task Checks_de_la_base()
    {
        _api.Hora(T);
        var (asesor, pub, _) = await _api.AsesorPublicado("Asesor Checks P6");
        Lead Base(string origen, long? publicacion, string? tipo, long? responsable) => new()
        {
            Origen = origen, PublicacionId = publicacion, TipoVehiculo = tipo, ResponsableAsesorId = responsable,
            Nombre = "Prueba Check P6", Telefono = "+57" + TelefonoUnico(), Mensaje = "Prueba", Estado = Lead.Abierto,
            CreadoEn = T, ActualizadoEn = T
        };
        var malos = new object[]
        {
            Base(Lead.OrigenCredito, null, "CAMION", null),
            Base(Lead.OrigenCredito, IdDe(pub), null, asesor.PerfilId),
            Base(Lead.OrigenBusqueda, IdDe(pub), null, null),
            Base(Lead.OrigenBusqueda, null, "CARRO", null),
            Base(Lead.OrigenBusqueda, null, null, asesor.PerfilId),
            new ConsentimientoDatos { Formulario = "CREDITO_BANCO", VersionPolitica = "v", Entidad = "Lead", EntidadId = "1" },
            new EventoLead { LeadId = (await _api.CreditoNuevo()).Id, Tipo = "VEHICULO_BORRADO", CreadoEn = T }
        };
        for (var i = 0; i < malos.Length; i++)
        {
            var malo = malos[i];
            var fallo = await Record.ExceptionAsync(() => _api.Db(db =>
            {
                db.Add(malo);
                return Task.CompletedTask;
            }));
            Assert.True(fallo is DbUpdateException, $"El caso {i} no fue rechazado por la base");
        }

        // Los válidos sí entran (la API los crea por sus rutas; aquí solo se comprueban los CHECK).
        var credito = Base(Lead.OrigenCredito, IdDe(pub), null, null);
        credito.Estado = Lead.Cerrado;
        credito.ResultadoCierre = Lead.RemitidoEntidad;
        var busqueda = Base(Lead.OrigenBusqueda, null, null, null);
        busqueda.Estado = Lead.Cerrado;
        busqueda.ResultadoCierre = Lead.SinVehiculo;
        await _api.Db(db =>
        {
            db.Leads.AddRange(credito, busqueda, Base(Lead.OrigenCredito, null, null, null));
            db.ConsentimientosDatos.AddRange(
                new ConsentimientoDatos { Formulario = "CREDITO", VersionPolitica = "v", Entidad = "Lead", EntidadId = "1" },
                new ConsentimientoDatos { Formulario = "BUSQUEDA", VersionPolitica = "v", Entidad = "Lead", EntidadId = "1" });
            return Task.CompletedTask;
        });
        await _api.Db(db =>
        {
            db.EventosLead.Add(new EventoLead { LeadId = busqueda.Id, Tipo = EventoLead.VehiculoRelacionado, CreadoEn = T });
            db.EventosLead.Add(new EventoLead { LeadId = busqueda.Id, Tipo = EventoLead.VehiculoRetirado, CreadoEn = T });
            return Task.CompletedTask;
        });

        // leads_busqueda: categoría obligatoria (FK) y CHECK de años y presupuesto.
        foreach (var criterio in new[]
                 {
                     new LeadBusqueda { LeadId = busqueda.Id, TipoVehiculoId = 999_999 },
                     new LeadBusqueda { LeadId = busqueda.Id, TipoVehiculoId = _api.Camion, AnioDesde = 2020, AnioHasta = 2010 },
                     new LeadBusqueda { LeadId = busqueda.Id, TipoVehiculoId = _api.Camion, AnioDesde = 1900 },
                     new LeadBusqueda { LeadId = busqueda.Id, TipoVehiculoId = _api.Camion, PresupuestoMaxCop = 999_999 }
                 })
        {
            var fallo = await Record.ExceptionAsync(() => _api.Db(db =>
            {
                db.LeadsBusqueda.Add(criterio);
                return Task.CompletedTask;
            }));
            Assert.True(fallo is DbUpdateException, "leads_busqueda aceptó una fila inválida");
        }
    }
}
