using PasheCPE.Pdf.Representacion;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static PasheCPE.Pdf.Disenos.ComponentesComunes;

namespace PasheCPE.Pdf.Disenos;

/// <summary>
/// Diseño de la guía de remisión remitente (TRD §22): cabecera, destinatario, datos del traslado (motivo, fecha,
/// modalidad, peso y bultos), partida y llegada, transportista o vehículos y conductores, documentos relacionados,
/// bienes y el QR que entrega SUNAT en el CDR. La guía transportista (31) se agrega con la Fase 10b.
/// </summary>
internal sealed class DisenoGuia(GuiaImpresa guia, EstiloPdf estilo) : IDocument
{
    private static readonly string[] _titulosBienes = ["N.°", "Código", "Descripción", "Unid.", "Cantidad"];

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"{FormatoImpresion.Titulo(TipoDocumentoSunat.GuiaDeRemisionRemitente)} {guia.SerieNumero}",
        Author = guia.Remitente.Nombre,
        Creator = "PasheCPE",
        Producer = "PasheCPE.Pdf",
    };

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container) =>
        container.Page(pagina => Pagina(
            pagina,
            estilo,
            c => Cabecera(c, estilo, guia.Remitente, FormatoImpresion.Titulo(TipoDocumentoSunat.GuiaDeRemisionRemitente), guia.SerieNumero),
            Contenido));

    private void Contenido(IContainer contenedor) => contenedor.Column(columna =>
    {
        columna.Spacing(4, Unit.Millimetre);
        columna.Item().Row(fila =>
        {
            fila.RelativeItem().Element(c => Bloque(c, estilo, "DESTINATARIO", cuerpo => Datos(cuerpo,
            [
                ("Nombre", guia.Destinatario.Nombre),
                ("Documento", Documento(guia.Destinatario)),
                ("Comprador", guia.Comprador is { } comprador ? $"{comprador.Nombre} ({Documento(comprador)})" : null),
                ("Proveedor", guia.Proveedor is { } proveedor ? $"{proveedor.Nombre} ({Documento(proveedor)})" : null),
            ], 20)));
            fila.ConstantItem(4, Unit.Millimetre);
            fila.RelativeItem().Element(c => Bloque(c, estilo, "DATOS DEL TRASLADO", cuerpo => Datos(cuerpo,
            [
                ("Fecha de emisión", FormatoImpresion.Fecha(guia.FechaEmision) + (guia.HoraEmision is { } h ? " " + FormatoImpresion.Hora(h) : string.Empty)),
                ("Inicio del traslado", guia.FechaTraslado is { } f ? FormatoImpresion.Fecha(f) : null),
                ("Motivo", guia.CodigoMotivo is null ? guia.Motivo : $"{guia.CodigoMotivo} - {guia.Motivo}"),
                ("Modalidad", guia.CodigoModalidad is null ? null : $"{guia.CodigoModalidad} - {guia.Modalidad}"),
                ("Peso bruto total", guia.PesoBruto is { } peso ? $"{FormatoImpresion.Cantidad(peso)} {guia.UnidadPeso}" : null),
                ("Bultos", guia.Bultos),
            ], 30)));
        });

        columna.Item().Element(c => Bloque(c, estilo, "PUNTOS DE PARTIDA Y LLEGADA", cuerpo => Datos(cuerpo,
        [
            ("Partida", guia.Partida),
            ("Llegada", guia.Llegada),
        ], 16)));

        columna.Item().Element(c => Bloque(c, estilo, "TRANSPORTE", cuerpo => cuerpo.Column(transporte =>
        {
            transporte.Item().Element(d => Datos(d,
            [
                ("Transportista", guia.Transportista is { } t ? $"{t.Nombre} ({Documento(t)})" : null),
                ("Registro MTC", guia.Transportista?.RegistroMtc),
                ("Vehículo(s)", guia.Vehiculos.Count == 0 ? null : string.Join(", ", guia.Vehiculos)),
                ("Indicadores", guia.Indicadores.Count == 0 ? null : string.Join(", ", guia.Indicadores)),
            ], 26));
            foreach (var conductor in guia.Conductores)
            {
                transporte.Item().Text(t =>
                {
                    t.Span(conductor.Principal ? "Conductor: " : "Conductor secundario: ").Bold();
                    t.Span($"{conductor.Nombre} ({FormatoImpresion.DocumentoIdentidad(conductor.TipoDocumento)} {conductor.NumeroDocumento})");
                    if (conductor.Licencia is not null)
                    {
                        t.Span($" · Licencia {conductor.Licencia}");
                    }
                });
            }
        })));

        if (guia.DocumentosRelacionados.Count > 0)
        {
            columna.Item().Element(c => Bloque(c, estilo, "DOCUMENTOS RELACIONADOS", cuerpo => cuerpo.Column(documentos =>
            {
                foreach (var documento in guia.DocumentosRelacionados)
                {
                    documentos.Item().Text(documento.Emisor is null ? $"{documento.Tipo} {documento.Numero}" : $"{documento.Tipo} {documento.Numero} (RUC {documento.Emisor})");
                }
            })));
        }

        columna.Item().Element(Bienes);
        if (guia.Observaciones is not null)
        {
            columna.Item().Element(c => Bloque(c, estilo, "OBSERVACIONES", cuerpo => cuerpo.Text(guia.Observaciones)));
        }

        InformacionAdicional(columna, estilo);
        columna.Item().Element(c => BloqueQr(c, guia.EnlaceQr, guia.ValorResumen, FormatoImpresion.RepresentacionImpresa(TipoDocumentoSunat.GuiaDeRemisionRemitente), guia.Estado));
    });

    private void Bienes(IContainer contenedor) => contenedor.Table(tabla =>
    {
        tabla.ColumnsDefinition(c =>
        {
            c.ConstantColumn(10, Unit.Millimetre);
            c.ConstantColumn(22, Unit.Millimetre);
            c.RelativeColumn();
            c.ConstantColumn(16, Unit.Millimetre);
            c.ConstantColumn(22, Unit.Millimetre);
        });

        tabla.Header(h =>
        {
            foreach (var titulo in _titulosBienes)
            {
                h.Cell().Background(estilo.ColorPrincipal).PaddingVertical(3).PaddingHorizontal(2).AlignCenter()
                    .Text(titulo).FontSize(7.5f).Bold().FontColor(Colors.White);
            }
        });

        foreach (var bien in guia.Bienes)
        {
            Celda(tabla).AlignCenter().Text(bien.Numero.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Celda(tabla).Text(bien.Codigo ?? string.Empty);
            Celda(tabla).Text(bien.Descripcion);
            Celda(tabla).AlignCenter().Text(bien.Unidad);
            Celda(tabla).AlignRight().Text(FormatoImpresion.Cantidad(bien.Cantidad));
        }
    });

    private static IContainer Celda(TableDescriptor tabla) =>
        tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2.5f).PaddingHorizontal(2);
}
