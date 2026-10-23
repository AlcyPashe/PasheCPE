# Diferencias esperadas frente a los dorados

`DoradosInvoiceTests` arma el modelo desde cada factura aceptada por SUNAT de `tests/Datos/Dorados/01-Factura` (Fattura v2, generadas con GasperSoft), genera el XML con PasheCPE y lo compara por estructura y valores (`ComparadorXml`). La comparación ignora espacios, orden de atributos y declaraciones de namespace, y compara los números por valor («8.00» = «8», «20.0000000000» = «20», «0.00000» = «0.00»). Los archivos de Dorados nunca se modifican.

Solo se aceptan estas diferencias; cualquier otra hace fallar la prueba.

| Ruta | Tipo | Motivo |
|---|---|---|
| `ext:UBLExtensions` y `cac:Signature` | No se comparan | Dependen de la firma (Fase 4): el dorado lleva la firma y el `Id` de GasperSoft (`signatureGASPERSOFT`); PasheCPE usa `IdFirma` (`PasheSign`) |
| `/Invoice/cbc:Note[1000]` | Elemento adicional | PasheCPE siempre agrega el monto en letras (leyenda 1000, PRD RF-03); GasperSoft lo omitía |
| `/Invoice/cbc:Note[2001]` | Elemento adicional | Leyenda de la Amazonía para bienes exonerados (PRD RF-04); GasperSoft la omitía |
| `/Invoice/cbc:InvoiceTypeCode/@name` y `@listSchemeURI` | Atributos adicionales | «Tipo de Operacion» y el URI del catálogo 51, como en la guía de elaboración UBL 2.1 vigente |

## Notas de crédito

`DoradosCreditNoteTests` hace lo mismo con las notas de crédito aceptadas de `tests/Datos/Dorados/07-NotaCredito` (F001-840 y F001-841, motivo 01, sobre facturas exoneradas). El lector arma la `NotaCpe` con el motivo, el sustento y el documento modificado del dorado. Solo se aceptan estas diferencias, además de la firma:

| Ruta | Tipo | Motivo |
|---|---|---|
| `/CreditNote/cbc:Note[1000]` | Elemento adicional | Monto en letras, como en la factura |
| `/CreditNote/cbc:Note[2001]` | Elemento adicional | Leyenda de la Amazonía para bienes exonerados, como en la factura |

No hay nota de débito dorada: la ND se valida con el XSD y las XSL de SUNAT sobre los XML generados (`XslSobreNotasTests`).

## Diferencias que no aparecen en estos dorados

- **Campos 2027** (R.S. 000048-2026): el contrato de colaboración empresarial (`cac:ContractDocumentReference`) solo se genera si el comprobante lo informa y desde el 01/01/2027; ninguno de estos dorados lo tiene.
- **`cbc:IssueTime`**: GasperSoft emitía `00:00:00`. El lector toma la hora del dorado, así que no difiere; en uso normal PasheCPE pone la hora real de Lima.
- **Declaración `xmlns:sac`**: el dorado la declara sin usarla. Las declaraciones de namespace no se comparan.
- **Gravado con IGV**: no hay dorado gravado; esos casos se prueban con cálculos a mano y validación XSD (`CalculadoraTributariaTests`). El primer dorado gravado real saldrá de la Fase 5 (beta).
