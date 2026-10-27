using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.CatalogoJson;
using static Camion.Api.Tests.Infraestructura.CatalogoApi;

namespace Camion.Api.Tests;

/// <summary>P3: GET /api/catalogo/inicio y GET /api/catalogo/publicaciones (HU-001 a HU-004, HU-012, RN-VE-011/012).</summary>
public sealed class CatalogoBusquedaTests(CatalogoReferencia referencia) : IClassFixture<CatalogoReferencia>
{
    private CatalogoApi Ref => referencia.Api;

    private static readonly string[] ClavesTarjeta =
    [
        "departamento", "destacado", "estadoComercial", "fotoPortada", "kilometraje", "linea", "marca", "modeloAnio",
        "municipio", "negociable", "precioCop", "publicadaEn", "slug", "tipo", "titulo"
    ];

    private static JsonElement Tarjeta(JsonElement lista, string slug) =>
        lista.EnumerateArray().Single(x => x.GetProperty("slug").GetString() == slug);

    // ---------- /inicio ----------

    [Fact(DisplayName = "CA-001-01, CA-002-02, CA-003-01, CA-003-03: /inicio con los datos de referencia")]
    public async Task Inicio_referencia()
    {
        var inicio = await Ref.Inicio();
        Assert.Equal(new[] { "destacados", "recientes" }, Claves(inicio));
        Assert.Equal(new[] { "chevrolet-npr-2020" }, Slugs(inicio.GetProperty("destacados")));
        var recientes = inicio.GetProperty("recientes");
        Assert.Equal(new[] { "chevrolet-npr-2020", "kenworth-t800-2018", "renault-logan-2021", "chevrolet-camion-2015" }, Slugs(recientes));
        Assert.Equal(new[] { true, false, false, false }, recientes.EnumerateArray().Select(x => x.GetProperty("destacado").GetBoolean()).ToArray());
        Assert.True(inicio.GetProperty("destacados")[0].GetProperty("destacado").GetBoolean());
    }

    [Fact(DisplayName = "CA-001-03, CA-001-05: cada tarjeta tiene exactamente las 15 claves; publicadaEn en ISO UTC; precioCop entero; V4 linea null")]
    public async Task Forma_de_la_tarjeta()
    {
        var inicio = await Ref.Inicio();
        var busqueda = await Ref.Buscar();
        foreach (var t in inicio.GetProperty("recientes").EnumerateArray().Concat(busqueda.GetProperty("items").EnumerateArray()))
        {
            Assert.Equal(ClavesTarjeta, Claves(t));
            var fecha = t.GetProperty("publicadaEn").GetString()!;
            Assert.True(fecha.EndsWith("Z") || fecha.EndsWith("+00:00"), fecha);
            Assert.Equal(JsonValueKind.Number, t.GetProperty("precioCop").ValueKind);
            Assert.DoesNotContain(".", t.GetProperty("precioCop").GetRawText());
        }
        var recientes = inicio.GetProperty("recientes");
        var v1 = Tarjeta(recientes, "chevrolet-npr-2020");
        Assert.Equal(120000000, v1.GetProperty("precioCop").GetInt64());
        Assert.Equal("120000000", v1.GetProperty("precioCop").GetRawText());
        Assert.Equal(T.AddDays(-1), v1.GetProperty("publicadaEn").GetDateTime().ToUniversalTime());
        Assert.Equal("Camión", v1.GetProperty("tipo").GetString());
        Assert.Equal("Chevrolet", v1.GetProperty("marca").GetString());
        Assert.Equal("NPR", v1.GetProperty("linea").GetString());
        Assert.Equal(2020, v1.GetProperty("modeloAnio").GetInt32());
        Assert.Equal(85000, v1.GetProperty("kilometraje").GetInt32());
        Assert.Equal("Medellín", v1.GetProperty("municipio").GetString());
        Assert.Equal("Antioquia", v1.GetProperty("departamento").GetString());
        Assert.Equal("Chevrolet NPR 2020 furgón", v1.GetProperty("titulo").GetString());
        Assert.False(v1.GetProperty("negociable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, Tarjeta(recientes, "chevrolet-camion-2015").GetProperty("linea").ValueKind);
        Assert.True(Tarjeta(recientes, "kenworth-t800-2018").GetProperty("negociable").GetBoolean());
    }

    [Fact(DisplayName = "CA-001-04: fotoPortada = es_portada; si ninguna, la de menor orden; sin fotos null; URL /media/ codificada por segmento")]
    public async Task Foto_de_portada()
    {
        var recientes = (await Ref.Inicio()).GetProperty("recientes");
        Assert.Equal("/media/pub/v1/frente.jpg", Tarjeta(recientes, "chevrolet-npr-2020").GetProperty("fotoPortada").GetString());
        Assert.Equal("/media/pub/v2/a.jpg", Tarjeta(recientes, "kenworth-t800-2018").GetProperty("fotoPortada").GetString());
        Assert.Equal(JsonValueKind.Null, Tarjeta(recientes, "renault-logan-2021").GetProperty("fotoPortada").ValueKind);
        Assert.Equal("/media/pub%201/foto%20%C3%B1.jpg", Tarjeta(recientes, "chevrolet-camion-2015").GetProperty("fotoPortada").GetString());
        // La misma portada en la búsqueda.
        var items = (await Ref.Buscar()).GetProperty("items");
        Assert.Equal("/media/pub/v1/frente.jpg", Tarjeta(items, "chevrolet-npr-2020").GetProperty("fotoPortada").GetString());
    }

    [Fact(DisplayName = "CA-001-02, CA-FIL-05: base sin publicaciones o solo con no visibles -> 200 con listas vacías")]
    public async Task Inicio_y_busqueda_vacios()
    {
        foreach (var noVisibles in new[] { false, true })
        {
            using var api = await CatalogoApi.Crear(visibles: false, noVisibles: noVisibles);
            var inicio = await api.Inicio();
            Assert.Empty(inicio.GetProperty("destacados").EnumerateArray());
            Assert.Empty(inicio.GetProperty("recientes").EnumerateArray());
            var b = await api.Buscar();
            Assert.Empty(b.GetProperty("items").EnumerateArray());
            Assert.Equal(0, b.GetProperty("total").GetInt32());
            var f = await api.Filtros();
            Assert.Equal("||null,null,null,null,null", Resumen(f));
            foreach (var n in new[] { "precioMin", "precioMax", "anioMin", "anioMax", "kmMax" })
                Assert.Equal(JsonValueKind.Null, f.GetProperty("rangos").GetProperty(n).ValueKind);
        }
    }

    [Fact(DisplayName = "CA-002-01, CA-003-02: límites de /inicio (12 recientes y 8 destacados, por publicadaEn descendente)")]
    public async Task Limites_del_inicio()
    {
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s =>
        {
            for (var i = 1; i <= 15; i++)
            {
                var p = s.Pub($"p{i:00}", publicadaEn: T.AddHours(-i));
                if (i >= 6) s.Destacar(p, T.AddDays(-1), T.AddDays(1)); // 10 destacadas: p06 a p15
            }
        });
        var inicio = await api.Inicio();
        Assert.Equal(Enumerable.Range(1, 12).Select(i => $"p{i:00}").ToArray(), Slugs(inicio.GetProperty("recientes")));
        Assert.Equal(Enumerable.Range(6, 8).Select(i => $"p{i:00}").ToArray(), Slugs(inicio.GetProperty("destacados")));
        Assert.All(inicio.GetProperty("destacados").EnumerateArray(), x => Assert.True(x.GetProperty("destacado").GetBoolean()));
    }

    // ---------- destacados ----------

    [Fact(DisplayName = "CA-003-03/04/05/07: destacado solo con fila ACTIVA e inicio <= T < fin; varias filas no duplican")]
    public async Task Limites_del_destacado()
    {
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s =>
        {
            s.Destacar(s.Pub("dest-vigente", publicadaEn: T.AddHours(-1)), T.AddDays(-1), T.AddDays(1));
            s.Destacar(s.Pub("dest-fin-exacto", publicadaEn: T.AddHours(-2)), T.AddDays(-2), T);
            s.Destacar(s.Pub("dest-futuro", publicadaEn: T.AddHours(-3)), T.AddHours(1), T.AddDays(2));
            s.Destacar(s.Pub("dest-inicio-exacto", publicadaEn: T.AddHours(-4)), T, T.AddDays(1));
            s.Destacar(s.Pub("dest-cancelado", publicadaEn: T.AddHours(-5)), T.AddDays(-1), T.AddDays(1), "CANCELADA");
            var doble = s.Pub("dest-doble", publicadaEn: T.AddHours(-6));
            s.Destacar(doble, T.AddDays(-10), T.AddDays(-5));
            s.Destacar(doble, T.AddDays(-1), T.AddDays(1));
            s.Destacar(doble, T.AddDays(-2), T.AddDays(2));
        });
        var esperados = new[] { "dest-vigente", "dest-inicio-exacto", "dest-doble" };
        var inicio = await api.Inicio();
        Assert.Equal(esperados, Slugs(inicio.GetProperty("destacados")));
        Assert.Equal(6, inicio.GetProperty("recientes").GetArrayLength());

        var b = await api.Buscar();
        Assert.Equal(6, b.GetProperty("total").GetInt32());
        var items = b.GetProperty("items");
        Assert.Equal(esperados.Concat(new[] { "dest-fin-exacto", "dest-futuro", "dest-cancelado" }).ToArray(), Slugs(items));
        foreach (var t in items.EnumerateArray())
            Assert.Equal(esperados.Contains(t.GetProperty("slug").GetString()), t.GetProperty("destacado").GetBoolean());
        foreach (var slug in new[] { "dest-fin-exacto", "dest-futuro", "dest-cancelado" })
            Assert.False((await AyudasP2.Ok(await api.Detalle(slug))).GetProperty("destacado").GetBoolean());
        Assert.True((await AyudasP2.Ok(await api.Detalle("dest-doble"))).GetProperty("destacado").GetBoolean());
        Assert.Equal("Camión=6|Chevrolet=6[NPR=6]|100000000,100000000,2020,2020,50000", Resumen(await api.Filtros()));
    }

    // ---------- /publicaciones: básico y q ----------

    [Fact(DisplayName = "CA-004-01, CA-012-01: sin parámetros -> 200 {items,total:4,pagina:1,tamano:12}, destacado primero")]
    public async Task Busqueda_sin_parametros()
    {
        var b = await Ref.Buscar();
        Assert.Equal(new[] { "items", "pagina", "tamano", "total" }, Claves(b));
        Assert.Equal(4, b.GetProperty("total").GetInt32());
        Assert.Equal(1, b.GetProperty("pagina").GetInt32());
        Assert.Equal(12, b.GetProperty("tamano").GetInt32());
        Assert.Equal(new[] { "chevrolet-npr-2020", "kenworth-t800-2018", "renault-logan-2021", "chevrolet-camion-2015" }, Slugs(b.GetProperty("items")));
    }

    [Fact(DisplayName = "CA-012-01, CA-004-05, CA-004-13: parámetros vacíos, q de solo espacios y parámetros desconocidos equivalen a sin parámetros")]
    public async Task Limpiar_filtros_equivale_a_sin_parametros()
    {
        var sin = await (await Ref.Get("/api/catalogo/publicaciones")).Content.ReadAsStringAsync();
        foreach (var q in new[] { "?q=", "?q=%20%20%20", "?color=rojo",
                     "?q=&tipoId=&marcaId=&lineaId=&precioMin=&precioMax=&anioMin=&anioMax=&kmMax=&orden=&pagina=&tamano=" })
        {
            var r = await Ref.Get("/api/catalogo/publicaciones" + q);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal(sin, await r.Content.ReadAsStringAsync());
        }
    }

    [Theory(DisplayName = "CA-004-02: q parcial, sin mayúsculas y recortado en título, marca y línea")]
    [InlineData("npr", "chevrolet-npr-2020")]
    [InlineData("NPR", "chevrolet-npr-2020")]
    [InlineData("nPr", "chevrolet-npr-2020")]
    [InlineData("chevrolet", "chevrolet-npr-2020,chevrolet-camion-2015")]
    [InlineData("CHEVROLET", "chevrolet-npr-2020,chevrolet-camion-2015")]
    [InlineData("  t800  ", "kenworth-t800-2018")]
    [InlineData("IMPECABLE", "renault-logan-2021")]
    [InlineData("ogan", "renault-logan-2021")]
    [InlineData("tractomula", "kenworth-t800-2018")]
    public async Task Busqueda_por_texto(string q, string esperados)
    {
        var slugs = await Ref.SlugsBusqueda("?q=" + Uri.EscapeDataString(q));
        Assert.Equal(esperados.Split(','), slugs);
    }

    [Fact(DisplayName = "CA-004-02: q busca en marca y línea aunque el título no las nombre")]
    public async Task Busqueda_en_marca_y_linea_no_en_titulo()
    {
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s =>
            s.Pub("sin-nombres", marca: s.Kenworth, linea: s.T800, titulo: "Tractocamión doble troque"));
        Assert.Equal(new[] { "sin-nombres" }, await api.SlugsBusqueda("?q=kenWORTH"));
        Assert.Equal(new[] { "sin-nombres" }, await api.SlugsBusqueda("?q=t80"));
        Assert.Equal(new[] { "sin-nombres" }, await api.SlugsBusqueda("?q=troque"));
        Assert.Empty(await api.SlugsBusqueda("?q=sin-nombres")); // el slug no es un campo de búsqueda
    }

    [Theory(DisplayName = "CA-004-04: caracteres especiales en q se buscan como texto literal (200 con 0 resultados, nunca 500)")]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("' OR 1=1 --")]
    [InlineData("<script>")]
    [InlineData("\"; DROP TABLE publicaciones; --")]
    public async Task Busqueda_con_caracteres_especiales(string q)
    {
        var b = await Ref.Buscar("?q=" + Uri.EscapeDataString(q));
        Assert.Equal(0, b.GetProperty("total").GetInt32());
        Assert.Empty(b.GetProperty("items").EnumerateArray());
    }

    [Fact(DisplayName = "CA-004-03: q de 100 caracteres (con 4 espacios alrededor) -> 200; de 101 -> 400 con su mensaje")]
    public async Task Limite_de_q()
    {
        var cien = new string('x', 100);
        var r = await Ref.Get("/api/catalogo/publicaciones?q=" + Uri.EscapeDataString("  " + cien + "  "));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        await Ref.Afirmar400("?q=" + new string('x', 101), "q", MsgBuscar);
        var r2 = await Ref.Get("/api/catalogo/publicaciones?q=" + new string('x', 101));
        Assert.Equal(new[] { "q" }, Claves((await Ayudas.Json(r2)).GetProperty("errores")));
    }

    // ---------- orden ----------

    [Fact(DisplayName = "CA-004-06: destacados primero con precio_asc y precio_desc")]
    public async Task Destacados_primero_con_orden_de_precio()
    {
        Assert.Equal(new[] { "chevrolet-npr-2020", "renault-logan-2021", "chevrolet-camion-2015", "kenworth-t800-2018" },
            await Ref.SlugsBusqueda("?orden=precio_asc"));
        Assert.Equal(new[] { "chevrolet-npr-2020", "kenworth-t800-2018", "chevrolet-camion-2015", "renault-logan-2021" },
            await Ref.SlugsBusqueda("?orden=precio_desc"));
        Assert.Equal(new[] { "chevrolet-npr-2020", "renault-logan-2021", "kenworth-t800-2018", "chevrolet-camion-2015" },
            await Ref.SlugsBusqueda("?orden=anio_desc"));
        Assert.Equal(new[] { "chevrolet-npr-2020", "renault-logan-2021", "kenworth-t800-2018", "chevrolet-camion-2015" },
            await Ref.SlugsBusqueda("?orden=km_asc"));
    }

    [Theory(DisplayName = "CA-004-07, CA-002-03: cada orden dentro de cada grupo, desempate publicadaEn desc y slug asc, estable entre llamadas")]
    [InlineData("recientes", "d-a,d-b,r-1,r-2,r-3,empate-a,empate-b")]
    [InlineData("", "d-a,d-b,r-1,r-2,r-3,empate-a,empate-b")]
    [InlineData("precio_asc", "d-b,d-a,r-1,empate-a,empate-b,r-3,r-2")]
    [InlineData("precio_desc", "d-a,d-b,r-2,r-3,empate-a,empate-b,r-1")]
    [InlineData("anio_desc", "d-b,d-a,r-3,r-2,empate-a,empate-b,r-1")]
    [InlineData("km_asc", "d-a,d-b,r-2,empate-a,empate-b,r-3,r-1")]
    public async Task Orden_con_desempates(string orden, string esperado)
    {
        using var api = await ApiOrden();
        var url = orden == "" ? "" : "?orden=" + orden;
        var primera = await api.SlugsBusqueda(url);
        Assert.Equal(esperado.Split(','), primera);
        Assert.Equal(primera, await api.SlugsBusqueda(url));
        // Paginado de a 2 da el mismo orden.
        var paginas = new List<string>();
        for (var p = 1; p <= 4; p++)
            paginas.AddRange(await api.SlugsBusqueda($"?tamano=2&pagina={p}" + (orden == "" ? "" : "&orden=" + orden)));
        Assert.Equal(primera, paginas);
    }

    [Fact(DisplayName = "CA-002-03: /inicio desempata por slug con el mismo publicadaEn y es estable")]
    public async Task Inicio_desempate_por_slug()
    {
        using var api = await ApiOrden();
        var a = Slugs((await api.Inicio()).GetProperty("recientes"));
        Assert.Equal(new[] { "r-1", "r-2", "r-3", "empate-a", "empate-b", "d-a", "d-b" }, a);
        Assert.Equal(a, Slugs((await api.Inicio()).GetProperty("recientes")));
        Assert.Equal(new[] { "d-a", "d-b" }, Slugs((await api.Inicio()).GetProperty("destacados")));
    }

    private static Task<CatalogoApi> ApiOrden() => CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s =>
    {
        // Insertadas en desorden; empate-b antes que empate-a para que el slug decida.
        s.Pub("empate-b", precio: 60_000_000m, anio: 2017, km: 80_000, publicadaEn: T.AddDays(-4));
        s.Pub("r-3", precio: 70_000_000m, anio: 2021, km: 150_000, publicadaEn: T.AddDays(-3));
        s.Destacar(s.Pub("d-b", precio: 100_000_000m, anio: 2022, km: 200_000, publicadaEn: T.AddDays(-6)), T.AddDays(-1), T.AddDays(1));
        s.Pub("r-1", precio: 50_000_000m, anio: 2015, km: 300_000, publicadaEn: T.AddDays(-1));
        s.Pub("empate-a", precio: 60_000_000m, anio: 2017, km: 80_000, publicadaEn: T.AddDays(-4));
        s.Destacar(s.Pub("d-a", precio: 300_000_000m, anio: 2010, km: 100_000, publicadaEn: T.AddDays(-5)), T.AddDays(-1), T.AddDays(1));
        s.Pub("r-2", precio: 90_000_000m, anio: 2019, km: 10_000, publicadaEn: T.AddDays(-2));
    });

    [Theory(DisplayName = "CA-004-08: orden no válido -> 400 'Orden no válido.'")]
    [InlineData("precio")]
    [InlineData("aleatorio")]
    [InlineData("precio_asc;DROP")]
    [InlineData("1")]
    public async Task Orden_no_valido(string orden) =>
        await Ref.Afirmar400("?orden=" + Uri.EscapeDataString(orden), "orden", MsgOrden);

    [Fact(DisplayName = "CA-004-08 (hueco C-P3-6 n, sin decidir): orden en mayúsculas nunca da 500 (hoy se acepta como precio_asc)")]
    public async Task Orden_en_mayusculas_no_da_500()
    {
        var r = await Ref.Get("/api/catalogo/publicaciones?orden=PRECIO_ASC");
        Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"llegó {(int)r.StatusCode}");
    }

    // ---------- paginación ----------

    [Fact(DisplayName = "CA-004-09, CA-004-14: paginación con 30 visibles; total cuenta todo el filtro; las páginas no repiten ni pierden")]
    public async Task Paginacion()
    {
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s =>
        {
            for (var i = 1; i <= 30; i++) s.Pub($"pag-{i:00}", publicadaEn: T.AddHours(-i));
        });
        var p3 = await api.Buscar("?tamano=12&pagina=3");
        Assert.Equal(6, p3.GetProperty("items").GetArrayLength());
        Assert.Equal(30, p3.GetProperty("total").GetInt32());
        Assert.Equal(3, p3.GetProperty("pagina").GetInt32());
        Assert.Equal(12, p3.GetProperty("tamano").GetInt32());

        var p4 = await api.Buscar("?tamano=12&pagina=4");
        Assert.Empty(p4.GetProperty("items").EnumerateArray());
        Assert.Equal(30, p4.GetProperty("total").GetInt32());
        Assert.Equal(4, p4.GetProperty("pagina").GetInt32());

        var todo = await api.Buscar("?tamano=48");
        Assert.Equal(30, todo.GetProperty("items").GetArrayLength());
        Assert.Equal(48, todo.GetProperty("tamano").GetInt32());

        var unidas = new List<string>();
        for (var p = 1; p <= 3; p++) unidas.AddRange(await api.SlugsBusqueda($"?tamano=12&pagina={p}"));
        Assert.Equal(Enumerable.Range(1, 30).Select(i => $"pag-{i:00}").ToArray(), unidas);

        var defecto = await api.Buscar();
        Assert.Equal(12, defecto.GetProperty("items").GetArrayLength());

        var enorme = await api.Buscar("?pagina=2147483647&tamano=48");
        Assert.Empty(enorme.GetProperty("items").EnumerateArray());
        Assert.Equal(30, enorme.GetProperty("total").GetInt32());

        // CA-004-14 con la referencia: chevrolet deja 2, tamano=1 -> 1 ítem y total 2.
        var uno = await Ref.Buscar("?q=chevrolet&tamano=1");
        Assert.Equal(1, uno.GetProperty("items").GetArrayLength());
        Assert.Equal(2, uno.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "chevrolet-camion-2015" }, await Ref.SlugsBusqueda("?q=chevrolet&tamano=1&pagina=2"));
    }

    [Theory(DisplayName = "CA-004-10: pagina/tamano no válidos -> 400 'Página o tamaño no válidos.' (nunca 500)")]
    [InlineData("pagina", "0")]
    [InlineData("pagina", "-1")]
    [InlineData("pagina", "abc")]
    [InlineData("pagina", "1.5")]
    [InlineData("pagina", "2147483648")]
    [InlineData("tamano", "0")]
    [InlineData("tamano", "49")]
    [InlineData("tamano", "1000")]
    [InlineData("tamano", "abc")]
    [InlineData("tamano", "-5")]
    public async Task Pagina_o_tamano_no_validos(string parametro, string valor) =>
        await Ref.Afirmar400($"?{parametro}={valor}", parametro, MsgPagina);

    [Fact(DisplayName = "CA-004-11: varios parámetros inválidos -> un solo 400 con todas las claves")]
    public async Task Varios_errores_a_la_vez()
    {
        var r = await Ref.Get("/api/catalogo/publicaciones?q=" + new string('q', 101) +
                              "&precioMin=-1&anioMin=1800&orden=x&kmMax=abc&tamano=49&tipoId=x&marcaId=y&lineaId=z&precioMax=1.5&anioMax=3000&pagina=0");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var json = await Ayudas.Json(r);
        Assert.Equal(MsgFiltros, json.GetProperty("error").GetString());
        Assert.Equal(new[] { "anioMax", "anioMin", "kmMax", "lineaId", "marcaId", "orden", "pagina", "precioMax", "precioMin", "q", "tamano", "tipoId" },
            Claves(json.GetProperty("errores")));
    }
}
