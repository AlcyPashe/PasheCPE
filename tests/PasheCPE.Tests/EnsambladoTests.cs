using System.Reflection;

namespace PasheCPE.Tests;

// Prueba de humo de la Fase 0: el ensamblado principal se publica con su nombre y versión.
// La versión viene del tag en publicar.yml (-p:Version, que se aplica a toda la solución): se compara con la de
// este ensamblado de pruebas en lugar de fijar un número.
public sealed class EnsambladoTests
{
    [Fact]
    public void Ensamblado_SeCarga_ConNombreYVersion()
    {
        var ensamblado = Assembly.Load(new AssemblyName("PasheCPE"));

        Assert.Equal("PasheCPE", ensamblado.GetName().Name);
        Assert.Equal(typeof(EnsambladoTests).Assembly.GetName().Version, ensamblado.GetName().Version);
    }
}
