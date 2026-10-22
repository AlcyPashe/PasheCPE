using PasheCPE.Catalogos;

namespace PasheCPE.Tests.Catalogos;

public sealed class CatalogoErroresTests
{
    private readonly CatalogoErrores _errores = new();

    [Fact]
    public void Buscar_PerfilNoAutorizado_EsServicioNoTransitorio()
    {
        var error = _errores.Buscar("0111");

        Assert.NotNull(error);
        Assert.Equal("servicio", error.Tipo);
        Assert.False(error.Transitorio);
        Assert.Contains("perfil", error.Descripcion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Buscar_SistemaNoResponde_EsTransitorio() =>
        Assert.True(_errores.Buscar("0100")?.Transitorio);

    [Fact]
    public void Buscar_CodigoSinCeros_LoCompleta() =>
        Assert.Equal("0111", _errores.Buscar("111")?.Codigo);

    [Theory]
    [InlineData("2017", "rechazo")]
    [InlineData("3205", "rechazo")]
    [InlineData("4252", "observacion")]
    [InlineData("1033", "servicio")]
    public void Buscar_TipoSegunRango(string codigo, string tipo) =>
        Assert.Equal(tipo, _errores.Buscar(codigo)?.Tipo);

    [Fact]
    public void Buscar_CodigoInexistente_EsNull() =>
        Assert.Null(_errores.Buscar("9999"));

    [Fact]
    public void Local_FormateaElMensaje()
    {
        var error = _errores.Local("PCPE-1101", OrigenError.Modelo, "TipoDocumento", "01", "01");

        Assert.Equal(OrigenError.Modelo, error.Origen);
        Assert.EndsWith("no el tipo de documento 01.", error.Descripcion, StringComparison.Ordinal);
    }
}
