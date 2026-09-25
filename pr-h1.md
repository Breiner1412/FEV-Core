Cierra el hito **H1 - Camino delgado**.

## Qué entrega

Emitir una factura y consultarla, de punta a punta: HTTP → autenticación → dominio → PostgreSQL → y de vuelta.

- Tipo `Dinero` con precisión decimal exacta.
- Entidades `Documento`, `Linea`, `ImpuestoLinea` y `Totales`, con sus invariantes.
- Cálculo de totales con el redondeo aplicado sobre el documento.
- Persistencia con Entity Framework sobre PostgreSQL, con el mapeo fuera de las entidades.
- Autenticación por llave de API, con rechazo por defecto.
- `POST /api/v1/facturas` y `GET /api/v1/documentos/{id}`.
- Errores en formato Problem Details y registros con identificador de correlación.

**83 pruebas**, de las cuales 16 recorren los endpoints contra un PostgreSQL real levantado con Testcontainers.

## Requerimientos cubiertos

| ID | Cómo se cumple |
|---|---|
| RF-01 | Toda petición sin llave válida responde 401 |
| RF-02 | Cada documento registra qué integrador lo emitió |
| RF-14 | La emisión responde 202 sin contactar servicios externos |
| RF-15 | Referencia externa: reintentar devuelve el documento existente, con 200 |
| RF-22 | `GET /api/v1/documentos/{id}` |
| RNF-01 | Sin secretos en el repositorio; falla al arrancar si falta la configuración |
| RNF-10 | Registros con correlación; cabecera `X-Trace-Id` en la respuesta |
| RNF-11 | Problem Details en todos los errores, sin filtrar detalles internos |
| RNF-12 | Tipo `Dinero`; columnas `numeric(18,6)` |
| RN-06 | Redondeo sobre el total, verificado a nivel de dominio y por HTTP |
| RN-08 | Líneas con cantidad y precio positivos; documento con al menos una línea |
| RN-09 | El total es la suma de bases más impuestos menos descuentos |
| RN-10 | Las líneas copian los datos; cambiar un precio no altera facturas emitidas |

## Demostración

```
docker compose up --build -d

POST /api/v1/facturas  con X-Api-Key
→ 202, totalAPagar 357000, estado RECIBIDO, número SETP1

La misma petición otra vez
→ 200, mismo id, mismo consecutivo

Sin llave                     → 401 LLAVE_INVALIDA
Cantidad en cero              → 400
Descuento mayor que la línea  → 409 LINEA_DESCUENTO_EXCESIVO
Documento inexistente         → 404 DOCUMENTO_NO_ENCONTRADO

dotnet test
→ total: 83; con errores: 0
```

## Qué quedó deliberadamente fuera

- Catálogos de emisor, adquirentes y productos → H2
- Rangos de numeración y asignación con bloqueo → H3
- Notas crédito y débito, máquina de estados → H4
- XML, firma y transmisión → H5 a H7

## Lo que no estaba previsto

Nueve hallazgos que la planeación no anticipó.

**1. Un atributo de validación dependiente de la cultura.** `[Range(typeof(decimal), "0.000001", ...)]` convierte sus límites usando la cultura del sistema. En el contenedor Linux, con cultura invariante, funcionaba; en un Windows en español de Colombia, donde el separador decimal es la coma, la conversión fallaba y devolvía 500. Se corrigió con `ParseLimitsInInvariantCulture`. Es el error más instructivo del hito: mismo código, distinto resultado según dónde corra.

**2. Entity Framework obligó a ceder en el dominio.** Las colecciones se exponían como `ReadOnlyCollection`, y EF no puede poblar eso. Hubo que exponer el campo `List` directamente. Se cedió en lo mecánico —ahora una conversión explícita permitiría modificar la colección— pero ninguna invariante quedó desprotegida: sigue sin existir forma de construir un documento inválido. Las dos pruebas que verificaban la garantía anterior se retiraron en lugar de dejarlas mintiendo.

**3. La abstracción del reloj ya existía en el framework.** El documento de arquitectura planeaba una interfaz propia, `IRelojSistema`. Resultó que .NET trae `TimeProvider`, con el mismo propósito y reconocible por cualquier desarrollador de la plataforma.

**4. La política de rechazo por defecto trajo un beneficio no buscado.** Una ruta inexistente ahora responde 401 en vez de 404, lo que impide mapear la API probando direcciones. No se diseñó para eso; salió de exigir autenticación por defecto.

**5. Conflicto de versiones entre dependencias.** Npgsql traía Entity Framework 10.0.4 y el paquete de diseño traía 10.0.12. MSBuild elegía la vieja y emitía advertencias tan extensas que el registrador de la terminal agotó la memoria y abortó la compilación. Se resolvió fijando la versión explícitamente.

**6. Una ambigüedad en el modelo de dominio.** La etapa 3 definía `totalBrutoAntesImpuestos` y `totalBaseImponible` de forma que describían lo mismo, sin distinguir el bruto antes de descuentos del de después. La implementación obligó a precisarlo.

**7. `decimal` conserva la escala.** La misma factura se serializa como `357000,00` recién creada y como `357000,000000` leída de la base, porque la columna es `numeric(18,6)`. Mismo valor, distinta representación. Queda como pendiente de pulido.

**8. El orden de la configuración de EF importa.** `Navigation()` debe ir después de `OwnsMany()`: no declara la navegación, configura una que debe existir.

**9. Una prueba que pasaba por la razón equivocada.** `Los_errores_no_exponen_detalles_internos` estaba en verde mientras el endpoint devolvía 500, porque un error genérico tampoco contiene las palabras buscadas. Se le agregó la verificación del código de estado.

## Deuda registrada

| Pendiente | Cuándo |
|---|---|
| El consecutivo es el máximo más uno; falla bajo concurrencia | H3 |
| `openapi.yaml` describe líneas que referencian el catálogo; H1 las recibe en crudo | H2 |
| Ceros a la derecha inconsistentes según el origen del valor | H8 |
| Gestión centralizada de versiones de paquetes | H8 |

## Verificación de la definición de terminado

- [x] Los requerimientos del hito están implementados
- [x] Cada regla de negocio tiene una prueba que la verifica y otra que verifica su violación
- [x] Las pruebas pasan en integración continua
- [x] `docker compose up` levanta el sistema desde cero
- [x] La demostración se ejecuta y produce el resultado esperado
- [x] La documentación afectada está actualizada
