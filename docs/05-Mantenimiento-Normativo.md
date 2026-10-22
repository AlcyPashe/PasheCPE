# Mantenimiento normativo — PasheCPE

Cómo absorber cambios de SUNAT sin tocar los sistemas de ventas.

## 1. Dónde vive cada cosa

| Cambio de SUNAT | Archivo | Versión del paquete |
|---|---|---|
| Tasa (IGV/IPM, MYPE, ICBPER) | `src/PasheCPE/Recursos/tasas.json` | parche |
| Plazo de envío | `plazos.json` | parche |
| Nueva versión del Excel de reglas (códigos, catálogos, reglas) | `recursos/VALIDACIONES` → `dotnet run --project tools/GeneradorRecursos` → revisar diff de `errores-sunat.json`, `catalogos.json`, `reglas-cpe.json` | parche o menor |
| Endpoint | `endpoints.json` | parche |
| Regla de validación | `recursos/XSL` (si SUNAT publica) o `Validacion/Reglas2027` + `reglas-2027.json` | menor |
| Campo XML nuevo | `recursos/XSD` → `tools/generar-ubl.ps1` → perfil `Ubl/` + mapeador + modelo | menor (mayor si rompe el contrato) |
| Canal nuevo | `Transporte/` | menor |

## 2. Procedimiento

1. Descargar el material oficial de https://cpe.sunat.gob.pe/guias-y-manuales y la norma de El Peruano.
2. Copiarlo a `recursos/` y actualizar `recursos/VERSION.md` (fecha, URL, resolución).
3. Regenerar (catálogos o referencia UBL) y revisar el diff.
4. Ajustar datos, reglas o perfil; agregar pruebas con casos válidos e inválidos del cambio.
5. `dotnet test`, entrada en `CHANGELOG.md`, tag `vX.Y.Z` → publicación automática.
6. Actualizar el paquete en cada sistema de ventas.

## 3. Calendario conocido

| Fecha | Cambio | Acción en PasheCPE |
|---|---|---|
| 01/01/2027 | Entra en vigor la R.S. 000048-2026 (postergada por la R.S. 000143-2026): NC/ND con un solo comprobante, ND motivo 13, NC motivo 03, código de producto, contrato de colaboración | Reglas 2027 ya implementadas; confirmar códigos con el listado oficial |
| 01/01/2027 | IPM 3 % / IGV 15 % (total 18 %); tasa MYPE restaurantes/hoteles 12 % + 3 % = 15 % | Ya en `tasas.json` |
| 01/04/2027 | Designaciones DAE/DAEE (fuera de alcance) | Ninguna |
| 01/01/2028 | IPM 3.5 %; fin de la tasa especial MYPE (salvo prórroga) | Revisar prórroga |
| 01/01/2029 | IPM 4 % / IGV 14 % | Ya en `tasas.json` |

Revisar cada diciembre las leyes de prórroga (tasa MYPE, exoneraciones de la Amazonía) y cada trimestre las publicaciones del portal CPE.
