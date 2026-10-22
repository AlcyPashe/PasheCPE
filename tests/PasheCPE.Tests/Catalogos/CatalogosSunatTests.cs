using System.Reflection;
using PasheCPE.Catalogos;

namespace PasheCPE.Tests.Catalogos;

public sealed class CatalogosSunatTests
{
    private readonly CatalogosSunat _catalogos = new(new CatalogoErrores());

    [Fact]
    public void Validar_CodigoVigente_NoDevuelveError() =>
        Assert.Null(_catalogos.Validar(AfectacionIgv.NumeroCatalogo, AfectacionIgv.ExoneradoOneroso, new DateOnly(2026, 10, 22)));

    [Fact]
    public void Validar_CodigoInexistente_IndicaElNumeroDeCatalogo()
    {
        var error = _catalogos.Validar(MotivoNotaDebito.NumeroCatalogo, "99", new DateOnly(2026, 10, 22), "Motivo");

        Assert.NotNull(error);
        Assert.Equal("PCPE-1001", error.Codigo);
        Assert.Equal("10", error.Catalogo);
        Assert.Equal("Motivo", error.Ruta);
        Assert.Contains("catálogo N.° 10", error.Descripcion, StringComparison.Ordinal);
    }

    [Fact]
    public void Validar_CodigoQueEntraEn2027_EsInvalidoEn2026YValidoEn2027()
    {
        var antes = _catalogos.Validar(MotivoNotaDebito.NumeroCatalogo, MotivoNotaDebito.Penalidades, new DateOnly(2026, 12, 31));

        Assert.Equal("PCPE-1002", antes?.Codigo);
        Assert.Null(_catalogos.Validar(MotivoNotaDebito.NumeroCatalogo, MotivoNotaDebito.Penalidades, new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void Validar_CodigoQueSaleEn2027_EsValidoEn2026EInvalidoEn2027()
    {
        Assert.Null(_catalogos.Validar("11", "05", new DateOnly(2026, 12, 31)));
        Assert.Equal("PCPE-1002", _catalogos.Validar("11", "05", new DateOnly(2027, 1, 1))?.Codigo);
    }

    [Fact]
    public void Validar_CatalogoRetirado_EsValidoAntesEInvalidoDespues()
    {
        var codigo = PrimerCodigo("25.2");

        Assert.Null(_catalogos.Validar("25.2", codigo, new DateOnly(2026, 7, 31)));
        Assert.Equal("PCPE-1002", _catalogos.Validar("25.2", codigo, new DateOnly(2026, 8, 1))?.Codigo);
    }

    [Theory]
    [InlineData("02", "PEN", true)]
    [InlineData("02", "PE", false)]
    [InlineData("04", "PE", true)]
    [InlineData("13", "160101", true)]
    [InlineData("13", "1601", false)]
    [InlineData("25", "50221200", true)]
    [InlineData("25", "5022120", false)]
    public void Validar_CatalogoSoloFormato_ValidaPorExpresionRegular(string catalogo, string codigo, bool valido)
    {
        var error = _catalogos.Validar(catalogo, codigo, new DateOnly(2026, 10, 22));

        Assert.Equal(valido, error is null);
        if (!valido)
        {
            Assert.Equal("PCPE-1003", error?.Codigo);
        }
    }

    [Fact]
    public void Validar_CodigoVacio_DevuelveError() =>
        Assert.Equal("PCPE-1004", _catalogos.Validar(TipoDocumentoIdentidad.NumeroCatalogo, " ", new DateOnly(2026, 10, 22))?.Codigo);

    [Fact]
    public void Validar_CatalogoInexistente_Lanza() =>
        Assert.Throws<ArgumentException>(() => _catalogos.Validar("99", "01", new DateOnly(2026, 10, 22)));

    [Fact]
    public void Descripcion_DevuelveLaOficial() =>
        Assert.Equal("Factura", _catalogos.Descripcion(TipoDocumentoSunat.NumeroCatalogo, TipoDocumentoSunat.Factura));

    [Fact]
    public void ConstantesGeneradas_ExistenEnSuCatalogo()
    {
        var clases = typeof(OpcionesPasheCpe).Assembly.GetExportedTypes()
            .Select(t => (Tipo: t, Numero: t.GetField("NumeroCatalogo", BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() as string))
            .Where(c => c.Numero is not null)
            .ToList();

        Assert.Equal(15, clases.Count);
        foreach (var (tipo, numero) in clases)
        {
            var codigos = tipo.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.Name != "NumeroCatalogo")
                .Select(f => (string)f.GetRawConstantValue()!);
            Assert.All(codigos, codigo => Assert.True(_catalogos.Existe(numero!, codigo), $"{tipo.Name}: {codigo} no existe en el catálogo {numero}."));
        }
    }

    private static string PrimerCodigo(string catalogo)
    {
        using var flujo = RecursosEmbebidos.Abrir("catalogos.json");
        using var json = System.Text.Json.JsonDocument.Parse(flujo);
        return json.RootElement.GetProperty("catalogos").GetProperty(catalogo).GetProperty("items")[0].GetProperty("codigo").GetString()!;
    }
}
