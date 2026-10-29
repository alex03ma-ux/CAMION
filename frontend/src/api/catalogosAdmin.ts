// Catálogos administrables (contrato docs/criterios/P4p-contrato-api.md; D-32, D-37).
// HU-082 marcas (RN-PA-002), HU-083 modelos (RN-PA-003), HU-084 tipos de carrocería (RN-PA-004).
// Todas las rutas exigen el rol ADMINISTRADOR en la API. No se borra nada: solo se desactiva.
import { MENSAJE_SESION_TERMINADA } from './auth'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion } from './http'

// ---- Tipos del contrato ----

export type CatalogoAdmin = 'marcas' | 'carrocerias' | 'materiales' | 'tipos-maquina' | 'combustibles'

export const CATALOGOS_ADMIN: readonly { valor: CatalogoAdmin; nombre: string; singular: string }[] = [
  { valor: 'marcas', nombre: 'Marcas', singular: 'marca' },
  { valor: 'carrocerias', nombre: 'Tipos de carrocería', singular: 'tipo de carrocería' },
  { valor: 'materiales', nombre: 'Materiales', singular: 'material' },
  { valor: 'tipos-maquina', nombre: 'Tipos de máquina', singular: 'tipo de máquina' },
  { valor: 'combustibles', nombre: 'Combustibles', singular: 'combustible' },
]

export function datosCatalogo(catalogo: CatalogoAdmin) {
  return CATALOGOS_ADMIN.find((c) => c.valor === catalogo) ?? CATALOGOS_ADMIN[0]
}

/** Modelo (línea) de una marca, con sus categorías (D-32). */
export type LineaAdmin = {
  id: number
  nombre: string
  activo: boolean
  tipoIds: number[]
  vehiculos: number
}

export type ElementoCatalogo = {
  id: number
  nombre: string
  activo: boolean
  /** Número de vehículos que lo usan. */
  vehiculos: number
  /** Solo en marcas. */
  lineas: LineaAdmin[]
}

export type AccionEstadoCatalogo = 'activar' | 'desactivar'

/** Resultado de una escritura: el elemento (si vino) y el aviso del servidor (p. ej. vehículos afectados). */
export type ResultadoCatalogo = { aviso: string | null }

export const NOMBRE_CATALOGO_MAX = 100
export const MENSAJE_NOMBRE_CATALOGO = 'Escribe el nombre (1 a 100 caracteres).'
export const MENSAJE_CATEGORIAS = 'Elige al menos una categoría.'
export const MENSAJE_CATALOGO_NO_ENCONTRADO = 'No encontrado.'

export function validarNombreCatalogo(nombre: string): string[] {
  const largo = nombre.trim().length
  return largo >= 1 && largo <= NOMBRE_CATALOGO_MAX ? [] : [MENSAJE_NOMBRE_CATALOGO]
}

export function validarCategorias(tipoIds: readonly number[]): string[] {
  return tipoIds.length > 0 ? [] : [MENSAJE_CATEGORIAS]
}

/** Texto sin tildes ni mayúsculas, para el buscador. */
export function normalizarBusqueda(valor: string): string {
  return valor
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .trim()
}

// ---- Lectura de respuestas ----

function numero(valor: unknown, porDefecto: number): number {
  return typeof valor === 'number' && Number.isFinite(valor) ? valor : porDefecto
}

function leerLinea(valor: unknown): LineaAdmin | null {
  if (!esObjetoJson(valor) || typeof valor.id !== 'number') return null
  return {
    id: valor.id,
    nombre: typeof valor.nombre === 'string' ? valor.nombre : '',
    activo: valor.activo !== false,
    tipoIds: Array.isArray(valor.tipoIds)
      ? valor.tipoIds.filter((t): t is number => typeof t === 'number')
      : [],
    vehiculos: numero(valor.vehiculos, 0),
  }
}

function leerElemento(valor: unknown): ElementoCatalogo | null {
  if (!esObjetoJson(valor) || typeof valor.id !== 'number') return null
  return {
    id: valor.id,
    nombre: typeof valor.nombre === 'string' ? valor.nombre : '',
    activo: valor.activo !== false,
    vehiculos: numero(valor.vehiculos, 0),
    lineas: Array.isArray(valor.lineas)
      ? valor.lineas.map(leerLinea).filter((l): l is LineaAdmin => l !== null)
      : [],
  }
}

/** Aviso opcional del servidor (`aviso`, `advertencia` o `mensaje`). */
function leerAviso(valor: unknown): string | null {
  if (!esObjetoJson(valor)) return null
  for (const clave of ['aviso', 'advertencia', 'mensaje']) {
    const v = valor[clave]
    if (typeof v === 'string' && v.trim() !== '') return v
  }
  return null
}

const MENSAJES_CATALOGOS: Partial<Record<number, string>> = {
  401: MENSAJE_SESION_TERMINADA,
  404: MENSAJE_CATALOGO_NO_ENCONTRADO,
}

const BASE = '/api/admin/catalogos'

function ruta(...partes: (string | number)[]): string {
  return [BASE, ...partes.map((p) => encodeURIComponent(String(p)))].join('/')
}

// ---- Endpoints ----

export async function listarCatalogo(
  token: string,
  catalogo: CatalogoAdmin,
  signal?: AbortSignal,
): Promise<ElementoCatalogo[]> {
  const respuesta = await peticion(ruta(catalogo), {
    token,
    signal,
    mensajesPorCodigo: MENSAJES_CATALOGOS,
  })
  const lista = esObjetoJson(respuesta) ? respuesta.items : respuesta
  if (!Array.isArray(lista)) throw new ApiError(200, MENSAJE_INESPERADO)
  return lista.map(leerElemento).filter((e): e is ElementoCatalogo => e !== null)
}

export async function crearElemento(
  token: string,
  catalogo: CatalogoAdmin,
  nombre: string,
): Promise<ResultadoCatalogo> {
  const respuesta = await peticion(ruta(catalogo), {
    method: 'POST',
    token,
    body: { nombre: nombre.trim() },
    mensajesPorCodigo: MENSAJES_CATALOGOS,
  })
  return { aviso: leerAviso(respuesta) }
}

export async function renombrarElemento(
  token: string,
  catalogo: CatalogoAdmin,
  id: number,
  nombre: string,
): Promise<ResultadoCatalogo> {
  const respuesta = await peticion(ruta(catalogo, id), {
    method: 'PUT',
    token,
    body: { nombre: nombre.trim() },
    mensajesPorCodigo: MENSAJES_CATALOGOS,
  })
  return { aviso: leerAviso(respuesta) }
}

export async function cambiarEstadoElemento(
  token: string,
  catalogo: CatalogoAdmin,
  id: number,
  accion: AccionEstadoCatalogo,
): Promise<ResultadoCatalogo> {
  const respuesta = await peticion(ruta(catalogo, id, accion), {
    method: 'POST',
    token,
    mensajesPorCodigo: MENSAJES_CATALOGOS,
  })
  return { aviso: leerAviso(respuesta) }
}

export async function crearLinea(
  token: string,
  marcaId: number,
  nombre: string,
  tipoIds: readonly number[],
): Promise<ResultadoCatalogo> {
  const respuesta = await peticion(ruta('marcas', marcaId, 'lineas'), {
    method: 'POST',
    token,
    body: { nombre: nombre.trim(), tipoIds: [...tipoIds] },
    mensajesPorCodigo: MENSAJES_CATALOGOS,
  })
  return { aviso: leerAviso(respuesta) }
}

/** Renombrar o cambiar las categorías de un modelo. La respuesta puede avisar de vehículos afectados. */
export async function editarLinea(
  token: string,
  id: number,
  datos: { nombre?: string; tipoIds?: readonly number[] },
): Promise<ResultadoCatalogo> {
  const body: Record<string, unknown> = {}
  if (datos.nombre !== undefined) body.nombre = datos.nombre.trim()
  if (datos.tipoIds !== undefined) body.tipoIds = [...datos.tipoIds]
  const respuesta = await peticion(ruta('lineas', id), {
    method: 'PUT',
    token,
    body,
    mensajesPorCodigo: MENSAJES_CATALOGOS,
  })
  return { aviso: leerAviso(respuesta) }
}

export async function cambiarEstadoLinea(
  token: string,
  id: number,
  accion: AccionEstadoCatalogo,
): Promise<ResultadoCatalogo> {
  const respuesta = await peticion(ruta('lineas', id, accion), {
    method: 'POST',
    token,
    mensajesPorCodigo: MENSAJES_CATALOGOS,
  })
  return { aviso: leerAviso(respuesta) }
}
