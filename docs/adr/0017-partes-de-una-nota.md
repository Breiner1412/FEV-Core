# ADR-0017: El adquirente de una nota es el de la factura; el emisor, el de hoy

**Estado:** Aceptada
**Fecha:** 2026-10-02

## Contexto

Una nota crédito o débito declara, como cualquier documento, los datos de las dos partes: emisor y adquirente. Cada documento guarda una copia de esos datos tal como estaban al emitirse *(RN-10)*. La pregunta es de dónde sale esa copia en una nota, que corrige una factura emitida antes y con datos que pueden haber cambiado desde entonces.

Hasta la auditoría final, `EmitirNotaHandler` hacía esto con el adquirente:

- La **identidad** (`adquirenteId`) salía de la factura. El dominio la heredaba, con un comentario: "no se corrige una factura a nombre de otro".
- Los **datos** (razón social, identificación tributaria, dirección…) salían del **catálogo actual**, consultado por ese identificador.

Esa mezcla es incoherente de las dos formas. La identidad dice "el de la factura" y el contenido dice "el de hoy". Si el adquirente cambió su identificación después de facturar, la nota llevaba el identificador interno de la factura con la identificación tributaria nueva: dos documentos de la misma operación declarando partes distintas. La auditoría lo reprodujo.

## Decisión

**Cada parte sale de un solo sitio, pero no del mismo sitio para las dos.**

- **Adquirente: de la factura.** Identidad y datos. La nota copia los datos que declaró la factura que corrige.
- **Emisor: el actual.** Los datos de hoy, como en cualquier documento nuevo.

Lo que se arregla no es que una fuente gane a otra. Es que la identidad y los datos de una misma parte salgan del mismo sitio.

### Por qué las dos partes se tratan distinto

No es obvio, y de hecho la versión anterior las trataba igual: las dos del catálogo actual.

El **adquirente es parte de la operación que se corrige**. Una nota crédito corrige una venta concreta, hecha a un comprador concreto, tal como se declaró. Si el comprador se mudó o cambió de razón social, la venta no cambió: fue a nombre de quien figuraba entonces. Declarar en la nota otros datos sería declarar otra operación.

El **emisor es quien expide el documento nuevo**, hoy. La nota es un documento propio, con su propia fecha, su propio número y su propia firma, y quien la expide es el emisor tal como es ahora: con su dirección actual y sus responsabilidades tributarias vigentes. Una factura de hoy no lleva la dirección que el emisor tenía hace un año, y una nota de hoy tampoco.

Dicho de otra forma: el adquirente pertenece al **pasado** que se corrige; el emisor, al **presente** en que se corrige.

### Dónde vive

En `Documento.EmitirNota`, en el dominio, que ya heredaba de la factura la moneda y el `adquirenteId`. Ahora también copia ahí los datos del adquirente, y por eso dejó de recibirlos como parámetro: ningún caso de uso puede volver a mezclarlos. El emisor sigue entrando como parámetro, porque sus datos actuales no los conoce el documento.

Como efecto secundario, `EmitirNotaHandler` ya no consulta el catálogo de adquirentes, y esa consulta ocurría con la factura bloqueada.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| Las dos partes del catálogo actual, incluida la identidad del adquirente | Coherente consigo misma, pero la nota podría declarar un comprador distinto del de la operación que corrige. Contradice la razón por la que la nota hereda el adquirente. |
| Las dos partes de la factura, también el emisor | La nota se expediría con datos del emisor que ya no son los suyos: una dirección o unas responsabilidades que dejó de tener. Es un documento nuevo, y lo expide quien es hoy. |
| Mantener la mezcla: identidad de la factura, datos del catálogo | Es lo que había. Identidad y contenido de una misma parte dicen cosas distintas. |
| Exigir que el adquirente no haya cambiado desde la factura | Convierte un cambio legítimo, como una mudanza, en un impedimento para corregir una venta. Desactivar o modificar a un adquirente impide venderle de nuevo con los datos viejos, no corregir lo que ya se le vendió. |

## Lo que NO se ha verificado

**No se ha comprobado contra el anexo técnico de la DIAN que esto sea lo que la autoridad exige** para las partes de una nota crédito o débito. El razonamiento es de dominio —qué representa cada parte en una corrección—, no de lectura de la norma.

Es la misma limitación que ya se declara del CUFE y del CUDE en `docs/07-cobertura-ubl.md` y del redondeo en ADR-0016. Cualquier uso real exige contrastarlo con el anexo vigente.

## Consecuencias

**Positivas**
- La factura y la nota que la corrige declaran el mismo comprador, por construcción.
- La regla vive en el dominio, donde se puede defender sola.
- La emisión de una nota hace una consulta menos con la factura bloqueada.

**Negativas**
- Si los datos del adquirente en la factura eran erróneos, la nota los repite. Corregir la identificación del comprador no es lo que hace una nota; si hiciera falta, es otro procedimiento que este sistema no implementa.
- Las notas emitidas antes de este cambio conservan los datos del adquirente que tenía el catálogo en ese momento.

## Verificación

- `PartesDeLaNotaTests.El_adquirente_de_la_nota_es_el_de_la_factura_aunque_el_catalogo_haya_cambiado`: después de facturar, el adquirente cambia de dirección y de identificación, y la nota declara exactamente los datos de la factura. Estaba en rojo antes del cambio, con el identificador de la factura y la identificación nueva.
- `PartesDeLaNotaTests.El_emisor_de_la_nota_es_el_actual_aunque_haya_cambiado_desde_la_factura`: fija la otra mitad de la decisión.
- `NotasTests.La_nota_hereda_identidad_y_datos_del_adquirente_de_la_factura_como_copia` (dominio): son iguales y no son la misma instancia. Verificado por mutación: sin la copia, falla.
