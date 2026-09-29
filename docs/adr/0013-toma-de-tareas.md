# ADR-0013: Toma de tareas bajo concurrencia

**Estado:** Aceptada
**Fecha:** 2026-09-29

## Contexto

ADR-0006 decidió que el trabajo pendiente vive como filas en una tabla y que un proceso en segundo plano las consulta. Dejó explícitamente sin resolver una consecuencia: *"escalar a varios trabajadores exige que la toma de tareas sea correcta bajo concurrencia"*.

El problema tiene dos caras que se estorban entre sí.

La primera es la exclusión. Si dos trabajadores leen la misma fila pendiente, ambos transmiten el mismo documento. La DIAN no recibe dos borradores: recibe dos veces una factura con el mismo consecutivo, que es exactamente lo que RN-01 existe para impedir.

La segunda es la duración. Transmitir tarda segundos: se genera el XML, se firma, se hace una llamada de red a un servicio externo. La forma obvia de garantizar exclusión —abrir una transacción, bloquear la fila, trabajar, cerrar— mantendría una conexión de base de datos ocupada durante toda esa espera. Con veinte documentos pendientes serían veinte conexiones esperando a un tercero. El agotamiento del pozo de conexiones llegaría antes que el problema que se intentaba resolver.

## Decisión

La toma y el trabajo se separan en dos momentos.

**Tomar** ocurre dentro de una transacción corta:

```sql
SELECT * FROM tareas_salida
WHERE "CompletadaEn" IS NULL
  AND "ProximoIntentoEn" <= @momento
  AND ("TomadaEn" IS NULL OR "TomadaEn" < @limiteAbandono)
ORDER BY "ProximoIntentoEn"
LIMIT 1
FOR UPDATE SKIP LOCKED
```

`FOR UPDATE` bloquea la fila elegida. `SKIP LOCKED` hace que un segundo trabajador simultáneo no espere a que se libere, sino que pase a la siguiente candidata. Sin él, N trabajadores se pondrían en fila para la misma tarea y el sistema procesaría en serie por mucho que se multiplicaran los procesos.

Tomada la tarea, se escribe `TomadaEn` y se cierra la transacción. El bloqueo de fila desaparece.

**Trabajar** ocurre después, sin transacción abierta. Durante ese rato, lo único que impide que otro trabajador agarre la tarea es la marca `TomadaEn`: la consulta de arriba descarta las tareas tomadas recientemente.

La marca envejece. Una tarea cuyo `TomadaEn` es más antiguo que `Salida:TiempoDeAbandonoSegundos` vuelve a ser candidata, porque el proceso que la tenía no va a soltarla nunca.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| Mantener la transacción abierta durante la transmisión | Es lo más simple de razonar y lo que agota el pozo de conexiones. Una conexión bloqueada esperando a un servicio externo es una conexión que ningún otro trabajo puede usar. |
| `FOR UPDATE` sin `SKIP LOCKED` | Garantiza exclusión, pero serializa: los trabajadores hacen cola para la misma fila en vez de repartirse el trabajo. Añadir procesos no añadiría rendimiento. |
| Bloqueo optimista con número de versión | Funciona, pero el trabajo se descubre inútil al final: dos trabajadores generan y firman el XML, y uno de los dos descubre al guardar que perdió. Se paga el trabajo dos veces. |
| Bloqueos consultivos de PostgreSQL (`pg_advisory_lock`) | Viven fuera de la transacción y sobreviven a ella, que es justo lo que hace falta. Se descartó porque se liberan al cerrarse la conexión: con un pozo de conexiones, el momento en que eso ocurre deja de ser evidente. |
| Que el trabajador sea un proceso único | Elimina la concurrencia eliminando el paralelismo. También elimina la disponibilidad: ese proceso es un punto único de fallo. |

## Consecuencias

**Positivas**
- Varios trabajadores se reparten el trabajo sin pisarse, y añadir procesos añade rendimiento.
- Ninguna conexión de base de datos queda retenida mientras se espera a un servicio externo.
- Un proceso que muere a mitad no deja trabajo atascado: su marca envejece y la tarea vuelve sola a la bandeja.

**Negativas**
- Existe una ventana en la que dos trabajadores **pueden** procesar la misma tarea: si el primero sigue vivo pero tardó más que el tiempo de abandono. El parámetro es un compromiso, no una garantía. Demasiado corto duplica trabajo; demasiado largo deja documentos parados tras un reinicio.
- La consulta con SQL explícito no la genera Entity Framework. Es deuda deliberada: `SKIP LOCKED` no tiene equivalente en LINQ, y escribirlo a mano es preferible a fingir que no está ahí.

**Compensación**
- La ventana de solapamiento no produce documentos duplicados por sí sola. El consecutivo se toma bajo el bloqueo de ADR-0009 y queda escrito en el documento antes de transmitir, así que un segundo intento transmite el MISMO documento, no uno nuevo. El riesgo real es una transmisión repetida del mismo documento. Qué hace el servicio de validación ante eso no se ha verificado contra documentación oficial, así que no se apoya ninguna garantía en ello: se registra como pregunta abierta para cuando se integre el servicio real.

## Verificación

`TareaSalidaTests` cubre las reglas de la tarea en el dominio, donde el reloj es un parámetro y el intento número doce ocurre sin esperar una hora:

- `Una_tarea_recien_tomada_no_esta_abandonada` y `Una_tarea_tomada_hace_rato_se_considera_abandonada` fijan las dos caras de la ventana.
- `La_espera_se_duplica_con_cada_intento` y `La_espera_nunca_pasa_del_techo` cubren RNF-05.

`BandejaDeSalidaTests` cubre la consulta contra PostgreSQL real, que es donde vive de verdad esta decisión. Una base en memoria pasaría estas pruebas sin ejecutar nunca la cláusula que importa:

- `Una_tarea_tomada_no_se_la_lleva_otro` y `Una_tarea_abandonada_vuelve_sola_a_la_bandeja` son las dos caras de la ventana de abandono. La segunda es la demostración de "matar el proceso a mitad y reiniciar": avanzar el reloj media hora **es** matar el proceso, porque lo único que ocurre al morir es que la marca deja de refrescarse.
- `Cuatro_trabajadores_simultaneos_no_se_llevan_la_misma_tarea` prueba la exclusión.
- `Tres_trabajadores_simultaneos_se_reparten_tres_tareas` prueba la otra cara, que es la razón de `SKIP LOCKED` frente a `FOR UPDATE` a secas: sin él seguirían saliendo tres tareas distintas, pero en serie.

`CicloCompletoTests` cubre el recorrido de punta a punta, con el trabajador de fondo apagado para que las pruebas decidan cuándo se procesa cada paso.

**Lo que estas pruebas no cubren.** La exclusión se verifica con transacciones simultáneas sobre la misma base, que es el escenario real, pero no se ha forzado el entrelazado peor posible. Retirar `SKIP LOCKED` no pone roja ninguna de estas pruebas —solo las volvería más lentas—, así que la verificación por mutación que sí existe en ADR-0009 y ADR-0010 aquí no aplica de la misma forma. Lo que sí se rompe al retirar `FOR UPDATE` entero es la exclusión, y eso `Cuatro_trabajadores_simultaneos` lo detecta.
