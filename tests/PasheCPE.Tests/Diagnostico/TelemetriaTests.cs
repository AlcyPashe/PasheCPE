using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using PasheCPE.Diagnostico;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Diagnostico;

/// <summary>
/// TRD §21: actividades <c>generar</c>, <c>validar</c> y <c>firmar</c> y la métrica <c>pashecpe.validacion.errores</c>.
/// Los escuchas son globales y las demás pruebas corren en paralelo: se filtra por un número de documento propio y la
/// métrica se comprueba como mínimo.
/// </summary>
public sealed class TelemetriaTests : IDisposable
{
    private const long Numero = 987654;

    private readonly ServiciosPrueba _servicios = new();
    private readonly ConcurrentBag<Activity> _actividades = [];
    private readonly ConcurrentBag<(long Valor, string? Tipo, string? Origen)> _errores = [];
    private readonly ActivityListener _escucha;
    private readonly MeterListener _medidor = new();

    public TelemetriaTests()
    {
        _escucha = new ActivityListener
        {
            ShouldListenTo = fuente => fuente.Name == Telemetria.Nombre,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _actividades.Add,
        };
        ActivitySource.AddActivityListener(_escucha);
        _medidor.InstrumentPublished = (instrumento, escucha) =>
        {
            if (instrumento.Meter.Name == Telemetria.Nombre && instrumento.Name == "pashecpe.validacion.errores")
            {
                escucha.EnableMeasurementEvents(instrumento);
            }
        };
        _medidor.SetMeasurementEventCallback<long>((_, valor, etiquetas, _) =>
        {
            string? tipo = null, origen = null;
            foreach (var etiqueta in etiquetas)
            {
                tipo = etiqueta.Key == "tipoDocumento" ? etiqueta.Value as string : tipo;
                origen = etiqueta.Key == "origen" ? etiqueta.Value as string : origen;
            }

            _errores.Add((valor, tipo, origen));
        });
        _medidor.Start();
    }

    public void Dispose()
    {
        _escucha.Dispose();
        _medidor.Dispose();
        _servicios.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GenerarAsync_ActividadesGenerarValidarYFirmarAnidadas()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);

        var generacion = await _servicios.Obtener<IServicioCpe>().GenerarAsync(emisor, Ejemplos.Factura() with { Numero = Numero }, Ct);

        Assert.True(generacion.Exito);
        var documento = generacion.Referencia.NombreArchivo;
        var generar = Assert.Single(_actividades, a => a.OperationName == Telemetria.Generar && Documento(a) == documento);
        var validar = Assert.Single(_actividades, a => a.OperationName == Telemetria.Validar && Documento(a) == documento);
        var firmar = Assert.Single(_actividades, a => a.OperationName == Telemetria.Firmar && a.Parent == generar);
        Assert.Same(generar, validar.Parent);
        Assert.All([generar, validar, firmar], a => Assert.Equal(ActivityStatusCode.Unset, a.Status));
        Assert.Equal(0, generar.GetTagItem(Telemetria.EtiquetaErrores));
    }

    [Fact]
    public async Task ValidarAsync_ConErrores_MetricaPorOrigen()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);

        var validacion = await _servicios.Obtener<IServicioCpe>().ValidarAsync(emisor, Ejemplos.Factura() with { Serie = "B001", Numero = Numero }, Ct);

        var modelo = validacion.Errores.Count(e => e.Origen == OrigenError.Modelo);
        Assert.True(modelo > 0);
        Assert.True(_errores.Where(e => e is { Tipo: TipoDocumentoSunat.Factura, Origen: nameof(OrigenError.Modelo) }).Sum(e => e.Valor) >= modelo);
    }

    [Fact]
    public async Task GenerarAsync_ConErrores_ActividadEnError()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);

        var generacion = await _servicios.Obtener<IServicioCpe>().GenerarAsync(emisor, Ejemplos.Factura() with { Serie = "B001", Numero = Numero + 1 }, Ct);

        Assert.False(generacion.Exito);
        var generar = Assert.Single(_actividades, a => a.OperationName == Telemetria.Generar && Documento(a) == generacion.Referencia.NombreArchivo);
        Assert.Equal(ActivityStatusCode.Error, generar.Status);
        Assert.Equal(generacion.Errores.Count, generar.GetTagItem(Telemetria.EtiquetaErrores));
    }

    private static string? Documento(Activity actividad) => actividad.GetTagItem(Telemetria.EtiquetaDocumento) as string;
}
