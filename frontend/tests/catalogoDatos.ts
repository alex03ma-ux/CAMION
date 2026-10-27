// Datos ficticios del catálogo (P3) para las pruebas de interfaz.
// No son vehículos ni personas reales.
import { render } from '@testing-library/react'
import { createElement } from 'react'
import App from '../src/App'
import type { DetalleVehiculo, TarjetaVehiculo } from '../src/api/catalogo'
import { olvidarUltimaBusqueda } from '../src/rutas'
import { RUTA_FILTROS, type RespuestaSimulada } from './helpers'

export const RUTA_INICIO = '/api/catalogo/inicio'

/** URL exacta que pide la app para una búsqueda (`consulta` sin `tamano`). */
export function rutaPublicaciones(consulta = ''): string {
  return `/api/catalogo/publicaciones?${consulta === '' ? '' : `${consulta}&`}tamano=12`
}

export function rutaDetalle(slug: string): string {
  return `/api/catalogo/publicaciones/${encodeURIComponent(slug)}`
}

export const TIPOS = [
  { id: 1, nombre: 'Carro', total: 3 },
  { id: 2, nombre: 'Camión', total: 5 },
  { id: 3, nombre: 'Carrocería', total: 2 },
  { id: 4, nombre: 'Maquinaria', total: 2 },
]

export const MARCAS = [
  {
    id: 10,
    nombre: 'Chevrolet',
    total: 4,
    lineas: [
      { id: 100, nombre: 'NHR', total: 1 },
      { id: 101, nombre: 'NPR', total: 3 },
    ],
  },
  { id: 11, nombre: 'Kenworth', total: 2, lineas: [{ id: 110, nombre: 'T800', total: 2 }] },
  { id: 12, nombre: 'Renault', total: 1, lineas: [{ id: 120, nombre: 'Kerax', total: 1 }] },
]

export function respuestaFiltros(tipos = TIPOS): RespuestaSimulada {
  return {
    status: 200,
    body: {
      tipos,
      marcas: MARCAS,
      rangos: { precioMin: 30000000, precioMax: 400000000, anioMin: 2010, anioMax: 2024, kmMax: 300000 },
    },
  }
}

export function tarjeta(
  datos: Partial<TarjetaVehiculo> & Pick<TarjetaVehiculo, 'slug' | 'titulo'>,
): TarjetaVehiculo {
  return {
    precioCop: 100000000,
    negociable: false,
    estadoComercial: 'DISPONIBLE',
    destacado: false,
    tipo: 'Camión',
    marca: 'Chevrolet',
    linea: 'NPR',
    modeloAnio: 2020,
    kilometraje: 50000,
    municipio: 'Bogotá',
    departamento: 'Cundinamarca',
    fotoPortada: null,
    publicadaEn: '2026-10-01T12:00:00Z',
    ...datos,
  }
}

export const V1 = tarjeta({
  slug: 'chevrolet-npr-2020',
  titulo: 'Chevrolet NPR 2020',
  precioCop: 120000000,
  destacado: true,
  modeloAnio: 2020,
  kilometraje: 85000,
  municipio: 'Bogotá',
  fotoPortada: '/fotos/ficticia-v1.jpg',
})
export const V2 = tarjeta({
  slug: 'kenworth-t800-2018',
  titulo: 'Kenworth T800 2018',
  precioCop: 250000000,
  negociable: true,
  estadoComercial: 'RESERVADO',
  marca: 'Kenworth',
  linea: 'T800',
  modeloAnio: 2018,
  kilometraje: 300000,
  municipio: 'Medellín',
  departamento: 'Antioquia',
})
export const V3 = tarjeta({
  slug: 'renault-kerax-2015',
  titulo: 'Renault Kerax 2015',
  marca: 'Renault',
  linea: 'Kerax',
  modeloAnio: 2015,
  kilometraje: 150000,
  municipio: 'Cali',
  departamento: 'Valle del Cauca',
})
export const V4 = tarjeta({
  slug: 'chevrolet-nhr-2022',
  titulo: 'Chevrolet NHR 2022',
  linea: 'NHR',
  modeloAnio: 2022,
  kilometraje: 20000,
})

/** Carrocerías y máquinas de D-17. */
export const C1 = tarjeta({ slug: 'furgon-c1', titulo: 'Furgón seco C1', tipo: 'Carrocería', kilometraje: 0, linea: null, modeloAnio: null })
export const C2 = tarjeta({ slug: 'estacas-c2', titulo: 'Estacas C2', tipo: 'Carrocería', kilometraje: 500, linea: null })
export const M1 = tarjeta({ slug: 'retro-m1', titulo: 'Retroexcavadora M1', tipo: 'Maquinaria', kilometraje: 0 })
export const M2 = tarjeta({ slug: 'mini-m2', titulo: 'Minicargador M2', tipo: 'Maquinaria', kilometraje: 1200 })
export const K0 = tarjeta({ slug: 'camion-cero-km', titulo: 'Camión nuevo K0', tipo: 'Camión', kilometraje: 0 })

export function detalle(base: TarjetaVehiculo, extra: Partial<DetalleVehiculo> = {}): DetalleVehiculo {
  return {
    ...base,
    descripcionComercial: null,
    descripcionTecnica: null,
    carroceria: null,
    combustible: null,
    capacidadCargaKg: null,
    pesoBrutoKg: null,
    cilindrajeCc: null,
    fotos: [],
    ...extra,
  }
}

export function pagina(items: TarjetaVehiculo[], total = items.length, numero = 1): RespuestaSimulada {
  return { status: 200, body: { items, total, pagina: numero, tamano: 12 } }
}

/** Abre la app en `hash` (sin sesión salvo que la prueba la guarde antes). */
export function abrirApp(hash: string) {
  olvidarUltimaBusqueda()
  window.location.hash = hash
  return render(createElement(App))
}

export { RUTA_FILTROS }
