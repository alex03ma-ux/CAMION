# P6 — Contrato de la API: créditos, búsquedas y "Mis solicitudes"

Fijado antes de programar, a partir de `docs/criterios/criterios-P6-creditos.md` (CA-P6-01 a CA-P6-80) y de las decisiones D-3, D-7, D-9, D-44, D-45, D-48, D-52 y D-54 a D-59 (todas TOMADAS). Extiende `docs/criterios/P5-contrato-api.md` y `docs/criterios/P5b-contrato-api.md` sin cambiar lo que allí no se menciona: formato de error `{ error, errores? }`, auditoría en `auditoria_eventos`, límites `Contactos:*` y política `PoliticaDatos:Version`.

El frontend se programa contra este documento. **Cualquier cambio posterior se anota en la sección "Cambios al contrato" al final.**

**Trazabilidad**

| Código | Tema | Regla |
|---|---|---|
| HU-039, HU-040 | Solicitar y registrar crédito | RN-CO-006, D-54 A, D-55 B |
| HU-041 | Asignar solicitud (siempre Administración) | RN-CO-007, D-7, D-58 A |
| HU-042 | Gestionar solicitud (notas, cierre, reabrir) | RN-CO-008, D-45, D-57 |
| HU-034 | Registrar búsqueda | RN-BU-001, D-56 a A |
| HU-035, HU-037, HU-038 | Consultar, gestionar y cerrar búsquedas | RN-BU-002, D-7, D-57 |
| HU-036 | Relacionar vehículos a una búsqueda | RN-BU-002, D-56 b A, D-56 c A |
| HU-071, HU-072 | "Contactos (N nuevos)" | D-48 |
| — | "Mis solicitudes" del comprador | D-53, D-59 |
| HU-061, HU-062 | Auditoría y autorización de datos | RN-SE-003, RN-SE-004, D-44 |

## Reglas de P6

- **Un crédito o una búsqueda es un lead** con `origen` = `CREDITO` o `BUSQUEDA` (como los avisos de P5b). Se reutilizan `leads`, `eventos_lead`, `consentimientos_datos`, la bandeja del administrador, notas, cierre, reapertura, auditoría y el contador de nuevos.
- **Responsable**: siempre Administración (`responsable_asesor_id = NULL`), también el crédito pedido desde la ficha de un vehículo de un asesor (D-58 A). La base lo impide con `CK_Lead_AvisoAdmin` (sin cambios).
- **Nunca se reasignan** (409) y **el asesor no los ve** (D-58 A).
- **Quién envía**: cualquiera, sin cuenta (D-3, D-9). Con un token válido de cualquier rol el lead queda ligado a su usuario (`usuario_id`); un token inválido o vencido se trata como anónimo. Solo el crédito desde la ficha da 409 si el vehículo es del propio asesor.
- **Crédito sin datos financieros (D-55 B)**: solo nombre, teléfono, correo opcional y comentario opcional. `monto`, `cuotaInicial`, `ingresos`, `tipoVehiculo`, `documento` o cualquier otro campo no listado **se ignoran**: no se guardan en ninguna tabla, historial, auditoría ni registro técnico, y la respuesta es la misma que sin ellos (CA-P6-09, CA-P6-12).
- **Campos que siempre se ignoran** (CA-P6-15): `origen`, `estado`, `responsable`, `asesorId`, `publicacionId`, `usuarioId`, `versionPolitica`. El origen lo fija la ruta; la publicación sale solo del `slug`; el usuario, solo del token.
- **Estados**: en la base siguen `ABIERTO` y `CERRADO`. Se **muestran** derivados (D-57 a A): ver `estadoGestion` y "Mis solicitudes".
- **Sin avisos externos** (D-5, D-48): ninguna ruta envía correo, SMS ni WhatsApp.

## Rutas nuevas

| Método | Ruta | Rol | Limitación |
|---|---|---|---|
| POST | `/api/creditos` | anónimo (token opcional) | `LimitePeticionesContacto` |
| POST | `/api/busquedas` | anónimo (token opcional) | `LimitePeticionesContacto` |
| GET | `/api/catalogo/listas-busqueda` | anónimo (solo lectura) | — (caché `public, max-age=300`) |
| GET | `/api/leads/{id}/coincidencias` | AsesorOAdministrador; solo el administrador la usa (ver permisos) | — |
| POST | `/api/leads/{id}/vehiculos` | AsesorOAdministrador; solo el administrador | — |
| POST | `/api/leads/{id}/vehiculos/{publicacionId}/retirar` | AsesorOAdministrador; solo el administrador | — |
| GET | `/api/mis-solicitudes` | SoloComprador (nueva política) | — |
| GET | `/api/mis-solicitudes/{id}` | SoloComprador | — |

Las tres rutas nuevas bajo `/api/leads` heredan la política del grupo (`AsesorOAdministrador`), no `SoloAdministrador`, para que un asesor reciba **404** "Contacto no encontrado." al pedir una búsqueda o un crédito (CA-P6-61), igual que en el detalle. Ver "Permisos".

---

## POST /api/creditos (HU-039, HU-040)

Cuerpo (JSON, máx. 8 KB) — `CreditoRequest`:

```json
{ "slug": null, "nombre": "Ana Pérez", "telefono": "300 123 4567", "correo": null,
  "mensaje": "Me interesa un camión sencillo", "autorizacionDatos": true, "sitioWeb": "" }
```

| Campo JSON | Tipo | Regla | Mensaje 400 (en `errores.<campo>`) |
|---|---|---|---|
| `slug` | string \| null | opcional. Vacío, `null` o ausente = crédito desde la portada. Con valor = crédito desde la ficha; hasta 200 caracteres | más de 200 → 404 (como un slug inexistente) |
| `nombre` | string | obligatorio, 2 a 150 caracteres tras quitar espacios | "Escribe tu nombre (entre 2 y 150 caracteres)." |
| `telefono` | string | obligatorio; celular (3…) o fijo (60…) colombiano de 10 dígitos, con o sin 57/+57 y separadores; se guarda `+57XXXXXXXXXX` | "Escribe un celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567." |
| `correo` | string \| null | opcional; formato válido y hasta 254; se guarda en minúsculas | "Escribe un correo válido o deja el campo vacío." |
| `mensaje` | string \| null | opcional (el comentario); hasta 500 tras quitar espacios | "El comentario no puede pasar de 500 caracteres." |
| `autorizacionDatos` | boolean | debe ser `true` | "Debes autorizar el tratamiento de tus datos para enviar el formulario." |
| `sitioWeb` | string | campo trampa: vacío o ausente | (relleno → 201 sin guardar nada) |

Se guarda: `origen = CREDITO`, `publicacion_id` = la publicación del `slug` o `NULL`, `tipo_vehiculo = NULL`, `mensaje` = el comentario recortado o, si está vacío, **"Solicitud de crédito"** (portada) o **"Solicitud de crédito para este vehículo"** (ficha).

Publicación (solo con `slug`):
- Debe ser **visible en el portal** en ese momento (la misma regla de `/api/catalogo`, incluido el plan vigente del asesor). Si no existe o no es visible → **404** `{ "error": "Este vehículo ya no está disponible. Mira otros vehículos en el catálogo." }` (CA-P6-04).
- Con un token válido cuyo usuario es el asesor dueño → **409** `{ "error": "Este vehículo es tuyo; no puedes contactarte a ti mismo." }` (CA-P6-05).

### Respuestas (crédito y búsqueda)

| Código | Cuándo | Cuerpo |
|---|---|---|
| 201 | solicitud nueva, repetida o campo trampa | `{ "mensaje": "¡Listo! Recibimos tu solicitud. Te contactaremos pronto por teléfono o WhatsApp." }` (sin id ni responsable) |
| 400 | validación | `{ "error": "Revisa los datos marcados.", "errores": { "<campo>": ["<mensaje>"] } }` |
| 400 | JSON malformado, cuerpo vacío o tipos incorrectos (p. ej. `"tipoId": "abc"`) | `{ "error": "La solicitud no tiene un formato válido." }` |
| 404 | (solo crédito con `slug`) publicación inexistente o no visible | ver arriba |
| 409 | (solo crédito con `slug`) vehículo propio | ver arriba |
| 413 | cuerpo de más de 8 KB | `{ "error": "La solicitud es demasiado grande." }` |
| 429 | límite por IP o por teléfono | `{ "error": "Has enviado muchas solicitudes. Intenta de nuevo en unos minutos." }` |
| 500 | error inesperado | `{ "error": "No se pudo completar la operación. Intenta de nuevo en unos minutos." }` |

Orden: límite por IP (middleware) → tamaño (413) → formato (400) → campo trampa (201) → campos (400) → publicación (404, solo crédito con `slug`) → propio (409) → límite por teléfono (429) → repetido (201) → nuevo (201).

---

## POST /api/busquedas (HU-034)

Cuerpo (JSON, máx. 8 KB) — `BusquedaRequest`. Los nombres de los criterios son los mismos que usa el filtro del catálogo (`tipoId`, `marcaId`, `lineaId`) para reutilizar sus listas:

```json
{ "nombre": "Ana Pérez", "telefono": "300 123 4567", "correo": null,
  "tipoId": 2, "marcaId": 5, "lineaId": 17, "anioDesde": 2015, "anioHasta": 2020,
  "presupuestoMaximo": 150000000, "departamentoId": 1, "municipioId": 1,
  "descripcion": "Doble troque con platón", "autorizacionDatos": true, "sitioWeb": "" }
```

| Campo JSON | Tipo | Regla | Mensaje 400 |
|---|---|---|---|
| `nombre`, `telefono`, `correo`, `autorizacionDatos`, `sitioWeb` | | como en el crédito | los mismos |
| `tipoId` | number | **obligatorio**; una categoría activa de `tipos_vehiculo` (D-17) | "Elige la categoría del vehículo que buscas." |
| `marcaId` | number \| null | opcional; una marca activa | "Elige una marca de la lista." |
| `lineaId` | number \| null | opcional; un modelo activo **de esa marca** (exige `marcaId`) y, si el modelo está clasificado, de esa categoría (D-32) | "Elige un modelo de la lista." |
| `anioDesde`, `anioHasta` | number \| null | opcionales; entre 1950 y el año actual + 1; `anioDesde <= anioHasta` (el error del orden va en `anioHasta`) | "Revisa el rango de años." |
| `presupuestoMaximo` | number \| null | opcional; pesos enteros entre 1.000.000 y 5.000.000.000 | "Escribe un valor en pesos entre 1.000.000 y 5.000.000.000, o deja el campo vacío." |
| `departamentoId` | number \| null | opcional; un departamento DIVIPOLA existente (D-29) | "Elige un departamento de la lista." |
| `municipioId` | number \| null | opcional; un municipio existente; si llega `departamentoId`, debe ser de ese departamento. Si llega solo `municipioId`, el departamento se toma del municipio | "Elige una ciudad de la lista." |
| `descripcion` | string \| null | opcional; hasta 500 tras quitar espacios | "La descripción no puede pasar de 500 caracteres." |

Se guarda: `origen = BUSQUEDA`, `publicacion_id = NULL`, `tipo_vehiculo = NULL`, `mensaje` = la descripción recortada o **"Busco un vehículo"**; los criterios en `leads_busqueda` (una fila por lead). Respuestas: como el crédito, sin 404 ni 409.

### GET /api/catalogo/listas-busqueda (listas del formulario)

Anónima, de solo lectura, sin datos personales; responde igual con o sin token. Cabecera `Cache-Control: public, max-age=300`. Los ids son los que acepta `POST /api/busquedas`.

```json
{ "tipos": [ { "id": 2, "nombre": "Camión" } ],
  "marcas": [ { "id": 5, "nombre": "Kenworth",
                "lineas": [ { "id": 17, "nombre": "T800", "tipoIds": [2] } ] } ],
  "departamentos": [ { "id": 1, "nombre": "Antioquia",
                       "municipios": [ { "id": 1, "nombre": "Medellín" } ] } ] }
```

- `tipos`: categorías activas (D-17). `marcas`: marcas activas con sus modelos activos. `tipoIds`: categorías **activas** del modelo (D-32); vacío = sin clasificar, vale para cualquier categoría. El formulario ofrece en "Modelo" los de la marca elegida cuyo `tipoIds` está vacío o contiene la categoría elegida.
- `departamentos`: los 33 de DIVIPOLA con **todos** sus municipios (1.122 en `camion_ventas`, unos 40 KB sin comprimir). Se envían juntos en lugar de una ruta por departamento: es el mismo formato de `/api/inventario/catalogos`, evita una petición por cambio de departamento y la caché lo amortiza.
- Orden alfabético (es-CO) en todas las listas.
- El servidor vuelve a validar en `POST /api/busquedas`: municipio del departamento, modelo de la marca y de la categoría (si está clasificado).

Una búsqueda **no se publica** ni se muestra a ningún asesor (CA-P6-24), y el sistema **no busca coincidencias solo** (CA-P6-25).

---

## Anti-abuso (CA-P6-29 a CA-P6-33)

- **Por IP**: las dos rutas llevan `LimitePeticionesContacto`; el cupo (5 cada 10 min, 20 al día) se **comparte** con `POST /api/contactos` y los dos avisos.
- **Por teléfono**: `Contactos:LimiteTelefonoDia` (10) cuenta, en 24 h y para el teléfono normalizado, todos los leads creados (cualquier origen) más los `CONTACTO_REPETIDO` de leads de ese teléfono. Se comprueba antes de la rama de repetido.
- **Tamaño** 8 KB y **campo trampa** `sitioWeb`: como P5 y P5b.
- **Repetido**:
  - Crédito: hay un lead ABIERTO con `origen = CREDITO`, el mismo teléfono y **la misma publicación** (o ambos sin publicación). No se crea otro: se añade `CONTACTO_REPETIDO` con el texto que se habría guardado en `mensaje` y se guarda su consentimiento.
  - Búsqueda: hay un lead ABIERTO con `origen = BUSQUEDA` y el mismo teléfono. Se añade `CONTACTO_REPETIDO` con un resumen legible de los criterios nuevos (por ejemplo "Busca: Camión · Kenworth T800 · años 2015 a 2020 · hasta $150.000.000 · Medellín (Antioquia). Doble troque con platón", recortado a 500); los criterios guardados no cambian.
  - En ambos casos la respuesta es idéntica a un envío nuevo y el lead no vuelve a marcarse como nuevo (D-49).
  - **Mismo usuario (D-60)**: solo se agrega al lead abierto si tiene el mismo `usuario_id` que el envío; dos anónimos (NULL y NULL) cuentan como iguales. Nunca se asigna un `usuario_id` a una solicitud anónima existente (D-59 a).
- **Nuevo**: si el anterior está CERRADO, es de otro origen, (crédito) de otra publicación o de otro usuario (anónimo frente a una cuenta, o dos cuentas distintas), se crea un lead nuevo. Los límites por teléfono no cambian: cuentan todos los envíos del teléfono, de cualquier usuario.

## Autorización de datos (CA-P6-27, CA-P6-28)

Cada envío aceptado (nuevo o repetido) guarda en la misma transacción una fila de `consentimientos_datos`: `formulario = CREDITO` o `BUSQUEDA`, `version_politica` del servidor, `aceptado_en` UTC, `usuario_id` si hay sesión, `entidad = 'Lead'`, `entidad_id` = id del lead. Es la misma casilla de contacto de P5 y P5b; no hay casilla para bancos ni centrales de riesgo (D-45). Si cualquier escritura falla, no queda nada (lead, criterios, eventos, consentimiento ni auditoría) y se responde 500.

## Historial y auditoría al crear (CA-P6-37)

- `eventos_lead`: `CREADO` y `ASIGNADO` (automático, actor SISTEMA, `responsable_nuevo_asesor_id = NULL`).
- `auditoria_eventos`: `Lead / CREAR`, `usuario_id = NULL`, datos nuevos `{ "origen": "CREDITO"|"BUSQUEDA", "responsableAsesorId": null, "conCuenta": true|false, "conVehiculo": true|false }`. Nunca nombre, teléfono, correo, comentario ni criterios de la búsqueda.

---

## Cambios en `/api/leads` (gestión)

Mismas rutas, roles y códigos de P5 y P5b, con estos cambios.

### Alcance y permisos (CA-P6-60 a CA-P6-63)

- **Asesor**: nunca ve créditos ni búsquedas, tampoco los créditos pedidos desde sus propios vehículos; no cuentan en su `resumen`. Por id (detalle, notas, cerrar, coincidencias, relacionar, retirar) → 404 "Contacto no encontrado."; reabrir y reasignar → 403 (como en P5). Si un asesor llama a coincidencias, relacionar o retirar sobre un contacto propio → 403.
- **Comprador** → 403; **sin token** → 401.
- **Administrador**: ve todo. Créditos y búsquedas ABIERTOS no vistos cuentan en `resumen.nuevos`; abrir el detalle los marca como vistos (CA-P6-57).

### Filtro `origen` en `GET /api/leads`

Admite además `CREDITO` y `BUSQUEDA` (mayúsculas o minúsculas). Otro valor → 400 `errores.origen` "Origen no válido.". `publicacionId` y `vehiculoId` incluyen los créditos de ese vehículo y excluyen las búsquedas (no tienen publicación). `responsable=ADMINISTRACION` los incluye.

### `LeadItem` (lista) — campo nuevo

```text
... (los de P5b) ...,
origen ("CONTACTO_VEHICULO" | "VENDE_TU_CAMION" | "SEGURO_TODO_RIESGO" | "CREDITO" | "BUSQUEDA"),   <- AMPLIADO
estadoGestion ("REGISTRADO" | "EN_GESTION" | "CERRADO" | null)                                      <- NUEVO
```

- `estadoGestion` (D-57 a A) solo en `CREDITO` y `BUSQUEDA`; `null` en los demás orígenes. `REGISTRADO` = ABIERTO sin notas; `EN_GESTION` = ABIERTO con al menos un evento `NOTA` (también si se reabrió después); `CERRADO` = CERRADO. Etiquetas: "Registrado", "En gestión", "Cerrado".
- `vehiculo`: objeto en el crédito desde la ficha; `null` en el crédito desde la portada ("Sin vehículo asociado") y en la búsqueda.
- `tipoVehiculo` siempre `null` en créditos y búsquedas.
- `mensaje`: el comentario del crédito o la descripción de la búsqueda (o sus textos fijos).

### `LeadDetalle` — campos nuevos

```text
... (los de LeadItem y P5b) ...,
estadoGestion,                                                   <- NUEVO (como en LeadItem)
vehiculo { vehiculoId, publicacionId, titulo, slug, estadoVehiculo, estadoPublicacion,
           precioCop (number|null), visible (boolean|null) } | null,   <- precioCop y visible NUEVOS
busqueda { tipoId, tipo, marcaId?, marca?, lineaId?, linea?, anioDesde?, anioHasta?,
           presupuestoMaximo?, departamentoId?, departamento?, municipioId?, municipio? } | null,   <- NUEVO
vehiculosRelacionados [ { publicacionId, vehiculoId, titulo, slug, precioCop?, estadoVehiculo,
           estadoPublicacion, visible, nota?, relacionadoEn, relacionadoPor? } ] | null   <- NUEVO
```

- `vehiculo.precioCop` (precio que muestra el portal: el del vehículo o, si no tiene, el de la publicación) y `vehiculo.visible` (si hoy se ve en el portal; el frontend enlaza a `#/vehiculo/<slug>` solo si es `true`) se calculan **solo en el detalle**; en la lista van `null`. Se añaden para cualquier origen con vehículo (CA-P6-41).
- `busqueda`: solo en `BUSQUEDA`; `null` en los demás. Los nombres se leen aunque la marca, el modelo o la categoría se hayan desactivado después (CA-P6-42).
- `vehiculosRelacionados`: solo en `BUSQUEDA` (`[]` si no hay); `null` en los demás. Solo las relaciones **vigentes** (no retiradas), de la más reciente a la más antigua, con el estado actual del vehículo y `visible` (CA-P6-56). Las retiradas quedan en el historial (`VEHICULO_RETIRADO`).
- No hay monto, cuota, ingresos ni tipo de vehículo del crédito (D-55 B).
- Eventos nuevos en `eventos[].tipo`: `VEHICULO_RELACIONADO` y `VEHICULO_RETIRADO` (actor USUARIO, `texto` = título del vehículo).

### Notas, reabrir y reasignar

- Notas y reabrir: sin cambios (CA-P6-43, CA-P6-47). Nota de 5 a 500; sobre un CERRADO → 409 "Este contacto está cerrado.".
- Reasignar un crédito o una búsqueda (cualquier destino) → **409** `{ "error": "Esta solicitud la atiende siempre la administración." }` (CA-P6-39, D-58 A).

### Cerrar (CA-P6-45, CA-P6-46)

Lista ampliada de `resultado` con `REMITIDO_ENTIDAD` ("Remitido a entidad financiera"), `VEHICULO_ENCONTRADO` ("Encontró vehículo") y `SIN_VEHICULO` ("Sin vehículo disponible"). Resultados válidos **por origen**:

| Resultado | `CONTACTO_VEHICULO` | Avisos | `CREDITO` | `BUSQUEDA` |
|---|---|---|---|---|
| `VENTA_CONCRETADA` | sí | no | no | no |
| `VEHICULO_NO_DISPONIBLE` | sí | no | no | no |
| `ATENDIDA` | no | sí | sí | no |
| `REMITIDO_ENTIDAD` | no | no | sí | no |
| `VEHICULO_ENCONTRADO`, `SIN_VEHICULO` | no | no | no | sí |
| `NO_INTERESADO`, `NO_RESPONDIO`, `DUPLICADO_INVALIDO`, `OTRO` | sí | sí | sí | sí |

- Valor fuera de la lista → 400 `errores.resultado` "Elige el resultado del cierre.". Valor de la lista que no aplica al origen → 400 "Ese resultado no aplica a esta solicitud.". `OTRO` exige observación de 5 a 500. Ya cerrado → 409 "Este contacto ya está cerrado.".

### GET /api/leads/{id}/coincidencias (CA-P6-51)

Ayuda bajo demanda para el administrador; no crea nada ni deja auditoría.

- Solo `BUSQUEDA` (otro origen → 409 "Solo se pueden relacionar vehículos a una búsqueda."). Funciona también con la búsqueda cerrada (solo lectura).
- Una búsqueda sin fila en `leads_busqueda` (dato incoherente; la API siempre la crea) → 409 `{ "error": "Esta búsqueda no tiene criterios guardados." }`, nunca 500.
- Publicaciones **visibles** (misma regla del catálogo) que cumplen los criterios guardados: categoría; marca y modelo si se indicaron; `anioDesde <= año <= anioHasta` si se indicaron; precio mostrado `<= presupuestoMaximo` si se indicó; ciudad si se indicó y, si solo hay departamento, ese departamento.
- Orden: destacados primero y luego las más recientes. Hasta 20.

Respuesta 200 — `CoincidenciasResponse`:

```json
{ "items": [ { "publicacionId": 12, "relacionado": false,
               "vehiculo": { "slug": "...", "titulo": "...", "precioCop": 150000000, "...": "TarjetaVehiculo del catálogo" } } ],
  "total": 1 }
```

`total` es el número de coincidencias (puede ser mayor que los 20 devueltos). `relacionado` = ya está relacionada y vigente.

### POST /api/leads/{id}/vehiculos (CA-P6-50, CA-P6-52, CA-P6-54)

Cuerpo — `RelacionarVehiculoRequest`: `{ "publicacionId": 12, "nota": "Coincide en año y precio" }`.

| Código | Cuándo | Cuerpo |
|---|---|---|
| 201 | relación creada (o reactivada si estaba retirada) | `LeadDetalle` |
| 400 | `publicacionId` ausente o no positivo | `errores.publicacionId` "Elige un vehículo." |
| 400 | `nota` de más de 300 caracteres tras quitar espacios | `errores.nota` "La nota no puede pasar de 300 caracteres." |
| 403 | asesor sobre un contacto propio; comprador | `{ "error": ... }` común |
| 404 | lead inexistente o fuera del alcance (asesor) | "Contacto no encontrado." |
| 409 | el lead no es una búsqueda | "Solo se pueden relacionar vehículos a una búsqueda." |
| 409 | búsqueda CERRADA | "Esta búsqueda está cerrada." |
| 409 | publicación inexistente o no visible | "Ese vehículo no está publicado." |
| 409 | ya relacionada y vigente | "Ese vehículo ya está relacionado con esta búsqueda." |
| 409 | ya hay 10 relacionadas vigentes | "Esta búsqueda ya tiene 10 vehículos relacionados." |

Orden: alcance (404) → rol (403) → origen (409) → cuerpo (400) → cerrada (409) → no visible (409) → ya relacionada (409) → tope (409).
Guarda: fila en `busqueda_vehiculos` (quién y cuándo; relacionar de nuevo una retirada reactiva la misma fila), evento `VEHICULO_RELACIONADO` (texto = título), `auditoria_eventos` `Lead / RELACIONAR_VEHICULO` con `{ "publicacionId": n }`. No crea leads ni avisos para el asesor dueño (CA-P6-55). La nota es interna: el comprador no la ve.

### POST /api/leads/{id}/vehiculos/{publicacionId}/retirar (CA-P6-53)

Sin cuerpo. 200 con `LeadDetalle`. Mismos 403, 404 y 409 de origen y de búsqueda cerrada que relacionar; si no hay relación vigente con esa publicación → 404 "Ese vehículo no está relacionado con esta búsqueda.". Marca la fila como retirada (quién y cuándo, sin borrarla), evento `VEHICULO_RETIRADO` y auditoría `Lead / RETIRAR_VEHICULO` con `{ "publicacionId": n }`.

### Barrido D-46 y reasignación de vehículo (CA-P6-38)

No mueven créditos ni búsquedas: solo miran leads con responsable asesor.

---

## GET /api/mis-solicitudes (D-53, D-59)

Rol: **COMPRADOR** (política nueva `SoloComprador`). Asesor o administrador → 403 `{ "error": ... }` común; sin token → 401 (CA-P6-63, CA-P6-74). Solo lectura: no hay rutas para editar, cancelar ni borrar (CA-P6-69).

Parámetros: `pagina` (≥ 1, por defecto 1) y `tamano` (1 a 50, por defecto 20). Otro valor → 400 `{ "error": "Revisa los filtros.", "errores": { "pagina"|"tamano": ["Página o tamaño no válidos."] } }`.

Contenido: **solo** los leads con `usuario_id` = el usuario del token (enviados con la sesión iniciada; D-59 a A), de los cinco orígenes, del más reciente al más antiguo (CA-P6-66, CA-P6-67).

Respuesta 200 — `MisSolicitudesPaginaResponse`: `{ "items": [MiSolicitudItem], "total", "pagina", "tamano" }`.

`MiSolicitudItem`:

| Campo JSON | Tipo | Contenido |
|---|---|---|
| `id` | number | id del lead (solo para pedir el detalle) |
| `creadoEn` | string (UTC) | fecha del envío |
| `tipo` | string | el `origen`. Etiquetas: `CONTACTO_VEHICULO` "Contacto por vehículo", `VENDE_TU_CAMION` "Vende tu camión", `SEGURO_TODO_RIESGO` "Seguro todo riesgo", `CREDITO` "Crédito", `BUSQUEDA` "Búsqueda" |
| `resumen` | string | contacto y crédito con vehículo: título de la publicación; crédito sin vehículo: "Sin vehículo asociado"; seguro: etiqueta del tipo de vehículo ("Camión", "Carro", "Maquinaria", "Otro"); vende tu camión: "Venta de tu vehículo"; búsqueda: la categoría, más marca y modelo si los indicó ("Camión Kenworth T800") |
| `estado` | string | contactos y avisos: `PENDIENTE` ("Pendiente") o `FINALIZADA` ("Finalizada"); créditos y búsquedas: `REGISTRADA` ("Registrada"), `EN_GESTION` ("En gestión") o `FINALIZADA` ("Finalizada") (CA-P6-70) |
| `vehiculo` | objeto \| null | contacto y crédito con vehículo: `{ "titulo", "slug", "disponible" }`. `slug` solo si `disponible` es `true` (la publicación se ve hoy en el portal); si no, `slug = null` y el frontend dice "Este vehículo ya no está disponible", sin enlace (CA-P6-71). `null` en los demás |
| `recomendados` | `TarjetaVehiculo[]` | solo búsquedas: los vehículos relacionados **vigentes y visibles hoy**, con la tarjeta del catálogo (foto, título, precio, slug…); sin la nota interna. `[]` en los demás (CA-P6-72) |

Nunca incluye: notas, historial, responsable (asesor o Administración), consentimientos, resultado ni observación de cierre, nombre, teléfono, correo ni el texto del mensaje (CA-P6-68).

### GET /api/mis-solicitudes/{id}

El mismo `MiSolicitudItem`. Si el lead no existe o no es del usuario → **404** `{ "error": "Solicitud no encontrada." }` (no se revela que existe, CA-P6-73).

---

## Esquema: migración `database/migraciones/015_p6_creditos_busquedas.sql` (+ `_SUBIR.sql`)

La aplica la responsable con respaldo previo (D-14, D-16). La parte SUBIR no borra datos; sus únicos DROP son `DROP CONSTRAINT` de CHECK seguidos de su re-creación (como la 014).

| Cambio | Detalle |
|---|---|
| `CK_Lead_Origen` | Se re-crea con las tres ramas de la 014 más `(origen = 'CREDITO' AND tipo_vehiculo IS NULL)` y `(origen = 'BUSQUEDA' AND publicacion_id IS NULL AND tipo_vehiculo IS NULL)` |
| `CK_Lead_AvisoAdmin` | Sin cambios |
| `CK_Lead_Resultado` | Se re-crea con `REMITIDO_ENTIDAD`, `VEHICULO_ENCONTRADO` y `SIN_VEHICULO` |
| `CK_Consent_Formulario` | Se re-crea con `CREDITO` y `BUSQUEDA` |
| `CK_EvLead_Tipo` | Se re-crea con `VEHICULO_RELACIONADO` y `VEHICULO_RETIRADO` |
| `IX_Lead_Usuario` (nuevo) | `leads (usuario_id, creado_en)` para "Mis solicitudes" |
| `leads_busqueda` (nueva) | `lead_id` PK y FK RESTRICT a `leads`; `tipo_vehiculo_id` NOT NULL (FK `tipos_vehiculo`); `marca_id`, `linea_id`, `departamento_id`, `municipio_id` NULL (FK RESTRICT); `anio_desde`, `anio_hasta` SMALLINT NULL; `presupuesto_max_cop` BIGINT NULL; CHECK de años (1950 a 2100, desde ≤ hasta) y de presupuesto (1.000.000 a 5.000.000.000) |
| `busqueda_vehiculos` (nueva) | `id` PK; `lead_id` FK RESTRICT; `publicacion_id` FK RESTRICT; `nota` VARCHAR(300) NULL; `relacionado_por` y `retirado_por` FK a `AspNetUsers` SET NULL; `relacionado_en`; `retirado_en` NULL; UNIQUE (`lead_id`, `publicacion_id`) |
| Crédito | Sin tabla ni columnas propias (D-55 B) |
| No se usan | `solicitudes`, `detalle_solicitud_credito`, `detalle_busqueda_personalizada`, `alertas_busqueda` del script 002 (existen en la base; no se tocan) |
| BAJAR (comentada) | Solo si no hay leads `CREDITO`/`BUSQUEDA`, resultados nuevos, consentimientos `CREDITO`/`BUSQUEDA`, eventos nuevos ni filas en las tablas nuevas |

Diferencias con la sección 14 de los criterios (PROPUESTO del arquitecto): se añade `departamento_id` (la búsqueda admite departamento sin ciudad) y el presupuesto es `BIGINT` (pesos enteros) en lugar de `DECIMAL(15,0)`.

## Cambios en pruebas existentes

- `SesionTests.Inventario_de_endpoints`: rutas anónimas nuevas (`POST /api/creditos`, `POST /api/busquedas`, `GET /api/catalogo/listas-busqueda`; `/api/catalogo` pasa de 5 a 6 rutas) con `LimitePeticionesContacto`; `/api/leads` pasa de 7 a 10 rutas; `/api/mis-solicitudes` exige `SoloComprador`.
- P5b: `ATENDIDA` sigue siendo válido en avisos; los resultados nuevos dan "Ese resultado no aplica a esta solicitud." en contactos y avisos.

## Riesgos

- **Orden de despliegue**: la API de P6 no debe arrancar contra `camion_ventas` sin la 015 (EF mapea `leads_busqueda` y `busqueda_vehiculos`; créditos, búsquedas, relacionar vehículos y "Mis solicitudes" fallarían; los CHECK viejos rechazarían los orígenes nuevos).
- **Cupo por IP compartido** entre cinco formularios; detrás del proxy de Vite todos comparten 127.0.0.1 (riesgo heredado).
- **Duplicados simultáneos**: riesgo aceptado (B-2, D-49).
- **La política sigue en borrador** (D-44) y debe añadir la finalidad "solicitud de crédito o búsqueda" sin datos financieros (tarea documental).

## Cambios al contrato

Cada cambio posterior se anota aquí con fecha, motivo y efecto en el frontend.

- 2026-11-10 (pedido del frontend, aprobado por el coordinador): nueva ruta pública `GET /api/catalogo/listas-busqueda` con categorías, marcas y modelos activos (con sus categorías) y DIVIPOLA completo. Motivo: `/api/inventario/catalogos` es solo para asesor y administrador, y `/api/catalogo/filtros` solo trae lo que tiene publicaciones visibles. Efecto en el frontend: el formulario "Busco un vehículo" usa esta ruta; no cambia ningún otro campo.
- 2026-11-10 (revisiones de seguridad y QA; D-7, D-59 a, D-60):
  - M-P6-1: el repetido del contacto de vehículo (P5) solo mira leads `CONTACTO_VEHICULO`; un crédito pedido desde esa ficha ya no absorbe el contacto, que vuelve a llegar al asesor dueño (D-7). El repetido del crédito ya filtraba por `CREDITO`.
  - O-1 (D-60): el repetido de contacto, avisos, crédito y búsqueda exige el mismo `usuario_id` (NULL con NULL cuenta como igual). Un comprador con sesión cuyo teléfono tiene una solicitud abierta anónima o de otra cuenta obtiene una solicitud nueva que aparece en "Mis solicitudes" (CA-P6-06, CA-P6-23). Sin efecto en el frontend: la respuesta sigue siendo la misma.
  - `GET /api/leads/{id}/coincidencias` responde 409 "Esta búsqueda no tiene criterios guardados." si falta la fila de criterios (antes, 500).
