// Demo de PasheCPE: envíos reales a SUNAT (beta o producción), ejecutados manualmente por el desarrollador.
// Configuración en appsettings.json; credenciales en user-secrets (Demo:UsuarioSol, Demo:ClaveSol, Demo:ClaveCertificado).
// Si falta una clave, se pide en pantalla sin mostrarla y no se guarda. La demo nunca lee el .pfx: lo hace la librería.
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PasheCPE;

Console.OutputEncoding = Encoding.UTF8;
var configuracion = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddUserSecrets(typeof(Program).Assembly, optional: true)
    .Build();

var opciones = new OpcionesPasheCpe();
configuracion.GetSection("PasheCPE").Bind(opciones);
var datos = configuracion.GetSection("Demo").Get<DatosDemo>() ?? throw new InvalidOperationException("Falta la sección Demo en appsettings.json.");

Console.WriteLine($"PasheCPE.Demo — ambiente {opciones.Ambiente}, almacén {opciones.RutaBase}, emisor {datos.Emisor.Ruc}.");
if (opciones.Ambiente == Ambiente.Produccion && !Consola.Confirmar("ATENCIÓN: ambiente de PRODUCCIÓN, los envíos son reales. Escriba PRODUCCION para continuar", "PRODUCCION"))
{
    return;
}

var usuarioSol = configuracion["Demo:UsuarioSol"] ?? Consola.Preguntar("Usuario secundario SOL (sin el RUC)");
var claveSol = configuracion["Demo:ClaveSol"] ?? Consola.PreguntarOculto("Clave SOL");
var claveCertificado = configuracion["Demo:ClaveCertificado"] ?? Consola.PreguntarOculto($"Clave del certificado {datos.Certificado}");

using var registros = LoggerFactory.Create(b => b.AddConfiguration(configuracion.GetSection("Logging")).AddSimpleConsole(o => o.SingleLine = true));
using var pashe = PasheCpe.Crear(opciones, registros);

// Rechazo forzado: sin validación local, para que el error lo detecte SUNAT y no la librería.
var opcionesSinValidacion = new OpcionesPasheCpe();
configuracion.GetSection("PasheCPE").Bind(opcionesSinValidacion);
opcionesSinValidacion.Validacion.Modelo = false;
opcionesSinValidacion.Validacion.Esquema = false;
opcionesSinValidacion.Validacion.XslSunat = false;
opcionesSinValidacion.Validacion.ReglasComplementarias = false;
using var pasheSinValidacion = PasheCpe.Crear(opcionesSinValidacion, registros);

// Resumen diario: las boletas se generan sin enviarlas y se informan en el RC (ModoEnvioBoletas = ResumenDiario).
var opcionesResumen = new OpcionesPasheCpe();
configuracion.GetSection("PasheCPE").Bind(opcionesResumen);
opcionesResumen.ModoEnvioBoletas = ModoEnvioBoletas.ResumenDiario;
using var pasheResumen = PasheCpe.Crear(opcionesResumen, registros);

EmisorCpe Emisor(bool amazonia, string clave, string? usuario = null) => new()
{
    Ruc = datos.Emisor.Ruc,
    RazonSocial = datos.Emisor.RazonSocial,
    NombreComercial = datos.Emisor.NombreComercial,
    CodigoEstablecimiento = datos.Emisor.CodigoEstablecimiento,
    Domicilio = new DireccionCpe
    {
        Direccion = datos.Emisor.Direccion,
        Urbanizacion = string.IsNullOrWhiteSpace(datos.Emisor.Urbanizacion) ? null : datos.Emisor.Urbanizacion,
        Ubigeo = datos.Emisor.Ubigeo,
        Departamento = datos.Emisor.Departamento,
        Provincia = datos.Emisor.Provincia,
        Distrito = datos.Emisor.Distrito,
    },
    ExoneradoAmazonia = amazonia,
    Canal = CanalEnvio.Sunat,
    CredencialesSoap = new CredencialesSoap(usuario ?? usuarioSol, clave),
    Certificado = new CertificadoCpe(datos.Certificado, claveCertificado),
};

ComprobanteCpe Comprobante(string tipo, string serie, long numero, DatosCliente cliente, string? afectacion) => new()
{
    TipoDocumento = tipo,
    Serie = serie,
    Numero = numero,
    Cliente = new ClienteCpe { TipoDocumento = cliente.TipoDocumento, NumeroDocumento = cliente.NumeroDocumento, RazonSocial = cliente.RazonSocial },
    FormaPago = FormaPagoCpe.Contado,
    Lineas =
    [
        new LineaCpe { CodigoInterno = "P001", Descripcion = "PRODUCTO DE PRUEBA PASHECPE", CodigoProductoSunat = "50221200", Cantidad = 2, ImporteUnitario = 59.00m, Afectacion = afectacion },
        new LineaCpe { CodigoInterno = "S001", Descripcion = "SERVICIO DE PRUEBA", TipoItem = TipoItem.Servicio, Cantidad = 1, ImporteUnitario = 11.80m, Afectacion = afectacion },
    ],
};

while (true)
{
    Console.WriteLine();
    Console.WriteLine("1. Factura gravada (IGV 18 %)");
    Console.WriteLine("2. Factura exonerada (Ley de la Amazonía)");
    Console.WriteLine("3. Boleta de venta individual");
    Console.WriteLine("4. Factura con rechazo forzado (cliente con DNI, sin validación local)");
    Console.WriteLine("5. Factura con usuario y clave SOL erróneos (beta acepta cualquier clave de MODDATOS)");
    Console.WriteLine("6. Reenviar o reconciliar un documento ya generado (EnviarAsync)");
    Console.WriteLine("7. Consultar el CDR (getStatusCdr, solo producción)");
    Console.WriteLine("8. Ver el historial de un documento");
    Console.WriteLine("9. Resumen diario: 2 boletas (gravada y exonerada), RC desde FIRMA, envío y espera del ticket");
    Console.WriteLine("10. Consultar el ticket de un resumen ya enviado (también desde otra ejecución)");
    Console.WriteLine("0. Salir");
    var opcion = Consola.Preguntar("Opción");
    try
    {
        switch (opcion)
        {
            case "1":
                await Emitir(pashe, Emisor(amazonia: false, claveSol), Comprobante(TipoDocumentoSunat.Factura, datos.SerieFactura, Consola.Numero(), datos.Cliente, AfectacionIgv.GravadoOneroso));
                break;
            case "2":
                await Emitir(pashe, Emisor(amazonia: true, claveSol), Comprobante(TipoDocumentoSunat.Factura, datos.SerieFactura, Consola.Numero(), datos.Cliente, null));
                break;
            case "3":
                await Emitir(pashe, Emisor(amazonia: false, claveSol), Comprobante(TipoDocumentoSunat.BoletaDeVenta, datos.SerieBoleta, Consola.Numero(), datos.ClienteBoleta, AfectacionIgv.GravadoOneroso));
                break;
            case "4":
                await Emitir(pasheSinValidacion, Emisor(amazonia: false, claveSol), Comprobante(TipoDocumentoSunat.Factura, datos.SerieFactura, Consola.Numero(), datos.ClienteBoleta, AfectacionIgv.GravadoOneroso));
                break;
            case "5":
                await Emitir(pashe, Emisor(amazonia: false, "CLAVE-ERRONEA", usuario: "NOEXISTE"), Comprobante(TipoDocumentoSunat.Factura, datos.SerieFactura, Consola.Numero(), datos.Cliente, AfectacionIgv.GravadoOneroso));
                break;
            case "6":
                Consola.Imprimir(await pashe.Cpe.EnviarAsync(Emisor(amazonia: false, claveSol), Consola.Referencia(datos.Emisor.Ruc)));
                break;
            case "7":
                Consola.Imprimir(await pashe.Cpe.ConsultarCdrAsync(Emisor(amazonia: false, claveSol), Consola.Referencia(datos.Emisor.Ruc)));
                break;
            case "8":
                Consola.Imprimir(await pashe.Historial.ObtenerAsync(Consola.Referencia(datos.Emisor.Ruc)));
                break;
            case "9":
                await ResumenDiario(pasheResumen, Emisor(amazonia: false, claveSol), Consola.Numero());
                break;
            case "10":
                var resumen = Consola.ReferenciaResumen(datos.Emisor.Ruc);
                Consola.Imprimir(await pasheResumen.Cpe.ConsultarTicketAsync(Emisor(amazonia: false, claveSol), resumen));
                await ImprimirDocumentos(pasheResumen, resumen);
                break;
            case "0":
                return;
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Cancelado.");
    }
}

// Genera dos boletas (sin enviarlas), arma el RC leyendo sus XML firmados, lo envía y espera el ticket.
async Task ResumenDiario(PasheCpe instancia, EmisorCpe emisor, long numero)
{
    var hoy = DateOnly.FromDateTime(DateTime.Now);
    var boletas = new[] { (numero, AfectacionIgv.GravadoOneroso), (numero + 1, AfectacionIgv.ExoneradoOneroso) };
    foreach (var (n, afectacion) in boletas)
    {
        var generacion = await instancia.Cpe.GenerarAsync(emisor, Comprobante(TipoDocumentoSunat.BoletaDeVenta, datos.SerieBoleta, n, datos.ClienteBoleta, afectacion) with { FechaEmision = hoy });
        Consola.Imprimir(generacion);
        if (!generacion.Exito)
        {
            return;
        }
    }

    var documentos = boletas.Select(b => new DocumentoResumenCpe { TipoDocumento = TipoDocumentoSunat.BoletaDeVenta, Serie = datos.SerieBoleta, Numero = b.Item1 }).ToList();
    foreach (var resumen in await instancia.Cpe.GenerarResumenAsync(emisor, hoy, documentos))
    {
        Consola.Imprimir(resumen);
        if (!resumen.Exito)
        {
            continue;
        }

        var envio = await instancia.Cpe.EnviarAsync(emisor, resumen.Referencia);
        Consola.Imprimir(envio);
        if (envio.Estado == EstadoCpe.EnProceso)
        {
            Console.WriteLine("  Esperando el ticket (2, 4, 8, 15 y 30 s)…");
            Consola.Imprimir(await instancia.Cpe.EsperarTicketAsync(emisor, resumen.Referencia));
        }

        await ImprimirDocumentos(instancia, resumen.Referencia);
    }
}

// Estado de cada documento informado en el resumen, según su propio historial.
static async Task ImprimirDocumentos(PasheCpe instancia, ReferenciaCpe resumen)
{
    var historial = await instancia.Historial.ObtenerAsync(resumen);
    foreach (var documento in historial?.Documentos ?? [])
    {
        var propio = await instancia.Historial.ObtenerAsync(documento.Referencia);
        Console.WriteLine($"  {documento.Referencia.NombreArchivo} (estado del ítem {documento.Estado}): {propio?.EstadoActual.ToString() ?? "sin historial"}");
    }
}

static async Task Emitir(PasheCpe instancia, EmisorCpe emisor, ComprobanteCpe cpe)
{
    var emision = await instancia.Cpe.EmitirAsync(emisor, cpe);
    Consola.Imprimir(emision.Generacion);
    if (emision.Generacion.Exito)
    {
        Consola.Imprimir(emision.Envio);
    }
}

/// <summary>Entrada y salida por consola de la demo.</summary>
internal static class Consola
{
    public static string Preguntar(string texto, string? porDefecto = null)
    {
        Console.Write(porDefecto is null ? $"{texto}: " : $"{texto} [{porDefecto}]: ");
        var respuesta = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(respuesta) ? porDefecto ?? string.Empty : respuesta;
    }

    /// <summary>Lee una clave sin mostrarla (asteriscos); no se guarda en ningún lado.</summary>
    public static string PreguntarOculto(string texto)
    {
        Console.Write($"{texto}: ");
        var clave = new StringBuilder();
        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);
            if (tecla.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return clave.ToString();
            }

            if (tecla.Key == ConsoleKey.Backspace)
            {
                if (clave.Length > 0)
                {
                    clave.Length--;
                    Console.Write("\b \b");
                }
            }
            else if (!char.IsControl(tecla.KeyChar))
            {
                clave.Append(tecla.KeyChar);
                Console.Write('*');
            }
        }
    }

    public static bool Confirmar(string texto, string palabra) =>
        string.Equals(Preguntar(texto), palabra, StringComparison.Ordinal);

    /// <summary>Número del comprobante; por defecto uno que no se repite (MMddHHmm) para no chocar con envíos anteriores.</summary>
    public static long Numero()
    {
        var porDefecto = DateTime.Now.ToString("MMddHHmm", CultureInfo.InvariantCulture);
        return long.Parse(Preguntar("Número", porDefecto), CultureInfo.InvariantCulture);
    }

    public static ReferenciaCpe Referencia(string ruc)
    {
        var tipo = Preguntar("Tipo (01 factura, 03 boleta, RC resumen diario)", "01");
        if (tipo == ReferenciaCpe.TipoResumenDiario)
        {
            return ReferenciaResumen(ruc);
        }

        var serie = Preguntar("Serie", tipo == "03" ? "B001" : "F001");
        var numero = long.Parse(Preguntar("Número"), CultureInfo.InvariantCulture);
        var fecha = DateOnly.ParseExact(Preguntar("Fecha de emisión (yyyy-MM-dd)", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return ReferenciaCpe.Comprobante(ruc, tipo, serie, numero, fecha);
    }

    public static ReferenciaCpe ReferenciaResumen(string ruc)
    {
        var fecha = DateOnly.ParseExact(Preguntar("Fecha de generación del resumen (yyyy-MM-dd)", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var correlativo = int.Parse(Preguntar("Correlativo del día", "1"), CultureInfo.InvariantCulture);
        return ReferenciaCpe.Resumen(ruc, fecha, correlativo);
    }

    public static void Imprimir(ResultadoGeneracion generacion)
    {
        Console.WriteLine();
        Console.WriteLine($"— Generación {generacion.NombreArchivo}: {(generacion.Exito ? "correcta" : "con errores")}");
        foreach (var error in generacion.Errores)
        {
            Console.WriteLine($"  ERROR {error.Codigo} [{error.Origen}] {error.Ruta}: {error.Descripcion}");
        }

        foreach (var advertencia in generacion.Advertencias)
        {
            Console.WriteLine($"  Advertencia {advertencia.Codigo}: {advertencia.Descripcion}");
        }

        if (generacion.Exito)
        {
            Console.WriteLine($"  Valor resumen: {generacion.ValorResumen}");
            if (generacion.ContenidoQr is not null)
            {
                Console.WriteLine($"  QR: {generacion.ContenidoQr}");
            }

            Console.WriteLine($"  Fecha límite de envío: {generacion.FechaLimiteEnvio:dd/MM/yyyy}");
        }
    }

    public static void Imprimir(ResultadoEnvio envio)
    {
        Console.WriteLine();
        Console.WriteLine($"— Envío: {envio.Estado} (nivel {envio.Nivel?.ToString(CultureInfo.InvariantCulture) ?? "-"}) código {envio.Codigo}");
        Console.WriteLine($"  SUNAT: {envio.Descripcion}");
        if (envio.DescripcionCatalogo is not null && envio.DescripcionCatalogo != envio.Descripcion)
        {
            Console.WriteLine($"  Catálogo: {envio.DescripcionCatalogo}");
        }

        Console.WriteLine($"  Mensaje para el cajero: {envio.MensajeUsuario}");
        if (envio.Ticket is not null)
        {
            Console.WriteLine($"  Ticket: {envio.Ticket}");
        }

        Console.WriteLine($"  Reintentable: {envio.EsReintentable} · Requiere consulta de estado: {envio.RequiereConsultaEstado}");
        foreach (var observacion in envio.Observaciones)
        {
            Console.WriteLine($"  Observación {observacion.Codigo}: {observacion.Descripcion}");
        }

        foreach (var error in envio.Errores)
        {
            Console.WriteLine($"  Error {error.Codigo}: {error.Descripcion}");
        }

        if (envio.Tecnico is { } tecnico)
        {
            Console.WriteLine($"  Técnico: {tecnico.Operacion} {tecnico.Endpoint} HTTP {tecnico.Http} {tecnico.DuracionMs} ms {tecnico.FaultCode} {tecnico.Excepcion}");
        }

        Console.WriteLine($"  ZIP: {envio.Rutas.Zip}");
        Console.WriteLine($"  CDR: {envio.Rutas.Cdr}");
        Console.WriteLine($"  Historial: {envio.Rutas.Historial}");
        Imprimir(envio.Historial);
    }

    public static void Imprimir(HistorialCpe? historial)
    {
        if (historial is null)
        {
            Console.WriteLine("  Sin historial.");
            return;
        }

        Console.WriteLine($"  Estado actual: {historial.EstadoActual}{(historial.Ticket is null ? string.Empty : $" · ticket {historial.Ticket}")}");
        Imprimir(historial.Intentos);
        foreach (var documento in historial.Documentos ?? [])
        {
            Console.WriteLine($"    Informa {documento.Referencia.NombreArchivo} (estado del ítem {documento.Estado})");
        }
    }

    private static void Imprimir(IReadOnlyList<IntentoEnvio> intentos)
    {
        foreach (var intento in intentos)
        {
            Console.WriteLine($"    {intento.Fecha:yyyy-MM-dd HH:mm:ss} {intento.Operacion,-12} {intento.Estado,-24} {intento.Codigo} {intento.Descripcion}");
        }
    }
}

/// <summary>Sección «Demo» de appsettings.json.</summary>
internal sealed class DatosDemo
{
    public DatosEmisor Emisor { get; set; } = new();

    public string Certificado { get; set; } = "certificado.pfx";

    public DatosCliente Cliente { get; set; } = new();

    public DatosCliente ClienteBoleta { get; set; } = new();

    public string SerieFactura { get; set; } = "F001";

    public string SerieBoleta { get; set; } = "B001";
}

/// <summary>Emisor de la demo.</summary>
internal sealed class DatosEmisor
{
    public string Ruc { get; set; } = string.Empty;

    public string RazonSocial { get; set; } = string.Empty;

    public string? NombreComercial { get; set; }

    public string CodigoEstablecimiento { get; set; } = "0000";

    public string Direccion { get; set; } = string.Empty;

    public string? Urbanizacion { get; set; }

    public string? Ubigeo { get; set; }

    public string? Departamento { get; set; }

    public string? Provincia { get; set; }

    public string? Distrito { get; set; }
}

/// <summary>Cliente de la demo.</summary>
internal sealed class DatosCliente
{
    public string TipoDocumento { get; set; } = "6";

    public string NumeroDocumento { get; set; } = string.Empty;

    public string RazonSocial { get; set; } = string.Empty;
}
