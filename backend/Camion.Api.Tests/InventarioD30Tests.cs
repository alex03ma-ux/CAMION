using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.InventarioApi;
using static Camion.Api.Tests.Infraestructura.InventarioJson;

namespace Camion.Api.Tests;

/// <summary>
/// P4a — D-30: el vehículo se crea con sus fotos en una sola operación (multipart "datos" + 1 a 10 "fotos") y nunca se
/// queda sin fotos (DELETE de la única foto 409; PUT sobre un vehículo sin fotos 409). Salvaguarda de la placa.
/// Contrato: docs/criterios/P4a-contrato-api.md, sección "Cambios por D-30". SQLite en memoria y carpeta temporal.
/// </summary>
public sealed class InventarioD30Tests(InventarioFixture fx) : IClassFixture<InventarioFixture>
{
    private readonly InventarioApi _api = fx.Api;

    private Task<int> Vehiculos() => _api.Leer(db => db.Vehiculos.CountAsync());
    private Task<int> FilasFotos() => _api.Leer(db => db.FotosVehiculo.CountAsync());

    /// <summary>Ejecuta la petición y comprueba que no se creó ningún vehículo, fila de foto ni archivo.</summary>
    private async Task<HttpResponseMessage> SinEscribir(Func<Task<HttpResponseMessage>> peticion)
    {
        var (vehiculos, fotos, archivos) = (await Vehiculos(), await FilasFotos(), _api.ArchivosEnMedia());
        var r = await peticion();
        Assert.Equal(vehiculos, await Vehiculos());
        Assert.Equal(fotos, await FilasFotos());
        Assert.Equal(archivos, _api.ArchivosEnMedia());
        return r;
    }

    // ---------------------------------------------------------------- crear con fotos

    [Fact(DisplayName = "D-30: crear sin fotos (o con las fotos en otro campo) -> 400 errores.fotos \"Agrega al menos una foto del vehículo.\"")]
    public async Task Sin_fotos_400()
    {
        var datos = JsonSerializer.Serialize(_api.CuerpoCamion(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await Afirmar400(await SinEscribir(() => _api.CrearMultipart(_api.A.Token, datos, [])), "fotos", MsgSinFotos);
        await Afirmar400(await SinEscribir(() => _api.CrearMultipart(_api.A.Token, datos, [Jpeg()], campoFotos: "archivo")),
            "fotos", MsgSinFotos);
    }

    [Fact(DisplayName = "D-30: crear con 1 foto -> 201; el detalle trae la foto con orden 1 y portada, y el archivo se guarda")]
    public async Task Una_foto_201()
    {
        var bytes = Png(1024);
        var r = await _api.CrearConFotos(_api.A.Token, _api.CuerpoCamion(), [bytes]);
        var json = await Json(r, HttpStatusCode.Created);
        var id = json.GetProperty("id").GetInt64();
        var foto = Assert.Single(json.GetProperty("fotos").EnumerateArray());
        Assert.Equal(1, foto.GetProperty("orden").GetInt32());
        Assert.True(foto.GetProperty("esPortada").GetBoolean());
        var url = foto.GetProperty("url").GetString()!;
        Assert.Matches($"^/media/vehiculos/{id}/[0-9a-f]{{32}}\\.png$", url);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(_api.RutaFisica(url)));
        Assert.Equal(1, _api.ArchivosDe(id));
        // Una sola fila de auditoría CREAR.
        Assert.Equal("CREAR", Assert.Single(await _api.AuditoriaVehiculo(id)).Accion);
    }

    [Fact(DisplayName = "D-30, RN-VE-001: crear con 10 fotos -> 201 con orden 1..10 en el orden enviado; solo la primera es portada")]
    public async Task Diez_fotos_201()
    {
        var enviadas = Enumerable.Range(0, 10).Select(i => i % 3 == 0 ? Jpeg(300 + i) : i % 3 == 1 ? Png(300 + i) : Webp(300 + i)).ToList();
        var json = await Json(await _api.CrearConFotos(_api.A.Token, _api.CuerpoCarro(), enviadas), HttpStatusCode.Created);
        var id = json.GetProperty("id").GetInt64();
        var fotos = json.GetProperty("fotos").EnumerateArray().ToArray();
        Assert.Equal(Enumerable.Range(1, 10), fotos.Select(f => f.GetProperty("orden").GetInt32()));
        Assert.Equal(new[] { true }.Concat(Enumerable.Repeat(false, 9)), fotos.Select(f => f.GetProperty("esPortada").GetBoolean()));
        for (var i = 0; i < 10; i++)
            Assert.Equal(enviadas[i], await File.ReadAllBytesAsync(_api.RutaFisica(fotos[i].GetProperty("url").GetString()!)));
        Assert.Equal(10, _api.ArchivosDe(id));

        // Ya tiene 10: la subida de una más sigue respondiendo 409.
        await AyudasP2.AfirmarError(await _api.SubirFoto(_api.A.Token, id, Jpeg()), HttpStatusCode.Conflict, MsgMaxFotos);

        // La lista muestra la portada y el total.
        var item = (await Json(await _api.Lista(_api.A.Token, "?tamano=100"), HttpStatusCode.OK)).GetProperty("items")
            .EnumerateArray().Single(x => x.GetProperty("id").GetInt64() == id);
        Assert.Equal(10, item.GetProperty("totalFotos").GetInt32());
        Assert.Equal(fotos[0].GetProperty("url").GetString(), item.GetProperty("fotoPortada").GetString());
    }

    [Fact(DisplayName = "D-30: crear con 11 fotos -> 400 errores.fotos \"Puedes subir hasta 10 fotos.\" sin crear nada")]
    public async Task Once_fotos_400()
    {
        var fotos = Enumerable.Range(0, 11).Select(_ => Jpeg()).ToList();
        await Afirmar400(await SinEscribir(() => _api.CrearConFotos(_api.A.Token, _api.CuerpoCamion(), fotos)), "fotos",
            MsgDemasiadasFotos);
    }

    [Theory(DisplayName = "D-30: una foto no válida -> 400 errores.fotos \"Foto N: ...\"; no se crea el vehículo ni quedan archivos")]
    [InlineData("texto")]
    [InlineData("vacia")]
    [InlineData("grande")]
    public async Task Foto_invalida_400(string caso)
    {
        byte[] mala = caso switch
        {
            "texto" => "esto no es una imagen"u8.ToArray(),
            "vacia" => [],
            _ => Jpeg(5 * 1024 * 1024 + 1)
        };
        var r = await SinEscribir(() => _api.CrearConFotos(_api.A.Token, _api.CuerpoCamion(), [Jpeg(), mala, Png()]));
        await Afirmar400(r, "fotos", $"Foto 2: {MsgFoto}");
    }

    [Fact(DisplayName = "D-30: varias fotos no válidas se indican todas; los errores de datos y de fotos llegan juntos")]
    public async Task Errores_juntos()
    {
        var r = await SinEscribir(() => _api.CrearConFotos(_api.A.Token, Sin(_api.CuerpoCamion(), "precioCop"),
            [Jpeg(), "GIF89a-----"u8.ToArray(), "%PDF-1.7"u8.ToArray()]));
        Assert.Equal(new[] { $"Foto 2: {MsgFoto}", $"Foto 3: {MsgFoto}" }, await Errores(r, HttpStatusCode.BadRequest, "fotos", MsgRevisar));
        var json = await Json(r, HttpStatusCode.BadRequest);
        Assert.True(json.GetProperty("errores").TryGetProperty("precioCop", out _));
    }

    [Fact(DisplayName = "D-30: 'datos' con errores de validación -> 400 errores por campo, sin crear nada (las fotos válidas no se guardan)")]
    public async Task Datos_invalidos_400()
    {
        var r = await SinEscribir(() => _api.CrearConFotos(_api.A.Token, Con(_api.CuerpoCamion(), ("precioCop", 0)), [Jpeg(), Png()]));
        await Afirmar400(r, "precioCop", "Escribe el precio en pesos (número entero mayor que 0 y hasta 10.000.000.000).");
    }

    [Theory(DisplayName = "D-30: 'datos' ausente, vacío, null, repetido o que no es JSON -> 400 { error } formato común")]
    [InlineData("ausente")]
    [InlineData("vacio")]
    [InlineData("null")]
    [InlineData("no-json")]
    [InlineData("tipo-incorrecto")]
    [InlineData("repetido")]
    public async Task Datos_mal_formados_400(string caso)
    {
        var valido = JsonSerializer.Serialize(_api.CuerpoCamion(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        HttpResponseMessage r;
        if (caso == "repetido")
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(valido), "datos" },
                { new StringContent(valido), "datos" },
                { new ByteArrayContent(Jpeg()), "fotos", "f.jpg" }
            };
            var req = Ayudas.ConToken(HttpMethod.Post, "/api/inventario/vehiculos", _api.A.Token);
            req.Content = form;
            r = await SinEscribir(() => _api.C.SendAsync(req));
        }
        else
        {
            var datos = caso switch
            {
                "ausente" => null,
                "vacio" => "  ",
                "null" => "null",
                "no-json" => "{ no es json",
                _ => "{\"tipoId\":\"abc\"}"
            };
            r = await SinEscribir(() => _api.CrearMultipart(_api.A.Token, datos, [Jpeg()]));
        }
        await AyudasP2.AfirmarError(r, HttpStatusCode.BadRequest, MsgFormato);
    }

    [Fact(DisplayName = "D-30: crear con un JSON simple como antes -> 415 (ya no se acepta crear sin fotos) y no se crea nada")]
    public async Task Json_simple_rechazado()
    {
        var r = await SinEscribir(() => _api.Enviar(HttpMethod.Post, "/api/inventario/vehiculos", _api.A.Token, _api.CuerpoCamion()));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, r.StatusCode);
        var json = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("error").GetString()));
        // Sin cuerpo tampoco.
        Assert.Equal(HttpStatusCode.UnsupportedMediaType,
            (await SinEscribir(() => _api.Enviar(HttpMethod.Post, "/api/inventario/vehiculos", _api.A.Token))).StatusCode);
    }

    [Fact(DisplayName = "D-30: el administrador crea con fotos para un asesor; asesorId inválido -> 400 sin guardar las fotos")]
    public async Task Admin_crea_con_fotos()
    {
        var json = await Json(await _api.CrearConFotos(_api.Admin.Token, _api.CuerpoCarro(_api.B.PerfilId), [Jpeg(), Webp()]),
            HttpStatusCode.Created);
        Assert.Equal(2, json.GetProperty("fotos").GetArrayLength());
        Assert.Equal(_api.B.PerfilId, json.GetProperty("asesor").GetProperty("id").GetInt64());

        await Afirmar400(await SinEscribir(() => _api.CrearConFotos(_api.Admin.Token, _api.CuerpoCarro(987_654), [Jpeg()])),
            "asesorId", MsgAsesor);
    }

    [Fact(DisplayName = "D-30: placa repetida con fotos válidas -> 409 sin crear el vehículo ni dejar archivos")]
    public async Task Placa_repetida_sin_archivos()
    {
        var placa = PlacaUnica();
        await _api.CrearId(_api.A, "camion", ("placa", placa));
        var r = await SinEscribir(() => _api.CrearConFotos(_api.A.Token, Con(_api.CuerpoCamion(), ("placa", placa)), [Jpeg(), Png()]));
        Assert.Equal(new[] { MsgPlacaDuplicada }, await Errores(r, HttpStatusCode.Conflict, "placa"));
    }

    [Fact(DisplayName = "D-30, atomicidad: si la base falla al guardar las fotos -> 500; no queda vehículo, auditoría ni archivo suelto")]
    public async Task Atomicidad_si_falla_la_base()
    {
        // API propia: el disparador de prueba solo existe en esta base SQLite en memoria.
        using var api = await InventarioApi.Crear();
        await api.Db(db => db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER prueba_falla_foto BEFORE INSERT ON fotos_vehiculo BEGIN SELECT RAISE(ABORT, 'falla de prueba'); END;"));
        var vehiculos = await api.Leer(db => db.Vehiculos.CountAsync());
        var auditoria = await api.Leer(db => db.AuditoriaEventos.CountAsync(a => a.Entidad == "Vehiculo"));

        var r = await api.CrearConFotos(api.A.Token, api.CuerpoCamion(), [Jpeg(), Png(), Webp()]);
        Assert.Equal(HttpStatusCode.InternalServerError, r.StatusCode);
        Assert.Equal(vehiculos, await api.Leer(db => db.Vehiculos.CountAsync()));
        Assert.Equal(0, await api.Leer(db => db.FotosVehiculo.CountAsync()));
        Assert.Equal(auditoria, await api.Leer(db => db.AuditoriaEventos.CountAsync(a => a.Entidad == "Vehiculo")));
        Assert.Equal(0, api.ArchivosEnMedia());
        // La respuesta no expone detalles internos.
        Assert.DoesNotContain("falla de prueba", await r.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------- vehículos que ya existen

    [Fact(DisplayName = "D-30: borrar la última foto -> 409; el vehículo conserva su foto")]
    public async Task Borrar_ultima_409()
    {
        var (id, foto) = await _api.CrearConFoto(_api.A, "maquinaria");
        await AyudasP2.AfirmarError(await _api.BorrarFoto(_api.A.Token, id, foto.GetProperty("id").GetInt64()),
            HttpStatusCode.Conflict, MsgUltimaFoto);
        Assert.Equal(1, _api.ArchivosDe(id));
        Assert.Equal(1, (await Json(await _api.Detalle(_api.A.Token, id), HttpStatusCode.OK)).GetProperty("fotos").GetArrayLength());
    }

    [Fact(DisplayName = "D-30: PUT sobre un vehículo sin fotos -> 409 { error, errores.fotos } sin cambios; tras subir una foto -> 200")]
    public async Task Editar_sin_fotos_409()
    {
        var id = await _api.CrearId(_api.A);
        await _api.QuitarFotosDb(id); // como un vehículo DEMO o anterior a D-30
        var cuerpo = Con(_api.CuerpoCamion(), ("kilometraje", 130_000));

        var r = await _api.Editar(_api.A.Token, id, cuerpo);
        Assert.Equal(new[] { MsgEditarSinFotos }, await Errores(r, HttpStatusCode.Conflict, "fotos", MsgEditarSinFotos));
        Assert.Equal(120_000, (await _api.VehiculoDb(id)).Kilometraje);
        Assert.DoesNotContain(await _api.AuditoriaVehiculo(id), a => a.Accion == "EDITAR");
        // El administrador tampoco puede.
        Assert.Equal(HttpStatusCode.Conflict, (await _api.Editar(_api.Admin.Token, id, cuerpo)).StatusCode);

        // Las acciones de estado no exigen fotos.
        Assert.Equal(HttpStatusCode.OK, (await _api.Accion(_api.A.Token, id, "reservar")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.Accion(_api.A.Token, id, "liberar")).StatusCode);

        await _api.FotoSubida(_api.A.Token, id);
        var ok = await Json(await _api.Editar(_api.A.Token, id, cuerpo), HttpStatusCode.OK);
        Assert.Equal(130_000, ok.GetProperty("kilometraje").GetInt32());
    }

    // ---------------------------------------------------------------- salvaguarda de la placa

    [Fact(DisplayName = "Salvaguarda de la placa: en el caso normal (crear y editar con placa) no se registra la advertencia")]
    public async Task Log_placa_no_se_dispara()
    {
        var placa = PlacaUnica();
        var creado = await _api.Creado(_api.A.Token, Con(_api.CuerpoCamion(), ("placa", placa.ToLowerInvariant())));
        var id = creado.GetProperty("id").GetInt64();
        Assert.Equal(placa, creado.GetProperty("placa").GetString());
        var otra = PlacaUnica();
        var editado = await Json(await _api.Editar(_api.A.Token, id, Con(_api.CuerpoCamion(), ("placa", otra))), HttpStatusCode.OK);
        Assert.Equal(otra, editado.GetProperty("placa").GetString());
        // Sin placa tampoco es un caso de advertencia.
        await _api.CrearId(_api.A, "carro");

        Assert.Contains(_api.F.Logs, l => l.Contains("vehiculo_crear", StringComparison.Ordinal) && l.Contains($"vehiculoId={id}"));
        Assert.DoesNotContain(_api.F.Logs, l => l.Contains("placa_no_guardada", StringComparison.Ordinal));
        // Ningún log contiene la placa (sin datos personales).
        Assert.DoesNotContain(_api.F.Logs, l => l.Contains(placa, StringComparison.Ordinal) || l.Contains(otra, StringComparison.Ordinal));
    }
}
