using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PasheCPE;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Conectividad;
using PasheCPE.Configuracion;
using PasheCPE.Firma;
using PasheCPE.Mapeo;
using PasheCPE.Qr;
using PasheCPE.Respuestas;
using PasheCPE.Servicios;
using PasheCPE.Tiempo;
using PasheCPE.Transporte;
using PasheCPE.Transporte.Soap;
using PasheCPE.Tributos;
using PasheCPE.Validacion;
using PasheCPE.Validacion.Reglas;

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
        services.TryAddSingleton<MapeadorComun>();
        services.TryAddSingleton<MapeadorInvoice>();
        services.TryAddSingleton<MapeadorCreditNote>();
        services.TryAddSingleton<MapeadorDebitNote>();
        services.TryAddSingleton<ConstructorXml>();
        services.TryAddSingleton<ValidadorDocumentoModificado>();
        services.TryAddSingleton<MapeadorSummary>();
        services.TryAddSingleton<ValidadorResumen>();
        services.TryAddSingleton<GeneradorSummary>();
        services.TryAddSingleton<LectorBoletaFirmada>();
        services.TryAddSingleton<AsignadorCorrelativos>();
        services.TryAddSingleton<ValidadorEsquema>();
        services.TryAddSingleton<ValidadorModelo>();
        services.TryAddSingleton<MotorXslSunat>();
        services.TryAddSingleton<ReglasExcelSunat>();
        services.TryAddSingleton<AjustesXsl>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReglaCpe, ReglaSumatoriaIgv>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReglaCpe, ReglaTasaIgvUnica>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReglaCpe, ReglaTotalPrecioVenta>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReglaCpe, ReglaCodigoProducto>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReglaCpe, ReglaContratoColaboracion>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReglaCpe, ReglaReferenciaUnica>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReglaCpe, ReglaReferenciaNotaDebito>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReglaCpe, ReglaPenalidadesSinIgv>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReglaCpe, ReglaTasaIgvResumen>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReglaCpe, ReglaIgvResumen>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReglaCpe, ReglaRazonSocialAdquiriente>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReglaCpe, ReglaNombreTributoGratuito>());
        services.TryAddSingleton<ValidadorSunat>();
        services.TryAddSingleton<IPrecalentador, Precalentador>();
        services.TryAddSingleton<GeneradorInvoice>();
        services.TryAddSingleton<GeneradorNota>();
        services.TryAddSingleton<ProveedorCertificados>();
        services.TryAddSingleton<FirmadorXml>();
        services.TryAddSingleton<ConstructorContenidoQr>();
        services.TryAddSingleton<CatalogoRespuestas>();
        services.TryAddSingleton<ISondaRed, SondaRed>();
        services.TryAddSingleton<VerificadorConectividad>();
        services.TryAddSingleton<IVerificadorConectividad>(sp => sp.GetRequiredService<VerificadorConectividad>());
        services.TryAddSingleton(sp => FabricaManejadorHttp.Sockets(sp.GetRequiredService<IOptions<OpcionesPasheCpe>>()));
        services.TryAddSingleton(EsperasReintento.PorDefecto);
        services.AddHttpClient(ClienteSoap.NombreCliente, c => c.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(sp => sp.GetRequiredService<FabricaManejadorHttp>().Crear());
        services.TryAddSingleton<ClienteSoap>();
        services.TryAddSingleton<LectorRespuestaSoap>();
        services.TryAddSingleton<LectorCdr>();
        services.TryAddSingleton<ClasificadorRespuesta>();
        services.TryAddSingleton<BloqueoDocumentos>();
        services.TryAddSingleton<SoporteEnvio>();
        services.TryAddSingleton<EnvioComprobantes>();
        services.TryAddSingleton<EnvioResumenes>();
        services.TryAddSingleton<GeneracionResumenes>();
        services.TryAddSingleton<IServicioCpe, ServicioCpe>();
        return services;
    }
}
