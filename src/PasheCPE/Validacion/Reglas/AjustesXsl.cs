using System.Collections.Frozen;
using PasheCPE.Catalogos;

namespace PasheCPE.Validacion.Reglas;

/// <summary>
/// Ajustes entre las XSL de SUNAT y el Excel de reglas vigente (<c>ajustes-xsl.json</c>, TRD §9.4): códigos de la XSL
/// que se suprimen porque los reemplaza una regla C#, y la clasificación de las filas del Excel posteriores a la XSL.
/// </summary>
internal sealed class AjustesXsl
{
    private readonly Lazy<ArchivoAjustesXsl> _archivo = new(() => RecursosEmbebidos.Leer("ajustes-xsl.json", DatosJson.Default.ArchivoAjustesXsl));

    private readonly Lazy<FrozenDictionary<(string Documento, string Codigo), DateOnly?>> _suprimidos;

    public AjustesXsl()
    {
        _suprimidos = new(() => _archivo.Value.Suprimidos.ToFrozenDictionary(s => (s.Documento, s.Codigo), s => s.Desde));
    }

    public IReadOnlyList<SupresionXsl> Suprimidos => _archivo.Value.Suprimidos;

    public IReadOnlyList<ClasificacionRegla> Clasificacion => _archivo.Value.Clasificacion;

    /// <summary>
    /// true si el código de la XSL del documento se descarta a la fecha porque lo reemplaza una regla C# (desde
    /// <see cref="SupresionXsl.Desde"/>, si la tiene).
    /// </summary>
    public bool EstaSuprimido(string documento, string codigo, DateOnly fecha) =>
        _suprimidos.Value.TryGetValue((documento, codigo), out var desde) && (desde is null || fecha >= desde);
}

/// <summary>
/// Código de la XSL que se descarta y la regla C# que lo reemplaza, con las filas del Excel que lo respaldan. Con
/// <see cref="Desde"/> (la vigencia de esas filas) la XSL sigue aplicando hasta el día anterior.
/// </summary>
internal sealed record SupresionXsl(string Documento, string Codigo, string Reemplazo, IReadOnlyList<int> FilasExcel, string Motivo, DateOnly? Desde = null);

/// <summary>Cómo se cubre una fila del Excel posterior a la XSL.</summary>
internal sealed record ClasificacionRegla(string Documento, int Fila, string Codigo, TratamientoRegla Tratamiento, string? Regla, string Motivo);

/// <summary>Cobertura de una fila del Excel.</summary>
internal enum TratamientoRegla
{
    /// <summary>La XSL la calcula distinto: se suprime y la reemplaza una regla C#.</summary>
    Reemplazada,

    /// <summary>La XSL no la tiene: la implementa una regla C#.</summary>
    Nueva,

    /// <summary>La XSL ya la cumple.</summary>
    Xsl,

    /// <summary>La controla ValidadorModelo antes de generar el XML.</summary>
    Modelo,

    /// <summary>Requiere padrones o listados de SUNAT que no están disponibles localmente.</summary>
    NoVerificable,
}
