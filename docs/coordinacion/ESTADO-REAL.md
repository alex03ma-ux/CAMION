# Estado real del código — CAMION

| Dato | Valor |
|---|---|
| Fecha de auditoría | 2026-10-26 |
| Rama auditada | `feature/p0-auditoria` (creada desde `main` sin cambios) |
| Commit auditado | `4e41f43eb6349eec995b5f1850abd7ea322bc28b` (igual a `main` y a `origin/main` tras `git fetch`) |
| Auditor | `auditor-codigo` (solo lectura; P0 del backlog) |
| Archivos sin seguimiento | `frontend/respaldos/` (no se leyó ni se tocó). La compilación del frontend generó además `frontend/tsconfig.app.tsbuildinfo` y `frontend/tsconfig.node.tsbuildinfo` (no están en `.gitignore`). |

## Resumen ejecutivo (en palabras sencillas)

- **Lo único que funciona hoy es la cuenta de comprador**: crear la cuenta, iniciar sesión y ver "mi perfil". Hay 5 direcciones (endpoints) en el backend: 2 de salud y 3 de autenticación.
- **Ambas compilaciones funcionan**: backend 0 errores / 0 advertencias; frontend compila sin errores.
- **No hay ninguna prueba automática ni CI.** Lo que se dice "probado" en la minuta fue una prueba manual.
- **La base de datos tiene 39 tablas en el script, pero el código solo usa 4** (usuarios, roles, usuario-rol y perfil de comprador). Tener una tabla no significa que la función exista.
- **No existen asesores ni administradores en el código**: no hay registro de asesor, ni aprobación, ni panel de administrador, ni catálogo, ni vehículos, ni leads, ni créditos, ni avisos "Vende tu camión" / "Seguro".
- **El esquema SQL contradice varias decisiones ya tomadas** (D-2 placa, D-4 estados, D-6 avisos sin cuenta). Habrá que corregirlo con migraciones antes de construir esas funciones.
- **El README y las guías están desactualizados** (dicen que no hay autenticación ni base de datos, y que el código no se ha compilado).
- La sesión del frontend **se pierde al recargar** la página y "Cerrar sesión" solo borra la pantalla: el token sigue siendo válido 2 horas.

---

## 1. Git

- Rama actual `feature/p0-auditoria`; HEAD = `main` = `origin/main` = `4e41f43eb6349eec995b5f1850abd7ea322bc28b`.
- Últimos commits: los 6 más recientes (`a13c781` a `4e41f43`) son solo documentación y configuración de agentes. El último commit con código es `eb1a733` (2026-10-13, frontend de registro/login). El backend de autenticación llegó en una serie de 7 commits con el mismo mensaje `feat(auth): implementar registro y login comprador con Identity y JWT` (`697d5a7` … `2c98a22`) y el arreglo `38e46b6`.
- `git status`: solo `?? frontend/respaldos/` antes de compilar.

## 2. Backend (`backend/Camion.Api`)

### 2.1 Estructura

- Proyecto único, .NET 8 (`backend/Camion.Api/Camion.Api.csproj:3`), paquetes: JwtBearer 8.0.11, Identity.EntityFrameworkCore 8.0.11, Pomelo MySQL 8.0.3, MySqlConnector 2.3.7 (`Camion.Api.csproj:9-12`).
- Todo el código HTTP está en **minimal APIs dentro de `Program.cs`**; no hay controladores, servicios ni capas separadas.
- `Contracts/AuthContracts.cs`, `Models/ApplicationUser.cs`, `Models/PerfilComprador.cs`, `Data/CamionDbContext.cs`.
- No hay carpeta `Migrations/` (no existen migraciones EF).
- Secretos: se leen de configuración `ConnectionStrings:CamionDatabase` y `Jwt:Key` (`Program.cs:15-18`); la clave JWT debe tener ≥ 32 bytes (`Program.cs:19-20`). `UserSecretsId` = `camion-api-local-development` (`Camion.Api.csproj:6`). `appsettings.json` versionado solo contiene las claves `Logging`, `LogLevel`, `Default`, `AllowedHosts` (sin secretos). No existe `appsettings.Development.json` en el repositorio. No se consultó `dotnet user-secrets` (contiene valores): **NO VERIFICABLE** si los secretos locales están configurados.

### 2.2 Identity y JWT

- `AddIdentityCore<ApplicationUser>` con correo único, contraseña ≥ 8 con dígito, minúscula y mayúscula (sin símbolo obligatorio), bloqueo de 15 min tras 5 intentos, también para usuarios nuevos (`Program.cs:24-39`).
- Roles con `IdentityRole` (`Program.cs:36`). El único rol que el código crea es `COMPRADOR`, y lo crea "al vuelo" en el primer registro (`Program.cs:102-107`). **No existen los roles ASESOR ni ADMINISTRADOR, ni siembra de un usuario administrador.**
- JWT HS256, emisor `Camion.Api`, audiencia `Camion.Frontend`, validación de vida útil con 30 s de tolerancia (`Program.cs:41-55`); duración 2 horas (`Program.cs:148`). Claims: `sub`, `NameIdentifier`, `Name`, `jti`, `session_version` y roles (`Program.cs:149-157`).
- `AddAuthorization()` sin políticas (`Program.cs:56`). **Ningún endpoint exige un rol concreto.**
- No hay CORS, HTTPS/HSTS, Swagger, limitación de peticiones ni manejador global de errores (no aparece en `Program.cs:14-60`). `launchSettings.json:6` solo expone `http://localhost:5080`.

### 2.3 Endpoints reales

| # | Método | Ruta | Autorización | Entrada (DTO) | Respuestas reales | Evidencia |
|---|---|---|---|---|---|---|
| 1 | GET | `/health` | Anónimo | — | 200 `{status, service}` | `Program.cs:62` |
| 2 | GET | `/health/database` | Anónimo | — | 200 `{status, database, tables}`; 503 si falla MariaDB | `Program.cs:63-79` |
| 3 | POST | `/api/auth/registro/comprador` | Anónimo | `RegistroCompradorRequest(NombreCompleto, Email, Telefono, Password)` (`Contracts/AuthContracts.cs:5-9`) | 201 `{usuarioId, mensaje}` con `Location: /api/auth/me`; 400 `{error}` (validación manual); 400 `{errors[]}` (reglas de contraseña de Identity, mensajes en inglés); 409 `{error}` correo duplicado; 500 ProblemDetails si falla rol | `Program.cs:81-134` |
| 4 | POST | `/api/auth/login` | Anónimo | `LoginRequest(Email, Password)` (`AuthContracts.cs:11-13`) | 200 `AuthResponse(AccessToken, TokenType, ExpiraEnUtc, UsuarioId, NombreCompleto, Email, Roles)` (`AuthContracts.cs:15-22`); 401 sin cuerpo (usuario inexistente, cuenta no ACTIVA, contraseña errónea **o cuenta bloqueada**, indistinguibles) | `Program.cs:136-170` |
| 5 | GET | `/api/auth/me` | Bearer JWT (cualquier rol) | — | 200 `{usuarioId, nombreCompleto, email, phoneNumber, roles}`; 401 si no hay token, usuario no ACTIVA o `session_version` distinta | `Program.cs:172-182` |

No existe ningún endpoint de escritura sobre vehículos, publicaciones, solicitudes ni usuarios ajenos. No existe endpoint de cierre de sesión, cambio o recuperación de contraseña.

### 2.4 Validaciones

- Los atributos `[Required]`, `[EmailAddress]`, `[MaxLength]` de `AuthContracts.cs:6-13` **no se ejecutan solos** en minimal APIs de .NET 8; la validación real es la manual de `Program.cs:87-95` (campos no vacíos, correo válido, nombre 2-150, teléfono ≤ 25, contraseña ≥ 8). Consecuencias:
  - No se valida la longitud máxima del correo (254 en el DTO, 256 en la columna `database/002_esquema_inicial_mariadb_10_4.sql:12`): un correo muy largo terminaría en error 500 de base de datos.
  - El teléfono no se valida en formato, solo en longitud (`Program.cs:93`), aunque la v3.0 pide validar formato de teléfono (`docs/requisitos/requisitos-funcionales-v3.0.md:1167`).
- El correo se normaliza a minúsculas en el registro (`Program.cs:97`).
- Registro dentro de transacción (usuario + rol + perfil) (`Program.cs:109-132`), pero sin captura de `DbUpdateException`: dos registros simultáneos con el mismo correo darían 500 en lugar de 409.
- `EstadoCuenta` por defecto `ACTIVA` (`Models/ApplicationUser.cs:8`); el login y `/me` solo aceptan `ACTIVA` (`Program.cs:143`, `Program.cs:177`).
- `SessionVersion` existe (`ApplicationUser.cs:11`) y se compara en `/me` (`Program.cs:178-179`), pero **ningún código la incrementa**: la revocación de sesiones no está operativa.

## 3. Base de datos

- Scripts: `database/001_esquema_inicial_mysql.sql` (MySQL 8) y `database/002_esquema_inicial_mariadb_10_4.sql` (MariaDB 10.4, XAMPP). Son idénticos salvo cabecera y collation (líneas 1 y 3). Cada uno crea **39 tablas**. No hay migraciones EF; el propio script advierte que no se cree una migración inicial de Identity sobre esas tablas (`002_…sql:466-468`).
- No se ejecutó ninguna consulta contra la base de datos (solo lectura de scripts).

### 3.1 Tablas mapeadas en `CamionDbContext`

| Tabla | Mapeo | ¿La usa el código? |
|---|---|---|
| AspNetUsers | `IdentityDbContext` + columnas propias (`Data/CamionDbContext.cs:17-29`) | Sí (registro, login, me) |
| AspNetRoles | `CamionDbContext.cs:30-35` | Sí (crea `COMPRADOR`) |
| AspNetUserRoles | `CamionDbContext.cs:36-40` | Sí |
| AspNetUserClaims, AspNetRoleClaims, AspNetUserLogins, AspNetUserTokens | `CamionDbContext.cs:41-54` | No (mapeadas, sin uso) |
| perfiles_comprador | `CamionDbContext.cs:55-64`, `DbSet` en `CamionDbContext.cs:12` | Sí (registro, `Program.cs:130`) |

### 3.2 Tablas del script NO mapeadas ni usadas por el código (31)

`perfiles_asesor`, `historial_autorizaciones`, `sesiones_usuario`, `planes`, `suscripciones`, `pagos`, `notificaciones_plan`, `marcas`, `lineas`, `tipos_vehiculo`, `tipos_carroceria`, `combustibles`, `departamentos`, `municipios`, `vehiculos`, `publicaciones`, `fotos_publicacion`, `historial_precios`, `historial_vigencias`, `publicaciones_destacadas`, `enlaces_compartidos`, `favoritos`, `solicitudes`, `detalle_solicitud_credito`, `detalle_busqueda_personalizada`, `asignaciones_solicitud`, `seguimiento_solicitud`, `alertas_busqueda`, `notificaciones`, `auditoria_eventos`, `configuracion_sistema` (`002_…sql:88-463`).

### 3.3 Incoherencias del esquema con decisiones vigentes

- **D-2 placa**: la tabla `vehiculos` no tiene columna de placa (`002_…sql:222-249`).
- **D-4 estados**: no coinciden con el esquema:
  - Vehículo: `vehiculos` no tiene estado; el estado comercial vive en `publicaciones.estado_comercial` = DISPONIBLE/RESERVADO/VENDIDO (`002_…sql:260`, `274`). D-4 pide Activo/Inactivo/Vendido.
  - Publicación: BORRADOR/PUBLICADA/OCULTA/ELIMINADA (`002_…sql:275`); no existe "Vencida" de la v3.0.
  - Crédito/solicitud: NUEVA/EN_GESTION/CERRADA/CANCELADA (`002_…sql:368`); D-4 pide Registrado/Asignado/Gestionado/Cerrado.
  - Suscripción: PENDIENTE/ACTIVA/VENCIDA/CANCELADA/RECHAZADA (`002_…sql:152`); faltan Suspendida y Reactivada (D-4).
  - Asesor: PENDIENTE/AUTORIZADO/DESAUTORIZADO (`002_…sql:102`); la v3.0 usa Pendiente/Aprobado/Rechazado/Bloqueado.
  - `AspNetUsers.EstadoCuenta` es texto libre sin restricción (`002_…sql:25`).
- **D-6 / RN-SE-005 (servicios sin cuenta)**: `solicitudes.comprador_usuario_id` es obligatorio (`002_…sql:347`) y `correo_contacto` también (`002_…sql:352`); además `tipo_solicitud` solo admite CONTACTO/CREDITO/BUSQUEDA (`002_…sql:367`). Hoy no se podría guardar un contacto anónimo, un aviso "Vende tu camión" ni "Seguro" sin cambiar el esquema.
- **D-7 asignación auditada**: la tabla `asignaciones_solicitud` con `asignado_por` y `motivo` (`002_…sql:392-405`) sí soporta la reasignación auditada, pero solo en esquema.

## 4. Frontend (`frontend/src`)

- Una sola pantalla en `App.tsx`, sin enrutador (no hay `react-router`; dependencias en `frontend/package.json:16-26`). Estados de vista: login / registro / perfil (`App.tsx:11`, `App.tsx:115-216`).
- Llamadas a la API (rutas relativas, vía proxy Vite a `http://localhost:5080`, `frontend/vite.config.ts:6-16`):
  - `POST /api/auth/registro/comprador` (`App.tsx:36-45`)
  - `POST /api/auth/login` (`App.tsx:63-67`)
  - `GET /api/auth/me` con `Authorization: Bearer` (`App.tsx:75-79`)
- **Token/sesión**: el token no se guarda en ningún sitio; se usa una sola vez para pedir `/me` (`App.tsx:73-86`). Al recargar la página se pierde la sesión. "Cerrar sesión" solo limpia el estado local (`App.tsx:99-107`); no avisa al backend y el token sigue siendo válido hasta 2 h.
- **Errores**: solo distingue 409 en el registro (`App.tsx:48-50`); cualquier otro error muestra un mensaje genérico (`App.tsx:52-54`, `App.tsx:70`) e ignora el cuerpo de la respuesta. El formulario solo exige 8 caracteres de contraseña (`App.tsx:177`), pero el backend también exige mayúscula, minúscula y dígito (`Program.cs:28-30`): el usuario recibe "Revisa los datos" sin saber por qué. Un bloqueo por intentos fallidos se muestra igual que una contraseña incorrecta.
- `frontend/.env.example` declara `VITE_API_BASE_URL`, pero el código no la usa.
- No hay catálogo, ni páginas públicas, ni avisos, ni paneles de asesor/administrador.

## 5. Pruebas y CI

- **No hay proyectos de prueba** (ningún archivo versionado con "test", ningún `.sln`).
- `frontend/package.json:6-10` solo tiene `dev`, `build`, `preview` (sin `test` ni `lint`).
- **No hay CI**: no existe `.github/`.
- La minuta reporta pruebas **manuales** de registro, login y `/me` (`docs/requisitos/minuta-transferencia-agentes-ia.md:48-49`) y reconoce lo no probado (`minuta…md:52`). Nada está PROBADO de forma automatizada.

## 6. Compilaciones (ejecutadas en esta auditoría)

| Componente | Comando | Resultado |
|---|---|---|
| Backend | `dotnet build --no-restore` en `backend/Camion.Api` | **Compilación correcta. 0 advertencias, 0 errores.** 13,5 s. SDKs instalados 8.0.425 y 10.0.401; sin `global.json`, se usó el SDK 10.0.401 apuntando a `net8.0`. No hizo falta restaurar paquetes. |
| Frontend | `npm run build` (`tsc -b && vite build`) en `frontend/` | **Correcto.** Vite 7.3.7, 29 módulos, `dist/` generado (JS 226 kB / 70,6 kB gzip), 1,27 s. Node 25.8.1, npm 11.11.0. `node_modules` ya existía; no se instaló nada. Efecto secundario: se crearon `frontend/tsconfig.*.tsbuildinfo` sin seguimiento. |

No se arrancó el backend ni se probó contra la base de datos.

## 7. Cobertura por módulo (M01–M14 = EP-01–EP-14 de la v3.0) y HU tocadas

Numeración oficial: matriz de 87 HU (sección 7) por D-1 (`docs/requisitos/requisitos-funcionales-v3.0.md:131-144`, `145-595`).

| Módulo | HU | Estado | Evidencia |
|---|---|---|---|
| M01 Portal Web | HU-001 a HU-012 | NO INICIADO | No hay endpoints de catálogo ni pantallas públicas (`Program.cs:62-182`, `App.tsx`) |
| M02 Vehículos | HU-013 a HU-022 | NO INICIADO | Tabla `vehiculos` solo en script (`002_…sql:222`); sin mapeo |
| M03 Publicaciones | HU-023 a HU-028 | NO INICIADO | Tabla `publicaciones` solo en script (`002_…sql:251`) |
| M04 Leads | HU-029 a HU-033 | NO INICIADO | Tabla `solicitudes` solo en script (`002_…sql:345`) |
| M05 Búsquedas | HU-034 a HU-038 | NO INICIADO | `detalle_busqueda_personalizada` solo en script (`002_…sql:378`) |
| M06 Créditos | HU-039 a HU-042 | NO INICIADO | `detalle_solicitud_credito` solo en script (`002_…sql:371`) |
| M07 Administración | HU-043 a HU-051 | NO INICIADO | No hay rol administrador ni endpoints de aprobación/planes; `perfiles_asesor`, `planes` solo en script |
| M08 Marketing | HU-052 a HU-055 | NO INICIADO | No hay tabla ni código de contenido |
| M09 Favoritos | HU-056 a HU-058 | NO INICIADO | `favoritos` solo en script (`002_…sql:335`) |
| M10 Seguridad | HU-059 Iniciar Sesión | **IMPLEMENTADO** (prueba manual reportada; sin pruebas automáticas) | `Program.cs:136-170`, `App.tsx:63-86`; `minuta…md:48-49` |
| M10 Seguridad | HU-060 Cierre automático de sesión | PARCIAL / IMPLEMENTADO solo como expiración fija de 2 h del token; no por inactividad; el frontend no gestiona la expiración | `Program.cs:148`, `Program.cs:52` |
| M10 Seguridad | HU-061 Registrar auditoría | NO INICIADO | `auditoria_eventos` solo en script (`002_…sql:444`) |
| M10 Seguridad | HU-062 Proteger datos personales | PARCIAL (contraseñas con hash de Identity; `/me` solo devuelve datos propios) | `Program.cs:118`, `Program.cs:174-181` |
| M10 Seguridad | HU-063 Recuperar contraseña | NO INICIADO | Sin endpoint |
| M11 Gestión comercial avanzada | HU-064 a HU-069 | NO INICIADO | — |
| M12 Notificaciones | HU-070 a HU-074 | NO INICIADO | `notificaciones` solo en script |
| M13 Reportes | HU-075 a HU-080 | NO INICIADO | — |
| M14 Parametrización | HU-081 a HU-087 | NO INICIADO | `configuracion_sistema`, catálogos solo en script |
| Fuera de matriz | Registro de comprador | **IMPLEMENTADO** (prueba manual reportada) | `Program.cs:81-134`, `App.tsx:35-60` |
| Nuevo (D-6) | HU-N01 Vende tu camión / HU-N02 Seguro | NO INICIADO (el esquema actual no lo soporta, ver 3.3) | — |

Nota: la matriz oficial de 87 HU **no tiene una HU de "Registrar usuario/comprador"**; esa HU solo aparece en la lista de 90 HU de la sección 10, que D-1 descartó como numeración. Ver "Ambigüedades".

## 8. Contraste con decisiones D-1 a D-7 y CLAUDE.md

| Regla | Estado | Evidencia |
|---|---|---|
| (a) Registro de comprador con acceso inmediato, sin aprobación (D-3) | **CUMPLE** | `EstadoCuenta` = `ACTIVA` por defecto (`Models/ApplicationUser.cs:8`); el login solo exige `ACTIVA` (`Program.cs:143`). |
| (b) Comprador de solo lectura (D-3) | **CUMPLE hoy, por ausencia de funciones** | Se asigna siempre el rol fijo `COMPRADOR` (`Program.cs:124`); el DTO no tiene campo de rol (`AuthContracts.cs:5-9`), así que el registro público **no puede elegir** asesor/admin. No existe ningún endpoint de escritura salvo el propio registro y el login. Riesgo futuro: no hay políticas de autorización por rol (`Program.cs:56`); cuando se añadan endpoints de escritura habrá que proteger cada uno. |
| Registro opcional; sin cuenta se consulta el catálogo y se contacta (D-3, RN-SE-005) | NO APLICA AÚN (no hay catálogo ni contacto) / el esquema **NO CUMPLE** | `solicitudes.comprador_usuario_id NOT NULL` (`002_…sql:347`). |
| (c) Registro de asesor en estado Pendiente y aprobación del administrador (D-3) | **NO APLICA AÚN** (no implementado) | Sin endpoint ni rol; solo la tabla `perfiles_asesor` con `PENDIENTE` por defecto (`002_…sql:94`). |
| (d) Placa opcional y única (D-2) | **NO CUMPLE** el esquema; código NO APLICA AÚN | Sin columna placa en `vehiculos` (`002_…sql:222-249`). |
| (e) Estados D-4 | **NO CUMPLE** el esquema; código NO APLICA AÚN | Ver 3.3 (`002_…sql:152`, `260`, `275`, `368`). |
| (f) Asignación de servicios al administrador y reasignación auditada (D-7) | NO APLICA AÚN | Sin código; el esquema permite responsable y auditoría de reasignación (`002_…sql:356`, `392-405`). |
| (g) Avisos HU-N01 / HU-N02 sin cuenta (D-6) | **NO APLICA AÚN**; el esquema **NO CUMPLE** | `tipo_solicitud` sin VENTA/SEGURO (`002_…sql:367`); usuario y correo obligatorios (`002_…sql:347`, `352`). |
| (h) Contraseña elegida por el usuario (D-5) | **CUMPLE** | `users.CreateAsync(user, request.Password)` (`Program.cs:118`); no se envían credenciales. |
| D-1 numeración de 87 HU | NO VERIFICABLE en código (los commits de auth no citan HU) | `git log` (`2c98a22`…`eb1a733`). |
| CLAUDE.md: secretos fuera de Git | **CUMPLE** | `.gitignore:9-14`; `appsettings.json` sin secretos; ningún archivo de secretos en el historial. |
| CLAUDE.md: cambios de esquema con migración reversible | NO APLICA AÚN / riesgo | No hay sistema de migraciones; el esquema solo vive en scripts de creación completos. |
| CLAUDE.md: asesor solo atiende contactos de sus vehículos con plan vigente | NO APLICA AÚN | Sin código de leads. |

## 9. Diferencias entre documentación y código

| Documento | Lo que dice | Realidad |
|---|---|---|
| `README.md:5`, `README.md:51` | "Solo base técnica"; "No hay registro/inicio de sesión… ni base de datos creada" | Hay registro, login, JWT, Identity y EF Core contra MariaDB (`Program.cs:22-182`). |
| `README.md:11`, `README.md:29` | MySQL "a integrar"; "no se necesita" | El backend no arranca sin `ConnectionStrings:CamionDatabase` y `Jwt:Key` (`Program.cs:15-20`); se usa MariaDB 10.4 (`Program.cs:23`). |
| `README.md:47` | "Expone únicamente `GET /health`" | Expone 5 endpoints (sección 2.3). |
| `README.md:18-19` | Frontend vacío; API sin endpoints | Ver secciones 2 y 4. |
| `docs/ALCANCE-INICIAL.md:13-18` | Ninguna tabla, conexión ni autenticación | Desactualizado (mismo motivo). |
| `backend/GUIA-CONEXION-MARIADB.md:37` | "No se han configurado todavía EF Core ni Identity" | Sí están configurados (`Program.cs:22-39`). |
| `backend/GUIA-AUTENTICACION.md:41` | "No se ha compilado ni probado el código" | Compila (sección 6) y la minuta reporta prueba manual (`minuta…md:48-49`). |
| `backend/GUIA-AUTENTICACION.md:7`, `GUIA-CONEXION-MARIADB.md:8` | Ruta `C:\Users\alex0\Documents\CAMION` | El repositorio está en `C:\Users\alex0\OneDrive\Documentos\proyecto venta de camiones\CAMION`. |
| `backend/GUIA-AUTENTICACION.md:28-30` | Ejemplo de contraseña `PruebaSegura123` | Válida para las reglas actuales; es un dato de ejemplo, no un secreto. |
| `docs/requisitos/minuta…md:113` | "Placa única" | Sustituido por D-2 (opcional y única); el esquema no tiene placa. |
| `requisitos-funcionales-v3.0.md:807` | RN-US-001 "Todo usuario debe ser aprobado" | Sustituido por D-3 (solo asesores); el código cumple D-3 para compradores. |
| `frontend/.env.example` | Variable `VITE_API_BASE_URL` | No se usa en el código (`App.tsx` usa rutas relativas). |

## 10. Riesgos detectados

**Seguridad**
1. No hay autorización por roles ni políticas (`Program.cs:56`); los futuros endpoints de escritura deben protegerse uno a uno (riesgo para la regla "comprador solo lectura").
2. Revocación de sesión inoperante: `SessionVersion` nunca se incrementa y solo `/me` la comprueba (`Program.cs:178-179`); "Cerrar sesión" no invalida el token (`App.tsx:99-107`).
3. Sin limitación de peticiones en registro/login (solo bloqueo de cuenta, `Program.cs:32-34`); la guía lo reconoce como pendiente (`GUIA-AUTENTICACION.md:39`).
4. Sin confirmación de correo (`EmailConfirmed` nunca se usa); cualquiera puede registrar correos ajenos.
5. `/health/database` es anónimo y revela nombre de la base y número de tablas (`Program.cs:63-73`). Menor, pero conviene restringirlo fuera de desarrollo.
6. Sin HTTPS/HSTS ni CORS configurados (`Program.cs:57-60`, `launchSettings.json:6`): aceptable en local con proxy Vite, no para producción.
7. La guía documenta conexión con usuario `root` sin contraseña para XAMPP (`GUIA-CONEXION-MARIADB.md:15-18`): aceptable solo en local.
8. No se detectaron secretos versionados (solo nombres de claves).

**Datos**
9. Esquema sin migraciones y en conflicto con D-2, D-4 y D-6 (sección 3.3): corregirlo exigirá migraciones reversibles y respaldo previo.
10. Dos scripts SQL casi idénticos (`001` y `002`) que pueden divergir.
11. Correo > 256 caracteres o carrera de registros duplicados terminan en 500 (sección 2.4).

**Deuda técnica**
12. Todo el backend en un solo `Program.cs`; DataAnnotations decorativos (no se ejecutan).
13. Cero pruebas automáticas y sin CI.
14. Frontend de un solo componente; sin persistencia de sesión, sin enrutador, mensajes de error genéricos y reglas de contraseña no mostradas.
15. Sin `global.json`: el build usa el SDK 10 aunque el objetivo es .NET 8 (funciona, pero no es reproducible entre equipos).
16. `*.tsbuildinfo` no está en `.gitignore`; podrían commitearse por error.

## 11. Ambigüedades de negocio detectadas (para `DECISIONES.md`)

1. **¿Qué HU oficial corresponde al registro de comprador?** La matriz de 87 HU no tiene "Registrar usuario" (la sección 10 sí, como HU-043, pero D-1 la descartó). Hace falta decidir qué HU/RN citar (p. ej., una HU nueva tipo HU-N03, o ligarla a HU-059/RN-SE-001).
2. **Contacto sin cuenta**: D-3 y RN-SE-005 permiten contactar sin cuenta, pero el esquema exige usuario y correo en `solicitudes`. Confirmar qué datos mínimos se piden a un visitante anónimo (¿correo obligatorio o solo teléfono, como en HU-N01?).
3. **Estados del asesor**: el esquema usa AUTORIZADO/DESAUTORIZADO; la v3.0 usa Aprobado/Rechazado/Bloqueado. Confirmar el conjunto definitivo (D-4 no cubrió usuarios).
4. **Estado "Reservado"** de publicación existe en el esquema pero no en la v3.0 ni en D-4: ¿se mantiene?

## Actualización P5 — 2026-11-04 — commit ae12c0b (rama feature/diseno-portal)

> **Corrección de estado (2026-11-06, documentador).** Esta sección describe el código **antes** de construir P5 (commit `ae12c0b`) y por eso dice "NO INICIADO". Ya no es el estado actual. Estado vigente de P5 en `feature/p5-leads` (fuente: `BITACORA.md`, sección "2026-11-04 — P5 Leads y contactos", y `docs/criterios/P5-evidencia.md`):
> - **IMPLEMENTADO y PROBADO**: contacto desde la ficha del vehículo con autorización de datos (D-44), botón de WhatsApp (D-47, D-49), límites anti-abuso, asignación al asesor dueño con plan vigente o a Administración (D-7), "Mis contactos" y "Contactos" con notas, cierre, reapertura y reasignación, y barrido D-46. Pruebas ejecutadas: `dotnet test` 962/962, `npx vitest run` 528/528 (531/531 tras el ajuste de Planes), `tsc -b` y `npm run build` correctos.
> - **Migración 013 APLICADA en `camion_ventas` el 2026-11-05**, con respaldo previo `C:\Respaldo_CAMION\camion_ventas_antes_013_p5_20261105_1023.sql`.
> - **Prueba manual PROBADA por la responsable el 2026-11-05** ("Todo bien").
> - Sin E2E. Pendientes: INF-2 y la revisión legal (D-44).
> - Las subsecciones P5.1 a P5.7 se conservan como registro histórico de la auditoría; no se reescribieron.

Esta es una auditoría solo para P5 (Leads y contactos).

- **Commit auditado:** `ae12c0b3673d3319852025a7795934cc466e6b89`, el HEAD de `feature/diseno-portal`.
- **Ramas:** `feature/diseno-portal` está apilada sobre `feature/p4b-publicaciones`, que está sobre `feature/p4p-planes`, que está sobre `feature/p4a-inventario`. `main` está en `de4c7fb`.
- **Límites:** solo lectura. No me conecté a la base, no leí secretos y no revisé `frontend/respaldos/`.
- **Secciones anteriores:** las secciones 1 a 11 siguen describiendo el commit `4e41f43` (P0). No se reescribieron.
- **Cambios de otro agente:** al auditar, `docs/coordinacion/DECISIONES.md` estaba modificado (añade D-46, sin commit) y `docs/criterios/criterios-P5-leads.md` no estaba en git. Otro agente los escribe en paralelo.

### P5.1 ¿Existe algo de leads, contactos o solicitudes?

**No hay nada en el código: ni modelo, ni DbSet, ni endpoint, ni pantalla, ni prueba.** Estado de P5: **NO INICIADO**.

- **Modelos.** No hay ninguna clase `Solicitud`, `Lead` ni `Contacto` en `backend/Camion.Api/Models/`.
- **La palabra "solicitud" en el backend.** Solo aparece en los textos del registro de asesor (`Endpoints/AuthEndpoints.cs:27`, `:180`) y en `MensajesError.FormatoInvalido` (`Contracts/AuthContracts.cs:59`).
- **La palabra "contacto".** Solo aparece en el origen de venta `CONTACTO_ASESOR` (`Models/VentaVehiculo.cs:8`, `Data/CamionDbContext.cs:421`). Es un dato de la venta (P4b), no un lead.
- **DbContext.** Mapea 23 DbSet (`Data/CamionDbContext.cs:12-42`). Ninguno corresponde a estas tablas: `solicitudes`, `asignaciones_solicitud`, `seguimiento_solicitud`, `detalle_solicitud_credito`, `detalle_busqueda_personalizada`, `enlaces_compartidos`, `favoritos`, `notificaciones`.
- **Frontend.**
  - El detalle del vehículo dice "ni datos del asesor (el contacto llega en P5)" (`frontend/src/components/DetalleVehiculo.tsx:46-49`).
  - En la portada, "Quiero un crédito" lleva al registro (`frontend/src/components/Inicio.tsx:165-168`). El comentario dice "La solicitud de crédito llega en P5".
- **Pruebas.** No hay ninguna prueba de leads en `backend/Camion.Api.Tests/` ni en `frontend/tests/`.

**Tablas previstas en `database/002_esquema_inicial_mariadb_10_4.sql`.** Existen en el script, pero ninguna está mapeada en EF.

- **`solicitudes`** (`:345-370`)
  - **Choca con D-9.** Exige usuario registrado: `comprador_usuario_id VARCHAR(255) NOT NULL`, con FK a AspNetUsers (`:347`, `:363`). También exige correo: `correo_contacto NOT NULL` (`:352`). D-9 dice que el contacto sin cuenta pide nombre y teléfono, y el correo es opcional.
  - `clave_idempotencia` es `NOT NULL UNIQUE` (`:357`).
  - `CK_Sol_Tipo` solo admite `CONTACTO`, `CREDITO` y `BUSQUEDA` (`:367`). No incluye los avisos "Vende tu camión" ni "Seguro" de P5b (D-6).
  - Estados posibles: `NUEVA`, `EN_GESTION`, `CERRADA`, `CANCELADA` (`:368`).
  - `CK_Sol_Contacto` exige `publicacion_id` cuando el tipo es CONTACTO (`:369`).
  - Ya tiene el campo del responsable: `responsable_usuario_id`, FK a AspNetUsers con ON DELETE SET NULL (`:355`, `:365`), y el índice `IX_Sol_ResponsableEstado` (`:360`).
- **`asignaciones_solicitud`** (`:392-405`). Guarda el responsable anterior, el nuevo, `asignado_por` y `motivo NOT NULL`. Sirve para auditar la reasignación de D-7.
- **`seguimiento_solicitud`** (`:406-417`). Guarda cada cambio de estado con una observación.
- **`detalle_solicitud_credito`** (`:371`) y **`detalle_busqueda_personalizada`** (`:378`). Son de P6.
- **Ojo con los identificadores.**
  - `solicitudes` guarda el responsable como **id de usuario** (AspNetUsers.Id, texto).
  - Vehículos, publicaciones y suscripciones usan **`perfiles_asesor.id`** (número).
  - Para unirlos hay que pasar por `PerfilAsesor.UsuarioId` (`Models/PerfilAsesor.cs:13`).
- **Base real.** No sabemos si estas tablas existen en la base real, porque no se consultó (regla de solo lectura).

**Migraciones** (`database/migraciones/`)

- Existen las 003, 004, 006, 009, 011 y 012. Desde la 006, cada una tiene su archivo `_SUBIR`.
- Los números 005, 007, 008 y 010 son scripts de datos que están en `database/catalogos/`.
- Ninguna otra rama usa números nuevos (`git log --all -- database/migraciones`).
- **El siguiente número libre es 013.**
- No hay migraciones EF (`dotnet ef`). Todos los cambios de esquema son scripts SQL manuales.

### P5.2 Quién es el asesor dueño y si su plan está vigente (D-7)

**Dueño del vehículo y de la publicación**

- Está en `Vehiculo.AsesorId` (`Models/Vehiculo.cs:16`) y en `Publicacion.AsesorId` (`Models/Publicacion.cs:17`).
- Los dos apuntan a `perfiles_asesor.id`, con FK Restrict (`Data/CamionDbContext.cs:272`, `:324`, `:352`).
- Cuando el administrador reasigna el vehículo (D-34), cambian los dos a la vez (`Endpoints/VentasEndpoints.cs:188-194`).
- Por eso, para saber el dueño al momento del contacto, basta con leer `Publicacion.AsesorId`.

**Plan vigente.** La regla está en un solo lugar y ya se puede reutilizar:

- `DatosP4b.SuscripcionesVigentes(db, ahora)` (`Publicaciones/DatosP4b.cs:15-19`): suscripción ACTIVA o REACTIVADA, con inicio <= ahora < fin.
- `DatosP4b.SuscripcionVigenteAsync(db, asesorId, ahora, ct)` (`:22-25`): la suscripción vigente de un asesor.
- `ReglasP4p.Vigente` (`Suscripciones/ReglasP4p.cs:52-53`): la misma regla, evaluada en memoria.

**Asesor activo.** `InventarioEndpoints.AsesorActivoAsync` (`Endpoints/InventarioEndpoints.cs:682`). Es `internal`.

**Publicación visible.**

- La regla está en `ConsultasCatalogo.Visibles(db, ahora)` (`Catalogo/ConsultasCatalogo.cs:23-37`).
- Exige cuatro cosas: plan vigente del dueño, cuenta ACTIVA, vehículo ACTIVO o RESERVADO, y publicación PUBLICADA.
- El detalle por slug (`Endpoints/CatalogoEndpoints.cs:147`) usa esa misma consulta.

**Conclusión.** Un lead creado sobre una publicación visible casi siempre tendrá un dueño con plan vigente. Lo que falta decidir es qué pasa después de crearlo (D-46, PENDIENTE; ver P5.7).

### P5.3 Detalle público del vehículo (frontend)

- **Dónde está.** La ruta es `#/vehiculo/<slug>` (`frontend/src/rutas.ts:4`, `:21`). La pantalla es `DetalleVehiculo` (`App.tsx:298`, `components/DetalleVehiculo.tsx:50`).
- **Hoy no muestra ningún contacto.**
  - El backend tampoco lo envía. El DTO `DetalleVehiculo` (`Contracts/CatalogoContracts.cs:26-49`) no trae teléfono, WhatsApp ni datos del asesor.
  - La regla está escrita en `CatalogoContracts.cs:4`: el catálogo no entrega datos del asesor.
- **Teléfono del asesor.** Solo existe como `AspNetUsers.PhoneNumber`, que se guarda al registrarse (`Endpoints/AuthEndpoints.cs:132`). No se muestra en el catálogo.
- **Contacto visible hoy.** El único es el del administrador, en el pie del portal: WhatsApp 316 615 2907 y el correo (`components/PiePortal.tsx:10-13`, `:63-72`).
- **Dónde poner el botón o formulario de contacto.** En la columna `cat-detalle-info`, justo después del precio y las etiquetas (`DetalleVehiculo.tsx:106-116`) y antes de la "Ficha técnica" (`:118`).

### P5.4 Patrones que P5 puede reutilizar

**Auditoría**

- `Auditoria.AgregarEvento(db, actorId, entidad, entidadId, accion, anteriores, nuevos, ahora)` (`Auth/Auditoria.cs:72-85`). Agrega la fila al contexto, y se guarda en la misma transacción que el cambio auditado.
- Ya existe la acción `Reasignar = "REASIGNAR"` (`:49`).
- Falta una constante de entidad para las solicitudes (p. ej. `EntidadSolicitud`). Las actuales están en `:10`, `:19`, `:28-30` y `:42-43`.
- En un contacto anónimo, `actorId` puede ser null, porque `UsuarioId` admite nulos (`Auth/Auditoria.cs:52`).

**Validación de teléfono**

- Ya existe, pero es **privada**: está dentro de `ValidarDatosBasicos` (`Auth/ValidacionAuth.cs:83-102`).
- Reglas: de 7 a 25 caracteres, solo dígitos, espacio y `+ - ( )`, y al menos 7 dígitos.
- Usa la expresión `FormatoTelefono()` (`:30-31`, privada) y el mensaje público `MensajeTelefono` (`:12`).
- Para reutilizarla hay que sacarla a un método público. Es un cambio pequeño.
- La validación del nombre pide de 2 a 150 caracteres (`:92-94`).

**Limitación de peticiones (rate limiting): no existe**

- No hay `AddRateLimiter` ni `UseRateLimiter` (`Program.cs:25-90`, `:152`).
- Lo único parecido es el bloqueo de Identity tras varios intentos fallidos de login (`Program.cs:35-37`, `AuthEndpoints.cs:204`).
- .NET 8 ya trae `Microsoft.AspNetCore.RateLimiting`, así que no hace falta instalar un paquete.
- Sin límite, un formulario de contacto anónimo queda expuesto a spam.

**Cómo se registran los endpoints**

- Los anónimos se marcan con `.AllowAnonymous()`, en grupo (`Endpoints/CatalogoEndpoints.cs:40`) o en una sola ruta (`PortalEndpoints.cs:29`, `MediaEndpoints.cs:26`).
- Los que exigen sesión usan `.RequireAuthorization(Roles.Politica…)`. Hay 4 políticas (`Program.cs:74-80`).
- Todos se registran en `Program.cs:173-185`.

**Prueba `SesionTests.Inventario_de_endpoints`** (`backend/Camion.Api.Tests/SesionTests.cs:186-300`). Cada ruta nueva obliga a actualizarla, porque comprueba:

- La lista exacta de rutas anónimas (`:193-199`).
- Que todo `/api/catalogo` sea GET y que sean exactamente 5 rutas (`:202-210`). Por eso el POST de contacto **no debe** ir bajo `/api/catalogo`; conviene un prefijo nuevo, p. ej. `/api/contactos`.
- La lista exacta de rutas que modifican datos (`:219-251`).
- Que `/api/admin` tenga exactamente 31 rutas, todas con la política SoloAdministrador (`:255-262`).
- Que ninguna ruta contenga "auditoria" (`:299`).

**Panel del administrador**

- Las secciones se definen en tres sitios:
  - `SeccionAdmin` y `SECCIONES_ADMIN` (`frontend/src/rutas.ts:29-30`);
  - los nombres y el orden en `NavAdmin` (`components/NavAdmin.tsx:5-13`);
  - qué se pinta en cada sección, en `App.tsx:482-495`.
- Hoy son cinco: usuarios, planes, suscripciones, catálogos y portada.
- Una sección "Contactos" o "Solicitudes" se añade en esos tres sitios, con la ruta `#/cuenta/<seccion>`.

**Panel del asesor**

- No tiene navegación propia.
- Desde "Mi perfil" (`components/PerfilPanel.tsx:36-40`, `:66`) se llega a Inventario (`#/inventario`), a "Mis publicaciones" (`#/publicaciones`, `App.tsx:346`) y a "Mi suscripción".
- Para "Mis contactos" hay que añadir una ruta en `rutas.ts:18-26` y un acceso en `PerfilPanel`.

**Errores en el frontend**

- `ApiError` trae el código (`status`) y un `mensaje` listo para mostrar (`frontend/src/api/http.ts:22-41`).
- El 403 usa un mensaje fijo (`:88-94`).

### P5.5 Pruebas y compilación (ejecutadas en esta auditoría)

| Comando | Resultado |
|---|---|
| `dotnet test Camion.Api.Tests --artifacts-path <scratchpad>` | **876/876 correctas**, 0 fallos, 52 s. |
| Conteo en el código del backend | 277 `[Fact]` y 80 `[Theory]` (con 424 `InlineData`). |
| `npx vitest run` (2 corridas) | **453 de 454: falla 1, la misma en las dos corridas.** |
| `npx vitest run tests/InventarioLista.test.tsx` (sola) | Falla igual (24 de 25). No depende de la carga de la máquina. |
| `npx tsc -p tsconfig.app.json --noEmit` | Sin errores. |
| `npm run build` | No se ejecutó, para no escribir en `dist/`. |

**Hay una prueba del frontend que falla y conviene corregirla antes de P5**

- **Qué falla.** La prueba "25 vehículos: página 1 de 2…" (`frontend/tests/InventarioLista.test.tsx:212-223`). Espera 20 títulos `h3` en la página y encuentra 21.
- **Causa probable.** El `<h3>Horario de atención</h3>` del pie del portal (`frontend/src/components/PiePortal.tsx:75`), añadido en el commit `8212c63`.
- **La bitácora ya no coincide.** `docs/coordinacion/BITACORA.md:323` dice 453/453, pero es de antes de ese commit. En HEAD ya no se cumple.
- **Cómo arreglarlo.** Que la prueba busque los `h3` solo dentro del listado, o cambiar el nivel del título del pie.

### P5.6 Riesgos para P5

1. **El esquema de `solicitudes` contradice D-9.** Exige usuario y correo.
   - Hace falta la migración **013**, reversible y con respaldo previo, que vuelva opcionales esos dos campos.
   - Si P5b usa la misma tabla, la 013 también debe ampliar `CK_Sol_Tipo`.
   - Tampoco sabemos si la tabla existe en la base real.
2. **No hay limitación de peticiones.** El contacto sería el primer endpoint anónimo que escribe datos de negocio; hoy solo el login y los registros escriben sin sesión. Sin límite, queda expuesto a spam.
3. **Hay que decidir quién genera `clave_idempotencia`** (el navegador o el servidor). Como es `NOT NULL UNIQUE`, esa clave es la que evita leads duplicados por doble clic.
4. **Mostrar el teléfono del asesor en el detalle** rompería la regla de `CatalogoContracts.cs:4` y expondría datos personales. Hace falta una decisión explícita.
5. **Hay una prueba del frontend fallando en HEAD** (ver P5.5).

### P5.7 Ambigüedades de negocio

- **D-46 (PENDIENTE, aún sin commit).** ¿Qué pasa con los leads abiertos si el asesor pierde el plan, es bloqueado o deja de ser dueño del vehículo?
  - Bloquea CA-P5-50 a CA-P5-53.
  - D-46 cita `docs/criterios/criterios-P5-leads.md`, pero el archivo está en `docs/criterios/criterios-P5-leads.md`.
- **Sin decisión registrada:**
  - ¿El detalle público muestra el WhatsApp del asesor dueño, o solo un formulario que crea el lead (quizá junto al WhatsApp del administrador que ya está en el pie)?
  - ¿El contacto se envía solo con el formulario, o también abre un enlace `wa.me`?
- **P5b (D-6).** ¿Los avisos "Vende tu camión" y "Seguro" van en la misma tabla `solicitudes`, con tipos nuevos, o en tablas propias? La respuesta cambia el diseño de la migración 013.

## Actualización P5b — 2026-11-06 — commit 4c871ef (rama feature/p5b-avisos)

> **Corrección de estado (2026-11-12, documentador).** Esta sección se escribió el 2026-11-06, antes de aplicar la migración y de la prueba manual. Estado vigente (fuente: `BITACORA.md`, sección "2026-11-06 — P5b Avisos"):
> - **Migración 014 APLICADA en `camion_ventas` el 2026-11-09**, con la API detenida y respaldo previo `C:\Respaldo_CAMION\camion_ventas_antes_014_p5b_20261109_1406.sql`; verificación de solo lectura correcta.
> - **Prueba manual PROBADA por la responsable el 2026-11-10** ("Todo bien"). Notas, cierres y reaperturas de avisos no se ejercieron a mano; los cubren las pruebas automáticas.
> - Ajuste posterior (commit `12dea7e`): tras enviar, el formulario se oculta y ofrece tres salidas; `npx vitest run` 577/577.
> - Sigue sin E2E. Pendientes: INF-2 y la revisión legal (D-44).
> - Por eso, lo que dicen P5b.1 ("NO APLICADA"), P5b.3 y P5b.4 ya no es el estado actual; se conserva como registro histórico.

Redactado por el documentador a partir de lo verificado por los demás agentes: `docs/criterios/criterios-P5b-avisos.md`, `docs/criterios/P5b-contrato-api.md`, `docs/criterios/P5b-evidencia.md`, `docs/coordinacion/REVISION-SEGURIDAD-P5b.md` y D-50 a D-53. **No es una auditoría nueva del `auditor-codigo`.**

- **Rama:** `feature/p5b-avisos`, apilada sobre `feature/p5-leads` (`b057c1d`). 15 commits hasta `4c871ef`.
- **HU:** HU-N01 ("Vende tu camión") y HU-N02 ("Compra tu seguro todo riesgo"), definidas en D-6.

### P5b.1 Qué está IMPLEMENTADO y PROBADO (pruebas automáticas)

- **Modelo:** un aviso es un lead sin publicación, con `origen` (`VENDE_TU_CAMION` o `SEGURO_TODO_RIESGO`) y, en el seguro, `tipo_vehiculo` (`CAMION`, `CARRO`, `MAQUINARIA`, `OTRO`; D-50 a B). Reutiliza `leads`, `eventos_lead` y `consentimientos_datos` de P5.
- **API:** `POST /api/avisos/vende-tu-camion` y `POST /api/avisos/seguro-todo-riesgo`, anónimas, con límite por IP compartido con el contacto de P5, límite por teléfono compartido, campo trampa, tope de 8 KB y avisos repetidos agregados al abierto del mismo origen y teléfono.
- **Validaciones:** nombre de 2 a 150; teléfono colombiano celular o fijo (regla de P5, D-50 c A); correo opcional; descripción obligatoria de hasta 500 caracteres; tipo de vehículo en lista cerrada; casilla de autorización obligatoria con prueba guardada en la misma transacción (D-44). Sin ciudad, marca ni año (D-50 b A).
- **Asignación:** siempre a Administración (D-6, D-7). No se reasignan a un asesor: 409 en la API y `CK_Lead_AvisoAdmin` en la base (D-52 A). El barrido D-46 y la reasignación de vehículos no los mueven.
- **`/api/leads`:** campo y filtro `origen`, detalle sin vehículo, resultado de cierre nuevo `ATENDIDA` ("Solicitud atendida") solo para avisos. El asesor nunca ve avisos (404 en detalle y acciones).
- **Frontend:** pantallas `#/vende-tu-camion` y `#/seguro-todo-riesgo`; dos bloques en la portada y columna "Servicios" en el pie (D-51 A); en "Contactos" del administrador, etiqueta y filtro "Origen", detalle "Sin vehículo asociado", cierre con "Solicitud atendida" y sin "Reasignar" en los avisos. El comprador no tiene lista propia de avisos (D-53 A; "Mis solicitudes" pasa a P6).
- **Migración `014_p5b_avisos`** (con su archivo `_SUBIR`): IMPLEMENTADA, **NO APLICADA** en `camion_ventas`. Solo `ALTER TABLE leads`; BAJAR comentada y protegida por conteos.

### P5b.2 Pruebas ejecutadas por QA (2026-11-06)

| Comando | Resultado |
|---|---|
| `dotnet test` (`backend/Camion.Api.Tests`) | 1029/1029 |
| `npx vitest run` | 576/576 |
| `npx tsc -b` | Correcto |
| `npx tsc -p tsconfig.test.json --noEmit` | Correcto (tras corregir el ayudante de prueba `catalogoDatos.ts`) |
| `npm run build` | Correcto |

Cobertura: 60 de 62 criterios con prueba automática (CA-P5b-58 en parte); CA-P5b-59 y 60 solo manuales. Detalle en `P5b-evidencia.md`.

### P5b.3 Qué NO está probado

- **E2E:** NO EJECUTADO (Playwright no está instalado).
- **Migración 014 en MariaDB:** NO APLICADA; CA-P5b-59 y 60 quedan para la prueba manual. La revisión de seguridad la analizó solo de forma estática contra la estructura real.
- **Prueba manual de la responsable:** PENDIENTE; requiere la 014 aplicada.

### P5b.4 Riesgo de despliegue

La API de esta rama **no debe arrancar contra `camion_ventas` sin la migración 014**: lee `origen` y `tipo_vehiculo`, que aún no existen en la base.

## Actualización P6 — 2026-11-12 — commit ca1a9e1 (rama feature/p6-creditos)

Redactado por el documentador a partir de lo verificado por los demás agentes: `docs/criterios/criterios-P6-creditos.md`, `docs/criterios/P6-contrato-api.md`, `docs/criterios/P6-evidencia.md`, `docs/coordinacion/REVISION-SEGURIDAD-P6.md`, D-54 a D-60 y el historial de la rama. **No es una auditoría nueva del `auditor-codigo`.**

- **Rama:** `feature/p6-creditos`, apilada sobre `feature/p5b-avisos` (`7908e4a`). 24 commits hasta `ca1a9e1`. Sin publicar.
- **HU:** HU-034 a HU-042, HU-071 y HU-072 (solo aviso dentro del panel), más "Mis solicitudes" (D-53).

### P6.1 Qué está IMPLEMENTADO y PROBADO (pruebas automáticas)

- **Modelo:** un crédito o una búsqueda es un lead con `origen = CREDITO` o `BUSQUEDA`, siempre en Administración. El crédito no tiene tabla propia ni datos financieros (D-55 B): solo nombre, teléfono, correo opcional y comentario opcional. La búsqueda guarda sus criterios en `leads_busqueda` y los vehículos relacionados en `busqueda_vehiculos` (D-56 a y b, A).
- **API pública:** `POST /api/creditos` (desde la portada o desde la ficha, solo con publicación visible; D-60), `POST /api/busquedas` y `GET /api/catalogo/listas-busqueda`, anónimas, con los límites compartidos de P5 y P5b (por IP y por teléfono), campo trampa y tope de 8 KB. Campos financieros o manipulados se ignoran y no se guardan en ninguna tabla ni en el registro técnico.
- **Repetidos (D-60):** solo se agrega a una solicitud abierta del mismo origen y del mismo `usuario_id`, en contacto, avisos, crédito y búsqueda. Corrige M-P6-1 (un contacto desde la ficha ya no se pierde dentro de un crédito de esa ficha) y O-1 (el comprador con sesión siempre ve su envío en "Mis solicitudes").
- **Gestión (`/api/leads`):** filtro y etiquetas "Crédito" y "Búsqueda"; estados mostrados Registrado / En gestión / Cerrado (D-57 a A); notas, cierre con resultados por origen ("Remitido a entidad financiera"; "Encontró vehículo", "Sin vehículo disponible"; D-57 b A), reapertura; nunca se reasignan (409, D-58 A). "Ver coincidencias" bajo demanda (409 si no hay criterios, D-60), relacionar hasta 10 vehículos visibles y retirar sin borrar la fila. Sin coincidencias automáticas ni avisos externos (D-56 c A, D-48).
- **Permisos:** el asesor nunca ve créditos ni búsquedas, tampoco los pedidos desde sus vehículos (D-58 A); el comprador recibe 403 en `/api/leads`.
- **"Mis solicitudes":** `GET /api/mis-solicitudes` y `/{id}`, solo COMPRADOR, solo lo enviado con su sesión, de solo lectura, sin notas, responsable ni resultado (D-59).
- **Frontend:** `#/credito` (banda "Quiero un crédito" de la portada y botón "Solicitar crédito para este vehículo" en la ficha), `#/busco-un-vehiculo` (en el catálogo sin resultados, al final de los resultados y en la columna "Servicios" del pie), panel del administrador con créditos y búsquedas, y `#/mis-solicitudes` desde "Mi cuenta" del comprador.
- **Migración `015_p6_creditos_busquedas`** (con su archivo `_SUBIR`): IMPLEMENTADA, **NO APLICADA** en `camion_ventas`. Amplía cuatro CHECK, añade `IX_Lead_Usuario` y crea `leads_busqueda` y `busqueda_vehiculos`; BAJAR comentada y protegida por conteos.

### P6.2 Pruebas ejecutadas

| Comando | Resultado |
|---|---|
| `dotnet test` (`backend/Camion.Api.Tests`), tras las correcciones de M-P6-1, O-1 y coincidencias (commits `35129b1` a `f3013e2`) | 1118/1118 |
| `npx vitest run` | 643/643 |
| `npx tsc -b` | Correcto |
| `npx tsc -p tsconfig.test.json --noEmit` | Correcto |
| `npm run build` | Correcto (aviso de Vite por un paquete de más de 500 kB, O-5, no bloqueante) |

Cobertura (QA, antes de las correcciones): 78 de 80 criterios con prueba automática (CA-P6-36 en parte); CA-P6-77 y 78 solo manuales. Las pruebas de API usan SQLite en memoria y no tocan `camion_ventas`. Detalle en `P6-evidencia.md`.

### P6.3 Qué NO está probado

- **E2E:** NO EJECUTADO (Playwright no está instalado).
- **Migración 015 en MariaDB:** NO APLICADA; CA-P6-77 y 78 quedan para la prueba manual. La revisión de seguridad la analizó solo de forma estática contra la estructura y los datos reales.
- **Prueba manual de la responsable:** PENDIENTE; requiere la 015 aplicada. Pasos en `docs/manual/P6-creditos.md`.

### P6.4 Riesgo de despliegue

La API de esta rama **no debe arrancar contra `camion_ventas` sin la migración 015**: lee `leads_busqueda` y `busqueda_vehiculos`, y guarda orígenes que los CHECK actuales rechazan.
