// P7 (D-61 B; CA-P7-30, CA-P7-31) — Pagos de una suscripción, solo para el administrador: lista con su total,
// "Registrar pago" (pago posterior) y "Anular" con motivo en el diálogo de confirmación. La API vuelve a
// comprobarlo todo (SoloAdministrador; 404 si no existe; 409 si el pago ya está anulado).
// D-67: anular deshace lo que el pago dio. Antes de abrir el diálogo se consulta el efecto
// (GET /efecto-anulacion) y el diálogo dice qué pasará; si está bloqueado, se muestra el motivo y no se ofrece.
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import {
  anularPago,
  AVISO_QUEDARA_VENCIDA,
  MENSAJE_ANULACION_BLOQUEADA,
  obtenerEfectoAnulacion,
  textoEfectoAnulacion,
  type EfectoAnulacion,
  erroresPagoDe,
  formularioPagoInicial,
  hayErroresPago,
  listarPagos,
  MENSAJE_PAGO_SIN_PRECIO,
  nombreConceptoPago,
  nombreEstadoPago,
  nombreMedioPago,
  pagoDesdeFormulario,
  registrarPago,
  validarMotivoAnulacion,
  validarPago,
  type ErroresPago,
  type FormularioPago,
  type ListaPagos,
  type PagoItem,
} from '../api/pagos'
import { formatearFechaSuscripcion, type Suscripcion } from '../api/suscripciones'
import { formatearFechaHora } from '../api/leads'
import { formatoCop } from '../catalogo/formato'
import { Aviso, type DatosAviso } from './Aviso'
import { CamposPago } from './CamposPago'
import { DialogoConfirmacion } from './DialogoAccion'
import { NotaObligatorio } from './NotaObligatorio'

type PagosSuscripcionProps = {
  token: string
  suscripcion: Suscripcion
  /** Precio del plan de la suscripción; null si no se conoce (plan inactivo). */
  precioPlan: number | null
  onSesionTerminada: () => void
  onCerrar: () => void
  /** D-67: la anulación cambió la suscripción (fecha, estado o plan): hay que recargar la lista. */
  onSuscripcionCambiada?: () => void
}

type Carga = { estado: 'cargando' } | { estado: 'ok'; datos: ListaPagos } | { estado: 'error'; aviso: DatosAviso }

type DialogoAnular = {
  pago: PagoItem
  efecto: EfectoAnulacion
  enviando: boolean
  erroresMotivo: string[]
  error: DatosAviso | null
}

/** Fecha AAAA-MM-DD como fecha local (sin correr el día por la zona horaria). */
function fechaPagoTexto(valor: string | null): string {
  return valor ? formatearFechaSuscripcion(`${valor}T12:00:00`) : '—'
}

export function PagosSuscripcion({
  token,
  suscripcion,
  precioPlan,
  onSesionTerminada,
  onCerrar,
  onSuscripcionCambiada,
}: PagosSuscripcionProps) {
  const [carga, setCarga] = useState<Carga>({ estado: 'cargando' })
  const [recarga, setRecarga] = useState(0)
  const [resultado, setResultado] = useState<DatosAviso | null>(null)
  const [formulario, setFormulario] = useState<FormularioPago | null>(null)
  const [errores, setErrores] = useState<ErroresPago>({})
  const [errorFormulario, setErrorFormulario] = useState<DatosAviso | null>(null)
  const [guardando, setGuardando] = useState(false)
  const [dialogo, setDialogo] = useState<DialogoAnular | null>(null)
  // D-67: pago cuyo efecto se está consultando y pagos que no se pueden anular (con su motivo).
  const [consultando, setConsultando] = useState<number | null>(null)
  const [bloqueados, setBloqueados] = useState<Map<number, string>>(new Map())
  const titulo = useRef<HTMLHeadingElement>(null)
  const botonRegistrar = useRef<HTMLButtonElement>(null)
  const origenDialogo = useRef<HTMLButtonElement | null>(null)
  const sinPrecio = precioPlan === 0
  const idTitulo = `pagos-titulo-${suscripcion.id}`

  useEffect(() => {
    titulo.current?.focus()
  }, [])

  const es401 = useCallback(
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
    setCarga({ estado: 'cargando' })
    listarPagos(token, suscripcion.id, control.signal)
      .then((datos) => setCarga({ estado: 'ok', datos }))
      .catch((error: unknown) => {
        if (control.signal.aborted || es401(error)) return
        setCarga({
          estado: 'error',
          aviso: { tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO },
        })
      })
    return () => control.abort()
  }, [token, suscripcion.id, recarga, es401])

  function abrirFormulario() {
    setResultado(null)
    setErrores({})
    setErrorFormulario(null)
    setFormulario(formularioPagoInicial(precioPlan))
    window.setTimeout(() => document.getElementById(`pago-posterior-${suscripcion.id}-valor`)?.focus(), 0)
  }

  function cerrarFormulario() {
    setFormulario(null)
    setErrores({})
    setErrorFormulario(null)
    window.setTimeout(() => botonRegistrar.current?.focus(), 0)
  }

  async function guardar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (!formulario || guardando) return
    const validacion = validarPago(formulario)
    setErrores(validacion)
    setErrorFormulario(null)
    if (hayErroresPago(validacion)) {
      const primero = (['valorCop', 'fechaPago', 'medio', 'referencia'] as const).find((c) => validacion[c])
      const sufijo = { valorCop: 'valor', fechaPago: 'fecha', medio: 'medio', referencia: 'referencia' }
      if (primero) document.getElementById(`pago-posterior-${suscripcion.id}-${sufijo[primero]}`)?.focus()
      return
    }
    setGuardando(true)
    try {
      await registrarPago(token, suscripcion.id, pagoDesdeFormulario(formulario))
      setGuardando(false)
      setFormulario(null)
      setResultado({ tipo: 'exito', texto: 'Se registró el pago.' })
      setRecarga((r) => r + 1)
      window.setTimeout(() => botonRegistrar.current?.focus(), 0)
    } catch (error) {
      setGuardando(false)
      if (es401(error)) return
      if (error instanceof ApiError && error.status === 400) {
        const porCampo = erroresPagoDe(error)
        setErrores(porCampo)
        setErrorFormulario({
          tipo: 'error',
          texto: porCampo.pago ? porCampo.pago.join(' ') : error.mensaje,
        })
        return
      }
      setErrorFormulario({ tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO })
    }
  }

  async function abrirAnular(pago: PagoItem, boton: HTMLButtonElement) {
    if (consultando !== null) return
    origenDialogo.current = boton
    setResultado(null)
    setConsultando(pago.id)
    try {
      const efecto = await obtenerEfectoAnulacion(token, pago.id)
      setConsultando(null)
      if (efecto.bloqueadoPor !== null) {
        const motivo = efecto.mensaje ?? MENSAJE_ANULACION_BLOQUEADA
        setBloqueados((m) => new Map(m).set(pago.id, motivo))
        setResultado({ tipo: 'error', texto: motivo })
        origenDialogo.current = null
        window.setTimeout(() => titulo.current?.focus(), 0)
        return
      }
      setDialogo({ pago, efecto, enviando: false, erroresMotivo: [], error: null })
    } catch (error) {
      setConsultando(null)
      origenDialogo.current = null
      if (es401(error)) return
      setResultado({ tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO })
      if (error instanceof ApiError && error.status === 404) setRecarga((r) => r + 1)
    }
  }

  function cerrarAnular() {
    setDialogo(null)
    const boton = origenDialogo.current
    origenDialogo.current = null
    window.setTimeout(() => boton?.focus(), 0)
  }

  async function confirmarAnular(motivo: string) {
    if (!dialogo || dialogo.enviando) return
    setDialogo({ ...dialogo, enviando: true, erroresMotivo: [], error: null })
    try {
      await anularPago(token, dialogo.pago.id, motivo)
      setDialogo(null)
      origenDialogo.current = null
      setResultado({
        tipo: 'exito',
        texto: `Se anuló el pago. Queda en la lista como "Anulado". ${textoEfectoAnulacion(dialogo.efecto)}`,
      })
      setRecarga((r) => r + 1)
      // D-67: la suscripción pudo cambiar de fecha, estado o plan.
      if (dialogo.efecto.tipo !== 'SOLO_PAGO') onSuscripcionCambiada?.()
      window.setTimeout(() => titulo.current?.focus(), 0)
    } catch (error) {
      if (es401(error)) {
        setDialogo(null)
        return
      }
      if (error instanceof ApiError && error.status === 400) {
        const motivoErr = Object.entries(error.errores)
          .filter(([c]) => c.toLowerCase() === 'motivo')
          .flatMap(([, m]) => m)
        setDialogo((d) => d && { ...d, enviando: false, erroresMotivo: motivoErr, error: motivoErr.length ? null : { tipo: 'error', texto: error.mensaje } })
        return
      }
      if (error instanceof ApiError && (error.status === 404 || error.status === 409 || error.status === 403)) {
        // El pago cambió (ya anulado o no existe): se cierra y se recarga la lista.
        setDialogo(null)
        origenDialogo.current = null
        setResultado({ tipo: 'error', texto: error.mensaje })
        if (error.status === 409) setBloqueados((m) => new Map(m).set(dialogo.pago.id, error.mensaje))
        setRecarga((r) => r + 1)
        window.setTimeout(() => titulo.current?.focus(), 0)
        return
      }
      const texto = error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO
      setDialogo((d) => d && { ...d, enviando: false, error: { tipo: 'error', texto } })
    }
  }

  const nombre = suscripcion.asesor.nombre || 'el asesor'

  return (
    <section className="pagos-suscripcion" aria-labelledby={idTitulo}>
      <h3 id={idTitulo} ref={titulo} tabIndex={-1} className="admin-subtitulo">
        Pagos de {nombre}
        {suscripcion.plan.nombre ? ` (${suscripcion.plan.nombre})` : ''}
      </h3>
      <p className="form-ayuda">
        Pagos recibidos fuera del portal y anotados por la administración. Un pago mal escrito se anula con un
        motivo (no se borra) y se puede anotar de nuevo.
      </p>
      <Aviso aviso={resultado} />
      {carga.estado === 'cargando' && <p role="status">Cargando pagos...</p>}
      {carga.estado === 'error' && (
        <>
          <Aviso aviso={carga.aviso} />
          <button type="button" className="secondary-button inv-boton-auto" onClick={() => setRecarga((r) => r + 1)}>
            Reintentar
          </button>
        </>
      )}
      {carga.estado === 'ok' && (
        <>
          <p className="pagos-total">
            <strong>Total pagado: {formatoCop(carga.datos.totalPagadoCop)}</strong>{' '}
            <span className="dato-secundario">(sin contar los pagos anulados)</span>
          </p>
          {carga.datos.items.length === 0 ? (
            <p className="admin-vacio">Esta suscripción no tiene pagos registrados.</p>
          ) : (
            <div className="tabla-contenedor" role="region" aria-labelledby={`${idTitulo}-tabla`} tabIndex={0}>
              <table className="tabla-usuarios tabla-admin">
                <caption id={`${idTitulo}-tabla`} className="sr-only">
                  Pagos de la suscripción de {nombre}
                </caption>
                <thead>
                  <tr>
                    <th scope="col">Fecha</th>
                    <th scope="col">Valor</th>
                    <th scope="col">Medio</th>
                    <th scope="col">Referencia</th>
                    <th scope="col">Concepto</th>
                    <th scope="col">Estado</th>
                    <th scope="col">Registrado por</th>
                    <th scope="col">Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {carga.datos.items.map((p) => {
                    const anulado = p.estado === 'ANULADO'
                    return (
                      <tr key={p.id}>
                        <th scope="row">{fechaPagoTexto(p.fechaPago)}</th>
                        <td>{anulado ? <s>{formatoCop(p.valorCop)}</s> : formatoCop(p.valorCop)}</td>
                        <td>{nombreMedioPago(p.medio)}</td>
                        <td className="inv-texto-largo">{p.referencia ?? '—'}</td>
                        <td>{nombreConceptoPago(p.concepto)}</td>
                        <td>
                          <span className={`estado ${anulado ? 'estado-bloqueada' : 'estado-activa'}`}>
                            {nombreEstadoPago(p.estado)}
                          </span>
                          {anulado && (
                            <span className="dato-secundario inv-texto-largo">
                              {p.anuladoPor ?? 'Cuenta eliminada'}, {formatearFechaHora(p.anuladoEn)}
                              {p.motivoAnulacion ? `. Motivo: ${p.motivoAnulacion}` : ''}
                            </span>
                          )}
                        </td>
                        <td>
                          {p.registradoPor ?? '—'}
                          <span className="dato-secundario">{formatearFechaHora(p.registradoEn)}</span>
                        </td>
                        <td>
                          {anulado ? (
                            <span className="dato-secundario">Sin acciones</span>
                          ) : bloqueados.has(p.id) ? (
                            // D-67: no se ofrece anular; se explica por qué.
                            <span className="dato-secundario inv-texto-largo">
                              No se puede anular: {bloqueados.get(p.id)}
                            </span>
                          ) : (
                            <button
                              type="button"
                              className="boton-peligro"
                              disabled={dialogo !== null || consultando !== null}
                              aria-busy={consultando === p.id}
                              onClick={(e) => void abrirAnular(p, e.currentTarget)}
                            >
                              {consultando === p.id ? 'Consultando...' : 'Anular'}{' '}
                              <span className="sr-only">el pago de {formatoCop(p.valorCop)} del {fechaPagoTexto(p.fechaPago)}</span>
                            </button>
                          )}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}

      {sinPrecio ? (
        <p className="form-ayuda">{MENSAJE_PAGO_SIN_PRECIO}</p>
      ) : formulario === null ? (
        <div className="admin-botones">
          <button ref={botonRegistrar} type="button" onClick={abrirFormulario}>
            Registrar pago
          </button>
          <button type="button" className="secondary-button" onClick={onCerrar}>
            Cerrar pagos
          </button>
        </div>
      ) : (
        <form className="admin-form" onSubmit={(e) => void guardar(e)} noValidate aria-busy={guardando}>
          <fieldset className="pago-fieldset">
            <legend>Pago recibido</legend>
            <NotaObligatorio />
            <CamposPago
              id={`pago-posterior-${suscripcion.id}`}
              valor={formulario}
              onCambio={(campo, v) => {
                setFormulario((f) => f && { ...f, [campo]: v })
                setErrores((e) => ({ ...e, [campo]: undefined, ...(campo === 'medio' ? { referencia: undefined } : {}) }))
              }}
              errores={errores}
              precioPlan={precioPlan}
              deshabilitado={guardando}
            />
          </fieldset>
          <Aviso aviso={errorFormulario} />
          <div className="admin-botones">
            <button type="submit" disabled={guardando}>
              {guardando ? 'Guardando...' : 'Guardar pago'}
            </button>
            <button type="button" className="secondary-button" onClick={cerrarFormulario} disabled={guardando}>
              Cancelar
            </button>
          </div>
        </form>
      )}
      {sinPrecio && (
        <div className="admin-botones">
          <button type="button" className="secondary-button" onClick={onCerrar}>
            Cerrar pagos
          </button>
        </div>
      )}

      {dialogo && (
        <DialogoConfirmacion
          titulo="Anular pago"
          descripcion={`¿Anular el pago de ${formatoCop(dialogo.pago.valorCop)} del ${fechaPagoTexto(dialogo.pago.fechaPago)}? ${textoEfectoAnulacion(dialogo.efecto)} El pago quedará en la lista como "Anulado" y no se suma al total.`}
          contenido={
            dialogo.efecto.quedaraVencida ? (
              <Aviso aviso={{ tipo: 'advertencia', texto: AVISO_QUEDARA_VENCIDA }} />
            ) : undefined
          }
          textoBoton="Anular pago"
          textoEnviando="Anulando..."
          peligro
          motivo="obligatorio"
          validarMotivo={validarMotivoAnulacion}
          enviando={dialogo.enviando}
          erroresMotivo={dialogo.erroresMotivo}
          error={dialogo.error}
          onConfirmar={(m) => void confirmarAnular(m)}
          onCancelar={cerrarAnular}
        />
      )}
    </section>
  )
}
