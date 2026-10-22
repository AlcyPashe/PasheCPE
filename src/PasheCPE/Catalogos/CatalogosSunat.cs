using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PasheCPE.Catalogos;

/// <summary>
/// Catálogos del Anexo N.° 8 (<c>catalogos.json</c>, generado del Excel de SUNAT). Un código es válido si existe
/// y está vigente a la fecha de emisión; los catálogos que SUNAT solo publica como enlace se validan por formato.
/// </summary>
internal sealed class CatalogosSunat(CatalogoErrores errores)
{
    private readonly Lazy<FrozenDictionary<string, CatalogoCargado>> _catalogos = new(Cargar);

    public bool Existe(string catalogo, string codigo) => Obtener(catalogo).Items.ContainsKey(codigo);

    public string Nombre(string catalogo) => Obtener(catalogo).Datos.Nombre;

    public string? Descripcion(string catalogo, string codigo) =>
        Obtener(catalogo).Items.TryGetValue(codigo, out var items) ? items[0].Descripcion : null;

    /// <summary>Devuelve null si el código es válido a la fecha; si no, el error con el número de catálogo.</summary>
    public ErrorCpe? Validar(string catalogo, string? codigo, DateOnly fecha, string? ruta = null)
    {
        var cargado = Obtener(catalogo);
        var nombre = cargado.Datos.Nombre;
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return errores.Local("PCPE-1004", OrigenError.Modelo, ruta, catalogo, catalogo, nombre);
        }

        if (cargado.Formato is { } formato)
        {
            return formato.IsMatch(codigo) ? null : errores.Local("PCPE-1003", OrigenError.Modelo, ruta, catalogo, codigo, catalogo, nombre);
        }

        if (!cargado.Items.TryGetValue(codigo, out var items))
        {
            return errores.Local("PCPE-1001", OrigenError.Modelo, ruta, catalogo, codigo, catalogo, nombre);
        }

        var vigente = EsVigente(cargado.Datos.Desde, cargado.Datos.Hasta, fecha) && items.Any(i => EsVigente(i.Desde, i.Hasta, fecha));
        return vigente
            ? null
            : errores.Local("PCPE-1002", OrigenError.Modelo, ruta, catalogo, codigo, catalogo, nombre, fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
    }

    private static bool EsVigente(DateOnly? desde, DateOnly? hasta, DateOnly fecha) =>
        (desde is null || fecha >= desde) && (hasta is null || fecha <= hasta);

    private CatalogoCargado Obtener(string catalogo) =>
        _catalogos.Value.GetValueOrDefault(catalogo)
        ?? throw new ArgumentException($"No existe el catálogo N.° {catalogo} en catalogos.json.", nameof(catalogo));

    private static FrozenDictionary<string, CatalogoCargado> Cargar() =>
        RecursosEmbebidos.Leer("catalogos.json", DatosJson.Default.ArchivoCatalogos).Catalogos
            .ToFrozenDictionary(
                p => p.Key,
                p => new CatalogoCargado(
                    p.Value,
                    p.Value.Formato is null ? null : new Regex(p.Value.Formato, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)),
                    p.Value.Items.GroupBy(i => i.Codigo, StringComparer.Ordinal)
                        .ToFrozenDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal)),
                StringComparer.Ordinal);

    private sealed record CatalogoCargado(DatosCatalogo Datos, Regex? Formato, FrozenDictionary<string, ItemCatalogo[]> Items);
}
