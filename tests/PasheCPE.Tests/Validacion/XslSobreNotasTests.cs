using PasheCPE.Mapeo;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;
using PasheCPE.Validacion.Reglas;

namespace PasheCPE.Tests.Validacion;

/// <summary>
/// Las notas de crédito y débito que genera PasheCPE para cada caso pasan el XSD, las XSL de SUNAT (descontando los
/// códigos que <c>ajustes-xsl.json</c> suprime a la fecha) y las reglas complementarias sin errores ni observaciones.
/// </summary>
public sealed class XslSobreNotasTests
{
    public static TheoryData<string> Casos => [.. CasosNotas.Todos.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Casos))]
    public async Task Validar_NotaGenerada_SinHallazgosDeSunat(string nombre)
    {
        var caso = CasosNotas.Todos[nombre];
        using var servicios = new ServiciosPrueba(ahora: CasosTributarios.Ahora(caso.FechaEmision));

        var generado = await servicios.Obtener<GeneradorNota>().GenerarAsync(caso.Emisor, caso.Nota, TestContext.Current.CancellationToken);

        Assert.True(generado.Exito, string.Join(Environment.NewLine, generado.Errores.Select(e => $"{e.Codigo} {e.Ruta}: {e.Descripcion}")));
        Assert.DoesNotContain(generado.Advertencias, a => !a.Codigo.StartsWith("PCPE-", StringComparison.Ordinal));
        var xsl = servicios.Obtener<MotorXslSunat>().Validar(generado.Xml!, generado.Referencia);
        Assert.Null(xsl.Falla);
        var ajustes = servicios.Obtener<AjustesXsl>();
        var hallazgos = xsl.Hallazgos.Where(h => !ajustes.EstaSuprimido(caso.Nota.TipoDocumento, h.Codigo, caso.FechaEmision)).ToList();
        Assert.True(hallazgos.Count == 0, string.Join(Environment.NewLine, hallazgos.Select(h => $"{h.Codigo} {(h.EsError ? "ERR" : "OBS")} {h.Ruta}: {h.Detalle}")));
    }
}
