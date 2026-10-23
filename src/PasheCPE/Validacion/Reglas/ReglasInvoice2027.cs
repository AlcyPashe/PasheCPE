using System.Text.RegularExpressions;

namespace PasheCPE.Validacion.Reglas;

/// <summary>
/// Código de producto SUNAT de 8 dígitos (3496, fila «Si el tag existe, el formato…»): observación hasta el 31/12/2026
/// y error desde el 01/01/2027. La otra fila de 3496 (código fuera del listado) requiere el listado de SUNAT.
/// </summary>
internal sealed partial class ReglaCodigoProducto : IReglaCpe
{
    public string Nombre => "CodigoProducto";

    public IReadOnlyList<FilaExcel> Filas { get; } = [new("01", 155), new("03", 155)];

    public IEnumerable<HallazgoRegla> Evaluar(DocumentoXml documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        foreach (var codigo in documento.Nodos("/*/cac:InvoiceLine/cac:Item/cac:CommodityClassification/cbc:ItemClassificationCode"))
        {
            if (!OchoDigitos().IsMatch(codigo.Value))
            {
                yield return new HallazgoRegla(155, DocumentoXml.Ruta(codigo), $"Código de producto '{codigo.Value}'.");
            }
        }
    }

    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex OchoDigitos();
}

/// <summary>
/// Contrato de colaboración empresarial (R.S. 000048-2026, desde el 01/01/2027): 3497 a 3502. El Excel repite las
/// filas para el partícipe y para el adquirente; el primer <c>cac:ContractDocumentReference</c> usa el primer bloque y
/// los siguientes, el segundo (mismos códigos).
/// </summary>
internal sealed partial class ReglaContratoColaboracion : IReglaCpe
{
    // Filas por documento y bloque: tipo (3497), un solo número (3498), formato del número (3501), datos completos
    // (3499), descripción (3502) y porcentaje (3500).
    private static readonly Dictionary<string, Bloque[]> _bloques = new(StringComparer.Ordinal)
    {
        ["01"] = [new(916, 917, 918, 919, 920, 921), new(923, 924, 925, 926, 927, 928)],
        ["03"] = [new(822, 823, 824, 825, 826, 827), new(829, 830, 831, 832, 833, 834)],
    };

    public string Nombre => "ContratoColaboracion";

    public IReadOnlyList<FilaExcel> Filas { get; } =
        [.. _bloques.SelectMany(p => p.Value.SelectMany(b => b.Todas).Select(f => new FilaExcel(p.Key, f)))];

    public IEnumerable<HallazgoRegla> Evaluar(DocumentoXml documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        if (!_bloques.TryGetValue(documento.TipoDocumento, out var bloques))
        {
            yield break;
        }

        var indice = 0;
        foreach (var contrato in documento.Nodos("/*/cac:ContractDocumentReference"))
        {
            var bloque = bloques[Math.Min(indice++, bloques.Length - 1)];
            var tipo = documento.Nodo("cbc:DocumentTypeCode", contrato);
            var numeros = documento.Nodos("cbc:ID", contrato).ToList();
            var descripcion = documento.Nodo("cbc:DocumentDescription", contrato);
            var porcentaje = documento.Nodo("cac:IssuerParty/cac:PartyLegalEntity/cac:ShareholderParty/cbc:PartecipationPercent", contrato);

            if (tipo is not null && tipo.Value is not ("1" or "2"))
            {
                yield return new HallazgoRegla(bloque.Tipo, DocumentoXml.Ruta(tipo), $"Tipo de contrato '{tipo.Value}'.");
            }

            if (numeros.Count > 1)
            {
                yield return new HallazgoRegla(bloque.UnNumero, DocumentoXml.Ruta(numeros[1]), $"{numeros.Count} números de contrato.");
            }

            if (numeros.Count > 0)
            {
                if (!TextoValido(numeros[0].Value, 50))
                {
                    yield return new HallazgoRegla(bloque.FormatoNumero, DocumentoXml.Ruta(numeros[0]), "Número de contrato de 1 a 50 caracteres.");
                }

                if (tipo is null || descripcion is null || porcentaje is null)
                {
                    yield return new HallazgoRegla(bloque.Completo, DocumentoXml.Ruta(numeros[0]), "Faltan el tipo, la descripción o el porcentaje de participación.");
                }
            }

            if (descripcion is not null && !TextoValido(descripcion.Value, 250))
            {
                yield return new HallazgoRegla(bloque.Descripcion, DocumentoXml.Ruta(descripcion), "Descripción del contrato de 1 a 250 caracteres.");
            }

            if (porcentaje is not null && !Porcentaje().IsMatch(porcentaje.Value))
            {
                yield return new HallazgoRegla(bloque.Porcentaje, DocumentoXml.Ruta(porcentaje), $"Porcentaje de participación '{porcentaje.Value}'.");
            }
        }
    }

    /// <summary>
    /// De 1 al máximo de caracteres; se admite el espacio pero ningún otro carácter de espaciado (salto de línea,
    /// tabulación…), y no puede tener solo espacios.
    /// </summary>
    private static bool TextoValido(string texto, int maximo) =>
        texto.Length >= 1 && texto.Length <= maximo && !texto.Any(c => char.IsWhiteSpace(c) && c != ' ') && !string.IsNullOrWhiteSpace(texto);

    /// <summary>Decimal positivo de hasta 2 enteros y 2 decimales.</summary>
    [GeneratedRegex(@"^(?!0+(\.0+)?$)\d{1,2}(\.\d{1,2})?$")]
    private static partial Regex Porcentaje();

    private sealed record Bloque(int Tipo, int UnNumero, int FormatoNumero, int Completo, int Descripcion, int Porcentaje)
    {
        public IEnumerable<int> Todas => [Tipo, UnNumero, FormatoNumero, Completo, Descripcion, Porcentaje];
    }
}
