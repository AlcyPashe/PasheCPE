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
| Resiliencia HTTP | `Microsoft.Extensions.Http.Resilience` | PasheCPE | Microsoft; solo operaciones idempotentes |
| Serialización rápida | `Microsoft.XmlSerializer.Generator` | PasheCPE | Microsoft; genera los serializadores en compilación |
| QR | `QRCoder` | PasheCPE | MIT, utilitario permitido |
| PDF | `QuestPDF` | PasheCPE.Pdf | Licencia Community (ingresos < USD 1 M) |
| Pruebas | `xunit.v3`, `Microsoft.NET.Test.Sdk`, `Microsoft.Extensions.TimeProvider.Testing`, `coverlet.collector` | tests | — |
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
│   │                                      Control de Cambios) + reglas GRE (Excel aparte, antes de la Fase 10)
│   ├── CATALOGOS\                         solo catálogos externos que el Excel referencia por enlace
│   │                                      (p. ej. UNSPSC completo CCNU_MOD_2.xlsm para el Cat. 25)
│   └── VERSION.md                         origen, fecha y resolución de cada archivo
├── tools\
│   ├── generar-ubl.ps1                    XSD → clases de referencia (tools\ubl-referencia\, no compila)
│   ├── GeneradorRecursos\                 consola C#: Excel SUNAT → JSON embebidos + XML de catálogos para las XSL
│   └── github-workflows\                  workflows a mover a .github\workflows\ en la Fase 0
├── src\
│   ├── PasheCPE\                          paquete principal
│   │   ├── Modelos\          Entrada: EmisorCpe, ComprobanteCpe, LineaCpe, NotaCpe, ResumenDiarioCpe,
│   │   │                     ComunicacionBajaCpe, GuiaRemisionCpe, ClienteCpe, DireccionCpe, ReferenciaCpe…
│   │   ├── Resultados\       ResultadoGeneracion, ResultadoEnvio, ResultadoEmision, EstadoCpe, ErrorCpe,
│   │   │                     ObservacionCpe, RutasCpe, IntentoEnvio, HistorialCpe, EstadoConectividad
│   │   ├── Catalogos\        catálogos SUNAT (generados) + TablaTasas + TablaPlazos + CatalogoErrores
│   │   ├── Tributos\         CalculadoraTributaria, ReglasRegimen, NumeroALetras, Redondeo
│   │   ├── Ubl\Comunes\      cbc/cac/ext/sac (perfil SUNAT)
│   │   ├── Ubl\Documentos\   Invoice, CreditNote, DebitNote, SummaryDocuments, VoidedDocuments,
│   │   │                     DespatchAdvice, ApplicationResponse (lectura)
│   │   ├── Mapeo\            MapeadorInvoice, MapeadorCreditNote, MapeadorDebitNote, MapeadorSummary,
│   │   │                     MapeadorVoided, MapeadorDespatchAdvice
│   │   ├── Serializacion\    SerializadorUbl
│   │   ├── Validacion\       ValidadorModelo, ValidadorEsquema, MotorXslSunat, Emulacion\ (DataPower,
│   │   │                     EXSLT, resolutor local:///), Reglas2027\
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
│   └── Datos\                             XML dorados, CDR reales, respuestas SOAP/REST grabadas
├── samples\
│   └── PasheCPE.Demo\                     consola para envíos reales a beta/producción (manual)
└── benchmarks\PasheCPE.Benchmarks\        (opcional, Fase 12)
```

Decisión: **un solo ensamblado principal** con carpetas por responsabilidad y tipos `internal` (solo la fachada, modelos, resultados, opciones e interfaces son `public`). Menos ensamblados = arranque más rápido, un solo paquete que versionar y cero dependencias circulares. `PasheCPE.Pdf` va aparte porque arrastra QuestPDF. `InternalsVisibleTo` solo para los proyectos de prueba.

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
| `Catálogos` (bloques verticales `No.` / `Catálogo` / `Código`–`Descripción`, catálogos 01–65 y 25.1–25.3) | `catalogos.json` + `catalogos\cat_XX.xml` | Clases de catálogo en C# y plantilla `findElementInCatalog` de las XSL |
| `CódigosRetorno` (≈2 077 códigos) | `errores-sunat.json` | Descripción oficial; `tipo` y `transitorio` se completan con la hoja `General ` y una lista manual revisada |
| `General ` (errores de servicio/sobre: 0100, 0109, 0111, 0151…) | `errores-sunat.json` (tipo `servicio`) | Clasificación de faults (TRD §5.1) |
| `Factura2_0`, `Boleta2_0`, `NotaCredito2_0`, `NotaDebito2_0`, `Resumen Diario1_1`, `Comunicación de Baja1_0` | `reglas-cpe.json` (N.°, dato, tag UBL, condición, tipo ERROR/OBSERV, código) | Matriz de reglas: fuente de las pruebas de validación y de las reglas C# que las XSL 2022 no cubren |
| `Control de Cambios` | `reglas-cpe.json` (campo `vigencia`) | Identifica las reglas con vigencia 01/01/2027 (R.S. 000048-2026) |
| `Parámetros` (004 Plazo máximo de envío) | contraste con `plazos.json` | Verificación de plazos |

Hojas fuera de alcance (no se procesan): Retenciones, Percepciones, Resumen de reversiones, Liquidación de compra, DAE-*, DOAE-*, CDR-OSE-*, `Firma` (solo referencia), `Listados` (padrones que SUNAT consulta del lado servidor; no se replican).

Notas del archivo:
- Los catálogos 02 (ISO 4217), 03 (UN/ECE Rec. 20), 04 (ISO 3166) y 25 (UNSPSC completo) solo aparecen como **enlace**. Para la validación local basta el formato; si se requiere la lista completa del 25, el desarrollador descarga `CCNU_MOD_2.xlsm` a `recursos\CATALOGOS` (opcional, recurso comprimido y de carga perezosa).
- El Excel **no incluye la GRE**; sus reglas se publican aparte en la sección GRE del portal y se agregan antes de la Fase 10.
- Al publicar SUNAT una versión nueva del Excel: reemplazar el archivo, actualizar `VERSION.md`, ejecutar el generador y revisar el diff de los JSON.

### 3.3 Datos versionados embebidos (sobrescribibles por configuración)

| Archivo | Contenido |
|---|---|
| `tasas.json` | IGV/IPM, tasa especial MYPE restaurantes/hoteles, ICBPER, con `desde`/`hasta` |
| `plazos.json` | Plazos de envío por tipo de documento |
| `errores-sunat.json` | Código → descripción oficial, tipo (`servicio`, `rechazo`, `observacion`) y `transitorio` (bool) — generado del Excel (§3.2) |
| `reglas-cpe.json` | Matriz de reglas por documento con tag UBL, tipo, código y vigencia — generado del Excel (§3.2) |
| `endpoints.json` | Endpoints por canal y ambiente |
| `reglas-2027.json` | Parámetros de las reglas complementarias (códigos de error, motivos válidos) |

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
- `NotaCpe`: datos de `ComprobanteCpe` + tipo (07/08), motivo (Cat.09/10), sustento, **un** documento modificado (tipo, serie, número).
- `ResumenDiarioCpe`: fecha de referencia + líneas (boleta/nota, estado 1 adicionar / 2 modificar / 3 anular, cliente con razón social, totales por afectación con gratuitas desagregadas en gravadas/exoneradas/inafectas, IGV de gratuitas y tasa del IGV — campos 2027). La librería asigna correlativos y divide en lotes de 500.
- `ComunicacionBajaCpe`: fecha de referencia + documentos (tipo, serie, número, motivo).
- `GuiaRemisionCpe` (09/31): remitente/transportista, destinatario, motivo (Cat.20) y descripción, modalidad (Cat.18: 01 público / 02 privado), fecha de inicio de traslado, peso bruto y unidad, bultos, partida y llegada (ubigeo, dirección, código de establecimiento), vehículos (placa, autorización), conductores (documento, nombres, licencia), transportista (RUC, razón social, MTC), indicador M1/L, documentos relacionados (Cat.61), bienes (código, descripción, unidad, cantidad, bien normalizado Cat.62).
- `ReferenciaCpe`: `(Ruc, TipoDocumento, Serie, Numero, Fecha)` o `(Ruc, Tipo RC/RA, Fecha, Correlativo)`; resuelve nombre de archivo y periodo.

Todos son `record` inmutables, serializables con `System.Text.Json` mediante el contexto público generado `PasheCpeJson` (`JsonSerializerContext`), que incluye modelos, resultados e historial.

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
| Fault con código `transitorio = true` (p. ej. "El sistema no puede responder su solicitud") | `ServicioNoDisponible` | sí | según operación |
| Fault 0100–1999 no transitorio (credenciales, perfil, nombre/estructura de archivo) | `ErrorServicio` | no | no |
| Fault 1033 (registrado previamente) | — | — | **sí** → `getStatusCdr` y se devuelve el estado real |
| Timeout de **lectura** tras enviar, conexión cortada a mitad | `RespuestaIncierta` | no (reconciliar) | **sí** |
| HTTP 401 GRE | renovar token una vez; si persiste `ErrorServicio` | — | — |
| HTTP 422 GRE (`errors[]`) | `ErrorServicio` con la lista | no | no |
| CDR 0 sin notas | `Aceptado` | — | — |
| CDR 0 con notas ≥ 4000 | `AceptadoConObservaciones` | — | — |
| CDR 2000–3999 | `Rechazado` (permite reemitir corregido) | no | no |
| `sendSummary` / GRE envío correcto, ticket 98 | `EnProceso` | — | sí |
| Ticket 0 | según CDR | — | — |
| Ticket 99 con CDR | `Rechazado` con errores del CDR | no | no |
| Ticket 99 sin CDR (GRE `error.numError/desError`) | `Rechazado` o `ErrorServicio` según código | no | no |

Regla dura: **ningún** `sendBill` se reenvía automáticamente; `RespuestaIncierta` y 1033 siempre pasan por reconciliación.

## 6. API pública

```csharp
public interface IServicioCpe   // 01, 03, 07, 08, RC, RA
{
    Task<ResultadoGeneracion> GenerarAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct = default);
    Task<ResultadoGeneracion> GenerarAsync(EmisorCpe emisor, NotaCpe nota, CancellationToken ct = default);
    Task<IReadOnlyList<ResultadoGeneracion>> GenerarResumenAsync(EmisorCpe emisor, ResumenDiarioCpe resumen, CancellationToken ct = default);
    Task<ResultadoGeneracion> GenerarBajaAsync(EmisorCpe emisor, ComunicacionBajaCpe baja, CancellationToken ct = default);

    Task<ResultadoEnvio> EnviarAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct = default);   // sendBill o sendSummary según tipo
    Task<ResultadoEmision> EmitirAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct = default);       // generar + enviar
    Task<ResultadoEmision> EmitirAsync(EmisorCpe emisor, NotaCpe nota, CancellationToken ct = default);

    Task<ResultadoEnvio> ConsultarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string ticket, CancellationToken ct = default);
    Task<ResultadoEnvio> EsperarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string ticket, CancellationToken ct = default); // con reintentos progresivos
    Task<ResultadoEnvio> ConsultarCdrAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct = default);   // getStatusCdr

    Task<ResultadoValidacion> ValidarAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct = default);       // solo nivel 1
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

Pipeline de `EmitirAsync`: **validar modelo → calcular → mapear UBL → serializar (DATA) → validar XSD + XSL + reglas 2027 → firmar (FIRMA, valor resumen) → ZIP (ENVIO) → QR (QR) → verificar conectividad → enviar → clasificar → CDR (RPTA) → historial (LOG) → resultado**. Cada etapa persiste su archivo y el resultado devuelve todas las rutas. `EnviarAsync` parte de ENVIO\ y nunca regenera.

## 7. Cálculo tributario

- Toda la aritmética con `decimal`; redondeo `MidpointRounding.AwayFromZero` a 2 decimales en montos; valores unitarios hasta 10 decimales.
- `ModoPrecio.PrecioUnitario`: valor unitario = precio / (1 + tasa) con la tasa de la línea.
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
- Leyenda 1000 (monto en letras) con `NumeroALetras` propio ("CIENTO VEINTE CON 50/100 SOLES").
- Cuadre obligatorio antes de serializar: suma de líneas = `LineExtensionAmount`; `TaxInclusiveAmount`; descuentos/cargos/anticipos; `PayableAmount`.
- Forma de pago: `cac:PaymentTerms` `FormaPago` Contado, o Crédito + monto pendiente + `Cuota001…` con monto y vencimiento; detracción con `PaymentMeans` (cuenta Banco de la Nación) y `PaymentTerms` (código Cat.54, porcentaje, monto).

## 8. Serialización

- `XmlSerializerNamespaces` explícitos: default del documento, `cac`, `cbc`, `ext`, `ds`, `sac` (Summary/Voided/notas cuando aplique); sin `xsi`/`xsd` espurios.
- `XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, NewLineChars = "\n" }`.
- `InvariantCulture`; fechas `yyyy-MM-dd`, horas `HH:mm:ss`; atributos de catálogo (`listAgencyName="PE:SUNAT"`, `listName`, `listURI`, `unitCodeListID="UN/ECE rec 20"`…) según la guía de elaboración vigente.
- Opcionales vacíos se omiten (`ShouldSerialize*`).
- `UBLExtensions/UBLExtension/ExtensionContent` vacío preparado para la firma.
- Versiones: Invoice/CreditNote/DebitNote/DespatchAdvice `UBLVersionID 2.1`, `CustomizationID 2.0`; SummaryDocuments `2.0`/`1.1`; VoidedDocuments `2.0`/`1.0`.

## 9. Validación local (nivel 1)

### 9.1 Modelo (C#)
Obligatorios, serie vs tipo (F/B/T/V…), tipo de documento del cliente (factura → 6; boleta > S/ 700 → identificación), fechas, plazos (`FechaLimiteEnvio` vencida → error), NRUS, longitud de campos, código de producto 8 dígitos, una sola referencia en notas.

### 9.2 Esquema
`XmlSchemaSet` compilado una vez desde `recursos\XSD` (embebidos), con resolutor local para los `import`. Se valida el XML **sin firmar** (la firma no altera la estructura validada).

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
| 09 | `ValidaExprRegGuiaRemitente-2.0.1.xsl` |
| 31 | sin XSL oficial → XSD + reglas C# |

Las XSL de SUNAT son XSLT 1.0 escritas para IBM DataPower. Lo que se encontró en los archivos y cómo se resuelve con `XslCompiledTransform`:

| Dependencia | Uso en las XSL | Solución |
|---|---|---|
| `xsl:include href="local:///commons/error/validate_utils.xsl"` y `error_utils.xsl` | Plantillas `isTrueExpresion` (307 llamadas), `regexpValidateElementIfExist` (291), `existElement` (143), `existAndRegexpValidateElement`, `findElementInCatalog`, `existAndValidateValueTwoDecimal`, `rejectCall`, `addWarning`, `isDateBefore`, etc. **No vienen en la descarga de SUNAT.** | Implementación propia en `Recursos\xsl-utils\` con la misma firma de plantillas y parámetros (`errorCodeValidate`, `node`, `regexp`, `expresion`, `catalogo`, `idCatalogo`, `descripcion`…). En vez de `dp:reject` (detiene el proceso), **emiten nodos** `<pcpe:error codigo="…" ruta="…"/>` y `<pcpe:observacion …/>`, de modo que se reportan todos los errores de una pasada. |
| `XmlResolver` para `local:///` | includes, `document('local:///commons/cpe/datos/dat_fechas.xml')`, catálogos | `ResolutorRecursosLocales : XmlResolver` que mapea `local:///…` a recursos embebidos. |
| `dp:variable('var://context/cpe/…')` (14 usos: nombre del archivo enviado, etc.) | Función | Objeto de extensión registrado en `XsltArgumentList` para `http://www.datapower.com/extensions` con `variable(string)` que lee un diccionario de contexto por documento (nombre de archivo, RUC, tipo, serie, número…). En la Fase 3 se inventarían todas las claves. |
| `dp:ignore-multiple="yes"` | Atributo en `xsl:include` | Ignorado (atributo de otro namespace, válido en XSLT 1.0). |
| `extension-element-prefixes="dp"` | Declaración | Compatible; las plantillas propias no usan elementos `dp:`. |
| EXSLT `regexp:test/match/replace`, `date:seconds`, `date:date`, `date:difference` | Funciones | Objetos de extensión para `http://exslt.org/regular-expressions` y `http://exslt.org/dates-and-times` (con `Regex` compilado y cacheado, y fechas en hora de Lima). |
| Catálogos (`cat_XX.xml`) | `findElementInCatalog` | Generados por `tools\GeneradorRecursos` desde la hoja `Catálogos` (§3.2). |

- Cada XSL se compila **una sola vez** (perezoso, `Lazy<T>` thread-safe) con `XsltSettings(enableDocumentFunction: true, enableScript: false)`; el precalentador puede compilarlas al iniciar.
- Las XSL originales no se modifican; se embeben tal cual.
- Prueba de humo temprana (Fase 3): compilar las 7 XSL y validar un XML dorado aceptado por SUNAT sin falsos errores.

### 9.4 Reglas complementarias 2027 (C#)

Fuente principal: las filas del Excel de reglas cuya vigencia en `Control de Cambios` es 01/01/2027 (`reglas-cpe.json`). Cambios ya identificados en el Excel 26.08.2026: NC/ND con documentos 28 y 56 (ERR-2205, 2207, 2208, 2209, 2885, 3209); **Resumen diario**: operaciones gratuitas desagregadas en gravadas/exoneradas/inafectas, campo "Tasa del IGV" (ERR-2992, 3504, 3102), IGV de gratuitas y razón social del adquiriente (ERR-2022 pasa de OBS a ERROR); Catálogo 09 con código 13 y Catálogo 10 con 13 (Penalidades) y 03 "Penalidades/otros conceptos"; Catálogo 54 con 038 y 042.

`IReglaCpe` registradas por tipo de documento, parametrizadas en `reglas-2027.json`: una referencia por NC (3261) / ND (3194); NC motivo 03 sin variación de importes; ND motivo 13 penalidades con IGV 0 (3507); ND motivo 03 "Otros conceptos"; serie F/B coherente con el documento modificado; código de producto de 8 dígitos numéricos (≠ 00000000/99999999); contrato de colaboración (tipo 1/2, número 1–50 alfanumérico, descripción 1–250, porcentaje 2 enteros y 2 decimales); catálogos 09/10 vigentes. Los códigos se toman de `reglas-cpe.json`; los que no figuren en el Excel 26.08.2026 se marcan **[verificar]**.

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
- `SignedXml` con `SigningKey = cert.GetRSAPrivateKey()`, `SignatureMethod = rsa-sha1`, `CanonicalizationMethod` C14N (configurable a exc-c14n), `Reference("")` + `XmlDsigEnvelopedSignatureTransform`, `DigestMethod = sha1`, `KeyInfoX509Data(cert)`.
- Perfil de algoritmo en opciones (`PerfilFirma.Sha1` por defecto, `Sha256` preparado).
- Firma con `Id` configurable (`IdFirma`, por defecto `"PasheSign"`), prefijo `ds`, insertada en `ext:ExtensionContent` por XPath con `XmlNamespaceManager`. `cac:Signature` del documento coherente (ID, RUC, razón social, `#IdFirma`).
- Guardar con `PreserveWhitespace = true` y sin reindentar. Verificación local `CheckSignature(cert, true)` antes del ZIP.
- Valor resumen = `ds:DigestValue` (Base64).
- Certificado próximo a vencer (< 30 días, configurable) → advertencia en el resultado y en el log.

## 11. ZIP y nombres

- `ZipArchive` con una entrada en la raíz: `{nombre}.xml` dentro de `{nombre}.zip`; escritura atómica (temporal + `File.Move(overwrite: true)`).
- `NombreArchivoCpe`: `RUC-TT-SERIE-NUMERO` (número sin ceros a la izquierda), `RUC-RC-yyyyMMdd-n`, `RUC-RA-yyyyMMdd-n`, `RUC-09-T001-n`, `RUC-31-V001-n`; CDR `R-{nombre}.zip`.

## 12. QR

- `ConstructorContenidoQr` (01/03/07/08): `RUC|TIPO|SERIE|NUMERO|IGV|TOTAL|FECHA|TIPODOCADQ|NUMDOCADQ|VALORRESUMEN`, montos `0.00`, fecha `yyyy-MM-dd`; opciones: ceros del número y `|` final (por defecto no).
- GRE (09/31): contenido = enlace que SUNAT devuelve en el CDR **[verificar el nodo con un CDR real; se espera en `cac:DocumentResponse/cac:DocumentReference/cbc:DocumentDescription`]**.
- `GeneradorQrPng` (QRCoder): corrección Q, 20 px por módulo, zona de silencio de 4 módulos, PNG en `QR\yyyy\MM\{nombre}.png`. Tamaño impreso en el PDF A4: 3.5 × 3.5 cm.

## 13. Conectividad

`VerificadorConectividad`:
1. `NetworkInterface.GetIsNetworkAvailable()` → si `false`: `SinConexionInternet`.
2. DNS + conexión TCP al puerto 443 del host destino (timeout 3 s).
3. Si falla, prueba un host de control configurable (por defecto `www.gob.pe:443`): si el control responde → `ServicioNoDisponible`; si no → `SinConexionInternet`.
4. Resultado cacheado por destino 15 s (configurable); invalidado ante cualquier error de red en un envío.
No usa ping (ICMP suele estar bloqueado). Se puede desactivar por opción.

## 14. Transporte SOAP (SUNAT y OSE)

- `HttpClient` nombrado por canal vía `IHttpClientFactory`; SOAP 1.1, `Content-Type: text/xml; charset=utf-8`, `SOAPAction: ""` **[verificar por canal]**.
- Sobre construido con plantillas (raw string literals) y `wsse:UsernameToken` (`Username = RUC + usuario`, `Password` texto). Para OSE, usuario y clave los asigna el OSE.
- Operaciones: `sendBill(fileName, contentFile)`, `sendSummary(fileName, contentFile)`, `getStatus(ticket)`, `getStatusCdr(rucComprobante, tipoComprobante, serieComprobante, numeroComprobante)`.
- Respuestas parseadas con `XmlReader` (sin cargar DOM completo): `applicationResponse` (Base64 ZIP), `ticket`, `status/statusCode/content`, `statusCdr/statusCode/statusMessage/content`.
- `LectorFaultSoap`: `faultcode`/`faultstring`/`detail`; código = sufijo numérico del `faultcode` (`soap-env:Client.0111` → `0111`) o, si no, el número del `faultstring`; se enriquece con `errores-sunat.json`.
- Timeouts separados: conexión (`TimeoutConexionSegundos`, 10) y respuesta (`TimeoutRespuestaSegundos`, 60). Resiliencia (reintentos con espera exponencial) **solo** para `getStatus`, `getStatusCdr` y token GRE.
- Bloqueo por documento dentro del proceso (`SemaphoreSlim` por nombre de archivo) para impedir envíos simultáneos del mismo comprobante.

Endpoints por defecto (`endpoints.json`, sobrescribibles por emisor/canal):

| Canal | Ambiente | Servicio | URL |
|---|---|---|---|
| SUNAT | Beta | billService | `https://e-beta.sunat.gob.pe/ol-ti-itcpfegem-beta/billService` |
| SUNAT | Producción | billService | `https://e-factura.sunat.gob.pe/ol-ti-itcpfegem/billService` |
| SUNAT | Producción | billConsultService (`getStatusCdr`) | `https://e-factura.sunat.gob.pe/ol-it-wsconscpegem/billConsultService` **[verificar]** |
| OSE eFact | Producción | billService | `https://ose.efact.pe/ol-ti-itcpe/billService` **[verificar con el eFacturador]** |
| OSE eFact | Pruebas | billService | tomar del eFacturador **[pendiente]** |
| GRE | Producción | token | `https://api-seguridad.sunat.gob.pe/v1/clientessol/{client_id}/oauth2/token/` |
| GRE | Producción | API | `https://api-cpe.sunat.gob.pe/v1/contribuyente/gem/comprobantes` |

Notas: `getStatusCdr` no existe en beta → `ConsultarCdrAsync` en beta devuelve `ErrorServicio` "no soportado en beta". Si el OSE no ofrece `getStatusCdr`, el canal lo declara (`SoportaConsultaCdr = false`) y la reconciliación se hace con `getStatus` o consulta manual.

## 15. Lectura del CDR

`LectorCdr` (con `XmlReader` y `XmlNamespaceManager`): guarda `R-{nombre}.zip` íntegro en `RPTA\yyyy\MM\`; extrae en memoria; lee `cbc:ResponseDate`, `cbc:ResponseTime`, `cac:DocumentResponse/cac:Response/cbc:ResponseCode`, `cbc:Description`, `cbc:ReferenceID`, todas las `cbc:Note` (formato `"4252 - texto"` → `ObservacionCpe{Codigo, Descripcion}`), `cac:DocumentReference/cbc:ID` y, en GRE, el enlace del QR. Valida opcionalmente la firma del CDR.

## 16. Tickets

`ConsultorTicket` común a SOAP y GRE: espera progresiva configurable (por defecto 2, 4, 8, 15, 30 s; máximo 5 intentos por llamada a `EsperarTicketAsync`); `98` → `EnProceso` con ticket; `0` → CDR; `99` → errores. El ticket siempre queda en el historial para consultas posteriores (otro proceso u otro día).

## 17. GRE — API REST con OAuth2

1. **Token** (`ProveedorTokenGre`): `POST` form `grant_type=password`, `scope=https://api-cpe.sunat.gob.pe`, `client_id`, `client_secret`, `username={RUC}{UsuarioSOL}`, `password={ClaveSOL}`. Caché por `(client_id, username)` hasta `expires_in − 60 s`; renovación con bloqueo para evitar solicitudes simultáneas.
2. **Envío**: `POST {api}/{RUC-TT-SERIE-NUMERO}` con `{"archivo":{"nomArchivo":"{nombre}.zip","arcGreZip":"<Base64>","hashZip":"<SHA-256>"}}` → `numTicket`, `fecRecepcion`. Formato del hash: hexadecimal en minúsculas **[verificar contra el manual GRE]**.
3. **Consulta**: `GET {api}/envios/{numTicket}` → `codRespuesta` (0/98/99), `error{numError, desError}`, `arcCdr` (Base64), `indCdrGenerado`.
4. Errores HTTP: 401 → renovar token y reintentar una vez; 400/404/422 → `ErrorServicio` con detalle; 500/503 → `ServicioNoDisponible`.
5. Sin ambiente beta: la GRE **siempre** usa los endpoints de producción. Si `Ambiente = Beta`, la librería no envía GRE (devuelve `ErrorServicio` "GRE solo existe en producción") salvo que `Gre.PermitirEnvioConAmbienteBeta = true`. Es una protección contra emisiones reales accidentales durante pruebas. La demo pide confirmación explícita antes de enviar una GRE.

## 18. Almacenamiento e historial

- `AlmacenCpe` es el único componente que compone rutas (`Path.Combine`): `{RutaBase}\{CARPETA}\{yyyy}\{MM}\{archivo}`; `CERT\{archivo.pfx}` sin periodo. Crea carpetas bajo demanda. **Sin subcarpetas por día.**
- Escrituras atómicas (temporal en la misma carpeta + `File.Move`).
- `RegistroHistorial`: `LOG\yyyy\MM\{nombre}.json` con `{ referencia, estadoActual, ticket, intentos: [ { fecha, operacion, canal, endpoint, duracionMs, estado, codigo, descripcion, http, ticket } ] }`. Serialización con `JsonSerializerContext` generado. Nunca contiene credenciales. Acceso serializado por documento.

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
  "PerfilFirma": "Sha1",
  "ModoEnvioBoletas": "ResumenDiario",      // ResumenDiario | Individual
  "Validacion": { "Modelo": true, "Esquema": true, "XslSunat": true, "Reglas2027": true },
  "TimeoutConexionSegundos": 10,
  "TimeoutRespuestaSegundos": 60,
  "Conectividad": { "Verificar": true, "HostControl": "www.gob.pe", "CacheSegundos": 15 },
  "Tickets": { "EsperasSegundos": [2, 4, 8, 15, 30] },
  "Gre": { "PermitirEnvioConAmbienteBeta": false },
  "AvisoVencimientoCertificadoDias": 30,
  "PrecalentarAlIniciar": true,
  "GenerarPdfAutomaticamente": false,
  "Endpoints": { }                          // sobrescrituras opcionales por canal/ambiente
}
```

```csharp
builder.Services.AddPasheCpe(builder.Configuration.GetSection("PasheCPE"));  // valida opciones al iniciar (ValidateOnStart)
builder.Services.AddPasheCpePdf();
// Sin DI:
var cpe = PasheCpe.Crear(new OpcionesPasheCpe { RutaBase = @"D:\CPE", Ambiente = Ambiente.Beta });
```

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
