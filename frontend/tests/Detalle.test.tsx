// Detalle público del vehículo y galería (P3: CA-DET-10 a 15, CA-D17-06 a 10, CA-D11-02).
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { DetalleVehiculo } from '../src/api/catalogo'
import {
  abrirApp,
  C1,
  C2,
  detalle,
  K0,
  M1,
  M2,
  pagina,
  respuestaFiltros,
  RUTA_FILTROS,
  rutaDetalle,
  rutaPublicaciones,
  V1,
  V2,
} from './catalogoDatos'
import { cabecera, guardarSesionFicticia, PERFIL_FICTICIO, simularFetch } from './helpers'

const FOTOS = [
  { url: '/fotos/ficticia-3.jpg', orden: 3, esPortada: false },
  { url: '/fotos/ficticia-1.jpg', orden: 1, esPortada: false },
  { url: '/fotos/ficticia-2.jpg', orden: 2, esPortada: true },
]

const V2_DETALLE = detalle(V2, {
  descripcionComercial: 'Tractocamión listo para trabajar.',
  descripcionTecnica: 'Motor revisado, llantas al 70 %.',
  carroceria: null,
  combustible: 'Diésel',
  capacidadCargaKg: 30000,
  pesoBrutoKg: null,
  cilindrajeCc: 14000,
})

function abrirDetalle(d: DetalleVehiculo, extra = {}) {
  const api = simularFetch({
    [RUTA_FILTROS]: respuestaFiltros(),
    [rutaDetalle(d.slug)]: { status: 200, body: d },
    ...extra,
  })
  abrirApp(`#/vehiculo/${d.slug}`)
  return api
}

function ficha(): Record<string, string> {
  const dl = screen.getByRole('heading', { name: 'Ficha técnica' }).parentElement?.querySelector('dl') as HTMLElement
  const filas: Record<string, string> = {}
  for (const div of Array.from(dl.children)) {
    filas[div.querySelector('dt')?.textContent ?? ''] = div.querySelector('dd')?.textContent ?? ''
  }
  return filas
}

describe('Detalle', () => {
  it('CA-DET-10 V2: h1, precio, Negociable, Reservado, ficha solo con valores y descripciones', async () => {
    const api = abrirDetalle(V2_DETALLE)
    expect(await screen.findByRole('heading', { level: 1, name: V2.titulo })).toBeInTheDocument()
    expect(api.llamadasA(rutaDetalle('kenworth-t800-2018'))).toHaveLength(1)
    expect(screen.getByText('$ 250.000.000')).toBeInTheDocument()
    expect(screen.getByText('Negociable')).toBeInTheDocument()
    expect(screen.getByText('Reservado')).toBeInTheDocument()
    expect(screen.queryByText('Destacado')).not.toBeInTheDocument()
    expect(ficha()).toEqual({
      Categoría: 'Camión',
      Marca: 'Kenworth',
      Modelo: 'T800',
      Año: '2018',
      Kilometraje: '300.000 km',
      Combustible: 'Diésel',
      'Capacidad de carga': '30.000 kg',
      Cilindraje: '14.000 cc',
      Ubicación: 'Medellín, Antioquia',
    })
    const main = screen.getByRole('main')
    expect(main).not.toHaveTextContent(/null|undefined|NaN/)
    expect(screen.getByText('Tractocamión listo para trabajar.')).toBeInTheDocument()
    expect(screen.getByText('Motor revisado, llantas al 70 %.')).toBeInTheDocument()
    expect(document.title).toBe(`${V2.titulo} | Financiamos Tu Camión`)
  })

  it('CA-D11-02 DISPONIBLE y no negociable: sin etiquetas Reservado ni Negociable', async () => {
    abrirDetalle(detalle(V1))
    await screen.findByRole('heading', { level: 1, name: V1.titulo })
    expect(screen.queryByText('Reservado')).not.toBeInTheDocument()
    expect(screen.queryByText('Negociable')).not.toBeInTheDocument()
    expect(screen.getByText('Destacado')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Descripción' })).not.toBeInTheDocument()
  })

  it.each([
    ['CA-D17-06 Carrocería con 0', C1],
    ['CA-D17-06 Carrocería con 500', C2],
    ['CA-D17-07 Maquinaria con 0', M1],
  ])('%s: la ficha no muestra kilometraje', async (_n, t) => {
    abrirDetalle(detalle(t))
    await screen.findByRole('heading', { level: 1, name: t.titulo })
    expect(ficha()).not.toHaveProperty('Kilometraje')
    expect(screen.getByRole('main')).not.toHaveTextContent(/\bkm\b/)
  })

  it.each([
    ['CA-D17-08 Maquinaria con 1200', M2, '1.200 km'],
    ['CA-D17-08 Camión con 0', K0, '0 km'],
  ])('%s: la ficha muestra el kilometraje', async (_n, t, km) => {
    abrirDetalle(detalle(t))
    await screen.findByRole('heading', { level: 1, name: t.titulo })
    expect(ficha().Kilometraje).toBe(km)
  })

  it('CA-D17-10 la ficha de C1 solo trae campos actuales con valor (sin campos inventados de D-19)', async () => {
    abrirDetalle(detalle(C1, { carroceria: 'Furgón', modeloAnio: null, linea: null }))
    await screen.findByRole('heading', { level: 1, name: C1.titulo })
    const nombres = Object.keys(ficha())
    expect(nombres).toEqual(['Categoría', 'Carrocería', 'Marca', 'Ubicación'])
    expect(screen.getByRole('main')).not.toHaveTextContent(/medidas|material|horas de uso/i)
  })

  it('CA-DET-14 un 404 muestra "Este vehículo no está disponible." y enlace al inicio, sin datos', async () => {
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [rutaDetalle('no-existe')]: { status: 404, body: { error: 'Este vehículo no está disponible.' } },
    })
    abrirApp('#/vehiculo/no-existe')
    expect(await screen.findByRole('heading', { level: 1, name: 'Este vehículo no está disponible.' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ir al inicio' })).toHaveAttribute('href', '#/')
    expect(screen.queryByText(/\$\s/)).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Ficha técnica' })).not.toBeInTheDocument()
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })

  it('CA-DET-14 un 404 sin cuerpo usa el mismo mensaje', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [rutaDetalle('x')]: { status: 404 } })
    abrirApp('#/vehiculo/x')
    expect(await screen.findByRole('heading', { level: 1, name: 'Este vehículo no está disponible.' })).toBeInTheDocument()
  })

  it('CA-UI-10 error de red en el detalle: mensaje y Reintentar', async () => {
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [rutaDetalle(V1.slug)]: [{ red: true }, { status: 200, body: detalle(V1) }],
    })
    abrirApp(`#/vehiculo/${V1.slug}`)
    expect(screen.getByText('Cargando vehículo...')).toHaveAttribute('role', 'status')
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo conectar con el servidor.')
    await userEvent.setup().click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByRole('heading', { level: 1, name: V1.titulo })).toBeInTheDocument()
  })

  it('CA-DET-13 "Volver a resultados" vuelve a la búsqueda de la que se vino, con filtros y página', async () => {
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [rutaPublicaciones('marcaId=10&pagina=2')]: pagina([V1], 30, 2),
      [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1) },
    })
    abrirApp('#/buscar?marcaId=10&pagina=2')
    const user = userEvent.setup()
    await user.click(await screen.findByRole('link', { name: V1.titulo }))
    await screen.findByRole('heading', { level: 1, name: V1.titulo })
    expect(screen.getByRole('link', { name: 'Volver a resultados' })).toHaveAttribute(
      'href',
      '#/buscar?marcaId=10&pagina=2',
    )
  })

  it('CA-SE005-08 / CA-D03-02 con sesión de comprador: mismo detalle, sin botones de gestión ni contacto, sin token', async () => {
    guardarSesionFicticia(Date.now() + 2 * 60 * 60 * 1000)
    const api = abrirDetalle(detalle(V1, { fotos: FOTOS }), {
      '/api/auth/me': { status: 200, body: PERFIL_FICTICIO },
    })
    await screen.findByRole('heading', { level: 1, name: V1.titulo })
    const nombres = screen.getAllByRole('button').map((b) => b.textContent + ' ' + (b.getAttribute('aria-label') ?? ''))
    for (const n of nombres) expect(n).not.toMatch(/contactar|editar|eliminar|vendido|destacar|ocultar/i)
    for (const l of api.llamadas.filter((x) => x.url.startsWith('/api/catalogo'))) {
      expect(cabecera(l.init, 'Authorization')).toBeUndefined()
    }
  })
})

describe('Galería', () => {
  it('CA-DET-11 abre en la portada; miniaturas como botones con aria-current; texto alternativo "<título>, foto N de 3"', async () => {
    abrirDetalle(detalle(V1, { fotos: FOTOS }))
    await screen.findByRole('heading', { level: 1, name: V1.titulo })
    const galeria = screen.getByRole('group', { name: `Fotos de ${V1.titulo}` })
    const principal = () => within(galeria).getByRole('img', { name: /foto \d de 3$/ })
    expect(principal()).toHaveAccessibleName(`${V1.titulo}, foto 2 de 3`)
    expect(principal()).toHaveAttribute('src', '/fotos/ficticia-2.jpg')
    const miniaturas = within(galeria).getAllByRole('button', { name: /^Ver foto \d de 3$/ })
    expect(miniaturas).toHaveLength(3)
    expect(miniaturas[1]).toHaveAttribute('aria-current', 'true')
    expect(miniaturas[0]).not.toHaveAttribute('aria-current')

    const user = userEvent.setup()
    await user.click(miniaturas[2])
    expect(principal()).toHaveAccessibleName(`${V1.titulo}, foto 3 de 3`)
    expect(within(galeria).getByRole('button', { name: 'Ver foto 3 de 3' })).toHaveAttribute('aria-current', 'true')
    expect(galeria).toHaveTextContent('Foto 3 de 3')
  })

  it('CA-DET-11 teclado: Tab llega a la miniatura activa, flechas/Inicio/Fin recorren, Enter y Espacio eligen', async () => {
    abrirDetalle(detalle(V1, { fotos: FOTOS }))
    await screen.findByRole('heading', { level: 1, name: V1.titulo })
    const galeria = screen.getByRole('group', { name: `Fotos de ${V1.titulo}` })
    const principal = () => within(galeria).getByRole('img', { name: /foto \d de 3$/ })
    const mini = (n: number) => within(galeria).getByRole('button', { name: `Ver foto ${n} de 3` })
    const user = userEvent.setup()

    mini(2).focus()
    await user.keyboard('{ArrowRight}')
    expect(mini(3)).toHaveFocus()
    expect(principal()).toHaveAccessibleName(`${V1.titulo}, foto 3 de 3`)
    await user.keyboard('{ArrowRight}')
    expect(mini(1)).toHaveFocus()
    expect(principal()).toHaveAccessibleName(`${V1.titulo}, foto 1 de 3`)
    await user.keyboard('{ArrowLeft}')
    expect(mini(3)).toHaveFocus()
    await user.keyboard('{Home}')
    expect(mini(1)).toHaveAttribute('aria-current', 'true')
    await user.keyboard('{End}')
    expect(mini(3)).toHaveAttribute('aria-current', 'true')
    // La miniatura activa es la que queda en el orden de tabulación.
    expect(mini(3)).toHaveAttribute('tabindex', '0')
    expect(mini(1)).toHaveAttribute('tabindex', '-1')

    // Enter y Espacio activan el botón enfocado.
    act(() => mini(1).focus())
    await user.keyboard('{Enter}')
    expect(mini(1)).toHaveAttribute('aria-current', 'true')
    act(() => mini(2).focus())
    await user.keyboard(' ')
    expect(mini(2)).toHaveAttribute('aria-current', 'true')
  })

  it('CA-DET-11 botones "Foto anterior" y "Foto siguiente" con vuelta al inicio', async () => {
    abrirDetalle(detalle(V1, { fotos: FOTOS }))
    await screen.findByRole('heading', { level: 1, name: V1.titulo })
    const galeria = screen.getByRole('group', { name: `Fotos de ${V1.titulo}` })
    const principal = () => within(galeria).getByRole('img', { name: /foto \d de 3$/ })
    const user = userEvent.setup()
    await user.click(within(galeria).getByRole('button', { name: 'Foto siguiente' }))
    expect(principal()).toHaveAccessibleName(`${V1.titulo}, foto 3 de 3`)
    await user.click(within(galeria).getByRole('button', { name: 'Foto siguiente' }))
    expect(principal()).toHaveAccessibleName(`${V1.titulo}, foto 1 de 3`)
    await user.click(within(galeria).getByRole('button', { name: 'Foto anterior' }))
    expect(principal()).toHaveAccessibleName(`${V1.titulo}, foto 3 de 3`)
  })

  it('CA-DET-12 si la foto principal falla se ve la imagen de reemplazo con texto alternativo', async () => {
    abrirDetalle(detalle(V1, { fotos: FOTOS }))
    await screen.findByRole('heading', { level: 1, name: V1.titulo })
    const galeria = screen.getByRole('group', { name: `Fotos de ${V1.titulo}` })
    fireEvent.error(within(galeria).getByRole('img', { name: `${V1.titulo}, foto 2 de 3` }))
    await waitFor(() => expect(within(galeria).getByRole('img', { name: 'Sin foto disponible' })).toBeInTheDocument())
  })

  it('sin fotos: imagen de reemplazo y sin miniaturas; una sola foto: sin flechas', async () => {
    abrirDetalle(detalle(V2))
    await screen.findByRole('heading', { level: 1, name: V2.titulo })
    expect(screen.getByRole('img', { name: 'Sin foto disponible' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Ver foto/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Foto siguiente' })).not.toBeInTheDocument()
  })
})
