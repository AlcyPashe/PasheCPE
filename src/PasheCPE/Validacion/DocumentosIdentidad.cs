using System.Text.RegularExpressions;

namespace PasheCPE.Validacion;

/// <summary>Formatos de los documentos de identidad (catálogo N.° 06) y dígito verificador del RUC.</summary>
internal static partial class DocumentosIdentidad
{
    private static readonly int[] _pesosRuc = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    /// <summary>RUC de 11 dígitos con dígito verificador (módulo 11).</summary>
    public static bool EsRucValido(string? ruc)
    {
        if (ruc is null || !OnceDigitos().IsMatch(ruc))
        {
            return false;
        }

        var suma = 0;
        for (var i = 0; i < 10; i++)
        {
            suma += (ruc[i] - '0') * _pesosRuc[i];
        }

        var verificador = 11 - (suma % 11);
        verificador = verificador switch
        {
            10 => 0,
            11 => 1,
            _ => verificador,
        };
        return ruc[10] - '0' == verificador;
    }

    /// <summary>El número cumple el formato de su tipo de documento.</summary>
    public static bool TieneFormato(string tipo, string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return false;
        }

        return tipo switch
        {
            TipoDocumentoIdentidad.Ruc => EsRucValido(numero),
            TipoDocumentoIdentidad.Dni => OchoDigitos().IsMatch(numero),
            TipoDocumentoIdentidad.CarnetExtranjeria or TipoDocumentoIdentidad.Pasaporte => Alfanumerico12().IsMatch(numero),
            _ => Alfanumerico15().IsMatch(numero),
        };
    }

    /// <summary>Cliente sin identificar en una boleta: tipo 0 o «-», o número vacío, «-» o solo ceros.</summary>
    public static bool EsAnonimo(ClienteCpe cliente)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        return cliente.TipoDocumento is TipoDocumentoIdentidad.NoDomiciliadoSinRuc or "-"
            || string.IsNullOrWhiteSpace(cliente.NumeroDocumento)
            || cliente.NumeroDocumento.Trim() == "-"
            || cliente.NumeroDocumento.All(c => c == '0');
    }

    [GeneratedRegex(@"^\d{11}$")]
    private static partial Regex OnceDigitos();

    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex OchoDigitos();

    [GeneratedRegex(@"^[A-Za-z0-9]{1,12}$")]
    private static partial Regex Alfanumerico12();

    [GeneratedRegex(@"^[A-Za-z0-9\-]{1,15}$")]
    private static partial Regex Alfanumerico15();
}
