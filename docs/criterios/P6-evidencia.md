# P6 — Evidencia de pruebas: créditos, búsquedas y "Mis solicitudes"

Ítem: **P6** (HU-034 a HU-042; D-7, D-45, D-53, D-54 a D-59).
Rama: `feature/p6-creditos`. Fecha: 2026-11-10. Autor: QA.
Fuentes: `docs/criterios/criterios-P6-creditos.md` (CA-P6-01 a 80), `docs/criterios/P6-contrato-api.md` (incluida la sección "Cambios al contrato": `GET /api/catalogo/listas-busqueda`) y D-54 a D-59 en `docs/coordinacion/DECISIONES.md`.

Base de datos: las pruebas de API usan SQLite en memoria (`CamionApiFactory`, `P5Fixture`) y reloj fijo; **ninguna prueba toca `camion_ventas`** (D-16). Datos ficticios; no se imprimen tokens ni contraseñas. No se modificó código de producción.

## 1. Comandos y resultados

Todos ejecutados por QA en esta sesión. No hay archivo de solución (`.sln`): `dotnet build` y `dotnet test` se ejecutan sobre `backend/Camion.Api.Tests`, que compila también `Camion.Api`.

| # | Comando (desde la carpeta indicada) | Resultado |
|---|---|---|
| 1 | `backend/Camion.Api.Tests`: `dotnet build --artifacts-path <scratchpad>/art-qa-p6` | **PASA**: 0 errores, 3 advertencias heredadas (NU1902 de `MySqlConnector` 2.3.7, dos veces; xUnit2031 en `InventarioCrearEditarTests.cs:547`), 14 s |
| 2 | ídem `dotnet test --no-build` (antes de las pruebas de QA) | **PASA**: 1093 superadas, 0 con error, 0 omitidas (1 m 40 s) |
| 3 | ídem, solo `P6PermisosQaTests` (nuevo) | **PASA**: 17 superadas, 0 con error, 0 omitidas (12 s) |
| 4 | ídem, suite completa con las pruebas de QA | **PASA**: 1110 superadas, 0 con error, 0 omitidas (1 m 24 s); 3 advertencias heredadas, ninguna nueva |
| 5 | `frontend`: `npx tsc -b` | **PASA** (sin errores) |
| 6 | `frontend`: `npx tsc -p tsconfig.test.json --noEmit` | **PASA** (sin errores, también con el archivo nuevo de QA) |
| 7 | `frontend`: `npx vitest run` (antes de las pruebas de QA) | **PASA**: 32 archivos, 641 pruebas (67,5 s) |
| 8 | ídem, solo `tests/P6Qa.test.tsx` (nuevo) | **PASA**: 1 archivo, 2 pruebas |
| 9 | ídem, suite completa con las pruebas de QA | **PASA**: 33 archivos, 643 pruebas (96,6 s) |
| 10 | `frontend`: `npm run build` | **PASA** (`tsc -b && vite build`, `index-*.js` 534,06 kB, gzip 153,45 kB; aviso de Vite por superar 500 kB, ver O-5) |
| 11 | E2E de P6 contra la API real | **NO EJECUTADA**: Playwright no está instalado y la API de esta rama no debe arrancar contra `camion_ventas` sin la migración 015 (riesgo de despliegue del contrato). No se hizo ninguna llamada a http://localhost:5080. |

Regresión (CA-P6-80): las ejecuciones 4 y 9 incluyen todas las pruebas de P1 a P5b: contacto desde la ficha, asignación, bandeja, cierre, reapertura, reasignación y barrido D-46 (`ContactosTests`, `ContactosLimitesTests`, `ContactosTransaccionTests`, `LeadsGestionTests`, `LeadsBarridoTests`, `LeadsPermisosQaTests`, `ReasignacionTests`), avisos (`AvisosTests`, `AvisosLimitesTests`, `AvisosPermisosQaTests`), el inventario de rutas (`SesionTests.Inventario_de_endpoints`, ya con las rutas de P6) y en el frontend `P5Contacto`, `P5Leads`, `P5bAvisos` y `P5bAvisosQa`. Ninguna falla.

## 2. Pruebas añadidas por QA

`backend/Camion.Api.Tests/P6PermisosQaTests.cs` (10 métodos, 17 casos, todos pasan):

| Prueba | Qué cubre |
|---|---|
| `D55_nada_financiero_en_la_base_ni_en_los_logs` | CA-P6-09, 12 (D-55 B): `monto`, `cuotaInicial`, `ingresos`, `documento`, `actividad`, un objeto anidado `datosFinancieros` y un arreglo `adjuntos` en un crédito nuevo, uno repetido, uno desde la ficha, uno rechazado (400) y una búsqueda. Se lee **todo el texto de todas las tablas** de la base de prueba (`sqlite_master`) y todas las líneas del registro técnico: ningún marcador aparece. La respuesta es byte a byte la misma que sin esos campos |
| `Mis_solicitudes_sin_idor` | CA-P6-67, 73: 404 "Solicitud no encontrada." idéntico para solicitudes de otro comprador (cinco orígenes), del asesor, del administrador, anónimas, inexistentes, 0 y −1; `/abc`, `/1.5` y una inyección no devuelven datos; `?usuarioId=`, `?id=` y otros parámetros no amplían la lista |
| `Mis_solicitudes_sin_datos_internos_tras_la_gestion` | CA-P6-68, 70, 72: tras notas, relacionar con nota, reasignar el contacto a otro asesor, cerrar con `OTRO` y observación y reabrir, ni la lista ni el detalle contienen notas, observación, resultado, responsable, nombres de asesores, `REASIGNADO`, historial, teléfono, correo ni nombre del titular; estados Pendiente / Finalizada / En gestión |
| `Asesor_nunca_ve_creditos_ni_busquedas` | CA-P6-24, 38, 60, 61: con 11 combinaciones de filtros (`origen`, `publicacionId`, `vehiculoId` de su propio vehículo, `estado`, `responsable`, `tamano`) la lista del asesor nunca trae créditos ni búsquedas; el contador solo suma su contacto; por id 404 (detalle, nota, cierre, coincidencias, relacionar, retirar) y 403 (reabrir, reasignar); no se marca visto. Reasignar el vehículo (D-34) mueve el contacto (regresión P5) pero no el crédito; bloquear al asesor no mueve su búsqueda |
| `Reasignar_siempre_409` (8 casos) | CA-P6-39, D-58 A: crédito de portada, crédito de ficha y búsqueda; abierto, cerrado y reabierto; a un asesor o a Administración, con y sin motivo → 409 "Esta solicitud la atiende siempre la administración."; sin eventos, auditoría ni cambio de responsable |
| `Relacionar_solo_publicaciones_visibles` | CA-P6-52, 56, 72: vehículo vendido, asesor bloqueado y plan vencido → 409 "Ese vehículo no está publicado." (también cuando la vendida ya estaba relacionada) y salen de coincidencias; la relacionada que deja de verse queda `visible: false` para el administrador, desaparece de los recomendados del comprador y se puede retirar (la fila se conserva) |
| `Sin_rutas_de_edicion` | CA-P6-48, 69: GET/PUT/PATCH/DELETE en `/api/creditos`, `/api/busquedas`, sus `/{id}`, `/api/leads/{id}` y las relaciones, con administrador, asesor, comprador y sin token → 404/405/401/403; nada cambia en la base |
| `Busqueda_contador_y_reapertura` | CA-P6-47, 57: la búsqueda cuenta en los nuevos del administrador y deja de contar al abrirla; cerrar dos veces → 409; reabrir sin motivo → 400; con motivo → ABIERTO, "Registrado", Administración, evento `REABIERTO` y auditoría `REABRIR` |
| `Sin_coincidencias_automaticas` | CA-P6-25, 55, 58: publicar después un vehículo que coincide no crea relaciones, eventos, auditoría ni leads para el asesor; solo aparece en "Ver coincidencias" |
| `Portal_sin_solicitudes` | CA-P6-65: inicio, catálogo, filtros, ficha y listas de la búsqueda, con y sin token, no contienen datos ni rastros de créditos o búsquedas |

`frontend/tests/P6Qa.test.tsx` (2 pruebas): "Mis solicitudes" no pinta campos internos aunque la API los devolviera (notas, responsable, resultado, observación, teléfono, mensaje, nota de la relación) y no enlaza un vehículo con `disponible: false` aunque llegue un `slug`; los recomendados solo se muestran en búsquedas (CA-P6-68, 71, 72).

## 3. Matriz de trazabilidad CA-P6 → prueba

Abreviaturas: **CR** = `backend/Camion.Api.Tests/CreditosTests.cs`, **BU** = `BusquedasTests.cs`, **MS** = `MisSolicitudesTests.cs`, **SL** = `SolicitudesLimitesTests.cs`, **LI** = `ListasBusquedaTests.cs`, **SE** = `SesionTests.cs`, **PQ** = `P6PermisosQaTests.cs` (nuevo), **FC** = `frontend/tests/P6Creditos.test.tsx`, **FQ** = `frontend/tests/P6Qa.test.tsx` (nuevo). El nombre es el método xUnit o el título del `it`.

| CA | Cobertura | Estado |
|---|---|---|
| 01 | CR:`Portada_anonimo`; FC:"anónimo: POST sin token con el cuerpo del contrato…" | PROBADO |
| 02 | FC:"la banda de crédito de la portada abre #/credito…" | PROBADO |
| 03 | CR:`Ficha_en_administracion`; FC:"la ficha ofrece \"Solicitar crédito para este vehículo\"…", FC:"muestra el vehículo (no editable) y envía su slug" | PROBADO |
| 04 | CR:`Publicacion_no_visible` (inexistente, >200, inactiva, plan vencido); FC:"un vehículo que ya no es visible no muestra el formulario", FC:"un 404 al enviar muestra el mensaje del contrato…" | PROBADO |
| 05 | CR:`Propio_y_con_sesion`; FC:"CA-P6-05 el asesor dueño no ve el acceso…", FC:"un 409 al enviar muestra \"Este vehículo es tuyo\"" | PROBADO |
| 06 | CR:`Propio_y_con_sesion`; MS:`Lista_de_los_cinco_origenes`; FC:"CA-P6-06 con sesión de comprador prellena el perfil…" | PROBADO (ver O-1) |
| 07 | CR:`Validaciones`, CR:`Comentario_500_y_normalizacion`; FC:"validación en el navegador…" | PROBADO |
| 08 | FC:"pide solo nombre, teléfono, correo y comentario…", FC:"muestra el vehículo (no editable)…" | PROBADO |
| 09 | CR:`Campos_financieros_y_manipulados_ignorados`; PQ:`D55_nada_financiero_en_la_base_ni_en_los_logs` (toda la base y los logs) | PROBADO |
| 10 | CR:`Portada_anonimo` (`publicacion_id` y `tipo_vehiculo` NULL) | PROBADO |
| 11 | CR:`Validaciones` (501), CR:`Comentario_500_y_normalizacion`, CR:`Portada_anonimo` y `Ficha_en_administracion` (textos fijos); FC:"pide solo…" (ayuda y contador), FC:"comentario vacío viaja como null…", FC:"un 400 del servidor marca el comentario…" | PROBADO |
| 12 | CR:`Campos_financieros_y_manipulados_ignorados`, PQ:`D55_nada_financiero_en_la_base_ni_en_los_logs`; FC:"pide solo…" (sin campos financieros) | PROBADO |
| 13 | FC:"pide solo…" ("Somos intermediarios…") | PROBADO |
| 14 | CR:`Portada_anonimo` (cuerpo exacto); FC:"anónimo: POST sin token… confirmación con WhatsApp y tres salidas" | PROBADO |
| 15 | CR:`Campos_financieros_y_manipulados_ignorados` (origen, estado, responsable, asesorId, publicacionId, usuarioId, versionPolitica) | PROBADO |
| 16 | BU:`Anonima_con_criterios`, BU:`Solo_categoria_y_ciudad`; FC:"envía los criterios con números y null…", FC:"solo la categoría y los datos de contacto bastan…" | PROBADO |
| 17 | BU:`Validaciones` (sin tipo, inexistente, inactivo); FC:"validación: categoría, años, presupuesto y autorización…" | PROBADO (ver O-3) |
| 18 | BU:`Validaciones` (marca inactiva o inexistente, modelo de otra marca, sin marca, inexistente), BU:`Modelo_de_otra_categoria`, LI:`Ids_coherentes_con_la_busqueda`; FC:"todas las categorías activas… modelos por marca y categoría", FC:"el modelo se filtra por marca y categoría…", FC:"validarBusqueda rechaza modelo sin marca o de otra categoría…" | PROBADO |
| 19 | BU:`Validaciones` (1949, futuro, invertidos), BU:`Limites_formato_y_trampa` (1950 y año + 1); FC:"\"desde\" mayor que \"hasta\" marca el año final" | PROBADO |
| 20 | BU:`Validaciones`, BU:`Limites_formato_y_trampa` (1.000.000 y 5.000.000.000); FC:"envía los criterios… presupuesto con miles en pantalla" | PROBADO |
| 21 | BU:`Validaciones` (ciudad de otro departamento, inexistente), BU:`Solo_categoria_y_ciudad`; FC:"la ciudad depende del departamento y viajan sus ids", FC:"validarBusqueda rechaza… ciudad de otro departamento" | PROBADO |
| 22 | BU:`Validaciones` (501), BU:`Anonima_con_criterios`, BU:`Solo_categoria_y_ciudad` (texto fijo) | PROBADO |
| 23 | BU:`Permisos` (ligada al asesor), MS:`Lista_de_los_cinco_origenes`; FC:"CA-P6-23 con sesión envía el token y prellena el contacto" | PROBADO (ver O-1) |
| 24 | BU:`Permisos`, PQ:`Asesor_nunca_ve_creditos_ni_busquedas`, PQ:`Portal_sin_solicitudes` | PROBADO |
| 25 | PQ:`Sin_coincidencias_automaticas` | PROBADO |
| 26 | FC:"el pie ofrece \"Busco un vehículo\"…", FC:"el catálogo ofrece… cuando no hay resultados", FC:"…al final de la lista de resultados" | PROBADO (COMP) + **manual** (vista en el navegador) |
| 27 | CR:`Validaciones` y BU:`Validaciones` (sin casilla), CR:`Portada_anonimo` y BU:`Anonima_con_criterios` (consentimiento), SL:`Falla_al_guardar_no_deja_nada` (transacción); FC:"pide solo…" (casilla sin marcar) | PROBADO |
| 28 | FC:"pide solo…" (una sola casilla; sin bancos ni centrales) | PROBADO |
| 29 | CR:`Trampa_tamano_formato`, BU:`Limites_formato_y_trampa` | PROBADO |
| 30 | SL:`Cupo_por_ip_compartido`, SL:`Por_telefono_compartido`; SE:`Inventario_de_endpoints` (`LimitePeticionesContacto`) | PROBADO |
| 31 | CR:`Repetidos` | PROBADO |
| 32 | BU:`Repetida` | PROBADO |
| 33 | CR:`Repetidos`, BU:`Repetida` | PROBADO |
| 34 | FC:"un 400 del servidor marca el comentario y conserva lo escrito", FC:"%s muestra un mensaje…" (429, 413, 500, red), FC:"un 400 del servidor en tipoId…", FC:"429 y sin conexión muestran los mensajes del contrato" | PROBADO |
| 35 | FC:"pide solo…" (nota y `aria-required`), FC:"validación en el navegador…" | PROBADO |
| 36 | FC:"pide solo…" (`h1`, `aria-live`, `inputmode`, trampa), FC:"anónimo…" (`role="status"` con foco), FC:"envía los criterios…" (`inputmode="numeric"`) | PARCIAL: atributos PROBADOS; 360 px y contraste AA solo **manual** |
| 37 | CR:`Portada_anonimo`, BU:`Anonima_con_criterios` (historial SISTEMA y auditoría sin datos personales ni criterios) | PROBADO |
| 38 | CR:`Barrido_no_lo_mueve`; PQ:`Asesor_nunca_ve_creditos_ni_busquedas` (D-34 y bloqueo) | PROBADO |
| 39 | CR:`No_se_reasigna_ni_relaciona`, BU:`Detalle_y_cierre`, PQ:`Reasignar_siempre_409` (8 casos), CR:`Checks_de_la_base` (`CK_Lead_AvisoAdmin`); FC:"con vehículo: … sin \"Reasignar\"…", FC:"muestra los criterios legibles… sin \"Reasignar\"" | PROBADO |
| 40 | CR:`Ficha_en_administracion` (origen, publicacionId, responsable), BU:`Detalle_y_cierre`; FC:"muestra \"Crédito\" y \"Búsqueda\"…", FC:"filtra por origen CREDITO" | PROBADO |
| 41 | CR:`Gestion_por_el_administrador`; FC:"con vehículo: precio, estado, enlace…", FC:"un vehículo que ya no se ve en el portal no se enlaza" | PROBADO |
| 42 | BU:`Detalle_y_cierre` (marca desactivada después); FC:"muestra los criterios legibles…" | PROBADO |
| 43 | CR:`Gestion_por_el_administrador` (201, 400, 409); FC:"con vehículo…" (aviso del campo de nota) | PROBADO |
| 44 | CR:`Gestion_por_el_administrador`, BU:`Detalle_y_cierre`; FC:"muestra \"Crédito\" y \"Búsqueda\" con su estado…" | PROBADO |
| 45 | CR:`Gestion_por_el_administrador`; FC:"el cierre ofrece los resultados del crédito…", FC:"resultados por origen según la tabla del contrato P6" | PROBADO |
| 46 | BU:`Detalle_y_cierre`; FC:"el cierre de una búsqueda ofrece \"Encontró vehículo\"…" | PROBADO |
| 47 | CR:`Gestion_por_el_administrador`, PQ:`Busqueda_contador_y_reapertura` | PROBADO |
| 48 | PQ:`Sin_rutas_de_edicion` | PROBADO |
| 49 | CR:`Gestion_por_el_administrador` (sin monto, cuota ni ingresos en el detalle); FC:"con vehículo… sin datos financieros" | PROBADO |
| 50 | BU:`Relacionar_y_retirar`; FC:"\"Ver coincidencias\" y relacionar con nota interna…" | PROBADO |
| 51 | BU:`Coincidencias_bajo_demanda`, PQ:`Relacionar_solo_publicaciones_visibles`; FC:"\"Ver coincidencias\"…" | PROBADO |
| 52 | BU:`Relacionar_rechazos`, BU:`Tope_de_diez`, PQ:`Relacionar_solo_publicaciones_visibles` (vendido, bloqueado, plan vencido); FC:"un 409 al relacionar se anuncia…", FC:"con 10 relacionados el botón…" | PROBADO |
| 53 | BU:`Relacionar_y_retirar`; FC:"retirar: confirma, envía la ruta sin cuerpo…" | PROBADO |
| 54 | CR:`No_se_reasigna_ni_relaciona` (crédito, contacto y aviso) | PROBADO |
| 55 | BU:`Relacionar_y_retirar` (sin leads para el asesor), PQ:`Sin_coincidencias_automaticas` | PROBADO |
| 56 | BU:`Relacionar_y_retirar`, PQ:`Relacionar_solo_publicaciones_visibles`; FC:"muestra los criterios legibles… relacionados con su estado" | PROBADO |
| 57 | CR:`Portada_anonimo`, CR:`Gestion_por_el_administrador` (visto al abrir), PQ:`Busqueda_contador_y_reapertura` | PROBADO |
| 58 | PQ:`Sin_coincidencias_automaticas`; revisión de código: `Camion.Api` no referencia ningún servicio de correo, SMS ni WhatsApp (búsqueda de `SmtpClient`, `IEmailSender`, `SendGrid`, `Twilio`, `MailKit` sin resultados); FC:"anónimo…" (el mensaje no promete correo) | PROBADO (API por ausencia de salida) + revisión de código |
| 59 | MS:`Estados_y_vehiculo_no_disponible` (el estado se ve al volver a consultar); no hay canal de aviso (ver 58) | PROBADO |
| 60 | CR:`Ficha_en_administracion`, BU:`Permisos`, PQ:`Asesor_nunca_ve_creditos_ni_busquedas`; FC:"el asesor no ve los orígenes nuevos ni el filtro por origen" | PROBADO |
| 61 | CR:`Ficha_en_administracion`, BU:`Permisos`, PQ:`Asesor_nunca_ve_creditos_ni_busquedas`; FC:"un asesor que abre por id una búsqueda recibe 404…" | PROBADO |
| 62 | CR:`Permisos_y_portal`, BU:`Permisos`; FC:"un comprador en #/contactos ve \"No tienes permiso\"…" | PROBADO |
| 63 | CR:`Permisos_y_portal`, BU:`Permisos`, MS:`Permisos_y_solo_lectura` (401); CR:`Portada_anonimo`, BU:`Anonima_con_criterios` (anónimo aceptado); FC:"sin sesión, #/mis-solicitudes lleva al ingreso…" | PROBADO |
| 64 | CR:`Propio_y_con_sesion` (otro asesor y administrador), BU:`Permisos` | PROBADO |
| 65 | CR:`Permisos_y_portal`, PQ:`Portal_sin_solicitudes` | PROBADO |
| 66 | MS:`Lista_de_los_cinco_origenes`, MS:`Paginacion_y_vacia`; FC:"lista con fecha, tipo, resumen y estado…", FC:"pagina de 20 en 20" | PROBADO |
| 67 | MS:`Lista_de_los_cinco_origenes` (anónima con su teléfono, de otro comprador), PQ:`Mis_solicitudes_sin_idor` | PROBADO |
| 68 | MS:`Lista_de_los_cinco_origenes`, MS:`Estados_y_vehiculo_no_disponible`, PQ:`Mis_solicitudes_sin_datos_internos_tras_la_gestion`; FC:"lista con fecha…" (sin Responsable, Nota, Resultado); FQ:"campos internos de más… no se pintan" | PROBADO |
| 69 | MS:`Permisos_y_solo_lectura` (POST/PUT/DELETE/PATCH → 405), PQ:`Sin_rutas_de_edicion`; FC:"lista con fecha…" (sin botones) | PROBADO |
| 70 | MS:`Estados_y_vehiculo_no_disponible`, PQ:`Mis_solicitudes_sin_datos_internos_tras_la_gestion`; FC:"lista con fecha…" | PROBADO |
| 71 | MS:`Estados_y_vehiculo_no_disponible`; FC:"lista con fecha… enlace solo si el vehículo sigue disponible"; FQ:"un vehículo con disponible=false no se enlaza…" | PROBADO |
| 72 | MS:`Recomendados`, PQ:`Relacionar_solo_publicaciones_visibles`, PQ:`Mis_solicitudes_sin_datos_internos_tras_la_gestion`; FC:"lista con fecha… recomendados en la búsqueda"; FQ:"campos internos de más…" (nota de la relación) | PROBADO |
| 73 | MS:`Detalle_propio_y_ajeno`, PQ:`Mis_solicitudes_sin_idor` | PROBADO |
| 74 | MS:`Permisos_y_solo_lectura`, SE:`Inventario_de_endpoints` (`SoloComprador`); FC:"%s: no ve el acceso ni llama a la API" (ASESOR, ADMINISTRADOR), FC:"el perfil del asesor no ofrece \"Mis solicitudes\"" | PROBADO |
| 75 | MS:`Paginacion_y_vacia`; FC:"sin solicitudes muestra el texto y los accesos" | PROBADO |
| 76 | FC:"lista con fecha…" (artículos con nombre y estados en texto), FC:"pagina de 20 en 20" | PROBADO (COMP) + **manual** (lector de pantalla) |
| 77 | — | **Sin prueba automática (MAN)**: la migración 015 solo puede comprobarse en una base de prueba MariaDB con leads de P5 y P5b; las pruebas usan SQLite creado por EF |
| 78 | — | **Sin prueba automática (MAN)**: comprobaciones previas del BAJAR comentado de la 015 |
| 79 | CR:`Checks_de_la_base` (`CK_Lead_Origen`, `CK_Lead_AvisoAdmin`, resultados, consentimientos, eventos, `leads_busqueda`) | PROBADO en SQLite; el mismo CHECK en MariaDB queda para CA-P6-77 (MAN) |
| 80 | Suites completas en verde (comandos 4 y 9); PQ:`Asesor_nunca_ve_creditos_ni_busquedas` (D-34 mueve el contacto), BU:`Detalle_y_cierre` (avisos sin resultados nuevos) | PROBADO |
| Listas de la búsqueda (Cambios al contrato) | LI:`Listas_publicas`, LI:`Ids_coherentes_con_la_busqueda`; FC:"todas las categorías activas de /api/catalogo/listas-busqueda…", FC:"si fallan las listas…" | PROBADO |
| Prueba manual (§12 de los criterios) | Crédito y búsqueda sin cuenta y con comprador, asesor sin créditos ni búsquedas, administradora gestiona, relaciona y cierra, comprador en "Mis solicitudes" | **Manual pendiente**: requiere la migración 015 aplicada por la responsable con respaldo (D-14, D-16) |

Resumen: 78 de 80 criterios con al menos una prueba automática que pasa (CA-P6-36 solo en parte); 2 solo manuales (77, 78); 3 con parte manual (26, 36, 76). Antes de QA quedaban sin prueba 25 y 48, y con hueco 09 y 12 (solo las tablas del lead, no toda la base ni los logs), 38 (D-34 y bloqueo), 39 (cerrado y reabierto), 47 (búsqueda), 52 (plan vencido, vendido, bloqueado), 57 (búsqueda), 65 (búsqueda, catálogo y ficha) y 73 (IDOR con otros orígenes y roles).

## 4. Defectos y observaciones

Ninguna prueba falla. No se encontraron defectos del código de producción frente al contrato.

- **O-1 (comportamiento a confirmar, no bloqueante).** Un comprador con sesión que envía un crédito o una búsqueda con un teléfono que ya tiene una solicitud ABIERTA del mismo tipo (misma publicación en el crédito) **no** ve ese envío en "Mis solicitudes": se trata como repetido y solo se añade `CONTACTO_REPETIDO` y su consentimiento al lead existente, cuyo `usuario_id` no cambia (`SolicitudesEndpoints.GuardarAsync`, la consulta de repetidos no mira el usuario). Es lo que dice el contrato ("Repetido": mismo origen, teléfono y publicación) y evita fusionar solicitudes de personas distintas, pero choca con la letra de CA-P6-06 y CA-P6-23 ("aparece en Mis solicitudes"). Pasos: 1) enviar `POST /api/creditos` sin token con el teléfono 3001112233; 2) iniciar sesión como comprador y enviar otro crédito desde la portada con el mismo teléfono (201); 3) `GET /api/mis-solicitudes` con su token → `total: 0`. Lo mismo ocurre con `POST /api/busquedas`. QA lo comprobó con una sonda temporal (crédito y búsqueda: `total = 0` y 2 leads del teléfono, ambos sin el comprador), que se borró después: ninguna prueba del repositorio lo fija. Decisión de negocio para el coordinador.
- **O-2 (entorno).** Sin E2E: Playwright no está instalado y la API de la rama requiere la migración 015 en `camion_ventas`. Se propone al coordinador incorporar Playwright antes de añadir la dependencia.
- **O-3 (documento).** CA-P6-17, 18 y 21 nombran los campos `categoria`, `modelo` y `municipio`; el contrato y el código usan `tipoId`, `lineaId` y `municipioId`. Las pruebas siguen el contrato.
- **O-4 (advertencias heredadas).** NU1902 en `MySqlConnector` 2.3.7 (vulnerabilidad moderada conocida) y xUnit2031 en `InventarioCrearEditarTests.cs:547`; no son de P6.
- **O-5 (rendimiento, no bloqueante).** El paquete principal pasa de 494 kB (P5b) a 534 kB y `vite build` avisa "Some chunks are larger than 500 kB". No rompe el build; conviene valorar dividir el código (por ejemplo, cargar el panel del administrador aparte).
