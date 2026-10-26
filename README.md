# CAMION — Financiamos Tu Camión

Portal **Red de Venta – Financiamos Tu Camión** (línea base funcional v3.0): un sitio donde los compradores buscan camiones, piden crédito o seguro y venden su propio vehículo; los asesores publican vehículos y el administrador atiende los servicios.

> **Estado actual (rama `feature/p1-autenticacion`):** solo existe la **cuenta de comprador** (crear cuenta, iniciar sesión, ver mi perfil y cerrar sesión). El resto del portal todavía no está construido. El estado verificado del código está en `docs/coordinacion/ESTADO-REAL.md`.

Estados que usamos en este documento:

- **IMPLEMENTADO**: el código existe en esta rama.
- **PROBADO**: hay pruebas automáticas que se ejecutaron y pasaron. *Todavía no hay nada en este estado.*
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

Ninguna de estas funciones está todavía en estado PROBADO. El detalle de cada endpoint está en `backend/GUIA-AUTENTICACION.md` y en `docs/criterios/P1-contrato-api.md`.

## Qué NO existe todavía

Catálogo público, vehículos, fotografías, publicaciones, asesores (registro y aprobación), administrador y su panel, planes y pagos, contactos (leads), créditos, búsquedas personalizadas, favoritos, avisos "Vende tu camión" y "Compra tu seguro todo riesgo", notificaciones, reportes, auditoría guardada en base de datos y recuperación de contraseña (esta última BLOQUEADA: no hay servicio de correo aprobado).

La base de datos tiene 39 tablas en el script, pero el código solo usa las de usuarios, roles y perfil de comprador. **Que exista una tabla no significa que la función exista.**

El orden de trabajo está en `docs/coordinacion/BACKLOG.md`.

## Estructura

```text
CAMION/
├── frontend/                 # React + Vite (pantallas de login, registro y perfil)
├── backend/Camion.Api/       # API .NET 8 (salud y autenticación)
├── backend/Camion.Api.Tests/ # Pruebas del backend (en preparación)
├── database/                 # Scripts SQL del esquema (MySQL 8 y MariaDB 10.4)
├── docs/                     # Requisitos, criterios y coordinación
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

Resultado el 2026-10-26 en la rama `feature/p1-autenticacion`: backend 76/76 y frontend 42/42 pruebas pasan.

## Seguridad

- Nunca guardes contraseñas, tokens, cadenas de conexión ni claves en Git. Usa `dotnet user-secrets` o variables de entorno.
- Los ejemplos de esta documentación usan marcadores como `<TU_CLAVE>` y datos de prueba evidentes; no son credenciales reales.
- La configuración actual (HTTP sin HTTPS, sin CORS, usuario `root` de XAMPP) sirve solo para trabajar en tu equipo, no para producción.

## Cómo se trabaja

El trabajo se coordina con el comando `/coordinar` (ver `CLAUDE.md`). Requisitos en `docs/requisitos/`; estado real, backlog, decisiones y bitácora en `docs/coordinacion/`.
