namespace Camion.Api.Seo;

/// <summary>
/// P10 (M-1 de la revisión de seguridad): cupo por IP de /sitemap.xml y /vehiculo/* (sección "LimitesSeo") y caché del
/// sitemap en el servidor. Valores generosos: un buscador o una red social normales no los alcanzan. Las pruebas los suben en
/// CamionApiFactory y P10SeguridadTests los baja.
/// </summary>
public sealed class OpcionesLimitesSeo
{
    /// <summary>Peticiones por IP (IPv6 agrupada por /64) en cada ventana, sumando sitemap y fichas.</summary>
    public int LimiteVentana { get; set; } = 120;
    public int VentanaMinutos { get; set; } = 1;
    /// <summary>Minutos que se reutiliza el sitemap generado (0 = sin caché). Caduca solo.</summary>
    public int SitemapCacheMinutos { get; set; } = 10;
}

/// <summary>Marca las rutas con el cupo de <see cref="OpcionesLimitesSeo"/> (el limitador global de Program.cs la lee).</summary>
public sealed class LimitePeticionesSeo
{
    public const string Mensaje = "Demasiadas peticiones. Espera un momento e intenta de nuevo.";
}

/// <summary>
/// M-1: el sitemap ya generado, por URL pública, durante <see cref="OpcionesLimitesSeo.SitemapCacheMinutos"/> según el
/// <see cref="TimeProvider"/> de la API. Una sola generación a la vez (sin estampida de consultas).
/// </summary>
public sealed class CacheSitemap
{
    private readonly SemaphoreSlim _candado = new(1, 1);
    private (byte[] Xml, DateTimeOffset Hasta)? _actual;

    public async Task<byte[]> ObtenerAsync(TimeProvider reloj, int minutos, Func<Task<byte[]>> generar, CancellationToken ct)
    {
        if (minutos <= 0) return await generar();
        if (_actual is { } a && reloj.GetUtcNow() < a.Hasta) return a.Xml;
        await _candado.WaitAsync(ct);
        try
        {
            if (_actual is { } b && reloj.GetUtcNow() < b.Hasta) return b.Xml;
            var xml = await generar();
            _actual = (xml, reloj.GetUtcNow().AddMinutes(minutos));
            return xml;
        }
        finally
        {
            _candado.Release();
        }
    }
}
