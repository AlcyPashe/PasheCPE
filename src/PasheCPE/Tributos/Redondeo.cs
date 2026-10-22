namespace PasheCPE.Tributos;

/// <summary>Redondeo de montos (TRD §7): siempre <c>decimal</c> y <see cref="MidpointRounding.AwayFromZero"/>.</summary>
internal static class Redondeo
{
    /// <summary>Montos: 2 decimales.</summary>
    public static decimal Monto(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    /// <summary>Valores y precios unitarios: hasta 10 decimales.</summary>
    public static decimal Unitario(decimal valor) => Math.Round(valor, 10, MidpointRounding.AwayFromZero);
}
