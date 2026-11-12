using System.Security.Claims;
using System.Text.Json;
using Camion.Api.Auth;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Leads;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Camion.Api.Endpoints;

// P5b: avisos públicos "Vende tu camión" (HU-N01) y "Compra tu seguro todo riesgo" (HU-N02). D-3, D-6, D-7, D-9, D-44,
// D-50, D-52. Contrato: docs/criterios/P5b-contrato-api.md.
// Anónimos (con token opcional: el aviso queda ligado a la cuenta). Un aviso es un lead sin publicación, con origen propio,
// siempre en Administración (nunca un asesor; CK_Lead_AvisoAdmin). No crea vehículos ni publicaciones.
// Comparten con POST /api/contactos el límite por IP (LimitePeticionesContacto), el límite por teléfono, el tope de 8 KB y
// el campo trampa. Nunca devuelven el id del lead ni el responsable.
public static class AvisosEndpoints
{
    public static IEndpointRouteBuilder MapAvisosEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/avisos/vende-tu-camion", (HttpRequest http, ClaimsPrincipal principal, CamionDbContext db,
                TimeProvider reloj, IOptions<OpcionesContacto> opciones, IConfiguration configuracion,
                IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json, CancellationToken ct) =>
            RecibirAsync<VendeTuCamionRequest>(http, principal, db, reloj, opciones.Value, configuracion, json.Value, ct,
                r => r.SitioWeb, (VendeTuCamionRequest r, out ReglasLeads.DatosAviso? d) => ReglasLeads.ValidarVendeTuCamion(r, out d)))
        .AllowAnonymous()
        .WithMetadata(new LimitePeticionesContacto());

        app.MapPost("/api/avisos/seguro-todo-riesgo", (HttpRequest http, ClaimsPrincipal principal, CamionDbContext db,
                TimeProvider reloj, IOptions<OpcionesContacto> opciones, IConfiguration configuracion,
                IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json, CancellationToken ct) =>
            RecibirAsync<SeguroTodoRiesgoRequest>(http, principal, db, reloj, opciones.Value, configuracion, json.Value, ct,
                r => r.SitioWeb, (SeguroTodoRiesgoRequest r, out ReglasLeads.DatosAviso? d) => ReglasLeads.ValidarSeguro(r, out d)))
        .AllowAnonymous()
        .WithMetadata(new LimitePeticionesContacto());

        return app;
    }

    private delegate Dictionary<string, string[]> Validador<T>(T request, out ReglasLeads.DatosAviso? datos);

    // Orden (contrato P5b): 413 -> 400 formato -> trampa (201) -> 400 campos -> 429 teléfono -> repetido (201) -> nuevo (201).
    private static async Task<IResult> RecibirAsync<T>(HttpRequest http, ClaimsPrincipal principal, CamionDbContext db,
        TimeProvider reloj, OpcionesContacto limites, IConfiguration configuracion,
        Microsoft.AspNetCore.Http.Json.JsonOptions json, CancellationToken ct, Func<T, string?> trampa, Validador<T> validar)
        where T : class
    {
        // CA-P5b-23: más de 8 KB -> 413 (se mide lo leído, no solo Content-Length).
        if (http.ContentLength > limites.TamanoMaximoBytes) return ContactosEndpoints.DemasiadoGrande();
        var cuerpo = await ContactosEndpoints.LeerHastaAsync(http.Body, limites.TamanoMaximoBytes, ct);
        if (cuerpo is null) return ContactosEndpoints.DemasiadoGrande();

        T? request;
        try
        {
            request = cuerpo.Length == 0 ? null : JsonSerializer.Deserialize<T>(cuerpo, json.SerializerOptions);
        }
        catch (JsonException)
        {
            request = null;
        }
        if (request is null) return Results.BadRequest(new ErrorResponse(MensajesError.FormatoInvalido));

        // CA-P5b-22: campo trampa relleno -> la misma respuesta de éxito, sin guardar nada.
        if (!string.IsNullOrWhiteSpace(trampa(request))) return Exito();

        var errores = validar(request, out var datos);
        if (errores.Count > 0 || datos is null)
            return Results.BadRequest(new ErrorResponse(ReglasLeads.MensajeRevisar, errores));

        var ahora = reloj.GetUtcNow().UtcDateTime;
        // CA-P5b-02, 12, 35: con un token válido (cualquier rol) el aviso queda ligado a la cuenta; nunca cambia el responsable.
        var usuarioId = principal.Identity?.IsAuthenticated == true ? principal.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        var version = configuracion["PoliticaDatos:Version"];
        if (string.IsNullOrWhiteSpace(version)) version = ReglasLeads.VersionPoliticaPorDefecto;

        // CA-P5b-25: límite por teléfono compartido con el contacto de P5, antes de la rama de repetidos.
        if (await ContactosEndpoints.EnviosTelefonoAsync(db, datos.Telefono, ahora, ct) >= limites.LimiteTelefonoDia)
            return ContactosEndpoints.MuchasSolicitudes();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // CA-P5b-26, CA-P5b-27: mismo origen y mismo teléfono con un aviso ABIERTO -> contacto repetido en su historial.
        var existente = await db.Leads.AsNoTracking()
            .Where(l => l.Origen == datos.Origen && l.PublicacionId == null && l.Telefono == datos.Telefono &&
                        l.Estado == Lead.Abierto &&
                        // D-60: solo con el mismo usuario (o ambos anónimos).
                        (usuarioId == null ? l.UsuarioId == null : l.UsuarioId == usuarioId))
            .OrderByDescending(l => l.Id).Select(l => (long?)l.Id).FirstOrDefaultAsync(ct);
        if (existente is { } leadId)
        {
            db.EventosLead.Add(new EventoLead
            {
                LeadId = leadId,
                Tipo = EventoLead.ContactoRepetido,
                Texto = datos.Origen == Lead.OrigenSeguroTodoRiesgo
                    ? ReglasLeads.TextoSeguroRepetido(datos.TipoVehiculo)
                    : datos.Mensaje,
                CreadoEn = ahora
            });
            db.ConsentimientosDatos.Add(Consentimiento(datos.Origen, leadId, version, usuarioId, ahora));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Exito();
        }

        // CA-P5b-01, 11, 32: nace ABIERTO, sin publicación y en Administración (responsable_asesor_id = NULL).
        var lead = new Lead
        {
            PublicacionId = null,
            Origen = datos.Origen,
            TipoVehiculo = datos.TipoVehiculo,
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

        db.EventosLead.Add(new EventoLead { LeadId = lead.Id, Tipo = EventoLead.Creado, CreadoEn = ahora });
        db.EventosLead.Add(new EventoLead
        {
            LeadId = lead.Id,
            Tipo = EventoLead.Asignado,
            Automatico = true,
            ResponsableNuevoAsesorId = null,
            CreadoEn = ahora
        });
        // CA-P5b-20: la prueba de la autorización, en la misma transacción.
        db.ConsentimientosDatos.Add(Consentimiento(datos.Origen, lead.Id, version, usuarioId, ahora));
        // CA-P5b-34: sin nombre, teléfono, correo ni descripción.
        var auditoria = new Dictionary<string, object?>
        {
            ["origen"] = datos.Origen,
            ["responsableAsesorId"] = null,
            ["conCuenta"] = usuarioId is not null
        };
        if (datos.TipoVehiculo is not null) auditoria["tipoVehiculo"] = datos.TipoVehiculo;
        Auditoria.AgregarEvento(db, null, Auditoria.EntidadLead, BarridoLeads.Id(lead.Id), Auditoria.Crear, null, auditoria, ahora);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Exito();
    }

    // El formulario del consentimiento usa el mismo código que el origen (VENDE_TU_CAMION o SEGURO_TODO_RIESGO).
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

    private static IResult Exito() =>
        Results.Json(new ContactoResponse(ReglasLeads.MensajeExitoAviso), statusCode: StatusCodes.Status201Created);
}
