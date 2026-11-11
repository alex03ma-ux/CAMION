# P5b — Contrato de la API: avisos "Vende tu camión" y "Compra tu seguro todo riesgo"

Fijado antes de programar, a partir de `docs/criterios/criterios-P5b-avisos.md` (CA-P5b-01 a CA-P5b-62) y de las decisiones D-6, D-7, D-9, D-44, D-46, D-49 y D-50 a D-53 (todas TOMADAS). Extiende `docs/criterios/P5-contrato-api.md` sin cambiar lo que allí no se menciona: formato de error `{ error, errores? }`, auditoría en `auditoria_eventos`, límites `Contactos:*` y política `PoliticaDatos:Version`.

El frontend se programa contra este documento. **Cualquier cambio posterior se anota en la sección "Cambios al contrato" al final.**

**Trazabilidad**

| Código | Tema | Regla |
|---|---|---|
| HU-N01 | Aviso "Vende tu camión" | D-6, D-50 (b, c) |
| HU-N02 | Aviso "Compra tu seguro todo riesgo" | D-6, D-50 (a, c) |
| — | Siempre al administrador, nunca a un asesor | D-6, D-7, D-52 (A) |
| — | Sin cuenta; nombre y teléfono obligatorios, correo opcional | D-3, D-9, RN-SE-005 |
| HU-061, HU-062 | Auditoría y autorización de datos | RN-SE-003, RN-SE-004, D-44 |
| HU-032, HU-033 | Seguimiento y cierre (reutilizados) | RN-CL-004 |
| HU-071 | "Contactos (N nuevos)" | D-48 |

## Reglas de P5b

- **Un aviso es un lead sin publicación** con `origen` = `VENDE_TU_CAMION` o `SEGURO_TODO_RIESGO`. Los contactos de P5 tienen `origen` = `CONTACTO_VEHICULO`. Se reutilizan `leads`, `eventos_lead`, `consentimientos_datos`, la bandeja del administrador, notas, cierre, reapertura y auditoría.
- **Responsable**: siempre Administración (`responsable_asesor_id = NULL`), sin importar quién envía ni lo que traiga la petición (CA-P5b-10, 32, 35). La base lo impide con `CK_Lead_AvisoAdmin` (D-52 A).
- **Quién envía**: cualquiera, sin cuenta (D-3, D-6). Con un token válido de cualquier rol (comprador, asesor, administrador) el aviso queda ligado a su usuario (`usuario_id`); un token inválido o vencido se trata como anónimo. No hay 409 por "propio" (no hay vehículo).
- **No crea** vehículos, publicaciones, fotos ni planes (D-6, CA-P5b-33).
- **Estados**: los de P5 (`ABIERTO`, `CERRADO`), nace ABIERTO; el administrador puede reabrir.
- **El comprador no ve sus avisos** en P5b (D-53 A): no hay ruta de lectura para él; `/api/leads` sigue dándole 403.
- **Campos ignorados**: `slug`, `publicacionId`, `origen`, `estado`, `asesorId`, `responsable`, `versionPolitica` y cualquier otro campo no listado se ignoran (CA-P5b-08, CA-P5b-10). Tampoco se piden ciudad, marca ni año (D-50 b).

## Rutas públicas nuevas

| Método | Ruta | Rol | Limitación |
|---|---|---|---|
| POST | `/api/avisos/vende-tu-camion` | anónimo (token opcional) | `LimitePeticionesContacto` |
| POST | `/api/avisos/seguro-todo-riesgo` | anónimo (token opcional) | `LimitePeticionesContacto` |

### POST /api/avisos/vende-tu-camion (HU-N01)

Cuerpo (JSON, máx. 8 KB) — `VendeTuCamionRequest`:

```json
{ "nombre": "Ana Pérez", "telefono": "300 123 4567", "correo": null,
  "descripcion": "Kenworth T800 2015, 450.000 km, en Medellín", "autorizacionDatos": true, "sitioWeb": "" }
```

| Campo JSON | Tipo | Regla | Mensaje 400 (en `errores.<campo>`) |
|---|---|---|---|
| `nombre` | string | obligatorio, 2 a 150 caracteres tras quitar espacios | "Escribe tu nombre (entre 2 y 150 caracteres)." |
| `telefono` | string | obligatorio; celular (3…) o fijo (60…) colombiano de 10 dígitos, con o sin 57/+57 y separadores; se guarda `+57XXXXXXXXXX` (regla de P5, D-50 c) | "Escribe un celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567." |
| `correo` | string \| null | opcional; formato válido y hasta 254; se guarda en minúsculas (D-9, D-50 c) | "Escribe un correo válido o deja el campo vacío." |
| `descripcion` | string | obligatoria; vacía o solo espacios → error | "Describe tu vehículo (máximo 500 caracteres)." |
| `descripcion` | string | más de 500 caracteres tras quitar espacios | "La descripción no puede pasar de 500 caracteres." |
| `autorizacionDatos` | boolean | debe ser `true` (ausente, `null` o `false` → error) | "Debes autorizar el tratamiento de tus datos para enviar el formulario." |
| `sitioWeb` | string | campo trampa: debe llegar vacío o ausente | (relleno → 201 sin guardar nada) |

Se guarda: `origen = VENDE_TU_CAMION`, `publicacion_id = NULL`, `tipo_vehiculo = NULL`, `mensaje` = la descripción (recortada).

### POST /api/avisos/seguro-todo-riesgo (HU-N02)

Cuerpo (JSON, máx. 8 KB) — `SeguroTodoRiesgoRequest`:

```json
{ "nombre": "Ana Pérez", "telefono": "300 123 4567", "correo": null,
  "tipoVehiculo": "CAMION", "autorizacionDatos": true, "sitioWeb": "" }
```

| Campo JSON | Tipo | Regla | Mensaje 400 |
|---|---|---|---|
| `nombre`, `telefono`, `correo`, `autorizacionDatos`, `sitioWeb` | | como en "Vende tu camión" | los mismos |
| `tipoVehiculo` | string | obligatorio; uno de `CAMION`, `CARRO`, `MAQUINARIA`, `OTRO` (D-50 a). Se admiten mayúsculas o minúsculas y espacios alrededor; se guarda en mayúsculas | "Elige el tipo de vehículo." |

Etiquetas para la interfaz: `CAMION` = "Camión", `CARRO` = "Carro", `MAQUINARIA` = "Maquinaria", `OTRO` = "Otro". Con "Otro" no se piden más datos (CA-P5b-16).

Se guarda: `origen = SEGURO_TODO_RIESGO`, `publicacion_id = NULL`, `tipo_vehiculo` = el elegido, `mensaje` = "Solicitud de seguro todo riesgo".

### Respuestas (ambas rutas)

| Código | Cuándo | Cuerpo |
|---|---|---|
| 201 | aviso nuevo, aviso repetido o campo trampa | `{ "mensaje": "¡Listo! Recibimos tu solicitud. Te contactaremos pronto por teléfono o WhatsApp." }` (sin id ni responsable) |
| 400 | validación | `{ "error": "Revisa los datos marcados.", "errores": { "<campo>": ["<mensaje>"] } }` |
| 400 | JSON malformado, cuerpo vacío o tipos incorrectos | `{ "error": "La solicitud no tiene un formato válido." }` |
| 413 | cuerpo de más de 8 KB (con o sin Content-Length) | `{ "error": "La solicitud es demasiado grande." }` |
| 429 | límite por IP o por teléfono | `{ "error": "Has enviado muchas solicitudes. Intenta de nuevo en unos minutos." }` |
| 500 | error inesperado | `{ "error": "No se pudo completar la operación. Intenta de nuevo en unos minutos." }` (formato común; sin trazas) |

Orden: límite por IP (middleware) → tamaño (413) → formato (400) → campo trampa (201) → campos (400) → límite por teléfono (429) → repetido (201) → aviso nuevo (201).

### Anti-abuso (CA-P5b-22 a 27)

- **Por IP** (CA-P5b-24): las dos rutas llevan `LimitePeticionesContacto`. El limitador particiona solo por IP, así que el cupo (5 cada 10 min, 20 al día; `Contactos:LimiteVentanaCorta`, `Contactos:VentanaCortaMinutos`, `Contactos:LimiteDiario`) se **comparte** entre `POST /api/contactos` y ambos avisos.
- **Por teléfono** (CA-P5b-25): `Contactos:LimiteTelefonoDia` (10) cuenta, en las últimas 24 h y para el teléfono normalizado, todos los leads creados (contactos y avisos) más los eventos `CONTACTO_REPETIDO` de cualquier lead de ese teléfono. Se comprueba antes de la rama de repetido. Al superarlo, 429 sin guardar nada.
- **Tamaño** 8 KB (`Contactos:TamanoMaximoBytes`) y **campo trampa** `sitioWeb`: como P5.
- **Repetido** (CA-P5b-26): si hay un lead ABIERTO con el **mismo origen** y el mismo teléfono normalizado, no se crea otro: se añade `CONTACTO_REPETIDO` a su historial y se guarda su consentimiento. Texto del evento: la descripción nueva (N01) o "Solicitud de seguro todo riesgo. Tipo de vehículo: <Etiqueta>." (N02). El lead conserva su `tipo_vehiculo` original. No vuelve a marcar el lead como nuevo (D-49).
- **Nuevo** (CA-P5b-27): si el anterior del mismo origen está CERRADO, o el abierto es de otro origen, se crea un lead nuevo.

### Autorización de datos (CA-P5b-19 a 21)

Cada envío aceptado (nuevo o repetido) guarda en la misma transacción una fila de `consentimientos_datos`: `formulario` = `VENDE_TU_CAMION` o `SEGURO_TODO_RIESGO` (ya admitidos por `CK_Consent_Formulario` de la 013), `version_politica` del servidor (`PoliticaDatos:Version`), `aceptado_en` UTC, `usuario_id` si hay sesión, `entidad = 'Lead'`, `entidad_id` = id del lead. Si cualquier escritura falla, no queda nada (lead, eventos, consentimiento ni auditoría) y se responde 500.

### Historial y auditoría al crear (CA-P5b-32, 34)

- `eventos_lead`: `CREADO` y `ASIGNADO` (automático, actor SISTEMA, `responsable_nuevo_asesor_id = NULL`).
- `auditoria_eventos`: `Lead / CREAR`, `usuario_id = NULL`, datos nuevos `{ "origen": "...", "responsableAsesorId": null, "conCuenta": true|false }` (más `"tipoVehiculo"` en N02). Nunca nombre, teléfono, correo ni descripción; tampoco en el registro técnico.

## Cambios en `/api/leads` (gestión)

Mismas rutas, roles y códigos de P5. Cambios:

### Alcance

- **Asesor** (CA-P5b-46, 47): nunca ve avisos. La lista, el resumen (`nuevos` y `abiertos`) y el detalle solo consideran leads `CONTACTO_VEHICULO` de los que es responsable. Pedir un aviso por id (detalle, notas, cerrar) → 404 "Contacto no encontrado."; reabrir y reasignar → 403 (solo administrador, como en P5).
- **Comprador** → 403; **sin token** → 401 (sin cambios, CA-P5b-48, 50).
- **Administrador**: ve contactos y avisos. Los avisos ABIERTOS no vistos cuentan en `GET /api/leads/resumen` → `nuevos` (son de Administración, CA-P5b-39). Abrir el detalle de un aviso lo marca como visto.

### Filtro nuevo en `GET /api/leads`

- `origen`: `CONTACTO_VEHICULO`, `VENDE_TU_CAMION` o `SEGURO_TODO_RIESGO` (mayúsculas o minúsculas). Otro valor → 400 `{ "error": "Revisa los filtros.", "errores": { "origen": ["Origen no válido."] } }`.
- `publicacionId` y `vehiculoId` excluyen los avisos (no tienen vehículo, CA-P5b-44). `responsable=ADMINISTRACION` los incluye.

### `LeadItem` y `LeadDetalle` (campos nuevos o cambiados)

```text
id, estado, nombre, telefono, correo?, mensaje, creadoEn, nuevo,
origen ("CONTACTO_VEHICULO" | "VENDE_TU_CAMION" | "SEGURO_TODO_RIESGO"),      <- NUEVO
tipoVehiculo ("CAMION" | "CARRO" | "MAQUINARIA" | "OTRO" | null),             <- NUEVO (solo SEGURO_TODO_RIESGO)
vehiculo { vehiculoId, publicacionId, titulo, slug, estadoVehiculo, estadoPublicacion } | null,   <- AHORA ADMITE null
responsable { tipo, asesorId?, nombre? }, resultadoCierre?, cerradoEn?
```

- `vehiculo` es `null` en los avisos ("Sin vehículo asociado", CA-P5b-38); siempre presente en `CONTACTO_VEHICULO`.
- **Descripción**: no hay campo aparte. En `VENDE_TU_CAMION`, `mensaje` contiene la descripción del vehículo; en `SEGURO_TODO_RIESGO`, `mensaje` es "Solicitud de seguro todo riesgo" y el dato útil es `tipoVehiculo`.
- `responsable.tipo` es siempre `ADMINISTRACION` en los avisos.
- `LeadDetalle.consentimientos[].formulario` será `VENDE_TU_CAMION` o `SEGURO_TODO_RIESGO` en los avisos (solo para el administrador, como en P5).

### Cerrar (CA-P5b-41)

Lista ampliada de `resultado` con `ATENDIDA` ("Solicitud atendida"). Resultados válidos **por origen**:

| Resultado | `CONTACTO_VEHICULO` | Avisos |
|---|---|---|
| `VENTA_CONCRETADA` | sí | no |
| `VEHICULO_NO_DISPONIBLE` | sí | no |
| `ATENDIDA` | no | sí |
| `NO_INTERESADO`, `NO_RESPONDIO`, `DUPLICADO_INVALIDO`, `OTRO` | sí | sí |

- Valor que no está en la lista → 400 en `resultado` "Elige el resultado del cierre." (como P5).
- Valor de la lista que no aplica al origen → 400 en `resultado` "Ese resultado no aplica a esta solicitud.". Nota: en P5, `ATENDIDA` en un contacto de vehículo daba "Elige el resultado del cierre."; ahora da este mensaje (sigue siendo 400).
- `OTRO` exige observación de 5 a 500, igual que P5. Ya cerrado → 409 "Este contacto ya está cerrado.".

### Notas y reabrir

Sin cambios para los avisos (CA-P5b-40, 42): nota 5 a 500 (400), aviso cerrado → 409 "Este contacto está cerrado."; reabrir con motivo 5 a 500, solo administrador, vuelve a ABIERTO en Administración.

### Reasignar (CA-P5b-43, D-52 A)

`POST /api/leads/{id}/reasignar` sobre un aviso (cualquier destino, también `administracion: true`) → **409** `{ "error": "Esta solicitud la atiende siempre la administración." }`. Se comprueba justo después de la autorización (admin) y antes de validar el cuerpo; no cambia nada ni deja auditoría.

### Barrido D-46 y reasignación de vehículo (CA-P5b-36)

No tocan los avisos: el barrido solo mira leads con responsable asesor, y "seguir al vehículo" solo los que tienen publicación de ese vehículo.

## Esquema: migración `database/migraciones/014_p5b_avisos.sql` (+ `_SUBIR.sql`)

Solo `ALTER TABLE leads`; no crea tablas ni toca datos (los leads existentes toman el valor por defecto `CONTACTO_VEHICULO`). La aplica la responsable con respaldo previo (D-14, D-16).

| Cambio | Detalle |
|---|---|
| `publicacion_id` | `BIGINT NULL` (antes NOT NULL). FK `FK_Lead_Publicacion` RESTRICT se mantiene |
| `origen` (nueva) | `VARCHAR(30) NOT NULL DEFAULT 'CONTACTO_VEHICULO'` |
| `tipo_vehiculo` (nueva) | `VARCHAR(20) NULL` |
| `CK_Lead_Origen` (nueva) | `(origen = 'CONTACTO_VEHICULO' AND publicacion_id IS NOT NULL AND tipo_vehiculo IS NULL) OR (origen = 'VENDE_TU_CAMION' AND publicacion_id IS NULL AND tipo_vehiculo IS NULL) OR (origen = 'SEGURO_TODO_RIESGO' AND publicacion_id IS NULL AND tipo_vehiculo IS NOT NULL AND tipo_vehiculo IN ('CAMION','CARRO','MAQUINARIA','OTRO'))` |
| `CK_Lead_AvisoAdmin` (nueva) | `origen = 'CONTACTO_VEHICULO' OR responsable_asesor_id IS NULL` |
| `CK_Lead_Resultado` | Se reemplaza (DROP CONSTRAINT y ADD en dos sentencias seguidas) para admitir `ATENDIDA` |
| `IX_Lead_Origen` (nuevo) | `(origen, estado, creado_en)` |
| `IX_Lead_AvisoDuplicado` (nuevo) | `(origen, telefono, estado)` |
| BAJAR (comentada) | Solo si no hay filas con `origen <> 'CONTACTO_VEHICULO'` ni `resultado_cierre = 'ATENDIDA'` |

## Cambios en pruebas existentes

- `SesionTests.Inventario_de_endpoints`: añade las dos rutas a anónimos y a las que modifican.

## Riesgos

- **Orden de despliegue**: la API de P5b no debe arrancar contra `camion_ventas` sin la 014 (fallarían contacto, avisos, `/api/leads`, bloquear asesor y reasignar vehículo, porque EF lee `origen` y `tipo_vehiculo`).
- **Cupo por IP compartido**: un visitante que envía 5 contactos ya no puede enviar un aviso en esos 10 minutos (es lo pedido en CA-P5b-24). Detrás del proxy de Vite todos comparten la IP 127.0.0.1 (riesgo heredado de P5).
- **Duplicados simultáneos**: mismo riesgo aceptado de P5 (B-2, D-49).
- **La política sigue en borrador** (D-44): los avisos no salen a internet hasta su aprobación.

## Cambios al contrato

Cada cambio posterior se anota aquí con fecha, motivo y efecto en el frontend.

- 2026-11-06 (antes de entregar; sin efecto en el frontend): `CK_Lead_Origen` añade `tipo_vehiculo IS NOT NULL` en la rama del seguro. Motivo: un `IN` con NULL da NULL y el CHECK dejaba pasar un seguro sin tipo (lo detectó la prueba CA-P5b-62).
- 2026-11-10 (P6, D-60; sin efecto en el frontend): el **repetido** de los avisos y del contacto de P5 solo se agrega al lead ABIERTO si tiene el mismo `usuario_id` que el envío (dos anónimos cuentan como iguales); si no, se crea un lead nuevo. Nunca se liga una solicitud anónima a una cuenta (D-59 a). El contacto de P5 además solo mira leads `CONTACTO_VEHICULO` (M-P6-1, D-7). Los límites por teléfono no cambian. Detalle en `P6-contrato-api.md`.
