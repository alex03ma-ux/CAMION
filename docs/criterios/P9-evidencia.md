# P9 — Evidencia de pruebas: prepublicación

Ítem: **P9** "Prepublicación" (HU-059, HU-062, HU-081, HU-085, HU-086; RN-SE-001, RN-SE-004 a 006, RN-PA-001, RN-PA-005, RN-PA-006; D-44, D-63, D-83 A, D-84 A, D-85 A).
Rama: `feature/p9-prepublicacion`. Fecha: 2026-11-16. Autor: QA.
Fuentes: `docs/criterios/P9-criterios.md` (CA-P9-01 a 66, trazabilidad §4) y D-83 a D-85 en `docs/coordinacion/DECISIONES.md`.
Código revisado: backend `10b26d5`–`2644bb0`; frontend `5ce1927`–`4cf5480`.

Base de datos: las pruebas de API usan SQLite en memoria (`CamionApiFactory` o una `InventarioApi` propia por prueba); **ninguna prueba toca `camion_ventas`** (D-16). Las IP de las pruebas son de los rangos de documentación (203.0.113.0/24, 2001:db8::/32). Datos ficticios; no se imprimen tokens ni contraseñas. **No se modificó código de producción.**

## 1. Comandos y resultados

Todos ejecutados por QA en esta sesión. La API local estaba en marcha (ver §5), así que el backend se compiló y probó con `--artifacts-path` en la carpeta temporal de la sesión (`art1`, `art2`, `art3`); esta vez la DLL sí se cargó en todas las corridas (no se repitió el bloqueo O-3 de P8).

| # | Comando | Resultado |
|---|---|---|
| 1 | `dotnet test Camion.Api.Tests --artifacts-path <tmp>/art1` (antes de las pruebas de QA) | **PASA**: 1448 superadas, 0 con error, 0 omitidas (2 m 7 s) |
| 2 | `dotnet test ... --filter FullyQualifiedName~P9QaTests` (pruebas de QA solas) | **PASA**: 32 / 0 / 0 (17 s); después, `Put_simultaneos_sin_fila` sola 3 veces: 1 / 0 / 0 las tres |
| 3 | `dotnet test Camion.Api.Tests --artifacts-path <tmp>/art2`, corrida 1 (con `P9QaTests.cs`) | **PASA**: 1481 / 0 / 0 (3 m 48 s; en paralelo con vitest) |
| 4 | ídem, corrida 2 | **PASA**: 1481 / 0 / 0 (3 m 37 s) |
| 5 | `dotnet build Camion.Api.Tests --artifacts-path <tmp>/art3` | **PASA**: 0 errores, 1 advertencia heredada (xUnit2031 en `InventarioCrearEditarTests.cs:547`) |
| 6 | `frontend`: `npx tsc -b` (3 veces) | **PASA** (código 0) |
| 7 | `frontend`: `npx tsc -p tsconfig.test.json` (3 veces, incluye `tests/P9Qa.test.tsx`) | **PASA** (código 0) |
| 8 | `frontend`: `npx vitest run`, corrida 1 (26 pruebas de QA) | **PASA**: 43 archivos, 874 pruebas (76 s) |
| 9 | ídem, corrida 2 | **PASA**: 43 archivos, 874 pruebas (155 s; en paralelo con `dotnet test`) |
| 10 | ídem, corridas 3 y 4 con el archivo final (27 pruebas de QA) | **PASA** las dos: 43 archivos, 875 pruebas |
| 11 | `frontend`: `npm run build` (2 veces) | **PASA** (`tsc -b && vite build`, 3,8 s y 4,1 s; `index-*.js` 656,95 kB, gzip 186,80 kB; aviso de Vite por superar 500 kB, heredado). `dist/` contiene `favicon.svg`, `favicon-32.png` (PNG 32×32 comprobado leyendo la cabecera), `robots.txt`, y `dist/index.html` lleva la meta description y los dos `<link rel="icon">` |
| 12 | Humo contra la API real (http://localhost:5080) | **NO APLICABLE**: la API en marcha es una compilación **anterior a P9** (ver §5) |
| 13 | E2E (Playwright) | **NO EJECUTADA**: Playwright sigue sin instalarse (propuesta pendiente del coordinador) |

Totales: backend **1481** pruebas (1448 previas + 33 de QA), todas pasan, 0 omitidas. Frontend **875** (848 previas + 27 de QA), todas pasan. Ninguna intermitencia en las repeticiones.

Regresión: las corridas 3, 4 y 8 a 10 incluyen todas las pruebas de P1 a P8 e INF-1 a INF-3 (en el backend, entre otras, `LoginTests` y `LoginEstadoTests` del bloqueo 423, `ContactosTests`, `ContactosLimitesTests`, `AvisosTests`, `CreditosTests`, `BusquedasTests`, `ParametrosTests`, `P7PermisosQaTests`, `P8RecuperacionTests`, `InfPublicacionTests` y `SesionTests.Inventario_de_endpoints`; en el frontend `P4p*`, `P5*`, `P6*`, `P7*`, `P8*`, `rutas.test.ts`). **Ninguna falla.**

## 2. Pruebas añadidas por QA

`backend/Camion.Api.Tests/P9QaTests.cs` (33 casos, todos pasan):

| Prueba | Qué comprueba | CA |
|---|---|---|
| `Login_xff_sin_confianza_no_salta_el_cupo` (×2: sin proxy y proxy no confiable) | Cambiar `X-Forwarded-For` no salta el cupo de inicio de sesión si el proxy no está en `KnownProxies` | 43 |
| `Registro_xff_sin_confianza_no_salta_el_cupo` (×2) | Lo mismo para el cupo de registro (comprador y asesor) | 43, 38 |
| `Registro_ipv6_por_64_e_ipv4_mapeada` | Comprador y asesor desde dos IPv6 del mismo /64 comparten cupo; otro /64 no; `::ffff:203.0.113.120` = `203.0.113.120`; sin usuario nuevo tras el 429 | 38, 43 |
| `Login_429_en_el_umbral_no_bloquea` | Con 4 fallos acumulados (uno antes del bloqueo de 5), tres 429 seguidos no cuentan como fallo: `AccessFailedCount` = 4, sin `LockoutEnd`, y desde otra IP la contraseña correcta entra | 41, 45 |
| `Valores_por_defecto` | `OpcionesLimitesAuth` y `appsettings.json`: 5 / 60 min, 20 por día, 20 / 15 min; `PoliticaDatos:Version` = `BORRADOR-2026-11-04` | 38–40, 62 |
| `Registro_sexta_peticion_con_valor_de_produccion` | Con 5 por ventana, la sexta petición (comprador y asesor alternados) da 429 | 38 |
| `Login_peticion_21_con_valor_de_produccion` | Con 20 cada 15 min, la petición 21 da 429 | 40 |
| `Panel_sin_limite_nuevo` | Con registro e inicio de sesión agotados, 25 GET a `/api/admin/parametros/empresa` (comprador → 403) y a `/api/portal/contacto` nunca dan 429; sin token → 401 | 44, 32 |
| `Empresa_sesion_no_valida` | GET y PUT de la empresa con token alterado, con un texto que no es JWT y con el token del administrador tras cerrar sesión → 401; nada se escribe ni se audita | 32 |
| `Portal_contacto_solo_lectura` | POST, PUT y DELETE a `/api/portal/contacto` no escriben la empresa | 33 |
| `Nit_bordes` (16 casos) | Base de 6 y 10 dígitos válida (`123456-3`, `1234567890-2`, con puntos); 5 y 11 dígitos, DV errado, raya `–`, `_`, comas, dígitos de ancho completo y arábigos, guion inicial o final → rechazo | 29 |
| `Digitos_de_bordes` | DV de 123456 = 3 y de 1234567890 = 2 (calculados a mano con los pesos de la DIAN) | 29 |
| `Bordes_validos_por_api` | Razón social de 2 y 150, dirección de 5 y 200 y NIT de 6 y 10 dígitos → 200 y `completo: true` | 28–30 |
| `Nit_muy_largo` | NIT bruto de más de 5000 caracteres → 400 con el mensaje del NIT, sin 500 | 29, 31 |
| `Sin_autorizacion_y_correo_repetido_da_400` | Sin autorización y con un correo ya registrado → 400 (no 409): la validación va antes de comprobar el correo y no se guarda consentimiento | 08, 10 |
| `Put_simultaneos_sin_fila` | 4 PUT simultáneos de la empresa sin fila previa → ningún 500 y una sola fila (en SQLite; ver O-2) | 26 |

`frontend/tests/P9Qa.test.tsx` (27 casos, todos pasan):

| Prueba | Qué comprueba | CA |
|---|---|---|
| Bordes del NIT (17 casos + DV) | Los mismos casos que `Nit_bordes` del servidor, más uno de 5000 espacios: la regla del frontend coincide con la del servidor | 29 |
| Pie con NIT no válido en el borde | `12345678901-1` y `123456-4` que lleguen del servidor no se publican en el pie | 34 |
| "Compra tu seguro todo riesgo" con 503 | Mensaje de fallo con el WhatsApp guardado (300 123 4567) | 57 |
| "Busco un vehículo" con 500 | Mensaje de fallo con el número vigente | 57 |
| "Busco un vehículo" sin conexión y lectura del contacto fallida | Respaldo de D-42 (316 615 2907) | 59 |
| "Saltar al contenido" en "Página no encontrada" | Mueve el foco a `<main>` sin cambiar el hash ni la pantalla; el `noindex` sigue | 49, 50 |
| `#/TERMINOS` | Una ruta `#/` en mayúsculas da "no encontrada", no la portada | 48 |
| `#/terminos` con sesión de administrador | Se ve, sin `noindex` | 17, 55 |
| `#/cuenta/registro` | Pone `noindex` | 55 |
| Foco en `#/privacidad` | Al llegar desde la portada, el foco va al `h1` y cambia el título del documento | 21 |

## 3. Trazabilidad criterio → prueba

Archivos: backend `RegistroConsentimientoTests` (RC), `LimitesAuthTests` (LA), `ParametrosEmpresaTests` (PE), `PoliticaDatosTests` (PD), `P9QaTests` (QA-B); frontend `P9Registro.test.tsx` (FR), `P9Legal.test.tsx` (FL), `P9Empresa.test.tsx` (FE), `P9NoEncontradaSeo.test.tsx` (FN), `P9Contacto.test.tsx` (FC), `P9Qa.test.tsx` (QA-F), `rutas.test.ts`. Se leyó el cuerpo de cada prueba, no solo el nombre.

| CA | Prueba(s) | Estado |
|---|---|---|
| 01 | FR "comprador: casilla sin marcar antes de Crear cuenta…" (sin marcar, `aria-required`, orden, texto, enlace `#/privacidad` con `target=_blank`) | PROBADO |
| 02 | FR "asesor: la misma casilla sin marcar antes de Enviar solicitud" | PROBADO |
| 03 | FR "formulario válido sin la casilla: no llama a la API, mensaje… foco en ella"; "con otros errores, el foco va al primer campo" | PROBADO |
| 04 | FR "al marcarla tras el error el mensaje desaparece; al desmarcarla vuelve" | PROBADO |
| 05 | FR "al cambiar de tipo de cuenta conserva el estado de la casilla y limpia los errores" | PROBADO |
| 06 | RC `Comprador_guarda_consentimiento` (formulario, versión, `entidad`, `entidad_id`, `usuario_id`, hora UTC) | PROBADO |
| 07 | RC `Asesor_guarda_consentimiento` (PENDIENTE, auditoría, fila `INSCRIPCION_ASESOR`) | PROBADO |
| 08 | RC `Sin_autorizacion_da_400` (6 casos: ausente, null, false × 2 rutas; sin usuario ni consentimiento), `Sin_autorizacion_con_otros_errores`; QA-B `Sin_autorizacion_y_correo_repetido_da_400` | PROBADO |
| 09 | RC `Tipo_no_booleano_da_400` (`"true"` y `1` × 2 rutas) | PROBADO |
| 10 | RC `Duplicados_no_guardan_consentimiento`, `Simultaneos_un_solo_consentimiento` (6 en paralelo × 2 rutas); QA-B `Sin_autorizacion_y_correo_repetido_da_400` | PROBADO |
| 11 | RC `Fallo_del_consentimiento_revierte_todo` (disparador SQLite; sin usuario, perfil, rol, auditoría ni historial; 500 genérico sin el detalle) | PROBADO |
| 12 | FR "comprador/asesor: el cuerpo lleva autorizacionDatos: true", "un 400 con errores.autorizacionDatos se muestra bajo la casilla" | PROBADO |
| 13 | RC `Comandos_no_crean_consentimientos` (ejecuta `PromoverAdmin` y `RecuperarAdmin`) | PROBADO |
| 14 | Regresión: `ContactosTests`, `AvisosTests`, `CreditosTests`, `BusquedasTests` (versión y formulario de cada consentimiento) y PD `Seis_rutas_misma_version` | PROBADO |
| 15 | FR "comprador: …" (enlace a `#/terminos` fuera de la etiqueta, `rel`, nombre de la casilla sin "Términos"); FR "el componente común muestra el enlace… en los demás formularios" | PROBADO |
| 16 | FR: una sola casilla en comprador y asesor (D-84 A) | PROBADO |
| 17 | FL "#/terminos anónimo…" (franja, aviso, contacto vigente, "Ir al inicio", sin `/api/auth/me`), "con sesión de asesor"; QA-F con sesión de administrador | PROBADO |
| 18 | FL "Constante única del aviso…" (misma franja en las dos páginas), "#/privacidad franja En revisión legal" | PROBADO |
| 19 | FL `MARCAS_BORRADOR` (NOTA PARA REVISIÓN, [RAZÓN SOCIAL], [NIT], [DOMINIO], BORRADOR) en las dos páginas | PROBADO |
| 20 | FL `TEXTOS_LEGALES_EN_REVISION === true` y ambas páginas con la franja. No se prueba el valor `false` (la constante es de módulo) | PROBADO (solo con `true`) |
| 21 | FL títulos de las dos páginas, foco en el `h1` de `#/terminos`; QA-F foco en el `h1` de `#/privacidad` | PROBADO |
| 22 | FL "Pie: enlaces legales" con y sin sesión, misma pestaña, junto al © | PROBADO |
| 23 | FL "#/terminos con y sin barra final", `HASH_TERMINOS` | PROBADO |
| 24 | FE "sin datos: sección, aviso y estado…" | PROBADO |
| 25 | PE `Sin_datos` (forma exacta del cuerpo, nulls, `completo`/`configurado` false) | PROBADO |
| 26 | PE `Guardar_completo` (auditoría `CAMBIAR` con valores anterior y nuevo, sin cambios no audita), `Vacio_sin_cambios`; QA-B `Put_simultaneos_sin_fila` | PROBADO |
| 27 | PE `Campos_opcionales`; FE "los tres vacíos… viajan como null" | PROBADO |
| 28 | PE `Invalido` (1 y 151 caracteres, HTML, salto de línea); QA-B `Bordes_validos_por_api`; FE validación | PROBADO |
| 29 | PE `Invalido` (6 NIT del criterio), `Nit_valido`, `Nit_invalido`, `Digito_verificacion`; QA-B `Nit_bordes`, `Digitos_de_bordes`, `Nit_muy_largo`; FE "Regla del NIT…"; QA-F bordes (mismos casos que el servidor) | PROBADO |
| 30 | PE `Invalido` (4 y 201 caracteres, HTML); QA-B `Bordes_validos_por_api`; FE validación | PROBADO |
| 31 | PE `Varios_errores_y_formato` (los tres errores; `""`, JSON roto, tipo incorrecto, `[]`) | PROBADO |
| 32 | PE `Permisos` (sin token 401; comprador y asesor 403; asesor PENDIENTE y cuenta bloqueada 401; GET y PUT); QA-B `Empresa_sesion_no_valida` (token alterado, no JWT, tras cerrar sesión), `Panel_sin_limite_nuevo`; FE "un comprador… no llama a la API de la empresa". El 200 del administrador está en `Sin_datos` y `Guardar_completo` | PROBADO |
| 33 | PE `Sin_datos`, `Guardar_completo` (solo los tres campos, sin `actualizadoPor` ni `completo`), `Campos_opcionales`, `Valor_manipulado`; QA-B `Portal_contacto_solo_lectura` | PROBADO |
| 34 | FL "el pie muestra razón social, NIT con puntos y dirección", "con empresa null, sin el campo o si la lectura falla…", "datos incompletos o con NIT no válido…", "como texto, nunca como HTML"; QA-F NIT en el borde (D-83 A) | PROBADO |
| 35 | FL "las páginas legales muestran los datos como Responsable", "sin empresa… no muestran Responsable" | PROBADO |
| 36 | FE "400 del servidor: errores por campo…", "un 401 al guardar lleva a… sesión terminada", "Datos de la empresa guardados." | PROBADO |
| 37 | PE `Independiente_del_contacto`; regresión `ParametrosTests` (CA-P7-36 a 44) | PROBADO |
| 38 | LA `Registro_cupo_compartido`; QA-B `Registro_sexta_peticion_con_valor_de_produccion`, `Registro_ipv6_por_64_e_ipv4_mapeada`, `Valores_por_defecto` | PROBADO |
| 39 | LA `Registro_limite_diario` | PROBADO |
| 40 | LA `Login_cupo_sin_fallo_de_cuenta`; QA-B `Login_peticion_21_con_valor_de_produccion` | PROBADO |
| 41 | LA `Login_cupo_sin_fallo_de_cuenta` (sin token, `AccessFailedCount`, `LockoutEnd`, `UltimoAccesoEn`); QA-B `Login_429_en_el_umbral_no_bloquea` | PROBADO |
| 42 | LA `Es429` en todas las pruebas de cupo (`Retry-After` entero > 0); FR `segundosDeRetryAfter` | PROBADO |
| 43 | LA `Login_ipv6_por_64`, IPv4 distinta en `Registro_cupo_compartido`; QA-B `Login_xff_sin_confianza…`, `Registro_xff_sin_confianza…`, `Registro_ipv6_por_64_e_ipv4_mapeada` | PROBADO |
| 44 | LA `Cupos_independientes`, `Contacto_agotado_no_afecta_auth`; QA-B `Panel_sin_limite_nuevo` | PROBADO |
| 45 | Regresión `LoginTests` (423 y mensaje de bloqueo, mismo 401 para correo inexistente y contraseña incorrecta); QA-B `Login_429_en_el_umbral_no_bloquea` | PROBADO |
| 46 | FR "registro: 429 muestra el mensaje del servidor y la espera…", "registro de asesor: 429 sin cuerpo usa el respaldo", "inicio de sesión: 429…" (con y sin cuerpo), regresión del 401 | PROBADO |
| 47 | LA `Registro_cupo_compartido` (línea `limite_auth` con `cupo=registro`, sin IP ni correo) | PROBADO (solo el cupo de registro; el de inicio de sesión usa la misma línea de código) |
| 48 | FN `leerRuta` (7 hashes), pantalla con textos y enlaces; `rutas.test.ts`; QA-F `#/TERMINOS` | PROBADO |
| 49 | FN `''`, `#`, `#/`, `#contenido`, `#algo/raro` → inicio; barras finales; "#contenido sigue mostrando la portada"; QA-F "Saltar al contenido" en "no encontrada" | PROBADO |
| 50 | FN título, foco en el `h1`, `noindex` y su retirada; la nota del 200 HTTP está en `rutas.ts:34-37` | PROBADO |
| 51 | FN `#/inventario` sin sesión, comprador en `#/cuenta/usuarios`, ficha inexistente | PROBADO |
| 52 | FN lectura de `index.html` (texto exacto, sin tasas ni "peritados"), `dist/index.html` tras el build | PROBADO, con QA-P9-1 (longitud) |
| 53 | FN `<link rel="icon">` SVG y PNG, color `#d71920`; build: los dos archivos en `dist/`, PNG 32×32 | PROBADO |
| 54 | FN `robots.txt` (4 líneas exactas, sin `Sitemap`); build: en `dist/` | PROBADO |
| 55 | FN `rutaSinIndexar` (12 privadas, 9 públicas), `fijarNoIndex`, `#/cuenta` → catálogo; QA-F `#/cuenta/registro`, `#/terminos` con sesión | PROBADO |
| 56 | Regresión de títulos en `Catalogo`, `Detalle`, `P5bAvisos`, `P6Creditos`; nuevos en FL y FN | PROBADO |
| 57 | FC `mensajeErrorEnvio`, crédito 500, contacto de la ficha 502, "Vende tu camión" 500; QA-F seguro 503 y búsqueda 500 | PROBADO |
| 58 | FC "Busco un vehículo sin categorías…", "Mis solicitudes muestra el WhatsApp vigente" | PROBADO |
| 59 | FC crédito sin conexión, "Mis solicitudes" con lectura fallida; QA-F búsqueda sin conexión | PROBADO |
| 60 | FC "el enlace de WhatsApp de la confirmación del crédito usa el número vigente" | PROBADO |
| 61 | FC búsqueda en `src/**/*.{ts,tsx}` (más de 50 archivos; solo los 4 permitidos contienen el número) | PROBADO |
| 62 | PD `Borrador_avisa_en_el_log` (`VersionPorDefecto`); QA-B `Valores_por_defecto` (`appsettings.json`); RC con `BORRADOR-2026-11-04` | PROBADO |
| 63 | PD `Seis_rutas_misma_version` (`PRUEBA-1` en las 7 variantes de formulario de las 6 rutas) | PROBADO |
| 64 | PD `Borrador_avisa_en_el_log` (Testing, nivel Warning), `Version_aprobada_sin_aviso`. No hay prueba de que en Development no se avise | PROBADO (fuera de Development) |
| 65 | PD `Version_invalida_no_arranca` (5 casos; el mensaje no repite el valor), `Formato` (7 casos, borde 40/41) | PROBADO |
| 66 | Revisión manual de `docs/GUIA-PUBLICACION.md` §3.1 y tabla de variables (`PoliticaDatos__Version`, `LimitesAuth__*`) | DOCUMENTADO (MAN), con QA-P9-2 |

Los 66 criterios tienen al menos una prueba automática que los cubre de verdad, salvo CA-P9-66, que por definición es revisión manual (MAN) del documento.

## 4. Defectos y observaciones

| ID | Severidad | Descripción | Evidencia | Propuesta |
|---|---|---|---|---|
| QA-P9-1 | Baja | La meta description aprobada tiene **172 caracteres**; el propio CA-P9-52 pide "máx. 160". Los buscadores suelen recortar hacia 155–160, así que se vería cortada en "…seguro todo riesgo con Financiamos…". La prueba de FN compara el texto exacto pero no la longitud, por eso pasa. No se añadió una prueba que falle porque el texto está aprobado por la responsable: es una contradicción del criterio, no del código | `frontend/index.html`, `dist/index.html`; `len()` = 172 | Decisión de la responsable: mantener el texto (y quitar "máx. 160" del criterio) o aprobar uno más corto, por ejemplo quitando "en Colombia" o "con Financiamos Tu Camión" |
| QA-P9-2 | Baja (documental) | `docs/GUIA-PUBLICACION.md:119` dice que qué hacer con las autorizaciones del borrador está "PENDIENTE de D-85", pero D-85 está TOMADA (A: se conservan con su versión y se pregunta al abogado). La guía (`2644bb0`) es anterior a la decisión (`4e1a097`). También `P9-criterios.md` §5 sigue listando D-83, D-84 y D-85 como PENDIENTE | `DECISIONES.md:1052-1066` | Actualizar la línea 119 de la guía (y §5 de los criterios) con D-85 A |
| O-1 | Media (configuración) | El cupo de inicio de sesión es por IP. Si en producción falta `Proxy:KnownProxies`/`KnownNetworks`, todas las personas llegan con la IP del proxy y comparten **20 inicios de sesión cada 15 min y 5 registros por hora para todo el portal** (QA-B `Login_xff_sin_confianza_no_salta_el_cupo` lo demuestra con el encabezado ignorado). Antes de P9 esa mala configuración solo afectaba a contacto y restablecer; ahora puede impedir entrar al panel | `Program.cs` (partición por `ClaveIp(RemoteIpAddress)`); `GUIA-PUBLICACION.md:50`. Coincide con M-1 de `docs/coordinacion/REVISION-SEGURIDAD-P9.md`; la prueba de QA lo confirma | Que la guía diga explícitamente, junto a `LimitesAuth__*`, que sin el proxy configurado el límite se vuelve global, y añadir a la lista de comprobación una prueba con dos IP distintas |
| O-2 | Baja (no reproducible en SQLite) | `PUT /api/admin/parametros/empresa` lee y luego inserta la fila `portal.empresa` sin bloqueo. En MariaDB, dos guardados simultáneos sin fila previa podrían chocar por la clave primaria (500) o perder uno de los cambios con dos auditorías del mismo "anterior". En SQLite las escrituras se serializan y `Put_simultaneos_sin_fila` pasa 4 de 4 veces, así que no se pudo comprobar. Es el mismo patrón que `PUT /contacto` de P7 y hay un solo administrador | `ParametrosEndpoints.cs` (PUT `/empresa`) | Aceptar el riesgo o, si se quiere cerrar, `SELECT … FOR UPDATE` o reintentar ante `DbUpdateException` |
| O-3 | Informativa | Los roles salen de los *claims* del token (`RequireRole`); `OnTokenValidated` solo comprueba el estado de la cuenta y `session_version`. Con la clave JWT secreta no es explotable; se anota porque la prueba de un "administrador falsificado" se retiró al ver que solo probaría la firma | `Program.cs:100-109` | Ninguna en P9 |

No se encontraron defectos funcionales en el código de P9: todas las pruebas, las previas y las 60 añadidas por QA, pasan.

## 5. Prueba de humo contra la API local

La API de http://localhost:5080 estaba en marcha (`/health` → 200, `{"status":"ok"}`), pero es una compilación **anterior a P9** (el proceso `dotnet` más antiguo arrancó el 13/11/2026, y P9 empieza el 16/11):

- `GET /api/portal/contacto` → 200 **sin** el campo `empresa`.
- `POST /api/auth/registro/comprador` y `/asesor` sin `autorizacionDatos` → 400, pero **sin** `errores.autorizacionDatos`.
- `GET` y `PUT /api/admin/parametros/empresa` sin token → **404** (la ruta no existe en esa compilación).

Para que estas peticiones no pudieran crear cuentas en ninguna versión, se enviaron con correo y contraseña no válidos. No se creó ninguna cuenta ni se escribió en `camion_ventas`, y no se hizo ningún SELECT. Resultado: **NO APLICABLE** hasta reiniciar la API con la rama P9 (no lo hizo QA porque es un proceso de la responsable). Cuando se reinicie, repetir: `GET /api/portal/contacto` (debe traer `"empresa": null`), el mismo POST de registro (debe traer `errores.autorizacionDatos`) y `GET /api/admin/parametros/empresa` sin token (debe dar 401).
