using System.Net;
using System.Text.Json;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.InventarioApi;

namespace Camion.Api.Tests;

/// <summary>
/// P4b — publicaciones: HU-023 crear (RN-CO-001, D-21, D-30), HU-024 editar, HU-025 inactivar, reactivar, HU-026 renovar,
/// eliminar (D-40), HU-028 vencimiento ligado al plan (D-41), HU-065 mis publicaciones, cupo (D-40), D-33 y el portal con
/// el precio y las fotos del vehículo. SQLite en memoria y reloj fijo (T); nunca MariaDB.
/// </summary>
public sealed class PublicacionesTests(P4bFixture fixture) : IClassFixture<P4bFixture>
{
    private readonly InventarioApi _api = fixture.Api;

    public static TheoryData<string, string> Rutas => new()
    {
        { "GET", "/api/publicaciones" },
        { "GET", "/api/publicaciones/cupo" },
        { "GET", "/api/publicaciones/1" },
        { "POST", "/api/publicaciones" },
        { "PUT", "/api/publicaciones/1" },
        { "POST", "/api/publicaciones/1/inactivar" },
        { "POST", "/api/publicaciones/1/reactivar" },
        { "POST", "/api/publicaciones/1/renovar" },
        { "POST", "/api/publicaciones/1/eliminar" },
        { "POST", "/api/publicaciones/1/destacar" },
        { "POST", "/api/publicaciones/1/quitar-destacado" },
    };

    [Theory(DisplayName = "RN-SE-006: /api/publicaciones es AsesorOAdministrador (401 sin token; 403 comprador)")]
    [MemberData(nameof(Rutas))]
    public async Task Permisos(string metodo, string ruta)
    {
        var m = new HttpMethod(metodo);
        object? cuerpo = metodo == "GET" ? null : new { vehiculoId = 1, titulo = "Título de prueba", motivo = MotivoInactivar };
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Enviar(m, ruta, null, cuerpo)).StatusCode);
        await Error(await _api.Enviar(m, ruta, _api.TokenComprador, cuerpo), HttpStatusCode.Forbidden, MsgSinPermiso);
    }

    [Fact(DisplayName = "HU-023: crear y publicar -> 201 PUBLICADA, slug único, precio y foto del vehículo, vigencia hasta el fin del plan, auditoría CREAR")]
    public async Task Crear_201()
    {
        var (asesor, susId) = await _api.AsesorConPlan("Asesora Publica Ficticia");
        var vehiculo = await _api.Creado(asesor.Token, _api.CuerpoCamion());
        var vehiculoId = IdDe(vehiculo);
        var json = await Esperar(await _api.Publicar(asesor.Token, vehiculoId, "  Chevrolet   NPR muy bien cuidado ",
            "Listo para trabajar", negociable: true), HttpStatusCode.Created);
        var id = IdDe(json);

        Assert.Equal(new[]
        {
            "actualizadoEn", "descripcionComercial", "destacada", "estado", "estadoEfectivo", "fotoPortada", "id",
            "motivoOcultamiento", "negociable", "precioCop", "publicadaEn", "slugPublico", "titulo", "vehiculoId",
            "vehiculoTitulo", "vigenteHasta"
        }, CatalogoJson.Claves(json));
        Assert.Equal(vehiculoId, json.GetProperty("vehiculoId").GetInt64());
        Assert.Equal("Chevrolet NPR muy bien cuidado", Texto(json, "titulo"));
        Assert.Equal("Chevrolet NPR 2019", Texto(json, "vehiculoTitulo"));
        Assert.Equal("PUBLICADA", Texto(json, "estado"));
        Assert.Equal("PUBLICADA", Texto(json, "estadoEfectivo"));
        Assert.True(json.GetProperty("negociable").GetBoolean());
        Assert.False(json.GetProperty("destacada").GetBoolean());
        Assert.Equal($"chevrolet-npr-2019-{id}", Slug(json));
        Assert.Equal(100_000_000, json.GetProperty("precioCop").GetInt64());
        Assert.Equal(vehiculo.GetProperty("fotos")[0].GetProperty("url").GetString(), Texto(json, "fotoPortada"));
        Assert.Equal(T.AddDays(30), Fecha(json, "vigenteHasta"));
        Assert.Equal(T, Fecha(json, "publicadaEn"));

        var vigencia = Assert.Single(await _api.VigenciasDb(id));
        Assert.Equal(susId, vigencia.SuscripcionId);
        Assert.Equal(T, vigencia.Inicio);
        Assert.Equal(T.AddDays(30), vigencia.Fin);
        Assert.Equal("PUBLICAR", vigencia.Motivo);
        var pub = await _api.PubDb(id);
        Assert.Equal(asesor.PerfilId, pub.AsesorId);
        Assert.Equal(100_000_000m, pub.PrecioCop);

        var audit = Assert.Single(await _api.Auditoria("Publicacion", id.ToString()));
        Assert.Equal("CREAR", audit.Accion);
        Assert.Equal(asesor.UsuarioId, audit.UsuarioId);
        Assert.Equal("PUBLICADA", JsonDe(audit.DatosNuevos)["estado"].GetString());

        // El portal la muestra con el precio y la foto del vehículo.
        Assert.Contains(Slug(json), await _api.SlugsPortal());
        var detalle = await Esperar(await _api.DetallePortal(Slug(json)), HttpStatusCode.OK);
        Assert.Equal(100_000_000, detalle.GetProperty("precioCop").GetInt64());
        Assert.StartsWith($"/media/vehiculos/{vehiculoId}/", detalle.GetProperty("fotos")[0].GetProperty("url").GetString());

        // El inventario indica la publicación del vehículo.
        var inv = await Esperar(await _api.Detalle(asesor.Token, vehiculoId), HttpStatusCode.OK);
        Assert.Equal(id, inv.GetProperty("publicacion").GetProperty("id").GetInt64());
        Assert.Equal("PUBLICADA", inv.GetProperty("publicacion").GetProperty("estadoEfectivo").GetString());
        var listaInv = await Esperar(await _api.Lista(asesor.Token), HttpStatusCode.OK);
        var fila = listaInv.GetProperty("items").EnumerateArray().Single(x => IdDe(x) == vehiculoId);
        Assert.Equal(id, fila.GetProperty("publicacion").GetProperty("id").GetInt64());
        Assert.Equal(Slug(json), fila.GetProperty("publicacion").GetProperty("slugPublico").GetString());
    }

    [Fact(DisplayName = "P4b: el portal toma el precio del vehículo; cambiarlo en el inventario cambia el precio publicado")]
    public async Task Portal_precio_del_vehiculo()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesor Precio Ficticio");
        var pub = await _api.Publicada(asesor);
        await Esperar(await _api.Editar(asesor.Token, pub.GetProperty("vehiculoId").GetInt64(),
            Sin(Con(_api.CuerpoCamion(), ("precioCop", 88_000_000)), "asesorId")), HttpStatusCode.OK);
        var tarjeta = (await _api.TarjetasPortal()).Single(t => t.GetProperty("slug").GetString() == Slug(pub));
        Assert.Equal(88_000_000, tarjeta.GetProperty("precioCop").GetInt64());
        Assert.Equal(88_000_000, (await Esperar(await _api.DetallePub(asesor.Token, IdDe(pub)), HttpStatusCode.OK))
            .GetProperty("precioCop").GetInt64());
    }

    [Fact(DisplayName = "RN-CO-001, D-21: sin plan vigente -> 409 \"No tienes un plan activo...\" y no se crea nada")]
    public async Task Crear_sin_plan_409()
    {
        _api.Hora(T);
        var asesor = await _api.CrearAsesor("Asesor Sin Plan Ficticio");
        var vehiculo = await _api.CrearId(asesor);
        await Error(await _api.Publicar(asesor.Token, vehiculo), HttpStatusCode.Conflict, MsgSinPlan);
        Assert.False(await _api.Leer(db => db.Publicaciones.AnyAsync(p => p.VehiculoId == vehiculo)));
        var cupo = await _api.Cupo(asesor.Token);
        Assert.False(cupo.GetProperty("planVigente").GetBoolean());
        Assert.Equal(0, cupo.GetProperty("limitePublicaciones").GetInt32());
        Assert.Equal(JsonValueKind.Null, cupo.GetProperty("finPlan").ValueKind);
    }

    [Theory(DisplayName = "HU-023: título (5 a 180), descripción (hasta 4000) y vehiculoId -> 400 en el campo")]
    [InlineData("titulo", "abcd")]
    [InlineData("titulo", "    ")]
    [InlineData("titulo", null)]
    [InlineData("titulo", "x181")]
    [InlineData("descripcionComercial", "x4001")]
    [InlineData("vehiculoId", null)]
    public async Task Crear_validacion_400(string campo, string? valor)
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesor Validación Ficticio");
        var vehiculo = await _api.CrearId(asesor);
        string? Texto(string? v) => v is "x181" ? new string('a', 181) : v is "x4001" ? new string('a', 4001) : v;
        var r = campo switch
        {
            "titulo" => await _api.Publicar(asesor.Token, vehiculo, Texto(valor)),
            "descripcionComercial" => await _api.Publicar(asesor.Token, vehiculo, descripcion: Texto(valor)),
            _ => await _api.Publicar(asesor.Token, null)
        };
        var mensaje = campo switch { "titulo" => MsgTitulo, "descripcionComercial" => MsgDescripcion, _ => MsgVehiculoId };
        await Error400(r, campo, mensaje);
        Assert.False(await _api.Leer(db => db.Publicaciones.AnyAsync(p => p.VehiculoId == vehiculo)));
    }

    [Fact(DisplayName = "RN-CO-002: el asesor no publica un vehículo ajeno (404); el administrador sí, a nombre del dueño")]
    public async Task Crear_ajeno_y_admin()
    {
        var (dueno, _) = await _api.AsesorConPlan("Asesor Dueño Ficticio");
        var (otro, _) = await _api.AsesorConPlan("Asesor Otro Ficticio");
        var vehiculo = await _api.CrearId(dueno);
        await Error(await _api.Publicar(otro.Token, vehiculo), HttpStatusCode.NotFound, MsgVehiculoNoEncontrado);
        await Error(await _api.Publicar(otro.Token, 999_999), HttpStatusCode.NotFound, MsgVehiculoNoEncontrado);

        var json = await Esperar(await _api.Publicar(_api.Admin.Token, vehiculo), HttpStatusCode.Created);
        Assert.Equal(dueno.PerfilId, json.GetProperty("asesor").GetProperty("id").GetInt64());
        Assert.Equal(dueno.PerfilId, (await _api.PubDb(IdDe(json))).AsesorId);
        // El dueño la ve; el otro asesor recibe 404 en detalle y no la ve en su lista.
        await Esperar(await _api.DetallePub(dueno.Token, IdDe(json)), HttpStatusCode.OK);
        await Error(await _api.DetallePub(otro.Token, IdDe(json)), HttpStatusCode.NotFound, MsgNoEncontrada);
        Assert.DoesNotContain(IdDe(json), await _api.IdsLista(otro.Token));
        Assert.Equal(1, (await _api.Cupo(dueno.Token)).GetProperty("usadas").GetInt32());
    }

    [Fact(DisplayName = "Una publicación no eliminada por vehículo: la segunda -> 409; tras eliminar la primera se puede volver a publicar")]
    public async Task Una_por_vehiculo()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesor Una Ficticio");
        var vehiculo = await _api.CrearId(asesor);
        var primera = await Esperar(await _api.Publicar(asesor.Token, vehiculo), HttpStatusCode.Created);
        await Error(await _api.Publicar(asesor.Token, vehiculo), HttpStatusCode.Conflict, MsgYaPublicado);
        await _api.Inactivada(asesor.Token, IdDe(primera));
        await Error(await _api.Publicar(asesor.Token, vehiculo), HttpStatusCode.Conflict, MsgYaPublicado);
        await _api.AccionPubOk(asesor.Token, IdDe(primera), "eliminar");
        var segunda = await Esperar(await _api.Publicar(asesor.Token, vehiculo), HttpStatusCode.Created);
        Assert.NotEqual(Slug(primera), Slug(segunda));
    }

    [Fact(DisplayName = "Requisitos para publicar: vehículo INACTIVO o VENDIDO, sin fotos (D-30) o sin precio -> 409")]
    public async Task Vehiculo_no_publicable_409()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesor Requisitos Ficticio");
        var inactivo = await _api.CrearId(asesor);
        await Esperar(await _api.Accion(asesor.Token, inactivo, "inactivar", "Fuera de servicio"), HttpStatusCode.OK);
        await Error(await _api.Publicar(asesor.Token, inactivo), HttpStatusCode.Conflict,
            "No se puede publicar un vehículo en estado inactivo.");
        var vendido = await _api.CrearId(asesor);
        await _api.MarcarVendido(vendido);
        await Error(await _api.Publicar(asesor.Token, vendido), HttpStatusCode.Conflict,
            "No se puede publicar un vehículo en estado vendido.");
        var sinFotos = await _api.CrearId(asesor);
        await _api.QuitarFotosDb(sinFotos);
        await Error(await _api.Publicar(asesor.Token, sinFotos), HttpStatusCode.Conflict, MsgSinFoto);
        var sinPrecio = await _api.CrearId(asesor);
        await _api.Db(async db => (await db.Vehiculos.SingleAsync(v => v.Id == sinPrecio)).PrecioCop = null);
        await Error(await _api.Publicar(asesor.Token, sinPrecio), HttpStatusCode.Conflict, MsgSinPrecio);
        // Un vehículo RESERVADO sí se publica.
        var reservado = await _api.CrearId(asesor);
        await Esperar(await _api.Accion(asesor.Token, reservado, "reservar"), HttpStatusCode.OK);
        await Esperar(await _api.Publicar(asesor.Token, reservado), HttpStatusCode.Created);
        Assert.Equal(1, (await _api.Cupo(asesor.Token)).GetProperty("usadas").GetInt32());
    }

    [Fact(DisplayName = "D-40: cuentan publicadas, inactivas y vendidas; al llegar al límite -> 409; solo eliminar libera cupo")]
    public async Task Cupo_d40()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesora Cupo Ficticia", limite: 3);
        var a = await _api.Publicada(asesor);
        var b = await _api.Publicada(asesor);
        var c = await _api.Publicada(asesor);
        var cuarto = await _api.CrearId(asesor);
        await Error(await _api.Publicar(asesor.Token, cuarto), HttpStatusCode.Conflict, MsgCupo(3));

        await _api.Inactivada(asesor.Token, IdDe(a));                                   // inactiva: cuenta
        await Esperar(await _api.Vender(asesor.Token, b.GetProperty("vehiculoId").GetInt64()), HttpStatusCode.OK); // vendida: cuenta
        await Error(await _api.Publicar(asesor.Token, cuarto), HttpStatusCode.Conflict, MsgCupo(3));
        var cupo = await _api.Cupo(asesor.Token);
        Assert.Equal(3, cupo.GetProperty("usadas").GetInt32());
        Assert.Equal(3, cupo.GetProperty("limitePublicaciones").GetInt32());
        Assert.True(cupo.GetProperty("planVigente").GetBoolean());
        Assert.Equal(T.AddDays(30), Fecha(cupo, "finPlan"));

        await _api.AccionPubOk(asesor.Token, IdDe(a), "eliminar");                       // libera
        Assert.Equal(2, (await _api.Cupo(asesor.Token)).GetProperty("usadas").GetInt32());
        await Esperar(await _api.Publicar(asesor.Token, cuarto), HttpStatusCode.Created);
        await Error(await _api.Publicar(asesor.Token, await _api.CrearId(asesor)), HttpStatusCode.Conflict, MsgCupo(3));
        await _api.AccionPubOk(asesor.Token, IdDe(b), "eliminar");                       // vendida eliminada: libera
        Assert.Equal(2, (await _api.Cupo(asesor.Token)).GetProperty("usadas").GetInt32());
        _ = c;
    }

    [Fact(DisplayName = "HU-024: editar título, descripción y negociable (auditoría solo de lo que cambió); el precio no se toca aquí")]
    public async Task Editar()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesor Editar Ficticio");
        var pub = await _api.Publicada(asesor);
        var id = IdDe(pub);
        var json = await Esperar(await _api.Enviar(HttpMethod.Put, $"/api/publicaciones/{id}", asesor.Token,
            new { titulo = "Título nuevo del camión", descripcionComercial = "Nueva descripción", negociable = false, precioCop = 1 }),
            HttpStatusCode.OK);
        Assert.Equal("Título nuevo del camión", Texto(json, "titulo"));
        Assert.Equal("Nueva descripción", Texto(json, "descripcionComercial"));
        Assert.Equal(100_000_000, json.GetProperty("precioCop").GetInt64());
        var editar = Assert.Single(await _api.Auditoria("Publicacion", id.ToString()), x => x.Accion == "EDITAR");
        Assert.Equal(new[] { "descripcionComercial", "titulo" }, JsonDe(editar.DatosNuevos).Keys.Order(StringComparer.Ordinal).ToArray());

        await Error400(await _api.Enviar(HttpMethod.Put, $"/api/publicaciones/{id}", asesor.Token, new { titulo = "abc" }),
            "titulo", MsgTitulo);
        // Se puede editar inactiva y vendida; ajena -> 404.
        await _api.Inactivada(asesor.Token, id);
        await Esperar(await _api.Enviar(HttpMethod.Put, $"/api/publicaciones/{id}", asesor.Token,
            new { titulo = "Título estando inactiva" }), HttpStatusCode.OK);
        await Error(await _api.Enviar(HttpMethod.Put, $"/api/publicaciones/{id}", _api.B.Token, new { titulo = "Título ajeno 123" }),
            HttpStatusCode.NotFound, MsgNoEncontrada);
        // Eliminada: el asesor ya no la ve (404); el administrador recibe 409.
        await _api.AccionPubOk(asesor.Token, id, "eliminar");
        await Error(await _api.Enviar(HttpMethod.Put, $"/api/publicaciones/{id}", asesor.Token, new { titulo = "Título eliminada" }),
            HttpStatusCode.NotFound, MsgNoEncontrada);
        await Error(await _api.Enviar(HttpMethod.Put, $"/api/publicaciones/{id}", _api.Admin.Token, new { titulo = "Título eliminada" }),
            HttpStatusCode.Conflict, MsgTransicion("editar", "eliminada"));
    }

    [Fact(DisplayName = "HU-025: inactivar exige motivo (400); PUBLICADA -> OCULTA, sale del portal; de nuevo -> 409")]
    public async Task Inactivar()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesora Inactivar Ficticia");
        var pub = await _api.Publicada(asesor);
        var id = IdDe(pub);
        await Error400(await _api.AccionPub(asesor.Token, id, "inactivar", new { motivo = "abc" }), "motivo",
            "Escribe el motivo (5 a 500 caracteres).");
        var largo = new string('m', 500);
        var json = await _api.AccionPubOk(asesor.Token, id, "inactivar", new { motivo = largo });
        Assert.Equal("OCULTA", Texto(json, "estado"));
        Assert.Equal(largo, Texto(json, "motivoOcultamiento"));
        Assert.Equal(JsonValueKind.Null, json.GetProperty("vigenteHasta").ValueKind);
        Assert.Equal(T, (await _api.PubDb(id)).OcultadaEn);
        Assert.DoesNotContain(Slug(pub), await _api.SlugsPortal());
        Assert.Equal(HttpStatusCode.NotFound, (await _api.DetallePortal(Slug(pub))).StatusCode);
        await Error(await _api.AccionPub(asesor.Token, id, "inactivar", new { motivo = MotivoInactivar }), HttpStatusCode.Conflict,
            MsgTransicion("inactivar", "oculta"));
        var audit = Assert.Single(await _api.Auditoria("Publicacion", id.ToString()), x => x.Accion == "INACTIVAR");
        Assert.Equal("PUBLICADA", JsonDe(audit.DatosAnteriores)["estado"].GetString());
        Assert.Equal("OCULTA", JsonDe(audit.DatosNuevos)["estado"].GetString());
    }

    [Fact(DisplayName = "Reactivar: OCULTA -> PUBLICADA con vigencia nueva y sin cupo nuevo; sin plan o vehículo inactivo -> 409")]
    public async Task Reactivar()
    {
        var (asesor, susId) = await _api.AsesorConPlan("Asesor Reactivar Ficticio", limite: 1);
        var pub = await _api.Publicada(asesor);
        var id = IdDe(pub);
        var vehiculo = pub.GetProperty("vehiculoId").GetInt64();
        await Error(await _api.AccionPub(asesor.Token, id, "reactivar"), HttpStatusCode.Conflict, MsgTransicion("reactivar", "publicada"));
        await _api.Inactivada(asesor.Token, id);

        // Vehículo inactivo: no se reactiva.
        await Esperar(await _api.Accion(asesor.Token, vehiculo, "inactivar", "Al taller unos días"), HttpStatusCode.OK);
        await Error(await _api.AccionPub(asesor.Token, id, "reactivar"), HttpStatusCode.Conflict,
            "No se puede publicar un vehículo en estado inactivo.");
        await Esperar(await _api.Accion(asesor.Token, vehiculo, "reactivar"), HttpStatusCode.OK);

        // Plan suspendido: no se reactiva.
        await _api.AccionOk(susId, "suspender", new { motivo = MotivoValido });
        await Error(await _api.AccionPub(asesor.Token, id, "reactivar"), HttpStatusCode.Conflict, MsgSinPlan);
        await _api.AccionOk(susId, "reactivar");

        _api.Hora(T.AddDays(2));
        var json = await _api.AccionPubOk(asesor.Token, id, "reactivar");   // límite 1 y ya cuenta: no consume cupo nuevo
        Assert.Equal("PUBLICADA", Texto(json, "estadoEfectivo"));
        Assert.Equal(JsonValueKind.Null, json.GetProperty("motivoOcultamiento").ValueKind);
        var vigencias = await _api.VigenciasDb(id);
        Assert.Equal(2, vigencias.Count);
        Assert.Equal("REACTIVAR", vigencias[1].Motivo);
        Assert.Equal(T.AddDays(2), vigencias[1].Inicio);
        Assert.Equal(T.AddDays(30), vigencias[1].Fin);
        Assert.Contains(Slug(pub), await _api.SlugsPortal());
        Assert.Equal(1, (await _api.Cupo(asesor.Token)).GetProperty("usadas").GetInt32());
    }

    [Fact(DisplayName = "HU-028, D-41: vence sola al vencer el plan; renovar el plan la extiende sin tocarla; renovar la publicación exige plan")]
    public async Task Vigencia_ligada_al_plan()
    {
        var (asesor, susId) = await _api.AsesorConPlan("Asesora Vigencia Ficticia");
        var pub = await _api.Publicada(asesor);
        var id = IdDe(pub);
        var antes = (await _api.PubDb(id)).ActualizadoEn;

        _api.Hora(T.AddDays(30));                              // fin exacto del plan: vencida
        Assert.DoesNotContain(Slug(pub), await _api.SlugsPortal());
        var lista = await Esperar(await _api.ListaPub(asesor.Token), HttpStatusCode.OK);
        var item = lista.GetProperty("items").EnumerateArray().Single(x => IdDe(x) == id);
        Assert.Equal("PUBLICADA", Texto(item, "estado"));
        Assert.Equal("VENCIDA", Texto(item, "estadoEfectivo"));
        Assert.Equal(new[] { id }, await _api.IdsLista(asesor.Token, "?estado=VENCIDA"));
        Assert.Empty(await _api.IdsLista(asesor.Token, "?estado=PUBLICADA"));
        Assert.Equal("VENCIDA", (await Esperar(await _api.Detalle(asesor.Token, pub.GetProperty("vehiculoId").GetInt64()),
            HttpStatusCode.OK)).GetProperty("publicacion").GetProperty("estadoEfectivo").GetString());
        await Error(await _api.AccionPub(asesor.Token, id, "renovar"), HttpStatusCode.Conflict, MsgSinPlan);

        // El administrador renueva el plan (vencido: vuelve a ACTIVA desde ahora): la publicación vuelve a verse sola.
        await _api.AccionOk(susId, "renovar");
        Assert.Contains(Slug(pub), await _api.SlugsPortal());
        Assert.Single(await _api.VigenciasDb(id));
        Assert.Equal(antes, (await _api.PubDb(id)).ActualizadoEn);
        var detalle = await Esperar(await _api.DetallePub(asesor.Token, id), HttpStatusCode.OK);
        Assert.Equal("PUBLICADA", Texto(detalle, "estadoEfectivo"));
        Assert.Equal(T.AddDays(60), Fecha(detalle, "vigenteHasta"));

        // Renovar la publicación escribe una vigencia nueva hasta el fin del plan.
        _api.Hora(T.AddDays(31));
        var renovada = await _api.AccionPubOk(asesor.Token, id, "renovar");
        Assert.Equal("PUBLICADA", Texto(renovada, "estadoEfectivo"));
        var vigencias = await _api.VigenciasDb(id);
        Assert.Equal(2, vigencias.Count);
        Assert.Equal(("RENOVAR", T.AddDays(31), T.AddDays(60)), (vigencias[1].Motivo, vigencias[1].Inicio, vigencias[1].Fin));
        Assert.Single(await _api.Auditoria("Publicacion", id.ToString()), x => x.Accion == "RENOVAR");

        // Renovar el plan vigente suma al final: visible pasado el fin anterior.
        await _api.AccionOk(susId, "renovar");
        _api.Hora(T.AddDays(70));
        Assert.Contains(Slug(pub), await _api.SlugsPortal());
        // Renovar una OCULTA -> 409.
        await _api.Inactivada(asesor.Token, id);
        await Error(await _api.AccionPub(asesor.Token, id, "renovar"), HttpStatusCode.Conflict, MsgTransicion("renovar", "oculta"));
    }

    [Fact(DisplayName = "D-40: eliminar solo desde OCULTA o VENDIDA; borrado lógico; desaparece de Mis publicaciones; el admin la ve con estado=ELIMINADA")]
    public async Task Eliminar()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesor Eliminar Ficticio");
        var pub = await _api.Publicada(asesor);
        var id = IdDe(pub);
        await Error(await _api.AccionPub(asesor.Token, id, "eliminar"), HttpStatusCode.Conflict, MsgTransicion("eliminar", "publicada"));
        await _api.Inactivada(asesor.Token, id);
        var json = await Esperar(await _api.AccionPub(asesor.Token, id, "eliminar"), HttpStatusCode.OK);
        Assert.Equal("ELIMINADA", Texto(json, "estado"));

        var db = await _api.PubDb(id);
        Assert.Equal(Publicacion.Eliminada, db.EstadoPublicacion);
        Assert.Equal(T, db.EliminadaEn);
        Assert.Single(await _api.VigenciasDb(id));       // no se borra nada
        Assert.DoesNotContain(id, await _api.IdsLista(asesor.Token));
        Assert.Empty(await _api.IdsLista(asesor.Token, "?estado=ELIMINADA"));
        await Error(await _api.DetallePub(asesor.Token, id), HttpStatusCode.NotFound, MsgNoEncontrada);
        await Error(await _api.AccionPub(asesor.Token, id, "reactivar"), HttpStatusCode.NotFound, MsgNoEncontrada);
        Assert.Contains(id, await _api.IdsLista(_api.Admin.Token, $"?estado=ELIMINADA&asesorId={asesor.PerfilId}"));
        Assert.DoesNotContain(id, await _api.IdsLista(_api.Admin.Token, $"?asesorId={asesor.PerfilId}"));
        await Error(await _api.AccionPub(_api.Admin.Token, id, "eliminar"), HttpStatusCode.Conflict, MsgTransicion("eliminar", "eliminada"));
        Assert.Single(await _api.Auditoria("Publicacion", id.ToString()), x => x.Accion == "ELIMINAR");
        // El vehículo ya no muestra publicación en el inventario.
        Assert.True(InventarioJson.EsNulo(await Esperar(await _api.Detalle(asesor.Token, pub.GetProperty("vehiculoId").GetInt64()),
            HttpStatusCode.OK), "publicacion"));
    }

    [Fact(DisplayName = "HU-065: Mis publicaciones (solo lo suyo; el admin todo con 'asesor'), filtros estado, q, asesorId y paginación; filtros inválidos -> 400")]
    public async Task Lista()
    {
        var (a, _) = await _api.AsesorConPlan("Asesora Lista A Ficticia");
        var (b, _) = await _api.AsesorConPlan("Asesor Lista B Ficticio");
        var a1 = await _api.Publicada(a, "Furgón refrigerado de A");
        var a2 = await _api.Publicada(a, "Volqueta grande de A");
        var b1 = await _api.Publicada(b, "Furgón seco de B");
        await _api.Inactivada(a.Token, IdDe(a2));

        Assert.Equal(new[] { IdDe(a2), IdDe(a1) }, await _api.IdsLista(a.Token));
        var lista = await Esperar(await _api.ListaPub(a.Token), HttpStatusCode.OK);
        Assert.Equal(2, lista.GetProperty("total").GetInt32());
        Assert.All(lista.GetProperty("items").EnumerateArray(), x => Assert.False(x.TryGetProperty("asesor", out _)));
        Assert.Equal(new[] { IdDe(a2) }, await _api.IdsLista(a.Token, "?estado=oculta"));
        Assert.Equal(new[] { IdDe(a1) }, await _api.IdsLista(a.Token, "?estado=PUBLICADA"));
        Assert.Equal(new[] { IdDe(a1) }, await _api.IdsLista(a.Token, "?q=FURGÓN"));
        // asesorId lo ignora el asesor (solo ve lo suyo).
        Assert.Equal(new[] { IdDe(a2), IdDe(a1) }, await _api.IdsLista(a.Token, $"?asesorId={b.PerfilId}"));
        Assert.Equal(new[] { IdDe(a2) }, await _api.IdsLista(a.Token, "?tamano=1&pagina=1"));

        var admin = await Esperar(await _api.ListaPub(_api.Admin.Token, $"?asesorId={b.PerfilId}"), HttpStatusCode.OK);
        var item = Assert.Single(admin.GetProperty("items").EnumerateArray());
        Assert.Equal(IdDe(b1), IdDe(item));
        Assert.Equal(b.PerfilId, item.GetProperty("asesor").GetProperty("id").GetInt64());
        Assert.Equal(b.Nombre, item.GetProperty("asesor").GetProperty("nombre").GetString());

        await Error400(await _api.ListaPub(a.Token, "?estado=BORRADA"), "estado", "Estado no válido.", AyudasP4p.MsgFiltros);
        await Error400(await _api.ListaPub(a.Token, "?tamano=101"), "tamano", "Página o tamaño no válidos.", AyudasP4p.MsgFiltros);
        await Error400(await _api.ListaPub(a.Token, "?q=" + new string('q', 101)), "q",
            "La búsqueda no puede tener más de 100 caracteres.", AyudasP4p.MsgFiltros);
    }

    [Fact(DisplayName = "GET /api/publicaciones/cupo: el asesor ve el suyo; el admin indica asesorId (400 sin él, 404 si no existe)")]
    public async Task Cupo_endpoint()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesor Cupo Ficticio", limite: 4, destacados: 2);
        await _api.Publicada(asesor);
        var cupo = await _api.Cupo(asesor.Token);
        Assert.Equal(new[] { "destacadasUsadas", "finPlan", "limiteDestacados", "limitePublicaciones", "planVigente", "usadas" },
            CatalogoJson.Claves(cupo));
        Assert.Equal((4, 1, 2, 0), (cupo.GetProperty("limitePublicaciones").GetInt32(), cupo.GetProperty("usadas").GetInt32(),
            cupo.GetProperty("limiteDestacados").GetInt32(), cupo.GetProperty("destacadasUsadas").GetInt32()));
        var admin = await _api.Cupo(_api.Admin.Token, asesor.PerfilId);
        Assert.Equal(1, admin.GetProperty("usadas").GetInt32());
        await Error400(await _api.Enviar(HttpMethod.Get, "/api/publicaciones/cupo", _api.Admin.Token), "asesorId", "Elige un asesor.",
            AyudasP4p.MsgFiltros);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _api.Enviar(HttpMethod.Get, "/api/publicaciones/cupo?asesorId=999999", _api.Admin.Token)).StatusCode);
    }

    [Fact(DisplayName = "D-33: con la publicación PUBLICADA no se cambia la categoría (409); tras inactivarla, sí")]
    public async Task D33_categoria()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesora Categoría Ficticia");
        var pub = await _api.Publicada(asesor);
        var vehiculo = pub.GetProperty("vehiculoId").GetInt64();
        var r = await _api.Editar(asesor.Token, vehiculo, Sin(_api.CuerpoCarro(), "asesorId"));
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal(_api.Camion, (await _api.VehiculoDb(vehiculo)).TipoVehiculoId);
        await _api.Inactivada(asesor.Token, IdDe(pub));
        await Esperar(await _api.Editar(asesor.Token, vehiculo, Sin(_api.CuerpoCarro(), "asesorId")), HttpStatusCode.OK);
        Assert.Equal(_api.Carro, (await _api.VehiculoDb(vehiculo)).TipoVehiculoId);
    }
}
