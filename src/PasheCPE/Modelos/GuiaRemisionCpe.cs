namespace PasheCPE;

/// <summary>
/// Guía de remisión electrónica remitente (09), GRE 2.0 (Excel GRE 25/09/2026). El sistema de ventas entrega los datos
/// del traslado; la librería arma el XML, lo valida con la XSL GRE 2.0 de SUNAT y lo envía por la API REST (solo
/// producción). Fuera de alcance por ahora: contenedores y precintos del manifiesto de carga (motivo 19 con documento
/// 91), coordenadas de los puntos y autorización especial del remitente.
/// </summary>
public sealed record GuiaRemisionCpe
{
    /// <summary>Serie de 4 caracteres: T seguida de 3 letras o números (T001).</summary>
    public required string Serie { get; init; }

    /// <summary>Número correlativo (sin ceros a la izquierda).</summary>
    public required long Numero { get; init; }

    /// <summary>Fecha de emisión; si es null se usa la fecha actual de Lima.</summary>
    public DateOnly? FechaEmision { get; init; }

    /// <summary>Hora de emisión; si es null se usa la hora actual de Lima.</summary>
    public TimeOnly? HoraEmision { get; init; }

    /// <summary>Observaciones (cbc:Note).</summary>
    public string? Observaciones { get; init; }

    /// <summary>Destinatario de los bienes. En el motivo 04 (entre establecimientos) es el mismo remitente.</summary>
    public required ClienteCpe Destinatario { get; init; }

    /// <summary>Proveedor (motivo 02, compra).</summary>
    public ClienteCpe? Proveedor { get; init; }

    /// <summary>Comprador (motivo 03, venta con entrega a terceros).</summary>
    public ClienteCpe? Comprador { get; init; }

    /// <summary>Motivo de traslado (catálogo N.° 20): <see cref="MotivoTraslado"/>.</summary>
    public required string Motivo { get; init; }

    /// <summary>Descripción del motivo; obligatoria en el motivo 13 (otros).</summary>
    public string? DescripcionMotivo { get; init; }

    /// <summary>Modalidad (catálogo N.° 18): <see cref="ModalidadTraslado"/>.</summary>
    public required string Modalidad { get; init; }

    /// <summary>Fecha de inicio del traslado (en la modalidad pública, de entrega de los bienes al transportista).</summary>
    public required DateOnly FechaInicioTraslado { get; init; }

    /// <summary>Peso bruto total de los bienes (hasta 3 decimales).</summary>
    public required decimal PesoBruto { get; init; }

    /// <summary>Unidad del peso bruto: KGM (kilogramos) o TNE (toneladas).</summary>
    public string UnidadPeso { get; init; } = "KGM";

    /// <summary>Número de bultos o pallets (importación y exportación).</summary>
    public int? Bultos { get; init; }

    /// <summary>Punto de partida.</summary>
    public required PuntoTrasladoCpe Partida { get; init; }

    /// <summary>Punto de llegada.</summary>
    public required PuntoTrasladoCpe Llegada { get; init; }

    /// <summary>Transportista (modalidad pública).</summary>
    public TransportistaCpe? Transportista { get; init; }

    /// <summary>Vehículos: el primero es el principal; los siguientes (hasta 2), secundarios.</summary>
    public IReadOnlyList<VehiculoCpe> Vehiculos { get; init; } = [];

    /// <summary>Conductores: el primero es el principal; los siguientes (hasta 2), secundarios.</summary>
    public IReadOnlyList<ConductorCpe> Conductores { get; init; } = [];

    /// <summary>Indicadores del traslado (cbc:SpecialInstructions).</summary>
    public IndicadoresTrasladoCpe Indicadores { get; init; } = new();

    /// <summary>Puerto o aeropuerto de embarque o desembarque (motivos 08, 09 y 19).</summary>
    public PuertoCpe? Puerto { get; init; }

    /// <summary>Documentos relacionados (catálogo N.° 61).</summary>
    public IReadOnlyList<DocumentoTrasladoCpe> DocumentosRelacionados { get; init; } = [];

    /// <summary>Bienes trasladados.</summary>
    public required IReadOnlyList<BienGuiaCpe> Bienes { get; init; }
}

/// <summary>Punto de partida o de llegada.</summary>
public sealed record PuntoTrasladoCpe
{
    /// <summary>Ubigeo INEI de 6 dígitos.</summary>
    public required string Ubigeo { get; init; }

    /// <summary>Dirección completa.</summary>
    public required string Direccion { get; init; }

    /// <summary>RUC al que pertenece el establecimiento (motivo 04 y traslados con establecimiento declarado).</summary>
    public string? Ruc { get; init; }

    /// <summary>Código del establecimiento anexo declarado en SUNAT (0000 = domicilio fiscal).</summary>
    public string? CodigoEstablecimiento { get; init; }
}

/// <summary>Transportista de la modalidad pública.</summary>
public sealed record TransportistaCpe
{
    /// <summary>RUC del transportista.</summary>
    public required string Ruc { get; init; }

    /// <summary>Razón social.</summary>
    public required string RazonSocial { get; init; }

    /// <summary>Número de registro MTC (hasta 20 caracteres); si falta, SUNAT observa 4391.</summary>
    public string? RegistroMtc { get; init; }

    /// <summary>Autorización especial del transportista para el traslado de los bienes.</summary>
    public AutorizacionEspecialCpe? Autorizacion { get; init; }
}

/// <summary>Autorización especial para el traslado: entidad emisora (listado D-37) y número.</summary>
public sealed record AutorizacionEspecialCpe
{
    /// <summary>Entidad que emite la autorización: <see cref="EntidadAutorizacionEspecial"/>.</summary>
    public required string Entidad { get; init; }

    /// <summary>Número de la autorización.</summary>
    public required string Numero { get; init; }
}

/// <summary>Vehículo (modalidad privada o vehículos M1/L).</summary>
public sealed record VehiculoCpe
{
    /// <summary>Placa, en mayúsculas y sin guiones (6 a 8 letras y números).</summary>
    public required string Placa { get; init; }

    /// <summary>Tarjeta Única de Circulación Electrónica o certificado de habilitación vehicular.</summary>
    public string? CertificadoHabilitacion { get; init; }

    /// <summary>Autorización especial del vehículo.</summary>
    public AutorizacionEspecialCpe? Autorizacion { get; init; }
}

/// <summary>Conductor (modalidad privada).</summary>
public sealed record ConductorCpe
{
    /// <summary>Tipo de documento de identidad (catálogo N.° 06).</summary>
    public required string TipoDocumento { get; init; }

    /// <summary>Número de documento.</summary>
    public required string NumeroDocumento { get; init; }

    /// <summary>Nombres.</summary>
    public required string Nombres { get; init; }

    /// <summary>Apellidos.</summary>
    public required string Apellidos { get; init; }

    /// <summary>Número de licencia de conducir (9 a 10 caracteres).</summary>
    public required string Licencia { get; init; }
}

/// <summary>Indicadores del traslado (Excel GRE: cbc:SpecialInstructions con los valores SUNAT_Envio_Indicador…).</summary>
public sealed record IndicadoresTrasladoCpe
{
    /// <summary>Traslado en vehículos de categoría M1 o L (no requiere datos del vehículo ni del conductor).</summary>
    public bool VehiculoM1L { get; init; }

    /// <summary>Retorno de vehículo vacío.</summary>
    public bool RetornoVehiculoVacio { get; init; }

    /// <summary>Retorno de vehículo con envases o embalajes vacíos.</summary>
    public bool RetornoEnvasesVacios { get; init; }

    /// <summary>Transbordo programado.</summary>
    public bool TransbordoProgramado { get; init; }

    /// <summary>Traslado total de la DAM o la DS.</summary>
    public bool TrasladoTotalDamDs { get; init; }

    /// <summary>Se registran los vehículos y conductores del transportista (modalidad pública).</summary>
    public bool VehiculosConductoresTransportista { get; init; }
}

/// <summary>Puerto, aeropuerto o centro de atención en frontera.</summary>
public sealed record PuertoCpe
{
    /// <summary>1 = puerto (catálogo N.° 63), 2 = aeropuerto (catálogo N.° 64), 3 = centro de atención en frontera.</summary>
    public required string Tipo { get; init; }

    /// <summary>Código del puerto o aeropuerto.</summary>
    public required string Codigo { get; init; }

    /// <summary>Nombre.</summary>
    public string? Nombre { get; init; }
}

/// <summary>Documento relacionado con el traslado (catálogo N.° 61).</summary>
public sealed record DocumentoTrasladoCpe
{
    /// <summary>Tipo (catálogo N.° 61): <see cref="DocumentoRelacionadoTraslado"/>.</summary>
    public required string Tipo { get; init; }

    /// <summary>Número: serie-número de un comprobante, numeración de la DAM, etc.</summary>
    public required string Numero { get; init; }

    /// <summary>Tipo de documento de identidad del emisor del documento (catálogo N.° 06); por defecto RUC.</summary>
    public string TipoDocumentoEmisor { get; init; } = TipoDocumentoIdentidad.Ruc;

    /// <summary>Número de documento del emisor; si es null, el RUC del remitente.</summary>
    public string? NumeroDocumentoEmisor { get; init; }
}

/// <summary>Bien trasladado.</summary>
public sealed record BienGuiaCpe
{
    /// <summary>Descripción del bien.</summary>
    public required string Descripcion { get; init; }

    /// <summary>Cantidad (hasta 10 decimales).</summary>
    public required decimal Cantidad { get; init; }

    /// <summary>Unidad de medida UN/ECE Rec. 20 (catálogo N.° 03 o N.° 65).</summary>
    public string Unidad { get; init; } = UnidadMedida.Unidad;

    /// <summary>Código interno del producto.</summary>
    public string? CodigoInterno { get; init; }

    /// <summary>Código de producto SUNAT (UNSPSC, catálogo N.° 25).</summary>
    public string? CodigoProductoSunat { get; init; }

    /// <summary>Indicador de bien normalizado (concepto 7022); null = no se informa.</summary>
    public bool? BienNormalizado { get; init; }

    /// <summary>Partida arancelaria (concepto 7020), obligatoria si es un bien normalizado.</summary>
    public string? PartidaArancelaria { get; init; }

    /// <summary>Numeración de la DAM o DS (concepto 7021).</summary>
    public string? NumeroDam { get; init; }

    /// <summary>Número de serie en la DAM o DS (concepto 7023).</summary>
    public string? SerieDam { get; init; }
}
