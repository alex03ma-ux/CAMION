# Criterios de aceptación P7: ventas, planes y administración

Ítem del backlog: **P7 "Ventas, planes y administración"**. Criterio de salida (BACKLOG.md y minuta §11): "Historial, reversión, parametrización, panel administrativo". Además, D-21 y D-26 dejaron para P7 la parte de planes y **pagos** que no entró en P4p.
Rama: `feature/p7-ventas-admin`, creada desde `main` (`35ad702`, con P0 a P6).
Autor: analista funcional. Fecha: 2026-11-12. Estado del documento: PROPUESTO (pendiente de revisión del coordinador y de las decisiones D-61 a D-65).

Fuentes leídas:
- `docs/requisitos/requisitos-funcionales-v3.0.md`: matriz §7 (HU-022, HU-043 a HU-051, HU-061, HU-064 a HU-069, HU-073 a HU-087), FN-01 y FN-04 (§8), RN-VE-014, RN-PL-001/002, RN-SU-001 a 005, RN-RE-001 a 006, RN-PA-001 a 007 (§9), estados (§12-13), integraciones (§14) y restricciones (§15).
- `docs/requisitos/minuta-transferencia-agentes-ia.md` §5 (M07, M11, M13, M14), §6 (venta y reversión), §7 (FN-01), §8 (integraciones no contratadas) y §11.
- `docs/coordinacion/DECISIONES.md` completo (en especial D-4, D-7, D-20, D-21, D-22, D-26, D-28, D-34 a D-42, D-44, D-45, D-47 a D-49, D-53 a D-60).
- `docs/coordinacion/ESTADO-REAL.md` y `docs/coordinacion/BACKLOG.md`.
- `docs/criterios/P4p-contrato-api.md` y `docs/criterios/P4b-contrato-api.md`.
- Código en HEAD `35ad702` (lectura directa del analista, **no** es una auditoría del `auditor-codigo`): `Endpoints/VentasEndpoints.cs`, `Models/VentaVehiculo.cs`, `Endpoints/InventarioEndpoints.cs` (detalle con venta vigente), `Endpoints/PlanesEndpoints.cs`, `Endpoints/SuscripcionesEndpoints.cs`, `Models/Suscripcion.cs`, `Endpoints/AdminCatalogosEndpoints.cs`, `Endpoints/PublicacionesEndpoints.cs` (reactivar), `Endpoints/LeadsEndpoints.cs` (`/resumen`), `Leads/ReglasLeads.cs` (`WhatsappPortal`), `Data/CamionDbContext.cs`, `database/002_esquema_inicial_mariadb_10_4.sql` (`pagos`, `configuracion_sistema`), `frontend/src/components/NavAdmin.tsx`, `PerfilPanel.tsx`, `PiePortal.tsx`, `useDialogosVehiculo.tsx`.

Aviso sobre `ESTADO-REAL.md`: no tiene secciones para P4a, P4p ni P4b (salta de P0 a P5). Lo que este documento dice que "existe" de P4p y P4b sale de la lectura del código hecha por el analista. Se recomienda que el `auditor-codigo` lo verifique antes de programar.

Numeración: matriz de 87 HU (D-1). En ella "Marcar vehículo como vendido" es **HU-064**, "Notificación vencimiento suscripción" es **HU-074** y "Administrar parámetros generales" es **HU-081** (la lista de la sección 10 las desplaza +1; ver D-65).

Marcas:
- **CONFIRMADO (fuente)**: sale literalmente de una HU, RN, de la minuta o de una decisión TOMADA.
- **PROPUESTO**: lo infiere el analista; necesita visto bueno del coordinador o de la responsable.
- **PENDIENTE D-n**: depende de una decisión abierta. Se escribe con la opción recomendada; si la responsable elige otra, el criterio se reescribe.
- **EXISTE**: ya está en el código de `main`; en P7 queda como prueba de regresión.

Cómo se prueba: API = prueba de integración; COMP = prueba de componente del frontend; MAN = prueba manual de la responsable.
Rutas, nombres de campos y textos son orientativos; el contrato definitivo lo fija el arquitecto (`docs/criterios/P7-contrato-api.md`).

---

## 1. HU, RN y decisiones implicadas, y a qué entrega pertenecen

| Código | Nombre (matriz v3.0) | Regla | Actor v3.0 | Entrega | Estado real |
|---|---|---|---|---|---|
| HU-064 | Marcar vehículo como vendido | RN-GC-001, RN-VE-014 "Oculta publicación" | Asesor/Admin | P4b (D-22) | EXISTE: `POST /api/inventario/vehiculos/{id}/vender` |
| — | Reversión de la venta | Minuta §6 "Reversión por asesor propietario con motivo y auditoría; reactivación requiere confirmación y cupo" | Asesor | P4b | EXISTE: `POST /api/inventario/vehiculos/{id}/revertir-venta` (dueño **o** administrador) |
| — | Historial comercial | Minuta §6 "se conserva el historial comercial"; M11 "ventas, historial y reversión auditada" | — | **P7** | PARCIAL: `ventas_vehiculo` guarda todas las filas (también las revertidas), pero ninguna ruta ni pantalla las lista; el detalle del vehículo solo muestra la venta vigente |
| HU-049 / HU-050 / HU-051 | Crear, modificar, activar/inactivar plan | RN-PL-001, RN-PL-002 | Administrador | P4p (D-26) | EXISTE |
| HU-066 a HU-069 | Consultar, renovar, suspender, reactivar suscripción | RN-SU-001 a RN-SU-005 | Asesor / Admin | P4p (D-26, D-35, D-36) | EXISTE (asignar, renovar, cambiar de plan, suspender, reactivar, cancelar) |
| — | Pagos del plan | FN-04; v3.0 §14 "Pasarela de pagos" (posible); D-21/D-26 "pagos llegan en P7" | Administrador | **P7 (según D-61)** | NO INICIADO: la tabla `pagos` está en el script 002, sin mapear |
| HU-081 | Administrar parámetros generales | RN-PA-001 "Configuración global" | Administrador | **P7** | NO INICIADO: tabla `configuracion_sistema` en el script 002, sin mapear; contacto del pie fijo en `PiePortal.tsx:19-22` y `WhatsappPortal` en `appsettings.json` |
| HU-082 / HU-083 / HU-084 | Marcas, modelos, tipos de carrocería | RN-PA-002 a RN-PA-004 | Administrador | P4p (D-27, D-37) | EXISTE (además materiales, tipos de máquina y combustibles) |
| HU-085 | Administrar información corporativa | RN-PA-005 | Administrador | **P7 en parte / BLOQUEADO** | NO INICIADO; razón social, NIT y dirección faltan (D-44) |
| HU-086 | Administrar textos legales | RN-PA-006 | Administrador | **BLOQUEADO por D-44** | NO INICIADO; borradores sin revisión del abogado |
| HU-087 | Administrar configuración comercial | RN-PA-007 "Administrar promociones y parámetros" | Administrador | **P8** (promociones = marketing) | NO INICIADO |
| HU-075 | Dashboard general | RN-RE-001 "Indicadores consolidados" | Administrador | **P7 solo contadores operativos (D-64); el resto P8** | NO INICIADO (solo existe `GET /api/leads/resumen`) |
| HU-076 a HU-080 | Reportes de vehículos, leads, créditos, exportación, indicadores comerciales | RN-RE-002 a RN-RE-006 | Administrador | **P8** | NO INICIADO |
| HU-073 / HU-074 | Notificación vencimiento publicación / suscripción | RN-NO-004, RN-NO-005 "Preventiva" | Sistema | **P7 solo aviso dentro del panel (D-64 b); correo/WhatsApp P8** (como D-48) | NO INICIADO (Mi suscripción ya muestra días restantes) |
| HU-061 | Registrar auditoría | RN-SE-003 | Sistema | Transversal | EXISTE (`auditoria_eventos`); no hay pantalla de consulta |
| HU-022, HU-043 a HU-048 | Inventario general; usuarios | RN-AD-001, RN-US-* | Administrador | P2, P4a | EXISTE |
| HU-052 a HU-055 | Contenido y marketing | RN-MKT-001 | Administrador | **P8** | NO INICIADO (la foto de portada de D-42 ya existe) |
| — | Permisos | RN-SE-006; v3.0 §15 "Solo administradores podrán gestionar planes / parametrizar" | — | Transversal | — |
| — | Modelo de negocio | D-45 | — | Transversal | La plataforma solo pone en contacto; la venta ocurre fuera |

Flujos v3.0: FN-01 "… Lead → Negociación → Venta → Cierre"; FN-04 "Creación plan → Asignación → Activación → Renovación → Suspensión → Reactivación".

---

## 2. Qué pide la v3.0 / qué existe ya / qué falta

| Tema | Qué pide la v3.0 y la minuta | Qué existe ya (HEAD `35ad702`) | Qué falta para P7 |
|---|---|---|---|
| Marcar vendido | HU-064, RN-VE-014: marcar vendido oculta la publicación; minuta: fecha, precio final, origen y observaciones | Vender desde ACTIVO o RESERVADO; fila en `ventas_vehiculo`; publicación VENDIDA fuera del portal; destacado quitado; nota de reserva borrada; origen de lista fija (D-39); auditoría `Venta/VENDER` | Nada nuevo; solo regresión |
| Reversión | Minuta: "por asesor propietario con motivo y auditoría; reactivación requiere confirmación y cupo" | Revertir (dueño **o** administrador), motivo 5-500; vehículo ACTIVO; publicación DISPONIBLE y OCULTA; venta marcada con quién, cuándo y por qué; reactivar exige plan vigente (la publicación ya cuenta en el cupo, D-40); auditoría `REVERTIR_VENTA` | Confirmar quién revierte y si hay plazo (D-62 a); diálogo de confirmación en la interfaz (verificar); mostrar las ventas revertidas |
| Historial comercial | Minuta: "se oculta del catálogo y conserva historial"; M11 "historial" | Filas conservadas en `ventas_vehiculo`; filtro `estado=VENDIDO` en el inventario | Lista de ventas (administrador: todas; asesor: las suyas) con filtros; historial de ventas por vehículo, incluidas las revertidas (D-62 c) |
| Planes | HU-049 a HU-051 | CRUD sin borrado, activar/inactivar, destacados incluidos (D-38) | Nada nuevo |
| Suscripciones | HU-066 a HU-069, RN-SU-005 30 días | Asignar, renovar (solo admin, D-36), cambiar plan, suspender, reactivar, cancelar; vigencia 30 × meses (D-35); "Mi suscripción" del asesor | Aviso de vencimiento cercano dentro del panel (D-64 b) |
| Pagos | FN-04 "Activación"; §14 pasarela como posibilidad; D-26: "el administrador activa a mano cuando confirma el pago" | Nada: activación manual sin registro del pago; tabla `pagos` solo en el script | Según D-61: nada (A), registro interno del pago (B), comprobante (C) o pasarela (D, integración externa) |
| Parámetros generales | HU-081, RN-PA-001 | Contacto del pie fijo en el frontend; WhatsApp del portal en `appsettings.json` (D-47, D-49); foto de portada administrable (D-42) | Pantalla "Parámetros" con los datos de contacto del portal (D-63) |
| Catálogos | HU-082 a HU-084 | Completo (D-37) | Nada |
| Información corporativa y textos legales | HU-085, HU-086 | Borradores en `docs/legal/` sin publicar (D-44) | BLOQUEADO hasta la revisión legal y los datos de la responsable |
| Configuración comercial | HU-087 "promociones y parámetros" | Nada | Pasa a P8 con marketing |
| Panel administrativo | HU-075 "indicadores consolidados"; minuta M13 | Secciones Usuarios, Planes, Suscripciones, Catálogos, Portada y enlace a Contactos (`NavAdmin.tsx`); Inventario y Publicaciones solo desde `PerfilPanel.tsx`; contador "Contactos (N nuevos)" | Sección "Inicio" con contadores operativos (D-64); añadir Ventas, Parámetros, Inventario y Publicaciones a la navegación |
| Reportes, exportación, indicadores comerciales | HU-076 a HU-080 | Nada | P8 |
| Auditoría | HU-061 | Se registra en todas las acciones; ninguna ruta la expone (la prueba `SesionTests` prohíbe "auditoria" en las rutas) | Consulta del historial del vehículo: no en P7 (D-62 d) |

---

## 3. Contradicciones y choques con D-45

| # | Fuente | Qué dice | Choque | Tratamiento |
|---|---|---|---|---|
| C-1 | Minuta §6 | "Reversión **por asesor propietario**" | El código (P4b, aprobado) permite revertir al dueño **y** al administrador | No es contradicción grave: el administrador tiene "facultades administrativas" (minuta §6). Se pide confirmar en **D-62 a** |
| C-2 | v3.0 §13 "Vehículos: Activo → Inactivo → Vendido" | Vendido llega después de Inactivo | El código vende desde ACTIVO o RESERVADO, no desde INACTIVO (D-11, D-28) | Ya resuelto por D-4, D-11 y D-28; sin cambio |
| C-3 | v3.0 §14 "Pasarela de pagos"; FN-04 "Activación" | Posible cobro en línea | D-26 dejó fuera los pagos en línea; D-45: la plataforma pone en contacto | **D-61**. Toda pasarela, factura electrónica o contabilidad es una integración externa y decisión de la responsable |
| C-4 | RN-SU-005 "Duración de 30 días. Renovable"; Validaciones §11 "vigencia de suscripción (30 días)" | La vigencia es una regla fija | Si la vigencia fuera un "parámetro general" editable, el administrador podría cambiar una regla de negocio sin control de cambios (v3.0 §16) | **D-63**: se propone que no sea editable |
| C-5 | RN-PA-007 / HU-087 "promociones" | Parametrización comercial | Las promociones son contenido de marketing (EP-08, P8) | Se propone pasar HU-087 a P8 (D-63) |
| C-6 | `docs/criterios/P4p-contrato-api.md:11` y `:137` | Cita "notificaciones (HU-081)" | En la matriz oficial (D-1) HU-081 es "Administrar parámetros generales"; la notificación de vencimiento de suscripción es HU-074 | **D-65** (documental) |
| C-7 | HU-075 / HU-080 (indicadores) frente al backlog P8 "Indicadores e integraciones" | El dashboard es de Reportes | El encargo de P7 pide "panel administrativo e indicadores básicos" | **D-64**: en P7 solo contadores operativos; dashboard, reportes y exportación en P8 |
| C-8 | HU-073 / HU-074 (vencimientos, "Preventiva") | Notificar | Sin servicio de correo (D-5); D-48 dejó los avisos externos para P8 | **D-64 b**: aviso solo dentro del panel en P7 |

**HU que suponen dinero o contabilidad (D-45):**
- Ninguna HU de la matriz pide procesar pagos en línea, facturar ni llevar contabilidad. La venta de vehículos (HU-064) solo **registra** que se vendió, fuera del portal; el precio final es un dato informativo para los reportes de P8 (D-39), no una transacción.
- La única fuente que menciona pagos es v3.0 §14 ("Pasarela de pagos" como integración **posible**) y la tabla `pagos` del script 002. El único dinero que entra a la empresa por el portal es el **plan del asesor**. Cobrarlo en línea (pasarela), emitir la factura electrónica de ese cobro o llevar su contabilidad son integraciones externas: **D-61**.

---

## 4. Alcance propuesto para P7 (PROPUESTO)

Alcance mínimo y entregable, sin integraciones externas:

1. **Historial de ventas** (minuta §6, M11): lista de ventas para el administrador (todas) y el asesor (las de sus vehículos), con filtros, y el historial de ventas de cada vehículo, incluidas las revertidas. Sin tablas nuevas.
2. **Reversión**: se mantiene lo de P4b; se añaden la confirmación en la interfaz (si falta) y lo que decida D-62.
3. **Pagos del plan (según D-61, recomendada B)**: el administrador registra a mano el pago al asignar o renovar un plan con precio; historial de pagos por suscripción. Sin pasarela, sin comprobante, sin factura.
4. **Parámetros generales (HU-081, según D-63, recomendada A)**: datos de contacto del portal (WhatsApp, correo, horario e Instagram/TikTok) editables por el administrador y usados por el pie y por el botón de WhatsApp de D-47.
5. **Panel administrativo (HU-075 parcial, según D-64, recomendada A)**: sección "Inicio" del administrador con contadores operativos enlazados a sus listas, y navegación completa (Inicio, Usuarios, Planes, Suscripciones, Pagos si aplica, Ventas, Inventario, Publicaciones, Contactos, Catálogos, Portada, Parámetros).
6. **Aviso de vencimiento dentro del panel (HU-073/HU-074 parcial, D-64 b)**: contador del administrador y aviso al asesor en "Mi suscripción".

Pasa a **P8**: HU-076 a HU-080 (reportes filtrables, exportación Excel/PDF, indicadores comerciales con valores y gráficos), el resto de HU-075, HU-087 (promociones), HU-052 a HU-055 (marketing), avisos por correo o WhatsApp (HU-071 a HU-074), y cualquier integración aprobada (pasarela, Analytics, redes).
Queda **BLOQUEADO**: HU-085 (salvo los datos de contacto) y HU-086, hasta la revisión legal y los datos corporativos (D-44).
Queda **fuera**: facturación electrónica, contabilidad, datos del comprador final de un vehículo, consulta libre de la auditoría.

---

## 5. Criterios de aceptación

### 5.1 Historial de ventas (minuta §6, M11; HU-064)

| ID | Criterio (Dado / Cuando / Entonces) | Marca | Prueba |
|---|---|---|---|
| CA-P7-01 | **Dado** un administrador con sesión, **cuando** consulta `GET /api/ventas?pagina=1&tamano=20`, **entonces** recibe 200 con la lista paginada de **todas** las ventas, vigentes y revertidas, ordenadas de la más reciente a la más antigua por fecha de registro, con: id, vehículo (id y título), asesor dueño actual (id y nombre), fecha de venta, precio final, origen (texto de D-39), observaciones, estado "Vigente" o "Revertida", quién y cuándo la registró y, si se revirtió, quién, cuándo y el motivo. | PROPUESTO (base CONFIRMADA: minuta "conserva historial") | API |
| CA-P7-02 | **Dado** un asesor aprobado, **cuando** consulta la misma lista, **entonces** solo ve las ventas de los vehículos que hoy son suyos (mismo alcance que su inventario), sin el filtro de asesor. | PENDIENTE D-62 c (A) | API |
| CA-P7-03 | **Dado** el administrador, **cuando** filtra por `desde`/`hasta` (fecha de venta), `origen`, `estado` (VIGENTE/REVERTIDA) y `asesorId`, **entonces** solo recibe las ventas que cumplen todos los filtros. | PROPUESTO | API |
| CA-P7-04 | **Dado** un filtro inválido (fecha mal escrita, `desde` posterior a `hasta`, origen fuera de la lista de D-39, estado desconocido, página o tamaño fuera de rango), **cuando** consulta, **entonces** recibe 400 `{ error, errores }` con el campo y un mensaje en español, p. ej. "La fecha inicial no puede ser posterior a la final.". | PROPUESTO | API |
| CA-P7-05 | **Dado** un asesor que envía `asesorId` de otro asesor, **cuando** consulta, **entonces** el filtro se ignora y solo ve lo suyo (como en `/api/publicaciones`). | PROPUESTO | API |
| CA-P7-06 | **Dado** un vehículo con una venta revertida y una venta posterior vigente, **cuando** el dueño o el administrador consulta `GET /api/inventario/vehiculos/{id}/ventas`, **entonces** recibe las dos filas, la vigente primero, cada una con su estado. | PROPUESTO | API |
| CA-P7-07 | **Dado** un asesor, **cuando** pide el historial de ventas de un vehículo ajeno o inexistente, **entonces** recibe 404 "Vehículo no encontrado." (no se revela que existe). | CONFIRMADO (regla de alcance de P4a/P4b) | API |
| CA-P7-08 | **Dado** un comprador con sesión, **cuando** llama a cualquiera de las rutas de ventas, **entonces** recibe 403 "No tienes permiso para realizar esta acción."; **dado** que no hay sesión, recibe 401. | CONFIRMADO (RN-SE-006, D-3) | API |
| CA-P7-09 | **Dado** un vehículo VENDIDO sin fila de venta (DEMO o anterior a P4b), **cuando** se consulta su historial, **entonces** la lista viene vacía y la interfaz dice "Este vehículo figura como vendido, pero no tiene datos de venta registrados.". | PROPUESTO | API, COMP |
| CA-P7-10 | **Dado** que no hay ventas con los filtros elegidos, **cuando** se abre la pantalla "Ventas", **entonces** se muestra "No hay ventas con estos filtros." y el botón "Limpiar filtros". | PROPUESTO | COMP |
| CA-P7-11 | **Dado** el administrador en "Ventas", **cuando** abre una fila, **entonces** llega al detalle del vehículo en el inventario, donde una sección "Historial de ventas" muestra todas sus ventas (las revertidas con su motivo). | PROPUESTO | COMP, MAN |
| CA-P7-12 | **Dado** cualquier consulta de ventas, **entonces** la respuesta no incluye datos de un comprador final (el portal no los pide ni los guarda) ni datos de leads. | PROPUESTO (coherente con D-28 y D-45) | API |
| CA-P7-13 | **Dado** un vehículo vendido, **cuando** un visitante consulta el catálogo o el detalle por slug, **entonces** no aparece y nunca se muestra su precio final ni su historial. | CONFIRMADO (RN-VE-014) · EXISTE | API |
| CA-P7-14 | **Dado** el listado de ventas, **cuando** se pide sin filtros, **entonces** el total y la suma de filas coinciden con `ventas_vehiculo` (no se omiten revertidas). | PROPUESTO | API |

### 5.2 Marcar vendido y reversión (HU-064, minuta §6, D-22, D-39, D-62)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P7-15 | **Dado** un vehículo ACTIVO o RESERVADO del asesor, **cuando** lo marca vendido con fecha (hoy o anterior, no antes de su creación), precio final > 0, origen de la lista y observaciones (obligatorias si el origen es "Otro"), **entonces** queda VENDIDO, su publicación sale del portal, pierde el destacado y se crea una fila de venta auditada. | CONFIRMADO (HU-064, RN-VE-014, D-39) · EXISTE (regresión) | API |
| CA-P7-16 | **Dado** el dueño de un vehículo VENDIDO, **cuando** revierte con motivo de 5 a 500 caracteres, **entonces** el vehículo vuelve a ACTIVO, la publicación queda DISPONIBLE y OCULTA con ese motivo, la venta queda marcada como revertida (quién, cuándo, por qué) y se audita `REVERTIR_VENTA` con antes y después. | CONFIRMADO (minuta §6) · EXISTE | API |
| CA-P7-17 | **Dado** el administrador, **cuando** revierte la venta de un vehículo de cualquier asesor, **entonces** se aplica lo mismo que en CA-P7-16. | PENDIENTE D-62 a (A, comportamiento actual) | API |
| CA-P7-18 | **Dado** un motivo vacío, de menos de 5 o de más de 500 caracteres, **cuando** se revierte, **entonces** 400 con el error en `motivo` y nada cambia. | CONFIRMADO · EXISTE | API |
| CA-P7-19 | **Dado** un vehículo que no está VENDIDO, **cuando** se intenta revertir, **entonces** 409 "No se puede revertir la venta de un vehículo en estado <estado>.". | CONFIRMADO · EXISTE | API |
| CA-P7-20 | **Dado** una venta revertida, **cuando** el dueño quiere volver a mostrar la publicación, **entonces** debe reactivarla: con plan vigente pasa a PUBLICADA (no consume cupo nuevo, D-40); sin plan vigente, 409 con el mensaje de "sin plan" de P4b. | CONFIRMADO (minuta "reactivación requiere confirmación y cupo"; D-40) · EXISTE | API |
| CA-P7-21 | **Dado** un vehículo revertido, **cuando** se vuelve a marcar vendido, **entonces** se crea una fila nueva y la anterior sigue como revertida (CA-P7-06). | PROPUESTO · EXISTE en el backend | API |
| CA-P7-22 | **Dado** el diálogo "Revertir venta", **cuando** el usuario lo abre, **entonces** explica "El vehículo volverá a estar activo y la publicación quedará oculta hasta que la reactives." y exige el motivo con asterisco (D-31) antes de confirmar. | PROPUESTO (minuta "confirmación") | COMP |
| CA-P7-23 | **Dado** dos reversiones simultáneas de la misma venta, **entonces** una responde 200 y la otra 409, y solo queda un evento de auditoría. | PROPUESTO (bloqueo de fila ya existe en P4b) | API |
| CA-P7-24 | **Dado** un comprador o un visitante, **cuando** llama a vender o revertir, **entonces** 403 o 401. | CONFIRMADO (D-3) · EXISTE | API |

### 5.3 Planes y pagos (HU-049 a HU-051, HU-066 a HU-069, D-26, D-36, D-61)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P7-25 | **Dado** el administrador, **cuando** crea, edita, activa o inactiva un plan y asigna, renueva, suspende, reactiva o cancela una suscripción, **entonces** se comporta como en `P4p-contrato-api.md` (regresión). | CONFIRMADO · EXISTE | API |
| CA-P7-26 | **Dado** un asesor, **cuando** intenta renovar su suscripción por la API, **entonces** 403 (solo el administrador renueva, D-36), y en "Mi suscripción" ve "Para renovar, comunícate con el administrador" con el WhatsApp del portal (parámetro de CA-P7-40). | CONFIRMADO (D-36) + PROPUESTO (texto y enlace) | API, COMP |
| CA-P7-27 | **Dado** el administrador que asigna o renueva un plan con precio mayor que 0, **cuando** confirma, **entonces** el formulario le pide registrar el pago: valor (por defecto el precio del plan; entero > 0), fecha de pago (hoy o anterior), medio (Transferencia, Consignación, Efectivo, Otro) y referencia opcional (hasta 100 caracteres); suscripción y pago se guardan en la misma transacción. | PENDIENTE D-61 (B) | API, COMP |
| CA-P7-28 | **Dado** un plan de prueba o de precio 0, **cuando** se asigna, **entonces** no se pide ni se crea pago. | PENDIENTE D-61 (B) | API |
| CA-P7-29 | **Dado** datos de pago inválidos (valor ≤ 0 o no entero, fecha futura o mal escrita, medio fuera de la lista, "Otro" sin referencia, referencia > 100), **entonces** 400 con el campo y el mensaje, y **no** se crea ni la suscripción ni el pago. | PENDIENTE D-61 (B) | API |
| CA-P7-30 | **Dado** el administrador, **cuando** abre una suscripción, **entonces** ve su historial de pagos (fecha, valor, medio, referencia, quién lo registró) y el total pagado de esa suscripción. | PENDIENTE D-61 (B) | API, COMP |
| CA-P7-31 | **Dado** un pago registrado por error, **cuando** el administrador lo anula con motivo (5-500), **entonces** queda como "Anulado" con quién, cuándo y por qué, no se borra, se audita y la suscripción no cambia. | PENDIENTE D-61 (B) + PROPUESTO | API |
| CA-P7-32 | **Dado** un asesor, comprador o visitante, **cuando** llama a cualquier ruta de pagos, **entonces** 403 o 401; el asesor no ve los pagos en "Mi suscripción". | PENDIENTE D-61 (B) + PROPUESTO | API |
| CA-P7-33 | **Dado** el portal, **entonces** no hay ninguna pantalla ni ruta que reciba datos de tarjeta, cuentas bancarias o pagos en línea, ni que emita facturas. | CONFIRMADO (D-26 "sin pagos en línea"; D-45) | API (inventario de rutas), MAN |
| CA-P7-34 | **Dado** que la responsable elige D-61 A, **entonces** CA-P7-27 a CA-P7-32 se retiran y la activación manual sigue como hoy (auditada con quién y cuándo). | PENDIENTE D-61 | — |

### 5.4 Parámetros generales (HU-081, RN-PA-001, D-42, D-47, D-63)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P7-35 | **Dado** el administrador en "Parámetros", **cuando** abre la pantalla, **entonces** ve los valores vigentes: WhatsApp del portal, correo de contacto, horario de atención (hasta 4 líneas) e Instagram y TikTok (opcionales). | PENDIENTE D-63 (A) | COMP |
| CA-P7-36 | **Dado** valores válidos, **cuando** guarda, **entonces** 200, el pie del portal muestra los valores nuevos en la siguiente carga y se audita `Parametro/CAMBIAR` con los valores anteriores y nuevos. | PENDIENTE D-63 (A) | API, COMP |
| CA-P7-37 | **Dado** un WhatsApp que no es un celular colombiano válido (regla de P5), un correo inválido, una línea de horario vacía o de más de 100 caracteres, o un enlace de red social que no empieza por `https://www.instagram.com/` o `https://www.tiktok.com/`, **entonces** 400 con el campo y el mensaje, y nada cambia. | PENDIENTE D-63 (A) + PROPUESTO (límites) | API |
| CA-P7-38 | **Dado** que aún no se ha guardado ningún parámetro, **cuando** se carga el portal, **entonces** se usan los valores confirmados de D-42 (WhatsApp 316 615 2907, vc.automotores@gmail.com y el horario confirmado). | CONFIRMADO (D-42) + PROPUESTO (valor por defecto) | API, COMP |
| CA-P7-39 | **Dado** Instagram o TikTok vacíos, **entonces** el pie no los muestra. | CONFIRMADO (D-42: "no se muestran hasta tener los enlaces") | COMP |
| CA-P7-40 | **Dado** un nuevo WhatsApp del portal, **cuando** una publicación está a cargo del administrador o el asesor tiene un fijo, **entonces** el botón de WhatsApp de la ficha (D-47, D-49) usa el número nuevo en lugar del de `appsettings.json`. | PROPUESTO | API |
| CA-P7-41 | **Dado** un valor con etiquetas HTML o `javascript:`, **entonces** se rechaza (400) o se muestra como texto plano; nunca se interpreta. | PROPUESTO (seguridad) | API, COMP |
| CA-P7-42 | **Dado** la ruta pública de lectura de parámetros, **entonces** es anónima, solo GET, devuelve únicamente los datos de contacto públicos y no va bajo `/api/catalogo` (la prueba `SesionTests` fija sus rutas). | PROPUESTO | API |
| CA-P7-43 | **Dado** un asesor, comprador o visitante, **cuando** intenta modificar parámetros, **entonces** 403 o 401. | CONFIRMADO (v3.0 §4.1 "Administrar parámetros"; RN-SE-006) | API |
| CA-P7-44 | **Dado** la pantalla "Parámetros", **entonces** **no** ofrece cambiar los días por mes del plan, el límite de fotos ni su tamaño, los límites anti-abuso, las categorías ni la DIVIPOLA. | PENDIENTE D-63 (A) + CONFIRMADO en parte (RN-SU-005, D-20, D-37) | COMP |

### 5.5 Panel administrativo e indicadores básicos (HU-075 parcial, HU-073/HU-074 parcial, D-64)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P7-45 | **Dado** un administrador que entra a "Mi cuenta", **cuando** abre la sección "Inicio" (`#/cuenta/inicio`), **entonces** ve contadores: asesores pendientes de aprobación; solicitudes abiertas por tipo (contactos a cargo de Administración, "Vende tu camión", seguros, créditos y búsquedas) y cuántas son nuevas; suscripciones que vencen en los próximos 7 días; suscripciones vencidas sin renovar; publicaciones visibles en el portal; vehículos por estado (Activo, Reservado, Inactivo, Vendido); ventas vigentes registradas en los últimos 30 días (cantidad). | PENDIENTE D-64 (A) | API, COMP |
| CA-P7-46 | **Dado** cada contador, **cuando** el administrador hace clic, **entonces** llega a la lista correspondiente ya filtrada, y el número coincide con el total de esa lista. | PENDIENTE D-64 (A) + PROPUESTO | API, COMP |
| CA-P7-47 | **Dado** un contador en 0, **entonces** se muestra "0" (no se oculta) con un texto en palabras, p. ej. "Sin asesores pendientes". | PROPUESTO | COMP |
| CA-P7-48 | **Dado** el resumen, **entonces** solo devuelve números; ningún nombre, teléfono ni correo. | PROPUESTO (RN-SE-004) | API |
| CA-P7-49 | **Dado** un asesor o comprador, **cuando** pide el resumen del panel, **entonces** 403; sin sesión, 401; la sección "Inicio" del administrador no aparece en su navegación. | CONFIRMADO (RN-SE-006) | API, COMP |
| CA-P7-50 | **Dado** un asesor con plan vigente que vence en 7 días o menos, **cuando** abre "Mi cuenta", **entonces** "Mi suscripción" muestra "Tu plan vence en N días. Para renovarlo, comunícate con el administrador." | PENDIENTE D-64 b (A) | COMP |
| CA-P7-51 | **Dado** el panel, **entonces** no hay gráficos, filtros por fecha, sumas de dinero ni exportación (quedan para P8, HU-076 a HU-080). | PENDIENTE D-64 (A) | COMP |
| CA-P7-52 | **Dado** el administrador en cualquier sección, **entonces** la navegación ofrece Inicio, Usuarios, Planes, Suscripciones, Ventas, Inventario, Publicaciones, Contactos, Catálogos, Portada y Parámetros (y Pagos si D-61 es B y no se integra en Suscripciones), con `aria-current` en la actual, usable en móvil. | PROPUESTO | COMP, MAN |

### 5.6 Transversales: permisos, auditoría, datos y regresión

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P7-53 | **Dado** cada ruta nueva de P7, **entonces** aparece en la prueba de inventario de rutas (`SesionTests.Inventario_de_endpoints`) con su política: las de administración bajo `/api/admin` con `SoloAdministrador`; las de ventas con `AsesorOAdministrador`; la lectura pública de parámetros como única anónima nueva. Ninguna ruta contiene "auditoria". | PROPUESTO | API |
| CA-P7-54 | **Dado** cualquier acción de escritura de P7 (registrar o anular pago, cambiar parámetro), **entonces** se audita en la misma transacción con quién, cuándo, datos antes y después y motivo cuando aplica. | CONFIRMADO (RN-SE-003, HU-061) | API |
| CA-P7-55 | **Dado** un token de una sesión cerrada (`SessionVersion` distinta) o expirada, **cuando** llama a cualquier ruta de P7, **entonces** 401 y el frontend vuelve al inicio de sesión. | CONFIRMADO (D-12) | API, COMP |
| CA-P7-56 | **Dado** el despliegue de P7, **entonces** no se borra ni se modifica ninguna fila existente de `ventas_vehiculo`, `suscripciones`, `planes` ni `auditoria_eventos`; los conteos antes y después de cualquier migración coinciden. | CONFIRMADO (CLAUDE.md, minuta §10) | MAN |
| CA-P7-57 | **Dado** el cierre de P7, **entonces** pasan `dotnet test`, `npx vitest run`, `npx tsc -b` y `npm run build`, sin regresiones en P1 a P6. | CONFIRMADO (minuta §12) | API, COMP |
| CA-P7-58 | **Dado** todas las pantallas nuevas (Inicio, Ventas, Parámetros y pagos), **entonces** los campos obligatorios llevan asterisco y la nota "* Campo obligatorio" (D-31), los errores del backend se muestran con su mensaje y son usables con teclado y en móvil. | CONFIRMADO (D-31, minuta §12) | COMP, MAN |
| CA-P7-59 | **Dado** la prueba manual en `camion_ventas`, **cuando** la responsable marca vendido, revierte, vuelve a vender, consulta el historial, cambia el WhatsApp del portal y revisa el panel, **entonces** todo coincide con estos criterios. | CONFIRMADO (procedimiento del proyecto) | MAN |

---

## 6. Trazabilidad HU/RN/D → criterio → prueba prevista

| HU / RN / D | Criterios | Prueba prevista |
|---|---|---|
| HU-064, RN-GC-001, RN-VE-014, D-22, D-39 | CA-P7-13, 15, 21, 24 | API (regresión de P4b) |
| Minuta §6 (reversión), D-62 a/b | CA-P7-16 a 20, 22, 23 | API, COMP |
| Minuta §6 (historial), M11, D-62 c | CA-P7-01 a 12, 14 | API, COMP, MAN |
| HU-049 a HU-051, RN-PL-001/002 | CA-P7-25 | API (regresión de P4p) |
| HU-066 a HU-069, RN-SU-001 a 005, D-35, D-36 | CA-P7-25, 26, 50 | API, COMP |
| FN-04, v3.0 §14, D-26, D-61 | CA-P7-27 a 34 | API, COMP |
| HU-081, RN-PA-001, D-42, D-47, D-49, D-63 | CA-P7-35 a 44 | API, COMP |
| HU-085, HU-086 (D-44) | — (BLOQUEADO) | — |
| HU-087, HU-076 a HU-080, HU-052 a HU-055 | — (P8) | — |
| HU-075, RN-RE-001, D-64 a | CA-P7-45 a 49, 51, 52 | API, COMP |
| HU-073, HU-074, RN-NO-004/005, D-64 b | CA-P7-45 (contadores de vencimiento), 50 | API, COMP |
| HU-061, RN-SE-003 | CA-P7-16, 54 | API |
| RN-SE-004 | CA-P7-12, 48 | API |
| RN-SE-006, D-3 | CA-P7-08, 24, 32, 43, 49, 53 | API |
| D-12 | CA-P7-55 | API, COMP |
| D-31 | CA-P7-22, 58 | COMP |
| D-45 | CA-P7-12, 33 | API, MAN |
| CLAUDE.md (datos y respaldos) | CA-P7-56 | MAN |
| Minuta §12 | CA-P7-57, 58, 59 | API, COMP, MAN |

---

## 7. ¿Hace falta migración?

- **Historial de ventas, reversión y panel:** no. Usan `ventas_vehiculo`, `suscripciones`, `leads`, `perfiles_asesor`, `vehiculos` y `publicaciones`, que ya existen.
- **Parámetros (D-63 A):** probablemente no. La tabla `configuracion_sistema (clave, valor_json, actualizado_por, actualizado_en)` está en el script 002, y los respaldos de `camion_ventas` registran 39 tablas, igual que el script. Hay que verificar (auditor, solo lectura) que existe con esas columnas. Si falta, hace falta la migración **016**, reversible.
- **Pagos (D-61 B):** probablemente sí, la **016**, pequeña y reversible. La tabla `pagos` existe en el script (valor, referencia, soporte_archivo, estado PENDIENTE/APROBADO/RECHAZADO, verificado_por/en), pero le faltan la fecha de pago, el medio y el motivo de anulación, y su CHECK de estado no tiene "ANULADO". Con D-61 A no hace falta migración.
- Siguiente número libre: **016** (la última es `015_p6_creditos_busquedas`). Siempre con respaldo previo, conteos, sección BAJAR y aplicada por la responsable (D-16).
