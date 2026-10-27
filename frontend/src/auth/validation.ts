// Validación en cliente de los formularios (mismas reglas y mensajes de los contratos P1 y P2).
// Funciones puras: reciben los valores y devuelven errores por campo.
import { TIPOS_DOCUMENTO, type TipoDocumento } from '../api/auth'
import type { ErroresPorCampo } from '../api/http'
import { erroresPassword } from './passwordRules'

export type CampoRegistro = 'nombreCompleto' | 'email' | 'telefono' | 'password'
export const CAMPOS_REGISTRO: readonly CampoRegistro[] = [
  'nombreCompleto',
  'email',
  'telefono',
  'password',
]

export type DatosRegistro = Record<CampoRegistro, string>
export type ErroresRegistro = Partial<Record<CampoRegistro, string[]>>

export const MENSAJES_VALIDACION = {
  nombre: 'Escribe tu nombre completo (2 a 150 caracteres).',
  email: 'Escribe un correo válido.',
  emailLargo: 'El correo no puede tener más de 254 caracteres.',
  telefono: 'Escribe un teléfono válido (solo números, espacios, +, - o paréntesis).',
  tipoDocumento: 'Elige un tipo de documento válido.',
  numeroDocumento: 'Escribe un número de documento válido (4 a 30 letras, números o guiones).',
  nombreComercial: 'El nombre comercial no puede tener más de 150 caracteres.',
} as const

// Formato básico: algo@algo.dominio, sin espacios.
const PATRON_EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const PATRON_TELEFONO = /^[0-9 +\-()]+$/
// Número de documento (P2): solo letras, dígitos y guiones.
const PATRON_DOCUMENTO = /^[A-Za-z0-9-]+$/
export const DOCUMENTO_MIN = 4
export const DOCUMENTO_MAX = 30
export const NOMBRE_COMERCIAL_MAX = 150

export function validarNombre(valor: string): string[] {
  const largo = valor.trim().length
  return largo >= 2 && largo <= 150 ? [] : [MENSAJES_VALIDACION.nombre]
}

export function validarEmail(valor: string): string[] {
  const email = valor.trim()
  if (email.length > 254) return [MENSAJES_VALIDACION.emailLargo]
  return PATRON_EMAIL.test(email) ? [] : [MENSAJES_VALIDACION.email]
}

export function validarTelefono(valor: string): string[] {
  const telefono = valor.trim()
  const digitos = telefono.replace(/[^0-9]/g, '').length
  const valido =
    telefono.length >= 7 &&
    telefono.length <= 25 &&
    PATRON_TELEFONO.test(telefono) &&
    digitos >= 7
  return valido ? [] : [MENSAJES_VALIDACION.telefono]
}

export function validarRegistro(datos: DatosRegistro): ErroresRegistro {
  const errores: ErroresRegistro = {}
  const nombre = validarNombre(datos.nombreCompleto)
  const email = validarEmail(datos.email)
  const telefono = validarTelefono(datos.telefono)
  const password = erroresPassword(datos.password)
  if (nombre.length) errores.nombreCompleto = nombre
  if (email.length) errores.email = email
  if (telefono.length) errores.telefono = telefono
  if (password.length) errores.password = password
  return errores
}

export function hayErrores(errores: ErroresRegistro): boolean {
  return CAMPOS_REGISTRO.some((c) => (errores[c]?.length ?? 0) > 0)
}

// ---- Registro de asesor (contrato P2, D-3) ----

export type CampoAsesor = CampoRegistro | 'tipoDocumento' | 'numeroDocumento' | 'nombreComercial'
/** Orden en que aparecen los campos en el formulario (para enfocar el primer error). */
export const CAMPOS_ASESOR: readonly CampoAsesor[] = [
  'nombreCompleto',
  'email',
  'telefono',
  'tipoDocumento',
  'numeroDocumento',
  'nombreComercial',
  'password',
]

export type DatosRegistroAsesor = Record<CampoAsesor, string>
export type ErroresRegistroAsesor = Partial<Record<CampoAsesor, string[]>>

export function esTipoDocumento(valor: string): valor is TipoDocumento {
  return TIPOS_DOCUMENTO.some((t) => t.valor === valor)
}

export function validarTipoDocumento(valor: string): string[] {
  return esTipoDocumento(valor) ? [] : [MENSAJES_VALIDACION.tipoDocumento]
}

export function validarNumeroDocumento(valor: string): string[] {
  const numero = valor.trim()
  const valido =
    numero.length >= DOCUMENTO_MIN &&
    numero.length <= DOCUMENTO_MAX &&
    PATRON_DOCUMENTO.test(numero)
  return valido ? [] : [MENSAJES_VALIDACION.numeroDocumento]
}

export function validarNombreComercial(valor: string): string[] {
  return valor.trim().length <= NOMBRE_COMERCIAL_MAX ? [] : [MENSAJES_VALIDACION.nombreComercial]
}

export function validarRegistroAsesor(datos: DatosRegistroAsesor): ErroresRegistroAsesor {
  const errores: ErroresRegistroAsesor = { ...validarRegistro(datos) }
  const tipo = validarTipoDocumento(datos.tipoDocumento)
  const numero = validarNumeroDocumento(datos.numeroDocumento)
  const comercial = validarNombreComercial(datos.nombreComercial)
  if (tipo.length) errores.tipoDocumento = tipo
  if (numero.length) errores.numeroDocumento = numero
  if (comercial.length) errores.nombreComercial = comercial
  return errores
}

export function hayErroresEn<C extends string>(
  errores: Partial<Record<C, string[]>>,
  campos: readonly C[],
): boolean {
  return campos.some((c) => (errores[c]?.length ?? 0) > 0)
}

/** Igual que `repartirErroresServidor`, pero con los campos del formulario de asesor. */
export function repartirErroresAsesor(errores: ErroresPorCampo): {
  porCampo: ErroresRegistroAsesor
  otros: string[]
} {
  return repartirEn(errores, CAMPOS_ASESOR)
}

function repartirEn<C extends string>(
  errores: ErroresPorCampo,
  campos: readonly C[],
): { porCampo: Partial<Record<C, string[]>>; otros: string[] } {
  const porCampo: Partial<Record<C, string[]>> = {}
  const otros: string[] = []
  for (const [clave, mensajes] of Object.entries(errores)) {
    const campo = campos.find((c) => c.toLowerCase() === clave.toLowerCase())
    if (campo) porCampo[campo] = [...(porCampo[campo] ?? []), ...mensajes]
    else otros.push(...mensajes)
  }
  return { porCampo, otros }
}

/**
 * Reparte los `errores` del servidor entre los campos conocidos del formulario.
 * Las claves se comparan sin distinguir mayúsculas (el contrato usa camelCase).
 * Lo que no corresponde a un campo se devuelve en `otros`.
 */
export function repartirErroresServidor(errores: ErroresPorCampo): {
  porCampo: ErroresRegistro
  otros: string[]
} {
  return repartirEn(errores, CAMPOS_REGISTRO)
}

/** Datos listos para enviar: se recortan espacios (la contraseña no se toca). */
export function normalizarRegistro(datos: DatosRegistro): DatosRegistro {
  return {
    nombreCompleto: datos.nombreCompleto.trim(),
    email: datos.email.trim(),
    telefono: datos.telefono.trim(),
    password: datos.password,
  }
}

/** Datos de asesor listos para enviar; el nombre comercial vacío se envía como null. */
export function normalizarRegistroAsesor(datos: DatosRegistroAsesor): DatosRegistroAsesor {
  return {
    ...normalizarRegistro(datos),
    tipoDocumento: datos.tipoDocumento.trim(),
    numeroDocumento: datos.numeroDocumento.trim(),
    nombreComercial: datos.nombreComercial.trim(),
  }
}
