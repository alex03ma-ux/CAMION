// QA de P10 (docs/criterios/P10-criterios.md): casos negativos y de borde que no cubrían P10Rutas, P10Seo ni rutas.test.
// Datos ficticios; ningún código de recuperación real. No toca la base ni la API real (fetch simulado).
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { etiquetasHtml } from '../etiquetasHtml'
import App from '../src/App'
import { codigoRecuperacionActual } from '../src/api/recuperacion'
import {
  convertirHashViejo,
  esRutaReservada,
  leerRuta,
  PREFIJOS_RESERVADOS,
  rutaVehiculo,
} from '../src/rutas'
import {
  abrirApp,
  detalle,
  pagina,
  respuestaFiltros,
  RUTA_FILTROS,
  RUTA_INICIO,
  rutaDetalle,
  rutaPublicaciones,
  tarjeta,
  V1,
  V2,
} from './catalogoDatos'
import { irA, simularFetch, ubicacionActual, type RespuestaSimulada } from './helpers'

const ORIGEN = window.location.origin
const INICIO = { status: 200, body: { destacados: [], recientes: [V1, V2] } }
const NO_DISPONIBLE = { status: 404, body: { error: 'Este vehículo no está disponible.' } }

function datos(extra: Record<string, RespuestaSimulada> = {}) {
  return {
    [RUTA_FILTROS]: respuestaFiltros(),
    [RUTA_INICIO]: INICIO,
    [rutaPublicaciones()]: pagina([V1, V2]),
    [rutaPublicaciones('tipoId=2')]: pagina([V1, V2]),
    [rutaPublicaciones('marcaId=10')]: pagina([V1]),
    [rutaPublicaciones('marcaId=11')]: pagina([V2]),
    [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1) },
    [rutaDetalle(V2.slug)]: { status: 200, body: detalle(V2) },
    '/api/catalogo/listas-busqueda': { status: 200, body: { tipos: [], marcas: [] } },
    ...extra,
  }
}

const meta = (nombre: string) => document.head.querySelector(`meta[name="${nombre}"]`)?.getAttribute('content') ?? null
const og = (nombre: string) => document.head.querySelector(`meta[property="${nombre}"]`)?.getAttribute('content') ?? null
const canonica = () => document.head.querySelector('link[rel="canonical"]')?.getAttribute('href') ?? null

/** CA-P10-28: como máximo una etiqueta de cada tipo en <head> (title, description, robots, canonical, og:*, twitter:*, JSON-LD). */
function unaDeCada(contexto: string) {
  const head = document.head
  const contar = (selector: string) => head.querySelectorAll(selector).length
  expect(contar('title'), `${contexto}: <title>`).toBeLessThanOrEqual(1)
  expect(contar('meta[name="description"]'), `${contexto}: description`).toBe(1)
  expect(contar('meta[name="robots"]'), `${contexto}: robots`).toBeLessThanOrEqual(1)
  expect(contar('link[rel="canonical"]'), `${contexto}: canonical`).toBeLessThanOrEqual(1)
  const nombres = new Map<string, number>()
  for (const m of Array.from(head.querySelectorAll('meta[property], meta[name]'))) {
    const n = m.getAttribute('property') ?? m.getAttribute('name') ?? ''
    nombres.set(n, (nombres.get(n) ?? 0) + 1)
  }
  for (const [n, veces] of nombres) expect(veces, `${contexto}: ${n}`).toBe(1)
  const marcas = new Map<string, number>()
  for (const s of Array.from(head.querySelectorAll('script[type="application/ld+json"]'))) {
    const n = s.getAttribute('data-camion') ?? '(sin marca)'
    marcas.set(n, (marcas.get(n) ?? 0) + 1)
  }
  for (const [n, veces] of marcas) expect(veces, `${contexto}: JSON-LD ${n}`).toBe(1)
  // Todo lo que hay en <head> de buscadores y redes lleva su marca data-camion (si no, no se reutilizaría).
  for (const m of Array.from(head.querySelectorAll('meta[name="robots"], meta[name="description"], link[rel="canonical"], meta[property^="og:"], meta[name^="twitter:"], script[type="application/ld+json"]')))
    expect(m.hasAttribute('data-camion'), `${contexto}: ${m.outerHTML}`).toBe(true)
}

/** Bloque <!--camion:meta:…--> de index.html tal como lo compila Vite (etiquetasHtml), sin el <title>. */
function bloqueCompilado(entorno: Parameters<typeof etiquetasHtml>[1]): string {
  const html = (import.meta.glob('../index.html', { query: '?raw', import: 'default', eager: true }) as Record<string, string>)[
    '../index.html'
  ]
  const compilado = etiquetasHtml(html, entorno)
  const inicio = compilado.indexOf('<!--camion:meta:inicio-->')
  const fin = compilado.indexOf('<!--camion:meta:fin-->')
  return compilado.slice(inicio, fin).replace(/<title>[^<]*<\/title>/, '')
}

// ---------------------------------------------------------------------------------------------------------------------
describe('QA CA-P10-68, 74: XSS en el título y la descripción del asesor (frontend)', () => {
  const XSS = '"><img src=x onerror=alert(1)></script><script>alert(1)</script>'
  const DESCRIPCION = '</script><script>alert(2)</script> & <b>más</b>'
  const VX = tarjeta({ slug: 'xss-2020', titulo: XSS, marca: null, linea: null, municipio: 'Medellín', fotoPortada: null })

  it('og:*, twitter:* y JSON-LD llevan el texto literal: no crean etiquetas, no cierran el bloque y el JSON es válido', async () => {
    const alerta = vi.spyOn(window, 'alert').mockImplementation(() => {})
    simularFetch(datos({ [rutaDetalle(VX.slug)]: { status: 200, body: detalle(VX, { descripcionComercial: DESCRIPCION }) } }))
    abrirApp(rutaVehiculo(VX.slug))
    await waitFor(() => expect(document.title).toBe(`${XSS} en Medellín`))
    // Ninguna etiqueta nueva a partir del texto: ni <img> ni <script> ejecutable en el documento.
    expect(document.querySelectorAll('img[src="x"]').length).toBe(0)
    expect(document.head.querySelectorAll('img, b').length).toBe(0)
    expect(document.querySelectorAll('script:not([type="application/ld+json"])').length).toBe(0)
    // Atributos con el texto literal (setAttribute, no innerHTML).
    expect(og('og:title')).toBe(`${XSS} en Medellín`)
    expect(meta('twitter:title')).toBe(`${XSS} en Medellín`)
    expect(meta('description')).not.toContain('<')
    // JSON-LD: el texto del bloque no contiene "<" ni "</script"; al leerlo da el texto original.
    const bloque = document.head.querySelector('script[data-camion="jsonld-vehiculo"]')
    expect(bloque).not.toBeNull()
    const texto = bloque?.textContent ?? ''
    expect(texto).not.toMatch(/<|>/)
    expect(texto).toContain('\\u003c/script\\u003e')
    const json = JSON.parse(texto) as { name: string; description: string }
    expect(json.name).toBe(`${XSS} en Medellín`)
    expect(json.description).toBe(DESCRIPCION)
    const migas = JSON.parse(document.head.querySelector('script[data-camion="jsonld-migas"]')?.textContent ?? '') as {
      itemListElement: { name: string }[]
    }
    expect(migas.itemListElement.at(-1)?.name).toBe(`${XSS} en Medellín`)
    // El h1 muestra el texto, no lo interpreta.
    expect(screen.getByRole('heading', { level: 1 }).textContent).toBe(XSS)
    expect(alerta).not.toHaveBeenCalled()
    unaDeCada('ficha XSS')
  })
})

// ---------------------------------------------------------------------------------------------------------------------
describe('QA CA-P10-34, 72: slugs raros', () => {
  it.each([
    ['codificado con %2F y ../', '/vehiculo/..%2F..%2Fapi%2Fadmin%2Fusuarios', '../../api/admin/usuarios'],
    ['con espacios y comillas', '/vehiculo/a%20%22b%22%3Cc%3E', 'a "b"<c>'],
    ['muy largo (500 caracteres)', `/vehiculo/${'a'.repeat(500)}`, 'a'.repeat(500)],
    ['con ñ y &', `/vehiculo/${encodeURIComponent('camion-ñ&x-2020')}`, 'camion-ñ&x-2020'],
  ])('%s: la petición a la API va codificada y la página queda "no disponible" con noindex', async (_caso, direccion, slug) => {
    const api = simularFetch(datos({ [rutaDetalle(slug)]: NO_DISPONIBLE }))
    abrirApp(direccion)
    await waitFor(() => expect(document.title).toBe('Este vehículo no está disponible | Financiamos tu Camión'))
    expect(meta('robots')).toBe('noindex')
    expect(canonica()).toBeNull()
    // Una sola petición de detalle, a /api/catalogo/publicaciones/<slug codificado>; nada fuera de esa ruta.
    const detalles = api.llamadas.filter((l) => l.url.startsWith('/api/catalogo/publicaciones/'))
    expect(detalles.map((l) => l.url)).toEqual([rutaDetalle(slug)])
    for (const l of api.llamadas) {
      const ruta = new URL(l.url, ORIGEN).pathname
      expect(ruta.startsWith('/api/admin'), l.url).toBe(false)
    }
    // El slug no se refleja en etiquetas del <head>.
    expect(document.head.innerHTML).not.toContain('<c>')
  })

  it('rutas con barras o codificación rota no son una ficha: "no encontrada"', () => {
    for (const direccion of ['/vehiculo/a/b', '/vehiculo/../api/x', '/vehiculo/%E0%A4%A', '/vehiculo/%', '/vehiculo/%20', '/vehiculo//x', '//vehiculo/x'])
      expect(leerRuta(direccion), direccion).toEqual({ nombre: 'noEncontrada' })
  })

  it('slugs "%2E" y "%2E%2E": el navegador normaliza los puntos antes de la app, así que nunca son una ficha ni piden fuera de /api/catalogo/publicaciones/', async () => {
    // leerRuta('/vehiculo/%2E%2E') a secas daría el slug "..", pero location.pathname nunca lo trae así (WHATWG URL
    // trata %2E como punto): /vehiculo/%2E%2E → / y /vehiculo/%2E → /vehiculo/. Ver O-7 de la evidencia.
    const api = simularFetch(datos())
    abrirApp('/vehiculo/%2E%2E')
    expect(ubicacionActual()).toBe('/')
    await screen.findByRole('link', { name: V1.titulo })
    irA('/vehiculo/%2E')
    expect(ubicacionActual()).toBe('/vehiculo/')
    expect(await screen.findByRole('heading', { level: 1, name: 'Página no encontrada' })).toBeInTheDocument()
    expect(api.llamadas.filter((l) => l.url.startsWith('/api/catalogo/publicaciones/'))).toEqual([])
  })
})

// ---------------------------------------------------------------------------------------------------------------------
describe('QA CA-P10-36, 46, 48: /api, /media, /health, /assets y archivos nunca caen en el SPA', () => {
  it('ninguna ruta reservada abre una pantalla del portal (todas dan "no encontrada" si llegaran a la app)', () => {
    for (const direccion of ['/api', '/api/no-existe', '/media/x.jpg', '/health', '/health/ready', '/assets/index-x.js', '/marca/og-portal.jpg', '/sitemap.xml', '/robots.txt', '/favicon.ico', '/site.webmanifest']) {
      expect(esRutaReservada(direccion), direccion).toBe(true)
      expect(leerRuta(direccion).nombre, direccion).toBe('noEncontrada')
    }
    // Rutas del portal que se parecen pero no son prefijos reservados.
    for (const direccion of ['/vehiculos', '/vehiculo/api', '/cuenta', '/credito'])
      expect(esRutaReservada(direccion), direccion).toBe(false)
    expect(PREFIJOS_RESERVADOS).toEqual(
      expect.arrayContaining(['/api', '/media', '/health', '/assets', '/sitemap.xml', '/robots.txt']),
    )
  })

  it('los enlaces a archivos y a la API no se interceptan: el navegador los pide al servidor', async () => {
    simularFetch(datos())
    abrirApp('/')
    await screen.findByRole('link', { name: V1.titulo })
    const contenedor = document.createElement('div')
    contenedor.innerHTML = [
      '/api',
      '/api/catalogo/filtros',
      '/media/pub/v1/frente.jpg',
      '/health/ready',
      '/assets/index-x.js',
      '/sitemap.xml',
      '/robots.txt',
      '/favicon.ico',
      '/marca/og-portal.jpg',
      '/site.webmanifest',
      'tel:+573000000000',
    ]
      .map((h) => `<a href="${h}">${h}</a>`)
      .join('')
    document.body.appendChild(contenedor)
    try {
      for (const a of Array.from(contenedor.querySelectorAll('a'))) {
        let atendido = true
        const espia = (e: Event) => {
          atendido = e.defaultPrevented
          e.preventDefault()
        }
        document.addEventListener('click', espia)
        act(() => {
          a.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }))
        })
        document.removeEventListener('click', espia)
        expect(atendido, a.getAttribute('href') ?? '').toBe(false)
      }
    } finally {
      contenedor.remove()
    }
    expect(ubicacionActual()).toBe('/')
  })
})

// ---------------------------------------------------------------------------------------------------------------------
describe('QA CA-P10-36: Ctrl+clic y otros modificadores', () => {
  function clic(enlace: Element, opciones: MouseEventInit = {}): boolean {
    let atendido = true
    const espia = (e: Event) => {
      atendido = e.defaultPrevented
      e.preventDefault()
    }
    document.addEventListener('click', espia)
    act(() => {
      enlace.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, ...opciones }))
    })
    document.removeEventListener('click', espia)
    return atendido
  }

  it('Ctrl+clic, Alt+clic, clic central y download no cambian la pantalla ni el historial; target=_self sí navega', async () => {
    simularFetch(datos())
    abrirApp('/')
    const enlace = await screen.findByRole('link', { name: V1.titulo })
    const largo = window.history.length
    for (const opciones of [{ ctrlKey: true }, { altKey: true }, { button: 1 }, { ctrlKey: true, shiftKey: true }]) {
      expect(clic(enlace, opciones), JSON.stringify(opciones)).toBe(false)
    }
    expect(window.history.length).toBe(largo)
    expect(ubicacionActual()).toBe('/')
    expect(screen.queryByRole('heading', { level: 1, name: V1.titulo })).not.toBeInTheDocument()

    const contenedor = document.createElement('div')
    contenedor.innerHTML = `<a href="/vehiculos" download>descarga</a><a href="/vehiculos" target="_self">misma</a>`
    document.body.appendChild(contenedor)
    try {
      const [descarga, misma] = Array.from(contenedor.querySelectorAll('a'))
      expect(clic(descarga)).toBe(false)
      expect(ubicacionActual()).toBe('/')
      expect(clic(misma)).toBe(true)
      expect(await screen.findByRole('heading', { level: 1, name: 'Buscar vehículos' })).toBeInTheDocument()
      expect(ubicacionActual()).toBe('/vehiculos')
    } finally {
      contenedor.remove()
    }
  })
})

// ---------------------------------------------------------------------------------------------------------------------
describe('QA CA-P10-37: Atrás y Adelante del navegador (history.back / forward reales)', () => {
  /** history.back() y forward() son asíncronos en jsdom: espera al popstate. */
  async function moverHistorial(paso: () => void) {
    const fin = new Promise<void>((resolver) => window.addEventListener('popstate', () => resolver(), { once: true }))
    await act(async () => {
      paso()
      await fin
    })
  }

  it('portada → búsqueda con filtro → ficha; Atrás vuelve a la búsqueda con el filtro, otra vez Atrás a la portada y Adelante a la búsqueda', async () => {
    simularFetch(datos())
    abrirApp('/')
    const user = userEvent.setup()
    await screen.findByRole('link', { name: V1.titulo })
    act(() => {
      window.history.pushState(null, '', '/vehiculos?marcaId=10')
      window.dispatchEvent(new Event('camion:navegacion'))
    })
    expect(await screen.findByRole('heading', { level: 1, name: 'Buscar vehículos' })).toBeInTheDocument()
    await user.click(await screen.findByRole('link', { name: V1.titulo }))
    expect(await screen.findByRole('heading', { level: 1, name: V1.titulo })).toBeInTheDocument()
    expect(ubicacionActual()).toBe(`/vehiculo/${V1.slug}`)
    await waitFor(() => expect(document.title).toBe('Chevrolet NPR 2020 en Bogotá | Financiamos tu Camión'))

    await moverHistorial(() => window.history.back())
    expect(ubicacionActual()).toBe('/vehiculos?marcaId=10')
    expect(await screen.findByRole('heading', { level: 1, name: 'Buscar vehículos' })).toBeInTheDocument()
    expect((screen.getByLabelText('Marca') as HTMLSelectElement).value).toBe('10')
    await waitFor(() => expect(document.title).toBe('Camiones y vehículos de trabajo en venta | Financiamos tu Camión'))
    expect(meta('robots')).toBe('noindex, follow')
    expect(document.head.querySelector('script[data-camion="jsonld-vehiculo"]')).toBeNull()

    await moverHistorial(() => window.history.back())
    expect(ubicacionActual()).toBe('/')
    await waitFor(() => expect(document.title).toBe('Venta de camiones usados y crédito | Financiamos tu Camión'))

    await moverHistorial(() => window.history.forward())
    expect(ubicacionActual()).toBe('/vehiculos?marcaId=10')
    expect(await screen.findByRole('heading', { level: 1, name: 'Buscar vehículos' })).toBeInTheDocument()
    expect((screen.getByLabelText('Marca') as HTMLSelectElement).value).toBe('10')

    await moverHistorial(() => window.history.forward())
    expect(ubicacionActual()).toBe(`/vehiculo/${V1.slug}`)
    expect(await screen.findByRole('heading', { level: 1, name: V1.titulo })).toBeInTheDocument()
    unaDeCada('ficha tras Adelante')
  })
})

// ---------------------------------------------------------------------------------------------------------------------
describe('QA CA-P10-28, 80: una sola etiqueta de cada tipo al navegar entre páginas', () => {
  const RECORRIDO = [
    '/',
    '/vehiculos',
    `/vehiculo/${V1.slug}`,
    '/vehiculos?marcaId=11',
    `/vehiculo/${V2.slug}`,
    '/camiones',
    '/credito',
    '/vende-tu-camion',
    '/seguro-todo-riesgo',
    '/busco-un-vehiculo',
    '/privacidad',
    '/no-existe',
    '/vehiculo/vendido',
    `/vehiculo/${V1.slug}`,
    '/',
  ]

  async function recorrer() {
    for (const direccion of RECORRIDO) {
      irA(direccion)
      if (direccion.startsWith('/vehiculo/') && direccion !== '/vehiculo/vendido') {
        const slug = direccion.slice('/vehiculo/'.length)
        await waitFor(() => expect(document.head.querySelector('script[data-camion="jsonld-vehiculo"]')?.textContent ?? '').toContain(slug))
      } else if (direccion === '/vehiculo/vendido') {
        await waitFor(() => expect(document.title).toBe('Este vehículo no está disponible | Financiamos tu Camión'))
      } else {
        await waitFor(() => expect(document.head.querySelector('script[data-camion="jsonld-vehiculo"]')).toBeNull())
        await waitFor(() => expect(document.title).not.toMatch(/^Detalle del vehículo/))
      }
      unaDeCada(direccion)
    }
  }

  it('con la indexación encendida, empezando con el <head> que entrega la API/proxy (bloque compilado con URL pública)', async () => {
    document.head.insertAdjacentHTML('beforeend', bloqueCompilado({ VITE_PERMITIR_INDEXACION: 'true', VITE_URL_PUBLICA: 'https://ejemplo.test' }))
    simularFetch(datos({ '/api/catalogo/publicaciones/vendido': NO_DISPONIBLE }))
    abrirApp('/')
    await waitFor(() => expect(document.head.querySelector('script[data-camion="jsonld-organizacion"]')).not.toBeNull())
    unaDeCada('portada')
    await recorrer()
    // Las páginas indexables tienen canonical; las de noindex no (CA-P10-27).
    irA('/credito')
    await waitFor(() => expect(canonica()).toBe(`${ORIGEN}/credito`))
    expect(meta('robots')).toBeNull()
    irA('/privacidad')
    await waitFor(() => expect(meta('robots')).toBe('noindex'))
    expect(canonica()).toBeNull()
  })

  it('con la indexación apagada (D-89 A), empezando con el bloque compilado que ya trae robots noindex', async () => {
    vi.stubEnv('VITE_PERMITIR_INDEXACION', '')
    try {
      document.head.insertAdjacentHTML('beforeend', bloqueCompilado({}))
      expect(document.head.querySelectorAll('meta[name="robots"]').length).toBe(1)
      simularFetch(datos({ '/api/catalogo/publicaciones/vendido': NO_DISPONIBLE }))
      abrirApp('/')
      await waitFor(() => expect(document.head.querySelector('script[data-camion="jsonld-organizacion"]')).not.toBeNull())
      await recorrer()
    } finally {
      vi.unstubAllEnvs()
    }
  })
})

// ---------------------------------------------------------------------------------------------------------------------
describe('QA CA-P10-53, D-89 A: noindex en todas las páginas con la indexación apagada', () => {
  it.each([
    ['/', null],
    ['/vehiculos', null],
    ['/camiones', 'Camiones'],
    [`/vehiculo/${V1.slug}`, V1.titulo],
    ['/credito', null],
    ['/vende-tu-camion', null],
    ['/seguro-todo-riesgo', null],
    ['/busco-un-vehiculo', null],
    ['/vehiculos?pagina=2', null],
  ])('%s: robots noindex, sin canonical ni og:url', async (direccion, h1) => {
    vi.stubEnv('VITE_PERMITIR_INDEXACION', '')
    try {
      simularFetch(datos({ [rutaPublicaciones('pagina=2')]: pagina([V1], 30, 2) }))
      abrirApp(direccion)
      if (h1) await screen.findByRole('heading', { level: 1, name: h1 })
      if (direccion.startsWith('/vehiculo/'))
        await waitFor(() => expect(document.title).toBe('Chevrolet NPR 2020 en Bogotá | Financiamos tu Camión'))
      await waitFor(() => expect(meta('robots')).toBe('noindex'))
      expect(canonica()).toBeNull()
      expect(og('og:url')).toBeNull()
      expect(document.head.querySelectorAll('meta[name="robots"]').length).toBe(1)
    } finally {
      vi.unstubAllEnvs()
    }
  })

  it('"true" en mayúsculas o con espacios no enciende la indexación (solo el texto exacto "true")', async () => {
    for (const valor of ['TRUE', ' true', '1', 'si']) {
      vi.stubEnv('VITE_PERMITIR_INDEXACION', valor)
      try {
        simularFetch(datos())
        const { unmount } = abrirApp('/credito')
        await waitFor(() => expect(document.title).toBe('Crédito para camión | Financiamos tu Camión'))
        expect(meta('robots'), valor).toBe('noindex')
        expect(canonica(), valor).toBeNull()
        unmount()
      } finally {
        vi.unstubAllEnvs()
      }
    }
    // Y en el HTML compilado pasa lo mismo.
    expect(bloqueCompilado({ VITE_PERMITIR_INDEXACION: 'TRUE' })).toContain('<meta name="robots" content="noindex" data-camion="noindex" />')
  })
})

// ---------------------------------------------------------------------------------------------------------------------
describe('QA CA-P10-41: enlaces viejos con "#/"', () => {
  it.each([
    ['/#/cuenta/usuarios?rol=ASESOR', '/cuenta/usuarios?rol=ASESOR'],
    ['/#/buscar?marcaId=3', '/vehiculos?marcaId=3'],
    ['/#/buscar?marcaId=3&pagina=1', '/vehiculos?marcaId=3'],
    ['/#/', '/'],
    ['/#/vende-tu-camion', '/vende-tu-camion'],
    ['/#/credito/chevrolet-npr-2020', '/credito/chevrolet-npr-2020'],
    ['/#/inventario/7', '/inventario/7'],
  ])('%s → %s', (vieja, nueva) => {
    expect(convertirHashViejo(vieja.slice(1))).toBe(nueva)
  })

  it('un hash que no empieza por "#/" no se toca (#contenido, #codigo=, #)', () => {
    for (const hash of ['#contenido', '#codigo=ABC', '#', '', '#%2F', '#?x=1']) expect(convertirHashViejo(hash), hash).toBeNull()
  })

  it.fails('QA-P10-1: un enlace viejo manipulado "/#//otro-dominio" no debe dejar la página en blanco', async () => {
    // convertirHashViejo('#//ejemplo-externo.test/x') da '//ejemplo-externo.test/x' (dirección de otro origen);
    // history.replaceState lanza SecurityError dentro de useState y React no pinta nada. No hay redirección a otro
    // dominio (el navegador la bloquea), pero la página queda vacía. Ver la evidencia.
    const consola = vi.spyOn(console, 'error').mockImplementation(() => {})
    simularFetch(datos())
    window.history.replaceState(null, '', '/#//ejemplo-externo.test/x')
    let error: unknown = null
    try {
      render(<App />)
    } catch (e) {
      error = e
    }
    consola.mockRestore()
    expect(error).toBeNull()
    expect(await screen.findByRole('heading', { level: 1, name: 'Página no encontrada' })).toBeInTheDocument()
  })
})

// ---------------------------------------------------------------------------------------------------------------------
describe('QA CA-P10-42 a 44: enlaces de recuperación (el código nunca llega a la ruta ni a la consulta)', () => {
  const RUTA = '/api/auth/restablecer-contrasena'
  const OK = { status: 200, body: { mensaje: 'Listo. Tu contraseña cambió; inicia sesión con la nueva.' } }

  /** Registra toda dirección que pase por pushState/replaceState mientras dura la prueba. */
  function espiarHistorial() {
    const direcciones: string[] = []
    const historial = window.history
    const reemplazar = historial.replaceState.bind(historial)
    const empujar = historial.pushState.bind(historial)
    historial.replaceState = (estado: unknown, titulo: string, url?: string | URL | null) => {
      direcciones.push(String(url))
      reemplazar(estado, titulo, url)
    }
    historial.pushState = (estado: unknown, titulo: string, url?: string | URL | null) => {
      direcciones.push(String(url))
      empujar(estado, titulo, url)
    }
    return {
      direcciones,
      reemplazar,
      restaurar: () => {
        historial.replaceState = reemplazar
        historial.pushState = empujar
      },
    }
  }

  it.each([
    ['/#/restablecer-contrasena?codigo=Qa_Viejo-1', 'Qa_Viejo-1'],
    ['/#/restablecer-contrasena/?codigo=Qa_Viejo-2', 'Qa_Viejo-2'],
    ['/#/restablecer-contrasena?otra=1&codigo=Qa_Viejo-3', 'Qa_Viejo-3'],
    ['/#/restablecer-contrasena?codigo=Qa%2EViejo%7E4', 'Qa.Viejo~4'],
  ])('viejo %s: la dirección queda /restablecer-contrasena y el código no aparece en ninguna URL, título ni almacenamiento', async (enlace, codigo) => {
    const api = simularFetch({ [RUTA]: OK })
    const espia = espiarHistorial()
    const consola = [vi.spyOn(console, 'log'), vi.spyOn(console, 'info'), vi.spyOn(console, 'warn'), vi.spyOn(console, 'error')]
    espia.reemplazar(null, '', enlace)
    try {
      render(<App />)
      await screen.findByRole('heading', { level: 1, name: 'Crear una contraseña nueva' })
      expect(window.location.href).toBe(`${ORIGEN}/restablecer-contrasena`)
      for (const d of espia.direcciones) {
        expect(d).not.toContain(codigo)
        expect(d).not.toContain('codigo')
      }
      expect(document.title).not.toContain(codigo)
      expect(document.head.innerHTML).not.toContain(codigo)
      expect(JSON.stringify(window.localStorage)).not.toContain(codigo)
      for (let i = 0; i < window.localStorage.length; i++)
        expect(window.localStorage.getItem(window.localStorage.key(i) ?? '') ?? '').not.toContain(codigo)
      for (let i = 0; i < window.sessionStorage.length; i++)
        expect(window.sessionStorage.getItem(window.sessionStorage.key(i) ?? '') ?? '').not.toContain(codigo)
      for (const c of consola) for (const llamada of c.mock.calls) expect(JSON.stringify(llamada)).not.toContain(codigo)
      const user = userEvent.setup()
      await user.type(screen.getByLabelText('Contraseña nueva'), 'Nueva1234')
      await user.type(screen.getByLabelText('Confirma la contraseña nueva'), 'Nueva1234')
      await user.click(screen.getByRole('button', { name: 'Guardar contraseña nueva' }))
      await screen.findByText('Listo. Tu contraseña cambió; inicia sesión con la nueva.')
      const cuerpo = JSON.parse(String(api.llamadasA(RUTA)[0].init?.body)) as Record<string, unknown>
      expect(Object.values(cuerpo)).toContain(codigo)
      // Solo una petición a la API con el código, por POST en el cuerpo (nunca en la URL).
      for (const l of api.llamadas) expect(l.url).not.toContain(codigo)
      // Ya usado: no queda en memoria.
      expect(codigoRecuperacionActual()).toBe('')
    } finally {
      espia.restaurar()
    }
  })

  it('un enlace viejo pegado con la app abierta (hashchange) tampoco deja el código en la dirección', async () => {
    simularFetch(datos({ [RUTA]: OK }))
    abrirApp('/')
    await screen.findByRole('link', { name: V1.titulo })
    const espia = espiarHistorial()
    try {
      act(() => {
        espia.reemplazar(null, '', '/#/restablecer-contrasena?codigo=Qa_Pegado-5')
        window.dispatchEvent(new HashChangeEvent('hashchange'))
      })
      await screen.findByRole('heading', { level: 1, name: 'Crear una contraseña nueva' })
      expect(window.location.href).toBe(`${ORIGEN}/restablecer-contrasena`)
      expect(espia.direcciones.every((d) => !d.includes('Qa_Pegado-5'))).toBe(true)
      expect(screen.queryByText('El enlace no es válido o ya venció. Pide uno nuevo al administrador.')).not.toBeInTheDocument()
    } finally {
      espia.restaurar()
    }
  })

  it('CA-P10-44: un código en la consulta (?codigo=) no se acepta; solo en el fragmento', async () => {
    simularFetch({ [RUTA]: OK })
    irA('/restablecer-contrasena?codigo=Qa_Consulta-6')
    render(<App />)
    await screen.findByRole('heading', { level: 1, name: 'Crear una contraseña nueva' })
    expect(await screen.findByText('El enlace no es válido o ya venció. Pide uno nuevo al administrador.')).toBeInTheDocument()
    expect(screen.queryByLabelText('Contraseña nueva')).not.toBeInTheDocument()
  })

  it('el enlace nuevo con barra final (/restablecer-contrasena/#codigo=) también funciona y se limpia', async () => {
    simularFetch({ [RUTA]: OK })
    irA('/restablecer-contrasena/#codigo=Qa_Nuevo-7')
    render(<App />)
    await screen.findByRole('heading', { level: 1, name: 'Crear una contraseña nueva' })
    await waitFor(() => expect(window.location.hash).toBe(''))
    expect(window.location.href).not.toContain('Qa_Nuevo-7')
    expect(screen.getByLabelText('Contraseña nueva')).toBeInTheDocument()
  })
})

// ---------------------------------------------------------------------------------------------------------------------
describe('QA CA-P10-08, 10, 11: pesos y tamaños de los derivados de marca (tabla de §2.1; KB = 1024 bytes)', () => {
  const binarios = import.meta.glob('../public/{favicon.ico,marca/*}', { query: '?inline', import: 'default', eager: true }) as Record<
    string,
    string
  >
  const bytes = (archivo: string) => atob((binarios[`../public/${archivo}`] ?? '').split(',')[1] ?? '')
  const u32 = (b: string, i: number) => ((b.charCodeAt(i) << 24) | (b.charCodeAt(i + 1) << 16) | (b.charCodeAt(i + 2) << 8) | b.charCodeAt(i + 3)) >>> 0
  const u16 = (b: string, i: number) => (b.charCodeAt(i) << 8) | b.charCodeAt(i + 1)
  /** Ancho y alto de un PNG (IHDR). */
  const png = (b: string) => [u32(b, 16), u32(b, 20)]
  /** Ancho y alto de un JPEG (primer SOF0/SOF2). */
  function jpeg(b: string): number[] {
    let i = 2
    while (i < b.length) {
      const marca = b.charCodeAt(i + 1)
      const largo = u16(b, i + 2)
      if (marca === 0xc0 || marca === 0xc2) return [u16(b, i + 7), u16(b, i + 5)]
      i += 2 + largo
    }
    return [0, 0]
  }

  it.each([
    ['marca/logo-horizontal.webp', 30],
    ['marca/logo-horizontal.png', 60],
    ['marca/logo-cuadrado.webp', 60],
    ['marca/logo-cuadrado.png', 120],
    ['marca/og-portal.jpg', 200],
    ['marca/favicon-32.png', 10],
    ['marca/apple-touch-icon.png', 10],
    ['marca/icon-192.png', 10],
    ['marca/icon-512.png', 60],
  ])('%s pesa como mucho %i KB', (archivo, maximo) => {
    const b = bytes(archivo)
    expect(b.length).toBeGreaterThan(0)
    expect(b.length).toBeLessThanOrEqual(maximo * 1024)
  })

  it('tamaños: 180×180, 192×192, 512×512, 32×32, logo horizontal de 112 px de alto, cuadrado de 320 px de ancho, og 1200×630', () => {
    expect(png(bytes('marca/apple-touch-icon.png'))).toEqual([180, 180])
    expect(png(bytes('marca/icon-192.png'))).toEqual([192, 192])
    expect(png(bytes('marca/icon-512.png'))).toEqual([512, 512])
    expect(png(bytes('marca/favicon-32.png'))).toEqual([32, 32])
    expect(png(bytes('marca/logo-horizontal.png'))[1]).toBe(112)
    expect(png(bytes('marca/logo-cuadrado.png'))[0]).toBe(320)
    expect(jpeg(bytes('marca/og-portal.jpg'))).toEqual([1200, 630])
    // favicon.ico con 16, 32 y 48 (directorio ICO: número de imágenes y anchos).
    const ico = bytes('favicon.ico')
    const n = ico.charCodeAt(4) | (ico.charCodeAt(5) << 8)
    const anchos = Array.from({ length: n }, (_, k) => ico.charCodeAt(6 + 16 * k) || 256).sort((a, b) => a - b)
    expect(anchos).toEqual([16, 32, 48])
  })

  it('CA-P10-11: logos WebP de la portada (encabezado + pie) ≤ 40 KB en total', () => {
    const total = bytes('marca/logo-horizontal.webp').length + bytes('marca/logo-cuadrado.webp').length
    // 40 902 bytes al 2026-11-16: cabe en 40 KiB (40 960) por 58 bytes; ver O-3 de la evidencia.
    expect(total).toBeLessThanOrEqual(40 * 1024)
  })
})

// ---------------------------------------------------------------------------------------------------------------------
describe('QA CA-P10-17, 38: "Mi cuenta" y foco al cambiar de pantalla', () => {
  it('CA-P10-17: /cuenta muestra "VCA AUTOMOTORES" como antetítulo y "Venta de Camiones" como h1; el párrafo no cambia', async () => {
    simularFetch(datos())
    abrirApp('/cuenta')
    const h1 = await screen.findByRole('heading', { level: 1, name: 'Venta de Camiones' })
    expect(h1.previousElementSibling?.textContent).toBe('VCA AUTOMOTORES')
    expect(h1.nextElementSibling?.textContent).toBe('Tu próximo negocio puede comenzar con un camión.')
    expect(screen.queryByText('RED DE VENTA')).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { level: 1, name: /Financiamos Tu Camión/ })).not.toBeInTheDocument()
  })

  it('CA-P10-38: al navegar con un enlace interno (sin recargar) el foco va al h1 de la pantalla nueva', async () => {
    simularFetch(datos())
    abrirApp('/')
    const user = userEvent.setup()
    await user.click(await screen.findByRole('link', { name: V1.titulo }))
    const h1 = await screen.findByRole('heading', { level: 1, name: V1.titulo })
    await waitFor(() => expect(h1).toHaveFocus())
    irA('/vende-tu-camion')
    const h1b = await screen.findByRole('heading', { level: 1, name: /Vende tu camión/i })
    await waitFor(() => expect(h1b).toHaveFocus())
  })
})
