# Cobertura del estándar UBL 2.1

> Este documento existe como mitigación del riesgo **R-01** (*la
> especificación de la DIAN es extensa y la tentación es implementarla
> entera*). Declara qué se implementó, qué se dejó fuera y por qué, para que
> nadie tenga que deducirlo leyendo el generador.

**Estado:** el XML generado valida contra el esquema oficial de UBL 2.1
(criterio **CE-05**). Eso significa que su estructura es correcta. **No
significa que la DIAN lo aceptaría**: el anexo técnico añade centenares de
validaciones de negocio que ningún esquema expresa.

---

## 1. Qué se implementó

### Firma digital (desde la etapa 6)

`ext:UBLExtensions/ext:ExtensionContent` con una firma **XAdES-EPES**: tres
referencias, política de firma de la DIAN, `SigningTime`, `SigningCertificate`,
`SignaturePolicyIdentifier` y `SignerRole`. Los detalles y sus limitaciones
están en [ADR-0012](adr/0012-firma-xades.md).

El bloque solo aparece cuando hay firma: el esquema no admite un
`ExtensionContent` vacío.

### Cabecera del documento

| Elemento | Valor | Nota |
|---|---|---|
| `cbc:UBLVersionID` | `UBL 2.1` | Fijo. |
| `cbc:CustomizationID` | `10` / `20` / `30` | Código de operación por tipo de documento. |
| `cbc:ProfileID` | `DIAN 2.1` | |
| `cbc:ProfileExecutionID` | `1` o `2` | Producción o pruebas. |
| `cbc:ID` | Prefijo + consecutivo | El número autorizado del rango. |
| `cbc:UUID` | CUFE | Con `schemeName="CUFE-SHA384"`. Ver sección 3. |
| `cbc:IssueDate` / `cbc:IssueTime` | Fecha y hora en `-05:00` | |
| `cbc:InvoiceTypeCode` | `01` | Solo en facturas. |
| `cbc:DocumentCurrencyCode` | Moneda del documento | |
| `cbc:LineCountNumeric` | Número de líneas | |

### Partes

Emisor (`cac:AccountingSupplierParty`) y adquirente
(`cac:AccountingCustomerParty`), cada uno con nombre, ubicación física,
identificación tributaria con dígito de verificación cuando es NIT,
responsabilidades fiscales, dirección de registro y entidad legal.

### Importes

`cac:TaxTotal` con un subtotal por cada combinación de impuesto y tarifa,
tanto a nivel de documento como de línea, y el bloque de totales
(`cac:LegalMonetaryTotal` en facturas, `cac:RequestedMonetaryTotal` en notas).

### Líneas

Número, cantidad con unidad de medida, base gravable, impuestos con su
tarifa, descripción, código del producto y precio unitario.

### Notas crédito y débito

El mismo generador produce los tres tipos. UBL les cambia el espacio de
nombres raíz y algunos nombres de elemento —`LegalMonetaryTotal` pasa a
`RequestedMonetaryTotal`, `InvoicedQuantity` a `CreditedQuantity`— y las
notas añaden `cac:BillingReference` para señalar la factura corregida, **pero
la señalan con el identificador interno de este sistema**, no con datos que la
autoridad conozca. Ver la sección 2.

---

## 2. Qué se dejó fuera, y por qué

| Elemento del estándar | Por qué se omite |
|---|---|
| El contenido real de `cac:BillingReference` en las notas | **Hueco declarado en la auditoría final, no resuelto.** `cac:InvoiceDocumentReference/cbc:ID` lleva el identificador interno (un GUID) de la factura corregida, que solo significa algo dentro de este sistema. La DIAN identifica la factura por su número (prefijo y consecutivo), su `cbc:UUID` con el CUFE y su `cbc:IssueDate`, y nada de eso se escribe. El XML valida contra el esquema, porque el esquema solo exige que haya un `ID`, pero una nota así no le dice a la autoridad qué factura corrige. Arreglarlo exige que el generador reciba los datos de la factura referenciada, que hoy no tiene: la nota guarda su identificador, no su número ni su CUFE. |
| `cac:Signature` | El bloque descriptivo de UBL sobre quién firma. La firma real va en `ext:UBLExtensions`, que sí se emite desde la etapa 6. |
| `cbc:CityName`, `cbc:CountrySubentity` | El sistema guarda el **código** de municipio, no su nombre ni el del departamento. Añadirlos exigiría incorporar el listado oficial de municipios. El plan lo situaba en H8 *(validar códigos contra listas oficiales)* y no se hizo: los códigos se comprueban que vengan, no que existan (`08-despliegue.md`, sección 6). La DIAN los exige; hoy es una omisión conocida, no un descuido. |
| `cac:PaymentMeans`, `cac:PaymentTerms` | La forma de pago aparece en RF-11 pero el modelo de dominio no la capturó. Es una **inconsistencia entre requisitos e implementación**, registrada en la sección 4. |
| `cac:Delivery`, `cac:AllowanceCharge` a nivel de documento | Fuera del alcance declarado en la visión. Los descuentos existen solo a nivel de línea. |
| `cac:WithholdingTaxTotal` | Las retenciones están excluidas explícitamente en `01-vision-alcance.md`. |
| ICA y otros impuestos municipales | El dominio modela IVA e INC. El ICA entra en el cálculo del CUFE como `0.00`, que es lo que exige la fórmula, pero no se declara como impuesto del documento. |
| Los 60 documentos restantes de UBL 2.1 | El estándar cubre órdenes de compra, despacho, catálogos y mucho más. Este proyecto emite tres tipos de documento. |

---

## 3. El código único (CUFE)

Se calcula como SHA-384 sobre la concatenación de quince valores, en el orden
que fija el anexo técnico:

```
NumFac + FecFac + HorFac + ValFac
       + "01" + ValIva + "04" + ValInc + "03" + ValIca
       + ValTot + NitEmisor + NumAdquirente + ClaveTecnica + Ambiente
```

Los tres códigos de impuesto son constantes y van siempre, aunque su valor
sea `0.00`.

### Limitaciones declaradas

**No se ha contrastado contra un ejemplo oficial.** El orden de los campos se
tomó de dos fuentes independientes que coinciden, pero no se dispuso de un
caso de prueba publicado por la DIAN con sus datos de entrada y su resultado
esperado. Las pruebas del proyecto demuestran que el cálculo es **coherente**
—que ningún campo quedó fuera de la concatenación y que alterar cualquiera
cambia el resultado— pero **no que sea el que la DIAN espera**.

**El CUDE usa la fórmula equivocada.** Las notas crédito y débito no llevan
CUFE sino CUDE. El *nombre* del esquema sí se declara correctamente
(`CUDE-SHA384`), pero el valor se calcula hoy con la fórmula de factura. Las
fuentes consultadas coinciden en que la del CUDE es casi idéntica
sustituyendo la clave técnica por el PIN del software, y ninguna lo afirma
con autoridad suficiente. Se prefiere una limitación declarada a un algoritmo
normativo escrito de memoria, pero **el código que hoy llevan las notas no es
el que la DIAN calcularía**.

### Qué haría falta para cerrar esto

1. Leer el numeral 11.1 del anexo técnico vigente y confirmar campo por campo.
2. Reproducir un ejemplo oficial y comprobar que el resultado coincide.
3. Emitir contra el entorno de pruebas de la DIAN y obtener una aceptación.

---

## 4. Inconsistencias conocidas entre requisitos e implementación

| Qué dice | Qué ocurre | Resolución |
|---|---|---|
| **RF-11** menciona la forma de pago como dato de entrada de una factura. | El modelo de dominio nunca la incorporó, así que el XML no la declara. | Registrado. Requiere decidir si se añade al dominio o se retira de RF-11. |
| **RF-20** decía que el código único lo *asigna la autoridad al aprobar*. | En Colombia lo **calcula el emisor** y viaja dentro del XML desde el primer envío. | **Corregido** en esta etapa. El requisito y el contrato ya lo describen bien. |

---

## 5. Los esquemas

Versionados en `schemas/ubl-2.1/`, obtenidos con
`scripts/descargar-esquemas-ubl.ps1`. Ver `schemas/README.md` para la
procedencia y para por qué se versionan en vez de descargarse al construir.

Una particularidad que costó encontrar: `UBL-SignatureAggregateComponents-2.1.xsd`
referencia `ds:Signature` **sin importar** el esquema donde ese elemento está
declarado. Cualquier validador debe cargar `UBL-xmldsig-core-schema-2.1.xsd`
por su cuenta, y ese archivo lleva un `DOCTYPE` que obliga a habilitar el
procesamiento de DTD solo para leerlo.
