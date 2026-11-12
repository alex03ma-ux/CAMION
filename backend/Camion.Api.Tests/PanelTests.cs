using System.Net;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP7;

namespace Camion.Api.Tests;

/// <summary>
/// P7 — panel del administrador (HU-075 parcial, D-64 a A; CA-P7-45 a CA-P7-51) y aviso de vencimiento en "Mi suscripción"
/// (HU-074 parcial, D-64 b A; CA-P7-50). Los contadores son globales: cada prueba de conteo usa su propia API.
/// SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class PanelTests(P7Fixture fixture) : IClassFixture<P7Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    [Fact(DisplayName = "CA-P7-49: panel -> 401 sin sesión; 403 asesor y comprador")]
    public async Task Permisos()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Panel(null)).StatusCode);
        foreach (var token in new[] { _api.A.Token, _api.TokenComprador })
            await Error(await _api.Panel(token), HttpStatusCode.Forbidden, MsgSinPermiso);
    }

    private static int Solicitud(JsonElement panel, string origen, string campo) =>
        panel.GetProperty("solicitudes").EnumerateArray().Single(s => s.GetProperty("origen").GetString() == origen)
            .GetProperty(campo).GetInt32();

    private static int Vehiculos(JsonElement panel, string estado) =>
        panel.GetProperty("vehiculosPorEstado").GetProperty(estado).GetInt32();

    [Fact(DisplayName = "CA-P7-45, 46, 47, 48: contadores operativos (asesores, solicitudes, suscripciones, publicaciones, vehículos, ventas)")]
    public async Task Contadores()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        api.Hora(T);

        var vacio = await Esperar(await api.Panel(), HttpStatusCode.OK);
        Assert.Equal(new[]
            {
                "asesoresPendientes", "publicacionesVisibles", "solicitudes", "suscripcionesPorVencer", "suscripcionesVencidas",
                "vehiculosPorEstado", "ventasDesde", "ventasUltimos30Dias"
            },
            vacio.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(1, vacio.GetProperty("asesoresPendientes").GetInt32());   // el asesor pendiente sembrado
        // CA-P7-47: los 5 orígenes y los 4 estados aparecen aunque estén en 0.
        Assert.Equal(new[] { "CONTACTO_VEHICULO", "VENDE_TU_CAMION", "SEGURO_TODO_RIESGO", "CREDITO", "BUSQUEDA" },
            vacio.GetProperty("solicitudes").EnumerateArray().Select(s => s.GetProperty("origen").GetString()).ToArray());
        Assert.Equal(new[] { "ACTIVO", "INACTIVO", "RESERVADO", "VENDIDO" },
            vacio.GetProperty("vehiculosPorEstado").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.All(vacio.GetProperty("vehiculosPorEstado").EnumerateObject(), p => Assert.Equal(0, p.Value.GetInt32()));
        Assert.Equal(0, vacio.GetProperty("ventasUltimos30Dias").GetInt32());

        // Datos: un asesor con plan (30 días) y una publicación visible con un contacto; un aviso; una venta vigente,
        // una revertida y un vehículo reservado.
        var (asesor, susId) = await api.AsesorConPlan("Asesora Panel Ficticia");
        var pub = await api.Publicada(asesor);
        await api.LeadNuevo(Slug(pub));
        await api.AvisoNuevo(AyudasP5b.RutaVende);
        await api.VehiculoVendido(asesor);
        var revertido = await api.VehiculoVendido(asesor);
        await Esperar(await api.RevertirVenta(asesor.Token, revertido), HttpStatusCode.OK);
        var reservado = await api.CrearId(asesor);
        await Esperar(await api.Accion(asesor.Token, reservado, "reservar", "Separado por un cliente (prueba)"), HttpStatusCode.OK);

        var panel = await Esperar(await api.Panel(), HttpStatusCode.OK);
        Assert.Equal(0, Solicitud(panel, "CONTACTO_VEHICULO", "abiertas"));   // es del asesor, no de Administración
        Assert.Equal((1, 1), (Solicitud(panel, "VENDE_TU_CAMION", "abiertas"), Solicitud(panel, "VENDE_TU_CAMION", "nuevas")));
        Assert.Equal(1, panel.GetProperty("publicacionesVisibles").GetInt32());
        Assert.Equal((2, 1, 0, 1), (Vehiculos(panel, "ACTIVO"), Vehiculos(panel, "RESERVADO"), Vehiculos(panel, "INACTIVO"),
            Vehiculos(panel, "VENDIDO")));
        Assert.Equal(1, panel.GetProperty("ventasUltimos30Dias").GetInt32());   // la revertida no cuenta
        Assert.Equal("2026-09-27", panel.GetProperty("ventasDesde").GetString());
        Assert.Equal((0, 0), (panel.GetProperty("suscripcionesPorVencer").GetInt32(), panel.GetProperty("suscripcionesVencidas").GetInt32()));
        // CA-P7-46: el contador de ventas coincide con la lista filtrada.
        var lista = await api.VentasOk(api.Admin.Token, $"?estado=VIGENTE&desde={panel.GetProperty("ventasDesde").GetString()}");
        Assert.Equal(1, lista.GetProperty("total").GetInt32());

        // CA-P7-48: solo números; ningún nombre, correo ni teléfono.
        var texto = await (await api.Panel()).Content.ReadAsStringAsync();
        foreach (var prohibido in new[] { asesor.Nombre, asesor.Email, "300", "@", "Cop" })
            Assert.DoesNotContain(prohibido, texto, StringComparison.Ordinal);

        // Faltan 6 días: por vencer.
        api.Hora(T.AddDays(24));
        panel = await Esperar(await api.Panel(), HttpStatusCode.OK);
        Assert.Equal((1, 0), (panel.GetProperty("suscripcionesPorVencer").GetInt32(), panel.GetProperty("suscripcionesVencidas").GetInt32()));
        Assert.Equal(1, panel.GetProperty("ventasUltimos30Dias").GetInt32());

        // Vencida: sale del portal, el contacto pasa a Administración (barrido D-46) y la venta queda fuera de los 30 días.
        api.Hora(T.AddDays(31));
        panel = await Esperar(await api.Panel(), HttpStatusCode.OK);
        Assert.Equal((0, 1), (panel.GetProperty("suscripcionesPorVencer").GetInt32(), panel.GetProperty("suscripcionesVencidas").GetInt32()));
        Assert.Equal(0, panel.GetProperty("publicacionesVisibles").GetInt32());
        Assert.Equal(1, Solicitud(panel, "CONTACTO_VEHICULO", "abiertas"));
        Assert.Equal(0, panel.GetProperty("ventasUltimos30Dias").GetInt32());

        // Renovada: ya no cuenta como vencida.
        await api.AccionOk(susId, "renovar");
        panel = await Esperar(await api.Panel(), HttpStatusCode.OK);
        Assert.Equal(0, panel.GetProperty("suscripcionesVencidas").GetInt32());

        // Aprobar al asesor pendiente baja el contador.
        await Esperar(await AyudasP2.AccionMotivo(api.C, api.Admin.Token, api.Pendiente.Id, "aprobar", null), HttpStatusCode.OK);
        Assert.Equal(0, (await Esperar(await api.Panel(), HttpStatusCode.OK)).GetProperty("asesoresPendientes").GetInt32());
    }

    [Fact(DisplayName = "CA-P7-50: Mi suscripción avisa 'Tu plan vence en N días' solo con 7 días o menos y plan vigente")]
    public async Task Aviso_vencimiento()
    {
        var (asesor, susId) = await _api.AsesorConPlan("Asesor Aviso Vence Ficticio");

        async Task<(bool Aviso, string? Mensaje)> Mi(DateTime hora)
        {
            _api.Hora(hora);
            var json = await Esperar(await _api.MiSuscripcion(asesor.Token), HttpStatusCode.OK);
            var mensaje = json.GetProperty("mensajeVencimiento");
            return (json.GetProperty("avisoVencimiento").GetBoolean(),
                mensaje.ValueKind == JsonValueKind.Null ? null : mensaje.GetString());
        }

        Assert.Equal((false, null), await Mi(T));
        Assert.Equal((false, null), await Mi(T.AddDays(22).AddHours(1)));   // 7,96 días -> 8
        Assert.Equal((true, "Tu plan vence en 7 días. Para renovarlo, comunícate con el administrador."), await Mi(T.AddDays(23)));
        Assert.Equal((true, "Tu plan vence en 1 día. Para renovarlo, comunícate con el administrador."),
            await Mi(T.AddDays(29).AddHours(1)));
        Assert.Equal((false, null), await Mi(T.AddDays(30)));   // vencida

        // Suspendida: no está vigente, sin aviso.
        _api.Hora(T.AddDays(25));
        var (otro, otraSus) = await _api.AsesorConPlan("Asesora Aviso Suspendida Ficticia");
        _api.Hora(T.AddDays(25));
        await _api.AccionOk(otraSus, "suspender", new { motivo = MotivoValido });
        var json = await Esperar(await _api.MiSuscripcion(otro.Token), HttpStatusCode.OK);
        Assert.False(json.GetProperty("avisoVencimiento").GetBoolean());
        Assert.NotEqual(0, susId);
        _api.Hora(T);
    }
}
