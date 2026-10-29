using System.Globalization;
using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Models;
using Camion.Api.Publicaciones;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Endpoints;

// P4b: publicaciones (HU-023 crear, HU-024 modificar, HU-025 inactivar, HU-026 renovar, HU-027 destacar, HU-028
// vencimiento, HU-065 mis publicaciones; RN-CO-001 a RN-CO-005, D-21, D-30, D-38, D-40, D-41).
// Rol AsesorOAdministrador. El asesor solo ve y toca lo suyo (una ELIMINADA ya no es suya para la API); lo ajeno da 404.
// La vigencia es la del plan del asesor dueño: VENCIDA se calcula al leer, sin proceso automático.
public static class PublicacionesEndpoints
{
    private const int TamanoPorDefecto = 20;
    private const int TamanoMaximo = 100;

    public static IEndpointRouteBuilder MapPublicacionesEndpoints(this IEndpointRouteBuilder app)
    {
        var pubs = app.MapGroup("/api/publicaciones").RequireAuthorization(Roles.PoliticaAsesorOAdministrador);

        // HU-065: "Mis publicaciones". El administrador ve todas (y puede filtrar por asesorId o pedir las ELIMINADA).
        pubs.MapGet("", async (string? estado, string? q, string? pagina, string? tamano, string? asesorId,
            ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();

            var errores = new Dictionary<string, string[]>();
            var estadoFiltro = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
            if (estadoFiltro is not null && !ReglasP4b.EstadosFiltro.Contains(estadoFiltro)) errores["estado"] = [ReglasP4b.MensajeEstado];
            var texto = (q ?? "").Trim();
            if (texto.Length > ReglasP4b.MaxBusqueda) errores["q"] = [ReglasP4b.MensajeBuscar];
            var asesor = actor.EsAdmin ? LeerEntero(asesorId, 1, long.MaxValue, "asesorId", ReglasP4b.MensajeAsesorId, errores) : null;
            var numeroPagina = (int)(LeerEntero(pagina, 1, int.MaxValue, "pagina", ReglasP4b.MensajePagina, errores) ?? 1);
            var tamanoPagina = (int)(LeerEntero(tamano, 1, TamanoMaximo, "tamano", ReglasP4b.MensajePagina, errores) ?? TamanoPorDefecto);
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasP4b.MensajeFiltros, errores));

            var ahora = reloj.GetUtcNow().UtcDateTime;
            var vigentes = DatosP4b.SuscripcionesVigentes(db, ahora);
            var consulta = Alcance(db.Publicaciones.AsNoTracking(), actor);
            if (asesor is not null) consulta = consulta.Where(p => p.AsesorId == asesor);
            consulta = estadoFiltro switch
            {
                null => consulta.Where(p => p.EstadoPublicacion != Publicacion.Eliminada),
                Publicacion.Eliminada => consulta.Where(p => p.EstadoPublicacion == Publicacion.Eliminada),
                ReglasP4b.Vendida => consulta.Where(p => p.EstadoPublicacion != Publicacion.Eliminada &&
                                                         p.EstadoComercial == Publicacion.Vendido),
                Publicacion.Publicada => consulta.Where(p => p.EstadoPublicacion == Publicacion.Publicada &&
                    p.EstadoComercial != Publicacion.Vendido &&
                    vigentes.Any(s => s.AsesorId == p.AsesorId) && p.Vigencias.Any(v => v.Inicio <= ahora)),
                ReglasP4b.Vencida => consulta.Where(p => p.EstadoPublicacion == Publicacion.Publicada &&
                    p.EstadoComercial != Publicacion.Vendido &&
                    !(vigentes.Any(s => s.AsesorId == p.AsesorId) && p.Vigencias.Any(v => v.Inicio <= ahora))),
                _ => consulta.Where(p => p.EstadoPublicacion == estadoFiltro && p.EstadoComercial != Publicacion.Vendido)
            };
            if (texto.Length > 0)
            {
                var minusculas = texto.ToLowerInvariant();
                consulta = consulta.Where(p =>
                    (p.Titulo != null && p.Titulo.ToLower().Contains(minusculas)) ||
                    (p.Vehiculo!.Marca != null && p.Vehiculo.Marca.Nombre.ToLower().Contains(minusculas)) ||
                    (p.Vehiculo.Linea != null && p.Vehiculo.Linea.Nombre.ToLower().Contains(minusculas)));
            }

            var total = await consulta.CountAsync(ct);
            var saltar = (long)(numeroPagina - 1) * tamanoPagina;
            var filas = saltar >= total
                ? []
                // Las destacadas van primero (sección aparte en "Mis publicaciones"), luego las más recientes.
                : await Filas(db, consulta
                    .OrderByDescending(p => p.Destacados.Any(d => d.Estado == PublicacionDestacada.Activa && d.Inicio <= ahora && d.Fin > ahora))
                    .ThenByDescending(p => p.ActualizadoEn).ThenByDescending(p => p.Id)
                    .Skip((int)saltar).Take(tamanoPagina), ahora).ToListAsync(ct);
            var items = filas.OrderByDescending(f => f.Destacada).ThenByDescending(f => f.ActualizadoEn).ThenByDescending(f => f.Id)
                .Select(f => Item(f, actor)).ToArray();
            return Results.Ok(new MisPublicacionesPaginaResponse(items, total, numeroPagina, tamanoPagina));
        });

        // Cupo del plan (D-38, D-40). El asesor ve el suyo; el administrador indica asesorId.
        pubs.MapGet("/cupo", async (string? asesorId, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
        {
            var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            var errores = new Dictionary<string, string[]>();
            var asesor = actor.EsAdmin
                ? LeerEntero(asesorId, 1, long.MaxValue, "asesorId", ReglasP4b.MensajeAsesorId, errores)
                : actor.PerfilAsesorId;
            if (asesor is null && !errores.ContainsKey("asesorId")) errores["asesorId"] = [ReglasP4b.MensajeAsesorCupo];
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasP4b.MensajeFiltros, errores));
            if (actor.EsAdmin && !await db.PerfilesAsesor.AsNoTracking().AnyAsync(p => p.Id == asesor, ct))
                return Results.NotFound(new ErrorResponse(ReglasP4b.MensajeAsesorId));

            var ahora = reloj.GetUtcNow().UtcDateTime;
            var sus = await DatosP4b.SuscripcionVigenteAsync(db, asesor!.Value, ahora, ct);
            var usadas = await DatosP4b.CupoUsadoAsync(db, asesor.Value, ct);
            var destacadas = await DatosP4b.DestacadosVigentes(db, asesor.Value, ahora).CountAsync(ct);
            return Results.Ok(new CupoResponse(
                sus?.Plan?.LimitePublicaciones ?? 0, usadas, sus?.Plan?.LimiteDestacados ?? 0, destacadas, sus is not null,
                sus?.FinVigencia is { } fin ? ConsultasCatalogo.Utc(fin) : null));
        });

        pubs.MapGet("/{id}", async (long id, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            var item = await ItemAsync(db, actor, id, reloj.GetUtcNow().UtcDateTime, ct);
            return item is null ? NoEncontrada() : Results.Ok(item);
        });

        // HU-023 (RN-CO-001, D-21, D-30, D-40): crea y publica en una transacción.
        pubs.MapPost("", async (PublicacionRequest? request, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
        {
            var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            var errores = ReglasP4b.ValidarTextos(request?.Titulo, request?.DescripcionComercial, out var titulo, out var descripcion);
            var vehiculoId = request?.VehiculoId ?? 0;
            if (request?.VehiculoId is null) errores["vehiculoId"] = [ReglasP4b.MensajeVehiculoId];
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasP4b.MensajeRevisar, errores));

            var ahora = reloj.GetUtcNow().UtcDateTime;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var asesorDueno = await InventarioEndpoints.Alcance(db.Vehiculos.AsNoTracking(), actor).Where(v => v.Id == vehiculoId)
                .Select(v => (long?)v.AsesorId).FirstOrDefaultAsync(ct);
            if (asesorDueno is null) return Results.NotFound(new ErrorResponse(ReglasP4b.MensajeVehiculoNoEncontrado));
            // Orden de bloqueo: asesor (cupo) y luego vehículo (una publicación por vehículo).
            await DatosP4b.BloquearAsesorAsync(db, asesorDueno.Value, ct);
            await DatosP4b.BloquearVehiculoAsync(db, vehiculoId, ct);
            var v = await db.Vehiculos.AsNoTracking().Where(x => x.Id == vehiculoId).Select(x => new
            {
                x.AsesorId,
                x.Estado,
                x.PrecioCop,
                Tipo = x.TipoVehiculo!.Nombre,
                Marca = x.Marca == null ? null : x.Marca.Nombre,
                Linea = x.Linea == null ? null : x.Linea.Nombre,
                x.ModeloAnio,
                Fotos = x.Fotos.Count
            }).FirstAsync(ct);

            var sus = await DatosP4b.SuscripcionVigenteAsync(db, v.AsesorId, ahora, ct);
            if (sus is null) return Conflicto(ReglasP4b.MensajeSinPlan);
            if (await db.Publicaciones.AnyAsync(p => p.VehiculoId == vehiculoId && p.EstadoPublicacion != Publicacion.Eliminada, ct))
                return Conflicto(ReglasP4b.MensajeYaPublicado);
            var noPublicable = NoPublicable(v.Estado, v.Fotos, v.PrecioCop);
            if (noPublicable is not null) return noPublicable;
            var limite = sus.Plan!.LimitePublicaciones;
            if (await DatosP4b.CupoUsadoAsync(db, v.AsesorId, ct) >= limite) return Conflicto(ReglasP4b.MensajeCupo(limite));

            var pub = new Publicacion
            {
                VehiculoId = vehiculoId,
                AsesorId = v.AsesorId,
                SlugPublico = "tmp-" + Guid.NewGuid().ToString("N"),
                Titulo = titulo,
                DescripcionComercial = descripcion,
                PrecioCop = v.PrecioCop,
                Negociable = request!.Negociable ?? false,
                EstadoComercial = Publicacion.Disponible,
                EstadoPublicacion = Publicacion.Publicada,
                PublicadaEn = ahora,
                CreadoEn = ahora,
                ActualizadoEn = ahora
            };
            db.Publicaciones.Add(pub);
            await db.SaveChangesAsync(ct);
            pub.SlugPublico = await SlugUnicoAsync(db, ReglasP4b.Slug(v.Marca, v.Linea, v.ModeloAnio, v.Tipo, pub.Id), pub.Id, ct);
            var vigencia = NuevaVigencia(pub.Id, sus, ahora, ReglasP4b.MotivoPublicar);
            db.HistorialVigencias.Add(vigencia);
            var nuevos = Datos(pub);
            nuevos["slugPublico"] = pub.SlugPublico;
            nuevos["vigenciaFin"] = ConsultasCatalogo.Utc(vigencia.Fin);
            Auditar(db, actor, pub.Id, Auditoria.Crear, null, nuevos, ahora);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/publicaciones/{pub.Id}", await ItemAsync(db, actor, pub.Id, ahora, ct));
        });

        // HU-024 (RN-CO-002): título, descripción y negociable, en cualquier estado salvo ELIMINADA.
        pubs.MapPut("/{id}", (long id, EditarPublicacionRequest? request, ClaimsPrincipal principal, CamionDbContext db,
            TimeProvider reloj, CancellationToken ct) =>
            AccionAsync(id, principal, db, reloj, ct, c => Task.FromResult(Editar(db, c, request))));

        // HU-025 (RN-CO-003): PUBLICADA (vigente o vencida) -> OCULTA, con motivo; quita su destacado.
        pubs.MapPost("/{id}/inactivar", (long id, MotivoPublicacionRequest? request, ClaimsPrincipal principal,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
            AccionAsync(id, principal, db, reloj, ct, async c =>
            {
                var errores = ValidacionAuth.ValidarMotivo(request?.Motivo, obligatorio: true);
                if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasP4b.MensajeRevisar, errores));
                if (c.Estado != Publicacion.Publicada) return Transicion("inactivar", c);
                var p = c.Pub;
                var antes = Datos(p);
                p.EstadoPublicacion = Publicacion.Oculta;
                p.MotivoOcultamiento = ValidacionAuth.NormalizarOpcional(request!.Motivo);
                p.OcultadaEn = c.Ahora;
                p.ActualizadoEn = c.Ahora;
                var quitados = await DatosP4b.QuitarDestacadosAsync(db, p.Id, c.Ahora, ct);
                var nuevos = Datos(p);
                if (quitados > 0) nuevos["destacadoQuitado"] = true;
                Auditar(db, c.Actor, p.Id, Auditoria.Inactivar, antes, nuevos, c.Ahora);
                return null;
            }));

        // OCULTA -> PUBLICADA: plan vigente y vehículo publicable; no consume cupo nuevo (ya cuenta, D-40).
        pubs.MapPost("/{id}/reactivar", (long id, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
            AccionAsync(id, principal, db, reloj, ct, async c =>
            {
                if (c.Estado != Publicacion.Oculta) return Transicion("reactivar", c);
                var sus = await DatosP4b.SuscripcionVigenteAsync(db, c.Pub.AsesorId, c.Ahora, ct);
                if (sus is null) return Conflicto(ReglasP4b.MensajeSinPlan);
                var v = await db.Vehiculos.AsNoTracking().Where(x => x.Id == c.Pub.VehiculoId)
                    .Select(x => new { x.Estado, x.PrecioCop, Fotos = x.Fotos.Count }).FirstAsync(ct);
                var noPublicable = NoPublicable(v.Estado, v.Fotos, v.PrecioCop);
                if (noPublicable is not null) return noPublicable;

                var p = c.Pub;
                var antes = Datos(p);
                p.EstadoPublicacion = Publicacion.Publicada;
                p.MotivoOcultamiento = null;
                p.PrecioCop = v.PrecioCop;
                p.PublicadaEn ??= c.Ahora;
                p.ActualizadoEn = c.Ahora;
                var vigencia = NuevaVigencia(p.Id, sus, c.Ahora, ReglasP4b.MotivoReactivar);
                db.HistorialVigencias.Add(vigencia);
                var nuevos = Datos(p);
                nuevos["vigenciaFin"] = ConsultasCatalogo.Utc(vigencia.Fin);
                Auditar(db, c.Actor, p.Id, Auditoria.Reactivar, antes, nuevos, c.Ahora);
                return null;
            }));

        // HU-026 (RN-SU-002, D-41): PUBLICADA (vigente o vencida) con plan vigente: vigencia nueva hasta el fin del plan.
        pubs.MapPost("/{id}/renovar", (long id, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
            AccionAsync(id, principal, db, reloj, ct, async c =>
            {
                if (c.Estado != Publicacion.Publicada) return Transicion("renovar", c);
                var sus = await DatosP4b.SuscripcionVigenteAsync(db, c.Pub.AsesorId, c.Ahora, ct);
                if (sus is null) return Conflicto(ReglasP4b.MensajeSinPlan);
                var p = c.Pub;
                var antes = Datos(p);
                antes["estadoEfectivo"] = c.EstadoEfectivo;
                p.ActualizadoEn = c.Ahora;
                var vigencia = NuevaVigencia(p.Id, sus, c.Ahora, ReglasP4b.MotivoRenovar);
                db.HistorialVigencias.Add(vigencia);
                var nuevos = Datos(p);
                nuevos["estadoEfectivo"] = Publicacion.Publicada;
                nuevos["vigenciaFin"] = ConsultasCatalogo.Utc(vigencia.Fin);
                Auditar(db, c.Actor, p.Id, Auditoria.Renovar, antes, nuevos, c.Ahora);
                return null;
            }));

        // D-40: OCULTA o VENDIDA -> ELIMINADA (borrado lógico, libera cupo). La confirmación la pide la interfaz.
        pubs.MapPost("/{id}/eliminar", (long id, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
            AccionAsync(id, principal, db, reloj, ct, async c =>
            {
                if (c.Estado is not (Publicacion.Oculta or ReglasP4b.Vendida)) return Transicion("eliminar", c);
                var p = c.Pub;
                var antes = Datos(p);
                p.EstadoPublicacion = Publicacion.Eliminada;
                p.EliminadaEn = c.Ahora;
                p.ActualizadoEn = c.Ahora;
                var quitados = await DatosP4b.QuitarDestacadosAsync(db, p.Id, c.Ahora, ct);
                var nuevos = Datos(p);
                if (quitados > 0) nuevos["destacadoQuitado"] = true;
                Auditar(db, c.Actor, p.Id, Auditoria.Eliminar, antes, nuevos, c.Ahora);
                return null;
            }));

        // HU-027 modificada por D-38: el dueño o el administrador destacan una publicación vigente, sin pasar del límite
        // del plan a la vez. Ya destacada: 200 sin cambios.
        pubs.MapPost("/{id}/destacar", (long id, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
            AccionAsync(id, principal, db, reloj, ct, async c =>
            {
                if (c.EstadoEfectivo != Publicacion.Publicada) return Transicion("destacar", c);
                var p = c.Pub;
                if (await DatosP4b.DestacadosVigentes(db, p.AsesorId, c.Ahora).AnyAsync(d => d.PublicacionId == p.Id, ct)) return null;
                var sus = await DatosP4b.SuscripcionVigenteAsync(db, p.AsesorId, c.Ahora, ct);
                if (sus is null) return Conflicto(ReglasP4b.MensajeSinPlan);
                var limite = sus.Plan!.LimiteDestacados;
                if (await DatosP4b.DestacadosVigentes(db, p.AsesorId, c.Ahora).CountAsync(ct) >= limite)
                    return Conflicto(ReglasP4b.MensajeDestacados(limite));
                var destacado = new PublicacionDestacada
                {
                    PublicacionId = p.Id,
                    Inicio = c.Ahora,
                    Fin = sus.FinVigencia!.Value,
                    Estado = PublicacionDestacada.Activa,
                    CreadoPor = c.Actor.UsuarioId
                };
                db.PublicacionesDestacadas.Add(destacado);
                p.ActualizadoEn = c.Ahora;
                Auditar(db, c.Actor, p.Id, Auditoria.Destacar, new Dictionary<string, object?> { ["destacada"] = false },
                    new Dictionary<string, object?>
                    {
                        ["destacada"] = true, ["fin"] = ConsultasCatalogo.Utc(destacado.Fin), ["limiteDestacados"] = limite
                    }, c.Ahora);
                return null;
            }));

        // D-38: quitar el destacado (FINALIZADA, fin = ahora). Sin destacado: 200 sin cambios.
        pubs.MapPost("/{id}/quitar-destacado", (long id, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
            AccionAsync(id, principal, db, reloj, ct, async c =>
            {
                if (c.Estado == Publicacion.Eliminada) return Transicion("quitar el destacado de", c);
                if (await DatosP4b.QuitarDestacadosAsync(db, c.Pub.Id, c.Ahora, ct) == 0) return null;
                c.Pub.ActualizadoEn = c.Ahora;
                Auditar(db, c.Actor, c.Pub.Id, Auditoria.QuitarDestacado, new Dictionary<string, object?> { ["destacada"] = true },
                    new Dictionary<string, object?> { ["destacada"] = false }, c.Ahora);
                return null;
            }));

        return app;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Acción sobre una publicación: 403 si el actor no es válido, 404 si no está en su alcance; si no, transacción con
    // el asesor dueño bloqueado (cupo y destacados), la acción y la respuesta con la publicación actualizada.
    // ---------------------------------------------------------------------------------------------------------------

    private sealed class ContextoAccion
    {
        public InventarioEndpoints.Actor Actor { get; init; } = null!;
        public Publicacion Pub { get; init; } = null!;
        public DateTime Ahora { get; init; }
        public string Estado { get; init; } = "";
        public string EstadoEfectivo { get; init; } = "";
    }

    private static async Task<IResult> AccionAsync(long id, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
        CancellationToken ct, Func<ContextoAccion, Task<IResult?>> accion)
    {
        var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
        if (actor is null) return SinPermiso();
        var asesorId = await Alcance(db.Publicaciones.AsNoTracking(), actor).Where(p => p.Id == id)
            .Select(p => (long?)p.AsesorId).FirstOrDefaultAsync(ct);
        if (asesorId is null) return NoEncontrada();

        var ahora = reloj.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await DatosP4b.BloquearAsesorAsync(db, asesorId.Value, ct);
        var pub = await Alcance(db.Publicaciones, actor).SingleOrDefaultAsync(p => p.Id == id, ct);
        if (pub is null) return NoEncontrada();
        var vigente = await DatosP4b.SuscripcionesVigentes(db, ahora).AnyAsync(s => s.AsesorId == pub.AsesorId, ct) &&
                      await db.HistorialVigencias.AsNoTracking().AnyAsync(v => v.PublicacionId == id && v.Inicio <= ahora, ct);
        var estado = ReglasP4b.Estado(pub.EstadoPublicacion, pub.EstadoComercial);
        var contexto = new ContextoAccion
        {
            Actor = actor, Pub = pub, Ahora = ahora, Estado = estado, EstadoEfectivo = ReglasP4b.EstadoEfectivo(estado, vigente)
        };
        var error = await accion(contexto);
        if (error is not null) return error;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        // La respuesta muestra la publicación aunque la acción la haya dejado ELIMINADA.
        return Results.Ok(await ItemAsync(db, actor, id, ahora, ct, incluirEliminada: true));
    }

    // HU-024: edición (sin escrituras asíncronas; la guarda AccionAsync).
    private static IResult? Editar(CamionDbContext db, ContextoAccion c, EditarPublicacionRequest? request)
    {
        if (request is null) return Results.BadRequest(new ErrorResponse(MensajesError.FormatoInvalido));
        var errores = ReglasP4b.ValidarTextos(request.Titulo, request.DescripcionComercial, out var titulo, out var descripcion);
        if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasP4b.MensajeRevisar, errores));
        if (c.Estado == Publicacion.Eliminada) return Transicion("editar", c);
        var p = c.Pub;
        var antes = new Dictionary<string, object?>
        {
            ["titulo"] = p.Titulo, ["descripcionComercial"] = p.DescripcionComercial, ["negociable"] = p.Negociable
        };
        p.Titulo = titulo;
        p.DescripcionComercial = descripcion;
        p.Negociable = request.Negociable ?? false;
        var despues = new Dictionary<string, object?>
        {
            ["titulo"] = p.Titulo, ["descripcionComercial"] = p.DescripcionComercial, ["negociable"] = p.Negociable
        };
        var cambiados = antes.Keys.Where(k => !Equals(antes[k], despues[k])).ToList();
        if (cambiados.Count > 0)
        {
            p.ActualizadoEn = c.Ahora;
            Auditar(db, c.Actor, p.Id, Auditoria.Editar, cambiados.ToDictionary(k => k, k => antes[k]),
                cambiados.ToDictionary(k => k, k => despues[k]), c.Ahora);
        }
        return null;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Ayudas
    // ---------------------------------------------------------------------------------------------------------------

    // D-7, RN-SE-006: el asesor solo ve lo suyo y no ve sus ELIMINADA (D-40: "deja de verse en Mis publicaciones").
    private static IQueryable<Publicacion> Alcance(IQueryable<Publicacion> consulta, InventarioEndpoints.Actor actor,
        bool incluirEliminada = false) =>
        actor.EsAdmin
            ? consulta
            : consulta.Where(p => p.AsesorId == actor.PerfilAsesorId &&
                                  (incluirEliminada || p.EstadoPublicacion != Publicacion.Eliminada));

    private sealed class Fila
    {
        public long Id { get; init; }
        public long VehiculoId { get; init; }
        public long AsesorId { get; init; }
        public string? AsesorNombre { get; init; }
        public string? Titulo { get; init; }
        public string? DescripcionComercial { get; init; }
        public bool Negociable { get; init; }
        public string EstadoPublicacion { get; init; } = "";
        public string EstadoComercial { get; init; } = "";
        public string Slug { get; init; } = "";
        public string? MotivoOcultamiento { get; init; }
        public DateTime? PublicadaEn { get; init; }
        public DateTime ActualizadoEn { get; init; }
        public string Tipo { get; init; } = "";
        public string? Marca { get; init; }
        public string? Linea { get; init; }
        public int? ModeloAnio { get; init; }
        public decimal? PrecioVehiculo { get; init; }
        public decimal? PrecioPublicacion { get; init; }
        public string? FotoVehiculo { get; init; }
        public string? FotoPublicacion { get; init; }
        public DateTime? FinPlan { get; init; }
        public bool VigenciaIniciada { get; init; }
        public bool Destacada { get; init; }
    }

    private static IQueryable<Fila> Filas(CamionDbContext db, IQueryable<Publicacion> consulta, DateTime ahora)
    {
        var vigentes = DatosP4b.SuscripcionesVigentes(db, ahora);
        return consulta.Select(p => new Fila
        {
            Id = p.Id,
            VehiculoId = p.VehiculoId,
            AsesorId = p.AsesorId,
            AsesorNombre = db.PerfilesAsesor.Where(a => a.Id == p.AsesorId)
                .Select(a => db.Users.Where(u => u.Id == a.UsuarioId).Select(u => u.NombreCompleto).FirstOrDefault())
                .FirstOrDefault(),
            Titulo = p.Titulo,
            DescripcionComercial = p.DescripcionComercial,
            Negociable = p.Negociable,
            EstadoPublicacion = p.EstadoPublicacion,
            EstadoComercial = p.EstadoComercial,
            Slug = p.SlugPublico,
            MotivoOcultamiento = p.MotivoOcultamiento,
            PublicadaEn = p.PublicadaEn,
            ActualizadoEn = p.ActualizadoEn,
            Tipo = p.Vehiculo!.TipoVehiculo!.Nombre,
            Marca = p.Vehiculo.Marca == null ? null : p.Vehiculo.Marca.Nombre,
            Linea = p.Vehiculo.Linea == null ? null : p.Vehiculo.Linea.Nombre,
            ModeloAnio = p.Vehiculo.ModeloAnio,
            PrecioVehiculo = p.Vehiculo.PrecioCop,
            PrecioPublicacion = p.PrecioCop,
            FotoVehiculo = p.Vehiculo.Fotos.OrderByDescending(f => f.EsPortada).ThenBy(f => f.Orden)
                .Select(f => f.ArchivoClave).FirstOrDefault(),
            FotoPublicacion = p.Fotos.OrderByDescending(f => f.EsPortada).ThenBy(f => f.Orden)
                .Select(f => f.ArchivoClave).FirstOrDefault(),
            FinPlan = vigentes.Where(s => s.AsesorId == p.AsesorId).OrderByDescending(s => s.FinVigencia)
                .Select(s => s.FinVigencia).FirstOrDefault(),
            VigenciaIniciada = p.Vigencias.Any(v => v.Inicio <= ahora),
            Destacada = p.Destacados.Any(d => d.Estado == PublicacionDestacada.Activa && d.Inicio <= ahora && d.Fin > ahora)
        });
    }

    private static PublicacionItem Item(Fila f, InventarioEndpoints.Actor actor)
    {
        var estado = ReglasP4b.Estado(f.EstadoPublicacion, f.EstadoComercial);
        var efectivo = ReglasP4b.EstadoEfectivo(estado, f.FinPlan is not null && f.VigenciaIniciada);
        var vigente = efectivo == Publicacion.Publicada;
        var precio = f.PrecioVehiculo ?? f.PrecioPublicacion;
        return new PublicacionItem(
            f.Id, f.VehiculoId, ReglasP4b.TituloVehiculo(f.Marca, f.Linea, f.ModeloAnio, f.Tipo), f.Titulo,
            f.DescripcionComercial, f.Negociable, estado, efectivo, f.Destacada && vigente,
            vigente && f.FinPlan is { } fin ? ConsultasCatalogo.Utc(fin) : null,
            f.Slug, ConsultasCatalogo.UrlFoto(f.FotoVehiculo ?? f.FotoPublicacion),
            precio is null ? null : ConsultasCatalogo.Pesos(precio.Value),
            f.MotivoOcultamiento,
            f.PublicadaEn is { } publicada ? ConsultasCatalogo.Utc(publicada) : null,
            ConsultasCatalogo.Utc(f.ActualizadoEn),
            actor.EsAdmin ? new AsesorPublicacion(f.AsesorId, f.AsesorNombre ?? "") : null);
    }

    private static async Task<PublicacionItem?> ItemAsync(CamionDbContext db, InventarioEndpoints.Actor actor, long id,
        DateTime ahora, CancellationToken ct, bool incluirEliminada = false)
    {
        var fila = await Filas(db, Alcance(db.Publicaciones.AsNoTracking(), actor, incluirEliminada).Where(p => p.Id == id), ahora)
            .FirstOrDefaultAsync(ct);
        return fila is null ? null : Item(fila, actor);
    }

    // D-30 y requisitos para publicar: vehículo ACTIVO o RESERVADO, con foto y con precio.
    private static IResult? NoPublicable(string estadoVehiculo, int fotos, decimal? precio)
    {
        if (!ReglasP4b.VehiculoPublicable(estadoVehiculo)) return Conflicto(ReglasP4b.MensajeVehiculoNoPublicable(estadoVehiculo));
        if (fotos == 0) return Conflicto(ReglasP4b.MensajeSinFoto);
        if (precio is null or <= 0) return Conflicto(ReglasP4b.MensajeSinPrecio);
        return null;
    }

    // D-41: la vigencia va de ahora al fin del plan, con su suscripción (historial).
    internal static HistorialVigencia NuevaVigencia(long publicacionId, Suscripcion sus, DateTime ahora, string motivo) => new()
    {
        PublicacionId = publicacionId,
        SuscripcionId = sus.Id,
        Inicio = ahora,
        Fin = sus.FinVigencia!.Value,
        Motivo = motivo
    };

    // El slug lleva el id, así que solo choca con un slug antiguo (DEMO) idéntico: en ese caso se añade un sufijo.
    private static async Task<string> SlugUnicoAsync(CamionDbContext db, string slug, long id, CancellationToken ct) =>
        await db.Publicaciones.AsNoTracking().AnyAsync(p => p.SlugPublico == slug && p.Id != id, ct)
            ? $"{slug}-{Guid.NewGuid().ToString("N")[..6]}"
            : slug;

    internal static Dictionary<string, object?> Datos(Publicacion p) => new()
    {
        ["vehiculoId"] = p.VehiculoId,
        ["asesorId"] = p.AsesorId,
        ["titulo"] = p.Titulo,
        ["negociable"] = p.Negociable,
        ["estado"] = ReglasP4b.Estado(p.EstadoPublicacion, p.EstadoComercial),
        ["estadoPublicacion"] = p.EstadoPublicacion,
        ["estadoComercial"] = p.EstadoComercial,
        ["motivoOcultamiento"] = p.MotivoOcultamiento
    };

    internal static void Auditar(CamionDbContext db, InventarioEndpoints.Actor actor, long publicacionId, string accion,
        IReadOnlyDictionary<string, object?>? antes, IReadOnlyDictionary<string, object?> despues, DateTime ahora) =>
        Auditoria.AgregarEvento(db, actor.UsuarioId, Auditoria.EntidadPublicacion,
            publicacionId.ToString(CultureInfo.InvariantCulture), accion, antes, despues, ahora);

    private static IResult Transicion(string accion, ContextoAccion c) =>
        Conflicto(ReglasP4b.MensajeTransicion(accion, c.EstadoEfectivo));

    private static IResult Conflicto(string mensaje) => Results.Conflict(new ErrorResponse(mensaje));

    private static IResult NoEncontrada() => Results.NotFound(new ErrorResponse(ReglasP4b.MensajeNoEncontrada));

    private static IResult SinPermiso() =>
        Results.Json(new ErrorResponse(MensajesError.SinPermiso), statusCode: StatusCodes.Status403Forbidden);

    private static long? LeerEntero(string? valor, long minimo, long maximo, string campo, string mensaje,
        Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (long.TryParse(valor.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= minimo && n <= maximo)
            return n;
        errores[campo] = [mensaje];
        return null;
    }
}
