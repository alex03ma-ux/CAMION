# Guía de autenticación de compradores (backend)

Explica cómo configurar y probar a mano la cuenta de comprador: registro, inicio de sesión, perfil y cierre de sesión.

- Contrato oficial de la API: `docs/criterios/P1-contrato-api.md`.
- Código: `backend/Camion.Api/Endpoints/AuthEndpoints.cs` (endpoints), `backend/Camion.Api/Auth/ValidacionAuth.cs` (validaciones y mensajes) y `backend/Camion.Api/Program.cs` (Identity, JWT y validación de sesión).
- Trazabilidad: HU-N03 Registro de comprador (D-8), HU-059 Iniciar sesión (RN-SE-001), HU-060 Cierre automático de sesión (RN-SE-002), HU-061 Registrar auditoría (RN-SE-003), HU-062 Proteger datos personales (RN-SE-004), RN-SE-006 Control de accesos.

Estado: los 4 endpoints están **IMPLEMENTADOS**. Ninguno está **PROBADO** todavía con pruebas automáticas (se están creando en `backend/Camion.Api.Tests/`).

Requisitos: SDK de .NET 8, XAMPP con MariaDB 10.4 encendido y el script `database/002_esquema_inicial_mariadb_10_4.sql` importado (ver `backend/GUIA-CONEXION-MARIADB.md`).

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

## 4. Los 4 endpoints

Todas las respuestas de error con cuerpo tienen esta forma (JSON):

```json
{ "error": "Mensaje general en español.",
  "errores": { "password": ["Debe tener al menos una letra mayúscula."] } }
```

`errores` solo aparece en las respuestas 400 del registro, con un mensaje por cada campo que falla. Nunca se devuelve la contraseña ni detalles internos.

### POST /api/auth/registro/comprador (sin iniciar sesión)

Crea una cuenta de comprador. La cuenta queda **activa al instante** (no necesita aprobación) y siempre recibe el rol `COMPRADOR`: no se puede pedir otro rol.

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

### POST /api/auth/login (sin iniciar sesión)

Datos que se envían: `email`, `password`.

- **200**: `{ "accessToken", "tokenType": "Bearer", "expiraEnUtc", "usuarioId", "nombreCompleto", "email", "roles" }`. El token dura **2 horas**.
- **400**: `"Escribe tu correo y tu contraseña."` si falta alguno.
- **401**: `"Correo o contraseña incorrectos."` El mismo mensaje si el correo no existe, la contraseña está mal o la cuenta no está activa, para no revelar qué falló.
- **423**: `"Cuenta bloqueada temporalmente por varios intentos fallidos. Intenta de nuevo en 15 minutos."` Tras 5 intentos fallidos la cuenta queda bloqueada 15 minutos.

### GET /api/auth/me (requiere token)

Devuelve el perfil de quien inició sesión. Se envía la cabecera `Authorization: Bearer <TOKEN>`.

- **200**: `{ "usuarioId", "nombreCompleto", "email", "phoneNumber", "roles" }`
- **401**: sin token, token inválido o caducado, cuenta no activa, o sesión cerrada con `/logout`.

### POST /api/auth/logout (requiere token)

Cierra la sesión. Por dentro aumenta en uno el número de versión de sesión del usuario (`SessionVersion`), así que **todos** los tokens emitidos antes para ese usuario dejan de valer, en cualquier dispositivo.

- **204**: sin cuerpo.
- **401**: si el token ya no era válido.

### Validación de sesión en toda la API

La comprobación "la cuenta está activa y la sesión no se cerró" se hace una sola vez, al validar el token (evento `OnTokenValidated` en `Program.cs`). Por eso vale para **todos** los endpoints protegidos, los de hoy y los que se añadan después.

## 5. Probar a mano con PowerShell

Usa una segunda terminal con el backend encendido. Los datos son **de prueba** (correo de `example.com`, contraseña inventada); no uses datos ni contraseñas reales.

```powershell
$base = "http://localhost:5080/api/auth"

# Registro
$registro = @{ nombreCompleto="Comprador Prueba"; email="comprador.prueba@example.com"; telefono="300 123 4567"; password="SoloPrueba123" } | ConvertTo-Json
Invoke-RestMethod -Uri "$base/registro/comprador" -Method Post -ContentType "application/json" -Body $registro

# Login (guarda la respuesta sin imprimir el token)
$login = @{ email="comprador.prueba@example.com"; password="SoloPrueba123" } | ConvertTo-Json
$resultado = Invoke-RestMethod -Uri "$base/login" -Method Post -ContentType "application/json" -Body $login
$resultado | Select-Object usuarioId, nombreCompleto, email, roles, expiraEnUtc
$cabecera = @{ Authorization = "Bearer $($resultado.accessToken)" }

# Mi perfil
Invoke-RestMethod -Uri "$base/me" -Headers $cabecera

# Cerrar sesión
Invoke-RestMethod -Uri "$base/logout" -Method Post -Headers $cabecera

# Después del logout, /me debe responder 401 (PowerShell lo muestra como error)
Invoke-RestMethod -Uri "$base/me" -Headers $cabecera
```

Nota: `Invoke-RestMethod` muestra las respuestas 400, 401, 409 y 423 como un error rojo; es lo esperado en esos casos.

## 6. Registro de eventos (auditoría básica)

El backend escribe en su log (consola), con la categoría `Camion.Api.Auditoria`: registro exitoso, login exitoso, login fallido, bloqueo y logout, con el id de usuario cuando existe. Nunca escribe contraseñas ni tokens, y el correo de un intento fallido aparece enmascarado (por ejemplo `c***@example.com`). Guardar estos eventos en la tabla `auditoria_eventos` es un ítem posterior.

## 7. Qué hace el frontend con la sesión

- Guarda el token y su caducidad en el navegador (`localStorage`, clave `camion.sesion`), así la sesión se mantiene al recargar. IMPLEMENTADO.
- Si el backend responde 401, borra la sesión y muestra "Tu sesión terminó. Inicia sesión de nuevo." IMPLEMENTADO.
- El botón "Cerrar sesión" llama a `/logout` y borra la sesión local aunque la llamada falle. IMPLEMENTADO.
- Tras **30 minutos** sin actividad cierra la sesión solo en ese navegador (sin llamar a `/logout`). IMPLEMENTADO; el valor de 30 minutos es **PROPUESTO**, pendiente de confirmar por la responsable.

## 8. Avisos importantes

- El script SQL ya creó las tablas de Identity. **No** ejecutes `dotnet ef database update` ni crees una migración inicial que las vuelva a crear.
- **Pendiente** (no existe todavía): roles de asesor y administrador, permisos por rol, confirmación de correo, cambio de contraseña, limitación de peticiones por IP (se valorará en P2) y guardar la auditoría en base de datos.
- **BLOQUEADO**: recuperar contraseña (HU-063), porque necesita enviar correos y no hay servicio de correo aprobado (D-5).
