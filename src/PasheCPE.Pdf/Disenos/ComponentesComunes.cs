using PasheCPE.Pdf.Representacion;
using PasheCPE.Qr;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PasheCPE.Pdf.Disenos;

/// <summary>Marca ya resuelta para dibujar: logo validado y color principal.</summary>
internal sealed record EstiloPdf(MarcaEmisor Marca, byte[]? Logo, Color ColorPrincipal);

/// <summary>
/// Piezas comunes de los dos diseños (TRD §22): página A4 vertical, cabecera con logo, emisor y recuadro del RUC,
/// tipo y serie-número; bloques con título; QR de 3.5 × 3.5 cm con el valor resumen y el texto de representación
/// impresa; pie con el crédito de Pashe Soluciones y la paginación. Fuente Lato, embebida por QuestPDF.
/// </summary>
internal static class ComponentesComunes
{
    public const float LadoQrMm = 35f;
    public const float MargenMm = 12f;

    public static readonly Color Borde = Colors.Grey.Lighten1;
    public static readonly Color Tenue = Colors.Grey.Darken1;

    public static void Pagina(PageDescriptor pagina, EstiloPdf estilo, Action<IContainer> cabecera, Action<IContainer> contenido)
    {
        pagina.Size(PageSizes.A4);
        pagina.Margin(MargenMm, Unit.Millimetre);
        pagina.PageColor(Colors.White);
        pagina.DefaultTextStyle(t => t.FontSize(8.5f).FontColor(Colors.Black));
        pagina.Header().Element(cabecera);
        pagina.Content().PaddingTop(4, Unit.Millimetre).Element(contenido);
        pagina.Footer().Element(Pie);
    }

    /// <summary>Logo y datos del emisor a la izquierda; recuadro del RUC, título y serie-número a la derecha.</summary>
    public static void Cabecera(IContainer contenedor, EstiloPdf estilo, ParteImpresa emisor, string titulo, string serieNumero)
    {
        contenedor.Row(fila =>
        {
            fila.RelativeItem().Row(izquierda =>
            {
                if (estilo.Logo is { } logo)
                {
                    izquierda.ConstantItem(32, Unit.Millimetre).Height(24, Unit.Millimetre).AlignMiddle().Image(logo).FitArea();
                    izquierda.ConstantItem(3, Unit.Millimetre);
                }

                izquierda.RelativeItem().AlignMiddle().Column(datos =>
                {
                    datos.Spacing(1);
                    datos.Item().Text(emisor.NombreComercial ?? emisor.Nombre).FontSize(12).Bold().FontColor(estilo.ColorPrincipal);
                    if (emisor.NombreComercial is not null && emisor.NombreComercial != emisor.Nombre)
                    {
                        datos.Item().Text(emisor.Nombre).Bold();
                    }

                    if (emisor.Direccion is not null)
                    {
                        datos.Item().Text(emisor.Direccion).FontSize(7.5f);
                    }

                    var marca = estilo.Marca;
                    var contacto = new[]
                    {
                        marca.Telefonos.Count > 0 ? "Telf.: " + string.Join(" / ", marca.Telefonos) : null,
                        marca.Correo,
                        marca.Web,
                    }.Where(c => !string.IsNullOrWhiteSpace(c));
                    var linea = string.Join("  ·  ", contacto);
                    if (linea.Length > 0)
                    {
                        datos.Item().Text(linea).FontSize(7.5f).FontColor(Tenue);
                    }
                });
            });

            fila.ConstantItem(4, Unit.Millimetre);
            fila.ConstantItem(64, Unit.Millimetre).Border(1.2f).BorderColor(estilo.ColorPrincipal).Column(recuadro =>
            {
                recuadro.Item().PaddingVertical(5).AlignCenter().Text($"R.U.C. N.° {emisor.NumeroDocumento}").FontSize(11).Bold();
                recuadro.Item().Background(estilo.ColorPrincipal).PaddingVertical(5).PaddingHorizontal(4).AlignCenter()
                    .Text(titulo).FontSize(10).Bold().FontColor(Colors.White);
                recuadro.Item().PaddingVertical(5).AlignCenter().Text(serieNumero).FontSize(12).Bold();
            });
        });
    }

    /// <summary>Bloque con borde y título sobre fondo del color principal.</summary>
    public static void Bloque(IContainer contenedor, EstiloPdf estilo, string titulo, Action<IContainer> cuerpo)
    {
        contenedor.Border(0.6f).BorderColor(Borde).Column(columna =>
        {
            columna.Item().Background(estilo.ColorPrincipal).PaddingVertical(2).PaddingHorizontal(4)
                .Text(titulo).FontSize(7.5f).Bold().FontColor(Colors.White);
            columna.Item().Padding(4).Element(cuerpo);
        });
    }

    /// <summary>Pares «Etiqueta: valor» en dos columnas; los valores nulos o vacíos no se muestran.</summary>
    public static void Datos(IContainer contenedor, IEnumerable<(string Etiqueta, string? Valor)> datos, float anchoEtiquetaMm = 30)
    {
        contenedor.Column(columna =>
        {
            columna.Spacing(1.5f);
            foreach (var (etiqueta, valor) in datos.Where(d => !string.IsNullOrWhiteSpace(d.Valor)))
            {
                columna.Item().Row(fila =>
                {
                    fila.ConstantItem(anchoEtiquetaMm, Unit.Millimetre).Text(etiqueta + ":").Bold();
                    fila.RelativeItem().Text(valor);
                });
            }
        });
    }

    /// <summary>QR de 3.5 × 3.5 cm (con su zona de silencio) y, a su lado, valor resumen, estado y representación impresa.</summary>
    public static void BloqueQr(IContainer contenedor, string contenidoQr, string? valorResumen, string representacion, EstadoSunatImpreso? estado)
    {
        contenedor.ShowEntire().Row(fila =>
        {
            fila.ConstantItem(LadoQrMm, Unit.Millimetre).Height(LadoQrMm, Unit.Millimetre).Image(GeneradorQrPng.Generar(contenidoQr)).FitArea();
            fila.ConstantItem(4, Unit.Millimetre);
            fila.RelativeItem().AlignMiddle().Column(texto =>
            {
                texto.Spacing(2);
                if (valorResumen is not null)
                {
                    texto.Item().Text(t =>
                    {
                        t.Span("Valor resumen: ").Bold();
                        t.Span(valorResumen);
                    });
                }

                if (estado is not null)
                {
                    texto.Item().Text(t =>
                    {
                        t.Span("Estado: ").Bold();
                        t.Span(estado.Fecha is { } fecha ? $"{estado.Texto} ({FormatoImpresion.Fecha(fecha)})" : estado.Texto);
                    });
                }

                texto.Item().Text(representacion).Italic().FontColor(Tenue);
            });
        });
    }

    private static void Pie(IContainer contenedor)
    {
        contenedor.PaddingTop(3).BorderTop(0.6f).BorderColor(Borde).PaddingTop(2).Row(fila =>
        {
            fila.RelativeItem().Text(FormatoImpresion.Pie).FontSize(7).FontColor(Tenue);
            fila.ConstantItem(30, Unit.Millimetre).AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7).FontColor(Tenue));
                t.Span("Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        });
    }

    /// <summary>Documento de identidad abreviado: «RUC 20123456786».</summary>
    public static string Documento(ParteImpresa parte) =>
        string.IsNullOrEmpty(parte.TipoDocumento) ? parte.NumeroDocumento : $"{FormatoImpresion.DocumentoIdentidad(parte.TipoDocumento)} {parte.NumeroDocumento}";
}
