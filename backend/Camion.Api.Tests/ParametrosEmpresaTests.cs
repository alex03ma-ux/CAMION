using System.Net;
using System.Text;
using System.Text.Json;
using Camion.Api.Models;
using Camion.Api.Parametros;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP7;

namespace Camion.Api.Tests;

/// <summary>
/// P9 — datos de la empresa (HU-085, RN-PA-005, D-63 B, D-44; CA-P9-25 a CA-P9-33, CA-P9-37): lectura y edición del
/// administrador en configuracion_sistema (clave portal.empresa), NIT con dígito de verificación de la DIAN y publicación en
/// GET /api/portal/contacto solo con los tres datos. SQLite en memoria; nunca MariaDB. Datos ficticios (el NIT de ejemplo
/// es el público de la DIAN).
/// </summary>
public sealed class ParametrosEmpresaTests
{
    private const string Ruta = "/api/admin/parametros/empresa";
    private const string MsgRazon = "Escribe la razón social (entre 2 y 150 caracteres, sin los signos < >).";
    private const string MsgNit = "Escribe el NIT con su dígito de verificación, por ejemplo 900.123.456-7.";
    private const string MsgDireccion = "Escribe la dirección de notificaciones (entre 5 y 200 caracteres, sin los signos < >).";

    private static Dictionary<string, object?> Cuerpo(string? razon = "Empresa Ficticia S.A.S.", string? nit = "800.197.268-4",
        string? direccion = "Calle 10 # 20-30, Medellín") => new()
    {
        ["razonSocial"] = razon,
        ["nit"] = nit,
        ["direccion"] = direccion
    };

    private static Task<HttpResponseMessage> Guardar(InventarioApi api, object? cuerpo, string? token = "") =>
        api.Enviar(HttpMethod.Put, Ruta, token == "" ? api.Admin.Token : token, cuerpo);

    private static async Task<JsonElement> Leer(InventarioApi api) =>
        await Esperar(await api.Enviar(HttpMethod.Get, Ruta, api.Admin.Token), HttpStatusCode.OK);

    private static Task<List<AuditoriaEvento>> Auditorias(InventarioApi api) =>
        api.Leer(db => db.AuditoriaEventos.AsNoTracking()
            .Where(a => a.Entidad == "Parametro" && a.EntidadId == ParametrosEmpresa.Clave).ToListAsync());

    private static Task<int> Filas(InventarioApi api) =>
        api.Leer(db => db.ConfiguracionesSistema.CountAsync(c => c.Clave == ParametrosEmpresa.Clave));

    [Fact(DisplayName = "CA-P9-32: GET y PUT de la empresa -> 401 sin sesión, 403 comprador y asesor, 401 asesor PENDIENTE o cuenta bloqueada")]
    public async Task Permisos()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var pendiente = await AyudasP2.Usuario(api.F, api.Pendiente.Id);
        var tokenPendiente = AyudasP2.Firmar(api.F, api.Pendiente.Id, pendiente.SessionVersion, "ASESOR");
        var bloqueado = await api.CrearAsesor("Asesor Bloqueado Empresa");
        await AyudasP2.CambiarEstado(api.F, bloqueado.UsuarioId, EstadosCuenta.Bloqueada);

        foreach (var metodo in new[] { HttpMethod.Get, HttpMethod.Put })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await api.Enviar(metodo, Ruta, null, Cuerpo())).StatusCode);
            foreach (var token in new[] { api.TokenComprador, api.A.Token })
                await Error(await api.Enviar(metodo, Ruta, token, Cuerpo()), HttpStatusCode.Forbidden, MsgSinPermiso);
            foreach (var token in new[] { tokenPendiente, bloqueado.Token })
                Assert.Equal(HttpStatusCode.Unauthorized, (await api.Enviar(metodo, Ruta, token, Cuerpo())).StatusCode);
        }
        Assert.Equal(0, await Filas(api));
        Assert.Empty(await Auditorias(api));
    }

    [Fact(DisplayName = "CA-P9-25, 33: sin datos guardados -> los tres en null, completo y configurado false; en el portal empresa = null")]
    public async Task Sin_datos()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var json = await Leer(api);
        Assert.Equal(new[] { "actualizadoEn", "actualizadoPor", "completo", "configurado", "direccion", "nit", "razonSocial" },
            json.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
        foreach (var campo in new[] { "razonSocial", "nit", "direccion", "actualizadoEn", "actualizadoPor" })
            Assert.Equal(JsonValueKind.Null, json.GetProperty(campo).ValueKind);
        Assert.False(json.GetProperty("completo").GetBoolean());
        Assert.False(json.GetProperty("configurado").GetBoolean());
        Assert.Equal(JsonValueKind.Null, (await api.ContactoPublico()).GetProperty("empresa").ValueKind);
    }

    [Fact(DisplayName = "CA-P9-26, 29, 33: guardar los tres -> 200 con el NIT normalizado, auditoría CAMBIAR y empresa en el portal; repetir no escribe ni audita")]
    public async Task Guardar_completo()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        api.Hora(T);
        var json = await Esperar(await Guardar(api, Cuerpo(razon: "  Empresa Ficticia S.A.S.  ")), HttpStatusCode.OK);
        Assert.Equal("Empresa Ficticia S.A.S.", json.GetProperty("razonSocial").GetString());
        Assert.Equal("800197268-4", json.GetProperty("nit").GetString());
        Assert.Equal("Calle 10 # 20-30, Medellín", json.GetProperty("direccion").GetString());
        Assert.True(json.GetProperty("completo").GetBoolean());
        Assert.True(json.GetProperty("configurado").GetBoolean());
        Assert.Equal("Admin Prueba", json.GetProperty("actualizadoPor").GetString());
        Assert.Equal(T, json.GetProperty("actualizadoEn").GetDateTime().ToUniversalTime());

        var auditoria = Assert.Single(await Auditorias(api));
        Assert.Equal("CAMBIAR", auditoria.Accion);
        Assert.Equal(api.Admin.Id, auditoria.UsuarioId);
        Assert.Null(auditoria.DatosAnteriores);
        Assert.Contains("800197268-4", auditoria.DatosNuevos);

        var publico = await api.ContactoPublico();
        var empresa = publico.GetProperty("empresa");
        Assert.Equal(new[] { "direccion", "nit", "razonSocial" },
            empresa.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("Empresa Ficticia S.A.S.", empresa.GetProperty("razonSocial").GetString());
        Assert.Equal("800197268-4", empresa.GetProperty("nit").GetString());
        Assert.Equal("Calle 10 # 20-30, Medellín", empresa.GetProperty("direccion").GetString());
        var texto = publico.GetRawText();
        foreach (var prohibido in new[] { api.Admin.Id, "Admin Prueba", "actualizado", "configurado", "completo" })
            Assert.DoesNotContain(prohibido, texto, StringComparison.OrdinalIgnoreCase);

        // Mismo valor con otro formato del NIT: nada cambia.
        await Esperar(await Guardar(api, Cuerpo(nit: "800 197 268 - 4")), HttpStatusCode.OK);
        Assert.Single(await Auditorias(api));

        // Cambio real: segunda auditoría con valores anterior y nuevo.
        await Esperar(await Guardar(api, Cuerpo(direccion: "Carrera 50 # 10-20, Bogotá")), HttpStatusCode.OK);
        var segunda = (await Auditorias(api)).OrderBy(a => a.Id).Last();
        Assert.Contains("Calle 10", segunda.DatosAnteriores);
        Assert.Contains("Carrera 50", segunda.DatosNuevos);
    }

    [Fact(DisplayName = "CA-P9-27, 33: cada campo es opcional; con uno solo el portal no publica nada; vacío o espacios -> null")]
    public async Task Campos_opcionales()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var json = await Esperar(await Guardar(api, Cuerpo(nit: "  ", direccion: null)), HttpStatusCode.OK);
        Assert.Equal("Empresa Ficticia S.A.S.", json.GetProperty("razonSocial").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("nit").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("direccion").ValueKind);
        Assert.False(json.GetProperty("completo").GetBoolean());
        Assert.True(json.GetProperty("configurado").GetBoolean());
        Assert.Equal(JsonValueKind.Null, (await api.ContactoPublico()).GetProperty("empresa").ValueKind);

        // Vaciarlos todos los retira (queda la fila con los tres en null).
        json = await Esperar(await Guardar(api, Cuerpo(razon: "", nit: "", direccion: " ")), HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("razonSocial").ValueKind);
        Assert.Equal(2, (await Auditorias(api)).Count);
        Assert.Equal(JsonValueKind.Null, (await api.ContactoPublico()).GetProperty("empresa").ValueKind);
    }

    [Fact(DisplayName = "CA-P9-26: sin datos guardados, guardar los tres vacíos ({}) no escribe ni audita")]
    public async Task Vacio_sin_cambios()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var json = await Esperar(await Guardar(api, new { }), HttpStatusCode.OK);
        Assert.False(json.GetProperty("configurado").GetBoolean());
        Assert.Equal(0, await Filas(api));
        Assert.Empty(await Auditorias(api));
    }

    public static TheoryData<string, Dictionary<string, object?>, string, string> Invalidos => new()
    {
        { "razón de 1", Cuerpo(razon: "A"), "razonSocial", MsgRazon },
        { "razón de 151", Cuerpo(razon: new string('r', 151)), "razonSocial", MsgRazon },
        { "razón con HTML", Cuerpo(razon: "<b>Empresa</b>"), "razonSocial", MsgRazon },
        { "razón con salto", Cuerpo(razon: "Empresa\nFicticia"), "razonSocial", MsgRazon },
        { "nit con DV errado", Cuerpo(nit: "800197268-5"), "nit", MsgNit },
        { "nit sin guion", Cuerpo(nit: "8001972684"), "nit", MsgNit },
        { "nit corto", Cuerpo(nit: "80019-4"), "nit", MsgNit },
        { "nit largo", Cuerpo(nit: "12345678901-2"), "nit", MsgNit },
        { "nit con letras", Cuerpo(nit: "ABC-1"), "nit", MsgNit },
        { "nit de ceros", Cuerpo(nit: "000000-0"), "nit", MsgNit },
        { "dirección corta", Cuerpo(direccion: "Cl 1"), "direccion", MsgDireccion },
        { "dirección de 201", Cuerpo(direccion: new string('d', 201)), "direccion", MsgDireccion },
        { "dirección con HTML", Cuerpo(direccion: "Calle 1 <script>"), "direccion", MsgDireccion },
    };

    [Theory(DisplayName = "CA-P9-28 a 30: dato inválido -> 400 con el campo y el mensaje; nada se escribe")]
    [MemberData(nameof(Invalidos))]
    public async Task Invalido(string caso, Dictionary<string, object?> cuerpo, string campo, string mensaje)
    {
        Assert.NotNull(caso);
        using var api = await InventarioApi.Crear(NuevoReloj());
        await Error400(await Guardar(api, cuerpo), campo, mensaje);
        Assert.Equal(0, await Filas(api));
        Assert.Empty(await Auditorias(api));
    }

    [Fact(DisplayName = "CA-P9-31: varios errores a la vez -> 400 con los tres; cuerpo vacío, JSON no válido o tipo incorrecto -> formato común")]
    public async Task Varios_errores_y_formato()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var json = await Esperar(await Guardar(api, Cuerpo(razon: "A", nit: "1-1", direccion: "x")), HttpStatusCode.BadRequest);
        Assert.Equal(new[] { "direccion", "nit", "razonSocial" },
            json.GetProperty("errores").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());

        foreach (var crudo in new[] { "", "{ no es json", "{\"razonSocial\": 5}", "[]" })
        {
            var req = Ayudas.ConToken(HttpMethod.Put, Ruta, api.Admin.Token);
            req.Content = new StringContent(crudo, Encoding.UTF8, "application/json");
            await Error(await api.C.SendAsync(req), HttpStatusCode.BadRequest, "La solicitud no tiene un formato válido.");
        }
        Assert.Equal(0, await Filas(api));
        Assert.Empty(await Auditorias(api));
    }

    [Fact(DisplayName = "CA-P9-33 (defensa): un dato editado a mano en la base que no cumple las reglas no se devuelve ni se publica")]
    public async Task Valor_manipulado()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        await Esperar(await Guardar(api, Cuerpo()), HttpStatusCode.OK);
        await api.Db(async db =>
        {
            var fila = await db.ConfiguracionesSistema.SingleAsync(c => c.Clave == ParametrosEmpresa.Clave);
            fila.ValorJson = "{\"razonSocial\":\"<script>x</script>\",\"nit\":\"800197268-5\",\"direccion\":\"Calle 10 # 20-30\"}";
            await db.SaveChangesAsync();
        });
        var json = await Leer(api);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("razonSocial").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("nit").ValueKind);
        Assert.Equal("Calle 10 # 20-30", json.GetProperty("direccion").GetString());
        Assert.False(json.GetProperty("completo").GetBoolean());
        Assert.Equal(JsonValueKind.Null, (await api.ContactoPublico()).GetProperty("empresa").ValueKind);

        await api.Db(async db =>
        {
            (await db.ConfiguracionesSistema.SingleAsync(c => c.Clave == ParametrosEmpresa.Clave)).ValorJson = "no es json";
            await db.SaveChangesAsync();
        });
        Assert.False((await Leer(api)).GetProperty("configurado").GetBoolean());
    }

    [Fact(DisplayName = "CA-P9-37: guardar la empresa no cambia los datos de contacto, y guardar el contacto no toca la empresa")]
    public async Task Independiente_del_contacto()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var antes = (await api.ContactoPublico()).GetProperty("whatsapp").GetString();
        await Esperar(await Guardar(api, Cuerpo()), HttpStatusCode.OK);
        Assert.Equal(antes, (await api.ContactoPublico()).GetProperty("whatsapp").GetString());
        await Esperar(await api.GuardarContacto(CuerpoContacto(whatsapp: "3191112233")), HttpStatusCode.OK);
        var publico = await api.ContactoPublico();
        Assert.Equal("3191112233", publico.GetProperty("whatsapp").GetString());
        Assert.Equal("800197268-4", publico.GetProperty("empresa").GetProperty("nit").GetString());
    }

    // ---------------------------------------------------------------- NIT (UNIT, CA-P9-29)

    [Theory(DisplayName = "CA-P9-29 (UNIT): NIT válido con DV de la DIAN -> se normaliza sin puntos ni espacios")]
    [InlineData("800197268-4", "800197268-4")]
    [InlineData("800.197.268-4", "800197268-4")]
    [InlineData("800 197 268 - 4", "800197268-4")]
    [InlineData(" 860.034.313-7 ", "860034313-7")]
    public void Nit_valido(string entrada, string esperado) => Assert.Equal(esperado, ParametrosEmpresa.NormalizarNit(entrada));

    [Theory(DisplayName = "CA-P9-29 (UNIT): NIT no válido -> null")]
    [InlineData("800197268-5")]
    [InlineData("8001972684")]
    [InlineData("80019-4")]
    [InlineData("12345678901-2")]
    [InlineData("ABC-1")]
    [InlineData("000000-0")]
    [InlineData("800197268-")]
    [InlineData("800197268-44")]
    [InlineData("800-197-268-4")]
    [InlineData("")]
    [InlineData(null)]
    public void Nit_invalido(string? entrada) => Assert.Null(ParametrosEmpresa.NormalizarNit(entrada));

    [Fact(DisplayName = "CA-P9-29 (UNIT): dígito de verificación de 800197268 = 4 (suma 733, 733 mod 11 = 7)")]
    public void Digito_verificacion() => Assert.Equal(4, ParametrosEmpresa.DigitoVerificacion("800197268"));
}
