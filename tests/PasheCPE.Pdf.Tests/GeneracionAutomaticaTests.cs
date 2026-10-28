using Microsoft.Extensions.DependencyInjection;
using PasheCPE.Tests.Utilidades;
using static PasheCPE.Tests.Utilidades.RespuestasSoap;

namespace PasheCPE.Pdf.Tests;

/// <summary>PDF dentro de <c>EmitirAsync</c> con PasheCPE.Pdf registrado (TRD §22). SUNAT es el doble de las pruebas.</summary>
public sealed class GeneracionAutomaticaTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ServiciosPrueba Servicios(bool automatico = true, Action<OpcionesPasheCpe>? configurar = null) =>
        new(
            o =>
            {
                o.GenerarPdfAutomaticamente = automatico;
                configurar?.Invoke(o);
            },
            registrar: s => s.AddPasheCpePdf(p => p.Marcas[Ejemplos.Ruc] = new MarcaEmisor { Web = "www.ejemplo.pe" }));

    [Fact]
    public async Task EmitirAsync_Aceptado_PdfEnRepoYEnLasRutas()
    {
        using var servicios = Servicios();
        servicios.Sunat.Responder(SendBill, ConCdr(Cdr("F001-123", "0", "La Factura numero F001-123, ha sido aceptada")));

        var emision = await servicios.Obtener<IServicioCpe>().EmitirAsync(CertificadoPrueba.Emisor(servicios.Almacen), Ejemplos.Factura(), Ct);

        Assert.Equal(EstadoCpe.Aceptado, emision.Envio.Estado);
        Assert.Equal(Path.Combine(servicios.Almacen.Ruta, "REPO", "2026", "10", "20123456786-01-F001-123.pdf"), emision.Envio.Rutas.Pdf);
        Assert.Equal(emision.Envio.Rutas.Pdf, emision.Generacion.Rutas.Pdf);
        Assert.True(File.Exists(emision.Envio.Rutas.Pdf));
        Assert.DoesNotContain(emision.Generacion.Advertencias, a => a.Codigo == "PCPE-5001");
    }

    [Fact]
    public async Task EmitirAsync_20EnParalelo_CadaUnaConSuPdf()
    {
        const int cantidad = 20;
        using var servicios = Servicios();
        var emisor = CertificadoPrueba.Emisor(servicios.Almacen);
        for (var n = 1; n <= cantidad; n++)
        {
            servicios.Sunat.Responder(SendBill, ConCdr(Cdr($"F001-{n}", "0", $"La Factura numero F001-{n}, ha sido aceptada")));
        }

        var emisiones = await Task.WhenAll(Enumerable.Range(1, cantidad).Select(n =>
            Task.Run(() => servicios.Obtener<IServicioCpe>().EmitirAsync(emisor, Ejemplos.Factura() with { Numero = n }, Ct), Ct)));

        Assert.All(emisiones, e => Assert.Equal(EstadoCpe.Aceptado, e.Envio.Estado));
        Assert.All(emisiones, e => Assert.DoesNotContain(e.Generacion.Advertencias, a => a.Codigo == "PCPE-5001"));
        Assert.Equal(cantidad, emisiones.Select(e => e.Envio.Rutas.Pdf).Distinct(StringComparer.Ordinal).Count(p => File.Exists(p)));
    }

    [Fact]
    public async Task EmitirAsync_Rechazado_SinPdf()
    {
        using var servicios = Servicios();
        servicios.Sunat.Responder(SendBill, ConCdr(Cdr("F001-123", "2800", "El dato ingresado en el tipo de documento de identidad del receptor no esta permitido.")));

        var emision = await servicios.Obtener<IServicioCpe>().EmitirAsync(CertificadoPrueba.Emisor(servicios.Almacen), Ejemplos.Factura(), Ct);

        Assert.Equal(EstadoCpe.Rechazado, emision.Envio.Estado);
        Assert.Null(emision.Envio.Rutas.Pdf);
        Assert.False(Directory.Exists(Path.Combine(servicios.Almacen.Ruta, "REPO")));
    }

    [Fact]
    public async Task EmitirAsync_BoletaParaElResumenDiario_PdfSinEnviar()
    {
        using var servicios = Servicios();

        var emision = await servicios.Obtener<IServicioCpe>().EmitirAsync(CertificadoPrueba.Emisor(servicios.Almacen), CasosTributarios.Todos["BoletaExonerada"].Comprobante, Ct);

        Assert.Equal("PCPE-4002", emision.Envio.Codigo);
        Assert.True(File.Exists(emision.Envio.Rutas.Pdf));
        Assert.Empty(servicios.Sunat.Solicitudes);
    }

    [Fact]
    public async Task EmitirAsync_OpcionDesactivada_SinPdf()
    {
        using var servicios = Servicios(automatico: false);
        servicios.Sunat.Responder(SendBill, ConCdr(Cdr("F001-123", "0", "La Factura numero F001-123, ha sido aceptada")));

        var emision = await servicios.Obtener<IServicioCpe>().EmitirAsync(CertificadoPrueba.Emisor(servicios.Almacen), Ejemplos.Factura(), Ct);

        Assert.Equal(EstadoCpe.Aceptado, emision.Envio.Estado);
        Assert.Null(emision.Envio.Rutas.Pdf);
    }

    [Fact]
    public async Task EnviarAsync_SoloEmitirGeneraElPdf()
    {
        using var servicios = Servicios();
        var emisor = CertificadoPrueba.Emisor(servicios.Almacen);
        var cpe = servicios.Obtener<IServicioCpe>();
        var generacion = await cpe.GenerarAsync(emisor, Ejemplos.Factura(), Ct);
        servicios.Sunat.Responder(SendBill, ConCdr(Cdr("F001-123", "0", "La Factura numero F001-123, ha sido aceptada")));

        var envio = await cpe.EnviarAsync(emisor, generacion.Referencia, Ct);
        var pdf = await servicios.Obtener<IGeneradorPdf>().GenerarAsync(generacion.Referencia, MarcaEmisor.SinMarca, Ct);

        Assert.Equal(EstadoCpe.Aceptado, envio.Estado);
        Assert.Null(envio.Rutas.Pdf);
        Assert.True(pdf.Exito);
        Assert.Equal(pdf.Ruta, (await cpe.ConsultarCdrAsync(emisor, generacion.Referencia, Ct)).Rutas.Pdf);
    }
}
