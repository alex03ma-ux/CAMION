using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.CatalogoJson;
using static Camion.Api.Tests.Infraestructura.CatalogoApi;

namespace Camion.Api.Tests;

/// <summary>P3: GET /api/catalogo/publicaciones/{slug} (RN-SE-005, RN-VE-013, RN-VE-014, HU-004).</summary>
public sealed class CatalogoDetalleTests(CatalogoReferencia referencia) : IClassFixture<CatalogoReferencia>
{
    private CatalogoApi Ref => referencia.Api;

    private static readonly string[] ClavesDetalle =
    [
        "capacidadCargaKg", "carroceria", "cilindrajeCc", "combustible", "departamento", "descripcionComercial",
        "descripcionTecnica", "destacado", "estadoComercial", "fotoPortada", "fotos", "kilometraje", "linea", "marca",
        "modeloAnio", "municipio", "negociable", "pesoBrutoKg", "precioCop", "publicadaEn", "slug", "tipo", "titulo"
    ];

    // Cuerpo común del 404 (se compara ya interpretado: el serializador escapa los caracteres no ASCII).
    private static void AfirmarCuerpo404(string texto)
    {
        var json = JsonDocument.Parse(texto).RootElement;
        Assert.Equal(new[] { "error" }, Claves(json));
        Assert.Equal(MsgNoDisponible, json.GetProperty("error").GetString());
    }

    private async Task<string> Texto404(HttpResponseMessage r)
    {
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.NotFound, $"se esperaba 404 y llegó {(int)r.StatusCode}: {texto}");
        return texto;
    }

    [Fact(DisplayName = "CA-DET-01, CA-003-01: detalle de V1 con tarjeta + ficha + 3 fotos ordenadas, portada solo la de orden 2")]
    public async Task Detalle_V1()
    {
        var d = await AyudasP2.Ok(await Ref.Detalle("chevrolet-npr-2020"));
        Assert.Equal(ClavesDetalle, Claves(d));
        Assert.Equal("chevrolet-npr-2020", d.GetProperty("slug").GetString());
        Assert.Equal("Chevrolet NPR 2020 furgón", d.GetProperty("titulo").GetString());
        Assert.Equal("120000000", d.GetProperty("precioCop").GetRawText());
        Assert.True(d.GetProperty("destacado").GetBoolean());
        Assert.Equal("DISPONIBLE", d.GetProperty("estadoComercial").GetString());
        Assert.Equal("Camión", d.GetProperty("tipo").GetString());
        Assert.Equal("NPR", d.GetProperty("linea").GetString());
        Assert.Equal("Camión en buen estado, listo para trabajar.", d.GetProperty("descripcionComercial").GetString());
        Assert.Equal("Motor revisado, llantas nuevas.", d.GetProperty("descripcionTecnica").GetString());
        Assert.Equal("Furgón", d.GetProperty("carroceria").GetString());
        Assert.Equal("Diésel", d.GetProperty("combustible").GetString());
        Assert.Equal(4500.5m, d.GetProperty("capacidadCargaKg").GetDecimal());
        Assert.Equal(7500m, d.GetProperty("pesoBrutoKg").GetDecimal());
        Assert.Equal(5193, d.GetProperty("cilindrajeCc").GetInt32());
        Assert.Equal("/media/pub/v1/frente.jpg", d.GetProperty("fotoPortada").GetString());
        var fotos = d.GetProperty("fotos").EnumerateArray().ToArray();
        Assert.Equal(3, fotos.Length);
        Assert.All(fotos, f => Assert.Equal(new[] { "esPortada", "orden", "url" }, Claves(f)));
        Assert.Equal(new[] { 1, 2, 3 }, fotos.Select(f => f.GetProperty("orden").GetInt32()).ToArray());
        Assert.Equal(new[] { false, true, false }, fotos.Select(f => f.GetProperty("esPortada").GetBoolean()).ToArray());
        Assert.Equal(new[] { "/media/pub/v1/lateral.jpg", "/media/pub/v1/frente.jpg", "/media/pub/v1/cabina.jpg" },
            fotos.Select(f => f.GetProperty("url").GetString()).ToArray());
    }

    [Fact(DisplayName = "CA-DET-02, CA-D11-01: detalle del reservado V2 -> RESERVADO, negociable; fotos sin portada ordenadas")]
    public async Task Detalle_reservado()
    {
        var d = await AyudasP2.Ok(await Ref.Detalle("kenworth-t800-2018"));
        Assert.Equal("RESERVADO", d.GetProperty("estadoComercial").GetString());
        Assert.True(d.GetProperty("negociable").GetBoolean());
        Assert.Equal("/media/pub/v2/a.jpg", d.GetProperty("fotoPortada").GetString());
        var fotos = d.GetProperty("fotos").EnumerateArray().ToArray();
        Assert.Equal(new[] { 3, 5 }, fotos.Select(f => f.GetProperty("orden").GetInt32()).ToArray());
        Assert.All(fotos, f => Assert.False(f.GetProperty("esPortada").GetBoolean()));
    }

    [Fact(DisplayName = "CA-DET-03, CA-DET-09: detalle sin ficha ni fotos -> campos null, fotos [] y fotoPortada null")]
    public async Task Detalle_sin_datos_opcionales()
    {
        var d = await AyudasP2.Ok(await Ref.Detalle("renault-logan-2021"));
        foreach (var c in new[] { "descripcionComercial", "descripcionTecnica", "carroceria", "combustible", "capacidadCargaKg",
                     "pesoBrutoKg", "cilindrajeCc", "fotoPortada" })
            Assert.Equal(JsonValueKind.Null, d.GetProperty(c).ValueKind);
        Assert.Empty(d.GetProperty("fotos").EnumerateArray());
    }

    [Fact(DisplayName = "CA-001-04, CA-003-03: V4 -> URL /media/ codificada por segmento; destacada vencida -> destacado false; linea null")]
    public async Task Detalle_url_codificada()
    {
        var d = await AyudasP2.Ok(await Ref.Detalle("chevrolet-camion-2015"));
        Assert.Equal("/media/pub%201/foto%20%C3%B1.jpg", d.GetProperty("fotoPortada").GetString());
        Assert.Equal("/media/pub%201/foto%20%C3%B1.jpg", d.GetProperty("fotos")[0].GetProperty("url").GetString());
        Assert.True(d.GetProperty("fotos")[0].GetProperty("esPortada").GetBoolean() == false);
        Assert.False(d.GetProperty("destacado").GetBoolean());
        Assert.Equal(JsonValueKind.Null, d.GetProperty("linea").ValueKind);
    }

    [Fact(DisplayName = "CA-DET-05: slug inexistente -> 404 { error: 'Este vehículo no está disponible.' }")]
    public async Task Slug_inexistente()
    {
        AfirmarCuerpo404(await Texto404(await Ref.Detalle("no-existe")));
    }

    public static TheoryData<string> Casos()
    {
        var d = new TheoryData<string>();
        foreach (var c in Siembra.CasosNoVisibles) d.Add(c);
        return d;
    }

    [Theory(DisplayName = "CA-DET-06: cada no visible -> 404 con el cuerpo idéntico byte a byte al del inexistente")]
    [MemberData(nameof(Casos))]
    public async Task No_visible_404_identico(string slug)
    {
        var inexistente = await (await Ref.Detalle("no-existe")).Content.ReadAsByteArrayAsync();
        var r = await Ref.Detalle(slug);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal(inexistente, await r.Content.ReadAsByteArrayAsync());
        Assert.Equal(r.Content.Headers.ContentType?.ToString(), (await Ref.Detalle("no-existe")).Content.Headers.ContentType?.ToString());
    }

    [Fact(DisplayName = "CA-DET-07: slug visible de 200 caracteres -> 200; de 201 -> 404 aunque exista en la base")]
    public async Task Limite_del_slug()
    {
        var s200 = "s" + new string('a', 199);
        var s201 = "s" + new string('b', 200);
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s =>
        {
            s.Pub(s200);
            s.Pub(s201); // SQLite no impone el largo: existe y es visible, pero el endpoint corta en 200
        });
        Assert.Equal(s200, (await AyudasP2.Ok(await api.Detalle(s200))).GetProperty("slug").GetString());
        AfirmarCuerpo404(await Texto404(await api.Detalle(s201)));
    }

    [Theory(DisplayName = "CA-DET-08: slugs raros -> 404 con el cuerpo común; nunca 500 ni otra ruta")]
    // "%00" no se prueba: TestHost lanza la excepción al construir la petición (antes de llegar a la API);
    // en ejecución real Kestrel rechaza esa ruta con 400.
    [InlineData("..%2F..%2Fetc")]
    [InlineData("a%20b")]
    [InlineData("cami%C3%B3n")]
    [InlineData("'%20OR%201=1")]
    [InlineData("chevrolet-npr-2020%3Cscript%3E")]
    [InlineData("CHEVROLET-NPR-2020")]
    [InlineData("chevrolet-npr-2020%20")]
    public async Task Slugs_raros(string crudo)
    {
        var r = await Ref.Get("/api/catalogo/publicaciones/" + crudo);
        var texto = await r.Content.ReadAsStringAsync();
        if (crudo == "CHEVROLET-NPR-2020")
        {
            // C-P3-6 c: en SQLite el slug distingue mayúsculas; en MariaDB (utf8mb4_general_ci) podría dar 200.
            Assert.True(r.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.OK, $"llegó {(int)r.StatusCode}");
            return;
        }
        Assert.True(r.StatusCode == HttpStatusCode.NotFound, $"{crudo}: llegó {(int)r.StatusCode}: {texto}");
        AfirmarCuerpo404(texto);
        Assert.Equal(await (await Ref.Detalle("no-existe")).Content.ReadAsStringAsync(), texto);
    }
}
