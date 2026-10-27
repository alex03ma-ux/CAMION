// Navegación por el hash de la URL (P3), sin dependencias.
//   #/                  Inicio
//   #/buscar?…          Resultados (filtros en la URL: se comparten y funcionan con Atrás/Adelante)
//   #/vehiculo/<slug>   Detalle
//   #/cuenta            Login / registro / perfil / panel de administración
//   #/cuenta/registro   Lo mismo, abriendo el formulario de registro
// Cualquier otra ruta muestra el inicio. Nunca se pone el token en la URL.
import { useEffect, useState } from 'react'
import { CLAVES_FILTROS, FILTROS_VACIOS, type FiltrosCatalogo } from './api/catalogo'

export type Ruta =
  | { nombre: 'inicio' }
  | { nombre: 'buscar'; filtros: FiltrosCatalogo }
  | { nombre: 'vehiculo'; slug: string }
  | { nombre: 'cuenta'; registro: boolean }

export const HASH_INICIO = '#/'
export const HASH_BUSCAR = '#/buscar'
export const HASH_CUENTA = '#/cuenta'
export const HASH_REGISTRO = '#/cuenta/registro'

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
  const vehiculo = /^\/vehiculo\/([^/]+)$/.exec(camino)
  if (vehiculo) {
    try {
      const slug = decodeURIComponent(vehiculo[1])
      if (slug.trim() !== '') return { nombre: 'vehiculo', slug }
    } catch {
      // Codificación inválida: se trata como ruta desconocida.
    }
  }
  return INICIO
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
