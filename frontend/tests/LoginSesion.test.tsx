// Login, sesión persistente y cierre de sesión (CA-059-13 a 20, CA-060-08, 09, 12, CA-062-06 a 08).
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import App from '../src/App'
import {
  cabecera,
  guardarSesionFicticia,
  PERFIL_FICTICIO,
  simularFetch,
  TOKEN_FICTICIO,
} from './helpers'

const LOGIN = '/api/auth/login'
const ME = '/api/auth/me'
const LOGOUT = '/api/auth/logout'
const PASSWORD = 'Camion2026'
const MSG_TERMINADA = 'Tu sesión terminó. Inicia sesión de nuevo.'
const MSG_CREDENCIALES = 'Correo o contraseña incorrectos.'
const MSG_BLOQUEO =
  'Cuenta bloqueada temporalmente por varios intentos fallidos. Intenta de nuevo en 15 minutos.'

const DOS_HORAS = 2 * 60 * 60 * 1000

function respuestaLogin() {
  return {
    accessToken: TOKEN_FICTICIO,
    tokenType: 'Bearer',
    expiraEnUtc: new Date(Date.now() + DOS_HORAS).toISOString(),
    usuarioId: PERFIL_FICTICIO.usuarioId,
    nombreCompleto: PERFIL_FICTICIO.nombreCompleto,
    email: PERFIL_FICTICIO.email,
    roles: ['COMPRADOR'],
  }
}

async function iniciarSesionUI(email = PERFIL_FICTICIO.email, password = PASSWORD) {
  const user = userEvent.setup()
  if (email) await user.type(screen.getByLabelText('Correo electrónico'), email)
  if (password) await user.type(screen.getByLabelText('Contraseña'), password)
  await user.click(screen.getByRole('button', { name: 'Iniciar sesión' }))
  return user
}

describe('Login', () => {
  it('CA-059-17 un 401 muestra "Correo o contraseña incorrectos.", conserva el correo y vacía la contraseña', async () => {
    simularFetch({ [LOGIN]: { status: 401, body: { error: MSG_CREDENCIALES } } })
    render(<App />)
    await iniciarSesionUI()
    expect(await screen.findByRole('alert')).toHaveTextContent(MSG_CREDENCIALES)
    expect(screen.getByLabelText('Correo electrónico')).toHaveValue(PERFIL_FICTICIO.email)
    expect(screen.getByLabelText('Contraseña')).toHaveValue('')
  })

  it('CA-059-18 un 423 muestra el mensaje de bloqueo, distinto del de credenciales', async () => {
    simularFetch({ [LOGIN]: { status: 423, body: { error: MSG_BLOQUEO } } })
    render(<App />)
    await iniciarSesionUI()
    const alerta = await screen.findByRole('alert')
    expect(alerta).toHaveTextContent(MSG_BLOQUEO)
    expect(alerta).not.toHaveTextContent(MSG_CREDENCIALES)
  })

  it('CA-059-17/18 sin cuerpo, 401 y 423 usan mensajes de respaldo distintos', async () => {
    simularFetch({ [LOGIN]: [{ status: 401 }, { status: 423 }] })
    render(<App />)
    const user = await iniciarSesionUI()
    expect(await screen.findByRole('alert')).toHaveTextContent(MSG_CREDENCIALES)
    await user.type(screen.getByLabelText('Contraseña'), PASSWORD)
    await user.click(screen.getByRole('button', { name: 'Iniciar sesión' }))
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(MSG_BLOQUEO))
  })

  it('CA-062-05 error de red muestra "No se pudo conectar con el servidor."', async () => {
    simularFetch({ [LOGIN]: { red: true } })
    render(<App />)
    await iniciarSesionUI()
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo conectar con el servidor.')
  })

  it('CA-059-19 con campos vacíos no se envía la petición y se marca el campo', async () => {
    const api = simularFetch({})
    render(<App />)
    await iniciarSesionUI('', '')
    expect(screen.getByLabelText('Correo electrónico')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByRole('alert')).toHaveTextContent('Escribe tu correo y tu contraseña.')
    expect(api.fn).not.toHaveBeenCalled()
  })

  it('CA-059-20 el campo contraseña es de tipo password', () => {
    simularFetch({})
    render(<App />)
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('type', 'password')
  })
})

describe('Sesión persistente', () => {
  it('CA-059-13 / CA-062-06 tras el login se guarda camion.sesion sin contraseña y se ve el perfil', async () => {
    const api = simularFetch({
      [LOGIN]: { status: 200, body: respuestaLogin() },
      [ME]: { status: 200, body: PERFIL_FICTICIO },
    })
    render(<App />)
    await iniciarSesionUI()

    expect(
      await screen.findByRole('heading', { name: `¡Bienvenido, ${PERFIL_FICTICIO.nombreCompleto}!` }),
    ).toBeInTheDocument()

    const crudo = window.localStorage.getItem('camion.sesion')
    expect(crudo).not.toBeNull()
    const sesion = JSON.parse(crudo as string)
    expect(sesion.accessToken).toBe(TOKEN_FICTICIO)
    expect(typeof sesion.expiraEnUtc).toBe('string')
    expect(Object.keys(sesion)).not.toContain('password')
    expect(crudo).not.toContain(PASSWORD)

    // CA-062-07: el token va en la cabecera Authorization, nunca en la URL.
    const [me] = api.llamadasA(ME)
    expect(cabecera(me.init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
    expect(me.url).not.toContain(TOKEN_FICTICIO)
  })

  it('CA-059-14 al montar con sesión vigente y /me 200 se ve el perfil sin pedir credenciales', async () => {
    guardarSesionFicticia(Date.now() + DOS_HORAS)
    const api = simularFetch({ [ME]: { status: 200, body: PERFIL_FICTICIO } })
    render(<App />)
    expect(
      await screen.findByRole('heading', { name: `¡Bienvenido, ${PERFIL_FICTICIO.nombreCompleto}!` }),
    ).toBeInTheDocument()
    expect(screen.queryByLabelText('Contraseña')).not.toBeInTheDocument()
    expect(api.llamadasA(ME)).toHaveLength(1)
    expect(api.llamadasA(LOGIN)).toHaveLength(0)
  })

  it('CA-059-16 / CA-060-12 con /me 401 se borra la sesión y aparece el aviso de sesión terminada', async () => {
    guardarSesionFicticia(Date.now() + DOS_HORAS)
    simularFetch({ [ME]: { status: 401 } })
    render(<App />)
    expect(await screen.findByRole('alert')).toHaveTextContent(MSG_TERMINADA)
    expect(screen.getByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
  })

  it('CA-059-15 sesión caducada: se borra, no se llama a /me y se muestra el login', () => {
    guardarSesionFicticia(Date.now() - 1000)
    const api = simularFetch({ [ME]: { status: 200, body: PERFIL_FICTICIO } })
    render(<App />)
    expect(screen.getByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
    expect(api.fn).not.toHaveBeenCalled()
  })
})

describe('Cerrar sesión', () => {
  async function montarConPerfil(logout: Parameters<typeof simularFetch>[0][string]) {
    guardarSesionFicticia(Date.now() + DOS_HORAS)
    window.localStorage.setItem('camion.ultimaActividad', String(Date.now()))
    const api = simularFetch({
      [ME]: { status: 200, body: PERFIL_FICTICIO },
      [LOGOUT]: logout,
    })
    render(<App />)
    await screen.findByRole('heading', { name: `¡Bienvenido, ${PERFIL_FICTICIO.nombreCompleto}!` })
    return api
  }

  it('CA-060-08 / CA-062-08 llama a POST /api/auth/logout con Bearer y borra las claves', async () => {
    const api = await montarConPerfil({ status: 204 })
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Cerrar sesión' }))

    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    const llamadas = api.llamadasA(LOGOUT)
    expect(llamadas).toHaveLength(1)
    expect(llamadas[0].init?.method).toBe('POST')
    expect(cabecera(llamadas[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
    expect(window.localStorage.getItem('camion.ultimaActividad')).toBeNull()
  })

  it('CA-060-09 borra las claves y muestra el login aunque el fetch de logout falle (red)', async () => {
    const api = await montarConPerfil({ red: true })
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Cerrar sesión' }))

    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(api.llamadasA(LOGOUT)).toHaveLength(1)
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
    expect(window.localStorage.getItem('camion.ultimaActividad')).toBeNull()
  })

  it('CA-060-09 borra las claves aunque el logout responda 500', async () => {
    await montarConPerfil({ status: 500 })
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Cerrar sesión' }))
    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
    expect(window.localStorage.getItem('camion.ultimaActividad')).toBeNull()
  })
})
