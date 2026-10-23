using System.Text;
using PasheCPE.Almacenamiento;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Almacenamiento;

public sealed class RegistroHistorialTests : IDisposable
{
    private readonly AlmacenTemporal _temporal = new();
    private readonly RegistroFalso<RegistroHistorial> _log = new();
    private readonly AlmacenCpe _almacen;
    private readonly RegistroHistorial _registro;

    public RegistroHistorialTests()
    {
        _almacen = new AlmacenCpe(_temporal.OpcionesDi());
        _registro = new RegistroHistorial(_almacen, _log);
    }

    public void Dispose()
    {
        _registro.Dispose();
        _temporal.Dispose();
    }

    [Fact]
    public async Task RegistrarAsync_GuardaEnLogDelPeriodoConIntentosYTicket()
    {
        var ct = TestContext.Current.CancellationToken;
        var referencia = ReferenciaCpe.Resumen(Ejemplos.Ruc, new DateOnly(2026, 10, 23), 1);

        await _registro.RegistrarAsync(referencia, EstadoCpe.Generado, null, ct);
        await _registro.RegistrarAsync(referencia, EstadoCpe.EnProceso, Intento(EstadoCpe.EnProceso, "1729000000000"), ct);
        var historial = await _registro.ObtenerAsync(referencia, ct);

        Assert.True(File.Exists(Path.Combine(_temporal.Ruta, "LOG", "2026", "10", "20123456786-RC-20261023-1.json")));
        Assert.NotNull(historial);
        Assert.Equal(EstadoCpe.EnProceso, historial.EstadoActual);
        Assert.Equal("1729000000000", historial.Ticket);
        Assert.Single(historial.Intentos);
    }

    [Fact]
    public async Task RegistrarAsync_IntentosConcurrentes_NoSePierdeNinguno()
    {
        var ct = TestContext.Current.CancellationToken;
        var referencia = Ejemplos.Referencia();

        await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
            Task.Run(() => _registro.RegistrarAsync(referencia, EstadoCpe.ServicioNoDisponible, Intento(EstadoCpe.ServicioNoDisponible, codigo: i.ToString(System.Globalization.CultureInfo.InvariantCulture)), ct), ct)));
        var historial = await _registro.ObtenerAsync(referencia, ct);

        Assert.Equal(50, historial?.Intentos.Count);
        Assert.Equal(50, historial?.Intentos.Select(i => i.Codigo).Distinct().Count());
    }

    [Fact]
    public async Task ListarPendientesAsync_SoloDevuelveLosQueRequierenAccion()
    {
        var ct = TestContext.Current.CancellationToken;
        await _registro.RegistrarAsync(Ejemplos.Referencia(1), EstadoCpe.Aceptado, Intento(EstadoCpe.Aceptado), ct);
        await _registro.RegistrarAsync(Ejemplos.Referencia(2), EstadoCpe.RespuestaIncierta, Intento(EstadoCpe.RespuestaIncierta), ct);
        await _registro.RegistrarAsync(Ejemplos.Referencia(3), EstadoCpe.Generado, null, ct);
        await _registro.RegistrarAsync(Ejemplos.Referencia(4), EstadoCpe.Rechazado, Intento(EstadoCpe.Rechazado), ct);
        await _registro.RegistrarAsync(Ejemplos.Referencia(5, new DateOnly(2026, 11, 2)), EstadoCpe.EnProceso, null, ct);

        var pendientes = new List<HistorialCpe>();
        await foreach (var historial in new ServicioHistorial(_registro).ListarPendientesAsync(2026, 10, ct))
        {
            pendientes.Add(historial);
        }

        Assert.Equal([2L, 3L], pendientes.Select(p => p.Referencia.Numero!.Value).Order());
    }

    [Fact]
    public async Task ListarAsync_ArchivoIlegible_SeOmiteConAdvertencia()
    {
        var ct = TestContext.Current.CancellationToken;
        await _registro.RegistrarAsync(Ejemplos.Referencia(1), EstadoCpe.Generado, null, ct);
        var carpeta = _almacen.CarpetaPeriodo(CarpetaCpe.Log, 2026, 10);
        await File.WriteAllTextAsync(Path.Combine(carpeta, "dañado.json"), "{ no es json", Encoding.UTF8, ct);

        var historiales = new List<HistorialCpe>();
        await foreach (var historial in _registro.ListarAsync(2026, 10, ct))
        {
            historiales.Add(historial);
        }

        Assert.Single(historiales);
        Assert.Single(_log.Eventos);
    }

    [Fact]
    public async Task ListarAsync_PeriodoSinCarpeta_NoDevuelveNada()
    {
        var cantidad = 0;
        await foreach (var _ in _registro.ListarAsync(2020, 1, TestContext.Current.CancellationToken))
        {
            cantidad++;
        }

        Assert.Equal(0, cantidad);
    }

    private static IntentoEnvio Intento(EstadoCpe estado, string? ticket = null, string? codigo = null) => new()
    {
        Fecha = new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.FromHours(-5)),
        Operacion = "sendBill",
        Canal = CanalEnvio.ClaveSunat,
        Estado = estado,
        Ticket = ticket,
        Codigo = codigo,
    };
}
