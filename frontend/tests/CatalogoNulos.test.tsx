// P4a: el catálogo público (P3) tolera `marca` y `modeloAnio` null (carrocerías sin
// marca, D-19) sin mostrar "null" ni descartar la tarjeta. También CA-VIS7-02 (Reservado).
import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import {
  abrirApp,
  detalle,
  respuestaFiltros,
  RUTA_FILTROS,
  RUTA_INICIO,
  rutaDetalle,
  tarjeta,
} from './catalogoDatos'
import { simularFetch } from './helpers'

const SIN_MARCA_SIN_TITULO = {
  ...tarjeta({ slug: 'carroceria-furgon', titulo: '', tipo: 'Carrocería' }),
  marca: null,
  linea: null,
  modeloAnio: null,
  kilometraje: null,
}
const SIN_MARCA_CON_TITULO = {
  ...tarjeta({ slug: 'furgon-aluminio', titulo: 'Furgón en aluminio', tipo: 'Carrocería' }),
  marca: null,
  linea: null,
  modeloAnio: null,
  kilometraje: null,
}
const RESERVADO = tarjeta({ slug: 'npr-reservado', titulo: 'Chevrolet NPR 2021', estadoComercial: 'RESERVADO' })

describe('Catálogo con marca/año null (P4a)', () => {
  it('las tarjetas sin marca ni año se muestran; sin título se usa la categoría; nunca "null"', async () => {
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: {
        status: 200,
        body: { destacados: [], recientes: [SIN_MARCA_SIN_TITULO, SIN_MARCA_CON_TITULO, RESERVADO] },
      },
    })
    abrirApp('#/')
    expect(await screen.findByRole('heading', { level: 3, name: 'Carrocería' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 3, name: 'Furgón en aluminio' })).toBeInTheDocument()
    for (const art of screen.getAllByRole('article')) {
      expect(art.textContent ?? '').not.toMatch(/null|undefined|NaN/)
    }
    const reservado = screen.getByRole('heading', { level: 3, name: 'Chevrolet NPR 2021' }).closest('article') as HTMLElement
    expect(within(reservado).getByText('Reservado')).toBeInTheDocument()
  })

  it('el detalle sin marca ni año no muestra esas filas ni "null"', async () => {
    const d = detalle(SIN_MARCA_CON_TITULO, { carroceria: 'Furgón' })
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [rutaDetalle(d.slug)]: { status: 200, body: d } })
    abrirApp(`#/vehiculo/${d.slug}`)
    expect(await screen.findByRole('heading', { level: 1, name: 'Furgón en aluminio' })).toBeInTheDocument()
    const dl = screen.getByRole('heading', { name: 'Ficha técnica' }).parentElement?.querySelector('dl') as HTMLElement
    const nombres = Array.from(dl.querySelectorAll('dt')).map((dt) => dt.textContent)
    expect(nombres).not.toContain('Marca')
    expect(nombres).not.toContain('Año')
    expect(nombres).toContain('Carrocería')
    expect(document.body.textContent ?? '').not.toMatch(/\bnull\b|undefined/)
  })
})
