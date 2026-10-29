// Estados de la publicación y sus acciones válidas (contrato P4b, D-38, D-40, D-41). Módulo puro.
import type { CupoPublicaciones, Publicacion } from '../api/publicaciones'
import { formatoMiles } from '../catalogo/formato'

/** Estado con texto (no solo color). OCULTA se muestra como "Inactiva" (HU-025). */
const NOMBRES: Readonly<Record<string, string>> = {
  PUBLICADA: 'Publicada',
  OCULTA: 'Inactiva',
  VENCIDA: 'Vencida',
  VENDIDA: 'Vendida',
  ELIMINADA: 'Eliminada',
}

export function nombreEstadoPublicacion(estado: string): string {
  return NOMBRES[estado.toUpperCase()] ?? (estado || 'Desconocido')
}

/** Estado que se muestra: el efectivo (añade VENCIDA) o, si no viene, el guardado. */
export function estadoVisible(p: Pick<Publicacion, 'estado' | 'estadoEfectivo'>): string {
  return (p.estadoEfectivo || p.estado).toUpperCase()
}

export type AccionVisible =
  | 'editar'
  | 'inactivar'
  | 'reactivar'
  | 'renovar'
  | 'destacar'
  | 'quitar-destacado'
  | 'eliminar'
  | 'ver-portal'

/**
 * Acciones válidas por estado:
 * - Publicada (vigente): editar, inactivar, renovar, destacar o quitar destacado, ver en el portal.
 * - Vencida (PUBLICADA con el plan vencido): editar, inactivar y renovar.
 * - Inactiva (OCULTA): editar, reactivar y eliminar (libera cupo, D-40).
 * - Vendida: editar y eliminar.
 */
export function accionesPublicacion(p: Pick<Publicacion, 'estado' | 'estadoEfectivo' | 'destacada' | 'slugPublico'>): AccionVisible[] {
  switch (estadoVisible(p)) {
    case 'PUBLICADA': {
      const acciones: AccionVisible[] = ['editar', 'inactivar', 'renovar', p.destacada ? 'quitar-destacado' : 'destacar']
      if (p.slugPublico) acciones.push('ver-portal')
      return acciones
    }
    case 'VENCIDA':
      return ['editar', 'inactivar', 'renovar']
    case 'OCULTA':
      return ['editar', 'reactivar', 'eliminar']
    case 'VENDIDA':
      return ['editar', 'eliminar']
    default:
      return []
  }
}

export const TEXTOS_ACCION_PUBLICACION: Readonly<
  Record<'reactivar' | 'renovar' | 'destacar' | 'quitar-destacado' | 'eliminar' | 'inactivar', { boton: string; enviando: string; exito: (t: string) => string }>
> = {
  inactivar: { boton: 'Inactivar', enviando: 'Inactivando...', exito: (t) => `La publicación «${t}» quedó inactiva.` },
  reactivar: { boton: 'Reactivar', enviando: 'Reactivando...', exito: (t) => `La publicación «${t}» volvió a publicarse.` },
  renovar: { boton: 'Renovar', enviando: 'Renovando...', exito: (t) => `La publicación «${t}» se renovó hasta el fin de tu plan.` },
  destacar: { boton: 'Destacar', enviando: 'Destacando...', exito: (t) => `La publicación «${t}» ahora es destacada.` },
  'quitar-destacado': {
    boton: 'Quitar destacado',
    enviando: 'Quitando destacado...',
    exito: (t) => `La publicación «${t}» ya no es destacada.`,
  },
  eliminar: {
    boton: 'Eliminar',
    enviando: 'Eliminando...',
    exito: (t) => `Se eliminó la publicación «${t}» y se liberó un cupo de tu plan.`,
  },
}

/** "Publicaciones: 3 de 5 · Destacados: 1 de 2". */
export function textoContadorCupo(c: CupoPublicaciones): string {
  return `Publicaciones: ${formatoMiles(c.usadas)} de ${formatoMiles(c.limitePublicaciones)} · Destacados: ${formatoMiles(
    c.destacadasUsadas,
  )} de ${formatoMiles(c.limiteDestacados)}`
}

export const MENSAJE_SIN_PLAN = 'No tienes un plan activo. Comunícate con el administrador.'

export function mensajeSinCupo(limite: number): string {
  return `Ya usas las ${formatoMiles(limite)} publicaciones de tu plan. Elimina una para crear otra.`
}

export function mensajeSinDestacados(limite: number): string {
  return `Tu plan permite ${formatoMiles(limite)} destacado(s). Quita uno para destacar otra.`
}

/** Avisos del contador: sin plan, sin cupo o sin destacados disponibles. */
export function avisosCupo(c: CupoPublicaciones): string[] {
  if (!c.planVigente) return [MENSAJE_SIN_PLAN]
  const avisos: string[] = []
  if (c.usadas >= c.limitePublicaciones) avisos.push(mensajeSinCupo(c.limitePublicaciones))
  if (c.limiteDestacados === 0) avisos.push('Tu plan no incluye destacados.')
  else if (c.destacadasUsadas >= c.limiteDestacados) avisos.push(mensajeSinDestacados(c.limiteDestacados))
  return avisos
}
