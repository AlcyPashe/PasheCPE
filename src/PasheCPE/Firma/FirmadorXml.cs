using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Options;
using PasheCPE.Catalogos;
using PasheCPE.Diagnostico;
using PasheCPE.Ubl;

namespace PasheCPE.Firma;

/// <summary>XML firmado con su valor resumen, o el error que impidió firmarlo.</summary>
internal sealed record XmlFirmado(byte[]? Xml, string? ValorResumen, ErrorCpe? Error)
{
    public bool Exito => Error is null;
}

/// <summary>
/// Firma XML-DSig enveloped (TRD §10): C14N, RSA-SHA256/SHA-256 o RSA-SHA1/SHA-1 según
/// <see cref="OpcionesPasheCpe.PerfilFirma"/>, <c>Reference URI=""</c> con la transformación enveloped y el certificado
/// en <c>KeyInfo/X509Data</c>. La firma va sin prefijo (namespace por defecto), como en los comprobantes aceptados por
/// SUNAT, dentro del <c>ext:ExtensionContent</c> vacío que deja el mapeador, con <c>Id</c> =
/// <see cref="OpcionesPasheCpe.IdFirma"/>. Se verifica antes de devolverla y se guarda en UTF-8 sin BOM, sin reindentar
/// (el resto del documento conserva sus bytes). Valor resumen = <c>DigestValue</c> en Base64. Nunca lanza.
/// </summary>
internal sealed class FirmadorXml(IOptions<OpcionesPasheCpe> opciones, CatalogoErrores errores)
{
    private static readonly UTF8Encoding _utf8SinBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Firma dentro de la actividad <c>firmar</c>, hija de la de generación (que lleva el nombre del documento).</summary>
    public XmlFirmado Firmar(byte[] xml, X509Certificate2 certificado, string archivoCertificado)
    {
        using var actividad = Telemetria.Actividades.StartActivity(Telemetria.Firmar);
        var firmado = FirmarXml(xml, certificado, archivoCertificado);
        Telemetria.Concluir(actividad, firmado.Exito ? 0 : 1);
        return firmado;
    }

    private XmlFirmado FirmarXml(byte[] xml, X509Certificate2 certificado, string archivoCertificado)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(certificado);
        try
        {
            var documento = Cargar(xml);
            var espacios = new XmlNamespaceManager(documento.NameTable);
            espacios.AddNamespace("ext", EspaciosUbl.Ext);
            if (documento.DocumentElement?.SelectSingleNode("ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent[not(*)]", espacios) is not XmlElement contenido)
            {
                return Fallo("PCPE-3006", "el documento no tiene un ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent vacío para la firma.");
            }

            using var clave = certificado.GetRSAPrivateKey();
            if (clave is null)
            {
                return Fallo("PCPE-3003", archivoCertificado);
            }

            var sha256 = opciones.Value.PerfilFirma == PerfilFirma.Sha256;
            var firma = new SignedXml(documento) { SigningKey = clave };
            firma.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
            firma.SignedInfo.SignatureMethod = sha256 ? SignedXml.XmlDsigRSASHA256Url : SignedXml.XmlDsigRSASHA1Url;
            var referencia = new Reference(string.Empty) { DigestMethod = sha256 ? SignedXml.XmlDsigSHA256Url : SignedXml.XmlDsigSHA1Url };
            referencia.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            firma.AddReference(referencia);
            var datosClave = new KeyInfo();
            datosClave.AddClause(new KeyInfoX509Data(certificado));
            firma.KeyInfo = datosClave;
            firma.Signature.Id = opciones.Value.IdFirma;
            firma.ComputeSignature();

            var elemento = (XmlElement)contenido.AppendChild(documento.ImportNode(firma.GetXml(), deep: true))!;
            if (!Verificar(documento, elemento, certificado))
            {
                return Fallo("PCPE-3007", archivoCertificado);
            }

            return new XmlFirmado(Guardar(documento), Convert.ToBase64String(referencia.DigestValue!), null);
        }
        catch (Exception ex) when (ex is CryptographicException or XmlException)
        {
            return Fallo("PCPE-3006", ex.Message);
        }
    }

    /// <summary>Verifica la firma de un XML firmado (la primera <c>Signature</c> del documento) con el certificado.</summary>
    public static bool Verificar(byte[] xmlFirmado, X509Certificate2 certificado)
    {
        ArgumentNullException.ThrowIfNull(xmlFirmado);
        try
        {
            var documento = Cargar(xmlFirmado);
            return documento.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl) is { Count: > 0 } firmas
                && Verificar(documento, (XmlElement)firmas[0]!, certificado);
        }
        catch (Exception ex) when (ex is CryptographicException or XmlException)
        {
            return false;
        }
    }

    private static bool Verificar(XmlDocument documento, XmlElement firma, X509Certificate2 certificado)
    {
        var verificador = new SignedXml(documento);
        verificador.LoadXml(firma);
        return verificador.CheckSignature(certificado, verifySignatureOnly: true);
    }

    private static XmlDocument Cargar(byte[] xml)
    {
        var documento = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        using var memoria = new MemoryStream(xml, writable: false);
        using var lector = XmlReader.Create(memoria, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        documento.Load(lector);
        return documento;
    }

    /// <summary>UTF-8 sin BOM, sin sangría ni normalización de saltos de línea: nunca se reindenta un XML firmado.</summary>
    private static byte[] Guardar(XmlDocument documento)
    {
        using var memoria = new MemoryStream();
        var ajustes = new XmlWriterSettings { Encoding = _utf8SinBom, Indent = false, NewLineHandling = NewLineHandling.None };
        using (var escritor = XmlWriter.Create(memoria, ajustes))
        {
            documento.Save(escritor);
        }

        return memoria.ToArray();
    }

    private XmlFirmado Fallo(string codigo, string argumento) =>
        new(null, null, errores.Local(codigo, OrigenError.Firma, null, null, argumento));
}
