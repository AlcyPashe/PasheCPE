using System.Text.Json;
using PasheCPE.Tests.Baja;
using PasheCPE.Tests.Gre;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Transporte;
using static PasheCPE.Tests.Utilidades.RespuestasSoap;
using Api = PasheCPE.Tests.Utilidades.RespuestasGre;

namespace PasheCPE.Tests.Concurrencia;

/// <summary>
/// TRD §20 y §24: los servicios son singleton y atienden a muchos cajeros a la vez. Emisiones, envíos repetidos,
/// resúmenes, bajas y guías en paralelo contra el SUNAT falso: sin colisiones de archivos, historiales ni correlativos,
/// y sin llamadas de más a SUNAT.
/// </summary>
public sealed class ConcurrenciaTests : IDisposable
{
    private const int Emisiones = 50;

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServicioCpe Servicio => _servicios.Obtener<IServicioCpe>();

    [Fact]
    public async Task EmitirAsync_50FacturasEnParalelo_CadaUnaConSusArchivosYSuHistorial()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        for (var i = 0; i < Emisiones; i++)
        {
            _servicios.Sunat.Responder(SendBill, ConCdr(CdrDoradoAceptado()));
        }

        // Dos series para que también se crucen las carpetas y los nombres de distintas series.
        var facturas = Enumerable.Range(1, Emisiones).Select(n => Ejemplos.Factura() with { Serie = n % 2 == 0 ? "F001" : "F002", Numero = n });
        var emisiones = await Task.WhenAll(facturas.Select(f => Task.Run(() => Servicio.EmitirAsync(emisor, f, Ct), Ct)));

        Assert.All(emisiones, e => Assert.Equal(EstadoCpe.Aceptado, e.Envio.Estado));
        Assert.Equal(Emisiones, _servicios.Sunat.Llamadas(SendBill));
        Assert.Equal(Emisiones, emisiones.Select(e => e.Generacion.Referencia.NombreArchivo).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Emisiones, emisiones.Select(e => e.Generacion.ValorResumen).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Emisiones, _servicios.Sunat.Solicitudes.Select(s => NombreZip(s.Cuerpo)).Distinct(StringComparer.Ordinal).Count());

        var historiales = _servicios.Obtener<IHistorialCpe>();
        foreach (var emision in emisiones)
        {
            var rutas = emision.Envio.Rutas;
            Assert.All([rutas.Xml, rutas.XmlFirmado, rutas.Zip, rutas.Qr, rutas.Cdr, rutas.Historial], r => Assert.True(File.Exists(r), r));
            Assert.Contains(emision.Generacion.Referencia.NombreArchivo, rutas.XmlFirmado, StringComparison.Ordinal);
            var historial = await historiales.ObtenerAsync(emision.Generacion.Referencia, Ct);
            Assert.Equal(EstadoCpe.Aceptado, historial!.EstadoActual);
            Assert.Equal(["generar", "sendBill"], historial.Intentos.Select(i => i.Operacion));
        }

        // Ningún temporal de las escrituras atómicas quedó suelto.
        Assert.Empty(Directory.GetFiles(_servicios.Almacen.Ruta, "*.tmp", SearchOption.AllDirectories));
        Assert.Equal(0, _servicios.Obtener<BloqueoDocumentos>().Activos);
    }

    [Fact]
    public async Task EnviarAsync_50VecesElMismoDocumento_UnSoloSendBill()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        var generacion = await Servicio.GenerarAsync(emisor, Ejemplos.Factura(), Ct);
        var liberar = new TaskCompletionSource();
        _servicios.Sunat.ResponderTras(SendBill, liberar.Task, ConCdr(CdrDoradoAceptado()));

        var envios = Enumerable.Range(0, Emisiones).Select(_ => Task.Run(() => Servicio.EnviarAsync(emisor, generacion.Referencia, Ct), Ct)).ToList();
        await Task.Delay(50, Ct);
        liberar.SetResult();
        var resultados = await Task.WhenAll(envios);

        Assert.All(resultados, r => Assert.Equal(EstadoCpe.Aceptado, r.Estado));
        Assert.Equal(1, _servicios.Sunat.Llamadas(SendBill));
        var historial = await _servicios.Obtener<IHistorialCpe>().ObtenerAsync(generacion.Referencia, Ct);
        Assert.Equal(1, historial!.Intentos.Count(i => i.Operacion == "sendBill"));
        Assert.Equal(0, _servicios.Obtener<BloqueoDocumentos>().Activos);
    }

    [Fact]
    public async Task GenerarResumenYBaja_20DeCadaUnoEnParalelo_CorrelativosSinRepetir()
    {
        const int cantidad = 20;
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);

        var resumenes = Enumerable.Range(1, cantidad).Select(n => Task.Run(() => Servicio.GenerarResumenAsync(emisor, new ResumenDiarioCpe { FechaReferencia = GeneracionBajaTests.Fecha, Lineas = [LineaResumen(n)] }, Ct), Ct));
        var bajas = Enumerable.Range(1, cantidad).Select(n => Task.Run(() => Servicio.GenerarBajaAsync(emisor, GeneracionBajaTests.Comunicacion(GeneracionBajaTests.Documento(1000 + n)), Ct), Ct));
        var rc = (await Task.WhenAll(resumenes)).SelectMany(r => r).ToList();
        var ra = await Task.WhenAll(bajas);

        Assert.All(rc, r => Assert.True(r.Exito, string.Join("; ", r.Errores.Select(e => e.Codigo))));
        Assert.All(ra, r => Assert.True(r.Exito, string.Join("; ", r.Errores.Select(e => e.Codigo))));
        Assert.Equal(Enumerable.Range(1, cantidad), rc.Select(r => r.Referencia.Correlativo!.Value).Order());
        Assert.Equal(Enumerable.Range(1, cantidad), ra.Select(r => r.Referencia.Correlativo!.Value).Order());
        Assert.All(rc.Concat(ra), r => Assert.True(File.Exists(r.Rutas.Zip)));
    }

    [Fact]
    public async Task EnviarGre_20GuiasEnParalelo_UnSoloTokenYUnTicketPorGuia()
    {
        const int cantidad = 20;
        using var servicios = new ServiciosPrueba(o => o.Ambiente = Ambiente.Produccion);
        var emisor = CertificadoPrueba.Emisor(servicios.Almacen);
        var gre = servicios.Obtener<IServicioGre>();
        var referencias = new List<ReferenciaCpe>();
        for (var n = 0; n < cantidad; n++)
        {
            var generacion = await gre.GenerarAsync(emisor, CasosGre.Dorada() with { Numero = 40000 + n }, Ct);
            Assert.True(generacion.Exito, string.Join("; ", generacion.Errores.Select(e => e.Codigo)));
            referencias.Add(generacion.Referencia);
            servicios.Sunat.Responder(Api.Envio, Api.Ticket($"t-{n}"));
        }

        var liberar = new TaskCompletionSource();
        servicios.Sunat.ResponderTras(Api.Token, liberar.Task, Api.TokenOk());
        var envios = referencias.Select(r => Task.Run(() => gre.EnviarAsync(emisor, r, Ct), Ct)).ToList();
        await Task.Delay(50, Ct);
        liberar.SetResult();
        var resultados = await Task.WhenAll(envios);

        Assert.All(resultados, r => Assert.Equal(EstadoCpe.EnProceso, r.Estado));
        Assert.Equal(1, servicios.Sunat.Llamadas(Api.Token));
        Assert.Equal(cantidad, resultados.Select(r => r.Ticket).Distinct(StringComparer.Ordinal).Count());
        var nombres = servicios.Sunat.Solicitudes.Where(s => s.Operacion == Api.Envio)
            .Select(s => JsonDocument.Parse(s.Cuerpo).RootElement.GetProperty("archivo").GetProperty("nomArchivo").GetString());
        Assert.Equal(cantidad, nombres.Distinct(StringComparer.Ordinal).Count());
    }

    private static string NombreZip(string sobre)
    {
        var inicio = sobre.IndexOf("<fileName>", StringComparison.Ordinal) + "<fileName>".Length;
        return sobre[inicio..sobre.IndexOf("</fileName>", inicio, StringComparison.Ordinal)];
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
