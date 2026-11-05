using System.Net;
using System.Text;
using Camion.Api.Auth;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;

namespace Camion.Api.Tests;

/// <summary>
/// P5 — contacto público (HU-029, HU-030, RN-CL-001, RN-CL-002, D-3, D-7, D-9, D-44): POST /api/contactos anónimo,
/// validaciones, autorización de datos, campo trampa, tamaño, duplicados, asignación y WhatsApp del detalle (D-47).
/// SQLite en memoria y reloj fijo; nunca MariaDB. Datos ficticios.
/// </summary>
public sealed class ContactosTests(P5Fixture fixture) : IClassFixture<P5Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    [Fact(DisplayName = "CA-P5-01, CA-P5-28: anónimo -> 201 con el mensaje de éxito y sin id ni responsable; lead ABIERTO del asesor dueño; historial CREADO y ASIGNADO (SISTEMA); auditoría Lead/CREAR sin datos del visitante")]
    public async Task Anonimo_crea_lead()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesora Contacto Uno");
        var telefono = TelefonoUnico();
        var r = await _api.Contactar(CuerpoContacto(slug, telefono));
        var texto = await r.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.Equal($"{{\"mensaje\":\"{MsgExito}\"}}", texto);

        var lead = await _api.Leer(db => db.Leads.AsNoTracking().SingleAsync(l => l.PublicacionId == IdDe(pub)));
        Assert.Equal(Lead.Abierto, lead.Estado);
        Assert.Equal(asesor.PerfilId, lead.ResponsableAsesorId);
        Assert.Equal("+57" + telefono, lead.Telefono);
        Assert.Equal("Me interesa este vehículo", lead.Mensaje);   // CA-P5-07: mensaje vacío.
        Assert.Null(lead.Correo);
        Assert.Null(lead.UsuarioId);
        Assert.False(lead.VistoPorResponsable);
        Assert.Equal(T, lead.CreadoEn);

        var eventos = await _api.EventosDb(lead.Id);
        Assert.Equal(new[] { EventoLead.Creado, EventoLead.Asignado }, eventos.Select(e => e.Tipo).ToArray());
        Assert.All(eventos, e => Assert.Null(e.ActorUsuarioId));
        Assert.True(eventos[1].Automatico);
        Assert.Equal(asesor.PerfilId, eventos[1].ResponsableNuevoAsesorId);
        Assert.All(eventos, e => Assert.DoesNotContain(telefono, e.Texto ?? ""));

        var audit = Assert.Single(await _api.AuditoriaLead(lead.Id));
        Assert.Equal("CREAR", audit.Accion);
        Assert.Null(audit.UsuarioId);
        Assert.DoesNotContain(telefono, audit.DatosNuevos!);
        Assert.DoesNotContain("Visitante", audit.DatosNuevos!);
        Assert.Equal(asesor.PerfilId, JsonDe(audit.DatosNuevos)["responsableAsesorId"].GetInt64());
    }

    [Fact(DisplayName = "CA-P5-02, CA-P5-18: un comprador con sesión crea el lead ligado a su cuenta; debe marcar la autorización y cada envío guarda una prueba nueva")]
    public async Task Comprador_con_sesion()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Contacto Dos");
        var compradorId = (await Ayudas.Json(await Ayudas.Me(_api.C, _api.TokenComprador))).GetProperty("usuarioId").GetString();
        var telefono = TelefonoUnico();
        var sinCasilla = CuerpoContacto(slug, telefono);
        sinCasilla["autorizacionDatos"] = false;
        await Error400(await _api.Contactar(sinCasilla, _api.TokenComprador), "autorizacionDatos", MsgAutorizacion, MsgRevisar);

        var lead = await _api.LeadNuevo(slug, telefono, _api.TokenComprador);
        Assert.NotNull(compradorId);
        Assert.Equal(compradorId, lead.UsuarioId);
        await _api.ContactoOk(CuerpoContacto(slug, telefono), _api.TokenComprador);
        var pruebas = await _api.ConsentimientosDb(lead.Id);
        Assert.Equal(2, pruebas.Count);
        Assert.All(pruebas, p => Assert.Equal(lead.UsuarioId, p.UsuarioId));
    }

    [Fact(DisplayName = "CA-P5-02: un token inválido o vencido en el contacto se trata como anónimo")]
    public async Task Token_invalido_es_anonimo()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Contacto Tres");
        var lead = await _api.LeadNuevo(slug, token: "no-es-un-jwt");
        Assert.Null(lead.UsuarioId);
    }

    [Theory(DisplayName = "CA-P5-03: nombre vacío, de 1 o de más de 150 caracteres -> 400 en nombre; no se crea nada")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData(" B ")]
    [InlineData("LARGO151")]
    public async Task Nombre_invalido(string? nombre)
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Nombre");
        var cuerpo = CuerpoContacto(slug);
        cuerpo["nombre"] = nombre == "LARGO151" ? new string('n', 151) : nombre;
        var antes = await _api.TotalLeads();
        await Error400(await _api.Contactar(cuerpo), "nombre", MsgNombre, MsgRevisar);
        Assert.Equal(antes, await _api.TotalLeads());
    }

    [Theory(DisplayName = "CA-P5-04: teléfono vacío, de 7 dígitos, extranjero o que no es celular/fijo colombiano -> 400 en telefono")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData("4123456")]
    [InlineData("2001234567")]
    [InlineData("6101234567")]
    [InlineData("+1 305 555 0100")]
    [InlineData("+34 600123456")]
    [InlineData("300123456")]
    [InlineData("30012345678")]
    [InlineData("300-ABC-4567")]
    [InlineData("3+001234567")]
    [InlineData("+3001234567")]
    public async Task Telefono_invalido(string? telefono)
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Telefono");
        var cuerpo = CuerpoContacto(slug);
        cuerpo["telefono"] = telefono;
        await Error400(await _api.Contactar(cuerpo), "telefono", MsgTelefono, MsgRevisar);
    }

    [Theory(DisplayName = "CA-P5-05: formatos aceptados se guardan como +57XXXXXXXXXX; un fijo que empieza por 60 también")]
    [InlineData("300 123 4567", "+573001234567")]
    [InlineData("300-123-4567", "+573001234567")]
    [InlineData("(300) 1234567", "+573001234567")]
    [InlineData("+57 3001234567", "+573001234567")]
    [InlineData("573001234567", "+573001234567")]
    [InlineData("601 234 5678", "+576012345678")]
    public void Telefono_normalizado(string entrada, string esperado) =>
        Assert.Equal(esperado, ValidacionAuth.NormalizarTelefonoColombia(entrada));

    [Fact(DisplayName = "CA-P5-05, CA-P5-23: el duplicado se detecta con el número normalizado aunque se escriba distinto")]
    public async Task Duplicado_con_formato_distinto()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Formato");
        var digitos = TelefonoUnico();
        var lead = await _api.LeadNuevo(slug, digitos);
        var conFormato = $"+57 ({digitos[..3]}) {digitos[3..6]}-{digitos[6..]}";
        await _api.ContactoOk(CuerpoContacto(slug, conFormato));
        Assert.Equal(1, await _api.Leer(db => db.Leads.CountAsync(l => l.PublicacionId == lead.PublicacionId)));
        Assert.Contains(await _api.EventosDb(lead.Id), e => e.Tipo == EventoLead.ContactoRepetido);
    }

    [Theory(DisplayName = "CA-P5-06: correo opcional; con formato inválido o de más de 254 -> 400 en correo")]
    [InlineData("no-es-correo")]
    [InlineData("a@b")]
    [InlineData("LARGO")]
    public async Task Correo_invalido(string correo)
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Correo");
        var cuerpo = CuerpoContacto(slug);
        cuerpo["correo"] = correo == "LARGO" ? new string('c', 250) + "@x.co" : correo;
        await Error400(await _api.Contactar(cuerpo), "correo", MsgCorreo, MsgRevisar);
    }

    [Fact(DisplayName = "CA-P5-06, CA-P5-07: correo válido se guarda en minúsculas; mensaje de 500 se acepta y de 501 -> 400")]
    public async Task Correo_y_mensaje()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Mensaje");
        var cuerpo = CuerpoContacto(slug);
        cuerpo["correo"] = "  Visitante.Ficticio@Ejemplo.TEST ";
        cuerpo["mensaje"] = new string('m', 500);
        await _api.ContactoOk(cuerpo);
        var lead = await _api.Leer(db => db.Leads.AsNoTracking().Where(l => l.Publicacion!.SlugPublico == slug).SingleAsync());
        Assert.Equal("visitante.ficticio@ejemplo.test", lead.Correo);
        Assert.Equal(500, lead.Mensaje.Length);

        var largo = CuerpoContacto(slug);
        largo["mensaje"] = new string('m', 501);
        await Error400(await _api.Contactar(largo), "mensaje", MsgMensaje, MsgRevisar);
    }

    [Fact(DisplayName = "CA-P5-08: responsable, estado o versión enviados por el cliente se ignoran; sin slug -> 400")]
    public async Task Campos_manipulados()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Manipulado");
        var cuerpo = CuerpoContacto(slug);
        cuerpo["responsableAsesorId"] = _api.B.PerfilId;
        cuerpo["asesorId"] = _api.B.PerfilId;
        cuerpo["estado"] = "CERRADO";
        cuerpo["versionPolitica"] = "VERSION-FALSA";
        await _api.ContactoOk(cuerpo);
        var lead = await _api.Leer(db => db.Leads.AsNoTracking().Where(l => l.Publicacion!.SlugPublico == slug).SingleAsync());
        Assert.Equal(asesor.PerfilId, lead.ResponsableAsesorId);
        Assert.Equal(Lead.Abierto, lead.Estado);
        Assert.Equal(VersionPolitica, Assert.Single(await _api.ConsentimientosDb(lead.Id)).VersionPolitica);

        var sinSlug = CuerpoContacto(null);
        await Error400(await _api.Contactar(sinSlug), "slug", MsgSlug, MsgRevisar);
    }

    [Fact(DisplayName = "CA-P5-09: publicación oculta, vendida, eliminada, vehículo inactivo o slug inexistente -> 404; no se crea lead ni prueba")]
    public async Task No_contactable_404()
    {
        _api.Hora(T);
        var (asesor, _) = await _api.AsesorConPlan("Asesor No Contactable", limite: 10);
        var oculta = await _api.Publicada(asesor);
        await _api.Inactivada(asesor.Token, IdDe(oculta));
        var vendida = await _api.Publicada(asesor);
        await Esperar(await _api.Vender(asesor.Token, vendida.GetProperty("vehiculoId").GetInt64()), HttpStatusCode.OK);
        var eliminada = await _api.Publicada(asesor);
        await _api.Inactivada(asesor.Token, IdDe(eliminada));
        await _api.AccionPubOk(asesor.Token, IdDe(eliminada), "eliminar");
        var inactivo = await _api.Publicada(asesor);
        Assert.Equal(HttpStatusCode.OK, (await _api.Accion(asesor.Token, inactivo.GetProperty("vehiculoId").GetInt64(), "inactivar",
            "Fuera de servicio (prueba)")).StatusCode);

        var leads = await _api.TotalLeads();
        var pruebas = await _api.Leer(db => db.ConsentimientosDatos.CountAsync());
        foreach (var slug in new[] { Slug(oculta), Slug(vendida), Slug(eliminada), Slug(inactivo), "no-existe-123" })
            await Error(await _api.Contactar(CuerpoContacto(slug)), HttpStatusCode.NotFound, MsgNoDisponible);
        await Error400(await _api.Contactar(CuerpoContacto(new string('s', 201))), "slug", MsgSlug, MsgRevisar);
        Assert.Equal(leads, await _api.TotalLeads());
        Assert.Equal(pruebas, await _api.Leer(db => db.ConsentimientosDatos.CountAsync()));
    }

    [Fact(DisplayName = "CA-P5-10: un vehículo RESERVADO se contacta normalmente")]
    public async Task Reservado_se_contacta()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor Reservado");
        Assert.Equal(HttpStatusCode.OK, (await _api.Accion(asesor.Token, pub.GetProperty("vehiculoId").GetInt64(), "reservar")).StatusCode);
        var lead = await _api.LeadNuevo(slug);
        Assert.Equal(asesor.PerfilId, lead.ResponsableAsesorId);
    }

    [Fact(DisplayName = "CA-P5-11: el asesor dueño con sesión -> 409; otro asesor sí puede contactar")]
    public async Task Duenio_no_se_contacta()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Duenio");
        var antes = await _api.TotalLeads();
        await Error(await _api.Contactar(CuerpoContacto(slug), asesor.Token), HttpStatusCode.Conflict, MsgPropio);
        Assert.Equal(antes, await _api.TotalLeads());
        var lead = await _api.LeadNuevo(slug, token: _api.B.Token);
        Assert.Equal(_api.B.UsuarioId, lead.UsuarioId);
    }

    [Theory(DisplayName = "CA-P5-14: autorizacionDatos ausente, null o false -> 400 en autorizacionDatos; no se guarda nada")]
    [InlineData("ausente")]
    [InlineData("null")]
    [InlineData("false")]
    public async Task Sin_autorizacion(string caso)
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Autorizacion");
        var cuerpo = CuerpoContacto(slug);
        if (caso == "ausente") cuerpo.Remove("autorizacionDatos");
        else cuerpo["autorizacionDatos"] = caso == "null" ? null : false;
        var (leads, pruebas) = (await _api.TotalLeads(), await _api.Leer(db => db.ConsentimientosDatos.CountAsync()));
        await Error400(await _api.Contactar(cuerpo), "autorizacionDatos", MsgAutorizacion, MsgRevisar);
        Assert.Equal(leads, await _api.TotalLeads());
        Assert.Equal(pruebas, await _api.Leer(db => db.ConsentimientosDatos.CountAsync()));
    }

    [Fact(DisplayName = "CA-P5-15, CA-P5-19: la prueba guarda fecha UTC, versión del servidor, formulario CONTACTO_VEHICULO y la referencia al lead")]
    public async Task Prueba_de_autorizacion()
    {
        _api.Hora(T.AddMinutes(7));
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Prueba Datos");
        _api.Hora(T.AddMinutes(7));
        var lead = await _api.LeadNuevo(slug);
        var prueba = Assert.Single(await _api.ConsentimientosDb(lead.Id));
        Assert.Equal("CONTACTO_VEHICULO", prueba.Formulario);
        Assert.Equal(VersionPolitica, prueba.VersionPolitica);
        Assert.Equal(T.AddMinutes(7), prueba.AceptadoEn);
        Assert.Equal("Lead", prueba.Entidad);
        Assert.Contains("VENDE_TU_CAMION", ConsentimientoDatos.Formularios);
        Assert.Contains("SEGURO_TODO_RIESGO", ConsentimientoDatos.Formularios);
        Assert.Contains("REGISTRO_COMPRADOR", ConsentimientoDatos.Formularios);
        Assert.Contains("INSCRIPCION_ASESOR", ConsentimientoDatos.Formularios);
    }

    [Fact(DisplayName = "CA-P5-22: campo trampa relleno -> 201 con el mensaje normal y no se guarda nada")]
    public async Task Campo_trampa()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Trampa");
        var cuerpo = CuerpoContacto(slug);
        cuerpo["sitioWeb"] = "http://spam.example";
        var (leads, pruebas) = (await _api.TotalLeads(), await _api.Leer(db => db.ConsentimientosDatos.CountAsync()));
        await _api.ContactoOk(cuerpo);
        // Incluso con datos inválidos el robot no aprende nada.
        await _api.ContactoOk(new Dictionary<string, object?> { ["sitioWeb"] = "x", ["telefono"] = "1" });
        Assert.Equal(leads, await _api.TotalLeads());
        Assert.Equal(pruebas, await _api.Leer(db => db.ConsentimientosDatos.CountAsync()));
    }

    [Fact(DisplayName = "CA-P5-22: cuerpo de más de 8 KB -> 413 (con y sin Content-Length); JSON malformado -> 400 formato")]
    public async Task Tamano_y_formato()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Tamano");
        var cuerpo = CuerpoContacto(slug);
        cuerpo["mensaje"] = new string('m', 8200);
        await Error(await _api.Contactar(cuerpo), HttpStatusCode.RequestEntityTooLarge, MsgGrande);

        // Sin Content-Length (envío por partes).
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/contactos")
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("{\"nombre\":\"" + new string('x', 9000) + "\"}")))
        };
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        req.Headers.TransferEncodingChunked = true;
        await Error(await _api.C.SendAsync(req), HttpStatusCode.RequestEntityTooLarge, MsgGrande);

        var malo = new HttpRequestMessage(HttpMethod.Post, "/api/contactos")
        {
            Content = new StringContent("{\"slug\": ", Encoding.UTF8, "application/json")
        };
        await Error(await _api.C.SendAsync(malo), HttpStatusCode.BadRequest, InventarioApi.MsgFormato);
        var tipo = new HttpRequestMessage(HttpMethod.Post, "/api/contactos")
        {
            Content = new StringContent("{\"autorizacionDatos\": \"si\"}", Encoding.UTF8, "application/json")
        };
        await Error(await _api.C.SendAsync(tipo), HttpStatusCode.BadRequest, InventarioApi.MsgFormato);
        await Error(await _api.C.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/contactos")), HttpStatusCode.BadRequest, InventarioApi.MsgFormato);
    }

    [Fact(DisplayName = "CA-P5-23: mismo teléfono y publicación con lead ABIERTO -> sin lead nuevo; evento CONTACTO_REPETIDO con el mensaje y prueba nueva; la misma respuesta 201")]
    public async Task Contacto_repetido()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Repetido");
        var telefono = TelefonoUnico();
        var lead = await _api.LeadNuevo(slug, telefono);
        _api.Hora(T.AddHours(2));
        var otra = CuerpoContacto(slug, telefono);
        otra["mensaje"] = "¿Sigue disponible? (prueba)";
        await _api.ContactoOk(otra);
        Assert.Equal(1, await _api.Leer(db => db.Leads.CountAsync(l => l.PublicacionId == lead.PublicacionId)));
        var repetido = Assert.Single(await _api.EventosDb(lead.Id), e => e.Tipo == EventoLead.ContactoRepetido);
        Assert.Equal("¿Sigue disponible? (prueba)", repetido.Texto);
        Assert.Equal(T.AddHours(2), repetido.CreadoEn);
        Assert.Equal(2, (await _api.ConsentimientosDb(lead.Id)).Count);
        Assert.Single(await _api.AuditoriaLead(lead.Id));
    }

    [Fact(DisplayName = "CA-P5-24: si el lead anterior de ese teléfono y publicación está CERRADO se crea uno nuevo")]
    public async Task Nuevo_si_el_anterior_esta_cerrado()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Cerrado Antes");
        var telefono = TelefonoUnico();
        var lead = await _api.LeadNuevo(slug, telefono);
        await Esperar(await _api.AccionLead(asesor.Token, lead.Id, "cerrar", new { resultado = "NO_RESPONDIO" }), HttpStatusCode.OK);
        var nuevo = await _api.LeadNuevo(slug, telefono);
        Assert.NotEqual(lead.Id, nuevo.Id);
        Assert.Equal(Lead.Abierto, nuevo.Estado);
        Assert.Equal(asesor.PerfilId, nuevo.ResponsableAsesorId);
    }

    [Fact(DisplayName = "CA-P5-26: publicación PUBLICADA con el plan del asesor ya vencido -> se acepta y va a Administración, con el mismo mensaje")]
    public async Task Plan_vencido_va_a_administracion()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Plan Vencido");
        _api.Hora(T.AddDays(40));
        Assert.Equal(HttpStatusCode.NotFound, (await _api.DetallePortal(slug)).StatusCode); // ya no se ve (D-41)
        var lead = await _api.LeadNuevo(slug);
        Assert.Null(lead.ResponsableAsesorId);
        var asignado = Assert.Single(await _api.EventosDb(lead.Id), e => e.Tipo == EventoLead.Asignado);
        Assert.Null(asignado.ResponsableNuevoAsesorId);
    }

    [Fact(DisplayName = "CA-P5-27: publicación de un asesor Bloqueado -> el lead va a Administración")]
    public async Task Asesor_bloqueado_va_a_administracion()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Bloqueado Contacto");
        Assert.Equal(HttpStatusCode.OK, (await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, asesor.UsuarioId, "bloquear",
            "Incumplimiento (prueba)")).StatusCode);
        var lead = await _api.LeadNuevo(slug);
        Assert.Null(lead.ResponsableAsesorId);
    }

    [Fact(DisplayName = "D-47 (CA-P5-62), contrato P5: publicación cuyo dueño es administrador -> lead a Administración y WhatsApp del portal")]
    public async Task Duenio_administrador()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Promovido");
        await _api.Db(async db =>
        {
            var rol = await db.Roles.SingleAsync(r => r.Name == "ADMINISTRADOR");
            db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = asesor.UsuarioId, RoleId = rol.Id });
        });
        var lead = await _api.LeadNuevo(slug);
        Assert.Null(lead.ResponsableAsesorId);
        var detalle = await Esperar(await _api.DetallePortal(slug), HttpStatusCode.OK);
        Assert.Equal("https://wa.me/573166152907", detalle.GetProperty("whatsapp").GetString());
    }

    [Fact(DisplayName = "CA-P5-61: el detalle público trae solo whatsapp = wa.me/57 + celular del asesor; nunca su correo, documento ni nombre")]
    public async Task Whatsapp_del_asesor()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesora Whatsapp Visible");
        var r = await _api.DetallePortal(slug);
        var detalle = await Esperar(r, HttpStatusCode.OK);
        Assert.Equal("https://wa.me/573001234567", detalle.GetProperty("whatsapp").GetString());
        var texto = await r.Content.ReadAsStringAsync();
        var perfil = await _api.Leer(db => db.PerfilesAsesor.AsNoTracking().SingleAsync(p => p.Id == asesor.PerfilId));
        foreach (var prohibido in new[] { asesor.Email, perfil.NumeroDocumento, asesor.Nombre, asesor.UsuarioId, "300 123 4567" })
            Assert.DoesNotContain(prohibido, texto, StringComparison.Ordinal);
        foreach (var clave in new[] { "telefono", "email", "correo", "asesor", "asesorId", "numeroDocumento" })
            Assert.False(detalle.TryGetProperty(clave, out _), clave);
    }

    [Theory(DisplayName = "CA-P5-62: asesor sin teléfono válido o con un fijo -> WhatsApp del portal (316 615 2907)")]
    [InlineData(null)]
    [InlineData("1234567")]
    [InlineData("601 234 5678")]
    public async Task Whatsapp_del_portal(string? telefono)
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Sin Celular");
        await _api.Db(async db => (await db.Users.SingleAsync(u => u.Id == asesor.UsuarioId)).PhoneNumber = telefono);
        var detalle = await Esperar(await _api.DetallePortal(slug), HttpStatusCode.OK);
        Assert.Equal("https://wa.me/573166152907", detalle.GetProperty("whatsapp").GetString());
    }

    [Fact(DisplayName = "CA-P5-63: publicación no visible (plan vencido o vendida) -> 404 sin teléfono")]
    public async Task Whatsapp_no_visible()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor No Visible");
        _api.Hora(T.AddDays(40));
        var r = await _api.DetallePortal(slug);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.DoesNotContain("wa.me", await r.Content.ReadAsStringAsync());
        _api.Hora(T);
        await Esperar(await _api.Vender(asesor.Token, pub.GetProperty("vehiculoId").GetInt64()), HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.DetallePortal(slug)).StatusCode);
    }

    [Fact(DisplayName = "CA-P5-35: el catálogo público nunca muestra leads, cuántos contactos tiene un vehículo ni quién contactó")]
    public async Task Catalogo_sin_leads()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor Catalogo Sin Leads");
        var cuerpo = CuerpoContacto(slug);
        cuerpo["nombre"] = "Nombre Muy Unico Ficticio";
        await _api.ContactoOk(cuerpo);
        foreach (var url in new[] { "/api/catalogo/inicio", "/api/catalogo/publicaciones?tamano=48", "/api/catalogo/publicaciones/" + slug })
        {
            var texto = await (await _api.Enviar(HttpMethod.Get, url, null)).Content.ReadAsStringAsync();
            Assert.DoesNotContain("Nombre Muy Unico Ficticio", texto);
            Assert.DoesNotContain((string)cuerpo["telefono"]!, texto);
            Assert.DoesNotContain("lead", texto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("contactos", texto, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact(DisplayName = "CA-P5-59: el registro técnico no incluye nombre, teléfono, correo ni mensaje del visitante")]
    public async Task Logs_sin_datos_del_visitante()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Logs");
        var telefono = TelefonoUnico();
        var cuerpo = CuerpoContacto(slug, telefono);
        cuerpo["nombre"] = "Persona Secreta Ficticia";
        cuerpo["correo"] = "secreto.ficticio@ejemplo.test";
        cuerpo["mensaje"] = "Mensaje secreto ficticio";
        await _api.ContactoOk(cuerpo);
        var lead = await _api.Leer(db => db.Leads.AsNoTracking().SingleAsync(l => l.Telefono == "+57" + telefono));
        await Esperar(await _api.DetalleLead(asesor.Token, lead.Id), HttpStatusCode.OK);
        await Esperar(await _api.AccionLead(asesor.Token, lead.Id, "notas", new { texto = "Llamé y no contestó (prueba)" }), HttpStatusCode.Created);
        foreach (var l in _api.F.Logs.ToArray())
            foreach (var dato in new[] { telefono, "Persona Secreta Ficticia", "secreto.ficticio@ejemplo.test", "Mensaje secreto ficticio" })
                Assert.DoesNotContain(dato, l);
    }
}
