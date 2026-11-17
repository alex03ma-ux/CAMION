// QA de P9 — casos añadidos por QA (CA-P9-17, 29, 34, 49, 50, 55, 57): bordes del NIT iguales a los del servidor,
// mensajes de fallo de "Seguro todo riesgo" y "Busco un vehículo" con el WhatsApp vigente, "Saltar al contenido" en la
// página no encontrada, términos con sesión de administrador y noindex en el registro. Datos ficticios.
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import App from '../src/App'
import { digitoVerificacionNit, normalizarNit } from '../src/api/parametros'
import { PiePortal } from '../src/components/PiePortal'
import { olvidarContactoPortal } from '../src/components/useContactoPortal'
import { CONTACTO_PORTAL_POR_DEFECTO, RUTA_CONTACTO_PORTAL, simularFetch, type RespuestaSimulada } from './helpers'
import { abrirApp } from './p4pDatos'
import { irA, ubicacionActual } from './helpers'

const base = (CONTACTO_PORTAL_POR_DEFECTO as { body: Record<string, unknown> }).body
const CONTACTO_300: RespuestaSimulada = {
  status: 200,
  body: { ...base, whatsapp: '3001234567', whatsappEnlace: 'https://wa.me/573001234567' },
}
const FALLO_300 =
  'No pudimos enviar tus datos. Intenta de nuevo en unos minutos o escríbenos por WhatsApp al 300 123 4567.'
const FALLO_RESPALDO =
  'No pudimos enviar tus datos. Intenta de nuevo en unos minutos o escríbenos por WhatsApp al 316 615 2907.'
const LISTAS: RespuestaSimulada = {
  status: 200,
  body: {
    tipos: [{ id: 2, nombre: 'Camión' }],
    marcas: [],
    departamentos: [],
  },
}
const noIndex = () => document.head.querySelector('meta[name="robots"][content="noindex"]')
const casilla = () => screen.getByRole('checkbox', { name: /Autorizo el tratamiento de mis datos personales/ })

function abrir(hash: string, rutas: Record<string, RespuestaSimulada | RespuestaSimulada[]>) {
  const api = simularFetch(rutas)
  irA(hash)
  render(<App />)
  return { api, user: userEvent.setup() }
}

describe('QA CA-P9-29: bordes del NIT (mismos casos que P9QaTests.Nit_bordes del servidor)', () => {
  it('DV de 123456 = 3 y de 1234567890 = 2', () => {
    expect(digitoVerificacionNit('123456')).toBe(3)
    expect(digitoVerificacionNit('1234567890')).toBe(2)
  })

  it.each<[string, string | null]>([
    ['123456-3', '123456-3'],
    ['1234567890-2', '1234567890-2'],
    ['123.456-3', '123456-3'],
    ['1.234.567.890-2', '1234567890-2'],
    ['800197268 - 4', '800197268-4'],
    ['123456-4', null],
    ['12345-5', null],
    ['12345678901-1', null],
    ['800197268–4', null],
    ['800197268_4', null],
    ['800,197,268-4', null],
    ['８００197268-4', null],
    ['-800197268-4', null],
    ['800197268-4-', null],
    [' - ', null],
    ['800197268-٤', null],
    ['800' + ' '.repeat(5000) + '197268-4', null],
  ])('%j → %j', (entrada, esperado) => {
    expect(normalizarNit(entrada)).toBe(esperado)
  })
})

describe('QA CA-P9-34: el pie no publica una empresa con NIT no válido en el borde', () => {
  it('NIT de 11 dígitos o con DV errado de 6 dígitos: no se muestra nada de la empresa', async () => {
    for (const nit of ['12345678901-1', '123456-4']) {
      const api = simularFetch({
        [RUTA_CONTACTO_PORTAL]: {
          status: 200,
          body: { ...base, empresa: { razonSocial: 'Empresa Ficticia S.A.S.', nit, direccion: 'Calle 10 # 20-30' } },
        },
      })
      const { unmount, container } = render(<PiePortal tipos={[]} conSesion={false} />)
      await waitFor(() => expect(api.llamadasA(RUTA_CONTACTO_PORTAL)).toHaveLength(1))
      await new Promise((r) => setTimeout(r, 0))
      expect(container).not.toHaveTextContent(/NIT|Empresa Ficticia/)
      unmount()
      olvidarContactoPortal()
    }
  })
})

describe('QA CA-P9-57: fallo de envío con el WhatsApp vigente en los formularios que faltaban', () => {
  it('"Compra tu seguro todo riesgo": un 503 muestra el número guardado por el administrador', async () => {
    const { user } = abrir('/seguro-todo-riesgo', {
      [RUTA_CONTACTO_PORTAL]: CONTACTO_300,
      '/api/avisos/seguro-todo-riesgo': { status: 503 },
    })
    await screen.findByRole('form', { name: 'Formulario Compra tu seguro todo riesgo' })
    await waitFor(() => expect(screen.getByRole('link', { name: 'WhatsApp 300 123 4567' })).toBeInTheDocument())
    await user.type(screen.getByLabelText('Nombre'), 'Ana Pérez')
    await user.type(screen.getByLabelText('Teléfono'), '3001234567')
    await user.selectOptions(screen.getByLabelText('Tipo de vehículo'), 'CAMION')
    await user.click(casilla())
    await user.click(screen.getByRole('button', { name: /^Enviar/ }))
    expect(await screen.findByText(FALLO_300)).toBeInTheDocument()
  })

  it('"Busco un vehículo": un 500 muestra el número vigente', async () => {
    const { user } = abrir('/busco-un-vehiculo', {
      [RUTA_CONTACTO_PORTAL]: CONTACTO_300,
      '/api/catalogo/listas-busqueda': LISTAS,
      '/api/busquedas': { status: 500 },
    })
    await screen.findByRole('form', { name: 'Formulario Busco un vehículo' })
    await waitFor(() => expect(screen.getByRole('link', { name: 'WhatsApp 300 123 4567' })).toBeInTheDocument())
    await user.selectOptions(screen.getByLabelText('Categoría'), '2')
    await user.type(screen.getByLabelText('Nombre'), 'Ana Pérez')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '300 123 4567')
    await user.click(casilla())
    await user.click(screen.getByRole('button', { name: /^Enviar/ }))
    expect(await screen.findByText(FALLO_300)).toBeInTheDocument()
  })

  it('"Busco un vehículo": sin conexión y con la lectura del contacto fallida usa el respaldo de D-42', async () => {
    const { user } = abrir('/busco-un-vehiculo', {
      [RUTA_CONTACTO_PORTAL]: { red: true },
      '/api/catalogo/listas-busqueda': LISTAS,
      '/api/busquedas': { red: true },
    })
    await screen.findByRole('form', { name: 'Formulario Busco un vehículo' })
    await user.selectOptions(screen.getByLabelText('Categoría'), '2')
    await user.type(screen.getByLabelText('Nombre'), 'Ana Pérez')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '300 123 4567')
    await user.click(casilla())
    await user.click(screen.getByRole('button', { name: /^Enviar/ }))
    expect(await screen.findByText(FALLO_RESPALDO)).toBeInTheDocument()
  })
})

describe('QA CA-P9-49, 50: ruta desconocida frente al ancla #contenido', () => {
  it('en "Página no encontrada", "Saltar al contenido" mueve el foco al contenido sin cambiar de pantalla', async () => {
    const { user } = abrir('/no-existe', {})
    const h1 = await screen.findByRole('heading', { level: 1, name: 'Página no encontrada' })
    await user.click(screen.getByRole('link', { name: 'Saltar al contenido' }))
    expect(screen.getByRole('main')).toHaveFocus()
    expect(ubicacionActual()).toBe('/no-existe')
    expect(h1).toBeInTheDocument()
    expect(noIndex()).not.toBeNull()
  })

  it('/ con mayúsculas o con espacios no coincide con ninguna ruta: "no encontrada", no la portada', async () => {
    abrir('/TERMINOS', {})
    expect(await screen.findByRole('heading', { level: 1, name: 'Página no encontrada' })).toBeInTheDocument()
  })
})

describe('QA CA-P9-17, 55: términos con sesión de administrador y noindex en el registro', () => {
  it('con sesión de administrador /terminos se ve; P10 (CA-P10-29): noindex mientras esté en revisión legal', async () => {
    abrirApp('/terminos', ['ADMINISTRADOR'])
    const main = await screen.findByRole('main')
    expect(await within(main).findByRole('heading', { level: 1, name: 'Términos y condiciones de uso' })).toBeInTheDocument()
    expect(noIndex()).not.toBeNull()
  })

  it('/cuenta/registro pone noindex', async () => {
    abrir('/cuenta/registro', {})
    await screen.findByRole('checkbox', { name: /Autorizo el tratamiento de mis datos personales/ })
    expect(noIndex()).not.toBeNull()
  })
})

describe('QA CA-P9-21: foco en el h1 de #/privacidad al llegar desde otra ruta', () => {
  it('desde la portada, el foco va al h1 de la política y el título del documento cambia', async () => {
    abrir('/', {})
    irA('/privacidad')
    const h1 = await screen.findByRole('heading', { level: 1, name: 'Política de tratamiento de datos personales' })
    await waitFor(() => expect(h1).toHaveFocus())
    expect(document.title).toBe('Política de tratamiento de datos | Financiamos tu Camión')
    // P10 (CA-P10-29): en revisión legal, noindex.
    expect(noIndex()).not.toBeNull()
  })
})
