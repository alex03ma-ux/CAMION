using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.Ayudas;
using static Camion.Api.Tests.Infraestructura.AyudasP2;

namespace Camion.Api.Tests;

/// <summary>P2 — GET /api/admin/usuarios (HU-048). Cada prueba con datos propios usa una base en memoria nueva.</summary>
public sealed class ListaUsuariosTests(CamionApiFactory factory) : IClassFixture<CamionApiFactory>
{
    private static string[] Ids(JsonElement pagina) =>
        pagina.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("usuarioId").GetString()!).ToArray();

    private static string[] Claves(JsonElement e) => e.EnumerateObject().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    [Fact(DisplayName = "CA-048-01/02/03/04/08, CA-N03-R02: lista con 3 roles y 4 estados, orden PENDIENTE primero, forma de ítem, filtros y sin campos internos")]
    public async Task Lista_filtros_y_orden()
    {
        await using var f = new CamionApiFactory();
        var c = f.CreateClient();
        var (adminId, _, admin) = await CrearAdministrador(f, c);
        var (c1, _) = await CrearComprador(c);
        var (c2, _) = await CrearComprador(c);
        await Ok(await AccionMotivo(c, admin, c2, "bloquear", MotivoValido));
        var (a1, _, _) = await CrearAsesorPendiente(c);
        var (a2, _, _) = await CrearAsesorPendiente(c);
        var (a3, _, _) = await CrearAsesorPendiente(c);
        await Ok(await AccionMotivo(c, admin, a3, "aprobar", null));
        var (a4, _, _) = await CrearAsesorPendiente(c);
        await Ok(await AccionMotivo(c, admin, a4, "rechazar", MotivoValido));
        var (a5, _, _) = await CrearAsesorPendiente(c);
        await Ok(await AccionMotivo(c, admin, a5, "aprobar", null));
        await Ok(await AccionMotivo(c, admin, a5, "bloquear", MotivoValido));
        var (a6, _, _) = await CrearAsesorPendiente(c); // el PENDIENTE más reciente

        var r = await Lista(c, admin);
        var json = await Ok(r);
        await AfirmarSinCamposInternos(r);
        Assert.Equal(new[] { "items", "pagina", "tamano", "total" }, Claves(json));
        Assert.Equal(9, json.GetProperty("total").GetInt32());
        Assert.Equal(1, json.GetProperty("pagina").GetInt32());
        Assert.Equal(20, json.GetProperty("tamano").GetInt32());

        var items = json.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(9, items.Length);
        // Orden: primero los PENDIENTE y después por creadoEn descendente.
        var claves = items.Select(i => (Pend: i.GetProperty("estado").GetString() == "PENDIENTE" ? 0 : 1,
            Creado: i.GetProperty("creadoEn").GetDateTime())).ToArray();
        var ordenado = claves.OrderBy(k => k.Pend).ThenByDescending(k => k.Creado).ToArray();
        Assert.Equal(ordenado, claves);
        Assert.Equal(new[] { a6, a2, a1 }, Ids(json).Take(3));

        foreach (var i in items)
        {
            Assert.Equal(new[] { "asesor", "creadoEn", "email", "estado", "nombreCompleto", "roles", "telefono", "ultimoAccesoEn", "usuarioId" }, Claves(i));
            Assert.Equal(JsonValueKind.Array, i.GetProperty("roles").ValueKind);
            var roles = i.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ToArray();
            if (roles.Contains("ASESOR"))
                Assert.Equal(new[] { "autorizadoEn", "estadoAutorizacion", "nombreComercial", "numeroDocumento", "tipoDocumento" },
                    Claves(i.GetProperty("asesor")));
            else
                Assert.Equal(JsonValueKind.Null, i.GetProperty("asesor").ValueKind);
        }
        var porId = items.ToDictionary(i => i.GetProperty("usuarioId").GetString()!);
        Assert.Equal(["ADMINISTRADOR"], porId[adminId].GetProperty("roles").EnumerateArray().Select(x => x.GetString()!).ToArray());
        Assert.Equal("BLOQUEADO", porId[a5].GetProperty("asesor").GetProperty("estadoAutorizacion").GetString());
        Assert.Equal("RECHAZADO", porId[a4].GetProperty("asesor").GetProperty("estadoAutorizacion").GetString());
        Assert.Equal("APROBADO", porId[a3].GetProperty("asesor").GetProperty("estadoAutorizacion").GetString());

        // CA-048-04 y CA-N03-R02.
        var pend = await Ok(await Lista(c, admin, "?estado=PENDIENTE"));
        Assert.Equal(new[] { a1, a2, a6 }.Order(), Ids(pend).Order());
        Assert.Equal(3, pend.GetProperty("total").GetInt32());
        Assert.DoesNotContain(c1, Ids(pend));
        var asesores = await Ok(await Lista(c, admin, "?rol=ASESOR"));
        Assert.Equal(new[] { a1, a2, a3, a4, a5, a6 }.Order(), Ids(asesores).Order());
        Assert.Equal(6, asesores.GetProperty("total").GetInt32());
        var bloqComp = await Ok(await Lista(c, admin, "?estado=BLOQUEADA&rol=COMPRADOR"));
        Assert.Equal(new[] { c2 }, Ids(bloqComp));
        Assert.Equal(1, bloqComp.GetProperty("total").GetInt32());
        var admins = await Ok(await Lista(c, admin, "?rol=ADMINISTRADOR"));
        Assert.Equal(new[] { adminId }, Ids(admins));

        // CA-048-08
        var vacia = await Ok(await Lista(c, admin, "?buscar=zzzz"));
        Assert.Empty(vacia.GetProperty("items").EnumerateArray());
        Assert.Equal(0, vacia.GetProperty("total").GetInt32());
        Assert.Equal(1, vacia.GetProperty("pagina").GetInt32());
        Assert.Equal(20, vacia.GetProperty("tamano").GetInt32());
    }

    [Fact(DisplayName = "CA-048-05: buscar por coincidencia parcial en nombre o correo, sin distinguir mayúsculas")]
    public async Task Busqueda()
    {
        await using var f = new CamionApiFactory();
        var c = f.CreateClient();
        var (_, _, admin) = await CrearAdministrador(f, c);
        var (luis, _) = await CrearComprador(c, "Luis Gómez");
        var rAna = await Registrar(c, Registro("ana.perez@ejemplo.com", nombre: "Ana Ficticia"));
        Assert.Equal(HttpStatusCode.Created, rAna.StatusCode);
        var ana = (await Json(rAna)).GetProperty("usuarioId").GetString()!;
        await CrearComprador(c, "Otra Persona");

        foreach (var (buscar, esperado) in new[] { ("gómez", luis), ("LUIS", luis), ("ana.perez", ana), ("ANA.PEREZ@", ana), ("ficti", ana) })
        {
            var json = await Ok(await Lista(c, admin, "?buscar=" + Uri.EscapeDataString(buscar)));
            Assert.True(Ids(json).SequenceEqual(new[] { esperado }), $"buscar={buscar} -> {string.Join(",", Ids(json))}");
            Assert.Equal(1, json.GetProperty("total").GetInt32());
        }
    }

    [Fact(DisplayName = "CA-048-06: 25 usuarios: tamano=10&pagina=3 -> 5; pagina=4 -> []; tamano=100 -> 25")]
    public async Task Paginacion()
    {
        await using var f = new CamionApiFactory();
        var c = f.CreateClient();
        var (_, _, admin) = await CrearAdministrador(f, c);
        for (var i = 0; i < 24; i++) await CrearComprador(c, $"Persona {i:00}");

        var p3 = await Ok(await Lista(c, admin, "?tamano=10&pagina=3"));
        Assert.Equal(5, p3.GetProperty("items").GetArrayLength());
        Assert.Equal(25, p3.GetProperty("total").GetInt32());
        Assert.Equal(3, p3.GetProperty("pagina").GetInt32());
        Assert.Equal(10, p3.GetProperty("tamano").GetInt32());

        var p4 = await Ok(await Lista(c, admin, "?tamano=10&pagina=4"));
        Assert.Equal(0, p4.GetProperty("items").GetArrayLength());
        Assert.Equal(25, p4.GetProperty("total").GetInt32());

        var todo = await Ok(await Lista(c, admin, "?tamano=100"));
        Assert.Equal(25, todo.GetProperty("items").GetArrayLength());

        // Las páginas no se solapan y cubren a todos.
        var ids = new List<string>();
        for (var p = 1; p <= 3; p++) ids.AddRange(Ids(await Ok(await Lista(c, admin, $"?tamano=10&pagina={p}"))));
        Assert.Equal(25, ids.Distinct().Count());
    }

    [Theory(DisplayName = "CA-048-07: filtros inválidos -> 400 'Revisa los filtros.' con errores por parámetro; nunca 500")]
    [InlineData("estado=ACTIVO", "estado", "Estado no válido.")]
    [InlineData("rol=VENDEDOR", "rol", "Rol no válido.")]
    [InlineData("pagina=0", "pagina", "Página o tamaño no válidos.")]
    [InlineData("pagina=-1", "pagina", "Página o tamaño no válidos.")]
    [InlineData("pagina=abc", "pagina", "Página o tamaño no válidos.")]
    [InlineData("tamano=0", "tamano", "Página o tamaño no válidos.")]
    [InlineData("tamano=101", "tamano", "Página o tamaño no válidos.")]
    [InlineData("tamano=abc", "tamano", "Página o tamaño no válidos.")]
    [InlineData("buscar=101", "buscar", "La búsqueda no puede tener más de 100 caracteres.")]
    public async Task Filtros_invalidos(string query, string campo, string mensaje)
    {
        var c = factory.CreateClient();
        var (_, _, admin) = await CrearAdministrador(factory, c);
        if (query == "buscar=101") query = "buscar=" + new string('b', 101);
        var r = await Lista(c, admin, "?" + query);
        await AfirmarError(r, HttpStatusCode.BadRequest, "Revisa los filtros.");
        Assert.Equal([mensaje], await ErroresDe(r, campo));
    }

    [Fact(DisplayName = "CA-048-07: los límites válidos (tamano=100, buscar de 100, filtros en minúscula) -> 200")]
    public async Task Filtros_en_el_limite()
    {
        var c = factory.CreateClient();
        var (_, _, admin) = await CrearAdministrador(factory, c);
        Assert.Equal(HttpStatusCode.OK, (await Lista(c, admin, "?tamano=100&pagina=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Lista(c, admin, "?buscar=" + new string('b', 100))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Lista(c, admin, "?estado=pendiente&rol=asesor")).StatusCode);
    }
}
