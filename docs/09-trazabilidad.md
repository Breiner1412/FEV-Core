# Trazabilidad

Cada requerimiento con dónde vive y qué lo verifica. Implementa el criterio **CE-07**.

## Cómo se construyó esta tabla

Las dos últimas columnas salieron de buscar cada identificador en el código y en las pruebas, no de la memoria de nadie. Eso tiene un límite que conviene decir de entrada: **que un identificador aparezca en un archivo no demuestra que esté probado**, y que no aparezca no demuestra que no lo esté.

Por eso el barrido automático fue el punto de partida y no la conclusión. Los requerimientos que no mencionaban su identificador en ninguna prueba se revisaron uno a uno; los que sí tenían cobertura se anotaron con el nombre de la prueba, y los que no la tenían aparecen marcados **sin verificación automática**, no rellenados con algo que sonara bien.

Ese repaso encontró tres huecos reales, y están en la sección final.

> **Corrección de la auditoría final.** Esta tabla afirmaba cobertura que no existía, que es exactamente lo que el párrafo anterior dice que no hace. RNF-10 figuraba como verificado y no se cumplía; RF-11 figuraba como verificado y la forma de pago nunca se implementó; tres reglas tenían la descripción vacía y nadie lo vio. Está todo en la sección *Lo que esta tabla afirmaba y no era cierto*, al final. Se deja escrito en vez de corregirse en silencio, porque la tabla se presentó como la prueba de que no se maquillaba nada.

## Requerimientos funcionales

| ID | Requerimiento | Implementado en | Verificado por |
|---|---|---|---|
| `RF-01` | El sistema debe rechazar toda petición que no incluya una llave de API válida. | ManejadorAutenticacionLlaveApi | AutenticacionTests |
| `RF-02` | El sistema debe registrar, por cada petición autenticada, qué integrador la originó. | ExtensionesUsuario · ManejadorAutenticacionLlaveApi · MiddlewareCorrelacion · IRepositorioDocumentos | DocumentoTests |
| `RF-03` | El sistema debe permitir registrar y consultar los datos de la empresa emisora: identificaci… | EmisorController · GestionCatalogos | CatalogosEndpointTests |
| `RF-04` | El sistema debe permitir registrar el certificado de firma digital asociado al emisor. | IFirmadorXml · ProveedorCertificadoConfiguracion | FirmaXadesTests · FirmaEndpointTests |
| `RF-05` | El sistema debe impedir la emisión de documentos si el emisor no está completamente configur… | ContratosCatalogos · EmitirFacturaHandler | EmisionSinEmisorTests |
| `RF-06` | El sistema debe permitir registrar, consultar, modificar y desactivar adquirentes, con su ti… | AdquirentesController · GestionCatalogos | CatalogosEndpointTests · UnicidadCatalogosTests |
| `RF-07` | El sistema debe permitir registrar, consultar, modificar y desactivar productos o servicios,… | ProductosController · GestionCatalogos | CatalogosEndpointTests · UnicidadCatalogosTests |
| `RF-08` | El sistema debe permitir registrar rangos de numeración autorizados, con su prefijo, número … | RangosNumeracionController · GestionRangos | RangosNumeracionEndpointTests |
| `RF-09` | El sistema debe asignar a cada documento el siguiente número disponible del rango vigente co… | — | NumeracionConcurrenteTests · RangoNumeracionTests · FacturasEndpointTests |
| `RF-10` | El sistema debe informar cuántos números quedan disponibles y cuántos días faltan para el ve… | ContratosRangos · RangosNumeracionController · GestionRangos | RangoNumeracionTests · EmisionSinEmisorTests · NumeracionConcurrenteTests · RangosNumeracionEndpointTests · FechaColombianaTests |
| `RF-11` | El sistema debe permitir emitir una factura electrónica de venta a partir de un adquirente, … | FacturasController | FacturasEndpointTests · DocumentoTests — **parcial: la forma de pago no está implementada** |
| `RF-12` | El sistema debe permitir emitir una nota crédito asociada a una factura existente. | NotasController · EmitirNotaHandler | NotasEndpointTests |
| `RF-13` | El sistema debe permitir emitir una nota débito asociada a una factura existente. | NotasController · EmitirNotaHandler | NotasEndpointTests |
| `RF-14` | El sistema debe responder a toda solicitud de emisión con un identificador del documento y s… | FacturasController | FacturasEndpointTests |
| `RF-15` | El sistema debe aceptar una referencia externa única por solicitud, de modo que una solicitu… | ContratosNotas · EmitirFacturaSolicitud · FacturasController · IRepositorioDocumentos · ComandoEmitirFactura · EmitirFacturaHandler · EmitirNotaHandler · Documento · ConfiguracionDocumento | DocumentoTests · FacturasEndpointTests · NotasEndpointTests · ReintentosSimultaneosTests |
| `RF-16` | El sistema debe generar, para cada documento recibido, un archivo XML conforme al estándar U… | DocumentosController · IGeneradorXml · GenerarXmlHandler · Documento · GeneradorXmlUbl | XmlEndpointTests · EndpointsSoloDesarrolloTests |
| `RF-17` | El sistema debe firmar digitalmente el XML generado con el certificado del emisor. | DocumentosController · IFirmadorXml · FirmarDocumentoHandler · Documento · FirmadorXadesEpes | FirmaEndpointTests · FirmaXadesTests · EndpointsSoloDesarrolloTests |
| `RF-18` | El sistema debe transmitir el documento firmado al servicio de validación y registrar el ide… | RespuestaDocumento · IProveedorValidacion · Documento · Transmision · ConfiguracionDocumento · ProveedorValidacionHttp | CicloCompletoTests · ProveedorValidacionHttpTests |
| `RF-19` | El sistema debe consultar el resultado de la validación hasta obtener un veredicto definitiv… | IProveedorValidacion · Documento · ProveedorValidacionHttp | ProveedorValidacionHttpTests |
| `RF-20` | El sistema debe calcular el código único de identificación del documento (CUFE) e incluirlo … | RespuestaDocumento | CodigoUnicoTests · GeneracionXmlTests · RangoDelDocumentoTests |
| `RF-21` | El sistema debe registrar, cuando un documento es rechazado, la lista de errores devueltos p… | RespuestaDocumento · Documento · ConfiguracionDocumento | CicloCompletoTests |
| `RF-22` | El sistema debe permitir consultar un documento por su identificador, devolviendo su estado … | DocumentosController · ConsultarDocumentoHandler | FacturasEndpointTests |
| `RF-23` | El sistema debe permitir consultar el historial completo de estados de un documento, con la … | ContratosNotas · DocumentosController · Documento · TransicionEstado · ConfiguracionDocumento | TransicionesTests · NotasEndpointTests |
| `RF-24` | El sistema debe permitir listar documentos filtrando por tipo, estado y rango de fechas, con… | RespuestaDocumento · DocumentosController · IRepositorioDocumentos · ConsultarDocumentoHandler · RepositorioDocumentos | ResumenDocumentoTests · ListadoDocumentosTests — **el máximo por página es fijo (100), no configurable** |
| `RF-25` | El sistema debe permitir descargar el XML firmado de un documento. | DocumentosController · Documento | FirmaEndpointTests · XmlEndpointTests |

## Reglas de negocio

| ID | Requerimiento | Implementado en | Verificado por |
|---|---|---|---|
| `RN-01` | Un número de documento se asigna una sola vez. Dos documentos no pueden compartir número den… | IRepositorioRangos · RangoNumeracion · ConfiguracionDocumento | NumeracionConcurrenteTests |
| `RN-02` | Un documento solo puede emitirse si existe un rango vigente, no agotado y no vencido, para s… | — | FacturasEndpointTests · RangosNumeracionEndpointTests · FechaColombianaTests · HoraColombiaTests |
| `RN-03` | Una nota crédito o débito debe referenciar una factura que exista en el sistema y se encuent… | EndpointsDesarrollo · ContratosNotas · EmitirNotaHandler · Documento | NotasTests · NotasEndpointTests |
| `RN-04` | La suma de las notas crédito asociadas a una factura no puede superar el valor total de esa … | IRepositorioDocumentos · EmitirNotaHandler · Documento · ConfiguracionDocumento · RepositorioDocumentos | FabricaDocumentos · NotasTests · NotasConcurrentesTests · NotasEndpointTests |
| `RN-05` | Una nota crédito o débito no puede referenciar a otra nota crédito o débito. | ContratosNotas · EmitirNotaHandler · Documento | NotasTests · NotasEndpointTests |
| `RN-06` | El impuesto se calcula sobre la base gravable de cada línea aplicando la tarifa del producto… | Dinero · ImpuestoLinea · Totales · ConfiguracionDocumento | DocumentoTests · FacturasEndpointTests · GeneracionXmlTests |
| `RN-07` | Un documento rechazado por la autoridad no puede corregirse ni retransmitirse. La corrección… | RespuestaDocumento · Documento | TransicionesTests |
| `RN-08` | Un documento debe tener al menos una línea de detalle, y toda línea debe tener cantidad y pr… | Documento · Linea | DocumentoTests · LineaTests |
| `RN-09` | El total del documento es la suma de las bases gravables más la suma de los impuestos, menos… | Documento | DocumentoTests · GeneracionXmlTests |
| `RN-10` | Una factura aprobada es inmutable. Ninguno de sus datos puede modificarse después de la apro… | EmitirFacturaSolicitud · RespuestaDocumento · AdquirentesController · ProductosController · GestionCatalogos · ComandoEmitirFactura · ConstructorLineas · EmitirFacturaHandler · EmitirNotaHandler · Adquirente · DatosTributarios · Documento · Emisor · Producto · ConfiguracionDocumento | DatosTributariosTests · DocumentoTests · CatalogosEndpointTests · FacturasEndpointTests |
| `RN-11` | Un documento en estado terminal no cambia de estado nunca más. | TransicionarDocumentoHandler · ProcesadorTareas · Documento · MaquinaEstados | TransicionesTests · NotasEndpointTests · FalloAlGenerarTests |
| `RN-12` | Toda transición queda registrada con su marca de tiempo y el motivo que la produjo. | TransicionarDocumentoHandler · Documento · TransicionEstado · ConfiguracionDocumento | TransicionesTests |
| `RN-13` | Un documento en `FALLIDO` no implica que la autoridad no lo haya recibido. El historial dice qué consta; solo el resultado desconocido exige verificar ante la DIAN (ADR-0015). | OpcionesSalida · ProcesadorTareas · Program · Documento · EstadoDocumento · Transmision · TareaSalida · ConfiguracionDocumento · RepositorioDocumentos · ProveedorValidacionHttp | CicloCompletoTests · ProveedorValidacionHttpTests · ProveedorValidacionSimulado · DesenlaceFallidoTests · FalloAlGenerarTests |

## Requerimientos no funcionales

| ID | Requerimiento | Implementado en | Verificado por |
|---|---|---|---|
| `RNF-01` | Ningún secreto real en el repositorio; las credenciales de desarrollo publicadas se nombran como tales y solo existen fuera de producción. … | Program · FabricaDbContextDisenio · ProveedorCertificadoConfiguracion | FabricaApiConBaseDeDatos · FirmaXadesTests |
| `RNF-02` | El acceso al servicio de validación de la autoridad debe estar detrás de una abstracción que… | IProveedorValidacion | ProveedorValidacionHttpTests · CicloCompletoTests |
| `RNF-03` | La solicitud de emisión debe responder en menos de 500 ms en el percentil 95, medido sin inc… | — | **sin verificación automática** |
| `RNF-04` | El procesamiento de documentos debe continuar aunque el servicio de validación esté caído, e… | TrabajadorSalida · IRepositorioTareas · OpcionesSalida · Program · TareaSalida · RepositorioTareas | TareaSalidaTests · BandejaDeSalidaTests · CicloCompletoTests · ProveedorValidacionSimulado |
| `RNF-05` | Los reintentos ante fallas transitorias deben usar espera creciente entre intentos, con un n… | OpcionesSalida · Program · Documento · Transmision · TareaSalida | TareaSalidaTests · BandejaDeSalidaTests · CicloCompletoTests · ProveedorValidacionSimulado · DocumentoIlegibleTests · FalloAlRendirseTests |
| `RNF-06` | La asignación de números debe ser correcta bajo concurrencia. | — | NumeracionConcurrenteTests |
| `RNF-07` | La API debe estar documentada con OpenAPI, generado desde el código y no mantenido aparte. | Program | ContratoGeneradoTests |
| `RNF-08` | El sistema completo debe levantarse en una máquina limpia con un solo comando. | Program | **sin verificación automática** |
| `RNF-09` | Las reglas de negocio de la sección 5 deben tener cobertura de pruebas automatizadas. | — | (meta) todas las RN de abajo |
| `RNF-10` | Los registros de actividad deben ser estructurados e incluir un identificador de correlación… | MiddlewareCorrelacion · ProcesadorTareas · EmisionIdempotente · Program | RegistrosPorDocumentoTests · CatalogosEndpointTests *(solo el traceId de los errores)* |
| `RNF-11` | Los mensajes de error de la API deben indicar qué está mal y qué debe corregirse, sin expone… | ManejadorAutenticacionLlaveApi · ManejadorExcepcionNoPrevista · Program | AutenticacionTests |
| `RNF-12` | El sistema debe manejar valores monetarios con un tipo de dato de precisión decimal exacta. | Dinero | DineroTests |

---

## Lo que no está verificado

Tres requerimientos no tienen verificación automática. Se declaran en vez de maquillarse.

Son huecos de **verificación**: se cree que se cumplen y falta la prueba que lo demuestre. Los requerimientos que **no se cumplen del todo** son otra cosa y van en la sección siguiente.

### RF-04 — Registrar el certificado de firma

El enunciado dice *"permitir registrar el certificado de firma digital asociado al emisor"*, y sugiere un endpoint. No lo hay: el certificado entra por configuración, en base 64.

Es una desviación deliberada, y la razón es RNF-01. Un endpoint para subir un `.p12` significa recibir una clave privada por HTTP, guardarla en algún sitio y tener que protegerla ahí; por configuración, la clave privada nunca pasa por la aplicación como dato de negocio y nunca se acerca al repositorio.

La capacidad existe —se puede cambiar el certificado sin recompilar— pero por otro camino. `FirmaXadesTests` y `FirmaEndpointTests` cubren que un certificado configurado firma y que uno vencido no.

### RNF-03 — Menos de 500 ms en el percentil 95

No se ha medido. Nunca. No hay prueba de carga, ni una medición puntual.

El requerimiento está marcado como "Debería" y la arquitectura lo tiene en cuenta —la API responde `202` sin esperar a la autoridad, que era el riesgo real—, pero eso es un argumento, no un número. Decir que se cumple porque el diseño lo favorece sería exactamente el tipo de afirmación que este documento existe para evitar.

### RNF-08 — Levantarse con un solo comando

Se comprueba a mano, y funciona: `docker compose up --build`. No está automatizado, y cualquier arreglo que rompa el arranque en una máquina limpia no lo detectaría el CI, porque el CI ya tiene el proyecto compilado.

Verificarlo de verdad es la demostración de **CE-01**: una persona ajena al proyecto, con solo el README, levanta el sistema y emite una factura. Eso no lo puede firmar quien escribió el README.

---

## Lo que no está completo

Dos requerimientos están implementados solo en parte. No es que falte probarlos: falta una parte de lo que piden.

### RF-11 — La forma de pago

RF-11 pide emitir una factura "a partir de un adquirente, una lista de líneas de detalle y una forma de pago". La forma de pago nunca entró en el modelo de dominio, así que no se recibe, no se guarda y el XML no la declara (`cac:PaymentMeans`). `07-cobertura-ubl.md` lo registró en la etapa 5 como inconsistencia pendiente de decidir; esta tabla, en cambio, daba RF-11 por verificado. Sigue pendiente decidir si se añade al dominio o se retira del requerimiento.

### RF-24 — El máximo por página

El criterio de aceptación dice "no devuelve más de un máximo configurable por página". El máximo existe y se respeta, pero es una constante (`ListarDocumentosHandler.TamanoPaginaMaximo`, 100), no una configuración.

---

## Lo que sí quedó cubierto en este hito

La revisión destapó cuatro requerimientos "Debe" que la suite no verificaba, pese a estar todo verde:

- **RF-03, RF-06, RF-07** — emisor, adquirentes y productos se usaban en casi todas las pruebas, pero siempre para lo mismo: crear uno y poder emitir. Consultar, modificar y desactivar no los probaba nadie. Los cubre `CatalogosEndpointTests`.
- **RF-05** — impedir la emisión sin emisor configurado. Lo cubre `EmisionSinEmisorTests`, en su propia clase porque necesita una base de datos sin emisor.
- **RNF-10** — el identificador de correlación en las respuestas de error. Lo cubre `CatalogosEndpointTests`. *(Falso como cobertura de RNF-10: ver la sección siguiente.)*

Merece quedar escrito porque el modo de fallo es instructivo: **un requerimiento cubierto de rebote no está cubierto**. La suite crecía hito a hito y el recuento subía, mientras tres verbos de tres requerimientos quedaban fuera. No se manifestaba como un fallo, sino como una ausencia, y las ausencias no se ven mirando el color de la suite.

---

## Lo que esta tabla afirmaba y no era cierto

La auditoría final contrastó esta tabla con el código, y encontró que afirmaba cobertura que no existía. Se corrigió, y esto es lo que decía:

| Qué decía | Qué pasaba de verdad | Ahora |
|---|---|---|
| **RNF-10** verificado por `CatalogosEndpointTests`. | Esa prueba solo comprueba el `traceId` en las respuestas de error. El requerimiento pide seguir **un documento** por sus registros, y ningún registro llevaba el documento. Además, la consola escribía texto y descartaba los *scopes*: ni siquiera el `traceId` llegaba a la salida. | Implementado y verificado: `RegistrosPorDocumentoTests`. |
| **RF-11** verificado. | La forma de pago no existe en el modelo. `07-cobertura-ubl.md` lo decía desde la etapa 5. | Marcado como parcial (sección *Lo que no está completo*). |
| **RF-24** verificado. | El máximo por página es fijo, no configurable. | Marcado como parcial. |
| **RN-11, RN-12, RN-13** con la descripción vacía. | Viven en la sección 6.3 de los requerimientos, no en la tabla de la 5, y el barrido automático solo leyó la tabla. Nadie revisó la salida. | Completadas. |
| **RNF-01** implementado, entre otros, en `EmitirFacturaHandler`. | El barrido lo encontró porque un comentario de ese handler citaba RNF-01 al hablar de rendimiento. RNF-01 es sobre secretos. | Quitado de la fila; el comentario se corrigió. |

El modo de fallo es el mismo que esta tabla describe en *Lo que sí quedó cubierto*, solo que aplicado a la tabla misma. **Que un identificador aparezca junto a una prueba no demuestra que la prueba verifique lo que el requerimiento pide.** Para RNF-10 había una prueba, tenía el identificador correcto y pasaba en verde; verificaba otra cosa.

### Cobertura nueva de la auditoría final

Las pruebas añadidas en la auditoría ya figuran en la tabla. Las que cubren reglas que antes solo se probaban de rebote, o no se probaban:

- **RF-15** bajo concurrencia: `ReintentosSimultaneosTests`.
- **RF-20**, el CUFE con la clave del rango que numeró el documento: `RangoDelDocumentoTests`.
- **RN-02** con la fecha colombiana, para facturas, notas y RF-10: `FechaColombianaTests`, `HoraColombiaTests`.
- **RN-06 y RN-09** entre el XML y el CUFE: `GeneracionXmlTests` (ADR-0016).
- **RN-13**, los cuatro desenlaces de un `FALLIDO`: `DesenlaceFallidoTests` y el texto del historial en las pruebas del ciclo (ADR-0015).
- **RNF-05**, el tope de intentos aunque el fallo sea permanente: `DocumentoIlegibleTests`, `FalloAlRendirseTests`.
- **RF-06 y RF-07**, la unicidad bajo concurrencia: `UnicidadCatalogosTests`.

