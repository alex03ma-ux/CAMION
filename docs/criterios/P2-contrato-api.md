# P2 — Contrato de la API: asesores y administración de usuarios

Fijado por el coordinador antes de programar. Backend y frontend lo siguen tal cual; un cambio aquí se registra en `docs/coordinacion/BITACORA.md`.
Se mantiene todo lo del contrato P1 (`P1-contrato-api.md`): formato común de error `{ error, errores? }`, validaciones de nombre, correo, teléfono y contraseña, logout y sesión.

Trazabilidad (numeración oficial de 87 HU, D-1):
- HU-043 Aprobar usuario (RN-US-001, aplicada solo a asesores por D-3).
- HU-044 Rechazar usuario (RN-US-002).
- HU-045 Activar acceso (RN-US-003).
- HU-046 Enviar credenciales (RN-US-004): NO APLICA. Por D-5 no se envían credenciales.
- HU-047 Bloquear usuario (RN-US-005).
- HU-048 Consultar usuarios.
- HU-061 Registrar auditoría (RN-SE-003).
- RN-SE-006 Control de accesos.
- Decisiones: D-3, D-5, D-7, D-10 y D-12.

## Estados

`AspNetUsers.EstadoCuenta` es la única fuente que decide si se puede entrar. Esta columna no tiene restricción CHECK, así que no requiere cambio de esquema.

| EstadoCuenta | Quién | Puede iniciar sesión | Se muestra como (asesor) |
|---|---|---|---|
| `ACTIVA` | comprador (desde el registro), asesor aprobado, administrador | sí | Aprobado |
| `PENDIENTE` | asesor recién registrado | no | Pendiente |
| `RECHAZADA` | asesor rechazado | no | Rechazado |
| `BLOQUEADA` | cualquier usuario no administrador bloqueado | no | Bloqueado |

- Para los asesores, `perfiles_asesor.estado_autorizacion` refleja el estado con los nombres de D-10: `PENDIENTE` / `APROBADO` / `RECHAZADO` / `BLOQUEADO`. Se actualiza en la misma transacción que `EstadoCuenta`.
- En la base real, esos valores exigen la migración `database/migraciones/003_p2_estados_asesor.sql`. La migración no se aplica sin aprobación de la responsable (ver DECISIONES).

### Transiciones permitidas

Cualquier otra transición devuelve 409.

| Acción | Desde | Hacia | Solo para |
|---|---|---|---|
| aprobar | PENDIENTE | ACTIVA | asesores |
| rechazar | PENDIENTE | RECHAZADA | asesores |
| bloquear | ACTIVA | BLOQUEADA | comprador o asesor (nunca un administrador) |
| desbloquear | BLOQUEADA | ACTIVA | comprador o asesor |

- Rechazar y bloquear incrementan `SessionVersion`: la persona pierde la sesión al momento en todos los equipos.
- Aprobar y desbloquear no tocan `SessionVersion`.
- Un administrador nunca puede actuar sobre su propia cuenta ni sobre la de otro administrador. Responde 403 `{ "error": "No se puede modificar una cuenta de administrador." }`.

## Roles y permisos (RN-SE-006)

Hay tres roles: `COMPRADOR`, `ASESOR` y `ADMINISTRADOR`.

Políticas de autorización con nombre:
- `SoloAdministrador`: rol ADMINISTRADOR.
- `SoloAsesor`: rol ASESOR. Se define ya, aunque P2 todavía no la usa.
- `Autenticado`: cualquier rol.

Respuestas de error:
- Sin token, o con un token inválido o de una sesión cerrada: **401**.
- Con token válido pero sin el rol requerido: **403** `{ "error": "No tienes permiso para realizar esta acción." }`. El 403 del middleware se escribe con el formato común.
- El rol nunca se elige en un registro público. El registro de comprador asigna COMPRADOR; el de asesor asigna ASESOR.

## Endpoints

### POST /api/auth/registro/asesor — anónimo (D-3, D-5)

Petición:

```json
{ "nombreCompleto", "email", "telefono", "password",
  "tipoDocumento", "numeroDocumento", "nombreComercial" }
```

Validaciones:
- `nombreCompleto`, `email`, `telefono` y `password`: iguales que en P1.
- `tipoDocumento`: obligatorio, uno de `CC`, `CE`, `NIT`, `PAS` (PROPUESTO). Mensaje: "Elige un tipo de documento válido."
- `numeroDocumento`: obligatorio, de 4 a 30 caracteres tras recortar; solo letras, dígitos y guiones. Mensaje: "Escribe un número de documento válido (4 a 30 letras, números o guiones)."
- `nombreComercial`: opcional, hasta 150 caracteres. Mensaje: "El nombre comercial no puede tener más de 150 caracteres."

Efecto:
- Crea el usuario con `EstadoCuenta = PENDIENTE` y el rol `ASESOR`.
- Crea `perfiles_asesor` con `estado_autorizacion = PENDIENTE`.
- Registra el evento de auditoría `REGISTRO_ASESOR`.
- Todo ocurre en una transacción.

Respuestas:
- 201 `{ "usuarioId", "mensaje": "Solicitud enviada. Podrás iniciar sesión cuando el administrador apruebe tu cuenta." }`
- 400: error de validación, con el formato común y `errores`.
- 409 `{ "error": "Este correo ya está registrado." }`. Incluye los registros simultáneos.
- 409 `{ "error": "Ya existe un asesor con ese documento.", "errores": { "numeroDocumento": ["Ya existe un asesor con ese documento."] } }`. El índice único es (tipo, número).

### POST /api/auth/login — cambios respecto a P1

El orden de las comprobaciones cambia: **primero la contraseña, después el estado**. Así el estado solo se revela a quien conoce la contraseña.

Respuestas:
- Correo inexistente o contraseña incorrecta: 401 `{ "error": "Correo o contraseña incorrectos." }`, sin cambios.
- Cuenta bloqueada por intentos fallidos: 423, sin cambios.
- Contraseña correcta y estado distinto de ACTIVA: 403 con el campo `estado`:
  - PENDIENTE: `{ "error": "Tu cuenta de asesor está pendiente de aprobación. Te avisaremos cuando el administrador la apruebe.", "estado": "PENDIENTE" }`
  - RECHAZADA: `{ "error": "Tu solicitud de asesor no fue aprobada. Comunícate con el administrador.", "estado": "RECHAZADA" }`
  - BLOQUEADA: `{ "error": "Tu cuenta está bloqueada. Comunícate con el administrador.", "estado": "BLOQUEADA" }`

`ErrorResponse` admite el campo opcional `estado`, que solo se serializa si no es nulo.

### GET /api/auth/me

Sin cambios. La respuesta añade `"estado"` (el valor de `EstadoCuenta`).

### GET /api/admin/usuarios — SoloAdministrador (HU-048)

Parámetros de consulta, todos opcionales:
- `estado`: ACTIVA / PENDIENTE / RECHAZADA / BLOQUEADA.
- `rol`: COMPRADOR / ASESOR / ADMINISTRADOR.
- `buscar`: texto que se busca en el nombre o el correo, hasta 100 caracteres.
- `pagina`: por defecto 1.
- `tamano`: por defecto 20, máximo 100.

Un valor inválido devuelve 400 con `errores`.

Respuesta 200:

```json
{ "items": [ { "usuarioId", "nombreCompleto", "email", "telefono", "roles": [], "estado",
               "creadoEn", "ultimoAccesoEn",
               "asesor": { "tipoDocumento", "numeroDocumento", "nombreComercial",
                           "estadoAutorizacion", "autorizadoEn" } | null } ],
  "total", "pagina", "tamano" }
```

- Orden: primero los PENDIENTE, después por `creadoEn` descendente.
- La respuesta nunca incluye hash, SecurityStamp ni otros campos internos de Identity.

### POST /api/admin/usuarios/{id}/aprobar | rechazar | bloquear | desbloquear — SoloAdministrador

Corresponden a HU-043, HU-044/HU-045 y HU-047.

Petición: `{ "motivo": "..." }`.
- Para rechazar y bloquear, el motivo es obligatorio, de 5 a 500 caracteres (PROPUESTO). Mensaje: "Escribe el motivo (5 a 500 caracteres)."
- Para aprobar y desbloquear es opcional, hasta 500 caracteres.

Respuestas:
- 200: el usuario actualizado, con la misma forma que un ítem de la lista.
- 400: motivo inválido.
- 403: cuenta de administrador o la propia cuenta.
- 404 `{ "error": "Usuario no encontrado." }`
- 409 `{ "error": "No se puede <acción> una cuenta en estado <Estado>." }`. Ejemplo: "No se puede aprobar una cuenta en estado Activa."

Cada acción ejecuta en **una sola transacción**:
1. Cambia `EstadoCuenta` y, si es asesor, `perfiles_asesor`. Al aprobar también escribe `autorizado_por` y `autorizado_en`.
2. Incrementa `SessionVersion` si corresponde.
3. Si es asesor, escribe una fila en `historial_autorizaciones` (estado anterior y nuevo con los nombres de D-10, administrador y motivo).
4. Escribe una fila en `auditoria_eventos`.

## Auditoría (HU-061, RN-SE-003)

Se mapea la tabla existente `auditoria_eventos`; no hay cambio de esquema.

Contenido de cada fila:
- `usuario_id`: quién actúa (el administrador, o el propio usuario en el registro).
- `entidad`: `Usuario`.
- `entidad_id`: el id del afectado.
- `accion`: `REGISTRO_ASESOR` / `APROBAR` / `RECHAZAR` / `BLOQUEAR` / `DESBLOQUEAR`.
- `datos_anteriores`: `{ "estado": "..." }`.
- `datos_nuevos`: `{ "estado": "...", "motivo": "..." }`.
- `creado_en`: la fecha y hora (UTC).

Las filas nunca guardan contraseñas, tokens ni el documento completo. Se mantiene además el log de P1.

También se mapea `historial_autorizaciones` para los asesores, igual que en el script.

## Primer administrador

Cómo se crea es una DECISIÓN pendiente (D-13), así que **no se implementa en P2**. Las pruebas crean el administrador directamente en la base en memoria.

## Interfaz (mínima)

- **Registro de asesor:**
  - Se elige entre "Soy comprador" y "Soy asesor".
  - El formulario de asesor usa los campos del contrato y las reglas de contraseña visibles de P1.
  - Tras el 201, muestra el mensaje de "Solicitud enviada".
- **Login:**
  - Los 403 muestran el mensaje del servidor en un aviso propio, distinto del de credenciales incorrectas.
  - PENDIENTE se muestra como información, no como error.
- **Panel de administración:**
  - Solo se muestra si `roles` incluye ADMINISTRADOR, y siempre lo protege la API.
  - Contiene la lista de usuarios, con filtros por estado y rol y búsqueda. Por defecto muestra los PENDIENTE.
  - Cada fila tiene los botones Aprobar, Rechazar, Bloquear y Desbloquear, solo para las transiciones válidas.
  - Rechazar y bloquear piden el motivo en un cuadro de confirmación.
  - Después de cada acción, la lista se actualiza.
  - Un 403 o un 401 se tratan como en P1.
- **Usuarios no administradores:** no ven el panel ni el enlace al panel.

## Fuera de P2

- Creación del primer administrador (D-13).
- Aplicar la migración 003 a `camion_ventas` (D-14).
- Gestión de planes y roles adicionales.
- Notificación por correo de la aprobación (D-5: requiere integración de correo).
- Limitación de peticiones por IP.
