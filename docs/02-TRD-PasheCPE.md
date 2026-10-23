# TRD — Documento de Requisitos Técnicos
## PasheCPE — Librería de comprobantes y guías electrónicas SUNAT

| Campo | Valor |
|---|---|
| Producto | PasheCPE / PasheCPE.Pdf |
| Plataforma | .NET 10 LTS (`net10.0`), C# 14, `nullable enable` |
| Entorno | Windows, VS Code + .NET CLI + Claude Code, PowerShell |
| Versión del documento | 2.0 — 22/10/2026 |
| Documento rector | `01-PRD-PasheCPE.md` (ante conflicto prevalece el PRD) |

> Convención: los nombres de dominio van en español; las clases UBL conservan los nombres del estándar (`Invoice`, `InvoiceLine`, `TaxTotal`…). Las URL y códigos marcados **[verificar]** deben confirmarse contra el manual vigente o un envío real antes de fijarlos como valores por defecto.

---

## 1. Dependencias

| Uso | Paquete | Proyecto | Nota |
|---|---|---|---|
| Firma XML-DSig | `System.Security.Cryptography.Xml` | PasheCPE | Microsoft |
| DI, opciones, logging | `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Http` | PasheCPE | Microsoft |
| Contenedor para `PasheCpe.Crear` (sin DI) | `Microsoft.Extensions.DependencyInjection` | PasheCPE | Microsoft; ya llegaba como transitiva de `Microsoft.Extensions.Http` |
| Resiliencia HTTP | `Microsoft.Extensions.Http.Resilience` | PasheCPE | Microsoft; solo operaciones idempotentes |
| Serialización rápida | `Microsoft.XmlSerializer.Generator` | PasheCPE | Microsoft; genera `PasheCPE.XmlSerializers.dll` en compilación (va en el `.nupkg`). En el SDK 10 necesita también `DotNetCliToolReference`, porque el target del paquete invoca `dotnet Microsoft.XmlSerializer.Generator`; `SGenTypes` lo limita a los documentos UBL raíz |
| QR | `QRCoder` | PasheCPE | MIT, utilitario permitido |
| PDF | `QuestPDF` | PasheCPE.Pdf | Licencia Community (ingresos < USD 1 M) |
| Pruebas | `xunit.v3.mtp-off`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Microsoft.Extensions.TimeProvider.Testing`, `coverlet.collector` | tests | VSTest; `xunit.v3.mtp-off` es xUnit v3 sin Microsoft Testing Platform (con el SDK 10, `xunit.v3` 4.x no corre bajo VSTest) |
| Generación de código (solo desarrollo) | `dotnet-xscgen` | herramienta | No se distribuye |
| Lectura del Excel de SUNAT (solo desarrollo) | `DocumentFormat.OpenXml` | `tools\GeneradorRecursos` | Microsoft; no se distribuye |

- Versiones centralizadas en `Directory.Packages.props` (Central Package Management). En la Fase 0 se fija la última estable de cada paquete.
- **Prohibido** como dependencia: GasperSoft, OpenInvoicePeru, Greenter, cualquier SDK de facturación de terceros, WCF (`System.ServiceModel.*`), Newtonsoft.Json. Cualquier paquete no listado se consulta antes.

## 2. Estructura del repositorio

```
D:\Sistemas\PasheCPE\                     (raíz git → github.com/AlcyPashe/PasheCPE)
├── PasheCPE.slnx
├── global.json  Directory.Build.props  Directory.Packages.props  nuget.config
├── .editorconfig  .gitignore  .gitattributes  README.md  CHANGELOG.md  CLAUDE.md
├── .github\workflows\ci.yml  publicar.yml
├── docs\                                  PRD, TRD, Plan, guías
├── recursos\                              fuentes oficiales SUNAT (versionadas)
│   ├── XSD\2.0\ XSD\2.1\                  XSD 28/02/2022
│   ├── XSL\                               XSL 06/09/2022
│   ├── VALIDACIONES\                      ReglasValidacion-CPE-2026-08-26.xlsx (reglas, CódigosRetorno, Catálogos,
│   │                                      Control de Cambios) + ReglasValidacion-GRE-2026-09-25.xlsx
│   ├── CATALOGOS\                         solo catálogos externos que el Excel referencia por enlace
│   │                                      (p. ej. UNSPSC completo CCNU_MOD_2.xlsm para el Cat. 25)
│   └── VERSION.md                         origen, fecha y resolución de cada archivo
├── tools\
│   ├── generar-ubl.ps1                    XSD → clases de referencia (tools\ubl-referencia\, no compila)
│   ├── GeneradorRecursos\                 consola C#: Excel SUNAT → JSON embebidos + XML de catálogos para las XSL
│   │   └── Datos\                         datos curados: vigencias y formatos de catálogos, errores transitorios,
│   │                                      nombres estables de las constantes
│   └── github-workflows\                  workflows a mover a .github\workflows\ en la Fase 0
├── src\
│   ├── PasheCPE\                          paquete principal
│   │   ├── Modelos\          Entrada: EmisorCpe, ComprobanteCpe, LineaCpe, NotaCpe, ResumenDiarioCpe,
│   │   │                     ComunicacionBajaCpe, GuiaRemisionCpe, ClienteCpe, DireccionCpe, ReferenciaCpe…
│   │   ├── Resultados\       ResultadoGeneracion, ResultadoEnvio, ResultadoEmision, EstadoCpe, ErrorCpe,
│   │   │                     ObservacionCpe, RutasCpe, IntentoEnvio, HistorialCpe, EstadoConectividad
│   │   ├── Catalogos\        CodigosSunat.g.cs (constantes generadas) + CatalogosSunat + TablaTasas + TablaPlazos +
│   │   │                     CatalogoErrores + CatalogoEndpoints
│   │   ├── Tiempo\           ZonaLima, RelojCpe (TimeProvider → hora de Lima)
│   │   ├── Tributos\         CalculadoraTributaria, ReglasRegimen, NumeroALetras, Redondeo
│   │   ├── Ubl\Comunes\      cbc/cac/ext (perfil SUNAT; namespace PasheCPE.Ubl, públicas pero ocultas)
│   │   ├── Ubl\Documentos\   Invoice, CreditNote, DebitNote, SummaryDocuments, VoidedDocuments,
│   │   │                     DespatchAdvice, ApplicationResponse (lectura)
│   │   ├── Mapeo\            MapeadorComun, MapeadorInvoice, AtributosCatalogo, ConstructorXml, GeneradorInvoice y
│   │   │                     GeneradorNota (→ DATA), MapeadorCreditNote, MapeadorDebitNote, MapeadorSummary,
│   │   │                     MapeadorVoided, MapeadorDespatchAdvice
│   │   ├── Serializacion\    SerializadorUbl
│   │   ├── Validacion\       ValidadorModelo, ValidadorDocumentoModificado, ValidadorEsquema, MotorXslSunat, ValidadorSunat, Precalentador,
│   │   │                     Emulacion\ (DataPower, EXSLT, resolutor local:///, adaptador),
│   │   │                     Reglas\ (reglas complementarias y ajustes de la XSL)
│   │   ├── Firma\            ProveedorCertificados, FirmadorXml
│   │   ├── Empaquetado\      EmpaquetadorZip, NombreArchivoCpe
│   │   ├── Qr\               ConstructorContenidoQr, GeneradorQrPng
│   │   ├── Conectividad\     VerificadorConectividad
│   │   ├── Transporte\Soap\  ClienteSoap, ConstructorSobreSoap, LectorFaultSoap
│   │   ├── Transporte\Gre\   ClienteGre, ProveedorTokenGre
│   │   ├── Respuestas\       LectorCdr, ClasificadorRespuesta, ConsultorTicket
│   │   ├── Almacenamiento\   AlmacenCpe, RegistroHistorial
│   │   ├── Servicios\        ServicioCpe, ServicioGre (fachadas públicas)
│   │   ├── Configuracion\    OpcionesPasheCpe, EndpointsCpe, validación de opciones
│   │   ├── Diagnostico\      Telemetria (ActivitySource, Meter), Enmascarador
│   │   ├── Recursos\         *.json, catalogos\*.xml, xsl-utils\*.xsl, xsd\, xsl\ (EmbeddedResource)
│   │   ├── PasheCpe.cs       fábrica sin DI
│   │   └── ServiceCollectionExtensions.cs
│   └── PasheCPE.Pdf\
│       ├── Disenos\          DisenoComprobante, DisenoGuia
│       ├── Lectura\          LectorRepresentacion (XML firmado → modelo de impresión)
│       ├── MarcaEmisor.cs  GeneradorPdf.cs  ServiceCollectionExtensions.cs
├── tests\
│   ├── PasheCPE.Tests\                    unitarias (sin red)
│   ├── PasheCPE.Pdf.Tests\
│   ├── GeneradorRecursos.Tests\           idempotencia del generador y recursos al día con el Excel
│   └── Datos\                             XML dorados, CDR reales, respuestas SOAP/REST grabadas
├── samples\
│   └── PasheCPE.Demo\                     consola para envíos reales a beta/producción (manual)
└── benchmarks\PasheCPE.Benchmarks\        (opcional, Fase 12)
```

Decisión: **un solo ensamblado principal** con carpetas por responsabilidad y tipos `internal` (solo la fachada, modelos, resultados, opciones e interfaces son `public`). Menos ensamblados = arranque más rápido, un solo paquete que versionar y cero dependencias circulares. `PasheCPE.Pdf` va aparte porque arrastra QuestPDF. `InternalsVisibleTo` solo para los proyectos de prueba.

**Excepción — `PasheCPE.Ubl`:** las clases del perfil UBL son `public` porque `XmlSerializer` y su pregeneración solo admiten tipos públicos. Llevan `[EditorBrowsable(Never)]` y **no forman parte del contrato estable**: pueden cambiar en versiones menores sin aviso (así se indica en el README y en los XML-docs). El contrato es el de `Modelos`, `Resultados` y las interfaces.

## 3. Recursos SUNAT y generación

### 3.1 Clases UBL (perfil SUNAT)

1. `tools\generar-ubl.ps1` ejecuta `dotnet-xscgen` sobre `recursos\XSD\2.1\maindoc\UBL-{Invoice,CreditNote,DebitNote,DespatchAdvice,ApplicationResponse}-2.1.xsd` y `recursos\XSD\2.0\maindoc\UBLPE-{SummaryDocuments,VoidedDocuments}-1.0.xsd` hacia `tools\ubl-referencia\` (no compila).
2. A partir de esa referencia se crean en `src\PasheCPE\Ubl\` las clases del **perfil SUNAT**: solo los elementos que usan las guías de elaboración de SUNAT, con el mismo nombre, namespace y orden del XSD (`[XmlElement(Order = n)]`).
3. Motivo: el UBL 2.1 completo genera cientos de tipos; con `XmlSerializer` eso cuesta segundos y memoria en el primer uso. El perfil es pequeño y se **comprueba contra el XSD oficial** en cada prueba (§9.2), así que no pierde exactitud.
4. Los serializadores se pregeneran en compilación con `Microsoft.XmlSerializer.Generator` y se cachean en instancias estáticas.

Cuando SUNAT publique XSD nuevos: copiar a `recursos\XSD`, actualizar `VERSION.md`, regenerar la referencia, comparar diferencias, ajustar el perfil y correr la suite (procedimiento en `05-Mantenimiento-Normativo.md`).

### 3.2 Excel de reglas de SUNAT (fuente única de catálogos, reglas y códigos)

Fuente: `recursos\VALIDACIONES\ReglasValidacion-CPE-2026-08-26.xlsx` (publicado en https://cpe.sunat.gob.pe/guias-y-manuales). El archivo original **no se edita ni se divide**; `tools\GeneradorRecursos` lo lee con `DocumentFormat.OpenXml` usando los **valores calculados** de las celdas (varias columnas son fórmulas `VLOOKUP`) y genera recursos embebidos en `src\PasheCPE\Recursos\`:

| Hoja(s) del Excel | Salida | Uso |
|---|---|---|
| `Catálogos` (45 bloques verticales `No.` / `Catálogo` / encabezados, catálogos 01–27, 25.1–25.3 y 51–65) | `catalogos.json` + `catalogos\cat_XX.xml` + `Catalogos\CodigosSunat.g.cs` | Validación de códigos por fecha (`CatalogosSunat`), constantes públicas y plantilla `findElementInCatalog` de las XSL |
| `CódigosRetorno` (≈2 077 códigos) | `errores-sunat.json` | Descripción oficial; `tipo` y `transitorio` se completan con la hoja `General ` y una lista manual revisada |
| `General ` (errores de servicio/sobre: 0100, 0109, 0111, 0151…) | `errores-sunat.json` (tipo `servicio`) | Clasificación de faults (TRD §5.1) |
| `Factura2_0`, `Boleta2_0`, `NotaCredito2_0`, `NotaDebito2_0`, `Resumen Diario1_1`, `Comunicación de Baja1_0` | `reglas-cpe.json` (N.°, dato, tag UBL, condición, tipo `error`/`observacion`, código; «OBSERV ERROR» → `tipo: error` + `tipoAnterior: observacion`) | Matriz de reglas: fuente de las pruebas de validación y de las reglas C# que las XSL 2022 no cubren |
| `Control de Cambios` | `reglas-cpe.json` (campo `vigencia`) | Identifica las reglas con vigencia 01/01/2027 (R.S. 000048-2026) |
| `Parámetros` (004 Plazo máximo de envío) | contraste con `plazos.json` | Verificación de plazos |

Hojas fuera de alcance (no se procesan): Retenciones, Percepciones, Resumen de reversiones, Liquidación de compra, DAE-*, DOAE-*, CDR-OSE-*, `Firma` (solo referencia), `Listados` (padrones que SUNAT consulta del lado servidor; no se replican).

Notas del archivo:
- Los catálogos 02 (ISO 4217), 03 (UN/ECE Rec. 20), 04 (ISO 3166) y 25 (UNSPSC completo) solo aparecen como **enlace**. Para la validación local basta el formato; si se requiere la lista completa del 25, el desarrollador descarga `CCNU_MOD_2.xlsm` a `recursos\CATALOGOS` (opcional, recurso comprimido y de carga perezosa).
- El Excel CPE **no incluye la GRE**; sus reglas están en `recursos\VALIDACIONES\ReglasValidacion-GRE-2026-09-25.xlsx` (sección GRE del portal), que se procesa en la Fase 10.
- Al publicar SUNAT una versión nueva del Excel: reemplazar el archivo, actualizar `VERSION.md`, ejecutar el generador y revisar el diff de los JSON.
- **Datos curados** (`tools\GeneradorRecursos\Datos\`, cada entrada cita su fila de `Control de Cambios`): lo que el Excel solo describe en texto libre.
  - `vigencias-catalogos.json`: desde/hasta de códigos o catálogos completos (Cat. 01 código 25, Cat. 10 código 13, Cat. 11 códigos 05–09, Cat. 25.1–25.3 retirados).
  - `formatos-catalogos.json`: expresión regular de los catálogos publicados solo como enlace (02, 03, 04, 13, 25).
  - `errores-transitorios.json` (revisado con el desarrollador el 22/10/2026): `transitorios` (18 faults que describen una falla interna de SUNAT → `transitorio = true`), `enProceso` (0140 → `enProceso = true`) y `noTransitoriosRevisados` (0250, 0252: apuntan al ZIP enviado; solo documentación). Un código nuevo se detecta con el historial `LOG` y se agrega aquí.
  - `nombres-constantes.json`: nombre de cada constante de `CodigosSunat.g.cs`. Los códigos nuevos reciben un nombre derivado de la descripción, que queda fijo aunque SUNAT cambie el texto (así no se rompe el contrato público).
  - `unidades-medida.json`: unidades de medida de uso habitual (NIU, ZZ, KGM, LTR, MTR, BX, GLL…) → clase `UnidadMedida` (catálogo N.° 03, que el Excel publica solo como enlace). Cada código debe cumplir el formato del catálogo; cualquier otro código con ese formato se sigue aceptando.
- Las fechas de `Control de Cambios` llegan como serial de Excel o texto; si una celda trae dos fechas (vigencia postergada) vale la última.
- `GeneradorRecursos.Tests` falla si los recursos del repositorio no coinciden con lo que genera el Excel vigente.

### 3.3 Datos versionados embebidos (sobrescribibles por configuración)

| Archivo | Contenido |
|---|---|
| `tasas.json` | IGV/IPM, tasa especial MYPE restaurantes/hoteles, ICBPER, con `desde`/`hasta` |
| `plazos.json` | Plazos de envío por tipo de documento |
| `catalogos.json` | Catálogos con vigencia por código — generado del Excel (§3.2). Las vigencias se sobrescriben con `PasheCPE:VigenciasCatalogos` (catálogo, código opcional, desde, hasta), por si SUNAT vuelve a postergar un código |
| `errores-sunat.json` | Código → descripción oficial, tipo (`servicio`, `rechazo`, `observacion`), `transitorio` y `enProceso` (bool) — generado del Excel (§3.2) |
| `reglas-cpe.json` | Matriz de reglas por documento con tag UBL, tipo, código y vigencia — generado del Excel (§3.2) |
| `endpoints.json` | Endpoints por canal y ambiente |
| `parametros.json` | Parámetros legales que no son tasas ni plazos: importe de la boleta sin identificar al cliente (S/ 700), inicio del contrato de colaboración empresarial (01/01/2027) |
| `monedas.json`, `errores-locales.json` | Nombres de moneda para el monto en letras; mensajes de la validación local (`PCPE-xxxx`) |
| `estados-consulta-cdr.json` | Estados de `getStatusCdr` (`statusCode` 0001–0012) y su significado (aceptado, rechazado, de baja, no existe, error) **[verificar]** contra el manual de consulta |
| `estados-ticket.json` | Estados del ticket de `getStatus` (0 procesado, 98 en proceso, 99 con errores) para el resumen diario y la baja |
| `mensajes-usuario.json` | `MensajeUsuario` para el cajero: por estado y, con prioridad, por código (1033, 0140, 0102–0104, 0111, `PCPE-4xxx`…) |
| `ajustes-xsl.json` | Ajustes entre las XSL (06/09/2022) y el Excel vigente: códigos de la XSL que se suprimen y la regla C# que los reemplaza, y la clasificación de cada fila de 01/03 posterior a la XSL (§9.4). No se sobrescribe por configuración |
| `xsl-compatibilidad.json` | Variables que las XSL usan sin declarar y su valor de respaldo (§9.3). No se sobrescribe por configuración |
| `xsl-utils\` | `validate_utils.xsl`, `error_utils.xsl` y `dat_fechas.xml` propios, que reemplazan a los de DataPower (§9.3) |

Valores iniciales de `tasas.json`:

```json
{
  "igv": [
    { "desde": "2011-03-01", "tasa": 18.00, "igv": 16.00, "ipm": 2.00 },
    { "desde": "2026-01-01", "tasa": 18.00, "igv": 15.50, "ipm": 2.50 },
    { "desde": "2027-01-01", "tasa": 18.00, "igv": 15.00, "ipm": 3.00 },
    { "desde": "2028-01-01", "tasa": 18.00, "igv": 14.50, "ipm": 3.50 },
    { "desde": "2029-01-01", "tasa": 18.00, "igv": 14.00, "ipm": 4.00 }
  ],
  "mypeRestaurantesHoteles": [
    { "desde": "2025-01-01", "tasa": 10.00 },
    { "desde": "2026-01-01", "tasa": 10.50 },
    { "desde": "2027-01-01", "hasta": "2027-12-31", "tasa": 15.00 }
  ],
  "icbper": [ { "desde": "2023-01-01", "montoPorUnidad": 0.50 } ]
}
```

Valores iniciales de `plazos.json` (días calendario contados desde el día siguiente):

| Documento | Plazo |
|---|---|
| Factura y notas vinculadas | 3 desde la emisión |
| Boletas y notas vinculadas (resumen diario) | 7 desde la emisión |
| Comunicación de baja | 7 desde la recepción del CDR del documento |

## 4. Modelo público de entrada

```csharp
public sealed record EmisorCpe
{
    public required string Ruc { get; init; }
    public required string RazonSocial { get; init; }
    public string? NombreComercial { get; init; }
    public required DireccionCpe Domicilio { get; init; }           // ubigeo, dirección, urbanización…
    public string CodigoEstablecimiento { get; init; } = "0000";
    public RegimenTributario Regimen { get; init; } = RegimenTributario.General; // NRUS, RER, RMT, General
    public bool? ExoneradoAmazonia { get; init; }                    // null → OpcionesPasheCpe.ExoneradoAmazoniaPorDefecto (true)
    public bool TasaEspecialRestaurantesHoteles { get; init; }
    public required CanalEnvio Canal { get; init; }                  // Sunat | Ose(nombre)
    public CredencialesSoap? CredencialesSoap { get; init; }         // usuario SOL o usuario OSE + clave
    public CredencialesGre? CredencialesGre { get; init; }           // client_id, client_secret, usuario y clave SOL
    public required CertificadoCpe Certificado { get; init; }        // archivo .pfx en CERT\ + clave
}
```

- `ComprobanteCpe` (01/03): tipo, serie, número, fecha y hora de emisión (null → ahora en Lima), vencimiento, moneda, tipo de operación (Cat.51), cliente, líneas, forma de pago (contado/crédito + cuotas), descuentos/cargos globales, anticipos, detracción, documentos relacionados, orden de compra, contrato de colaboración, leyendas adicionales, observaciones, `ModoPrecio` (`ValorUnitario` sin impuestos | `PrecioUnitario` con impuestos).
- `LineaCpe`: código interno, código de producto SUNAT (8 dígitos), GTIN, descripción, unidad (Cat.03), cantidad, valor o precio unitario, afectación (Cat.07, null → default del emisor), descuento, ICBPER (cantidad de bolsas), indicador de gratuito, ISC opcional.
- `NotaCpe`: datos comunes de `DocumentoVentaCpe` (sin anticipos ni detracción) + tipo (07/08), motivo (Cat.09/10), sustento, **un** documento modificado (`DocumentoModificadoCpe`: tipo 01 o 03, serie, número y, opcional, `FechaEmision`). El documento modificado es obligatorio salvo en la ND por penalidades (motivo 13, 2524). Con `FechaEmision` se valida que la nota no sea anterior al original (2885) y, si el original lo generó PasheCPE, su estado, moneda e importe total en el historial (§9.4). Las notas no llevan descuentos ni cargos (PCPE-1414/1415); la forma de pago solo va en la NC por ajuste de cuotas (motivo 13), donde el monto neto pendiente es la suma de las cuotas (3319).
- `ResumenDiarioCpe`: fecha de referencia + líneas (boleta/nota, estado 1 adicionar / 2 modificar / 3 anular, cliente con razón social, totales por afectación con gratuitas desagregadas en gravadas/exoneradas/inafectas/exportación, IGV de gratuitas y tasa del IGV — campos 2027; sin tasa, la librería usa la vigente a la fecha de emisión, o la MYPE si el emisor la aplica). La librería asigna correlativos y divide en lotes de 500.
- `DocumentoResumenCpe` (Fase 6, decidido con el desarrollador el 23/10/2026): tipo, serie, número y estado de una boleta o nota **ya generada por PasheCPE**; con esta vía la librería arma la línea leyendo su XML firmado de `FIRMA` (lo informado coincide con lo firmado).
- `ComunicacionBajaCpe`: fecha de referencia + documentos (tipo, serie, número, motivo).
- `GuiaRemisionCpe` (09/31) — **se define en la Fase 10** a partir del Excel GRE; campos previstos: remitente/transportista, destinatario, motivo (Cat.20) y descripción, modalidad (Cat.18: 01 público / 02 privado), fecha de inicio de traslado, peso bruto y unidad, bultos, partida y llegada (ubigeo, dirección, código de establecimiento), vehículos (placa, autorización), conductores (documento, nombres, licencia), transportista (RUC, razón social, MTC), indicador M1/L, documentos relacionados (Cat.61), bienes (código, descripción, unidad, cantidad, bien normalizado Cat.62).
- `ReferenciaCpe`: `(Ruc, TipoDocumento, Serie, Numero, Fecha)` o `(Ruc, Tipo RC/RA, Fecha, Correlativo)`; resuelve nombre de archivo y periodo.

Todos son `record` inmutables, serializables con `System.Text.Json` mediante el contexto público generado `PasheCpeJson` (`JsonSerializerContext`), que incluye modelos, resultados e historial. `EmisorCpe` queda fuera del contexto porque contiene credenciales; sus claves llevan `[JsonIgnore]` y el `ToString()` de los records de credenciales las enmascara.

Los códigos de catálogo son `string` (p. ej. `Afectacion = "20"`), con constantes generadas desde el Excel (`AfectacionIgv.ExoneradoOneroso`, `TipoOperacion.VentaInterna`, `MotivoNotaDebito.Penalidades`…). `CatalogosSunat.Validar` rechaza los códigos inexistentes o no vigentes a la fecha de emisión e indica el número de catálogo (`ErrorCpe.Catalogo`); los mensajes de la validación local son datos en `errores-locales.json` (códigos `PCPE-xxxx`).

## 5. Resultados y estados

```csharp
public enum EstadoCpe
{
    Generado,                   // generado y firmado, aún no enviado
    ErrorValidacionLocal,       // nivel 1
    SinConexionInternet,        // nivel 0
    ServicioNoDisponible,       // nivel 0 (caída, 5xx, timeout de conexión, fault transitorio)
    ErrorServicio,              // nivel 2 (fault/HTTP no transitorio)
    RespuestaIncierta,          // nivel 2 (enviado sin respuesta) → reconciliar
    EnProceso,                  // ticket recibido / 98
    Aceptado,                   // nivel 3
    AceptadoConObservaciones,   // nivel 3
    Rechazado                   // nivel 3
}

public sealed record ResultadoEnvio
{
    public EstadoCpe Estado { get; init; }
    public int Nivel { get; init; }                               // 0..3
    public string? Codigo { get; init; }                          // "0", "2324", "0111", "98"…
    public string? Descripcion { get; init; }                     // texto literal SUNAT/OSE
    public string? DescripcionCatalogo { get; init; }             // errores-sunat.json
    public string MensajeUsuario { get; init; } = "";             // español claro para el cajero
    public IReadOnlyList<ObservacionCpe> Observaciones { get; init; } = [];
    public IReadOnlyList<ErrorCpe> Errores { get; init; } = [];   // validación local o ticket 99
    public string? Ticket { get; init; }
    public string? ValorResumen { get; init; }
    public string? ContenidoQr { get; init; }
    public RutasCpe Rutas { get; init; } = RutasCpe.Vacio;
    public bool EsReintentable { get; init; }
    public bool RequiereConsultaEstado { get; init; }
    public DateOnly? FechaLimiteEnvio { get; init; }
    public DetalleTecnico? Tecnico { get; init; }                 // endpoint, operación, HTTP, fault, duración, excepción resumida
    public DateTimeOffset FechaProceso { get; init; }             // hora Lima
    public IReadOnlyList<IntentoEnvio> Historial { get; init; } = [];
}
```

`ResultadoGeneracion` (referencia, nombre, valor resumen, contenido QR, rutas, errores, advertencias) y `ResultadoEmision { Generacion, Envio }` siguen el mismo patrón. Si la generación falla, `Envio.Estado = ErrorValidacionLocal` y no se intenta el envío.

### 5.1 Tabla de clasificación (única fuente: `ClasificadorRespuesta`)

| Situación | Estado | Reintentable | Consulta estado |
|---|---|---|---|
| Sin interfaz de red / sin Internet | `SinConexionInternet` | sí | no |
| DNS o TCP del destino falla con Internet disponible | `ServicioNoDisponible` | sí | no |
| HTTP 502/503/504, timeout de **conexión**, error TLS | `ServicioNoDisponible` | sí | no |
| Fault con código `transitorio = true` (falla interna de SUNAT, p. ej. 0100 "El sistema no puede responder su solicitud") | `ServicioNoDisponible` con `EsReintentable = true`; los reintentos dentro de la llamada dependen de la operación (§14): `sendBill` nunca, `sendSummary` solo faults de disponibilidad, `getStatus`/`getStatusCdr`/token GRE hasta 3. Nunca reintentos infinitos; el reenvío lo decide el sistema de ventas | sí | según el último fault (§14) |
| Fault con código `enProceso = true` (0140 "Existe un documento igual en proceso") | `EnProceso` con `RequiereConsultaEstado = true`: **nunca se reenvía**; se consulta el CDR después de una espera | — | **sí** |
| Fault que apunta al archivo enviado (0250 error al descomprimir, 0252 ZIP sin archivos…) | `ErrorServicio`: reenviar el mismo archivo no lo arregla | no | no |
| Fault 0100–1999 no transitorio (credenciales, perfil, nombre/estructura de archivo) | `ErrorServicio` | no | no |
| Fault 2000–3999 (SUNAT devuelve así los rechazos de `sendBill`, sin CDR) | `Rechazado` con el código en `Errores` | no | no |
| Fault 1033 (registrado previamente) | — | — | **sí** → `getStatusCdr` y se devuelve el estado real; donde no se puede consultar (beta, boletas, series B): `ErrorServicio` con `RequiereConsultaEstado` («verifique en SOL») |
| Timeout de **lectura** tras enviar, conexión cortada a mitad | `RespuestaIncierta` | no (reconciliar) | **sí** |
| HTTP 401 GRE | renovar token una vez; si persiste `ErrorServicio` | — | — |
| HTTP 422 GRE (`errors[]`) | `ErrorServicio` con la lista | no | no |
| CDR 0 sin notas | `Aceptado` | — | — |
| CDR 0 con notas ≥ 4000 | `AceptadoConObservaciones` | — | — |
| CDR 2000–3999 | `Rechazado` (permite reemitir corregido) | no | no |
| `sendSummary` / GRE envío correcto, ticket 98 | `EnProceso` | — | sí |
| Ticket 0 | según CDR | — | — |
| Ticket 99 con CDR | `Rechazado` con errores del CDR | no | no |
| Ticket 99 sin CDR (SOAP) | `Rechazado` con `PCPE-4013`: el resumen no se registró y sus documentos se informan en otro | no | no |
| Ticket 0 sin CDR o estado desconocido (`estados-ticket.json`) | `ErrorServicio` (`PCPE-4013`/`PCPE-4012`); el estado del resumen no cambia | no | sí |
| Fault 0127 (el ticket no existe) | `ErrorServicio`; el siguiente `EnviarAsync` reenvía el mismo ZIP | no | no |
| Ticket 99 sin CDR (GRE `error.numError/desError`) | `Rechazado` o `ErrorServicio` según código | no | no |

Regla dura: **ningún** `sendBill` se reenvía automáticamente; `RespuestaIncierta` y 1033 siempre pasan por reconciliación.

## 6. API pública

```csharp
public interface IServicioCpe   // 01, 03, 07, 08, RC, RA
{
    Task<ResultadoGeneracion> GenerarAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct = default);
    Task<ResultadoGeneracion> GenerarAsync(EmisorCpe emisor, NotaCpe nota, CancellationToken ct = default);
    Task<IReadOnlyList<ResultadoGeneracion>> GenerarResumenAsync(EmisorCpe emisor, ResumenDiarioCpe resumen, CancellationToken ct = default);
    Task<IReadOnlyList<ResultadoGeneracion>> GenerarResumenAsync(EmisorCpe emisor, DateOnly fechaReferencia, IReadOnlyList<DocumentoResumenCpe> documentos, CancellationToken ct = default); // desde FIRMA
    Task<ResultadoGeneracion> GenerarBajaAsync(EmisorCpe emisor, ComunicacionBajaCpe baja, CancellationToken ct = default);

    Task<ResultadoEnvio> EnviarAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct = default);   // sendBill o sendSummary según tipo
    Task<ResultadoEmision> EmitirAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct = default);       // generar + enviar
    Task<ResultadoEmision> EmitirAsync(EmisorCpe emisor, NotaCpe nota, CancellationToken ct = default);

    Task<ResultadoEnvio> ConsultarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string? ticket = null, CancellationToken ct = default); // null → ticket del historial
    Task<ResultadoEnvio> EsperarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string? ticket = null, CancellationToken ct = default); // con espera progresiva
    Task<ResultadoEnvio> ConsultarCdrAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct = default);   // getStatusCdr

    Task<ResultadoValidacion> ValidarAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct = default);       // solo nivel 1
    Task<ResultadoValidacion> ValidarAsync(EmisorCpe emisor, NotaCpe nota, CancellationToken ct = default);             // nivel 1 + documento modificado
}

public interface IServicioGre   // 09, 31
{
    Task<ResultadoGeneracion> GenerarAsync(EmisorCpe emisor, GuiaRemisionCpe guia, CancellationToken ct = default);
    Task<ResultadoEnvio> EnviarAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct = default);   // → ticket
    Task<ResultadoEnvio> ConsultarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string ticket, CancellationToken ct = default);
    Task<ResultadoEmision> EmitirAsync(EmisorCpe emisor, GuiaRemisionCpe guia, CancellationToken ct = default);     // genera, envía y espera ticket
}

public interface IVerificadorConectividad { Task<EstadoConectividad> VerificarAsync(CanalEnvio canal, CancellationToken ct = default); }
public interface IHistorialCpe
{
    Task<HistorialCpe?> ObtenerAsync(ReferenciaCpe referencia, CancellationToken ct = default);
    IAsyncEnumerable<HistorialCpe> ListarPendientesAsync(int anio, int mes, CancellationToken ct = default); // EnProceso, RespuestaIncierta, ServicioNoDisponible…
}
public interface IGeneradorPdf  // PasheCPE.Pdf
{
    Task<ResultadoPdf> GenerarAsync(ReferenciaCpe referencia, MarcaEmisor marca, DatosAdicionalesPdf? extra = null, CancellationToken ct = default);
}
public interface IPrecalentador { Task PrecalentarAsync(CancellationToken ct = default); } // compila XSL, carga esquemas y serializadores
```

`IPrecalentador` compila en paralelo las 7 XSL, el conjunto de XSD y los serializadores (sin precalentar, la primera validación tarda ≈ 1 s). Sin dependencias nuevas (no se usa `Microsoft.Extensions.Hosting`): `PasheCpe.Crear` lo lanza en segundo plano si `PrecalentarAlIniciar` (una falla solo se registra); con `AddPasheCpe`, el sistema lo llama al iniciar: `_ = app.Services.GetRequiredService<IPrecalentador>().PrecalentarAsync();`.

Pipeline de `EmitirAsync`: **validar modelo → calcular → mapear UBL → serializar (DATA) → validar XSD + XSL + reglas complementarias → firmar (FIRMA, valor resumen) → ZIP (ENVIO) → QR (QR) → verificar conectividad → enviar → clasificar → CDR (RPTA) → historial (LOG) → resultado**. Cada etapa persiste su archivo y el resultado devuelve todas las rutas. `EnviarAsync` parte de ENVIO\ y nunca regenera.

`IServicioCpe` crece por fases (decidido con el desarrollador el 23/10/2026): en la Fase 4 solo tiene `GenerarAsync(emisor, ComprobanteCpe)` y `ValidarAsync`; la Fase 5 agrega `EnviarAsync`, `EmitirAsync(emisor, ComprobanteCpe)` y `ConsultarCdrAsync`; la Fase 6, `GenerarResumenAsync` (con totales o desde FIRMA), `ConsultarTicketAsync` y `EsperarTicketAsync`, y `EnviarAsync` envía el RC con `sendSummary`; la Fase 7, `GenerarAsync`, `EmitirAsync` y `ValidarAsync` de `NotaCpe` (las notas F van por `sendBill`; las B siguen `ModoEnvioBoletas`); los demás métodos se agregan en la fase que los implementa (8), sin stubs. `GenerarAsync` no regenera un documento cuyo historial tenga un estado distinto de `Generado` o `ErrorValidacionLocal` (`PCPE-3101`): cambiaría el valor resumen de lo enviado a SUNAT.

## 7. Cálculo tributario

- Toda la aritmética con `decimal`; redondeo `MidpointRounding.AwayFromZero` a 2 decimales en montos; valores unitarios hasta 10 decimales.
- `ModoPrecio.PrecioUnitario` — **manda el precio**: total de línea = R2(cantidad × precio); base = R2(total ÷ (1 + tasa)); IGV = total − base. El cliente paga exactamente lo de la etiqueta y el IGV puede diferir ±0.01 de base × tasa (tolerancia SUNAT ±1). Con descuento de línea: base = bruto − descuento e IGV = R2(base × tasa). El valor unitario del XML (`cac:Price`) = base bruta ÷ cantidad, con hasta 10 decimales; el precio de referencia (tipo 01) es el precio de la etiqueta, salvo con descuento de línea: entonces es (valor de venta + tributos de la línea) ÷ cantidad, que es lo que valida SUNAT (3270). Con tasa 0 ambos modos dan lo mismo.
- ISC: el `cbc:Percent` del subtotal 2000 es obligatorio también en el sistema de monto fijo (2992); se informa la tasa equivalente ISC ÷ base × 100 (5 decimales), que cumple ISC = base × tasa (3108).
- ICBPER: la cantidad de bolsas debe ser la cantidad de la línea (3236); las bolsas se registran en una línea propia (`PCPE-1219`).
- `ModoPrecio.ValorUnitario`: base = R2(cantidad × valor) − descuento; IGV = R2((base + ISC) × tasa).
- El tributo de cada afectación sale de la columna «Codigo de tributo» del catálogo N.° 07 (gratuita = 9996), y el nombre y el código internacional del `TaxScheme`, del catálogo N.° 05. El texto de las leyendas es el que va entre comillas en el catálogo N.° 52.
- Totales según las reglas del Excel (3277, 3278, 3279, 3280, 3291, 3294): la base gravada (subtotal 1000) = Σ bases − descuentos 02 − anticipos 04 + cargos 49; el total valor de venta y el precio de venta no restan los anticipos; el importe a pagar = precio de venta + cargos 50 − descuentos 03 − total de anticipos. El IGV de las gratuitas va en el subtotal 9996 y no suma a `TaxTotal/TaxAmount`.
- ISC: sistemas 01 (al valor) y 02 (monto fijo); el 03 se rechaza en la validación local. IVAP (afectación 17), percepción y retención: fuera del alcance actual.
- Afectación por línea (Cat.07) → tributo (Cat.05):

| Afectación | Tributo | Código | Nombre | Tipo | Tasa |
|---|---|---|---|---|---|
| 10 Gravado oneroso | 1000 | IGV | VAT | S | `tasas.json` (18.00 o tasa MYPE) |
| 11–17 Gravado gratuito | 9996 | GRA | FRE | Z | referencial (`PriceTypeCode 02`) |
| 20 Exonerado oneroso | 9997 | EXO | VAT | E | 0 |
| 21 Exonerado gratuito | 9996 | GRA | FRE | Z | 0 |
| 30 Inafecto oneroso | 9998 | INA | FRE | O | 0 |
| 31–37 Inafecto gratuito | 9996 | GRA | FRE | Z | 0 |
| 40 Exportación | 9995 | EXP | FRE | G | 0 |

- Default por emisor: `ExoneradoAmazonia` → 20 y leyenda Cat.52 (2001 bienes, 2002 servicios, 2003 construcción, según `LineaCpe.TipoItem`); si no → 10. `TasaEspecialRestaurantesHoteles` → la afectación 10 usa la tasa MYPE vigente en la fecha de emisión.
- `NRUS`: solo tipos 03, 07/08 de boletas y GRE; factura → error local.
- ICBPER: tributo 7152 (ICBPER/OTH), monto = bolsas × tarifa vigente; no integra la base del IGV.
- Leyenda 1000 (monto en letras) con `NumeroALetras` propio ("CIENTO VEINTE CON 50/100 SOLES"), **sin tildes por defecto** (DIECISEIS, VEINTIDOS, MILLON, DOLARES AMERICANOS) porque los tickets se imprimen en impresoras térmicas ESC/POS; `MontoEnLetras.ConTildes = true` las conserva (PDF u otros usos). «UNO»/«VEINTIUNO» al final de la cifra y «UN»/«VEINTIUN» delante de MIL o MILLON(ES). Importe cero: «CERO CON 00/100 SOLES»; si todo es gratuito va además la leyenda 1002.
- Cuadre obligatorio antes de serializar: suma de líneas = `LineExtensionAmount`; `TaxInclusiveAmount`; descuentos/cargos/anticipos; `PayableAmount`.
- Forma de pago: `cac:PaymentTerms` `FormaPago` Contado, o Crédito + monto pendiente + `Cuota001…` con monto y vencimiento; detracción con `PaymentMeans` (cuenta Banco de la Nación) y `PaymentTerms` (código Cat.54, porcentaje, monto).

## 8. Serialización

- `XmlSerializerNamespaces` explícitos: default del documento, `cac`, `cbc` y `ext`; `ds` lo agrega la firma y `sac` solo los documentos que lo usan (Summary/Voided); sin `xsi`/`xsd` espurios.
- `XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, NewLineChars = "\n" }`.
- `InvariantCulture`; fechas `yyyy-MM-dd`, horas `HH:mm:ss`; atributos de catálogo (`listAgencyName="PE:SUNAT"`, `listName`, `listURI`, `unitCodeListID="UN/ECE rec 20"`…) según la guía de elaboración vigente.
- Opcionales vacíos se omiten (`ShouldSerialize*`).
- `UBLExtensions/UBLExtension/ExtensionContent` vacío preparado para la firma.
- Versiones: Invoice/CreditNote/DebitNote/DespatchAdvice `UBLVersionID 2.1`, `CustomizationID 2.0`; SummaryDocuments `2.0`/`1.1`; VoidedDocuments `2.0`/`1.0`.

## 9. Validación local (nivel 1)

### 9.1 Modelo (C#)
Obligatorios, serie vs tipo (F/B/T/V…), tipo de documento del cliente (factura → 6; boleta > S/ 700 → identificación), fechas, plazos (`FechaLimiteEnvio` vencida → error), NRUS, longitud de campos, código de producto 8 dígitos (00000000 y 99999999 se aceptan, como excepción del Excel, 3496), una sola referencia en notas. Notas (`PCPE-1401`–`1415`): motivo vigente (Cat.09/10), sustento de 1 a 500 caracteres, documento modificado 01 o 03 con su serie, serie de la nota coherente (F → 01, B → 03), plazo según el documento modificado (3 o 7 días; en el envío se deduce de la serie), NC 13 solo sobre factura, al crédito con cuotas y con importe cero, ND 13 sin IGV. Ante diferencias con el Excel de validación, prevalece el Excel (§9.4).

### 9.2 Esquema
`XmlSchemaSet` compilado una vez desde `recursos\XSD` (embebidos, enlazados sin copiar), con resolutor local para los `import` (ninguna URI externa se resuelve). El resumen diario (UBL 2.0) usa un **segundo conjunto** con `UBLPE-SummaryDocuments-1.0.xsd` y sus `import` de `recursos\XSD\2.0\common` (Fase 6): UBL 2.0 y 2.1 comparten los namespaces `cac`/`cbc` y no pueden compilarse juntos, así que se elige por el elemento raíz. Solo se carga el documento principal porque `UBLPE-SunatAggregateComponents` 1.0 y 1.1 comparten el namespace (el XSD de SUNAT importa el 1.0, que ya trae cliente, referencia y estado en las líneas). Los 2 RC dorados lo pasan. El xmldsig de UBL trae un DOCTYPE con entidades internas: se lee con `DtdProcessing.Parse` y se carga primero. Se valida el XML **sin firmar** (la firma no altera la estructura validada); como `ext:ExtensionContent` exige un elemento, el `ext:UBLExtensions` vacío preparado para la firma se excluye de la validación (única exclusión). Los errores se devuelven como `PCPE-2001` con la ruta del elemento.

### 9.3 Reglas SUNAT — XSL con emulación DataPower

Archivo por documento:

| Documento | XSL |
|---|---|
| 01 | `ValidaExprRegFactura-2.0.1.xsl` |
| 03 | `ValidaExprRegBoleta-2.0.1.xsl` |
| 07 | `ValidaExprRegNC-2.0.1.xsl` |
| 08 | `ValidaExprRegND-2.0.1.xsl` |
| RC | `ValidaExprRegSummary-1.1.0.xsl` |
| RA | `ValidaExprRegSummaryVoided-1.0.1.xsl` |
| 09 | `ValidaExprRegGuiaRemitente-2.0.1.xsl`: **se compila pero no se usa**. Es de la GRE 1.0 (exige `CustomizationID` 1.0) y da errores falsos con las GRE 2.0 aceptadas; en la Fase 10 se revisa si SUNAT publicó una XSL de la GRE 2.0 y se evalúa con los dorados antes de decidir |
| 31 | sin XSL oficial → XSD + reglas C# |

Las XSL de SUNAT son XSLT 1.0 escritas para IBM DataPower. Lo que se encontró en los archivos (inventario automático en `InventarioXslTests`) y cómo se resuelve con `XslCompiledTransform`:

| Dependencia | Uso en las XSL | Solución |
|---|---|---|
| `xsl:include href="local:///commons/error/validate_utils.xsl"` y `error_utils.xsl` | 16 plantillas: `isTrueExpresion` (888 llamadas), `regexpValidateElementIfExist` (689), `existElement` (337), `existAndRegexpValidateElement` (127), `findElementInCatalog` (122), `existAndValidateValueTwoDecimal` (54), `validateValueTwoDecimalIfExist` (40), `findElementInCatalogProperty` (28), `existElementNoVacio` (26), `rejectCall` (19), `existAndValidateValueTenDecimal` (15), `isTrueExpresionIfExist` (14), `addWarning` (6), `isDateBefore`, `isDateAfterToday` y `validateValueThreeDecimalIfExist`. **No vienen en la descarga de SUNAT.** | Implementación propia en `Recursos\xsl-utils\validate_utils.xsl` con las mismas plantillas y parámetros (`isError` y `isGreaterCero` valen `true()` por defecto). En vez de `dp:reject` (que detiene el proceso), **emiten** `<pcpe:resultado codigo tipo ruta detalle/>`: se reportan todos los errores y observaciones de una pasada, en orden de ejecución (el primero es el que devolvería SUNAT). Un recolector (`XmlWriter`) conserva solo esos nodos y descarta la copia del documento. Un código de catálogo vacío no se reporta (SUNAT acepta sin 4332 las líneas sin código de producto). `error_utils.xsl` queda vacío: DataPower evita la doble inclusión con `dp:ignore-multiple`. |
| `XmlResolver` para `local:///` | includes y `document('local:///commons/cpe/datos/dat_fechas.xml')` | `ResolutorRecursosLocales : XmlResolver` que mapea las 3 rutas a recursos embebidos y rechaza cualquier otra URI. `dat_fechas.xml` propio: `factoring` = 20210401 (forma de pago obligatoria desde el 01/04/2021). |
| `dp:variable('var://context/cpe/nombreArchivoEnviado')` (31 usos, **única clave**) | RUC, tipo, serie y número con `substring` del nombre del archivo | `ExtensionesDataPower.variable(string)` devuelve `NombreArchivoCpe.Xml(referencia)`; una clave desconocida produce `PCPE-2101`. |
| `dp:ignore-multiple="yes"`, `extension-element-prefixes="dp"` | Atributo y declaración | Compatibles con XSLT 1.0. |
| EXSLT `date:date()` (factoring), `date:seconds()`, `regexp:match()` (GRE); `regexp:test` en las utilidades | Funciones | `ExtensionesExsltFechas` (hora de Lima con `RelojCpe`) y `ExtensionesExsltRegexp` (`Regex` cacheado, `CultureInvariant`, 1 s de tiempo límite). Las 178 expresiones regulares distintas de las XSL compilan en .NET sin cambios. Los nombres de método conservan el de la función XPath (`XslCompiledTransform` distingue mayúsculas). |
| Catálogos (25 distintos) | `findElementInCatalog(Property)` | `ExtensionesPasheCpe.enCatalogo` con `CatalogosSunat` a la **fecha de recepción** (hoy en Lima, como DataPower). El catálogo N.° 02 se valida contra `monedas.json`; 03, 04, 13 y 25, por formato. `propiedadCatalogo`: `factura`/`boleta` → «Tipo de Comprobante asociado» (Cat. 51); `name` → «Nombre» y `UN_ECE_5153` → «Código internacional» (Cat. 05); `igv`, `iva`, `exp`, `gra`, `exo`, `ina`, `oth` → «Codigo de tributo» (Cat. 07); `tasa` → «Porcentaje %» (Cat. 22). Una propiedad desconocida produce `PCPE-2102`. |

**Compatibilidad (`AdaptadorXslDataPower`).** 4 de las 7 XSL no compilan tal cual con `XslCompiledTransform` (DataPower es más permisivo). Se corrigen en memoria al cargar, sin cambiar la semántica; los conteos por XSL quedan fijados en una prueba:

| Corrección | Dónde |
|---|---|
| `concat('…')` con un solo argumento → el literal | Factura (2), Boleta (2) |
| Variable local declarada otra vez en su ámbito → se renombra la nueva declaración y sus referencias posteriores (equivale a sombrearla) | Factura y Boleta (`totalDescuentosGlobales`), ND (`totalImpuestos`, `SumatoriaImpuestos`, `valorVentaLinea`) |
| Variables usadas sin declarar → variable global de respaldo (`xsl-compatibilidad.json`, con su motivo) | Boleta y ND (`nroLinea` = ''), NC y ND (`MontoDescuentoAfectoBI` = 0, cuya definición SUNAT dejó comentada; revisado en la Fase 7: es el valor exacto porque las notas no llevan descuentos globales) |

- Cada XSL se compila **una sola vez** (perezoso, `Lazy<T>` thread-safe) con `XsltSettings(enableDocumentFunction: true, enableScript: false)`; `IPrecalentador` puede compilarlas al iniciar. Cada validación usa un `XsltArgumentList` nuevo con sus objetos de extensión.
- Las XSL originales no se modifican; se embeben tal cual, enlazadas desde `recursos\XSL`.
- Se valida el XML **sin firmar**. Las XSL no exigen la firma en los dorados ni en los XML generados.
- `MotorXslSunat` nunca lanza: una falla al compilar o ejecutar devuelve `PCPE-2101` o `PCPE-2102`.
- Resultados de la Fase 3: las 2 facturas, las 2 NC y los 2 RC dorados dan 0 hallazgos; los XML generados de todos los casos tributarios, también; en caliente una factura se valida en pocos milisegundos (meta < 300 ms).

### 9.4 Reglas complementarias (C#) y ajustes de la XSL

> **Regla de precedencia (PRD RF-07): el Excel de validación prevalece sobre las XSL.** Ante cualquier incongruencia o duda entre una XSL y el Excel de reglas de validación (`recursos\VALIDACIONES`, el más actualizado), se implementa lo que dice el Excel: el código de la XSL se suprime en `ajustes-xsl.json` y lo reemplaza una regla C# conforme al Excel, con el motivo, la fila del Excel y una prueba. Lo mismo vale para `ValidadorModelo` y para el XML que se genera: si son más estrictos o distintos que el Excel, se alinean con el Excel. Solo los dorados aceptados por SUNAT (CDR reales) pueden matizar una fila del Excel, y eso se documenta en el motivo.

Las XSL son del 06/09/2022 y el Excel de reglas, del 26/08/2026: hay filas posteriores que la XSL no tiene o calcula distinto (por ejemplo, la sumatoria del IGV con la tasa MYPE de 10.5 %). Se cubren con:

- **`IReglaCpe`** (`Validacion\Reglas\`): cada regla evalúa el **XML** (no el modelo, así sirve para cualquier XML) e informa la fila del Excel que no se cumple. El **código y el tipo salen de `reglas-cpe.json`**: error u observación según la vigencia a la fecha de recepción (hoy en Lima); antes de una vigencia «OBSERV → ERROR» se aplica `tipoAnterior`, y una regla sin tipo anterior no aplica antes de su vigencia. Origen `OrigenError.ReglasComplementarias`; se activan con `Validacion.ReglasComplementarias` (la vigencia de cada regla sigue en los datos, `reglas-cpe.json`; desactivarlas también anula las supresiones de `ajustes-xsl.json`).
- **`ajustes-xsl.json`**: `suprimidos` (código de la XSL que se descarta, la regla C# que lo reemplaza, las filas del Excel que lo respaldan, el motivo y, opcional, `desde`: la vigencia de esas filas, antes de la cual sigue aplicando la XSL; solo se suprime si las reglas complementarias están activas) y `clasificacion` (cada fila de 01/03/07/08/RC con vigencia posterior al 06/09/2022: `reemplazada`, `nueva`, `xsl`, `modelo` o `noVerificable`, con el motivo). Una prueba falla si alguna fila queda sin clasificar, si una regla no respalda sus filas o si un código suprimido ya no está activo en la XSL. Con cada XSL nueva de SUNAT el archivo se revisa completo.

Reglas implementadas para 01/03 (Fase 3):

| Regla | Filas del Excel | Motivo |
|---|---|---|
| `SumatoriaIgv` (3291 factura; 4290 boleta) | 01: 351; 03: 346, 349 | Σ base de las líneas 1000 − descuentos 02/04 + cargos 49 − anticipos ISC 20, por la tasa de las líneas (18 % o 10.5 %), ±1. La XSL de factura la tiene comentada; la de boleta solo la valida con 0113 y 0.18 fijo → **4290 suprimida** |
| `TasaIgvUnica` (3462) | 01: 352; 03: 347 | Todas las líneas afectas al IGV con la misma tasa |
| `TotalPrecioVenta` (3279 factura; 4310 boleta) | 01: 420; 03: 416 | Rama sin IVAP del `TaxInclusiveAmount` con la tasa de las líneas (la XSL solo valida la del IVAP) |
| `CodigoProducto` (3496) | 01/03: 155 | Formato de 8 dígitos: observación hasta el 31/12/2026, error desde el 01/01/2027 |
| `ContratoColaboracion` (3497–3502) | 01: 916–921, 923–928; 03: 822–827, 829–834 | Desde el 01/01/2027 |

Reglas implementadas para el RC (Fase 6; todas desde el 01/01/2027, con las filas posteriores a la XSL clasificadas en `ajustes-xsl.json`):

| Regla | Filas del Excel (RC) | Motivo |
|---|---|---|
| `TasaIgvResumen` (2992, 3504, 3102) | 122–125, 162–164 | Tasa obligatoria en 1000 y 9996: 10.5 o 18 en boletas; 10, 10.5 o 18 en notas; hasta 3 enteros y 5 decimales |
| `IgvResumen` (4019, observación) | 118, 119, 158, 159 | IGV = (gravado + ISC) × tasa e IGV de gratuitas = gratuitas gravadas (06) × tasa, ±5, con alguna de las tasas vigentes |
| `RazonSocialAdquiriente` (2022) | 48 | Razón social del adquiriente de 3 a 250 caracteres, sin saltos de línea |
| `NombreTributoGratuito` (3051) | 169 | Nombre del tributo 9996 = «GRA» (la XSL solo valida 3051 para el IVAP) |

El resto de filas 2027 del RC (2048, 2344, 2268, 2269, 2271, 2355) ya las cumple la XSL para todos los tributos; la 4443 (plazo) la informa `ValidadorResumen` como advertencia (`PCPE-1318`). **[verificar]** 2048 desde 2027: el Excel dice «diferente de cero» para el total del IGV, pero el dorado aceptado informa IGV 0.00 en una boleta exonerada; se mantiene así hasta confirmarlo con SUNAT en enero de 2027.

Sin regla C#: 2108/1079 (plazos, `ValidadorModelo`), 2369, 3461, 4332 y 4337 (ya en la XSL), 1086, 2529, 4439 y 3496 fila 156 (requieren padrones o listados de SUNAT).

Reglas implementadas para NC/ND (Fase 7, R.S. 000048-2026; las 125 filas de 07/08 posteriores a la XSL están clasificadas en `ajustes-xsl.json`):

| Regla | Filas del Excel | Motivo |
|---|---|---|
| `ReferenciaUnica` (3261 NC; 3194 ND) | 07: 90; 08: 90 | Un solo `InvoiceDocumentReference` desde el 01/01/2027 (en la NC, salvo el motivo 11, que la XSL valida con 3194). La XSL de NC solo lo valida en el motivo 13 → **3261 suprimida desde el 01/01/2027** |
| `ReferenciaNotaDebito` (2524) | 08: 89 | Documento modificado obligatorio salvo en las penalidades (motivo 13). La XSL lo exige salvo en el motivo 03 → **2524 de ND suprimida desde el 01/01/2027** |
| `PenalidadesSinIgv` (3507) | 08: 194, 283, 325 | ND 13 inafecta del IGV/IVAP (línea, 9995/9997 y sumatoria) |
| `SumatoriaIgv` (3291 si modifica factura; 4290 en otro caso) | 07: 341, 343; 08: 328, 330 | Igual que en factura; las XSL de NC/ND la tienen comentada |
| `TasaIgvUnica` (3462) | 07: 342, 344; 08: 329, 331 | Igual que en factura |
| `CodigoProducto` (3496) | 07: 169; 08: 147 | Desde el 01/01/2027 |
| `ContratoColaboracion` (3497–3502) | 07: 478–483, 485–490; 08: 458–463, 465–470 | Desde el 01/01/2027 |

El formato del documento modificado (2117, 2205) y su tipo según la serie de la nota (2116, 2204, 2400) ya los valida la XSL. Los tipos distintos de 01 y 03 (DAE 28 y 56, documentos autorizados, tickets) y el tipo vacío o guion quedan fuera del alcance: `ValidadorModelo` los rechaza (`PCPE-1404`) y en las penalidades sin documento se omite `cac:BillingReference`.

Las filas que comparan con el documento original en el registro de SUNAT (2119–2121, 2207–2209 estado; 3286/4028 importe total; 3209 moneda; 3503 totales por tributo) son `noVerificable`, pero `ValidadorDocumentoModificado` cubre parte cuando el original lo generó PasheCPE y se indica su `FechaEmision`: no puede estar rechazado ni con error de validación (`PCPE-1430`), una factura debe estar aceptada (`PCPE-1431`; una boleta del resumen puede ir en el mismo RC que su nota), misma moneda que la factura (`PCPE-1432`) e importe total de la NC no mayor que el original (`PCPE-1433`, con tolerancia de 1 en boletas y salvo el motivo 10). Si el original no está en el historial, no valida nada. La 2885 (fecha de la nota ≥ fecha del original) la controla `ValidadorModelo` con esa fecha (`PCPE-1408`).

**[verificar]** NC motivo 03 (corrección de la descripción): la R.S. 000048-2026 indica que no varía los importes, pero el Excel 26.08.2026 no tiene una fila ni un código para ello. Si la NC 03 trae importes mayores que cero se emite con la advertencia `PCPE-1420`. En la prueba manual de beta se envían una NC 03 con importes en cero y otra con los importes de la línea corregida; según lo que responda SUNAT, la advertencia pasa a ser un error o se elimina. En la misma prueba se confirma la fila 2369 («PriceAmount diferente de cero»), que la XSL no aplica y que afecta a las NC 03 y 13 en cero.

## 10. Firma digital

```csharp
internal sealed class ProveedorCertificados
{
    // Caché por (ruta, fecha de modificación). Único punto que toca la API de certificados.
    public X509Certificate2 Obtener(string rutaPfx, string clave) =>
        X509CertificateLoader.LoadPkcs12FromFile(rutaPfx, clave,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
}
```

- Prohibido: `new X509Certificate2(...)` (SYSLIB0057), `RSACryptoServiceProvider`, `EphemeralKeySet`.
- `SignedXml` con `SigningKey = cert.GetRSAPrivateKey()`, `CanonicalizationMethod` C14N, `Reference("")` + `XmlDsigEnvelopedSignatureTransform`, `SignatureMethod`/`DigestMethod` según el perfil, `KeyInfoX509Data(cert)` (solo el certificado del firmante).
- Perfil de algoritmo en opciones: `PerfilFirma.Sha256` por defecto (`rsa-sha256` + `sha256`) y `Sha1` (`rsa-sha1` + `sha1`). Decidido con el desarrollador el 23/10/2026: los 8 dorados aceptados por SUNAT están firmados con RSA-SHA256/SHA-256.
- Firma con `Id` configurable (`IdFirma`, por defecto `"PasheSign"`), **sin prefijo** (namespace por defecto `http://www.w3.org/2000/09/xmldsig#`, como los dorados aceptados; con `SignedXml` un prefijo `ds:` obligaría a firmar el `SignedInfo` a mano), insertada en el `ext:ExtensionContent` vacío por XPath con `XmlNamespaceManager`. `cac:Signature` del documento coherente (ID, RUC, razón social, `#IdFirma`).
- Guardar con `PreserveWhitespace = true`, UTF-8 sin BOM y sin reindentar (fuera de la firma, los bytes son los de DATA). Verificación local `CheckSignature(cert, true)` antes del ZIP.
- `ProveedorCertificados` no lanza: archivo inexistente o fuera de CERT (`PCPE-3001`), clave incorrecta (`PCPE-3002`), sin clave privada RSA (`PCPE-3003`), fuera de vigencia en hora de Lima (`PCPE-3004`); la caché se invalida si cambia la fecha del archivo o la clave (que solo se guarda como hash SHA-256 en memoria).
- Valor resumen = `ds:DigestValue` (Base64).
- Certificado próximo a vencer (< 30 días, configurable) → advertencia `PCPE-3005` en el resultado y en el log.

## 11. ZIP y nombres

- `ZipArchive` con una entrada en la raíz: `{nombre}.xml` dentro de `{nombre}.zip`; escritura atómica (temporal + `File.Move(overwrite: true)`).
- `NombreArchivoCpe`: `RUC-TT-SERIE-NUMERO` (número sin ceros a la izquierda), `RUC-RC-yyyyMMdd-n`, `RUC-RA-yyyyMMdd-n`, `RUC-09-T001-n`, `RUC-31-V001-n`; CDR `R-{nombre}.zip`.

## 12. QR

- `ConstructorContenidoQr` (01/03/07/08): `RUC|TIPO|SERIE|NUMERO|IGV|TOTAL|FECHA|TIPODOCADQ|NUMDOCADQ|VALORRESUMEN`, montos `0.00`, fecha `yyyy-MM-dd`; opciones `Qr.CerosNumero` (número a 8 dígitos) y `Qr.SeparadorFinal` (`|` final), ambas por defecto `false`. IGV = subtotal del tributo 1000; TOTAL = importe total a pagar.
- GRE (09/31): contenido = enlace que SUNAT devuelve en el CDR, en `cac:DocumentResponse/cac:DocumentReference/cbc:DocumentDescription` (confirmado con los CDR reales de `tests/Datos/Dorados/09-GRE`: `https://e-factura.sunat.gob.pe/v1/contribuyente/gre/comprobantes/descargaqr?hashqr=…`).
- `GeneradorQrPng` (QRCoder): corrección Q, 20 px por módulo, zona de silencio de 4 módulos, PNG en `QR\yyyy\MM\{nombre}.png`. Tamaño impreso en el PDF A4: 3.5 × 3.5 cm.

## 13. Conectividad

`VerificadorConectividad`:
1. `NetworkInterface.GetIsNetworkAvailable()` → si `false`: `SinConexionInternet`.
2. DNS + conexión TCP al puerto 443 del host destino (timeout 3 s).
3. Si falla, prueba un host de control configurable (por defecto `www.gob.pe:443`): si el control responde → `ServicioNoDisponible`; si no → `SinConexionInternet`.
4. Resultado cacheado por destino 15 s (configurable); invalidado ante cualquier error de red en un envío.
No usa ping (ICMP suele estar bloqueado). Se puede desactivar por opción.

## 14. Transporte SOAP (SUNAT y OSE)

- `HttpClient` nombrado vía `IHttpClientFactory` (`SocketsHttpHandler` con `ConnectCallback`: un timeout de la conexión significa que no se envió nada); SOAP 1.1, `Content-Type: text/xml; charset=utf-8`. `SOAPAction` es dato por endpoint (`soapAction` en `endpoints.json`, plantilla `urn:{operacion}` para SUNAT, decidido el 23/10/2026): **confirmado en beta** (`billService`: `sendBill`, `sendSummary` y `getStatus`, 23/10/2026); producción y `billConsultService` **[verificar]** en el primer uso real; vacío → `SOAPAction: ""`.
- Sobre construido con plantillas (raw string literals) y `wsse:UsernameToken` (`Username = RUC + usuario`, `Password` texto). Para OSE, usuario y clave los asigna el OSE.
- Operaciones: `sendBill(fileName, contentFile)`, `sendSummary(fileName, contentFile)`, `getStatus(ticket)`, `getStatusCdr(rucComprobante, tipoComprobante, serieComprobante, numeroComprobante)`.
- Respuestas parseadas con `XmlReader` (sin cargar DOM completo): `applicationResponse` (Base64 ZIP), `ticket`, `status/statusCode/content`, `statusCdr/statusCode/statusMessage/content`.
- `LectorFaultSoap`: `faultcode`/`faultstring`/`detail`; código = sufijo numérico del `faultcode` (`soap-env:Client.0111` → `0111`) o, si no, el número del `faultstring`; se enriquece con `errores-sunat.json`.
- Timeouts separados: conexión (`TimeoutConexionSegundos`, 10) y respuesta (`TimeoutRespuestaSegundos`, 60). Resiliencia (reintentos con espera exponencial) **solo** para `getStatus`, `getStatusCdr` y token GRE.
- **Faults transitorios y reintentos** (decidido con el desarrollador el 22/10/2026). Nunca hay reintentos infinitos. Los transitorios se agrupan así:
  - *Disponibilidad* (0100, 0109, 0138): SUNAT no recibió el documento.
  - *Constancia o ticket* (0125, 0130, 0307): SUNAT pudo haberlo registrado.
  - *Resto de transitorios* (0131–0137, 0200, 0201, 0251, 0253, 0254): por prudencia se tratan como los de constancia o ticket.

  | Operación | Reintentos dentro de la llamada | Resultado si persiste |
  |---|---|---|
  | `sendBill` | **Ninguno** (principio 4) | `ServicioNoDisponible` con `EsReintentable = true` de inmediato; el sistema de ventas decide cuándo reenviar (su `BackgroundService`) |
  | `sendSummary` | Solo faults de *disponibilidad*, como máximo 3, con espera progresiva | `ServicioNoDisponible` con `EsReintentable = true` |
  | `getStatus`, `getStatusCdr`, token GRE | Hasta 3, con espera progresiva | `ServicioNoDisponible` con `EsReintentable = true` |

- **Siguiente `EnviarAsync` del mismo documento**: antes de reenviar se lee el último intento del historial (`LOG`).
  - Si fue `RespuestaIncierta` o un fault de *constancia o ticket* (o del resto de transitorios), primero se consulta con `getStatusCdr`. Si SUNAT ya lo tiene, se devuelve ese estado y no se reenvía.
  - Si fue un fault de *disponibilidad*, se reenvía directamente.
  - En el RC y la RA, que no tienen `getStatusCdr`, el ticket registrado se consulta con `getStatus` antes de reenviar (Fase 6): si hay ticket no se reenvía, salvo que SUNAT responda que no existe (0127). Sin ticket (respuesta incierta o servicio no disponible) se reenvía el mismo ZIP; si SUNAT ya lo tenía responde que el identificador ya fue presentado (2223, **[verificar]** con el primer caso real) y el resultado se devuelve tal cual.
  - Donde `getStatusCdr` no existe (beta, boletas, series que no empiezan con F: códigos 0009/0010 de la consulta), se reenvía **el mismo ZIP** de ENVIO sin regenerarlo; si SUNAT ya lo tenía responde 1033 (decidido con el desarrollador el 23/10/2026).
  - Los grupos *disponibilidad* y *constancia* son datos (`grupo` en `tools/GeneradorRecursos/Datos/errores-transitorios.json` → `grupoTransitorio` en `errores-sunat.json`).
  - Una cancelación del llamador durante la llamada se registra como `RespuestaIncierta` (SUNAT pudo haber recibido el documento) y luego se propaga la cancelación.
- 0140 (`enProceso`): nunca se reenvía; `EnProceso` con `RequiereConsultaEstado = true` y consulta del CDR después de una espera.
- Bloqueo por documento dentro del proceso (`SemaphoreSlim` por nombre de archivo) para impedir envíos simultáneos del mismo comprobante.

Endpoints por defecto (`endpoints.json`, sobrescribibles por emisor/canal):

| Canal | Ambiente | Servicio | URL |
|---|---|---|---|
| SUNAT | Beta | billService | `https://e-beta.sunat.gob.pe/ol-ti-itcpfegem-beta/billService` |
| SUNAT | Producción | billService | `https://e-factura.sunat.gob.pe/ol-ti-itcpfegem/billService` |
| SUNAT | Producción | billConsultService (`getStatusCdr`) | `https://e-factura.sunat.gob.pe/ol-it-wsconscpegem/billConsultService` **[verificar]** |
| OSE | Producción | billService | **[pendiente]** — se define con el sistema de ventas que use OSE |
| OSE | Pruebas | billService | **[pendiente]** — ídem |
| GRE | Producción | token | `https://api-seguridad.sunat.gob.pe/v1/clientessol/{client_id}/oauth2/token/` |
| GRE | Producción | API | `https://api-cpe.sunat.gob.pe/v1/contribuyente/gem/comprobantes` |

Notas: `endpoints.json` no trae valores por defecto para OSE; el emisor o la configuración los indican. `getStatusCdr` no existe en beta → `ConsultarCdrAsync` en beta devuelve `ErrorServicio` "no soportado en beta". Si el OSE no ofrece `getStatusCdr`, el canal lo declara (`SoportaConsultaCdr = false`) y la reconciliación se hace con `getStatus` o consulta manual.

## 15. Lectura del CDR

`LectorCdr` (con `XmlReader` y `XmlNamespaceManager`): guarda `R-{nombre}.zip` íntegro en `RPTA\yyyy\MM\`; extrae en memoria; lee `cbc:ResponseDate`, `cbc:ResponseTime`, `cac:DocumentResponse/cac:Response/cbc:ResponseCode`, `cbc:Description`, `cbc:ReferenceID`, todas las `cbc:Note` (formato `"4252 - texto"` → `ObservacionCpe{Codigo, Descripcion}`), `cac:DocumentReference/cbc:ID` y, en GRE, el enlace del QR. Valida la firma del CDR solo si `VerificarFirmaCdr = true` (si no es válida, observación `PCPE-4009`). SUNAT agrega dentro de `Signature` elementos fuera del estándar (`Proposito`, `Revocacion`, `TSL`, `Expiracion`) que no están firmados: se retiran antes de verificar. Los CDR SOAP dorados verifican; en los de la GRE (API REST) el `SignedInfo` verifica pero el digest del documento no coincide (revisar en la Fase 10). El ZIP del CDR por SOAP trae además una entrada de carpeta `dummy/` (vista en los CDR reales): se ignoran las entradas que no son archivos XML.

## 16. Tickets

Espera progresiva configurable (por defecto 2, 4, 8, 15, 30 s; máximo 5 consultas por llamada a `EsperarTicketAsync`, con `TimeProvider`); `98` → `EnProceso` con ticket; `0` → CDR; `99` → rechazado (con o sin CDR). Los estados son datos (`estados-ticket.json`). Cada consulta (`ConsultarTicketAsync`) reintenta hasta 3 veces ante faults transitorios o fallas de red; si se agotan las esperas se devuelve `EnProceso` con el ticket. El ticket siempre queda en el historial y `ConsultarTicketAsync` sin ticket lo toma de ahí, así se consulta desde otro proceso u otro día. En la Fase 6 lo implementa `EnvioResumenes` (SOAP); en la Fase 10 se evalúa extraerlo para la GRE.

**Resumen diario (Fase 6, decidido con el desarrollador el 23/10/2026)**:
- Generación: bloqueo del día por RUC (`RUC-RC-yyyyMMdd`); correlativo = mayor `n` de ENVIO y LOG del periodo + 1; lotes de 500 con `LineID` 1…n. Un error del modelo detiene todo; un lote con errores no se escribe ni impide los demás.
- Validación del modelo (`ValidadorResumen`, `PCPE-13xx`): tipo y serie, estado (Cat. 19), fechas (2671), repetidos (3094, 3095, 3096), cliente para importes > S/ 700 (2514), notas con boleta modificada (2583/2582), importes no negativos y, contra el historial de cada documento, no adicionar lo que SUNAT ya tiene o procesa (2282) ni modificar o anular lo que no aceptó (2663). Fuera de plazo se envía con la advertencia `PCPE-1318` (SUNAT observa 4443); la anulación fuera de plazo se bloquea (`PCPE-1313`, 2957).
- Mapeo: un `BillingPayment` por tipo de valor de venta (Cat. 11) distinto de cero; gratuitas en 05 o desagregadas en 06–09 según la vigencia del Cat. 11 a la fecha de generación (= recepción); IGV 1000 siempre con su tasa; ISC, ICBPER y otros cargos solo si son mayores que cero; 9996 con el IGV de gratuitas cuando rigen las gratuitas desagregadas.
- El resultado del resumen se registra también en el historial de cada documento informado: `EnProceso` al recibir el ticket, `Aceptado`/`AceptadoConObservaciones` con el CDR y, si el resumen se rechaza, las boletas adicionadas vuelven a `Generado` (se pueden informar en otro RC); las modificadas o anuladas conservan su estado y suman el intento.

## 17. GRE — API REST con OAuth2

1. **Token** (`ProveedorTokenGre`): `POST` form `grant_type=password`, `scope=https://api-cpe.sunat.gob.pe`, `client_id`, `client_secret`, `username={RUC}{UsuarioSOL}`, `password={ClaveSOL}`. Caché por `(client_id, username)` hasta `expires_in − 60 s`; renovación con bloqueo para evitar solicitudes simultáneas.
2. **Envío**: `POST {api}/{RUC-TT-SERIE-NUMERO}` con `{"archivo":{"nomArchivo":"{nombre}.zip","arcGreZip":"<Base64>","hashZip":"<SHA-256>"}}` → `numTicket`, `fecRecepcion`. Formato del hash: hexadecimal en minúsculas **[verificar contra el manual GRE]**.
3. **Consulta**: `GET {api}/envios/{numTicket}` → `codRespuesta` (0/98/99), `error{numError, desError}`, `arcCdr` (Base64), `indCdrGenerado`.
4. Errores HTTP: 401 → renovar token y reintentar una vez; 400/404/422 → `ErrorServicio` con detalle; 500/503 → `ServicioNoDisponible`.
5. Sin ambiente beta: la GRE **siempre** usa los endpoints de producción. Si `Ambiente = Beta`, la librería no envía GRE (devuelve `ErrorServicio` "GRE solo existe en producción") salvo que `Gre.PermitirEnvioConAmbienteBeta = true`. Es una protección contra emisiones reales accidentales durante pruebas. La demo pide confirmación explícita antes de enviar una GRE.

## 18. Almacenamiento e historial

- `AlmacenCpe` es el único componente que compone rutas (`Path.Combine`): `{RutaBase}\{CARPETA}\{yyyy}\{MM}\{archivo}`; `CERT\{archivo.pfx}` sin periodo. Crea carpetas bajo demanda. **Sin subcarpetas por día.**
- Escrituras atómicas (temporal en la misma carpeta + `File.Move`).
- `RegistroHistorial`: `LOG\yyyy\MM\{nombre}.json` con `{ referencia, estadoActual, ticket, intentos: [ { fecha, operacion, canal, endpoint, duracionMs, estado, codigo, descripcion, http, ticket } ], documentos }`; `documentos` (solo en el RC) lista las boletas y notas informadas con su estado del ítem, para registrar el resultado del resumen en el historial de cada una (§16). Serialización con `JsonSerializerContext` generado. Nunca contiene credenciales. Acceso serializado por documento.

## 19. Fecha y hora

- `TimeProvider` inyectable (pruebas con `FakeTimeProvider`).
- `ZonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Lima")` (en Windows .NET 10 resuelve IANA vía ICU; respaldo `"SA Pacific Standard Time"`).
- Fecha de emisión por defecto, historial, plazos y nombres de RC/RA usan esa zona.

## 20. Rendimiento y concurrencia

- Servicios registrados como singleton, sin estado mutable compartido; cachés con `ConcurrentDictionary`/`Lazy<T>`.
- Precalentamiento opcional (`OpcionesPasheCpe.PrecalentarAlIniciar`): compila XSL, esquemas y serializadores en segundo plano.
- Streams y `ArrayPool` para ZIP y Base64; `XmlReader` para respuestas.
- `ConfigureAwait(false)` en toda la librería.
- Benchmarks (Fase 12): generación+firma+ZIP de factura de 20 líneas < 150 ms en caliente; validación XSL < 300 ms.

## 21. Observabilidad y seguridad

- `ILogger<T>`: `Information` hitos con nombre de archivo; `Warning` observaciones, certificado por vencer, reintentos; `Error` faults, rechazos y fallas técnicas; `Debug` sobres SOAP y JSON completos **con credenciales y Base64 truncados/enmascarados** (`Enmascarador`).
- `ActivitySource("PasheCPE")` por operación (generar, validar, firmar, enviar, consultar) y `Meter("PasheCPE")`: `pashecpe.envios` (por estado), `pashecpe.envio.duracion`, `pashecpe.validacion.errores`.
- Claves solo en memoria, nunca en disco ni en logs. TLS 1.2+ del sistema operativo.

## 22. PDF (PasheCPE.Pdf)

- QuestPDF, licencia Community declarada al registrar (`QuestPDF.Settings.License = LicenseType.Community`).
- `LectorRepresentacion` lee el **XML firmado** de `FIRMA\` (y el CDR si existe) → modelo de impresión. Así el PDF refleja exactamente lo enviado y puede regenerarse en cualquier momento.
- `MarcaEmisor`: logo (ruta o bytes), color principal, teléfonos, correo, web, texto de pie.
- **DisenoComprobante** (01, 03, 07, 08): cabecera (logo, emisor, recuadro RUC/tipo/serie-número), cliente, fechas, moneda, forma de pago y cuotas, documento modificado y motivo (notas), detalle de líneas, totales por afectación, ICBPER, detracción, leyendas, monto en letras, QR 3.5 × 3.5 cm, valor resumen, "Representación impresa de la …", estado SUNAT si hay CDR.
- **DisenoGuia** (09, 31): cabecera, remitente/destinatario, motivo y fecha de traslado, modalidad, partida/llegada, vehículos, conductores, transportista, bienes, peso y bultos, documentos relacionados, QR de SUNAT.
- Formato: `S/ 0,000.00` (o símbolo de la moneda), fechas `DD/MM/AAAA`, A4 vertical, fuentes embebidas.
- Salida en `REPO\yyyy\MM\{nombre}.pdf`. Generación opcional dentro de `EmitirAsync` si `AddPasheCpePdf` está registrado y `GenerarPdfAutomaticamente = true`.

## 23. Configuración

```jsonc
// appsettings.json del sistema de ventas
"PasheCPE": {
  "RutaBase": "D:\\CPE",
  "Ambiente": "Beta",                       // Beta | Produccion
  "ExoneradoAmazoniaPorDefecto": true,
  "IdFirma": "PasheSign",
  "PerfilFirma": "Sha256",                 // Sha256 | Sha1
  "ModoEnvioBoletas": "ResumenDiario",      // ResumenDiario | Individual
  "Validacion": { "Modelo": true, "Esquema": true, "XslSunat": true, "ReglasComplementarias": true },
  "TimeoutConexionSegundos": 10,
  "TimeoutRespuestaSegundos": 60,
  "Conectividad": { "Verificar": true, "HostControl": "www.gob.pe", "CacheSegundos": 15 },
  "Tickets": { "EsperasSegundos": [2, 4, 8, 15, 30] },
  "Gre": { "PermitirEnvioConAmbienteBeta": false },
  "VerificarFirmaCdr": false,               // verifica la firma de SUNAT en cada CDR
  "AvisoVencimientoCertificadoDias": 30,
  "PrecalentarAlIniciar": true,
  "GenerarPdfAutomaticamente": false,
  "Endpoints": [                            // opcional para SUNAT; obligatorio para OSE
    { "Canal": "OSE:efact", "Ambiente": "Produccion", "Servicio": "Comprobantes", "Url": "https://…/billService" }
  ],
  "Tasas": { "Igv": [], "MypeRestaurantesHoteles": [], "Icbper": [] },  // cada lista informada reemplaza a la de tasas.json
  "Plazos": [],                             // si se informa, reemplaza plazos.json
  "VigenciasCatalogos": [                   // reemplazan las vigencias de catalogos.json (p. ej. una nueva postergación)
    // { "Catalogo": "10", "Codigo": "13", "Desde": "2027-07-01" }
  ],
  "MontoEnLetras": { "ConTildes": false },  // leyenda 1000 sin tildes (impresoras ESC/POS)
  "Qr": { "CerosNumero": false, "SeparadorFinal": false }
}
```

```csharp
builder.Services.AddPasheCpe(builder.Configuration.GetSection("PasheCPE"));  // valida opciones al iniciar (ValidateOnStart)
builder.Services.AddPasheCpePdf();

// Sin DI: arma internamente el mismo ServiceCollection que AddPasheCpe y lo encapsula.
using var pashe = PasheCpe.Crear(new OpcionesPasheCpe { RutaBase = @"D:\CPE", Ambiente = Ambiente.Beta }, loggerFactory);
var r = await pashe.Cpe.EmitirAsync(emisor, factura, ct);   // pashe.Gre, pashe.Historial, pashe.Conectividad
// Dispose libera el proveedor de servicios y los HttpClient.
```

`PasheCpe.Crear` valida las opciones de inmediato (`OptionsValidationException`). Cada servicio aparece en `PasheCpe` en la fase que lo implementa (Fase 1: `Historial`; Fase 4: `Cpe`), sin stubs. Un endpoint con `"verificado": false` en `endpoints.json` (p. ej. `billConsultService`) se advierte en el log la primera vez que se usa.

## 24. Pruebas

- **Unitarias sin red** (`PasheCPE.Tests`, xUnit v3): cálculo (gravado, exonerado Amazonía, MYPE 2026/2027, mixto, gratuitas, ICBPER, descuentos, crédito con cuotas, detracción, precio con impuestos), número a letras, nombres, rutas año/mes, tasas por vigencia, plazos, reglas por régimen.
- **XML dorados**: por documento, comparación con XML aceptados por SUNAT (normalizando espacios) y **validación XSD obligatoria** de todo XML generado.
- **XSL**: cada XSL compila; los dorados pasan sin errores; casos inválidos producen el código esperado.
- **Firma**: certificado autofirmado generado en la prueba; `CheckSignature`; firma en la ubicación exacta; alteración posterior detectada.
- **Transporte**: `HttpMessageHandler` falso con respuestas grabadas (CDR aceptado/observado/rechazado, faults 0111/1033/0100, HTTP 503, timeout de conexión y de lectura, ticket 98→0, 99, GRE 401/422).
- **Conectividad**: simulada con dobles de prueba.
- **Concurrencia**: 50 emisiones paralelas sin colisión de archivos ni de historial.
- **PDF**: generación de ambos diseños sin excepción y con QR del tamaño indicado.
- **Envíos reales**: solo desde `samples\PasheCPE.Demo`, ejecutados manualmente por el desarrollador; credenciales en `dotnet user-secrets`.
- `.gitattributes` marca `tests\Datos\**` como binario para no alterar bytes de XML firmados.

## 25. Distribución y CI

- Paquetes `PasheCPE` y `PasheCPE.Pdf`, SemVer (parche = correcciones; menor = funciones compatibles; mayor = cambio de contrato), README y XML-docs incluidos, Source Link y símbolos embebidos en el ensamblado (`DebugType=embedded`; GitHub Packages no admite `.snupkg`).
- Feed: GitHub Packages `https://nuget.pkg.github.com/AlcyPashe/index.json`; los consumidores lo agregan en su `nuget.config` con un PAT de lectura (`read:packages`).
- `ci.yml` (push/PR): restore, build (warnings como errores), test, en `windows-latest`.
- `publicar.yml` (tag `v*`): pack + push al feed con `GITHUB_TOKEN`.

## 26. Compatibilidad con Bottega

`docs\Contrato-Bottega.md` describe una API anterior (`ProcesadorCpe`, `OpcionesCpe` con credenciales). Equivalencias en PasheCPE 2.0:

| Bottega | PasheCPE 2.0 |
|---|---|
| `GenerarFactura(doc)` | `IServicioCpe.GenerarAsync(emisor, cpe)` |
| `GenerarResumen(res)` | `GenerarResumenAsync` |
| `EnviarComprobanteAsync(nombre, fecha)` | `EnviarAsync(emisor, referencia)` |
| `EnviarResumenAsync` / `ConsultarTicketAsync` | `EnviarAsync` / `ConsultarTicketAsync` / `EsperarTicketAsync` |
| `ConsultarCdrAsync(...)` | `ConsultarCdrAsync(emisor, referencia)` |
| `OpcionesCpe` (credenciales globales) | `OpcionesPasheCpe` (global) + `EmisorCpe` (por llamada) |
| `ErrorComunicacion` | `SinConexionInternet` / `ServicioNoDisponible` / `RespuestaIncierta` |
| `Excepcion` (fault) | `ErrorServicio` |
| `EnProceso` | `EnProceso` |

El TRD de Bottega debe actualizarse a esta tabla antes de integrar.

## 27. Prohibiciones (checklist de revisión)

1. Ninguna dependencia fuera de §1.
2. Ninguna tasa, plazo, endpoint o código de error escrito en el código fuera de los recursos de datos.
3. Ningún reenvío automático de `sendBill`.
4. Ninguna excepción por respuesta de negocio o de red.
5. Ninguna credencial en logs, historial o repositorio; ningún `.pfx` versionado.
6. Ninguna reindentación de XML firmado.
7. Ningún acceso a base de datos.
8. Ninguna subcarpeta por día en el almacén.
9. Ninguna llamada real a SUNAT/OSE desde las pruebas automáticas.
