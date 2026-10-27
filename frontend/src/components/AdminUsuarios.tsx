// Panel de administración de usuarios (HU-043, HU-044, HU-047, HU-048, RN-SE-006, D-3).
// Solo se muestra a quien tiene el rol ADMINISTRADOR; la API lo protege igualmente.
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import {
  BUSCAR_MAX,
  ejecutarAccionUsuario,
  listarUsuarios,
  TAMANO_PAGINA,
  type AccionUsuario,
  type FiltrosUsuarios,
  type ListaUsuarios,
  type UsuarioAdmin,
} from '../api/admin'
import { TIPOS_DOCUMENTO, type EstadoCuenta } from '../api/auth'
import {
  ApiError,
  MENSAJE_INESPERADO,
  MENSAJE_SIN_PERMISO,
  type ErroresPorCampo,
} from '../api/http'
import { esAdministrador, nombreRol, ROL_ASESOR, tieneRol } from '../auth/roles'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo, CampoSelect } from './Campo'
import { DialogoAccion, TEXTOS_ACCION } from './DialogoAccion'

type AdminUsuariosProps = {
  token: string
  /** Id del administrador en sesión: nunca se ofrecen acciones sobre su propia cuenta. */
  usuarioIdPropio: string
  /** 401: la sesión ya no vale (igual que en P1). */
  onSesionTerminada: () => void
  onVolver: () => void
}

const OPCIONES_ESTADO: readonly { valor: EstadoCuenta; nombre: string }[] = [
  { valor: 'PENDIENTE', nombre: 'Pendiente' },
  { valor: 'ACTIVA', nombre: 'Activa (asesor aprobado)' },
  { valor: 'RECHAZADA', nombre: 'Rechazada' },
  { valor: 'BLOQUEADA', nombre: 'Bloqueada' },
]

const OPCIONES_ROL: readonly { valor: string; nombre: string }[] = [
  { valor: 'COMPRADOR', nombre: 'Comprador' },
  { valor: 'ASESOR', nombre: 'Asesor' },
  { valor: 'ADMINISTRADOR', nombre: 'Administrador' },
]

const ESTADOS_ASESOR: Record<string, string> = {
  ACTIVA: 'Aprobado',
  PENDIENTE: 'Pendiente',
  RECHAZADA: 'Rechazado',
  BLOQUEADA: 'Bloqueado',
}

const ESTADOS_CUENTA: Record<string, string> = {
  ACTIVA: 'Activa',
  PENDIENTE: 'Pendiente',
  RECHAZADA: 'Rechazada',
  BLOQUEADA: 'Bloqueada',
}

const EXITO_ACCION: Record<AccionUsuario, (nombre: string) => string> = {
  aprobar: (n) => `Se aprobó la cuenta de asesor de ${n}.`,
  rechazar: (n) => `Se rechazó la solicitud de asesor de ${n}.`,
  bloquear: (n) => `Se bloqueó la cuenta de ${n}.`,
  desbloquear: (n) => `Se desbloqueó la cuenta de ${n}.`,
  reconsiderar: (n) => `Se reconsideró y aprobó la cuenta de asesor de ${n}.`,
}

const FILTROS_INICIALES: FiltrosUsuarios = {
  estado: 'PENDIENTE',
  rol: '',
  buscar: '',
  pagina: 1,
  tamano: TAMANO_PAGINA,
}

function esAsesor(u: UsuarioAdmin): boolean {
  return u.asesor !== null || tieneRol(u.roles, ROL_ASESOR)
}

/** Estado con texto (no solo color). Los asesores usan los nombres de D-10. */
export function etiquetaEstado(u: UsuarioAdmin): string {
  const tabla = esAsesor(u) ? ESTADOS_ASESOR : ESTADOS_CUENTA
  return tabla[u.estado] ?? (u.estado || 'Desconocido')
}

/** Transiciones válidas del contrato P2; cualquier otra no se ofrece. */
export function accionesValidas(u: UsuarioAdmin, usuarioIdPropio: string): AccionUsuario[] {
  if (u.usuarioId === usuarioIdPropio || esAdministrador(u.roles)) return []
  switch (u.estado) {
    case 'PENDIENTE':
      return esAsesor(u) ? ['aprobar', 'rechazar'] : []
    case 'ACTIVA':
      return ['bloquear']
    case 'BLOQUEADA':
      return ['desbloquear']
    case 'RECHAZADA':
      return esAsesor(u) ? ['reconsiderar'] : []
    default:
      return []
  }
}

function nombreTipoDocumento(tipo: string): string {
  return TIPOS_DOCUMENTO.find((t) => t.valor === tipo)?.nombre ?? tipo
}

const FORMATO_FECHA = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })

function formatearFecha(valor: string): string {
  const ms = Date.parse(valor)
  return Number.isNaN(ms) ? '—' : FORMATO_FECHA.format(ms)
}

type CampoFiltro = 'estado' | 'rol' | 'buscar'
type ErroresFiltros = Partial<Record<CampoFiltro, string[]>>
const CAMPOS_FILTRO: readonly CampoFiltro[] = ['estado', 'rol', 'buscar']

function repartirErroresFiltros(errores: ErroresPorCampo): {
  porCampo: ErroresFiltros
  otros: string[]
} {
  const porCampo: ErroresFiltros = {}
  const otros: string[] = []
  for (const [clave, mensajes] of Object.entries(errores)) {
    const campo = CAMPOS_FILTRO.find((c) => c === clave.toLowerCase())
    if (campo) porCampo[campo] = [...(porCampo[campo] ?? []), ...mensajes]
    else for (const m of mensajes) if (!otros.includes(m)) otros.push(m)
  }
  return { porCampo, otros }
}

type Dialogo = {
  usuario: UsuarioAdmin
  accion: AccionUsuario
  enviando: boolean
  erroresMotivo: string[]
  error: DatosAviso | null
}

export function AdminUsuarios({
  token,
  usuarioIdPropio,
  onSesionTerminada,
  onVolver,
}: AdminUsuariosProps) {
  const [filtros, setFiltros] = useState<FiltrosUsuarios>(FILTROS_INICIALES)
  const [borrador, setBorrador] = useState({
    estado: FILTROS_INICIALES.estado as string,
    rol: FILTROS_INICIALES.rol,
    buscar: FILTROS_INICIALES.buscar,
  })
  const [recarga, setRecarga] = useState(0)
  const [lista, setLista] = useState<ListaUsuarios | null>(null)
  const [cargando, setCargando] = useState(true)
  const [errorCarga, setErrorCarga] = useState<DatosAviso | null>(null)
  /** 400 de la lista: errores por parámetro (estado, rol, buscar, pagina, tamano). */
  const [erroresFiltros, setErroresFiltros] = useState<ErroresFiltros>({})
  const [resultado, setResultado] = useState<DatosAviso | null>(null)
  const [dialogo, setDialogo] = useState<Dialogo | null>(null)

  const titulo = useRef<HTMLHeadingElement>(null)
  const tituloResultados = useRef<HTMLHeadingElement>(null)
  /** Botón que abrió el diálogo, para devolverle el foco al cancelar. */
  const origenDialogo = useRef<HTMLButtonElement | null>(null)

  useEffect(() => {
    titulo.current?.focus()
  }, [])

  const mensajeDe = useCallback((error: unknown): string => {
    if (!(error instanceof ApiError)) return MENSAJE_INESPERADO
    if (error.status === 403) return error.mensaje || MENSAJE_SIN_PERMISO
    return error.mensaje
  }, [])

  // Carga de la lista cada vez que cambian los filtros aplicados o tras una acción.
  useEffect(() => {
    const control = new AbortController()
    setCargando(true)
    setErrorCarga(null)
    setErroresFiltros({})
    listarUsuarios(token, filtros, control.signal)
      .then((respuesta) => {
        // Si la página quedó vacía (p. ej. tras aprobar el último de la página), se retrocede.
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
        if (error instanceof ApiError && error.status === 403) setLista(null)
        if (error instanceof ApiError && error.status === 400) {
          // "Revisa los filtros." + cada mensaje junto a su filtro; pagina/tamano van al aviso.
          const { porCampo, otros } = repartirErroresFiltros(error.errores)
          setLista(null)
          setErroresFiltros(porCampo)
          setErrorCarga({ tipo: 'error', texto: [error.mensaje, ...otros].join(' ') })
          return
        }
        setErrorCarga({ tipo: 'error', texto: mensajeDe(error) })
      })
    return () => control.abort()
  }, [token, filtros, recarga, onSesionTerminada, mensajeDe])

  function aplicarFiltros(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    setResultado(null)
    setFiltros({
      estado: borrador.estado as FiltrosUsuarios['estado'],
      rol: borrador.rol,
      buscar: borrador.buscar.trim(),
      pagina: 1,
      tamano: TAMANO_PAGINA,
    })
  }

  function irAPagina(pagina: number) {
    setResultado(null)
    setFiltros((f) => ({ ...f, pagina }))
    tituloResultados.current?.focus()
  }

  function abrirDialogo(
    usuario: UsuarioAdmin,
    accion: AccionUsuario,
    boton: HTMLButtonElement,
  ) {
    origenDialogo.current = boton
    setResultado(null)
    setDialogo({ usuario, accion, enviando: false, erroresMotivo: [], error: null })
  }

  function cancelarDialogo() {
    setDialogo(null)
    const boton = origenDialogo.current
    origenDialogo.current = null
    // Se espera al siguiente ciclo para que el diálogo ya no exista.
    window.setTimeout(() => boton?.focus(), 0)
  }

  /** Cierra el diálogo, muestra el resultado y recarga la lista. */
  function terminarAccion(aviso: DatosAviso) {
    setDialogo(null)
    origenDialogo.current = null
    setResultado(aviso)
    setRecarga((r) => r + 1)
    // La fila puede desaparecer al recargar: el foco va al encabezado de resultados.
    window.setTimeout(() => tituloResultados.current?.focus(), 0)
  }

  async function confirmar(motivo: string) {
    if (!dialogo || dialogo.enviando) return
    const { usuario, accion } = dialogo
    setDialogo({ ...dialogo, enviando: true, erroresMotivo: [], error: null })
    try {
      const actualizado = await ejecutarAccionUsuario(token, usuario.usuarioId, accion, motivo)
      const nombre = actualizado?.nombreCompleto || usuario.nombreCompleto || usuario.email
      terminarAccion({ tipo: 'exito', texto: EXITO_ACCION[accion](nombre) })
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
          d && {
            ...d,
            enviando: false,
            erroresMotivo: errores.length > 0 ? errores : [error.mensaje],
          },
        )
        return
      }
      if (error instanceof ApiError && [403, 404, 409].includes(error.status)) {
        // El estado de la cuenta pudo cambiar: se informa y se recarga la lista.
        terminarAccion({ tipo: 'error', texto: mensajeDe(error) })
        return
      }
      // Sin conexión o error del servidor: el diálogo sigue abierto para reintentar.
      setDialogo((d) => d && { ...d, enviando: false, error: { tipo: 'error', texto: mensajeDe(error) } })
    }
  }

  const totalPaginas = lista ? Math.max(1, Math.ceil(lista.total / Math.max(1, lista.tamano))) : 1
  const paginaActual = lista?.pagina ?? filtros.pagina

  return (
    <section className="panel-ancho" aria-labelledby="admin-titulo">
      <h2 id="admin-titulo" ref={titulo} tabIndex={-1}>
        Administración de usuarios
      </h2>
      <p className="form-ayuda">
        Aprueba o rechaza las solicitudes de asesor y bloquea o desbloquea cuentas. Por defecto se
        muestran las solicitudes pendientes.
      </p>

      <form className="filtros" onSubmit={aplicarFiltros} aria-label="Filtros de usuarios">
        <CampoSelect
          id="admin-estado"
          etiqueta="Estado"
          valor={borrador.estado}
          onCambio={(v) => setBorrador((b) => ({ ...b, estado: v }))}
          opciones={OPCIONES_ESTADO}
          textoVacio="Todos los estados"
          errores={erroresFiltros.estado}
          requerido={false}
        />
        <CampoSelect
          id="admin-rol"
          etiqueta="Rol"
          valor={borrador.rol}
          onCambio={(v) => setBorrador((b) => ({ ...b, rol: v }))}
          opciones={OPCIONES_ROL}
          textoVacio="Todos los roles"
          errores={erroresFiltros.rol}
          requerido={false}
        />
        <Campo
          id="admin-buscar"
          etiqueta="Buscar por nombre o correo"
          tipo="search"
          valor={borrador.buscar}
          onCambio={(v) => setBorrador((b) => ({ ...b, buscar: v }))}
          maxLength={BUSCAR_MAX}
          errores={erroresFiltros.buscar}
          autoComplete="off"
          requerido={false}
        />
        <button type="submit" disabled={cargando}>
          Aplicar filtros
        </button>
      </form>

      <h3 ref={tituloResultados} tabIndex={-1} className="admin-subtitulo">
        Resultados
        {lista && !cargando && (
          <span className="admin-total">
            {' '}
            ({lista.total} {lista.total === 1 ? 'usuario' : 'usuarios'})
          </span>
        )}
      </h3>

      <Aviso aviso={resultado} />
      <Aviso aviso={errorCarga} />
      {cargando && <p role="status">Cargando usuarios...</p>}

      {lista && lista.items.length === 0 && !cargando && !errorCarga && (
        <p className="admin-vacio">No hay usuarios con estos filtros.</p>
      )}

      {lista && lista.items.length > 0 && (
        <div
          className="tabla-contenedor"
          role="region"
          aria-labelledby="admin-tabla-titulo"
          tabIndex={0}
          aria-busy={cargando}
        >
          <table className="tabla-usuarios">
            <caption id="admin-tabla-titulo" className="sr-only">
              Usuarios registrados, página {paginaActual} de {totalPaginas}
            </caption>
            <thead>
              <tr>
                <th scope="col">Nombre</th>
                <th scope="col">Correo</th>
                <th scope="col">Teléfono</th>
                <th scope="col">Rol</th>
                <th scope="col">Estado</th>
                <th scope="col">Documento</th>
                <th scope="col">Fecha de registro</th>
                <th scope="col">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {lista.items.map((u) => {
                const acciones = accionesValidas(u, usuarioIdPropio)
                const nombre = u.nombreCompleto || u.email
                return (
                  <tr key={u.usuarioId}>
                    <th scope="row">
                      {u.nombreCompleto || '—'}
                      {u.asesor?.nombreComercial && (
                        <span className="dato-secundario">
                          <span className="sr-only">, nombre comercial: </span>
                          {u.asesor.nombreComercial}
                        </span>
                      )}
                    </th>
                    <td className="celda-correo">{u.email}</td>
                    <td>{u.telefono || '—'}</td>
                    <td>{u.roles.map(nombreRol).join(', ') || '—'}</td>
                    <td>
                      <span className={`estado estado-${u.estado.toLowerCase() || 'desconocido'}`}>
                        {etiquetaEstado(u)}
                      </span>
                    </td>
                    <td>
                      {u.asesor ? (
                        <>
                          <abbr title={nombreTipoDocumento(u.asesor.tipoDocumento)}>
                            {u.asesor.tipoDocumento}
                          </abbr>{' '}
                          {u.asesor.numeroDocumento}
                        </>
                      ) : (
                        '—'
                      )}
                    </td>
                    <td>{formatearFecha(u.creadoEn)}</td>
                    <td>
                      {acciones.length === 0 ? (
                        <span className="dato-secundario">Sin acciones</span>
                      ) : (
                        <div className="acciones-fila">
                          {acciones.map((accion) => (
                            <button
                              key={accion}
                              type="button"
                              className={
                                accion === 'rechazar' || accion === 'bloquear'
                                  ? 'boton-peligro'
                                  : undefined
                              }
                              disabled={dialogo !== null}
                              onClick={(e) => abrirDialogo(u, accion, e.currentTarget)}
                            >
                              {TEXTOS_ACCION[accion].boton}{' '}
                              <span className="sr-only">a {nombre}</span>
                            </button>
                          ))}
                        </div>
                      )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      {lista && lista.total > 0 && (
        <nav className="paginacion" aria-label="Paginación de usuarios">
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

      <button className="secondary-button" type="button" onClick={onVolver}>
        Volver a mi perfil
      </button>

      {dialogo && (
        <DialogoAccion
          usuario={dialogo.usuario}
          accion={dialogo.accion}
          enviando={dialogo.enviando}
          erroresMotivo={dialogo.erroresMotivo}
          error={dialogo.error}
          onConfirmar={(motivo) => void confirmar(motivo)}
          onCancelar={cancelarDialogo}
        />
      )}
    </section>
  )
}
