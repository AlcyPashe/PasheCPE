namespace PasheCPE.Tests.Gre;

/// <summary>Guías de remisión de prueba: la GRE dorada T001-31093 (CSJ, aceptada por SUNAT el 22/10/2026) y variantes.</summary>
internal static class CasosGre
{
    public static readonly DateOnly Fecha = new(2026, 10, 22);

    public const string RucDorado = "20611619767";

    public const string RazonSocialDorada = "CSJ DISTRIBUCIONES SOCIEDAD ANONIMA CERRADA";

    /// <summary>Emisor de la GRE dorada (CSJ).</summary>
    public static EmisorCpe EmisorDorado(EmisorCpe baseEmisor) => baseEmisor with { Ruc = RucDorado, RazonSocial = RazonSocialDorada };

    /// <summary>Datos de la GRE dorada T001-31093: venta, transporte público, una boleta relacionada y un bien.</summary>
    public static GuiaRemisionCpe Dorada() => new()
    {
        Serie = "T001",
        Numero = 31093,
        FechaEmision = Fecha,
        HoraEmision = new TimeOnly(16, 38, 34),
        Destinatario = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Dni, NumeroDocumento = "05374177", RazonSocial = "RAMIREZ RUIZ, ROXANA" },
        Motivo = MotivoTraslado.Venta,
        DescripcionMotivo = "VENTA",
        Modalidad = ModalidadTraslado.TransportePublico,
        FechaInicioTraslado = Fecha,
        PesoBruto = 3m,
        Partida = new PuntoTrasladoCpe { Ubigeo = "160101", Direccion = "Av. Augusto Freyre Nro. 727" },
        Llegada = new PuntoTrasladoCpe { Ubigeo = "160101", Direccion = "CAL CALVO N° 1004" },
        Transportista = new TransportistaCpe { Ruc = "20607254720", RazonSocial = "SOLUCIONES LOGISTICAS J & H SOCIEDAD ANONIMA CERRADA" },
        DocumentosRelacionados = [new DocumentoTrasladoCpe { Tipo = DocumentoRelacionadoTraslado.BoletaDeVenta, Numero = "B006-00644248" }],
        Bienes = [new BienGuiaCpe { Descripcion = "BEBIDA DE ALMENDRA LAIVE CJ 946ML", Cantidad = 3m, Unidad = UnidadMedida.Unidad, BienNormalizado = false }],
    };

    /// <summary>Transporte privado: un vehículo y su conductor, sin transportista.</summary>
    public static GuiaRemisionCpe Privada() => Dorada() with
    {
        Modalidad = ModalidadTraslado.TransportePrivado,
        Transportista = null,
        Vehiculos = [new VehiculoCpe { Placa = "ABC123" }],
        Conductores = [new ConductorCpe { TipoDocumento = TipoDocumentoIdentidad.Dni, NumeroDocumento = "12345678", Nombres = "JUAN", Apellidos = "PEREZ LOPEZ", Licencia = "Q12345678" }],
    };
}
