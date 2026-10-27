# Bitácora de coordinación

Registro interno del coordinador y los agentes (progreso, decisiones técnicas reversibles, reintentos). La responsable solo recibe decisiones y entregas.

## 2026-10-26 — Configuración del flujo de agentes
- Rama `feature/flujo-agentes-ia` creada desde `main` (`eb1a733`).
- Creados: comando `/coordinar`, 7 agentes especializados, `CLAUDE.md`, reglas de permisos en `.claude/settings.json`, copia en texto de los documentos fuente en `docs/requisitos/`, backlog y registro de decisiones (D-1 a D-5).
- PR [#1](https://github.com/alex03ma-ux/CAMION/pull/1) abierto contra `main` (sin merge).
- La responsable resolvió D-1 a D-5: 87 HU oficiales (CRM fuera de alcance); placa opcional pero única; aprobación obligatoria para todas las cuentas; contraseña elegida al registrarse; estados Crédito Cerrado, Vehículo Vendido y Suscripción Reactivada añadidos. Ver `DECISIONES.md`.
- Corrección de la responsable sobre D-3: solo los asesores requieren aprobación. El comprador es un perfil independiente de registro opcional e inmediato y de solo lectura, que puede pedir seguro o crédito y ofrecer un camión. Las funciones nuevas (seguro y ofrecer un camión) quedan como D-6 PENDIENTE.
- D-6 resuelta: avisos "Vende tu camión" (HU-N01) y "Compra tu seguro todo riesgo" (HU-N02), atendidos por el administrador. Nueva D-7: todo servicio lo atiende el administrador; el asesor solo atiende los contactos de sus vehículos publicados con plan vigente. Añadido el ítem P5b al backlog.
- D-7 completada: el administrador puede reasignar manualmente cualquier solicitud a un asesor, con auditoría.
- Observación preliminar (no es auditoría): el README afirma que no hay autenticación, pero `main` contiene registro/login con Identity y JWT; no existen proyectos de prueba ni CI; el esquema vive en scripts SQL de `database/` sin migraciones EF. Pendiente de confirmar por `auditor-codigo` (P0).

## 2026-10-26 — P0 Auditoría inicial
- Rama `feature/p0-auditoria` desde `main` (`4e41f43`). `auditor-codigo` escribió `ESTADO-REAL.md` (solo lectura; sin cambios de código ni de base de datos).
- Resultado: 5 endpoints (2 de salud, 3 de autenticación); 39 tablas en scripts, 8 mapeadas en EF y 4 usadas; sin migraciones EF, sin pruebas, sin CI. Frontend de una sola pantalla.
- Compilaciones ejecutadas: `dotnet build` 0 errores / 0 advertencias (SDK 10.0.401, objetivo net8); `npm run build` correcto (Vite 7.3.7).
- Contraste con decisiones: el código cumple D-3 (comprador inmediato, rol fijo COMPRADOR, sin endpoints de escritura) y D-5. El esquema SQL no cumple D-2 (sin placa), D-4 (estados) ni D-6 (solicitudes exigen usuario y correo; sin tipos venta/seguro).
- Registradas D-8 a D-11 como PENDIENTE. P2 bloqueado por D-10; P5, P5b y P6 por D-9; estados de P4 por D-11. P1 puede empezar.
- La compilación dejó `frontend/tsconfig.*.tsbuildinfo` sin seguimiento; no se commitean (propuesta: añadirlos a `.gitignore` en P1).
- La responsable resolvió D-8 a D-11: HU-N03 para el registro de comprador; contacto sin cuenta con nombre y teléfono (correo opcional); estados del asesor Pendiente/Aprobado/Rechazado/Bloqueado; se mantiene el estado "Reservado". P2, P5 y P5b desbloqueados.

## 2026-10-26 — P1 Robustecer autenticación
- Rama `feature/p1-autenticacion` desde `main` (`a3212ea`).
- Contrato de API fijado por el coordinador antes de programar (`docs/criterios/P1-contrato-api.md`); `analista-funcional` escribió 81 criterios (`docs/criterios/P1.md`) trazados a HU-N03, HU-059 a HU-062 y RN-SE-006. HU-063 (recuperar contraseña) BLOQUEADO: no hay servicio de correo (D-5).
- Por eficiencia, criterios, backend y frontend se ejecutaron en paralelo sobre el contrato fijado (archivos distintos). Las contradicciones del analista se resolvieron en el contrato:
  - C-1: se aceptan 409 y 423 aunque revelen que el correo existe (decisión técnica reversible).
  - C-2 y C-3: la inactividad solo cierra la sesión local, comprueba la última actividad al cargar y la comparte entre pestañas.
  - C-4: pasa a P2.
  - C-5: el contrato se completó.
- Backend: validación manual con mensajes en español; 409 también en registros simultáneos; 400/401/423 en el login; nuevo `POST /api/auth/logout` (incrementa `SessionVersion`); comprobación de sesión en `OnTokenValidated` para toda la API; log de auditoría sin datos sensibles; JSON malformado → 400 `{ error }`; `global.json` con el SDK 8.0.425.
- Frontend: módulos `api/`, `auth/` y `components/`; sesión en `localStorage` (riesgo XSS aceptado para P1); reglas de contraseña visibles (solo ASCII); cierre por inactividad de 30 min (PROPUESTO); `*.tsbuildinfo` en `.gitignore`.
- Pruebas (nuevas): `backend/Camion.Api.Tests` (xUnit, WebApplicationFactory, SQLite en memoria; nunca toca `camion_ventas`) y Vitest + Testing Library en `frontend/tests`.
  - Defecto D-P1-01: un JSON malformado daba 400 sin cuerpo. Se corrigió.
  - Limitación: el choque de registros simultáneos contra el índice único de MariaDB no se reproduce con SQLite.
- `devops-seguridad`: sin secretos, sin cambios de esquema, `npm audit` sin vulnerabilidades.
  - Bloqueante: SQLite nativo transitivo con GHSA-2m69-gcr7-jv3q. Corregido fijando `SQLitePCLRaw.lib.e_sqlite3` 3.53.3; la 2.1.11 que se propuso al principio también es vulnerable.
  - Recomendaciones aplicadas: errores 500 con el formato común y manejador global de excepciones.
  - Pendiente: CI (INF-1).
- Sin E2E con navegador (Playwright): la verificación manual con la app arrancada queda para la responsable o para un ítem posterior.
- `gh` no está instalado: la rama se publica y el PR lo abre la responsable.
- PR [#3](https://github.com/alex03ma-ux/CAMION/pull/3) abierto para P1. Pruebas verificadas de nuevo por el coordinador principal: 76/76 backend, 42/42 frontend. La responsable resolvió D-12: 30 minutos de inactividad; "Cerrar sesión" en todos los equipos.
- Prueba manual de P1 hecha por la responsable con la app arrancada contra MariaDB `camion_ventas` (2026-10-26): registro de comprador con acceso inmediato, sesión conservada al recargar, cierre de sesión, contraseña incorrecta con mensaje claro, bloqueo de 15 minutos al 5.º intento fallido (los intentos durante el bloqueo se rechazan) e inicio de sesión correcto pasado el bloqueo. Todo según lo esperado; evidencia en el registro de auditoría del servidor. Queda una cuenta de prueba en la base de datos.

## 2026-10-26 — P2 Asesores y administración
- Rama `feature/p2-asesores` desde `main` (`b0a9506`). Contrato `docs/criterios/P2-contrato-api.md`, 109 criterios en `docs/criterios/P2.md`, D-13 y D-14 TOMADAS, D-15 PENDIENTE.
- IMPLEMENTADO (sin pruebas P2 todavía):
  - Backend: registro de asesor, login con 403 por estado, roles y políticas, panel `/api/admin/usuarios`, auditoría, `promover-admin` y migración 003 (sin aplicar). `dotnet build` sin errores; 75 de 76 pruebas de P1 pasan. La que falla es el inventario de endpoints, que hay que actualizar por las rutas nuevas.
  - Frontend: registro de asesor, avisos de estado en el login y panel de administración. `npm run build` correcto; las 42 pruebas de P1 pasan.
- Pendiente:
  - Pruebas de API y de componentes de P2 (los agentes de QA se detuvieron antes de terminar).
  - Revisión de `devops-seguridad`.
  - Documentación (README, guía de autenticación, `docs/GUIA-ADMINISTRACION.md`).
  - **Migración 003 NO aplicada**: la condición 4 de D-14 exige que antes pasen las pruebas de P2.

### P2 — continuación
- D-15 TOMADA (A): reconsiderar asesor rechazado (RECHAZADA → ACTIVA, motivo obligatorio, auditoría RECONSIDERAR), incluido en P2. Añadido P2b ("Solicitar ser asesor") al backlog.
- Pruebas ejecutadas: `dotnet test` 223/223 (147 nuevas de P2; actualizadas `Inventario_de_endpoints` y la de cuenta no activa); `npm test` 93/93 (51 nuevas); `npm run build` correcto; sin defectos de la app.
- `devops-seguridad`: sin bloqueantes; 0 vulnerabilidades (npm y NuGet); recomendaciones aplicadas en la migración 003 (detener la API, ejecutar sin `--force`, alcance de BAJAR). Pendiente: CI (INF-1).
- Documentación: README, `backend/GUIA-AUTENTICACION.md`, nueva `docs/GUIA-ADMINISTRACION.md`.
- Migración 003 (D-14):
  - Respaldo completo nuevo: `C:\Respaldo_CAMION\camion_ventas_completo_antes_migracion003_20261026_191015.sql` (51.366 bytes, 39 tablas, "Dump completed").
  - Conteos antes: AspNetUsers 4, perfiles_asesor 0, perfiles_comprador 4, AspNetUserRoles 4 (historial_autorizaciones 0). La restricción `CK_Asesor_Estado` tiene todavía los valores antiguos.
  - **SUBIR NO aplicado**: el sistema de permisos de esta sesión bloqueó la escritura en `camion_ventas`. Se comprobó que la base sigue sin cambios. Queda para la responsable (pasos en `docs/GUIA-ADMINISTRACION.md`, sección 4) o para una sesión con permiso.
- PR [#4](https://github.com/alex03ma-ux/CAMION/pull/4) abierto para P2. El coordinador principal volvió a ejecutar las pruebas: 223/223 API y 93/93 pantallas.
- D-16: la responsable aplicó la migración 003 (sección SUBIR) en `camion_ventas` desde phpMyAdmin (2026-10-26).
  - Respaldo previo: `C:\Respaldo_CAMION\camion_ventas_completo_antes_migracion003_20261026_191015.sql` (51 KB).
  - Conteos antes y después, iguales: AspNetUsers 4, perfiles_asesor 0, perfiles_comprador 4, AspNetUserRoles 4.
  - `CK_Asesor_Estado` admite ahora PENDIENTE/APROBADO/RECHAZADO/BLOQUEADO; `valores_fuera_de_d10` = 0. Las 7 instrucciones, sin errores.

- Prueba manual de P2 hecha por la responsable contra `camion_ventas` (2026-10-26), confirmada en el registro de auditoría del servidor:
  - Registro como comprador y `promover-admin` (D-13): la cuenta pasó a ADMINISTRADOR, con evento en `auditoria_eventos`.
  - Login de administradora con el botón "Administrar usuarios".
  - Registro de asesor de prueba (`registro_asesor`), aprobación (`admin_aprobar`) y bloqueo (`admin_bloquear`).
  - Login del asesor bloqueado rechazado (`cuenta_no_activa estado=BLOQUEADA`).
  - Sin errores. El registro no muestra el intento de login del asesor en estado Pendiente (cubierto por las pruebas automáticas).
  - Quedan en la base la cuenta de administradora y un asesor de prueba bloqueado.

## 2026-10-26/27 — P3 Catálogo público
- Rama `feature/p3-catalogo` desde `main` (`6a5266f`).
- Contrato `docs/criterios/P3-contrato-api.md`, con aclaraciones. Criterios: 162 en `docs/criterios/P3.md`.
- D-17 (cuatro categorías), D-18 (DEMO autorizado) y D-19 (campos de carrocería y máquina para P4) quedan TOMADAS.
- Sin cambios de esquema: mapeo de solo lectura de las tablas de vehículos, publicaciones y catálogos.
- Regla de visibilidad única (`ConsultasCatalogo.Visibles`). Una publicación se muestra si cumple todo esto:
  - está publicada;
  - está disponible o reservada;
  - está dentro de su vigencia;
  - el plan está ACTIVO y vigente;
  - el asesor está ACTIVO;
  - tiene precio y título.
- Endpoints anónimos GET `/api/catalogo/{inicio, publicaciones, publicaciones/{slug}, filtros}`.
- Frontend: portal con rutas hash (`#/`, `#/buscar`, `#/vehiculo/<slug>`, `#/cuenta`), filtros, tarjetas, detalle con galería y 4 categorías.
- Script `database/demo/demo_p3_catalogo.sql` sin aplicar: 12 publicaciones, de las que 10 son visibles. Lo aplica la responsable (D-16, D-18).
- Los agentes de pruebas se cortaron dos veces (límite de uso y bloqueo de 600 s). El coordinador terminó las pruebas directamente:
  - Defecto corregido (CA-REG-05): si la sesión terminaba por inactividad en el catálogo, no se avisaba.
  - Ajustes de prueba:
    - CA-FIL-08 esperaba un texto genérico; la app muestra "No se pudieron cargar las opciones de los filtros.".
    - CA-DET-08 con `%00` falla en el propio TestHost, no en la API.
- Resultados ejecutados:
  - `dotnet test`: 382/382.
  - `npm test`: 191/191.
  - `npm run build`: correcto.
- Revisión de seguridad (coordinador; no se lanzaron agentes, para evitar otro bloqueo):
  - sin secretos;
  - el catálogo solo tiene GET anónimos sin escrituras;
  - el DEMO solo inserta, en una transacción, y su BAJAR está acotado a las filas DEMO;
  - sin dependencias nuevas.
- Limitaciones conocidas:
  - SQLite no reproduce la intercalación de MariaDB: tildes y mayúsculas en la búsqueda, y mayúsculas en el slug.
  - Las fotos devuelven URL `/media/…`, pero el servicio de archivos llega en P4.
  - Sin E2E en navegador ni CI (INF-1).
- PR [#5](https://github.com/alex03ma-ux/CAMION/pull/5) abierto para P3. El coordinador principal volvió a ejecutar las pruebas: 382/382 API y 191/191 pantallas.
- D-18 aplicada (2026-10-27):
  - El coordinador principal hizo el respaldo completo previo `C:\Respaldo_CAMION\camion_ventas_antes_demo_p3_20261027_102121.sql` (39 tablas).
  - Conteos antes: 6 usuarios, 0 publicaciones, 0 vehículos y catálogos vacíos; existe el rol ASESOR.
  - La responsable importó `database/demo/demo_p3_catalogo.sql` desde phpMyAdmin (Importar, en Chrome): "140 consultas", sin errores.
  - Conteos después: 7 usuarios (+1 asesor DEMO), 12 publicaciones DEMO, 12 vehículos y 2 destacadas. Por categoría: Camión 5, Carro 3, Carrocería 2, Maquinaria 2. Tildes guardadas en UTF-8 correcto.
- Prueba manual de P3 hecha por la responsable con los datos DEMO: inicio con destacados y recientes, las cuatro categorías, filtros con "Limpiar", búsqueda, detalle, etiqueta "Reservado" y carrocería sin kilometraje. Todo según lo esperado.
- Pendiente: ejecutar la sección BAJAR del script DEMO (con respaldo nuevo) antes de cargar datos reales en P4. Las publicaciones DEMO dejan de verse solas a los 30 días.

## 2026-10-27 — P4a Inventario de vehículos
- Rama `feature/p4a-inventario` desde `main` (`de4c7fb`). Contrato `docs/criterios/P4a-contrato-api.md`, 120 criterios en `docs/criterios/P4a.md`.
- Decisiones:
  - TOMADAS: D-20 (fotos), D-21 (planes en P7), D-22 (vendido en P4b), D-23 (migración 004), D-24 (retirar el DEMO tras P4b), D-25 y D-26.
  - D-25: línea obligatoria, placa de maquinaria de hasta 8 caracteres y capacidad en kg.
  - D-26: P4p antes de P4b.
  - PENDIENTE: D-27 (marcas y modelos).
  - Nota sobre D-22: la respuesta decía "A", pero el texto pedía meterlo en P4b, y se registró P4b.
- Backend:
  - `/api/inventario` (política AsesorOAdministrador) con validación por categoría, placa normalizada y única, precio con precio anterior, estados, y auditoría de solo los campos que cambian.
  - Fotos en `Media:Ruta` (por defecto `C:\CAMION_media`): se validan por su contenido, hasta 5 MB y 10 por vehículo; se sirven en `/media` de forma segura.
  - Condición 7 en el catálogo.
- Migración `database/migraciones/004_p4a_inventario.sql` escrita y sin aplicar (D-23).
- Frontend: rutas `#/inventario`, lista, formulario por categoría y gestión de fotos.
- Defectos hallados por las pruebas y corregidos:
  - el peso y la capacidad no admitían decimales;
  - "1.5" se leía como 15.
- Los agentes de pruebas se cortaron por el límite de uso. Las pruebas de la API que dejaron estaban completas, y el coordinador las terminó, las ajustó a D-25 y las ejecutó.
- Resultados ejecutados:
  - `dotnet test`: 650/650.
  - `npm test`: 312/312.
  - `npm run build`: correcto.
- Revisión de seguridad (coordinador):
  - sin secretos ni dependencias nuevas;
  - permisos por rol;
  - `/media` protegido contra rutas fuera de la carpeta;
  - fotos validadas por su contenido;
  - la migración 004 solo añade, y su BAJAR (comentado) solo quita lo que añadió.
- Limitaciones:
  - SQLite no reproduce los bloqueos de fila de MariaDB.
  - El portal sigue mostrando el precio y las fotos de la publicación hasta P4b.
  - Prueba manual pendiente de la migración 004 y de D-27.
