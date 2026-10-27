using PasheCPE.Pdf.Representacion;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static PasheCPE.Pdf.Disenos.ComponentesComunes;

namespace PasheCPE.Pdf.Disenos;

/// <summary>
/// Diseño de factura, boleta, nota de crédito y nota de débito (TRD §22): cabecera, cliente y datos del comprobante,
/// documento modificado y motivo (notas), detalle (el encabezado se repite en cada página), cuotas, detracción,
/// leyendas y monto en letras, totales por afectación y bloque del QR.
/// </summary>
internal sealed class DisenoComprobante(ComprobanteImpreso cpe, EstiloPdf estilo) : IDocument
{
    private static readonly string[] _titulosDetalle = ["Cantidad", "Unid.", "Código", "Descripción", "V. unitario", "P. unitario", "Valor venta"];

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"{FormatoImpresion.Titulo(cpe.TipoDocumento)} {cpe.SerieNumero}",
        Author = cpe.Emisor.Nombre,
        Creator = "PasheCPE",
        Producer = "PasheCPE.Pdf",
    };

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container) =>
        container.Page(pagina => Pagina(
            pagina,
            estilo,
            c => Cabecera(c, estilo, cpe.Emisor, FormatoImpresion.Titulo(cpe.TipoDocumento), cpe.SerieNumero),
            Contenido));

    private bool EsNota => cpe.TipoDocumento is TipoDocumentoSunat.NotaDeCredito or TipoDocumentoSunat.NotaDeDebito;

    private void Contenido(IContainer contenedor) => contenedor.Column(columna =>
    {
        columna.Spacing(4, Unit.Millimetre);
        columna.Item().Element(DatosGenerales);
        if (EsNota)
        {
            columna.Item().Element(c => Bloque(c, estilo, "DOCUMENTO QUE MODIFICA", cuerpo => Datos(cuerpo,
            [
                ("Documento", cpe.DocumentoModificado is { } m ? $"{NombreTipo(m.Tipo)} {m.Numero}" : null),
                ("Motivo", cpe.CodigoMotivo is null ? null : $"{cpe.CodigoMotivo} - {cpe.Motivo}"),
                ("Sustento", cpe.Sustento),
            ])));
        }

        columna.Item().Element(Detalle);
        columna.Item().Row(fila =>
        {
            fila.RelativeItem().Column(Izquierda);
            fila.ConstantItem(5, Unit.Millimetre);
            fila.ConstantItem(72, Unit.Millimetre).AlignTop().Element(Totales);
        });
        columna.Item().Element(c => BloqueQr(c, cpe.ContenidoQr, cpe.ValorResumen, FormatoImpresion.RepresentacionImpresa(cpe.TipoDocumento), cpe.Estado));
    });

    private void DatosGenerales(IContainer contenedor) => contenedor.Border(0.6f).BorderColor(Borde).Padding(4).Row(fila =>
    {
        fila.RelativeItem(3).Element(c => Datos(c,
        [
            (cpe.TipoDocumento == TipoDocumentoSunat.BoletaDeVenta ? "Cliente" : "Señor(es)", cpe.Cliente.Nombre),
            ("Documento", Documento(cpe.Cliente)),
            ("Dirección", cpe.Cliente.Direccion),
            ("Orden de compra", cpe.OrdenCompra),
            ("Guía(s)", cpe.Guias.Count == 0 ? null : string.Join(", ", cpe.Guias.Select(g => g.Numero))),
        ], 24));
        fila.ConstantItem(4, Unit.Millimetre);
        fila.RelativeItem(2).Element(c => Datos(c,
        [
            ("Fecha de emisión", FormatoImpresion.Fecha(cpe.FechaEmision) + (cpe.HoraEmision is { } h ? " " + FormatoImpresion.Hora(h) : string.Empty)),
            ("Vencimiento", cpe.FechaVencimiento is { } v ? FormatoImpresion.Fecha(v) : null),
            ("Moneda", $"{cpe.Moneda} ({FormatoImpresion.Simbolo(cpe.Moneda)})"),
            ("Forma de pago", cpe.FormaPago),
        ], 28));
    });

    private void Detalle(IContainer contenedor) => contenedor.Table(tabla =>
    {
        tabla.ColumnsDefinition(c =>
        {
            c.ConstantColumn(16, Unit.Millimetre);
            c.ConstantColumn(11, Unit.Millimetre);
            c.ConstantColumn(18, Unit.Millimetre);
            c.RelativeColumn();
            c.ConstantColumn(22, Unit.Millimetre);
            c.ConstantColumn(22, Unit.Millimetre);
            c.ConstantColumn(23, Unit.Millimetre);
        });

        tabla.Header(h =>
        {
            foreach (var titulo in _titulosDetalle)
            {
                h.Cell().Background(estilo.ColorPrincipal).PaddingVertical(3).PaddingHorizontal(2).AlignCenter()
                    .Text(titulo).FontSize(7.5f).Bold().FontColor(Colors.White);
            }
        });

        foreach (var linea in cpe.Lineas)
        {
            Celda(tabla).AlignRight().Text(FormatoImpresion.Cantidad(linea.Cantidad));
            Celda(tabla).AlignCenter().Text(linea.Unidad);
            Celda(tabla).Text(linea.Codigo ?? string.Empty);
            Celda(tabla).Column(d =>
            {
                d.Item().Text(linea.Descripcion);
                if (linea.EsGratuita)
                {
                    d.Item().Text("Transferencia gratuita").FontSize(7).Italic().FontColor(Tenue);
                }

                if (linea.Descuento > 0)
                {
                    d.Item().Text($"Descuento: {FormatoImpresion.Numero(linea.Descuento)}").FontSize(7).FontColor(Tenue);
                }
            });
            Celda(tabla).AlignRight().Text(FormatoImpresion.Precio(linea.ValorUnitario));
            Celda(tabla).AlignRight().Text(FormatoImpresion.Precio(linea.PrecioUnitario));
            Celda(tabla).AlignRight().Text(FormatoImpresion.Numero(linea.ValorVenta));
        }
    });

    private static IContainer Celda(TableDescriptor tabla) =>
        tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2.5f).PaddingHorizontal(2);

    /// <summary>Monto en letras, otras leyendas, cuotas y detracción.</summary>
    private void Izquierda(ColumnDescriptor columna)
    {
        columna.Spacing(3, Unit.Millimetre);
        var enLetras = cpe.Leyendas.FirstOrDefault(l => l.Codigo == CodigoLeyenda.MontoEnLetras);
        if (enLetras is not null)
        {
            columna.Item().Text(t =>
            {
                t.Span("SON: ").Bold();
                t.Span(enLetras.Texto);
            });
        }

        foreach (var leyenda in cpe.Leyendas.Where(l => l.Codigo != CodigoLeyenda.MontoEnLetras))
        {
            columna.Item().Text(FormatoImpresion.Leyenda(leyenda.Codigo, leyenda.Texto)).FontSize(7.5f).FontColor(Tenue);
        }

        if (cpe.Cuotas.Count > 0)
        {
            columna.Item().Element(c => Bloque(c, estilo, "CUOTAS", cuerpo => cuerpo.Column(cuotas =>
            {
                if (cpe.MontoPendiente is { } pendiente)
                {
                    cuotas.Item().PaddingBottom(2).Text($"Monto neto pendiente de pago: {FormatoImpresion.Monto(pendiente, cpe.Moneda)}").Bold();
                }

                foreach (var cuota in cpe.Cuotas)
                {
                    cuotas.Item().Row(fila =>
                    {
                        fila.RelativeItem().Text(cuota.Numero);
                        fila.RelativeItem().Text(cuota.Vencimiento is { } v ? FormatoImpresion.Fecha(v) : string.Empty);
                        fila.RelativeItem().AlignRight().Text(FormatoImpresion.Monto(cuota.Monto, cpe.Moneda));
                    });
                }
            })));
        }

        if (cpe.Detraccion is { } detraccion)
        {
            columna.Item().Element(c => Bloque(c, estilo, "OPERACIÓN SUJETA AL SPOT (DETRACCIÓN)", cuerpo => Datos(cuerpo,
            [
                ("Bien o servicio", detraccion.Codigo is null ? null : $"{detraccion.Codigo} - {detraccion.Descripcion}"),
                ("Porcentaje", detraccion.Porcentaje is { } p ? FormatoImpresion.Porcentaje(p) : null),
                ("Monto", detraccion.Monto is { } m ? FormatoImpresion.Monto(m, "PEN") : null),
                ("Cta. Banco de la Nación", detraccion.Cuenta),
                ("Medio de pago", detraccion.MedioPago),
            ], 36)));
        }
    }

    /// <summary>Totales por afectación: solo las filas con importe, y siempre el importe total.</summary>
    private void Totales(IContainer contenedor)
    {
        var t = cpe.Totales;
        (string Etiqueta, decimal Monto)[] filas =
        [
            ("Op. gravadas", t.Gravado),
            ("Op. exoneradas", t.Exonerado),
            ("Op. inafectas", t.Inafecto),
            ("Exportación", t.Exportacion),
            ("Op. gratuitas", t.Gratuito),
            ("Descuentos globales", t.Descuentos),
            ("Cargos", t.Cargos),
            ("Anticipos", t.Anticipos),
            ("ISC", t.Isc),
            ("IGV", t.Igv),
            ("ICBPER", t.Icbper),
            ("Otros tributos", t.OtrosTributos),
            ("Redondeo", t.Redondeo),
        ];

        contenedor.Border(0.6f).BorderColor(Borde).Column(columna =>
        {
            foreach (var (etiqueta, monto) in filas.Where(f => f.Monto != 0 || f.Etiqueta == "IGV" && cpe.Totales.Gravado != 0))
            {
                columna.Item().PaddingHorizontal(4).PaddingVertical(1.5f).Row(fila =>
                {
                    fila.RelativeItem().Text(etiqueta);
                    fila.AutoItem().Text(FormatoImpresion.Monto(monto, cpe.Moneda));
                });
            }

            columna.Item().Background(estilo.ColorPrincipal).PaddingHorizontal(4).PaddingVertical(3).Row(fila =>
            {
                fila.RelativeItem().Text("IMPORTE TOTAL").Bold().FontColor(Colors.White);
                fila.AutoItem().Text(FormatoImpresion.Monto(t.ImporteTotal, cpe.Moneda)).Bold().FontColor(Colors.White);
            });
        });
    }

    private static string NombreTipo(string tipo) => tipo switch
    {
        TipoDocumentoSunat.Factura => "FACTURA",
        TipoDocumentoSunat.BoletaDeVenta => "BOLETA DE VENTA",
        _ => tipo,
    };
}
