// P10: direcciones sin `#` en la aplicación (CA-P10-35 a 45, 50).
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import App from '../src/App'
import { MENSAJE_CATEGORIA_VACIA } from '../src/components/CategoriaSinTipo'
import {
  abrirApp,
  detalle,
  pagina,
  respuestaFiltros,
  RUTA_FILTROS,
  RUTA_INICIO,
  rutaDetalle,
  rutaPublicaciones,
  TIPOS,
  V1,
} from './catalogoDatos'
import { irA, simularFetch, ubicacionActual } from './helpers'

const INICIO = { status: 200, body: { destacados: [], recientes: [V1] } }

function datosBase() {
  return {
    [RUTA_FILTROS]: respuestaFiltros(),
    [RUTA_INICIO]: INICIO,
    [rutaPublicaciones()]: pagina([V1]),
    [rutaPublicaciones('tipoId=2')]: pagina([V1]),
    [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1) },
  }
}

/** Hace clic y dice si la app atendió el enlace (preventDefault); evita la navegación real de jsdom. */
function atendidoPorLaApp(enlace: Element, opciones: MouseEventInit = {}): boolean {
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

describe('Enlaces internos sin recargar (CA-P10-35, 36)', () => {
  it('un clic normal cambia de pantalla con pushState y no hay hrefs "#/" en el DOM', async () => {
    simularFetch(datosBase())
    abrirApp('/')
    const largo = window.history.length
    const user = userEvent.setup()
    await user.click(await screen.findByRole('link', { name: V1.titulo }))
    expect(await screen.findByRole('heading', { level: 1, name: V1.titulo })).toBeInTheDocument()
    expect(ubicacionActual()).toBe(`/vehiculo/${V1.slug}`)
    expect(window.history.length).toBe(largo + 1)
    const conHashViejo = Array.from(document.querySelectorAll('a[href]')).filter((a) =>
      (a.getAttribute('href') ?? '').startsWith('#/'),
    )
    expect(conHashViejo).toEqual([])
  })

  it('con Ctrl, Mayús, Cmd o clic central no se intercepta (el navegador abre otra pestaña)', async () => {
    simularFetch(datosBase())
    abrirApp('/')
    const enlace = await screen.findByRole('link', { name: V1.titulo })
    for (const opciones of [{ ctrlKey: true }, { shiftKey: true }, { metaKey: true }, { button: 1 }]) {
      expect(atendidoPorLaApp(enlace, opciones)).toBe(false)
      expect(ubicacionActual()).toBe('/')
    }
    expect(atendidoPorLaApp(enlace)).toBe(true)
  })

  it('no intercepta /api, /media, /health, mailto, target=_blank ni otros dominios', async () => {
    simularFetch(datosBase())
    abrirApp('/')
    await screen.findByRole('link', { name: V1.titulo })
    const contenedor = document.createElement('div')
    contenedor.innerHTML =
      '<a href="/api/x">api</a><a href="/media/f.jpg">media</a><a href="/health">salud</a>' +
      '<a href="mailto:a@b.co">correo</a><a href="https://ejemplo.test/x">fuera</a>' +
      '<a href="/vehiculos" target="_blank">nueva</a>'
    document.body.appendChild(contenedor)
    for (const a of Array.from(contenedor.querySelectorAll('a'))) expect(atendidoPorLaApp(a)).toBe(false)
    contenedor.remove()
    expect(ubicacionActual()).toBe('/')
  })

  it('CA-P10-37: Atrás (popstate) vuelve a la búsqueda y "Volver a resultados" lleva a la última búsqueda', async () => {
    simularFetch({ ...datosBase(), [rutaPublicaciones('marcaId=10')]: pagina([V1]) })
    abrirApp('/vehiculos?marcaId=10')
    const user = userEvent.setup()
    await user.click(await screen.findByRole('link', { name: V1.titulo }))
    await screen.findByRole('heading', { level: 1, name: V1.titulo })
    expect(screen.getByRole('link', { name: 'Volver a resultados' })).toHaveAttribute('href', '/vehiculos?marcaId=10')
    irA('/vehiculos?marcaId=10')
    expect(await screen.findByRole('heading', { level: 1, name: 'Buscar vehículos' })).toBeInTheDocument()
    expect(ubicacionActual()).toBe('/vehiculos?marcaId=10')
  })

  it('CA-P10-45: "Saltar al contenido" mueve el foco sin cambiar de pantalla ni de dirección', async () => {
    simularFetch(datosBase())
    abrirApp('/vehiculos')
    await screen.findByRole('heading', { level: 1, name: 'Buscar vehículos' })
    const user = userEvent.setup()
    await user.click(screen.getByRole('link', { name: 'Saltar al contenido' }))
    expect(document.activeElement).toBe(screen.getByRole('main'))
    expect(window.location.href).toBe(`${window.location.origin}/vehiculos`)
  })
})

describe('Categorías (CA-P10-25, 39, 40)', () => {
  it('/vehiculos?tipoId=2 se reemplaza por /camiones y muestra los camiones', async () => {
    const api = simularFetch(datosBase())
    abrirApp('/vehiculos?tipoId=2')
    expect(await screen.findByRole('heading', { level: 1, name: 'Camiones' })).toBeInTheDocument()
    await waitFor(() => expect(ubicacionActual()).toBe('/camiones'))
    expect(await screen.findByRole('link', { name: V1.titulo })).toBeInTheDocument()
    expect(api.llamadasA(rutaPublicaciones('tipoId=2')).length).toBeGreaterThan(0)
    expect(within(screen.getByRole('navigation', { name: 'Principal' })).getByRole('link', { name: 'Camiones' })).toHaveAttribute(
      'aria-current',
      'page',
    )
  })

  it('una categoría sin vehículos visibles muestra el aviso (no "no encontrada")', async () => {
    simularFetch({ ...datosBase(), [RUTA_FILTROS]: respuestaFiltros(TIPOS.filter((t) => t.nombre !== 'Maquinaria')) })
    abrirApp('/maquinaria')
    expect(await screen.findByText(MENSAJE_CATEGORIA_VACIA)).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Maquinaria' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Página no encontrada' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ver todos los vehículos' })).toHaveAttribute('href', '/vehiculos')
  })
})

describe('Enlaces viejos con "#/" (CA-P10-41)', () => {
  it.each([
    [`/#/vehiculo/${V1.slug}`, `/vehiculo/${V1.slug}`, V1.titulo],
    ['/#/buscar?tipoId=2', '/camiones', 'Camiones'],
    ['/#/buscar', '/vehiculos', 'Buscar vehículos'],
    ['/#/no-existe', '/no-existe', 'Página no encontrada'],
  ])('%s → %s', async (vieja, nueva, h1) => {
    simularFetch(datosBase())
    abrirApp(vieja)
    expect(await screen.findByRole('heading', { level: 1, name: h1 })).toBeInTheDocument()
    await waitFor(() => expect(ubicacionActual()).toBe(nueva))
    expect(window.location.hash).toBe('')
  })

  it('un enlace viejo pegado con la app abierta (hashchange) también se convierte', async () => {
    simularFetch(datosBase())
    abrirApp('/')
    await screen.findByRole('link', { name: V1.titulo })
    act(() => {
      window.history.pushState(null, '', `/#/vehiculo/${V1.slug}`)
      window.dispatchEvent(new HashChangeEvent('hashchange'))
    })
    expect(await screen.findByRole('heading', { level: 1, name: V1.titulo })).toBeInTheDocument()
    expect(ubicacionActual()).toBe(`/vehiculo/${V1.slug}`)
    expect(window.location.hash).toBe('')
  })
})

describe('Recuperación de contraseña con las rutas nuevas (CA-P10-42 a 44)', () => {
  const RUTA = '/api/auth/restablecer-contrasena'
  const MENSAJE = 'Listo. Tu contraseña cambió; inicia sesión con la nueva.'
  const OK = { status: 200, body: { mensaje: MENSAJE } }

  async function cambiar(codigo: string, api: ReturnType<typeof simularFetch>) {
    const user = userEvent.setup()
    await user.type(await screen.findByLabelText('Contraseña nueva'), 'Nueva1234')
    await user.type(screen.getByLabelText('Confirma la contraseña nueva'), 'Nueva1234')
    await user.click(screen.getByRole('button', { name: 'Guardar contraseña nueva' }))
    await screen.findByText(MENSAJE)
    expect(String(api.llamadasA(RUTA)[0].init?.body)).toContain(codigo)
  }

  it('enlace nuevo /restablecer-contrasena#codigo=…: se lee y se quita de la dirección', async () => {
    const api = simularFetch({ [RUTA]: OK })
    irA('/restablecer-contrasena#codigo=Nuevo_123')
    render(<App />)
    await screen.findByRole('heading', { level: 1, name: 'Crear una contraseña nueva' })
    await waitFor(() => expect(window.location.href).toBe(`${window.location.origin}/restablecer-contrasena`))
    await cambiar('Nuevo_123', api)
  })

  it('enlace viejo /#/restablecer-contrasena?codigo=…: el código nunca pasa a la ruta ni a la consulta', async () => {
    const api = simularFetch({ [RUTA]: OK })
    const direcciones: string[] = []
    const historial = window.history
    const reemplazar = historial.replaceState.bind(historial)
    const empujar = historial.pushState.bind(historial)
    reemplazar(null, '', '/#/restablecer-contrasena?codigo=Viejo_456')
    historial.replaceState = (estado: unknown, titulo: string, url?: string | URL | null) => {
      direcciones.push(String(url))
      reemplazar(estado, titulo, url)
    }
    historial.pushState = (estado: unknown, titulo: string, url?: string | URL | null) => {
      direcciones.push(String(url))
      empujar(estado, titulo, url)
    }
    try {
      render(<App />)
      await screen.findByRole('heading', { level: 1, name: 'Crear una contraseña nueva' })
      expect(window.location.href).toBe(`${window.location.origin}/restablecer-contrasena`)
      expect(direcciones.length).toBeGreaterThan(0)
      expect(direcciones.every((d) => !d.includes('Viejo_456'))).toBe(true)
      await cambiar('Viejo_456', api)
    } finally {
      historial.replaceState = reemplazar
      historial.pushState = empujar
    }
  })
})

describe('Código fuente (CA-P10-35)', () => {
  const fuentes = import.meta.glob('../src/**/*.{ts,tsx}', { query: '?raw', import: 'default', eager: true }) as Record<
    string,
    string
  >

  it("solo el convertidor de rutas.ts contiene '#/ en src", () => {
    const con = Object.entries(fuentes)
      .filter(([, texto]) => texto.includes("'#/"))
      .map(([archivo]) => archivo)
    expect(con).toEqual(['../src/rutas.ts'])
  })
})
