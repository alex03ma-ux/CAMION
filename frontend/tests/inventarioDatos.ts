// Datos ficticios del inventario (P4a) para las pruebas de interfaz.
// No son vehículos, asesores ni personas reales.
import { render } from '@testing-library/react'
import { createElement } from 'react'
import App from '../src/App'
import { descartarAvisoPendiente } from '../src/inventario/avisoPendiente'
import { olvidarFiltrosInventario } from '../src/components/InventarioLista'
import { guardarSesionFicticia, type RespuestaSimulada } from './helpers'

export const ME = '/api/auth/me'
export const CATALOGOS = '/api/inventario/catalogos'
export const VEHICULOS = '/api/inventario/vehiculos'

/** URL exacta de la lista (los filtros van antes de pagina y tamano). */
export function rutaLista(consulta = '', pagina = 1): string {
  return `${VEHICULOS}?${consulta === '' ? '' : `${consulta}&`}pagina=${pagina}&tamano=20`
}

export const LISTA = rutaLista()

export const CAT = {
  tipos: [
    { id: 1, nombre: 'Carro' },
    { id: 2, nombre: 'Camión' },
    { id: 3, nombre: 'Carrocería' },
    { id: 4, nombre: 'Maquinaria' },
  ],
  marcas: [
    {
      id: 10,
      nombre: 'Chevrolet',
      lineas: [
        { id: 100, nombre: 'NPR' },
        { id: 101, nombre: 'NHR' },
      ],
    },
    { id: 11, nombre: 'Kenworth', lineas: [{ id: 110, nombre: 'T800' }] },
  ],
  carrocerias: [{ id: 5, nombre: 'Furgón' }],
  materiales: [{ id: 6, nombre: 'Aluminio' }],
  tiposMaquina: [{ id: 7, nombre: 'Retroexcavadora' }],
  combustibles: [{ id: 8, nombre: 'Diésel' }],
  departamentos: [
    { id: 20, nombre: 'Antioquia', municipios: [{ id: 200, nombre: 'Medellín' }] },
    { id: 21, nombre: 'Cundinamarca', municipios: [{ id: 210, nombre: 'Bogotá' }] },
  ],
}

export const ASESORES = [
  { id: 5, nombre: 'Asesor Uno Ficticio' },
  { id: 6, nombre: 'Asesor Dos Ficticio' },
]

export const CAT_ADMIN = { ...CAT, asesores: ASESORES }

export function me(roles: string[]): RespuestaSimulada {
  return {
    status: 200,
    body: {
      usuarioId: 'u-inv-1',
      nombreCompleto: 'Usuaria Ficticia',
      email: 'usuaria.ficticia@ejemplo.test',
      phoneNumber: '',
      roles,
      estado: 'ACTIVA',
    },
  }
}

export type ItemPrueba = {
  id: number
  tipo: string
  marca: string | null
  linea: string | null
  modeloAnio: number | null
  placa: string | null
  precioCop: number
  estado: string
  kilometraje: number | null
  horasUso: number | null
  municipio: string | null
  fotoPortada: string | null
  totalFotos: number
  actualizadoEn: string
  notaReserva?: string | null
  asesor?: { id: number; nombre: string }
}

export function item(datos: Partial<ItemPrueba> & Pick<ItemPrueba, 'id'>): ItemPrueba {
  return {
    tipo: 'Camión',
    marca: 'Chevrolet',
    linea: 'NPR',
    modeloAnio: 2020,
    placa: 'ABC123',
    precioCop: 120000000,
    estado: 'ACTIVO',
    kilometraje: 85000,
    horasUso: null,
    municipio: 'Medellín',
    fotoPortada: null,
    totalFotos: 0,
    actualizadoEn: '2026-10-27T10:00:00Z',
    ...datos,
  }
}

export function lista(items: ItemPrueba[], total = items.length, pagina = 1): RespuestaSimulada {
  return { status: 200, body: { items, total, pagina, tamano: 20 } }
}

export const VACIA = lista([])

/** Foto ficticia del vehículo 9 tal como la devuelve la API. */
export function fotoDetalle(id: number, orden: number) {
  return { id, url: `/media/vehiculos/9/f${id}.jpg`, orden, esPortada: orden === 1 }
}

/**
 * Detalle de un Camión (V1 ficticio) con precio anterior y una foto (D-30: todo
 * vehículo tiene al menos una). Para un vehículo sin fotos, pasa `{ fotos: [] }`.
 */
export function detalleCamion(extra: Record<string, unknown> = {}) {
  return {
    id: 9,
    tipoId: 2,
    tipo: 'Camión',
    marcaId: 10,
    lineaId: 100,
    modeloAnio: 2020,
    kilometraje: 85000,
    horasUso: null,
    placa: 'ABC123',
    carroceriaId: null,
    combustibleId: 8,
    cilindrajeCc: null,
    pesoBrutoKg: null,
    capacidadCargaKg: null,
    largoM: null,
    anchoM: null,
    altoM: null,
    materialId: null,
    tipoMaquinaId: null,
    municipioId: 200,
    precioCop: 115000000,
    precioAnteriorCop: 120000000,
    descripcionTecnica: null,
    estado: 'ACTIVO',
    motivoInactivacion: null,
    fotos: [fotoDetalle(1, 1)],
    ...extra,
  }
}

/** Abre la app en `hash` con una sesión ficticia vigente (el rol real lo da /me). */
export function abrirConSesion(hash: string) {
  olvidarFiltrosInventario()
  descartarAvisoPendiente()
  guardarSesionFicticia(Date.now() + 60 * 60 * 1000)
  window.location.hash = hash
  return render(createElement(App))
}

/** Abre la app sin sesión. */
export function abrirSinSesion(hash: string) {
  olvidarFiltrosInventario()
  descartarAvisoPendiente()
  window.location.hash = hash
  return render(createElement(App))
}

export function cuerpoJson(init: RequestInit | undefined): unknown {
  return JSON.parse(String(init?.body))
}

// ---- D-30: registrar con fotos (multipart) ----

/** Archivo PNG ficticio (bytes en cero; la validación real por firma es de la API). */
export function png(nombre: string, bytes = 10): File {
  return new File([new Uint8Array(bytes)], nombre, { type: 'image/png' })
}

/** El POST de registro es multipart: devuelve el JSON del campo `datos`. */
export function datosCreacion(init: RequestInit | undefined): Record<string, unknown> {
  const cuerpo = init?.body
  if (!(cuerpo instanceof FormData)) throw new Error('El registro debe enviarse como FormData.')
  const datos = cuerpo.get('datos')
  if (typeof datos !== 'string') throw new Error('Falta el campo "datos" como texto JSON.')
  return JSON.parse(datos) as Record<string, unknown>
}

/** Nombres de los archivos del campo `fotos`, en el orden enviado. */
export function fotosCreacion(init: RequestInit | undefined): string[] {
  const cuerpo = init?.body
  if (!(cuerpo instanceof FormData)) throw new Error('El registro debe enviarse como FormData.')
  return cuerpo.getAll('fotos').map((f) => (f instanceof File ? f.name : String(f)))
}
