# ADR-0015: Qué se sabe de un documento FALLIDO, y un 2xx sin seguimiento es un resultado desconocido

**Estado:** Aceptada
**Fecha:** 2026-10-02
**Sustituye a:** ADR-0014 (`docs/adr/0014-resultado-desconocido.md`)

## Contexto

ADR-0014 introdujo el resultado desconocido como valor propio de una transmisión, y sigue siendo correcto en lo esencial: los cuatro resultados, la traducción de cada fallo HTTP y la razón para no reintentar a ciegas. Este ADR lo sustituye porque la auditoría final encontró dos cosas que ADR-0014 decía mal.

### 1. Un 202 o 200 sin identificador de seguimiento no es "no llegó"

ADR-0014 lo clasificaba como `ErrorTransitorio` con este argumento: aceptar un documento sin devolver con qué consultarlo lo deja sin forma de averiguar su veredicto, y darlo por bueno sería perderlo en silencio.

El argumento es correcto y la conclusión no. `ErrorTransitorio` significa, en el propio ADR-0014, "**no llegó**, y el motivo puede desaparecer solo". Un 2xx dice exactamente lo contrario: el servicio recibió la petición. Lo que falta es el medio para saber qué pasó después. Eso es "no se sabe", que es `SinRespuesta`.

La diferencia no es de nombre. Al agotarse los intentos, el historial de un documento así decía "no consta que el documento llegara", y una persona que leyera eso podía emitir un reemplazo sin verificar nada, que es justo la duplicación que ADR-0014 existía para evitar.

### 2. FALLIDO no significa siempre "resultado desconocido"

ADR-0014 terminaba así: "`FALLIDO` no significa 'salió mal': significa que **nadie sabe qué pasó**". El contrato y RN-13 lo repetían.

La sección 6.2 de los requerimientos siempre llevó a `FALLIDO` por dos caminos: `EN_PROCESO → FALLIDO` ("no se pudo generar o firmar") y `TRANSMITIDO → FALLIDO` ("sin veredicto tras agotar reintentos"). Un documento que no logró generar su XML tiene un resultado perfectamente conocido: no salió de aquí. La definición de ADR-0014 solo describía bien uno de los casos.

Y tenía una consecuencia práctica: si `FALLIDO` significa siempre "no se sabe", todo `FALLIDO` manda a investigar ante la autoridad. Cuando lo que faltaba era un rango de numeración, esa investigación es para nada.

## Decisión

### `FALLIDO` es "sin desenlace, hace falta una persona"

`FALLIDO` significa que el documento no llegó a un desenlace y que hace falta una persona. No dice por sí solo qué pasó. Lo dice el **historial**: el detalle de la transición a `FALLIDO` distingue los casos según lo que consta en las transmisiones.

| Lo que consta | Detalle del historial | ¿Verificar ante la DIAN antes de reemplazar? |
|---|---|---|
| Alguna entrega aceptada, con identificador de seguimiento | `RADICADO SIN VEREDICTO` | No. Consta que llegó; falta consultar su veredicto. |
| Ninguna aceptada y alguna sin respuesta | `RESULTADO DESCONOCIDO` | **Sí.** Es el único caso. |
| Ninguna aceptada ni sin respuesta, y el servicio rechazó la entrega | `NO RADICADO` | No. |
| Ningún envío llegó, o no hubo envío | `NO SALIO DE AQUI` | No. |

El orden importa. Una entrega aceptada va primero: si consta que llegó, un envío anterior sin respuesta ya no deja la duda de si llegó.

`NO RADICADO` es un cuarto texto dentro del mismo grupo que `NO SALIO DE AQUI`. Según ADR-0014, `ErrorDefinitivo` significa que el envío **llegó** y el servicio rechazó la entrega: decir que no salió de aquí sería falso, aunque la conclusión práctica sea la misma.

Vive en `Documento.RegistrarFallo`, en el dominio, para que ningún proveedor de validación pueda saltárselo.

### Un 2xx sin seguimiento utilizable es `SinRespuesta`

En `ProveedorValidacionHttp`, un 202 o 200 cuyo identificador de seguimiento falta, viene en blanco o no cabe donde se guarda (`Transmision.LongitudMaximaSeguimiento`) es `SinRespuesta`, no `ErrorTransitorio`.

La longitud entra en "utilizable" porque un identificador que no se puede guardar es igual de inservible que uno que no vino: guardarlo hacía fallar cada intento, y la auditoría encontró que ese fallo hacía girar la tarea sin fin.

### Una excepción no prevista al transmitir también es `SinRespuesta`

Si el proveedor lanza algo que nadie previó durante la transmisión, `ProcesadorTareas` lo registra como un envío sin respuesta. Desde el procesador no se puede saber si la petición salió antes de la excepción. Antes ese intento no dejaba rastro, y el historial acababa afirmando que el documento no había salido.

El resto de ADR-0014 —los otros valores, la traducción de 5xx, 408, 429 y 4xx, y las alternativas descartadas— sigue vigente tal como está escrito.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| Mantener `ErrorTransitorio` para el 2xx sin seguimiento | Afirma que el documento no llegó cuando el servicio dijo que sí. Es la conclusión opuesta a lo que se sabe. |
| Tratar el 2xx sin seguimiento como `Aceptada` | No hay con qué consultar el veredicto, así que el documento quedaría en `TRANSMITIDO` para siempre. Es el "perderlo en silencio" que ADR-0014 ya descartó con razón. |
| Un estado nuevo por cada desenlace (`NO_ENVIADO`, `SIN_VEREDICTO`, …) | Cambia la máquina de estados de la sección 6 y el contrato de la API para algo que el historial ya puede decir. Los casos comparten lo que importa al integrador: no hay desenlace y hace falta una persona. |
| Seguir diciendo que todo `FALLIDO` es desconocido | Es la opción conservadora, y por eso tentadora. Pero un aviso que salta siempre deja de leerse: si todo `FALLIDO` manda a investigar ante la DIAN, el que de verdad lo necesita se pierde entre los que no. |

## Consecuencias

**Positivas**
- El historial dice qué se sabe, sin inventar en ninguna dirección: ni que llegó lo que no consta, ni que no llegó lo que sí.
- Quien revisa un `FALLIDO` sabe si tiene que ir a la DIAN o no.

**Negativas**
- El integrador tiene que leer el historial para saber qué hacer; el estado solo no basta. El contrato (sección 5.5) lo explica.
- Los documentos que ya estaban en `FALLIDO` antes de este cambio conservan el texto anterior. "No consta que el documento llegara" en un documento antiguo **no** garantiza que no llegara.
- Si un proveedor distinto de `ProveedorValidacionHttp` devolviera `Aceptada` con un identificador demasiado largo, el guardado seguiría fallando. Ese caso termina gracias al tope de intentos, pero sin registrar la transmisión, y el historial diría `NO SALIO DE AQUI`. La regla de "utilizable" vive en el adaptador HTTP, que es el único proveedor que existe; llevarla al dominio queda como mejora.

## Verificación

- `DesenlaceFallidoTests` (dominio): un caso por cada texto.
- `ProveedorValidacionHttpTests.Una_respuesta_aceptada_sin_seguimiento_utilizable_deja_el_resultado_desconocido`: 202 y 200 sin identificador, con identificador en blanco y con uno de 150 caracteres. Sustituye a la prueba que afirmaba lo contrario, `…se_trata_como_transitoria`, que estaba bien escrita y defendía una decisión equivocada.
- El texto del historial se comprueba de punta a punta:
  - `FalloAlGenerarTests` → `NO SALIO DE AQUI`.
  - `CicloCompletoTests.Un_error_definitivo_no_se_reintenta` → `NO RADICADO`.
  - `…fallo_no_previsto_al_consultar…` → `RADICADO SIN VEREDICTO`.
  - `…fallo_no_previsto_al_transmitir…` y `Sin_respuesta_hasta_agotar_intentos…` → `RESULTADO DESCONOCIDO`.
