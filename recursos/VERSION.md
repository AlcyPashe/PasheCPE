# Recursos oficiales SUNAT

No modificar estos archivos; reemplazarlos solo con nuevas versiones oficiales y registrar el cambio aquí.

| Carpeta | Contenido | Versión | Origen | Registrado |
|---|---|---|---|---|
| `XSD/2.1` | UBL 2.1 (maindoc + common) | Actualizado al 28/02/2022 | https://cpe.sunat.gob.pe/guias-y-manuales | 22/10/2026 |
| `XSD/2.0` | UBL 2.0 SUNAT (SummaryDocuments, VoidedDocuments, extensiones) | Actualizado al 28/02/2022 | ídem | 22/10/2026 |
| `XSL` | Reglas de validación `ValidaExprReg*` | Actualizado al 06/09/2022 | ídem | 22/10/2026 |
| `VALIDACIONES/ReglasValidacion-CPE-2026-08-26.xlsx` | Reglas de validación CPE: hojas por documento, `General `, `CódigosRetorno`, `Catálogos` (Anexo 8), `Parámetros`, `Control de Cambios` (incluye vigencias 01/01/2027). SHA-256 `cb5e871cfe3b81abea7faf25b156e5d35b21e8c4837979350919926612da73b6` | Actualizado al 26.08.2026 | ídem | 22/10/2026 |
| `VALIDACIONES` (GRE) | Reglas de validación GRE | **pendiente (antes de la Fase 10)** | sección GRE del portal | — |
| `CATALOGOS` | Solo catálogos externos enlazados por el Excel (opcional: UNSPSC `CCNU_MOD_2.xlsm`) | — | ídem | — |

## Notas

- Las XSL dependen de `local:///commons/error/validate_utils.xsl`, `local:///commons/error/error_utils.xsl`, `local:///commons/cpe/datos/dat_fechas.xml` y catálogos XML que **no vienen** en la descarga. PasheCPE implementa sus propias versiones (TRD §9.3).
- Uso de cada XSL: Factura → `ValidaExprRegFactura-2.0.1`; Boleta → `ValidaExprRegBoleta-2.0.1`; NC → `ValidaExprRegNC-2.0.1`; ND → `ValidaExprRegND-2.0.1`; RC → `ValidaExprRegSummary-1.1.0`; RA → `ValidaExprRegSummaryVoided-1.0.1`; GRE 09 → `ValidaExprRegGuiaRemitente-2.0.1`. No existe XSL para la GRE transportista (31).
- Fuera de alcance (se conservan como referencia): `ValidaExprRegLiquidacion`, `ValidaExprRegPercepcion`, `ValidaExprRegRetencion`, `ValidaExprRegOtrosVoided` (RR), `ValidaExprRegSummary-1.0.1`, `ValidaExprRegOperador`, `ValidaExprRegAdquiriente`, `ValidaExprRegAdqOtros`.
