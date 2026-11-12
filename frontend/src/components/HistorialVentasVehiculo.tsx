// P7 (CA-P7-06, 09, 11; D-62 A) — Sección "Historial de ventas" del detalle del vehículo en el inventario:
// todas sus ventas, la vigente primero, las revertidas con quién, cuándo y el motivo. La API devuelve 404 si el
// vehículo es de otro asesor (CA-P7-07). Vender y revertir no cambian: se hacen desde la lista o las publicaciones.
import { useEffect, useState } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { listarVentasVehiculo, MENSAJE_VENDIDO_SIN_DATOS, type VentaItem } from '../api/ventas'
import { Aviso, type DatosAviso } from './Aviso'
import { TarjetaVenta } from './Ventas'

type Props = {
  token: string
  vehiculoId: number
  /** Estado actual del vehículo (para el texto de CA-P7-09). */
  estadoVehiculo: string
  esAdmin: boolean
  onSesionTerminada: () => void
}

type Carga = { estado: 'cargando' } | { estado: 'ok'; items: VentaItem[] } | { estado: 'error'; aviso: DatosAviso }

export function HistorialVentasVehiculo({ token, vehiculoId, estadoVehiculo, esAdmin, onSesionTerminada }: Props) {
  const [carga, setCarga] = useState<Carga>({ estado: 'cargando' })
  const [intento, setIntento] = useState(0)

  useEffect(() => {
    const control = new AbortController()
    setCarga({ estado: 'cargando' })
    listarVentasVehiculo(token, vehiculoId, control.signal)
      .then((items) => setCarga({ estado: 'ok', items }))
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        if (error instanceof ApiError && error.status === 401) {
          onSesionTerminada()
          return
        }
        setCarga({
          estado: 'error',
          aviso: { tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO },
        })
      })
    return () => control.abort()
  }, [token, vehiculoId, estadoVehiculo, intento, onSesionTerminada])

  return (
    <section className="inv-historial-ventas" aria-labelledby="historial-ventas-titulo">
      <h2 id="historial-ventas-titulo" className="inv-subtitulo">
        Historial de ventas
      </h2>
      {carga.estado === 'cargando' && <p role="status">Cargando el historial de ventas...</p>}
      {carga.estado === 'error' && (
        <div className="inv-estado-carga">
          <Aviso aviso={carga.aviso} />
          <button type="button" className="secondary-button inv-boton-auto" onClick={() => setIntento((i) => i + 1)}>
            Reintentar
          </button>
        </div>
      )}
      {carga.estado === 'ok' && carga.items.length === 0 && (
        <p className="admin-vacio">
          {estadoVehiculo.toUpperCase() === 'VENDIDO' ? MENSAJE_VENDIDO_SIN_DATOS : 'Este vehículo no tiene ventas registradas.'}
        </p>
      )}
      {carga.estado === 'ok' && carga.items.length > 0 && (
        <ul className="inv-lista venta-lista">
          {carga.items.map((v) => (
            <TarjetaVenta key={v.id} venta={v} mostrarAsesor={esAdmin} conEnlace={false} />
          ))}
        </ul>
      )}
    </section>
  )
}
