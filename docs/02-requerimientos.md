# Documento de Requerimientos

**Proyecto:** FEV-Core — API de emisión de documentos electrónicos (Colombia)
**Versión:** 1.0
**Fecha:** 24 de septiembre de 2026
**Autor:** Breiner Stiven Guisao Rodríguez
**Documento previo:** `01-vision-alcance.md` v1.0
**Estado:** Aprobado para pasar a modelado de dominio

---

## 1. Propósito

Convertir las capacidades descritas en la sección 5 del documento de visión en requerimientos individuales, numerados y verificables. Cada requerimiento de este documento debe poder responderse con un sí o un no al terminar el proyecto.

Este documento **no describe soluciones técnicas**. No dice qué base de datos, qué librería ni cómo se organizan las clases. Eso corresponde a la etapa 4. Aquí se define *qué* debe hacer el sistema, no *cómo*.

---

## 2. Convenciones

### 2.1 Tipos de enunciado

| Código | Tipo | Qué es |
|---|---|---|
| **RF-nn** | Requerimiento funcional | Algo que el sistema debe hacer. |
| **RN-nn** | Regla de negocio | Una restricción del dominio que el sistema debe hacer cumplir. Existe aunque el software no exista. |
| **RNF-nn** | Requerimiento no funcional | Una cualidad del sistema: seguridad, rendimiento, mantenibilidad. |

La distinción entre RF y RN importa. "El sistema debe permitir emitir notas crédito" es funcional. "Una nota crédito no puede superar el valor de la factura que corrige" es una regla del negocio: sería cierta aunque se facturara en papel. Separarlas hace que las reglas se puedan probar de forma independiente y que se noten cuando cambian.

### 2.2 Prioridad

| Nivel | Significado |
|---|---|
| **Debe** | Sin esto la versión 1 no existe. |
| **Debería** | Importante, pero la versión 1 puede entregarse sin ello. |
| **Podría** | Deseable. Se implementa solo si sobra tiempo. |

### 2.3 Lectura de un requerimiento

Cada requerimiento incluye su criterio de aceptación: la condición concreta que permite declararlo cumplido. Si un requerimiento no tiene criterio de aceptación verificable, está mal escrito.

---

## 3. Decisiones de diseño que originan requerimientos

Tres decisiones tomadas antes de redactar este documento condicionan varios requerimientos. Se registran aquí para que su origen sea rastreable.

### 3.1 Emisión asíncrona

**Problema:** la validación ante la DIAN tiene una latencia típica de 2 a 8 segundos y puede fallar. Una API que mantenga la conexión abierta durante esa espera acumula conexiones bloqueadas y, ante un corte de red, deja el documento en estado indeterminado: no se sabe si la autoridad lo recibió.

**Decisión:** la recepción del documento y su validación ante la DIAN son operaciones separadas. La API acepta el documento, lo persiste y responde de inmediato con un identificador. Un proceso en segundo plano se encarga de generar, firmar, transmitir y consultar el resultado.

**Consecuencia:** origina RF-08, RF-14, RF-15, RF-16, RNF-04 y RNF-05.

### 3.2 Consulta de estado por parte del integrador

**Decisión:** el integrador conoce el resultado consultando el estado del documento. El sistema no notifica hacia afuera en la versión 1.

**Razón:** no exige que el integrador exponga un servidor accesible desde internet, y evita tener que resolver reintentos de entrega, firmas de notificación y direcciones caídas. La notificación saliente queda registrada como candidata a la versión 2.

**Consecuencia:** origina RF-17 y RF-18.

### 3.3 Autenticación por llave de API

**Decisión:** cada sistema integrador se identifica con una llave propia enviada en cada petición.

**Razón:** la comunicación es entre dos sistemas, no hay una persona iniciando sesión. Un esquema de tokens con expiración está diseñado para sesiones de usuario y agregaría un flujo de renovación que ningún actor de este sistema necesita.

**Consecuencia:** origina RF-01, RF-02 y RNF-01.

---

## 4. Requerimientos funcionales

### 4.1 Acceso y seguridad

| ID | Requerimiento | Prioridad |
|---|---|---|
| **RF-01** | El sistema debe rechazar toda petición que no incluya una llave de API válida. | Debe |
| **RF-02** | El sistema debe registrar, por cada petición autenticada, qué integrador la originó. | Debe |

**Criterios de aceptación**
- RF-01: una petición sin llave, o con una llave inexistente o desactivada, recibe respuesta `401` y no produce ningún efecto sobre los datos.
- RF-02: dado un documento cualquiera, es posible determinar qué integrador lo emitió.

---

### 4.2 Configuración del emisor

| ID | Requerimiento | Prioridad |
|---|---|---|
| **RF-03** | El sistema debe permitir registrar y consultar los datos de la empresa emisora: identificación tributaria, razón social, dirección, municipio, responsabilidades tributarias y régimen. | Debe |
| **RF-04** | El sistema debe permitir registrar el certificado de firma digital asociado al emisor. | Debe |
| **RF-05** | El sistema debe impedir la emisión de documentos si el emisor no está completamente configurado. | Debe |

**Criterios de aceptación**
- RF-03: los datos registrados aparecen íntegros en el XML generado.
- RF-04: el certificado se almacena de forma que no queda expuesto en respuestas de la API ni en registros de actividad.
- RF-05: un intento de emisión con emisor incompleto recibe `409` con el detalle de los campos faltantes, y no consume número.

---

### 4.3 Adquirentes y productos

| ID | Requerimiento | Prioridad |
|---|---|---|
| **RF-06** | El sistema debe permitir registrar, consultar, modificar y desactivar adquirentes, con su tipo y número de identificación, nombre o razón social, dirección y correo. | Debe |
| **RF-07** | El sistema debe permitir registrar, consultar, modificar y desactivar productos o servicios, con su código, descripción, unidad de medida, precio unitario y tarifa de impuesto aplicable. | Debe |

**Criterios de aceptación**
- RF-06: no se permiten dos adquirentes activos con la misma combinación de tipo y número de identificación.
- RF-07: la tarifa de impuesto es un dato del producto. El sistema no la asume fija en el código.

---

### 4.4 Rangos de numeración

| ID | Requerimiento | Prioridad |
|---|---|---|
| **RF-08** | El sistema debe permitir registrar rangos de numeración autorizados, con su prefijo, número inicial, número final, fecha de inicio y fecha de vencimiento. | Debe |
| **RF-09** | El sistema debe asignar a cada documento el siguiente número disponible del rango vigente correspondiente a su tipo. | Debe |
| **RF-10** | El sistema debe informar cuántos números quedan disponibles y cuántos días faltan para el vencimiento de cada rango. | Debería |

**Criterios de aceptación**
- RF-08: no se aceptan rangos cuyo número final sea menor o igual al inicial, ni rangos que se solapen con otro rango activo del mismo prefijo.
- RF-09: verificado bajo emisión concurrente. Ver RN-01 y RNF-06.
- RF-10: la consulta devuelve números disponibles y días restantes por rango.

---

### 4.5 Emisión de documentos

| ID | Requerimiento | Prioridad |
|---|---|---|
| **RF-11** | El sistema debe permitir emitir una factura electrónica de venta a partir de un adquirente, una lista de líneas de detalle y una forma de pago. | Debe |
| **RF-12** | El sistema debe permitir emitir una nota crédito asociada a una factura existente. | Debe |
| **RF-13** | El sistema debe permitir emitir una nota débito asociada a una factura existente. | Debe |
| **RF-14** | El sistema debe responder a toda solicitud de emisión con un identificador del documento y su estado actual, sin esperar la respuesta de la autoridad tributaria. | Debe |
| **RF-15** | El sistema debe aceptar una referencia externa única por solicitud, de modo que una solicitud repetida con la misma referencia devuelva el documento ya creado en lugar de crear uno nuevo. | Debe |

**Criterios de aceptación**
- RF-11: la respuesta incluye el identificador interno, el número asignado y el estado `RECIBIDO`.
- RF-12 y RF-13: ver RN-03, RN-04 y RN-05.
- RF-14: la respuesta se produce sin haber contactado a la autoridad tributaria.
- RF-15: dos solicitudes idénticas con la misma referencia externa producen un solo documento y un solo número consumido. La segunda respuesta indica que corresponde a un documento preexistente.

---

### 4.6 Procesamiento y transmisión

| ID | Requerimiento | Prioridad |
|---|---|---|
| **RF-16** | El sistema debe generar, para cada documento recibido, un archivo XML conforme al estándar UBL 2.1 con las adendas colombianas. | Debe |
| **RF-17** | El sistema debe firmar digitalmente el XML generado con el certificado del emisor. | Debe |
| **RF-18** | El sistema debe transmitir el documento firmado al servicio de validación y registrar el identificador de seguimiento devuelto. | Debe |
| **RF-19** | El sistema debe consultar el resultado de la validación hasta obtener un veredicto definitivo o agotar los reintentos configurados. | Debe |
| **RF-20** | El sistema debe registrar el código único de identificación asignado por la autoridad al aprobarse el documento. | Debe |
| **RF-21** | El sistema debe registrar, cuando un documento es rechazado, la lista de errores devueltos por la autoridad. | Debe |

**Criterios de aceptación**
- RF-16: el XML valida contra el esquema oficial del estándar.
- RF-17: la firma es verificable con la clave pública del certificado, y cualquier alteración posterior del XML la invalida.
- RF-19: ver RNF-05 sobre la política de reintentos.
- RF-21: los errores se conservan asociados al documento y son consultables por el integrador.

---

### 4.7 Consulta

| ID | Requerimiento | Prioridad |
|---|---|---|
| **RF-22** | El sistema debe permitir consultar un documento por su identificador, devolviendo su estado actual, sus datos y, cuando exista, su código único y sus errores de validación. | Debe |
| **RF-23** | El sistema debe permitir consultar el historial completo de estados de un documento, con la marca de tiempo de cada transición. | Debe |
| **RF-24** | El sistema debe permitir listar documentos filtrando por tipo, estado y rango de fechas, con paginación. | Debería |
| **RF-25** | El sistema debe permitir descargar el XML firmado de un documento. | Debe |

**Criterios de aceptación**
- RF-23: para un documento aprobado, el historial muestra la secuencia completa de transiciones con sus tiempos.
- RF-24: la respuesta incluye el total de resultados y no devuelve más de un máximo configurable por página.
- RF-25: el archivo devuelto es byte a byte el que fue transmitido.

---

## 5. Reglas de negocio

| ID | Regla | Origen |
|---|---|---|
| **RN-01** | Un número de documento se asigna una sola vez. Dos documentos no pueden compartir número dentro del mismo prefijo. | Requisito legal de consecutividad. |
| **RN-02** | Un documento solo puede emitirse si existe un rango vigente, no agotado y no vencido, para su tipo y prefijo. | Requisito legal de autorización de numeración. |
| **RN-03** | Una nota crédito o débito debe referenciar una factura que exista en el sistema y se encuentre aprobada. | Coherencia documental. |
| **RN-04** | La suma de las notas crédito asociadas a una factura no puede superar el valor total de esa factura. | Coherencia contable. |
| **RN-05** | Una nota crédito o débito no puede referenciar a otra nota crédito o débito. | Coherencia documental. |
| **RN-06** | El impuesto se calcula sobre la base gravable de cada línea aplicando la tarifa del producto, pero el redondeo se aplica sobre el total del documento, no línea por línea. | Consistencia con la validación de la autoridad, que compara el total declarado contra la sumatoria. |
| **RN-07** | Un documento rechazado por la autoridad no puede corregirse ni retransmitirse. La corrección exige emitir un documento nuevo, con número nuevo. | Ver nota al pie. |
| **RN-08** | Un documento debe tener al menos una línea de detalle, y toda línea debe tener cantidad y precio mayores que cero. | Coherencia básica. |
| **RN-09** | El total del documento es la suma de las bases gravables más la suma de los impuestos, menos los descuentos. Este valor debe coincidir exactamente con el declarado en el XML. | Requisito de validación. |
| **RN-10** | Una factura aprobada es inmutable. Ninguno de sus datos puede modificarse después de la aprobación. | Requisito legal. |

> **Nota sobre RN-07.** El tratamiento del número de un documento rechazado admite más de una interpretación en la práctica. Este proyecto adopta la posición conservadora: el número se considera consumido y la corrección se hace con un documento nuevo. Se prefiere así porque reutilizar un número ya transmitido abre la posibilidad de que dos documentos distintos hayan circulado con la misma identificación. Cualquier uso real de este software exige verificar esta regla contra la versión vigente del anexo técnico de la DIAN.

---

## 6. Ciclo de vida del documento

Todo documento recorre esta máquina de estados. Ninguna transición distinta de las listadas es válida.

### 6.1 Estados

| Estado | Significado | ¿Terminal? |
|---|---|---|
| `RECIBIDO` | Aceptado por la API, persistido y con número asignado. Aún no procesado. | No |
| `EN_PROCESO` | Se está generando o firmando el XML. | No |
| `TRANSMITIDO` | Entregado al servicio de validación. Se tiene identificador de seguimiento. A la espera de veredicto. | No |
| `APROBADO` | Validado por la autoridad. Tiene código único. | Sí |
| `RECHAZADO` | La autoridad lo rechazó. Tiene lista de errores. | Sí |
| `FALLIDO` | El sistema no logró completar el proceso tras agotar los reintentos. El fallo es propio o de comunicación, no un rechazo de la autoridad. | Sí |

### 6.2 Transiciones permitidas

```
RECIBIDO     → EN_PROCESO
EN_PROCESO   → TRANSMITIDO
EN_PROCESO   → FALLIDO        (no se pudo generar o firmar)
TRANSMITIDO  → APROBADO
TRANSMITIDO  → RECHAZADO
TRANSMITIDO  → FALLIDO        (sin veredicto tras agotar reintentos)
```

### 6.3 Reglas de la máquina de estados

- **RN-11:** un documento en estado terminal no cambia de estado nunca más.
- **RN-12:** toda transición queda registrada con su marca de tiempo y el motivo que la produjo.
- **RN-13:** un documento en `FALLIDO` no implica que la autoridad no lo haya recibido. El estado se documenta como "resultado desconocido" y exige revisión manual antes de emitir un reemplazo.

> RN-13 es deliberada. La tentación es tratar `FALLIDO` como "no pasó nada" y reintentar con un documento nuevo, pero si la autoridad alcanzó a recibir el original se produciría una duplicación. Reconocer la incertidumbre en el modelo es preferible a esconderla.

---

## 7. Requerimientos no funcionales

| ID | Requerimiento | Criterio de aceptación | Prioridad |
|---|---|---|---|
| **RNF-01** | Los secretos —llaves de API, certificado, credenciales de base de datos— no pueden estar en el código fuente ni en el repositorio. | Una revisión del repositorio no encuentra ningún secreto. La configuración se provee por variables de entorno. | Debe |
| **RNF-02** | El acceso al servicio de validación de la autoridad debe estar detrás de una abstracción que permita sustituir la implementación sin modificar la lógica de negocio. | Cambiar del simulador a otra implementación se logra cambiando configuración, sin tocar el código de emisión. Origen: sección 7.1 del documento de visión. | Debe |
| **RNF-03** | La solicitud de emisión debe responder en menos de 500 ms en el percentil 95, medido sin incluir el tiempo del servicio externo. | Medición bajo carga de prueba. | Debería |
| **RNF-04** | El procesamiento de documentos debe continuar aunque el servicio de validación esté caído, encolando el trabajo pendiente. | Con el servicio externo apagado, la emisión sigue aceptándose y los documentos quedan en espera, no en error. | Debe |
| **RNF-05** | Los reintentos ante fallas transitorias deben usar espera creciente entre intentos, con un número máximo configurable. | Verificado con fallos simulados. El sistema no reintenta indefinidamente ni satura el servicio externo. | Debe |
| **RNF-06** | La asignación de números debe ser correcta bajo concurrencia. | Una prueba que emite múltiples documentos simultáneamente no produce números repetidos ni saltados. | Debe |
| **RNF-07** | La API debe estar documentada con OpenAPI, generado desde el código y no mantenido aparte. | La documentación está disponible en el servicio en ejecución y refleja los endpoints reales. | Debe |
| **RNF-08** | El sistema completo debe levantarse en una máquina limpia con un solo comando. | Verificado en un entorno sin configuración previa. | Debe |
| **RNF-09** | Las reglas de negocio de la sección 5 deben tener cobertura de pruebas automatizadas. | Cada RN tiene al menos una prueba que verifica su cumplimiento y una que verifica su violación. | Debe |
| **RNF-10** | Los registros de actividad deben ser estructurados e incluir un identificador de correlación que permita seguir un documento a lo largo de todo su procesamiento. | Dado un identificador de documento, es posible recuperar todos sus registros. | Debería |
| **RNF-11** | Los mensajes de error de la API deben indicar qué está mal y qué debe corregirse, sin exponer detalles internos del sistema. | Revisión de las respuestas de error. Ningún mensaje expone rutas, consultas ni trazas. | Debe |
| **RNF-12** | El sistema debe manejar valores monetarios con un tipo de dato de precisión decimal exacta. | Revisión del modelo de datos. No se usan tipos de punto flotante para dinero. | Debe |

---

## 8. Trazabilidad con los criterios de éxito

| Criterio de éxito (etapa 1) | Requerimientos que lo satisfacen |
|---|---|
| CE-01 — Integrador autónomo | RNF-07, RNF-11 |
| CE-02 — Nota crédito referencia factura válida | RF-12, RN-03, RN-05 |
| CE-03 — Numeración sin repetidos ni fuera de rango | RF-09, RN-01, RN-02, RNF-06 |
| CE-04 — Sin documentos en estado indeterminado | RF-14, RF-19, RN-11, RN-13, RNF-04, RNF-05 |
| CE-05 — XML válido contra el esquema | RF-16 |
| CE-06 — Levanta con un comando | RNF-08 |
| CE-07 — Trazabilidad requerimiento-prueba-commit | RNF-09 |

Todo criterio de éxito tiene al menos un requerimiento que lo respalda. Ningún requerimiento obligatorio queda huérfano de criterio.

---

## 9. Requerimientos considerados y diferidos

Se registran para dejar constancia de que fueron evaluados y descartados por decisión, no por olvido.

| Requerimiento | Decisión |
|---|---|
| Notificación saliente al integrador cuando cambia el estado | Diferido a versión 2. Exige que el integrador exponga un servidor accesible y obliga a resolver reintentos de entrega y firmas de notificación. |
| Representación gráfica en PDF con código QR | Diferido a versión 1.1. No es requisito de validez del documento. |
| Envío del documento por correo al adquirente | Fuera de alcance. Responsabilidad del sistema integrador, según sección 7.2 del documento de visión. |
| Soporte multi-empresa emisora | Fuera de alcance de la versión 1. El modelo de datos no debe impedirlo, pero no se implementa ni se prueba. |
| Límite de peticiones por integrador | Diferido. Relevante en un despliegue real, no en un ejercicio. |
| Anulación de documentos | Descartado. No existe la anulación de una factura electrónica aprobada; la corrección se hace con nota crédito, que ya está cubierta por RF-12. |

---

## 10. Control de cambios

| Versión | Fecha | Cambio |
|---|---|---|
| 1.0 | 2026-09-24 | Versión inicial. 25 requerimientos funcionales, 13 reglas de negocio, 12 requerimientos no funcionales. |
