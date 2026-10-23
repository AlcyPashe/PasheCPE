using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PasheCPE.Diagnostico;

/// <summary>
/// Trazas y métricas de PasheCPE (TRD §21): <c>ActivitySource("PasheCPE")</c> por operación y <c>Meter("PasheCPE")</c>
/// con <c>pashecpe.envios</c> (por operación y estado) y <c>pashecpe.envio.duracion</c> (ms). Sin credenciales.
/// </summary>
internal static class Telemetria
{
    public const string Nombre = "PasheCPE";

    public static ActivitySource Actividades { get; } = new(Nombre);

    private static readonly Meter _medidor = new(Nombre);
    private static readonly Counter<long> _envios = _medidor.CreateCounter<long>("pashecpe.envios", description: "Envíos y consultas a SUNAT u OSE por estado resultante.");
    private static readonly Histogram<double> _duracion = _medidor.CreateHistogram<double>("pashecpe.envio.duracion", "ms", "Duración de cada llamada a SUNAT u OSE.");

    public static void RegistrarEnvio(string operacion, EstadoCpe estado, long duracionMs)
    {
        var etiquetas = new TagList { { "operacion", operacion }, { "estado", estado.ToString() } };
        _envios.Add(1, etiquetas);
        _duracion.Record(duracionMs, etiquetas);
    }
}
