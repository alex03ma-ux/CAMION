// P4p — Planes de los asesores (HU-049 crear, HU-050 modificar, HU-051 activar o inactivar; D-35).
// Solo se muestra al ADMINISTRADOR; la API lo protege igualmente. Un plan nunca se borra.
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import {
  cambiarEstadoPlan,
  crearPlan,
  datosDeFormulario,
  diasDeMeses,
  editarPlan,
  FORMULARIO_PLAN_VACIO,
  formularioDePlan,
  listarPlanes,
  NOMBRE_PLAN_MAX,
  repartirErroresPlan,
  validarPlan,
  type AccionPlan,
  type CampoPlan,
  type ErroresPlan,
  type FormularioPlan,
  type Plan,
} from '../api/planes'
import { formatoCop, formatoMiles, mostrarConMiles, soloDigitos } from '../catalogo/formato'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo } from './Campo'
import { DialogoConfirmacion } from './DialogoAccion'
import { NotaObligatorio } from './NotaObligatorio'

type AdminPlanesProps = {
  token: string
  onSesionTerminada: () => void
}

const ID_CAMPO: Record<CampoPlan, string> = {
  nombre: 'plan-nombre',
  precioCop: 'plan-precio',
  duracionMeses: 'plan-duracion',
  limitePublicaciones: 'plan-limite',
  esPrueba: 'plan-prueba',
}

const ORDEN_CAMPOS: readonly CampoPlan[] = [
  'nombre',
  'precioCop',
  'duracionMeses',
  'limitePublicaciones',
  'esPrueba',
]

function textoMeses(meses: number): string {
  return meses === 1 ? '1 mes' : `${meses} meses`
}

/** "= 90 días" mientras se escribe la duración (D-35: 30 días por mes). */
export function textoDiasDuracion(duracion: string): string {
  const n = Number(duracion)
  if (!/^\d{1,2}$/.test(duracion.trim()) || n < 1) return 'Cada mes equivale a 30 días.'
  return `= ${formatoMiles(diasDeMeses(n))} días (30 días por mes).`
}

type Dialogo = { plan: Plan; accion: AccionPlan; enviando: boolean; error: DatosAviso | null }

export function AdminPlanes({ token, onSesionTerminada }: AdminPlanesProps) {
  const [planes, setPlanes] = useState<Plan[] | null>(null)
  const [cargando, setCargando] = useState(true)
  const [errorCarga, setErrorCarga] = useState<DatosAviso | null>(null)
  const [recarga, setRecarga] = useState(0)
  const [resultado, setResultado] = useState<DatosAviso | null>(null)

  const [formulario, setFormulario] = useState<FormularioPlan>(FORMULARIO_PLAN_VACIO)
  const [editando, setEditando] = useState<Plan | null>(null)
  const [errores, setErrores] = useState<ErroresPlan>({})
  const [errorFormulario, setErrorFormulario] = useState<DatosAviso | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [dialogo, setDialogo] = useState<Dialogo | null>(null)

  const titulo = useRef<HTMLHeadingElement>(null)
  const tituloFormulario = useRef<HTMLHeadingElement>(null)
  const tituloLista = useRef<HTMLHeadingElement>(null)
  const origenDialogo = useRef<HTMLButtonElement | null>(null)

  useEffect(() => {
    titulo.current?.focus()
  }, [])

  useEffect(() => {
    const control = new AbortController()
    setCargando(true)
    setErrorCarga(null)
    listarPlanes(token, undefined, control.signal)
      .then((lista) => {
        setPlanes(lista)
        setCargando(false)
      })
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        setCargando(false)
        if (error instanceof ApiError && error.status === 401) {
          onSesionTerminada()
          return
        }
        setErrorCarga({
          tipo: 'error',
          texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO,
        })
      })
    return () => control.abort()
  }, [token, recarga, onSesionTerminada])

  function cambiar<C extends CampoPlan>(campo: C, valor: FormularioPlan[C]) {
    setFormulario((f) => ({ ...f, [campo]: valor }))
    if (errores[campo]) setErrores((e) => ({ ...e, [campo]: undefined }))
  }

  function enfocarPrimerError(e: ErroresPlan) {
    const campo = ORDEN_CAMPOS.find((c) => (e[c]?.length ?? 0) > 0)
    if (campo) window.setTimeout(() => document.getElementById(ID_CAMPO[campo])?.focus(), 0)
  }

  function empezarEdicion(plan: Plan) {
    setEditando(plan)
    setFormulario(formularioDePlan(plan))
    setErrores({})
    setErrorFormulario(null)
    setResultado(null)
    window.setTimeout(() => tituloFormulario.current?.focus(), 0)
  }

  function limpiarFormulario() {
    setEditando(null)
    setFormulario(FORMULARIO_PLAN_VACIO)
    setErrores({})
    setErrorFormulario(null)
  }

  async function guardar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (enviando) return
    setResultado(null)
    setErrorFormulario(null)
    const validacion = validarPlan(formulario)
    setErrores(validacion)
    if (Object.keys(validacion).length > 0) {
      setErrorFormulario({ tipo: 'error', texto: 'Revisa los campos marcados.' })
      enfocarPrimerError(validacion)
      return
    }
    setEnviando(true)
    const datos = datosDeFormulario(formulario)
    try {
      if (editando) await editarPlan(token, editando.id, datos)
      else await crearPlan(token, datos)
      setEnviando(false)
      setResultado({
        tipo: 'exito',
        texto: editando
          ? `Se guardaron los cambios del plan ${datos.nombre}.`
          : `Se creó el plan ${datos.nombre}.`,
      })
      limpiarFormulario()
      setRecarga((r) => r + 1)
      window.setTimeout(() => tituloLista.current?.focus(), 0)
    } catch (error) {
      setEnviando(false)
      if (error instanceof ApiError && error.status === 401) {
        onSesionTerminada()
        return
      }
      if (error instanceof ApiError && error.status === 400) {
        const { porCampo, otros } = repartirErroresPlan(error.errores)
        setErrores(porCampo)
        setErrorFormulario({ tipo: 'error', texto: [error.mensaje, ...otros].join(' ') })
        enfocarPrimerError(porCampo)
        return
      }
      if (error instanceof ApiError && error.status === 409) {
        // Nombre duplicado (sin distinguir mayúsculas).
        const { porCampo } = repartirErroresPlan(error.errores)
        const conNombre: ErroresPlan = porCampo.nombre ? porCampo : { ...porCampo, nombre: [error.mensaje] }
        setErrores(conNombre)
        setErrorFormulario({ tipo: 'error', texto: error.mensaje })
        enfocarPrimerError(conNombre)
        return
      }
      if (error instanceof ApiError && error.status === 404) {
        limpiarFormulario()
        setRecarga((r) => r + 1)
      }
      setErrorFormulario({
        tipo: 'error',
        texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO,
      })
    }
  }

  function abrirDialogo(plan: Plan, accion: AccionPlan, boton: HTMLButtonElement) {
    origenDialogo.current = boton
    setResultado(null)
    setDialogo({ plan, accion, enviando: false, error: null })
  }

  function cancelarDialogo() {
    setDialogo(null)
    const boton = origenDialogo.current
    origenDialogo.current = null
    window.setTimeout(() => boton?.focus(), 0)
  }

  async function confirmarEstado() {
    if (!dialogo || dialogo.enviando) return
    const { plan, accion } = dialogo
    setDialogo({ ...dialogo, enviando: true, error: null })
    try {
      await cambiarEstadoPlan(token, plan.id, accion)
      setDialogo(null)
      origenDialogo.current = null
      setResultado({
        tipo: 'exito',
        texto: accion === 'activar' ? `Se activó el plan ${plan.nombre}.` : `Se inactivó el plan ${plan.nombre}.`,
      })
      setRecarga((r) => r + 1)
      window.setTimeout(() => tituloLista.current?.focus(), 0)
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setDialogo(null)
        onSesionTerminada()
        return
      }
      const texto = error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO
      if (error instanceof ApiError && [403, 404, 409].includes(error.status)) {
        setDialogo(null)
        setResultado({ tipo: 'error', texto })
        setRecarga((r) => r + 1)
        window.setTimeout(() => tituloLista.current?.focus(), 0)
        return
      }
      setDialogo((d) => d && { ...d, enviando: false, error: { tipo: 'error', texto } })
    }
  }

  const idAyudaDias = 'plan-duracion-dias'

  return (
    <section className="panel-ancho" aria-labelledby="planes-titulo">
      <h2 id="planes-titulo" ref={titulo} tabIndex={-1}>
        Planes
      </h2>
      <p className="form-ayuda">
        Crea y edita los planes que se asignan a los asesores. Un plan inactivo no se puede asignar
        ni renovar, pero las suscripciones que ya lo usan siguen. Los planes no se borran.
      </p>

      <form className="admin-form" onSubmit={(e) => void guardar(e)} noValidate aria-busy={enviando}>
        <h3 ref={tituloFormulario} tabIndex={-1} className="admin-subtitulo">
          {editando ? `Editar plan ${editando.nombre}` : 'Nuevo plan'}
        </h3>
        {editando && (
          <p className="campo-ayuda">
            Los cambios no modifican las suscripciones en curso; se aplican al asignar o renovar.
          </p>
        )}
        <NotaObligatorio />
        <div className="admin-rejilla">
          <Campo
            id={ID_CAMPO.nombre}
            etiqueta="Nombre"
            valor={formulario.nombre}
            onCambio={(v) => cambiar('nombre', v)}
            maxLength={NOMBRE_PLAN_MAX}
            errores={errores.nombre}
            autoComplete="off"
            deshabilitado={enviando}
          />
          <Campo
            id={ID_CAMPO.precioCop}
            etiqueta="Precio (COP)"
            valor={mostrarConMiles(formulario.precioCop)}
            onCambio={(v) => cambiar('precioCop', soloDigitos(v))}
            inputMode="numeric"
            maxLength={15}
            errores={errores.precioCop}
            autoComplete="off"
            describidoPor="plan-precio-ayuda"
            deshabilitado={enviando}
          />
          <p id="plan-precio-ayuda" className="campo-ayuda admin-ayuda-campo">
            En pesos, sin decimales. Un plan de prueba puede costar 0.
          </p>
          <Campo
            id={ID_CAMPO.duracionMeses}
            etiqueta="Duración en meses"
            valor={formulario.duracionMeses}
            onCambio={(v) => cambiar('duracionMeses', soloDigitos(v).slice(0, 2))}
            inputMode="numeric"
            maxLength={2}
            errores={errores.duracionMeses}
            autoComplete="off"
            describidoPor={idAyudaDias}
            deshabilitado={enviando}
          />
          <p id={idAyudaDias} className="campo-ayuda admin-ayuda-campo" aria-live="polite">
            {textoDiasDuracion(formulario.duracionMeses)}
          </p>
          <Campo
            id={ID_CAMPO.limitePublicaciones}
            etiqueta="Cupo de publicaciones"
            valor={formulario.limitePublicaciones}
            onCambio={(v) => cambiar('limitePublicaciones', soloDigitos(v).slice(0, 4))}
            inputMode="numeric"
            maxLength={4}
            errores={errores.limitePublicaciones}
            autoComplete="off"
            describidoPor="plan-limite-ayuda"
            deshabilitado={enviando}
          />
          <p id="plan-limite-ayuda" className="campo-ayuda admin-ayuda-campo">
            De 1 a 1.000 vehículos publicados a la vez.
          </p>
        </div>
        <label className="opcion-casilla" htmlFor={ID_CAMPO.esPrueba}>
          <input
            id={ID_CAMPO.esPrueba}
            type="checkbox"
            checked={formulario.esPrueba}
            onChange={(e) => cambiar('esPrueba', e.target.checked)}
            disabled={enviando}
          />
          <span>Es un plan de prueba</span>
        </label>
        {errores.esPrueba && (
          <ul className="campo-errores">
            {errores.esPrueba.map((m) => (
              <li key={m}>{m}</li>
            ))}
          </ul>
        )}

        <Aviso aviso={errorFormulario} />
        <div className="admin-botones">
          <button type="submit" disabled={enviando}>
            {enviando ? 'Guardando...' : editando ? 'Guardar cambios' : 'Crear plan'}
          </button>
          {editando && (
            <button type="button" className="secondary-button" onClick={limpiarFormulario} disabled={enviando}>
              Cancelar edición
            </button>
          )}
        </div>
      </form>

      <h3 ref={tituloLista} tabIndex={-1} className="admin-subtitulo">
        Planes registrados
        {planes && !cargando && <span className="admin-total"> ({planes.length})</span>}
      </h3>
      <Aviso aviso={resultado} />
      <Aviso aviso={errorCarga} />
      {cargando && <p role="status">Cargando planes...</p>}
      {planes && planes.length === 0 && !cargando && !errorCarga && (
        <p className="admin-vacio">Aún no hay planes. Crea el primero con el formulario.</p>
      )}

      {planes && planes.length > 0 && (
        <div className="tabla-contenedor" role="region" aria-labelledby="planes-tabla" tabIndex={0}>
          <table className="tabla-usuarios tabla-admin">
            <caption id="planes-tabla" className="sr-only">
              Planes registrados
            </caption>
            <thead>
              <tr>
                <th scope="col">Nombre</th>
                <th scope="col">Precio</th>
                <th scope="col">Duración</th>
                <th scope="col">Cupo</th>
                <th scope="col">Tipo</th>
                <th scope="col">Estado</th>
                <th scope="col">Suscripciones vivas</th>
                <th scope="col">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {planes.map((p) => (
                <tr key={p.id}>
                  <th scope="row">{p.nombre}</th>
                  <td>{formatoCop(p.precioCop)}</td>
                  <td>
                    {textoMeses(p.duracionMeses)}
                    <span className="dato-secundario">{formatoMiles(p.diasVigencia)} días</span>
                  </td>
                  <td>
                    {formatoMiles(p.limitePublicaciones)}{' '}
                    {p.limitePublicaciones === 1 ? 'publicación' : 'publicaciones'}
                  </td>
                  <td>{p.esPrueba ? 'De prueba' : 'Normal'}</td>
                  <td>
                    <span className={`estado ${p.activo ? 'estado-activa' : 'estado-bloqueada'}`}>
                      {p.activo ? 'Activo' : 'Inactivo'}
                    </span>
                  </td>
                  <td>{formatoMiles(p.suscripcionesVivas)}</td>
                  <td>
                    <div className="acciones-fila">
                      <button
                        type="button"
                        className="secondary-button"
                        disabled={dialogo !== null || enviando}
                        onClick={() => empezarEdicion(p)}
                      >
                        Editar <span className="sr-only">el plan {p.nombre}</span>
                      </button>
                      <button
                        type="button"
                        className={p.activo ? 'boton-peligro' : undefined}
                        disabled={dialogo !== null || enviando}
                        onClick={(e) => abrirDialogo(p, p.activo ? 'inactivar' : 'activar', e.currentTarget)}
                      >
                        {p.activo ? 'Inactivar' : 'Activar'}{' '}
                        <span className="sr-only">el plan {p.nombre}</span>
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {dialogo && (
        <DialogoConfirmacion
          titulo={dialogo.accion === 'activar' ? 'Activar plan' : 'Inactivar plan'}
          descripcion={
            dialogo.accion === 'activar'
              ? `¿Activar el plan ${dialogo.plan.nombre}? Se podrá volver a asignar y renovar.`
              : `¿Inactivar el plan ${dialogo.plan.nombre}? Ya no se podrá asignar ni renovar con él. ` +
                `Las ${formatoMiles(dialogo.plan.suscripcionesVivas)} suscripciones vivas que lo usan siguen igual.`
          }
          textoBoton={dialogo.accion === 'activar' ? 'Activar' : 'Inactivar'}
          textoEnviando={dialogo.accion === 'activar' ? 'Activando...' : 'Inactivando...'}
          peligro={dialogo.accion === 'inactivar'}
          motivo="no"
          enviando={dialogo.enviando}
          error={dialogo.error}
          onConfirmar={() => void confirmarEstado()}
          onCancelar={cancelarDialogo}
        />
      )}
    </section>
  )
}
