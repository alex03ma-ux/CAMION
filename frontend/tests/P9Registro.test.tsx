// P9 — Autorización de datos en el registro de comprador y la inscripción de asesor (HU-062, RN-SE-004, D-44, D-84 A;
// CA-P9-01 a CA-P9-05, CA-P9-12, CA-P9-15, CA-P9-16) y mensaje del límite por IP (HU-059; CA-P9-46).
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../src/App'
import { segundosDeRetryAfter, textoEspera } from '../src/api/http'
import { AutorizacionDatos } from '../src/components/AutorizacionDatos'
import { RegistroForm } from '../src/components/RegistroForm'
import { irACuenta, simularFetch } from './helpers'

const RUTA_COMPRADOR = '/api/auth/registro/comprador'
const RUTA_ASESOR = '/api/auth/registro/asesor'
const RUTA_LOGIN = '/api/auth/login'
const MENSAJE_AUTORIZACION = 'Debes autorizar el tratamiento de tus datos para crear tu cuenta.'
const MENSAJE_429_REGISTRO = 'Hiciste muchos intentos de registro desde tu conexión. Intenta de nuevo más tarde.'
const MENSAJE_429_LOGIN = 'Hiciste muchos intentos de inicio de sesión. Espera unos minutos e intenta de nuevo.'
const MENSAJE_429_RESPALDO = 'Hiciste muchos intentos. Espera unos minutos e intenta de nuevo.'

type Usuario = ReturnType<typeof userEvent.setup>

const casilla = () => screen.getByRole('checkbox', { name: /Autorizo el tratamiento de mis datos personales/ })
const errorCasilla = () => document.getElementById('registro-autorizacion-error')

async function rellenarComprador(user: Usuario) {
  await user.type(screen.getByLabelText('Nombre completo'), 'Comprador Ficticio')
  await user.type(screen.getByLabelText('Correo electrónico'), 'comprador.ficticio@ejemplo.test')
  await user.type(screen.getByLabelText('Teléfono'), '+57 300 000 0000')
  await user.type(screen.getByLabelText('Contraseña'), 'Camion2026')
}

async function rellenarAsesor(user: Usuario) {
  await user.click(screen.getByLabelText('Soy asesor'))
  await user.type(screen.getByLabelText('Nombre completo'), 'Asesor Ficticio')
  await user.type(screen.getByLabelText('Correo electrónico'), 'asesor.ficticio@ejemplo.test')
  await user.type(screen.getByLabelText('Teléfono'), '+57 310 000 0000')
  await user.selectOptions(screen.getByLabelText('Tipo de documento'), 'CC')
  await user.type(screen.getByLabelText('Número de documento'), '1012345678')
  await user.type(screen.getByLabelText('Contraseña'), 'Camion2026a')
}

function cuerpo(api: ReturnType<typeof simularFetch>, ruta: string) {
  return JSON.parse(String(api.llamadasA(ruta)[0].init?.body)) as Record<string, unknown>
}

function antesDe(a: Element, b: Element) {
  return (a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0
}

beforeEach(irACuenta)

describe('Casilla de autorización en el registro (CA-P9-01, 02, 15, 16)', () => {
  it('comprador: casilla sin marcar antes de "Crear cuenta", con la política y los términos en pestaña nueva', () => {
    simularFetch({})
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    const c = casilla()
    expect(c).not.toBeChecked()
    expect(c).toHaveAttribute('aria-required', 'true')
    expect(antesDe(c, screen.getByRole('button', { name: 'Crear cuenta' }))).toBe(true)
    const label = document.querySelector('label[for="registro-autorizacion"]')
    expect(label).toHaveTextContent(
      'Autorizo el tratamiento de mis datos personales según la Política de tratamiento de datos',
    )
    const politica = within(label as HTMLElement).getByRole('link', { name: /Política de tratamiento de datos/ })
    expect(politica).toHaveAttribute('href', '#/privacidad')
    expect(politica).toHaveAttribute('target', '_blank')
    // CA-P9-15: el enlace a los términos está fuera de la etiqueta y no cambia el nombre de la casilla.
    const terminos = screen.getByRole('link', { name: /Términos y condiciones/ })
    expect(terminos).toHaveAttribute('href', '#/terminos')
    expect(terminos).toHaveAttribute('target', '_blank')
    expect(terminos).toHaveAttribute('rel', 'noopener noreferrer')
    expect(label?.contains(terminos)).toBe(false)
    expect(c).not.toHaveAccessibleName(/Términos/)
    // CA-P9-16 (D-84 A): no hay casilla de términos.
    expect(screen.getAllByRole('checkbox')).toHaveLength(1)
  })

  it('asesor: la misma casilla sin marcar antes de "Enviar solicitud"', async () => {
    simularFetch({})
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await user.click(screen.getByLabelText('Soy asesor'))
    expect(casilla()).not.toBeChecked()
    expect(antesDe(casilla(), screen.getByRole('button', { name: 'Enviar solicitud' }))).toBe(true)
    expect(screen.getByRole('link', { name: /Términos y condiciones/ })).toHaveAttribute('href', '#/terminos')
    expect(screen.getAllByRole('checkbox')).toHaveLength(1)
  })

  it('el componente común muestra el enlace a los términos en los demás formularios sin cambiar su texto', () => {
    render(<AutorizacionDatos id="x" marcada={false} onCambio={vi.fn()} />)
    const c = screen.getByRole('checkbox')
    expect(c).toHaveAccessibleName(/^Autorizo el tratamiento de mis datos personales según la Política de tratamiento de datos/)
    expect(c).not.toHaveAccessibleName(/Términos/)
    expect(screen.getByRole('link', { name: /Términos y condiciones/ })).toHaveAttribute('href', '#/terminos')
  })
})

describe('Validación de la casilla (CA-P9-03, 04, 05)', () => {
  it('formulario válido sin la casilla: no llama a la API, mensaje bajo la casilla y foco en ella', async () => {
    const api = simularFetch({})
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await rellenarComprador(user)
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    expect(errorCasilla()).toHaveTextContent(MENSAJE_AUTORIZACION)
    expect(casilla()).toHaveAttribute('aria-invalid', 'true')
    expect(casilla()).toHaveAttribute('aria-describedby', 'registro-autorizacion-error')
    expect(casilla()).toHaveFocus()
    expect(api.fn).not.toHaveBeenCalled()
  })

  it('con otros errores, el foco va al primer campo con error', async () => {
    simularFetch({})
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    expect(errorCasilla()).toHaveTextContent(MENSAJE_AUTORIZACION)
    expect(screen.getByLabelText('Nombre completo')).toHaveFocus()
  })

  it('al marcarla tras el error el mensaje desaparece; al desmarcarla vuelve', async () => {
    simularFetch({})
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await rellenarComprador(user)
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    await user.click(casilla())
    expect(errorCasilla()).toBeNull()
    expect(casilla()).toHaveAttribute('aria-invalid', 'false')
    await user.click(casilla())
    expect(errorCasilla()).toHaveTextContent(MENSAJE_AUTORIZACION)
  })

  it('al cambiar de tipo de cuenta conserva el estado de la casilla y limpia los errores', async () => {
    simularFetch({})
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    expect(errorCasilla()).not.toBeNull()
    await user.click(screen.getByLabelText('Soy asesor'))
    expect(errorCasilla()).toBeNull()
    expect(casilla()).not.toBeChecked()
    await user.click(casilla())
    await user.click(screen.getByLabelText('Soy comprador'))
    expect(casilla()).toBeChecked()
  })
})

describe('Envío con la autorización (CA-P9-12)', () => {
  it('comprador: el cuerpo lleva autorizacionDatos: true', async () => {
    const api = simularFetch({ [RUTA_COMPRADOR]: { status: 201, body: { usuarioId: 'u-1', mensaje: 'Cuenta creada.' } } })
    const user = userEvent.setup()
    const onRegistrado = vi.fn()
    render(<RegistroForm onRegistrado={onRegistrado} onIrALogin={vi.fn()} />)
    await rellenarComprador(user)
    await user.click(casilla())
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    await waitFor(() => expect(onRegistrado).toHaveBeenCalled())
    expect(cuerpo(api, RUTA_COMPRADOR).autorizacionDatos).toBe(true)
  })

  it('asesor: el cuerpo lleva autorizacionDatos: true', async () => {
    const api = simularFetch({ [RUTA_ASESOR]: { status: 201, body: { usuarioId: 'u-2', mensaje: 'Solicitud enviada.' } } })
    const user = userEvent.setup()
    const onRegistrado = vi.fn()
    render(<RegistroForm onRegistrado={onRegistrado} onIrALogin={vi.fn()} />)
    await rellenarAsesor(user)
    await user.click(casilla())
    await user.click(screen.getByRole('button', { name: 'Enviar solicitud' }))
    await waitFor(() => expect(onRegistrado).toHaveBeenCalled())
    expect(cuerpo(api, RUTA_ASESOR).autorizacionDatos).toBe(true)
  })

  it('un 400 con errores.autorizacionDatos se muestra bajo la casilla, con el foco en ella', async () => {
    simularFetch({
      [RUTA_COMPRADOR]: {
        status: 400,
        body: { error: 'Revisa los datos marcados.', errores: { autorizacionDatos: [MENSAJE_AUTORIZACION] } },
      },
    })
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await rellenarComprador(user)
    await user.click(casilla())
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    await waitFor(() => expect(errorCasilla()).toHaveTextContent(MENSAJE_AUTORIZACION))
    await waitFor(() => expect(casilla()).toHaveFocus())
    // No se repite en el aviso general.
    expect(screen.getByRole('alert')).toHaveTextContent(/^Revisa los datos marcados\.$/)
  })
})

describe('Límite de intentos (CA-P9-42, 46)', () => {
  it('Retry-After y el texto de espera', () => {
    expect(segundosDeRetryAfter('120')).toBe(120)
    expect(segundosDeRetryAfter(' 5 ')).toBe(5)
    expect(segundosDeRetryAfter('0')).toBeUndefined()
    expect(segundosDeRetryAfter('Wed, 21 Oct 2026 07:28:00 GMT')).toBeUndefined()
    expect(segundosDeRetryAfter(null)).toBeUndefined()
    expect(textoEspera(undefined)).toBe('')
    expect(textoEspera(1)).toBe('Podrás intentarlo de nuevo en 1 segundo.')
    expect(textoEspera(45)).toBe('Podrás intentarlo de nuevo en 45 segundos.')
    expect(textoEspera(60)).toBe('Podrás intentarlo de nuevo en 1 minuto.')
    expect(textoEspera(61)).toBe('Podrás intentarlo de nuevo en unos 2 minutos.')
    expect(textoEspera(3600)).toBe('Podrás intentarlo de nuevo en unos 60 minutos.')
  })

  it('registro: 429 muestra el mensaje del servidor y la espera; conserva lo escrito y reactiva el botón', async () => {
    simularFetch({
      [RUTA_COMPRADOR]: { status: 429, body: { error: MENSAJE_429_REGISTRO }, headers: { 'Retry-After': '1800' } },
    })
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await rellenarComprador(user)
    await user.click(casilla())
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(
      `${MENSAJE_429_REGISTRO} Podrás intentarlo de nuevo en unos 30 minutos.`,
    )
    expect(screen.getByLabelText('Nombre completo')).toHaveValue('Comprador Ficticio')
    expect(screen.getByLabelText('Contraseña')).toHaveValue('Camion2026')
    expect(casilla()).toBeChecked()
    expect(screen.getByRole('button', { name: 'Crear cuenta' })).toBeEnabled()
  })

  it('registro de asesor: 429 sin cuerpo usa el mensaje de respaldo', async () => {
    simularFetch({ [RUTA_ASESOR]: { status: 429 } })
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await rellenarAsesor(user)
    await user.click(casilla())
    await user.click(screen.getByRole('button', { name: 'Enviar solicitud' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(new RegExp(`^${MENSAJE_429_RESPALDO}$`))
    expect(screen.getByRole('button', { name: 'Enviar solicitud' })).toBeEnabled()
  })

  it('inicio de sesión: 429 muestra el mensaje del servidor, conserva correo y contraseña y reactiva el botón', async () => {
    simularFetch({ [RUTA_LOGIN]: { status: 429, body: { error: MENSAJE_429_LOGIN }, headers: { 'Retry-After': '300' } } })
    const user = userEvent.setup()
    render(<App />)
    await user.type(await screen.findByLabelText('Correo electrónico'), 'persona@ejemplo.test')
    await user.type(screen.getByLabelText('Contraseña'), 'Camion2026')
    await user.click(screen.getByRole('button', { name: 'Iniciar sesión' }))
    expect(await screen.findByText(`${MENSAJE_429_LOGIN} Podrás intentarlo de nuevo en unos 5 minutos.`)).toBeInTheDocument()
    expect(screen.getByLabelText('Correo electrónico')).toHaveValue('persona@ejemplo.test')
    expect(screen.getByLabelText('Contraseña')).toHaveValue('Camion2026')
    expect(screen.getByRole('button', { name: 'Iniciar sesión' })).toBeEnabled()
  })

  it('inicio de sesión: 429 sin cuerpo ni Retry-After usa el respaldo', async () => {
    simularFetch({ [RUTA_LOGIN]: { status: 429 } })
    const user = userEvent.setup()
    render(<App />)
    await user.type(await screen.findByLabelText('Correo electrónico'), 'persona@ejemplo.test')
    await user.type(screen.getByLabelText('Contraseña'), 'Camion2026')
    await user.click(screen.getByRole('button', { name: 'Iniciar sesión' }))
    expect(await screen.findByText(MENSAJE_429_RESPALDO)).toBeInTheDocument()
  })

  it('regresión: un 401 sigue borrando la contraseña', async () => {
    simularFetch({ [RUTA_LOGIN]: { status: 401, body: { error: 'Correo o contraseña incorrectos.' } } })
    const user = userEvent.setup()
    render(<App />)
    await user.type(await screen.findByLabelText('Correo electrónico'), 'persona@ejemplo.test')
    await user.type(screen.getByLabelText('Contraseña'), 'Camion2026')
    await user.click(screen.getByRole('button', { name: 'Iniciar sesión' }))
    expect(await screen.findByText('Correo o contraseña incorrectos.')).toBeInTheDocument()
    expect(screen.getByLabelText('Contraseña')).toHaveValue('')
  })
})
