# ADR-0012: Firma XAdES-EPES construida sobre SignedXml

**Estado:** Aceptada
**Fecha:** 2026-09-29

## Contexto

RF-17 exige firmar digitalmente el XML con el certificado del emisor, de forma que la firma sea verificable con su clave pública y cualquier alteración posterior la invalide.

La DIAN no acepta una firma XML-DSig corriente. Exige **XAdES-EPES**: la firma va dentro de `ext:UBLExtensions/ext:ExtensionContent`, con canonicalización inclusiva C14N 1.0, SHA-256 y RSA-SHA256, tres referencias —el documento, el `KeyInfo` y las `SignedProperties`— y cuatro propiedades cualificadas: `SigningTime`, `SigningCertificate`, `SignaturePolicyIdentifier` y `SignerRole` con valor `supplier`. La política de firma es única para todo el país y su huella SHA-256 es un valor fijo.

**.NET no trae XAdES.** `SignedXml` implementa XML-DSig: referencias, digestos y firma. Las propiedades cualificadas —que son lo que distingue XAdES— hay que construirlas a mano.

R-05 identificaba esto como la parte técnicamente más difícil, capaz de bloquear el proyecto.

## Decisión

Se construye XAdES-EPES sobre `SignedXml`, añadiendo a mano el bloque de propiedades cualificadas como un `ds:Object` referenciado con `Type="http://uri.etsi.org/01903#SignedProperties"`.

El certificado se carga desde configuración, en base 64 por variable de entorno, y su vigencia se comprueba en cada firma.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| Una biblioteca de terceros para XAdES | Resolvería el problema sin enseñar nada, ataría el proyecto a su interpretación de un anexo que cambia, y añadiría una dependencia con acceso a la clave privada. |
| Firmar solo con XML-DSig y declarar XAdES pendiente | Cumpliría la demostración que pide el plan —firma presente, verificable, se rompe al alterar un byte— pero la DIAN la rechazaría. El proyecto prefiere implementar la norma y declarar lo que no puede verificar. |
| Construir el `ds:Signature` entero a mano | Daría control total y obligaría a reimplementar canonicalización y digestos, que es justo la parte que `SignedXml` hace bien. |

## Los dos problemas que costaron encontrar

Los dos son el mismo problema con distinta cara: **los bytes que se firman tienen que ser los bytes que se verifican**. Es la misma lección que ADR-0011 registró para el código único.

### Las referencias a fragmentos que aún no existen

`SignedXml` resuelve cada referencia `#id` buscando ese elemento **en el documento**. Las propiedades XAdES no están ahí: viven dentro del `ds:Object` de la firma, que todavía no existe como XML cuando se calculan los digestos. Lo mismo ocurre con el `KeyInfo`.

El síntoma es `CryptographicException: Malformed reference element`, que no menciona nada de esto.

`SignedXml.GetIdElement` es **virtual** precisamente para este caso. Una subclase que añade los fragmentos sueltos como sitio adicional donde mirar resuelve las dos referencias.

### La canonicalización inclusiva hereda espacios de nombres

El digesto de las propiedades se calcula dos veces en momentos distintos. Al firmar, el bloque está suelto y solo conoce los prefijos que declara él mismo. Al verificar, ya está dentro de la factura y **hereda** los suyos.

La canonicalización inclusiva —la que exige la DIAN— incluye los espacios de nombres heredados en el texto que se convierte en digesto. Mismo contenido, distinto contexto, distinto texto, distinto hash. La firma no verifica y el único síntoma es un `false`.

La corrección es declarar de antemano en el bloque lo que va a heredar después, de modo que el contexto sea idéntico en los dos momentos. Con un matiz: a las propiedades XAdES se les copia también el espacio de nombres por defecto de la factura, porque ese elemento viaja entero al documento con sus declaraciones puestas; al `KeyInfo` **no**, porque vive en el espacio de firma XML y dentro de `ds:Signature` ese es el que hereda.

### Y una consecuencia de lo anterior

`SignedXml` serializa su propio `KeyInfo` al final, que no es el elemento sobre el que se calculó el digesto. Serían equivalentes en significado y podrían diferir en un espacio o en una declaración de prefijo. Después de firmar se sustituye por el mismo elemento que se digirió: si son literalmente el mismo, no hay nada que pueda diferir.

## Consecuencias

**Positivas**
- La firma es conforme en estructura a lo que exige el anexo técnico.
- No hay dependencias de terceros con acceso a la clave privada.
- El riesgo R-05 queda retirado, y **se mantiene retirado**: la prueba de concepto que el plan situaba en H0 era desechable; estas pruebas se quedan.

**Negativas**
- El firmador depende de detalles internos de `SignedXml` —que `GetIdElement` sea virtual, que `GetXml` produzca una estructura sustituible—. Una versión futura de .NET podría cambiarlos. Las pruebas lo detectarían.
- La corrección de espacios de nombres es frágil de entender y fácil de romper al refactorizar. Está documentada en el propio código.

## Verificación

`FirmaXadesTests` y `FirmaEndpointTests`, diecisiete pruebas en total. Las que importan:

- La firma se verifica con la clave pública del certificado.
- **Alterar un solo dígito del total invalida la firma.** Sin esta, la firma no estaría protegiendo nada.
- La firma de otro certificado no verifica.
- El documento firmado sigue validando contra el esquema oficial de UBL 2.1.
- La firma que sale **por la API** se verifica. Entre firmar y descargar, el documento pasa por PostgreSQL, por otra petición HTTP y por la serialización de la respuesta; cualquiera de esos pasos podría alterar un byte.
- Un certificado vencido no firma (INV-CER-01).

## Lo que NO está verificado

Que la DIAN aceptaría esta firma. La estructura sigue lo que dos fuentes independientes describen y el documento valida contra el esquema, pero **no se ha emitido contra el entorno de pruebas de la autoridad**. Hasta que eso ocurra, la conformidad es una hipótesis razonada, no un hecho.

El certificado que usan las pruebas es autofirmado. Uno real lo emite una entidad de certificación autorizada, y la DIAN valida esa cadena de confianza además de la firma.
