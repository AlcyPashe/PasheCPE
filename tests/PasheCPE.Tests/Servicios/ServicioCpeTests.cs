using System.IO.Compression;
using Microsoft.Extensions.Time.Testing;
using PasheCPE.Almacenamiento;
using PasheCPE.Firma;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Servicios;

public sealed class ServicioCpeTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    private IServicioCpe Servicio => _servicios.Obtener<IServicioCpe>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GenerarAsync_FacturaValida_DejaLosArchivosEnElPeriodoYElHistorial()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);

        var resultado = await Servicio.GenerarAsync(emisor, Ejemplos.Factura(), Ct);

        Assert.True(resultado.Exito, string.Join(Environment.NewLine, resultado.Errores.Select(e => $"{e.Codigo}: {e.Descripcion}")));
        string Ruta(string carpeta, string extension) => Path.Combine(_servicios.Almacen.Ruta, carpeta, "2026", "10", "20123456786-01-F001-123" + extension);
        Assert.Equal(Ruta("DATA", ".xml"), resultado.Rutas.Xml);
        Assert.Equal(Ruta("FIRMA", ".xml"), resultado.Rutas.XmlFirmado);
        Assert.Equal(Ruta("ENVIO", ".zip"), resultado.Rutas.Zip);
        Assert.Equal(Ruta("QR", ".png"), resultado.Rutas.Qr);
        Assert.Equal(Ruta("LOG", ".json"), resultado.Rutas.Historial);
        Assert.All(new[] { resultado.Rutas.Xml, resultado.Rutas.XmlFirmado, resultado.Rutas.Zip, resultado.Rutas.Qr, resultado.Rutas.Historial }, r => Assert.True(File.Exists(r), r));

        var firmado = await File.ReadAllBytesAsync(resultado.Rutas.XmlFirmado!, Ct);
        var certificado = _servicios.Obtener<ProveedorCertificados>().Obtener(emisor.Certificado).Certificado!;
        Assert.True(FirmadorXml.Verificar(firmado, certificado));
        using (var zip = ZipFile.OpenRead(resultado.Rutas.Zip!))
        {
            var entrada = Assert.Single(zip.Entries);
            Assert.Equal("20123456786-01-F001-123.xml", entrada.FullName);
            using var flujo = entrada.Open();
            using var copia = new MemoryStream();
            await flujo.CopyToAsync(copia, Ct);
            Assert.Equal(firmado, copia.ToArray());
        }

        Assert.False(string.IsNullOrEmpty(resultado.ValorResumen));
        Assert.Equal($"20123456786|01|F001|123|1.53|61.00|2026-10-22|6|20987654326|{resultado.ValorResumen}", resultado.ContenidoQr);
        Assert.Equal(new DateOnly(2026, 10, 25), resultado.FechaLimiteEnvio);
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.FromHours(-5)), resultado.FechaProceso);

        var historial = await _servicios.Obtener<IHistorialCpe>().ObtenerAsync(resultado.Referencia, Ct);
        Assert.Equal(EstadoCpe.Generado, historial!.EstadoActual);
        var intento = Assert.Single(historial.Intentos);
        Assert.Equal(("generar", "SUNAT", EstadoCpe.Generado), (intento.Operacion, intento.Canal, intento.Estado));
    }

    [Fact]
    public async Task GenerarAsync_ErrorDeValidacion_NoFirmaNiRegistraHistorial()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);

        var resultado = await Servicio.GenerarAsync(emisor, Ejemplos.Factura() with { Serie = "B001" }, Ct);

        Assert.Contains(resultado.Errores, e => e.Codigo == "PCPE-1204");
        Assert.Null(resultado.Rutas.Xml);
        Assert.Null(resultado.ValorResumen);
        foreach (var carpeta in new[] { "DATA", "FIRMA", "ENVIO", "QR", "LOG" })
        {
            Assert.False(Directory.Exists(Path.Combine(_servicios.Almacen.Ruta, carpeta)), carpeta);
        }
    }

    [Fact]
    public async Task GenerarAsync_CertificadoInexistente_ErrorDeFirmaSinExcepcion()
    {
        var resultado = await Servicio.GenerarAsync(Ejemplos.Emisor(), Ejemplos.Factura(), Ct);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal(("PCPE-3001", OrigenError.Firma), (error.Codigo, error.Origen));
        Assert.True(File.Exists(resultado.Rutas.Xml));
        Assert.Null(resultado.Rutas.XmlFirmado);
        Assert.False(Directory.Exists(Path.Combine(_servicios.Almacen.Ruta, "FIRMA")));
        Assert.Null(await _servicios.Obtener<IHistorialCpe>().ObtenerAsync(resultado.Referencia, Ct));
    }

    [Fact]
    public async Task GenerarAsync_CertificadoPorVencer_AdvierteYGenera()
    {
        var emisor = Ejemplos.Emisor() with { Certificado = CertificadoPrueba.Crear(_servicios.Almacen, hasta: new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero)) };

        var resultado = await Servicio.GenerarAsync(emisor, Ejemplos.Factura(), Ct);

        Assert.True(resultado.Exito);
        Assert.Contains(resultado.Advertencias, a => a.Codigo == "PCPE-3005");
    }

    [Fact]
    public async Task GenerarAsync_DocumentoSoloGenerado_SePuedeRegenerar()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        await Servicio.GenerarAsync(emisor, Ejemplos.Factura(), Ct);

        var segundo = await Servicio.GenerarAsync(emisor, Ejemplos.Factura(), Ct);

        Assert.True(segundo.Exito);
        var historial = await _servicios.Obtener<IHistorialCpe>().ObtenerAsync(segundo.Referencia, Ct);
        Assert.Equal(2, historial!.Intentos.Count);
    }

    [Theory]
    [InlineData(EstadoCpe.Aceptado)]
    [InlineData(EstadoCpe.AceptadoConObservaciones)]
    [InlineData(EstadoCpe.Rechazado)]
    [InlineData(EstadoCpe.EnProceso)]
    [InlineData(EstadoCpe.RespuestaIncierta)]
    [InlineData(EstadoCpe.ServicioNoDisponible)]
    public async Task GenerarAsync_DocumentoYaEnviado_NoSeRegenera(EstadoCpe estado)
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        var primero = await Servicio.GenerarAsync(emisor, Ejemplos.Factura(), Ct);
        await _servicios.Obtener<RegistroHistorial>().RegistrarAsync(primero.Referencia, estado, null, Ct);
        var firmadoAntes = await File.ReadAllBytesAsync(primero.Rutas.XmlFirmado!, Ct);

        var segundo = await Servicio.GenerarAsync(emisor, Ejemplos.Factura() with { Observaciones = "CAMBIO" }, Ct);

        var error = Assert.Single(segundo.Errores);
        Assert.Equal("PCPE-3101", error.Codigo);
        Assert.Contains(estado.ToString(), error.Descripcion, StringComparison.Ordinal);
        Assert.Equal(firmadoAntes, await File.ReadAllBytesAsync(primero.Rutas.XmlFirmado!, Ct));
    }

    [Fact]
    public async Task ValidarAsync_NoEscribeArchivos()
    {
        var valido = await Servicio.ValidarAsync(Ejemplos.Emisor(), Ejemplos.Factura(), Ct);
        var invalido = await Servicio.ValidarAsync(Ejemplos.Emisor(), Ejemplos.Factura() with { Serie = "B001" }, Ct);

        Assert.True(valido.EsValido);
        Assert.False(invalido.EsValido);
        Assert.Contains(invalido.Errores, e => e.Codigo == "PCPE-1204");
        Assert.Empty(Directory.EnumerateFileSystemEntries(_servicios.Almacen.Ruta));
    }

    [Fact]
    public async Task PasheCpeCrear_ExponeCpe()
    {
        using var almacen = new AlmacenTemporal();
        var emisor = CertificadoPrueba.Emisor(almacen);
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 10, 22, 15, 0, 0, TimeSpan.Zero));
        using var pashe = PasheCpe.Crear(almacen.Opciones(o => o.PrecalentarAlIniciar = false), tiempo: reloj);

        var resultado = await pashe.Cpe.GenerarAsync(emisor, Ejemplos.Factura(), Ct);

        Assert.True(resultado.Exito);
        Assert.True(File.Exists(resultado.Rutas.Zip));
    }
}
