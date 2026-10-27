using System.Xml.Linq;
using PasheCPE.Mapeo;
using PasheCPE.Serializacion;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;
using static PasheCPE.Tests.Utilidades.XmlDePrueba;

namespace PasheCPE.Tests.Baja;

/// <summary>
/// La RA mapeada pasa el XSD UBL 2.0 y la XSL de SUNAT sin hallazgos; cada alteración produce el código del Excel (hoja
/// «Comunicación de Baja1_0») por la XSL o por el XSD.
/// </summary>
public sealed class ValidacionSunatBajaTests : IDisposable
{
    private static readonly XNamespace _sac = GeneracionBajaTests.Sac;

    private static readonly Dictionary<string, (string Codigo, bool EsError, Action<XElement> Alteracion)> _casos = new(StringComparer.Ordinal)
    {
        ["VersionUbl"] = ("2074", true, r => r.Element(Cbc + "UBLVersionID")!.Value = "2.1"),
        ["VersionEstructura"] = ("2072", true, r => r.Element(Cbc + "CustomizationID")!.Value = "1.1"),
        ["IdDistintoDelArchivo"] = ("2220", true, r => r.Element(Cbc + "ID")!.Value = "RA-20261022-9"),
        ["FechaDistintaDelArchivo"] = ("2346", true, r => r.Element(Cbc + "IssueDate")!.Value = "2026-10-21"),
        ["GeneracionFutura"] = ("2301", true, r => r.Element(Cbc + "IssueDate")!.Value = "2026-10-23"),
        ["EmisionPosteriorALaGeneracion"] = ("2671", true, r => r.Element(Cbc + "ReferenceDate")!.Value = "2026-10-23"),
        ["TipoDocumentoEmisor"] = ("2287", true, r => r.Elemento(Cac + "AccountingSupplierParty", Cbc + "AdditionalAccountID").Value = "1"),
        ["RazonSocialCorta"] = ("2228", true, r => r.Elemento(Cac + "AccountingSupplierParty", Cac + "Party", Cac + "PartyLegalEntity", Cbc + "RegistrationName").Value = "AB"),
        ["LineaRepetida"] = ("2752", true, r => r.Elements(_sac + "VoidedDocumentsLine").Last().Element(Cbc + "LineID")!.Value = "1"),
        ["TipoBoleta"] = ("2308", true, r => r.Element(_sac + "VoidedDocumentsLine")!.Element(Cbc + "DocumentTypeCode")!.Value = "03"),
        ["SerieDeBoleta"] = ("2310", true, r => r.Element(_sac + "VoidedDocumentsLine")!.Element(_sac + "DocumentSerialID")!.Value = "B001"),
        ["NumeroDeNueveDigitos"] = ("2312", true, r => r.Element(_sac + "VoidedDocumentsLine")!.Element(_sac + "DocumentNumberID")!.Value = "123456789"),
        ["MotivoCorto"] = ("4203", false, r => r.Element(_sac + "VoidedDocumentsLine")!.Element(_sac + "VoidReasonDescription")!.Value = "NO"),
    };

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    public static TheoryData<string> Casos => [.. _casos.Keys.Order(StringComparer.Ordinal)];

    [Fact]
    public void Validar_RaMapeada_SinHallazgosDelXsdNiDeLaXsl()
    {
        var (xml, referencia) = Generar();

        var resultado = _servicios.Obtener<ValidadorSunat>().Validar(xml, referencia);

        Assert.Empty(_servicios.Obtener<ValidadorEsquema>().Validar(xml));
        Assert.Empty(resultado.Errores);
        Assert.Empty(resultado.Observaciones);
    }

    [Fact]
    public void Serializar_IdaYVuelta_SinCambios()
    {
        var (xml, _) = Generar();

        Assert.Equal(xml, SerializadorUbl.Serializar(SerializadorUbl.Deserializar<global::PasheCPE.Ubl.VoidedDocuments>(xml)));
    }

    [Fact]
    public void Validar_SinFechaDeReferencia_ErrorDeEsquema()
    {
        var (xml, _) = Generar();

        var errores = _servicios.Obtener<ValidadorEsquema>().Validar(Alterar(xml, r => r.Element(Cbc + "ReferenceDate")!.Remove()));

        Assert.Equal("PCPE-2001", Assert.Single(errores).Codigo);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void Validar_XmlAlterado_DevuelveElCodigoDeSunat(string nombre)
    {
        var (codigo, esError, alteracion) = _casos[nombre];
        var (xml, referencia) = Generar();

        var resultado = _servicios.Obtener<ValidadorSunat>().Validar(Alterar(xml, alteracion), referencia);

        var codigos = esError ? resultado.Errores.Select(e => e.Codigo) : resultado.Observaciones.Select(o => o.Codigo);
        Assert.True(
            codigos.Contains(codigo),
            $"Se esperaba el {(esError ? "error" : "la observación")} {codigo}. Errores: {string.Join(", ", resultado.Errores.Select(e => e.Codigo))}. Observaciones: {string.Join(", ", resultado.Observaciones.Select(o => o.Codigo))}.");
    }

    /// <summary>RA de dos documentos (factura y nota de crédito) del 22/10/2026, sin firmar.</summary>
    private (byte[] Xml, ReferenciaCpe Referencia) Generar()
    {
        var referencia = ReferenciaCpe.Baja(Ejemplos.Ruc, GeneracionBajaTests.Fecha, 1);
        var lote = new LoteBaja(
            Ejemplos.Emisor(),
            referencia,
            GeneracionBajaTests.Fecha,
            [GeneracionBajaTests.Documento(123), GeneracionBajaTests.Documento(5, TipoDocumentoSunat.NotaDeCredito, "FC01")]);
        return (SerializadorUbl.Serializar(_servicios.Obtener<MapeadorVoided>().Mapear(lote)), referencia);
    }
}
