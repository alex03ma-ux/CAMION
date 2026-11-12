// P7 — Historial de ventas (HU-064, minuta §6; D-39, D-62 A; CA-P7-01 a 14).
// Administrador: #/cuenta/ventas, todas las ventas, con filtro por asesor. Asesor: #/ventas ("Mis ventas"), las de
// los vehículos que hoy son suyos, también las revertidas. Un comprador ve "No tienes permiso" sin llamar a la API;
// quien protege es la API (AsesorOAdministrador: 403 / 401). Cada venta enlaza al detalle del vehículo en el
// inventario, donde está su "Historial de ventas". Vender y revertir no cambian (P4b).
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import type { PerfilUsuario } from '../api/auth'
import { ApiError, MENSAJE_INESPERADO, MENSAJE_SIN_PERMISO } from '../api/http'
import { obtenerCatalogosInventario, type ResumenAsesor } from '../api/inventario'
import { formatearFechaHora } from '../api/leads'
import { NOMBRES_ORIGEN_VENTA, ORIGENES_VENTA, type OrigenVenta } from '../api/publicaciones'
import { formatearFechaSuscripcion } from '../api/suscripciones'
import {
  ESTADOS_VENTA,
  FILTROS_VENTAS_VACIOS,
  filtrosVentasDesdeConsulta,
  listarVentas,
  NOMBRES_ESTADO_VENTA,
  nombreEstadoVenta,
  nombreOrigenVenta,
  validarFiltrosVentas,
  type EstadoVenta,
  type FiltrosVentas,
  type ListaVentas,
  type VentaItem,
} from '../api/ventas'
import { formatoCop, formatoMiles } from '../catalogo/formato'
import { nombreEstado } from '../inventario/estados'
import { HASH_CUENTA, hashInventarioVehiculo } from '../rutas'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo, CampoSelect } from './Campo'
import { modoInventario } from './InventarioPagina'

/** Fecha AAAA-MM-DD como fecha local (sin correr el día por la zona horaria). */
export function fechaDiaTexto(valor: string | null): string {
  return valor ? formatearFechaSuscripcion(`${valor.slice(0, 10)}T12:00:00`) : '—'
}

/** Una venta (lista general o historial del vehículo). Las revertidas muestran quién, cuándo y el motivo. */
export function TarjetaVenta({
  venta,
  mostrarAsesor,
  conEnlace,
  nivelTitulo = 3,
}: {
  venta: VentaItem
  mostrarAsesor: boolean
  /** Enlace al detalle del vehículo (no en el propio detalle). */
  conEnlace: boolean
  nivelTitulo?: 3 | 4
}) {
  const revertida = venta.estado === 'REVERTIDA'
  const titulo = venta.vehiculo.titulo || `Vehículo ${venta.vehiculo.id}`
  const Titulo = nivelTitulo === 3 ? 'h3' : 'h4'
  return (
    <li className={revertida ? 'inv-tarjeta venta-tarjeta venta-revertida' : 'inv-tarjeta venta-tarjeta'}>
      <div className="inv-tarjeta-cuerpo">
        <Titulo className="inv-tarjeta-titulo">
          {conEnlace ? titulo : `Venta del ${fechaDiaTexto(venta.fechaVenta)}`}
        </Titulo>
        <p>
          <span className={`estado ${revertida ? 'estado-bloqueada' : 'estado-activa'}`}>
            <span className="sr-only">Estado de la venta: </span>
            {nombreEstadoVenta(venta.estado)}
          </span>
          {conEnlace && venta.vehiculo.estado && (
            <span className="dato-secundario">Vehículo hoy: {nombreEstado(venta.vehiculo.estado)}</span>
          )}
        </p>
        <dl className="inv-datos">
          <div>
            <dt>Fecha de venta</dt>
            <dd>{fechaDiaTexto(venta.fechaVenta)}</dd>
          </div>
          <div>
            <dt>Precio final</dt>
            <dd>{formatoCop(venta.precioFinalCop)}</dd>
          </div>
          <div>
            <dt>Origen</dt>
            <dd>{nombreOrigenVenta(venta.origen)}</dd>
          </div>
          {mostrarAsesor && (
            <div>
              <dt>Asesor</dt>
              <dd>{venta.asesor?.nombre || '—'}</dd>
            </div>
          )}
          <div>
            <dt>Registrada</dt>
            <dd>
              {venta.registradaPor ?? 'Cuenta eliminada'}
              <span className="dato-secundario">{formatearFechaHora(venta.registradaEn)}</span>
            </dd>
          </div>
          {venta.observaciones && (
            <div>
              <dt>Observaciones</dt>
              <dd className="inv-texto-largo">{venta.observaciones}</dd>
            </div>
          )}
          {revertida && (
            <div>
              <dt>Revertida por</dt>
              <dd>
                {venta.revertidaPor ?? 'Cuenta eliminada'}
                <span className="dato-secundario">{formatearFechaHora(venta.revertidaEn)}</span>
              </dd>
            </div>
          )}
          {revertida && venta.motivoReversion && (
            <div>
              <dt>Motivo de la reversión</dt>
              <dd className="inv-texto-largo">{venta.motivoReversion}</dd>
            </div>
          )}
        </dl>
        {conEnlace && venta.vehiculo.id > 0 && (
          <div className="acciones-fila inv-acciones">
            <a className="inv-boton-enlace inv-boton-pequeno" href={hashInventarioVehiculo(venta.vehiculo.id)}>
              Ver vehículo <span className="sr-only">{titulo}</span>
            </a>
          </div>
        )}
      </div>
    </li>
  )
}

type VentasProps = {
  token: string
  perfil: PerfilUsuario
  /** Filtros iniciales desde la consulta del hash (enlaces del panel). */
  consulta?: string
  /** "seccion" dentro de "Mi cuenta" (administrador, título h2); "pagina" en #/ventas (título h1). */
  nivel: 'pagina' | 'seccion'
  onSesionTerminada: () => void
}

export function tituloVentas(esAdmin: boolean): string {
  return esAdmin ? 'Ventas' : 'Mis ventas'
}

export function Ventas({ token, perfil, consulta, nivel, onSesionTerminada }: VentasProps) {
  const modo = modoInventario(perfil.roles)
  if (modo === null) {
    const Titulo = nivel === 'pagina' ? 'h1' : 'h2'
    return (
      <div className={nivel === 'pagina' ? 'cat-pagina inv-pagina' : undefined}>
        <Titulo tabIndex={-1} className={nivel === 'pagina' ? 'inv-titulo' : undefined}>
          Ventas
        </Titulo>
        <Aviso aviso={{ tipo: 'error', texto: MENSAJE_SIN_PERMISO }} />
        <p>
          <a href={HASH_CUENTA}>Volver a mi cuenta</a>
        </p>
      </div>
    )
  }
  return (
    <ListaVentasPanel
      token={token}
      esAdmin={modo === 'administrador'}
      consulta={consulta}
      nivel={nivel}
      onSesionTerminada={onSesionTerminada}
    />
  )
}

type Borrador = { desde: string; hasta: string; origen: string; estado: string; asesorId: string }
const CAMPOS_FILTRO = ['desde', 'hasta', 'origen', 'estado', 'asesorId'] as const
type CampoFiltro = (typeof CAMPOS_FILTRO)[number]
type ErroresFiltro = Partial<Record<CampoFiltro, string[]>>

function borradorDe(f: FiltrosVentas): Borrador {
  return { desde: f.desde, hasta: f.hasta, origen: f.origen, estado: f.estado, asesorId: f.asesorId }
}

function ListaVentasPanel({
  token,
  esAdmin,
  consulta,
  nivel,
  onSesionTerminada,
}: {
  token: string
  esAdmin: boolean
  consulta: string | undefined
  nivel: 'pagina' | 'seccion'
  onSesionTerminada: () => void
}) {
  const [filtros, setFiltros] = useState<FiltrosVentas>(() => {
    const f = filtrosVentasDesdeConsulta(consulta ?? '')
    return esAdmin ? f : { ...f, asesorId: '' }
  })
  const [borrador, setBorrador] = useState<Borrador>(() => borradorDe(filtros))
  const [asesores, setAsesores] = useState<ResumenAsesor[]>([])
  const [lista, setLista] = useState<ListaVentas | null>(null)
  const [cargando, setCargando] = useState(true)
  const [errorCarga, setErrorCarga] = useState<DatosAviso | null>(null)
  const [erroresFiltro, setErroresFiltro] = useState<ErroresFiltro>({})
  const titulo = useRef<HTMLHeadingElement>(null)
  const tituloLista = useRef<HTMLHeadingElement>(null)
  const refDesde = useRef<HTMLInputElement>(null)
  const refHasta = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (nivel === 'seccion') titulo.current?.focus()
  }, [nivel])

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

  // Asesores para el filtro (solo administrador). Si falla, el filtro queda sin opciones.
  useEffect(() => {
    if (!esAdmin) return
    const control = new AbortController()
    obtenerCatalogosInventario(token, control.signal)
      .then((c) => setAsesores(c.asesores))
      .catch((error: unknown) => {
        if (!control.signal.aborted) es401(error)
      })
    return () => control.abort()
  }, [token, esAdmin, es401])

  useEffect(() => {
    const control = new AbortController()
    setCargando(true)
    setErrorCarga(null)
    listarVentas(token, filtros, esAdmin, control.signal)
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
        if (es401(error)) return
        setLista(null)
        if (error instanceof ApiError && error.status === 400) {
          const porCampo: ErroresFiltro = {}
          const otros: string[] = []
          for (const [campo, mensajes] of Object.entries(error.errores)) {
            const clave = CAMPOS_FILTRO.find((c) => c.toLowerCase() === campo.toLowerCase())
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
  }, [token, esAdmin, filtros, es401])

  function aplicarFiltros(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    const errores = validarFiltrosVentas(borrador)
    setErroresFiltro(errores)
    if (errores.desde) {
      refDesde.current?.focus()
      return
    }
    if (errores.hasta) {
      refHasta.current?.focus()
      return
    }
    setFiltros({
      ...FILTROS_VENTAS_VACIOS,
      desde: borrador.desde,
      hasta: borrador.hasta,
      origen: borrador.origen as OrigenVenta | '',
      estado: borrador.estado as EstadoVenta | '',
      asesorId: esAdmin ? borrador.asesorId : '',
    })
  }

  function limpiarFiltros() {
    setErroresFiltro({})
    setBorrador(borradorDe(FILTROS_VENTAS_VACIOS))
    setFiltros(FILTROS_VENTAS_VACIOS)
    tituloLista.current?.focus()
  }

  function irAPagina(pagina: number) {
    setFiltros((f) => ({ ...f, pagina }))
    tituloLista.current?.focus()
  }

  const totalPaginas = lista ? Math.max(1, Math.ceil(lista.total / Math.max(1, lista.tamano))) : 1
  const paginaActual = lista?.pagina ?? filtros.pagina
  const hayFiltros =
    filtros.desde !== '' || filtros.hasta !== '' || filtros.origen !== '' || filtros.estado !== '' || filtros.asesorId !== ''
  const esPagina = nivel === 'pagina'
  const TituloPrincipal = esPagina ? 'h1' : 'h2'
  const TituloLista = esPagina ? 'h2' : 'h3'

  return (
    <div className={esPagina ? 'cat-pagina inv-pagina ventas' : 'panel-ancho ventas'}>
      <TituloPrincipal ref={titulo} tabIndex={-1} className={esPagina ? 'inv-titulo' : undefined}>
        {tituloVentas(esAdmin)}
      </TituloPrincipal>
      <p className="inv-intro">
        {esAdmin
          ? 'Todas las ventas registradas, vigentes y revertidas. Abre una venta para ver el vehículo y su historial de ventas.'
          : 'Las ventas de tus vehículos, también las revertidas. Para revertir una venta, hazlo desde tu inventario o tus publicaciones.'}
      </p>

      <form className="inv-filtros" onSubmit={aplicarFiltros} aria-label="Filtros de las ventas" noValidate>
        <Campo
          ref={refDesde}
          id="venta-filtro-desde"
          etiqueta="Vendida desde"
          tipo="date"
          valor={borrador.desde}
          onCambio={(v) => setBorrador((b) => ({ ...b, desde: v }))}
          errores={erroresFiltro.desde}
          requerido={false}
        />
        <Campo
          ref={refHasta}
          id="venta-filtro-hasta"
          etiqueta="Vendida hasta"
          tipo="date"
          valor={borrador.hasta}
          onCambio={(v) => setBorrador((b) => ({ ...b, hasta: v }))}
          errores={erroresFiltro.hasta}
          requerido={false}
        />
        <CampoSelect
          id="venta-filtro-origen"
          etiqueta="Origen"
          valor={borrador.origen}
          onCambio={(v) => setBorrador((b) => ({ ...b, origen: v }))}
          opciones={ORIGENES_VENTA.map((o) => ({ valor: o, nombre: NOMBRES_ORIGEN_VENTA[o] }))}
          textoVacio="Todos los orígenes"
          errores={erroresFiltro.origen}
          requerido={false}
        />
        <CampoSelect
          id="venta-filtro-estado"
          etiqueta="Estado"
          valor={borrador.estado}
          onCambio={(v) => setBorrador((b) => ({ ...b, estado: v }))}
          opciones={ESTADOS_VENTA.map((e) => ({ valor: e, nombre: NOMBRES_ESTADO_VENTA[e] }))}
          textoVacio="Vigentes y revertidas"
          errores={erroresFiltro.estado}
          requerido={false}
        />
        {esAdmin && (
          <CampoSelect
            id="venta-filtro-asesor"
            etiqueta="Asesor"
            valor={borrador.asesorId}
            onCambio={(v) => setBorrador((b) => ({ ...b, asesorId: v }))}
            opciones={asesores.map((a) => ({ valor: a.id, nombre: a.nombre || `Asesor ${a.id}` }))}
            textoVacio="Todos los asesores"
            errores={erroresFiltro.asesorId}
            requerido={false}
          />
        )}
        <div className="inv-filtros-botones">
          <button type="submit" disabled={cargando}>
            Aplicar filtros
          </button>
          <button type="button" className="secondary-button" onClick={limpiarFiltros}>
            Limpiar
          </button>
        </div>
      </form>

      <TituloLista ref={tituloLista} tabIndex={-1} className="inv-subtitulo">
        Ventas
        {lista && !cargando && (
          <span className="admin-total">
            {' '}
            ({formatoMiles(lista.total)} {lista.total === 1 ? 'venta' : 'ventas'})
          </span>
        )}
      </TituloLista>

      <Aviso aviso={errorCarga} />
      {cargando && <p role="status">Cargando ventas...</p>}

      {lista && lista.items.length === 0 && !cargando && !errorCarga && (
        <div className="admin-vacio">
          {hayFiltros ? (
            <>
              <p>No hay ventas con estos filtros.</p>
              <button type="button" className="secondary-button inv-boton-auto" onClick={limpiarFiltros}>
                Limpiar filtros
              </button>
            </>
          ) : (
            <p>{esAdmin ? 'Aún no hay ventas registradas.' : 'Aún no tienes ventas registradas.'}</p>
          )}
        </div>
      )}

      {lista && lista.items.length > 0 && (
        <ul className="inv-lista venta-lista" aria-busy={cargando}>
          {lista.items.map((v) => (
            <TarjetaVenta key={v.id} venta={v} mostrarAsesor={esAdmin} conEnlace nivelTitulo={esPagina ? 3 : 4} />
          ))}
        </ul>
      )}

      {lista && lista.total > 0 && totalPaginas > 1 && (
        <nav className="paginacion" aria-label="Paginación de las ventas">
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
