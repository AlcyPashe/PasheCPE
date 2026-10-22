using PasheCPE.Catalogos;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Tributos;

namespace PasheCPE.Tests.Tributos;

public sealed class ReglasRegimenTests : IDisposable
{
    private readonly AlmacenTemporal _almacen = new();

    public void Dispose() => _almacen.Dispose();

    [Theory]
    [InlineData(TipoDocumentoSunat.BoletaDeVenta, null)]
    [InlineData(TipoDocumentoSunat.NotaDeCredito, TipoDocumentoSunat.BoletaDeVenta)]
    [InlineData(TipoDocumentoSunat.NotaDeDebito, TipoDocumentoSunat.BoletaDeVenta)]
    [InlineData(ReferenciaCpe.TipoResumenDiario, null)]
    [InlineData(TipoDocumentoSunat.GuiaDeRemisionRemitente, null)]
    [InlineData(TipoDocumentoSunat.GuiaDeRemisionTransportista, null)]
    public void ValidarTipoDocumento_NrusConDocumentoPermitido_NoDevuelveError(string tipo, string? modificado) =>
        Assert.Null(Reglas().ValidarTipoDocumento(Ejemplos.Emisor(RegimenTributario.NRUS), tipo, modificado));

    [Theory]
    [InlineData(TipoDocumentoSunat.Factura, null)]
    [InlineData(TipoDocumentoSunat.NotaDeCredito, TipoDocumentoSunat.Factura)]
    [InlineData(ReferenciaCpe.TipoComunicacionBaja, null)]
    public void ValidarTipoDocumento_NrusConDocumentoNoPermitido_DevuelveError(string tipo, string? modificado)
    {
        var error = Reglas().ValidarTipoDocumento(Ejemplos.Emisor(RegimenTributario.NRUS), tipo, modificado);

        Assert.NotNull(error);
        Assert.Equal("PCPE-1101", error.Codigo);
        Assert.Contains(tipo, error.Descripcion, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RegimenTributario.General)]
    [InlineData(RegimenTributario.RMT)]
    [InlineData(RegimenTributario.RER)]
    public void ValidarTipoDocumento_OtrosRegimenes_PermitenFactura(RegimenTributario regimen) =>
        Assert.Null(Reglas().ValidarTipoDocumento(Ejemplos.Emisor(regimen), TipoDocumentoSunat.Factura));

    [Fact]
    public void AfectacionPorDefecto_SinIndicacion_EsExoneradoAmazonia() =>
        Assert.Equal(AfectacionIgv.ExoneradoOneroso, Reglas().AfectacionPorDefecto(Ejemplos.Emisor()));

    [Fact]
    public void AfectacionPorDefecto_EmisorNoAmazonia_EsGravado() =>
        Assert.Equal(AfectacionIgv.GravadoOneroso, Reglas().AfectacionPorDefecto(Ejemplos.Emisor(amazonia: false)));

    [Fact]
    public void AfectacionPorDefecto_OpcionGlobalSinAmazonia_EsGravado() =>
        Assert.Equal(AfectacionIgv.GravadoOneroso, Reglas(o => o.ExoneradoAmazoniaPorDefecto = false).AfectacionPorDefecto(Ejemplos.Emisor()));

    [Theory]
    [InlineData(2025, 10.00)]
    [InlineData(2026, 10.50)]
    [InlineData(2027, 15.00)]
    public void TasaGravada_Mype_UsaTasaEspecialDelAnio(int anio, double tasa)
    {
        var aplicable = Reglas().TasaGravada(Ejemplos.Emisor(mype: true), new DateOnly(anio, 6, 15));

        Assert.Equal((decimal)tasa, aplicable.Tasa);
        Assert.Null(aplicable.Advertencia);
    }

    [Fact]
    public void TasaGravada_MypeSinTasaVigente_UsaTasaGeneralConAdvertencia()
    {
        var aplicable = Reglas().TasaGravada(Ejemplos.Emisor(mype: true), new DateOnly(2028, 1, 1));

        Assert.Equal(18.00m, aplicable.Tasa);
        Assert.Equal("PCPE-1102", aplicable.Advertencia?.Codigo);
    }

    [Fact]
    public void TasaGravada_SinMype_UsaTasaGeneral() =>
        Assert.Equal(18.00m, Reglas().TasaGravada(Ejemplos.Emisor(), new DateOnly(2026, 10, 22)).Tasa);

    [Theory]
    [InlineData(TipoItem.Bien, "2001")]
    [InlineData(TipoItem.Servicio, "2002")]
    [InlineData(TipoItem.Construccion, "2003")]
    public void LeyendaAmazonia_SegunTipoDeItem(TipoItem tipo, string codigo) =>
        Assert.Equal(codigo, ReglasRegimen.LeyendaAmazonia(tipo));

    private ReglasRegimen Reglas(Action<OpcionesPasheCpe>? configurar = null)
    {
        var opciones = _almacen.OpcionesDi(configurar);
        return new ReglasRegimen(new TablaTasas(opciones), new CatalogoErrores(), opciones);
    }
}
