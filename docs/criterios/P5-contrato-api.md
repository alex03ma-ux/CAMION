# P5 — Contrato de la API: leads y contactos

Fijado antes de programar, a partir de `docs/criterios/criterios-P5-leads.md` (CA-P5-01 a CA-P5-64) y de las restricciones del coordinador. Se mantienen el formato `{ error, errores? }`, la auditoría (`auditoria_eventos`) y los contratos de P1 a P4b.

**Trazabilidad** (matriz oficial de 87 HU, D-1):

| HU | Tema | Regla |
|---|---|---|
| HU-029 | Contactar asesor | RN-CL-001, D-3, D-9 |
| HU-030 | Generar lead | RN-CL-002 |
| HU-031 | Asignar lead | RN-CL-003 modificada por D-7 y D-46 (B) |
| HU-032 | Gestionar lead | RN-CL-004 |
| HU-033 | Cerrar lead | RN-CL-004 |
| HU-071 | Aviso de lead nuevo | RN-NO-002, parcial por D-48 (solo dentro del panel) |
| HU-061 / HU-062 | Auditoría y datos personales | RN-SE-003, RN-SE-004, D-44 |

También aplican RN-SE-005 y RN-SE-006 (anónimo contacta; permisos por rol), D-34 (reasignar vehículo), D-41 (vigencia = plan), D-42 (WhatsApp del portal), D-45 (la plataforma solo pone en contacto) y D-47 (botón de WhatsApp).

## Reglas de negocio de P5

- **Quién contacta.** Cualquiera, sin cuenta (RN-SE-005, D-3). Si llega con un token válido, el lead queda ligado a ese usuario (`usuario_id`) además de guardar los datos escritos (CA-P5-02). Un token inválido o vencido se trata como anónimo (la ruta es anónima).
- **Sobre qué.** Una publicación **contactable**: PUBLICADA, estado comercial DISPONIBLE o RESERVADO, vehículo ACTIVO o RESERVADO, con vigencia iniciada, título y precio (condiciones 1, 2, 3, 5 y 7 de `ConsultasCatalogo.Visibles`). **No** se exige el plan vigente ni la cuenta activa del dueño (condiciones 4 y 6): CA-P5-26 y CA-P5-27 piden aceptar el contacto y mandarlo a Administración. Cualquier otra publicación, o un slug que no existe, da **404** (CA-P5-09). `ConsultasCatalogo.Visibles` pasa a construirse como `Contactables` + condiciones 4 y 6, sin cambiar su resultado.
- **Identificación de la publicación.** Por su `slug` (el portal no expone ids, CA-DET-04).
- **Asignación al crear (D-7, CA-P5-25 a 28).** Al asesor dueño (`publicaciones.asesor_id`) si: su perfil está APROBADO, su cuenta ACTIVA con rol ASESOR (`InventarioEndpoints.AsesorActivoAsync`), **no** tiene rol ADMINISTRADOR y tiene suscripción vigente (`DatosP4b.SuscripcionVigenteAsync`). Si no, a **Administración** (`responsable_asesor_id = NULL`): lo ve y lo atiende cualquier administrador. El cliente nunca elige ni ve el responsable; cualquier campo de responsable o estado que envíe se ignora (CA-P5-08).
- **Publicación "a cargo del administrador" (D-47, CA-P5-62).** Se interpreta como: el usuario dueño del perfil de asesor tiene el rol ADMINISTRADOR (un asesor promovido conserva su perfil). Sus leads van a Administración y su botón de WhatsApp es el del portal.
- **Estados (v3.0 §12-13).** `ABIERTO` y `CERRADO`. Se nace ABIERTO.
- **Duplicados (CA-P5-23, CA-P5-24).** Si existe un lead ABIERTO con el mismo teléfono normalizado y la misma publicación, no se crea otro: se añade al historial un evento `CONTACTO_REPETIDO` con el mensaje nuevo y se guarda su prueba de autorización. Si el anterior está CERRADO, se crea uno nuevo. La respuesta es idéntica en los tres casos.
- **Nuevos (D-48, CA-P5-58).** `visto_por_responsable` nace en falso y vuelve a falso cada vez que cambia el responsable. Pasa a verdadero cuando el responsable abre el detalle: el asesor asignado, o cualquier administrador si el responsable es Administración. Abrir el lead de un asesor como administrador no lo marca como visto para el asesor.

### D-46 (B): los leads siguen al plan

- **Barrido** (`Leads/BarridoLeads.cs`), idempotente: pasa a Administración los leads ABIERTO de un asesor que
  - tiene la cuenta no ACTIVA o el perfil no APROBADO → motivo **"Asesor bloqueado"**, o
  - no tiene suscripción vigente → motivo **"Plan vencido"**, salvo que el lead le haya llegado por **reasignación manual** del administrador (ver abajo).
- Cada paso deja un evento `REASIGNADO` (automático, actor SISTEMA) y una fila `Lead / REASIGNAR` en `auditoria_eventos` con `usuario_id = NULL`. Cada lead se mueve con un `UPDATE … WHERE id = ? AND responsable_asesor_id = ? AND estado = 'ABIERTO'`: si otra petición ya lo movió, no se repite el evento.
- **Cuándo corre (bajo demanda; no hay BackgroundService):**
  - al principio de **cada** endpoint de `/api/leads` (lista, resumen, detalle y acciones), para todos los asesores;
  - dentro de la transacción de `POST /api/admin/usuarios/{id}/bloquear`, para el asesor bloqueado (motivo "Asesor bloqueado").
  Por eso el paso ocurre "como tarde en la siguiente consulta de leads" (CA-P5-50). La fecha del evento es la del barrido, no la del vencimiento exacto.
- **Reasignar el vehículo (D-34, CA-P5-52).** Dentro de la transacción de `POST /api/admin/vehiculos/{id}/reasignar`, los leads ABIERTO de cualquier publicación del vehículo **que estaban asignados al dueño anterior** pasan al nuevo dueño, con su evento `REASIGNADO` (automático, actor = el administrador que reasignó el vehículo, texto "El vehículo se reasignó a otro asesor.") y su auditoría. Los CERRADOS no cambian. Los que estaban en Administración o con otro asesor por reasignación manual tampoco (ver Ambigüedades).
- **No vuelven solos (CA-P5-53).** Renovar el plan o desbloquear no mueve nada.
- **Reasignación manual y plan (CA-P5-47 frente a D-46 B).** El administrador puede reasignar a un asesor aprobado sin plan vigente. Ese lead lleva `asignacion_manual = 1` y el barrido por "Plan vencido" no lo toca (si no, lo devolvería en la siguiente consulta). El barrido por "Asesor bloqueado" sí. Al volver a una asignación automática (vehículo reasignado) o a Administración, `asignacion_manual = 0`.

### Autorización de tratamiento de datos (D-44, CA-P5-13 a 19)

- `autorizacionDatos` es obligatorio y debe ser `true`. Ausente, `null` o `false` → 400 en `autorizacionDatos`. No se guarda nada.
- Cada envío aceptado (lead nuevo o contacto repetido) guarda una fila en `consentimientos_datos` **en la misma transacción** que el lead o el evento: `formulario = CONTACTO_VEHICULO`, `version_politica` (de la configuración `PoliticaDatos:Version`, hoy `BORRADOR-2026-11-04`; la que mande el cliente se ignora), `aceptado_en` (UTC), `usuario_id` (si hay sesión), `entidad = 'Lead'` y `entidad_id`.
- La tabla es común: P5b y los registros usan la misma regla con otro `formulario` (`VENDE_TU_CAMION`, `SEGURO_TODO_RIESGO`, `REGISTRO_COMPRADOR`, `INSCRIPCION_ASESOR`) y su propia `entidad`.
- No hay endpoint para editar ni borrar consentimientos. Solo el administrador los ve, dentro del detalle del lead (CA-P5-16).

### Anti-abuso (CA-P5-20 a 22)

| Regla | Valor por defecto | Configuración | Respuesta |
|---|---|---|---|
| Peticiones por IP en la ventana corta | 5 cada 10 minutos | `Contactos:LimiteVentanaCorta`, `Contactos:VentanaCortaMinutos` | 429 |
| Peticiones por IP al día | 20 (ventana fija de 24 h) | `Contactos:LimiteDiario` | 429 |
| Leads nuevos por teléfono normalizado en 24 h | 10 | `Contactos:LimiteTelefonoDia` | 429 |
| Tamaño del cuerpo | 8 KB (8192 bytes) | `Contactos:TamanoMaximoBytes` | 413 |
| Campo trampa `sitioWeb` relleno | — | — | 201 normal, no guarda nada |

- La limitación por IP usa `Microsoft.AspNetCore.RateLimiting` (incluido en .NET 8, sin paquetes nuevos), con un limitador global encadenado que solo actúa sobre las rutas marcadas con `LimitePeticionesContacto` (hoy `POST /api/contactos`; P5b marcará las suyas). Cuenta todas las peticiones a la ruta, también las rechazadas por validación.
- La IP es `Connection.RemoteIpAddress`. No se leen cabeceras `X-Forwarded-For` (ver Riesgos).
- El límite por teléfono cuenta leads creados (no contactos repetidos) en las últimas 24 h y se comprueba solo cuando se va a crear un lead nuevo.
- Las pruebas suben los límites en la configuración para no volverse frágiles; una clase de pruebas propia los baja para comprobar el 429.

### Teléfono (CA-P5-04, CA-P5-05)

- Caracteres admitidos: dígitos, espacio, `+ - ( )`; hasta 25 caracteres (la misma expresión del registro, ahora pública en `ValidacionAuth`).
- Se quitan los separadores. Se acepta: 10 dígitos que empiezan por `3` (celular) o por `60` (fijo), o los mismos 10 dígitos precedidos de `57` (con o sin `+`). Se guarda como `+57XXXXXXXXXX`.
- Se rechazan 7 dígitos, números extranjeros y cualquier otro formato.
- El registro de comprador y de asesor **no cambia** (sigue con la regla genérica de 7 a 25 caracteres).

### WhatsApp en el detalle público (D-47, CA-P5-61 a 63)

- `GET /api/catalogo/publicaciones/{slug}` añade **un solo campo**: `whatsapp`, un enlace `https://wa.me/57XXXXXXXXXX` sin texto.
- Es el teléfono del asesor dueño (`AspNetUsers.PhoneNumber`) si, normalizado con la regla de arriba, es un **celular** (empieza por 3). Si es fijo, no es válido, el dueño tiene rol ADMINISTRADOR o no hay teléfono, es el del portal: `https://wa.me/573166152907` (configuración `Contactos:WhatsappPortal`, D-42).
- Nunca se envían el correo, el documento, el nombre ni el id del asesor. Las demás rutas del catálogo no cambian, y siguen siendo 5 GET.
- Una publicación no visible sigue dando 404 (CA-P5-63): no se expone ningún teléfono.
- El texto inicial del mensaje (`?text=`) lo arma el frontend con el título del vehículo.

## Esquema: migración `database/migraciones/013_p5_leads.sql` (+ `_SUBIR.sql`)

**No se usa la tabla `solicitudes` del script 002** ni `asignaciones_solicitud` / `seguimiento_solicitud`: no están mapeadas, exigen usuario y correo (contradicen D-9), sus estados no son los de la v3.0 y no sabemos si existen en la base real. P5 crea tablas propias y no toca aquellas. CA-P5-44 menciona `asignaciones_solicitud`: la reasignación queda en `eventos_lead` (tipo `REASIGNADO`, de quién a quién, quién, cuándo y motivo) y en `auditoria_eventos`.

**`leads`**

| Columna | Tipo | Nota |
|---|---|---|
| `id` | BIGINT PK AUTO_INCREMENT | |
| `publicacion_id` | BIGINT NOT NULL | FK `publicaciones`, RESTRICT |
| `usuario_id` | VARCHAR(255) NULL | FK `AspNetUsers`, SET NULL. Quien contactó con sesión |
| `nombre` | VARCHAR(150) NOT NULL | |
| `telefono` | VARCHAR(20) NOT NULL | normalizado `+57…` |
| `correo` | VARCHAR(254) NULL | D-9 |
| `mensaje` | VARCHAR(500) NOT NULL | "Me interesa este vehículo" si llega vacío |
| `estado` | VARCHAR(20) NOT NULL DEFAULT 'ABIERTO' | CHECK `ABIERTO`, `CERRADO` |
| `responsable_asesor_id` | BIGINT NULL | FK `perfiles_asesor`, RESTRICT. NULL = Administración |
| `asignacion_manual` | TINYINT(1) NOT NULL DEFAULT 0 | ver D-46 |
| `visto_por_responsable` | TINYINT(1) NOT NULL DEFAULT 0 | D-48 |
| `resultado_cierre` | VARCHAR(30) NULL | CHECK de los 6 valores |
| `observacion_cierre` | VARCHAR(500) NULL | |
| `cerrado_por` | VARCHAR(255) NULL | FK `AspNetUsers`, SET NULL |
| `cerrado_en` | DATETIME(6) NULL | |
| `creado_en`, `actualizado_en` | DATETIME(6) NOT NULL | |

CHECK `CK_Lead_Cierre`: `estado = 'ABIERTO' OR resultado_cierre IS NOT NULL`. Índices: `IX_Lead_Responsable (responsable_asesor_id, estado, creado_en)`, `IX_Lead_Duplicado (publicacion_id, telefono, estado)`, `IX_Lead_Telefono (telefono, creado_en)`, `IX_Lead_Creado (creado_en)`.

**`eventos_lead`** (historial; solo inserciones)

| Columna | Tipo | Nota |
|---|---|---|
| `id` | BIGINT PK | |
| `lead_id` | BIGINT NOT NULL | FK `leads`, RESTRICT |
| `tipo` | VARCHAR(30) NOT NULL | CHECK `CREADO`, `ASIGNADO`, `CONTACTO_REPETIDO`, `NOTA`, `REASIGNADO`, `CERRADO`, `REABIERTO` |
| `actor_usuario_id` | VARCHAR(255) NULL | FK `AspNetUsers`, SET NULL. NULL = SISTEMA |
| `automatico` | TINYINT(1) NOT NULL DEFAULT 0 | |
| `responsable_anterior_asesor_id` | BIGINT NULL | FK `perfiles_asesor`. NULL = Administración |
| `responsable_nuevo_asesor_id` | BIGINT NULL | FK `perfiles_asesor`. NULL = Administración |
| `texto` | VARCHAR(500) NULL | nota, motivo, mensaje repetido u observación del cierre |
| `resultado_cierre` | VARCHAR(30) NULL | solo en `CERRADO`; se conserva aunque el lead se reabra |
| `creado_en` | DATETIME(6) NOT NULL | |

Nunca guarda el teléfono ni el correo del visitante (CA-P5-28). Índice `IX_EvLead_Lead (lead_id, creado_en)`.

**`consentimientos_datos`** (prueba de autorización, común a P5, P5b y registros)

| Columna | Tipo | Nota |
|---|---|---|
| `id` | BIGINT PK | |
| `formulario` | VARCHAR(30) NOT NULL | CHECK de los 5 valores |
| `version_politica` | VARCHAR(40) NOT NULL | |
| `aceptado_en` | DATETIME(6) NOT NULL | UTC |
| `usuario_id` | VARCHAR(255) NULL | FK `AspNetUsers`, SET NULL |
| `entidad` | VARCHAR(40) NOT NULL | `Lead` en P5 |
| `entidad_id` | VARCHAR(100) NOT NULL | |

Índice `IX_Consent_Entidad (entidad, entidad_id)`.

La sección BAJAR, comentada, elimina solo las tres tablas nuevas (en orden inverso) y exige antes comprobar que estén vacías o exportadas. La parte SUBIR va en un archivo aparte, sin consultas a `information_schema` (D-16). **No se aplica** a `camion_ventas` hasta la aprobación y el respaldo (D-16).

## Endpoints

### Público

| Método | Ruta | Rol | Uso |
|---|---|---|---|
| POST | `/api/contactos` | anónimo (o con token) | Crear un contacto (HU-029, HU-030) |

Cuerpo (JSON, máx. 8 KB):

```json
{ "slug": "chevrolet-npr-2020-15", "nombre": "Ana", "telefono": "300 123 4567", "correo": null,
  "mensaje": null, "autorizacionDatos": true, "sitioWeb": "" }
```

Respuestas:

| Código | Cuándo | Cuerpo |
|---|---|---|
| 201 | lead nuevo, contacto repetido o campo trampa | `{ "mensaje": "¡Listo! Recibimos tus datos. Te contactaremos pronto por teléfono o WhatsApp." }` (sin id ni responsable) |
| 400 | validación | `{ "error": "Revisa los datos marcados.", "errores": { campo: [mensaje] } }` |
| 400 | JSON malformado o tipos incorrectos | `{ "error": "La solicitud no tiene un formato válido." }` |
| 404 | publicación no contactable o inexistente | `{ "error": "Este vehículo ya no está disponible. Mira otros vehículos en el catálogo." }` |
| 409 | el asesor con sesión es el dueño | `{ "error": "Este vehículo es tuyo; no puedes contactarte a ti mismo." }` |
| 413 | cuerpo de más de 8 KB | `{ "error": "La solicitud es demasiado grande." }` |
| 429 | límite por IP o por teléfono | `{ "error": "Has enviado muchas solicitudes. Intenta de nuevo en unos minutos." }` |

Mensajes por campo (400):

| Campo | Regla | Mensaje |
|---|---|---|
| `slug` | obligatorio, hasta 200 | "Indica el vehículo que quieres contactar." |
| `nombre` | 2 a 150 tras quitar espacios | "Escribe tu nombre (entre 2 y 150 caracteres)." |
| `telefono` | celular o fijo colombiano | "Escribe un celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567." |
| `correo` | opcional; formato y hasta 254 | "Escribe un correo válido o deja el campo vacío." |
| `mensaje` | opcional; hasta 500 | "El mensaje no puede pasar de 500 caracteres." |
| `autorizacionDatos` | debe ser `true` | "Debes autorizar el tratamiento de tus datos para enviar el formulario." |

Orden: tamaño (413) → formato (400) → campo trampa (201) → campos (400) → publicación (404) → dueño (409) → duplicado (201) → límite por teléfono (429) → lead nuevo (201). El límite por IP actúa antes que todo (middleware).

### Gestión (panel)

Grupo `/api/leads`, política `AsesorOAdministrador`. Sin token → 401; comprador → 403; asesor sin perfil → 403. El asesor solo ve y toca los leads de los que es responsable **ahora**; cualquier otro da **404** "Contacto no encontrado." (CA-P5-30, CA-P5-48). El administrador ve todos. Todas las rutas que modifican usan POST (el proyecto solo admite DELETE bajo `/api/inventario`, y no hay PUT ni PATCH aquí). No hay rutas para editar los datos del visitante (CA-P5-43), borrar leads (CA-P5-60) ni tocar consentimientos (CA-P5-16).

| Método | Ruta | Rol | Uso |
|---|---|---|---|
| GET | `/api/leads?estado=&responsable=&publicacionId=&vehiculoId=&desde=&hasta=&pagina=&tamano=` | asesor o admin | Lista, del más reciente al más antiguo |
| GET | `/api/leads/resumen` | asesor o admin | `{ nuevos, abiertos }` para "Contactos (N nuevos)" (D-48) |
| GET | `/api/leads/{id}` | asesor o admin | Detalle con historial; marca visto si lo abre el responsable |
| POST | `/api/leads/{id}/notas` | asesor o admin | `{ texto }` → 201 con el detalle |
| POST | `/api/leads/{id}/cerrar` | asesor o admin | `{ resultado, observacion? }` → 200 con el detalle |
| POST | `/api/leads/{id}/reabrir` | **solo admin** | `{ motivo }` → 200 con el detalle |
| POST | `/api/leads/{id}/reasignar` | **solo admin** | `{ asesorId?, administracion?, motivo }` → 200 con el detalle |

**Filtros de la lista** (400 "Revisa los filtros." con `errores`):

- `estado`: `ABIERTO` o `CERRADO` → "Estado no válido.".
- `responsable` (solo admin; el asesor lo tiene fijo y se ignora): `ADMINISTRACION` o un `perfiles_asesor.id` → "Responsable no válido.".
- `publicacionId`, `vehiculoId`: enteros > 0 → "Vehículo no válido.".
- `desde`, `hasta`: `yyyy-MM-dd`, día de Colombia (UTC-5), ambos incluidos → "Escribe una fecha válida (aaaa-mm-dd)."; `hasta` < `desde` → "La fecha final debe ser igual o posterior a la inicial." en `hasta`.
- `pagina` ≥ 1, `tamano` 1 a 100 (20 por defecto) → "Página o tamaño no válidos.".

**`LeadItem`** (lista):

```text
id, estado ("ABIERTO" | "CERRADO"), nombre, telefono, correo?, mensaje, creadoEn, nuevo (bool),
vehiculo { vehiculoId, publicacionId, titulo, slug, estadoVehiculo, estadoPublicacion },
responsable { tipo ("ASESOR" | "ADMINISTRACION"), asesorId?, nombre? },
resultadoCierre?, cerradoEn?
```

`estadoVehiculo` (ACTIVO, RESERVADO, INACTIVO, VENDIDO) y `estadoPublicacion` (PUBLICADA, OCULTA, ELIMINADA, con VENDIDA si el estado comercial es VENDIDO) son los actuales (CA-P5-49). `nuevo` es `!visto_por_responsable` solo si el que consulta es el responsable (el asesor, o un admin con lead de Administración); en los demás casos es falso.

**`LeadDetalle`** = `LeadItem` + `observacionCierre?`, `cerradoPor?` (nombre) y:

- `eventos`: `[{ tipo, fecha, actor { tipo ("USUARIO" | "SISTEMA"), nombre? }, automatico, texto?, resultado?, responsableAnterior?, responsableNuevo? }]` en orden cronológico (CA-P5-42). `responsableAnterior/Nuevo` tienen la forma de `responsable`.
- `consentimientos`: `[{ formulario, versionPolitica, aceptadoEn }]` **solo para el administrador**; para el asesor es `null` (CA-P5-16).

**Notas** (CA-P5-37, 38): texto de 5 a 500 → 400 en `texto` "La nota debe tener entre 5 y 500 caracteres."; lead CERRADO → 409 "Este contacto está cerrado.".

**Cerrar** (CA-P5-39, 40): `resultado` uno de `VENTA_CONCRETADA` (Se concretó la venta), `NO_INTERESADO` (No le interesó), `NO_RESPONDIO` (No respondió), `VEHICULO_NO_DISPONIBLE` (El vehículo ya no está disponible), `DUPLICADO_INVALIDO` (Contacto duplicado o inválido), `OTRO` (Otro).
- 400 en `resultado`: "Elige el resultado del cierre.".
- 400 en `observacion`: con OTRO, de 5 a 500 → "Escribe la observación (5 a 500 caracteres)."; con los demás, opcional hasta 500 → "La observación no puede tener más de 500 caracteres.".
- Lead ya CERRADO → 409 "Este contacto ya está cerrado.".

**Reabrir** (CA-P5-41): motivo de 5 a 500 (400 en `motivo`, "Escribe el motivo (5 a 500 caracteres)."). Lead ABIERTO → 409 "Este contacto ya está abierto.". Vuelve a ABIERTO con el mismo responsable; se borran resultado, observación, quién y cuándo se cerró (quedan en el historial). Asesor → 403.

**Reasignar** (CA-P5-44 a 48): `administracion: true` (sin `asesorId`) o `asesorId` de un asesor con perfil APROBADO, cuenta ACTIVA y rol ASESOR (con o sin plan, CA-P5-47).
- 400 en `asesorId`: "Elige un asesor aprobado." (falta, no existe, no aprobado, o vienen los dos).
- 400 en `motivo`: "Escribe el motivo (5 a 500 caracteres).".
- Lead CERRADO → 409 "Este contacto está cerrado."; mismo responsable → 409 "El contacto ya está asignado a ese responsable.".
- Efecto: cambia el responsable, `visto_por_responsable = 0`, `asignacion_manual = 1` si el destino es un asesor (0 si es Administración), evento `REASIGNADO` y auditoría.

### Auditoría

Entidad `Lead` (`entidad_id` = id del lead), sin nombre, teléfono, correo ni mensaje del visitante (CA-P5-59):

| Acción | Cuándo | `usuario_id` |
|---|---|---|
| `CREAR` | lead nuevo | NULL (SISTEMA) |
| `NOTA` | nota | quien la escribe |
| `CERRAR` | cierre | quien cierra |
| `REABRIR` | reapertura | administrador |
| `REASIGNAR` | manual, barrido o vehículo reasignado | administrador o NULL (barrido) |

Los contactos repetidos solo dejan evento en el historial y su consentimiento. No se escribe ningún dato del visitante en el registro técnico de la aplicación.

### Cambios en endpoints existentes

- `GET /api/catalogo/publicaciones/{slug}`: añade `whatsapp` (D-47).
- `POST /api/admin/usuarios/{id}/bloquear`: si el usuario es asesor, sus leads ABIERTO pasan a Administración en la misma transacción (D-46 B).
- `POST /api/admin/vehiculos/{id}/reasignar`: los leads ABIERTO del dueño anterior siguen al vehículo (D-34, D-46 B).
- `SesionTests.Inventario_de_endpoints`: añade `POST /api/contactos` a los anónimos y a los que modifican, las 4 POST de `/api/leads`, y comprueba que `/api/leads` tiene 7 rutas con `AsesorOAdministrador` (y `SoloAdministrador` en reabrir y reasignar) y que `/api/contactos` es una única ruta POST anónima con limitación de peticiones. `/api/catalogo` sigue con 5 GET y `/api/admin` con 31.

## Interfaz (para el frontend; fuera de este entregable)

- Formulario en la ficha (`DetalleVehiculo.tsx`), con asteriscos (D-31), casilla de autorización sin marcar con enlace a `#/privacidad`, campo trampa oculto `sitioWeb` (fuera de la vista y del orden de tabulación) y contador del mensaje.
- Botón "Escribir por WhatsApp" con el `whatsapp` del detalle más `?text=` con el título.
- "Mis contactos" (asesor) y "Contactos" (admin) con lista, detalle, notas, cierre y, para el admin, reabrir y reasignar. Menú "Contactos (N nuevos)" con `GET /api/leads/resumen`.

## Riesgos

- **Orden de despliegue.** Con el código de P5 y sin la migración 013 en la base, fallan el contacto, `/api/leads`, **bloquear un asesor** y **reasignar un vehículo** (los dos últimos consultan `leads`). La migración debe aplicarse antes de arrancar esta versión de la API contra `camion_ventas`.

- **IP detrás del proxy.** En desarrollo, Vite reenvía todo desde 127.0.0.1: todos los visitantes comparten el límite por IP. En producción hará falta `ForwardedHeaders` con los proxies de confianza; no se lee `X-Forwarded-For` sin esa configuración porque cualquiera podría falsearla.
- **Ventana diaria fija.** El límite diario por IP se reinicia cada 24 h desde la primera petición, no a medianoche.
- **Barrido bajo demanda.** Si nadie consulta leads, los de un plan vencido siguen asignados al asesor en la base hasta la siguiente consulta. Como toda lectura de `/api/leads` barre antes de leer, nadie llega a verlos con el asesor después del vencimiento; la fecha del evento es la de esa consulta.
- **La política sigue en borrador (D-44).** P5 no sale a internet hasta que exista una versión aprobada en `PoliticaDatos:Version`.

## Ambigüedades (decisión del implementador, a confirmar)

1. **Leads de un vehículo reasignado que no estaban con el dueño anterior** (en Administración o con otro asesor por decisión manual): se quedan donde están. D-46 dice "sus leads abiertos lo siguen"; se interpreta como los del dueño anterior, para no deshacer decisiones del administrador.
2. **CA-P5-47 frente a D-46 (B):** la reasignación manual a un asesor sin plan queda fuera del barrido por plan (`asignacion_manual`). Sin esa marca, el barrido la desharía en la siguiente consulta.
3. **WhatsApp con teléfono fijo:** `wa.me` solo sirve con celulares; un fijo válido usa el WhatsApp del portal.
4. **"Publicación a cargo del administrador":** dueño con rol ADMINISTRADOR.

## Fuera de P5

- Avisos "Vende tu camión" y "Seguro" (P5b), que reutilizan `consentimientos_datos` y `LimitePeticionesContacto`.
- Créditos y búsquedas (P6), reporte de leads (P8), aviso por correo o WhatsApp (P8, D-48).
