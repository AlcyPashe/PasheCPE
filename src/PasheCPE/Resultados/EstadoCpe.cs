namespace PasheCPE;

/// <summary>Estado de un documento tras una operación (PRD RF-15). Los niveles indican dónde terminó el proceso.</summary>
public enum EstadoCpe
{
    /// <summary>Generado y firmado, aún no enviado.</summary>
    Generado,

    /// <summary>Nivel 1: el documento no pasó la validación local y no se envió.</summary>
    ErrorValidacionLocal,

    /// <summary>Nivel 0: sin red o sin Internet; no se intentó el envío.</summary>
    SinConexionInternet,

    /// <summary>Nivel 0: servicio caído, HTTP 5xx, timeout de conexión, TLS o fault transitorio.</summary>
    ServicioNoDisponible,

    /// <summary>Nivel 2: fault SOAP o error HTTP no transitorio (credenciales, perfil, estructura, archivo).</summary>
    ErrorServicio,

    /// <summary>Nivel 2: se envió pero no hubo respuesta; reconciliar con getStatusCdr antes de reenviar.</summary>
    RespuestaIncierta,

    /// <summary>Nivel 3: ticket recibido o en proceso (98).</summary>
    EnProceso,

    /// <summary>Nivel 3: CDR con código 0 y sin observaciones.</summary>
    Aceptado,

    /// <summary>Nivel 3: CDR con código 0 y observaciones (4000 o más).</summary>
    AceptadoConObservaciones,

    /// <summary>Nivel 3: CDR con código 2000–3999 (se puede reemitir corregido).</summary>
    Rechazado,
}

/// <summary>Utilidades de <see cref="EstadoCpe"/>.</summary>
public static class EstadoCpeExtensiones
{
    /// <summary>Nivel de error (0 red, 1 validación local, 2 servicio, 3 respuesta de negocio); null si aún no se envió.</summary>
    public static int? Nivel(this EstadoCpe estado) => estado switch
    {
        EstadoCpe.SinConexionInternet or EstadoCpe.ServicioNoDisponible => 0,
        EstadoCpe.ErrorValidacionLocal => 1,
        EstadoCpe.ErrorServicio or EstadoCpe.RespuestaIncierta => 2,
        EstadoCpe.EnProceso or EstadoCpe.Aceptado or EstadoCpe.AceptadoConObservaciones or EstadoCpe.Rechazado => 3,
        _ => null,
    };

    /// <summary>
    /// Estados que aún requieren una acción del sistema de ventas (envío, reenvío o consulta), usados por
    /// <see cref="IHistorialCpe.ListarPendientesAsync"/>.
    /// </summary>
    public static bool EsPendiente(this EstadoCpe estado) => estado is EstadoCpe.Generado
        or EstadoCpe.SinConexionInternet
        or EstadoCpe.ServicioNoDisponible
        or EstadoCpe.RespuestaIncierta
        or EstadoCpe.EnProceso;
}
