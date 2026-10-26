// Reglas de contraseña del contrato P1 (funciones puras, sin React).
// Mayúscula, minúscula y dígito solo en ASCII (A-Z, a-z, 0-9), igual que el backend:
// "Ñ" o letras acentuadas no cuentan como mayúscula/minúscula.

export const PASSWORD_MIN = 8
export const PASSWORD_MAX = 128

export type ReglaPassword = {
  id: 'longitud' | 'mayuscula' | 'minuscula' | 'digito'
  /** Texto para la lista visible en el formulario. */
  descripcion: string
  /** Mensaje del contrato cuando la regla no se cumple. */
  mensaje: string
  cumple: (password: string) => boolean
}

export const REGLAS_PASSWORD: readonly ReglaPassword[] = [
  {
    id: 'longitud',
    descripcion: `Al menos ${PASSWORD_MIN} caracteres`,
    mensaje: 'Debe tener al menos 8 caracteres.',
    cumple: (p) => p.length >= PASSWORD_MIN,
  },
  {
    id: 'mayuscula',
    descripcion: 'Al menos una letra mayúscula (A-Z)',
    mensaje: 'Debe tener al menos una letra mayúscula.',
    cumple: (p) => /[A-Z]/.test(p),
  },
  {
    id: 'minuscula',
    descripcion: 'Al menos una letra minúscula (a-z)',
    mensaje: 'Debe tener al menos una letra minúscula.',
    cumple: (p) => /[a-z]/.test(p),
  },
  {
    id: 'digito',
    descripcion: 'Al menos un número (0-9)',
    mensaje: 'Debe tener al menos un número.',
    cumple: (p) => /[0-9]/.test(p),
  },
]

export const MENSAJE_PASSWORD_MAX = `No puede tener más de ${PASSWORD_MAX} caracteres.`

export type EstadoRegla = { regla: ReglaPassword; cumplida: boolean }

export function evaluarPassword(password: string): EstadoRegla[] {
  return REGLAS_PASSWORD.map((regla) => ({ regla, cumplida: regla.cumple(password) }))
}

/** Un mensaje por regla incumplida (vacío si la contraseña es válida). */
export function erroresPassword(password: string): string[] {
  const errores = REGLAS_PASSWORD.filter((r) => !r.cumple(password)).map((r) => r.mensaje)
  if (password.length > PASSWORD_MAX) errores.push(MENSAJE_PASSWORD_MAX)
  return errores
}
