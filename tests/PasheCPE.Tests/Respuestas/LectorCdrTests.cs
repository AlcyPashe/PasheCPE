using System.IO.Compression;
using System.Text;
using PasheCPE.Respuestas;
using PasheCPE.Tests.Dorados;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Respuestas;

public sealed class LectorCdrTests
{
    public static TheoryData<string, string, string> Dorados => new()
    {
        { "01-Factura", "R-20611619767-01-F001-3565.zip", "F001-3565" },
        { "01-Factura", "R-20611619767-01-F006-180006.zip", "F006-180006" },
        { "07-NotaCredito", "R-20611619767-07-F001-840.zip", "F001-840" },
        { "07-NotaCredito", "R-20611619767-07-F001-841.zip", "F001-841" },
        { "09-GRE", "R-20611619767-09-T001-31093.zip", "T001-31093" },
        { "RC-Resumen", "R-20611619767-RC-20261021-11.zip", "RC-20261021-11" },
        { "RA-Baja", "R-20493400364-RA-20261027-1.zip", "RA-20261027-1" },
    };

    [Theory]
    [MemberData(nameof(Dorados))]
    public void Leer_CdrDorado_AceptadoConReferenciaYFecha(string carpeta, string archivo, string referencia)
    {
        var cdr = LectorCdr.Leer(File.ReadAllBytes(Path.Combine(LectorDorado.Carpeta(carpeta), archivo)), verificarFirma: false)!;

        Assert.Equal("0", cdr.Codigo);
        Assert.Equal(referencia, cdr.Referencia);
        Assert.NotNull(cdr.Fecha);
        Assert.NotNull(cdr.Hora);
        Assert.Contains("aceptad", cdr.Descripcion, StringComparison.OrdinalIgnoreCase);
        Assert.Null(cdr.FirmaValida);
    }

    [Fact]
    public void Leer_CdrConCarpetaDummy_LaIgnora()
    {
        var cdr = LectorCdr.Leer(RespuestasSoap.CdrDoradoAceptado(), verificarFirma: false)!;

        Assert.Equal("F001-3565", cdr.Referencia);
        Assert.Empty(cdr.Notas);
    }

    [Fact]
    public void Leer_CdrGre_NotasYEnlaceDelQr()
    {
        var cdr = LectorCdr.Leer(RespuestasSoap.CdrDoradoObservado(), verificarFirma: false)!;

        Assert.Equal(["4391", "4382"], cdr.Notas.Select(n => n.Codigo));
        Assert.StartsWith("El Numero de Registro MTC", cdr.Notas[0].Descripcion, StringComparison.Ordinal);
        Assert.StartsWith("https://e-factura.sunat.gob.pe/v1/contribuyente/gre/comprobantes/descargaqr?hashqr=", cdr.EnlaceQr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("01-Factura", "R-20611619767-01-F001-3565.zip")]
    [InlineData("01-Factura", "R-20611619767-01-F006-180006.zip")]
    [InlineData("07-NotaCredito", "R-20611619767-07-F001-840.zip")]
    [InlineData("07-NotaCredito", "R-20611619767-07-F001-841.zip")]
    [InlineData("RC-Resumen", "R-20611619767-RC-20261021-11.zip")]
    [InlineData("RC-Resumen", "R-20611619767-RC-20261022-7.zip")]
    public void Leer_VerificandoFirma_CdrSoapConFirmaDeSunatValida(string carpeta, string archivo)
    {
        var cdr = LectorCdr.Leer(File.ReadAllBytes(Path.Combine(LectorDorado.Carpeta(carpeta), archivo)), verificarFirma: true)!;

        Assert.True(cdr.FirmaValida);
    }

    /// <summary>
    /// Hallazgo (Fase 8): beta no firma sus CDR; la firma trae textos de relleno («Private key 'BetaPublicCert' not up»).
    /// Con <see cref="OpcionesPasheCpe.VerificarFirmaCdr"/> un CDR de beta queda observado (PCPE-4009).
    /// </summary>
    [Fact]
    public void Leer_VerificandoFirma_CdrDeBetaSinFirmaReal()
    {
        var cdr = LectorCdr.Leer(File.ReadAllBytes(Path.Combine(LectorDorado.Carpeta("RA-Baja"), "R-20493400364-RA-20261027-1.zip")), verificarFirma: true)!;

        Assert.Equal("0", cdr.Codigo);
        Assert.False(cdr.FirmaValida);
    }

    /// <summary>
    /// Hallazgo (Fase 5): en los CDR de la GRE (API REST) la firma del SignedInfo es válida, pero el digest del documento
    /// no coincide con la canonicalización declarada (SUNAT parece modificar el XML después de firmarlo). Se revisa en
    /// la Fase 10; mientras tanto, con <see cref="OpcionesPasheCpe.VerificarFirmaCdr"/> la GRE quedaría observada.
    /// </summary>
    [Theory]
    [InlineData("R-20611619767-09-T001-31093.zip")]
    [InlineData("R-20611619767-09-T001-31094.zip")]
    public void Leer_VerificandoFirma_CdrGreNoCoincideElDigest(string archivo)
    {
        var cdr = LectorCdr.Leer(File.ReadAllBytes(Path.Combine(LectorDorado.Carpeta("09-GRE"), archivo)), verificarFirma: true)!;

        Assert.False(cdr.FirmaValida);
    }

    [Fact]
    public void Leer_CdrAlterado_FirmaNoValida()
    {
        var alterado = Reempaquetar(RespuestasSoap.CdrDoradoAceptado(), x => x.Replace("ha sido aceptada", "ha sido ACEPTADA", StringComparison.Ordinal));

        var cdr = LectorCdr.Leer(alterado, verificarFirma: true)!;

        Assert.False(cdr.FirmaValida);
    }

    [Fact]
    public void Leer_NotaSinCodigo_ConservaElTexto()
    {
        var cdr = LectorCdr.Leer(RespuestasSoap.Cdr("F001-1", "0", "Aceptada", "Texto libre"), verificarFirma: false)!;

        var nota = Assert.Single(cdr.Notas);
        Assert.Equal((string.Empty, "Texto libre"), (nota.Codigo, nota.Descripcion));
    }

    [Theory]
    [InlineData(new byte[] { 1, 2, 3 })]
    [InlineData(new byte[0])]
    public void Leer_ZipInvalido_DevuelveNull(byte[] zip)
    {
        Assert.Null(LectorCdr.Leer(zip, verificarFirma: false));
    }

    private static byte[] Reempaquetar(byte[] zip, Func<string, string> cambio)
    {
        using var origen = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var entrada = origen.Entries.Single(e => e.Name.EndsWith(".xml", StringComparison.Ordinal));
        string xml;
        using (var lector = new StreamReader(entrada.Open(), Encoding.UTF8))
        {
            xml = lector.ReadToEnd();
        }

        using var memoria = new MemoryStream();
        using (var destino = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var flujo = destino.CreateEntry(entrada.FullName).Open();
            flujo.Write(new UTF8Encoding(false).GetBytes(cambio(xml)));
        }

        return memoria.ToArray();
    }
}
