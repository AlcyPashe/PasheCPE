using System.Xml.Linq;
using PasheCPE.Serializacion;
using PasheCPE.Tests.Dorados;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;

namespace PasheCPE.Tests.Gre;

/// <summary>Perfil DespatchAdvice (GRE 2.0): las clases UBL cubren todo lo que traen las GRE aceptadas por SUNAT.</summary>
public sealed class UblGreTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    public static TheoryData<string> Dorados => ["20611619767-09-T001-31093.xml", "20611619767-09-T001-31094.xml"];

    [Theory]
    [MemberData(nameof(Dorados))]
    public void Deserializar_GreDorada_IdaYVueltaSinPerderElementos(string archivo)
    {
        var ruta = Path.Combine(LectorDorado.Carpeta("09-GRE"), archivo);
        var dorado = XDocument.Load(ruta).Root!;

        var guia = SerializadorUbl.Deserializar<global::PasheCPE.Ubl.DespatchAdvice>(File.ReadAllBytes(ruta));
        var xml = SerializadorUbl.Serializar(guia);

        var diferencias = ComparadorXml.Comparar(dorado, XDocument.Parse(System.Text.Encoding.UTF8.GetString(xml)).Root!, new HashSet<string>(StringComparer.Ordinal) { "ext:UBLExtensions" });
        Assert.True(diferencias.Count == 0, string.Join(Environment.NewLine, diferencias));
        Assert.Empty(_servicios.Obtener<ValidadorEsquema>().Validar(xml));
    }
}
