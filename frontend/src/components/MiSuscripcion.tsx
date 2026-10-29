// P4p — Tarjeta "Mi suscripción" del asesor en "Mi cuenta" (HU-066, RN-SU-001, RN-SU-005; D-35).
// La API (política SoloAsesor) devuelve la suscripción viva o la última; sin plan, el mensaje del contrato.
import { useEffect, useState } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import {
  formatearFechaSuscripcion,
  nombreEstadoSuscripcion,
  obtenerMiSuscripcion,
  textoDiasRestantes,
  type MiSuscripcion as DatosMiSuscripcion,
} from '../api/suscripciones'
import { formatoMiles } from '../catalogo/formato'
import { Aviso } from './Aviso'

type MiSuscripcionProps = {
  token: string
  onSesionTerminada?: () => void
}

type Estado =
  | { tipo: 'cargando' }
  | { tipo: 'ok'; datos: DatosMiSuscripcion }
  | { tipo: 'error'; mensaje: string }

export function MiSuscripcion({ token, onSesionTerminada }: MiSuscripcionProps) {
  const [estado, setEstado] = useState<Estado>({ tipo: 'cargando' })
  const [intento, setIntento] = useState(0)

  useEffect(() => {
    const control = new AbortController()
    setEstado({ tipo: 'cargando' })
    obtenerMiSuscripcion(token, control.signal)
      .then((datos) => setEstado({ tipo: 'ok', datos }))
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        if (error instanceof ApiError && error.status === 401 && onSesionTerminada) {
          onSesionTerminada()
          return
        }
        setEstado({
          tipo: 'error',
          mensaje: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO,
        })
      })
    return () => control.abort()
  }, [token, intento, onSesionTerminada])

  return (
    <div className="tarjeta-suscripcion" role="region" aria-labelledby="mi-suscripcion-titulo">
      <h3 id="mi-suscripcion-titulo">Mi suscripción</h3>
      {estado.tipo === 'cargando' && <p role="status">Cargando tu suscripción...</p>}
      {estado.tipo === 'error' && (
        <>
          <Aviso aviso={{ tipo: 'error', texto: estado.mensaje }} />
          <button type="button" className="secondary-button" onClick={() => setIntento((i) => i + 1)}>
            Reintentar
          </button>
        </>
      )}
      {estado.tipo === 'ok' && !estado.datos.tieneSuscripcion && (
        <p className="tarjeta-suscripcion-vacia">{estado.datos.mensaje}</p>
      )}
      {estado.tipo === 'ok' && estado.datos.tieneSuscripcion && (
        <>
          <dl className="perfil-datos">
            <dt>Plan</dt>
            <dd>{estado.datos.plan.nombre || '—'}</dd>
            <dt>Estado</dt>
            <dd>{nombreEstadoSuscripcion(estado.datos.estadoEfectivo)}</dd>
            <dt>Vence el</dt>
            <dd>{formatearFechaSuscripcion(estado.datos.finVigencia, true)}</dd>
            <dt>Días restantes</dt>
            <dd>{textoDiasRestantes(estado.datos.diasRestantes)}</dd>
            <dt>Cupo</dt>
            <dd>
              {estado.datos.plan.limitePublicaciones === null
                ? '—'
                : `${formatoMiles(estado.datos.plan.limitePublicaciones)} ${
                    estado.datos.plan.limitePublicaciones === 1 ? 'publicación' : 'publicaciones'
                  }`}
            </dd>
          </dl>
          {!estado.datos.vigente && (
            <p className="form-ayuda">
              {estado.datos.estadoEfectivo === 'SUSPENDIDA'
                ? 'Tu suscripción está suspendida: tus publicaciones no se muestran en el portal. Comunícate con el administrador.'
                : 'Tu suscripción no está vigente. Comunícate con el administrador para renovarla.'}
            </p>
          )}
        </>
      )}
    </div>
  )
}
