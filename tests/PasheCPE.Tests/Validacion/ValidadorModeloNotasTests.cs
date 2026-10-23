using PasheCPE.Mapeo;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;

namespace PasheCPE.Tests.Validacion;

/// <summary>Validación del modelo de notas de crédito y débito (PCPE-14xx y las reglas comunes con factura y boleta).</summary>
public sealed class ValidadorModeloNotasTests : IDisposable
{
    private static readonly DateOnly _fecha2027 = new(2027, 1, 15);

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    public static TheoryData<string> Casos => [.. CasosNotas.Todos.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Casos))]
    public void Validar_CasosDeEjemplo_NoTienenErrores(string nombre)
    {
        var caso = CasosNotas.Todos[nombre];
        using var servicios = new ServiciosPrueba(ahora: CasosTributarios.Ahora(caso.FechaEmision));

        Assert.Empty(servicios.Obtener<ValidadorModelo>().Validar(caso.Emisor, caso.Nota));
    }

    [Theory]
    [InlineData("TipoDocumento", "PCPE-1401", null)]
    [InlineData("MotivoInexistente", "PCPE-1001", "09")]
    [InlineData("MotivoDeDebitoInexistente", "PCPE-1001", "10")]
    [InlineData("PenalidadesAntesDe2027", "PCPE-1002", "10")]
    [InlineData("SustentoVacio", "PCPE-1402", null)]
    [InlineData("SustentoConSaltoDeLinea", "PCPE-1402", null)]
    [InlineData("SustentoLargo", "PCPE-1402", null)]
    [InlineData("SinDocumentoModificado", "PCPE-1403", null)]
    [InlineData("ModificaUnTicket", "PCPE-1404", null)]
    [InlineData("SerieDelModificadoIncoherente", "PCPE-1405", null)]
    [InlineData("NumeroDelModificado", "PCPE-1205", null)]
    [InlineData("ModificadoPosteriorALaNota", "PCPE-1408", null)]
    [InlineData("NotaBModificaFactura", "PCPE-1406", null)]
    [InlineData("NotaFModificaBoleta", "PCPE-1406", null)]
    [InlineData("SerieDeNotaInvalida", "PCPE-1407", null)]
    [InlineData("PlazoDeFacturaVencido", "PCPE-1207", null)]
    [InlineData("DescuentoGlobal", "PCPE-1414", null)]
    [InlineData("DescuentoDeLinea", "PCPE-1415", null)]
    [InlineData("FormaPagoFueraDelAjusteDeCuotas", "PCPE-1412", null)]
    [InlineData("AjusteDeCuotasSobreBoleta", "PCPE-1409", null)]
    [InlineData("AjusteDeCuotasAlContado", "PCPE-1410", null)]
    [InlineData("ClienteConFormatoInvalido", "PCPE-1212", null)]
    public void Validar_Nota_DetectaElError(string caso, string codigo, string? catalogo)
    {
        var errores = _servicios.Obtener<ValidadorModelo>().Validar(CasosTributarios.Gravado(), Modificar(caso));

        var error = Assert.Single(errores, e => e.Codigo == codigo);
        Assert.Equal(OrigenError.Modelo, error.Origen);
        Assert.Equal(catalogo, error.Catalogo);
    }

    [Fact]
    public void Validar_PlazoDeNotaDeBoleta_EsDeSieteDias()
    {
        // Emitida el 18/10: la nota de factura venció el 21/10 (3 días); la de boleta vence el 25/10 (7 días).
        var nota = CasosNotas.Boleta(CasosNotas.Credito(MotivoNotaCredito.AnulacionDeLaOperacion, CasosTributarios.Linea(1, 59m))) with
        {
            FechaEmision = new DateOnly(2026, 10, 18),
            DocumentoModificado = CasosNotas.BoletaModificada(new DateOnly(2026, 10, 18)),
        };

        Assert.Empty(_servicios.Obtener<ValidadorModelo>().Validar(CasosTributarios.Gravado(), nota));
    }

    [Fact]
    public void Validar_NuevoRusConNotaDeFactura_DevuelveError()
    {
        var emisor = Ejemplos.Emisor(RegimenTributario.NRUS);

        var errores = _servicios.Obtener<ValidadorModelo>().Validar(emisor, CasosNotas.Todos["NcAnulacionFactura"].Nota);

        Assert.Contains(errores, e => e.Codigo == "PCPE-1101");
    }

    [Fact]
    public void Validar_NuevoRusConNotaDeBoleta_NoTieneErrores()
    {
        var emisor = Ejemplos.Emisor(RegimenTributario.NRUS);

        var errores = _servicios.Obtener<ValidadorModelo>().Validar(emisor, CasosNotas.Todos["NcBoletaExonerada"].Nota);

        Assert.DoesNotContain(errores, e => e.Codigo == "PCPE-1101");
    }

    [Fact]
    public async Task Construir_AjusteDeCuotasConImporte_DevuelvePcpe1411()
    {
        var nota = CasosNotas.Todos["NcAjusteCuotas"].Nota;
        nota = nota with { Lineas = [CasosTributarios.Linea(1, 10m)] };

        var generado = await _servicios.Obtener<GeneradorNota>().ConstruirAsync(CasosTributarios.Gravado(), nota, TestContext.Current.CancellationToken);

        Assert.Contains(generado.Errores, e => e.Codigo == "PCPE-1411");
    }

    [Fact]
    public async Task Construir_PenalidadesGravadas_DevuelvePcpe1413()
    {
        using var servicios = new ServiciosPrueba(ahora: CasosTributarios.Ahora(_fecha2027));
        var nota = CasosNotas.Todos["NdPenalidades2027"].Nota with { Lineas = [CasosTributarios.Linea(1, 118m)] };

        var generado = await servicios.Obtener<GeneradorNota>().ConstruirAsync(CasosTributarios.Gravado(), nota, TestContext.Current.CancellationToken);

        var error = Assert.Single(generado.Errores);
        Assert.Equal("PCPE-1413", error.Codigo);
    }

    [Fact]
    public async Task Construir_CorreccionDeDescripcionConImportes_SeGeneraSinAdvertencias()
    {
        // Beta aceptó la NC 03 en cero (FC01-24) y con importes (FC01-25) sin observaciones, y el Excel 26.08.2026 no
        // tiene una regla para sus importes: no se valida (se revisa en enero de 2027, 05-Mantenimiento-Normativo).
        var nota = CasosNotas.Credito(MotivoNotaCredito.CorreccionDescripcion, CasosTributarios.Linea(1, 59m));

        var generado = await _servicios.Obtener<GeneradorNota>().ConstruirAsync(CasosTributarios.Gravado(), nota, TestContext.Current.CancellationToken);

        Assert.True(generado.Exito, string.Join("; ", generado.Errores.Select(e => e.Codigo)));
        Assert.Empty(generado.Advertencias);
    }

    private static NotaCpe Modificar(string caso)
    {
        var nota = CasosNotas.Todos["NcAnulacionFactura"].Nota;
        var debito = CasosNotas.Todos["NdIntereses"].Nota;
        return caso switch
        {
            "TipoDocumento" => nota with { TipoDocumento = TipoDocumentoSunat.Factura },
            "MotivoInexistente" => nota with { Motivo = "99" },
            "MotivoDeDebitoInexistente" => debito with { Motivo = "07" },
            "PenalidadesAntesDe2027" => debito with { Motivo = MotivoNotaDebito.Penalidades, Lineas = [CasosTributarios.Linea(1, 10m) with { Afectacion = AfectacionIgv.InafectoOneroso }] },
            "SustentoVacio" => nota with { Sustento = " " },
            "SustentoConSaltoDeLinea" => nota with { Sustento = "DEVOLUCIÓN\nPARCIAL" },
            "SustentoLargo" => nota with { Sustento = new string('A', 501) },
            "SinDocumentoModificado" => nota with { DocumentoModificado = null },
            "ModificaUnTicket" => nota with { DocumentoModificado = CasosNotas.Factura() with { TipoDocumento = "12" } },
            "SerieDelModificadoIncoherente" => nota with { DocumentoModificado = CasosNotas.Factura() with { Serie = "B001" } },
            "NumeroDelModificado" => nota with { DocumentoModificado = CasosNotas.Factura() with { Numero = 0 } },
            "ModificadoPosteriorALaNota" => nota with { DocumentoModificado = CasosNotas.Factura(new DateOnly(2026, 10, 23)) },
            "NotaBModificaFactura" => nota with { Serie = "BC01" },
            "NotaFModificaBoleta" => nota with { DocumentoModificado = CasosNotas.BoletaModificada() },
            "SerieDeNotaInvalida" => nota with { Serie = "XC1" },
            "PlazoDeFacturaVencido" => nota with { FechaEmision = new DateOnly(2026, 10, 18), DocumentoModificado = CasosNotas.Factura(new DateOnly(2026, 10, 18)) },
            "DescuentoGlobal" => nota with { DescuentosCargos = [new DescuentoCargoCpe { Codigo = CargoDescuento.DescuentoGlobalAfectaBase, Factor = 0.1m }] },
            "DescuentoDeLinea" => nota with { Lineas = [CasosTributarios.Linea(2, 59m) with { Descuento = 10m }] },
            "FormaPagoFueraDelAjusteDeCuotas" => nota with { FormaPago = FormaPagoCpe.Contado },
            "AjusteDeCuotasSobreBoleta" => CasosNotas.Todos["NcAjusteCuotas"].Nota with { DocumentoModificado = CasosNotas.BoletaModificada(), Serie = "BC01" },
            "AjusteDeCuotasAlContado" => CasosNotas.Todos["NcAjusteCuotas"].Nota with { FormaPago = null },
            "ClienteConFormatoInvalido" => nota with { Cliente = nota.Cliente with { TipoDocumento = TipoDocumentoIdentidad.Dni, NumeroDocumento = "123" } },
            _ => throw new ArgumentOutOfRangeException(nameof(caso), caso, null),
        };
    }
}
