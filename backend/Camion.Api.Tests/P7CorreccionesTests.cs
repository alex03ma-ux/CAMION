using System.Net;
using System.Text.Json;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP7;

namespace Camion.Api.Tests;

/// <summary>
/// P7 — correcciones de REVISION-SEGURIDAD-P7 (B-1 a B-4) y de P7-evidencia (O-5, CA-P7-46). SQLite en memoria; nunca MariaDB.
/// Las pruebas que guardan parámetros o cuentan suscripciones usan su propia API.
/// </summary>
public sealed class P7CorreccionesTests(P7Fixture fixture) : IClassFixture<P7Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    public static TheoryData<string> CorreosInvalidos => new()
    {
        "a@b.co?bcc=otro@x.co", "a&b@x.co", "a;b@x.co", "a,b@x.co", "a:b@x.co", "a/b@x.co", "a<b@x.co", "a>b@x.co",
        "a\"b@x.co", "a'b@x.co", "a%0Ab@x.co", "a b@x.co", "a@x.co/", "a@x..co", "a@x", "@x.co"
    };

    [Theory(DisplayName = "B-2: el correo del portal rechaza ? & ; , : / < > \" ' % y espacios (alterarían el mailto:)")]
    [MemberData(nameof(CorreosInvalidos))]
    public async Task Correo_estricto(string correo)
    {
        await Error400(await _api.GuardarContacto(CuerpoContacto(correo: correo)), "correo", MsgCorreo);
        Assert.Equal("vc.automotores@gmail.com", (await _api.ContactoPublico()).GetProperty("correo").GetString());
    }

    [Fact(DisplayName = "B-2: un correo normal con punto, guion y + se acepta")]
    public async Task Correo_valido()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var json = await Esperar(await api.GuardarContacto(CuerpoContacto(correo: "Ventas.Camion+1@empresa-x.com.co")),
            HttpStatusCode.OK);
        Assert.Equal("ventas.camion+1@empresa-x.com.co", json.GetProperty("correo").GetString());
    }

    [Fact(DisplayName = "B-1: un valor guardado que ya no es válido no se devuelve; se usa el respaldo o null, campo a campo")]
    public async Task Revalida_al_leer()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        api.Hora(T);
        var (asesor, _, slug) = await api.AsesorPublicado("Asesora Revalida Ficticia");
        await api.Db(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == asesor.UsuarioId)).PhoneNumber = "+576044441234";
            db.ConfiguracionesSistema.Add(new ConfiguracionSistema
            {
                Clave = "portal.contacto",
                ValorJson = JsonSerializer.Serialize(new
                {
                    whatsapp = "3166152907?text=hola",
                    correo = "a@b.co?bcc=otro@x.co",
                    horario = new[] { "<b>Lunes</b>" },
                    instagram = "javascript:alert(1)",
                    tiktok = "https://www.tiktok.com/@cuenta"
                })
            });
        });

        var publico = await api.ContactoPublico();
        Assert.Equal("3166152907", publico.GetProperty("whatsapp").GetString());
        Assert.Equal("https://wa.me/573166152907", publico.GetProperty("whatsappEnlace").GetString());
        Assert.Equal("vc.automotores@gmail.com", publico.GetProperty("correo").GetString());
        Assert.Equal(HorarioD42, Lista(publico, "horario"));
        Assert.Equal(JsonValueKind.Null, publico.GetProperty("instagram").ValueKind);
        Assert.Equal("https://www.tiktok.com/@cuenta", publico.GetProperty("tiktok").GetString());   // el válido se conserva
        // La ficha (D-47) tampoco usa el WhatsApp inválido.
        Assert.Equal("https://wa.me/573166152907",
            (await Esperar(await api.DetallePortal(slug), HttpStatusCode.OK)).GetProperty("whatsapp").GetString());
        var admin = await Esperar(await api.Enviar(HttpMethod.Get, "/api/admin/parametros/contacto", api.Admin.Token), HttpStatusCode.OK);
        Assert.Equal("vc.automotores@gmail.com", admin.GetProperty("correo").GetString());
    }

    public static TheoryData<string> ReferenciasControl => new() { "TRX\n001", "TRX\t001", "TRX\u0007001", "TRX\u0085001" };

    [Theory(DisplayName = "B-3: la referencia del pago rechaza saltos de línea y caracteres de control")]
    [MemberData(nameof(ReferenciasControl))]
    public async Task Referencia_sin_control(string referencia)
    {
        _api.Hora(T);
        var sus = (await Esperar(await _api.RegistrarPagoBase(), HttpStatusCode.Created)).GetProperty("id").GetInt64();
        var antes = await _api.TotalPagos();
        await Error400(await _api.RegistrarPago(sus, PagoValido(referencia: referencia)), "referencia",
            "La referencia no puede tener saltos de línea ni caracteres de control.");
        Assert.Equal(antes, await _api.TotalPagos());
    }

    [Fact(DisplayName = "B-3: el motivo de anulación rechaza caracteres de control y el pago no cambia")]
    public async Task Motivo_sin_control()
    {
        _api.Hora(T);
        var sus = (await Esperar(await _api.RegistrarPagoBase(PagoValido()), HttpStatusCode.Created)).GetProperty("id").GetInt64();
        var pago = Assert.Single(await _api.PagosDb(sus));
        await Error400(await _api.AnularPago(pago.Id, "Valor mal\nescrito (prueba)"), "motivo",
            "El motivo no puede tener saltos de línea ni caracteres de control.");
        Assert.Equal("REGISTRADO", Assert.Single(await _api.PagosDb(sus)).Estado);
    }

    [Fact(DisplayName = "B-4: anular un pago antiguo audita su estado real; sin fecha o medio -> 409")]
    public async Task Anular_audita_estado_real()
    {
        _api.Hora(T);
        var sus = (await Esperar(await _api.RegistrarPagoBase(), HttpStatusCode.Created)).GetProperty("id").GetInt64();
        long aprobado = 0, sinFecha = 0;
        await _api.Db(db =>
        {
            var a = new Pago { SuscripcionId = sus, ValorCop = 50_000, Estado = "APROBADO", FechaPago = new DateOnly(2026, 10, 1),
                Medio = Pago.Efectivo, CreadoEn = T };
            var b = new Pago { SuscripcionId = sus, ValorCop = 50_000, Estado = "PENDIENTE", CreadoEn = T };
            db.Pagos.AddRange(a, b);
            return db.SaveChangesAsync().ContinueWith(_ => (aprobado, sinFecha) = (a.Id, b.Id));
        });

        await Esperar(await _api.AnularPago(aprobado), HttpStatusCode.OK);
        var evento = Assert.Single(await _api.Auditoria("Pago", aprobado.ToString()));
        Assert.Equal("APROBADO", JsonDe(evento.DatosAnteriores)["estado"].GetString());

        await Error(await _api.AnularPago(sinFecha), HttpStatusCode.Conflict,
            "Este pago no tiene fecha o medio registrados: no se puede anular.");
        Assert.Equal("PENDIENTE", (await _api.PagosDb(sus)).Single(p => p.Id == sinFecha).Estado);
        Assert.Empty(await _api.Auditoria("Pago", sinFecha.ToString()));
    }

    [Fact(DisplayName = "O-5, CA-P7-46: vista=porVencer y vista=vencidasSinRenovar coinciden con los contadores del panel")]
    public async Task Vistas_coinciden_con_panel()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        // X: vence en 6 días al momento de contar. Y: vencida sin renovar. Z: una VENCIDA antigua y otra vigente.
        var (x, susX) = await api.AsesorConPlan("Asesor X Vistas Ficticio");
        api.Hora(T.AddDays(-10));
        var y = await api.CrearAsesor("Asesora Y Vistas Ficticia");
        var susY = (await api.Asignada(y.PerfilId, await api.PlanP4b())).GetProperty("id").GetInt64();
        api.Hora(T.AddDays(-40));
        var z = await api.CrearAsesor("Asesor Z Vistas Ficticio");
        var susZ = (await api.Asignada(z.PerfilId, await api.PlanP4b())).GetProperty("id").GetInt64();
        await api.Db(async db => (await db.Suscripciones.SingleAsync(s => s.Id == susZ)).Estado = Suscripcion.Vencida);
        api.Hora(T.AddDays(24));
        await api.Asignada(z.PerfilId, await api.PlanP4b());

        var panel = await Esperar(await api.Panel(), HttpStatusCode.OK);
        async Task<long[]> Vista(string q) => Ids(await Esperar(await api.Enviar(HttpMethod.Get,
            "/api/admin/suscripciones" + q, api.Admin.Token), HttpStatusCode.OK));

        Assert.Equal(new[] { susX }, await Vista("?vista=porVencer"));
        Assert.Equal(panel.GetProperty("suscripcionesPorVencer").GetInt32(), (await Vista("?vista=porVencer")).Length);
        Assert.Equal(new[] { susY }, await Vista("?vista=vencidasSinRenovar"));
        Assert.Equal(new[] { susY }, await Vista("?vista=VENCIDASSINRENOVAR"));
        Assert.Equal(panel.GetProperty("suscripcionesVencidas").GetInt32(), (await Vista("?vista=vencidasSinRenovar")).Length);
        // estado=VENCIDA sigue incluyendo la vencida ya reemplazada de Z.
        Assert.Equal(new[] { susY, susZ }.Order().ToArray(), (await Vista("?estado=VENCIDA")).Order().ToArray());
        // Se combina con los demás filtros.
        Assert.Empty(await Vista($"?vista=porVencer&asesorId={y.PerfilId}"));
        Assert.NotEqual(0, x.PerfilId);

        await Error400(await api.Enviar(HttpMethod.Get, "/api/admin/suscripciones?vista=todas", api.Admin.Token),
            "vista", "Vista no válida.", MsgFiltros);
    }
}

internal static class AyudasCorrecciones
{
    /// <summary>Asesor nuevo con un plan de precio y una suscripción asignada (con pago opcional); devuelve la suscripción (201).</summary>
    public static async Task<HttpResponseMessage> RegistrarPagoBase(this InventarioApi api, object? pago = null)
    {
        var asesor = await api.CrearAsesor(Unico("Asesor Correcciones"));
        var plan = await api.PlanConPrecio(100_000);
        return await api.AsignarConPago(asesor.PerfilId, plan, pago);
    }
}
