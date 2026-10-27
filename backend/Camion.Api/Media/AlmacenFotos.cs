namespace Camion.Api.Media;

// P4a (HU-014, RN-VE-001, D-20): almacenamiento de las fotos del vehículo fuera del repositorio y de la base.
// La interfaz permite sustituir la carpeta real por una temporal en las pruebas.
public interface IAlmacenFotos
{
    /// <summary>Guarda el contenido con la clave relativa (p. ej. vehiculos/5/&lt;guid&gt;.jpg). Crea las carpetas si faltan.</summary>
    Task GuardarAsync(string clave, Stream contenido, CancellationToken ct);

    /// <summary>Borra el archivo si existe. Nunca lanza por un archivo inexistente.</summary>
    void Eliminar(string clave);

    /// <summary>Ruta física segura dentro de la raíz, o null si la clave sale de la raíz o no es una ruta válida.</summary>
    string? RutaSegura(string clave);
}

public sealed class AlmacenFotosLocal : IAlmacenFotos
{
    public const string RutaPorDefecto = @"C:\CAMION_media";

    private readonly string _raiz;

    public AlmacenFotosLocal(string? raiz)
    {
        var ruta = string.IsNullOrWhiteSpace(raiz) ? RutaPorDefecto : raiz.Trim();
        // Raíz absoluta y terminada en separador para que la comprobación de prefijo no acepte "C:\CAMION_media2".
        _raiz = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ruta)) + Path.DirectorySeparatorChar;
    }

    public string Raiz => _raiz;

    public async Task GuardarAsync(string clave, Stream contenido, CancellationToken ct)
    {
        var destino = RutaSegura(clave) ?? throw new ArgumentException("Clave de archivo no válida.", nameof(clave));
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        // CreateNew: nunca sobrescribe un archivo existente.
        await using var archivo = new FileStream(destino, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await contenido.CopyToAsync(archivo, ct);
    }

    public void Eliminar(string clave)
    {
        var ruta = RutaSegura(clave);
        if (ruta is not null && File.Exists(ruta)) File.Delete(ruta);
    }

    public string? RutaSegura(string clave)
    {
        if (string.IsNullOrWhiteSpace(clave)) return null;
        var relativa = clave.Replace('\\', '/');
        // Sin rutas absolutas, unidades, flujos alternativos (:) ni segmentos "..".
        if (relativa.StartsWith('/') || relativa.Contains(':') || relativa.Contains('\0') ||
            relativa.Split('/').Any(s => s == ".." || s == "."))
            return null;
        try
        {
            var completa = Path.GetFullPath(Path.Combine(_raiz, relativa.Replace('/', Path.DirectorySeparatorChar)));
            return completa.StartsWith(_raiz, StringComparison.OrdinalIgnoreCase) && completa.Length > _raiz.Length
                ? completa
                : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}

// Formatos admitidos, reconocidos por la firma de los bytes (no por la extensión ni por el Content-Type).
public static class FormatoImagen
{
    public const long TamanoMaximo = 5 * 1024 * 1024;
    public const long CuerpoMaximo = 6 * 1024 * 1024;

    public static readonly string[] Extensiones = ["jpg", "jpeg", "png", "webp"];

    /// <summary>Extensión que corresponde a la firma (jpg, png o webp), o null si no es un formato admitido.</summary>
    public static string? Detectar(ReadOnlySpan<byte> cabecera)
    {
        if (cabecera.Length >= 3 && cabecera[0] == 0xFF && cabecera[1] == 0xD8 && cabecera[2] == 0xFF) return "jpg";
        if (cabecera.Length >= 4 && cabecera[0] == 0x89 && cabecera[1] == 0x50 && cabecera[2] == 0x4E && cabecera[3] == 0x47)
            return "png";
        if (cabecera.Length >= 12 &&
            cabecera[0] == (byte)'R' && cabecera[1] == (byte)'I' && cabecera[2] == (byte)'F' && cabecera[3] == (byte)'F' &&
            cabecera[8] == (byte)'W' && cabecera[9] == (byte)'E' && cabecera[10] == (byte)'B' && cabecera[11] == (byte)'P')
            return "webp";
        return null;
    }

    public static string? TipoContenido(string extension) => extension.ToLowerInvariant() switch
    {
        "jpg" or "jpeg" => "image/jpeg",
        "png" => "image/png",
        "webp" => "image/webp",
        _ => null
    };
}
