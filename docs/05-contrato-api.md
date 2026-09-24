# Contrato de la API

**Proyecto:** FEV-Core — API de emisión de documentos electrónicos (Colombia)
**Versión:** 1.0
**Fecha:** 24 de septiembre de 2026
**Autor:** Breiner Stiven Guisao Rodríguez
**Especificación:** [`api/openapi.yaml`](../api/openapi.yaml)
**Estado:** Aprobado. Última etapa previa a la implementación.

---

## 1. Propósito

La especificación OpenAPI define el contrato de forma precisa y procesable por herramientas. Este documento explica las decisiones que hay detrás, describe el catálogo de errores y sirve de guía de integración.

**Por qué el contrato se define antes de programar.** Una vez que un integrador consume la API, cualquier cambio en un nombre de campo, un tipo de dato o un código de estado rompe su sistema en producción. Diseñarlo en papel cuesta unas horas; cambiarlo después cuesta coordinar con todos los que ya dependen de él.

**Relación con RNF-07.** La especificación de este documento es el diseño. Una vez implementado, ASP.NET Core genera la especificación desde el código, y esa generada es la que se publica. Ambas deben coincidir: si difieren, el código se desvió del contrato acordado.

---

## 2. Decisiones del contrato

### 2.1 Rutas separadas para crear, una sola para consultar

```
POST /facturas
POST /notas-credito
POST /notas-debito
GET  /documentos/{id}
```

**Razón.** Los tres tipos comparten estructura de salida pero no de entrada: las notas exigen `documentoReferenciadoId` y `motivo`, que no aplican a una factura. Una sola ruta de creación obligaría a declarar esos campos como opcionales y a explicar en prosa cuándo son obligatorios, lo cual las herramientas no pueden verificar.

Separando la creación, cada tipo declara exactamente sus campos requeridos y la validación es automática.

La consulta sí es una sola ruta porque a la salida los tres son el mismo recurso: un documento. Esto es coherente con el modelo de dominio, donde `Documento` es una entidad con un atributo `tipo`, no tres entidades.

### 2.2 Formato de error estándar

Se usa **Problem Details (RFC 7807)**, con `application/problem+json`.

**Razón.** Es un estándar publicado, ya integrado en ASP.NET Core, y cualquier desarrollador que haya consumido APIs modernas lo reconoce. Inventar un formato propio obligaría a cada integrador a aprender una convención privada sin ganar nada.

**Extensión.** Se agrega un campo `codigo` con un valor estable por tipo de error. RFC 7807 permite extensiones, y esta resuelve un problema concreto: `title` y `detail` son texto para humanos y pueden cambiar de redacción; `codigo` no cambia, y es lo que el integrador debe usar para decidir qué hacer.

### 2.3 Versión en la ruta

Todas las rutas van bajo `/api/v1/`.

**Razón.** Es visible, se puede probar desde un navegador y no deja ambigüedad sobre qué versión se está consumiendo. La alternativa de versionar por cabecera es más pura conceptualmente, pero invisible al depurar.

Incluir `v1` desde el primer día no cuesta nada. Agregarlo después obliga a mover todas las rutas y romper a quien ya integró.

### 2.4 Nombres de campo en español

Coherente con la sección 3.4 del documento de arquitectura: los conceptos del dominio conservan su nombre en español, porque son términos del sistema tributario colombiano.

Traducir `adquirente` a `acquirer` introduciría un paso de traducción entre los requerimientos y el contrato, y ahí es donde se pierden los matices.

### 2.5 Identificadores opacos

Los identificadores de recurso son UUID, no números consecutivos.

**Razón.** Un consecutivo expuesto revela cuántos documentos se han emitido. Además, el identificador del documento debe existir con independencia de su número fiscal, que se rige por reglas de negocio distintas (sección 6.2 del modelo de dominio).

---

## 3. Códigos de estado

### 3.1 Respuestas exitosas

| Código | Cuándo | Significado preciso |
|---|---|---|
| `200 OK` | Consultas, modificaciones, y **emisión con referencia repetida**. | Aquí está el recurso tal como está ahora. |
| `201 Created` | Creación de adquirentes, productos y rangos. | El recurso quedó creado y listo para usarse. |
| `202 Accepted` | **Emisión de un documento nuevo.** | Lo recibí y lo voy a procesar. Aún no hay resultado. |
| `204 No Content` | Desactivación de adquirentes y productos. | Hecho. No hay nada que devolver. |

**Por qué `202` y no `201` al emitir.** `201 Created` significa que el recurso quedó en su estado final. Un documento recién recibido no lo está: falta generarlo, firmarlo, transmitirlo y esperar el veredicto. `202 Accepted` existe exactamente para esto, y le comunica al integrador que debe consultar después.

**Por qué `200` cuando la referencia se repite.** La primera solicitud responde `202` porque hay algo pendiente de procesar. Si tres minutos después llega la misma solicitud, el documento puede estar ya aprobado: responder `202` otra vez sería afirmar que hay trabajo pendiente cuando no lo hay.

La distinción le sirve al integrador para saber si su solicitud creó el documento o si ya existía.

### 3.2 Errores

| Código | Cuándo | Qué debe hacer el integrador |
|---|---|---|
| `400 Bad Request` | La solicitud está mal formada: falta un campo obligatorio, un tipo no corresponde, un valor está fuera de rango. | Corregir la solicitud. Reintentar igual fallará igual. |
| `401 Unauthorized` | Falta la llave, o es inválida o está desactivada. | Revisar la credencial. |
| `404 Not Found` | El recurso no existe. | Verificar el identificador. |
| `409 Conflict` | La solicitud es válida en forma, pero viola una regla de negocio o el estado actual no permite la operación. | Leer `codigo` y actuar según la causa. |
| `500 Internal Server Error` | Falla del sistema. | Reintentar con la misma `referenciaExterna`. Es seguro. |

**La distinción entre `400` y `409` es deliberada.** `400` significa "esta solicitud está mal escrita" y se detecta sin consultar nada. `409` significa "la solicitud está bien escrita, pero no procede en este momento o contra este estado": el rango está agotado, la factura referenciada no está aprobada, la nota supera el valor. Esa distinción le dice al integrador si debe corregir su código o resolver una situación del negocio.

---

## 4. Catálogo de errores

El valor de `codigo` es estable. Es lo que el integrador debe usar para decidir; `title` y `detail` pueden cambiar de redacción sin previo aviso.

### 4.1 Autenticación

| Código | Estado | Significado |
|---|---|---|
| `LLAVE_INVALIDA` | 401 | La llave no existe, está desactivada o no fue enviada. |

### 4.2 Configuración

| Código | Estado | Significado |
|---|---|---|
| `EMISOR_INCOMPLETO` | 409 | Falta configurar datos del emisor o el certificado. No se consume consecutivo. *(RF-05)* |
| `CERTIFICADO_VENCIDO` | 409 | El certificado de firma está fuera de vigencia. *(INV-CER-01)* |

### 4.3 Numeración

| Código | Estado | Significado |
|---|---|---|
| `RANGO_NO_ENCONTRADO` | 409 | No hay rango registrado para ese tipo de documento. |
| `RANGO_AGOTADO` | 409 | No quedan números disponibles. *(INV-RAN-04)* |
| `RANGO_VENCIDO` | 409 | El rango está fuera de su periodo de vigencia. *(INV-RAN-04)* |
| `RANGO_SOLAPADO` | 409 | El rango que se intenta registrar se cruza con otro activo. *(INV-RAN-03)* |
| `RANGO_LIMITES_INVALIDOS` | 400 | El número final es menor o igual al inicial. *(INV-RAN-01)* |

### 4.4 Catálogos

| Código | Estado | Significado |
|---|---|---|
| `ADQUIRENTE_NO_ENCONTRADO` | 409 | El adquirente indicado no existe. |
| `ADQUIRENTE_INACTIVO` | 409 | El adquirente existe pero está desactivado. |
| `PRODUCTO_NO_ENCONTRADO` | 409 | Un producto de las líneas no existe. |
| `PRODUCTO_INACTIVO` | 409 | Un producto de las líneas está desactivado. |
| `IDENTIFICACION_DUPLICADA` | 409 | Ya existe un adquirente activo con esa identificación. *(INV-ADQ-01)* |
| `CODIGO_PRODUCTO_DUPLICADO` | 409 | Ya existe un producto activo con ese código. |

### 4.5 Documentos

| Código | Estado | Significado |
|---|---|---|
| `DOCUMENTO_REFERENCIADO_NO_ENCONTRADO` | 409 | La factura referenciada no existe. *(RN-03)* |
| `DOCUMENTO_REFERENCIADO_NO_APROBADO` | 409 | La factura referenciada existe pero no está aprobada. *(RN-03)* |
| `DOCUMENTO_REFERENCIADO_INVALIDO` | 409 | Se intentó referenciar una nota en lugar de una factura. *(RN-05)* |
| `NOTA_EXCEDE_VALOR_FACTURA` | 409 | El acumulado de notas crédito superaría el total de la factura. *(RN-04)* |
| `XML_NO_DISPONIBLE` | 409 | El documento aún no ha sido firmado; no hay XML que descargar. |
| `VALIDACION` | 400 | Error de forma. El campo `errores` detalla qué falló y dónde. |

---

## 5. Guía de integración

### 5.1 Configuración inicial

Antes de emitir, una sola vez:

1. `PUT /emisor` — datos de la empresa.
2. `POST /rangos-numeracion` — el rango autorizado.
3. `POST /adquirentes` y `POST /productos` — el catálogo.

`GET /emisor` devuelve `configuracionCompleta`. Si es `false`, la emisión fallará con `EMISOR_INCOMPLETO`.

### 5.2 Emitir una factura

```
POST /api/v1/facturas
X-Api-Key: <llave>
Content-Type: application/json

{
  "referenciaExterna": "VTA-2026-000145",
  "adquirenteId": "a3f1c2d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
  "formaPago": "CONTADO",
  "lineas": [
    { "productoId": "b1e2d3c4-5f6a-4b7c-8d9e-0f1a2b3c4d5e", "cantidad": 2 }
  ]
}
```

Respuesta `202`:

```json
{
  "id": "f1a2b3c4-5d6e-4f7a-8b9c-0d1e2f3a4b5c",
  "tipo": "FACTURA",
  "estado": "RECIBIDO",
  "numeroCompleto": "SETP990000123",
  "totales": { "totalAPagar": 357000.00 },
  "codigoUnico": null
}
```

**Guardar el `id`.** Es la única forma de consultar el documento después.

### 5.3 Consultar el resultado

```
GET /api/v1/documentos/{id}
```

Consultar cada pocos segundos hasta que el estado sea terminal: `APROBADO`, `RECHAZADO` o `FALLIDO`.

| Estado | Qué significa para el integrador |
|---|---|
| `RECIBIDO`, `EN_PROCESO`, `TRANSMITIDO` | Sigue esperando. |
| `APROBADO` | Listo. `codigoUnico` está disponible y el XML se puede descargar. |
| `RECHAZADO` | La autoridad lo rechazó. Revisar `erroresValidacion`, corregir y emitir un documento nuevo. El original no se puede corregir *(RN-07)*. |
| `FALLIDO` | **Resultado desconocido.** Requiere revisión manual antes de emitir un reemplazo. Ver más abajo. |

### 5.4 Reintentos seguros

Si se pierde la respuesta de una emisión —se cortó la red, venció el tiempo de espera, el servidor respondió `500`— **reenviar la misma solicitud con la misma `referenciaExterna` es seguro**.

- Si el documento no se había creado, se crea. Respuesta `202`.
- Si ya se había creado, se devuelve el existente. Respuesta `200`.

En ningún caso se crea un documento duplicado ni se consume un consecutivo adicional.

Esto vale para todos los errores de comunicación y para `500`. No vale para `400` ni `409`: esos indican que la solicitud tiene un problema que reintentar no resuelve.

### 5.5 El estado `FALLIDO`

Es el único estado que exige intervención humana.

Significa que el sistema agotó sus reintentos sin obtener un veredicto. No significa que la autoridad no haya recibido el documento: puede haberlo recibido y haberse perdido la respuesta.

Por eso el integrador **no debe reemplazarlo automáticamente**. Emitir un documento nuevo sin verificar podría duplicar una factura que sí llegó, y una factura duplicada ante la autoridad tributaria no se arregla borrando un registro.

El procedimiento correcto es consultar `GET /documentos/{id}/historial` para ver qué ocurrió, verificar por fuera del sistema si el documento existe ante la autoridad, y solo entonces decidir. Esto implementa RN-13.

---

## 6. Trazabilidad

| Requerimiento | Dónde se cumple en el contrato |
|---|---|
| RF-01, RF-02 | Esquema de seguridad `LlaveApi`, obligatorio en todas las operaciones |
| RF-03, RF-04, RF-05 | `/emisor`, campo `configuracionCompleta`, error `EMISOR_INCOMPLETO` |
| RF-06 | `/adquirentes` |
| RF-07 | `/productos` |
| RF-08, RF-10 | `/rangos-numeracion` con `numerosDisponibles` y `diasParaVencimiento` |
| RF-11, RF-12, RF-13 | `/facturas`, `/notas-credito`, `/notas-debito` |
| RF-14 | Respuesta `202` sin contactar el servicio externo |
| RF-15 | `referenciaExterna` y la distinción entre `202` y `200` |
| RF-20, RF-21 | Campos `codigoUnico` y `erroresValidacion` |
| RF-22 | `GET /documentos/{id}` |
| RF-23 | `GET /documentos/{id}/historial` |
| RF-24 | `GET /documentos` con filtros y paginación |
| RF-25 | `GET /documentos/{id}/xml` |
| RNF-07 | La especificación OpenAPI |
| RNF-10 | Campo `traceId` en las respuestas de error |
| RNF-11 | Formato Problem Details; ningún error expone detalles internos |

---

## 7. Lo que el contrato no promete

Declarado explícitamente para que ningún integrador asuma de más.

| No se promete | Por qué |
|---|---|
| Que un documento aceptado será aprobado | `202` significa recibido, no validado. |
| Un tiempo máximo hasta el veredicto | Depende de un servicio externo. |
| Que `FALLIDO` implique que el documento no llegó a la autoridad | RN-13. Es un resultado desconocido. |
| Notificación cuando cambia el estado | Versión 1 es solo consulta. Candidato a versión 2. |
| Estabilidad del texto de `title` y `detail` | Solo `codigo` y `status` son estables. |
| Orden de procesamiento entre documentos | Se asigna consecutivo en orden de llegada, pero el procesamiento posterior puede completarse en otro orden. |

---

## 8. Control de cambios

| Versión | Fecha | Cambio |
|---|---|---|
| 1.0 | 2026-09-24 | Versión inicial. 13 rutas, 31 esquemas, 20 códigos de error. |
