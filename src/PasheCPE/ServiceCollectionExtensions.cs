using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PasheCPE;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Configuracion;
using PasheCPE.Mapeo;
using PasheCPE.Tiempo;
using PasheCPE.Tributos;
using PasheCPE.Validacion;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registro de PasheCPE en ASP.NET Core o en un Worker Service (PRD RF-22).</summary>
public static class PasheCpeServiceCollectionExtensions
{
    /// <summary>
    /// Registra PasheCPE con las opciones de la sección indicada (normalmente «PasheCPE»). Las opciones se validan
    /// al iniciar la aplicación.
    /// </summary>
    public static IServiceCollection AddPasheCpe(this IServiceCollection services, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuracion);
        services.AddOptions<OpcionesPasheCpe>().Bind(configuracion).ValidateOnStart();
        return services.AgregarNucleoPasheCpe();
    }

    /// <summary>Registra PasheCPE configurando las opciones por código. Las opciones se validan al iniciar.</summary>
    public static IServiceCollection AddPasheCpe(this IServiceCollection services, Action<OpcionesPasheCpe> configurar)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configurar);
        services.AddOptions<OpcionesPasheCpe>().Configure(configurar).ValidateOnStart();
        return services.AgregarNucleoPasheCpe();
    }

    /// <summary>Servicios de PasheCPE; lo usan <c>AddPasheCpe</c> y <see cref="PasheCpe.Crear"/> por igual.</summary>
    internal static IServiceCollection AgregarNucleoPasheCpe(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OpcionesPasheCpe>, ValidadorOpciones>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<RelojCpe>();
        services.TryAddSingleton<CatalogoErrores>();
        services.TryAddSingleton<CatalogosSunat>();
        services.TryAddSingleton<TablaTasas>();
        services.TryAddSingleton<TablaPlazos>();
        services.TryAddSingleton<CatalogoEndpoints>();
        services.TryAddSingleton<ReglasRegimen>();
        services.TryAddSingleton<AlmacenCpe>();
        services.TryAddSingleton<RegistroHistorial>();
        services.TryAddSingleton<IHistorialCpe, ServicioHistorial>();
        services.TryAddSingleton<ParametrosSunat>();
        services.TryAddSingleton<CalculadoraTributaria>();
        services.TryAddSingleton<MapeadorInvoice>();
        services.TryAddSingleton<ValidadorEsquema>();
        services.TryAddSingleton<ValidadorModelo>();
        services.TryAddSingleton<GeneradorInvoice>();
        return services;
    }
}
