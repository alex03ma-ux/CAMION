// D-93 (entrega 3b): el sistema de diseño en las páginas públicas. jsdom no aplica el CSS: aquí se comprueba la
// estructura de la que dependen los estilos (clases públicas, tarjeta de la portada) y la carga de las fotos.
import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { abrirApp, detalle, respuestaFiltros, RUTA_FILTROS, RUTA_INICIO, rutaDetalle, V1 } from './catalogoDatos'
import { guardarSesionFicticia, PERFIL_FICTICIO, simularFetch } from './helpers'

const FOTOS = [
  { url: '/fotos/ficticia-1.jpg', orden: 1, esPortada: true },
  { url: '/fotos/ficticia-2.jpg', orden: 2, esPortada: false },
]

describe('Páginas públicas (D-93, 3b)', () => {
  it('ficha: la foto principal se pide de inmediato y con prioridad alta; las miniaturas, en diferido', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1, { fotos: FOTOS }) } })
    abrirApp(`/vehiculo/${V1.slug}`)
    const principal = await screen.findByRole('img', { name: `${V1.titulo}, foto 1 de 2` })
    expect(principal).toHaveAttribute('loading', 'eager')
    expect(principal).toHaveAttribute('fetchpriority', 'high')
    // Las miniaturas son decorativas (alt vacío, sin rol img): se buscan por etiqueta.
    const miniaturas = Array.from(screen.getByRole('list', { name: 'Miniaturas' }).querySelectorAll('img'))
    expect(miniaturas.length).toBe(FOTOS.length)
    for (const m of miniaturas) expect(m).toHaveAttribute('loading', 'lazy')
    // "Volver a resultados" sigue siendo el enlace de siempre, marcado para volver a la misma posición (3a).
    expect(screen.getByRole('link', { name: 'Volver a resultados' })).toHaveAttribute('data-restaurar-scroll')
  })

  it('portada: textos y buscador van juntos en la tarjeta blanca, sin cambiar ningún texto', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: { status: 200, body: { destacados: [], recientes: [] } } })
    const { container } = abrirApp('/')
    await screen.findByText('Pronto tendremos vehículos publicados.')
    const tarjeta = container.querySelector('.cat-portada .cat-portada-tarjeta') as HTMLElement
    expect(tarjeta).not.toBeNull()
    expect(within(tarjeta).getByRole('heading', { level: 1, name: 'Venta de camiones usados' })).toBeInTheDocument()
    expect(tarjeta).toHaveTextContent('Tu próximo negocio empieza con el vehículo correcto')
    expect(tarjeta).toHaveTextContent('Compra con respaldo y te ayudamos con la financiación.')
    expect(within(tarjeta).getByRole('search')).toBeInTheDocument()
    // La imagen queda fuera de la tarjeta (a la derecha en escritorio, arriba en celular).
    expect(tarjeta.querySelector('.cat-portada-imagen')).toBeNull()
    // Estructura del eje común: la foto va en su propio marco dentro del contenedor de la portada (ya no es una
    // capa absoluta sobre todo el ancho), junto a la tarjeta.
    const interior = container.querySelector('.cat-portada > .cat-portada-interior') as HTMLElement
    expect(interior).not.toBeNull()
    expect(interior.querySelector(':scope > .cat-portada-figura > .cat-portada-imagen')).not.toBeNull()
    expect(interior.querySelector(':scope > .cat-portada-tarjeta')).toBe(tarjeta)
    expect(container.querySelector('.cat-portada > .cat-portada-imagen')).toBeNull()
    // "Ver catálogo" sigue en las categorías (oculto al lector; en celular, también a la vista con CSS).
    const categorias = screen.getByRole('list', { name: 'Categorías' })
    expect(within(categorias).getAllByText(/Ver catálogo/).length).toBeGreaterThan(0)
  })

  it.each([
    ['/cuenta', 'inicio de sesión'],
    ['/cuenta/registro', 'registro'],
    ['/restablecer-contrasena#codigo=Prueba_1', 'restablecer contraseña'],
  ])('%s (%s) usa el contenedor público "cuenta-publica"', async (direccion) => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros() })
    const { container } = abrirApp(direccion)
    await screen.findByRole('heading', { level: 1 })
    expect(container.querySelector('.container.cuenta.cuenta-publica')).not.toBeNull()
    expect(container.querySelector('.cuenta-publica form')).not.toBeNull()
  })

  it('con sesión, el perfil conserva el contenedor de siempre (no es una página pública)', async () => {
    guardarSesionFicticia(Date.now() + 2 * 60 * 60 * 1000)
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), '/api/auth/me': { status: 200, body: PERFIL_FICTICIO } })
    const { container } = abrirApp('/cuenta')
    await screen.findByText(PERFIL_FICTICIO.nombreCompleto, { exact: false })
    expect(container.querySelector('.container.cuenta')).not.toBeNull()
    expect(container.querySelector('.cuenta-publica')).toBeNull()
  })
})
