# P5 — Evidencia de pruebas: leads y contactos

Ítem: **P5 "Leads y contactos"** (HU-029 a HU-033, HU-071 parcial, HU-061/062; D-7, D-9, D-44, D-46 B, D-47, D-48).
Rama: `feature/p5-leads`. Fecha: 2026-11-04. Autor: QA.
Fuentes: `docs/criterios/criterios-P5-leads.md` (CA-P5-01 a 64) y `docs/criterios/P5-contrato-api.md`.

Base de datos: las pruebas de API usan SQLite en memoria (`CamionApiFactory`) y reloj fijo; **ninguna prueba toca `camion_ventas`** (D-16). Datos ficticios; no se imprimen tokens ni contraseñas.

## 1. Comandos y resultados

| # | Comando (desde la carpeta indicada) | Resultado |
|---|---|---|
| 1 | `backend/Camion.Api.Tests`: `dotnet test --artifacts-path <scratchpad>/art-qa-p5` (antes de las pruebas nuevas) | **PASA**: 953 superadas, 0 con error, 0 omitidas (2 m 47 s) |
| 2 | ídem, con las 6 primeras pruebas de `LeadsPermisosQaTests.cs` | **PASA**: 959 superadas, 0 con error, 0 omitidas (49 s) |
| 2b | ídem, con las 7 pruebas de `LeadsPermisosQaTests.cs` (versión final) | **PASA**: 960 superadas, 0 con error, 0 omitidas (50 s) |
| 3 | `frontend`: `npx tsc -b` | **PASA** (sin errores) |
| 4 | `frontend`: `npm run build` | **PASA** (`vite build`, 96 módulos, `index-*.js` 481,81 kB) |
| 5 | `frontend`: `npx vitest run` con la configuración anterior (workers por defecto) | **FALLA, intermitente**: 20 fallos en una ejecución en limpio y 46 en otra (36 con el backend corriendo a la vez); siempre por `Test timed out in 5000ms` o por `findBy*` agotando su espera de 1 s |
| 6 | `frontend`: `npx vitest run --maxWorkers=4` (diagnóstico) | **PASA**: 29 archivos, 526 pruebas, 79,6 s; prueba más lenta 2,3 s |
| 7 | `frontend`: `npx vitest run` con la corrección (`maxWorkers: '50%'`, `testTimeout: 15_000`), ejecución 1 | **PASA**: 29 archivos, 526 pruebas, 43,5 s |
| 8 | ídem, ejecución 2 | **PASA**: 29 archivos, 526 pruebas, 43,8 s |
| 9 | E2E de P5 contra http://localhost:5080 | **NO EJECUTADA**: la API que corre en 5080 no es la de esta rama (`GET /api/leads` sin token → 404 en lugar de 401; el detalle público no trae `whatsapp`). Solo se hicieron sondeos GET de lectura (`/health` 200). Playwright no está instalado en el proyecto. |

Regresión: las ejecuciones 2, 2b, 7 y 8 incluyen todas las pruebas de P1 a P4b; ninguna falla.

### Diagnóstico de la fragilidad de vitest

- Con los workers por defecto de Vitest 4 (núcleos lógicos − 1 = 17 en este equipo de 18 hilos) cada worker arranca su propio jsdom. Las pruebas que teclean formularios completos con `user-event` (registro de comprador y de asesor, formulario de inventario, publicar) son de CPU pura y, con 17 procesos compitiendo, pasan de ~2 s a más de 5 s.
- No era un problema de una prueba concreta: el número y la lista de fallos cambian entre ejecuciones (20, 36, 46) y crecen con la carga externa; también fallan `findBy*` con su espera de 1 s, que un `testTimeout` mayor no arregla.
- Corrección en `frontend/vitest.config.ts`: `maxWorkers: '50%'` (la causa) y `testTimeout: 15_000` (solo margen para equipos más lentos). No se tocó ninguna aserción ni la espera de `findBy*`. La suite queda además más rápida (≈44 s frente a ≈80 s con 4 workers).

## 2. Pruebas añadidas por QA

`backend/Camion.Api.Tests/LeadsPermisosQaTests.cs` (7 pruebas, todas pasan):

| Prueba | Qué cubre |
|---|---|
| `Asesor_rechazado_sin_acceso` | CA-P5-34: asesor Rechazado, aun con rol ASESOR en el token y su SessionVersion vigente → 401 en lista, resumen, detalle y acción |
| `Asesor_sin_perfil_403` | Contrato: usuario con rol ASESOR sin `perfiles_asesor` → 403 en lectura y acciones; el lead no cambia |
| `Asesor_ajeno_en_todas_las_rutas` | CA-P5-29, 30, 45: asesor ajeno no lo lista (ni filtrando por la publicación o por el responsable), el resumen da 0, detalle/nota/cerrar → 404, reabrir/reasignar → 403; no se añaden eventos ni se marca visto |
| `Lead_de_administracion_sin_acciones_del_asesor` | CA-P5-30: el asesor dueño no puede anotar ni cerrar un lead que está en Administración (404) |
| `Admin_cierra_lead_de_asesor` | CA-P5-37, 39: el administrador cierra el lead de un asesor; auditoría con su usuario; el responsable no cambia y el asesor lo ve CERRADO |
| `Vehiculo_inactivo_o_publicacion_eliminada` | CA-P5-49: vehículo inactivo y publicación eliminada; el lead sigue ABIERTO con su responsable, el detalle muestra INACTIVO / ELIMINADA y se puede cerrar |
| `Comprador_contacta_pero_no_lee` | CA-P5-32: el comprador crea su contacto (ligado a su cuenta) pero recibe 403 al leerlo |

Frontend: no se añadieron pruebas; la matriz de permisos de interfaz ya estaba cubierta en `P5Leads.test.tsx` (ver CA-P5-29 a 34 y 57).

## 3. Matriz de trazabilidad CA-P5 → prueba

Abreviaturas: **CT** = `backend/Camion.Api.Tests/ContactosTests.cs`, **CL** = `ContactosLimitesTests.cs`, **LG** = `LeadsGestionTests.cs`, **LB** = `LeadsBarridoTests.cs`, **LQ** = `LeadsPermisosQaTests.cs` (nuevo), **SE** = `SesionTests.cs`, **FC** = `frontend/tests/P5Contacto.test.tsx`, **FL** = `frontend/tests/P5Leads.test.tsx`. El nombre es el método xUnit o el título del `it`.

| CA | Cobertura | Estado |
|---|---|---|
| 01 | CT:`Anonimo_crea_lead`; FC:"CA-P5-01 / CA-P5-54 anónimo: POST sin token, mensaje de éxito exacto…" | PROBADO |
| 02 | CT:`Comprador_con_sesion`, CT:`Token_invalido_es_anonimo`; FC:"comprador: datos del perfil prellenados…" | PROBADO |
| 03 | CT:`Nombre_invalido`; FC:"CA-P5-03, 04, 14, 55 vacío: errores exactos junto a cada campo…" | PROBADO |
| 04 | CT:`Telefono_invalido`; FC:"CA-P5-04 el teléfono %s se rechaza en el navegador", FC (Teléfono colombiano):"%s se rechaza" | PROBADO |
| 05 | CT:`Telefono_normalizado`, CT:`Duplicado_con_formato_distinto`; FC (Teléfono colombiano): normalización | PROBADO |
| 06 | CT:`Correo_invalido`, CT:`Correo_y_mensaje`; FC:"CA-P5-06 un correo mal escrito se marca…", FC:"correo y mensaje vacíos viajan como null" | PROBADO |
| 07 | CT:`Correo_y_mensaje`, CT:`Anonimo_crea_lead` (mensaje por defecto); FC:"el mensaje tiene límite de 500 y un contador" | PROBADO |
| 08 | CT:`Campos_manipulados` | PROBADO |
| 09 | CT:`No_contactable_404`; FC:"CA-P5-09, 11, 56: un 404 muestra…" | PROBADO |
| 10 | CT:`Reservado_se_contacta` | PROBADO |
| 11 | CT:`Duenio_no_se_contacta`; API: `esPropio` en el detalle público, `true` solo con el token del asesor dueño (commit c9aaa64); FC:"CA-P5-09, 11, 56: un 409 muestra…"; FC (describe "Asesor en su propia publicación (CA-P5-11)"): el formulario no aparece (commit 77a5d4a) | PROBADO (O-1 resuelta) |
| 12 | FC:"nota de obligatorios, aria-required…", FC:"CA-P5-12 el botón se desactiva mientras se envía", FC:"CA-P5-55 un 400 del servidor…" (conserva datos) | PROBADO |
| 13 | FC:"la casilla empieza sin marcar y enlaza a #/privacidad en una pestaña nueva" | PROBADO |
| 14 | CT:`Sin_autorizacion`; FC:"CA-P5-03, 04, 14, 55 vacío…" | PROBADO |
| 15 | CT:`Prueba_de_autorizacion` (fecha UTC, versión del servidor, formulario, entidad), CT:`Campos_manipulados` (versión del cliente ignorada); `ContactosTransaccionTests` (un fallo al guardar el consentimiento no deja lead, historial ni auditoría; commit c8181e6) | PROBADO |
| 16 | LG:`Detalle_con_historial` (solo admin ve consentimientos), LG:`Sin_edicion_ni_borrado`; FL:"CA-P5-16 el administrador ve la prueba de autorización", FL:"CA-P5-42 historial… sin autorizaciones…" | PROBADO |
| 17 | FC:"muestra la franja de borrador, el aviso de revisión legal…"; "P5 no sale a internet sin versión aprobada" | PROBADO (COMP) + **manual** (salida a internet) |
| 18 | CT:`Comprador_con_sesion`; FC:"comprador: … casilla en cada envío" | PROBADO |
| 19 | CT:`Prueba_de_autorizacion` (la lista admite los 5 valores) | PROBADO en P5; el uso real queda para P5b |
| 20 | CL:`Ventana_corta_por_ip`, CL:`Diario_por_ip` | PROBADO |
| 21 | CL:`Por_telefono` | PROBADO |
| 22 | CT:`Campo_trampa`, CT:`Tamano_y_formato`; FC:"el campo trampa sitioWeb está en el DOM…", FC:"CA-P5-22 si un robot llena el campo trampa…" | PROBADO |
| 23 | CT:`Contacto_repetido`, CT:`Duplicado_con_formato_distinto` | PROBADO |
| 24 | CT:`Nuevo_si_el_anterior_esta_cerrado` | PROBADO |
| 25 | CT:`Anonimo_crea_lead` (asignado al asesor dueño con plan) | PROBADO |
| 26 | CT:`Plan_vencido_va_a_administracion` | PROBADO |
| 27 | CT:`Asesor_bloqueado_va_a_administracion`, CT:`Duenio_administrador` | PROBADO |
| 28 | CT:`Anonimo_crea_lead` (CREADO/ASIGNADO SISTEMA, `Lead/CREAR` sin datos del visitante) | PROBADO |
| 29 | LG:`Asesor_ve_solo_lo_suyo`, LG:`Filtros`, LQ:`Asesor_ajeno_en_todas_las_rutas`; FL:"\"Mis contactos\": pide /api/leads con token…", FL:"filtrar por estado pide ?estado=ABIERTO" | PROBADO |
| 30 | LG:`Asesor_ve_solo_lo_suyo`, LG:`Administracion_404_al_asesor`, LQ:`Asesor_ajeno_en_todas_las_rutas`, LQ:`Lead_de_administracion_sin_acciones_del_asesor`; FL:"un 404 (contacto ajeno o reasignado)…" | PROBADO |
| 31 | LG:`Admin_ve_todos`, LG:`Filtros`; FL:"\"Contactos\" con responsable en cada tarjeta y filtros…" | PROBADO |
| 32 | LG:`Permisos_por_rol`, LQ:`Comprador_contacta_pero_no_lee`; FL:"CA-P5-32 un comprador en #/contactos ve \"No tienes permiso\"…", FL:"un comprador en #/contactos/5…", FL:"el perfil del comprador no ofrece \"Contactos\"…" | PROBADO |
| 33 | LG:`Permisos_por_rol`, SE:`Inventario_de_endpoints` (solo `POST /api/contactos` es anónimo); FL:"sin sesión, #/contactos lleva al inicio de sesión…" | PROBADO |
| 34 | LG:`Asesor_bloqueado_sin_acceso` (bloqueado y pendiente), LQ:`Asesor_rechazado_sin_acceso`, LQ:`Asesor_sin_perfil_403`; FL:"un 403 (asesor sin perfil) muestra el mensaje de permiso" | PROBADO |
| 35 | CT:`Catalogo_sin_leads` | PROBADO |
| 36 | LG:`Asesor_ve_solo_lo_suyo` (códigos); FL:"\"Mis contactos\"… estado en texto" ("Pendiente de gestión"/"Finalizado") | PROBADO |
| 37 | LG:`Notas`, LQ:`Admin_cierra_lead_de_asesor`; FL:"CA-P5-37 añadir una nota…", FL:"nota corta: error en el campo…" | PROBADO |
| 38 | LG:`Notas` | PROBADO |
| 39 | LG:`Cerrar`, LQ:`Admin_cierra_lead_de_asesor`; FL:"CA-P5-39 cerrar con \"Otro\" exige observación…", FL:"cerrar con un resultado distinto de \"Otro\"…" | PROBADO |
| 40 | LG:`Cerrar` (doble cierre asesor y admin → 409) | PROBADO |
| 41 | LG:`Reabrir`, LG:`Permisos_por_rol` (asesor 403); FL:"CA-P5-41 reabrir un contacto cerrado con motivo", FL:"un asesor con un contacto cerrado no ve \"Reabrir\"" | PROBADO |
| 42 | LG:`Detalle_con_historial`; FL:"CA-P5-42 historial en orden con actor y automático…" | PROBADO |
| 43 | LG:`Sin_edicion_ni_borrado` | PROBADO |
| 44 | LG:`Reasignar_a_asesor`, LG:`Reasignar_validaciones`; FL:"CA-P5-44 reasignar a otro asesor…", FL:"reasignar a Administración envía administracion: true…" | PROBADO (la reasignación queda en `eventos_lead` y `auditoria_eventos`, no en `asignaciones_solicitud`, según el contrato) |
| 45 | LG:`Permisos_por_rol`, LQ:`Asesor_ajeno_en_todas_las_rutas` | PROBADO |
| 46 | LG:`Reasignar_validaciones`; FL:"un 400 del servidor en asesorId se muestra en el diálogo…" | PROBADO |
| 47 | LB:`Manual_sin_plan` | PROBADO |
| 48 | LG:`Reasignar_a_asesor` | PROBADO |
| 49 | LG:`Vehiculo_vendido`, LQ:`Vehiculo_inactivo_o_publicacion_eliminada` | PROBADO |
| 50 | LB:`Plan_vencido` | PROBADO |
| 51 | LB:`Asesor_bloqueado` | PROBADO |
| 52 | LB:`Siguen_al_vehiculo` | PROBADO |
| 53 | LB:`No_vuelven_solos` | PROBADO |
| 54 | FC:"CA-P5-01 / CA-P5-54 anónimo…"; CT:`Campo_trampa`, CT:`Contacto_repetido` (misma respuesta) | PROBADO |
| 55 | FC:"CA-P5-03, 04, 14, 55 vacío…", FC:"CA-P5-55 un 400 del servidor marca cada campo…" | PROBADO |
| 56 | FC:"CA-P5-09, 11, 56: un %i muestra…" (404, 413, 429), FC:"CA-P5-56 %s: mensaje con el WhatsApp del portal…" (500 y sin conexión) | PROBADO |
| 57 | FL:"CA-P5-57 un 409 al anotar muestra el mensaje de la API…", FL:"un 403 (asesor sin perfil)…" | PROBADO |
| 58 | LG:`Nuevos`; FL:"asesor con %i nuevos ve \"%s\" en su perfil", FL:"el administrador también lo ve…", FL:"si el resumen falla…" | PROBADO |
| 59 | CT:`Logs_sin_datos_del_visitante` | PROBADO |
| 60 | LG:`Sin_edicion_ni_borrado` | PROBADO |
| 61 | CT:`Whatsapp_del_asesor`; FC:"CA-P5-61 usa el enlace del asesor…" | PROBADO |
| 62 | CT:`Whatsapp_del_portal`, CT:`Duenio_administrador`; FC:"CA-P5-62 sin un enlace válido (%s)…" | PROBADO |
| 63 | CT:`Whatsapp_no_visible` | PROBADO |
| 64 | FC:"CA-P5-61 usa el enlace del asesor…" (pulsarlo no llama a `/api/contactos`) | PROBADO |
| Prueba manual (§12) | Contacto anónimo y con cuenta, gestión como asesor y administradora | **Manual pendiente**: requiere la migración 013 aplicada por la responsable (D-16) |

Resumen: 64 criterios con al menos una prueba automática; CA-P5-11 y CA-P5-15, antes parciales, quedan cubiertos (ver §5); 1 con parte manual (CA-P5-17) más la prueba manual general.

## 4. Defectos y observaciones

Ninguna prueba falla. No se encontraron defectos de comportamiento de la API.

- **O-1 (CA-P5-11, desviación de interfaz, no bloqueante). RESUELTA: se añadió `esPropio` (ver §5).** El criterio dice que al asesor dueño "el formulario no aparece". Hoy aparece y, al enviarlo, la API responde 409 y la interfaz muestra "Este vehículo es tuyo; no puedes contactarte a ti mismo.". La ficha no puede saber quién es el dueño porque el contrato prohíbe enviar el id del asesor en el DTO público (D-47). Reproducción: iniciar sesión como asesor, abrir `#/vehiculo/<slug de un vehículo propio>`, llenar y enviar. Decisión para el coordinador: aceptar el 409 como cumplimiento o añadir al detalle un indicador booleano (`esPropio`) solo con token.
- **O-2 (fragilidad de pruebas, corregida).** Ver §1, diagnóstico de vitest.
- **O-3 (entorno).** La API en http://localhost:5080 no es la de `feature/p5-leads`; no se puede hacer E2E de P5 hasta arrancar esta rama, y eso exige antes la migración 013 en `camion_ventas` (riesgo de orden de despliegue del contrato).

## 5. Cierre (2026-11-04)

- **CA-P5-11 cubierto.** La API devuelve `esPropio` en el detalle público, `true` solo con el token del asesor dueño (c9aaa64); el frontend no muestra el formulario al dueño y en su lugar indica que los contactos llegan a "Mis contactos" (77a5d4a). Pruebas: CT:`Duenio_no_se_contacta` y el bloque "Asesor en su propia publicación (CA-P5-11)" de `frontend/tests/P5Contacto.test.tsx`.
- **CA-P5-15 cubierto.** `backend/Camion.Api.Tests/ContactosTransaccionTests.cs`: si falla la escritura del consentimiento, no quedan lead, historial ni auditoría (c8181e6).
- **B-1 corregido** en la API: el límite por teléfono va antes de la rama de contacto repetido y la cuenta (ee923a0). B-2 queda como riesgo aceptado (D-49).

| Comando | Resultado | Quién lo ejecutó |
|---|---|---|
| `dotnet test` (Camion.Api.Tests) | **PASA**: 962/962 | agente de backend |
| `npx vitest run` (frontend) | **PASA**: 528/528 | coordinador |
| `npx tsc -b` | **PASA** | reportado al cierre por el coordinador |
| `npm run build` | **PASA** | reportado al cierre por el coordinador |
| E2E / prueba manual con la API real | **NO EJECUTADA**: requiere la migración 013 aplicada | — |
