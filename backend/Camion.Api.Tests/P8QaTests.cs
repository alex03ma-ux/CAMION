using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Camion.Api.Endpoints;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.Ayudas;
using static Camion.Api.Tests.Infraestructura.AyudasP2;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;
using static Camion.Api.Tests.Infraestructura.AyudasP5b;
using static Camion.Api.Tests.Infraestructura.AyudasP6;
using static Camion.Api.Tests.Infraestructura.AyudasP7;

namespace Camion.Api.Tests;

/// <summary>
/// P8 — pruebas de QA que completan P8ReportesTests, P8ActividadTests y P8RecuperacionTests (CA-P8-07, 23, 44 a 46, 49, 52,
/// 53, 55, 58, 60, 67). Cada prueba con cifras usa su propia API (SQLite en memoria, reloj fijo en T); nunca MariaDB.
/// Datos ficticios con marcas reconocibles ("Zeta") para buscarlas en el contenido; no se imprimen códigos ni contraseñas.
/// Las dos pruebas marcadas DEFECTO QA-P8-1 y QA-P8-2 fallan a propósito: documentan defectos (docs/criterios/P8-evidencia.md).
/// </summary>
public sealed class P8QaTests
{
    private static readonly string[] Tipos = ["vehiculos", "solicitudes", "creditos", "ventas", "suscripciones"];
    private const string NuevaClave = "NuevaClaveQa2027";

    private static Task<HttpResponseMessage> Get(InventarioApi api, string url, string? token) => api.Enviar(HttpMethod.Get, url, token);

    private static async Task<string> Csv(InventarioApi api, string tipo, string query = "")
    {
        var r = await Get(api, $"/api/admin/reportes/{tipo}/csv{query}", api.Admin.Token);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var bytes = await r.Content.ReadAsByteArrayAsync();
        return Encoding.UTF8.GetString(bytes[3..]);
    }

    private static Task<int> Exportaciones(InventarioApi api) =>
        api.Leer(db => db.AuditoriaEventos.CountAsync(a => a.Entidad == "Reporte" && a.Accion == "EXPORTAR"));

    /// <summary>Celdas de un CSV con ';' y comillas dobles (RFC 4180 con ';').</summary>
    private static List<string> Celdas(string csv)
    {
        var celdas = new List<string>();
        var actual = new StringBuilder();
        var entreComillas = false;
        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (entreComillas)
            {
                if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"') { actual.Append('"'); i++; }
                else if (c == '"') entreComillas = false;
                else actual.Append(c);
            }
            else if (c == '"' && actual.Length == 0) entreComillas = true;
            else if (c == ';') { celdas.Add(actual.ToString()); actual.Clear(); }
            else if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') { celdas.Add(actual.ToString()); actual.Clear(); i++; }
            else actual.Append(c);
        }
        if (actual.Length > 0) celdas.Add(actual.ToString());
        return celdas;
    }

    // ================================================================ CSV y reportes: sin datos personales (CA-P8-07, 23, 45)

    [Fact(DisplayName = "QA CA-P8-07, 23, 45: ningún reporte, CSV, indicador ni actividad contiene nombre, teléfono, correo, mensaje, notas u observaciones")]
    public async Task Sin_datos_personales_en_ningun_reporte_ni_csv()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var (asesor, pub, slug) = await api.AsesorPublicado("Asesora Privacidad Ficticia");
        var leads = new List<Lead>
        {
            await api.LeadNuevo(slug), await api.LeadNuevo(slug), await api.AvisoNuevo(RutaVende), await api.AvisoNuevo(RutaSeguro),
            await api.CreditoNuevo(slug), await api.CreditoNuevo(), await api.BusquedaNueva(api.Camion)
        };
        Assert.True((await api.Nota(api.Admin.Token, leads[4].Id, "QA nota interna Zeta")).IsSuccessStatusCode);
        await Esperar(await api.Cerrar(asesor.Token, leads[1].Id, Lead.NoRespondio, "QA observacion cierre Zeta"), HttpStatusCode.OK);
        await Esperar(await api.Cerrar(api.Admin.Token, leads[4].Id, Lead.RemitidoEntidad, "QA observacion credito Zeta"), HttpStatusCode.OK);

        var ids = leads.Select(l => l.Id).ToArray();
        await api.Db(async db =>
        {
            foreach (var l in await db.Leads.Where(l => ids.Contains(l.Id)).ToListAsync())
            {
                l.Nombre = "Zeta Secretisima Ficticia";
                l.Correo = "zeta.secreta@ejemplo.test";
                l.Mensaje = "QA mensaje privado Zeta";
            }
        });
        var telefonos = leads.Select(l => l.Telefono).ToArray(); // "+57" + 10 dígitos
        var reservado = await api.CrearId(asesor);
        await Esperar(await api.Accion(asesor.Token, reservado, "reservar", "QA nota reserva Zeta comprador"), HttpStatusCode.OK);
        await api.VehiculoVendido(asesor, "PORTAL", 80_000_000, observaciones: "QA observacion venta Zeta");
        var plan = await api.PlanConPrecio(100_000);
        var sus = (await Esperar(await api.AsignarConPago(api.B.PerfilId, plan, PagoValido(referencia: "QA-REF-ZETA")), HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        await api.AccionOk(sus, "renovar", new { pago = PagoValido(referencia: "QA-REF-ZETA-2") });
        await Esperar(await api.AnularPago((await api.PagosDb(sus))[1].Id, "QA motivo anulacion Omega"), HttpStatusCode.OK);

        var prohibidos = new List<string>
        {
            "Zeta Secretisima", "zeta.secreta", "QA mensaje privado", "QA nota interna", "QA observacion", "QA nota reserva",
            "QA-REF-ZETA", "Zeta"
        };
        prohibidos.AddRange(telefonos);
        prohibidos.AddRange(telefonos.Select(t => t[3..]));

        var textos = new List<(string Que, string Texto)>();
        foreach (var tipo in Tipos)
        {
            textos.Add(($"{tipo} JSON", await (await Get(api, $"/api/admin/reportes/{tipo}", api.Admin.Token)).Content.ReadAsStringAsync()));
            textos.Add(($"{tipo} CSV", await Csv(api, tipo)));
        }
        textos.Add(("indicadores", await (await Get(api, "/api/admin/indicadores", api.Admin.Token)).Content.ReadAsStringAsync()));
        textos.Add(("mis indicadores", await (await Get(api, "/api/asesor/indicadores", asesor.Token)).Content.ReadAsStringAsync()));
        textos.Add(("actividad vehículo reservado",
            await (await Get(api, $"/api/admin/vehiculos/{reservado}/actividad", api.Admin.Token)).Content.ReadAsStringAsync()));
        textos.Add(("actividad vehículo publicado",
            await (await Get(api, $"/api/admin/vehiculos/{pub.GetProperty("vehiculoId").GetInt64()}/actividad", api.Admin.Token))
                .Content.ReadAsStringAsync()));
        textos.Add(("actividad suscripción",
            await (await Get(api, $"/api/admin/suscripciones/{sus}/actividad", api.Admin.Token)).Content.ReadAsStringAsync()));

        foreach (var (que, texto) in textos)
        {
            foreach (var p in prohibidos)
                Assert.False(texto.Contains(p, StringComparison.OrdinalIgnoreCase), $"{que} contiene un dato personal o texto libre ('{p}')");
            // El motivo de anulación no va en los reportes ni en el CSV (contrato §6); la actividad sí lo muestra (§10, CA-P8-56).
            if (!que.StartsWith("actividad"))
                Assert.False(texto.Contains("QA motivo anulacion", StringComparison.OrdinalIgnoreCase), $"{que} contiene el motivo de anulación");
        }

        // No es una búsqueda vacía: el CSV de solicitudes trae las 7 filas y los ids de los leads.
        var solicitudes = await Csv(api, "solicitudes");
        Assert.All(ids, id => Assert.Contains($"\r\n{id};", solicitudes));
        // Las cabeceras de las filas no incluyen columnas de contacto.
        foreach (var columna in new[] { "Nombre del cliente", "Teléfono", "Telefono", "Correo", "Mensaje", "Observación", "Notas" })
            Assert.DoesNotContain(columna, solicitudes, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================ CSV: inyección de fórmulas (CA-P8-44)

    [Fact(DisplayName = "QA CA-P8-44: en los cinco CSV ninguna celda empieza por = + - @, tabulador o retorno sin apóstrofo")]
    public async Task Csv_sin_formulas_en_ninguna_celda()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var nombres = new[] { "+Asesor Mas Ficticio", "-Asesor Menos Ficticio", "@Asesor Arroba Ficticio", "=Asesor Igual Ficticio" };
        var plan = await api.PlanConPrecio(50_000);
        foreach (var nombre in nombres)
        {
            var asesor = await api.CrearAsesor(nombre);
            await api.VehiculoVendido(asesor, "CONTACTO_ASESOR", 60_000_000);
            await Esperar(await api.AsignarConPago(asesor.PerfilId, plan, PagoValido()), HttpStatusCode.Created);
        }
        await api.Db(async db =>
        {
            (await db.Planes.SingleAsync(p => p.Id == plan)).Nombre = "=Plan Formula Ficticio";
        });

        foreach (var tipo in Tipos)
        {
            var texto = await Csv(api, tipo);
            foreach (var celda in Celdas(texto))
                Assert.False(celda.Length > 0 && "=+-@\t\r".Contains(celda[0]), $"{tipo}: celda peligrosa '{celda}'");
        }
        var ventas = await Csv(api, "ventas");
        foreach (var nombre in nombres) Assert.Contains("'" + nombre, ventas);
        Assert.Contains("'=Plan Formula Ficticio", await Csv(api, "suscripciones"));
    }

    // ================================================================ CSV: auditoría de cada exportación (CA-P8-46)

    [Fact(DisplayName = "QA CA-P8-46, 49: cada exportación deja una fila con quién, tipo, filtros y filas; las rechazadas no se auditan")]
    public async Task Csv_auditoria_de_cada_exportacion()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var (asesor, _, slug) = await api.AsesorPublicado("Asesora Auditada Ficticia");
        await api.LeadNuevo(slug);
        await api.VehiculoVendido(asesor, "REFERIDO", 70_000_000);
        var filtros = new Dictionary<string, string>
        {
            ["vehiculos"] = $"?desde=2026-10-01&hasta=2026-10-31&asesorId={asesor.PerfilId}",
            ["solicitudes"] = "?desde=2026-10-01&hasta=2026-10-31&origen=CONTACTO_VEHICULO&estado=ABIERTO",
            ["creditos"] = "?desde=2026-10-01&hasta=2026-10-31&canal=PORTADA",
            ["ventas"] = $"?desde=2026-10-01&hasta=2026-10-31&origen=REFERIDO&asesorId={asesor.PerfilId}",
            ["suscripciones"] = "?desde=2026-10-01&hasta=2026-10-31&medio=EFECTIVO",
        };

        foreach (var (tipo, query) in filtros)
        {
            var antes = await Exportaciones(api);
            var contenido = await Csv(api, tipo, query);
            Assert.Equal(antes + 1, await Exportaciones(api));
            var fila = (await api.Auditoria("Reporte", tipo)).OrderBy(a => a.Id).Last();
            Assert.Equal(("EXPORTAR", api.Admin.Id), (fila.Accion, fila.UsuarioId));
            var datos = JsonDocument.Parse(fila.DatosNuevos!).RootElement;
            var json = await Esperar(await Get(api, $"/api/admin/reportes/{tipo}{query}", api.Admin.Token), HttpStatusCode.OK);
            Assert.Equal(json.GetProperty("filas").GetProperty("total").GetInt32(), datos.GetProperty("filas").GetInt32());
            var f = datos.GetProperty("filtros").GetRawText();
            Assert.Contains("2026-10-01", f);
            Assert.Contains("2026-10-31", f);
            foreach (var par in query.TrimStart('?').Split('&').Where(p => !p.StartsWith("desde") && !p.StartsWith("hasta")))
                Assert.Contains(par.Split('=')[1], f);
            // Nunca el contenido: ni nombres ni líneas del archivo.
            Assert.DoesNotContain("Asesora Auditada", fila.DatosNuevos!);
            Assert.DoesNotContain("70000000", fila.DatosNuevos!);
            Assert.True(contenido.Length > fila.DatosNuevos!.Length);
        }

        // Rechazadas: filtro inválido, sin sesión, asesor (también con su propio asesorId) y comprador.
        var total = await Exportaciones(api);
        Assert.Equal(HttpStatusCode.BadRequest, (await Get(api, "/api/admin/reportes/ventas/csv?desde=mal", api.Admin.Token)).StatusCode);
        foreach (var tipo in Tipos)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Get(api, $"/api/admin/reportes/{tipo}/csv", null)).StatusCode);
            var r = await Get(api, $"/api/admin/reportes/{tipo}/csv?asesorId={asesor.PerfilId}", asesor.Token);
            await Error(r, HttpStatusCode.Forbidden, AyudasP2.MsgSinPermiso);
            Assert.NotEqual("text/csv", r.Content.Headers.ContentType?.MediaType);
            await Error(await Get(api, $"/api/admin/reportes/{tipo}/csv", api.TokenComprador), HttpStatusCode.Forbidden, AyudasP2.MsgSinPermiso);
            // Solo GET.
            var post = await api.Enviar(HttpMethod.Post, $"/api/admin/reportes/{tipo}/csv", api.Admin.Token, new { });
            Assert.Contains(post.StatusCode, new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });
        }
        Assert.Equal(total, await Exportaciones(api));
    }

    [Fact(DisplayName = "QA CA-P8-08, 50: consultar reportes, indicadores y CSV no cambia datos ni deja archivos en el servidor")]
    public async Task Consultar_no_cambia_nada_ni_guarda_archivos()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var (asesor, _, slug) = await api.AsesorPublicado("Asesora Solo Lectura Ficticia");
        await api.LeadNuevo(slug);
        await api.CreditoNuevo();
        await api.VehiculoVendido(asesor, "PORTAL", 50_000_000);
        async Task<string> Huella() => await api.Leer(async db => string.Join("|",
            await db.Leads.CountAsync(), await db.EventosLead.CountAsync(), await db.Vehiculos.CountAsync(),
            await db.Publicaciones.CountAsync(), await db.VentasVehiculo.CountAsync(), await db.Pagos.CountAsync(),
            await db.Suscripciones.CountAsync(), await db.AuditoriaEventos.CountAsync(a => a.Entidad != "Reporte"),
            string.Join(",", await db.Leads.OrderBy(l => l.Id).Select(l => l.Estado + l.ResponsableAsesorId).ToListAsync()),
            string.Join(",", await db.Vehiculos.OrderBy(v => v.Id).Select(v => v.Estado + v.AsesorId).ToListAsync())));
        var antes = await Huella();
        var archivosAntes = Directory.Exists(api.F.CarpetaMedia)
            ? Directory.GetFiles(api.F.CarpetaMedia, "*", SearchOption.AllDirectories).Length : 0;
        foreach (var tipo in Tipos)
        {
            await Esperar(await Get(api, $"/api/admin/reportes/{tipo}", api.Admin.Token), HttpStatusCode.OK);
            await Csv(api, tipo);
        }
        await Esperar(await Get(api, "/api/admin/indicadores", api.Admin.Token), HttpStatusCode.OK);
        await Esperar(await Get(api, "/api/asesor/indicadores", asesor.Token), HttpStatusCode.OK);
        Assert.Equal(antes, await Huella());
        var archivos = Directory.Exists(api.F.CarpetaMedia)
            ? Directory.GetFiles(api.F.CarpetaMedia, "*", SearchOption.AllDirectories) : [];
        Assert.Equal(archivosAntes, archivos.Length);
        Assert.DoesNotContain(archivos, f => f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(Directory.GetFiles(AppContext.BaseDirectory, "reporte-*.csv", SearchOption.AllDirectories));
    }

    // ================================================================ Mis indicadores: alcance e IDOR (CA-P8-52, 53, 55)

    [Fact(DisplayName = "QA CA-P8-52, 53: Mis indicadores coincide con su inventario, Mis contactos y Mis ventas; sigue al dueño tras reasignar")]
    public async Task Mis_indicadores_alcance_y_reasignacion()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var (uno, pub, slug) = await api.AsesorPublicado("Asesora Uno Alcance Ficticia");
        var (dos, _) = await api.AsesorConPlan("Asesor Dos Alcance Ficticio");
        var tres = await api.CrearAsesor("Asesor Tres Sin Datos Ficticio");
        await api.LeadNuevo(slug);
        await api.LeadNuevo(slug);
        await api.CreditoNuevo(slug);
        await api.BusquedaNueva(api.Camion);
        await api.VehiculoVendido(uno, "PORTAL", 90_000_000);
        await api.CrearId(uno);

        async Task<JsonElement> Mis(AsesorPrueba a, string q = "") =>
            await Esperar(await Get(api, "/api/asesor/indicadores" + q, a.Token), HttpStatusCode.OK);
        async Task Coherente(AsesorPrueba a)
        {
            var m = await Mis(a);
            var vehiculos = await api.Leer(db => db.Vehiculos.CountAsync(v => v.AsesorId == a.PerfilId));
            Assert.Equal(vehiculos, m.GetProperty("vehiculosPorEstado").EnumerateObject().Sum(p => p.Value.GetInt32()));
            var contactos = await Esperar(await api.ListaLeads(a.Token), HttpStatusCode.OK);
            Assert.Equal(contactos.GetProperty("total").GetInt32(), m.GetProperty("contactosRecibidos").GetInt32());
            var ventas = await api.VentasOk(a.Token, "?estado=VIGENTE");
            Assert.Equal(ventas.GetProperty("total").GetInt32(), m.GetProperty("ventasVigentes").GetProperty("cantidad").GetInt32());
            Assert.Equal(ventas.GetProperty("items").EnumerateArray().Sum(v => v.GetProperty("precioFinalCop").GetInt64()),
                m.GetProperty("ventasVigentes").GetProperty("valorCop").GetInt64());
        }

        var antes = await Mis(uno);
        Assert.Equal(2, antes.GetProperty("contactosRecibidos").GetInt32());
        await Coherente(uno);
        await Coherente(dos);

        // Un asesor sin datos ve ceros aunque el portal tenga actividad, y no puede pedir lo de otro (IDOR).
        var vacio = await Mis(tres);
        Assert.Equal(0, vacio.GetProperty("contactosRecibidos").GetInt32());
        Assert.Equal(0, vacio.GetProperty("vehiculosPorEstado").EnumerateObject().Sum(p => p.Value.GetInt32()));
        Assert.Equal(0, vacio.GetProperty("ventasVigentes").GetProperty("valorCop").GetInt64());
        foreach (var q in new[] { $"?asesorId={uno.PerfilId}", $"?asesorId={uno.PerfilId}&responsable={uno.PerfilId}", $"?usuarioId={uno.UsuarioId}",
                     $"?perfilId={uno.PerfilId}", "?todos=true" })
            Assert.Equal(vacio.GetRawText(), (await Mis(tres, q)).GetRawText());
        foreach (var ruta in new[] { $"/api/asesor/{uno.PerfilId}/indicadores", $"/api/asesor/indicadores/{uno.PerfilId}" })
            Assert.Contains((await Get(api, ruta, tres.Token)).StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });

        // Reasignar el vehículo publicado: deja de contar para uno y pasa a contar para dos (alcance = dueño de hoy).
        var vehiculo = pub.GetProperty("vehiculoId").GetInt64();
        await Esperar(await api.Reasignar(api.Admin.Token, vehiculo, dos.PerfilId), HttpStatusCode.OK);
        await Coherente(uno);
        await Coherente(dos);
        Assert.Equal(antes.GetProperty("vehiculosPorEstado").EnumerateObject().Sum(p => p.Value.GetInt32()) - 1,
            (await Mis(uno)).GetProperty("vehiculosPorEstado").EnumerateObject().Sum(p => p.Value.GetInt32()));
        foreach (var a in new[] { uno, dos, tres })
            foreach (var prohibido in new[] { "credito", "busqueda", "aviso", "pago", "ingreso", "anterior", "asesor\"", "ranking", "posicion" })
                Assert.DoesNotContain(prohibido, (await Mis(a)).GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "QA CA-P8-55: un asesor bloqueado o rechazado pierde Mis indicadores; el pendiente no tiene sesión")]
    public async Task Mis_indicadores_cuentas_no_activas()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var bloqueado = await api.CrearAsesor("Asesor Bloqueado Indicadores Ficticio");
        Assert.Equal(HttpStatusCode.OK, (await Get(api, "/api/asesor/indicadores", bloqueado.Token)).StatusCode);
        await Ok(await AccionMotivo(api.C, api.Admin.Token, bloqueado.UsuarioId, "bloquear", AyudasP2.MotivoValido));
        Assert.Contains((await Get(api, "/api/asesor/indicadores", bloqueado.Token)).StatusCode,
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        Assert.NotEqual(HttpStatusCode.OK, (await Login(api.C, bloqueado.Email, PasswordValida)).StatusCode);

        var (pendienteId, pendienteEmail, _) = await CrearAsesorPendiente(api.C, "Asesor Pendiente Indicadores Ficticio");
        Assert.NotEqual(HttpStatusCode.OK, (await Login(api.C, pendienteEmail, PasswordValida)).StatusCode);
        await Ok(await AccionMotivo(api.C, api.Admin.Token, pendienteId, "rechazar", AyudasP2.MotivoValido));
        Assert.NotEqual(HttpStatusCode.OK, (await Login(api.C, pendienteEmail, PasswordValida)).StatusCode);
        // Un token firmado a mano para la cuenta rechazada tampoco sirve.
        var falso = Firmar(api.F, pendienteId, (await Usuario(api.F, pendienteId)).SessionVersion, "ASESOR");
        Assert.Contains((await Get(api, "/api/asesor/indicadores", falso)).StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }

    // ================================================================ Actividad (CA-P8-58, 59, 60)

    [Fact(DisplayName = "QA CA-P8-59, 60: actividad solo GET y solo administrador; el dueño sin reasignar, ids raros y otros métodos")]
    public async Task Actividad_permisos_y_metodos()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var (asesor, sus) = await api.AsesorConPlan("Asesora Duena Actividad Ficticia");
        var id = await api.CrearId(asesor);
        foreach (var ruta in new[] { $"/api/admin/vehiculos/{id}/actividad", $"/api/admin/suscripciones/{sus}/actividad" })
        {
            await Error(await Get(api, ruta, asesor.Token), HttpStatusCode.Forbidden, AyudasP2.MsgSinPermiso);   // su propio vehículo y su plan
            await Error(await Get(api, ruta, api.TokenComprador), HttpStatusCode.Forbidden, AyudasP2.MsgSinPermiso);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Get(api, ruta, "token.no.valido")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Get(api, ruta, api.Admin.Token)).StatusCode);
            foreach (var metodo in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Patch })
                Assert.Contains((await api.Enviar(metodo, ruta, api.Admin.Token, new { })).StatusCode,
                    new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });
        }
        foreach (var raro in new[] { "0", "-1", "abc", "1.5", "99999999999999999999" })
            Assert.Contains((await Get(api, $"/api/admin/vehiculos/{raro}/actividad", api.Admin.Token)).StatusCode,
                new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest });
        // Ninguna ruta del asesor para su actividad.
        Assert.Equal(HttpStatusCode.NotFound, (await Get(api, $"/api/inventario/vehiculos/{id}/actividad", asesor.Token)).StatusCode);
    }

    // ================================================================ Recuperación (CA-P8-67, D-73 D)

    private static Task<HttpResponseMessage> Generar(InventarioApi api, string usuarioId, string? token) =>
        api.Enviar(HttpMethod.Post, $"/api/admin/usuarios/{Uri.EscapeDataString(usuarioId)}/enlace-recuperacion", token);

    private static Task<HttpResponseMessage> Restablecer(InventarioApi api, string? codigo, string? password = NuevaClave) =>
        api.C.PostAsJsonAsync("/api/auth/restablecer-contrasena", new { codigo, password });

    private static async Task<(string Enlace, string Codigo)> Enlace(InventarioApi api, string usuarioId)
    {
        var json = await Esperar(await Generar(api, usuarioId, api.Admin.Token), HttpStatusCode.OK);
        var enlace = json.GetProperty("enlace").GetString()!;
        return (enlace, enlace[(enlace.IndexOf("codigo=", StringComparison.Ordinal) + "codigo=".Length)..]);
    }

    [Fact(DisplayName = "QA CA-P8-67: ni el enlace ni el código aparecen en ningún log ni en ninguna fila de auditoría, tampoco tras intentos fallidos")]
    public async Task Codigo_nunca_en_logs_ni_auditoria()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        api.Hora(T);
        var (id, email) = await CrearComprador(api.C, "Comprador Logs Ficticio");
        var (enlace1, codigo1) = await Enlace(api, id);
        var (enlace2, codigo2) = await Enlace(api, id);                       // anula el primero
        await Error400(await Restablecer(api, codigo1), "codigo", RecuperacionEndpoints.MensajeCodigo);
        Assert.Equal(HttpStatusCode.BadRequest, (await Restablecer(api, codigo2, "debil")).StatusCode);
        await Error400(await Restablecer(api, codigo2[..^3] + "xyz"), "codigo", RecuperacionEndpoints.MensajeCodigo);
        await Esperar(await Restablecer(api, codigo2), HttpStatusCode.OK);
        await Error400(await Restablecer(api, codigo2, "OtraClaveQa2028"), "codigo", RecuperacionEndpoints.MensajeCodigo);

        var secretos = new[] { enlace1, enlace2, codigo1, codigo2, codigo2[..40], NuevaClave, "OtraClaveQa2028" };
        var logs = api.F.Logs.ToArray();
        Assert.NotEmpty(logs);
        foreach (var s in secretos)
            Assert.DoesNotContain(logs, l => l.Contains(s, StringComparison.Ordinal));
        var auditoria = await api.Leer(db => db.AuditoriaEventos.AsNoTracking().ToListAsync());
        foreach (var a in auditoria)
            foreach (var s in secretos)
                Assert.DoesNotContain(s, $"{a.DatosAnteriores}|{a.DatosNuevos}|{a.EntidadId}");
        var generar = auditoria.Where(a => a.Accion == "GENERAR_ENLACE_RECUPERACION" && a.EntidadId == id).ToList();
        Assert.Equal(2, generar.Count);
        Assert.All(generar, g => Assert.Equal(new[] { "venceEn" },
            JsonDocument.Parse(g.DatosNuevos!).RootElement.EnumerateObject().Select(p => p.Name).ToArray()));
        Assert.Single(auditoria, a => a.Accion == "RESTABLECER_CONTRASENA" && a.EntidadId == id);
        Assert.Equal(HttpStatusCode.OK, (await Login(api.C, email, NuevaClave)).StatusCode);
    }

    [Fact(DisplayName = "QA CA-P8-67: dos usos simultáneos del mismo código -> uno 200 y otro 400; nunca 500 ni dos cambios")]
    public async Task Uso_simultaneo_un_solo_exito()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        api.Hora(T);
        var (id, email) = await CrearComprador(api.C, "Comprador Simultaneo Ficticio");
        var (_, codigo) = await Enlace(api, id);
        var version = (await Usuario(api.F, id)).SessionVersion;
        var respuestas = await Task.WhenAll(Restablecer(api, codigo, "ClaveUnoQa2027"), Restablecer(api, codigo, "ClaveDosQa2027"));
        var codigos = respuestas.Select(r => r.StatusCode).Order().ToArray();
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest }, codigos);
        Assert.Equal(version + 1, (await Usuario(api.F, id)).SessionVersion);
        Assert.Single(await AuditoriaDe(api.F, id), a => a.Accion == "RESTABLECER_CONTRASENA");
        var ganadora = respuestas[0].StatusCode == HttpStatusCode.OK ? "ClaveUnoQa2027" : "ClaveDosQa2027";
        Assert.Equal(HttpStatusCode.OK, (await Login(api.C, email, ganadora)).StatusCode);
    }

    [Fact(DisplayName = "QA CA-P8-67, D-12: restablecer cierra todas las sesiones abiertas (SessionVersion), generar no")]
    public async Task Cierra_todas_las_sesiones()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        api.Hora(T);
        var asesor = await api.CrearAsesor("Asesora Dos Equipos Ficticia");
        var otraSesion = await ObtenerToken(api.C, asesor.Email);
        var version = (await Usuario(api.F, asesor.UsuarioId)).SessionVersion;
        var (_, codigo) = await Enlace(api, asesor.UsuarioId);
        Assert.Equal(version, (await Usuario(api.F, asesor.UsuarioId)).SessionVersion);
        foreach (var t in new[] { asesor.Token, otraSesion })
            Assert.Equal(HttpStatusCode.OK, (await Get(api, "/api/asesor/indicadores", t)).StatusCode);
        await Esperar(await Restablecer(api, codigo), HttpStatusCode.OK);
        Assert.Equal(version + 1, (await Usuario(api.F, asesor.UsuarioId)).SessionVersion);
        foreach (var t in new[] { asesor.Token, otraSesion })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Me(api.C, t)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Get(api, "/api/asesor/indicadores", t)).StatusCode);
        }
    }

    [Fact(DisplayName = "QA D-73 D: con el límite por defecto (5), la 6.ª petición da 429 aunque el código sea bueno y no cambia nada; cupo aparte del contacto")]
    public async Task Limite_bloquea_tambien_el_codigo_bueno()
    {
        using var api = await InventarioApi.Crear(NuevoReloj(), b => b.UseSetting("Recuperacion:LimiteVentana", "5"));
        api.Hora(T);
        var (id, email) = await CrearComprador(api.C, "Comprador Limite Ficticio");
        var (_, codigo) = await Enlace(api, id);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await Restablecer(api, "no-es-un-codigo")).StatusCode);
        var r = await Restablecer(api, codigo);
        await Error(r, HttpStatusCode.TooManyRequests, RecuperacionEndpoints.MensajeMuchos);
        Assert.Equal(HttpStatusCode.OK, (await Login(api.C, email, PasswordValida)).StatusCode);
        Assert.DoesNotContain(await AuditoriaDe(api.F, id), a => a.Accion == "RESTABLECER_CONTRASENA");
        // Generar (administrador) y el contacto público no comparten el cupo.
        await Enlace(api, id);
        var (_, _, slug) = await api.AsesorPublicado("Asesora Cupo Ficticia");
        await api.LeadNuevo(slug);
    }

    [Fact(DisplayName = "QA D-73 D: no se genera para cuentas rechazadas, y un código de cuenta bloqueada no se reactiva al desbloquear (DEFECTO QA-P8-1)")]
    public async Task No_activas_y_desbloqueo()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        api.Hora(T);
        var (rechazado, _, _) = await CrearAsesorPendiente(api.C, "Asesor Rechazado Enlace Ficticio");
        await Ok(await AccionMotivo(api.C, api.Admin.Token, rechazado, "rechazar", AyudasP2.MotivoValido));
        await Error(await Generar(api, rechazado, api.Admin.Token), HttpStatusCode.Conflict, RecuperacionEndpoints.MensajeNoActiva);
        Assert.DoesNotContain(await AuditoriaDe(api.F, rechazado), a => a.Accion == "GENERAR_ENLACE_RECUPERACION");

        var asesor = await api.CrearAsesor("Asesor Bloqueo Desbloqueo Ficticio");
        var (_, codigo) = await Enlace(api, asesor.UsuarioId);
        await Ok(await AccionMotivo(api.C, api.Admin.Token, asesor.UsuarioId, "bloquear", AyudasP2.MotivoValido));
        await Error400(await Restablecer(api, codigo), "codigo", RecuperacionEndpoints.MensajeCodigo);
        await Ok(await AccionMotivo(api.C, api.Admin.Token, asesor.UsuarioId, "desbloquear", AyudasP2.MotivoValido));
        // El enlace se emitió antes del bloqueo: el administrador debe generar uno nuevo tras desbloquear.
        await Error400(await Restablecer(api, codigo), "codigo", RecuperacionEndpoints.MensajeCodigo);
    }

    [Fact(DisplayName = "QA D-73 D: un código emitido para un comprador no restablece la cuenta si después pasa a administrador (DEFECTO QA-P8-2)")]
    public async Task Codigo_de_cuenta_promovida_a_administrador()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        api.Hora(T);
        var (id, email) = await CrearComprador(api.C, "Comprador Luego Admin Ficticio");
        var (_, codigo) = await Enlace(api, id);
        using (var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(api.F.Services))
        {
            var sp = scope.ServiceProvider;
            var r = await Camion.Api.Comandos.PromoverAdmin.EjecutarAsync(email,
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>(sp),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>(sp),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Camion.Api.Data.CamionDbContext>(sp));
        }
        Assert.Contains("ADMINISTRADOR", await RolesDe(api.F, id));
        await Error400(await Restablecer(api, codigo), "codigo", RecuperacionEndpoints.MensajeCodigo);
    }
}
