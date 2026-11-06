# Revisión de seguridad — P5b Avisos "Vende tu camión" (HU-N01) y "Compra tu seguro todo riesgo" (HU-N02)

- Rama revisada: `feature/p5b-avisos`, diff `feature/p5-leads...feature/p5b-avisos` (30 archivos, 11 commits, hasta `8c70945`).
- Fecha: 2026-11-06. Revisor: devops-seguridad (solo lectura de código; solo consultas SELECT a `camion_ventas`; no se tocó código, pruebas ni la base).
- Referencias: `CLAUDE.md`, `docs/criterios/P5b-contrato-api.md`, `docs/criterios/criterios-P5b-avisos.md`, `DECISIONES.md` (D-44, D-50 a D-53), `database/migraciones/014_p5b_avisos.sql` y `014_p5b_avisos_SUBIR.sql`, revisión previa `REVISION-SEGURIDAD-P5.md`.

## Veredicto: CON OBSERVACIONES

No hay hallazgos críticos ni altos. No hay hallazgos medios nuevos: el único medio que afecta a P5b es M-1 de P5 (límite por IP sin cabeceras reenviadas), que ya está en INF-2 y ahora cubre también los dos avisos. Los hallazgos bajos se pueden pasar al backlog. La migración 014 se puede aplicar con el procedimiento de este documento.

## Verificaciones ejecutadas

| Verificación | Resultado |
|---|---|
| Búsqueda de secretos en las líneas añadidas (claves, cadenas de conexión, JWT, llaves privadas, `server=`) | Sin hallazgos |
| Datos personales en el diff | Solo ficticios: `3001234567`, `@ejemplo.test` en pruebas y contrato |
| Archivos de respaldo, `.env` o volcados en el diff | Ninguno. `frontend/respaldos/` sigue sin seguimiento y no se tocó |
| Dependencias nuevas (`*.csproj`, `package*.json`) | Ninguna |
| `npm audit --omit=dev` | 0 vulnerabilidades |
| `dotnet list package --vulnerable --include-transitive` | Solo MySqlConnector 2.3.7 (GHSA-473q-m89c-ghf8), ya registrado como B-4 de P5 / INF-2 |
| `dotnet build Camion.Api.Tests` (salida a carpeta temporal) | 0 errores, 3 advertencias |
| `dotnet test` / `npx vitest run` | NO EJECUTADO por este agente (QA ejecuta las pruebas en paralelo; ver `docs/criterios/P5b-evidencia.md`) |
| Consultas de solo lectura a `camion_ventas` | MariaDB 10.4.32; 3 leads, todos con `publicacion_id` no nulo y `resultado_cierre` NULL; 14 eventos; 3 consentimientos `CONTACTO_VEHICULO`; `CK_Consent_Formulario` ya admite `VENDE_TU_CAMION` y `SEGURO_TODO_RIESGO` (013) |
| Migración 014 ejecutada | NO EJECUTADA (prohibido escribir en cualquier base). Análisis estático contra la estructura y los datos reales |

## Hallazgos por severidad

### Crítica / Alta
Ninguno.

### Media
Ninguno nuevo.

- Recordatorio M-1 (P5, en INF-2): `backend/Camion.Api/Program.cs:103` usa `http.Connection.RemoteIpAddress`. La partición es `prefijo + ip` sin ruta, así que el contacto y los dos avisos comparten el mismo cupo de 5 cada 10 min y 20 al día. Detrás de un proxy, ese cupo sería global para los tres formularios. La corrección es la ya propuesta (`UseForwardedHeaders` con `KnownProxies` antes de `UseRateLimiter`), requisito del despliegue.

### Baja

**B-1. Un tercero que conoce el teléfono puede añadir texto al aviso abierto de otra persona.**
`backend/Camion.Api/Endpoints/AvisosEndpoints.cs:85-103`: el repetido se detecta solo por origen + teléfono + ABIERTO. Quien envíe un "Vende tu camión" con el teléfono de otra persona agrega su descripción como `CONTACTO_REPETIDO` y un consentimiento al lead de esa persona. El nombre y el correo enviados se descartan. Es el mismo diseño de P5 (CA-P5b-26/27) y lo limita el tope por teléfono (10 al día).
Corrección: en el panel, mostrar el evento repetido como "Nuevo envío con este teléfono (sin verificar)", para que el administrador confirme por teléfono. No hace falta cambiar código ahora.

**B-2. Bloqueo dirigido por teléfono.**
`AvisosEndpoints.cs:80` y `ContactosEndpoints.cs` (`EnviosTelefonoAsync`): el tope de 10 envíos al día es compartido entre el contacto y los dos avisos. Con varias IP, alguien que conozca un teléfono puede agotarlo y dejar a esa persona 24 h con el mensaje "Has enviado muchas solicitudes". El 429 también indica que ese teléfono tuvo más de 10 envíos (oráculo menor, igual que en P5).
Corrección: aceptarlo (lo cubre el límite por IP cuando se resuelva M-1) o, fuera del tope, responder 201 sin guardar nada, como con el campo trampa.

**B-3. Conteo del tope y búsqueda del repetido sin bloqueo.**
`AvisosEndpoints.cs:80-90`: el conteo por teléfono se hace antes de abrir la transacción (`:83`) y la búsqueda del repetido es una lectura sin bloqueo. Dos envíos simultáneos pueden superar el tope por 1 o crear dos avisos abiertos del mismo teléfono. El impacto es funcional (el cierre `DUPLICADO_INVALIDO` lo cubre). Es igual que B-2 de P5.
Corrección: aceptarlo y dejarlo documentado.

**B-4. El script SUBIR no fija la base ni se puede repetir.**
`database/migraciones/014_p5b_avisos_SUBIR.sql:1-13`: no tiene `USE camion_ventas;` (igual que B-5 de P5). Tampoco es repetible: si falla a mitad, volver a importarlo entero falla en `ADD COLUMN origen` (columna duplicada).
Corrección: seleccionar `camion_ventas` antes de importar y, si falla, seguir el rollback parcial descrito más abajo (no volver a importar el archivo entero).

**B-5. Ventana sin `CK_Lead_Resultado` entre el DROP y el ADD.**
`014_p5b_avisos_SUBIR.sql:32-35` (y `014_p5b_avisos.sql:61-64`). Ver la valoración en la sección de la migración. El riesgo es bajo con la API detenida.

### Informativos (sin acción)

- I-1. Tope de 8 KB: `AvisosEndpoints.cs:51-53` corta por `Content-Length` y mide lo leído (`ContactosEndpoints.LeerHastaAsync`, ahora `internal`). Responde 413.
- I-2. Campo trampa: `AvisosEndpoints.cs:67` responde el mismo 201 sin guardar, después de validar el formato y antes de validar los campos. No revela si el envío se guardó.
- I-3. Asignación masiva: `VendeTuCamionRequest` y `SeguroTodoRiesgoRequest` (`backend/Camion.Api/Contracts/LeadsContracts.cs`) no tienen origen, estado, responsable ni publicación. El servidor fija `ResponsableAsesorId = null` y `PublicacionId = null`. La respuesta nunca incluye el id ni el responsable (`AvisosEndpoints.cs`, `Exito()`).
- I-4. Datos personales en registros: no hay `ILogger` en los avisos ni en `ReglasLeads`. No se usa `EnableSensitiveDataLogging`. La auditoría (`AvisosEndpoints.cs:136-143`) guarda solo el origen, el tipo de vehículo y `conCuenta`. Los eventos no guardan teléfono ni correo.
- I-5. Token opcional: un token bloqueado o vencido se rechaza en `OnTokenValidated`, y la ruta sigue siendo anónima (`AllowAnonymous`). El aviso no se liga a la cuenta.

## Revisión por punto pedido

**Endpoints anónimos.** `POST /api/avisos/vende-tu-camion` y `/seguro-todo-riesgo` (`AvisosEndpoints.cs:23-36`) llevan `AllowAnonymous` y `LimitePeticionesContacto`. El orden es 413 -> 400 formato -> trampa -> 400 campos -> 429 teléfono -> repetido -> nuevo.
- Validación del servidor (`backend/Camion.Api/Leads/ReglasLeads.cs`, `ValidarTitular`, `ValidarVendeTuCamion`, `ValidarSeguro`): nombre de 2 a 150, teléfono colombiano normalizado, correo opcional validado, autorización obligatoria, descripción de 1 a 500 y tipo en lista cerrada.
- Inyección: solo LINQ parametrizado.

**Permisos.**
- El asesor nunca ve avisos. Hay dos barreras: el alcance filtra `Origen == CONTACTO_VEHICULO` (`backend/Camion.Api/Endpoints/LeadsEndpoints.cs:249`), y `CK_Lead_AvisoAdmin` impide que un aviso tenga responsable asesor. Como `PrepararAsync` (`:263`) usa el mismo alcance, el asesor recibe 404 en la lista, los contadores, el detalle, las notas y el cierre.
- Reasignar un aviso: el administrador recibe 409 (`:208`), antes de validar el motivo. El asesor recibe 404 antes de llegar ahí.
- Reabrir: lleva la política SoloAdministrador (`:198`) y no cambia el responsable.
- Barrido D-46 y reasignación de vehículo (`backend/Camion.Api/Leads/BarridoLeads.cs`): solo actúan sobre leads con responsable asesor o con publicación, así que no tocan avisos.
- Cierre: los resultados que no aplican al origen dan 400 (`ReglasLeads.ValidarCierre`).
- IDOR: no hay fugas nuevas. El filtro `origen` se valida contra una lista cerrada y se aplica sobre el alcance.

**D-44.** El consentimiento se inserta en la misma transacción que el lead nuevo (`AvisosEndpoints.cs:83`, `:135`, commit único al final) y que el evento repetido (`:100`). Usa `formulario` = el origen, que `CK_Consent_Formulario` ya admite.

**Frontend.**
- No hay `dangerouslySetInnerHTML` ni `innerHTML`. El nombre y la descripción se pintan como texto de React (`frontend/src/components/DetalleContacto.tsx`, `Contactos.tsx`). `tel:` y `mailto:` usan valores que el servidor ya normalizó.
- El único `target="_blank"` nuevo (`frontend/src/components/PiePortal.tsx:86`) lleva `rel="noopener noreferrer"`.
- El token se envía solo si hay sesión activa con perfil (`frontend/src/App.tsx`, `sesionFormulario`; `frontend/src/components/AvisoPublico.tsx:149-150`), y solo a la ruta relativa `/api/avisos/...` (`frontend/src/api/avisos.ts:177-183`).
- Los errores 5xx y de red se muestran con un mensaje genérico, sin trazas.

## Migración 014

Revisión:
- **Reversible y sin borrar datos.** SUBIR solo hace `ALTER TABLE leads`: `MODIFY publicacion_id NULL`, dos columnas nuevas (una con `DEFAULT`), dos CHECK nuevos, dos índices y el reemplazo de `CK_Lead_Resultado`. No hay DROP TABLE, DROP COLUMN, TRUNCATE ni DELETE (`014_p5b_avisos_SUBIR.sql`). BAJAR está comentada (`014_p5b_avisos.sql:99-129`) y protegida por tres conteos. El `DROP COLUMN` de BAJAR solo afecta a columnas creadas por la 014, y solo se ejecuta si no hay avisos ni cierres ATENDIDA.
- **`DROP CONSTRAINT CK_Lead_Resultado` (`014_p5b_avisos_SUBIR.sql:32`).** Riesgo bajo:
  - quita una regla, no datos;
  - MariaDB 10.4 admite `DROP CONSTRAINT` para CHECK;
  - es el mismo patrón de 003, 011 y 012.
  - Riesgo real: si el `ADD CONSTRAINT` siguiente (`:33-35`) fallara, la tabla quedaría sin la regla de resultados. Con la API detenida nadie escribe en ese intervalo, y el nuevo CHECK es un superconjunto del anterior, así que las filas actuales lo cumplen. Si falla, se vuelve a ejecutar solo `:33-35`.
  - Mitigación ya prevista: la verificación posterior lista `CK_Lead_Resultado` con ATENDIDA.
- **Datos actuales (consultados, solo lectura).** Con las 3 filas reales:
  - `origen` toma `CONTACTO_VEHICULO`, `publicacion_id` no es nulo y `tipo_vehiculo` es NULL, así que se cumple `CK_Lead_Origen`;
  - el origen es contacto, así que se cumple `CK_Lead_AvisoAdmin` aunque las filas 2 y 3 tengan un asesor;
  - `resultado_cierre` es NULL en todas, así que se cumplen los dos `CK_Lead_Resultado`.
  - Los nombres `IX_Lead_Origen`, `IX_Lead_AvisoDuplicado`, `CK_Lead_Origen` y `CK_Lead_AvisoAdmin` no existen hoy, así que no hay colisión. Conclusión: con estos datos la migración no debería fallar (análisis estático, NO EJECUTADA).
- **FK.** Pasar `publicacion_id` de NOT NULL a NULL mantiene `FK_Lead_Publicacion` (RESTRICT). BAJAR vuelve a NOT NULL solo si `sin_publicacion = 0`.
- **No es atómica.** Cada `ALTER` hace commit implícito (está documentado en `014_p5b_avisos.sql:21`). Con 3 filas, la copia de la tabla es inmediata.
- **EF alineado.** `backend/Camion.Api/Data/CamionDbContext.cs` declara los mismos CHECK, columnas e índices.

### Procedimiento para la responsable (NO ejecutado por este agente)

1. **Detener la API** (la API de P5b lee `origen` y `tipo_vehiculo`; la de P5 no admite `publicacion_id` nulo).
2. **Respaldo previo verificable.** En una consola en `C:\xampp\mysql\bin` ejecutar
   `mysqldump -u root -p --single-transaction --routines --triggers --databases camion_ventas --result-file=C:\Respaldo_CAMION\camion_ventas_antes_014_AAAAMMDD_HHMM.sql`
   - Usar un nombre nuevo. Antes de ejecutar, comprobar con `dir C:\Respaldo_CAMION\` que no existe un archivo con ese nombre. Nunca sobrescribir ni borrar respaldos existentes.
   - Para verificarlo: el archivo pesa más de 0 bytes, termina con `-- Dump completed` y contiene `CREATE TABLE \`leads\`` y `INSERT INTO \`leads\``.
   - Anotar el nombre, el tamaño y la hora en `docs/coordinacion/BITACORA.md`.
3. **Verificación antes.** El coordinador ejecuta, con el cliente mysql, el bloque "VERIFICACIÓN ANTES" (`014_p5b_avisos.sql:30-36`). Esperado: sin `origen` ni `tipo_vehiculo`; `publicacion_id` con `IS_NULLABLE = NO`; conteos 3 / 14 / 3 (o los vigentes ese día, que se anotan); `leads_sin_publicacion = 0`.
4. **Aplicación.** En phpMyAdmin, **seleccionar primero `camion_ventas`** (B-4). Después, en Importar, cargar **solo** `database/migraciones/014_p5b_avisos_SUBIR.sql`.
5. **Verificación después.** El coordinador ejecuta el bloque "VERIFICACIÓN DESPUÉS DE SUBIR" (`014_p5b_avisos.sql:72-91`). Esperado:
   - los mismos conteos que en el paso 3;
   - todos los leads con `CONTACTO_VEHICULO`;
   - `publicacion_id` con `IS_NULLABLE = YES`, `origen` NOT NULL con DEFAULT `'CONTACTO_VEHICULO'` y `tipo_vehiculo` NULL;
   - 5 CHECK en leads (`CK_Lead_Estado`, `CK_Lead_Cierre`, `CK_Lead_Resultado` con ATENDIDA, `CK_Lead_Origen` y `CK_Lead_AvisoAdmin`);
   - `FK_Lead_Publicacion` con RESTRICT;
   - `IX_Lead_Origen` e `IX_Lead_AvisoDuplicado` presentes.
6. Arrancar la API P5b y enviar un aviso de cada tipo con datos ficticios. Comprobar que aparecen en "Contactos" del administrador y que un asesor no los ve.

### Rollback

- **Fallo a mitad de SUBIR.** No volver a importar el archivo entero. Con la verificación posterior, ver qué `ALTER` se aplicó:
  - si solo falló el último, ejecutar solo el `ADD CONSTRAINT CK_Lead_Resultado`;
  - si se quiere volver atrás, ejecutar del bloque BAJAR solo las partes que correspondan a lo aplicado.
- **Revertir sin avisos.** Si los tres conteos de BAJAR (`014_p5b_avisos.sql:100-102`) dan 0: con la API detenida, ejecutar el paso 2 de BAJAR (`:105-118`), luego su verificación (`:122-128`), y volver a desplegar la API de P5.
- **Revertir con avisos o cierres ATENDIDA.** No se baja sin decisión de la responsable. Primero se exportan con un nombre nuevo:
  `mysqldump -u root -p --single-transaction camion_ventas leads eventos_lead consentimientos_datos --result-file=C:\Respaldo_CAMION\p5b_avisos_export_AAAAMMDD_HHMM.sql`
  Otra opción es restaurar el respaldo del paso 2, pero se perdería todo lo escrito después del respaldo; esa restauración la decide la responsable.
