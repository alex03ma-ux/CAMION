using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Media;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Camion.Api.Endpoints;

// D-42: foto de portada del portal, administrada por el administrador sin tocar código ni la base de datos.
// El archivo vive en Media:Ruta/portal/ con nombre aleatorio y fechado; la portada vigente es el más reciente.
// Sin foto, el portal muestra su ilustración propia. Cambiarla o quitarla queda en auditoria_eventos.
public static class PortalEndpoints
{
    public const string Carpeta = "portal";
    public const string EntidadPortal = "Portal";
    public const string EntidadIdPortada = "portada";
    public const string CambiarPortada = "CAMBIAR_PORTADA";
    public const string QuitarPortada = "QUITAR_PORTADA";
    public const string MensajeFoto = "Sube una imagen JPG, PNG o WebP de hasta 5 MB.";

    public static IEndpointRouteBuilder MapPortalEndpoints(this IEndpointRouteBuilder app)
    {
        // Público: el inicio pregunta qué foto de portada usar ({ url } o { url: null }).
        app.MapGet("/api/catalogo/portada", (IAlmacenFotos almacen) =>
            Results.Ok(new PortadaResponse(ConsultasCatalogo.UrlFoto(Vigente(almacen)))))
            .AllowAnonymous();

        var admin = app.MapGroup("/api/admin/portal").RequireAuthorization(Roles.PoliticaSoloAdministrador);

        admin.MapPost("/portada", async (HttpRequest http, ClaimsPrincipal principal, CamionDbContext db,
            IAlmacenFotos almacen, TimeProvider reloj, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var limite = http.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (limite is { IsReadOnly: false }) limite.MaxRequestBodySize = FormatoImagen.CuerpoMaximo;
            if (http.ContentLength > FormatoImagen.CuerpoMaximo || !http.HasFormContentType) return FotoNoValida();

            IFormFile? archivo;
            try
            {
                archivo = (await http.ReadFormAsync(ct)).Files.GetFile("archivo");
            }
            catch (InvalidDataException)
            {
                return FotoNoValida();
            }
            var extension = await ExtensionValidaAsync(archivo, ct);
            if (extension is null) return FotoNoValida();

            var ahora = reloj.GetUtcNow().UtcDateTime;
            var anteriores = almacen.Listar(Carpeta);
            // Nombre que ordena por fecha; el original del archivo nunca se usa.
            var clave = $"{Carpeta}/portada-{ahora:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.{extension}";
            await using (var contenido = archivo!.OpenReadStream())
                await almacen.GuardarAsync(clave, contenido, ct);

            try
            {
                Auditoria.AgregarEvento(db, principal.FindFirstValue(ClaimTypes.NameIdentifier), EntidadPortal, EntidadIdPortada,
                    CambiarPortada,
                    anteriores.Count == 0 ? null : new Dictionary<string, object?> { ["archivo"] = Vigente(anteriores) },
                    new Dictionary<string, object?> { ["archivo"] = clave }, ahora);
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                almacen.Eliminar(clave);
                throw;
            }

            // La anterior ya no se usa: se borra para no acumular archivos (queda constancia en la auditoría).
            foreach (var vieja in anteriores) EliminarSinFallar(almacen, vieja, loggerFactory);
            return Results.Ok(new PortadaResponse(ConsultasCatalogo.UrlFoto(clave)));
        }).WithMetadata(new RequestSizeLimitAttribute(FormatoImagen.CuerpoMaximo));

        admin.MapPost("/portada/quitar", async (ClaimsPrincipal principal, CamionDbContext db, IAlmacenFotos almacen,
            TimeProvider reloj, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var anteriores = almacen.Listar(Carpeta);
            if (anteriores.Count == 0) return Results.NoContent();
            Auditoria.AgregarEvento(db, principal.FindFirstValue(ClaimTypes.NameIdentifier), EntidadPortal, EntidadIdPortada,
                QuitarPortada, new Dictionary<string, object?> { ["archivo"] = Vigente(anteriores) },
                new Dictionary<string, object?> { ["archivo"] = null }, reloj.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
            foreach (var vieja in anteriores) EliminarSinFallar(almacen, vieja, loggerFactory);
            return Results.NoContent();
        });

        return app;
    }

    /// <summary>Clave de la portada vigente: la imagen admitida más reciente de la carpeta, o null.</summary>
    public static string? Vigente(IAlmacenFotos almacen) => Vigente(almacen.Listar(Carpeta));

    private static string? Vigente(IReadOnlyList<string> claves) => claves
        .Where(c => FormatoImagen.Extensiones.Contains(Path.GetExtension(c).TrimStart('.').ToLowerInvariant()))
        .OrderByDescending(c => c, StringComparer.Ordinal)
        .FirstOrDefault();

    private static IResult FotoNoValida() => Results.BadRequest(new ErrorResponse(MensajeFoto));

    private static async Task<string?> ExtensionValidaAsync(IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0 || archivo.Length > FormatoImagen.TamanoMaximo) return null;
        var cabecera = new byte[12];
        int leidos;
        await using (var lectura = archivo.OpenReadStream())
            leidos = await lectura.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false, ct);
        return FormatoImagen.Detectar(cabecera.AsSpan(0, leidos));
    }

    private static void EliminarSinFallar(IAlmacenFotos almacen, string clave, ILoggerFactory loggerFactory)
    {
        try
        {
            almacen.Eliminar(clave);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            loggerFactory.CreateLogger("Camion.Api.Media").LogWarning("No se pudo borrar una portada anterior: {Tipo}", ex.GetType().Name);
        }
    }
}

public sealed record PortadaResponse(string? Url);
