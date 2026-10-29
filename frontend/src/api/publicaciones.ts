// Publicaciones, destacados, venta y reasignación (contrato docs/criterios/P4b-contrato-api.md).
// HU-023 crear, HU-024 modificar, HU-025 inactivar, HU-026 renovar, HU-027 destacar (D-38),
// HU-028 vencimiento, HU-064 marcar vendido (D-22, D-39), HU-065 mis publicaciones; D-34, D-40, D-41.
// Política AsesorOAdministrador (reasignar: solo el administrador). El token va siempre en la
// cabecera Bearer, nunca en la URL ni en la consola.
import { MENSAJE_SESION_TERMINADA } from './auth'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion, type ErroresPorCampo } from './http'
import { MENSAJE_VEHICULO_NO_ENCONTRADO, type ResumenAsesor } from './inventario'

// ---- Tipos del contrato ----

/** `estado_publicacion` que devuelve la API. */
export type EstadoPublicacion = 'PUBLICADA' | 'OCULTA' | 'VENDIDA' | 'ELIMINADA'
/** `estadoEfectivo`: añade VENCIDA (PUBLICADA con el plan del asesor vencido, D-41). */
export type EstadoEfectivoPublicacion = EstadoPublicacion | 'VENCIDA'

export const ESTADOS_FILTRO_PUBLICACION: readonly EstadoPublicacion[] = ['PUBLICADA', 'OCULTA', 'VENDIDA']

export type Publicacion = {
  id: number
  vehiculoId: number
  /** Título del vehículo (marca, modelo y año) que arma la API. */
  tituloVehiculo: string
  titulo: string
  descripcionComercial: string | null
  negociable: boolean
  estado: string
  estadoEfectivo: string
  destacada: boolean
  vigenteHasta: string | null
  slugPublico: string | null
  fotoPortada: string | null
  precioCop: number | null
  motivoOcultamiento: string | null
  publicadaEn: string | null
  /** Solo lo envía la API al administrador. */
  asesor: ResumenAsesor | null
}

export type ListaPublicaciones = {
  items: Publicacion[]
  total: number
  pagina: number
  tamano: number
}

export type FiltrosPublicaciones = {
  /** Vacío = todos. */
  estado: string
  q: string
  /** Solo para el administrador. */
  asesorId: string
  pagina: number
  tamano: number
}

export type CupoPublicaciones = {
  limitePublicaciones: number
  usadas: number
  limiteDestacados: number
  destacadasUsadas: number
  planVigente: boolean
  finPlan: string | null
}

/** Cuerpo de POST (con `vehiculoId`) y de PUT (sin él). */
export type DatosPublicacion = {
  titulo: string
  descripcionComercial: string | null
  negociable: boolean
}

export type FormularioPublicacion = {
  titulo: string
  descripcionComercial: string
  negociable: boolean
}

export type CampoPublicacion = 'titulo' | 'descripcionComercial' | 'negociable'
export type ErroresPublicacion = Partial<Record<CampoPublicacion, string[]>>

/** Acciones sin cuerpo sobre una publicación. */
export type AccionPublicacion = 'reactivar' | 'renovar' | 'eliminar' | 'destacar' | 'quitar-destacado'

/** D-39: origen de la venta (lista fija). */
export type OrigenVenta = 'PORTAL' | 'CONTACTO_ASESOR' | 'REFERIDO' | 'OTRO'

export const ORIGENES_VENTA: readonly OrigenVenta[] = ['PORTAL', 'CONTACTO_ASESOR', 'REFERIDO', 'OTRO']

export const NOMBRES_ORIGEN_VENTA: Readonly<Record<OrigenVenta, string>> = {
  PORTAL: 'Portal web',
  CONTACTO_ASESOR: 'Contacto directo del asesor',
  REFERIDO: 'Referido',
  OTRO: 'Otro',
}

export type DatosVenta = {
  fechaVenta: string
  precioFinalCop: number
  origen: OrigenVenta
  observaciones: string | null
}

export type FormularioVenta = {
  /** `AAAA-MM-DD` (input date). */
  fechaVenta: string
  /** Solo dígitos. */
  precioFinalCop: string
  origen: string
  observaciones: string
}

export type CampoVenta = keyof FormularioVenta
export type ErroresVenta = Partial<Record<CampoVenta, string[]>>

// ---- Límites y mensajes del contrato ----

export const TAMANO_PAGINA_PUBLICACIONES = 20
export const Q_MAX_PUBLICACIONES = 100
export const TITULO_MIN = 5
export const TITULO_MAX = 180
export const DESCRIPCION_COMERCIAL_MAX = 4000
export const OBSERVACIONES_MAX = 1000
export const MOTIVO_MIN = 5
export const MOTIVO_MAX = 500

export const MENSAJE_TITULO = 'Escribe el título de la publicación (5 a 180 caracteres).'
export const MENSAJE_DESCRIPCION_COMERCIAL = 'La descripción admite hasta 4.000 caracteres.'
export const MENSAJE_FECHA_VENTA = 'Elige la fecha de la venta (hoy o antes).'
export const MENSAJE_FECHA_FUTURA = 'La fecha de la venta no puede ser posterior a hoy.'
export const MENSAJE_PRECIO_FINAL = 'Escribe el precio final de la venta en pesos (mayor que 0).'
export const MENSAJE_ORIGEN = 'Elige el origen de la venta.'
export const MENSAJE_OBSERVACIONES_OTRO = 'Si el origen es «Otro», explica en las observaciones cómo se vendió.'
export const MENSAJE_OBSERVACIONES_LARGO = 'Las observaciones admiten hasta 1.000 caracteres.'
export const MENSAJE_ASESOR_DESTINO = 'Elige el asesor que recibirá el vehículo.'
export const MENSAJE_PUBLICACION_NO_ENCONTRADA = 'Publicación no encontrada.'

export const FORMULARIO_PUBLICACION_VACIO: FormularioPublicacion = {
  titulo: '',
  descripcionComercial: '',
  negociable: false,
}

// ---- Validación en el navegador (la API vuelve a validar) ----

export function validarPublicacion(f: FormularioPublicacion): ErroresPublicacion {
  const errores: ErroresPublicacion = {}
  const titulo = f.titulo.trim().length
  if (titulo < TITULO_MIN || titulo > TITULO_MAX) errores.titulo = [MENSAJE_TITULO]
  if (f.descripcionComercial.trim().length > DESCRIPCION_COMERCIAL_MAX) {
    errores.descripcionComercial = [MENSAJE_DESCRIPCION_COMERCIAL]
  }
  return errores
}

export function datosDePublicacion(f: FormularioPublicacion): DatosPublicacion {
  const descripcion = f.descripcionComercial.trim()
  return {
    titulo: f.titulo.trim(),
    descripcionComercial: descripcion === '' ? null : descripcion,
    negociable: f.negociable,
  }
}

export function formularioDePublicacion(p: Publicacion): FormularioPublicacion {
  return {
    titulo: p.titulo,
    descripcionComercial: p.descripcionComercial ?? '',
    negociable: p.negociable,
  }
}

/** Fecha local de hoy como `AAAA-MM-DD`. */
export function hoyIso(ahora: Date = new Date()): string {
  const a = ahora.getFullYear()
  const m = String(ahora.getMonth() + 1).padStart(2, '0')
  const d = String(ahora.getDate()).padStart(2, '0')
  return `${a}-${m}-${d}`
}

function esOrigen(valor: string): valor is OrigenVenta {
  return (ORIGENES_VENTA as readonly string[]).includes(valor)
}

function enteroPositivo(textoNumero: string): number | null {
  const limpio = textoNumero.trim()
  if (!/^\d{1,15}$/.test(limpio)) return null
  const n = Number(limpio)
  return Number.isSafeInteger(n) && n > 0 ? n : null
}

export function validarVenta(f: FormularioVenta, hoy: string = hoyIso()): ErroresVenta {
  const errores: ErroresVenta = {}
  if (!/^\d{4}-\d{2}-\d{2}$/.test(f.fechaVenta)) errores.fechaVenta = [MENSAJE_FECHA_VENTA]
  else if (f.fechaVenta > hoy) errores.fechaVenta = [MENSAJE_FECHA_FUTURA]
  if (enteroPositivo(f.precioFinalCop) === null) errores.precioFinalCop = [MENSAJE_PRECIO_FINAL]
  if (!esOrigen(f.origen)) errores.origen = [MENSAJE_ORIGEN]
  const observaciones = f.observaciones.trim().length
  if (observaciones > OBSERVACIONES_MAX) errores.observaciones = [MENSAJE_OBSERVACIONES_LARGO]
  else if (f.origen === 'OTRO' && observaciones === 0) errores.observaciones = [MENSAJE_OBSERVACIONES_OTRO]
  return errores
}

/** Formulario ya validado → cuerpo de la API. */
export function datosDeVenta(f: FormularioVenta): DatosVenta {
  const observaciones = f.observaciones.trim()
  return {
    fechaVenta: f.fechaVenta,
    precioFinalCop: enteroPositivo(f.precioFinalCop) ?? 0,
    origen: esOrigen(f.origen) ? f.origen : 'OTRO',
    observaciones: observaciones === '' ? null : observaciones,
  }
}

/** Reparte los `errores` del 400 entre los campos conocidos (sin distinguir mayúsculas). */
export function repartirErrores<C extends string>(
  errores: ErroresPorCampo,
  campos: readonly C[],
): { porCampo: Partial<Record<C, string[]>>; otros: string[] } {
  const porCampo: Partial<Record<C, string[]>> = {}
  const otros: string[] = []
  for (const [clave, mensajes] of Object.entries(errores)) {
    const campo = campos.find((c) => c.toLowerCase() === clave.toLowerCase())
    if (campo) porCampo[campo] = [...(porCampo[campo] ?? []), ...mensajes]
    else for (const m of mensajes) if (!otros.includes(m)) otros.push(m)
  }
  return { porCampo, otros }
}

export const CAMPOS_PUBLICACION: readonly CampoPublicacion[] = ['titulo', 'descripcionComercial', 'negociable']
export const CAMPOS_VENTA: readonly CampoVenta[] = ['fechaVenta', 'precioFinalCop', 'origen', 'observaciones']

// ---- Lectura de respuestas ----

function texto(valor: unknown): string {
  return typeof valor === 'string' ? valor : ''
}

function textoONulo(valor: unknown): string | null {
  return typeof valor === 'string' && valor.trim() !== '' ? valor : null
}

function numeroONulo(valor: unknown): number | null {
  if (typeof valor === 'number' && Number.isFinite(valor)) return valor
  if (typeof valor === 'string' && valor.trim() !== '' && Number.isFinite(Number(valor))) return Number(valor)
  return null
}

function leerAsesor(valor: unknown): ResumenAsesor | null {
  if (!esObjetoJson(valor)) return null
  const id = valor.id
  const idTexto = typeof id === 'string' ? id : typeof id === 'number' ? String(id) : ''
  if (idTexto === '') return null
  return { id: idTexto, nombre: texto(valor.nombre) }
}

/** El contrato no fija el nombre: `vehiculoTitulo`, `tituloVehiculo` o `vehiculo.titulo`. */
function tituloDelVehiculo(valor: Record<string, unknown>): string {
  if (typeof valor.vehiculoTitulo === 'string') return valor.vehiculoTitulo
  if (typeof valor.tituloVehiculo === 'string') return valor.tituloVehiculo
  if (esObjetoJson(valor.vehiculo) && typeof valor.vehiculo.titulo === 'string') return valor.vehiculo.titulo
  return ''
}

export function leerPublicacion(valor: unknown): Publicacion | null {
  if (!esObjetoJson(valor)) return null
  const id = numeroONulo(valor.id)
  const vehiculoId = numeroONulo(valor.vehiculoId)
  if (id === null || vehiculoId === null) return null
  const estado = texto(valor.estado).toUpperCase()
  const efectivo = texto(valor.estadoEfectivo).toUpperCase()
  return {
    id,
    vehiculoId,
    tituloVehiculo: tituloDelVehiculo(valor),
    titulo: texto(valor.titulo),
    descripcionComercial: textoONulo(valor.descripcionComercial),
    negociable: valor.negociable === true,
    estado,
    estadoEfectivo: efectivo || estado,
    destacada: valor.destacada === true,
    vigenteHasta: textoONulo(valor.vigenteHasta),
    slugPublico: textoONulo(valor.slugPublico),
    fotoPortada: textoONulo(valor.fotoPortada),
    precioCop: numeroONulo(valor.precioCop),
    motivoOcultamiento: textoONulo(valor.motivoOcultamiento),
    publicadaEn: textoONulo(valor.publicadaEn),
    asesor: leerAsesor(valor.asesor),
  }
}

export function leerCupo(valor: unknown): CupoPublicaciones | null {
  if (!esObjetoJson(valor)) return null
  return {
    limitePublicaciones: numeroONulo(valor.limitePublicaciones) ?? 0,
    usadas: numeroONulo(valor.usadas) ?? 0,
    limiteDestacados: numeroONulo(valor.limiteDestacados) ?? 0,
    destacadasUsadas: numeroONulo(valor.destacadasUsadas) ?? 0,
    planVigente: valor.planVigente === true,
    finPlan: textoONulo(valor.finPlan),
  }
}

const MENSAJES_PUBLICACIONES: Partial<Record<number, string>> = {
  401: MENSAJE_SESION_TERMINADA,
  404: MENSAJE_PUBLICACION_NO_ENCONTRADA,
}

const MENSAJES_VEHICULO: Partial<Record<number, string>> = {
  401: MENSAJE_SESION_TERMINADA,
  404: MENSAJE_VEHICULO_NO_ENCONTRADO,
}

const BASE = '/api/publicaciones'

function rutaPublicacion(id: number): string {
  return `${BASE}/${encodeURIComponent(String(id))}`
}

function rutaVehiculo(id: number): string {
  return `/api/inventario/vehiculos/${encodeURIComponent(String(id))}`
}

// ---- Endpoints ----

/** Cadena de consulta del listado (solo los filtros con valor). */
export function consultaPublicaciones(filtros: FiltrosPublicaciones, esAdmin: boolean): string {
  const parametros = new URLSearchParams()
  if (filtros.estado) parametros.set('estado', filtros.estado)
  const q = filtros.q.trim()
  if (q) parametros.set('q', q.slice(0, Q_MAX_PUBLICACIONES))
  if (esAdmin && filtros.asesorId) parametros.set('asesorId', filtros.asesorId)
  parametros.set('pagina', String(filtros.pagina))
  parametros.set('tamano', String(filtros.tamano))
  return parametros.toString()
}

export async function listarPublicaciones(
  token: string,
  filtros: FiltrosPublicaciones,
  esAdmin: boolean,
  signal?: AbortSignal,
): Promise<ListaPublicaciones> {
  const respuesta = await peticion(`${BASE}?${consultaPublicaciones(filtros, esAdmin)}`, {
    token,
    signal,
    mensajesPorCodigo: MENSAJES_PUBLICACIONES,
  })
  if (!esObjetoJson(respuesta) || !Array.isArray(respuesta.items)) throw new ApiError(200, MENSAJE_INESPERADO)
  const items = respuesta.items.map(leerPublicacion).filter((p): p is Publicacion => p !== null)
  return {
    items,
    total: numeroONulo(respuesta.total) ?? items.length,
    pagina: numeroONulo(respuesta.pagina) ?? filtros.pagina,
    tamano: numeroONulo(respuesta.tamano) ?? filtros.tamano,
  }
}

/** Tamaño de página para reunir las publicaciones del inventario (máximo del contrato P4a). */
export const TAMANO_PAGINA_MAPA = 100
const MAX_PAGINAS_MAPA = 20

/**
 * Publicaciones (no eliminadas) por id de vehículo, para mostrarlas en el inventario.
 * Recorre las páginas del listado; el contrato no trae la publicación dentro del vehículo.
 */
export async function publicacionesPorVehiculo(
  token: string,
  esAdmin: boolean,
  asesorId: string,
  signal?: AbortSignal,
): Promise<Map<number, Publicacion>> {
  const mapa = new Map<number, Publicacion>()
  for (let pagina = 1; pagina <= MAX_PAGINAS_MAPA; pagina++) {
    const lista = await listarPublicaciones(
      token,
      { estado: '', q: '', asesorId, pagina, tamano: TAMANO_PAGINA_MAPA },
      esAdmin,
      signal,
    )
    for (const p of lista.items) if (p.estado !== 'ELIMINADA') mapa.set(p.vehiculoId, p)
    if (lista.items.length === 0 || pagina * Math.max(1, lista.tamano) >= lista.total) break
  }
  return mapa
}

export async function obtenerCupo(token: string, signal?: AbortSignal): Promise<CupoPublicaciones> {
  const respuesta = await peticion(`${BASE}/cupo`, {
    token,
    signal,
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA },
  })
  const cupo = leerCupo(respuesta)
  if (!cupo) throw new ApiError(200, MENSAJE_INESPERADO)
  return cupo
}

export async function obtenerPublicacion(token: string, id: number, signal?: AbortSignal): Promise<Publicacion> {
  const respuesta = await peticion(rutaPublicacion(id), {
    token,
    signal,
    mensajesPorCodigo: MENSAJES_PUBLICACIONES,
  })
  const publicacion = leerPublicacion(respuesta)
  if (!publicacion) throw new ApiError(200, MENSAJE_INESPERADO)
  return publicacion
}

export async function crearPublicacion(
  token: string,
  vehiculoId: number,
  datos: DatosPublicacion,
): Promise<Publicacion | null> {
  const respuesta = await peticion(BASE, {
    method: 'POST',
    token,
    body: { vehiculoId, ...datos },
    mensajesPorCodigo: MENSAJES_VEHICULO,
  })
  return leerPublicacion(respuesta)
}

export async function editarPublicacion(
  token: string,
  id: number,
  datos: DatosPublicacion,
): Promise<Publicacion | null> {
  const respuesta = await peticion(rutaPublicacion(id), {
    method: 'PUT',
    token,
    body: datos,
    mensajesPorCodigo: MENSAJES_PUBLICACIONES,
  })
  return leerPublicacion(respuesta)
}

export async function inactivarPublicacion(token: string, id: number, motivo: string): Promise<Publicacion | null> {
  const respuesta = await peticion(`${rutaPublicacion(id)}/inactivar`, {
    method: 'POST',
    token,
    body: { motivo: motivo.trim() },
    mensajesPorCodigo: MENSAJES_PUBLICACIONES,
  })
  return leerPublicacion(respuesta)
}

/** Reactivar, renovar, eliminar, destacar o quitar el destacado. */
export async function accionPublicacion(
  token: string,
  id: number,
  accion: AccionPublicacion,
): Promise<Publicacion | null> {
  const respuesta = await peticion(`${rutaPublicacion(id)}/${accion}`, {
    method: 'POST',
    token,
    mensajesPorCodigo: MENSAJES_PUBLICACIONES,
  })
  return leerPublicacion(respuesta)
}

export async function venderVehiculo(token: string, vehiculoId: number, datos: DatosVenta): Promise<void> {
  await peticion(`${rutaVehiculo(vehiculoId)}/vender`, {
    method: 'POST',
    token,
    body: datos,
    mensajesPorCodigo: MENSAJES_VEHICULO,
  })
}

export async function revertirVenta(token: string, vehiculoId: number, motivo: string): Promise<void> {
  await peticion(`${rutaVehiculo(vehiculoId)}/revertir-venta`, {
    method: 'POST',
    token,
    body: { motivo: motivo.trim() },
    mensajesPorCodigo: MENSAJES_VEHICULO,
  })
}

/** D-34: solo el administrador. `asesorId` es `perfiles_asesor.id` (numérico en la API). */
export async function reasignarVehiculo(
  token: string,
  vehiculoId: number,
  asesorId: string,
  motivo: string,
): Promise<void> {
  const numerico = Number(asesorId)
  await peticion(`/api/admin/vehiculos/${encodeURIComponent(String(vehiculoId))}/reasignar`, {
    method: 'POST',
    token,
    body: { asesorId: /^\d+$/.test(asesorId) && Number.isSafeInteger(numerico) ? numerico : asesorId, motivo: motivo.trim() },
    mensajesPorCodigo: MENSAJES_VEHICULO,
  })
}
