# CAMION — Financiamos Tu Camión

Portal **Red de Venta – Financiamos Tu Camión** (línea base funcional v3.0): un sitio donde los compradores buscan camiones, piden crédito o seguro y venden su propio vehículo; los asesores publican vehículos y el administrador atiende los servicios.

> **Estado actual (rama `feature/p2-asesores`):** existen las **cuentas** del portal: comprador (acceso inmediato), asesor (queda pendiente hasta que el administrador lo apruebe) y administrador (con un panel para aprobar, rechazar, reconsiderar, bloquear y desbloquear cuentas). El resto del portal (catálogo, vehículos, créditos, etc.) todavía no está construido. El estado verificado del código antes de P2 está en `docs/coordinacion/ESTADO-REAL.md`; lo de P2 que se describe aquí se comprobó leyendo el código de esta rama.

Estados que usamos en este documento:

- **IMPLEMENTADO**: el código existe en esta rama.
- **PROBADO**: hay pruebas automáticas que se ejecutaron y pasaron. *Las pruebas de P2 se están escribiendo; por eso nada de P2 aparece como PROBADO.*
- **DOCUMENTADO**: está descrito, pero no necesariamente programado.
- **PROPUESTO**: valor o comportamiento sugerido, pendiente de confirmación de la responsable.
- **BLOQUEADO**: no se puede hacer hasta que se resuelva algo.

## Tecnologías

| Parte | Tecnología | Carpeta |
|---|---|---|
| Frontend | React 19 + Vite 7 + TypeScript | `frontend/` |
| Backend | ASP.NET Core .NET 8 (minimal APIs) con ASP.NET Core Identity y tokens JWT | `backend/Camion.Api/` |
| Acceso a datos | Entity Framework Core con el proveedor Pomelo (MySQL/MariaDB) | `backend/Camion.Api/Data/` |
| Base de datos | MariaDB 10.4 de XAMPP, base `camion_ventas`, puerto 3306 | scripts en `database/` |
| Pruebas backend | xUnit (proyecto en preparación) | `backend/Camion.Api.Tests/` |

El archivo `global.json` de la raíz fija el **SDK 8 de .NET** (versión 8.0.425, admite actualizaciones menores de la misma versión 8.0.x). Así todos los equipos compilan con el mismo SDK aunque tengan otros instalados.

## Qué funciona hoy

| Función | Estado | Dónde está |
|---|---|---|
| Registro de comprador (acceso inmediato, sin aprobación; siempre con rol `COMPRADOR`) | IMPLEMENTADO | `POST /api/auth/registro/comprador` |
| Iniciar sesión con correo y contraseña (token válido 2 horas) | IMPLEMENTADO | `POST /api/auth/login` |
| Ver mi perfil | IMPLEMENTADO | `GET /api/auth/me` |
| Cerrar sesión (invalida el token en **todos** los dispositivos del usuario) | IMPLEMENTADO | `POST /api/auth/logout` |
| Bloqueo de la cuenta 15 minutos tras 5 intentos fallidos | IMPLEMENTADO | `backend/Camion.Api/Program.cs` |
| Mensajes de error en español, campo por campo, y reglas de contraseña visibles en el formulario | IMPLEMENTADO | backend y `frontend/src/components/` |
| Sesión persistente: al recargar la página la sesión se mantiene mientras el token siga vigente | IMPLEMENTADO | `frontend/src/auth/session.ts` |
| Cierre automático por inactividad: **30 minutos** sin clics, teclas ni toques cierran la sesión solo en ese navegador | IMPLEMENTADO; el valor de 30 min es **PROPUESTO** (pendiente de confirmar) | `frontend/src/auth/inactivity.ts` |
| Comprobación de salud del backend y de la base de datos | IMPLEMENTADO | `GET /health`, `GET /health/database` |
| Registro de eventos de seguridad (registro, login, fallo, bloqueo, logout) en el log del servidor, sin contraseñas ni tokens | IMPLEMENTADO | `backend/Camion.Api/Endpoints/AuthEndpoints.cs` |

### Novedades de P2: asesores y administración de usuarios

| Función | Estado | Dónde está |
|---|---|---|
| Registro de asesor: en "Crear cuenta" se elige "Soy comprador" o "Soy asesor". El asesor da además tipo y número de documento y, si quiere, un nombre comercial. Su cuenta queda **PENDIENTE** (no puede entrar) hasta que el administrador la apruebe | IMPLEMENTADO | `POST /api/auth/registro/asesor`; `frontend/src/components/RegistroForm.tsx` |
| Tipos de documento CC, CE, NIT y PAS (pasaporte) | IMPLEMENTADO; la lista es **PROPUESTA** (pendiente de confirmar) | `backend/Camion.Api/Auth/ValidacionAuth.cs` |
| Inicio de sesión con aviso por estado: si la contraseña es correcta pero la cuenta está pendiente, rechazada o bloqueada, se explica el motivo (respuesta 403) | IMPLEMENTADO | `POST /api/auth/login`; `frontend/src/components/LoginForm.tsx` |
| Tres roles (Comprador, Asesor, Administrador) que se crean solos al arrancar el backend; el rol nunca se elige en un formulario público | IMPLEMENTADO | `backend/Camion.Api/Auth/Roles.cs`, `Program.cs` |
| Permisos por rol: solo el administrador puede usar `/api/admin/…`; los demás reciben "No tienes permiso para realizar esta acción." | IMPLEMENTADO | `Program.cs`, `Auth/RespuestaAutorizacion.cs` |
| Crear el primer administrador con el comando local `promover-admin` (D-13) | IMPLEMENTADO | `backend/Camion.Api/Comandos/PromoverAdmin.cs`; ver `docs/GUIA-ADMINISTRACION.md` |
| Panel "Administrar usuarios": lista con filtros (estado, rol, búsqueda por nombre o correo) y páginas de 20; por defecto muestra los pendientes | IMPLEMENTADO | `GET /api/admin/usuarios`; `frontend/src/components/AdminUsuarios.tsx` |
| Aprobar o rechazar (con motivo) a un asesor pendiente | IMPLEMENTADO | `POST /api/admin/usuarios/{id}/aprobar` y `/rechazar` |
| Reconsiderar a un asesor rechazado (pasa a aprobado; motivo obligatorio) (D-15) | IMPLEMENTADO | `POST /api/admin/usuarios/{id}/reconsiderar` |
| Bloquear (con motivo) y desbloquear compradores y asesores; nunca a un administrador ni la propia cuenta | IMPLEMENTADO | `POST /api/admin/usuarios/{id}/bloquear` y `/desbloquear` |
| Rechazar y bloquear cierran al instante las sesiones abiertas de esa persona en todos sus equipos | IMPLEMENTADO | `Endpoints/AdminUsuariosEndpoints.cs` |
| Cuadro de confirmación para cada acción, con el motivo (5 a 500 caracteres cuando es obligatorio) | IMPLEMENTADO; el límite de 5 a 500 es **PROPUESTO** | `frontend/src/components/DialogoAccion.tsx` |
| Auditoría guardada en la base de datos: cada registro de asesor, cada acción del administrador y cada promoción a administrador quedan en `auditoria_eventos`; los cambios de un asesor también en `historial_autorizaciones` | IMPLEMENTADO | `backend/Camion.Api/Auth/Auditoria.cs` |
| Migración 003 que adapta la tabla `perfiles_asesor` a los estados Pendiente / Aprobado / Rechazado / Bloqueado (D-10) | IMPLEMENTADO como script; **su aplicación a la base real no está registrada en este documento** | `database/migraciones/003_p2_estados_asesor.sql` |

Importante: mientras la migración 003 no se aplique a `camion_ventas`, el registro de asesor funciona, pero **aprobar, rechazar, reconsiderar, bloquear y desbloquear a un asesor fallan** contra la base real (la tabla no acepta todavía los estados nuevos). Bloquear y desbloquear a un comprador no dependen de la migración. Ver `docs/GUIA-ADMINISTRACION.md`, sección 4.

Ninguna de las funciones de P2 está todavía en estado PROBADO. El detalle de cada endpoint está en `backend/GUIA-AUTENTICACION.md`, `docs/criterios/P1-contrato-api.md` y `docs/criterios/P2-contrato-api.md`. La guía paso a paso del administrador está en `docs/GUIA-ADMINISTRACION.md`.

## Qué NO existe todavía

- Catálogo público, vehículos, fotografías y publicaciones.
- Planes y pagos.
- Contactos (leads), créditos, búsquedas personalizadas y favoritos.
- Avisos "Vende tu camión" y "Compra tu seguro todo riesgo".
- Notificaciones (por ejemplo, avisar por correo al asesor cuando lo aprueban) y reportes.
- "Solicitar ser asesor" desde una cuenta de comprador (por ahora, quien quiera ser asesor usa otro correo; D-15).
- Que el administrador cree administradores desde la web: hoy solo se hace con el comando local `promover-admin`.
- Limitación de peticiones por IP.
- Guardar en la base de datos los eventos de inicio y cierre de sesión (hoy solo van al log del servidor).
- Recuperación de contraseña: **BLOQUEADA**, no hay servicio de correo aprobado (D-5).

La base de datos tiene 39 tablas en el script, pero el código solo usa las de usuarios, roles, perfil de comprador, perfil de asesor, historial de autorizaciones y auditoría. **Que exista una tabla no significa que la función exista.**

El orden de trabajo está en `docs/coordinacion/BACKLOG.md`.

## Estructura

```text
CAMION/
├── frontend/                 # React + Vite (login, registro, perfil y panel de administración)
├── backend/Camion.Api/       # API .NET 8 (salud, autenticación, administración de usuarios
│                             #   y el comando local promover-admin)
├── backend/Camion.Api.Tests/ # Pruebas del backend
├── database/                 # Scripts SQL del esquema (MySQL 8 y MariaDB 10.4)
│   └── migraciones/          # Cambios de esquema con vuelta atrás (003: estados del asesor)
├── docs/                     # Requisitos, criterios, coordinación y GUIA-ADMINISTRACION.md
├── global.json               # Fija el SDK 8 de .NET
└── README.md
```

## Requisitos en tu equipo

- SDK de .NET 8 (el `global.json` pide la 8.0.425 o una 8.0.x posterior).
- Node.js 20.19+ o 22.12+ (lo exige Vite 7).
- XAMPP con MariaDB encendido en el puerto 3306 y el script `database/002_esquema_inicial_mariadb_10_4.sql` importado en la base `camion_ventas`. Ver `backend/GUIA-CONEXION-MARIADB.md`.

## Configurar los secretos locales

El backend **no arranca** si faltan estas dos claves. Se guardan con `dotnet user-secrets`, que las deja fuera del repositorio (nunca en Git ni en `appsettings.json`):

| Clave | Para qué sirve |
|---|---|
| `ConnectionStrings:CamionDatabase` | Cadena de conexión a la base `camion_ventas` de MariaDB |
| `Jwt:Key` | Clave secreta para firmar los tokens; debe tener **al menos 32 bytes** |

```powershell
cd "C:\Users\alex0\OneDrive\Documentos\proyecto venta de camiones\CAMION\backend\Camion.Api"
dotnet user-secrets set "ConnectionStrings:CamionDatabase" "<TU_CADENA_DE_CONEXION>"
dotnet user-secrets set "Jwt:Key" "<TU_CLAVE_DE_32_BYTES_O_MAS>"
```

Las guías `backend/GUIA-CONEXION-MARIADB.md` y `backend/GUIA-AUTENTICACION.md` explican cómo armar la cadena y cómo generar una clave aleatoria sin escribirla a mano. No compartas estos valores con nadie ni los pegues en documentos.

## Arrancar el backend

```powershell
cd "C:\Users\alex0\OneDrive\Documentos\proyecto venta de camiones\CAMION\backend\Camion.Api"
dotnet restore
dotnet run
```

Queda escuchando en **http://localhost:5080**. Para comprobarlo abre http://localhost:5080/health y http://localhost:5080/health/database.

## Arrancar el frontend

En otra terminal:

```powershell
cd "C:\Users\alex0\OneDrive\Documentos\proyecto venta de camiones\CAMION\frontend"
npm install
npm run dev
```

Abre **http://localhost:5173**. Vite reenvía (proxy) las rutas `/api` y `/health` al backend en el puerto 5080, por eso el backend tiene que estar encendido.

## Ejecutar las pruebas

Pruebas automáticas (añadidas en P1). No usan la base de datos real: el backend se prueba con SQLite en memoria.

```powershell
# Backend (desde la raíz del repositorio)
dotnet test backend/Camion.Api.Tests

# Frontend (desde la carpeta frontend)
cd frontend
npm test
```

Último resultado registrado: el 2026-10-26 en la rama `feature/p1-autenticacion` (solo P1), backend 76/76 y frontend 42/42 pruebas pasan. Las pruebas de P2 se están escribiendo; todavía no hay resultado de P2 que informar.

## Seguridad

- Nunca guardes contraseñas, tokens, cadenas de conexión ni claves en Git. Usa `dotnet user-secrets` o variables de entorno.
- Los ejemplos de esta documentación usan marcadores como `<TU_CLAVE>` y datos de prueba evidentes; no son credenciales reales.
- La configuración actual (HTTP sin HTTPS, sin CORS, usuario `root` de XAMPP) sirve solo para trabajar en tu equipo, no para producción.

## Cómo se trabaja

El trabajo se coordina con el comando `/coordinar` (ver `CLAUDE.md`). Requisitos en `docs/requisitos/`; estado real, backlog, decisiones y bitácora en `docs/coordinacion/`.
