using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.InventarioJson;

namespace Camion.Api.Tests;

/// <summary>
/// P4p — catálogos administrables (HU-082 marcas, HU-083 modelos, HU-084 tipos de carrocería; D-32, D-37): crear,
/// renombrar y activar/desactivar, sin borrar; nombres únicos sin mayúsculas ni tildes; auditoría "Catalogo".
/// </summary>
public sealed class CatalogosAdminTests(P4pFixture fixture) : IClassFixture<P4pFixture>
{
    private readonly InventarioApi _api = fixture.Api;

    public static TheoryData<string, string> Rutas => new()
    {
        { "GET", "/api/admin/catalogos/marcas" },
        { "POST", "/api/admin/catalogos/marcas" },
        { "PUT", "/api/admin/catalogos/marcas/1" },
        { "POST", "/api/admin/catalogos/carrocerias/1/activar" },
        { "POST", "/api/admin/catalogos/combustibles/1/desactivar" },
        { "POST", "/api/admin/catalogos/marcas/1/lineas" },
        { "PUT", "/api/admin/catalogos/lineas/1" },
        { "POST", "/api/admin/catalogos/lineas/1/activar" },
        { "POST", "/api/admin/catalogos/lineas/1/desactivar" },
    };

    [Theory(DisplayName = "RN-SE-006: catálogos administrables solo para el administrador (401 sin token; 403 asesor y comprador)")]
    [MemberData(nameof(Rutas))]
    public async Task Permisos(string metodo, string ruta)
    {
        var m = new HttpMethod(metodo);
        object? cuerpo = metodo == "GET" ? null : new { nombre = "Prueba Permisos", tipoIds = new[] { _api.Camion } };
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Enviar(m, ruta, null, cuerpo)).StatusCode);
        await Error(await _api.Enviar(m, ruta, _api.A.Token, cuerpo), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.Enviar(m, ruta, _api.TokenComprador, cuerpo), HttpStatusCode.Forbidden, MsgSinPermiso);
    }

    [Theory(DisplayName = "D-37: fuera del alcance (categorías, DIVIPOLA) o desconocido -> 404")]
    [InlineData("tipos-vehiculo")]
    [InlineData("departamentos")]
    [InlineData("municipios")]
    [InlineData("lineas")]
    [InlineData("otro")]
    public async Task Catalogo_desconocido_404(string catalogo)
    {
        await Error(await _api.Catalogo(catalogo), HttpStatusCode.NotFound, MsgNoEncontrado);
        await Error(await _api.CrearElemento(catalogo, "Algo"), HttpStatusCode.NotFound, MsgNoEncontrado);
    }

    [Theory(DisplayName = "HU-082/HU-084, D-37: GET lista { id, nombre, activo, vehiculos } con los inactivos incluidos")]
    [InlineData("carrocerias", "Niñera")]
    [InlineData("materiales", "Cartón")]
    [InlineData("tipos-maquina", "Grúa antigua")]
    [InlineData("combustibles", "Carbón")]
    public async Task Lista(string catalogo, string inactivo)
    {
        var json = await Esperar(await _api.Catalogo(catalogo), HttpStatusCode.OK);
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.All(items, i => Assert.Equal(new[] { "activo", "id", "nombre", "vehiculos" },
            i.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray()));
        Assert.False(items.Single(i => i.GetProperty("nombre").GetString() == inactivo).GetProperty("activo").GetBoolean());
    }

    [Fact(DisplayName = "HU-082/HU-083: GET marcas incluye sus modelos con tipoIds y número de vehículos")]
    public async Task Lista_marcas()
    {
        var id = await _api.CrearId(_api.A, "camion"); // Chevrolet NPR
        var json = await Esperar(await _api.Catalogo("marcas"), HttpStatusCode.OK);
        var chevrolet = json.GetProperty("items").EnumerateArray().Single(m => m.GetProperty("id").GetInt64() == _api.Chevrolet);
        Assert.True(chevrolet.GetProperty("vehiculos").GetInt32() >= 1);
        var npr = chevrolet.GetProperty("lineas").EnumerateArray().Single(l => l.GetProperty("id").GetInt64() == _api.Npr);
        Assert.Equal(new[] { "activo", "id", "nombre", "tipoIds", "vehiculos" },
            npr.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.True(npr.GetProperty("vehiculos").GetInt32() >= 1);
        Assert.True(id > 0);
    }

    [Theory(DisplayName = "D-37: crear -> 201 activo, auditoría CREAR con entidad_id '<catalogo>:<id>'")]
    [InlineData("marcas")]
    [InlineData("carrocerias")]
    [InlineData("materiales")]
    [InlineData("tipos-maquina")]
    [InlineData("combustibles")]
    public async Task Crear_201(string catalogo)
    {
        var nombre = Unico("Nuevo");
        var json = await Esperar(await _api.CrearElemento(catalogo, "  " + nombre + " "), HttpStatusCode.Created);
        Assert.Equal(nombre, json.GetProperty("nombre").GetString());
        Assert.True(json.GetProperty("activo").GetBoolean());
        Assert.Equal(0, json.GetProperty("vehiculos").GetInt32());
        var id = json.GetProperty("id").GetInt64();
        var audit = Assert.Single(await _api.Auditoria("Catalogo", $"{catalogo}:{id}"));
        Assert.Equal("CREAR", audit.Accion);
        Assert.Equal(_api.Admin.Id, audit.UsuarioId);
        Assert.Equal(nombre, JsonDe(audit.DatosNuevos)["nombre"].GetString());
    }

    [Theory(DisplayName = "D-37: nombre duplicado sin distinguir mayúsculas ni tildes (también con un inactivo) -> 409")]
    [InlineData("marcas", "  CHEVROLÉT ", "Ya existe una marca con ese nombre.")]
    [InlineData("marcas", "dína", "Ya existe una marca con ese nombre.")]
    [InlineData("carrocerias", "FURGON", "Ya existe un tipo de carrocería con ese nombre.")]
    [InlineData("carrocerias", "ninera", "Ya existe un tipo de carrocería con ese nombre.")]
    [InlineData("materiales", "aluminio", "Ya existe un material con ese nombre.")]
    [InlineData("tipos-maquina", "RETROEXCAVADORA", "Ya existe un tipo de máquina con ese nombre.")]
    [InlineData("combustibles", "diesel", "Ya existe un combustible con ese nombre.")]
    public async Task Duplicado_409(string catalogo, string nombre, string mensaje) =>
        await Error(await _api.CrearElemento(catalogo, nombre), HttpStatusCode.Conflict, mensaje);

    [Theory(DisplayName = "D-37: nombre vacío, solo espacios o de más de 100 caracteres -> 400 en errores.nombre")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("101")]
    public async Task Nombre_invalido_400(string? nombre)
    {
        if (nombre == "101") nombre = new string('m', 101);
        await Error400(await _api.CrearElemento("marcas", nombre), "nombre", "Escribe el nombre (1 a 100 caracteres).");
        await Error400(await _api.Renombrar("marcas", _api.Kenworth, nombre), "nombre", "Escribe el nombre (1 a 100 caracteres).");
    }

    [Fact(DisplayName = "D-37: renombrar -> 200 y el vehículo que lo usa muestra el nombre nuevo; auditoría RENOMBRAR")]
    public async Task Renombrar()
    {
        var combustible = (await Esperar(await _api.CrearElemento("combustibles", Unico("Gas")), HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        var vehiculo = await _api.CrearId(_api.A, "camion", ("combustibleId", combustible));
        var nuevo = Unico("Gas natural");
        var json = await Esperar(await _api.Renombrar("combustibles", combustible, nuevo), HttpStatusCode.OK);
        Assert.Equal(nuevo, json.GetProperty("nombre").GetString());
        Assert.Equal(1, json.GetProperty("vehiculos").GetInt32());
        var detalle = await Esperar(await _api.Detalle(_api.A.Token, vehiculo), HttpStatusCode.OK);
        Assert.Equal(nuevo, detalle.GetProperty("combustible").GetString());
        var audit = (await _api.Auditoria("Catalogo", $"combustibles:{combustible}")).Last();
        Assert.Equal("RENOMBRAR", audit.Accion);
        Assert.Equal(nuevo, JsonDe(audit.DatosNuevos)["nombre"].GetString());

        // Cambiar solo mayúsculas del propio nombre se permite; el de otro elemento no.
        await Esperar(await _api.Renombrar("combustibles", combustible, nuevo.ToUpperInvariant()), HttpStatusCode.OK);
        await Error(await _api.Renombrar("combustibles", combustible, "Diésel"), HttpStatusCode.Conflict,
            "Ya existe un combustible con ese nombre.");
        await Error(await _api.Renombrar("combustibles", 999_999, "Lo que sea"), HttpStatusCode.NotFound, MsgNoEncontrado);
    }

    [Fact(DisplayName = "D-37: desactivar una marca usada -> el vehículo la conserva y la muestra; deja de ofrecerse; activar la devuelve")]
    public async Task Desactivar_marca_usada()
    {
        var marca = (await Esperar(await _api.CrearElemento("marcas", Unico("Marca Usada")), HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        var linea = (await Esperar(await _api.CrearLinea(marca, "Modelo X", [_api.Camion]), HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        var vehiculo = await _api.CrearId(_api.A, "camion", ("marcaId", marca), ("lineaId", linea));

        var json = await Esperar(await _api.ActivarElemento("marcas", marca, false), HttpStatusCode.OK);
        Assert.False(json.GetProperty("activo").GetBoolean());
        Assert.Equal(1, json.GetProperty("vehiculos").GetInt32());

        var detalle = await Esperar(await _api.Detalle(_api.A.Token, vehiculo), HttpStatusCode.OK);
        Assert.Equal(marca, detalle.GetProperty("marcaId").GetInt64());
        Assert.False(string.IsNullOrEmpty(detalle.GetProperty("marca").GetString()));
        var lista = await Esperar(await _api.Lista(_api.Admin.Token, $"?marcaId={marca}"), HttpStatusCode.OK);
        Assert.Equal(1, lista.GetProperty("total").GetInt32());

        var formulario = await Esperar(await _api.Enviar(HttpMethod.Get, "/api/inventario/catalogos", _api.A.Token), HttpStatusCode.OK);
        Assert.DoesNotContain(formulario.GetProperty("marcas").EnumerateArray(), m => m.GetProperty("id").GetInt64() == marca);
        await Afirmar400(await _api.Crear(_api.A.Token, InventarioApi.Con(_api.CuerpoCamion(), ("marcaId", marca), ("lineaId", linea))),
            "marcaId", "Marca no válida.");

        await Esperar(await _api.ActivarElemento("marcas", marca, true), HttpStatusCode.OK);
        formulario = await Esperar(await _api.Enviar(HttpMethod.Get, "/api/inventario/catalogos", _api.A.Token), HttpStatusCode.OK);
        Assert.Contains(formulario.GetProperty("marcas").EnumerateArray(), m => m.GetProperty("id").GetInt64() == marca);
        Assert.Equal(new[] { "CREAR", "DESACTIVAR", "ACTIVAR" },
            (await _api.Auditoria("Catalogo", $"marcas:{marca}")).Select(a => a.Accion).ToArray());
    }

    [Fact(DisplayName = "D-37: desactivar un tipo de carrocería usado -> se conserva en el vehículo y deja de ofrecerse")]
    public async Task Desactivar_carroceria_usada()
    {
        var carroceria = (await Esperar(await _api.CrearElemento("carrocerias", Unico("Volco")), HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        var vehiculo = await _api.CrearId(_api.A, "carroceria", ("carroceriaId", carroceria));
        await Esperar(await _api.ActivarElemento("carrocerias", carroceria, false), HttpStatusCode.OK);
        var detalle = await Esperar(await _api.Detalle(_api.A.Token, vehiculo), HttpStatusCode.OK);
        Assert.Equal(carroceria, detalle.GetProperty("carroceriaId").GetInt64());
        Assert.False(string.IsNullOrEmpty(detalle.GetProperty("carroceria").GetString()));
        var formulario = await Esperar(await _api.Enviar(HttpMethod.Get, "/api/inventario/catalogos", _api.A.Token), HttpStatusCode.OK);
        Assert.DoesNotContain(formulario.GetProperty("carrocerias").EnumerateArray(), c => c.GetProperty("id").GetInt64() == carroceria);
        await Error(await _api.ActivarElemento("carrocerias", 999_999, false), HttpStatusCode.NotFound, MsgNoEncontrado);
    }

    [Fact(DisplayName = "HU-083 (D-32): crear modelo exige tipoIds de las 4 categorías; 201 con tipoIds; aparece en el formulario")]
    public async Task Crear_linea()
    {
        await Error400(await _api.CrearLinea(_api.Kenworth, Unico("W900"), null), "tipoIds", "Elige al menos una categoría.");
        await Error400(await _api.CrearLinea(_api.Kenworth, Unico("W900"), []), "tipoIds", "Elige al menos una categoría.");
        await Error400(await _api.CrearLinea(_api.Kenworth, Unico("W900"), [_api.Remolque]), "tipoIds", "Categoría no válida.");
        await Error400(await _api.CrearLinea(_api.Kenworth, Unico("W900"), [_api.Camion, 999_999]), "tipoIds", "Categoría no válida.");
        await Error400(await _api.CrearLinea(_api.Kenworth, " ", [_api.Camion]), "nombre", "Escribe el nombre (1 a 100 caracteres).");
        await Error(await _api.CrearLinea(999_999, Unico("W900"), [_api.Camion]), HttpStatusCode.NotFound, MsgNoEncontrado);

        var nombre = Unico("W900");
        var json = await Esperar(await _api.CrearLinea(_api.Kenworth, nombre, [_api.Maquinaria, _api.Camion, _api.Camion]),
            HttpStatusCode.Created);
        var id = json.GetProperty("id").GetInt64();
        Assert.Equal(_api.Kenworth, json.GetProperty("marcaId").GetInt64());
        Assert.Equal(new[] { _api.Camion, _api.Maquinaria }.Order().ToArray(),
            json.GetProperty("tipoIds").EnumerateArray().Select(x => x.GetInt64()).ToArray());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("aviso").ValueKind);

        var formulario = await Esperar(await _api.Enviar(HttpMethod.Get, "/api/inventario/catalogos", _api.A.Token), HttpStatusCode.OK);
        var kenworth = formulario.GetProperty("marcas").EnumerateArray().Single(m => m.GetProperty("id").GetInt64() == _api.Kenworth);
        Assert.Contains(kenworth.GetProperty("lineas").EnumerateArray(), l => l.GetProperty("id").GetInt64() == id);

        var audit = Assert.Single(await _api.Auditoria("Catalogo", $"lineas:{id}"));
        Assert.Equal("CREAR", audit.Accion);
    }

    [Fact(DisplayName = "HU-083: modelo duplicado en la misma marca (tildes/mayúsculas) -> 409; en otra marca se permite")]
    public async Task Linea_duplicada()
    {
        await Error(await _api.CrearLinea(_api.Chevrolet, "npr", [_api.Camion]), HttpStatusCode.Conflict,
            "Ya existe un modelo con ese nombre en esta marca.");
        var nombre = Unico("Línea Común");
        await Esperar(await _api.CrearLinea(_api.Chevrolet, nombre, [_api.Carro]), HttpStatusCode.Created);
        await Error(await _api.CrearLinea(_api.Chevrolet, nombre.Replace("í", "i").ToUpperInvariant(), [_api.Carro]),
            HttpStatusCode.Conflict, "Ya existe un modelo con ese nombre en esta marca.");
        await Esperar(await _api.CrearLinea(_api.Kenworth, nombre, [_api.Carro]), HttpStatusCode.Created);
        var otra = (await Esperar(await _api.CrearLinea(_api.Chevrolet, Unico("Otra"), [_api.Carro]), HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        await Error(await _api.EditarLinea(otra, new { nombre = "NPR" }), HttpStatusCode.Conflict,
            "Ya existe un modelo con ese nombre en esta marca.");
    }

    [Fact(DisplayName = "D-32: quitar una categoría que usan vehículos se permite y avisa; auditoría CATEGORIAS y RENOMBRAR")]
    public async Task Cambiar_categorias_con_aviso()
    {
        var linea = (await Esperar(await _api.CrearLinea(_api.Kenworth, Unico("T880"), [_api.Camion, _api.Maquinaria]),
            HttpStatusCode.Created)).GetProperty("id").GetInt64();
        var vehiculo = await _api.CrearId(_api.A, "camion", ("marcaId", _api.Kenworth), ("lineaId", linea));
        await _api.CrearId(_api.A, "maquinaria", ("lineaId", linea));

        var json = await Esperar(await _api.EditarLinea(linea, new { tipoIds = new[] { _api.Maquinaria } }), HttpStatusCode.OK);
        Assert.Equal(new[] { _api.Maquinaria }, json.GetProperty("tipoIds").EnumerateArray().Select(x => x.GetInt64()).ToArray());
        Assert.Equal("1 vehículo usa este modelo en una categoría que ya no tiene; se corregirá al editarlo.",
            json.GetProperty("aviso").GetString());
        Assert.Equal(2, json.GetProperty("vehiculos").GetInt32());
        // El vehículo conserva su modelo.
        var detalle = await Esperar(await _api.Detalle(_api.A.Token, vehiculo), HttpStatusCode.OK);
        Assert.Equal(linea, detalle.GetProperty("lineaId").GetInt64());

        var nuevo = Unico("T880 S");
        json = await Esperar(await _api.EditarLinea(linea, new { nombre = nuevo }), HttpStatusCode.OK);
        Assert.Equal(nuevo, json.GetProperty("nombre").GetString());
        Assert.Equal(new[] { _api.Maquinaria }, json.GetProperty("tipoIds").EnumerateArray().Select(x => x.GetInt64()).ToArray());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("aviso").ValueKind);

        var audit = await _api.Auditoria("Catalogo", $"lineas:{linea}");
        Assert.Equal(new[] { "CREAR", "CATEGORIAS", "RENOMBRAR" }, audit.Select(a => a.Accion).ToArray());
        Assert.Equal(1, JsonDe(audit[1].DatosNuevos)["vehiculosAfectados"].GetInt32());
    }

    [Fact(DisplayName = "D-32: el aviso usa el plural con varios vehículos; clasificar un modelo sin categorías también avisa")]
    public async Task Aviso_plural_y_sin_clasificar()
    {
        // T800 sin clasificar (se acepta en todas): dos camiones lo usan; al dejarlo solo en Maquinaria, avisa de 2.
        var t800 = (await Esperar(await _api.CrearLinea(_api.Kenworth, Unico("Sin clasificar"), [_api.Camion]),
            HttpStatusCode.Created)).GetProperty("id").GetInt64();
        await _api.Leer(async db =>
        {
            db.LineasTipos.RemoveRange(db.LineasTipos.Where(x => x.LineaId == t800));
            return await db.SaveChangesAsync();
        });
        await _api.CrearId(_api.A, "camion", ("marcaId", _api.Kenworth), ("lineaId", t800));
        await _api.CrearId(_api.B, "camion", ("marcaId", _api.Kenworth), ("lineaId", t800));
        var json = await Esperar(await _api.EditarLinea(t800, new { tipoIds = new[] { _api.Maquinaria } }), HttpStatusCode.OK);
        Assert.Equal("2 vehículos usan este modelo en una categoría que ya no tiene; se corregirán al editarlos.",
            json.GetProperty("aviso").GetString());
    }

    [Fact(DisplayName = "PUT lineas: sin nombre ni tipoIds o con tipoIds vacío -> 400; modelo inexistente -> 404")]
    public async Task Editar_linea_invalida()
    {
        await Error400(await _api.EditarLinea(_api.Npr, new { }), "nombre", "Escribe el nombre o elige las categorías.");
        await Error400(await _api.EditarLinea(_api.Npr, new { tipoIds = Array.Empty<long>() }), "tipoIds", "Elige al menos una categoría.");
        await Error(await _api.EditarLinea(999_999, new { nombre = "X" }), HttpStatusCode.NotFound, MsgNoEncontrado);
    }

    [Fact(DisplayName = "D-37: desactivar un modelo usado -> el vehículo lo conserva; deja de ofrecerse; auditoría")]
    public async Task Desactivar_linea()
    {
        var linea = (await Esperar(await _api.CrearLinea(_api.Chevrolet, Unico("NQR"), [_api.Camion]), HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        var vehiculo = await _api.CrearId(_api.A, "camion", ("lineaId", linea));
        var json = await Esperar(await _api.Enviar(HttpMethod.Post, $"/api/admin/catalogos/lineas/{linea}/desactivar",
            _api.Admin.Token), HttpStatusCode.OK);
        Assert.False(json.GetProperty("activo").GetBoolean());
        var detalle = await Esperar(await _api.Detalle(_api.A.Token, vehiculo), HttpStatusCode.OK);
        Assert.Equal(linea, detalle.GetProperty("lineaId").GetInt64());
        Assert.False(string.IsNullOrEmpty(detalle.GetProperty("linea").GetString()));
        var formulario = await Esperar(await _api.Enviar(HttpMethod.Get, "/api/inventario/catalogos", _api.A.Token), HttpStatusCode.OK);
        Assert.DoesNotContain(formulario.GetProperty("marcas").EnumerateArray().SelectMany(m => m.GetProperty("lineas").EnumerateArray()),
            l => l.GetProperty("id").GetInt64() == linea);
        await Esperar(await _api.Enviar(HttpMethod.Post, $"/api/admin/catalogos/lineas/{linea}/activar", _api.Admin.Token),
            HttpStatusCode.OK);
        Assert.Equal(new[] { "CREAR", "DESACTIVAR", "ACTIVAR" },
            (await _api.Auditoria("Catalogo", $"lineas:{linea}")).Select(a => a.Accion).ToArray());
        await Error(await _api.Enviar(HttpMethod.Post, "/api/admin/catalogos/lineas/999999/activar", _api.Admin.Token),
            HttpStatusCode.NotFound, MsgNoEncontrado);
    }
}
