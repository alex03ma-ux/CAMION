# P1 — Contrato de la API de autenticación

Fijado por el coordinador antes de programar. Backend y frontend lo siguen tal cual; un cambio aquí se registra en `docs/coordinacion/BITACORA.md`.
Trazabilidad (numeración oficial de 87 HU, D-1): HU-N03 Registro de comprador (D-8), HU-059 Iniciar sesión (RN-SE-001), HU-060 Cierre automático de sesión (RN-SE-002), HU-061 Registrar auditoría (RN-SE-003), HU-062 Proteger datos personales (RN-SE-004), RN-SE-006 Control de accesos.

## Formato común de error

Todas las respuestas de error con cuerpo usan JSON:

```json
{ "error": "Mensaje en español para mostrar al usuario.",
  "errores": { "email": ["El correo no es válido."], "password": ["Debe tener al menos una mayúscula."] } }
```

- `error`: siempre presente (texto general).
- `errores`: solo en 400; claves en camelCase iguales a los campos del DTO (`nombreCompleto`, `email`, `telefono`, `password`).
- Nunca se devuelve 500 por datos del usuario. Nunca se devuelve la contraseña, el hash ni detalles internos.
- Los 401 que genera el middleware JWT (sin token, token inválido o caducado) pueden venir sin cuerpo; el frontend los trata por código.

## Reglas de validación (registro)

| Campo | Regla | Mensaje |
|---|---|---|
| nombreCompleto | obligatorio; 2 a 150 caracteres tras recortar espacios | "Escribe tu nombre completo (2 a 150 caracteres)." |
| email | obligatorio; formato de correo válido; máximo 254 caracteres | "Escribe un correo válido." / "El correo no puede tener más de 254 caracteres." |
| telefono | obligatorio; 7 a 25 caracteres; solo dígitos, espacios, `+`, `-`, `(`, `)`; al menos 7 dígitos | "Escribe un teléfono válido (solo números, espacios, +, - o paréntesis)." |
| password | obligatoria; mínimo 8 caracteres; al menos una mayúscula, una minúscula y un dígito; máximo 128 | Un mensaje por regla incumplida: "Debe tener al menos 8 caracteres.", "Debe tener al menos una letra mayúscula.", "Debe tener al menos una letra minúscula.", "Debe tener al menos un número.", "No puede tener más de 128 caracteres." |

- Texto general (`error`) de un 400 de registro: "Revisa los datos marcados."
- Mayúscula/minúscula/dígito se evalúan solo en ASCII (igual que Identity): "Ñ" no cuenta como mayúscula. El frontend usa el mismo criterio.

El correo se guarda recortado y en minúsculas. Las reglas de contraseña se muestran en el formulario antes de enviar (con indicación de cuáles se cumplen).

## Endpoints

### POST /api/auth/registro/comprador — anónimo (HU-N03, D-3, D-5)
Petición: `{ "nombreCompleto", "email", "telefono", "password" }`. No acepta rol: siempre se asigna `COMPRADOR`; la cuenta queda `ACTIVA` (acceso inmediato).
- 201 `{ "usuarioId", "mensaje": "Cuenta creada. Ya puedes iniciar sesión." }`
- 400 validación (formato común, con `errores`)
- 409 `{ "error": "Este correo ya está registrado." }` — también cuando dos registros simultáneos chocan en el índice único (se captura la excepción de base de datos; nunca 500).

### POST /api/auth/login — anónimo (HU-059, RN-SE-001)
Petición: `{ "email", "password" }`.
- 200 `{ "accessToken", "tokenType": "Bearer", "expiraEnUtc", "usuarioId", "nombreCompleto", "email", "roles": [] }`
- 400 `{ "error": "Escribe tu correo y tu contraseña." }` si falta alguno
- 401 `{ "error": "Correo o contraseña incorrectos." }` — mismo mensaje si el correo no existe, la contraseña es incorrecta o la cuenta no está ACTIVA (no revela qué falló, HU-062)
- 423 `{ "error": "Cuenta bloqueada temporalmente por varios intentos fallidos. Intenta de nuevo en 15 minutos." }` desde el 5.º intento fallido inclusive (bloqueo de Identity ya configurado). Se acepta que el 423 y el 409 del registro indiquen que el correo existe: es el comportamiento habitual y el bloqueo frena los ataques de fuerza bruta.

### GET /api/auth/me — requiere token (HU-059)
- 200 `{ "usuarioId", "nombreCompleto", "email", "phoneNumber", "roles": [] }`
- 401 sin token, token inválido/caducado, cuenta no ACTIVA o `session_version` distinto del actual

### POST /api/auth/logout — requiere token (HU-060, RN-SE-002) · NUEVO
- 204 sin cuerpo. Incrementa `SessionVersion` del usuario: **todos** los tokens emitidos antes para ese usuario dejan de valer (en todos los dispositivos).
- 401 si el token ya no es válido.

## Validación de sesión en toda la API (RN-SE-006)
La comprobación de `session_version` y de `EstadoCuenta == ACTIVA` se hace en el evento `OnTokenValidated` del JwtBearer, de modo que aplica a **todos** los endpoints con `RequireAuthorization()` presentes y futuros, no solo a `/me`.

## Sesión en el frontend (HU-059, HU-060)
- El token y su `expiraEnUtc` se guardan en `localStorage` (clave `camion.sesion`). Al cargar la app: si caducó, se borra; si no, se llama a `/api/auth/me`; si responde 401 se borra y se muestra el login.
- Cualquier 401 de una llamada autenticada borra la sesión y muestra "Tu sesión terminó. Inicia sesión de nuevo."
- "Cerrar sesión" llama a `POST /api/auth/logout` y borra la sesión local aunque la llamada falle.
- Cierre automático por inactividad: tras **30 minutos** sin actividad (clics/teclas/toques) se borra la sesión **solo en ese navegador** (no llama a `/logout`, para no cerrar la sesión en otros equipos). La hora de la última actividad se guarda en `localStorage` (`camion.ultimaActividad`); al abrir o recargar la app, si pasaron más de 30 minutos, la sesión se borra aunque el token siga vigente. Valor de 30 min PROPUESTO, pendiente de confirmación de la responsable. La caducidad del token sigue siendo 2 horas.
- "Cerrar sesión" (botón) sí llama a `/logout` y, por tanto, cierra la sesión en todos los dispositivos de ese usuario (PROPUESTO).

## Auditoría y datos personales (HU-061, HU-062)
- Se registran en el log del servidor (ILogger, sin tabla nueva en P1): registro exitoso, login exitoso, login fallido, bloqueo y logout, con id de usuario cuando existe. Nunca se registran contraseñas ni tokens; el correo de un intento fallido no se registra completo.
- La persistencia en la tabla `auditoria_eventos` queda para un ítem posterior (no está mapeada; ver `ESTADO-REAL.md`).

## Fuera de P1
- HU-063 Recuperar contraseña: requiere enviar correos y no hay servicio de correo aprobado (D-5) → BLOQUEADO.
- Limitación de peticiones por IP: se valora en P2.
