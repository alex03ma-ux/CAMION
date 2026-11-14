using System.Globalization;
using System.Text;

namespace Camion.Api.Reportes;

/// <summary>
/// P8 (HU-079, D-71 a): CSV UTF-8 con BOM, separador ";" y fin de línea CRLF, que Excel en español abre directamente.
/// CA-P8-44: una celda que empieza por = + - @, tabulador o retorno (también tras espacios) se escribe precedida de un
/// apóstrofo para que Excel no la ejecute como fórmula; un número negativo puro no se escapa (B-3 de P8). Se genera en memoria; nunca se guarda en disco (CA-P8-50).
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
        // INF-2 (B-3 de P8): también se escapa si el carácter peligroso va tras espacios iniciales ("  =1+1"), y no se
        // escapa un número negativo puro ("-5", "-1,5"), que Excel y las demás hojas leen como número, nunca como fórmula.
        var peligroso = valor.IndexOfAny(InicioPeligroso) == 0 || valor.TrimStart().IndexOfAny(InicioPeligroso) == 0;
        var limpio = peligroso && !EsNegativoPuro(valor) ? "'" + valor : valor;
        return limpio.IndexOfAny(RequiereComillas) >= 0 ? "\"" + limpio.Replace("\"", "\"\"") + "\"" : limpio;
    }

    private static bool EsNegativoPuro(string valor)
    {
        if (valor.Length < 2 || valor[0] != '-') return false;
        var separadores = 0;
        for (var i = 1; i < valor.Length; i++)
        {
            var c = valor[i];
            if (c is '.' or ',' && i > 1 && i < valor.Length - 1 && ++separadores == 1) continue;
            if (c is < '0' or > '9') return false;
        }
        return true;
    }

    /// <summary>Bytes UTF-8 con BOM (EF BB BF).</summary>
    public byte[] Bytes()
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. utf8.GetPreamble(), .. utf8.GetBytes(_texto.ToString())];
    }

    public override string ToString() => _texto.ToString();
}
