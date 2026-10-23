using PasheCPE.Conectividad;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Conectividad;

public sealed class VerificadorConectividadTests : IDisposable
{
    private const string _beta = "e-beta.sunat.gob.pe";

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IVerificadorConectividad Verificador => _servicios.Obtener<IVerificadorConectividad>();

    [Fact]
    public async Task VerificarAsync_DestinoResponde_Disponible()
    {
        var estado = await Verificador.VerificarAsync(CanalEnvio.Sunat, Ct);

        Assert.Equal((Disponibilidad.Disponible, _beta, false), (estado.Disponibilidad, estado.Destino, estado.DesdeCache));
    }

    [Fact]
    public async Task VerificarAsync_SinInterfazDeRed_SinConexionSinSondear()
    {
        _servicios.Red.HayInterfaz = false;

        var estado = await Verificador.VerificarAsync(CanalEnvio.Sunat, Ct);

        Assert.Equal(Disponibilidad.SinConexionInternet, estado.Disponibilidad);
        Assert.Equal(0, _servicios.Red.Sondeos);
    }

    [Fact]
    public async Task VerificarAsync_DestinoFallaYControlResponde_ServicioNoDisponible()
    {
        _servicios.Red.Host(_beta, responde: false);

        var estado = await Verificador.VerificarAsync(CanalEnvio.Sunat, Ct);

        Assert.Equal(Disponibilidad.ServicioNoDisponible, estado.Disponibilidad);
        Assert.Contains("www.gob.pe", estado.Detalle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerificarAsync_DestinoYControlFallan_SinConexionInternet()
    {
        _servicios.Red.Host(_beta, responde: false);
        _servicios.Red.Host("www.gob.pe", responde: false);

        var estado = await Verificador.VerificarAsync(CanalEnvio.Sunat, Ct);

        Assert.Equal(Disponibilidad.SinConexionInternet, estado.Disponibilidad);
    }

    [Fact]
    public async Task VerificarAsync_DentroDeLaCache_NoVuelveASondear()
    {
        await Verificador.VerificarAsync(CanalEnvio.Sunat, Ct);
        _servicios.Reloj.Advance(TimeSpan.FromSeconds(10));

        var estado = await Verificador.VerificarAsync(CanalEnvio.Sunat, Ct);

        Assert.True(estado.DesdeCache);
        Assert.Equal(1, _servicios.Red.Sondeos);
    }

    [Fact]
    public async Task VerificarAsync_CacheVencidaOInvalidada_VuelveASondear()
    {
        await Verificador.VerificarAsync(CanalEnvio.Sunat, Ct);
        _servicios.Reloj.Advance(TimeSpan.FromSeconds(16));
        var vencida = await Verificador.VerificarAsync(CanalEnvio.Sunat, Ct);
        _servicios.Obtener<VerificadorConectividad>().Invalidar(_beta);
        var invalidada = await Verificador.VerificarAsync(CanalEnvio.Sunat, Ct);

        Assert.False(vencida.DesdeCache);
        Assert.False(invalidada.DesdeCache);
        Assert.Equal(3, _servicios.Red.Sondeos);
    }

    [Fact]
    public async Task VerificarAsync_OseSinEndpoint_ServicioNoDisponible()
    {
        var estado = await Verificador.VerificarAsync(CanalEnvio.Ose("sin-configurar"), Ct);

        Assert.Equal(Disponibilidad.ServicioNoDisponible, estado.Disponibilidad);
        Assert.Equal(0, _servicios.Red.Sondeos);
    }
}
