using Microsoft.Extensions.DependencyInjection;
using PasheCPE.Pdf.Representacion;
using PasheCPE.Pdf.Tests.Apoyo;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Pdf.Tests.Representacion;

public sealed class LectorRepresentacionTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new(registrar: s => s.AddPasheCpePdf());

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private LectorRepresentacion Lector => _servicios.Obtener<LectorRepresentacion>();

    private ComprobanteImpreso Comprobante(DoradoPdf dorado)
    {
        var lectura = Lector.Leer(dorado.Referencia(), File.ReadAllBytes(dorado.RutaXml), File.ReadAllBytes(dorado.RutaCdr), null);
        Assert.Null(lectura.Error);
        return lectura.Comprobante!;
    }

    [Fact]
    public void Leer_FacturaExoneradaConGratuitas_TotalesLineasYMontoEnLetrasCalculado()
    {
        var cpe = Comprobante(DoradosPdf.FacturaExoneradaGratuita);

        Assert.Equal(("01", "F001-3565", new DateOnly(2026, 10, 22), "PEN"), (cpe.TipoDocumento, cpe.SerieNumero, cpe.FechaEmision, cpe.Moneda));
        Assert.Equal(("6", "20611619767", "CSJ DISTRIBUCIONES SOCIEDAD ANONIMA CERRADA"), (cpe.Emisor.TipoDocumento, cpe.Emisor.NumeroDocumento, cpe.Emisor.Nombre));
        Assert.Equal("Av. Augusto Freyre Nro. 727, IQUITOS, MAYNAS, LORETO", cpe.Emisor.Direccion);
        Assert.Equal(("20600124936", "DISTRIBUIDORA RAISSI E.I.R.L."), (cpe.Cliente.NumeroDocumento, cpe.Cliente.Nombre));
        Assert.Equal("Contado", cpe.FormaPago);
        Assert.Equal((2634.25m, 218.55m, 0m, 2634.25m), (cpe.Totales.Exonerado, cpe.Totales.Gratuito, cpe.Totales.Igv, cpe.Totales.ImporteTotal));
        Assert.Equal(8, cpe.Lineas.Count);

        var primera = cpe.Lineas[0];
        Assert.Equal((1, "040271", "NIU", 20m, 1.96m, 39.20m, true), (primera.Numero, primera.Codigo, primera.Unidad, primera.Cantidad, primera.PrecioUnitario, primera.ValorVenta, primera.EsGratuita));

        // El XML de Fattura no trae la leyenda 1000: se arma con tildes para el PDF.
        Assert.Equal(new LeyendaImpresa("1000", "DOS MIL SEISCIENTOS TREINTA Y CUATRO CON 25/100 SOLES"), cpe.Leyendas[0]);
        Assert.Contains(cpe.Leyendas, l => l.Codigo == "1002");
        Assert.NotNull(cpe.ValorResumen);
        Assert.Contains("aceptada", cpe.Estado!.Texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Leer_FacturaExoneradaBeta_LeyendasDelXmlYAmazonia()
    {
        var cpe = Comprobante(DoradosPdf.FacturaExonerada);

        Assert.Equal((129.80m, 129.80m, 0m), (cpe.Totales.Exonerado, cpe.Totales.ImporteTotal, cpe.Totales.Igv));
        Assert.Equal(["1000", "2001", "2002"], cpe.Leyendas.Select(l => l.Codigo));
        Assert.Equal("CIENTO VEINTINUEVE CON 80/100 SOLES", cpe.Leyendas[0].Texto);
        Assert.Equal(2, cpe.Lineas.Count);
    }

    [Fact]
    public void Leer_Boleta_ClienteConDniEIgv()
    {
        var cpe = Comprobante(DoradosPdf.Boleta);

        Assert.Equal(("03", "B001-3"), (cpe.TipoDocumento, cpe.SerieNumero));
        Assert.Equal(("1", "12345678"), (cpe.Cliente.TipoDocumento, cpe.Cliente.NumeroDocumento));
        Assert.True(cpe.Totales.Igv > 0);
        Assert.Equal(cpe.Totales.ImporteTotal, cpe.Totales.Gravado + cpe.Totales.Igv);
    }

    [Fact]
    public void Leer_NotaDeCredito_DocumentoModificadoYMotivoDelCatalogo()
    {
        var cpe = Comprobante(DoradosPdf.NotaCredito);

        Assert.Equal(("07", "F001-840"), (cpe.TipoDocumento, cpe.SerieNumero));
        Assert.Equal(new DocumentoRelacionadoImpreso("01", "F006-179919"), cpe.DocumentoModificado);
        Assert.Equal(("01", "Anulación de la operación"), (cpe.CodigoMotivo, cpe.Motivo));
        Assert.Equal(46.18m, cpe.Totales.ImporteTotal);
    }

    [Fact]
    public void Leer_NotaDeDebito_TotalesDeRequestedMonetaryTotal()
    {
        var cpe = Comprobante(DoradosPdf.NotaDebito);

        Assert.Equal(("08", "FD01-23"), (cpe.TipoDocumento, cpe.SerieNumero));
        Assert.Equal(new DocumentoRelacionadoImpreso("01", "F001-2"), cpe.DocumentoModificado);
        Assert.Equal(("01", "Intereses por mora", "INTERESES POR PAGO FUERA DE PLAZO"), (cpe.CodigoMotivo, cpe.Motivo, cpe.Sustento));
        Assert.Equal((10m, 1.80m, 11.80m), (cpe.Totales.Gravado, cpe.Totales.Igv, cpe.Totales.ImporteTotal));
        var linea = Assert.Single(cpe.Lineas);
        Assert.Equal(("S002", "INTERESES POR MORA", 10m, 11.80m), (linea.Codigo, linea.Descripcion, linea.ValorUnitario, linea.PrecioUnitario));
    }

    [Fact]
    public void Leer_ValorResumenYQr_DelXmlFirmado()
    {
        var cpe = Comprobante(DoradosPdf.NotaDebito);

        Assert.Equal($"20493400364|08|FD01|23|1.80|11.80|2026-10-23|6|20611619767|{cpe.ValorResumen}", cpe.ContenidoQr);
    }

    [Fact]
    public void Leer_GuiaRemitente_TrasladoTransportistaYQrDelCdr()
    {
        var dorado = DoradosPdf.Guia;
        var lectura = Lector.Leer(dorado.Referencia(), File.ReadAllBytes(dorado.RutaXml), File.ReadAllBytes(dorado.RutaCdr), null);

        Assert.Null(lectura.Error);
        var guia = lectura.Guia!;
        Assert.Equal(("T001-31093", new DateOnly(2026, 10, 22)), (guia.SerieNumero, guia.FechaEmision));
        Assert.Equal(("6", "20611619767"), (guia.Remitente.TipoDocumento, guia.Remitente.NumeroDocumento));
        Assert.Equal(("1", "05374177", "RAMIREZ RUIZ, ROXANA"), (guia.Destinatario.TipoDocumento, guia.Destinatario.NumeroDocumento, guia.Destinatario.Nombre));
        Assert.Equal(("01", "Venta", "01", "Transporte público"), (guia.CodigoMotivo, guia.Motivo, guia.CodigoModalidad, guia.Modalidad));
        Assert.Equal((3m, "KGM", new DateOnly(2026, 10, 22)), (guia.PesoBruto, guia.UnidadPeso, guia.FechaTraslado));
        Assert.Equal(("20607254720", "SOLUCIONES LOGISTICAS J & H SOCIEDAD ANONIMA CERRADA"), (guia.Transportista!.NumeroDocumento, guia.Transportista.Nombre));
        Assert.Equal("Av. Augusto Freyre Nro. 727 (UBIGEO 160101)", guia.Partida);
        Assert.StartsWith("CAL CALVO", guia.Llegada, StringComparison.Ordinal);
        var documento = Assert.Single(guia.DocumentosRelacionados);
        Assert.Equal(("Boleta de Venta", "B006-00644248", "20611619767"), (documento.Tipo, documento.Numero, documento.Emisor));
        var bien = Assert.Single(guia.Bienes);
        Assert.Equal(("BEBIDA DE ALMENDRA LAIVE CJ 946ML", "NIU", 3m), (bien.Descripcion, bien.Unidad, bien.Cantidad));
        Assert.StartsWith("https://", guia.EnlaceQr, StringComparison.Ordinal);
    }

    [Fact]
    public void Leer_GuiaSinCdr_Pcpe5005()
    {
        var dorado = DoradosPdf.Guia;

        var lectura = Lector.Leer(dorado.Referencia(), File.ReadAllBytes(dorado.RutaXml), null, null);

        Assert.Equal("PCPE-5005", lectura.Error!.Codigo);
        Assert.Equal(OrigenError.Pdf, lectura.Error.Origen);
    }

    [Fact]
    public void Leer_XmlIlegible_Pcpe5003SinExcepcion()
    {
        var lectura = Lector.Leer(DoradosPdf.Boleta.Referencia(), "<Invoice><sin cerrar"u8.ToArray(), null, null);

        Assert.Equal("PCPE-5003", lectura.Error!.Codigo);
    }

    [Fact]
    public void Leer_ResumenDiario_Pcpe5004()
    {
        var lectura = Lector.Leer(ReferenciaCpe.Resumen(Ejemplos.Ruc, new DateOnly(2026, 10, 22), 1), "<x/>"u8.ToArray(), null, null);

        Assert.Equal("PCPE-5004", lectura.Error!.Codigo);
    }

    [Fact]
    public async Task LeerAsync_SinXmlFirmado_Pcpe5002()
    {
        var lectura = await Lector.LeerAsync(Ejemplos.Referencia(), Ct);

        Assert.Equal("PCPE-5002", lectura.Error!.Codigo);
    }

    [Fact]
    public async Task LeerAsync_DesdeElAlmacen_ConEstadoDelCdr()
    {
        var referencia = DoradosPdf.Colocar(_servicios.Almacen, DoradosPdf.NotaDebito);

        var lectura = await Lector.LeerAsync(referencia, Ct);

        Assert.Equal("FD01-23", lectura.Comprobante!.SerieNumero);
        Assert.NotNull(lectura.Comprobante.Estado);
    }
}
