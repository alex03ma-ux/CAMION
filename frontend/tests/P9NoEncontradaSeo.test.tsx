// P9 — Página "no encontrada" (RN-SE-005; CA-P9-48 a CA-P9-51) y SEO básico (CA-P9-52 a CA-P9-56).
import { render, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import App from '../src/App'
import { leerRuta, rutaSinIndexar, type Ruta } from '../src/rutas'
import { fijarNoIndex } from '../src/seo'
import { simularFetch } from './helpers'
import { abrirApp } from './p4pDatos'
import { irA } from './helpers'

const DESCRIPCION =
  'Camiones usados en venta en Colombia: NPR, volquetas, tractomulas y maquinaria. Solicita tu crédito para camión o tu seguro todo riesgo por WhatsApp.'

const noIndex = () => document.head.querySelector('meta[name="robots"][content="noindex"]')

function abrirAnonimo(hash: string) {
  const api = simularFetch({})
  irA(hash)
  render(<App />)
  return api
}

describe('leerRuta: rutas desconocidas (CA-P9-48, 49)', () => {
  it.each([['/xyz'], ['/cuenta/otra'], ['/vehiculos/extra'], ['/contactos/abc'], ['/inventario/0'], ['/vehiculo/%E0%A4%A'], ['/terminos/x']])(
    '%s → no encontrada',
    (hash) => {
      expect(leerRuta(hash)).toEqual({ nombre: 'noEncontrada' })
    },
  )

  it.each([[''], ['#'], ['/'], ['#contenido'], ['#algo/raro']])('%s → inicio', (hash) => {
    expect(leerRuta(hash)).toEqual({ nombre: 'inicio' })
  })

  it('las rutas existentes con barra final siguen llevando a su pantalla', () => {
    expect(leerRuta('/vehiculos/')).toEqual(expect.objectContaining({ nombre: 'buscar' }))
    expect(leerRuta('/cuenta/')).toEqual({ nombre: 'cuenta', registro: false })
    expect(leerRuta('/privacidad/')).toEqual({ nombre: 'privacidad' })
    expect(leerRuta('/vehiculo/npr-2020/')).toEqual({ nombre: 'vehiculo', slug: 'npr-2020' })
  })
})

describe('Pantalla "Página no encontrada" (CA-P9-48, 50)', () => {
  it('muestra el título, el texto y los dos enlaces; encabezado y pie normales; noindex y título del documento', async () => {
    abrirAnonimo('/xyz')
    const h1 = await screen.findByRole('heading', { level: 1, name: 'Página no encontrada' })
    expect(h1).toHaveAttribute('tabindex', '-1')
    const main = screen.getByRole('main')
    expect(within(main).getByText('La página que buscas no existe o cambió de dirección.')).toBeInTheDocument()
    expect(within(main).getByRole('link', { name: 'Ir al inicio' })).toHaveAttribute('href', '/')
    expect(within(main).getByRole('link', { name: 'Ver todos los vehículos' })).toHaveAttribute('href', '/vehiculos')
    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
    expect(document.title).toBe('Página no encontrada | Financiamos tu Camión')
    expect(noIndex()).not.toBeNull()
  })

  it('al llegar desde otra pantalla, el foco va al h1; al volver al inicio se quita el noindex', async () => {
    abrirAnonimo('/')
    irA('/cuenta/otra')
    const h1 = await screen.findByRole('heading', { level: 1, name: 'Página no encontrada' })
    await waitFor(() => expect(h1).toHaveFocus())
    expect(noIndex()).not.toBeNull()
    irA('/')
    await waitFor(() => expect(screen.queryByRole('heading', { level: 1, name: 'Página no encontrada' })).not.toBeInTheDocument())
    expect(noIndex()).toBeNull()
  })

  it('#contenido (ancla de "Saltar al contenido") sigue mostrando la portada', async () => {
    abrirAnonimo('#contenido')
    await waitFor(() => expect(document.title).toBe('Venta de camiones usados y crédito | Financiamos tu Camión'))
    expect(screen.queryByRole('heading', { level: 1, name: 'Página no encontrada' })).not.toBeInTheDocument()
  })
})

describe('Regresión de rutas protegidas y ficha inexistente (CA-P9-51)', () => {
  it('sin sesión, #/inventario lleva al inicio de sesión, no a "no encontrada"', async () => {
    abrirAnonimo('/inventario')
    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Página no encontrada' })).not.toBeInTheDocument()
  })

  it('un comprador en #/cuenta/usuarios ve "sin permiso", no "no encontrada"', async () => {
    abrirApp('/cuenta/usuarios', ['COMPRADOR'])
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Página no encontrada' })).not.toBeInTheDocument()
  })

  it('la ficha de un vehículo inexistente conserva su propio mensaje', async () => {
    simularFetch({ '/api/catalogo/publicaciones/no-existe': { status: 404, body: { error: 'No encontrado.' } } })
    irA('/vehiculo/no-existe')
    render(<App />)
    expect(await screen.findByText(/no está disponible/i)).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Página no encontrada' })).not.toBeInTheDocument()
  })
})

describe('noindex en pantallas privadas (CA-P9-55)', () => {
  const privadas: Ruta[] = [
    { nombre: 'cuenta', registro: false },
    { nombre: 'inventario' },
    { nombre: 'inventarioNuevo' },
    { nombre: 'inventarioVehiculo', id: 1 },
    { nombre: 'publicaciones' },
    { nombre: 'contactos' },
    { nombre: 'contacto', id: 1 },
    { nombre: 'ventas' },
    { nombre: 'misSolicitudes' },
    { nombre: 'misIndicadores' },
    { nombre: 'restablecerContrasena' },
    { nombre: 'noEncontrada' },
  ]
  const publicas: Ruta[] = [
    { nombre: 'inicio' },
    leerRuta('/vehiculos'),
    { nombre: 'vehiculo', slug: 'x' },
    { nombre: 'privacidad' },
    { nombre: 'terminos' },
    { nombre: 'vendeTuCamion' },
    { nombre: 'seguroTodoRiesgo' },
    { nombre: 'credito', slug: null },
    { nombre: 'buscoVehiculo' },
  ]

  it('privadas sí, públicas no', () => {
    for (const r of privadas) expect(rutaSinIndexar(r), r.nombre).toBe(true)
    for (const r of publicas) expect(rutaSinIndexar(r), r.nombre).toBe(false)
  })

  it('fijarNoIndex crea una sola etiqueta y la quita', () => {
    fijarNoIndex(true)
    fijarNoIndex(true)
    expect(document.head.querySelectorAll('meta[name="robots"]')).toHaveLength(1)
    fijarNoIndex(false)
    expect(noIndex()).toBeNull()
  })

  it('/cuenta pone noindex y el catálogo lo quita', async () => {
    abrirAnonimo('/cuenta')
    await screen.findByRole('heading', { name: 'Iniciar sesión' })
    expect(noIndex()).not.toBeNull()
    irA('/vehiculos')
    await waitFor(() => expect(noIndex()).toBeNull())
  })
})

describe('SEO básico (CA-P9-52 a 54; CA-P10-14, 51)', () => {
  const archivos = (import.meta.glob('../{index.html,public/robots.txt}', {
    query: '?raw',
    import: 'default',
    eager: true,
  }) as Record<string, string>)
  const html = archivos['../index.html']
  const robots = archivos['../public/robots.txt']

  it('index.html: meta description aprobada tal cual, sin tasas, aprobaciones ni "peritados"', () => {
    const doc = new DOMParser().parseFromString(html, 'text/html')
    const descripcion = doc.querySelector('meta[name="description"]')?.getAttribute('content')
    expect(descripcion).toBe(DESCRIPCION)
    expect(descripcion).not.toMatch(/tasa|aprobad|aprobaci|peritad/i)
    expect(doc.querySelector('title')?.textContent).toBe('Venta de camiones usados y crédito | Financiamos tu Camión')
    expect(doc.documentElement.getAttribute('lang')).toBe('es')
  })

  it('P10 (CA-P10-07, 09): index.html enlaza favicon.ico, el PNG de 32×32, el ícono de Apple y el manifiesto (sin favicon.svg)', () => {
    const doc = new DOMParser().parseFromString(html, 'text/html')
    const iconos = Array.from(doc.querySelectorAll('link[rel="icon"]')).map((l) => [l.getAttribute('href'), l.getAttribute('type')])
    expect(iconos).toEqual([
      ['/favicon.ico', null],
      ['/marca/favicon-32.png', 'image/png'],
    ])
    expect(doc.querySelector('link[rel="apple-touch-icon"]')?.getAttribute('href')).toBe('/marca/apple-touch-icon.png')
    expect(doc.querySelector('link[rel="manifest"]')?.getAttribute('href')).toBe('/site.webmanifest')
    expect(doc.querySelector('meta[name="theme-color"]')?.getAttribute('content')).toBe('#d71920')
    expect(html).not.toContain('favicon.svg')
  })

  it('P10 (CA-P10-51, §3.1): no hay robots.txt estático; lo genera la API según Portal:PermitirIndexacion (D-89 A)', () => {
    expect(robots).toBeUndefined()
  })
})
