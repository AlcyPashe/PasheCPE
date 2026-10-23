using System.Globalization;
using PasheCPE.Catalogos;
using PasheCPE.Tiempo;
using PasheCPE.Transporte.Soap;

namespace PasheCPE.Respuestas;

/// <summary>Documento y operación de la respuesta que se clasifica.</summary>
internal sealed record ContextoEnvio(ReferenciaCpe Referencia, string Operacion, RutasCpe Rutas);

/// <summary>
/// Única fuente de la tabla de clasificación (TRD §5.1): convierte un CDR, un fault, una falla de red, un ticket o su
/// estado, el resultado de la conectividad o un error local en un <see cref="ResultadoEnvio"/> completo (estado, códigos, descripción literal y
/// del catálogo, mensaje para el cajero, reintentable, consulta de estado, fecha límite y detalle técnico).
/// </summary>
internal sealed class ClasificadorRespuesta(CatalogoErrores errores, CatalogoRespuestas respuestas, TablaPlazos plazos, RelojCpe reloj)
{
    public const string CodigoRegistradoPreviamente = "1033";

    /// <summary>Estado del ticket en proceso; su mensaje para el cajero es el del resumen.</summary>
    public const string CodigoTicketEnProceso = "98";

    /// <summary>CDR: 0 aceptado (con notas, con observaciones), 2000–3999 rechazado, 98 en proceso, 99 rechazado.</summary>
    public ResultadoEnvio DesdeCdr(ContextoEnvio contexto, RespuestaSoap? soap, CdrLeido cdr)
    {
        ArgumentNullException.ThrowIfNull(cdr);
        var numero = Numero(cdr.Codigo);
        var estado = numero switch
        {
            0 => cdr.Notas.Count > 0 ? EstadoCpe.AceptadoConObservaciones : EstadoCpe.Aceptado,
            >= 2000 and < 4000 or 99 => EstadoCpe.Rechazado,
            >= 4000 => EstadoCpe.AceptadoConObservaciones,
            98 => EstadoCpe.EnProceso,
            _ => EstadoCpe.ErrorServicio,
        };
        IReadOnlyList<ObservacionCpe> observaciones = cdr.FirmaValida == false
            ? [.. cdr.Notas, errores.Advertencia("PCPE-4009", contexto.Referencia.NombreArchivo)]
            : cdr.Notas;
        return Crear(contexto, soap, estado, cdr.Codigo, cdr.Descripcion) with
        {
            Observaciones = observaciones,
            Errores = estado is EstadoCpe.Rechazado or EstadoCpe.ErrorServicio ? [ErrorSunat(cdr.Codigo, cdr.Descripcion)] : [],
            RequiereConsultaEstado = estado == EstadoCpe.EnProceso,
        };
    }

    /// <summary>
    /// Fault: 2000–3999 rechazado (SUNAT devuelve así los rechazos de sendBill); en proceso (0140) → consultar, nunca
    /// reenviar; transitorio → servicio no disponible reintentable (consultar antes si es de constancia); 1033 → consultar;
    /// el resto, error de servicio.
    /// </summary>
    public ResultadoEnvio DesdeFault(ContextoEnvio contexto, RespuestaSoap soap, FaultSoap fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        var numero = Numero(fault.Codigo);
        var catalogo = fault.Catalogo;
        var (estado, reintentable, consultar) = numero switch
        {
            >= 2000 and < 4000 => (EstadoCpe.Rechazado, false, false),
            _ when catalogo?.EnProceso == true => (EstadoCpe.EnProceso, false, true),
            _ when catalogo?.Transitorio == true => (EstadoCpe.ServicioNoDisponible, true, catalogo.GrupoTransitorio != "disponibilidad"),
            _ when fault.Codigo == CodigoRegistradoPreviamente => (EstadoCpe.ErrorServicio, false, true),
            _ => (EstadoCpe.ErrorServicio, false, false),
        };
        var resultado = Crear(contexto, soap, estado, fault.Codigo, fault.FaultString);
        return resultado with
        {
            Errores = estado is EstadoCpe.Rechazado or EstadoCpe.ErrorServicio ? [ErrorSunat(fault.Codigo ?? fault.FaultCode, fault.FaultString)] : [],
            EsReintentable = reintentable,
            RequiereConsultaEstado = consultar,
            Tecnico = resultado.Tecnico! with { FaultCode = fault.FaultCode, FaultString = fault.FaultString },
        };
    }

    /// <summary>
    /// Sin respuesta SOAP legible. No se envió (DNS, TCP, TLS, HTTP 502/503/504) → servicio no disponible, reintentable.
    /// Se envió sin respuesta (timeout de lectura, conexión cortada, cuerpo ilegible) → respuesta incierta, salvo en
    /// operaciones de consulta (<paramref name="idempotente"/>), donde se puede repetir sin riesgo. Otro HTTP → error de
    /// servicio.
    /// </summary>
    public ResultadoEnvio DesdeRed(ContextoEnvio contexto, RespuestaSoap soap, bool idempotente)
    {
        ArgumentNullException.ThrowIfNull(soap);
        var noDisponible = soap.Falla == FallaRed.SinConexion || soap.Http is 502 or 503 or 504;
        var sinRespuesta = soap.Falla == FallaRed.SinRespuesta || soap.Http is 200 or 500;
        var estado = noDisponible || (sinRespuesta && idempotente)
            ? EstadoCpe.ServicioNoDisponible
            : sinRespuesta ? EstadoCpe.RespuestaIncierta : EstadoCpe.ErrorServicio;
        var descripcion = soap.Excepcion ?? $"HTTP {soap.Http}";
        var resultado = Crear(contexto, soap, estado, null, descripcion) with
        {
            EsReintentable = estado == EstadoCpe.ServicioNoDisponible,
            RequiereConsultaEstado = estado == EstadoCpe.RespuestaIncierta,
        };
        return soap.Falla == FallaRed.Ninguna
            ? resultado with { Errores = [errores.Local("PCPE-4010", OrigenError.Sunat, null, null, soap.Operacion, soap.Http, Recortar(soap.Cuerpo))] }
            : resultado;
    }

    /// <summary>La verificación previa dio «sin Internet» o «servicio no disponible»: no se intentó el envío.</summary>
    public ResultadoEnvio DesdeConectividad(ContextoEnvio contexto, EstadoConectividad conectividad)
    {
        ArgumentNullException.ThrowIfNull(conectividad);
        var estado = conectividad.Disponibilidad == Disponibilidad.SinConexionInternet ? EstadoCpe.SinConexionInternet : EstadoCpe.ServicioNoDisponible;
        return Crear(contexto, null, estado, null, conectividad.Detalle) with
        {
            EsReintentable = true,
            Tecnico = new DetalleTecnico { Endpoint = conectividad.Destino, Operacion = contexto.Operacion },
        };
    }

    /// <summary>Estado de getStatusCdr sin CDR adjunto (statusCode y statusMessage).</summary>
    public ResultadoEnvio DesdeConsulta(ContextoEnvio contexto, RespuestaSoap soap, EstadoConsultaCdr consulta, string? mensaje, EstadoCpe estadoSiNoExiste)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        var estado = consulta.Resultado switch
        {
            ResultadoConsultaCdr.Aceptado => EstadoCpe.Aceptado,
            ResultadoConsultaCdr.Rechazado => EstadoCpe.Rechazado,
            ResultadoConsultaCdr.NoExiste => estadoSiNoExiste,
            _ => EstadoCpe.ErrorServicio,
        };
        return Crear(contexto, soap, estado, consulta.Codigo, mensaje ?? consulta.Descripcion) with
        {
            DescripcionCatalogo = consulta.Descripcion,
            Errores = estado is EstadoCpe.Rechazado or EstadoCpe.ErrorServicio ? [ErrorSunat(consulta.Codigo, mensaje ?? consulta.Descripcion)] : [],
            EsReintentable = consulta.Resultado == ResultadoConsultaCdr.NoExiste,
        };
    }

    /// <summary>sendSummary recibió el archivo: el ticket queda en proceso y hay que consultarlo (getStatus).</summary>
    public ResultadoEnvio DesdeTicket(ContextoEnvio contexto, RespuestaSoap soap, string ticket)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticket);
        return Crear(contexto, soap, EstadoCpe.EnProceso, null, $"Ticket {ticket}") with
        {
            Ticket = ticket,
            RequiereConsultaEstado = true,
            MensajeUsuario = respuestas.MensajeUsuario(EstadoCpe.EnProceso, CodigoTicketEnProceso),
        };
    }

    /// <summary>
    /// getStatus sin CDR adjunto: 98 → en proceso (consultar después); 99 → rechazado (proceso con errores: el resumen
    /// no se registró y sus documentos se pueden informar en otro); 0 sin CDR o un estado desconocido (PCPE-4012) →
    /// error de servicio. Con CDR se clasifica con <see cref="DesdeCdr"/>.
    /// </summary>
    public ResultadoEnvio DesdeEstadoTicket(ContextoEnvio contexto, RespuestaSoap soap, EstadoTicket? estado, string? codigo, string ticket)
    {
        if (estado is null)
        {
            return Local(contexto, EstadoCpe.ErrorServicio, errores.Local("PCPE-4012", OrigenError.Sunat, null, null, ticket, codigo)) with { Ticket = ticket };
        }

        if (estado.Resultado == ResultadoTicket.EnProceso)
        {
            return Crear(contexto, soap, EstadoCpe.EnProceso, estado.Codigo, estado.Descripcion) with { Ticket = ticket, RequiereConsultaEstado = true };
        }

        var error = errores.Local("PCPE-4013", OrigenError.Sunat, null, null, ticket, estado.Codigo);
        var final = estado.Resultado == ResultadoTicket.ConErrores ? EstadoCpe.Rechazado : EstadoCpe.ErrorServicio;
        return Crear(contexto, soap, final, estado.Codigo, error.Descripcion) with { Ticket = ticket, Errores = [error] };
    }

    /// <summary>Error que impide la operación sin llamar a SUNAT (validación local, configuración, no soportado).</summary>
    public ResultadoEnvio Local(ContextoEnvio contexto, EstadoCpe estado, ErrorCpe error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Crear(contexto, null, estado, error.Codigo, error.Descripcion) with { Errores = [error] };
    }

    /// <summary>El historial ya tiene el resultado definitivo: se devuelve sin llamar a SUNAT.</summary>
    public ResultadoEnvio DesdeHistorial(ContextoEnvio contexto, HistorialCpe historial)
    {
        ArgumentNullException.ThrowIfNull(historial);
        var ultimo = historial.Intentos.LastOrDefault(i => i.Estado == historial.EstadoActual);
        return Crear(contexto, null, historial.EstadoActual, ultimo?.Codigo, ultimo?.Descripcion) with { Historial = historial.Intentos };
    }

    private ResultadoEnvio Crear(ContextoEnvio contexto, RespuestaSoap? soap, EstadoCpe estado, string? codigo, string? descripcion)
    {
        var referencia = contexto.Referencia;
        return new ResultadoEnvio
        {
            Estado = estado,
            Codigo = codigo,
            Descripcion = descripcion,
            DescripcionCatalogo = errores.Buscar(codigo)?.Descripcion,
            MensajeUsuario = respuestas.MensajeUsuario(estado, codigo),
            Rutas = contexto.Rutas,
            // El plazo del resumen se cuenta desde la emisión de sus documentos, que la referencia del RC no tiene.
            FechaLimiteEnvio = referencia.Correlativo is null ? plazos.FechaLimite(referencia.TipoDocumento, null, referencia.Fecha) : null,
            Tecnico = soap is null ? null : new DetalleTecnico
            {
                Endpoint = soap.Endpoint,
                Operacion = soap.Operacion,
                Http = soap.Http,
                DuracionMs = soap.DuracionMs,
                Excepcion = soap.Excepcion,
            },
            FechaProceso = reloj.AhoraLima(),
        };
    }

    private static ErrorCpe ErrorSunat(string? codigo, string? descripcion) => new()
    {
        Codigo = codigo ?? string.Empty,
        Descripcion = descripcion ?? string.Empty,
        Origen = OrigenError.Sunat,
    };

    private static int? Numero(string? codigo) =>
        int.TryParse(codigo, NumberStyles.None, CultureInfo.InvariantCulture, out var numero) ? numero : null;

    private static string Recortar(string? cuerpo) =>
        cuerpo is null ? string.Empty : cuerpo.Length <= 300 ? cuerpo : cuerpo[..300] + "…";
}
