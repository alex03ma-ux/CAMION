// D-42: foto de portada del portal. El público la consulta; el administrador la sube o la quita.
import { MENSAJE_SESION_TERMINADA } from './auth'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion } from './http'

export const MENSAJE_PORTADA_FORMATO = 'Sube una imagen JPG, PNG o WebP de hasta 5 MB.'
export const PORTADA_TAMANO_MAXIMO = 5 * 1024 * 1024
export const PORTADA_TIPOS = ['image/jpeg', 'image/png', 'image/webp'] as const

const MENSAJES = {
  400: MENSAJE_PORTADA_FORMATO,
  401: MENSAJE_SESION_TERMINADA,
  403: 'No tienes permiso para realizar esta acción.',
  413: MENSAJE_PORTADA_FORMATO,
  415: MENSAJE_PORTADA_FORMATO,
}

function leerUrl(datos: unknown): string | null {
  if (!esObjetoJson(datos)) throw new ApiError(0, MENSAJE_INESPERADO)
  return typeof datos.url === 'string' && datos.url.startsWith('/media/') ? datos.url : null
}

/** GET /api/catalogo/portada (sin token). Devuelve la URL de la foto o null si se usa la ilustración. */
export async function obtenerPortada(signal?: AbortSignal): Promise<string | null> {
  return leerUrl(await peticion('/api/catalogo/portada', { signal }))
}

/** Validación en el navegador antes de subir (la API vuelve a validar el contenido). */
export function validarArchivoPortada(archivo: File): string | null {
  if (archivo.size === 0 || archivo.size > PORTADA_TAMANO_MAXIMO) return MENSAJE_PORTADA_FORMATO
  if (archivo.type !== '' && !(PORTADA_TIPOS as readonly string[]).includes(archivo.type)) return MENSAJE_PORTADA_FORMATO
  return null
}

/** POST /api/admin/portal/portada (multipart, campo "archivo"). Devuelve la URL nueva. */
export async function subirPortada(token: string, archivo: File): Promise<string | null> {
  const formulario = new FormData()
  formulario.append('archivo', archivo)
  return leerUrl(
    await peticion('/api/admin/portal/portada', { method: 'POST', token, formulario, mensajesPorCodigo: MENSAJES }),
  )
}

/** POST /api/admin/portal/portada/quitar: el portal vuelve a la ilustración. */
export async function quitarPortada(token: string): Promise<void> {
  await peticion('/api/admin/portal/portada/quitar', { method: 'POST', token, mensajesPorCodigo: MENSAJES })
}
