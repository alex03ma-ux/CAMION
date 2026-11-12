// P5 — Detalle de un contacto (#/contactos/<id>): datos del visitante (solo lectura, CA-P5-43), vehículo con su
// estado actual (CA-P5-49), historial cronológico (CA-P5-42), notas (CA-P5-37), cierre con resultado (CA-P5-39)
// y, solo para el administrador, reabrir (CA-P5-41), reasignar (CA-P5-44) y la prueba de autorización (CA-P5-16).
// P5b: un aviso no tiene vehículo ("Sin vehículo asociado"), muestra la descripción o el tipo de vehículo, ofrece
// sus propios resultados de cierre (CA-P5b-41) y nunca se reasigna (D-52 A, CA-P5b-43).
// P6: créditos y búsquedas muestran Registrado / En gestión / Cerrado (D-57 a A) y sus resultados (D-57 b A); el
// crédito, su vehículo con precio y enlace si sigue visible (CA-P6-41); la búsqueda, sus criterios, vehículos
// relacionados y coincidencias (CA-P6-42, 50 a 56). Nunca se reasignan (D-58 A) ni muestran datos financieros (D-55 B).
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { nombreTipoVehiculo } from '../api/avisos'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import type { ResumenAsesor } from '../api/inventario'
import {
  agregarNota,
  AYUDA_NOTA_SIN_FINANCIEROS,
  cerrarLead,
  erroresDeCampo,
  esAviso,
  esSolicitudGestion,
  estadoMostrado,
  formatearFechaHora,
  MENSAJE_DESTINO,
  MENSAJE_RESULTADO,
  nombreEstadoPublicacionLead,
  nombreEstadoVehiculo,
  nombreEvento,
  nombreFormulario,
  nombreOrigen,
  nombreResponsable,
  nombreResultado,
  obtenerLead,
  reabrirLead,
  reasignarLead,
  RESPONSABLE_ADMINISTRACION,
  resultadosCierrePara,
  TEXTO_MAX,
  validarMotivoLead,
  validarNota,
  validarObservacion,
  type EventoLead,
  type LeadDetalle,
  type ResultadoCierre,
} from '../api/leads'
import { formatoCop } from '../catalogo/formato'
import { HASH_CONTACTOS, hashVehiculo, navegar } from '../rutas'
import { Aviso, type DatosAviso } from './Aviso'
import { BusquedaAdmin } from './BusquedaAdmin'
import { CampoAreaTexto, CampoSelect } from './Campo'
import { DialogoConfirmacion } from './DialogoAccion'
import { NotaObligatorio } from './NotaObligatorio'

export type VehiculoFiltro = { vehiculoId: number; titulo: string }

type DetalleContactoProps = {
  token: string
  id: number
  esAdmin: boolean
  /** Asesores activos (solo administrador) para reasignar. */
  asesores: readonly ResumenAsesor[]
  onSesionTerminada: () => void
  /** "Ver todos los contactos de este vehículo": filtra la lista y vuelve a ella. */
  onFiltrarVehiculo: (v: VehiculoFiltro) => void
}

type Estado =
  | { tipo: 'cargando' }
  | { tipo: 'error'; aviso: DatosAviso; noEncontrado: boolean }
  | { tipo: 'ok'; lead: LeadDetalle }

type Dialogo =
  | { tipo: 'cerrar'; enviando: boolean; erroresResultado: string[]; erroresObservacion: string[]; error: DatosAviso | null }
  | { tipo: 'reabrir'; enviando: boolean; erroresMotivo: string[]; error: DatosAviso | null }
  | { tipo: 'reasignar'; enviando: boolean; erroresMotivo: string[]; erroresDestino: string[]; error: DatosAviso | null }

const SIN_ERRORES: string[] = []

function mensajeDe(error: unknown): string {
  return error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO
}

export function DetalleContacto({
  token,
  id,
  esAdmin,
  asesores,
  onSesionTerminada,
  onFiltrarVehiculo,
}: DetalleContactoProps) {
  const [estado, setEstado] = useState<Estado>({ tipo: 'cargando' })
  const [recarga, setRecarga] = useState(0)
  const [resultado, setResultado] = useState<DatosAviso | null>(null)
  const [nota, setNota] = useState('')
  const [erroresNota, setErroresNota] = useState<string[]>([])
  const [enviandoNota, setEnviandoNota] = useState(false)
  const [dialogo, setDialogo] = useState<Dialogo | null>(null)
  const refResultado = useRef<HTMLDivElement>(null)
  const refNota = useRef<HTMLTextAreaElement>(null)
  const origenDialogo = useRef<HTMLButtonElement | null>(null)

  const alError401 = useCallback(
    (error: unknown): boolean => {
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
    obtenerLead(token, id, control.signal)
      .then((lead) => setEstado({ tipo: 'ok', lead }))
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        if (alError401(error)) return
        setEstado({
          tipo: 'error',
          aviso: { tipo: 'error', texto: mensajeDe(error) },
          noEncontrado: error instanceof ApiError && (error.status === 404 || error.status === 403),
        })
      })
    return () => control.abort()
  }, [token, id, recarga, alError401])

  function anunciar(aviso: DatosAviso) {
    setResultado(aviso)
    window.setTimeout(() => refResultado.current?.focus(), 0)
  }

  /** 403, 404 y 409: se muestra el mensaje de la API y se vuelve a leer el contacto (CA-P5-57). */
  function errorDeEstado(error: unknown): boolean {
    if (error instanceof ApiError && [403, 404, 409].includes(error.status)) {
      setDialogo(null)
      origenDialogo.current = null
      anunciar({ tipo: 'error', texto: error.mensaje })
      setRecarga((r) => r + 1)
      return true
    }
    return false
  }

  function terminar(lead: LeadDetalle, texto: string) {
    setEstado({ tipo: 'ok', lead })
    setDialogo(null)
    origenDialogo.current = null
    anunciar({ tipo: 'exito', texto })
  }

  async function guardarNota(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (enviandoNota) return
    setResultado(null)
    const locales = validarNota(nota)
    setErroresNota(locales)
    if (locales.length > 0) {
      refNota.current?.focus()
      return
    }
    setEnviandoNota(true)
    try {
      const lead = await agregarNota(token, id, nota.trim())
      setNota('')
      terminar(lead, 'Nota guardada en el historial.')
    } catch (error) {
      if (alError401(error)) return
      if (error instanceof ApiError && error.status === 400) {
        const propios = erroresDeCampo(error, 'texto')
        setErroresNota(propios.length > 0 ? propios : [error.mensaje])
        refNota.current?.focus()
        return
      }
      if (errorDeEstado(error)) return
      anunciar({ tipo: 'error', texto: mensajeDe(error) })
    } finally {
      setEnviandoNota(false)
    }
  }

  function abrir(tipo: Dialogo['tipo'], boton: HTMLButtonElement) {
    origenDialogo.current = boton
    setResultado(null)
    if (tipo === 'cerrar') {
      setDialogo({ tipo, enviando: false, erroresResultado: SIN_ERRORES, erroresObservacion: SIN_ERRORES, error: null })
    } else if (tipo === 'reabrir') {
      setDialogo({ tipo, enviando: false, erroresMotivo: SIN_ERRORES, error: null })
    } else {
      setDialogo({ tipo, enviando: false, erroresMotivo: SIN_ERRORES, erroresDestino: SIN_ERRORES, error: null })
    }
  }

  function cancelar() {
    setDialogo(null)
    const boton = origenDialogo.current
    origenDialogo.current = null
    window.setTimeout(() => boton?.focus(), 0)
  }

  /** Error dentro del diálogo (400 o sin conexión): no se cierra. */
  function errorEnDialogo(cambios: Partial<Dialogo>) {
    setDialogo((d) => (d ? ({ ...d, enviando: false, error: null, ...cambios } as Dialogo) : d))
  }

  async function confirmarCerrar(resultadoCierre: string, observacion: string) {
    if (!dialogo || dialogo.tipo !== 'cerrar' || dialogo.enviando) return
    setDialogo({ ...dialogo, enviando: true, error: null, erroresResultado: SIN_ERRORES, erroresObservacion: SIN_ERRORES })
    try {
      const lead = await cerrarLead(token, id, resultadoCierre, observacion)
      terminar(lead, `Contacto cerrado: ${nombreResultado(resultadoCierre)}.`)
    } catch (error) {
      if (alError401(error)) {
        setDialogo(null)
        return
      }
      if (error instanceof ApiError && error.status === 400) {
        const r = erroresDeCampo(error, 'resultado')
        const o = erroresDeCampo(error, 'observacion')
        errorEnDialogo({
          erroresResultado: r,
          erroresObservacion: o,
          error: r.length === 0 && o.length === 0 ? { tipo: 'error', texto: error.mensaje } : null,
        })
        return
      }
      if (errorDeEstado(error)) return
      errorEnDialogo({ error: { tipo: 'error', texto: mensajeDe(error) } })
    }
  }

  async function confirmarReabrir(motivo: string) {
    if (!dialogo || dialogo.tipo !== 'reabrir' || dialogo.enviando) return
    setDialogo({ ...dialogo, enviando: true, error: null, erroresMotivo: SIN_ERRORES })
    try {
      const lead = await reabrirLead(token, id, motivo)
      terminar(lead, 'Contacto reabierto. Vuelve a estar pendiente de gestión.')
    } catch (error) {
      if (alError401(error)) {
        setDialogo(null)
        return
      }
      if (error instanceof ApiError && error.status === 400) {
        const m = erroresDeCampo(error, 'motivo')
        errorEnDialogo({ erroresMotivo: m.length > 0 ? m : [error.mensaje] })
        return
      }
      if (errorDeEstado(error)) return
      errorEnDialogo({ error: { tipo: 'error', texto: mensajeDe(error) } })
    }
  }

  async function confirmarReasignar(destino: string, motivo: string) {
    if (!dialogo || dialogo.tipo !== 'reasignar' || dialogo.enviando) return
    setDialogo({ ...dialogo, enviando: true, error: null, erroresMotivo: SIN_ERRORES, erroresDestino: SIN_ERRORES })
    try {
      const lead = await reasignarLead(token, id, destino, motivo)
      terminar(lead, `Contacto reasignado a ${nombreResponsable(lead.responsable)}.`)
    } catch (error) {
      if (alError401(error)) {
        setDialogo(null)
        return
      }
      if (error instanceof ApiError && error.status === 400) {
        const a = [...erroresDeCampo(error, 'asesorId'), ...erroresDeCampo(error, 'administracion')]
        const m = erroresDeCampo(error, 'motivo')
        errorEnDialogo({
          erroresDestino: a,
          erroresMotivo: m,
          error: a.length === 0 && m.length === 0 ? { tipo: 'error', texto: error.mensaje } : null,
        })
        return
      }
      if (errorDeEstado(error)) return
      errorEnDialogo({ error: { tipo: 'error', texto: mensajeDe(error) } })
    }
  }

  const lead = estado.tipo === 'ok' ? estado.lead : null
  const abierto = lead?.estado === 'ABIERTO'
  const ocupado = dialogo !== null || enviandoNota
  const aviso = lead !== null && esAviso(lead.origen)
  const vehiculo = lead?.vehiculo ?? null
  const esBusqueda = lead?.origen === 'BUSQUEDA'
  // P6 (CA-P6-41): el detalle dice si el vehículo se ve hoy en el portal; si no llega, la regla de P5.
  const vehiculoVisible = vehiculo !== null && (vehiculo.visible ?? vehiculo.estadoPublicacion === 'PUBLICADA')
  const tituloMensaje =
    lead?.origen === 'VENDE_TU_CAMION'
      ? 'Descripción del vehículo'
      : lead?.origen === 'CREDITO'
        ? 'Comentario'
        : lead?.origen === 'BUSQUEDA'
          ? 'Descripción de lo que busca'
          : 'Mensaje'

  return (
    <div className="cat-pagina inv-pagina contactos">
      <p className="inv-volver">
        <a href={HASH_CONTACTOS}>Volver a la lista de contactos</a>
      </p>
      <h1 tabIndex={-1} className="inv-titulo">
        {lead ? `${aviso ? 'Solicitud' : 'Contacto'} de ${lead.nombre || 'sin nombre'}` : 'Contacto'}
      </h1>

      {estado.tipo === 'cargando' && <p role="status">Cargando contacto...</p>}
      {estado.tipo === 'error' && (
        <>
          <Aviso aviso={estado.aviso} />
          {!estado.noEncontrado && (
            <button
              type="button"
              className="secondary-button inv-boton-auto"
              onClick={() => {
                setEstado({ tipo: 'cargando' })
                setRecarga((r) => r + 1)
              }}
            >
              Reintentar
            </button>
          )}
        </>
      )}

      <div ref={refResultado} tabIndex={-1} className="contacto-aviso">
        <Aviso aviso={resultado} />
      </div>

      {lead && (
        <>
          <section className="inv-resumen" aria-labelledby="lead-datos-titulo">
            <h2 id="lead-datos-titulo" className="inv-resumen-titulo">
              Datos del contacto
            </h2>
            <p>
              <span className={`estado ${abierto ? 'estado-pendiente' : 'estado-activa'}`}>
                <span className="sr-only">Estado: </span>
                {estadoMostrado(lead)}
              </span>
            </p>
            <dl className="inv-datos">
              {esAdmin && (
                <div>
                  <dt>Origen</dt>
                  <dd>{nombreOrigen(lead.origen)}</dd>
                </div>
              )}
              {lead.origen === 'SEGURO_TODO_RIESGO' && (
                <div>
                  <dt>Tipo de vehículo</dt>
                  <dd>{lead.tipoVehiculo ? nombreTipoVehiculo(lead.tipoVehiculo) : '—'}</dd>
                </div>
              )}
              <div>
                <dt>Nombre</dt>
                <dd>{lead.nombre || '—'}</dd>
              </div>
              <div>
                <dt>Teléfono</dt>
                <dd>
                  <a href={`tel:${lead.telefono}`}>{lead.telefono}</a>
                </dd>
              </div>
              <div>
                <dt>Correo</dt>
                <dd>{lead.correo ? <a href={`mailto:${lead.correo}`}>{lead.correo}</a> : 'No lo dejó'}</dd>
              </div>
              <div>
                <dt>Recibido</dt>
                <dd>{formatearFechaHora(lead.creadoEn)}</dd>
              </div>
              <div>
                <dt>Responsable</dt>
                <dd>{nombreResponsable(lead.responsable)}</dd>
              </div>
              {!abierto && lead.resultadoCierre && (
                <div>
                  <dt>Resultado del cierre</dt>
                  <dd>{nombreResultado(lead.resultadoCierre)}</dd>
                </div>
              )}
              {!abierto && lead.cerradoEn && (
                <div>
                  <dt>Cerrado</dt>
                  <dd>
                    {formatearFechaHora(lead.cerradoEn)}
                    {lead.cerradoPor && <span className="dato-secundario">por {lead.cerradoPor}</span>}
                  </dd>
                </div>
              )}
            </dl>
            {!abierto && lead.observacionCierre && (
              <p className="inv-texto-largo">
                <strong>Observación del cierre:</strong> {lead.observacionCierre}
              </p>
            )}
            <h3 className="lead-subtitulo">{tituloMensaje}</h3>
            <p className="lead-mensaje inv-texto-largo">{lead.mensaje || '—'}</p>
            <p className="campo-ayuda">
              Los datos que dejó la persona no se pueden cambiar. Si hay que corregir algo, anótalo como nota.
            </p>
          </section>

          {esBusqueda && (
            <BusquedaAdmin
              token={token}
              lead={lead}
              abierto={abierto}
              ocupado={ocupado}
              onActualizado={terminar}
              onErrorEstado={(error) => {
                if (alError401(error)) return true
                return errorDeEstado(error)
              }}
            />
          )}

          {!esBusqueda && (
          <section className="inv-resumen" aria-labelledby="lead-vehiculo-titulo">
            <h2 id="lead-vehiculo-titulo" className="inv-resumen-titulo">
              Vehículo
            </h2>
            {vehiculo === null ? (
              <p>Sin vehículo asociado</p>
            ) : (
              <>
                <dl className="inv-datos">
                  <div>
                    <dt>Vehículo</dt>
                    <dd>{vehiculo.titulo || '—'}</dd>
                  </div>
                  {vehiculo.precioCop !== null && (
                    <div>
                      <dt>Precio publicado</dt>
                      <dd>{formatoCop(vehiculo.precioCop)}</dd>
                    </div>
                  )}
                  <div>
                    <dt>Estado del vehículo</dt>
                    <dd>{vehiculo.estadoVehiculo ? nombreEstadoVehiculo(vehiculo.estadoVehiculo) : '—'}</dd>
                  </div>
                  <div>
                    <dt>Publicación</dt>
                    <dd>
                      {vehiculo.estadoPublicacion ? nombreEstadoPublicacionLead(vehiculo.estadoPublicacion) : '—'}
                      {vehiculo.visible === false && (
                        <span className="dato-secundario">Ya no se ve en el portal</span>
                      )}
                    </dd>
                  </div>
                </dl>
                <div className="acciones-fila">
                  {vehiculoVisible && vehiculo.slug && (
                    <a
                      className="inv-boton-enlace inv-boton-pequeno inv-boton-secundario"
                      href={hashVehiculo(vehiculo.slug)}
                    >
                      Ver en el portal
                    </a>
                  )}
                  {vehiculo.vehiculoId > 0 && (
                    <button
                      type="button"
                      className="secondary-button"
                      onClick={() => {
                        onFiltrarVehiculo({ vehiculoId: vehiculo.vehiculoId, titulo: vehiculo.titulo })
                        navegar(HASH_CONTACTOS)
                      }}
                    >
                      Ver todos los contactos de este vehículo
                    </button>
                  )}
                </div>
              </>
            )}
          </section>
          )}

          <section className="inv-resumen" aria-labelledby="lead-acciones-titulo">
            <h2 id="lead-acciones-titulo" className="inv-resumen-titulo">
              Seguimiento
            </h2>
            {abierto && (
              <form className="lead-nota-form" onSubmit={guardarNota} noValidate aria-busy={enviandoNota}>
                <NotaObligatorio />
                <CampoAreaTexto
                  ref={refNota}
                  id="lead-nota"
                  etiqueta="Nueva nota"
                  valor={nota}
                  onCambio={(v) => {
                    setNota(v)
                    if (erroresNota.length > 0) setErroresNota(validarNota(v))
                  }}
                  errores={erroresNota}
                  maxLength={TEXTO_MAX}
                  ayuda={
                    esSolicitudGestion(lead.origen)
                      ? `De 5 a 500 caracteres. ${AYUDA_NOTA_SIN_FINANCIEROS}`
                      : 'De 5 a 500 caracteres.'
                  }
                  deshabilitado={enviandoNota}
                />
                <button type="submit" className="inv-boton-auto" disabled={ocupado}>
                  {enviandoNota ? 'Guardando...' : 'Guardar nota'}
                </button>
              </form>
            )}
            {!abierto && <p>Este contacto está cerrado. {esAdmin ? 'Puedes reabrirlo si hace falta.' : ''}</p>}
            {esAdmin && aviso && (
              <p className="campo-ayuda">Esta solicitud la atiende siempre la administración: no se reasigna a un asesor.</p>
            )}
            <div className="acciones-fila lead-acciones">
              {abierto && (
                <button type="button" disabled={ocupado} onClick={(e) => abrir('cerrar', e.currentTarget)}>
                  Cerrar contacto
                </button>
              )}
              {/* D-52 A: un aviso lo atiende siempre la administración; no se ofrece reasignar. */}
              {esAdmin && abierto && !aviso && (
                <button
                  type="button"
                  className="secondary-button"
                  disabled={ocupado}
                  onClick={(e) => abrir('reasignar', e.currentTarget)}
                >
                  Reasignar
                </button>
              )}
              {esAdmin && !abierto && (
                <button
                  type="button"
                  className="secondary-button"
                  disabled={ocupado}
                  onClick={(e) => abrir('reabrir', e.currentTarget)}
                >
                  Reabrir
                </button>
              )}
            </div>
          </section>

          <section className="inv-resumen" aria-labelledby="lead-historial-titulo">
            <h2 id="lead-historial-titulo" className="inv-resumen-titulo">
              Historial
            </h2>
            {lead.eventos.length === 0 ? (
              <p className="admin-vacio">Sin movimientos.</p>
            ) : (
              <ol className="lead-historial">
                {lead.eventos.map((e, i) => (
                  <EventoHistorial key={`${e.fecha}-${i}`} evento={e} />
                ))}
              </ol>
            )}
          </section>

          {esAdmin && lead.consentimientos !== null && (
            <section className="inv-resumen" aria-labelledby="lead-consentimientos-titulo">
              <h2 id="lead-consentimientos-titulo" className="inv-resumen-titulo">
                Autorización de tratamiento de datos
              </h2>
              {lead.consentimientos.length === 0 ? (
                <p className="admin-vacio">No hay autorizaciones registradas.</p>
              ) : (
                <ul className="lead-consentimientos">
                  {lead.consentimientos.map((c, i) => (
                    <li key={`${c.aceptadoEn}-${i}`}>
                      {nombreFormulario(c.formulario)} · versión {c.versionPolitica} · aceptada el{' '}
                      {formatearFechaHora(c.aceptadoEn)}
                    </li>
                  ))}
                </ul>
              )}
            </section>
          )}
        </>
      )}

      {lead && dialogo?.tipo === 'cerrar' && (
        <DialogoCerrarLead
          opciones={resultadosCierrePara(lead.origen)}
          enviando={dialogo.enviando}
          erroresResultado={dialogo.erroresResultado}
          erroresObservacion={dialogo.erroresObservacion}
          error={dialogo.error}
          onConfirmar={(r, o) => void confirmarCerrar(r, o)}
          onCancelar={cancelar}
        />
      )}
      {lead && dialogo?.tipo === 'reabrir' && (
        <DialogoConfirmacion
          titulo="Reabrir contacto"
          descripcion={`¿Reabrir el contacto de ${lead.nombre}? Vuelve a estar pendiente de gestión con el mismo responsable (${nombreResponsable(lead.responsable)}).`}
          textoBoton="Reabrir"
          textoEnviando="Reabriendo..."
          motivo="obligatorio"
          validarMotivo={validarMotivoLead}
          enviando={dialogo.enviando}
          erroresMotivo={dialogo.erroresMotivo}
          error={dialogo.error}
          onConfirmar={(m) => void confirmarReabrir(m)}
          onCancelar={cancelar}
        />
      )}
      {lead && dialogo?.tipo === 'reasignar' && (
        <DialogoReasignarLead
          nombre={lead.nombre}
          responsableActual={lead.responsable.tipo === 'ASESOR' ? lead.responsable.asesorId : RESPONSABLE_ADMINISTRACION}
          nombreActual={nombreResponsable(lead.responsable)}
          asesores={asesores}
          enviando={dialogo.enviando}
          erroresMotivo={dialogo.erroresMotivo}
          erroresDestino={dialogo.erroresDestino}
          error={dialogo.error}
          onConfirmar={(d, m) => void confirmarReasignar(d, m)}
          onCancelar={cancelar}
        />
      )}
    </div>
  )
}

function EventoHistorial({ evento: e }: { evento: EventoLead }) {
  const actor = e.actor.tipo === 'SISTEMA' ? 'Sistema' : e.actor.nombre || 'Usuario'
  const cambio =
    e.tipo === 'REASIGNADO' && e.responsableAnterior
      ? `De ${nombreResponsable(e.responsableAnterior)} a ${nombreResponsable(e.responsableNuevo)}`
      : (e.tipo === 'ASIGNADO' || e.tipo === 'REASIGNADO') && e.responsableNuevo
        ? `A ${nombreResponsable(e.responsableNuevo)}`
        : null
  return (
    <li className="lead-evento">
      <p className="lead-evento-cabecera">
        <strong>{nombreEvento(e.tipo)}</strong>
        {e.automatico && <span> (automático)</span>}
        <span className="dato-secundario">
          <time dateTime={e.fecha}>{formatearFechaHora(e.fecha)}</time> · {actor}
        </span>
      </p>
      {cambio && <p>{cambio}</p>}
      {e.resultado && <p>Resultado: {nombreResultado(e.resultado)}</p>}
      {e.texto && <p className="inv-texto-largo">{e.texto}</p>}
    </li>
  )
}

type DialogoCerrarLeadProps = {
  /** P5b: resultados válidos según el origen (CA-P5b-41). */
  opciones: readonly { valor: ResultadoCierre; nombre: string }[]
  enviando: boolean
  erroresResultado: string[]
  erroresObservacion: string[]
  error: DatosAviso | null
  onConfirmar: (resultado: string, observacion: string) => void
  onCancelar: () => void
}

/** Cerrar con resultado (CA-P5-39): "Otro" exige observación de 5 a 500; los demás la tienen opcional. */
function DialogoCerrarLead({
  opciones,
  enviando,
  erroresResultado,
  erroresObservacion,
  error,
  onConfirmar,
  onCancelar,
}: DialogoCerrarLeadProps) {
  const [resultado, setResultado] = useState('')
  const [observacion, setObservacion] = useState('')
  const [locales, setLocales] = useState<{ resultado: string[]; observacion: string[] } | null>(null)
  const [vistos, setVistos] = useState({ erroresResultado, erroresObservacion })
  if (vistos.erroresResultado !== erroresResultado || vistos.erroresObservacion !== erroresObservacion) {
    setVistos({ erroresResultado, erroresObservacion })
    setLocales(null)
  }
  const errR = locales?.resultado ?? erroresResultado
  const errO = locales?.observacion ?? erroresObservacion
  const obligatoria = resultado === 'OTRO'

  useEffect(() => {
    if (erroresResultado.length > 0) document.getElementById('lead-cerrar-resultado')?.focus()
    else if (erroresObservacion.length > 0) document.getElementById('lead-cerrar-observacion')?.focus()
  }, [erroresResultado, erroresObservacion])

  function confirmar() {
    const r = resultado === '' ? [MENSAJE_RESULTADO] : []
    const o = resultado === '' ? [] : validarObservacion(resultado, observacion)
    if (r.length > 0 || o.length > 0) {
      setLocales({ resultado: r, observacion: o })
      const idFoco = r.length > 0 ? 'lead-cerrar-resultado' : 'lead-cerrar-observacion'
      window.setTimeout(() => document.getElementById(idFoco)?.focus(), 0)
      return
    }
    onConfirmar(resultado, observacion.trim())
  }

  return (
    <DialogoConfirmacion
      titulo="Cerrar contacto"
      descripcion="Elige cómo terminó la gestión. El cierre queda en el historial."
      textoBoton="Cerrar contacto"
      textoEnviando="Cerrando..."
      motivo="no"
      contenido={
        <>
          <NotaObligatorio />
          <CampoSelect
            id="lead-cerrar-resultado"
            etiqueta="Resultado"
            valor={resultado}
            onCambio={(v) => {
              setResultado(v)
              if (locales) setLocales({ resultado: [], observacion: [] })
            }}
            opciones={opciones}
            textoVacio="Elige el resultado"
            errores={errR}
            deshabilitado={enviando}
          />
          <CampoAreaTexto
            id="lead-cerrar-observacion"
            etiqueta={obligatoria ? 'Observación' : 'Observación (opcional)'}
            valor={observacion}
            onCambio={(v) => {
              setObservacion(v)
              if (locales) setLocales({ resultado: locales.resultado, observacion: [] })
            }}
            errores={errO}
            maxLength={TEXTO_MAX}
            ayuda={obligatoria ? 'De 5 a 500 caracteres.' : 'Hasta 500 caracteres.'}
            requerido={obligatoria}
            deshabilitado={enviando}
          />
        </>
      }
      enviando={enviando}
      error={error}
      onConfirmar={confirmar}
      onCancelar={onCancelar}
    />
  )
}

type DialogoReasignarLeadProps = {
  nombre: string
  /** perfiles_asesor.id del responsable actual, o 'ADMINISTRACION'. */
  responsableActual: string | null
  nombreActual: string
  asesores: readonly ResumenAsesor[]
  enviando: boolean
  erroresMotivo: string[]
  erroresDestino: string[]
  error: DatosAviso | null
  onConfirmar: (destino: string, motivo: string) => void
  onCancelar: () => void
}

/** Reasignar a un asesor o a Administración, con motivo obligatorio (D-7, CA-P5-44). */
function DialogoReasignarLead({
  nombre,
  responsableActual,
  nombreActual,
  asesores,
  enviando,
  erroresMotivo,
  erroresDestino,
  error,
  onConfirmar,
  onCancelar,
}: DialogoReasignarLeadProps) {
  const [destino, setDestino] = useState('')
  const [locales, setLocales] = useState<string[] | null>(null)
  const [vistos, setVistos] = useState(erroresDestino)
  if (vistos !== erroresDestino) {
    setVistos(erroresDestino)
    setLocales(null)
  }
  const errores = locales ?? erroresDestino
  const opciones = [
    { valor: RESPONSABLE_ADMINISTRACION, nombre: 'Administración' },
    ...asesores.map((a) => ({ valor: a.id, nombre: a.nombre || `Asesor ${a.id}` })),
  ].filter((o) => o.valor !== responsableActual)

  useEffect(() => {
    if (erroresDestino.length > 0) document.getElementById('lead-reasignar-destino')?.focus()
  }, [erroresDestino])

  function confirmar(motivo: string) {
    if (destino === '') {
      setLocales([MENSAJE_DESTINO])
      window.setTimeout(() => document.getElementById('lead-reasignar-destino')?.focus(), 0)
      return
    }
    onConfirmar(destino, motivo)
  }

  return (
    <DialogoConfirmacion
      titulo="Reasignar contacto"
      descripcion={`El contacto de ${nombre} pasará al responsable que elijas (hoy: ${nombreActual}). El cambio queda en el historial y en la auditoría.`}
      textoBoton="Reasignar"
      textoEnviando="Reasignando..."
      motivo="obligatorio"
      validarMotivo={validarMotivoLead}
      contenido={
        <CampoSelect
          id="lead-reasignar-destino"
          etiqueta="Nuevo responsable"
          valor={destino}
          onCambio={(v) => {
            setDestino(v)
            if (errores.length > 0) setLocales([])
          }}
          opciones={opciones}
          textoVacio="Elige el responsable"
          errores={errores}
          deshabilitado={enviando}
        />
      }
      enviando={enviando}
      erroresMotivo={erroresMotivo}
      error={error}
      onConfirmar={confirmar}
      onCancelar={onCancelar}
    />
  )
}
