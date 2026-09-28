# Esquemas

## `ubl-2.1/`

Esquemas XSD de **UBL 2.1**, el estándar OASIS sobre el que la DIAN define la
factura electrónica colombiana.

- **Origen:** <https://docs.oasis-open.org/ubl/os-UBL-2.1/UBL-2.1.zip>, carpeta `xsd/`.
- **Versión:** UBL 2.1, especificación OASIS Standard (4 de noviembre de 2013).
- **Cómo se obtuvieron:** `scripts/descargar-esquemas-ubl.ps1`.

### Por qué están en el repositorio y no se descargan al construir

La validación contra el esquema es un criterio de éxito del proyecto (CE-05).
Una prueba que depende de descargar archivos de un servidor ajeno falla el día
que ese servidor cambia de URL, se cae o deja de publicar la versión. Versionar
los esquemas hace la validación reproducible: el mismo commit valida igual hoy
y dentro de cinco años.

El coste es tener unos megas de archivos ajenos en el repositorio. Es el
intercambio habitual al fijar una dependencia que debe permanecer estable.

### Qué NO garantiza esta validación

Que un XML valide contra el esquema significa que su **estructura** es correcta:
los elementos existen, están anidados como corresponde y sus tipos de dato
cuadran.

No significa que la DIAN lo vaya a aceptar. El anexo técnico añade centenares de
validaciones de negocio que el esquema no expresa: códigos de municipio válidos,
coherencia entre totales, formatos de identificación, reglas de vigencia. Esas
las verifica el servicio de validación previa, y este proyecto solo las cubre en
la medida en que aparecen en sus propios requerimientos.
