// Estados del vehículo y sus acciones (contrato P4a, D-4, D-11, D-22). Módulo puro.
import type { AccionEstado, EstadoVehiculo } from '../api/inventario'

export const NOMBRES_ESTADO: Readonly<Record<EstadoVehiculo, string>> = {
  ACTIVO: 'Activo',
  RESERVADO: 'Reservado',
  INACTIVO: 'Inactivo',
  VENDIDO: 'Vendido',
}

/** Estado con texto (no solo color). */
export function nombreEstado(estado: string): string {
  const clave = estado.toUpperCase()
  return clave in NOMBRES_ESTADO ? NOMBRES_ESTADO[clave as EstadoVehiculo] : estado || 'Desconocido'
}

/**
 * Transiciones válidas del contrato. Un VENDIDO no tiene acciones (marcarlo
 * vendido espera a HU-064, D-22); un estado desconocido tampoco.
 */
export function accionesEstado(estado: string): AccionEstado[] {
  switch (estado.toUpperCase()) {
    case 'ACTIVO':
      return ['reservar', 'inactivar']
    case 'RESERVADO':
      return ['liberar', 'inactivar']
    case 'INACTIVO':
      return ['reactivar']
    default:
      return []
  }
}

export function esEditable(estado: string): boolean {
  return estado.toUpperCase() !== 'VENDIDO'
}

export const TEXTOS_ACCION_ESTADO: Readonly<
  Record<AccionEstado, { boton: string; enviando: string; exito: (titulo: string) => string }>
> = {
  reservar: {
    boton: 'Reservar',
    enviando: 'Reservando...',
    exito: (t) => `${t} quedó reservado.`,
  },
  liberar: {
    boton: 'Liberar',
    enviando: 'Liberando...',
    exito: (t) => `${t} volvió a estar activo.`,
  },
  inactivar: {
    boton: 'Inactivar',
    enviando: 'Inactivando...',
    exito: (t) => `${t} quedó inactivo.`,
  },
  reactivar: {
    boton: 'Reactivar',
    enviando: 'Reactivando...',
    exito: (t) => `${t} volvió a estar activo.`,
  },
}

export const MOTIVO_INACTIVAR_MIN = 5
export const MOTIVO_INACTIVAR_MAX = 500
export const MENSAJE_MOTIVO_INACTIVAR = 'Escribe el motivo (5 a 500 caracteres).'

export function validarMotivoInactivar(motivo: string): string[] {
  const largo = motivo.trim().length
  return largo >= MOTIVO_INACTIVAR_MIN && largo <= MOTIVO_INACTIVAR_MAX
    ? []
    : [MENSAJE_MOTIVO_INACTIVAR]
}

/** "Chevrolet NPR 2020", o la categoría si no hay marca (p. ej. una carrocería). */
export function tituloVehiculo(v: {
  tipo: string
  marca: string | null
  linea: string | null
  modeloAnio: number | null
}): string {
  const partes = [v.marca, v.linea, v.modeloAnio !== null ? String(v.modeloAnio) : null].filter(
    (p): p is string => !!p && p.trim() !== '',
  )
  if (partes.length === 0) return v.tipo || 'Vehículo sin título'
  if (!v.marca) return [v.tipo, ...partes].filter(Boolean).join(' ')
  return partes.join(' ')
}
