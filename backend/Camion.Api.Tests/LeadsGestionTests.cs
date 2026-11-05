using System.Net;
using System.Text.Json;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;

namespace Camion.Api.Tests;

/// <summary>
/// P5 — gestión de leads (HU-031 a HU-033, RN-CL-003, RN-CL-004, RN-SE-003, RN-SE-006, D-7, D-48): permisos por rol,
/// lista y filtros, detalle con historial, notas, cierre, reapertura, reasignación manual y aviso de nuevos.
/// SQLite en memoria y reloj fijo; nunca MariaDB. Datos ficticios.
/// </summary>
public sealed class LeadsGestionTests(P5Fixture fixture) : IClassFixture<P5Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    private static readonly string[] RutasLectura = ["/api/leads", "/api/leads/resumen", "/api/leads/1"];
    private static readonly string[] Acciones = ["notas", "cerrar", "reabrir", "reasignar"];

    [Fact(DisplayName = "CA-P5-32, CA-P5-33, CA-P5-45: sin sesión -> 401 y comprador -> 403 en toda ruta de /api/leads; asesor -> 403 en reabrir y reasignar")]
    public async Task Permisos_por_rol()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Permisos Leads");
        var lead = await _api.LeadNuevo(slug);
        foreach (var url in RutasLectura.Append($"/api/leads/{lead.Id}"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Enviar(HttpMethod.Get, url, null)).StatusCode);
            await Error(await _api.Enviar(HttpMethod.Get, url, _api.TokenComprador), HttpStatusCode.Forbidden, MsgSinPermiso);
        }
        foreach (var accion in Acciones)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _api.AccionLead(null, lead.Id, accion, new { })).StatusCode);
            await Error(await _api.AccionLead(_api.TokenComprador, lead.Id, accion, new { }), HttpStatusCode.Forbidden, MsgSinPermiso);
        }
        await Error(await _api.AccionLead(asesor.Token, lead.Id, "reabrir", new { motivo = MotivoP5 }), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.ReasignarLead(asesor.Token, lead.Id, _api.B.PerfilId), HttpStatusCode.Forbidden, MsgSinPermiso);
        Assert.Equal(asesor.PerfilId, (await _api.LeadDb(lead.Id)).ResponsableAsesorId);
    }

    [Fact(DisplayName = "CA-P5-34: un asesor bloqueado no entra (401 con su token anterior), igual que en el resto del panel")]
    public async Task Asesor_bloqueado_sin_acceso()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Sin Acceso");
        await _api.LeadNuevo(slug);
        Assert.Equal(HttpStatusCode.OK, (await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, asesor.UsuarioId, "bloquear",
            "Incumplimiento (prueba)")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.ListaLeads(asesor.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.ListaLeads(AyudasP2.Firmar(_api.F, _api.Pendiente.Id, 0, "ASESOR"))).StatusCode);
    }

    [Fact(DisplayName = "CA-P5-29, CA-P5-30: el asesor ve solo los suyos, del más reciente al más antiguo, con sus datos; lo ajeno -> 404")]
    public async Task Asesor_ve_solo_lo_suyo()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesora Solo Lo Suyo");
        var (otro, _, slugOtro) = await _api.AsesorPublicado("Asesor Ajeno");
        var primero = await _api.LeadNuevo(slug);
        _api.Hora(T.AddMinutes(5));
        var cuerpo = CuerpoContacto(slug);
        cuerpo["correo"] = "comprador.ficticio@ejemplo.test";
        cuerpo["mensaje"] = "Quiero verlo el sábado (prueba)";
        await _api.ContactoOk(cuerpo);
        var ajeno = await _api.LeadNuevo(slugOtro);

        var json = await Esperar(await _api.ListaLeads(asesor.Token), HttpStatusCode.OK);
        var items = json.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, json.GetProperty("total").GetInt32());
        Assert.Equal(primero.Id, items[1].GetProperty("id").GetInt64());
        var reciente = items[0];
        Assert.Equal("Visitante Ficticio", reciente.GetProperty("nombre").GetString());
        Assert.StartsWith("+573", reciente.GetProperty("telefono").GetString());
        Assert.Equal("comprador.ficticio@ejemplo.test", reciente.GetProperty("correo").GetString());
        Assert.Equal("Quiero verlo el sábado (prueba)", reciente.GetProperty("mensaje").GetString());
        Assert.Equal("ABIERTO", reciente.GetProperty("estado").GetString());
        Assert.True(reciente.GetProperty("nuevo").GetBoolean());
        var vehiculo = reciente.GetProperty("vehiculo");
        Assert.Equal(IdDe(pub), vehiculo.GetProperty("publicacionId").GetInt64());
        Assert.Equal(slug, vehiculo.GetProperty("slug").GetString());
        Assert.Equal("ACTIVO", vehiculo.GetProperty("estadoVehiculo").GetString());
        Assert.Equal("PUBLICADA", vehiculo.GetProperty("estadoPublicacion").GetString());
        var responsable = reciente.GetProperty("responsable");
        Assert.Equal("ASESOR", responsable.GetProperty("tipo").GetString());
        Assert.Equal(asesor.PerfilId, responsable.GetProperty("asesorId").GetInt64());

        Assert.DoesNotContain(ajeno.Id, await _api.IdsLeads(asesor.Token));
        await Error(await _api.DetalleLead(asesor.Token, ajeno.Id), HttpStatusCode.NotFound, MsgLeadNoEncontrado);
        await Error(await _api.AccionLead(asesor.Token, ajeno.Id, "notas", new { texto = "Intento ajeno (prueba)" }),
            HttpStatusCode.NotFound, MsgLeadNoEncontrado);
        await Error(await _api.AccionLead(asesor.Token, ajeno.Id, "cerrar", new { resultado = "OTRO", observacion = "nada" }),
            HttpStatusCode.NotFound, MsgLeadNoEncontrado);
        await Error(await _api.DetalleLead(asesor.Token, 999_999), HttpStatusCode.NotFound, MsgLeadNoEncontrado);
        Assert.Contains(ajeno.Id, await _api.IdsLeads(otro.Token));
    }

    [Fact(DisplayName = "CA-P5-30: un lead de Administración da 404 al asesor dueño del vehículo")]
    public async Task Administracion_404_al_asesor()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Lead Admin");
        var lead = await _api.LeadNuevo(slug);
        await Esperar(await _api.ReasignarLead(_api.Admin.Token, lead.Id, null, administracion: true), HttpStatusCode.OK);
        await Error(await _api.DetalleLead(asesor.Token, lead.Id), HttpStatusCode.NotFound, MsgLeadNoEncontrado);
    }

    [Fact(DisplayName = "CA-P5-29, CA-P5-31: filtros por estado, responsable, vehículo y fechas; valores inválidos -> 400")]
    public async Task Filtros()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor Filtros");
        var a = await _api.LeadNuevo(slug);
        _api.Hora(T.AddDays(2));
        var b = await _api.LeadNuevo(slug);
        await Esperar(await _api.AccionLead(asesor.Token, b.Id, "cerrar", new { resultado = "NO_INTERESADO" }), HttpStatusCode.OK);
        var c = await _api.LeadNuevo(slug);
        await Esperar(await _api.ReasignarLead(_api.Admin.Token, c.Id, null, administracion: true), HttpStatusCode.OK);

        Assert.Equal(new[] { a.Id }, await _api.IdsLeads(asesor.Token, "?estado=abierto"));
        Assert.Equal(new[] { b.Id }, await _api.IdsLeads(asesor.Token, "?estado=CERRADO"));
        var admin = _api.Admin.Token;
        var porVehiculo = $"&vehiculoId={pub.GetProperty("vehiculoId").GetInt64()}";
        Assert.Equal(new[] { c.Id, b.Id, a.Id }, await _api.IdsLeads(admin, "?publicacionId=" + IdDe(pub)));
        Assert.Equal(new[] { c.Id }, await _api.IdsLeads(admin, "?responsable=ADMINISTRACION" + porVehiculo));
        Assert.Equal(new[] { b.Id, a.Id }, await _api.IdsLeads(admin, $"?responsable={asesor.PerfilId}"));
        Assert.Equal(new[] { b.Id, a.Id }, await _api.IdsLeads(asesor.Token, "?responsable=ADMINISTRACION")); // se ignora
        // T = 2026-10-26 15:00 UTC (10:00 en Colombia); T+2 días = 2026-10-28.
        Assert.Equal(new[] { a.Id }, await _api.IdsLeads(admin, "?hasta=2026-10-26" + porVehiculo));
        Assert.Equal(new[] { c.Id, b.Id }, await _api.IdsLeads(admin, "?desde=2026-10-27&hasta=2026-10-28" + porVehiculo));
        Assert.Empty(await _api.IdsLeads(admin, "?desde=2026-10-29" + porVehiculo));

        await Error400(await _api.ListaLeads(admin, "?estado=PENDIENTE"), "estado", "Estado no válido.", MsgFiltros);
        await Error400(await _api.ListaLeads(admin, "?responsable=abc"), "responsable", "Responsable no válido.", MsgFiltros);
        await Error400(await _api.ListaLeads(admin, "?vehiculoId=0"), "vehiculoId", "Vehículo no válido.", MsgFiltros);
        await Error400(await _api.ListaLeads(admin, "?desde=26/10/2026"), "desde", "Escribe una fecha válida (aaaa-mm-dd).", MsgFiltros);
        await Error400(await _api.ListaLeads(admin, "?desde=2026-10-28&hasta=2026-10-27"), "hasta",
            "La fecha final debe ser igual o posterior a la inicial.", MsgFiltros);
        await Error400(await _api.ListaLeads(admin, "?tamano=101"), "tamano", "Página o tamaño no válidos.", MsgFiltros);
        var pagina = await Esperar(await _api.ListaLeads(admin, "?tamano=1&pagina=2" + porVehiculo), HttpStatusCode.OK);
        Assert.Equal(3, pagina.GetProperty("total").GetInt32());
        Assert.Equal(b.Id, Assert.Single(pagina.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64());
    }

    [Fact(DisplayName = "CA-P5-31: el administrador ve todos los leads, de todos los asesores y de Administración")]
    public async Task Admin_ve_todos()
    {
        _api.Hora(T);
        var (_, _, slug1) = await _api.AsesorPublicado("Asesor Todos Uno");
        var (_, _, slug2) = await _api.AsesorPublicado("Asesor Todos Dos");
        var l1 = await _api.LeadNuevo(slug1);
        var l2 = await _api.LeadNuevo(slug2);
        var ids = await _api.IdsLeads(_api.Admin.Token, "?tamano=100");
        Assert.Contains(l1.Id, ids);
        Assert.Contains(l2.Id, ids);
        Assert.Equal(HttpStatusCode.OK, (await _api.DetalleLead(_api.Admin.Token, l1.Id)).StatusCode);
    }

    [Fact(DisplayName = "CA-P5-58 (D-48): resumen { nuevos, abiertos }; abrir el detalle como responsable lo marca visto; el admin no lo marca por el asesor")]
    public async Task Nuevos()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesora Nuevos");
        var l1 = await _api.LeadNuevo(slug);
        var l2 = await _api.LeadNuevo(slug);
        var r = await _api.Resumen(asesor.Token);
        Assert.Equal(2, r.GetProperty("nuevos").GetInt32());
        Assert.Equal(2, r.GetProperty("abiertos").GetInt32());

        var admin = await Esperar(await _api.DetalleLead(_api.Admin.Token, l1.Id), HttpStatusCode.OK);
        Assert.False(admin.GetProperty("nuevo").GetBoolean()); // para el admin no es "suyo"
        Assert.Equal(2, (await _api.Resumen(asesor.Token)).GetProperty("nuevos").GetInt32());

        var visto = await Esperar(await _api.DetalleLead(asesor.Token, l1.Id), HttpStatusCode.OK);
        Assert.False(visto.GetProperty("nuevo").GetBoolean());
        Assert.True((await _api.LeadDb(l1.Id)).VistoPorResponsable);
        Assert.Equal(1, (await _api.Resumen(asesor.Token)).GetProperty("nuevos").GetInt32());

        // Al administrador le cuentan los de Administración; al pasar a Administración vuelve a ser nuevo.
        var antes = (await _api.Resumen(_api.Admin.Token)).GetProperty("nuevos").GetInt32();
        await Esperar(await _api.ReasignarLead(_api.Admin.Token, l1.Id, null, administracion: true), HttpStatusCode.OK);
        Assert.Equal(antes + 1, (await _api.Resumen(_api.Admin.Token)).GetProperty("nuevos").GetInt32());
        Assert.Equal(1, (await _api.Resumen(asesor.Token)).GetProperty("abiertos").GetInt32());
        await Esperar(await _api.DetalleLead(_api.Admin.Token, l1.Id), HttpStatusCode.OK);
        Assert.Equal(antes, (await _api.Resumen(_api.Admin.Token)).GetProperty("nuevos").GetInt32());
        // Cerrado ya no cuenta.
        await Esperar(await _api.AccionLead(asesor.Token, l2.Id, "cerrar", new { resultado = "NO_RESPONDIO" }), HttpStatusCode.OK);
        Assert.Equal(0, (await _api.Resumen(asesor.Token)).GetProperty("nuevos").GetInt32());
    }

    [Fact(DisplayName = "CA-P5-37, CA-P5-38: nota 5-500 -> 201 en el historial con autor; corta o larga -> 400; lead cerrado -> 409")]
    public async Task Notas()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Notas");
        var lead = await _api.LeadNuevo(slug);
        _api.Hora(T.AddHours(1));
        var json = await Esperar(await _api.AccionLead(asesor.Token, lead.Id, "notas", new { texto = "  Llamé, pidió fotos (prueba) " }),
            HttpStatusCode.Created);
        var nota = json.GetProperty("eventos").EnumerateArray().Last();
        Assert.Equal("NOTA", nota.GetProperty("tipo").GetString());
        Assert.Equal("Llamé, pidió fotos (prueba)", nota.GetProperty("texto").GetString());
        Assert.Equal("USUARIO", nota.GetProperty("actor").GetProperty("tipo").GetString());
        Assert.Equal(asesor.Nombre, nota.GetProperty("actor").GetProperty("nombre").GetString());
        Assert.Equal(T.AddHours(1), Fecha(nota, "fecha"));
        await Esperar(await _api.AccionLead(_api.Admin.Token, lead.Id, "notas", new { texto = "Nota del administrador (prueba)" }),
            HttpStatusCode.Created);
        Assert.Contains(await _api.AuditoriaLead(lead.Id), a => a.Accion == "NOTA" && a.UsuarioId == asesor.UsuarioId);

        foreach (var texto in new string?[] { null, "abcd", "    abcd   ", new string('n', 501) })
            await Error400(await _api.AccionLead(asesor.Token, lead.Id, "notas", new { texto }), "texto", MsgNota, MsgRevisar);
        await Esperar(await _api.AccionLead(asesor.Token, lead.Id, "cerrar", new { resultado = "VENTA_CONCRETADA" }), HttpStatusCode.OK);
        await Error(await _api.AccionLead(asesor.Token, lead.Id, "notas", new { texto = "Después del cierre (prueba)" }),
            HttpStatusCode.Conflict, MsgCerrado);
        Assert.Equal(2, (await _api.EventosDb(lead.Id)).Count(e => e.Tipo == EventoLead.Nota));
    }

    [Fact(DisplayName = "CA-P5-39, CA-P5-40: cerrar con resultado -> CERRADO con resultado, fecha y quién; OTRO exige observación; resultado inválido -> 400; doble cierre -> 409")]
    public async Task Cerrar()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Cierre");
        var lead = await _api.LeadNuevo(slug);
        await Error400(await _api.AccionLead(asesor.Token, lead.Id, "cerrar", new { resultado = "GANADO" }), "resultado", MsgResultado, MsgRevisar);
        await Error400(await _api.AccionLead(asesor.Token, lead.Id, "cerrar", new { }), "resultado", MsgResultado, MsgRevisar);
        await Error400(await _api.AccionLead(asesor.Token, lead.Id, "cerrar", new { resultado = "OTRO" }), "observacion",
            MsgObservacionOtro, MsgRevisar);
        await Error400(await _api.AccionLead(asesor.Token, lead.Id, "cerrar", new { resultado = "OTRO", observacion = "abc" }),
            "observacion", MsgObservacionOtro, MsgRevisar);
        await Error400(await _api.AccionLead(asesor.Token, lead.Id, "cerrar", new { resultado = "NO_RESPONDIO", observacion = new string('o', 501) }),
            "observacion", "La observación no puede tener más de 500 caracteres.", MsgRevisar);
        Assert.Equal(Lead.Abierto, (await _api.LeadDb(lead.Id)).Estado);

        _api.Hora(T.AddDays(1));
        var json = await Esperar(await _api.AccionLead(asesor.Token, lead.Id, "cerrar",
            new { resultado = "otro", observacion = "Compró en otro lado (prueba)" }), HttpStatusCode.OK);
        Assert.Equal("CERRADO", json.GetProperty("estado").GetString());
        Assert.Equal("OTRO", json.GetProperty("resultadoCierre").GetString());
        Assert.Equal("Compró en otro lado (prueba)", json.GetProperty("observacionCierre").GetString());
        Assert.Equal(asesor.Nombre, json.GetProperty("cerradoPor").GetString());
        Assert.Equal(T.AddDays(1), Fecha(json, "cerradoEn"));
        var evento = json.GetProperty("eventos").EnumerateArray().Last();
        Assert.Equal("CERRADO", evento.GetProperty("tipo").GetString());
        Assert.Equal("OTRO", evento.GetProperty("resultado").GetString());
        var audit = Assert.Single(await _api.AuditoriaLead(lead.Id), a => a.Accion == "CERRAR");
        Assert.Equal(asesor.UsuarioId, audit.UsuarioId);
        Assert.Equal("OTRO", JsonDe(audit.DatosNuevos)["resultado"].GetString());

        await Error(await _api.AccionLead(asesor.Token, lead.Id, "cerrar", new { resultado = "NO_RESPONDIO" }), HttpStatusCode.Conflict, MsgYaCerrado);
        await Error(await _api.AccionLead(_api.Admin.Token, lead.Id, "cerrar", new { resultado = "NO_RESPONDIO" }), HttpStatusCode.Conflict, MsgYaCerrado);
    }

    [Fact(DisplayName = "CA-P5-41: el administrador reabre con motivo -> ABIERTO con el mismo responsable, historial y auditoría; abierto -> 409; sin motivo -> 400")]
    public async Task Reabrir()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Reabrir");
        var lead = await _api.LeadNuevo(slug);
        await Error(await _api.AccionLead(_api.Admin.Token, lead.Id, "reabrir", new { motivo = MotivoP5 }), HttpStatusCode.Conflict, MsgYaAbierto);
        await Esperar(await _api.AccionLead(asesor.Token, lead.Id, "cerrar", new { resultado = "NO_INTERESADO" }), HttpStatusCode.OK);
        await Error400(await _api.AccionLead(_api.Admin.Token, lead.Id, "reabrir", new { motivo = "abc" }), "motivo", MsgMotivo, MsgRevisar);

        var json = await Esperar(await _api.AccionLead(_api.Admin.Token, lead.Id, "reabrir", new { motivo = "Volvió a llamar (prueba)" }),
            HttpStatusCode.OK);
        Assert.Equal("ABIERTO", json.GetProperty("estado").GetString());
        Assert.Equal(asesor.PerfilId, json.GetProperty("responsable").GetProperty("asesorId").GetInt64());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("resultadoCierre").ValueKind);
        var tipos = json.GetProperty("eventos").EnumerateArray().Select(e => e.GetProperty("tipo").GetString()).ToArray();
        Assert.Equal(new[] { "CREADO", "ASIGNADO", "CERRADO", "REABIERTO" }, tipos);
        var cierre = json.GetProperty("eventos").EnumerateArray().ElementAt(2);
        Assert.Equal("NO_INTERESADO", cierre.GetProperty("resultado").GetString()); // el historial lo conserva
        var audit = Assert.Single(await _api.AuditoriaLead(lead.Id), a => a.Accion == "REABRIR");
        Assert.Equal(_api.Admin.Id, audit.UsuarioId);
        Assert.Equal("NO_INTERESADO", JsonDe(audit.DatosAnteriores)["resultado"].GetString());
        Assert.Contains(lead.Id, await _api.IdsLeads(asesor.Token, "?estado=ABIERTO"));
    }

    [Fact(DisplayName = "CA-P5-44, CA-P5-48: reasignar a otro asesor aprobado con motivo -> 200; historial y auditoría de quién, cuándo, de quién a quién; el anterior deja de verlo (404) y el nuevo lo ve con su historial")]
    public async Task Reasignar_a_asesor()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Reasignar Origen");
        var lead = await _api.LeadNuevo(slug);
        await Esperar(await _api.AccionLead(asesor.Token, lead.Id, "notas", new { texto = "Primera llamada (prueba)" }), HttpStatusCode.Created);
        _api.Hora(T.AddHours(3));
        var json = await Esperar(await _api.ReasignarLead(_api.Admin.Token, lead.Id, _api.B.PerfilId, motivo: "Zona del otro asesor (prueba)"),
            HttpStatusCode.OK);
        Assert.Equal(_api.B.PerfilId, json.GetProperty("responsable").GetProperty("asesorId").GetInt64());
        var ev = json.GetProperty("eventos").EnumerateArray().Last();
        Assert.Equal("REASIGNADO", ev.GetProperty("tipo").GetString());
        Assert.False(ev.GetProperty("automatico").GetBoolean());
        Assert.Equal(asesor.PerfilId, ev.GetProperty("responsableAnterior").GetProperty("asesorId").GetInt64());
        Assert.Equal(_api.B.PerfilId, ev.GetProperty("responsableNuevo").GetProperty("asesorId").GetInt64());
        Assert.Equal("Zona del otro asesor (prueba)", ev.GetProperty("texto").GetString());

        var audit = Assert.Single(await _api.AuditoriaLead(lead.Id), a => a.Accion == "REASIGNAR");
        Assert.Equal(_api.Admin.Id, audit.UsuarioId);
        Assert.Equal(T.AddHours(3), audit.CreadoEn);
        Assert.Equal(asesor.PerfilId, JsonDe(audit.DatosAnteriores)["responsableAsesorId"].GetInt64());
        Assert.Equal(_api.B.PerfilId, JsonDe(audit.DatosNuevos)["responsableAsesorId"].GetInt64());
        Assert.Equal("Zona del otro asesor (prueba)", JsonDe(audit.DatosNuevos)["motivo"].GetString());

        await Error(await _api.DetalleLead(asesor.Token, lead.Id), HttpStatusCode.NotFound, MsgLeadNoEncontrado);
        var nuevo = await Esperar(await _api.DetalleLead(_api.B.Token, lead.Id), HttpStatusCode.OK);
        Assert.Contains(nuevo.GetProperty("eventos").EnumerateArray(), e => e.GetProperty("texto").GetString() == "Primera llamada (prueba)");
        var lDb = await _api.LeadDb(lead.Id);
        Assert.True(lDb.AsignacionManual);
    }

    [Fact(DisplayName = "CA-P5-44, CA-P5-46: reasignar a Administración; validaciones -> 400; mismo responsable o cerrado -> 409")]
    public async Task Reasignar_validaciones()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Reasignar Validaciones");
        var lead = await _api.LeadNuevo(slug);
        var admin = _api.Admin.Token;
        await Error400(await _api.ReasignarLead(admin, lead.Id, null), "asesorId", MsgAsesorAprobado, MsgRevisar);
        await Error400(await _api.ReasignarLead(admin, lead.Id, _api.B.PerfilId, administracion: true), "asesorId", MsgAsesorAprobado, MsgRevisar);
        await Error400(await _api.ReasignarLead(admin, lead.Id, _api.Pendiente.PerfilId), "asesorId", MsgAsesorAprobado, MsgRevisar);
        await Error400(await _api.ReasignarLead(admin, lead.Id, 999_999), "asesorId", MsgAsesorAprobado, MsgRevisar);
        await Error400(await _api.ReasignarLead(admin, lead.Id, _api.B.PerfilId, motivo: "abc"), "motivo", MsgMotivo, MsgRevisar);
        await Error(await _api.ReasignarLead(admin, lead.Id, asesor.PerfilId), HttpStatusCode.Conflict, MsgMismoResponsable);
        await Error(await _api.ReasignarLead(admin, 999_999, _api.B.PerfilId), HttpStatusCode.NotFound, MsgLeadNoEncontrado);
        Assert.DoesNotContain(await _api.AuditoriaLead(lead.Id), a => a.Accion == "REASIGNAR");

        var json = await Esperar(await _api.ReasignarLead(admin, lead.Id, null, administracion: true), HttpStatusCode.OK);
        Assert.Equal("ADMINISTRACION", json.GetProperty("responsable").GetProperty("tipo").GetString());
        Assert.False((await _api.LeadDb(lead.Id)).AsignacionManual);
        await Error(await _api.ReasignarLead(admin, lead.Id, null, administracion: true), HttpStatusCode.Conflict, MsgMismoResponsable);

        await Esperar(await _api.AccionLead(admin, lead.Id, "cerrar", new { resultado = "DUPLICADO_INVALIDO" }), HttpStatusCode.OK);
        await Error(await _api.ReasignarLead(admin, lead.Id, _api.B.PerfilId), HttpStatusCode.Conflict, MsgCerrado);
    }

    [Fact(DisplayName = "CA-P5-42, CA-P5-16: el detalle muestra el historial en orden con actor SISTEMA/USUARIO; solo el admin ve la prueba de autorización")]
    public async Task Detalle_con_historial()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Historial");
        var telefono = TelefonoUnico();
        var lead = await _api.LeadNuevo(slug, telefono);
        _api.Hora(T.AddMinutes(1));
        await _api.ContactoOk(CuerpoContacto(slug, telefono));
        _api.Hora(T.AddMinutes(2));
        await Esperar(await _api.AccionLead(asesor.Token, lead.Id, "notas", new { texto = "Nota de historial (prueba)" }), HttpStatusCode.Created);

        var json = await Esperar(await _api.DetalleLead(asesor.Token, lead.Id), HttpStatusCode.OK);
        var eventos = json.GetProperty("eventos").EnumerateArray().ToArray();
        Assert.Equal(new[] { "CREADO", "ASIGNADO", "CONTACTO_REPETIDO", "NOTA" },
            eventos.Select(e => e.GetProperty("tipo").GetString()).ToArray());
        Assert.Equal("SISTEMA", eventos[0].GetProperty("actor").GetProperty("tipo").GetString());
        Assert.Equal("ASESOR", eventos[1].GetProperty("responsableNuevo").GetProperty("tipo").GetString());
        Assert.True(eventos[1].GetProperty("automatico").GetBoolean());
        Assert.Equal("USUARIO", eventos[3].GetProperty("actor").GetProperty("tipo").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("consentimientos").ValueKind);

        var admin = await Esperar(await _api.DetalleLead(_api.Admin.Token, lead.Id), HttpStatusCode.OK);
        var pruebas = admin.GetProperty("consentimientos").EnumerateArray().ToArray();
        Assert.Equal(2, pruebas.Length);
        Assert.All(pruebas, p =>
        {
            Assert.Equal("CONTACTO_VEHICULO", p.GetProperty("formulario").GetString());
            Assert.Equal(VersionPolitica, p.GetProperty("versionPolitica").GetString());
        });
        Assert.Equal(T, Fecha(pruebas[0], "aceptadoEn"));
    }

    [Fact(DisplayName = "CA-P5-49: si el vehículo se vende el lead sigue ABIERTO con su responsable y el detalle muestra el estado actual")]
    public async Task Vehiculo_vendido()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor Vendio");
        var lead = await _api.LeadNuevo(slug);
        await Esperar(await _api.Vender(asesor.Token, pub.GetProperty("vehiculoId").GetInt64()), HttpStatusCode.OK);
        var json = await Esperar(await _api.DetalleLead(asesor.Token, lead.Id), HttpStatusCode.OK);
        Assert.Equal("ABIERTO", json.GetProperty("estado").GetString());
        Assert.Equal("VENDIDO", json.GetProperty("vehiculo").GetProperty("estadoVehiculo").GetString());
        Assert.Equal("VENDIDA", json.GetProperty("vehiculo").GetProperty("estadoPublicacion").GetString());
        Assert.Equal(asesor.PerfilId, (await _api.LeadDb(lead.Id)).ResponsableAsesorId);
    }

    [Fact(DisplayName = "CA-P5-43, CA-P5-60, CA-P5-16: no hay rutas para editar los datos del visitante, borrar leads ni tocar consentimientos")]
    public async Task Sin_edicion_ni_borrado()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor Sin Borrar");
        var lead = await _api.LeadNuevo(slug);
        foreach (var metodo in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
            foreach (var url in new[] { $"/api/leads/{lead.Id}", "/api/contactos", $"/api/leads/{lead.Id}/consentimientos" })
            {
                var r = await _api.Enviar(metodo, url, _api.Admin.Token, new { nombre = "Cambiado", telefono = "3009999999" });
                Assert.True(r.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound, $"{metodo} {url}: {(int)r.StatusCode}");
            }
        var actual = await _api.LeadDb(lead.Id);
        Assert.Equal(lead.Nombre, actual.Nombre);
        Assert.Equal(lead.Telefono, actual.Telefono);
        Assert.Single(await _api.ConsentimientosDb(lead.Id));
    }
}
