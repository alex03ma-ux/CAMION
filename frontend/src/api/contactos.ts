// P5 — Contacto desde la ficha del vehículo (HU-029, HU-030; contrato docs/criterios/P5-contrato-api.md).
// POST /api/contactos es anónimo; si hay sesión se envía el token para ligar el lead a la cuenta (CA-P5-02).
// La API vuelve a validar todo: la validación de aquí solo evita viajes inútiles y usa los mismos mensajes.
import { ApiError, esObjetoJson, peticion, type ErroresPorCampo } from './http'

export type ContactoRequest = {
  slug: string
  nombre: string
  telefono: string
  correo: string | null
  mensaje: string | null
  autorizacionDatos: boolean
  /** Campo trampa (CA-P5-22): una persona nunca lo ve ni lo llena. */
  sitioWeb: string
}

export type CampoContacto = 'nombre' | 'telefono' | 'correo' | 'mensaje' | 'autorizacionDatos'
/** Orden en pantalla: el foco va al primero con error (CA-P5-55). */
export const CAMPOS_CONTACTO: readonly CampoContacto[] = ['nombre', 'telefono', 'correo', 'mensaje', 'autorizacionDatos']
export type ErroresContacto = Partial<Record<CampoContacto, string[]>>

export const NOMBRE_MIN = 2
export const NOMBRE_MAX = 150
export const TELEFONO_MAX = 25
export const CORREO_MAX = 254
export const MENSAJE_MAX = 500

// ---- Mensajes del contrato ----

export const MENSAJE_CONTACTO_OK =
  '¡Listo! Recibimos tus datos. Te contactaremos pronto por teléfono o WhatsApp.'
export const MENSAJE_REVISA_DATOS = 'Revisa los datos marcados.'
export const MENSAJE_FORMATO = 'La solicitud no tiene un formato válido.'
export const MENSAJE_VEHICULO_NO_DISPONIBLE =
  'Este vehículo ya no está disponible. Mira otros vehículos en el catálogo.'
export const MENSAJE_VEHICULO_PROPIO = 'Este vehículo es tuyo; no puedes contactarte a ti mismo.'
export const MENSAJE_DEMASIADO_GRANDE = 'La solicitud es demasiado grande.'
export const MENSAJE_MUCHAS_SOLICITUDES = 'Has enviado muchas solicitudes. Intenta de nuevo en unos minutos.'
/** CA-P5-56: 5xx o sin conexión. Nunca se muestran detalles técnicos. */
export const MENSAJE_CONTACTO_FALLO =
  'No pudimos enviar tus datos. Intenta de nuevo en unos minutos o escríbenos por WhatsApp al 316 615 2907.'

export const MENSAJES_CONTACTO: Record<CampoContacto, string> = {
  nombre: 'Escribe tu nombre (entre 2 y 150 caracteres).',
  telefono: 'Escribe un celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567.',
  correo: 'Escribe un correo válido o deja el campo vacío.',
  mensaje: 'El mensaje no puede pasar de 500 caracteres.',
  autorizacionDatos: 'Debes autorizar el tratamiento de tus datos para enviar el formulario.',
}

/** D-42: WhatsApp del portal, usado si el detalle no trae uno válido. */
export const WHATSAPP_PORTAL = 'https://wa.me/573166152907'

// ---- Validación (mismas reglas que la API) ----

const PATRON_CARACTERES_TELEFONO = /^[0-9 +\-()]+$/
const PATRON_CORREO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/**
 * Teléfono colombiano normalizado (`+57XXXXXXXXXX`) o null si no es válido (CA-P5-04, CA-P5-05):
 * 10 dígitos que empiezan por 3 (celular) o por 60 (fijo), con o sin el prefijo 57 / +57.
 */
export function normalizarTelefono(valor: string): string | null {
  const texto = valor.trim()
  if (texto === '' || texto.length > TELEFONO_MAX || !PATRON_CARACTERES_TELEFONO.test(texto)) return null
  let digitos = texto.replace(/[^0-9]/g, '')
  if (digitos.length === 12 && digitos.startsWith('57')) digitos = digitos.slice(2)
  if (digitos.length !== 10) return null
  if (!digitos.startsWith('3') && !digitos.startsWith('60')) return null
  return `+57${digitos}`
}

export type FormularioContacto = {
  nombre: string
  telefono: string
  correo: string
  mensaje: string
  autorizacionDatos: boolean
}

export function validarContacto(f: FormularioContacto): ErroresContacto {
  const errores: ErroresContacto = {}
  const nombre = f.nombre.trim().length
  if (nombre < NOMBRE_MIN || nombre > NOMBRE_MAX) errores.nombre = [MENSAJES_CONTACTO.nombre]
  if (normalizarTelefono(f.telefono) === null) errores.telefono = [MENSAJES_CONTACTO.telefono]
  const correo = f.correo.trim()
  if (correo !== '' && (correo.length > CORREO_MAX || !PATRON_CORREO.test(correo))) {
    errores.correo = [MENSAJES_CONTACTO.correo]
  }
  if (f.mensaje.length > MENSAJE_MAX) errores.mensaje = [MENSAJES_CONTACTO.mensaje]
  if (!f.autorizacionDatos) errores.autorizacionDatos = [MENSAJES_CONTACTO.autorizacionDatos]
  return errores
}

/** Reparte los errores del servidor entre los campos conocidos (sin importar mayúsculas). */
export function erroresDeServidor(errores: ErroresPorCampo): { porCampo: ErroresContacto; otros: string[] } {
  const porCampo: ErroresContacto = {}
  const otros: string[] = []
  for (const [clave, mensajes] of Object.entries(errores)) {
    const campo = CAMPOS_CONTACTO.find((c) => c.toLowerCase() === clave.toLowerCase())
    if (campo) porCampo[campo] = [...(porCampo[campo] ?? []), ...mensajes]
    else otros.push(...mensajes)
  }
  return { porCampo, otros }
}

/** Cuerpo de la petición: textos recortados; correo y mensaje vacíos van como null. */
export function datosDeContacto(slug: string, f: FormularioContacto, sitioWeb: string): ContactoRequest {
  const correo = f.correo.trim()
  const mensaje = f.mensaje.trim()
  return {
    slug,
    nombre: f.nombre.trim(),
    telefono: f.telefono.trim(),
    correo: correo === '' ? null : correo,
    mensaje: mensaje === '' ? null : mensaje,
    autorizacionDatos: f.autorizacionDatos,
    sitioWeb,
  }
}

/**
 * POST /api/contactos. Devuelve el mensaje de éxito (siempre el mismo, CA-P5-54).
 * Lanza ApiError con el mensaje del contrato para cada código; 5xx y sin conexión, el de CA-P5-56.
 */
export async function enviarContacto(datos: ContactoRequest, token?: string): Promise<string> {
  try {
    const respuesta = await peticion('/api/contactos', {
      method: 'POST',
      body: datos,
      token,
      mensajesPorCodigo: {
        400: MENSAJE_REVISA_DATOS,
        404: MENSAJE_VEHICULO_NO_DISPONIBLE,
        409: MENSAJE_VEHICULO_PROPIO,
        413: MENSAJE_DEMASIADO_GRANDE,
        429: MENSAJE_MUCHAS_SOLICITUDES,
      },
    })
    return esObjetoJson(respuesta) && typeof respuesta.mensaje === 'string' && respuesta.mensaje.trim() !== ''
      ? respuesta.mensaje
      : MENSAJE_CONTACTO_OK
  } catch (error) {
    if (error instanceof ApiError && (error.status === 0 || error.status >= 500)) {
      throw new ApiError(error.status, MENSAJE_CONTACTO_FALLO)
    }
    throw error
  }
}

/**
 * Enlace "Escribir por WhatsApp" (D-47, CA-P5-61, 62): el `whatsapp` del detalle (o el del portal si no
 * llega uno válido) con un texto inicial que nombra el vehículo.
 */
export function enlaceWhatsapp(whatsapp: string | null, titulo: string): string {
  const base = whatsapp !== null && /^https:\/\/wa\.me\/\d{8,15}$/.test(whatsapp) ? whatsapp : WHATSAPP_PORTAL
  const texto = `Hola, me interesa el vehículo ${titulo} que vi en Financiamos Tu Camión.`
  return `${base}?text=${encodeURIComponent(texto)}`
}
