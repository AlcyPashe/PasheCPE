using PasheCPE.Mapeo;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;

namespace PasheCPE.Tests.Validacion;

/// <summary>
/// Los XML que genera PasheCPE para cada caso tributario pasan las XSL de SUNAT y las reglas complementarias sin
/// errores ni observaciones.
/// </summary>
public sealed class XslSobreGeneradosTests
{
    public static TheoryData<string> Casos => [.. CasosTributarios.Todos.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Casos))]
    public async Task Validar_XmlGenerado_SinHallazgosDeSunat(string nombre)
    {
        var caso = CasosTributarios.Todos[nombre];
        using var servicios = new ServiciosPrueba(ahora: CasosTributarios.Ahora(caso.FechaEmision));

        var generado = await servicios.Obtener<GeneradorInvoice>().GenerarAsync(caso.Emisor, caso.Comprobante, TestContext.Current.CancellationToken);

        Assert.True(generado.Exito, string.Join(Environment.NewLine, generado.Errores.Select(e => $"{e.Codigo} {e.Ruta}: {e.Descripcion}")));
        Assert.DoesNotContain(generado.Advertencias, a => !a.Codigo.StartsWith("PCPE-", StringComparison.Ordinal));
        var xsl = servicios.Obtener<MotorXslSunat>().Validar(generado.Xml!, generado.Referencia);
        Assert.Null(xsl.Falla);
        Assert.True(xsl.Hallazgos.Count == 0, string.Join(Environment.NewLine, xsl.Hallazgos.Select(h => $"{h.Codigo} {(h.EsError ? "ERR" : "OBS")} {h.Ruta}: {h.Detalle}")));
    }
}
