// P8 — Sección "Actividad" del detalle de un vehículo del inventario y de una suscripción (HU-061, RN-SE-003; D-62 d,
// D-72 A/A; CA-P8-56 a 62). Solo el administrador; línea de tiempo de solo lectura, del evento más reciente al más
// antiguo, con la acción en palabras, quién, cuándo (hora de Colombia), el motivo y, en las ediciones, los campos que
// cambiaron. Se abre con un botón para no consultar la API si no se necesita. La API lo protege igual (403 / 401).
import { useCallback, useEffect, useRef, useState } from 'react'
import { listarActividad, nombreSobre, type ListaActividad, type RecursoActividad } from '../api/actividad'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { formatearFechaHora } from '../api/leads'
import { Aviso, type DatosAviso } from './Aviso'
import { PaginacionBotones, plural } from './PiezasReporte'

type Props = {
  token: string
  recurso: RecursoActividad
  id: number
  onSesionTerminada: () => void
  /** Nivel del título de la sección. */
  nivelTitulo?: 2 | 3 | 4
  /** Abierta desde el inicio (p. ej. cuando la abre un botón externo). */
  abiertaAlInicio?: boolean
  /** Texto para el lector de pantalla tras "Actividad" (p. ej. "de la suscripción de Ana"). */
  descripcion?: string
  /** Al ocultarla, quien la abrió la quita y devuelve el foco a su botón. */
  onCerrar?: () => void
  /** Mueve el foco al título al aparecer (cuando la abre un botón de otra parte de la pantalla). */
  enfocarAlMontar?: boolean
}

type Carga = { estado: 'cargando' } | { estado: 'ok'; datos: ListaActividad } | { estado: 'error'; aviso: DatosAviso }

/** Sección con botón "Ver actividad" / "Ocultar actividad". */
export function SeccionActividad(props: Props) {
  const [abierta, setAbierta] = useState(props.abiertaAlInicio ?? false)
  const Titulo = `h${props.nivelTitulo ?? 2}` as 'h2' | 'h3' | 'h4'
  const idTitulo = `actividad-${props.recurso}-${props.id}-titulo`
  const idPanel = `actividad-${props.recurso}-${props.id}`
  const refTitulo = useRef<HTMLHeadingElement>(null)
  const enfocar = props.enfocarAlMontar ?? false
  useEffect(() => {
    if (enfocar) refTitulo.current?.focus()
  }, [enfocar])
  return (
    <section className="actividad" aria-labelledby={idTitulo}>
      <Titulo id={idTitulo} ref={refTitulo} tabIndex={-1} className="inv-subtitulo">
        Actividad
        {props.descripcion && <span className="sr-only"> {props.descripcion}</span>}
      </Titulo>
      <p className="form-ayuda">Registro de solo lectura de lo que se hizo y quién lo hizo.</p>
      <button
        type="button"
        className="secondary-button inv-boton-auto"
        aria-expanded={abierta}
        aria-controls={abierta ? idPanel : undefined}
        onClick={() => {
          if (abierta && props.onCerrar) {
            props.onCerrar()
            return
          }
          setAbierta((a) => !a)
        }}
      >
        {abierta ? 'Ocultar actividad' : 'Ver actividad'}
      </button>
      {abierta && <LineaTiempo {...props} idPanel={idPanel} />}
    </section>
  )
}

function LineaTiempo({ token, recurso, id, onSesionTerminada, idPanel }: Props & { idPanel: string }) {
  const [pagina, setPagina] = useState(1)
  const [carga, setCarga] = useState<Carga>({ estado: 'cargando' })
  const [intento, setIntento] = useState(0)
  const inicio = useRef<HTMLParagraphElement>(null)
  const enfocarAlCargar = useRef(false)

  const es401 = useCallback(
    (error: unknown) => {
      if (error instanceof ApiError && error.status === 401) {
        onSesionTerminada()
        return true
      }
      return false
    },
    [onSesionTerminada],
  )

  useEffect(() => {
    const control = new AbortController()
    setCarga({ estado: 'cargando' })
    listarActividad(token, recurso, id, pagina, control.signal)
      .then((datos) => {
        setCarga({ estado: 'ok', datos })
        if (enfocarAlCargar.current) {
          enfocarAlCargar.current = false
          window.setTimeout(() => inicio.current?.focus(), 0)
        }
      })
      .catch((error: unknown) => {
        if (control.signal.aborted || es401(error)) return
        setCarga({ estado: 'error', aviso: { tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO } })
      })
    return () => control.abort()
  }, [token, recurso, id, pagina, intento, es401])

  return (
    <div id={idPanel} className="actividad-panel">
      {carga.estado === 'cargando' && <p role="status">Cargando la actividad...</p>}
      {carga.estado === 'error' && (
        <div className="inv-estado-carga">
          <Aviso aviso={carga.aviso} />
          <button type="button" className="secondary-button inv-boton-auto" onClick={() => setIntento((i) => i + 1)}>
            Reintentar
          </button>
        </div>
      )}
      {carga.estado === 'ok' && carga.datos.total === 0 && <p className="admin-vacio">Aún no hay actividad registrada.</p>}
      {carga.estado === 'ok' && carga.datos.items.length > 0 && (
        <>
          <p ref={inicio} tabIndex={-1} className="admin-total actividad-total">
            {plural(carga.datos.total, 'evento', 'eventos')}, del más reciente al más antiguo.
          </p>
          <ol className="actividad-lista">
            {carga.datos.items.map((e) => (
              <li key={e.id} className="actividad-evento">
                <p className="actividad-descripcion">{e.descripcion}</p>
                <p className="actividad-meta">
                  <time dateTime={e.fecha ?? undefined}>{formatearFechaHora(e.fecha)}</time>
                  <span aria-hidden="true"> · </span>
                  <span>
                    <span className="sr-only">Por: </span>
                    {e.quien}
                  </span>
                  <span aria-hidden="true"> · </span>
                  <span>
                    <span className="sr-only">Sobre: </span>
                    {nombreSobre(e.sobre, e.sobreId)}
                  </span>
                </p>
                {e.motivo && (
                  <p className="actividad-motivo inv-texto-largo">
                    <strong>Motivo:</strong> {e.motivo}
                  </p>
                )}
                {e.cambios.length > 0 && (
                  <ul className="actividad-cambios" aria-label="Campos que cambiaron">
                    {e.cambios.map((c, i) => (
                      <li key={`${c.campo}-${i}`} className="inv-texto-largo">
                        <strong>{c.campo}:</strong> {c.anterior} <span aria-hidden="true">→</span>
                        <span className="sr-only"> pasó a </span> {c.nuevo}
                      </li>
                    ))}
                  </ul>
                )}
              </li>
            ))}
          </ol>
        </>
      )}
      {carga.estado === 'ok' && (
        <PaginacionBotones
          etiqueta="Paginación de la actividad"
          pagina={carga.datos.pagina}
          total={carga.datos.total}
          tamano={carga.datos.tamano}
          onIr={(n) => {
            enfocarAlCargar.current = true
            setPagina(n)
          }}
        />
      )}
    </div>
  )
}
