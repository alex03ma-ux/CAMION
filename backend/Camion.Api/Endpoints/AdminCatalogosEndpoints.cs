using System.Globalization;
using System.Linq.Expressions;
using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Inventario;
using Camion.Api.Models;
using Camion.Api.Suscripciones;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Endpoints;

// P4p: catálogos administrables (HU-082 marcas, HU-083 modelos, HU-084 tipos de carrocería; RN-PA-002 a RN-PA-004,
// D-32, D-37). El administrador crea, renombra y activa o desactiva; nada se borra. Un elemento desactivado deja de
// ofrecerse en formularios y filtros, y los vehículos que ya lo usan lo conservan. Fuera: tipos_vehiculo (D-17) y DIVIPOLA.
public static class AdminCatalogosEndpoints
{
    private const string CatalogoLineas = "lineas";

    private static readonly Dictionary<string, DefCatalogo> Catalogos = new(StringComparer.Ordinal)
    {
        ["marcas"] = new DefCatalogo<Marca>("marcas", "una marca", db => db.Marcas, v => v.MarcaId),
        ["carrocerias"] = new DefCatalogo<TipoCarroceria>("carrocerias", "un tipo de carrocería", db => db.TiposCarroceria, v => v.CarroceriaId),
        ["materiales"] = new DefCatalogo<MaterialCarroceria>("materiales", "un material", db => db.MaterialesCarroceria, v => v.MaterialId),
        ["tipos-maquina"] = new DefCatalogo<TipoMaquina>("tipos-maquina", "un tipo de máquina", db => db.TiposMaquina, v => v.TipoMaquinaId),
        ["combustibles"] = new DefCatalogo<Combustible>("combustibles", "un combustible", db => db.Combustibles, v => v.CombustibleId),
    };

    public static IEndpointRouteBuilder MapAdminCatalogosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin/catalogos").RequireAuthorization(Roles.PoliticaSoloAdministrador);

        grupo.MapGet("/{catalogo}", async (string catalogo, CamionDbContext db, CancellationToken ct) =>
        {
            if (!Catalogos.TryGetValue(catalogo, out var def)) return NoEncontrado();
            var comparador = Comparador();
            var elementos = await def.ListarAsync(db, ct);
            var usos = await def.UsosAsync(db, ct);
            if (def.Ruta != "marcas")
                return Results.Ok(new CatalogoAdminResponse<ElementoCatalogoAdmin>(elementos
                    .Select(e => new ElementoCatalogoAdmin(e.Id, e.Nombre, e.Activo, usos.GetValueOrDefault(e.Id)))
                    .OrderBy(e => e.Nombre, comparador).ThenBy(e => e.Id).ToArray()));

            var lineas = await db.Lineas.AsNoTracking().Select(l => new { l.Id, l.MarcaId, l.Nombre, l.Activo }).ToListAsync(ct);
            var tipos = await TiposPorLineaAsync(db, null, ct);
            var usosLinea = await UsosLineaAsync(db, null, ct);
            return Results.Ok(new CatalogoAdminResponse<MarcaCatalogoAdmin>(elementos
                .Select(m => new MarcaCatalogoAdmin(m.Id, m.Nombre, m.Activo, usos.GetValueOrDefault(m.Id),
                    lineas.Where(l => l.MarcaId == m.Id)
                        .Select(l => new LineaCatalogoAdmin(l.Id, l.Nombre, l.Activo,
                            tipos.GetValueOrDefault(l.Id) ?? [], usosLinea.GetValueOrDefault(l.Id)))
                        .OrderBy(l => l.Nombre, comparador).ThenBy(l => l.Id).ToArray()))
                .OrderBy(m => m.Nombre, comparador).ThenBy(m => m.Id).ToArray()));
        });

        // Crear (HU-082, HU-084): nombre de 1 a 100 caracteres, único sin mayúsculas ni tildes.
        grupo.MapPost("/{catalogo}", async (string catalogo, NombreCatalogoRequest? request, ClaimsPrincipal principal,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            if (!Catalogos.TryGetValue(catalogo, out var def)) return NoEncontrado();
            var errores = ReglasP4p.ValidarNombreCatalogo(request?.Nombre);
            if (errores.Count > 0) return Revisar(errores);
            var nombre = ReglasP4p.NormalizarTexto(request!.Nombre);
            if (Ocupado(await def.NombresAsync(db, null, ct), nombre)) return Duplicado(def.Articulo);

            var ahora = reloj.GetUtcNow().UtcDateTime;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var elemento = def.Agregar(db, nombre);
            if (!await GuardarAsync(db, ct)) return Duplicado(def.Articulo);
            Auditar(db, principal, def.Ruta, elemento.Id, Auditoria.Crear, null,
                new() { ["nombre"] = nombre, ["activo"] = true }, ahora);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/admin/catalogos/{def.Ruta}/{elemento.Id}",
                new ElementoCatalogoAdmin(elemento.Id, elemento.Nombre, elemento.Activo, 0));
        });

        // Renombrar: los vehículos que lo usan muestran el nombre nuevo.
        grupo.MapPut("/{catalogo}/{id}", async (string catalogo, long id, NombreCatalogoRequest? request,
            ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            if (!Catalogos.TryGetValue(catalogo, out var def)) return NoEncontrado();
            var errores = ReglasP4p.ValidarNombreCatalogo(request?.Nombre);
            if (errores.Count > 0) return Revisar(errores);
            var elemento = await def.BuscarAsync(db, id, ct);
            if (elemento is null) return NoEncontrado();
            var nombre = ReglasP4p.NormalizarTexto(request!.Nombre);
            if (Ocupado(await def.NombresAsync(db, id, ct), nombre)) return Duplicado(def.Articulo);

            if (elemento.Nombre != nombre)
            {
                var anterior = elemento.Nombre;
                elemento.Nombre = nombre;
                Auditar(db, principal, def.Ruta, id, Auditoria.Renombrar, new() { ["nombre"] = anterior },
                    new() { ["nombre"] = nombre }, reloj.GetUtcNow().UtcDateTime);
                if (!await GuardarAsync(db, ct)) return Duplicado(def.Articulo);
            }
            return Results.Ok(new ElementoCatalogoAdmin(elemento.Id, elemento.Nombre, elemento.Activo,
                await def.UsosDeAsync(db, id, ct)));
        });

        foreach (var (ruta, activo, accion) in new[] { ("activar", true, Auditoria.Activar), ("desactivar", false, Auditoria.Desactivar) })
        {
            grupo.MapPost("/{catalogo}/{id}/" + ruta, async (string catalogo, long id, ClaimsPrincipal principal,
                CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
            {
                if (!Catalogos.TryGetValue(catalogo, out var def)) return NoEncontrado();
                var elemento = await def.BuscarAsync(db, id, ct);
                if (elemento is null) return NoEncontrado();
                if (elemento.Activo != activo)
                {
                    elemento.Activo = activo;
                    Auditar(db, principal, def.Ruta, id, accion, new() { ["activo"] = !activo }, new() { ["activo"] = activo },
                        reloj.GetUtcNow().UtcDateTime);
                    await db.SaveChangesAsync(ct);
                }
                return Results.Ok(new ElementoCatalogoAdmin(elemento.Id, elemento.Nombre, elemento.Activo,
                    await def.UsosDeAsync(db, id, ct)));
            });

            grupo.MapPost("/lineas/{id}/" + ruta, async (long id, ClaimsPrincipal principal, CamionDbContext db,
                TimeProvider reloj, CancellationToken ct) =>
            {
                var linea = await db.Lineas.SingleOrDefaultAsync(l => l.Id == id, ct);
                if (linea is null) return NoEncontrado();
                if (linea.Activo != activo)
                {
                    linea.Activo = activo;
                    Auditar(db, principal, CatalogoLineas, id, accion, new() { ["activo"] = !activo }, new() { ["activo"] = activo },
                        reloj.GetUtcNow().UtcDateTime);
                    await db.SaveChangesAsync(ct);
                }
                return Results.Ok(await LineaRespuestaAsync(db, linea, null, ct));
            });
        }

        // HU-083 (D-32): crear un modelo dentro de una marca, con al menos una de las 4 categorías.
        grupo.MapPost("/marcas/{marcaId}/lineas", async (long marcaId, LineaAdminRequest? request, ClaimsPrincipal principal,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var errores = ReglasP4p.ValidarNombreCatalogo(request?.Nombre);
            var tipoIds = await ValidarTiposAsync(db, request?.TipoIds, errores, ct);
            if (errores.Count > 0) return Revisar(errores);
            if (!await db.Marcas.AsNoTracking().AnyAsync(m => m.Id == marcaId, ct)) return NoEncontrado();
            var nombre = ReglasP4p.NormalizarTexto(request!.Nombre);
            if (Ocupado(await NombresLineaAsync(db, marcaId, null, ct), nombre)) return DuplicadoLinea();

            var ahora = reloj.GetUtcNow().UtcDateTime;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var linea = new Linea { MarcaId = marcaId, Nombre = nombre, Activo = true };
            db.Lineas.Add(linea);
            if (!await GuardarAsync(db, ct)) return DuplicadoLinea();
            db.LineasTipos.AddRange(tipoIds!.Select(t => new LineaTipo { LineaId = linea.Id, TipoVehiculoId = t }));
            Auditar(db, principal, CatalogoLineas, linea.Id, Auditoria.Crear, null,
                new() { ["marcaId"] = marcaId, ["nombre"] = nombre, ["activo"] = true, ["tipoIds"] = tipoIds }, ahora);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/admin/catalogos/lineas/{linea.Id}", await LineaRespuestaAsync(db, linea, null, ct));
        });

        // Renombrar y/o cambiar las categorías. Quitar una categoría que usan vehículos se permite, con aviso.
        grupo.MapPut("/lineas/{id}", async (long id, LineaAdminRequest? request, ClaimsPrincipal principal,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var errores = new Dictionary<string, string[]>();
            if (request?.Nombre is null && request?.TipoIds is null)
                errores["nombre"] = [ReglasP4p.MensajeLineaSinCambios];
            if (request?.Nombre is not null)
                foreach (var (campo, lista) in ReglasP4p.ValidarNombreCatalogo(request.Nombre)) errores[campo] = lista;
            var tipoIds = request?.TipoIds is null ? null : await ValidarTiposAsync(db, request.TipoIds, errores, ct);
            if (errores.Count > 0) return Revisar(errores);

            var linea = await db.Lineas.SingleOrDefaultAsync(l => l.Id == id, ct);
            if (linea is null) return NoEncontrado();
            var ahora = reloj.GetUtcNow().UtcDateTime;
            string? nombre = null;
            if (request!.Nombre is not null)
            {
                nombre = ReglasP4p.NormalizarTexto(request.Nombre);
                if (Ocupado(await NombresLineaAsync(db, linea.MarcaId, id, ct), nombre)) return DuplicadoLinea();
            }

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (nombre is not null && nombre != linea.Nombre)
            {
                Auditar(db, principal, CatalogoLineas, id, Auditoria.Renombrar, new() { ["nombre"] = linea.Nombre },
                    new() { ["nombre"] = nombre }, ahora);
                linea.Nombre = nombre;
            }
            string? aviso = null;
            if (tipoIds is not null)
            {
                var actuales = await db.LineasTipos.Where(x => x.LineaId == id).ToListAsync(ct);
                var anteriores = actuales.Select(x => x.TipoVehiculoId).Order().ToArray();
                if (!anteriores.SequenceEqual(tipoIds))
                {
                    // Vehículos que eran válidos con las categorías anteriores (sin clasificar = todas) y ya no lo son.
                    var afectados = await db.Vehiculos.AsNoTracking()
                        .Where(v => v.LineaId == id && !tipoIds.Contains(v.TipoVehiculoId) &&
                                    (anteriores.Length == 0 || anteriores.Contains(v.TipoVehiculoId)))
                        .CountAsync(ct);
                    if (afectados > 0) aviso = Aviso(afectados);
                    db.LineasTipos.RemoveRange(actuales.Where(x => !tipoIds.Contains(x.TipoVehiculoId)));
                    db.LineasTipos.AddRange(tipoIds.Where(t => !anteriores.Contains(t))
                        .Select(t => new LineaTipo { LineaId = id, TipoVehiculoId = t }));
                    Auditar(db, principal, CatalogoLineas, id, Auditoria.Categorias, new() { ["tipoIds"] = anteriores },
                        new() { ["tipoIds"] = tipoIds, ["vehiculosAfectados"] = afectados }, ahora);
                }
            }
            if (!await GuardarAsync(db, ct)) return DuplicadoLinea();
            await tx.CommitAsync(ct);
            return Results.Ok(await LineaRespuestaAsync(db, linea, aviso, ct));
        });

        return app;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Ayudas
    // ---------------------------------------------------------------------------------------------------------------

    public static string Aviso(int n) => n == 1
        ? "1 vehículo usa este modelo en una categoría que ya no tiene; se corregirá al editarlo."
        : $"{n} vehículos usan este modelo en una categoría que ya no tiene; se corregirán al editarlos.";

    private static StringComparer Comparador() =>
        StringComparer.Create(CultureInfo.GetCultureInfo("es-CO"), CompareOptions.IgnoreCase);

    private static bool Ocupado(IEnumerable<string> existentes, string nombre)
    {
        var clave = ReglasP4p.ClaveCatalogo(nombre);
        return existentes.Any(n => ReglasP4p.ClaveCatalogo(n) == clave);
    }

    // Respaldo del índice único de la base ante dos altas simultáneas con el mismo nombre.
    private static async Task<bool> GuardarAsync(CamionDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    private static Task<List<string>> NombresLineaAsync(CamionDbContext db, long marcaId, long? excepto, CancellationToken ct) =>
        db.Lineas.AsNoTracking().Where(l => l.MarcaId == marcaId && (excepto == null || l.Id != excepto))
            .Select(l => l.Nombre).ToListAsync(ct);

    // tipoIds obligatorio: al menos una de las 4 categorías (Carro, Camión, Carrocería, Maquinaria) activas. Ordenados y sin repetir.
    private static async Task<long[]?> ValidarTiposAsync(CamionDbContext db, IReadOnlyList<long>? tipoIds,
        Dictionary<string, string[]> errores, CancellationToken ct)
    {
        if (tipoIds is null || tipoIds.Count == 0)
        {
            errores["tipoIds"] = [ReglasP4p.MensajeTipoIds];
            return null;
        }
        var categorias = (await db.TiposVehiculo.AsNoTracking().Where(t => t.Activo).Select(t => new { t.Id, t.Nombre })
                .ToListAsync(ct))
            .Where(t => ReglasInventario.CategoriaDe(t.Nombre) is not null).Select(t => t.Id).ToHashSet();
        var distintos = tipoIds.Distinct().Order().ToArray();
        if (!distintos.All(categorias.Contains))
        {
            errores["tipoIds"] = [ReglasP4p.MensajeTipoIdInvalido];
            return null;
        }
        return distintos;
    }

    private static async Task<Dictionary<long, long[]>> TiposPorLineaAsync(CamionDbContext db, long? lineaId, CancellationToken ct) =>
        (await db.LineasTipos.AsNoTracking().Where(x => lineaId == null || x.LineaId == lineaId).ToListAsync(ct))
        .GroupBy(x => x.LineaId).ToDictionary(g => g.Key, g => g.Select(x => x.TipoVehiculoId).Order().ToArray());

    private static async Task<Dictionary<long, int>> UsosLineaAsync(CamionDbContext db, long? lineaId, CancellationToken ct) =>
        await db.Vehiculos.AsNoTracking().Where(v => v.LineaId != null && (lineaId == null || v.LineaId == lineaId))
            .GroupBy(v => v.LineaId!.Value).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);

    private static async Task<LineaCatalogoRespuesta> LineaRespuestaAsync(CamionDbContext db, Linea linea, string? aviso,
        CancellationToken ct)
    {
        var tipos = await TiposPorLineaAsync(db, linea.Id, ct);
        var usos = await UsosLineaAsync(db, linea.Id, ct);
        return new LineaCatalogoRespuesta(linea.Id, linea.MarcaId, linea.Nombre, linea.Activo,
            tipos.GetValueOrDefault(linea.Id) ?? [], usos.GetValueOrDefault(linea.Id), aviso);
    }

    private static void Auditar(CamionDbContext db, ClaimsPrincipal principal, string catalogo, long id, string accion,
        Dictionary<string, object?>? antes, Dictionary<string, object?> despues, DateTime ahora) =>
        Auditoria.AgregarEvento(db, principal.FindFirstValue(ClaimTypes.NameIdentifier), Auditoria.EntidadCatalogo,
            $"{catalogo}:{id.ToString(CultureInfo.InvariantCulture)}", accion, antes, despues, ahora);

    private static IResult Revisar(Dictionary<string, string[]> errores) =>
        Results.BadRequest(new ErrorResponse(ReglasP4p.MensajeRevisar, errores));

    private static IResult NoEncontrado() => Results.NotFound(new ErrorResponse(ReglasP4p.MensajeNoEncontrado));

    private static IResult Duplicado(string articulo) =>
        Results.Conflict(new ErrorResponse($"Ya existe {articulo} con ese nombre."));

    private static IResult DuplicadoLinea() =>
        Results.Conflict(new ErrorResponse("Ya existe un modelo con ese nombre en esta marca."));

    // ---------------------------------------------------------------------------------------------------------------
    // Definición de cada catálogo simple (id, nombre, activo) y su columna en vehiculos.
    // ---------------------------------------------------------------------------------------------------------------

    private sealed class ElementoFila
    {
        public long Id { get; init; }
        public string Nombre { get; init; } = "";
        public bool Activo { get; init; }
    }

    private abstract class DefCatalogo(string ruta, string articulo)
    {
        public string Ruta { get; } = ruta;
        public string Articulo { get; } = articulo;
        public abstract Task<List<ElementoFila>> ListarAsync(CamionDbContext db, CancellationToken ct);
        public abstract Task<IElementoCatalogo?> BuscarAsync(CamionDbContext db, long id, CancellationToken ct);
        public abstract IElementoCatalogo Agregar(CamionDbContext db, string nombre);
        public abstract Task<List<string>> NombresAsync(CamionDbContext db, long? excepto, CancellationToken ct);
        public abstract Task<Dictionary<long, int>> UsosAsync(CamionDbContext db, CancellationToken ct);
        public abstract Task<int> UsosDeAsync(CamionDbContext db, long id, CancellationToken ct);
    }

    private sealed class DefCatalogo<T>(string ruta, string articulo, Func<CamionDbContext, DbSet<T>> tabla,
        Expression<Func<Vehiculo, long?>> columna) : DefCatalogo(ruta, articulo)
        where T : class, IElementoCatalogo, new()
    {
        public override Task<List<ElementoFila>> ListarAsync(CamionDbContext db, CancellationToken ct) =>
            tabla(db).AsNoTracking().Select(x => new ElementoFila { Id = x.Id, Nombre = x.Nombre, Activo = x.Activo })
                .ToListAsync(ct);

        public override async Task<IElementoCatalogo?> BuscarAsync(CamionDbContext db, long id, CancellationToken ct) =>
            await tabla(db).SingleOrDefaultAsync(x => x.Id == id, ct);

        public override IElementoCatalogo Agregar(CamionDbContext db, string nombre)
        {
            var elemento = new T { Nombre = nombre, Activo = true };
            tabla(db).Add(elemento);
            return elemento;
        }

        public override Task<List<string>> NombresAsync(CamionDbContext db, long? excepto, CancellationToken ct) =>
            tabla(db).AsNoTracking().Where(x => excepto == null || x.Id != excepto).Select(x => x.Nombre).ToListAsync(ct);

        public override async Task<Dictionary<long, int>> UsosAsync(CamionDbContext db, CancellationToken ct) =>
            await db.Vehiculos.AsNoTracking().Select(columna).Where(x => x != null)
                .GroupBy(x => x!.Value).Select(g => new { g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Total, ct);

        public override Task<int> UsosDeAsync(CamionDbContext db, long id, CancellationToken ct) =>
            db.Vehiculos.AsNoTracking().Select(columna).CountAsync(x => x == id, ct);
    }
}
