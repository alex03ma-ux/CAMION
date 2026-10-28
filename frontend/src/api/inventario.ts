// Inventario de vehículos (contrato docs/criterios/P4a-contrato-api.md).
// HU-013 a HU-022, RN-VE-001 a RN-VE-010, RN-CO-009, RN-AD-001, D-2, D-11, D-19, D-20.
// Todas las rutas exigen la política AsesorOAdministrador: se envía siempre el token
// Bearer (nunca en la URL ni en la consola).
import { MENSAJE_SESION_TERMINADA } from './auth'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion } from './http'

// ---- Tipos del contrato ----

export type EstadoVehiculo = 'ACTIVO' | 'RESERVADO' | 'INACTIVO' | 'VENDIDO'

export const ESTADOS_VEHICULO: readonly EstadoVehiculo[] = [
  'ACTIVO',
  'RESERVADO',
  'INACTIVO',
  'VENDIDO',
]

export type AccionEstado = 'reservar' | 'liberar' | 'inactivar' | 'reactivar'

export type Opcion = { id: number; nombre: string }

export type ResumenAsesor = { id: string; nombre: string }

export type ItemInventario = {
  id: number
  tipo: string
  marca: string | null
  linea: string | null
  modeloAnio: number | null
  placa: string | null
  precioCop: number
  estado: string
  kilometraje: number | null
  horasUso: number | null
  municipio: string | null
  fotoPortada: string | null
  totalFotos: number
  actualizadoEn: string
  /** D-28: nota de la reserva (solo inventario, nunca el portal). */
  notaReserva: string | null
  /** Solo lo envía la API al administrador. */
  asesor: ResumenAsesor | null
}

export type ListaInventario = {
  items: ItemInventario[]
  total: number
  pagina: number
  tamano: number
}

export type FiltrosInventario = {
  /** Vacío = todos. */
  estado: string
  tipoId: string
  q: string
  /** Solo para el administrador. */
  asesorId: string
  pagina: number
  tamano: number
}

export type FotoInventario = {
  id: number
  url: string
  orden: number
  esPortada: boolean
}

export type DetalleInventario = {
  id: number
  tipoId: number | null
  tipo: string
  marcaId: number | null
  lineaId: number | null
  modeloAnio: number | null
  kilometraje: number | null
  horasUso: number | null
  placa: string | null
  carroceriaId: number | null
  combustibleId: number | null
  cilindrajeCc: number | null
  pesoBrutoKg: number | null
  capacidadCargaKg: number | null
  largoM: number | null
  anchoM: number | null
  altoM: number | null
  materialId: number | null
  tipoMaquinaId: number | null
  municipioId: number | null
  /** Puede no venir: se deduce del municipio con los catálogos. */
  departamentoId: number | null
  precioCop: number
  precioAnteriorCop: number | null
  descripcionTecnica: string | null
  estado: string
  motivoInactivacion: string | null
  notaReserva: string | null
  fotos: FotoInventario[]
  asesor: ResumenAsesor | null
}

/** Cuerpo de POST (con `asesorId` si es el administrador) y de PUT (sin `asesorId`). */
export type VehiculoRequest = {
  tipoId: number
  marcaId?: number | null
  lineaId?: number | null
  modeloAnio?: number | null
  kilometraje?: number | null
  horasUso?: number | null
  placa?: string | null
  carroceriaId?: number | null
  combustibleId?: number | null
  cilindrajeCc?: number | null
  pesoBrutoKg?: number | null
  capacidadCargaKg?: number | null
  largoM?: number | null
  anchoM?: number | null
  altoM?: number | null
  materialId?: number | null
  tipoMaquinaId?: number | null
  municipioId: number
  precioCop: number
  descripcionTecnica?: string | null
  /** `perfiles_asesor.id` (numérico en la API). */
  asesorId?: number | string
}

export type MarcaInventario = Opcion & { lineas: Opcion[] }
export type DepartamentoInventario = Opcion & { municipios: Opcion[] }

export type CatalogosInventario = {
  tipos: Opcion[]
  marcas: MarcaInventario[]
  carrocerias: Opcion[]
  materiales: Opcion[]
  tiposMaquina: Opcion[]
  combustibles: Opcion[]
  departamentos: DepartamentoInventario[]
  /** Solo para el administrador (asesores ACTIVA). */
  asesores: ResumenAsesor[]
}

// ---- Límites y mensajes del contrato ----

export const TAMANO_PAGINA_INVENTARIO = 20
export const Q_MAX_INVENTARIO = 100
export const MAX_FOTOS = 10
export const MAX_BYTES_FOTO = 5 * 1024 * 1024
export const TIPOS_FOTO: readonly string[] = ['image/jpeg', 'image/png', 'image/webp']
export const ACEPTA_FOTOS = TIPOS_FOTO.join(',')

export const MENSAJE_VEHICULO_NO_ENCONTRADO = 'Vehículo no encontrado.'
export const MENSAJE_FOTO_FORMATO = 'Sube una imagen JPG, PNG o WebP de hasta 5 MB.'
export const MENSAJE_FOTOS_LLENAS = 'Ya hay 10 fotos; elimina alguna para subir otra.'
export const MENSAJE_FOTO_NO_ENCONTRADA = 'Foto no encontrada.'
export const MENSAJE_PLACA_REPETIDA = 'Esta placa ya está registrada en otro vehículo.'
/** D-30: mensajes de las fotos al registrar y al editar. */
export const MENSAJE_SIN_FOTOS = 'Agrega al menos una foto del vehículo.'
export const MENSAJE_MAX_FOTOS = 'Puedes subir hasta 10 fotos.'
export const MENSAJE_FOTOS_PESADAS =
  'Las fotos pesan demasiado en total. Quita alguna o usa imágenes más livianas e intenta de nuevo.'
export const MENSAJE_ULTIMA_FOTO =
  'El vehículo debe tener al menos una foto. Sube otra antes de eliminar esta.'
export const MENSAJE_EDITAR_SIN_FOTOS =
  'Este vehículo no tiene fotos. Agrega al menos una para poder guardar cambios.'

const MENSAJES_INVENTARIO: Partial<Record<number, string>> = {
  401: MENSAJE_SESION_TERMINADA,
  404: MENSAJE_VEHICULO_NO_ENCONTRADO,
}

// ---- Validadores de forma de las respuestas ----

function texto(valor: unknown): string {
  return typeof valor === 'string' ? valor : ''
}

function textoONulo(valor: unknown): string | null {
  return typeof valor === 'string' && valor.trim() !== '' ? valor : null
}

function numeroONulo(valor: unknown): number | null {
  if (typeof valor === 'number' && Number.isFinite(valor)) return valor
  // DECIMAL de .NET puede llegar como texto si el serializador lo configura así.
  if (typeof valor === 'string' && valor.trim() !== '' && Number.isFinite(Number(valor))) {
    return Number(valor)
  }
  return null
}

function numero(valor: unknown, porDefecto: number): number {
  return numeroONulo(valor) ?? porDefecto
}

/** Acepta `"Camión"` o `{ id, nombre }` (el contrato no fija la forma de los nombres). */
function nombreDe(valor: unknown): string | null {
  if (typeof valor === 'string') return textoONulo(valor)
  if (esObjetoJson(valor)) return textoONulo(valor.nombre)
  return null
}

function idDe(valor: unknown): number | null {
  return esObjetoJson(valor) ? numeroONulo(valor.id) : null
}

function leerOpcion(valor: unknown): Opcion | null {
  if (!esObjetoJson(valor)) return null
  const id = numeroONulo(valor.id)
  const nombre = texto(valor.nombre)
  if (id === null || nombre === '') return null
  return { id, nombre }
}

function listaOpciones(valor: unknown): Opcion[] {
  return Array.isArray(valor)
    ? valor.map(leerOpcion).filter((o): o is Opcion => o !== null)
    : []
}

function leerAsesor(valor: unknown): ResumenAsesor | null {
  if (!esObjetoJson(valor)) return null
  const id = valor.id
  const idTexto = typeof id === 'string' ? id : typeof id === 'number' ? String(id) : ''
  if (idTexto === '') return null
  return { id: idTexto, nombre: texto(valor.nombre) }
}

function leerFoto(valor: unknown): FotoInventario | null {
  if (!esObjetoJson(valor)) return null
  const id = numeroONulo(valor.id)
  const url = texto(valor.url)
  if (id === null || url === '') return null
  return {
    id,
    url,
    orden: numero(valor.orden, 0),
    esPortada: valor.esPortada === true,
  }
}

/** Fotos ordenadas por `orden`. */
export function leerFotos(valor: unknown): FotoInventario[] {
  const fotos = Array.isArray(valor)
    ? valor.map(leerFoto).filter((f): f is FotoInventario => f !== null)
    : []
  return fotos.sort((a, b) => a.orden - b.orden)
}

function leerItem(valor: unknown): ItemInventario | null {
  if (!esObjetoJson(valor)) return null
  const id = numeroONulo(valor.id)
  if (id === null) return null
  return {
    id,
    tipo: nombreDe(valor.tipo) ?? '',
    marca: nombreDe(valor.marca),
    linea: nombreDe(valor.linea),
    modeloAnio: numeroONulo(valor.modeloAnio),
    placa: textoONulo(valor.placa),
    precioCop: numero(valor.precioCop, 0),
    estado: texto(valor.estado).toUpperCase(),
    kilometraje: numeroONulo(valor.kilometraje),
    horasUso: numeroONulo(valor.horasUso),
    municipio: nombreDe(valor.municipio),
    fotoPortada: textoONulo(valor.fotoPortada),
    totalFotos: numero(valor.totalFotos, 0),
    actualizadoEn: texto(valor.actualizadoEn),
    notaReserva: textoONulo(valor.notaReserva),
    asesor: leerAsesor(valor.asesor),
  }
}

export function leerDetalle(valor: unknown): DetalleInventario | null {
  if (!esObjetoJson(valor)) return null
  const id = numeroONulo(valor.id)
  if (id === null) return null
  return {
    id,
    tipoId: numeroONulo(valor.tipoId) ?? idDe(valor.tipo),
    tipo: nombreDe(valor.tipo) ?? '',
    marcaId: numeroONulo(valor.marcaId) ?? idDe(valor.marca),
    lineaId: numeroONulo(valor.lineaId) ?? idDe(valor.linea),
    modeloAnio: numeroONulo(valor.modeloAnio),
    kilometraje: numeroONulo(valor.kilometraje),
    horasUso: numeroONulo(valor.horasUso),
    placa: textoONulo(valor.placa),
    carroceriaId: numeroONulo(valor.carroceriaId),
    combustibleId: numeroONulo(valor.combustibleId),
    cilindrajeCc: numeroONulo(valor.cilindrajeCc),
    pesoBrutoKg: numeroONulo(valor.pesoBrutoKg),
    capacidadCargaKg: numeroONulo(valor.capacidadCargaKg),
    largoM: numeroONulo(valor.largoM),
    anchoM: numeroONulo(valor.anchoM),
    altoM: numeroONulo(valor.altoM),
    materialId: numeroONulo(valor.materialId),
    tipoMaquinaId: numeroONulo(valor.tipoMaquinaId),
    municipioId: numeroONulo(valor.municipioId) ?? idDe(valor.municipio),
    departamentoId: numeroONulo(valor.departamentoId) ?? idDe(valor.departamento),
    precioCop: numero(valor.precioCop, 0),
    precioAnteriorCop: numeroONulo(valor.precioAnteriorCop),
    descripcionTecnica: textoONulo(valor.descripcionTecnica),
    estado: texto(valor.estado).toUpperCase(),
    motivoInactivacion: textoONulo(valor.motivoInactivacion),
    notaReserva: textoONulo(valor.notaReserva),
    fotos: leerFotos(valor.fotos),
    asesor: leerAsesor(valor.asesor),
  }
}

function respuestaInvalida(): ApiError {
  return new ApiError(200, MENSAJE_INESPERADO)
}

function detalleOError(respuesta: unknown): DetalleInventario {
  const detalle = leerDetalle(respuesta)
  if (!detalle) throw respuestaInvalida()
  return detalle
}

// ---- Endpoints ----

const BASE = '/api/inventario'

function rutaVehiculo(id: number): string {
  return `${BASE}/vehiculos/${encodeURIComponent(String(id))}`
}

/** Cadena de consulta de la lista (solo los filtros con valor). */
export function consultaInventario(filtros: FiltrosInventario, esAdmin: boolean): string {
  const parametros = new URLSearchParams()
  if (filtros.estado) parametros.set('estado', filtros.estado)
  if (filtros.tipoId) parametros.set('tipoId', filtros.tipoId)
  const q = filtros.q.trim()
  if (q) parametros.set('q', q.slice(0, Q_MAX_INVENTARIO))
  if (esAdmin && filtros.asesorId) parametros.set('asesorId', filtros.asesorId)
  parametros.set('pagina', String(filtros.pagina))
  parametros.set('tamano', String(filtros.tamano))
  return parametros.toString()
}

export async function listarInventario(
  token: string,
  filtros: FiltrosInventario,
  esAdmin: boolean,
  signal?: AbortSignal,
): Promise<ListaInventario> {
  const respuesta = await peticion(
    `${BASE}/vehiculos?${consultaInventario(filtros, esAdmin)}`,
    { token, signal, mensajesPorCodigo: MENSAJES_INVENTARIO },
  )
  if (!esObjetoJson(respuesta) || !Array.isArray(respuesta.items)) throw respuestaInvalida()
  const items = respuesta.items
    .map(leerItem)
    .filter((i): i is ItemInventario => i !== null)
  return {
    items,
    total: numero(respuesta.total, items.length),
    pagina: numero(respuesta.pagina, filtros.pagina),
    tamano: numero(respuesta.tamano, filtros.tamano),
  }
}

export async function obtenerVehiculoInventario(
  token: string,
  id: number,
  signal?: AbortSignal,
): Promise<DetalleInventario> {
  const respuesta = await peticion(rutaVehiculo(id), {
    token,
    signal,
    mensajesPorCodigo: MENSAJES_INVENTARIO,
  })
  return detalleOError(respuesta)
}

/**
 * D-30: el vehículo se crea junto con sus fotos (1 a 10) en un solo
 * `multipart/form-data`: `datos` lleva el JSON de `VehiculoRequest` como texto
 * y `fotos`, los archivos en orden (la primera es la portada).
 */
export async function crearVehiculo(
  token: string,
  datos: VehiculoRequest,
  fotos: readonly File[],
): Promise<DetalleInventario> {
  const formulario = new FormData()
  formulario.append('datos', JSON.stringify(datos))
  for (const foto of fotos) formulario.append('fotos', foto, foto.name)
  const respuesta = await peticion(`${BASE}/vehiculos`, {
    method: 'POST',
    token,
    formulario,
    mensajesPorCodigo: { ...MENSAJES_INVENTARIO, 413: MENSAJE_FOTOS_PESADAS },
  })
  return detalleOError(respuesta)
}

export async function editarVehiculo(
  token: string,
  id: number,
  datos: VehiculoRequest,
): Promise<DetalleInventario> {
  // El PUT no lleva asesorId (contrato).
  const { asesorId: _omitido, ...cuerpo } = datos
  void _omitido
  const respuesta = await peticion(rutaVehiculo(id), {
    method: 'PUT',
    token,
    body: cuerpo,
    mensajesPorCodigo: MENSAJES_INVENTARIO,
  })
  return detalleOError(respuesta)
}

/** Reservar, liberar, inactivar (con motivo) o reactivar. Devuelve el detalle actualizado. */
export async function cambiarEstadoVehiculo(
  token: string,
  id: number,
  accion: AccionEstado,
  motivo?: string,
): Promise<DetalleInventario | null> {
  const limpio = (motivo ?? '').trim()
  const respuesta = await peticion(`${rutaVehiculo(id)}/${accion}`, {
    method: 'POST',
    token,
    body: { motivo: limpio === '' ? null : limpio },
    mensajesPorCodigo: MENSAJES_INVENTARIO,
  })
  return leerDetalle(respuesta)
}

export async function obtenerCatalogosInventario(
  token: string,
  signal?: AbortSignal,
): Promise<CatalogosInventario> {
  const respuesta = await peticion(`${BASE}/catalogos`, {
    token,
    signal,
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA },
  })
  if (!esObjetoJson(respuesta)) throw respuestaInvalida()
  const marcas: MarcaInventario[] = Array.isArray(respuesta.marcas)
    ? respuesta.marcas.flatMap((m: unknown) => {
        const opcion = leerOpcion(m)
        if (!opcion || !esObjetoJson(m)) return []
        return [{ ...opcion, lineas: listaOpciones(m.lineas) }]
      })
    : []
  const departamentos: DepartamentoInventario[] = Array.isArray(respuesta.departamentos)
    ? respuesta.departamentos.flatMap((d: unknown) => {
        const opcion = leerOpcion(d)
        if (!opcion || !esObjetoJson(d)) return []
        return [{ ...opcion, municipios: listaOpciones(d.municipios) }]
      })
    : []
  const asesores = Array.isArray(respuesta.asesores)
    ? respuesta.asesores
        .map(leerAsesor)
        .filter((a): a is ResumenAsesor => a !== null)
    : []
  return {
    tipos: listaOpciones(respuesta.tipos),
    marcas,
    carrocerias: listaOpciones(respuesta.carrocerias),
    materiales: listaOpciones(respuesta.materiales),
    tiposMaquina: listaOpciones(respuesta.tiposMaquina),
    combustibles: listaOpciones(respuesta.combustibles),
    departamentos,
    asesores,
  }
}

// ---- Fotos ----

/** Validación previa al envío (la API vuelve a validar por la firma de los bytes). */
export function validarArchivoFoto(archivo: { type: string; size: number }): string | null {
  if (!TIPOS_FOTO.includes(archivo.type.toLowerCase())) return MENSAJE_FOTO_FORMATO
  if (archivo.size <= 0 || archivo.size > MAX_BYTES_FOTO) return MENSAJE_FOTO_FORMATO
  return null
}

export async function subirFoto(
  token: string,
  vehiculoId: number,
  archivo: File,
): Promise<FotoInventario> {
  const formulario = new FormData()
  formulario.append('archivo', archivo)
  const respuesta = await peticion(`${rutaVehiculo(vehiculoId)}/fotos`, {
    method: 'POST',
    token,
    formulario,
    mensajesPorCodigo: {
      ...MENSAJES_INVENTARIO,
      400: MENSAJE_FOTO_FORMATO,
      409: MENSAJE_FOTOS_LLENAS,
      413: MENSAJE_FOTO_FORMATO,
      415: MENSAJE_FOTO_FORMATO,
    },
  })
  const foto = leerFoto(respuesta)
  if (!foto) throw respuestaInvalida()
  return foto
}

export async function eliminarFoto(
  token: string,
  vehiculoId: number,
  fotoId: number,
): Promise<void> {
  await peticion(`${rutaVehiculo(vehiculoId)}/fotos/${encodeURIComponent(String(fotoId))}`, {
    method: 'DELETE',
    token,
    mensajesPorCodigo: { ...MENSAJES_INVENTARIO, 404: MENSAJE_FOTO_NO_ENCONTRADA },
  })
}

/** Nuevo orden de todas las fotos; la primera pasa a ser la portada. */
export async function reordenarFotos(
  token: string,
  vehiculoId: number,
  fotoIds: readonly number[],
): Promise<FotoInventario[]> {
  const respuesta = await peticion(`${rutaVehiculo(vehiculoId)}/fotos/orden`, {
    method: 'PUT',
    token,
    body: { fotoIds },
    mensajesPorCodigo: MENSAJES_INVENTARIO,
  })
  // El contrato devuelve la lista; se acepta también { fotos: [...] }.
  const lista = esObjetoJson(respuesta) ? respuesta.fotos : respuesta
  if (!Array.isArray(lista)) throw respuestaInvalida()
  return leerFotos(lista)
}
