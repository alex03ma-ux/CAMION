// Utilidades de prueba: fetch simulado por ruta y sesiones ficticias.
import { vi } from 'vitest'

export type RespuestaSimulada =
  | { status: number; body?: unknown }
  | { red: true } // simula fallo de red (fetch rechaza)

export type Llamada = { url: string; init: RequestInit | undefined }

/**
 * Sustituye `fetch` por un simulador. `rutas` asocia una ruta a una respuesta
 * o a una lista (se consume una por llamada; la última se repite).
 */
export function simularFetch(
  rutasPropias: Record<string, RespuestaSimulada | RespuestaSimulada[]>,
) {
  // P3: el encabezado pide siempre (sin token) las opciones del catálogo; si la
  // prueba no la define, se responde con un catálogo sin categorías.
  const rutas: Record<string, RespuestaSimulada | RespuestaSimulada[]> = {
    [RUTA_FILTROS]: FILTROS_SIN_DATOS,
    ...rutasPropias,
  }
  const llamadas: Llamada[] = []
  const contadores: Record<string, number> = {}
  const fn = vi.fn(async (entrada: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof entrada === 'string' ? entrada : entrada.toString()
    llamadas.push({ url, init })
    const definida = rutas[url]
    if (!definida) throw new Error(`Ruta no simulada en la prueba: ${url}`)
    const lista = Array.isArray(definida) ? definida : [definida]
    const i = contadores[url] ?? 0
    contadores[url] = i + 1
    const r = lista[Math.min(i, lista.length - 1)]
    if ('red' in r) throw new TypeError('Failed to fetch')
    const cuerpo = r.body === undefined ? null : JSON.stringify(r.body)
    return new Response(cuerpo, {
      status: r.status,
      headers: cuerpo ? { 'Content-Type': 'application/json' } : undefined,
    })
  })
  vi.stubGlobal('fetch', fn)
  return {
    fn,
    llamadas,
    llamadasA: (ruta: string) => llamadas.filter((l) => l.url === ruta),
    /** Llamadas a /api/auth/* (login, me, logout, registro). */
    llamadasAuth: () => llamadas.filter((l) => l.url.startsWith('/api/auth/')),
  }
}

/** Ruta del GET anónimo de opciones del catálogo que hace el encabezado (P3). */
export const RUTA_FILTROS = '/api/catalogo/filtros'

/** Respuesta de /api/catalogo/filtros sin categorías ni marcas. */
export const FILTROS_SIN_DATOS: RespuestaSimulada = {
  status: 200,
  body: {
    tipos: [],
    marcas: [],
    rangos: { precioMin: null, precioMax: null, anioMin: null, anioMax: null, kmMax: null },
  },
}

/**
 * P3: la app abre en el catálogo; las pruebas de P1/P2 abren la pantalla de
 * cuenta (`#/cuenta`) antes de renderizar <App />.
 */
export function irACuenta() {
  window.location.hash = '#/cuenta'
}

/** Token ficticio: no es un JWT real. */
export const TOKEN_FICTICIO = 'token-de-prueba-ficticio'

export const PERFIL_FICTICIO = {
  usuarioId: 'u-0001',
  nombreCompleto: 'Comprador Ficticio',
  email: 'comprador.ficticio@ejemplo.test',
  phoneNumber: '+57 300 000 0000',
  roles: ['COMPRADOR'],
}

export function sesionFicticia(expiraEnMs: number) {
  return {
    accessToken: TOKEN_FICTICIO,
    expiraEnUtc: new Date(expiraEnMs).toISOString(),
    usuarioId: PERFIL_FICTICIO.usuarioId,
    nombreCompleto: PERFIL_FICTICIO.nombreCompleto,
    email: PERFIL_FICTICIO.email,
    roles: ['COMPRADOR'],
  }
}

export function guardarSesionFicticia(expiraEnMs: number) {
  window.localStorage.setItem('camion.sesion', JSON.stringify(sesionFicticia(expiraEnMs)))
}

export function cabecera(init: RequestInit | undefined, nombre: string): string | undefined {
  const h = init?.headers as Record<string, string> | undefined
  return h?.[nombre]
}
