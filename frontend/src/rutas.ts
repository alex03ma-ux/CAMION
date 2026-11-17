// Navegación por la dirección (P10, CA-P10-33 a 46): rutas sin `#` con la History API, sin dependencias.
// Hasta P9 las rutas vivían en el hash (`#/buscar?…`); los enlaces viejos se convierten al abrirlos (CA-P10-41).
//   /                       Inicio
//   /vehiculos?…            Resultados (filtros en la consulta: se comparten y funcionan con Atrás/Adelante)
//   /carros, /camiones, /carrocerias, /maquinaria (con filtros opcionales en la consulta)
//                           P10 (HU-005, HU-006, D-17): resultados de una categoría
//   /vehiculo/<slug>        Detalle (con D-88 A, en producción el HTML lo entrega la API con sus etiquetas)
//   /cuenta                 Login / registro / perfil / panel de administración
//   /cuenta/registro        Lo mismo, abriendo el formulario de registro
//   /cuenta/<seccion>?…     P4p, P7, P8: secciones del administrador (la API las protege igualmente)
//   /inventario, /inventario/nuevo, /inventario/<id>     P4a
//   /publicaciones          P4b
//   /contactos, /contactos/<id>                          P5
//   /privacidad, /terminos  P5, P9 (D-44): en revisión legal
//   /vende-tu-camion, /seguro-todo-riesgo                P5b (D-6, D-51)
//   /credito, /credito/<slug>, /busco-un-vehiculo        P6
//   /mis-solicitudes, /ventas, /mis-indicadores          P6, P7, P8
//   /restablecer-contrasena#codigo=…                     P8 (D-73 D), P10 (CA-P10-43): el código va en el fragmento,
//                           que el navegador no envía al servidor; la pantalla lo lee una vez y lo quita de la URL.
// Cualquier otra ruta muestra "Página no encontrada" (CA-P9-48). Nunca se pone el token en la URL.
import { useEffect, useState } from 'react'
import {
  categoriasDisponibles,
  CATEGORIAS,
  CLAVES_FILTROS,
  FILTROS_VACIOS,
  type FiltrosCatalogo,
  type OpcionCatalogo,
  type SlugCategoria,
} from './api/catalogo'
import { guardarCodigoRecuperacion } from './api/recuperacion'

export type Ruta =
  | { nombre: 'inicio' }
  /**
   * P10: `categoria` = ruta de categoría (`/camiones`) o null (`/vehiculos`). En una categoría, `filtros.tipoId` es el
   * id del tipo según `/api/catalogo/filtros`, o '' mientras no se conoce o si la categoría no tiene vehículos.
   */
  | { nombre: 'buscar'; filtros: FiltrosCatalogo; categoria: SlugCategoria | null }
  | { nombre: 'vehiculo'; slug: string }
  /** P7: `consulta` = filtros iniciales de la sección (enlaces del panel), solo si no está vacía. */
  | { nombre: 'cuenta'; registro: boolean; seccion?: SeccionAdmin; consulta?: string }
  | { nombre: 'inventario'; consulta?: string }
  | { nombre: 'inventarioNuevo' }
  | { nombre: 'inventarioVehiculo'; id: number }
  | { nombre: 'publicaciones' }
  | { nombre: 'contactos'; consulta?: string }
  | { nombre: 'contacto'; id: number }
  | { nombre: 'privacidad' }
  /** P9 (CA-P9-17, 23). */
  | { nombre: 'terminos' }
  | { nombre: 'vendeTuCamion' }
  | { nombre: 'seguroTodoRiesgo' }
  /** P6: `slug` null = crédito desde la portada. */
  | { nombre: 'credito'; slug: string | null }
  | { nombre: 'buscoVehiculo' }
  | { nombre: 'misSolicitudes' }
  /** P7: "Mis ventas" del asesor (el administrador ve todas). */
  | { nombre: 'ventas'; consulta?: string }
  /** P8 (D-70 A): "Mis indicadores" del asesor. */
  | { nombre: 'misIndicadores' }
  /** P8 (D-73 D): el código no viaja en la ruta; la pantalla lo lee una vez del fragmento y lo quita de la URL. */
  | { nombre: 'restablecerContrasena' }
  /** P9 (CA-P9-48): ruta desconocida. */
  | { nombre: 'noEncontrada' }

/** P4p: secciones del administrador dentro de "Mi cuenta". P7: inicio, ventas y parámetros. */
export type SeccionAdmin =
  | 'inicio'
  | 'usuarios'
  | 'planes'
  | 'suscripciones'
  | 'ventas'
  | 'catalogos'
  | 'portada'
  | 'parametros'
  | 'reportes'
  | 'indicadores'
export const SECCIONES_ADMIN: readonly SeccionAdmin[] = [
  'inicio',
  'usuarios',
  'planes',
  'suscripciones',
  'ventas',
  'catalogos',
  'portada',
  'parametros',
  'reportes',
  'indicadores',
]

/** `/cuenta/<seccion>`; P7: con filtros iniciales opcionales (`?clave=valor`). */
export function rutaSeccionAdmin(seccion: SeccionAdmin, filtros: Record<string, string> = {}): string {
  return conConsulta(`/cuenta/${seccion}`, filtros)
}

/** Añade `?clave=valor` con los valores no vacíos. */
export function conConsulta(ruta: string, filtros: Record<string, string>): string {
  const p = new URLSearchParams()
  for (const [clave, valor] of Object.entries(filtros)) if (valor !== '') p.set(clave, valor)
  const consulta = p.toString()
  return consulta === '' ? ruta : `${ruta}?${consulta}`
}

export const RUTA_INICIO = '/'
export const RUTA_BUSCAR = '/vehiculos'
export const RUTA_CUENTA = '/cuenta'
export const RUTA_REGISTRO = '/cuenta/registro'
export const RUTA_INVENTARIO = '/inventario'
export const RUTA_INVENTARIO_NUEVO = '/inventario/nuevo'
export const RUTA_PUBLICACIONES = '/publicaciones'
export const RUTA_CONTACTOS = '/contactos'
export const RUTA_PRIVACIDAD = '/privacidad'
export const RUTA_TERMINOS = '/terminos'
export const RUTA_VENDE_TU_CAMION = '/vende-tu-camion'
export const RUTA_SEGURO_TODO_RIESGO = '/seguro-todo-riesgo'
export const RUTA_CREDITO = '/credito'
export const RUTA_BUSCO_VEHICULO = '/busco-un-vehiculo'
export const RUTA_MIS_SOLICITUDES = '/mis-solicitudes'
export const RUTA_VENTAS = '/ventas'
export const RUTA_MIS_INDICADORES = '/mis-indicadores'
export const RUTA_RESTABLECER = '/restablecer-contrasena'

/**
 * CA-P10-46: prefijos reservados para el servidor o para archivos estáticos. Ninguna ruta del portal empieza por
 * ellos y los enlaces que los usan no se interceptan (los atiende el navegador).
 */
export const PREFIJOS_RESERVADOS = [
  '/api',
  '/media',
  '/health',
  '/assets',
  '/marca',
  '/sitemap.xml',
  '/robots.txt',
  '/favicon',
  '/site.webmanifest',
] as const

/** true si la ruta (`/api/x`, `/health`) empieza por un prefijo reservado (segmento completo o archivo). */
export function esRutaReservada(camino: string): boolean {
  return PREFIJOS_RESERVADOS.some(
    (p) => camino === p || camino.startsWith(`${p}/`) || (p === '/favicon' && camino.startsWith(p)),
  )
}

/** `/credito/<slug>`: crédito para un vehículo, desde su ficha (CA-P6-03). */
export function rutaCreditoVehiculo(slug: string): string {
  return `${RUTA_CREDITO}/${encodeURIComponent(slug)}`
}

/** `/contactos/<id>`. */
export function rutaContacto(id: number): string {
  return `${RUTA_CONTACTOS}/${id}`
}

/** true si la ruta es la lista o el detalle de los contactos (exigen sesión). */
export function esRutaContactos(ruta: Ruta): boolean {
  return ruta.nombre === 'contactos' || ruta.nombre === 'contacto'
}

/** `/inventario/<id>` (id entero positivo). */
export function rutaInventarioVehiculo(id: number): string {
  return `${RUTA_INVENTARIO}/${id}`
}

/** true si la ruta es una de las pantallas del inventario (exigen sesión). */
export function esRutaInventario(ruta: Ruta): boolean {
  return (
    ruta.nombre === 'inventario' ||
    ruta.nombre === 'inventarioNuevo' ||
    ruta.nombre === 'inventarioVehiculo'
  )
}

const INICIO: Ruta = { nombre: 'inicio' }
const NO_ENCONTRADA: Ruta = { nombre: 'noEncontrada' }

/**
 * P9 (CA-P9-50, 55), P10 (CA-P10-30): pantallas que piden `noindex` siempre: las privadas (cuenta, inventario,
 * publicaciones, contactos, ventas, mis solicitudes, mis indicadores), restablecer contraseña, el crédito de un
 * vehículo y la página no encontrada.
 */
export function rutaSinIndexar(ruta: Ruta): boolean {
  switch (ruta.nombre) {
    case 'cuenta':
    case 'inventario':
    case 'inventarioNuevo':
    case 'inventarioVehiculo':
    case 'publicaciones':
    case 'contactos':
    case 'contacto':
    case 'ventas':
    case 'misSolicitudes':
    case 'misIndicadores':
    case 'restablecerContrasena':
    case 'noEncontrada':
      return true
    case 'credito':
      return ruta.slug !== null
    default:
      return false
  }
}

// ---- Categorías (CA-P10-39) ----

/**
 * Tipos de `/api/catalogo/filtros` que conoce la aplicación: traducen `/camiones` ↔ `tipoId`. Los fija App al cargar
 * las opciones; mientras no se conocen, una categoría se lee con `tipoId` vacío y los enlaces van a `/vehiculos`.
 */
let tiposConocidos: readonly OpcionCatalogo[] = []

export function fijarTiposCatalogo(tipos: readonly OpcionCatalogo[]): void {
  tiposConocidos = tipos
}

/** Solo para pruebas: olvida los tipos conocidos. */
export function olvidarTiposCatalogo(): void {
  tiposConocidos = []
}

/** Categoría (y su id) a la que corresponde un `tipoId`, si es una de las cuatro principales. */
function categoriaDeTipo(tipoId: string): SlugCategoria | null {
  if (tipoId === '') return null
  return categoriasDisponibles(tiposConocidos).find((c) => String(c.tipo.id) === tipoId)?.slug ?? null
}

/** Id del tipo de una categoría; '' si aún no se conoce o si no tiene vehículos visibles. */
export function tipoDeCategoria(slug: SlugCategoria): string {
  const encontrada = categoriasDisponibles(tiposConocidos).find((c) => c.slug === slug)
  return encontrada ? String(encontrada.tipo.id) : ''
}

/** `/camiones` (con filtros opcionales, sin `tipoId`). */
export function rutaCategoria(slug: SlugCategoria, filtros: Partial<FiltrosCatalogo> = {}): string {
  return `/${slug}${consultaFiltros({ ...filtros, tipoId: '' })}`
}

/** Lee los filtros conocidos de la cadena de consulta; ignora los demás. */
export function leerFiltros(consulta: string): FiltrosCatalogo {
  const params = new URLSearchParams(consulta)
  const filtros: FiltrosCatalogo = { ...FILTROS_VACIOS }
  for (const clave of CLAVES_FILTROS) {
    filtros[clave] = (params.get(clave) ?? '').trim()
  }
  return filtros
}

/** `?…` con los filtros no vacíos (sin `pagina=1`, que es la de defecto); '' si no hay. */
function consultaFiltros(filtros: Partial<FiltrosCatalogo>): string {
  const params = new URLSearchParams()
  for (const clave of CLAVES_FILTROS) {
    const valor = (filtros[clave] ?? '').trim()
    if (valor === '' || (clave === 'pagina' && valor === '1')) continue
    params.set(clave, valor)
  }
  const consulta = params.toString()
  return consulta === '' ? '' : `?${consulta}`
}

/**
 * Dirección de una búsqueda. Si el tipo es una de las categorías principales, la de la categoría (`/camiones?…`,
 * CA-P10-39); si no, `/vehiculos?…`.
 */
export function rutaBuscar(filtros: Partial<FiltrosCatalogo> = {}): string {
  const categoria = categoriaDeTipo((filtros.tipoId ?? '').trim())
  if (categoria !== null) return rutaCategoria(categoria, filtros)
  return `${RUTA_BUSCAR}${consultaFiltros(filtros)}`
}

export function rutaVehiculo(slug: string): string {
  return `/vehiculo/${encodeURIComponent(slug)}`
}

/**
 * Convierte una dirección (`/vehiculos?marcaId=3`, `pathname + search`) en una ruta (CA-P10-34). Acepta barra final;
 * distingue mayúsculas; un fragmento (`#…`) se ignora.
 */
export function leerRuta(direccion: string): Ruta {
  const sinFragmento = direccion.split('#', 1)[0]
  const corte = sinFragmento.indexOf('?')
  const crudo = corte === -1 ? sinFragmento : sinFragmento.slice(0, corte)
  const camino = crudo.replace(/\/+$/, '')
  const consulta = corte === -1 ? '' : sinFragmento.slice(corte + 1)

  if (camino === '') return INICIO
  if (camino === RUTA_BUSCAR) return { nombre: 'buscar', filtros: leerFiltros(consulta), categoria: null }
  const categoria = CATEGORIAS.find((c) => camino === `/${c.slug}`)
  if (categoria) {
    return {
      nombre: 'buscar',
      filtros: { ...leerFiltros(consulta), tipoId: tipoDeCategoria(categoria.slug) },
      categoria: categoria.slug,
    }
  }
  if (camino === '/cuenta') return { nombre: 'cuenta', registro: false }
  if (camino === '/cuenta/registro') return { nombre: 'cuenta', registro: true }
  const seccion = SECCIONES_ADMIN.find((s) => camino === `/cuenta/${s}`)
  if (seccion) return { nombre: 'cuenta', registro: false, seccion, ...conFiltros(consulta) }
  if (camino === '/inventario') return { nombre: 'inventario', ...conFiltros(consulta) }
  if (camino === '/inventario/nuevo') return { nombre: 'inventarioNuevo' }
  if (camino === '/publicaciones') return { nombre: 'publicaciones' }
  if (camino === '/contactos') return { nombre: 'contactos', ...conFiltros(consulta) }
  if (camino === '/ventas') return { nombre: 'ventas', ...conFiltros(consulta) }
  if (camino === '/privacidad') return { nombre: 'privacidad' }
  if (camino === '/terminos') return { nombre: 'terminos' }
  if (camino === '/vende-tu-camion') return { nombre: 'vendeTuCamion' }
  if (camino === '/seguro-todo-riesgo') return { nombre: 'seguroTodoRiesgo' }
  if (camino === '/credito') return { nombre: 'credito', slug: null }
  if (camino === '/busco-un-vehiculo') return { nombre: 'buscoVehiculo' }
  if (camino === '/mis-solicitudes') return { nombre: 'misSolicitudes' }
  if (camino === '/mis-indicadores') return { nombre: 'misIndicadores' }
  if (camino === RUTA_RESTABLECER) return { nombre: 'restablecerContrasena' }
  const contacto = /^\/contactos\/(\d{1,15})$/.exec(camino)
  if (contacto) {
    const id = Number(contacto[1])
    if (Number.isSafeInteger(id) && id > 0) return { nombre: 'contacto', id }
  }
  const inventario = /^\/inventario\/(\d{1,15})$/.exec(camino)
  if (inventario) {
    const id = Number(inventario[1])
    if (Number.isSafeInteger(id) && id > 0) return { nombre: 'inventarioVehiculo', id }
  }
  const vehiculo = /^\/vehiculo\/([^/]+)$/.exec(camino)
  if (vehiculo) {
    const slug = decodificar(vehiculo[1])
    if (slug !== null) return { nombre: 'vehiculo', slug }
  }
  const credito = /^\/credito\/([^/]+)$/.exec(camino)
  if (credito) {
    const slug = decodificar(credito[1])
    if (slug !== null) return { nombre: 'credito', slug }
  }
  return NO_ENCONTRADA
}

/** P7: `{ consulta }` solo si hay filtros (las rutas sin consulta no cambian). */
function conFiltros(consulta: string): { consulta?: string } {
  return consulta.trim() === '' ? {} : { consulta }
}

/** Slug decodificado y no vacío; null si la codificación no es válida (se trata como ruta desconocida). */
function decodificar(valor: string): string | null {
  try {
    const slug = decodeURIComponent(valor)
    return slug.trim() !== '' ? slug : null
  } catch {
    return null
  }
}

// ---- Compatibilidad con los enlaces de antes de P10 (CA-P10-41, 42) ----

/**
 * Dirección sin `#` equivalente a un hash viejo (`#/buscar?marcaId=3` → `/vehiculos?marcaId=3`); null si el hash no
 * empieza por `#/` (p. ej. `#contenido` o `#codigo=…`, que no se tocan). Un `#/…` desconocido da su ruta tal cual, que
 * se mostrará como "no encontrada".
 *
 * El enlace viejo de recuperación (`#/restablecer-contrasena?codigo=…`) da `/restablecer-contrasena` sin el código:
 * el código se guarda en memoria (nunca pasa a la ruta ni a la consulta, ni siquiera de forma transitoria).
 */
export function convertirHashViejo(hash: string): string | null {
  if (!hash.startsWith('#/')) return null
  const resto = hash.slice(1)
  const corte = resto.indexOf('?')
  const camino = corte === -1 ? resto : resto.slice(0, corte)
  const consulta = corte === -1 ? '' : resto.slice(corte)
  const caminoSinBarra = camino.replace(/\/+$/, '')
  if (caminoSinBarra === RUTA_RESTABLECER) {
    const codigo = new URLSearchParams(consulta).get('codigo')
    if (codigo !== null && codigo.trim() !== '') guardarCodigoRecuperacion(codigo.trim())
    return RUTA_RESTABLECER
  }
  if (caminoSinBarra === '/buscar') {
    // `#/buscar?tipoId=N` lleva a la categoría si los tipos ya se conocen; si no, App lo redirige al conocerlos.
    return rutaBuscar(leerFiltros(consulta))
  }
  // B-1 / QA-P10-1: `#//otro-dominio` o `#/\otro` darían una dirección de otro origen (el navegador trata `/\` como
  // `//`); replaceState lanzaría SecurityError. Se lleva a la portada.
  if (/^\/[/\\]/.test(camino)) return RUTA_INICIO
  return `${camino === '' ? '/' : camino}${consulta}`
}

/** Dirección actual (`pathname + search`), sin el fragmento. */
export function ubicacionActual(): string {
  if (typeof window === 'undefined') return RUTA_INICIO
  return `${window.location.pathname}${window.location.search}`
}

/**
 * Reemplaza la dirección sin recargar. Nunca lanza (B-1): si el navegador rechaza la dirección, intenta la portada y,
 * si tampoco puede, deja la dirección como está; la página nunca queda en blanco.
 */
function reemplazarSeguro(direccion: string): void {
  try {
    window.history.replaceState(window.history.state, '', direccion)
  } catch {
    try {
      window.history.replaceState(window.history.state, '', RUTA_INICIO)
    } catch {
      // Se deja la dirección actual.
    }
  }
}

/** Si la dirección trae un hash viejo `#/…`, la reemplaza por su equivalente sin `#` (sin recargar). */
export function convertirEnlaceViejo(): void {
  if (typeof window === 'undefined') return
  const nueva = convertirHashViejo(window.location.hash)
  if (nueva === null) return
  reemplazarSeguro(nueva)
}

/**
 * O-1: una barra final (`/vehiculo/x/`, `/camiones/`) se quita con replaceState, para que la dirección visible sea la
 * misma que la canónica, la del sitemap y la que responde la API. Se conservan la consulta y el fragmento.
 */
export function quitarBarraFinal(): void {
  if (typeof window === 'undefined') return
  const { pathname, search, hash } = window.location
  if (pathname.length <= 1 || !pathname.endsWith('/')) return
  const limpio = pathname.replace(/\/+$/, '')
  // `//x/` sin barra final sería `//x`, dirección de otro origen: se deja la portada.
  reemplazarSeguro(limpio === '' || limpio.startsWith('//') ? RUTA_INICIO : `${limpio}${search}${hash}`)
}

const EVENTO_NAVEGACION = 'camion:navegacion'

function leerUbicacion(): string {
  convertirEnlaceViejo()
  quitarBarraFinal()
  return ubicacionActual()
}

/**
 * Dirección actual; se actualiza con `navegar`, con los enlaces internos y con Atrás/Adelante (`popstate`). Un hash
 * viejo que llegue después (pegado en la barra de direcciones) también se convierte (`hashchange`).
 */
export function useUbicacion(): string {
  const [ubicacion, setUbicacion] = useState(leerUbicacion)
  useEffect(() => {
    const alCambiar = () => setUbicacion(leerUbicacion())
    window.addEventListener('popstate', alCambiar)
    window.addEventListener('hashchange', alCambiar)
    window.addEventListener(EVENTO_NAVEGACION, alCambiar)
    // Por si cambió entre el primer render y la suscripción.
    alCambiar()
    return () => {
      window.removeEventListener('popstate', alCambiar)
      window.removeEventListener('hashchange', alCambiar)
      window.removeEventListener(EVENTO_NAVEGACION, alCambiar)
    }
  }, [])
  return ubicacion
}

/** Ruta actual. */
export function useRuta(): Ruta {
  return leerRuta(useUbicacion())
}

/**
 * Navega a una dirección del portal sin recargar (CA-P10-36): `pushState` (o `replaceState` con `reemplazar`) y aviso
 * a `useUbicacion`. Si ya se está en esa dirección, no hace nada.
 */
export function navegar(direccion: string, { reemplazar = false }: { reemplazar?: boolean } = {}): void {
  if (`${window.location.pathname}${window.location.search}${window.location.hash}` === direccion) return
  if (reemplazar) window.history.replaceState(null, '', direccion)
  else window.history.pushState(null, '', direccion)
  window.dispatchEvent(new Event(EVENTO_NAVEGACION))
}

/**
 * CA-P10-36: un clic normal en un enlace interno cambia de pantalla sin recargar. Con Ctrl, Cmd, Mayús, Alt, clic
 * central, `target` o `download`, el navegador hace lo de siempre. No se interceptan otros orígenes, otros esquemas
 * (`mailto:`, `tel:`), los prefijos reservados (`/api`, `/media`, `/health`…) ni los enlaces a un ancla de la misma
 * página (`#contenido`). Devuelve la función que quita el oyente.
 */
export function instalarNavegacionEnlaces(): () => void {
  const alHacerClic = (evento: MouseEvent) => {
    if (evento.defaultPrevented || evento.button !== 0) return
    if (evento.metaKey || evento.ctrlKey || evento.shiftKey || evento.altKey) return
    const destino = evento.target instanceof Element ? evento.target.closest('a') : null
    if (!destino || !destino.hasAttribute('href') || destino.hasAttribute('download')) return
    const target = destino.getAttribute('target')
    if (target !== null && target !== '' && target !== '_self') return
    const href = destino.getAttribute('href') ?? ''
    if (href.startsWith('#')) return
    let url: URL
    try {
      url = new URL(destino.href, window.location.href)
    } catch {
      return
    }
    if (url.origin !== window.location.origin) return
    if (esRutaReservada(url.pathname)) return
    evento.preventDefault()
    navegar(`${url.pathname}${url.search}${url.hash}`)
  }
  document.addEventListener('click', alHacerClic)
  return () => document.removeEventListener('click', alHacerClic)
}

// ---- Última búsqueda, para "Volver a resultados" ----

let ultimaBusqueda: string | null = null

export function recordarBusqueda(direccion: string): void {
  ultimaBusqueda = direccion
}

/** Dirección de la última búsqueda vista o, si no hubo, la del inicio. */
export function rutaVolverAResultados(): string {
  return ultimaBusqueda ?? RUTA_INICIO
}

/** Solo para pruebas: olvida la última búsqueda. */
export function olvidarUltimaBusqueda(): void {
  ultimaBusqueda = null
}
