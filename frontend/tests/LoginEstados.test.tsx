// P2 — Avisos del login por estado de la cuenta (CA-045-11, CA-045-12) y CA-D12-02.
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import App from '../src/App'
import { guardarSesionFicticia, irACuenta, simularFetch } from './helpers'

const LOGIN = '/api/auth/login'
const EMAIL = 'asesor.ficticio@ejemplo.test'
const PASS = 'Camion2026a' // contraseña ficticia de prueba

const PENDIENTE = {
  status: 403,
  body: {
    error:
      'Tu cuenta de asesor está pendiente de aprobación. Te avisaremos cuando el administrador la apruebe.',
    estado: 'PENDIENTE',
  },
}
const RECHAZADA = {
  status: 403,
  body: {
    error: 'Tu solicitud de asesor no fue aprobada. Comunícate con el administrador.',
    estado: 'RECHAZADA',
  },
}
const BLOQUEADA = {
  status: 403,
  body: { error: 'Tu cuenta está bloqueada. Comunícate con el administrador.', estado: 'BLOQUEADA' },
}

type Usuario = ReturnType<typeof userEvent.setup>

async function entrar(user: Usuario, email = EMAIL) {
  const campo = screen.getByLabelText('Correo electrónico')
  if ((campo as HTMLInputElement).value === '') await user.type(campo, email)
  await user.type(screen.getByLabelText('Contraseña'), PASS)
  await user.click(screen.getByRole('button', { name: 'Iniciar sesión' }))
}

// P3: la app abre en el catálogo; estas pruebas usan la pantalla de cuenta.
beforeEach(irACuenta)

describe('Login con cuenta no activa (P2)', () => {
  it('CA-045-11 403 PENDIENTE: aviso informativo (role=status), sin alerta ni sesión guardada', async () => {
    simularFetch({ [LOGIN]: PENDIENTE })
    const user = userEvent.setup()
    render(<App />)
    await entrar(user)

    const aviso = await screen.findByRole('status')
    expect(aviso).toHaveTextContent('pendiente de aprobación')
    expect(aviso).toHaveClass('aviso-info')
    expect(aviso).not.toHaveClass('aviso-error')
    expect(screen.queryByRole('alert')).toBeNull()
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
    expect(screen.getByLabelText('Correo electrónico')).toHaveValue(EMAIL)
    expect(screen.getByLabelText('Contraseña')).toHaveValue('')
  })

  it.each([
    ['RECHAZADA', RECHAZADA, 'Tu solicitud de asesor no fue aprobada.'],
    ['BLOQUEADA', BLOQUEADA, 'Tu cuenta está bloqueada.'],
  ])(
    'CA-045-12 403 %s: aviso de advertencia (role=alert) con el texto del servidor',
    async (_estado, respuesta, texto) => {
      simularFetch({ [LOGIN]: respuesta })
      const user = userEvent.setup()
      render(<App />)
      await entrar(user)

      const alerta = await screen.findByRole('alert')
      expect(alerta).toHaveTextContent(texto)
      expect(alerta).toHaveClass('aviso-advertencia')
      expect(alerta).not.toHaveClass('aviso-error')
      expect(window.localStorage.getItem('camion.sesion')).toBeNull()
      expect(screen.getByLabelText('Correo electrónico')).toHaveValue(EMAIL)
      expect(screen.getByLabelText('Contraseña')).toHaveFocus()
    },
  )

  it('CA-045-11/12 el 401 y el 423 siguen como error, distintos de los avisos 403', async () => {
    simularFetch({
      [LOGIN]: [
        { status: 401, body: { error: 'Correo o contraseña incorrectos.' } },
        { status: 423, body: { error: 'Demasiados intentos fallidos. Intenta de nuevo en 15 minutos.' } },
        PENDIENTE,
        BLOQUEADA,
      ],
    })
    const user = userEvent.setup()
    render(<App />)

    await entrar(user)
    const a401 = await screen.findByRole('alert')
    expect(a401).toHaveTextContent('Correo o contraseña incorrectos.')
    expect(a401).toHaveClass('aviso-error')

    await entrar(user)
    const a423 = await screen.findByRole('alert')
    expect(a423).toHaveTextContent('Demasiados intentos fallidos')
    expect(a423).toHaveClass('aviso-error')

    await entrar(user)
    const info = await screen.findByRole('status')
    expect(info).toHaveClass('aviso-info')
    expect(info).not.toHaveTextContent('Correo o contraseña incorrectos.')
    expect(screen.queryByRole('alert')).toBeNull()

    await entrar(user)
    const adv = await screen.findByRole('alert')
    expect(adv).toHaveClass('aviso-advertencia')
    expect(adv).toHaveTextContent('Tu cuenta está bloqueada.')
    expect(screen.queryByRole('status')).toBeNull()
  })

  it('un 403 sin campo estado se muestra como error (no como aviso de cuenta)', async () => {
    simularFetch({ [LOGIN]: { status: 403, body: { error: 'No tienes permiso para realizar esta acción.' } } })
    const user = userEvent.setup()
    render(<App />)
    await entrar(user)
    const alerta = await screen.findByRole('alert')
    expect(alerta).toHaveClass('aviso-error')
    expect(alerta).toHaveTextContent('No tienes permiso para realizar esta acción.')
  })

  it('CA-D12-02 usuario bloqueado con la app abierta: 401 termina la sesión y al entrar ve el aviso de bloqueo', async () => {
    guardarSesionFicticia(Date.now() + 60 * 60 * 1000)
    simularFetch({ '/api/auth/me': { status: 401 }, [LOGIN]: BLOQUEADA })
    const user = userEvent.setup()
    render(<App />)

    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Tu sesión terminó. Inicia sesión de nuevo.')
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()

    await entrar(user)
    const alerta = await screen.findByRole('alert')
    expect(alerta).toHaveTextContent('Tu cuenta está bloqueada.')
    expect(alerta).toHaveClass('aviso-advertencia')
  })
})
