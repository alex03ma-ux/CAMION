# P10 — Evidencia de pruebas: marca y visibilidad en Google

Ítem: **P10** "Marca y visibilidad en Google" (D-87 a D-92; HU-001 a HU-012, HU-063, HU-081, HU-085 como regresión).
Rama: `feature/p10-marca-visibilidad`. Fecha: 2026-11-16. Autor: QA.
Fuentes: `docs/criterios/P10-criterios.md` (CA-P10-01 a 90) y D-87 a D-92 en `docs/coordinacion/DECISIONES.md`.
Código revisado: backend `50685c7`, `315c15c`, `ac2a972`, `a54a0f8`, `0d6ad46`; frontend `87de799`, `050540e`, `b2b4f2c`, `0a09368`.

Base de datos: las pruebas de API usan SQLite en memoria (`CamionApiFactory` / `CatalogoApi`); **ninguna prueba toca `camion_ventas`**. Las del frontend simulan `fetch`. Datos ficticios (slugs `qa-*`, códigos de recuperación `Qa_*`, dominio `ejemplo.test`); no se imprimen tokens ni contraseñas. **No se modificó código de producción.**

## 1. Comandos y resultados

Todo ejecutado por QA en esta sesión. La API local bloquea la DLL, así que el backend se compiló y probó con `--artifacts-path` en la carpeta temporal de la sesión (`<tmp>/art1`…`art3`). Las compilaciones del frontend se hicieron con `--outDir <tmp>/…` (fuera del repositorio): **no se tocó `frontend/dist/`** ni quedó basura en el repositorio (`git status` solo muestra los archivos de QA y los dos directorios sin seguimiento que ya estaban: `docs/marca/` y `frontend/respaldos/`).

| # | Comando | Resultado |
|---|---|---|
| 1 | `dotnet test Camion.Api.Tests --artifacts-path <tmp>/art1` (antes de las pruebas de QA) | **PASA**: 1545 superadas, 0 con error, 0 omitidas (2 m 41 s) |
| 2 | `frontend`: `npx vitest run` (antes de las pruebas de QA) | **PASA**: 46 archivos, 966 pruebas (91 s) |
| 3 | `npm run build -- --outDir <tmp>/dist-apagada --emptyOutDir` **sin** `VITE_PERMITIR_INDEXACION` ni `VITE_URL_PUBLICA` | **PASA** (`tsc -b && vite build`, 1,9 s; `index-DSxRb7yU.js` 667,96 kB, gzip 190,93 kB; aviso de Vite por superar 500 kB, heredado). El bloque `<!--camion:meta:…-->` trae **un** `<meta name="robots" content="noindex">` y no trae `og:url`, `og:image` ni `twitter:image`; el `<script type="module">` y el CSS quedan **fuera** del bloque, después de `<!--camion:meta:fin-->`. Sin `robots.txt` estático. |
| 4 | `VITE_PERMITIR_INDEXACION=true VITE_URL_PUBLICA=https://ejemplo.test npm run build -- --outDir <tmp>/dist-encendida --emptyOutDir` | **PASA** (1,9 s). Sin `robots`; con `og:url = https://ejemplo.test/`, `og:image` y `twitter:image = https://ejemplo.test/marca/og-portal.jpg`, dentro del bloque |
| 5 | `dotnet test … --filter P10Qa` (antes de añadir la prueba de B-5) con `CAMION_QA_INDEX_APAGADA=<tmp>/dist-apagada/index.html` y `CAMION_QA_INDEX_ENCENDIDA=<tmp>/dist-encendida/index.html` (integración con el `index.html` **real**) | **PASA**: 8 / 0 / 0 |
| 6 | ídem **sin** las dos variables | 5 **OMITIDAS** a propósito, con el motivo ("Falta CAMION_QA_INDEX_…"); las otras 3 pasan. Nunca pasan en vacío |
| 7 | `dotnet build Camion.Api.Tests --artifacts-path <tmp>/art3` | **PASA**: 0 errores; 1 advertencia heredada (xUnit2031 en `InventarioCrearEditarTests.cs:547`) |
| 8 | `dotnet test … --artifacts-path <tmp>/art3 --no-build`, 2 corridas intermedias (sin la prueba de B-5) | **PASA** las dos: 1553 / 0 / 0 (3 m 41 s y 2 m 51 s) |
| 9 | `dotnet build` + `dotnet test … --artifacts-path <tmp>/art4 --no-build`, corridas finales 1 y 2 (con B-5 y las variables del #5) | `dotnet build`: **PASA** (0 errores, 1 advertencia heredada). Corrida 1: **PASA** 1554 / 0 / 0 (3 m 18 s). Corrida 2: **PASA** 1554 / 0 / 0 (3 m 40 s). Las dos con las variables del #5, así que las 5 pruebas del `index.html` real se ejecutaron (0 omitidas) |
| 9b | Corrida anterior lanzada en paralelo con vitest y **cortada** por una interrupción de la sesión | **NO VÁLIDA**: el registro muestra unas 30 pruebas `[FAIL]` de P5–P9 en los mismos 4 segundos, justo al matar el proceso; sin resumen final. Se descartó y se repitió en primer plano (corridas 1 y 2 del #9, ambas sin fallos) |
| 10 | `frontend`: `npx tsc -b` (corridas 1 y 2) | **PASA** (código 0) las dos |
| 11 | `frontend`: `npx tsc -p tsconfig.test.json` (corridas 1 y 2, incluye `tests/P10Qa.test.tsx`) | **PASA** (código 0) las dos |
| 12 | `frontend`: `npx vitest run`, corrida 1 | **PASA**: 47 archivos, 1016 pasan + 2 "fallo esperado" (versión intermedia del archivo de QA, 130 s) |
| 13 | ídem, corrida 2 (misma versión) | **PASA**: 1016 + 2 "fallo esperado" (92 s) |
| 13b | `npx tsc -b`, `npx tsc -p tsconfig.test.json`, `npx vitest run` y `npm run build -- --outDir <tmp>/dist-final1|2`, corridas finales 1 y 2 (archivo de QA definitivo) | Corrida 1: `tsc -b` 0, `tsc -p tsconfig.test.json` 0, vitest **PASA** 47 archivos, 1017 pasan + 1 "fallo esperado" (QA-P10-1), 1018 (127 s), `npm run build` **PASA** (3,2 s). Corrida 2: `tsc -b` 0, `tsc -p tsconfig.test.json` 0, vitest **PASA** 1017 + 1 "fallo esperado" (130 s), `npm run build` **PASA** (5,7 s) |
| 14 | `frontend`: `npm run build -- --outDir <tmp>/dist-run1` y `dist-run2` (sin variables) | **PASA** las dos (4,2 s; mismo `index-DSxRb7yU.js` que el #3) |
| 15 | E2E (Playwright) | **NO EJECUTADA**: Playwright sigue sin instalarse (propuesta pendiente del coordinador). Los flujos de navegador (Atrás/Adelante, Ctrl+clic, enlaces viejos) se cubrieron con jsdom (§2) |
| 16 | Prueba contra la API local (http://localhost:5080) | **NO EJECUTADA**: no se reinició la API de la responsable; la integración real del #5 usa `WebApplicationFactory` con el `index.html` compilado |

Totales: backend **1554** (1545 previas + 9 de QA). Frontend **1018** (966 previas + 52 de QA: 51 pasan y 1 es `it.fails` que documenta QA-P10-1).

Sobre la prueba `it.fails` (QA-P10-1 = B-1 de la revisión de seguridad): describe el comportamiento **correcto** y hoy falla por el defecto (sin `.fails`: `expected DOMException{ SecurityError… } to be null`). Vitest la cuenta como "expected fail"; cuando se corrija B-1 empezará a fallar y habrá que quitarle `.fails`. No está omitida. Una segunda `it.fails` de una versión intermedia (slugs `.` y `..`) se retiró al comprobar que el navegador normaliza `%2E` antes de que la app lea la ruta (O-7); se sustituyó por una prueba que pasa y fija esa normalización.

Regresión: las corridas 8, 9, 12 y 13 incluyen todas las pruebas de P1 a P9 e INF-1 a INF-3 (backend: `P8RecuperacionTests`, `P8RecuperarAdminTests`, `SesionTests`, `CatalogoDetalleTests`, `MediaTests`…; frontend: los 33 archivos migrados de `#/` a rutas, `P8Reportes`, `P8Qa`, `P9*`, `rutas.test.ts`). **Ninguna falla.**

## 2. Pruebas añadidas por QA

### 2.1 `backend/Camion.Api.Tests/P10QaTests.cs` (9 pruebas)

| Prueba | Qué comprueba | CA |
|---|---|---|
| `Compilado_apagado_ficha_visible` | Con el `dist/index.html` real compilado **sin** indexación y la API con la indexación apagada: `/vehiculo/chevrolet-npr-2020` → 200 con **un** `<title>`, **una** description, **un** robots `noindex` (el del compilado se reemplaza, no se suma), sin canonical, `X-Robots-Tag: noindex`, cada `og:*`/`twitter:*` una vez, `og:image` = foto de portada absoluta, los dos JSON-LD válidos; fuera del bloque el archivo es idéntico byte a byte (script y CSS con huella); ningún `<script>` en línea que la CSP del frontend bloquearía | 53, 28, 71, 73, 75, 76 |
| `Compilado_encendido_ficha_visible` | Con el compilado **con** `VITE_URL_PUBLICA=https://ejemplo.test` y la API encendida (`Portal:UrlPublica` igual): 0 robots, canonical `https://ejemplo.test/vehiculo/…`, `og:url`, `og:image` y `twitter:image` del vehículo **reemplazan** a los genéricos del compilado (una sola vez cada uno, sin `og-portal.jpg`); sin fotos, `og:image` = `https://ejemplo.test/marca/og-portal.jpg` una vez | 27, 28, 71, 73, 82 |
| `Compilado_xss_en_titulo_y_descripcion` | En los dos compilados, título `"><img src=x onerror=alert(1)></script><script>alert(1)</script>` y descripción con `</script>`: no aparece `<img src=x` ni `<script>alert`; exactamente 3 `<script` (2 JSON-LD + módulo de Vite); `og:title`, `twitter:title`, `og:image:alt` decodifican al texto original; los JSON-LD no tienen `<` y se leen con el texto original (también en las migas) | 68, 74 |
| `Compilado_no_disponible_y_slugs_raros` | En los dos compilados, 10 rutas (vendida, inexistente, `..%2F..%2Fapi%2Fcatalogo%2Ffiltros`, `"><img…` y `<script>` codificados, 201 y 5000 caracteres, `%E0%A4%A`, `a/b`, mayúsculas): 404 `text/html`, **mismo cuerpo** para todas, un robots `noindex`, un title, `X-Robots-Tag`, sin JSON-LD, el slug **no se refleja**; el resto del archivo intacto; con URL pública conserva `og:image` genérica una vez | 72, 74, 28 |
| `Compilado_head` | `HEAD` de ficha visible (200) y no disponible (404) con `text/html` y `X-Robots-Tag` (el cuerpo de HEAD no se puede comprobar con TestServer: O-4) | 70, 59 |
| `Prefijos_no_caen_en_el_spa` | Con la plantilla configurada: `/api/no-existe`, `/api/catalogo/publicaciones/no-existe`, `/api/vehiculo/…`, `/media/…`, `/assets/index-x.js`, `/assets/vehiculo/x.css`, `/marca/…`, `/favicon.ico`, `/site.webmanifest`, `/vehiculos`, `/camiones`, `/restablecer-contrasena`, `/cuenta/usuarios`, `/index.html`, `/vehiculos/<slug>` → 404, nunca HTML ni el bloque `camion:meta`; `/api/…` sigue con JSON y la CSP de la API; `/health` 200 sin HTML | 46, 48, 70, 87 |
| `Barra_final` | `/vehiculo/<slug>/` → 404 `noindex` (O-1); `/Vehiculo/<slug>` → 200 con las etiquetas (O-2); `/vehiculo` sin slug → no 200 | 34, 72 |
| `Separadores_y_comilla_escapados` (B-5) | Título y descripción del asesor con U+2028, U+2029 y `'`: en los dos JSON-LD no hay ninguno crudo y aparecen `\u2028`, `\u2029` y `\u0027`; el JSON se lee con el texto original; en todo el HTML (title, `og:*`) no quedan U+2028/U+2029 crudos ni `'comillas'` sin codificar. Fija `JavaScriptEncoder` de `FichaSeo.cs` frente a un cambio futuro (p. ej. `UnsafeRelaxedJsonEscaping`) | 68, 74 |
| `Sitemap_y_ficha_con_mil` | 1.000 publicaciones visibles: el sitemap tiene 1.000 fichas; mejor de 3: **sitemap 17 ms, ficha 7 ms** (< 500 ms) | 89, 55 |

### 2.2 `frontend/tests/P10Qa.test.tsx` (52 pruebas)

| Grupo | Qué comprueba | CA |
|---|---|---|
| XSS (1) | Ficha con título y descripción maliciosos: ningún `<img>`/`<script>` creado, `og:title`/`twitter:title` con el texto literal, JSON-LD sin `<` ni `>` (`</script>`) que se lee con el texto original (también migas), `alert` nunca llamado, una etiqueta de cada tipo | 68, 74, 28 |
| Slugs raros (6) | `..%2F..%2Fapi%2Fadmin%2Fusuarios`, comillas y `<c>`, 500 caracteres, `ñ&`: una sola petición a `/api/catalogo/publicaciones/<codificado>`, ninguna a `/api/admin`, página "no disponible" con `noindex` y sin canonical; `a/b`, `../`, `%E0%A4%A`, `%`, `%20`, `//` → "no encontrada"; `/vehiculo/%2E%2E` y `/vehiculo/%2E` se normalizan a `/` y `/vehiculo/` sin pedir ninguna ficha (O-7) | 34, 72, 24 |
| Prefijos reservados (2) | 11 rutas reservadas dan "no encontrada" si llegaran al SPA; los enlaces a `/api`, `/media`, `/health/ready`, `/assets`, `/sitemap.xml`, `/robots.txt`, `/favicon.ico`, `/marca`, `/site.webmanifest` y `tel:` no se interceptan | 36, 46, 48 |
| Ctrl+clic (1) | Ctrl, Alt, clic central y Ctrl+Mayús: ni pantalla ni historial cambian; `download` no se intercepta; `target="_self"` sí navega sin recargar | 36 |
| Atrás / Adelante (1) | Con `history.back()` y `history.forward()` reales (popstate de jsdom): portada → `/vehiculos?marcaId=10` → ficha → Atrás (filtro "Marca" = 10, `noindex, follow`, sin JSON-LD de vehículo) → Atrás (portada) → Adelante → Adelante (ficha) | 37, 26, 28 |
| Una etiqueta de cada tipo (2) | Recorrido de 15 pantallas (portada, catálogo, dos fichas, filtros, categoría, crédito, avisos, privacidad, no encontrada, vendido, ficha otra vez, portada) empezando con el `<head>` compilado (encendido con URL pública, y apagado con su robots): en cada paso 1 description, ≤ 1 title/robots/canonical, cada `og:*`/`twitter:*` y cada JSON-LD una vez y todos con `data-camion` | 28, 80, 32, 27 |
| Indexación apagada (10) | Con `VITE_PERMITIR_INDEXACION` vacía: `/`, `/vehiculos`, `/camiones`, ficha, `/credito`, `/vende-tu-camion`, `/seguro-todo-riesgo`, `/busco-un-vehiculo`, `?pagina=2` → un robots `noindex`, sin canonical ni `og:url`; `TRUE`, ` true`, `1`, `si` no encienden la indexación (tampoco en el HTML compilado) | 53, D-89 A |
| Enlaces viejos (10) | `#/cuenta/usuarios?rol=ASESOR`, `#/buscar?marcaId=3` (y con `pagina=1`), `#/`, `#/vende-tu-camion`, `#/credito/<slug>`, `#/inventario/7`; `#contenido`, `#codigo=`, `#`, vacío, `#%2F`, `#?x=1` no se tocan; **`it.fails` QA-P10-1** | 41 |
| Recuperación (7) | 4 variantes del enlace viejo (normal, barra final, `otra=1&codigo=`, codificado): la dirección final es `/restablecer-contrasena`, el código no pasa por **ninguna** llamada a `pushState`/`replaceState`, ni por el título, el `<head>`, `localStorage`, `sessionStorage` o la consola; se envía solo en el cuerpo del POST y se olvida al usarlo; pegado con la app abierta (`hashchange`) igual; `?codigo=` en la consulta **no** se acepta; el enlace nuevo con barra final funciona y se limpia | 42, 43, 44 |
| Archivos de marca (12) | Pesos de la tabla de §2.1 (KB = 1024), tamaños 180, 192, 512, 32, alto 112 del horizontal, ancho 320 del cuadrado, `og-portal.jpg` 1200×630, `favicon.ico` con 16, 32 y 48; WebP de la portada ≤ 40 KB (O-3) | 01, 08, 10, 11 |
| Cuenta y foco (2) | `/cuenta`: antetítulo "VCA AUTOMOTORES", h1 "Venta de Camiones", párrafo igual; al navegar sin recargar el foco va al h1 | 17, 38 |

Fondo opaco de `apple-touch-icon` e `icon-192/512` (CA-P10-08): se comprobó leyendo los píxeles con Pillow sobre `<tmp>/dist-apagada/marca` (esquina `(255, 255, 255, 255)`); no hay prueba automática porque los PNG son de paleta y el frontend no tiene decodificador.

## 3. Trazabilidad CA-P10 → prueba

Leyenda: B = backend (`P10SeoTests.cs`, salvo que diga `QA` = `P10QaTests.cs`, o P8 = `P8RecuperacionTests`/`P8RecuperarAdminTests`); F = frontend (`P10Marca`, `P10Rutas`, `P10Seo`, `rutas.test`, `Catalogo`, `P5Contacto`; `QA` = `P10Qa.test.tsx`); MAN = revisión manual pendiente.

| CA | Prueba | Estado |
|---|---|---|
| 01 | F `P10Marca` "existen todos los derivados…" (lista exacta de `public/`), F QA pesos; compilado sin originales (`find` en `<tmp>/dist-apagada`) | PROBADO |
| 02 | F `P10Marca` "el encabezado muestra el logo horizontal con WebP y PNG…" | PROBADO (COMP); 56 px en pantalla: MAN |
| 03 | F `P10Marca` y `Catalogo` (enlace con el alt y `href="/"`) | PROBADO |
| 04 | — | MAN (320–1280 px) |
| 05 | F `P10Marca` "el pie oscuro usa el logo cuadrado…" | PROBADO |
| 06 | F `P10Marca` "no queda el logo de texto anterior…" | PROBADO |
| 07 | F `P10Marca` "index.html usa rutas absolutas…", compilado #3; F QA `favicon.ico` 16/32/48 | PROBADO; recorte del toro: MAN |
| 08 | F QA tamaños; píxeles con Pillow (§2.2) | PROBADO (lectura de archivos) |
| 09 | F `P10Marca` "site.webmanifest…" | PROBADO |
| 10 | F QA `og-portal.jpg` 1200×630 y ≤ 200 KB | PROBADO |
| 11 | F QA pesos y total WebP | PROBADO, con O-3; pestaña Red: MAN |
| 12 | F `P10Marca` rutas absolutas; compilado #3 copia `marca/`, `favicon.ico`, `site.webmanifest` | PROBADO |
| 13 | F `P10Marca` "define los nombres de D-87…" | PROBADO |
| 14 | F `P10Seo` "título, descripción y etiquetas genéricas…"; compilado #3 y #4 | PROBADO |
| 15 | F `P10Seo` tabla de páginas | PROBADO |
| 16 | F `P10Marca` "© VCA Automotores" | PROBADO |
| 17 | F QA "/cuenta muestra VCA AUTOMOTORES…" | PROBADO (antes sin prueba de DOM) |
| 18 | F `P10Marca` búsqueda en `src`; F `P5Contacto` mensaje de la ficha | PROBADO |
| 19 | F `P10Marca` "ningún archivo de src escribe Financiamos Tu Camión…" | PROBADO |
| 20 | F `P10Marca` "h1 Venta de camiones usados…" (D-92) | PROBADO |
| 21 | F `P10Seo` tabla (11 rutas) | PROBADO |
| 22 | F `P10Seo` textos; B `Titulos`, `Ficha_visible`, `Ficha_sin_linea_y_sin_fotos` | PROBADO |
| 23 | F `P10Seo` descripción; B `Descripciones`, `Ficha_reservada`, `Ficha_carroceria` | PROBADO |
| 24 | F `P10Seo` "vehículo no disponible…"; F QA slugs raros | PROBADO |
| 25 | F `P10Rutas` "una categoría sin vehículos visibles…" | PROBADO |
| 26 | F `P10Seo` tabla (`?marcaId=10`, `?pagina=2`); F QA Atrás (`noindex, follow`) | PROBADO |
| 27 | F `P10Seo`; F QA recorrido; B QA `Compilado_encendido_ficha_visible` | PROBADO |
| 28 | F `P10Seo` "reutiliza las etiquetas que inyecta la API"; F QA recorrido de 15 pantallas (dos variantes); B QA (una de cada en el HTML real) | PROBADO |
| 29 | F `P10Seo` tabla (`/privacidad` `noindex`) | PROBADO |
| 30 | F `rutas.test` "privadas, restablecer, no encontrada y /credito/<slug>…" | PROBADO |
| 31 | F `P10Seo` "si pasa de 65…"; B `Titulos`, `Ficha_xss` | PROBADO |
| 32 | F `P10Seo` "al salir… vuelve la descripción por defecto"; F QA recorrido | PROBADO |
| 33 | F `rutas.test` tabla de §4 | PROBADO |
| 34 | F `rutas.test`; F QA slugs raros y rutas rotas | PROBADO, con O-1, O-2 y O-7 |
| 35 | F `P10Rutas` (sin `#/` en el DOM y en `src`) | PROBADO |
| 36 | F `P10Rutas`; F QA Ctrl/Alt/clic central/download/_self y prefijos | PROBADO |
| 37 | F `P10Rutas` (popstate simulado); F QA `history.back()`/`forward()` reales | PROBADO (jsdom); en navegador real: E2E no ejecutada |
| 38 | F QA foco al h1 | PROBADO |
| 39 | F `P10Rutas` `/vehiculos?tipoId=2` → `/camiones`; F `Catalogo` encabezado y pie | PROBADO |
| 40 | F `P10Rutas` categoría vacía | PROBADO |
| 41 | F `rutas.test`, `P10Rutas`; F QA 7 conversiones y 6 hashes intactos | PROBADO, con **QA-P10-1** |
| 42 | F `P10Rutas` "enlace viejo… nunca pasa a la ruta"; F QA 4 variantes + `hashchange` | PROBADO |
| 43 | B P8 (formato exacto y sin `?codigo=`); F `rutas.test`, `P10Rutas`, QA | PROBADO |
| 44 | F QA (código fuera de URL, título, almacenamiento y consola; `?codigo=` rechazado); registro del proxy: MAN | PROBADO (COMP) / MAN |
| 45 | F `P10Rutas` "Saltar al contenido" | PROBADO |
| 46 | F `rutas.test` prefijos; F QA; B QA `Prefijos_no_caen_en_el_spa` | PROBADO |
| 47 | Lectura de `vite.config.ts` (proxy de `/robots.txt` y `/sitemap.xml`, sin `/vehiculo/`) | MAN (recargar con `npm run dev`) |
| 48 | B QA (la API nunca entrega el `index.html` fuera de `/vehiculo/`); reglas del proxy en la guía | PROBADO (API) / MAN (proxy real) |
| 49 | Guía §6 (`no-cache` e `immutable`) | MAN |
| 50 | Suite completa con `irA` (corridas 12 y 13) | PROBADO |
| 51 | B `Robots_encendido` | PROBADO |
| 52 | B `Robots_apagado` | PROBADO |
| 53 | B `Sitemap_apagado`, `Ficha_indexacion_apagada`, `Robots_sin_duplicar`; B QA `Compilado_apagado_ficha_visible`; F `P10Seo` D-89 y F QA 10 casos | PROBADO |
| 54 | Guía §2 y §3 ("dos claves que van juntas") revisada | DOCUMENTADO (MAN) |
| 55 | B `Sitemap_contenido`; B QA 1.000 fichas | PROBADO |
| 56 | B `Sitemap_contenido` (exclusiones) | PROBADO |
| 57 | B `Sitemap_contenido`, `Sitemap_lastmod` | PROBADO, salvo el corte en 50.000 (O-5) |
| 58 | B `Url_publica`, `Sitemap_url_publica` | PROBADO |
| 59 | B `Metodos`, `No_escriben`, `Encabezados` | PROBADO |
| 60 | B `Sitemap_vendido` | PROBADO |
| 61 | F `P10Seo` Organization en la portada y no en otras | PROBADO |
| 62 | F `P10Seo` "Organization sin dirección ni AutoDealer" | PROBADO |
| 63 | F `P10Seo` (razón social y NIT solo con empresa completa) | PROBADO |
| 64 | F `P10Seo`; B `Ficha_visible`, `Ficha_sin_linea_y_sin_fotos`, `Ficha_carroceria` | PROBADO |
| 65 | B `Ficha_visible`, `Ficha_reservada` | PROBADO |
| 66 | F `P10Seo`; B `Ficha_visible`, `Ficha_sin_datos_personales` | PROBADO |
| 67 | F `P10Seo` migas; B `Ficha_visible`, `Categorias_migas` | PROBADO |
| 68 | F `P10Seo` `jsonLdSeguro`; F QA XSS; B `Ficha_xss`; B QA `Compilado_xss…` y `Separadores_y_comilla_escapados` (B-5) | PROBADO |
| 69 | — | MAN (después de D-81) |
| 70 | B `Sin_plantilla_no_hay_ruta`, `Ruta_index`, `Metodos`; B QA `Compilado_head` | PROBADO |
| 71 | B `Ficha_visible`; B QA compilados | PROBADO |
| 72 | B `Ficha_no_disponible`, `Robots_sin_duplicar`; B QA slugs raros, `Barra_final` | PROBADO, con O-1 y O-2 |
| 73 | B `Ficha_visible`, `Ficha_sin_linea_y_sin_fotos`; B QA compilados | PROBADO |
| 74 | B `Ficha_xss`; B QA `Compilado_xss…` y slugs con `<img`/`<script>` | PROBADO |
| 75 | B `Ficha_visible`, `Sin_marcas`; B QA (archivo real idéntico fuera del bloque) | PROBADO |
| 76 | B `Encabezados`; B QA (sin scripts en línea en el HTML real) | PROBADO |
| 77 | B `Recarga_y_503`, `Sin_archivo_no_arranca` | PROBADO |
| 78 | Guía §6 | MAN |
| 79 | B `Ficha_no_disponible` (mismas reglas que el detalle), `No_escriben` | PROBADO |
| 80 | F `P10Seo` "reutiliza las etiquetas…"; F QA recorrido con el bloque compilado | PROBADO |
| 81 | — | MAN (después de D-81) |
| 82 | F `P10Seo` `etiquetasHtml`; compilados #3 y #4 | PROBADO |
| 83 | F `P5Contacto` (mensaje con `<origen>/vehiculo/<slug>`) | PROBADO |
| 84 | B `Sitemap_sin_datos_personales`, `Ficha_sin_datos_personales` | PROBADO |
| 85 | B `Igual_con_cualquier_token`, `Ficha_igual_con_cualquier_token`; rutas privadas: suites P4p/P7 | PROBADO |
| 86 | B `Metodos` | PROBADO |
| 87 | Suite completa (corridas 8 y 9); B QA `Prefijos_no_caen_en_el_spa` | PROBADO |
| 88 | Suite completa (corridas 12 y 13) | PROBADO |
| 89 | B QA `Sitemap_y_ficha_con_mil` (17 ms / 7 ms en SQLite en memoria) | PROBADO (orientativo; MariaDB no medido) |
| 90 | `grep` en `frontend/src` e `index.html`: sin `gtag`, `googletagmanager`, `facebook.net`, `iframe` ni `document.cookie`; guía §2 (Search Console por DNS) | PROBADO (lectura) / MAN |

Resumen: 78 de 90 criterios tienen prueba automática que los verifica; 12 son total o parcialmente manuales (04, 44 en el registro del proxy, 47, 48 en el proxy real, 49, 54, 69, 78, 81, 90, y el aspecto visual de 02 y 07). Ninguno queda sin plan.

## 4. Defectos y observaciones

| ID | Severidad | Descripción | Evidencia | Propuesta |
|---|---|---|---|---|
| QA-P10-1 (= B-1 de seguridad) | Baja | Un enlace viejo manipulado `https://<portal>/#//otro-dominio/x` deja la página **en blanco**. `convertirHashViejo('#//otro-dominio/x')` devuelve `//otro-dominio/x` (dirección de otro origen); `history.replaceState` lanza `SecurityError` dentro del `useState` de `useUbicacion` y React no pinta nada. No hay redirección a otro dominio (el navegador la bloquea) ni fuga de datos, pero cualquiera puede difundir un enlace del portal que se ve roto | `frontend/src/rutas.ts` (`convertirHashViejo`, `convertirEnlaceViejo`); prueba `it.fails` "QA-P10-1" en `P10Qa.test.tsx` (sin `.fails`: `expected DOMException{ SecurityError… } to be null`) | Que `convertirHashViejo` trate como desconocido todo camino que empiece por `//` o contenga `\` (por ejemplo devolver `/no-encontrada`), o envolver el `replaceState` en `try/catch` y caer en "no encontrada" |
| O-1 | Baja | `/vehiculo/<slug>/` (barra final): la API responde 404 con `noindex` (la ruta comodín recibe `<slug>/`), pero el SPA (`leerRuta` acepta barra final, CA-P10-34) muestra la ficha. Un visitante la ve; un buscador ve 404. Como el canonical y el sitemap nunca llevan barra final, el efecto es menor | B QA `Barra_final`; `SeoEndpoints.cs` (`/vehiculo/{**slug}`) | Que la API quite una barra final del slug antes de buscar, o que el proxy redirija `/vehiculo/x/` → `/vehiculo/x` (301) |
| O-2 | Baja | El enrutado de ASP.NET Core no distingue mayúsculas: `/Vehiculo/<slug>` llega a la API y da 200 con las etiquetas del vehículo, mientras el SPA lo trata como "no encontrada" (CA-P10-34). Con la regla nginx de la guía (`location ^~ /vehiculo/`, sensible a mayúsculas) no llega a la API; con IIS URL Rewrite (insensible por defecto) sí | B QA `Barra_final` (fija el comportamiento actual) | Aceptarlo (el canonical apunta a la dirección correcta) o hacer la regla de IIS sensible a mayúsculas en la guía |
| O-3 | Informativa | CA-P10-11: los dos WebP de la portada suman **40 902 bytes**; caben en 40 KiB (40 960) por 58 bytes, pero no en 40 KB decimales (40 000). Si se cuentan también los íconos (favicon), se pasa | F QA "logos WebP de la portada ≤ 40 KB" | Aclarar en el criterio que KB = 1024 o bajar un poco la calidad del WebP cuadrado (se carga con `loading="lazy"` en el pie) |
| O-4 | Informativa | El cuerpo de `HEAD /vehiculo/…` no se puede comprobar en las pruebas: TestServer entrega el cuerpo; Kestrel lo descarta por su cuenta. Tampoco se puede probar `%00` en la ruta (TestServer lanza antes de llegar a la API) | B QA `Compilado_head`, comentario en `SlugsRaros` | Comprobar `curl -I` contra la API real en la prueba manual |
| O-5 | Informativa | CA-P10-57: el corte del sitemap en 50.000 direcciones con aviso en el log no tiene prueba automática (sembrar 50.000 filas no es razonable en la suite). Revisado en el código (`SeoEndpoints.Sitemap`: `Take(cupo + 1)` y `LogWarning`) | `SeoEndpoints.cs` | Aceptar la revisión de código, o hacer `MaxDirecciones` configurable para probarlo con un número pequeño |
| O-7 | Informativa (descartado como defecto) | `leerRuta('/vehiculo/%2E%2E')` a secas daría el slug `..` y `encodeURIComponent` no codifica los puntos, pero en un navegador `location.pathname` nunca lo trae así: WHATWG URL trata `%2E` como punto y normaliza `/vehiculo/%2E%2E` → `/` y `/vehiculo/%2E` → `/vehiculo/` (comprobado con `new URL` de Node y en jsdom). No es alcanzable | F QA "slugs %2E y %2E%2E…" | Ninguna |
| O-6 | Informativa | E2E: Playwright no está instalado. Atrás/Adelante, Ctrl+clic y enlaces viejos se probaron en jsdom (con `history.back()`/`forward()` reales de jsdom), no en un navegador | — | Mantener la propuesta de incorporar Playwright para el recorrido de P10 (recargar `/vehiculo/<slug>` y `/cuenta/usuarios`, Atrás/Adelante, Ctrl+clic) |

**Revisión de seguridad** (`docs/coordinacion/REVISION-SEGURIDAD-P10.md`, `c96ea07`): B-5 se atendió con `Separadores_y_comilla_escapados` (pasa: el código ya escapaba). **B-1 a B-4, B-6 y M-1 se corrigen después de esta entrega**, así que QA no probó su corrección; B-1 es el mismo defecto que QA-P10-1 y su `it.fails` servirá de prueba de regresión cuando se corrija. M-1 (sitemap sin caché del servidor) solo aplica con la indexación encendida; la medición de CA-P10-89 (17 ms con 1.000 fichas en SQLite) no lo cubre.

No se encontraron defectos en el backend: con el `index.html` **real** compilado de las dos formas, la ficha tiene un solo robots, un solo title, `og:image` correcto y JSON-LD válido, también con texto malicioso del asesor, y el resto del archivo queda igual byte a byte.

## 5. Pendiente para la prueba manual (responsable o QA con el portal en marcha)

1. CA-P10-04 y 02: encabezado a 320, 375, 768 y 1280 px.
2. CA-P10-47: con `npm run dev`, recargar `http://localhost:5173/vehiculo/<slug>`, `/camiones` y `/cuenta/usuarios`; `/robots.txt` y `/sitemap.xml` deben venir de la API.
3. CA-P10-48, 49, 78: en el servidor con el proxy de la guía, recargar cualquier ruta (200), `/api/no-existe` (404 JSON), `/assets/no-existe.js` (404, no HTML), `/health`, encabezados `Cache-Control`; `view-source:` de una ficha con sus `og:*`; `curl -I` de una ficha (O-4).
4. CA-P10-44: un enlace de recuperación nuevo; el registro del proxy solo muestra `/restablecer-contrasena`.
5. CA-P10-69 y 81 (después de D-81): validadores de Google y schema.org; vista previa en WhatsApp y en el depurador de Facebook.

## 6. Correcciones posteriores (2026-11-17)

Hechos verificados por el coordinador **después** de esta evidencia de QA (no son corridas de QA). Commits de la rama: `4292ce8` (B-6), `76a7349` (B-1 = QA-P10-1, O-1 en el SPA), `0748c93` (M-1, B-2, O-1 en la API, O-2) y `3da7e38` (guía de publicación: B-2, B-3, B-4, M-1, O-1, O-2).

| Hallazgo | Qué se corrigió | Estado |
|---|---|---|
| M-1 (revisión de seguridad) | El sitemap generado se guarda en memoria unos minutos (`LimitesSeo:SitemapCacheMinutos`, 10 por defecto) y `/sitemap.xml` y `/vehiculo/*` tienen un cupo de peticiones por IP (`LimitesSeo:LimiteVentana` en `LimitesSeo:VentanaMinutos`; 120 por minuto por defecto) | IMPLEMENTADO y PROBADO |
| B-1 = QA-P10-1 | Un enlace viejo manipulado `/#//otro-dominio` lleva a la portada en lugar de dejar la página en blanco. La prueba `it.fails` de QA-P10-1 ya no está marcada como fallo esperado | IMPLEMENTADO y PROBADO |
| B-2 | `Portal:RutaIndexHtml` solo acepta un archivo `.html` de tamaño razonable, y la plantilla debe traer las marcas `camion:meta` | IMPLEMENTADO y PROBADO |
| B-3 | Reglas del proxy de la guía: `location = /api` y `location = /media` no caen en `index.html`; `/marca/` responde 404 si el archivo no existe | DOCUMENTADO (guía; el proxy real sigue sin probar, D-81) |
| B-4 | HSTS repetido en las `location` del frontend del ejemplo nginx | DOCUMENTADO (guía; el proxy real sigue sin probar, D-81) |
| B-6 | `etiquetasHtml` reemplaza con una función, así que `$&`, `$1` y `` $` `` de la URL pública no se interpretan | IMPLEMENTADO y PROBADO |
| O-1 | `/vehiculo/<slug>/` responde 301 a `/vehiculo/<slug>` en la API, y el SPA quita la barra final | IMPLEMENTADO y PROBADO |
| O-2 | La API distingue mayúsculas en `/vehiculo/`, como el SPA (`/Vehiculo/x` ya no da 200) | IMPLEMENTADO y PROBADO |

Siguen abiertos: O-3 (40 KB del WebP de la portada), O-4 (cuerpo de HEAD, solo con `curl -I` contra la API real), O-5 (corte en 50.000 sin prueba automática) y O-6 (E2E sin Playwright). O-7 fue descartado como defecto.

**Corridas después de las correcciones** (las ejecutó el coordinador):

- Frontend: **1025/1025** pruebas pasan, **ninguna** marcada como fallo esperado.
- Backend, corrida completa: **1557 pasan y 5 omitidas**. Las 5 omitidas son `P10QaIndexCompiladoTests`, que necesitan las variables `CAMION_QA_INDEX_APAGADA` y `CAMION_QA_INDEX_ENCENDIDA` (§1, #6).
- Backend, corridas aparte con el `index.html` compilado real (indexación apagada y encendida): las **52** pruebas P10 pasan y **0** se omiten.

No cambian: E2E y la prueba contra la API local siguen NO EJECUTADAS, y la prueba manual (§5) sigue pendiente. La guía para la responsable está en `docs/manual/P10-marca-visibilidad.md`.
