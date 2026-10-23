using System.Xml.Linq;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;
using static PasheCPE.Tests.Utilidades.XmlDePrueba;

namespace PasheCPE.Tests.Validacion;

/// <summary>Reglas complementarias del resumen diario vigentes desde el 01/01/2027 (TRD §9.4).</summary>
public sealed class ReglasResumenTests : IDisposable
{
    private static readonly XNamespace _sac = "urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1";
    private static readonly DateTimeOffset _ahora2027 = new(2027, 1, 5, 15, 0, 0, TimeSpan.Zero);

    private readonly ServiciosPrueba _servicios = new(ahora: _ahora2027);

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ResumenGenerado2027_PasaLasReglas()
    {
        var (xml, referencia) = await GenerarAsync();

        var resultado = _servicios.Obtener<ValidadorSunat>().Validar(xml, referencia);

        Assert.Empty(resultado.Errores);
        Assert.Empty(resultado.Observaciones);
    }

    [Theory]
    [InlineData("sinTasa", "2992")]
    [InlineData("tasa15", "3504")]
    [InlineData("formatoTasa", "3102")]
    [InlineData("sinTasaGratuitas", "2992")]
    [InlineData("razonSocial", "2022")]
    [InlineData("nombreGra", "3051")]
    public async Task Regla2027_Incumplida_Error(string caso, string codigo)
    {
        var (xml, referencia) = await GenerarAsync();
        var alterado = Alterar(xml, raiz => Romper(caso, raiz.Element(_sac + "SummaryDocumentsLine")!));

        var resultado = _servicios.Obtener<ValidadorSunat>().Validar(alterado, referencia);

        Assert.Contains(resultado.Errores, e => e.Codigo == codigo && e.Origen == OrigenError.ReglasComplementarias);
    }

    [Theory]
    [InlineData("1000")]
    [InlineData("9996")]
    public async Task IgvMalCalculado2027_Observacion4019(string tributo)
    {
        var (xml, referencia) = await GenerarAsync();
        var alterado = Alterar(xml, raiz => Monto(raiz.Element(_sac + "SummaryDocumentsLine")!, tributo, "50.00"));

        var resultado = _servicios.Obtener<ValidadorSunat>().Validar(alterado, referencia);

        Assert.Contains(resultado.Observaciones, o => o.Codigo == "4019");
    }

    [Fact]
    public async Task Antes2027_LasReglasNoAplican()
    {
        using var servicios = new ServiciosPrueba();
        var emisor = CertificadoPrueba.Emisor(servicios.Almacen);
        var resultado = Assert.Single(await servicios.Obtener<IServicioCpe>().GenerarResumenAsync(emisor, Resumen(new DateOnly(2026, 10, 22)), Ct));
        var xml = await File.ReadAllBytesAsync(resultado.Rutas.Xml!, Ct);
        var alterado = Alterar(xml, raiz => Romper("tasa15", raiz.Element(_sac + "SummaryDocumentsLine")!));

        var validacion = servicios.Obtener<ValidadorSunat>().Validar(alterado, resultado.Referencia);

        Assert.DoesNotContain(validacion.Errores, e => e.Codigo == "3504");
    }

    private static void Romper(string caso, XElement linea)
    {
        var igv = Categoria(linea, "1000");
        switch (caso)
        {
            case "sinTasa":
                igv.Element(Cbc + "Percent")!.Remove();
                break;
            case "tasa15":
                igv.Element(Cbc + "Percent")!.Value = "15.00";
                break;
            case "formatoTasa":
                igv.Element(Cbc + "Percent")!.Value = "18.123456";
                break;
            case "sinTasaGratuitas":
                Categoria(linea, "9996").Element(Cbc + "Percent")!.Remove();
                break;
            case "razonSocial":
                linea.Element(Cac + "AccountingCustomerParty")!.Element(Cac + "Party")!.Element(Cac + "PartyLegalEntity")!.Element(Cbc + "RegistrationName")!.Value = "JUAN\nPÉREZ";
                break;
            default:
                Categoria(linea, "9996").Element(Cac + "TaxScheme")!.Element(Cbc + "Name")!.Value = "IGV";
                break;
        }
    }

    private static XElement Categoria(XElement linea, string tributo) =>
        linea.Elements(Cac + "TaxTotal").Single(t => t.Descendants(Cbc + "ID").Any(i => i.Value == tributo))
            .Element(Cac + "TaxSubtotal")!.Element(Cac + "TaxCategory")!;

    private static void Monto(XElement linea, string tributo, string monto)
    {
        var total = linea.Elements(Cac + "TaxTotal").Single(t => t.Descendants(Cbc + "ID").Any(i => i.Value == tributo));
        total.Element(Cbc + "TaxAmount")!.Value = monto;
        total.Element(Cac + "TaxSubtotal")!.Element(Cbc + "TaxAmount")!.Value = monto;
    }

    private static ResumenDiarioCpe Resumen(DateOnly fecha) => new()
    {
        FechaReferencia = fecha,
        Lineas =
        [
            new LineaResumenCpe
            {
                TipoDocumento = TipoDocumentoSunat.BoletaDeVenta,
                Serie = "B001",
                Numero = 1,
                Estado = EstadoItemResumen.Adicionar,
                Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Dni, NumeroDocumento = "12345678", RazonSocial = "JUAN PÉREZ" },
                TotalGravado = 100m,
                Igv = 18m,
                TotalGratuitoGravado = 10m,
                IgvGratuitas = 1.80m,
                ImporteTotal = 118m,
            },
        ],
    };

    private async Task<(byte[] Xml, ReferenciaCpe Referencia)> GenerarAsync()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        var resultado = Assert.Single(await _servicios.Obtener<IServicioCpe>().GenerarResumenAsync(emisor, Resumen(new DateOnly(2027, 1, 4)), Ct));
        Assert.True(resultado.Exito, string.Join("; ", resultado.Errores.Select(e => $"{e.Codigo} {e.Descripcion}")));
        return (await File.ReadAllBytesAsync(resultado.Rutas.Xml!, Ct), resultado.Referencia);
    }
}
