// D-93 (entrega 3a): carrusel de destacados en el inicio. jsdom no calcula el diseño: los anchos y el desplazamiento
// se simulan para comprobar el comportamiento (cuánto avanza cada flecha, extremos, sin animación).
import { fireEvent, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { abrirApp, respuestaFiltros, RUTA_FILTROS, RUTA_INICIO, tarjeta, V1 } from './catalogoDatos'
import { simularFetch } from './helpers'

const DIEZ = Array.from({ length: 10 }, (_, i) =>
  tarjeta({ ...V1, slug: `destacado-${i + 1}`, titulo: `Destacado número ${i + 1}`, destacado: true }),
)

const ANCHO_TARJETA = 300
const ANCHO_VISIBLE = 640
const ANCHO_TOTAL = 3160

type Descriptor = PropertyDescriptor | undefined
const originales: Record<string, Descriptor> = {}

function simularMedidas() {
  for (const nombre of ['scrollWidth', 'clientWidth', 'offsetWidth']) {
    originales[nombre] = Object.getOwnPropertyDescriptor(HTMLElement.prototype, nombre)
  }
  Object.defineProperty(HTMLElement.prototype, 'scrollWidth', {
    configurable: true,
    get(this: HTMLElement) {
      return this.classList.contains('cat-carrusel-pista') ? ANCHO_TOTAL : 0
    },
  })
  Object.defineProperty(HTMLElement.prototype, 'clientWidth', {
    configurable: true,
    get(this: HTMLElement) {
      return this.classList.contains('cat-carrusel-pista') ? ANCHO_VISIBLE : 0
    },
  })
  Object.defineProperty(HTMLElement.prototype, 'offsetWidth', {
    configurable: true,
    get(this: HTMLElement) {
      return this.parentElement?.classList.contains('cat-carrusel-pista') ? ANCHO_TARJETA : 0
    },
  })
}

function restaurarMedidas() {
  for (const [nombre, descriptor] of Object.entries(originales)) {
    if (descriptor) Object.defineProperty(HTMLElement.prototype, nombre, descriptor)
    else delete (HTMLElement.prototype as unknown as Record<string, unknown>)[nombre]
  }
}

async function abrirConDestacados(destacados = DIEZ) {
  simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: { status: 200, body: { destacados, recientes: [] } } })
  abrirApp('/')
  const titulo = await screen.findByRole('heading', { level: 2, name: 'Destacados' })
  return titulo.closest('section') as HTMLElement
}

function pista(seccion: HTMLElement) {
  return seccion.querySelector('.cat-carrusel-pista') as HTMLUListElement
}

describe('Carrusel de destacados', () => {
  let scrollBy: ReturnType<typeof vi.fn>

  beforeEach(() => {
    scrollBy = vi.fn()
    Object.defineProperty(HTMLElement.prototype, 'scrollBy', { value: scrollBy, configurable: true, writable: true })
  })

  afterEach(() => {
    restaurarMedidas()
    delete (HTMLElement.prototype as unknown as Record<string, unknown>).scrollBy
    Reflect.deleteProperty(window, 'matchMedia')
  })

  it('muestra todos los destacados que mande la API en una región con nombre, y mantiene "Ver todos"', async () => {
    const seccion = await abrirConDestacados()
    const region = within(seccion).getByRole('region', { name: 'Vehículos destacados' })
    expect(within(region).getAllByRole('article')).toHaveLength(10)
    expect(within(seccion).getByRole('link', { name: /Ver todos/ })).toHaveAttribute('href', '/vehiculos')
    // Las tarjetas siguen siendo enlaces al detalle (se recorren con Tab).
    expect(within(region).getByRole('link', { name: 'Destacado número 1' })).toHaveAttribute('href', '/vehiculo/destacado-1')
  })

  it('si caben todas, no hay flechas', async () => {
    const seccion = await abrirConDestacados(DIEZ.slice(0, 2))
    expect(within(seccion).queryByRole('button', { name: 'Destacado siguiente' })).not.toBeInTheDocument()
    expect(within(seccion).queryByRole('button', { name: 'Destacado anterior' })).not.toBeInTheDocument()
  })

  it('las flechas controlan la pista; "siguiente" avanza exactamente una tarjeta y "anterior" está desactivada al inicio', async () => {
    simularMedidas()
    const seccion = await abrirConDestacados()
    const user = userEvent.setup()
    const siguiente = await within(seccion).findByRole('button', { name: 'Destacado siguiente' })
    const anterior = within(seccion).getByRole('button', { name: 'Destacado anterior' })
    expect(siguiente).toHaveAttribute('aria-controls', pista(seccion).id)
    expect(anterior).toHaveAttribute('aria-controls', pista(seccion).id)
    expect(anterior).toHaveAttribute('aria-disabled', 'true')
    expect(siguiente).toHaveAttribute('aria-disabled', 'false')

    await user.click(anterior)
    expect(scrollBy).not.toHaveBeenCalled()

    await user.click(siguiente)
    expect(scrollBy).toHaveBeenCalledTimes(1)
    expect(scrollBy).toHaveBeenCalledWith({ left: ANCHO_TARJETA, behavior: 'smooth' })
  })

  it('al llegar al final "siguiente" se desactiva (sin perder el foco) y "anterior" retrocede una tarjeta', async () => {
    simularMedidas()
    const seccion = await abrirConDestacados()
    const user = userEvent.setup()
    const siguiente = await within(seccion).findByRole('button', { name: 'Destacado siguiente' })
    const elemento = pista(seccion)
    Object.defineProperty(elemento, 'scrollLeft', { configurable: true, value: ANCHO_TOTAL - ANCHO_VISIBLE })
    fireEvent.scroll(elemento)
    expect(siguiente).toHaveAttribute('aria-disabled', 'true')
    siguiente.focus()
    await user.click(siguiente)
    expect(scrollBy).not.toHaveBeenCalled()
    expect(siguiente).toHaveFocus()
    await user.click(within(seccion).getByRole('button', { name: 'Destacado anterior' }))
    expect(scrollBy).toHaveBeenCalledWith({ left: -ANCHO_TARJETA, behavior: 'smooth' })
  })

  it('con prefers-reduced-motion el desplazamiento no se anima', async () => {
    simularMedidas()
    Object.defineProperty(window, 'matchMedia', {
      configurable: true,
      writable: true,
      value: vi.fn((consulta: string) => ({ matches: consulta.includes('reduce'), media: consulta })),
    })
    const seccion = await abrirConDestacados()
    await userEvent.setup().click(await within(seccion).findByRole('button', { name: 'Destacado siguiente' }))
    expect(scrollBy).toHaveBeenCalledWith({ left: ANCHO_TARJETA, behavior: 'auto' })
  })

  it('Recientes sigue en rejilla (sin carrusel)', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: { status: 200, body: { destacados: [], recientes: [V1] } } })
    abrirApp('/')
    const recientes = (await screen.findByRole('heading', { level: 2, name: 'Recientes' })).closest('section') as HTMLElement
    expect(recientes.querySelector('.cat-rejilla')).not.toBeNull()
    expect(within(recientes).queryByRole('region', { name: 'Vehículos destacados' })).not.toBeInTheDocument()
  })
})
