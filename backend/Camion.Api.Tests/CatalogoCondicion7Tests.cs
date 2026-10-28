using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Camion.Api.Data;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Camion.Api.Tests.Infraestructura.Ayudas;

namespace Camion.Api.Tests;

/// <summary>
/// P4a — catálogo público tras P4a (condición 7, D-4, D-11): un vehículo INACTIVO o VENDIDO nunca se ve aunque su
/// publicación siga PUBLICADA; un vehículo RESERVADO se muestra como RESERVADO aunque la publicación diga DISPONIBLE;
/// la placa nunca sale en el catálogo (PROPUESTO). Usa los datos de referencia de P3 (V1 a V4) en una base propia.
/// </summary>
public sealed class CatalogoCondicion7Tests
{
    private static async Task CambiarEstadoVehiculo(CatalogoApi api, Publicacion p, string estado) =>
        await api.Db(async db => (await db.Vehiculos.SingleAsync(v => v.Id == p.VehiculoId)).Estado = estado);

    private static async Task<string[]> SlugsInicio(CatalogoApi api)
    {
        var inicio = await api.Inicio();
        return CatalogoJson.Slugs(inicio.GetProperty("destacados")).Concat(CatalogoJson.Slugs(inicio.GetProperty("recientes"))).ToArray();
    }

    private static async Task<JsonElement> Tarjeta(CatalogoApi api, string slug) =>
        (await api.Buscar("?tamano=48")).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("slug").GetString() == slug);

    /// <summary>Administrador ACTIVA creado en la base de la API del catálogo (mismo host que atiende las peticiones).</summary>
    private static async Task<string> TokenAdmin(CatalogoApi api)
    {
        var email = CorreoUnico("admin");
        using (var scope = api.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync("ADMINISTRADOR")) await roles.CreateAsync(new IdentityRole("ADMINISTRADOR"));
            var u = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(), UserName = email, Email = email, PhoneNumber = TelefonoValido,
                NombreCompleto = "Admin Catálogo Prueba", EstadoCuenta = EstadosCuenta.Activa
            };
            Assert.True((await users.CreateAsync(u, PasswordValida)).Succeeded);
            Assert.True((await users.AddToRoleAsync(u, "ADMINISTRADOR")).Succeeded);
        }
        return await ObtenerToken(api.Cliente, email);
    }

    [Theory(DisplayName = "Condición 7 (D-4, D-11): vehículo INACTIVO o VENDIDO -> fuera de búsqueda, inicio, detalle (404) y filtros")]
    [InlineData("INACTIVO")]
    [InlineData("VENDIDO")]
    public async Task Inactivo_o_vendido_oculto(string estado)
    {
        using var api = await CatalogoApi.Crear(noVisibles: false);
        var v3 = api.Datos.V3; // único Carro visible (Renault Logan)
        var v1 = api.Datos.V1; // destacado
        Assert.Contains(v3.SlugPublico!, await api.SlugsBusqueda("?tamano=48"));
        Assert.Contains("Carro", (await api.Filtros()).GetProperty("tipos").EnumerateArray().Select(t => t.GetProperty("nombre").GetString()));

        await CambiarEstadoVehiculo(api, v3, estado);
        await CambiarEstadoVehiculo(api, v1, estado);

        var slugs = await api.SlugsBusqueda("?tamano=48");
        Assert.DoesNotContain(v3.SlugPublico!, slugs);
        Assert.DoesNotContain(v1.SlugPublico!, slugs);
        Assert.Equal(2, (await api.Buscar("?tamano=48")).GetProperty("total").GetInt32()); // quedan V2 y V4
        Assert.DoesNotContain(v1.SlugPublico!, await SlugsInicio(api));
        Assert.DoesNotContain(v3.SlugPublico!, await SlugsInicio(api));
        await AyudasP2.AfirmarError(await api.Detalle(v3.SlugPublico!), HttpStatusCode.NotFound, CatalogoApi.MsgNoDisponible);
        await AyudasP2.AfirmarError(await api.Detalle(v1.SlugPublico!), HttpStatusCode.NotFound, CatalogoApi.MsgNoDisponible);
        // Filtros: el tipo Carro desaparece y los rangos se calculan sin esos vehículos.
        var filtros = await api.Filtros();
        Assert.DoesNotContain("Carro", filtros.GetProperty("tipos").EnumerateArray().Select(t => t.GetProperty("nombre").GetString()));
        Assert.Equal(80_000_000, filtros.GetProperty("rangos").GetProperty("precioMin").GetInt64());
        // La publicación sigue PUBLICADA (P4a no toca publicaciones).
        Assert.Equal("PUBLICADA", await api.Leer(db => db.Publicaciones.Where(p => p.Id == v3.Id).Select(p => p.EstadoPublicacion).SingleAsync()));
    }

    [Fact(DisplayName = "Condición 7: vehículo RESERVADO con publicación DISPONIBLE se ve como RESERVADO en búsqueda, inicio y detalle")]
    public async Task Reservado_se_muestra_reservado()
    {
        using var api = await CatalogoApi.Crear(noVisibles: false);
        var v1 = api.Datos.V1;
        Assert.Equal("DISPONIBLE", (await Tarjeta(api, v1.SlugPublico!)).GetProperty("estadoComercial").GetString());

        await CambiarEstadoVehiculo(api, v1, "RESERVADO");

        Assert.Equal("RESERVADO", (await Tarjeta(api, v1.SlugPublico!)).GetProperty("estadoComercial").GetString());
        var detalle = await AyudasP2.Ok(await api.Detalle(v1.SlugPublico!));
        Assert.Equal("RESERVADO", detalle.GetProperty("estadoComercial").GetString());
        var inicio = await api.Inicio();
        foreach (var lista in new[] { "destacados", "recientes" })
            foreach (var t in inicio.GetProperty(lista).EnumerateArray().Where(x => x.GetProperty("slug").GetString() == v1.SlugPublico))
                Assert.Equal("RESERVADO", t.GetProperty("estadoComercial").GetString());
        // La publicación conserva su estado comercial en la base.
        Assert.Equal("DISPONIBLE", await api.Leer(db => db.Publicaciones.Where(p => p.Id == v1.Id).Select(p => p.EstadoComercial).SingleAsync()));
        // Los demás no cambian: V2 (publicación RESERVADO, vehículo ACTIVO) sigue RESERVADO; V3 sigue DISPONIBLE.
        Assert.Equal("RESERVADO", (await Tarjeta(api, api.Datos.V2.SlugPublico!)).GetProperty("estadoComercial").GetString());
        Assert.Equal("DISPONIBLE", (await Tarjeta(api, api.Datos.V3.SlugPublico!)).GetProperty("estadoComercial").GetString());
    }

    [Fact(DisplayName = "Condición 7 de punta a punta: reservar/liberar/inactivar/reactivar por /api/inventario cambia lo que ve el catálogo")]
    public async Task De_punta_a_punta_con_inventario()
    {
        using var api = await CatalogoApi.Crear(noVisibles: false);
        var token = await TokenAdmin(api);
        var v3 = api.Datos.V3;
        Task<HttpResponseMessage> Accion(string accion, string? motivo = null)
        {
            var req = ConToken(HttpMethod.Post, $"/api/inventario/vehiculos/{v3.VehiculoId}/{accion}", token);
            req.Content = JsonContent.Create(new { motivo = motivo ?? (accion == "reservar" ? InventarioApi.NotaReservaPrueba : null) });
            return api.Cliente.SendAsync(req);
        }

        await AyudasP2.Ok(await Accion("reservar"));
        Assert.Equal("RESERVADO", (await Tarjeta(api, v3.SlugPublico!)).GetProperty("estadoComercial").GetString());
        await AyudasP2.Ok(await Accion("liberar"));
        Assert.Equal("DISPONIBLE", (await Tarjeta(api, v3.SlugPublico!)).GetProperty("estadoComercial").GetString());
        await AyudasP2.Ok(await Accion("inactivar", "Retirado temporalmente"));
        Assert.DoesNotContain(v3.SlugPublico!, await api.SlugsBusqueda("?tamano=48"));
        Assert.Equal(HttpStatusCode.NotFound, (await api.Detalle(v3.SlugPublico!)).StatusCode);
        await AyudasP2.Ok(await Accion("reactivar"));
        Assert.Contains(v3.SlugPublico!, await api.SlugsBusqueda("?tamano=48"));
        Assert.Equal(HttpStatusCode.OK, (await api.Detalle(v3.SlugPublico!)).StatusCode);
    }

    [Fact(DisplayName = "P4a (PROPUESTO): la placa del vehículo nunca sale en el catálogo público (búsqueda, inicio, detalle, filtros)")]
    public async Task Sin_placa_en_catalogo()
    {
        using var api = await CatalogoApi.Crear(noVisibles: false);
        const string placa = "QZX987";
        await api.Db(async db => (await db.Vehiculos.SingleAsync(v => v.Id == api.Datos.V1.VehiculoId)).Placa = placa);

        foreach (var url in new[] { "/api/catalogo/inicio", "/api/catalogo/publicaciones?tamano=48", "/api/catalogo/filtros",
                     "/api/catalogo/publicaciones/" + api.Datos.V1.SlugPublico, "/api/catalogo/publicaciones?q=QZX987" })
        {
            var r = await api.Get(url);
            var texto = await r.Content.ReadAsStringAsync();
            Assert.True(r.IsSuccessStatusCode, $"{url}: {(int)r.StatusCode}");
            Assert.DoesNotContain(placa, texto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("placa", CatalogoJson.TodasLasClaves(JsonDocument.Parse(texto).RootElement), StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact(DisplayName = "D-19 en el catálogo: una carrocería sin marca ni año visible no rompe la búsqueda ni el detalle (marca y modeloAnio null)")]
    public async Task Carroceria_sin_marca_en_catalogo()
    {
        using var api = await CatalogoApi.Crear(noVisibles: false);
        await api.Db(async db =>
        {
            var v = await db.Vehiculos.SingleAsync(x => x.Id == api.Datos.V4.VehiculoId);
            v.TipoVehiculoId = api.Datos.Carroceria.Id;
            v.MarcaId = null;
            v.LineaId = null;
            v.ModeloAnio = null;
        });
        var tarjeta = await Tarjeta(api, api.Datos.V4.SlugPublico!);
        Assert.Equal(JsonValueKind.Null, tarjeta.GetProperty("marca").ValueKind);
        Assert.Equal(JsonValueKind.Null, tarjeta.GetProperty("modeloAnio").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await api.Detalle(api.Datos.V4.SlugPublico!)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Get("/api/catalogo/filtros")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Get("/api/catalogo/inicio")).StatusCode);
    }
}
