using System.Net;
using System.Text;
using System.Text.Json;
using Camion.Api.Models;
using Camion.Api.Reportes;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;
using static Camion.Api.Tests.Infraestructura.AyudasP5b;
using static Camion.Api.Tests.Infraestructura.AyudasP6;
using static Camion.Api.Tests.Infraestructura.AyudasP7;

namespace Camion.Api.Tests;

/// <summary>
/// P8 — reportes del administrador (HU-076 a HU-078, HU-080, D-68 A), exportación CSV (HU-079, D-71), indicadores
/// (HU-075, HU-080, D-69 A) y "Mis indicadores" del asesor (D-70 A). Contrato: docs/criterios/P8-contrato-api.md.
/// El reloj está en T = 2026-10-26 15:00 UTC (10:00 en Colombia): el período por defecto es 2026-10-01 a 2026-10-31.
/// Las cifras son globales: cada prueba de conteo usa su propia API. SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class P8ReportesTests(P7Fixture fixture) : IClassFixture<P7Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    private const string Octubre = "?desde=2026-10-01&hasta=2026-10-31";
    private static readonly string[] Tipos = ["vehiculos", "solicitudes", "creditos", "ventas", "suscripciones"];

    private static Task<HttpResponseMessage> Get(InventarioApi api, string url, string? token) =>
        api.Enviar(HttpMethod.Get, url, token);

    private static async Task<JsonElement> Reporte(InventarioApi api, string tipo, string query = "") =>
        await Esperar(await Get(api, $"/api/admin/reportes/{tipo}{query}", api.Admin.Token), HttpStatusCode.OK);

    private static int N(JsonElement e, string campo) => e.GetProperty(campo).GetInt32();

    private static async Task AfirmarSinDatosDeClientes(HttpResponseMessage r, params string[] prohibidos)
    {
        var texto = await r.Content.ReadAsStringAsync();
        foreach (var p in prohibidos.Concat(["Visitante Ficticio", "Vendedor Ficticio", "Solicitante Ficticio", "Buscador Ficticio"]))
            Assert.DoesNotContain(p, texto, StringComparison.Ordinal);
        foreach (var campo in new[] { "\"nombre\":\"Visitante", "\"telefono\"", "\"correo\"", "\"mensaje\"", "\"observacion", "\"notas\"" })
            Assert.DoesNotContain(campo, texto, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================ permisos y filtros comunes

    [Fact(DisplayName = "CA-P8-06, 40, 49, 60: reportes, CSV, indicadores y actividad -> 401 sin sesión; 403 asesor y comprador")]
    public async Task Permisos_administrador()
    {
        var rutas = Tipos.SelectMany(t => new[] { $"/api/admin/reportes/{t}", $"/api/admin/reportes/{t}/csv" })
            .Concat(["/api/admin/indicadores", "/api/admin/vehiculos/1/actividad", "/api/admin/suscripciones/1/actividad"]);
        foreach (var ruta in rutas)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Get(_api, ruta, null)).StatusCode);
            foreach (var token in new[] { _api.A.Token, _api.TokenComprador })
                await Error(await Get(_api, ruta, token), HttpStatusCode.Forbidden, MsgSinPermiso);
        }
    }

    [Fact(DisplayName = "CA-P8-40, CA-P8-55: Mis indicadores -> 401 sin sesión; 403 administrador y comprador")]
    public async Task Permisos_asesor()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(_api, "/api/asesor/indicadores", null)).StatusCode);
        foreach (var token in new[] { _api.Admin.Token, _api.TokenComprador })
            await Error(await Get(_api, "/api/asesor/indicadores", token), HttpStatusCode.Forbidden, MsgSinPermiso);
        // El asesor no tiene CSV propio (CA-P8-54): no existe ninguna ruta /api/asesor/.../csv.
        Assert.Equal(HttpStatusCode.NotFound, (await Get(_api, "/api/asesor/indicadores/csv", _api.A.Token)).StatusCode);
    }

    [Fact(DisplayName = "CA-P8-10: un token de sesión cerrada -> 401 en reportes e indicadores")]
    public async Task Sesion_cerrada()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        Assert.Equal(HttpStatusCode.NoContent, (await Ayudas.Logout(api.C, api.Admin.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(api, "/api/admin/reportes/ventas", api.Admin.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(api, "/api/admin/indicadores", api.Admin.Token)).StatusCode);
    }

    [Theory(DisplayName = "CA-P8-04, CA-P8-48: filtros inválidos -> 400 { error, errores } en el reporte y en su CSV")]
    [InlineData("ventas", "?desde=2026-13-01", "desde", MensajesP8.Fecha)]
    [InlineData("ventas", "?desde=2026-10-31&hasta=2026-10-01", "hasta", MensajesP8.Rango)]
    [InlineData("ventas", "?desde=2025-01-01&hasta=2026-01-02", "hasta", MensajesP8.PeriodoMaximo)]
    [InlineData("ventas", "?asesorId=999999", "asesorId", MensajesP8.Asesor)]
    [InlineData("ventas", "?asesorId=abc", "asesorId", MensajesP8.Asesor)]
    [InlineData("vehiculos", "?categoriaId=999999", "categoriaId", MensajesP8.Categoria)]
    [InlineData("vehiculos", "?departamentoId=999999", "departamentoId", MensajesP8.Departamento)]
    [InlineData("vehiculos", "?municipioId=999999", "municipioId", MensajesP8.Municipio)]
    [InlineData("ventas", "?origen=TELEVISION", "origen", MensajesP8.Origen)]
    [InlineData("solicitudes", "?origen=OTRO", "origen", MensajesP8.Origen)]
    [InlineData("solicitudes", "?estado=PENDIENTE", "estado", MensajesP8.Estado)]
    [InlineData("solicitudes", "?resultado=APROBADO", "resultado", MensajesP8.Resultado)]
    [InlineData("solicitudes", "?origen=CREDITO&resultado=VENTA_CONCRETADA", "resultado", MensajesP8.ResultadoOrigen)]
    [InlineData("solicitudes", "?responsable=999999", "responsable", MensajesP8.Responsable)]
    [InlineData("creditos", "?resultado=VENTA_CONCRETADA", "resultado", MensajesP8.ResultadoOrigen)]
    [InlineData("creditos", "?estado=ABIERTO", "estado", MensajesP8.Estado)]
    [InlineData("creditos", "?canal=WEB", "canal", MensajesP8.Canal)]
    [InlineData("suscripciones", "?planId=999999", "planId", MensajesP8.Plan)]
    [InlineData("suscripciones", "?medio=TARJETA", "medio", MensajesP8.Medio)]
    [InlineData("suscripciones", "?pagina=0", "pagina", MensajesP8.Pagina)]
    [InlineData("vehiculos", "?tamano=101", "tamano", MensajesP8.Pagina)]
    public async Task Filtros_invalidos(string tipo, string query, string campo, string mensaje)
    {
        await Error400(await Get(_api, $"/api/admin/reportes/{tipo}{query}", _api.Admin.Token), campo, mensaje, MensajesP8.Filtros);
        if (campo is "pagina" or "tamano") return; // el CSV ignora la paginación
        await Error400(await Get(_api, $"/api/admin/reportes/{tipo}/csv{query}", _api.Admin.Token), campo, mensaje, MensajesP8.Filtros);
    }

    [Fact(DisplayName = "CA-P8-04: el municipio de otro departamento -> 400; indicadores validan el período")]
    public async Task Municipio_de_otro_departamento()
    {
        var antioquia = await _api.Leer(db => db.Municipios.Where(m => m.Id == _api.Medellin).Select(m => m.DepartamentoId).SingleAsync());
        var bogota = await _api.Leer(db => db.Municipios.Where(m => m.Id == _api.Bogota).Select(m => m.DepartamentoId).SingleAsync());
        Assert.NotEqual(antioquia, bogota);
        await Error400(await Get(_api, $"/api/admin/reportes/vehiculos?departamentoId={bogota}&municipioId={_api.Medellin}", _api.Admin.Token),
            "municipioId", MensajesP8.MunicipioDepartamento, MensajesP8.Filtros);
        await Error400(await Get(_api, "/api/admin/indicadores?desde=2026-10-31&hasta=2026-10-01", _api.Admin.Token),
            "hasta", MensajesP8.Rango, MensajesP8.Filtros);
        await Error400(await Get(_api, "/api/asesor/indicadores?desde=ayer", _api.A.Token), "desde", MensajesP8.Fecha, MensajesP8.Filtros);
    }

    [Fact(DisplayName = "CA-P8-02, CA-P8-05: sin filtros, el mes en curso; sin datos, 200 con ceros y listas vacías")]
    public async Task Periodo_por_defecto_y_vacio()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        foreach (var tipo in Tipos)
        {
            var r = await Reporte(api, tipo);
            Assert.Equal("2026-10-01", r.GetProperty("periodo").GetProperty("desde").GetString());
            Assert.Equal("2026-10-31", r.GetProperty("periodo").GetProperty("hasta").GetString());
            Assert.Equal(0, r.GetProperty("filas").GetProperty("total").GetInt32());
            Assert.Empty(r.GetProperty("filas").GetProperty("items").EnumerateArray());
        }
        var v = await Reporte(api, "vehiculos");
        Assert.All(v.GetProperty("inventarioPorEstado").EnumerateObject(), p => Assert.Equal(0, p.Value.GetInt32()));
        Assert.Equal(0, N(v, "publicacionesVisiblesHoy"));
        var s = await Reporte(api, "solicitudes");
        Assert.Equal(new[] { "CONTACTO_VEHICULO", "VENDE_TU_CAMION", "SEGURO_TODO_RIESGO", "CREDITO", "BUSQUEDA" },
            s.GetProperty("porOrigen").EnumerateArray().Select(x => x.GetProperty("origen").GetString()).ToArray());
        Assert.All(s.GetProperty("tiempoMedioCierre").EnumerateArray(), x => Assert.Equal(JsonValueKind.Null, x.GetProperty("dias").ValueKind));
        var c = await Reporte(api, "creditos");
        Assert.Equal(JsonValueKind.Null, c.GetProperty("porcentajeRemitido").ValueKind); // CA-P8-38: "Sin datos"
        var ventas = await Reporte(api, "ventas");
        Assert.Equal(4, ventas.GetProperty("porOrigen").GetArrayLength());
        Assert.Equal(0, ventas.GetProperty("vigentes").GetProperty("valorCop").GetInt64());
        var sus = await Reporte(api, "suscripciones");
        Assert.Equal(MensajesP8.AvisoPagos, sus.GetProperty("aviso").GetString()); // CA-P8-33
    }

    // ================================================================ 1. vehículos y publicaciones

    [Fact(DisplayName = "CA-P8-11 a 16: inventario por estado y categoría, publicaciones visibles, más contactados y filtros")]
    public async Task Reporte_vehiculos()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var (asesor, pub, slug) = await api.AsesorPublicado("Asesora Reporte Ficticia");
        var telefono = TelefonoUnico();
        await api.LeadNuevo(slug, telefono);
        await api.LeadNuevo(slug);
        var carro = await api.CrearId(api.A, "carro");
        await Esperar(await api.Accion(api.A.Token, carro, "reservar"), HttpStatusCode.OK);
        // Un vehículo DEMO sin publicación ni fotos registrado en septiembre (CA-P8-16).
        await api.Db(db =>
        {
            db.Vehiculos.Add(new Vehiculo
            {
                AsesorId = api.B.PerfilId, TipoVehiculoId = api.Maquinaria, MunicipioId = api.Bogota, Estado = Vehiculo.Inactivo,
                CreadoEn = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc)
            });
            return Task.CompletedTask;
        });

        var r = await api.Enviar(HttpMethod.Get, "/api/admin/reportes/vehiculos", api.Admin.Token);
        await AfirmarSinDatosDeClientes(r, telefono);
        var v = await Esperar(r, HttpStatusCode.OK);
        var estados = v.GetProperty("inventarioPorEstado");
        Assert.Equal(1, estados.GetProperty("ACTIVO").GetInt32());
        Assert.Equal(1, estados.GetProperty("RESERVADO").GetInt32());
        Assert.Equal(1, estados.GetProperty("INACTIVO").GetInt32());
        Assert.Equal(2, N(v, "registradosEnPeriodo"));
        var categorias = v.GetProperty("inventarioPorCategoria").EnumerateArray()
            .ToDictionary(x => x.GetProperty("nombre").GetString()!, x => x.GetProperty("total").GetInt32());
        Assert.Equal(1, categorias["Camión"]);
        Assert.Equal(1, categorias["Carro"]);
        Assert.Equal(1, categorias["Maquinaria"]);
        Assert.Equal(1, v.GetProperty("publicacionesPorEstado").GetProperty("PUBLICADA").GetInt32());
        Assert.Equal(1, N(v, "publicacionesVisiblesHoy"));
        var top = Assert.Single(v.GetProperty("masContactados").EnumerateArray());
        Assert.Equal(pub.GetProperty("vehiculoId").GetInt64(), top.GetProperty("vehiculoId").GetInt64());
        Assert.Equal(2, top.GetProperty("contactos").GetInt32());
        var filas = v.GetProperty("filas");
        Assert.Equal(2, filas.GetProperty("total").GetInt32());
        var fila = filas.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("vehiculoId").GetInt64() == top.GetProperty("vehiculoId").GetInt64());
        Assert.Equal(asesor.PerfilId, fila.GetProperty("asesor").GetProperty("id").GetInt64());
        Assert.Equal(2, fila.GetProperty("contactosEnPeriodo").GetInt32());
        Assert.Equal(IdDe(pub), fila.GetProperty("publicacion").GetProperty("id").GetInt64());

        // CA-P8-12: los filtros limitan todas las cifras; el período, solo los registrados y las filas.
        var soloAsesor = await Reporte(api, "vehiculos", $"?asesorId={asesor.PerfilId}");
        Assert.Equal(0, soloAsesor.GetProperty("inventarioPorEstado").GetProperty("RESERVADO").GetInt32());
        Assert.Equal(1, N(soloAsesor, "registradosEnPeriodo"));
        var soloCarros = await Reporte(api, "vehiculos", $"?categoriaId={api.Carro}");
        Assert.Equal(carro, soloCarros.GetProperty("filas").GetProperty("items")[0].GetProperty("vehiculoId").GetInt64());
        Assert.Equal(0, N(soloCarros, "publicacionesVisiblesHoy"));
        var bogota = await Reporte(api, "vehiculos", $"?municipioId={api.Bogota}");
        Assert.Equal(1, bogota.GetProperty("inventarioPorEstado").GetProperty("INACTIVO").GetInt32());
        Assert.Equal(0, N(bogota, "registradosEnPeriodo"));
        var septiembre = await Reporte(api, "vehiculos", "?desde=2026-09-01&hasta=2026-09-30");
        Assert.Equal(1, N(septiembre, "registradosEnPeriodo"));
        Assert.Empty(septiembre.GetProperty("masContactados").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, septiembre.GetProperty("filas").GetProperty("items")[0].GetProperty("publicacion").ValueKind);

        // CA-P8-14: eliminada cuenta aparte y no suma en visibles.
        await api.Inactivada(asesor.Token, IdDe(pub));
        await api.AccionPubOk(asesor.Token, IdDe(pub), "eliminar");
        var despues = await Reporte(api, "vehiculos");
        Assert.Equal(1, N(despues, "publicacionesEliminadas"));
        Assert.Equal(0, N(despues, "publicacionesVisiblesHoy"));
        Assert.False(despues.GetProperty("publicacionesPorEstado").TryGetProperty("ELIMINADA", out _));
    }

    // ================================================================ 2. solicitudes

    [Fact(DisplayName = "CA-P8-17 a 23: solicitudes por origen, resultado, responsable, tiempo medio y filtros combinados")]
    public async Task Reporte_solicitudes()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var (asesor, _, slug) = await api.AsesorPublicado("Asesor Solicitudes Ficticio");
        var c1 = await api.LeadNuevo(slug);
        var c2 = await api.LeadNuevo(slug);
        await api.AvisoNuevo(RutaVende);
        await api.CreditoNuevo();
        await api.BusquedaNueva(api.Camion);
        api.Hora(T.AddDays(2));
        await Esperar(await api.Cerrar(asesor.Token, c1.Id, Lead.VentaConcretada), HttpStatusCode.OK);
        // CA-P8-22: reabierto y cerrado de nuevo cuenta una vez, con el último resultado (el tiempo usa el primer cierre).
        await Esperar(await api.AccionLead(api.Admin.Token, c1.Id, "reabrir", new { motivo = MotivoP5 }), HttpStatusCode.OK);
        api.Hora(T.AddDays(3));
        await Esperar(await api.Cerrar(api.Admin.Token, c1.Id, Lead.NoInteresado), HttpStatusCode.OK);
        // CA-P8-19: c2 se reasigna a mano a Administración.
        await Esperar(await api.ReasignarLead(api.Admin.Token, c2.Id, null, administracion: true), HttpStatusCode.OK);

        var respuesta = await api.Enviar(HttpMethod.Get, "/api/admin/reportes/solicitudes", api.Admin.Token);
        await AfirmarSinDatosDeClientes(respuesta, c1.Telefono, c2.Telefono);
        var s = await Esperar(respuesta, HttpStatusCode.OK);
        Assert.Equal(5, N(s, "recibidas"));
        var contacto = s.GetProperty("porOrigen").EnumerateArray().Single(x => x.GetProperty("origen").GetString() == Lead.OrigenContactoVehiculo);
        Assert.Equal((2, 1, 1), (N(contacto, "recibidas"), N(contacto, "abiertas"), N(contacto, "cerradas")));
        Assert.Equal(1, N(s, "cerradasEnPeriodo"));
        var resultados = s.GetProperty("cerradasPorResultado").EnumerateArray().ToList();
        Assert.Equal(1, resultados.Single(x => x.GetProperty("origen").GetString() == Lead.OrigenContactoVehiculo &&
                                               x.GetProperty("resultado").GetString() == Lead.NoInteresado).GetProperty("total").GetInt32());
        Assert.Equal(0, resultados.Single(x => x.GetProperty("origen").GetString() == Lead.OrigenContactoVehiculo &&
                                               x.GetProperty("resultado").GetString() == Lead.VentaConcretada).GetProperty("total").GetInt32());
        // CA-P8-18: cada resultado solo en los orígenes donde es válido.
        Assert.DoesNotContain(resultados, x => x.GetProperty("origen").GetString() == Lead.OrigenVendeTuCamion &&
                                               x.GetProperty("resultado").GetString() == Lead.VentaConcretada);
        Assert.DoesNotContain(resultados, x => x.GetProperty("origen").GetString() == Lead.OrigenCredito &&
                                               x.GetProperty("resultado").GetString() == Lead.VehiculoEncontrado);
        var tiempo = s.GetProperty("tiempoMedioCierre").EnumerateArray().Single(x => x.GetProperty("origen").GetString() == Lead.OrigenContactoVehiculo);
        Assert.Equal(2.0, tiempo.GetProperty("dias").GetDouble());
        var porResponsable = s.GetProperty("contactosPorResponsable");
        Assert.Equal(1, N(porResponsable, "administracion"));
        Assert.Equal(1, N(porResponsable, "reasignadosAMano"));
        var deAsesor = Assert.Single(porResponsable.GetProperty("asesores").EnumerateArray());
        Assert.Equal((asesor.PerfilId, 1), (deAsesor.GetProperty("id").GetInt64(), N(deAsesor, "total")));
        var filaC1 = s.GetProperty("filas").GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetInt64() == c1.Id);
        Assert.Equal(Lead.NoInteresado, filaC1.GetProperty("resultado").GetString());
        Assert.Equal("ASESOR", filaC1.GetProperty("responsable").GetProperty("tipo").GetString());
        Assert.False(filaC1.GetProperty("vehiculo").ValueKind == JsonValueKind.Null);

        // CA-P8-20: filtros combinados.
        Assert.Equal(1, N(await Reporte(api, "solicitudes", "?origen=CONTACTO_VEHICULO&resultado=NO_INTERESADO"), "recibidas"));
        Assert.Equal(0, N(await Reporte(api, "solicitudes", "?origen=CONTACTO_VEHICULO&estado=ABIERTO&resultado=NO_INTERESADO"), "recibidas"));
        Assert.Equal(4, N(await Reporte(api, "solicitudes", "?responsable=ADMINISTRACION"), "recibidas"));
        Assert.Equal(1, N(await Reporte(api, "solicitudes", $"?responsable={asesor.PerfilId}"), "recibidas"));
        Assert.Equal(2, N(await Reporte(api, "solicitudes", $"?categoriaId={api.Camion}"), "recibidas"));
        Assert.Equal(0, N(await Reporte(api, "solicitudes", "?desde=2026-09-01&hasta=2026-09-30"), "recibidas"));

        // CA-P8-09: coincide con la bandeja con el mismo período.
        var bandeja = await Esperar(await api.ListaLeads(api.Admin.Token, Octubre), HttpStatusCode.OK);
        Assert.Equal(N(s, "recibidas"), bandeja.GetProperty("total").GetInt32());
        Assert.Equal(5, N(await Reporte(api, "solicitudes", "?tamano=2"), "recibidas"));
        var pagina3 = (await Reporte(api, "solicitudes", "?tamano=2&pagina=3")).GetProperty("filas");
        Assert.Equal((5, 3, 2, 1), (pagina3.GetProperty("total").GetInt32(), pagina3.GetProperty("pagina").GetInt32(),
            pagina3.GetProperty("tamano").GetInt32(), pagina3.GetProperty("items").GetArrayLength()));
    }

    // ================================================================ 3. créditos

    [Fact(DisplayName = "CA-P8-24 a 27: créditos por estado mostrado, canal, resultado y porcentaje remitido; sin montos")]
    public async Task Reporte_creditos()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var (_, _, slug) = await api.AsesorPublicado("Asesora Creditos Ficticia");
        var portada = await api.CreditoNuevo();
        var ficha = await api.CreditoNuevo(slug);
        await api.CreditoNuevo();
        await Esperar(await api.Cerrar(api.Admin.Token, portada.Id, Lead.RemitidoEntidad), HttpStatusCode.OK);
        await Esperar(await api.Nota(api.Admin.Token, ficha.Id), HttpStatusCode.Created);

        var c = await Reporte(api, "creditos");
        Assert.Equal(3, N(c, "recibidos"));
        Assert.Equal(1, c.GetProperty("porEstado").GetProperty("REGISTRADO").GetInt32());
        Assert.Equal(1, c.GetProperty("porEstado").GetProperty("EN_GESTION").GetInt32());
        Assert.Equal(1, c.GetProperty("porEstado").GetProperty("CERRADO").GetInt32());
        Assert.Equal(2, c.GetProperty("porCanal").GetProperty("PORTADA").GetInt32());
        Assert.Equal(1, c.GetProperty("porCanal").GetProperty("FICHA").GetInt32());
        Assert.Equal(1, N(c, "cerrados"));
        Assert.Equal(100.0, c.GetProperty("porcentajeRemitido").GetDouble());
        Assert.Equal(6, c.GetProperty("cerradosPorResultado").GetArrayLength());
        var texto = c.GetRawText();
        foreach (var prohibido in new[] { "monto", "ingreso", "entidadFinanciera", "aprobad" })
            Assert.DoesNotContain(prohibido, texto, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(1, N(await Reporte(api, "creditos", "?canal=FICHA"), "recibidos"));
        Assert.Equal(1, N(await Reporte(api, "creditos", "?estado=EN_GESTION"), "recibidos"));
        Assert.Equal(1, N(await Reporte(api, "creditos", "?resultado=REMITIDO_ENTIDAD"), "recibidos"));
        var fila = (await Reporte(api, "creditos", "?canal=FICHA")).GetProperty("filas").GetProperty("items")[0];
        Assert.Equal(ficha.Id, fila.GetProperty("id").GetInt64());
        Assert.Equal("EN_GESTION", fila.GetProperty("estado").GetString());
    }

    // ================================================================ 4. ventas

    [Fact(DisplayName = "CA-P8-28, 29, CA-P8-09: ventas vigentes con valor, revertidas aparte, cortes y coherencia con /api/ventas")]
    public async Task Reporte_ventas()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var vendido = await api.VehiculoVendido(api.A, "PORTAL", 95_000_000, observaciones: "Lo compró Pedro Comprador (prueba)");
        var revertido = await api.VehiculoVendido(api.B, "REFERIDO", 50_000_000);
        await Esperar(await api.RevertirVenta(api.B.Token, revertido), HttpStatusCode.OK);
        await api.VehiculoVendido(api.B, "CONTACTO_ASESOR", 40_000_000);

        var r = await api.Enviar(HttpMethod.Get, "/api/admin/reportes/ventas", api.Admin.Token);
        await AfirmarSinDatosDeClientes(r, "Pedro Comprador");
        var v = await Esperar(r, HttpStatusCode.OK);
        Assert.Equal(2, v.GetProperty("vigentes").GetProperty("cantidad").GetInt32());
        Assert.Equal(135_000_000, v.GetProperty("vigentes").GetProperty("valorCop").GetInt64());
        Assert.Equal(1, N(v, "revertidas"));
        var origen = v.GetProperty("porOrigen").EnumerateArray().ToDictionary(x => x.GetProperty("origen").GetString()!, x => x);
        Assert.Equal(95_000_000, origen["PORTAL"].GetProperty("valorCop").GetInt64());
        Assert.Equal(0, origen["REFERIDO"].GetProperty("cantidad").GetInt32());
        var asesores = v.GetProperty("porAsesor").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetInt64(), x => x.GetProperty("valorCop").GetInt64());
        Assert.Equal(95_000_000, asesores[api.A.PerfilId]);
        Assert.Equal(40_000_000, asesores[api.B.PerfilId]);
        Assert.Equal(3, v.GetProperty("filas").GetProperty("total").GetInt32());
        Assert.Equal("REVERTIDA", v.GetProperty("filas").GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("vehiculo").GetProperty("id").GetInt64() == revertido).GetProperty("estado").GetString());

        var lista = await api.VentasOk(api.Admin.Token, Octubre + "&estado=VIGENTE");
        Assert.Equal(v.GetProperty("vigentes").GetProperty("cantidad").GetInt32(), lista.GetProperty("total").GetInt32());
        var deB = await Reporte(api, "ventas", $"?asesorId={api.B.PerfilId}&origen=REFERIDO");
        Assert.Equal((0, 1), (deB.GetProperty("vigentes").GetProperty("cantidad").GetInt32(), N(deB, "revertidas")));
        Assert.Equal(0, (await Reporte(api, "ventas", $"?categoriaId={api.Carro}")).GetProperty("vigentes").GetProperty("cantidad").GetInt32());
        Assert.NotEqual(0, vendido);
    }

    // ================================================================ 5. suscripciones y pagos

    [Fact(DisplayName = "CA-P8-30 a 33: suscripciones por estado y plan, movimientos del período y pagos sin anulados")]
    public async Task Reporte_suscripciones()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var asesor = await api.CrearAsesor("Asesor Pagos Ficticio");
        var plan = await api.PlanConPrecio(100_000);
        var sus = (await Esperar(await api.AsignarConPago(asesor.PerfilId, plan, PagoValido()), HttpStatusCode.Created)).GetProperty("id").GetInt64();
        await api.AccionOk(sus, "renovar", new { pago = PagoValido(medio: "EFECTIVO", referencia: null) });
        var pagoRenovacion = (await api.PagosDb(sus)).Last().Id;
        await Esperar(await api.AnularPago(pagoRenovacion), HttpStatusCode.OK);
        // Un pago anterior a P7 (script 002) sin fecha ni medio, creado en octubre (CA-P8-32): se lista, no suma.
        await api.Db(db =>
        {
            db.Pagos.Add(new Pago
            {
                SuscripcionId = sus, ValorCop = 30_000, Estado = "APROBADO",
                CreadoEn = new DateTime(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc)
            });
            return Task.CompletedTask;
        });

        var s = await Reporte(api, "suscripciones");
        Assert.Equal(1, s.GetProperty("porEstado").GetProperty("ACTIVA").GetInt32());
        Assert.Equal(1, N(s, "asignadasEnPeriodo"));
        Assert.Equal(1, N(s, "renovadasEnPeriodo"));
        var vigente = Assert.Single(s.GetProperty("vigentesPorPlan").EnumerateArray());
        Assert.Equal(plan, vigente.GetProperty("id").GetInt64());
        var pagos = s.GetProperty("pagos");
        Assert.Equal(1, N(pagos, "cantidad"));
        Assert.Equal(100_000, pagos.GetProperty("totalRecibidoCop").GetInt64());
        var totalP7 = (await api.PagosOk(sus)).GetProperty("totalPagadoCop").GetInt64();
        Assert.Equal(totalP7, pagos.GetProperty("totalRecibidoCop").GetInt64()); // CA-P8-09
        Assert.Equal(1, pagos.GetProperty("anulados").GetProperty("cantidad").GetInt32());
        Assert.Equal(100_000, pagos.GetProperty("anulados").GetProperty("valorCop").GetInt64());
        Assert.Equal(1, N(pagos, "sinFechaPago"));
        var medios = pagos.GetProperty("porMedio").EnumerateArray().ToDictionary(x => x.GetProperty("codigo").GetString()!, x => N(x, "cantidad"));
        Assert.Equal(1, medios["TRANSFERENCIA"]);
        Assert.Equal(0, medios["EFECTIVO"]);
        Assert.False(medios.ContainsKey(Etiquetas.SinDato));
        Assert.Equal(3, s.GetProperty("filas").GetProperty("total").GetInt32());
        var antiguo = s.GetProperty("filas").GetProperty("items").EnumerateArray().Single(x => x.GetProperty("sinFechaPago").GetBoolean());
        Assert.Equal(("2026-10-05", "APROBADO", Etiquetas.SinDato), (antiguo.GetProperty("fecha").GetString(),
            antiguo.GetProperty("estado").GetString(), antiguo.GetProperty("medio").GetString()));
        Assert.DoesNotContain("TRX-PRUEBA-1", s.GetRawText(), StringComparison.Ordinal); // sin referencia

        var efectivo = await Reporte(api, "suscripciones", "?medio=EFECTIVO");
        Assert.Equal(0, N(efectivo.GetProperty("pagos"), "cantidad"));
        Assert.Equal(1, efectivo.GetProperty("pagos").GetProperty("anulados").GetProperty("cantidad").GetInt32());
        Assert.Equal(0, N(await Reporte(api, "suscripciones", $"?asesorId={api.A.PerfilId}"), "asignadasEnPeriodo"));
    }

    // ================================================================ exportación CSV

    [Fact(DisplayName = "CA-P8-42 a 46, 50: CSV UTF-8 con BOM, ';', adjunto, no-store, sin datos de clientes, contra fórmulas y auditado")]
    public async Task Exportar_csv()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var formula = await api.CrearAsesor("=HIPERVINCULO(\"x\");Ficticio");
        await api.VehiculoVendido(formula, "PORTAL", 95_000_000, observaciones: "Pedro Comprador pagó (prueba)");

        var r = await api.Enviar(HttpMethod.Get, "/api/admin/reportes/ventas/csv", api.Admin.Token);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("text/csv", r.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", r.Content.Headers.ContentType.CharSet);
        Assert.Equal("attachment", r.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("reporte-ventas-2026-10-01_2026-10-31.csv", r.Content.Headers.ContentDisposition.FileName?.Trim('"'));
        Assert.True(r.Headers.CacheControl?.NoStore, "Falta Cache-Control: no-store");
        var bytes = await r.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var texto = Encoding.UTF8.GetString(bytes[3..]);
        Assert.Contains("Reporte;Ventas\r\n", texto);
        Assert.Contains("Ventas vigentes;1;95000000", texto);
        Assert.Contains("\"'=HIPERVINCULO(\"\"x\"\");Ficticio\"", texto); // apóstrofo y comillas
        Assert.DoesNotContain("Pedro Comprador", texto);
        Assert.Contains("Portal web", texto);

        var auditoria = Assert.Single(await api.Auditoria("Reporte", "ventas"));
        Assert.Equal("EXPORTAR", auditoria.Accion);
        Assert.Equal(api.Admin.Id, auditoria.UsuarioId);
        var datos = JsonDocument.Parse(auditoria.DatosNuevos!).RootElement;
        Assert.Equal(1, datos.GetProperty("filas").GetInt32());
        Assert.Equal("2026-10-01", datos.GetProperty("filtros").GetProperty("desde").GetString());
        Assert.DoesNotContain("95000000", auditoria.DatosNuevos!);

        // Los cinco reportes se exportan con los mismos filtros.
        foreach (var tipo in Tipos)
        {
            var csv = await api.Enviar(HttpMethod.Get, $"/api/admin/reportes/{tipo}/csv?desde=2026-10-01&hasta=2026-10-15", api.Admin.Token);
            Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
            Assert.Equal($"reporte-{tipo}-2026-10-01_2026-10-15.csv", csv.Content.Headers.ContentDisposition!.FileName?.Trim('"'));
            Assert.Contains("\r\nFilas\r\n", Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync()));
        }
        Assert.Equal(6, await api.Leer(db => db.AuditoriaEventos.CountAsync(a => a.Entidad == "Reporte" && a.Accion == "EXPORTAR")));
    }

    [Theory(DisplayName = "CA-P8-44: celdas que empiezan por = + - @, tabulador o retorno llevan apóstrofo; ; y comillas, entre comillas")]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+573001234567", "'+573001234567")]
    [InlineData("-3", "'-3")]
    [InlineData("@SUMA(A1)", "'@SUMA(A1)")]
    [InlineData("\tx", "'\tx")]
    [InlineData("\rx", "\"'\rx\"")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("di \"hola\"", "\"di \"\"hola\"\"\"")]
    [InlineData("Camión NPR", "Camión NPR")]
    [InlineData("", "")]
    public void Celda_csv(string valor, string esperado) => Assert.Equal(esperado, Csv.Celda(valor));

    [Fact(DisplayName = "CA-P8-47: más de 10.000 filas -> 400, sin archivo parcial ni auditoría")]
    public async Task Exportar_demasiadas_filas()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        await api.Db(db =>
        {
            for (var i = 0; i < 10_001; i++)
                db.Leads.Add(new Lead
                {
                    Origen = Lead.OrigenVendeTuCamion, Nombre = "Ficticio", Telefono = "+573000000000", Mensaje = "Prueba",
                    CreadoEn = T, ActualizadoEn = T
                });
            return Task.CompletedTask;
        });
        await Error(await api.Enviar(HttpMethod.Get, "/api/admin/reportes/solicitudes/csv", api.Admin.Token),
            HttpStatusCode.BadRequest, MensajesP8.DemasiadasFilas);
        Assert.Equal(0, await api.Leer(db => db.AuditoriaEventos.CountAsync(a => a.Entidad == "Reporte")));
        // En pantalla sí se consulta (paginado).
        Assert.Equal(10_001, N(await Reporte(api, "solicitudes"), "recibidas"));
    }

    // ================================================================ indicadores

    [Fact(DisplayName = "CA-P8-34 a 38: indicadores del período, período anterior, porcentajes sin datos y 4 gráficos")]
    public async Task Indicadores_administrador()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var (asesor, _, slug) = await api.AsesorPublicado("Asesora Indicadores Ficticia");
        var c1 = await api.LeadNuevo(slug);
        await api.LeadNuevo(slug);
        await Esperar(await api.Cerrar(asesor.Token, c1.Id, Lead.VentaConcretada), HttpStatusCode.OK);
        var credito = await api.CreditoNuevo();
        await Esperar(await api.Cerrar(api.Admin.Token, credito.Id, Lead.RemitidoEntidad), HttpStatusCode.OK);
        await api.AvisoNuevo(RutaSeguro);
        await api.VehiculoVendido(api.A, "PORTAL", 95_000_000);
        var plan = await api.PlanConPrecio(100_000);
        await Esperar(await api.AsignarConPago(api.B.PerfilId, plan, PagoValido()), HttpStatusCode.Created);

        var r = await Esperar(await Get(api, "/api/admin/indicadores", api.Admin.Token), HttpStatusCode.OK);
        Assert.Equal("2026-08-31", r.GetProperty("periodoAnterior").GetProperty("desde").GetString());
        Assert.Equal("2026-09-30", r.GetProperty("periodoAnterior").GetProperty("hasta").GetString());
        var i = r.GetProperty("indicadores");
        Assert.Equal(2, i.GetProperty("contactosRecibidos").GetProperty("actual").GetInt64());
        Assert.Equal(2, i.GetProperty("contactosRecibidos").GetProperty("diferencia").GetInt64());
        Assert.Equal(100.0, i.GetProperty("porcentajeVentaConcretada").GetProperty("actual").GetDouble());
        Assert.Equal(JsonValueKind.Null, i.GetProperty("porcentajeVentaConcretada").GetProperty("anterior").ValueKind);
        Assert.Equal(JsonValueKind.Null, i.GetProperty("porcentajeVentaConcretada").GetProperty("diferencia").ValueKind);
        Assert.Equal(1, i.GetProperty("ventasVigentes").GetProperty("actual").GetInt64());
        Assert.Equal(95_000_000, i.GetProperty("valorVendidoCop").GetProperty("actual").GetInt64());
        Assert.Equal(2, i.GetProperty("solicitudesAdministracion").GetProperty("actual").GetInt64());
        Assert.Equal(1, i.GetProperty("creditosRemitidos").GetProperty("actual").GetInt64());
        Assert.Equal(100_000, i.GetProperty("ingresosPlanesCop").GetProperty("actual").GetInt64());
        Assert.Equal(0.0, i.GetProperty("tiempoMedioCierreDias").GetProperty("actual").GetDouble());
        Assert.Equal(1, N(r, "publicacionesVisiblesHoy"));
        var g = r.GetProperty("graficos");
        Assert.Equal(4, g.EnumerateObject().Count());
        var meses = g.GetProperty("ventasPorMes").EnumerateArray().ToList();
        Assert.Equal(12, meses.Count);
        Assert.Equal(("2025-11", "2026-10"), (meses[0].GetProperty("mes").GetString(), meses[^1].GetProperty("mes").GetString()));
        Assert.Equal(1, N(meses[^1], "cantidad"));
        Assert.Equal(6, g.GetProperty("contactosPorResultado").GetArrayLength());
        Assert.Equal(5, g.GetProperty("solicitudesPorOrigen").GetArrayLength());
        // CA-P8-41: sin visitas ni navegación.
        Assert.DoesNotContain("visita", r.GetRawText(), StringComparison.OrdinalIgnoreCase);

        // El panel Inicio de P7 sigue sin dinero (CA-P8-39).
        var panel = await Esperar(await api.Panel(), HttpStatusCode.OK);
        Assert.DoesNotContain("Cop", panel.GetRawText(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "CA-P8-51 a 54: Mis indicadores solo con lo del asesor, sin créditos, avisos ni pagos; asesorId se ignora")]
    public async Task Indicadores_asesor()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var (uno, _, slugUno) = await api.AsesorPublicado("Asesora Uno Ficticia");
        var (_, _, slugDos) = await api.AsesorPublicado("Asesor Dos Ficticio");
        var contacto = await api.LeadNuevo(slugUno);
        await api.LeadNuevo(slugDos);
        await api.LeadNuevo(slugDos);
        await api.CreditoNuevo(slugUno);                      // crédito desde su ficha: no lo ve (D-58 A)
        await api.AvisoNuevo(RutaVende);
        await Esperar(await api.Cerrar(uno.Token, contacto.Id, Lead.NoRespondio), HttpStatusCode.OK);
        await api.VehiculoVendido(uno, "REFERIDO", 70_000_000);
        await api.VehiculoVendido(api.B, "PORTAL", 10_000_000);   // de otro asesor

        var r = await api.Enviar(HttpMethod.Get, "/api/asesor/indicadores", uno.Token);
        await AfirmarSinDatosDeClientes(r, contacto.Telefono);
        var m = await Esperar(r, HttpStatusCode.OK);
        Assert.Equal(new[] { "contactosAbiertos", "contactosPorResultado", "contactosRecibidos", "periodo", "publicacionesVisiblesHoy",
                "vehiculosPorEstado", "ventasVigentes" },
            m.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(1, N(m, "contactosRecibidos"));
        Assert.Equal(0, N(m, "contactosAbiertos"));
        Assert.Equal(1, m.GetProperty("contactosPorResultado").EnumerateArray()
            .Single(x => x.GetProperty("resultado").GetString() == Lead.NoRespondio).GetProperty("total").GetInt32());
        Assert.Equal(1, N(m, "publicacionesVisiblesHoy"));
        Assert.Equal(1, m.GetProperty("vehiculosPorEstado").GetProperty("VENDIDO").GetInt32());
        Assert.Equal(1, m.GetProperty("vehiculosPorEstado").GetProperty("ACTIVO").GetInt32());
        Assert.Equal(70_000_000, m.GetProperty("ventasVigentes").GetProperty("valorCop").GetInt64());
        foreach (var prohibido in new[] { "credito", "pago", "aviso", "busqueda", "ingreso", "anterior" })
            Assert.DoesNotContain(prohibido, m.GetRawText(), StringComparison.OrdinalIgnoreCase);

        // CA-P8-52: asesorId y cualquier otro filtro se ignoran.
        var conOtro = await Esperar(await api.Enviar(HttpMethod.Get, $"/api/asesor/indicadores?asesorId={api.B.PerfilId}&origen=CREDITO",
            uno.Token), HttpStatusCode.OK);
        Assert.Equal(m.GetRawText(), conOtro.GetRawText());
    }
}
