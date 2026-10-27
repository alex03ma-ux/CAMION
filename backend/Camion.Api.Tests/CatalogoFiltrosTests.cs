using System.Net;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.CatalogoJson;
using static Camion.Api.Tests.Infraestructura.CatalogoApi;

namespace Camion.Api.Tests;

/// <summary>P3: filtros de /publicaciones (HU-005 a HU-011, D-17) y GET /api/catalogo/filtros.</summary>
public sealed class CatalogoFiltrosTests(CatalogoReferencia referencia) : IClassFixture<CatalogoReferencia>
{
    private CatalogoApi Ref => referencia.Api;
    private Siembra D => Ref.Datos;

    private const string V1 = "chevrolet-npr-2020", V2 = "kenworth-t800-2018", V3 = "renault-logan-2021", V4 = "chevrolet-camion-2015";

    private async Task Ver(string query, params string[] esperados)
    {
        var b = await Ref.Buscar(query);
        Assert.Equal(esperados, Slugs(b.GetProperty("items")));
        Assert.Equal(esperados.Length, b.GetProperty("total").GetInt32());
    }

    // ---------- categoría, marca, modelo ----------

    [Fact(DisplayName = "CA-005-01, CA-006-01, CA-005-02: tipoId Carro -> V3; Camión -> V1, V2, V4; inexistente -> 200 vacío")]
    public async Task Filtro_por_tipo()
    {
        await Ver($"?tipoId={D.Carro.Id}", V3);
        await Ver($"?tipoId={D.Camion.Id}", V1, V2, V4);
        await Ver("?tipoId=999999");
        await Ver($"?tipoId={D.Maquinaria.Id}");
    }

    [Fact(DisplayName = "CA-007-01, CA-007-02: marcaId Chevrolet -> V1, V4 (sin no visibles); Hino (solo vendida) e inexistente -> 200 vacío")]
    public async Task Filtro_por_marca()
    {
        await Ver($"?marcaId={D.Chevrolet.Id}", V1, V4);
        await Ver($"?marcaId={D.Hino.Id}");
        await Ver("?marcaId=999999");
    }

    [Fact(DisplayName = "CA-008-01/02/05: lineaId NPR -> V1; Chevrolet+NPR -> V1; NHR -> vacío (V4 sin línea no sale con ninguna línea)")]
    public async Task Filtro_por_linea()
    {
        await Ver($"?lineaId={D.Npr.Id}", V1);
        await Ver($"?marcaId={D.Chevrolet.Id}&lineaId={D.Npr.Id}", V1);
        await Ver($"?lineaId={D.Nhr.Id}");
        await Ver($"?marcaId={D.Chevrolet.Id}&lineaId={D.Nhr.Id}");
        await Ver("?lineaId=999999"); // CA-008-04 sin marca: 200 vacío
    }

    [Fact(DisplayName = "CA-008-03, CA-008-04 (C-P3-4): lineaId de otra marca o inexistente con marcaId -> 400 'El modelo no corresponde a la marca elegida.'")]
    public async Task Linea_que_no_es_de_la_marca()
    {
        await Afirmar(Ref, $"?marcaId={D.Kenworth.Id}&lineaId={D.Npr.Id}");
        await Afirmar(Ref, $"?marcaId={D.Chevrolet.Id}&lineaId=999999");
        await Afirmar(Ref, $"?marcaId=999999&lineaId={D.Npr.Id}");
        static Task Afirmar(CatalogoApi api, string q) => api.Afirmar400(q, "lineaId", MsgLineaMarca);
    }

    [Theory(DisplayName = "CA-004-12 y aclaración de ids no numéricos: tipoId/marcaId/lineaId no numéricos -> 400 con su mensaje")]
    [InlineData("tipoId", "abc", MsgTipoId)]
    [InlineData("tipoId", "-1", MsgTipoId)]
    [InlineData("tipoId", "99999999999999999999", MsgTipoId)]
    [InlineData("marcaId", "1e3", MsgMarcaId)]
    [InlineData("marcaId", "1.0", MsgMarcaId)]
    [InlineData("lineaId", "x", MsgLineaId)]
    [InlineData("lineaId", "0x10", MsgLineaId)]
    public async Task Ids_no_numericos(string parametro, string valor, string mensaje) =>
        await Ref.Afirmar400($"?{parametro}={Uri.EscapeDataString(valor)}", parametro, mensaje);

    [Fact(DisplayName = "CA-004-12: marcaId no numérico junto con lineaId válido -> 400 de marcaId, sin 500")]
    public async Task Marca_no_numerica_con_linea() =>
        await Ref.Afirmar400($"?marcaId=abc&lineaId={D.Npr.Id}", "marcaId", MsgMarcaId);

    // ---------- presupuesto, año, km ----------

    [Fact(DisplayName = "CA-009-01/02/04/05: precio con límites inclusivos, solo mínimo, solo máximo, iguales y enorme")]
    public async Task Filtro_por_precio()
    {
        await Ver("?precioMin=80000000&precioMax=120000000", V1, V4);
        await Ver("?precioMin=100000000", V1, V2);
        await Ver("?precioMax=50000000", V3);
        await Ver("?precioMin=0", V1, V2, V3, V4);
        await Ver("?precioMin=120000000&precioMax=120000000", V1);
        await Ver("?precioMax=99999999999999", V1, V2, V3, V4);
        await Ver("?precioMin=120000001&precioMax=249999999");
    }

    [Theory(DisplayName = "CA-009-03: precio no válido -> 400 con el mensaje de precio")]
    [InlineData("precioMin", "-1")]
    [InlineData("precioMin", "abc")]
    [InlineData("precioMax", "1.5")]
    [InlineData("precioMax", "120.000.000")]
    [InlineData("precioMin", "10.000.000")]
    [InlineData("precioMax", "99999999999999999999")]
    public async Task Precio_no_valido(string parametro, string valor) =>
        await Ref.Afirmar400($"?{parametro}={valor}", parametro, MsgPrecio);

    [Fact(DisplayName = "CA-009-04: precioMax < precioMin -> 400 'El precio máximo debe ser mayor o igual al mínimo.'")]
    public async Task Precio_rango_invertido() =>
        await Ref.Afirmar400("?precioMin=200000000&precioMax=100000000", "precioMax", MsgPrecioRango);

    [Fact(DisplayName = "CA-010-01/02: año con límites inclusivos; 1900-2200 -> 200")]
    public async Task Filtro_por_anio()
    {
        await Ver("?anioMin=2018&anioMax=2020", V1, V2);
        await Ver("?anioMin=2020", V1, V3);
        await Ver("?anioMax=2015", V4);
        await Ver("?anioMin=1900&anioMax=2200", V1, V2, V3, V4);
    }

    [Theory(DisplayName = "CA-010-02: año fuera de 1900-2200 o no numérico -> 400 'Escribe un año entre 1900 y 2200.'")]
    [InlineData("anioMin", "1899")]
    [InlineData("anioMax", "2201")]
    [InlineData("anioMin", "abc")]
    [InlineData("anioMax", "2020.5")]
    [InlineData("anioMin", "-2020")]
    public async Task Anio_no_valido(string parametro, string valor) =>
        await Ref.Afirmar400($"?{parametro}={valor}", parametro, MsgAnio);

    [Fact(DisplayName = "CA-010-03: anioMax < anioMin -> 400 'El año final debe ser mayor o igual al inicial.'")]
    public async Task Anio_rango_invertido() =>
        await Ref.Afirmar400("?anioMin=2021&anioMax=2018", "anioMax", MsgAnioRango);

    [Fact(DisplayName = "CA-011-01: kmMax inclusivo; 0 -> vacío; enorme -> todo")]
    public async Task Filtro_por_km()
    {
        await Ver("?kmMax=85000", V1, V3);
        await Ver("?kmMax=84999", V3);
        await Ver("?kmMax=0");
        await Ver("?kmMax=99999999999", V1, V2, V3, V4);
    }

    [Theory(DisplayName = "CA-011-02: kmMax no válido -> 400 con el mensaje de kilometraje")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("85.000")]
    [InlineData("85k")]
    public async Task Km_no_valido(string valor) => await Ref.Afirmar400($"?kmMax={valor}", "kmMax", MsgKm);

    [Fact(DisplayName = "CA-004-15: los filtros se combinan con 'y'")]
    public async Task Filtros_combinados()
    {
        await Ver($"?tipoId={D.Camion.Id}&marcaId={D.Chevrolet.Id}&precioMax=100000000", V4);
        await Ver($"?q=chevrolet&anioMin=2016", V1);
        await Ver($"?tipoId={D.Camion.Id}&kmMax=300000&orden=precio_asc", V1, V2);
        await Ver($"?marcaId={D.Chevrolet.Id}&lineaId={D.Npr.Id}&precioMin=1&precioMax=200000000&anioMin=2020&anioMax=2020&kmMax=85000&q=npr", V1);
        await Ver($"?tipoId={D.Carro.Id}&marcaId={D.Chevrolet.Id}");
    }

    // ---------- /filtros ----------

    [Fact(DisplayName = "CA-FIL-01, CA-FIL-02, CA-FIL-04, CA-FIL-06: /filtros con la referencia (solo visibles, orden alfabético, totales, rangos)")]
    public async Task Filtros_referencia()
    {
        var f = await Ref.Filtros();
        Assert.Equal(new[] { "marcas", "rangos", "tipos" }, Claves(f));
        Assert.Equal(
            "Camión=3,Carro=1|Chevrolet=2[NPR=1],Kenworth=1[T800=1],Renault=1[Logan=1]|45000000,250000000,2015,2021,500000",
            Resumen(f));
        // Los ids son los del catálogo.
        Assert.Equal(new[] { D.Camion.Id, D.Carro.Id }, f.GetProperty("tipos").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray());
        Assert.Equal(new[] { D.Chevrolet.Id, D.Kenworth.Id, D.Renault.Id },
            f.GetProperty("marcas").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray());
        Assert.Equal(D.Npr.Id, f.GetProperty("marcas")[0].GetProperty("lineas")[0].GetProperty("id").GetInt64());
        Assert.All(f.GetProperty("tipos").EnumerateArray(), t => Assert.Equal(new[] { "id", "nombre", "total" }, Claves(t)));
        Assert.All(f.GetProperty("marcas").EnumerateArray(), m => Assert.Equal(new[] { "id", "lineas", "nombre", "total" }, Claves(m)));
        Assert.Equal(new[] { "anioMax", "anioMin", "kmMax", "precioMax", "precioMin" }, Claves(f.GetProperty("rangos")));
    }

    [Fact(DisplayName = "CA-FIL-07: el total de cada tipo, marca y línea de /filtros coincide con /publicaciones")]
    public async Task Filtros_coinciden_con_busqueda()
    {
        var f = await Ref.Filtros();
        foreach (var t in f.GetProperty("tipos").EnumerateArray())
            Assert.Equal(t.GetProperty("total").GetInt32(), (await Ref.Buscar($"?tipoId={t.GetProperty("id").GetInt64()}")).GetProperty("total").GetInt32());
        foreach (var m in f.GetProperty("marcas").EnumerateArray())
        {
            Assert.Equal(m.GetProperty("total").GetInt32(), (await Ref.Buscar($"?marcaId={m.GetProperty("id").GetInt64()}")).GetProperty("total").GetInt32());
            foreach (var l in m.GetProperty("lineas").EnumerateArray())
                Assert.Equal(l.GetProperty("total").GetInt32(), (await Ref.Buscar($"?lineaId={l.GetProperty("id").GetInt64()}")).GetProperty("total").GetInt32());
        }
    }

    [Fact(DisplayName = "CA-FIL-03: marca, línea y tipo con activo = 0 no aparecen en /filtros aunque tengan publicaciones visibles")]
    public async Task Filtros_solo_entradas_activas()
    {
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s =>
        {
            s.Pub("n12-dina", marca: s.Dina, linea: s.DinaL, precio: 70_000_000m);
            s.Pub("remolque-inactivo", tipo: s.Remolque, marca: s.Kenworth, linea: s.T800, precio: 60_000_000m);
            s.Nhr.Activo = false;
            s.Pub("nhr-inactiva", marca: s.Chevrolet, linea: s.Nhr, precio: 80_000_000m);
        });
        var f = await api.Filtros();
        Assert.Equal("Camión=2|Chevrolet=1[],Kenworth=1[T800=1]|60000000,80000000,2020,2020,50000", Resumen(f));
        // Hueco C-P3-6 g (sin decidir): se documenta lo que hace hoy la búsqueda con esas publicaciones.
        Assert.Equal(3, (await api.Buscar()).GetProperty("total").GetInt32());
    }

    [Fact(DisplayName = "CA-FIL-04: orden alfabético en los tres niveles (Camión < Carro < Carrocería < Maquinaria; NHR < NPR)")]
    public async Task Filtros_orden_alfabetico()
    {
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s =>
        {
            s.Pub("a", tipo: s.Maquinaria, marca: s.Caterpillar, km: 0);
            s.Pub("b", tipo: s.Carro, marca: s.Renault, linea: s.Logan);
            s.Pub("c", tipo: s.Carroceria, marca: s.Andinas, km: 0);
            s.Pub("d", marca: s.Chevrolet, linea: s.Npr);
            s.Pub("e", marca: s.Chevrolet, linea: s.Nhr);
            s.Pub("f", marca: s.Kenworth, linea: s.T800);
            s.Pub("g", tipo: s.Maquinaria, marca: s.Bobcat, km: 1200);
        });
        Assert.Equal(
            "Camión=3,Carro=1,Carrocería=1,Maquinaria=2|Bobcat=1[],Carrocerías Andinas=1[],Caterpillar=1[],Chevrolet=2[NHR=1,NPR=1],Kenworth=1[T800=1],Renault=1[Logan=1]|100000000,100000000,2020,2020,50000",
            Resumen(await api.Filtros()));
    }

    [Fact(DisplayName = "Aclaración de redondeo: precioCop se redondea; rangos.precioMin hacia abajo y precioMax hacia arriba")]
    public async Task Redondeo_de_precios()
    {
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s =>
        {
            s.Pub("con-centavos-bajo", precio: 45_000_000.75m, publicadaEn: T.AddHours(-1));
            s.Pub("con-centavos-alto", precio: 250_000_000.25m, publicadaEn: T.AddHours(-2));
            s.Pub("medio", precio: 100_000_000.50m, publicadaEn: T.AddHours(-3));
        });
        var items = (await api.Buscar()).GetProperty("items");
        Assert.Equal(new[] { "45000001", "250000000", "100000001" }, items.EnumerateArray().Select(x => x.GetProperty("precioCop").GetRawText()).ToArray());
        var r = (await api.Filtros()).GetProperty("rangos");
        Assert.Equal(45000000, r.GetProperty("precioMin").GetInt64());
        Assert.Equal(250000001, r.GetProperty("precioMax").GetInt64());
        // El filtro compara el DECIMAL: 45.000.000,75 no entra con precioMax=45000000.
        Assert.Empty(await api.SlugsBusqueda("?precioMax=45000000"));
    }

    // ---------- D-17 ----------

    [Fact(DisplayName = "CA-D17-01/02/03/04/09, C-P3-6 q: Carrocería y Maquinaria se filtran, cuentan y muestran como cualquier artículo; km 0 llega en la API")]
    public async Task Categorias_D17()
    {
        using var api = await CatalogoApi.Crear(extra: s =>
        {
            var c1 = s.Pub("carroceria-furgon-aluminio", tipo: s.Carroceria, marca: s.Andinas, anio: 2023, km: 0,
                precio: 35_000_000m, publicadaEn: T.AddHours(-1));
            c1.Vehiculo!.Carroceria = s.Furgon;
            s.Pub("retroexcavadora-2019", tipo: s.Maquinaria, marca: s.Caterpillar, anio: 2019, km: 0, precio: 180_000_000m,
                publicadaEn: T.AddHours(-2));
            s.Pub("minicargador-2017", tipo: s.Maquinaria, marca: s.Bobcat, anio: 2017, km: 1200, precio: 95_000_000m,
                publicadaEn: T.AddHours(-3));
        });
        var d = api.Datos;
        Assert.Equal(new[] { "carroceria-furgon-aluminio" }, await api.SlugsBusqueda($"?tipoId={d.Carroceria.Id}"));
        Assert.Equal(new[] { "retroexcavadora-2019", "minicargador-2017" }, await api.SlugsBusqueda($"?tipoId={d.Maquinaria.Id}"));
        Assert.Equal(
            "Camión=3,Carro=1,Carrocería=1,Maquinaria=2|Bobcat=1[],Carrocerías Andinas=1[],Caterpillar=1[],Chevrolet=2[NPR=1],Kenworth=1[T800=1],Renault=1[Logan=1]|35000000,250000000,2015,2023,500000",
            Resumen(await api.Filtros()));
        var recientes = Slugs((await api.Inicio()).GetProperty("recientes"));
        Assert.Contains("carroceria-furgon-aluminio", recientes);
        Assert.Contains("retroexcavadora-2019", recientes);
        Assert.Contains("minicargador-2017", recientes);

        var c1d = await AyudasP2.Ok(await api.Detalle("carroceria-furgon-aluminio"));
        Assert.Equal("Carrocería", c1d.GetProperty("tipo").GetString());
        Assert.Equal("Carrocerías Andinas", c1d.GetProperty("marca").GetString());
        Assert.Equal("Furgón", c1d.GetProperty("carroceria").GetString());
        Assert.Equal(0, c1d.GetProperty("kilometraje").GetInt32());
        var m1 = await AyudasP2.Ok(await api.Detalle("retroexcavadora-2019"));
        Assert.Equal(0, m1.GetProperty("kilometraje").GetInt32());
        // C-P3-6 q aceptado: kmMax=0 y km_asc incluyen carrocerías y máquinas con 0 km.
        Assert.Equal(new[] { "carroceria-furgon-aluminio", "retroexcavadora-2019" }, await api.SlugsBusqueda("?kmMax=0"));
        Assert.Equal(new[] { "carroceria-furgon-aluminio", "retroexcavadora-2019" }, (await api.SlugsBusqueda("?orden=km_asc")).Skip(1).Take(2).ToArray());
    }
}
