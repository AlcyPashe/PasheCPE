using Microsoft.Extensions.Options;

namespace PasheCPE.Catalogos;

/// <summary>
/// Tasas por fecha de emisión (PRD RF-05): <c>tasas.json</c>, o las listas informadas en
/// <see cref="OpcionesPasheCpe.Tasas"/>, que reemplazan a la embebida del mismo tipo.
/// </summary>
internal sealed class TablaTasas
{
    private readonly TramoIgv[] _igv;
    private readonly TramoTasa[] _mype;
    private readonly TramoIcbper[] _icbper;

    public TablaTasas(IOptions<OpcionesPasheCpe> opciones)
    {
        var embebidas = RecursosEmbebidos.Leer("tasas.json", DatosJson.Default.ArchivoTasas);
        var configuradas = opciones.Value.Tasas;
        _igv = Elegir(configuradas.Igv, embebidas.Igv);
        _mype = Elegir(configuradas.MypeRestaurantesHoteles, embebidas.MypeRestaurantesHoteles);
        _icbper = Elegir(configuradas.Icbper, embebidas.Icbper);
    }

    /// <summary>Tasa del IGV (con su división IGV + IPM) vigente a la fecha.</summary>
    public TramoIgv Igv(DateOnly fecha) =>
        Vigente(_igv, fecha) ?? throw new InvalidOperationException($"No hay tasa de IGV configurada para el {fecha:yyyy-MM-dd}.");

    /// <summary>Tasa especial MYPE de restaurantes y hoteles vigente a la fecha; null si no hay.</summary>
    public decimal? MypeRestaurantesHoteles(DateOnly fecha) => Vigente(_mype, fecha)?.Tasa;

    /// <summary>Monto del ICBPER por bolsa vigente a la fecha; null si no hay.</summary>
    public decimal? Icbper(DateOnly fecha) => Vigente(_icbper, fecha)?.MontoPorUnidad;

    /// <summary>El último tramo que empezó hasta la fecha, si no venció.</summary>
    private static T? Vigente<T>(T[] tramos, DateOnly fecha)
        where T : TramoVigencia
    {
        var tramo = tramos.LastOrDefault(t => t.Desde <= fecha);
        return tramo is not null && (tramo.Hasta is null || fecha <= tramo.Hasta) ? tramo : null;
    }

    /// <summary>La lista configurada si se informó; si no, la embebida. Ordenada por fecha de inicio.</summary>
    private static T[] Elegir<T>(IList<T> configuradas, IReadOnlyList<T> embebidas)
        where T : TramoVigencia => [.. (configuradas.Count > 0 ? (IEnumerable<T>)configuradas : embebidas).OrderBy(t => t.Desde)];
}
