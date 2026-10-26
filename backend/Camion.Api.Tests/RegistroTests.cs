using System.Net;
using System.Text;
using Camion.Api.Data;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Camion.Api.Tests.Infraestructura.Ayudas;

namespace Camion.Api.Tests;

/// <summary>HU-N03 Registro de comprador — POST /api/auth/registro/comprador.</summary>
public sealed class RegistroTests(CamionApiFactory factory) : IClassFixture<CamionApiFactory>
{
    private readonly HttpClient _c = factory.CreateClient();

    private const string MsgNombre = "Escribe tu nombre completo (2 a 150 caracteres).";
    private const string MsgEmail = "Escribe un correo válido.";
    private const string MsgEmailLargo = "El correo no puede tener más de 254 caracteres.";
    private const string MsgTelefono = "Escribe un teléfono válido (solo números, espacios, +, - o paréntesis).";
    private const string MsgDuplicado = "Este correo ya está registrado.";

    private async Task<int> ContarUsuarios(string emailNormalizado)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CamionDbContext>();
        return await db.Users.CountAsync(u => u.Email == emailNormalizado);
    }

    [Fact(DisplayName = "CA-N03-01/02/03/19: registro anónimo 201, rol COMPRADOR, perfil creado y acceso inmediato")]
    public async Task Registro_exitoso_da_acceso_inmediato_como_comprador()
    {
        var email = CorreoUnico();
        var r = await Registrar(_c, Registro(email));

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var json = await Json(r);
        Assert.Equal("Cuenta creada. Ya puedes iniciar sesión.", json.GetProperty("mensaje").GetString());
        var id = json.GetProperty("usuarioId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(id));
        await AfirmarSinDatosSensibles(r, PasswordValida);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CamionDbContext>();
            var u = await db.Users.SingleAsync(x => x.Id == id);
            Assert.Equal("ACTIVA", u.EstadoCuenta);
            Assert.Equal(1, await db.PerfilesComprador.CountAsync(p => p.UsuarioId == id));
            // CA-062-02: la contraseña se guarda con hash, nunca en texto plano.
            Assert.False(string.IsNullOrEmpty(u.PasswordHash));
            Assert.DoesNotContain(PasswordValida, u.PasswordHash!);
        }

        // Acceso inmediato con la contraseña elegida (sin aprobación).
        var token = await ObtenerToken(_c, email);
        var me = await Me(_c, token);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meJson = await Json(me);
        Assert.Equal(["COMPRADOR"], meJson.GetProperty("roles").EnumerateArray().Select(x => x.GetString()!).ToArray());
        Assert.Equal(id, meJson.GetProperty("usuarioId").GetString());
    }

    [Fact(DisplayName = "CA-N03-04: un campo 'rol'/'roles' extra se ignora; el usuario queda solo COMPRADOR")]
    public async Task Campo_rol_extra_se_ignora()
    {
        var email = CorreoUnico();
        var r = await Registrar(_c, new
        {
            nombreCompleto = "Intruso Prueba", email, telefono = TelefonoValido, password = PasswordValida,
            rol = "ADMIN", roles = new[] { "ASESOR", "ADMINISTRADOR" }
        });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);

        var login = await Login(_c, email, PasswordValida);
        var roles = (await Json(login)).GetProperty("roles").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(["COMPRADOR"], roles);
        var me = await Json(await Me(_c, (await Json(login)).GetProperty("accessToken").GetString()));
        Assert.Equal(["COMPRADOR"], me.GetProperty("roles").EnumerateArray().Select(x => x.GetString()!).ToArray());
    }

    [Fact(DisplayName = "CA-N03-06/10: nombre se guarda recortado; teléfono con +, paréntesis y guiones es válido")]
    public async Task Nombre_recortado_y_telefono_con_simbolos()
    {
        var email = CorreoUnico();
        var r = await Registrar(_c, Registro(email, nombre: "  Ana  ", telefono: "+57 (300) 123-4567"));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var me = await Json(await Me(_c, await ObtenerToken(_c, email)));
        Assert.Equal("Ana", me.GetProperty("nombreCompleto").GetString());
        Assert.Equal("+57 (300) 123-4567", me.GetProperty("phoneNumber").GetString());
    }

    [Theory(DisplayName = "CA-N03-05: nombre inválido -> 400 errores.nombreCompleto")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData("LARGO151")]
    public async Task Nombre_invalido_da_400(string? nombre)
    {
        if (nombre == "LARGO151") nombre = new string('a', 151);
        var email = CorreoUnico();
        var r = await Registrar(_c, Registro(email, nombre: nombre));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal([MsgNombre], await ErroresDe(r, "nombreCompleto"));
        Assert.Equal("Revisa los datos marcados.", (await Json(r)).GetProperty("error").GetString());
        Assert.Equal(0, await ContarUsuarios(email));
    }

    [Theory(DisplayName = "CA-N03-07: correo con formato inválido -> 400 errores.email")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ana")]
    [InlineData("ana@")]
    [InlineData("ana@@x.com")]
    [InlineData("ana perez@x.com")]
    public async Task Correo_invalido_da_400(string email)
    {
        var r = await Registrar(_c, Registro(email));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal([MsgEmail], await ErroresDe(r, "email"));
    }

    [Fact(DisplayName = "CA-N03-07: correo ausente (null) -> 400 errores.email")]
    public async Task Correo_ausente_da_400()
    {
        var r = await Registrar(_c, new { nombreCompleto = "Ana Prueba", telefono = TelefonoValido, password = PasswordValida });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal([MsgEmail], await ErroresDe(r, "email"));
    }

    [Theory(DisplayName = "CA-N03-08: correo de más de 254 caracteres -> 400, nunca 500")]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(300)]
    [InlineData(2000)]
    public async Task Correo_largo_da_400(int largo)
    {
        const string dominio = "@ejemplo.test"; // 13 caracteres
        var email = new string('a', largo - dominio.Length) + dominio;
        Assert.Equal(largo, email.Length);
        var r = await Registrar(_c, Registro(email));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal([MsgEmailLargo], await ErroresDe(r, "email"));
    }

    [Fact(DisplayName = "CA-N03-08: correo de exactamente 254 caracteres -> 201")]
    public async Task Correo_de_254_da_201()
    {
        var sufijo = $"{Guid.NewGuid():N}@ejemplo.test";
        var email = new string('b', 254 - sufijo.Length) + sufijo;
        Assert.Equal(254, email.Length);
        var r = await Registrar(_c, Registro(email));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Theory(DisplayName = "CA-N03-09: teléfono inválido -> 400 errores.telefono")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("300abc4567")]
    [InlineData("12-34")]
    [InlineData("300#1234567")]
    [InlineData("12345678901234567890123456")] // 26 caracteres
    [InlineData("+ ( ) - 12")] // menos de 7 dígitos
    public async Task Telefono_invalido_da_400(string? telefono)
    {
        var r = await Registrar(_c, Registro(telefono: telefono));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal([MsgTelefono], await ErroresDe(r, "telefono"));
    }

    [Theory(DisplayName = "CA-N03-11: cada regla de contraseña incumplida da su propio mensaje")]
    [InlineData("abc", new[] { "Debe tener al menos 8 caracteres.", "Debe tener al menos una letra mayúscula.", "Debe tener al menos un número." })]
    [InlineData("camion2026", new[] { "Debe tener al menos una letra mayúscula." })]
    [InlineData("CAMION2026", new[] { "Debe tener al menos una letra minúscula." })]
    [InlineData("Camionaaa", new[] { "Debe tener al menos un número." })]
    [InlineData("Camión1", new[] { "Debe tener al menos 8 caracteres." })]
    [InlineData("ÑANDÚ2026", new[] { "Debe tener al menos una letra minúscula." })] // Ñ/Ú no cuentan (solo ASCII)
    [InlineData("ñandú2026", new[] { "Debe tener al menos una letra mayúscula." })]
    [InlineData("", new[] { "Debe tener al menos 8 caracteres.", "Debe tener al menos una letra mayúscula.", "Debe tener al menos una letra minúscula.", "Debe tener al menos un número." })]
    public async Task Password_invalida_da_mensaje_por_regla(string password, string[] esperados)
    {
        var r = await Registrar(_c, Registro(password: password));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var errores = await ErroresDe(r, "password");
        Assert.Equal(esperados.OrderBy(x => x, StringComparer.Ordinal), errores.OrderBy(x => x, StringComparer.Ordinal));
        if (password.Length > 0) await AfirmarSinDatosSensibles(r, password);
    }

    [Fact(DisplayName = "CA-N03-12: contraseña de 129 caracteres -> 400; de 128 -> 201")]
    public async Task Password_largo_maximo()
    {
        var p129 = "Aa1" + new string('x', 126);
        var r = await Registrar(_c, Registro(password: p129));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(["No puede tener más de 128 caracteres."], await ErroresDe(r, "password"));

        var p128 = "Aa1" + new string('x', 125);
        Assert.Equal(HttpStatusCode.Created, (await Registrar(_c, Registro(password: p128))).StatusCode);
    }

    [Fact(DisplayName = "CA-N03-13: varios campos inválidos -> todas las claves en una sola respuesta")]
    public async Task Varios_errores_a_la_vez()
    {
        var r = await Registrar(_c, new { nombreCompleto = "", email = "x", telefono = "abc", password = "abc" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var errores = (await Json(r)).GetProperty("errores");
        Assert.Equal(new[] { "email", "nombreCompleto", "password", "telefono" },
            errores.EnumerateObject().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact(DisplayName = "CA-N03-14: cuerpo JSON vacío ({}) -> 400 con errores de los 4 campos")]
    public async Task Cuerpo_objeto_vacio_da_400()
    {
        var r = await _c.PostAsync("/api/auth/registro/comprador", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var errores = (await Json(r)).GetProperty("errores");
        Assert.Equal(4, errores.EnumerateObject().Count());
    }

    [Fact(DisplayName = "CA-N03-14: petición sin cuerpo -> 400 con { error }, nunca 500")]
    public async Task Sin_cuerpo_da_400()
    {
        var r = await _c.PostAsync("/api/auth/registro/comprador", new StringContent("", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.True((await Json(r)).TryGetProperty("error", out _));
    }

    [Theory(DisplayName = "CA-N03-14: JSON malformado o con tipos incorrectos -> 400 con { error }, nunca 500")]
    [InlineData("{ esto no es json")]
    [InlineData("{\"nombreCompleto\": 123, \"email\": true, \"telefono\": [], \"password\": {}}")]
    [InlineData("[]")]
    public async Task Json_malformado_da_400_con_error(string cuerpo)
    {
        var r = await _c.PostAsync("/api/auth/registro/comprador", new StringContent(cuerpo, Encoding.UTF8, "application/json"));
        Assert.NotEqual(HttpStatusCode.InternalServerError, r.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var texto = await r.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(texto), "El 400 llega sin cuerpo; el contrato pide { error }.");
        Assert.True((await Json(r)).TryGetProperty("error", out _));
    }

    [Fact(DisplayName = "CA-N03-15: correo duplicado -> 409 con el mensaje del contrato y sin segundo usuario")]
    public async Task Duplicado_da_409()
    {
        var email = CorreoUnico("ana.perez");
        Assert.Equal(HttpStatusCode.Created, (await Registrar(_c, Registro(email))).StatusCode);

        var r = await Registrar(_c, Registro(email, nombre: "Otra Persona"));
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal(MsgDuplicado, (await Json(r)).GetProperty("error").GetString());
        Assert.Equal(1, await ContarUsuarios(email));
    }

    [Fact(DisplayName = "CA-N03-16: duplicado con mayúsculas y espacios -> 409; el correo se guarda normalizado")]
    public async Task Duplicado_con_mayusculas_y_espacios_da_409()
    {
        var email = CorreoUnico("ana.perez");
        Assert.Equal(HttpStatusCode.Created, (await Registrar(_c, Registro("  " + email.ToUpperInvariant() + " "))).StatusCode);
        Assert.Equal(1, await ContarUsuarios(email)); // guardado recortado y en minúsculas

        var variante = "  " + char.ToUpperInvariant(email[0]) + email[1..].Replace("@ejemplo", "@EJEMPLO") + "  ";
        var r = await Registrar(_c, Registro(variante));
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal(MsgDuplicado, (await Json(r)).GetProperty("error").GetString());
        Assert.Equal(1, await ContarUsuarios(email));
    }

    [Fact(DisplayName = "CA-N03-17: registros simultáneos con el mismo correo -> un 201, el resto 409, nunca 500")]
    public async Task Registros_simultaneos_mismo_correo()
    {
        const int paralelos = 8;
        var email = CorreoUnico("simultaneo");
        using var barrera = new Barrier(paralelos);
        var tareas = Enumerable.Range(0, paralelos).Select(i => Task.Run(async () =>
        {
            using var cliente = factory.CreateClient();
            barrera.SignalAndWait();
            return await Registrar(cliente, Registro(email, nombre: $"Paralelo {i}"));
        })).ToArray();
        var respuestas = await Task.WhenAll(tareas);
        var codigos = respuestas.Select(r => (int)r.StatusCode).ToArray();
        var resumen = string.Join(",", codigos);

        Assert.True(!codigos.Contains(500), $"Hubo un 500. Códigos: {resumen}");
        Assert.True(codigos.Count(x => x == 201) == 1, $"Se esperaba un único 201. Códigos: {resumen}");
        Assert.True(codigos.Count(x => x == 409) == paralelos - 1, $"Se esperaba 409 en el resto. Códigos: {resumen}");
        Assert.Equal(1, await ContarUsuarios(email));
    }
}
