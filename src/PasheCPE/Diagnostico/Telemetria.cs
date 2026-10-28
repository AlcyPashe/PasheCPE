using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PasheCPE.Diagnostico;

/// <summary>
/// Trazas y métricas de PasheCPE (TRD §21): <c>ActivitySource("PasheCPE")</c> por operación (generar, validar, firmar,
/// envíos y consultas) y <c>Meter("PasheCPE")</c> con <c>pashecpe.envios</c> (por operación y estado),
/// <c>pashecpe.envio.duracion</c> (ms) y <c>pashecpe.validacion.errores</c> (por tipo de documento y origen). Las
/// etiquetas solo llevan el nombre del archivo, el tipo y los estados: nunca credenciales.
/// </summary>
internal static class Telemetria
{
    public const string Nombre = "PasheCPE";

    public const string Generar = "generar";

    public const string Validar = "validar";

    public const string Firmar = "firmar";

    public const string EtiquetaDocumento = "pashecpe.documento";

    public const string EtiquetaErrores = "pashecpe.errores";

    public static ActivitySource Actividades { get; } = new(Nombre);

    private static readonly Meter _medidor = new(Nombre);
    private static readonly Counter<long> _envios = _medidor.CreateCounter<long>("pashecpe.envios", description: "Envíos y consultas a SUNAT u OSE por estado resultante.");
    private static readonly Histogram<double> _duracion = _medidor.CreateHistogram<double>("pashecpe.envio.duracion", "ms", "Duración de cada llamada a SUNAT u OSE.");
    private static readonly Counter<long> _erroresValidacion = _medidor.CreateCounter<long>("pashecpe.validacion.errores", description: "Errores de la validación local (modelo, XSD, XSL de SUNAT y reglas complementarias).");

    /// <summary>Inicia la actividad de la operación con el nombre del archivo del documento.</summary>
    public static Activity? Iniciar(string operacion, ReferenciaCpe referencia)
    {
        var actividad = Actividades.StartActivity(operacion);
        actividad?.SetTag(EtiquetaDocumento, referencia.NombreArchivo);
        return actividad;
    }

    /// <summary>Cierra la actividad con la cantidad de errores; con alguno, queda en estado de error.</summary>
    public static void Concluir(Activity? actividad, int errores)
    {
        if (actividad is null)
        {
            return;
        }

        actividad.SetTag(EtiquetaErrores, errores);
        if (errores > 0)
        {
            actividad.SetStatus(ActivityStatusCode.Error);
        }
    }

    public static void RegistrarEnvio(string operacion, EstadoCpe estado, long duracionMs)
    {
        var etiquetas = new TagList { { "operacion", operacion }, { "estado", estado.ToString() } };
        _envios.Add(1, etiquetas);
        _duracion.Record(duracionMs, etiquetas);
    }

    /// <summary>Cuenta los errores de la validación local por origen; los del certificado o la firma no cuentan.</summary>
    public static void RegistrarErroresValidacion(string tipoDocumento, IReadOnlyList<ErrorCpe> errores)
    {
        foreach (var grupo in errores.Where(e => e.Origen is OrigenError.Modelo or OrigenError.Esquema or OrigenError.XslSunat or OrigenError.ReglasComplementarias).GroupBy(e => e.Origen))
        {
            _erroresValidacion.Add(grupo.Count(), new TagList { { "tipoDocumento", tipoDocumento }, { "origen", grupo.Key.ToString() } });
        }
    }
}
