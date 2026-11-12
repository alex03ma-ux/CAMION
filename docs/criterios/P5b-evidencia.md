# P5b — Evidencia de pruebas: avisos "Vende tu camión" y "Compra tu seguro todo riesgo"

Ítem: **P5b** (HU-N01, HU-N02; D-6, D-7, D-9, D-44, D-50 a D-53).
Rama: `feature/p5b-avisos`. Fecha: 2026-11-06. Autor: QA.
Fuentes: `docs/criterios/criterios-P5b-avisos.md` (CA-P5b-01 a 62), `docs/criterios/P5b-contrato-api.md` y D-50 a D-53 en `docs/coordinacion/DECISIONES.md`.

Base de datos: las pruebas de API usan SQLite en memoria (`CamionApiFactory`, `P5Fixture`) y reloj fijo; **ninguna prueba toca `camion_ventas`** (D-16). Datos ficticios; no se imprimen tokens ni contraseñas.

## 1. Comandos y resultados

Todos ejecutados por QA en esta sesión. No hay archivo de solución (`.sln`): `dotnet build` y `dotnet test` se ejecutan sobre `backend/Camion.Api.Tests`, que compila también `Camion.Api`.

| # | Comando (desde la carpeta indicada) | Resultado |
|---|---|---|
| 1 | `backend/Camion.Api.Tests`: `dotnet build --artifacts-path <scratchpad>/art-qa-p5b` | **PASA**: 0 errores, 3 advertencias (NU1902 de `MySqlConnector` 2.3.7, dos veces; xUnit2031 en `InventarioCrearEditarTests.cs:547`), 19,6 s |
| 2 | ídem `dotnet test --no-build` (antes de las pruebas de QA) | **PASA**: 1020 superadas, 0 con error, 0 omitidas (1 m 3 s) |
| 3 | ídem, solo `AvisosPermisosQaTests` (nuevo) | **PASA**: 9 superadas, 0 con error, 0 omitidas |
| 4 | ídem, suite completa con las pruebas de QA | **PASA**: 1029 superadas, 0 con error, 0 omitidas (1 m 7 s) |
| 5 | `frontend`: `npx tsc -b` | **PASA** (sin errores) |
| 6 | `frontend`: `npx tsc -p tsconfig.test.json --noEmit` antes de la corrección | **FALLA**: `tests/catalogoDatos.ts(123,3): error TS2322` (`esPropio` puede ser `undefined`) |
| 7 | ídem después de la corrección (§2) | **PASA** (sin errores) |
| 8 | `frontend`: `npx vitest run` (antes de las pruebas de QA, con la corrección) | **PASA**: 30 archivos, 569 pruebas (58,8 s) |
| 9 | ídem, solo `tests/P5bAvisosQa.test.tsx` (nuevo) | **PASA**: 1 archivo, 7 pruebas |
| 10 | ídem, suite completa con las pruebas de QA | **PASA**: 31 archivos, 576 pruebas (46,3 s) |
| 11 | `frontend`: `npm run build` | **PASA** (`vite build`, 98 módulos, `index-*.js` 494,24 kB, gzip 143,12 kB) |
| 12 | E2E de P5b contra la API real | **NO EJECUTADA**: Playwright no está instalado en el proyecto y la API de esta rama no debe arrancar contra `camion_ventas` sin la migración 014 (riesgo de despliegue del contrato). No se hizo ninguna llamada a http://localhost:5080. |

Regresión: las ejecuciones 4 y 10 incluyen todas las pruebas de P1 a P5 (contacto desde la ficha, asignación, bandeja, cierre, reapertura, reasignación y barrido D-46 en `ContactosTests`, `ContactosLimitesTests`, `ContactosTransaccionTests`, `LeadsGestionTests`, `LeadsBarridoTests`, `LeadsPermisosQaTests`, `P5Contacto.test.tsx`, `P5Leads.test.tsx`); ninguna falla.

## 2. Cambios de QA

**Corrección de la prueba** `frontend/tests/catalogoDatos.ts`: el ayudante `detalle()` no daba valor a `esPropio`, campo obligatorio de `DetalleVehiculo` desde P5 (CA-P5-11). Se añade `esPropio: false` antes de `...extra`. Solo afecta a los datos simulados; el cliente ya normaliza `esPropio === true`, así que ninguna prueba cambia de comportamiento (comando 8).

`backend/Camion.Api.Tests/AvisosPermisosQaTests.cs` (8 métodos, 9 casos, todos pasan):

| Prueba | Qué cubre |
|---|---|
| `Reasignacion_de_vehiculo_y_bloqueo_no_mueven_avisos` | CA-P5b-36, 61: la reasignación del vehículo (D-34) y el bloqueo del asesor mueven su contacto de vehículo (regresión CA-P5-51, 52) pero no el aviso que ese asesor envió; sin eventos REASIGNADO ni auditoría |
| `Sin_edicion_borrado_ni_lectura_de_avisos` | CA-P5b-45, D-53 A: PUT/PATCH/DELETE en `/api/leads/{id}` y `/api/avisos/*`, y GET en `/api/avisos/*`, con administrador, comprador y sin token → 404/405 (o 401/403); el aviso no cambia |
| `Comprador_no_ve_ni_toca_avisos_ajenos` | CA-P5b-48: comprador → 403 en lista, detalle, nota, cerrar, reabrir y reasignar sobre el aviso de otra persona; no se marca visto ni se añaden eventos |
| `Asesor_rechazado_o_bloqueado_envia_como_visitante` (rechazar, bloquear) | CA-P5b-51: su aviso se acepta (201, Administración, sin `usuario_id` porque su sesión no es válida) y `/api/leads` le da 401 |
| `Version_de_politica_del_cliente_se_ignora` | CA-P5b-10, 20: `versionPolitica`, `usuarioId` y `vistoPorResponsable` del cuerpo se ignoran; la prueba usa la versión del servidor, `entidad = Lead` |
| `Filtro_por_vehiculo_excluye_avisos` | CA-P5b-44: `vehiculoId` excluye los avisos (la prueba previa solo cubría `publicacionId`) |
| `Comprador_autoriza_en_cada_envio` | CA-P5b-21: con sesión, sin casilla → 400; dos envíos aceptados → dos pruebas ligadas a su cuenta |
| `Reasignar_aviso_cerrado_o_reabierto_409` | CA-P5b-43 (D-52 A): también 409 con el aviso cerrado y tras reabrirlo; el asesor nunca lo ve |

`frontend/tests/P5bAvisosQa.test.tsx` (7 pruebas, todas pasan): pantalla del seguro con el formulario vacío (errores de nombre, teléfono, tipo y casilla, foco, sin llamada; CA-P5b-13, 14, 18, 19, 31), teléfonos inválidos `3001234`, `+1 305 555 1234` y `2001234567` (CA-P5b-14), todo válido sin casilla (CA-P5b-19), y 429 / sin conexión conservando los datos (CA-P5b-30).

## 3. Matriz de trazabilidad CA-P5b → prueba

Abreviaturas: **AV** = `backend/Camion.Api.Tests/AvisosTests.cs`, **AL** = `AvisosLimitesTests.cs`, **AQ** = `AvisosPermisosQaTests.cs` (nuevo), **SE** = `SesionTests.cs`, **CT** = `ContactosTests.cs`, **LG** = `LeadsGestionTests.cs`, **LB** = `LeadsBarridoTests.cs`, **FA** = `frontend/tests/P5bAvisos.test.tsx`, **FQ** = `frontend/tests/P5bAvisosQa.test.tsx` (nuevo), **FL** = `frontend/tests/P5Leads.test.tsx`. El nombre es el método xUnit o el título del `it`.

| CA | Cobertura | Estado |
|---|---|---|
| 01 | AV:`Vende_anonimo`; FA:"anónimo: POST sin token con el cuerpo del contrato, éxito en role=\"status\"…" | PROBADO |
| 02 | AV:`Con_sesion_ligado_a_la_cuenta`; FA:"CA-P5b-02 / 21 con sesión de comprador: prellena el perfil…" | PROBADO |
| 03 | AV:`Nombre_invalido`; FA:"validación en el navegador: errores por campo…" | PROBADO |
| 04 | AV:`Telefono_invalido`; FA:"CA-P5b-04 el teléfono %s se rechaza" | PROBADO |
| 05 | AV:`Telefono_normalizado` (incluye fijo 60…) | PROBADO |
| 06 | AV:`Descripcion_obligatoria`; FA:"validación en el navegador…" | PROBADO |
| 07 | AV:`Descripcion_500_y_501`; FA:"CA-P5b-07 contador \"N/500\"…", FA:"501 caracteres (pegados…) se rechazan" | PROBADO |
| 08 | AV:`Campos_manipulados_se_ignoran` (ciudad, marca, año); FA:"sin sesión muestra h1, solo nombre, teléfono…" | PROBADO |
| 09 | AV:`Correo_opcional`; FA:"CA-P5b-09 correo mal escrito se marca…" | PROBADO |
| 10 | AV:`Campos_manipulados_se_ignoran`, AQ:`Version_de_politica_del_cliente_se_ignora` | PROBADO |
| 11 | AV:`Seguro_valido`; FA:"el tipo de vehículo es un select nativo…", FA:"CA-P5b-16 con \"Otro\"…" | PROBADO |
| 12 | AV:`Con_sesion_ligado_a_la_cuenta`; FA:"CA-P5b-12 con sesión envía el token" | PROBADO |
| 13 | AV:`Nombre_invalido` (ruta del seguro); FQ:"vacío: errores en nombre, teléfono, tipo y casilla…" | PROBADO |
| 14 | AV:`Telefono_invalido`, AV:`Telefono_normalizado` (ruta del seguro); FQ:"CA-P5b-14 el teléfono %s se rechaza…" | PROBADO |
| 15 | AV:`Seguro_tipo_invalido`; FA:"sin tipo de vehículo…", FA:"un 400 en tipoVehiculo del servidor…" | PROBADO |
| 16 | AV:`Seguro_valido` (OTRO); FA:"CA-P5b-16 con \"Otro\"…" | PROBADO |
| 17 | AV:`Correo_opcional` (ruta del seguro); FA:"CA-P5b-16 con \"Otro\"…" (`correo: null`) | PROBADO |
| 18 | FA:"sin sesión muestra h1…" (casilla sin marcar, `#/privacidad`); FQ:"vacío: errores…" (seguro) | PROBADO |
| 19 | AV:`Sin_autorizacion`; FA:"validación en el navegador…"; FQ:"CA-P5b-19 todo válido pero sin marcar la casilla…" | PROBADO |
| 20 | AV:`Vende_anonimo`, AV:`Seguro_valido`; AL:`Falla_al_guardar_no_deja_nada` (transacción); AQ:`Version_de_politica_del_cliente_se_ignora` | PROBADO |
| 21 | AQ:`Comprador_autoriza_en_cada_envio`; FA:"CA-P5b-02 / 21 con sesión de comprador…" | PROBADO |
| 22 | AV:`Campo_trampa`; FA:"el campo trampa está fuera del orden de tabulación…" | PROBADO |
| 23 | AV:`Tamano_y_formato` | PROBADO |
| 24 | AL:`Cupo_por_ip_compartido`, AL:`Diario_por_ip`; SE:`Inventario_de_endpoints` (las dos rutas con `LimitePeticionesContacto`) | PROBADO |
| 25 | AL:`Por_telefono_compartido` | PROBADO |
| 26 | AV:`Aviso_repetido` | PROBADO |
| 27 | AV:`Aviso_nuevo_tras_cierre_u_otro_origen` | PROBADO |
| 28 | FA:"anónimo: POST sin token… formulario vacío", FA:"CA-P5b-16 con \"Otro\"…" (formulario vacío); AV/AQ: `AvisoOk` exige el mismo cuerpo en nuevo, repetido y trampa | PROBADO |
| 29 | FA:"CA-P5b-29 un 400 del servidor marca cada campo, conserva lo escrito…" | PROBADO |
| 30 | FA:"CA-P5b-30 %s muestra un mensaje comprensible…" (429, 413, 500, sin conexión); FQ:"%s: mensaje comprensible…" (seguro) | PROBADO |
| 31 | FA:"sin sesión muestra h1…" (nota y `aria-required`), FA:"CA-P5b-31 no se envía dos veces…"; FQ:"vacío: errores…" | PROBADO |
| 32 | AV:`Vende_anonimo`, AV:`Con_sesion_ligado_a_la_cuenta`, AV:`Campos_manipulados_se_ignoran` | PROBADO |
| 33 | AV:`Vende_anonimo` (conteos de vehículos y publicaciones) | PROBADO |
| 34 | AV:`Vende_anonimo`, AV:`Seguro_valido` (auditoría), AV:`Logs_sin_datos` | PROBADO |
| 35 | AV:`Con_sesion_ligado_a_la_cuenta` (asesor y administrador) | PROBADO |
| 36 | AV:`Barrido_no_toca_avisos` (plan vencido), AQ:`Reasignacion_de_vehiculo_y_bloqueo_no_mueven_avisos` (D-34 y bloqueo) | PROBADO |
| 37 | AV:`Admin_lista_y_detalle`; FA:"cada solicitud muestra su origen…", FA:"filtra por origen…", FA:"un 400 \"Origen no válido.\"…" | PROBADO |
| 38 | AV:`Admin_lista_y_detalle`; FA:"\"Vende tu camión\": origen, descripción, \"Sin vehículo asociado\"…", FA:"seguro: muestra el tipo de vehículo" | PROBADO |
| 39 | AV:`Admin_lista_y_detalle` (`nuevos` +2 y −1 al abrir), AV:`Aviso_repetido` (no vuelve a ser nuevo); FL:"el administrador también lo ve…" (el enlace muestra lo que da el resumen) | PROBADO |
| 40 | AV:`Admin_seguimiento_de_aviso` | PROBADO |
| 41 | AV:`Admin_seguimiento_de_aviso`, AV:`Atendida_no_aplica_a_contacto`; FA:"el cierre de un aviso ofrece \"Solicitud atendida\"…", FA:"un 400 \"Ese resultado no aplica…\"", FA:"resultados por origen según la tabla del contrato" | PROBADO |
| 42 | AV:`Admin_seguimiento_de_aviso` | PROBADO |
| 43 | AV:`Reasignar_aviso_409`, AQ:`Reasignar_aviso_cerrado_o_reabierto_409`, AV:`Checks_de_la_base` (`CK_Lead_AvisoAdmin`); FA:"\"Vende tu camión\": … sin \"Reasignar\"" | PROBADO |
| 44 | AV:`Admin_lista_y_detalle` (`publicacionId`), AQ:`Filtro_por_vehiculo_excluye_avisos` (`vehiculoId`) | PROBADO |
| 45 | AQ:`Sin_edicion_borrado_ni_lectura_de_avisos` | PROBADO |
| 46 | AV:`Asesor_no_ve_avisos`; FA:"el asesor no tiene filtro ni etiqueta de origen…", FA:"el detalle del asesor no muestra el origen…" | PROBADO |
| 47 | AV:`Asesor_no_ve_avisos` (404 en detalle, nota y cierre; 403 en reabrir y reasignar) | PROBADO |
| 48 | AV:`Comprador_y_anonimo`, AQ:`Comprador_no_ve_ni_toca_avisos_ajenos` | PROBADO |
| 49 | FA:"el comprador que envía un aviso no llama a /api/leads ni ve \"Mis solicitudes\" (D-53 A)"; AQ:`Sin_edicion_borrado_ni_lectura_de_avisos` (no hay GET en `/api/avisos`) | PROBADO |
| 50 | AV:`Comprador_y_anonimo` (401), AV:`Vende_anonimo` (envío anónimo); SE:`Inventario_de_endpoints` (las dos rutas son las únicas nuevas anónimas) | PROBADO |
| 51 | AV:`Con_sesion_ligado_a_la_cuenta` (Pendiente), AQ:`Asesor_rechazado_o_bloqueado_envia_como_visitante` | PROBADO |
| 52 | AV:`Comprador_y_anonimo` (inicio, publicaciones y portada sin datos de avisos) | PROBADO |
| 53 | FA:"rutas #/vende-tu-camion y #/seguro-todo-riesgo", FA:"sin sesión muestra h1…", FA:"CA-P5b-53 un asesor con sesión también ve el formulario" | PROBADO |
| 54 | FA:"la portada tiene los dos bloques…", FA:"el pie tiene la columna \"Servicios\"…" | PROBADO (COMP) + **manual** (vista en el navegador) |
| 55 | FA:"sin sesión muestra h1…", FA:"CA-P5b-07 contador… en aria-live", FA:"validación en el navegador…" (`aria-invalid`, `aria-describedby`), FA:"el tipo de vehículo es un select nativo…" | PROBADO |
| 56 | FA:"validación en el navegador…" (foco al primero), FA:"anónimo… éxito en role=\"status\" con foco" | PROBADO |
| 57 | FA:"el campo trampa está fuera del orden de tabulación…", FA:"sin sesión muestra h1…" ("se abre en una pestaña nueva") | PROBADO (COMP) + **manual** (recorrido con teclado y lector) |
| 58 | FA:"sin sesión muestra h1…" (`inputmode`, `autocomplete`) | PARCIAL: atributos PROBADOS; 360 px sin desplazamiento y contraste AA solo **manual** (jsdom no calcula diseño) |
| 59 | — | **Sin prueba automática (MAN)**: la migración 014 solo puede comprobarse en una base de prueba MariaDB con leads de P5; las pruebas usan SQLite creado por EF |
| 60 | — | **Sin prueba automática (MAN)**: comprobaciones previas del BAJAR comentado de la 014 |
| 61 | Suite P5 completa en verde (comandos 4 y 10); AV:`Atendida_no_aplica_a_contacto`, AQ:`Reasignacion_de_vehiculo_y_bloqueo_no_mueven_avisos`; FA:"un contacto de vehículo sigue ofreciendo \"Reasignar\"…" | PROBADO |
| 62 | AV:`Checks_de_la_base` (`CK_Lead_Origen`, `CK_Lead_AvisoAdmin`, `ATENDIDA` en `CK_Lead_Resultado`, en SQLite) | PROBADO en SQLite; el mismo CHECK en MariaDB queda para CA-P5b-59 (MAN) |
| Prueba manual (§12 de los criterios) | Ambos avisos sin cuenta y con comprador, asesor sin avisos, administradora anota, cierra y reabre | **Manual pendiente**: requiere la migración 014 aplicada por la responsable con respaldo (D-14, D-16) |

Resumen: 60 de 62 criterios con al menos una prueba automática que pasa (CA-P5b-58 solo en parte); 2 solo manuales (59, 60); 3 con parte manual (54, 57, 58). Antes de QA quedaban sin prueba 21 (parcial), 45 y 51 (Rechazado/Bloqueado), y con hueco 36 (D-34 y bloqueo), 44 (`vehiculoId`) y la pantalla del seguro en 13, 14, 18, 19 y 30.

## 4. Defectos y observaciones

Ninguna prueba falla. No se encontraron defectos de comportamiento en el código de producción.

- **O-1 (pruebas, corregido).** `npx tsc -p tsconfig.test.json --noEmit` fallaba en `tests/catalogoDatos.ts:123` porque el ayudante no daba `esPropio`. Era solo de la prueba; corregido (§2). Nota: `npx tsc -b` no revisa `tsconfig.test.json`, por eso el error no salía en el build.
- **O-2 (comportamiento a confirmar, no bloqueante).** Un asesor Rechazado o Bloqueado que envía un aviso con su token queda como anónimo (`usuario_id = NULL`), porque su sesión no es válida. Es coherente con el contrato ("un token inválido o vencido se trata como anónimo") y con CA-P5b-51 ("se acepta como cualquier visitante"). La prueba fija este comportamiento; si se quisiera ligarlo a su cuenta, habría que cambiar contrato y prueba.
- **O-3 (entorno).** Sin E2E: Playwright no está instalado y la API de la rama requiere la migración 014 en `camion_ventas`. Proponer al coordinador incorporar Playwright antes de añadir la dependencia.
- **O-4 (documento).** `criterios-P5b-avisos.md` sigue con "Estado del documento: PROPUESTO" y marca D-50 a D-53 como PENDIENTE, aunque ya están TOMADAS (2026-11-06).
- **O-5 (advertencias heredadas).** NU1902 en `MySqlConnector` 2.3.7 (vulnerabilidad moderada conocida) y xUnit2031 en `InventarioCrearEditarTests.cs:547`; no son de P5b.
