using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BenchmarkDotNet.Attributes;
using PasheCPE.Pdf;

namespace PasheCPE.Benchmarks;

/// <summary>
/// Metas del TRD §20 con una factura de 20 líneas, en caliente y por la API pública (<see cref="PasheCpe.Crear"/>):
/// <list type="bullet">
/// <item><see cref="GenerarFirmarZip"/>: generación + firma + ZIP (+ QR e historial en disco), sin la XSL ni las reglas
/// complementarias; meta &lt; 150 ms.</item>
/// <item><see cref="ValidarXsl"/>: validación de nivel 1 completa (modelo, XSD, XSL de SUNAT y reglas); meta &lt; 300 ms.</item>
/// <item><see cref="GenerarCompleto"/>: lo que hace un sistema de ventas con las opciones por defecto (todo lo anterior).</item>
/// <item><see cref="GenerarPdf"/>: PDF A4 del documento (sin meta en el TRD; referencia).</item>
/// </list>
/// Cada iteración genera un número nuevo para que el historial no crezca. Certificado autofirmado creado aquí, con una
/// clave aleatoria en memoria; todo en una carpeta temporal que se borra al final. No hay red.
/// </summary>
[MemoryDiagnoser]
public class ComprobanteBenchmarks
{
    private const int Lineas = 20;

    private string _carpeta = string.Empty;
    private PasheCpe _completo = null!;
    private PasheCpe _sinXsl = null!;
    private IGeneradorPdf _pdf = null!;
    private EmisorCpe _emisor = null!;
    private ComprobanteCpe _factura = null!;
    private ReferenciaCpe _generada = null!;
    private long _numero;

    [GlobalSetup]
    public void Preparar()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "PasheCPE.Benchmarks", Guid.NewGuid().ToString("N"));
        var clave = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        Directory.CreateDirectory(Path.Combine(_carpeta, "CERT"));
        using (var rsa = RSA.Create(2048))
        {
            var solicitud = new CertificateRequest("CN=BENCHMARK PASHECPE 20123456786, C=PE", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificado = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));
            File.WriteAllBytes(Path.Combine(_carpeta, "CERT", "benchmark.pfx"), certificado.Export(X509ContentType.Pfx, clave));
        }

        _emisor = new EmisorCpe
        {
            Ruc = "20123456786",
            RazonSocial = "COMERCIAL DE PRUEBA S.A.C.",
            Domicilio = new DireccionCpe { Direccion = "JR. PRÓSPERO 123", Ubigeo = "160101", Distrito = "IQUITOS", Provincia = "MAYNAS", Departamento = "LORETO" },
            Canal = CanalEnvio.Sunat,
            Certificado = new CertificadoCpe("benchmark.pfx", clave),
        };
        _factura = new ComprobanteCpe
        {
            TipoDocumento = TipoDocumentoSunat.Factura,
            Serie = "F001",
            Numero = 1,
            Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Ruc, NumeroDocumento = "20987654326", RazonSocial = "CLIENTE S.A." },
            FormaPago = FormaPagoCpe.Contado,
            Lineas = [.. Enumerable.Range(1, Lineas).Select(i => new LineaCpe
            {
                Descripcion = $"PRODUCTO DE PRUEBA {i:00}",
                Cantidad = i % 3 + 1,
                ImporteUnitario = 10.50m + i,
                CodigoProductoSunat = "50221200",
            })],
        };

        _completo = PasheCpePdf.Crear(Opciones(xsl: true));
        _sinXsl = PasheCpe.Crear(Opciones(xsl: false));
        _pdf = _completo.Pdf();

        var generada = _completo.Cpe.GenerarAsync(_emisor, _factura).GetAwaiter().GetResult();
        if (!generada.Exito)
        {
            throw new InvalidOperationException(string.Join("; ", generada.Errores.Select(e => $"{e.Codigo} {e.Descripcion}")));
        }

        _generada = generada.Referencia;
        _numero = 1;
    }

    [GlobalCleanup]
    public void Limpiar()
    {
        _completo.Dispose();
        _sinXsl.Dispose();
        Directory.Delete(_carpeta, recursive: true);
    }

    [Benchmark(Description = "Generar + firmar + ZIP (sin XSL), meta < 150 ms")]
    public async Task<ResultadoGeneracion> GenerarFirmarZip() => await Generar(_sinXsl).ConfigureAwait(false);

    [Benchmark(Description = "Validación nivel 1 con XSL, meta < 300 ms")]
    public async Task<ResultadoValidacion> ValidarXsl() => await _completo.Cpe.ValidarAsync(_emisor, _factura).ConfigureAwait(false);

    [Benchmark(Description = "Generar completo (opciones por defecto)")]
    public async Task<ResultadoGeneracion> GenerarCompleto() => await Generar(_completo).ConfigureAwait(false);

    [Benchmark(Description = "PDF A4")]
    public async Task<ResultadoPdf> GenerarPdf() => await _pdf.GenerarAsync(_generada, MarcaEmisor.SinMarca).ConfigureAwait(false);

    private async Task<ResultadoGeneracion> Generar(PasheCpe pashe)
    {
        var resultado = await pashe.Cpe.GenerarAsync(_emisor, _factura with { Numero = Interlocked.Increment(ref _numero) }).ConfigureAwait(false);
        return resultado.Exito ? resultado : throw new InvalidOperationException(resultado.Errores[0].Descripcion);
    }

    private OpcionesPasheCpe Opciones(bool xsl) => new()
    {
        RutaBase = _carpeta,
        Validacion = new OpcionesValidacion { XslSunat = xsl, ReglasComplementarias = xsl },
    };
}
