using System.Net;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;

namespace Camion.Api.Tests;

/// <summary>
/// P4b — reasignar un vehículo a otro asesor (D-34): solo el administrador, con motivo, a un asesor ACTIVA; si la
/// publicación cuenta (no eliminada, D-40), el destino necesita plan vigente con cupo. Se quitan los destacados y se
/// audita (quién, cuándo, de quién a quién y por qué). SQLite en memoria y reloj fijo; nunca MariaDB.
/// </summary>
public sealed class ReasignacionTests(P4bFixture fixture) : IClassFixture<P4bFixture>
{
    private readonly InventarioApi _api = fixture.Api;
    private const string MsgRevisar = "Revisa los datos marcados.";

    [Fact(DisplayName = "D-34: solo el administrador (401 sin token; 403 asesor dueño y comprador)")]
    public async Task Permisos()
    {
        _api.Hora(T);
        var vehiculo = await _api.CrearId(_api.A);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Reasignar(null, vehiculo, _api.B.PerfilId)).StatusCode);
        await Error(await _api.Reasignar(_api.A.Token, vehiculo, _api.B.PerfilId), HttpStatusCode.Forbidden, MsgSinPermiso);
        await Error(await _api.Reasignar(_api.TokenComprador, vehiculo, _api.B.PerfilId), HttpStatusCode.Forbidden, MsgSinPermiso);
        Assert.Equal(_api.A.PerfilId, (await _api.VehiculoDb(vehiculo)).AsesorId);
    }

    [Fact(DisplayName = "D-34: motivo (5 a 500) y asesor ACTIVA -> 400; vehículo inexistente -> 404; mismo asesor -> 409")]
    public async Task Validaciones()
    {
        _api.Hora(T);
        var vehiculo = await _api.CrearId(_api.A);
        await Error400(await _api.Reasignar(_api.Admin.Token, vehiculo, _api.B.PerfilId, "abc"), "motivo",
            "Escribe el motivo (5 a 500 caracteres).", MsgRevisar);
        await Error400(await _api.Reasignar(_api.Admin.Token, vehiculo, null), "asesorId", MsgAsesorActivo, MsgRevisar);
        await Error400(await _api.Reasignar(_api.Admin.Token, vehiculo, _api.Pendiente.PerfilId), "asesorId", MsgAsesorActivo, MsgRevisar);
        await Error400(await _api.Reasignar(_api.Admin.Token, vehiculo, 999_999), "asesorId", MsgAsesorActivo, MsgRevisar);
        await Error(await _api.Reasignar(_api.Admin.Token, 999_999, _api.B.PerfilId), HttpStatusCode.NotFound, MsgVehiculoNoEncontrado);
        await Error(await _api.Reasignar(_api.Admin.Token, vehiculo, _api.A.PerfilId), HttpStatusCode.Conflict, MsgMismoAsesor);
        Assert.DoesNotContain(await _api.AuditoriaVehiculo(vehiculo), a => a.Accion == "REASIGNAR");
    }

    [Fact(DisplayName = "D-34: sin publicación se reasigna aunque el destino no tenga plan; el vehículo pasa al inventario del destino; auditoría REASIGNAR")]
    public async Task Sin_publicacion()
    {
        _api.Hora(T);
        var vehiculo = await _api.CrearId(_api.A);
        var json = await Esperar(await _api.Reasignar(_api.Admin.Token, vehiculo, _api.B.PerfilId), HttpStatusCode.OK);
        Assert.Equal(_api.B.PerfilId, json.GetProperty("asesor").GetProperty("id").GetInt64());
        Assert.Equal(_api.B.PerfilId, (await _api.VehiculoDb(vehiculo)).AsesorId);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.Detalle(_api.A.Token, vehiculo)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.Detalle(_api.B.Token, vehiculo)).StatusCode);

        var audit = Assert.Single(await _api.AuditoriaVehiculo(vehiculo), a => a.Accion == "REASIGNAR");
        Assert.Equal(_api.Admin.Id, audit.UsuarioId);
        Assert.Equal(T, audit.CreadoEn);
        Assert.Equal(_api.A.PerfilId, JsonDe(audit.DatosAnteriores)["asesorId"].GetInt64());
        Assert.Equal(_api.B.PerfilId, JsonDe(audit.DatosNuevos)["asesorId"].GetInt64());
        Assert.Equal("Cambio de zona del asesor (prueba)", JsonDe(audit.DatosNuevos)["motivo"].GetString());
    }

    [Fact(DisplayName = "D-34 + D-40: con publicación, destino sin plan o sin cupo -> 409; con cupo pasa con su publicación, sin destacado y sigue en el portal")]
    public async Task Con_publicacion()
    {
        var (origen, _) = await _api.AsesorConPlan("Asesora Origen Ficticia", destacados: 1);
        var (lleno, _) = await _api.AsesorConPlan("Asesor Lleno Ficticio", limite: 1);
        var (destino, susDestino) = await _api.AsesorConPlan("Asesor Destino Ficticio", limite: 2);
        _api.Hora(T);
        var sinPlan = await _api.CrearAsesor("Asesor Destino Sin Plan Ficticio");
        await _api.Publicada(lleno);
        var pub = await _api.Publicada(origen);
        var vehiculo = pub.GetProperty("vehiculoId").GetInt64();
        await _api.AccionPubOk(origen.Token, IdDe(pub), "destacar");

        await Error(await _api.Reasignar(_api.Admin.Token, vehiculo, sinPlan.PerfilId), HttpStatusCode.Conflict, MsgDestinoSinPlan);
        await Error(await _api.Reasignar(_api.Admin.Token, vehiculo, lleno.PerfilId), HttpStatusCode.Conflict, MsgDestinoSinCupo(1));
        Assert.Equal(origen.PerfilId, (await _api.PubDb(IdDe(pub))).AsesorId);

        _api.Hora(T.AddHours(2));
        var json = await Esperar(await _api.Reasignar(_api.Admin.Token, vehiculo, destino.PerfilId), HttpStatusCode.OK);
        Assert.Equal(destino.PerfilId, json.GetProperty("asesor").GetProperty("id").GetInt64());
        Assert.False(json.GetProperty("publicacion").GetProperty("destacada").GetBoolean());
        var p = await _api.PubDb(IdDe(pub));
        Assert.Equal(destino.PerfilId, p.AsesorId);
        Assert.Equal(PublicacionDestacada.Finalizada, (await _api.DestacadosDb(IdDe(pub))).Single().Estado);
        var vigencias = await _api.VigenciasDb(IdDe(pub));
        Assert.Equal(("REASIGNAR", susDestino), (vigencias[^1].Motivo, vigencias[^1].SuscripcionId));

        Assert.Equal(0, (await _api.Cupo(origen.Token)).GetProperty("usadas").GetInt32());
        Assert.Equal(1, (await _api.Cupo(destino.Token)).GetProperty("usadas").GetInt32());
        Assert.DoesNotContain(IdDe(pub), await _api.IdsLista(origen.Token));
        Assert.Contains(IdDe(pub), await _api.IdsLista(destino.Token));
        Assert.Contains(Slug(pub), await _api.SlugsPortal());
        var audit = Assert.Single(await _api.AuditoriaVehiculo(vehiculo), a => a.Accion == "REASIGNAR");
        Assert.Equal(IdDe(pub), JsonDe(audit.DatosNuevos)["publicacionId"].GetInt64());
        Assert.True(JsonDe(audit.DatosNuevos)["destacadoQuitado"].GetBoolean());
    }

    [Fact(DisplayName = "D-40: una publicación ELIMINADA no cuenta: se reasigna a un destino sin plan y la eliminada queda con su dueño anterior")]
    public async Task Eliminada_no_cuenta()
    {
        var (origen, _) = await _api.AsesorConPlan("Asesor Origen Eliminada Ficticio");
        _api.Hora(T);
        var sinPlan = await _api.CrearAsesor("Asesora Sin Plan Eliminada Ficticia");
        var pub = await _api.Publicada(origen);
        await _api.Inactivada(origen.Token, IdDe(pub));
        await _api.AccionPubOk(origen.Token, IdDe(pub), "eliminar");
        var vehiculo = pub.GetProperty("vehiculoId").GetInt64();
        await Esperar(await _api.Reasignar(_api.Admin.Token, vehiculo, sinPlan.PerfilId), HttpStatusCode.OK);
        Assert.Equal(sinPlan.PerfilId, (await _api.VehiculoDb(vehiculo)).AsesorId);
        Assert.Equal(origen.PerfilId, (await _api.PubDb(IdDe(pub))).AsesorId);
    }
}
