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

// P5: contacto público desde la ficha del vehículo (HU-029, HU-030, RN-CL-001, RN-CL-002, RN-SE-005, D-3, D-7, D-9, D-44).
// Anónimo (o con token: el lead queda ligado a la cuenta). Fuera de /api/catalogo, que sigue siendo solo lectura.
// Nunca devuelve el id del lead ni a quién se asignó (CA-P5-54). Limitado por IP (LimitePeticionesContacto, Program.cs).
public static class ContactosEndpoints
{
    public static IEndpointRouteBuilder MapContactosEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contactos", async (HttpRequest http, ClaimsPrincipal principal, CamionDbContext db,
            TimeProvider reloj, IOptions<OpcionesContacto> opciones, IConfiguration configuracion,
            IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json, CancellationToken ct) =>
        {
            var limites = opciones.Value;

            // CA-P5-22: más de 8 KB -> 413 (se mide lo leído, no solo Content-Length).
            if (http.ContentLength > limites.TamanoMaximoBytes) return DemasiadoGrande();
            var cuerpo = await LeerHastaAsync(http.Body, limites.TamanoMaximoBytes, ct);
            if (cuerpo is null) return DemasiadoGrande();

            ContactoRequest? request;
            try
            {
                request = cuerpo.Length == 0 ? null
                    : JsonSerializer.Deserialize<ContactoRequest>(cuerpo, json.Value.SerializerOptions);
            }
            catch (JsonException)
            {
                request = null;
            }
            if (request is null) return Results.BadRequest(new ErrorResponse(MensajesError.FormatoInvalido));

            // CA-P5-22: campo trampa relleno -> la misma respuesta de éxito, sin guardar nada.
            if (!string.IsNullOrWhiteSpace(request.SitioWeb)) return Exito();

            var errores = ReglasLeads.ValidarContacto(request, out var datos);
            if (errores.Count > 0 || datos is null)
                return Results.BadRequest(new ErrorResponse(ReglasLeads.MensajeRevisar, errores));

            var ahora = reloj.GetUtcNow().UtcDateTime;
            // CA-P5-09: solo publicaciones contactables (sin exigir plan ni cuenta activa del dueño, CA-P5-26 y 27).
            var pub = await ConsultasCatalogo.Contactables(db, ahora).Where(p => p.SlugPublico == datos.Slug)
                .Select(p => new { p.Id, p.AsesorId }).FirstOrDefaultAsync(ct);
            if (pub is null) return Results.NotFound(new ErrorResponse(ReglasLeads.MensajeNoDisponible));

            // CA-P5-02: con un token válido el lead queda ligado a la cuenta. CA-P5-11: el dueño no se contacta a sí mismo.
            var usuarioId = principal.Identity?.IsAuthenticated == true ? principal.FindFirstValue(ClaimTypes.NameIdentifier) : null;
            if (usuarioId is not null &&
                await db.PerfilesAsesor.AsNoTracking().AnyAsync(a => a.Id == pub.AsesorId && a.UsuarioId == usuarioId, ct))
                return Results.Conflict(new ErrorResponse(ReglasLeads.MensajePropio));

            var version = configuracion["PoliticaDatos:Version"];
            if (string.IsNullOrWhiteSpace(version)) version = ReglasLeads.VersionPoliticaPorDefecto;

            // CA-P5-21 (y B-1 de la revisión de seguridad): envíos aceptados del mismo teléfono en 24 h, contando los leads
            // nuevos y los contactos repetidos. Va antes de la rama de repetidos para que varias IP no inflen sin fin el
            // historial ni consentimientos_datos.
            if (await EnviosTelefonoAsync(db, datos.Telefono, ahora, ct) >= limites.LimiteTelefonoDia) return MuchasSolicitudes();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // CA-P5-23: mismo teléfono y misma publicación con un lead ABIERTO -> contacto repetido en su historial.
            // M-P6-1 (D-7): solo contactos de vehículo; un crédito de esa ficha (siempre en Administración) no lo absorbe.
            // D-60: solo si la solicitud abierta es del mismo usuario (o ambas anónimas); nunca se liga a otra cuenta.
            var existente = await db.Leads.AsNoTracking()
                .Where(l => l.Origen == Lead.OrigenContactoVehiculo && l.PublicacionId == pub.Id &&
                            l.Telefono == datos.Telefono && l.Estado == Lead.Abierto &&
                            (usuarioId == null ? l.UsuarioId == null : l.UsuarioId == usuarioId))
                .OrderByDescending(l => l.Id).Select(l => (long?)l.Id).FirstOrDefaultAsync(ct);
            if (existente is { } leadId)
            {
                db.EventosLead.Add(new EventoLead
                {
                    LeadId = leadId,
                    Tipo = EventoLead.ContactoRepetido,
                    Texto = datos.Mensaje,
                    CreadoEn = ahora
                });
                db.ConsentimientosDatos.Add(Consentimiento(leadId, version, usuarioId, ahora));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Exito();
            }

            // CA-P5-25 a CA-P5-28: el responsable lo calcula siempre el servidor; nace ABIERTO.
            var responsable = await BarridoLeads.ResponsableAlCrearAsync(db, pub.AsesorId, ahora, ct);
            var lead = new Lead
            {
                PublicacionId = pub.Id,
                UsuarioId = usuarioId,
                Nombre = datos.Nombre,
                Telefono = datos.Telefono,
                Correo = datos.Correo,
                Mensaje = datos.Mensaje,
                Estado = Lead.Abierto,
                ResponsableAsesorId = responsable,
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
                ResponsableNuevoAsesorId = responsable,
                CreadoEn = ahora
            });
            // CA-P5-15: la prueba de la autorización, en la misma transacción.
            db.ConsentimientosDatos.Add(Consentimiento(lead.Id, version, usuarioId, ahora));
            Auditoria.AgregarEvento(db, null, Auditoria.EntidadLead, BarridoLeads.Id(lead.Id), Auditoria.Crear, null,
                new Dictionary<string, object?>
                {
                    ["publicacionId"] = pub.Id,
                    ["responsableAsesorId"] = responsable,
                    ["conCuenta"] = usuarioId is not null
                }, ahora);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Exito();
        })
        .AllowAnonymous()
        .WithMetadata(new LimitePeticionesContacto());

        return app;
    }

    private static ConsentimientoDatos Consentimiento(long leadId, string version, string? usuarioId, DateTime ahora) => new()
    {
        Formulario = ConsentimientoDatos.ContactoVehiculo,
        VersionPolitica = version,
        AceptadoEn = ahora,
        UsuarioId = usuarioId,
        Entidad = Auditoria.EntidadLead,
        EntidadId = BarridoLeads.Id(leadId)
    };

    /// <summary>
    /// CA-P5-21, CA-P5b-25: envíos aceptados del teléfono en las últimas 24 h, sin mirar el origen: leads creados (contactos y
    /// avisos) más eventos CONTACTO_REPETIDO de cualquier lead de ese teléfono.
    /// </summary>
    internal static async Task<int> EnviosTelefonoAsync(CamionDbContext db, string telefono, DateTime ahora, CancellationToken ct)
    {
        var desde = ahora.AddDays(-1);
        return await db.Leads.AsNoTracking().CountAsync(l => l.Telefono == telefono && l.CreadoEn > desde, ct) +
               await db.EventosLead.AsNoTracking().CountAsync(e => e.Tipo == EventoLead.ContactoRepetido &&
                   e.CreadoEn > desde && db.Leads.Any(l => l.Id == e.LeadId && l.Telefono == telefono), ct);
    }

    // Lee el cuerpo completo; null si pasa de "maximo" bytes. También lo usan los avisos de P5b.
    internal static async Task<byte[]?> LeerHastaAsync(Stream cuerpo, int maximo, CancellationToken ct)
    {
        using var memoria = new MemoryStream();
        var buffer = new byte[4096];
        int leidos;
        while ((leidos = await cuerpo.ReadAsync(buffer, ct)) > 0)
        {
            if (memoria.Length + leidos > maximo) return null;
            memoria.Write(buffer, 0, leidos);
        }
        return memoria.ToArray();
    }

    private static IResult Exito() =>
        Results.Json(new ContactoResponse(ReglasLeads.MensajeExito), statusCode: StatusCodes.Status201Created);

    internal static IResult DemasiadoGrande() =>
        Results.Json(new ErrorResponse(ReglasLeads.MensajeDemasiadoGrande), statusCode: StatusCodes.Status413PayloadTooLarge);

    public static IResult MuchasSolicitudes() =>
        Results.Json(new ErrorResponse(ReglasLeads.MensajeMuchas), statusCode: StatusCodes.Status429TooManyRequests);
}
