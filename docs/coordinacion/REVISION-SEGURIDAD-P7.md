# Revisión de seguridad — P7 Ventas, pagos, parámetros y panel (HU-064, HU-066, HU-067, HU-074, HU-075, HU-081; D-61 a D-64)

- Rama revisada: `feature/p7-ventas-admin`, diff `origin/main...HEAD` (61 archivos, hasta `99f973a`).
- Fecha: 2026-11-12. Revisor: devops-seguridad. Solo leí código y ejecuté consultas SELECT/SHOW/DESCRIBE contra `camion_ventas`. No modifiqué código, pruebas ni la base, y no leí user-secrets ni archivos `appsettings*`.
- Referencias: `CLAUDE.md`, `docs/criterios/P7-contrato-api.md`, `docs/criterios/criterios-P7-ventas-admin.md`, `DECISIONES.md` (D-44, D-61 a D-64), `database/migraciones/016_p7_pagos.sql` y `016_p7_pagos_SUBIR.sql`, `REVISION-SEGURIDAD-P6.md`.

## Veredicto: APROBADO

No hay hallazgos críticos, altos ni medios. Los datos financieros del pago solo los ve y los escribe el administrador. El historial de ventas respeta el alcance del inventario. Los enlaces de Instagram, TikTok y WhatsApp se validan en el servidor y otra vez en el navegador. El panel devuelve solo números. Los hallazgos bajos pueden ir al backlog. M-1 (límite por IP sin cabeceras reenviadas) y MySqlConnector 2.3.7 siguen en INF-2. La migración 016 se puede aplicar con el procedimiento de este documento.

## Verificaciones ejecutadas

| Verificación | Resultado |
|---|---|
| Secretos en las líneas añadidas (password, `server=`, cadenas de conexión, JWT `eyJ…`, llaves privadas, `Bearer` literal) | Sin hallazgos. El README solo nombra las claves `ConnectionStrings:CamionDatabase` y `Jwt:Key`, con marcadores `<TU_…>` |
| Datos personales en el diff | Solo ficticios (`3001234567`, `3170000001`, `3180000002`, `*@ejemplo.test`, `*@prueba.test`). El 316 615 2907 y `vc.automotores@gmail.com` son el contacto público del portal (D-42) |
| Archivos de respaldo, `.env` o volcados en el diff | Ninguno. `frontend/respaldos/` sigue sin seguimiento y no se tocó |
| Dependencias nuevas (`*.csproj`, `package*.json`) | Ninguna |
| `npm audit --omit=dev` | 0 vulnerabilidades |
| `dotnet list package --vulnerable --include-transitive` | Solo MySqlConnector 2.3.7 (GHSA-473q-m89c-ghf8, moderada), ya en INF-2 |
| Logs | Ninguna línea añadida usa `ILogger`, `Log*` ni `Console`. Valor, medio y referencia no van a ningún log |
| `dotnet build` / `dotnet test` / `npx vitest run` | NO EJECUTADO por este agente. QA ejecuta las pruebas en paralelo |
| Consultas de solo lectura a `camion_ventas` | MariaDB 10.4.32. `pagos` tiene 0 filas, las 9 columnas del script 002, los CHECK `CK_Pago_Valor` y `CK_Pago_Estado` (3 valores), las FK `pagos_ibfk_1` y `pagos_ibfk_2`, y los índices `PRIMARY`, `IX_Pago_Suscripcion` y `verificado_por`. No existen las columnas nuevas ni `FK_Pago_AnuladoPor`. `aspnetusers.Id` es `VARCHAR(255) utf8mb4_general_ci`. `configuracion_sistema` tiene (`clave` PK, `valor_json` longtext, `actualizado_por`, `actualizado_en`) y 0 filas. Otros conteos: suscripciones 2, planes 2, ventas_vehiculo 1, auditoria_eventos 65 |
| Migración 016 ejecutada | NO EJECUTADA (está prohibido escribir en cualquier base). Hice un análisis estático contra la estructura y los datos reales |

## Hallazgos por severidad

### Crítica / Alta / Media
Ninguno.

- Recordatorio M-1 (P5, en INF-2): `backend/Camion.Api/Program.cs:104`. La única ruta anónima nueva es `GET /api/portal/contacto`, que es de solo lectura. No suma un formulario al cupo por IP.

### Baja

**B-1. El valor guardado de `portal.contacto` no se vuelve a validar al leerlo.**
`backend/Camion.Api/Parametros/ParametrosPortal.cs:70-83`: `Deserializar` solo comprueba que WhatsApp, correo y horario no estén vacíos. Si alguien edita `valor_json` a mano, la ruta pública (`ParametrosPortal.cs:64`) y la ficha (`CatalogoEndpoints.cs:195-197`, vía `WhatsappConIndicativoAsync`) devolverían ese valor tal cual: por ejemplo, un `instagram` con `javascript:` o un WhatsApp con `?text=…`.
- Hoy no es explotable desde la API: el PUT valida todo (`ParametrosPortal.cs:100-124`, `:130-144`), y el navegador filtra otra vez. El pie solo pinta redes que pasan `enlaceRedSeguro` (`frontend/src/api/parametros.ts:104-106`). El enlace de WhatsApp tiene que cumplir `^https://wa\.me/\d{10,15}$` (`parametros.ts:183`) y el de la ficha `^https://wa\.me/\d{8,15}$` (`frontend/src/api/contactos.ts:156`).
- Corrección (defensa en profundidad): en `LeerAsync`, pasar lo leído por `Validar` (o por las mismas reglas) y, si no cumple, usar `PorDefecto`.

**B-2. El correo del portal admite `?`, `&`, `<`, `>` y comillas.**
`backend/Camion.Api/Auth/ValidacionAuth.cs:27` (`^[^@\s]+@[^@\s]+\.[^@\s]+$`) y `frontend/src/api/parametros.ts:79`. El pie arma `mailto:${correo}` (`frontend/src/components/PiePortal.tsx:122`). Un valor como `a@b.co?bcc=otro@x.co` añadiría destinatarios al correo que escribe el visitante. React escapa el atributo, así que no hay XSS. Solo lo puede escribir el administrador.
- Corrección: en `ParametrosPortal.Validar` (`:108-109`), rechazar `? & < > " ' %` en el correo del portal, o armar el enlace con `encodeURIComponent` en `PiePortal.tsx:122`.

**B-3. La referencia del pago admite caracteres de control y saltos de línea.**
`backend/Camion.Api/Suscripciones/ReglasPagos.cs:42-46`: solo se excluyen `<` y `>`. No hay riesgo de XSS (React escapa y el valor solo lo ve el administrador), pero un salto de línea o un carácter invisible puede confundir al leer o exportar.
- Corrección: añadir `|| referencia.Any(char.IsControl)`, igual que en los enlaces (`ParametrosPortal.cs:136`).

**B-4. Anular un pago de estado antiguo deja una auditoría inexacta.**
`backend/Camion.Api/Endpoints/PagosEndpoints.cs:71-80`: el UPDATE acepta cualquier estado distinto de `ANULADO` (también `PENDIENTE`, `APROBADO` o `RECHAZADO` del script 002), pero la auditoría escribe siempre `estado: REGISTRADO` como estado anterior. Hoy `pagos` tiene 0 filas y la API solo crea `REGISTRADO`, así que no ocurre.
- Corrección: condicionar a `p.Estado == Pago.Registrado` (`:71`) y responder 409 en los demás casos, o leer el estado antes de cambiarlo y auditar el real.

**B-5. Script SUBIR sin `USE` y no repetible.**
`database/migraciones/016_p7_pagos_SUBIR.sql:1-9` no fija la base, igual que B-7 de P6. Si falla a mitad, volver a importarlo falla en `ADD COLUMN fecha_pago` (`:14`, columna duplicada).
- Corrección: seleccionar `camion_ventas` antes de importar y, si falla, seguir el rollback parcial (más abajo).

**B-6. Una ventana sin `CK_Pago_Estado` entre el DROP y el ADD.**
`016_p7_pagos_SUBIR.sql:24-26`. Con la API detenida y la tabla vacía, el riesgo es nulo. Ya está documentado en `016_p7_pagos.sql:23-24`.

### Informativos (sin acción)

- **I-1. Pagos: solo el administrador.**
  - Las tres rutas nuevas están en un grupo con `PoliticaSoloAdministrador` (`backend/Camion.Api/Endpoints/PagosEndpoints.cs:20`). Asignar, renovar y cambiar de plan con `pago` están en el grupo de administrador de `SuscripcionesEndpoints.cs:26`.
  - `GET /api/asesor/suscripcion` (`SuscripcionesEndpoints.cs:226-231`) solo añade `avisoVencimiento` y `mensajeVencimiento`; no tiene valor, medio, referencia ni total (CA-P7-32).
  - `PagoItem` muestra solo el nombre de quien registró o anuló (`PagosEndpoints.cs:96-107`), nunca su correo ni su teléfono.
  - Auditoría: `REGISTRAR` con suscripción, valor, fecha, medio, referencia, concepto y estado (`ReglasPagos.cs:69-78`), y `ANULAR` con estado y motivo (`PagosEndpoints.cs:78-80`). Las dos van en la misma transacción que el cambio. No hay ruta que lea `auditoria_eventos` para otro rol.
  - La anulación usa un UPDATE condicionado (`:71-77`): de dos anulaciones simultáneas, una recibe 409. La fila nunca se borra.
  - El pago se valida antes de abrir la transacción, y un plan sin precio da 400 (`SuscripcionesEndpoints.cs:85`, `:92`, `:114`, `:124`, `:162`, `:169`). Valor entero, mayor que 0 y dentro de DECIMAL(15,2); fecha de hoy o anterior; medio de lista fija (`ReglasPagos.cs:30-50`). El CHECK de la base repite las listas.
- **I-2. Historial de ventas.**
  - Las dos rutas exigen `AsesorOAdministrador` (`backend/Camion.Api/Endpoints/VentasListaEndpoints.cs:72`, `:86`). El comprador recibe 403 y sin sesión, 401.
  - IDOR: las dos consultas hacen join con `InventarioEndpoints.Alcance` (`:97-100`). El historial de un vehículo ajeno o inexistente da 404 (`:80-81`), sin revelar que existe.
  - El asesor no puede filtrar por asesor; el parámetro se ignora (`:51`) y `asesor` llega `null` (`:128`).
  - Sin datos de compradores ni de leads (CA-P7-12). El asesor sí ve las observaciones, el motivo de reversión y el nombre de quien registró o revirtió (`:132-138`), y también las ventas anteriores de un vehículo que hoy es suyo. Lo decidió D-62 A.
  - Fechas con formato estricto, `tamano` como máximo 100, y solo LINQ parametrizado.
- **I-3. Parámetros de contacto.**
  - La ruta pública `GET /api/portal/contacto` (`backend/Camion.Api/Endpoints/ParametrosEndpoints.cs:20-23`) devuelve solo WhatsApp, enlace, correo, horario, Instagram y TikTok. No incluye `configurado`, `actualizadoEn` ni `actualizadoPor`, que solo están en la ruta de administrador (`:25-60`, `SoloAdministrador`).
  - Enlaces (`ParametrosPortal.cs:130-144`): https, host exacto (`instagram.com`, `www.instagram.com`, `tiktok.com`, `www.tiktok.com`), sin usuario ni puerto, con ruta, sin espacios, controles, `< > " ' \`. Rechaza `javascript:`, `data:`, `http:`, `www.instagram.com.evil.com` y `www.instagram.com@evil.com` (UserInfo no vacío). El navegador repite la regla (`frontend/src/api/parametros.ts:82-98`).
  - El WhatsApp debe ser un celular colombiano de 10 dígitos (`ParametrosPortal.cs:105-106`, `ValidacionAuth.cs:125-141`). El servidor arma `https://wa.me/57` más esos dígitos (`:62`). La ficha usa `ReglasLeads.EnlaceWhatsapp`, que concatena solo dígitos. No se puede inyectar en el enlace wa.me.
  - El horario no admite `< >` (`:111-114`) y React lo pinta como texto (`PiePortal.tsx:127-129`).
  - Los tres `target="_blank"` del pie llevan `rel="noopener noreferrer"` (`PiePortal.tsx:117`, `:135`, `:143`).
  - Valor y auditoría `Parametro/CAMBIAR` van en la misma transacción; si no hay cambios, no se audita (`ParametrosEndpoints.cs:38-57`). Ningún valor de negocio es editable (D-63 C descartada).
- **I-4. Panel.** `GET /api/admin/panel` exige `PoliticaSoloAdministrador` (`backend/Camion.Api/Endpoints/PanelEndpoints.cs:75`). Devuelve solo conteos y la fecha `ventasDesde`: ningún nombre, teléfono, correo ni suma de dinero (CA-P7-48, CA-P7-51). Antes de contar ejecuta el barrido de D-46 (`:31`), igual que `GET /api/leads/resumen`; un GET que escribe es un patrón heredado y solo lo puede llamar el administrador.
- **I-5. Frontend.**
  - No hay `dangerouslySetInnerHTML`, `innerHTML`, `eval`, `localStorage` ni `console` en las líneas añadidas.
  - El token se envía en la cabecera `Authorization` mediante `peticion` (sin cambios en `frontend/src/api/http.ts`), y solo a rutas relativas (`/api/admin/...`, `/api/ventas`, `/api/inventario/...`). La lectura pública del contacto va sin token (`frontend/src/api/parametros.ts:205-210`).
  - El id del vehículo pasa por `encodeURIComponent` (`frontend/src/api/ventas.ts:188`) y los filtros por `URLSearchParams` (`:163-170`). `asesorId` solo se envía si es administrador (`:168`), aunque la barrera real es el servidor.
  - Las secciones `inicio`, `ventas` y `parametros` del administrador se pintan dentro de la vista de administrador (`frontend/src/App.tsx`); el servidor responde 403 a cualquier otro rol.
- **I-6. EF alineado.** `backend/Camion.Api/Data/CamionDbContext.cs` declara para `pagos` los mismos CHECK, columnas, índice y FK `SET NULL` que la 016. `configuracion_sistema` se mapea sin cambios de esquema.
- **I-7. FK `SET NULL`.** Si se borra la cuenta de un administrador, `verificado_por`, `anulado_por` y `actualizado_por` quedan en NULL; el actor sigue en `auditoria_eventos`.

## Migración 016

**Reversible y sin borrar datos.**
- SUBIR (`016_p7_pagos_SUBIR.sql`) solo hace `ALTER TABLE pagos`:
  - añade seis columnas NULL y `FK_Pago_AnuladoPor` (`:13-20`);
  - amplía `CK_Pago_Estado` (`:24-26`);
  - añade cuatro CHECK nuevos (`:29-33`).
- No hay DROP TABLE, DROP COLUMN, TRUNCATE, DELETE ni UPDATE. El único DROP es `DROP CONSTRAINT CK_Pago_Estado` (`:24`), seguido de inmediato de su `ADD CONSTRAINT` ampliado (`:25-26`). Es lo permitido.
- El bloque SUBIR de `016_p7_pagos.sql:53-73` es idéntico al archivo `_SUBIR`.
- BAJAR está comentada (`016_p7_pagos.sql:97-129`) y la protege una comprobación (`:98-99`). Sus `DROP COLUMN` solo afectan a las seis columnas que crea la 016, y solo se ejecutan si no hay pagos de P7. Si los hay, exige decisión de la responsable y exportación previa.

**Compatible con los datos actuales.**
- `pagos` tiene 0 filas: ninguna fila puede incumplir los CHECK nuevos, y las columnas NULL no exigen valor por defecto.
- `CK_Pago_Estado` nuevo es un superconjunto del anterior (añade `REGISTRADO` y `ANULADO`).
- No existen columnas ni restricciones con los nombres nuevos. `anulado_por VARCHAR(255)` coincide en tipo y cotejamiento con `aspnetusers.Id`. La tabla es InnoDB.
- `AspNetUsers` funciona porque `lower_case_table_names = 1` (igual que I-7 de P6).
- MariaDB 10.4 admite `DROP CONSTRAINT` para CHECK.
- **Conclusión: con los datos actuales, la 016 no debería fallar** (análisis estático; NO EJECUTADA).

**BAJAR.** `DROP KEY FK_Pago_AnuladoPor` (`016_p7_pagos.sql:112`) es correcto: InnoDB crea el índice implícito de la FK con el nombre de la restricción, y BAJAR quita antes la FK (`:110`).

### Procedimiento para la responsable (NO ejecutado por este agente)

1. **Detener la API.** La versión P7 mapea las columnas nuevas de `pagos`; con la base actual fallaría al leer o escribir pagos. Además, nadie escribe durante la ventana de B-6.
2. **Respaldo previo verificable.** En una consola en `C:\xampp\mysql\bin`:
   - Primero, `dir C:\Respaldo_CAMION\` para confirmar que el nombre que se va a usar no existe. Nunca sobrescribir ni borrar respaldos.
   - Después:
     `mysqldump -u root -p --single-transaction --routines --triggers --databases camion_ventas --result-file=C:\Respaldo_CAMION\camion_ventas_antes_016_AAAAMMDD_HHMM.sql`
   - Para verificarlo: el archivo pesa más de 0 bytes, la última línea es `-- Dump completed …` y contiene `CREATE TABLE \`pagos\``, `CREATE TABLE \`suscripciones\`` e `INSERT INTO \`suscripciones\``. Por ejemplo: `findstr /C:"Dump completed" /C:"CREATE TABLE \`pagos\`" /C:"INSERT INTO \`suscripciones\`" C:\Respaldo_CAMION\camion_ventas_antes_016_AAAAMMDD_HHMM.sql`.
   - Anotar el nombre, el tamaño y la hora en `docs/coordinacion/BITACORA.md`.
3. **Verificación antes.** El coordinador ejecuta con el cliente mysql el bloque "VERIFICACIÓN ANTES" (`016_p7_pagos.sql:37-45`). Esperado:
   - ninguna de las seis columnas nuevas;
   - `CK_Pago_Valor` y `CK_Pago_Estado` con 3 valores;
   - conteos pagos 0, suscripciones 2, planes 2, ventas 1, auditoría 65, parámetros 0 (o los vigentes ese día, que se anotan);
   - `estado_fuera_de_lista = 0`.
4. **Aplicación.** En phpMyAdmin, **seleccionar primero la base `camion_ventas`** en el panel izquierdo (B-5). Después, en Importar, cargar **solo** `database/migraciones/016_p7_pagos_SUBIR.sql`. Si hay un error, detenerse y no reimportar el archivo (ver Rollback).
5. **Verificación después.** El coordinador ejecuta el bloque "VERIFICACIÓN DESPUÉS DE SUBIR" (`016_p7_pagos.sql:81-89`). Esperado:
   - los seis conteos iguales a los del paso 3;
   - `fecha_pago`, `medio`, `concepto`, `anulado_por`, `anulado_en` y `motivo_anulacion` presentes y con `IS_NULLABLE = YES`;
   - `CK_Pago_Estado` con 5 valores; `CK_Pago_Valor` sin cambios; `CK_Pago_Medio`, `CK_Pago_Concepto`, `CK_Pago_Registro` y `CK_Pago_Anulacion` presentes;
   - `FK_Pago_AnuladoPor` hacia `aspnetusers` con `DELETE_RULE = SET NULL`, junto a `pagos_ibfk_1` y `pagos_ibfk_2` sin cambios.
   - Además, `SHOW INDEX FROM pagos;` debe mostrar el índice `FK_Pago_AnuladoPor`.
6. **Prueba funcional con datos ficticios.** Arrancar la API de P7 y comprobar:
   - Renovar un plan con pago: aparece en "Pagos" de la suscripción con el total.
   - Anular ese pago con motivo: queda "Anulado", el total baja y la suscripción no cambia.
   - Un asesor no ve pagos en "Mi suscripción" y recibe 403 en `/api/admin/suscripciones/{id}/pagos`.
   - Guardar Instagram con `javascript:alert(1)` da 400; con `https://www.instagram.com/cuenta` aparece en el pie.
   - El asesor solo ve sus ventas en `/api/ventas`, y el panel `Inicio` solo muestra números.

### Rollback

- **Fallo a mitad de SUBIR.** No volver a importar el archivo entero (B-5). Con la verificación posterior, ver qué sentencias se aplicaron.
  - Si falló el `ADD CONSTRAINT CK_Pago_Estado` justo después de su `DROP` (`:25-26`), ejecutar solo ese `ADD CONSTRAINT`, que es superconjunto del anterior, y continuar con la sentencia `:29-33`.
  - Si se prefiere volver atrás, ejecutar del bloque BAJAR (`016_p7_pagos.sql:102-118`) solo las partes que correspondan a lo aplicado.
- **Revertir sin pagos de P7.** Si la comprobación de BAJAR (`:98-99`) da 0:
  - Con la API detenida, ejecutar el paso 2 de BAJAR (`:102-118`).
  - Luego, ejecutar la verificación de BAJAR (`:122-128`) y volver a desplegar la API de P6.
  - `configuracion_sistema` no lo toca la 016. Si se guardó `portal.contacto`, la fila queda y la API de P6 la ignora.
- **Revertir con pagos de P7.** No se baja sin una decisión de la responsable.
  - Primero se exportan con un nombre nuevo:
    `mysqldump -u root -p --single-transaction camion_ventas pagos auditoria_eventos --result-file=C:\Respaldo_CAMION\p7_pagos_export_AAAAMMDD_HHMM.sql`
  - Otra opción es restaurar el respaldo del paso 2, pero se perdería todo lo escrito después; esa restauración la decide la responsable.
