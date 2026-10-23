using System.Xml;
using Microsoft.Extensions.Options;

namespace PasheCPE.Configuracion;

/// <summary>Valida <see cref="OpcionesPasheCpe"/> al iniciar: una configuración inválida es un error de programación.</summary>
internal sealed class ValidadorOpciones : IValidateOptions<OpcionesPasheCpe>
{
    public ValidateOptionsResult Validate(string? name, OpcionesPasheCpe options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errores = new List<string>();

        if (string.IsNullOrWhiteSpace(options.RutaBase) || !Path.IsPathFullyQualified(options.RutaBase))
        {
            errores.Add("PasheCPE:RutaBase debe ser una ruta absoluta (p. ej. D:\\CPE).");
        }

        if (options.TimeoutConexionSegundos <= 0)
        {
            errores.Add("PasheCPE:TimeoutConexionSegundos debe ser mayor que cero.");
        }

        if (options.TimeoutRespuestaSegundos <= 0)
        {
            errores.Add("PasheCPE:TimeoutRespuestaSegundos debe ser mayor que cero.");
        }

        if (options.AvisoVencimientoCertificadoDias < 0)
        {
            errores.Add("PasheCPE:AvisoVencimientoCertificadoDias no puede ser negativo.");
        }

        if (!EsNombreXml(options.IdFirma))
        {
            errores.Add("PasheCPE:IdFirma debe ser un nombre XML válido (letras, dígitos, '-', '_' o '.', sin empezar con dígito).");
        }

        if (string.IsNullOrWhiteSpace(options.Conectividad.HostControl))
        {
            errores.Add("PasheCPE:Conectividad:HostControl es obligatorio.");
        }

        if (options.Conectividad.CacheSegundos < 0)
        {
            errores.Add("PasheCPE:Conectividad:CacheSegundos no puede ser negativo.");
        }

        if (options.Tickets.EsperasSegundos.Any(e => e <= 0))
        {
            errores.Add("PasheCPE:Tickets:EsperasSegundos solo admite valores mayores que cero.");
        }

        foreach (var endpoint in options.Endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Canal))
            {
                errores.Add("PasheCPE:Endpoints: cada endpoint debe indicar el Canal (SUNAT u OSE:{nombre}).");
            }

            if (!Uri.TryCreate(endpoint.Url, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
            {
                errores.Add($"PasheCPE:Endpoints: la URL '{endpoint.Url}' debe ser absoluta y https.");
            }
        }

        ValidarTramos(errores, "Tasas:Igv", options.Tasas.Igv);
        ValidarTramos(errores, "Tasas:MypeRestaurantesHoteles", options.Tasas.MypeRestaurantesHoteles);
        ValidarTramos(errores, "Tasas:Icbper", options.Tasas.Icbper);
        foreach (var tramo in options.Tasas.Igv.Where(t => t.Igv + t.Ipm != t.Tasa))
        {
            errores.Add($"PasheCPE:Tasas:Igv: en el tramo desde {tramo.Desde:yyyy-MM-dd} IGV + IPM debe ser igual a la tasa.");
        }

        foreach (var vigencia in options.VigenciasCatalogos.Where(v => string.IsNullOrWhiteSpace(v.Catalogo) || v.Desde > v.Hasta))
        {
            errores.Add($"PasheCPE:VigenciasCatalogos: la vigencia del catálogo '{vigencia.Catalogo}' (código '{vigencia.Codigo}') debe indicar el catálogo y tener Desde ≤ Hasta.");
        }

        foreach (var plazo in options.Plazos.Where(p => string.IsNullOrWhiteSpace(p.Documento) || p.Dias <= 0))
        {
            errores.Add($"PasheCPE:Plazos: el plazo de '{plazo.Documento}' debe indicar el documento y días mayores que cero.");
        }

        return errores.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errores);
    }

    private static bool EsNombreXml(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        try
        {
            XmlConvert.VerifyNCName(valor);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static void ValidarTramos<T>(List<string> errores, string seccion, IEnumerable<T> tramos)
        where T : TramoVigencia
    {
        foreach (var tramo in tramos)
        {
            if (tramo.Desde == default)
            {
                errores.Add($"PasheCPE:{seccion}: cada tramo debe indicar la fecha Desde.");
            }
            else if (tramo.Hasta is { } hasta && hasta < tramo.Desde)
            {
                errores.Add($"PasheCPE:{seccion}: en el tramo desde {tramo.Desde:yyyy-MM-dd} la fecha Hasta es anterior a Desde.");
            }
        }
    }
}
