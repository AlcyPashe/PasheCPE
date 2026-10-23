using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PasheCPE.Firma;
using PasheCPE.Mapeo;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;

namespace PasheCPE.Tests.Firma;

public sealed partial class FirmadorXmlTests : IDisposable
{
    private static readonly XNamespace _ds = SignedXml.XmlDsigNamespaceUrl;
    private static readonly XNamespace _ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    [Fact]
    public async Task Firmar_Factura_FirmaVerificableDentroDeExtensionContent()
    {
        var (xml, certificado) = await PrepararAsync(_servicios);

        var firmado = Firmar(_servicios, xml, certificado);

        Assert.True(FirmadorXml.Verificar(firmado.Xml!, certificado));
        var firma = Assert.Single(Leer(firmado.Xml!).Descendants(_ds + "Signature"));
        Assert.Equal(_ext + "ExtensionContent", firma.Parent!.Name);
        Assert.Equal("PasheSign", (string?)firma.Attribute("Id"));
        Assert.Null(firma.GetPrefixOfNamespace(_ds));
        Assert.Equal(Convert.ToBase64String(certificado.RawData), firma.Descendants(_ds + "X509Certificate").Single().Value);
    }

    [Fact]
    public async Task Firmar_IdFirmaConfigurado_CoherenteConCacSignature()
    {
        using var servicios = new ServiciosPrueba(o => o.IdFirma = "FirmaBottega");
        var (xml, certificado) = await PrepararAsync(servicios);

        var raiz = Leer(Firmar(servicios, xml, certificado).Xml!);

        Assert.Equal("FirmaBottega", (string?)raiz.Descendants(_ds + "Signature").Single().Attribute("Id"));
        var cacSignature = raiz.Element(XmlDePrueba.Cac + "Signature")!;
        Assert.Equal("FirmaBottega", cacSignature.Element(XmlDePrueba.Cbc + "ID")!.Value);
        Assert.Equal("#FirmaBottega", cacSignature.Descendants(XmlDePrueba.Cbc + "URI").Single().Value);
    }

    [Theory]
    [InlineData(PerfilFirma.Sha256, SignedXml.XmlDsigRSASHA256Url, SignedXml.XmlDsigSHA256Url)]
    [InlineData(PerfilFirma.Sha1, SignedXml.XmlDsigRSASHA1Url, SignedXml.XmlDsigSHA1Url)]
    public async Task Firmar_Perfil_UsaSusAlgoritmos(PerfilFirma perfil, string metodoFirma, string metodoDigest)
    {
        using var servicios = new ServiciosPrueba(o => o.PerfilFirma = perfil);
        var (xml, certificado) = await PrepararAsync(servicios);

        var firmado = Firmar(servicios, xml, certificado);

        var info = Leer(firmado.Xml!).Descendants(_ds + "SignedInfo").Single();
        Assert.Equal(SignedXml.XmlDsigC14NTransformUrl, (string?)info.Element(_ds + "CanonicalizationMethod")!.Attribute("Algorithm"));
        Assert.Equal(metodoFirma, (string?)info.Element(_ds + "SignatureMethod")!.Attribute("Algorithm"));
        var referencia = info.Element(_ds + "Reference")!;
        Assert.Equal(string.Empty, (string?)referencia.Attribute("URI"));
        Assert.Equal(SignedXml.XmlDsigEnvelopedSignatureTransformUrl, (string?)referencia.Descendants(_ds + "Transform").Single().Attribute("Algorithm"));
        Assert.Equal(metodoDigest, (string?)referencia.Element(_ds + "DigestMethod")!.Attribute("Algorithm"));
        Assert.True(FirmadorXml.Verificar(firmado.Xml!, certificado));
    }

    [Fact]
    public async Task Firmar_ValorResumen_EsElDigestValue()
    {
        var (xml, certificado) = await PrepararAsync(_servicios);

        var firmado = Firmar(_servicios, xml, certificado);

        Assert.Equal(Leer(firmado.Xml!).Descendants(_ds + "DigestValue").Single().Value, firmado.ValorResumen);
        Assert.Equal(32, Convert.FromBase64String(firmado.ValorResumen!).Length);
    }

    [Fact]
    public async Task Firmar_MismoXml_DigestYBytesEstables()
    {
        var (xml, certificado) = await PrepararAsync(_servicios);

        var primero = Firmar(_servicios, xml, certificado);
        var segundo = Firmar(_servicios, xml, certificado);

        Assert.Equal(primero.ValorResumen, segundo.ValorResumen);
        Assert.Equal(primero.Xml, segundo.Xml);
    }

    [Fact]
    public async Task Verificar_AlteracionPosterior_Detectada()
    {
        var (xml, certificado) = await PrepararAsync(_servicios);
        var firmado = Encoding.UTF8.GetString(Firmar(_servicios, xml, certificado).Xml!);
        Assert.Contains("ARROZ X 5 KG", firmado, StringComparison.Ordinal);

        var alterado = Encoding.UTF8.GetBytes(firmado.Replace("ARROZ X 5 KG", "ARROZ X 9 KG", StringComparison.Ordinal));

        Assert.False(FirmadorXml.Verificar(alterado, certificado));
    }

    [Fact]
    public async Task Verificar_OtroCertificado_Falla()
    {
        var (xml, certificado) = await PrepararAsync(_servicios);
        var firmado = Firmar(_servicios, xml, certificado);
        var otro = _servicios.Obtener<ProveedorCertificados>().Obtener(CertificadoPrueba.Crear(_servicios.Almacen, "otro.pfx")).Certificado!;

        Assert.False(FirmadorXml.Verificar(firmado.Xml!, otro));
    }

    [Fact]
    public async Task Firmar_SinBomYSinReindentar_ConservaLosBytesFueraDeLaFirma()
    {
        var (xml, certificado) = await PrepararAsync(_servicios);

        var firmado = Firmar(_servicios, xml, certificado).Xml!;

        Assert.Equal((byte)'<', firmado[0]);
        var original = ExtensionVacia().Match(Encoding.UTF8.GetString(xml));
        var conFirma = ExtensionConFirma().Match(Encoding.UTF8.GetString(firmado));
        Assert.True(original.Success && conFirma.Success);
        Assert.Equal(Encoding.UTF8.GetString(xml)[..original.Index], Encoding.UTF8.GetString(firmado)[..conFirma.Index]);
        Assert.Equal(Encoding.UTF8.GetString(xml)[(original.Index + original.Length)..], Encoding.UTF8.GetString(firmado)[(conFirma.Index + conFirma.Length)..]);
    }

    [Fact]
    public async Task Firmar_XmlFirmado_PasaXsdYValidacionSunat()
    {
        var (xml, certificado) = await PrepararAsync(_servicios);

        var firmado = Firmar(_servicios, xml, certificado).Xml!;

        Assert.Empty(_servicios.Obtener<ValidadorEsquema>().Validar(firmado));
        Assert.Empty(_servicios.Obtener<ValidadorSunat>().Validar(firmado, Ejemplos.Referencia()).Errores);
    }

    [Fact]
    public async Task Firmar_XmlYaFirmado_DevuelveError3006()
    {
        var (xml, certificado) = await PrepararAsync(_servicios);
        var firmado = Firmar(_servicios, xml, certificado).Xml!;

        var otraVez = Firmar(_servicios, firmado, certificado);

        Assert.Equal(("PCPE-3006", OrigenError.Firma), (otraVez.Error!.Codigo, otraVez.Error.Origen));
        Assert.Null(otraVez.Xml);
    }

    [Fact]
    public void Firmar_XmlMalFormado_DevuelveError3006SinLanzar()
    {
        var certificado = _servicios.Obtener<ProveedorCertificados>().Obtener(CertificadoPrueba.Crear(_servicios.Almacen)).Certificado!;

        var firmado = Firmar(_servicios, Encoding.UTF8.GetBytes("<Invoice>"), certificado);

        Assert.Equal("PCPE-3006", firmado.Error!.Codigo);
    }

    internal static async Task<(byte[] Xml, X509Certificate2 Certificado)> PrepararAsync(ServiciosPrueba servicios)
    {
        var generado = await servicios.Obtener<GeneradorInvoice>().GenerarAsync(Ejemplos.Emisor(), Ejemplos.Factura(), TestContext.Current.CancellationToken);
        Assert.True(generado.Exito);
        var certificado = servicios.Obtener<ProveedorCertificados>().Obtener(CertificadoPrueba.Crear(servicios.Almacen));
        return (generado.Xml!, certificado.Certificado!);
    }

    private static XmlFirmado Firmar(ServiciosPrueba servicios, byte[] xml, X509Certificate2 certificado) =>
        servicios.Obtener<FirmadorXml>().Firmar(xml, certificado, "certificado.pfx");

    private static XElement Leer(byte[] xml)
    {
        using var memoria = new MemoryStream(xml);
        return XDocument.Load(memoria, LoadOptions.PreserveWhitespace).Root!;
    }

    [GeneratedRegex(@"<ext:ExtensionContent\s*/>")]
    private static partial Regex ExtensionVacia();

    [GeneratedRegex(@"<ext:ExtensionContent><Signature [^>]*>.*?</Signature></ext:ExtensionContent>", RegexOptions.Singleline)]
    private static partial Regex ExtensionConFirma();
}
