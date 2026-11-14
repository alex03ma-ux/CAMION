using System.Globalization;
using System.Text;

namespace Camion.Api.Reportes;

/// <summary>
/// P8 (HU-079, D-71 a): CSV UTF-8 con BOM, separador ";" y fin de línea CRLF, que Excel en español abre directamente.
/// CA-P8-44: una celda que empieza por = + - @, tabulador o retorno se escribe precedida de un apóstrofo para que Excel no
/// la ejecute como fórmula. Se genera en memoria; nunca se guarda en disco (CA-P8-50).
/// </summary>
public sealed class Csv
{
    public const char Separador = ';';
    private static readonly char[] InicioPeligroso = ['=', '+', '-', '@', '\t', '\r'];
    private static readonly char[] RequiereComillas = [Separador, '"', '\n', '\r'];

    private readonly StringBuilder _texto = new();

    public Csv Fila(params string?[] celdas)
    {
        _texto.Append(string.Join(Separador, celdas.Select(Celda))).Append("\r\n");
        return this;
    }

    public Csv Vacia()
    {
        _texto.Append("\r\n");
        return this;
    }

    public static string Num(long valor) => valor.ToString(CultureInfo.InvariantCulture);

    /// <summary>Celda escapada: apóstrofo ante fórmulas; comillas dobles si tiene separador, comillas o saltos de línea.</summary>
    public static string Celda(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return "";
        var limpio = valor.IndexOfAny(InicioPeligroso) == 0 ? "'" + valor : valor;
        return limpio.IndexOfAny(RequiereComillas) >= 0 ? "\"" + limpio.Replace("\"", "\"\"") + "\"" : limpio;
    }

    /// <summary>Bytes UTF-8 con BOM (EF BB BF).</summary>
    public byte[] Bytes()
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. utf8.GetPreamble(), .. utf8.GetBytes(_texto.ToString())];
    }

    public override string ToString() => _texto.ToString();
}
