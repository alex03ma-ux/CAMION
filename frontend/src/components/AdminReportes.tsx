// P8 — Reportes del administrador en #/cuenta/reportes (HU-076 a HU-080, RN-RE-002 a 006; D-68 A, D-71; CA-P8-01 a 33,
// 42 a 50, 73). Cinco reportes en pestañas (#/cuenta/reportes?tipo=...), cada uno con sus filtros (por defecto el mes en
// curso), sus cifras, la tabla de filas paginada y "Descargar CSV" con los mismos filtros. El CSV se pide con fetch y el
// token en el encabezado, y se guarda desde un blob: el token nunca va en la URL. Sin datos personales de clientes.
import { useCallback, useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { obtenerCatalogosInventario, type CatalogosInventario } from '../api/inventario'
import {
  NOMBRES_ESTADO_GESTION,
  NOMBRES_ESTADO_LEAD,
  NOMBRES_ORIGEN,
  nombreOrigen,
  nombreResultado,
  ORIGENES_LEAD,
  RESPONSABLE_ADMINISTRACION,
  RESULTADOS_CIERRE,
  resultadosCierrePara,
  type OrigenLead,
} from '../api/leads'
import { MEDIOS_PAGO, NOMBRES_MEDIO_PAGO, nombreConceptoPago, nombreMedioPago } from '../api/pagos'
import { listarPlanes, type Plan } from '../api/planes'
import { NOMBRES_ORIGEN_VENTA, ORIGENES_VENTA } from '../api/publicaciones'
import {
  CAMPOS_FILTRO_REPORTE,
  CANALES_CREDITO,
  descargarReporteCsv,
  diaColombia,
  ESTADOS_CREDITO_REPORTE,
  ESTADOS_INVENTARIO,
  ESTADOS_PUBLICACION_REPORTE,
  ESTADOS_SUSCRIPCION_REPORTE,
  mesEnCurso,
  MENSAJE_SIN_DATOS_REPORTE,
  NOMBRES_CANAL_CREDITO,
  NOMBRES_REPORTE,
  nombreCanalCredito,
  nombreCsvPorDefecto,
  obtenerReporte,
  TAMANO_PAGINA_REPORTE,
  textoDias,
  textoPeriodo,
  textoPorcentaje,
  TIPOS_REPORTE,
  validarPeriodo,
  type FiltrosReporte,
  type PaginaFilas,
  type Periodo,
  type ReportePorTipo,
  type TipoReporte,
} from '../api/reportes'
import { nombreEstadoSuscripcion } from '../api/suscripciones'
import { nombreEstadoVenta, nombreOrigenVenta } from '../api/ventas'
import { formatoCop, formatoMiles } from '../catalogo/formato'
import { nombreEstado } from '../inventario/estados'
import { conConsulta, hashInventarioVehiculo, hashSeccionAdmin } from '../rutas'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo, CampoSelect } from './Campo'
import { Cifras, PaginacionBotones, plural, TablaDatos } from './PiezasReporte'

type Props = {
  token: string
  /** Consulta del hash: `tipo=ventas`. */
  consulta?: string
  onSesionTerminada: () => void
}

/** Tipo de reporte de la consulta del hash; por defecto, vehículos. */
export function tipoReporteDesdeConsulta(consulta: string | undefined): TipoReporte {
  const tipo = new URLSearchParams(consulta ?? '').get('tipo') ?? ''
  return TIPOS_REPORTE.includes(tipo as TipoReporte) ? (tipo as TipoReporte) : 'vehiculos'
}

export function hashReporte(tipo: TipoReporte): string {
  return conConsulta(hashSeccionAdmin('reportes'), tipo === 'vehiculos' ? {} : { tipo })
}

export function AdminReportes({ token, consulta, onSesionTerminada }: Props) {
  const tipo = tipoReporteDesdeConsulta(consulta)
  const titulo = useRef<HTMLHeadingElement>(null)
  useEffect(() => {
    titulo.current?.focus()
  }, [])

  return (
    <section className="panel-ancho reportes" aria-labelledby="reportes-titulo">
      <h2 id="reportes-titulo" ref={titulo} tabIndex={-1}>
        Reportes
      </h2>
      <p className="form-ayuda">
        Elige un reporte y sus filtros. Por defecto se muestra el mes en curso. Los reportes no incluyen nombres,
        teléfonos, correos ni mensajes de clientes.
      </p>
      <nav className="reportes-pestanas" aria-label="Tipos de reporte">
        <ul>
          {TIPOS_REPORTE.map((t) => (
            <li key={t}>
              <a href={hashReporte(t)} aria-current={t === tipo ? 'page' : undefined}>
                {NOMBRES_REPORTE[t]}
              </a>
            </li>
          ))}
        </ul>
      </nav>
      <VistaReporte key={tipo} tipo={tipo} token={token} onSesionTerminada={onSesionTerminada} />
    </section>
  )
}

// ---------------------------------------------------------------- Vista de un reporte

type Resultado = { [T in TipoReporte]: { tipo: T; datos: ReportePorTipo[T] } }[TipoReporte]
type Carga = { estado: 'cargando' } | { estado: 'ok'; resultado: Resultado } | { estado: 'error'; aviso: DatosAviso }
type ErroresFiltros = Record<string, string[]>

function filtrosIniciales(tipo: TipoReporte): FiltrosReporte {
  const mes = mesEnCurso()
  const f: FiltrosReporte = { desde: mes.desde, hasta: mes.hasta }
  for (const campo of CAMPOS_FILTRO_REPORTE[tipo]) f[campo] = ''
  return f
}

/** Reparte `errores` del 400 por filtro (sin distinguir mayúsculas); el resto va al aviso. */
function repartirErrores(tipo: TipoReporte, error: ApiError): { porCampo: ErroresFiltros; otros: string[] } {
  const campos = ['desde', 'hasta', ...CAMPOS_FILTRO_REPORTE[tipo]]
  const porCampo: ErroresFiltros = {}
  const otros: string[] = []
  for (const [clave, mensajes] of Object.entries(error.errores)) {
    const campo = campos.find((c) => c.toLowerCase() === clave.toLowerCase())
    if (campo) porCampo[campo] = [...(porCampo[campo] ?? []), ...mensajes]
    else for (const m of mensajes) if (!otros.includes(m)) otros.push(m)
  }
  return { porCampo, otros }
}

type Opciones = { catalogos: CatalogosInventario | null; planes: Plan[] }

function VistaReporte({
  tipo,
  token,
  onSesionTerminada,
}: {
  tipo: TipoReporte
  token: string
  onSesionTerminada: () => void
}) {
  const [aplicados, setAplicados] = useState<FiltrosReporte>(() => filtrosIniciales(tipo))
  const [borrador, setBorrador] = useState<FiltrosReporte>(() => filtrosIniciales(tipo))
  const [pagina, setPagina] = useState(1)
  const [carga, setCarga] = useState<Carga>({ estado: 'cargando' })
  const [cargando, setCargando] = useState(true)
  const [intento, setIntento] = useState(0)
  const [errores, setErrores] = useState<ErroresFiltros>({})
  const [opciones, setOpciones] = useState<Opciones>({ catalogos: null, planes: [] })
  const [descarga, setDescarga] = useState<{ enCurso: boolean; aviso: DatosAviso | null }>({ enCurso: false, aviso: null })
  const tituloResultado = useRef<HTMLHeadingElement>(null)
  const tituloFilas = useRef<HTMLHeadingElement>(null)
  const idBase = `reporte-${tipo}`

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

  // Listas de los filtros (asesores, categorías, ubicación y planes). Si fallan, los filtros quedan sin opciones.
  const usaCatalogos = CAMPOS_FILTRO_REPORTE[tipo].some((c) =>
    ['asesorId', 'categoriaId', 'departamentoId', 'responsable'].includes(c),
  )
  const usaPlanes = CAMPOS_FILTRO_REPORTE[tipo].includes('planId')
  useEffect(() => {
    const control = new AbortController()
    if (usaCatalogos) {
      obtenerCatalogosInventario(token, control.signal)
        .then((catalogos) => setOpciones((o) => ({ ...o, catalogos })))
        .catch((error: unknown) => {
          if (!control.signal.aborted) es401(error)
        })
    }
    if (usaPlanes) {
      listarPlanes(token, undefined, control.signal)
        .then((planes) => setOpciones((o) => ({ ...o, planes })))
        .catch((error: unknown) => {
          if (!control.signal.aborted) es401(error)
        })
    }
    return () => control.abort()
  }, [token, usaCatalogos, usaPlanes, es401])

  useEffect(() => {
    const control = new AbortController()
    setCarga((c) => (c.estado === 'ok' ? c : { estado: 'cargando' }))
    setCargando(true)
    obtenerReporte(token, tipo, aplicados, pagina, control.signal)
      .then((datos) => {
        setCarga({ estado: 'ok', resultado: { tipo, datos } as Resultado })
        setCargando(false)
      })
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        setCargando(false)
        if (es401(error)) return
        if (error instanceof ApiError && error.status === 400) {
          const { porCampo, otros } = repartirErrores(tipo, error)
          setErrores(porCampo)
          setCarga({ estado: 'error', aviso: { tipo: 'error', texto: [error.mensaje, ...otros].join(' ') } })
          return
        }
        setCarga({ estado: 'error', aviso: { tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO } })
      })
    return () => control.abort()
  }, [token, tipo, aplicados, pagina, intento, es401])

  function cambiar(campo: string, valor: string) {
    setBorrador((b) => {
      const nuevo = { ...b, [campo]: valor }
      // El municipio depende del departamento; el resultado, del origen.
      if (campo === 'departamentoId') nuevo.municipioId = ''
      if (campo === 'origen' && tipo === 'solicitudes' && valor !== '' && nuevo.resultado) {
        const validos = resultadosCierrePara(valor as OrigenLead).map((r) => r.valor as string)
        if (!validos.includes(nuevo.resultado)) nuevo.resultado = ''
      }
      return nuevo
    })
  }

  function aplicar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    const validacion = validarPeriodo({ desde: borrador.desde, hasta: borrador.hasta })
    setErrores(validacion)
    if (validacion.desde || validacion.hasta) {
      document.getElementById(`${idBase}-${validacion.desde ? 'desde' : 'hasta'}`)?.focus()
      return
    }
    setDescarga({ enCurso: false, aviso: null })
    setPagina(1)
    setAplicados({ ...borrador })
    setIntento((i) => i + 1)
  }

  function limpiar() {
    const iniciales = filtrosIniciales(tipo)
    setErrores({})
    setDescarga({ enCurso: false, aviso: null })
    setBorrador(iniciales)
    setAplicados(iniciales)
    setPagina(1)
    tituloResultado.current?.focus()
  }

  function irAPagina(n: number) {
    setPagina(n)
    tituloFilas.current?.focus()
  }

  async function descargar() {
    if (descarga.enCurso) return
    setDescarga({ enCurso: true, aviso: null })
    try {
      const archivo = await descargarReporteCsv(token, tipo, aplicados)
      const periodo: Periodo =
        carga.estado === 'ok' ? carga.resultado.datos.periodo : { desde: aplicados.desde, hasta: aplicados.hasta }
      const nombre = archivo.nombre ?? nombreCsvPorDefecto(tipo, periodo)
      guardarArchivo(archivo.contenido, nombre)
      setDescarga({ enCurso: false, aviso: { tipo: 'exito', texto: `Se descargó el archivo ${nombre}.` } })
    } catch (error) {
      if (es401(error)) {
        setDescarga({ enCurso: false, aviso: null })
        return
      }
      setDescarga({
        enCurso: false,
        aviso: { tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO },
      })
    }
  }

  const resultado = carga.estado === 'ok' ? carga.resultado : null
  const sinFilas = resultado !== null && resultado.datos.filas.total === 0

  return (
    <div className="reporte-vista">
      <form className="filtros" onSubmit={aplicar} aria-label={`Filtros del reporte de ${NOMBRES_REPORTE[tipo].toLowerCase()}`} noValidate>
        <Campo
          id={`${idBase}-desde`}
          etiqueta="Desde"
          tipo="date"
          valor={borrador.desde}
          onCambio={(v) => cambiar('desde', v)}
          errores={errores.desde}
          requerido={false}
        />
        <Campo
          id={`${idBase}-hasta`}
          etiqueta="Hasta"
          tipo="date"
          valor={borrador.hasta}
          onCambio={(v) => cambiar('hasta', v)}
          errores={errores.hasta}
          requerido={false}
        />
        <FiltrosPropios tipo={tipo} idBase={idBase} borrador={borrador} errores={errores} opciones={opciones} onCambio={cambiar} />
        <div className="inv-filtros-botones">
          <button type="submit" disabled={cargando}>
            Aplicar filtros
          </button>
          <button type="button" className="secondary-button" onClick={limpiar}>
            Limpiar filtros
          </button>
        </div>
      </form>

      <h3 ref={tituloResultado} tabIndex={-1} className="admin-subtitulo">
        {NOMBRES_REPORTE[tipo]}
        {resultado && <span className="admin-total"> · {textoPeriodo(resultado.datos.periodo)}</span>}
      </h3>

      <div className="acciones-fila reporte-acciones">
        <button
          type="button"
          className="secondary-button"
          onClick={() => void descargar()}
          disabled={descarga.enCurso || carga.estado !== 'ok'}
          aria-busy={descarga.enCurso || undefined}
        >
          {descarga.enCurso ? 'Descargando...' : 'Descargar CSV'}
        </button>
      </div>
      <Aviso aviso={descarga.aviso} />

      {carga.estado === 'cargando' && <p role="status">Cargando el reporte...</p>}
      {carga.estado === 'error' && (
        <div className="inv-estado-carga">
          <Aviso aviso={carga.aviso} />
          <button type="button" className="secondary-button inv-boton-auto" onClick={() => setIntento((i) => i + 1)}>
            Reintentar
          </button>
        </div>
      )}

      {resultado && (
        <div aria-busy={cargando || undefined}>
          {cargando && <p role="status">Actualizando el reporte...</p>}
          {sinFilas && (
            <div className="admin-vacio reporte-vacio">
              <p>{MENSAJE_SIN_DATOS_REPORTE}</p>
              <button type="button" className="secondary-button inv-boton-auto" onClick={limpiar}>
                Limpiar filtros
              </button>
            </div>
          )}
          <ContenidoReporte resultado={resultado} idBase={idBase} />
          <h4 ref={tituloFilas} tabIndex={-1} className="admin-subtitulo admin-subtitulo-menor reporte-filas-titulo">
            Detalle
            <span className="admin-total"> ({plural(resultado.datos.filas.total, 'fila', 'filas')})</span>
          </h4>
          {!sinFilas && <FilasReporte resultado={resultado} idBase={idBase} ocupada={cargando} />}
          <PaginacionBotones
            etiqueta={`Paginación del reporte de ${NOMBRES_REPORTE[tipo].toLowerCase()}`}
            pagina={resultado.datos.filas.pagina}
            total={resultado.datos.filas.total}
            tamano={resultado.datos.filas.tamano || TAMANO_PAGINA_REPORTE}
            deshabilitado={cargando}
            onIr={irAPagina}
          />
        </div>
      )}
    </div>
  )
}

/** Guarda el blob con un enlace temporal `download` (la URL de objeto se libera enseguida). */
export function guardarArchivo(contenido: Blob, nombre: string): void {
  const url = URL.createObjectURL(contenido)
  const enlace = document.createElement('a')
  enlace.href = url
  enlace.download = nombre
  enlace.rel = 'noopener'
  enlace.style.display = 'none'
  document.body.appendChild(enlace)
  try {
    enlace.click()
  } finally {
    enlace.remove()
    window.setTimeout(() => URL.revokeObjectURL(url), 0)
  }
}

// ---------------------------------------------------------------- Filtros propios

const NOMBRES_ESTADO_PUBLICACION_REPORTE: Readonly<Record<string, string>> = {
  BORRADOR: 'Borrador',
  PUBLICADA: 'Publicada',
  OCULTA: 'Inactiva',
}

function FiltrosPropios({
  tipo,
  idBase,
  borrador,
  errores,
  opciones,
  onCambio,
}: {
  tipo: TipoReporte
  idBase: string
  borrador: FiltrosReporte
  errores: ErroresFiltros
  opciones: Opciones
  onCambio: (campo: string, valor: string) => void
}) {
  const asesores = (opciones.catalogos?.asesores ?? []).map((a) => ({ valor: a.id, nombre: a.nombre || `Asesor ${a.id}` }))
  const categorias = (opciones.catalogos?.tipos ?? []).map((t) => ({ valor: String(t.id), nombre: t.nombre }))
  const departamentos = opciones.catalogos?.departamentos ?? []
  const departamento = departamentos.find((d) => String(d.id) === borrador.departamentoId)
  const select = (campo: string, etiqueta: string, lista: readonly { valor: string; nombre: string }[], vacio: string, deshabilitado = false) => (
    <CampoSelect
      key={campo}
      id={`${idBase}-${campo}`}
      etiqueta={etiqueta}
      valor={borrador[campo] ?? ''}
      onCambio={(v) => onCambio(campo, v)}
      opciones={lista}
      textoVacio={vacio}
      errores={errores[campo]}
      requerido={false}
      deshabilitado={deshabilitado}
    />
  )
  const asesor = select('asesorId', 'Asesor', asesores, 'Todos los asesores')
  const categoria = select('categoriaId', 'Categoría', categorias, 'Todas las categorías')

  if (tipo === 'vehiculos') {
    return (
      <>
        {asesor}
        {categoria}
        {select(
          'departamentoId',
          'Departamento',
          departamentos.map((d) => ({ valor: String(d.id), nombre: d.nombre })),
          'Todos los departamentos',
        )}
        {select(
          'municipioId',
          'Municipio',
          (departamento?.municipios ?? []).map((m) => ({ valor: String(m.id), nombre: m.nombre })),
          departamento ? 'Todos los municipios' : 'Elige primero el departamento',
          !departamento,
        )}
      </>
    )
  }
  if (tipo === 'solicitudes') {
    const origen = borrador.origen as OrigenLead | ''
    const resultados = origen ? resultadosCierrePara(origen) : RESULTADOS_CIERRE
    return (
      <>
        {select('origen', 'Origen', ORIGENES_LEAD.map((o) => ({ valor: o, nombre: NOMBRES_ORIGEN[o] })), 'Todos los orígenes')}
        {select(
          'estado',
          'Estado',
          (['ABIERTO', 'CERRADO'] as const).map((e) => ({ valor: e, nombre: NOMBRES_ESTADO_LEAD[e] })),
          'Todos los estados',
        )}
        {select('resultado', 'Resultado del cierre', resultados.map((r) => ({ valor: r.valor, nombre: r.nombre })), 'Todos los resultados')}
        {select(
          'responsable',
          'Responsable',
          [{ valor: RESPONSABLE_ADMINISTRACION, nombre: 'Administración' }, ...asesores],
          'Todos los responsables',
        )}
        {categoria}
      </>
    )
  }
  if (tipo === 'creditos') {
    return (
      <>
        {select(
          'estado',
          'Estado',
          ESTADOS_CREDITO_REPORTE.map((e) => ({ valor: e, nombre: NOMBRES_ESTADO_GESTION[e] })),
          'Todos los estados',
        )}
        {select(
          'resultado',
          'Resultado del cierre',
          resultadosCierrePara('CREDITO').map((r) => ({ valor: r.valor, nombre: r.nombre })),
          'Todos los resultados',
        )}
        {select('canal', 'Canal', CANALES_CREDITO.map((c) => ({ valor: c, nombre: NOMBRES_CANAL_CREDITO[c] })), 'Todos los canales')}
      </>
    )
  }
  if (tipo === 'ventas') {
    return (
      <>
        {asesor}
        {categoria}
        {select('origen', 'Origen de la venta', ORIGENES_VENTA.map((o) => ({ valor: o, nombre: NOMBRES_ORIGEN_VENTA[o] })), 'Todos los orígenes')}
      </>
    )
  }
  return (
    <>
      {asesor}
      {select('planId', 'Plan', opciones.planes.map((p) => ({ valor: String(p.id), nombre: p.nombre })), 'Todos los planes')}
      {select('medio', 'Medio de pago', MEDIOS_PAGO.map((m) => ({ valor: m, nombre: NOMBRES_MEDIO_PAGO[m] })), 'Todos los medios')}
    </>
  )
}

// ---------------------------------------------------------------- Cifras de cada reporte

function ContenidoReporte({ resultado, idBase }: { resultado: Resultado; idBase: string }) {
  switch (resultado.tipo) {
    case 'vehiculos':
      return <ContenidoVehiculos d={resultado.datos} idBase={idBase} />
    case 'solicitudes':
      return <ContenidoSolicitudes d={resultado.datos} idBase={idBase} />
    case 'creditos':
      return <ContenidoCreditos d={resultado.datos} idBase={idBase} />
    case 'ventas':
      return <ContenidoVentas d={resultado.datos} idBase={idBase} />
    case 'suscripciones':
      return <ContenidoSuscripciones d={resultado.datos} idBase={idBase} />
  }
}

function enlaceVehiculo(v: { id: number; titulo: string } | null): ReactNode {
  if (!v) return '—'
  const titulo = v.titulo || `Vehículo ${v.id}`
  return v.id > 0 ? <a href={hashInventarioVehiculo(v.id)}>{titulo}</a> : titulo
}

function ContenidoVehiculos({ d, idBase }: { d: ReportePorTipo['vehiculos']; idBase: string }) {
  return (
    <>
      <Cifras
        titulo="Resumen de vehículos y publicaciones"
        cifras={[
          { clave: 'registrados', etiqueta: 'Registrados en el período', valor: formatoMiles(d.registradosEnPeriodo) },
          { clave: 'visibles', etiqueta: 'Publicaciones visibles hoy', valor: formatoMiles(d.publicacionesVisiblesHoy) },
          { clave: 'destacadas', etiqueta: 'Destacadas hoy', valor: formatoMiles(d.publicacionesDestacadasHoy) },
          { clave: 'eliminadas', etiqueta: 'Publicaciones eliminadas', valor: formatoMiles(d.publicacionesEliminadas) },
        ]}
      />
      <div className="reporte-tablas">
        <TablaDatos
          id={`${idBase}-estado`}
          titulo="Inventario actual por estado"
          columnas={['Estado', 'Vehículos']}
          filas={ESTADOS_INVENTARIO.map((e) => [nombreEstado(e), formatoMiles(d.inventarioPorEstado[e])])}
        />
        <TablaDatos
          id={`${idBase}-categoria`}
          titulo="Inventario actual por categoría"
          columnas={['Categoría', 'Vehículos']}
          filas={d.inventarioPorCategoria.map((c) => [c.nombre || `Categoría ${c.id}`, formatoMiles(c.total)])}
        />
        <TablaDatos
          id={`${idBase}-publicaciones`}
          titulo="Publicaciones por estado"
          columnas={['Estado', 'Publicaciones']}
          filas={ESTADOS_PUBLICACION_REPORTE.map((e) => [NOMBRES_ESTADO_PUBLICACION_REPORTE[e], formatoMiles(d.publicacionesPorEstado[e])])}
        />
      </div>
      <TablaDatos
        id={`${idBase}-contactados`}
        titulo="Los 10 vehículos con más contactos en el período"
        columnas={['Vehículo', 'Contactos']}
        filas={d.masContactados.map((m) => [enlaceVehiculo({ id: m.vehiculoId, titulo: m.titulo }), formatoMiles(m.contactos)])}
        textoVacio="Ningún vehículo recibió contactos en el período."
      />
    </>
  )
}

function ContenidoSolicitudes({ d, idBase }: { d: ReportePorTipo['solicitudes']; idBase: string }) {
  const c = d.contactosPorResponsable
  return (
    <>
      <Cifras
        titulo="Resumen de solicitudes"
        cifras={[
          { clave: 'recibidas', etiqueta: 'Recibidas en el período', valor: formatoMiles(d.recibidas) },
          { clave: 'cerradas', etiqueta: 'Cerradas en el período', valor: formatoMiles(d.cerradasEnPeriodo) },
        ]}
      />
      <TablaDatos
        id={`${idBase}-origen`}
        titulo="Solicitudes recibidas por origen"
        columnas={['Origen', 'Recibidas', 'Siguen abiertas', 'Cerradas']}
        filas={d.porOrigen.map((o) => [nombreOrigen(o.origen), formatoMiles(o.recibidas), formatoMiles(o.abiertas), formatoMiles(o.cerradas)])}
      />
      <TablaDatos
        id={`${idBase}-resultado`}
        titulo="Cerradas en el período por resultado"
        columnas={['Origen', 'Resultado', 'Solicitudes']}
        filas={d.cerradasPorResultado.map((r) => [nombreOrigen(r.origen), nombreResultado(r.resultado), formatoMiles(r.total)])}
      />
      <TablaDatos
        id={`${idBase}-tiempo`}
        titulo="Tiempo medio hasta el primer cierre"
        columnas={['Origen', 'Cerradas', 'Tiempo medio']}
        filas={d.tiempoMedioCierre.map((t) => [nombreOrigen(t.origen), formatoMiles(t.cerradas), textoDias(t.dias)])}
      />
      <h4 className="admin-subtitulo admin-subtitulo-menor">Contactos de vehículo por responsable</h4>
      <Cifras
        titulo="Contactos de vehículo por responsable"
        cifras={[
          { clave: 'admin', etiqueta: 'A cargo de Administración', valor: formatoMiles(c.administracion) },
          { clave: 'mano', etiqueta: 'Reasignados a mano', valor: formatoMiles(c.reasignadosAMano) },
          { clave: 'solos', etiqueta: 'Pasaron solos a Administración', valor: formatoMiles(c.pasadosAAdministracion) },
        ]}
      />
      <TablaDatos
        id={`${idBase}-asesores`}
        titulo="Contactos de vehículo a cargo de cada asesor"
        columnas={['Asesor', 'Contactos']}
        filas={c.asesores.map((a) => [a.nombre || `Asesor ${a.id}`, formatoMiles(a.total)])}
        textoVacio="Ningún asesor tiene contactos del período."
      />
    </>
  )
}

function ContenidoCreditos({ d, idBase }: { d: ReportePorTipo['creditos']; idBase: string }) {
  return (
    <>
      <Cifras
        titulo="Resumen de créditos"
        cifras={[
          { clave: 'recibidos', etiqueta: 'Recibidos en el período', valor: formatoMiles(d.recibidos) },
          { clave: 'cerrados', etiqueta: 'Cerrados en el período', valor: formatoMiles(d.cerrados) },
          { clave: 'remitido', etiqueta: 'Remitidos a entidad financiera (de los cerrados)', valor: textoPorcentaje(d.porcentajeRemitido) },
        ]}
      />
      <div className="reporte-tablas">
        <TablaDatos
          id={`${idBase}-estado`}
          titulo="Recibidos por estado"
          columnas={['Estado', 'Créditos']}
          filas={ESTADOS_CREDITO_REPORTE.map((e) => [NOMBRES_ESTADO_GESTION[e], formatoMiles(d.porEstado[e])])}
        />
        <TablaDatos
          id={`${idBase}-canal`}
          titulo="Recibidos por canal"
          columnas={['Canal', 'Créditos']}
          filas={CANALES_CREDITO.map((c) => [NOMBRES_CANAL_CREDITO[c], formatoMiles(d.porCanal[c])])}
        />
        <TablaDatos
          id={`${idBase}-resultado`}
          titulo="Cerrados por resultado"
          columnas={['Resultado', 'Créditos']}
          filas={d.cerradosPorResultado.map((r) => [nombreResultado(r.resultado), formatoMiles(r.total)])}
        />
      </div>
    </>
  )
}

function ContenidoVentas({ d, idBase }: { d: ReportePorTipo['ventas']; idBase: string }) {
  const columnas = ['Ventas vigentes', 'Valor vendido']
  return (
    <>
      <Cifras
        titulo="Resumen de ventas"
        cifras={[
          { clave: 'vigentes', etiqueta: 'Ventas vigentes', valor: formatoMiles(d.vigentes.cantidad) },
          { clave: 'valor', etiqueta: 'Valor vendido', valor: formatoCop(d.vigentes.valorCop) },
          { clave: 'revertidas', etiqueta: 'Revertidas (no suman)', valor: formatoMiles(d.revertidas) },
        ]}
      />
      <TablaDatos
        id={`${idBase}-origen`}
        titulo="Ventas vigentes por origen"
        columnas={['Origen', ...columnas]}
        filas={d.porOrigen.map((o) => [nombreOrigenVenta(o.origen), formatoMiles(o.cantidad), formatoCop(o.valorCop)])}
      />
      <TablaDatos
        id={`${idBase}-categoria`}
        titulo="Ventas vigentes por categoría"
        columnas={['Categoría', ...columnas]}
        filas={d.porCategoria.map((c) => [c.nombre || `Categoría ${c.id}`, formatoMiles(c.cantidad), formatoCop(c.valorCop)])}
      />
      <TablaDatos
        id={`${idBase}-asesor`}
        titulo="Ventas vigentes por asesor"
        columnas={['Asesor', ...columnas]}
        filas={d.porAsesor.map((a) => [a.nombre || `Asesor ${a.id}`, formatoMiles(a.cantidad), formatoCop(a.valorCop)])}
        textoVacio="Sin ventas vigentes en el período."
      />
    </>
  )
}

function nombreMedioOSinDato(medio: string): string {
  return medio === 'SIN_DATO' || medio === '' ? 'Sin dato' : nombreMedioPago(medio)
}

function nombreConceptoOSinDato(concepto: string): string {
  return concepto === 'SIN_DATO' || concepto === '' ? 'Sin dato' : nombreConceptoPago(concepto)
}

const NOMBRES_ESTADO_PAGO_REPORTE: Readonly<Record<string, string>> = {
  REGISTRADO: 'Registrado',
  ANULADO: 'Anulado',
  PENDIENTE: 'Pendiente (registro anterior)',
  APROBADO: 'Aprobado (registro anterior)',
  RECHAZADO: 'Rechazado (registro anterior)',
}

function ContenidoSuscripciones({ d, idBase }: { d: ReportePorTipo['suscripciones']; idBase: string }) {
  const p = d.pagos
  return (
    <>
      <Aviso aviso={{ tipo: 'info', texto: d.aviso }} />
      <Cifras
        titulo="Resumen de suscripciones"
        cifras={[
          { clave: 'asignadas', etiqueta: 'Asignadas en el período', valor: formatoMiles(d.asignadasEnPeriodo) },
          { clave: 'renovadas', etiqueta: 'Renovadas en el período', valor: formatoMiles(d.renovadasEnPeriodo) },
          { clave: 'cambios', etiqueta: 'Cambios de plan en el período', valor: formatoMiles(d.cambiosDePlanEnPeriodo) },
          { clave: 'vencidas', etiqueta: 'Vencidas en el período', valor: formatoMiles(d.vencidasEnPeriodo) },
        ]}
      />
      <div className="reporte-tablas">
        <TablaDatos
          id={`${idBase}-estado`}
          titulo="Suscripciones por estado (hoy)"
          columnas={['Estado', 'Suscripciones']}
          filas={ESTADOS_SUSCRIPCION_REPORTE.map((e) => [nombreEstadoSuscripcion(e), formatoMiles(d.porEstado[e])])}
        />
        <TablaDatos
          id={`${idBase}-plan`}
          titulo="Suscripciones vigentes por plan"
          columnas={['Plan', 'Suscripciones']}
          filas={d.vigentesPorPlan.map((x) => [x.nombre || `Plan ${x.id}`, formatoMiles(x.total)])}
          textoVacio="Sin suscripciones vigentes."
        />
      </div>
      <h4 className="admin-subtitulo admin-subtitulo-menor">Pagos registrados en el período</h4>
      <Cifras
        titulo="Resumen de pagos"
        cifras={[
          { clave: 'cantidad', etiqueta: 'Pagos registrados', valor: formatoMiles(p.cantidad) },
          { clave: 'total', etiqueta: 'Total recibido', valor: formatoCop(p.totalRecibidoCop) },
          {
            clave: 'anulados',
            etiqueta: 'Anulados (no suman)',
            valor: formatoMiles(p.anulados.cantidad),
            detalle: formatoCop(p.anulados.valorCop),
          },
          { clave: 'sinFecha', etiqueta: 'Sin fecha de pago (se usa la de registro)', valor: formatoMiles(p.sinFechaPago) },
        ]}
      />
      <div className="reporte-tablas">
        <TablaDatos
          id={`${idBase}-medio`}
          titulo="Pagos por medio"
          columnas={['Medio', 'Pagos', 'Valor']}
          filas={p.porMedio.map((m) => [nombreMedioOSinDato(m.codigo), formatoMiles(m.cantidad), formatoCop(m.valorCop)])}
        />
        <TablaDatos
          id={`${idBase}-concepto`}
          titulo="Pagos por concepto"
          columnas={['Concepto', 'Pagos', 'Valor']}
          filas={p.porConcepto.map((c) => [nombreConceptoOSinDato(c.codigo), formatoMiles(c.cantidad), formatoCop(c.valorCop)])}
        />
        <TablaDatos
          id={`${idBase}-pagos-plan`}
          titulo="Pagos por plan"
          columnas={['Plan', 'Pagos', 'Valor']}
          filas={p.porPlan.map((x) => [x.nombre || `Plan ${x.id}`, formatoMiles(x.cantidad), formatoCop(x.valorCop)])}
          textoVacio="Sin pagos en el período."
        />
      </div>
    </>
  )
}

// ---------------------------------------------------------------- Filas de cada reporte

function FilasReporte({ resultado, idBase, ocupada }: { resultado: Resultado; idBase: string; ocupada: boolean }) {
  const comunes = { id: `${idBase}-filas`, ancha: true, ocupada, tituloVisible: false }
  const pie = (f: PaginaFilas<unknown>) => `página ${formatoMiles(f.pagina)}`
  switch (resultado.tipo) {
    case 'vehiculos': {
      const f = resultado.datos.filas
      return (
        <TablaDatos
          {...comunes}
          titulo={`Vehículos registrados en el período, ${pie(f)}`}
          columnas={['Vehículo', 'Categoría', 'Estado', 'Asesor', 'Ubicación', 'Registrado', 'Publicación', 'Contactos en el período']}
          filas={f.items.map((v) => [
            enlaceVehiculo({ id: v.vehiculoId, titulo: v.titulo }),
            v.categoria || '—',
            nombreEstado(v.estado),
            v.asesor?.nombre || '—',
            [v.municipio, v.departamento].filter(Boolean).join(', ') || '—',
            diaColombia(v.registradoEn),
            v.publicacion ? `#${v.publicacion.id} · ${NOMBRES_ESTADO_PUBLICACION_REPORTE[v.publicacion.estado] ?? (v.publicacion.estado === 'ELIMINADA' ? 'Eliminada' : v.publicacion.estado)}` : 'Sin publicación',
            formatoMiles(v.contactosEnPeriodo),
          ])}
        />
      )
    }
    case 'solicitudes': {
      const f = resultado.datos.filas
      return (
        <TablaDatos
          {...comunes}
          titulo={`Solicitudes recibidas en el período, ${pie(f)}`}
          columnas={['Solicitud', 'Recibida', 'Origen', 'Estado', 'Resultado', 'Cerrada', 'Responsable', 'Vehículo']}
          filas={f.items.map((s) => [
            `#${s.id}`,
            diaColombia(s.creadoEn),
            nombreOrigen(s.origen),
            NOMBRES_ESTADO_LEAD[s.estado as 'ABIERTO' | 'CERRADO'] ?? s.estado,
            s.resultado ? nombreResultado(s.resultado) : '—',
            diaColombia(s.cerradoEn),
            s.responsable.tipo === 'ADMINISTRACION' ? 'Administración' : s.responsable.nombre || `Asesor ${s.responsable.asesorId ?? ''}`,
            enlaceVehiculo(s.vehiculo),
          ])}
        />
      )
    }
    case 'creditos': {
      const f = resultado.datos.filas
      return (
        <TablaDatos
          {...comunes}
          titulo={`Créditos recibidos en el período, ${pie(f)}`}
          columnas={['Crédito', 'Recibido', 'Canal', 'Estado', 'Resultado', 'Cerrado', 'Vehículo']}
          filas={f.items.map((c) => [
            `#${c.id}`,
            diaColombia(c.creadoEn),
            nombreCanalCredito(c.canal),
            NOMBRES_ESTADO_GESTION[c.estado as 'REGISTRADO'] ?? c.estado,
            c.resultado ? nombreResultado(c.resultado) : '—',
            diaColombia(c.cerradoEn),
            enlaceVehiculo(c.vehiculo),
          ])}
        />
      )
    }
    case 'ventas': {
      const f = resultado.datos.filas
      return (
        <TablaDatos
          {...comunes}
          titulo={`Ventas del período, ${pie(f)}`}
          columnas={['Fecha de venta', 'Vehículo', 'Categoría', 'Asesor', 'Origen', 'Precio final', 'Estado']}
          filas={f.items.map((v) => [
            diaColombia(v.fechaVenta),
            enlaceVehiculo(v.vehiculo),
            v.categoria || '—',
            v.asesor?.nombre || '—',
            nombreOrigenVenta(v.origen),
            formatoCop(v.precioFinalCop),
            nombreEstadoVenta(v.estado),
          ])}
        />
      )
    }
    case 'suscripciones': {
      const f = resultado.datos.filas
      return (
        <TablaDatos
          {...comunes}
          titulo={`Pagos del período, ${pie(f)}`}
          columnas={['Fecha', 'Suscripción', 'Asesor', 'Plan', 'Medio', 'Concepto', 'Valor', 'Estado']}
          filas={f.items.map((x) => [
            x.sinFechaPago ? `${diaColombia(x.fecha)} (sin fecha de pago)` : diaColombia(x.fecha),
            `#${x.suscripcionId}`,
            x.asesor?.nombre || '—',
            x.plan?.nombre || '—',
            nombreMedioOSinDato(x.medio),
            nombreConceptoOSinDato(x.concepto),
            formatoCop(x.valorCop),
            NOMBRES_ESTADO_PAGO_REPORTE[x.estado] ?? (x.estado || '—'),
          ])}
        />
      )
    }
  }
}
