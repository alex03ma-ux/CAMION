// P4p — datos ficticios de planes, suscripciones y catálogos para las pruebas.
import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from '../src/App'
import { simularFetch, TOKEN_FICTICIO, type RespuestaSimulada } from './helpers'

export type Rutas = Record<string, RespuestaSimulada | RespuestaSimulada[]>

export const RUTA_PLANES = '/api/admin/planes'
export const RUTA_PLANES_ACTIVOS = '/api/admin/planes?activo=true'
export const RUTA_CATALOGOS_INV = '/api/inventario/catalogos'
export const RUTA_SUSCRIPCIONES = '/api/admin/suscripciones?pagina=1&tamano=20'
export const RUTA_MI_SUSCRIPCION = '/api/asesor/suscripcion'

export function plan(id: number, nombre: string, extra: Record<string, unknown> = {}) {
  return {
    id,
    nombre,
    precioCop: 150000,
    duracionMeses: 3,
    diasVigencia: 90,
    limitePublicaciones: 10,
    esPrueba: false,
    activo: true,
    suscripcionesVivas: 2,
    ...extra,
  }
}

export const PLAN_BASICO = plan(1, 'Básico')
export const PLAN_PRO = plan(2, 'Profesional', { precioCop: 300000, duracionMeses: 6, diasVigencia: 180, limitePublicaciones: 30 })
export const PLAN_VIEJO = plan(3, 'Antiguo', { activo: false, suscripcionesVivas: 0 })

export const CATALOGOS_INVENTARIO = {
  status: 200,
  body: {
    tipos: [
      { id: 1, nombre: 'Carro' },
      { id: 2, nombre: 'Camión' },
      { id: 3, nombre: 'Volqueta' },
      { id: 4, nombre: 'Maquinaria' },
    ],
    marcas: [],
    carrocerias: [],
    materiales: [],
    tiposMaquina: [],
    combustibles: [],
    departamentos: [],
    asesores: [
      { id: 7, nombre: 'Asesor Uno' },
      { id: 8, nombre: 'Asesor Dos' },
    ],
  },
}

export function suscripcion(id: number, estado: string, extra: Record<string, unknown> = {}) {
  return {
    id,
    asesor: { id: 7, nombre: 'Asesor Uno' },
    plan: { id: 1, nombre: 'Básico', limitePublicaciones: 10, diasVigencia: 90 },
    estado,
    estadoEfectivo: estado,
    inicioVigencia: '2026-10-01T12:00:00Z',
    finVigencia: '2026-12-30T12:00:00Z',
    diasRestantes: 62,
    ...extra,
  }
}

export function paginaDe(items: unknown[]) {
  return { status: 200, body: { items, total: items.length, pagina: 1, tamano: 20 } }
}

export function cuerpoDe(api: ReturnType<typeof simularFetch>, ruta: string, i = 0): unknown {
  const llamada = api.llamadasA(ruta)[i]
  return llamada?.init?.body === undefined ? undefined : JSON.parse(String(llamada.init.body))
}

export function perfil(roles: string[]) {
  return {
    usuarioId: 'p-0001',
    nombreCompleto: 'Persona Ficticia',
    email: 'persona.ficticia@ejemplo.test',
    phoneNumber: '+57 300 000 0009',
    roles,
    estado: 'ACTIVA',
  }
}

/** Guarda una sesión con esos roles, abre la app en `hash` y responde /me con esos roles. */
export function abrirApp(hash: string, roles: string[], rutas: Rutas = {}) {
  window.localStorage.setItem(
    'camion.sesion',
    JSON.stringify({
      accessToken: TOKEN_FICTICIO,
      expiraEnUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      usuarioId: 'p-0001',
      nombreCompleto: 'Persona Ficticia',
      email: 'persona.ficticia@ejemplo.test',
      roles,
    }),
  )
  window.location.hash = hash
  const api = simularFetch({ '/api/auth/me': { status: 200, body: perfil(roles) }, ...rutas })
  render(<App />)
  return { api, user: userEvent.setup() }
}
