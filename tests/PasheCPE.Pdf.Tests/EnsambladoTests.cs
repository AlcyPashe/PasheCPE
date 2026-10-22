using System.Reflection;

namespace PasheCPE.Pdf.Tests;

// Prueba de humo de la Fase 0: el ensamblado del PDF se publica con su nombre y versión.
public sealed class EnsambladoTests
{
    [Fact]
    public void Ensamblado_SeCarga_ConNombreYVersion()
    {
        var ensamblado = Assembly.Load(new AssemblyName("PasheCPE.Pdf"));

        Assert.Equal("PasheCPE.Pdf", ensamblado.GetName().Name);
        Assert.Equal(new Version(0, 1, 0, 0), ensamblado.GetName().Version);
    }
}
