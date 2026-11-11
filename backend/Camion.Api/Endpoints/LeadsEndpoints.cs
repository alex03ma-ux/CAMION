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
    // P6 (CA-P6-51): coincidencias devueltas como máximo.
    private const int MaxCoincidencias = 20;
    // Las fechas de los filtros son días de Colombia (UTC-5, sin horario de verano).
    private static readonly TimeSpan DesfaseColombia = TimeSpan.FromHours(5);

    public static IEndpointRouteBuilder MapLeadsEndpoints(this IEndpointRouteBuilder app)
    {
        var leads = app.MapGroup("/api/leads").RequireAuthorization(Roles.PoliticaAsesorOAdministrador);

        // CA-P5-29, CA-P5-31: "Mis contactos" (asesor) y "Contactos" (administrador), del más reciente al más antiguo.
        leads.MapGet("", async (string? estado, string? origen, string? responsable, string? publicacionId, string? vehiculoId,
            string? desde, string? hasta, string? pagina, string? tamano, ClaimsPrincipal principal, CamionDbContext db,
            TimeProvider reloj, CancellationToken ct) =>
        {
            var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();

            var errores = new Dictionary<string, string[]>();
            var estadoFiltro = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
            if (estadoFiltro is not null && !Lead.Estados.Contains(estadoFiltro)) errores["estado"] = [ReglasLeads.MensajeEstado];
            // P5b (CA-P5b-37): filtro por origen.
            var origenFiltro = string.IsNullOrWhiteSpace(origen) ? null : origen.Trim().ToUpperInvariant();
            if (origenFiltro is not null && !Lead.Origenes.Contains(origenFiltro)) errores["origen"] = [ReglasLeads.MensajeOrigen];
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
            if (origenFiltro is not null) consulta = consulta.Where(l => l.Origen == origenFiltro);
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
            return Results.Ok(await DetalleAsync(db, fila, actor, reloj.GetUtcNow().UtcDateTime, ct));
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
            return Results.Json(await DetalleAsync(db, id, actor, ahora, ct), statusCode: StatusCodes.Status201Created);
        });

        // P6 (CA-P6-51, D-56 c A): "Ver coincidencias" bajo demanda; publicaciones visibles que cumplen los criterios.
        leads.MapGet("/{id}/coincidencias", async (long id, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
        {
            var (actor, lead, ahora, error) = await PrepararAsync(principal, db, reloj, id, ct);
            if (error is not null) return error;
            if (!actor!.EsAdmin) return SinPermiso();
            if (lead!.Origen != Lead.OrigenBusqueda) return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeSoloBusqueda));
            // Una búsqueda sin fila de criterios (dato incoherente; la API siempre la crea) -> 409 controlado, nunca 500.
            var c = await db.LeadsBusqueda.AsNoTracking().FirstOrDefaultAsync(b => b.LeadId == id, ct);
            if (c is null) return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeSinCriterios));

            var consulta = ConsultasCatalogo.VisiblesConDestacado(db, ahora)
                .Where(x => x.Publicacion.Vehiculo!.TipoVehiculoId == c.TipoVehiculoId);
            if (c.MarcaId is { } marca) consulta = consulta.Where(x => x.Publicacion.Vehiculo!.MarcaId == marca);
            if (c.LineaId is { } linea) consulta = consulta.Where(x => x.Publicacion.Vehiculo!.LineaId == linea);
            if (c.AnioDesde is { } desde) consulta = consulta.Where(x => x.Publicacion.Vehiculo!.ModeloAnio >= desde);
            if (c.AnioHasta is { } hasta) consulta = consulta.Where(x => x.Publicacion.Vehiculo!.ModeloAnio <= hasta);
            if (c.PresupuestoMaxCop is { } tope)
            {
                var maximo = (decimal)tope;
                consulta = consulta.Where(x => x.Precio <= maximo);
            }
            if (c.MunicipioId is { } municipio) consulta = consulta.Where(x => x.Publicacion.Vehiculo!.MunicipioId == municipio);
            else if (c.DepartamentoId is { } departamento)
                consulta = consulta.Where(x => x.Publicacion.Vehiculo!.Municipio!.DepartamentoId == departamento);

            var total = await consulta.CountAsync(ct);
            var filas = await ConsultasCatalogo.ATarjetas(consulta.OrderByDescending(x => x.Destacado)
                    .ThenByDescending(x => x.Publicacion.PublicadaEn).ThenBy(x => x.Publicacion.SlugPublico))
                .Take(MaxCoincidencias).ToListAsync(ct);
            var relacionadas = await RelacionadasVigentes(db, id).Select(r => r.PublicacionId).ToListAsync(ct);
            return Results.Ok(new CoincidenciasResponse(
                filas.Select(f => new CoincidenciaItem(f.Id, relacionadas.Contains(f.Id), ConsultasCatalogo.Tarjeta(f))).ToArray(),
                total));
        });

        // P6 (HU-036, CA-P6-50, CA-P6-52, CA-P6-54, CA-P6-55): el administrador relaciona una publicación visible.
        leads.MapPost("/{id}/vehiculos", async (long id, RelacionarVehiculoRequest? request, ClaimsPrincipal principal,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var (actor, lead, ahora, error) = await PrepararAsync(principal, db, reloj, id, ct);
            if (error is not null) return error;
            if (!actor!.EsAdmin) return SinPermiso();
            if (lead!.Origen != Lead.OrigenBusqueda) return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeSoloBusqueda));
            var errores = new Dictionary<string, string[]>();
            if (request?.PublicacionId is not ({ } pid and > 0)) errores["publicacionId"] = [ReglasLeads.MensajeElegirVehiculo];
            var nota = ValidacionAuth.NormalizarOpcional(request?.Nota);
            if (nota is { Length: > BusquedaVehiculo.MaxNota }) errores["nota"] = [ReglasLeads.MensajeNotaRelacion];
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasLeads.MensajeRevisar, errores));
            if (lead.Estado == Lead.Cerrado) return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeBusquedaCerrada));

            var publicacionId = request!.PublicacionId!.Value;
            var titulo = await ConsultasCatalogo.Visibles(db, ahora).Where(p => p.Id == publicacionId)
                .Select(p => p.Titulo).FirstOrDefaultAsync(ct);
            if (titulo is null) return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeNoPublicado));
            var fila = await db.BusquedaVehiculos.FirstOrDefaultAsync(r => r.LeadId == id && r.PublicacionId == publicacionId, ct);
            if (fila is { RetiradoEn: null }) return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeYaRelacionado));
            if (await RelacionadasVigentes(db, id).CountAsync(ct) >= BusquedaVehiculo.MaxVigentes)
                return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeTopeRelacionados));

            if (fila is null)
            {
                fila = new BusquedaVehiculo { LeadId = id, PublicacionId = publicacionId };
                db.BusquedaVehiculos.Add(fila);
            }
            // Relacionar de nuevo una retirada reactiva la misma fila (UNIQUE lead + publicación).
            fila.Nota = nota;
            fila.RelacionadoPor = actor.UsuarioId;
            fila.RelacionadoEn = ahora;
            fila.RetiradoPor = null;
            fila.RetiradoEn = null;
            lead.ActualizadoEn = ahora;
            db.EventosLead.Add(new EventoLead
            {
                LeadId = id, Tipo = EventoLead.VehiculoRelacionado, ActorUsuarioId = actor.UsuarioId,
                Texto = Recortar(titulo), CreadoEn = ahora
            });
            Auditoria.AgregarEvento(db, actor.UsuarioId, Auditoria.EntidadLead, BarridoLeads.Id(id), Auditoria.RelacionarVehiculo,
                null, new Dictionary<string, object?> { ["publicacionId"] = publicacionId }, ahora);
            await db.SaveChangesAsync(ct);
            return Results.Json(await DetalleAsync(db, id, actor, ahora, ct), statusCode: StatusCodes.Status201Created);
        });

        // P6 (CA-P6-53): retirar una relación vigente; la fila se conserva marcada como retirada.
        leads.MapPost("/{id}/vehiculos/{publicacionId}/retirar", async (long id, long publicacionId, ClaimsPrincipal principal,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var (actor, lead, ahora, error) = await PrepararAsync(principal, db, reloj, id, ct);
            if (error is not null) return error;
            if (!actor!.EsAdmin) return SinPermiso();
            if (lead!.Origen != Lead.OrigenBusqueda) return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeSoloBusqueda));
            if (lead.Estado == Lead.Cerrado) return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeBusquedaCerrada));
            var fila = await db.BusquedaVehiculos.Include(r => r.Publicacion)
                .FirstOrDefaultAsync(r => r.LeadId == id && r.PublicacionId == publicacionId && r.RetiradoEn == null, ct);
            if (fila is null) return Results.NotFound(new ErrorResponse(ReglasLeads.MensajeNoRelacionado));

            fila.RetiradoPor = actor.UsuarioId;
            fila.RetiradoEn = ahora;
            lead.ActualizadoEn = ahora;
            db.EventosLead.Add(new EventoLead
            {
                LeadId = id, Tipo = EventoLead.VehiculoRetirado, ActorUsuarioId = actor.UsuarioId,
                Texto = Recortar(fila.Publicacion?.Titulo), CreadoEn = ahora
            });
            Auditoria.AgregarEvento(db, actor.UsuarioId, Auditoria.EntidadLead, BarridoLeads.Id(id), Auditoria.RetirarVehiculo,
                null, new Dictionary<string, object?> { ["publicacionId"] = publicacionId }, ahora);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await DetalleAsync(db, id, actor, ahora, ct));
        });

        // HU-033, CA-P5-39, CA-P5-40: cierre con resultado.
        leads.MapPost("/{id}/cerrar", async (long id, CerrarLeadRequest? request, ClaimsPrincipal principal, CamionDbContext db,
            TimeProvider reloj, CancellationToken ct) =>
        {
            var (actor, lead, ahora, error) = await PrepararAsync(principal, db, reloj, id, ct);
            if (error is not null) return error;
            var errores = ReglasLeads.ValidarCierre(request, lead!.Origen, out var resultado, out var observacion);
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
            return Results.Ok(await DetalleAsync(db, id, actor, ahora, ct));
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
            return Results.Ok(await DetalleAsync(db, id, actor, ahora, ct));
        }).RequireAuthorization(Roles.PoliticaSoloAdministrador);

        // D-7, CA-P5-44 a CA-P5-48: reasignación manual auditada (quién, cuándo, de quién a quién y por qué).
        leads.MapPost("/{id}/reasignar", async (long id, ReasignarLeadRequest? request, ClaimsPrincipal principal,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var (actor, lead, ahora, error) = await PrepararAsync(principal, db, reloj, id, ct);
            if (error is not null) return error;
            if (!actor!.EsAdmin) return SinPermiso();
            // P5b (D-52 A, CA-P5b-43): un aviso nunca se reasigna; la base también lo impide (CK_Lead_AvisoAdmin).
            if (Lead.EsAviso(lead!.Origen)) return Results.Conflict(new ErrorResponse(ReglasLeads.MensajeAvisoAdministracion));
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
            return Results.Ok(await DetalleAsync(db, id, actor, ahora, ct));
        }).RequireAuthorization(Roles.PoliticaSoloAdministrador);

        return app;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Ayudas
    // ---------------------------------------------------------------------------------------------------------------

    // D-7, RN-SE-006: el asesor solo ve los leads de los que es responsable ahora. P5b (D-6, CA-P5b-46): nunca los avisos
    // (ya nacen en Administración y CK_Lead_AvisoAdmin lo garantiza; el filtro por origen es una segunda barrera).
    private static IQueryable<Lead> Alcance(IQueryable<Lead> consulta, InventarioEndpoints.Actor actor) =>
        actor.EsAdmin
            ? consulta
            : consulta.Where(l => l.ResponsableAsesorId == actor.PerfilAsesorId && l.Origen == Lead.OrigenContactoVehiculo);

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
            Origen = l.Origen,
            TipoVehiculo = l.TipoVehiculo,
            // P6 (D-57 a A): "En gestión" = al menos una nota.
            ConNotas = db.EventosLead.Any(e => e.LeadId == l.Id && e.Tipo == EventoLead.Nota),
            // P5b: los avisos no tienen publicación; los campos del vehículo quedan nulos.
            PublicacionId = l.PublicacionId,
            VehiculoId = l.Publicacion == null ? null : (long?)l.Publicacion.VehiculoId,
            Titulo = l.Publicacion == null ? null : l.Publicacion.Titulo,
            Slug = l.Publicacion == null ? null : l.Publicacion.SlugPublico,
            EstadoVehiculo = l.Publicacion == null ? null : l.Publicacion.Vehiculo!.Estado,
            EstadoPublicacion = l.Publicacion == null ? null : l.Publicacion.EstadoPublicacion,
            EstadoComercial = l.Publicacion == null ? null : l.Publicacion.EstadoComercial
        });

    private static LeadItem Item(FilaLead f, InventarioEndpoints.Actor actor) => new(
        f.Id, f.Estado, f.Nombre, f.Telefono, f.Correo, f.Mensaje, ConsultasCatalogo.Utc(f.CreadoEn), Nuevo(f, actor),
        Vehiculo(f), Responsable(f.ResponsableAsesorId, f.ResponsableNombre), f.ResultadoCierre,
        f.CerradoEn is { } c ? ConsultasCatalogo.Utc(c) : null, f.Origen, f.TipoVehiculo,
        ReglasLeads.EstadoGestion(f.Origen, f.Estado, f.ConNotas));

    private static bool Nuevo(FilaLead f, InventarioEndpoints.Actor actor) =>
        f.Estado == Lead.Abierto && !f.Visto && EsResponsable(actor, f.ResponsableAsesorId);

    // P5b (CA-P5b-38): null si el lead no tiene publicación (aviso).
    private static VehiculoLead? Vehiculo(FilaLead f) => f.PublicacionId is not { } publicacionId || f.VehiculoId is not { } vehiculoId
        ? null
        : new(vehiculoId, publicacionId, f.Titulo ?? "", f.Slug ?? "", f.EstadoVehiculo ?? "",
            f.EstadoPublicacion != Publicacion.Eliminada && f.EstadoComercial == Publicacion.Vendido
                ? ReglasLeads.EstadoVendida
                : f.EstadoPublicacion ?? "");

    private static ResponsableLead Responsable(long? asesorId, string? nombre) => asesorId is null
        ? new ResponsableLead(ReglasLeads.ResponsableAdministracion, null, null)
        : new ResponsableLead(ReglasLeads.ResponsableAsesor, asesorId, nombre);

    private static async Task<LeadDetalle> DetalleAsync(CamionDbContext db, long id, InventarioEndpoints.Actor actor,
        DateTime ahora, CancellationToken ct)
    {
        var fila = await Proyectar(db, db.Leads.AsNoTracking().Where(l => l.Id == id)).FirstAsync(ct);
        return await DetalleAsync(db, fila, actor, ahora, ct);
    }

    private static async Task<LeadDetalle> DetalleAsync(CamionDbContext db, FilaLead f, InventarioEndpoints.Actor actor,
        DateTime ahora, CancellationToken ct)
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
        // P6 (CA-P6-41): precio mostrado y si el vehículo sigue visible (para enlazar a la ficha).
        var vehiculo = item.Vehiculo;
        if (vehiculo is not null)
        {
            var precio = await db.Publicaciones.AsNoTracking().Where(p => p.Id == vehiculo.PublicacionId)
                .Select(p => p.Vehiculo!.PrecioCop ?? p.PrecioCop).FirstOrDefaultAsync(ct);
            var visible = await ConsultasCatalogo.Visibles(db, ahora).AnyAsync(p => p.Id == vehiculo.PublicacionId, ct);
            vehiculo = vehiculo with { PrecioCop = precio is { } v ? ConsultasCatalogo.Pesos(v) : null, Visible = visible };
        }
        // P6 (CA-P6-42, CA-P6-50, CA-P6-56): criterios y relaciones vigentes de una búsqueda.
        BusquedaCriterios? busqueda = null;
        IReadOnlyList<VehiculoRelacionadoItem>? relacionados = null;
        if (f.Origen == Lead.OrigenBusqueda)
        {
            busqueda = await ReglasBusqueda.CriteriosAsync(db, f.Id, ct);
            relacionados = await RelacionadosAsync(db, f.Id, ahora, ct);
        }
        return new LeadDetalle(item.Id, item.Estado, item.Nombre, item.Telefono, item.Correo, item.Mensaje, item.CreadoEn,
            item.Nuevo, vehiculo, item.Responsable, item.ResultadoCierre, item.CerradoEn, item.Origen, item.TipoVehiculo,
            f.ObservacionCierre,
            f.CerradoPor is null ? null : nombresUsuario.GetValueOrDefault(f.CerradoPor), items, consentimientos,
            item.EstadoGestion, busqueda, relacionados);
    }

    private static IQueryable<BusquedaVehiculo> RelacionadasVigentes(CamionDbContext db, long leadId) =>
        db.BusquedaVehiculos.AsNoTracking().Where(r => r.LeadId == leadId && r.RetiradoEn == null);

    /// <summary>CA-P6-56: relaciones vigentes con el estado actual del vehículo, de la más reciente a la más antigua.</summary>
    private static async Task<IReadOnlyList<VehiculoRelacionadoItem>> RelacionadosAsync(CamionDbContext db, long leadId,
        DateTime ahora, CancellationToken ct)
    {
        var filas = await RelacionadasVigentes(db, leadId)
            .OrderByDescending(r => r.RelacionadoEn).ThenByDescending(r => r.Id)
            .Select(r => new
            {
                r.PublicacionId,
                r.Publicacion!.VehiculoId,
                r.Publicacion.Titulo,
                r.Publicacion.SlugPublico,
                Precio = r.Publicacion.Vehiculo!.PrecioCop ?? r.Publicacion.PrecioCop,
                EstadoVehiculo = r.Publicacion.Vehiculo.Estado,
                r.Publicacion.EstadoPublicacion,
                r.Publicacion.EstadoComercial,
                r.Nota,
                r.RelacionadoEn,
                Por = db.Users.Where(u => u.Id == r.RelacionadoPor).Select(u => u.NombreCompleto).FirstOrDefault()
            }).ToListAsync(ct);
        if (filas.Count == 0) return [];
        var ids = filas.Select(x => x.PublicacionId).ToList();
        var visibles = await ConsultasCatalogo.Visibles(db, ahora).Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        return filas.Select(x => new VehiculoRelacionadoItem(x.PublicacionId, x.VehiculoId, x.Titulo ?? "", x.SlugPublico,
            x.Precio is { } v ? ConsultasCatalogo.Pesos(v) : null, x.EstadoVehiculo,
            x.EstadoPublicacion != Publicacion.Eliminada && x.EstadoComercial == Publicacion.Vendido
                ? ReglasLeads.EstadoVendida
                : x.EstadoPublicacion,
            visibles.Contains(x.PublicacionId), x.Nota, ConsultasCatalogo.Utc(x.RelacionadoEn), x.Por)).ToArray();
    }

    private static string? Recortar(string? texto) =>
        texto is null ? null : texto.Length <= ReglasLeads.MaxMensaje ? texto : texto[..ReglasLeads.MaxMensaje];

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
        public string Origen { get; init; } = Lead.OrigenContactoVehiculo;
        public string? TipoVehiculo { get; init; }
        public bool ConNotas { get; init; }
        public long? PublicacionId { get; init; }
        public long? VehiculoId { get; init; }
        public string? Titulo { get; init; }
        public string? Slug { get; init; }
        public string? EstadoVehiculo { get; init; }
        public string? EstadoPublicacion { get; init; }
        public string? EstadoComercial { get; init; }
    }
}
