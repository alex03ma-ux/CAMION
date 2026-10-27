// Lista del inventario (HU-020, HU-021 inventario propio, HU-022 inventario general,
// RN-CO-009, RN-AD-001, D-7). El asesor ve lo suyo y el administrador todo; la API
// lo aplica igualmente (un vehículo ajeno responde 404).
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import {
  cambiarEstadoVehiculo,
  ESTADOS_VEHICULO,
  listarInventario,
  Q_MAX_INVENTARIO,
  TAMANO_PAGINA_INVENTARIO,
  type AccionEstado,
  type CatalogosInventario,
  type FiltrosInventario,
  type ItemInventario,
  type ListaInventario,
} from '../api/inventario'
import { ApiError, MENSAJE_INESPERADO, type ErroresPorCampo } from '../api/http'
import { formatoCop, formatoMiles } from '../catalogo/formato'
import {
  accionesEstado,
  NOMBRES_ESTADO,
  nombreEstado,
  TEXTOS_ACCION_ESTADO,
  tituloVehiculo,
  validarMotivoInactivar,
} from '../inventario/estados'
import { categoriaDeTipo } from '../inventario/reglasCategoria'
import { HASH_INVENTARIO_NUEVO, hashInventarioVehiculo } from '../rutas'
import { descartarAvisoPendiente } from '../inventario/avisoPendiente'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo, CampoSelect } from './Campo'
import { DialogoConfirmacion } from './DialogoAccion'
import { ImagenVehiculo } from './ImagenVehiculo'

type InventarioListaProps = {
  token: string
  esAdmin: boolean
  /** null mientras cargan o si fallaron (los filtros de listas quedan vacíos). */
  catalogos: CatalogosInventario | null
  /** Aviso que dejó otra pantalla (p. ej. tras registrar un vehículo). */
  avisoInicial: DatosAviso | null
  onSesionTerminada: () => void
}

const FILTROS_INICIALES: FiltrosInventario = {
  estado: '',
  tipoId: '',
  q: '',
  asesorId: '',
  pagina: 1,
  tamano: TAMANO_PAGINA_INVENTARIO,
}

// Últimos filtros usados, para conservarlos al volver desde el formulario.
let ultimosFiltros: FiltrosInventario = FILTROS_INICIALES

/** Solo para pruebas: olvida los últimos filtros. */
export function olvidarFiltrosInventario(): void {
  ultimosFiltros = FILTROS_INICIALES
}

function hayFiltros(f: FiltrosInventario): boolean {
  return f.estado !== '' || f.tipoId !== '' || f.q !== '' || f.asesorId !== ''
}

type CampoFiltro = 'estado' | 'tipoId' | 'q' | 'asesorId'
const CAMPOS_FILTRO: readonly CampoFiltro[] = ['estado', 'tipoId', 'q', 'asesorId']

function repartirErroresFiltros(errores: ErroresPorCampo) {
  const porCampo: Partial<Record<CampoFiltro, string[]>> = {}
  const otros: string[] = []
  for (const [clave, mensajes] of Object.entries(errores)) {
    const campo = CAMPOS_FILTRO.find((c) => c.toLowerCase() === clave.toLowerCase())
    if (campo) porCampo[campo] = [...(porCampo[campo] ?? []), ...mensajes]
    else for (const m of mensajes) if (!otros.includes(m)) otros.push(m)
  }
  return { porCampo, otros }
}

const FORMATO_FECHA = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })

function formatearFecha(valor: string): string {
  const ms = Date.parse(valor)
  return Number.isNaN(ms) ? '—' : FORMATO_FECHA.format(ms)
}

/** Kilometraje u horas según la categoría (D-17, D-19). */
function datoUso(item: ItemInventario): string | null {
  const categoria = categoriaDeTipo(item.tipo)
  if (categoria === 'MAQUINARIA') {
    return item.horasUso !== null ? `${formatoMiles(item.horasUso)} horas de uso` : null
  }
  if (categoria === 'CARROCERIA') return null
  return item.kilometraje !== null ? `${formatoMiles(item.kilometraje)} km` : null
}

type DialogoInactivar = {
  item: ItemInventario
  enviando: boolean
  erroresMotivo: string[]
  error: DatosAviso | null
}

export function InventarioLista({
  token,
  esAdmin,
  catalogos,
  avisoInicial,
  onSesionTerminada,
}: InventarioListaProps) {
  const [filtros, setFiltros] = useState<FiltrosInventario>(ultimosFiltros)
  const [borrador, setBorrador] = useState({
    estado: ultimosFiltros.estado,
    tipoId: ultimosFiltros.tipoId,
    q: ultimosFiltros.q,
    asesorId: ultimosFiltros.asesorId,
  })
  const [recarga, setRecarga] = useState(0)
  const [lista, setLista] = useState<ListaInventario | null>(null)
  const [cargando, setCargando] = useState(true)
  const [errorCarga, setErrorCarga] = useState<DatosAviso | null>(null)
  const [erroresFiltros, setErroresFiltros] = useState<Partial<Record<CampoFiltro, string[]>>>({})
  const [resultado, setResultado] = useState<DatosAviso | null>(avisoInicial)
  /** Id del vehículo con una acción directa en curso (reservar, liberar, reactivar). */
  const [enCurso, setEnCurso] = useState<{ id: number; accion: AccionEstado } | null>(null)
  const [dialogo, setDialogo] = useState<DialogoInactivar | null>(null)

  const tituloResultados = useRef<HTMLHeadingElement>(null)
  const origenDialogo = useRef<HTMLButtonElement | null>(null)

  useEffect(() => {
    ultimosFiltros = filtros
  }, [filtros])

  // El aviso que dejó la pantalla anterior ya se mostró: se descarta.
  useEffect(() => {
    descartarAvisoPendiente()
  }, [])

  const mensajeDe = useCallback((error: unknown): string => {
    return error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO
  }, [])

  useEffect(() => {
    const control = new AbortController()
    setCargando(true)
    setErrorCarga(null)
    setErroresFiltros({})
    listarInventario(token, filtros, esAdmin, control.signal)
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
        if (error instanceof ApiError && error.status === 401) {
          onSesionTerminada()
          return
        }
        setLista(null)
        if (error instanceof ApiError && error.status === 400) {
          const { porCampo, otros } = repartirErroresFiltros(error.errores)
          setErroresFiltros(porCampo)
          setErrorCarga({ tipo: 'error', texto: [error.mensaje, ...otros].join(' ') })
          return
        }
        setErrorCarga({ tipo: 'error', texto: mensajeDe(error) })
      })
    return () => control.abort()
  }, [token, esAdmin, filtros, recarga, onSesionTerminada, mensajeDe])

  function aplicarFiltros(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    setResultado(null)
    setFiltros({
      estado: borrador.estado,
      tipoId: borrador.tipoId,
      q: borrador.q.trim(),
      asesorId: esAdmin ? borrador.asesorId : '',
      pagina: 1,
      tamano: TAMANO_PAGINA_INVENTARIO,
    })
  }

  function limpiarFiltros() {
    setResultado(null)
    setBorrador({ estado: '', tipoId: '', q: '', asesorId: '' })
    setFiltros(FILTROS_INICIALES)
  }

  function irAPagina(pagina: number) {
    setResultado(null)
    setFiltros((f) => ({ ...f, pagina }))
    tituloResultados.current?.focus()
  }

  /** Muestra el resultado, recarga y lleva el foco al encabezado de resultados. */
  function terminarAccion(aviso: DatosAviso) {
    setResultado(aviso)
    setRecarga((r) => r + 1)
    window.setTimeout(() => tituloResultados.current?.focus(), 0)
  }

  async function accionDirecta(item: ItemInventario, accion: AccionEstado) {
    if (enCurso || dialogo) return
    setResultado(null)
    setEnCurso({ id: item.id, accion })
    try {
      await cambiarEstadoVehiculo(token, item.id, accion)
      terminarAccion({ tipo: 'exito', texto: TEXTOS_ACCION_ESTADO[accion].exito(tituloVehiculo(item)) })
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        onSesionTerminada()
        return
      }
      // 404 o 409: el estado cambió en otra pestaña o por otra persona; se recarga.
      terminarAccion({ tipo: 'error', texto: mensajeDe(error) })
    } finally {
      setEnCurso(null)
    }
  }

  function abrirInactivar(item: ItemInventario, boton: HTMLButtonElement) {
    origenDialogo.current = boton
    setResultado(null)
    setDialogo({ item, enviando: false, erroresMotivo: [], error: null })
  }

  function cancelarDialogo() {
    setDialogo(null)
    const boton = origenDialogo.current
    origenDialogo.current = null
    window.setTimeout(() => boton?.focus(), 0)
  }

  async function confirmarInactivar(motivo: string) {
    if (!dialogo || dialogo.enviando) return
    const { item } = dialogo
    setDialogo({ ...dialogo, enviando: true, erroresMotivo: [], error: null })
    try {
      await cambiarEstadoVehiculo(token, item.id, 'inactivar', motivo)
      setDialogo(null)
      origenDialogo.current = null
      terminarAccion({ tipo: 'exito', texto: TEXTOS_ACCION_ESTADO.inactivar.exito(tituloVehiculo(item)) })
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setDialogo(null)
        onSesionTerminada()
        return
      }
      if (error instanceof ApiError && error.status === 400) {
        const errores = Object.entries(error.errores)
          .filter(([campo]) => campo.toLowerCase() === 'motivo')
          .flatMap(([, mensajes]) => mensajes)
        setDialogo((d) =>
          d && { ...d, enviando: false, erroresMotivo: errores.length > 0 ? errores : [error.mensaje] },
        )
        return
      }
      if (error instanceof ApiError && [403, 404, 409].includes(error.status)) {
        setDialogo(null)
        origenDialogo.current = null
        terminarAccion({ tipo: 'error', texto: mensajeDe(error) })
        return
      }
      setDialogo((d) => d && { ...d, enviando: false, error: { tipo: 'error', texto: mensajeDe(error) } })
    }
  }

  const opcionesEstado = ESTADOS_VEHICULO.map((e) => ({ valor: e, nombre: NOMBRES_ESTADO[e] }))
  const opcionesTipo = (catalogos?.tipos ?? []).map((t) => ({ valor: String(t.id), nombre: t.nombre }))
  const opcionesAsesor = (catalogos?.asesores ?? []).map((a) => ({ valor: a.id, nombre: a.nombre }))
  const totalPaginas = lista ? Math.max(1, Math.ceil(lista.total / Math.max(1, lista.tamano))) : 1
  const paginaActual = lista?.pagina ?? filtros.pagina
  const ocupado = enCurso !== null || dialogo !== null

  return (
    <>
      <div className="inv-barra">
        <a className="inv-boton-enlace" href={HASH_INVENTARIO_NUEVO}>
          Registrar vehículo
        </a>
      </div>

      <form
        className="inv-filtros"
        onSubmit={aplicarFiltros}
        aria-label="Filtros del inventario"
        noValidate
      >
        <CampoSelect
          id="inv-filtro-estado"
          etiqueta="Estado"
          valor={borrador.estado}
          onCambio={(v) => setBorrador((b) => ({ ...b, estado: v }))}
          opciones={opcionesEstado}
          textoVacio="Todos los estados"
          errores={erroresFiltros.estado}
          requerido={false}
        />
        <CampoSelect
          id="inv-filtro-tipo"
          etiqueta="Categoría"
          valor={borrador.tipoId}
          onCambio={(v) => setBorrador((b) => ({ ...b, tipoId: v }))}
          opciones={opcionesTipo}
          textoVacio="Todas las categorías"
          errores={erroresFiltros.tipoId}
          requerido={false}
        />
        {esAdmin && (
          <CampoSelect
            id="inv-filtro-asesor"
            etiqueta="Asesor"
            valor={borrador.asesorId}
            onCambio={(v) => setBorrador((b) => ({ ...b, asesorId: v }))}
            opciones={opcionesAsesor}
            textoVacio="Todos los asesores"
            errores={erroresFiltros.asesorId}
            requerido={false}
          />
        )}
        <Campo
          id="inv-filtro-q"
          etiqueta="Buscar por placa, marca, línea o descripción"
          tipo="search"
          valor={borrador.q}
          onCambio={(v) => setBorrador((b) => ({ ...b, q: v }))}
          maxLength={Q_MAX_INVENTARIO}
          errores={erroresFiltros.q}
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

      <h2 ref={tituloResultados} tabIndex={-1} className="inv-subtitulo">
        Vehículos
        {lista && !cargando && (
          <span className="admin-total">
            {' '}
            ({formatoMiles(lista.total)} {lista.total === 1 ? 'vehículo' : 'vehículos'})
          </span>
        )}
      </h2>

      <Aviso aviso={resultado} />
      <Aviso aviso={errorCarga} />
      {cargando && <p role="status">Cargando inventario...</p>}

      {lista && lista.items.length === 0 && !cargando && !errorCarga && (
        <p className="admin-vacio">
          {!hayFiltros(filtros)
            ? esAdmin
              ? 'Aún no hay vehículos registrados.'
              : 'Aún no tienes vehículos registrados.'
            : 'No hay vehículos con estos filtros.'}
        </p>
      )}

      {lista && lista.items.length > 0 && (
        <ul className="inv-lista" aria-busy={cargando}>
          {lista.items.map((item) => {
            const titulo = tituloVehiculo(item)
            const acciones = accionesEstado(item.estado)
            const uso = datoUso(item)
            const idTitulo = `inv-item-${item.id}`
            return (
              <li key={item.id} className="inv-tarjeta">
                <div className="inv-tarjeta-foto">
                  <ImagenVehiculo src={item.fotoPortada} alt="" />
                </div>
                <div className="inv-tarjeta-cuerpo">
                  <h3 id={idTitulo} className="inv-tarjeta-titulo">
                    {titulo}
                  </h3>
                  <p className="inv-tarjeta-precio">{formatoCop(item.precioCop)}</p>
                  <p>
                    <span className={`estado inv-estado-${item.estado.toLowerCase() || 'desconocido'}`}>
                      <span className="sr-only">Estado: </span>
                      {nombreEstado(item.estado)}
                    </span>
                  </p>
                  <dl className="inv-datos">
                    <div>
                      <dt>Categoría</dt>
                      <dd>{item.tipo || '—'}</dd>
                    </div>
                    <div>
                      <dt>Placa</dt>
                      <dd>{item.placa ?? 'Sin placa'}</dd>
                    </div>
                    {uso && (
                      <div>
                        <dt>Uso</dt>
                        <dd>{uso}</dd>
                      </div>
                    )}
                    <div>
                      <dt>Ubicación</dt>
                      <dd>{item.municipio ?? '—'}</dd>
                    </div>
                    <div>
                      <dt>Fotos</dt>
                      <dd>{item.totalFotos} de 10</dd>
                    </div>
                    {esAdmin && item.asesor && (
                      <div>
                        <dt>Asesor</dt>
                        <dd>{item.asesor.nombre || '—'}</dd>
                      </div>
                    )}
                    <div>
                      <dt>Actualizado</dt>
                      <dd>{formatearFecha(item.actualizadoEn)}</dd>
                    </div>
                  </dl>
                  <div className="acciones-fila inv-acciones">
                    <a className="inv-boton-enlace inv-boton-pequeno" href={hashInventarioVehiculo(item.id)}>
                      {item.estado === 'VENDIDO' ? 'Ver' : 'Editar'}
                      {' '}<span className="sr-only">{titulo}</span>
                    </a>
                    {acciones.map((accion) => {
                      const textos = TEXTOS_ACCION_ESTADO[accion]
                      const enviando = enCurso?.id === item.id && enCurso.accion === accion
                      return (
                        <button
                          key={accion}
                          type="button"
                          className={accion === 'inactivar' ? 'boton-peligro' : 'secondary-button'}
                          disabled={ocupado}
                          onClick={(e) =>
                            accion === 'inactivar'
                              ? abrirInactivar(item, e.currentTarget)
                              : void accionDirecta(item, accion)
                          }
                        >
                          {enviando ? textos.enviando : textos.boton}
                          {' '}<span className="sr-only">{titulo}</span>
                        </button>
                      )
                    })}
                    {acciones.length === 0 && (
                      <span className="dato-secundario">
                        Un vehículo vendido no se puede modificar.
                      </span>
                    )}
                  </div>
                </div>
              </li>
            )
          })}
        </ul>
      )}

      {lista && lista.total > 0 && totalPaginas > 1 && (
        <nav className="paginacion" aria-label="Paginación del inventario">
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

      {dialogo && (
        <DialogoConfirmacion
          titulo="Inactivar vehículo"
          descripcion={`¿Inactivar ${tituloVehiculo(dialogo.item)}? Dejará de mostrarse en el catálogo hasta que lo reactives.`}
          textoBoton={TEXTOS_ACCION_ESTADO.inactivar.boton}
          textoEnviando={TEXTOS_ACCION_ESTADO.inactivar.enviando}
          peligro
          motivo="obligatorio"
          validarMotivo={validarMotivoInactivar}
          enviando={dialogo.enviando}
          erroresMotivo={dialogo.erroresMotivo}
          error={dialogo.error}
          onConfirmar={(m) => void confirmarInactivar(m)}
          onCancelar={cancelarDialogo}
        />
      )}
    </>
  )
}
