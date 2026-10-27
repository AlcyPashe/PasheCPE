using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Conectividad;
using PasheCPE.Diagnostico;
using PasheCPE.Empaquetado;
using PasheCPE.Respuestas;
using PasheCPE.Transporte.Soap;

namespace PasheCPE.Servicios;

/// <summary>
/// Pasos comunes al envío por <c>sendBill</c> (<see cref="EnvioComprobantes"/>) y por <c>sendSummary</c>
/// (<see cref="EnvioResumenes"/>): credenciales y endpoint, verificación de conectividad, registro del intento en el
/// historial (con su telemetría y log) y las rutas del documento en el resultado.
/// </summary>
internal sealed class SoporteEnvio(
    ClasificadorRespuesta clasificador,
    VerificadorConectividad conectividad,
    CatalogoEndpoints endpoints,
    CatalogoErrores errores,
    AlmacenCpe almacen,
    RegistroHistorial historial,
    IOptions<OpcionesPasheCpe> opciones,
    ILogger<SoporteEnvio> registro)
{
    /// <summary>Credenciales SOAP y endpoint del servicio; un error de configuración no llama a SUNAT.</summary>
    public (EndpointCpe? Endpoint, ResultadoEnvio? Error) Preparar(EmisorCpe emisor, ContextoEnvio contexto, ServicioSunat servicio)
    {
        if (emisor.CredencialesSoap is not { Usuario.Length: > 0, Clave.Length: > 0 })
        {
            return (null, Local(contexto, "PCPE-4003", emisor.Canal.Clave));
        }

        var endpoint = endpoints.Obtener(emisor.Canal, opciones.Value.Ambiente, servicio);
        return endpoint is null
            ? (null, Local(contexto, "PCPE-4004", servicio.ToString(), emisor.Canal.Clave, opciones.Value.Ambiente))
            : (endpoint, null);
    }

    /// <summary>Verificación previa: sin Internet o servicio caído se registra en el historial y no se llama a SUNAT.</summary>
    public async Task<ResultadoEnvio?> ConectividadAsync(EmisorCpe emisor, ContextoEnvio contexto, string url, CancellationToken ct)
    {
        if (!opciones.Value.Conectividad.Verificar || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var estado = await conectividad.VerificarHostAsync(uri.Host, ct).ConfigureAwait(false);
        if (estado.Disponibilidad == Disponibilidad.Disponible)
        {
            return null;
        }

        var resultado = clasificador.DesdeConectividad(contexto, estado);
        return await RegistrarAsync(emisor, contexto.Referencia, resultado, resultado.Estado, ct).ConfigureAwait(false);
    }

    /// <summary>Ante una falla de red en la llamada, la conectividad del destino se vuelve a verificar en el siguiente envío.</summary>
    public void InvalidarConectividad(string url, RespuestaSoap soap)
    {
        if (soap.Falla != FallaRed.Ninguna && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            conectividad.Invalidar(uri.Host);
        }
    }

    /// <summary>
    /// Registra el intento en el historial con el estado actual indicado (aunque la llamada se haya cancelado: SUNAT pudo
    /// haber recibido el documento), con su telemetría y log, y completa el resultado con el historial y las rutas.
    /// </summary>
    public async Task<ResultadoEnvio> RegistrarAsync(EmisorCpe emisor, ReferenciaCpe referencia, ResultadoEnvio resultado, EstadoCpe estadoActual, CancellationToken ct)
    {
        var intento = Intento(emisor, resultado);
        var registrado = await historial.RegistrarAsync(referencia, estadoActual, intento, CancellationToken.None).ConfigureAwait(false);
        Telemetria.RegistrarEnvio(intento.Operacion, resultado.Estado, intento.DuracionMs);
        if (resultado.Estado is EstadoCpe.Aceptado or EstadoCpe.AceptadoConObservaciones or EstadoCpe.EnProceso or EstadoCpe.Generado or EstadoCpe.DeBaja)
        {
            RegistroEventos.EnvioResultado(registro, intento.Operacion, referencia.NombreArchivo, resultado.Estado, resultado.Codigo ?? string.Empty, resultado.Descripcion);
        }
        else
        {
            RegistroEventos.EnvioFallido(registro, intento.Operacion, referencia.NombreArchivo, resultado.Estado, resultado.Codigo ?? string.Empty, resultado.Descripcion);
        }

        ct.ThrowIfCancellationRequested();
        return Completar(resultado with { Historial = registrado.Intentos }, referencia);
    }

    /// <summary>Intento del historial a partir del resultado (nunca contiene credenciales).</summary>
    public static IntentoEnvio Intento(EmisorCpe emisor, ResultadoEnvio resultado) => new()
    {
        Fecha = resultado.FechaProceso,
        Operacion = resultado.Tecnico?.Operacion ?? OperacionSoap.SendBill,
        Canal = emisor.Canal.Clave,
        Endpoint = resultado.Tecnico?.Endpoint,
        DuracionMs = resultado.Tecnico?.DuracionMs ?? 0,
        Estado = resultado.Estado,
        Codigo = resultado.Codigo,
        Descripcion = resultado.Descripcion,
        Http = resultado.Tecnico?.Http,
        Ticket = resultado.Ticket,
    };

    /// <summary>Rutas existentes del documento, historial y valor resumen del XML firmado.</summary>
    public ResultadoEnvio Completar(ResultadoEnvio resultado, ReferenciaCpe referencia)
    {
        var rutas = Rutas(referencia);
        return resultado with
        {
            Rutas = rutas with { Cdr = resultado.Rutas.Cdr ?? rutas.Cdr },
            ValorResumen = resultado.ValorResumen ?? ValorResumen(rutas.XmlFirmado),
        };
    }

    public ResultadoEnvio Local(ContextoEnvio contexto, string codigo, params object?[] argumentos) =>
        clasificador.Local(contexto, EstadoCpe.ErrorValidacionLocal, errores.Local(codigo, OrigenError.Modelo, null, null, argumentos));

    public RutasCpe Rutas(ReferenciaCpe referencia)
    {
        string? Existente(CarpetaCpe carpeta, string archivo)
        {
            var ruta = almacen.Ruta(carpeta, referencia, archivo);
            return File.Exists(ruta) ? ruta : null;
        }

        return new RutasCpe
        {
            Xml = Existente(CarpetaCpe.Data, NombreArchivoCpe.Xml(referencia)),
            XmlFirmado = Existente(CarpetaCpe.Firma, NombreArchivoCpe.Xml(referencia)),
            Zip = almacen.Ruta(CarpetaCpe.Envio, referencia, NombreArchivoCpe.Zip(referencia)),
            Cdr = Existente(CarpetaCpe.Rpta, NombreArchivoCpe.Cdr(referencia)),
            Qr = Existente(CarpetaCpe.Qr, NombreArchivoCpe.Qr(referencia)),
            Pdf = Existente(CarpetaCpe.Repo, NombreArchivoCpe.Pdf(referencia)),
            Historial = historial.Ruta(referencia),
        };
    }

    public static byte[]? TryBase64(string texto)
    {
        try
        {
            return Convert.FromBase64String(texto);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>DigestValue del XML firmado (FIRMA), leído con XmlReader.</summary>
    private static string? ValorResumen(string? rutaFirmado)
    {
        if (rutaFirmado is null || !File.Exists(rutaFirmado))
        {
            return null;
        }

        try
        {
            using var lector = XmlReader.Create(rutaFirmado, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return lector.ReadToFollowing("DigestValue", System.Security.Cryptography.Xml.SignedXml.XmlDsigNamespaceUrl) ? lector.ReadElementContentAsString() : null;
        }
        catch (Exception ex) when (ex is XmlException or IOException)
        {
            return null;
        }
    }
}
