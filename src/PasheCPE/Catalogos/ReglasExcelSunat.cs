using System.Collections.Frozen;

namespace PasheCPE.Catalogos;

/// <summary>
/// Filas de las hojas de reglas del Excel de SUNAT (<c>reglas-cpe.json</c>, generado por tools/GeneradorRecursos): código,
/// tipo (error u observación) y vigencia de cada validación. Las reglas complementarias toman de aquí su código y su tipo.
/// </summary>
internal sealed class ReglasExcelSunat
{
    private readonly Lazy<FrozenDictionary<(string Documento, int Fila), ReglaExcelSunat>> _reglas = new(() =>
        RecursosEmbebidos.Leer("reglas-cpe.json", DatosJson.Default.ArchivoReglasCpe).Reglas
            .ToFrozenDictionary(r => (r.Documento, r.Fila)));

    public IEnumerable<ReglaExcelSunat> Todas => _reglas.Value.Values;

    public ReglaExcelSunat Obtener(string documento, int fila) =>
        _reglas.Value.GetValueOrDefault((documento, fila))
        ?? throw new ArgumentException($"No existe la fila {fila} del documento {documento} en reglas-cpe.json.", nameof(fila));

    /// <summary>
    /// Tipo que aplica a la fecha: «error» u «observacion». Antes de la vigencia se aplica el tipo anterior (filas
    /// «OBSERV ERROR»); si no tiene, la regla aún no aplica (null).
    /// </summary>
    public static string? TipoVigente(ReglaExcelSunat regla, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(regla);
        return regla.Vigencia is { } vigencia && fecha < vigencia ? regla.TipoAnterior : regla.Tipo;
    }
}
