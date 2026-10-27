// Administración de usuarios (contrato docs/criterios/P2-contrato-api.md).
// HU-043 aprobar, HU-044 rechazar, HU-047 bloquear, HU-048 consultar; RN-SE-006.
// Todas las rutas exigen el rol ADMINISTRADOR en la API (política SoloAdministrador).
import { MENSAJE_SESION_TERMINADA, type EstadoCuenta } from './auth'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion } from './http'

// ---- Tipos del contrato ----

export type DatosAsesor = {
  tipoDocumento: string
  numeroDocumento: string
  nombreComercial: string | null
  estadoAutorizacion: string
  autorizadoEn: string | null
}

export type UsuarioAdmin = {
  usuarioId: string
  nombreCompleto: string
  email: string
  telefono: string
  roles: string[]
  estado: string
  creadoEn: string
  ultimoAccesoEn: string | null
  asesor: DatosAsesor | null
}

export type ListaUsuarios = {
  items: UsuarioAdmin[]
  total: number
  pagina: number
  tamano: number
}

export type FiltrosUsuarios = {
  /** Vacío = todos los estados. */
  estado: EstadoCuenta | ''
  /** Vacío = todos los roles. */
  rol: string
  buscar: string
  pagina: number
  tamano: number
}

export type AccionUsuario = 'aprobar' | 'rechazar' | 'bloquear' | 'desbloquear' | 'reconsiderar'

export const TAMANO_PAGINA = 20
export const BUSCAR_MAX = 100
export const MOTIVO_MIN = 5
export const MOTIVO_MAX = 500

export const MENSAJE_MOTIVO = 'Escribe el motivo (5 a 500 caracteres).'
export const MENSAJE_MOTIVO_LARGO = 'El motivo no puede tener más de 500 caracteres.'
export const MENSAJE_USUARIO_NO_ENCONTRADO = 'Usuario no encontrado.'

/** Rechazar, bloquear y reconsiderar exigen motivo; aprobar y desbloquear lo tienen opcional. */
export function motivoObligatorio(accion: AccionUsuario): boolean {
  return accion === 'rechazar' || accion === 'bloquear' || accion === 'reconsiderar'
}

/** Valida el motivo según el contrato. Devuelve la lista de errores. */
export function validarMotivo(accion: AccionUsuario, motivo: string): string[] {
  const largo = motivo.trim().length
  if (motivoObligatorio(accion)) {
    return largo >= MOTIVO_MIN && largo <= MOTIVO_MAX ? [] : [MENSAJE_MOTIVO]
  }
  return largo <= MOTIVO_MAX ? [] : [MENSAJE_MOTIVO_LARGO]
}

// ---- Validadores de forma de las respuestas ----

function texto(valor: unknown): string {
  return typeof valor === 'string' ? valor : ''
}

function textoONulo(valor: unknown): string | null {
  return typeof valor === 'string' && valor !== '' ? valor : null
}

function numero(valor: unknown, porDefecto: number): number {
  return typeof valor === 'number' && Number.isFinite(valor) ? valor : porDefecto
}

function listaTextos(valor: unknown): string[] {
  return Array.isArray(valor) ? valor.filter((v): v is string => typeof v === 'string') : []
}

function leerAsesor(valor: unknown): DatosAsesor | null {
  if (!esObjetoJson(valor)) return null
  return {
    tipoDocumento: texto(valor.tipoDocumento),
    numeroDocumento: texto(valor.numeroDocumento),
    nombreComercial: textoONulo(valor.nombreComercial),
    estadoAutorizacion: texto(valor.estadoAutorizacion),
    autorizadoEn: textoONulo(valor.autorizadoEn),
  }
}

function leerUsuario(valor: unknown): UsuarioAdmin | null {
  if (!esObjetoJson(valor)) return null
  const usuarioId = texto(valor.usuarioId)
  if (!usuarioId) return null
  return {
    usuarioId,
    nombreCompleto: texto(valor.nombreCompleto),
    email: texto(valor.email),
    telefono: texto(valor.telefono),
    roles: listaTextos(valor.roles),
    estado: texto(valor.estado).toUpperCase(),
    creadoEn: texto(valor.creadoEn),
    ultimoAccesoEn: textoONulo(valor.ultimoAccesoEn),
    asesor: leerAsesor(valor.asesor),
  }
}

const MENSAJES_ADMIN: Partial<Record<number, string>> = {
  401: MENSAJE_SESION_TERMINADA,
  404: MENSAJE_USUARIO_NO_ENCONTRADO,
}

// ---- Endpoints ----

export async function listarUsuarios(
  token: string,
  filtros: FiltrosUsuarios,
  signal?: AbortSignal,
): Promise<ListaUsuarios> {
  const parametros = new URLSearchParams()
  if (filtros.estado) parametros.set('estado', filtros.estado)
  if (filtros.rol) parametros.set('rol', filtros.rol)
  const buscar = filtros.buscar.trim()
  if (buscar) parametros.set('buscar', buscar.slice(0, BUSCAR_MAX))
  parametros.set('pagina', String(filtros.pagina))
  parametros.set('tamano', String(filtros.tamano))

  const respuesta = await peticion(`/api/admin/usuarios?${parametros.toString()}`, {
    token,
    signal,
    mensajesPorCodigo: MENSAJES_ADMIN,
  })
  if (!esObjetoJson(respuesta) || !Array.isArray(respuesta.items)) {
    throw new ApiError(200, MENSAJE_INESPERADO)
  }
  const items = respuesta.items
    .map(leerUsuario)
    .filter((u): u is UsuarioAdmin => u !== null)
  return {
    items,
    total: numero(respuesta.total, items.length),
    pagina: numero(respuesta.pagina, filtros.pagina),
    tamano: numero(respuesta.tamano, filtros.tamano),
  }
}

/**
 * Ejecuta aprobar / rechazar / bloquear / desbloquear.
 * Devuelve el usuario actualizado (misma forma que un ítem de la lista), o null
 * si el servidor no lo devolvió con la forma esperada.
 */
export async function ejecutarAccionUsuario(
  token: string,
  usuarioId: string,
  accion: AccionUsuario,
  motivo: string,
): Promise<UsuarioAdmin | null> {
  const limpio = motivo.trim()
  const respuesta = await peticion(
    `/api/admin/usuarios/${encodeURIComponent(usuarioId)}/${accion}`,
    {
      method: 'POST',
      token,
      body: { motivo: limpio === '' ? null : limpio },
      mensajesPorCodigo: MENSAJES_ADMIN,
    },
  )
  return leerUsuario(respuesta)
}
