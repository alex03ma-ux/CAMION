// P10 (D-87, D-91 A, D-92): logos, textos de marca, favicon y manifiesto (CA-P10-02 a 20).
import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import {
  ALT_LOGO_CUADRADO,
  ALT_LOGO_HORIZONTAL,
  LEMA,
  MARCA,
  NOMBRE_PORTAL,
  NOMBRE_SITIO,
  SUFIJO_TITULO,
  TITULO_POR_DEFECTO,
} from '../src/marca'
import { abrirApp, respuestaFiltros, RUTA_FILTROS, RUTA_INICIO } from './catalogoDatos'
import { simularFetch } from './helpers'

const INICIO_VACIO = { status: 200, body: { destacados: [], recientes: [] } }

describe('Módulo de marca (CA-P10-13, 14)', () => {
  it('define los nombres de D-87 en un solo sitio', () => {
    expect(MARCA).toBe('VCA Automotores')
    expect(NOMBRE_PORTAL).toBe('Venta de Camiones')
    expect(LEMA).toBe('Financiamos tu Camión')
    expect(SUFIJO_TITULO).toBe('Financiamos tu Camión')
    expect(NOMBRE_SITIO).toBe('Venta de Camiones – Financiamos tu Camión')
    expect(TITULO_POR_DEFECTO).toBe('Venta de camiones usados y crédito | Financiamos tu Camión')
    expect(TITULO_POR_DEFECTO.length).toBeLessThanOrEqual(60)
  })
})

describe('Logos en el encabezado y el pie (CA-P10-02 a 06)', () => {
  it('el encabezado muestra el logo horizontal con WebP y PNG, tamaño fijo y enlace a la portada', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: INICIO_VACIO })
    abrirApp('/')
    const banner = screen.getByRole('banner')
    const enlace = within(banner).getByRole('link', { name: ALT_LOGO_HORIZONTAL })
    expect(enlace).toHaveAttribute('href', '/')
    expect(ALT_LOGO_HORIZONTAL).toBe('VCA Automotores, Venta de Camiones. Financiamos tu Camión')
    const img = within(enlace).getByRole('img')
    expect(img).toHaveAttribute('src', '/marca/logo-horizontal.png')
    expect(img).toHaveAttribute('width', '322')
    expect(img).toHaveAttribute('height', '56')
    const fuente = enlace.querySelector('picture source')
    expect(fuente).toHaveAttribute('srcset', '/marca/logo-horizontal.webp')
    expect(fuente).toHaveAttribute('type', 'image/webp')
    // Sin texto oculto duplicado: el único contenido del enlace es la imagen.
    expect(enlace.textContent).toBe('')
  })

  it('el pie oscuro usa el logo cuadrado y nunca el horizontal; © VCA Automotores', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: INICIO_VACIO })
    abrirApp('/')
    const pie = screen.getByRole('contentinfo')
    const img = within(pie).getByRole('img', { name: ALT_LOGO_CUADRADO })
    expect(img).toHaveAttribute('src', '/marca/logo-cuadrado.png')
    expect(img.closest('picture')?.querySelector('source')).toHaveAttribute('srcset', '/marca/logo-cuadrado.webp')
    expect(pie.innerHTML).not.toContain('logo-horizontal')
    expect(within(pie).getByText(/© \d{4} VCA Automotores/)).toBeInTheDocument()
    expect(within(pie).getByText('Portal de venta de vehículos de trabajo en Colombia.')).toBeInTheDocument()
  })

  it('no queda el logo de texto anterior en ninguna parte (CA-P10-06)', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: INICIO_VACIO })
    abrirApp('/')
    expect(document.querySelector('.cat-logo-camion, .cat-logo-eyebrow, .cat-logo-nombre')).toBeNull()
    expect(document.body.textContent).not.toMatch(/Financiamos Tu/)
  })
})

describe('Portada (CA-P10-20, D-92)', () => {
  it('h1 "Venta de camiones usados" y la frase de D-42 como subtítulo', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: INICIO_VACIO })
    abrirApp('/')
    const h1s = screen.getAllByRole('heading', { level: 1 })
    expect(h1s).toHaveLength(1)
    expect(h1s[0]).toHaveTextContent('Venta de camiones usados')
    const frase = document.querySelector('.cat-portada-frase')
    expect(frase?.tagName).toBe('P')
    expect(frase).toHaveTextContent('Tu próximo negocio empieza con el vehículo correcto')
  })
})

describe('Textos de marca en el código (CA-P10-18, 19)', () => {
  const fuentes = import.meta.glob('../src/**/*.{ts,tsx}', { query: '?raw', import: 'default', eager: true }) as Record<
    string,
    string
  >

  it('ningún archivo de src escribe "Financiamos Tu Camión" con "Tu" en mayúscula', () => {
    const conMayuscula = Object.entries(fuentes)
      .filter(([, texto]) => /Financiamos Tu Camión/.test(texto))
      .map(([archivo]) => archivo)
    expect(conMayuscula).toEqual([])
  })
})

describe('Manifiesto e íconos (CA-P10-07 a 10, 12)', () => {
  const archivos = import.meta.glob('../{index.html,public/site.webmanifest}', {
    query: '?raw',
    import: 'default',
    eager: true,
  }) as Record<string, string>
  const imagenes = import.meta.glob('../public/{favicon.ico,marca/*}', { query: '?url', import: 'default', eager: true })

  it('site.webmanifest con nombre, colores e íconos 192 y 512 de /marca/', () => {
    const manifiesto = JSON.parse(archivos['../public/site.webmanifest']) as Record<string, unknown>
    expect(manifiesto).toEqual({
      name: 'Venta de Camiones – Financiamos tu Camión',
      short_name: 'VCA Camiones',
      lang: 'es-CO',
      start_url: '/',
      display: 'browser',
      background_color: '#ffffff',
      theme_color: '#d71920',
      icons: [
        { src: '/marca/icon-192.png', sizes: '192x192', type: 'image/png' },
        { src: '/marca/icon-512.png', sizes: '512x512', type: 'image/png' },
      ],
    })
  })

  it('existen todos los derivados de la tabla de §2.1 (y no los originales de docs/marca)', () => {
    const nombres = Object.keys(imagenes)
      .map((ruta) => ruta.replace('../public/', ''))
      .sort()
    expect(nombres).toEqual([
      'favicon.ico',
      'marca/apple-touch-icon.png',
      'marca/favicon-32.png',
      'marca/icon-192.png',
      'marca/icon-512.png',
      'marca/logo-cuadrado.png',
      'marca/logo-cuadrado.webp',
      'marca/logo-horizontal.png',
      'marca/logo-horizontal.webp',
      'marca/og-portal.jpg',
    ])
  })

  it('index.html usa rutas absolutas para íconos y manifiesto', () => {
    const doc = new DOMParser().parseFromString(archivos['../index.html'], 'text/html')
    const rutas = Array.from(doc.querySelectorAll('link[rel~="icon"], link[rel="apple-touch-icon"], link[rel="manifest"]')).map(
      (l) => l.getAttribute('href') ?? '',
    )
    expect(rutas.length).toBeGreaterThanOrEqual(4)
    for (const ruta of rutas) expect(ruta.startsWith('/')).toBe(true)
  })
})
