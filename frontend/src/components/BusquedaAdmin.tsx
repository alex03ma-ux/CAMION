// P6 — Detalle de una búsqueda en el panel del administrador (HU-035 a HU-038, D-56 b A, D-56 c A):
// criterios con nombres legibles (CA-P6-42), vehículos relacionados vigentes con su estado actual (CA-P6-50, 56),
// "Ver coincidencias" bajo demanda (CA-P6-51), relacionar con nota interna opcional y retirar (CA-P6-53).
// La API protege cada acción: 404 al asesor, 409 si la búsqueda está cerrada, el vehículo no está publicado, ya está
// relacionado o hay 10 relacionados (CA-P6-52).
import { useRef, useState } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import {
  erroresDeCampo,
  formatearFechaHora,
  MENSAJE_NOTA_RELACION,
  nombreEstadoPublicacionLead,
  nombreEstadoVehiculo,
  NOTA_RELACION_MAX,
  obtenerCoincidencias,
  relacionarVehiculo,
  retirarVehiculo,
  type Coincidencia,
  type CriteriosBusqueda,
  type LeadDetalle,
  type VehiculoRelacionado,
} from '../api/leads'
import { formatoCop, formatoMiles, textoUbicacion } from '../catalogo/formato'
import { hashVehiculo } from '../rutas'
import { Aviso, type DatosAviso } from './Aviso'
import { CampoAreaTexto } from './Campo'
import { DialogoConfirmacion } from './DialogoAccion'

/** Tope de relaciones vigentes por búsqueda (contrato P6). */
export const MAX_RELACIONADOS = 10

type BusquedaAdminProps = {
  token: string
  lead: LeadDetalle
  abierto: boolean
  /** Otra acción del detalle en curso: se desactivan los botones. */
  ocupado: boolean
  onActualizado: (lead: LeadDetalle, texto: string) => void
  /** 401, 403, 404 y 409 los resuelve el detalle (y vuelve a leer el contacto); devuelve true si lo hizo. */
  onErrorEstado: (error: unknown) => boolean
}

type Dialogo =
  | { tipo: 'relacionar'; coincidencia: Coincidencia; enviando: boolean; erroresNota: string[]; error: DatosAviso | null }
  | { tipo: 'retirar'; relacionado: VehiculoRelacionado; enviando: boolean; error: DatosAviso | null }

type EstadoCoincidencias =
  | { tipo: 'cerrado' }
  | { tipo: 'cargando' }
  | { tipo: 'error'; aviso: DatosAviso }
  | { tipo: 'ok'; items: Coincidencia[]; total: number }

function mensajeDe(error: unknown): string {
  return error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO
}

function rangoAnios(c: CriteriosBusqueda): string | null {
  if (c.anioDesde !== null && c.anioHasta !== null) return `${c.anioDesde} a ${c.anioHasta}`
  if (c.anioDesde !== null) return `Desde ${c.anioDesde}`
  if (c.anioHasta !== null) return `Hasta ${c.anioHasta}`
  return null
}

/** Criterios de la búsqueda (solo lectura; las correcciones van como nota, CA-P6-48). */
export function CriteriosBusquedaVista({ criterios: c }: { criterios: CriteriosBusqueda }) {
  const filas: [string, string | null][] = [
    ['Categoría', c.tipo || null],
    ['Marca', c.marca],
    ['Modelo', c.linea],
    ['Años', rangoAnios(c)],
    ['Presupuesto máximo', c.presupuestoMaximo !== null ? formatoCop(c.presupuestoMaximo) : null],
    ['Ubicación', textoUbicacion(c.municipio, c.departamento) || null],
  ]
  return (
    <dl className="inv-datos">
      {filas.map(([nombre, valor]) => (
        <div key={nombre}>
          <dt>{nombre}</dt>
          <dd>{valor ?? 'Sin preferencia'}</dd>
        </div>
      ))}
    </dl>
  )
}

export function BusquedaAdmin({ token, lead, abierto, ocupado, onActualizado, onErrorEstado }: BusquedaAdminProps) {
  const [coincidencias, setCoincidencias] = useState<EstadoCoincidencias>({ tipo: 'cerrado' })
  const [dialogo, setDialogo] = useState<Dialogo | null>(null)
  const origenDialogo = useRef<HTMLButtonElement | null>(null)
  const relacionados = lead.vehiculosRelacionados ?? []
  const lleno = relacionados.length >= MAX_RELACIONADOS
  const bloqueado = ocupado || dialogo !== null

  async function verCoincidencias() {
    setCoincidencias({ tipo: 'cargando' })
    try {
      const r = await obtenerCoincidencias(token, lead.id)
      setCoincidencias({ tipo: 'ok', items: r.items, total: r.total })
    } catch (error) {
      if (onErrorEstado(error)) {
        setCoincidencias({ tipo: 'cerrado' })
        return
      }
      setCoincidencias({ tipo: 'error', aviso: { tipo: 'error', texto: mensajeDe(error) } })
    }
  }

  function marcar(publicacionId: number, relacionado: boolean) {
    setCoincidencias((c) =>
      c.tipo === 'ok'
        ? { ...c, items: c.items.map((i) => (i.publicacionId === publicacionId ? { ...i, relacionado } : i)) }
        : c,
    )
  }

  function cancelar() {
    setDialogo(null)
    const boton = origenDialogo.current
    origenDialogo.current = null
    window.setTimeout(() => boton?.focus(), 0)
  }

  /** 401/403/404/409: el detalle lo anuncia y vuelve a leer; 400 y sin conexión, dentro del diálogo. */
  function fallo(error: unknown, enDialogo: (aviso: DatosAviso) => void) {
    if (onErrorEstado(error)) {
      setDialogo(null)
      origenDialogo.current = null
      return
    }
    enDialogo({ tipo: 'error', texto: mensajeDe(error) })
  }

  async function confirmarRelacionar(nota: string) {
    if (!dialogo || dialogo.tipo !== 'relacionar' || dialogo.enviando) return
    const { coincidencia } = dialogo
    setDialogo({ ...dialogo, enviando: true, error: null, erroresNota: [] })
    try {
      const actualizado = await relacionarVehiculo(token, lead.id, coincidencia.publicacionId, nota)
      setDialogo(null)
      origenDialogo.current = null
      marcar(coincidencia.publicacionId, true)
      onActualizado(actualizado, `Vehículo relacionado con la búsqueda: ${coincidencia.vehiculo.titulo}.`)
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        const n = erroresDeCampo(error, 'nota')
        const p = erroresDeCampo(error, 'publicacionId')
        setDialogo((d) =>
          d && d.tipo === 'relacionar'
            ? {
                ...d,
                enviando: false,
                erroresNota: n,
                error: n.length === 0 ? { tipo: 'error', texto: [error.mensaje, ...p].join(' ') } : null,
              }
            : d,
        )
        return
      }
      fallo(error, (aviso) =>
        setDialogo((d) => (d && d.tipo === 'relacionar' ? { ...d, enviando: false, error: aviso } : d)),
      )
    }
  }

  async function confirmarRetirar() {
    if (!dialogo || dialogo.tipo !== 'retirar' || dialogo.enviando) return
    const { relacionado } = dialogo
    setDialogo({ ...dialogo, enviando: true, error: null })
    try {
      const actualizado = await retirarVehiculo(token, lead.id, relacionado.publicacionId)
      setDialogo(null)
      origenDialogo.current = null
      marcar(relacionado.publicacionId, false)
      onActualizado(actualizado, `Vehículo retirado de la búsqueda: ${relacionado.titulo}.`)
    } catch (error) {
      fallo(error, (aviso) =>
        setDialogo((d) => (d && d.tipo === 'retirar' ? { ...d, enviando: false, error: aviso } : d)),
      )
    }
  }

  return (
    <>
      <section className="inv-resumen" aria-labelledby="busqueda-criterios-titulo">
        <h2 id="busqueda-criterios-titulo" className="inv-resumen-titulo">
          Qué busca
        </h2>
        {lead.busqueda ? <CriteriosBusquedaVista criterios={lead.busqueda} /> : <p>Sin criterios.</p>}
      </section>

      <section className="inv-resumen" aria-labelledby="busqueda-relacionados-titulo">
        <h2 id="busqueda-relacionados-titulo" className="inv-resumen-titulo">
          Vehículos relacionados{' '}
          <span className="admin-total">
            ({relacionados.length} de {MAX_RELACIONADOS})
          </span>
        </h2>
        <p className="campo-ayuda">
          El comprador con cuenta ve en "Mis solicitudes" los que siguen publicados. La nota es interna: no la ve.
        </p>
        {relacionados.length === 0 ? (
          <p className="admin-vacio">Aún no hay vehículos relacionados.</p>
        ) : (
          <ul className="busqueda-relacionados">
            {relacionados.map((r) => (
              <li key={r.publicacionId} className="busqueda-relacionado">
                <p className="busqueda-relacionado-titulo">
                  <strong>{r.titulo || `Publicación ${r.publicacionId}`}</strong>
                  {r.precioCop !== null && <span className="dato-secundario">{formatoCop(r.precioCop)}</span>}
                </p>
                <p>
                  {r.estadoVehiculo ? nombreEstadoVehiculo(r.estadoVehiculo) : '—'} ·{' '}
                  {r.estadoPublicacion ? nombreEstadoPublicacionLead(r.estadoPublicacion) : '—'} ·{' '}
                  {r.visible ? 'Se ve en el portal' : 'Ya no se ve en el portal'}
                </p>
                {r.nota && <p className="inv-texto-largo">Nota interna: {r.nota}</p>}
                <p className="dato-secundario">
                  Relacionado el {formatearFechaHora(r.relacionadoEn)}
                  {r.relacionadoPor ? ` por ${r.relacionadoPor}` : ''}
                </p>
                <div className="acciones-fila">
                  {r.visible && r.slug && (
                    <a
                      className="inv-boton-enlace inv-boton-pequeno inv-boton-secundario"
                      href={hashVehiculo(r.slug)}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      Ver en el portal <span className="sr-only">{r.titulo} (se abre en una pestaña nueva)</span>
                    </a>
                  )}
                  {abierto && (
                    <button
                      type="button"
                      className="secondary-button"
                      disabled={bloqueado}
                      onClick={(e) => {
                        origenDialogo.current = e.currentTarget
                        setDialogo({ tipo: 'retirar', relacionado: r, enviando: false, error: null })
                      }}
                    >
                      Retirar <span className="sr-only">{r.titulo}</span>
                    </button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}

        <div className="acciones-fila">
          <button
            type="button"
            className="secondary-button"
            disabled={bloqueado || coincidencias.tipo === 'cargando'}
            onClick={() => void verCoincidencias()}
          >
            {coincidencias.tipo === 'ok' ? 'Actualizar coincidencias' : 'Ver coincidencias'}
          </button>
        </div>

        {coincidencias.tipo === 'cargando' && <p role="status">Buscando coincidencias...</p>}
        {coincidencias.tipo === 'error' && <Aviso aviso={coincidencias.aviso} />}
        {coincidencias.tipo === 'ok' && (
          <div className="busqueda-coincidencias">
            <h3 className="lead-subtitulo" tabIndex={-1}>
              Coincidencias en el catálogo
            </h3>
            <p role="status">
              {coincidencias.total === 0
                ? 'No hay vehículos publicados que cumplan estos criterios.'
                : coincidencias.total > coincidencias.items.length
                  ? `${formatoMiles(coincidencias.total)} coincidencias; se muestran las ${coincidencias.items.length} primeras.`
                  : coincidencias.total === 1
                    ? '1 coincidencia.'
                    : `${formatoMiles(coincidencias.total)} coincidencias.`}
            </p>
            {!abierto && <p className="campo-ayuda">La búsqueda está cerrada: solo puedes consultar.</p>}
            {abierto && lleno && (
              <p className="campo-ayuda">Ya hay {MAX_RELACIONADOS} vehículos relacionados. Retira uno para relacionar otro.</p>
            )}
            {coincidencias.items.length > 0 && (
              <ul className="busqueda-relacionados">
                {coincidencias.items.map((c) => (
                  <li key={c.publicacionId} className="busqueda-relacionado">
                    <p className="busqueda-relacionado-titulo">
                      <a href={hashVehiculo(c.vehiculo.slug)} target="_blank" rel="noopener noreferrer">
                        {c.vehiculo.titulo}{' '}
                        <span className="sr-only">(se abre en una pestaña nueva)</span>
                      </a>
                      <span className="dato-secundario">{formatoCop(c.vehiculo.precioCop)}</span>
                    </p>
                    <p className="dato-secundario">
                      {[
                        c.vehiculo.modeloAnio !== null ? String(c.vehiculo.modeloAnio) : null,
                        textoUbicacion(c.vehiculo.municipio, c.vehiculo.departamento) || null,
                      ]
                        .filter(Boolean)
                        .join(' · ')}
                    </p>
                    {c.relacionado ? (
                      <p>
                        <span className="estado estado-activa">Ya relacionado</span>
                      </p>
                    ) : (
                      abierto && (
                        <button
                          type="button"
                          className="inv-boton-auto"
                          disabled={bloqueado || lleno}
                          onClick={(e) => {
                            origenDialogo.current = e.currentTarget
                            setDialogo({
                              tipo: 'relacionar',
                              coincidencia: c,
                              enviando: false,
                              erroresNota: [],
                              error: null,
                            })
                          }}
                        >
                          Relacionar <span className="sr-only">{c.vehiculo.titulo}</span>
                        </button>
                      )
                    )}
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </section>

      {dialogo?.tipo === 'relacionar' && (
        <DialogoRelacionar
          titulo={dialogo.coincidencia.vehiculo.titulo}
          enviando={dialogo.enviando}
          erroresNota={dialogo.erroresNota}
          error={dialogo.error}
          onConfirmar={(n) => void confirmarRelacionar(n)}
          onCancelar={cancelar}
        />
      )}
      {dialogo?.tipo === 'retirar' && (
        <DialogoConfirmacion
          titulo="Retirar vehículo"
          descripcion={`¿Retirar ${dialogo.relacionado.titulo} de esta búsqueda? El comprador dejará de verlo y queda en el historial.`}
          textoBoton="Retirar"
          textoEnviando="Retirando..."
          peligro
          motivo="no"
          enviando={dialogo.enviando}
          error={dialogo.error}
          onConfirmar={() => void confirmarRetirar()}
          onCancelar={cancelar}
        />
      )}
    </>
  )
}

type DialogoRelacionarProps = {
  titulo: string
  enviando: boolean
  erroresNota: string[]
  error: DatosAviso | null
  onConfirmar: (nota: string) => void
  onCancelar: () => void
}

function DialogoRelacionar({ titulo, enviando, erroresNota, error, onConfirmar, onCancelar }: DialogoRelacionarProps) {
  const [nota, setNota] = useState('')
  const [locales, setLocales] = useState<string[] | null>(null)
  const [vistos, setVistos] = useState(erroresNota)
  if (vistos !== erroresNota) {
    setVistos(erroresNota)
    setLocales(null)
  }
  const errores = locales ?? erroresNota

  function confirmar() {
    if (nota.trim().length > NOTA_RELACION_MAX) {
      setLocales([MENSAJE_NOTA_RELACION])
      window.setTimeout(() => document.getElementById('busqueda-relacionar-nota')?.focus(), 0)
      return
    }
    onConfirmar(nota)
  }

  return (
    <DialogoConfirmacion
      titulo="Relacionar vehículo"
      descripcion={`¿Relacionar ${titulo} con esta búsqueda? El comprador con cuenta lo verá como recomendación.`}
      textoBoton="Relacionar"
      textoEnviando="Relacionando..."
      motivo="no"
      contenido={
        <CampoAreaTexto
          id="busqueda-relacionar-nota"
          etiqueta="Nota interna (opcional)"
          valor={nota}
          onCambio={(v) => {
            setNota(v)
            if (errores.length > 0) setLocales([])
          }}
          errores={errores}
          maxLength={NOTA_RELACION_MAX}
          filas={3}
          ayuda="Hasta 300 caracteres. El comprador no la ve."
          contador
          requerido={false}
          deshabilitado={enviando}
        />
      }
      enviando={enviando}
      error={error}
      onConfirmar={confirmar}
      onCancelar={onCancelar}
    />
  )
}
