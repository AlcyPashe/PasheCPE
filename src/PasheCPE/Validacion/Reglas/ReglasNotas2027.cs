namespace PasheCPE.Validacion.Reglas;

/// <summary>
/// Una nota modifica un único comprobante (R.S. 000048-2026, desde el 01/01/2027): más de un
/// <c>cac:InvoiceDocumentReference</c> es 3261 en la nota de crédito (salvo el motivo 11, que la XSL ya valida con 3194)
/// y 3194 en la de débito. La XSL de la nota de crédito solo lo valida en el motivo 13: su 3261 se suprime desde la
/// vigencia de la fila.
/// </summary>
internal sealed class ReglaReferenciaUnica : IReglaCpe
{
    public string Nombre => "ReferenciaUnica";

    public IReadOnlyList<FilaExcel> Filas { get; } = [new("07", 90), new("08", 90)];

    public IEnumerable<HallazgoRegla> Evaluar(DocumentoXml documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var referencias = documento.Nodos(DocumentoXml.ReferenciaModificado).ToList();
        if (referencias.Count <= 1
            || (documento.TipoDocumento == TipoDocumentoSunat.NotaDeCredito && documento.Motivo == MotivoNotaCredito.AjustesDeOperacionesDeExportacion))
        {
            yield break;
        }

        yield return new HallazgoRegla(90, DocumentoXml.Ruta(referencias[1]), $"{referencias.Count} documentos modificados.");
    }
}

/// <summary>
/// La nota de débito indica el documento que modifica salvo en las penalidades (2524, motivo 13, desde el 01/01/2027).
/// La XSL lo exige salvo en el motivo 03: su 2524 se suprime desde la vigencia de la fila.
/// </summary>
internal sealed class ReglaReferenciaNotaDebito : IReglaCpe
{
    public string Nombre => "ReferenciaNotaDebito";

    public IReadOnlyList<FilaExcel> Filas { get; } = [new("08", 89)];

    public IEnumerable<HallazgoRegla> Evaluar(DocumentoXml documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        if (documento.TipoDocumento == TipoDocumentoSunat.NotaDeDebito
            && documento.Motivo != MotivoNotaDebito.Penalidades
            && !documento.Existe(DocumentoXml.ReferenciaModificado))
        {
            yield return new HallazgoRegla(89, null, $"Motivo {documento.Motivo} sin documento modificado.");
        }
    }
}

/// <summary>
/// Las penalidades (nota de débito motivo 13, desde el 01/01/2027) son inafectas del IGV (3507): IGV o IVAP de una línea
/// mayor que cero (fila 194), monto de los tributos 9995 o 9997 mayor que cero (fila 283) o sumatoria del IGV o del IVAP
/// mayor que cero (fila 325).
/// </summary>
internal sealed class ReglaPenalidadesSinIgv : IReglaCpe
{
    private const string _igvIvap = "cac:TaxCategory/cac:TaxScheme/cbc:ID='1000' or cac:TaxCategory/cac:TaxScheme/cbc:ID='1016'";

    public string Nombre => "PenalidadesSinIgv";

    public IReadOnlyList<FilaExcel> Filas { get; } = [new("08", 194), new("08", 283), new("08", 325)];

    public IEnumerable<HallazgoRegla> Evaluar(DocumentoXml documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        if (documento.TipoDocumento != TipoDocumentoSunat.NotaDeDebito || documento.Motivo != MotivoNotaDebito.Penalidades)
        {
            yield break;
        }

        foreach (var monto in documento.Nodos(DocumentoXml.Lineas + $"/cac:TaxTotal/cac:TaxSubtotal[{_igvIvap}]/cbc:TaxAmount").Where(n => documento.Decimal(".", n) > 0))
        {
            yield return new HallazgoRegla(194, DocumentoXml.Ruta(monto), $"IGV o IVAP de la línea {monto.Value}.");
        }

        var cabecera = "/*/cac:TaxTotal/cac:TaxSubtotal";
        foreach (var monto in documento.Nodos(cabecera + "[cac:TaxCategory/cac:TaxScheme/cbc:ID='9995' or cac:TaxCategory/cac:TaxScheme/cbc:ID='9997']/cbc:TaxAmount").Where(n => documento.Decimal(".", n) > 0))
        {
            yield return new HallazgoRegla(283, DocumentoXml.Ruta(monto), $"Monto del tributo {monto.Value}.");
        }

        foreach (var monto in documento.Nodos(cabecera + $"[{_igvIvap}]/cbc:TaxAmount").Where(n => documento.Decimal(".", n) > 0))
        {
            yield return new HallazgoRegla(325, DocumentoXml.Ruta(monto), $"Sumatoria del IGV o del IVAP {monto.Value}.");
        }
    }
}
