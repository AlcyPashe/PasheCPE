using System.Globalization;
using System.Text.RegularExpressions;
using PasheCPE.Catalogos;
using PasheCPE.Tiempo;

namespace PasheCPE.Validacion;

/// <summary>
/// Validación del modelo de la guía de remisión remitente (TRD §9.1, nivel 1), con los códigos del Excel GRE entre
/// paréntesis. Solo lo que conviene avisar con un mensaje claro antes de armar el XML o lo que la XSL GRE 2.0 no tiene
/// (fecha de emisión futura, 2329, y plazo de envío, 2108); el resto lo valida la XSL. Acumula todos los errores y nunca
/// lanza.
/// </summary>
internal sealed partial class ValidadorGuia(CatalogoErrores errores, CatalogosSunat catalogos, TablaPlazos plazos, RelojCpe reloj)
{
    private const int _maximoPorTipo = 3;

    public IReadOnlyList<ErrorCpe> Validar(EmisorCpe emisor, GuiaRemisionCpe guia, DateOnly fechaEmision)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(guia);
        var lista = new List<ErrorCpe>();
        var hoy = reloj.HoyLima();

        ErrorCpe Local(string codigo, string ruta, params object?[] argumentos) => errores.Local(codigo, OrigenError.Modelo, ruta, null, argumentos);

        void Catalogo(string catalogo, string? codigo, string ruta)
        {
            if (catalogos.Validar(catalogo, codigo, fechaEmision, ruta) is { } error)
            {
                lista.Add(error);
            }
        }

        if (!DocumentosIdentidad.EsRucValido(emisor.Ruc))
        {
            lista.Add(Local("PCPE-1201", "Emisor.Ruc", emisor.Ruc));
        }

        if (string.IsNullOrWhiteSpace(emisor.RazonSocial))
        {
            lista.Add(Local("PCPE-1202", "Emisor.RazonSocial", "la razón social del emisor"));
        }

        if (!SerieGuia().IsMatch(guia.Serie ?? string.Empty))
        {
            lista.Add(Local("PCPE-1601", "Serie", guia.Serie));
        }

        if (guia.Numero is < 1 or > 99_999_999)
        {
            lista.Add(Local("PCPE-1602", "Numero", guia.Numero));
        }

        if (fechaEmision > hoy)
        {
            lista.Add(Local("PCPE-1603", "FechaEmision", Texto(fechaEmision), Texto(hoy)));
        }
        else if (plazos.FechaLimite(TipoDocumentoSunat.GuiaDeRemisionRemitente, null, fechaEmision) is { } limite && hoy > limite)
        {
            lista.Add(Local("PCPE-1604", "FechaEmision", $"{guia.Serie}-{guia.Numero.ToString(CultureInfo.InvariantCulture)}", Texto(limite)));
        }

        Catalogo(MotivoTraslado.NumeroCatalogo, guia.Motivo, "Motivo");
        Catalogo(ModalidadTraslado.NumeroCatalogo, guia.Modalidad, "Modalidad");
        Catalogo(TipoDocumentoIdentidad.NumeroCatalogo, guia.Destinatario?.TipoDocumento, "Destinatario.TipoDocumento");
        for (var i = 0; i < guia.DocumentosRelacionados.Count; i++)
        {
            Catalogo(DocumentoRelacionadoTraslado.NumeroCatalogo, guia.DocumentosRelacionados[i].Tipo, Ruta("DocumentosRelacionados", i) + ".Tipo");
        }

        if (guia.Motivo == MotivoTraslado.Otros && string.IsNullOrWhiteSpace(guia.DescripcionMotivo))
        {
            lista.Add(Local("PCPE-1605", "DescripcionMotivo"));
        }

        if (guia.PesoBruto <= 0 || decimal.Round(guia.PesoBruto, 3) != guia.PesoBruto || guia.UnidadPeso is not ("KGM" or "TNE"))
        {
            lista.Add(Local("PCPE-1606", "PesoBruto", $"{guia.PesoBruto.ToString(CultureInfo.InvariantCulture)} {guia.UnidadPeso}"));
        }

        if (guia.FechaInicioTraslado < fechaEmision)
        {
            lista.Add(Local("PCPE-1612", "FechaInicioTraslado", Texto(guia.FechaInicioTraslado), Texto(fechaEmision)));
        }

        if (guia.Bienes is not { Count: > 0 })
        {
            lista.Add(Local("PCPE-1607", "Bienes"));
        }
        else
        {
            for (var i = 0; i < guia.Bienes.Count; i++)
            {
                var bien = guia.Bienes[i];
                if (string.IsNullOrWhiteSpace(bien.Descripcion) || bien.Cantidad <= 0)
                {
                    lista.Add(Local("PCPE-1608", Ruta("Bienes", i), i + 1));
                }
            }
        }

        ValidarTransporte(guia, lista, Local);
        return lista;
    }

    /// <summary>Datos del transporte según la modalidad; con el indicador M1/L no se piden vehículo, conductor ni transportista.</summary>
    private static void ValidarTransporte(GuiaRemisionCpe guia, List<ErrorCpe> lista, Func<string, string, object?[], ErrorCpe> local)
    {
        if (!guia.Indicadores.VehiculoM1L)
        {
            if (guia.Modalidad == ModalidadTraslado.TransportePublico
                && (guia.Transportista is null || string.IsNullOrWhiteSpace(guia.Transportista.Ruc) || string.IsNullOrWhiteSpace(guia.Transportista.RazonSocial)))
            {
                lista.Add(local("PCPE-1609", "Transportista", []));
            }

            if (guia.Modalidad == ModalidadTraslado.TransportePrivado && (guia.Vehiculos.Count == 0 || guia.Conductores.Count == 0))
            {
                lista.Add(local("PCPE-1610", guia.Vehiculos.Count == 0 ? "Vehiculos" : "Conductores", []));
            }
        }

        if (guia.Vehiculos.Count > _maximoPorTipo)
        {
            lista.Add(local("PCPE-1611", "Vehiculos", [guia.Vehiculos.Count, "vehículos"]));
        }

        if (guia.Conductores.Count > _maximoPorTipo)
        {
            lista.Add(local("PCPE-1611", "Conductores", [guia.Conductores.Count, "conductores"]));
        }
    }

    private static string Ruta(string lista, int indice) => string.Create(CultureInfo.InvariantCulture, $"{lista}[{indice}]");

    private static string Texto(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>T seguida de 3 letras mayúsculas o dígitos (Excel GRE, 1001).</summary>
    [GeneratedRegex("^T[A-Z0-9]{3}$")]
    private static partial Regex SerieGuia();
}
