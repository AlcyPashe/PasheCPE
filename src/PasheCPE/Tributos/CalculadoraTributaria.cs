using PasheCPE.Catalogos;
using PasheCPE.Tiempo;

namespace PasheCPE.Tributos;

/// <summary>
/// Cálculo tributario de facturas y boletas (TRD §7). El sistema de ventas entrega cantidades, precios y afectaciones;
/// aquí se obtienen bases, impuestos, totales y leyendas. Toda la aritmética es <c>decimal</c> con
/// <see cref="Redondeo"/>.
/// <para>
/// Con precios que incluyen impuestos manda el precio: total de línea = R2(cantidad × precio), base = R2(total ÷
/// (1 + tasa)) e IGV = total − base, de modo que el cliente paga exactamente lo de la etiqueta. El IGV puede diferir
/// en ±0.01 de base × tasa, dentro de la tolerancia de ±1 de SUNAT.
/// </para>
/// <para>
/// Totales según las reglas de SUNAT (3277, 3278, 3279, 3280, 3291, 3294): la base gravada resta los descuentos
/// globales 02 y los anticipos 04 y suma los cargos 49; el total valor de venta y el precio de venta no restan los
/// anticipos; el importe a pagar resta el total de anticipos.
/// </para>
/// </summary>
internal sealed class CalculadoraTributaria(ReglasRegimen reglas, TablaTasas tasas, CatalogosSunat catalogos, CatalogoErrores errores, RelojCpe reloj)
{
    private const string _columnaTributo = "Codigo de tributo";

    public CalculoComprobante Calcular(EmisorCpe emisor, ComprobanteCpe comprobante)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(comprobante);

        var ahora = reloj.AhoraLima();
        var fecha = comprobante.FechaEmision ?? DateOnly.FromDateTime(ahora.DateTime);
        var hora = comprobante.HoraEmision ?? TimeOnly.FromDateTime(ahora.DateTime);
        var tasa = reglas.TasaGravada(emisor, fecha);
        var tarifaIcbper = tasas.Icbper(fecha) ?? 0m;
        var lineas = comprobante.Lineas
            .Select((linea, indice) => CalcularLinea(indice + 1, linea, comprobante.ModoPrecio, emisor, tasa.Tasa, tarifaIcbper))
            .ToList();

        var advertencias = new List<ObservacionCpe>();
        if (tasa.Advertencia is { } advertencia && lineas.Exists(l => l.Tasa > 0))
        {
            advertencias.Add(advertencia);
        }

        var t = tasa.Tasa / 100m;
        var onerosas = lineas.Where(l => !l.EsGratuita).ToList();
        var gratuitas = lineas.Where(l => l.EsGratuita).ToList();
        decimal BaseDe(string tributo) => onerosas.Where(l => l.Tributo == tributo).Sum(l => l.ValorVenta);
        var baseGravada = BaseDe(TributoSunat.Igv);
        var baseExonerada = BaseDe(TributoSunat.Exonerado);
        var baseInafecta = BaseDe(TributoSunat.Inafecto);
        var baseExportacion = BaseDe(TributoSunat.Exportacion);
        var igvLineas = onerosas.Where(l => l.Tributo == TributoSunat.Igv).Sum(l => l.Igv);
        var isc = onerosas.Sum(l => l.Isc);
        var icbper = lineas.Sum(l => l.Icbper);
        var valorVentaLineas = baseGravada + baseExonerada + baseInafecta + baseExportacion;

        var cargos = new List<CargoCalculado>();
        decimal descuentoBase = 0, cargoBase = 0, descuentoNoBase = 0, cargoNoBase = 0;
        foreach (var dc in comprobante.DescuentosCargos)
        {
            var afectaBase = dc.Codigo is CargoDescuento.DescuentoGlobalAfectaBase or CargoDescuento.CargoGlobalAfectaBase;
            var baseCargo = dc.Base ?? (afectaBase ? baseGravada : valorVentaLineas);
            var monto = dc.Monto ?? Redondeo.Monto(baseCargo * (dc.Factor ?? 0m));
            var factor = dc.Factor ?? (baseCargo == 0 ? 0m : Math.Round(monto / baseCargo, 5, MidpointRounding.AwayFromZero));
            cargos.Add(new CargoCalculado(dc.Codigo, dc.EsCargo, factor, monto, baseCargo));
            switch (dc.Codigo)
            {
                case CargoDescuento.DescuentoGlobalAfectaBase: descuentoBase += monto; break;
                case CargoDescuento.CargoGlobalAfectaBase: cargoBase += monto; break;
                case CargoDescuento.DescuentoGlobalNoAfectaBase: descuentoNoBase += monto; break;
                case CargoDescuento.CargoGlobalNoAfectaBase: cargoNoBase += monto; break;
            }
        }

        var anticipos = new List<AnticipoCalculado>();
        decimal anticipoGravado = 0, anticipoExonerado = 0, anticipoInafecto = 0, totalAnticipos = 0;
        foreach (var (anticipo, indice) in comprobante.Anticipos.Select((a, i) => (a, i)))
        {
            var tributo = TributoDe(anticipo.Afectacion ?? reglas.AfectacionPorDefecto(emisor));
            var (codigo, deduccion, baseAnticipo) = tributo switch
            {
                TributoSunat.Igv => (CargoDescuento.AnticipoGravado, Redondeo.Monto(anticipo.Monto / (1 + t)), baseGravada),
                TributoSunat.Exonerado => (CargoDescuento.AnticipoExonerado, anticipo.Monto, baseExonerada),
                _ => (CargoDescuento.AnticipoInafecto, anticipo.Monto, baseInafecta),
            };
            anticipos.Add(new AnticipoCalculado(indice + 1, anticipo, codigo, deduccion));
            cargos.Add(new CargoCalculado(codigo, false, baseAnticipo == 0 ? 0m : Math.Round(deduccion / baseAnticipo, 5, MidpointRounding.AwayFromZero), deduccion, baseAnticipo));
            totalAnticipos += anticipo.Monto;
            switch (codigo)
            {
                case CargoDescuento.AnticipoGravado: anticipoGravado += deduccion; break;
                case CargoDescuento.AnticipoExonerado: anticipoExonerado += deduccion; break;
                default: anticipoInafecto += deduccion; break;
            }
        }

        var igvSinAnticipos = igvLineas - Redondeo.Monto(descuentoBase * t) + Redondeo.Monto(cargoBase * t);
        var igvNeto = igvSinAnticipos - Redondeo.Monto(anticipoGravado * t);
        var valorVenta = valorVentaLineas - descuentoBase + cargoBase;
        var precioVenta = valorVenta + isc + icbper + igvSinAnticipos;
        var importeTotal = precioVenta + cargoNoBase - descuentoNoBase - totalAnticipos;

        // Mismo orden que los comprobantes aceptados por SUNAT: gravado, exonerado, inafecto, exportación, gratuito.
        var subtotales = new List<SubtotalTributo>();
        if (onerosas.Exists(l => l.Tributo == TributoSunat.Igv))
        {
            subtotales.Add(new SubtotalTributo(TributoSunat.Igv, baseGravada - descuentoBase - anticipoGravado + cargoBase, igvNeto));
        }

        if (onerosas.Exists(l => l.Tributo == TributoSunat.Exonerado))
        {
            subtotales.Add(new SubtotalTributo(TributoSunat.Exonerado, baseExonerada - anticipoExonerado, 0m));
        }

        if (onerosas.Exists(l => l.Tributo == TributoSunat.Inafecto))
        {
            subtotales.Add(new SubtotalTributo(TributoSunat.Inafecto, baseInafecta - anticipoInafecto, 0m));
        }

        if (onerosas.Exists(l => l.Tributo == TributoSunat.Exportacion))
        {
            subtotales.Add(new SubtotalTributo(TributoSunat.Exportacion, baseExportacion, 0m));
        }

        if (gratuitas.Count > 0)
        {
            subtotales.Add(new SubtotalTributo(TributoSunat.Gratuito, gratuitas.Sum(l => l.ValorVenta), gratuitas.Sum(l => l.Igv)));
        }

        if (isc > 0)
        {
            subtotales.Add(new SubtotalTributo(TributoSunat.Isc, onerosas.Where(l => l.Isc > 0).Sum(l => l.ValorVenta), isc));
        }

        if (icbper > 0)
        {
            subtotales.Add(new SubtotalTributo(TributoSunat.Icbper, 0m, icbper));
        }

        var erroresCalculo = new List<ErrorCpe>();
        if (importeTotal < 0 || subtotales.Exists(s => s.Base < 0))
        {
            erroresCalculo.Add(errores.Local("PCPE-1290", OrigenError.Modelo, "Anticipos", null, "el importe total o una base imponible resultan negativos"));
        }

        var detraccion = comprobante.Detraccion is { } d
            ? new DetraccionCalculada(d, d.Monto ?? Redondeo.Monto(importeTotal * d.Porcentaje / 100m))
            : null;
        var montoNeto = importeTotal - (detraccion is not null && comprobante.Moneda == "PEN" ? detraccion.Monto : 0m);

        return new CalculoComprobante
        {
            Emisor = emisor,
            Comprobante = comprobante,
            FechaEmision = fecha,
            HoraEmision = hora,
            TasaGravada = tasa.Tasa,
            Lineas = lineas,
            Subtotales = subtotales,
            CargosGlobales = cargos,
            Anticipos = anticipos,
            Detraccion = detraccion,
            Leyendas = Leyendas(emisor, comprobante, lineas, Math.Max(importeTotal, 0m), detraccion is not null),
            ValorVenta = valorVenta,
            PrecioVenta = precioVenta,
            DescuentosNoBase = descuentoNoBase,
            CargosNoBase = cargoNoBase,
            TotalAnticipos = totalAnticipos,
            ImporteTotal = importeTotal,
            TotalTributos = igvNeto + isc + icbper,
            MontoNetoPendiente = montoNeto,
            Advertencias = advertencias,
            Errores = erroresCalculo,
        };
    }

    /// <summary>Tributo de una afectación según la columna «Codigo de tributo» del catálogo N.° 07 (10 → 1000, 21 → 9996…).</summary>
    public string TributoDe(string afectacion)
    {
        var tributo = catalogos.Extra(AfectacionIgv.NumeroCatalogo, afectacion, _columnaTributo)
            ?? throw new InvalidOperationException($"La afectación {afectacion} no tiene tributo en el catálogo N.° 07.");
        return tributo[..4];
    }

    private LineaCalculada CalcularLinea(int numero, LineaCpe linea, ModoPrecio modo, EmisorCpe emisor, decimal tasaGravada, decimal tarifaIcbper)
    {
        var exonerado = reglas.EsExoneradoAmazonia(emisor);
        var afectacion = linea.Afectacion
            ?? (linea.EsGratuito && exonerado ? AfectacionIgv.ExoneradoGratuito : reglas.AfectacionPorDefecto(emisor));
        var tributo = TributoDe(afectacion);
        var gratuita = tributo == TributoSunat.Gratuito;
        var tasa = afectacion.StartsWith('1') ? tasaGravada : 0m;
        var t = tasa / 100m;
        var cantidad = linea.Cantidad;
        var tasaIsc = !gratuita && linea.Isc?.Sistema == SistemaIsc.SistemaAlValor ? (linea.Isc.Tasa ?? 0m) / 100m : 0m;
        var iscUnitario = !gratuita && linea.Isc?.Sistema == SistemaIsc.MontoFijo ? linea.Isc.MontoPorUnidad ?? 0m : 0m;
        var descuento = linea.Descuento ?? 0m;

        decimal bruto, isc, igv, precio;
        if (modo == ModoPrecio.PrecioUnitario)
        {
            var total = Redondeo.Monto(cantidad * linea.ImporteUnitario);
            bruto = Redondeo.Monto(((total / (1 + t)) - (cantidad * iscUnitario)) / (1 + tasaIsc));
            var neto = bruto - descuento;
            isc = Redondeo.Monto((neto * tasaIsc) + (cantidad * iscUnitario));
            igv = descuento == 0 && !gratuita ? total - bruto - isc : Redondeo.Monto((neto + isc) * t);
            precio = linea.ImporteUnitario;
        }
        else
        {
            bruto = Redondeo.Monto(cantidad * linea.ImporteUnitario);
            var neto = bruto - descuento;
            isc = Redondeo.Monto((neto * tasaIsc) + (cantidad * iscUnitario));
            igv = Redondeo.Monto((neto + isc) * t);
            precio = Redondeo.Unitario(((linea.ImporteUnitario * (1 + tasaIsc)) + iscUnitario) * (1 + t));
        }

        return new LineaCalculada
        {
            Numero = numero,
            Linea = linea,
            Afectacion = afectacion,
            Tributo = tributo,
            EsGratuita = gratuita,
            Tasa = tasa,
            ValorBruto = bruto,
            Descuento = descuento,
            ValorVenta = bruto - descuento,
            Isc = isc,
            Igv = igv,
            Icbper = Redondeo.Monto(linea.BolsasIcbper * tarifaIcbper),
            TarifaIcbper = tarifaIcbper,
            ValorUnitario = gratuita ? 0m : Redondeo.Unitario(bruto / cantidad),
            PrecioReferencia = gratuita ? Redondeo.Unitario(bruto / cantidad) : precio,
            TipoPrecio = gratuita ? TipoPrecio.ValorReferencialGratuito : TipoPrecio.PrecioUnitarioConIgv,
        };
    }

    private List<LeyendaCpe> Leyendas(EmisorCpe emisor, ComprobanteCpe comprobante, List<LineaCalculada> lineas, decimal importeTotal, bool conDetraccion)
    {
        var leyendas = new List<LeyendaCpe>
        {
            new() { Codigo = CodigoLeyenda.MontoEnLetras, Texto = NumeroALetras.Convertir(importeTotal, comprobante.Moneda) },
        };

        void Agregar(string codigo)
        {
            if (!leyendas.Exists(l => l.Codigo == codigo))
            {
                leyendas.Add(new LeyendaCpe { Codigo = codigo, Texto = catalogos.TextoLeyenda(codigo) });
            }
        }

        if (lineas.Exists(l => l.EsGratuita))
        {
            Agregar(CodigoLeyenda.TransferenciaGratuita);
        }

        if (reglas.EsExoneradoAmazonia(emisor))
        {
            foreach (var tipo in lineas.Where(l => l.Afectacion == AfectacionIgv.ExoneradoOneroso).Select(l => l.Linea.TipoItem).Distinct().Order())
            {
                Agregar(ReglasRegimen.LeyendaAmazonia(tipo));
            }
        }

        if (conDetraccion)
        {
            Agregar(CodigoLeyenda.OperacionSujetaDetraccion);
        }

        foreach (var adicional in comprobante.LeyendasAdicionales.Where(a => !leyendas.Exists(l => l.Codigo == a.Codigo)))
        {
            leyendas.Add(adicional);
        }

        return leyendas;
    }
}
