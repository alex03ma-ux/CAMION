// Cierre automático por inactividad con reloj simulado (CA-060-10, 11, 13, 14).
import { act, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../src/App'
import { guardarSesionFicticia, PERFIL_FICTICIO, simularFetch } from './helpers'

const ME = '/api/auth/me'
const LOGOUT = '/api/auth/logout'
const MIN = 60 * 1000
const INICIO = new Date('2026-01-15T10:00:00.000Z').getTime()
const DOS_HORAS = 120 * MIN
const BIENVENIDA = `¡Bienvenido, ${PERFIL_FICTICIO.nombreCompleto}!`
const MSG_TERMINADA = 'Tu sesión terminó. Inicia sesión de nuevo.'

/** Avanza el reloj simulado y deja que React procese los efectos. */
async function avanzar(ms: number) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms)
  })
}

async function montarConPerfil() {
  guardarSesionFicticia(INICIO + DOS_HORAS)
  const api = simularFetch({
    [ME]: { status: 200, body: PERFIL_FICTICIO },
    [LOGOUT]: { status: 204 },
  })
  render(<App />)
  await avanzar(0)
  expect(screen.getByRole('heading', { name: BIENVENIDA })).toBeInTheDocument()
  return api
}

describe('Inactividad', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(INICIO)
  })
  afterEach(() => {
    vi.useRealTimers()
  })

  it('CA-060-10 a los 30 min sin actividad se borra la sesión sin llamar a /logout', async () => {
    const api = await montarConPerfil()

    await avanzar(30 * MIN - 1000)
    expect(screen.getByRole('heading', { name: BIENVENIDA })).toBeInTheDocument()
    expect(window.localStorage.getItem('camion.sesion')).not.toBeNull()

    await avanzar(1000)
    expect(screen.queryByRole('heading', { name: BIENVENIDA })).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent(MSG_TERMINADA)
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
    expect(window.localStorage.getItem('camion.ultimaActividad')).toBeNull()
    expect(api.llamadasA(LOGOUT)).toHaveLength(0)
  })

  it('CA-060-11 actividad a los 29 min reinicia el plazo (sigue abierta a los 31 min)', async () => {
    const api = await montarConPerfil()

    await avanzar(29 * MIN)
    fireEvent.click(document.body)
    await avanzar(2 * MIN) // minuto 31 desde el inicio
    expect(screen.getByRole('heading', { name: BIENVENIDA })).toBeInTheDocument()
    expect(window.localStorage.getItem('camion.sesion')).not.toBeNull()

    // También vale una tecla: a los 58 min (29 tras el clic) se pulsa otra vez.
    await avanzar(27 * MIN)
    fireEvent.keyDown(document.body, { key: 'a' })
    await avanzar(29 * MIN) // 29 min después de la tecla
    expect(screen.getByRole('heading', { name: BIENVENIDA })).toBeInTheDocument()

    // 30 min tras la última actividad se cierra.
    await avanzar(1 * MIN)
    expect(screen.getByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
    expect(api.llamadasA(LOGOUT)).toHaveLength(0)
  })

  it('CA-060-13 camion.ultimaActividad de hace más de 30 min al cargar borra la sesión', async () => {
    guardarSesionFicticia(INICIO + DOS_HORAS)
    window.localStorage.setItem('camion.ultimaActividad', String(INICIO - 31 * MIN))
    const api = simularFetch({ [ME]: { status: 200, body: PERFIL_FICTICIO } })
    render(<App />)
    await avanzar(0)

    expect(screen.getByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent(MSG_TERMINADA)
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
    expect(window.localStorage.getItem('camion.ultimaActividad')).toBeNull()
    expect(api.fn).not.toHaveBeenCalled()
  })

  it('CA-060-13 camion.ultimaActividad de hace menos de 30 min al cargar mantiene la sesión', async () => {
    guardarSesionFicticia(INICIO + DOS_HORAS)
    window.localStorage.setItem('camion.ultimaActividad', String(INICIO - 10 * MIN))
    simularFetch({ [ME]: { status: 200, body: PERFIL_FICTICIO } })
    render(<App />)
    await avanzar(0)
    expect(screen.getByRole('heading', { name: BIENVENIDA })).toBeInTheDocument()

    // El plazo cuenta desde la actividad guardada: cierra a los 20 min de cargar.
    await avanzar(20 * MIN)
    expect(screen.getByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
  })

  it('CA-060-14 si otra pestaña actualizó camion.ultimaActividad, no se cierra', async () => {
    const api = await montarConPerfil()

    await avanzar(29 * MIN)
    // Otra pestaña registra actividad (escribe en el almacenamiento compartido).
    window.localStorage.setItem('camion.ultimaActividad', String(Date.now()))
    await avanzar(2 * MIN) // minuto 31 sin actividad en esta pestaña
    expect(screen.getByRole('heading', { name: BIENVENIDA })).toBeInTheDocument()
    expect(window.localStorage.getItem('camion.sesion')).not.toBeNull()

    // Sin más actividad en ninguna pestaña, cierra 30 min después de la de la otra pestaña.
    await avanzar(28 * MIN)
    expect(screen.getByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(api.llamadasA(LOGOUT)).toHaveLength(0)
  })
})
