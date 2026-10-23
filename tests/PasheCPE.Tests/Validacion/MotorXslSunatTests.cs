using PasheCPE.Tests.Dorados;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;

namespace PasheCPE.Tests.Validacion;

public sealed class MotorXslSunatTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    public static TheoryData<string> Xsl => [.. MotorXslSunat.XslEmbebidas];

    public static TheoryData<string> Dorados =>
    [
        Path.Combine("01-Factura", "20611619767-01-F001-3565.xml"),
        Path.Combine("01-Factura", "20611619767-01-F006-180006.xml"),
        Path.Combine("07-NotaCredito", "20611619767-07-F001-840.xml"),
        Path.Combine("07-NotaCredito", "20611619767-07-F001-841.xml"),
        Path.Combine("RC-Resumen", "20611619767-RC-20261021-11.xml"),
        Path.Combine("RC-Resumen", "20611619767-RC-20261022-7.xml"),
    ];

    [Theory]
    [MemberData(nameof(Xsl))]
    public void Compilar_CadaXsl_CompilaConLasCorreccionesConocidas(string archivo)
    {
        var correcciones = MotorXslSunat.Compilar(archivo);

        // Si SUNAT cambia una XSL, estos conteos cambian y obligan a revisar xsl-compatibilidad.json (TRD §9.3).
        var esperadas = archivo switch
        {
            "ValidaExprRegFactura-2.0.1.xsl" => (2, new[] { "totalDescuentosGlobales" }, Array.Empty<string>()),
            "ValidaExprRegBoleta-2.0.1.xsl" => (2, ["totalDescuentosGlobales"], ["nroLinea"]),
            "ValidaExprRegNC-2.0.1.xsl" => (0, [], ["MontoDescuentoAfectoBI"]),
            "ValidaExprRegND-2.0.1.xsl" => (0, ["totalImpuestos", "SumatoriaImpuestos", "valorVentaLinea"], ["nroLinea", "MontoDescuentoAfectoBI"]),
            _ => (0, [], []),
        };
        Assert.Equal(esperadas.Item1, correcciones.ConcatUnArgumento);
        Assert.Equal(esperadas.Item2.Order(), correcciones.VariablesRedeclaradas.Order());
        Assert.Equal(esperadas.Item3.Order(), correcciones.VariablesRespaldo.Order());
    }

    [Theory]
    [MemberData(nameof(Dorados))]
    public void Validar_DoradoAceptadoPorSunat_SinErrores(string relativo)
    {
        var ruta = Path.Combine(LectorDorado.Carpeta(Path.GetDirectoryName(relativo)!), Path.GetFileName(relativo));
        var resultado = _servicios.Obtener<MotorXslSunat>().Validar(File.ReadAllBytes(ruta), LectorDorado.Referencia(ruta));

        Assert.Null(resultado.Falla);
        Assert.True(
            resultado.Hallazgos.Count == 0,
            string.Join(Environment.NewLine, resultado.Hallazgos.Select(h => $"{h.Codigo} {(h.EsError ? "ERR" : "OBS")} {h.Ruta}: {h.Detalle}")));
    }
}
