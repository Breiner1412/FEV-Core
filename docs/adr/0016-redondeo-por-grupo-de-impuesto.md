# ADR-0016: El impuesto se redondea por grupo, y el total es la suma de los grupos

**Estado:** Aceptada
**Fecha:** 2026-10-02
**Precisa:** RN-06 (`docs/02-requerimientos.md`)

## Contexto

RN-06 decía:

> El impuesto se calcula sobre la base gravable de cada línea aplicando la tarifa del producto, pero el redondeo se aplica sobre el total del documento, no línea por línea.

El código la cumplía al pie de la letra: el impuesto total era la suma exacta de todos los impuestos de todas las líneas, redondeada una sola vez. Pero un documento no declara un impuesto total suelto. Lo declara en dos sitios más, cada uno calculado por su cuenta:

- **El XML.** Un `cac:TaxTotal` lleva su `cbc:TaxAmount` y, debajo, un `cac:TaxSubtotal` por cada tipo y tarifa, cada uno con su propio `TaxAmount`. El generador redondeaba cada subtotal por separado.
- **El CUFE.** Lleva `ValIva` y `ValInc`, que se redondeaban cada uno por separado, y `ValTot`, que salía del total redondeado una vez.

Con un solo grupo de impuesto, los tres cálculos coinciden. Con varios grupos que caen en medio centavo, no. La auditoría final lo reprodujo con IVA 19 % sobre 10,50 (1,995), IVA 5 % sobre 0,10 (0,005) e INC 8 % sobre 0,0625 (0,005):

| | Antes |
|---|---|
| Subtotales del XML | 2,00 + 0,01 + 0,01 = **2,02** |
| `TaxAmount` del XML | **2,01** |
| `ValIva` + `ValInc` del CUFE | 2,00 + 0,01 = **2,01** |

El XML declaraba un total que no era la suma de sus propios subtotales, y el CUFE uno que no era la suma de sus partes.

### Por qué la redacción de RN-06 era imprecisa

En UBL, el `TaxAmount` de un `TaxTotal` **es** la suma de sus `TaxSubtotal`. No es una política de redondeo entre varias posibles: es lo que significa el elemento. "Redondear sobre el total del documento" pide que ese total sea el redondeo de la suma exacta, y eso es incompatible con que sea la suma de unos subtotales que también se escriben redondeados. RN-06 pedía dos cosas que solo coinciden cuando hay un grupo.

### Por qué "por grupo" conserva la intención

RN-06 existe para impedir el redondeo **línea por línea**, que acumula un error por cada línea: tres líneas de 190,0019 dan 570,00 redondeando cada una y 570,01 redondeando el total. Eso sigue prohibido. Un grupo de impuesto suma las líneas con precisión completa y se redondea una sola vez, así que el error no crece con el número de líneas: como mucho, medio centavo por grupo, y un documento tiene pocos grupos. Cambia la redacción, no la intención.

## Decisión

Los impuestos se agrupan por **tipo y tarifa** sobre todo el documento, cada grupo se redondea **una vez**, y el impuesto total es **la suma de los grupos**.

- `SubtotalImpuesto.Agrupar` (dominio) es la única fuente. De él salen `Totales.TotalImpuestos`, los `TaxSubtotal` del XML y `ValIva`/`ValInc` del CUFE.
- `ValIva` es todo lo que el XML declara con el código DIAN `01` (IVA, IVA exento e IVA excluido) y `ValInc` lo que declara con `04`. Antes `ValIva` solo sumaba el tipo `Iva`, mientras el XML declaraba los tres bajo `01`. El código DIAN pasa al dominio, porque el CUFE lo necesita.
- En el `TaxTotal` de cada línea se aplica la misma regla: su `TaxAmount` es la suma de sus subtotales tal como se escriben.
- Los importes que no son impuestos (bruto, descuentos) siguen redondeándose una vez sobre el documento, como antes.

Con el ejemplo de arriba, el XML y el CUFE declaran 2,02, y todo cuadra.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| **Mantener RN-06 literal y repartir el centavo.** El total sigue siendo el redondeo de la suma exacta, y la diferencia se asigna a algún subtotal para que sumen. | Produce subtotales que ya no son "base por tarifa, redondeado". Cualquier validador que compruebe un subtotal por separado lo marcaría. Además, a qué grupo se le asigna el centavo es arbitrario: una regla fiscal que depende de un desempate no es una regla. |
| **Redondear por grupo** *(elegida)* | Cada cifra del documento se puede recalcular sola y todas cuadran entre sí. Exige precisar RN-06 y cambiar `Totales.Calcular`. |
| **No arreglarlo y declararlo** | El documento sería incoherente consigo mismo en cuanto tuviera dos grupos a medio centavo. No es un caso exótico: basta con dos tarifas distintas. |

## Lo que NO se ha verificado

**No se ha comprobado contra el anexo técnico de la DIAN que esto sea lo que la autoridad compara.** El razonamiento se apoya en la semántica de UBL (el `TaxAmount` de un `TaxTotal` es la suma de sus subtotales) y en que un documento debe ser coherente consigo mismo. No se apoya en haber leído la regla de validación de la DIAN que compara esos valores, ni en haber emitido un documento con varios grupos ante su entorno de pruebas.

Es la misma limitación que ya se declara del CUFE y del CUDE en `docs/07-cobertura-ubl.md`, y por la misma razón. Callarlo sería peor que cualquiera de las tres alternativas.

## Consecuencias

**Positivas**
- El XML, el CUFE y la respuesta de la API declaran el mismo impuesto, porque los tres lo leen de los mismos grupos.
- El código DIAN de cada impuesto existe en un solo sitio.

**Negativas**
- RN-06 cambia de redacción. Quien lea solo la versión anterior encontrará un código que no la cumple al pie de la letra; por eso RN-06 remite ahora a este ADR.
- Los documentos ya emitidos guardan su `TotalImpuestos` calculado con la regla anterior. Si alguno estaba en `RECIBIDO` al desplegar este cambio y tenía varios grupos a medio centavo, su XML se generará con subtotales de la regla nueva y un total de la anterior. Solo afecta a documentos en curso en ese momento.

## Verificación

`GeneracionXmlTests.El_xml_y_el_codigo_unico_cuadran_consigo_mismos_y_entre_ellos` comprueba tres cosas sobre el ejemplo de arriba: que los `TaxSubtotal` del XML suman su `TaxAmount`, que el `ValTot` del CUFE es `ValFac` más sus impuestos, y que el impuesto del CUFE es el del XML.

Se verificó por mutación:

- Con todo el código anterior a este ADR falla la primera comprobación (2,02 frente a 2,01).
- Con solo el cálculo anterior del CUFE falla la segunda (`ValTot` 12,68 frente a 12,67).

La primera versión de la prueba tenía un solo grupo por código DIAN, y con ella la mutación del CUFE **pasaba**: redondear por código o por grupo daba lo mismo. Hizo falta un segundo grupo de IVA para que la comparación pudiera fallar.
