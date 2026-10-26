// Reglas de contraseña y validación en cliente (CA-N03-07, 08, 09, 10, 11, 12, 22).
import { describe, expect, it } from 'vitest'
import {
  erroresPassword,
  evaluarPassword,
  MENSAJE_PASSWORD_MAX,
  PASSWORD_MAX,
} from '../src/auth/passwordRules'
import {
  MENSAJES_VALIDACION,
  validarEmail,
  validarNombre,
  validarRegistro,
  validarTelefono,
} from '../src/auth/validation'

const M_LONG = 'Debe tener al menos 8 caracteres.'
const M_MAY = 'Debe tener al menos una letra mayúscula.'
const M_MIN = 'Debe tener al menos una letra minúscula.'
const M_DIG = 'Debe tener al menos un número.'

function cumplidas(password: string) {
  return Object.fromEntries(evaluarPassword(password).map((e) => [e.regla.id, e.cumplida]))
}

describe('passwordRules', () => {
  it('CA-N03-11 contraseña válida no da errores', () => {
    expect(erroresPassword('Camion2026')).toEqual([])
  })

  it('CA-N03-11 regla de longitud mínima (8)', () => {
    expect(erroresPassword('Camio26')).toEqual([M_LONG])
    expect(erroresPassword('Camion26')).toEqual([])
  })

  it('CA-N03-11 regla de mayúscula', () => {
    expect(erroresPassword('camion2026')).toEqual([M_MAY])
  })

  it('CA-N03-11 regla de minúscula', () => {
    expect(erroresPassword('CAMION2026')).toEqual([M_MIN])
  })

  it('CA-N03-11 regla de número', () => {
    expect(erroresPassword('Camionaaa')).toEqual([M_DIG])
  })

  it('CA-N03-11 "abc" incumple 3 reglas, un mensaje por regla', () => {
    expect(erroresPassword('abc')).toEqual([M_LONG, M_MAY, M_DIG])
  })

  it('CA-N03-11 solo ASCII: "Ñandu123" no cumple mayúscula', () => {
    expect(cumplidas('Ñandu123').mayuscula).toBe(false)
    expect(erroresPassword('Ñandu123')).toEqual([M_MAY])
  })

  it('CA-N03-11 solo ASCII: letras acentuadas no cuentan como minúscula', () => {
    expect(cumplidas('ABCDÉÁ12').minuscula).toBe(false)
  })

  it('CA-N03-12 máximo 128 caracteres: 128 vale, 129 no', () => {
    const base = 'Aa1'
    const p128 = base + 'x'.repeat(PASSWORD_MAX - base.length)
    expect(p128).toHaveLength(128)
    expect(erroresPassword(p128)).toEqual([])
    expect(erroresPassword(p128 + 'x')).toEqual([MENSAJE_PASSWORD_MAX])
  })

  it('CA-N03-21 evaluarPassword informa cada regla', () => {
    expect(cumplidas('')).toEqual({
      longitud: false,
      mayuscula: false,
      minuscula: false,
      digito: false,
    })
    expect(cumplidas('Camion2026')).toEqual({
      longitud: true,
      mayuscula: true,
      minuscula: true,
      digito: true,
    })
  })
})

describe('validación de correo, teléfono y nombre', () => {
  it('CA-N03-07 correos sin formato válido', () => {
    for (const e of ['ana', 'ana@', 'ana@@x.com', 'ana@x', 'a na@x.com', '']) {
      expect(validarEmail(e), e).toEqual([MENSAJES_VALIDACION.email])
    }
    expect(validarEmail('ana.perez@ejemplo.com')).toEqual([])
  })

  it('CA-N03-08 correo de 255 caracteres rechazado; 254 aceptado', () => {
    const dominio = '@ejemplo.com' // 12
    const e254 = 'a'.repeat(254 - dominio.length) + dominio
    expect(e254).toHaveLength(254)
    expect(validarEmail(e254)).toEqual([])
    const e255 = 'a' + e254
    expect(validarEmail(e255)).toEqual([MENSAJES_VALIDACION.emailLargo])
  })

  it('CA-N03-09 teléfonos inválidos', () => {
    for (const t of ['', '300abc4567', '12-34', '300#1234567', '1'.repeat(26)]) {
      expect(validarTelefono(t), t).toEqual([MENSAJES_VALIDACION.telefono])
    }
  })

  it('CA-N03-10 teléfono "+57 (300) 123-4567" válido', () => {
    expect(validarTelefono('+57 (300) 123-4567')).toEqual([])
  })

  it('CA-N03-05 nombre vacío, espacios, 1 o 151 caracteres', () => {
    for (const n of ['', '   ', 'A', 'a'.repeat(151)]) {
      expect(validarNombre(n), n).toEqual([MENSAJES_VALIDACION.nombre])
    }
    expect(validarNombre('  Ana  ')).toEqual([])
  })

  it('CA-N03-13 validarRegistro devuelve todos los campos inválidos a la vez', () => {
    const errores = validarRegistro({ nombreCompleto: '', email: 'x', telefono: '1', password: 'a' })
    expect(Object.keys(errores).sort()).toEqual(['email', 'nombreCompleto', 'password', 'telefono'])
  })
})
