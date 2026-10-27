using System.Net;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.CatalogoJson;
using static Camion.Api.Tests.Infraestructura.CatalogoApi;

namespace Camion.Api.Tests;

/// <summary>
/// P3, regla de visibilidad única (RN-VE-013, RN-VE-014, D-11, D-17, condición 6 de C-P3-2), comprobada en los 4 endpoints.
/// SQLite en memoria y reloj fijo (T); nunca MariaDB.
/// </summary>
public sealed class CatalogoVisibilidadTests(CatalogoReferencia referencia) : IClassFixture<CatalogoReferencia>
{
    private CatalogoApi Ref => referencia.Api;

    // Filtros de una base que solo tiene V3 (Renault Logan, Carro).
    private const string FiltrosSoloV3 = "Carro=1|Renault=1[Logan=1]|45000000,45000000,2021,2021,40000";

    public static TheoryData<string> Casos()
    {
        var d = new TheoryData<string>();
        foreach (var c in Siembra.CasosNoVisibles) d.Add(c);
        return d;
    }

    private static async Task<string> Cuerpo404(CatalogoApi api, string slug)
    {
        var r = await api.Detalle(slug);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        return await r.Content.ReadAsStringAsync();
    }

    /// <summary>La publicación no aparece en /inicio, /publicaciones (sin filtro, con su marca y con su línea), ni en el detalle.</summary>
    private static async Task AfirmarAusente(CatalogoApi api, string slug, long marcaId, long? lineaId)
    {
        var inicio = await api.Inicio();
        Assert.DoesNotContain(slug, Slugs(inicio.GetProperty("destacados")));
        Assert.DoesNotContain(slug, Slugs(inicio.GetProperty("recientes")));
        Assert.DoesNotContain(slug, await api.SlugsBusqueda("?tamano=48"));
        Assert.DoesNotContain(slug, await api.SlugsBusqueda($"?tamano=48&marcaId={marcaId}"));
        if (lineaId is not null) Assert.Empty(await api.SlugsBusqueda($"?tamano=48&lineaId={lineaId}"));
        Assert.Empty(await api.SlugsBusqueda("?tamano=48&anioMax=1990"));
        Assert.Equal(await Cuerpo404(api, "no-existe"), await Cuerpo404(api, slug));
    }

    [Fact(DisplayName = "CA-VIS-01, CA-SE005-01: V1 visible aparece en los 4 endpoints")]
    public async Task V1_visible_en_los_4()
    {
        var inicio = await Ref.Inicio();
        Assert.Contains("chevrolet-npr-2020", Slugs(inicio.GetProperty("destacados")));
        Assert.Contains("chevrolet-npr-2020", Slugs(inicio.GetProperty("recientes")));
        Assert.Contains("chevrolet-npr-2020", await Ref.SlugsBusqueda());
        Assert.Equal(HttpStatusCode.OK, (await Ref.Detalle("chevrolet-npr-2020")).StatusCode);
        var filtros = await Ref.Filtros();
        Assert.Contains(filtros.GetProperty("marcas").EnumerateArray(),
            m => m.GetProperty("nombre").GetString() == "Chevrolet" && m.GetProperty("total").GetInt32() == 2);
    }

    [Theory(DisplayName = "CA-VIS-02 a 13, CA-VIS-15, CA-D11-03, CA-003-06, CA-DET-06: cada condición no cumplida oculta la publicación en los 4 endpoints (base aislada)")]
    [MemberData(nameof(Casos))]
    public async Task Cada_condicion_por_separado_en_los_4_endpoints(string caso)
    {
        // Base con solo V3 (visible) y el caso: así /filtros demuestra que el caso no suma en nada.
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s =>
        {
            s.Pub("renault-logan-2021", tipo: s.Carro, marca: s.Renault, linea: s.Logan, precio: 45_000_000m, anio: 2021, km: 40_000);
            s.CasoNoVisible(caso);
        });
        var d = api.Datos;
        var marca = caso == "n01-vendida" ? d.Hino : d.Chevrolet;
        var linea = caso == "n01-vendida" ? d.L500 : d.Nhr;
        await AfirmarAusente(api, caso, marca.Id, linea.Id);

        var busqueda = await api.Buscar();
        Assert.Equal(1, busqueda.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "renault-logan-2021" }, Slugs(busqueda.GetProperty("items")));
        Assert.Equal(FiltrosSoloV3, Resumen(await api.Filtros()));
        Assert.Empty((await api.Inicio()).GetProperty("destacados").EnumerateArray());
    }

    [Fact(DisplayName = "CA-VIS-15, CA-FIL-02: con todos los no visibles sembrados juntos los 4 endpoints coinciden")]
    public async Task Todos_los_no_visibles_juntos()
    {
        var d = Ref.Datos;
        foreach (var caso in Siembra.CasosNoVisibles)
            await AfirmarAusente(Ref, caso, caso == "n01-vendida" ? d.Hino.Id : d.Chevrolet.Id, null);
        Assert.Equal(4, (await Ref.Buscar()).GetProperty("total").GetInt32());
        Assert.Equal(
            "Camión=3,Carro=1|Chevrolet=2[NPR=1],Kenworth=1[T800=1],Renault=1[Logan=1]|45000000,250000000,2015,2021,500000",
            Resumen(await Ref.Filtros()));
    }

    [Fact(DisplayName = "CA-VIS-08: una vigencia con inicio = T exacto es visible (inicio inclusivo); con T+1h no")]
    public async Task Inicio_de_vigencia_inclusivo()
    {
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s =>
        {
            var p = s.Pub("inicio-exacto", vigente: false);
            s.Vigencia(p, T, T.AddDays(30));
            var q = s.Pub("inicio-futuro", vigente: false);
            s.Vigencia(q, T.AddHours(1), T.AddDays(30));
        });
        Assert.Equal(new[] { "inicio-exacto" }, await api.SlugsBusqueda());
        Assert.Equal(HttpStatusCode.OK, (await api.Detalle("inicio-exacto")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Detalle("inicio-futuro")).StatusCode);
    }

    [Fact(DisplayName = "CA-VIS-09, CA-003-07: varias vigencias (renovación y solapadas) y varias destacadas no duplican la publicación ni los totales")]
    public async Task Varias_vigencias_no_duplican()
    {
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s =>
        {
            var renovada = s.Pub("renovada", vigente: false, publicadaEn: T.AddDays(-1));
            s.Vigencia(renovada, T.AddDays(-40), T.AddDays(-10));
            s.Vigencia(renovada, T.AddDays(-10), T.AddDays(20));
            var solapada = s.Pub("solapada", vigente: false, publicadaEn: T.AddDays(-2));
            s.Vigencia(solapada, T.AddDays(-5), T.AddDays(20));
            s.Vigencia(solapada, T.AddDays(-3), T.AddDays(25));
            s.Destacar(solapada, T.AddDays(-10), T.AddDays(-5));
            s.Destacar(solapada, T.AddDays(-1), T.AddDays(3));
            s.Destacar(solapada, T.AddDays(-2), T.AddDays(4));
        });
        var busqueda = await api.Buscar();
        Assert.Equal(2, busqueda.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "solapada", "renovada" }, Slugs(busqueda.GetProperty("items")));
        var inicio = await api.Inicio();
        Assert.Equal(new[] { "solapada" }, Slugs(inicio.GetProperty("destacados")));
        Assert.Equal(new[] { "renovada", "solapada" }, Slugs(inicio.GetProperty("recientes")));
        Assert.Equal("Camión=2|Chevrolet=2[NPR=2]|100000000,100000000,2020,2020,50000", Resumen(await api.Filtros()));
    }

    [Fact(DisplayName = "CA-VIS-12: al desbloquear al asesor (ACTIVA) su publicación vigente vuelve a verse sin tocarla")]
    public async Task Asesor_desbloqueado_vuelve_a_exhibir()
    {
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s => s.CasoNoVisible("n08-asesor-bloqueado"));
        Assert.Equal(HttpStatusCode.NotFound, (await api.Detalle("n08-asesor-bloqueado")).StatusCode);
        var a2 = api.Datos.A2.Usuario.Id;
        await api.Db(async db => (await db.Users.SingleAsync(u => u.Id == a2)).EstadoCuenta = EstadosCuenta.Activa);
        Assert.Equal(HttpStatusCode.OK, (await api.Detalle("n08-asesor-bloqueado")).StatusCode);
        Assert.Equal(new[] { "n08-asesor-bloqueado" }, await api.SlugsBusqueda());
        Assert.Equal(new[] { "n08-asesor-bloqueado" }, Slugs((await api.Inicio()).GetProperty("destacados")));
    }

    [Fact(DisplayName = "CA-VIS-14: la vigencia se compara en UTC aunque el reloj esté en la zona de Colombia (UTC-5)")]
    public async Task Vigencia_en_utc_con_zona_colombia()
    {
        var colombia = TimeZoneInfo.CreateCustomTimeZone("Colombia-prueba", TimeSpan.FromHours(-5), "Colombia", "Colombia");
        // El mismo instante T expresado con desplazamiento -05:00.
        var ahoraLocal = new DateTimeOffset(T).ToOffset(TimeSpan.FromHours(-5));
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, ahora: ahoraLocal, zona: colombia, extra: s =>
        {
            var sigue = s.Pub("fin-en-1h", vigente: false);
            s.Vigencia(sigue, T.AddDays(-1), T.AddHours(1));
            var vencio = s.Pub("fin-hace-1min", vigente: false);
            s.Vigencia(vencio, T.AddDays(-1), T.AddMinutes(-1));
            // Vencería si se comparara con la hora local (T-5h) como si fuera UTC: debe seguir oculta.
            var futura = s.Pub("inicio-en-2h", vigente: false);
            s.Vigencia(futura, T.AddHours(2), T.AddDays(1));
        });
        Assert.Equal(new[] { "fin-en-1h" }, await api.SlugsBusqueda());
        Assert.Equal(HttpStatusCode.NotFound, (await api.Detalle("fin-hace-1min")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Detalle("inicio-en-2h")).StatusCode);
    }

    [Fact(DisplayName = "CA-VIS-16: sin caché; al pasar a VENDIDO o al avanzar el reloj tras el fin de la vigencia deja de verse")]
    public async Task Sin_cache_de_visibilidad()
    {
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s =>
        {
            s.Pub("se-vende");
            var p = s.Pub("se-vence", vigente: false);
            s.Vigencia(p, T.AddDays(-1), T.AddHours(2));
        });
        Assert.Equal(2, (await api.Buscar()).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await api.Detalle("se-vende")).StatusCode);

        await api.Db(async db => (await db.Publicaciones.SingleAsync(p => p.SlugPublico == "se-vende")).EstadoComercial = Publicacion.Vendido);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Detalle("se-vende")).StatusCode);
        Assert.Equal(new[] { "se-vence" }, await api.SlugsBusqueda());

        api.Reloj.Ahora = new DateTimeOffset(T.AddHours(2));
        Assert.Equal(0, (await api.Buscar()).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await api.Detalle("se-vence")).StatusCode);
        Assert.Equal("||null,null,null,null,null", Resumen(await api.Filtros()));
    }

    [Fact(DisplayName = "Condición 6 (C-P3-2): plan ACTIVA con fin_vigencia nula o futura se ve; la vigencia ligada a un plan vencido no se salva por otro plan activo")]
    public async Task Condicion_6_plan()
    {
        using var api = await CatalogoApi.Crear(visibles: false, noVisibles: false, extra: s =>
        {
            s.Pub("plan-sin-fin", plan: s.PlanSinFin);
            s.Pub("plan-fin-futuro"); // plan de A1: ACTIVA hasta T+30d
            // Vigencia actual ligada al plan vencido y una vigencia pasada con el plan activo: no visible.
            var p = s.Pub("plan-mezclado", vigente: false);
            s.Vigencia(p, T.AddDays(-40), T.AddDays(-10), s.A1.Plan);
            s.Vigencia(p, T.AddDays(-10), T.AddDays(20), s.PlanVencido);
        });
        Assert.Equal(new[] { "plan-fin-futuro", "plan-sin-fin" }, (await api.SlugsBusqueda()).OrderBy(x => x).ToArray());
        Assert.Equal(HttpStatusCode.NotFound, (await api.Detalle("plan-mezclado")).StatusCode);
    }

    [Fact(DisplayName = "CA-D11-01: el reservado V2 se ve en los 4 endpoints con estadoComercial RESERVADO")]
    public async Task Reservado_visible()
    {
        var d = Ref.Datos;
        var inicio = await Ref.Inicio();
        var tarjeta = inicio.GetProperty("recientes").EnumerateArray().Single(x => x.GetProperty("slug").GetString() == "kenworth-t800-2018");
        Assert.Equal("RESERVADO", tarjeta.GetProperty("estadoComercial").GetString());
        foreach (var q in new[] { "", $"?tipoId={d.Camion.Id}", $"?marcaId={d.Kenworth.Id}" })
        {
            var items = (await Ref.Buscar(q)).GetProperty("items").EnumerateArray()
                .Where(x => x.GetProperty("slug").GetString() == "kenworth-t800-2018").ToList();
            Assert.Single(items);
            Assert.Equal("RESERVADO", items[0].GetProperty("estadoComercial").GetString());
        }
        var detalle = await AyudasP2.Ok(await Ref.Detalle("kenworth-t800-2018"));
        Assert.Equal("RESERVADO", detalle.GetProperty("estadoComercial").GetString());
        Assert.Contains("Kenworth=1[T800=1]", Resumen(await Ref.Filtros()));
        // Los demás visibles son DISPONIBLE.
        Assert.All(inicio.GetProperty("recientes").EnumerateArray().Where(x => x.GetProperty("slug").GetString() != "kenworth-t800-2018"),
            x => Assert.Equal("DISPONIBLE", x.GetProperty("estadoComercial").GetString()));
    }
}
