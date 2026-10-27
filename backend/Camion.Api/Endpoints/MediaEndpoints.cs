using Camion.Api.Contracts;
using Camion.Api.Media;

namespace Camion.Api.Endpoints;

// P4a (D-20): sirve las fotos de Media:Ruta en /media/... Es anónimo porque el catálogo público las necesita.
// Solo imágenes (jpg, jpeg, png, webp) y solo dentro de la carpeta raíz; cualquier otra ruta responde 404.
public static class MediaEndpoints
{
    private const string MensajeNoEncontrado = "Archivo no encontrado.";

    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/media/{**ruta}", (string? ruta, IAlmacenFotos almacen, HttpContext http) =>
        {
            var extension = Path.GetExtension(ruta ?? "").TrimStart('.').ToLowerInvariant();
            var tipo = FormatoImagen.Extensiones.Contains(extension) ? FormatoImagen.TipoContenido(extension) : null;
            // RutaSegura rechaza "..", rutas absolutas y unidades, y comprueba con Path.GetFullPath que quede dentro de la raíz.
            var fisica = tipo is null ? null : almacen.RutaSegura(ruta!);
            if (fisica is null || !File.Exists(fisica))
                return Results.NotFound(new ErrorResponse(MensajeNoEncontrado));

            http.Response.Headers.CacheControl = "public, max-age=86400";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(fisica, tipo);
        }).AllowAnonymous();
        return app;
    }
}
