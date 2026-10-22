using System.Reflection;

namespace PasheCPE.Tests;

// Prueba de humo de la Fase 0: el ensamblado principal se publica con su nombre y versión.
public sealed class EnsambladoTests
{
    [Fact]
    public void Ensamblado_SeCarga_ConNombreYVersion()
    {
        var ensamblado = Assembly.Load(new AssemblyName("PasheCPE"));

        Assert.Equal("PasheCPE", ensamblado.GetName().Name);
        Assert.Equal(new Version(0, 1, 0, 0), ensamblado.GetName().Version);
    }
}
