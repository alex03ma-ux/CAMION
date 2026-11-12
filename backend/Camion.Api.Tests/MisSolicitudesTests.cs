using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;
using static Camion.Api.Tests.Infraestructura.AyudasP5b;
using static Camion.Api.Tests.Infraestructura.AyudasP6;

namespace Camion.Api.Tests;

/// <summary>
/// P6 — "Mis solicitudes" del comprador (D-53, D-59; CA-P6-66 a CA-P6-76): solo COMPRADOR, solo lo enviado con su sesión,
/// los cinco orígenes, estados mostrados, vehículo disponible o no, vehículos recomendados y nada interno. SQLite en memoria.
/// </summary>
public sealed class MisSolicitudesTests(P5Fixture fixture) : IClassFixture<P5Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    private static readonly string[] Claves = ["id", "creadoEn", "tipo", "resumen", "estado", "vehiculo", "recomendados"];

    [Fact(DisplayName = "CA-P6-66, 67, 68, 70: una sola lista con contacto, avisos, créditos y búsqueda enviados con su sesión, de la más reciente a la más antigua; ni las anónimas con su teléfono ni las de otro comprador; sin datos internos")]
    public async Task Lista_de_los_cinco_origenes()
    {
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor Mis Solicitudes");
        var (token, _) = await _api.CompradorNuevo();
        var (otroToken, _) = await _api.CompradorNuevo();
        var t = TelefonoUnico();
        var minuto = 0;
        void Siguiente() => _api.Hora(T.AddMinutes(++minuto));

        Siguiente(); await _api.ContactoOk(CuerpoContacto(slug, t), token);
        Siguiente(); await _api.AvisoOk(RutaVende, CuerpoVende(t), token);
        Siguiente(); await _api.AvisoOk(RutaSeguro, CuerpoSeguro(t, "MAQUINARIA"), token);
        Siguiente(); await _api.SolicitudOk(RutaCredito, CuerpoCredito(telefono: t), token);
        Siguiente(); await _api.SolicitudOk(RutaCredito, CuerpoCredito(slug, t), token);
        Siguiente(); await _api.SolicitudOk(RutaBusqueda, InventarioApi.Con(CuerpoBusqueda(_api.Camion, t),
            ("marcaId", _api.Kenworth), ("lineaId", _api.T800)), token);
        // Sin sesión (aunque con su teléfono) y de otro comprador: no aparecen (D-59 a A).
        Siguiente(); await _api.SolicitudOk(RutaCredito, CuerpoCredito(telefono: TelefonoUnico()));
        Siguiente(); await _api.SolicitudOk(RutaBusqueda, CuerpoBusqueda(_api.Carro), otroToken);
        _api.Hora(T);

        var json = await Esperar(await _api.Mias(token), HttpStatusCode.OK);
        Assert.Equal(6, json.GetProperty("total").GetInt32());
        Assert.Equal(1, json.GetProperty("pagina").GetInt32());
        Assert.Equal(20, json.GetProperty("tamano").GetInt32());
        var items = json.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new[] { "BUSQUEDA", "CREDITO", "CREDITO", "SEGURO_TODO_RIESGO", "VENDE_TU_CAMION", "CONTACTO_VEHICULO" },
            items.Select(i => i.GetProperty("tipo").GetString()).ToArray());
        Assert.Equal(new[] { "Camión Kenworth T800", "Camión de prueba listo", "Sin vehículo asociado", "Maquinaria",
                "Venta de tu vehículo", "Camión de prueba listo" },
            items.Select(i => i.GetProperty("resumen").GetString()).ToArray());
        Assert.Equal(new[] { "REGISTRADA", "REGISTRADA", "REGISTRADA", "PENDIENTE", "PENDIENTE", "PENDIENTE" },
            items.Select(i => i.GetProperty("estado").GetString()).ToArray());
        foreach (var i in items)
            Assert.Equal(Claves, i.EnumerateObject().Select(p => p.Name).ToArray());

        var ficha = items[1].GetProperty("vehiculo");
        Assert.Equal("Camión de prueba listo", ficha.GetProperty("titulo").GetString());
        Assert.Equal(slug, ficha.GetProperty("slug").GetString());
        Assert.True(ficha.GetProperty("disponible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, items[2].GetProperty("vehiculo").ValueKind);
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("vehiculo").ValueKind);
        Assert.Equal(0, items[0].GetProperty("recomendados").GetArrayLength());

        // CA-P6-68: nada interno ni datos de contacto en la respuesta.
        var texto = json.ToString();
        foreach (var prohibido in new[] { t, "Solicitante Ficticio", "ADMINISTRACION", "consentimientos", "notas", "responsable",
                     "resultadoCierre", asesor.Nombre, "mensaje" })
            Assert.DoesNotContain(prohibido, texto);
        Assert.Equal(1, (await Esperar(await _api.Mias(otroToken), HttpStatusCode.OK)).GetProperty("total").GetInt32());
        _ = pub;
    }

    [Fact(DisplayName = "CA-P6-70: En gestión con una nota y Finalizada al cerrar (sin el resultado); contactos y avisos: Pendiente y Finalizada; CA-P6-71: vehículo no disponible sin enlace")]
    public async Task Estados_y_vehiculo_no_disponible()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor Estados Mis Solicitudes");
        var (token, _) = await _api.CompradorNuevo();
        var credito = await _api.CreditoNuevo(slug, token);
        var aviso = await _api.AvisoNuevo(RutaVende, token: token);

        async Task<string> Estado(long id) =>
            (await Esperar(await _api.Mias(token, $"/{id}"), HttpStatusCode.OK)).GetProperty("estado").GetString()!;
        Assert.Equal("REGISTRADA", await Estado(credito.Id));
        await Esperar(await _api.Nota(_api.Admin.Token, credito.Id), HttpStatusCode.Created);
        Assert.Equal("EN_GESTION", await Estado(credito.Id));
        await Esperar(await _api.Cerrar(_api.Admin.Token, credito.Id, "REMITIDO_ENTIDAD"), HttpStatusCode.OK);
        Assert.Equal("FINALIZADA", await Estado(credito.Id));
        Assert.Equal("PENDIENTE", await Estado(aviso.Id));
        await Esperar(await _api.Cerrar(_api.Admin.Token, aviso.Id, "ATENDIDA"), HttpStatusCode.OK);
        Assert.Equal("FINALIZADA", await Estado(aviso.Id));
        var lista = (await Esperar(await _api.Mias(token), HttpStatusCode.OK)).ToString();
        Assert.DoesNotContain("REMITIDO_ENTIDAD", lista);
        Assert.DoesNotContain("ATENDIDA", lista);
        Assert.DoesNotContain(NotaP6, lista);

        await _api.Inactivada(asesor.Token, IdDe(pub));
        var v = (await Esperar(await _api.Mias(token, $"/{credito.Id}"), HttpStatusCode.OK)).GetProperty("vehiculo");
        Assert.False(v.GetProperty("disponible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, v.GetProperty("slug").ValueKind);
        Assert.Equal("Camión de prueba listo", v.GetProperty("titulo").GetString());
    }

    [Fact(DisplayName = "CA-P6-72: la búsqueda muestra los vehículos relacionados vigentes y visibles con su tarjeta, sin la nota interna; retirados o no visibles no aparecen")]
    public async Task Recomendados()
    {
        _api.Hora(T);
        var (asesor, pub, slug) = await _api.AsesorPublicado("Asesor Recomendados");
        var (_, pub2, slug2) = await _api.AsesorPublicado("Asesor Recomendados 2");
        var (token, _) = await _api.CompradorNuevo();
        var busqueda = await _api.BusquedaNueva(_api.Camion, token);
        const string notaInterna = "Nota interna que no ve el cliente (prueba)";
        await Esperar(await _api.Relacionar(_api.Admin.Token, busqueda.Id, IdDe(pub), notaInterna), HttpStatusCode.Created);
        _api.Hora(T.AddMinutes(1));
        await Esperar(await _api.Relacionar(_api.Admin.Token, busqueda.Id, IdDe(pub2)), HttpStatusCode.Created);

        var item = (await _api.ItemsMias(token)).Single(i => i.GetProperty("id").GetInt64() == busqueda.Id);
        var recomendados = item.GetProperty("recomendados").EnumerateArray().ToArray();
        Assert.Equal(new[] { slug2, slug }, recomendados.Select(r => r.GetProperty("slug").GetString()).ToArray());
        Assert.Equal(100_000_000, recomendados[0].GetProperty("precioCop").GetInt64());
        Assert.Equal("Camión de prueba listo", recomendados[0].GetProperty("titulo").GetString());
        Assert.True(recomendados[0].TryGetProperty("fotoPortada", out _));
        Assert.DoesNotContain(notaInterna, item.ToString());

        await Esperar(await _api.Retirar(_api.Admin.Token, busqueda.Id, IdDe(pub2)), HttpStatusCode.OK);
        await _api.Inactivada(asesor.Token, IdDe(pub));
        var detalle = await Esperar(await _api.Mias(token, $"/{busqueda.Id}"), HttpStatusCode.OK);
        Assert.Equal(0, detalle.GetProperty("recomendados").GetArrayLength());
        _api.Hora(T);
    }

    [Fact(DisplayName = "CA-P6-73: detalle propio -> 200; ajeno o inexistente -> 404 'Solicitud no encontrada.'")]
    public async Task Detalle_propio_y_ajeno()
    {
        _api.Hora(T);
        var (token, _) = await _api.CompradorNuevo();
        var (otro, _) = await _api.CompradorNuevo();
        var mio = await _api.CreditoNuevo(token: token);
        var ajeno = await _api.CreditoNuevo(token: otro);
        var anonimo = await _api.CreditoNuevo();
        var d = await Esperar(await _api.Mias(token, $"/{mio.Id}"), HttpStatusCode.OK);
        Assert.Equal(mio.Id, d.GetProperty("id").GetInt64());
        Assert.Equal(Claves, d.EnumerateObject().Select(p => p.Name).ToArray());
        foreach (var id in new[] { ajeno.Id, anonimo.Id, 999_999 })
            await Error(await _api.Mias(token, $"/{id}"), HttpStatusCode.NotFound, MsgSolicitudNoEncontrada);
    }

    [Fact(DisplayName = "CA-P6-63, 69, 74: asesor y administrador -> 403; sin token -> 401; no hay rutas para editar, cancelar ni borrar")]
    public async Task Permisos_y_solo_lectura()
    {
        _api.Hora(T);
        var (token, _) = await _api.CompradorNuevo();
        var lead = await _api.CreditoNuevo(token: token);
        foreach (var t in new[] { _api.A.Token, _api.Admin.Token })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await _api.Mias(t)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _api.Mias(t, $"/{lead.Id}")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Mias(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Mias(null, $"/{lead.Id}")).StatusCode);
        foreach (var metodo in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Patch })
        {
            var r = await _api.Enviar(metodo, $"{RutaMias}/{lead.Id}", token, new { estado = "CERRADO" });
            Assert.Equal(HttpStatusCode.MethodNotAllowed, r.StatusCode);
        }
        Assert.Equal("ABIERTO", (await _api.LeadDb(lead.Id)).Estado);
    }

    [Fact(DisplayName = "CA-P6-66, 75: paginación de 20 por defecto (tamano 1 a 50); valores no válidos -> 400; sin solicitudes -> lista vacía")]
    public async Task Paginacion_y_vacia()
    {
        _api.Hora(T);
        var (token, _) = await _api.CompradorNuevo();
        var vacia = await Esperar(await _api.Mias(token), HttpStatusCode.OK);
        Assert.Equal(0, vacia.GetProperty("total").GetInt32());
        Assert.Equal(0, vacia.GetProperty("items").GetArrayLength());
        for (var i = 0; i < 3; i++)
        {
            _api.Hora(T.AddMinutes(i));
            await _api.CreditoNuevo(token: token);
        }
        _api.Hora(T);
        var p1 = await Esperar(await _api.Mias(token, "?tamano=2"), HttpStatusCode.OK);
        Assert.Equal(3, p1.GetProperty("total").GetInt32());
        Assert.Equal(2, p1.GetProperty("items").GetArrayLength());
        var p2 = await Esperar(await _api.Mias(token, "?tamano=2&pagina=2"), HttpStatusCode.OK);
        Assert.Equal(1, p2.GetProperty("items").GetArrayLength());
        Assert.Equal(0, (await Esperar(await _api.Mias(token, "?pagina=9"), HttpStatusCode.OK)).GetProperty("items").GetArrayLength());
        foreach (var (q, campo) in new[] { ("?pagina=0", "pagina"), ("?tamano=51", "tamano"), ("?tamano=abc", "tamano") })
            await Error400(await _api.Mias(token, q), campo, MsgPaginaP6, MsgFiltrosP6);
    }
}
