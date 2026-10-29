// P4b — "Mis publicaciones" (#/publicaciones, HU-065, RN-GC-002). El asesor ve las suyas con el
// contador de cupo y destacados de su plan (D-38, D-40, D-41); el administrador ve todas y puede
// filtrarlas por asesor. La interfaz solo oculta: la API protege cada acción (401/403/404/409).
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import type { PerfilUsuario } from '../api/auth'
import { ApiError, MENSAJE_INESPERADO, MENSAJE_SIN_PERMISO } from '../api/http'
import { obtenerCatalogosInventario, type ResumenAsesor } from '../api/inventario'
import {
  accionPublicacion,
  CAMPOS_PUBLICACION,
  datosDePublicacion,
  editarPublicacion,
  ESTADOS_FILTRO_PUBLICACION,
  formularioDePublicacion,
  inactivarPublicacion,
  listarPublicaciones,
  obtenerCupo,
  obtenerPublicacion,
  Q_MAX_PUBLICACIONES,
  repartirErrores,
  TAMANO_PAGINA_PUBLICACIONES,
  type AccionPublicacion,
  type CupoPublicaciones,
  type ErroresPublicacion,
  type FiltrosPublicaciones,
  type FormularioPublicacion,
  type ListaPublicaciones,
  type Publicacion,
} from '../api/publicaciones'
import { formatearFechaSuscripcion } from '../api/suscripciones'
import { formatoCop, formatoMiles } from '../catalogo/formato'
import { validarMotivoInactivar } from '../inventario/estados'
import {
  accionesPublicacion,
  avisosCupo,
  estadoVisible,
  nombreEstadoPublicacion,
  TEXTOS_ACCION_PUBLICACION,
  textoContadorCupo,
} from '../publicaciones/estados'
import { HASH_CUENTA, HASH_INVENTARIO, hashVehiculo } from '../rutas'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo, CampoSelect } from './Campo'
import { DialogoConfirmacion } from './DialogoAccion'
import { DialogoPublicacion } from './DialogosPublicacion'
import { ImagenVehiculo } from './ImagenVehiculo'
import { modoInventario } from './InventarioPagina'

type MisPublicacionesProps = {
  token: string
  perfil: PerfilUsuario
  onSesionTerminada: () => void
}

export function MisPublicaciones({ token, perfil, onSesionTerminada }: MisPublicacionesProps) {
  const modo = modoInventario(perfil.roles)
  if (modo === null) {
    return (
      <div className="cat-pagina inv-pagina">
        <h1 tabIndex={-1} className="inv-titulo">
          Mis publicaciones
        </h1>
        <Aviso aviso={{ tipo: 'error', texto: MENSAJE_SIN_PERMISO }} />
        <p>
          <a href={HASH_CUENTA}>Volver a mi cuenta</a>
        </p>
      </div>
    )
  }
  return <PanelPublicaciones token={token} esAdmin={modo === 'administrador'} onSesionTerminada={onSesionTerminada} />
}

const FILTROS_INICIALES: FiltrosPublicaciones = {
  estado: '',
  q: '',
  asesorId: '',
  pagina: 1,
  tamano: TAMANO_PAGINA_PUBLICACIONES,
}

type Dialogo =
  | { tipo: 'editar'; publicacion: Publicacion; inicial: FormularioPublicacion; enviando: boolean; errores: ErroresPublicacion; error: DatosAviso | null }
  | { tipo: 'inactivar' | 'eliminar' | 'reactivar'; publicacion: Publicacion; enviando: boolean; erroresMotivo: string[]; error: DatosAviso | null }

type EnCurso = { id: number; accion: AccionPublicacion | 'cargar-edicion' }

const SIN_ERRORES: string[] = []

function mensajeDe(error: unknown): string {
  return error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO
}

function tituloDe(p: Publicacion): string {
  return p.titulo || p.tituloVehiculo || `Publicación ${p.id}`
}

function PanelPublicaciones({
  token,
  esAdmin,
  onSesionTerminada,
}: {
  token: string
  esAdmin: boolean
  onSesionTerminada: () => void
}) {
  const [filtros, setFiltros] = useState<FiltrosPublicaciones>(FILTROS_INICIALES)
  const [borrador, setBorrador] = useState({ estado: '', q: '', asesorId: '' })
  const [recarga, setRecarga] = useState(0)
  const [lista, setLista] = useState<ListaPublicaciones | null>(null)
  const [cargando, setCargando] = useState(true)
  const [errorCarga, setErrorCarga] = useState<DatosAviso | null>(null)
  const [cupo, setCupo] = useState<CupoPublicaciones | null>(null)
  const [errorCupo, setErrorCupo] = useState<string | null>(null)
  const [asesores, setAsesores] = useState<ResumenAsesor[]>([])
  const [resultado, setResultado] = useState<DatosAviso | null>(null)
  const [enCurso, setEnCurso] = useState<EnCurso | null>(null)
  const [dialogo, setDialogo] = useState<Dialogo | null>(null)
  const tituloLista = useRef<HTMLHeadingElement>(null)
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

  // Listado.
  useEffect(() => {
    const control = new AbortController()
    setCargando(true)
    setErrorCarga(null)
    listarPublicaciones(token, filtros, esAdmin, control.signal)
      .then((respuesta) => {
        const ultima = Math.max(1, Math.ceil(respuesta.total / Math.max(1, respuesta.tamano)))
        if (respuesta.items.length === 0 && filtros.pagina > ultima) {
          setFiltros((f) => ({ ...f, pagina: ultima }))
          return
        }
        setLista(respuesta)
        setCargando(false)
      })
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        setCargando(false)
        if (alError401(error)) return
        setLista(null)
        const otros =
          error instanceof ApiError && error.status === 400
            ? Object.values(error.errores).flat()
            : []
        setErrorCarga({ tipo: 'error', texto: [mensajeDe(error), ...otros].join(' ') })
      })
    return () => control.abort()
  }, [token, esAdmin, filtros, recarga, alError401])

  // Contador de cupo y destacados del plan (solo el asesor: es su plan).
  useEffect(() => {
    if (esAdmin) return
    const control = new AbortController()
    obtenerCupo(token, control.signal)
      .then((c) => {
        setCupo(c)
        setErrorCupo(null)
      })
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        if (alError401(error)) return
        setErrorCupo(mensajeDe(error))
      })
    return () => control.abort()
  }, [token, esAdmin, recarga, alError401])

  // Asesores para el filtro del administrador.
  useEffect(() => {
    if (!esAdmin) return
    const control = new AbortController()
    obtenerCatalogosInventario(token, control.signal)
      .then((c) => setAsesores(c.asesores))
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        alError401(error)
      })
    return () => control.abort()
  }, [token, esAdmin, alError401])

  function aplicarFiltros(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    setResultado(null)
    setFiltros({
      estado: borrador.estado,
      q: borrador.q.trim(),
      asesorId: esAdmin ? borrador.asesorId : '',
      pagina: 1,
      tamano: TAMANO_PAGINA_PUBLICACIONES,
    })
  }

  function limpiarFiltros() {
    setResultado(null)
    setBorrador({ estado: '', q: '', asesorId: '' })
    setFiltros(FILTROS_INICIALES)
  }

  function irAPagina(pagina: number) {
    setResultado(null)
    setFiltros((f) => ({ ...f, pagina }))
    tituloLista.current?.focus()
  }

  function terminarAccion(aviso: DatosAviso) {
    setResultado(aviso)
    setRecarga((r) => r + 1)
    window.setTimeout(() => tituloLista.current?.focus(), 0)
  }

  async function accionDirecta(p: Publicacion, accion: 'renovar' | 'destacar' | 'quitar-destacado') {
    if (enCurso || dialogo) return
    setResultado(null)
    setEnCurso({ id: p.id, accion })
    try {
      await accionPublicacion(token, p.id, accion)
      terminarAccion({ tipo: 'exito', texto: TEXTOS_ACCION_PUBLICACION[accion].exito(tituloDe(p)) })
    } catch (error) {
      if (alError401(error)) return
      // 409 (sin plan, sin destacados, estado cambiado) o 404: el mensaje del servidor y recarga.
      terminarAccion({ tipo: 'error', texto: mensajeDe(error) })
    } finally {
      setEnCurso(null)
    }
  }

  async function abrirEdicion(p: Publicacion, boton: HTMLButtonElement) {
    if (enCurso || dialogo) return
    setResultado(null)
    origenDialogo.current = boton
    setEnCurso({ id: p.id, accion: 'cargar-edicion' })
    try {
      // El detalle trae la descripción y si es negociable (el listado puede no traerlos).
      const detalle = await obtenerPublicacion(token, p.id)
      setDialogo({
        tipo: 'editar',
        publicacion: detalle,
        inicial: formularioDePublicacion(detalle),
        enviando: false,
        errores: {},
        error: null,
      })
    } catch (error) {
      origenDialogo.current = null
      if (alError401(error)) return
      terminarAccion({ tipo: 'error', texto: mensajeDe(error) })
    } finally {
      setEnCurso(null)
    }
  }

  function abrirDialogo(tipo: 'inactivar' | 'eliminar' | 'reactivar', p: Publicacion, boton: HTMLButtonElement) {
    origenDialogo.current = boton
    setResultado(null)
    setDialogo({ tipo, publicacion: p, enviando: false, erroresMotivo: SIN_ERRORES, error: null })
  }

  function cancelarDialogo() {
    setDialogo(null)
    const boton = origenDialogo.current
    origenDialogo.current = null
    window.setTimeout(() => boton?.focus(), 0)
  }

  function cerrarYTerminar(aviso: DatosAviso) {
    setDialogo(null)
    origenDialogo.current = null
    terminarAccion(aviso)
  }

  async function guardarEdicion(form: FormularioPublicacion) {
    if (!dialogo || dialogo.tipo !== 'editar' || dialogo.enviando) return
    const { publicacion } = dialogo
    setDialogo({ ...dialogo, enviando: true, error: null })
    try {
      const datos = datosDePublicacion(form)
      await editarPublicacion(token, publicacion.id, datos)
      cerrarYTerminar({ tipo: 'exito', texto: `Se guardaron los cambios de la publicación «${datos.titulo}».` })
    } catch (error) {
      if (alError401(error)) {
        setDialogo(null)
        return
      }
      if (error instanceof ApiError && error.status === 400) {
        const { porCampo, otros } = repartirErrores(error.errores, CAMPOS_PUBLICACION)
        setDialogo((d) =>
          d && d.tipo === 'editar'
            ? { ...d, enviando: false, errores: porCampo, error: { tipo: 'error', texto: [error.mensaje, ...otros].join(' ') } }
            : d,
        )
        return
      }
      if (error instanceof ApiError && [403, 404, 409].includes(error.status)) {
        cerrarYTerminar({ tipo: 'error', texto: error.mensaje })
        return
      }
      setDialogo((d) => d && { ...d, enviando: false, error: { tipo: 'error', texto: mensajeDe(error) } })
    }
  }

  async function confirmarDialogo(motivo: string) {
    if (!dialogo || dialogo.tipo === 'editar' || dialogo.enviando) return
    const { publicacion, tipo } = dialogo
    setDialogo({ ...dialogo, enviando: true, erroresMotivo: SIN_ERRORES, error: null })
    try {
      if (tipo === 'inactivar') await inactivarPublicacion(token, publicacion.id, motivo)
      else await accionPublicacion(token, publicacion.id, tipo)
      cerrarYTerminar({ tipo: 'exito', texto: TEXTOS_ACCION_PUBLICACION[tipo].exito(tituloDe(publicacion)) })
    } catch (error) {
      if (alError401(error)) {
        setDialogo(null)
        return
      }
      if (error instanceof ApiError && error.status === 400 && tipo === 'inactivar') {
        const propios = Object.entries(error.errores)
          .filter(([c]) => c.toLowerCase() === 'motivo')
          .flatMap(([, m]) => m)
        setDialogo((d) =>
          d && d.tipo !== 'editar' ? { ...d, enviando: false, erroresMotivo: propios.length > 0 ? propios : [error.mensaje] } : d,
        )
        return
      }
      if (error instanceof ApiError && [403, 404, 409].includes(error.status)) {
        cerrarYTerminar({ tipo: 'error', texto: error.mensaje })
        return
      }
      setDialogo((d) => d && { ...d, enviando: false, error: { tipo: 'error', texto: mensajeDe(error) } })
    }
  }

  const totalPaginas = lista ? Math.max(1, Math.ceil(lista.total / Math.max(1, lista.tamano))) : 1
  const paginaActual = lista?.pagina ?? filtros.pagina
  const ocupado = enCurso !== null || dialogo !== null
  const hayFiltros = filtros.estado !== '' || filtros.q !== '' || filtros.asesorId !== ''
  const avisos = cupo ? avisosCupo(cupo) : []

  return (
    <div className="cat-pagina inv-pagina">
      <h1 tabIndex={-1} className="inv-titulo">
        {esAdmin ? 'Publicaciones' : 'Mis publicaciones'}
      </h1>
      <p className="inv-intro">
        {esAdmin
          ? 'Las publicaciones de todos los asesores. Puedes filtrarlas por asesor.'
          : 'Tus publicaciones en el portal. Para publicar un vehículo, usa «Publicar» en tu inventario.'}
      </p>
      <div className="inv-barra">
        <a className="inv-boton-enlace inv-boton-secundario" href={HASH_INVENTARIO}>
          {esAdmin ? 'Ir al inventario' : 'Ir a mi inventario'}
        </a>
      </div>

      {!esAdmin && (
        <section className="tarjeta-suscripcion" aria-labelledby="cupo-titulo">
          <h2 id="cupo-titulo">Cupo de tu plan</h2>
          {cupo === null && errorCupo === null && <p role="status">Consultando tu plan...</p>}
          {errorCupo !== null && <Aviso aviso={{ tipo: 'error', texto: errorCupo }} />}
          {cupo && (
            <>
              {cupo.planVigente && <p className="pub-contador">{textoContadorCupo(cupo)}</p>}
              {cupo.planVigente && (
                <p className="campo-ayuda">
                  Tu plan vence el {formatearFechaSuscripcion(cupo.finPlan, true)}. Tus publicaciones se ven en el
                  portal hasta esa fecha. Todas cuentan para el cupo salvo las eliminadas.
                </p>
              )}
              {avisos.map((texto) => (
                <Aviso key={texto} aviso={{ tipo: 'advertencia', texto }} />
              ))}
            </>
          )}
        </section>
      )}

      <form className="inv-filtros" onSubmit={aplicarFiltros} aria-label="Filtros de las publicaciones" noValidate>
        <CampoSelect
          id="pub-filtro-estado"
          etiqueta="Estado"
          valor={borrador.estado}
          onCambio={(v) => setBorrador((b) => ({ ...b, estado: v }))}
          opciones={ESTADOS_FILTRO_PUBLICACION.map((e) => ({ valor: e, nombre: nombreEstadoPublicacion(e) }))}
          textoVacio="Todos los estados"
          requerido={false}
        />
        {esAdmin && (
          <CampoSelect
            id="pub-filtro-asesor"
            etiqueta="Asesor"
            valor={borrador.asesorId}
            onCambio={(v) => setBorrador((b) => ({ ...b, asesorId: v }))}
            opciones={asesores.map((a) => ({ valor: a.id, nombre: a.nombre }))}
            textoVacio="Todos los asesores"
            requerido={false}
          />
        )}
        <Campo
          id="pub-filtro-q"
          etiqueta="Buscar por título"
          tipo="search"
          valor={borrador.q}
          onCambio={(v) => setBorrador((b) => ({ ...b, q: v }))}
          maxLength={Q_MAX_PUBLICACIONES}
          autoComplete="off"
          requerido={false}
        />
        <div className="inv-filtros-botones">
          <button type="submit" disabled={cargando}>
            Aplicar filtros
          </button>
          <button type="button" className="secondary-button" onClick={limpiarFiltros}>
            Limpiar
          </button>
        </div>
      </form>

      <h2 ref={tituloLista} tabIndex={-1} className="inv-subtitulo">
        Publicaciones
        {lista && !cargando && (
          <span className="admin-total">
            {' '}
            ({formatoMiles(lista.total)} {lista.total === 1 ? 'publicación' : 'publicaciones'})
          </span>
        )}
      </h2>

      <Aviso aviso={resultado} />
      <Aviso aviso={errorCarga} />
      {cargando && <p role="status">Cargando publicaciones...</p>}

      {lista && lista.items.length === 0 && !cargando && !errorCarga && (
        <p className="admin-vacio">
          {hayFiltros
            ? 'No hay publicaciones con estos filtros.'
            : esAdmin
              ? 'Aún no hay publicaciones.'
              : 'Aún no tienes publicaciones. Publica un vehículo desde tu inventario.'}
        </p>
      )}

      {lista && lista.items.length > 0 && (() => {
        // Petición de la responsable en la prueba de P4b: las destacadas en una sección aparte,
        // arriba y diferenciadas; debajo, el resto. La API ya las devuelve primero.
        const tarjeta = (p: Publicacion) => {
            const titulo = tituloDe(p)
            const estado = estadoVisible(p)
            const acciones = accionesPublicacion(p)
            return (
              <li key={p.id} className={p.destacada ? 'inv-tarjeta pub-tarjeta-destacada' : 'inv-tarjeta'}>
                <div className="inv-tarjeta-foto">
                  <ImagenVehiculo src={p.fotoPortada} alt="" />
                  {p.destacada && (
                    <span className="pub-sello-destacada" aria-hidden="true">
                      ★ Destacada
                    </span>
                  )}
                </div>
                <div className="inv-tarjeta-cuerpo">
                  <h3 className="inv-tarjeta-titulo">{titulo}</h3>
                  {p.tituloVehiculo && p.tituloVehiculo !== titulo && (
                    <p className="dato-secundario">{p.tituloVehiculo}</p>
                  )}
                  {p.precioCop !== null && <p className="inv-tarjeta-precio">{formatoCop(p.precioCop)}</p>}
                  <p>
                    <span className={`estado pub-estado-${estado.toLowerCase() || 'desconocido'}`}>
                      <span className="sr-only">Estado: </span>
                      {nombreEstadoPublicacion(estado)}
                    </span>
                    {p.destacada && <span className="cat-etiqueta cat-etiqueta-destacado pub-destacada">Destacada</span>}
                  </p>
                  <dl className="inv-datos">
                    {estado === 'PUBLICADA' && p.vigenteHasta && (
                      <div>
                        <dt>Visible hasta</dt>
                        <dd>{formatearFechaSuscripcion(p.vigenteHasta)}</dd>
                      </div>
                    )}
                    {estado === 'VENCIDA' && (
                      <div>
                        <dt>Vigencia</dt>
                        <dd>Venció con el plan. Renuévala cuando el plan esté vigente.</dd>
                      </div>
                    )}
                    {estado === 'OCULTA' && p.motivoOcultamiento && (
                      <div>
                        <dt>Motivo de inactivación</dt>
                        <dd className="inv-texto-largo">{p.motivoOcultamiento}</dd>
                      </div>
                    )}
                    {esAdmin && p.asesor && (
                      <div>
                        <dt>Asesor</dt>
                        <dd>{p.asesor.nombre || '—'}</dd>
                      </div>
                    )}
                    {p.publicadaEn && (
                      <div>
                        <dt>Publicada el</dt>
                        <dd>{formatearFechaSuscripcion(p.publicadaEn)}</dd>
                      </div>
                    )}
                  </dl>
                  <div className="acciones-fila inv-acciones">
                    {acciones.map((accion) => {
                      const sr = <span className="sr-only">{titulo}</span>
                      if (accion === 'ver-portal') {
                        return (
                          <a
                            key={accion}
                            className="inv-boton-enlace inv-boton-pequeno"
                            href={hashVehiculo(p.slugPublico ?? '')}
                          >
                            Ver en el portal {sr}
                          </a>
                        )
                      }
                      if (accion === 'editar') {
                        const cargandoEdicion = enCurso?.id === p.id && enCurso.accion === 'cargar-edicion'
                        return (
                          <button
                            key={accion}
                            type="button"
                            className="secondary-button"
                            disabled={ocupado}
                            onClick={(e) => void abrirEdicion(p, e.currentTarget)}
                          >
                            {cargandoEdicion ? 'Cargando...' : 'Editar'} {sr}
                          </button>
                        )
                      }
                      const textos = TEXTOS_ACCION_PUBLICACION[accion]
                      const enviando = enCurso?.id === p.id && enCurso.accion === accion
                      return (
                        <button
                          key={accion}
                          type="button"
                          className={accion === 'inactivar' || accion === 'eliminar' ? 'boton-peligro' : 'secondary-button'}
                          disabled={ocupado}
                          onClick={(e) =>
                            accion === 'inactivar' || accion === 'eliminar' || accion === 'reactivar'
                              ? abrirDialogo(accion, p, e.currentTarget)
                              : void accionDirecta(p, accion)
                          }
                        >
                          {enviando ? textos.enviando : textos.boton} {sr}
                        </button>
                      )
                    })}
                  </div>
                </div>
              </li>
            )
        }
        const destacadas = lista.items.filter((p) => p.destacada)
        const resto = lista.items.filter((p) => !p.destacada)
        return (
          <>
            {destacadas.length > 0 && (
              <section className="pub-seccion pub-seccion-destacadas" aria-labelledby="pub-titulo-destacadas">
                <h2 id="pub-titulo-destacadas" className="pub-seccion-titulo">
                  <span aria-hidden="true">★ </span>Destacadas ({destacadas.length})
                </h2>
                <p className="campo-ayuda">Se muestran primero en el portal.</p>
                <ul className="inv-lista" aria-busy={cargando}>
                  {destacadas.map(tarjeta)}
                </ul>
              </section>
            )}
            {resto.length > 0 && (
              <section className="pub-seccion" aria-labelledby="pub-titulo-resto">
                <h2 id="pub-titulo-resto" className="pub-seccion-titulo">
                  {destacadas.length > 0 ? 'Otras publicaciones' : 'Publicaciones'} ({resto.length})
                </h2>
                <ul className="inv-lista" aria-busy={cargando}>
                  {resto.map(tarjeta)}
                </ul>
              </section>
            )}
          </>
        )
      })()}

      {lista && lista.total > 0 && totalPaginas > 1 && (
        <nav className="paginacion" aria-label="Paginación de las publicaciones">
          <button
            type="button"
            className="secondary-button"
            disabled={cargando || paginaActual <= 1}
            onClick={() => irAPagina(paginaActual - 1)}
          >
            Anterior
          </button>
          <span>
            Página {formatoMiles(paginaActual)} de {formatoMiles(totalPaginas)}
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

      {dialogo?.tipo === 'editar' && (
        <DialogoPublicacion
          modo="editar"
          tituloVehiculo={dialogo.publicacion.tituloVehiculo || tituloDe(dialogo.publicacion)}
          inicial={dialogo.inicial}
          enviando={dialogo.enviando}
          erroresServidor={dialogo.errores}
          error={dialogo.error}
          onConfirmar={(f) => void guardarEdicion(f)}
          onCancelar={cancelarDialogo}
        />
      )}
      {dialogo && dialogo.tipo !== 'editar' && (
        <DialogoConfirmacion
          titulo={
            dialogo.tipo === 'inactivar'
              ? 'Inactivar publicación'
              : dialogo.tipo === 'eliminar'
                ? 'Eliminar publicación'
                : 'Reactivar publicación'
          }
          descripcion={
            dialogo.tipo === 'inactivar'
              ? `¿Inactivar «${tituloDe(dialogo.publicacion)}»? Dejará de verse en el portal y perderá su destacado. Sigue ocupando un cupo de tu plan.`
              : dialogo.tipo === 'eliminar'
                ? `¿Eliminar «${tituloDe(dialogo.publicacion)}»? Desaparece de «Mis publicaciones» y libera un cupo de tu plan para publicar otro vehículo. El vehículo sigue en tu inventario. No se puede deshacer.`
                : `¿Reactivar «${tituloDe(dialogo.publicacion)}»? Volverá a verse en el portal hasta el fin de tu plan. Necesitas un plan vigente.`
          }
          textoBoton={TEXTOS_ACCION_PUBLICACION[dialogo.tipo].boton}
          textoEnviando={TEXTOS_ACCION_PUBLICACION[dialogo.tipo].enviando}
          peligro={dialogo.tipo !== 'reactivar'}
          motivo={dialogo.tipo === 'inactivar' ? 'obligatorio' : 'no'}
          validarMotivo={validarMotivoInactivar}
          enviando={dialogo.enviando}
          erroresMotivo={dialogo.erroresMotivo}
          error={dialogo.error}
          onConfirmar={(m) => void confirmarDialogo(m)}
          onCancelar={cancelarDialogo}
        />
      )}
    </div>
  )
}
