using System.Globalization;

namespace PasheCPE.Empaquetado;

/// <summary>
/// Nombres oficiales de archivo (TRD §11): RUC-TT-SERIE-NUMERO (número sin ceros a la izquierda) para comprobantes y
/// guías, RUC-RC-yyyyMMdd-n y RUC-RA-yyyyMMdd-n para resumen y baja; el CDR es R-{nombre}.zip.
/// </summary>
internal static class NombreArchivoCpe
{
    public static string Base(ReferenciaCpe referencia)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        if (referencia.TipoDocumento is ReferenciaCpe.TipoResumenDiario or ReferenciaCpe.TipoComunicacionBaja)
        {
            var correlativo = referencia.Correlativo
                ?? throw new InvalidOperationException($"La referencia {referencia.TipoDocumento} no tiene correlativo.");
            return string.Create(CultureInfo.InvariantCulture, $"{referencia.Ruc}-{referencia.TipoDocumento}-{referencia.Fecha:yyyyMMdd}-{correlativo}");
        }

        var numero = referencia.Numero
            ?? throw new InvalidOperationException($"La referencia {referencia.TipoDocumento} no tiene número.");
        return string.Create(CultureInfo.InvariantCulture, $"{referencia.Ruc}-{referencia.TipoDocumento}-{referencia.Serie}-{numero}");
    }

    public static string Xml(ReferenciaCpe referencia) => Base(referencia) + ".xml";

    public static string Zip(ReferenciaCpe referencia) => Base(referencia) + ".zip";

    public static string Cdr(ReferenciaCpe referencia) => "R-" + Base(referencia) + ".zip";

    public static string Pdf(ReferenciaCpe referencia) => Base(referencia) + ".pdf";

    public static string Qr(ReferenciaCpe referencia) => Base(referencia) + ".png";

    public static string Historial(ReferenciaCpe referencia) => Base(referencia) + ".json";
}
