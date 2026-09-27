Cierra el hito **H2 - Catálogos**.

## Qué entrega

Los catálogos del sistema, y el cambio que da sentido al hito: **la factura deja de recibir datos en crudo y pasa a copiarlos del catálogo al emitir**.

- Objeto de valor `DatosTributarios`, con validación del dígito de verificación del NIT.
- Entidades `Emisor`, `Adquirente` y `Producto`.
- Endpoints `/emisor`, `/adquirentes` y `/productos`, con paginación y desactivación en lugar de borrado.
- La emisión resuelve el adquirente y los productos contra el catálogo, y copia sus datos al documento.
- El documento guarda `EmisorSnapshot` y `AdquirenteSnapshot`.

**118 pruebas**, de 83 que había al cerrar H1.

## Requerimientos cubiertos

| ID | Cómo se cumple |
|---|---|
| RF-03 | `GET` y `PUT /api/v1/emisor` |
| RF-05 | Emitir sin emisor configurado responde `409 EMISOR_INCOMPLETO` |
| RF-06 | `/api/v1/adquirentes`, con INV-ADQ-01 verificado antes de guardar |
| RF-07 | `/api/v1/productos`, con código único entre los activos |
| RF-11 | Completado: la factura referencia productos del catálogo |
| RN-10 | Las líneas y los datos de ambas partes se copian al emitir |
| INV-EMI-01 | Dígito de verificación del NIT, verificado contra NIT reales publicados |
| INV-EMI-03 | El emisor debe declarar al menos una responsabilidad tributaria |
| INV-ADQ-02 | Adquirentes y productos se desactivan, nunca se eliminan |

## Demostración

Dos pruebas de integración, contra PostgreSQL real, hacen exactamente lo que el hito prometía:

```
Cambiar_el_precio_de_un_producto_no_altera_facturas_ya_emitidas
  1. Producto a 150.000 → factura por 357.000
  2. Se sube el producto a 180.000 (y se verifica que el catálogo sí cambió)
  3. Se consulta la factura anterior → sigue en 150.000 y 357.000

Cambiar_los_datos_del_adquirente_no_altera_facturas_ya_emitidas
  El adquirente se muda; la factura conserva la dirección que declaró.
```

Si alguien "optimizara" el sistema haciendo que la línea consulte el producto en vez de copiarlo, esas dos pruebas se ponen rojas de inmediato.

## Qué quedó deliberadamente fuera

- Rangos de numeración y asignación con bloqueo → H3
- Notas crédito y débito, máquina de estados → H4
- XML, firma y transmisión → H5 a H7

## Lo que no estaba previsto

**1. Un `record` con una colección no tiene igualdad por valor.** `DatosTributarios` es un `record`, pero su lista de responsabilidades se comparaba por referencia, así que dos instancias con contenido idéntico salían distintas. La prueba lo detectó, y no era un problema de la prueba: sin igualdad real, comparar la copia de un documento contra los datos vivos del adquirente habría dado "distintos" siempre. Se resolvió escribiendo `Equals` y `GetHashCode` a mano.

**2. El mismo problema reapareció en la capa de persistencia.** EF decide si un valor cambió comparándolo con una instantánea, y para colecciones también compara por referencia. Hubo que darle un `ValueComparer` explícito. Conclusión que vale registrar: **cuando un tipo lleva una colección, la igualdad hay que definirla a mano en todos los sitios que la usen.**

**3. Aprovisionar el certificado por configuración dejó a RF-04 sin contenido en este hito.** Si el archivo y su clave viven en variables de entorno, no hay nada que guardar en la base de datos: `Certificado` deja de ser una entidad persistida. RF-04 se mueve a H6, donde el certificado se usa de verdad. Es un cambio de alcance derivado de una decisión, no un olvido.

**4. `dotnet ef migrations remove` se conecta a la base de datos.** Lo hace para comprobar si la migración ya está aplicada. La fábrica de diseño traía una contraseña de relleno que no coincidía con la del entorno, así que fallaba. Ahora lee el archivo `.env`, subiendo por los directorios padre.

**5. `ValueComparer` no vive junto a `ValueConverter`.** Uno está en `ChangeTracking` y el otro en `Storage.ValueConversion`, aunque casi siempre se usen juntos. Segundo error de este tipo en el proyecto, después de `JsonNamingPolicy`.

**6. Dos migraciones se colapsaron en una.** La primera versión de los catálogos generó su propia migración; al cambiar el modelo para las instantáneas se eliminó y se regeneró, para que el historial cuente un cambio coherente en vez de los pasos intermedios.

**7. Entity Framework avisó de una consulta ineficiente.** Cargar un documento con sus líneas y los impuestos de cada línea genera una sola consulta con dos uniones anidadas, que multiplica filas. Con documentos pequeños no importa; con facturas largas sí. Queda como deuda.

## Deuda registrada

| Pendiente | Cuándo |
|---|---|
| El consecutivo es el máximo más uno; falla bajo concurrencia | H3 |
| `QuerySplittingBehavior.SplitQuery` al cargar documentos | H8 |
| Ceros a la derecha inconsistentes según el origen del valor | H8 |
| Gestión centralizada de versiones de paquetes | H8 |
| Validar municipios y unidades de medida contra las listas oficiales | H5 |

## Verificación de la definición de terminado

- [x] Los requerimientos del hito están implementados
- [x] Cada regla de negocio tiene una prueba que la verifica y otra que verifica su violación
- [x] Las pruebas pasan en integración continua
- [x] `docker compose up` levanta el sistema desde cero
- [x] La demostración se ejecuta y produce el resultado esperado
- [x] La documentación afectada está actualizada
