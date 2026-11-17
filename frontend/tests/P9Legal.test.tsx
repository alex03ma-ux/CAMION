// P9 — Páginas de términos y privacidad, enlaces legales del pie y datos de la empresa publicados
// (HU-085, HU-086, RN-PA-005, RN-PA-006, D-44, D-83 A; CA-P9-17 a CA-P9-23, CA-P9-34, CA-P9-35).
import { render, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import App from '../src/App'
import { TEXTO_FRANJA_REVISION, TEXTOS_LEGALES_EN_REVISION } from '../src/legal'
import { PiePortal } from '../src/components/PiePortal'
import { Privacidad } from '../src/components/Privacidad'
import { Terminos } from '../src/components/Terminos'
import { olvidarContactoPortal } from '../src/components/useContactoPortal'
import { HASH_TERMINOS, leerRuta } from '../src/rutas'
import { CONTACTO_PORTAL_POR_DEFECTO, RUTA_CONTACTO_PORTAL, simularFetch, type RespuestaSimulada } from './helpers'
import { abrirApp } from './p4pDatos'

const base = (CONTACTO_PORTAL_POR_DEFECTO as { body: Record<string, unknown> }).body
const EMPRESA = { razonSocial: 'Financiamos Tu Camión S.A.S.', nit: '800197268-4', direccion: 'Calle 10 # 20-30, Bogotá' }
const CON_EMPRESA: RespuestaSimulada = { status: 200, body: { ...base, empresa: EMPRESA } }
const LINEA_EMPRESA =
  'Financiamos Tu Camión S.A.S. · NIT 800.197.268-4 · Dirección de notificaciones: Calle 10 # 20-30, Bogotá'
/** Marcas de los borradores de docs/legal que nunca deben verse en el portal (CA-P9-19). */
const MARCAS_BORRADOR = /NOTA PARA REVISIÓN|\[RAZÓN SOCIAL\]|\[NIT\]|\[DOMINIO\]|BORRADOR/

describe('Rutas legales (CA-P9-23)', () => {
  it('#/terminos con y sin barra final', () => {
    expect(HASH_TERMINOS).toBe('#/terminos')
    expect(leerRuta('#/terminos')).toEqual({ nombre: 'terminos' })
    expect(leerRuta('#/terminos/')).toEqual({ nombre: 'terminos' })
    expect(leerRuta('#/privacidad/')).toEqual({ nombre: 'privacidad' })
  })
})

describe('Constante única del aviso de revisión (CA-P9-18, 20)', () => {
  it('mientras los textos están en revisión, las dos páginas muestran la misma franja', () => {
    expect(TEXTOS_LEGALES_EN_REVISION).toBe(true)
    expect(TEXTO_FRANJA_REVISION).toBe('En revisión legal')
    simularFetch({})
    const { unmount } = render(<Terminos />)
    expect(screen.getByRole('note')).toHaveTextContent(TEXTO_FRANJA_REVISION)
    expect(screen.getByRole('note')).toHaveClass('franja-borrador')
    unmount()
    render(<Privacidad />)
    expect(screen.getByRole('note')).toHaveTextContent(TEXTO_FRANJA_REVISION)
  })
})

describe('#/terminos (CA-P9-17, 19, 21)', () => {
  it('anónimo: título, franja, aviso, contacto vigente e "Ir al inicio"; sin texto de los borradores', async () => {
    const api = simularFetch({
      [RUTA_CONTACTO_PORTAL]: { status: 200, body: { ...base, whatsapp: '3001234567', whatsappEnlace: 'https://wa.me/573001234567' } },
    })
    window.location.hash = '#/terminos'
    render(<App />)
    const h1 = await screen.findByRole('heading', { level: 1, name: 'Términos y condiciones de uso' })
    expect(document.title).toBe('Términos y condiciones | Financiamos Tu Camión')
    const main = screen.getByRole('main')
    expect(within(main).getByRole('note')).toHaveTextContent('En revisión legal')
    expect(within(main).getByText(/se publicarán en esta página cuando termine su revisión legal/)).toBeInTheDocument()
    expect(await within(main).findByRole('link', { name: /300 123 4567/ })).toHaveAttribute('href', 'https://wa.me/573001234567')
    expect(within(main).getByRole('link', { name: 'Ir al inicio' })).toHaveAttribute('href', '#/')
    expect(main).not.toHaveTextContent(MARCAS_BORRADOR)
    // No exige sesión: no se llama a /api/auth/me.
    expect(api.llamadasAuth()).toHaveLength(0)
    expect(h1).toHaveAttribute('tabindex', '-1')
  })

  it('al llegar desde otra ruta, el foco va al h1', async () => {
    simularFetch({})
    render(<App />)
    window.location.hash = '#/terminos'
    window.dispatchEvent(new HashChangeEvent('hashchange'))
    const h1 = await screen.findByRole('heading', { level: 1, name: 'Términos y condiciones de uso' })
    await waitFor(() => expect(h1).toHaveFocus())
  })

  it('con sesión de asesor también se ve', async () => {
    abrirApp('#/terminos', ['ASESOR'])
    expect(await screen.findByRole('heading', { level: 1, name: 'Términos y condiciones de uso' })).toBeInTheDocument()
  })
})

describe('#/privacidad (CA-P9-18, 19, 21)', () => {
  it('franja "En revisión legal", título del documento y sin texto de los borradores', async () => {
    simularFetch({})
    window.location.hash = '#/privacidad'
    render(<App />)
    await screen.findByRole('heading', { level: 1, name: 'Política de tratamiento de datos personales' })
    expect(document.title).toBe('Política de tratamiento de datos | Financiamos Tu Camión')
    const main = screen.getByRole('main')
    expect(within(main).getByRole('note')).toHaveTextContent(/^En revisión legal$/)
    expect(main).not.toHaveTextContent(MARCAS_BORRADOR)
  })
})

describe('Pie: enlaces legales (CA-P9-22)', () => {
  it.each([[false], [true]])('con sesión = %s muestra Términos y Política en la franja inferior, misma pestaña', (conSesion) => {
    simularFetch({})
    render(<PiePortal tipos={[]} conSesion={conSesion} />)
    const legal = screen.getByRole('navigation', { name: 'Información legal' })
    const terminos = within(legal).getByRole('link', { name: 'Términos y condiciones' })
    const politica = within(legal).getByRole('link', { name: 'Política de tratamiento de datos' })
    expect(terminos).toHaveAttribute('href', '#/terminos')
    expect(politica).toHaveAttribute('href', '#/privacidad')
    expect(terminos).not.toHaveAttribute('target')
    expect(politica).not.toHaveAttribute('target')
    expect(legal.closest('.cat-pie-legal')).toHaveTextContent(/© \d{4} Financiamos Tu Camión/)
  })
})

describe('Datos de la empresa publicados (CA-P9-34, 35; D-83 A)', () => {
  it('el pie muestra razón social, NIT con puntos y dirección cuando llegan los tres', async () => {
    simularFetch({ [RUTA_CONTACTO_PORTAL]: CON_EMPRESA })
    render(<PiePortal tipos={[]} conSesion={false} />)
    expect(await screen.findByText(LINEA_EMPRESA)).toBeInTheDocument()
  })

  it('con empresa null, sin el campo o si la lectura falla, no se muestra nada de la empresa', async () => {
    for (const respuesta of [
      { status: 200, body: { ...base, empresa: null } },
      CONTACTO_PORTAL_POR_DEFECTO,
      { red: true } as const,
    ] satisfies RespuestaSimulada[]) {
      const api = simularFetch({ [RUTA_CONTACTO_PORTAL]: respuesta })
      const { unmount, container } = render(<PiePortal tipos={[]} conSesion={false} />)
      await waitFor(() => expect(api.llamadasA(RUTA_CONTACTO_PORTAL)).toHaveLength(1))
      await screen.findByRole('link', { name: 'WhatsApp 316 615 2907' })
      expect(container).not.toHaveTextContent(/NIT|Dirección de notificaciones/)
      unmount()
      olvidarContactoPortal()
    }
  })

  it('datos incompletos o con NIT no válido no se publican', async () => {
    for (const empresa of [
      { ...EMPRESA, direccion: '' },
      { ...EMPRESA, nit: '800197268-5' },
      { razonSocial: 'X S.A.S.', nit: '800197268-4' },
    ]) {
      const api = simularFetch({ [RUTA_CONTACTO_PORTAL]: { status: 200, body: { ...base, empresa } } })
      const { unmount, container } = render(<PiePortal tipos={[]} conSesion={false} />)
      await waitFor(() => expect(api.llamadasA(RUTA_CONTACTO_PORTAL)).toHaveLength(1))
      await new Promise((r) => setTimeout(r, 0))
      expect(container).not.toHaveTextContent(/NIT/)
      unmount()
      olvidarContactoPortal()
    }
  })

  it('el texto de la empresa se muestra como texto, nunca como HTML', async () => {
    simularFetch({
      [RUTA_CONTACTO_PORTAL]: { status: 200, body: { ...base, empresa: { ...EMPRESA, razonSocial: '<b>Negrita</b> S.A.S.' } } },
    })
    const { container } = render(<PiePortal tipos={[]} conSesion={false} />)
    expect(await screen.findByText(/<b>Negrita<\/b> S\.A\.S\. · NIT/)).toBeInTheDocument()
    expect(container.querySelector('b')).toBeNull()
  })

  it('las páginas legales muestran los datos como "Responsable"', async () => {
    simularFetch({ [RUTA_CONTACTO_PORTAL]: CON_EMPRESA })
    const { unmount } = render(<Terminos />)
    const responsable = await screen.findByRole('region', { name: 'Responsable' })
    expect(responsable).toHaveTextContent('Financiamos Tu Camión S.A.S.')
    expect(responsable).toHaveTextContent('800.197.268-4')
    expect(responsable).toHaveTextContent('Calle 10 # 20-30, Bogotá')
    unmount()
    render(<Privacidad />)
    expect(screen.getByRole('region', { name: 'Responsable' })).toBeInTheDocument()
  })

  it('sin empresa, las páginas legales no muestran "Responsable"', async () => {
    const api = simularFetch({})
    render(<Terminos />)
    await waitFor(() => expect(api.llamadasA(RUTA_CONTACTO_PORTAL)).toHaveLength(1))
    expect(screen.queryByRole('region', { name: 'Responsable' })).not.toBeInTheDocument()
  })
})
