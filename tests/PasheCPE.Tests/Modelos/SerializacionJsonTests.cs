using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PasheCPE.Tests.Baja;
using PasheCPE.Tests.Gre;
using PasheCPE.Tests.Utilidades;
using static PasheCPE.Tests.Utilidades.RespuestasSoap;

namespace PasheCPE.Tests.Modelos;

/// <summary>
/// RNF-11: cada modelo de entrada y cada resultado se guarda y se recupera con <see cref="PasheCpeJson"/> sin perder
/// datos. La prueba serializa, lee y vuelve a serializar: el segundo JSON debe ser idéntico al primero.
/// </summary>
public sealed class SerializacionJsonTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Modelos_IdaYVuelta_SinPerdida()
    {
        IdaYVuelta(Ejemplos.Factura(), PasheCpeJson.Default.ComprobanteCpe);
        foreach (var caso in CasosNotas.Todos.Values)
        {
            IdaYVuelta(caso.Nota, PasheCpeJson.Default.NotaCpe);
        }

        IdaYVuelta(new ResumenDiarioCpe { FechaReferencia = GeneracionBajaTests.Fecha, Lineas = [LineaResumen(1), LineaResumen(2) with { Estado = EstadoItemResumen.Anulado }] }, PasheCpeJson.Default.ResumenDiarioCpe);
        IdaYVuelta(GeneracionBajaTests.Comunicacion(GeneracionBajaTests.Documento(1), GeneracionBajaTests.Documento(2)), PasheCpeJson.Default.ComunicacionBajaCpe);
        IdaYVuelta(CasosGre.Dorada(), PasheCpeJson.Default.GuiaRemisionCpe);
        IdaYVuelta(CasosGre.Privada(), PasheCpeJson.Default.GuiaRemisionCpe);
        IdaYVuelta(Ejemplos.Referencia(), PasheCpeJson.Default.ReferenciaCpe);
    }

    [Fact]
    public async Task Resultados_DeUnaEmisionReal_IdaYVueltaSinPerdida()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        _servicios.Sunat.Responder(SendBill, ConCdr(Cdr("F001-123", "0", "La Factura numero F001-123, ha sido aceptada", "4252 - El dato ingresado como atributo @listName es incorrecto.")));
        var servicio = _servicios.Obtener<IServicioCpe>();

        var emision = await servicio.EmitirAsync(emisor, Ejemplos.Factura(), Ct);
        var validacion = await servicio.ValidarAsync(emisor, Ejemplos.Factura() with { Serie = "B001" }, Ct);
        var historial = await _servicios.Obtener<IHistorialCpe>().ObtenerAsync(emision.Generacion.Referencia, Ct);

        Assert.Equal(EstadoCpe.AceptadoConObservaciones, emision.Envio.Estado);
        Assert.NotEmpty(validacion.Errores);
        var json = IdaYVuelta(emision, PasheCpeJson.Default.ResultadoEmision);
        Assert.Contains("\"estado\":\"AceptadoConObservaciones\"", json, StringComparison.Ordinal);
        IdaYVuelta(emision.Generacion, PasheCpeJson.Default.ResultadoGeneracion);
        IdaYVuelta(emision.Envio, PasheCpeJson.Default.ResultadoEnvio);
        IdaYVuelta(validacion, PasheCpeJson.Default.ResultadoValidacion);
        IdaYVuelta(historial!, PasheCpeJson.Default.HistorialCpe);
        IdaYVuelta(await _servicios.Obtener<IVerificadorConectividad>().VerificarAsync(emisor.Canal, Ct), PasheCpeJson.Default.EstadoConectividad);
    }

    private static string IdaYVuelta<T>(T valor, JsonTypeInfo<T> tipo)
    {
        var json = JsonSerializer.Serialize(valor, tipo);
        var leido = JsonSerializer.Deserialize(json, tipo);

        Assert.NotNull(leido);
        Assert.Equal(json, JsonSerializer.Serialize(leido, tipo));
        return json;
    }

    private static LineaResumenCpe LineaResumen(long numero) => new()
    {
        TipoDocumento = TipoDocumentoSunat.BoletaDeVenta,
        Serie = "B001",
        Numero = numero,
        Estado = EstadoItemResumen.Adicionar,
        Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Dni, NumeroDocumento = "12345678", RazonSocial = "CLIENTE DE PRUEBA" },
        TotalGravado = 100m,
        Igv = 18m,
        ImporteTotal = 118m,
    };
}
