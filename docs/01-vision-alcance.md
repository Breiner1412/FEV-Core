# Documento de Visión y Alcance

**Proyecto:** FEV-Core — API de emisión de documentos electrónicos (Colombia)
**Versión:** 1.0
**Fecha:** 24 de septiembre de 2026
**Autor:** Breiner Stiven Guisao Rodríguez
**Estado:** Aprobado para pasar a levantamiento de requerimientos

---

## 1. Propósito de este documento

Definir qué problema resuelve FEV-Core, para quién, y — sobre todo — **qué queda explícitamente fuera del alcance**. Este documento es la referencia contra la cual se acepta o se rechaza cualquier solicitud de cambio durante el proyecto. Si algo no está aquí, no se construye en la versión 1.

---

## 2. Contexto y problema

En Colombia la facturación electrónica es obligatoria para la mayoría de contribuyentes y opera bajo un modelo de **validación previa**: la DIAN debe validar y aprobar el documento antes de que se considere expedido y se entregue al adquirente. La DIAN ejecuta las validaciones en tiempo real y asigna un código único de identificación (CUFE).

Esto plantea un problema técnico concreto para cualquier empresa que ya tenga un sistema propio (un ERP, un e-commerce, un software de punto de venta):

- El formato exigido es **UBL 2.1 con las adendas colombianas**, un estándar que requiere construir el documento con una estructura precisa.
- El documento debe ir **firmado digitalmente**.
- La validación con la DIAN tiene una **latencia típica de 2 a 8 segundos** y puede fallar. Un sistema que asuma respuesta inmediata y sin errores deja documentos en estado indeterminado.
- Cada rechazo de la DIAN tiene consecuencias de negocio: ventas sin soporte fiscal, clientes que no pueden descontar IVA y reprocesos contables.

Estos detalles no son el negocio de quien vende zapatos o software de inventarios. Hoy la alternativa es contratar un proveedor tecnológico completo, que a menudo impone su propio modelo de datos y su propia interfaz.

**El problema que atacamos:** no existe un componente pequeño, integrable y con un contrato claro, que se encargue únicamente de convertir una venta en un documento electrónico válido, sin obligar a la empresa a cambiar el sistema que ya tiene.

---

## 3. Visión del producto

> Para **empresas colombianas que ya operan un sistema propio de ventas**,
> que **necesitan emitir documentos electrónicos válidos ante la DIAN**,
> FEV-Core es una **API REST**
> que **recibe los datos de una venta y devuelve un documento electrónico generado, firmado y validado, con su estado rastreable en todo momento**.
> A diferencia de **contratar una plataforma de facturación completa**,
> nuestro producto **no impone interfaz ni modelo de negocio: se integra como un componente más de la arquitectura existente**.

---

## 4. Usuarios y actores

| Actor | Descripción | Qué espera del sistema |
|---|---|---|
| **Sistema integrador** | El software del cliente (ERP, e-commerce, POS). Es el usuario principal. | Un contrato REST estable, respuestas predecibles y errores descriptivos. |
| **Desarrollador integrador** | La persona que conecta ese software con FEV-Core. | Documentación viva (OpenAPI), ejemplos y un entorno local que levante con un comando. |
| **Administrador de la empresa emisora** | Configura los datos del emisor y sus rangos de numeración. | Que la configuración sea explícita y que el sistema avise antes de que algo se venza o se agote. |
| **DIAN (sistema externo)** | Autoridad que valida y aprueba. | Documentos conformes al estándar. |
| **Adquirente (cliente final)** | Recibe el documento. | Fuera del alcance directo: FEV-Core no le entrega nada; eso lo hace el sistema integrador. |

---

## 5. Alcance incluido (versión 1)

### 5.1 Documentos soportados
- **Factura Electrónica de Venta (FEV)**
- **Nota Crédito** (asociada a una factura existente)
- **Nota Débito** (asociada a una factura existente)

### 5.2 Capacidades funcionales
1. Administración de la **empresa emisora** y sus datos tributarios.
2. Administración de **adquirentes** (clientes) y **productos o servicios**.
3. Administración de **rangos de numeración** autorizados, con control de consumo y de vigencia.
4. **Generación** del documento en formato XML conforme a UBL 2.1 con adendas colombianas.
5. **Cálculo tributario**: base gravable, IVA y total, con las reglas de redondeo correctas.
6. **Firma digital** del XML.
7. **Transmisión** del documento para validación.
8. **Seguimiento de estado** del documento a lo largo de su ciclo de vida, con reintentos automáticos ante fallas transitorias.
9. **Consulta** del documento y de su historial de estados.

### 5.3 Capacidades técnicas
- API REST documentada con OpenAPI.
- Autenticación por token para los sistemas integradores.
- Trazabilidad: todo cambio de estado de un documento queda registrado con marca de tiempo.
- Despliegue local con un solo comando (contenedores).
- Pruebas automatizadas sobre las reglas de negocio críticas.

---

## 6. Fuera de alcance (explícito)

Lo siguiente **no se construye en la versión 1**. Se deja documentado para que la decisión sea visible y para que el crecimiento del proyecto sea una decisión, no un accidente.

| Excluido | Razón |
|---|---|
| Interfaz gráfica de usuario | El producto es un componente de integración. Una UI duplicaría el esfuerzo sin demostrar nada nuevo. |
| Nómina electrónica | Es un subsistema distinto, con su propio estándar y su propio ciclo. |
| Documento soporte en adquisiciones a no obligados a facturar | Candidato claro a versión 2. |
| Documento equivalente electrónico (tiquete POS) | Tiene reglas propias y un código distinto (CUDE). Versión 2. |
| Eventos de recepción, aceptación y reclamación (RADIAN) | Depende de tener el ciclo básico resuelto primero. |
| Conexión con el servicio real de la DIAN | Ver sección 7. Se sustituye por un simulador, detrás de una interfaz que permite el reemplazo. |
| Representación gráfica en PDF con código QR | Deseable y vistoso, pero no es requisito para que el documento sea válido. Candidato a versión 1.1. |
| Multi-empresa (varias empresas emisoras en una misma instalación) | Se modela pensando en no impedirlo, pero no se implementa ni se prueba. |
| Contabilidad, inventarios, cartera | No es el problema que resolvemos. |

---

## 7. Supuestos y restricciones

### 7.1 Restricción principal: el entorno de la DIAN

Conectarse al servicio real de la DIAN exige un proceso de habilitación que incluye registro formal, certificado digital emitido por una entidad autorizada y la aprobación de un set de pruebas. Ese trámite es un requisito administrativo, no técnico, y su duración no depende del equipo de desarrollo.

**Decisión:** la versión 1 opera contra un **simulador de la DIAN** desarrollado como parte del proyecto, que replica el contrato de interacción: recibe el documento, responde con un identificador de seguimiento, y permite consultar el resultado de la validación.

**Condición que esta decisión impone al diseño:** el acceso a la DIAN debe estar detrás de una abstracción. Sustituir el simulador por el servicio real debe ser reemplazar una implementación y su configuración, sin tocar la lógica de negocio. Esta restricción se convierte en un requerimiento no funcional en la etapa 2 y en una decisión de arquitectura registrada en la etapa 4.

### 7.2 Otros supuestos
- El sistema integrador es responsable de entregar el documento al adquirente. FEV-Core no envía correos.
- El certificado de firma digital se provee como archivo, configurado por instalación.
- La moneda de la versión 1 es el peso colombiano (COP).
- La tarifa general de IVA es 19%, pero el sistema no la asume fija: la tarifa es un dato del producto, no una constante del código.
- Los valores que dependen de la UVT (que se actualiza cada año) son configurables, no constantes. Para 2026 la UVT es de $52.374.

---

## 8. Criterios de éxito

El proyecto se considera exitoso en su versión 1 cuando:

| # | Criterio | Cómo se verifica |
|---|---|---|
| CE-01 | Un integrador puede emitir una factura desde cero siguiendo únicamente la documentación, sin ayuda del autor. | Prueba con una persona ajena al proyecto. |
| CE-02 | Una nota crédito no puede emitirse sin referenciar una factura válida y existente. | Prueba automatizada. |
| CE-03 | Ningún número de documento se emite dos veces, ni fuera de un rango autorizado vigente. | Prueba automatizada, incluyendo emisión concurrente. |
| CE-04 | Ningún documento queda en estado indeterminado cuando el servicio de validación falla o demora. | Prueba de fallo simulado. |
| CE-05 | El XML generado es válido contra el esquema UBL 2.1. | Validación automatizada contra el esquema. |
| CE-06 | El proyecto levanta completo en una máquina limpia con un solo comando. | Prueba en entorno limpio. |
| CE-07 | Cada requerimiento funcional tiene al menos una prueba y un commit rastreable. | Revisión de trazabilidad. |

---

## 9. Riesgos identificados

| # | Riesgo | Impacto | Mitigación |
|---|---|---|---|
| R-01 | El estándar UBL 2.1 con adendas colombianas es extenso; modelarlo completo puede consumir todo el tiempo. | Alto | Implementar el subconjunto mínimo que hace válido el documento. Documentar qué campos se omitieron y por qué. |
| R-02 | Al no validar contra la DIAN real, el XML puede ser incorrecto sin que nos enteremos. | Alto | Validar contra el esquema oficial y contra ejemplos públicos. Declarar la limitación abiertamente en el README. |
| R-03 | El autor es nuevo en la plataforma .NET; la curva de aprendizaje puede desviar el cronograma. | Medio | Hito 0 dedicado exclusivamente a aprendizaje, con un entregable mínimo. Cronograma con holgura. |
| R-04 | El alcance crece durante la implementación ("ya que estoy, le agrego..."). | Medio | Este documento. Toda adición se evalúa contra la sección 6. |
| R-05 | La firma digital es la parte técnicamente más difícil y podría bloquear el avance. | Medio | Aislarla en un hito propio. El sistema debe funcionar sin firma en los hitos previos. |

---

## 10. Glosario

| Término | Significado |
|---|---|
| **DIAN** | Dirección de Impuestos y Aduanas Nacionales. Autoridad tributaria de Colombia. |
| **FEV** | Factura Electrónica de Venta. |
| **CUFE** | Código Único de Factura Electrónica. Identificador que asigna la DIAN al validar. |
| **UBL 2.1** | Universal Business Language. Estándar internacional de documentos comerciales en XML, sobre el cual Colombia define sus adendas. |
| **Validación previa** | Modelo en el que la DIAN aprueba el documento antes de que se entregue al adquirente. |
| **Adquirente** | Quien compra. En términos comunes, el cliente. |
| **Emisor** | Quien expide el documento. La empresa que usa FEV-Core. |
| **UVT** | Unidad de Valor Tributario. Valor de referencia que se actualiza anualmente. |
| **Rango de numeración** | Conjunto de números consecutivos autorizados para emitir documentos. |

---

## 11. Marco normativo de referencia

Este proyecto es un ejercicio técnico y **no constituye asesoría tributaria ni legal**. Las siguientes normas se citan como referencia del modelo que se busca replicar:

- Resolución DIAN 000165 de 2023 — regulación base del sistema de facturación electrónica.
- Resolución DIAN 000202 de 2025 — modificaciones sobre requisitos, validaciones y estructura de los documentos.
- Resolución DIAN 000227 de 2025 — consolidación normativa del sistema.
- Artículo 616-1 del Estatuto Tributario — obligación de facturar.

La normativa se actualiza con frecuencia. Cualquier uso real de este software exige verificar la versión vigente del anexo técnico de la DIAN.

---

## 12. Control de cambios

| Versión | Fecha | Cambio |
|---|---|---|
| 1.0 | 2026-09-24 | Versión inicial. |
