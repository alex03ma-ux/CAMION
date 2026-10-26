// Validación en cliente de los formularios (mismas reglas y mensajes del contrato P1).
// Funciones puras: reciben los valores y devuelven errores por campo.
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
} as const

// Formato básico: algo@algo.dominio, sin espacios.
const PATRON_EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const PATRON_TELEFONO = /^[0-9 +\-()]+$/

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

/**
 * Reparte los `errores` del servidor entre los campos conocidos del formulario.
 * Las claves se comparan sin distinguir mayúsculas (el contrato usa camelCase).
 * Lo que no corresponde a un campo se devuelve en `otros`.
 */
export function repartirErroresServidor(errores: ErroresPorCampo): {
  porCampo: ErroresRegistro
  otros: string[]
} {
  const porCampo: ErroresRegistro = {}
  const otros: string[] = []
  for (const [clave, mensajes] of Object.entries(errores)) {
    const campo = CAMPOS_REGISTRO.find((c) => c.toLowerCase() === clave.toLowerCase())
    if (campo) porCampo[campo] = [...(porCampo[campo] ?? []), ...mensajes]
    else otros.push(...mensajes)
  }
  return { porCampo, otros }
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
