using System.Data.Common;
using System.Net;
using System.Text.Json;
using Camion.Api.Data;
using Camion.Api.Models;
using Camion.Api.Parametros;
using Camion.Api.Seguridad;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP7;

namespace Camion.Api.Tests;

/// <summary>
/// Correcciones de la revisión de seguridad de P9 (docs/coordinacion/REVISION-SEGURIDAD-P9.md): B-3 (caracteres Unicode de
/// formato en los datos de la empresa), B-4 (dos PUT /empresa simultáneos sin fila previa -> 409, no 500) y M-1 (aviso en el
/// log sin proxies de confianza fuera de Development). SQLite en memoria; nunca MariaDB. Datos ficticios.
/// </summary>
public sealed class P9RevisionSeguridadTests
{
    private const string Ruta = "/api/admin/parametros/empresa";
    private const string MsgRazon = "Escribe la razón social (entre 2 y 150 caracteres, sin los signos < >).";
    private const string MsgNit = "Escribe el NIT con su dígito de verificación, por ejemplo 900.123.456-7.";
    private const string MsgDireccion = "Escribe la dirección de notificaciones (entre 5 y 200 caracteres, sin los signos < >).";
    private const string MsgConflicto =
        "Otro administrador acaba de guardar los datos de la empresa. Recarga la página, revisa los datos y vuelve a guardar.";

    private static Dictionary<string, object?> Cuerpo(string? razon = "Empresa Ficticia S.A.S.", string? nit = "800.197.268-4",
        string? direccion = "Calle 10 # 20-30, Medellín") => new()
    {
        ["razonSocial"] = razon,
        ["nit"] = nit,
        ["direccion"] = direccion
    };

    private static Task<HttpResponseMessage> Guardar(InventarioApi api, object? cuerpo) =>
        api.Enviar(HttpMethod.Put, Ruta, api.Admin.Token, cuerpo);

    private static Task<List<AuditoriaEvento>> Auditorias(InventarioApi api) =>
        api.Leer(db => db.AuditoriaEventos.AsNoTracking()
            .Where(a => a.Entidad == "Parametro" && a.EntidadId == ParametrosEmpresa.Clave).ToListAsync());

    private static Task<int> Filas(InventarioApi api) =>
        api.Leer(db => db.ConfiguracionesSistema.CountAsync(c => c.Clave == ParametrosEmpresa.Clave));

    // ---------------------------------------------------------------- B-3

    public static TheoryData<string, Dictionary<string, object?>, string, string> Invisibles => new()
    {
        { "razón con U+202E", Cuerpo(razon: "Empresa ‮Ficticia"), "razonSocial", MsgRazon },
        { "razón con U+200B", Cuerpo(razon: "Empresa​Ficticia"), "razonSocial", MsgRazon },
        { "razón con U+FEFF al inicio", Cuerpo(razon: "﻿Empresa Ficticia"), "razonSocial", MsgRazon },
        { "razón con U+2066", Cuerpo(razon: "Empresa ⁦Ficticia⁩"), "razonSocial", MsgRazon },
        { "razón con U+0007", Cuerpo(razon: "Empresa\u0007Ficticia"), "razonSocial", MsgRazon },
        { "razón con U+0085", Cuerpo(razon: "Empresa\u0085Ficticia"), "razonSocial", MsgRazon },
        { "nit con U+200B", Cuerpo(nit: "800.197.268​-4"), "nit", MsgNit },
        { "nit con U+202E", Cuerpo(nit: "‮800197268-4"), "nit", MsgNit },
        { "nit con tabulador", Cuerpo(nit: "800197268\t-4"), "nit", MsgNit },
        { "dirección con U+202E", Cuerpo(direccion: "Calle 10 ‮# 20-30"), "direccion", MsgDireccion },
        { "dirección con U+200D", Cuerpo(direccion: "Calle 10‍ # 20-30"), "direccion", MsgDireccion },
        { "dirección con U+00AD", Cuerpo(direccion: "Calle 10 # 20­-30"), "direccion", MsgDireccion },
        { "dirección con U+001B", Cuerpo(direccion: "Calle 10 \u001B# 20-30"), "direccion", MsgDireccion },
    };

    [Theory(DisplayName = "B-3: razón social, NIT o dirección con caracteres de formato (U+202E, U+200B...) o de control -> 400 con el campo y su mensaje; nada se escribe")]
    [MemberData(nameof(Invisibles))]
    public async Task Invisibles_rechazados(string caso, Dictionary<string, object?> cuerpo, string campo, string mensaje)
    {
        Assert.NotNull(caso);
        using var api = await InventarioApi.Crear(NuevoReloj());
        var json = await Esperar(await Guardar(api, cuerpo), HttpStatusCode.BadRequest);
        Assert.Equal("Revisa los datos marcados.", json.GetProperty("error").GetString());
        var errores = json.GetProperty("errores");
        Assert.Equal(new[] { campo }, errores.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(mensaje, Assert.Single(errores.GetProperty(campo).EnumerateArray()).GetString());
        Assert.Equal(0, await Filas(api));
        Assert.Empty(await Auditorias(api));
    }

    [Fact(DisplayName = "B-3: los tres campos con caracteres invisibles a la vez -> 400 con los tres")]
    public async Task Invisibles_tres_campos()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var json = await Esperar(await Guardar(api,
            Cuerpo(razon: "Empresa​", nit: "800197268-4‮", direccion: "Calle⁠ 10 # 20-30")), HttpStatusCode.BadRequest);
        Assert.Equal(new[] { "direccion", "nit", "razonSocial" },
            json.GetProperty("errores").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact(DisplayName = "B-3: tildes, eñe, # y signos habituales siguen siendo válidos")]
    public async Task Texto_habitual_valido()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var json = await Esperar(await Guardar(api, Cuerpo(razon: "Camiones Peña & Cía. S.A.S.",
            direccion: "Avenida Calle 26 # 68C-61, Oficina 304, Bogotá D.C.")), HttpStatusCode.OK);
        Assert.Equal("Camiones Peña & Cía. S.A.S.", json.GetProperty("razonSocial").GetString());
        Assert.True(json.GetProperty("completo").GetBoolean());
    }

    [Fact(DisplayName = "B-3 (defensa): un carácter de formato escrito a mano en la base no se devuelve ni se publica")]
    public async Task Invisibles_revalidados_al_leer()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        await Esperar(await Guardar(api, Cuerpo()), HttpStatusCode.OK);
        await api.Db(async db =>
        {
            var fila = await db.ConfiguracionesSistema.SingleAsync(c => c.Clave == ParametrosEmpresa.Clave);
            fila.ValorJson = JsonSerializer.Serialize(new
            {
                razonSocial = "Empresa ‮Ficticia",
                nit = "800197268-4",
                direccion = "Calle 10​ # 20-30"
            });
            await db.SaveChangesAsync();
        });
        var json = await Esperar(await api.Enviar(HttpMethod.Get, Ruta, api.Admin.Token), HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("razonSocial").ValueKind);
        Assert.Equal("800197268-4", json.GetProperty("nit").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("direccion").ValueKind);
        Assert.False(json.GetProperty("completo").GetBoolean());
        Assert.Equal(JsonValueKind.Null, (await api.ContactoPublico()).GetProperty("empresa").ValueKind);
    }

    [Theory(DisplayName = "B-3 (UNIT): TieneFormato")]
    [InlineData("Empresa Ficticia", false)]
    [InlineData("Peña Cía. # 20-30", false)]
    [InlineData("a‮b", true)]
    [InlineData("a​b", true)]
    [InlineData("a‌b", true)]
    [InlineData("a﻿", true)]
    [InlineData("a­b", true)]
    public void Tiene_formato(string texto, bool esperado) => Assert.Equal(esperado, ParametrosEmpresa.TieneFormato(texto));

    // ---------------------------------------------------------------- B-4

    /// <summary>
    /// Simula la carrera de forma determinista: las dos primeras lecturas de configuracion_sistema de la petición (la del
    /// valor anterior y la de la fila a actualizar) no ven la fila que otro administrador acaba de guardar, como si la
    /// hubiera confirmado justo después de esas lecturas. El INSERT choca entonces con la clave primaria de verdad.
    /// </summary>
    private sealed class OcultarFilaEmpresa : DbCommandInterceptor
    {
        private int _pendientes;
        public void Armar(int lecturas) => Volatile.Write(ref _pendientes, lecturas);
        public int Pendientes => Volatile.Read(ref _pendientes);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Ocultar(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Ocultar(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        private void Ocultar(DbCommand command)
        {
            if (!command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                !command.CommandText.Contains("configuracion_sistema", StringComparison.Ordinal))
                return;
            if (Interlocked.Decrement(ref _pendientes) < 0)
            {
                Interlocked.Increment(ref _pendientes);
                return;
            }
            command.CommandText = "SELECT * FROM (" + command.CommandText.TrimEnd().TrimEnd(';') + ") WHERE 0";
        }
    }

    private static Action<IWebHostBuilder> ConInterceptor(OcultarFilaEmpresa interceptor) => b => b.ConfigureTestServices(s =>
    {
        var original = s.Single(d => d.ServiceType == typeof(DbContextOptions<CamionDbContext>));
        s.Remove(original);
        s.Add(new ServiceDescriptor(typeof(DbContextOptions<CamionDbContext>), sp =>
        {
            var opciones = (DbContextOptions<CamionDbContext>)original.ImplementationFactory!(sp);
            return new DbContextOptionsBuilder<CamionDbContext>(opciones).AddInterceptors(interceptor).Options;
        }, original.Lifetime));
    });

    [Fact(DisplayName = "B-4: PUT /empresa sin fila previa mientras otro administrador la crea -> 409 con { error }, no 500; se conserva lo del otro y no se audita dos veces")]
    public async Task Primer_guardado_simultaneo_409()
    {
        var interceptor = new OcultarFilaEmpresa();
        using var api = await InventarioApi.Crear(NuevoReloj(), ConInterceptor(interceptor));
        // El otro administrador guarda primero (sin interceptar).
        await Esperar(await Guardar(api, Cuerpo(razon: "Empresa Primera S.A.S.")), HttpStatusCode.OK);
        Assert.Single(await Auditorias(api));

        interceptor.Armar(2);
        var r = await Guardar(api, Cuerpo(razon: "Empresa Segunda S.A.S."));
        Assert.Equal(0, interceptor.Pendientes);
        var json = await Esperar(r, HttpStatusCode.Conflict);
        Assert.Equal(new[] { "error" }, json.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(MsgConflicto, json.GetProperty("error").GetString());

        // Ni valor ni auditoría de la petición rechazada.
        var valor = await api.Leer(db => db.ConfiguracionesSistema.AsNoTracking()
            .SingleAsync(c => c.Clave == ParametrosEmpresa.Clave));
        Assert.Contains("Empresa Primera S.A.S.", valor.ValorJson);
        Assert.Single(await Auditorias(api));

        // Al reintentar (ya existe la fila) se actualiza con normalidad.
        var reintento = await Esperar(await Guardar(api, Cuerpo(razon: "Empresa Segunda S.A.S.")), HttpStatusCode.OK);
        Assert.Equal("Empresa Segunda S.A.S.", reintento.GetProperty("razonSocial").GetString());
        Assert.Equal(2, (await Auditorias(api)).Count);
    }

    [Fact(DisplayName = "B-4: dos PUT /empresa simultáneos sin fila previa -> ninguno 500; al menos uno 200; una sola fila")]
    public async Task Dos_put_simultaneos_sin_500()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var respuestas = await Task.WhenAll(
            Task.Run(() => Guardar(api, Cuerpo(razon: "Empresa Uno S.A.S."))),
            Task.Run(() => Guardar(api, Cuerpo(razon: "Empresa Dos S.A.S."))));
        Assert.All(respuestas, x => Assert.Contains(x.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Contains(respuestas, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Equal(1, await Filas(api));
        Assert.Equal(respuestas.Count(x => x.StatusCode == HttpStatusCode.OK), (await Auditorias(api)).Count);
    }

    // ---------------------------------------------------------------- M-1

    [Fact(DisplayName = "M-1: fuera de Development y sin Proxy:KnownProxies ni Proxy:KnownNetworks -> arranca y avisa en el log (sin valores)")]
    public async Task Sin_proxy_avisa()
    {
        using var f = new CamionApiFactory();
        using var c = f.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health")).StatusCode);
        var aviso = Assert.Single(f.Logs, l => l.Contains(ConfiguracionServidor.AvisoSinProxy, StringComparison.Ordinal));
        Assert.Contains("|Warning|", aviso);
        Assert.Contains("LimitesAuth", ConfiguracionServidor.AvisoSinProxy);
        // Ningún valor de configuración en el aviso.
        foreach (var valor in new[] { f.JwtKey, f.CarpetaMedia, CamionApiFactory.UrlPortalPruebas, "prueba_inexistente" })
            Assert.DoesNotContain(valor, aviso, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "M-1: con Proxy:KnownProxies o Proxy:KnownNetworks configurados no se avisa")]
    [InlineData("Proxy:KnownProxies", "10.0.0.5")]
    [InlineData("Proxy:KnownNetworks", "10.0.0.0/8")]
    public async Task Con_proxy_sin_aviso(string clave, string valor)
    {
        using var f = new CamionApiFactory();
        using var w = f.WithWebHostBuilder(b => b.UseSetting(clave, valor));
        using var c = w.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health")).StatusCode);
        Assert.DoesNotContain(f.Logs, l => l.Contains(ConfiguracionServidor.AvisoSinProxy, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "M-1 (UNIT): en Development no se avisa aunque no haya proxy")]
    public void Development_sin_aviso()
    {
        Assert.False(ConfiguracionServidor.AvisarSinProxy(null, esDesarrollo: true));
        Assert.True(ConfiguracionServidor.AvisarSinProxy(null, esDesarrollo: false));
        Assert.False(ConfiguracionServidor.AvisarSinProxy(new ForwardedHeadersOptions(), esDesarrollo: false));
    }
}
