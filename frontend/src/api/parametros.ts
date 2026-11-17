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
  /**
   * P9 (CA-P9-33, D-83 A): razón social, NIT y dirección de notificaciones, solo si los tres están guardados;
   * null si falta alguno o si la lectura falla (no hay respaldo fijo).
   */
  empresa: EmpresaPortal | null
}

/** P9: datos de la empresa publicados en el portal. `nit` normalizado ("800197268-4"). */
export type EmpresaPortal = {
  razonSocial: string
  nit: string
  direccion: string
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
  empresa: null,
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

/**
 * B-2: misma regla estricta que el servidor. Parte local con letras y dígitos ASCII y `. _ + -` (empieza por letra
 * o dígito), `@`, dominio con al menos un punto y terminación de 2 a 24 letras; sin `..`. Así se rechazan
 * `? & ; , : / < > " ' %` y los espacios.
 */
const PATRON_CORREO = /^[A-Za-z0-9][A-Za-z0-9._+-]*@[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)*\.[A-Za-z]{2,24}$/

export function correoPortalValido(valor: string): boolean {
  return valor.length <= CORREO_MAX && !valor.includes('..') && PATRON_CORREO.test(valor)
}

/** Caracteres de control (saltos de línea, tabuladores...). */
const PATRON_CONTROL = /[\u0000-\u001F\u007F]/

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
  if (!correoPortalValido(correo)) errores.correo = [MENSAJE_PARAM_CORREO]
  const lineas = f.horario.map((l) => l.trim())
  if (
    lineas.length < 1 ||
    lineas.length > HORARIO_LINEAS_MAX ||
    lineas.some((l) => l === '' || l.length > HORARIO_LINEA_MAX || /[<>]/.test(l) || PATRON_CONTROL.test(l))
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
    empresa: leerEmpresaPublica(valor.empresa),
  }
}

/** CA-P9-33: solo con los tres datos y un NIT válido; si no, null (el pie no muestra nada de la empresa). */
function leerEmpresaPublica(valor: unknown): EmpresaPortal | null {
  if (!esObjetoJson(valor)) return null
  const razonSocial = texto(valor.razonSocial).trim()
  const direccion = texto(valor.direccion).trim()
  const nit = normalizarNit(texto(valor.nit))
  if (razonSocial === '' || direccion === '' || nit === null) return null
  return { razonSocial, nit, direccion }
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

// ---- P9: datos de la empresa (HU-085, RN-PA-005, D-63 B, D-83 A; contrato P9 §3.5 y §3.6) ----

export type EmpresaPortalAdmin = {
  razonSocial: string | null
  nit: string | null
  direccion: string | null
  /** true si los tres están guardados (solo entonces se publican). */
  completo: boolean
  /** false = aún no se guardó nada. */
  configurado: boolean
  actualizadoEn: string | null
  actualizadoPor: string | null
}

export type FormularioEmpresa = { razonSocial: string; nit: string; direccion: string }
export type CampoEmpresa = keyof FormularioEmpresa
export type ErroresEmpresa = Partial<Record<CampoEmpresa, string[]>>
export const CAMPOS_EMPRESA: readonly CampoEmpresa[] = ['razonSocial', 'nit', 'direccion']

export const RAZON_SOCIAL_MIN = 2
export const RAZON_SOCIAL_MAX = 150
export const DIRECCION_MIN = 5
export const DIRECCION_MAX = 200
/** Entrada bruta del NIT (con puntos y espacios), igual que el servidor. */
export const NIT_MAX = 40

export const MENSAJE_EMPRESA_RAZON_SOCIAL = 'Escribe la razón social (entre 2 y 150 caracteres, sin los signos < >).'
export const MENSAJE_EMPRESA_NIT = 'Escribe el NIT con su dígito de verificación, por ejemplo 900.123.456-7.'
export const MENSAJE_EMPRESA_DIRECCION =
  'Escribe la dirección de notificaciones (entre 5 y 200 caracteres, sin los signos < >).'

/** Pesos de la DIAN para el dígito de verificación, de derecha a izquierda. */
const PESOS_DIAN = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71] as const

/** Dígito de verificación de la DIAN (módulo 11) para una base de hasta 15 dígitos. */
export function digitoVerificacionNit(digitos: string): number {
  let suma = 0
  for (let i = 0; i < digitos.length; i++) suma += Number(digitos[digitos.length - 1 - i]) * PESOS_DIAN[i]
  const r = suma % 11
  return r === 0 || r === 1 ? r : 11 - r
}

/**
 * CA-P9-29 (misma regla que `ParametrosEmpresa.NormalizarNit` del servidor): base de 6 a 10 dígitos, con puntos o
 * espacios como separadores, un guion obligatorio y 1 dígito de verificación correcto; base solo de ceros no vale.
 * Devuelve "800197268-4" o null.
 */
export function normalizarNit(bruto: string): string | null {
  const t = bruto.trim()
  if (t === '' || t.length > NIT_MAX) return null
  const partes = t.split('-')
  if (partes.length !== 2) return null
  const [base, dvBruto] = partes
  const dv = dvBruto.trim()
  if (!/^[0-9. ]*$/.test(base)) return null
  const digitos = base.replace(/[. ]/g, '')
  if (digitos.length < 6 || digitos.length > 10 || /^0+$/.test(digitos)) return null
  if (!/^[0-9]$/.test(dv)) return null
  return digitoVerificacionNit(digitos) === Number(dv) ? `${digitos}-${dv}` : null
}

/** "800197268-4" → "800.197.268-4" (puntos de miles en la base). Si no tiene esa forma, se deja igual. */
export function formatearNit(nit: string): string {
  const m = /^(\d+)-(\d)$/.exec(nit)
  if (!m) return nit
  return `${m[1].replace(/\B(?=(\d{3})+(?!\d))/g, '.')}-${m[2]}`
}

/** Como `char.IsControl` de .NET: U+0000–U+001F y U+007F–U+009F. */
const PATRON_CONTROL_AMPLIO = /[\u0000-\u001F\u007F-\u009F]/

function textoEmpresaValido(valor: string, min: number, max: number): boolean {
  const t = valor.trim()
  if (t === '') return true // vacío = se retira (CA-P9-27)
  return t.length >= min && t.length <= max && !/[<>]/.test(t) && !PATRON_CONTROL_AMPLIO.test(t)
}

/** CA-P9-27 a 30: cada campo es opcional; si se escribe, debe cumplir su regla. */
export function validarEmpresa(f: FormularioEmpresa): ErroresEmpresa {
  const errores: ErroresEmpresa = {}
  if (!textoEmpresaValido(f.razonSocial, RAZON_SOCIAL_MIN, RAZON_SOCIAL_MAX))
    errores.razonSocial = [MENSAJE_EMPRESA_RAZON_SOCIAL]
  if (f.nit.trim() !== '' && normalizarNit(f.nit) === null) errores.nit = [MENSAJE_EMPRESA_NIT]
  if (!textoEmpresaValido(f.direccion, DIRECCION_MIN, DIRECCION_MAX)) errores.direccion = [MENSAJE_EMPRESA_DIRECCION]
  return errores
}

export function formularioDesdeEmpresa(e: EmpresaPortalAdmin): FormularioEmpresa {
  return {
    razonSocial: e.razonSocial ?? '',
    nit: e.nit ? formatearNit(e.nit) : '',
    direccion: e.direccion ?? '',
  }
}

/** Vacío o solo espacios = null (CA-P9-27). El servidor normaliza el NIT. */
export function cuerpoDesdeEmpresa(f: FormularioEmpresa) {
  const o = (v: string) => (v.trim() === '' ? null : v.trim())
  return { razonSocial: o(f.razonSocial), nit: o(f.nit), direccion: o(f.direccion) }
}

/** Reparte `errores` de un 400 por campo (sin distinguir mayúsculas). */
export function erroresEmpresaDe(error: ApiError): { porCampo: ErroresEmpresa; otros: string[] } {
  const porCampo: ErroresEmpresa = {}
  const otros: string[] = []
  for (const [clave, mensajes] of Object.entries(error.errores)) {
    const campo = CAMPOS_EMPRESA.find((c) => c.toLowerCase() === clave.toLowerCase())
    if (campo) porCampo[campo] = Array.from(new Set([...(porCampo[campo] ?? []), ...mensajes]))
    else otros.push(...mensajes)
  }
  return { porCampo, otros }
}

function leerEmpresaAdmin(valor: unknown): EmpresaPortalAdmin {
  if (!esObjetoJson(valor)) throw new ApiError(200, MENSAJE_INESPERADO)
  return {
    razonSocial: textoONulo(valor.razonSocial),
    nit: textoONulo(valor.nit),
    direccion: textoONulo(valor.direccion),
    completo: valor.completo === true,
    configurado: valor.configurado === true,
    actualizadoEn: textoONulo(valor.actualizadoEn),
    actualizadoPor: textoONulo(valor.actualizadoPor),
  }
}

export async function obtenerEmpresaAdmin(token: string, signal?: AbortSignal): Promise<EmpresaPortalAdmin> {
  const respuesta = await peticion('/api/admin/parametros/empresa', {
    token,
    signal,
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA },
  })
  return leerEmpresaAdmin(respuesta)
}

export async function guardarEmpresaAdmin(token: string, f: FormularioEmpresa): Promise<EmpresaPortalAdmin> {
  const respuesta = await peticion('/api/admin/parametros/empresa', {
    method: 'PUT',
    token,
    body: cuerpoDesdeEmpresa(f),
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA },
  })
  return leerEmpresaAdmin(respuesta)
}
