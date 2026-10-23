using System.Xml.Linq;
using PasheCPE.Catalogos;
using PasheCPE.Tests.Dorados;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;
using PasheCPE.Validacion.Reglas;

namespace PasheCPE.Tests.Validacion;

/// <summary>
/// Cobertura de <c>ajustes-xsl.json</c>: toda fila de 01/03/RC del Excel posterior a las XSL (06/09/2022) está clasificada,
/// cada código suprimido tiene su regla C# de reemplazo y cada regla C# respalda sus filas.
/// </summary>
public sealed class AjustesXslTests : IDisposable
{
    private static readonly DateOnly _fechaXsl = new(2022, 9, 6);

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    private AjustesXsl Ajustes => _servicios.Obtener<AjustesXsl>();

    private IReadOnlyList<IReglaCpe> Reglas => [.. _servicios.Obtener<IEnumerable<IReglaCpe>>()];

    [Fact]
    public void Clasificacion_CubreCadaFilaPosteriorALaXsl_UnaSolaVez()
    {
        var posteriores = _servicios.Obtener<ReglasExcelSunat>().Todas
            .Where(r => r.Documento is "01" or "03" or ReferenciaCpe.TipoResumenDiario && r.Vigencia > _fechaXsl)
            .Select(r => (r.Documento, r.Fila, r.Codigo))
            .Order()
            .ToList();
        var clasificadas = Ajustes.Clasificacion.Select(c => (c.Documento, c.Fila, c.Codigo)).Order().ToList();

        Assert.Equal(posteriores, clasificadas);
        Assert.All(Ajustes.Clasificacion, c => Assert.False(string.IsNullOrWhiteSpace(c.Motivo), $"{c.Documento}/{c.Fila} sin motivo"));
    }

    [Fact]
    public void Clasificacion_NuevaOReemplazada_LaImplementaSuRegla()
    {
        var reglas = Reglas.ToDictionary(r => r.Nombre, StringComparer.Ordinal);

        foreach (var clasificacion in Ajustes.Clasificacion.Where(c => c.Tratamiento is TratamientoRegla.Nueva or TratamientoRegla.Reemplazada))
        {
            Assert.True(reglas.TryGetValue(clasificacion.Regla ?? string.Empty, out var regla), $"{clasificacion.Documento}/{clasificacion.Fila}: no existe la regla '{clasificacion.Regla}'.");
            Assert.Contains(new FilaExcel(clasificacion.Documento, clasificacion.Fila), regla.Filas);
        }

        foreach (var clasificacion in Ajustes.Clasificacion.Where(c => c.Tratamiento is not (TratamientoRegla.Nueva or TratamientoRegla.Reemplazada)))
        {
            Assert.Null(clasificacion.Regla);
        }
    }

    [Fact]
    public void Reglas_CadaFilaQueImplementan_EstaClasificadaConEsaRegla()
    {
        var clasificacion = Ajustes.Clasificacion.ToDictionary(c => new FilaExcel(c.Documento, c.Fila));

        foreach (var regla in Reglas)
        {
            Assert.All(regla.Filas, fila =>
            {
                Assert.True(clasificacion.TryGetValue(fila, out var c), $"La fila {fila} de {regla.Nombre} no está clasificada.");
                Assert.Equal(regla.Nombre, c.Regla);
            });
        }
    }

    [Fact]
    public void Suprimidos_TienenReemplazoMotivoYFilasDelExcel()
    {
        var reglas = Reglas.ToDictionary(r => r.Nombre, StringComparer.Ordinal);
        var excel = _servicios.Obtener<ReglasExcelSunat>();

        Assert.NotEmpty(Ajustes.Suprimidos);
        foreach (var suprimido in Ajustes.Suprimidos)
        {
            Assert.False(string.IsNullOrWhiteSpace(suprimido.Motivo));
            var regla = reglas[suprimido.Reemplazo];
            Assert.NotEmpty(suprimido.FilasExcel);
            Assert.All(suprimido.FilasExcel, fila =>
            {
                Assert.Equal(suprimido.Codigo, excel.Obtener(suprimido.Documento, fila).Codigo);
                Assert.Contains(new FilaExcel(suprimido.Documento, fila), regla.Filas);
            });
            Assert.Contains(Ajustes.Clasificacion, c => c.Documento == suprimido.Documento && c.Codigo == suprimido.Codigo && c.Tratamiento == TratamientoRegla.Reemplazada);
        }
    }

    [Fact]
    public void Suprimidos_ElCodigoExisteActivoEnLaXsl()
    {
        foreach (var suprimido in Ajustes.Suprimidos)
        {
            var archivo = MotorXslSunat.XslPorDocumento[suprimido.Documento];
            var xsl = XDocument.Load(Path.Combine(LectorDorado.Raiz(), "recursos", "XSL", archivo));
            var activo = xsl.Descendants().Any(e => e.Name.LocalName == "with-param" && (string?)e.Attribute("select") == $"'{suprimido.Codigo}'");

            Assert.True(activo, $"El código {suprimido.Codigo} ya no está activo en {archivo}: revise ajustes-xsl.json.");
        }
    }
}
