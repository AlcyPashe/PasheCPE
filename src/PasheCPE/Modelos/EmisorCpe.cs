using System.Text.Json.Serialization;

namespace PasheCPE;

/// <summary>
/// Datos del emisor que el sistema de ventas pasa en cada llamada (multiempresa sin estado, PRD RF-21).
/// No se incluye en <see cref="PasheCpeJson"/> porque contiene credenciales.
/// </summary>
public sealed record EmisorCpe
{
    /// <summary>RUC del emisor (11 dígitos).</summary>
    public required string Ruc { get; init; }

    /// <summary>Apellidos y nombres, denominación o razón social.</summary>
    public required string RazonSocial { get; init; }

    /// <summary>Nombre comercial, si lo tiene.</summary>
    public string? NombreComercial { get; init; }

    /// <summary>Domicilio fiscal.</summary>
    public required DireccionCpe Domicilio { get; init; }

    /// <summary>Código del establecimiento anexo registrado en SUNAT («0000» = domicilio fiscal).</summary>
    public string CodigoEstablecimiento { get; init; } = "0000";

    /// <summary>Régimen tributario del emisor.</summary>
    public RegimenTributario Regimen { get; init; } = RegimenTributario.General;

    /// <summary>
    /// Exoneración del IGV por la Ley de la Amazonía. Si es null se usa
    /// <see cref="OpcionesPasheCpe.ExoneradoAmazoniaPorDefecto"/>.
    /// </summary>
    public bool? ExoneradoAmazonia { get; init; }

    /// <summary>MYPE de restaurantes, hoteles y alojamientos con tasa especial del IGV según el año de emisión.</summary>
    public bool TasaEspecialRestaurantesHoteles { get; init; }

    /// <summary>Canal de envío de los comprobantes: SUNAT u OSE.</summary>
    public required CanalEnvio Canal { get; init; }

    /// <summary>Usuario secundario SOL (o usuario del OSE) y su clave, para el servicio SOAP.</summary>
    public CredencialesSoap? CredencialesSoap { get; init; }

    /// <summary>Credenciales de la API de SUNAT para las guías de remisión.</summary>
    public CredencialesGre? CredencialesGre { get; init; }

    /// <summary>Certificado digital (.pfx en la carpeta CERT del almacén) y su clave.</summary>
    public required CertificadoCpe Certificado { get; init; }
}

/// <summary>Dirección (domicilio fiscal, establecimiento o dirección del cliente).</summary>
public sealed record DireccionCpe
{
    /// <summary>Dirección: calle, número, interior…</summary>
    public required string Direccion { get; init; }

    /// <summary>Ubigeo INEI de 6 dígitos (catálogo N.° 13).</summary>
    public string? Ubigeo { get; init; }

    /// <summary>Urbanización.</summary>
    public string? Urbanizacion { get; init; }

    /// <summary>Distrito.</summary>
    public string? Distrito { get; init; }

    /// <summary>Provincia.</summary>
    public string? Provincia { get; init; }

    /// <summary>Departamento.</summary>
    public string? Departamento { get; init; }

    /// <summary>Código de país ISO 3166-1 alfa-2 (catálogo N.° 04).</summary>
    public string CodigoPais { get; init; } = "PE";
}

/// <summary>Régimen tributario que informa el sistema de ventas (PRD RF-04).</summary>
public enum RegimenTributario
{
    /// <summary>Régimen General.</summary>
    General,

    /// <summary>Régimen MYPE Tributario.</summary>
    RMT,

    /// <summary>Régimen Especial de Renta.</summary>
    RER,

    /// <summary>Nuevo Régimen Único Simplificado: solo boletas, sus notas, resúmenes y guías.</summary>
    NRUS,
}

/// <summary>Tipo de canal de envío.</summary>
public enum TipoCanal
{
    /// <summary>Envío directo a SUNAT.</summary>
    Sunat,

    /// <summary>Envío a un Operador de Servicios Electrónicos.</summary>
    Ose,
}

/// <summary>Canal por el que el emisor envía sus comprobantes: SUNAT o un OSE identificado por nombre.</summary>
public sealed record CanalEnvio
{
    /// <summary>Clave del canal SUNAT en los endpoints (<see cref="EndpointCpe.Canal"/>).</summary>
    public const string ClaveSunat = "SUNAT";

    /// <summary>Tipo de canal.</summary>
    public TipoCanal Tipo { get; init; }

    /// <summary>Nombre del OSE (p. ej. «efact»); null para SUNAT.</summary>
    public string? NombreOse { get; init; }

    /// <summary>Envío directo a SUNAT.</summary>
    public static CanalEnvio Sunat { get; } = new() { Tipo = TipoCanal.Sunat };

    /// <summary>Clave con la que se buscan los endpoints: «SUNAT» u «OSE:{nombre}».</summary>
    [JsonIgnore]
    public string Clave => Tipo == TipoCanal.Sunat ? ClaveSunat : $"OSE:{NombreOse}";

    /// <summary>Envío por el OSE indicado; sus endpoints se configuran en <see cref="OpcionesPasheCpe.Endpoints"/>.</summary>
    public static CanalEnvio Ose(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        return new CanalEnvio { Tipo = TipoCanal.Ose, NombreOse = nombre.Trim() };
    }
}
