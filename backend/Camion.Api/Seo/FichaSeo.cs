using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace Camion.Api.Seo;

/// <summary>Datos públicos de una ficha visible (los mismos de GET /api/catalogo/publicaciones/{slug}; sin asesor ni placa).</summary>
public sealed record DatosFicha(
    string Slug, string Titulo, string Tipo, string? Marca, string? Linea, int? Anio, int Kilometraje,
    string Municipio, string Departamento, long PrecioCop, bool Negociable, string EstadoComercial,
    string? Combustible, string? DescripcionComercial, string? FotoPortada, IReadOnlyList<string> Fotos);

/// <summary>
/// P10 (D-88 A): textos, etiquetas Open Graph / Twitter y JSON-LD de la ficha de un vehículo (CA-P10-22, 23, 31, 64 a 68,
/// 71, 73, 74). Todo valor que sale de la base se codifica: en HTML con <see cref="Html"/> y en el JSON-LD con un serializador
/// que escapa &lt;, &gt; y &amp; (no puede cerrar el bloque &lt;script&gt;).
/// </summary>
public static class FichaSeo
{
    public const string Sufijo = " | Financiamos tu Camión";
    public const string NombreSitio = "Venta de Camiones – Financiamos tu Camión";
    public const string ImagenGenerica = "/marca/og-portal.jpg";
    public const int MaxTitulo = 65;
    public const int MaxDescripcion = 160;
    public const int MaxDescripcionJsonLd = 500;
    public const int MaxImagenes = 10;

    // Atributos data-camion: el frontend reutiliza estas etiquetas en lugar de duplicarlas (CA-P10-28, CA-P10-80).
    // "noindex" es el mismo valor que ya usa frontend/src/seo.ts para el robots de P9.
    public const string DataRobots = "noindex";
    public const string DataJsonLdVehiculo = "jsonld-vehiculo";
    public const string DataJsonLdMigas = "jsonld-migas";

    /// <summary>Codificador HTML que conserva tildes y eñes, y codifica &lt; &gt; &amp; " ' (atributos y texto).</summary>
    private static readonly HtmlEncoder Codificador = HtmlEncoder.Create(UnicodeRanges.All);

    /// <summary>JSON sin sangría, con &lt; &gt; &amp; ' escapados como <… (el encoder siempre escapa esos caracteres).</summary>
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false
    };

    private static readonly NumberFormatInfo Miles = new() { NumberGroupSeparator = ".", NumberGroupSizes = [3] };

    public static string Html(string texto) => Codificador.Encode(texto);

    /// <summary>Separador de miles colombiano: 120000000 → "120.000.000" (como formatoMiles del frontend).</summary>
    public static string ConMiles(long valor) => valor.ToString("#,0", Miles);

    /// <summary>
    /// CA-P10-22: "Marca Línea Año en Municipio". Sin marca o sin línea (D-19), el título de la publicación en su lugar
    /// ("Título en Municipio"); sin año, se omite.
    /// </summary>
    public static string Nucleo(DatosFicha d)
    {
        var baseTitulo = !string.IsNullOrWhiteSpace(d.Marca) && !string.IsNullOrWhiteSpace(d.Linea)
            ? Unir(d.Marca, d.Linea, d.Anio?.ToString(CultureInfo.InvariantCulture))
            : d.Titulo.Trim();
        return string.IsNullOrWhiteSpace(d.Municipio) ? baseTitulo : $"{baseTitulo} en {d.Municipio.Trim()}";
    }

    /// <summary>CA-P10-22, CA-P10-31: con el sufijo salvo que pase de 65 caracteres.</summary>
    public static string TituloPagina(DatosFicha d)
    {
        var nucleo = Nucleo(d);
        return (nucleo + Sufijo).Length > MaxTitulo ? nucleo : nucleo + Sufijo;
    }

    /// <summary>D-17: el kilometraje no se muestra en carrocerías ni en maquinaria con 0 km (mostrarKilometraje del frontend).</summary>
    public static bool MostrarKilometraje(string tipo, int km)
    {
        var t = Normalizar(tipo);
        return t != "carroceria" && !(t == "maquinaria" && km == 0);
    }

    /// <summary>
    /// CA-P10-23: "Categoría Marca Línea Año[, N km] en Municipio, Departamento. Precio: $ N[ negociable].[ Reservado.]
    /// Escríbenos por WhatsApp o solicita tu crédito." cortada en 160 por palabra completa. No usa la descripción libre.
    /// </summary>
    public static string Descripcion(DatosFicha d)
    {
        var sb = new StringBuilder(Unir(d.Tipo, d.Marca, d.Linea, d.Anio?.ToString(CultureInfo.InvariantCulture)));
        if (MostrarKilometraje(d.Tipo, d.Kilometraje)) sb.Append(", ").Append(ConMiles(d.Kilometraje)).Append(" km");
        var lugar = UnirCon(", ", d.Municipio, d.Departamento);
        if (lugar.Length > 0) sb.Append(" en ").Append(lugar);
        sb.Append(". Precio: $ ").Append(ConMiles(d.PrecioCop));
        if (d.Negociable) sb.Append(" negociable");
        sb.Append('.');
        if (EsReservado(d)) sb.Append(" Reservado.");
        sb.Append(" Escríbenos por WhatsApp o solicita tu crédito.");
        return Recortar(sb.ToString(), MaxDescripcion);
    }

    /// <summary>Corta en <paramref name="max"/> caracteres (incluido "…") por palabra completa.</summary>
    public static string Recortar(string texto, int max)
    {
        texto = texto.Trim();
        if (texto.Length <= max) return texto;
        var corte = texto[..(max - 1)];
        var espacio = corte.LastIndexOf(' ');
        if (espacio > 0) corte = corte[..espacio];
        return corte.TrimEnd(' ', ',', '.', ';', ':') + "…";
    }

    public static string Canonica(string urlPublica, string slug) => $"{urlPublica}/vehiculo/{Uri.EscapeDataString(slug)}";

    public static string Absoluta(string urlPublica, string rutaRelativa) => urlPublica + rutaRelativa;

    /// <summary>CA-P10-73: foto de portada o, si no hay fotos, la imagen genérica del portal.</summary>
    public static string Imagen(string urlPublica, DatosFicha d) => Absoluta(urlPublica, d.FotoPortada ?? ImagenGenerica);

    /// <summary>
    /// Bloque que reemplaza el de index.html (CA-P10-71). Con la indexación apagada (D-89 A) lleva robots noindex y no lleva
    /// canonical (CA-P10-27, CA-P10-53).
    /// </summary>
    public static string Bloque(string urlPublica, bool permitirIndexacion, DatosFicha d)
    {
        var titulo = TituloPagina(d);
        var descripcion = Descripcion(d);
        var canonica = Canonica(urlPublica, d.Slug);
        var imagen = Imagen(urlPublica, d);
        var sb = new StringBuilder("\n");
        sb.Append("    <title>").Append(Html(titulo)).Append("</title>\n");
        Meta(sb, "name", "description", descripcion);
        if (permitirIndexacion)
            sb.Append("    <link rel=\"canonical\" href=\"").Append(Html(canonica)).Append("\" data-camion=\"canonical\" />\n");
        else
            sb.Append("    ").Append(MetaNoIndex).Append('\n');
        Meta(sb, "property", "og:type", "website");
        Meta(sb, "property", "og:site_name", NombreSitio);
        Meta(sb, "property", "og:locale", "es_CO");
        Meta(sb, "property", "og:title", titulo);
        Meta(sb, "property", "og:description", descripcion);
        Meta(sb, "property", "og:url", canonica);
        Meta(sb, "property", "og:image", imagen);
        Meta(sb, "property", "og:image:alt", d.Titulo.Trim());
        Meta(sb, "name", "twitter:card", "summary_large_image");
        Meta(sb, "name", "twitter:title", titulo);
        Meta(sb, "name", "twitter:description", descripcion);
        Meta(sb, "name", "twitter:image", imagen);
        sb.Append("    <script type=\"application/ld+json\" data-camion=\"").Append(DataJsonLdVehiculo).Append("\">")
            .Append(JsonLdVehiculo(urlPublica, d)).Append("</script>\n");
        sb.Append("    <script type=\"application/ld+json\" data-camion=\"").Append(DataJsonLdMigas).Append("\">")
            .Append(JsonLdMigas(urlPublica, d)).Append("</script>\n");
        sb.Append("    ");
        return sb.ToString();
    }

    public const string MetaNoIndex = "<meta name=\"robots\" content=\"noindex\" data-camion=\"" + DataRobots + "\" />";

    /// <summary>CA-P10-72: el bloque genérico del index.html más robots noindex.</summary>
    /// Si el frontend se compiló con la indexación apagada, el bloque genérico ya trae su propio robots noindex: se quitan
    /// todas las etiquetas robots del bloque y se pone una sola (nunca dos).
    public static string BloqueNoDisponible(string bloqueGenerico) =>
        SinRobots(bloqueGenerico).TrimEnd() + "\n    " + MetaNoIndex + "\n    ";

    private static readonly Regex MetaRobots = new(
        @"[ \t]*<meta\b[^>]*\bname\s*=\s*[""']?robots\b[^>]*>[ \t]*(\r?\n)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Quita las etiquetas &lt;meta name="robots"&gt; de un bloque (las del index.html compilado).</summary>
    public static string SinRobots(string bloque) => MetaRobots.Replace(bloque, "");

    /// <summary>CA-P10-64 a 66: Product + Vehicle, sin placa, asesor, WhatsApp, seller ni itemCondition.</summary>
    public static string JsonLdVehiculo(string urlPublica, DatosFicha d)
    {
        var canonica = Canonica(urlPublica, d.Slug);
        var o = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = new[] { "Product", "Vehicle" },
            ["name"] = Nucleo(d),
            ["url"] = canonica
        };
        var imagenes = d.Fotos.Take(MaxImagenes).Select(f => Absoluta(urlPublica, f)).ToArray();
        if (imagenes.Length > 0) o["image"] = imagenes;
        o["description"] = string.IsNullOrWhiteSpace(d.DescripcionComercial)
            ? Descripcion(d)
            : Recortar(d.DescripcionComercial, MaxDescripcionJsonLd);
        o["category"] = d.Tipo;
        if (!string.IsNullOrWhiteSpace(d.Marca)) o["brand"] = new Dictionary<string, object?> { ["@type"] = "Brand", ["name"] = d.Marca };
        if (!string.IsNullOrWhiteSpace(d.Linea)) o["model"] = d.Linea;
        if (d.Anio is { } anio) o["vehicleModelDate"] = anio.ToString(CultureInfo.InvariantCulture);
        if (MostrarKilometraje(d.Tipo, d.Kilometraje))
            o["mileageFromOdometer"] = new Dictionary<string, object?>
            {
                ["@type"] = "QuantitativeValue", ["value"] = d.Kilometraje, ["unitCode"] = "KMT"
            };
        if (!string.IsNullOrWhiteSpace(d.Combustible)) o["fuelType"] = d.Combustible;
        o["offers"] = new Dictionary<string, object?>
        {
            ["@type"] = "Offer",
            ["price"] = d.PrecioCop,
            ["priceCurrency"] = "COP",
            ["availability"] = EsReservado(d) ? "https://schema.org/LimitedAvailability" : "https://schema.org/InStock",
            ["url"] = canonica,
            ["availableAtOrFrom"] = new Dictionary<string, object?>
            {
                ["@type"] = "Place",
                ["address"] = new Dictionary<string, object?>
                {
                    ["@type"] = "PostalAddress",
                    ["addressLocality"] = d.Municipio,
                    ["addressRegion"] = d.Departamento,
                    ["addressCountry"] = "CO"
                }
            }
        };
        return JsonSerializer.Serialize(o, Json);
    }

    /// <summary>CA-P10-67: Inicio › Categoría › vehículo (sin la categoría si no es una de las cuatro de D-17).</summary>
    public static string JsonLdMigas(string urlPublica, DatosFicha d)
    {
        var elementos = new List<Dictionary<string, object?>> { Miga(1, "Inicio", urlPublica + "/") };
        if (Categorias.Ruta(d.Tipo) is { } categoria)
            elementos.Add(Miga(2, categoria.Nombre, urlPublica + categoria.Ruta));
        elementos.Add(Miga(elementos.Count + 1, Nucleo(d), Canonica(urlPublica, d.Slug)));
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = elementos
        }, Json);
    }

    private static Dictionary<string, object?> Miga(int posicion, string nombre, string url) => new()
    {
        ["@type"] = "ListItem", ["position"] = posicion, ["name"] = nombre, ["item"] = url
    };

    private static bool EsReservado(DatosFicha d) => d.EstadoComercial == Models.Publicacion.Reservado;

    private static void Meta(StringBuilder sb, string atributo, string nombre, string valor) =>
        sb.Append("    <meta ").Append(atributo).Append("=\"").Append(nombre).Append("\" content=\"").Append(Html(valor))
            .Append("\" data-camion=\"").Append(nombre).Append("\" />\n");

    private static string Unir(params string?[] partes) => UnirCon(" ", partes);

    private static string UnirCon(string separador, params string?[] partes) =>
        string.Join(separador, partes.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    public static string Normalizar(string texto)
    {
        var descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(descompuesto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }
}

/// <summary>P10 (§4, D-17): rutas de las cuatro categorías del portal, por nombre de tipo sin tildes ni mayúsculas.</summary>
public static class Categorias
{
    public sealed record Categoria(string Nombre, string Ruta);

    /// <summary>En el orden del encabezado (frontend/src/api/catalogo.ts, CATEGORIAS).</summary>
    public static readonly IReadOnlyList<(string Tipo, Categoria Categoria)> Todas =
    [
        ("carro", new("Carros", "/carros")),
        ("camion", new("Camiones", "/camiones")),
        ("carroceria", new("Carrocerías", "/carrocerias")),
        ("maquinaria", new("Maquinaria", "/maquinaria"))
    ];

    public static Categoria? Ruta(string tipo)
    {
        var t = FichaSeo.Normalizar(tipo);
        foreach (var (nombre, categoria) in Todas)
            if (nombre == t) return categoria;
        return null;
    }
}
