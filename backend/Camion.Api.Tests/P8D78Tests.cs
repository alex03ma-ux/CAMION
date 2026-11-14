using System.Net;
using Camion.Api.Auth;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using static Camion.Api.Tests.Infraestructura.Ayudas;
using static Camion.Api.Tests.Infraestructura.AyudasP2;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;

namespace Camion.Api.Tests;

/// <summary>
/// D-78 (técnica, coordinador): correcciones de la recuperación asistida tras QA-P8-1, QA-P8-2 y la revisión de seguridad
/// (B-1, M-1, M-2, B-4). SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class P8D78Tests(P7Fixture fixture) : IClassFixture<P7Fixture>
{
    private readonly InventarioApi _api = fixture.Api;

    private static Task<string?> Sello(CamionApiFactory f, string id) => Db(f, async db => (await db.Users.FindAsync(id))!.SecurityStamp);

    [Fact(DisplayName = "D-78 (QA-P8-1): aprobar, bloquear, desbloquear, rechazar y reconsiderar cambian el SecurityStamp")]
    public async Task Cambios_de_estado_cambian_el_sello()
    {
        var (pendiente, _, _) = await CrearAsesorPendiente(_api.C, "Asesor Sello Ficticio");
        var antes = await Sello(_api.F, pendiente);
        foreach (var accion in new[] { "aprobar", "bloquear", "desbloquear" })
        {
            await Ok(await AccionMotivo(_api.C, _api.Admin.Token, pendiente, accion, AyudasP2.MotivoValido));
            var despues = await Sello(_api.F, pendiente);
            Assert.NotEqual(antes, despues);
            antes = despues;
        }
        var (otro, _, _) = await CrearAsesorPendiente(_api.C, "Asesor Sello Rechazo Ficticio");
        var s0 = await Sello(_api.F, otro);
        await Ok(await AccionMotivo(_api.C, _api.Admin.Token, otro, "rechazar", AyudasP2.MotivoValido));
        var s1 = await Sello(_api.F, otro);
        await Ok(await AccionMotivo(_api.C, _api.Admin.Token, otro, "reconsiderar", AyudasP2.MotivoValido));
        Assert.NotEqual(s0, s1);
        Assert.NotEqual(s1, await Sello(_api.F, otro));
    }

    [Fact(DisplayName = "D-78: cambiar el SecurityStamp no cierra sesiones (solo SessionVersion lo hace, D-12)")]
    public async Task El_sello_no_cierra_sesiones()
    {
        var (id, email) = await CrearComprador(_api.C, "Comprador Sello Ficticio");
        var token = await ObtenerToken(_api.C, email);
        using (var scope = _api.F.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Camion.Api.Models.ApplicationUser>>();
            Assert.True((await users.UpdateSecurityStampAsync((await users.FindByIdAsync(id))!)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.OK, (await Me(_api.C, token)).StatusCode);
    }

    [Fact(DisplayName = "D-78 (QA-P8-2): promover a administrador cambia el SecurityStamp")]
    public async Task Promover_cambia_el_sello()
    {
        var (id, email) = await CrearComprador(_api.C, "Comprador Promovido Sello");
        var antes = await Sello(_api.F, id);
        using (var scope = _api.F.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var r = await Camion.Api.Comandos.PromoverAdmin.EjecutarAsync(email, sp.GetRequiredService<UserManager<Camion.Api.Models.ApplicationUser>>(),
                sp.GetRequiredService<RoleManager<IdentityRole>>(), sp.GetRequiredService<Camion.Api.Data.CamionDbContext>());
            Assert.Equal(0, r.CodigoSalida);
        }
        Assert.NotEqual(antes, await Sello(_api.F, id));
    }

    [Fact(DisplayName = "D-78 (B-4): Referrer-Policy: no-referrer en las respuestas de la API, también en errores")]
    public async Task Referrer_policy()
    {
        var respuestas = new[]
        {
            await _api.C.GetAsync("/health"),
            await Login(_api.C, "nadie@ejemplo.test", "Incorrecta2026"),
            await _api.Enviar(HttpMethod.Post, "/api/auth/restablecer-contrasena", null, new { codigo = "x", password = "x" }),
            await _api.Enviar(HttpMethod.Get, "/api/admin/indicadores", null),
            await _api.Enviar(HttpMethod.Get, "/api/admin/indicadores", _api.A.Token)
        };
        foreach (var r in respuestas)
            Assert.Equal("no-referrer", Assert.Single(r.Headers.GetValues("Referrer-Policy")));
    }

    [Fact(DisplayName = "D-78 (M-1): la carpeta de claves es configurable, por defecto C:\\CAMION_claves, nunca dentro del repositorio")]
    public void Ruta_de_claves()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "repo_ficticio", "backend", "Camion.Api");
        Assert.Equal(Path.GetFullPath(ConfiguracionRecuperacion.RutaClavesPorDefecto), ConfiguracionRecuperacion.RutaClaves(null, contentRoot));
        var fuera = Path.Combine(Path.GetTempPath(), "claves_fuera");
        Assert.Equal(Path.GetFullPath(fuera), ConfiguracionRecuperacion.RutaClaves(fuera, contentRoot));
        foreach (var dentro in new[] { contentRoot, Path.Combine(contentRoot, "claves"), Path.Combine(contentRoot, "..", "..", "docs") })
            Assert.Contains(ConfiguracionRecuperacion.ClaveRutaClaves,
                Assert.Throws<InvalidOperationException>(() => ConfiguracionRecuperacion.RutaClaves(dentro, contentRoot)).Message);
    }

    [Theory(DisplayName = "D-78 (M-2): Recuperacion:UrlPortal debe ser https:// fuera de Development")]
    [InlineData("https://portal.ejemplo.test", false, true)]
    [InlineData("http://localhost:5173", true, true)]
    [InlineData("http://localhost:5173", false, false)]
    [InlineData("ftp://portal.ejemplo.test", true, false)]
    [InlineData("no-es-url", true, false)]
    [InlineData("", false, false)]
    public void Url_portal(string url, bool desarrollo, bool valida)
    {
        if (valida) ConfiguracionRecuperacion.ValidarUrlPortal(url, desarrollo);
        else Assert.Contains(ConfiguracionRecuperacion.ClaveUrlPortal,
            Assert.Throws<InvalidOperationException>(() => ConfiguracionRecuperacion.ValidarUrlPortal(url, desarrollo)).Message);
    }

    [Fact(DisplayName = "D-78 (M-2): en Production la API no arranca con un enlace http://")]
    public void Production_sin_https_no_arranca()
    {
        using var f = new CamionApiFactory();
        using var prod = f.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting(ConfiguracionRecuperacion.ClaveUrlPortal, "http://portal.ejemplo.test");
        });
        var ex = Assert.ThrowsAny<Exception>(() => prod.CreateClient());
        Assert.Contains("https://", (ex.InnerException ?? ex).Message + ex.Message);
    }
}
