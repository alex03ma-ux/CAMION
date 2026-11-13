# Revisión de seguridad — P8 Reportes, CSV, indicadores, actividad y recuperación asistida (HU-061, HU-063, HU-075 a HU-080; D-68 a D-77)

- Rama revisada: `feature/p8-reportes`, diff `origin/main...HEAD` (49 archivos, 13 commits, hasta `14cc073`).
- Fecha: 2026-11-13. Revisor: devops-seguridad. Solo leí código y ejecuté `npm audit` y `dotnet list package --vulnerable`. No modifiqué código, pruebas ni ninguna base. No leí user-secrets ni archivos `appsettings*`.
- Referencias: `CLAUDE.md`, `docs/criterios/P8-contrato-api.md`, `docs/criterios/criterios-P8-reportes.md`, `DECISIONES.md` (D-12, D-44, D-68 a D-77), `REVISION-SEGURIDAD-P7.md`.

## Veredicto: CON OBSERVACIONES

No hay hallazgos críticos ni altos. La recuperación asistida está bien resuelta:
- el token va cifrado y firmado;
- es de un solo uso, porque está atado al SecurityStamp;
- vence a los 60 minutos y, al generar uno nuevo, el anterior deja de valer;
- no aparece en logs ni en la auditoría;
- la ruta anónima no permite enumerar usuarios;
- al usarlo se cierran las sesiones.

Los reportes y el CSV son solo para el administrador y no llevan datos personales de clientes.

Los dos hallazgos medios (M-1 y M-2) son de configuración para producción. No bloquean el PR en el entorno local, pero deben cerrarse antes de publicar el portal. Propongo sumarlos a INF-2. M-1 de P5 (límite por IP sin cabeceras reenviadas) y MySqlConnector 2.3.7 siguen en INF-2.

## Verificaciones ejecutadas

| Verificación | Resultado |
|---|---|
| Secretos en las líneas añadidas (password, `Server=`, cadenas de conexión, JWT, `Bearer` literal, llaves) | Sin hallazgos. `OpcionesRecuperacion` (`backend/Camion.Api/Auth/RecuperacionAsistida.cs:10-17`) no contiene secretos |
| Archivos de respaldo, `.env` o volcados en el diff | Ninguno. `frontend/respaldos/` sigue sin seguimiento y no se tocó |
| Migración | **No hay migración en P8.** El diff no toca `database/`, `Models/`, `CamionDbContext` ni ningún `.sql`. El token no se guarda en ninguna tabla |
| Dependencias nuevas (`*.csproj`, `package*.json`) | Ninguna. DataProtection viene incluido en ASP.NET Core |
| `npm audit` y `npm audit --omit=dev` | 0 vulnerabilidades |
| `dotnet list package --vulnerable --include-transitive` | Solo MySqlConnector 2.3.7 (GHSA-473q-m89c-ghf8, moderada), ya en INF-2 |
| Logs y auditoría de la recuperación | `RecuperacionEndpoints.cs:49-53` y `:95-100` solo registran `usuarioId`, `administradorId`, `venceEn` y `sesionesCerradas`. Nunca el enlace, el código ni la contraseña. La respuesta del enlace lleva `Cache-Control: no-store` (`:55`) |
| Scripts o servicios de terceros en el frontend (D-74, D-76, D-77) | Ninguno. La única URL externa es `wa.me` |
| XSS | Ningún `dangerouslySetInnerHTML`, `innerHTML` ni `eval` en los archivos nuevos. El enlace se valida con `enlaceValido` (`frontend/src/api/recuperacion.ts:21-23`) antes de pintarlo |
| `dotnet build`, `dotnet test`, `npx vitest run` | NO EJECUTADO por este agente: QA ejecuta las pruebas en paralelo |

## Detalle de la recuperación asistida (D-73 D)

| Punto | Estado | Evidencia |
|---|---|---|
| Generación y validación | Correcto | Usa un `IDataProtector` con propósito propio (`RecuperacionAsistida.cs:36,43`). Lleva hora, usuario, propósito y SecurityStamp (`:56-59`). Si el token está mal formado o alterado, se devuelve `null` sin excepción (`:88-92`). Su longitud máxima es 2048 (`:80`) |
| Un solo uso | Correcto | `ResetPasswordAsync` cambia el SecurityStamp, y `ValidateAsync` lo compara (`:72`) |
| Vencimiento | Correcto | 60 minutos con 1 minuto de tolerancia de reloj (`:35`, `:70`). Prueba: `P8RecuperacionTests.Vencimiento` |
| Solo vale el último enlace | Correcto | `UpdateSecurityStampAsync` se ejecuta antes de generar (`RecuperacionEndpoints.cs:45`). Prueba: `Solo_vale_el_ultimo` |
| Enumeración de usuarios | Correcto | La ruta anónima no recibe un correo. Un código no válido, un usuario inexistente y una cuenta no activa devuelven el mismo 400 `codigo` (`:68-74`, `:81`) |
| Límite de peticiones | Correcto, con la salvedad de M-1 de P5 | Cupo propio por IP: 5 cada 15 minutos (`Program.cs:120-134`), con 429 y su propio mensaje |
| Cierre de sesiones (D-12) | Correcto | `SessionVersion++` en la misma transacción que el cambio de contraseña (`RecuperacionEndpoints.cs:76-98`). También quita el bloqueo temporal (HU-062) |
| Reglas de contraseña | Correcto | `ValidacionAuth.ErroresPassword` (`:66`) y los validadores de Identity (`:82-83`) |
| Administradores y cuentas no activas | Correcto al generar (`:40-42`) | Al usar el enlace se comprueba de nuevo que la cuenta esté activa (`:74`), pero no el rol (ver B-1) |
| Frontend: `replaceState` | Correcto | `RestablecerContrasena.tsx:25-29` y `:52-54` quitan el código sin añadir una entrada al historial |
| Frontend: Referer | Correcto | El código va en el fragmento (`#/...`), que el navegador nunca envía al servidor ni en el Referer. Se manda a la API en el cuerpo del POST (`recuperacion.ts:65-70`), no en la URL. Los enlaces externos usan `rel="noopener noreferrer"` |
| Frontend: WhatsApp | Correcto | `recuperacion.ts:26-35`: el texto solo lleva un saludo y el enlace. No incluye el nombre, el correo ni el rol. El enlace solo existe en memoria (`EnlaceRecuperacion.tsx:1-4`) |
| Claves de DataProtection | Ver M-1 | `Program.cs:50` usa `AddDataProtection()` sin configuración. Las pruebas usan `EphemeralDataProtectionProvider` (`CamionApiFactory.cs`) |

## Hallazgos por severidad

### Crítica / Alta
Ninguno.

### Media

**M-1. Las claves de DataProtection usan la ubicación por defecto, sin nombre de aplicación.**
`backend/Camion.Api/Program.cs:50` (`builder.Services.AddDataProtection();`).
- En Windows sin IIS (el entorno actual), las claves quedan en `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` del usuario que ejecuta la API, cifradas con DPAPI. Para el uso local está bien.
- En un contenedor o en Linux, quedan en `~/.aspnet/DataProtection-Keys` **sin cifrar**, o solo en memoria si no hay carpeta de perfil. En el segundo caso, cada reinicio invalida los enlaces emitidos.
- Con varias instancias, cada una tiene claves distintas.
- Quien lea esa carpeta puede fabricar enlaces de recuperación válidos para cualquier cuenta.
- Corrección (antes de publicar, en INF-2): configurar `.SetApplicationName("Camion.Api")` y `.PersistKeysToFileSystem(<carpeta fuera del repositorio y de los respaldos públicos, con permisos solo para la cuenta del servicio>)`. Proteger las claves con `.ProtectKeysWithDpapi()` en Windows o con `.ProtectKeysWithCertificate(...)` en Linux, con el certificado fuera del repositorio (variables de entorno o almacén de certificados). Documentar la carpeta en el procedimiento de despliegue. Las claves no se deben commitear.

**M-2. La base del enlace (`Recuperacion:UrlPortal`) vale `http://localhost:5173` por defecto y no está documentada.**
`backend/Camion.Api/Auth/RecuperacionAsistida.cs:16` y `backend/Camion.Api/Endpoints/RecuperacionEndpoints.cs:56`.
- En producción, si nadie la configura, el enlace apuntará a localhost.
- Si se configura con `http://`, el código viajará sin cifrar.
- No hay `.env.example` en el repositorio que liste la clave.
- Corrección: crear `.env.example` (o una sección del README) con `Recuperacion__UrlPortal`, `Recuperacion__LimiteVentana` y `Recuperacion__VentanaMinutos`, sin valores reales. Fuera de Development, validar al arrancar que `UrlPortal` empiece por `https://`.

### Baja

**B-1. Al usar el enlace no se vuelve a comprobar que la cuenta no sea de administrador.**
`backend/Camion.Api/Endpoints/RecuperacionEndpoints.cs:74` solo comprueba `EstadoCuenta`. Si una cuenta recibiera el rol de administrador dentro de los 60 minutos de vigencia, el enlace seguiría valiendo, porque `AddToRoleAsync` no cambia el SecurityStamp. Hoy la API no permite promover a administrador.
- Corrección (defensa en profundidad): antes de `ResetPasswordAsync`, añadir `|| await users.IsInRoleAsync(user, Roles.Administrador)` a la condición y devolver `CodigoNoValido()`.

**B-2. El límite de 10 000 filas del CSV se comprueba después de calcular todo, y la paginación es en memoria.**
`backend/Camion.Api/Endpoints/ReportesEndpoints.cs:65-66` y `backend/Camion.Api/Reportes/FiltrosReporte.cs:286-290` (`Paginas.De`). Cada petición carga en memoria todas las filas del período, que como máximo es de 366 días (`FiltrosReporte.cs:68,142`). Solo lo puede hacer el administrador y el volumen actual es pequeño.
- Corrección (cuando crezca el volumen): contar con `CountAsync` antes de materializar y paginar en SQL con `Skip`/`Take`, como ya hace `ConsultasActividad.cs:62`.

**B-3. Detalles del escape de fórmulas del CSV.**
`backend/Camion.Api/Reportes/Csv.cs:14` y `:31`.
- Un número negativo de `Csv.Num` pasa por `Celda` y sale como `'-5`. No es un riesgo, pero Excel lo mostrará como texto.
- Un texto que empiece con espacios y luego `=` no se escapa. Excel no lo ejecuta, pero otras hojas de cálculo pueden hacerlo.
- Corrección: evaluar `valor.TrimStart()` al detectar el inicio peligroso, y no escapar las celdas que son números puros.

**B-4. Falta `Referrer-Policy` como defensa en profundidad.**
`frontend/index.html` y el servidor estático. Hoy el código nunca va en el Referer, porque está en el fragmento.
- Corrección: al publicar, enviar `Referrer-Policy: no-referrer` (o `strict-origin`) desde el servidor web, junto con el resto de cabeceras de INF-2.

### Informativos (sin acción)

- **I-1.** El enlace pasa por los servidores de WhatsApp (Meta) y queda en el chat. Lo acepta el diseño de D-73 D. Mitigación: un solo uso y 60 minutos de vigencia.
- **I-2.** El administrador puede tomar cualquier cuenta que no sea de administrador si genera un enlace y lo usa él mismo. Es inherente a la recuperación asistida y queda auditado (`GENERAR_ENLACE_RECUPERACION` y `RESTABLECER_CONTRASENA`, sin datos sensibles).
- **I-3.** Reportes, CSV e indicadores del administrador:
  - Todo va en grupos con `RequireAuthorization(PoliticaSoloAdministrador)` (`ReportesEndpoints.cs:42`, `ActividadEndpoints.cs:16`).
  - La exportación queda auditada con el tipo, los filtros y el número de filas, sin el contenido (`ReportesEndpoints.cs:71-79`).
  - El CSV no lleva nombres, teléfonos, correos ni mensajes de clientes. Solo lleva IDs, fechas, estados, títulos de vehículos y nombres de asesores (`Contracts/P8Contracts.cs:71-158`).
- **I-4.** "Mis indicadores": exige `PoliticaSoloAsesor` y un perfil de asesor. Ignora `asesorId` y filtra por el perfil del token (`ReportesEndpoints.cs:98-109`, `ConsultasIndicadores.cs:108-117`). No hay IDOR.
- **I-5.** Actividad:
  - Solo GET, con un tamaño de página máximo de 100 (`ActividadEndpoints.cs:44-45`).
  - Muestra solo los campos conocidos. La nota de reserva (D-28) nunca se muestra (`ConsultasActividad.cs:117`, `:220`).
- **I-6.** El CSV se descarga con el token en `Authorization`, nunca en la URL. El nombre del archivo se limpia (`frontend/src/api/http.ts`, `nombreDeDisposicion` y `descargarArchivo`).
- **I-7.** Las GET de reportes ejecutan el barrido de leads (D-46). Es una escritura conocida y ya aceptada en P5.

## Pendientes propuestos para el backlog

- Sumar M-1 y M-2 a INF-2: claves de DataProtection persistentes y protegidas, `UrlPortal` con https, `.env.example`.
- Abrir P8-B con B-1 a B-4.
