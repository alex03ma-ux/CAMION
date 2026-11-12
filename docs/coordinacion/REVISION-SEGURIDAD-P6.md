# Revisión de seguridad — P6 Créditos, búsquedas y "Mis solicitudes" (HU-034 a HU-042, D-54 a D-59)

- Rama revisada: `feature/p6-creditos`, diff `feature/p5b-avisos...HEAD` (49 archivos, 15 commits, hasta `f2adf48`).
- Fecha: 2026-11-10. Revisor: devops-seguridad. Solo leí código y ejecuté consultas SELECT/SHOW contra `camion_ventas`. No modifiqué código, pruebas ni la base, y no leí user-secrets ni archivos `appsettings*`.
- Referencias: `CLAUDE.md`, `docs/criterios/P6-contrato-api.md`, `docs/criterios/criterios-P6-creditos.md`, `DECISIONES.md` (D-44, D-45, D-54 a D-59), `database/migraciones/015_p6_creditos_busquedas.sql` y `015_p6_creditos_busquedas_SUBIR.sql`, `REVISION-SEGURIDAD-P5b.md`.

## Veredicto: CON OBSERVACIONES

No hay hallazgos críticos ni altos. Hay **un hallazgo medio nuevo (M-P6-1)**: una regresión en el contacto de P5 que provoca P6, porque ahora un crédito puede tener publicación. Conviene corregirlo antes del merge. Es un cambio de una línea, con su prueba. M-1 de P5 (límite por IP sin cabeceras reenviadas) sigue en INF-2 y ahora también cubre `/api/creditos` y `/api/busquedas`. Los hallazgos bajos pueden ir al backlog. La migración 015 se puede aplicar con el procedimiento de este documento.

## Verificaciones ejecutadas

| Verificación | Resultado |
|---|---|
| Secretos en las líneas añadidas (password, `server=`, cadenas de conexión, JWT `eyJ…`, llaves privadas, `Bearer` literal) | Sin hallazgos |
| Datos personales en el diff | Solo ficticios: `3001234567`, `cliente.ficticio@ejemplo.test`, `persona.ficticia@ejemplo.test`. El 316 615 2907 es el WhatsApp público del portal (D-42) |
| Archivos de respaldo, `.env` o volcados en el diff | Ninguno. `frontend/respaldos/` sigue sin seguimiento y no se tocó |
| Dependencias nuevas (`*.csproj`, `package*.json`) | Ninguna |
| `npm audit --omit=dev` | 0 vulnerabilidades |
| `dotnet list package --vulnerable --include-transitive` | Solo MySqlConnector 2.3.7 (GHSA-473q-m89c-ghf8, moderada), ya en INF-2 |
| `dotnet build` / `dotnet test` / `npx vitest run` | NO EJECUTADO por este agente. QA ejecuta las pruebas en paralelo (`docs/criterios/P6-evidencia.md`) |
| Consultas de solo lectura a `camion_ventas` | MariaDB 10.4.32, `lower_case_table_names = 1`, cotejamiento `utf8mb4_general_ci`. Hay 6 leads (3 `CONTACTO_VEHICULO` con publicación, 2 `VENDE_TU_CAMION` y 1 `SEGURO_TODO_RIESGO` `MAQUINARIA`, sin publicación), todos ABIERTO, con `resultado_cierre` NULL y sin `usuario_id`. Hay 20 eventos (`ASIGNADO` 6, `CREADO` 6, `NOTA` 3, `CERRADO` 2, `REABIERTO` 2, `REASIGNADO` 1) y 6 consentimientos (`CONTACTO_VEHICULO` 3, `VENDE_TU_CAMION` 2, `SEGURO_TODO_RIESGO` 1). No existen `leads_busqueda`, `busqueda_vehiculos`, `IX_Lead_Usuario` ni restricciones `*LeadBusq*` o `*BusqVeh*` |
| Migración 015 ejecutada | NO EJECUTADA (está prohibido escribir en cualquier base). Hice un análisis estático contra la estructura y los datos reales |

## Hallazgos por severidad

### Crítica / Alta
Ninguno.

### Media

**M-P6-1. Un contacto de P5 puede quedar dentro del crédito de Administración y no llegar al asesor.**
`backend/Camion.Api/Endpoints/ContactosEndpoints.cs:73-75`: la búsqueda de repetidos del contacto de P5 filtra solo por `PublicacionId == pub.Id`, teléfono y ABIERTO, sin mirar el origen. Hasta P5b solo los contactos tenían publicación. Desde P6, un crédito pedido desde la ficha también la tiene (`SolicitudesEndpoints.cs:68`, `:135`).
- Escenario: una persona pide crédito desde la ficha de un vehículo y después pulsa "Contactar" en la misma ficha con el mismo teléfono. El contacto no crea un lead para el asesor dueño. Se agrega como `CONTACTO_REPETIDO` al lead `CREDITO`, que está en Administración, junto con un consentimiento `CONTACTO_VEHICULO` ligado a ese crédito.
- Impacto: el asesor con plan vigente pierde el contacto de su vehículo. Esto contradice la regla "el asesor atiende los contactos de sus propios vehículos publicados" (CLAUDE.md, D-7). No hay fuga de datos: el administrador ya ve ambos.
- El caso contrario está bien: el crédito filtra por `Origen` (`SolicitudesEndpoints.cs:114`).
- Corrección: añadir `&& l.Origen == Lead.OrigenContactoVehiculo` en `ContactosEndpoints.cs:74`, con una prueba del tipo "crédito desde la ficha y luego contacto con el mismo teléfono: se crean dos leads, el contacto con responsable el asesor". El índice `IX_Lead_Duplicado` sigue sirviendo.

- Recordatorio M-1 (P5, en INF-2): `backend/Camion.Api/Program.cs:104` usa `RemoteIpAddress`. El contacto, los dos avisos, el crédito y la búsqueda comparten el mismo cupo por IP (5 cada 10 min y 20 al día). Detrás de un proxy, ese cupo sería global. La corrección no cambia: `UseForwardedHeaders` con `KnownProxies` antes de `UseRateLimiter`.

### Baja

**B-1. Un tercero que conoce el teléfono puede añadir texto a un crédito o una búsqueda abiertos de otra persona.**
`SolicitudesEndpoints.cs:113-130`: es el mismo diseño que B-1 de P5b.
- Si quien envía tiene sesión, el consentimiento queda con su `usuario_id`, pero el lead sigue siendo del dueño original. Por eso no aparece en "Mis solicitudes" de quien envió, y no hay fuga.
- Corrección: la misma de P5b. En el panel, mostrar el repetido como "Nuevo envío con este teléfono (sin verificar)".

**B-2. Bloqueo dirigido por teléfono y oráculo del 429.**
`SolicitudesEndpoints.cs:108-109`: el tope de 10 al día es compartido por los cinco formularios. Es igual que B-2 de P5b.

**B-3. Carreras sin bloqueo.**
- `SolicitudesEndpoints.cs:108-116`: el conteo por teléfono y el repetido se calculan sin bloqueo. Es igual que B-3 de P5b.
- `LeadsEndpoints.cs:198-201`: el tope de 10 vehículos relacionados y el "ya relacionado" se comprueban sin bloqueo. Dos peticiones simultáneas del administrador pueden dejar 11 relaciones, o chocar con `UX_BusqVeh_LeadPub` y recibir 500.
- Corrección: aceptarlo (solo afecta al administrador) o capturar `DbUpdateException` y responder 409.

**B-4. La gestión de vehículos relacionados no usa la política de administrador.**
`LeadsEndpoints.cs:145`, `:180` y `:227`: las tres rutas solo heredan `AsesorOAdministrador` (`:28`) y comprueban `EsAdmin` dentro del método (`:150`, `:185`, `:232`).
- Hoy es correcto: el asesor recibe 404 antes, porque el alcance (`:357-360`) excluye todo lo que no es `CONTACTO_VEHICULO`. Después, el 403 de `EsAdmin` es una segunda barrera.
- Corrección (defensa en profundidad): añadir `.RequireAuthorization(Roles.PoliticaSoloAdministrador)` a las tres rutas, como `reabrir` (`:309`) y `reasignar` (`:346`).

**B-5. `coincidencias` responde 500 si falta la fila de criterios.**
`LeadsEndpoints.cs:152`: `FirstAsync` lanza una excepción si un lead `BUSQUEDA` no tiene fila en `leads_busqueda`, por ejemplo si se cargó a mano.
- La API siempre crea esa fila en la misma transacción (`SolicitudesEndpoints.cs:151-155`).
- Corrección: usar `FirstOrDefaultAsync` y responder 409 o una lista vacía.

**B-6. `GET /api/catalogo/listas-busqueda` es anónima y no tiene límite de peticiones.**
`CatalogoEndpoints.cs:281-312`: hace 6 consultas por petición y devuelve unos 40 KB con todo DIVIPOLA. No tiene datos personales y lleva `Cache-Control: public, max-age=300` (`:302`).
- Riesgo: amplificación de carga si se llama en bucle.
- Corrección (en el despliegue, junto con M-1): poner la respuesta en caché en memoria del servidor (por ejemplo, 5 min) o aplicarle un límite por IP generoso.

**B-7. Script SUBIR sin `USE` y no repetible.**
`015_p6_creditos_busquedas_SUBIR.sql:1-10` no fija la base (es igual que B-4 de P5b). Tampoco se puede repetir entero: si falla a mitad, volver a importarlo falla en `ADD KEY IX_Lead_Usuario` (`:32`) o en `CREATE TABLE` (`:49`, `:79`).
- Corrección: seleccionar `camion_ventas` antes de importar y, si falla, seguir el rollback parcial (más abajo).

**B-8. Cuatro ventanas sin CHECK entre cada DROP y su ADD.**
`015_p6_creditos_busquedas_SUBIR.sql:14-15`, `:25-26`, `:35-36` y `:42-43`. El riesgo es bajo con la API detenida. Ver la valoración de la migración.

**B-9. BAJAR puede fallar al quitar `IX_Lead_Usuario`.**
`015_p6_creditos_busquedas.sql:206`: el índice `FK_Lead_Usuario` lo creó InnoDB de forma implícita para la FK (migración 013, `013_p5_leads_SUBIR.sql:35`, sin `KEY` explícita). InnoDB puede retirar ese índice implícito cuando aparece otro que sirve a la FK, como `IX_Lead_Usuario (usuario_id, creado_en)`. En ese caso, `DROP KEY IX_Lead_Usuario` fallaría con el error 1553 ("needed in a foreign key constraint"). No se pierden datos: solo falla esa sentencia.
- Corrección: en la verificación posterior, comprobar con `SHOW INDEX FROM leads` si `FK_Lead_Usuario` sigue existiendo. Si no existe, en BAJAR usar `ALTER TABLE leads ADD KEY FK_Lead_Usuario (usuario_id), DROP KEY IX_Lead_Usuario;` en una sola sentencia.

### Informativos (sin acción)

- **I-1. D-55 B se cumple:**
  - `CreditoRequest` (`backend/Camion.Api/Contracts/LeadsContracts.cs:40-50`) no tiene monto, cuota, ingresos ni tipo de vehículo.
  - `SolicitudesEndpoints.cs:190` deserializa con `System.Text.Json`, que por defecto descarta los campos desconocidos. Por eso lo financiero que llegue en el JSON no se guarda en ninguna tabla.
  - No se registra en ningún log: no hay `ILogger` en `SolicitudesEndpoints`, `MisSolicitudesEndpoints`, `ReglasBusqueda` ni `ReglasLeads`; no se usa `EnableSensitiveDataLogging` ni `HttpLogging`; el manejador global (`Program.cs:173-179`) registra el tipo, la ruta y la pila, nunca el cuerpo ni el mensaje.
  - La auditoría guarda solo `origen`, `responsableAsesorId`, `conCuenta` y `conVehiculo` (`SolicitudesEndpoints.cs:167-174`).
  - `leads_credito` no existe. La prueba `CreditosTests.cs:100-118` envía campos financieros manipulados y comprueba que no se guardan (NO EJECUTADA por mí).
  - Riesgo residual: el comentario libre. Lo mitiga la ayuda "No escribas ingresos, documentos ni datos bancarios" (`frontend/src/api/solicitudes.ts:84`).
- **I-2. Presupuesto de la búsqueda.** `presupuesto_max_cop` se guarda en `leads_busqueda` y en el texto del repetido (`ReglasBusqueda.cs:142`). Lo decidió D-56 a A, y es un dato de la compra, no de la situación financiera del titular. No va a la auditoría.
- **I-3. Endpoints anónimos.** `POST /api/creditos` y `/api/busquedas` (`SolicitudesEndpoints.cs:25-31`) llevan `AllowAnonymous` y `LimitePeticionesContacto`. El orden es 413 (`:184-186`, por `Content-Length` y por lo leído) → 400 formato (`:190-196`) → trampa con 201 sin guardar (`:46`, `:82`) → 400 campos → 404 o 409 → 429 → repetido → nuevo.
  - Validación: el titular usa las mismas reglas de P5 y P5b. El comentario y la descripción admiten hasta 500. El slug se recorta (`:60`).
  - Criterios: se validan contra catálogos activos, la línea debe ser de la marca y de la categoría (D-32), el municipio del departamento, los años de 1950 al año actual + 1 y el presupuesto de 1.000.000 a 5.000.000.000 (`ReglasBusqueda.cs:20-86`). El CHECK de la base repite los rangos.
  - Solo hay LINQ parametrizado.
  - Asignación masiva: los DTO no tienen origen, estado, responsable ni `publicacionId`. El servidor fija `ResponsableAsesorId = null` (`:144`).
  - La respuesta nunca incluye el id.
  - Enumeración: el 404 por slug solo revela si una publicación es visible, y eso ya es público.
- **I-4. "Mis solicitudes".** El grupo usa `SoloComprador` (`MisSolicitudesEndpoints.cs:31`, política en `Program.cs:84`): el asesor y el administrador reciben 403 y sin token, 401.
  - IDOR: la lista y el detalle filtran `l.UsuarioId == usuarioId` (`:43`, `:59`). El detalle ajeno da 404. `usuarioId` nunca es nulo, porque `OnTokenValidated` rechaza un token sin `NameIdentifier` (`Program.cs:66-72`).
  - La proyección (`:68-80`, `MiSolicitudItem`) no incluye notas, historial, responsable, resultado, observación, consentimientos, datos de contacto ni mensaje. De la nota interna de las relaciones solo usa los ids (`:100`), y las tarjetas recomendadas son solo de publicaciones visibles.
  - Es solo lectura: no hay POST, PUT ni DELETE.
  - El `id` del lead es secuencial y deja ver el volumen aproximado de solicitudes (dato menor).
- **I-5. `/api/leads`.**
  - El alcance del asesor (`LeadsEndpoints.cs:357-360`) excluye créditos y búsquedas también en la lista, el resumen, el detalle, las notas, el cierre, las coincidencias y las relaciones (vía `PrepararAsync`).
  - `CK_Lead_AvisoAdmin` impide en la base que un crédito o una búsqueda tengan un asesor responsable.
  - Reasignar un crédito o una búsqueda da 409 (`:319`, `Lead.EsAviso`).
  - Relacionar exige una publicación visible (`:195-197`, `ConsultasCatalogo.Visibles`).
  - Relacionar y retirar exigen además una búsqueda abierta, solo el administrador, y quedan auditados (`:216-217`, `:245-246`).
  - El barrido D-46 y el seguimiento de vehículos (`BarridoLeads.cs:49`, `:99`) solo tocan leads con un asesor responsable, así que no mueven créditos con publicación.
- **I-6. Frontend.**
  - No hay `dangerouslySetInnerHTML` ni `innerHTML`. Los dos `target="_blank"` nuevos (`frontend/src/components/BusquedaAdmin.tsx:226`, `:287`) llevan `rel="noopener noreferrer"`. Los slugs pasan por `encodeURIComponent` (`frontend/src/rutas.ts:72`, `:124`).
  - El token se envía solo con una sesión activa con perfil (`frontend/src/App.tsx:201-202`) y solo a rutas relativas (`frontend/src/api/solicitudes.ts:381-382`, `frontend/src/api/misSolicitudes.ts:31`, `frontend/src/api/leads.ts:537`), en la cabecera `Authorization` (`frontend/src/api/http.ts:110`).
  - En la ficha del crédito, solo el asesor envía su token, para el 409 de vehículo propio.
  - Los errores 5xx y de red muestran un mensaje genérico.
- **I-7. Nombres de tabla.** La 015 referencia `AspNetUsers`, y en esta instalación la tabla se llama `aspnetusers` (`lower_case_table_names = 1`), así que funciona. Es algo a tener en cuenta en un despliegue en Linux (ya aplicaba desde la 013).

## Migración 015

**Reversible y sin borrar datos.**
- SUBIR (`015_p6_creditos_busquedas_SUBIR.sql`) hace estos cambios:
  - amplía cuatro CHECK (`CK_Lead_Origen`, `CK_Lead_Resultado`, `CK_Consent_Formulario`, `CK_EvLead_Tipo`);
  - añade el índice `IX_Lead_Usuario`;
  - crea dos tablas nuevas.
- No hay DROP TABLE, DROP COLUMN, TRUNCATE, DELETE ni UPDATE.
- Los únicos DROP son `DROP CONSTRAINT` de un CHECK, y cada uno va seguido de inmediato de su `ADD CONSTRAINT` ampliado (`:14-21`, `:25-29`, `:35-39`, `:42-46`). Es lo permitido.
- BAJAR está comentada (`015_p6_creditos_busquedas.sql:183-228`) y la protegen seis comprobaciones (`:184-191`). Sus `DROP TABLE` (`:194-195`) solo afectan a las dos tablas que crea la 015, y solo se ejecutan si las comprobaciones dan 0 (tablas vacías).

**Cada CHECK nuevo es un superconjunto del anterior.**
- `CK_Lead_Origen` conserva sus tres ramas sin cambios y añade `CREDITO` y `BUSQUEDA`.
- Los otros tres CHECK solo añaden valores a la lista.
- Por eso las filas actuales cumplen las versiones nuevas. Lo comprobé con los datos reales:
  - los 3 contactos tienen publicación y `tipo_vehiculo` NULL;
  - los 2 "Vende tu camión" no tienen publicación ni tipo;
  - el seguro no tiene publicación y su tipo es `MAQUINARIA`;
  - los resultados son todos NULL;
  - los tipos de evento y los formularios están dentro de las listas nuevas.

**Objetos nuevos sin colisiones.** No existen tablas ni restricciones con los nombres nuevos. Los tipos de las FK coinciden:
- `id BIGINT` en `tipos_vehiculo`, `marcas`, `lineas`, `departamentos`, `municipios`, `publicaciones` y `leads`;
- `aspnetusers.Id` es `VARCHAR(255) utf8mb4_general_ci`, igual que el cotejamiento por defecto con el que se crean las tablas nuevas.

Todas las tablas son InnoDB. **Conclusión: con los datos actuales, la 015 no debería fallar** (análisis estático; NO EJECUTADA).

**FK.**
- Todas son `RESTRICT`, salvo `relacionado_por` y `retirado_por`, que son `SET NULL` hacia los usuarios.
- Ninguna es `CASCADE`, así que borrar un lead o una publicación no arrastra criterios ni relaciones.

**No es atómica.** Cada `ALTER` y cada `CREATE` hace commit implícito (está documentado en `015_p6_creditos_busquedas.sql:25-26`). Con 6 filas, la copia de cada tabla es inmediata.

**EF alineado.** `backend/Camion.Api/Data/CamionDbContext.cs` declara las mismas tablas, CHECK, índices y comportamientos de borrado.

### Procedimiento para la responsable (NO ejecutado por este agente)

1. **Detener la API.**
   - La versión P6 lee `leads_busqueda` y `busqueda_vehiculos`, y guarda `CREDITO` y `BUSQUEDA`, que la base actual rechaza.
   - Mientras la API esté detenida, nadie escribe en las cuatro ventanas sin CHECK (B-8).
2. **Respaldo previo verificable.** En una consola en `C:\xampp\mysql\bin`:
   - Primero, `dir C:\Respaldo_CAMION\` para confirmar que el nombre que se va a usar no existe. Nunca sobrescribir ni borrar respaldos.
   - Después:
     `mysqldump -u root -p --single-transaction --routines --triggers --databases camion_ventas --result-file=C:\Respaldo_CAMION\camion_ventas_antes_015_AAAAMMDD_HHMM.sql`
   - Para verificarlo: el archivo pesa más de 0 bytes, la última línea es `-- Dump completed …` y contiene `CREATE TABLE \`leads\``, `INSERT INTO \`leads\`` y `CREATE TABLE \`consentimientos_datos\``. Por ejemplo: `findstr /C:"Dump completed" /C:"CREATE TABLE \`leads\`" C:\Respaldo_CAMION\camion_ventas_antes_015_AAAAMMDD_HHMM.sql`.
   - Anotar el nombre, el tamaño y la hora en `docs/coordinacion/BITACORA.md`.
3. **Verificación antes.** El coordinador ejecuta con el cliente mysql el bloque "VERIFICACIÓN ANTES" (`015_p6_creditos_busquedas.sql:39-57`). Esperado:
   - las dos tablas nuevas no existen;
   - `ix_lead_usuario = 0`;
   - existen los 4 CHECK;
   - los conteos son 6 / 20 / 6 (o los vigentes ese día, que se anotan);
   - las cuatro consultas `*_fuera_de_lista` dan 0.
   - Además, anotar la salida de `SHOW INDEX FROM leads WHERE Key_name = 'FK_Lead_Usuario';` (esperado: una fila), por B-9.
4. **Aplicación.** En phpMyAdmin, **seleccionar primero la base `camion_ventas`** en el panel izquierdo (B-7). Después, en Importar, cargar **solo** `database/migraciones/015_p6_creditos_busquedas_SUBIR.sql`. Si hay un error, detenerse y no reimportar el archivo (ver Rollback).
5. **Verificación después.** El coordinador ejecuta el bloque "VERIFICACIÓN DESPUÉS DE SUBIR" (`015_p6_creditos_busquedas.sql:156-175`). Esperado:
   - los mismos tres conteos que en el paso 3;
   - los orígenes `CONTACTO_VEHICULO` 3, `VENDE_TU_CAMION` 2 y `SEGURO_TODO_RIESGO` 1;
   - `CK_Lead_Origen` con `CREDITO` y `BUSQUEDA`;
   - `CK_Lead_Resultado` con `REMITIDO_ENTIDAD`, `VEHICULO_ENCONTRADO` y `SIN_VEHICULO`;
   - `CK_Consent_Formulario` con `CREDITO` y `BUSQUEDA`;
   - `CK_EvLead_Tipo` con `VEHICULO_RELACIONADO` y `VEHICULO_RETIRADO`;
   - `CK_Lead_AvisoAdmin`, `CK_Lead_Estado` y `CK_Lead_Cierre` sin cambios;
   - `CK_LeadBusq_Anios` y `CK_LeadBusq_Presupuesto` presentes;
   - 6 FK en `leads_busqueda` y 2 en `busqueda_vehiculos` con `RESTRICT`, más 2 con `SET NULL`;
   - `IX_Lead_Usuario (usuario_id, creado_en)` y `UX_BusqVeh_LeadPub` (único) presentes;
   - `criterios = 0` y `relaciones = 0`.
   - Repetir `SHOW INDEX FROM leads WHERE Key_name IN ('FK_Lead_Usuario', 'IX_Lead_Usuario');` y anotar si `FK_Lead_Usuario` sigue existiendo (B-9).
6. **Prueba funcional con datos ficticios.** Arrancar la API de P6 y comprobar:
   - Enviar un crédito desde la portada, otro desde una ficha y una búsqueda.
   - Los tres aparecen en "Contactos" del administrador con el estado "Registrado".
   - Un asesor no los ve, tampoco el crédito de su vehículo.
   - Un comprador con sesión los ve en "Mis solicitudes", sin notas ni resultado.
   - En el detalle del crédito no hay monto ni cuota.

### Rollback

- **Fallo a mitad de SUBIR.** No volver a importar el archivo entero (B-7). Con la verificación posterior, ver qué sentencias se aplicaron.
  - Si falló un `ADD CONSTRAINT` justo después de su `DROP`, ejecutar solo ese `ADD CONSTRAINT`, que es superconjunto del anterior, y continuar con las sentencias siguientes del archivo una a una.
  - Si se prefiere volver atrás, ejecutar del bloque BAJAR (`015_p6_creditos_busquedas.sql:194-218`) solo las partes que correspondan a lo aplicado.
- **Revertir sin créditos ni búsquedas.** Si las seis comprobaciones de BAJAR (`:184-191`) dan 0:
  - Con la API detenida, ejecutar el paso 2 de BAJAR (`:194-218`).
  - Si en el paso 5 `FK_Lead_Usuario` ya no existía, sustituir `:206` por `ALTER TABLE leads ADD KEY FK_Lead_Usuario (usuario_id), DROP KEY IX_Lead_Usuario;` (B-9).
  - Luego, ejecutar la verificación de BAJAR (`:222-227`) y volver a desplegar la API de P5b.
- **Revertir con créditos, búsquedas o relaciones.** No se baja sin una decisión de la responsable.
  - Primero se exportan con un nombre nuevo:
    `mysqldump -u root -p --single-transaction camion_ventas leads eventos_lead consentimientos_datos leads_busqueda busqueda_vehiculos auditoria_eventos --result-file=C:\Respaldo_CAMION\p6_creditos_export_AAAAMMDD_HHMM.sql`
    (`auditoria_eventos` es el nombre real de la tabla de auditoría, comprobado con `SHOW TABLES`.)
  - Otra opción es restaurar el respaldo del paso 2, pero se perdería todo lo escrito después; esa restauración la decide la responsable.
