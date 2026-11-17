// P9 — Teléfono de los mensajes tomado de la configuración (HU-081, RN-PA-001, D-63 A; CA-P9-57 a CA-P9-61) y caché
// en memoria de useContactoPortal (nota §6 de docs/criterios/P9-criterios.md).
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import App from '../src/App'
import { mensajeContactoFallo, mensajeErrorEnvio, MENSAJE_CONTACTO_FALLO } from '../src/api/contactos'
import { ApiError } from '../src/api/http'
import { PiePortal } from '../src/components/PiePortal'
import { Privacidad } from '../src/components/Privacidad'
import { recargarContactoPortal } from '../src/components/useContactoPortal'
import { CONTACTO_PORTAL_POR_DEFECTO, RUTA_CONTACTO_PORTAL, RUTA_FILTROS, simularFetch, type RespuestaSimulada } from './helpers'
import { detalle, rutaDetalle, V1 } from './catalogoDatos'
import { abrirApp } from './p4pDatos'
import { irA } from './helpers'

declare global {
  interface ImportMeta {
    glob(patron: string, opciones: { query: string; import: string; eager: true }): Record<string, string>
  }
}

const RUTA_CREDITOS = '/api/creditos'
const RUTA_LISTAS = '/api/catalogo/listas-busqueda'
const RUTA_MIS = '/api/mis-solicitudes?pagina=1&tamano=20'

const base = (CONTACTO_PORTAL_POR_DEFECTO as { body: Record<string, unknown> }).body
const CONTACTO_300: RespuestaSimulada = {
  status: 200,
  body: { ...base, whatsapp: '3001234567', whatsappEnlace: 'https://wa.me/573001234567' },
}
const FALLO_300 =
  'No pudimos enviar tus datos. Intenta de nuevo en unos minutos o escríbenos por WhatsApp al 300 123 4567.'
const FALLO_RESPALDO =
  'No pudimos enviar tus datos. Intenta de nuevo en unos minutos o escríbenos por WhatsApp al 316 615 2907.'

function abrir(hash: string, rutas: Record<string, RespuestaSimulada | RespuestaSimulada[]>) {
  const api = simularFetch(rutas)
  irA(hash)
  render(<App />)
  return { api, user: userEvent.setup() }
}

async function llenarCredito(user: ReturnType<typeof userEvent.setup>) {
  await screen.findByRole('form', { name: 'Formulario de solicitud de crédito' })
  await user.type(screen.getByLabelText('Nombre'), 'Ana Pérez')
  await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '300 765 4321')
  await user.click(screen.getByRole('checkbox', { name: /Autorizo el tratamiento de mis datos personales/ }))
  await user.click(screen.getByRole('button', { name: 'Enviar' }))
}

describe('Mensajes con el WhatsApp vigente (CA-P9-57, 59)', () => {
  it('mensajeErrorEnvio usa el número vigente en 5xx, sin conexión o error desconocido; los demás, el de la API', () => {
    expect(mensajeErrorEnvio(new ApiError(503, 'x'), '3001234567')).toBe(FALLO_300)
    expect(mensajeErrorEnvio(new ApiError(0, 'x'), '3001234567')).toBe(FALLO_300)
    expect(mensajeErrorEnvio(new Error('raro'), '3001234567')).toBe(FALLO_300)
    expect(mensajeErrorEnvio(new ApiError(429, 'Has enviado muchas solicitudes.'), '3001234567')).toBe(
      'Has enviado muchas solicitudes.',
    )
    expect(mensajeContactoFallo('3166152907')).toBe(FALLO_RESPALDO)
    expect(MENSAJE_CONTACTO_FALLO).toBe(FALLO_RESPALDO)
  })

  it('crédito: un 500 muestra el WhatsApp guardado por el administrador', async () => {
    const { user } = abrir('/credito', {
      [RUTA_CONTACTO_PORTAL]: CONTACTO_300,
      [RUTA_CREDITOS]: { status: 500, body: { error: 'interno' } },
    })
    await waitFor(() => expect(screen.getByRole('link', { name: 'WhatsApp 300 123 4567' })).toBeInTheDocument())
    await llenarCredito(user)
    expect(await screen.findByText(FALLO_300)).toBeInTheDocument()
  })

  it('crédito: sin conexión y con la lectura del contacto fallida usa el respaldo de D-42', async () => {
    const { user } = abrir('/credito', {
      [RUTA_CONTACTO_PORTAL]: { red: true },
      [RUTA_CREDITOS]: { red: true },
    })
    await llenarCredito(user)
    expect(await screen.findByText(FALLO_RESPALDO)).toBeInTheDocument()
  })

  it('contacto de la ficha: un 502 muestra el WhatsApp vigente', async () => {
    const d = detalle(V1)
    const { user } = abrir(`/vehiculo/${d.slug}`, {
      [RUTA_CONTACTO_PORTAL]: CONTACTO_300,
      [rutaDetalle(d.slug)]: { status: 200, body: d },
      '/api/contactos': { status: 502 },
    })
    await screen.findByRole('heading', { level: 1, name: d.titulo })
    await waitFor(() => expect(screen.getByRole('link', { name: 'WhatsApp 300 123 4567' })).toBeInTheDocument())
    await user.type(screen.getByLabelText('Nombre'), 'Ana Pérez')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '300 765 4321')
    await user.click(screen.getByRole('checkbox', { name: /Autorizo el tratamiento de mis datos personales/ }))
    await user.click(screen.getByRole('button', { name: /^Enviar/ }))
    expect(await screen.findByText(FALLO_300)).toBeInTheDocument()
  })

  it('aviso "Vende tu camión": un 500 muestra el WhatsApp vigente', async () => {
    const { user } = abrir('/vende-tu-camion', {
      [RUTA_CONTACTO_PORTAL]: CONTACTO_300,
      '/api/avisos/vende-tu-camion': { status: 500 },
    })
    await waitFor(() => expect(screen.getByRole('link', { name: 'WhatsApp 300 123 4567' })).toBeInTheDocument())
    await user.type(screen.getByLabelText('Nombre'), 'Ana Pérez')
    await user.type(screen.getByLabelText(/Teléfono/), '300 765 4321')
    await user.type(screen.getByLabelText(/Descripción/), 'Camión NPR 2018 en buen estado')
    await user.click(screen.getByRole('checkbox', { name: /Autorizo el tratamiento de mis datos personales/ }))
    await user.click(screen.getByRole('button', { name: /^Enviar/ }))
    expect(await screen.findByText(FALLO_300)).toBeInTheDocument()
  })
})

describe('Textos fijos con el número vigente (CA-P9-58, 60)', () => {
  it('"Busco un vehículo" sin categorías muestra el WhatsApp vigente', async () => {
    abrir('/busco-un-vehiculo', {
      [RUTA_CONTACTO_PORTAL]: CONTACTO_300,
      [RUTA_LISTAS]: { status: 200, body: { tipos: [], marcas: [], departamentos: [] } },
    })
    expect(
      await screen.findByText('Por ahora no podemos recibir búsquedas aquí. Escríbenos por WhatsApp al 300 123 4567.'),
    ).toBeInTheDocument()
  })

  it('"Mis solicitudes" muestra el WhatsApp vigente en la introducción', async () => {
    abrirApp('/mis-solicitudes', ['COMPRADOR'], {
      [RUTA_CONTACTO_PORTAL]: CONTACTO_300,
      [RUTA_MIS]: { status: 200, body: { items: [], total: 0, pagina: 1, tamano: 20 } },
    })
    expect(await screen.findByText(/escríbenos por WhatsApp al 300 123 4567\./)).toBeInTheDocument()
  })

  it('"Mis solicitudes" con la lectura fallida usa el respaldo', async () => {
    abrirApp('/mis-solicitudes', ['COMPRADOR'], {
      [RUTA_CONTACTO_PORTAL]: { red: true },
      [RUTA_MIS]: { status: 200, body: { items: [], total: 0, pagina: 1, tamano: 20 } },
    })
    expect(await screen.findByText(/escríbenos por WhatsApp al 316 615 2907\./)).toBeInTheDocument()
  })

  it('el enlace de WhatsApp de la confirmación del crédito usa el número vigente', async () => {
    const { user } = abrir('/credito', {
      [RUTA_CONTACTO_PORTAL]: CONTACTO_300,
      [RUTA_CREDITOS]: { status: 201, body: { mensaje: 'Listo.' } },
    })
    await waitFor(() => expect(screen.getByRole('link', { name: 'WhatsApp 300 123 4567' })).toBeInTheDocument())
    await llenarCredito(user)
    const enlace = await screen.findByRole('link', { name: /Escribir por WhatsApp/ })
    expect(enlace.getAttribute('href')).toMatch(/^https:\/\/wa\.me\/573001234567\?text=/)
  })
})

describe('Caché en memoria del contacto del portal', () => {
  it('el pie y la página de privacidad comparten una sola petición', async () => {
    const api = simularFetch({ [RUTA_CONTACTO_PORTAL]: CONTACTO_300 })
    render(
      <>
        <Privacidad />
        <PiePortal tipos={[]} conSesion={false} />
      </>,
    )
    await screen.findByRole('link', { name: 'WhatsApp 300 123 4567' })
    expect(api.llamadasA(RUTA_CONTACTO_PORTAL)).toHaveLength(1)
  })

  it('un componente montado después usa el valor guardado sin volver a pedirlo', async () => {
    const api = simularFetch({ [RUTA_CONTACTO_PORTAL]: CONTACTO_300 })
    const { rerender } = render(<PiePortal tipos={[]} conSesion={false} />)
    await screen.findByRole('link', { name: 'WhatsApp 300 123 4567' })
    rerender(
      <>
        <PiePortal tipos={[]} conSesion={false} />
        <Privacidad />
      </>,
    )
    expect(screen.getAllByRole('link', { name: /300 123 4567/ }).length).toBeGreaterThanOrEqual(2)
    expect(api.llamadasA(RUTA_CONTACTO_PORTAL)).toHaveLength(1)
  })

  it('si la lectura falla no se guarda: el siguiente intento vuelve a pedirlo', async () => {
    const api = simularFetch({ [RUTA_CONTACTO_PORTAL]: [{ red: true }, CONTACTO_300] })
    const { unmount } = render(<PiePortal tipos={[]} conSesion={false} />)
    await waitFor(() => expect(api.llamadasA(RUTA_CONTACTO_PORTAL)).toHaveLength(1))
    expect(screen.getByRole('link', { name: 'WhatsApp 316 615 2907' })).toBeInTheDocument()
    unmount()
    render(<PiePortal tipos={[]} conSesion={false} />)
    expect(await screen.findByRole('link', { name: 'WhatsApp 300 123 4567' })).toBeInTheDocument()
    expect(api.llamadasA(RUTA_CONTACTO_PORTAL)).toHaveLength(2)
  })

  it('recargarContactoPortal actualiza los componentes montados', async () => {
    const api = simularFetch({ [RUTA_CONTACTO_PORTAL]: [CONTACTO_PORTAL_POR_DEFECTO, CONTACTO_300] })
    render(<PiePortal tipos={[]} conSesion={false} />)
    await waitFor(() => expect(api.llamadasA(RUTA_CONTACTO_PORTAL)).toHaveLength(1))
    recargarContactoPortal()
    expect(await screen.findByRole('link', { name: 'WhatsApp 300 123 4567' })).toBeInTheDocument()
  })

  it('la app completa pide el contacto una sola vez por carga', async () => {
    const api = simularFetch({ [RUTA_FILTROS]: { status: 200, body: { tipos: [], marcas: [], rangos: {} } } })
    irA('/privacidad')
    render(<App />)
    await screen.findByRole('heading', { level: 1, name: /Política de tratamiento de datos/ })
    await waitFor(() => expect(api.llamadasA(RUTA_CONTACTO_PORTAL)).toHaveLength(1))
  })
})

describe('Sin el número fijo en los textos visibles (CA-P9-61)', () => {
  it('solo los valores de respaldo y el ejemplo de formato contienen 316 615 2907', () => {
    const fuentes = import.meta.glob('../src/**/*.{ts,tsx}', { query: '?raw', import: 'default', eager: true })
    const permitidos = [
      '../src/api/parametros.ts',
      '../src/components/PiePortal.tsx',
      '../src/api/contactos.ts',
      '../src/components/AdminParametros.tsx',
    ]
    const conNumero = Object.entries(fuentes)
      .filter(([ruta, texto]) => !permitidos.includes(ruta) && /316 ?615 ?2907/.test(texto))
      .map(([ruta]) => ruta)
    expect(Object.keys(fuentes).length).toBeGreaterThan(50)
    expect(conNumero).toEqual([])
  })
})
