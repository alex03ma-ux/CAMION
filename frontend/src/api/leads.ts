// P5 — Gestión de contactos (leads) en el panel: /api/leads (contrato docs/criterios/P5-contrato-api.md).
// HU-031 a HU-033, RN-CL-003, RN-CL-004, D-7, D-46 (B), D-48. Política AsesorOAdministrador; reabrir y reasignar
// solo el administrador. La interfaz solo oculta: la API vuelve a comprobar el rol y el responsable (404 si no es suyo).
// P5b (docs/criterios/P5b-contrato-api.md): los avisos "Vende tu camión" y "Seguro todo riesgo" son leads sin
// vehículo, con `origen` y (el seguro) `tipoVehiculo`; tienen sus propios resultados de cierre y nunca se reasignan (D-52).
import { MENSAJE_SESION_TERMINADA } from './auth'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion } from './http'

// ---- Tipos del contrato ----

export type EstadoLead = 'ABIERTO' | 'CERRADO'

/** P5b: de dónde viene la solicitud. */
export type OrigenLead = 'CONTACTO_VEHICULO' | 'VENDE_TU_CAMION' | 'SEGURO_TODO_RIESGO'
export const ORIGENES_LEAD: readonly OrigenLead[] = ['CONTACTO_VEHICULO', 'VENDE_TU_CAMION', 'SEGURO_TODO_RIESGO']

export type ResponsableLead = {
  tipo: 'ASESOR' | 'ADMINISTRACION'
  /** perfiles_asesor.id, como texto (igual que los asesores del inventario). */
  asesorId: string | null
  nombre: string | null
}

export type VehiculoLead = {
  vehiculoId: number
  publicacionId: number
  titulo: string
  slug: string
  estadoVehiculo: string
  estadoPublicacion: string
}

export type LeadItem = {
  id: number
  estado: string
  nombre: string
  telefono: string
  correo: string | null
  mensaje: string
  creadoEn: string
  nuevo: boolean
  /** P5b: los contactos de P5 son CONTACTO_VEHICULO. */
  origen: OrigenLead
  /** P5b: solo en SEGURO_TODO_RIESGO ("CAMION", "CARRO", "MAQUINARIA", "OTRO"). */
  tipoVehiculo: string | null
  /** P5b: null en los avisos ("Sin vehículo asociado", CA-P5b-38). */
  vehiculo: VehiculoLead | null
  responsable: ResponsableLead
  resultadoCierre: string | null
  cerradoEn: string | null
}

export type EventoLead = {
  tipo: string
  fecha: string
  actor: { tipo: 'USUARIO' | 'SISTEMA'; nombre: string | null }
  automatico: boolean
  texto: string | null
  resultado: string | null
  responsableAnterior: ResponsableLead | null
  responsableNuevo: ResponsableLead | null
}

export type ConsentimientoLead = { formulario: string; versionPolitica: string; aceptadoEn: string }

export type LeadDetalle = LeadItem & {
  observacionCierre: string | null
  cerradoPor: string | null
  eventos: EventoLead[]
  /** Solo para el administrador; null para el asesor (CA-P5-16). */
  consentimientos: ConsentimientoLead[] | null
}

export type ListaLeads = { items: LeadItem[]; total: number; pagina: number; tamano: number }
export type ResumenLeads = { nuevos: number; abiertos: number }

export type FiltrosLeads = {
  estado: EstadoLead | ''
  /** Solo administrador: 'ADMINISTRACION' o un perfiles_asesor.id. */
  responsable: string
  /** P5b, solo administrador: filtro por origen. */
  origen: OrigenLead | ''
  vehiculoId: string
  desde: string
  hasta: string
  pagina: number
  tamano: number
}

export type ResultadoCierre =
  | 'VENTA_CONCRETADA'
  | 'NO_INTERESADO'
  | 'NO_RESPONDIO'
  | 'VEHICULO_NO_DISPONIBLE'
  | 'DUPLICADO_INVALIDO'
  | 'ATENDIDA'
  | 'OTRO'

// ---- Constantes y textos ----

export const TAMANO_PAGINA_LEADS = 20
export const RESPONSABLE_ADMINISTRACION = 'ADMINISTRACION'
export const TEXTO_MIN = 5
export const TEXTO_MAX = 500

export const MENSAJE_LEAD_NO_ENCONTRADO = 'Contacto no encontrado.'
export const MENSAJE_REVISA_FILTROS = 'Revisa los filtros.'
export const MENSAJE_FECHA = 'Escribe una fecha válida (aaaa-mm-dd).'
export const MENSAJE_FECHA_RANGO = 'La fecha final debe ser igual o posterior a la inicial.'
export const MENSAJE_NOTA = 'La nota debe tener entre 5 y 500 caracteres.'
export const MENSAJE_RESULTADO = 'Elige el resultado del cierre.'
export const MENSAJE_OBSERVACION_OTRO = 'Escribe la observación (5 a 500 caracteres).'
export const MENSAJE_OBSERVACION_LARGA = 'La observación no puede tener más de 500 caracteres.'
export const MENSAJE_MOTIVO_LEAD = 'Escribe el motivo (5 a 500 caracteres).'
export const MENSAJE_DESTINO = 'Elige el nuevo responsable.'
export const MENSAJE_RESULTADO_NO_APLICA = 'Ese resultado no aplica a esta solicitud.'
export const MENSAJE_AVISO_SIN_REASIGNAR = 'Esta solicitud la atiende siempre la administración.'

/** CA-P5-36: la pantalla muestra el texto, no el código. */
export const NOMBRES_ESTADO_LEAD: Record<EstadoLead, string> = {
  ABIERTO: 'Pendiente de gestión',
  CERRADO: 'Finalizado',
}

export const RESULTADOS_CIERRE: readonly { valor: ResultadoCierre; nombre: string }[] = [
  { valor: 'VENTA_CONCRETADA', nombre: 'Se concretó la venta' },
  { valor: 'NO_INTERESADO', nombre: 'No le interesó' },
  { valor: 'NO_RESPONDIO', nombre: 'No respondió' },
  { valor: 'VEHICULO_NO_DISPONIBLE', nombre: 'El vehículo ya no está disponible' },
  { valor: 'DUPLICADO_INVALIDO', nombre: 'Contacto duplicado o inválido' },
  { valor: 'ATENDIDA', nombre: 'Solicitud atendida' },
  { valor: 'OTRO', nombre: 'Otro' },
]

/** P5b (CA-P5b-41): resultados que solo aplican a un contacto de vehículo o solo a un aviso. */
const SOLO_VEHICULO: readonly ResultadoCierre[] = ['VENTA_CONCRETADA', 'VEHICULO_NO_DISPONIBLE']
const SOLO_AVISO: readonly ResultadoCierre[] = ['ATENDIDA']

/** true si la solicitud es un aviso de P5b (sin vehículo; siempre de Administración, D-52). */
export function esAviso(origen: OrigenLead): boolean {
  return origen !== 'CONTACTO_VEHICULO'
}

/** Resultados de cierre válidos para el origen (tabla del contrato P5b). */
export function resultadosCierrePara(origen: OrigenLead): readonly { valor: ResultadoCierre; nombre: string }[] {
  const excluidos = esAviso(origen) ? SOLO_VEHICULO : SOLO_AVISO
  return RESULTADOS_CIERRE.filter((r) => !excluidos.includes(r.valor))
}

/** CA-P5b-37: etiqueta del origen en la bandeja del administrador. */
export const NOMBRES_ORIGEN: Record<OrigenLead, string> = {
  CONTACTO_VEHICULO: 'Contacto de vehículo',
  VENDE_TU_CAMION: 'Vende tu camión',
  SEGURO_TODO_RIESGO: 'Seguro todo riesgo',
}

const NOMBRES_EVENTO: Record<string, string> = {
  CREADO: 'Contacto recibido',
  ASIGNADO: 'Asignado',
  CONTACTO_REPETIDO: 'Contacto repetido',
  NOTA: 'Nota',
  REASIGNADO: 'Reasignado',
  CERRADO: 'Cerrado',
  REABIERTO: 'Reabierto',
}

const NOMBRES_ESTADO_VEHICULO: Record<string, string> = {
  ACTIVO: 'Activo',
  RESERVADO: 'Reservado',
  INACTIVO: 'Inactivo',
  VENDIDO: 'Vendido',
}

const NOMBRES_ESTADO_PUBLICACION: Record<string, string> = {
  PUBLICADA: 'Publicada',
  OCULTA: 'Oculta',
  ELIMINADA: 'Eliminada',
  VENDIDA: 'Vendida',
}

const NOMBRES_FORMULARIO: Record<string, string> = {
  CONTACTO_VEHICULO: 'Contacto desde la ficha del vehículo',
  VENDE_TU_CAMION: 'Vende tu camión',
  SEGURO_TODO_RIESGO: 'Seguro todo riesgo',
  REGISTRO_COMPRADOR: 'Registro de comprador',
  INSCRIPCION_ASESOR: 'Inscripción de asesor',
}

function nombreDe(tabla: Record<string, string>, codigo: string): string {
  return tabla[codigo.toUpperCase()] ?? codigo
}

export function nombreEstadoLead(estado: string): string {
  return nombreDe(NOMBRES_ESTADO_LEAD, estado)
}
export function nombreEvento(tipo: string): string {
  return nombreDe(NOMBRES_EVENTO, tipo)
}
export function nombreEstadoVehiculo(estado: string): string {
  return nombreDe(NOMBRES_ESTADO_VEHICULO, estado)
}
export function nombreEstadoPublicacionLead(estado: string): string {
  return nombreDe(NOMBRES_ESTADO_PUBLICACION, estado)
}
export function nombreFormulario(formulario: string): string {
  return nombreDe(NOMBRES_FORMULARIO, formulario)
}
export function nombreOrigen(origen: string): string {
  return nombreDe(NOMBRES_ORIGEN, origen)
}
export function nombreResultado(resultado: string): string {
  return RESULTADOS_CIERRE.find((r) => r.valor === resultado.toUpperCase())?.nombre ?? resultado
}
export function nombreResponsable(r: ResponsableLead | null): string {
  if (!r || r.tipo === 'ADMINISTRACION') return 'Administración'
  return r.nombre || (r.asesorId ? `Asesor ${r.asesorId}` : 'Asesor')
}

/** "Contactos", "Contactos (1 nuevo)", "Contactos (N nuevos)" (D-48). */
export function textoMenuContactos(nuevos: number | null): string {
  if (nuevos === null || nuevos <= 0) return 'Contactos'
  return nuevos === 1 ? 'Contactos (1 nuevo)' : `Contactos (${nuevos} nuevos)`
}

const FORMATO_FECHA_HORA = new Intl.DateTimeFormat('es-CO', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: 'America/Bogota',
})

/** Fecha y hora de Colombia; "—" si no hay o no es válida. */
export function formatearFechaHora(valor: string | null): string {
  if (!valor) return '—'
  const ms = Date.parse(valor)
  return Number.isNaN(ms) ? '—' : FORMATO_FECHA_HORA.format(ms)
}

// ---- Validación en cliente (mismos mensajes que la API) ----

function largoEntre(valor: string, min: number, max: number): boolean {
  const largo = valor.trim().length
  return largo >= min && largo <= max
}

export function validarNota(texto: string): string[] {
  return largoEntre(texto, TEXTO_MIN, TEXTO_MAX) ? [] : [MENSAJE_NOTA]
}

export function validarMotivoLead(motivo: string): string[] {
  return largoEntre(motivo, TEXTO_MIN, TEXTO_MAX) ? [] : [MENSAJE_MOTIVO_LEAD]
}

export function validarObservacion(resultado: string, observacion: string): string[] {
  if (resultado === 'OTRO') return largoEntre(observacion, TEXTO_MIN, TEXTO_MAX) ? [] : [MENSAJE_OBSERVACION_OTRO]
  return observacion.trim().length <= TEXTO_MAX ? [] : [MENSAJE_OBSERVACION_LARGA]
}

const PATRON_FECHA = /^\d{4}-\d{2}-\d{2}$/

function fechaValida(valor: string): boolean {
  if (!PATRON_FECHA.test(valor)) return false
  const d = new Date(`${valor}T00:00:00Z`)
  return !Number.isNaN(d.getTime()) && d.toISOString().slice(0, 10) === valor
}

export function validarFiltrosLeads(f: Pick<FiltrosLeads, 'desde' | 'hasta'>): Partial<Record<'desde' | 'hasta', string[]>> {
  const errores: Partial<Record<'desde' | 'hasta', string[]>> = {}
  if (f.desde !== '' && !fechaValida(f.desde)) errores.desde = [MENSAJE_FECHA]
  if (f.hasta !== '' && !fechaValida(f.hasta)) errores.hasta = [MENSAJE_FECHA]
  if (!errores.desde && !errores.hasta && f.desde !== '' && f.hasta !== '' && f.hasta < f.desde) {
    errores.hasta = [MENSAJE_FECHA_RANGO]
  }
  return errores
}

// ---- Lectura de las respuestas ----

function texto(valor: unknown): string {
  return typeof valor === 'string' ? valor : ''
}

function textoONulo(valor: unknown): string | null {
  return typeof valor === 'string' && valor.trim() !== '' ? valor : null
}

function numero(valor: unknown): number {
  return typeof valor === 'number' && Number.isFinite(valor) ? valor : 0
}

function idTexto(valor: unknown): string | null {
  if (typeof valor === 'number' && Number.isFinite(valor)) return String(valor)
  if (typeof valor === 'string' && valor.trim() !== '') return valor
  return null
}

function respuestaInvalida(): ApiError {
  return new ApiError(200, MENSAJE_INESPERADO)
}

function leerResponsable(valor: unknown): ResponsableLead | null {
  if (!esObjetoJson(valor)) return null
  const tipo = texto(valor.tipo).toUpperCase() === 'ASESOR' ? 'ASESOR' : 'ADMINISTRACION'
  return { tipo, asesorId: idTexto(valor.asesorId), nombre: textoONulo(valor.nombre) }
}

function leerOrigen(valor: unknown): OrigenLead {
  const codigo = texto(valor).trim().toUpperCase()
  return ORIGENES_LEAD.find((o) => o === codigo) ?? 'CONTACTO_VEHICULO'
}

function leerVehiculo(valor: unknown): VehiculoLead | null {
  if (!esObjetoJson(valor)) return null
  const v = valor
  return {
    vehiculoId: numero(v.vehiculoId),
    publicacionId: numero(v.publicacionId),
    titulo: texto(v.titulo),
    slug: texto(v.slug),
    estadoVehiculo: texto(v.estadoVehiculo),
    estadoPublicacion: texto(v.estadoPublicacion),
  }
}

function leerItem(valor: unknown): LeadItem | null {
  if (!esObjetoJson(valor) || typeof valor.id !== 'number') return null
  return {
    id: valor.id,
    estado: texto(valor.estado).toUpperCase(),
    nombre: texto(valor.nombre),
    telefono: texto(valor.telefono),
    correo: textoONulo(valor.correo),
    mensaje: texto(valor.mensaje),
    creadoEn: texto(valor.creadoEn),
    nuevo: valor.nuevo === true,
    origen: leerOrigen(valor.origen),
    tipoVehiculo: textoONulo(valor.tipoVehiculo)?.toUpperCase() ?? null,
    vehiculo: leerVehiculo(valor.vehiculo),
    responsable: leerResponsable(valor.responsable) ?? { tipo: 'ADMINISTRACION', asesorId: null, nombre: null },
    resultadoCierre: textoONulo(valor.resultadoCierre),
    cerradoEn: textoONulo(valor.cerradoEn),
  }
}

function leerEvento(valor: unknown): EventoLead | null {
  if (!esObjetoJson(valor)) return null
  const actor = esObjetoJson(valor.actor) ? valor.actor : {}
  return {
    tipo: texto(valor.tipo).toUpperCase(),
    fecha: texto(valor.fecha),
    actor: {
      tipo: texto(actor.tipo).toUpperCase() === 'USUARIO' ? 'USUARIO' : 'SISTEMA',
      nombre: textoONulo(actor.nombre),
    },
    automatico: valor.automatico === true,
    texto: textoONulo(valor.texto),
    resultado: textoONulo(valor.resultado),
    responsableAnterior: leerResponsable(valor.responsableAnterior),
    responsableNuevo: leerResponsable(valor.responsableNuevo),
  }
}

function leerDetalle(valor: unknown): LeadDetalle {
  const item = leerItem(valor)
  if (!item || !esObjetoJson(valor)) throw respuestaInvalida()
  const eventos = Array.isArray(valor.eventos)
    ? valor.eventos.map(leerEvento).filter((e): e is EventoLead => e !== null)
    : []
  const consentimientos = Array.isArray(valor.consentimientos)
    ? valor.consentimientos
        .filter(esObjetoJson)
        .map((c) => ({
          formulario: texto(c.formulario),
          versionPolitica: texto(c.versionPolitica),
          aceptadoEn: texto(c.aceptadoEn),
        }))
    : null
  return {
    ...item,
    observacionCierre: textoONulo(valor.observacionCierre),
    cerradoPor: textoONulo(valor.cerradoPor),
    eventos,
    consentimientos,
  }
}

// ---- Llamadas ----

const BASE = '/api/leads'
const MENSAJES_COMUNES = { 401: MENSAJE_SESION_TERMINADA, 404: MENSAJE_LEAD_NO_ENCONTRADO }

/** Cadena de consulta en orden fijo: solo los filtros con valor, y siempre página y tamaño. */
export function consultaLeads(f: FiltrosLeads, esAdmin: boolean): string {
  const params = new URLSearchParams()
  if (f.estado !== '') params.set('estado', f.estado)
  if (esAdmin && f.responsable !== '') params.set('responsable', f.responsable)
  if (esAdmin && f.origen !== '') params.set('origen', f.origen)
  if (f.vehiculoId !== '') params.set('vehiculoId', f.vehiculoId)
  if (f.desde !== '') params.set('desde', f.desde)
  if (f.hasta !== '') params.set('hasta', f.hasta)
  params.set('pagina', String(f.pagina))
  params.set('tamano', String(f.tamano))
  return params.toString()
}

/** GET /api/leads — del más reciente al más antiguo. */
export async function listarLeads(
  token: string,
  filtros: FiltrosLeads,
  esAdmin: boolean,
  signal?: AbortSignal,
): Promise<ListaLeads> {
  const datos = await peticion(`${BASE}?${consultaLeads(filtros, esAdmin)}`, {
    token,
    signal,
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA, 400: MENSAJE_REVISA_FILTROS },
  })
  if (!esObjetoJson(datos) || !Array.isArray(datos.items)) throw respuestaInvalida()
  return {
    items: datos.items.map(leerItem).filter((i): i is LeadItem => i !== null),
    total: numero(datos.total),
    pagina: numero(datos.pagina) || filtros.pagina,
    tamano: numero(datos.tamano) || filtros.tamano,
  }
}

/** GET /api/leads/resumen — "Contactos (N nuevos)" (D-48). */
export async function obtenerResumenLeads(token: string, signal?: AbortSignal): Promise<ResumenLeads> {
  const datos = await peticion(`${BASE}/resumen`, { token, signal, mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA } })
  if (!esObjetoJson(datos)) throw respuestaInvalida()
  return { nuevos: numero(datos.nuevos), abiertos: numero(datos.abiertos) }
}

/** GET /api/leads/{id} — marca el contacto como visto si lo abre su responsable. */
export async function obtenerLead(token: string, id: number, signal?: AbortSignal): Promise<LeadDetalle> {
  const datos = await peticion(`${BASE}/${id}`, { token, signal, mensajesPorCodigo: MENSAJES_COMUNES })
  return leerDetalle(datos)
}

async function accion(token: string, id: number, ruta: string, body: unknown): Promise<LeadDetalle> {
  const datos = await peticion(`${BASE}/${id}/${ruta}`, {
    method: 'POST',
    token,
    body,
    mensajesPorCodigo: MENSAJES_COMUNES,
  })
  return leerDetalle(datos)
}

export function agregarNota(token: string, id: number, textoNota: string): Promise<LeadDetalle> {
  return accion(token, id, 'notas', { texto: textoNota })
}

export function cerrarLead(token: string, id: number, resultado: string, observacion: string): Promise<LeadDetalle> {
  return accion(token, id, 'cerrar', { resultado, observacion: observacion === '' ? null : observacion })
}

export function reabrirLead(token: string, id: number, motivo: string): Promise<LeadDetalle> {
  return accion(token, id, 'reabrir', { motivo })
}

/** `destino` = 'ADMINISTRACION' o un perfiles_asesor.id. */
export function reasignarLead(token: string, id: number, destino: string, motivo: string): Promise<LeadDetalle> {
  const body =
    destino === RESPONSABLE_ADMINISTRACION
      ? { administracion: true, motivo }
      : { asesorId: Number(destino), motivo }
  return accion(token, id, 'reasignar', body)
}

/** Errores del servidor para un campo concreto (sin importar mayúsculas). */
export function erroresDeCampo(error: unknown, campo: string): string[] {
  if (!(error instanceof ApiError)) return []
  return Object.entries(error.errores)
    .filter(([c]) => c.toLowerCase() === campo.toLowerCase())
    .flatMap(([, m]) => m)
}
