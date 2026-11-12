using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.InventarioApi;
using static Camion.Api.Tests.Infraestructura.InventarioJson;

namespace Camion.Api.Tests;

/// <summary>
/// P4a — POST /api/inventario/vehiculos/{id}/reservar|liberar|inactivar|reactivar (HU-020; RN-VE-004, RN-VE-008;
/// D-4, D-11, D-22). Contrato: docs/criterios/P4a-contrato-api.md, "Estados del vehículo".
/// </summary>
public sealed class InventarioEstadosTests(InventarioFixture fx) : IClassFixture<InventarioFixture>
{
    private readonly InventarioApi _api = fx.Api;
    private const string Motivo = "Vehículo en revisión mecánica";

    private async Task<long> VehiculoEn(string estado)
    {
        var id = await _api.CrearId(_api.A);
        switch (estado)
        {
            case "ACTIVO": break;
            case "RESERVADO": await Json(await _api.Accion(_api.A.Token, id, "reservar"), HttpStatusCode.OK); break;
            case "INACTIVO": await Json(await _api.Accion(_api.A.Token, id, "inactivar", Motivo), HttpStatusCode.OK); break;
            case "VENDIDO": await _api.MarcarVendido(id); break;
            default: throw new ArgumentException(estado);
        }
        return id;
    }

    [Theory(DisplayName = "HU-020 (D-4, D-11): transiciones válidas -> 200 con el detalle y el estado nuevo; auditoría con la acción en mayúsculas")]
    [InlineData("reservar", "ACTIVO", "RESERVADO")]
    [InlineData("liberar", "RESERVADO", "ACTIVO")]
    [InlineData("inactivar", "ACTIVO", "INACTIVO")]
    [InlineData("inactivar", "RESERVADO", "INACTIVO")]
    [InlineData("reactivar", "INACTIVO", "ACTIVO")]
    public async Task Transicion_valida(string accion, string desde, string hacia)
    {
        var id = await VehiculoEn(desde);
        var antes = (await _api.AuditoriaVehiculo(id)).Count;
        var json = await Json(await _api.Accion(_api.A.Token, id, accion, accion == "inactivar" ? Motivo : null), HttpStatusCode.OK);
        Assert.Equal(id, json.GetProperty("id").GetInt64());
        Assert.Equal(hacia, json.GetProperty("estado").GetString());
        Assert.Equal(hacia, (await _api.VehiculoDb(id)).Estado);

        var filas = await _api.AuditoriaVehiculo(id);
        Assert.Equal(antes + 1, filas.Count);
        var fila = filas[^1];
        Assert.Equal(accion.ToUpperInvariant(), fila.Accion);
        Assert.Equal(_api.A.UsuarioId, fila.UsuarioId);
        Assert.Equal(desde, JsonDocument.Parse(fila.DatosAnteriores!).RootElement.GetProperty("estado").GetString());
        var nuevos = JsonDocument.Parse(fila.DatosNuevos!).RootElement;
        Assert.Equal(hacia, nuevos.GetProperty("estado").GetString());
        if (accion == "inactivar") Assert.Equal(Motivo, nuevos.GetProperty("motivo").GetString());
    }

    [Theory(DisplayName = "HU-020: transiciones inválidas -> 409 \"No se puede <acción> un vehículo en estado <estado en minúscula>.\" sin cambios ni auditoría")]
    [InlineData("reservar", "RESERVADO")]
    [InlineData("reservar", "INACTIVO")]
    [InlineData("liberar", "ACTIVO")]
    [InlineData("liberar", "INACTIVO")]
    [InlineData("inactivar", "INACTIVO")]
    [InlineData("reactivar", "ACTIVO")]
    [InlineData("reactivar", "RESERVADO")]
    [InlineData("reservar", "VENDIDO")]
    [InlineData("liberar", "VENDIDO")]
    [InlineData("inactivar", "VENDIDO")]
    [InlineData("reactivar", "VENDIDO")]
    public async Task Transicion_invalida_409(string accion, string desde)
    {
        var id = await VehiculoEn(desde);
        var antes = (await _api.AuditoriaVehiculo(id)).Count;
        var r = await _api.Accion(_api.A.Token, id, accion, accion == "inactivar" ? Motivo : null);
        await AyudasP2.AfirmarError(r, HttpStatusCode.Conflict, $"No se puede {accion} un vehículo en estado {desde.ToLowerInvariant()}.");
        Assert.Equal(desde, (await _api.VehiculoDb(id)).Estado);
        Assert.Equal(antes, (await _api.AuditoriaVehiculo(id)).Count);
    }

    [Theory(DisplayName = "HU-020, RN-VE-008: inactivar exige motivo de 5 a 500 caracteres (400 errores.motivo)")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("abcd")]
    [InlineData("LARGO")]
    public async Task Inactivar_motivo_invalido(string? motivo)
    {
        if (motivo == "LARGO") motivo = new string('m', 501);
        var id = await VehiculoEn("ACTIVO");
        await Afirmar400(await _api.Accion(_api.A.Token, id, "inactivar", motivo), "motivo", "Escribe el motivo (5 a 500 caracteres).");
        Assert.Equal("ACTIVO", (await _api.VehiculoDb(id)).Estado);
    }

    [Fact(DisplayName = "HU-020: motivo con 5 y con 500 caracteres es válido; se guarda recortado y sale en el detalle")]
    public async Task Inactivar_motivo_limites()
    {
        var id5 = await VehiculoEn("ACTIVO");
        var j5 = await Json(await _api.Accion(_api.A.Token, id5, "inactivar", "  abcde  "), HttpStatusCode.OK);
        Assert.Equal("abcde", j5.GetProperty("motivoInactivacion").GetString());

        var id500 = await VehiculoEn("RESERVADO");
        var largo = new string('m', 500);
        var j500 = await Json(await _api.Accion(_api.A.Token, id500, "inactivar", largo), HttpStatusCode.OK);
        Assert.Equal(largo, j500.GetProperty("motivoInactivacion").GetString());
        Assert.Equal(largo, (await _api.VehiculoDb(id500)).MotivoInactivacion);
    }

    [Fact(DisplayName = "HU-020: reactivar borra el motivo de inactivación")]
    public async Task Reactivar_borra_motivo()
    {
        var id = await VehiculoEn("INACTIVO");
        Assert.Equal(Motivo, (await _api.VehiculoDb(id)).MotivoInactivacion);
        var json = await Json(await _api.Accion(_api.A.Token, id, "reactivar"), HttpStatusCode.OK);
        Assert.True(EsNulo(json, "motivoInactivacion"));
        Assert.Null((await _api.VehiculoDb(id)).MotivoInactivacion);
    }

    [Fact(DisplayName = "HU-020: en las acciones sin motivo obligatorio, un motivo de más de 500 caracteres -> 400")]
    public async Task Motivo_opcional_largo_400()
    {
        var id = await VehiculoEn("ACTIVO");
        await Afirmar400(await _api.Accion(_api.A.Token, id, "liberar", new string('m', 501)), "motivo",
            "El motivo no puede tener más de 500 caracteres.");
        Assert.Equal("ACTIVO", (await _api.VehiculoDb(id)).Estado);
    }

    [Fact(DisplayName = "HU-020: acción sobre un id inexistente -> 404 \"Vehículo no encontrado.\"")]
    public async Task Accion_inexistente_404()
    {
        foreach (var accion in new[] { "reservar", "liberar", "inactivar", "reactivar" })
            await AyudasP2.AfirmarError(await _api.Accion(_api.A.Token, 987_654, accion, Motivo), HttpStatusCode.NotFound, MsgNoEncontrado);
    }

    [Fact(DisplayName = "D-22: VENDIDO no se edita (409); sus fotos no se modifican (409); el detalle sigue disponible")]
    public async Task Vendido_bloqueado()
    {
        var (id, foto) = await _api.CrearConFoto(_api.A); // D-30: nace con su foto
        await _api.MarcarVendido(id);

        await AyudasP2.AfirmarError(await _api.Editar(_api.A.Token, id, Con(_api.CuerpoCamion(), ("kilometraje", 1))),
            HttpStatusCode.Conflict, MsgEditarVendido);
        await AyudasP2.AfirmarError(await _api.Editar(_api.Admin.Token, id, _api.CuerpoCamion()), HttpStatusCode.Conflict, MsgEditarVendido);
        await AyudasP2.AfirmarError(await _api.SubirFoto(_api.A.Token, id, Jpeg()), HttpStatusCode.Conflict, MsgFotosVendido);
        await AyudasP2.AfirmarError(await _api.BorrarFoto(_api.A.Token, id, foto.GetProperty("id").GetInt64()),
            HttpStatusCode.Conflict, MsgFotosVendido);
        await AyudasP2.AfirmarError(await _api.Ordenar(_api.A.Token, id, new { fotoIds = new[] { foto.GetProperty("id").GetInt64() } }),
            HttpStatusCode.Conflict, MsgFotosVendido);

        var v = await _api.VehiculoDb(id);
        Assert.Equal("VENDIDO", v.Estado);
        Assert.Equal(120_000, v.Kilometraje);
        Assert.Equal(1, _api.ArchivosDe(id));
        var detalle = await Json(await _api.Detalle(_api.A.Token, id), HttpStatusCode.OK);
        Assert.Equal("VENDIDO", detalle.GetProperty("estado").GetString());
        Assert.Single(detalle.GetProperty("fotos").EnumerateArray());
        Assert.DoesNotContain(await _api.AuditoriaVehiculo(id), a => a.Accion == "EDITAR");
    }

    [Fact(DisplayName = "D-22: no existe acción para marcar VENDIDO en P4a")]
    public async Task No_hay_accion_vender()
    {
        var id = await _api.CrearId(_api.A);
        var r = await _api.Accion(_api.A.Token, id, "vender");
        Assert.True(r.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"Llegó {(int)r.StatusCode}");
        Assert.Equal("ACTIVO", (await _api.VehiculoDb(id)).Estado);
    }

    [Fact(DisplayName = "HU-020: dos reservas simultáneas del mismo vehículo -> una 200 y la otra 409; una sola fila de auditoría")]
    public async Task Reserva_concurrente()
    {
        var id = await VehiculoEn("ACTIVO");
        var respuestas = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => _api.Accion(_api.A.Token, id, "reservar")));
        var codigos = respuestas.Select(r => (int)r.StatusCode).OrderBy(x => x).ToArray();
        Assert.True(codigos.Count(c => c == 200) == 1 && codigos.Count(c => c == 409) == 3, $"Códigos: {string.Join(",", codigos)}");
        Assert.Single(await _api.AuditoriaVehiculo(id), a => a.Accion == "RESERVAR");
    }

    [Theory(DisplayName = "D-28: reservar sin nota o con menos de 5 / más de 500 caracteres -> 400 en motivo; no cambia nada")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abcd")]
    public async Task Reservar_sin_nota_400(string nota)
    {
        var id = await VehiculoEn("ACTIVO");
        var r = await _api.Accion(_api.A.Token, id, "reservar", nota);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("\"motivo\"", await r.Content.ReadAsStringAsync());
        var v = await _api.VehiculoDb(id);
        Assert.Equal("ACTIVO", v.Estado);
        Assert.Null(v.NotaReserva);
    }

    [Fact(DisplayName = "D-28: la nota de reserva se guarda, sale en el detalle y en la lista, queda en la auditoría y se borra al liberar")]
    public async Task Nota_reserva_ciclo()
    {
        var id = await VehiculoEn("ACTIVO");
        var json = await Json(await _api.Accion(_api.A.Token, id, "reservar", "  Esperando crédito  "), HttpStatusCode.OK);
        Assert.Equal("Esperando crédito", json.GetProperty("notaReserva").GetString());
        Assert.Equal("Esperando crédito", (await _api.VehiculoDb(id)).NotaReserva);
        var lista = await Json(await _api.Lista(_api.A.Token), HttpStatusCode.OK);
        var item = lista.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetInt64() == id);
        Assert.Equal("Esperando crédito", item.GetProperty("notaReserva").GetString());
        var auditoria = await _api.Leer(db => Task.FromResult(db.AuditoriaEventos
            .Where(a => a.Entidad == "Vehiculo" && a.EntidadId == id.ToString() && a.Accion == "RESERVAR").Select(a => a.DatosNuevos).Single()));
        Assert.Equal("Esperando crédito", JsonDocument.Parse(auditoria!).RootElement.GetProperty("motivo").GetString());

        json = await Json(await _api.Accion(_api.A.Token, id, "liberar"), HttpStatusCode.OK);
        Assert.True(EsNulo(json, "notaReserva"));
        Assert.Null((await _api.VehiculoDb(id)).NotaReserva);
    }

    [Fact(DisplayName = "D-28: inactivar un vehículo reservado borra la nota de reserva (queda en la auditoría)")]
    public async Task Inactivar_borra_nota()
    {
        var id = await VehiculoEn("RESERVADO");
        await Json(await _api.Accion(_api.A.Token, id, "inactivar", Motivo), HttpStatusCode.OK);
        Assert.Null((await _api.VehiculoDb(id)).NotaReserva);
    }
}
