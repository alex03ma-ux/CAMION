// P5 — Contactos (leads) en el panel: #/contactos y #/contactos/<id> (HU-031 a HU-033, RN-CL-003, RN-CL-004,
// D-7, D-46 B, D-48). El asesor ve solo los suyos ("Mis contactos"); el administrador ve todos ("Contactos").
// Un comprador ve "No tienes permiso" sin llamar a /api/leads. La interfaz solo oculta: la API protege cada
// acción (401, 403, 404 si el contacto no es suyo, 409 si el estado cambió).
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import type { PerfilUsuario } from '../api/auth'
import { ApiError, MENSAJE_INESPERADO, MENSAJE_SIN_PERMISO } from '../api/http'
import { obtenerCatalogosInventario, type ResumenAsesor } from '../api/inventario'
import {
  listarLeads,
  NOMBRES_ESTADO_LEAD,
  nombreEstadoLead,
  nombreEstadoVehiculo,
  nombreResponsable,
  nombreResultado,
  RESPONSABLE_ADMINISTRACION,
  TAMANO_PAGINA_LEADS,
  validarFiltrosLeads,
  formatearFechaHora,
  type EstadoLead,
  type FiltrosLeads,
  type LeadItem,
  type ListaLeads,
} from '../api/leads'
import { formatoMiles } from '../catalogo/formato'
import { HASH_CUENTA, hashContacto } from '../rutas'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo, CampoSelect } from './Campo'
import { DetalleContacto, type VehiculoFiltro } from './DetalleContacto'
import { modoInventario } from './InventarioPagina'

type ContactosProps = {
  token: string
  perfil: PerfilUsuario
  /** Id del contacto abierto (#/contactos/<id>); sin id, la lista. */
  id?: number
  onSesionTerminada: () => void
}

/** Título de la sección según el rol (asesor: los suyos; administrador: todos). */
export function tituloContactos(esAdmin: boolean): string {
  return esAdmin ? 'Contactos' : 'Mis contactos'
}

export function Contactos({ token, perfil, id, onSesionTerminada }: ContactosProps) {
  const modo = modoInventario(perfil.roles)
  if (modo === null) {
    return (
      <div className="cat-pagina inv-pagina">
        <h1 tabIndex={-1} className="inv-titulo">
          Contactos
        </h1>
        <Aviso aviso={{ tipo: 'error', texto: MENSAJE_SIN_PERMISO }} />
        <p>
          <a href={HASH_CUENTA}>Volver a mi cuenta</a>
        </p>
      </div>
    )
  }
  return <PanelContactos token={token} esAdmin={modo === 'administrador'} id={id} onSesionTerminada={onSesionTerminada} />
}

const FILTROS_INICIALES: FiltrosLeads = {
  estado: '',
  responsable: '',
  vehiculoId: '',
  desde: '',
  hasta: '',
  pagina: 1,
  tamano: TAMANO_PAGINA_LEADS,
}

type Borrador = { estado: string; responsable: string; desde: string; hasta: string }
const BORRADOR_INICIAL: Borrador = { estado: '', responsable: '', desde: '', hasta: '' }
type ErroresFiltro = Partial<Record<'estado' | 'responsable' | 'desde' | 'hasta', string[]>>

function PanelContactos({
  token,
  esAdmin,
  id,
  onSesionTerminada,
}: {
  token: string
  esAdmin: boolean
  id: number | undefined
  onSesionTerminada: () => void
}) {
  // Los filtros viven aquí para conservarse al ir al detalle y volver a la lista.
  const [filtros, setFiltros] = useState<FiltrosLeads>(FILTROS_INICIALES)
  const [borrador, setBorrador] = useState<Borrador>(BORRADOR_INICIAL)
  const [vehiculo, setVehiculo] = useState<VehiculoFiltro | null>(null)
  const [asesores, setAsesores] = useState<ResumenAsesor[]>([])

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

  // Asesores (solo administrador): filtro por responsable y destinos de la reasignación.
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

  if (id !== undefined) {
    return (
      <DetalleContacto
        key={id}
        token={token}
        id={id}
        esAdmin={esAdmin}
        asesores={asesores}
        onSesionTerminada={onSesionTerminada}
        onFiltrarVehiculo={(v) => {
          setVehiculo(v)
          setFiltros((f) => ({ ...f, vehiculoId: String(v.vehiculoId), pagina: 1 }))
        }}
      />
    )
  }

  return (
    <ListaContactos
      token={token}
      esAdmin={esAdmin}
      asesores={asesores}
      filtros={filtros}
      setFiltros={setFiltros}
      borrador={borrador}
      setBorrador={setBorrador}
      vehiculo={vehiculo}
      setVehiculo={setVehiculo}
      alError401={alError401}
    />
  )
}

type ListaContactosProps = {
  token: string
  esAdmin: boolean
  asesores: readonly ResumenAsesor[]
  filtros: FiltrosLeads
  setFiltros: (f: FiltrosLeads | ((f: FiltrosLeads) => FiltrosLeads)) => void
  borrador: Borrador
  setBorrador: (b: Borrador | ((b: Borrador) => Borrador)) => void
  vehiculo: VehiculoFiltro | null
  setVehiculo: (v: VehiculoFiltro | null) => void
  alError401: (error: unknown) => boolean
}

function ListaContactos({
  token,
  esAdmin,
  asesores,
  filtros,
  setFiltros,
  borrador,
  setBorrador,
  vehiculo,
  setVehiculo,
  alError401,
}: ListaContactosProps) {
  const [lista, setLista] = useState<ListaLeads | null>(null)
  const [cargando, setCargando] = useState(true)
  const [errorCarga, setErrorCarga] = useState<DatosAviso | null>(null)
  const [erroresFiltro, setErroresFiltro] = useState<ErroresFiltro>({})
  const tituloLista = useRef<HTMLHeadingElement>(null)
  const refDesde = useRef<HTMLInputElement>(null)
  const refHasta = useRef<HTMLInputElement>(null)

  useEffect(() => {
    const control = new AbortController()
    setCargando(true)
    setErrorCarga(null)
    listarLeads(token, filtros, esAdmin, control.signal)
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
        if (error instanceof ApiError && error.status === 400) {
          const porCampo: ErroresFiltro = {}
          const otros: string[] = []
          for (const [campo, mensajes] of Object.entries(error.errores)) {
            const clave = (['estado', 'responsable', 'desde', 'hasta'] as const).find(
              (c) => c === campo.toLowerCase(),
            )
            if (clave) porCampo[clave] = mensajes
            else otros.push(...mensajes)
          }
          setErroresFiltro(porCampo)
          setErrorCarga({ tipo: 'error', texto: [error.mensaje, ...otros].join(' ') })
          return
        }
        setErrorCarga({ tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO })
      })
    return () => control.abort()
  }, [token, esAdmin, filtros, alError401, setFiltros])

  function aplicarFiltros(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    const errores = validarFiltrosLeads(borrador)
    setErroresFiltro(errores)
    if (errores.desde) {
      refDesde.current?.focus()
      return
    }
    if (errores.hasta) {
      refHasta.current?.focus()
      return
    }
    setFiltros((f) => ({
      ...f,
      estado: borrador.estado as EstadoLead | '',
      responsable: esAdmin ? borrador.responsable : '',
      desde: borrador.desde,
      hasta: borrador.hasta,
      pagina: 1,
    }))
  }

  function limpiarFiltros() {
    setErroresFiltro({})
    setBorrador(BORRADOR_INICIAL)
    setVehiculo(null)
    setFiltros(FILTROS_INICIALES)
  }

  function quitarVehiculo() {
    setVehiculo(null)
    setFiltros((f) => ({ ...f, vehiculoId: '', pagina: 1 }))
    tituloLista.current?.focus()
  }

  function irAPagina(pagina: number) {
    setFiltros((f) => ({ ...f, pagina }))
    tituloLista.current?.focus()
  }

  const totalPaginas = lista ? Math.max(1, Math.ceil(lista.total / Math.max(1, lista.tamano))) : 1
  const paginaActual = lista?.pagina ?? filtros.pagina
  const hayFiltros =
    filtros.estado !== '' || filtros.responsable !== '' || filtros.vehiculoId !== '' || filtros.desde !== '' || filtros.hasta !== ''
  const opcionesResponsable = [
    { valor: RESPONSABLE_ADMINISTRACION, nombre: 'Administración' },
    ...asesores.map((a) => ({ valor: a.id, nombre: a.nombre || `Asesor ${a.id}` })),
  ]

  return (
    <div className="cat-pagina inv-pagina contactos">
      <h1 tabIndex={-1} className="inv-titulo">
        {tituloContactos(esAdmin)}
      </h1>
      <p className="inv-intro">
        {esAdmin
          ? 'Todos los contactos que llegan desde el portal. Los de Administración los atiende cualquier administrador.'
          : 'Las personas que te escribieron desde la ficha de tus vehículos publicados. Ábrelos para ver el historial, añadir notas o cerrarlos.'}
      </p>

      <form className="inv-filtros" onSubmit={aplicarFiltros} aria-label="Filtros de los contactos" noValidate>
        <CampoSelect
          id="lead-filtro-estado"
          etiqueta="Estado"
          valor={borrador.estado}
          onCambio={(v) => setBorrador((b) => ({ ...b, estado: v }))}
          opciones={(Object.keys(NOMBRES_ESTADO_LEAD) as EstadoLead[]).map((e) => ({
            valor: e,
            nombre: NOMBRES_ESTADO_LEAD[e],
          }))}
          textoVacio="Todos los estados"
          errores={erroresFiltro.estado}
          requerido={false}
        />
        {esAdmin && (
          <CampoSelect
            id="lead-filtro-responsable"
            etiqueta="Responsable"
            valor={borrador.responsable}
            onCambio={(v) => setBorrador((b) => ({ ...b, responsable: v }))}
            opciones={opcionesResponsable}
            textoVacio="Todos los responsables"
            errores={erroresFiltro.responsable}
            requerido={false}
          />
        )}
        <Campo
          ref={refDesde}
          id="lead-filtro-desde"
          etiqueta="Desde"
          tipo="date"
          valor={borrador.desde}
          onCambio={(v) => setBorrador((b) => ({ ...b, desde: v }))}
          errores={erroresFiltro.desde}
          requerido={false}
        />
        <Campo
          ref={refHasta}
          id="lead-filtro-hasta"
          etiqueta="Hasta"
          tipo="date"
          valor={borrador.hasta}
          onCambio={(v) => setBorrador((b) => ({ ...b, hasta: v }))}
          errores={erroresFiltro.hasta}
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

      {vehiculo && filtros.vehiculoId !== '' && (
        <p className="lead-filtro-vehiculo">
          Solo los contactos de <strong>{vehiculo.titulo || `vehículo ${vehiculo.vehiculoId}`}</strong>.{' '}
          <button type="button" className="secondary-button inv-boton-auto" onClick={quitarVehiculo}>
            Ver todos los vehículos
          </button>
        </p>
      )}

      <h2 ref={tituloLista} tabIndex={-1} className="inv-subtitulo">
        Contactos
        {lista && !cargando && (
          <span className="admin-total">
            {' '}
            ({formatoMiles(lista.total)} {lista.total === 1 ? 'contacto' : 'contactos'})
          </span>
        )}
      </h2>

      <Aviso aviso={errorCarga} />
      {cargando && <p role="status">Cargando contactos...</p>}

      {lista && lista.items.length === 0 && !cargando && !errorCarga && (
        <p className="admin-vacio">
          {hayFiltros
            ? 'No hay contactos con estos filtros.'
            : esAdmin
              ? 'Aún no hay contactos.'
              : 'Aún no tienes contactos. Llegarán cuando alguien escriba desde la ficha de tus vehículos publicados.'}
        </p>
      )}

      {lista && lista.items.length > 0 && (
        <ul className="inv-lista lead-lista" aria-busy={cargando}>
          {lista.items.map((lead) => (
            <TarjetaContacto key={lead.id} lead={lead} esAdmin={esAdmin} />
          ))}
        </ul>
      )}

      {lista && lista.total > 0 && totalPaginas > 1 && (
        <nav className="paginacion" aria-label="Paginación de los contactos">
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
    </div>
  )
}

function TarjetaContacto({ lead, esAdmin }: { lead: LeadItem; esAdmin: boolean }) {
  const abierto = lead.estado === 'ABIERTO'
  return (
    <li className={lead.nuevo ? 'inv-tarjeta lead-tarjeta lead-tarjeta-nueva' : 'inv-tarjeta lead-tarjeta'}>
      <div className="inv-tarjeta-cuerpo">
        <h3 className="inv-tarjeta-titulo">
          {lead.nombre || 'Sin nombre'}
          {lead.nuevo && <span className="lead-nuevo"> Nuevo</span>}
        </h3>
        <p>
          <span className={`estado ${abierto ? 'estado-pendiente' : 'estado-activa'}`}>
            <span className="sr-only">Estado: </span>
            {nombreEstadoLead(lead.estado)}
          </span>
        </p>
        <dl className="inv-datos">
          <div>
            <dt>Vehículo</dt>
            <dd>
              {lead.vehiculo.titulo || '—'}
              {lead.vehiculo.estadoVehiculo && (
                <span className="dato-secundario">{nombreEstadoVehiculo(lead.vehiculo.estadoVehiculo)}</span>
              )}
            </dd>
          </div>
          <div>
            <dt>Recibido</dt>
            <dd>{formatearFechaHora(lead.creadoEn)}</dd>
          </div>
          <div>
            <dt>Teléfono</dt>
            <dd>
              <a href={`tel:${lead.telefono}`}>{lead.telefono}</a>
            </dd>
          </div>
          {lead.correo && (
            <div>
              <dt>Correo</dt>
              <dd>{lead.correo}</dd>
            </div>
          )}
          {esAdmin && (
            <div>
              <dt>Responsable</dt>
              <dd>{nombreResponsable(lead.responsable)}</dd>
            </div>
          )}
          {!abierto && lead.resultadoCierre && (
            <div>
              <dt>Resultado</dt>
              <dd>{nombreResultado(lead.resultadoCierre)}</dd>
            </div>
          )}
        </dl>
        {lead.mensaje && <p className="lead-mensaje inv-texto-largo">{lead.mensaje}</p>}
        <div className="acciones-fila inv-acciones">
          <a className="inv-boton-enlace inv-boton-pequeno" href={hashContacto(lead.id)}>
            Ver detalle <span className="sr-only">del contacto de {lead.nombre}</span>
          </a>
        </div>
      </div>
    </li>
  )
}
