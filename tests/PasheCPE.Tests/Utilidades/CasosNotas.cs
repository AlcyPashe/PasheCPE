namespace PasheCPE.Tests.Utilidades;

/// <summary>
/// Notas de crédito y débito de ejemplo que cubren los motivos de la Fase 7 sobre factura y boleta. Se usan para
/// comprobar que el XML generado pasa el XSD, las XSL de SUNAT y las reglas complementarias sin errores ni
/// observaciones.
/// </summary>
internal static class CasosNotas
{
    private static readonly DateOnly _fecha = new(2026, 10, 22);
    private static readonly DateOnly _fecha2027 = new(2027, 1, 15);

    public static IReadOnlyDictionary<string, CasoNota> Todos { get; } = new Dictionary<string, CasoNota>(StringComparer.Ordinal)
    {
        ["NcAnulacionFactura"] = new(CasosTributarios.Gravado(), Credito(MotivoNotaCredito.AnulacionDeLaOperacion, CasosTributarios.Linea(2, 59m), CasosTributarios.Linea(1, 11.80m))),
        ["NcDevolucionTotalExonerada"] = new(Ejemplos.Emisor(), Credito(MotivoNotaCredito.DevolucionTotal, CasosTributarios.Linea(1, 46.18m))),
        ["NcDevolucionPorItemMixta"] = new(CasosTributarios.Gravado(), Credito(
            MotivoNotaCredito.DevolucionPorItem,
            CasosTributarios.Linea(1, 118m),
            CasosTributarios.Linea(2, 25m) with { Afectacion = AfectacionIgv.ExoneradoOneroso },
            CasosTributarios.Linea(1, 40m) with { Afectacion = AfectacionIgv.InafectoOneroso })),
        ["NcCorreccionDescripcionEnCero"] = new(CasosTributarios.Gravado(), Credito(MotivoNotaCredito.CorreccionDescripcion, CasosTributarios.Linea(1, 0m) with { Descripcion = "ARROZ EXTRA X 5 KG" })),
        ["NcAjusteCuotas"] = new(CasosTributarios.Gravado(), Credito(MotivoNotaCredito.CorreccionMontoNetoPendientePago, CasosTributarios.Linea(1, 0m)) with
        {
            FormaPago = new FormaPagoCpe
            {
                Tipo = TipoFormaPago.Credito,
                Cuotas =
                [
                    new CuotaCpe { Monto = 177m, FechaVencimiento = new DateOnly(2026, 12, 22) },
                    new CuotaCpe { Monto = 177m, FechaVencimiento = new DateOnly(2027, 1, 22) },
                ],
            },
        }),
        ["NcIcbper"] = new(CasosTributarios.Gravado(), Credito(MotivoNotaCredito.DevolucionPorItem, CasosTributarios.Linea(2, 0.20m) with { Descripcion = "BOLSA PLÁSTICA", BolsasIcbper = 2 })),
        ["NcBoleta"] = new(CasosTributarios.Gravado(), Boleta(Credito(MotivoNotaCredito.AnulacionDeLaOperacion, CasosTributarios.Linea(1, 59m)))),
        ["NcBoletaExonerada"] = new(Ejemplos.Emisor(), Boleta(Credito(MotivoNotaCredito.DisminucionEnElValor, CasosTributarios.Linea(1, 5m)))),
        ["NcContratoColaboracion2027"] = new(CasosTributarios.Gravado(), Credito(MotivoNotaCredito.DevolucionTotal, CasosTributarios.Linea(1, 118m)) with
        {
            FechaEmision = _fecha2027,
            DocumentoModificado = Factura(_fecha2027),
            ContratoColaboracion = new ContratoColaboracionCpe { Tipo = "1", Numero = "CCE-2027-001", Descripcion = "CONSORCIO VIAL DE PRUEBA", Porcentaje = 50m },
        }, _fecha2027),
        ["NdIntereses"] = new(CasosTributarios.Gravado(), Debito(MotivoNotaDebito.InteresesPorMora, CasosTributarios.Linea(1, 23.60m))),
        ["NdIntereses2027"] = new(CasosTributarios.Gravado(), Debito(MotivoNotaDebito.InteresesPorMora, CasosTributarios.Linea(1, 23.60m)) with
        {
            FechaEmision = _fecha2027,
            DocumentoModificado = Factura(_fecha2027),
        }, _fecha2027),
        ["NdAumentoExonerada"] = new(Ejemplos.Emisor(), Debito(MotivoNotaDebito.AumentoEnElValor, CasosTributarios.Linea(1, 10m))),
        ["NdBoleta"] = new(CasosTributarios.Gravado(), Boleta(Debito(MotivoNotaDebito.AumentoEnElValor, CasosTributarios.Linea(1, 11.80m)))),
        ["NdOtrosConceptos"] = new(CasosTributarios.Gravado(), Debito(MotivoNotaDebito.PenalidadesOtrosConceptos, CasosTributarios.Linea(1, 50m))),
        ["NdPenalidades2027"] = new(CasosTributarios.Gravado(), Debito(MotivoNotaDebito.Penalidades, CasosTributarios.Linea(1, 300m) with { Afectacion = AfectacionIgv.InafectoOneroso }) with
        {
            FechaEmision = _fecha2027,
            DocumentoModificado = Factura(_fecha2027),
        }, _fecha2027),
        ["NdPenalidadesSinDocumento2027"] = new(CasosTributarios.Gravado(), Debito(MotivoNotaDebito.Penalidades, CasosTributarios.Linea(1, 300m) with { Afectacion = AfectacionIgv.InafectoOneroso }) with
        {
            FechaEmision = _fecha2027,
            DocumentoModificado = null,
        }, _fecha2027),
    };

    /// <summary>Factura F001-1 que modifican las notas (emitida el 22/10/2026 o en la fecha indicada).</summary>
    public static DocumentoModificadoCpe Factura(DateOnly? fecha = null) =>
        new() { TipoDocumento = TipoDocumentoSunat.Factura, Serie = "F001", Numero = 1, FechaEmision = fecha ?? _fecha };

    /// <summary>Boleta B001-1 que modifican las notas de serie B.</summary>
    public static DocumentoModificadoCpe BoletaModificada(DateOnly? fecha = null) =>
        new() { TipoDocumento = TipoDocumentoSunat.BoletaDeVenta, Serie = "B001", Numero = 1, FechaEmision = fecha ?? _fecha };

    public static NotaCpe Credito(string motivo, params LineaCpe[] lineas) => Nota(TipoDocumentoSunat.NotaDeCredito, "FC01", motivo, lineas);

    public static NotaCpe Debito(string motivo, params LineaCpe[] lineas) => Nota(TipoDocumentoSunat.NotaDeDebito, "FD01", motivo, lineas);

    /// <summary>La misma nota sobre la boleta B001-1, con serie B y cliente sin documento.</summary>
    public static NotaCpe Boleta(NotaCpe nota) => nota with
    {
        Serie = nota.TipoDocumento == TipoDocumentoSunat.NotaDeCredito ? "BC01" : "BD01",
        DocumentoModificado = BoletaModificada(),
        Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.NoDomiciliadoSinRuc, NumeroDocumento = "-", RazonSocial = "CLIENTES VARIOS" },
    };

    private static NotaCpe Nota(string tipo, string serie, string motivo, LineaCpe[] lineas) => new()
    {
        TipoDocumento = tipo,
        Serie = serie,
        Numero = 1,
        FechaEmision = _fecha,
        HoraEmision = new TimeOnly(11, 0, 0),
        Motivo = motivo,
        Sustento = "SUSTENTO DE PRUEBA",
        DocumentoModificado = Factura(),
        Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Ruc, NumeroDocumento = "20987654326", RazonSocial = "CLIENTE S.A." },
        Lineas = lineas,
    };
}

/// <summary>Emisor y nota de un caso, con la fecha de emisión (también la del reloj de la prueba).</summary>
internal sealed record CasoNota(EmisorCpe Emisor, NotaCpe Nota, DateOnly? Fecha = null)
{
    public DateOnly FechaEmision => Fecha ?? Nota.FechaEmision ?? new DateOnly(2026, 10, 22);
}
