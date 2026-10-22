namespace PasheCPE.Tiempo;

/// <summary>Zona horaria de Lima, independiente de la zona del servidor (PRD RF-24).</summary>
internal static class ZonaLima
{
    public static TimeZoneInfo Zona { get; } = Resolver();

    private static TimeZoneInfo Resolver()
    {
        try
        {
            // En Windows, .NET 10 resuelve los identificadores IANA mediante ICU.
            return TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
        }
    }
}

/// <summary>Fecha y hora de Lima sobre un <see cref="TimeProvider"/> inyectable (en pruebas, FakeTimeProvider).</summary>
internal sealed class RelojCpe(TimeProvider tiempo)
{
    public DateTimeOffset AhoraLima() => TimeZoneInfo.ConvertTime(tiempo.GetUtcNow(), ZonaLima.Zona);

    public DateOnly HoyLima() => DateOnly.FromDateTime(AhoraLima().DateTime);
}
