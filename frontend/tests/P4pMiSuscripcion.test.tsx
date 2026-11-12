// P4p — Tarjeta "Mi suscripción" del asesor en "Mi cuenta" (HU-066, RN-SU-001, RN-SU-005).
import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { cabecera, TOKEN_FICTICIO } from './helpers'
import { abrirApp, RUTA_MI_SUSCRIPCION } from './p4pDatos'

const CON_PLAN = {
  status: 200,
  body: {
    plan: { nombre: 'Profesional', limitePublicaciones: 30, diasVigencia: 180 },
    estado: 'ACTIVA',
    estadoEfectivo: 'ACTIVA',
    inicioVigencia: '2026-10-01T12:00:00Z',
    finVigencia: '2027-03-30T12:00:00Z',
    diasRestantes: 152,
    vigente: true,
  },
}

const SIN_PLAN = {
  status: 200,
  body: { suscripcion: null, mensaje: 'Aún no tienes un plan activo. Comunícate con el administrador.' },
}

function datoDe(tarjeta: HTMLElement, termino: string): string {
  const dt = within(tarjeta).getByText(termino, { selector: 'dt' })
  return dt.nextElementSibling?.textContent ?? ''
}

describe('Mi suscripción (asesor)', () => {
  it('con plan: plan, estado, vence el…, días restantes y cupo; con el token', async () => {
    const { api } = abrirApp('#/cuenta', ['ASESOR'], { [RUTA_MI_SUSCRIPCION]: CON_PLAN })
    const tarjeta = await screen.findByRole('region', { name: 'Mi suscripción' })
    await waitFor(() => expect(datoDe(tarjeta, 'Plan')).toBe('Profesional'))
    expect(datoDe(tarjeta, 'Estado')).toBe('Activa')
    expect(datoDe(tarjeta, 'Vence el')).toBe(
      new Intl.DateTimeFormat('es-CO', { dateStyle: 'long' }).format(Date.parse('2027-03-30T12:00:00Z')),
    )
    expect(datoDe(tarjeta, 'Días restantes')).toBe('152 días')
    expect(datoDe(tarjeta, 'Cupo')).toBe('30 publicaciones')
    expect(cabecera(api.llamadasA(RUTA_MI_SUSCRIPCION)[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
  })

  it('sin plan: el mensaje del contrato', async () => {
    abrirApp('#/cuenta', ['ASESOR'], { [RUTA_MI_SUSCRIPCION]: SIN_PLAN })
    const tarjeta = await screen.findByRole('region', { name: 'Mi suscripción' })
    expect(
      await within(tarjeta).findByText('Aún no tienes un plan activo. Comunícate con el administrador.'),
    ).toBeInTheDocument()
    expect(within(tarjeta).queryByText('Plan', { selector: 'dt' })).not.toBeInTheDocument()
  })

  it('suspendida: estado en texto y aviso de que sus publicaciones no se muestran', async () => {
    abrirApp('#/cuenta', ['ASESOR'], {
      [RUTA_MI_SUSCRIPCION]: {
        status: 200,
        body: { ...CON_PLAN.body, estado: 'SUSPENDIDA', estadoEfectivo: 'SUSPENDIDA', vigente: false },
      },
    })
    const tarjeta = await screen.findByRole('region', { name: 'Mi suscripción' })
    await waitFor(() => expect(datoDe(tarjeta, 'Estado')).toBe('Suspendida'))
    expect(within(tarjeta).getByText(/tus publicaciones no se muestran/)).toBeInTheDocument()
  })

  it('vencida (estado efectivo): se muestra "Vencida"', async () => {
    abrirApp('#/cuenta', ['ASESOR'], {
      [RUTA_MI_SUSCRIPCION]: {
        status: 200,
        body: { ...CON_PLAN.body, estadoEfectivo: 'VENCIDA', diasRestantes: 0, vigente: false },
      },
    })
    const tarjeta = await screen.findByRole('region', { name: 'Mi suscripción' })
    await waitFor(() => expect(datoDe(tarjeta, 'Estado')).toBe('Vencida'))
    expect(datoDe(tarjeta, 'Días restantes')).toBe('0 días')
  })

  it('sin conexión: error comprensible y Reintentar', async () => {
    const { user, api } = abrirApp('#/cuenta', ['ASESOR'], {
      [RUTA_MI_SUSCRIPCION]: [{ red: true }, SIN_PLAN],
    })
    const tarjeta = await screen.findByRole('region', { name: 'Mi suscripción' })
    expect(await within(tarjeta).findByText('No se pudo conectar con el servidor.')).toBeInTheDocument()
    await user.click(within(tarjeta).getByRole('button', { name: 'Reintentar' }))
    expect(await within(tarjeta).findByText(/Aún no tienes un plan activo/)).toBeInTheDocument()
    expect(api.llamadasA(RUTA_MI_SUSCRIPCION)).toHaveLength(2)
  })

  it('401: la sesión termina', async () => {
    abrirApp('#/cuenta', ['ASESOR'], { [RUTA_MI_SUSCRIPCION]: { status: 401 } })
    expect(await screen.findByText(/Tu sesión terminó/)).toBeInTheDocument()
  })

  it.each([['COMPRADOR'], ['ADMINISTRADOR']])('%s no ve la tarjeta ni consulta la suscripción', async (rol) => {
    const { api } = abrirApp('#/cuenta', [rol])
    expect(await screen.findByRole('heading', { name: /Bienvenido/ })).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Mi suscripción' })).not.toBeInTheDocument()
    expect(api.llamadasA(RUTA_MI_SUSCRIPCION)).toHaveLength(0)
  })
})
