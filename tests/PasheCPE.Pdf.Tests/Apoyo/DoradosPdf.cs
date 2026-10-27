using System.Globalization;
using System.Xml.Linq;
using PasheCPE.Tests.Dorados;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Pdf.Tests.Apoyo;

/// <summary>Un documento dorado (XML firmado y CDR aceptados) de <c>tests/Datos/Dorados</c>.</summary>
internal sealed record DoradoPdf(string Carpeta, string Nombre)
{
    public string RutaXml => Path.Combine(LectorDorado.Carpeta(Carpeta), Nombre + ".xml");

    public string RutaCdr => Path.Combine(LectorDorado.Carpeta(Carpeta), "R-" + Nombre + ".zip");

    /// <summary>Referencia con la fecha de emisión del XML (define la carpeta yyyy\MM).</summary>
    public ReferenciaCpe Referencia()
    {
        var partes = Nombre.Split('-');
        var emision = XDocument.Load(RutaXml).Root!.Elements().First(e => e.Name.LocalName == "IssueDate").Value;
        var fecha = DateOnly.ParseExact(emision, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var numero = long.Parse(partes[3], CultureInfo.InvariantCulture);
        return partes[1] == TipoDocumentoSunat.GuiaDeRemisionRemitente
            ? ReferenciaCpe.Guia(partes[0], partes[1], partes[2], numero, fecha)
            : ReferenciaCpe.Comprobante(partes[0], partes[1], partes[2], numero, fecha);
    }
}

/// <summary>Los seis documentos de la aceptación de la Fase 11 y su colocación en un almacén temporal (Dorados no se modifica).</summary>
internal static class DoradosPdf
{
    public static readonly DoradoPdf FacturaExoneradaGratuita = new("01-Factura", "20611619767-01-F001-3565");
    public static readonly DoradoPdf FacturaExonerada = new("Beta-20493400364", "20493400364-01-F001-2");
    public static readonly DoradoPdf Boleta = new("Beta-20493400364", "20493400364-03-B001-3");
    public static readonly DoradoPdf NotaCredito = new("07-NotaCredito", "20611619767-07-F001-840");
    public static readonly DoradoPdf NotaDebito = new("Beta-20493400364", "20493400364-08-FD01-23");
    public static readonly DoradoPdf Guia = new("09-GRE", "20611619767-09-T001-31093");

    public static IReadOnlyDictionary<string, DoradoPdf> Todos { get; } = new Dictionary<string, DoradoPdf>(StringComparer.Ordinal)
    {
        [nameof(FacturaExoneradaGratuita)] = FacturaExoneradaGratuita,
        [nameof(FacturaExonerada)] = FacturaExonerada,
        [nameof(Boleta)] = Boleta,
        [nameof(NotaCredito)] = NotaCredito,
        [nameof(NotaDebito)] = NotaDebito,
        [nameof(Guia)] = Guia,
    };

    /// <summary>Copia el XML a FIRMA y, si se pide, el CDR a RPTA del almacén temporal; devuelve la referencia.</summary>
    public static ReferenciaCpe Colocar(AlmacenTemporal almacen, DoradoPdf dorado, bool conCdr = true)
    {
        var referencia = dorado.Referencia();
        Copiar(dorado.RutaXml, Path.Combine(almacen.Ruta, "FIRMA", Periodo(referencia), dorado.Nombre + ".xml"));
        if (conCdr)
        {
            Copiar(dorado.RutaCdr, Path.Combine(almacen.Ruta, "RPTA", Periodo(referencia), "R-" + dorado.Nombre + ".zip"));
        }

        return referencia;
    }

    public static string Periodo(ReferenciaCpe referencia) =>
        Path.Combine(referencia.Fecha.Year.ToString("0000", CultureInfo.InvariantCulture), referencia.Fecha.Month.ToString("00", CultureInfo.InvariantCulture));

    private static void Copiar(string origen, string destino)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        File.Copy(origen, destino, overwrite: true);
    }
}
