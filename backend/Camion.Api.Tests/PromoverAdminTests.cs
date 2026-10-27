using System.Net;
using System.Text.Json;
using Camion.Api.Comandos;
using Camion.Api.Data;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Camion.Api.Tests.Infraestructura.Ayudas;
using static Camion.Api.Tests.Infraestructura.AyudasP2;

namespace Camion.Api.Tests;

/// <summary>
/// D-13 (opción A): PromoverAdmin.EjecutarAsync con los servicios del host de pruebas (SQLite en memoria).
/// No se ejecuta el comando `promover-admin` contra ninguna base real.
/// </summary>
public sealed class PromoverAdminTests(CamionApiFactory factory) : IClassFixture<CamionApiFactory>
{
    private readonly HttpClient _c = factory.CreateClient();

    private async Task<ResultadoPromoverAdmin> Promover(string? correo)
    {
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        return await PromoverAdmin.EjecutarAsync(correo,
            sp.GetRequiredService<UserManager<ApplicationUser>>(),
            sp.GetRequiredService<RoleManager<IdentityRole>>(),
            sp.GetRequiredService<CamionDbContext>());
    }

    private Task<int> EventosPromover(string id) =>
        Db(factory, db => db.AuditoriaEventos.CountAsync(a => a.EntidadId == id && a.Accion == "PROMOVER_ADMIN"));

    [Theory(DisplayName = "D-13: correo vacío -> CorreoVacio, código 1")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Correo_vacio(string? correo)
    {
        var r = await Promover(correo);
        Assert.Equal(ResultadoPromoverAdminTipo.CorreoVacio, r.Tipo);
        Assert.Equal(1, r.CodigoSalida);
    }

    [Fact(DisplayName = "D-13: correo inexistente -> UsuarioNoEncontrado, código 1")]
    public async Task Inexistente()
    {
        var r = await Promover(CorreoUnico("nadie"));
        Assert.Equal(ResultadoPromoverAdminTipo.UsuarioNoEncontrado, r.Tipo);
        Assert.Equal(1, r.CodigoSalida);
    }

    [Fact(DisplayName = "D-13: cuenta no ACTIVA (asesor PENDIENTE) -> CuentaNoActiva; no cambia nada")]
    public async Task No_activa()
    {
        var (id, email, _) = await CrearAsesorPendiente(_c);
        var sv = (await Usuario(factory, id)).SessionVersion;
        var r = await Promover(email);
        Assert.Equal(ResultadoPromoverAdminTipo.CuentaNoActiva, r.Tipo);
        Assert.Equal(1, r.CodigoSalida);
        Assert.Equal(["ASESOR"], await RolesDe(factory, id));
        Assert.Equal(sv, (await Usuario(factory, id)).SessionVersion);
        Assert.Equal(0, await EventosPromover(id));
    }

    [Fact(DisplayName = "D-13: éxito con un comprador ACTIVA (correo en mayúsculas): rol ADMINISTRADOR sin COMPRADOR, SessionVersion+1, auditoría PROMOVER_ADMIN; ya admin -> sin cambios")]
    public async Task Exito_y_ya_administrador()
    {
        var (id, email) = await CrearComprador(_c);
        var tokenViejo = await ObtenerToken(_c, email);
        var sv = (await Usuario(factory, id)).SessionVersion;
        var inicio = DateTime.UtcNow.AddSeconds(-1);

        var r = await Promover("  " + email.ToUpperInvariant() + " ");
        Assert.Equal(ResultadoPromoverAdminTipo.Promovido, r.Tipo);
        Assert.Equal(0, r.CodigoSalida);
        Assert.DoesNotContain(PasswordValida, r.Mensaje);

        Assert.Equal(["ADMINISTRADOR"], await RolesDe(factory, id));
        Assert.Equal(sv + 1, (await Usuario(factory, id)).SessionVersion);
        Assert.Equal(1, await Db(factory, db => db.PerfilesComprador.CountAsync(p => p.UsuarioId == id))); // el perfil se conserva
        var aud = await Db(factory, db => db.AuditoriaEventos.AsNoTracking().SingleAsync(a => a.EntidadId == id && a.Accion == "PROMOVER_ADMIN"));
        Assert.Null(aud.UsuarioId); // acción del sistema
        Assert.Equal("Usuario", aud.Entidad);
        Assert.InRange(aud.CreadoEn, inicio, DateTime.UtcNow.AddSeconds(1));
        var roles = JsonSerializer.Deserialize<Dictionary<string, string[]>>(aud.DatosNuevos!)!["roles"];
        Assert.Equal(["ADMINISTRADOR"], roles);

        // La sesión previa se cierra; el login nuevo trae el rol y da acceso al panel.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, tokenViejo)).StatusCode);
        var nuevo = await ObtenerToken(_c, email);
        Assert.Equal(["ADMINISTRADOR"], (await Json(await Me(_c, nuevo))).GetProperty("roles").EnumerateArray().Select(x => x.GetString()!).ToArray());
        Assert.Equal(HttpStatusCode.OK, (await Lista(_c, nuevo)).StatusCode);

        // Segunda ejecución: ya es administrador, código 0 y nada cambia.
        var sv2 = (await Usuario(factory, id)).SessionVersion;
        var r2 = await Promover(email);
        Assert.Equal(ResultadoPromoverAdminTipo.YaEraAdministrador, r2.Tipo);
        Assert.Equal(0, r2.CodigoSalida);
        Assert.Equal(sv2, (await Usuario(factory, id)).SessionVersion);
        Assert.Equal(1, await EventosPromover(id));
        Assert.Equal(HttpStatusCode.OK, (await Me(_c, nuevo)).StatusCode);
    }
}
