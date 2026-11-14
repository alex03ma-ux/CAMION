// P8 — Recuperación asistida de contraseña (contrato docs/criterios/P8-contrato-api.md §11; HU-063, RN-US-006;
// D-73 D, D-12). Sin correo ni WhatsApp automáticos: el administrador genera el enlace, lo copia y lo envía él.
// El enlace y el código nunca se registran en consola ni se guardan en el navegador.
import { MENSAJE_REVISA_DATOS, MENSAJE_SESION_TERMINADA } from './auth'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion } from './http'
import { correoPortalValido, normalizarCelular } from './parametros'

export type EnlaceRecuperacion = { enlace: string; venceEn: string | null; mensaje: string }

export const MENSAJE_ENLACE =
  'Envía este enlace por WhatsApp a la persona después de confirmar su identidad. Sirve una sola vez y vence en 60 minutos.'
export const MENSAJE_CODIGO_NO_VALIDO = 'El enlace no es válido o ya venció. Pide uno nuevo al administrador.'
export const MENSAJE_RESTABLECIDA = 'Listo. Tu contraseña cambió; inicia sesión con la nueva.'
export const MENSAJE_DEMASIADOS_INTENTOS = 'Demasiados intentos. Espera unos minutos e intenta de nuevo.'
export const MENSAJE_PASSWORD_NO_COINCIDE = 'Las contraseñas no coinciden.'
export const MENSAJE_CONFIRMAR_PASSWORD = 'Escribe de nuevo la contraseña.'
/** Texto prellenado del WhatsApp del inicio de sesión (CA-P8-68). */
export const TEXTO_WHATSAPP_OLVIDO = 'Hola, olvidé mi contraseña de Financiamos Tu Camión.'

/** Solo se acepta un enlace a la propia pantalla de restablecimiento (nunca otro esquema ni otra ruta). */
export function enlaceValido(enlace: string): boolean {
  return /^https?:\/\/[^\s/?#]+\/?#\/restablecer-contrasena\?codigo=[A-Za-z0-9_\-%.~]+$/.test(enlace)
}

/** Texto breve del WhatsApp que envía el administrador, con el enlace. */
export function textoWhatsappEnlace(enlace: string): string {
  return `Hola. Este es tu enlace para crear una contraseña nueva en Financiamos Tu Camión. Vence en 60 minutos y sirve una sola vez: ${enlace}`
}

/** Asunto del correo que envía el administrador desde su propio programa de correo. */
export const ASUNTO_CORREO_ENLACE = 'Recupera tu contraseña – Financiamos Tu Camión'

/**
 * Pedido de la responsable (2026-11-13): enviar el enlace también por correo sin servicio externo (D-73 D).
 * `mailto:` al correo registrado del usuario, con asunto y texto; el administrador lo envía desde su correo.
 * Solo si el correo cumple la regla estricta (B-2 de P7): así no se pueden inyectar `?`, `&` ni destinatarios.
 */
export function enlaceCorreoUsuario(correo: string, enlace: string): string | null {
  const limpio = correo.trim()
  if (!correoPortalValido(limpio) || !enlaceValido(enlace)) return null
  const asunto = encodeURIComponent(ASUNTO_CORREO_ENLACE)
  const cuerpo = encodeURIComponent(
    'Hola.\n\nEste es tu enlace para crear una contraseña nueva en Financiamos Tu Camión:\n\n' +
      `${enlace}\n\n` +
      'Vence en 60 minutos y sirve una sola vez. Si no pediste este cambio, ignora este mensaje.',
  )
  return `mailto:${limpio}?subject=${asunto}&body=${cuerpo}`
}

/** wa.me al celular del usuario si es un celular colombiano válido; null si no. */
export function enlaceWhatsappUsuario(telefono: string, enlace: string): string | null {
  const celular = normalizarCelular(telefono)
  if (celular === null) return null
  return `https://wa.me/57${celular}?text=${encodeURIComponent(textoWhatsappEnlace(enlace))}`
}

/** Lee el código del fragmento `#/restablecer-contrasena?codigo=...`; '' si no hay. */
export function codigoDelHash(hash: string): string {
  const coincidencia = /[?&]codigo=([^&]*)/.exec(hash)
  if (!coincidencia) return ''
  try {
    return decodeURIComponent(coincidencia[1]).trim()
  } catch {
    return ''
  }
}

export async function generarEnlaceRecuperacion(token: string, usuarioId: string): Promise<EnlaceRecuperacion> {
  const respuesta = await peticion(`/api/admin/usuarios/${encodeURIComponent(usuarioId)}/enlace-recuperacion`, {
    method: 'POST',
    token,
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA, 404: 'Usuario no encontrado.' },
  })
  if (!esObjetoJson(respuesta) || typeof respuesta.enlace !== 'string' || !enlaceValido(respuesta.enlace)) {
    throw new ApiError(200, MENSAJE_INESPERADO)
  }
  return {
    enlace: respuesta.enlace,
    venceEn: typeof respuesta.venceEn === 'string' && respuesta.venceEn.trim() !== '' ? respuesta.venceEn : null,
    mensaje: typeof respuesta.mensaje === 'string' && respuesta.mensaje.trim() !== '' ? respuesta.mensaje : MENSAJE_ENLACE,
  }
}

/** Anónima. 400 con `errores.password` o `errores.codigo`; 429 si hay demasiados intentos. */
export async function restablecerContrasena(codigo: string, password: string): Promise<string> {
  const respuesta = await peticion('/api/auth/restablecer-contrasena', {
    method: 'POST',
    body: { codigo, password },
    mensajesPorCodigo: { 400: MENSAJE_REVISA_DATOS, 429: MENSAJE_DEMASIADOS_INTENTOS },
  })
  return esObjetoJson(respuesta) && typeof respuesta.mensaje === 'string' && respuesta.mensaje.trim() !== ''
    ? respuesta.mensaje
    : MENSAJE_RESTABLECIDA
}
