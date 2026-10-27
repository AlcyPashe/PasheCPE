using System.Globalization;
using Microsoft.Extensions.Options;
using PasheCPE.Catalogos;
using PasheCPE.Ubl;

namespace PasheCPE.Mapeo;

/// <summary>Una comunicación de baja (RA) lista para mapear: su referencia (fecha de generación y correlativo) y sus documentos.</summary>
internal sealed record LoteBaja(EmisorCpe Emisor, ReferenciaCpe Referencia, DateOnly FechaReferencia, IReadOnlyList<DocumentoBajaCpe> Documentos);

/// <summary>
/// Comunicación de baja → VoidedDocuments UBL 2.0 del perfil SUNAT 1.0: emisor, firma y una línea por documento con su
/// tipo, serie, número (sin ceros a la izquierda) y motivo. Deja <c>ext:UBLExtensions</c> vacío, preparado para la firma.
/// </summary>
internal sealed class MapeadorVoided(IOptions<OpcionesPasheCpe> opciones)
{
    public VoidedDocuments Mapear(LoteBaja lote)
    {
        ArgumentNullException.ThrowIfNull(lote);
        var emisor = lote.Emisor;
        var baja = new VoidedDocuments
        {
            UBLExtensions = new UBLExtensions { UBLExtension = { new UBLExtension() } },
            UBLVersionID = "2.0",
            CustomizationID = new IdentifierType { Value = "1.0" },
            ID = new IdentifierType { Value = MapeadorSummary.Identificador(lote.Referencia) },
            ReferenceDate = Fecha(lote.FechaReferencia),
            IssueDate = Fecha(lote.Referencia.Fecha),
            AccountingSupplierParty = MapeadorComun.Cuenta(TipoDocumentoIdentidad.Ruc, emisor.Ruc, emisor.RazonSocial),
        };
        baja.Signature.Add(MapeadorComun.Firma(emisor, opciones.Value.IdFirma));

        for (var i = 0; i < lote.Documentos.Count; i++)
        {
            var documento = lote.Documentos[i];
            baja.VoidedDocumentsLine.Add(new VoidedDocumentsLine
            {
                LineID = (i + 1).ToString(CultureInfo.InvariantCulture),
                DocumentTypeCode = documento.TipoDocumento,
                DocumentSerialID = documento.Serie,
                DocumentNumberID = documento.Numero.ToString(CultureInfo.InvariantCulture),
                VoidReasonDescription = documento.Motivo.Trim(),
            });
        }

        return baja;
    }

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
