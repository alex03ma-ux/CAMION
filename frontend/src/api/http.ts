// Cliente HTTP mínimo para la API de CAMION.
// Usa rutas relativas (/api/...) que el proxy de Vite redirige al backend.
// Nunca registra en consola el token ni los cuerpos de las peticiones.

export type ErroresPorCampo = Record<string, string[]>

/** Formato común de error del contrato P1. */
export type CuerpoError = {
  error?: string
  errores?: ErroresPorCampo
  /** P2: estado de la cuenta en los 403 del login (PENDIENTE, RECHAZADA, BLOQUEADA). */
  estado?: string
}

export const MENSAJE_SIN_CONEXION = 'No se pudo conectar con el servidor.'
export const MENSAJE_SERVIDOR_NO_DISPONIBLE =
  'No se pudo conectar con el servidor. Intenta de nuevo en unos minutos.'
export const MENSAJE_SIN_PERMISO = 'No tienes permiso para realizar esta acción.'
export const MENSAJE_INESPERADO = 'Ocurrió un error inesperado. Intenta de nuevo.'

/**
 * Error de la API. `status` 0 indica que no hubo respuesta (sin conexión).
 * `mensaje` siempre trae un texto listo para mostrar al usuario.
 */
export class ApiError extends Error {
  readonly status: number
  readonly mensaje: string
  readonly errores: ErroresPorCampo
  /** Campo opcional `estado` del cuerpo de error (contrato P2), si viene. */
  readonly estado: string | undefined
  /** P9 (CA-P9-42): segundos del encabezado `Retry-After` de un 429, si llega como número. */
  readonly reintentarEnSegundos: number | undefined

  constructor(
    status: number,
    mensaje: string,
    errores: ErroresPorCampo = {},
    estado?: string,
    reintentarEnSegundos?: number,
  ) {
    super(mensaje)
    this.name = 'ApiError'
    this.status = status
    this.mensaje = mensaje
    this.errores = errores
    this.estado = estado
    this.reintentarEnSegundos = reintentarEnSegundos
  }
}

type OpcionesPeticion = {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  /** P4a: cuerpo multipart (fotos). El navegador pone el Content-Type con su boundary. */
  formulario?: FormData
  token?: string
  signal?: AbortSignal
  /** Mensaje por código cuando la respuesta no trae `error`. */
  mensajesPorCodigo?: Partial<Record<number, string>>
}

function esObjeto(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === 'object' && valor !== null && !Array.isArray(valor)
}

function normalizarErrores(valor: unknown): ErroresPorCampo {
  if (!esObjeto(valor)) return {}
  const resultado: ErroresPorCampo = {}
  for (const [campo, mensajes] of Object.entries(valor)) {
    if (Array.isArray(mensajes)) {
      const textos = mensajes.filter(
        (m): m is string => typeof m === 'string' && m.trim() !== '',
      )
      if (textos.length > 0) resultado[campo] = textos
    } else if (typeof mensajes === 'string' && mensajes.trim() !== '') {
      resultado[campo] = [mensajes]
    }
  }
  return resultado
}

async function leerJson(respuesta: Response): Promise<unknown> {
  const texto = await respuesta.text()
  if (texto.trim() === '') return undefined
  try {
    return JSON.parse(texto) as unknown
  } catch {
    return undefined
  }
}

function mensajePorDefecto(
  status: number,
  mensajes: Partial<Record<number, string>>,
): string {
  const propio = mensajes[status]
  if (propio) return propio
  if (status === 403) return MENSAJE_SIN_PERMISO
  if (status >= 500) return MENSAJE_SERVIDOR_NO_DISPONIBLE
  return MENSAJE_INESPERADO
}

/**
 * Hace la petición y devuelve el cuerpo JSON (o `undefined` si no hay cuerpo).
 * Lanza `ApiError` en cualquier respuesta no 2xx o si no hay conexión.
 */
export async function peticion(
  ruta: string,
  opciones: OpcionesPeticion = {},
): Promise<unknown> {
  const { method = 'GET', body, formulario, token, signal, mensajesPorCodigo = {} } = opciones
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (body !== undefined && formulario === undefined) headers['Content-Type'] = 'application/json'
  if (token) headers.Authorization = `Bearer ${token}`

  let respuesta: Response
  try {
    respuesta = await fetch(ruta, {
      method,
      headers,
      body: formulario ?? (body === undefined ? undefined : JSON.stringify(body)),
      signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError(0, MENSAJE_SIN_CONEXION)
  }

  const datos = await leerJson(respuesta)

  if (!respuesta.ok) throw errorDeRespuesta(respuesta.status, datos, mensajesPorCodigo, respuesta.headers)

  return datos
}

/** ApiError a partir de una respuesta no 2xx ya leída como JSON (o `undefined`). */
function errorDeRespuesta(
  status: number,
  datos: unknown,
  mensajesPorCodigo: Partial<Record<number, string>>,
  encabezados?: Headers,
): ApiError {
  const cuerpo: CuerpoError = esObjeto(datos) ? (datos as CuerpoError) : {}
  // Un 5xx (p. ej. el proxy de Vite cuando el backend está apagado) se
  // muestra siempre como problema de conexión, sin detalles internos.
  const mensaje =
    status < 500 && typeof cuerpo.error === 'string' && cuerpo.error.trim() !== ''
      ? cuerpo.error
      : mensajePorDefecto(status, mensajesPorCodigo)
  const estado =
    typeof cuerpo.estado === 'string' && cuerpo.estado.trim() !== ''
      ? cuerpo.estado.trim().toUpperCase()
      : undefined
  const reintentar = status === 429 ? segundosDeRetryAfter(encabezados?.get('Retry-After') ?? null) : undefined
  return new ApiError(status, mensaje, normalizarErrores(cuerpo.errores), estado, reintentar)
}

/** `Retry-After` en segundos (entero positivo); la forma de fecha HTTP no se usa en la API y se ignora. */
export function segundosDeRetryAfter(valor: string | null): number | undefined {
  if (valor === null || !/^\d{1,9}$/.test(valor.trim())) return undefined
  const segundos = Number(valor.trim())
  return segundos > 0 ? segundos : undefined
}

/**
 * P9 (CA-P9-46): cuánto falta para volver a intentar, en palabras ("Podrás intentarlo de nuevo en unos 12 minutos.").
 * Vacío si el servidor no lo indicó.
 */
export function textoEspera(segundos: number | undefined): string {
  if (segundos === undefined) return ''
  if (segundos < 60) return `Podrás intentarlo de nuevo en ${segundos} ${segundos === 1 ? 'segundo' : 'segundos'}.`
  const minutos = Math.ceil(segundos / 60)
  return `Podrás intentarlo de nuevo en ${minutos === 1 ? '1 minuto' : `unos ${minutos} minutos`}.`
}

/** P8: archivo descargado con su nombre (del encabezado Content-Disposition, si viene). */
export type ArchivoDescargado = { contenido: Blob; nombre: string | null }

/** Nombre de `Content-Disposition: attachment; filename="..."`, limpio para guardarlo; null si no hay. */
export function nombreDeDisposicion(valor: string | null): string | null {
  if (!valor) return null
  const coincidencia = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(valor)
  if (!coincidencia) return null
  let nombre = coincidencia[1].trim()
  try {
    nombre = decodeURIComponent(nombre)
  } catch {
    // Se deja tal cual.
  }
  const limpio = nombre.replace(/[^A-Za-z0-9._-]/g, '_')
  return limpio === '' || limpio.startsWith('.') ? null : limpio
}

/**
 * P8 (HU-079): GET de un archivo con el token en el encabezado Authorization (nunca en la URL).
 * Los errores llegan en JSON con el mismo formato que `peticion`.
 */
export async function descargarArchivo(
  ruta: string,
  opciones: Pick<OpcionesPeticion, 'token' | 'signal' | 'mensajesPorCodigo'> = {},
): Promise<ArchivoDescargado> {
  const { token, signal, mensajesPorCodigo = {} } = opciones
  const headers: Record<string, string> = { Accept: 'text/csv, application/json' }
  if (token) headers.Authorization = `Bearer ${token}`
  let respuesta: Response
  try {
    respuesta = await fetch(ruta, { method: 'GET', headers, signal })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError(0, MENSAJE_SIN_CONEXION)
  }
  if (!respuesta.ok) throw errorDeRespuesta(respuesta.status, await leerJson(respuesta), mensajesPorCodigo, respuesta.headers)
  const contenido = await respuesta.blob()
  return { contenido, nombre: nombreDeDisposicion(respuesta.headers.get('Content-Disposition')) }
}

export function esObjetoJson(valor: unknown): valor is Record<string, unknown> {
  return esObjeto(valor)
}
