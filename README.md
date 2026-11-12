# CAMION — Financiamos Tu Camión

Portal **Red de Venta – Financiamos Tu Camión** (línea base funcional v3.0).

## 1. Qué es

Un portal de **intermediación** (D-45). Los vehículos son de terceros y los publican asesores aprobados. La plataforma **solo pone en contacto** al interesado con el asesor o con el administrador. La gestión de créditos ante bancos y de pólizas de seguro es **interna** y no se hace en la plataforma (D-45, D-54, D-55).

Estados que usa esta documentación: **IMPLEMENTADO** (el código existe), **PROBADO** (pruebas ejecutadas y aprobadas), **DOCUMENTADO** (descrito, no necesariamente programado), **PROPUESTO** (pendiente de confirmar) y **BLOQUEADO**.

## 2. Qué funciona hoy

`main` contiene P0 a P6, unidos mediante las PR #2 a #12. Todo lo de esta sección está **IMPLEMENTADO y PROBADO** con pruebas automáticas. Además, la responsable hizo una prueba manual de cada fase. La de P6 fue **parcial**: lo que no se probó a mano lo cubren las pruebas automáticas. No hay pruebas E2E. Detalle por fase en [`docs/coordinacion/BITACORA.md`](docs/coordinacion/BITACORA.md) y [`docs/coordinacion/ESTADO-REAL.md`](docs/coordinacion/ESTADO-REAL.md).

**Visitante (sin cuenta)**
- Ver el catálogo: inicio con destacados y recientes, búsqueda, filtros y ficha del vehículo (P3, P4b).
- Contactar al vendedor desde la ficha, con casilla de autorización de datos, o escribirle por WhatsApp (P5).
- Enviar los avisos "Vende tu camión" y "Compra tu seguro todo riesgo" (P5b, D-6).
- Pedir un crédito desde la portada o desde una ficha, y llenar "Busco un vehículo" (P6).

**Comprador** (registro opcional, acceso inmediato y de solo lectura; D-3)
- Todo lo del visitante, con la sesión abierta.
- Ver en "Mis solicitudes" lo que envió con su sesión. Es una vista de solo lectura (P6, D-59).

**Asesor** (su cuenta queda pendiente hasta que el administrador la aprueba; P2)
- Gestionar su inventario de vehículos con fotos (P4a).
- Gestionar sus publicaciones y destacados según su plan, marcar vendido y revertir la venta (P4b).
- Consultar "Mi suscripción" (P4p).
- Atender en "Mis contactos" solo los contactos de sus propios vehículos con plan vigente, o los que el administrador le reasigne (P5, D-7). No ve avisos, créditos ni búsquedas.

**Administrador**
- Aprobar, rechazar, reconsiderar, bloquear y desbloquear cuentas (P2).
- Gestionar planes, suscripciones, catálogos de marcas y modelos y la foto de portada (P4p, D-42).
- Gestionar el inventario y las publicaciones de todos, y reasignar vehículos con motivo (P4b, D-34).
- Atender todas las solicitudes en "Contactos": contactos, avisos, créditos y búsquedas. Puede añadir notas, cerrar, reabrir, reasignar contactos (con auditoría) y relacionar vehículos con una búsqueda (P5, P5b, P6).
- El primer administrador se crea con el comando local `promover-admin` (D-13). Ver [`docs/GUIA-ADMINISTRACION.md`](docs/GUIA-ADMINISTRACION.md).

**Pendiente** (ver [`docs/coordinacion/BACKLOG.md`](docs/coordinacion/BACKLOG.md))
- P7: ventas, planes y administración. P8: reportes, marketing e integraciones.
- INF-1: CI en GitHub Actions. INF-2: requisitos antes de publicar en internet.
- Revisión legal de la política de datos y de los términos (D-44, PROPUESTO). Ningún formulario sale a internet sin ella.
- Otros: P2b ("Solicitar ser asesor") y P6-B (hallazgos bajos de seguridad).
- Recuperar contraseña está **BLOQUEADO**: no hay un servicio de correo aprobado (D-5).
- Antes de producción hay que borrar los datos de prueba de `camion_ventas`.

## 3. Tecnologías

| Parte | Tecnología | Carpeta | Puerto |
|---|---|---|---|
| Frontend | React 19 + Vite 7 + TypeScript | `frontend/` | 5173 (proxy de `/api` y `/health`) |
| Backend | ASP.NET Core .NET 8 (minimal APIs) + Identity/JWT, EF Core con Pomelo | `backend/Camion.Api/` | 5080 |
| Base de datos | MariaDB 10.4 (XAMPP), base `camion_ventas` | `database/` | 3306 |
| Pruebas | xUnit (API, con SQLite en memoria) y Vitest | `backend/Camion.Api.Tests/`, `frontend/tests/` | — |

`global.json` fija el SDK 8 de .NET.

## 4. Puesta en marcha en un equipo nuevo

### Requisitos
- SDK de .NET 8 (versión de `global.json` o una 8.0.x posterior).
- Node.js 20.19+ o 22.12+ (lo exige Vite 7).
- XAMPP con MariaDB encendido en el puerto 3306.

### Secretos locales
La API **no arranca** sin dos claves. Se guardan con `dotnet user-secrets` desde `backend/Camion.Api`, nunca en Git ni en `appsettings.json`.

| Clave | Para qué sirve |
|---|---|
| `ConnectionStrings:CamionDatabase` | Conexión a la base `camion_ventas` |
| `Jwt:Key` | Firma de los tokens; mínimo 32 bytes |

```powershell
dotnet user-secrets set "ConnectionStrings:CamionDatabase" "<TU_CADENA_DE_CONEXION>"
dotnet user-secrets set "Jwt:Key" "<TU_CLAVE_DE_32_BYTES_O_MAS>"
```

Hay una clave opcional, `Media:Ruta`: es la carpeta de las fotos y por defecto vale `C:\CAMION_media`. Para armar la cadena y generar la clave, ver [`backend/GUIA-CONEXION-MARIADB.md`](backend/GUIA-CONEXION-MARIADB.md) y [`backend/GUIA-AUTENTICACION.md`](backend/GUIA-AUTENTICACION.md). No pegues estos valores en chats ni en documentos.

### Base de datos
1. Antes de cada script, haz un **respaldo** con `mysqldump --result-file=...` en `C:\Respaldo_CAMION\`. El comando está en la cabecera de cada migración. Nunca sobrescribas un respaldo.
2. **Detén la API.**
3. En phpMyAdmin, **selecciona primero la base `camion_ventas`**: los archivos `_SUBIR` no traen `USE`. Luego entra en *Importar*, elige el archivo y ejecútalo.
4. Importa los scripts en este orden:
   - `database/002_esquema_inicial_mariadb_10_4.sql` (esquema inicial);
   - por número, `database/migraciones/` y `database/catalogos/`: 003, 004, 005, 006, 007, 008, 009, 010, 011, 012, 013, 014 y 015.
5. Si un script tiene archivo `_SUBIR`, importa **solo ese archivo**. Si no lo tiene, sigue las instrucciones de su cabecera. En phpMyAdmin, importar la 004 completa falló; hubo que importar solo su sección SUBIR (ver BITACORA, P4a).
6. Los archivos `_VERIFICAR_Y_BAJAR` y las secciones BAJAR **no se importan al instalar**.
7. `database/demo/demo_p3_catalogo.sql` carga datos ficticios. Es opcional y sirve solo para pruebas (D-18, D-24).

### Arrancar la API
Desde `backend/Camion.Api`:

```powershell
dotnet run
```

Si Windows (Smart App Control) bloquea `Camion.Api.exe`, compila con `dotnet build` y arranca la `.dll` (ver BITACORA, P5b):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5080"
dotnet bin\Debug\net8.0\Camion.Api.dll
```

Para comprobar que funciona, abre http://localhost:5080/health y http://localhost:5080/health/database.

### Arrancar el frontend
Desde `frontend/`, en otra terminal:

```powershell
npm install
npm run dev
```

Abre http://localhost:5173. El inicio es el catálogo. "Mi cuenta" está en `#/cuenta`.

## 5. Pruebas

```powershell
# Backend (desde la raíz)
dotnet test backend/Camion.Api.Tests

# Frontend (desde frontend/)
npx vitest run
npx tsc -b
npm run build
```

Último resultado registrado, del **2026-11-12** (P6): backend **1118/1118** y frontend **643/643**; `tsc -b` y `npm run build`, correctos. Las pruebas de la API usan SQLite en memoria y no tocan `camion_ventas`.

## 6. Forma de trabajo

- Ramas `feature/*`, una por fase, y PR contra `main`. Nadie hace merge ni push a `main` sin aprobación humana.
- Respaldo antes de cada migración. Los cambios de esquema se hacen solo con migraciones reversibles.
- El trabajo se coordina con el comando `/coordinar`. El estado, el backlog, las decisiones y la bitácora están en `docs/coordinacion/`.
- Las reglas completas de negocio, de seguridad y de Git están en [`CLAUDE.md`](CLAUDE.md).
- Nunca guardes contraseñas, tokens ni cadenas de conexión en Git. La configuración actual (HTTP, usuario local de XAMPP) sirve solo para trabajar en tu equipo.

## 7. Mapa de la documentación

| Dónde | Qué contiene |
|---|---|
| `docs/coordinacion/ESTADO-REAL.md` | Estado verificado del código, con actualizaciones hasta P6 |
| `docs/coordinacion/BACKLOG.md` | Orden de trabajo y estado de cada fase |
| `docs/coordinacion/DECISIONES.md` | Decisiones D-1 en adelante |
| `docs/coordinacion/BITACORA.md` | Qué se hizo en cada sesión, con migraciones, respaldos y pruebas |
| `docs/coordinacion/REVISION-SEGURIDAD-P5*.md`, `-P6.md` | Revisiones de seguridad |
| `docs/requisitos/` | Requisitos funcionales v3.0 y minuta de transferencia |
| `docs/criterios/` | Criterios, contratos de API y evidencia por fase |
| `docs/manual/` | Guías de uso y de prueba manual: P5 contactos, P5b avisos y P6 créditos |
| `docs/GUIA-ADMINISTRACION.md`, `GUIA-CATALOGO.md`, `GUIA-CATALOGOS.md` | Guías de administración, catálogo público y marcas y modelos |
| `docs/legal/` | Borradores de la política de datos y de los términos (D-44; falta la revisión legal) |
| `backend/GUIA-*.md` | Conexión a MariaDB y autenticación |
| `database/` | Esquema, `migraciones/`, `catalogos/` y `demo/` |

Datos de contacto públicos: los del pie del portal.
