# Retrospectiva

Qué salió distinto a lo planeado, qué costó más de lo previsto y qué haría diferente.

Este documento existe porque un plan que se cumple a la perfección no se lo cree nadie, y con razón. Lo que sigue es lo que de verdad pasó.

---

## 1. Dónde acabó el proyecto

| | |
|---|---|
| Hitos | 9 planeados (H0 a H8), 9 entregados |
| Del primer commit al último | 24 al 30 de septiembre de 2026 |
| Código de producción | 15.800 líneas en 106 archivos *(al cierre de H8)* |
| Código de pruebas | 7.100 líneas en 38 archivos *(al cierre de H8)* |
| Pruebas automatizadas | 361 al cierre de H8; 402 tras la auditoría final, todas en verde |
| Decisiones registradas (ADR) | 17 (14 en H8; la auditoría añadió ADR-0015, que sustituye a ADR-0014, ADR-0016 y ADR-0017) |
| Migraciones de base de datos | 9 (la novena, de la auditoría final) |
| Requerimientos | 50: 3 sin verificación automática y 2 implementados solo en parte (RF-11, RF-24) |

El dato que más dice de los de arriba es la proporción entre código y pruebas: por cada dos líneas de producción hay casi una de prueba. No era una meta; salió de una decisión tomada en H0 y sostenida después, que es que ningún mecanismo de concurrencia entra sin una prueba que lo demuestre roto al quitarlo.

---

## 2. Lo que se subestimó

### 2.1 La firma digital, con diferencia

El plan le daba a la firma un hito entero y marcaba el riesgo R-05 como capaz de bloquear el proyecto. Acertaba en la importancia y se quedaba corto en la naturaleza del problema.

Lo difícil no fue firmar. Lo difícil fueron dos errores que son el mismo con distinta cara: **los bytes que se firman tienen que ser los bytes que se verifican.**

Uno, `SignedXml` resuelve cada referencia buscando el identificador en el documento, y las propiedades XAdES viven dentro de un elemento que todavía no existe cuando se calculan los resúmenes. Dos, la canonicalización inclusiva arrastra los espacios de nombres heredados: un bloque suelto al firmar y anidado al verificar produce dos textos distintos y por tanto dos resúmenes distintos.

Ninguno de los dos se manifiesta como "la firma es inválida". Se manifiestan como `Malformed reference element` y como un `false` sin explicación.

**Lo que costó tiempo de verdad fue un diagnóstico equivocado.** Se señaló una causa, se quitó, y el error siguió igual. De ahí salió la regla que más veces se ha usado después: *cuando quitas la causa sospechada y el error no cambia, la sospecha era falsa.* Parece obvia escrita. No lo es cuando llevas dos horas convencido.

### 2.2 El entorno, que no estaba en ningún riesgo del plan

A mitad de H7, `dotnet test` empezó a fallar con un error de carga de ensamblados. No era el código: era **Smart App Control**, una política de Windows que bloquea binarios sin firma digital, y un DLL recién compilado nunca la tiene.

Se persiguieron dos causas falsas antes de acertar —la carpeta en Descargas, la marca de "descargado de internet"—, y las dos se descartaron por el mismo método que la firma: quitar la causa sospechada y ver que el error no cambiaba.

Lo que zanjó el asunto fue **dejar de suponer y preguntarle a Windows**. El registro de eventos lo decía en una línea: *"did not meet the Enterprise signing level requirements"*. Diez minutos de consultar el visor de eventos habrían ahorrado dos rondas de teorías.

La solución fue montar WSL y correr las pruebas en Linux. Salió mejor que el problema: ahora el entorno de pruebas local es el mismo del CI, así que "verde en mi máquina" y "verde en GitHub" significan lo mismo.

**El plan tenía una sección de riesgos y ninguno era del entorno de desarrollo.** Todos eran técnicos o normativos. El que paró el trabajo medio día no estaba en la lista.

### 2.3 Escribir pruebas es fácil; escribir pruebas que puedan fallar, no

Tres veces apareció el mismo problema con tres caras distintas.

En H7, el trabajador en segundo plano arrancaba durante las pruebas pese a estar apagado por configuración. Las pruebas pasaban, pero el bucle procesaba tareas mientras la prueba creía tener el control: el resultado dependía de quién llegara primero. **No producía ningún error.** Ochenta y ocho pruebas en verde que pasaban por suerte.

En H8, la prueba que contrasta el contrato escrito a mano con el generado pasó a la primera. Antes de celebrarlo hubo que comprobar que comparaba algo: dos conjuntos vacíos tampoco tienen diferencias entre sí.

Y la revisión de trazabilidad destapó el tercero, que es el peor. Los catálogos de emisor, adquirentes y productos se usaban en casi todas las pruebas, siempre igual: crear uno para poder emitir. **Consultar, modificar y desactivar no los probaba nadie.** La suite crecía hito a hito, el recuento subía, y tres verbos de tres requerimientos "Debe" estaban sin tocar.

Un requerimiento cubierto de rebote no está cubierto: está sin probar y con suerte. Y no se ve mirando el color de la suite, porque no es un fallo sino una ausencia.

### 2.4 Los comentarios envejecen peor que el código

`DatosTributarios` documentaba desde H1 que cada documento guarda una **copia** de los datos de las partes. Los manejadores pasaban la referencia. El comentario decía una cosa y el código hacía otra, y así estuvo seis hitos.

No lo encontró una revisión: lo encontró Entity Framework, avisando en cada escritura de que rastreaba el mismo objeto bajo dos identidades. El aviso llevaba tiempo saliendo mezclado con el resto de los registros.

---

## 3. Lo que salió mejor de lo esperado

### 3.1 Escribir el contrato antes que el código

`api/openapi.yaml` se escribió en H0, antes de que existiera un solo endpoint. Parecía un ejercicio de documentación y resultó ser lo contrario: durante ocho hitos se implementó **contra algo** en vez de inventar sobre la marcha.

La prueba de que funcionó llegó en H8, cuando se contrastó contra el documento que genera el código: **la superficie coincide exactamente.** Más de veinte operaciones, ninguna sobra y ninguna falta. Ocho hitos después.

Eso no pasó por suerte. Pasó porque el contrato se actualizaba en el mismo commit que el código que lo cambiaba.

### 3.2 Los ADR con sus alternativas descartadas

Cada ADR lleva una tabla de lo que **no** se hizo y por qué. Esa tabla resultó ser la parte más útil, y no por documentar: por obligar a mirar de frente las opciones antes de elegir.

En ADR-0013 esa tabla descartó los bloqueos consultivos de PostgreSQL por una razón que no se había visto al empezar a escribirlo — que se liberan al cerrarse la conexión, y con un pozo de conexiones ese momento deja de ser evidente.

### 3.3 Romper los mecanismos a propósito

Cada mecanismo de concurrencia se verificó quitándolo y comprobando que la prueba se ponía roja. Sonaba a ceremonia y enseñó algo que ninguna prueba en verde habría enseñado: **los dos mecanismos fallan de forma distinta.**

Sin el bloqueo de numeración, el índice único rechaza el duplicado y el sistema falla ruidosamente: nada incorrecto llega a guardarse. Sin el bloqueo de las notas crédito no hay red debajo; no se lanza ninguna excepción, no se registra ningún error, y la factura queda acreditada por el doble de su valor.

El segundo es infinitamente peor, y esa diferencia solo se ve rompiéndolo. Quedó escrita en ADR-0010.

---

## 4. Qué haría diferente

**Probar el entorno de desarrollo el primer día.** El plan tenía nueve riesgos y ninguno era del entorno. Media hora en H0 corriendo la suite vacía en la máquina definitiva habría destapado Smart App Control cuando no costaba nada.

**Revisar la trazabilidad en cada hito, no al final.** El repaso de H8 encontró cuatro requerimientos "Debe" sin verificar. Ese repaso son veinte minutos si se hace al cerrar cada hito, y la mitad del trabajo de un hito entero si se deja para el último.

**Contrastar el contrato generado desde H1.** La prueba que compara los dos documentos se escribió en H8 y pasó a la primera. Escrita en H1 habría costado lo mismo y habría vigilado ocho hitos en vez de uno.

**Dudar de una prueba nueva que pasa a la primera.** Pasó tres veces y las tres había algo. No significa que esté mal; significa que todavía no se sabe si puede fallar.

**Registrar la deuda con más disciplina.** Se fue anotando en la conversación y se recuperó al final por memoria. Un archivo con una línea por deuda desde H0 habría costado nada y no habría dependido de acordarse.

---

## 5. Lo que este proyecto no es

Se repite aquí porque es lo primero que debería saber quien lo mire.

- **No ha emitido una factura ante la DIAN.** Todo corre contra un simulador. La habilitación exige un proceso formal con certificado de entidad autorizada, y no se ha hecho.
- **El XML valida contra el esquema oficial, y eso no es lo mismo que ser aceptado.** El esquema comprueba estructura, no reglas de negocio de la autoridad.
- **La firma sigue el anexo técnico y no se ha verificado contra la DIAN.** El certificado de las pruebas es autofirmado.
- **Los códigos de municipio y unidad de medida no se validan contra las listas oficiales.** Se comprueba que vengan, no que existan.
- **RNF-03 no se ha medido nunca.** No hay prueba de carga.

Declararlo no le quita valor al ejercicio. Lo que se lo quitaría es dejar que alguien supusiera lo contrario.

---

## 6. Lo que queda escrito para la próxima vez

Frases que salieron de errores concretos y que valen fuera de este proyecto:

1. **Cuando quitas la causa sospechada y el error no cambia, la sospecha era falsa.** No sigas con ella.
2. **Una prueba que no puede fallar no está probando.** Rompe el mecanismo y comprueba que se pone roja.
3. **Un requerimiento cubierto de rebote no está cubierto.** Usarlo como preparación de otra prueba no es probarlo.
4. **El fallo silencioso es peor que el ruidoso.** Un error que nadie ve no deja de ocurrir.
5. **Antes de teorizar, pregúntale al sistema.** El registro de eventos, el log, la excepción completa. Suele saberlo.
6. **Un ejemplo documentado que nadie ejecuta es una prueba que nadie corre.** *(Añadida en la auditoría final.)*
7. **El documento que certifica la cobertura también necesita que alguien lo verifique.** *(Añadida en la auditoría final.)*


---

## 7. La auditoría final

Con el proyecto declarado completo se hizo una auditoría por pasadas: primero los hallazgos, después los arreglos, cada uno con una prueba escrita antes y en rojo. Lo que encontró corrige alguna afirmación de este mismo documento, y por eso va aquí y no en otro sitio.

### 7.1 Datos falsos que no avisaban

Los dos peores hallazgos tenían la misma forma: el sistema producía un dato falso, todo seguía en verde y nada fallaba.

- **El CUFE con la clave de otro rango.** El rango se buscaba por prefijo y tipo, que no es único: la autorización de este año y la del anterior pueden compartirlos. La prueba lo reprodujo de punta a punta: la factura se generó, se firmó, se transmitió y quedó **aprobada** con un código único calculado con la clave del rango vencido.
- **La fecha en UTC.** Una factura de las 19:30 del 31 de diciembre se numeraba con el rango del año siguiente mientras su XML decía 31 de diciembre. El listado ya sabía que había que usar la hora colombiana, y lo explicaba en un comentario; los otros cuatro sitios no lo hacían.

Detrás de ellos hubo más del mismo tipo: documentos que se quedaban sin estado final para siempre, un historial que decía "no consta que llegara" de documentos que la autoridad sí había recibido, y un XML cuyo impuesto total no era la suma de sus propios subtotales.

### 7.2 Nadie siguió nunca su propia documentación

**El ejemplo del propio contrato respondía 500.** `openapi.yaml` enseña a mandar la fecha como `"2026-09-24T14:30:00-05:00"`, que es como la mandaría cualquier integrador colombiano, y PostgreSQL rechaza en esas columnas cualquier desfase distinto de cero. Ninguna prueba enviaba nunca `fechaEmision`.

No fue un caso aislado. El recorrido del README tenía dos pasos 7 y, después de que el trabajador en segundo plano ya hubiera generado y firmado el documento, pedía generarlo y firmarlo a mano; seguido al pie de la letra, respondía `409`. Se escribió en H5 y H6 y nadie lo volvió a ejecutar después de H7.

La sección 3.1 dice que escribir el contrato antes que el código salió mejor de lo esperado, y es verdad para la **superficie**: rutas y métodos coinciden, y hay una prueba que lo vigila. Pero esa prueba compara qué operaciones existen, no si los ejemplos funcionan. **Un ejemplo documentado que nadie ejecuta es una prueba que nadie corre**, y se pudre igual que un comentario (sección 2.4), solo que desde un sitio con más autoridad: es lo primero que copia quien integra.

### 7.3 Pruebas que no podían fallar, otra vez

La sección 2.3 contaba tres. La auditoría encontró más:

- `EstaAbandonada` tenía cuatro pruebas y la producción nunca la llamaba: el criterio real de abandono era una consulta SQL. Con la consulta rota, las cuatro seguían en verde. Se eliminó.
- La lista blanca de los endpoints de desarrollo solo se había probado por un lado: que existen bajo `Testing`. Que no existan en producción no lo comprobaba nadie, y dos de ellos sí existían.
- La primera versión de la prueba del redondeo por grupo **pasó la mutación** que debía detectar: con un solo grupo por impuesto, los dos cálculos daban lo mismo. Hizo falta un segundo grupo de IVA para que la comparación pudiera fallar. Sin la mutación, habría quedado una prueba verde que no vigilaba nada.
- `09-trazabilidad.md` daba RNF-10 por verificado con una prueba que miraba otra cosa. Ese documento lo cuenta ahora en su propia sección.

### 7.4 Un arreglo que rompió otra cosa

El arreglo de A1 sacó la carga del documento fuera de un `try` para que el `catch` pudiera usarlo. Eso abrió un camino por el que un documento ilegible hacía girar su tarea para siempre. Las pruebas en verde no lo detectaron. Lo detectó una pregunta: *¿y si el fallo es permanente?*

Escribir la prueba antes de opinar encontró dos caminos, el introducido y otro que existía desde H7. Comprobar cuál era cuál —corriendo la prueba contra el código anterior— impidió atribuirle al arreglo más culpa de la que tenía, o menos.

### 7.5 Deuda que queda registrada

Una línea por deuda, como la sección 4 dice que debió hacerse desde H0:

- **`Documento.Transicionar` es público.** Permite llegar a `RECHAZADO` sin errores o a `TRANSMITIDO` sin transmisión. La producción no lo usa (el procesador mueve los documentos con los métodos que además registran lo que pasó), pero lo usan el endpoint de desarrollo y las pruebas de dominio. Cerrarlo obliga a reescribir esas pruebas para que recorran los caminos reales; al cierre no compensa.
- **Cada repositorio tiene su `GuardarCambiosAsync`, y todos guardan el contexto entero.** La emisión guarda la tarea y el contador del rango "a través" del repositorio de documentos, y un comentario lo tiene que aclarar. La operación pertenece a `IUnidadDeTrabajo`.
- **El documento no guarda el rango que lo numeró.** La clave técnica se busca deduciendo el rango por prefijo, tipo y número. Es correcto porque INV-RAN-03 impide que dos rangos compartan números, pero guardar `RangoId` sería explícito. Exige migración y tocar el agregado.
- **El alta de integradores no existe fuera de `Development`.** Declarado en `08-despliegue.md`, sección 6.
- **RF-11 y RF-24 están implementados solo en parte.** Declarado en `09-trazabilidad.md`.
- **`cac:BillingReference` de las notas lleva el identificador interno de la factura**, no su número ni su CUFE. Declarado en `07-cobertura-ubl.md`.

Lo que la auditoría encontró y decidió no arreglar, con el motivo de cada cosa, está en el cuerpo del pull request de la auditoría. Aquí queda solo lo que es deuda.
