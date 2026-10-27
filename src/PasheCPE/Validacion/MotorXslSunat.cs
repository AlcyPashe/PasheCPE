using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using PasheCPE.Catalogos;
using PasheCPE.Empaquetado;
using PasheCPE.Tiempo;
using PasheCPE.Validacion.Emulacion;

namespace PasheCPE.Validacion;

/// <summary>
/// Ejecuta las XSL oficiales de SUNAT con emulación de DataPower (TRD §9.3). Cada XSL se adapta y compila una sola
/// vez (perezoso y thread-safe); cada validación usa sus propios objetos de extensión. Devuelve todos los errores y
/// observaciones de una pasada, en el orden en que se ejecutan. Nunca lanza: una falla interna se devuelve como
/// PCPE-2101 o PCPE-2102.
/// </summary>
internal sealed class MotorXslSunat(CatalogosSunat catalogos, CatalogoErrores errores, RelojCpe reloj)
{
    /// <summary>XSL que se usa para cada tipo de documento. La GRE usa las XSL GRE 2.0 de SUNAT (01/10/2026, Fase 10).</summary>
    public static FrozenDictionary<string, string> XslPorDocumento { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["01"] = "ValidaExprRegFactura-2.0.1.xsl",
        ["03"] = "ValidaExprRegBoleta-2.0.1.xsl",
        ["07"] = "ValidaExprRegNC-2.0.1.xsl",
        ["08"] = "ValidaExprRegND-2.0.1.xsl",
        [ReferenciaCpe.TipoResumenDiario] = "ValidaExprRegSummary-1.1.0.xsl",
        [ReferenciaCpe.TipoComunicacionBaja] = "ValidaExprRegSummaryVoided-1.0.1.xsl",
        [TipoDocumentoSunat.GuiaDeRemisionRemitente] = XslGreRemitente,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Parámetro global con el nombre del archivo enviado (XSL GRE 2.0).</summary>
    public const string ParametroNombreArchivo = "nombreArchivoEnviado";

    /// <summary>XSL de la GRE remitente 2.0 (en <c>recursos\GRE\XSL</c>).</summary>
    public const string XslGreRemitente = "ValidaExprRegGreRemitente-2.0.1.xsl";

    /// <summary>XSL de la GRE transportista 2.0: se compila; se usa desde la Fase 10b.</summary>
    public const string XslGreTransportista = "ValidaExprRegGreTransportista-2.0.1.xsl";

    /// <summary>Todas las XSL embebidas, incluida la de la GRE transportista (solo se compila).</summary>
    public static IReadOnlyList<string> XslEmbebidas { get; } = [.. XslPorDocumento.Values, XslGreTransportista];

    private static readonly Lazy<IReadOnlyList<VariableRespaldoXsl>> _respaldos = new(() =>
        RecursosEmbebidos.Leer("xsl-compatibilidad.json", DatosJson.Default.ArchivoCompatibilidadXsl).VariablesRespaldo);

    private static readonly ConcurrentDictionary<string, Lazy<XslCargada>> _compiladas = new(StringComparer.Ordinal);

    /// <summary>true si el tipo de documento tiene XSL oficial en uso.</summary>
    public static bool TieneXsl(string tipoDocumento) => XslPorDocumento.ContainsKey(tipoDocumento);

    /// <summary>Errores y observaciones de la XSL del tipo de documento de la referencia.</summary>
    public ResultadoMotorXsl Validar(byte[] xml, ReferenciaCpe referencia)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(referencia);
        if (!XslPorDocumento.TryGetValue(referencia.TipoDocumento, out var archivo))
        {
            return new ResultadoMotorXsl([], null);
        }

        XslCompiledTransform transformacion;
        try
        {
            transformacion = Cargar(archivo).Transformacion;
        }
        catch (Exception ex) when (ex is XsltException or XmlException or InvalidOperationException or IOException)
        {
            return new ResultadoMotorXsl([], Falla(archivo, ex));
        }

        return Ejecutar(transformacion, archivo, xml, referencia);
    }

    /// <summary>Ejecuta una XSL ya compilada con los objetos de extensión de esta validación.</summary>
    internal ResultadoMotorXsl Ejecutar(XslCompiledTransform transformacion, string archivo, byte[] xml, ReferenciaCpe referencia)
    {
        try
        {
            var argumentos = new XsltArgumentList();
            var ahora = reloj.AhoraLima();
            // Las XSL GRE 2.0 (variante SFS) reciben el nombre del archivo como parámetro global en vez de dp:variable.
            argumentos.AddParam(ParametroNombreArchivo, string.Empty, NombreArchivoCpe.Xml(referencia));
            argumentos.AddExtensionObject(ExtensionesDataPower.Espacio, new ExtensionesDataPower(NombreArchivoCpe.Xml(referencia)));
            argumentos.AddExtensionObject(ExtensionesExsltFechas.Espacio, new ExtensionesExsltFechas(ahora));
            argumentos.AddExtensionObject(ExtensionesExsltRegexp.Espacio, new ExtensionesExsltRegexp());
            argumentos.AddExtensionObject(ExtensionesPasheCpe.Espacio, new ExtensionesPasheCpe(catalogos, DateOnly.FromDateTime(ahora.DateTime)));

            var recolector = new RecolectorResultadosXsl();
            using (var memoria = new MemoryStream(xml, writable: false))
            using (var lector = XmlReader.Create(memoria, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                transformacion.Transform(lector, argumentos, recolector, ResolutorRecursosLocales.Instancia);
            }

            return new ResultadoMotorXsl(recolector.Hallazgos, null);
        }
        catch (Exception ex) when (ex is XsltException or XmlException or InvalidOperationException or IOException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return new ResultadoMotorXsl([], Falla(archivo, ex));
        }
    }

    /// <summary>Compila la XSL (si aún no lo está) y devuelve las correcciones de compatibilidad aplicadas.</summary>
    public static CorreccionesXsl Compilar(string archivo) => Cargar(archivo).Correcciones;

    private static XslCargada Cargar(string archivo) =>
        _compiladas.GetOrAdd(archivo, a => new Lazy<XslCargada>(() => CargarDesdeRecursos(a), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    private static XslCargada CargarDesdeRecursos(string archivo)
    {
        XDocument original;
        using (var flujo = RecursosEmbebidos.Abrir("xsl." + archivo))
        using (var lector = XmlReader.Create(flujo, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
        {
            original = XDocument.Load(lector);
        }

        var (adaptada, correcciones) = AdaptadorXslDataPower.Adaptar(
            original,
            _respaldos.Value.Where(r => string.Equals(r.Xsl, archivo, StringComparison.Ordinal)));
        var transformacion = new XslCompiledTransform();
        using (var lector = adaptada.CreateReader())
        {
            transformacion.Load(lector, new XsltSettings(enableDocumentFunction: true, enableScript: false), ResolutorRecursosLocales.Instancia);
        }

        return new XslCargada(transformacion, correcciones);
    }

    private ErrorCpe Falla(string archivo, Exception ex)
    {
        for (var actual = ex; actual is not null; actual = actual.InnerException)
        {
            if (actual is ClaveDataPowerDesconocidaException clave)
            {
                return errores.Local("PCPE-2101", OrigenError.XslSunat, null, null, archivo, clave.Clave);
            }
        }

        var detalle = ex.InnerException is { } interna ? $"{ex.Message} {interna.Message}" : ex.Message;
        return errores.Local("PCPE-2102", OrigenError.XslSunat, null, null, archivo, detalle);
    }

    private sealed record XslCargada(XslCompiledTransform Transformacion, CorreccionesXsl Correcciones);
}

/// <summary>Resultado de una XSL: hallazgos en orden de ejecución, o la falla interna que impidió ejecutarla.</summary>
internal sealed record ResultadoMotorXsl(IReadOnlyList<HallazgoXsl> Hallazgos, ErrorCpe? Falla);
