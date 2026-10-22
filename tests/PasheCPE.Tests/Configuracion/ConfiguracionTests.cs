using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PasheCPE.Catalogos;
using PasheCPE.Configuracion;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Tiempo;

namespace PasheCPE.Tests.Configuracion;

public sealed class ConfiguracionTests : IDisposable
{
    private readonly AlmacenTemporal _almacen = new();

    public void Dispose() => _almacen.Dispose();

    [Fact]
    public void Validar_OpcionesPorDefectoConRuta_SonValidas() =>
        Assert.True(new ValidadorOpciones().Validate(null, _almacen.Opciones()).Succeeded);

    [Theory]
    [InlineData("RutaBase")]
    [InlineData("IdFirma")]
    [InlineData("Timeout")]
    [InlineData("Endpoint")]
    [InlineData("TramoIgv")]
    [InlineData("Esperas")]
    public void Validar_OpcionInvalida_Falla(string caso)
    {
        var opciones = _almacen.Opciones(o =>
        {
            switch (caso)
            {
                case "RutaBase": o.RutaBase = @"relativa\CPE"; break;
                case "IdFirma": o.IdFirma = "1 firma"; break;
                case "Timeout": o.TimeoutRespuestaSegundos = 0; break;
                case "Endpoint": o.Endpoints.Add(new EndpointCpe { Canal = "OSE:efact", Url = "http://ose.ejemplo.pe" }); break;
                case "TramoIgv": o.Tasas.Igv.Add(new TramoIgv { Desde = new DateOnly(2030, 1, 1), Tasa = 18m, Igv = 15m, Ipm = 2m }); break;
                case "Esperas": o.Tickets.EsperasSegundos.Add(-1); break;
            }
        });

        Assert.True(new ValidadorOpciones().Validate(null, opciones).Failed);
    }

    [Fact]
    public void AddPasheCpe_DesdeConfiguracion_EnlazaListasYFechas()
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PasheCPE:RutaBase"] = _almacen.Ruta,
            ["PasheCPE:Ambiente"] = "Produccion",
            ["PasheCPE:Endpoints:0:Canal"] = "OSE:efact",
            ["PasheCPE:Endpoints:0:Ambiente"] = "Produccion",
            ["PasheCPE:Endpoints:0:Servicio"] = "Comprobantes",
            ["PasheCPE:Endpoints:0:Url"] = "https://ose.ejemplo.pe/billService",
            ["PasheCPE:Tasas:Icbper:0:Desde"] = "2027-01-01",
            ["PasheCPE:Tasas:Icbper:0:MontoPorUnidad"] = "0.60",
        }).Build();
        using var proveedor = Servicios().AddPasheCpe(configuracion.GetSection("PasheCPE")).BuildServiceProvider();

        var opciones = proveedor.GetRequiredService<IOptions<OpcionesPasheCpe>>().Value;
        var tasas = proveedor.GetRequiredService<TablaTasas>();

        Assert.Equal(Ambiente.Produccion, opciones.Ambiente);
        Assert.Equal("https://ose.ejemplo.pe/billService", Assert.Single(opciones.Endpoints).Url);
        Assert.Equal(0.60m, tasas.Icbper(new DateOnly(2027, 6, 1)));
        Assert.NotNull(proveedor.GetRequiredService<IHistorialCpe>());
    }

    [Fact]
    public void AddPasheCpe_OpcionesInvalidas_FallanAlResolver()
    {
        using var proveedor = Servicios().AddPasheCpe(o => o.RutaBase = "relativa").BuildServiceProvider();

        var error = Assert.Throws<OptionsValidationException>(() => proveedor.GetRequiredService<IOptions<OpcionesPasheCpe>>().Value);
        Assert.Contains("RutaBase", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Crear_SinDi_UsaLosMismosServiciosQueAddPasheCpe()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var pashe = PasheCpe.Crear(_almacen.Opciones(), NullLoggerFactory.Instance, new FakeTimeProvider());

        Assert.Null(await pashe.Historial.ObtenerAsync(Ejemplos.Referencia(), ct));
        var pendientes = 0;
        await foreach (var _ in pashe.Historial.ListarPendientesAsync(2026, 10, ct))
        {
            pendientes++;
        }

        Assert.Equal(0, pendientes);
    }

    [Fact]
    public void Crear_OpcionesInvalidas_LanzaAlCrear() =>
        Assert.Throws<OptionsValidationException>(() => PasheCpe.Crear(new OpcionesPasheCpe()));

    [Fact]
    public void RelojCpe_ConvierteAHoraDeLima()
    {
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 23, 3, 30, 0, TimeSpan.Zero));
        var reloj = new RelojCpe(tiempo);

        Assert.Equal(new DateTimeOffset(2026, 10, 22, 22, 30, 0, TimeSpan.FromHours(-5)), reloj.AhoraLima());
        Assert.Equal(TimeSpan.FromHours(-5), reloj.AhoraLima().Offset);
        Assert.Equal(new DateOnly(2026, 10, 22), reloj.HoyLima());
    }

    private static ServiceCollection Servicios()
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        return servicios;
    }
}
