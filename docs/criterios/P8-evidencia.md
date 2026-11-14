# P8 — Evidencia de pruebas: reportes, indicadores, actividad y recuperación asistida

Ítem: **P8** "Reportes, marketing e integraciones" (HU-061, HU-063, HU-075 a HU-080; D-68 A, D-69 A/A, D-70 A, D-71, D-72 A/A, D-73 D, D-74 a D-77 A).
Rama: `feature/p8-reportes`. Fecha: 2026-11-13. Autor: QA.
Fuentes: `docs/criterios/criterios-P8-reportes.md` (CA-P8-01 a 75), `docs/criterios/P8-contrato-api.md` y D-68 a D-77 en `docs/coordinacion/DECISIONES.md`.

Base de datos: las pruebas de API usan SQLite en memoria (`CamionApiFactory`, `P7Fixture` o una `InventarioApi` propia por prueba) y reloj fijo en T = 2026-10-26 15:00 UTC; **ninguna prueba toca `camion_ventas`** (D-16). Datos ficticios; no se imprimen códigos de recuperación, tokens ni contraseñas. No se modificó código de producción.

## 1. Comandos y resultados

Todos ejecutados por QA en esta sesión, desde `backend/Camion.Api.Tests` o `frontend`. No había ninguna API en marcha (`tasklist` sin procesos `Camion*`), así que se usó la carpeta de salida normal. Con `--artifacts-path` en la carpeta temporal la DLL de pruebas **no se carga**: "Una directiva de Control de aplicaciones bloqueó este archivo (0x800711C7)", y `dotnet test` termina con código 0 y **0 pruebas** (ver O-3). Por eso cada corrida se verificó con el recuento, no con el código de salida.

| # | Comando | Resultado |
|---|---|---|
| 1 | `dotnet build --artifacts-path <scratchpad>/art` | Compila (0 errores, 3 advertencias heredadas), pero `dotnet test` sobre esa salida: **NO EJECUTADA**, 0 pruebas por el bloqueo de Control de aplicaciones (O-3) |
| 2 | `dotnet build` | **PASA**: 0 errores, 3 advertencias heredadas (NU1902 `MySqlConnector` 2.3.7 dos veces; xUnit2031 en `InventarioCrearEditarTests.cs:547`) |
| 3 | `dotnet test --no-build`, corrida 1 (antes de las pruebas de QA) | **PASA**: 1303 superadas, 0 con error, 0 omitidas (1 m 21 s) |
| 4 | ídem, corrida 2 | **PASA**: 1303 / 0 / 0 (1 m 15 s) |
| 5 | ídem, tras recompilar con `P8QaTests.cs` | **NO EJECUTADA** dos veces: la DLL recién compilada quedó bloqueada por Control de aplicaciones (0 pruebas, código 0). Tras cambiar un comentario y recompilar, se cargó (O-3) |
| 6 | ídem, corrida 3 (con las pruebas de QA) | **FALLA 2**: 1313 superadas, 2 con error, 0 omitidas (1 m 32 s). Las dos son QA-P8-1 y QA-P8-2 (§4) |
| 7 | ídem, corrida 4 | **FALLA 2**: 1313 / 2 / 0 (1 m 39 s), las mismas |
| 8 | ídem, filtro `P8` (clases `P8*`, 67 casos), 3 veces | **FALLA 2** las tres veces: 65 / 2 / 0, las mismas; ningún otro fallo (también la prueba de uso simultáneo) |
| 9 | ídem, corrida 5 (con `Consultar_no_cambia_nada_ni_guarda_archivos`) | **FALLA 2**: 1314 / 2 / 0 (1 m 33 s), las mismas |
| 10 | ídem, corrida 6 | **FALLA 2**: 1314 / 2 / 0 (1 m 41 s), las mismas |
| 11 | `frontend`: `npx tsc -b` | **PASA** (sin errores, código 0) |
| 12 | `frontend`: `npx tsc -p tsconfig.test.json --noEmit` | **PASA** (sin errores, código 0; incluye `tests/P8Qa.test.tsx`) |
| 13 | `frontend`: `npx vitest run`, corrida 1 (con `P8Qa.test.tsx`) | **PASA**: 37 archivos, 750 pruebas (60,9 s) |
| 14 | ídem, corrida 2 | **PASA**: 37 archivos, 750 pruebas (65,4 s) |
| 15 | ídem, solo `tests/P8Qa.test.tsx` y `tests/P8Reportes.test.tsx` | **PASA**: 2 archivos, 39 pruebas (4 de QA + 35) |
| 16 | `frontend`: `npm run build` | **PASA** (`tsc -b && vite build`, 2,7 s; `index-*.js` 643,92 kB, gzip 183,19 kB; aviso de Vite por superar 500 kB, O-4) |
| 17 | E2E contra la API real | **NO EJECUTADA**: Playwright no está instalado (se mantiene la propuesta al coordinador) y no se hizo ninguna llamada a http://localhost:5080 ni a `camion_ventas` |

Totales finales: backend **1316** pruebas (1303 previas + 13 de QA): 1314 pasan, 2 fallan (QA-P8-1, QA-P8-2), 0 omitidas. Frontend **750** (746 previas + 4 de QA), todas pasan.

Regresión (CA-P8-72): las corridas 3 a 6 y 13 y 14 incluyen todas las pruebas de P1 a P7: P5/P5b (`ContactosTests`, `ContactosLimitesTests`, `ContactosTransaccionTests`, `LeadsGestionTests`, `LeadsBarridoTests`, `LeadsPermisosQaTests`, `AvisosTests`, `AvisosLimitesTests`, `AvisosPermisosQaTests`), P6 (`CreditosTests`, `BusquedasTests`, `MisSolicitudesTests`, `SolicitudesLimitesTests`, `ListasBusquedaTests`, `P6CorreccionesTests`, `P6PermisosQaTests`), P7 (`VentasHistorialTests`, `PagosTests`, `ParametrosTests`, `PanelTests`, `P7CorreccionesTests`, `P7PermisosQaTests`, `AnulacionD67Tests`), el inventario de rutas (`SesionTests.Inventario_de_endpoints`, con las rutas de P8) y en el frontend `P5*`, `P6*`, `P7*`. **Ninguna prueba de P1 a P7 falla**; los dos únicos fallos son pruebas nuevas de QA que documentan defectos de P8.

## 2. Pruebas añadidas por QA

`backend/Camion.Api.Tests/P8QaTests.cs` (13 pruebas; 11 pasan, 2 fallan a propósito y documentan QA-P8-1 y QA-P8-2):

| Prueba | Qué cubre | Resultado |
|---|---|---|
| `Sin_datos_personales_en_ningun_reporte_ni_csv` | CA-P8-07, 23, 45, 58: con 7 solicitudes (2 contactos, 2 avisos, 2 créditos, 1 búsqueda) cuyos nombre, correo y mensaje se marcan, sus teléfonos (con y sin +57), una nota interna, dos observaciones de cierre, una nota de reserva, observaciones de venta, referencias de pago y motivo de anulación, **busca en el contenido** de los 5 reportes JSON, los 5 CSV, indicadores, Mis indicadores y 3 actividades: ninguno aparece (el motivo de anulación sí en la actividad, como dice el contrato §10). Comprueba que el CSV de solicitudes trae los 7 ids (la búsqueda no es vacía) y que no hay columnas de contacto | PASA |
| `Csv_sin_formulas_en_ninguna_celda` | CA-P8-44 de extremo a extremo: asesores llamados `+…`, `-…`, `@…`, `=…` y un plan `=…`; se analizan **todas las celdas** de los 5 CSV: ninguna empieza por `= + - @`, tabulador o retorno sin apóstrofo | PASA |
| `Csv_auditoria_de_cada_exportacion` | CA-P8-46, 49: cada exportación de los 5 tipos con filtros propios suma exactamente 1 fila `Reporte/EXPORTAR` con el administrador, el tipo, los filtros (fechas y cada filtro enviado) y `filas` = `filas.total` del JSON; sin nombres ni cifras del archivo. Filtro inválido, sin sesión, asesor (también con su propio `asesorId`), comprador y POST → 400/401/403/405 y **ninguna** fila nueva | PASA |
| `Consultar_no_cambia_nada_ni_guarda_archivos` | CA-P8-08, 50: tras consultar los 5 reportes, 5 CSV e indicadores, los conteos y estados de leads, eventos, vehículos, publicaciones, ventas, pagos, suscripciones y auditoría (salvo `Reporte`) no cambian; no aparece ningún archivo en la carpeta de medios ni `reporte-*.csv` | PASA |
| `Mis_indicadores_alcance_y_reasignacion` | CA-P8-52, 53 (IDOR): Mis indicadores coincide con sus vehículos, "Mis contactos" (`/api/leads`) y "Mis ventas" (`/api/ventas`); un asesor sin datos ve ceros y recibe lo mismo con `asesorId`, `responsable`, `usuarioId`, `perfilId` o `todos` de otro; rutas con id de otro → 404/405; tras reasignar el vehículo, deja de contar para el primero y pasa al segundo; nunca créditos, búsquedas, avisos, pagos, ingresos ni comparaciones | PASA |
| `Mis_indicadores_cuentas_no_activas` | CA-P8-55: asesor bloqueado → su token da 401/403 y no inicia sesión; pendiente y rechazado no inician sesión; un token firmado a mano para la cuenta rechazada → 401/403 | PASA |
| `Actividad_permisos_y_metodos` | CA-P8-59, 60: el asesor **dueño** (sin reasignar) recibe 403 en la actividad de su vehículo y de su suscripción; comprador 403; token inválido 401; POST/PUT/DELETE/PATCH → 405/404; ids `0`, `-1`, `abc`, `1.5`, enorme → 404/400; no existe ruta del asesor | PASA |
| `Codigo_nunca_en_logs_ni_auditoria` | CA-P8-67: dos enlaces, intentos con el código anulado, contraseña débil, código alterado, éxito y reutilización: ni los enlaces, ni los códigos (ni sus primeros 40 caracteres), ni las contraseñas aparecen en **ningún** log ni en **ninguna** fila de `auditoria_eventos`; `GENERAR_ENLACE_RECUPERACION` guarda solo `venceEn` | PASA |
| `Uso_simultaneo_un_solo_exito` | CA-P8-67 (un solo uso): dos peticiones simultáneas con el mismo código → una 200 y una 400, nunca 500; `SessionVersion` sube 1 vez; un solo evento | PASA (3 de 3 corridas del filtro y 4 completas) |
| `Cierra_todas_las_sesiones` | CA-P8-67, D-12: generar no cambia `SessionVersion`; restablecer la sube y **las dos** sesiones abiertas dan 401 en `/me` y en Mis indicadores | PASA |
| `Limite_bloquea_tambien_el_codigo_bueno` | D-73 D: con el límite por defecto (5) la 6.ª petición da 429 aunque el código sea válido, la contraseña no cambia y no hay evento; generar y el contacto público no comparten el cupo | PASA |
| `No_activas_y_desbloqueo` | Rechazado → 409 y sin auditoría; código emitido antes de bloquear → 400 mientras está bloqueado; **tras desbloquear vuelve a servir** | **FALLA** (QA-P8-1) |
| `Codigo_de_cuenta_promovida_a_administrador` | Código emitido para un comprador que luego se promueve a administrador (`PromoverAdmin`, D-13) | **FALLA** (QA-P8-2) |

`frontend/tests/P8Qa.test.tsx` (4 pruebas, todas pasan):

| Prueba | Qué cubre |
|---|---|
| "el asesor dueño no ve la sección \"Actividad\" ni se llama a la ruta del administrador" | CA-P8-60 (faltaba): en `#/inventario/9` como asesor no hay título "Actividad", ni botón, ni texto de la sección, ni llamadas a `/actividad` ni a `/api/admin/` |
| "el administrador sí la ve en el mismo vehículo y la carga solo al pulsar" | CA-P8-56, 60: control positivo de la prueba anterior |
| "nunca envía asesorId ni otros filtros aunque vengan en la URL; no hay CSV" | CA-P8-52, 54: `#/mis-indicadores?asesorId=99&origen=CREDITO` llama solo a `/api/asesor/indicadores?desde&hasta`; sin botón de exportar ni rutas `/csv` o `/api/admin/` |
| "se quita con replaceState, no queda en el historial ni en localStorage/sessionStorage y solo viaja en el cuerpo" | CA-P8-67: el código sale de la URL sin añadir entrada al historial; no queda en `history.state`, `localStorage`, `sessionStorage` ni en el DOM; la petición es POST sin `Authorization` y ninguna URL lo contiene |

## 3. Matriz de trazabilidad CA-P8 → prueba

Abreviaturas: **RT** = `backend/Camion.Api.Tests/P8ReportesTests.cs`, **AT** = `P8ActividadTests.cs`, **RC** = `P8RecuperacionTests.cs`, **QA** = `P8QaTests.cs` (nuevo), **SE** = `SesionTests.cs`, **PA** = `PanelTests.cs`, **P7** = `P7PermisosQaTests.cs`, **FR** = `frontend/tests/P8Reportes.test.tsx`, **FQ** = `frontend/tests/P8Qa.test.tsx` (nuevo), **F7** = `frontend/tests/P7Admin.test.tsx`. El nombre es el método xUnit o el título del `it`.

| CA | Cobertura | Estado |
|---|---|---|
| 01 | FR:"abre en \"Reportes\" con el mes en curso, cifras y filas, y Reportes marcado en la navegación", FR:"aplica los filtros propios a la vez" | PROBADO |
| 02 | RT:`Periodo_por_defecto_y_vacio`; FR:"el período por defecto es el mes en curso, también en febrero bisiesto" | PROBADO |
| 03 | RT:`Reporte_vehiculos`, `Reporte_solicitudes`, `Reporte_creditos`, `Reporte_ventas`, `Reporte_suscripciones` (filtros combinados); FR:"aplica los filtros propios a la vez (asesor y categoría)" | PROBADO |
| 04 | RT:`Filtros_invalidos` (21 casos), RT:`Municipio_de_otro_departamento`; FR:"valida fechas, orden y máximo 366 días…", "valida el período en cliente sin llamar a la API", "muestra el 400 del servidor junto al filtro" | PROBADO |
| 05 | RT:`Periodo_por_defecto_y_vacio`; FR:"sin datos: \"No hay datos para estos filtros.\" con \"Limpiar filtros\"" | PROBADO |
| 06 | RT:`Permisos_administrador`; FR:"COMPRADOR/ASESOR no ve Reportes ni Indicadores y no se llama a la API", "403 de la API muestra el mensaje de permiso" | PROBADO |
| 07 | QA:`Sin_datos_personales_en_ningun_reporte_ni_csv` (búsqueda en el contenido de todos los reportes), RT:`Reporte_solicitudes` | PROBADO |
| 08 | QA:`Consultar_no_cambia_nada_ni_guarda_archivos`; SE:`Inventario_de_endpoints` (solo GET) | PROBADO |
| 09 | RT:`Reporte_solicitudes` (bandeja), RT:`Reporte_ventas` (`/api/ventas`), RT:`Reporte_suscripciones` (total de pagos P7); QA:`Mis_indicadores_alcance_y_reasignacion` (inventario, Mis contactos, Mis ventas) | PROBADO |
| 10 | RT:`Sesion_cerrada`; FR:"401 termina la sesión" | PROBADO |
| 11 | RT:`Reporte_vehiculos` | PROBADO |
| 12 | RT:`Reporte_vehiculos` (asesor, categoría, departamento, municipio) | PROBADO |
| 13 | RT:`Reporte_vehiculos` (por estado, visibles y destacadas) | PROBADO |
| 14 | RT:`Reporte_vehiculos` (eliminada aparte) | PROBADO |
| 15 | RT:`Reporte_vehiculos` (más contactados), QA:`Sin_datos_personales…` | PROBADO |
| 16 | RT:`Reporte_vehiculos` (vehículo DEMO sin publicación ni fotos) | PROBADO |
| 17 | RT:`Reporte_solicitudes` | PROBADO |
| 18 | RT:`Reporte_solicitudes`; FR:"solicitudes: traduce origen y resultado a los nombres de la bandeja" | PROBADO |
| 19 | RT:`Reporte_solicitudes` (reasignado a mano, barrido D-46) | PROBADO |
| 20 | RT:`Reporte_solicitudes`, RT:`Filtros_invalidos` (resultado de otro origen) | PROBADO |
| 21 | RT:`Reporte_solicitudes` (tiempo medio con el primer cierre) | PROBADO |
| 22 | RT:`Reporte_solicitudes` (reabierta y cerrada de nuevo) | PROBADO |
| 23 | RT:`Reporte_solicitudes`, QA:`Sin_datos_personales…` (notas y observaciones de cierre) | PROBADO |
| 24 | RT:`Reporte_creditos` | PROBADO |
| 25 | RT:`Reporte_creditos`, RT:`Periodo_por_defecto_y_vacio` (`null` sin cerrados) | PROBADO |
| 26 | RT:`Reporte_creditos` (sin montos ni entidad) | PROBADO |
| 27 | RT:`Indicadores_asesor`, QA:`Mis_indicadores_alcance_y_reasignacion` (crédito desde su ficha no aparece) | PROBADO |
| 28 | RT:`Reporte_ventas` | PROBADO |
| 29 | RT:`Reporte_ventas` (revertidas aparte) | PROBADO |
| 30 | RT:`Reporte_suscripciones` | PROBADO |
| 31 | RT:`Reporte_suscripciones` (anulados aparte, por medio) | PROBADO |
| 32 | RT:`Reporte_suscripciones` (pago del script 002 sin fecha) | PROBADO |
| 33 | RT:`Periodo_por_defecto_y_vacio`; FR:"suscripciones: aviso visible de pagos registrados a mano" | PROBADO |
| 34 | RT:`Indicadores_administrador`; FR:"valores en pesos, comparación, \"Sin datos\" y 4 gráficos…" | PROBADO |
| 35 | RT:`Indicadores_administrador` (período anterior y diferencia); FR:ídem ("+3 frente al período anterior") | PROBADO |
| 36 | RT:`Indicadores_administrador` (4 gráficos, 12 meses); FR:ídem (4 `figure`) | PROBADO |
| 37 | FR:"…4 gráficos con su tabla equivalente" (tabla accesible por gráfico) | PROBADO (COMP) + **manual** (lector de pantalla y contraste) |
| 38 | RT:`Indicadores_administrador`, RT:`Periodo_por_defecto_y_vacio`; FR:"…formatea \"Sin datos\"" | PROBADO |
| 39 | RT:`Indicadores_administrador` (panel sin "Cop"), PA y P7:`Panel_sin_datos_de_pagos` (regresión); FR:"el panel Inicio ofrece \"Ver indicadores\"" | PROBADO |
| 40 | RT:`Permisos_administrador`, RT:`Permisos_asesor` | PROBADO |
| 41 | RT:`Indicadores_administrador` (sin "visita"); FR:"…sin visitas" | PROBADO |
| 42 | RT:`Exportar_csv` (nombre y adjunto en los 5 tipos); FR:"\"Descargar CSV\" usa fetch con el token en el encabezado…" | PROBADO |
| 43 | RT:`Exportar_csv` (BOM, `;`, CRLF, enteros) | PROBADO (API) + **manual** (abrir en Excel en español) |
| 44 | RT:`Celda_csv` (10 casos), RT:`Exportar_csv`, QA:`Csv_sin_formulas_en_ninguna_celda` (todas las celdas de los 5 CSV) | PROBADO |
| 45 | QA:`Sin_datos_personales_en_ningun_reporte_ni_csv` (5 CSV), RT:`Exportar_csv` | PROBADO |
| 46 | RT:`Exportar_csv`, QA:`Csv_auditoria_de_cada_exportacion` (cada tipo, filtros, filas; rechazadas sin fila) | PROBADO |
| 47 | RT:`Exportar_demasiadas_filas`; FR:"CSV con demasiadas filas: muestra el error y no guarda archivo" | PROBADO |
| 48 | RT:`Filtros_invalidos` (mismo 400 en el CSV) | PROBADO |
| 49 | RT:`Permisos_administrador`, QA:`Csv_auditoria_de_cada_exportacion` (asesor con su propio `asesorId`, comprador, sin sesión) | PROBADO |
| 50 | RT:`Exportar_csv` (`no-store`), QA:`Consultar_no_cambia_nada_ni_guarda_archivos` (sin archivos) | PROBADO |
| 51 | RT:`Indicadores_asesor`; FR:"muestra solo lo suyo, sin exportar" | PROBADO |
| 52 | RT:`Indicadores_asesor`, QA:`Mis_indicadores_alcance_y_reasignacion`; FQ:"nunca envía asesorId ni otros filtros…" | PROBADO |
| 53 | RT:`Indicadores_asesor`, QA:`Mis_indicadores_alcance_y_reasignacion` | PROBADO |
| 54 | RT:`Permisos_asesor` (sin ruta CSV); FR:"muestra solo lo suyo, sin exportar"; FQ:"…no hay CSV" | PROBADO |
| 55 | RT:`Permisos_asesor`, QA:`Mis_indicadores_cuentas_no_activas`; FR:"un comprador ve \"No tienes permiso\"…" | PROBADO |
| 56 | AT:`Actividad_vehiculo`; FR:"se carga al pulsar \"Ver actividad\"…"; FQ:"el administrador sí la ve…" | PROBADO |
| 57 | AT:`Actividad_suscripcion` | PROBADO |
| 58 | AT:`Actividad_vehiculo` (cambios del EDITAR, sin nota de reserva ni observaciones), QA:`Sin_datos_personales…` (actividades) | PROBADO |
| 59 | AT:`Actividad_vehiculo` (no cambia nada), QA:`Actividad_permisos_y_metodos` (solo GET), SE:`Inventario_de_endpoints` (sin "auditoria" ni "historial") | PROBADO |
| 60 | AT:`Actividad_vehiculo`, `Actividad_suscripcion`, RT:`Permisos_administrador`, QA:`Actividad_permisos_y_metodos`; FQ:"el asesor dueño no ve la sección \"Actividad\"…" | PROBADO |
| 61 | AT:`No_encontrado`; FR:"404 muestra \"Vehículo no encontrado.\"" | PROBADO |
| 62 | AT:`Actividad_vehiculo` (20 por defecto, `tamano=101` → 400) | PROBADO (con 8 eventos y `tamano=3`; no con más de 50 reales) |
| 63 | Revisión: `Camion.Api.csproj` y `package.json` sin dependencias de correo, WhatsApp ni SMS | **Sin prueba automática** (revisión de dependencias + MAN) |
| 64 | Revisión: `frontend/index.html` solo carga `/src/main.tsx`; `dependencies` = `react`, `react-dom` | **Sin prueba automática** (revisión + MAN) |
| 65 | SE:`Inventario_de_endpoints` (lista cerrada, sin pasarela) | PROBADO |
| 66 | — | No aplica mientras D-73 sea D (no hay servicio de correo) |
| 67 | RC (7 pruebas), QA:`Codigo_nunca_en_logs_ni_auditoria`, `Uso_simultaneo_un_solo_exito`, `Cierra_todas_las_sesiones`, `Limite_bloquea_tambien_el_codigo_bueno`; FR:"confirma, genera y muestra el enlace…", "lee el código, lo quita de la URL…", "confirmación distinta…", "enlace vencido o usado…", "429…"; FQ:"se quita con replaceState…" | **PROBADO con 2 defectos** (QA-P8-1, QA-P8-2: QA:`No_activas_y_desbloqueo`, QA:`Codigo_de_cuenta_promovida_a_administrador` FALLAN) |
| 68 | FR:"el inicio de sesión remite al WhatsApp del portal" | PROBADO (el texto se mantiene junto con D-73 D) |
| 69 | SE:`Inventario_de_endpoints` (rutas de P8 con su política; la única anónima nueva es la recuperación, registrada en el contrato "Pendiente" 3) | PROBADO |
| 70 | RT:`Exportar_demasiadas_filas` consulta 10.001 solicitudes en SQLite (indicio, sin medir tiempo) | **Sin prueba automática** del tiempo < 3 s con 5.000 solicitudes y 1.000 vehículos en MariaDB (MAN) |
| 71 | — | **Manual**: P8 no tiene migración; se comprueba en el despliegue |
| 72 | Corridas 3 a 6, 13 y 14 | **FALLA 2** (solo QA-P8-1 y QA-P8-2; sin regresiones de P1 a P7) |
| 73 | FR:"abre en \"Reportes\"… y Reportes marcado en la navegación" (`aria-current`), filtros con etiqueta (`getByLabelText`), errores del backend junto al campo | PROBADO (COMP) + **manual** (teclado y móvil) |
| 74 | — | **Manual pendiente** (responsable, en `camion_ventas`) |
| 75 | — | **Sin cubrir**: no existe `docs/manual/P8-reportes.md` (tarea del documentador) |

Resumen: **64 de 75** criterios con al menos una prueba automática que pasa (dos de ellos, 43 y 37, con parte manual); CA-P8-67 y 72 están cubiertos pero con **2 pruebas que fallan** por defectos (QA-P8-1, QA-P8-2); 62 cubierto con menos eventos de los que dice el criterio; sin prueba automática: 63, 64, 70, 71, 74 (manuales o de revisión); 66 no aplica; 75 sin cubrir (falta la guía). Antes de QA faltaban: la búsqueda de datos personales en todo el contenido (07, 45), la auditoría por tipo y la ausencia de auditoría en exportaciones rechazadas (46, 49), la comprobación de solo lectura y de archivos (08, 50), el IDOR y la coherencia de Mis indicadores tras reasignar (52, 53), la cuenta bloqueada (55), el asesor dueño en la pantalla de su vehículo (60), y en la recuperación: logs y auditoría completos, uso simultáneo, varias sesiones, límite con un código válido y el código fuera del historial y del almacenamiento (67).

## 4. Defectos y observaciones

No se corrigió código de producción.

- **QA-P8-1 (seguridad, media). Un enlace de recuperación emitido antes de bloquear una cuenta vuelve a servir al desbloquearla.** El bloqueo (`POST /api/admin/usuarios/{id}/bloquear`) no cambia el `SecurityStamp` y `restablecer-contrasena` solo comprueba que la cuenta esté ACTIVA en el momento de usarse (`RecuperacionEndpoints.cs`, `if (user is null || user.EstadoCuenta != EstadosCuenta.Activa)`). Si el administrador bloquea una cuenta porque el enlace se envió a quien no era, y la desbloquea dentro de los 60 minutos, el enlace filtrado vuelve a funcionar. Pasos: (1) como administrador, `POST /api/admin/usuarios/{id}/enlace-recuperacion` para un asesor activo; (2) `POST /api/admin/usuarios/{id}/bloquear` con motivo; (3) `POST /api/auth/restablecer-contrasena` con el código → 400 (correcto); (4) `POST /api/admin/usuarios/{id}/desbloquear`; (5) repetir el paso 3 dentro de la hora → **200** y la contraseña cambia. Esperado: 400 (el contrato §11 y el comentario de `RC:Permisos_y_casos_de_generacion` dicen "Un código emitido antes de bloquear la cuenta ya no sirve"). Prueba: `P8QaTests.No_activas_y_desbloqueo` (falla). Sugerencia para quien programe: cambiar el `SecurityStamp` al bloquear (o al cambiar el estado de la cuenta).
- **QA-P8-2 (seguridad, baja; decisión). Un enlace emitido para un comprador o asesor sirve aunque la cuenta pase después a administrador.** `restablecer-contrasena` no comprueba el rol y `PromoverAdmin` (D-13) no cambia el `SecurityStamp`. Pasos: (1) generar el enlace de un comprador activo; (2) promoverlo con la herramienta `PromoverAdmin` (línea de comandos); (3) usar el código dentro de la hora → **200**: se cambia la contraseña de una cuenta de administrador mediante un enlace, lo que el contrato §11 excluye para administradores. Requiere acceso técnico para promover, por eso la gravedad es baja. Prueba: `P8QaTests.Codigo_de_cuenta_promovida_a_administrador` (falla). Decisión del coordinador: rechazar el código si la cuenta es administradora al usarlo, o cambiar el `SecurityStamp` al promover.
- **O-1 (contrato).** CA-P8-69 dice "ninguna anónima nueva"; P8 añade `POST /api/auth/restablecer-contrasena` (anónima, con límite propio), registrada en el contrato ("Pendiente" 3) y en `SesionTests`. Coherente con D-73 D; se deja anotado.
- **O-2 (prueba frágil, ya resuelta en la prueba).** En `#/mis-indicadores?asesorId=…` la pantalla se monta dos veces al leer la URL: un `findByRole` devolvía el título ya desmontado. La prueba espera a que el título quede estable. No es un defecto visible.
- **O-3 (entorno).** Windows "Control de aplicaciones" (Smart App Control) bloquea a veces la DLL de pruebas recién compilada (0x800711C7): en la carpeta temporal siempre, y una vez en `bin/Debug`. `dotnet test` termina entonces con código 0 y **0 pruebas** ("No hay ninguna prueba disponible"). Recompilar con un cambio mínimo lo resolvió. Recomendación: comprobar siempre el recuento "Total: N", no el código de salida.
- **O-4 (rendimiento, no bloqueante).** El paquete principal pasa de 581 kB (P7) a 644 kB; Vite avisa por superar 500 kB. Conviene dividir el código (p. ej. cargar Reportes e Indicadores con `import()`).
- **O-5 (cobertura).** CA-P8-70 (rendimiento con 5.000 solicitudes y 1.000 vehículos) no tiene medición; las pruebas usan SQLite y no representan MariaDB. CA-P8-75: falta la guía `docs/manual/P8-reportes.md`.
- **O-6 (entorno).** Sin E2E: Playwright no está instalado; se mantiene la propuesta al coordinador antes de añadir la dependencia.
- **O-7 (advertencias heredadas).** NU1902 en `MySqlConnector` 2.3.7 y xUnit2031 en `InventarioCrearEditarTests.cs:547`; no son de P8.
