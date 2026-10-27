using System.Xml.Linq;
using PasheCPE.Mapeo;
using PasheCPE.Serializacion;
using PasheCPE.Tests.Dorados;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;

namespace PasheCPE.Tests.Gre;

/// <summary>Mapeo de la GRE remitente: mismos datos que la GRE dorada → misma estructura y valores; XSD y XSL GRE 2.0.</summary>
public sealed class MapeoGreTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    /// <summary>
    /// Sin la firma ni el nombre del concepto 7022: la GRE dorada (generada con otro sistema) lo llama «Indicador de bien
    /// regulado por SUNAT»; PasheCPE usa el nombre del catálogo N.° 55 («Indicador de bien normalizado»).
    /// </summary>
    [Fact]
    public void Mapear_DatosDeLaGreDorada_MismaEstructuraYValores()
    {
        var dorado = XDocument.Load(Path.Combine(LectorDorado.Carpeta("09-GRE"), "20611619767-09-T001-31093.xml")).Root!;

        var generado = XDocument.Parse(System.Text.Encoding.UTF8.GetString(Xml(CasosGre.Dorada()))).Root!;

        var diferencias = ComparadorXml.Comparar(dorado, generado, new HashSet<string>(StringComparer.Ordinal) { "ext:UBLExtensions", "cac:Signature", "cbc:Name" });
        Assert.True(diferencias.Count == 0, string.Join(Environment.NewLine, diferencias));
    }

    [Theory]
    [InlineData("Publica")]
    [InlineData("Privada")]
    public void Mapear_PasaElXsdYLaXslGre_SoloLaObservacionDelRegistroMtc(string caso)
    {
        var guia = caso == "Publica" ? CasosGre.Dorada() : CasosGre.Privada();
        var xml = Xml(guia);

        var resultado = _servicios.Obtener<ValidadorSunat>().Validar(xml, Referencia(guia));

        Assert.Empty(_servicios.Obtener<ValidadorEsquema>().Validar(xml));
        Assert.Empty(resultado.Errores);
        Assert.Equal(caso == "Publica" ? ["4391"] : [], resultado.Observaciones.Select(o => o.Codigo));
    }

    private byte[] Xml(GuiaRemisionCpe guia) =>
        SerializadorUbl.Serializar(_servicios.Obtener<MapeadorDespatchAdvice>().Mapear(Emisor(), guia, Referencia(guia), guia.HoraEmision!.Value));

    private static EmisorCpe Emisor() => CasosGre.EmisorDorado(Ejemplos.Emisor());

    private static ReferenciaCpe Referencia(GuiaRemisionCpe guia) =>
        ReferenciaCpe.Guia(CasosGre.RucDorado, TipoDocumentoSunat.GuiaDeRemisionRemitente, guia.Serie, guia.Numero, guia.FechaEmision!.Value);
}
