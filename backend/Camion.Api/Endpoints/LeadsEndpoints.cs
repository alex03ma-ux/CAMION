using System.Globalization;
using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Leads;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Endpoints;

// P5: gestión de leads (HU-031 a HU-033, HU-071 parcial, RN-CL-003, RN-CL-004, RN-SE-003, RN-SE-006, D-7, D-46 B, D-48).
// Rol AsesorOAdministrador (comprador 403, sin token 401). El asesor solo ve y toca los leads de los que es responsable
// ahora; lo ajeno da 404. Reabrir y reasignar: solo el administrador. Todas las rutas barren antes (D-46 B).
// No hay rutas para editar los datos del visitante, borrar leads ni tocar consentimientos (CA-P5-16, 43, 60).
public static class LeadsEndpoints
{
    private const int TamanoPorDefecto = 20;
    private const int TamanoMaximo = 100;
    // Las fechas de los filtros son días de Colombia (UTC-5, sin horario de verano).
    private static readonly TimeSpan DesfaseColombia = TimeSpan.FromHours(5);

    public static IEndpointRouteBuilder MapLeadsEndpoints(this IEndpointRouteBuilder app)
    {
        var leads = app.MapGroup("/api/leads").RequireAuthorization(Roles.PoliticaAsesorOAdministrador);

        // CA-P5-29, CA-P5-31: "Mis contactos" (asesor) y "Contactos" (administrador), del más reciente al más antiguo.
        leads.MapGet("", async (string? estado, string? responsable, string? publicacionId, string? vehiculoId,
            string? desde, string? hasta, string? pagina, string? tamano, ClaimsPrincipal principal, CamionDbContext db,
            TimeProvider reloj, CancellationToken ct) =>
        {
            var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();

            var errores = new Dictionary<string, string[]>();
            var estadoFiltro = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
            if (estadoFiltro is not null && !Lead.Estados.Contains(estadoFiltro)) errores["estado"] = [ReglasLeads.MensajeEstado];
            var soloAdministracion = false;
            long? responsableId = null;
            if (actor.EsAdmin && !string.IsNullOrWhiteSpace(responsable))
            {
                if (responsable.Trim().Equals(ReglasLeads.ResponsableAdministracion, StringComparison.OrdinalIgnoreCase))
                    soloAdministracion = true;
                else
                    responsableId = LeerEntero(responsable, 1, long.MaxValue, "responsable", ReglasLeads.MensajeResponsable, errores);
            }
            var publicacion = LeerEntero(publicacionId, 1, long.MaxValue, "publicacionId", ReglasLeads.MensajeVehiculo, errores);
            var vehiculo = LeerEntero(vehiculoId, 1, long.MaxValue, "vehiculoId", ReglasLeads.MensajeVehiculo, errores);
            var dia1 = LeerFecha(desde, "desde", errores);
            var dia2 = LeerFecha(hasta, "hasta", errores);
            if (dia1 is not null && dia2 is not null && dia2 < dia1) errores["hasta"] = [ReglasLeads.MensajeFechaRango];
            var numeroPagina = (int)(LeerEntero(pagina, 1, int.MaxValue, "pagina", ReglasLeads.MensajePagina, errores) ?? 1);
            var tamanoPagina = (int)(LeerEntero(tamano, 1, TamanoMaximo, "tamano", ReglasLeads.MensajePagina, errores) ?? TamanoPorDefecto);
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasLeads.MensajeFiltros, errores));

            var ahora = reloj.GetUtcNow().UtcDateTime;
            await BarridoLeads.BarrerAsync(db, ahora, ct);

            var consulta = Alcance(db.Leads.AsNoTracking(), actor);
            if (estadoFiltro is not null) consulta = consulta.Where(l => l.Estado == estadoFiltro);
            if (soloAdministracion) consulta = consulta.Where(l => l.ResponsableAsesorId == null);
            if (responsableId is not null) consulta = consulta.Where(l => l.ResponsableAsesorId == responsableId);
            if (publicacion is not null) consulta = consulta.Where(l => l.PublicacionId == publicacion);
            if (vehiculo is not null) consulta = consulta.Where(l => l.Publicacion!.VehiculoId == vehiculo);
            if (dia1 is { } d1)
            {
                var inicio = d1.ToDateTime(TimeOnly.MinValue) + DesfaseColombia;
                consulta = consulta.Where(l => l.CreadoEn >= inicio);
            }
            if (dia2 is { } d2)
            {
                var fin = d2.AddDays(1).ToDateTime(TimeOnly.MinValue) + DesfaseColombia;
                consulta = consulta.Where(l => l.CreadoEn < fin);
            }

            var total = await consulta.CountAsync(ct);
            var saltar = (long)(numeroPagina - 1) * tamanoPagina;
            var filas = saltar >= total
                ? []
                : await Proyectar(db, consulta.OrderByDescending(l => l.CreadoEn).ThenByDescending(l => l.Id)
                    .Skip((int)saltar).Take(tamanoPagina)).ToListAsync(ct);
            return Results.Ok(new LeadsPaginaResponse(filas.Select(f => Item(f, actor)).ToArray(), total, numeroPagina, tamanoPagina));
        });

        // D-48, CA-P5-58: "Contactos (N nuevos)". Nuevos = ABIERTO, no vistos, de los que quien consulta es responsable.
        leads.MapGet("/resumen", async (ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            await BarridoLeads.BarrerAsync(db, reloj.GetUtcNow().UtcDateTime, ct);
            var abiertos = Alcance(db.Leads.AsNoTracking(), actor).Where(l => l.Estado == Lead.Abierto);
            var nuevos = actor.EsAdmin
                ? abiertos.Where(l => l.ResponsableAsesorId == null && !l.VistoPorResponsable)
                : abiertos.Where(l => !l.VistoPorResponsable);
            return Results.Ok(new LeadsResumenResponse(await nuevos.CountAsync(ct), await abiertos.CountAsync(ct)));
        });

        // CA-P5-42: detalle con historial. Si lo abre su responsable, deja de ser nuevo (D-48).
        leads.MapGet("/{id}", async (long id, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
        {
            var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            await BarridoLeads.BarrerAsync(db, reloj.GetUtcNow().UtcDateTime, ct);
            var fila = await Proyectar(db, Alcance(db.Leads.AsNoTracking(), actor).Where(l => l.Id == id)).FirstOrDefaultAsync(ct);
            if (fila is null) return NoEncontrado();
            if (!fila.Visto && EsResponsable(actor, fila.ResponsableAsesorId))
            {
                await db.Leads.Where(l => l.Id == id).ExecuteUpdateAsync(s => s.SetProperty(l => l.VistoPorResponsable, true), ct);
                fila.Visto = true;
            }
            return Results.Ok(await DetalleAsync(db, fila, actor, ct));
        });

        // CA-P5-37, CA-P5-38: nota de seguimiento del responsable (asesor asignado o administrador).
        leads.MapPost("/{id}/notas", async (long id, NotaLeadRequest? request, ClaimsPrincipal principal, CamionDbContext db,
            TimeProvider reloj, CancellationToken ct) =>
        {
            var (actor, lead, ahora, error) = await PrepararAsync(principal, db, reloj, id, ct);
            if (error is not null) return error;
            var errores = ReglasLeads.ValidarNota(request?.Texto);
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasLeads.MensajeRevisar, errores));
            if (lead!.Estado == Lead.Cerrado) return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeCerrado));

            var texto = request!.Texto!.Trim();
            lead.ActualizadoEn = ahora;
            db.EventosLead.Add(new EventoLead
            {
                LeadId = id, Tipo = EventoLead.Nota, ActorUsuarioId = actor!.UsuarioId, Texto = texto, CreadoEn = ahora
            });
            Auditoria.AgregarEvento(db, actor.UsuarioId, Auditoria.EntidadLead, BarridoLeads.Id(id), Auditoria.Nota, null,
                new Dictionary<string, object?> { ["longitud"] = texto.Length }, ahora);
            await db.SaveChangesAsync(ct);
            return Results.Json(await DetalleAsync(db, id, actor, ct), statusCode: StatusCodes.Status201Created);
        });

        // HU-033, CA-P5-39, CA-P5-40: cierre con resultado.
        leads.MapPost("/{id}/cerrar", async (long id, CerrarLeadRequest? request, ClaimsPrincipal principal, CamionDbContext db,
            TimeProvider reloj, CancellationToken ct) =>
        {
            var (actor, lead, ahora, error) = await PrepararAsync(principal, db, reloj, id, ct);
            if (error is not null) return error;
            var errores = ReglasLeads.ValidarCierre(request, out var resultado, out var observacion);
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasLeads.MensajeRevisar, errores));
            if (lead!.Estado == Lead.Cerrado) return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeYaCerrado));

            lead.Estado = Lead.Cerrado;
            lead.ResultadoCierre = resultado;
            lead.ObservacionCierre = observacion;
            lead.CerradoPor = actor!.UsuarioId;
            lead.CerradoEn = ahora;
            lead.ActualizadoEn = ahora;
            db.EventosLead.Add(new EventoLead
            {
                LeadId = id, Tipo = EventoLead.CerradoTipo, ActorUsuarioId = actor.UsuarioId, Texto = observacion,
                ResultadoCierre = resultado, CreadoEn = ahora
            });
            Auditoria.AgregarEvento(db, actor.UsuarioId, Auditoria.EntidadLead, BarridoLeads.Id(id), Auditoria.Cerrar,
                new Dictionary<string, object?> { ["estado"] = Lead.Abierto },
                new Dictionary<string, object?> { ["estado"] = Lead.Cerrado, ["resultado"] = resultado }, ahora);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await DetalleAsync(db, id, actor, ct));
        });

        // CA-P5-41: solo el administrador reabre, con motivo; vuelve a ABIERTO con el mismo responsable.
        leads.MapPost("/{id}/reabrir", async (long id, ReabrirLeadRequest? request, ClaimsPrincipal principal, CamionDbContext db,
            TimeProvider reloj, CancellationToken ct) =>
        {
            var (actor, lead, ahora, error) = await PrepararAsync(principal, db, reloj, id, ct);
            if (error is not null) return error;
            if (!actor!.EsAdmin) return SinPermiso();
            var errores = ValidacionAuth.ValidarMotivo(request?.Motivo, obligatorio: true);
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasLeads.MensajeRevisar, errores));
            if (lead!.Estado == Lead.Abierto) return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeYaAbierto));

            var motivo = ValidacionAuth.NormalizarOpcional(request!.Motivo)!;
            var resultadoAnterior = lead.ResultadoCierre;
            lead.Estado = Lead.Abierto;
            lead.ResultadoCierre = null;
            lead.ObservacionCierre = null;
            lead.CerradoPor = null;
            lead.CerradoEn = null;
            lead.ActualizadoEn = ahora;
            db.EventosLead.Add(new EventoLead
            {
                LeadId = id, Tipo = EventoLead.Reabierto, ActorUsuarioId = actor.UsuarioId, Texto = motivo, CreadoEn = ahora
            });
            Auditoria.AgregarEvento(db, actor.UsuarioId, Auditoria.EntidadLead, BarridoLeads.Id(id), Auditoria.Reabrir,
                new Dictionary<string, object?> { ["estado"] = Lead.Cerrado, ["resultado"] = resultadoAnterior },
                new Dictionary<string, object?> { ["estado"] = Lead.Abierto, ["motivo"] = motivo }, ahora);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await DetalleAsync(db, id, actor, ct));
        }).RequireAuthorization(Roles.PoliticaSoloAdministrador);

        // D-7, CA-P5-44 a CA-P5-48: reasignación manual auditada (quién, cuándo, de quién a quién y por qué).
        leads.MapPost("/{id}/reasignar", async (long id, ReasignarLeadRequest? request, ClaimsPrincipal principal,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var (actor, lead, ahora, error) = await PrepararAsync(principal, db, reloj, id, ct);
            if (error is not null) return error;
            if (!actor!.EsAdmin) return SinPermiso();
            var errores = ValidacionAuth.ValidarMotivo(request?.Motivo, obligatorio: true);
            var aAdministracion = request?.Administracion == true;
            long? destino = null;
            if (aAdministracion == (request?.AsesorId is not null))
                errores["asesorId"] = [ReglasLeads.MensajeAsesorAprobado];
            else if (!aAdministracion)
            {
                destino = request!.AsesorId;
                // CA-P5-47: con o sin plan vigente; perfil APROBADO, cuenta ACTIVA y rol ASESOR.
                if (!await db.PerfilesAsesor.AsNoTracking().AnyAsync(a => a.Id == destino && a.EstadoAutorizacion == PerfilAsesor.Aprobado, ct) ||
                    !await InventarioEndpoints.AsesorActivoAsync(db, destino!.Value, ct))
                    errores["asesorId"] = [ReglasLeads.MensajeAsesorAprobado];
            }
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasLeads.MensajeRevisar, errores));
            if (lead!.Estado == Lead.Cerrado) return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeCerrado));
            if (lead.ResponsableAsesorId == destino) return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeMismoResponsable));

            var motivo = ValidacionAuth.NormalizarOpcional(request!.Motivo)!;
            var anterior = lead.ResponsableAsesorId;
            lead.ResponsableAsesorId = destino;
            lead.AsignacionManual = destino is not null;
            lead.VistoPorResponsable = false;
            lead.ActualizadoEn = ahora;
            BarridoLeads.AgregarReasignacion(db, id, actor.UsuarioId, anterior, destino, motivo, automatico: false, ahora);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await DetalleAsync(db, id, actor, ct));
        }).RequireAuthorization(Roles.PoliticaSoloAdministrador);

        return app;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Ayudas
    // ---------------------------------------------------------------------------------------------------------------

    // D-7, RN-SE-006: el asesor solo ve los leads de los que es responsable ahora.
    private static IQueryable<Lead> Alcance(IQueryable<Lead> consulta, InventarioEndpoints.Actor actor) =>
        actor.EsAdmin ? consulta : consulta.Where(l => l.ResponsableAsesorId == actor.PerfilAsesorId);

    // D-48: el responsable es el asesor asignado o, si el lead es de Administración, cualquier administrador.
    private static bool EsResponsable(InventarioEndpoints.Actor actor, long? responsable) =>
        actor.EsAdmin ? responsable is null : responsable == actor.PerfilAsesorId;

    /// <summary>Actor, barrido y lead con seguimiento (dentro del alcance). error != null si hay que responder ya.</summary>
    private static async Task<(InventarioEndpoints.Actor? Actor, Lead? Lead, DateTime Ahora, IResult? Error)> PrepararAsync(
        ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj, long id, CancellationToken ct)
    {
        var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
        var ahora = reloj.GetUtcNow().UtcDateTime;
        if (actor is null) return (null, null, ahora, SinPermiso());
        await BarridoLeads.BarrerAsync(db, ahora, ct);
        var lead = await Alcance(db.Leads, actor).FirstOrDefaultAsync(l => l.Id == id, ct);
        return lead is null ? (actor, null, ahora, NoEncontrado()) : (actor, lead, ahora, null);
    }

    private static IQueryable<FilaLead> Proyectar(CamionDbContext db, IQueryable<Lead> consulta) =>
        consulta.Select(l => new FilaLead
        {
            Id = l.Id,
            Estado = l.Estado,
            Nombre = l.Nombre,
            Telefono = l.Telefono,
            Correo = l.Correo,
            Mensaje = l.Mensaje,
            CreadoEn = l.CreadoEn,
            Visto = l.VistoPorResponsable,
            ResponsableAsesorId = l.ResponsableAsesorId,
            ResponsableNombre = (from a in db.PerfilesAsesor
                                 join u in db.Users on a.UsuarioId equals u.Id
                                 where a.Id == l.ResponsableAsesorId
                                 select u.NombreCompleto).FirstOrDefault(),
            ResultadoCierre = l.ResultadoCierre,
            ObservacionCierre = l.ObservacionCierre,
            CerradoPor = l.CerradoPor,
            CerradoEn = l.CerradoEn,
            PublicacionId = l.PublicacionId,
            VehiculoId = l.Publicacion!.VehiculoId,
            Titulo = l.Publicacion.Titulo,
            Slug = l.Publicacion.SlugPublico,
            EstadoVehiculo = l.Publicacion.Vehiculo!.Estado,
            EstadoPublicacion = l.Publicacion.EstadoPublicacion,
            EstadoComercial = l.Publicacion.EstadoComercial
        });

    private static LeadItem Item(FilaLead f, InventarioEndpoints.Actor actor) => new(
        f.Id, f.Estado, f.Nombre, f.Telefono, f.Correo, f.Mensaje, ConsultasCatalogo.Utc(f.CreadoEn), Nuevo(f, actor),
        Vehiculo(f), Responsable(f.ResponsableAsesorId, f.ResponsableNombre), f.ResultadoCierre,
        f.CerradoEn is { } c ? ConsultasCatalogo.Utc(c) : null);

    private static bool Nuevo(FilaLead f, InventarioEndpoints.Actor actor) =>
        f.Estado == Lead.Abierto && !f.Visto && EsResponsable(actor, f.ResponsableAsesorId);

    private static VehiculoLead Vehiculo(FilaLead f) => new(
        f.VehiculoId, f.PublicacionId, f.Titulo ?? "", f.Slug, f.EstadoVehiculo,
        f.EstadoPublicacion != Publicacion.Eliminada && f.EstadoComercial == Publicacion.Vendido
            ? ReglasLeads.EstadoVendida
            : f.EstadoPublicacion);

    private static ResponsableLead Responsable(long? asesorId, string? nombre) => asesorId is null
        ? new ResponsableLead(ReglasLeads.ResponsableAdministracion, null, null)
        : new ResponsableLead(ReglasLeads.ResponsableAsesor, asesorId, nombre);

    private static async Task<LeadDetalle> DetalleAsync(CamionDbContext db, long id, InventarioEndpoints.Actor actor,
        CancellationToken ct)
    {
        var fila = await Proyectar(db, db.Leads.AsNoTracking().Where(l => l.Id == id)).FirstAsync(ct);
        return await DetalleAsync(db, fila, actor, ct);
    }

    private static async Task<LeadDetalle> DetalleAsync(CamionDbContext db, FilaLead f, InventarioEndpoints.Actor actor,
        CancellationToken ct)
    {
        var eventos = await db.EventosLead.AsNoTracking().Where(e => e.LeadId == f.Id)
            .OrderBy(e => e.CreadoEn).ThenBy(e => e.Id).ToListAsync(ct);
        var usuarios = eventos.Select(e => e.ActorUsuarioId).Append(f.CerradoPor).OfType<string>().Distinct().ToList();
        var nombresUsuario = await db.Users.AsNoTracking().Where(u => usuarios.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NombreCompleto, ct);
        var asesores = eventos.SelectMany(e => new[] { e.ResponsableAnteriorAsesorId, e.ResponsableNuevoAsesorId })
            .OfType<long>().Distinct().ToList();
        var nombresAsesor = await (from a in db.PerfilesAsesor.AsNoTracking()
                                   join u in db.Users on a.UsuarioId equals u.Id
                                   where asesores.Contains(a.Id)
                                   select new { a.Id, u.NombreCompleto }).ToDictionaryAsync(x => x.Id, x => x.NombreCompleto, ct);

        ResponsableLead? ResponsableEvento(EventoLead e, long? asesor) =>
            e.Tipo is EventoLead.Asignado or EventoLead.Reasignado
                ? Responsable(asesor, asesor is { } a ? nombresAsesor.GetValueOrDefault(a) : null)
                : null;

        var items = eventos.Select(e => new EventoLeadItem(
            e.Tipo, ConsultasCatalogo.Utc(e.CreadoEn),
            e.ActorUsuarioId is null
                ? new ActorEvento(ReglasLeads.ActorSistema, null)
                : new ActorEvento(ReglasLeads.ActorUsuario, nombresUsuario.GetValueOrDefault(e.ActorUsuarioId)),
            e.Automatico, e.Texto, e.ResultadoCierre,
            e.Tipo == EventoLead.Reasignado ? ResponsableEvento(e, e.ResponsableAnteriorAsesorId) : null,
            ResponsableEvento(e, e.ResponsableNuevoAsesorId))).ToArray();

        // CA-P5-16: solo el administrador ve la prueba de la autorización.
        IReadOnlyList<ConsentimientoItem>? consentimientos = null;
        if (actor.EsAdmin)
        {
            var entidadId = BarridoLeads.Id(f.Id);
            consentimientos = (await db.ConsentimientosDatos.AsNoTracking()
                    .Where(c => c.Entidad == Auditoria.EntidadLead && c.EntidadId == entidadId)
                    .OrderBy(c => c.AceptadoEn).ThenBy(c => c.Id).ToListAsync(ct))
                .Select(c => new ConsentimientoItem(c.Formulario, c.VersionPolitica, ConsultasCatalogo.Utc(c.AceptadoEn)))
                .ToArray();
        }

        var item = Item(f, actor);
        return new LeadDetalle(item.Id, item.Estado, item.Nombre, item.Telefono, item.Correo, item.Mensaje, item.CreadoEn,
            item.Nuevo, item.Vehiculo, item.Responsable, item.ResultadoCierre, item.CerradoEn, f.ObservacionCierre,
            f.CerradoPor is null ? null : nombresUsuario.GetValueOrDefault(f.CerradoPor), items, consentimientos);
    }

    private static DateOnly? LeerFecha(string? valor, string parametro, Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (DateOnly.TryParseExact(valor.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            return fecha;
        errores[parametro] = [ReglasLeads.MensajeFecha];
        return null;
    }

    private static long? LeerEntero(string? valor, long minimo, long maximo, string parametro, string mensaje,
        Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (!long.TryParse(valor.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var numero) ||
            numero < minimo || numero > maximo)
        {
            errores[parametro] = [mensaje];
            return null;
        }
        return numero;
    }

    private static IResult NoEncontrado() => Results.NotFound(new ErrorResponse(ReglasLeads.MensajeNoEncontrado));

    private static IResult SinPermiso() =>
        Results.Json(new ErrorResponse(MensajesError.SinPermiso), statusCode: StatusCodes.Status403Forbidden);

    private sealed class FilaLead
    {
        public long Id { get; init; }
        public string Estado { get; init; } = "";
        public string Nombre { get; init; } = "";
        public string Telefono { get; init; } = "";
        public string? Correo { get; init; }
        public string Mensaje { get; init; } = "";
        public DateTime CreadoEn { get; init; }
        public bool Visto { get; set; }
        public long? ResponsableAsesorId { get; init; }
        public string? ResponsableNombre { get; init; }
        public string? ResultadoCierre { get; init; }
        public string? ObservacionCierre { get; init; }
        public string? CerradoPor { get; init; }
        public DateTime? CerradoEn { get; init; }
        public long PublicacionId { get; init; }
        public long VehiculoId { get; init; }
        public string? Titulo { get; init; }
        public string Slug { get; init; } = "";
        public string EstadoVehiculo { get; init; } = "";
        public string EstadoPublicacion { get; init; } = "";
        public string EstadoComercial { get; init; } = "";
    }
}
