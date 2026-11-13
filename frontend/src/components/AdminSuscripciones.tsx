// P4p — Suscripciones de los asesores (HU-066 a HU-069, RN-SU-001 a RN-SU-005; D-4, D-26, D-35, D-36).
// El administrador asigna, renueva, cambia de plan, suspende, reactiva y cancela. La interfaz solo
// ofrece las acciones válidas del contrato; la API vuelve a comprobarlas (409 si no se puede).
// P7 (D-61 B; CA-P7-27 a 31): al asignar, renovar o cambiar de plan con precio se ofrece "Pago recibido"
// (opcional); cada fila tiene "Pagos" con la lista, el total, "Registrar pago" y "Anular".
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { obtenerCatalogosInventario, type ResumenAsesor } from '../api/inventario'
import {
  erroresPagoDe,
  formularioPagoInicial,
  hayErroresPago,
  pagoDesdeFormulario,
  validarPago,
  type CampoPago,
  type ErroresPago,
  type FormularioPago,
} from '../api/pagos'
import { listarPlanes, type Plan } from '../api/planes'
import {
  accionesSuscripcion,
  asignarPlan,
  ejecutarAccionSuscripcion,
  ESTADOS_FILTRO,
  formatearFechaSuscripcion,
  type EstadoSuscripcion,
  leerVistaSuscripciones,
  listarSuscripciones,
  NOMBRES_VISTA_SUSCRIPCIONES,
  MENSAJE_ELIGE_ASESOR,
  MENSAJE_ELIGE_PLAN,
  motivoObligatorioSuscripcion,
  nombreEstadoSuscripcion,
  TAMANO_PAGINA_SUSCRIPCIONES,
  textoDiasRestantes,
  validarMotivoSuscripcion,
  type AccionSuscripcion,
  type FiltrosSuscripciones,
  type ListaSuscripciones,
  type Suscripcion,
} from '../api/suscripciones'
import { formatoMiles } from '../catalogo/formato'
import { Aviso, type DatosAviso } from './Aviso'
import { CampoSelect } from './Campo'
import { CamposPago } from './CamposPago'
import { DialogoConfirmacion } from './DialogoAccion'
import { NotaObligatorio } from './NotaObligatorio'
import { PagosSuscripcion } from './PagosSuscripcion'
import { SeccionActividad } from './Actividad'
import { hashSeccionAdmin, navegar } from '../rutas'

type AdminSuscripcionesProps = {
  token: string
  onSesionTerminada: () => void
  /** P7 (CA-P7-46): filtros iniciales desde el panel, p. ej. `estado=VENCIDA`. */
  consulta?: string
}

/** Filtros iniciales a partir de la consulta del hash; ignora valores desconocidos. */
export function filtrosSuscripcionesDesdeConsulta(consulta: string | undefined): FiltrosSuscripciones {
  const p = new URLSearchParams(consulta ?? '')
  const estado = (p.get('estado') ?? '').toUpperCase()
  const asesorId = p.get('asesorId') ?? ''
  return {
    ...FILTROS_INICIALES,
    estado: ESTADOS_FILTRO.includes(estado as EstadoSuscripcion) ? estado : '',
    asesorId: /^\d{1,15}$/.test(asesorId) ? asesorId : '',
    // P7 (O-5): vista=porVencer o vista=vencidasSinRenovar, desde el panel Inicio.
    vista: leerVistaSuscripciones(p.get('vista')),
  }
}

/** Estado del bloque "Pago recibido" (asignar, renovar, cambiar de plan). */
type EstadoPago = {
  /** El administrador decide si anota el pago ahora (opcional, visible si el plan tiene precio). */
  registrar: boolean
  datos: FormularioPago
  errores: ErroresPago
}

function pagoInicial(precio: number | null): EstadoPago {
  return { registrar: true, datos: formularioPagoInicial(precio), errores: {} }
}

/** El bloque de pago se ofrece si el plan tiene precio (o si no se conoce: la API decide). */
function ofrecePago(precio: number | null | undefined): boolean {
  return precio === null || (precio !== undefined && precio > 0)
}

const SUFIJOS_PAGO: Record<CampoPago, string> = {
  valorCop: 'valor',
  fechaPago: 'fecha',
  medio: 'medio',
  referencia: 'referencia',
}

function enfocarPrimerErrorPago(prefijo: string, errores: ErroresPago) {
  const campo = (['valorCop', 'fechaPago', 'medio', 'referencia'] as const).find((c) => errores[c]?.length)
  if (campo) document.getElementById(`${prefijo}-${SUFIJOS_PAGO[campo]}`)?.focus()
}

type BloquePagoProps = {
  id: string
  pago: EstadoPago
  precio: number | null
  deshabilitado: boolean
  onCambio: (pago: EstadoPago) => void
}

/** Sección "Pago recibido" con la casilla para anotarlo ahora o después (D-61 B). */
function BloquePago({ id, pago, precio, deshabilitado, onCambio }: BloquePagoProps) {
  return (
    <fieldset className="pago-fieldset">
      <legend>Pago recibido</legend>
      <div className="campo-casilla">
        <input
          id={`${id}-registrar`}
          type="checkbox"
          checked={pago.registrar}
          onChange={(e) => onCambio({ ...pago, registrar: e.target.checked, errores: {} })}
          disabled={deshabilitado}
          aria-describedby={`${id}-registrar-ayuda`}
        />
        <label htmlFor={`${id}-registrar`}>Anotar ahora el pago recibido</label>
      </div>
      <p id={`${id}-registrar-ayuda`} className="campo-ayuda">
        {pago.registrar
          ? 'Se guarda junto con la suscripción. Si aún no tienes el pago, desmarca la casilla: podrás anotarlo después en "Pagos".'
          : 'No se anotará ningún pago. Podrás registrarlo después en "Pagos" de esta suscripción.'}
      </p>
      {pago.registrar && (
        <CamposPago
          id={id}
          valor={pago.datos}
          onCambio={(campo, v) =>
            onCambio({
              ...pago,
              datos: { ...pago.datos, [campo]: v },
              errores: {
                ...pago.errores,
                [campo]: undefined,
                ...(campo === 'medio' ? { referencia: undefined } : {}),
                pago: undefined,
              },
            })
          }
          errores={pago.errores}
          precioPlan={precio}
          deshabilitado={deshabilitado}
        />
      )}
      {pago.errores.pago && pago.errores.pago.length > 0 && (
        <ul className="campo-errores">
          {pago.errores.pago.map((m) => (
            <li key={m}>{m}</li>
          ))}
        </ul>
      )}
    </fieldset>
  )
}

export const TEXTOS_ACCION_SUSCRIPCION: Record<
  AccionSuscripcion,
  { boton: string; titulo: string; enviando: string; peligro: boolean }
> = {
  renovar: { boton: 'Renovar', titulo: 'Renovar suscripción', enviando: 'Renovando...', peligro: false },
  'cambiar-plan': {
    boton: 'Cambiar plan',
    titulo: 'Cambiar de plan',
    enviando: 'Cambiando...',
    peligro: false,
  },
  suspender: { boton: 'Suspender', titulo: 'Suspender suscripción', enviando: 'Suspendiendo...', peligro: true },
  reactivar: { boton: 'Reactivar', titulo: 'Reactivar suscripción', enviando: 'Reactivando...', peligro: false },
  cancelar: { boton: 'Cancelar suscripción', titulo: 'Cancelar suscripción', enviando: 'Cancelando...', peligro: true },
}

function pregunta(accion: AccionSuscripcion, s: Suscripcion): string {
  const n = s.asesor.nombre || 'este asesor'
  switch (accion) {
    case 'renovar':
      return `¿Renovar la suscripción de ${n}? Se suman los días del plan a la fecha de vencimiento (o desde hoy si ya venció).`
    case 'cambiar-plan':
      return `¿Cambiar el plan de ${n}? La suscripción actual termina hoy y empieza una nueva con el plan elegido.`
    case 'suspender':
      return `¿Suspender la suscripción de ${n}? Sus publicaciones dejarán de verse en el portal.`
    case 'reactivar':
      return `¿Reactivar la suscripción de ${n}? La fecha de vencimiento no cambia.`
    case 'cancelar':
      return `¿Cancelar la suscripción de ${n}? Termina hoy y no se puede deshacer.`
  }
}

function exito(accion: AccionSuscripcion, nombre: string, actualizada: Suscripcion | null): string {
  switch (accion) {
    case 'renovar':
      return actualizada?.finVigencia
        ? `Se renovó la suscripción de ${nombre}. Ahora vence el ${formatearFechaSuscripcion(actualizada.finVigencia)}.`
        : `Se renovó la suscripción de ${nombre}.`
    case 'cambiar-plan':
      return actualizada?.plan.nombre
        ? `Se cambió el plan de ${nombre} a ${actualizada.plan.nombre}.`
        : `Se cambió el plan de ${nombre}.`
    case 'suspender':
      return `Se suspendió la suscripción de ${nombre}.`
    case 'reactivar':
      return `Se reactivó la suscripción de ${nombre}.`
    case 'cancelar':
      return `Se canceló la suscripción de ${nombre}.`
  }
}

/** Clase de color; el estado siempre va también en texto. */
function claseEstado(estado: string): string {
  switch (estado) {
    case 'ACTIVA':
    case 'REACTIVADA':
      return 'estado-activa'
    case 'SUSPENDIDA':
    case 'PENDIENTE':
      return 'estado-pendiente'
    default:
      return 'estado-bloqueada'
  }
}

const FILTROS_INICIALES: FiltrosSuscripciones = {
  asesorId: '',
  estado: '',
  pagina: 1,
  tamano: TAMANO_PAGINA_SUSCRIPCIONES,
}

function erroresDe(error: ApiError, campo: string): string[] {
  return Object.entries(error.errores)
    .filter(([c]) => c.toLowerCase() === campo.toLowerCase())
    .flatMap(([, m]) => m)
}

type Dialogo = {
  suscripcion: Suscripcion
  accion: AccionSuscripcion
  planId: string
  erroresPlan: string[]
  enviando: boolean
  erroresMotivo: string[]
  error: DatosAviso | null
  /** P7: solo renovar y cambiar de plan. */
  pago: EstadoPago
}

export function AdminSuscripciones({ token, onSesionTerminada, consulta }: AdminSuscripcionesProps) {
  const [asesores, setAsesores] = useState<ResumenAsesor[]>([])
  const [planesActivos, setPlanesActivos] = useState<Plan[]>([])
  const [errorOpciones, setErrorOpciones] = useState<DatosAviso | null>(null)

  const [filtros, setFiltros] = useState<FiltrosSuscripciones>(() => filtrosSuscripcionesDesdeConsulta(consulta))
  const [borrador, setBorrador] = useState(() => {
    const f = filtrosSuscripcionesDesdeConsulta(consulta)
    return { asesorId: f.asesorId, estado: f.estado }
  })
  const [lista, setLista] = useState<ListaSuscripciones | null>(null)
  const [cargando, setCargando] = useState(true)
  const [errorCarga, setErrorCarga] = useState<DatosAviso | null>(null)
  const [recarga, setRecarga] = useState(0)
  const [resultado, setResultado] = useState<DatosAviso | null>(null)

  const [asignacion, setAsignacion] = useState({ asesorId: '', planId: '' })
  const [erroresAsignacion, setErroresAsignacion] = useState<{ asesorId?: string[]; planId?: string[] }>({})
  const [avisoAsignacion, setAvisoAsignacion] = useState<DatosAviso | null>(null)
  const [asignando, setAsignando] = useState(false)
  const [pagoAsignacion, setPagoAsignacion] = useState<EstadoPago>(() => pagoInicial(null))

  const [dialogo, setDialogo] = useState<Dialogo | null>(null)
  // P7: suscripción con la sección "Pagos" abierta.
  const [pagosDe, setPagosDe] = useState<Suscripcion | null>(null)
  const origenPagos = useRef<HTMLButtonElement | null>(null)
  // P8 (D-72 A/A): suscripción con la sección "Actividad" abierta.
  const [actividadDe, setActividadDe] = useState<Suscripcion | null>(null)
  const origenActividad = useRef<HTMLButtonElement | null>(null)

  const titulo = useRef<HTMLHeadingElement>(null)
  const tituloLista = useRef<HTMLHeadingElement>(null)
  const origenDialogo = useRef<HTMLButtonElement | null>(null)

  useEffect(() => {
    titulo.current?.focus()
  }, [])

  const tratarError = useCallback(
    (error: unknown): string | null => {
      if (error instanceof ApiError && error.status === 401) {
        onSesionTerminada()
        return null
      }
      return error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO
    },
    [onSesionTerminada],
  )

  // Asesores aprobados (ACTIVA) y planes activos, para asignar y filtrar.
  useEffect(() => {
    const control = new AbortController()
    Promise.all([
      obtenerCatalogosInventario(token, control.signal),
      listarPlanes(token, true, control.signal),
    ])
      .then(([catalogos, planes]) => {
        setAsesores(catalogos.asesores)
        setPlanesActivos(planes.filter((p) => p.activo))
      })
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        const texto = tratarError(error)
        if (texto) {
          setErrorOpciones({
            tipo: 'error',
            texto: `No se pudieron cargar los asesores y los planes. ${texto}`,
          })
        }
      })
    return () => control.abort()
  }, [token, recarga, tratarError])

  useEffect(() => {
    const control = new AbortController()
    setCargando(true)
    setErrorCarga(null)
    listarSuscripciones(token, filtros, control.signal)
      .then((respuesta) => {
        const ultima = Math.max(1, Math.ceil(respuesta.total / Math.max(1, respuesta.tamano)))
        if (respuesta.items.length === 0 && filtros.pagina > ultima) {
          setFiltros((f) => ({ ...f, pagina: ultima }))
          return
        }
        setLista(respuesta)
        setCargando(false)
        // D-67: tras anular un pago, la sección "Pagos" muestra la suscripción ya actualizada.
        setPagosDe((actual) => (actual ? (respuesta.items.find((s) => s.id === actual.id) ?? actual) : actual))
      })
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        setCargando(false)
        const texto = tratarError(error)
        if (texto) {
          setLista(null)
          // P7 (O-5): un 400 de filtros trae el detalle por campo (p. ej. "Vista no válida.").
          const detalle = error instanceof ApiError && error.status === 400 ? Object.values(error.errores).flat() : []
          setErrorCarga({ tipo: 'error', texto: [texto, ...detalle].join(' ') })
        }
      })
    return () => control.abort()
  }, [token, filtros, recarga, tratarError])

  function aplicarFiltros(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    setResultado(null)
    // La vista del panel se conserva y se combina con los filtros elegidos.
    setFiltros((f) => ({ ...FILTROS_INICIALES, asesorId: borrador.asesorId, estado: borrador.estado, vista: f.vista }))
  }

  /** Quita la vista del panel: vuelve a la lista con los demás filtros aplicados. */
  function quitarVista() {
    setResultado(null)
    const hash = hashSeccionAdmin('suscripciones', { asesorId: filtros.asesorId, estado: filtros.estado })
    if (window.location.hash === hash) setFiltros((f) => ({ ...f, vista: '', pagina: 1 }))
    else navegar(hash)
  }

  function irAPagina(pagina: number) {
    setResultado(null)
    setFiltros((f) => ({ ...f, pagina }))
    tituloLista.current?.focus()
  }

  async function asignar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (asignando) return
    setAvisoAsignacion(null)
    setResultado(null)
    const errores: { asesorId?: string[]; planId?: string[] } = {}
    if (!asignacion.asesorId) errores.asesorId = [MENSAJE_ELIGE_ASESOR]
    if (!asignacion.planId) errores.planId = [MENSAJE_ELIGE_PLAN]
    setErroresAsignacion(errores)
    if (errores.asesorId || errores.planId) {
      document.getElementById(errores.asesorId ? 'asignar-asesor' : 'asignar-plan')?.focus()
      return
    }
    const asesor = asesores.find((a) => a.id === asignacion.asesorId)
    const plan = planesActivos.find((p) => String(p.id) === asignacion.planId)
    // P7 (CA-P7-27, 28): pago solo si el plan tiene precio y el administrador lo anota ahora.
    const conPago = ofrecePago(plan ? plan.precioCop : null) && pagoAsignacion.registrar
    if (conPago) {
      const erroresPago = validarPago(pagoAsignacion.datos)
      setPagoAsignacion((p) => ({ ...p, errores: erroresPago }))
      if (hayErroresPago(erroresPago)) {
        enfocarPrimerErrorPago('asignar-pago', erroresPago)
        return
      }
    }
    setAsignando(true)
    try {
      await asignarPlan(
        token,
        asignacion.asesorId,
        Number(asignacion.planId),
        conPago ? pagoDesdeFormulario(pagoAsignacion.datos) : null,
      )
      setAsignando(false)
      setAsignacion({ asesorId: '', planId: '' })
      setPagoAsignacion(pagoInicial(null))
      setAvisoAsignacion({
        tipo: 'exito',
        texto: `Se asignó el plan ${plan?.nombre ?? ''} a ${asesor?.nombre ?? 'el asesor'}.${conPago ? ' Se registró el pago.' : ''}`,
      })
      setRecarga((r) => r + 1)
    } catch (error) {
      setAsignando(false)
      const texto = tratarError(error)
      if (texto === null) return
      if (error instanceof ApiError && error.status === 400) {
        const porCampo = { asesorId: erroresDe(error, 'asesorId'), planId: erroresDe(error, 'planId') }
        setErroresAsignacion({
          asesorId: porCampo.asesorId.length ? porCampo.asesorId : undefined,
          planId: porCampo.planId.length ? porCampo.planId : undefined,
        })
        // P7 (CA-P7-29): errores del pago junto a su campo; nada se creó.
        const erroresPago = erroresPagoDe(error)
        if (hayErroresPago(erroresPago)) {
          setPagoAsignacion((p) => ({ ...p, errores: erroresPago }))
          enfocarPrimerErrorPago('asignar-pago', erroresPago)
        }
      }
      // 409: ya tiene una suscripción viva (o el plan ya no está activo).
      setAvisoAsignacion({ tipo: 'error', texto })
    }
  }

  function abrirDialogo(suscripcion: Suscripcion, accion: AccionSuscripcion, boton: HTMLButtonElement) {
    origenDialogo.current = boton
    setResultado(null)
    setDialogo({
      suscripcion,
      accion,
      planId: '',
      erroresPlan: [],
      enviando: false,
      erroresMotivo: [],
      error: null,
      pago: pagoInicial(accion === 'renovar' ? precioDePlan(suscripcion.plan.id) : null),
    })
  }

  /** Precio de un plan activo; null si no se conoce (p. ej. un plan ya inactivo). */
  function precioDePlan(id: number | null | undefined): number | null {
    if (id === null || id === undefined) return null
    const plan = planesActivos.find((p) => p.id === id)
    return plan ? plan.precioCop : null
  }

  /** Precio del plan que quedará tras renovar o cambiar; undefined si aún no se eligió (cambiar de plan). */
  function precioDialogo(d: Dialogo): number | null | undefined {
    if (d.accion === 'renovar') return precioDePlan(d.planId ? Number(d.planId) : d.suscripcion.plan.id)
    if (d.accion === 'cambiar-plan') return d.planId ? precioDePlan(Number(d.planId)) : undefined
    return undefined
  }

  function abrirPagos(suscripcion: Suscripcion, boton: HTMLButtonElement) {
    origenPagos.current = boton
    setResultado(null)
    setPagosDe(suscripcion)
  }

  function cerrarPagos() {
    setPagosDe(null)
    const boton = origenPagos.current
    origenPagos.current = null
    window.setTimeout(() => boton?.focus(), 0)
  }

  function abrirActividad(suscripcion: Suscripcion, boton: HTMLButtonElement) {
    origenActividad.current = boton
    setActividadDe(suscripcion)
  }

  function cerrarActividad() {
    setActividadDe(null)
    const boton = origenActividad.current
    origenActividad.current = null
    window.setTimeout(() => boton?.focus(), 0)
  }

  function cancelarDialogo() {
    setDialogo(null)
    const boton = origenDialogo.current
    origenDialogo.current = null
    window.setTimeout(() => boton?.focus(), 0)
  }

  function terminarAccion(aviso: DatosAviso) {
    setDialogo(null)
    origenDialogo.current = null
    setResultado(aviso)
    setRecarga((r) => r + 1)
    window.setTimeout(() => tituloLista.current?.focus(), 0)
  }

  async function confirmar(motivo: string) {
    if (!dialogo || dialogo.enviando) return
    const { suscripcion, accion, planId } = dialogo
    if (accion === 'cambiar-plan' && !planId) {
      setDialogo({ ...dialogo, erroresPlan: [MENSAJE_ELIGE_PLAN] })
      document.getElementById('dialogo-plan')?.focus()
      return
    }
    const conPago =
      (accion === 'renovar' || accion === 'cambiar-plan') && ofrecePago(precioDialogo(dialogo)) && dialogo.pago.registrar
    if (conPago) {
      const erroresPago = validarPago(dialogo.pago.datos)
      if (hayErroresPago(erroresPago)) {
        setDialogo({ ...dialogo, pago: { ...dialogo.pago, errores: erroresPago } })
        enfocarPrimerErrorPago('dialogo-pago', erroresPago)
        return
      }
    }
    setDialogo({
      ...dialogo,
      enviando: true,
      erroresPlan: [],
      erroresMotivo: [],
      error: null,
      pago: { ...dialogo.pago, errores: {} },
    })
    try {
      const actualizada = await ejecutarAccionSuscripcion(token, suscripcion.id, accion, {
        planId: planId ? Number(planId) : null,
        motivo,
        pago: conPago ? pagoDesdeFormulario(dialogo.pago.datos) : null,
      })
      terminarAccion({
        tipo: 'exito',
        texto: `${exito(accion, suscripcion.asesor.nombre || 'el asesor', actualizada)}${conPago ? ' Se registró el pago.' : ''}`,
      })
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setDialogo(null)
        onSesionTerminada()
        return
      }
      if (error instanceof ApiError && error.status === 400) {
        const motivoErr = erroresDe(error, 'motivo')
        const planErr = erroresDe(error, 'planId')
        const pagoErr = erroresPagoDe(error)
        const hayPago = hayErroresPago(pagoErr)
        setDialogo(
          (d) =>
            d && {
              ...d,
              enviando: false,
              erroresMotivo: motivoErr,
              erroresPlan: planErr,
              pago: { ...d.pago, errores: pagoErr },
              error: motivoErr.length || planErr.length || hayPago ? null : { tipo: 'error', texto: error.mensaje },
            },
        )
        if (hayPago) window.setTimeout(() => enfocarPrimerErrorPago('dialogo-pago', pagoErr), 0)
        return
      }
      if (error instanceof ApiError && [403, 404, 409].includes(error.status)) {
        // La suscripción cambió (p. ej. "No se puede suspender una suscripción en estado CANCELADA.").
        terminarAccion({ tipo: 'error', texto: error.mensaje })
        return
      }
      const texto = error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO
      setDialogo((d) => d && { ...d, enviando: false, error: { tipo: 'error', texto } })
    }
  }

  const opcionesAsesor = asesores.map((a) => ({ valor: a.id, nombre: a.nombre || `Asesor ${a.id}` }))
  const opcionesPlan = planesActivos.map((p) => ({
    valor: String(p.id),
    nombre: `${p.nombre} (${p.duracionMeses === 1 ? '1 mes' : `${p.duracionMeses} meses`}, ${formatoMiles(p.limitePublicaciones)} publicaciones)`,
  }))
  const opcionesEstado = ESTADOS_FILTRO.map((e) => ({ valor: e, nombre: nombreEstadoSuscripcion(e) }))

  const totalPaginas = lista ? Math.max(1, Math.ceil(lista.total / Math.max(1, lista.tamano))) : 1
  const paginaActual = lista?.pagina ?? filtros.pagina
  const planAsignado = planesActivos.find((p) => String(p.id) === asignacion.planId) ?? null

  function contenidoDialogo(d: Dialogo) {
    if (d.accion !== 'renovar' && d.accion !== 'cambiar-plan') return undefined
    const actual = d.suscripcion.plan
    const opciones =
      d.accion === 'cambiar-plan' ? opcionesPlan.filter((o) => o.valor !== String(actual.id)) : opcionesPlan
    return (
      <>
        {d.accion === 'cambiar-plan' && <NotaObligatorio />}
        <CampoSelect
          id="dialogo-plan"
          etiqueta={d.accion === 'renovar' ? 'Plan para la renovación' : 'Plan nuevo'}
          valor={d.planId}
          onCambio={(v) =>
            setDialogo((x) => {
              if (!x) return x
              // P7: el valor del pago toma el precio del plan elegido.
              const id = v ? Number(v) : x.accion === 'renovar' ? x.suscripcion.plan.id : null
              const precio = precioDePlan(id)
              const valorCop = precio !== null && precio > 0 ? String(precio) : x.pago.datos.valorCop
              return { ...x, planId: v, erroresPlan: [], pago: { ...x.pago, datos: { ...x.pago.datos, valorCop } } }
            })
          }
          opciones={opciones}
          textoVacio={
            d.accion === 'renovar'
              ? `Mantener el plan actual${actual.nombre ? ` (${actual.nombre})` : ''}`
              : 'Elige un plan activo'
          }
          errores={d.erroresPlan}
          requerido={d.accion === 'cambiar-plan'}
          deshabilitado={d.enviando}
        />
        {ofrecePago(precioDialogo(d)) && (
          <BloquePago
            id="dialogo-pago"
            pago={d.pago}
            precio={precioDialogo(d) ?? null}
            deshabilitado={d.enviando}
            onCambio={(pago) => setDialogo((x) => x && { ...x, pago })}
          />
        )}
      </>
    )
  }

  return (
    <section className="panel-ancho" aria-labelledby="suscripciones-titulo">
      <h2 id="suscripciones-titulo" ref={titulo} tabIndex={-1}>
        Suscripciones
      </h2>
      <p className="form-ayuda">
        Asigna un plan a un asesor aprobado cuando confirmes su pago, y renueva, cambia, suspende,
        reactiva o cancela sus suscripciones. Cada mes del plan da 30 días.
      </p>

      <form className="admin-form" onSubmit={(e) => void asignar(e)} noValidate aria-busy={asignando}>
        <h3 className="admin-subtitulo">Asignar plan</h3>
        <NotaObligatorio />
        <Aviso aviso={errorOpciones} />
        <div className="admin-rejilla">
          <CampoSelect
            id="asignar-asesor"
            etiqueta="Asesor aprobado"
            valor={asignacion.asesorId}
            onCambio={(v) => {
              setAsignacion((a) => ({ ...a, asesorId: v }))
              setErroresAsignacion((e) => ({ ...e, asesorId: undefined }))
            }}
            opciones={opcionesAsesor}
            textoVacio={opcionesAsesor.length ? 'Elige un asesor' : 'No hay asesores aprobados'}
            errores={erroresAsignacion.asesorId}
            deshabilitado={asignando}
          />
          <CampoSelect
            id="asignar-plan"
            etiqueta="Plan activo"
            valor={asignacion.planId}
            onCambio={(v) => {
              setAsignacion((a) => ({ ...a, planId: v }))
              setErroresAsignacion((e) => ({ ...e, planId: undefined }))
              // P7: el valor del pago toma el precio del plan elegido.
              setPagoAsignacion((p) => ({
                ...p,
                errores: {},
                datos: { ...p.datos, valorCop: formularioPagoInicial(precioDePlan(v ? Number(v) : null)).valorCop },
              }))
            }}
            opciones={opcionesPlan}
            textoVacio={opcionesPlan.length ? 'Elige un plan' : 'No hay planes activos'}
            errores={erroresAsignacion.planId}
            deshabilitado={asignando}
          />
        </div>
        {planAsignado && ofrecePago(planAsignado.precioCop) && (
          <BloquePago
            id="asignar-pago"
            pago={pagoAsignacion}
            precio={planAsignado.precioCop}
            deshabilitado={asignando}
            onCambio={setPagoAsignacion}
          />
        )}
        {planAsignado && !ofrecePago(planAsignado.precioCop) && (
          <p className="form-ayuda">Este plan no tiene precio: no se registra un pago.</p>
        )}
        <Aviso aviso={avisoAsignacion} />
        <div className="admin-botones">
          <button type="submit" disabled={asignando}>
            {asignando ? 'Asignando...' : 'Asignar plan'}
          </button>
        </div>
      </form>

      <form className="filtros" onSubmit={aplicarFiltros} aria-label="Filtros de suscripciones">
        <CampoSelect
          id="suscripciones-asesor"
          etiqueta="Asesor"
          valor={borrador.asesorId}
          onCambio={(v) => setBorrador((b) => ({ ...b, asesorId: v }))}
          opciones={opcionesAsesor}
          textoVacio="Todos los asesores"
          requerido={false}
        />
        <CampoSelect
          id="suscripciones-estado"
          etiqueta="Estado"
          valor={borrador.estado}
          onCambio={(v) => setBorrador((b) => ({ ...b, estado: v }))}
          opciones={opcionesEstado}
          textoVacio="Todos los estados"
          requerido={false}
        />
        <button type="submit" disabled={cargando}>
          Aplicar filtros
        </button>
      </form>

      {filtros.vista && (
        <p className="lead-filtro-vehiculo" role="status">
          Filtro del panel: <strong>{NOMBRES_VISTA_SUSCRIPCIONES[filtros.vista]}</strong>.{' '}
          <button type="button" className="secondary-button inv-boton-auto" onClick={quitarVista}>
            Quitar este filtro
          </button>
        </p>
      )}

      <h3 ref={tituloLista} tabIndex={-1} className="admin-subtitulo">
        Resultados
        {lista && !cargando && (
          <span className="admin-total">
            {' '}
            ({lista.total} {lista.total === 1 ? 'suscripción' : 'suscripciones'})
          </span>
        )}
      </h3>
      <Aviso aviso={resultado} />
      <Aviso aviso={errorCarga} />
      {cargando && <p role="status">Cargando suscripciones...</p>}
      {lista && lista.items.length === 0 && !cargando && !errorCarga && (
        <p className="admin-vacio">No hay suscripciones con estos filtros.</p>
      )}

      {lista && lista.items.length > 0 && (
        <div
          className="tabla-contenedor"
          role="region"
          aria-labelledby="suscripciones-tabla"
          tabIndex={0}
          aria-busy={cargando}
        >
          <table className="tabla-usuarios tabla-admin">
            <caption id="suscripciones-tabla" className="sr-only">
              Suscripciones, página {paginaActual} de {totalPaginas}
            </caption>
            <thead>
              <tr>
                <th scope="col">Asesor</th>
                <th scope="col">Plan</th>
                <th scope="col">Estado</th>
                <th scope="col">Inicio</th>
                <th scope="col">Vence</th>
                <th scope="col">Días restantes</th>
                <th scope="col">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {lista.items.map((s) => {
                const acciones = accionesSuscripcion(s)
                const nombre = s.asesor.nombre || 'el asesor'
                return (
                  <tr key={s.id}>
                    <th scope="row">{s.asesor.nombre || '—'}</th>
                    <td>{s.plan.nombre || '—'}</td>
                    <td>
                      <span className={`estado ${claseEstado(s.estadoEfectivo)}`}>
                        {nombreEstadoSuscripcion(s.estadoEfectivo)}
                      </span>
                    </td>
                    <td>{formatearFechaSuscripcion(s.inicioVigencia)}</td>
                    <td>{formatearFechaSuscripcion(s.finVigencia)}</td>
                    <td>{acciones.length > 0 ? textoDiasRestantes(s.diasRestantes) : '—'}</td>
                    <td>
                      <div className="acciones-fila">
                        <button
                          type="button"
                          className="secondary-button"
                          aria-expanded={pagosDe?.id === s.id}
                          disabled={dialogo !== null}
                          onClick={(e) => (pagosDe?.id === s.id ? cerrarPagos() : abrirPagos(s, e.currentTarget))}
                        >
                          Pagos <span className="sr-only">de {nombre}</span>
                        </button>
                        <button
                          type="button"
                          className="secondary-button"
                          aria-expanded={actividadDe?.id === s.id}
                          disabled={dialogo !== null}
                          onClick={(e) =>
                            actividadDe?.id === s.id ? cerrarActividad() : abrirActividad(s, e.currentTarget)
                          }
                        >
                          Actividad <span className="sr-only">de la suscripción de {nombre}</span>
                        </button>
                        {acciones.map((accion) => (
                            <button
                              key={accion}
                              type="button"
                              className={TEXTOS_ACCION_SUSCRIPCION[accion].peligro ? 'boton-peligro' : undefined}
                              disabled={dialogo !== null}
                              onClick={(e) => abrirDialogo(s, accion, e.currentTarget)}
                            >
                              {TEXTOS_ACCION_SUSCRIPCION[accion].boton}{' '}
                              <span className="sr-only">de {nombre}</span>
                            </button>
                        ))}
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      {lista && lista.total > 0 && (
        <nav className="paginacion" aria-label="Paginación de suscripciones">
          <button
            type="button"
            className="secondary-button"
            disabled={cargando || paginaActual <= 1}
            onClick={() => irAPagina(paginaActual - 1)}
          >
            Anterior
          </button>
          <span>
            Página {paginaActual} de {totalPaginas}
          </span>
          <button
            type="button"
            className="secondary-button"
            disabled={cargando || paginaActual >= totalPaginas}
            onClick={() => irAPagina(paginaActual + 1)}
          >
            Siguiente
          </button>
        </nav>
      )}

      {pagosDe && (
        <PagosSuscripcion
          key={pagosDe.id}
          token={token}
          suscripcion={pagosDe}
          precioPlan={precioDePlan(pagosDe.plan.id)}
          onSesionTerminada={onSesionTerminada}
          onCerrar={cerrarPagos}
          onSuscripcionCambiada={() => setRecarga((r) => r + 1)}
        />
      )}

      {actividadDe && (
        <SeccionActividad
          key={actividadDe.id}
          token={token}
          recurso="suscripcion"
          id={actividadDe.id}
          nivelTitulo={3}
          abiertaAlInicio
          enfocarAlMontar
          descripcion={`de la suscripción de ${actividadDe.asesor.nombre || 'el asesor'} (${actividadDe.plan.nombre || 'sin plan'})`}
          onCerrar={cerrarActividad}
          onSesionTerminada={onSesionTerminada}
        />
      )}

      {dialogo && (
        <DialogoConfirmacion
          titulo={TEXTOS_ACCION_SUSCRIPCION[dialogo.accion].titulo}
          descripcion={pregunta(dialogo.accion, dialogo.suscripcion)}
          textoBoton={TEXTOS_ACCION_SUSCRIPCION[dialogo.accion].boton}
          textoEnviando={TEXTOS_ACCION_SUSCRIPCION[dialogo.accion].enviando}
          peligro={TEXTOS_ACCION_SUSCRIPCION[dialogo.accion].peligro}
          motivo={
            dialogo.accion === 'renovar' || dialogo.accion === 'cambiar-plan'
              ? 'no'
              : motivoObligatorioSuscripcion(dialogo.accion)
                ? 'obligatorio'
                : 'opcional'
          }
          validarMotivo={(m) => validarMotivoSuscripcion(dialogo.accion, m)}
          contenido={contenidoDialogo(dialogo)}
          enviando={dialogo.enviando}
          erroresMotivo={dialogo.erroresMotivo}
          error={dialogo.error}
          onConfirmar={(m) => void confirmar(m)}
          onCancelar={cancelarDialogo}
        />
      )}
    </section>
  )
}
