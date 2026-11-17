using System.Globalization;
using System.Text;
using System.Xml;
using Camion.Api.Catalogo;
using Camion.Api.Data;
using Camion.Api.Seguridad;
using Camion.Api.Seo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Camion.Api.Endpoints;

/// <summary>
/// P10 "Marca y visibilidad": /robots.txt, /sitemap.xml y /vehiculo/{slug} (D-88 A, D-89 A; CA-P10-51 a 60, 70 a 79,
/// 84 a 86). Viven en la raíz porque los buscadores y las redes las piden ahí. Anónimas, solo GET y HEAD (otro método: 405),
/// con o sin token responden igual, no escriben en la base ni en la auditoría y no exponen datos del asesor. La visibilidad
/// sale de <see cref="ConsultasCatalogo"/> (regla única de P3).
/// </summary>
public static class SeoEndpoints
{
    public const string CacheTexto = "public, max-age=3600";
    public const string CacheFicha = "public, max-age=300";
    public const string MensajeNoDisponible = "El portal no está disponible en este momento.";
    public const string MensajeSinSitemap = "No encontrado.";
    public const int MaxDirecciones = 50_000;
    private const int MaxSlug = 200;
    private static readonly string[] GetHead = ["GET", "HEAD"];
    public const string PrefijoFicha = "/vehiculo";

    /// <summary>Páginas públicas fijas del sitemap (CA-P10-55). /privacidad y /terminos no, mientras estén en revisión.</summary>
    public static readonly string[] PaginasFijas = ["/credito", "/vende-tu-camion", "/seguro-todo-riesgo", "/busco-un-vehiculo"];

    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder app, OpcionesPortal portal)
    {
        app.MapMethods("/robots.txt", GetHead, (HttpContext http) =>
        {
            http.Response.Headers.CacheControl = CacheTexto;
            return Results.Text(Robots(portal), "text/plain; charset=utf-8", Encoding.UTF8);
        }).AllowAnonymous();

        app.MapMethods("/sitemap.xml", GetHead, async (HttpContext http, CamionDbContext db, TimeProvider reloj,
            ILoggerFactory logs, CacheSitemap cache, IOptions<OpcionesLimitesSeo> limites, CancellationToken ct) =>
        {
            if (!portal.PermitirIndexacion)
                return Results.Text(MensajeSinSitemap, "text/plain; charset=utf-8", Encoding.UTF8, StatusCodes.Status404NotFound);
            // M-1: se reutiliza el XML generado durante unos minutos (caduca solo), sin consultar la base en cada petición.
            var xml = await cache.ObtenerAsync(reloj, limites.Value.SitemapCacheMinutos, () => Sitemap(db,
                reloj.GetUtcNow().UtcDateTime, portal.UrlPublica, logs.CreateLogger("Camion.Api.Seo"), ct), ct);
            http.Response.Headers.CacheControl = CacheTexto;
            return Results.Bytes(xml, "application/xml; charset=utf-8");
        }).AllowAnonymous().WithMetadata(new LimitePeticionesSeo());

        // D-88 A: solo si se configuró la plantilla; sin ella la ruta no existe y el proxy no debe enviar /vehiculo/ a la API.
        if (portal.RutaIndexHtml is not null)
            app.MapMethods("/vehiculo/{**slug}", GetHead, async (string? slug, HttpContext http, CamionDbContext db,
                TimeProvider reloj, PlantillaPortal plantilla, CancellationToken ct) =>
            {
                // CA-P10-76: política de seguridad del frontend en estas respuestas HTML (la de la API bloquearía la página).
                http.Items[ConfiguracionServidor.ClaveCspPropia] = ConfiguracionServidor.CspFrontend;
                var leida = plantilla.Obtener();
                if (leida is null)
                {
                    http.Response.Headers.CacheControl = "no-store";
                    return Results.Text(MensajeNoDisponible, "text/plain; charset=utf-8", Encoding.UTF8,
                        StatusCodes.Status503ServiceUnavailable);
                }

                // O-2: la ruta distingue mayúsculas, como el SPA (/Vehiculo/x es "no encontrada"). El enrutado de ASP.NET Core
                // no las distingue, así que se compara el prefijo exacto.
                var exacta = http.Request.Path.StartsWithSegments(PrefijoFicha, StringComparison.Ordinal);
                // O-1: /vehiculo/<slug>/ (una barra final) redirige 301 a /vehiculo/<slug>, con la misma consulta.
                if (exacta && slug is { Length: > 1 } conBarra && conBarra[^1] == '/' && !conBarra[..^1].Contains('/'))
                {
                    http.Response.Headers.CacheControl = CacheFicha;
                    // Codificado como la canónica (FichaSeo.Canonica), sin el origen.
                    var destino = PrefijoFicha + "/" + Uri.EscapeDataString(conBarra[..^1]) + http.Request.QueryString.ToUriComponent();
                    return Results.Redirect(destino, permanent: true);
                }
                var datos = !exacta || string.IsNullOrEmpty(slug) || slug.Length > MaxSlug
                    ? null
                    : await Ficha(db, reloj.GetUtcNow().UtcDateTime, slug, ct);
                http.Response.Headers.CacheControl = CacheFicha;
                if (datos is null)
                {
                    // CA-P10-72: mismo cuerpo para inexistente, no visible, vacío o demasiado largo.
                    http.Response.Headers["X-Robots-Tag"] = "noindex";
                    return Results.Text(leida.Con(FichaSeo.BloqueNoDisponible), "text/html; charset=utf-8", Encoding.UTF8,
                        StatusCodes.Status404NotFound);
                }
                if (!portal.PermitirIndexacion) http.Response.Headers["X-Robots-Tag"] = "noindex";
                return Results.Text(leida.Con(_ => FichaSeo.Bloque(portal.UrlPublica, portal.PermitirIndexacion, datos)),
                    "text/html; charset=utf-8", Encoding.UTF8);
            }).AllowAnonymous().WithMetadata(new LimitePeticionesSeo());

        return app;
    }

    /// <summary>CA-P10-51, CA-P10-52 (D-89 A).</summary>
    public static string Robots(OpcionesPortal portal) => portal.PermitirIndexacion
        ? "User-agent: *\nAllow: /api/catalogo/\nAllow: /api/portal/\nDisallow: /api/\nDisallow: /health\nAllow: /\n\n" +
          $"Sitemap: {portal.UrlPublica}/sitemap.xml\n"
        : "User-agent: *\nDisallow: /\n";

    /// <summary>
    /// CA-P10-55 a 57, 60, 89: una consulta para las categorías y otra para la lista de publicaciones visibles (sin una
    /// consulta por publicación). Más de 50.000 direcciones: se cortan y se avisa en el log.
    /// </summary>
    public static async Task<byte[]> Sitemap(CamionDbContext db, DateTime ahora, string urlPublica, ILogger log,
        CancellationToken ct)
    {
        var visibles = ConsultasCatalogo.Visibles(db, ahora);
        var idsTipo = visibles.Select(p => p.Vehiculo!.TipoVehiculoId).Distinct();
        var tipos = await db.TiposVehiculo.AsNoTracking().Where(t => t.Activo && idsTipo.Contains(t.Id))
            .Select(t => t.Nombre).ToListAsync(ct);
        var categorias = Categorias.Todas
            .Where(c => tipos.Any(t => FichaSeo.Normalizar(t) == c.Tipo)).Select(c => c.Categoria.Ruta);

        var fijas = new[] { "/", "/vehiculos" }.Concat(categorias).Concat(PaginasFijas).ToList();
        var cupo = MaxDirecciones - fijas.Count;
        var fichas = await visibles
            .OrderByDescending(p => p.PublicadaEn).ThenBy(p => p.SlugPublico)
            .Select(p => new { p.SlugPublico, Publicacion = p.ActualizadoEn, Vehiculo = p.Vehiculo!.ActualizadoEn })
            .Take(cupo + 1).ToListAsync(ct);
        if (fichas.Count > cupo)
        {
            log.LogWarning("sitemap.xml: hay más de {Maximo} direcciones; se publican solo las primeras {Maximo}.",
                MaxDirecciones, MaxDirecciones);
            fichas.RemoveAt(fichas.Count - 1);
        }

        using var memoria = new MemoryStream();
        var opciones = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
        using (var xml = XmlWriter.Create(memoria, opciones))
        {
            xml.WriteStartDocument();
            xml.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
            foreach (var ruta in fijas)
            {
                xml.WriteStartElement("url");
                xml.WriteElementString("loc", urlPublica + ruta);
                xml.WriteEndElement();
            }
            foreach (var f in fichas)
            {
                xml.WriteStartElement("url");
                xml.WriteElementString("loc", FichaSeo.Canonica(urlPublica, f.SlugPublico));
                var fecha = f.Publicacion > f.Vehiculo ? f.Publicacion : f.Vehiculo;
                xml.WriteElementString("lastmod", fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
            xml.WriteEndDocument();
        }
        return memoria.ToArray();
    }

    /// <summary>
    /// CA-P10-79: la misma regla y los mismos datos públicos que GET /api/catalogo/publicaciones/{slug}
    /// (<see cref="ConsultasCatalogo.VisiblesConDestacado"/>); null si no existe o no está visible.
    /// </summary>
    public static async Task<DatosFicha?> Ficha(CamionDbContext db, DateTime ahora, string slug, CancellationToken ct)
    {
        var fila = await ConsultasCatalogo.ATarjetas(ConsultasCatalogo.VisiblesConDestacado(db, ahora)
            .Where(x => x.Publicacion.SlugPublico == slug)).FirstOrDefaultAsync(ct);
        if (fila is null) return null;

        var extra = await db.Publicaciones.AsNoTracking().Where(p => p.Id == fila.Id).Select(p => new
        {
            p.VehiculoId,
            p.DescripcionComercial,
            Combustible = p.Vehiculo!.Combustible!.Nombre
        }).FirstAsync(ct);
        // Como el detalle (P4b): las fotos del vehículo; si no tiene (DEMO), las de la publicación. Portada primero.
        var fotos = await db.FotosVehiculo.AsNoTracking().Where(f => f.VehiculoId == extra.VehiculoId)
            .OrderByDescending(f => f.EsPortada).ThenBy(f => f.Orden).Select(f => f.ArchivoClave)
            .Take(FichaSeo.MaxImagenes).ToListAsync(ct);
        if (fotos.Count == 0)
            fotos = await db.FotosPublicacion.AsNoTracking().Where(f => f.PublicacionId == fila.Id)
                .OrderByDescending(f => f.EsPortada).ThenBy(f => f.Orden).Select(f => f.ArchivoClave)
                .Take(FichaSeo.MaxImagenes).ToListAsync(ct);

        return new DatosFicha(fila.Slug, fila.Titulo, fila.Tipo, fila.Marca, fila.Linea, fila.ModeloAnio, fila.Kilometraje,
            fila.Municipio, fila.Departamento, ConsultasCatalogo.Pesos(fila.PrecioCop), fila.Negociable, fila.EstadoComercial,
            extra.Combustible, extra.DescripcionComercial, ConsultasCatalogo.UrlFoto(fila.FotoClave),
            fotos.Select(ConsultasCatalogo.UrlFoto).OfType<string>().ToArray());
    }
}
