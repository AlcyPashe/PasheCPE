using System.Globalization;
using PasheCPE.Catalogos;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;
using static PasheCPE.Tests.Utilidades.XmlDePrueba;

namespace PasheCPE.Tests.Validacion;

/// <summary>Reglas C# que complementan o reemplazan a la XSL de SUNAT (ajustes-xsl.json, TRD §9.4).</summary>
public sealed class ReglasComplementariasTests
{
    [Fact]
    public async Task SumatoriaIgv_BoletaMypeDiezYMedio_SinObservacion4290()
    {
        using var servicios = new ServiciosPrueba();
        var (xml, referencia) = await GenerarAsync(servicios, "BoletaMypeDiezYMedio");

        var resultado = servicios.Obtener<ValidadorSunat>().Validar(xml, referencia);

        Assert.Empty(resultado.Errores);
        Assert.Empty(resultado.Observaciones);
    }

    [Fact]
    public async Task SumatoriaIgv_BoletaNrusMype_SuprimeLa4290DeLaXslQueUsa18Fijo()
    {
        using var servicios = new ServiciosPrueba();
        var (xml, referencia) = await GenerarAsync(servicios, "BoletaMypeDiezYMedio");
        var nrus = Alterar(xml, r => r.Element(Cbc + "InvoiceTypeCode")!.SetAttributeValue("listID", "0113"));

        var xsl = servicios.Obtener<MotorXslSunat>().Validar(nrus, referencia);
        var validacion = servicios.Obtener<ValidadorSunat>().Validar(nrus, referencia);

        // La XSL calcula la sumatoria con 0.18 y observa un IGV correcto al 10.5 %; la regla C# usa la tasa de las líneas.
        Assert.Contains(xsl.Hallazgos, h => h.Codigo == "4290" && !h.EsError);
        Assert.DoesNotContain(validacion.Observaciones, o => o.Codigo == "4290");
    }

    [Fact]
    public async Task SumatoriaIgv_BoletaNrusConIgvMalCalculado_ObservaConLaReglaDeReemplazo()
    {
        using var servicios = new ServiciosPrueba();
        var (xml, referencia) = await GenerarAsync(servicios, "BoletaMypeDiezYMedio");
        var nrus = Alterar(xml, r =>
        {
            r.Element(Cbc + "InvoiceTypeCode")!.SetAttributeValue("listID", "0113");
            r.Subtotal("1000").Element(Cbc + "TaxAmount")!.Value = "40.00";
        });

        var validacion = servicios.Obtener<ValidadorSunat>().Validar(nrus, referencia);

        var observacion = Assert.Single(validacion.Observaciones, o => o.Codigo == "4290");
        Assert.Contains("10.5", observacion.Descripcion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SumatoriaIgv_SinReglasComplementarias_NoSuprimeLaXsl()
    {
        using var servicios = new ServiciosPrueba(o => o.Validacion.Reglas2027 = false);
        var (xml, referencia) = await GenerarAsync(servicios, "BoletaMypeDiezYMedio");
        var nrus = Alterar(xml, r => r.Element(Cbc + "InvoiceTypeCode")!.SetAttributeValue("listID", "0113"));

        var validacion = servicios.Obtener<ValidadorSunat>().Validar(nrus, referencia);

        Assert.Contains(validacion.Observaciones, o => o.Codigo == "4290");
    }

    [Theory]
    [InlineData("0.90", false)]
    [InlineData("-0.90", false)]
    [InlineData("1.10", true)]
    [InlineData("-1.10", true)]
    public async Task SumatoriaIgv_Factura_ToleranciaDeUnSol(string diferencia, bool rechaza)
    {
        using var servicios = new ServiciosPrueba();
        var (xml, referencia) = await GenerarAsync(servicios, "MypeDiezYMedio2026");
        var alterado = Alterar(xml, r =>
        {
            var igv = r.Subtotal("1000").Element(Cbc + "TaxAmount")!;
            igv.Value = (decimal.Parse(igv.Value, CultureInfo.InvariantCulture) + decimal.Parse(diferencia, CultureInfo.InvariantCulture)).ToString("0.00", CultureInfo.InvariantCulture);
        });

        var validacion = servicios.Obtener<ValidadorSunat>().Validar(alterado, referencia);

        Assert.Equal(rechaza, validacion.Errores.Any(e => e.Codigo == "3291"));
    }

    [Fact]
    public async Task TasaIgvUnica_BoletaConTasasDistintas_Rechaza()
    {
        using var servicios = new ServiciosPrueba();
        var (xml, referencia) = await GenerarAsync(servicios, "BoletaGravada");
        var alterado = Alterar(xml, r => r.PrimeraLinea().Subtotal("1000").Elemento(Cac + "TaxCategory", Cbc + "Percent").Value = "10.50");

        var validacion = servicios.Obtener<ValidadorSunat>().Validar(alterado, referencia);

        Assert.Contains(validacion.Errores, e => e.Codigo == "3462");
    }

    [Fact]
    public async Task ContratoColaboracion_DosNumeros_Rechaza3498()
    {
        using var servicios = new ServiciosPrueba(ahora: CasosTributarios.Ahora(new DateOnly(2027, 1, 15)));
        var (xml, referencia) = await GenerarAsync(servicios, "ContratoColaboracion2027");
        var alterado = Alterar(xml, r =>
        {
            var numero = r.Elemento(Cac + "ContractDocumentReference", Cbc + "ID");
            numero.AddAfterSelf(new System.Xml.Linq.XElement(numero));
        });

        var validacion = servicios.Obtener<ValidadorSunat>().Validar(alterado, referencia);

        Assert.Contains(validacion.Errores, e => e.Codigo == "3498");
    }

    [Fact]
    public async Task ContratoColaboracion_AntesDel2027_NoAplica()
    {
        byte[] xml;
        ReferenciaCpe referencia;
        using (var servicios2027 = new ServiciosPrueba(ahora: CasosTributarios.Ahora(new DateOnly(2027, 1, 15))))
        {
            (xml, referencia) = await GenerarAsync(servicios2027, "ContratoColaboracion2027");
        }

        var alterado = Alterar(xml, r => r.Elemento(Cac + "ContractDocumentReference", Cbc + "DocumentTypeCode").Value = "3");
        using var servicios2026 = new ServiciosPrueba(ahora: CasosTributarios.Ahora(new DateOnly(2026, 12, 31)));

        var validacion = servicios2026.Obtener<ValidadorSunat>().Validar(alterado, referencia);

        Assert.DoesNotContain(validacion.Errores, e => e.Codigo is "3497" or "3498" or "3499" or "3500" or "3501" or "3502");
    }

    [Fact]
    public async Task ValidadorSunat_SinXslNiReglas_NoValidaNada()
    {
        using var servicios = new ServiciosPrueba(o =>
        {
            o.Validacion.XslSunat = false;
            o.Validacion.Reglas2027 = false;
        });
        var (xml, referencia) = await GenerarAsync(servicios, "Gravada18");
        var alterado = Alterar(xml, r => r.Element(Cbc + "DocumentCurrencyCode")!.Value = "XYZ");

        var validacion = servicios.Obtener<ValidadorSunat>().Validar(alterado, referencia);

        Assert.Empty(validacion.Errores);
        Assert.Empty(validacion.Observaciones);
    }

    [Fact]
    public void TipoVigente_ObservErrorAntesYDespuesDeLaVigencia()
    {
        var regla = new ReglaExcelSunat("01", 155, null, null, null, "error", "observacion", "3496", null, new DateOnly(2027, 1, 1));
        var nueva = regla with { TipoAnterior = null };

        Assert.Equal("observacion", ReglasExcelSunat.TipoVigente(regla, new DateOnly(2026, 12, 31)));
        Assert.Equal("error", ReglasExcelSunat.TipoVigente(regla, new DateOnly(2027, 1, 1)));
        Assert.Null(ReglasExcelSunat.TipoVigente(nueva, new DateOnly(2026, 12, 31)));
        Assert.Equal("error", ReglasExcelSunat.TipoVigente(regla with { Vigencia = null }, new DateOnly(2020, 1, 1)));
    }
}
