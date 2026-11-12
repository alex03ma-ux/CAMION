# P7 — Evidencia de pruebas: ventas, pagos del plan, parámetros y panel

Ítem: **P7** "Ventas, planes y administración" (HU-064, HU-066, HU-067, HU-073/HU-074 parcial, HU-075 parcial, HU-081; D-61 B, D-62 A, D-63 A, D-64 A).
Rama: `feature/p7-ventas-admin`. Fecha: 2026-11-12. Autor: QA.
Fuentes: `docs/criterios/criterios-P7-ventas-admin.md` (CA-P7-01 a 59), `docs/criterios/P7-contrato-api.md` y D-61 a D-64 en `docs/coordinacion/DECISIONES.md`.

Base de datos: las pruebas de API usan SQLite en memoria (`CamionApiFactory`, `P7Fixture`) y reloj fijo en T = 2026-10-26 15:00 UTC; **ninguna prueba toca `camion_ventas`** (D-16). Datos ficticios; no se imprimen tokens ni contraseñas. No se modificó código de producción.

## 1. Comandos y resultados

Todos ejecutados por QA en esta sesión. Sin `.sln`: `dotnet build` y `dotnet test` van sobre `backend/Camion.Api.Tests` (compila también `Camion.Api`), con `--artifacts-path <scratchpad>/art` para no chocar con una API en marcha. No se detuvo ningún proceso.

| # | Comando (desde la carpeta indicada) | Resultado |
|---|---|---|
| 1 | `backend/Camion.Api.Tests`: `dotnet build --artifacts-path <scratchpad>/art` | **PASA**: 0 errores, 3 advertencias heredadas (NU1902 de `MySqlConnector` 2.3.7, dos veces; xUnit2031 en `InventarioCrearEditarTests.cs:547`), 15 s |
| 2 | ídem `dotnet test --no-build`, corrida 1 (antes de las pruebas de QA) | **PASA**: 1184 superadas, 0 con error, 0 omitidas (1 m 14 s) |
| 3 | ídem, corrida 2 | **PASA**: 1184 / 0 / 0 (1 m 33 s) |
| 4 | ídem, corrida 3 | **PASA**: 1184 / 0 / 0 (1 m 32 s) |
| 5 | ídem, solo `P7PermisosQaTests` (nuevo), 3 veces | **PASA**: 31 / 0 / 0 las tres veces (6-7 s) |
| 6 | ídem, suite completa con las pruebas de QA, corrida 4 (con `vitest` corriendo a la vez, máquina cargada) | **PASA**: 1215 / 0 / 0 (2 m 3 s) |
| 7 | ídem, corrida 5 | **PASA**: 1215 / 0 / 0 (1 m 43 s) |
| 8 | ídem, corrida 6 | **PASA**: 1215 / 0 / 0 (1 m 48 s) |
| 9 | ídem, filtro con las clases que usan concurrencia (`Task.WhenAll`) y las de P7 (10 clases, 438 casos), 8 veces seguidas, a la vez que otras corridas y `vitest` | **FALLA 1 de 8**: 7 corridas 438 / 0 / 0; la 6.ª (con `vitest` en paralelo) dio **1 con error, 437 superadas**. El filtro de salida de esa corrida no capturó el nombre de la prueba (la salida en español dice "Con error" y no "Failed"), así que **no está identificada** (§4, D-1) |
| 9b | ídem, mismo filtro, 12 veces más con `--logger trx` y la máquina cargada (dos y tres `vitest` en paralelo) | **PASA**: 12 de 12, 438 / 0 / 0; ningún `.trx` con `outcome="Failed"` |
| 9c | ídem, suite completa con `--logger trx`, corrida 7, con la máquina cargada | **PASA**: 1215 / 0 / 0 (4 m 21 s) |
| 10 | `frontend`: `npx tsc -b` | **PASA** (sin errores) |
| 11 | `frontend`: `npx tsc -p tsconfig.test.json --noEmit` | **PASA** (sin errores; también con el archivo nuevo de QA) |
| 12 | `frontend`: `npx vitest run` (antes de las pruebas de QA) | **PASA**: 34 archivos, 676 pruebas (117,9 s) |
| 13 | ídem, solo `tests/P7Qa.test.tsx` (nuevo) | **PASA**: 1 archivo, 18 pruebas |
| 14 | ídem, suite completa con las pruebas de QA | **PASA**: 35 archivos, 694 pruebas (86,2 s) |
| 15 | `frontend`: `npm run build` | **PASA** (`tsc -b && vite build`, 5,2 s; `index-*.js` 581,46 kB, gzip 166,37 kB; aviso de Vite por superar 500 kB, ver O-4) |
| 16 | E2E de P7 contra la API real | **NO EJECUTADA**: Playwright no está instalado y la API de esta rama necesita la migración 016 en `camion_ventas`, que aplica la responsable (D-16). No se hizo ninguna llamada a http://localhost:5080 |

Regresión (CA-P7-57): las corridas 6 a 8 y 14 incluyen todas las pruebas de P1 a P6: P4p (`PlanesTests`, `SuscripcionesTests`, `CatalogosAdminTests`, `SuscripcionCatalogoTests`), P4b (`VentasTests`, `PublicacionesTests`, `DestacadosTests`, `ReasignacionTests`), P5 y P5b (`ContactosTests`, `ContactosLimitesTests`, `ContactosTransaccionTests`, `LeadsGestionTests`, `LeadsBarridoTests`, `LeadsPermisosQaTests`, `AvisosTests`, `AvisosLimitesTests`, `AvisosPermisosQaTests`), P6 (`CreditosTests`, `BusquedasTests`, `MisSolicitudesTests`, `SolicitudesLimitesTests`, `ListasBusquedaTests`, `P6CorreccionesTests`, `P6PermisosQaTests`), el inventario de rutas (`SesionTests.Inventario_de_endpoints`, ya con las rutas de P7) y en el frontend `P4p*`, `P4b*`, `P5*`, `P6*`. Ninguna falla.

## 2. Pruebas añadidas por QA

`backend/Camion.Api.Tests/P7PermisosQaTests.cs` (11 métodos, 31 casos, todos pasan):

| Prueba | Qué cubre |
|---|---|
| `Pagos_asesor_ni_siquiera_lo_suyo` | CA-P7-32: el asesor recibe 403 al leer, registrar o anular los pagos **de su propia** suscripción y al asignar o renovar con pago; el pago sigue REGISTRADO y sin evento nuevo |
| `Anular_bordes` | CA-P7-31: motivo de 501 o solo espacios → 400 y nada cambia; 500 caracteres → 200; segunda anulación → 409 "Este pago ya está anulado." y se conservan `anuladoEn` y el motivo originales; auditoría `REGISTRAR`, `ANULAR` y nada más |
| `Panel_sin_datos_de_pagos` | CA-P7-48, 51: tras registrar un pago con valor, medio y referencia reconocibles, el panel no contiene el valor, la referencia, el medio ni las palabras pago/valor/precio/total, y sus claves no cambian |
| `Ventas_ids_raros` (5 casos) | CA-P7-07: `/api/inventario/vehiculos/{0,-1,abc,1.5,2^66}/ventas` → 404 o 400, nunca datos |
| `Ventas_idor_por_vehiculo_y_filtros` | CA-P7-02, 05, 07: con 10 combinaciones de filtros (`estado`, `origen`, `asesorId` de otro, `vehiculoId`, fechas, `tamano`) el asesor nunca recibe precio, motivo, observaciones ni nombre de las ventas vigentes o revertidas de otro; el historial de esos vehículos → 404; el administrador sí las ve; comprador → 403 sobre un vehículo real |
| `Reversion_concurrente` | CA-P7-23: dueño y administrador revierten a la vez → una 200 y otra 409, una sola marca de reversión y un solo evento `REVERTIR` |
| `Redes_invalidas` (18 casos) | CA-P7-37, 41: Instagram y TikTok rechazan `javascript:` (también en mayúsculas y con espacios), `data:`, sin esquema, `http`, `ftp`, el dominio dentro de la ruta de otro, `usuario@` hacia otro host, subdominios falsos, espacios, `<`, comilla simple, más de 200 caracteres, sin ruta y la red equivocada → 400 en el campo; sin fila ni auditoría |
| `Publico_sin_datos_internos_tras_guardar` | CA-P7-37, 42: `instagram.com` y `tiktok.com` sin `www` se aceptan; tras guardar, `GET /api/portal/contacto` (sin token, comprador, asesor y administrador) trae solo los 6 campos públicos, sin id, correo ni nombre del administrador, `configurado`, `actualizado*` ni la clave; POST, DELETE y PATCH → 405 |
| `Whatsapp_ficha_duenio_administrador` | CA-P7-40: publicación cuyo dueño es administrador: sin parámetro usa `appsettings.json`; tras guardar, la ficha usa el número configurado y coincide con el del pie |
| `Panel_solo_admin` | CA-P7-49: asesores A y B y comprador → 403 (también con parámetros en la consulta); token falso → 401; POST/PUT/DELETE → 405/404; administrador 200 |

`frontend/tests/P7Qa.test.tsx` (18 pruebas): `enlaceRedValido` rechaza los mismos casos peligrosos en el cliente y acepta los dominios propios con y sin `www`; el pie, con una respuesta manipulada (`whatsappEnlace: "javascript:..."`, redes de otro dominio, HTML en el horario), arma el enlace `wa.me` con el número, muestra el HTML como texto (sin `<img>` ni `<script>`), no muestra las redes y ningún `href` empieza por `javascript:` o `data:` (CA-P7-39, 41).

## 3. Matriz de trazabilidad CA-P7 → prueba

Abreviaturas: **VH** = `backend/Camion.Api.Tests/VentasHistorialTests.cs`, **VT** = `VentasTests.cs`, **PG** = `PagosTests.cs`, **PR** = `ParametrosTests.cs`, **PA** = `PanelTests.cs`, **PL** = `PlanesTests.cs`, **SU** = `SuscripcionesTests.cs`, **PU** = `PublicacionesTests.cs`, **SE** = `SesionTests.cs`, **QA** = `P7PermisosQaTests.cs` (nuevo), **FA** = `frontend/tests/P7Admin.test.tsx`, **FI** = `frontend/tests/P4bInventario.test.tsx`, **FM** = `frontend/tests/P4pMiSuscripcion.test.tsx`, **FQ** = `frontend/tests/P7Qa.test.tsx` (nuevo). El nombre es el método xUnit o el título del `it`.

| CA | Cobertura | Estado |
|---|---|---|
| 01 | VH:`Admin_ve_vigente_y_revertida` (campos, orden, vigente y revertida con quién, cuándo y motivo) | PROBADO |
| 02 | VH:`Asesor_solo_lo_suyo`, VH:`Reasignado_sigue_al_dueno`, QA:`Ventas_idor_por_vehiculo_y_filtros`; FA:"asesor (#/ventas): \"Mis ventas\", sin filtro de asesor y nunca envía asesorId" | PROBADO |
| 03 | VH:`Filtros` (desde, hasta, origen, estado, asesorId, combinados y paginación) | PROBADO |
| 04 | VH:`Filtro_invalido` (8 casos); FA:"valida fechas y rango…", FA:"400 del servidor: el mensaje va junto al filtro" | PROBADO |
| 05 | VH:`Asesor_solo_lo_suyo` (`asesorId` de otro y `abc`), QA:`Ventas_idor_por_vehiculo_y_filtros` | PROBADO |
| 06 | VH:`Admin_ve_vigente_y_revertida` (dueño y administrador, vigente primero); FA:"vigente primero y revertida con su motivo" | PROBADO |
| 07 | VH:`Asesor_solo_lo_suyo`, VH:`Reasignado_sigue_al_dueno`, QA:`Ventas_ids_raros`, QA:`Ventas_idor_por_vehiculo_y_filtros`; FA:"404 de un vehículo ajeno: \"Vehículo no encontrado.\"" | PROBADO |
| 08 | VH:`Permisos` (401 y 403 comprador en las dos rutas), QA:`Ventas_idor_por_vehiculo_y_filtros` (403 sobre un vehículo real); FA:"comprador en #/ventas…", FA:"asesor en #/cuenta/ventas…" | PROBADO |
| 09 | VH:`Vendido_sin_fila`; FA:"VENDIDO sin filas: texto de CA-P7-09" | PROBADO |
| 10 | FA:"rango de fechas inválido no consulta; sin resultados muestra \"Limpiar filtros\"" | PROBADO |
| 11 | FA:"administrador (#/cuenta/ventas): todas las ventas, revertidas con motivo y enlace al vehículo", FA:"vigente primero y revertida con su motivo" | PROBADO (COMP) + **manual** (navegación real) |
| 12 | VH:`Admin_ve_vigente_y_revertida` (lista exacta de campos), QA:`Ventas_idor_por_vehiculo_y_filtros` | PROBADO |
| 13 | VH:`Vendido_fuera_del_portal`; VT:`Vender…` (regresión P4b) | PROBADO |
| 14 | VH:`Admin_ve_vigente_y_revertida` (total = filas de `ventas_vehiculo`), VH:`Filtros` | PROBADO |
| 15 | VT (regresión P4b): "HU-064: vender -> VENDIDO…", "D-39: fecha… -> 400", "D-39: origen OTRO con observaciones…" | PROBADO |
| 16 | VT:`Revertir` (ACTIVO, DISPONIBLE y OCULTA, quién, cuándo, motivo, auditoría con antes y después) | PROBADO (ver O-2: la acción se llama `REVERTIR`, no `REVERTIR_VENTA`) |
| 17 | VT:`Ajeno_y_admin`, VH:`Admin_ve_vigente_y_revertida` (el administrador vuelve a vender) | PROBADO |
| 18 | VT:`Revertir` (motivo obligatorio) | PROBADO |
| 19 | VT:`Revertir` ("No se puede revertir la venta de un vehículo en estado activo.") | PROBADO |
| 20 | VT:`Revertir` (reactivar la vuelve a mostrar), PU:"Reactivar: OCULTA -> PUBLICADA… sin plan… -> 409" | PROBADO |
| 21 | VH:`Admin_ve_vigente_y_revertida` (venta nueva; la anterior sigue revertida) | PROBADO |
| 22 | FI:"pide un motivo de 5 a 500 caracteres y envía { motivo }" (diálogo, explicación y `aria-required`) | PARCIAL: el texto del diálogo dice "…vuelve a estar activo y su publicación queda inactiva: para mostrarla otra vez… tendrás que reactivarla", equivalente al propuesto pero no literal (O-3) |
| 23 | QA:`Reversion_concurrente` | PROBADO |
| 24 | VT:`Permisos` (401 y 403 comprador en vender y revertir) | PROBADO |
| 25 | PL y SU completos (regresión P4p); PG:`Asignar_sin_pago` | PROBADO |
| 26 | SU:`Permisos` (renovar → 403 asesor); FM:"suspendida…", "vencida…" | PARCIAL: la API está PROBADA; "Mi suscripción" dice "Comunícate con el administrador…" pero **no** ofrece el enlace de WhatsApp del portal (O-1) |
| 27 | PG:`Asignar_con_pago`, PG:`Renovar_y_cambiar_con_pago`; FA:"con precio: ofrece el pago…", FA:"envía { pago } con el precio del plan actual" | PROBADO |
| 28 | PG:`Plan_sin_precio`; FA:"plan de precio 0: no ofrece pago y envía solo { planId }" | PROBADO |
| 29 | PG:`Pago_invalido` (10 casos, sin suscripción ni pago), PG:`Renovar_y_cambiar_con_pago` (no extiende la vigencia); FA:"acepta un pago válido y rechaza…", FA:"400 del servidor: muestra cada error…" | PROBADO |
| 30 | PG:`Asignar_con_pago` (historial y total), PG:`Orden_por_fecha`; FA:"lista con total, registra un pago posterior y anula con motivo" | PROBADO |
| 31 | PG:`Anular`, PG:`Anular_concurrente`, QA:`Anular_bordes`; FA:"lista con total… anula con motivo", FA:"409 al anular un pago ya anulado…" | PROBADO |
| 32 | PG:`Permisos` (401; 403 asesor y comprador), PG:`Asesor_no_ve_pagos`, QA:`Pagos_asesor_ni_siquiera_lo_suyo`; FA:"sin aviso del servidor no muestra nada, y el asesor no ve pagos" | PROBADO |
| 33 | SE:`Inventario_de_endpoints` (lista cerrada de rutas: ninguna de tarjeta, banco, pasarela ni factura) | PROBADO (API) + **manual** (pantallas) |
| 34 | — | No aplica: D-61 es B |
| 35 | FA:"muestra los valores, valida en cliente, guarda con PUT y no ofrece valores de negocio" | PROBADO |
| 36 | PR:`Flujo_completo` (200, valores nuevos en la lectura pública, auditoría `CAMBIAR` con antes y después; sin cambio, sin evento); FA:"usa los valores del servidor…" (pie) | PROBADO |
| 37 | PR:`Invalido` (20 casos), QA:`Redes_invalidas` (18 casos), QA:`Publico_sin_datos_internos_tras_guardar` (hosts sin `www`); FA:"acepta lo válido y rechaza…", FA:"400 del servidor: errores por campo"; FQ:"Instagram rechaza…", "TikTok rechaza…" | PROBADO |
| 38 | PR:`Flujo_completo` (valores de D-42), PR:`Valor_mal_formado`; FA:"si la lectura falla, usa los valores fijos y no muestra redes" | PROBADO |
| 39 | PR:`Flujo_completo` (TikTok vacío = null); FA:"…muestra Instagram y TikTok solo con enlace válido"; FQ:"WhatsApp con otro esquema…" | PROBADO |
| 40 | PR:`Flujo_completo` (asesor con fijo usa el nuevo; asesor con celular usa el suyo), QA:`Whatsapp_ficha_duenio_administrador` | PROBADO |
| 41 | PR:`Invalido` (HTML en el horario, `javascript:`, comillas), QA:`Redes_invalidas`; FQ:"WhatsApp con otro esquema, redes de otro dominio y HTML en el horario se muestran seguros" | PROBADO |
| 42 | PR:`Publico_solo_contacto`, QA:`Publico_sin_datos_internos_tras_guardar`; SE:`Inventario_de_endpoints` (única anónima nueva, fuera de `/api/catalogo`) | PROBADO |
| 43 | PR:`Permisos` (GET y PUT: 401; 403 asesor y comprador; nada guardado); FA:"un asesor en #/cuenta/parametros ve \"No tienes permiso\"…" | PROBADO |
| 44 | FA:"muestra los valores… y no ofrece valores de negocio" | PROBADO |
| 45 | PA:`Contadores`; FA:"contadores con enlace a la lista filtrada, ceros en palabras y sin dinero" | PROBADO |
| 46 | PA:`Contadores` (ventas = lista filtrada); FA:"el enlace de asesores pendientes abre Usuarios ya filtrado", FA:"el enlace de suscripciones vencidas…" | PROBADO (ver O-5: el contrato advierte que la lista de vencidas puede incluir reemplazadas) |
| 47 | PA:`Contadores` (orígenes y estados en 0); FA:"contadores… ceros en palabras" | PROBADO |
| 48 | PA:`Contadores` (sin nombre, correo, teléfono ni "Cop"), QA:`Panel_sin_datos_de_pagos` | PROBADO |
| 49 | PA:`Permisos`, QA:`Panel_solo_admin`; FA:"secciones nuevas…" y navegación del administrador | PROBADO |
| 50 | PA:`Aviso_vencimiento` (7 días, 1 día en singular, 8 días sin aviso, vencida y suspendida sin aviso); FA:"muestra el mensaje del servidor cuando avisoVencimiento es true", FA:"sin aviso del servidor no muestra nada…" | PROBADO |
| 51 | QA:`Panel_sin_datos_de_pagos`; FA:"contadores… sin dinero" | PROBADO (COMP) + **manual** (sin gráficos ni exportación a la vista) |
| 52 | FA:"administrador (#/cuenta/ventas)…" y "contadores…" (`aria-current="page"`), FA:"secciones nuevas, #/ventas y filtros en la consulta" | PROBADO (COMP) + **manual** (móvil) |
| 53 | SE:`Inventario_de_endpoints` (rutas de P7 con su política; `/api/ventas` un solo GET `AsesorOAdministrador`; `/api/portal` un solo GET anónimo; ninguna con "auditoria") | PROBADO |
| 54 | PG:`Asignar_con_pago`, PG:`Anular`, PR:`Flujo_completo` (auditoría en la misma transacción con antes, después y motivo) | PROBADO |
| 55 | SE:"CA-SE006-01: un token con session_version anterior -> 401 en TODOS los endpoints protegidos" y "CA-SE006-02…" (recorren todas las rutas, incluidas las de P7); FA:"401 al cargar los pagos termina la sesión" | PROBADO |
| 56 | — | **Sin prueba automática (MAN)**: la migración 016 solo se comprueba en MariaDB con datos reales (conteos antes y después); las pruebas usan SQLite creado por EF |
| 57 | Corridas 6 a 8 y 14 en verde | PROBADO |
| 58 | FA:"con precio: ofrece el pago…" (`aria-required` en Referencia con "Otro"), FA:"400 del servidor…" (errores junto al campo), FA:"400 del servidor: errores por campo" (Parámetros); `NotaObligatorio` usado en `AdminParametros`, `AdminSuscripciones` y `PagosSuscripcion` (revisión de código) | PARCIAL: atributos y mensajes PROBADOS; teclado y móvil solo **manual** |
| 59 | — | **Manual pendiente**: requiere la migración 016 aplicada por la responsable con respaldo (D-14, D-16) |

Resumen: 53 de 59 criterios con al menos una prueba automática que pasa; 3 en parte (22, 26, 58); 2 solo manuales (56, 59); 1 no aplica (34, D-61 B). Con parte manual además: 11, 33, 51, 52. Antes de QA quedaba sin prueba CA-P7-23 (reversiones simultáneas) y con hueco 32 (el asesor sobre su propia suscripción), 40 (dueño administrador), 41/37 (`data:`, `usuario@host`, dominio dentro de la ruta, mayúsculas), 42 (lectura pública después de guardar) y 07 (ids no numéricos).

## 4. Defectos y observaciones

En las corridas normales ninguna prueba falla. No se encontraron defectos del código de producción frente al contrato. Hay un fallo intermitente del backend sin identificar (D-1) y fallos del frontend solo bajo carga artificial (O-8).

- **D-1 (prueba intermitente, vista pero no identificada).** El agente de backend vio 1 fallo de 1184 en una de tres corridas. QA ejecutó la suite completa **7 veces** (3 × 1184 y 4 × 1215, dos con la máquina cargada): todas en verde. Con el subconjunto de 10 clases (concurrencia y P7, 438 casos) hizo 20 corridas: **una falló (1 de 438)**, en la 6.ª corrida del comando 9, con `vitest` corriendo a la vez; el filtro de la salida no guardó el nombre y las 12 corridas siguientes con `.trx` no lo repitieron. Por tanto el fallo intermitente **existe** (frecuencia aproximada 1 de cada 20 corridas, bajo carga) y está dentro de estas clases: `InventarioEstadosTests`, `PagosTests`, `P7PermisosQaTests`, `AdminUsuariosTests`, `RegistroTests`, `RegistroAsesorTests`, `InventarioCrearEditarTests`, `PanelTests`, `VentasHistorialTests`, `ParametrosTests`. No se puede descartar que sea una de las pruebas nuevas de QA (`P7PermisosQaTests` estaba en el subconjunto; el fallo del agente de backend fue antes de que existieran, así que al menos aquel no lo era). No se corrigió nada porque no se sabe cuál es. Hipótesis (no comprobada) para mirar primero si vuelve a pasar: las pruebas con peticiones simultáneas (`Task.WhenAll`) sobre SQLite en memoria con caché compartida (en primer lugar `RegistroTests.Registros_simultaneos_mismo_correo`, 8 registros a la vez tras una barrera, que exige exactamente un 201, siete 409 y ningún 500; después `PagosTests.Anular_concurrente`, `InventarioEstadosTests` reservas simultáneas, `RegistroTests`, `RegistroAsesorTests`, `AdminUsuariosTests` y la nueva `P7PermisosQaTests.Reversion_concurrente`): el orden de las dos peticiones no es determinista y el bloqueo de fila de MariaDB se imita con un `UPDATE` previo, que en SQLite bloquea la tabla entera. Se revisaron las comparaciones con la hora real (`DateTime.UtcNow`): todas usan márgenes que contienen la petición, no dependen de la velocidad. No se encontró ninguna dependencia de la medianoche: las pruebas de P7 usan el reloj fijo T y `HoyT` (fecha de Colombia). Recomendación: si reaparece, guardar el `.trx` (`--logger trx`) para identificarla.
- **O-1 (CA-P7-26, interfaz).** "Mi suscripción" del asesor dice "Comunícate con el administrador para renovarla" (`frontend/src/components/MiSuscripcion.tsx:90-91`), pero no muestra el enlace de WhatsApp del portal que pide el criterio (PROPUESTO). Pasos: entrar como asesor con la suscripción vencida o suspendida → `#/cuenta` → la tarjeta "Mi suscripción" no tiene enlace `wa.me`. No bloquea; decisión del coordinador.
- **O-2 (documento).** CA-P7-16 y la tabla §2 de los criterios nombran la acción de auditoría `REVERTIR_VENTA`; el código (P4b, aprobado) guarda `REVERTIR` sobre la entidad `Venta` (`Auditoria.RevertirVenta = "REVERTIR"`). Las pruebas siguen el código.
- **O-3 (documento).** El texto del diálogo "Revertir venta" no es literal al de CA-P7-22, aunque dice lo mismo.
- **O-4 (rendimiento, no bloqueante).** El paquete principal pasa de 534 kB (P6) a 581 kB y Vite avisa "Some chunks are larger than 500 kB". Conviene valorar dividir el código.
- **O-5 (contrato).** `suscripcionesVencidas` del panel excluye a los asesores que ya tienen otra vigente, pero el enlace abre `/api/admin/suscripciones?estado=VENCIDA`, que puede incluir vencidas ya reemplazadas: los números pueden no coincidir (lo admite el contrato §5). Choca con la letra de CA-P7-46 ("el número coincide con el total de esa lista").
- **O-6 (entorno).** Sin E2E: Playwright no está instalado y la API de la rama requiere la migración 016 en `camion_ventas`. Se mantiene la propuesta al coordinador de incorporar Playwright antes de añadir la dependencia.
- **O-8 (frontend bajo carga, no es de P7).** Con dos `vitest` a la vez y `dotnet test` en paralelo (carga artificial de QA), fallan 1 a 4 pruebas **distintas** en cada corrida por `findBy*` que no encuentra el elemento en 1 s (`asyncUtilTimeout` por defecto de Testing Library): `InventarioLista` (Tarjetas), `InventarioFormulario` (Categoría primero), `P4bInventario` (Estado de la publicación), `P5Leads` ("Contactos (3 nuevos)") y `P7Admin` (enlace de asesores pendientes). Con una sola corrida, incluso con la API en pruebas, pasan las 694 (comandos 12 y 14). No es un defecto del producto; si el coordinador quiere robustez en máquinas lentas, subir `asyncUtilTimeout` en `tests/setup.ts`.
- **O-7 (advertencias heredadas).** NU1902 en `MySqlConnector` 2.3.7 y xUnit2031 en `InventarioCrearEditarTests.cs:547`; no son de P7.
