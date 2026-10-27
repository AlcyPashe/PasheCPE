namespace PasheCPE.Pdf;

/// <summary>
/// Opciones de PasheCPE.Pdf (sección <c>PasheCPE:Pdf</c>). Las marcas se usan en la generación automática dentro de
/// <c>EmitirAsync</c> (<see cref="OpcionesPasheCpe.GenerarPdfAutomaticamente"/>), que no recibe la marca.
/// </summary>
public sealed class OpcionesPasheCpePdf
{
    /// <summary>Marca de cada emisor, por RUC.</summary>
    public IDictionary<string, MarcaEmisor> Marcas { get; init; } = new Dictionary<string, MarcaEmisor>(StringComparer.Ordinal);

    /// <summary>Marca de los emisores que no están en <see cref="Marcas"/>; sin ella, el PDF sale sin logo.</summary>
    public MarcaEmisor? MarcaPorDefecto { get; set; }

    /// <summary>Marca del emisor: la de su RUC, la por defecto o <see cref="MarcaEmisor.SinMarca"/>.</summary>
    public MarcaEmisor MarcaDe(string ruc) => Marcas.TryGetValue(ruc, out var marca) ? marca : MarcaPorDefecto ?? MarcaEmisor.SinMarca;
}
