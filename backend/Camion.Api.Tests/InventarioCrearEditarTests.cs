using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.InventarioApi;
using static Camion.Api.Tests.Infraestructura.InventarioJson;

namespace Camion.Api.Tests;

/// <summary>
/// P4a — POST y PUT /api/inventario/vehiculos (HU-013, HU-015 a HU-019; RN-VE-002, RN-VE-003, RN-VE-005, RN-VE-007,
/// RN-VE-009, RN-VE-010; D-2, D-17, D-19). Contrato: docs/criterios/P4a-contrato-api.md.
/// </summary>
public sealed class InventarioCrearEditarTests(InventarioFixture fx) : IClassFixture<InventarioFixture>
{
    private readonly InventarioApi _api = fx.Api;

    private static string NoAplica(string categoria) => categoria switch
    {
        "carro" => "Este dato no aplica a carro.",
        "camion" => "Este dato no aplica a camión.",
        "carroceria" => "Este dato no aplica a carrocería.",
        _ => "Este dato no aplica a maquinaria."
    };

    // ---------------------------------------------------------------- éxito por categoría

    [Theory(DisplayName = "HU-013 (D-17/D-19): crear con los obligatorios mínimos de cada categoría -> 201, ACTIVO, del asesor")]
    [InlineData("carro")]
    [InlineData("camion")]
    [InlineData("carroceria")]
    [InlineData("maquinaria")]
    public async Task Crear_minimo_por_categoria(string categoria)
    {
        var r = await _api.Crear(_api.A.Token, _api.Cuerpo(categoria));
        var json = await Json(r, HttpStatusCode.Created);
        var id = json.GetProperty("id").GetInt64();
        Assert.Equal($"/api/inventario/vehiculos/{id}", r.Headers.Location!.OriginalString);
        Assert.Equal("ACTIVO", json.GetProperty("estado").GetString());
        Assert.True(EsNulo(json, "precioAnteriorCop"));
        Assert.True(EsNulo(json, "motivoInactivacion"));
        Assert.Empty(json.GetProperty("fotos").EnumerateArray());
        Assert.False(json.TryGetProperty("asesor", out _), "El asesor no debe recibir el bloque 'asesor'.");

        var v = await _api.VehiculoDb(id);
        Assert.Equal(_api.A.PerfilId, v.AsesorId); // RN-VE-007
        if (categoria is "carroceria" or "maquinaria")
        {
            Assert.Equal(0, v.Kilometraje);        // no se admite: se guarda 0
            Assert.True(EsNulo(json, "kilometraje"));
        }
        if (categoria == "carroceria")
        {
            Assert.Null(v.MarcaId);
            Assert.Null(v.ModeloAnio);
            Assert.Null(v.Placa);
        }
    }

    [Fact(DisplayName = "HU-013/HU-015 a HU-018: crear con todos los campos opcionales de cada categoría los guarda y devuelve")]
    public async Task Crear_completo_por_categoria()
    {
        var a = _api;
        var camion = await a.Creado(a.A.Token, Con(a.CuerpoCamion(), ("placa", PlacaUnica()), ("carroceriaId", a.Furgon),
            ("combustibleId", a.Diesel), ("cilindrajeCc", 5193), ("pesoBrutoKg", 7500.5m), ("capacidadCargaKg", 4500.25m),
            ("descripcionTecnica", "  Motor revisado.  ")));
        Assert.Equal("Furgón", camion.GetProperty("carroceria").GetString());
        Assert.Equal("Diésel", camion.GetProperty("combustible").GetString());
        Assert.Equal(5193, camion.GetProperty("cilindrajeCc").GetInt32());
        Assert.Equal(7500.5m, camion.GetProperty("pesoBrutoKg").GetDecimal());
        Assert.Equal(4500.25m, camion.GetProperty("capacidadCargaKg").GetDecimal());
        Assert.Equal("Motor revisado.", camion.GetProperty("descripcionTecnica").GetString()); // recortada
        Assert.Equal("NPR", camion.GetProperty("linea").GetString());
        Assert.Equal("Antioquia", camion.GetProperty("departamento").GetString());
        Assert.Equal(100_000_000, camion.GetProperty("precioCop").GetInt64());

        var carroceria = await a.Creado(a.A.Token, Con(a.CuerpoCarroceria(), ("marcaId", a.Chevrolet), ("modeloAnio", 2015),
            ("largoM", 6.5m), ("anchoM", 2.4m), ("altoM", 2.1m), ("materialId", a.Aluminio), ("capacidadCargaKg", 3000m)));
        Assert.Equal(6.5m, carroceria.GetProperty("largoM").GetDecimal());
        Assert.Equal(2.4m, carroceria.GetProperty("anchoM").GetDecimal());
        Assert.Equal(2.1m, carroceria.GetProperty("altoM").GetDecimal());
        Assert.Equal("Aluminio", carroceria.GetProperty("material").GetString());
        Assert.Equal("Chevrolet", carroceria.GetProperty("marca").GetString());

        var maquina = await a.Creado(a.A.Token, Con(a.CuerpoMaquinaria(), ("horasUso", 1200), ("placa", PlacaUnica()),
            ("combustibleId", a.Diesel), ("capacidadCargaKg", 900m)));
        Assert.Equal(1200, maquina.GetProperty("horasUso").GetInt32());
        Assert.Equal("Retroexcavadora", maquina.GetProperty("tipoMaquina").GetString());
        Assert.True(EsNulo(maquina, "kilometraje"));

        var carro = await a.Creado(a.A.Token, Con(a.CuerpoCarro(), ("carroceriaId", a.Estacas), ("pesoBrutoKg", 1500m)));
        Assert.Equal("Estacas", carro.GetProperty("carroceria").GetString());
    }

    [Fact(DisplayName = "HU-013, RN-VE-007: el asesor siempre crea a su nombre; un asesorId ajeno en el cuerpo se ignora")]
    public async Task Asesor_crea_a_su_nombre()
    {
        var id = (await _api.Creado(_api.A.Token, _api.CuerpoCamion(asesorId: _api.B.PerfilId))).GetProperty("id").GetInt64();
        Assert.Equal(_api.A.PerfilId, (await _api.VehiculoDb(id)).AsesorId);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.Detalle(_api.B.Token, id)).StatusCode);
    }

    // ---------------------------------------------------------------- obligatorios por categoría

    [Theory(DisplayName = "D-17/D-19: falta un obligatorio de la categoría -> 400 en ese campo")]
    [InlineData("carro", "marcaId", "Elige la marca.")]
    [InlineData("carro", "modeloAnio", "AÑO")]
    [InlineData("carro", "kilometraje", "Escribe el kilometraje (número entero mayor o igual a 0).")]
    [InlineData("carro", "municipioId", "Elige el municipio.")]
    [InlineData("carro", "precioCop", "Escribe el precio en pesos (número entero mayor que 0 y hasta 10.000.000.000).")]
    [InlineData("camion", "marcaId", "Elige la marca.")]
    [InlineData("camion", "modeloAnio", "AÑO")]
    [InlineData("camion", "kilometraje", "Escribe el kilometraje (número entero mayor o igual a 0).")]
    [InlineData("camion", "municipioId", "Elige el municipio.")]
    [InlineData("camion", "precioCop", "Escribe el precio en pesos (número entero mayor que 0 y hasta 10.000.000.000).")]
    [InlineData("carroceria", "carroceriaId", "Elige el tipo de carrocería.")]
    [InlineData("carroceria", "municipioId", "Elige el municipio.")]
    [InlineData("carroceria", "precioCop", "Escribe el precio en pesos (número entero mayor que 0 y hasta 10.000.000.000).")]
    [InlineData("maquinaria", "marcaId", "Elige la marca.")]
    [InlineData("maquinaria", "modeloAnio", "AÑO")]
    [InlineData("maquinaria", "tipoMaquinaId", "Elige el tipo de máquina.")]
    [InlineData("maquinaria", "municipioId", "Elige el municipio.")]
    [InlineData("maquinaria", "precioCop", "Escribe el precio en pesos (número entero mayor que 0 y hasta 10.000.000.000).")]
    [InlineData("carro", "tipoId", "Elige la categoría.")]
    [InlineData("carro", "lineaId", "Elige el modelo.")]
    [InlineData("camion", "lineaId", "Elige el modelo.")]
    [InlineData("maquinaria", "lineaId", "Elige el modelo.")]
    public async Task Falta_obligatorio(string categoria, string campo, string mensaje)
    {
        if (mensaje == "AÑO") mensaje = $"Escribe un año entre 1950 y {Anio + 1}.";
        var antes = await _api.Leer(db => Task.FromResult(db.Vehiculos.Count()));
        await Afirmar400(await _api.Crear(_api.A.Token, Sin(_api.Cuerpo(categoria), campo)), campo, mensaje);
        // null explícito equivale a ausente.
        await Afirmar400(await _api.Crear(_api.A.Token, Con(_api.Cuerpo(categoria), (campo, null))), campo, mensaje);
        Assert.Equal(antes, await _api.Leer(db => Task.FromResult(db.Vehiculos.Count())));
    }

    [Theory(DisplayName = "D-19/D-25: marca, año y línea son opcionales solo en carrocería; horas de uso opcionales")]
    [InlineData("carroceria", "marcaId")]
    [InlineData("carroceria", "modeloAnio")]
    [InlineData("carroceria", "lineaId")]
    [InlineData("maquinaria", "horasUso")]
    public async Task Opcionales_ausentes_dan_201(string categoria, string campo) =>
        await _api.Creado(_api.A.Token, Sin(_api.Cuerpo(categoria), campo));

    // ---------------------------------------------------------------- no admitidos por categoría

    [Theory(DisplayName = "D-17/D-19: un campo que la categoría no admite -> 400 \"Este dato no aplica a <categoría>.\"")]
    [InlineData("carro", "horasUso", 10)]
    [InlineData("carro", "largoM", 2.5)]
    [InlineData("carro", "anchoM", 2.5)]
    [InlineData("carro", "altoM", 2.5)]
    [InlineData("carro", "materialId", "MATERIAL")]
    [InlineData("carro", "tipoMaquinaId", "MAQUINA")]
    [InlineData("carro", "capacidadCargaKg", 1000)]
    [InlineData("camion", "horasUso", 10)]
    [InlineData("camion", "largoM", 2.5)]
    [InlineData("camion", "anchoM", 2.5)]
    [InlineData("camion", "altoM", 2.5)]
    [InlineData("camion", "materialId", "MATERIAL")]
    [InlineData("camion", "tipoMaquinaId", "MAQUINA")]
    [InlineData("carroceria", "kilometraje", 1000)]
    [InlineData("carroceria", "horasUso", 10)]
    [InlineData("carroceria", "placa", "PLACA")]
    [InlineData("carroceria", "tipoMaquinaId", "MAQUINA")]
    [InlineData("carroceria", "combustibleId", "COMBUSTIBLE")]
    [InlineData("carroceria", "cilindrajeCc", 2000)]
    [InlineData("carroceria", "pesoBrutoKg", 1000)]
    [InlineData("maquinaria", "kilometraje", 1000)]
    [InlineData("maquinaria", "carroceriaId", "CARROCERIA")]
    [InlineData("maquinaria", "largoM", 2.5)]
    [InlineData("maquinaria", "anchoM", 2.5)]
    [InlineData("maquinaria", "altoM", 2.5)]
    [InlineData("maquinaria", "materialId", "MATERIAL")]
    public async Task No_admitido(string categoria, string campo, object valor)
    {
        object? v = valor switch
        {
            "MATERIAL" => _api.Aluminio,
            "MAQUINA" => _api.Retro,
            "COMBUSTIBLE" => _api.Diesel,
            "CARROCERIA" => _api.Furgon,
            "PLACA" => PlacaUnica(),
            double d => (decimal)d,
            _ => valor
        };
        await Afirmar400(await _api.Crear(_api.A.Token, Con(_api.Cuerpo(categoria), (campo, v))), campo, NoAplica(categoria));
    }

    [Theory(DisplayName = "D-19 (nota del backend): un campo no admitido da 400 aunque llegue en 0")]
    [InlineData("carroceria", "kilometraje")]
    [InlineData("maquinaria", "kilometraje")]
    [InlineData("carro", "horasUso")]
    [InlineData("carroceria", "horasUso")]
    [InlineData("camion", "largoM")]
    public async Task No_admitido_en_cero(string categoria, string campo) =>
        await Afirmar400(await _api.Crear(_api.A.Token, Con(_api.Cuerpo(categoria), (campo, 0))), campo, NoAplica(categoria));

    // ---------------------------------------------------------------- rangos y referencias

    [Theory(DisplayName = "HU-016/HU-017, RN-VE-003/005: topes de año, kilometraje, horas, cilindraje, precio y descripción")]
    [InlineData("camion", "modeloAnio", 1949, false)]
    [InlineData("camion", "modeloAnio", 1950, true)]
    [InlineData("camion", "modeloAnio", "ANIO+1", true)]
    [InlineData("camion", "modeloAnio", "ANIO+2", false)]
    [InlineData("camion", "kilometraje", -1, false)]
    [InlineData("camion", "kilometraje", 0, true)]
    [InlineData("camion", "kilometraje", 9_999_999, true)]
    [InlineData("camion", "kilometraje", 10_000_000, false)]
    [InlineData("maquinaria", "horasUso", -1, false)]
    [InlineData("maquinaria", "horasUso", 0, true)]
    [InlineData("maquinaria", "horasUso", 999_999, true)]
    [InlineData("maquinaria", "horasUso", 1_000_000, false)]
    [InlineData("camion", "cilindrajeCc", 0, false)]
    [InlineData("camion", "cilindrajeCc", 1, true)]
    [InlineData("camion", "cilindrajeCc", 100_000, true)]
    [InlineData("camion", "cilindrajeCc", 100_001, false)]
    [InlineData("camion", "precioCop", 0, false)]
    [InlineData("camion", "precioCop", -5, false)]
    [InlineData("camion", "precioCop", 1, true)]
    [InlineData("camion", "precioCop", 1.5, false)]
    [InlineData("camion", "precioCop", 10_000_000_000L, true)]
    [InlineData("camion", "precioCop", 10_000_000_001L, false)]
    [InlineData("camion", "pesoBrutoKg", 0, false)]
    [InlineData("camion", "pesoBrutoKg", 1.234, false)]
    [InlineData("carroceria", "largoM", 0, false)]
    [InlineData("carroceria", "largoM", -1, false)]
    [InlineData("carroceria", "largoM", 9999.99, true)]
    [InlineData("carroceria", "largoM", 10000, false)]
    [InlineData("carroceria", "altoM", 1.005, false)]
    [InlineData("camion", "descripcionTecnica", "4000", true)]
    [InlineData("camion", "descripcionTecnica", "4001", false)]
    public async Task Topes(string categoria, string campo, object valor, bool valido)
    {
        object? v = valor switch
        {
            "ANIO+1" => Anio + 1,
            "ANIO+2" => Anio + 2,
            "4000" => new string('d', 4000),
            "4001" => new string('d', 4001),
            double d => (decimal)d,
            _ => valor
        };
        var r = await _api.Crear(_api.A.Token, Con(_api.Cuerpo(categoria), (campo, v)));
        if (valido)
        {
            var json = await Json(r, HttpStatusCode.Created);
            if (campo == "precioCop") Assert.Equal(Convert.ToInt64(valor), json.GetProperty("precioCop").GetInt64());
        }
        else
            Assert.Single(await Errores(r, HttpStatusCode.BadRequest, campo, MsgRevisar));
    }

    [Theory(DisplayName = "HU-013: cada id debe existir y estar activo; la línea debe ser de la marca")]
    [InlineData("tipoId", "REMOLQUE", "Categoría no válida.")]
    [InlineData("tipoId", 999_999, "Categoría no válida.")]
    [InlineData("marcaId", "DINA", "Marca no válida.")]
    [InlineData("marcaId", 999_999, "Marca no válida.")]
    [InlineData("lineaId", "T800", "El modelo no corresponde a la marca elegida.")]
    [InlineData("lineaId", 999_999, "Modelo no válido.")]
    [InlineData("carroceriaId", "CARR_INACTIVA", "Tipo de carrocería no válido.")]
    [InlineData("combustibleId", "COMB_INACTIVO", "Combustible no válido.")]
    [InlineData("municipioId", 999_999, "Municipio no válido.")]
    public async Task Referencias_invalidas(string campo, object valor, string mensaje)
    {
        object v = valor switch
        {
            "REMOLQUE" => _api.Remolque,
            "DINA" => _api.Dina,
            "T800" => _api.T800,
            "CARR_INACTIVA" => _api.CarroceriaInactiva,
            "COMB_INACTIVO" => _api.CombustibleInactivo,
            _ => valor
        };
        var cuerpo = Con(_api.CuerpoCamion(), (campo, v));
        if (campo == "marcaId") cuerpo.Remove("lineaId");
        await Afirmar400(await _api.Crear(_api.A.Token, cuerpo), campo, mensaje);
    }

    [Fact(DisplayName = "D-19: material y tipo de máquina inactivos -> 400")]
    public async Task Material_y_maquina_inactivos()
    {
        await Afirmar400(await _api.Crear(_api.A.Token, Con(_api.CuerpoCarroceria(), ("materialId", _api.MaterialInactivo))),
            "materialId", "Material no válido.");
        await Afirmar400(await _api.Crear(_api.A.Token, Con(_api.CuerpoMaquinaria(), ("tipoMaquinaId", _api.MaquinaInactiva))),
            "tipoMaquinaId", "Tipo de máquina no válido.");
    }

    [Fact(DisplayName = "Validación: varios errores a la vez se devuelven juntos; JSON vacío o mal formado -> 400, nunca 500")]
    public async Task Varios_errores_y_json_invalido()
    {
        var r = await _api.Crear(_api.A.Token, new Dictionary<string, object?> { ["tipoId"] = _api.Camion });
        var json = await Json(r, HttpStatusCode.BadRequest);
        var campos = json.GetProperty("errores").EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.Superset(new HashSet<string> { "marcaId", "modeloAnio", "kilometraje", "municipioId", "precioCop" }, campos);

        var req = Ayudas.ConToken(HttpMethod.Post, "/api/inventario/vehiculos", _api.A.Token);
        req.Content = new StringContent("{ no es json", System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.C.SendAsync(req)).StatusCode);
        var req2 = Ayudas.ConToken(HttpMethod.Post, "/api/inventario/vehiculos", _api.A.Token);
        req2.Content = new StringContent("{\"tipoId\":\"abc\"}", System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.C.SendAsync(req2)).StatusCode);
    }

    // ---------------------------------------------------------------- placa (D-2, RN-VE-002)

    [Theory(DisplayName = "D-2: la placa se guarda en mayúsculas, sin espacios ni guiones")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Placa_normalizada(int variante)
    {
        var esperada = PlacaUnica(); // p. ej. ABC123
        var m = esperada.ToLowerInvariant();
        var bruta = variante switch
        {
            0 => $" {m[..3]}-{m[3..]} ",
            1 => $"{m[0]} {m[1..3]} {m[3]}-{m[4]}-{m[5]}",
            2 => $"{esperada[..3]}	{esperada[3..]}",
            _ => esperada
        };
        var json = await _api.Creado(_api.A.Token, Con(_api.CuerpoCamion(), ("placa", bruta)));
        Assert.Equal(esperada, json.GetProperty("placa").GetString());
        Assert.Equal(esperada, (await _api.VehiculoDb(json.GetProperty("id").GetInt64())).Placa);
    }

    [Theory(DisplayName = "D-2: placa con formato inválido (5 a 7 letras y números tras normalizar) -> 400")]
    [InlineData("AB12")]
    [InlineData("ABCD1234")]
    [InlineData("AB#123")]
    [InlineData("ÑAB123")]
    [InlineData("AB.123")]
    public async Task Placa_formato_invalido(string placa) =>
        await Afirmar400(await _api.Crear(_api.A.Token, Con(_api.CuerpoCamion(), ("placa", placa))), "placa", MsgPlaca);

    [Theory(DisplayName = "D-2: placa vacía o solo espacios/guiones = sin placa (null); varias filas sin placa son válidas")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" - - ")]
    public async Task Placa_vacia_es_null(string placa)
    {
        var a = await _api.Creado(_api.A.Token, Con(_api.CuerpoCamion(), ("placa", placa)));
        var b = await _api.Creado(_api.A.Token, Con(_api.CuerpoCamion(), ("placa", placa)));
        Assert.True(EsNulo(a, "placa"));
        Assert.True(EsNulo(b, "placa"));
    }

    [Fact(DisplayName = "D-2: placa repetida -> 409 { errores.placa }, también con minúsculas, espacios o guiones y entre asesores")]
    public async Task Placa_duplicada_409()
    {
        var placa = PlacaUnica(); // p. ej. ABC123
        await _api.Creado(_api.A.Token, Con(_api.CuerpoCamion(), ("placa", placa)));
        var variantes = new[] { placa, placa.ToLowerInvariant(), $"{placa[..3]}-{placa[3..]}", $" {placa[..3].ToLowerInvariant()} {placa[3..]} " };
        foreach (var variante in variantes)
        {
            var r = await _api.Crear(_api.A.Token, Con(_api.CuerpoCamion(), ("placa", variante)));
            Assert.Equal(new[] { MsgPlacaDuplicada }, await Errores(r, HttpStatusCode.Conflict, "placa"));
        }
        // Otro asesor y el administrador tampoco pueden repetirla (la placa es única en todo el sistema).
        Assert.Equal(HttpStatusCode.Conflict, (await _api.Crear(_api.B.Token, Con(_api.CuerpoCarro(), ("placa", placa)))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await _api.Crear(_api.Admin.Token, Con(_api.CuerpoMaquinaria(_api.B.PerfilId), ("placa", placa)))).StatusCode);
        Assert.Equal(1, await _api.Leer(db => Task.FromResult(db.Vehiculos.Count(v => v.Placa == placa))));
    }

    [Fact(DisplayName = "D-2: dos o más altas simultáneas con la misma placa -> exactamente una 201 y el resto 409 (nunca 500)")]
    public async Task Placa_concurrente()
    {
        var placa = PlacaUnica();
        var tareas = Enumerable.Range(0, 6).Select(i =>
            _api.Crear(i % 2 == 0 ? _api.A.Token : _api.B.Token,
                Con(_api.CuerpoCamion(), ("placa", i % 3 == 0 ? placa.ToLowerInvariant() : placa)))).ToArray();
        var respuestas = await Task.WhenAll(tareas);
        var codigos = respuestas.Select(r => (int)r.StatusCode).OrderBy(x => x).ToArray();
        Assert.True(codigos.Count(c => c == 201) == 1 && codigos.Count(c => c == 409) == 5,
            $"Códigos: {string.Join(",", codigos)}");
        Assert.Equal(1, await _api.Leer(db => Task.FromResult(db.Vehiculos.Count(v => v.Placa == placa))));
    }

    [Fact(DisplayName = "D-2: al editar, conservar la propia placa da 200; tomar la de otro vehículo -> 409; quitarla la deja en null")]
    public async Task Placa_al_editar()
    {
        var placaA = PlacaUnica();
        var placaB = PlacaUnica();
        var idA = await _api.CrearId(_api.A, "camion", ("placa", placaA));
        await _api.CrearId(_api.A, "camion", ("placa", placaB));

        var mismo = Con(_api.CuerpoCamion(), ("placa", placaA.ToLowerInvariant()), ("kilometraje", 1));
        Assert.Equal(HttpStatusCode.OK, (await _api.Editar(_api.A.Token, idA, mismo)).StatusCode);

        var r = await _api.Editar(_api.A.Token, idA, Con(_api.CuerpoCamion(), ("placa", placaB)));
        Assert.Equal(new[] { MsgPlacaDuplicada }, await Errores(r, HttpStatusCode.Conflict, "placa"));
        Assert.Equal(placaA, (await _api.VehiculoDb(idA)).Placa);

        var sin = await Json(await _api.Editar(_api.A.Token, idA, Con(_api.CuerpoCamion(), ("placa", null))), HttpStatusCode.OK);
        Assert.True(EsNulo(sin, "placa"));
        // La placa liberada se puede volver a usar.
        await _api.CrearId(_api.B, "carro", ("placa", placaA));
    }

    // ---------------------------------------------------------------- edición (HU-019)

    [Fact(DisplayName = "HU-016, RN-VE-003: al cambiar el precio se conserva el anterior; sin cambio de precio no se toca")]
    public async Task Precio_anterior()
    {
        var id = await _api.CrearId(_api.A, "camion", ("precioCop", 100_000_000));
        var e1 = await Json(await _api.Editar(_api.A.Token, id, Con(_api.CuerpoCamion(), ("precioCop", 90_000_000))), HttpStatusCode.OK);
        Assert.Equal(90_000_000, e1.GetProperty("precioCop").GetInt64());
        Assert.Equal(100_000_000, e1.GetProperty("precioAnteriorCop").GetInt64());

        // Otro cambio sin tocar el precio: el precio anterior sigue igual.
        var e2 = await Json(await _api.Editar(_api.A.Token, id,
            Con(_api.CuerpoCamion(), ("precioCop", 90_000_000), ("kilometraje", 130_000))), HttpStatusCode.OK);
        Assert.Equal(100_000_000, e2.GetProperty("precioAnteriorCop").GetInt64());

        var e3 = await Json(await _api.Editar(_api.A.Token, id, Con(_api.CuerpoCamion(), ("precioCop", 80_000_000))), HttpStatusCode.OK);
        Assert.Equal(90_000_000, e3.GetProperty("precioAnteriorCop").GetInt64());

        // El detalle (GET) muestra lo mismo.
        var d = await Json(await _api.Detalle(_api.A.Token, id), HttpStatusCode.OK);
        Assert.Equal(80_000_000, d.GetProperty("precioCop").GetInt64());
        Assert.Equal(90_000_000, d.GetProperty("precioAnteriorCop").GetInt64());
    }

    [Fact(DisplayName = "HU-019: precio inválido al editar -> 400 y no cambia nada")]
    public async Task Precio_invalido_al_editar()
    {
        var id = await _api.CrearId(_api.A);
        await Afirmar400(await _api.Editar(_api.A.Token, id, Con(_api.CuerpoCamion(), ("precioCop", 0))), "precioCop",
            "Escribe el precio en pesos (número entero mayor que 0 y hasta 10.000.000.000).");
        var v = await _api.VehiculoDb(id);
        Assert.Equal(100_000_000m, v.PrecioCop);
        Assert.Null(v.PrecioAnteriorCop);
    }

    [Fact(DisplayName = "HU-019: el tipo no se puede cambiar (400); sin tipoId en el cuerpo se usa el actual")]
    public async Task Tipo_no_editable()
    {
        var id = await _api.CrearId(_api.A, "camion");
        await Afirmar400(await _api.Editar(_api.A.Token, id, Con(_api.CuerpoCamion(), ("tipoId", _api.Carro))), "tipoId", MsgTipoCambio);
        Assert.Equal(_api.Camion, (await _api.VehiculoDb(id)).TipoVehiculoId);

        var sinTipo = await Json(await _api.Editar(_api.A.Token, id, Sin(Con(_api.CuerpoCamion(), ("kilometraje", 7)), "tipoId")),
            HttpStatusCode.OK);
        Assert.Equal(_api.Camion, sinTipo.GetProperty("tipoId").GetInt64());
        Assert.Equal(7, sinTipo.GetProperty("kilometraje").GetInt32());
    }

    [Fact(DisplayName = "HU-019: las reglas por categoría también valen al editar (campo no admitido, obligatorio ausente)")]
    public async Task Reglas_al_editar()
    {
        var id = await _api.CrearId(_api.A, "carroceria");
        await Afirmar400(await _api.Editar(_api.A.Token, id, Con(_api.CuerpoCarroceria(), ("kilometraje", 10))), "kilometraje",
            NoAplica("carroceria"));
        await Afirmar400(await _api.Editar(_api.A.Token, id, Sin(_api.CuerpoCarroceria(), "carroceriaId")), "carroceriaId",
            "Elige el tipo de carrocería.");
    }

    [Fact(DisplayName = "HU-019: PUT sobre un id inexistente -> 404 \"Vehículo no encontrado.\"")]
    public async Task Editar_inexistente_404() =>
        await AyudasP2.AfirmarError(await _api.Editar(_api.A.Token, 987_654, _api.CuerpoCamion()), HttpStatusCode.NotFound, MsgNoEncontrado);

    // ---------------------------------------------------------------- auditoría (RN-VE-009)

    [Fact(DisplayName = "HU-013, RN-VE-009: CREAR deja una fila de auditoría (entidad Vehiculo, actor, sin datos anteriores)")]
    public async Task Auditoria_crear()
    {
        var placa = PlacaUnica();
        var id = await _api.CrearId(_api.A, "camion", ("placa", placa));
        var fila = Assert.Single(await _api.AuditoriaVehiculo(id));
        Assert.Equal("CREAR", fila.Accion);
        Assert.Equal(_api.A.UsuarioId, fila.UsuarioId);
        Assert.Null(fila.DatosAnteriores);
        var nuevos = JsonDocument.Parse(fila.DatosNuevos!).RootElement;
        Assert.Equal(placa, nuevos.GetProperty("placa").GetString());
        Assert.Equal(_api.A.PerfilId, nuevos.GetProperty("asesorId").GetInt64());
        Assert.Equal("ACTIVO", nuevos.GetProperty("estado").GetString());
    }

    [Fact(DisplayName = "HU-019, RN-VE-009: EDITAR audita solo los campos que cambiaron; un PUT sin cambios no audita")]
    public async Task Auditoria_editar_solo_cambios()
    {
        var id = await _api.CrearId(_api.A, "camion");

        Assert.Equal(HttpStatusCode.OK, (await _api.Editar(_api.A.Token, id, Con(_api.CuerpoCamion(), ("kilometraje", 125_000)))).StatusCode);
        var editar = (await _api.AuditoriaVehiculo(id)).Where(a => a.Accion == "EDITAR").ToList();
        var e1 = Assert.Single(editar);
        Assert.Equal(_api.A.UsuarioId, e1.UsuarioId);
        var ant = JsonDocument.Parse(e1.DatosAnteriores!).RootElement;
        var nue = JsonDocument.Parse(e1.DatosNuevos!).RootElement;
        Assert.Equal(new[] { "kilometraje" }, CatalogoJson.Claves(ant));
        Assert.Equal(new[] { "kilometraje" }, CatalogoJson.Claves(nue));
        Assert.Equal(120_000, ant.GetProperty("kilometraje").GetInt32());
        Assert.Equal(125_000, nue.GetProperty("kilometraje").GetInt32());

        // Sin cambios (misma información, placa y descripción equivalentes): ninguna fila nueva.
        Assert.Equal(HttpStatusCode.OK, (await _api.Editar(_api.A.Token, id, Con(_api.CuerpoCamion(), ("kilometraje", 125_000)))).StatusCode);
        Assert.Single((await _api.AuditoriaVehiculo(id)).Where(a => a.Accion == "EDITAR"));

        // Precio y descripción: solo esos campos (y el precio anterior, que cambia con el precio).
        Assert.Equal(HttpStatusCode.OK, (await _api.Editar(_api.A.Token, id, Con(_api.CuerpoCamion(), ("kilometraje", 125_000),
            ("precioCop", 95_000_000), ("descripcionTecnica", "Nueva descripción")))).StatusCode);
        var e2 = (await _api.AuditoriaVehiculo(id)).Where(a => a.Accion == "EDITAR").Last();
        var ant2 = JsonDocument.Parse(e2.DatosAnteriores!).RootElement;
        var nue2 = JsonDocument.Parse(e2.DatosNuevos!).RootElement;
        Assert.Equal(new[] { "descripcionTecnica", "precioAnteriorCop", "precioCop" }, CatalogoJson.Claves(ant2));
        Assert.Equal(new[] { "descripcionTecnica", "precioAnteriorCop", "precioCop" }, CatalogoJson.Claves(nue2));
        Assert.Equal(100_000_000m, ant2.GetProperty("precioCop").GetDecimal());
        Assert.Equal(95_000_000m, nue2.GetProperty("precioCop").GetDecimal());
        Assert.Equal(JsonValueKind.Null, ant2.GetProperty("descripcionTecnica").ValueKind);
        Assert.Equal("Nueva descripción", nue2.GetProperty("descripcionTecnica").GetString());
    }

    [Fact(DisplayName = "RN-VE-009: un alta o edición rechazada (400/409) no deja auditoría")]
    public async Task Rechazos_no_auditan()
    {
        var antes = await _api.Leer(db => Task.FromResult(db.AuditoriaEventos.Count(a => a.Entidad == "Vehiculo")));
        await _api.Crear(_api.A.Token, Sin(_api.CuerpoCamion(), "precioCop"));
        var placa = PlacaUnica();
        var id = await _api.CrearId(_api.A, "camion", ("placa", placa));
        await _api.Crear(_api.A.Token, Con(_api.CuerpoCamion(), ("placa", placa)));
        await _api.Editar(_api.A.Token, id, Con(_api.CuerpoCamion(), ("tipoId", _api.Carro)));
        Assert.Equal(antes + 1, await _api.Leer(db => Task.FromResult(db.AuditoriaEventos.Count(a => a.Entidad == "Vehiculo"))));
    }

    [Theory(DisplayName = "D-25: placa de maquinaria hasta 8 caracteres; en carro y camión hasta 7")]
    [InlineData("maquinaria", "MC123456", true)]
    [InlineData("maquinaria", "MC1234567", false)]
    [InlineData("camion", "MC123456", false)]
    [InlineData("carro", "ABC1234", true)]
    public async Task Placa_por_categoria(string categoria, string placa, bool valida)
    {
        var r = await _api.Crear(_api.A.Token, Con(_api.Cuerpo(categoria), ("placa", placa)));
        if (valida) Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        else await Afirmar400(r, "placa", "Escribe una placa válida (letras y números).");
    }

    [Theory(DisplayName = "Defecto P4a: la placa se guarda al crear y al editar en carro, camión y maquinaria")]
    [InlineData("carro", "CAR123")]
    [InlineData("camion", "ABC123")]
    [InlineData("maquinaria", "MAQ12345")]
    public async Task Placa_se_guarda_por_categoria(string categoria, string placaBase)
    {
        var placa = placaBase[..3] + Random.Shared.Next(100, 999) + placaBase[6..];
        var creado = await _api.Creado(_api.A.Token, Con(_api.Cuerpo(categoria), ("placa", placa)));
        var id = creado.GetProperty("id").GetInt64();
        Assert.Equal(placa, creado.GetProperty("placa").GetString());
        Assert.Equal(placa, (await _api.VehiculoDb(id)).Placa);

        var otra = placaBase[..3] + Random.Shared.Next(100, 999) + placaBase[6..];
        var editado = await _api.Enviar(HttpMethod.Put, $"/api/inventario/vehiculos/{id}", _api.A.Token,
            Sin(Con(_api.Cuerpo(categoria), ("placa", otra)), "asesorId"));
        Assert.Equal(HttpStatusCode.OK, editado.StatusCode);
        Assert.Equal(otra, (await _api.VehiculoDb(id)).Placa);
    }

    [Theory(DisplayName = "Defecto P4a: la misma placa no se puede repetir entre categorías distintas")]
    [InlineData("maquinaria", "camion")]
    [InlineData("camion", "carro")]
    [InlineData("carro", "maquinaria")]
    public async Task Placa_repetida_entre_categorias_409(string primera, string segunda)
    {
        var placa = "PL" + Random.Shared.Next(10000, 99999);
        await _api.Creado(_api.A.Token, Con(_api.Cuerpo(primera), ("placa", placa)));
        var repetida = await _api.Crear(_api.A.Token, Con(_api.Cuerpo(segunda), ("placa", placa.ToLowerInvariant())));
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
    }
}
