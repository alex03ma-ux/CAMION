// Suscripciones de los asesores (contrato docs/criterios/P4p-contrato-api.md).
// HU-066 consultar mi suscripción, HU-067 renovar, HU-068 suspender, HU-069 reactivar;
// RN-SU-001 a RN-SU-005; D-4, D-26, D-35, D-36.
// Las rutas /api/admin/* exigen ADMINISTRADOR y /api/asesor/suscripcion exige ASESOR en la API.
import { MOTIVO_MAX, MOTIVO_MIN } from './admin'
import { MENSAJE_SESION_TERMINADA } from './auth'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion } from './http'

// ---- Tipos del contrato ----

export type EstadoSuscripcion =
  | 'PENDIENTE'
  | 'ACTIVA'
  | 'SUSPENDIDA'
  | 'REACTIVADA'
  | 'VENCIDA'
  | 'CANCELADA'
  | 'RECHAZADA'

export type ResumenAsesorSuscripcion = { id: string; nombre: string }
export type ResumenPlanSuscripcion = {
  id: number | null
  nombre: string
  limitePublicaciones: number | null
  diasVigencia: number | null
}

export type Suscripcion = {
  id: number
  asesor: ResumenAsesorSuscripcion
  plan: ResumenPlanSuscripcion
  /** Estado guardado en la base de datos. */
  estado: string
  /** Estado calculado al leer (VENCIDA si la fecha de fin ya pasó). */
  estadoEfectivo: string
  inicioVigencia: string | null
  finVigencia: string | null
  diasRestantes: number | null
}

export type ListaSuscripciones = {
  items: Suscripcion[]
  total: number
  pagina: number
  tamano: number
}

export type FiltrosSuscripciones = {
  /** `perfiles_asesor.id`; vacío = todos. */
  asesorId: string
  /** Vacío = todos los estados. */
  estado: string
  pagina: number
  tamano: number
}

/** Respuesta de GET /api/asesor/suscripcion. */
export type MiSuscripcion =
  | {
      tieneSuscripcion: true
      plan: ResumenPlanSuscripcion
      estado: string
      estadoEfectivo: string
      inicioVigencia: string | null
      finVigencia: string | null
      diasRestantes: number | null
      vigente: boolean
    }
  | { tieneSuscripcion: false; mensaje: string }

export type AccionSuscripcion = 'renovar' | 'cambiar-plan' | 'suspender' | 'reactivar' | 'cancelar'

export const TAMANO_PAGINA_SUSCRIPCIONES = 20

export const MENSAJE_SIN_PLAN = 'Aún no tienes un plan activo. Comunícate con el administrador.'
export const MENSAJE_SUSCRIPCION_NO_ENCONTRADA = 'No encontrado.'
export const MENSAJE_MOTIVO_SUSCRIPCION = 'Escribe el motivo (5 a 500 caracteres).'
export const MENSAJE_ELIGE_PLAN = 'Elige un plan activo.'
export const MENSAJE_ELIGE_ASESOR = 'Elige un asesor aprobado.'

export const NOMBRES_ESTADO_SUSCRIPCION: Record<EstadoSuscripcion, string> = {
  PENDIENTE: 'Pendiente',
  ACTIVA: 'Activa',
  SUSPENDIDA: 'Suspendida',
  REACTIVADA: 'Reactivada',
  VENCIDA: 'Vencida',
  CANCELADA: 'Cancelada',
  RECHAZADA: 'Rechazada',
}

/** Estados que se ofrecen en el filtro de la lista. */
export const ESTADOS_FILTRO: readonly EstadoSuscripcion[] = [
  'ACTIVA',
  'SUSPENDIDA',
  'REACTIVADA',
  'VENCIDA',
  'CANCELADA',
]

/** Estado en texto (no solo color). */
export function nombreEstadoSuscripcion(estado: string): string {
  const clave = estado.toUpperCase()
  return clave in NOMBRES_ESTADO_SUSCRIPCION
    ? NOMBRES_ESTADO_SUSCRIPCION[clave as EstadoSuscripcion]
    : estado || 'Desconocido'
}

/**
 * Acciones válidas según el contrato, a partir del estado efectivo:
 * - ACTIVA / REACTIVADA: renovar, cambiar de plan, suspender y cancelar.
 * - VENCIDA (guardada como ACTIVA o REACTIVADA, sigue viva): renovar, cambiar de plan y cancelar.
 * - SUSPENDIDA: reactivar, cambiar de plan y cancelar.
 * - CANCELADA, PENDIENTE, RECHAZADA: ninguna.
 */
export function accionesSuscripcion(s: Pick<Suscripcion, 'estado' | 'estadoEfectivo'>): AccionSuscripcion[] {
  const efectivo = (s.estadoEfectivo || s.estado).toUpperCase()
  switch (efectivo) {
    case 'ACTIVA':
    case 'REACTIVADA':
      return ['renovar', 'cambiar-plan', 'suspender', 'cancelar']
    case 'VENCIDA':
      return ['renovar', 'cambiar-plan', 'cancelar']
    case 'SUSPENDIDA':
      return ['reactivar', 'cambiar-plan', 'cancelar']
    default:
      return []
  }
}

/** Suspender y cancelar exigen motivo (5–500); reactivar lo tiene opcional (hasta 500). */
export function motivoObligatorioSuscripcion(accion: AccionSuscripcion): boolean {
  return accion === 'suspender' || accion === 'cancelar'
}

export function validarMotivoSuscripcion(accion: AccionSuscripcion, motivo: string): string[] {
  const largo = motivo.trim().length
  if (motivoObligatorioSuscripcion(accion)) {
    return largo >= MOTIVO_MIN && largo <= MOTIVO_MAX ? [] : [MENSAJE_MOTIVO_SUSCRIPCION]
  }
  return largo <= MOTIVO_MAX ? [] : ['El motivo no puede tener más de 500 caracteres.']
}

// ---- Lectura de respuestas (tolerante con la forma exacta) ----

function texto(valor: unknown): string {
  return typeof valor === 'string' ? valor : ''
}

function textoONulo(valor: unknown): string | null {
  return typeof valor === 'string' && valor !== '' ? valor : null
}

function numeroONulo(valor: unknown): number | null {
  return typeof valor === 'number' && Number.isFinite(valor) ? valor : null
}

function idTexto(valor: unknown): string {
  if (typeof valor === 'number' && Number.isFinite(valor)) return String(valor)
  return typeof valor === 'string' ? valor : ''
}

function leerPlanResumen(valor: Record<string, unknown>): ResumenPlanSuscripcion {
  const plan = esObjetoJson(valor.plan) ? valor.plan : {}
  return {
    id: numeroONulo(plan.id) ?? numeroONulo(valor.planId),
    nombre: texto(plan.nombre) || texto(valor.planNombre),
    limitePublicaciones:
      numeroONulo(plan.limitePublicaciones) ?? numeroONulo(valor.limitePublicaciones),
    diasVigencia: numeroONulo(plan.diasVigencia) ?? numeroONulo(valor.diasVigencia),
  }
}

export function leerSuscripcion(valor: unknown): Suscripcion | null {
  if (!esObjetoJson(valor)) return null
  const id = numeroONulo(valor.id)
  if (id === null) return null
  const asesor = esObjetoJson(valor.asesor) ? valor.asesor : {}
  const estado = texto(valor.estado).toUpperCase()
  return {
    id,
    asesor: {
      id: idTexto(asesor.id) || idTexto(valor.asesorId),
      nombre: texto(asesor.nombre) || texto(asesor.nombreCompleto) || texto(valor.asesorNombre),
    },
    plan: leerPlanResumen(valor),
    estado,
    estadoEfectivo: texto(valor.estadoEfectivo).toUpperCase() || estado,
    inicioVigencia: textoONulo(valor.inicioVigencia),
    finVigencia: textoONulo(valor.finVigencia),
    diasRestantes: numeroONulo(valor.diasRestantes),
  }
}

const MENSAJES_SUSCRIPCIONES: Partial<Record<number, string>> = {
  401: MENSAJE_SESION_TERMINADA,
  404: MENSAJE_SUSCRIPCION_NO_ENCONTRADA,
}

// ---- Endpoints del administrador ----

export async function listarSuscripciones(
  token: string,
  filtros: FiltrosSuscripciones,
  signal?: AbortSignal,
): Promise<ListaSuscripciones> {
  const parametros = new URLSearchParams()
  if (filtros.asesorId) parametros.set('asesorId', filtros.asesorId)
  if (filtros.estado) parametros.set('estado', filtros.estado)
  parametros.set('pagina', String(filtros.pagina))
  parametros.set('tamano', String(filtros.tamano))
  const respuesta = await peticion(`/api/admin/suscripciones?${parametros.toString()}`, {
    token,
    signal,
    mensajesPorCodigo: MENSAJES_SUSCRIPCIONES,
  })
  const lista = esObjetoJson(respuesta) ? respuesta.items : respuesta
  if (!Array.isArray(lista)) throw new ApiError(200, MENSAJE_INESPERADO)
  const items = lista.map(leerSuscripcion).filter((s): s is Suscripcion => s !== null)
  const cuerpo = esObjetoJson(respuesta) ? respuesta : {}
  return {
    items,
    total: numeroONulo(cuerpo.total) ?? items.length,
    pagina: numeroONulo(cuerpo.pagina) ?? filtros.pagina,
    tamano: numeroONulo(cuerpo.tamano) ?? filtros.tamano,
  }
}

/** Asignar un plan a un asesor sin suscripción viva (201; 409 si ya tiene una). */
export async function asignarPlan(
  token: string,
  asesorId: string,
  planId: number,
): Promise<Suscripcion | null> {
  const respuesta = await peticion(
    `/api/admin/asesores/${encodeURIComponent(asesorId)}/suscripcion`,
    { method: 'POST', token, body: { planId }, mensajesPorCodigo: MENSAJES_SUSCRIPCIONES },
  )
  return leerSuscripcion(respuesta)
}

/**
 * Renovar (`{ planId? }`), cambiar de plan (`{ planId }`), suspender, reactivar o cancelar (`{ motivo }`).
 */
export async function ejecutarAccionSuscripcion(
  token: string,
  id: number,
  accion: AccionSuscripcion,
  datos: { planId?: number | null; motivo?: string },
): Promise<Suscripcion | null> {
  let body: Record<string, unknown>
  if (accion === 'renovar') body = datos.planId ? { planId: datos.planId } : {}
  else if (accion === 'cambiar-plan') body = { planId: datos.planId ?? null }
  else {
    const limpio = (datos.motivo ?? '').trim()
    body = { motivo: limpio === '' ? null : limpio }
  }
  const respuesta = await peticion(
    `/api/admin/suscripciones/${encodeURIComponent(String(id))}/${accion}`,
    { method: 'POST', token, body, mensajesPorCodigo: MENSAJES_SUSCRIPCIONES },
  )
  return leerSuscripcion(respuesta)
}

// ---- Mi suscripción (asesor) ----

export async function obtenerMiSuscripcion(
  token: string,
  signal?: AbortSignal,
): Promise<MiSuscripcion> {
  const respuesta = await peticion('/api/asesor/suscripcion', {
    token,
    signal,
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA },
  })
  if (!esObjetoJson(respuesta)) throw new ApiError(200, MENSAJE_INESPERADO)
  // Sin suscripción: { suscripcion: null, mensaje }.
  if ('suscripcion' in respuesta && respuesta.suscripcion === null) {
    return { tieneSuscripcion: false, mensaje: texto(respuesta.mensaje) || MENSAJE_SIN_PLAN }
  }
  // Con suscripción: los datos en la raíz (contrato) o dentro de `suscripcion`.
  const datos = esObjetoJson(respuesta.suscripcion) ? respuesta.suscripcion : respuesta
  const estado = texto(datos.estado).toUpperCase()
  if (!esObjetoJson(datos.plan) && !estado) {
    return { tieneSuscripcion: false, mensaje: texto(respuesta.mensaje) || MENSAJE_SIN_PLAN }
  }
  const estadoEfectivo = texto(datos.estadoEfectivo).toUpperCase() || estado
  return {
    tieneSuscripcion: true,
    plan: leerPlanResumen(datos),
    estado,
    estadoEfectivo,
    inicioVigencia: textoONulo(datos.inicioVigencia),
    finVigencia: textoONulo(datos.finVigencia),
    diasRestantes: numeroONulo(datos.diasRestantes),
    vigente:
      typeof datos.vigente === 'boolean'
        ? datos.vigente
        : estadoEfectivo === 'ACTIVA' || estadoEfectivo === 'REACTIVADA',
  }
}

// ---- Formatos ----

const FORMATO_FECHA = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' })
const FORMATO_FECHA_LARGA = new Intl.DateTimeFormat('es-CO', { dateStyle: 'long' })

/** Fecha en formato local (es-CO); "—" si no hay o no es válida. */
export function formatearFechaSuscripcion(valor: string | null, larga = false): string {
  if (!valor) return '—'
  const ms = Date.parse(valor)
  if (Number.isNaN(ms)) return '—'
  return (larga ? FORMATO_FECHA_LARGA : FORMATO_FECHA).format(ms)
}

/** "1 día", "12 días"; "—" si no se conoce. Nunca negativo. */
export function textoDiasRestantes(dias: number | null): string {
  if (dias === null) return '—'
  const n = Math.max(0, Math.floor(dias))
  return n === 1 ? '1 día' : `${n} días`
}
