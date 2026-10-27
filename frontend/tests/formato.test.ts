// Formatos del catálogo, regla del kilometraje y validación de filtros
// (P3: CA-UI-05, CA-009-06, CA-011-03, CA-D17-05 a 08, CA-004-18, mensajes del contrato).
import { describe, expect, it } from 'vitest'
import {
  categoriasDisponibles,
  FILTROS_VACIOS,
  mostrarKilometraje,
  tipoPorNombre,
  validarFiltros,
  type FiltrosCatalogo,
} from '../src/api/catalogo'
import {
  formatoCc,
  formatoCop,
  formatoKg,
  formatoKm,
  mostrarConMiles,
  soloDigitos,
  textoTotal,
  textoUbicacion,
} from '../src/catalogo/formato'
import { TIPOS } from './catalogoDatos'

describe('Formato COP y unidades (es-CO)', () => {
  it('CA-UI-05 precio "$ 120.000.000" sin decimales', () => {
    expect(formatoCop(120000000)).toBe('$ 120.000.000')
    expect(formatoCop(250000000)).toBe('$ 250.000.000')
    expect(formatoCop(0)).toBe('$ 0')
    expect(formatoCop(1999.6)).toBe('$ 2.000')
  })

  it('CA-UI-05 / CA-D17-08 kilometraje con miles y "km" (también 4 cifras y 0)', () => {
    expect(formatoKm(85000)).toBe('85.000 km')
    expect(formatoKm(1200)).toBe('1.200 km')
    expect(formatoKm(0)).toBe('0 km')
  })

  it('kg y cc con separador de miles', () => {
    expect(formatoKg(3500)).toBe('3.500 kg')
    expect(formatoCc(4500)).toBe('4.500 cc')
  })

  it('CA-009-06 / CA-011-03 el campo muestra miles y guarda solo dígitos', () => {
    expect(soloDigitos('120.000.000')).toBe('120000000')
    expect(mostrarConMiles('120000000')).toBe('120.000.000')
    expect(mostrarConMiles('85000')).toBe('85.000')
    expect(mostrarConMiles('')).toBe('')
    // Un valor no numérico (p. ej. desde la URL) se deja igual para que la validación lo marque.
    expect(mostrarConMiles('-5')).toBe('-5')
  })

  it('CA-004-18 contador en singular y plural', () => {
    expect(textoTotal(25)).toBe('25 vehículos encontrados')
    expect(textoTotal(1)).toBe('1 vehículo encontrado')
    expect(textoTotal(0)).toBe('0 vehículos encontrados')
    expect(textoTotal(1500)).toBe('1.500 vehículos encontrados')
  })

  it('ubicación con lo que haya', () => {
    expect(textoUbicacion('Bogotá', 'Cundinamarca')).toBe('Bogotá, Cundinamarca')
    expect(textoUbicacion(null, 'Antioquia')).toBe('Antioquia')
    expect(textoUbicacion(null, null)).toBe('')
  })
})

describe('mostrarKilometraje (D-17)', () => {
  it('CA-D17-06 Carrocería nunca muestra kilometraje (0 ni 500)', () => {
    expect(mostrarKilometraje('Carrocería', 0)).toBe(false)
    expect(mostrarKilometraje('Carrocería', 500)).toBe(false)
    expect(mostrarKilometraje('carroceria', 500)).toBe(false)
  })

  it('CA-D17-07 / CA-D17-08 Maquinaria con 0 no, con 1200 sí', () => {
    expect(mostrarKilometraje('Maquinaria', 0)).toBe(false)
    expect(mostrarKilometraje('Maquinaria', 1200)).toBe(true)
  })

  it('CA-D17-08 / CA-UI-05 Carro y Camión siempre, también con 0', () => {
    expect(mostrarKilometraje('Carro', 0)).toBe(true)
    expect(mostrarKilometraje('Camión', 0)).toBe(true)
    expect(mostrarKilometraje('CAMION', 85000)).toBe(true)
  })

  it('sin dato de kilometraje no se muestra', () => {
    expect(mostrarKilometraje('Camión', null)).toBe(false)
  })
})

describe('Categorías por nombre (D-17, CA-D17-05)', () => {
  it('encuentra los tipos sin distinguir tildes ni mayúsculas y omite los que faltan', () => {
    expect(tipoPorNombre([{ id: 9, nombre: 'CAMION', total: 1 }], 'Camión')?.id).toBe(9)
    expect(categoriasDisponibles(TIPOS).map((c) => c.enlace)).toEqual([
      'Carros',
      'Camiones',
      'Carrocerías',
      'Maquinaria',
    ])
    const sinMaquinaria = TIPOS.filter((t) => t.nombre !== 'Maquinaria')
    expect(categoriasDisponibles(sinMaquinaria).map((c) => c.enlace)).toEqual([
      'Carros',
      'Camiones',
      'Carrocerías',
    ])
  })
})

describe('validarFiltros (mensajes del contrato)', () => {
  const f = (cambios: Partial<FiltrosCatalogo>): FiltrosCatalogo => ({ ...FILTROS_VACIOS, ...cambios })

  it('sin filtros no hay errores', () => {
    expect(validarFiltros(FILTROS_VACIOS)).toEqual({})
  })

  it('cada parámetro inválido lleva su mensaje', () => {
    expect(validarFiltros(f({ q: 'x'.repeat(101) })).q).toEqual([
      'La búsqueda no puede tener más de 100 caracteres.',
    ])
    expect(validarFiltros(f({ q: '  ' + 'x'.repeat(100) + '  ' })).q).toBeUndefined()
    expect(validarFiltros(f({ precioMin: '-5' })).precioMin).toEqual([
      'Escribe un precio válido en pesos (número entero mayor o igual a 0).',
    ])
    expect(validarFiltros(f({ precioMin: '200', precioMax: '100' })).precioMax).toEqual([
      'El precio máximo debe ser mayor o igual al mínimo.',
    ])
    expect(validarFiltros(f({ anioMin: '1899' })).anioMin).toEqual(['Escribe un año entre 1900 y 2200.'])
    expect(validarFiltros(f({ anioMax: '2201' })).anioMax).toEqual(['Escribe un año entre 1900 y 2200.'])
    expect(validarFiltros(f({ anioMin: '2020', anioMax: '2019' })).anioMax).toEqual([
      'El año final debe ser mayor o igual al inicial.',
    ])
    expect(validarFiltros(f({ kmMax: 'abc' })).kmMax).toEqual([
      'Escribe un kilometraje válido (número entero mayor o igual a 0).',
    ])
    expect(validarFiltros(f({ orden: 'barato' })).orden).toEqual(['Orden no válido.'])
    expect(validarFiltros(f({ pagina: '0' })).pagina).toEqual(['Página o tamaño no válidos.'])
  })

  it('límites válidos: 0, 1900, 2200 y rangos iguales', () => {
    expect(
      validarFiltros(f({ precioMin: '0', precioMax: '0', anioMin: '1900', anioMax: '2200', kmMax: '0' })),
    ).toEqual({})
    expect(validarFiltros(f({ anioMin: '2020', anioMax: '2020' }))).toEqual({})
  })

  it('el modelo debe pertenecer a la marca elegida (con opciones cargadas)', () => {
    const opciones = {
      tipos: TIPOS,
      marcas: [{ id: 10, nombre: 'Chevrolet', total: 1, lineas: [{ id: 100, nombre: 'NHR', total: 1 }] }],
      rangos: { precioMin: null, precioMax: null, anioMin: null, anioMax: null, kmMax: null },
    }
    expect(validarFiltros(f({ marcaId: '10', lineaId: '110' }), opciones).lineaId).toEqual([
      'El modelo no corresponde a la marca elegida.',
    ])
    expect(validarFiltros(f({ marcaId: '10', lineaId: '100' }), opciones)).toEqual({})
  })
})
