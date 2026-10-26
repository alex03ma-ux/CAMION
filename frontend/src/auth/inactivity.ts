// Cierre automático de sesión por inactividad (HU-060, RN-SE-002).
// La hora de la última actividad se comparte entre pestañas mediante
// localStorage (`camion.ultimaActividad`): la actividad en cualquier pestaña
// cuenta para todas (CA-060-14). El cierre por inactividad es solo local:
// no llama a /api/auth/logout (CA-060-10).
import { useEffect, useRef } from 'react'
import { guardarUltimaActividad, leerUltimaActividad } from './session'

/**
 * Minutos sin actividad antes de cerrar la sesión.
 * Valor PROPUESTO (contrato P1), pendiente de confirmación de la responsable.
 * Para cambiarlo basta con editar esta constante.
 */
export const MINUTOS_INACTIVIDAD = 30
export const INACTIVIDAD_MS = MINUTOS_INACTIVIDAD * 60 * 1000

/** Eventos que cuentan como actividad: clics, teclas y toques. */
export const EVENTOS_ACTIVIDAD = ['click', 'keydown', 'touchstart'] as const

/** Como mucho una escritura en localStorage cada este intervalo. */
export const INTERVALO_GUARDADO_MS = 5 * 1000

/** setTimeout no admite más de ~24,8 días. */
const MAX_TIMEOUT_MS = 2_147_483_647

export function inactivoDemasiado(
  ultimaActividad: number,
  ahora: number,
  limiteMs: number = INACTIVIDAD_MS,
): boolean {
  return ahora - ultimaActividad >= limiteMs
}

/**
 * ¿La sesión guardada debe darse por terminada al abrir la app? (CA-060-13)
 * Sin valor guardado no se considera inactiva.
 */
export function inactividadGuardadaVencida(
  ahora: number = Date.now(),
  limiteMs: number = INACTIVIDAD_MS,
): boolean {
  const ultima = leerUltimaActividad()
  return ultima !== null && inactivoDemasiado(ultima, ahora, limiteMs)
}

/**
 * Llama a `alExpirar` una sola vez cuando pasan `limiteMs` sin actividad en
 * ninguna pestaña. Solo escucha mientras `activo` es true.
 */
export function useInactividad(
  activo: boolean,
  alExpirar: () => void,
  limiteMs: number = INACTIVIDAD_MS,
): void {
  const callback = useRef(alExpirar)
  useEffect(() => {
    callback.current = alExpirar
  }, [alExpirar])

  useEffect(() => {
    if (!activo) return

    const ahoraInicial = Date.now()
    const guardada = leerUltimaActividad()
    let ultimaLocal = guardada ?? ahoraInicial
    let ultimaEscritura = 0
    if (guardada === null) {
      guardarUltimaActividad(ahoraInicial)
      ultimaEscritura = ahoraInicial
    }

    let disparado = false
    let temporizador: number | undefined

    /** Última actividad conocida: la de esta pestaña o la de otra, la más reciente. */
    const ultimaActividad = () => Math.max(ultimaLocal, leerUltimaActividad() ?? 0)

    const expirar = () => {
      if (disparado) return
      disparado = true
      window.clearTimeout(temporizador)
      callback.current()
    }

    /** Relee el valor compartido; cierra o reprograma el temporizador. */
    const revisar = () => {
      if (disparado) return
      window.clearTimeout(temporizador)
      const ultima = ultimaActividad()
      const ahora = Date.now()
      if (inactivoDemasiado(ultima, ahora, limiteMs)) {
        expirar()
        return
      }
      const restante = ultima + limiteMs - ahora
      temporizador = window.setTimeout(revisar, Math.min(restante, MAX_TIMEOUT_MS))
    }

    const registrarActividad = () => {
      if (disparado) return
      const ahora = Date.now()
      // Si el equipo estuvo suspendido más del límite, la actividad posterior
      // no debe "revivir" la sesión.
      if (inactivoDemasiado(ultimaActividad(), ahora, limiteMs)) {
        expirar()
        return
      }
      ultimaLocal = ahora
      if (ahora - ultimaEscritura >= INTERVALO_GUARDADO_MS) {
        guardarUltimaActividad(ahora)
        ultimaEscritura = ahora
      }
    }

    const alVolverVisible = () => {
      if (document.visibilityState === 'visible') revisar()
    }

    for (const evento of EVENTOS_ACTIVIDAD) {
      window.addEventListener(evento, registrarActividad, { passive: true, capture: true })
    }
    document.addEventListener('visibilitychange', alVolverVisible)
    revisar()

    return () => {
      for (const evento of EVENTOS_ACTIVIDAD) {
        window.removeEventListener(evento, registrarActividad, { capture: true })
      }
      document.removeEventListener('visibilitychange', alVolverVisible)
      window.clearTimeout(temporizador)
    }
  }, [activo, limiteMs])
}
