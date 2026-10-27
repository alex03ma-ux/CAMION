// Carga de datos del catálogo con estados de carga, éxito y error, y reintento.
import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'

export type EstadoPeticion<T> =
  | { estado: 'cargando' }
  | { estado: 'ok'; datos: T }
  | { estado: 'error'; error: ApiError }

type Guardado<T> = { clave: string; intento: number; resultado: EstadoPeticion<T> }

const CARGANDO = { estado: 'cargando' } as const

/**
 * Ejecuta `cargar` cada vez que cambia `clave` (y al reintentar). Mientras la
 * clave guardada no coincide con la actual devuelve "cargando", para no
 * mostrar datos de la consulta anterior. Cancela la petición anterior.
 */
export function usePeticion<T>(
  cargar: (signal: AbortSignal) => Promise<T>,
  clave: string,
): [EstadoPeticion<T>, () => void] {
  const [intento, setIntento] = useState(0)
  const [guardado, setGuardado] = useState<Guardado<T> | null>(null)
  const refCargar = useRef(cargar)
  useEffect(() => {
    refCargar.current = cargar
  }, [cargar])

  useEffect(() => {
    const control = new AbortController()
    refCargar
      .current(control.signal)
      .then((datos) => {
        if (!control.signal.aborted) {
          setGuardado({ clave, intento, resultado: { estado: 'ok', datos } })
        }
      })
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        const apiError =
          error instanceof ApiError ? error : new ApiError(0, MENSAJE_INESPERADO)
        setGuardado({ clave, intento, resultado: { estado: 'error', error: apiError } })
      })
    return () => control.abort()
  }, [clave, intento])

  const reintentar = useCallback(() => setIntento((i) => i + 1), [])

  const vigente = guardado !== null && guardado.clave === clave && guardado.intento === intento
  return [vigente ? guardado.resultado : CARGANDO, reintentar]
}
