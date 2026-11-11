using System.Net;
using System.Text.Json;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP6;

namespace Camion.Api.Tests;

/// <summary>
/// P6 — listas públicas del formulario "Busco un vehículo" (HU-034, D-17, D-29, D-32): GET /api/catalogo/listas-busqueda,
/// anónima, de solo lectura, solo entradas activas y con los mismos ids que acepta POST /api/busquedas. SQLite en memoria.
/// </summary>
public sealed class ListasBusquedaTests
{
    private const string Ruta = "/api/catalogo/listas-busqueda";

    [Fact(DisplayName = "P6 listas-busqueda: anónima con caché corta; solo categorías, marcas y modelos activos; modelos con sus categorías activas; DIVIPOLA completo; sin datos personales; con o sin token responde igual")]
    public async Task Listas_publicas()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        await api.Db(db =>
        {
            db.LineasTipos.Add(new LineaTipo { LineaId = api.T800, TipoVehiculoId = api.Camion });
            db.LineasTipos.Add(new LineaTipo { LineaId = api.T800, TipoVehiculoId = api.Remolque });   // categoría inactiva
            db.Lineas.Add(new Linea { MarcaId = api.Kenworth, Nombre = "W900 inactivo", Activo = false });
            return Task.CompletedTask;
        });

        var r = await api.Enviar(HttpMethod.Get, Ruta, null);
        var json = await Esperar(r, HttpStatusCode.OK);
        Assert.Equal("public, max-age=300", r.Headers.CacheControl?.ToString());
        Assert.Equal(new[] { "tipos", "marcas", "departamentos" }, json.EnumerateObject().Select(p => p.Name).ToArray());

        var tipos = json.GetProperty("tipos").EnumerateArray().Select(t => t.GetProperty("id").GetInt64()).ToArray();
        Assert.Equal(new[] { api.Camion, api.Carro, api.Carroceria, api.Maquinaria }.Order(), tipos.Order());

        var marcas = json.GetProperty("marcas").EnumerateArray().ToArray();
        Assert.Equal(new[] { "Chevrolet", "Kenworth" }, marcas.Select(m => m.GetProperty("nombre").GetString()).ToArray());
        var kenworth = marcas.Single(m => m.GetProperty("id").GetInt64() == api.Kenworth);
        var t800 = Assert.Single(kenworth.GetProperty("lineas").EnumerateArray());
        Assert.Equal(api.T800, t800.GetProperty("id").GetInt64());
        Assert.Equal(new[] { api.Camion }, t800.GetProperty("tipoIds").EnumerateArray().Select(x => x.GetInt64()).ToArray());
        var npr = marcas.Single(m => m.GetProperty("id").GetInt64() == api.Chevrolet).GetProperty("lineas")[0];
        Assert.Equal(0, npr.GetProperty("tipoIds").GetArrayLength());

        var deptos = json.GetProperty("departamentos").EnumerateArray().ToArray();
        Assert.Equal(2, deptos.Length);
        var municipios = deptos.SelectMany(d => d.GetProperty("municipios").EnumerateArray().Select(m => m.GetProperty("id").GetInt64()));
        Assert.Equal(new[] { api.Medellin, api.Bogota }.Order(), municipios.Order());

        var texto = json.ToString();
        foreach (var prohibido in new[] { "Dina", "Remolque", "W900", api.A.Email, api.Admin.Email, "telefono", "correo" })
            Assert.DoesNotContain(prohibido, texto, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(texto, (await Esperar(await api.Enviar(HttpMethod.Get, Ruta, api.TokenComprador), HttpStatusCode.OK)).ToString());
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await api.Enviar(HttpMethod.Post, Ruta, null, new { })).StatusCode);
    }

    [Fact(DisplayName = "P6 listas-busqueda: los ids de las listas se aceptan en POST /api/busquedas; un municipio de otro departamento o un modelo de otra marca o categoría se rechazan en el servidor")]
    public async Task Ids_coherentes_con_la_busqueda()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        await api.Db(db =>
        {
            db.LineasTipos.Add(new LineaTipo { LineaId = api.T800, TipoVehiculoId = api.Camion });
            return Task.CompletedTask;
        });
        var json = await Esperar(await api.Enviar(HttpMethod.Get, Ruta, null), HttpStatusCode.OK);
        var kenworth = json.GetProperty("marcas").EnumerateArray().Single(m => m.GetProperty("nombre").GetString() == "Kenworth");
        var linea = kenworth.GetProperty("lineas")[0];
        var tipo = linea.GetProperty("tipoIds")[0].GetInt64();
        var depto = json.GetProperty("departamentos")[0];
        var otroDepto = json.GetProperty("departamentos")[1];
        var municipio = depto.GetProperty("municipios")[0].GetProperty("id").GetInt64();

        await api.SolicitudOk(RutaBusqueda, InventarioApi.Con(CuerpoBusqueda(tipo),
            ("marcaId", kenworth.GetProperty("id").GetInt64()), ("lineaId", linea.GetProperty("id").GetInt64()),
            ("departamentoId", depto.GetProperty("id").GetInt64()), ("municipioId", municipio)));

        await Error400(await api.Solicitar(RutaBusqueda, InventarioApi.Con(CuerpoBusqueda(api.Camion),
            ("departamentoId", otroDepto.GetProperty("id").GetInt64()), ("municipioId", municipio))), "municipioId", MsgCiudad);
        await Error400(await api.Solicitar(RutaBusqueda, InventarioApi.Con(CuerpoBusqueda(api.Camion),
            ("marcaId", api.Chevrolet), ("lineaId", api.T800))), "lineaId", MsgModelo);
        await Error400(await api.Solicitar(RutaBusqueda, InventarioApi.Con(CuerpoBusqueda(api.Carro),
            ("marcaId", api.Kenworth), ("lineaId", api.T800))), "lineaId", MsgModelo);
    }
}
