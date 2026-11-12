using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.InventarioApi;
using static Camion.Api.Tests.Infraestructura.InventarioJson;

namespace Camion.Api.Tests;

/// <summary>
/// P4a — fotos del vehículo (HU-014, RN-VE-001, D-20): subir (firma JPEG/PNG/WebP, 5 MB, máximo 10), borrar
/// (renumera, nueva portada, borra el archivo) y reordenar. Las fotos se guardan en la carpeta temporal de la fábrica.
/// </summary>
public sealed class InventarioFotosTests(InventarioFixture fx) : IClassFixture<InventarioFixture>
{
    private readonly InventarioApi _api = fx.Api;

    private async Task<JsonElement[]> FotosDetalle(long id) =>
        (await Json(await _api.Detalle(_api.A.Token, id), HttpStatusCode.OK)).GetProperty("fotos").EnumerateArray().ToArray();

    [Theory(DisplayName = "HU-014, D-20: JPEG, PNG y WebP por la firma -> 201 { id, url, orden, esPortada }; nombre aleatorio con la extensión de la firma")]
    [InlineData("jpg")]
    [InlineData("png")]
    [InlineData("webp")]
    public async Task Subir_formato_valido(string formato)
    {
        // D-30: el vehículo nace con una foto (la portada); la subida toma el orden 2.
        var id = await _api.CrearId(_api.A);
        var bytes = formato switch { "jpg" => Jpeg(2048), "png" => Png(2048), _ => Webp(2048) };
        // Nombre y Content-Type del cliente engañosos a propósito: no se usan.
        var r = await _api.SubirFoto(_api.A.Token, id, bytes, nombre: "mi-foto-secreta.gif", tipo: "application/octet-stream");
        var json = await Json(r, HttpStatusCode.Created);
        Assert.Equal(new[] { "esPortada", "id", "orden", "url" }, CatalogoJson.Claves(json));
        Assert.Equal(2, json.GetProperty("orden").GetInt32());
        Assert.False(json.GetProperty("esPortada").GetBoolean());
        var url = json.GetProperty("url").GetString()!;
        Assert.Matches($"^/media/vehiculos/{id}/[0-9a-f]{{32}}\\.{formato}$", url);
        Assert.DoesNotContain("secreta", url);
        Assert.Equal($"/api/inventario/vehiculos/{id}/fotos/{json.GetProperty("id").GetInt64()}", r.Headers.Location!.OriginalString);

        var ruta = _api.RutaFisica(url);
        Assert.StartsWith(Path.GetFullPath(_api.F.CarpetaMedia), Path.GetFullPath(ruta));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(ruta));
        Assert.Equal(2, (await FotosDetalle(id)).Length);
    }

    [Fact(DisplayName = "HU-014, D-20: en un vehículo sin fotos (anterior a D-30) la primera subida es la portada con orden 1")]
    public async Task Subir_primera_en_vehiculo_sin_fotos()
    {
        var id = await _api.CrearId(_api.A);
        await _api.QuitarFotosDb(id);
        var json = await _api.FotoSubida(_api.A.Token, id, Png());
        Assert.Equal(1, json.GetProperty("orden").GetInt32());
        Assert.True(json.GetProperty("esPortada").GetBoolean());
    }

    [Theory(DisplayName = "HU-014, D-20: archivo que no es imagen aunque tenga extensión/Content-Type de imagen -> 400 y no se guarda nada")]
    [InlineData("texto")]
    [InlineData("gif")]
    [InlineData("pdf")]
    [InlineData("riff-no-webp")]
    [InlineData("vacio")]
    [InlineData("corto")]
    public async Task Rechaza_firma_falsa(string caso)
    {
        var id = await _api.CrearId(_api.A);
        byte[] bytes = caso switch
        {
            "texto" => "esto no es una imagen, solo texto"u8.ToArray(),
            "gif" => "GIF89a-----------"u8.ToArray(),
            "pdf" => "%PDF-1.7 ficticio"u8.ToArray(),
            "riff-no-webp" => "RIFF\x10\0\0\0WAVEfmt "u8.ToArray(),
            "vacio" => [],
            _ => new byte[] { 0xFF, 0xD8 }
        };
        var r = await _api.SubirFoto(_api.A.Token, id, bytes, nombre: "foto.jpg", tipo: "image/jpeg");
        await AyudasP2.AfirmarError(r, HttpStatusCode.BadRequest, MsgFoto);
        // Solo queda la foto con la que nació el vehículo (D-30).
        Assert.Equal(1, _api.ArchivosDe(id));
        Assert.Single(await FotosDetalle(id));
    }

    [Fact(DisplayName = "HU-014, D-20: exactamente 5 MB se acepta; 5 MB + 1 byte -> 400; cuerpo de más de 6 MB -> rechazado sin guardar")]
    public async Task Limite_de_tamano()
    {
        var id = await _api.CrearId(_api.A);
        const int cincoMb = 5 * 1024 * 1024;
        Assert.Equal(HttpStatusCode.Created, (await _api.SubirFoto(_api.A.Token, id, Jpeg(cincoMb))).StatusCode);
        await AyudasP2.AfirmarError(await _api.SubirFoto(_api.A.Token, id, Jpeg(cincoMb + 1)), HttpStatusCode.BadRequest, MsgFoto);

        var r = await _api.SubirFoto(_api.A.Token, id, Jpeg(7 * 1024 * 1024));
        Assert.True(r.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge,
            $"Se esperaba 400 (o 413) y llegó {(int)r.StatusCode}");
        // La foto inicial (D-30) y la de 5 MB.
        Assert.Equal(2, _api.ArchivosDe(id));
        Assert.Equal(2, (await FotosDetalle(id)).Length);
    }

    [Fact(DisplayName = "HU-014: sin campo 'archivo', con otro nombre de campo o sin multipart -> 400")]
    public async Task Peticion_sin_archivo()
    {
        var id = await _api.CrearId(_api.A);
        await AyudasP2.AfirmarError(await _api.SubirFoto(_api.A.Token, id, Jpeg(), campo: "foto"), HttpStatusCode.BadRequest, MsgFoto);
        var json = await _api.Enviar(HttpMethod.Post, $"/api/inventario/vehiculos/{id}/fotos", _api.A.Token, new { archivo = "abc" });
        await AyudasP2.AfirmarError(json, HttpStatusCode.BadRequest, MsgFoto);
        Assert.Equal(1, _api.ArchivosDe(id));
    }

    [Fact(DisplayName = "RN-VE-001, D-20: hasta 10 fotos (orden 1..10, solo la primera es portada); la 11.ª -> 409 y no deja archivo")]
    public async Task Maximo_diez()
    {
        // D-30: la foto con la que nace el vehículo es la 1.ª; se suben 9 más.
        var (id, inicial) = await _api.CrearConFoto(_api.A);
        var fotos = new List<JsonElement> { inicial };
        for (var i = 1; i < 10; i++) fotos.Add(await _api.FotoSubida(_api.A.Token, id, i % 3 == 0 ? Jpeg() : i % 3 == 1 ? Png() : Webp()));
        Assert.Equal(Enumerable.Range(1, 10), fotos.Select(f => f.GetProperty("orden").GetInt32()));
        Assert.Equal(new[] { true }.Concat(Enumerable.Repeat(false, 9)), fotos.Select(f => f.GetProperty("esPortada").GetBoolean()));

        await AyudasP2.AfirmarError(await _api.SubirFoto(_api.A.Token, id, Jpeg()), HttpStatusCode.Conflict, MsgMaxFotos);
        Assert.Equal(10, _api.ArchivosDe(id));
        Assert.Equal(10, (await FotosDetalle(id)).Length);

        // Tras borrar una, se puede volver a subir (queda con orden 10).
        Assert.Equal(HttpStatusCode.NoContent, (await _api.BorrarFoto(_api.A.Token, id, fotos[4].GetProperty("id").GetInt64())).StatusCode);
        var nueva = await _api.FotoSubida(_api.A.Token, id);
        Assert.Equal(10, nueva.GetProperty("orden").GetInt32());
        Assert.False(nueva.GetProperty("esPortada").GetBoolean());
    }

    [Fact(DisplayName = "HU-014: borrar renumera 1..n, conserva los id, borra el archivo y, si era la portada, la nueva portada es la primera")]
    public async Task Borrar_renumera()
    {
        var (id, inicial) = await _api.CrearConFoto(_api.A);
        var f = new List<JsonElement> { inicial };
        for (var i = 1; i < 4; i++) f.Add(await _api.FotoSubida(_api.A.Token, id));
        long Id(int i) => f[i].GetProperty("id").GetInt64();
        string Ruta(int i) => _api.RutaFisica(f[i].GetProperty("url").GetString()!);

        // Borrar una del medio.
        var r = await _api.BorrarFoto(_api.A.Token, id, Id(2));
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        Assert.Equal("", await r.Content.ReadAsStringAsync());
        Assert.False(File.Exists(Ruta(2)));
        var tras1 = await FotosDetalle(id);
        Assert.Equal(new[] { Id(0), Id(1), Id(3) }, tras1.Select(x => x.GetProperty("id").GetInt64()));
        Assert.Equal(new[] { 1, 2, 3 }, tras1.Select(x => x.GetProperty("orden").GetInt32()));
        Assert.Equal(new[] { true, false, false }, tras1.Select(x => x.GetProperty("esPortada").GetBoolean()));

        // Borrar la portada.
        Assert.Equal(HttpStatusCode.NoContent, (await _api.BorrarFoto(_api.A.Token, id, Id(0))).StatusCode);
        Assert.False(File.Exists(Ruta(0)));
        var tras2 = await FotosDetalle(id);
        Assert.Equal(new[] { Id(1), Id(3) }, tras2.Select(x => x.GetProperty("id").GetInt64()));
        Assert.Equal(new[] { 1, 2 }, tras2.Select(x => x.GetProperty("orden").GetInt32()));
        Assert.Equal(new[] { true, false }, tras2.Select(x => x.GetProperty("esPortada").GetBoolean()));
        Assert.True(File.Exists(Ruta(1)) && File.Exists(Ruta(3)));
        Assert.Equal(2, _api.ArchivosDe(id));

        // La URL de la foto borrada ya no se sirve.
        Assert.Equal(HttpStatusCode.NotFound, (await _api.C.GetAsync(f[0].GetProperty("url").GetString())).StatusCode);

        // Borrar la misma otra vez o una inexistente -> 404 "Foto no encontrada.".
        await AyudasP2.AfirmarError(await _api.BorrarFoto(_api.A.Token, id, Id(0)), HttpStatusCode.NotFound, MsgFotoNoEncontrada);
        await AyudasP2.AfirmarError(await _api.BorrarFoto(_api.A.Token, id, 987_654), HttpStatusCode.NotFound, MsgFotoNoEncontrada);
        // Vehículo inexistente -> 404 "Vehículo no encontrado.".
        await AyudasP2.AfirmarError(await _api.BorrarFoto(_api.A.Token, 987_654, Id(1)), HttpStatusCode.NotFound, MsgNoEncontrado);
    }

    [Fact(DisplayName = "D-30: borrar la única foto -> 409 \"El vehículo debe tener al menos una foto...\"; la foto y el archivo siguen")]
    public async Task Borrar_unica_409()
    {
        var (id, foto) = await _api.CrearConFoto(_api.A);
        var fotoId = foto.GetProperty("id").GetInt64();
        await AyudasP2.AfirmarError(await _api.BorrarFoto(_api.A.Token, id, fotoId), HttpStatusCode.Conflict, MsgUltimaFoto);
        await AyudasP2.AfirmarError(await _api.BorrarFoto(_api.Admin.Token, id, fotoId), HttpStatusCode.Conflict, MsgUltimaFoto);
        Assert.Equal(new[] { fotoId }, (await FotosDetalle(id)).Select(x => x.GetProperty("id").GetInt64()));
        Assert.True(File.Exists(_api.RutaFisica(foto.GetProperty("url").GetString()!)));
        Assert.Equal(1, _api.ArchivosDe(id));

        // Con una segunda foto, la primera sí se puede borrar; la que queda pasa a ser la portada y ya no se puede borrar.
        var segunda = await _api.FotoSubida(_api.A.Token, id);
        Assert.Equal(HttpStatusCode.NoContent, (await _api.BorrarFoto(_api.A.Token, id, fotoId)).StatusCode);
        var quedan = await FotosDetalle(id);
        Assert.Equal(segunda.GetProperty("id").GetInt64(), Assert.Single(quedan).GetProperty("id").GetInt64());
        Assert.True(quedan[0].GetProperty("esPortada").GetBoolean());
        await AyudasP2.AfirmarError(await _api.BorrarFoto(_api.A.Token, id, segunda.GetProperty("id").GetInt64()),
            HttpStatusCode.Conflict, MsgUltimaFoto);
        Assert.Equal(1, _api.ArchivosDe(id));
    }

    [Fact(DisplayName = "HU-014: reordenar -> 200 con la lista en el orden nuevo (1..n, conserva id y URL); la primera pasa a ser la portada")]
    public async Task Reordenar()
    {
        var (id, inicial) = await _api.CrearConFoto(_api.A);
        var f = new List<JsonElement> { inicial };
        for (var i = 1; i < 3; i++) f.Add(await _api.FotoSubida(_api.A.Token, id));
        var ids = f.Select(x => x.GetProperty("id").GetInt64()).ToArray();
        var urls = f.ToDictionary(x => x.GetProperty("id").GetInt64(), x => x.GetProperty("url").GetString());
        var nuevo = new[] { ids[2], ids[0], ids[1] };

        var json = await Json(await _api.Ordenar(_api.A.Token, id, new { fotoIds = nuevo }), HttpStatusCode.OK);
        var lista = json.EnumerateArray().ToArray();
        Assert.Equal(nuevo, lista.Select(x => x.GetProperty("id").GetInt64()));
        Assert.Equal(new[] { 1, 2, 3 }, lista.Select(x => x.GetProperty("orden").GetInt32()));
        Assert.Equal(new[] { true, false, false }, lista.Select(x => x.GetProperty("esPortada").GetBoolean()));
        Assert.All(lista, x => Assert.Equal(urls[x.GetProperty("id").GetInt64()], x.GetProperty("url").GetString()));
        Assert.All(lista, x => Assert.True(File.Exists(_api.RutaFisica(x.GetProperty("url").GetString()!))));

        // El detalle y la portada de la lista reflejan el orden nuevo.
        Assert.Equal(nuevo, (await FotosDetalle(id)).Select(x => x.GetProperty("id").GetInt64()));
        var item = (await Json(await _api.Lista(_api.A.Token, "?tamano=100"), HttpStatusCode.OK)).GetProperty("items")
            .EnumerateArray().Single(x => x.GetProperty("id").GetInt64() == id);
        Assert.Equal(urls[ids[2]], item.GetProperty("fotoPortada").GetString());

        // Una subida posterior toma el siguiente orden libre.
        Assert.Equal(4, (await _api.FotoSubida(_api.A.Token, id)).GetProperty("orden").GetInt32());
    }

    [Theory(DisplayName = "HU-014: reordenar con ids incompletos, repetidos, ajenos, de más o sin cuerpo -> 400 errores.fotoIds sin cambios")]
    [InlineData("incompleto")]
    [InlineData("repetido")]
    [InlineData("ajeno")]
    [InlineData("de-mas")]
    [InlineData("vacio")]
    [InlineData("nulo")]
    public async Task Reordenar_invalido(string caso)
    {
        var (id, inicial) = await _api.CrearConFoto(_api.A);
        var ids = new List<long> { inicial.GetProperty("id").GetInt64() };
        for (var i = 1; i < 3; i++) ids.Add((await _api.FotoSubida(_api.A.Token, id)).GetProperty("id").GetInt64());
        var (_, fotoOtro) = await _api.CrearConFoto(_api.A);
        var ajena = fotoOtro.GetProperty("id").GetInt64();
        object cuerpo = caso switch
        {
            "incompleto" => new { fotoIds = new[] { ids[1], ids[0] } },
            "repetido" => new { fotoIds = new[] { ids[1], ids[1], ids[0] } },
            "ajeno" => new { fotoIds = new[] { ids[0], ids[1], ajena } },
            "de-mas" => new { fotoIds = new[] { ids[0], ids[1], ids[2], ajena } },
            "vacio" => new { fotoIds = Array.Empty<long>() },
            _ => new { }
        };
        await Afirmar400(await _api.Ordenar(_api.A.Token, id, cuerpo), "fotoIds", MsgOrdenFotos);
        Assert.Equal(ids, (await FotosDetalle(id)).Select(x => x.GetProperty("id").GetInt64()));
        Assert.Equal(new[] { true, false, false }, (await FotosDetalle(id)).Select(x => x.GetProperty("esPortada").GetBoolean()));
    }

    [Fact(DisplayName = "HU-014: reordenar un vehículo sin fotos (anterior a D-30) con una lista vacía -> 200 []; vehículo inexistente -> 404")]
    public async Task Reordenar_sin_fotos()
    {
        var id = await _api.CrearId(_api.A);
        await _api.QuitarFotosDb(id);
        var json = await Json(await _api.Ordenar(_api.A.Token, id, new { fotoIds = Array.Empty<long>() }), HttpStatusCode.OK);
        Assert.Equal(0, json.GetArrayLength());
        await AyudasP2.AfirmarError(await _api.Ordenar(_api.A.Token, 987_654, new { fotoIds = Array.Empty<long>() }),
            HttpStatusCode.NotFound, MsgNoEncontrado);
    }

    [Fact(DisplayName = "HU-014: subir a un vehículo inexistente -> 404 y no deja archivo")]
    public async Task Subir_inexistente_404()
    {
        await AyudasP2.AfirmarError(await _api.SubirFoto(_api.A.Token, 987_654, Jpeg()), HttpStatusCode.NotFound, MsgNoEncontrado);
        Assert.Equal(0, _api.ArchivosDe(987_654));
    }

    [Fact(DisplayName = "HU-014: las fotos se pueden gestionar con el vehículo RESERVADO o INACTIVO (solo VENDIDO lo bloquea)")]
    public async Task Fotos_en_reservado_e_inactivo()
    {
        var id = await _api.CrearId(_api.A);
        await Json(await _api.Accion(_api.A.Token, id, "reservar"), HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.Created, (await _api.SubirFoto(_api.A.Token, id, Jpeg())).StatusCode);
        await Json(await _api.Accion(_api.A.Token, id, "inactivar", "Motivo de prueba"), HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.Created, (await _api.SubirFoto(_api.A.Token, id, Png())).StatusCode);
    }

    [Fact(DisplayName = "HU-014: el detalle no expone la ruta física ni la carpeta de Media:Ruta")]
    public async Task Sin_ruta_fisica()
    {
        var id = await _api.CrearId(_api.A);
        await _api.FotoSubida(_api.A.Token, id);
        var texto = await (await _api.Detalle(_api.A.Token, id)).Content.ReadAsStringAsync();
        Assert.DoesNotContain(Path.GetFileName(_api.F.CarpetaMedia), texto);
        Assert.DoesNotContain("archivoClave", texto, StringComparison.OrdinalIgnoreCase);
    }
}
