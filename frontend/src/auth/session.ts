// Persistencia de la sesión en localStorage (HU-059, HU-060).
// Solo se guarda lo imprescindible: token, caducidad y datos básicos.
// Nunca se escribe el token en consola ni en la URL.
import type { LoginResponse } from '../api/auth'

export const CLAVE_SESION = 'camion.sesion'

export type SesionGuardada = {
  accessToken: string
  expiraEnUtc: string
  usuarioId: string
  nombreCompleto: string
  email: string
  roles: string[]
}

function almacen(): Storage | null {
  try {
    return typeof window !== 'undefined' ? window.localStorage : null
  } catch {
    // Acceso denegado (modo privado estricto, políticas del navegador, etc.).
    return null
  }
}

export function sesionDesdeLogin(respuesta: LoginResponse): SesionGuardada {
  return {
    accessToken: respuesta.accessToken,
    expiraEnUtc: respuesta.expiraEnUtc,
    usuarioId: respuesta.usuarioId,
    nombreCompleto: respuesta.nombreCompleto,
    email: respuesta.email,
    roles: respuesta.roles,
  }
}

/** Valida la forma de un valor leído de localStorage. */
export function esSesionValida(valor: unknown): valor is SesionGuardada {
  if (typeof valor !== 'object' || valor === null) return false
  const v = valor as Record<string, unknown>
  return (
    typeof v.accessToken === 'string' &&
    v.accessToken !== '' &&
    typeof v.expiraEnUtc === 'string' &&
    !Number.isNaN(Date.parse(v.expiraEnUtc)) &&
    typeof v.usuarioId === 'string' &&
    typeof v.nombreCompleto === 'string' &&
    typeof v.email === 'string' &&
    Array.isArray(v.roles) &&
    v.roles.every((r) => typeof r === 'string')
  )
}

/** Milisegundos hasta la caducidad del token (negativo si ya caducó). */
export function msHastaCaducidad(sesion: SesionGuardada, ahora: number = Date.now()): number {
  return Date.parse(sesion.expiraEnUtc) - ahora
}

export function sesionCaducada(sesion: SesionGuardada, ahora: number = Date.now()): boolean {
  return msHastaCaducidad(sesion, ahora) <= 0
}

/** Lee la sesión; si está corrupta la borra y devuelve null. */
export function leerSesion(): SesionGuardada | null {
  const ls = almacen()
  if (!ls) return null
  try {
    const crudo = ls.getItem(CLAVE_SESION)
    if (crudo === null) return null
    const datos: unknown = JSON.parse(crudo)
    if (esSesionValida(datos)) return datos
    ls.removeItem(CLAVE_SESION)
    return null
  } catch {
    borrarSesion()
    return null
  }
}

/** Guarda la sesión. Devuelve false si el navegador no lo permite. */
export function guardarSesion(sesion: SesionGuardada): boolean {
  const ls = almacen()
  if (!ls) return false
  try {
    ls.setItem(CLAVE_SESION, JSON.stringify(sesion))
    return true
  } catch {
    return false
  }
}

/** Borra la sesión y la hora de última actividad. */
export function borrarSesion(): void {
  const ls = almacen()
  if (!ls) return
  try {
    ls.removeItem(CLAVE_SESION)
  } catch {
    // Sin acceso al almacenamiento: no hay nada más que hacer.
  }
  borrarUltimaActividad()
}

// ---- Última actividad (cierre por inactividad, HU-060) ----

export const CLAVE_ULTIMA_ACTIVIDAD = 'camion.ultimaActividad'

/** Hora (ms desde época) de la última actividad guardada, o null si no hay. */
export function leerUltimaActividad(): number | null {
  const ls = almacen()
  if (!ls) return null
  try {
    const crudo = ls.getItem(CLAVE_ULTIMA_ACTIVIDAD)
    if (crudo === null) return null
    const valor = Number(crudo)
    return Number.isFinite(valor) && valor > 0 ? valor : null
  } catch {
    return null
  }
}

export function guardarUltimaActividad(momento: number = Date.now()): void {
  const ls = almacen()
  if (!ls) return
  try {
    ls.setItem(CLAVE_ULTIMA_ACTIVIDAD, String(Math.floor(momento)))
  } catch {
    // Sin acceso al almacenamiento: la inactividad se controla solo en memoria.
  }
}

export function borrarUltimaActividad(): void {
  const ls = almacen()
  if (!ls) return
  try {
    ls.removeItem(CLAVE_ULTIMA_ACTIVIDAD)
  } catch {
    // Nada más que hacer.
  }
}
