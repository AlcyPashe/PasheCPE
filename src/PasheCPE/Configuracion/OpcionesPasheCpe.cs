namespace PasheCPE;

/// <summary>
/// Opciones globales de PasheCPE (sección «PasheCPE» del appsettings, TRD §23). Los datos de cada emisor no van
/// aquí: se pasan en cada llamada con <see cref="EmisorCpe"/>.
/// </summary>
public sealed class OpcionesPasheCpe
{
    /// <summary>Ruta absoluta del almacén CPE (CERT, DATA, FIRMA, ENVIO, RPTA, REPO, QR, LOG).</summary>
    public string RutaBase { get; set; } = string.Empty;

    /// <summary>Ambiente de SUNAT para los comprobantes. Las guías siempre van a producción.</summary>
    public Ambiente Ambiente { get; set; } = Ambiente.Beta;

    /// <summary>Exoneración por la Ley de la Amazonía cuando el emisor no la indica.</summary>
    public bool ExoneradoAmazoniaPorDefecto { get; set; } = true;

    /// <summary>Id de la firma (<c>ds:Signature/@Id</c>) y de la referencia en <c>cac:Signature</c>.</summary>
    public string IdFirma { get; set; } = "PasheSign";

    /// <summary>Algoritmos de la firma (por defecto RSA-SHA256, el de los comprobantes que SUNAT ya aceptó).</summary>
    public PerfilFirma PerfilFirma { get; set; } = PerfilFirma.Sha256;

    /// <summary>Cómo se informan las boletas y sus notas.</summary>
    public ModoEnvioBoletas ModoEnvioBoletas { get; set; } = ModoEnvioBoletas.ResumenDiario;

    /// <summary>Capas de validación local activas.</summary>
    public OpcionesValidacion Validacion { get; set; } = new();

    /// <summary>Tiempo máximo para establecer la conexión, en segundos.</summary>
    public int TimeoutConexionSegundos { get; set; } = 10;

    /// <summary>Tiempo máximo para recibir la respuesta, en segundos.</summary>
    public int TimeoutRespuestaSegundos { get; set; } = 60;

    /// <summary>Verificación de conectividad antes de cada envío.</summary>
    public OpcionesConectividad Conectividad { get; set; } = new();

    /// <summary>Consulta de tickets.</summary>
    public OpcionesTickets Tickets { get; set; } = new();

    /// <summary>Guías de remisión.</summary>
    public OpcionesGre Gre { get; set; } = new();

    /// <summary>Días antes del vencimiento del certificado a partir de los cuales se advierte.</summary>
    public int AvisoVencimientoCertificadoDias { get; set; } = 30;

    /// <summary>Compila XSL, esquemas y serializadores en segundo plano al iniciar.</summary>
    public bool PrecalentarAlIniciar { get; set; } = true;

    /// <summary>Genera el PDF dentro de la emisión si PasheCPE.Pdf está registrado.</summary>
    public bool GenerarPdfAutomaticamente { get; set; }

    /// <summary>Endpoints que reemplazan o completan los de <c>endpoints.json</c> (obligatorios para OSE).</summary>
    public IList<EndpointCpe> Endpoints { get; } = [];

    /// <summary>Tasas que reemplazan las de <c>tasas.json</c>; cada lista informada reemplaza a la embebida.</summary>
    public OpcionesTasas Tasas { get; set; } = new();

    /// <summary>Plazos que reemplazan los de <c>plazos.json</c>; si se informa, reemplaza la lista completa.</summary>
    public IList<PlazoEnvio> Plazos { get; } = [];

    /// <summary>Vigencias de catálogos o códigos que reemplazan las de <c>catalogos.json</c>.</summary>
    public IList<VigenciaCatalogo> VigenciasCatalogos { get; } = [];

    /// <summary>Monto en letras de la leyenda 1000.</summary>
    public OpcionesMontoEnLetras MontoEnLetras { get; set; } = new();

    /// <summary>Contenido del código QR de los comprobantes.</summary>
    public OpcionesQr Qr { get; set; } = new();
}

/// <summary>
/// Contenido del QR de los comprobantes (TRD §12): RUC|TIPO|SERIE|NUMERO|IGV|TOTAL|FECHA|TIPODOCADQ|NUMDOCADQ|VALORRESUMEN.
/// </summary>
public sealed class OpcionesQr
{
    /// <summary>Número con ceros a la izquierda hasta 8 dígitos (00000123). Por defecto sin ceros (123).</summary>
    public bool CerosNumero { get; set; }

    /// <summary>Agrega un «|» al final del contenido. Por defecto no.</summary>
    public bool SeparadorFinal { get; set; }
}

/// <summary>Monto en letras de la leyenda 1000 (DIECISEIS CON 00/100 SOLES).</summary>
public sealed class OpcionesMontoEnLetras
{
    /// <summary>
    /// Con tildes (DIECISÉIS, MILLÓN, DÓLARES). Por defecto sin tildes: las impresoras térmicas ESC/POS suelen
    /// imprimirlas mal.
    /// </summary>
    public bool ConTildes { get; set; }
}

/// <summary>Capas de validación local (PRD RF-07).</summary>
public sealed class OpcionesValidacion
{
    /// <summary>Validación del modelo en C#.</summary>
    public bool Modelo { get; set; } = true;

    /// <summary>Validación contra los XSD oficiales.</summary>
    public bool Esquema { get; set; } = true;

    /// <summary>Reglas de las XSL oficiales de SUNAT.</summary>
    public bool XslSunat { get; set; } = true;

    /// <summary>
    /// Reglas complementarias en C#: filas del Excel de validación que las XSL no tienen o calculan distinto (incluidas
    /// las de 2027). Cada regla aplica según su vigencia en <c>reglas-cpe.json</c>. Desactivarlas también deja sin
    /// efecto las supresiones de <c>ajustes-xsl.json</c>.
    /// </summary>
    public bool ReglasComplementarias { get; set; } = true;
}

/// <summary>Verificación de conectividad (PRD RF-14).</summary>
public sealed class OpcionesConectividad
{
    /// <summary>Verifica la conectividad antes de cada envío.</summary>
    public bool Verificar { get; set; } = true;

    /// <summary>Host de control para distinguir «sin Internet» de «servicio no disponible».</summary>
    public string HostControl { get; set; } = "www.gob.pe";

    /// <summary>Segundos durante los que se reutiliza el último resultado.</summary>
    public int CacheSegundos { get; set; } = 15;
}

/// <summary>Consulta de tickets (PRD RF-17).</summary>
public sealed class OpcionesTickets
{
    /// <summary>Esperas entre consultas, en segundos. Vacía = 2, 4, 8, 15 y 30.</summary>
    public IList<int> EsperasSegundos { get; } = [];
}

/// <summary>Guías de remisión.</summary>
public sealed class OpcionesGre
{
    /// <summary>
    /// Permite enviar guías aunque <see cref="OpcionesPasheCpe.Ambiente"/> sea Beta. SUNAT no tiene ambiente de
    /// pruebas para guías: el envío es real.
    /// </summary>
    public bool PermitirEnvioConAmbienteBeta { get; set; }
}

/// <summary>Tasas que reemplazan las embebidas en <c>tasas.json</c>.</summary>
public sealed class OpcionesTasas
{
    /// <summary>Tramos del IGV (con su división IGV + IPM).</summary>
    public IList<TramoIgv> Igv { get; } = [];

    /// <summary>Tramos de la tasa especial para MYPE de restaurantes, hoteles y alojamientos.</summary>
    public IList<TramoTasa> MypeRestaurantesHoteles { get; } = [];

    /// <summary>Tramos del ICBPER.</summary>
    public IList<TramoIcbper> Icbper { get; } = [];
}

/// <summary>Ambiente de SUNAT.</summary>
public enum Ambiente
{
    /// <summary>Ambiente de pruebas (beta).</summary>
    Beta,

    /// <summary>Producción.</summary>
    Produccion,
}

/// <summary>Algoritmos de firma.</summary>
public enum PerfilFirma
{
    /// <summary>RSA-SHA1 con digest SHA-1 (el que documentan las guías de SUNAT; criptográficamente obsoleto).</summary>
    Sha1,

    /// <summary>RSA-SHA256 con digest SHA-256 (por defecto): el de los comprobantes que SUNAT ya aceptó en producción.</summary>
    Sha256,
}

/// <summary>Cómo se informan las boletas y sus notas.</summary>
public enum ModoEnvioBoletas
{
    /// <summary>En el resumen diario.</summary>
    ResumenDiario,

    /// <summary>Una por una con sendBill.</summary>
    Individual,
}
