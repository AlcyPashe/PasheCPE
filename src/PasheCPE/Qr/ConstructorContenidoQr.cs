using System.Globalization;
using Microsoft.Extensions.Options;
using PasheCPE.Tributos;

namespace PasheCPE.Qr;

/// <summary>
/// Contenido del QR de factura y boleta (PRD RF-11, TRD §12):
/// <c>RUC|TIPO|SERIE|NUMERO|IGV|TOTAL|FECHA|TIPODOCADQ|NUMDOCADQ|VALORRESUMEN</c>, montos con 2 decimales y punto,
/// fecha yyyy-MM-dd. Los ceros del número y el «|» final dependen de <see cref="OpcionesPasheCpe.Qr"/>.
/// </summary>
internal sealed class ConstructorContenidoQr(IOptions<OpcionesPasheCpe> opciones)
{
    public string Construir(CalculoComprobante calculo, string valorResumen)
    {
        ArgumentNullException.ThrowIfNull(calculo);
        ArgumentException.ThrowIfNullOrWhiteSpace(valorResumen);
        var qr = opciones.Value.Qr;
        var cpe = calculo.Comprobante;
        var igv = calculo.Subtotales.Where(s => s.Tributo == TributoSunat.Igv).Sum(s => s.Monto);
        string[] campos =
        [
            calculo.Emisor.Ruc,
            cpe.TipoDocumento,
            cpe.Serie,
            cpe.Numero.ToString(qr.CerosNumero ? "00000000" : "0", CultureInfo.InvariantCulture),
            Monto(igv),
            Monto(calculo.ImporteTotal),
            calculo.FechaEmision.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            cpe.Cliente.TipoDocumento,
            cpe.Cliente.NumeroDocumento,
            valorResumen,
        ];
        var contenido = string.Join('|', campos);
        return qr.SeparadorFinal ? contenido + "|" : contenido;
    }

    private static string Monto(decimal monto) => monto.ToString("0.00", CultureInfo.InvariantCulture);
}
