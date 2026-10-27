# Guía de autenticación (backend)

Explica cómo configurar y probar a mano las cuentas del portal: registro de comprador y de asesor, inicio de sesión, perfil, cierre de sesión, roles y los endpoints de administración de usuarios.

- Contratos oficiales de la API: `docs/criterios/P1-contrato-api.md` (comprador, login, sesión) y `docs/criterios/P2-contrato-api.md` (asesor, roles y administración).
- Código:
  - `backend/Camion.Api/Endpoints/AuthEndpoints.cs`: registro, login, perfil y logout.
  - `backend/Camion.Api/Endpoints/AdminUsuariosEndpoints.cs`: lista de usuarios y acciones del administrador.
  - `backend/Camion.Api/Auth/ValidacionAuth.cs`: validaciones y mensajes.
  - `backend/Camion.Api/Auth/Roles.cs` y `Auth/RespuestaAutorizacion.cs`: roles, políticas y respuesta 403.
  - `backend/Camion.Api/Auth/Auditoria.cs`: filas de auditoría en la base de datos.
  - `backend/Camion.Api/Comandos/PromoverAdmin.cs`: comando local para crear el primer administrador.
  - `backend/Camion.Api/Program.cs`: Identity, JWT, políticas y validación de sesión.
- Trazabilidad: HU-N03 Registro de comprador (D-8), HU-043 Aprobar usuario (solo asesores, D-3), HU-044 Rechazar usuario, HU-045 Activar acceso, HU-047 Bloquear usuario, HU-048 Consultar usuarios, HU-059 Iniciar sesión (RN-SE-001), HU-060 Cierre automático de sesión (RN-SE-002), HU-061 Registrar auditoría (RN-SE-003), HU-062 Proteger datos personales (RN-SE-004), RN-SE-006 Control de accesos. Decisiones D-3, D-5, D-10, D-12, D-13 y D-15.

Estado: todos los endpoints de esta guía están **IMPLEMENTADOS**. Los de P1 (comprador, login, perfil, logout) tienen pruebas automáticas de P1. Lo nuevo de P2 **no está PROBADO** todavía: sus pruebas se están escribiendo en `backend/Camion.Api.Tests/`.

Requisitos: SDK de .NET 8, XAMPP con MariaDB 10.4 encendido y el script `database/002_esquema_inicial_mariadb_10_4.sql` importado (ver `backend/GUIA-CONEXION-MARIADB.md`). Para aprobar, rechazar, reconsiderar o bloquear **asesores** contra la base real hace falta además la migración `database/migraciones/003_p2_estados_asesor.sql` (ver `docs/GUIA-ADMINISTRACION.md`, sección 4).

## 1. Ir a la carpeta del backend

```powershell
cd "C:\Users\alex0\OneDrive\Documentos\proyecto venta de camiones\CAMION\backend\Camion.Api"
```

## 2. Configurar la clave JWT (secreto local)

El backend firma los tokens con la clave `Jwt:Key`. Debe tener **al menos 32 bytes**; si es más corta, el backend se niega a arrancar. Genera una aleatoria con PowerShell y guárdala sin mostrarla en pantalla:

```powershell
$key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set "Jwt:Key" "$key"
Remove-Variable key
```

También debe estar configurada `ConnectionStrings:CamionDatabase` (paso de la guía de MariaDB). Si falta cualquiera de las dos, el backend se detiene al arrancar con un mensaje que dice qué clave falta.

## 3. Arrancar

```powershell
dotnet restore
dotnet run
```

El backend queda en http://localhost:5080.

Al arrancar, el backend comprueba que existan los tres roles (`COMPRADOR`, `ASESOR`, `ADMINISTRADOR`) y crea los que falten. Si en ese momento la base de datos no responde, arranca igual y lo anota en el log; los registros vuelven a asegurar su rol cuando se usan.

## 4. Roles y permisos (RN-SE-006)

Hay tres roles:

| Rol | Cómo se obtiene | Puede entrar |
|---|---|---|
| `COMPRADOR` | Registro de comprador | Al instante |
| `ASESOR` | Registro de asesor | Solo cuando el administrador lo aprueba |
| `ADMINISTRADOR` | Comando local `promover-admin` (D-13); nunca desde la web | Sí |

El rol **nunca** se elige en un formulario público: cada registro asigna su rol fijo.

Políticas de autorización (`Program.cs`):

- `SoloAdministrador`: exige el rol ADMINISTRADOR. Protege todo `/api/admin/usuarios…`.
- `SoloAsesor`: exige el rol ASESOR. Está definida, pero ningún endpoint la usa todavía.
- `Autenticado`: cualquier usuario con sesión. Protege `/api/auth/me` y `/api/auth/logout`.

Respuestas cuando falta permiso:

- Sin token, token inválido o caducado, cuenta no activa o sesión cerrada: **401** (sin cuerpo).
- Token válido pero sin el rol que pide la política: **403** `{ "error": "No tienes permiso para realizar esta acción." }`.

## 5. Estados de una cuenta

La columna `EstadoCuenta` de `AspNetUsers` decide si se puede entrar.

| Estado | Quién | Puede entrar | En el panel, un asesor se muestra como |
|---|---|---|---|
| `ACTIVA` | comprador, asesor aprobado, administrador | sí | Aprobado |
| `PENDIENTE` | asesor recién registrado | no | Pendiente |
| `RECHAZADA` | asesor rechazado | no | Rechazado |
| `BLOQUEADA` | comprador o asesor bloqueado | no | Bloqueado |

Para los asesores, la tabla `perfiles_asesor` guarda además su estado con los nombres de D-10: `PENDIENTE`, `APROBADO`, `RECHAZADO`, `BLOQUEADO`. Se cambia en la misma operación que `EstadoCuenta`.

## 6. Endpoints de cuenta (`/api/auth`)

Todas las respuestas de error con cuerpo tienen esta forma (JSON):

```json
{ "error": "Mensaje general en español.",
  "errores": { "password": ["Debe tener al menos una letra mayúscula."] } }
```

- `errores` aparece en las respuestas 400 (un mensaje por cada campo que falla) y en el 409 de documento repetido.
- `estado` aparece solo en el 403 del login de una cuenta no activa.
- Nunca se devuelve la contraseña ni detalles internos.

### POST /api/auth/registro/comprador (sin iniciar sesión)

Crea una cuenta de comprador. La cuenta queda **activa al instante** (no necesita aprobación) y siempre recibe el rol `COMPRADOR`.

Datos que se envían: `nombreCompleto`, `email`, `telefono`, `password`.

| Campo | Regla |
|---|---|
| nombreCompleto | obligatorio, 2 a 150 caracteres |
| email | obligatorio, formato de correo, máximo 254 caracteres; se guarda en minúsculas |
| telefono | obligatorio, 7 a 25 caracteres; solo números, espacios, `+`, `-`, `(`, `)`; al menos 7 dígitos |
| password | 8 a 128 caracteres, con al menos una mayúscula, una minúscula y un número (no se exige símbolo) |

Respuestas:

- **201**: `{ "usuarioId", "mensaje": "Cuenta creada. Ya puedes iniciar sesión." }`
- **400**: `"Revisa los datos marcados."` con la lista `errores` por campo.
- **409**: `"Este correo ya está registrado."` (también si dos personas se registran a la vez con el mismo correo).

### POST /api/auth/registro/asesor (sin iniciar sesión) — P2

Crea una solicitud de asesor. La cuenta queda **PENDIENTE**: no puede iniciar sesión hasta que el administrador la apruebe. Siempre recibe el rol `ASESOR`.

Datos que se envían: los cuatro del comprador más `tipoDocumento`, `numeroDocumento` y `nombreComercial`.

| Campo | Regla |
|---|---|
| nombreCompleto, email, telefono, password | las mismas reglas que en el registro de comprador |
| tipoDocumento | obligatorio: `CC` (cédula de ciudadanía), `CE` (cédula de extranjería), `NIT` o `PAS` (pasaporte). Esta lista es **PROPUESTA** |
| numeroDocumento | obligatorio, 4 a 30 caracteres; solo letras, números y guiones. Se guarda en mayúsculas |
| nombreComercial | opcional, hasta 150 caracteres |

Qué hace, todo en una sola operación: crea el usuario con estado `PENDIENTE` y rol `ASESOR`, crea su fila en `perfiles_asesor` con estado `PENDIENTE` y deja el evento `REGISTRO_ASESOR` en `auditoria_eventos`.

Respuestas:

- **201**: `{ "usuarioId", "mensaje": "Solicitud enviada. Podrás iniciar sesión cuando el administrador apruebe tu cuenta." }`
- **400**: `"Revisa los datos marcados."` con `errores` por campo.
- **409**: `"Este correo ya está registrado."`
- **409**: `"Ya existe un asesor con ese documento."`, con `errores.numeroDocumento`. No puede haber dos asesores con el mismo tipo y número de documento.

Un comprador que quiera ser asesor debe, por ahora, registrarse como asesor con **otro correo** (D-15).

### POST /api/auth/login (sin iniciar sesión)

Datos que se envían: `email`, `password`.

Orden de las comprobaciones (P2): primero los datos, después el usuario, el bloqueo por intentos y la contraseña, y **al final el estado de la cuenta**. Así el estado solo se le revela a quien conoce la contraseña.

- **200**: `{ "accessToken", "tokenType": "Bearer", "expiraEnUtc", "usuarioId", "nombreCompleto", "email", "roles" }`. El token dura **2 horas**. Solo en este caso se actualiza la fecha de último acceso.
- **400**: `"Escribe tu correo y tu contraseña."` si falta alguno.
- **401**: `"Correo o contraseña incorrectos."` El mismo mensaje si el correo no existe o la contraseña está mal, para no revelar qué falló.
- **423**: `"Cuenta bloqueada temporalmente por varios intentos fallidos. Intenta de nuevo en 15 minutos."` Tras 5 intentos fallidos la cuenta queda bloqueada 15 minutos.
- **403** (nuevo en P2; antes era 401): la contraseña es correcta pero la cuenta no está activa. La respuesta trae el campo `estado`:

| estado | Mensaje |
|---|---|
| `PENDIENTE` | "Tu cuenta de asesor está pendiente de aprobación. Te avisaremos cuando el administrador la apruebe." |
| `RECHAZADA` | "Tu solicitud de asesor no fue aprobada. Comunícate con el administrador." |
| `BLOQUEADA` | "Tu cuenta está bloqueada. Comunícate con el administrador." |

No confundir los dos bloqueos: el **423** es temporal (15 minutos, por contraseñas mal escritas) y se quita solo; el **403 BLOQUEADA** lo pone el administrador y solo él lo quita.

Si la cuenta tuviera un estado distinto de los cuatro conocidos, se responde 401 como si las credenciales fueran incorrectas.

### GET /api/auth/me (requiere token)

Devuelve el perfil de quien inició sesión. Se envía la cabecera `Authorization: Bearer <TOKEN>`.

- **200**: `{ "usuarioId", "nombreCompleto", "email", "phoneNumber", "roles", "estado" }` (`estado` es nuevo en P2).
- **401**: sin token, token inválido o caducado, cuenta no activa, o sesión cerrada con `/logout`.

### POST /api/auth/logout (requiere token)

Cierra la sesión. Por dentro aumenta en uno el número de versión de sesión del usuario (`SessionVersion`), así que **todos** los tokens emitidos antes para ese usuario dejan de valer, en cualquier dispositivo.

- **204**: sin cuerpo.
- **401**: si el token ya no era válido.

### Validación de sesión en toda la API

La comprobación "la cuenta está ACTIVA y la sesión no se cerró" se hace una sola vez, al validar el token (evento `OnTokenValidated` en `Program.cs`). Por eso vale para **todos** los endpoints protegidos. Consecuencia práctica: si el administrador rechaza o bloquea a alguien, o lo promueve a administrador, sus tokens anteriores dejan de valer en su siguiente petición.

## 7. Endpoints de administración (`/api/admin/usuarios`) — P2

Todos exigen la política `SoloAdministrador`. Sin token: 401. Con token de comprador o asesor: 403 "No tienes permiso para realizar esta acción."

### GET /api/admin/usuarios

Lista de usuarios (HU-048). Parámetros opcionales en la dirección:

| Parámetro | Valores | Por defecto |
|---|---|---|
| `estado` | `ACTIVA`, `PENDIENTE`, `RECHAZADA`, `BLOQUEADA` | todos |
| `rol` | `COMPRADOR`, `ASESOR`, `ADMINISTRADOR` | todos |
| `buscar` | texto que se busca en el nombre o el correo, hasta 100 caracteres | sin filtro |
| `pagina` | número desde 1 | 1 |
| `tamano` | de 1 a 100 | 20 |

- Orden: primero los `PENDIENTE`, después del más reciente al más antiguo.
- **200**: `{ "items": [...], "total", "pagina", "tamano" }`. Cada ítem trae `usuarioId`, `nombreCompleto`, `email`, `telefono`, `roles`, `estado`, `creadoEn`, `ultimoAccesoEn` y `asesor` (tipo y número de documento, nombre comercial, estado de autorización y fecha de autorización; `null` si no es asesor). Nunca trae contraseñas cifradas ni otros datos internos.
- **400**: `"Revisa los filtros."` con `errores` por parámetro: "Estado no válido.", "Rol no válido.", "La búsqueda no puede tener más de 100 caracteres." o "Página o tamaño no válidos."

### POST /api/admin/usuarios/{id}/{acción}

Las cinco acciones posibles y desde qué estado se pueden usar:

| Acción | Desde | Hacia | Para quién | Motivo | Cierra sus sesiones |
|---|---|---|---|---|---|
| `aprobar` | PENDIENTE | ACTIVA (asesor Aprobado) | solo asesores | opcional | no |
| `rechazar` | PENDIENTE | RECHAZADA | solo asesores | **obligatorio** | sí |
| `reconsiderar` (D-15) | RECHAZADA | ACTIVA (asesor Aprobado) | solo asesores | **obligatorio** | no |
| `bloquear` | ACTIVA | BLOQUEADA | compradores y asesores | **obligatorio** | sí |
| `desbloquear` | BLOQUEADA | ACTIVA (asesor Aprobado) | compradores y asesores | opcional | no |

Petición: `{ "motivo": "..." }`. Cuando es obligatorio, de 5 a 500 caracteres (límite **PROPUESTO**); cuando es opcional, hasta 500.

Respuestas:

- **200**: el usuario actualizado, con la misma forma que un ítem de la lista.
- **400**: `"Revisa los datos marcados."` con `errores.motivo`: "Escribe el motivo (5 a 500 caracteres)." o "El motivo no puede tener más de 500 caracteres."
- **403**: `"No se puede modificar una cuenta de administrador."` (la propia cuenta o la de otro administrador).
- **404**: `"Usuario no encontrado."`
- **409**: la acción no vale para el estado actual. Ejemplo: "No se puede aprobar una cuenta en estado activa." También si se intenta aprobar, rechazar o reconsiderar a alguien que no es asesor.
- **500**: `"No se pudo completar la operación. Intenta de nuevo en unos minutos."` Ocurre, por ejemplo, al actuar sobre un asesor en una base sin la migración 003.

Qué guarda cada acción, todo en una sola operación (si algo falla, no se guarda nada):

1. El nuevo `EstadoCuenta` y, si es asesor, el nuevo estado en `perfiles_asesor`. Al aprobar o reconsiderar también quién lo autorizó y cuándo.
2. Si la acción cierra sesiones, aumenta `SessionVersion`.
3. Si es asesor, una fila en `historial_autorizaciones` (estado anterior y nuevo, administrador y motivo).
4. Una fila en `auditoria_eventos` (acción `APROBAR`, `RECHAZAR`, `RECONSIDERAR`, `BLOQUEAR` o `DESBLOQUEAR`, estado anterior, estado nuevo y motivo).

Si dos cambios tocan la misma cuenta a la vez, el backend vuelve a leerla y reintenta hasta 3 veces.

## 8. Probar a mano con PowerShell

Usa una segunda terminal con el backend encendido. Los datos son **de prueba** (correos de `example.com`, contraseña inventada, documento inventado); no uses datos ni contraseñas reales.

```powershell
$base = "http://localhost:5080/api/auth"

# Registro de comprador
$registro = @{ nombreCompleto="Comprador Prueba"; email="comprador.prueba@example.com"; telefono="300 123 4567"; password="SoloPrueba123" } | ConvertTo-Json
Invoke-RestMethod -Uri "$base/registro/comprador" -Method Post -ContentType "application/json" -Body $registro

# Login (guarda la respuesta sin imprimir el token)
$login = @{ email="comprador.prueba@example.com"; password="SoloPrueba123" } | ConvertTo-Json
$resultado = Invoke-RestMethod -Uri "$base/login" -Method Post -ContentType "application/json" -Body $login
$resultado | Select-Object usuarioId, nombreCompleto, email, roles, expiraEnUtc
$cabecera = @{ Authorization = "Bearer $($resultado.accessToken)" }

# Mi perfil
Invoke-RestMethod -Uri "$base/me" -Headers $cabecera

# Un comprador no puede usar la administración: debe responder 403
Invoke-RestMethod -Uri "http://localhost:5080/api/admin/usuarios" -Headers $cabecera

# Cerrar sesión
Invoke-RestMethod -Uri "$base/logout" -Method Post -Headers $cabecera

# Después del logout, /me debe responder 401 (PowerShell lo muestra como error)
Invoke-RestMethod -Uri "$base/me" -Headers $cabecera
```

Registro de asesor y login pendiente:

```powershell
$asesor = @{ nombreCompleto="Asesor Prueba"; email="asesor.prueba@example.com"; telefono="300 765 4321"; password="SoloPrueba123"; tipoDocumento="CC"; numeroDocumento="0000-PRUEBA"; nombreComercial="Camiones de Prueba" } | ConvertTo-Json
Invoke-RestMethod -Uri "$base/registro/asesor" -Method Post -ContentType "application/json" -Body $asesor

# Debe responder 403 con estado PENDIENTE hasta que el administrador lo apruebe
$loginAsesor = @{ email="asesor.prueba@example.com"; password="SoloPrueba123" } | ConvertTo-Json
Invoke-RestMethod -Uri "$base/login" -Method Post -ContentType "application/json" -Body $loginAsesor
```

Para probar los endpoints de administración necesitas una cuenta de administrador: sigue `docs/GUIA-ADMINISTRACION.md`.

Nota: `Invoke-RestMethod` muestra las respuestas 400, 401, 403, 404, 409 y 423 como un error rojo; es lo esperado en esos casos.

## 9. Registro de eventos (auditoría)

Hay dos lugares:

- **Log del servidor** (consola), categoría `Camion.Api.Auditoria`: registro de comprador y de asesor, login exitoso, login fallido (incluido el de una cuenta no activa, con su estado), bloqueo por intentos, logout y cada acción del administrador. Nunca escribe contraseñas ni tokens, y el correo de un intento fallido aparece enmascarado (por ejemplo `c***@example.com`).
- **Base de datos** (nuevo en P2), tabla `auditoria_eventos`: registro de asesor (`REGISTRO_ASESOR`), las cinco acciones del administrador y la promoción a administrador (`PROMOVER_ADMIN`). Los cambios de estado de un asesor quedan además en `historial_autorizaciones`. El inicio y el cierre de sesión **todavía no** se guardan en la base. Detalle en `docs/GUIA-ADMINISTRACION.md`, sección 3.

## 10. Qué hace el frontend con la sesión

- Guarda el token y su caducidad en el navegador (`localStorage`, clave `camion.sesion`), así la sesión se mantiene al recargar. IMPLEMENTADO.
- Si el backend responde 401, borra la sesión y muestra "Tu sesión terminó. Inicia sesión de nuevo." IMPLEMENTADO.
- El botón "Cerrar sesión" llama a `/logout` y borra la sesión local aunque la llamada falle. IMPLEMENTADO.
- Tras **30 minutos** sin actividad cierra la sesión solo en ese navegador (sin llamar a `/logout`). IMPLEMENTADO (D-12).
- En el login, un 403 por estado se muestra con el mensaje del servidor en un aviso propio: PENDIENTE como información, RECHAZADA y BLOQUEADA como advertencia. IMPLEMENTADO (P2).
- El botón "Administrar usuarios" solo aparece si el perfil trae el rol ADMINISTRADOR. Ocultarlo es solo comodidad: la API vuelve a comprobar el rol en cada petición. IMPLEMENTADO (P2).

## 11. Avisos importantes

- El script SQL ya creó las tablas de Identity. **No** ejecutes `dotnet ef database update` ni crees una migración inicial que las vuelva a crear. Los cambios de esquema van en `database/migraciones/`, con respaldo previo y sección para revertir.
- **Pendiente** (no existe todavía): confirmación de correo, cambio de contraseña, limitación de peticiones por IP, "Solicitar ser asesor" desde una cuenta de comprador, crear administradores desde la web y guardar el login y el logout en la base de datos.
- **BLOQUEADO**: recuperar contraseña (HU-063) y avisar por correo al asesor aprobado, porque necesitan enviar correos y no hay servicio de correo aprobado (D-5).
