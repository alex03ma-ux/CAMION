using System.Data.Common;
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
/// P6 (QA) — huecos de la matriz CA-P6: D-55 B sin datos financieros en ninguna tabla ni registro técnico (CA-P6-09, 12),
/// "Mis solicitudes" sin IDOR ni datos internos (CA-P6-67, 68, 73, 74), el asesor sin créditos ni búsquedas con cualquier
/// filtro ni tras la reasignación del vehículo o el bloqueo (CA-P6-24, 38, 60, 61), reasignar siempre 409 (CA-P6-39),
/// relacionar solo publicaciones visibles (CA-P6-52, 56, 72), sin rutas de edición (CA-P6-48), reapertura y contador de
/// búsquedas (CA-P6-47, 57), sin coincidencias automáticas (CA-P6-25) y portal sin solicitudes (CA-P6-65). SQLite en memoria; nunca MariaDB. Datos ficticios.
/// </summary>
public sealed class P6PermisosQaTests(P5Fixture fixture) : IClassFixture<P5Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    private const string MsgSinPermisoP6 = "No tienes permiso para realizar esta acción.";

    /// <summary>Todo el texto de todas las filas de todas las tablas de la base SQLite de prueba.</summary>
    private Task<string> TodaLaBase() => _api.Leer(async db =>
    {
        var conexion = db.Database.GetDbConnection();
        await conexion.OpenAsync();
        var tablas = new List<string>();
        await using (var cmd = conexion.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) tablas.Add(r.GetString(0));
        }
        var texto = new StringBuilder();
        foreach (var tabla in tablas)
        {
            await using var cmd = conexion.CreateCommand();
            cmd.CommandText = $"SELECT * FROM \"{tabla}\"";
            await using DbDataReader r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                for (var i = 0; i < r.FieldCount; i++)
                    texto.Append(r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i), System.Globalization.CultureInfo.InvariantCulture)).Append('|');
        }
        return texto.ToString();
    });

    [Fact(DisplayName = "CA-P6-09, 12, D-55 B (QA): monto, cuota, ingresos, documento, actividad y objetos anidados enviados en un crédito nuevo, uno repetido, uno desde la ficha, uno rechazado (400) y una búsqueda no aparecen en ninguna tabla de la base ni en el registro técnico; la respuesta es idéntica a la de un envío sin ellos")]
    public async Task D55_nada_financiero_en_la_base_ni_en_los_logs()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor QA D55");
        // Marcadores únicos y ficticios: no pueden coincidir con ningún otro dato sembrado.
        string[] marcas = ["918273645", "564738291", "MARCA-QA-D55-INGRESOS", "1122334455667", "INDEPENDIENTE-QA-D55", "BANCO-QA-D55"];
        Dictionary<string, object?> ConFinancieros(Dictionary<string, object?> cuerpo)
        {
            cuerpo["monto"] = 918273645;
            cuerpo["cuotaInicial"] = "564738291";
            cuerpo["ingresos"] = "MARCA-QA-D55-INGRESOS";
            cuerpo["documento"] = 1122334455667;
            cuerpo["actividad"] = "INDEPENDIENTE-QA-D55";
            cuerpo["datosFinancieros"] = new Dictionary<string, object?> { ["entidad"] = "BANCO-QA-D55", ["monto"] = 918273645 };
            cuerpo["adjuntos"] = new object[] { "BANCO-QA-D55", 564738291 };
            return cuerpo;
        }

        var t = TelefonoUnico();
        var sinNada = await (await _api.Solicitar(RutaCredito, CuerpoCredito(telefono: TelefonoUnico()))).Content.ReadAsStringAsync();
        var r1 = await _api.Solicitar(RutaCredito, ConFinancieros(CuerpoCredito(telefono: t)));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(sinNada, await r1.Content.ReadAsStringAsync());
        // Repetido (mismo teléfono, sin publicación) y crédito desde la ficha.
        await _api.SolicitudOk(RutaCredito, ConFinancieros(CuerpoCredito(telefono: t, mensaje: "Segundo envío QA")));
        await _api.SolicitudOk(RutaCredito, ConFinancieros(CuerpoCredito(slug, t)));
        // Rechazado por validación: tampoco queda nada.
        var malo = ConFinancieros(CuerpoCredito(telefono: TelefonoUnico()));
        malo["nombre"] = "x";
        await Error400(await _api.Solicitar(RutaCredito, malo), "nombre", MsgNombre);
        // La búsqueda tampoco tiene campos financieros.
        await _api.SolicitudOk(RutaBusqueda, ConFinancieros(CuerpoBusqueda(_api.Camion, TelefonoUnico())));

        var lead = await _api.LeadDeTelefono(t);
        await _api.DetalleAdmin(lead.Id);   // también al leer el detalle

        var baseCompleta = await TodaLaBase();
        Assert.Contains("Segundo envío QA", baseCompleta);   // control: la búsqueda sobre la base funciona
        foreach (var marca in marcas)
            Assert.DoesNotContain(marca, baseCompleta);
        foreach (var linea in _api.F.Logs.ToArray())
            foreach (var marca in marcas)
                Assert.DoesNotContain(marca, linea);
    }

    [Fact(DisplayName = "CA-P6-67, 68, 73 (QA): IDOR en Mis solicitudes: el comprador recibe 404 igual para solicitudes de otro comprador (cinco orígenes), del asesor, del administrador, anónimas e inexistentes; un id no numérico no devuelve datos; parámetros extra no amplían la lista")]
    public async Task Mis_solicitudes_sin_idor()
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor QA IDOR");
        var (yo, _) = await _api.CompradorNuevo();
        var (otro, otroId) = await _api.CompradorNuevo();
        var mia = await _api.CreditoNuevo(token: yo);

        var ajenas = new List<long>
        {
            (await _api.LeadNuevo(slug, token: otro)).Id,
            (await _api.AvisoNuevo(RutaVende, token: otro)).Id,
            (await _api.AvisoNuevo(RutaSeguro, token: otro)).Id,
            (await _api.CreditoNuevo(slug, otro)).Id,
            (await _api.BusquedaNueva(_api.Camion, otro)).Id,
            (await _api.CreditoNuevo(token: _api.A.Token)).Id,
            (await _api.BusquedaNueva(_api.Carro, _api.Admin.Token)).Id,
            (await _api.CreditoNuevo()).Id,
            999_999_999, 0, -1
        };
        foreach (var id in ajenas)
            await Error(await _api.Mias(yo, $"/{id}"), HttpStatusCode.NotFound, MsgSolicitudNoEncontrada);

        foreach (var raro in new[] { "/abc", "/1.5", $"/{mia.Id}%20OR%201=1" })
        {
            var r = await _api.Mias(yo, raro);
            Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
            Assert.DoesNotContain("resumen", await r.Content.ReadAsStringAsync());
        }

        foreach (var q in new[] { $"?usuarioId={otroId}", "?origen=BUSQUEDA&todos=true", $"?id={ajenas[0]}" })
        {
            var json = await Esperar(await _api.Mias(yo, q), HttpStatusCode.OK);
            Assert.Equal(1, json.GetProperty("total").GetInt32());
            Assert.Equal(mia.Id, Assert.Single(json.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64());
        }
        Assert.Equal(5, (await Esperar(await _api.Mias(otro), HttpStatusCode.OK)).GetProperty("total").GetInt32());
        _ = asesor;
    }

    [Fact(DisplayName = "CA-P6-68, 70 (QA): tras notas, relacionar con nota, reasignar el contacto, cerrar con OTRO y observación y reabrir, Mis solicitudes no muestra notas, observación, resultado, responsable, nombre del asesor, historial ni datos de contacto")]
    public async Task Mis_solicitudes_sin_datos_internos_tras_la_gestion()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor QA Interno");
        var (destino, _) = await _api.AsesorConPlan("Asesor QA Destino Interno");
        var (yo, _) = await _api.CompradorNuevo();
        var t = TelefonoUnico();
        var contacto = await _api.LeadNuevo(slug, t, yo);
        var credito = await _api.CreditoNuevo(slug, yo, t);
        var busqueda = await _api.BusquedaNueva(_api.Camion, yo, t);

        const string nota = "Nota interna QA: cliente con deudas (no debe verse)";
        const string observacion = "Observación interna QA de cierre";
        const string notaRelacion = "Nota de relación QA interna";
        foreach (var id in new[] { contacto.Id, credito.Id, busqueda.Id })
            await Esperar(await _api.Nota(_api.Admin.Token, id, nota), HttpStatusCode.Created);
        await Esperar(await _api.Relacionar(_api.Admin.Token, busqueda.Id, IdDe(pub), notaRelacion), HttpStatusCode.Created);
        await Esperar(await _api.ReasignarLead(_api.Admin.Token, contacto.Id, destino.PerfilId), HttpStatusCode.OK);
        await Esperar(await _api.Cerrar(_api.Admin.Token, credito.Id, "OTRO", observacion), HttpStatusCode.OK);
        await Esperar(await _api.Cerrar(_api.Admin.Token, busqueda.Id, "NO_RESPONDIO"), HttpStatusCode.OK);
        await Esperar(await _api.AccionLead(_api.Admin.Token, busqueda.Id, "reabrir", new { motivo = "Reabierta por QA (prueba)" }),
            HttpStatusCode.OK);

        var lista = await Esperar(await _api.Mias(yo), HttpStatusCode.OK);
        var textos = new List<string> { lista.ToString() };
        foreach (var id in new[] { contacto.Id, credito.Id, busqueda.Id })
            textos.Add((await Esperar(await _api.Mias(yo, $"/{id}"), HttpStatusCode.OK)).ToString());
        foreach (var texto in textos)
            foreach (var prohibido in new[] { nota, observacion, notaRelacion, "Reabierta por QA", "OTRO", "NO_RESPONDIO", t,
                         "Solicitante Ficticio", "Buscador Ficticio", asesor.Nombre, destino.Nombre, "ADMINISTRACION", "REASIGNADO",
                         "eventos", "responsable", "consentimientos", "observacion", "resultado", "telefono", "correo" })
                Assert.DoesNotContain(prohibido, texto, StringComparison.OrdinalIgnoreCase);

        var items = lista.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetInt64());
        Assert.Equal("PENDIENTE", items[contacto.Id].GetProperty("estado").GetString());
        Assert.Equal("FINALIZADA", items[credito.Id].GetProperty("estado").GetString());
        Assert.Equal("EN_GESTION", items[busqueda.Id].GetProperty("estado").GetString());
        // El recomendado se muestra (vigente y visible) sin la nota de la relación.
        Assert.Equal(Slug(pub), Assert.Single(items[busqueda.Id].GetProperty("recomendados").EnumerateArray()).GetProperty("slug").GetString());
    }

    [Fact(DisplayName = "CA-P6-24, 38, 60, 61 (QA): el asesor dueño no ve el crédito de su vehículo ni la búsqueda con ningún filtro (origen, publicacionId, vehiculoId, estado, responsable); por id 404 o 403; tras reasignar el vehículo (D-34) o bloquear al asesor, créditos y búsquedas siguen en Administración sin eventos nuevos")]
    public async Task Asesor_nunca_ve_creditos_ni_busquedas()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor QA Sin Creditos");
        var (destino, _) = await _api.AsesorConPlan("Asesor QA Destino Credito");
        var vehiculoId = pub.GetProperty("vehiculoId").GetInt64();
        var resumen = (await _api.Resumen(asesor.Token)).ToString();
        var credito = await _api.CreditoNuevo(slug, _api.TokenComprador);
        var busqueda = await _api.BusquedaNueva(_api.Camion, asesor.Token);
        var contacto = await _api.LeadNuevo(slug);   // control: este sí es suyo
        var ids = new[] { credito.Id, busqueda.Id };

        foreach (var q in new[]
                 {
                     "", "?origen=CREDITO", "?origen=credito", "?origen=BUSQUEDA", $"?publicacionId={IdDe(pub)}",
                     $"?vehiculoId={vehiculoId}", "?estado=ABIERTO", "?estado=CERRADO", "?responsable=ADMINISTRACION",
                     "?tamano=100", $"?origen=CREDITO&publicacionId={IdDe(pub)}"
                 })
        {
            var r = await _api.ListaLeads(asesor.Token, q);
            if (r.StatusCode == HttpStatusCode.OK)
            {
                var texto = await r.Content.ReadAsStringAsync();
                Assert.DoesNotContain("\"CREDITO\"", texto);
                Assert.DoesNotContain("\"BUSQUEDA\"", texto);
                var json = JsonDocument.Parse(texto).RootElement;
                foreach (var i in json.GetProperty("items").EnumerateArray())
                    Assert.DoesNotContain(i.GetProperty("id").GetInt64(), ids);
            }
            else Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
        Assert.Contains(contacto.Id, await _api.IdsLeads(asesor.Token));
        var resumenDespues = await _api.Resumen(asesor.Token);
        Assert.Equal(JsonDocument.Parse(resumen).RootElement.GetProperty("nuevos").GetInt32() + 1,
            resumenDespues.GetProperty("nuevos").GetInt32());   // solo el contacto suma

        foreach (var id in ids)
        {
            foreach (var r in new[]
                     {
                         await _api.DetalleLead(asesor.Token, id), await _api.Nota(asesor.Token, id),
                         await _api.Cerrar(asesor.Token, id, "NO_RESPONDIO"), await _api.Coincidencias(asesor.Token, id),
                         await _api.Relacionar(asesor.Token, id, IdDe(pub)), await _api.Retirar(asesor.Token, id, IdDe(pub))
                     })
                await Error(r, HttpStatusCode.NotFound, MsgLeadNoEncontrado);
            Assert.Equal(HttpStatusCode.Forbidden, (await _api.AccionLead(asesor.Token, id, "reabrir", new { motivo = MotivoP5 })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _api.ReasignarLead(asesor.Token, id, asesor.PerfilId)).StatusCode);
        }
        Assert.False((await _api.LeadDb(credito.Id)).VistoPorResponsable);

        // D-34: reasignar el vehículo mueve el contacto (regresión P5) pero no el crédito.
        _api.Hora(T.AddHours(1));
        var eventosCredito = (await _api.EventosDb(credito.Id)).Count;
        await Esperar(await _api.Reasignar(_api.Admin.Token, vehiculoId, destino.PerfilId), HttpStatusCode.OK);
        Assert.Equal(destino.PerfilId, (await _api.LeadDb(contacto.Id)).ResponsableAsesorId);
        Assert.Null((await _api.LeadDb(credito.Id)).ResponsableAsesorId);
        Assert.Equal(eventosCredito, (await _api.EventosDb(credito.Id)).Count);
        Assert.DoesNotContain(credito.Id, await _api.IdsLeads(destino.Token, "?tamano=100"));

        // Bloquear al asesor que envió la búsqueda no la mueve.
        Assert.Equal(HttpStatusCode.OK, (await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, asesor.UsuarioId, "bloquear",
            "Bloqueo de prueba QA P6")).StatusCode);
        var b = await _api.LeadDb(busqueda.Id);
        Assert.Null(b.ResponsableAsesorId);
        Assert.Equal(Lead.Abierto, b.Estado);
        Assert.Equal(new[] { EventoLead.Creado, EventoLead.Asignado }, (await _api.EventosDb(busqueda.Id)).Select(e => e.Tipo).ToArray());
        Assert.DoesNotContain(await _api.AuditoriaLead(credito.Id), a => a.Accion == "REASIGNAR");
        _api.Hora(T);
    }

    public static TheoryData<string, string> CasosReasignar => new()
    {
        { "credito-portada", "abierto" }, { "credito-portada", "cerrado" }, { "credito-portada", "reabierto" },
        { "credito-ficha", "abierto" }, { "credito-ficha", "cerrado" },
        { "busqueda", "abierto" }, { "busqueda", "cerrado" }, { "busqueda", "reabierto" }
    };

    [Theory(DisplayName = "CA-P6-39, D-58 A (QA): reasignar un crédito o una búsqueda abierta, cerrada o reabierta, a un asesor o a Administración, con o sin motivo -> 409 sin eventos, auditoría ni cambio de responsable")]
    [MemberData(nameof(CasosReasignar))]
    public async Task Reasignar_siempre_409(string tipo, string estado)
    {
        _api.Hora(T);
        var (asesor, _, slug) = await _api.AsesorPublicado("Asesor QA Reasignar " + tipo);
        var lead = tipo switch
        {
            "credito-portada" => await _api.CreditoNuevo(),
            "credito-ficha" => await _api.CreditoNuevo(slug),
            _ => await _api.BusquedaNueva(_api.Camion)
        };
        var cierre = tipo == "busqueda" ? "SIN_VEHICULO" : "ATENDIDA";
        if (estado != "abierto") await Esperar(await _api.Cerrar(_api.Admin.Token, lead.Id, cierre), HttpStatusCode.OK);
        if (estado == "reabierto")
            await Esperar(await _api.AccionLead(_api.Admin.Token, lead.Id, "reabrir", new { motivo = "Reabre QA (prueba)" }), HttpStatusCode.OK);
        var eventos = (await _api.EventosDb(lead.Id)).Count;
        var auditoria = (await _api.AuditoriaLead(lead.Id)).Count;

        foreach (var r in new[]
                 {
                     await _api.ReasignarLead(_api.Admin.Token, lead.Id, asesor.PerfilId),
                     await _api.ReasignarLead(_api.Admin.Token, lead.Id, _api.A.PerfilId, motivo: null),
                     await _api.ReasignarLead(_api.Admin.Token, lead.Id, null, true),
                     await _api.ReasignarLead(_api.Admin.Token, lead.Id, null, true, motivo: null)
                 })
            await Error(r, HttpStatusCode.Conflict, MsgAvisoAdmin);
        var db = await _api.LeadDb(lead.Id);
        Assert.Null(db.ResponsableAsesorId);
        Assert.False(db.AsignacionManual);
        Assert.Equal(eventos, (await _api.EventosDb(lead.Id)).Count);
        Assert.Equal(auditoria, (await _api.AuditoriaLead(lead.Id)).Count);
    }

    [Fact(DisplayName = "CA-P6-52, 56, 72 (QA): solo se relacionan publicaciones visibles: plan vencido, vehículo vendido y asesor bloqueado -> 409 'Ese vehículo no está publicado.'; una ya relacionada que deja de verse queda marcada para el administrador, sale de coincidencias y de los recomendados del comprador, y se puede retirar")]
    public async Task Relacionar_solo_publicaciones_visibles()
    {
        _api.Hora(T);
        var (vencido, pubVencido, _) = await _api.AsesorPublicado("Asesor QA Vencido Rel");
        var (vende, pubVendida, _) = await _api.AsesorPublicado("Asesor QA Vendida Rel");
        var (bloqueado, pubBloqueada, _) = await _api.AsesorPublicado("Asesor QA Bloqueado Rel");
        var (yo, _) = await _api.CompradorNuevo();
        var busqueda = await _api.BusquedaNueva(_api.Camion, yo);

        // Relacionada mientras es visible.
        await Esperar(await _api.Relacionar(_api.Admin.Token, busqueda.Id, IdDe(pubVendida)), HttpStatusCode.Created);
        Assert.Single((await _api.ItemsMias(yo)).Single(i => i.GetProperty("id").GetInt64() == busqueda.Id)
            .GetProperty("recomendados").EnumerateArray());

        await Esperar(await _api.Vender(vende.Token, pubVendida.GetProperty("vehiculoId").GetInt64()), HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, (await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, bloqueado.UsuarioId, "bloquear",
            "Bloqueo QA relacionar")).StatusCode);
        var eventos = (await _api.EventosDb(busqueda.Id)).Count;
        // La vendida ya estaba relacionada: "no publicado" va antes que "ya relacionado" (orden del contrato).
        foreach (var p in new[] { pubVendida, pubBloqueada })
            await Error(await _api.Relacionar(_api.Admin.Token, busqueda.Id, IdDe(p)), HttpStatusCode.Conflict, MsgNoPublicado);
        var coincidencias = await _api.IdsCoincidencias(busqueda.Id);
        Assert.DoesNotContain(IdDe(pubVendida), coincidencias);
        Assert.DoesNotContain(IdDe(pubBloqueada), coincidencias);
        Assert.Contains(IdDe(pubVencido), coincidencias);

        // Plan vencido (D-41).
        _api.Hora(T.AddDays(45));
        await Error(await _api.Relacionar(_api.Admin.Token, busqueda.Id, IdDe(pubVencido)), HttpStatusCode.Conflict, MsgNoPublicado);
        Assert.DoesNotContain(IdDe(pubVencido), await _api.IdsCoincidencias(busqueda.Id));
        Assert.Equal(eventos, (await _api.EventosDb(busqueda.Id)).Count);

        // La vendida sigue relacionada: el administrador la ve marcada; el comprador ya no la ve.
        var rel = Assert.Single((await _api.DetalleAdmin(busqueda.Id)).GetProperty("vehiculosRelacionados").EnumerateArray());
        Assert.Equal(IdDe(pubVendida), rel.GetProperty("publicacionId").GetInt64());
        Assert.False(rel.GetProperty("visible").GetBoolean());
        Assert.Equal(0, (await Esperar(await _api.Mias(yo, $"/{busqueda.Id}"), HttpStatusCode.OK)).GetProperty("recomendados").GetArrayLength());
        await Esperar(await _api.Retirar(_api.Admin.Token, busqueda.Id, IdDe(pubVendida)), HttpStatusCode.OK);
        Assert.NotNull(Assert.Single(await _api.RelacionesDb(busqueda.Id)).RetiradoEn);
        _ = vencido;
        _api.Hora(T);
    }

    [Fact(DisplayName = "CA-P6-48, 69 (QA): no hay rutas para leer, editar ni borrar créditos y búsquedas (GET/PUT/PATCH/DELETE en /api/creditos, /api/busquedas, /api/leads/{id} y relaciones) con ningún rol; nada cambia")]
    public async Task Sin_rutas_de_edicion()
    {
        _api.Hora(T);
        var (_, pub, _) = await _api.AsesorPublicado("Asesor QA Sin Edicion");
        var credito = await _api.CreditoNuevo();
        var busqueda = await _api.BusquedaNueva(_api.Camion);
        await Esperar(await _api.Relacionar(_api.Admin.Token, busqueda.Id, IdDe(pub)), HttpStatusCode.Created);
        var antes = (await _api.TodoLoGuardado(credito.Id), await _api.TodoLoGuardado(busqueda.Id), (await _api.RelacionesDb(busqueda.Id)).Count);
        var cambio = new { nombre = "Cambiado QA", telefono = "3009999999", tipoId = _api.Carro, mensaje = "Cambiado QA", estado = "CERRADO" };
        foreach (var token in new[] { _api.Admin.Token, _api.A.Token, _api.TokenComprador, null })
        {
            foreach (var metodo in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
                foreach (var url in new[]
                         {
                             RutaCredito, RutaBusqueda, $"{RutaCredito}/{credito.Id}", $"{RutaBusqueda}/{busqueda.Id}",
                             $"/api/leads/{credito.Id}", $"/api/leads/{busqueda.Id}", $"/api/leads/{busqueda.Id}/vehiculos/{IdDe(pub)}",
                             $"/api/leads/{busqueda.Id}/busqueda"
                         })
                {
                    var r = await _api.Enviar(metodo, url, token, cambio);
                    Assert.True(r.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound
                        or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, $"{metodo} {url}: {(int)r.StatusCode}");
                }
            foreach (var url in new[] { RutaCredito, RutaBusqueda, $"{RutaCredito}/{credito.Id}", $"{RutaBusqueda}/{busqueda.Id}" })
            {
                var r = await _api.Enviar(HttpMethod.Get, url, token);
                Assert.True(r.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound, $"GET {url}: {(int)r.StatusCode}");
            }
        }
        Assert.Equal(antes, (await _api.TodoLoGuardado(credito.Id), await _api.TodoLoGuardado(busqueda.Id), (await _api.RelacionesDb(busqueda.Id)).Count));
    }

    [Fact(DisplayName = "CA-P6-47, 57 (QA): una búsqueda nueva cuenta en los nuevos del administrador y deja de contar al abrirla; cerrar dos veces -> 409; reabrir sin motivo -> 400; con motivo vuelve a ABIERTO en Administración con historial y auditoría")]
    public async Task Busqueda_contador_y_reapertura()
    {
        _api.Hora(T);
        var nuevos = (await _api.Resumen(_api.Admin.Token)).GetProperty("nuevos").GetInt32();
        var lead = await _api.BusquedaNueva(_api.Maquinaria);
        Assert.Equal(nuevos + 1, (await _api.Resumen(_api.Admin.Token)).GetProperty("nuevos").GetInt32());
        await _api.DetalleAdmin(lead.Id);
        Assert.Equal(nuevos, (await _api.Resumen(_api.Admin.Token)).GetProperty("nuevos").GetInt32());

        await Esperar(await _api.Cerrar(_api.Admin.Token, lead.Id, "VEHICULO_ENCONTRADO"), HttpStatusCode.OK);
        await Error(await _api.Cerrar(_api.Admin.Token, lead.Id, "SIN_VEHICULO"), HttpStatusCode.Conflict, MsgYaCerrado);
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.AccionLead(_api.Admin.Token, lead.Id, "reabrir", new { motivo = (string?)null })).StatusCode);
        var r = await Esperar(await _api.AccionLead(_api.Admin.Token, lead.Id, "reabrir", new { motivo = "El cliente sigue buscando (prueba)" }),
            HttpStatusCode.OK);
        Assert.Equal("ABIERTO", r.GetProperty("estado").GetString());
        Assert.Equal("REGISTRADO", r.GetProperty("estadoGestion").GetString());
        Assert.Equal("ADMINISTRACION", r.GetProperty("responsable").GetProperty("tipo").GetString());
        Assert.Equal(EventoLead.Reabierto, (await _api.EventosDb(lead.Id))[^1].Tipo);
        Assert.Equal("REABRIR", (await _api.AuditoriaLead(lead.Id))[^1].Accion);
        Assert.Null((await _api.LeadDb(lead.Id)).ResponsableAsesorId);
    }

    [Fact(DisplayName = "CA-P6-25, 55, 58 (QA): publicar después un vehículo que coincide con una búsqueda abierta no la relaciona, no añade eventos ni auditoría, no crea leads para el asesor y la búsqueda no se le muestra")]
    public async Task Sin_coincidencias_automaticas()
    {
        _api.Hora(T);
        var (yo, _) = await _api.CompradorNuevo();
        var busqueda = await _api.BusquedaNueva(_api.Camion, yo, cambios: [("marcaId", _api.Chevrolet), ("lineaId", _api.Npr)]);
        var antes = (await _api.TotalLeads(), (await _api.EventosDb(busqueda.Id)).Count, (await _api.AuditoriaLead(busqueda.Id)).Count);

        _api.Hora(T.AddMinutes(5));
        var (asesor, pub, _) = await _api.AsesorPublicado("Asesor QA Coincide Despues");   // Camión Chevrolet NPR
        Assert.Contains(IdDe(pub), await _api.IdsCoincidencias(busqueda.Id));             // coincide, pero solo bajo demanda
        Assert.Empty(await _api.RelacionesDb(busqueda.Id));
        Assert.Equal(antes, (await _api.TotalLeads(), (await _api.EventosDb(busqueda.Id)).Count, (await _api.AuditoriaLead(busqueda.Id)).Count));
        Assert.Empty(await _api.IdsLeads(asesor.Token));
        Assert.Equal(0, (await Esperar(await _api.Mias(yo, $"/{busqueda.Id}"), HttpStatusCode.OK)).GetProperty("recomendados").GetArrayLength());
        _api.Hora(T);
    }

    [Fact(DisplayName = "CA-P6-65 (QA):inicio, catálogo, filtros, ficha y listas de la búsqueda no exponen créditos, búsquedas, sus datos ni cuántos hay")]
    public async Task Portal_sin_solicitudes()
    {
        _api.Hora(T);
        var (_, _, slug) = await _api.AsesorPublicado("Asesor QA Portal P6");
        var t = TelefonoUnico();
        var cuerpo = CuerpoCredito(slug, t, "Comentario QA portal secreto");
        cuerpo["nombre"] = "Nombre QA Portal Secreto";
        await _api.SolicitudOk(RutaCredito, cuerpo);
        var b = InventarioApi.Con(CuerpoBusqueda(_api.Camion, TelefonoUnico()), ("descripcion", "Descripcion QA portal secreta"));
        await _api.SolicitudOk(RutaBusqueda, b);
        foreach (var url in new[]
                 {
                     "/api/catalogo/inicio", "/api/catalogo/publicaciones?tamano=48", "/api/catalogo/filtros",
                     "/api/catalogo/publicaciones/" + Uri.EscapeDataString(slug), "/api/catalogo/listas-busqueda"
                 })
        {
            foreach (var token in new[] { null, _api.TokenComprador })
            {
                var r = await _api.Enviar(HttpMethod.Get, url, token);
                Assert.Equal(HttpStatusCode.OK, r.StatusCode);
                var texto = await r.Content.ReadAsStringAsync();
                foreach (var prohibido in new[] { t, "Nombre QA Portal Secreto", "Comentario QA portal secreto", "Descripcion QA portal secreta",
                             "CREDITO", "BUSQUEDA", "credito", "leads", "solicitudes" })
                    Assert.DoesNotContain(prohibido, texto);
            }
        }
    }
}
