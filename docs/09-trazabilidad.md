# Trazabilidad

Cada requerimiento con dónde vive y qué lo verifica. Implementa el criterio **CE-07**.

## Cómo se construyó esta tabla

Las dos últimas columnas salieron de buscar cada identificador en el código y en las pruebas, no de la memoria de nadie. Eso tiene un límite que conviene decir de entrada: **que un identificador aparezca en un archivo no demuestra que esté probado**, y que no aparezca no demuestra que no lo esté.

Por eso el barrido automático fue el punto de partida y no la conclusión. Los requerimientos que no mencionaban su identificador en ninguna prueba se revisaron uno a uno; los que sí tenían cobertura se anotaron con el nombre de la prueba, y los que no la tenían aparecen marcados **sin verificación automática**, no rellenados con algo que sonara bien.

Ese repaso encontró tres huecos reales, y están en la sección final.

## Requerimientos funcionales

| ID | Requerimiento | Implementado en | Verificado por |
|---|---|---|---|
| `RF-01` | El sistema debe rechazar toda petición que no incluya una llave de API válida. | ManejadorAutenticacionLlaveApi | AutenticacionTests |
| `RF-02` | El sistema debe registrar, por cada petición autenticada, qué integrador la originó. | ExtensionesUsuario · ManejadorAutenticacionLlaveApi · MiddlewareCorrelacion · IRepositorioDocumentos | DocumentoTests |
| `RF-03` | El sistema debe permitir registrar y consultar los datos de la empresa emisora: identificaci… | EmisorController · GestionCatalogos | CatalogosEndpointTests |
| `RF-04` | El sistema debe permitir registrar el certificado de firma digital asociado al emisor. | IFirmadorXml · ProveedorCertificadoConfiguracion | FirmaXadesTests · FirmaEndpointTests |
| `RF-05` | El sistema debe impedir la emisión de documentos si el emisor no está completamente configur… | ContratosCatalogos · EmitirFacturaHandler | EmisionSinEmisorTests |
| `RF-06` | El sistema debe permitir registrar, consultar, modificar y desactivar adquirentes, con su ti… | AdquirentesController · GestionCatalogos | CatalogosEndpointTests |
| `RF-07` | El sistema debe permitir registrar, consultar, modificar y desactivar productos o servicios,… | ProductosController · GestionCatalogos | CatalogosEndpointTests |
| `RF-08` | El sistema debe permitir registrar rangos de numeración autorizados, con su prefijo, número … | RangosNumeracionController · GestionRangos | RangosNumeracionEndpointTests |
| `RF-09` | El sistema debe asignar a cada documento el siguiente número disponible del rango vigente co… | — | NumeracionConcurrenteTests · RangoNumeracionTests · FacturasEndpointTests |
| `RF-10` | El sistema debe informar cuántos números quedan disponibles y cuántos días faltan para el ve… | ContratosRangos · RangosNumeracionController · GestionRangos | RangoNumeracionTests · EmisionSinEmisorTests · NumeracionConcurrenteTests · RangosNumeracionEndpointTests |
| `RF-11` | El sistema debe permitir emitir una factura electrónica de venta a partir de un adquirente, … | FacturasController | FacturasEndpointTests · DocumentoTests |
| `RF-12` | El sistema debe permitir emitir una nota crédito asociada a una factura existente. | NotasController · EmitirNotaHandler | NotasEndpointTests |
| `RF-13` | El sistema debe permitir emitir una nota débito asociada a una factura existente. | NotasController · EmitirNotaHandler | NotasEndpointTests |
| `RF-14` | El sistema debe responder a toda solicitud de emisión con un identificador del documento y s… | FacturasController | FacturasEndpointTests |
| `RF-15` | El sistema debe aceptar una referencia externa única por solicitud, de modo que una solicitu… | ContratosNotas · EmitirFacturaSolicitud · FacturasController · IRepositorioDocumentos · ComandoEmitirFactura · EmitirFacturaHandler · EmitirNotaHandler · Documento · ConfiguracionDocumento | DocumentoTests · FacturasEndpointTests · NotasEndpointTests |
| `RF-16` | El sistema debe generar, para cada documento recibido, un archivo XML conforme al estándar U… | DocumentosController · IGeneradorXml · GenerarXmlHandler · Documento · GeneradorXmlUbl | XmlEndpointTests |
| `RF-17` | El sistema debe firmar digitalmente el XML generado con el certificado del emisor. | DocumentosController · IFirmadorXml · FirmarDocumentoHandler · Documento · FirmadorXadesEpes | FirmaEndpointTests · FirmaXadesTests |
| `RF-18` | El sistema debe transmitir el documento firmado al servicio de validación y registrar el ide… | RespuestaDocumento · IProveedorValidacion · Documento · Transmision · ConfiguracionDocumento · ProveedorValidacionHttp | CicloCompletoTests · ProveedorValidacionHttpTests |
| `RF-19` | El sistema debe consultar el resultado de la validación hasta obtener un veredicto definitiv… | IProveedorValidacion · Documento · ProveedorValidacionHttp | ProveedorValidacionHttpTests |
| `RF-20` | El sistema debe calcular el código único de identificación del documento (CUFE) e incluirlo … | RespuestaDocumento | CodigoUnicoTests · GeneracionXmlTests |
| `RF-21` | El sistema debe registrar, cuando un documento es rechazado, la lista de errores devueltos p… | RespuestaDocumento · Documento · ConfiguracionDocumento | CicloCompletoTests |
| `RF-22` | El sistema debe permitir consultar un documento por su identificador, devolviendo su estado … | DocumentosController · ConsultarDocumentoHandler | FacturasEndpointTests |
| `RF-23` | El sistema debe permitir consultar el historial completo de estados de un documento, con la … | ContratosNotas · DocumentosController · Documento · TransicionEstado · ConfiguracionDocumento | TransicionesTests · NotasEndpointTests |
| `RF-24` | El sistema debe permitir listar documentos filtrando por tipo, estado y rango de fechas, con… | RespuestaDocumento · DocumentosController · IRepositorioDocumentos · ConsultarDocumentoHandler · RepositorioDocumentos | ResumenDocumentoTests · ListadoDocumentosTests |
| `RF-25` | El sistema debe permitir descargar el XML firmado de un documento. | DocumentosController · Documento | FirmaEndpointTests · XmlEndpointTests |

## Reglas de negocio

| ID | Requerimiento | Implementado en | Verificado por |
|---|---|---|---|
| `RN-01` | Un número de documento se asigna una sola vez. Dos documentos no pueden compartir número den… | IRepositorioRangos · RangoNumeracion · ConfiguracionDocumento | NumeracionConcurrenteTests |
| `RN-02` | Un documento solo puede emitirse si existe un rango vigente, no agotado y no vencido, para s… | — | FacturasEndpointTests · RangosNumeracionEndpointTests |
| `RN-03` | Una nota crédito o débito debe referenciar una factura que exista en el sistema y se encuent… | EndpointsDesarrollo · ContratosNotas · EmitirNotaHandler · Documento | NotasTests · NotasEndpointTests |
| `RN-04` | La suma de las notas crédito asociadas a una factura no puede superar el valor total de esa … | IRepositorioDocumentos · EmitirNotaHandler · Documento · ConfiguracionDocumento · RepositorioDocumentos | FabricaDocumentos · NotasTests · NotasConcurrentesTests · NotasEndpointTests |
| `RN-05` | Una nota crédito o débito no puede referenciar a otra nota crédito o débito. | ContratosNotas · EmitirNotaHandler · Documento | NotasTests · NotasEndpointTests |
| `RN-06` | El impuesto se calcula sobre la base gravable de cada línea aplicando la tarifa del producto… | Dinero · ImpuestoLinea · Totales · ConfiguracionDocumento | DocumentoTests · FacturasEndpointTests |
| `RN-07` | Un documento rechazado por la autoridad no puede corregirse ni retransmitirse. La corrección… | RespuestaDocumento · Documento | TransicionesTests |
| `RN-08` | Un documento debe tener al menos una línea de detalle, y toda línea debe tener cantidad y pr… | Documento · Linea | DocumentoTests · LineaTests |
| `RN-09` | El total del documento es la suma de las bases gravables más la suma de los impuestos, menos… | Documento | DocumentoTests |
| `RN-10` | Una factura aprobada es inmutable. Ninguno de sus datos puede modificarse después de la apro… | EmitirFacturaSolicitud · RespuestaDocumento · AdquirentesController · ProductosController · GestionCatalogos · ComandoEmitirFactura · ConstructorLineas · EmitirFacturaHandler · EmitirNotaHandler · Adquirente · DatosTributarios · Documento · Emisor · Producto · ConfiguracionDocumento | DatosTributariosTests · DocumentoTests · CatalogosEndpointTests · FacturasEndpointTests |
| `RN-11` |  | TransicionarDocumentoHandler · ProcesadorTareas · Documento · MaquinaEstados | TransicionesTests · NotasEndpointTests |
| `RN-12` |  | TransicionarDocumentoHandler · Documento · TransicionEstado · ConfiguracionDocumento | TransicionesTests |
| `RN-13` |  | OpcionesSalida · ProcesadorTareas · Program · Documento · EstadoDocumento · Transmision · TareaSalida · ConfiguracionDocumento · RepositorioDocumentos · ProveedorValidacionHttp | CicloCompletoTests · ProveedorValidacionHttpTests · ProveedorValidacionSimulado |

## Requerimientos no funcionales

| ID | Requerimiento | Implementado en | Verificado por |
|---|---|---|---|
| `RNF-01` | Los secretos —llaves de API, certificado, credenciales de base de datos— no pueden estar en … | Program · EmitirFacturaHandler · FabricaDbContextDisenio · ProveedorCertificadoConfiguracion | FabricaApiConBaseDeDatos · FirmaXadesTests |
| `RNF-02` | El acceso al servicio de validación de la autoridad debe estar detrás de una abstracción que… | IProveedorValidacion | ProveedorValidacionHttpTests · CicloCompletoTests |
| `RNF-03` | La solicitud de emisión debe responder en menos de 500 ms en el percentil 95, medido sin inc… | — | **sin verificación automática** |
| `RNF-04` | El procesamiento de documentos debe continuar aunque el servicio de validación esté caído, e… | TrabajadorSalida · IRepositorioTareas · OpcionesSalida · Program · TareaSalida · RepositorioTareas | TareaSalidaTests · BandejaDeSalidaTests · CicloCompletoTests · ProveedorValidacionSimulado |
| `RNF-05` | Los reintentos ante fallas transitorias deben usar espera creciente entre intentos, con un n… | OpcionesSalida · Program · Documento · Transmision · TareaSalida | TareaSalidaTests · BandejaDeSalidaTests · CicloCompletoTests · ProveedorValidacionSimulado |
| `RNF-06` | La asignación de números debe ser correcta bajo concurrencia. | — | NumeracionConcurrenteTests |
| `RNF-07` | La API debe estar documentada con OpenAPI, generado desde el código y no mantenido aparte. | Program | ContratoGeneradoTests |
| `RNF-08` | El sistema completo debe levantarse en una máquina limpia con un solo comando. | Program | **sin verificación automática** |
| `RNF-09` | Las reglas de negocio de la sección 5 deben tener cobertura de pruebas automatizadas. | — | (meta) todas las RN de abajo |
| `RNF-10` | Los registros de actividad deben ser estructurados e incluir un identificador de correlación… | MiddlewareCorrelacion | CatalogosEndpointTests |
| `RNF-11` | Los mensajes de error de la API deben indicar qué está mal y qué debe corregirse, sin expone… | ManejadorAutenticacionLlaveApi · ManejadorExcepcionNoPrevista · Program | AutenticacionTests |
| `RNF-12` | El sistema debe manejar valores monetarios con un tipo de dato de precisión decimal exacta. | Dinero | DineroTests |

---

## Lo que no está verificado

Tres requerimientos no tienen verificación automática. Se declaran en vez de maquillarse.

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

## Lo que sí quedó cubierto en este hito

La revisión destapó cuatro requerimientos "Debe" que la suite no verificaba, pese a estar todo verde:

- **RF-03, RF-06, RF-07** — emisor, adquirentes y productos se usaban en casi todas las pruebas, pero siempre para lo mismo: crear uno y poder emitir. Consultar, modificar y desactivar no los probaba nadie. Los cubre `CatalogosEndpointTests`.
- **RF-05** — impedir la emisión sin emisor configurado. Lo cubre `EmisionSinEmisorTests`, en su propia clase porque necesita una base de datos sin emisor.
- **RNF-10** — el identificador de correlación en las respuestas de error. Lo cubre `CatalogosEndpointTests`.

Merece quedar escrito porque el modo de fallo es instructivo: **un requerimiento cubierto de rebote no está cubierto**. La suite crecía hito a hito y el recuento subía, mientras tres verbos de tres requerimientos quedaban fuera. No se manifestaba como un fallo, sino como una ausencia, y las ausencias no se ven mirando el color de la suite.
