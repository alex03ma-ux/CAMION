using System.Security.Claims;
using System.Text.Json;
using Camion.Api.Auth;
using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Leads;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Camion.Api.Endpoints;

// P6: solicitud de crédito (HU-039, HU-040, RN-CO-006) y búsqueda "Busco un vehículo" (HU-034, RN-BU-001). D-3, D-7, D-9,
// D-44, D-45, D-54 A, D-55 B, D-56 a A, D-58 A. Contrato: docs/criterios/P6-contrato-api.md.
// Anónimos (con token opcional: la solicitud queda ligada a la cuenta). Son leads con origen CREDITO o BUSQUEDA, siempre
// en Administración (nunca un asesor; CK_Lead_AvisoAdmin), también el crédito pedido desde la ficha de un vehículo.
// Comparten con P5 y P5b el límite por IP (LimitePeticionesContacto), el límite por teléfono, el tope de 8 KB y el campo
// trampa. El crédito no guarda datos financieros (D-55 B): CreditoRequest no los tiene y cualquier campo extra se descarta
// al leer el JSON. Nunca devuelven el id del lead ni el responsable.
public static class SolicitudesEndpoints
{
    public static IEndpointRouteBuilder MapSolicitudesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/creditos", RecibirCreditoAsync)
            .AllowAnonymous()
            .WithMetadata(new LimitePeticionesContacto());

        app.MapPost("/api/busquedas", RecibirBusquedaAsync)
            .AllowAnonymous()
            .WithMetadata(new LimitePeticionesContacto());

        return app;
    }

    // Orden (contrato P6): 413 -> 400 formato -> trampa (201) -> 400 campos -> 404 publicación -> 409 propio
    // -> 429 teléfono -> repetido (201) -> nuevo (201).
    private static async Task<IResult> RecibirCreditoAsync(HttpRequest http, ClaimsPrincipal principal, CamionDbContext db,
        TimeProvider reloj, IOptions<OpcionesContacto> opciones, IConfiguration configuracion,
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json, CancellationToken ct)
    {
        var limites = opciones.Value;
        var (request, error) = await LeerAsync<CreditoRequest>(http, limites, json.Value, ct);
        if (error is not null) return error;
        // CA-P6-29: campo trampa relleno -> la misma respuesta de éxito, sin guardar nada.
        if (!string.IsNullOrWhiteSpace(request!.SitioWeb)) return Exito();

        var slug = (request.Slug ?? "").Trim();
        var conVehiculo = slug.Length > 0;
        var errores = ReglasLeads.ValidarCredito(request, conVehiculo, out var datos);
        if (errores.Count > 0 || datos is null)
            return Results.BadRequest(new ErrorResponse(ReglasLeads.MensajeRevisar, errores));

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var usuarioId = UsuarioId(principal);
        long? publicacionId = null;
        if (conVehiculo)
        {
            // CA-P6-04: solo una publicación visible en el portal (misma regla del catálogo, con plan vigente).
            if (slug.Length > ReglasLeads.MaxSlug) return NoDisponible();
            var pub = await ConsultasCatalogo.Visibles(db, ahora).Where(p => p.SlugPublico == slug)
                .Select(p => new { p.Id, p.AsesorId }).FirstOrDefaultAsync(ct);
            if (pub is null) return NoDisponible();
            // CA-P6-05: el asesor dueño no pide crédito para su propio vehículo.
            if (usuarioId is not null &&
                await db.PerfilesAsesor.AsNoTracking().AnyAsync(a => a.Id == pub.AsesorId && a.UsuarioId == usuarioId, ct))
                return Results.Conflict(new ErrorResponse(ReglasLeads.MensajePropio));
            publicacionId = pub.Id;
        }

        return await GuardarAsync(db, configuracion, limites, datos, publicacionId, null, datos.Mensaje, usuarioId, ahora, ct);
    }

    // Orden: 413 -> 400 formato -> trampa (201) -> 400 campos y criterios -> 429 teléfono -> repetido (201) -> nuevo (201).
    private static async Task<IResult> RecibirBusquedaAsync(HttpRequest http, ClaimsPrincipal principal, CamionDbContext db,
        TimeProvider reloj, IOptions<OpcionesContacto> opciones, IConfiguration configuracion,
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json, CancellationToken ct)
    {
        var limites = opciones.Value;
        var (request, error) = await LeerAsync<BusquedaRequest>(http, limites, json.Value, ct);
        if (error is not null) return error;
        if (!string.IsNullOrWhiteSpace(request!.SitioWeb)) return Exito();

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var errores = ReglasLeads.ValidarTitularBusqueda(request, out var datos);
        // CA-P6-17 a CA-P6-21: el año de Colombia (UTC-5) fija el tope "año actual + 1".
        var criterios = await ReglasBusqueda.ValidarAsync(db, request, ahora.AddHours(-5).Year, errores, ct);
        if (errores.Count > 0 || datos is null || criterios is null)
            return Results.BadRequest(new ErrorResponse(ReglasLeads.MensajeRevisar, errores));

        // CA-P6-32: el repetido guarda un resumen legible de los criterios nuevos.
        var texto = ReglasBusqueda.TextoRepetido(await ReglasBusqueda.ConNombresAsync(db, criterios, ct), datos.Mensaje);
        return await GuardarAsync(db, configuracion, limites, datos, null, criterios, texto, UsuarioId(principal), ahora, ct);
    }

    /// <summary>
    /// CA-P6-27, 30 a 33, 37: límite por teléfono, repetido o lead nuevo con sus criterios, historial, consentimiento y
    /// auditoría, todo en una transacción.
    /// </summary>
    private static async Task<IResult> GuardarAsync(CamionDbContext db, IConfiguration configuracion, OpcionesContacto limites,
        ReglasLeads.DatosAviso datos, long? publicacionId, LeadBusqueda? criterios, string textoRepetido, string? usuarioId,
        DateTime ahora, CancellationToken ct)
    {
        var version = configuracion["PoliticaDatos:Version"];
        if (string.IsNullOrWhiteSpace(version)) version = ReglasLeads.VersionPoliticaPorDefecto;

        // CA-P6-30: límite por teléfono compartido con P5 y P5b, antes de la rama de repetidos.
        if (await ContactosEndpoints.EnviosTelefonoAsync(db, datos.Telefono, ahora, ct) >= limites.LimiteTelefonoDia)
            return ContactosEndpoints.MuchasSolicitudes();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // CA-P6-31 a CA-P6-33: mismo origen, mismo teléfono y (en el crédito) la misma publicación o ninguna.
        var existente = await db.Leads.AsNoTracking()
            .Where(l => l.Origen == datos.Origen && l.Telefono == datos.Telefono && l.Estado == Lead.Abierto &&
                        (publicacionId == null ? l.PublicacionId == null : l.PublicacionId == publicacionId) &&
                        // D-60: solo con el mismo usuario (o ambos anónimos); si no, solicitud nueva (CA-P6-06, CA-P6-23).
                        (usuarioId == null ? l.UsuarioId == null : l.UsuarioId == usuarioId))
            .OrderByDescending(l => l.Id).Select(l => (long?)l.Id).FirstOrDefaultAsync(ct);
        if (existente is { } leadId)
        {
            db.EventosLead.Add(new EventoLead
            {
                LeadId = leadId,
                Tipo = EventoLead.ContactoRepetido,
                Texto = textoRepetido,
                CreadoEn = ahora
            });
            db.ConsentimientosDatos.Add(Consentimiento(datos.Origen, leadId, version, usuarioId, ahora));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Exito();
        }

        // CA-P6-01, 03, 16, 37: nace ABIERTO y en Administración (responsable_asesor_id = NULL), sin tipo de vehículo.
        var lead = new Lead
        {
            PublicacionId = publicacionId,
            Origen = datos.Origen,
            TipoVehiculo = null,
            UsuarioId = usuarioId,
            Nombre = datos.Nombre,
            Telefono = datos.Telefono,
            Correo = datos.Correo,
            Mensaje = datos.Mensaje,
            Estado = Lead.Abierto,
            ResponsableAsesorId = null,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync(ct);

        if (criterios is not null)
        {
            criterios.LeadId = lead.Id;
            db.LeadsBusqueda.Add(criterios);
        }
        db.EventosLead.Add(new EventoLead { LeadId = lead.Id, Tipo = EventoLead.Creado, CreadoEn = ahora });
        db.EventosLead.Add(new EventoLead
        {
            LeadId = lead.Id,
            Tipo = EventoLead.Asignado,
            Automatico = true,
            ResponsableNuevoAsesorId = null,
            CreadoEn = ahora
        });
        db.ConsentimientosDatos.Add(Consentimiento(datos.Origen, lead.Id, version, usuarioId, ahora));
        // CA-P6-37: sin nombre, teléfono, correo, comentario ni criterios de la búsqueda.
        Auditoria.AgregarEvento(db, null, Auditoria.EntidadLead, BarridoLeads.Id(lead.Id), Auditoria.Crear, null,
            new Dictionary<string, object?>
            {
                ["origen"] = datos.Origen,
                ["responsableAsesorId"] = null,
                ["conCuenta"] = usuarioId is not null,
                ["conVehiculo"] = publicacionId is not null
            }, ahora);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Exito();
    }

    // CA-P6-29: más de 8 KB -> 413 (se mide lo leído); JSON malformado, vacío o con tipos incorrectos -> 400.
    private static async Task<(T? Request, IResult? Error)> LeerAsync<T>(HttpRequest http, OpcionesContacto limites,
        Microsoft.AspNetCore.Http.Json.JsonOptions json, CancellationToken ct) where T : class
    {
        if (http.ContentLength > limites.TamanoMaximoBytes) return (null, ContactosEndpoints.DemasiadoGrande());
        var cuerpo = await ContactosEndpoints.LeerHastaAsync(http.Body, limites.TamanoMaximoBytes, ct);
        if (cuerpo is null) return (null, ContactosEndpoints.DemasiadoGrande());
        T? request;
        try
        {
            request = cuerpo.Length == 0 ? null : JsonSerializer.Deserialize<T>(cuerpo, json.SerializerOptions);
        }
        catch (JsonException)
        {
            request = null;
        }
        return request is null ? (null, Results.BadRequest(new ErrorResponse(MensajesError.FormatoInvalido))) : (request, null);
    }

    // CA-P6-06, 23, 64: con un token válido (cualquier rol) la solicitud queda ligada a la cuenta; nunca cambia el responsable.
    private static string? UsuarioId(ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true ? principal.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    // El formulario del consentimiento usa el mismo código que el origen (CREDITO o BUSQUEDA).
    private static ConsentimientoDatos Consentimiento(string origen, long leadId, string version, string? usuarioId,
        DateTime ahora) => new()
    {
        Formulario = origen,
        VersionPolitica = version,
        AceptadoEn = ahora,
        UsuarioId = usuarioId,
        Entidad = Auditoria.EntidadLead,
        EntidadId = BarridoLeads.Id(leadId)
    };

    private static IResult NoDisponible() => Results.NotFound(new ErrorResponse(ReglasLeads.MensajeNoDisponible));

    private static IResult Exito() =>
        Results.Json(new ContactoResponse(ReglasLeads.MensajeExitoAviso), statusCode: StatusCodes.Status201Created);
}
