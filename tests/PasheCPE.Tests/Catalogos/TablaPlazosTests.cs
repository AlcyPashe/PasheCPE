using PasheCPE.Catalogos;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Catalogos;

public sealed class TablaPlazosTests : IDisposable
{
    private static readonly DateOnly _emision = new(2026, 10, 22);
    private readonly AlmacenTemporal _almacen = new();

    public void Dispose() => _almacen.Dispose();

    [Theory]
    [InlineData(TipoDocumentoSunat.Factura, null, 25)]
    [InlineData(TipoDocumentoSunat.NotaDeCredito, TipoDocumentoSunat.Factura, 25)]
    [InlineData(TipoDocumentoSunat.NotaDeDebito, TipoDocumentoSunat.Factura, 25)]
    [InlineData(TipoDocumentoSunat.BoletaDeVenta, null, 29)]
    [InlineData(TipoDocumentoSunat.NotaDeCredito, TipoDocumentoSunat.BoletaDeVenta, 29)]
    [InlineData(ReferenciaCpe.TipoResumenDiario, null, 29)]
    public void FechaLimite_DiasCalendarioDesdeElDiaSiguiente(string documento, string? modificado, int dia) =>
        Assert.Equal(new DateOnly(2026, 10, dia), new TablaPlazos(_almacen.OpcionesDi()).FechaLimite(documento, modificado, _emision));

    [Theory]
    [InlineData(TipoDocumentoSunat.Factura, "F001", 25)]
    [InlineData(TipoDocumentoSunat.NotaDeCredito, "FC01", 25)]
    [InlineData(TipoDocumentoSunat.NotaDeDebito, "FD01", 25)]
    [InlineData(TipoDocumentoSunat.NotaDeCredito, "BC01", 29)]
    [InlineData(TipoDocumentoSunat.NotaDeDebito, "BD01", 29)]
    public void FechaLimite_DeLaReferencia_EnNotasDeduceElModificadoDeLaSerie(string documento, string serie, int dia)
    {
        var referencia = ReferenciaCpe.Comprobante(Ejemplos.Ruc, documento, serie, 1, _emision);

        Assert.Equal(new DateOnly(2026, 10, dia), new TablaPlazos(_almacen.OpcionesDi()).FechaLimite(referencia));
    }

    [Fact]
    public void FechaLimite_Baja_SeCuentaDesdeLaEmisionDelDocumento()
    {
        var plazos = new TablaPlazos(_almacen.OpcionesDi());

        // Regla 2957 del Excel de SUNAT (decisión del 27/10/2026): 7 días desde la emisión, no desde el CDR.
        Assert.Equal(ContarPlazoDesde.Emision, plazos.Obtener(ReferenciaCpe.TipoComunicacionBaja)?.ContarDesde);
        Assert.Equal(new DateOnly(2026, 10, 29), plazos.FechaLimite(ReferenciaCpe.TipoComunicacionBaja, null, _emision));
    }

    [Fact]
    public void FechaLimite_DocumentoSinPlazo_EsNull() =>
        Assert.Null(new TablaPlazos(_almacen.OpcionesDi()).FechaLimite(TipoDocumentoSunat.GuiaDeRemisionTransportista, null, _emision));

    /// <summary>GRE remitente: SUNAT la recibe hasta 1 día después de la emisión (2108 del Excel GRE).</summary>
    [Fact]
    public void FechaLimite_GreRemitente_UnDia() =>
        Assert.Equal(_emision.AddDays(1), new TablaPlazos(_almacen.OpcionesDi()).FechaLimite(TipoDocumentoSunat.GuiaDeRemisionRemitente, null, _emision));

    [Fact]
    public void FechaLimite_ConPlazosConfigurados_ReemplazaLosEmbebidos()
    {
        var opciones = _almacen.OpcionesDi(o => o.Plazos.Add(new PlazoEnvio { Documento = TipoDocumentoSunat.Factura, Dias = 1 }));

        Assert.Equal(new DateOnly(2026, 10, 23), new TablaPlazos(opciones).FechaLimite(TipoDocumentoSunat.Factura, null, _emision));
    }
}
