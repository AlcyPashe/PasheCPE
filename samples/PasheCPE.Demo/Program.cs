// Demo de PasheCPE: envíos reales a SUNAT (beta o producción), ejecutados manualmente por el desarrollador.
// Configuración en appsettings.json; credenciales en user-secrets (Demo:UsuarioSol, Demo:ClaveSol, Demo:ClaveCertificado y,
// para el canal OSE eFact, Demo:UsuarioOse y Demo:ClaveOse).
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

// Canal OSE (Fase 9): eFact solo está embebido en producción; en beta el envío da PCPE-4004 sin llamar a nadie.
var canal = Consola.Preguntar("Canal de envío: 1 = SUNAT, 2 = OSE eFact", "1") == "2" ? CanalEnvio.Ose("efact") : CanalEnvio.Sunat;
if (canal.Tipo == TipoCanal.Ose && opciones.Ambiente != Ambiente.Produccion)
{
    Console.WriteLine("Aviso: eFact no tiene ambiente de pruebas; con Ambiente = Beta los envíos darán PCPE-4004.");
}

var (usuarioEnvio, claveEnvio) = canal.Tipo == TipoCanal.Sunat
    ? (configuracion["Demo:UsuarioSol"] ?? Consola.Preguntar("Usuario secundario SOL (sin el RUC)"), configuracion["Demo:ClaveSol"] ?? Consola.PreguntarOculto("Clave SOL"))
    : (configuracion["Demo:UsuarioOse"] ?? Consola.Preguntar("Usuario de eFact", datos.Emisor.Ruc), configuracion["Demo:ClaveOse"] ?? Consola.PreguntarOculto("Clave de eFact"));
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
    Canal = canal,
    CredencialesSoap = new CredencialesSoap(usuario ?? usuarioEnvio, clave),
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

// Factura al crédito en dos cuotas (a 30 y 60 días) por el total de Comprobante (129.80): base del ajuste de cuotas.
ComprobanteCpe FacturaAlCredito(long numero) => Comprobante(TipoDocumentoSunat.Factura, datos.SerieFactura, numero, datos.Cliente, AfectacionIgv.GravadoOneroso) with
{
    FormaPago = Cuotas(129.80m),
};

FormaPagoCpe Cuotas(decimal monto)
{
    var hoy = DateOnly.FromDateTime(DateTime.Now);
    var primera = Math.Round(monto / 2, 2, MidpointRounding.AwayFromZero);
    return new FormaPagoCpe
    {
        Tipo = TipoFormaPago.Credito,
        Cuotas =
        [
            new CuotaCpe { Monto = primera, FechaVencimiento = hoy.AddDays(30) },
            new CuotaCpe { Monto = monto - primera, FechaVencimiento = hoy.AddDays(60) },
        ],
    };
}

// Nota sobre una factura gravada de la demo (casos 1 u 11): pide la factura y arma la nota con las líneas indicadas.
NotaCpe Nota(string tipo, string motivo, string sustento, IReadOnlyList<LineaCpe> lineas) => new()
{
    TipoDocumento = tipo,
    Serie = tipo == TipoDocumentoSunat.NotaDeCredito ? datos.SerieNotaCredito : datos.SerieNotaDebito,
    Numero = Consola.Numero(),
    Motivo = motivo,
    Sustento = sustento,
    DocumentoModificado = Consola.Factura(datos.SerieFactura),
    Cliente = new ClienteCpe { TipoDocumento = datos.Cliente.TipoDocumento, NumeroDocumento = datos.Cliente.NumeroDocumento, RazonSocial = datos.Cliente.RazonSocial },
    Lineas = lineas,
};

IReadOnlyList<LineaCpe> LineasDeComprobante(string descripcionProducto, decimal importeProducto, decimal importeServicio) =>
[
    new LineaCpe { CodigoInterno = "P001", Descripcion = descripcionProducto, CodigoProductoSunat = "50221200", Cantidad = 2, ImporteUnitario = importeProducto, Afectacion = AfectacionIgv.GravadoOneroso },
    new LineaCpe { CodigoInterno = "S001", Descripcion = "SERVICIO DE PRUEBA", TipoItem = TipoItem.Servicio, Cantidad = 1, ImporteUnitario = importeServicio, Afectacion = AfectacionIgv.GravadoOneroso },
];

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
    Console.WriteLine("10. Consultar el ticket de un resumen o una baja ya enviados (también desde otra ejecución)");
    Console.WriteLine("11. Factura gravada al crédito en 2 cuotas (base del caso 16)");
    Console.WriteLine("12. Nota de crédito por devolución total (06) de una factura gravada aceptada");
    Console.WriteLine("13. Nota de débito por intereses por mora (01) de una factura gravada aceptada");
    Console.WriteLine("14. Nota de crédito por corrección de la descripción (03) con importes en cero");
    Console.WriteLine("15. Nota de crédito por corrección de la descripción (03) con los importes de la línea corregida");
    Console.WriteLine("16. Nota de crédito por ajuste de cuotas (13) de una factura al crédito aceptada (caso 11)");
    Console.WriteLine("17. Comunicación de baja de una factura aceptada: RA, envío y espera del ticket");
    Console.WriteLine("18. Comunicación de baja fuera de plazo (emitida hace 8 días): error local, no se envía");
    Console.WriteLine("0. Salir");
    var opcion = Consola.Preguntar("Opción");
    try
    {
        switch (opcion)
        {
            case "1":
                await Emitir(pashe, Emisor(amazonia: false, claveEnvio), Comprobante(TipoDocumentoSunat.Factura, datos.SerieFactura, Consola.Numero(), datos.Cliente, AfectacionIgv.GravadoOneroso));
                break;
            case "2":
                await Emitir(pashe, Emisor(amazonia: true, claveEnvio), Comprobante(TipoDocumentoSunat.Factura, datos.SerieFactura, Consola.Numero(), datos.Cliente, null));
                break;
            case "3":
                await Emitir(pashe, Emisor(amazonia: false, claveEnvio), Comprobante(TipoDocumentoSunat.BoletaDeVenta, datos.SerieBoleta, Consola.Numero(), datos.ClienteBoleta, AfectacionIgv.GravadoOneroso));
                break;
            case "4":
                await Emitir(pasheSinValidacion, Emisor(amazonia: false, claveEnvio), Comprobante(TipoDocumentoSunat.Factura, datos.SerieFactura, Consola.Numero(), datos.ClienteBoleta, AfectacionIgv.GravadoOneroso));
                break;
            case "5":
                await Emitir(pashe, Emisor(amazonia: false, "CLAVE-ERRONEA", usuario: "NOEXISTE"), Comprobante(TipoDocumentoSunat.Factura, datos.SerieFactura, Consola.Numero(), datos.Cliente, AfectacionIgv.GravadoOneroso));
                break;
            case "6":
                Consola.Imprimir(await pashe.Cpe.EnviarAsync(Emisor(amazonia: false, claveEnvio), Consola.Referencia(datos.Emisor.Ruc)));
                break;
            case "7":
                Consola.Imprimir(await pashe.Cpe.ConsultarCdrAsync(Emisor(amazonia: false, claveEnvio), Consola.Referencia(datos.Emisor.Ruc)));
                break;
            case "8":
                Consola.Imprimir(await pashe.Historial.ObtenerAsync(Consola.Referencia(datos.Emisor.Ruc)));
                break;
            case "9":
                await ResumenDiario(pasheResumen, Emisor(amazonia: false, claveEnvio), Consola.Numero());
                break;
            case "10":
                var resumen = Consola.ReferenciaResumen(datos.Emisor.Ruc, Consola.Preguntar("Tipo (RC resumen diario, RA comunicación de baja)", ReferenciaCpe.TipoResumenDiario));
                Consola.Imprimir(await pasheResumen.Cpe.ConsultarTicketAsync(Emisor(amazonia: false, claveEnvio), resumen));
                await ImprimirDocumentos(pasheResumen, resumen);
                break;
            case "11":
                await Emitir(pashe, Emisor(amazonia: false, claveEnvio), FacturaAlCredito(Consola.Numero()));
                break;
            case "12":
                await EmitirNota(pashe, Emisor(amazonia: false, claveEnvio), Nota(TipoDocumentoSunat.NotaDeCredito, MotivoNotaCredito.DevolucionTotal, "DEVOLUCION TOTAL DE LA MERCADERIA", LineasDeComprobante("PRODUCTO DE PRUEBA PASHECPE", 59.00m, 11.80m)));
                break;
            case "13":
                await EmitirNota(pashe, Emisor(amazonia: false, claveEnvio), Nota(TipoDocumentoSunat.NotaDeDebito, MotivoNotaDebito.InteresesPorMora, "INTERESES POR PAGO FUERA DE PLAZO", [new LineaCpe { CodigoInterno = "S002", Descripcion = "INTERESES POR MORA", TipoItem = TipoItem.Servicio, Cantidad = 1, ImporteUnitario = 11.80m, Afectacion = AfectacionIgv.GravadoOneroso }]));
                break;
            case "14":
                await EmitirNota(pashe, Emisor(amazonia: false, claveEnvio), Nota(TipoDocumentoSunat.NotaDeCredito, MotivoNotaCredito.CorreccionDescripcion, "CORRECCION DE LA DESCRIPCION DEL PRODUCTO", LineasDeComprobante("PRODUCTO DE PRUEBA PASHECPE CORREGIDO", 0m, 0m)));
                break;
            case "15":
                await EmitirNota(pashe, Emisor(amazonia: false, claveEnvio), Nota(TipoDocumentoSunat.NotaDeCredito, MotivoNotaCredito.CorreccionDescripcion, "CORRECCION DE LA DESCRIPCION DEL PRODUCTO", LineasDeComprobante("PRODUCTO DE PRUEBA PASHECPE CORREGIDO", 59.00m, 11.80m)));
                break;
            case "16":
                await EmitirNota(pashe, Emisor(amazonia: false, claveEnvio), Nota(TipoDocumentoSunat.NotaDeCredito, MotivoNotaCredito.CorreccionMontoNetoPendientePago, "NUEVAS FECHAS DE PAGO DE LAS CUOTAS", [new LineaCpe { CodigoInterno = "P001", Descripcion = "AJUSTE DE CUOTAS", Cantidad = 1, ImporteUnitario = 0m, Afectacion = AfectacionIgv.GravadoOneroso }]) with
                {
                    FormaPago = Cuotas(decimal.Parse(Consola.Preguntar("Monto neto pendiente", "129.80"), CultureInfo.InvariantCulture)),
                });
                break;
            case "17":
                await ComunicacionBaja(pashe, Emisor(amazonia: false, claveEnvio), Consola.Factura(datos.SerieFactura, "Factura que se da de baja:"));
                break;
            case "18":
                var vencida = DateOnly.FromDateTime(DateTime.Now).AddDays(-8);
                Consola.Imprimir(await pashe.Cpe.GenerarBajaAsync(Emisor(amazonia: false, claveEnvio), Baja(datos.SerieFactura, Consola.Numero(), vencida)));
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

// Da de baja una factura ya aceptada (casos 1 u 11): genera la RA, la envía y espera el ticket.
async Task ComunicacionBaja(PasheCpe instancia, EmisorCpe emisor, DocumentoModificadoCpe factura)
{
    var baja = await instancia.Cpe.GenerarBajaAsync(emisor, Baja(factura.Serie, factura.Numero, factura.FechaEmision!.Value));
    Consola.Imprimir(baja);
    if (!baja.Exito)
    {
        return;
    }

    var envio = await instancia.Cpe.EnviarAsync(emisor, baja.Referencia);
    Consola.Imprimir(envio);
    if (envio.Estado == EstadoCpe.EnProceso)
    {
        Console.WriteLine("  Esperando el ticket (2, 4, 8, 15 y 30 s)…");
        Consola.Imprimir(await instancia.Cpe.EsperarTicketAsync(emisor, baja.Referencia));
    }

    await ImprimirDocumentos(instancia, baja.Referencia);
}

ComunicacionBajaCpe Baja(string serie, long numero, DateOnly fechaEmision) => new()
{
    FechaReferencia = fechaEmision,
    Documentos =
    [
        new DocumentoBajaCpe
        {
            TipoDocumento = TipoDocumentoSunat.Factura,
            Serie = serie,
            Numero = numero,
            Motivo = Consola.Preguntar("Motivo de la baja", "ERROR EN LOS DATOS DEL CLIENTE"),
        },
    ],
};

// Estado de cada documento informado en el resumen o la baja, según su propio historial.
static async Task ImprimirDocumentos(PasheCpe instancia, ReferenciaCpe resumen)
{
    var historial = await instancia.Historial.ObtenerAsync(resumen);
    foreach (var documento in historial?.Documentos ?? [])
    {
        var propio = await instancia.Historial.ObtenerAsync(documento.Referencia);
        Console.WriteLine($"  {documento.Referencia.NombreArchivo} (estado del ítem {documento.Estado}): {propio?.EstadoActual.ToString() ?? "sin historial"}");
    }
}

static async Task Emitir(PasheCpe instancia, EmisorCpe emisor, ComprobanteCpe cpe) =>
    Imprimir(await instancia.Cpe.EmitirAsync(emisor, cpe));

static async Task EmitirNota(PasheCpe instancia, EmisorCpe emisor, NotaCpe nota) =>
    Imprimir(await instancia.Cpe.EmitirAsync(emisor, nota));

static void Imprimir(ResultadoEmision emision)
{
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
        var tipo = Preguntar("Tipo (01 factura, 03 boleta, 07 nota de crédito, 08 nota de débito, RC resumen diario, RA comunicación de baja)", "01");
        if (tipo is ReferenciaCpe.TipoResumenDiario or ReferenciaCpe.TipoComunicacionBaja)
        {
            return ReferenciaResumen(ruc, tipo);
        }

        var serie = Preguntar("Serie", tipo switch { "03" => "B001", "07" => "FC01", "08" => "FD01", _ => "F001" });
        var numero = long.Parse(Preguntar("Número"), CultureInfo.InvariantCulture);
        var fecha = DateOnly.ParseExact(Preguntar("Fecha de emisión (yyyy-MM-dd)", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return ReferenciaCpe.Comprobante(ruc, tipo, serie, numero, fecha);
    }

    /// <summary>Factura que modifica la nota o que se da de baja, con su fecha de emisión para validarla contra el historial.</summary>
    public static DocumentoModificadoCpe Factura(string seriePorDefecto, string titulo = "Factura que modifica la nota:")
    {
        Console.WriteLine(titulo);
        return new DocumentoModificadoCpe
        {
            TipoDocumento = TipoDocumentoSunat.Factura,
            Serie = Preguntar("  Serie", seriePorDefecto),
            Numero = long.Parse(Preguntar("  Número"), CultureInfo.InvariantCulture),
            FechaEmision = DateOnly.ParseExact(Preguntar("  Fecha de emisión (yyyy-MM-dd)", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
    }

    /// <summary>Resumen diario (RC) o comunicación de baja (RA) por su fecha de generación y correlativo.</summary>
    public static ReferenciaCpe ReferenciaResumen(string ruc, string tipo)
    {
        var fecha = DateOnly.ParseExact(Preguntar("Fecha de generación (yyyy-MM-dd)", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var correlativo = int.Parse(Preguntar("Correlativo del día", "1"), CultureInfo.InvariantCulture);
        return tipo == ReferenciaCpe.TipoComunicacionBaja ? ReferenciaCpe.Baja(ruc, fecha, correlativo) : ReferenciaCpe.Resumen(ruc, fecha, correlativo);
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

        Console.WriteLine($"  Estado actual: {historial.EstadoActual}{(historial.Ticket is null ? string.Empty : $" · ticket {historial.Ticket}")}{(historial.Baja is null ? string.Empty : $" · baja {historial.Baja.NombreArchivo}")}");
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

    public string SerieNotaCredito { get; set; } = "FC01";

    public string SerieNotaDebito { get; set; } = "FD01";
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
