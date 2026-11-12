// P7 — Parámetros: datos de contacto del portal (contrato docs/criterios/P7-contrato-api.md §4; HU-081, RN-PA-001;
// D-42, D-47, D-63 A). Solo WhatsApp, correo, horario (1 a 4 líneas), Instagram y TikTok: ningún valor de negocio
// es editable (CA-P7-44). La lectura pública es anónima y solo GET (CA-P7-42); la escritura exige ADMINISTRADOR.
import { MENSAJE_SESION_TERMINADA } from './auth'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion } from './http'

// ---- Tipos del contrato ----

export type ContactoPortal = {
  /** 10 dígitos, p. ej. "3166152907". */
  whatsapp: string
  whatsappEnlace: string
  correo: string
  horario: string[]
  instagram: string | null
  tiktok: string | null
}

export type ContactoPortalAdmin = ContactoPortal & {
  /** false = valores por defecto (aún no se guardó nada). */
  configurado: boolean
  actualizadoEn: string | null
  actualizadoPor: string | null
}

/** Lo que escribe el administrador. */
export type FormularioContactoPortal = {
  whatsapp: string
  correo: string
  horario: string[]
  instagram: string
  tiktok: string
}

export type CampoContactoPortal = keyof FormularioContactoPortal
export type ErroresContactoPortal = Partial<Record<CampoContactoPortal, string[]>>
export const CAMPOS_CONTACTO_PORTAL: readonly CampoContactoPortal[] = ['whatsapp', 'correo', 'horario', 'instagram', 'tiktok']

// ---- Valores por defecto (D-42), usados también si la lectura pública falla ----

export const CONTACTO_PORTAL_RESPALDO: ContactoPortal = {
  whatsapp: '3166152907',
  whatsappEnlace: 'https://wa.me/573166152907',
  correo: 'vc.automotores@gmail.com',
  horario: ['Lunes a viernes: 9:00 a. m. a 5:00 p. m.', 'Sábados: 9:00 a. m. a 3:00 p. m.'],
  instagram: null,
  tiktok: null,
}

// ---- Límites y mensajes del contrato ----

export const HORARIO_LINEAS_MAX = 4
export const HORARIO_LINEA_MAX = 100
export const CORREO_MAX = 254
export const RED_SOCIAL_MAX = 200

export const MENSAJE_PARAM_WHATSAPP = 'Escribe un celular colombiano de 10 dígitos, por ejemplo 316 615 2907.'
export const MENSAJE_PARAM_CORREO = 'Escribe un correo válido.'
export const MENSAJE_PARAM_HORARIO =
  'Escribe de 1 a 4 líneas de horario, de hasta 100 caracteres cada una y sin los signos < >.'
export const MENSAJE_PARAM_INSTAGRAM =
  'Escribe el enlace de Instagram (https://www.instagram.com/...) o deja el campo vacío.'
export const MENSAJE_PARAM_TIKTOK = 'Escribe el enlace de TikTok (https://www.tiktok.com/@...) o deja el campo vacío.'

/** "3166152907" → "316 615 2907"; si no son 10 dígitos, se deja igual. */
export function formatearCelular(digitos: string): string {
  return /^\d{10}$/.test(digitos) ? `${digitos.slice(0, 3)} ${digitos.slice(3, 6)} ${digitos.slice(6)}` : digitos
}

/** Celular colombiano (P5): 10 dígitos que empiezan por 3, con o sin +57 y espacios. Devuelve los 10 dígitos o null. */
export function normalizarCelular(valor: string): string | null {
  const t = valor.trim()
  if (t === '' || t.length > 20 || !/^[0-9 +\-()]+$/.test(t)) return null
  let d = t.replace(/\D/g, '')
  if (d.length === 12 && d.startsWith('57')) d = d.slice(2)
  return d.length === 10 && d.startsWith('3') ? d : null
}

const PATRON_CORREO = /^[^\s@<>"']+@[^\s@<>"']+\.[^\s@<>"']+$/

/** Enlace https a uno de los hosts permitidos, con ruta, sin usuario ni puerto ni caracteres peligrosos. */
export function enlaceRedValido(valor: string, hosts: readonly string[]): boolean {
  if (valor.length > RED_SOCIAL_MAX || /[\s<>"']/.test(valor)) return false
  let url: URL
  try {
    url = new URL(valor)
  } catch {
    return false
  }
  return (
    url.protocol === 'https:' &&
    hosts.includes(url.hostname.toLowerCase()) &&
    url.username === '' &&
    url.password === '' &&
    url.port === '' &&
    url.pathname.replace(/\/+$/, '') !== ''
  )
}

export const HOSTS_INSTAGRAM = ['www.instagram.com', 'instagram.com'] as const
export const HOSTS_TIKTOK = ['www.tiktok.com', 'tiktok.com'] as const

/** Solo muestra un enlace de red social si pasa la misma regla (nunca `javascript:` ni otros hosts, CA-P7-41). */
export function enlaceRedSeguro(valor: string | null, hosts: readonly string[]): string | null {
  return valor && enlaceRedValido(valor.trim(), hosts) ? valor.trim() : null
}

export function validarContactoPortal(f: FormularioContactoPortal): ErroresContactoPortal {
  const errores: ErroresContactoPortal = {}
  if (normalizarCelular(f.whatsapp) === null) errores.whatsapp = [MENSAJE_PARAM_WHATSAPP]
  const correo = f.correo.trim()
  if (correo === '' || correo.length > CORREO_MAX || !PATRON_CORREO.test(correo)) errores.correo = [MENSAJE_PARAM_CORREO]
  const lineas = f.horario.map((l) => l.trim())
  if (
    lineas.length < 1 ||
    lineas.length > HORARIO_LINEAS_MAX ||
    lineas.some((l) => l === '' || l.length > HORARIO_LINEA_MAX || /[<>]/.test(l))
  ) {
    errores.horario = [MENSAJE_PARAM_HORARIO]
  }
  const instagram = f.instagram.trim()
  if (instagram !== '' && !enlaceRedValido(instagram, HOSTS_INSTAGRAM)) errores.instagram = [MENSAJE_PARAM_INSTAGRAM]
  const tiktok = f.tiktok.trim()
  if (tiktok !== '' && !enlaceRedValido(tiktok, HOSTS_TIKTOK)) errores.tiktok = [MENSAJE_PARAM_TIKTOK]
  return errores
}

/** Reparte `errores` de un 400 por campo (las claves `horario[0]` van a `horario`). */
export function erroresContactoDe(error: ApiError): { porCampo: ErroresContactoPortal; otros: string[] } {
  const porCampo: ErroresContactoPortal = {}
  const otros: string[] = []
  for (const [clave, mensajes] of Object.entries(error.errores)) {
    const base = clave.replace(/\[\d+\]$/, '').toLowerCase()
    const campo = CAMPOS_CONTACTO_PORTAL.find((c) => c === base)
    if (campo) porCampo[campo] = Array.from(new Set([...(porCampo[campo] ?? []), ...mensajes]))
    else otros.push(...mensajes)
  }
  return { porCampo, otros }
}

export function formularioDesdeContacto(c: ContactoPortal): FormularioContactoPortal {
  return {
    whatsapp: formatearCelular(c.whatsapp),
    correo: c.correo,
    horario: c.horario.length > 0 ? [...c.horario] : [''],
    instagram: c.instagram ?? '',
    tiktok: c.tiktok ?? '',
  }
}

export function cuerpoDesdeFormulario(f: FormularioContactoPortal) {
  return {
    whatsapp: f.whatsapp.trim(),
    correo: f.correo.trim(),
    horario: f.horario.map((l) => l.trim()),
    instagram: f.instagram.trim(),
    tiktok: f.tiktok.trim(),
  }
}

// ---- Lectura de respuestas ----

function texto(valor: unknown): string {
  return typeof valor === 'string' ? valor : ''
}

function textoONulo(valor: unknown): string | null {
  return typeof valor === 'string' && valor.trim() !== '' ? valor : null
}

export function leerContactoPortal(valor: unknown): ContactoPortal | null {
  if (!esObjetoJson(valor)) return null
  const whatsapp = texto(valor.whatsapp).replace(/\D/g, '')
  const correo = texto(valor.correo).trim()
  const horario = Array.isArray(valor.horario)
    ? valor.horario.filter((l): l is string => typeof l === 'string' && l.trim() !== '')
    : []
  if (!/^\d{10}$/.test(whatsapp) || correo === '' || horario.length === 0) return null
  const enlace = texto(valor.whatsappEnlace)
  return {
    whatsapp,
    // Solo se acepta un enlace de wa.me; si no, se arma con el número (nunca otro esquema).
    whatsappEnlace: /^https:\/\/wa\.me\/\d{10,15}$/.test(enlace) ? enlace : `https://wa.me/57${whatsapp}`,
    correo,
    horario,
    instagram: textoONulo(valor.instagram),
    tiktok: textoONulo(valor.tiktok),
  }
}

function leerContactoAdmin(valor: unknown): ContactoPortalAdmin {
  const base = leerContactoPortal(valor)
  if (!base || !esObjetoJson(valor)) throw new ApiError(200, MENSAJE_INESPERADO)
  return {
    ...base,
    configurado: valor.configurado === true,
    actualizadoEn: textoONulo(valor.actualizadoEn),
    actualizadoPor: textoONulo(valor.actualizadoPor),
  }
}

// ---- Endpoints ----

/** Lectura pública (anónima, sin token). */
export async function obtenerContactoPortal(signal?: AbortSignal): Promise<ContactoPortal> {
  const respuesta = await peticion('/api/portal/contacto', { signal })
  const contacto = leerContactoPortal(respuesta)
  if (!contacto) throw new ApiError(200, MENSAJE_INESPERADO)
  return contacto
}

export async function obtenerContactoAdmin(token: string, signal?: AbortSignal): Promise<ContactoPortalAdmin> {
  const respuesta = await peticion('/api/admin/parametros/contacto', {
    token,
    signal,
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA },
  })
  return leerContactoAdmin(respuesta)
}

export async function guardarContactoAdmin(token: string, f: FormularioContactoPortal): Promise<ContactoPortalAdmin> {
  const respuesta = await peticion('/api/admin/parametros/contacto', {
    method: 'PUT',
    token,
    body: cuerpoDesdeFormulario(f),
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA },
  })
  return leerContactoAdmin(respuesta)
}
