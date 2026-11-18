// P10: títulos, descripciones, robots, canonical, vista previa y JSON-LD (CA-P10-14, 21 a 32, 61 a 68, 75, 80, 82;
// D-89 A, D-90 C).
import { act, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { etiquetasHtml } from '../etiquetasHtml'
import { CONTACTO_PORTAL_RESPALDO } from '../src/api/parametros'
import { fijarJsonLd, jsonLdSeguro } from '../src/seo'
import {
  conSufijo,
  descripcionVehiculo,
  jsonLdMigas,
  jsonLdOrganizacion,
  jsonLdVehiculo,
  recortar,
  tituloVehiculo,
} from '../src/seoDatos'
import {
  abrirApp,
  C1,
  detalle,
  pagina,
  respuestaFiltros,
  RUTA_FILTROS,
  RUTA_INICIO,
  rutaDetalle,
  rutaPublicaciones,
  V1,
  V2,
} from './catalogoDatos'
import { CONTACTO_PORTAL_POR_DEFECTO, irA, RUTA_CONTACTO_PORTAL, simularFetch, type RespuestaSimulada } from './helpers'

const ORIGEN = window.location.origin
const INICIO = { status: 200, body: { destacados: [], recientes: [V1] } }

const meta = (nombre: string) => document.head.querySelector(`meta[name="${nombre}"]`)?.getAttribute('content') ?? null
const og = (nombre: string) => document.head.querySelector(`meta[property="${nombre}"]`)?.getAttribute('content') ?? null
const canonica = () => document.head.querySelector('link[rel="canonical"]')?.getAttribute('href') ?? null
const jsonLd = (marca: string) => {
  const s = document.head.querySelector(`script[type="application/ld+json"][data-camion="${marca}"]`)
  return s ? (JSON.parse(s.textContent ?? '') as Record<string, unknown>) : null
}

function datos(extra: Record<string, RespuestaSimulada> = {}) {
  return {
    [RUTA_FILTROS]: respuestaFiltros(),
    [RUTA_INICIO]: INICIO,
    [rutaPublicaciones()]: pagina([V1]),
    [rutaPublicaciones('tipoId=2')]: pagina([V1]),
    [rutaPublicaciones('marcaId=10')]: pagina([V1]),
    [rutaPublicaciones('pagina=2')]: pagina([V1], 30, 2),
    [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1) },
    ...extra,
  }
}

describe('Textos de la ficha (CA-P10-22, 23, 31)', () => {
  it('título "Marca Línea Año en Municipio | Financiamos tu Camión"', () => {
    expect(tituloVehiculo(V2)).toBe('Kenworth T800 2018 en Medellín | Financiamos tu Camión')
  })

  it('sin marca o sin línea usa el título de la publicación, sin año', () => {
    expect(tituloVehiculo(C1)).toBe(`${C1.titulo} en ${C1.municipio} | Financiamos tu Camión`)
  })

  it('si pasa de 65 caracteres va sin el sufijo', () => {
    const largo = { ...V2, marca: 'International', linea: 'ProStar Premium', municipio: 'San José del Guaviare' }
    expect(tituloVehiculo(largo)).toBe('International ProStar Premium 2018 en San José del Guaviare')
    // " | Financiamos tu Camión" tiene 24 caracteres: hasta 41 cabe con el sufijo (65).
    expect(conSufijo('x'.repeat(41))).toHaveLength(65)
    expect(conSufijo('x'.repeat(42))).toBe('x'.repeat(42))
  })

  it('descripción con categoría, km, lugar, precio, negociable y reservado; máximo 160', () => {
    const d = descripcionVehiculo({ ...V2, negociable: true, estadoComercial: 'RESERVADO', kilometraje: 85000 })
    expect(d.startsWith(`${V2.tipo} Kenworth T800 2018, 85.000 km en Medellín, Antioquia. Precio: $`)).toBe(true)
    expect(d.length).toBeLessThanOrEqual(160)
    const corta = descripcionVehiculo({ ...V1, negociable: false, estadoComercial: 'DISPONIBLE' })
    expect(corta).toMatch(/\. Escríbenos por WhatsApp o solicita tu crédito\.$/)
    // Carrocería: sin kilometraje (D-17).
    expect(descripcionVehiculo({ ...C1, kilometraje: 900 })).not.toMatch(/km/)
  })

  it('recortar corta por palabra completa con "…"', () => {
    expect(recortar('uno dos tres', 20)).toBe('uno dos tres')
    expect(recortar('uno dos tres cuatro', 12)).toBe('uno dos…')
    expect(recortar('a'.repeat(200), 160)).toHaveLength(160)
  })
})

describe('JSON-LD (CA-P10-61 a 68)', () => {
  it('escapa <, > y &: un título con </script> no cierra el bloque y el JSON sigue siendo válido', () => {
    const malicioso = '</script><script>alert(1)</script> & más'
    const texto = jsonLdSeguro({ name: malicioso })
    expect(texto).not.toMatch(/[<>&]/)
    expect(JSON.parse(texto)).toEqual({ name: malicioso })
    fijarJsonLd('prueba', { name: malicioso })
    fijarJsonLd('prueba', { name: malicioso })
    const bloques = document.head.querySelectorAll('script[data-camion="prueba"]')
    expect(bloques).toHaveLength(1)
    expect(bloques[0].children).toHaveLength(0)
    expect(JSON.parse(bloques[0].textContent ?? '')).toEqual({ name: malicioso })
    fijarJsonLd('prueba', null)
    expect(document.head.querySelector('script[data-camion="prueba"]')).toBeNull()
  })

  it('Product + Vehicle con oferta, sin placa, asesor, WhatsApp, seller ni itemCondition', () => {
    const v = detalle(
      { ...V2, estadoComercial: 'RESERVADO' },
      {
        whatsapp: 'https://wa.me/573001112233',
        combustible: 'Diésel',
        fotos: [
          { url: '/media/b.jpg', orden: 2, esPortada: false },
          { url: '/media/a.jpg', orden: 1, esPortada: true },
        ],
      },
    )
    const d = jsonLdVehiculo(v, 'https://ejemplo.co')
    expect(d['@type']).toEqual(['Product', 'Vehicle'])
    expect(d.name).toBe('Kenworth T800 2018 en Medellín')
    expect(d.url).toBe(`https://ejemplo.co/vehiculo/${V2.slug}`)
    expect(d.image).toEqual(['https://ejemplo.co/media/a.jpg', 'https://ejemplo.co/media/b.jpg'])
    expect(d.brand).toEqual({ '@type': 'Brand', name: 'Kenworth' })
    expect(d.model).toBe('T800')
    expect(d.vehicleModelDate).toBe('2018')
    expect(d.fuelType).toBe('Diésel')
    expect(d.offers).toMatchObject({
      '@type': 'Offer',
      price: V2.precioCop,
      priceCurrency: 'COP',
      availability: 'https://schema.org/LimitedAvailability',
      availableAtOrFrom: { address: { addressLocality: 'Medellín', addressCountry: 'CO' } },
    })
    const texto = JSON.stringify(d)
    expect(texto).not.toMatch(/wa\.me|placa|seller|itemCondition|asesor/i)
  })

  it('migas: Inicio › Camiones › vehículo', () => {
    const m = jsonLdMigas(V2, 'https://ejemplo.co') as { itemListElement: { name: string; item: string }[] }
    expect(m.itemListElement.map((e) => [e.name, e.item])).toEqual([
      ['Inicio', 'https://ejemplo.co/'],
      ['Camiones', 'https://ejemplo.co/camiones'],
      ['Kenworth T800 2018 en Medellín', `https://ejemplo.co/vehiculo/${V2.slug}`],
    ])
  })

  it('Organization sin dirección ni AutoDealer (D-90 C); razón social y NIT solo con la empresa completa', () => {
    const sin = jsonLdOrganizacion({ ...CONTACTO_PORTAL_RESPALDO, empresa: null }, 'https://ejemplo.co')
    expect(sin).toMatchObject({
      '@type': 'Organization',
      name: 'VC Automotores',
      alternateName: 'Venta de Camiones – Financiamos tu Camión',
      url: 'https://ejemplo.co/',
      logo: 'https://ejemplo.co/marca/logo-cuadrado.png',
      email: 'vc.automotores@gmail.com',
      contactPoint: { telephone: '+57 316 615 2907', contactType: 'customer service', areaServed: 'CO', availableLanguage: 'es' },
    })
    expect(sin).not.toHaveProperty('legalName')
    expect(sin).not.toHaveProperty('address')
    expect(sin).not.toHaveProperty('sameAs')
    const con = jsonLdOrganizacion(
      {
        ...CONTACTO_PORTAL_RESPALDO,
        instagram: 'https://www.instagram.com/vca',
        empresa: { razonSocial: 'VCA S.A.S.', nit: '800197268-4', direccion: 'Calle 1' },
      },
      'https://ejemplo.co',
    )
    expect(con).toMatchObject({ legalName: 'VCA S.A.S.', taxID: '800197268-4', sameAs: ['https://www.instagram.com/vca'] })
    expect(JSON.stringify(con)).not.toContain('Calle 1')
  })
})

describe('Etiquetas por página en la aplicación (CA-P10-21, 24 a 28, 32)', () => {
  it.each([
    ['/', 'Venta de camiones usados y crédito | Financiamos tu Camión', /^Camiones usados en venta en Colombia/, null, '/'],
    ['/vehiculos', 'Camiones y vehículos de trabajo en venta | Financiamos tu Camión', /^Camiones, carros, carrocerías/, null, '/vehiculos'],
    ['/camiones', 'Camiones en venta en Colombia | Financiamos tu Camión', /^Camiones en venta en Colombia con fotos/, null, '/camiones'],
    ['/credito', 'Crédito para camión | Financiamos tu Camión', /^Solicita tu crédito para camión/, null, '/credito'],
    ['/vende-tu-camion', 'Vende tu camión | Financiamos tu Camión', /^Cuéntanos qué camión/, null, '/vende-tu-camion'],
    ['/seguro-todo-riesgo', 'Seguro todo riesgo para tu vehículo | Financiamos tu Camión', /^Déjanos tus datos/, null, '/seguro-todo-riesgo'],
    ['/busco-un-vehiculo', 'Busco un vehículo | Financiamos tu Camión', /^Cuéntanos qué vehículo buscas/, null, '/busco-un-vehiculo'],
    ['/vehiculos?marcaId=10', 'Camiones y vehículos de trabajo en venta | Financiamos tu Camión', /^Camiones, carros/, 'noindex, follow', null],
    ['/vehiculos?pagina=2', 'Camiones y vehículos de trabajo en venta | Financiamos tu Camión', /^Camiones, carros/, null, '/vehiculos?pagina=2'],
    ['/privacidad', 'Política de tratamiento de datos | Financiamos tu Camión', /^Camiones usados en venta/, 'noindex', null],
    ['/no-existe', 'Página no encontrada | Financiamos tu Camión', /^Camiones usados en venta/, 'noindex', null],
  ])('%s', async (direccion, titulo, descripcion, robots, canon) => {
    simularFetch(datos({ '/api/catalogo/listas-busqueda': { status: 200, body: { tipos: [], marcas: [] } } }))
    abrirApp(direccion)
    await waitFor(() => expect(document.title).toBe(titulo))
    expect(meta('description')).toMatch(descripcion)
    expect(meta('robots')).toBe(robots)
    expect(canonica()).toBe(canon === null ? null : `${ORIGEN}${canon}`)
    expect(og('og:title')).toBe(titulo)
  })

  it('la ficha usa los datos del vehículo, canonical propia y JSON-LD; al salir se quitan y vuelve la descripción por defecto', async () => {
    simularFetch(datos())
    abrirApp(`/vehiculo/${V1.slug}`)
    await waitFor(() => expect(document.title).toBe(tituloVehiculo(V1)))
    expect(meta('description')).toBe(descripcionVehiculo(V1))
    expect(canonica()).toBe(`${ORIGEN}/vehiculo/${V1.slug}`)
    expect(jsonLd('jsonld-vehiculo')).toMatchObject({ '@type': ['Product', 'Vehicle'] })
    expect(jsonLd('jsonld-migas')).toMatchObject({ '@type': 'BreadcrumbList' })
    irA('/vende-tu-camion')
    await waitFor(() => expect(document.title).toBe('Vende tu camión | Financiamos tu Camión'))
    expect(jsonLd('jsonld-vehiculo')).toBeNull()
    expect(jsonLd('jsonld-migas')).toBeNull()
    irA('/terminos')
    await waitFor(() => expect(document.title).toBe('Términos y condiciones | Financiamos tu Camión'))
    expect(meta('description')).toMatch(/^Camiones usados en venta en Colombia/)
  })

  it('CA-P10-24: vehículo no disponible: título propio, noindex y sin JSON-LD', async () => {
    simularFetch(datos({ [rutaDetalle('vendido')]: { status: 404, body: { error: 'Este vehículo no está disponible.' } } }))
    abrirApp('/vehiculo/vendido')
    await waitFor(() => expect(document.title).toBe('Este vehículo no está disponible | Financiamos tu Camión'))
    expect(meta('robots')).toBe('noindex')
    expect(canonica()).toBeNull()
    expect(jsonLd('jsonld-vehiculo')).toBeNull()
  })

  it('CA-P10-80, 28: reutiliza las etiquetas que inyecta la API (mismas marcas data-camion), sin duplicar', async () => {
    document.head.insertAdjacentHTML(
      'beforeend',
      `<meta name="description" content="De la API" data-camion="description" />` +
        `<link rel="canonical" href="${ORIGEN}/vehiculo/${V1.slug}" data-camion="canonical" />` +
        `<meta property="og:image" content="${ORIGEN}/media/x.jpg" data-camion="og:image" />` +
        `<script type="application/ld+json" data-camion="jsonld-vehiculo">{}</script>` +
        `<script type="application/ld+json" data-camion="jsonld-migas">{}</script>`,
    )
    simularFetch(datos())
    abrirApp(`/vehiculo/${V1.slug}`)
    await waitFor(() => expect(document.title).toBe(tituloVehiculo(V1)))
    for (const selector of [
      'meta[name="description"]',
      'meta[name="robots"]',
      'link[rel="canonical"]',
      'meta[property="og:title"]',
      'meta[property="og:image"]',
      'meta[name="twitter:card"]',
      'script[data-camion="jsonld-vehiculo"]',
      'script[data-camion="jsonld-migas"]',
    ]) {
      expect(document.head.querySelectorAll(selector).length).toBeLessThanOrEqual(1)
    }
    expect(meta('description')).toBe(descripcionVehiculo(V1))
  })

  it('CA-P10-61: la portada lleva Organization con el contacto vigente; las demás páginas no', async () => {
    simularFetch(datos({ [RUTA_CONTACTO_PORTAL]: CONTACTO_PORTAL_POR_DEFECTO }))
    abrirApp('/')
    await waitFor(() => expect(jsonLd('jsonld-organizacion')).toMatchObject({ '@type': 'Organization', url: `${ORIGEN}/` }))
    irA('/vehiculos')
    await screen.findByRole('heading', { level: 1, name: 'Buscar vehículos' })
    expect(jsonLd('jsonld-organizacion')).toBeNull()
  })
})

describe('D-89 A: indexación apagada por defecto', () => {
  it('sin VITE_PERMITIR_INDEXACION=true todas las páginas llevan noindex y ninguna canonical', async () => {
    vi.stubEnv('VITE_PERMITIR_INDEXACION', '')
    try {
      simularFetch(datos())
      abrirApp('/')
      await waitFor(() => expect(meta('robots')).toBe('noindex'))
      expect(canonica()).toBeNull()
      act(() => irA('/camiones'))
      await screen.findByRole('heading', { level: 1, name: 'Camiones' })
      expect(meta('robots')).toBe('noindex')
      expect(canonica()).toBeNull()
    } finally {
      vi.unstubAllEnvs()
    }
  })
})

describe('index.html y etiquetas al compilar (CA-P10-14, 75, 82)', () => {
  const html = (import.meta.glob('../index.html', { query: '?raw', import: 'default', eager: true }) as Record<string, string>)[
    '../index.html'
  ]

  it('título, descripción y etiquetas genéricas van dentro del bloque que reemplaza la API', () => {
    const inicio = html.indexOf('<!--camion:meta:inicio-->')
    const fin = html.indexOf('<!--camion:meta:fin-->')
    expect(inicio).toBeGreaterThan(0)
    expect(fin).toBeGreaterThan(inicio)
    const dentro = html.slice(inicio, fin)
    const fuera = html.slice(0, inicio) + html.slice(fin)
    for (const etiqueta of ['<title>', 'name="description"', 'og:type', 'og:site_name', 'og:locale', 'og:title', 'og:description', 'twitter:card', '<!--camion:robots-->', '<!--camion:og-absolutas-->']) {
      expect(dentro).toContain(etiqueta)
      expect(fuera).not.toContain(etiqueta)
    }
    expect(dentro).toContain('<title>Venta de camiones usados y crédito | Financiamos tu Camión</title>')
  })

  it('sin variables: noindex y sin og:url, og:image ni twitter:image (rutas relativas no sirven a las redes)', () => {
    const resultado = etiquetasHtml(html, {})
    expect(resultado).toContain('<meta name="robots" content="noindex" data-camion="noindex" />')
    expect(resultado).not.toMatch(/og:url|og:image|twitter:image|camion:robots|camion:og-absolutas/)
    expect(resultado).toContain('<!--camion:meta:inicio-->')
  })

  it("B-6: un '$&', '$1' o '$`' en la URL pública se copia tal cual (no se interpreta como patrón)", () => {
    const resultado = etiquetasHtml(html, { VITE_URL_PUBLICA: 'https://a$&b$1c$`d.example' })
    expect(resultado).toContain('<meta property="og:image" content="https://a$&amp;b$1c$`d.example/marca/og-portal.jpg"')
    expect(resultado).not.toContain('<!--camion:og-absolutas-->')
    // Sin el arreglo, `$&` se convertía en la marca `<!--camion:og-absolutas-->` y `` $` `` en todo el HTML anterior.
    expect(resultado.split('<!doctype html>')).toHaveLength(2)
  })

  it('con indexación y URL pública https: sin noindex y con las tres etiquetas absolutas', () => {
    const resultado = etiquetasHtml(html, { VITE_PERMITIR_INDEXACION: 'true', VITE_URL_PUBLICA: 'https://ejemplo.co/' })
    expect(resultado).not.toContain('name="robots"')
    expect(resultado).toContain('<meta property="og:url" content="https://ejemplo.co/" data-camion="og:url" />')
    expect(resultado).toContain('<meta property="og:image" content="https://ejemplo.co/marca/og-portal.jpg" data-camion="og:image" />')
    expect(resultado).toContain('<meta name="twitter:image" content="https://ejemplo.co/marca/og-portal.jpg" data-camion="twitter:image" />')
    // Una URL sin https o con ruta no se usa.
    expect(etiquetasHtml(html, { VITE_URL_PUBLICA: 'http://ejemplo.co' })).not.toContain('og:image')
    expect(etiquetasHtml(html, { VITE_URL_PUBLICA: 'https://ejemplo.co/x"><script>' })).not.toContain('og:image')
  })
})
