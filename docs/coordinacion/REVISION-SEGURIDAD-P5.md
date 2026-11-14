# Revisión de seguridad — P5 Leads y contactos

- Rama revisada: `feature/p5-leads`, diff `feature/diseno-portal...feature/p5-leads` (55 archivos).
- Fecha: 2026-11-04. Revisor: devops-seguridad (solo lectura de código; no se tocó código ni la base).
- Referencias: `CLAUDE.md`, `docs/criterios/P5-contrato-api.md`, `database/migraciones/013_p5_leads.sql` y `013_p5_leads_SUBIR.sql`.

## Veredicto: CON OBSERVACIONES

No hay hallazgos críticos ni altos. La entrega puede seguir. M-1 hay que resolverlo antes de publicar detrás de un proxy inverso. Los hallazgos bajos se pueden pasar al backlog.

## Verificaciones ejecutadas

| Verificación | Resultado |
|---|---|
| Búsqueda de secretos en las líneas añadidas (claves, cadenas de conexión, JWT, llaves privadas) | Sin hallazgos. Solo datos ficticios en las pruebas (`@ejemplo.test`) |
| Archivos de respaldo, `.env` o volcados en el diff | Ninguno. `frontend/respaldos/` sigue sin seguimiento y no se tocó |
| Dependencias nuevas (`*.csproj`, `package*.json`) | Ninguna. El limitador usa `Microsoft.AspNetCore.RateLimiting`, que viene en .NET 8 |
| `dotnet list package --vulnerable --include-transitive` | 1 aviso que ya existía antes de P5 (ver B-4) |
| `npm audit --omit=dev` | 0 vulnerabilidades |
| `dotnet build Camion.Api.Tests` (salida a una carpeta temporal, porque la API en ejecución bloquea `bin/`) | 0 errores |
| `dotnet test` | NO EJECUTADO: necesita MariaDB y la API en ejecución bloquea `bin/` |
| `npx vitest run` (todo) | 491/526. Los 35 fallos están en 10 archivos, casi todos de fases anteriores, en una máquina con carga (compilación en paralelo) |
| `npx vitest run tests/P5Contacto.test.tsx` (solo) | 38/38 superadas. Los fallos del conjunto completo parecen tiempos agotados; hay que repetirlo con la máquina libre |

## Hallazgos por severidad

### Crítica / Alta
Ninguno.

### Media

**M-1. El límite por IP usa la IP de la conexión, sin cabeceras reenviadas.**
`backend/Camion.Api/Program.cs:103` (`http.Connection.RemoteIpAddress`), sin `UseForwardedHeaders` en ninguna parte del proyecto.
Detrás de un proxy inverso (IIS/ARR, nginx, Cloudflare), y en desarrollo detrás del proxy de Vite, todas las visitas comparten una sola IP. El límite de 5 cada 10 minutos y 20 al día pasaría a ser **global para todo el portal**: un solo visitante, o un robot, deja el formulario "Contactar" sin servicio para todos.
Corrección propuesta: antes del despliegue, configurar `ForwardedHeadersOptions` con `KnownProxies`/`KnownNetworks` explícitos (nunca confiar en `X-Forwarded-For` de cualquier origen) y llamar a `app.UseForwardedHeaders()` antes de `UseRateLimiter` (`Program.cs:189`). Para IPv6, conviene agrupar por prefijo /64 en la clave de la partición. Mientras no haya despliegue, basta con registrarlo en el BACKLOG como requisito del despliegue.

### Baja

**B-1. Los contactos repetidos se saltan el límite por teléfono.**
`backend/Camion.Api/Endpoints/ContactosEndpoints.cs:67-84`: la rama de duplicado responde 201 y agrega un evento `CONTACTO_REPETIDO` y un consentimiento **antes** del límite por teléfono (`:86-90`). Con varias IP se puede inflar sin límite el historial de un lead y la tabla `consentimientos_datos`. También queda un oráculo menor: con un teléfono que ya tiene 10 leads en 24 h, el 201 frente al 429 indica si ese teléfono tiene un lead abierto en esa publicación.
Corrección: aplicar el límite por teléfono antes de la búsqueda del duplicado, contando leads nuevos más eventos `CONTACTO_REPETIDO` de las últimas 24 h, o poner un tope de repeticiones por lead y día. Fuera del tope se responde 201 sin guardar nada, igual que con el campo trampa, para no revelar estado.

**B-2. Condición de carrera en el duplicado.**
`ContactosEndpoints.cs:68-70`: la comprobación de "lead ABIERTO con el mismo teléfono y la misma publicación" es una lectura sin bloqueo, y el índice `IX_Lead_Duplicado` no es único (`013_p5_leads_SUBIR.sql:175`). Dos envíos simultáneos pueden crear dos leads. El impacto es funcional, no de confidencialidad.
Corrección: `SELECT ... FOR UPDATE` sobre la publicación dentro de la transacción, o aceptarlo y dejarlo documentado (el cierre `DUPLICADO_INVALIDO` ya cubre el caso).

**B-3. `Contactables` acepta publicaciones que ya no se ven en el catálogo.**
`backend/Camion.Api/Catalogo/ConsultasCatalogo.cs` (método `Contactables`) y `ContactosEndpoints.cs:53-55`: un slug de una publicación con el plan vencido o el asesor bloqueado responde 201, no 404. Es el comportamiento acordado (CA-P5-26/27, el lead va a Administración) y los slugs no son adivinables, así que solo se registra como aceptado.

**B-4. MySqlConnector 2.3.7 tiene un aviso moderado (GHSA-473q-m89c-ghf8). Ya existía antes de P5.**
`backend/Camion.Api/Camion.Api.csproj:12`. No lo introduce P5.
Corrección: subir a una versión corregida en un PR propio de dependencias, con `dotnet test` completo.

**B-5. El script SUBIR no fija la base de datos.**
`database/migraciones/013_p5_leads_SUBIR.sql:1-10`: no tiene `USE camion_ventas;`. Sigue la convención del 012 (phpMyAdmin), pero si se importa con otra base seleccionada, crearía las tablas allí. No define `CHARSET`: las tablas heredan el utf8mb4 de `camion_ventas`, lo cual es correcto.
Corrección: no hace falta tocar el script. Basta con seguir el paso 3 del procedimiento (seleccionar `camion_ventas` antes de importar).

### Informativos (sin acción)

- I-1. `backend/Camion.Api/appsettings.json:18` y `backend/Camion.Api/Leads/ReglasLeads.cs:19`: número de WhatsApp del portal. Es un dato comercial público (D-42), no un secreto.
- I-2. `frontend/tests/...` usa el correo comercial público del portal en una aserción de `mailto:`. Es dato público.
- I-3. Campo trampa correcto: `ContactosEndpoints.cs:45` responde el mismo 201 sin guardar, y `frontend/src/components/FormularioContacto.tsx:221-231` lo oculta con `aria-hidden`, `tabIndex={-1}` y `autoComplete="off"`.
- I-4. Tamaño: `ContactosEndpoints.cs:28-30` corta por `Content-Length` y además mide lo leído (cubre el envío por trozos). Devuelve 413 a partir de 8 KB.

## Revisión por punto pedido

**Endpoint anónimo POST /api/contactos.** El límite por IP con ventana corta y diaria está en `Program.cs:94-123` y solo se aplica a las rutas marcadas con la metadata correspondiente (`ContactosEndpoints.cs:133`). Además hay un límite por teléfono (`:86-90`; ver B-1) y el límite de tamaño (I-4). La validación del servidor normaliza el teléfono colombiano y valida el correo y los largos (`ReglasLeads.cs:82-102`). La autorización de datos es obligatoria.
- Inyección: solo hay LINQ y `ExecuteUpdate` parametrizados. No hay SQL crudo.
- Enumeración: la respuesta 201 no incluye id ni responsable (`:162-163`), y lo mismo pasa para nuevo, repetido y campo trampa. El 409 de "es tuyo" solo puede verlo el dueño autenticado.
- XSS: los datos se guardan tal cual, y el panel los pinta como texto de React. `tel:` y `mailto:` usan valores que el servidor ya validó.

**Autorización de /api/leads.** El grupo exige el rol ASESOR o ADMINISTRADOR (`LeadsEndpoints.cs:161`). El comprador recibe 403 y sin token se recibe 401. El asesor queda limitado a `ResponsableAsesorId == su perfil` (`:374-375`). `ActorAsync` nunca devuelve un asesor con perfil nulo, así que no puede ver los leads de Administración: un asesor sin perfil recibe 403 (`InventarioEndpoints.cs:652-661`). Lo ajeno da 404 en el detalle, las notas y el cierre (`:389-390`). Reabrir y reasignar llevan además la política SoloAdministrador (`:329`, `:364`). Las cuentas bloqueadas se rechazan en `OnTokenValidated` (`Program.cs:64-69`).

**Barrido D-46.** `backend/Camion.Api/Leads/BarridoLeads.cs:119-131` hace un UPDATE condicionado al responsable y al estado actuales, y solo escribe el evento y la auditoría si afectó una fila. Es idempotente aunque haya concurrencia, porque InnoDB hace la lectura con bloqueo en el UPDATE. Queda auditado: evento `REASIGNADO` con actor SISTEMA y fila de auditoría `Lead/REASIGNAR` sin datos del visitante (`:134-158`). El bloqueo de un asesor (`AdminUsuariosEndpoints.cs`, +4 líneas) y la reasignación de vehículo (`VentasEndpoints.cs`, +4 líneas) usan el mismo mecanismo dentro de la transacción de quien los llama.

**DTO público.** `backend/Camion.Api/Contracts/CatalogoContracts.cs` solo añade `Whatsapp` (`https://wa.me/...`). No se exponen el nombre, el correo, el documento ni el id del asesor (`CatalogoEndpoints.cs`, consulta `dueno`). El frontend vuelve a validar el formato con `^https://wa.me/\d{8,15}$` (`frontend/src/api/contactos.ts:156`). Los consentimientos solo los ve el administrador (`LeadsEndpoints.cs:476-486`).

**Frontend.** No hay `dangerouslySetInnerHTML`. Los enlaces con `target="_blank"` llevan `rel="noopener noreferrer"` (botón de WhatsApp, enlace a privacidad, WhatsApp del portal). En la URL solo van el id del lead (`#/contactos/{id}`) y los filtros (estado, responsable, vehículo, fechas, página); nada de nombre, teléfono ni correo. El formulario envía por POST.

## Migración 013

Revisión:
- Es reversible: solo crea 3 tablas nuevas (`leads`, `eventos_lead`, `consentimientos_datos`) y no modifica ni borra datos existentes.
- No tiene DROP, TRUNCATE ni DELETE activos. La parte BAJAR está dentro de un comentario `/* ... */` (`013_p5_leads.sql:131-144`) y va precedida de conteos que la protegen: si alguna tabla tiene filas, no se baja sin decisión de la responsable.
- Compatible con MariaDB 10.4: `DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6)` y `CHECK` (que se aplican desde 10.2). `information_schema.CHECK_CONSTRAINTS` existe desde 10.3.10.
- Claves foráneas: las FK a `AspNetUsers(Id)` son `VARCHAR(255)`, igual que en el esquema 002. Las de publicaciones y perfiles usan `RESTRICT`, y el código no borra físicamente publicaciones ni perfiles. Las de usuario usan `SET NULL`.
- Índices: alineados con las consultas (responsable+estado+fecha, duplicado, teléfono+fecha para el límite, entidad del consentimiento).
- No es atómica: cada `CREATE TABLE` hace commit implícito. Si falla a mitad, hay que bajar solo las tablas creadas (ver el rollback).

### Procedimiento para la responsable (NO ejecutado por este agente)

1. **Respaldo previo verificable.** Con la API detenida, abrir una consola en `C:\xampp\mysql\bin` y ejecutar
   `mysqldump -u root -p --single-transaction --routines --triggers --databases camion_ventas > C:\Respaldo_CAMION\camion_ventas_antes_013_AAAAMMDD_HHMM.sql`
   Usar siempre un nombre nuevo: nunca sobrescribir ni borrar respaldos que ya existan. Para verificarlo, comprobar que el archivo pesa más de 0 bytes, que termina con `-- Dump completed` y que contiene `CREATE TABLE \`publicaciones\``. Anotar el tamaño y la hora en `docs/coordinacion/BITACORA.md`.
2. **Verificación antes.** El coordinador ejecuta, con el cliente mysql, el bloque "VERIFICACIÓN ANTES" de `013_p5_leads.sql:27-31`. Lo esperado es que no aparezcan `leads`, `eventos_lead` ni `consentimientos_datos`, y que sí existan las tablas base. Anotar el número de publicaciones.
3. **Aplicación.** En phpMyAdmin, **seleccionar primero la base `camion_ventas`** en el panel izquierdo (B-5). Después, en la pestaña Importar, cargar **solo** `database/migraciones/013_p5_leads_SUBIR.sql`.
4. **Verificación después.** El coordinador ejecuta el bloque "VERIFICACIÓN DESPUÉS DE SUBIR" (`013_p5_leads.sql:111-123`). Lo esperado: 3 tablas vacías, 5 CHECK (3 en leads, 1 en eventos_lead, 1 en consentimientos_datos), 9 FK con sus reglas de borrado y el mismo número de publicaciones que en el paso 2.
5. Arrancar la API P5 y probar en el portal un contacto de prueba con datos ficticios.

### Rollback

- Si la importación falla a mitad, o hay que revertir y **las tres tablas están vacías**: con la API detenida, ejecutar los conteos y después los `DROP TABLE` del bloque comentado `013_p5_leads.sql:131-144`, en el orden indicado (consentimientos_datos, eventos_lead, leads). Esos DROP solo afectan a las tablas de esta migración. Si una tabla no llegó a crearse, se omite su línea.
- Si **hay filas** (leads reales o consentimientos D-44): no se baja sin decisión de la responsable. Primero se exportan las tres tablas (`mysqldump ... camion_ventas leads eventos_lead consentimientos_datos > C:\Respaldo_CAMION\p5_leads_export_AAAAMMDD_HHMM.sql`, con un nombre nuevo) o se restaura el respaldo del paso 1. La restauración la decide la responsable, porque se perdería todo lo escrito después del respaldo.
- Después: volver a desplegar la API anterior a P5, porque la API P5 necesita las tres tablas.

## Cierre INF-2

M-1 y B-4 quedaron corregidos en `feature/inf-publicacion`. El detalle está en `REVISION-SEGURIDAD-P8.md`, sección "Cierre INF-2".
