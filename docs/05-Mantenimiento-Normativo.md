# Mantenimiento normativo — PasheCPE

Cómo absorber cambios de SUNAT sin tocar los sistemas de ventas.

## 1. Dónde vive cada cosa

| Cambio de SUNAT | Archivo | Versión del paquete |
|---|---|---|
| Tasa (IGV/IPM, MYPE, ICBPER) | `src/PasheCPE/Recursos/tasas.json` | parche |
| Plazo de envío | `plazos.json` | parche |
| Nueva versión del Excel de reglas (códigos, catálogos, reglas) | `recursos/VALIDACIONES` → `dotnet run --project tools/GeneradorRecursos` → revisar diff de `errores-sunat.json`, `catalogos.json`, `reglas-cpe.json`, `CodigosSunat.g.cs` y `nombres-constantes.json` (constantes nuevas o retiradas) | parche o menor (mayor si se retira una constante) |
| Vigencia de un código de catálogo o nuevo fault transitorio | `tools/GeneradorRecursos/Datos/vigencias-catalogos.json` o `errores-transitorios.json` (citar la fila de `Control de Cambios`) → ejecutar el generador | parche |
| Endpoint | `endpoints.json` | parche |
| Regla de validación | `recursos/XSL` (si SUNAT publica, ver §2.1) o una `IReglaCpe` en `Validacion/Reglas` + su clasificación en `ajustes-xsl.json` | menor |
| Unidad de medida de uso habitual | `tools/GeneradorRecursos/Datos/unidades-medida.json` → ejecutar el generador | menor (constante nueva) |
| Campo XML nuevo | `recursos/XSD` → `tools/generar-ubl.ps1` → perfil `Ubl/` + mapeador + modelo | menor (mayor si rompe el contrato) |
| Canal nuevo | `Transporte/` | menor |

## 2. Procedimiento

1. Descargar el material oficial de https://cpe.sunat.gob.pe/guias-y-manuales y la norma de El Peruano.
2. Copiarlo a `recursos/` y actualizar `recursos/VERSION.md` (fecha, URL, resolución).
3. Regenerar (catálogos o referencia UBL) y revisar el diff.
4. Ajustar datos, reglas o perfil; agregar pruebas con casos válidos e inválidos del cambio.
5. `dotnet test`, entrada en `CHANGELOG.md`, tag `vX.Y.Z` → publicación automática.
6. Actualizar el paquete en cada sistema de ventas.

### 2.1 XSL nuevas de SUNAT o Excel nuevo

Cuando SUNAT publique XSL nuevas (o un Excel de reglas nuevo), **revisar completo** `src/PasheCPE/Recursos/ajustes-xsl.json` y `xsl-compatibilidad.json`:

1. Reemplazar los archivos en `recursos/XSL` (sin editarlos), actualizar `VERSION.md` y, si cambió el nombre, `MotorXslSunat.XslPorDocumento` y el `EmbeddedResource` del `.csproj`.
2. Correr `dotnet test`. Las pruebas que obligan a revisar:
   - `MotorXslSunatTests` (conteos de correcciones de compatibilidad);
   - `InventarioXslTests` (plantillas, parámetros, claves `dp:variable`, funciones, catálogos y propiedades nuevas);
   - `AjustesXslTests` (filas del Excel sin clasificar o códigos suprimidos que ya no están activos en la XSL).
3. Por cada código suprimido: si la XSL nueva ya calcula como el Excel, quitar la supresión y cambiar la clasificación a `xsl`. Si no, mantenerla con su regla C#, su prueba y el motivo actualizado.
4. Por cada fila nueva del Excel posterior a la XSL: clasificarla (`nueva` con su `IReglaCpe` y una prueba que la dispare y otra que no; `xsl`; `modelo`; o `noVerificable` con el motivo).
5. Validar los dorados y los casos tributarios (`XslSobreGeneradosTests`): ningún falso error.

## 3. Calendario conocido

| Fecha | Cambio | Acción en PasheCPE |
|---|---|---|
| 01/01/2027 | Entra en vigor la R.S. 000048-2026 (postergada por la R.S. 000143-2026): NC/ND con un solo comprobante, ND motivo 13, NC motivo 03, código de producto, contrato de colaboración | Facturas y boletas: código de producto (3496) y contrato de colaboración (3497–3502) implementados en la Fase 3; NC/ND en la Fase 7. Confirmar los códigos con el listado oficial |
| 01/01/2027 | IPM 3 % / IGV 15 % (total 18 %); tasa MYPE restaurantes/hoteles 12 % + 3 % = 15 % | Ya en `tasas.json` |
| 01/04/2027 | Designaciones DAE/DAEE (fuera de alcance) | Ninguna |
| 01/01/2028 | IPM 3.5 %; fin de la tasa especial MYPE (salvo prórroga) | Revisar prórroga |
| 01/01/2029 | IPM 4 % / IGV 14 % | Ya en `tasas.json` |

Revisar cada diciembre las leyes de prórroga (tasa MYPE, exoneraciones de la Amazonía) y cada trimestre las publicaciones del portal CPE.
