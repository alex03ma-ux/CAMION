// Navegación por el hash de la URL (P3), sin dependencias.
//   #/                  Inicio
//   #/buscar?…          Resultados (filtros en la URL: se comparten y funcionan con Atrás/Adelante)
//   #/vehiculo/<slug>   Detalle
//   #/cuenta            Login / registro / perfil / panel de administración
//   #/cuenta/registro   Lo mismo, abriendo el formulario de registro
//   #/cuenta/usuarios, #/cuenta/planes, #/cuenta/suscripciones, #/cuenta/catalogos
//                       P4p: secciones del administrador (la API las protege igualmente)
//   #/cuenta/portada    D-42: foto de portada del portal (administrador)
//   #/inventario        P4a: inventario del asesor (propio) o del administrador (general)
//   #/inventario/nuevo  P4a: registrar un vehículo
//   #/inventario/<id>   P4a: editar un vehículo y sus fotos
//   #/publicaciones     P4b: "Mis publicaciones" del asesor (el administrador ve todas)
//   #/contactos         P5: contactos (leads) del asesor; el administrador ve todos
//   #/contactos/<id>    P5: detalle de un contacto con su historial
//   #/privacidad        P5 (D-44): política de tratamiento de datos (borrador en revisión legal)
//   #/vende-tu-camion   P5b (HU-N01, D-51): aviso "Vende tu camión", sin cuenta
//   #/seguro-todo-riesgo P5b (HU-N02, D-51): aviso "Compra tu seguro todo riesgo", sin cuenta
//   #/credito           P6 (HU-039, D-42, D-54): solicitud de crédito desde la portada, sin cuenta
//   #/credito/<slug>    P6 (HU-039): solicitud de crédito para un vehículo, desde su ficha
//   #/busco-un-vehiculo P6 (HU-034, D-56): búsqueda "Busco un vehículo", sin cuenta
//   #/mis-solicitudes   P6 (D-53, D-59): "Mis solicitudes" del comprador (la API la protege con SoloComprador)
// Cualquier otra ruta muestra el inicio. Nunca se pone el token en la URL.
import { useEffect, useState } from 'react'
import { CLAVES_FILTROS, FILTROS_VACIOS, type FiltrosCatalogo } from './api/catalogo'

export type Ruta =
  | { nombre: 'inicio' }
  | { nombre: 'buscar'; filtros: FiltrosCatalogo }
  | { nombre: 'vehiculo'; slug: string }
  | { nombre: 'cuenta'; registro: boolean; seccion?: SeccionAdmin }
  | { nombre: 'inventario' }
  | { nombre: 'inventarioNuevo' }
  | { nombre: 'inventarioVehiculo'; id: number }
  | { nombre: 'publicaciones' }
  | { nombre: 'contactos' }
  | { nombre: 'contacto'; id: number }
  | { nombre: 'privacidad' }
  | { nombre: 'vendeTuCamion' }
  | { nombre: 'seguroTodoRiesgo' }
  /** P6: `slug` null = crédito desde la portada. */
  | { nombre: 'credito'; slug: string | null }
  | { nombre: 'buscoVehiculo' }
  | { nombre: 'misSolicitudes' }

/** P4p: secciones del administrador dentro de "Mi cuenta". */
export type SeccionAdmin = 'usuarios' | 'planes' | 'suscripciones' | 'catalogos' | 'portada'
export const SECCIONES_ADMIN: readonly SeccionAdmin[] = ['usuarios', 'planes', 'suscripciones', 'catalogos', 'portada']

/** `#/cuenta/<seccion>`. */
export function hashSeccionAdmin(seccion: SeccionAdmin): string {
  return `#/cuenta/${seccion}`
}

export const HASH_INICIO = '#/'
export const HASH_BUSCAR = '#/buscar'
export const HASH_CUENTA = '#/cuenta'
export const HASH_REGISTRO = '#/cuenta/registro'
export const HASH_INVENTARIO = '#/inventario'
export const HASH_INVENTARIO_NUEVO = '#/inventario/nuevo'
export const HASH_PUBLICACIONES = '#/publicaciones'
export const HASH_CONTACTOS = '#/contactos'
export const HASH_PRIVACIDAD = '#/privacidad'
export const HASH_VENDE_TU_CAMION = '#/vende-tu-camion'
export const HASH_SEGURO_TODO_RIESGO = '#/seguro-todo-riesgo'
export const HASH_CREDITO = '#/credito'
export const HASH_BUSCO_VEHICULO = '#/busco-un-vehiculo'
export const HASH_MIS_SOLICITUDES = '#/mis-solicitudes'

/** `#/credito/<slug>`: crédito para un vehículo, desde su ficha (CA-P6-03). */
export function hashCreditoVehiculo(slug: string): string {
  return `${HASH_CREDITO}/${encodeURIComponent(slug)}`
}

/** `#/contactos/<id>`. */
export function hashContacto(id: number): string {
  return `${HASH_CONTACTOS}/${id}`
}

/** true si la ruta es la lista o el detalle de los contactos (exigen sesión). */
export function esRutaContactos(ruta: Ruta): boolean {
  return ruta.nombre === 'contactos' || ruta.nombre === 'contacto'
}

/** `#/inventario/<id>` (id entero positivo). */
export function hashInventarioVehiculo(id: number): string {
  return `${HASH_INVENTARIO}/${id}`
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

/** Lee los filtros conocidos de la cadena de consulta; ignora los demás. */
export function leerFiltros(consulta: string): FiltrosCatalogo {
  const params = new URLSearchParams(consulta)
  const filtros: FiltrosCatalogo = { ...FILTROS_VACIOS }
  for (const clave of CLAVES_FILTROS) {
    filtros[clave] = (params.get(clave) ?? '').trim()
  }
  return filtros
}

/** `#/buscar?…` con los filtros no vacíos (sin `pagina=1`, que es la de defecto). */
export function hashBuscar(filtros: Partial<FiltrosCatalogo> = {}): string {
  const params = new URLSearchParams()
  for (const clave of CLAVES_FILTROS) {
    const valor = (filtros[clave] ?? '').trim()
    if (valor === '' || (clave === 'pagina' && valor === '1')) continue
    params.set(clave, valor)
  }
  const consulta = params.toString()
  return consulta === '' ? HASH_BUSCAR : `${HASH_BUSCAR}?${consulta}`
}

export function hashVehiculo(slug: string): string {
  return `#/vehiculo/${encodeURIComponent(slug)}`
}

/** Convierte un hash (`#/buscar?marcaId=3`) en una ruta. */
export function leerRuta(hash: string): Ruta {
  const sinAlmohadilla = hash.startsWith('#') ? hash.slice(1) : hash
  const corte = sinAlmohadilla.indexOf('?')
  const camino = (corte === -1 ? sinAlmohadilla : sinAlmohadilla.slice(0, corte)).replace(
    /\/+$/,
    '',
  )
  const consulta = corte === -1 ? '' : sinAlmohadilla.slice(corte + 1)

  if (camino === '') return INICIO
  if (camino === '/buscar') return { nombre: 'buscar', filtros: leerFiltros(consulta) }
  if (camino === '/cuenta') return { nombre: 'cuenta', registro: false }
  if (camino === '/cuenta/registro') return { nombre: 'cuenta', registro: true }
  const seccion = SECCIONES_ADMIN.find((s) => camino === `/cuenta/${s}`)
  if (seccion) return { nombre: 'cuenta', registro: false, seccion }
  if (camino === '/inventario') return { nombre: 'inventario' }
  if (camino === '/inventario/nuevo') return { nombre: 'inventarioNuevo' }
  if (camino === '/publicaciones') return { nombre: 'publicaciones' }
  if (camino === '/contactos') return { nombre: 'contactos' }
  if (camino === '/privacidad') return { nombre: 'privacidad' }
  if (camino === '/vende-tu-camion') return { nombre: 'vendeTuCamion' }
  if (camino === '/seguro-todo-riesgo') return { nombre: 'seguroTodoRiesgo' }
  if (camino === '/credito') return { nombre: 'credito', slug: null }
  if (camino === '/busco-un-vehiculo') return { nombre: 'buscoVehiculo' }
  if (camino === '/mis-solicitudes') return { nombre: 'misSolicitudes' }
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
  return INICIO
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

function hashActual(): string {
  return typeof window === 'undefined' ? '' : window.location.hash
}

/** Hash actual de la URL; se actualiza con `hashchange` (enlaces, Atrás/Adelante). */
export function useHash(): string {
  const [hash, setHash] = useState(hashActual)
  useEffect(() => {
    const alCambiar = () => setHash(hashActual())
    window.addEventListener('hashchange', alCambiar)
    // Por si cambió entre el primer render y la suscripción.
    alCambiar()
    return () => window.removeEventListener('hashchange', alCambiar)
  }, [])
  return hash
}

/** Ruta actual a partir del hash. */
export function useRuta(): Ruta {
  return leerRuta(useHash())
}

/** Navega a un hash (equivale a seguir un enlace). */
export function navegar(hash: string): void {
  if (window.location.hash === hash) return
  window.location.hash = hash
}

// ---- Última búsqueda, para "Volver a resultados" ----

let ultimaBusqueda: string | null = null

export function recordarBusqueda(hash: string): void {
  ultimaBusqueda = hash
}

/** Hash de la última búsqueda vista o, si no hubo, el del inicio. */
export function hashVolverAResultados(): string {
  return ultimaBusqueda ?? HASH_INICIO
}

/** Solo para pruebas: olvida la última búsqueda. */
export function olvidarUltimaBusqueda(): void {
  ultimaBusqueda = null
}
