// P4b — "Reasignar a otro asesor" en el detalle de un vehículo del inventario (D-34).
// Solo se muestra al administrador; la API (POST /api/admin/vehiculos/{id}/reasignar) lo protege igualmente.
import type { DetalleInventario, ResumenAsesor } from '../api/inventario'
import { Aviso, type DatosAviso } from './Aviso'
import { useDialogosVehiculo } from './useDialogosVehiculo'

type ReasignarVehiculoProps = {
  token: string
  detalle: DetalleInventario
  /** "Chevrolet NPR 2020" con los nombres de los catálogos. */
  titulo: string
  /** Asesores ACTIVA (catálogos del inventario del administrador). */
  asesores: readonly ResumenAsesor[]
  onSesionTerminada: () => void
  /** Resultado de la última reasignación; lo guarda la página porque el detalle se recarga. */
  resultado: DatosAviso | null
  /** Guarda el resultado y vuelve a cargar el detalle (el asesor cambió). */
  onResultado: (aviso: DatosAviso | null, recargar: boolean) => void
}

export function ReasignarVehiculo({
  token,
  detalle,
  titulo,
  asesores,
  onSesionTerminada,
  resultado,
  onResultado,
}: ReasignarVehiculoProps) {
  const dialogos = useDialogosVehiculo({
    token,
    asesores,
    onSesionTerminada,
    onTerminar: (aviso) => onResultado(aviso, true),
  })

  return (
    <section className="pub-reasignar" aria-labelledby="reasignar-titulo">
      <h2 id="reasignar-titulo" className="inv-subtitulo">
        Cambiar de asesor
      </h2>
      <p className="campo-ayuda">
        {detalle.asesor?.nombre ? `Hoy pertenece a ${detalle.asesor.nombre}. ` : ''}
        Puedes pasar el vehículo y su publicación a otro asesor activo. Se pide un motivo y queda auditado.
      </p>
      <Aviso aviso={resultado} />
      <button
        type="button"
        className="secondary-button"
        disabled={dialogos.ocupado}
        onClick={(e) => {
          onResultado(null, false)
          dialogos.abrirReasignar(
            { id: detalle.id, titulo, precioCop: detalle.precioCop, asesor: detalle.asesor },
            e.currentTarget,
          )
        }}
      >
        Reasignar a otro asesor
      </button>
      {dialogos.dialogo}
    </section>
  )
}
