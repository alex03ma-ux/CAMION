// Rutas por hash (P3: CA-001-06, CA-UI-01, CA-UI-02, CA-004-17, CA-012-02).
import { describe, expect, it } from 'vitest'
import { FILTROS_VACIOS } from '../src/api/catalogo'
import { hashBuscar, hashVehiculo, leerFiltros, leerRuta } from '../src/rutas'

describe('leerRuta', () => {
  it('CA-001-06 sin hash, "#" y "#/" abren el inicio', () => {
    expect(leerRuta('')).toEqual({ nombre: 'inicio' })
    expect(leerRuta('#')).toEqual({ nombre: 'inicio' })
    expect(leerRuta('#/')).toEqual({ nombre: 'inicio' })
  })

  it('CA-UI-01 buscar, vehículo y cuenta', () => {
    expect(leerRuta('#/buscar')).toEqual({ nombre: 'buscar', filtros: FILTROS_VACIOS })
    expect(leerRuta('#/buscar/')).toEqual({ nombre: 'buscar', filtros: FILTROS_VACIOS })
    expect(leerRuta('#/vehiculo/chevrolet-npr-2020')).toEqual({
      nombre: 'vehiculo',
      slug: 'chevrolet-npr-2020',
    })
    expect(leerRuta('#/cuenta')).toEqual({ nombre: 'cuenta', registro: false })
    expect(leerRuta('#/cuenta/registro')).toEqual({ nombre: 'cuenta', registro: true })
  })

  it('CA-UI-02 rutas desconocidas o mal codificadas abren el inicio', () => {
    expect(leerRuta('#/xyz')).toEqual({ nombre: 'inicio' })
    expect(leerRuta('#/vehiculo/')).toEqual({ nombre: 'inicio' })
    expect(leerRuta('#/vehiculo/a/b')).toEqual({ nombre: 'inicio' })
    expect(leerRuta('#/vehiculo/%E0%A4%A')).toEqual({ nombre: 'inicio' })
    expect(leerRuta('#contenido')).toEqual({ nombre: 'inicio' })
  })

  it('CA-004-17 lee los filtros conocidos de la URL e ignora los demás', () => {
    const ruta = leerRuta(
      '#/buscar?marcaId=10&precioMax=100000000&orden=precio_asc&otro=1&q=%20npr%20',
    )
    expect(ruta).toEqual({
      nombre: 'buscar',
      filtros: {
        ...FILTROS_VACIOS,
        marcaId: '10',
        precioMax: '100000000',
        orden: 'precio_asc',
        q: 'npr',
      },
    })
    expect(leerFiltros('q=cami%C3%B3n+blanco').q).toBe('camión blanco')
  })
})

describe('hashBuscar / hashVehiculo', () => {
  it('CA-012-02 sin filtros es "#/buscar" sin parámetros', () => {
    expect(hashBuscar()).toBe('#/buscar')
    expect(hashBuscar(FILTROS_VACIOS)).toBe('#/buscar')
  })

  it('omite vacíos y pagina=1, en el orden de los filtros', () => {
    expect(hashBuscar({ pagina: '1', marcaId: '10', q: ' npr ', tipoId: '' })).toBe(
      '#/buscar?q=npr&marcaId=10',
    )
    expect(hashBuscar({ orden: 'precio_asc', pagina: '2' })).toBe(
      '#/buscar?orden=precio_asc&pagina=2',
    )
  })

  it('ida y vuelta: construir y leer da los mismos filtros', () => {
    const filtros = {
      ...FILTROS_VACIOS,
      q: 'npr',
      tipoId: '2',
      marcaId: '10',
      lineaId: '101',
      precioMin: '50000000',
      precioMax: '120000000',
      anioMin: '2015',
      anioMax: '2022',
      kmMax: '85000',
      orden: 'precio_desc',
      pagina: '2',
    }
    expect(leerRuta(hashBuscar(filtros))).toEqual({ nombre: 'buscar', filtros })
  })

  it('el slug se codifica en el hash del detalle', () => {
    expect(hashVehiculo('kenworth-t800-2018')).toBe('#/vehiculo/kenworth-t800-2018')
    expect(leerRuta(hashVehiculo('camión ñ'))).toEqual({ nombre: 'vehiculo', slug: 'camión ñ' })
  })
})
