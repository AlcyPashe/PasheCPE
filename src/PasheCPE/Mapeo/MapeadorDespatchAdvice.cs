using System.Globalization;
using Microsoft.Extensions.Options;
using PasheCPE.Catalogos;
using PasheCPE.Ubl;

namespace PasheCPE.Mapeo;

/// <summary>
/// Guía de remisión remitente → DespatchAdvice UBL 2.1 (GRE 2.0) según el Excel GRE y la XSL GRE 2.0: remitente,
/// destinatario, proveedor o comprador, documentos relacionados, traslado (motivo, pesos, indicadores, modalidad,
/// transportista o conductores, vehículos, puntos de partida y llegada, puerto) y bienes con sus propiedades
/// (catálogo N.° 55). Deja <c>ext:UBLExtensions</c> vacío, preparado para la firma.
/// </summary>
internal sealed class MapeadorDespatchAdvice(CatalogosSunat catalogos, IOptions<OpcionesPasheCpe> opciones)
{
    /// <summary>cbc:ID del traslado que exige el Excel GRE.</summary>
    public const string IdEnvio = "SUNAT_Envio";

    public const string ConductorPrincipal = "Principal";
    public const string ConductorSecundario = "Secundario";

    /// <summary>Valores de cbc:SpecialInstructions de cada indicador (Excel GRE).</summary>
    public const string IndicadorM1L = "SUNAT_Envio_IndicadorTrasladoVehiculoM1L";
    public const string IndicadorRetornoVacio = "SUNAT_Envio_IndicadorRetornoVehiculoVacio";
    public const string IndicadorRetornoEnvases = "SUNAT_Envio_IndicadorRetornoVehiculoEnvaseVacio";
    public const string IndicadorTransbordo = "SUNAT_Envio_IndicadorTransbordoProgramado";
    public const string IndicadorTotalDamDs = "SUNAT_Envio_IndicadorTrasladoTotalDAMoDS";
    public const string IndicadorVehiculosTransportista = "SUNAT_Envio_IndicadorVehiculoConductoresTransp";

    /// <summary>Conceptos del catálogo N.° 55 que usa la GRE.</summary>
    public const string ConceptoPartida = "7020";
    public const string ConceptoDam = "7021";
    public const string ConceptoBienNormalizado = "7022";
    public const string ConceptoSerieDam = "7023";

    private const string _sunat = "PE:SUNAT";
    private const string _uriCatalogo = "urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo";

    public DespatchAdvice Mapear(EmisorCpe emisor, GuiaRemisionCpe guia, ReferenciaCpe referencia, TimeOnly hora)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(guia);
        ArgumentNullException.ThrowIfNull(referencia);
        var documento = new DespatchAdvice
        {
            UBLExtensions = MapeadorComun.ExtensionesVacias(),
            UBLVersionID = "2.1",
            CustomizationID = AtributosCatalogo.Personalizacion("2.0"),
            ID = MapeadorComun.SerieNumero(guia.Serie, guia.Numero),
            IssueDate = MapeadorComun.Fecha(referencia.Fecha),
            IssueTime = MapeadorComun.Hora(hora),
            DespatchAdviceTypeCode = AtributosCatalogo.Guia(TipoDocumentoSunat.GuiaDeRemisionRemitente),
            Note = Vacio(guia.Observaciones),
            DespatchSupplierParty = Parte(TipoDocumentoIdentidad.Ruc, emisor.Ruc, emisor.RazonSocial),
            DeliveryCustomerParty = Parte(guia.Destinatario),
            BuyerCustomerParty = guia.Comprador is { } comprador ? Parte(comprador) : null,
            SellerSupplierParty = guia.Proveedor is { } proveedor ? Parte(proveedor) : null,
            Shipment = Traslado(guia),
        };

        foreach (var relacionado in guia.DocumentosRelacionados)
        {
            documento.AdditionalDocumentReference.Add(new DocumentReference
            {
                ID = MapeadorComun.Id(relacionado.Numero),
                DocumentTypeCode = new CodeType { ListAgencyName = _sunat, ListName = "Documento relacionado al transporte", ListUri = _uriCatalogo + "61", Value = relacionado.Tipo },
                DocumentType = catalogos.Descripcion(DocumentoRelacionadoTraslado.NumeroCatalogo, relacionado.Tipo),
                IssuerParty = new Party
                {
                    PartyIdentification =
                    {
                        new PartyIdentification
                        {
                            ID = new IdentifierType
                            {
                                SchemeId = relacionado.TipoDocumentoEmisor,
                                SchemeAgencyName = _sunat,
                                SchemeUri = _uriCatalogo + "06",
                                Value = relacionado.NumeroDocumentoEmisor ?? emisor.Ruc,
                            },
                        },
                    },
                },
            });
        }

        documento.Signature.Add(MapeadorComun.Firma(emisor, opciones.Value.IdFirma));
        for (var i = 0; i < guia.Bienes.Count; i++)
        {
            documento.DespatchLine.Add(Linea(i + 1, guia.Bienes[i]));
        }

        return documento;
    }

    private static Shipment Traslado(GuiaRemisionCpe guia)
    {
        var publica = guia.Modalidad == ModalidadTraslado.TransportePublico;
        var traslado = new Shipment
        {
            ID = MapeadorComun.Id(IdEnvio),
            HandlingCode = new CodeType { ListAgencyName = _sunat, ListName = "Motivo de traslado", ListUri = _uriCatalogo + "20", Value = guia.Motivo },
            HandlingInstructions = Vacio(guia.DescripcionMotivo),
            GrossWeightMeasure = new MeasureType { UnitCode = guia.UnidadPeso, Valor = guia.PesoBruto },
            TotalTransportHandlingUnitQuantity = guia.Bultos?.ToString(CultureInfo.InvariantCulture),
            Delivery = new Delivery
            {
                DeliveryAddress = Direccion(guia.Llegada),
                Despatch = new Despatch { DespatchAddress = Direccion(guia.Partida) },
            },
        };

        foreach (var indicador in Indicadores(guia.Indicadores))
        {
            traslado.SpecialInstructions.Add(indicador);
        }

        var etapa = new ShipmentStage
        {
            TransportModeCode = new CodeType { ListAgencyName = _sunat, ListName = "Modalidad de traslado", ListUri = _uriCatalogo + "18", Value = guia.Modalidad },
            TransitPeriod = new Period { StartDate = MapeadorComun.Fecha(guia.FechaInicioTraslado) },
            LoadingTransportEvent = publica ? new TransportEvent { OccurrenceDate = MapeadorComun.Fecha(guia.FechaInicioTraslado) } : null,
        };
        if (guia.Transportista is { } transportista)
        {
            etapa.CarrierParty.Add(new Party
            {
                PartyIdentification = { new PartyIdentification { ID = AtributosCatalogo.DocumentoIdentidad(TipoDocumentoIdentidad.Ruc, transportista.Ruc) } },
                PartyLegalEntity =
                {
                    new PartyLegalEntity
                    {
                        RegistrationName = transportista.RazonSocial,
                        CompanyID = transportista.RegistroMtc is { Length: > 0 } mtc ? MapeadorComun.Id(mtc) : null,
                    },
                },
                AgentParty = transportista.Autorizacion is { } autorizacion ? Agente(autorizacion) : null,
            });
        }

        for (var i = 0; i < guia.Conductores.Count; i++)
        {
            var conductor = guia.Conductores[i];
            etapa.DriverPerson.Add(new Person
            {
                ID = new IdentifierType
                {
                    SchemeId = conductor.TipoDocumento,
                    SchemeName = "Documento de Identidad",
                    SchemeAgencyName = _sunat,
                    SchemeUri = _uriCatalogo + "06",
                    Value = conductor.NumeroDocumento,
                },
                FirstName = conductor.Nombres,
                FamilyName = conductor.Apellidos,
                JobTitle = i == 0 ? ConductorPrincipal : ConductorSecundario,
                IdentityDocumentReference = new DocumentReference { ID = MapeadorComun.Id(conductor.Licencia) },
            });
        }

        traslado.ShipmentStage.Add(etapa);
        if (guia.Vehiculos.Count > 0)
        {
            var principal = Vehiculo(guia.Vehiculos[0]);
            foreach (var secundario in guia.Vehiculos.Skip(1))
            {
                principal.AttachedTransportEquipment.Add(Vehiculo(secundario));
            }

            traslado.TransportHandlingUnit.Add(new TransportHandlingUnit { TransportEquipment = principal });
        }

        if (guia.Puerto is { } puerto)
        {
            traslado.FirstArrivalPortLocation = new Location
            {
                ID = MapeadorComun.Id(puerto.Codigo),
                LocationTypeCode = puerto.Tipo,
                Name = Vacio(puerto.Nombre),
            };
        }

        return traslado;
    }

    private DespatchLine Linea(int numero, BienGuiaCpe bien)
    {
        var orden = numero.ToString(CultureInfo.InvariantCulture);
        var item = new Item
        {
            Description = { bien.Descripcion.Trim() },
            SellersItemIdentification = bien.CodigoInterno is { Length: > 0 } codigo ? new ItemIdentification { ID = MapeadorComun.Id(codigo) } : null,
        };
        if (bien.CodigoProductoSunat is { Length: > 0 } producto)
        {
            item.CommodityClassification.Add(new CommodityClassification { ItemClassificationCode = AtributosCatalogo.ProductoSunat(producto) });
        }

        AgregarPropiedad(item, ConceptoPartida, bien.PartidaArancelaria);
        AgregarPropiedad(item, ConceptoDam, bien.NumeroDam);
        AgregarPropiedad(item, ConceptoBienNormalizado, bien.BienNormalizado is { } normalizado ? (normalizado ? "1" : "0") : null);
        AgregarPropiedad(item, ConceptoSerieDam, bien.SerieDam);

        return new DespatchLine
        {
            ID = MapeadorComun.Id(orden),
            DeliveredQuantity = AtributosCatalogo.Cantidad(bien.Cantidad, bien.Unidad),
            OrderLineReference = { new LineReference { LineID = orden } },
            Item = item,
        };
    }

    /// <summary>Propiedad del bien (catálogo N.° 55) con el nombre oficial del concepto; sin valor no se agrega.</summary>
    private void AgregarPropiedad(Item item, string concepto, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return;
        }

        item.AdditionalItemProperty.Add(new ItemProperty
        {
            Name = catalogos.Descripcion("55", concepto),
            NameCode = new CodeType { ListAgencyName = _sunat, ListName = "Propiedad del item", ListUri = _uriCatalogo + "55", Value = concepto },
            Value = valor.Trim(),
        });
    }

    private static TransportEquipment Vehiculo(VehiculoCpe vehiculo) => new()
    {
        ID = MapeadorComun.Id(vehiculo.Placa),
        ApplicableTransportMeans = vehiculo.CertificadoHabilitacion is { Length: > 0 } certificado ? new TransportMeans { RegistrationNationalityID = certificado } : null,
        ShipmentDocumentReference = vehiculo.Autorizacion is { } autorizacion
            ? new DocumentReference { ID = new IdentifierType { SchemeId = autorizacion.Entidad, Value = autorizacion.Numero } }
            : null,
    };

    private static Party Agente(AutorizacionEspecialCpe autorizacion) => new()
    {
        PartyLegalEntity = { new PartyLegalEntity { CompanyID = new IdentifierType { SchemeId = autorizacion.Entidad, Value = autorizacion.Numero } } },
    };

    private static Address Direccion(PuntoTrasladoCpe punto) => new()
    {
        ID = AtributosCatalogo.Ubigeo(punto.Ubigeo),
        AddressTypeCode = punto.CodigoEstablecimiento is { Length: > 0 } establecimiento
            ? new CodeType { ListId = punto.Ruc, ListAgencyName = _sunat, ListName = "Establecimientos anexos", Value = establecimiento }
            : null,
        AddressLine = { new AddressLine { Line = punto.Direccion.Trim() } },
    };

    private static IEnumerable<string> Indicadores(IndicadoresTrasladoCpe indicadores)
    {
        if (indicadores.TransbordoProgramado)
        {
            yield return IndicadorTransbordo;
        }

        if (indicadores.VehiculoM1L)
        {
            yield return IndicadorM1L;
        }

        if (indicadores.RetornoEnvasesVacios)
        {
            yield return IndicadorRetornoEnvases;
        }

        if (indicadores.RetornoVehiculoVacio)
        {
            yield return IndicadorRetornoVacio;
        }

        if (indicadores.TrasladoTotalDamDs)
        {
            yield return IndicadorTotalDamDs;
        }

        if (indicadores.VehiculosConductoresTransportista)
        {
            yield return IndicadorVehiculosTransportista;
        }
    }

    private static AccountParty Parte(ClienteCpe parte) => Parte(parte.TipoDocumento, parte.NumeroDocumento, parte.RazonSocial);

    private static AccountParty Parte(string tipoDocumento, string numeroDocumento, string razonSocial) => new()
    {
        Party = new Party
        {
            PartyIdentification = { new PartyIdentification { ID = AtributosCatalogo.DocumentoIdentidad(tipoDocumento, numeroDocumento) } },
            PartyLegalEntity = { new PartyLegalEntity { RegistrationName = razonSocial.Trim() } },
        },
    };

    private static string? Vacio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
