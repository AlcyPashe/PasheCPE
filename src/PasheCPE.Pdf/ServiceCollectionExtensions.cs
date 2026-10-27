using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PasheCPE.Pdf;
using PasheCPE.Pdf.Representacion;
using PasheCPE.Servicios;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registro de PasheCPE.Pdf (PRD RF-22), después de <c>AddPasheCpe</c>.</summary>
public static class PasheCpePdfServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="IGeneradorPdf"/> y la generación automática dentro de <c>EmitirAsync</c>
    /// (<see cref="PasheCPE.OpcionesPasheCpe.GenerarPdfAutomaticamente"/>), con las marcas por RUC indicadas. Declara la
    /// licencia Community de QuestPDF.
    /// </summary>
    public static IServiceCollection AddPasheCpePdf(this IServiceCollection services, Action<OpcionesPasheCpePdf>? configurar = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var opciones = services.AddOptions<OpcionesPasheCpePdf>();
        if (configurar is not null)
        {
            opciones.Configure(configurar);
        }

        return services.AgregarPdf();
    }

    /// <summary>Igual, con las marcas de la sección indicada (normalmente «PasheCPE:Pdf»).</summary>
    public static IServiceCollection AddPasheCpePdf(this IServiceCollection services, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuracion);
        services.AddOptions<OpcionesPasheCpePdf>().Bind(configuracion);
        return services.AgregarPdf();
    }

    internal static IServiceCollection AgregarPdf(this IServiceCollection services)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        services.TryAddSingleton<LectorRepresentacion>();
        services.TryAddSingleton<GeneradorPdf>();
        services.TryAddSingleton<IGeneradorPdf>(sp => sp.GetRequiredService<GeneradorPdf>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IGeneracionPdfAutomatica, GeneradorPdf>(sp => sp.GetRequiredService<GeneradorPdf>()));
        return services;
    }
}
