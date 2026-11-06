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

namespace Camion.Api.Tests;

/// <summary>
/// P5b — avisos públicos "Vende tu camión" (HU-N01) y "Compra tu seguro todo riesgo" (HU-N02): envío sin cuenta y con
/// cuenta, validaciones, autorización de datos, campo trampa, tamaño, repetidos, siempre en Administración (D-6, D-7, D-52)
/// y la bandeja del administrador. Criterios: docs/criterios/criterios-P5b-avisos.md. SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class AvisosTests(P5Fixture fixture) : IClassFixture<P5Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    public static TheoryData<string> Rutas => new() { RutaVende, RutaSeguro };

    private static Dictionary<string, object?> Cuerpo(string ruta, string? telefono = null) =>
        ruta == RutaVende ? CuerpoVende(telefono) : CuerpoSeguro(telefono);

    // ------------------------------------------------------------------ HU-N01

    [Fact(DisplayName = "CA-P5b-01, 32, 33, 34: anónimo -> 201 solo con el mensaje; lead ABIERTO VENDE_TU_CAMION sin publicación en Administración; CREADO y ASIGNADO (SISTEMA); consentimiento; auditoría sin datos; no crea vehículos ni publicaciones")]
    public async Task Vende_anonimo()
    {
        _api.Hora(T);
        var vehiculos = await _api.Leer(db => db.Vehiculos.CountAsync());
        var publicaciones = await _api.Leer(db => db.Publicaciones.CountAsync());
        var telefono = TelefonoUnico();
        var r = await _api.Aviso(RutaVende, CuerpoVende(telefono));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.Equal($"{{\"mensaje\":\"{MsgExitoAviso}\"}}", await r.Content.ReadAsStringAsync());

        var lead = await _api.LeadDeTelefono(telefono);
        Assert.Equal(Lead.OrigenVendeTuCamion, lead.Origen);
        Assert.Null(lead.PublicacionId);
        Assert.Null(lead.TipoVehiculo);
        Assert.Null(lead.ResponsableAsesorId);
        Assert.Null(lead.UsuarioId);
        Assert.Equal(Lead.Abierto, lead.Estado);
        Assert.Equal(DescripcionPrueba, lead.Mensaje);
        Assert.Equal("Vendedor Ficticio", lead.Nombre);
        Assert.False(lead.VistoPorResponsable);
        Assert.Equal(T, lead.CreadoEn);

        var eventos = await _api.EventosDb(lead.Id);
        Assert.Equal(new[] { EventoLead.Creado, EventoLead.Asignado }, eventos.Select(e => e.Tipo).ToArray());
        Assert.All(eventos, e => Assert.Null(e.ActorUsuarioId));
        Assert.True(eventos[1].Automatico);
        Assert.Null(eventos[1].ResponsableNuevoAsesorId);

        var prueba = Assert.Single(await _api.ConsentimientosDb(lead.Id));
        Assert.Equal(ConsentimientoDatos.VendeTuCamion, prueba.Formulario);
        Assert.Equal(VersionPolitica, prueba.VersionPolitica);
        Assert.Equal(T, prueba.AceptadoEn);
        Assert.Null(prueba.UsuarioId);

        var audit = Assert.Single(await _api.AuditoriaLead(lead.Id));
        Assert.Equal("CREAR", audit.Accion);
        Assert.Null(audit.UsuarioId);
        var datos = JsonDe(audit.DatosNuevos);
        Assert.Equal("VENDE_TU_CAMION", datos["origen"].GetString());
        Assert.False(datos["conCuenta"].GetBoolean());
        Assert.Equal(JsonValueKind.Null, datos["responsableAsesorId"].ValueKind);
        foreach (var dato in new[] { telefono, "Vendedor Ficticio", "Kenworth" })
            Assert.DoesNotContain(dato, audit.DatosNuevos!);

        Assert.Equal(vehiculos, await _api.Leer(db => db.Vehiculos.CountAsync()));
        Assert.Equal(publicaciones, await _api.Leer(db => db.Publicaciones.CountAsync()));
    }

    [Fact(DisplayName = "CA-P5b-02, 12, 35, 51: comprador, asesor y administrador con sesión -> aviso ligado a su usuario, siempre en Administración; un asesor Pendiente se trata como visitante")]
    public async Task Con_sesion_ligado_a_la_cuenta()
    {
        _api.Hora(T);
        var comprador = await _api.AvisoNuevo(RutaVende, token: _api.TokenComprador);
        Assert.NotNull(comprador.UsuarioId);
        Assert.Null(comprador.ResponsableAsesorId);
        Assert.Equal(comprador.UsuarioId, Assert.Single(await _api.ConsentimientosDb(comprador.Id)).UsuarioId);
        Assert.True(JsonDe(Assert.Single(await _api.AuditoriaLead(comprador.Id)).DatosNuevos)["conCuenta"].GetBoolean());

        var asesor = await _api.AvisoNuevo(RutaSeguro, token: _api.A.Token);
        Assert.Equal(_api.A.UsuarioId, asesor.UsuarioId);
        Assert.Null(asesor.ResponsableAsesorId);

        var admin = await _api.AvisoNuevo(RutaVende, token: _api.Admin.Token);
        Assert.Equal(_api.Admin.Id, admin.UsuarioId);
        Assert.Null(admin.ResponsableAsesorId);

        // CA-P5b-51: un asesor Pendiente envía como cualquier visitante.
        var (pendienteId, _, _) = await AyudasP2.CrearAsesorPendiente(_api.C, "Asesor Pendiente Aviso");
        var usuario = await AyudasP2.Usuario(_api.F, pendienteId);
        var tokenPendiente = AyudasP2.Firmar(_api.F, pendienteId, usuario.SessionVersion, "ASESOR");
        var pendiente = await _api.AvisoNuevo(RutaSeguro, token: tokenPendiente);
        Assert.Null(pendiente.ResponsableAsesorId);

        // Un token inválido se trata como anónimo.
        var invalido = await _api.AvisoNuevo(RutaVende, token: "token.invalido.prueba");
        Assert.Null(invalido.UsuarioId);
    }

    [Theory(DisplayName = "CA-P5b-03, 13: nombre vacío, de 1 o de más de 150 caracteres -> 400 en nombre; no se guarda nada")]
    [InlineData(RutaVende, "")]
    [InlineData(RutaVende, " A ")]
    [InlineData(RutaSeguro, null)]
    [InlineData(RutaSeguro, "x")]
    [InlineData(RutaVende, "151")]
    public async Task Nombre_invalido(string ruta, string? nombre)
    {
        _api.Hora(T);
        var telefono = TelefonoUnico();
        var cuerpo = Cuerpo(ruta, telefono);
        cuerpo["nombre"] = nombre == "151" ? new string('n', 151) : nombre;
        await Error400(await _api.Aviso(ruta, cuerpo), "nombre", MsgNombre);
        Assert.Equal(0, await _api.LeadsDeTelefono(telefono));
    }

    [Theory(DisplayName = "CA-P5b-04, 14: teléfono vacío, de 7 dígitos, extranjero o que no es celular/fijo colombiano -> 400 en telefono")]
    [InlineData(RutaVende, "")]
    [InlineData(RutaVende, "3001234")]
    [InlineData(RutaSeguro, "+1 305 555 1234")]
    [InlineData(RutaSeguro, "2001234567")]
    [InlineData(RutaVende, "30012345678")]
    [InlineData(RutaSeguro, null)]
    public async Task Telefono_invalido(string ruta, string? telefono)
    {
        _api.Hora(T);
        var cuerpo = Cuerpo(ruta);
        cuerpo["telefono"] = telefono;
        var antes = await _api.TotalLeads();
        await Error400(await _api.Aviso(ruta, cuerpo), "telefono", MsgTelefono);
        Assert.Equal(antes, await _api.TotalLeads());
    }

    [Theory(DisplayName = "CA-P5b-05, 14: formatos con separadores, 57 o +57 se guardan como +57XXXXXXXXXX; también un fijo 60…")]
    [InlineData(RutaVende, "{0} {1} {2}")]
    [InlineData(RutaSeguro, "{0}-{1}-{2}")]
    [InlineData(RutaVende, "({0}) {1}{2}")]
    [InlineData(RutaSeguro, "+57 {0}{1}{2}")]
    [InlineData(RutaVende, "57{0}{1}{2}")]
    public async Task Telefono_normalizado(string ruta, string formato)
    {
        _api.Hora(T);
        var t = TelefonoUnico();
        var cuerpo = Cuerpo(ruta);
        cuerpo["telefono"] = string.Format(formato, t[..3], t[3..6], t[6..]);
        await _api.AvisoOk(ruta, cuerpo);
        Assert.Equal("+57" + t, (await _api.LeadDeTelefono(t)).Telefono);

        var fijo = "60" + Random.Shared.NextInt64(10_000_000, 99_999_999);
        await _api.AvisoOk(ruta, Cuerpo(ruta, fijo));
        Assert.Equal("+57" + fijo, (await _api.LeadDeTelefono(fijo)).Telefono);
    }

    [Theory(DisplayName = "CA-P5b-06: descripción ausente, vacía o solo espacios -> 400 en descripcion")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public async Task Descripcion_obligatoria(string? descripcion)
    {
        _api.Hora(T);
        var telefono = TelefonoUnico();
        await Error400(await _api.Aviso(RutaVende, CuerpoVende(telefono, descripcion)), "descripcion", MsgDescripcionAviso);
        Assert.Equal(0, await _api.LeadsDeTelefono(telefono));
    }

    [Fact(DisplayName = "CA-P5b-07: descripción de 500 caracteres se acepta (recortada); de 501 -> 400 con su mensaje")]
    public async Task Descripcion_500_y_501()
    {
        _api.Hora(T);
        var t1 = TelefonoUnico();
        var quinientos = new string('d', 500);
        await _api.AvisoOk(RutaVende, CuerpoVende(t1, "  " + quinientos + "  "));
        Assert.Equal(quinientos, (await _api.LeadDeTelefono(t1)).Mensaje);

        var t2 = TelefonoUnico();
        await Error400(await _api.Aviso(RutaVende, CuerpoVende(t2, new string('d', 501))), "descripcion", MsgDescripcionLarga);
        Assert.Equal(0, await _api.LeadsDeTelefono(t2));
    }

    [Theory(DisplayName = "CA-P5b-09, 17 (D-50 c): correo opcional; con formato inválido o de más de 254 -> 400 en correo; válido se guarda en minúsculas")]
    [InlineData(RutaVende)]
    [InlineData(RutaSeguro)]
    public async Task Correo_opcional(string ruta)
    {
        _api.Hora(T);
        var cuerpo = Cuerpo(ruta);
        cuerpo["correo"] = "no-es-correo";
        await Error400(await _api.Aviso(ruta, cuerpo), "correo", MsgCorreo);
        cuerpo["correo"] = new string('c', 250) + "@x.co";
        await Error400(await _api.Aviso(ruta, cuerpo), "correo", MsgCorreo);

        var t = TelefonoUnico();
        var bueno = Cuerpo(ruta, t);
        bueno["correo"] = "  Persona.Ficticia@Ejemplo.TEST ";
        await _api.AvisoOk(ruta, bueno);
        Assert.Equal("persona.ficticia@ejemplo.test", (await _api.LeadDeTelefono(t)).Correo);
    }

    [Fact(DisplayName = "CA-P5b-08, 10: slug, publicacionId, origen, estado, asesorId, responsable, tipoVehiculo, ciudad, marca o año enviados se ignoran")]
    public async Task Campos_manipulados_se_ignoran()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor Aviso Manipulado");
        var t = TelefonoUnico();
        var cuerpo = CuerpoVende(t);
        cuerpo["slug"] = slug;
        cuerpo["publicacionId"] = IdDe(pub);
        cuerpo["origen"] = "SEGURO_TODO_RIESGO";
        cuerpo["estado"] = "CERRADO";
        cuerpo["asesorId"] = asesor.PerfilId;
        cuerpo["responsable"] = asesor.PerfilId;
        cuerpo["tipoVehiculo"] = "CAMION";
        cuerpo["ciudad"] = "Medellín";
        cuerpo["marca"] = "Kenworth";
        cuerpo["anio"] = 2015;
        await _api.AvisoOk(RutaVende, cuerpo);
        var lead = await _api.LeadDeTelefono(t);
        Assert.Equal(Lead.OrigenVendeTuCamion, lead.Origen);
        Assert.Null(lead.PublicacionId);
        Assert.Null(lead.TipoVehiculo);
        Assert.Null(lead.ResponsableAsesorId);
        Assert.Equal(Lead.Abierto, lead.Estado);
        Assert.Equal(0, await _api.Leer(db => db.Leads.CountAsync(l => l.PublicacionId == IdDe(pub))));
    }

    // ------------------------------------------------------------------ HU-N02

    [Theory(DisplayName = "CA-P5b-11, 16 (D-50 a): seguro con Camión, Carro, Maquinaria u Otro (mayúsculas o minúsculas) -> lead SEGURO_TODO_RIESGO con el tipo y el mensaje fijo, sin publicación, en Administración")]
    [InlineData("CAMION", "CAMION")]
    [InlineData("carro", "CARRO")]
    [InlineData(" Maquinaria ", "MAQUINARIA")]
    [InlineData("OTRO", "OTRO")]
    public async Task Seguro_valido(string enviado, string guardado)
    {
        _api.Hora(T);
        var t = TelefonoUnico();
        await _api.AvisoOk(RutaSeguro, CuerpoSeguro(t, enviado));
        var lead = await _api.LeadDeTelefono(t);
        Assert.Equal(Lead.OrigenSeguroTodoRiesgo, lead.Origen);
        Assert.Equal(guardado, lead.TipoVehiculo);
        Assert.Equal(MsgSeguro, lead.Mensaje);
        Assert.Null(lead.PublicacionId);
        Assert.Null(lead.ResponsableAsesorId);
        Assert.Equal(ConsentimientoDatos.SeguroTodoRiesgo, Assert.Single(await _api.ConsentimientosDb(lead.Id)).Formulario);
        var datos = JsonDe(Assert.Single(await _api.AuditoriaLead(lead.Id)).DatosNuevos);
        Assert.Equal("SEGURO_TODO_RIESGO", datos["origen"].GetString());
        Assert.Equal(guardado, datos["tipoVehiculo"].GetString());
    }

    [Theory(DisplayName = "CA-P5b-15: tipo de vehículo ausente, vacío o fuera de la lista -> 400 en tipoVehiculo; no se guarda nada")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CARROCERIA")]
    [InlineData("Camión")]
    [InlineData("MOTO")]
    public async Task Seguro_tipo_invalido(string? tipo)
    {
        _api.Hora(T);
        var t = TelefonoUnico();
        await Error400(await _api.Aviso(RutaSeguro, CuerpoSeguro(t, tipo)), "tipoVehiculo", MsgTipoVehiculo);
        Assert.Equal(0, await _api.LeadsDeTelefono(t));
    }

    // ------------------------------------------------------------------ Reglas comunes

    [Theory(DisplayName = "CA-P5b-19: autorizacionDatos ausente, null o false -> 400 en autorizacionDatos; no se guarda nada")]
    [InlineData(RutaVende, "ausente")]
    [InlineData(RutaVende, "false")]
    [InlineData(RutaSeguro, "null")]
    [InlineData(RutaSeguro, "false")]
    public async Task Sin_autorizacion(string ruta, string caso)
    {
        _api.Hora(T);
        var t = TelefonoUnico();
        var cuerpo = Cuerpo(ruta, t);
        if (caso == "ausente") cuerpo.Remove("autorizacionDatos");
        else cuerpo["autorizacionDatos"] = caso == "false" ? false : null;
        var pruebas = await _api.Leer(db => db.ConsentimientosDatos.CountAsync());
        await Error400(await _api.Aviso(ruta, cuerpo), "autorizacionDatos", MsgAutorizacion);
        Assert.Equal(0, await _api.LeadsDeTelefono(t));
        Assert.Equal(pruebas, await _api.Leer(db => db.ConsentimientosDatos.CountAsync()));
    }

    [Theory(DisplayName = "CA-P5b-22: campo trampa relleno -> 201 con el mensaje normal y no se guarda nada (aunque el resto no sea válido)")]
    [InlineData(RutaVende)]
    [InlineData(RutaSeguro)]
    public async Task Campo_trampa(string ruta)
    {
        _api.Hora(T);
        var t = TelefonoUnico();
        var cuerpo = Cuerpo(ruta, t);
        cuerpo["sitioWeb"] = "http://spam.example";
        var antes = (await _api.TotalLeads(), await _api.Leer(db => db.ConsentimientosDatos.CountAsync()));
        await _api.AvisoOk(ruta, cuerpo);
        await _api.AvisoOk(ruta, new Dictionary<string, object?> { ["sitioWeb"] = "x", ["telefono"] = "1" });
        Assert.Equal(antes, (await _api.TotalLeads(), await _api.Leer(db => db.ConsentimientosDatos.CountAsync())));
    }

    [Theory(DisplayName = "CA-P5b-23: más de 8 KB -> 413 (con y sin Content-Length); JSON malformado, vacío o con tipos incorrectos -> 400 formato")]
    [InlineData(RutaVende)]
    [InlineData(RutaSeguro)]
    public async Task Tamano_y_formato(string ruta)
    {
        _api.Hora(T);
        var cuerpo = Cuerpo(ruta);
        cuerpo["nombre"] = new string('n', 8300);
        await Error(await _api.Aviso(ruta, cuerpo), HttpStatusCode.RequestEntityTooLarge, MsgGrande);

        var req = new HttpRequestMessage(HttpMethod.Post, ruta)
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("{\"nombre\":\"" + new string('x', 9000) + "\"}")))
        };
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        req.Headers.TransferEncodingChunked = true;
        await Error(await _api.C.SendAsync(req), HttpStatusCode.RequestEntityTooLarge, MsgGrande);

        foreach (var crudo in new[] { "{\"nombre\": ", "{\"autorizacionDatos\": \"si\"}", "[1,2]" })
        {
            var malo = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = new StringContent(crudo, Encoding.UTF8, "application/json") };
            await Error(await _api.C.SendAsync(malo), HttpStatusCode.BadRequest, InventarioApi.MsgFormato);
        }
        await Error(await _api.C.SendAsync(new HttpRequestMessage(HttpMethod.Post, ruta)), HttpStatusCode.BadRequest, InventarioApi.MsgFormato);
    }

    [Fact(DisplayName = "CA-P5b-26: mismo origen y teléfono con aviso ABIERTO -> sin lead nuevo; CONTACTO_REPETIDO con la descripción nueva (o el tipo del seguro) y consentimiento nuevo; misma respuesta; no vuelve a ser nuevo")]
    public async Task Aviso_repetido()
    {
        _api.Hora(T);
        var t = TelefonoUnico();
        var vende = await _api.AvisoNuevo(RutaVende, t);
        await Esperar(await _api.DetalleLead(_api.Admin.Token, vende.Id), HttpStatusCode.OK); // visto
        await _api.AvisoOk(RutaVende, CuerpoVende("+57 " + t, "Ahora también vendo un remolque (prueba)"));
        Assert.Equal(1, await _api.Leer(db => db.Leads.CountAsync(l => l.Telefono == "+57" + t && l.Origen == Lead.OrigenVendeTuCamion)));
        var ev = (await _api.EventosDb(vende.Id)).Last();
        Assert.Equal(EventoLead.ContactoRepetido, ev.Tipo);
        Assert.Equal("Ahora también vendo un remolque (prueba)", ev.Texto);
        Assert.Equal(2, (await _api.ConsentimientosDb(vende.Id)).Count);
        Assert.True((await _api.LeadDb(vende.Id)).VistoPorResponsable);
        Assert.Equal(DescripcionPrueba, (await _api.LeadDb(vende.Id)).Mensaje);

        var t2 = TelefonoUnico();
        var seguro = await _api.AvisoNuevo(RutaSeguro, t2);
        await _api.AvisoOk(RutaSeguro, CuerpoSeguro(t2, "carro"));
        Assert.Equal(1, await _api.LeadsDeTelefono(t2));
        var ev2 = (await _api.EventosDb(seguro.Id)).Last();
        Assert.Equal(EventoLead.ContactoRepetido, ev2.Tipo);
        Assert.Equal("Solicitud de seguro todo riesgo. Tipo de vehículo: Carro.", ev2.Texto);
        Assert.Equal("CAMION", (await _api.LeadDb(seguro.Id)).TipoVehiculo);
        Assert.Equal(ConsentimientoDatos.SeguroTodoRiesgo, (await _api.ConsentimientosDb(seguro.Id)).Last().Formulario);
    }

    [Fact(DisplayName = "CA-P5b-27: si el aviso anterior está CERRADO, o el abierto es de otro origen (aviso o contacto de vehículo), se crea un lead nuevo")]
    public async Task Aviso_nuevo_tras_cierre_u_otro_origen()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Otro Origen");
        var t = TelefonoUnico();
        await _api.LeadNuevo(slug, t);                      // contacto de vehículo abierto
        var vende = await _api.AvisoNuevo(RutaVende, t);    // aviso nuevo, no repetido
        Assert.Equal(Lead.OrigenVendeTuCamion, vende.Origen);
        var seguro = await _api.AvisoNuevo(RutaSeguro, t);  // otro origen
        Assert.Equal(Lead.OrigenSeguroTodoRiesgo, seguro.Origen);
        Assert.Equal(3, await _api.LeadsDeTelefono(t));

        await Esperar(await _api.AccionLead(_api.Admin.Token, vende.Id, "cerrar", new { resultado = "ATENDIDA" }), HttpStatusCode.OK);
        var otro = await _api.AvisoNuevo(RutaVende, t);
        Assert.NotEqual(vende.Id, otro.Id);
        Assert.Equal(4, await _api.LeadsDeTelefono(t));
    }

    // ------------------------------------------------------------------ Panel del administrador

    [Fact(DisplayName = "CA-P5b-37, 38, 39, 44: el administrador ve los avisos con origen, tipoVehiculo y vehiculo null; filtra por origen; origen no válido -> 400; publicacionId los excluye; cuentan como nuevos y el detalle los marca vistos")]
    public async Task Admin_lista_y_detalle()
    {
        _api.Hora(T);
        var (_, pub, slug) = await _api.AsesorPublicado("Asesor Bandeja Avisos");
        var contacto = await _api.LeadNuevo(slug);
        var nuevosAntes = (await _api.Resumen(_api.Admin.Token)).GetProperty("nuevos").GetInt32();
        var vende = await _api.AvisoNuevo(RutaVende);
        var seguro = await _api.AvisoNuevo(RutaSeguro);
        Assert.Equal(nuevosAntes + 2, (await _api.Resumen(_api.Admin.Token)).GetProperty("nuevos").GetInt32());

        var lista = await Esperar(await _api.ListaLeads(_api.Admin.Token, "?tamano=100"), HttpStatusCode.OK);
        var items = lista.GetProperty("items").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetInt64());
        var iv = items[vende.Id];
        Assert.Equal("VENDE_TU_CAMION", iv.GetProperty("origen").GetString());
        Assert.Equal(JsonValueKind.Null, iv.GetProperty("vehiculo").ValueKind);
        Assert.Equal(JsonValueKind.Null, iv.GetProperty("tipoVehiculo").ValueKind);
        Assert.Equal(DescripcionPrueba, iv.GetProperty("mensaje").GetString());
        Assert.Equal("ADMINISTRACION", iv.GetProperty("responsable").GetProperty("tipo").GetString());
        Assert.True(iv.GetProperty("nuevo").GetBoolean());
        var isg = items[seguro.Id];
        Assert.Equal("SEGURO_TODO_RIESGO", isg.GetProperty("origen").GetString());
        Assert.Equal("CAMION", isg.GetProperty("tipoVehiculo").GetString());
        var ic = items[contacto.Id];
        Assert.Equal("CONTACTO_VEHICULO", ic.GetProperty("origen").GetString());
        Assert.Equal(IdDe(pub), ic.GetProperty("vehiculo").GetProperty("publicacionId").GetInt64());

        var soloVende = await _api.IdsLeads(_api.Admin.Token, "?origen=vende_tu_camion&tamano=100");
        Assert.Contains(vende.Id, soloVende);
        Assert.DoesNotContain(seguro.Id, soloVende);
        Assert.DoesNotContain(contacto.Id, soloVende);
        var soloContactos = await _api.IdsLeads(_api.Admin.Token, "?origen=CONTACTO_VEHICULO&tamano=100");
        Assert.Contains(contacto.Id, soloContactos);
        Assert.DoesNotContain(vende.Id, soloContactos);
        Assert.Contains(seguro.Id, await _api.IdsLeads(_api.Admin.Token, "?origen=SEGURO_TODO_RIESGO&responsable=ADMINISTRACION&tamano=100"));
        await Error400(await _api.ListaLeads(_api.Admin.Token, "?origen=CREDITO"), "origen", MsgOrigen, MsgFiltros);
        Assert.Equal(new[] { contacto.Id }, await _api.IdsLeads(_api.Admin.Token, "?publicacionId=" + IdDe(pub)));

        var detalle = await Esperar(await _api.DetalleLead(_api.Admin.Token, seguro.Id), HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, detalle.GetProperty("vehiculo").ValueKind);
        Assert.Equal("SEGURO_TODO_RIESGO", detalle.GetProperty("origen").GetString());
        Assert.Equal("CAMION", detalle.GetProperty("tipoVehiculo").GetString());
        Assert.Equal("SEGURO_TODO_RIESGO",
            Assert.Single(detalle.GetProperty("consentimientos").EnumerateArray()).GetProperty("formulario").GetString());
        Assert.Equal(new[] { "CREADO", "ASIGNADO" },
            detalle.GetProperty("eventos").EnumerateArray().Select(e => e.GetProperty("tipo").GetString()).ToArray());
        Assert.True((await _api.LeadDb(seguro.Id)).VistoPorResponsable);
        Assert.Equal(nuevosAntes + 1, (await _api.Resumen(_api.Admin.Token)).GetProperty("nuevos").GetInt32());
    }

    [Fact(DisplayName = "CA-P5b-40, 41, 42: el administrador anota, cierra con ATENDIDA y reabre un aviso; VENTA_CONCRETADA y VEHICULO_NO_DISPONIBLE -> 400 no aplica; cerrado -> 409 en nota y cierre")]
    public async Task Admin_seguimiento_de_aviso()
    {
        _api.Hora(T);
        var aviso = await _api.AvisoNuevo(RutaVende);
        var admin = _api.Admin.Token;
        await Error400(await _api.AccionLead(admin, aviso.Id, "notas", new { texto = "1234" }), "texto", MsgNota);
        var nota = await Esperar(await _api.AccionLead(admin, aviso.Id, "notas", new { texto = "Llamé, pide avalúo (prueba)" }),
            HttpStatusCode.Created);
        Assert.Equal("NOTA", nota.GetProperty("eventos").EnumerateArray().Last().GetProperty("tipo").GetString());

        foreach (var noAplica in new[] { "VENTA_CONCRETADA", "VEHICULO_NO_DISPONIBLE" })
            await Error400(await _api.AccionLead(admin, aviso.Id, "cerrar", new { resultado = noAplica }), "resultado", MsgNoAplica);
        await Error400(await _api.AccionLead(admin, aviso.Id, "cerrar", new { resultado = "INVENTADO" }), "resultado", MsgResultado);
        await Error400(await _api.AccionLead(admin, aviso.Id, "cerrar", new { resultado = "OTRO" }), "observacion", MsgObservacionOtro);
        Assert.Equal(Lead.Abierto, (await _api.LeadDb(aviso.Id)).Estado);

        var cerrado = await Esperar(await _api.AccionLead(admin, aviso.Id, "cerrar", new { resultado = "atendida" }), HttpStatusCode.OK);
        Assert.Equal("CERRADO", cerrado.GetProperty("estado").GetString());
        Assert.Equal("ATENDIDA", cerrado.GetProperty("resultadoCierre").GetString());
        Assert.Contains(await _api.AuditoriaLead(aviso.Id), a => a.Accion == "CERRAR" && a.UsuarioId == _api.Admin.Id);
        await Error(await _api.AccionLead(admin, aviso.Id, "cerrar", new { resultado = "ATENDIDA" }), HttpStatusCode.Conflict, MsgYaCerrado);
        await Error(await _api.AccionLead(admin, aviso.Id, "notas", new { texto = "Nota tardía (prueba)" }), HttpStatusCode.Conflict, MsgCerrado);

        var abierto = await Esperar(await _api.AccionLead(admin, aviso.Id, "reabrir", new { motivo = MotivoP5 }), HttpStatusCode.OK);
        Assert.Equal("ABIERTO", abierto.GetProperty("estado").GetString());
        Assert.Equal("ADMINISTRACION", abierto.GetProperty("responsable").GetProperty("tipo").GetString());
        Assert.Contains(await _api.AuditoriaLead(aviso.Id), a => a.Accion == "REABRIR");

        // Los resultados comunes también valen para los avisos.
        var seguro = await _api.AvisoNuevo(RutaSeguro);
        await Esperar(await _api.AccionLead(admin, seguro.Id, "cerrar", new { resultado = "NO_RESPONDIO" }), HttpStatusCode.OK);
    }

    [Fact(DisplayName = "CA-P5b-41, 61: ATENDIDA no aplica a un contacto de vehículo (400); sus resultados de P5 siguen funcionando")]
    public async Task Atendida_no_aplica_a_contacto()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Atendida");
        var lead = await _api.LeadNuevo(slug);
        await Error400(await _api.AccionLead(asesor.Token, lead.Id, "cerrar", new { resultado = "ATENDIDA" }), "resultado", MsgNoAplica);
        await Esperar(await _api.AccionLead(asesor.Token, lead.Id, "cerrar", new { resultado = "VENTA_CONCRETADA" }), HttpStatusCode.OK);
    }

    [Fact(DisplayName = "CA-P5b-43 (D-52 A): reasignar un aviso a un asesor o a Administración -> 409; nada cambia ni se audita")]
    public async Task Reasignar_aviso_409()
    {
        _api.Hora(T);
        var aviso = await _api.AvisoNuevo(RutaSeguro);
        var eventos = (await _api.EventosDb(aviso.Id)).Count;
        await Error(await _api.ReasignarLead(_api.Admin.Token, aviso.Id, _api.A.PerfilId), HttpStatusCode.Conflict, MsgAvisoAdmin);
        await Error(await _api.ReasignarLead(_api.Admin.Token, aviso.Id, null, administracion: true), HttpStatusCode.Conflict, MsgAvisoAdmin);
        await Error(await _api.ReasignarLead(_api.Admin.Token, aviso.Id, null, motivo: null), HttpStatusCode.Conflict, MsgAvisoAdmin);
        Assert.Null((await _api.LeadDb(aviso.Id)).ResponsableAsesorId);
        Assert.Equal(eventos, (await _api.EventosDb(aviso.Id)).Count);
        Assert.DoesNotContain(await _api.AuditoriaLead(aviso.Id), a => a.Accion == "REASIGNAR");
    }

    // ------------------------------------------------------------------ Permisos

    [Fact(DisplayName = "CA-P5b-46, 47: el asesor nunca ve avisos (lista, filtros, resumen); detalle, nota y cierre -> 404; reabrir y reasignar -> 403; nada cambia")]
    public async Task Asesor_no_ve_avisos()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Sin Avisos");
        var propio = await _api.LeadNuevo(slug);
        var vende = await _api.AvisoNuevo(RutaVende, token: asesor.Token);   // ni siquiera el que él mismo envió
        var seguro = await _api.AvisoNuevo(RutaSeguro);

        var ids = await _api.IdsLeads(asesor.Token, "?tamano=100");
        Assert.Equal(new[] { propio.Id }, ids);
        Assert.Empty(await _api.IdsLeads(asesor.Token, "?origen=VENDE_TU_CAMION"));
        Assert.Empty(await _api.IdsLeads(asesor.Token, "?origen=SEGURO_TODO_RIESGO&responsable=ADMINISTRACION"));
        var resumen = await _api.Resumen(asesor.Token);
        Assert.Equal(1, resumen.GetProperty("nuevos").GetInt32());
        Assert.Equal(1, resumen.GetProperty("abiertos").GetInt32());

        foreach (var aviso in new[] { vende, seguro })
        {
            await Error(await _api.DetalleLead(asesor.Token, aviso.Id), HttpStatusCode.NotFound, MsgLeadNoEncontrado);
            await Error(await _api.AccionLead(asesor.Token, aviso.Id, "notas", new { texto = "Nota del asesor (prueba)" }),
                HttpStatusCode.NotFound, MsgLeadNoEncontrado);
            await Error(await _api.AccionLead(asesor.Token, aviso.Id, "cerrar", new { resultado = "ATENDIDA" }),
                HttpStatusCode.NotFound, MsgLeadNoEncontrado);
            await Error(await _api.AccionLead(asesor.Token, aviso.Id, "reabrir", new { motivo = MotivoP5 }),
                HttpStatusCode.Forbidden, MsgSinPermiso);
            await Error(await _api.ReasignarLead(asesor.Token, aviso.Id, asesor.PerfilId), HttpStatusCode.Forbidden, MsgSinPermiso);
            var db = await _api.LeadDb(aviso.Id);
            Assert.Equal(Lead.Abierto, db.Estado);
            Assert.Null(db.ResponsableAsesorId);
            Assert.False(db.VistoPorResponsable);
            Assert.Equal(2, (await _api.EventosDb(aviso.Id)).Count);
        }
    }

    [Fact(DisplayName = "CA-P5b-48, 50, 52: comprador -> 403 en /api/leads aunque haya enviado el aviso; sin token -> 401; el catálogo público no muestra avisos")]
    public async Task Comprador_y_anonimo()
    {
        _api.Hora(T);
        var t = TelefonoUnico();
        var cuerpo = CuerpoVende(t);
        cuerpo["nombre"] = "Comprador Avisa Ficticio";
        await _api.AvisoOk(RutaVende, cuerpo, _api.TokenComprador);
        var aviso = await _api.LeadDeTelefono(t);
        await Error(await _api.DetalleLead(_api.TokenComprador, aviso.Id), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.ListaLeads(_api.TokenComprador, "?origen=VENDE_TU_CAMION"), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.Enviar(HttpMethod.Get, "/api/leads/resumen", _api.TokenComprador), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.AccionLead(_api.TokenComprador, aviso.Id, "cerrar", new { resultado = "ATENDIDA" }),
            HttpStatusCode.Forbidden, MsgSinPermiso);
        foreach (var url in new[] { "/api/leads", "/api/leads/resumen", $"/api/leads/{aviso.Id}" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Enviar(HttpMethod.Get, url, null)).StatusCode);

        foreach (var url in new[] { "/api/catalogo/inicio", "/api/catalogo/publicaciones?tamano=48", "/api/catalogo/portada" })
        {
            var texto = await (await _api.Enviar(HttpMethod.Get, url, null)).Content.ReadAsStringAsync();
            Assert.DoesNotContain("Comprador Avisa Ficticio", texto);
            Assert.DoesNotContain(t, texto);
            Assert.DoesNotContain("VENDE_TU_CAMION", texto);
        }
    }

    [Fact(DisplayName = "CA-P5b-36: el barrido de D-46 (plan vencido) no mueve los avisos")]
    public async Task Barrido_no_toca_avisos()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Barrido Avisos");
        var contacto = await _api.LeadNuevo(slug);
        var aviso = await _api.AvisoNuevo(RutaVende);
        try
        {
            _api.Hora(T.AddMonths(3));
            await Esperar(await _api.ListaLeads(_api.Admin.Token), HttpStatusCode.OK);
            Assert.Null((await _api.LeadDb(contacto.Id)).ResponsableAsesorId);   // el contacto sí se movió
            var db = await _api.LeadDb(aviso.Id);
            Assert.Null(db.ResponsableAsesorId);
            Assert.False(db.AsignacionManual);
            Assert.DoesNotContain(await _api.EventosDb(aviso.Id), e => e.Tipo == EventoLead.Reasignado);
        }
        finally
        {
            _api.Hora(T);
        }
    }

    // ------------------------------------------------------------------ Esquema

    [Fact(DisplayName = "CA-P5b-62, D-52: la base rechaza un aviso con asesor (CK_Lead_AvisoAdmin), un contacto sin publicación, un aviso con publicación y un seguro sin tipo (CK_Lead_Origen)")]
    public async Task Checks_de_la_base()
    {
        _api.Hora(T);
        var (asesor, pub, _) = await _api.AsesorPublicado("Asesor Checks Avisos");
        Lead Base(string origen, long? publicacion, string? tipo, long? responsable) => new()
        {
            Origen = origen, PublicacionId = publicacion, TipoVehiculo = tipo, ResponsableAsesorId = responsable,
            Nombre = "Prueba Check", Telefono = "+57" + TelefonoUnico(), Mensaje = "Prueba", Estado = Lead.Abierto,
            CreadoEn = T, ActualizadoEn = T
        };
        var malos = new[]
        {
            Base(Lead.OrigenVendeTuCamion, null, null, asesor.PerfilId),
            Base(Lead.OrigenSeguroTodoRiesgo, null, "CAMION", asesor.PerfilId),
            Base(Lead.OrigenContactoVehiculo, null, null, null),
            Base(Lead.OrigenVendeTuCamion, IdDe(pub), null, null),
            Base(Lead.OrigenSeguroTodoRiesgo, null, null, null),
            Base(Lead.OrigenSeguroTodoRiesgo, null, "MOTO", null),
            Base(Lead.OrigenContactoVehiculo, IdDe(pub), "CAMION", asesor.PerfilId),
            Base("CREDITO", null, null, null)
        };
        for (var i = 0; i < malos.Length; i++)
        {
            var malo = malos[i];
            var fallo = await Record.ExceptionAsync(() => _api.Db(db =>
            {
                db.Leads.Add(malo);
                return Task.CompletedTask;
            }));
            Assert.True(fallo is DbUpdateException, $"El caso {i} ({malo.Origen}) no fue rechazado por la base");
        }

        // Los válidos sí entran (la API nunca los crea así a mano; solo se comprueba el CHECK).
        await _api.Db(db =>
        {
            db.Leads.Add(Base(Lead.OrigenContactoVehiculo, IdDe(pub), null, asesor.PerfilId));
            db.Leads.Add(Base(Lead.OrigenSeguroTodoRiesgo, null, "OTRO", null));
            return Task.CompletedTask;
        });

        // Un aviso cerrado con ATENDIDA cabe en CK_Lead_Resultado.
        var aviso = await _api.AvisoNuevo(RutaVende);
        await Esperar(await _api.AccionLead(_api.Admin.Token, aviso.Id, "cerrar", new { resultado = "ATENDIDA" }), HttpStatusCode.OK);
        Assert.Equal(Lead.Atendida, (await _api.LeadDb(aviso.Id)).ResultadoCierre);
    }

    [Fact(DisplayName = "CA-P5b-34: el registro técnico no incluye nombre, teléfono, correo ni descripción de quien envía el aviso")]
    public async Task Logs_sin_datos()
    {
        _api.Hora(T);
        var t = TelefonoUnico();
        var cuerpo = CuerpoVende(t, "Descripción secreta ficticia");
        cuerpo["nombre"] = "Persona Aviso Secreta";
        cuerpo["correo"] = "aviso.secreto@ejemplo.test";
        await _api.AvisoOk(RutaVende, cuerpo);
        var lead = await _api.LeadDeTelefono(t);
        await Esperar(await _api.DetalleLead(_api.Admin.Token, lead.Id), HttpStatusCode.OK);
        foreach (var l in _api.F.Logs.ToArray())
            foreach (var dato in new[] { t, "Persona Aviso Secreta", "aviso.secreto@ejemplo.test", "Descripción secreta ficticia" })
                Assert.DoesNotContain(dato, l);
    }
}
