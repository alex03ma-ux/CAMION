using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.InventarioApi;
using static Camion.Api.Tests.Infraestructura.InventarioJson;

namespace Camion.Api.Tests;

/// <summary>
/// P4a — permisos del inventario (RN-SE-006, D-7, RN-VE-007, HU-021, HU-022). El asesor ve y modifica solo lo suyo
/// (lo ajeno responde 404); el administrador todo; COMPRADOR 403; sin token 401; cuenta no ACTIVA 401.
/// </summary>
public sealed class InventarioPermisosTests(InventarioFixture fx) : IClassFixture<InventarioFixture>
{
    private readonly InventarioApi _api = fx.Api;

    /// <summary>Todas las rutas de /api/inventario con un cuerpo válido de ejemplo.</summary>
    private IEnumerable<(HttpMethod Metodo, string Url, object? Cuerpo)> Rutas(long id, long fotoId) =>
    [
        (HttpMethod.Get, "/api/inventario/vehiculos", null),
        (HttpMethod.Get, $"/api/inventario/vehiculos/{id}", null),
        (HttpMethod.Get, "/api/inventario/catalogos", null),
        (HttpMethod.Post, "/api/inventario/vehiculos", _api.CuerpoCamion(_api.A.PerfilId)),
        (HttpMethod.Put, $"/api/inventario/vehiculos/{id}", Con(_api.CuerpoCamion(), ("kilometraje", 1))),
        (HttpMethod.Post, $"/api/inventario/vehiculos/{id}/reservar", new { }),
        (HttpMethod.Post, $"/api/inventario/vehiculos/{id}/liberar", new { }),
        (HttpMethod.Post, $"/api/inventario/vehiculos/{id}/inactivar", new { motivo = "Motivo de prueba" }),
        (HttpMethod.Post, $"/api/inventario/vehiculos/{id}/reactivar", new { }),
        (HttpMethod.Post, $"/api/inventario/vehiculos/{id}/fotos", null),
        (HttpMethod.Delete, $"/api/inventario/vehiculos/{id}/fotos/{fotoId}", null),
        (HttpMethod.Put, $"/api/inventario/vehiculos/{id}/fotos/orden", new { fotoIds = new[] { fotoId } }),
    ];

    private async Task<HttpResponseMessage> Llamar(string? token, (HttpMethod Metodo, string Url, object? Cuerpo) ruta)
    {
        if (ruta.Url.EndsWith("/fotos", StringComparison.Ordinal))
            return await _api.SubirFoto(token, long.Parse(ruta.Url.Split('/')[4]), Jpeg());
        return await _api.Enviar(ruta.Metodo, ruta.Url, token, ruta.Cuerpo);
    }

    private async Task<(long Id, long FotoId)> VehiculoDeAConFoto()
    {
        var id = await _api.CrearId(_api.A);
        var foto = await _api.FotoSubida(_api.A.Token, id);
        return (id, foto.GetProperty("id").GetInt64());
    }

    private async Task<(int Vehiculos, int Fotos, int Auditoria, string Estado, int Km)> Huella(long id) =>
        await _api.Leer(async db =>
        {
            var v = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Vehiculos, x => x.Id == id);
            return (db.Vehiculos.Count(), db.FotosVehiculo.Count(), db.AuditoriaEventos.Count(), v.Estado, v.Kilometraje);
        });

    [Fact(DisplayName = "RN-SE-006: sin token -> 401 en todas las rutas de /api/inventario, sin cambios")]
    public async Task Sin_token_401()
    {
        var (id, fotoId) = await VehiculoDeAConFoto();
        var antes = await Huella(id);
        foreach (var ruta in Rutas(id, fotoId))
            Assert.True((await Llamar(null, ruta)).StatusCode == HttpStatusCode.Unauthorized, $"{ruta.Metodo} {ruta.Url}");
        Assert.Equal(antes, await Huella(id));
        Assert.Equal(1, _api.ArchivosDe(id));
    }

    [Fact(DisplayName = "RN-SE-006: COMPRADOR -> 403 { error } en todas las rutas de /api/inventario, sin cambios")]
    public async Task Comprador_403()
    {
        var (id, fotoId) = await VehiculoDeAConFoto();
        var antes = await Huella(id);
        foreach (var ruta in Rutas(id, fotoId))
            await AyudasP2.AfirmarError(await Llamar(_api.TokenComprador, ruta), HttpStatusCode.Forbidden, AyudasP2.MsgSinPermiso);
        Assert.Equal(antes, await Huella(id));
        Assert.Equal(1, _api.ArchivosDe(id));
    }

    [Fact(DisplayName = "D-7: un asesor no ve ni modifica el vehículo de otro asesor -> 404 \"Vehículo no encontrado.\" (no 403), sin cambios")]
    public async Task Asesor_ajeno_404()
    {
        var (id, fotoId) = await VehiculoDeAConFoto();
        var antes = await Huella(id);
        foreach (var ruta in Rutas(id, fotoId).Where(r => r.Url.Contains($"/{id}", StringComparison.Ordinal)))
            await AyudasP2.AfirmarError(await Llamar(_api.B.Token, ruta), HttpStatusCode.NotFound, MsgNoEncontrado);
        Assert.Equal(antes, await Huella(id));
        Assert.Equal(1, _api.ArchivosDe(id));

        // Un id inexistente responde exactamente igual (no se revela si existe).
        var inexistente = await _api.Detalle(_api.B.Token, 987_654);
        var ajeno = await _api.Detalle(_api.B.Token, id);
        Assert.Equal(await inexistente.Content.ReadAsStringAsync(), await ajeno.Content.ReadAsStringAsync());

        // Ni la lista ni la búsqueda por placa de B devuelven el vehículo de A.
        var lista = await Json(await _api.Lista(_api.B.Token, "?tamano=100"), HttpStatusCode.OK);
        Assert.DoesNotContain(lista.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetInt64() == id);
    }

    [Fact(DisplayName = "D-7: un asesor no puede borrar ni reordenar con la foto de otro vehículo (404 \"Foto no encontrada.\" / 400)")]
    public async Task Foto_de_otro_vehiculo()
    {
        var (idA, fotoA) = await VehiculoDeAConFoto();
        var idB = await _api.CrearId(_api.B);
        var fotoB = (await _api.FotoSubida(_api.B.Token, idB)).GetProperty("id").GetInt64();
        await AyudasP2.AfirmarError(await _api.BorrarFoto(_api.B.Token, idB, fotoA), HttpStatusCode.NotFound, MsgFotoNoEncontrada);
        await Afirmar400(await _api.Ordenar(_api.B.Token, idB, new { fotoIds = new[] { fotoA } }), "fotoIds", MsgOrdenFotos);
        Assert.Equal(1, _api.ArchivosDe(idA));
        Assert.Equal(new[] { fotoB }, (await Json(await _api.Detalle(_api.B.Token, idB), HttpStatusCode.OK))
            .GetProperty("fotos").EnumerateArray().Select(f => f.GetProperty("id").GetInt64()).ToArray());
        _ = idA;
    }

    [Fact(DisplayName = "HU-022, RN-AD-001: el administrador ve y modifica cualquier vehículo; la auditoría registra al administrador")]
    public async Task Admin_todo()
    {
        var (id, fotoId) = await VehiculoDeAConFoto();
        var d = await Json(await _api.Detalle(_api.Admin.Token, id), HttpStatusCode.OK);
        Assert.Equal(_api.A.PerfilId, d.GetProperty("asesor").GetProperty("id").GetInt64());
        Assert.Equal(_api.A.Nombre, d.GetProperty("asesor").GetProperty("nombre").GetString());

        Assert.Equal(HttpStatusCode.OK, (await _api.Editar(_api.Admin.Token, id, Con(_api.CuerpoCamion(), ("kilometraje", 5)))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.Accion(_api.Admin.Token, id, "reservar")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.Accion(_api.Admin.Token, id, "inactivar", "Motivo del administrador")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.Accion(_api.Admin.Token, id, "reactivar")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _api.SubirFoto(_api.Admin.Token, id, Png())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _api.BorrarFoto(_api.Admin.Token, id, fotoId)).StatusCode);

        var filas = await _api.AuditoriaVehiculo(id);
        Assert.All(filas.Where(f => f.Accion != "CREAR"), f => Assert.Equal(_api.Admin.Id, f.UsuarioId));
        Assert.Equal(new[] { "CREAR", "EDITAR", "RESERVAR", "INACTIVAR", "REACTIVAR" }, filas.Select(f => f.Accion).ToArray());
        // El dueño no cambia.
        Assert.Equal(_api.A.PerfilId, (await _api.VehiculoDb(id)).AsesorId);
    }

    [Fact(DisplayName = "RN-VE-007: el administrador crea indicando un asesorId de un asesor ACTIVA; el detalle trae el asesor")]
    public async Task Admin_crea_con_asesor_valido()
    {
        var json = await _api.Creado(_api.Admin.Token, _api.CuerpoCarro(_api.B.PerfilId));
        Assert.Equal(_api.B.PerfilId, json.GetProperty("asesor").GetProperty("id").GetInt64());
        Assert.Equal(_api.B.Nombre, json.GetProperty("asesor").GetProperty("nombre").GetString());
        var id = json.GetProperty("id").GetInt64();
        Assert.Equal(_api.B.PerfilId, (await _api.VehiculoDb(id)).AsesorId);
        Assert.Equal(_api.Admin.Id, Assert.Single(await _api.AuditoriaVehiculo(id)).UsuarioId);
        // El asesor dueño lo ve; el otro no.
        Assert.Equal(HttpStatusCode.OK, (await _api.Detalle(_api.B.Token, id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.Detalle(_api.A.Token, id)).StatusCode);
    }

    [Fact(DisplayName = "RN-VE-007: administrador sin asesorId, con uno inexistente, PENDIENTE, BLOQUEADO o con el id de usuario -> 400 errores.asesorId")]
    public async Task Admin_asesor_invalido()
    {
        var bloqueado = await _api.CrearAsesor("Asesor Bloqueado Ficticio");
        await AyudasP2.Ok(await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, bloqueado.UsuarioId, "bloquear", AyudasP2.MotivoValido));
        var antes = await _api.Leer(db => Task.FromResult(db.Vehiculos.Count()));

        await Afirmar400(await _api.Crear(_api.Admin.Token, _api.CuerpoCarro()), "asesorId", MsgAsesor);
        foreach (var asesorId in new long[] { 987_654, _api.Pendiente.PerfilId, bloqueado.PerfilId, 0, -1 })
            await Afirmar400(await _api.Crear(_api.Admin.Token, _api.CuerpoCarro(asesorId)), "asesorId", MsgAsesor);
        // El id de usuario (texto) no es un asesorId válido: el cuerpo no se puede leer -> 400.
        var r = await _api.Crear(_api.Admin.Token, Con(_api.CuerpoCarro(), ("asesorId", _api.A.UsuarioId)));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(antes, await _api.Leer(db => Task.FromResult(db.Vehiculos.Count())));
    }

    [Fact(DisplayName = "RN-SE-006: asesor que deja de estar ACTIVA (bloqueado) -> 401 con su token; PENDIENTE con token firmado -> 401")]
    public async Task Asesor_no_activa_401()
    {
        var asesor = await _api.CrearAsesor("Asesor Por Bloquear Ficticio");
        var id = await _api.CrearId(asesor);
        Assert.Equal(HttpStatusCode.OK, (await _api.Detalle(asesor.Token, id)).StatusCode); // control positivo

        await AyudasP2.Ok(await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, asesor.UsuarioId, "bloquear", AyudasP2.MotivoValido));
        foreach (var ruta in Rutas(id, 1))
            Assert.True((await Llamar(asesor.Token, ruta)).StatusCode == HttpStatusCode.Unauthorized, $"{ruta.Metodo} {ruta.Url}");
        Assert.Equal("ACTIVO", (await _api.VehiculoDb(id)).Estado);

        // PENDIENTE: no puede iniciar sesión; un token bien firmado con rol ASESOR tampoco sirve.
        var usuario = await AyudasP2.Usuario(_api.F, _api.Pendiente.Id);
        var firmado = AyudasP2.Firmar(_api.F, _api.Pendiente.Id, usuario.SessionVersion, "ASESOR");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Lista(firmado)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Crear(firmado, _api.CuerpoCarro())).StatusCode);
    }

    [Fact(DisplayName = "RN-SE-006: un COMPRADOR con un token que dice ASESOR o ADMINISTRADOR sin serlo no obtiene datos ajenos")]
    public async Task Token_con_rol_falso()
    {
        var (compradorId, email) = await AyudasP2.CrearComprador(_api.C);
        var u = await AyudasP2.Usuario(_api.F, compradorId);
        // Firmado con la clave real pero con rol ASESOR: no tiene perfil de asesor -> 403, nunca datos.
        var comoAsesor = AyudasP2.Firmar(_api.F, compradorId, u.SessionVersion, "ASESOR");
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.Lista(comoAsesor)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.Crear(comoAsesor, _api.CuerpoCarro())).StatusCode);
        _ = email;
    }

    // ---------------------------------------------------------------- catálogos del formulario

    [Fact(DisplayName = "P4a catálogos: solo entradas activas, marcas con líneas, departamentos con municipios; sin 'asesores' para el asesor")]
    public async Task Catalogos_asesor()
    {
        var json = await Json(await _api.Enviar(HttpMethod.Get, "/api/inventario/catalogos", _api.A.Token), HttpStatusCode.OK);
        Assert.Equal(new[] { "carrocerias", "combustibles", "departamentos", "marcas", "materiales", "tipos", "tiposMaquina" },
            CatalogoJson.Claves(json));
        string[] Nombres(string p) => json.GetProperty(p).EnumerateArray().Select(x => x.GetProperty("nombre").GetString()!).ToArray();
        Assert.Equal(new[] { "Camión", "Carro", "Carrocería", "Maquinaria" }, Nombres("tipos"));
        Assert.Equal(new[] { "Chevrolet", "Kenworth" }, Nombres("marcas"));
        Assert.Equal(new[] { "Estacas", "Furgón" }, Nombres("carrocerias"));
        Assert.Equal(new[] { "Aluminio" }, Nombres("materiales"));
        Assert.Equal(new[] { "Retroexcavadora" }, Nombres("tiposMaquina"));
        Assert.Equal(new[] { "Diésel" }, Nombres("combustibles"));
        var chevrolet = json.GetProperty("marcas").EnumerateArray().Single(m => m.GetProperty("nombre").GetString() == "Chevrolet");
        Assert.Equal(new[] { "NPR" }, chevrolet.GetProperty("lineas").EnumerateArray().Select(l => l.GetProperty("nombre").GetString()).ToArray());
        var antioquia = json.GetProperty("departamentos").EnumerateArray().Single(d => d.GetProperty("nombre").GetString() == "Antioquia");
        Assert.Equal(_api.Medellin, Assert.Single(antioquia.GetProperty("municipios").EnumerateArray()).GetProperty("id").GetInt64());
    }

    [Fact(DisplayName = "P4a catálogos: el administrador recibe además 'asesores' (solo ACTIVA, id = perfiles_asesor.id)")]
    public async Task Catalogos_admin_con_asesores()
    {
        var bloqueado = await _api.CrearAsesor("Asesor Oculto Ficticio");
        await AyudasP2.Ok(await AyudasP2.AccionMotivo(_api.C, _api.Admin.Token, bloqueado.UsuarioId, "bloquear", AyudasP2.MotivoValido));

        var json = await Json(await _api.Enviar(HttpMethod.Get, "/api/inventario/catalogos", _api.Admin.Token), HttpStatusCode.OK);
        var asesores = json.GetProperty("asesores").EnumerateArray()
            .Select(a => (a.GetProperty("id").GetInt64(), a.GetProperty("nombre").GetString())).ToArray();
        Assert.Contains((_api.A.PerfilId, _api.A.Nombre), asesores);
        Assert.Contains((_api.B.PerfilId, _api.B.Nombre), asesores);
        Assert.DoesNotContain(asesores, a => a.Item1 == _api.Pendiente.PerfilId || a.Item1 == bloqueado.PerfilId);
        // Ningún dato sensible del asesor (documento, correo, teléfono).
        var texto = json.GetProperty("asesores").GetRawText();
        Assert.DoesNotContain("@", texto);
        Assert.DoesNotContain("documento", texto, StringComparison.OrdinalIgnoreCase);
    }
}
