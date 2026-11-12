using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Camion.Api.Data;
using Camion.Api.Endpoints;
using Camion.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Camion.Api.Tests.Infraestructura;

/// <summary>Reloj simulado (P3): fija la hora que ve el backend a través de <see cref="TimeProvider"/>.</summary>
public sealed class RelojFijo(DateTimeOffset ahora, TimeZoneInfo? zona = null) : TimeProvider
{
    public DateTimeOffset Ahora { get; set; } = ahora;
    public override DateTimeOffset GetUtcNow() => Ahora;
    public override TimeZoneInfo LocalTimeZone => zona ?? TimeZoneInfo.Utc;
}

/// <summary>
/// API del catálogo en memoria con un reloj fijo. Usa <see cref="CamionApiFactory"/> (SQLite en memoria; nunca MariaDB)
/// y reemplaza el <see cref="TimeProvider"/> con <c>ConfigureTestServices</c>.
/// </summary>
public sealed class CatalogoApi : IDisposable
{
    /// <summary>T: hora UTC fija de las pruebas.</summary>
    public static readonly DateTime T = new(2026, 10, 26, 15, 0, 0, DateTimeKind.Utc);

    public const string MsgFiltros = "Revisa los filtros.";
    public const string MsgNoDisponible = "Este vehículo no está disponible.";
    public const string MsgBuscar = "La búsqueda no puede tener más de 100 caracteres.";
    public const string MsgPrecio = "Escribe un precio válido en pesos (número entero mayor o igual a 0).";
    public const string MsgPrecioRango = "El precio máximo debe ser mayor o igual al mínimo.";
    public const string MsgAnio = "Escribe un año entre 1900 y 2200.";
    public const string MsgAnioRango = "El año final debe ser mayor o igual al inicial.";
    public const string MsgKm = "Escribe un kilometraje válido (número entero mayor o igual a 0).";
    public const string MsgLineaMarca = "El modelo no corresponde a la marca elegida.";
    public const string MsgOrden = "Orden no válido.";
    public const string MsgPagina = "Página o tamaño no válidos.";
    public const string MsgTipoId = "Categoría no válida.";
    public const string MsgMarcaId = "Marca no válida.";
    public const string MsgLineaId = "Modelo no válido.";

    public static readonly string[] Rutas =
        ["/api/catalogo/inicio", "/api/catalogo/publicaciones", "/api/catalogo/publicaciones/chevrolet-npr-2020", "/api/catalogo/filtros"];

    public CamionApiFactory Base { get; } = new();
    public WebApplicationFactory<Program> Factory { get; }
    public RelojFijo Reloj { get; }
    public HttpClient Cliente { get; }
    public Siembra Datos { get; private set; } = null!;

    public CatalogoApi(DateTimeOffset? ahora = null, TimeZoneInfo? zona = null)
    {
        Reloj = new RelojFijo(ahora ?? new DateTimeOffset(T), zona);
        Factory = Base.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(Reloj)));
        Cliente = Factory.CreateClient();
    }

    /// <summary>Crea la API, siembra los catálogos base y, si se pide, los datos de referencia de P3.md.</summary>
    public static async Task<CatalogoApi> Crear(bool visibles = true, bool noVisibles = true, Action<Siembra>? extra = null,
        DateTimeOffset? ahora = null, TimeZoneInfo? zona = null)
    {
        var api = new CatalogoApi(ahora, zona);
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CamionDbContext>();
        var s = await Siembra.Base(db);
        if (visibles) s.Visibles();
        if (noVisibles) s.NoVisibles();
        extra?.Invoke(s);
        await db.SaveChangesAsync();
        api.Datos = s;
        return api;
    }

    public async Task Db(Func<CamionDbContext, Task> accion)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CamionDbContext>();
        await accion(db);
        await db.SaveChangesAsync();
    }

    public async Task<T2> Leer<T2>(Func<CamionDbContext, Task<T2>> consulta)
    {
        using var scope = Factory.Services.CreateScope();
        return await consulta(scope.ServiceProvider.GetRequiredService<CamionDbContext>());
    }

    public Task<HttpResponseMessage> Get(string url, string? token = null) =>
        Cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, url, token));

    public async Task<JsonElement> Ok(string url, string? token = null) => await AyudasP2.Ok(await Get(url, token));

    public Task<JsonElement> Inicio() => Ok("/api/catalogo/inicio");
    public Task<JsonElement> Filtros() => Ok("/api/catalogo/filtros");
    public Task<JsonElement> Buscar(string query = "") => Ok("/api/catalogo/publicaciones" + query);
    public Task<HttpResponseMessage> Detalle(string slug) => Get("/api/catalogo/publicaciones/" + Uri.EscapeDataString(slug));

    /// <summary>Slugs de la búsqueda (con tamano=48 para ver todo salvo que la consulta diga otra cosa).</summary>
    public async Task<string[]> SlugsBusqueda(string query = "")
    {
        var json = await Buscar(query);
        return CatalogoJson.Slugs(json.GetProperty("items"));
    }

    /// <summary>400 con { error: "Revisa los filtros.", errores: { campo: [mensaje] } }; nunca 500.</summary>
    public async Task Afirmar400(string query, string campo, string mensaje)
    {
        var r = await Get("/api/catalogo/publicaciones" + query);
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{query}: se esperaba 400 y llegó {(int)r.StatusCode}: {texto}");
        var json = JsonDocument.Parse(texto).RootElement;
        Assert.Equal(MsgFiltros, json.GetProperty("error").GetString());
        Assert.True(json.GetProperty("errores").TryGetProperty(campo, out var lista), $"{query}: falta errores.{campo} en {texto}");
        Assert.Equal(new[] { mensaje }, lista.EnumerateArray().Select(x => x.GetString()).ToArray());
    }

    /// <summary>Token firmado con la clave de pruebas; permite fijar la caducidad (p. ej. un token caducado).</summary>
    public string Firmar(string usuarioId, int sessionVersion, string rol, DateTime? expira = null)
    {
        var fin = expira ?? DateTime.UtcNow.AddHours(1);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuarioId),
            new Claim(ClaimTypes.NameIdentifier, usuarioId),
            new Claim(AuthEndpoints.ClaimSessionVersion, sessionVersion.ToString()),
            new Claim(ClaimTypes.Role, rol)
        };
        var token = new JwtSecurityToken(AuthEndpoints.Emisor, AuthEndpoints.Audiencia, claims,
            notBefore: fin.AddHours(-2), expires: fin,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Base.JwtKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Dispose()
    {
        Cliente.Dispose();
        Factory.Dispose();
        Base.Dispose();
    }
}

/// <summary>Fixture compartida (solo lectura) con los datos de referencia de P3.md: V1 a V4 y todas las no visibles.</summary>
public sealed class CatalogoReferencia : IAsyncLifetime
{
    public CatalogoApi Api { get; private set; } = null!;
    public async Task InitializeAsync() => Api = await CatalogoApi.Crear();
    public Task DisposeAsync()
    {
        Api.Dispose();
        return Task.CompletedTask;
    }
}

public sealed record AsesorSembrado(ApplicationUser Usuario, PerfilAsesor Perfil, Suscripcion Plan);

/// <summary>
/// Siembra datos ficticios en la base SQLite en memoria (P3.md, "Datos de prueba de referencia"). Los catálogos se
/// cargan en desorden a propósito (CA-FIL-04). Ningún dato es real.
/// </summary>
public sealed class Siembra
{
    private static DateTime T => CatalogoApi.T;
    private readonly CamionDbContext _db;

    public TipoVehiculo Carro = null!, Camion = null!, Carroceria = null!, Maquinaria = null!, Remolque = null!;
    public Marca Chevrolet = null!, Kenworth = null!, Renault = null!, Hino = null!, Dina = null!, Andinas = null!,
        Caterpillar = null!, Bobcat = null!;
    public Linea Npr = null!, Nhr = null!, T800 = null!, Logan = null!, L500 = null!, DinaL = null!;
    public TipoCarroceria Furgon = null!;
    public Combustible Diesel = null!;
    public Municipio Medellin = null!;
    public AsesorSembrado A1 = null!, A2 = null!, A3 = null!, A4 = null!;
    public Suscripcion PlanVencido = null!, PlanCancelado = null!, PlanEstadoVencida = null!, PlanFinExacto = null!,
        PlanSinFin = null!;
    public Publicacion V1 = null!, V2 = null!, V3 = null!, V4 = null!;

    /// <summary>Casos no visibles: slug → condición que falla (P3.md N1 a N11 + condición 6 + D-11).</summary>
    public static readonly string[] CasosNoVisibles =
    [
        "n01-vendida", "n02-oculta", "n03-borrador", "n04-eliminada", "n05-vencida-fin-exacto", "n06-sin-vigencia",
        "n07-vigencia-futura", "n08-asesor-bloqueado", "n09-asesor-rechazado", "n10-asesor-pendiente",
        "n11-titulo-espacios", "n13-titulo-vacio", "n14-plan-vencido", "n15-plan-cancelado", "n16-plan-estado-vencida",
        "n17-precio-cero", "n18-reservado-vencido", "n19-reservado-asesor-bloqueado", "n20-vencida-hace-1min",
        "n21-plan-fin-exacto", "n22-precio-nulo", "n23-titulo-nulo",
        // P4b (D-41): plan ACTIVA sin fecha de fin (no es vigente) y plan SUSPENDIDA.
        "n24-plan-sin-fin", "n25-plan-suspendido"
    ];

    private global::Camion.Api.Models.Plan _planBase = null!;
    private int _asesoresExtra;

    private Siembra(CamionDbContext db) => _db = db;

    public static async Task<Siembra> Base(CamionDbContext db)
    {
        var s = new Siembra(db);
        // Desorden a propósito (CA-FIL-04).
        s.Maquinaria = new TipoVehiculo { Nombre = "Maquinaria" };
        s.Carro = new TipoVehiculo { Nombre = "Carro" };
        s.Carroceria = new TipoVehiculo { Nombre = "Carrocería" };
        s.Camion = new TipoVehiculo { Nombre = "Camión" };
        s.Remolque = new TipoVehiculo { Nombre = "Remolque", Activo = false };
        db.TiposVehiculo.AddRange(s.Maquinaria, s.Carro, s.Carroceria, s.Camion, s.Remolque);

        s.Renault = new Marca { Nombre = "Renault" };
        s.Chevrolet = new Marca { Nombre = "Chevrolet" };
        s.Kenworth = new Marca { Nombre = "Kenworth" };
        s.Hino = new Marca { Nombre = "Hino" };
        s.Dina = new Marca { Nombre = "Dina", Activo = false };
        s.Andinas = new Marca { Nombre = "Carrocerías Andinas" };
        s.Caterpillar = new Marca { Nombre = "Caterpillar" };
        s.Bobcat = new Marca { Nombre = "Bobcat" };
        db.Marcas.AddRange(s.Renault, s.Chevrolet, s.Kenworth, s.Hino, s.Dina, s.Andinas, s.Caterpillar, s.Bobcat);
        s.Npr = new Linea { Marca = s.Chevrolet, Nombre = "NPR" };
        s.Nhr = new Linea { Marca = s.Chevrolet, Nombre = "NHR" };
        s.T800 = new Linea { Marca = s.Kenworth, Nombre = "T800" };
        s.Logan = new Linea { Marca = s.Renault, Nombre = "Logan" };
        s.L500 = new Linea { Marca = s.Hino, Nombre = "500" };
        s.DinaL = new Linea { Marca = s.Dina, Nombre = "Linner" };
        db.Lineas.AddRange(s.Npr, s.Nhr, s.T800, s.Logan, s.L500, s.DinaL);

        s.Furgon = new TipoCarroceria { Nombre = "Furgón" };
        s.Diesel = new Combustible { Nombre = "Diésel" };
        db.TiposCarroceria.Add(s.Furgon);
        db.Combustibles.Add(s.Diesel);
        var dep = new Departamento { Codigo = "05", Nombre = "Antioquia" };
        s.Medellin = new Municipio { Departamento = dep, Codigo = "05001", Nombre = "Medellín" };
        db.Municipios.Add(s.Medellin);

        var usuarios = new[]
        {
            Usuario("a1", EstadosCuenta.Activa), Usuario("a2", EstadosCuenta.Bloqueada),
            Usuario("a3", EstadosCuenta.Rechazada), Usuario("a4", EstadosCuenta.Pendiente)
        };
        db.Users.AddRange(usuarios);
        var perfiles = new[]
        {
            Perfil(usuarios[0], PerfilAsesor.Aprobado, "9000000001"), Perfil(usuarios[1], PerfilAsesor.Bloqueado, "9000000002"),
            Perfil(usuarios[2], PerfilAsesor.Rechazado, "9000000003"), Perfil(usuarios[3], PerfilAsesor.Pendiente, "9000000004")
        };
        db.PerfilesAsesor.AddRange(perfiles);
        await db.SaveChangesAsync();

        // P4p: suscripciones.plan_id ya es FK a planes en el modelo; se siembra un plan ficticio.
        var plan = new global::Camion.Api.Models.Plan { Nombre = "Plan Ficticio P3", PrecioCop = 0, DuracionMeses = 1, LimitePublicaciones = 10 };
        db.Planes.Add(plan);
        await db.SaveChangesAsync();
        s._planBase = plan;
        Suscripcion Plan(PerfilAsesor p, string estado, DateTime? fin) =>
            new() { AsesorId = p.Id, PlanId = plan.Id, Estado = estado, InicioVigencia = T.AddDays(-30), FinVigencia = fin };
        var planes = perfiles.Select(p => Plan(p, Suscripcion.Activa, T.AddDays(30))).ToArray();
        s.PlanVencido = Plan(perfiles[0], Suscripcion.Activa, T.AddMinutes(-1));
        s.PlanCancelado = Plan(perfiles[0], "CANCELADA", T.AddDays(30));
        s.PlanEstadoVencida = Plan(perfiles[0], "VENCIDA", null);
        s.PlanFinExacto = Plan(perfiles[0], Suscripcion.Activa, T);
        s.PlanSinFin = Plan(perfiles[0], Suscripcion.Activa, null);
        db.Suscripciones.AddRange(planes);
        db.Suscripciones.AddRange(s.PlanVencido, s.PlanCancelado, s.PlanEstadoVencida, s.PlanFinExacto, s.PlanSinFin);
        await db.SaveChangesAsync();

        s.A1 = new(usuarios[0], perfiles[0], planes[0]);
        s.A2 = new(usuarios[1], perfiles[1], planes[1]);
        s.A3 = new(usuarios[2], perfiles[2], planes[2]);
        s.A4 = new(usuarios[3], perfiles[3], planes[3]);
        return s;
    }

    private static ApplicationUser Usuario(string clave, string estado) => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserName = $"{clave}.asesor@ejemplo.test",
        Email = $"{clave}.asesor@ejemplo.test",
        PhoneNumber = $"310 555 01{clave[1]}{clave[1]}",
        NombreCompleto = $"Asesor {clave.ToUpperInvariant()} Ficticio",
        EstadoCuenta = estado
    };

    private static PerfilAsesor Perfil(ApplicationUser u, string estado, string documento) => new()
    {
        UsuarioId = u.Id,
        TipoDocumento = "CC",
        NumeroDocumento = documento,
        NombreComercial = $"Camiones {u.NombreCompleto}",
        EstadoAutorizacion = estado
    };

    /// <summary>
    /// Publicación de prueba. Por defecto: de A1, PUBLICADA + DISPONIBLE, vigente [T-10d, T+20d) con el plan ACTIVA de su
    /// asesor, Camión Chevrolet NPR. <paramref name="vigente"/> = false no crea ninguna fila de vigencia.
    /// </summary>
    public Publicacion Pub(string slug, AsesorSembrado? asesor = null, TipoVehiculo? tipo = null, Marca? marca = null,
        Linea? linea = null, bool sinLinea = false, decimal? precio = 100_000_000m, short anio = 2020, int km = 50_000,
        string? titulo = "__por_defecto__", string estadoPub = "PUBLICADA", string estadoCom = "DISPONIBLE",
        DateTime? publicadaEn = null, bool vigente = true, Suscripcion? plan = null, bool negociable = false)
    {
        asesor ??= A1;
        marca ??= Chevrolet;
        if (linea is null && !sinLinea && marca == Chevrolet) linea = Npr;
        var v = new Vehiculo
        {
            AsesorId = asesor.Perfil.Id,
            Marca = marca,
            Linea = sinLinea ? null : linea,
            TipoVehiculo = tipo ?? Camion,
            Municipio = Medellin,
            ModeloAnio = anio,
            Kilometraje = km
        };
        var p = new Publicacion
        {
            Vehiculo = v,
            AsesorId = asesor.Perfil.Id,
            SlugPublico = slug,
            Titulo = titulo == "__por_defecto__" ? $"{marca.Nombre} {v.Linea?.Nombre} {slug}" : titulo,
            PrecioCop = precio,
            Negociable = negociable,
            EstadoPublicacion = estadoPub,
            EstadoComercial = estadoCom,
            PublicadaEn = publicadaEn ?? T.AddDays(-5),
            OcultadaEn = estadoPub == "OCULTA" ? T.AddDays(-1) : null,
            EliminadaEn = estadoPub == "ELIMINADA" ? T.AddDays(-1) : null
        };
        if (vigente) Vigencia(p, T.AddDays(-10), T.AddDays(20), plan ?? asesor.Plan);
        _db.Publicaciones.Add(p);
        return p;
    }

    /// <summary>
    /// P4b (D-41): asesor con cuenta ACTIVA cuya ÚNICA suscripción es la indicada. Desde P4b la visibilidad depende del
    /// plan del asesor (no de la suscripción de cada vigencia), así que los casos "plan vencido/cancelado/..." necesitan un
    /// asesor que no tenga además el plan vigente de A1. Guarda en la base al momento (ids).
    /// </summary>
    public AsesorSembrado AsesorConPlan(string estado, DateTime? fin, DateTime? inicio = null)
    {
        var n = ++_asesoresExtra;
        var u = Usuario($"x{n}", EstadosCuenta.Activa);
        var perfil = Perfil(u, PerfilAsesor.Aprobado, $"91{n:D8}");
        _db.Users.Add(u);
        _db.PerfilesAsesor.Add(perfil);
        _db.SaveChanges();
        var sus = new Suscripcion
        {
            AsesorId = perfil.Id, PlanId = _planBase.Id, Estado = estado, InicioVigencia = inicio ?? T.AddDays(-30), FinVigencia = fin
        };
        _db.Suscripciones.Add(sus);
        _db.SaveChanges();
        return new(u, perfil, sus);
    }

    public HistorialVigencia Vigencia(Publicacion p, DateTime inicio, DateTime fin, Suscripcion? plan = null)
    {
        var h = new HistorialVigencia { SuscripcionId = (plan ?? A1.Plan).Id, Inicio = inicio, Fin = fin, Motivo = "ALTA" };
        p.Vigencias.Add(h);
        return h;
    }

    public void Destacar(Publicacion p, DateTime inicio, DateTime fin, string estado = PublicacionDestacada.Activa) =>
        p.Destacados.Add(new PublicacionDestacada { Inicio = inicio, Fin = fin, Estado = estado });

    public void Foto(Publicacion p, string clave, byte orden, bool portada = false) =>
        p.Fotos.Add(new FotoPublicacion { ArchivoClave = clave, Orden = orden, EsPortada = portada });

    /// <summary>V1 a V4 de P3.md.</summary>
    public void Visibles()
    {
        V1 = Pub("chevrolet-npr-2020", tipo: Camion, marca: Chevrolet, linea: Npr, precio: 120_000_000m, anio: 2020, km: 85_000,
            titulo: "Chevrolet NPR 2020 furgón", publicadaEn: T.AddDays(-1));
        V1.DescripcionComercial = "Camión en buen estado, listo para trabajar.";
        V1.Vehiculo!.DescripcionTecnica = "Motor revisado, llantas nuevas.";
        V1.Vehiculo.Carroceria = Furgon;
        V1.Vehiculo.Combustible = Diesel;
        V1.Vehiculo.CapacidadCargaKg = 4500.5m;
        V1.Vehiculo.PesoBrutoKg = 7500m;
        V1.Vehiculo.CilindrajeCc = 5193;
        V1.MotivoOcultamiento = null;
        Destacar(V1, T.AddDays(-1), T.AddDays(6));
        // Insertadas en desorden: el detalle debe ordenarlas por `orden`.
        Foto(V1, "pub/v1/cabina.jpg", 3);
        Foto(V1, "pub/v1/lateral.jpg", 1);
        Foto(V1, "pub/v1/frente.jpg", 2, portada: true);

        V2 = Pub("kenworth-t800-2018", tipo: Camion, marca: Kenworth, linea: T800, precio: 250_000_000m, anio: 2018, km: 300_000,
            titulo: "Kenworth T800 2018 tractomula", estadoCom: "RESERVADO", negociable: true, publicadaEn: T.AddDays(-2));
        // Sin ninguna es_portada: la portada es la de menor orden (3).
        Foto(V2, "pub/v2/b.jpg", 5);
        Foto(V2, "pub/v2/a.jpg", 3);

        V3 = Pub("renault-logan-2021", tipo: Carro, marca: Renault, linea: Logan, precio: 45_000_000m, anio: 2021, km: 40_000,
            titulo: "Renault Logan 2021 impecable", publicadaEn: T.AddDays(-3));

        V4 = Pub("chevrolet-camion-2015", tipo: Camion, marca: Chevrolet, sinLinea: true, precio: 80_000_000m, anio: 2015,
            km: 500_000, titulo: "Chevrolet 2015 estacas", publicadaEn: T.AddDays(-4));
        Destacar(V4, T.AddDays(-10), T.AddDays(-1)); // destacada vencida
        Foto(V4, "pub 1/foto ñ.jpg", 1);
    }

    /// <summary>Todos los casos no visibles (Chevrolet NHR o Hino, con precio, año y km extremos para CA-FIL-02).</summary>
    public void NoVisibles()
    {
        foreach (var caso in CasosNoVisibles) CasoNoVisible(caso);
    }

    public Publicacion CasoNoVisible(string caso)
    {
        Publicacion N(string? titulo = "__por_defecto__", AsesorSembrado? asesor = null, string estadoPub = "PUBLICADA",
            string estadoCom = "DISPONIBLE", bool vigente = true, Suscripcion? plan = null, decimal? precio = 1_000_000_000m,
            Marca? marca = null) =>
            Pub(caso, asesor: asesor, tipo: Camion, marca: marca ?? Chevrolet, linea: marca == Hino ? L500 : Nhr, precio: precio,
                anio: 1990, km: 900_000, titulo: titulo, estadoPub: estadoPub, estadoCom: estadoCom, vigente: vigente,
                plan: plan, publicadaEn: T.AddHours(-1));

        Publicacion p;
        switch (caso)
        {
            case "n01-vendida":
                p = N(estadoCom: Publicacion.Vendido, marca: Hino);
                Destacar(p, T.AddDays(-1), T.AddDays(5));
                break;
            case "n02-oculta": p = N(estadoPub: "OCULTA"); break;
            case "n03-borrador": p = N(estadoPub: "BORRADOR"); break;
            case "n04-eliminada": p = N(estadoPub: "ELIMINADA"); break;
            // P4b (D-41): la publicación vence cuando vence el plan del asesor, no cuando termina la fila de vigencia.
            // Los casos de vencimiento usan un asesor cuya única suscripción es la vencida (antes eran del A1 vigente).
            case "n05-vencida-fin-exacto":
            {
                var a = AsesorConPlan(Suscripcion.Activa, T);
                p = N(asesor: a, vigente: false);
                Vigencia(p, T.AddDays(-30), T, a.Plan);
                Destacar(p, T.AddDays(-1), T.AddDays(5));
                break;
            }
            case "n06-sin-vigencia": p = N(vigente: false); break;
            case "n07-vigencia-futura":
                p = N(vigente: false);
                Vigencia(p, T.AddHours(1), T.AddDays(30));
                break;
            case "n08-asesor-bloqueado":
                p = N(asesor: A2);
                Destacar(p, T.AddDays(-1), T.AddDays(5));
                break;
            case "n09-asesor-rechazado": p = N(asesor: A3); break;
            case "n10-asesor-pendiente": p = N(asesor: A4); break;
            case "n11-titulo-espacios": p = N(titulo: "   "); break;
            case "n13-titulo-vacio": p = N(titulo: ""); break;
            case "n14-plan-vencido": p = N(asesor: AsesorConPlan(Suscripcion.Activa, T.AddMinutes(-1))); break;
            case "n15-plan-cancelado": p = N(asesor: AsesorConPlan(Suscripcion.Cancelada, T.AddDays(30))); break;
            case "n16-plan-estado-vencida": p = N(asesor: AsesorConPlan(Suscripcion.Vencida, null)); break;
            case "n17-precio-cero": p = N(precio: 0m); break;
            case "n18-reservado-vencido":
            {
                var a = AsesorConPlan(Suscripcion.Activa, T.AddMinutes(-1));
                p = N(estadoCom: Publicacion.Reservado, asesor: a, vigente: false);
                Vigencia(p, T.AddDays(-40), T.AddMinutes(-1), a.Plan);
                break;
            }
            case "n19-reservado-asesor-bloqueado": p = N(estadoCom: Publicacion.Reservado, asesor: A2); break;
            case "n20-vencida-hace-1min":
            {
                var a = AsesorConPlan(Suscripcion.Activa, T.AddMinutes(-1));
                p = N(asesor: a, vigente: false);
                Vigencia(p, T.AddDays(-30), T.AddMinutes(-1), a.Plan);
                break;
            }
            case "n21-plan-fin-exacto": p = N(asesor: AsesorConPlan(Suscripcion.Activa, T)); break;
            case "n22-precio-nulo": p = N(precio: null); break;
            case "n23-titulo-nulo": p = N(titulo: null); break;
            case "n24-plan-sin-fin": p = N(asesor: AsesorConPlan(Suscripcion.Activa, null)); break;
            case "n25-plan-suspendido": p = N(asesor: AsesorConPlan(Suscripcion.Suspendida, T.AddDays(30))); break;
            default: throw new ArgumentException("Caso desconocido: " + caso);
        }
        return p;
    }
}

public static class CatalogoJson
{
    public static string[] Slugs(JsonElement arreglo) =>
        arreglo.EnumerateArray().Select(x => x.GetProperty("slug").GetString()!).ToArray();

    public static string[] Claves(JsonElement objeto) =>
        objeto.EnumerateObject().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    /// <summary>Todos los nombres de propiedad del JSON, a cualquier profundidad.</summary>
    public static IEnumerable<string> TodasLasClaves(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object)
            foreach (var p in e.EnumerateObject())
            {
                yield return p.Name;
                foreach (var c in TodasLasClaves(p.Value)) yield return c;
            }
        else if (e.ValueKind == JsonValueKind.Array)
            foreach (var x in e.EnumerateArray())
                foreach (var c in TodasLasClaves(x)) yield return c;
    }

    /// <summary>Resumen legible de /filtros: "tipos|marcas[lineas]|rangos".</summary>
    public static string Resumen(JsonElement f)
    {
        string Op(JsonElement o) => $"{o.GetProperty("nombre").GetString()}={o.GetProperty("total").GetInt32()}";
        var tipos = string.Join(",", f.GetProperty("tipos").EnumerateArray().Select(Op));
        var marcas = string.Join(",", f.GetProperty("marcas").EnumerateArray()
            .Select(m => $"{Op(m)}[{string.Join(",", m.GetProperty("lineas").EnumerateArray().Select(Op))}]"));
        var r = f.GetProperty("rangos");
        string V(string n) => r.GetProperty(n).ValueKind == JsonValueKind.Null ? "null" : r.GetProperty(n).GetRawText();
        return $"{tipos}|{marcas}|{V("precioMin")},{V("precioMax")},{V("anioMin")},{V("anioMax")},{V("kmMax")}";
    }
}
