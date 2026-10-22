using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PasheCPE.GeneradorRecursos;

/// <summary>Lee el Excel de SUNAT en modo solo lectura, con los valores calculados de las celdas (no las fórmulas).</summary>
internal sealed partial class LibroExcel : IDisposable
{
    private readonly FileStream _flujo;
    private readonly SpreadsheetDocument _documento;
    private readonly string[] _cadenas;

    public LibroExcel(string ruta)
    {
        _flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        _documento = SpreadsheetDocument.Open(_flujo, isEditable: false);
        var tabla = _documento.WorkbookPart?.SharedStringTablePart?.SharedStringTable;
        _cadenas = tabla is null ? [] : [.. tabla.Elements<SharedStringItem>().Select(TextoDe)];
    }

    /// <summary>
    /// Devuelve las celdas no vacías de la hoja. Con <paramref name="aplicarCombinadas"/> cada celda de un rango
    /// combinado recibe el valor de la celda superior izquierda (así se leen las reglas agrupadas por campo).
    /// </summary>
    public HojaExcel Hoja(string nombre, bool aplicarCombinadas = false)
    {
        var libro = _documento.WorkbookPart ?? throw new InvalidDataException("El archivo no contiene un libro de Excel.");
        var hoja = libro.Workbook?.Sheets?.Elements<Sheet>()
            .FirstOrDefault(s => string.Equals(s.Name?.Value, nombre, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"No existe la hoja '{nombre}' en el Excel.");
        var hojaCalculo = ((WorksheetPart)libro.GetPartById(hoja.Id!.Value!)).Worksheet
            ?? throw new InvalidDataException($"La hoja '{nombre}' está vacía.");

        var filas = new SortedDictionary<int, Dictionary<string, string>>();
        foreach (var celda in hojaCalculo.Descendants<Cell>())
        {
            var valor = Valor(celda);
            if (string.IsNullOrWhiteSpace(valor) || celda.CellReference?.Value is not { } referencia)
            {
                continue;
            }

            var (columna, fila) = Referencia(referencia);
            FilaDe(filas, fila)[columna] = valor;
        }

        if (aplicarCombinadas)
        {
            foreach (var combinada in hojaCalculo.Descendants<MergeCell>())
            {
                if (combinada.Reference?.Value is not { } rango || rango.Split(':') is not [var inicio, var fin])
                {
                    continue;
                }

                var (columnaInicio, filaInicio) = Referencia(inicio);
                var (columnaFin, filaFin) = Referencia(fin);
                if (!filas.TryGetValue(filaInicio, out var origen) || !origen.TryGetValue(columnaInicio, out var valor))
                {
                    continue;
                }

                for (var fila = filaInicio; fila <= filaFin; fila++)
                {
                    for (var indice = IndiceColumna(columnaInicio); indice <= IndiceColumna(columnaFin); indice++)
                    {
                        FilaDe(filas, fila).TryAdd(LetraColumna(indice), valor);
                    }
                }
            }
        }

        return new HojaExcel(nombre, [.. filas.Select(p => new FilaExcel(p.Key, p.Value))]);
    }

    public void Dispose()
    {
        _documento.Dispose();
        _flujo.Dispose();
    }

    private string? Valor(Cell celda)
    {
        var texto = celda.CellValue?.Text;
        var tipo = celda.DataType?.Value;
        if (tipo == CellValues.SharedString)
        {
            return texto is null ? null : _cadenas[int.Parse(texto, CultureInfo.InvariantCulture)];
        }

        if (tipo == CellValues.InlineString)
        {
            return celda.InlineString?.InnerText;
        }

        if (tipo == CellValues.Boolean)
        {
            return texto == "1" ? "TRUE" : "FALSE";
        }

        // Números (incluidas fechas como serial de Excel): G15 evita artefactos de coma flotante (0.17999999 → 0.18).
        if ((tipo is null || tipo == CellValues.Number) && texto is not null
            && double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var numero))
        {
            return numero.ToString("G15", CultureInfo.InvariantCulture);
        }

        return texto;
    }

    private static string TextoDe(SharedStringItem item) =>
        item.Text?.Text ?? string.Concat(item.Elements<Run>().Select(r => r.Text?.Text));

    private static Dictionary<string, string> FilaDe(SortedDictionary<int, Dictionary<string, string>> filas, int numero)
    {
        if (!filas.TryGetValue(numero, out var celdas))
        {
            celdas = new Dictionary<string, string>(StringComparer.Ordinal);
            filas[numero] = celdas;
        }

        return celdas;
    }

    private static (string Columna, int Fila) Referencia(string referencia)
    {
        var coincidencia = ReferenciaCelda().Match(referencia);
        if (!coincidencia.Success)
        {
            throw new InvalidDataException($"Referencia de celda no válida: '{referencia}'.");
        }

        return (coincidencia.Groups[1].Value, int.Parse(coincidencia.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    private static int IndiceColumna(string letras) =>
        letras.Aggregate(0, (acumulado, letra) => (acumulado * 26) + (letra - 'A' + 1));

    private static string LetraColumna(int indice)
    {
        var letras = string.Empty;
        while (indice > 0)
        {
            var resto = (indice - 1) % 26;
            letras = (char)('A' + resto) + letras;
            indice = (indice - 1) / 26;
        }

        return letras;
    }

    [GeneratedRegex(@"^([A-Z]+)(\d+)$")]
    private static partial Regex ReferenciaCelda();
}

/// <summary>Hoja leída: filas ordenadas con sus celdas no vacías por letra de columna.</summary>
internal sealed record HojaExcel(string Nombre, IReadOnlyList<FilaExcel> Filas);

/// <summary>Fila de una hoja con sus celdas no vacías.</summary>
internal sealed record FilaExcel(int Numero, IReadOnlyDictionary<string, string> Celdas)
{
    public string? this[string columna] => Celdas.TryGetValue(columna, out var valor) ? valor : null;
}
