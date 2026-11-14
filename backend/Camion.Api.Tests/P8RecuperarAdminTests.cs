using System.Net;
using System.Net.Http.Json;
using Camion.Api.Auth;
using Camion.Api.Comandos;
using Camion.Api.Data;
using Camion.Api.Endpoints;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Camion.Api.Tests.Infraestructura.Ayudas;
using static Camion.Api.Tests.Infraestructura.AyudasP2;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;

namespace Camion.Api.Tests;

/// <summary>
/// D-79 (opción A), D-73 D, D-78: comando local `recuperar-admin &lt;correo&gt;` que genera el enlace de recuperación de una
/// cuenta de administrador. Mismo token (DataProtection, 60 minutos, un solo uso) con otro propósito: el enlace del panel no
/// sirve para un administrador y el del comando no sirve para otras cuentas. SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class P8RecuperarAdminTests(P7Fixture fixture) : IClassFixture<P7Fixture>
{
    private readonly InventarioApi _api = fixture.Api;
    private const string NuevaClave = "ClaveAdmin2027";

    // El comando se ejecuta con los servicios del host que atiende la API de prueba (mismo reloj y mismas claves).
    private static async Task<ResultadoRecuperarAdmin> Comando(InventarioApi api, string? correo)
    {
        using var scope = api.Servicios.CreateScope();
        var sp = scope.ServiceProvider;
        return await RecuperarAdmin.EjecutarAsync(correo, sp.GetRequiredService<UserManager<ApplicationUser>>(),
            sp.GetRequiredService<CamionDbContext>(), sp.GetRequiredService<TimeProvider>(), CamionApiFactory.UrlPortalPruebas);
    }

    private static string CodigoDe(string enlace) => enlace[(enlace.IndexOf("codigo=", StringComparison.Ordinal) + 7)..];

    private static Task<HttpResponseMessage> Restablecer(InventarioApi api, string codigo, string password = NuevaClave) =>
        api.C.PostAsJsonAsync("/api/auth/restablecer-contrasena", new { codigo, password });

    private static async Task<string> TokenDirecto(InventarioApi api, string usuarioId, string proposito)
    {
        using var scope = api.Servicios.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = (await users.FindByIdAsync(usuarioId))!;
        return await users.GenerateUserTokenAsync(u, ProveedorTokenRecuperacion.Nombre, proposito);
    }

    [Fact(DisplayName = "D-79: el comando genera el enlace de un administrador; sirve una vez, cierra sesiones y se audita como COMANDO_LOCAL")]
    public async Task Comando_genera_y_sirve()
    {
        _api.Hora(T);
        var admin = await CrearAdministrador(_api.F, _api.C);
        var sello = (await Usuario(_api.F, admin.Id)).SecurityStamp;
        var r = await Comando(_api, admin.Email.ToUpperInvariant());
        Assert.Equal(0, r.CodigoSalida);
        Assert.Contains("Vence en 60 minutos", r.Mensaje);
        Assert.Equal(T.AddMinutes(60), r.VenceEn);
        Assert.StartsWith(CamionApiFactory.UrlPortalPruebas + RecuperacionEndpoints.RutaFrontend, r.Enlace);
        var codigo = CodigoDe(r.Enlace!);
        Assert.DoesNotContain(codigo, r.Mensaje);

        var auditoria = Assert.Single(await AuditoriaDe(_api.F, admin.Id), a => a.Accion == RecuperarAdmin.AccionAuditoria);
        Assert.Null(auditoria.UsuarioId);
        Assert.Contains(RecuperarAdmin.ActorComandoLocal, auditoria.DatosNuevos);
        Assert.DoesNotContain(codigo, auditoria.DatosNuevos);
        Assert.DoesNotContain(_api.F.Logs, l => l.Contains(codigo, StringComparison.Ordinal));

        var version = (await Usuario(_api.F, admin.Id)).SessionVersion;
        await Esperar(await Restablecer(_api, codigo), HttpStatusCode.OK);
        var despues = await Usuario(_api.F, admin.Id);
        Assert.Equal(version + 1, despues.SessionVersion);
        Assert.NotEqual(sello, despues.SecurityStamp);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_api.C, admin.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(_api.C, admin.Email, NuevaClave)).StatusCode);
        Assert.Single(await AuditoriaDe(_api.F, admin.Id), a => a.Accion == "RESTABLECER_CONTRASENA");
        // Un solo uso.
        await Error400(await Restablecer(_api, codigo, "OtraClave2028"), "codigo", RecuperacionEndpoints.MensajeCodigo);
    }

    [Fact(DisplayName = "D-79: el comando no actúa sobre compradores, asesores, correos inexistentes ni administradores no activos")]
    public async Task Comando_solo_administrador_activo()
    {
        var (compradorId, compradorEmail) = await CrearComprador(_api.C, "Comprador Comando Ficticio");
        var asesor = await _api.CrearAsesor("Asesor Comando Ficticio");
        var bloqueado = await CrearAdministrador(_api.F, _api.C);
        await _api.Db(async db => (await db.Users.SingleAsync(u => u.Id == bloqueado.Id)).EstadoCuenta = EstadosCuenta.Bloqueada);
        foreach (var correo in new[] { compradorEmail, asesor.Email, "nadie.existe@ejemplo.test", bloqueado.Email })
        {
            var r = await Comando(_api, correo);
            Assert.Equal((1, RecuperarAdmin.MensajeNoAplica, (string?)null), (r.CodigoSalida, r.Mensaje, r.Enlace));
        }
        foreach (var id in new[] { compradorId, asesor.UsuarioId, bloqueado.Id })
            Assert.DoesNotContain(await AuditoriaDe(_api.F, id), a => a.Accion == RecuperarAdmin.AccionAuditoria);
        var vacio = await Comando(_api, "  ");
        Assert.Equal(2, vacio.CodigoSalida);
        Assert.Null(vacio.Enlace);
    }

    [Fact(DisplayName = "D-78, D-79: los enlaces no se intercambian (panel -> administrador no; comando -> otra cuenta no)")]
    public async Task Propositos_no_intercambiables()
    {
        _api.Hora(T);
        var admin = await CrearAdministrador(_api.F, _api.C);
        var deshecho = await TokenDirecto(_api, admin.Id, UserManager<ApplicationUser>.ResetPasswordTokenPurpose);
        await Error400(await Restablecer(_api, deshecho), "codigo", RecuperacionEndpoints.MensajeCodigo);

        var (compradorId, compradorEmail) = await CrearComprador(_api.C, "Comprador Proposito Ficticio");
        var deAdmin = await TokenDirecto(_api, compradorId, ProveedorTokenRecuperacion.PropositoAdministrador);
        await Error400(await Restablecer(_api, deAdmin), "codigo", RecuperacionEndpoints.MensajeCodigo);
        Assert.Equal(HttpStatusCode.OK, (await Login(_api.C, compradorEmail, PasswordValida)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(_api.C, admin.Email, PasswordValida)).StatusCode);
    }

    [Fact(DisplayName = "D-79: el enlace del comando vence a los 60 minutos y el siguiente invalida el anterior")]
    public async Task Vence_y_solo_vale_el_ultimo()
    {
        _api.Hora(T);
        var admin = await CrearAdministrador(_api.F, _api.C);
        var primero = CodigoDe((await Comando(_api, admin.Email)).Enlace!);
        var segundo = CodigoDe((await Comando(_api, admin.Email)).Enlace!);
        await Error400(await Restablecer(_api, primero), "codigo", RecuperacionEndpoints.MensajeCodigo);
        _api.Hora(T.AddMinutes(60));
        await Error400(await Restablecer(_api, segundo), "codigo", RecuperacionEndpoints.MensajeCodigo);
        _api.Hora(T);
        Assert.Equal(HttpStatusCode.OK, (await Login(_api.C, admin.Email, PasswordValida)).StatusCode);
    }
}
