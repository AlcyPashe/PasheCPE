using Microsoft.Extensions.DependencyInjection;
using PasheCPE.Servicios;
using PasheCPE.Tests.Utilidades;
using static PasheCPE.Tests.Utilidades.RespuestasSoap;

namespace PasheCPE.Tests.Servicios;

/// <summary>
/// Gancho de la generación automática del PDF (TRD §22) con un doble de PasheCPE.Pdf: cuándo se llama y qué pasa si
/// el PDF falla. El PDF real se prueba en PasheCPE.Pdf.Tests.
/// </summary>
public sealed class PdfAutomaticoTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (ServiciosPrueba Servicios, PdfFalso Pdf) Crear(bool automatico = true)
    {
        var pdf = new PdfFalso();
        var servicios = new ServiciosPrueba(o => o.GenerarPdfAutomaticamente = automatico, registrar: s => s.AddSingleton<IGeneracionPdfAutomatica>(pdf));
        return (servicios, pdf);
    }

    [Fact]
    public async Task EmitirAsync_PdfFalla_EmisionIgualYAdvertenciaPcpe5001()
    {
        var (servicios, pdf) = Crear();
        using var _ = servicios;
        pdf.Error = "disco lleno";
        servicios.Sunat.Responder(SendBill, ConCdr(Cdr("F001-123", "0", "La Factura numero F001-123, ha sido aceptada")));

        var emision = await servicios.Obtener<IServicioCpe>().EmitirAsync(CertificadoPrueba.Emisor(servicios.Almacen), Ejemplos.Factura(), Ct);

        Assert.Equal(EstadoCpe.Aceptado, emision.Envio.Estado);
        Assert.Null(emision.Envio.Rutas.Pdf);
        var advertencia = Assert.Single(emision.Generacion.Advertencias, a => a.Codigo == "PCPE-5001");
        Assert.Contains("20123456786-01-F001-123", advertencia.Descripcion, StringComparison.Ordinal);
        Assert.Contains("disco lleno", advertencia.Descripcion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmitirAsync_Aceptado_RecibeElEmisorYLaReferencia()
    {
        var (servicios, pdf) = Crear();
        using var _ = servicios;
        servicios.Sunat.Responder(SendBill, ConCdr(Cdr("F001-123", "0", "La Factura numero F001-123, ha sido aceptada")));
        var emisor = CertificadoPrueba.Emisor(servicios.Almacen);

        var emision = await servicios.Obtener<IServicioCpe>().EmitirAsync(emisor, Ejemplos.Factura(), Ct);

        var (ruc, referencia) = Assert.Single(pdf.Llamadas);
        Assert.Equal((emisor.Ruc, emision.Generacion.Referencia), (ruc, referencia));
        Assert.Equal(PdfFalso.Ruta, emision.Envio.Rutas.Pdf);
    }

    [Fact]
    public async Task EmitirAsync_ErrorDeValidacion_NoLlamaAlPdf()
    {
        var (servicios, pdf) = Crear();
        using var _ = servicios;

        var emision = await servicios.Obtener<IServicioCpe>().EmitirAsync(CertificadoPrueba.Emisor(servicios.Almacen), Ejemplos.Factura() with { Lineas = [] }, Ct);

        Assert.Equal(EstadoCpe.ErrorValidacionLocal, emision.Envio.Estado);
        Assert.Empty(pdf.Llamadas);
    }

    [Fact]
    public async Task EmitirAsync_ServicioNoDisponible_NoLlamaAlPdf()
    {
        var (servicios, pdf) = Crear();
        using var _ = servicios;
        servicios.Sunat.Responder(SendBill, "Service Unavailable", System.Net.HttpStatusCode.ServiceUnavailable);

        var emision = await servicios.Obtener<IServicioCpe>().EmitirAsync(CertificadoPrueba.Emisor(servicios.Almacen), Ejemplos.Factura(), Ct);

        Assert.Equal(EstadoCpe.ServicioNoDisponible, emision.Envio.Estado);
        Assert.Empty(pdf.Llamadas);
    }

    [Fact]
    public async Task EmitirAsync_OpcionDesactivada_NoLlamaAlPdf()
    {
        var (servicios, pdf) = Crear(automatico: false);
        using var _ = servicios;
        servicios.Sunat.Responder(SendBill, ConCdr(Cdr("F001-123", "0", "La Factura numero F001-123, ha sido aceptada")));

        await servicios.Obtener<IServicioCpe>().EmitirAsync(CertificadoPrueba.Emisor(servicios.Almacen), Ejemplos.Factura(), Ct);

        Assert.Empty(pdf.Llamadas);
    }

    [Fact]
    public async Task EmitirAsync_SinPasheCpePdf_NoHaceNada()
    {
        using var servicios = new ServiciosPrueba(o => o.GenerarPdfAutomaticamente = true);
        servicios.Sunat.Responder(SendBill, ConCdr(Cdr("F001-123", "0", "La Factura numero F001-123, ha sido aceptada")));

        var emision = await servicios.Obtener<IServicioCpe>().EmitirAsync(CertificadoPrueba.Emisor(servicios.Almacen), Ejemplos.Factura(), Ct);

        Assert.Equal(EstadoCpe.Aceptado, emision.Envio.Estado);
        Assert.Null(emision.Envio.Rutas.Pdf);
        Assert.DoesNotContain(emision.Generacion.Advertencias, a => a.Codigo == "PCPE-5001");
    }

    private sealed class PdfFalso : IGeneracionPdfAutomatica
    {
        public const string Ruta = @"X:\REPO\2026\10\documento.pdf";

        public List<(string Ruc, ReferenciaCpe Referencia)> Llamadas { get; } = [];

        public string? Error { get; set; }

        public Task<(string? Ruta, string? Error)> GenerarAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct)
        {
            Llamadas.Add((emisor.Ruc, referencia));
            return Task.FromResult(Error is null ? (Ruta, (string?)null) : ((string?)null, Error));
        }
    }
}
