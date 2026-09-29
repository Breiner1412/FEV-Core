# ADR-0014: El resultado desconocido como estado propio

**Estado:** Aceptada
**Fecha:** 2026-09-29

## Contexto

Transmitir un documento a un servicio externo puede terminar de más formas de las que parece. La lectura habitual es binaria —funcionó o falló— y esa lectura es incorrecta en un caso concreto y frecuente: la petición sale, y la respuesta no llega.

No es lo mismo que no poder conectar. Cuando la conexión se rechaza, se sabe que el documento no llegó. Cuando se agota el tiempo de espera después de haber enviado, no se sabe nada: el servicio pudo recibirlo, validarlo, aprobarlo y registrarlo, y solo se perdió el camino de vuelta.

Tratar ese caso como un fallo tiene una consecuencia legal. Reintentar significa transmitir de nuevo una factura que puede estar ya radicada ante la DIAN. Tratarlo como un éxito es peor: se daría por radicado un documento del que no hay constancia.

El dominio ya lo reconoce. RN-13 dice que un documento cuyo resultado se desconoce pasa a `FALLIDO` y exige revisión manual. Faltaba que el código pudiera distinguirlo.

## Decisión

El resultado de una transmisión tiene cuatro valores, no dos:

| Resultado | Qué se sabe | Qué hace el sistema |
|---|---|---|
| `Aceptada` | El servicio recibió el documento y devolvió con qué consultarlo | Pasa a consultar el veredicto |
| `ErrorTransitorio` | No llegó, y el motivo puede desaparecer solo | Reintenta con espera creciente |
| `ErrorDefinitivo` | Llegó y fue rechazado por lo que es | Deja de reintentar |
| `SinRespuesta` | **No se sabe si llegó** | Cuenta el intento y, al agotarlos, `FALLIDO` |

La traducción de cada forma de fallo vive en `ProveedorValidacionHttp` y es su única responsabilidad real:

- Excepción de cancelación tras enviar → `SinRespuesta`.
- `HttpRequestException` (no se pudo ni conectar) → `ErrorTransitorio`.
- 5xx, 408, 429 → `ErrorTransitorio`.
- Resto de 4xx → `ErrorDefinitivo`.
- 202 o 200 sin identificador de seguimiento → `ErrorTransitorio`.

Ese último merece explicación. Aceptar un documento sin devolver con qué consultarlo después lo deja sin forma de averiguar su veredicto nunca. Darlo por bueno sería perderlo silenciosamente.

Un documento que agota sus intentos en `SinRespuesta` no queda en el mismo sitio que uno rechazado. `FALLIDO` no significa "salió mal": significa que **nadie sabe qué pasó** y que una persona tiene que averiguarlo antes de volver a emitir.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| Éxito o fallo, y reintentar siempre que falle | Es lo que hace la mayoría del código, y produce facturas duplicadas ante la autoridad. El caso raro deja de ser raro en cuanto hay tráfico. |
| Tratar la falta de respuesta como fallo definitivo | Evita el duplicado dando el documento por perdido. Pero pudo haberse radicado: emitir otro con consecutivo nuevo dejaría dos radicados y ninguna forma de saberlo. |
| Reintentar apoyándose en la idempotencia del servicio | Es la solución correcta cuando el servicio la garantiza por contrato. No se ha verificado que la DIAN la garantice, y una regla legal no se apoya en una suposición sobre un servicio de terceros. |
| Consultar antes de reintentar, para averiguar si llegó | Es lo que debería hacerse y exige un identificador de seguimiento que en este caso justamente no se recibió. Queda como mejora si el servicio permitiera consultar por número de documento. |

## Consecuencias

**Positivas**
- El sistema no inventa información que no tiene. Un documento sin respuesta no se declara ni bueno ni malo.
- La distinción es visible en el tipo, no en un comentario: quien escriba otro proveedor tiene que decidir a qué categoría pertenece cada fallo.

**Negativas**
- `FALLIDO` es un estado terminal que requiere intervención humana, y no hay todavía herramienta para esa intervención. Queda registrado como deuda.
- Un servicio lento que responde tarde produce `SinRespuesta` sin que nada esté realmente mal. El tiempo de espera (`Validacion:TiempoEsperaSegundos`) es el parámetro que decide dónde está esa frontera, y no hay un valor correcto universal.

## Verificación

`ProveedorValidacionHttpTests` prueba cada traducción con un manejador HTTP falso, porque provocarlas contra un servicio real exigiría un servidor que se caiga a voluntad y que reciba una petición y no conteste nunca.

Las dos pruebas que sostienen esta decisión son deliberadamente contiguas:

- `Agotar_el_tiempo_de_espera_deja_el_resultado_desconocido` → `SinRespuesta`.
- `No_poder_conectar_es_transitorio` → `ErrorTransitorio`.

Ambas "fallan". Confundirlas es el error que este ADR existe para impedir.

`CicloCompletoTests.Sin_respuesta_hasta_agotar_intentos_el_documento_queda_fallido` cubre el recorrido completo: el documento acaba en `FALLIDO`, conserva lo recibido, y su historial dice "RESULTADO DESCONOCIDO" y no "rechazado".
