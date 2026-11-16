// P9 — Datos de la empresa administrables (HU-085, RN-PA-005, D-63 B, D-83 A; CA-P9-24 a CA-P9-37, parte UI).
import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import {
  cuerpoDesdeEmpresa,
  digitoVerificacionNit,
  formatearNit,
  MENSAJE_EMPRESA_DIRECCION,
  MENSAJE_EMPRESA_NIT,
  MENSAJE_EMPRESA_RAZON_SOCIAL,
  normalizarNit,
  validarEmpresa,
} from '../src/api/parametros'
import {
  CONTACTO_PORTAL_POR_DEFECTO,
  EMPRESA_ADMIN_VACIA,
  RUTA_CONTACTO_PORTAL,
  RUTA_EMPRESA_ADMIN,
  type RespuestaSimulada,
} from './helpers'
import { abrirApp } from './p4pDatos'

const VACIA = (EMPRESA_ADMIN_VACIA as { body: Record<string, unknown> }).body
const GUARDADA = {
  razonSocial: 'Financiamos Tu Camión S.A.S.',
  nit: '800197268-4',
  direccion: 'Calle 10 # 20-30, Bogotá',
  completo: true,
  configurado: true,
  actualizadoEn: '2026-11-16T15:00:00Z',
  actualizadoPor: 'Admin Ficticio',
}
const base = (CONTACTO_PORTAL_POR_DEFECTO as { body: Record<string, unknown> }).body

function puts(api: ReturnType<typeof abrirApp>['api']) {
  return api.llamadasA(RUTA_EMPRESA_ADMIN).filter((l) => l.init?.method === 'PUT')
}

describe('Regla del NIT igual a la del servidor (CA-P9-29)', () => {
  it('DV de la DIAN: 800197268 → 4', () => {
    expect(digitoVerificacionNit('800197268')).toBe(4)
  })

  it.each([
    ['800197268-4', '800197268-4'],
    ['800.197.268-4', '800197268-4'],
    ['800 197 268 - 4', '800197268-4'],
    ['  800197268-4  ', '800197268-4'],
  ])('%s se acepta y se normaliza', (entrada, normalizado) => {
    expect(normalizarNit(entrada)).toBe(normalizado)
  })

  it.each([['800197268-5'], ['8001972684'], ['80019-4'], ['12345678901-2'], ['ABC-1'], ['000000-0'], [''], ['800-197-268-4'], ['800197268-']])(
    '%s se rechaza',
    (entrada) => {
      expect(normalizarNit(entrada)).toBeNull()
    },
  )

  it('se muestra con puntos de miles', () => {
    expect(formatearNit('800197268-4')).toBe('800.197.268-4')
    expect(formatearNit('1234567-0')).toBe('1.234.567-0')
    expect(formatearNit('raro')).toBe('raro')
  })
})

describe('Validación de los campos (CA-P9-27, 28, 30)', () => {
  const ok = { razonSocial: 'Empresa S.A.S.', nit: '800.197.268-4', direccion: 'Calle 10 # 20-30' }

  it('los tres vacíos o solo con espacios son válidos y viajan como null', () => {
    const vacio = { razonSocial: '  ', nit: '', direccion: ' ' }
    expect(validarEmpresa(vacio)).toEqual({})
    expect(cuerpoDesdeEmpresa(vacio)).toEqual({ razonSocial: null, nit: null, direccion: null })
    expect(validarEmpresa(ok)).toEqual({})
    expect(cuerpoDesdeEmpresa({ ...ok, razonSocial: ' Empresa S.A.S. ' }).razonSocial).toBe('Empresa S.A.S.')
  })

  it('razón social: 2 a 150, sin < > ni caracteres de control', () => {
    expect(validarEmpresa({ ...ok, razonSocial: 'A' }).razonSocial).toEqual([MENSAJE_EMPRESA_RAZON_SOCIAL])
    expect(validarEmpresa({ ...ok, razonSocial: 'A'.repeat(151) }).razonSocial).toBeDefined()
    expect(validarEmpresa({ ...ok, razonSocial: 'A'.repeat(150) }).razonSocial).toBeUndefined()
    expect(validarEmpresa({ ...ok, razonSocial: '<b>Empresa</b>' }).razonSocial).toBeDefined()
    expect(validarEmpresa({ ...ok, razonSocial: 'Empresa\u0085S.A.S.' }).razonSocial).toBeDefined()
  })

  it('dirección: 5 a 200, sin < > ni caracteres de control', () => {
    expect(validarEmpresa({ ...ok, direccion: 'Cra' }).direccion).toEqual([MENSAJE_EMPRESA_DIRECCION])
    expect(validarEmpresa({ ...ok, direccion: 'C'.repeat(201) }).direccion).toBeDefined()
    expect(validarEmpresa({ ...ok, direccion: 'Calle\t10' }).direccion).toBeDefined()
  })

  it('NIT no válido', () => {
    expect(validarEmpresa({ ...ok, nit: '800197268-5' }).nit).toEqual([MENSAJE_EMPRESA_NIT])
  })
})

describe('Pantalla "Datos de la empresa" (CA-P9-24, 36)', () => {
  it('sin datos: sección, aviso y estado; guarda con PUT, anuncia el éxito y el pie los muestra', async () => {
    const { user, api } = abrirApp('#/cuenta/parametros', ['ADMINISTRADOR'], {
      [RUTA_EMPRESA_ADMIN]: [EMPRESA_ADMIN_VACIA, { status: 200, body: GUARDADA }],
      [RUTA_CONTACTO_PORTAL]: [CONTACTO_PORTAL_POR_DEFECTO, { status: 200, body: { ...base, empresa: GUARDADA } }],
    })
    const seccion = await screen.findByRole('region', { name: 'Datos de la empresa' })
    expect(within(seccion).getByText('Se muestran en el pie del portal solo cuando los tres están completos.')).toBeInTheDocument()
    expect(await within(seccion).findByText('Aún no se ha guardado ningún dato de la empresa.')).toBeInTheDocument()
    expect(within(seccion).getByText(/no se muestran en el portal/)).toBeInTheDocument()
    const razon = within(seccion).getByLabelText('Razón social')
    const nit = within(seccion).getByLabelText('NIT')
    const direccion = within(seccion).getByLabelText('Dirección de notificaciones')
    for (const campo of [razon, nit, direccion]) expect(campo).not.toHaveAttribute('aria-required')
    expect(nit).toHaveAccessibleDescription(/900\.123\.456-7/)

    await user.type(razon, ' Financiamos Tu Camión S.A.S. ')
    await user.type(nit, '800.197.268-4')
    await user.type(direccion, 'Calle 10 # 20-30, Bogotá')
    await user.click(within(seccion).getByRole('button', { name: 'Guardar datos de la empresa' }))

    await waitFor(() => expect(puts(api)).toHaveLength(1))
    expect(JSON.parse(String(puts(api)[0].init?.body))).toEqual({
      razonSocial: 'Financiamos Tu Camión S.A.S.',
      nit: '800.197.268-4',
      direccion: 'Calle 10 # 20-30, Bogotá',
    })
    expect(await within(seccion).findByText('Datos de la empresa guardados.')).toHaveAttribute('role', 'status')
    expect(within(seccion).getByLabelText('NIT')).toHaveValue('800.197.268-4')
    expect(within(seccion).getByText(/Última modificación: Admin Ficticio/)).toBeInTheDocument()
    expect(within(seccion).getByText('Estado: se muestran en el pie del portal.')).toBeInTheDocument()
    // El pie de esta misma carga se actualiza.
    const pie = screen.getByRole('contentinfo')
    expect(
      await within(pie).findByText(
        'Financiamos Tu Camión S.A.S. · NIT 800.197.268-4 · Dirección de notificaciones: Calle 10 # 20-30, Bogotá',
      ),
    ).toBeInTheDocument()
  })

  it('con datos guardados los muestra con el NIT formateado y el autor del cambio', async () => {
    abrirApp('#/cuenta/parametros', ['ADMINISTRADOR'], { [RUTA_EMPRESA_ADMIN]: { status: 200, body: GUARDADA } })
    const seccion = await screen.findByRole('region', { name: 'Datos de la empresa' })
    expect(await within(seccion).findByLabelText('NIT')).toHaveValue('800.197.268-4')
    expect(within(seccion).getByLabelText('Razón social')).toHaveValue('Financiamos Tu Camión S.A.S.')
    expect(within(seccion).getByText(/Última modificación: Admin Ficticio/)).toBeInTheDocument()
  })

  it('validación en el navegador: NIT no válido, foco en él y sin PUT', async () => {
    const { user, api } = abrirApp('#/cuenta/parametros', ['ADMINISTRADOR'])
    const seccion = await screen.findByRole('region', { name: 'Datos de la empresa' })
    const nit = await within(seccion).findByLabelText('NIT')
    await user.type(nit, '800197268-5')
    await user.click(within(seccion).getByRole('button', { name: 'Guardar datos de la empresa' }))
    expect(within(seccion).getByText(MENSAJE_EMPRESA_NIT)).toBeInTheDocument()
    expect(nit).toHaveAttribute('aria-invalid', 'true')
    await waitFor(() => expect(nit).toHaveFocus())
    expect(puts(api)).toHaveLength(0)
    // Al corregirlo, el error del campo desaparece.
    await user.clear(nit)
    expect(within(seccion).queryByText(MENSAJE_EMPRESA_NIT)).not.toBeInTheDocument()
  })

  it('400 del servidor: errores por campo y se conserva lo escrito', async () => {
    const { user } = abrirApp('#/cuenta/parametros', ['ADMINISTRADOR'], {
      [RUTA_EMPRESA_ADMIN]: [
        EMPRESA_ADMIN_VACIA,
        {
          status: 400,
          body: { error: 'Revisa los datos marcados.', errores: { direccion: [MENSAJE_EMPRESA_DIRECCION], razonSocial: ['Otro.'] } },
        },
      ],
    })
    const seccion = await screen.findByRole('region', { name: 'Datos de la empresa' })
    await user.type(await within(seccion).findByLabelText('Razón social'), 'Empresa S.A.S.')
    await user.type(within(seccion).getByLabelText('Dirección de notificaciones'), 'Calle 10 # 20-30')
    await user.click(within(seccion).getByRole('button', { name: 'Guardar datos de la empresa' }))
    expect(await within(seccion).findByText(MENSAJE_EMPRESA_DIRECCION)).toBeInTheDocument()
    expect(within(seccion).getByLabelText('Dirección de notificaciones')).toHaveAttribute('aria-invalid', 'true')
    expect(within(seccion).getByLabelText('Razón social')).toHaveValue('Empresa S.A.S.')
    expect(within(seccion).getByRole('alert')).toHaveTextContent('Revisa los datos marcados.')
    await waitFor(() => expect(within(seccion).getByLabelText('Razón social')).toHaveFocus())
  })

  it('un 401 al guardar lleva a la pantalla de sesión terminada', async () => {
    const { user } = abrirApp('#/cuenta/parametros', ['ADMINISTRADOR'], {
      [RUTA_EMPRESA_ADMIN]: [EMPRESA_ADMIN_VACIA, { status: 401 }],
    })
    const seccion = await screen.findByRole('region', { name: 'Datos de la empresa' })
    await within(seccion).findByLabelText('NIT')
    await user.click(within(seccion).getByRole('button', { name: 'Guardar datos de la empresa' }))
    expect(await screen.findByText('Tu sesión terminó. Inicia sesión de nuevo.')).toBeInTheDocument()
  })

  it('si la lectura falla ofrece reintentar', async () => {
    const respuestas: RespuestaSimulada[] = [{ status: 500 }, { status: 200, body: VACIA }]
    const { user } = abrirApp('#/cuenta/parametros', ['ADMINISTRADOR'], { [RUTA_EMPRESA_ADMIN]: respuestas })
    const seccion = await screen.findByRole('region', { name: 'Datos de la empresa' })
    await user.click(await within(seccion).findByRole('button', { name: 'Reintentar' }))
    expect(await within(seccion).findByLabelText('NIT')).toBeInTheDocument()
  })

  it('un comprador en #/cuenta/parametros no ve la sección ni llama a la API de la empresa', async () => {
    const { api } = abrirApp('#/cuenta/parametros', ['COMPRADOR'])
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Datos de la empresa' })).not.toBeInTheDocument()
    expect(api.llamadasA(RUTA_EMPRESA_ADMIN)).toHaveLength(0)
  })
})
