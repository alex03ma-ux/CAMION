// D-93 (entrega 3a): volver de la ficha al listado en la misma posición. jsdom no desplaza la página: se simula
// window.scrollY y se comprueba a dónde se lleva la página con window.scrollTo (simulado en setup.ts).
import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { abrirApp, detalle, pagina, respuestaFiltros, RUTA_FILTROS, rutaDetalle, rutaPublicaciones, V1, V2 } from './catalogoDatos'
import { simularFetch, ubicacionActual } from './helpers'

function fijarScrollY(y: number) {
  Object.defineProperty(window, 'scrollY', { configurable: true, value: y })
}

function scrollTo() {
  return window.scrollTo as unknown as ReturnType<typeof vi.fn>
}

function abrirListado() {
  simularFetch({
    [RUTA_FILTROS]: respuestaFiltros(),
    [rutaPublicaciones('')]: pagina([V1, V2]),
    [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1) },
  })
  abrirApp('/vehiculos')
}

async function irALaFicha() {
  const user = userEvent.setup()
  fijarScrollY(800)
  await user.click(await screen.findByRole('link', { name: V1.titulo }))
  await waitFor(() => expect(ubicacionActual()).toBe(`/vehiculo/${V1.slug}`))
  await screen.findByRole('heading', { level: 1, name: V1.titulo })
  return user
}

afterEach(() => {
  Reflect.deleteProperty(window, 'scrollY')
  Reflect.deleteProperty(window.history, 'scrollRestoration')
})

describe('Restauración del scroll', () => {
  it('la restauración del navegador pasa a manual', async () => {
    // jsdom no tiene history.scrollRestoration: se simula como en los navegadores (empieza en 'auto').
    Object.defineProperty(window.history, 'scrollRestoration', { configurable: true, writable: true, value: 'auto' })
    abrirListado()
    await screen.findByRole('link', { name: V1.titulo })
    expect(window.history.scrollRestoration).toBe('manual')
  })

  it('una navegación nueva lleva la página arriba y el foco al h1 no la mueve (preventScroll)', async () => {
    abrirListado()
    const foco = vi.spyOn(HTMLElement.prototype, 'focus')
    await irALaFicha()
    expect(scrollTo()).toHaveBeenLastCalledWith(0, 0)
    const llamadasAlH1 = foco.mock.contexts
      .map((elemento, i) => [elemento, foco.mock.calls[i]] as const)
      .filter(([elemento]) => elemento instanceof HTMLHeadingElement && elemento.tagName === 'H1')
    expect(llamadasAlH1.length).toBeGreaterThan(0)
    for (const [, argumentos] of llamadasAlH1) expect(argumentos[0]).toEqual({ preventScroll: true })
  })

  it('Atrás desde la ficha vuelve al listado en la misma posición, cuando la lista ya está pintada', async () => {
    abrirListado()
    await irALaFicha()
    fijarScrollY(0)
    scrollTo().mockClear()
    await act(async () => {
      window.history.back()
    })
    await waitFor(() => expect(ubicacionActual()).toBe('/vehiculos'))
    await screen.findByRole('link', { name: V1.titulo })
    await waitFor(() => expect(scrollTo()).toHaveBeenLastCalledWith(0, 800))
  })

  it('"Volver a resultados" también vuelve a la misma posición', async () => {
    abrirListado()
    const user = await irALaFicha()
    fijarScrollY(0)
    scrollTo().mockClear()
    await user.click(screen.getByRole('link', { name: 'Volver a resultados' }))
    await waitFor(() => expect(ubicacionActual()).toBe('/vehiculos'))
    await screen.findByRole('link', { name: V1.titulo })
    await waitFor(() => expect(scrollTo()).toHaveBeenLastCalledWith(0, 800))
  })

  it('un enlace normal al listado (no "Volver") empieza arriba', async () => {
    abrirListado()
    await irALaFicha()
    scrollTo().mockClear()
    const user = userEvent.setup()
    await user.click(within(screen.getByRole('contentinfo')).getAllByRole('link', { name: 'Camiones' })[0])
    await waitFor(() => expect(ubicacionActual()).toBe('/camiones'))
    expect(scrollTo()).toHaveBeenLastCalledWith(0, 0)
  })

  it('en sessionStorage solo se guardan posiciones (números), sin datos personales', async () => {
    abrirListado()
    await irALaFicha()
    const guardado = JSON.parse(window.sessionStorage.getItem('camion.scroll') ?? '{}') as Record<string, unknown>
    expect(Object.keys(guardado).length).toBeGreaterThan(0)
    for (const valor of Object.values(guardado)) expect(typeof valor).toBe('number')
    expect(Object.values(guardado)).toContain(800)
  })
})
