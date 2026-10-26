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
