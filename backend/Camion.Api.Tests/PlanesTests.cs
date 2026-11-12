using System.Net;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;

namespace Camion.Api.Tests;

/// <summary>P4p — planes: HU-049 crear, HU-050 modificar, HU-051 activar/inactivar (RN-PL-001, RN-PL-002, D-35).</summary>
public sealed class PlanesTests(P4pFixture fixture) : IClassFixture<P4pFixture>
{
    private readonly InventarioApi _api = fixture.Api;

    public static TheoryData<string, string> Rutas => new()
    {
        { "GET", "/api/admin/planes" },
        { "POST", "/api/admin/planes" },
        { "PUT", "/api/admin/planes/1" },
        { "POST", "/api/admin/planes/1/activar" },
        { "POST", "/api/admin/planes/1/inactivar" },
    };

    [Theory(DisplayName = "RN-SE-006: planes solo para el administrador (sin token 401; asesor y comprador 403)")]
    [MemberData(nameof(Rutas))]
    public async Task Permisos(string metodo, string ruta)
    {
        var m = new HttpMethod(metodo);
        var cuerpo = metodo == "GET" ? null : CuerpoPlan();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Enviar(m, ruta, null, cuerpo)).StatusCode);
        await Error(await _api.Enviar(m, ruta, _api.A.Token, cuerpo), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.Enviar(m, ruta, _api.TokenComprador, cuerpo), HttpStatusCode.Forbidden, MsgSinPermiso);
    }

    [Fact(DisplayName = "HU-049, D-35: crear un plan -> 201 con diasVigencia = 30 x meses, activo y auditoría CREAR")]
    public async Task Crear_201()
    {
        var nombre = Unico("Plan Trimestral");
        var json = await Esperar(await _api.Enviar(HttpMethod.Post, "/api/admin/planes", _api.Admin.Token,
            CuerpoPlan("  " + nombre + "  ", 250_000, 3, 12, true)), HttpStatusCode.Created);
        Assert.Equal(nombre, json.GetProperty("nombre").GetString());
        Assert.Equal(250_000, json.GetProperty("precioCop").GetInt64());
        Assert.Equal(3, json.GetProperty("duracionMeses").GetInt32());
        Assert.Equal(90, json.GetProperty("diasVigencia").GetInt32());
        Assert.Equal(12, json.GetProperty("limitePublicaciones").GetInt32());
        Assert.True(json.GetProperty("esPrueba").GetBoolean());
        Assert.True(json.GetProperty("activo").GetBoolean());
        Assert.Equal(0, json.GetProperty("suscripcionesVivas").GetInt32());

        var id = json.GetProperty("id").GetInt64();
        var audit = Assert.Single(await _api.Auditoria("Plan", id.ToString()));
        Assert.Equal("CREAR", audit.Accion);
        Assert.Equal(_api.Admin.Id, audit.UsuarioId);
        Assert.Null(audit.DatosAnteriores);
        Assert.Equal(nombre, JsonDe(audit.DatosNuevos)["nombre"].GetString());
    }

    [Fact(DisplayName = "HU-049: un plan de prueba puede costar 0; esPrueba es opcional (false)")]
    public async Task Precio_cero()
    {
        var cuerpo = CuerpoPlan(precio: 0);
        cuerpo.Remove("esPrueba");
        var json = await Esperar(await _api.Enviar(HttpMethod.Post, "/api/admin/planes", _api.Admin.Token, cuerpo),
            HttpStatusCode.Created);
        Assert.Equal(0, json.GetProperty("precioCop").GetInt64());
        Assert.False(json.GetProperty("esPrueba").GetBoolean());
    }

    public static TheoryData<string, object?, string> Invalidos => new()
    {
        { "nombre", "A", "Escribe el nombre del plan (2 a 100 caracteres)." },
        { "nombre", new string('x', 101), "Escribe el nombre del plan (2 a 100 caracteres)." },
        { "nombre", "   ", "Escribe el nombre del plan (2 a 100 caracteres)." },
        { "nombre", null, "Escribe el nombre del plan (2 a 100 caracteres)." },
        { "precioCop", -1, "Escribe el precio en pesos (número entero mayor o igual a 0)." },
        { "precioCop", 1.5, "Escribe el precio en pesos (número entero mayor o igual a 0)." },
        { "precioCop", null, "Escribe el precio en pesos (número entero mayor o igual a 0)." },
        { "duracionMeses", 0, "Escribe la duración en meses (número entero de 1 a 36)." },
        { "duracionMeses", 37, "Escribe la duración en meses (número entero de 1 a 36)." },
        { "duracionMeses", null, "Escribe la duración en meses (número entero de 1 a 36)." },
        { "limitePublicaciones", 0, "Escribe el límite de publicaciones (número entero de 1 a 1000)." },
        { "limitePublicaciones", 1001, "Escribe el límite de publicaciones (número entero de 1 a 1000)." },
        { "limitePublicaciones", null, "Escribe el límite de publicaciones (número entero de 1 a 1000)." },
    };

    [Theory(DisplayName = "HU-049/HU-050: validaciones -> 400 con errores por campo (crear y editar)")]
    [MemberData(nameof(Invalidos))]
    public async Task Validaciones_400(string campo, object? valor, string mensaje)
    {
        var cuerpo = CuerpoPlan();
        cuerpo[campo] = valor;
        await Error400(await _api.Enviar(HttpMethod.Post, "/api/admin/planes", _api.Admin.Token, cuerpo), campo, mensaje);
        var id = await _api.PlanId();
        await Error400(await _api.Enviar(HttpMethod.Put, $"/api/admin/planes/{id}", _api.Admin.Token, cuerpo), campo, mensaje);
    }

    [Theory(DisplayName = "HU-049: límites válidos (2 y 100 caracteres, 1 y 36 meses, 1 y 1000 publicaciones)")]
    [InlineData(2, 1, 1)]
    [InlineData(100, 36, 1000)]
    public async Task Limites_validos(int largo, int meses, int limite)
    {
        var nombre = Guid.NewGuid().ToString("N").PadRight(100, 'x')[..largo];
        var json = await Esperar(await _api.Enviar(HttpMethod.Post, "/api/admin/planes", _api.Admin.Token,
            CuerpoPlan(nombre, 1, meses, limite)), HttpStatusCode.Created);
        Assert.Equal(30 * meses, json.GetProperty("diasVigencia").GetInt32());
    }

    [Fact(DisplayName = "HU-049/HU-050: nombre único sin distinguir mayúsculas -> 409 al crear y al editar otro")]
    public async Task Duplicado_409()
    {
        var nombre = Unico("Plan Oro");
        await _api.CrearPlan(nombre);
        await Error(await _api.Enviar(HttpMethod.Post, "/api/admin/planes", _api.Admin.Token, CuerpoPlan(nombre.ToUpperInvariant())),
            HttpStatusCode.Conflict, MsgPlanDuplicado);
        var otro = await _api.PlanId();
        await Error(await _api.Enviar(HttpMethod.Put, $"/api/admin/planes/{otro}", _api.Admin.Token,
            CuerpoPlan(" " + nombre.ToLowerInvariant())), HttpStatusCode.Conflict, MsgPlanDuplicado);
    }

    [Fact(DisplayName = "HU-050: editar -> 200, auditoría EDITAR solo con lo que cambió; el propio nombre con otras mayúsculas vale")]
    public async Task Editar_200()
    {
        var nombre = Unico("Plan Plata");
        var id = (await _api.CrearPlan(nombre, meses: 1, limite: 5, precio: 50_000)).GetProperty("id").GetInt64();
        var json = await Esperar(await _api.Enviar(HttpMethod.Put, $"/api/admin/planes/{id}", _api.Admin.Token,
            CuerpoPlan(nombre.ToUpperInvariant(), 50_000, 2, 5)), HttpStatusCode.OK);
        Assert.Equal(nombre.ToUpperInvariant(), json.GetProperty("nombre").GetString());
        Assert.Equal(60, json.GetProperty("diasVigencia").GetInt32());

        var editar = Assert.Single(await _api.Auditoria("Plan", id.ToString()), a => a.Accion == "EDITAR");
        var antes = JsonDe(editar.DatosAnteriores);
        var despues = JsonDe(editar.DatosNuevos);
        Assert.Equal(new[] { "duracionMeses", "nombre" }, despues.Keys.Order().ToArray());
        Assert.Equal(1, antes["duracionMeses"].GetInt32());
        Assert.Equal(2, despues["duracionMeses"].GetInt32());
    }

    [Fact(DisplayName = "404 'No encontrado.' al editar, activar o inactivar un plan que no existe")]
    public async Task No_existe_404()
    {
        await Error(await _api.Enviar(HttpMethod.Put, "/api/admin/planes/999999", _api.Admin.Token, CuerpoPlan()),
            HttpStatusCode.NotFound, MsgNoEncontrado);
        foreach (var accion in new[] { "activar", "inactivar" })
            await Error(await _api.Enviar(HttpMethod.Post, $"/api/admin/planes/999999/{accion}", _api.Admin.Token),
                HttpStatusCode.NotFound, MsgNoEncontrado);
    }

    [Fact(DisplayName = "HU-051: inactivar y activar -> 200, auditoría, filtro ?activo= y el plan nunca se borra")]
    public async Task Inactivar_activar()
    {
        var id = await _api.PlanId();
        var r = await Esperar(await _api.Enviar(HttpMethod.Post, $"/api/admin/planes/{id}/inactivar", _api.Admin.Token),
            HttpStatusCode.OK);
        Assert.False(r.GetProperty("activo").GetBoolean());
        // Repetir no cambia nada ni audita otra vez.
        await Esperar(await _api.Enviar(HttpMethod.Post, $"/api/admin/planes/{id}/inactivar", _api.Admin.Token), HttpStatusCode.OK);

        var inactivos = await Esperar(await _api.Enviar(HttpMethod.Get, "/api/admin/planes?activo=false", _api.Admin.Token),
            HttpStatusCode.OK);
        Assert.Contains(inactivos.GetProperty("items").EnumerateArray(), p => p.GetProperty("id").GetInt64() == id);
        Assert.All(inactivos.GetProperty("items").EnumerateArray(), p => Assert.False(p.GetProperty("activo").GetBoolean()));
        var activos = await Esperar(await _api.Enviar(HttpMethod.Get, "/api/admin/planes?activo=true", _api.Admin.Token),
            HttpStatusCode.OK);
        Assert.DoesNotContain(activos.GetProperty("items").EnumerateArray(), p => p.GetProperty("id").GetInt64() == id);

        r = await Esperar(await _api.Enviar(HttpMethod.Post, $"/api/admin/planes/{id}/activar", _api.Admin.Token), HttpStatusCode.OK);
        Assert.True(r.GetProperty("activo").GetBoolean());
        Assert.Equal(new[] { "CREAR", "INACTIVAR", "ACTIVAR" },
            (await _api.Auditoria("Plan", id.ToString())).Select(a => a.Accion).ToArray());

        var todos = await Esperar(await _api.Enviar(HttpMethod.Get, "/api/admin/planes", _api.Admin.Token), HttpStatusCode.OK);
        Assert.Contains(todos.GetProperty("items").EnumerateArray(), p => p.GetProperty("id").GetInt64() == id);
    }

    [Fact(DisplayName = "GET /api/admin/planes?activo=otro -> 400 en errores.activo")]
    public async Task Filtro_activo_invalido() =>
        await Error400(await _api.Enviar(HttpMethod.Get, "/api/admin/planes?activo=quizas", _api.Admin.Token), "activo",
            "Escribe true o false.", MsgFiltros);

    [Fact(DisplayName = "HU-051 (RN-PL-002): inactivar no afecta las suscripciones que ya lo usan; suscripcionesVivas las cuenta")]
    public async Task Inactivar_no_afecta_suscripciones()
    {
        var asesor = await _api.CrearAsesor("Asesor Planes Ficticio");
        var plan = await _api.PlanId();
        var sus = (await _api.Asignada(asesor.PerfilId, plan)).GetProperty("id").GetInt64();
        await Esperar(await _api.Enviar(HttpMethod.Post, $"/api/admin/planes/{plan}/inactivar", _api.Admin.Token), HttpStatusCode.OK);
        Assert.Equal("ACTIVA", (await _api.SusDb(sus)).Estado);

        var lista = await Esperar(await _api.Enviar(HttpMethod.Get, "/api/admin/planes", _api.Admin.Token), HttpStatusCode.OK);
        var item = lista.GetProperty("items").EnumerateArray().Single(p => p.GetProperty("id").GetInt64() == plan);
        Assert.Equal(1, item.GetProperty("suscripcionesVivas").GetInt32());
    }

    [Fact(DisplayName = "HU-050 (RN-PL-001): editar el plan no cambia las suscripciones en curso")]
    public async Task Editar_no_cambia_suscripcion_en_curso()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor("Asesor Editar Plan Ficticio");
        var plan = await _api.PlanId(meses: 1);
        var sus = (await _api.Asignada(asesor.PerfilId, plan)).GetProperty("id").GetInt64();
        var antes = await _api.SusDb(sus);
        await Esperar(await _api.Enviar(HttpMethod.Put, $"/api/admin/planes/{plan}", _api.Admin.Token,
            CuerpoPlan(meses: 12, limite: 50)), HttpStatusCode.OK);
        var despues = await _api.SusDb(sus);
        Assert.Equal(antes.FinVigencia, despues.FinVigencia);
        Assert.Equal(antes.InicioVigencia, despues.InicioVigencia);
        Assert.Equal(T.AddDays(30), despues.FinVigencia);
    }
}
