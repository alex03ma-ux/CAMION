using System.Net;
using System.Text.Json;
using Camion.Api.Models;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.CatalogoJson;

namespace Camion.Api.Tests;

/// <summary>P3: consulta pública sin cuenta y de solo lectura (RN-SE-005, RN-CA-001, RN-SE-004, D-3).</summary>
public sealed class CatalogoSeguridadTests(CatalogoReferencia referencia) : IClassFixture<CatalogoReferencia>
{
    private CatalogoApi Ref => referencia.Api;

    private static readonly string[] Consultas =
    [
        "/api/catalogo/inicio", "/api/catalogo/publicaciones", "/api/catalogo/publicaciones?orden=precio_asc&tamano=2",
        "/api/catalogo/publicaciones/chevrolet-npr-2020", "/api/catalogo/publicaciones/kenworth-t800-2018",
        "/api/catalogo/publicaciones/n08-asesor-bloqueado", "/api/catalogo/publicaciones/no-existe",
        "/api/catalogo/publicaciones?precioMin=abc", "/api/catalogo/filtros"
    ];

    private async Task<Dictionary<string, (HttpStatusCode, string)>> Respuestas(string? token)
    {
        var d = new Dictionary<string, (HttpStatusCode, string)>();
        foreach (var url in Consultas)
        {
            var r = await Ref.Get(url, token);
            d[url] = (r.StatusCode, await r.Content.ReadAsStringAsync());
        }
        return d;
    }

    private async Task AfirmarIgualQueAnonimo(string? token, string quien)
    {
        var anonimo = await Respuestas(null);
        var conToken = await Respuestas(token);
        foreach (var url in Consultas)
        {
            Assert.True(conToken[url].Item1 is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden), $"{quien} {url}: {(int)conToken[url].Item1}");
            Assert.Equal(anonimo[url].Item1, conToken[url].Item1);
            Assert.Equal(anonimo[url].Item2, conToken[url].Item2);
        }
    }

    [Fact(DisplayName = "CA-SE005-01: sin Authorization los 4 endpoints responden 200")]
    public async Task Anonimo_200_en_los_4()
    {
        foreach (var url in CatalogoApi.Rutas)
            Assert.Equal(HttpStatusCode.OK, (await Ref.Get(url)).StatusCode);
    }

    [Fact(DisplayName = "CA-SE005-02, CA-D03-01: con token válido de comprador, asesor o administrador el cuerpo es idéntico al anónimo")]
    public async Task Tokens_validos_igual_que_anonimo()
    {
        var comprador = await Ayudas.RegistrarYObtenerToken(Ref.Cliente);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.Me(Ref.Cliente, comprador)).StatusCode); // el token es válido de verdad
        await AfirmarIgualQueAnonimo(comprador, "comprador");

        var a1 = Ref.Datos.A1.Usuario.Id;
        var asesor = Ref.Firmar(a1, 0, "ASESOR");
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.Me(Ref.Cliente, asesor)).StatusCode);
        await AfirmarIgualQueAnonimo(asesor, "asesor");

        var adminId = Guid.NewGuid().ToString();
        await Ref.Db(db =>
        {
            db.Users.Add(new ApplicationUser
            {
                Id = adminId, UserName = $"admin.{adminId:N}@ejemplo.test", Email = $"admin.{adminId:N}@ejemplo.test",
                NombreCompleto = "Admin Ficticio", EstadoCuenta = EstadosCuenta.Activa
            });
            return Task.CompletedTask;
        });
        await AfirmarIgualQueAnonimo(Ref.Firmar(adminId, 0, "ADMINISTRADOR"), "administrador");
    }

    [Fact(DisplayName = "CA-SE005-03: token caducado, revocado por logout o mal formado -> mismo cuerpo que anónimo; nunca 401/403")]
    public async Task Tokens_invalidos_igual_que_anonimo()
    {
        var email = Ayudas.CorreoUnico();
        var revocado = await Ayudas.RegistrarYObtenerToken(Ref.Cliente, email);
        Assert.Equal(HttpStatusCode.NoContent, (await Ayudas.Logout(Ref.Cliente, revocado)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Ayudas.Me(Ref.Cliente, revocado)).StatusCode);
        await AfirmarIgualQueAnonimo(revocado, "revocado");

        var caducado = Ref.Firmar(Ref.Datos.A1.Usuario.Id, 0, "ASESOR", DateTime.UtcNow.AddMinutes(-5));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Ayudas.Me(Ref.Cliente, caducado)).StatusCode);
        await AfirmarIgualQueAnonimo(caducado, "caducado");

        await AfirmarIgualQueAnonimo("abc", "mal formado");

        // Token válido de un asesor BLOQUEADO (A2): sigue siendo consulta anónima.
        await AfirmarIgualQueAnonimo(Ref.Firmar(Ref.Datos.A2.Usuario.Id, 0, "ASESOR"), "asesor bloqueado");
    }

    private Task<int[]> Conteos() => Ref.Leer(async db => new[]
    {
        await db.Users.CountAsync(), await db.Roles.CountAsync(), await db.UserRoles.CountAsync(),
        await db.PerfilesComprador.CountAsync(), await db.PerfilesAsesor.CountAsync(), await db.HistorialAutorizaciones.CountAsync(),
        await db.AuditoriaEventos.CountAsync(), await db.Marcas.CountAsync(), await db.Lineas.CountAsync(),
        await db.TiposVehiculo.CountAsync(), await db.TiposCarroceria.CountAsync(), await db.Combustibles.CountAsync(),
        await db.Departamentos.CountAsync(), await db.Municipios.CountAsync(), await db.Suscripciones.CountAsync(),
        await db.Vehiculos.CountAsync(), await db.Publicaciones.CountAsync(), await db.FotosPublicacion.CountAsync(),
        await db.HistorialVigencias.CountAsync(), await db.PublicacionesDestacadas.CountAsync()
    });

    private Task<string> EstadoPublicaciones() => Ref.Leer(async db => string.Join(";",
        (await db.Publicaciones.AsNoTracking().OrderBy(p => p.Id).ToListAsync())
        .Select(p => $"{p.SlugPublico}|{p.EstadoPublicacion}|{p.EstadoComercial}|{p.Titulo}|{p.PrecioCop}|{p.ActualizadoEn:O}")));

    [Fact(DisplayName = "CA-SE005-04, CA-SE005-05, CA-REG-10: POST/PUT/PATCH/DELETE -> 405/404, nunca 2xx; ninguna consulta escribe ni audita")]
    public async Task Solo_lectura()
    {
        var antes = await Conteos();
        var estadoAntes = await EstadoPublicaciones();
        var logsAntes = Ref.Base.Logs.Count(l => l.Contains("Auditoria"));
        var comprador = await Ayudas.RegistrarYObtenerToken(Ref.Cliente);
        antes = await Conteos(); // el registro del comprador sí escribe; se mide desde aquí
        logsAntes = Ref.Base.Logs.Count(l => l.Contains("Auditoria"));

        foreach (var url in CatalogoApi.Rutas)
            foreach (var metodo in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
                foreach (var token in new[] { null, comprador })
                {
                    var req = Ayudas.ConToken(metodo, url, token);
                    req.Content = JsonContent(new { estadoComercial = "VENDIDO", titulo = "cambiado" });
                    var r = await Ref.Cliente.SendAsync(req);
                    Assert.True(r.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound,
                        $"{metodo} {url}: {(int)r.StatusCode}");
                }
        // Batería de lecturas válidas, 400 y 404.
        await Respuestas(null);
        await Respuestas(comprador);

        Assert.Equal(antes, await Conteos());
        Assert.Equal(estadoAntes, await EstadoPublicaciones());
        Assert.Equal(logsAntes, Ref.Base.Logs.Count(l => l.Contains("Auditoria")));
    }

    private static HttpContent JsonContent(object o) => System.Net.Http.Json.JsonContent.Create(o);

    [Fact(DisplayName = "CA-DET-04, CA-SE005-07: ninguna respuesta incluye ids internos ni datos de usuarios o asesores")]
    public async Task Sin_datos_de_usuarios_ni_ids_internos()
    {
        var d = Ref.Datos;
        var prohibidosEnValores = new List<string>();
        foreach (var a in new[] { d.A1, d.A2, d.A3, d.A4 })
            prohibidosEnValores.AddRange(new[] { a.Usuario.Id, a.Usuario.Email!, a.Usuario.PhoneNumber!, a.Usuario.NombreCompleto,
                a.Perfil.NumeroDocumento, a.Perfil.NombreComercial! });
        var clavesProhibidas = new[] { "id", "vehiculoId", "publicacionId", "asesorId", "usuarioId", "email", "telefono",
            "nombreCompleto", "numeroDocumento", "nombreComercial", "motivoOcultamiento", "suscripcionId", "planId", "archivoClave" };

        var urls = new[] { "/api/catalogo/inicio", "/api/catalogo/publicaciones?tamano=48" }
            .Concat(new[] { "chevrolet-npr-2020", "kenworth-t800-2018", "renault-logan-2021", "chevrolet-camion-2015" }
                .Select(s => "/api/catalogo/publicaciones/" + s));
        foreach (var url in urls)
        {
            var texto = await (await Ref.Get(url)).Content.ReadAsStringAsync();
            var claves = TodasLasClaves(JsonDocument.Parse(texto).RootElement).Distinct().ToArray();
            foreach (var c in clavesProhibidas)
                Assert.DoesNotContain(claves, k => string.Equals(k, c, StringComparison.OrdinalIgnoreCase));
            foreach (var v in prohibidosEnValores) Assert.DoesNotContain(v, texto, StringComparison.Ordinal);
        }

        // /filtros solo trae ids de catálogo y las claves de su formato.
        var filtros = await (await Ref.Get("/api/catalogo/filtros")).Content.ReadAsStringAsync();
        var permitidas = new[] { "tipos", "marcas", "rangos", "id", "nombre", "total", "lineas", "precioMin", "precioMax", "anioMin", "anioMax", "kmMax" };
        Assert.All(TodasLasClaves(JsonDocument.Parse(filtros).RootElement).Distinct(), k => Assert.Contains(k, permitidas));
        foreach (var v in prohibidosEnValores) Assert.DoesNotContain(v, filtros, StringComparison.Ordinal);
    }
}
