using System.Globalization;
using System.Text.RegularExpressions;
using PasheCPE.Catalogos;
using PasheCPE.Tiempo;
using PasheCPE.Tributos;

namespace PasheCPE.Validacion;

/// <summary>
/// Validación del modelo de facturas, boletas y notas (TRD §9.1, nivel 1). Acumula todos los errores, nunca lanza. Los
/// códigos de catálogo se validan existentes y vigentes a la fecha de emisión, con el número de catálogo en el error.
/// <see cref="Validar(EmisorCpe, ComprobanteCpe)"/> y <see cref="Validar(EmisorCpe, NotaCpe)"/> corren antes del
/// cálculo; <see cref="ValidarTotales"/>, después (reglas que dependen de los importes).
/// </summary>
internal sealed partial class ValidadorModelo(
    CatalogosSunat catalogos,
    CatalogoErrores errores,
    ReglasRegimen reglas,
    TablaPlazos plazos,
    RelojCpe reloj,
    ParametrosSunat parametros)
{
    private const string _columnaComprobantes = "Tipo de Comprobante asociado";

    public IReadOnlyList<ErrorCpe> Validar(EmisorCpe emisor, ComprobanteCpe cpe)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(cpe);
        var lista = new List<ErrorCpe>();
        var hoy = reloj.HoyLima();
        var fecha = cpe.FechaEmision ?? hoy;

        void Agregar(ErrorCpe? error)
        {
            if (error is not null)
            {
                lista.Add(error);
            }
        }

        ErrorCpe Local(string codigo, string ruta, params object?[] argumentos) => errores.Local(codigo, OrigenError.Modelo, ruta, null, argumentos);

        void Catalogo(string catalogo, string? codigo, string ruta) => Agregar(catalogos.Validar(catalogo, codigo, fecha, ruta));

        ValidarEmisor(lista, emisor, Catalogo, Local);

        // Documento
        var esFactura = cpe.TipoDocumento == TipoDocumentoSunat.Factura;
        if (!esFactura && cpe.TipoDocumento != TipoDocumentoSunat.BoletaDeVenta)
        {
            lista.Add(Local("PCPE-1203", "TipoDocumento", cpe.TipoDocumento));
            return lista;
        }

        Agregar(reglas.ValidarTipoDocumento(emisor, cpe.TipoDocumento));
        var letra = esFactura ? "F" : "B";
        if (cpe.Serie is null || !Serie().IsMatch(cpe.Serie) || !cpe.Serie.StartsWith(letra, StringComparison.Ordinal))
        {
            lista.Add(Local("PCPE-1204", "Serie", cpe.Serie, cpe.TipoDocumento, letra));
        }

        if (cpe.Numero is < 1 or > 99_999_999)
        {
            lista.Add(Local("PCPE-1205", "Numero", cpe.Numero));
        }

        // Fechas
        if (fecha > hoy)
        {
            lista.Add(Local("PCPE-1206", "FechaEmision", Texto(fecha), Texto(hoy)));
        }
        else if (plazos.FechaLimite(cpe.TipoDocumento, null, fecha) is { } limite && limite < hoy)
        {
            lista.Add(Local("PCPE-1207", "FechaEmision", Texto(limite)));
        }

        if (cpe.FechaVencimiento is { } vencimiento && vencimiento < fecha)
        {
            lista.Add(Local("PCPE-1208", "FechaVencimiento", Texto(vencimiento), Texto(fecha)));
        }

        // Catálogos del documento
        Catalogo("02", cpe.Moneda, "Moneda");
        var errorOperacion = catalogos.Validar(TipoOperacion.NumeroCatalogo, cpe.TipoOperacion, fecha, "TipoOperacion");
        Agregar(errorOperacion);
        if (errorOperacion is null && !AdmiteComprobante(cpe.TipoOperacion, esFactura))
        {
            lista.Add(Local("PCPE-1209", "TipoOperacion", cpe.TipoOperacion, cpe.TipoDocumento));
        }

        // Cliente
        var cliente = cpe.Cliente;
        Catalogo(TipoDocumentoIdentidad.NumeroCatalogo, cliente.TipoDocumento, "Cliente.TipoDocumento");
        if (string.IsNullOrWhiteSpace(cliente.RazonSocial))
        {
            lista.Add(Local("PCPE-1202", "Cliente.RazonSocial", "la razón social o nombre del cliente"));
        }

        if (esFactura && !EsExportacion(cpe.TipoOperacion)
            && (cliente.TipoDocumento != TipoDocumentoIdentidad.Ruc || !DocumentosIdentidad.EsRucValido(cliente.NumeroDocumento)))
        {
            lista.Add(Local("PCPE-1210", "Cliente.NumeroDocumento"));
        }
        else if (!DocumentosIdentidad.EsAnonimo(cliente) && !DocumentosIdentidad.TieneFormato(cliente.TipoDocumento, cliente.NumeroDocumento))
        {
            lista.Add(Local("PCPE-1212", "Cliente.NumeroDocumento", cliente.NumeroDocumento, cliente.TipoDocumento));
        }

        ValidarLineas(lista, emisor, cpe, Catalogo, Local);
        ValidarPagosYCargos(lista, cpe, fecha, Catalogo, Local);
        ValidarReferenciasYLeyendas(lista, cpe, fecha, Catalogo, Local);
        return lista;
    }

    /// <summary>
    /// Nota de crédito (07) o débito (08): motivo y sustento, un único comprobante modificado (factura o boleta) con su
    /// serie coherente con la de la nota, plazo según el documento modificado, y las reglas de los motivos 13. La
    /// validación contra el documento original del historial la hace <see cref="ValidadorDocumentoModificado"/>.
    /// </summary>
    public IReadOnlyList<ErrorCpe> Validar(EmisorCpe emisor, NotaCpe nota)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(nota);
        var lista = new List<ErrorCpe>();
        var hoy = reloj.HoyLima();
        var fecha = nota.FechaEmision ?? hoy;

        void Agregar(ErrorCpe? error)
        {
            if (error is not null)
            {
                lista.Add(error);
            }
        }

        ErrorCpe Local(string codigo, string ruta, params object?[] argumentos) => errores.Local(codigo, OrigenError.Modelo, ruta, null, argumentos);

        void Catalogo(string catalogo, string? codigo, string ruta) => Agregar(catalogos.Validar(catalogo, codigo, fecha, ruta));

        ValidarEmisor(lista, emisor, Catalogo, Local);

        // Documento y motivo
        var esCredito = nota.TipoDocumento == TipoDocumentoSunat.NotaDeCredito;
        if (!esCredito && nota.TipoDocumento != TipoDocumentoSunat.NotaDeDebito)
        {
            lista.Add(Local("PCPE-1401", "TipoDocumento", nota.TipoDocumento));
            return lista;
        }

        Catalogo(esCredito ? MotivoNotaCredito.NumeroCatalogo : MotivoNotaDebito.NumeroCatalogo, nota.Motivo, "Motivo");
        if (!Sustento().IsMatch(nota.Sustento ?? string.Empty))
        {
            lista.Add(Local("PCPE-1402", "Sustento"));
        }

        var ajusteCuotas = esCredito && nota.Motivo == MotivoNotaCredito.CorreccionMontoNetoPendientePago;
        var penalidades = !esCredito && nota.Motivo == MotivoNotaDebito.Penalidades;

        // Documento modificado: uno solo (R.S. 000048-2026), factura o boleta; solo las penalidades pueden omitirlo.
        var modificado = nota.DocumentoModificado;
        string? letra = null;
        if (modificado is null)
        {
            if (!penalidades)
            {
                lista.Add(Local("PCPE-1403", "DocumentoModificado"));
            }
        }
        else if (modificado.TipoDocumento is not (TipoDocumentoSunat.Factura or TipoDocumentoSunat.BoletaDeVenta))
        {
            lista.Add(Local("PCPE-1404", "DocumentoModificado.TipoDocumento", modificado.TipoDocumento));
        }
        else
        {
            letra = modificado.TipoDocumento == TipoDocumentoSunat.Factura ? "F" : "B";
            if (modificado.Serie is null || !Serie().IsMatch(modificado.Serie) || !modificado.Serie.StartsWith(letra, StringComparison.Ordinal))
            {
                lista.Add(Local("PCPE-1405", "DocumentoModificado.Serie", modificado.Serie, modificado.TipoDocumento, letra));
            }

            if (modificado.Numero is < 1 or > 99_999_999)
            {
                lista.Add(Local("PCPE-1205", "DocumentoModificado.Numero", modificado.Numero));
            }

            if (modificado.FechaEmision is { } original && original > fecha)
            {
                lista.Add(Local("PCPE-1408", "DocumentoModificado.FechaEmision", Texto(fecha), Texto(original)));
            }
        }

        Agregar(reglas.ValidarTipoDocumento(emisor, nota.TipoDocumento, modificado?.TipoDocumento));
        if (nota.Serie is null || !Serie().IsMatch(nota.Serie))
        {
            lista.Add(Local("PCPE-1407", "Serie", nota.Serie));
        }
        else if (letra is not null && !nota.Serie.StartsWith(letra, StringComparison.Ordinal))
        {
            lista.Add(Local("PCPE-1406", "Serie", nota.Serie, $"{modificado!.TipoDocumento} {modificado.Serie}", letra));
        }
        else if (letra is null && nota.Serie[0] is not ('F' or 'B'))
        {
            lista.Add(Local("PCPE-1407", "Serie", nota.Serie));
        }

        if (nota.Numero is < 1 or > 99_999_999)
        {
            lista.Add(Local("PCPE-1205", "Numero", nota.Numero));
        }

        if (fecha > hoy)
        {
            lista.Add(Local("PCPE-1206", "FechaEmision", Texto(fecha), Texto(hoy)));
        }
        else if (plazos.FechaLimite(nota.TipoDocumento, TablaPlazos.TipoModificado(nota.TipoDocumento, nota.Serie), fecha) is { } limite && limite < hoy)
        {
            lista.Add(Local("PCPE-1207", "FechaEmision", Texto(limite)));
        }

        Catalogo("02", nota.Moneda, "Moneda");

        // Cliente
        var cliente = nota.Cliente;
        Catalogo(TipoDocumentoIdentidad.NumeroCatalogo, cliente.TipoDocumento, "Cliente.TipoDocumento");
        if (string.IsNullOrWhiteSpace(cliente.RazonSocial))
        {
            lista.Add(Local("PCPE-1202", "Cliente.RazonSocial", "la razón social o nombre del cliente"));
        }

        if (!DocumentosIdentidad.EsAnonimo(cliente) && !DocumentosIdentidad.TieneFormato(cliente.TipoDocumento, cliente.NumeroDocumento))
        {
            lista.Add(Local("PCPE-1212", "Cliente.NumeroDocumento", cliente.NumeroDocumento, cliente.TipoDocumento));
        }

        ValidarLineas(lista, emisor, nota, Catalogo, Local);

        // Las notas no llevan descuentos ni cargos (el Excel de SUNAT no los contempla en 07 ni 08); la forma de pago
        // solo se informa en el ajuste de cuotas, que modifica una factura al crédito (3257, 3259).
        if (nota.DescuentosCargos.Count > 0)
        {
            lista.Add(Local("PCPE-1414", "DescuentosCargos"));
        }

        if (ajusteCuotas)
        {
            if (modificado is not null && modificado.TipoDocumento != TipoDocumentoSunat.Factura)
            {
                lista.Add(Local("PCPE-1409", "DocumentoModificado.TipoDocumento"));
            }

            if (nota.FormaPago is not { Tipo: TipoFormaPago.Credito, Cuotas.Count: > 0 })
            {
                lista.Add(Local("PCPE-1410", "FormaPago"));
            }
        }
        else if (nota.FormaPago is not null)
        {
            lista.Add(Local("PCPE-1412", "FormaPago"));
        }

        ValidarPagosYCargos(lista, nota, fecha, Catalogo, Local);
        ValidarReferenciasYLeyendas(lista, nota, fecha, Catalogo, Local);
        return lista;
    }

    /// <summary>Reglas que dependen de los importes calculados.</summary>
    public IReadOnlyList<ErrorCpe> ValidarTotales(CalculoComprobante calculo)
    {
        ArgumentNullException.ThrowIfNull(calculo);
        var lista = new List<ErrorCpe>();
        var cpe = calculo.Comprobante;
        ErrorCpe Local(string codigo, string ruta, params object?[] argumentos) => errores.Local(codigo, OrigenError.Modelo, ruta, null, argumentos);

        if (cpe is NotaCpe nota)
        {
            // Ajuste de cuotas: el importe total es cero (3315); penalidades: inafectas del IGV (3507).
            if (nota.TipoDocumento == TipoDocumentoSunat.NotaDeCredito && nota.Motivo == MotivoNotaCredito.CorreccionMontoNetoPendientePago && calculo.ImporteTotal != 0)
            {
                lista.Add(Local("PCPE-1411", "Lineas", Monto(calculo.ImporteTotal)));
            }

            if (nota.TipoDocumento == TipoDocumentoSunat.NotaDeDebito && nota.Motivo == MotivoNotaDebito.Penalidades)
            {
                foreach (var linea in calculo.Lineas.Where(l => !l.EsGratuita && l.Igv > 0))
                {
                    lista.Add(Local("PCPE-1413", $"Lineas[{linea.Numero - 1}].Afectacion", linea.Numero, linea.Afectacion));
                }
            }
        }

        if (cpe.TipoDocumento == TipoDocumentoSunat.BoletaDeVenta
            && cpe.Moneda == "PEN"
            && calculo.ImporteTotal > parametros.BoletaSinIdentificacionHasta
            && DocumentosIdentidad.EsAnonimo(cpe.Cliente))
        {
            lista.Add(Local("PCPE-1211", "Cliente", parametros.BoletaSinIdentificacionHasta.ToString("0.00", CultureInfo.InvariantCulture)));
        }

        if (cpe.FormaPago is { Tipo: TipoFormaPago.Credito, Cuotas.Count: > 0 } credito)
        {
            var suma = credito.Cuotas.Sum(c => c.Monto);
            if (suma != calculo.MontoNetoPendiente)
            {
                lista.Add(Local(
                    "PCPE-1231",
                    "FormaPago.Cuotas",
                    suma.ToString("0.00", CultureInfo.InvariantCulture),
                    calculo.MontoNetoPendiente.ToString("0.00", CultureInfo.InvariantCulture)));
            }
        }

        foreach (var (cargo, i) in cpe.DescuentosCargos.Select((c, i) => (c, i)))
        {
            if (cargo.Codigo is CargoDescuento.DescuentoGlobalAfectaBase or CargoDescuento.CargoGlobalAfectaBase
                && !calculo.Lineas.Any(l => !l.EsGratuita && l.Tributo == TributoSunat.Igv))
            {
                lista.Add(Local("PCPE-1261", $"DescuentosCargos[{i}].Codigo", cargo.Codigo));
            }
        }

        return lista;
    }

    private static void ValidarEmisor(List<ErrorCpe> lista, EmisorCpe emisor, Action<string, string?, string> catalogo, Func<string, string, object?[], ErrorCpe> local)
    {
        if (!DocumentosIdentidad.EsRucValido(emisor.Ruc))
        {
            lista.Add(local("PCPE-1201", "Emisor.Ruc", [emisor.Ruc]));
        }

        if (string.IsNullOrWhiteSpace(emisor.RazonSocial))
        {
            lista.Add(local("PCPE-1202", "Emisor.RazonSocial", ["la razón social del emisor"]));
        }

        if (string.IsNullOrWhiteSpace(emisor.Domicilio.Direccion))
        {
            lista.Add(local("PCPE-1202", "Emisor.Domicilio.Direccion", ["la dirección del domicilio fiscal"]));
        }

        if (emisor.Domicilio.Ubigeo is { Length: > 0 } ubigeo)
        {
            catalogo("13", ubigeo, "Emisor.Domicilio.Ubigeo");
        }

        catalogo("04", emisor.Domicilio.CodigoPais, "Emisor.Domicilio.CodigoPais");
        if (!CuatroDigitos().IsMatch(emisor.CodigoEstablecimiento ?? string.Empty))
        {
            lista.Add(local("PCPE-1213", "Emisor.CodigoEstablecimiento", [emisor.CodigoEstablecimiento]));
        }
    }

    /// <summary>Documentos relacionados, leyendas adicionales y contrato de colaboración empresarial.</summary>
    private void ValidarReferenciasYLeyendas(
        List<ErrorCpe> lista,
        DocumentoVentaCpe cpe,
        DateOnly fecha,
        Action<string, string?, string> catalogo,
        Func<string, string, object?[], ErrorCpe> local)
    {
        foreach (var (relacionado, i) in cpe.DocumentosRelacionados.Select((d, i) => (d, i)))
        {
            var esGuia = relacionado.Tipo is TipoDocumentoSunat.GuiaDeRemisionRemitente or TipoDocumentoSunat.GuiaDeRemisionTransportista;
            catalogo(esGuia ? TipoDocumentoSunat.NumeroCatalogo : DocumentoRelacionadoTributario.NumeroCatalogo, relacionado.Tipo, $"DocumentosRelacionados[{i}].Tipo");
            if (string.IsNullOrWhiteSpace(relacionado.Numero))
            {
                lista.Add(local("PCPE-1202", $"DocumentosRelacionados[{i}].Numero", ["el número del documento relacionado"]));
            }
        }

        foreach (var (leyenda, i) in cpe.LeyendasAdicionales.Select((l, i) => (l, i)))
        {
            catalogo(CodigoLeyenda.NumeroCatalogo, leyenda.Codigo, $"LeyendasAdicionales[{i}].Codigo");
        }

        if (cpe.ContratoColaboracion is { } contrato)
        {
            if (fecha < parametros.ContratoColaboracionDesde)
            {
                lista.Add(local("PCPE-1250", "ContratoColaboracion", [Texto(parametros.ContratoColaboracionDesde)]));
            }

            var problema = contrato switch
            {
                { Tipo: not ("1" or "2") } => "el tipo debe ser 1 (ventas) o 2 (adquisiciones)",
                _ when !NumeroContrato().IsMatch(contrato.Numero ?? string.Empty) => "el número debe tener de 1 a 50 caracteres, sin saltos de línea ni tabulaciones",
                _ when !DescripcionContrato().IsMatch(contrato.Descripcion ?? string.Empty) => "la descripción debe tener de 1 a 250 caracteres, sin saltos de línea ni tabulaciones",
                _ when contrato.Porcentaje is <= 0 or >= 100 || decimal.Round(contrato.Porcentaje, 2) != contrato.Porcentaje => "el porcentaje debe tener 2 enteros y hasta 2 decimales",
                _ => null,
            };
            if (problema is not null)
            {
                lista.Add(local("PCPE-1251", "ContratoColaboracion", [problema]));
            }
        }
    }

    private void ValidarLineas(
        List<ErrorCpe> lista,
        EmisorCpe emisor,
        DocumentoVentaCpe cpe,
        Action<string, string?, string> catalogo,
        Func<string, string, object?[], ErrorCpe> local)
    {
        if (cpe.Lineas.Count == 0)
        {
            lista.Add(local("PCPE-1202", "Lineas", ["al menos una línea de detalle"]));
            return;
        }

        foreach (var (linea, i) in cpe.Lineas.Select((l, i) => (l, i)))
        {
            var numero = i + 1;
            var ruta = $"Lineas[{i}]";
            if (linea.Cantidad <= 0)
            {
                lista.Add(local("PCPE-1220", ruta + ".Cantidad", [numero]));
            }

            if (linea.ImporteUnitario < 0)
            {
                lista.Add(local("PCPE-1221", ruta + ".ImporteUnitario", [numero]));
            }

            if (string.IsNullOrWhiteSpace(linea.Descripcion) || linea.Descripcion.Length > 500)
            {
                lista.Add(local("PCPE-1222", ruta + ".Descripcion", [numero]));
            }

            catalogo("03", linea.Unidad, ruta + ".Unidad");
            // El Excel de SUNAT (3496) acepta 00000000 y 99999999 como excepción al listado UNSPSC.
            if (linea.CodigoProductoSunat is { } producto && !OchoDigitos().IsMatch(producto))
            {
                lista.Add(local("PCPE-1223", ruta + ".CodigoProductoSunat", [producto, numero]));
            }

            if (linea.Gtin is { } gtin && !Gtin().IsMatch(gtin))
            {
                lista.Add(local("PCPE-1224", ruta + ".Gtin", [gtin, numero]));
            }

            // SUNAT exige que la cantidad de bolsas sea la cantidad de la línea, redondeada (3236).
            if (linea.BolsasIcbper > 0 && linea.BolsasIcbper != Math.Round(linea.Cantidad, MidpointRounding.AwayFromZero))
            {
                lista.Add(local("PCPE-1219", ruta + ".BolsasIcbper", [numero, linea.BolsasIcbper, linea.Cantidad.ToString(CultureInfo.InvariantCulture)]));
            }

            if (linea.Afectacion is { } afectacion)
            {
                catalogo(AfectacionIgv.NumeroCatalogo, afectacion, ruta + ".Afectacion");
                if (afectacion == AfectacionIgv.GravadoIvap)
                {
                    lista.Add(local("PCPE-1226", ruta + ".Afectacion", [afectacion, numero]));
                }
                else if (linea.EsGratuito != EsAfectacionGratuita(afectacion))
                {
                    lista.Add(local("PCPE-1225", ruta + ".Afectacion", [numero, afectacion]));
                }
            }
            else if (linea.EsGratuito && !reglas.EsExoneradoAmazonia(emisor))
            {
                lista.Add(local("PCPE-1229", ruta + ".Afectacion", [numero]));
            }

            if (cpe is NotaCpe && linea.Descuento is > 0)
            {
                lista.Add(local("PCPE-1415", ruta + ".Descuento", [numero]));
            }
            else if (linea.Descuento is { } descuento && (descuento < 0 || descuento > linea.Cantidad * linea.ImporteUnitario))
            {
                lista.Add(local("PCPE-1228", ruta + ".Descuento", [numero]));
            }

            if (linea.Isc is { } isc)
            {
                catalogo(SistemaIsc.NumeroCatalogo, isc.Sistema, ruta + ".Isc.Sistema");
                var problema = isc.Sistema switch
                {
                    SistemaIsc.SistemaAlValor when isc.Tasa is not > 0 => "el sistema al valor (01) requiere una tasa mayor que cero",
                    SistemaIsc.MontoFijo when isc.MontoPorUnidad is not > 0 => "el sistema de monto fijo (02) requiere el monto por unidad",
                    SistemaIsc.SistemaAlValor or SistemaIsc.MontoFijo => null,
                    _ => "solo se soportan los sistemas 01 (al valor) y 02 (monto fijo)",
                };
                if (problema is not null)
                {
                    lista.Add(local("PCPE-1227", ruta + ".Isc", [numero, problema]));
                }
            }
        }
    }

    private static void ValidarPagosYCargos(
        List<ErrorCpe> lista,
        DocumentoVentaCpe cpe,
        DateOnly fecha,
        Action<string, string?, string> catalogo,
        Func<string, string, object?[], ErrorCpe> local)
    {
        if (cpe.FormaPago is { } pago)
        {
            if (pago.Tipo == TipoFormaPago.Credito && pago.Cuotas.Count == 0)
            {
                lista.Add(local("PCPE-1230", "FormaPago.Cuotas", []));
            }

            if (pago.Tipo == TipoFormaPago.Contado && pago.Cuotas.Count > 0)
            {
                lista.Add(local("PCPE-1233", "FormaPago.Cuotas", []));
            }

            foreach (var (cuota, i) in pago.Cuotas.Select((c, i) => (c, i)))
            {
                if (cuota.FechaVencimiento <= fecha || cuota.Monto <= 0)
                {
                    lista.Add(local("PCPE-1232", $"FormaPago.Cuotas[{i}]", [i + 1]));
                }
            }
        }

        var factura = cpe as ComprobanteCpe;
        if (factura?.Detraccion is { } detraccion)
        {
            if (cpe.TipoDocumento != TipoDocumentoSunat.Factura)
            {
                lista.Add(local("PCPE-1243", "Detraccion", []));
            }

            if (cpe.TipoOperacion is not ("1001" or "1002" or "1003" or "1004"))
            {
                lista.Add(local("PCPE-1240", "TipoOperacion", []));
            }

            catalogo(BienServicioDetraccion.NumeroCatalogo, detraccion.CodigoBienServicio, "Detraccion.CodigoBienServicio");
            catalogo(MedioPago.NumeroCatalogo, detraccion.MedioPago, "Detraccion.MedioPago");
            if (string.IsNullOrWhiteSpace(detraccion.CuentaBancoNacion) || detraccion.Porcentaje <= 0)
            {
                lista.Add(local("PCPE-1241", "Detraccion", []));
            }

            if (cpe.Moneda != "PEN" && detraccion.Monto is null)
            {
                lista.Add(local("PCPE-1242", "Detraccion.Monto", []));
            }
        }

        // Las notas no admiten descuentos ni cargos: ya se rechazan con PCPE-1414.
        foreach (var (cargo, i) in (factura?.DescuentosCargos ?? []).Select((c, i) => (c, i)))
        {
            var ruta = $"DescuentosCargos[{i}]";
            var esCargoSegunCodigo = cargo.Codigo switch
            {
                CargoDescuento.DescuentoGlobalAfectaBase or CargoDescuento.DescuentoGlobalNoAfectaBase => false,
                CargoDescuento.CargoGlobalAfectaBase or CargoDescuento.CargoGlobalNoAfectaBase => true,
                _ => (bool?)null,
            };
            if (esCargoSegunCodigo is null)
            {
                lista.Add(local("PCPE-1260", ruta + ".Codigo", [cargo.Codigo]));
            }
            else if (esCargoSegunCodigo != cargo.EsCargo || (cargo.Monto is not > 0 && cargo.Factor is not > 0))
            {
                lista.Add(local("PCPE-1262", ruta, [cargo.Codigo]));
            }
        }

        foreach (var (anticipo, i) in (factura?.Anticipos ?? []).Select((a, i) => (a, i)))
        {
            if (anticipo.TipoDocumento is not (DocumentoRelacionadoTributario.FacturaAnticipos or DocumentoRelacionadoTributario.BoletaAnticipos)
                || !SerieNumero().IsMatch(anticipo.SerieNumero ?? string.Empty)
                || anticipo.Monto <= 0)
            {
                lista.Add(local("PCPE-1270", $"Anticipos[{i}]", [i + 1]));
            }

            if (anticipo.Afectacion is { } afectacion)
            {
                catalogo(AfectacionIgv.NumeroCatalogo, afectacion, $"Anticipos[{i}].Afectacion");
            }
        }
    }

    /// <summary>La columna «Tipo de Comprobante asociado» del catálogo N.° 51 admite el comprobante.</summary>
    private bool AdmiteComprobante(string tipoOperacion, bool esFactura)
    {
        var asociados = catalogos.Extra(TipoOperacion.NumeroCatalogo, tipoOperacion, _columnaComprobantes) ?? string.Empty;
        return asociados.Contains(esFactura ? "Factura" : "Boleta", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EsExportacion(string tipoOperacion) => tipoOperacion is ['0', '2', '0', _] || tipoOperacion == "0401";

    private static bool EsAfectacionGratuita(string afectacion) =>
        afectacion is "11" or "12" or "13" or "14" or "15" or "16" or "21" or "31" or "32" or "33" or "34" or "35" or "36" or "37";

    private static string Texto(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Monto(decimal monto) => monto.ToString("0.00", CultureInfo.InvariantCulture);

    // Sustento de la nota (2135, 2136): de 1 a 500 caracteres, con espacios pero sin otros caracteres de espaciado.
    [GeneratedRegex(@"^(?=.*[^ ])(?:[^\s]| ){1,500}$")]
    private static partial Regex Sustento();

    [GeneratedRegex(@"^[A-Z0-9]{4}$")]
    private static partial Regex Serie();

    [GeneratedRegex(@"^\d{4}$")]
    private static partial Regex CuatroDigitos();

    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex OchoDigitos();

    [GeneratedRegex(@"^(\d{8}|\d{12}|\d{13}|\d{14})$")]
    private static partial Regex Gtin();

    // «Alfanumérico» en el Excel de SUNAT (3501, 3502): cualquier carácter, incluido el espacio, pero ningún otro
    // carácter de espaciado (salto de línea, tabulación…) y no solo espacios.
    [GeneratedRegex(@"^(?=.*[^ ])(?:[^\s]| ){1,50}$")]
    private static partial Regex NumeroContrato();

    [GeneratedRegex(@"^(?=.*[^ ])(?:[^\s]| ){1,250}$")]
    private static partial Regex DescripcionContrato();

    [GeneratedRegex(@"^[A-Z0-9]{4}-\d{1,8}$")]
    private static partial Regex SerieNumero();
}
