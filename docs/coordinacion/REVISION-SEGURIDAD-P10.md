# Revisión de seguridad P10 "Marca y visibilidad"

- Rama: `feature/p10-marca-visibilidad` (HEAD `0d6ad46`) contra `origin/main` (`2e23a8b`). 12 commits, 124 archivos.
- Fecha: 2026-11-16. Revisor: agente DevOps y seguridad.
- Alcance: `/vehiculo/{slug}` servido por la API (D-88 A), `/robots.txt` y `/sitemap.xml` (D-89 A), rutas sin `#` del frontend, enlace de recuperación `#codigo=`, reglas del proxy de `docs/GUIA-PUBLICACION.md`, `frontend/scripts/generar-marca.py` e imágenes de `frontend/public/marca`, dependencias.
- No se leyeron ni imprimieron valores de secretos. No se ejecutaron `dotnet test` ni `npm test` (los ejecuta QA).

## Veredicto

**APROBADO CON OBSERVACIONES.** No hay hallazgos altos y ninguno bloquea la entrega. El medio (M-1) solo aplica con la indexación encendida (apagada por defecto, D-89 A) y debe resolverse antes de poner `Portal__PermitirIndexacion=true`.

## Comprobaciones ejecutadas

| Comprobación | Resultado |
|---|---|
| `npm audit` (frontend) | 0 vulnerabilidades |
| `dotnet list Camion.Api.csproj package --vulnerable --include-transitive` | Sin paquetes vulnerables |
| `dotnet list Camion.Api.Tests.csproj package --vulnerable --include-transitive` | Sin paquetes vulnerables |
| Dependencias nuevas (csproj, package.json, package-lock.json) | Ninguna |
| Secretos, cadenas de conexión, tokens o llaves en el diff | Ninguno. `.env.example` solo añade nombres de claves sin valor |
| Respaldos, `.bak`, `.env` en el diff | Ninguno. `frontend/respaldos/` y `docs/marca/` siguen sin seguimiento |
| Datos personales en el diff | Solo el contacto comercial ya publicado (`ParametrosPortal.cs:23` en `main`) y datos ficticios de prueba |
| Metadatos de las imágenes de `frontend/public/marca` y `favicon.ico` | Sin EXIF, XMP ni texto: PNG solo con IHDR/PLTE/tRNS/IDAT/IEND (más `iCCP`, perfil de color, en `logo-horizontal.png`); WebP solo VP8/VP8X/ALPH; JPEG solo JFIF. Pesos dentro de los límites del script (máx. 58,8 KB `icon-512.png`, 54,9 KB `og-portal.jpg`) |
| Migraciones | Ninguna |

## Verificaciones sin hallazgo

- **Inyección en `/vehiculo/{slug}`**: todo valor de la base va a atributos y `<title>` con `HtmlEncoder.Create(UnicodeRanges.All)` (`backend/Camion.Api/Seo/FichaSeo.cs:38`, `:238-240`), que codifica `< > & " '`. El JSON-LD se serializa con `JavaScriptEncoder.Create(UnicodeRanges.All)` (`FichaSeo.cs:41-45`), que siempre escapa `< > & ' +` y los separadores U+2028/U+2029, así que no puede cerrar el `<script>`. Las pruebas cubren `</script>` y comillas (`backend/Camion.Api.Tests/P10SeoTests.cs:49`, `:56`, `:439-445`). En el navegador, el JSON-LD se escribe con `textContent` (`frontend/src/seo.ts:117`).
- **Slug**: solo se compara por igualdad en la consulta EF (`backend/Camion.Api/Endpoints/SeoEndpoints.cs:149-150`), nunca toca el sistema de archivos; tope de 200 caracteres (`SeoEndpoints.cs:65`). La canónica usa el slug guardado y `Uri.EscapeDataString` (`FichaSeo.cs:109`).
- **404 uniforme**: inexistente, no visible, vacío o demasiado largo devuelven el mismo cuerpo, estado y encabezados (`SeoEndpoints.cs:65-75`), con la misma consulta en los dos casos (sin diferencia de tiempo por existencia). La visibilidad sale de `ConsultasCatalogo.VisiblesConDestacado`, la misma regla de P3.
- **Encabezados**: el HTML lleva la CSP del frontend (`backend/Camion.Api/Seguridad/ConfiguracionServidor.cs` `CspFrontend`, aplicada en `backend/Camion.Api/Program.cs` en `OnStarting`), más `Referrer-Policy: no-referrer`, `nosniff` y `X-Frame-Options: DENY`; `X-Robots-Tag: noindex` en el 404 y con la indexación apagada; `Cache-Control: public, max-age=300` (la respuesta no depende de la sesión). Los bloques `application/ld+json` no se ejecutan, así que la CSP `default-src 'self'` no los bloquea ni los necesita.
- **Datos del asesor**: `DatosFicha` (`FichaSeo.cs:11-14`) no tiene asesor, placa, teléfono ni ids; el JSON-LD no lleva `seller` (`FichaSeo.cs:169-214`). La descripción comercial ya es pública en `GET /api/catalogo/publicaciones/{slug}`.
- **Plantilla**: la ruta viene solo de configuración (`Portal:RutaIndexHtml`), no de la petición; por petición solo se hace un `stat` y el texto se relee únicamente si cambian fecha o tamaño (`backend/Camion.Api/Seo/PlantillaPortal.cs:33-43`). Los errores no escriben la ruta en el log.
- **robots.txt / sitemap.xml**: con la indexación apagada, `Disallow: /` y sitemap 404 sin consultar la base. Encendida, el sitemap solo lista páginas públicas fijas, categorías con vehículos visibles y fichas de `ConsultasCatalogo.Visibles` (`SeoEndpoints.cs:94-141`); `/privacidad`, `/terminos`, `/cuenta`, `/inventario`, etc. no aparecen. Tope de 50.000 direcciones, en dos consultas (sin N+1). `XmlWriter` codifica los valores.
- **Autorización**: los tres endpoints son anónimos por diseño, solo GET/HEAD, y solo devuelven datos públicos. No se añadió ningún endpoint autenticado.
- **Enlace de recuperación**: el backend genera `UrlPortal + "/restablecer-contrasena#codigo=" + código` (`backend/Camion.Api/Endpoints/RecuperacionEndpoints.cs:30`, `:58`); el código va en el fragmento, que el navegador no envía ni al servidor ni en el `Referer`. El frontend solo acepta ese formato (`frontend/src/api/recuperacion.ts:37-39`), lee el código una vez y lo quita con `replaceState` sin dejar entrada en el historial (`frontend/src/components/RestablecerContrasena.tsx:28-33`, `:56-57`). El enlace viejo `#/restablecer-contrasena?codigo=` se convierte a `/restablecer-contrasena` guardando el código solo en memoria (`frontend/src/rutas.ts:370-374`): nunca pasa a la ruta ni a la consulta. `ubicacionActual()` excluye el fragmento (`rutas.ts:383-386`). La validación del código en `POST /api/auth/restablecer-contrasena` no cambió.
- **Intercepción de clics** (`frontend/src/rutas.ts:447-470`): solo mismo origen (`javascript:`, `mailto:` y otros dominios dan otro `origin` y no se interceptan), respeta modificadores, `target`, `download` y los prefijos reservados (`/api`, `/media`, `/health`, `/assets`…).
- **`generar-marca.py`**: lee solo de `docs/marca/` y escribe solo en `frontend/public/` (`frontend/scripts/generar-marca.py:17-22`); sin red, sin `subprocess`, sin argumentos externos. Pillow no guarda EXIF si no se le pasa `exif=`.

## Hallazgos

### Altos

Ninguno.

### Medios

**M-1. `/sitemap.xml` se recalcula en cada petición anónima, sin caché del servidor ni límite.**
`backend/Camion.Api/Endpoints/SeoEndpoints.cs:39-48` y `:94-141`. Con la indexación encendida, cada GET lee hasta 50.000 filas y arma el XML en memoria. `Cache-Control: public, max-age=3600` solo ayuda si hay una caché delante; el limitador global de `Program.cs` no cubre estas rutas. Un cliente que repita la petición carga la base. Con la indexación apagada (por defecto) responde 404 sin consultar, por eso no bloquea.
Recomendación: guardar el XML en `IMemoryCache` (10 a 60 min) o usar `AddOutputCache` con una política para `/sitemap.xml`; o, como mínimo, una partición del limitador por IP para `/sitemap.xml` y `/vehiculo/` (esta última hace 3 o 4 consultas por petición, igual que el detalle del catálogo). Resolver antes de `Portal__PermitirIndexacion=true`.

### Bajos

**B-1. Un enlace viejo `/#//otro-dominio` deja la página en blanco.**
`frontend/src/rutas.ts:363-379` y `:393`. `convertirHashViejo('#//evil.com')` devuelve `//evil.com` (también `#/\evil.com`, que el navegador trata igual), y `history.replaceState` con otro origen lanza `SecurityError`. Como se llama dentro del inicializador de `useState` (`rutas.ts:398-408`) y no hay límite de errores, React desmonta la aplicación. **No** es un redireccionamiento abierto (el navegador no navega), pero un enlace manipulado rompe el portal para quien lo abre.
Recomendación: en `convertirHashViejo`, si `camino` empieza por `//` o `/\`, devolver `/`; y envolver el `replaceState` de `convertirEnlaceViejo` en `try/catch`. Añadir una prueba con `#//evil.com` y `#/\evil.com`.

**B-2. `Portal:RutaIndexHtml` acepta cualquier archivo y de cualquier tamaño.**
`backend/Camion.Api/Seo/ConfiguracionPortal.cs:62-65` y `backend/Camion.Api/Seo/PlantillaPortal.cs:43`. Viene de configuración (no es explotable por un visitante), pero un error al configurarla (p. ej. apuntar a `appsettings.json`) haría que `/vehiculo/*` sirviera ese archivo tal cual, sin marcas. Tampoco hay tope de tamaño al leerlo.
Recomendación: exigir extensión `.html`, rechazar archivos de más de ~1 MB al arrancar y al releer, y responder 503 si la plantilla no tiene las dos marcas en lugar de servir el archivo completo.

**B-3. Reglas del proxy: `/api` y `/media` sin barra final y los archivos que faltan en `/marca/` caen en `index.html`.**
`docs/GUIA-PUBLICACION.md:334-335` y `:341-342`. `location ^~ /api/` no cubre `/api` ni `/media` exactos, que acaban en `try_files $uri /index.html` (200 HTML), en contra de lo que dice `:314`. Un `/marca/no-existe.png` o `/favicon-x.png` también devuelve `index.html` con 200 (la guía, en `:312`, los trata como archivos).
Recomendación: añadir `location = /api { return 404; }` y lo mismo para `/media` (o enviarlos a la API), y `location ^~ /marca/ { try_files $uri =404; … }` con los mismos encabezados de `/assets/`.

**B-4. Reglas del proxy: las `location` del frontend no envían HSTS.**
`docs/GUIA-PUBLICACION.md:326-348` y `:304`. El `add_header` dentro de `location` anula los del bloque `server`, así que un `Strict-Transport-Security` puesto en `server` no saldría en `/` ni en `/assets/`. Hoy el navegador lo recibe en la primera llamada a `/api`, pero el documento principal sale sin él.
Recomendación: añadir `add_header Strict-Transport-Security "max-age=2592000" always;` (o el valor vigente, 30 días según `:278`) en `location /` y `location ^~ /assets/` del ejemplo, y en el checklist de `:415` comprobarlo sobre `/` y no solo sobre la API.

**B-5. Falta una prueba de U+2028/U+2029 y `'` en el JSON-LD de la API.**
`backend/Camion.Api.Tests/P10SeoTests.cs:49-56`. El codificador ya los escapa (ver "Verificaciones sin hallazgo"), pero ninguna prueba lo fija; un cambio futuro de `JsonSerializerOptions` (`FichaSeo.cs:41-45`, p. ej. `UnsafeRelaxedJsonEscaping`) lo rompería sin aviso.
Recomendación: añadir al caso de XSS una descripción con ` `, ` ` y `'` y comprobar que el HTML contiene ` `, ` ` y `'` y no los caracteres crudos. Pedido para QA, que trabaja en las pruebas.

**B-6. `String.replace` con texto de configuración interpreta `$&`, `$'` y `` $` ``.**
`frontend/etiquetasHtml.ts:40`. Si `VITE_URL_PUBLICA` contuviera `$`, el HTML compilado saldría alterado. Solo se usa al compilar y la URL se valida (`:15-18`), así que el riesgo es mínimo.
Recomendación: `html.replace(marca, () => contenido)`.

## Notas para la publicación

- `docs/marca/` (sin seguimiento) contiene los originales, entre ellos una imagen exportada de WhatsApp. Si algún día se versionan, quitarles antes los metadatos; los derivados de `public/marca` ya están limpios.
- Tras publicar un frontend nuevo, las fichas cacheadas (`max-age=300`) pueden apuntar 5 minutos a los `assets` anteriores: conviene no borrar los `assets` viejos en el mismo momento del despliegue.
- Las reglas del proxy siguen como PROPUESTO (`docs/GUIA-PUBLICACION.md:445`); comprobar con el portal real los puntos de B-3 y B-4 y las verificaciones de `:423-428`.
