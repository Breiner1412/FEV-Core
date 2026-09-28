# ADR-0011: XML construido a mano sobre un subconjunto mínimo

**Estado:** Aceptada
**Fecha:** 2026-09-28

## Contexto

RF-16 exige generar el documento en formato UBL 2.1 con las adendas colombianas. UBL 2.1 define más de sesenta tipos de documento y varios miles de elementos; una factura de este proyecto usa alrededor del uno por ciento.

El plan de entregas marcó esta etapa como la de mayor riesgo de desbordarse, y R-01 nombra exactamente ese peligro: la tentación de implementar el estándar completo.

## Decisión

El XML se construye a mano con `XDocument`, escribiendo solo los elementos necesarios, y se declara por escrito qué quedó dentro y qué fuera en `docs/07-cobertura-ubl.md`.

Los esquemas oficiales de OASIS se versionan en `schemas/ubl-2.1/` y una prueba valida contra ellos cada documento generado.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| Generar clases C# desde el XSD | Produce decenas de miles de líneas para usar una fracción mínima. El código generado no se revisa, no se entiende y oculta justo la parte que hay que conocer: qué campos exige la norma y por qué. |
| Plantilla de texto con sustitución | Se rompe con listas de longitud variable —las líneas de la factura— y con los elementos que solo aparecen en algunos documentos. |
| Una biblioteca de terceros para UBL colombiano | Resolvería el problema sin enseñar nada, y ataría el proyecto a la interpretación que esa biblioteca haga de un anexo que cambia. |
| Descargar los esquemas al construir | La validación dejaría de ser reproducible: el día que OASIS cambie una URL, el proyecto no se podría construir. |

## Consecuencias

**Positivas**
- El generador se lee entero y cada elemento tiene una razón de estar.
- La validación contra el esquema es reproducible sin red.
- Lo omitido está declarado, que es lo que R-01 pedía.

**Negativas**
- El orden de los elementos hay que respetarlo a mano. El esquema los declara como secuencia, así que uno correcto en la posición equivocada invalida el documento. La prueba de validación es la única defensa, y sin ella este enfoque sería temerario.
- Ampliar la cobertura obliga a escribir cada elemento nuevo.
- Unos megas de archivos ajenos en el repositorio.

## Lo que la validación contra el esquema NO garantiza

Que un documento valide significa que su **estructura** es correcta: los elementos existen, están anidados como corresponde y sus tipos cuadran.

No significa que la DIAN lo aceptaría. El anexo técnico añade centenares de validaciones de negocio que ningún XSD expresa. Confundir las dos cosas —en el README o en una entrevista— sería una afirmación falsa sobre el alcance del proyecto.

## Nota sobre el código único

El CUFE se calcula sobre los valores **tal como quedan escritos en el XML**, no sobre los números del dominio. Por eso existe `ValoresCufe`: formatea una sola vez y alimenta tanto el hash como el documento. Si cada camino formateara por su cuenta, una diferencia mínima —la hora en otra zona, un decimal de más— haría que la autoridad recalculara el código desde el XML recibido y obtuviera otro, con un rechazo imposible de diagnosticar.

Las limitaciones del cálculo están declaradas en `docs/07-cobertura-ubl.md`, sección 3.
