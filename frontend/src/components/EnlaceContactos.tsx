// P5 (D-48, CA-P5-58): acceso "Contactos (N nuevos)" en el panel del asesor y del administrador.
// Consulta GET /api/leads/resumen al mostrarse; si falla (salvo 401), el enlace sigue sin el número.
import { useEffect, useState } from 'react'
import { ApiError } from '../api/http'
import { obtenerResumenLeads, textoMenuContactos } from '../api/leads'
import { HASH_CONTACTOS } from '../rutas'

type EnlaceContactosProps = {
  token: string
  className?: string
  onSesionTerminada?: () => void
}

export function EnlaceContactos({ token, className, onSesionTerminada }: EnlaceContactosProps) {
  const [nuevos, setNuevos] = useState<number | null>(null)

  useEffect(() => {
    const control = new AbortController()
    obtenerResumenLeads(token, control.signal)
      .then((r) => setNuevos(r.nuevos))
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        if (error instanceof ApiError && error.status === 401) onSesionTerminada?.()
      })
    return () => control.abort()
  }, [token, onSesionTerminada])

  return (
    <a className={className} href={HASH_CONTACTOS}>
      {textoMenuContactos(nuevos)}
    </a>
  )
}
