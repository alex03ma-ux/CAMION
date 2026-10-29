using System.Net;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;

namespace Camion.Api.Tests;

/// <summary>
/// P4b — destacados según el plan (HU-027 modificada por D-38, RN-CO-004): el dueño o el administrador marcan y quitan,
/// sin pasar del límite a la vez y sin límite de cambios; se quitan al inactivar, vender, eliminar y reasignar, y dejan de
/// contar al vencer el plan. Planes con limiteDestacados (0 a 20). SQLite en memoria y reloj fijo; nunca MariaDB.
/// </summary>
public sealed class DestacadosTests(P4bFixture fixture) : IClassFixture<P4bFixture>
{
    private readonly InventarioApi _api = fixture.Api;

    [Fact(DisplayName = "D-38: límite 1 -> destacar otra da 409; quitar y destacar otra, tantas veces como se quiera; el portal la marca")]
    public async Task Limite_y_cambio_libre()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesora Destaca Ficticia", destacados: 1);
        var a = await _api.Publicada(asesor);
        var b = await _api.Publicada(asesor);

        var json = await _api.AccionPubOk(asesor.Token, IdDe(a), "destacar");
        Assert.True(json.GetProperty("destacada").GetBoolean());
        Assert.Contains(Slug(a), await _api.DestacadosPortal());
        var fila = Assert.Single(await _api.DestacadosDb(IdDe(a)));
        Assert.Equal((PublicacionDestacada.Activa, T, T.AddDays(30), asesor.UsuarioId), (fila.Estado, fila.Inicio, fila.Fin, fila.CreadoPor));
        // Ya destacada: 200 sin fila nueva.
        await _api.AccionPubOk(asesor.Token, IdDe(a), "destacar");
        Assert.Single(await _api.DestacadosDb(IdDe(a)));
        await Error(await _api.AccionPub(asesor.Token, IdDe(b), "destacar"), HttpStatusCode.Conflict, MsgDestacados(1));

        for (var i = 0; i < 3; i++)
        {
            var (quitar, poner) = i % 2 == 0 ? (a, b) : (b, a);
            Assert.False((await _api.AccionPubOk(asesor.Token, IdDe(quitar), "quitar-destacado")).GetProperty("destacada").GetBoolean());
            Assert.True((await _api.AccionPubOk(asesor.Token, IdDe(poner), "destacar")).GetProperty("destacada").GetBoolean());
            Assert.Equal(new[] { Slug(poner) }, (await _api.DestacadosPortal()).Where(s => s == Slug(a) || s == Slug(b)).ToArray());
        }
        var cupo = await _api.Cupo(asesor.Token);
        Assert.Equal((1, 1), (cupo.GetProperty("limiteDestacados").GetInt32(), cupo.GetProperty("destacadasUsadas").GetInt32()));

        // Quitar deja la fila FINALIZADA con fin = ahora (no se borra).
        var cerradas = (await _api.DestacadosDb(IdDe(a))).Where(d => d.Estado == PublicacionDestacada.Finalizada).ToList();
        Assert.NotEmpty(cerradas);
        Assert.All(cerradas, d => Assert.True(d.Fin > d.Inicio));
        // Quitar sin destacado: 200 sin cambios ni auditoría nueva.
        var antes = (await _api.Auditoria("Publicacion", IdDe(a).ToString())).Count;
        await _api.AccionPubOk(asesor.Token, IdDe(a), "quitar-destacado");
        Assert.Equal(antes, (await _api.Auditoria("Publicacion", IdDe(a).ToString())).Count);
        Assert.Contains(await _api.Auditoria("Publicacion", IdDe(b).ToString()), x => x.Accion == "DESTACAR");
        Assert.Contains(await _api.Auditoria("Publicacion", IdDe(b).ToString()), x => x.Accion == "QUITAR_DESTACADO");
    }

    [Fact(DisplayName = "D-38: un plan con 0 destacados -> 409 \"Tu plan permite 0 destacado(s)...\"")]
    public async Task Plan_sin_destacados()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesor Cero Destacados Ficticio", destacados: 0);
        var a = await _api.Publicada(asesor);
        await Error(await _api.AccionPub(asesor.Token, IdDe(a), "destacar"), HttpStatusCode.Conflict, MsgDestacados(0));
        Assert.Empty(await _api.DestacadosDb(IdDe(a)));
    }

    [Fact(DisplayName = "D-38: el administrador destaca la de un asesor (cuenta en su límite); otro asesor recibe 404")]
    public async Task Admin_y_ajeno()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesora Admin Destaca Ficticia", destacados: 1);
        var a = await _api.Publicada(asesor);
        var b = await _api.Publicada(asesor);
        await Error(await _api.AccionPub(_api.B.Token, IdDe(a), "destacar"), HttpStatusCode.NotFound, MsgNoEncontrada);
        await Error(await _api.AccionPub(_api.B.Token, IdDe(a), "quitar-destacado"), HttpStatusCode.NotFound, MsgNoEncontrada);
        await _api.AccionPubOk(_api.Admin.Token, IdDe(a), "destacar");
        await Error(await _api.AccionPub(asesor.Token, IdDe(b), "destacar"), HttpStatusCode.Conflict, MsgDestacados(1));
        Assert.Equal(_api.Admin.Id, (await _api.DestacadosDb(IdDe(a))).Single().CreadoPor);
    }

    [Fact(DisplayName = "Inactivar, vender y eliminar quitan el destacado (FINALIZADA) y deja de contar")]
    public async Task Se_quita_al_inactivar_vender_eliminar()
    {
        var (asesor, _) = await _api.AsesorConPlan("Asesor Quita Ficticio", destacados: 3);
        var a = await _api.Publicada(asesor);
        var b = await _api.Publicada(asesor);
        foreach (var p in new[] { a, b }) await _api.AccionPubOk(asesor.Token, IdDe(p), "destacar");
        Assert.Equal(2, (await _api.Cupo(asesor.Token)).GetProperty("destacadasUsadas").GetInt32());

        var inactiva = await _api.Inactivada(asesor.Token, IdDe(a));
        Assert.False(inactiva.GetProperty("destacada").GetBoolean());
        Assert.Equal(PublicacionDestacada.Finalizada, (await _api.DestacadosDb(IdDe(a))).Single().Estado);
        await Error(await _api.AccionPub(asesor.Token, IdDe(a), "destacar"), HttpStatusCode.Conflict, MsgTransicion("destacar", "oculta"));

        await Esperar(await _api.Vender(asesor.Token, b.GetProperty("vehiculoId").GetInt64()), HttpStatusCode.OK);
        Assert.Equal(PublicacionDestacada.Finalizada, (await _api.DestacadosDb(IdDe(b))).Single().Estado);
        Assert.Equal(0, (await _api.Cupo(asesor.Token)).GetProperty("destacadasUsadas").GetInt32());
        Assert.DoesNotContain(Slug(b), await _api.DestacadosPortal());

        // Eliminar también cierra un destacado que siguiera activo (fila sembrada para probarlo).
        await _api.Db(async db =>
        {
            db.PublicacionesDestacadas.Add(new PublicacionDestacada
            {
                PublicacionId = IdDe(a), Inicio = T, Fin = T.AddDays(5), Estado = PublicacionDestacada.Activa
            });
            await Task.CompletedTask;
        });
        await _api.AccionPubOk(asesor.Token, IdDe(a), "eliminar");
        Assert.All(await _api.DestacadosDb(IdDe(a)), d => Assert.Equal(PublicacionDestacada.Finalizada, d.Estado));
    }

    [Fact(DisplayName = "D-41: al vencer el plan el destacado deja de contar y de verse; renovar el plan lo extiende al nuevo fin")]
    public async Task Vence_con_el_plan_y_se_extiende()
    {
        var (asesor, susId) = await _api.AsesorConPlan("Asesora Destacado Plan Ficticia", destacados: 1);
        var a = await _api.Publicada(asesor);
        await _api.AccionPubOk(asesor.Token, IdDe(a), "destacar");

        // Renovar el plan vigente (+30 días): el destacado sigue pasado el fin anterior.
        await _api.AccionOk(susId, "renovar");
        Assert.Equal(T.AddDays(60), (await _api.DestacadosDb(IdDe(a))).Single().Fin);
        _api.Hora(T.AddDays(45));
        Assert.Contains(Slug(a), await _api.DestacadosPortal());
        Assert.Equal(1, (await _api.Cupo(asesor.Token)).GetProperty("destacadasUsadas").GetInt32());

        // Vence el plan: ni cuenta ni se ve, y destacar da 409 (estado vencida).
        _api.Hora(T.AddDays(60));
        Assert.DoesNotContain(Slug(a), await _api.DestacadosPortal());
        Assert.Equal(0, (await _api.Cupo(asesor.Token)).GetProperty("destacadasUsadas").GetInt32());
        var detalle = await Esperar(await _api.DetallePub(asesor.Token, IdDe(a)), HttpStatusCode.OK);
        Assert.False(detalle.GetProperty("destacada").GetBoolean());
        await Error(await _api.AccionPub(asesor.Token, IdDe(a), "destacar"), HttpStatusCode.Conflict, MsgTransicion("destacar", "vencida"));
    }

    [Theory(DisplayName = "P4p + D-38: limiteDestacados del plan de 0 a 20 -> fuera de rango 400")]
    [InlineData(-1)]
    [InlineData(21)]
    public async Task Plan_limite_destacados_400(int valor)
    {
        var cuerpo = CuerpoPlan();
        cuerpo["limiteDestacados"] = valor;
        await Error400(await _api.Enviar(HttpMethod.Post, "/api/admin/planes", _api.Admin.Token, cuerpo), "limiteDestacados",
            MsgLimiteDestacados);
    }

    [Fact(DisplayName = "P4p + D-38: crear con limiteDestacados lo guarda y lista; sin él vale 0; editar sin él lo conserva; auditoría")]
    public async Task Plan_limite_destacados()
    {
        _api.Hora(T);
        var id = await _api.PlanP4b(destacados: 20);
        var lista = await Esperar(await _api.Enviar(HttpMethod.Get, "/api/admin/planes", _api.Admin.Token), HttpStatusCode.OK);
        Assert.Equal(20, lista.GetProperty("items").EnumerateArray().Single(p => IdDe(p) == id).GetProperty("limiteDestacados").GetInt32());

        var editado = await Esperar(await _api.Enviar(HttpMethod.Put, $"/api/admin/planes/{id}", _api.Admin.Token,
            CuerpoPlan(limite: 9)), HttpStatusCode.OK);
        Assert.Equal(20, editado.GetProperty("limiteDestacados").GetInt32());
        var cuerpo = CuerpoPlan();
        cuerpo["limiteDestacados"] = 2;
        Assert.Equal(2, (await Esperar(await _api.Enviar(HttpMethod.Put, $"/api/admin/planes/{id}", _api.Admin.Token, cuerpo),
            HttpStatusCode.OK)).GetProperty("limiteDestacados").GetInt32());
        var edicion = (await _api.Auditoria("Plan", id.ToString())).Last(a => a.Accion == "EDITAR");
        Assert.Equal(20, JsonDe(edicion.DatosAnteriores)["limiteDestacados"].GetInt32());
        Assert.Equal(2, JsonDe(edicion.DatosNuevos)["limiteDestacados"].GetInt32());

        var sinCampo = await _api.CrearPlan();
        Assert.Equal(0, sinCampo.GetProperty("limiteDestacados").GetInt32());
    }
}
