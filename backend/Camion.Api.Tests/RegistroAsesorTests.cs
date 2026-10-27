using System.Net;
using System.Text;
using Camion.Api.Data;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Camion.Api.Tests.Infraestructura.Ayudas;
using static Camion.Api.Tests.Infraestructura.AyudasP2;

namespace Camion.Api.Tests;

/// <summary>P2 — POST /api/auth/registro/asesor (HU-043 / RN-US-001 por D-3, D-5, D-10) y HU-061 del registro.</summary>
public sealed class RegistroAsesorTests(CamionApiFactory factory) : IClassFixture<CamionApiFactory>
{
    private readonly HttpClient _c = factory.CreateClient();

    private const string MsgTipo = "Elige un tipo de documento válido.";
    private const string MsgNumero = "Escribe un número de documento válido (4 a 30 letras, números o guiones).";
    private const string MsgComercial = "El nombre comercial no puede tener más de 150 caracteres.";

    private Task<int> UsuariosConCorreo(string email) =>
        Db(factory, db => db.Users.CountAsync(u => u.Email == email));

    private Task<int> PerfilesConDocumento(string tipo, string numero) =>
        Db(factory, db => db.PerfilesAsesor.CountAsync(p => p.TipoDocumento == tipo && p.NumeroDocumento == numero));

    [Fact(DisplayName = "CA-043R-01/15/16, CA-061-07/13: registro anónimo 201, PENDIENTE, rol ASESOR, perfil, auditoría sin documento ni token")]
    public async Task Registro_exitoso()
    {
        var email = CorreoUnico("asesor");
        var numero = DocumentoUnico();
        var antes = DateTime.UtcNow.AddSeconds(-1);
        var r = await RegistrarAsesor(_c, RegistroAsesor(email, numero, tipo: "CC", comercial: "Camiones del Norte"));

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var json = await Json(r);
        Assert.Equal(MsgSolicitud, json.GetProperty("mensaje").GetString());
        var id = json.GetProperty("usuarioId").GetString()!;
        Assert.False(json.TryGetProperty("accessToken", out _)); // CA-043R-16: no inicia sesión
        await AfirmarSinDatosSensibles(r, PasswordValida);

        var u = await Usuario(factory, id);
        Assert.Equal("PENDIENTE", u.EstadoCuenta);
        Assert.Equal(email, u.Email);
        Assert.Equal(["ASESOR"], await RolesDe(factory, id));
        Assert.Equal(1, await UsuariosConCorreo(email));
        Assert.Equal(0, await Db(factory, db => db.PerfilesComprador.CountAsync(p => p.UsuarioId == id)));

        var p = await Perfil(factory, id);
        Assert.NotNull(p);
        Assert.Equal("PENDIENTE", p!.EstadoAutorizacion);
        Assert.Equal("CC", p.TipoDocumento);
        Assert.Equal(numero, p.NumeroDocumento);
        Assert.Equal("Camiones del Norte", p.NombreComercial);
        Assert.Null(p.AutorizadoPor);
        Assert.Null(p.AutorizadoEn);

        // CA-061-07: fila de auditoría del registro.
        var aud = Assert.Single(await AuditoriaDe(factory, id));
        Assert.Equal(id, aud.UsuarioId);
        Assert.Equal("Usuario", aud.Entidad);
        Assert.Equal("REGISTRO_ASESOR", aud.Accion);
        Assert.Null(aud.DatosAnteriores);
        Assert.Equal("PENDIENTE", JsonPlano(aud.DatosNuevos)["estado"]);
        Assert.InRange(aud.CreadoEn, antes, DateTime.UtcNow.AddSeconds(1));
        // CA-061-13: ni contraseña, ni hash, ni documento.
        foreach (var texto in new[] { aud.DatosAnteriores ?? "", aud.DatosNuevos ?? "" })
        {
            Assert.DoesNotContain(numero, texto);
            Assert.DoesNotContain(PasswordValida, texto);
            Assert.DoesNotContain(u.PasswordHash!, texto);
        }
        Assert.Contains(factory.Logs, l => l.Contains("Auditoria registro_asesor exitoso") && l.Contains(id));
    }

    [Fact(DisplayName = "CA-043R-02: rol, roles, estadoCuenta o estadoAutorizacion extra se ignoran; queda ASESOR y PENDIENTE")]
    public async Task Campos_extra_se_ignoran()
    {
        var cuerpo = RegistroAsesor();
        cuerpo["rol"] = "ADMINISTRADOR";
        cuerpo["roles"] = new[] { "ADMINISTRADOR" };
        cuerpo["estadoCuenta"] = "ACTIVA";
        cuerpo["estadoAutorizacion"] = "APROBADO";
        var r = await RegistrarAsesor(_c, cuerpo);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var id = (await Json(r)).GetProperty("usuarioId").GetString()!;
        Assert.Equal(["ASESOR"], await RolesDe(factory, id));
        Assert.Equal("PENDIENTE", (await Usuario(factory, id)).EstadoCuenta);
        Assert.Equal("PENDIENTE", (await Perfil(factory, id))!.EstadoAutorizacion);
        Assert.Equal(HttpStatusCode.Forbidden, (await Login(_c, (string)cuerpo["email"]!, PasswordValida)).StatusCode);
    }

    [Theory(DisplayName = "CA-043R-04: datos básicos inválidos -> 400 con los mismos textos que el registro de comprador; no se crea nada")]
    [InlineData("nombreCompleto", "A")]
    [InlineData("nombreCompleto", "")]
    [InlineData("email", "no-es-un-correo")]
    [InlineData("telefono", "abc123")]
    [InlineData("password", "corta1A")]
    [InlineData("password", "sinmayuscula2026")]
    public async Task Datos_basicos_invalidos(string campo, string valor)
    {
        var asesor = RegistroAsesor();
        asesor[campo] = valor;
        var comprador = new Dictionary<string, object?>
        {
            ["nombreCompleto"] = asesor["nombreCompleto"], ["email"] = CorreoUnico(),
            ["telefono"] = asesor["telefono"], ["password"] = asesor["password"]
        };
        comprador[campo] = valor;

        var rA = await RegistrarAsesor(_c, asesor);
        var rC = await Registrar(_c, comprador);
        Assert.Equal(HttpStatusCode.BadRequest, rA.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, rC.StatusCode);
        Assert.Equal(await ErroresDe(rC, campo), await ErroresDe(rA, campo));
        if (campo != "email")
            Assert.Equal(0, await UsuariosConCorreo((string)asesor["email"]!));
        Assert.Equal(0, await PerfilesConDocumento("CC", (string)asesor["numeroDocumento"]!));
    }

    [Theory(DisplayName = "CA-043R-05: tipoDocumento ausente, vacío o fuera de la lista -> 400")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("XX")]
    [InlineData("TI")]
    public async Task Tipo_documento_invalido(string? tipo)
    {
        var r = await RegistrarAsesor(_c, RegistroAsesor(tipo: tipo));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal([MsgTipo], await ErroresDe(r, "tipoDocumento"));
    }

    [Theory(DisplayName = "CA-043R-05: CC, CE, NIT y PAS -> 201")]
    [InlineData("CC")]
    [InlineData("CE")]
    [InlineData("NIT")]
    [InlineData("PAS")]
    public async Task Tipo_documento_valido(string tipo)
    {
        var r = await RegistrarAsesor(_c, RegistroAsesor(tipo: tipo));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var id = (await Json(r)).GetProperty("usuarioId").GetString()!;
        Assert.Equal(tipo, (await Perfil(factory, id))!.TipoDocumento);
    }

    [Theory(DisplayName = "CA-043R-06: numeroDocumento vacío, corto, largo, con punto, espacio interno o símbolos -> 400")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("123")]
    [InlineData("1234567890123456789012345678901")]
    [InlineData("10.123.456")]
    [InlineData("10 123 456")]
    [InlineData("123#45")]
    public async Task Numero_documento_invalido(string? numero)
    {
        var cuerpo = RegistroAsesor();
        cuerpo["numeroDocumento"] = numero;
        var r = await RegistrarAsesor(_c, cuerpo);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal([MsgNumero], await ErroresDe(r, "numeroDocumento"));
    }

    [Fact(DisplayName = "CA-043R-07: numeroDocumento con espacios alrededor, con guion, de 4 y de 30 caracteres -> 201; se guarda recortado")]
    public async Task Numero_documento_valido()
    {
        var base10 = DocumentoUnico();
        var casos = new (string Enviado, string Guardado)[]
        {
            ("  " + base10 + "  ", base10),
            ("AB-" + base10[..5], "AB-" + base10[..5]),
            (base10[..4], base10[..4]),
            (base10 + base10 + base10, base10 + base10 + base10)
        };
        foreach (var (enviado, guardado) in casos)
        {
            var r = await RegistrarAsesor(_c, RegistroAsesor(numero: enviado, tipo: "PAS"));
            Assert.True(r.StatusCode == HttpStatusCode.Created, $"'{enviado}' -> {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
            var id = (await Json(r)).GetProperty("usuarioId").GetString()!;
            Assert.Equal(guardado, (await Perfil(factory, id))!.NumeroDocumento);
        }
    }

    [Fact(DisplayName = "CA-043R-08: nombreComercial ausente, null o de 150 -> 201; de 151 -> 400")]
    public async Task Nombre_comercial()
    {
        var ausente = RegistroAsesor();
        ausente.Remove("nombreComercial");
        var r1 = await RegistrarAsesor(_c, ausente);
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Null((await Perfil(factory, (await Json(r1)).GetProperty("usuarioId").GetString()!))!.NombreComercial);

        var r2 = await RegistrarAsesor(_c, RegistroAsesor(comercial: null));
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
        Assert.Null((await Perfil(factory, (await Json(r2)).GetProperty("usuarioId").GetString()!))!.NombreComercial);

        var r3 = await RegistrarAsesor(_c, RegistroAsesor(comercial: new string('c', 150)));
        Assert.Equal(HttpStatusCode.Created, r3.StatusCode);
        Assert.Equal(150, (await Perfil(factory, (await Json(r3)).GetProperty("usuarioId").GetString()!))!.NombreComercial!.Length);

        var r4 = await RegistrarAsesor(_c, RegistroAsesor(comercial: new string('c', 151)));
        Assert.Equal(HttpStatusCode.BadRequest, r4.StatusCode);
        Assert.Equal([MsgComercial], await ErroresDe(r4, "nombreComercial"));
    }

    [Fact(DisplayName = "CA-043R-09: varios campos inválidos -> 400 con todas las claves en camelCase")]
    public async Task Varios_campos_invalidos()
    {
        var cuerpo = RegistroAsesor(email: "malo", numero: "1#", tipo: "XX", comercial: new string('x', 151));
        var r = await RegistrarAsesor(_c, cuerpo);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var errores = (await Json(r)).GetProperty("errores").EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "email", "nombreComercial", "numeroDocumento", "tipoDocumento" }, errores);
    }

    [Theory(DisplayName = "CA-043R-10: cuerpo vacío, {}, JSON malformado o tipo incorrecto -> 400 { error }, nunca 500")]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{ no es json")]
    [InlineData("{\"tipoDocumento\": 5}")]
    public async Task Cuerpo_invalido(string cuerpo)
    {
        var r = await _c.PostAsync("/api/auth/registro/asesor", new StringContent(cuerpo, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True((await Json(r)).TryGetProperty("error", out _), texto);
        Assert.DoesNotContain("Exception", texto);
    }

    [Fact(DisplayName = "CA-043R-11, CA-N03-R05: correo de un comprador o asesor existente (también en mayúsculas y con espacios) -> 409; el existente no cambia")]
    public async Task Correo_duplicado()
    {
        var (idComprador, emailComprador) = await CrearComprador(_c);
        var (idAsesor, emailAsesor, _) = await CrearAsesorPendiente(_c);

        foreach (var (email, id, rol, estado) in new[]
                 {
                     (emailComprador, idComprador, "COMPRADOR", "ACTIVA"),
                     ("  " + emailComprador.ToUpperInvariant() + " ", idComprador, "COMPRADOR", "ACTIVA"),
                     (emailAsesor, idAsesor, "ASESOR", "PENDIENTE"),
                     (" " + emailAsesor.ToUpperInvariant() + "  ", idAsesor, "ASESOR", "PENDIENTE")
                 })
        {
            var numero = DocumentoUnico();
            var r = await RegistrarAsesor(_c, RegistroAsesor(email, numero));
            await AfirmarError(r, HttpStatusCode.Conflict, MsgCorreoDuplicado);
            Assert.Equal(0, await PerfilesConDocumento("CC", numero));
            Assert.Equal([rol], await RolesDe(factory, id));
            Assert.Equal(estado, (await Usuario(factory, id)).EstadoCuenta);
        }
        Assert.Equal(1, await UsuariosConCorreo(emailComprador));
        Assert.Equal(1, await UsuariosConCorreo(emailAsesor));
        Assert.Null(await Perfil(factory, idComprador));
    }

    [Fact(DisplayName = "CA-043R-13: mismo tipo y número (con espacios o minúsculas) -> 409 con errores.numeroDocumento; otro tipo -> 201")]
    public async Task Documento_duplicado()
    {
        var numero = "AB-" + DocumentoUnico();
        await RegistrarAsesor(_c, RegistroAsesor(numero: numero, tipo: "CC"));

        foreach (var variante in new[] { numero, "  " + numero + " ", numero.ToLowerInvariant() })
        {
            var email = CorreoUnico("asesor");
            var r = await RegistrarAsesor(_c, RegistroAsesor(email, variante, tipo: "cc"));
            await AfirmarError(r, HttpStatusCode.Conflict, MsgDocumentoDuplicado);
            Assert.Equal([MsgDocumentoDuplicado], await ErroresDe(r, "numeroDocumento"));
            Assert.Equal(0, await UsuariosConCorreo(email));
        }
        Assert.Equal(1, await PerfilesConDocumento("CC", numero));

        var otroTipo = await RegistrarAsesor(_c, RegistroAsesor(numero: numero, tipo: "CE"));
        Assert.Equal(HttpStatusCode.Created, otroTipo.StatusCode);
    }

    private async Task<HttpResponseMessage[]> EnParalelo(int n, Func<int, object> cuerpo)
    {
        using var barrera = new Barrier(n);
        var tareas = Enumerable.Range(0, n).Select(i => Task.Run(async () =>
        {
            var cliente = factory.CreateClient();
            barrera.SignalAndWait(TimeSpan.FromSeconds(10));
            return await RegistrarAsesor(cliente, cuerpo(i));
        })).ToArray();
        return await Task.WhenAll(tareas);
    }

    [Fact(DisplayName = "CA-043R-12: registros simultáneos con el mismo correo -> un 201 y el resto 409; un solo usuario y perfil; nunca 500")]
    public async Task Simultaneos_mismo_correo()
    {
        var email = CorreoUnico("simultaneo");
        var respuestas = await EnParalelo(6, _ => RegistroAsesor(email, DocumentoUnico()));
        var codigos = string.Join(",", respuestas.Select(r => (int)r.StatusCode));
        Assert.True(respuestas.Count(r => r.StatusCode == HttpStatusCode.Created) == 1, codigos);
        foreach (var r in respuestas.Where(r => r.StatusCode != HttpStatusCode.Created))
            await AfirmarError(r, HttpStatusCode.Conflict, MsgCorreoDuplicado);
        Assert.Equal(1, await UsuariosConCorreo(email));
        var id = (await Db(factory, db => db.Users.SingleAsync(u => u.Email == email))).Id;
        Assert.Equal(1, await Db(factory, db => db.PerfilesAsesor.CountAsync(p => p.UsuarioId == id)));
    }

    [Fact(DisplayName = "CA-043R-12/13: registros simultáneos con el mismo documento -> un 201 y el resto 409 de documento; nunca 500")]
    public async Task Simultaneos_mismo_documento()
    {
        var numero = DocumentoUnico();
        var correos = Enumerable.Range(0, 6).Select(_ => CorreoUnico("simdoc")).ToArray();
        var respuestas = await EnParalelo(6, i => RegistroAsesor(correos[i], numero));
        var codigos = string.Join(",", respuestas.Select(r => (int)r.StatusCode));
        Assert.True(respuestas.Count(r => r.StatusCode == HttpStatusCode.Created) == 1, codigos);
        foreach (var r in respuestas.Where(r => r.StatusCode != HttpStatusCode.Created))
            await AfirmarError(r, HttpStatusCode.Conflict, MsgDocumentoDuplicado);
        Assert.Equal(1, await PerfilesConDocumento("CC", numero));
        Assert.Equal(1, await Db(factory, db => db.Users.CountAsync(u => correos.Contains(u.Email!))));
    }

    [Fact(DisplayName = "CA-043R-14: si falla la escritura de la auditoría, no queda usuario, rol, perfil ni auditoría (todo o nada)")]
    public async Task Registro_es_atomico()
    {
        // Base propia para no afectar al resto: un disparador SQLite simula el fallo al escribir auditoria_eventos.
        await using var f = new CamionApiFactory();
        var c = f.CreateClient();
        await Db(f, db => db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER fallo_auditoria BEFORE INSERT ON auditoria_eventos BEGIN SELECT RAISE(ABORT, 'fallo simulado'); END;"));

        var email = CorreoUnico("atomico");
        var numero = DocumentoUnico();
        var r = await RegistrarAsesor(c, RegistroAsesor(email, numero));
        Assert.Equal(HttpStatusCode.InternalServerError, r.StatusCode);
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True((await Json(r)).TryGetProperty("error", out _), texto);
        Assert.DoesNotContain("fallo simulado", texto);

        Assert.Equal(0, await Db(f, db => db.Users.CountAsync(u => u.Email == email)));
        Assert.Equal(0, await Db(f, db => db.UserRoles.CountAsync()));
        Assert.Equal(0, await Db(f, db => db.PerfilesAsesor.CountAsync()));
        Assert.Equal(0, await Db(f, db => db.AuditoriaEventos.CountAsync()));
    }
}
