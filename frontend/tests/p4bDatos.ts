// P4b — datos ficticios de publicaciones para las pruebas (no son vehículos ni personas reales).
import type { RespuestaSimulada } from './helpers'

export const RUTA_PUBLICACIONES = '/api/publicaciones'
export const RUTA_CUPO = '/api/publicaciones/cupo'
/** Listado que reúne el inventario para saber la publicación de cada vehículo. */
export const RUTA_MAPA = '/api/publicaciones?pagina=1&tamano=100'
/** Primera página de "Mis publicaciones". */
export const RUTA_LISTA_PUB = '/api/publicaciones?pagina=1&tamano=20'

export function rutaListaPub(consulta = ''): string {
  return `${RUTA_PUBLICACIONES}?${consulta === '' ? '' : `${consulta}&`}pagina=1&tamano=20`
}

export function rutaPub(id: number, accion?: string): string {
  return accion ? `${RUTA_PUBLICACIONES}/${id}/${accion}` : `${RUTA_PUBLICACIONES}/${id}`
}

export function pub(id: number, vehiculoId: number, extra: Record<string, unknown> = {}) {
  return {
    id,
    vehiculoId,
    tituloVehiculo: 'Chevrolet NPR 2020',
    titulo: `Camión de trabajo ${id}`,
    estado: 'PUBLICADA',
    estadoEfectivo: 'PUBLICADA',
    destacada: false,
    vigenteHasta: '2026-12-30T12:00:00Z',
    slugPublico: `chevrolet-npr-2020-${vehiculoId}`,
    fotoPortada: null,
    precioCop: 120000000,
    motivoOcultamiento: null,
    publicadaEn: '2026-10-01T12:00:00Z',
    ...extra,
  }
}

export function paginaPub(items: unknown[], tamano = 20): RespuestaSimulada {
  return { status: 200, body: { items, total: items.length, pagina: 1, tamano } }
}

export function cupo(extra: Record<string, unknown> = {}): RespuestaSimulada {
  return {
    status: 200,
    body: {
      limitePublicaciones: 5,
      usadas: 3,
      limiteDestacados: 2,
      destacadasUsadas: 1,
      planVigente: true,
      finPlan: '2026-12-30T12:00:00Z',
      ...extra,
    },
  }
}

export function cuerpo(init: RequestInit | undefined): unknown {
  return init?.body === undefined ? undefined : JSON.parse(String(init.body))
}
