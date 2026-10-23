namespace PasheCPE.Tests.Utilidades;

/// <summary>Datos de ejemplo para las pruebas (RUC y nombres ficticios).</summary>
internal static class Ejemplos
{
    public const string Ruc = "20123456786";

    public static EmisorCpe Emisor(RegimenTributario regimen = RegimenTributario.General, bool? amazonia = null, bool mype = false) => new()
    {
        Ruc = Ruc,
        RazonSocial = "COMERCIAL DE PRUEBA S.A.C.",
        Domicilio = new DireccionCpe { Direccion = "JR. PRÓSPERO 123", Ubigeo = "160101", Distrito = "IQUITOS", Provincia = "MAYNAS", Departamento = "LORETO" },
        Regimen = regimen,
        ExoneradoAmazonia = amazonia,
        TasaEspecialRestaurantesHoteles = mype,
        Canal = CanalEnvio.Sunat,
        CredencialesSoap = new CredencialesSoap("MODDATOS", "clave-soap-secreta"),
        CredencialesGre = new CredencialesGre("id-cliente", "secreto-cliente", "MODDATOS", "clave-sol-secreta"),
        Certificado = new CertificadoCpe("certificado.pfx", "clave-pfx-secreta"),
    };

    public static ComprobanteCpe Factura() => new()
    {
        TipoDocumento = TipoDocumentoSunat.Factura,
        Serie = "F001",
        Numero = 123,
        FechaEmision = new DateOnly(2026, 10, 22),
        HoraEmision = new TimeOnly(10, 30, 0),
        Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Ruc, NumeroDocumento = "20987654326", RazonSocial = "CLIENTE S.A." },
        Lineas =
        [
            new LineaCpe { Descripcion = "ARROZ X 5 KG", Cantidad = 2, ImporteUnitario = 25.50m, CodigoProductoSunat = "50221200" },
            new LineaCpe { Descripcion = "SERVICIO DE ENVÍO", Cantidad = 1, ImporteUnitario = 10m, TipoItem = TipoItem.Servicio, Afectacion = AfectacionIgv.GravadoOneroso },
        ],
        FormaPago = new FormaPagoCpe
        {
            Tipo = TipoFormaPago.Credito,
            Cuotas = [new CuotaCpe { Monto = 61.00m, FechaVencimiento = new DateOnly(2026, 11, 22) }],
        },
    };

    public static ReferenciaCpe Referencia(long numero = 123, DateOnly? fecha = null) =>
        ReferenciaCpe.Comprobante(Ruc, TipoDocumentoSunat.Factura, "F001", numero, fecha ?? new DateOnly(2026, 10, 22));
}
