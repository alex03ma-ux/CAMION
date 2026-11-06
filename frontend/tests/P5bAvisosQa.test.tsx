// P5b (QA) — huecos de la matriz en la pantalla "Compra tu seguro todo riesgo" (HU-N02): validación de nombre, teléfono y
// autorización en el navegador (CA-P5b-13, 14, 19), casilla sin marcar con la política (CA-P5b-18), nota de obligatorios
// (CA-P5b-31) y mensaje de 429 / sin conexión conservando lo escrito (CA-P5b-30). Datos ficticios.
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import App from '../src/App'
import { simularFetch, type RespuestaSimulada } from './helpers'

const RUTA_SEGURO = '/api/avisos/seguro-todo-riesgo'
const MENSAJE_FALLO =
  'No pudimos enviar tus datos. Intenta de nuevo en unos minutos o escríbenos por WhatsApp al 316 615 2907.'
const MENSAJE_429 = 'Has enviado muchas solicitudes. Intenta de nuevo en unos minutos.'

function abrirSeguro(rutas: Record<string, RespuestaSimulada | RespuestaSimulada[]> = {}) {
  const api = simularFetch(rutas)
  window.location.hash = '#/seguro-todo-riesgo'
  render(<App />)
  return { api, user: userEvent.setup() }
}

const formSeguro = () => screen.findByRole('form', { name: 'Formulario Compra tu seguro todo riesgo' })
const nombre = () => screen.getByLabelText('Nombre')
const telefono = () => screen.getByLabelText('Teléfono')
const tipo = () => screen.getByLabelText('Tipo de vehículo')
const casilla = () => screen.getByRole('checkbox', { name: /Autorizo el tratamiento de mis datos personales/ })
const enviar = () => screen.getByRole('button', { name: /^(Enviar|Enviando\.\.\.)$/ })

describe('"Compra tu seguro todo riesgo": validaciones y autorización (QA, CA-P5b-13, 14, 18, 19, 31)', () => {
  it('vacío: errores en nombre, teléfono, tipo y casilla; foco al nombre; no llama a la API', async () => {
    const { api, user } = abrirSeguro()
    const form = await formSeguro()
    expect(within(form).getByText('Campo obligatorio')).toBeInTheDocument()
    expect(casilla()).not.toBeChecked()
    expect(casilla()).toHaveAttribute('aria-required', 'true')
    expect(nombre()).toHaveAttribute('aria-required', 'true')
    expect(telefono()).toHaveAttribute('aria-required', 'true')
    expect(within(form).getByRole('link', { name: /Política de tratamiento de datos/ })).toHaveAttribute('href', '#/privacidad')

    await user.click(enviar())
    expect(nombre()).toHaveAttribute('aria-invalid', 'true')
    expect(nombre()).toHaveAccessibleDescription('Escribe tu nombre (entre 2 y 150 caracteres).')
    expect(telefono()).toHaveAccessibleDescription(
      /Escribe un celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567\./,
    )
    expect(tipo()).toHaveAccessibleDescription('Elige el tipo de vehículo.')
    expect(casilla()).toHaveAccessibleDescription('Debes autorizar el tratamiento de tus datos para enviar el formulario.')
    await waitFor(() => expect(nombre()).toHaveFocus())
    expect(api.llamadasA(RUTA_SEGURO)).toHaveLength(0)
  })

  it.each([['3001234'], ['+1 305 555 1234'], ['2001234567']])('CA-P5b-14 el teléfono %s se rechaza sin llamar a la API', async (valor) => {
    const { api, user } = abrirSeguro()
    await formSeguro()
    await user.type(nombre(), 'Asegurado Ficticio')
    await user.type(telefono(), valor)
    await user.selectOptions(tipo(), 'CARRO')
    await user.click(casilla())
    await user.click(enviar())
    expect(telefono()).toHaveAttribute('aria-invalid', 'true')
    expect(api.llamadasA(RUTA_SEGURO)).toHaveLength(0)
  })

  it('CA-P5b-19 todo válido pero sin marcar la casilla: no se envía', async () => {
    const { api, user } = abrirSeguro()
    await formSeguro()
    await user.type(nombre(), 'Asegurado Ficticio')
    await user.type(telefono(), '3001234567')
    await user.selectOptions(tipo(), 'MAQUINARIA')
    await user.click(enviar())
    expect(casilla()).toHaveAttribute('aria-invalid', 'true')
    expect(api.llamadasA(RUTA_SEGURO)).toHaveLength(0)
  })
})

describe('"Compra tu seguro todo riesgo": errores del servidor (QA, CA-P5b-30)', () => {
  it.each<[string, RespuestaSimulada, string]>([
    ['429', { status: 429, body: { error: MENSAJE_429 } }, MENSAJE_429],
    ['sin conexión', { red: true }, MENSAJE_FALLO],
  ])('%s: mensaje comprensible y se conservan los datos escritos', async (_n, respuesta, texto) => {
    const { user } = abrirSeguro({ [RUTA_SEGURO]: respuesta })
    await formSeguro()
    await user.type(nombre(), 'Asegurado Ficticio')
    await user.type(telefono(), '3001234567')
    await user.selectOptions(tipo(), 'CAMION')
    await user.click(casilla())
    await user.click(enviar())
    expect(await screen.findByText(texto)).toHaveAttribute('role', 'alert')
    expect(nombre()).toHaveValue('Asegurado Ficticio')
    expect(tipo()).toHaveValue('CAMION')
    expect(casilla()).toBeChecked()
  })
})
