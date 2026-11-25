// D-93 (entrega 3a): encabezado fijo con lupa. jsdom no aplica el CSS (fijo arriba, buscador oculto si está cerrado);
// aquí se prueban el estado, la accesibilidad, el teclado y la búsqueda.
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { abrirApp, pagina, respuestaFiltros, RUTA_FILTROS, RUTA_INICIO, rutaPublicaciones, V1 } from './catalogoDatos'
import { simularFetch, ubicacionActual } from './helpers'

const INICIO_VACIO = { status: 200, body: { destacados: [], recientes: [] } }

function encabezado() {
  return screen.getByRole('banner')
}

/** La lupa (botón que despliega); el botón "Buscar" del formulario no tiene aria-expanded. */
function lupa() {
  const lupas = within(encabezado())
    .getAllByRole('button', { name: 'Buscar' })
    .filter((b) => b.hasAttribute('aria-expanded'))
  expect(lupas).toHaveLength(1)
  return lupas[0]
}

describe('Encabezado fijo con lupa', () => {
  it('fuera del inicio: la lupa controla el buscador plegado; al abrir el foco va al campo y Esc lo devuelve a la lupa', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros() })
    abrirApp('/cuenta')
    const buscador = await within(encabezado()).findByRole('search')
    expect(lupa()).toHaveAttribute('aria-expanded', 'false')
    expect(lupa()).toHaveAttribute('aria-controls', buscador.id)
    expect(buscador).toHaveAttribute('data-abierta', 'false')

    const user = userEvent.setup()
    await user.click(lupa())
    expect(lupa()).toHaveAttribute('aria-expanded', 'true')
    expect(buscador).toHaveAttribute('data-abierta', 'true')
    expect(within(buscador).getByRole('searchbox', { name: 'Buscar vehículos' })).toHaveFocus()

    await user.keyboard('{Escape}')
    expect(lupa()).toHaveAttribute('aria-expanded', 'false')
    expect(lupa()).toHaveFocus()
  })

  it('en el inicio el encabezado no tiene buscador hasta abrir la lupa (el de la portada sigue igual)', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: INICIO_VACIO })
    abrirApp('/')
    await screen.findByText('Pronto tendremos vehículos publicados.')
    expect(within(encabezado()).queryByRole('searchbox')).not.toBeInTheDocument()
    expect(encabezado()).toHaveAttribute('data-inicio', 'true')

    const user = userEvent.setup()
    await user.click(lupa())
    const campo = within(encabezado()).getByRole('searchbox', { name: 'Buscar vehículos' })
    expect(campo).toHaveFocus()
    await user.keyboard('{Escape}')
    expect(within(encabezado()).queryByRole('searchbox')).not.toBeInTheDocument()
    expect(lupa()).toHaveFocus()
  })

  it('buscar desde la lupa navega a /vehiculos?q=… y cierra el buscador', async () => {
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: INICIO_VACIO,
      [rutaPublicaciones('q=npr')]: pagina([V1]),
    })
    abrirApp('/')
    await screen.findByText('Pronto tendremos vehículos publicados.')
    const user = userEvent.setup()
    await user.click(lupa())
    await user.type(within(encabezado()).getByRole('searchbox', { name: 'Buscar vehículos' }), 'npr{Enter}')
    await waitFor(() => expect(ubicacionActual()).toBe('/vehiculos?q=npr'))
    expect(lupa()).toHaveAttribute('aria-expanded', 'false')
    expect(await screen.findByRole('link', { name: V1.titulo })).toBeInTheDocument()
  })

  it('abrir el menú cierra el buscador y abrir el buscador cierra el menú', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros() })
    abrirApp('/cuenta')
    await within(encabezado()).findByRole('search')
    const user = userEvent.setup()
    const menu = within(encabezado()).getByRole('button', { name: 'Menú' })
    await user.click(lupa())
    await user.click(menu)
    expect(menu).toHaveAttribute('aria-expanded', 'true')
    expect(lupa()).toHaveAttribute('aria-expanded', 'false')
    await user.click(lupa())
    expect(lupa()).toHaveAttribute('aria-expanded', 'true')
    expect(menu).toHaveAttribute('aria-expanded', 'false')
  })

  it('publica su alto en --alto-encabezado (para scroll-padding-top)', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros() })
    abrirApp('/cuenta')
    await within(encabezado()).findByRole('search')
    expect(document.documentElement.style.getPropertyValue('--alto-encabezado')).toMatch(/^\d+px$/)
  })
})
