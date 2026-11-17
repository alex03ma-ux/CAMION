// Etiquetas para buscadores y redes en el <head> (P9: CA-P9-50, 55; P10: CA-P10-21 a 32, 61 a 68, 80, 82).
// Solo se tocan las etiquetas marcadas con `data-camion="<nombre>"`: las del bloque `<!--camion:meta:…-->` de
// index.html y las que inserta la API en `/vehiculo/<slug>` (D-88 A) usan los mismos nombres, así que al navegar se
// reutilizan o se reemplazan, nunca se duplican (CA-P10-28, 80). El token nunca aparece aquí.
import { DESCRIPCION_POR_DEFECTO, NOMBRE_SITIO } from './marca'

/** Contenido de `<meta name="robots">`; null = sin etiqueta (indexable). */
export type Robots = null | 'noindex' | 'noindex, follow'

export type MetaPagina = {
  titulo: string
  /** null = descripción por defecto (CA-P10-32). */
  descripcion: string | null
  robots: Robots
  /** Ruta canónica (`/camiones`), solo en páginas indexables (CA-P10-27). */
  canonica: string | null
  /** Ruta o URL de la imagen para compartir; null = la genérica del portal (si hay URL pública). */
  imagen?: string | null
  imagenAlt?: string | null
}

/**
 * D-89 A: mientras el portal no se abra al público, todas las páginas llevan `noindex` (además del `robots.txt` con
 * `Disallow: /` que entrega la API). Se enciende compilando con `VITE_PERMITIR_INDEXACION=true`, en el mismo paso
 * en que se pone `Portal__PermitirIndexacion=true` en la API (docs/GUIA-PUBLICACION.md).
 */
export function indexacionPermitida(): boolean {
  return import.meta.env.VITE_PERMITIR_INDEXACION === 'true'
}

/** URL pública del portal para las imágenes de vista previa (CA-P10-82); '' si no está configurada con https. */
export function urlPublica(): string {
  const valor = (import.meta.env.VITE_URL_PUBLICA ?? '').trim().replace(/\/+$/, '')
  return /^https:\/\/[^\s/?#]+$/.test(valor) ? valor : ''
}

/** Origen para `canonical`, `og:url` y JSON-LD: el de la página abierta (CA-P10-27). */
export function origenActual(): string {
  return typeof window === 'undefined' ? '' : window.location.origin
}

/** Absoluta a partir de una ruta (`/marca/x.png` → `https://dominio/marca/x.png`). */
export function absoluta(ruta: string, origen = origenActual()): string {
  return /^https?:\/\//.test(ruta) ? ruta : `${origen}${ruta}`
}

function porNombre(nombre: string): Element | null {
  if (typeof document === 'undefined') return null
  return document.head.querySelector(`[data-camion="${nombre}"]`)
}

/** `<meta name|property=… content=…>` con su marca; `null` la quita. */
function fijarMeta(atributo: 'name' | 'property', nombre: string, valor: string | null, marca = nombre): void {
  if (typeof document === 'undefined') return
  const existente = porNombre(marca)
  if (valor === null) {
    existente?.remove()
    return
  }
  const meta = existente ?? document.createElement('meta')
  meta.setAttribute(atributo, nombre)
  meta.setAttribute('content', valor)
  meta.setAttribute('data-camion', marca)
  if (!existente) document.head.appendChild(meta)
}

/** P9: `<meta name="robots" content="noindex">` (marca "noindex", la misma que usa la API). */
export function fijarRobots(valor: Robots): void {
  fijarMeta('name', 'robots', valor, 'noindex')
}

/** Compatibilidad con P9: activa o quita el `noindex`. */
export function fijarNoIndex(activo: boolean): void {
  fijarRobots(activo ? 'noindex' : null)
}

/** `<link rel="canonical">`; null la quita. */
export function fijarCanonica(url: string | null): void {
  if (typeof document === 'undefined') return
  const existente = porNombre('canonical')
  if (url === null) {
    existente?.remove()
    return
  }
  const enlace = existente ?? document.createElement('link')
  enlace.setAttribute('rel', 'canonical')
  enlace.setAttribute('href', url)
  enlace.setAttribute('data-camion', 'canonical')
  if (!existente) document.head.appendChild(enlace)
}

/**
 * CA-P10-68: JSON para un `<script type="application/ld+json">` con `<`, `>` y `&` escapados (`<`…): un texto
 * del asesor como `</script>` no puede cerrar el bloque y el JSON sigue siendo válido.
 */
export function jsonLdSeguro(datos: unknown): string {
  return JSON.stringify(datos)
    .replace(/</g, '\\u003c')
    .replace(/>/g, '\\u003e')
    .replace(/&/g, '\\u0026')
    .replace(/\u2028/g, '\\u2028')
    .replace(/\u2029/g, '\\u2029')
}

/** Bloque JSON-LD con su marca (`jsonld-organizacion`, `jsonld-vehiculo`, `jsonld-migas`); null lo quita. */
export function fijarJsonLd(marca: string, datos: unknown | null): void {
  if (typeof document === 'undefined') return
  const existente = porNombre(marca)
  if (datos === null) {
    existente?.remove()
    return
  }
  const script = existente ?? document.createElement('script')
  script.setAttribute('type', 'application/ld+json')
  script.setAttribute('data-camion', marca)
  // Siempre con textContent, nunca con innerHTML (CA-P10-68).
  script.textContent = jsonLdSeguro(datos)
  if (!existente) document.head.appendChild(script)
}

export const JSONLD_ORGANIZACION = 'jsonld-organizacion'
export const JSONLD_VEHICULO = 'jsonld-vehiculo'
export const JSONLD_MIGAS = 'jsonld-migas'

/**
 * Aplica título, descripción, robots, canonical y las etiquetas de vista previa de una página. Con la indexación
 * apagada (D-89 A) todo lleva `noindex` y no hay canonical.
 */
export function aplicarMetaPagina(meta: MetaPagina): void {
  if (typeof document === 'undefined') return
  const indexar = indexacionPermitida()
  const robots: Robots = indexar ? meta.robots : 'noindex'
  const canonica = indexar && robots === null && meta.canonica !== null ? absoluta(meta.canonica) : null
  const descripcion = meta.descripcion ?? DESCRIPCION_POR_DEFECTO
  document.title = meta.titulo
  fijarMeta('name', 'description', descripcion)
  fijarRobots(robots)
  fijarCanonica(canonica)
  // Vista previa (CA-P10-71, 82): las redes no ejecutan JavaScript, pero así el documento queda coherente al navegar.
  const publica = urlPublica()
  const imagen = meta.imagen ? absoluta(meta.imagen, publica || origenActual()) : publica ? `${publica}/marca/og-portal.jpg` : null
  fijarMeta('property', 'og:type', 'website')
  fijarMeta('property', 'og:site_name', NOMBRE_SITIO)
  fijarMeta('property', 'og:locale', 'es_CO')
  fijarMeta('property', 'og:title', meta.titulo)
  fijarMeta('property', 'og:description', descripcion)
  fijarMeta('property', 'og:url', canonica)
  fijarMeta('property', 'og:image', imagen)
  fijarMeta('property', 'og:image:alt', meta.imagen ? (meta.imagenAlt ?? meta.titulo) : null)
  fijarMeta('name', 'twitter:card', 'summary_large_image')
  fijarMeta('name', 'twitter:title', meta.titulo)
  fijarMeta('name', 'twitter:description', descripcion)
  fijarMeta('name', 'twitter:image', imagen)
}
