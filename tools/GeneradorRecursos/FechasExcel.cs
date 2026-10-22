using System.Globalization;
using System.Text.RegularExpressions;

namespace PasheCPE.GeneradorRecursos;

/// <summary>
/// Fechas del Excel: llegan como serial de Excel (46388 = 01/01/2027) o como texto dd/MM/yyyy, a veces con
/// varias fechas en la misma celda («01/08/2026 01/01/2027» cuando SUNAT posterga una vigencia).
/// </summary>
internal static partial class FechasExcel
{
    public static IReadOnlyList<DateOnly> Interpretar(string? valor)
    {
        var texto = Texto.UnaLinea(valor);
        if (texto.Length == 0)
        {
            return [];
        }

        if (SerialExcel().IsMatch(texto))
        {
            var serial = double.Parse(texto, CultureInfo.InvariantCulture);
            return [DateOnly.FromDateTime(DateTime.FromOADate(serial))];
        }

        var fechas = new List<DateOnly>();
        foreach (Match coincidencia in FechaTexto().Matches(texto))
        {
            if (DateOnly.TryParseExact(coincidencia.Value, "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            {
                fechas.Add(fecha);
            }
        }

        return fechas;
    }

    /// <summary>La última fecha de la celda (la vigente cuando hubo postergación); null si no hay fecha.</summary>
    public static DateOnly? Ultima(string? valor) => Interpretar(valor) is [.., var ultima] ? ultima : null;

    [GeneratedRegex(@"^\d{5}(\.\d+)?$")]
    private static partial Regex SerialExcel();

    [GeneratedRegex(@"\b\d{1,2}/\d{1,2}/\d{4}\b")]
    private static partial Regex FechaTexto();
}
