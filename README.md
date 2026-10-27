# CAMION — Financiamos Tu Camión

Portal **Red de Venta – Financiamos Tu Camión** (línea base funcional v3.0): un sitio donde los compradores buscan camiones, piden crédito o seguro y venden su propio vehículo; los asesores publican vehículos y el administrador atiende los servicios.

> **Estado actual (rama `feature/p3-catalogo`):**
> - Existen las **cuentas** del portal (P1 y P2): comprador (acceso inmediato), asesor (queda pendiente hasta que el administrador lo apruebe) y administrador (con un panel para aprobar, rechazar, reconsiderar, bloquear y desbloquear cuentas).
> - Nuevo en P3: el **catálogo público**. Cualquier persona, sin cuenta, ve los vehículos publicados, busca, filtra y abre el detalle.
> - Todavía no existen el alta de vehículos (P4), el contacto con el vendedor (P5), los créditos, los seguros, etc.
>
> El estado verificado del código antes de P2 está en `docs/coordinacion/ESTADO-REAL.md`. Lo de P2 y P3 que se describe aquí se comprobó leyendo el código de esta rama.

Estados que usamos en este documento:

- **IMPLEMENTADO**: el código existe en esta rama.
- **PROBADO**: hay pruebas automáticas que se ejecutaron y pasaron. *Las pruebas de P3 se están escribiendo; por eso nada de P3 aparece como PROBADO.*
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
| Migración 003 que adapta la tabla `perfiles_asesor` a los estados Pendiente / Aprobado / Rechazado / Bloqueado (D-10) | IMPLEMENTADO; **aplicada** en `camion_ventas` por la responsable desde phpMyAdmin, con respaldo previo (D-14, D-16) | `database/migraciones/003_p2_estados_asesor.sql` |

Según `docs/coordinacion/BITACORA.md`, las funciones de P2 están **PROBADAS**: 223/223 pruebas de API y 93/93 de pantallas pasaron, y la responsable hizo la prueba manual contra `camion_ventas` el 2026-10-26 sin errores. El detalle de cada endpoint está en `backend/GUIA-AUTENTICACION.md`, `docs/criterios/P1-contrato-api.md` y `docs/criterios/P2-contrato-api.md`. La guía paso a paso del administrador está en `docs/GUIA-ADMINISTRACION.md`.

### Novedades de P3: catálogo público

Todo es de **solo lectura**: nadie puede crear, editar ni borrar vehículos desde el catálogo. Funciona igual con o sin cuenta.

| Función | Estado | Dónde está |
|---|---|---|
| Portal público **sin cuenta**: la página principal (`#/`) es ahora el catálogo, no el inicio de sesión | IMPLEMENTADO | `frontend/src/App.tsx`, `frontend/src/rutas.ts` |
| Página de inicio con **Destacados** (hasta 8) y **Recientes** (hasta 12) | IMPLEMENTADO | `GET /api/catalogo/inicio`; `frontend/src/components/Inicio.tsx` |
| Cuatro categorías: **Carros, Camiones, Carrocerías y Maquinaria** (D-17), como enlaces en el encabezado y en el inicio. Solo aparecen las que tienen al menos un vehículo visible | IMPLEMENTADO | `frontend/src/components/Encabezado.tsx`, `frontend/src/api/catalogo.ts` |
| Búsqueda por texto (marca, modelo o título; hasta 100 caracteres; no distingue mayúsculas) desde el encabezado | IMPLEMENTADO | `GET /api/catalogo/publicaciones`; `Encabezado.tsx` |
| Filtros: categoría, marca, modelo (según la marca), presupuesto mínimo y máximo, año desde y hasta, kilometraje máximo y orden. Botones **Aplicar filtros** y **Limpiar filtros**. Los filtros quedan en la dirección de la página, así que se pueden compartir | IMPLEMENTADO | `GET /api/catalogo/filtros`; `frontend/src/components/Filtros.tsx`, `Resultados.tsx` |
| Resultados en páginas de 12, con los **destacados siempre primero** (RN-VE-012) | IMPLEMENTADO | `backend/Camion.Api/Endpoints/CatalogoEndpoints.cs` |
| Detalle del vehículo (`#/vehiculo/<slug>`): fotos, precio, etiquetas, ficha técnica y descripción. Si el vehículo no se puede mostrar: "Este vehículo no está disponible." | IMPLEMENTADO | `GET /api/catalogo/publicaciones/{slug}`; `frontend/src/components/DetalleVehiculo.tsx` |
| Un vehículo **Reservado** se sigue viendo, con la etiqueta "Reservado" (D-11). Un vendido no se ve nunca | IMPLEMENTADO | `frontend/src/components/TarjetaVehiculo.tsx`, `backend/Camion.Api/Catalogo/ConsultasCatalogo.cs` |
| Regla de visibilidad en un solo lugar: publicado, no vendido, dentro de su vigencia, con la suscripción del asesor activa y el asesor con cuenta activa | IMPLEMENTADO | `backend/Camion.Api/Catalogo/ConsultasCatalogo.cs` |
| El kilometraje no se muestra en las carrocerías, ni en la maquinaria con kilometraje 0 (D-17) | IMPLEMENTADO | `frontend/src/api/catalogo.ts` |
| Si un vehículo no tiene foto o la foto no carga, se ve una imagen "Sin foto" | IMPLEMENTADO | `frontend/src/components/ImagenVehiculo.tsx` |
| Script de datos de demostración (12 publicaciones DEMO, 10 visibles) para la prueba manual (D-18) | IMPLEMENTADO como script; **no está aplicado** en `camion_ventas`. Lo aplica la responsable desde phpMyAdmin (D-16) | `database/demo/demo_p3_catalogo.sql` |

Ninguna función de P3 está todavía en estado PROBADO. La guía para la responsable (qué hace que un vehículo aparezca, cómo usar los filtros y cómo cargar los datos de demostración) está en `docs/GUIA-CATALOGO.md`. El detalle técnico de la API está en `docs/criterios/P3-contrato-api.md`.

Mientras la base no tenga publicaciones, el inicio muestra "Pronto tendremos vehículos publicados." Es lo normal: el alta de vehículos llega en P4.

## Qué NO existe todavía

- Alta y edición de vehículos y publicaciones por el asesor, subida de fotos reales y placa (P4). Hoy el catálogo solo muestra lo que ya está en la base; las fotos todavía no se sirven, por eso se ve "Sin foto".
- Contactar al vendedor desde el detalle del vehículo (P5).
- Planes y pagos.
- Contactos (leads), créditos, búsquedas personalizadas y favoritos.
- Avisos "Vende tu camión" y "Compra tu seguro todo riesgo".
- Notificaciones (por ejemplo, avisar por correo al asesor cuando lo aprueban) y reportes.
- "Solicitar ser asesor" desde una cuenta de comprador (por ahora, quien quiera ser asesor usa otro correo; D-15).
- Que el administrador cree administradores desde la web: hoy solo se hace con el comando local `promover-admin`.
- Limitación de peticiones por IP.
- Guardar en la base de datos los eventos de inicio y cierre de sesión (hoy solo van al log del servidor).
- Recuperación de contraseña: **BLOQUEADA**, no hay servicio de correo aprobado (D-5).

La base de datos tiene 39 tablas en el script. El código usa las de usuarios, roles, perfil de comprador, perfil de asesor, historial de autorizaciones y auditoría y, desde P3 y **solo para leer**, las de vehículos, publicaciones, fotos, destacados, vigencias, suscripciones y sus catálogos (categorías, marcas, modelos, carrocerías, combustibles, departamentos y municipios). P3 no cambia el esquema. **Que exista una tabla no significa que la función exista.**

El orden de trabajo está en `docs/coordinacion/BACKLOG.md`.

## Estructura

```text
CAMION/
├── frontend/                 # React + Vite (catálogo público, login, registro, perfil
│                             #   y panel de administración)
├── backend/Camion.Api/       # API .NET 8 (salud, autenticación, administración de usuarios,
│                             #   catálogo público y el comando local promover-admin)
├── backend/Camion.Api.Tests/ # Pruebas del backend
├── database/                 # Scripts SQL del esquema (MySQL 8 y MariaDB 10.4)
│   ├── migraciones/          # Cambios de esquema con vuelta atrás (003: estados del asesor)
│   └── demo/                 # Datos de demostración, no reales (P3, D-18)
├── docs/                     # Requisitos, criterios, coordinación, GUIA-ADMINISTRACION.md
│                             #   y GUIA-CATALOGO.md
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

Vite reenvía (proxy) las rutas `/api` y `/health` al backend en el puerto 5080, por eso el backend tiene que estar encendido.

## Cómo entrar al portal

| Quiero… | Abro |
|---|---|
| Ver el catálogo (sin cuenta) | **http://localhost:5173/#/** (también vale http://localhost:5173) |
| Buscar y filtrar | **http://localhost:5173/#/buscar**, o escribo en el buscador del encabezado |
| Ver un vehículo | Pulso su tarjeta; la dirección queda como `#/vehiculo/<slug>` |
| Iniciar sesión, ver mi perfil o, si soy administrador, entrar a "Administrar usuarios" | **http://localhost:5173/#/cuenta**, o el enlace **Iniciar sesión** del encabezado (con la sesión abierta se llama **Mi cuenta**) |
| Crear una cuenta | **http://localhost:5173/#/cuenta/registro**, o el enlace **Registrarme** del encabezado |

Cambio respecto a P2: la página principal ya **no** es el inicio de sesión, sino el catálogo. El inicio de sesión está en `#/cuenta`.

## Ejecutar las pruebas

Pruebas automáticas (añadidas en P1). No usan la base de datos real: el backend se prueba con SQLite en memoria.

```powershell
# Backend (desde la raíz del repositorio)
dotnet test backend/Camion.Api.Tests

# Frontend (desde la carpeta frontend)
cd frontend
npm test
```

Último resultado registrado en `docs/coordinacion/BITACORA.md`: el 2026-10-26, con P2, backend 223/223 y frontend 93/93 pruebas pasan. Las pruebas de P3 se están escribiendo; todavía no hay resultado de P3 que informar.

## Seguridad

- Nunca guardes contraseñas, tokens, cadenas de conexión ni claves en Git. Usa `dotnet user-secrets` o variables de entorno.
- Los ejemplos de esta documentación usan marcadores como `<TU_CLAVE>` y datos de prueba evidentes; no son credenciales reales.
- La configuración actual (HTTP sin HTTPS, sin CORS, usuario `root` de XAMPP) sirve solo para trabajar en tu equipo, no para producción.

## Cómo se trabaja

El trabajo se coordina con el comando `/coordinar` (ver `CLAUDE.md`). Requisitos en `docs/requisitos/`; estado real, backlog, decisiones y bitácora en `docs/coordinacion/`.
