# Criterios de aceptación P10: marca y visibilidad en Google

Ítem del backlog: **P10 "Marca y visibilidad en Google"** (`docs/coordinacion/BACKLOG.md`, fila P10; D-87 TOMADA). Alcance: logo VCA Automotores y nombre visible, títulos y descripciones por página, direcciones sin `#`, `sitemap.xml`, datos estructurados y vista previa al compartir.
Rama: `feature/p10-marca-visibilidad`. Autor: analista funcional. Fecha: 2026-11-16.

Fuentes leídas:
- `docs/coordinacion/DECISIONES.md`: D-17, D-20, D-42, D-44, D-45, D-47, D-63, D-73, D-76, D-77, D-78, D-81, D-83, D-87.
- `docs/coordinacion/BACKLOG.md` (fila P10), `docs/coordinacion/ESTADO-REAL.md` (§ SEO básico, PP.7).
- `docs/criterios/P9-criterios.md` §2.5 y §2.6 (CA-P9-48 a CA-P9-56).
- `docs/GUIA-PUBLICACION.md` §1, §3, §6, §9, §11.
- `docs/requisitos/requisitos-funcionales-v3.0.md`: matriz (HU-001 a HU-012, HU-063, HU-081, HU-085), RN-CA-001 a RN-CA-005, RN-VE-013, RN-VE-014, RN-SE-005, RN-SE-006, RN-US-006, RN-PA-005; §14 (integraciones: "redes sociales" y "Google Analytics" como posibilidades, sin HU).
- Logos: `docs/marca/Logo VCA Automotores Renovado.png` (1312×1199, PNG con transparencia) y `docs/marca/WhatsApp Image 2026-11-16 at 8.54.30 PM.jpeg` (1600×533, JPEG). Revisados visualmente: el JPEG ocupa solo la franja superior (aprox. de y=80 a y=310), tiene fondo blanco, una franja gris hacia y=340 y texto negro.
- Código (lectura directa del analista, no es auditoría): `frontend/index.html`, `frontend/vite.config.ts`, `frontend/public/` (`favicon.svg`, `favicon-32.png`, `robots.txt`), `frontend/src/rutas.ts`, `App.tsx:105-129`, `:276-296`, `:594-599`, `seo.ts`, `components/Encabezado.tsx` (`LogoPortal`), `PiePortal.tsx`, `DetalleVehiculo.tsx:63-65`, `Inicio.tsx:79-82`, `RestablecerContrasena.tsx:22-29`, `api/recuperacion.ts:18-97`, `api/contactos.ts:171-175`, `api/catalogo.ts:415-443`, `SolicitudCredito.tsx:49`, `useContactoPortal.ts:67`, `portal.css:6-8`; `backend/Camion.Api/Endpoints/RecuperacionEndpoints.cs:28`, `:56`, `CatalogoEndpoints.cs:40-213`, `MediaEndpoints.cs`, `Catalogo/ConsultasCatalogo.cs`, `Program.cs:285-369`, `Seguridad/ConfiguracionServidor.cs:25`, `:198-199`, `Auth/RecuperacionAsistida.cs:16`, `:106-129`, `Models/Publicacion.cs:26-30`, `Camion.Api.Tests/Infraestructura/CamionApiFactory.cs:78`.

Numeración: matriz oficial de 87 HU (D-1). La v3.0 **no tiene HU ni RN de SEO, marca o vista previa**: esos criterios salen del encargo de P10 y de D-87. Las HU y RN citadas son las que P10 no debe romper (portal público, categorías, visibilidad, recuperación de contraseña, información corporativa).

Marcas:
- **CONFIRMADO**: sale literalmente de una HU, RN, decisión TOMADA o del encargo de P10.
- **PROPUESTO**: lo infiere el analista (valores, textos, formatos, rutas); necesita visto bueno del coordinador o de la responsable.
- **PENDIENTE D-n**: depende de una decisión abierta; se implementa con la opción recomendada solo si el coordinador lo autoriza.
- **EXISTE**: ya está en el código; en P10 queda como regresión.

Cómo se prueba: API = prueba de integración (`Camion.Api.Tests`); COMP = prueba de componente (Vitest + Testing Library); UNIT = prueba unitaria; BUILD = comprobación sobre `npm run build` / `dist/`; MAN = revisión manual (la responsable o QA, con herramientas externas cuando haya dominio).

**Migración: NO.** P10 no cambia el esquema ni escribe en la base. El `sitemap.xml` y la vista previa leen las publicaciones con la regla de visibilidad que ya existe (`ConsultasCatalogo.Visibles`). Los slugs de las publicaciones (`slug_publico`, formato `marca-linea-año-id`) no cambian. Solo hay claves de configuración nuevas (§3.5).

---

## 1. Qué pide / qué existe / qué falta

| Punto del encargo | Fuente | Qué existe | Qué falta |
|---|---|---|---|
| 1. Marca: logos, favicon, íconos, textos | D-87, D-42 | Logo de texto "Financiamos Tu Camión" con un camión en SVG (`Encabezado.tsx:22-43`), usado en encabezado y pie (`claro`); `favicon.svg` y `favicon-32.png` con los colores de D-42 (P9); "Financiamos Tu Camión" en 13 textos de `frontend/src` | Derivados web de los dos logos; encabezado con el horizontal y pie con el cuadrado; favicon, `apple-touch-icon`, manifiesto e imagen para compartir; constantes de marca en un solo sitio |
| 2. `<title>` y descripciones por página | D-87, CA-P9-52 | `<title>Financiamos Tu Camión</title>` (`index.html:9`); meta description fija (P9); título por ruta (`App.tsx:107-129`) y del vehículo con su título (`DetalleVehiculo.tsx:64`); `noindex` en privadas (`seo.ts`, `rutas.ts:163`) | Título por defecto nuevo; descripción, `canonical` y `robots` por página; títulos de categoría y de vehículo con marca, línea, año y ciudad |
| 3. Direcciones sin `#` | Encargo P10 | Enrutado por hash (`rutas.ts`, `useHash`, `navegar`); enlace de recuperación `UrlPortal + "/#/restablecer-contrasena?codigo="` (`RecuperacionEndpoints.cs:28`, `:56`) validado por `enlaceValido` y leído por `codigoDelHash` (`recuperacion.ts:32-34`, `:89-97`); Vite con proxy `/api`, `/health`, `/media`; la guía dice "no hace falta configurar reescrituras" (§9) | Rutas por ruta (history API), navegación sin recarga, conversión de enlaces viejos `#/…`, nuevo formato del enlace de recuperación, reglas del proxy (respaldo a `index.html` sin tocar `/api`, `/media`, `/health`), migrar 33 archivos de pruebas del frontend que usan `#/` (585 apariciones) |
| 4. `sitemap.xml` y `Sitemap` en `robots.txt` | Encargo P10, D-81 | `robots.txt` estático sin `Sitemap` y con `Disallow: /api/` (`frontend/public/robots.txt`) | `GET /sitemap.xml` y `GET /robots.txt` dinámicos en la API; URL pública configurable; comportamiento sin dominio |
| 5. Datos estructurados JSON-LD | Encargo P10, D-44, D-83, D-45 | Nada | `Organization` en la portada y `Product`/`Vehicle` en la ficha |
| 6. Vista previa al compartir | Encargo P10, D-77 | Nada (sin Open Graph) | Etiquetas genéricas en `index.html` y, con D-88 A, HTML de la ficha servido por la API |

Hallazgo técnico (corrige CA-P9-54, que era PROPUESTO): el `robots.txt` de P9 bloquea `/api/`. Con el portal armado por JavaScript, Google necesita leer `/api/catalogo/…` para ver el contenido de la ficha y del catálogo; si no puede, indexa páginas vacías. P10 permite las rutas públicas de solo lectura (CA-P10-51).

---

## 2. Criterios de aceptación

### 2.1 Logos, favicon e íconos (D-87, D-42, D-91)

Archivos derivados (PROPUESTO; nombres y carpeta orientativos):

| Archivo en `frontend/public/marca/` | Origen | Tamaño | Peso máximo |
|---|---|---|---|
| `logo-horizontal.webp` y `logo-horizontal.png` | JPEG horizontal recortado al contenido (sin el margen blanco ni la franja gris) | alto 112 px (doble densidad para mostrar a 56 px) | 30 KB (WebP), 60 KB (PNG) |
| `logo-cuadrado.webp` y `logo-cuadrado.png` | PNG cuadrado, con transparencia | ancho 320 px (para mostrar a 160 px) | 60 KB (WebP), 120 KB (PNG) |
| `og-portal.jpg` | Logo horizontal centrado sobre fondo blanco | 1200×630 | 200 KB |
| `favicon.ico` (16, 32 y 48), `favicon-32.png`, `apple-touch-icon.png` (180×180), `icon-192.png`, `icon-512.png` | Recorte del logo cuadrado (D-91 b) | los indicados | 10 KB cada PNG pequeño; 60 KB el de 512 |

| ID | Criterio (Dado / Cuando / Entonces) | Marca | Prueba |
|---|---|---|---|
| CA-P10-01 | **Dado** el repositorio, **entonces** existen los derivados de la tabla en `frontend/public/marca/`, generados a partir de `docs/marca/`; los dos originales de `docs/marca/` **no se modifican, no se renombran y no se copian** a `frontend/public/` (no se publican en el sitio). | Usar los logos entregados: CONFIRMADO (D-87). Archivos y carpeta: PROPUESTO | BUILD, MAN |
| CA-P10-02 | **Dado** cualquier página, **entonces** el encabezado muestra el logo horizontal con `<picture>` (WebP y respaldo PNG), con atributos `width` y `height` (sin saltos de diseño al cargar), a 56 px de alto en escritorio. | Logo horizontal en el encabezado: CONFIRMADO (encargo, D-87). Formato y tamaño: PROPUESTO | COMP, MAN |
| CA-P10-03 | El logo es un enlace a `/` cuyo nombre accesible es su texto alternativo: `alt="VCA Automotores, Venta de Camiones. Financiamos tu Camión"`. Con lector de pantalla se anuncia como enlace con ese nombre; no hay otro texto oculto duplicado. | Alt: PROPUESTO | COMP (`getByRole('link', { name: /VCA Automotores/ })` con `href="/"`) |
| CA-P10-04 | **Dado** un ancho de 320 a 480 px, **entonces** el logo horizontal baja a 36 px de alto, no desborda el encabezado y el menú y la búsqueda siguen usables. | PROPUESTO | MAN (320, 375, 768 y 1280 px) |
| CA-P10-05 | **Dado** el pie oscuro, **entonces** muestra el logo **cuadrado** (PNG con transparencia, máximo 120 px de alto) con `alt="VCA Automotores"`. **Nunca** se usa el JPEG horizontal sobre fondo oscuro (su texto negro no se lee ni tiene transparencia). | Cuadrado para el pie: CONFIRMADO (encargo). Regla del fondo oscuro: PROPUESTO | COMP, MAN |
| CA-P10-06 | El logo de texto actual (`LogoPortal` con "Financiamos Tu" + "Camión" y el camión SVG) desaparece del encabezado y del pie; no quedan dos marcas en la misma pantalla. | PROPUESTO | COMP |
| CA-P10-07 | `index.html` enlaza `favicon.ico`, `favicon-32.png` y `apple-touch-icon.png`, hechos con el recorte del logo cuadrado que decida D-91 b (recomendación: la cabeza del toro). Se retira el enlace a `favicon.svg` de P9 (el ícono de D-42 dejaría dos marcas). | Favicon a partir del logo: CONFIRMADO (encargo). Recorte: **PENDIENTE D-91** | BUILD, MAN |
| CA-P10-08 | `apple-touch-icon.png` (180×180) e `icon-192/512.png` tienen **fondo opaco blanco** (iOS rellena la transparencia en negro). | PROPUESTO | MAN |
| CA-P10-09 | `frontend/public/site.webmanifest` con `name: "Venta de Camiones – Financiamos tu Camión"`, `short_name: "VCA Camiones"`, `lang: "es-CO"`, `start_url: "/"`, `display: "browser"` (no se instala como aplicación ni lleva service worker), `background_color: "#ffffff"`, `theme_color: "#d71920"` (rojo de `portal.css:6`) e íconos 192 y 512. `index.html` lo enlaza y añade `<meta name="theme-color" content="#d71920">`. | Manifiesto "si aplica": PROPUESTO | BUILD |
| CA-P10-10 | `og-portal.jpg` (1200×630, máximo 200 KB) existe y es la imagen genérica de vista previa (CA-P10-82). | PROPUESTO | BUILD, MAN |
| CA-P10-11 | Ningún archivo de `frontend/public/marca/` supera el peso de la tabla; la página de inicio no descarga más de 40 KB en imágenes de marca en un navegador con WebP. | PROPUESTO | BUILD (prueba que lee los tamaños), MAN (pestaña Red) |
| CA-P10-12 | Todas las rutas a imágenes, íconos y manifiesto son **absolutas** (`/marca/…`, `/favicon.ico`), para que funcionen en direcciones con varios niveles (`/vehiculo/x`, `/cuenta/usuarios`). `npm run build` las copia a `dist/`. | PROPUESTO (consecuencia de §2.4) | BUILD, MAN |

### 2.2 Textos de marca: dónde cambian y dónde no (D-87)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P10-13 | Un único módulo (por ejemplo `frontend/src/marca.ts`) define `MARCA = "VCA Automotores"`, `NOMBRE_PORTAL = "Venta de Camiones"`, `LEMA = "Financiamos tu Camión"` y `SUFIJO_TITULO = "Financiamos tu Camión"`. Títulos, pie, cuenta y mensajes los leen de ahí. | Nombres: CONFIRMADO (D-87). Módulo: PROPUESTO | UNIT |
| CA-P10-14 | `index.html` tiene `<title>Venta de camiones usados y crédito | Financiamos tu Camión</title>` (57 caracteres) y la misma meta description de CA-P9-52 (sin cambios). | Texto propuesto por el coordinador en D-87 ("pendiente de afinar"): PROPUESTO, falta visto bueno de la responsable. Descripción: EXISTE (CA-P9-52) | BUILD, UNIT (lectura del HTML) |
| CA-P10-15 | Los títulos por página usan el sufijo `| Financiamos tu Camión` ("tu" en minúscula, como el lema de D-87 y el logo). | Escritura del lema: CONFIRMADO (D-87). Sufijo: PROPUESTO | COMP |
| CA-P10-16 | La franja inferior del pie dice `© <año> VCA Automotores` en lugar de `© <año> Financiamos Tu Camión`. La razón social, el NIT y la dirección siguen como en P9 (D-83 A). La frase "Portal de venta de vehículos de trabajo en Colombia." no cambia. | Marca VCA: CONFIRMADO (D-87). Texto: PROPUESTO | COMP |
| CA-P10-17 | En "Mi cuenta" (`App.tsx:597-599`) el antetítulo "RED DE VENTA" pasa a "VCA AUTOMOTORES" y el h1 "Financiamos Tu Camión" pasa a "Venta de Camiones"; el párrafo no cambia. | PROPUESTO | COMP (actualizar las pruebas que buscan el h1) |
| CA-P10-18 | Los mensajes prellenados de WhatsApp y correo (`contactos.ts:173`, `SolicitudCredito.tsx:49`, `recuperacion.ts:18`, `:24`, `:29`, `:38`, `:42`, `:54`, `useContactoPortal.ts:67`) solo cambian "Financiamos Tu Camión" por "Financiamos tu Camión" (constante de CA-P10-13). Ningún otro texto de esos mensajes cambia. | PROPUESTO | UNIT, COMP |
| CA-P10-19 | No se reescriben: los borradores de `docs/legal/`, la franja "Nuestros servicios" (D-42), los textos de los avisos y formularios, ni el nombre del proyecto en la documentación interna. Una búsqueda en `frontend/src` (sin pruebas) no encuentra "Financiamos Tu Camión" con "Tu" en mayúscula. | PROPUESTO | UNIT (búsqueda) |
| CA-P10-20 | Portada: el h1 queda según D-92 (recomendación B: "Venta de camiones en Colombia" como h1 pequeño encima, y la frase actual como subtítulo con el mismo aspecto). Hasta que se decida, no se cambia. | **PENDIENTE D-92** | COMP, MAN |

### 2.3 Títulos, descripciones, `canonical` y `robots` por página

Tabla de páginas (textos PROPUESTOS; las rutas son las de §4):

| Ruta | `<title>` | Meta description | `robots` |
|---|---|---|---|
| `/` | Venta de camiones usados y crédito \| Financiamos tu Camión | La de CA-P9-52 | indexar |
| `/vehiculos` | Camiones y vehículos de trabajo en venta \| Financiamos tu Camión | Camiones, carros, carrocerías y maquinaria en venta en Colombia. Filtra por marca, modelo, año y precio, y escríbenos por WhatsApp. | indexar (sin filtros) |
| `/camiones` | Camiones en venta en Colombia \| Financiamos tu Camión | Camiones en venta en Colombia con fotos, precio y ciudad. Escríbenos por WhatsApp o solicita tu crédito para camión. | indexar |
| `/carros` | Carros en venta en Colombia \| Financiamos tu Camión | Carros en venta en Colombia con fotos, precio y ciudad. Escríbenos por WhatsApp o solicita tu crédito. | indexar |
| `/carrocerias` | Carrocerías en venta para camión \| Financiamos tu Camión | Carrocerías en venta en Colombia con fotos, precio y ciudad. Escríbenos por WhatsApp. | indexar |
| `/maquinaria` | Maquinaria amarilla en venta \| Financiamos tu Camión | Maquinaria amarilla en venta en Colombia con fotos, precio y ciudad. Escríbenos por WhatsApp. | indexar |
| `/vehiculo/<slug>` | CA-P10-22 | CA-P10-23 | indexar si está visible |
| `/credito` | Crédito para camión \| Financiamos tu Camión | Solicita tu crédito para camión o vehículo de trabajo. Te ayudamos ante bancos y entidades financieras. | indexar |
| `/credito/<slug>` | El de hoy (`TITULO_CREDITO_VEHICULO`) | — | `noindex` |
| `/vende-tu-camion` | Vende tu camión \| Financiamos tu Camión | Cuéntanos qué camión quieres vender y te contactamos por WhatsApp. No necesitas cuenta. | indexar |
| `/seguro-todo-riesgo` | Seguro todo riesgo para tu vehículo \| Financiamos tu Camión | Déjanos tus datos y te ayudamos con el seguro todo riesgo de tu camión o vehículo. | indexar |
| `/busco-un-vehiculo` | Busco un vehículo \| Financiamos tu Camión | Cuéntanos qué vehículo buscas y te contactamos con opciones. No necesitas cuenta. | indexar |
| `/privacidad`, `/terminos` | Los de hoy (CA-P9-21) | — | `noindex` mientras estén en revisión legal |
| Privadas y "no encontrada" | Los de hoy | — | `noindex` (EXISTE, CA-P9-50 y 55) |

La descripción de `/credito` no promete aprobación ni tasas (D-44, D-45: la empresa es intermediaria).

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P10-21 | **Dado** cada ruta de la tabla, **cuando** se abre (o se navega a ella), **entonces** `document.title`, `meta[name=description]` y `meta[name=robots]` toman los valores de la tabla. | Títulos y descripciones por página: CONFIRMADO (encargo). Textos: PROPUESTO | COMP (tabla de casos) |
| CA-P10-22 | **Dado** un vehículo visible, **entonces** su título es `<Marca> <Línea> <Año> en <Municipio> | Financiamos tu Camión` (por ejemplo "Chevrolet NPR 2018 en Medellín | Financiamos tu Camión"). Si falta la marca o la línea (carrocerías y máquinas, D-19), se usa el título de la publicación en su lugar; si falta el año, se omite. | Marca, línea, año y ciudad: CONFIRMADO (encargo). Formato: PROPUESTO | UNIT, COMP |
| CA-P10-23 | Su descripción es `<Categoría> <Marca> <Línea> <Año>[, <N> km] en <Municipio>, <Departamento>. Precio: <precio>[ negociable].[ Reservado.] Escríbenos por WhatsApp o solicita tu crédito.`, con los mismos formatos de la ficha (`formatoCop`, `formatoKm`; kilometraje solo si `mostrarKilometraje`), cortada en 160 caracteres por palabra completa con "…". No usa la descripción libre del asesor. | PROPUESTO | UNIT |
| CA-P10-24 | **Dado** un vehículo que no existe o ya no está visible (vendido, vencido, oculto), **entonces** el título es "Este vehículo no está disponible | Financiamos tu Camión", `robots` = `noindex` y no hay JSON-LD de vehículo. | Vendido oculto: CONFIRMADO (RN-VE-014). `noindex`: PROPUESTO | COMP |
| CA-P10-25 | **Dado** una categoría sin vehículos visibles (no está en `/api/catalogo/filtros`), **entonces** se ve su página con el aviso "Por ahora no hay vehículos publicados en esta categoría." y `noindex`. | PROPUESTO | COMP |
| CA-P10-26 | **Dado** `/vehiculos` o una categoría con filtros distintos de `pagina` (por ejemplo `?marcaId=3`, `?q=npr`), **entonces** `robots` = `noindex, follow` y `canonical` apunta a la página sin filtros. Solo con `?pagina=N` (N > 1): se indexa y `canonical` es la propia dirección con `?pagina=N`. | PROPUESTO (evita miles de combinaciones duplicadas) | UNIT, COMP |
| CA-P10-27 | Toda página indexable tiene `<link rel="canonical">` con la dirección absoluta sin filtros (origen de `window.location.origin`), sin barra final y sin `#`. Las páginas con `noindex` no llevan `canonical`. | PROPUESTO | COMP |
| CA-P10-28 | En el documento hay **como máximo una** etiqueta de cada tipo (`title`, `meta description`, `meta robots`, `canonical`, cada `og:*`, cada bloque JSON-LD); al navegar se actualizan las existentes y al salir de la ficha se quitan las del vehículo. Las etiquetas que maneja el frontend se marcan con `data-camion` (como `seo.ts`) y son las mismas que inyecta la API (CA-P10-80). | PROPUESTO | COMP |
| CA-P10-29 | `/privacidad` y `/terminos` llevan `noindex` mientras `TEXTOS_LEGALES_EN_REVISION` sea `true` (CA-P9-20); al publicarse los textos aprobados, se indexan. | PROPUESTO | COMP |
| CA-P10-30 | Regresión: las pantallas privadas, `/restablecer-contrasena` y "no encontrada" siguen con `noindex` (`rutaSinIndexar`), y se añade `/credito/<slug>`. | EXISTE / PROPUESTO | UNIT |
| CA-P10-31 | Si `título + sufijo` supera 65 caracteres, se omite el sufijo; ninguna descripción supera 160 caracteres. | PROPUESTO | UNIT |
| CA-P10-32 | Al pasar de una página con descripción propia a otra que no la tiene, vuelve la descripción por defecto de CA-P9-52 (no queda la anterior). | PROPUESTO | COMP |

### 2.4 Direcciones sin `#` (history routing)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P10-33 | Cada pantalla tiene una dirección sin `#` según la tabla de §4 (por ejemplo `/vehiculo/chevrolet-npr-2018-25`, `/camiones`, `/cuenta/usuarios?rol=ASESOR`). Cada vehículo tiene su propia dirección indexable. | Sin `#` y una URL por vehículo: CONFIRMADO (encargo, BACKLOG P10). Rutas: PROPUESTO | UNIT (`rutas.test.ts`) |
| CA-P10-34 | `leerRuta` recibe la ruta y la consulta (`pathname`, `search`) y devuelve la misma `Ruta` que hoy da su equivalente con `#` (tabla de §4), con y sin barra final. Un slug con codificación no válida (`/vehiculo/%E0%A4%A`) o una ruta desconocida da "no encontrada" (CA-P9-48). Las rutas distinguen mayúsculas (`/Camiones` → no encontrada). | PROPUESTO | UNIT |
| CA-P10-35 | Todos los enlaces internos son rutas sin `#` (`href="/camiones"`). En el DOM de inicio, catálogo, ficha, pie, cuenta y panel no hay ningún `href` que empiece por `#/`. En `frontend/src` (sin pruebas) solo el convertidor de CA-P10-41 contiene `'#/`. | PROPUESTO | COMP, UNIT (búsqueda) |
| CA-P10-36 | Un clic normal en un enlace interno cambia la pantalla **sin recargar** el documento (`history.pushState`). Con Ctrl, Cmd, Mayús, clic central o `target="_blank"`, el navegador hace lo de siempre (pestaña o ventana nueva). Los enlaces a `/api/`, `/media/`, `/health`, `mailto:`, `tel:` y otros dominios no se interceptan. | PROPUESTO | COMP |
| CA-P10-37 | Atrás y Adelante del navegador (`popstate`) cambian de pantalla; los filtros del catálogo viajan en la consulta (`/vehiculos?marcaId=3&pagina=2`) y se recuperan al volver, como hoy con el hash (HU-004 a HU-011). "Volver a resultados" lleva a la última búsqueda. | Filtros en la URL: EXISTE (P3). Con rutas: PROPUESTO | COMP |
| CA-P10-38 | Al navegar a otra pantalla la página sube al principio y el foco va al `h1`, como hoy (`App.tsx:276-296`); en la cuenta, los formularios conservan su propio manejo del foco. | EXISTE / PROPUESTO | COMP |
| CA-P10-39 | Los enlaces de categoría del encabezado y del pie llevan a `/carros`, `/camiones`, `/carrocerias` y `/maquinaria` (nombres de D-17). La ruta se traduce al `tipoId` con `/api/catalogo/filtros`. **Dado** `/vehiculos?tipoId=<id de Camión>` sin otros filtros, **entonces** se reemplaza la dirección por `/camiones` (`replaceState`). | Categorías: CONFIRMADO (HU-005, HU-006, D-17). Rutas: PROPUESTO | COMP |
| CA-P10-40 | **Dado** una categoría que no existe en `filtros` (sin vehículos visibles), **entonces** CA-P10-25 (no "no encontrada"). | PROPUESTO | COMP |
| CA-P10-41 | **Compatibilidad con enlaces viejos.** **Dado** que se abre `/` con un hash que empieza por `#/` (por ejemplo `/#/vehiculo/x`, `/#/buscar?marcaId=3`, `/#/cuenta/usuarios?rol=ASESOR`), **entonces**, antes de pintar la pantalla, se reemplaza la dirección por su equivalente sin `#` (`replaceState`, sin recargar ni pedir nada al servidor) y se muestra esa pantalla. `#/buscar?tipoId=N` lleva a la categoría si corresponde. Un `#/…` desconocido lleva a "no encontrada". Un hash que no empieza por `/` (como `#contenido`) no se toca. | Redirigir enlaces viejos: CONFIRMADO (encargo). Forma: PROPUESTO | UNIT, COMP |
| CA-P10-42 | **Enlace viejo de recuperación.** **Dado** `/#/restablecer-contrasena?codigo=ABC` (enlaces generados antes de publicar P10, vigentes 60 minutos), **entonces** el código se lee **en memoria**, la dirección pasa a `/restablecer-contrasena` sin el código y el formulario funciona igual que hoy (CA-P8-67). El código nunca pasa a la ruta ni a la consulta, ni siquiera de forma transitoria. | Validez del enlace (HU-063, RN-US-006, D-73 D): CONFIRMADO. Conversión: PROPUESTO | COMP |
| CA-P10-43 | **Enlace nuevo de recuperación.** El panel y `recuperar-admin` generan `<Recuperacion:UrlPortal>/restablecer-contrasena#codigo=<código>` (`RutaFrontend = "/restablecer-contrasena#codigo="`). `enlaceValido` acepta solo ese formato (`^https?://[^\s/?#]+/restablecer-contrasena#codigo=[A-Za-z0-9_\-%.~]+$`); `codigoDelHash` lee `#codigo=` (y el formato viejo para CA-P10-42). Al leerlo, la pantalla quita el código de la dirección (`replaceState` a `/restablecer-contrasena`), como hoy. Siguen igual: un solo uso, 60 minutos, sesiones cerradas, auditoría y los botones de WhatsApp, `mailto:` y Gmail (CA-P8-68). | Funcionamiento: EXISTE (D-73 D, D-78, D-79). Formato: PROPUESTO | API (`P8RecuperacionTests`, `P8RecuperarAdminTests`), UNIT, COMP |
| CA-P10-44 | El código de recuperación va **siempre en el fragmento** (`#`), que el navegador no envía al servidor: en el registro de accesos del proxy y de la API solo aparece `/restablecer-contrasena`. `Referrer-Policy: no-referrer` (D-78) se mantiene. | PROPUESTO (seguridad: con history routing, un código en la consulta llegaría a los registros del servidor) | MAN (registro del proxy), COMP |
| CA-P10-45 | "Saltar al contenido" (`#contenido`) sigue moviendo el foco al contenido sin cambiar de pantalla. | EXISTE | COMP |
| CA-P10-46 | Ninguna ruta del frontend empieza por `/api`, `/media`, `/health`, `/assets`, `/marca`, `/sitemap.xml`, `/robots.txt`, `/favicon` ni `/site.webmanifest` (prefijos reservados para el servidor o para archivos). | PROPUESTO | UNIT |
| CA-P10-47 | **Servidor de desarrollo.** Con `npm run dev`, recargar `http://localhost:5173/vehiculo/<slug>`, `/camiones` o `/cuenta/usuarios` muestra la pantalla (Vite responde `index.html`); `/api`, `/media` y `/health` siguen yendo a `http://localhost:5080`, y se añaden `/sitemap.xml` y `/robots.txt` al proxy de `vite.config.ts`. `/vehiculo/` **no** se envía a la API en desarrollo (Vite sirve su propio `index.html`). Igual con `npm run preview`. | PROPUESTO | MAN |
| CA-P10-48 | **Proxy en producción** (guía §6 y §9, ver §5): recargar cualquier ruta del portal responde `index.html` con 200; `/api/inexistente` responde el 404 JSON de la API (nunca `index.html`); `/media/inexistente.jpg` responde 404 de la API; `/health` responde la API; `/assets/inexistente.js` responde 404 (nunca `index.html`, que el navegador intentaría ejecutar como script). | Proxy sin romper `/api`, `/media`, `/health`: CONFIRMADO (encargo). Reglas: PROPUESTO | MAN (lista de §5) |
| CA-P10-49 | `index.html` se entrega con `Cache-Control: no-cache`; los archivos de `/assets/` (con huella en el nombre) con `Cache-Control: public, max-age=31536000, immutable`. Así una publicación nueva del frontend se ve sin borrar la caché. | PROPUESTO | MAN |
| CA-P10-50 | Las pruebas del frontend que hoy usan `window.location.hash` o `#/` (33 archivos) pasan a un ayudante común (por ejemplo `irA('/ruta')`, que usa `pushState` y lanza `popstate`), y `rutas.test.ts` cubre la tabla de §4, la compatibilidad y la recuperación. Todas las suites existentes pasan. | PROPUESTO | UNIT, COMP |

### 2.5 `robots.txt` y `sitemap.xml` (D-81, D-89)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P10-51 | **Dado** `Portal:PermitirIndexacion = true`, **cuando** se pide `GET /robots.txt`, **entonces** 200 `text/plain; charset=utf-8` con: `User-agent: *`, `Allow: /api/catalogo/`, `Allow: /api/portal/`, `Disallow: /api/`, `Disallow: /health`, `Allow: /` y `Sitemap: <URL pública>/sitemap.xml`. Las pantallas privadas **no** se bloquean aquí: llevan `noindex`, que Google solo ve si puede leer la página. | Línea `Sitemap`: CONFIRMADO (encargo). `Allow` de la API pública: PROPUESTO (corrige CA-P9-54) | API |
| CA-P10-52 | **Dado** `Portal:PermitirIndexacion = false` (valor por defecto), **entonces** `GET /robots.txt` responde `User-agent: *` y `Disallow: /`, sin línea `Sitemap`. | **PENDIENTE D-89** (recomendación A) | API |
| CA-P10-53 | Con la indexación apagada, `GET /sitemap.xml` responde 404 y las fichas servidas por la API (D-88 A) llevan `X-Robots-Tag: noindex` y `<meta name="robots" content="noindex">`. | **PENDIENTE D-89** | API |
| CA-P10-54 | La guía (§2 y §11) incluye el paso "encender la indexación" (`Portal__PermitirIndexacion=true`) al abrir al público, después de D-44 y D-81, y comprobar `robots.txt` y `sitemap.xml`. | **PENDIENTE D-89** | MAN |
| CA-P10-55 | **Dado** la indexación encendida, **cuando** se pide `GET /sitemap.xml`, **entonces** 200 `application/xml; charset=utf-8` con un `<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">` que tiene: `/`, `/vehiculos`, las categorías con al menos un vehículo visible, `/credito`, `/vende-tu-camion`, `/seguro-todo-riesgo`, `/busco-un-vehiculo` y una entrada `/vehiculo/<slug>` por cada publicación visible. | Vehículos publicados y páginas públicas: CONFIRMADO (encargo). Lista: PROPUESTO | API |
| CA-P10-56 | El sitemap **no** incluye: publicaciones no visibles (vendidas, vencidas, ocultas, de asesor no activo o sin plan vigente, RN-VE-013, RN-VE-014, D-41), `/credito/<slug>`, pantallas privadas, `/restablecer-contrasena`, ni `/privacidad` y `/terminos` mientras estén en revisión legal. | RN-VE-013, RN-VE-014: CONFIRMADO. Resto: PROPUESTO | API |
| CA-P10-57 | Cada `<loc>` es absoluto (`<URL pública>/vehiculo/<slug>`) y escapado para XML. Los vehículos llevan `<lastmod>` (fecha UTC `AAAA-MM-DD`), la mayor entre `publicaciones.actualizado_en` y la del vehículo si existe. Sin `changefreq` ni `priority`. Si hubiera más de 50.000 direcciones, se cortan en 50.000 y se escribe una advertencia en el log. | PROPUESTO | API |
| CA-P10-58 | **URL pública.** Se toma de `Portal:UrlPublica` y, si falta, de `Recuperacion:UrlPortal`. Debe ser absoluta, sin ruta, consulta ni fragmento (se quita la barra final). Fuera de Development debe empezar por `https://` o la API no arranca (como D-78 M-2). **No** se usa `Portal:Origen`, que activa CORS (`ConfiguracionServidor.cs:25`). Sin dominio todavía (D-81): en local vale `http://localhost:5173` (valor por defecto de `Recuperacion:UrlPortal`); en un servidor sin dominio la API ya no arranca porque `Recuperacion:UrlPortal` es obligatoria con `https://`, así que no se puede publicar un sitemap con una dirección provisional sin configurarla a propósito. | Comportamiento sin dominio: CONFIRMADO (encargo pide definirlo). Clave: PROPUESTO | API (configuración), UNIT |
| CA-P10-59 | `/sitemap.xml` y `/robots.txt` son anónimos, solo `GET` y `HEAD` (otro método: 405), con `Cache-Control: public, max-age=3600`; no escriben en la base ni en la auditoría y no exponen datos del asesor. | PROPUESTO | API |
| CA-P10-60 | El sitemap usa `ConsultasCatalogo.Visibles` (única regla de visibilidad, `ConsultasCatalogo.cs:9`): un vehículo vendido desaparece del sitemap en la siguiente respuesta sin caché (como mucho 1 hora después). | CONFIRMADO (RN-VE-014; regla única de P3) / plazo: PROPUESTO | API |

### 2.6 Datos estructurados (JSON-LD) (D-44, D-45, D-83, D-90)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P10-61 | **Dado** la portada (`/`), **entonces** hay un bloque `<script type="application/ld+json">` con `@type: "Organization"`: `name: "VCA Automotores"`, `alternateName: "Venta de Camiones – Financiamos tu Camión"`, `url` (origen + `/`), `logo` (URL absoluta de `logo-cuadrado.png`), `email` y `contactPoint` (`telephone` con el WhatsApp vigente en formato `+57 …`, `contactType: "customer service"`, `areaServed: "CO"`, `availableLanguage: "es"`), y `sameAs` con Instagram y TikTok solo si tienen enlace válido (D-63 A, CA-P7-39). Los datos salen de `GET /api/portal/contacto`; si la lectura falla, se usan los de respaldo del pie (D-42). Solo en la portada. | `Organization`: CONFIRMADO (encargo). Campos: PROPUESTO | COMP, UNIT |
| CA-P10-62 | El bloque **no** lleva dirección física ni `@type: "AutoDealer"` hasta que se decida D-90 (recomendación C: sin dirección ahora; `AutoDealer` cuando haya una sede de atención confirmada). | **PENDIENTE D-90** | COMP |
| CA-P10-63 | **Dado** `empresa` no nula en `GET /api/portal/contacto` (los tres datos completos, CA-P9-33), **entonces** se añaden `legalName` (razón social) y `taxID` (NIT normalizado, `800197268-4`). **Dado** `empresa: null`, **entonces** no se añade ninguno. | Solo si están completos: CONFIRMADO (encargo; D-83 A). Campos: PROPUESTO | COMP |
| CA-P10-64 | **Dado** una ficha visible, **entonces** hay un bloque con `@type: ["Product", "Vehicle"]`: `name` (título de CA-P10-22 sin sufijo), `url` (canónica), `image` (URLs absolutas de las fotos, máximo 10), `description` (la descripción comercial si existe, cortada en 500 caracteres; si no, la de CA-P10-23), `category` (categoría), `brand` (`{ "@type": "Brand", "name": marca }`, si hay), `model` (línea), `vehicleModelDate` (año), `mileageFromOdometer` (`{ "@type": "QuantitativeValue", "value": km, "unitCode": "KMT" }`, solo si `mostrarKilometraje`), `fuelType` (si hay). Campos vacíos se omiten. | Vehicle/Product en el detalle: CONFIRMADO (encargo). Campos: PROPUESTO | UNIT, COMP |
| CA-P10-65 | El bloque lleva `offers`: `{ "@type": "Offer", "price": <precioCop>, "priceCurrency": "COP", "availability": "https://schema.org/InStock" (DISPONIBLE) o "https://schema.org/LimitedAvailability" (RESERVADO), "url": <canónica>, "availableAtOrFrom": { "@type": "Place", "address": { "@type": "PostalAddress", "addressLocality": <municipio>, "addressRegion": <departamento>, "addressCountry": "CO" } } }`. **D-44 no impide el precio**: D-44 trata la política de datos personales y los textos legales, no el precio, que ya es público en la ficha desde P3. | Lectura de D-44: CONFIRMADO. Incluir el precio: PROPUESTO | UNIT |
| CA-P10-66 | El bloque **no** lleva: placa (nunca sale en el catálogo, `ConsultasCatalogo.cs:74`), datos ni teléfono del asesor, enlace de WhatsApp, `seller` (la empresa es intermediaria y no dueña del vehículo, D-45), `itemCondition` (no hay dato de nuevo o usado) ni calificaciones. | D-45, regla de `CatalogoContracts.cs:4`: CONFIRMADO. Resto: PROPUESTO | UNIT |
| CA-P10-67 | La ficha lleva además un `BreadcrumbList`: Inicio (`/`) › Categoría (`/camiones`…) › vehículo. | PROPUESTO | UNIT |
| CA-P10-68 | El JSON-LD se escribe con un serializador que escapa `<`, `>` y `&` (`<`…): **dado** un título de publicación `</script><script>alert(1)</script>`, **entonces** no se cierra el bloque ni se ejecuta nada, y el JSON sigue siendo válido. En el frontend se asigna con `textContent`, nunca con `innerHTML`. | PROPUESTO (seguridad: el título lo escribe el asesor) | UNIT, API |
| CA-P10-69 | Con dominio y portal abierto, la Prueba de resultados enriquecidos de Google y el validador de schema.org no muestran errores en la portada ni en una ficha (las advertencias se anotan). | PROPUESTO | MAN (después de D-81) |

### 2.7 Vista previa al compartir (Open Graph y Twitter) (D-88, D-77)

CA-P10-70 a CA-P10-81 aplican solo con **D-88 A** (recomendada). CA-P10-82 y 83 aplican en cualquier caso.

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P10-70 | **Dado** `Portal:RutaIndexHtml` configurada (ruta del `index.html` publicado del frontend), **entonces** la API atiende `GET` y `HEAD` de `/vehiculo/{slug}`. Sin esa clave, la ruta no existe en la API y el proxy no debe enviarle `/vehiculo/` (vista previa genérica, opción C). | **PENDIENTE D-88** | API |
| CA-P10-71 | **Dado** un slug visible, **entonces** 200 `text/html; charset=utf-8` con el `index.html` en el que se reemplazó el bloque de etiquetas por: `<title>` (CA-P10-22), `meta description` (CA-P10-23), `canonical`, `og:type=website`, `og:site_name="Venta de Camiones – Financiamos tu Camión"`, `og:locale=es_CO`, `og:title`, `og:description`, `og:url`, `og:image`, `og:image:alt` (el título), `twitter:card=summary_large_image`, `twitter:title`, `twitter:description`, `twitter:image` y el JSON-LD de CA-P10-64 a 67. | PENDIENTE D-88 / textos: PROPUESTO | API |
| CA-P10-72 | **Dado** un slug inexistente, no visible, vacío o de más de 200 caracteres, **entonces** 404 `text/html` con el `index.html` genérico más `<meta name="robots" content="noindex">` (la aplicación muestra "Este vehículo no está disponible", como hoy). Mismo cuerpo para todos los casos (no se distingue vendido de inexistente, como `CatalogoEndpoints.cs:146`). | PENDIENTE D-88 | API |
| CA-P10-73 | `og:image` es la URL absoluta de la foto de portada del vehículo (`<URL pública>/media/…`); sin fotos, `<URL pública>/marca/og-portal.jpg`. | PENDIENTE D-88 | API |
| CA-P10-74 | Todo valor que sale de la base (título, marca, línea, municipio, descripción) se codifica para HTML en atributos y texto. **Dado** un título `"><img src=x onerror=alert(1)>`, **entonces** la respuesta no contiene la etiqueta `<img src=x` sin codificar. El JSON-LD cumple CA-P10-68. | PENDIENTE D-88 / seguridad: PROPUESTO | API |
| CA-P10-75 | El `index.html` del frontend marca el bloque reemplazable con `<!--camion:meta:inicio-->` y `<!--camion:meta:fin-->` (dentro van el `<title>`, la descripción y las etiquetas genéricas de CA-P10-82). La API cambia solo ese bloque; el resto del archivo (scripts y estilos con su huella) queda igual byte a byte. Si faltan las marcas, entrega el archivo sin cambios y escribe una advertencia en el log una vez. | PENDIENTE D-88 / forma: PROPUESTO | API |
| CA-P10-76 | Esas respuestas HTML llevan la política de seguridad **del frontend** (la de la guía §6: `default-src 'self'; img-src 'self' data: blob:; …`), no la de la API (`default-src 'none'`, `ConfiguracionServidor.cs:199`), que bloquearía la página; mantienen `Referrer-Policy`, `nosniff` y `X-Frame-Options: DENY`; `Cache-Control: public, max-age=300`. Las respuestas JSON de la API no cambian. | PENDIENTE D-88 / PROPUESTO | API |
| CA-P10-77 | La API vuelve a leer `index.html` cuando cambia la fecha del archivo (una publicación nueva del frontend se ve sin reiniciar la API). Fuera de Development, si la clave está puesta y el archivo no existe, la API no arranca (mensaje con el nombre de la clave, nunca la ruta completa ni secretos). Si el archivo desaparece con la API en marcha: 503 `text/plain` "El portal no está disponible en este momento." y error en el log. | PENDIENTE D-88 / PROPUESTO | API |
| CA-P10-78 | El proxy de producción envía `^/vehiculo/` a la API (§5); en desarrollo no (CA-P10-47). La ficha se ve igual que si la sirviera el proxy: la aplicación arranca, pide `/api/catalogo/publicaciones/{slug}` y pinta la ficha. | PENDIENTE D-88 | MAN |
| CA-P10-79 | La consulta usa `ConsultasCatalogo.VisiblesConDestacado` (la misma regla de visibilidad y los mismos datos públicos que `GET /api/catalogo/publicaciones/{slug}`). No escribe en la base ni en la auditoría. | PENDIENTE D-88 / CONFIRMADO (regla única) | API |
| CA-P10-80 | Al arrancar en una ficha servida por la API, el frontend **reutiliza** las etiquetas inyectadas (mismas marcas `data-camion`) en lugar de duplicarlas (CA-P10-28). | PENDIENTE D-88 | COMP |
| CA-P10-81 | Con dominio: al pegar el enlace de una ficha en WhatsApp y en el Depurador de contenido compartido de Facebook se ven la foto, el título y la descripción del vehículo; al pegar el de la portada, el logo y el título del portal. Se anota si alguna foto no aparece por su peso (las fotos pueden pesar hasta 5 MB, D-20; WhatsApp no siempre muestra imágenes grandes). | PENDIENTE D-88 / PROPUESTO | MAN (después de D-81) |
| CA-P10-82 | **Base para todas las páginas (opción C):** `index.html` lleva `og:type=website`, `og:site_name`, `og:locale=es_CO`, `og:title` y `og:description` (los de la portada) y `twitter:card=summary_large_image`. `og:url`, `og:image` (`/marca/og-portal.jpg`) y `twitter:image` se escriben con URL **absoluta** solo si al compilar existe `VITE_URL_PUBLICA` con `https://` (las redes no aceptan imágenes con ruta relativa); si no existe, se omiten esas tres. | Vista previa genérica: CONFIRMADO (encargo, opción C como base). Forma: PROPUESTO | BUILD |
| CA-P10-83 | El mensaje prellenado de "Escribir por WhatsApp" de la ficha añade la dirección del vehículo: "Hola, me interesa el vehículo <título> que vi en Financiamos tu Camión: <origen>/vehiculo/<slug>". Así el asesor o el administrador sabe cuál es y, con D-88 A, ve su vista previa. No se añade botón "Compartir" (D-77). | Sin botón Compartir: CONFIRMADO (D-77). Enlace en el mensaje: PROPUESTO (cambia el texto de D-47; necesita visto bueno del coordinador) | UNIT, COMP |

### 2.8 Seguridad, permisos y regresión

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P10-84 | Ni el HTML inyectado, ni el sitemap, ni el JSON-LD contienen datos personales (asesor, teléfono del asesor, correo de usuarios), la placa, ids internos distintos del que ya lleva el slug, ni valores de configuración secretos. | CONFIRMADO (RN-SE-005, `CatalogoContracts.cs:4`, CLAUDE.md "secretos") | API |
| CA-P10-85 | Permisos: `/robots.txt`, `/sitemap.xml` y `/vehiculo/{slug}` responden igual sin token, con token de comprador, asesor o administrador, y con un token no válido (no dan 401). Las rutas privadas del frontend siguen protegidas por la API como hoy (401 / 403), y sin sesión muestran el inicio de sesión o "sin permiso" (CA-P9-51). | RN-SE-005, RN-SE-006: CONFIRMADO | API, COMP |
| CA-P10-86 | `POST`, `PUT` o `DELETE` sobre `/robots.txt`, `/sitemap.xml` o `/vehiculo/{slug}`: 405. | PROPUESTO | API |
| CA-P10-87 | Regresión de la API: todas las rutas `/api/…`, `/media/…` y `/health…` responden igual que antes (contenido y encabezados). Las pruebas de P8 sobre el enlace de recuperación pasan con el formato nuevo (CA-P10-43). | EXISTE | API (suite completa) |
| CA-P10-88 | Regresión del frontend: todas las suites (P1 a P9) pasan con el ayudante de rutas (CA-P10-50); sesión, inactividad (HU-060, D-12), cierre en otras pestañas y formularios funcionan con las rutas nuevas. | EXISTE | COMP |
| CA-P10-89 | `GET /sitemap.xml` con 1.000 publicaciones visibles y `GET /vehiculo/{slug}` responden en menos de 500 ms en el equipo local, con una consulta para la lista (sin una consulta por publicación). | PROPUESTO | API (medición orientativa) |
| CA-P10-90 | P10 no añade cookies, scripts de terceros ni píxeles (D-76 A, D-77 A). La verificación en Google Search Console se hace por DNS del dominio (sin código); si se prefiere la etiqueta `google-site-verification`, va en `index.html` con su valor, que no es secreto. | D-76, D-77: CONFIRMADO. Método: PROPUESTO | MAN |

---

## 3. Contrato de API (rutas nuevas y cambios)

Ninguna ruta nueva está bajo `/api/`: las tres viven en la raíz porque los buscadores y las redes las piden ahí. Formato de error JSON común sin cambios.

### 3.1 `GET|HEAD /robots.txt` (nueva)
- Anónima. 200 `text/plain; charset=utf-8`, `Cache-Control: public, max-age=3600`.
- Indexación encendida (CA-P10-51):
  ```
  User-agent: *
  Allow: /api/catalogo/
  Allow: /api/portal/
  Disallow: /api/
  Disallow: /health
  Allow: /

  Sitemap: https://<dominio>/sitemap.xml
  ```
- Indexación apagada (CA-P10-52): `User-agent: *` y `Disallow: /`.
- Se retira `frontend/public/robots.txt` (si no, el proxy podría servir el archivo estático en lugar del dinámico); en desarrollo, Vite lo envía a la API (CA-P10-47).

### 3.2 `GET|HEAD /sitemap.xml` (nueva)
- Anónima. 200 `application/xml; charset=utf-8`, `Cache-Control: public, max-age=3600`. 404 `text/plain` con la indexación apagada.
- Cuerpo:
  ```xml
  <?xml version="1.0" encoding="UTF-8"?>
  <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
    <url><loc>https://<dominio>/</loc></url>
    <url><loc>https://<dominio>/vehiculos</loc></url>
    <url><loc>https://<dominio>/camiones</loc></url>
    …
    <url><loc>https://<dominio>/vehiculo/chevrolet-npr-2018-25</loc><lastmod>2026-11-16</lastmod></url>
  </urlset>
  ```
- Contenido y exclusiones: CA-P10-55 a CA-P10-60.

### 3.3 `GET|HEAD /vehiculo/{slug}` (nueva, solo con D-88 A y `Portal:RutaIndexHtml`)
- Anónima. Slug de 1 a 200 caracteres (mismo límite que `CatalogoEndpoints.cs:30`).
- 200 `text/html; charset=utf-8`: `index.html` con las etiquetas del vehículo (CA-P10-71, 73, 74, 75).
- 404 `text/html; charset=utf-8`: `index.html` genérico con `noindex` (CA-P10-72).
- 503 `text/plain` si el archivo no se puede leer (CA-P10-77).
- Encabezados: CA-P10-76; con indexación apagada, además `X-Robots-Tag: noindex` (CA-P10-53).

### 3.4 Enlace de recuperación (cambia)
- `POST /api/admin/usuarios/{id}/enlace-recuperacion` y `recuperar-admin`: misma respuesta; el campo `enlace` pasa de `https://<dominio>/#/restablecer-contrasena?codigo=<c>` a `https://<dominio>/restablecer-contrasena#codigo=<c>` (CA-P10-43). `POST /api/auth/restablecer-contrasena` no cambia.
- Documentar el cambio en `backend/GUIA-AUTENTICACION.md` §12.

### 3.5 Configuración nueva
| Clave (variable de entorno) | Obligatoria | Valor por defecto | Uso |
|---|---|---|---|
| `Portal:UrlPublica` (`Portal__UrlPublica`) | No | `Recuperacion:UrlPortal` | URL pública para sitemap, robots, `canonical` y Open Graph del servidor. `https://` fuera de Development (CA-P10-58). |
| `Portal:PermitirIndexacion` (`Portal__PermitirIndexacion`) | No | `false` | D-89 (CA-P10-52, 53). |
| `Portal:RutaIndexHtml` (`Portal__RutaIndexHtml`) | Solo con D-88 A | vacía (ruta desactivada) | Ruta del `index.html` publicado (CA-P10-70, 77). |
| `VITE_URL_PUBLICA` (al compilar el frontend) | No | vacía | URL absoluta de las etiquetas genéricas (CA-P10-82). Debe ser igual que `Portal:UrlPublica`. |

`Portal:Origen` no cambia de significado (solo CORS).

---

## 4. Rutas del frontend (antes → después)

| Antes (hash) | Después | Notas |
|---|---|---|
| `#/` | `/` | |
| `#/buscar?…` | `/vehiculos?…` | `#/buscar?tipoId=N` → `/camiones`, `/carros`, `/carrocerias` o `/maquinaria` |
| — | `/carros`, `/camiones`, `/carrocerias`, `/maquinaria` (con filtros opcionales en la consulta) | nuevas (HU-005, HU-006, D-17) |
| `#/vehiculo/<slug>` | `/vehiculo/<slug>` | con D-88 A, servida por la API |
| `#/credito`, `#/credito/<slug>` | `/credito`, `/credito/<slug>` | |
| `#/vende-tu-camion`, `#/seguro-todo-riesgo`, `#/busco-un-vehiculo` | igual sin `#` | |
| `#/privacidad`, `#/terminos` | `/privacidad`, `/terminos` | |
| `#/cuenta`, `#/cuenta/registro`, `#/cuenta/<seccion>?…` | igual sin `#` | |
| `#/inventario`, `#/inventario/nuevo`, `#/inventario/<id>` | igual sin `#` | |
| `#/publicaciones`, `#/contactos`, `#/contactos/<id>`, `#/ventas`, `#/mis-solicitudes`, `#/mis-indicadores` | igual sin `#` | |
| `#/restablecer-contrasena?codigo=…` | `/restablecer-contrasena#codigo=…` | el código queda en el fragmento (CA-P10-43, 44) |
| `#/<desconocida>` | "no encontrada" | |

Constantes: `HASH_*` y `hash*()` de `rutas.ts` pasan a `RUTA_*` y `ruta*()`; `useHash` y `navegar` pasan a leer y escribir `location.pathname + location.search` con `pushState` y `popstate`. También cambian los usos directos de `window.location.hash` en `AdminSuscripciones.tsx:367` y `RestablecerContrasena.tsx:22-29`, y los comentarios con `#/` de los componentes y de `robots.txt`.

---

## 5. Impacto en la guía de publicación (`docs/GUIA-PUBLICACION.md`)

| Sección | Cambio |
|---|---|
| §1 Diagrama | `/` → archivos del frontend con respaldo a `index.html`; `/api`, `/media`, `/health`, `/sitemap.xml`, `/robots.txt` y (con D-88 A) `/vehiculo/` → API. |
| §2 Lista de verificación | Añadir: `Portal__UrlPublica` (o confirmar `Recuperacion__UrlPortal`), `VITE_URL_PUBLICA` al compilar, `Portal__RutaIndexHtml` (D-88 A), encender la indexación al abrir al público (D-89), dar de alta el dominio en Google Search Console (por DNS) y enviar el sitemap. |
| §3 Variables | Las cuatro claves de §3.5. |
| §6 Proxy | Reglas de abajo; aclarar que para `/vehiculo/` los encabezados de seguridad los pone la API (CA-P10-76) y el proxy no debe añadir otra política de seguridad. En nginx, `add_header` dentro de un `location` anula los del bloque `server`: repetirlos en cada `location` del frontend. |
| §9 Frontend | Quitar "El portal usa rutas con `#`… no hace falta configurar reescrituras". Sustituir por: el proxy debe responder `index.html` para cualquier ruta que no sea un archivo ni de la API; compilar con `VITE_URL_PUBLICA`. |
| §11 Comprobación final | Recargar `https://<dominio>/vehiculo/<slug>` y `/cuenta/usuarios` (se ven); `/api/no-existe` da 404 JSON; `/assets/no-existe.js` da 404; `view-source:` de una ficha muestra sus `og:*` (D-88 A); `robots.txt` y `sitemap.xml` según D-89; un enlace de recuperación nuevo tiene la forma `https://<dominio>/restablecer-contrasena#codigo=…` y el registro del proxy no muestra el código; vista previa en WhatsApp y en el depurador de Facebook (CA-P10-81). |
| §12 Pendientes | D-88, D-89, D-90, D-91. |

Ejemplo de reglas (nginx; IIS y Caddy equivalentes en la guía; PROPUESTO, se prueba con el portal antes de fijarlo):

```nginx
root /srv/camion/frontend;   # contenido de frontend/dist

location ^~ /assets/      { try_files $uri =404; add_header Cache-Control "public, max-age=31536000, immutable"; }
location ^~ /api/         { proxy_pass http://127.0.0.1:5080; }
location ^~ /media/       { proxy_pass http://127.0.0.1:5080; }
location = /health        { proxy_pass http://127.0.0.1:5080; }
location ^~ /health/      { proxy_pass http://127.0.0.1:5080; }   # limitar a la red interna (§6)
location = /robots.txt    { proxy_pass http://127.0.0.1:5080; }
location = /sitemap.xml   { proxy_pass http://127.0.0.1:5080; }
location ^~ /vehiculo/    { proxy_pass http://127.0.0.1:5080; }   # solo con D-88 A
location /                { try_files $uri /index.html; add_header Cache-Control "no-cache"; }
```

(En cada `proxy_pass` van los encabezados `X-Forwarded-For` y `X-Forwarded-Proto` de §6.) En IIS: regla de URL Rewrite "si no es archivo ni empieza por `/api`, `/media`, `/health`, `/assets` → `/index.html`". En Caddy: `try_files {path} /index.html` después de los `reverse_proxy` de esos prefijos.

---

## 6. Trazabilidad

| HU / RN / D | Criterios | Prueba prevista |
|---|---|---|
| D-87 (nombre visible y logo) | CA-P10-01 a 20 | BUILD, COMP (`P10Marca.test.tsx`), MAN |
| D-42 (colores, pie oscuro, contacto de respaldo) | CA-P10-05, 09, 16, 61 | COMP, MAN |
| D-91 (archivos del logo, favicon) | CA-P10-01, 07 | BUILD, MAN |
| D-92 (h1 de la portada) | CA-P10-20 | COMP |
| Encargo P10 punto 2 (títulos y descripciones) | CA-P10-14, 21 a 32 | UNIT (`seoPaginas.test.ts`), COMP |
| HU-001 a HU-012, RN-CA-001 a RN-CA-011 (portal, búsqueda, categorías, filtros) | CA-P10-21, 25, 26, 33, 37, 39, 40, 55 | UNIT (`rutas.test.ts`), COMP (`Catalogo`, `Filtros`, `Portada`) |
| HU-005, HU-006, RN-CA-004, RN-CA-005, D-17 (categorías) | CA-P10-39, 40, 55 | COMP |
| RN-VE-013, RN-VE-014, D-41 (solo vigentes; vendido oculto) | CA-P10-24, 56, 60, 72, 79 | API (`P10SitemapTests`, `P10VistaPreviaTests`) |
| RN-SE-005, RN-SE-006 (consulta pública, accesos) | CA-P10-30, 84, 85, 86 | API, COMP |
| HU-063, RN-US-006, D-73 D, D-78, D-79 (recuperación) | CA-P10-42, 43, 44 | API (`P8RecuperacionTests`, `P8RecuperarAdminTests`), COMP (`P8Reportes`, `P8Qa`) |
| HU-085, RN-PA-005, D-83 A (empresa) | CA-P10-16, 63 | COMP |
| HU-081, RN-PA-001, D-63 A (contacto vigente) | CA-P10-61 | COMP |
| D-44 (precio y datos personales), D-45 (intermediario) | CA-P10-65, 66, 84 | UNIT, API |
| D-47 (WhatsApp de la ficha), D-77 (sin botón Compartir) | CA-P10-83 | UNIT, COMP |
| D-76, D-77 (sin terceros) | CA-P10-90 | MAN |
| D-81 (dominio) | CA-P10-58, 69, 81, 82 | API, BUILD, MAN |
| D-88 (vista previa) | CA-P10-70 a 82 | API (`P10VistaPreviaTests`), COMP, MAN |
| D-89 (indexación) | CA-P10-51 a 54 | API (`P10RobotsTests`) |
| D-90 (dirección del negocio) | CA-P10-62 | COMP |
| Encargo P10 punto 3 (sin `#`) | CA-P10-33 a 50 | UNIT, COMP, MAN |
| Encargo P10 punto 4 (sitemap) | CA-P10-51, 55 a 60 | API |
| Encargo P10 punto 5 (JSON-LD) | CA-P10-61 a 69 | UNIT, COMP, MAN |
| CA-P9-50 a 56 (SEO de P9) | CA-P10-14, 30, 51 (corrige CA-P9-54) | COMP, API |

---

## 7. Decisiones registradas (DECISIONES.md)

- **D-88 PROPUESTO** — Vista previa al compartir un vehículo. Recomendación: **A** (la API entrega el HTML de `/vehiculo/<slug>` con sus etiquetas), con C como base. Bloquea CA-P10-70 a 81.
- **D-89 PROPUESTO** — Desde cuándo se deja indexar. Recomendación: **A** (apagada por configuración hasta abrir al público). Bloquea CA-P10-52 a 54; se implementa con A si no hay respuesta.
- **D-90 PROPUESTO** — Dirección del negocio para Google. Recomendación: **C** (sin dirección ahora; `AutoDealer` cuando haya sede de atención). Bloquea CA-P10-62.
- **D-91 PROPUESTO** — Archivos del logo y favicon. Recomendación: recortar el JPEG ahora y pedir el original con transparencia; favicon con la cabeza del toro. Bloquea CA-P10-07 (solo el recorte).
- **D-92 PROPUESTO** — h1 de la portada. Recomendación: **B**. Bloquea CA-P10-20.
- Dentro de D-87 (TOMADA, "pendiente de afinar"): falta el visto bueno de la responsable al texto del `<title>` por defecto (CA-P10-14).

## 8. Notas para el arquitecto y QA

- **Corrección de P9:** `robots.txt` con `Disallow: /api/` impide que Google lea los datos con los que se pinta la ficha. CA-P10-51 lo corrige con `Allow: /api/catalogo/` y `Allow: /api/portal/`.
- **`Portal:Origen` no sirve como URL pública**: activa CORS para ese origen (`Program.cs:35`, `ConfiguracionServidor.cs:25`). Por eso se propone `Portal:UrlPublica`.
- **Pruebas afectadas:** 33 archivos de `frontend/tests` (585 apariciones de `#/` o `location.hash`; los que más: `P6Creditos` 84, `Catalogo` 68, `Filtros` 46, `P5bAvisos` 44, `P5Leads` 42, `P7Admin` 36). En el backend solo cambia la constante `RecuperacionEndpoints.RutaFrontend`; `P8RecuperacionTests` y `P8RecuperarAdminTests` usan `StartsWith(UrlPortalPruebas + RutaFrontend)` y siguen valiendo, pero conviene una prueba nueva que fije el formato exacto y que no haya `?codigo=`.
- Conviene hacer P10 en dos entregas dentro de la rama: (1) marca, textos, títulos y JSON-LD con el enrutado actual; (2) rutas sin `#`, robots, sitemap y vista previa. La (2) es la que más pruebas toca.
- Las fotos se sirven tal como se suben (hasta 5 MB, D-20). Para la vista previa de WhatsApp convendría más adelante una miniatura por vehículo (por ejemplo 1200 px de ancho y menos de 300 KB) generada al subir la foto. Queda fuera de P10.
- `Recuperacion:UrlPortal` por defecto es `http://localhost:5173`; con `Portal:UrlPublica` vacía, el sitemap local usa esa dirección, lo cual es correcto para probar.
- Los originales de `docs/marca/` están sin seguimiento en Git (`git status`). Que se suban o no al repositorio lo decide el coordinador; los derivados de `frontend/public/marca/` sí van en el repositorio.
