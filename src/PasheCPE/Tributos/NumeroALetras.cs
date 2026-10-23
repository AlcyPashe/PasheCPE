using System.Collections.Frozen;
using System.Globalization;
using PasheCPE.Catalogos;

namespace PasheCPE.Tributos;

/// <summary>
/// Monto en letras de la leyenda 1000: «CIENTO VEINTE CON 50/100 SOLES». Se escribe «UNO» al final de la cifra y
/// «UN» delante de MIL y MILLÓN(ES) (VEINTIÚN MIL, UN MILLÓN). Los nombres de moneda vienen de <c>monedas.json</c>.
/// Sin tildes salvo que se pida (VEINTIUN MIL, UN MILLON). Un importe cero es «CERO CON 00/100 SOLES».
/// </summary>
internal static class NumeroALetras
{
    private const decimal _maximo = 999_999_999_999.99m;

    private static readonly Lazy<FrozenDictionary<string, string>> _monedas = new(() =>
        RecursosEmbebidos.Leer("monedas.json", DatosJson.Default.ArchivoMonedas).Monedas
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));

    private static readonly string[] _unidades =
    [
        "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
        "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE",
        "VEINTE", "VEINTIUNO", "VEINTIDÓS", "VEINTITRÉS", "VEINTICUATRO", "VEINTICINCO", "VEINTISÉIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE",
    ];

    private static readonly string[] _decenas = ["", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"];

    private static readonly string[] _centenas =
        ["", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"];

    /// <summary>
    /// Convierte un monto (redondeado a 2 decimales) a letras con el nombre de la moneda ISO 4217. Por defecto sin
    /// tildes (DIECISEIS, MILLON, DOLARES), para las impresoras térmicas ESC/POS (<see cref="OpcionesMontoEnLetras"/>).
    /// </summary>
    public static string Convertir(decimal monto, string moneda, bool conTildes = false)
    {
        var texto = ConTildes(monto, moneda);
        return conTildes ? texto : SinTildes(texto);
    }

    private static string SinTildes(string texto) =>
        texto.Replace('Á', 'A').Replace('É', 'E').Replace('Í', 'I').Replace('Ó', 'O').Replace('Ú', 'U').Replace('Ü', 'U');

    private static string ConTildes(decimal monto, string moneda)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(monto);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(monto, _maximo);
        ArgumentException.ThrowIfNullOrWhiteSpace(moneda);

        var redondeado = Redondeo.Monto(monto);
        var entero = (long)decimal.Truncate(redondeado);
        var centimos = (int)((redondeado - entero) * 100);
        var letras = entero == 0 ? "CERO" : Entero(entero);
        var nombreMoneda = _monedas.Value.GetValueOrDefault(moneda) ?? moneda.ToUpperInvariant();
        return string.Create(CultureInfo.InvariantCulture, $"{letras} CON {centimos:00}/100 {nombreMoneda}");
    }

    private static string Entero(long numero)
    {
        var millones = (int)(numero / 1_000_000);
        var resto = (int)(numero % 1_000_000);
        var partes = new List<string>(2);
        if (millones == 1)
        {
            partes.Add("UN MILLÓN");
        }
        else if (millones > 1)
        {
            partes.Add(MenorDeUnMillon(millones, apocope: true) + " MILLONES");
        }

        if (resto > 0)
        {
            partes.Add(MenorDeUnMillon(resto, apocope: false));
        }

        return string.Join(' ', partes);
    }

    private static string MenorDeUnMillon(int numero, bool apocope)
    {
        var miles = numero / 1000;
        var resto = numero % 1000;
        var partes = new List<string>(2);
        if (miles == 1)
        {
            partes.Add("MIL");
        }
        else if (miles > 1)
        {
            partes.Add(MenorDeMil(miles, apocope: true) + " MIL");
        }

        if (resto > 0)
        {
            partes.Add(MenorDeMil(resto, apocope));
        }

        return string.Join(' ', partes);
    }

    private static string MenorDeMil(int numero, bool apocope)
    {
        if (numero == 100)
        {
            return "CIEN";
        }

        var centenas = _centenas[numero / 100];
        var decenas = MenorDeCien(numero % 100, apocope);
        return centenas.Length == 0 ? decenas : decenas.Length == 0 ? centenas : $"{centenas} {decenas}";
    }

    private static string MenorDeCien(int numero, bool apocope)
    {
        if (numero < 30)
        {
            return (numero, apocope) switch
            {
                (1, true) => "UN",
                (21, true) => "VEINTIÚN",
                _ => _unidades[numero],
            };
        }

        var unidad = numero % 10;
        var decena = _decenas[numero / 10];
        if (unidad == 0)
        {
            return decena;
        }

        return $"{decena} Y {(unidad == 1 && apocope ? "UN" : _unidades[unidad])}";
    }
}
