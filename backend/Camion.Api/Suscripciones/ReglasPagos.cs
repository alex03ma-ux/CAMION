using System.Globalization;
using Camion.Api.Contracts;
using Camion.Api.Models;
using Camion.Api.Publicaciones;

namespace Camion.Api.Suscripciones;

// P7 (D-61 B): validación del pago anotado por el administrador. Lógica pura, sin base de datos.
public static class ReglasPagos
{
    public const string MensajeValor = "Escribe el valor pagado en pesos (número entero mayor que 0).";
    public const string MensajeFecha = "Escribe la fecha del pago (AAAA-MM-DD), de hoy o anterior.";
    public const string MensajeMedio = "Elige el medio de pago.";
    public const string MensajeReferencia = "La referencia no puede tener más de 100 caracteres ni los signos < >.";
    public const string MensajeReferenciaOtro = "Escribe la referencia: es obligatoria si el medio es Otro.";
    public const string MensajePlanSinPrecio = "Este plan no tiene precio: no se registra un pago.";
    public const string MensajeNoEncontrado = "Pago no encontrado.";
    public const string MensajeYaAnulado = "Este pago ya está anulado.";

    public const int MaxReferencia = 100;
    public const decimal ValorMaximo = 9_999_999_999_999m;   // DECIMAL(15,2)

    /// <summary>Datos ya validados. ValorCop null = se usa el precio del plan.</summary>
    public sealed record DatosPago(decimal? ValorCop, DateOnly FechaPago, string Medio, string? Referencia);

    public static Dictionary<string, string[]> Validar(PagoRequest r, DateTime ahoraUtc, out DatosPago? datos)
    {
        var errores = new Dictionary<string, string[]>();
        datos = null;
        if (r.ValorCop is { } valor && (valor <= 0 || valor > ValorMaximo || decimal.Truncate(valor) != valor))
            errores["valorCop"] = [MensajeValor];

        DateOnly fecha = default;
        if (string.IsNullOrWhiteSpace(r.FechaPago) ||
            !DateOnly.TryParseExact(r.FechaPago.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha) ||
            fecha > ReglasP4b.HoyColombia(ahoraUtc))
            errores["fechaPago"] = [MensajeFecha];

        var medio = (r.Medio ?? "").Trim().ToUpperInvariant();
        if (!Pago.Medios.Contains(medio)) errores["medio"] = [MensajeMedio];

        var referencia = (r.Referencia ?? "").Trim();
        if (referencia.Length > MaxReferencia || referencia.IndexOfAny(['<', '>']) >= 0)
            errores["referencia"] = [MensajeReferencia];
        else if (referencia.Length == 0 && medio == Pago.Otro)
            errores["referencia"] = [MensajeReferenciaOtro];

        if (errores.Count == 0)
            datos = new DatosPago(r.ValorCop, fecha, medio, referencia.Length == 0 ? null : referencia);
        return errores;
    }

    public static Pago Nuevo(long suscripcionId, DatosPago d, decimal precioPlan, string concepto, string? adminId,
        DateTime ahora) => new()
    {
        SuscripcionId = suscripcionId,
        ValorCop = d.ValorCop ?? precioPlan,
        FechaPago = d.FechaPago,
        Medio = d.Medio,
        Referencia = d.Referencia,
        Concepto = concepto,
        Estado = Pago.Registrado,
        RegistradoPor = adminId,
        RegistradoEn = ahora,
        CreadoEn = ahora
    };

    /// <summary>Datos de auditoría del pago (sin datos personales).</summary>
    public static Dictionary<string, object?> Datos(Pago p) => new()
    {
        ["suscripcionId"] = p.SuscripcionId,
        ["valorCop"] = (long)Math.Round(p.ValorCop, MidpointRounding.AwayFromZero),
        ["fechaPago"] = p.FechaPago?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["medio"] = p.Medio,
        ["referencia"] = p.Referencia,
        ["concepto"] = p.Concepto,
        ["estado"] = p.Estado
    };
}
