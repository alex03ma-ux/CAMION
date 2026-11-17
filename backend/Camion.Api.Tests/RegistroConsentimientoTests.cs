using System.Net;
using System.Text;
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
/// P9 (HU-062, RN-SE-004, D-44; CA-P9-06 a CA-P9-11, CA-P9-13): autorización de datos obligatoria en el registro de comprador y
/// en la inscripción de asesor, con su fila en consentimientos_datos en la misma transacción. SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class RegistroConsentimientoTests(CamionApiFactory factory) : IClassFixture<CamionApiFactory>
{
    private readonly HttpClient _c = factory.CreateClient();

    private const string MsgAutorizacion = "Debes autorizar el tratamiento de tus datos para crear tu cuenta.";
    private const string VersionBorrador = "BORRADOR-2026-11-04";
    private const string RutaComprador = "/api/auth/registro/comprador";
    private const string RutaAsesor = "/api/auth/registro/asesor";

    private static Dictionary<string, object?> CuerpoComprador(string? email = null) => new()
    {
        ["nombreCompleto"] = "Ana Consentimiento",
        ["email"] = email ?? CorreoUnico(),
        ["telefono"] = TelefonoValido,
        ["password"] = PasswordValida,
        ["autorizacionDatos"] = true
    };

    private Dictionary<string, object?> Cuerpo(string ruta) => ruta == RutaComprador ? CuerpoComprador() : RegistroAsesor();

    private Task<int> Consentimientos(CamionApiFactory? f = null) => Db(f ?? factory, db => db.ConsentimientosDatos.CountAsync());

    private Task<List<ConsentimientoDatos>> ConsentimientosDe(string usuarioId) =>
        Db(factory, db => db.ConsentimientosDatos.AsNoTracking().Where(c => c.UsuarioId == usuarioId).ToListAsync());

    private static Task<HttpResponseMessage> Post(HttpClient c, string ruta, object cuerpo) =>
        c.SendAsync(new HttpRequestMessage(HttpMethod.Post, ruta) { Content = System.Net.Http.Json.JsonContent.Create(cuerpo) });

    [Fact(DisplayName = "CA-P9-06: registro de comprador con autorizacionDatos = true -> 201 y una fila REGISTRO_COMPRADOR con versión, hora UTC, usuario y entidad Usuario")]
    public async Task Comprador_guarda_consentimiento()
    {
        var antes = DateTime.UtcNow.AddSeconds(-5);
        var r = await Post(_c, RutaComprador, CuerpoComprador());
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var id = (await Json(r)).GetProperty("usuarioId").GetString()!;

        var c = Assert.Single(await ConsentimientosDe(id));
        Assert.Equal(ConsentimientoDatos.RegistroComprador, c.Formulario);
        Assert.Equal(VersionBorrador, c.VersionPolitica);
        Assert.Equal("Usuario", c.Entidad);
        Assert.Equal(id, c.EntidadId);
        Assert.Equal(id, c.UsuarioId);
        Assert.InRange(c.AceptadoEn, antes, DateTime.UtcNow.AddSeconds(5));
    }

    [Fact(DisplayName = "CA-P9-07: inscripción de asesor con autorizacionDatos = true -> 201 PENDIENTE, auditoría RegistroAsesor y una fila INSCRIPCION_ASESOR")]
    public async Task Asesor_guarda_consentimiento()
    {
        var r = await RegistrarAsesor(_c, RegistroAsesor());
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var id = (await Json(r)).GetProperty("usuarioId").GetString()!;
        Assert.Equal(EstadosCuenta.Pendiente, (await Usuario(factory, id)).EstadoCuenta);
        Assert.Single(await AuditoriaDe(factory, id));

        var c = Assert.Single(await ConsentimientosDe(id));
        Assert.Equal(ConsentimientoDatos.InscripcionAsesor, c.Formulario);
        Assert.Equal(VersionBorrador, c.VersionPolitica);
        Assert.Equal("Usuario", c.Entidad);
        Assert.Equal(id, c.EntidadId);
        Assert.Equal(id, c.UsuarioId);
    }

    public static TheoryData<string, string> AusenteNullFalse => new()
    {
        { RutaComprador, "ausente" }, { RutaComprador, "null" }, { RutaComprador, "false" },
        { RutaAsesor, "ausente" }, { RutaAsesor, "null" }, { RutaAsesor, "false" },
    };

    [Theory(DisplayName = "CA-P9-08: autorizacionDatos ausente, null o false -> 400 con errores.autorizacionDatos; no se crea usuario, perfil, rol ni consentimiento")]
    [MemberData(nameof(AusenteNullFalse))]
    public async Task Sin_autorizacion_da_400(string ruta, string caso)
    {
        var cuerpo = Cuerpo(ruta);
        if (caso == "ausente") cuerpo.Remove("autorizacionDatos");
        else cuerpo["autorizacionDatos"] = caso == "null" ? null : false;
        var email = (string)cuerpo["email"]!;
        var antes = await Consentimientos();

        var r = await Post(_c, ruta, cuerpo);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("Revisa los datos marcados.", (await Json(r)).GetProperty("error").GetString());
        Assert.Equal([MsgAutorizacion], await ErroresDe(r, "autorizacionDatos"));
        Assert.Single((await Json(r)).GetProperty("errores").EnumerateObject());
        Assert.Equal(0, await Db(factory, db => db.Users.CountAsync(u => u.Email == email)));
        Assert.Equal(antes, await Consentimientos());
    }

    [Theory(DisplayName = "CA-P9-08: sin autorización y con otros campos inválidos -> 400 con todos los errores a la vez")]
    [InlineData(RutaComprador)]
    [InlineData(RutaAsesor)]
    public async Task Sin_autorizacion_con_otros_errores(string ruta)
    {
        var cuerpo = Cuerpo(ruta);
        cuerpo["autorizacionDatos"] = false;
        cuerpo["email"] = "no-es-correo";
        var r = await Post(_c, ruta, cuerpo);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal([MsgAutorizacion], await ErroresDe(r, "autorizacionDatos"));
        Assert.NotEmpty(await ErroresDe(r, "email"));
    }

    [Theory(DisplayName = "CA-P9-09: autorizacionDatos con un tipo no booleano -> 400 con el formato común; no se crea nada")]
    [InlineData(RutaComprador, "\"true\"")]
    [InlineData(RutaComprador, "1")]
    [InlineData(RutaAsesor, "\"true\"")]
    [InlineData(RutaAsesor, "1")]
    public async Task Tipo_no_booleano_da_400(string ruta, string valor)
    {
        var email = CorreoUnico("tipo");
        var json = ruta == RutaComprador
            ? $"{{\"nombreCompleto\":\"Ana Tipo\",\"email\":\"{email}\",\"telefono\":\"3001234567\",\"password\":\"{PasswordValida}\",\"autorizacionDatos\":{valor}}}"
            : $"{{\"nombreCompleto\":\"Carlos Tipo\",\"email\":\"{email}\",\"telefono\":\"3001234567\",\"password\":\"{PasswordValida}\"," +
              $"\"tipoDocumento\":\"CC\",\"numeroDocumento\":\"{DocumentoUnico()}\",\"autorizacionDatos\":{valor}}}";
        var antes = await Consentimientos();
        var r = await _c.PostAsync(ruta, new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("La solicitud no tiene un formato válido.", (await Json(r)).GetProperty("error").GetString());
        Assert.Equal(0, await Db(factory, db => db.Users.CountAsync(u => u.Email == email)));
        Assert.Equal(antes, await Consentimientos());
    }

    [Fact(DisplayName = "CA-P9-10: correo repetido (comprador o asesor) o documento repetido, con la casilla marcada -> 409 y ningún consentimiento nuevo")]
    public async Task Duplicados_no_guardan_consentimiento()
    {
        var (_, emailComprador) = await CrearComprador(_c);
        var (_, _, numero) = await CrearAsesorPendiente(_c);
        var antes = await Consentimientos();

        Assert.Equal(HttpStatusCode.Conflict, (await Post(_c, RutaComprador, CuerpoComprador(emailComprador))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await RegistrarAsesor(_c, RegistroAsesor(emailComprador))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await RegistrarAsesor(_c, RegistroAsesor(numero: numero))).StatusCode);
        Assert.Equal(antes, await Consentimientos());
    }

    [Theory(DisplayName = "CA-P9-10: registros simultáneos con el mismo correo -> un 201 y un solo consentimiento")]
    [InlineData(RutaComprador)]
    [InlineData(RutaAsesor)]
    public async Task Simultaneos_un_solo_consentimiento(string ruta)
    {
        const int paralelos = 6;
        var email = CorreoUnico("simcons");
        var antes = await Consentimientos();
        using var barrera = new Barrier(paralelos);
        var respuestas = await Task.WhenAll(Enumerable.Range(0, paralelos).Select(_ => Task.Run(async () =>
        {
            var cuerpo = ruta == RutaComprador ? CuerpoComprador(email) : RegistroAsesor(email);
            using var cliente = factory.CreateClient();
            barrera.SignalAndWait();
            return await Post(cliente, ruta, cuerpo);
        })));
        var codigos = string.Join(",", respuestas.Select(r => (int)r.StatusCode));
        Assert.True(respuestas.Count(r => r.StatusCode == HttpStatusCode.Created) == 1, codigos);
        Assert.True(respuestas.All(r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict), codigos);
        Assert.Equal(antes + 1, await Consentimientos());
        var id = (await Db(factory, db => db.Users.SingleAsync(u => u.Email == email))).Id;
        Assert.Single(await ConsentimientosDe(id));
    }

    [Theory(DisplayName = "CA-P9-11: si falla la escritura del consentimiento se revierte todo (sin usuario, perfil, rol ni auditoría) y 500 genérico")]
    [InlineData(RutaComprador)]
    [InlineData(RutaAsesor)]
    public async Task Fallo_del_consentimiento_revierte_todo(string ruta)
    {
        // Base propia: un disparador SQLite (nunca MariaDB) hace fallar solo la inserción del consentimiento del registro.
        await using var f = new CamionApiFactory();
        var c = f.CreateClient();
        await Db(f, db => db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER falla_consentimiento_registro BEFORE INSERT ON consentimientos_datos " +
            "WHEN NEW.formulario IN ('REGISTRO_COMPRADOR','INSCRIPCION_ASESOR') BEGIN SELECT RAISE(ABORT, 'falla de prueba'); END;"));
        var cuerpo = Cuerpo(ruta);
        var email = (string)cuerpo["email"]!;

        var r = await Post(c, ruta, cuerpo);
        Assert.Equal(HttpStatusCode.InternalServerError, r.StatusCode);
        var texto = await r.Content.ReadAsStringAsync();
        Assert.Equal("No se pudo completar la operación. Intenta de nuevo en unos minutos.", (await Json(r)).GetProperty("error").GetString());
        Assert.DoesNotContain("falla de prueba", texto);

        Assert.Equal(0, await Db(f, db => db.Users.CountAsync(u => u.Email == email)));
        Assert.Equal(0, await Db(f, db => db.PerfilesComprador.CountAsync()));
        Assert.Equal(0, await Db(f, db => db.PerfilesAsesor.CountAsync()));
        Assert.Equal(0, await Db(f, db => db.UserRoles.CountAsync()));
        Assert.Equal(0, await Db(f, db => db.AuditoriaEventos.CountAsync()));
        Assert.Equal(0, await Db(f, db => db.HistorialAutorizaciones.CountAsync()));
        Assert.Equal(0, await Consentimientos(f));
    }

    [Fact(DisplayName = "CA-P9-13: promover-admin y recuperar-admin no crean consentimientos")]
    public async Task Comandos_no_crean_consentimientos()
    {
        var (id, email) = await CrearComprador(_c, "Comprador Comandos");
        var antes = await Consentimientos();
        using (var scope = factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var promovido = await Camion.Api.Comandos.PromoverAdmin.EjecutarAsync(email,
                sp.GetRequiredService<UserManager<ApplicationUser>>(), sp.GetRequiredService<RoleManager<IdentityRole>>(),
                sp.GetRequiredService<CamionDbContext>());
            Assert.Equal(0, promovido.CodigoSalida);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var recuperado = await Camion.Api.Comandos.RecuperarAdmin.EjecutarAsync(email,
                sp.GetRequiredService<UserManager<ApplicationUser>>(), sp.GetRequiredService<CamionDbContext>(),
                sp.GetRequiredService<TimeProvider>(), CamionApiFactory.UrlPortalPruebas);
            Assert.Equal(0, recuperado.CodigoSalida);
        }
        Assert.Equal(antes, await Consentimientos());
        Assert.Single(await ConsentimientosDe(id));
    }
}
