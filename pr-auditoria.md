Auditoría final del proyecto, después de declararlo completo.

## Cómo se hizo

Por pasadas. La primera solo buscó, sin tocar código, y presentó los hallazgos. La segunda arregló los aprobados en el orden decidido, con una regla fija: **primero la prueba, en rojo contra el código anterior, después el arreglo**. Las pruebas de concurrencia, y las que podían pasar sin vigilar nada, se verificaron además por mutación: romper a propósito el mecanismo y comprobar que la prueba se pone roja.

Las decisiones registradas en `docs/adr/` no se cambiaron. Donde una estaba mal, se escribió otra que la sustituye.

| | Antes | Después |
|---|---|---|
| Pruebas | 361 | **400**, todas en verde |
| ADR | 14 | **17** (ADR-0015 sustituye a ADR-0014; ADR-0016 y ADR-0017 nuevas) |
| Migraciones | 8 | 9 |

`94 files changed, 3928 insertions(+), 627 deletions(-)`, en 25 commits agrupados por tema.

---

## Los tres errores que producían datos falsos sin avisar

Son lo más importante del PR. Los tres tienen la misma forma: el sistema producía un dato falso, la suite seguía en verde y nada fallaba.

### A3 — El CUFE se calculaba con la clave técnica de otro rango

`GenerarXmlHandler` buscaba el rango del documento por prefijo y tipo, con un `FirstOrDefault` sin orden, y el comentario afirmaba que esa pareja era única. No lo es: la autorización de este año y la del anterior pueden compartir prefijo, cada una con su clave.

La prueba lo reprodujo de punta a punta. Con un rango vencido y uno vigente del mismo prefijo, la factura se generó, se firmó, se transmitió y **quedó APROBADA con un código único calculado con la clave del rango vencido**.

Arreglo (`f71f8ee`): el rango se busca por prefijo, tipo y número, con `SingleOrDefault`.

### A1 — Documentos sin estado final, para siempre

Ante una excepción no prevista, `ProcesadorTareas` agotaba la tarea sin pasarle el documento a quien lo marca como `FALLIDO`. La tarea se cerraba y el documento se quedaba en `EN_PROCESO` o `TRANSMITIDO`: sin trabajo pendiente y sin desenlace. La API respondía "en proceso" indefinidamente. Es el estado indeterminado que CE-04 prohíbe, y el comentario decía que el `catch` existía para evitarlo.

Lo provocaban un certificado ausente o vencido, un rango sin clave técnica y un rechazo de la autoridad con la lista de errores vacía.

Arreglo (`64ba86a`, `a8c8112`): el `catch` recibe el documento, y el documento pasa a `EN_PROCESO` **antes** de generar su XML. Antes lo hacía después, y un fallo al generar lo dejaba en `RECIBIDO`, desde donde la máquina de estados no permite `FALLIDO`.

### G2 — El XML declaraba un impuesto total que no era la suma de sus subtotales

El impuesto total se redondeaba una vez sobre la suma exacta, mientras que los `TaxSubtotal` del XML y los valores del CUFE se redondeaban cada uno por su cuenta. Con varios grupos de impuesto a medio centavo no cuadraban: los subtotales sumaban 2,02 y su `TaxAmount` decía 2,01. El CUFE declaraba un `ValTot` que no era la suma de sus partes. En UBL, el `TaxAmount` de un `TaxTotal` **es** la suma de sus subtotales.

Arreglo (`d6c9c18`, ADR-0016): un solo cálculo por grupo de impuesto, del que salen los totales, el XML y el CUFE. RN-06 cambia de redacción y no de intención: sigue sin redondearse línea por línea. **No se ha verificado contra el anexo técnico de la DIAN que esto sea lo que la autoridad compara.**

### Del mismo tipo, menos graves

- **La fecha en UTC** (`9939c2e`). Una factura de las 19:30 del 31 de diciembre se numeraba con el rango del año siguiente, mientras su XML decía 31 de diciembre.
- **El historial de un `FALLIDO` decía "no consta que el documento llegara"** (`5409c12`) también cuando la autoridad sí lo había recibido y devuelto con qué consultarlo.

---

## La trazabilidad afirmaba cobertura que no existía

`docs/09-trazabilidad.md` se presenta como el documento que no maquilla nada: "no rellenados con algo que sonara bien". **Afirmaba cobertura que no existía.**

- **RNF-10 figuraba como verificado.** La prueba citada solo comprueba el `traceId` de las respuestas de error. El requerimiento pide seguir un documento por sus registros, y ningún registro llevaba el documento. Además, la consola escribía texto y descartaba los *scopes*: ni siquiera el `traceId` llegaba a la salida. Era una prueba con el identificador correcto, en verde, que verificaba otra cosa.
- **RF-11 figuraba como verificado.** La forma de pago nunca se implementó, y `07-cobertura-ubl.md` lo decía desde la etapa 5.
- **RF-24 figuraba como verificado.** El máximo por página es fijo, no configurable como pide el criterio.
- **RN-11, RN-12 y RN-13 tenían la descripción vacía.** El barrido automático solo leyó una tabla y nadie revisó lo que produjo.
- **RNF-01 se atribuía a `EmitirFacturaHandler`** porque un comentario citaba RNF-01 al hablar de rendimiento.

No se corrigió en silencio (`d4a03e8`). El documento dice al principio que lo hizo y al final qué afirmaba y qué pasaba de verdad. RNF-10 se implementó y se probó (`ed2276d`), en vez de declararlo "sin verificación automática" junto a RNF-03 y RNF-08: esos se cree que se cumplen y falta la prueba; RNF-10 no se cumplía.

---

## Resumen de los arreglos

| Commit | Hallazgo | Prueba |
|---|---|---|
| `64ba86a` | A1: un fallo no previsto dejaba el documento sin estado final | `CicloCompletoTests` (2), `ProveedorValidacionHttpTests` |
| `f71f8ee` | A3: el CUFE con la clave de otro rango | `RangoDelDocumentoTests` |
| `d3fc3e7` | Una tarea que fallaba siempre giraba sin fin (ver *Errores del propio proceso*) | `DocumentoIlegibleTests`, `FalloAlRendirseTests` |
| `a8c8112` | Un fallo al generar el XML dejaba el documento en `RECIBIDO` para siempre | `FalloAlGenerarTests` |
| `057d73a` | A8: el ejemplo del propio contrato (`fechaEmision` con `-05:00`) respondía 500 | `FacturasEndpointTests` |
| `9939c2e`, `16ee83c` | A2: la vigencia del rango y RF-10 en UTC | `FechaColombianaTests` (factura, nota, RF-10), `HoraColombiaTests` |
| `7245919` | A4: reintentos simultáneos con la misma referencia respondían 500 | `ReintentosSimultaneosTests` |
| `8418d7e` | A5: un descuento negativo respondía 500 | `FacturasEndpointTests` |
| `29f32e3` | F2: `EstaAbandonada`, cuatro pruebas que no podían fallar | (eliminada) |
| `9a35c3a` | A6: la unicidad de adquirentes y productos no estaba en la base | `UnicidadCatalogosTests` |
| `d6c9c18` | G2: el XML y el CUFE no cuadraban (ADR-0016) | `GeneracionXmlTests` |
| `5409c12`, `d4bfa66` | A7: el historial de un `FALLIDO` inventaba; ADR-0015 | `DesenlaceFallidoTests` y el texto del historial de punta a punta |
| `248905e` | B3: generar y firmar por HTTP estaban abiertos en producción | `EndpointsSoloDesarrolloTests` |
| `ed2276d` | RNF-10 no se cumplía | `RegistrosPorDocumentoTests` |
| `6090f0c` | Contraseña escrita en `FabricaDbContextDisenio` | `FabricaDisenioTests` |
| `8ec4aff` | D3: la nota mezclaba la identidad del adquirente de la factura con sus datos actuales (ADR-0017) | `PartesDeLaNotaTests`, `NotasTests` |
| `1277b62`, `3b185e1`, `9327e1c` | B8: comentarios que contaban otra cosa que el código | `LineaTests` (un caso se arregló en el código) |
| `3a65ba3` | F1: código sin llamador en producción | — |
| `d4a03e8`, `8d5e0f1`, `f568373` | B1, B6, B7 y la retrospectiva | — |

---

## Decisiones que conviene revisar

- **ADR-0015 sustituye a ADR-0014**, que se conserva con una nota.
  - Un 202 o 200 sin identificador de seguimiento utilizable es `SinRespuesta`, no `ErrorTransitorio`: un 2xx dice que el documento llegó.
  - `FALLIDO` deja de significar siempre "resultado desconocido". Significa "sin desenlace, hace falta una persona", y el historial dice cuál de cuatro casos fue. Solo uno, `RESULTADO DESCONOCIDO`, obliga a verificar ante la DIAN antes de reemplazar el documento. Antes, todo `FALLIDO` mandaba a investigar ante la autoridad, incluido el que falló por falta de un rango.
- **ADR-0016**: el impuesto se redondea por grupo y el total es la suma de los grupos. Declara que no está verificado contra la DIAN.
- **ADR-0017**: el adquirente de una nota es el de la factura, en identidad y datos; el emisor es el de hoy. El adquirente es parte de la operación que se corrige y el emisor expide el documento nuevo. Declara que no está verificado contra la DIAN.
- **El documento pasa a `EN_PROCESO` antes de generar el XML**, no después. Es la definición de la sección 6.1 de los requerimientos. El comentario de diseño que decía lo contrario se reescribió explicando por qué estaba equivocado, en vez de borrarse.
- **`POST /documentos/{id}/xml` y `/firma` solo existen en `Development` y `Testing`**. Salen del contrato publicado.
- **Registros en JSON con *scopes***, para RNF-10.
- **Migración `IndicesUnicosCatalogos`**: falla a propósito si una base ya tiene adquirentes o productos activos duplicados. Crear el índice saltándose esas filas haría cumplir la regla para lo nuevo y no para lo que ya está mal. `08-despliegue.md` explica cómo encontrarlos y resolverlos antes.

---

## Errores del propio proceso

Se dejan escritos por la misma razón que el resto.

- **El arreglo de A1 introdujo un bucle infinito.** Para que el `catch` pudiera usar el documento, la carga salió fuera del `try`. Un documento ilegible hacía que la tarea se reintentara para siempre. La suite estaba en verde; lo detectó una pregunta en la revisión: *¿y si el fallo es permanente?* La prueba escrita para contestarla encontró dos caminos: el introducido y otro que existía desde H7. Corrida contra el código anterior, la prueba distinguió cuál era cuál.
- **La primera prueba de G2 pasó la mutación que debía detectar.** Con un solo grupo de impuesto por código, los dos cálculos daban lo mismo. Hizo falta un segundo grupo de IVA para que la comparación pudiera fallar.
- **La primera prueba de la frontera de fecha no se comportó como esperaba.** El 500 que reveló A8 apareció ahí, y se trató como hallazgo propio en su propio commit, no como ruido.

---

## Lo que se encontró y NO se arregló

La lista vale tanto como los arreglos.

**Por decisión explícita, registrado como deuda en `docs/10-retrospectiva.md`, sección 7.5:**

1. **`Documento.Transicionar` es público.** Permite llegar a `RECHAZADO` sin errores o a `TRANSMITIDO` sin transmisión. La producción no lo usa; lo usan el endpoint de desarrollo y las pruebas de dominio, y cerrarlo obliga a reescribirlas para que recorran los caminos reales. Al cierre no compensa.
2. **Cada repositorio tiene su `GuardarCambiosAsync`, y todos guardan el contexto entero.** La operación pertenece a `IUnidadDeTrabajo`.
3. **El documento no guarda el rango que lo numeró.** A3 se arregló deduciéndolo por prefijo, tipo y número, lo cual es correcto por INV-RAN-03. Guardar `RangoId` sería más explícito, pero exige migración y tocar el agregado.

**Declarado en la documentación, no implementado:**

4. **No hay alta de integradores fuera de `Development`** (`08-despliegue.md` §6). En cualquier otro entorno la API arranca y nadie puede usarla. El dominio ya sabe crear y desactivar integradores (`Integrador.Crear`, `Desactivar`, con pruebas): por eso no se eliminaron como código muerto.
5. **`cac:BillingReference` de las notas lleva el GUID interno de la factura**, no su número, su CUFE ni su fecha (`07-cobertura-ubl.md`). El XML valida contra el esquema, pero no le dice a la autoridad qué factura corrige. Arreglarlo exige que el generador reciba los datos de la factura referenciada.
6. **RF-11: la forma de pago no existe** (`09-trazabilidad.md`). Falta decidir si se añade al dominio o se retira del requerimiento.
7. **RF-24: el máximo por página es fijo**, no configurable.

**Encontrado, no arreglado, con el motivo:**

8. **El procesador carga el agregado completo tres veces por tarea** (procesador, generar, firmar), con las dos columnas de XML. El comportamiento es correcto y RNF-03 nunca se ha medido; optimizar sin una medición sería adivinar.
9. **`GET /documentos/{id}` y `/historial` cargan el XML y las transmisiones sin usarlos.** El mismo motivo.
10. **`ProveedorCertificadoConfiguracion` descarta la excepción interna** y envuelve un error de configuración del servidor en `ExcepcionDominio`, que por HTTP sería un 409. Desde B3 solo lo alcanza el trabajador, que lo registra, y no llega a ninguna respuesta. Es una mejora de diagnóstico pendiente.
11. **`Dian:Ambiente` cae en silencio a `Pruebas`** si el valor no se reconoce, y `Enum.TryParse` acepta números fuera del enumerado. Equivocarse hacia `Pruebas` es el lado barato, pero sigue sin avisar.
12. **Duplicaciones sin defecto conocido:**
    - El 404 se arma en cinco controladores.
    - La paginación de los catálogos se limita en el controlador y la de documentos en el caso de uso.
    - Hay validaciones repetidas en `Emisor.Crear` y `Actualizar`.
    - `DOCUMENTO_SIN_LINEAS` y `XML_NO_DISPONIBLE` se comprueban en la aplicación y también en el dominio.

    La copia que había divergido, el 404 del endpoint de desarrollo, sí se arregló.
13. **`openapi.yaml` declara el dinero como `number/double`** en 19 campos, frente a RNF-12. Cambiarlo es un cambio de contrato. System.Text.Json serializa `decimal` sin pérdida, así que el defecto está en el documento, no en el dato.
14. **`RangoNumeracion.SeSolapaCon` es más estricto que RF-08**: rechaza rangos de prefijos distintos que coinciden en fechas, sin una ADR que lo registre. El comentario lo justifica.
15. **Restos que las propias ADR reconocen:**
    - ADR-0015: un proveedor que no sea HTTP y devuelva un identificador de seguimiento demasiado largo seguiría sin dejar rastro de la transmisión.
    - ADR-0015: un apagado justo en mitad de una transmisión no la registra como sin respuesta.
    - ADR-0016: hay un caso transitorio para documentos en curso en el momento del despliegue.
16. **Los datos ya guardados no se migran:**
    - Los `FALLIDO` anteriores conservan el texto viejo. "No consta que el documento llegara" en un documento antiguo **no** garantiza que no llegara.
    - Las notas anteriores conservan el adquirente del catálogo.
    - Los totales de impuesto anteriores conservan el redondeo anterior.
17. **El catálogo admite productos con precio 0**, mientras que una línea exige precio mayor que cero. Un producto así solo se puede facturar negociando el precio; sin hacerlo, la emisión responde 409.
18. **La lección de A8 no está automatizada.** Ninguna prueba envía literalmente los ejemplos de `openapi.yaml`. La de A8 envía un caso equivalente, no el ejemplo del contrato.
19. **Pendiente de decisión: la llave de API de desarrollo está escrita en el código** (`SemillaDesarrollo.LlavePorDefecto`, publicada en el README). Es el mismo caso que la contraseña de la fábrica de diseño (RNF-01 nombra las llaves de API). No se tocó porque arrastra el recorrido del README y el `docker compose`.

---

## Cómo verificarlo

```bash
dotnet test
```

400 pruebas, con Docker en marcha para Testcontainers.

Quien aplique la migración sobre una base con datos debe leer antes `docs/08-despliegue.md`, sección 4. Las herramientas de EF ya no tienen contraseña de respaldo: necesitan `ConnectionStrings__Principal` o un `.env`.

Este archivo se borra antes de fusionar, como los de los hitos.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
