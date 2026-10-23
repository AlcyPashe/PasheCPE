using PasheCPE.Respuestas;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Transporte;
using PasheCPE.Transporte.Soap;

namespace PasheCPE.Tests.Respuestas;

/// <summary>Tabla de clasificación del TRD §5.1.</summary>
public sealed class ClasificadorRespuestaTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    private ClasificadorRespuesta Clasificador => _servicios.Obtener<ClasificadorRespuesta>();

    private static ContextoEnvio Contexto => new(Ejemplos.Referencia(), OperacionSoap.SendBill, RutasCpe.Vacio);

    private static RespuestaSoap Soap(int? http = 500, FallaRed falla = FallaRed.Ninguna) =>
        new(OperacionSoap.SendBill, "https://e-beta.sunat.gob.pe/ol-ti-itcpfegem-beta/billService", http, null, falla, 120, null);

    [Theory]
    [InlineData("0100", EstadoCpe.ServicioNoDisponible, true, false)]
    [InlineData("0109", EstadoCpe.ServicioNoDisponible, true, false)]
    [InlineData("0138", EstadoCpe.ServicioNoDisponible, true, false)]
    [InlineData("0125", EstadoCpe.ServicioNoDisponible, true, true)]
    [InlineData("0130", EstadoCpe.ServicioNoDisponible, true, true)]
    [InlineData("0307", EstadoCpe.ServicioNoDisponible, true, true)]
    [InlineData("0135", EstadoCpe.ServicioNoDisponible, true, true)]
    [InlineData("0140", EstadoCpe.EnProceso, false, true)]
    [InlineData("0250", EstadoCpe.ErrorServicio, false, false)]
    [InlineData("0102", EstadoCpe.ErrorServicio, false, false)]
    [InlineData("0151", EstadoCpe.ErrorServicio, false, false)]
    [InlineData("1033", EstadoCpe.ErrorServicio, false, true)]
    [InlineData("2335", EstadoCpe.Rechazado, false, false)]
    [InlineData("3105", EstadoCpe.Rechazado, false, false)]
    public void DesdeFault_SegunElCodigo(string codigo, EstadoCpe estado, bool reintentable, bool consultar)
    {
        var fault = _servicios.Obtener<LectorRespuestaSoap>().Fault($"soap-env:Client.{codigo}", "texto", null);

        var resultado = Clasificador.DesdeFault(Contexto, Soap(), fault);

        Assert.Equal((estado, reintentable, consultar), (resultado.Estado, resultado.EsReintentable, resultado.RequiereConsultaEstado));
        Assert.Equal(codigo, resultado.Codigo);
        Assert.False(string.IsNullOrEmpty(resultado.MensajeUsuario));
    }

    [Theory]
    [InlineData("0", 0, EstadoCpe.Aceptado)]
    [InlineData("0", 1, EstadoCpe.AceptadoConObservaciones)]
    [InlineData("4252", 0, EstadoCpe.AceptadoConObservaciones)]
    [InlineData("2800", 0, EstadoCpe.Rechazado)]
    [InlineData("99", 0, EstadoCpe.Rechazado)]
    [InlineData("98", 0, EstadoCpe.EnProceso)]
    [InlineData("0151", 0, EstadoCpe.ErrorServicio)]
    public void DesdeCdr_SegunCodigoYNotas(string codigo, int notas, EstadoCpe estado)
    {
        var cdr = new CdrLeido(codigo, "texto", "F001-123", null, null, [.. Enumerable.Repeat(new ObservacionCpe { Codigo = "4252", Descripcion = "obs" }, notas)], null, null);

        Assert.Equal(estado, Clasificador.DesdeCdr(Contexto, Soap(200), cdr).Estado);
    }

    [Fact]
    public void DesdeCdr_FirmaNoValida_AgregaObservacion4009()
    {
        var cdr = new CdrLeido("0", "Aceptada", "F001-123", null, null, [], null, FirmaValida: false);

        var resultado = Clasificador.DesdeCdr(Contexto, Soap(200), cdr);

        Assert.Equal("PCPE-4009", Assert.Single(resultado.Observaciones).Codigo);
    }

    [Theory]
    // falla: 0 ninguna (hubo HTTP), 1 sin conexión (no se envió), 2 sin respuesta (se envió).
    [InlineData(null, 1, false, EstadoCpe.ServicioNoDisponible)]
    [InlineData(null, 2, false, EstadoCpe.RespuestaIncierta)]
    [InlineData(null, 2, true, EstadoCpe.ServicioNoDisponible)]
    [InlineData(503, 0, false, EstadoCpe.ServicioNoDisponible)]
    [InlineData(200, 0, false, EstadoCpe.RespuestaIncierta)]
    [InlineData(404, 0, false, EstadoCpe.ErrorServicio)]
    public void DesdeRed_SegunFallaYOperacion(int? http, int falla, bool idempotente, EstadoCpe estado)
    {
        var resultado = Clasificador.DesdeRed(Contexto, Soap(http, (FallaRed)falla), idempotente);

        Assert.Equal(estado, resultado.Estado);
        Assert.Equal(estado == EstadoCpe.ServicioNoDisponible, resultado.EsReintentable);
        Assert.Equal(estado == EstadoCpe.RespuestaIncierta, resultado.RequiereConsultaEstado);
    }

    [Fact]
    public void Resultado_FechaLimiteYDetalleTecnico()
    {
        var resultado = Clasificador.DesdeRed(Contexto, Soap(503), idempotente: false);

        Assert.Equal(new DateOnly(2026, 10, 25), resultado.FechaLimiteEnvio);
        Assert.Equal((OperacionSoap.SendBill, 503, 120L), (resultado.Tecnico!.Operacion, resultado.Tecnico.Http, resultado.Tecnico.DuracionMs));
    }

    [Fact]
    public async Task BloqueoDocumentos_SerializaPorDocumentoYLiberaLaMemoria()
    {
        var bloqueos = new BloqueoDocumentos();
        var primero = await bloqueos.AdquirirAsync("A", TestContext.Current.CancellationToken);
        var otroDocumento = bloqueos.AdquirirAsync("B", TestContext.Current.CancellationToken);
        var mismoDocumento = bloqueos.AdquirirAsync("a", TestContext.Current.CancellationToken);

        Assert.True(otroDocumento.IsCompletedSuccessfully);
        Assert.False(mismoDocumento.IsCompleted);
        primero.Dispose();
        (await mismoDocumento).Dispose();
        (await otroDocumento).Dispose();
        Assert.Equal(0, bloqueos.Activos);
    }
}
