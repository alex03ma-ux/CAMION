// P8 — Reportes, indicadores y exportación CSV (contrato docs/criterios/P8-contrato-api.md §1 a §9; HU-075 a HU-080,
// RN-RE-001 a 006; D-68 A, D-69 A/A, D-70 A, D-71). Solo lectura (GET). Los reportes, los indicadores del portal y el
// CSV son del administrador (SoloAdministrador); "Mis indicadores" es del asesor (SoloAsesor) y no se exporta.
// Las respuestas no traen datos personales de clientes (D-71 b A). El token va siempre en el encabezado, nunca en la URL.
import { MENSAJE_SESION_TERMINADA } from './auth'
import { ApiError, descargarArchivo, esObjetoJson, MENSAJE_INESPERADO, peticion, type ArchivoDescargado } from './http'
import { fechaIsoValida, hoyColombia } from './pagos'

// ---- Tipos comunes ----

export type TipoReporte = 'vehiculos' | 'solicitudes' | 'creditos' | 'ventas' | 'suscripciones'
export const TIPOS_REPORTE: readonly TipoReporte[] = ['vehiculos', 'solicitudes', 'creditos', 'ventas', 'suscripciones']
export const NOMBRES_REPORTE: Readonly<Record<TipoReporte, string>> = {
  vehiculos: 'Vehículos y publicaciones',
  solicitudes: 'Solicitudes',
  creditos: 'Créditos',
  ventas: 'Ventas',
  suscripciones: 'Suscripciones y pagos',
}

export type Periodo = { desde: string; hasta: string }
export type PaginaFilas<T> = { items: T[]; total: number; pagina: number; tamano: number }
export type Conteo = { id: number; nombre: string; total: number }
export type ConteoValor = { cantidad: number; valorCop: number }

/** Filtros de un reporte: `desde`, `hasta` y los propios (valores de texto; vacío = sin filtro). */
export type FiltrosReporte = Record<string, string>

/** Parámetros propios de cada reporte, además de `desde` y `hasta` (§2 a §6). */
export const CAMPOS_FILTRO_REPORTE: Readonly<Record<TipoReporte, readonly string[]>> = {
  vehiculos: ['asesorId', 'categoriaId', 'departamentoId', 'municipioId'],
  solicitudes: ['origen', 'estado', 'resultado', 'responsable', 'categoriaId'],
  creditos: ['estado', 'resultado', 'canal'],
  ventas: ['asesorId', 'categoriaId', 'origen'],
  suscripciones: ['asesorId', 'planId', 'medio'],
}

export const TAMANO_PAGINA_REPORTE = 20

export const MENSAJE_FILTRO_FECHA = 'Escribe una fecha válida (aaaa-mm-dd).'
export const MENSAJE_FILTRO_RANGO = 'La fecha inicial no puede ser posterior a la final.'
export const MENSAJE_FILTRO_366 = 'El período no puede superar 366 días.'
export const MENSAJE_SIN_DATOS_REPORTE = 'No hay datos para estos filtros.'
export const AVISO_PAGOS = 'Pagos registrados a mano por la administración. No es contabilidad ni facturación.'

const MENSAJES_REPORTES: Partial<Record<number, string>> = { 401: MENSAJE_SESION_TERMINADA }

// ---- Período por defecto y validación en cliente (mismos mensajes que la API, §1) ----

function diasDelMes(anio: number, mes: number): number {
  return new Date(Date.UTC(anio, mes, 0)).getUTCDate()
}

/** Del día 1 al último día del mes en curso en Colombia. */
export function mesEnCurso(hoy: string = hoyColombia()): Periodo {
  const [anio, mes] = hoy.split('-').map(Number)
  const mm = String(mes).padStart(2, '0')
  return { desde: `${anio}-${mm}-01`, hasta: `${anio}-${mm}-${String(diasDelMes(anio, mes)).padStart(2, '0')}` }
}

/** Días del período contando ambos extremos. */
export function diasDelPeriodo(desde: string, hasta: string): number {
  return Math.round((Date.parse(`${hasta}T00:00:00Z`) - Date.parse(`${desde}T00:00:00Z`)) / 86_400_000) + 1
}

export type ErroresPeriodo = Partial<Record<'desde' | 'hasta', string[]>>

/** Fechas vacías se permiten (la API usa el mes en curso). */
export function validarPeriodo(f: { desde: string; hasta: string }): ErroresPeriodo {
  const errores: ErroresPeriodo = {}
  if (f.desde !== '' && !fechaIsoValida(f.desde)) errores.desde = [MENSAJE_FILTRO_FECHA]
  if (f.hasta !== '' && !fechaIsoValida(f.hasta)) errores.hasta = [MENSAJE_FILTRO_FECHA]
  if (errores.desde || errores.hasta || f.desde === '' || f.hasta === '') return errores
  if (f.desde > f.hasta) errores.hasta = [MENSAJE_FILTRO_RANGO]
  else if (diasDelPeriodo(f.desde, f.hasta) > 366) errores.hasta = [MENSAJE_FILTRO_366]
  return errores
}

/** Cadena de consulta con los filtros no vacíos (y la página, si se indica). */
export function consultaReporte(filtros: FiltrosReporte, pagina?: number, tamano: number = TAMANO_PAGINA_REPORTE): string {
  const p = new URLSearchParams()
  for (const [clave, valor] of Object.entries(filtros)) {
    const limpio = valor.trim()
    if (limpio !== '') p.set(clave, limpio)
  }
  if (pagina !== undefined) {
    p.set('pagina', String(pagina))
    p.set('tamano', String(tamano))
  }
  return p.toString()
}

/** "Del 2026-11-01 al 2026-11-30" (CA-P8-02). */
export function textoPeriodo(p: Periodo): string {
  return `Del ${p.desde} al ${p.hasta}`
}

// ---- Lectura tolerante ----

function texto(valor: unknown): string {
  return typeof valor === 'string' ? valor : ''
}

function textoONulo(valor: unknown): string | null {
  return typeof valor === 'string' && valor.trim() !== '' ? valor : null
}

function codigo(valor: unknown): string {
  return texto(valor).toUpperCase()
}

function numero(valor: unknown): number {
  return typeof valor === 'number' && Number.isFinite(valor) ? valor : 0
}

function numeroONulo(valor: unknown): number | null {
  return typeof valor === 'number' && Number.isFinite(valor) ? valor : null
}

function objeto(valor: unknown): Record<string, unknown> {
  return esObjetoJson(valor) ? valor : {}
}

function lista(valor: unknown): Record<string, unknown>[] {
  return Array.isArray(valor) ? valor.filter(esObjetoJson) : []
}

function porClaves<K extends string>(valor: unknown, claves: readonly K[]): Record<K, number> {
  const o = objeto(valor)
  return Object.fromEntries(claves.map((k) => [k, numero(o[k])])) as Record<K, number>
}

function leerPeriodo(valor: unknown, porDefecto: Periodo): Periodo {
  const o = objeto(valor)
  const desde = texto(o.desde)
  const hasta = texto(o.hasta)
  return { desde: fechaIsoValida(desde) ? desde : porDefecto.desde, hasta: fechaIsoValida(hasta) ? hasta : porDefecto.hasta }
}

function leerConteo(o: Record<string, unknown>): Conteo {
  return { id: numero(o.id), nombre: texto(o.nombre), total: numero(o.total) }
}

function leerConteoValor(valor: unknown): ConteoValor {
  const o = objeto(valor)
  return { cantidad: numero(o.cantidad), valorCop: numero(o.valorCop) }
}

function leerPagina<T>(valor: unknown, leer: (o: Record<string, unknown>) => T): PaginaFilas<T> {
  const o = objeto(valor)
  const items = lista(o.items).map(leer)
  return {
    items,
    total: numeroONulo(o.total) ?? items.length,
    pagina: numeroONulo(o.pagina) ?? 1,
    tamano: numeroONulo(o.tamano) ?? TAMANO_PAGINA_REPORTE,
  }
}

function leerRef(valor: unknown): { id: number; nombre: string } | null {
  if (!esObjetoJson(valor)) return null
  return { id: numero(valor.id), nombre: texto(valor.nombre) }
}

function leerVehiculoRef(valor: unknown): { id: number; titulo: string } | null {
  if (!esObjetoJson(valor)) return null
  return { id: numero(valor.id), titulo: texto(valor.titulo) }
}

// ---- §2 Vehículos y publicaciones ----

export const ESTADOS_INVENTARIO = ['ACTIVO', 'RESERVADO', 'INACTIVO', 'VENDIDO'] as const
export type EstadoInventario = (typeof ESTADOS_INVENTARIO)[number]
export const ESTADOS_PUBLICACION_REPORTE = ['BORRADOR', 'PUBLICADA', 'OCULTA'] as const
export type EstadoPublicacionReporte = (typeof ESTADOS_PUBLICACION_REPORTE)[number]

export type FilaVehiculo = {
  vehiculoId: number
  titulo: string
  categoria: string
  estado: string
  asesor: { id: number; nombre: string } | null
  municipio: string
  departamento: string
  registradoEn: string | null
  publicacion: { id: number; estado: string } | null
  contactosEnPeriodo: number
}

export type ReporteVehiculos = {
  periodo: Periodo
  inventarioPorEstado: Record<EstadoInventario, number>
  inventarioPorCategoria: Conteo[]
  registradosEnPeriodo: number
  publicacionesPorEstado: Record<EstadoPublicacionReporte, number>
  publicacionesEliminadas: number
  publicacionesVisiblesHoy: number
  publicacionesDestacadasHoy: number
  masContactados: { vehiculoId: number; titulo: string; contactos: number }[]
  filas: PaginaFilas<FilaVehiculo>
}

export function leerReporteVehiculos(valor: unknown, porDefecto: Periodo): ReporteVehiculos {
  const o = objeto(valor)
  return {
    periodo: leerPeriodo(o.periodo, porDefecto),
    inventarioPorEstado: porClaves(o.inventarioPorEstado, ESTADOS_INVENTARIO),
    inventarioPorCategoria: lista(o.inventarioPorCategoria).map(leerConteo),
    registradosEnPeriodo: numero(o.registradosEnPeriodo),
    publicacionesPorEstado: porClaves(o.publicacionesPorEstado, ESTADOS_PUBLICACION_REPORTE),
    publicacionesEliminadas: numero(o.publicacionesEliminadas),
    publicacionesVisiblesHoy: numero(o.publicacionesVisiblesHoy),
    publicacionesDestacadasHoy: numero(o.publicacionesDestacadasHoy),
    masContactados: lista(o.masContactados).map((m) => ({
      vehiculoId: numero(m.vehiculoId),
      titulo: texto(m.titulo),
      contactos: numero(m.contactos),
    })),
    filas: leerPagina(o.filas, (f) => {
      const pub = esObjetoJson(f.publicacion) ? f.publicacion : null
      return {
        vehiculoId: numero(f.vehiculoId),
        titulo: texto(f.titulo),
        categoria: texto(f.categoria),
        estado: codigo(f.estado),
        asesor: leerRef(f.asesor),
        municipio: texto(f.municipio),
        departamento: texto(f.departamento),
        registradoEn: textoONulo(f.registradoEn),
        publicacion: pub ? { id: numero(pub.id), estado: codigo(pub.estado) } : null,
        contactosEnPeriodo: numero(f.contactosEnPeriodo),
      }
    }),
  }
}

// ---- §3 Solicitudes ----

export type ResponsableFila = { tipo: 'ADMINISTRACION' | 'ASESOR'; asesorId: number | null; nombre: string }

export type FilaSolicitud = {
  id: number
  creadoEn: string | null
  origen: string
  estado: string
  resultado: string | null
  cerradoEn: string | null
  responsable: ResponsableFila
  vehiculo: { id: number; titulo: string } | null
}

export type ReporteSolicitudes = {
  periodo: Periodo
  recibidas: number
  porOrigen: { origen: string; recibidas: number; abiertas: number; cerradas: number }[]
  cerradasEnPeriodo: number
  cerradasPorResultado: { origen: string; resultado: string; total: number }[]
  tiempoMedioCierre: { origen: string; cerradas: number; dias: number | null }[]
  contactosPorResponsable: {
    administracion: number
    asesores: Conteo[]
    reasignadosAMano: number
    pasadosAAdministracion: number
  }
  filas: PaginaFilas<FilaSolicitud>
}

function leerResponsable(valor: unknown): ResponsableFila {
  const o = objeto(valor)
  const tipo = codigo(o.tipo) === 'ASESOR' ? 'ASESOR' : 'ADMINISTRACION'
  return { tipo, asesorId: numeroONulo(o.asesorId), nombre: texto(o.nombre) }
}

export function leerReporteSolicitudes(valor: unknown, porDefecto: Periodo): ReporteSolicitudes {
  const o = objeto(valor)
  const c = objeto(o.contactosPorResponsable)
  return {
    periodo: leerPeriodo(o.periodo, porDefecto),
    recibidas: numero(o.recibidas),
    porOrigen: lista(o.porOrigen).map((x) => ({
      origen: codigo(x.origen),
      recibidas: numero(x.recibidas),
      abiertas: numero(x.abiertas),
      cerradas: numero(x.cerradas),
    })),
    cerradasEnPeriodo: numero(o.cerradasEnPeriodo),
    cerradasPorResultado: lista(o.cerradasPorResultado).map((x) => ({
      origen: codigo(x.origen),
      resultado: codigo(x.resultado),
      total: numero(x.total),
    })),
    tiempoMedioCierre: lista(o.tiempoMedioCierre).map((x) => ({
      origen: codigo(x.origen),
      cerradas: numero(x.cerradas),
      dias: numeroONulo(x.dias),
    })),
    contactosPorResponsable: {
      administracion: numero(c.administracion),
      asesores: lista(c.asesores).map(leerConteo),
      reasignadosAMano: numero(c.reasignadosAMano),
      pasadosAAdministracion: numero(c.pasadosAAdministracion),
    },
    filas: leerPagina(o.filas, (f) => ({
      id: numero(f.id),
      creadoEn: textoONulo(f.creadoEn),
      origen: codigo(f.origen),
      estado: codigo(f.estado),
      resultado: textoONulo(f.resultado)?.toUpperCase() ?? null,
      cerradoEn: textoONulo(f.cerradoEn),
      responsable: leerResponsable(f.responsable),
      vehiculo: leerVehiculoRef(f.vehiculo),
    })),
  }
}

// ---- §4 Créditos ----

export const ESTADOS_CREDITO_REPORTE = ['REGISTRADO', 'EN_GESTION', 'CERRADO'] as const
export type EstadoCreditoReporte = (typeof ESTADOS_CREDITO_REPORTE)[number]
export const CANALES_CREDITO = ['PORTADA', 'FICHA'] as const
export type CanalCredito = (typeof CANALES_CREDITO)[number]
export const NOMBRES_CANAL_CREDITO: Readonly<Record<CanalCredito, string>> = {
  PORTADA: 'Desde la portada',
  FICHA: 'Desde la ficha de un vehículo',
}

export function nombreCanalCredito(canal: string): string {
  const clave = canal.toUpperCase()
  return clave in NOMBRES_CANAL_CREDITO ? NOMBRES_CANAL_CREDITO[clave as CanalCredito] : canal || '—'
}

export type FilaCredito = {
  id: number
  creadoEn: string | null
  canal: string
  estado: string
  resultado: string | null
  cerradoEn: string | null
  vehiculo: { id: number; titulo: string } | null
}

export type ReporteCreditos = {
  periodo: Periodo
  recibidos: number
  porEstado: Record<EstadoCreditoReporte, number>
  porCanal: Record<CanalCredito, number>
  cerrados: number
  cerradosPorResultado: { resultado: string; total: number }[]
  porcentajeRemitido: number | null
  filas: PaginaFilas<FilaCredito>
}

export function leerReporteCreditos(valor: unknown, porDefecto: Periodo): ReporteCreditos {
  const o = objeto(valor)
  return {
    periodo: leerPeriodo(o.periodo, porDefecto),
    recibidos: numero(o.recibidos),
    porEstado: porClaves(o.porEstado, ESTADOS_CREDITO_REPORTE),
    porCanal: porClaves(o.porCanal, CANALES_CREDITO),
    cerrados: numero(o.cerrados),
    cerradosPorResultado: lista(o.cerradosPorResultado).map((x) => ({ resultado: codigo(x.resultado), total: numero(x.total) })),
    porcentajeRemitido: numeroONulo(o.porcentajeRemitido),
    filas: leerPagina(o.filas, (f) => ({
      id: numero(f.id),
      creadoEn: textoONulo(f.creadoEn),
      canal: codigo(f.canal),
      estado: codigo(f.estado),
      resultado: textoONulo(f.resultado)?.toUpperCase() ?? null,
      cerradoEn: textoONulo(f.cerradoEn),
      vehiculo: leerVehiculoRef(f.vehiculo),
    })),
  }
}

// ---- §5 Ventas ----

export type FilaVentaReporte = {
  id: number
  fechaVenta: string | null
  vehiculo: { id: number; titulo: string } | null
  categoria: string
  asesor: { id: number; nombre: string } | null
  origen: string
  precioFinalCop: number
  estado: string
}

export type ReporteVentas = {
  periodo: Periodo
  vigentes: ConteoValor
  revertidas: number
  porOrigen: ({ origen: string } & ConteoValor)[]
  porCategoria: ({ id: number; nombre: string } & ConteoValor)[]
  porAsesor: ({ id: number; nombre: string } & ConteoValor)[]
  filas: PaginaFilas<FilaVentaReporte>
}

function conValor(x: Record<string, unknown>): ConteoValor {
  return { cantidad: numero(x.cantidad), valorCop: numero(x.valorCop) }
}

export function leerReporteVentas(valor: unknown, porDefecto: Periodo): ReporteVentas {
  const o = objeto(valor)
  return {
    periodo: leerPeriodo(o.periodo, porDefecto),
    vigentes: leerConteoValor(o.vigentes),
    revertidas: numero(o.revertidas),
    porOrigen: lista(o.porOrigen).map((x) => ({ origen: codigo(x.origen), ...conValor(x) })),
    porCategoria: lista(o.porCategoria).map((x) => ({ id: numero(x.id), nombre: texto(x.nombre), ...conValor(x) })),
    porAsesor: lista(o.porAsesor).map((x) => ({ id: numero(x.id), nombre: texto(x.nombre), ...conValor(x) })),
    filas: leerPagina(o.filas, (f) => ({
      id: numero(f.id),
      fechaVenta: textoONulo(f.fechaVenta),
      vehiculo: leerVehiculoRef(f.vehiculo),
      categoria: texto(f.categoria),
      asesor: leerRef(f.asesor),
      origen: codigo(f.origen),
      precioFinalCop: numero(f.precioFinalCop),
      estado: codigo(f.estado) === 'REVERTIDA' ? 'REVERTIDA' : 'VIGENTE',
    })),
  }
}

// ---- §6 Suscripciones y pagos ----

export const ESTADOS_SUSCRIPCION_REPORTE = [
  'PENDIENTE',
  'ACTIVA',
  'SUSPENDIDA',
  'REACTIVADA',
  'VENCIDA',
  'CANCELADA',
  'RECHAZADA',
] as const
export type EstadoSuscripcionReporte = (typeof ESTADOS_SUSCRIPCION_REPORTE)[number]

export type FilaPagoReporte = {
  id: number
  fecha: string | null
  sinFechaPago: boolean
  suscripcionId: number
  asesor: { id: number; nombre: string } | null
  plan: { id: number; nombre: string } | null
  medio: string
  concepto: string
  valorCop: number
  estado: string
}

export type ReporteSuscripciones = {
  periodo: Periodo
  porEstado: Record<EstadoSuscripcionReporte, number>
  vigentesPorPlan: Conteo[]
  asignadasEnPeriodo: number
  renovadasEnPeriodo: number
  cambiosDePlanEnPeriodo: number
  vencidasEnPeriodo: number
  pagos: {
    cantidad: number
    totalRecibidoCop: number
    porMedio: ({ codigo: string } & ConteoValor)[]
    porPlan: ({ id: number; nombre: string } & ConteoValor)[]
    porConcepto: ({ codigo: string } & ConteoValor)[]
    anulados: ConteoValor
    sinFechaPago: number
  }
  aviso: string
  filas: PaginaFilas<FilaPagoReporte>
}

export function leerReporteSuscripciones(valor: unknown, porDefecto: Periodo): ReporteSuscripciones {
  const o = objeto(valor)
  const p = objeto(o.pagos)
  return {
    periodo: leerPeriodo(o.periodo, porDefecto),
    porEstado: porClaves(o.porEstado, ESTADOS_SUSCRIPCION_REPORTE),
    vigentesPorPlan: lista(o.vigentesPorPlan).map(leerConteo),
    asignadasEnPeriodo: numero(o.asignadasEnPeriodo),
    renovadasEnPeriodo: numero(o.renovadasEnPeriodo),
    cambiosDePlanEnPeriodo: numero(o.cambiosDePlanEnPeriodo),
    vencidasEnPeriodo: numero(o.vencidasEnPeriodo),
    pagos: {
      cantidad: numero(p.cantidad),
      totalRecibidoCop: numero(p.totalRecibidoCop),
      porMedio: lista(p.porMedio).map((x) => ({ codigo: codigo(x.codigo), ...conValor(x) })),
      porPlan: lista(p.porPlan).map((x) => ({ id: numero(x.id), nombre: texto(x.nombre), ...conValor(x) })),
      porConcepto: lista(p.porConcepto).map((x) => ({ codigo: codigo(x.codigo), ...conValor(x) })),
      anulados: leerConteoValor(p.anulados),
      sinFechaPago: numero(p.sinFechaPago),
    },
    // CA-P8-33: el aviso se muestra siempre, aunque la respuesta no lo traiga.
    aviso: textoONulo(o.aviso) ?? AVISO_PAGOS,
    filas: leerPagina(o.filas, (f) => ({
      id: numero(f.id),
      fecha: textoONulo(f.fecha),
      sinFechaPago: f.sinFechaPago === true,
      suscripcionId: numero(f.suscripcionId),
      asesor: leerRef(f.asesor),
      plan: leerRef(f.plan),
      medio: codigo(f.medio),
      concepto: codigo(f.concepto),
      valorCop: numero(f.valorCop),
      estado: codigo(f.estado),
    })),
  }
}

// ---- Endpoints de los reportes ----

export type ReportePorTipo = {
  vehiculos: ReporteVehiculos
  solicitudes: ReporteSolicitudes
  creditos: ReporteCreditos
  ventas: ReporteVentas
  suscripciones: ReporteSuscripciones
}

const LECTORES: { [T in TipoReporte]: (valor: unknown, porDefecto: Periodo) => ReportePorTipo[T] } = {
  vehiculos: leerReporteVehiculos,
  solicitudes: leerReporteSolicitudes,
  creditos: leerReporteCreditos,
  ventas: leerReporteVentas,
  suscripciones: leerReporteSuscripciones,
}

/** Solo los filtros que usa el reporte (los demás se ignoran). */
export function filtrosDelReporte(tipo: TipoReporte, filtros: FiltrosReporte): FiltrosReporte {
  const resultado: FiltrosReporte = { desde: filtros.desde ?? '', hasta: filtros.hasta ?? '' }
  for (const campo of CAMPOS_FILTRO_REPORTE[tipo]) resultado[campo] = filtros[campo] ?? ''
  return resultado
}

export async function obtenerReporte<T extends TipoReporte>(
  token: string,
  tipo: T,
  filtros: FiltrosReporte,
  pagina: number,
  signal?: AbortSignal,
): Promise<ReportePorTipo[T]> {
  const consulta = consultaReporte(filtrosDelReporte(tipo, filtros), pagina)
  const respuesta = await peticion(`/api/admin/reportes/${tipo}?${consulta}`, {
    token,
    signal,
    mensajesPorCodigo: MENSAJES_REPORTES,
  })
  if (!esObjetoJson(respuesta)) throw new ApiError(200, MENSAJE_INESPERADO)
  const porDefecto: Periodo = mesEnCurso()
  return LECTORES[tipo](respuesta, {
    desde: filtros.desde || porDefecto.desde,
    hasta: filtros.hasta || porDefecto.hasta,
  })
}

/** Nombre por defecto del CSV si la respuesta no trae Content-Disposition (§7). */
export function nombreCsvPorDefecto(tipo: TipoReporte, periodo: Periodo): string {
  return `reporte-${tipo}-${periodo.desde}_${periodo.hasta}.csv`
}

/** §7 (HU-079): todas las filas del reporte con los mismos filtros; el token va en el encabezado. */
export async function descargarReporteCsv(
  token: string,
  tipo: TipoReporte,
  filtros: FiltrosReporte,
  signal?: AbortSignal,
): Promise<ArchivoDescargado> {
  const consulta = consultaReporte(filtrosDelReporte(tipo, filtros))
  const ruta = `/api/admin/reportes/${tipo}/csv${consulta === '' ? '' : `?${consulta}`}`
  return descargarArchivo(ruta, { token, signal, mensajesPorCodigo: MENSAJES_REPORTES })
}

// ---- §8 Indicadores del administrador ----

export type Comparacion = { actual: number | null; anterior: number | null; diferencia: number | null }

export const CLAVES_INDICADORES = [
  'contactosRecibidos',
  'porcentajeVentaConcretada',
  'ventasVigentes',
  'valorVendidoCop',
  'solicitudesAdministracion',
  'creditosRemitidos',
  'ingresosPlanesCop',
  'tiempoMedioCierreDias',
] as const
export type ClaveIndicador = (typeof CLAVES_INDICADORES)[number]

export type IndicadoresAdmin = {
  periodo: Periodo
  periodoAnterior: Periodo | null
  indicadores: Record<ClaveIndicador, Comparacion>
  solicitudesAdministracionPorOrigen: { origen: string; actual: number; anterior: number }[]
  publicacionesVisiblesHoy: number
  graficos: {
    ventasPorMes: { mes: string; cantidad: number; valorCop: number }[]
    ventasPorOrigen: ({ origen: string } & ConteoValor)[]
    solicitudesPorOrigen: { origen: string; total: number }[]
    contactosPorResultado: { resultado: string; total: number }[]
  }
}

function leerComparacion(valor: unknown): Comparacion {
  const o = objeto(valor)
  const actual = numeroONulo(o.actual)
  const anterior = numeroONulo(o.anterior)
  const diferencia = numeroONulo(o.diferencia) ?? (actual !== null && anterior !== null ? actual - anterior : null)
  return { actual, anterior, diferencia: actual === null || anterior === null ? null : diferencia }
}

export function leerIndicadoresAdmin(valor: unknown, porDefecto: Periodo): IndicadoresAdmin {
  const o = objeto(valor)
  const ind = objeto(o.indicadores)
  const g = objeto(o.graficos)
  return {
    periodo: leerPeriodo(o.periodo, porDefecto),
    periodoAnterior: esObjetoJson(o.periodoAnterior) ? leerPeriodo(o.periodoAnterior, porDefecto) : null,
    indicadores: Object.fromEntries(CLAVES_INDICADORES.map((k) => [k, leerComparacion(ind[k])])) as Record<
      ClaveIndicador,
      Comparacion
    >,
    solicitudesAdministracionPorOrigen: lista(o.solicitudesAdministracionPorOrigen).map((x) => ({
      origen: codigo(x.origen),
      actual: numero(x.actual),
      anterior: numero(x.anterior),
    })),
    publicacionesVisiblesHoy: numero(o.publicacionesVisiblesHoy),
    graficos: {
      ventasPorMes: lista(g.ventasPorMes)
        .filter((x) => /^\d{4}-\d{2}$/.test(texto(x.mes)))
        .map((x) => ({ mes: texto(x.mes), ...conValor(x) })),
      ventasPorOrigen: lista(g.ventasPorOrigen).map((x) => ({ origen: codigo(x.origen), ...conValor(x) })),
      solicitudesPorOrigen: lista(g.solicitudesPorOrigen).map((x) => ({ origen: codigo(x.origen), total: numero(x.total) })),
      contactosPorResultado: lista(g.contactosPorResultado).map((x) => ({
        resultado: codigo(x.resultado),
        total: numero(x.total),
      })),
    },
  }
}

function periodoConsulta(p: { desde: string; hasta: string }): string {
  const consulta = consultaReporte({ desde: p.desde, hasta: p.hasta })
  return consulta === '' ? '' : `?${consulta}`
}

export async function obtenerIndicadoresAdmin(
  token: string,
  periodo: { desde: string; hasta: string },
  signal?: AbortSignal,
): Promise<IndicadoresAdmin> {
  const respuesta = await peticion(`/api/admin/indicadores${periodoConsulta(periodo)}`, {
    token,
    signal,
    mensajesPorCodigo: MENSAJES_REPORTES,
  })
  if (!esObjetoJson(respuesta)) throw new ApiError(200, MENSAJE_INESPERADO)
  const porDefecto = mesEnCurso()
  return leerIndicadoresAdmin(respuesta, { desde: periodo.desde || porDefecto.desde, hasta: periodo.hasta || porDefecto.hasta })
}

// ---- §9 "Mis indicadores" del asesor ----

export type IndicadoresAsesor = {
  periodo: Periodo
  vehiculosPorEstado: Record<EstadoInventario, number>
  publicacionesVisiblesHoy: number
  contactosRecibidos: number
  contactosAbiertos: number
  contactosPorResultado: { resultado: string; total: number }[]
  ventasVigentes: ConteoValor
}

export function leerIndicadoresAsesor(valor: unknown, porDefecto: Periodo): IndicadoresAsesor {
  const o = objeto(valor)
  return {
    periodo: leerPeriodo(o.periodo, porDefecto),
    vehiculosPorEstado: porClaves(o.vehiculosPorEstado, ESTADOS_INVENTARIO),
    publicacionesVisiblesHoy: numero(o.publicacionesVisiblesHoy),
    contactosRecibidos: numero(o.contactosRecibidos),
    contactosAbiertos: numero(o.contactosAbiertos),
    contactosPorResultado: lista(o.contactosPorResultado).map((x) => ({ resultado: codigo(x.resultado), total: numero(x.total) })),
    ventasVigentes: leerConteoValor(o.ventasVigentes),
  }
}

export async function obtenerIndicadoresAsesor(
  token: string,
  periodo: { desde: string; hasta: string },
  signal?: AbortSignal,
): Promise<IndicadoresAsesor> {
  const respuesta = await peticion(`/api/asesor/indicadores${periodoConsulta(periodo)}`, {
    token,
    signal,
    mensajesPorCodigo: MENSAJES_REPORTES,
  })
  if (!esObjetoJson(respuesta)) throw new ApiError(200, MENSAJE_INESPERADO)
  const porDefecto = mesEnCurso()
  return leerIndicadoresAsesor(respuesta, { desde: periodo.desde || porDefecto.desde, hasta: periodo.hasta || porDefecto.hasta })
}

// ---- Formatos de las cifras ----

const UN_DECIMAL = new Intl.NumberFormat('es-CO', { minimumFractionDigits: 1, maximumFractionDigits: 1 })

/** 25 → "25,0 %"; null → "Sin datos" (CA-P8-38). */
export function textoPorcentaje(valor: number | null): string {
  return valor === null ? 'Sin datos' : `${UN_DECIMAL.format(valor)} %`
}

/** 2.5 → "2,5 días"; null → "Sin datos". */
export function textoDias(valor: number | null): string {
  if (valor === null) return 'Sin datos'
  return valor === 1 ? '1,0 día' : `${UN_DECIMAL.format(valor)} días`
}

/** Mes "2025-12" → "dic. 2025". */
export function textoMes(mes: string): string {
  const [anio, m] = mes.split('-').map(Number)
  if (!anio || !m) return mes
  return new Intl.DateTimeFormat('es-CO', { month: 'short', year: 'numeric', timeZone: 'UTC' }).format(
    Date.UTC(anio, m - 1, 15),
  )
}

/** Fecha y día de Colombia de una fecha ISO UTC ("2026-11-05T15:00:00Z" → "2026-11-05"); "—" si no hay. */
export function diaColombia(valor: string | null): string {
  if (!valor) return '—'
  if (/^\d{4}-\d{2}-\d{2}$/.test(valor)) return valor
  const ms = Date.parse(valor)
  return Number.isNaN(ms) ? '—' : hoyColombia(new Date(ms))
}
