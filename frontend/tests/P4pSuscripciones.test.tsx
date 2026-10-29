// P4p — Suscripciones del administrador (HU-066 a HU-069, RN-SU-001 a RN-SU-005; D-4, D-26, D-36).
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { accionesSuscripcion } from '../src/api/suscripciones'
import { AdminSuscripciones } from '../src/components/AdminSuscripciones'
import { simularFetch, TOKEN_FICTICIO } from './helpers'
import {
  CATALOGOS_INVENTARIO,
  cuerpoDe,
  paginaDe,
  PLAN_BASICO,
  PLAN_PRO,
  RUTA_CATALOGOS_INV,
  RUTA_PLANES_ACTIVOS,
  RUTA_SUSCRIPCIONES,
  suscripcion,
  type Rutas,
} from './p4pDatos'

const BASE: Rutas = {
  [RUTA_CATALOGOS_INV]: CATALOGOS_INVENTARIO,
  [RUTA_PLANES_ACTIVOS]: { status: 200, body: { items: [PLAN_BASICO, PLAN_PRO] } },
}

const ACTIVA = suscripcion(11, 'ACTIVA')
const SUSPENDIDA = suscripcion(12, 'SUSPENDIDA', { asesor: { id: 8, nombre: 'Asesor Dos' } })
const VENCIDA = suscripcion(13, 'ACTIVA', {
  estadoEfectivo: 'VENCIDA',
  asesor: { id: 9, nombre: 'Asesor Tres' },
  diasRestantes: 0,
})
const CANCELADA = suscripcion(14, 'CANCELADA', { asesor: { id: 10, nombre: 'Asesor Cuatro' } })
const REACTIVADA = suscripcion(15, 'REACTIVADA', { asesor: { id: 11, nombre: 'Asesor Cinco' }, diasRestantes: 1 })

function renderSuscripciones(rutas: Rutas) {
  const api = simularFetch({ ...BASE, ...rutas })
  const onSesionTerminada = vi.fn()
  render(<AdminSuscripciones token={TOKEN_FICTICIO} onSesionTerminada={onSesionTerminada} />)
  return { api, onSesionTerminada, user: userEvent.setup() }
}

async function filaDe(nombre: string) {
  const celda = await screen.findByRole('rowheader', { name: nombre })
  return celda.closest('tr') as HTMLElement
}

function botonesDe(fila: HTMLElement): string[] {
  return within(fila)
    .queryAllByRole('button')
    .map((b) => (b.textContent ?? '').replace(/\s+de .*$/, '').trim())
}

describe('Acciones válidas según el estado (contrato)', () => {
  it.each([
    ['ACTIVA', 'ACTIVA', ['renovar', 'cambiar-plan', 'suspender', 'cancelar']],
    ['REACTIVADA', 'REACTIVADA', ['renovar', 'cambiar-plan', 'suspender', 'cancelar']],
    ['ACTIVA', 'VENCIDA', ['renovar', 'cambiar-plan', 'cancelar']],
    ['SUSPENDIDA', 'SUSPENDIDA', ['reactivar', 'cambiar-plan', 'cancelar']],
    ['CANCELADA', 'CANCELADA', []],
    ['RECHAZADA', 'RECHAZADA', []],
  ])('estado %s / efectivo %s', (estado, estadoEfectivo, esperadas) => {
    expect(accionesSuscripcion({ estado, estadoEfectivo })).toEqual(esperadas)
  })
})

describe('Lista de suscripciones', () => {
  it('estado en texto, fechas locales, días restantes y solo las acciones válidas', async () => {
    renderSuscripciones({ [RUTA_SUSCRIPCIONES]: paginaDe([ACTIVA, SUSPENDIDA, VENCIDA, CANCELADA, REACTIVADA]) })
    const activa = await filaDe('Asesor Uno')
    expect(within(activa).getByText('Activa')).toBeInTheDocument()
    expect(within(activa).getByText('Básico')).toBeInTheDocument()
    expect(within(activa).getByText('62 días')).toBeInTheDocument()
    expect(within(activa).getByText(new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium' }).format(Date.parse('2026-12-30T12:00:00Z')))).toBeInTheDocument()
    expect(botonesDe(activa)).toEqual(['Renovar', 'Cambiar plan', 'Suspender', 'Cancelar suscripción'])

    const suspendida = await filaDe('Asesor Dos')
    expect(within(suspendida).getByText('Suspendida')).toBeInTheDocument()
    expect(botonesDe(suspendida)).toEqual(['Reactivar', 'Cambiar plan', 'Cancelar suscripción'])

    const vencida = await filaDe('Asesor Tres')
    expect(within(vencida).getByText('Vencida')).toBeInTheDocument()
    expect(botonesDe(vencida)).toEqual(['Renovar', 'Cambiar plan', 'Cancelar suscripción'])

    const cancelada = await filaDe('Asesor Cuatro')
    expect(within(cancelada).getByText('Cancelada')).toBeInTheDocument()
    expect(within(cancelada).getByText('Sin acciones')).toBeInTheDocument()

    const reactivada = await filaDe('Asesor Cinco')
    expect(within(reactivada).getByText('Reactivada')).toBeInTheDocument()
    expect(within(reactivada).getByText('1 día')).toBeInTheDocument()
  })

  it('filtra por asesor y estado', async () => {
    const filtrada = '/api/admin/suscripciones?asesorId=8&estado=SUSPENDIDA&pagina=1&tamano=20'
    const { user, api } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: paginaDe([ACTIVA, SUSPENDIDA]),
      [filtrada]: paginaDe([SUSPENDIDA]),
    })
    await filaDe('Asesor Uno')
    await user.selectOptions(screen.getByLabelText('Asesor'), 'Asesor Dos')
    await user.selectOptions(screen.getByLabelText('Estado'), 'Suspendida')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => expect(api.llamadasA(filtrada)).toHaveLength(1))
    await waitFor(() => expect(screen.queryByRole('rowheader', { name: 'Asesor Uno' })).not.toBeInTheDocument())
    expect(await filaDe('Asesor Dos')).toBeInTheDocument()
  })

  it('401 al cargar: termina la sesión', async () => {
    const { onSesionTerminada } = renderSuscripciones({ [RUTA_SUSCRIPCIONES]: { status: 401 } })
    await waitFor(() => expect(onSesionTerminada).toHaveBeenCalled())
  })
})

describe('Asignar plan', () => {
  const RUTA_ASIGNAR = '/api/admin/asesores/7/suscripcion'

  it('exige asesor y plan antes de enviar (D-31)', async () => {
    const { user, api } = renderSuscripciones({ [RUTA_SUSCRIPCIONES]: paginaDe([]) })
    await screen.findByText('No hay suscripciones con estos filtros.')
    expect(screen.getByLabelText('Asesor aprobado')).toHaveAttribute('aria-required', 'true')
    expect(screen.getByLabelText('Plan activo')).toHaveAttribute('aria-required', 'true')
    await user.click(screen.getByRole('button', { name: 'Asignar plan' }))
    expect(screen.getByText('Elige un asesor aprobado.')).toBeInTheDocument()
    expect(screen.getByText('Elige un plan activo.')).toBeInTheDocument()
    expect(api.llamadasA(RUTA_ASIGNAR)).toHaveLength(0)
  })

  it('solo ofrece planes activos y asesores aprobados; envía { planId } y recarga', async () => {
    const { user, api } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: [paginaDe([]), paginaDe([ACTIVA])],
      [RUTA_ASIGNAR]: { status: 201, body: ACTIVA },
    })
    await screen.findByText('No hay suscripciones con estos filtros.')
    const planes = within(screen.getByLabelText('Plan activo')).getAllByRole('option').map((o) => o.textContent)
    expect(planes).toEqual(['Elige un plan', 'Básico (3 meses, 10 publicaciones)', 'Profesional (6 meses, 30 publicaciones)'])
    await user.selectOptions(screen.getByLabelText('Asesor aprobado'), 'Asesor Uno')
    await user.selectOptions(screen.getByLabelText('Plan activo'), 'Básico (3 meses, 10 publicaciones)')
    await user.click(screen.getByRole('button', { name: 'Asignar plan' }))
    await waitFor(() => expect(api.llamadasA(RUTA_ASIGNAR)).toHaveLength(1))
    expect(api.llamadasA(RUTA_ASIGNAR)[0].init?.method).toBe('POST')
    expect(cuerpoDe(api, RUTA_ASIGNAR)).toEqual({ planId: 1 })
    expect(await screen.findByText('Se asignó el plan Básico a Asesor Uno.')).toBeInTheDocument()
    expect(await filaDe('Asesor Uno')).toBeInTheDocument()
  })

  it('409 si ya tiene una suscripción viva: muestra el mensaje del servidor', async () => {
    const { user } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: paginaDe([]),
      [RUTA_ASIGNAR]: { status: 409, body: { error: 'El asesor ya tiene una suscripción viva.' } },
    })
    await screen.findByText('No hay suscripciones con estos filtros.')
    await user.selectOptions(screen.getByLabelText('Asesor aprobado'), 'Asesor Uno')
    await user.selectOptions(screen.getByLabelText('Plan activo'), 'Básico (3 meses, 10 publicaciones)')
    await user.click(screen.getByRole('button', { name: 'Asignar plan' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('El asesor ya tiene una suscripción viva.')
  })
})

describe('Acciones sobre una suscripción', () => {
  it('Suspender exige motivo de 5 a 500 caracteres y luego envía { motivo }', async () => {
    const ruta = '/api/admin/suscripciones/11/suspender'
    const { user, api } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: [paginaDe([ACTIVA]), paginaDe([{ ...ACTIVA, estado: 'SUSPENDIDA', estadoEfectivo: 'SUSPENDIDA' }])],
      [ruta]: { status: 200, body: { ...ACTIVA, estado: 'SUSPENDIDA', estadoEfectivo: 'SUSPENDIDA' } },
    })
    await user.click(within(await filaDe('Asesor Uno')).getByRole('button', { name: 'Suspender de Asesor Uno' }))
    const dialogo = screen.getByRole('dialog', { name: 'Suspender suscripción' })
    const motivo = within(dialogo).getByLabelText('Motivo')
    expect(motivo).toHaveFocus()
    expect(motivo).toHaveAttribute('aria-required', 'true')
    await user.type(motivo, 'abc')
    await user.click(within(dialogo).getByRole('button', { name: 'Suspender' }))
    expect(within(dialogo).getByText('Escribe el motivo (5 a 500 caracteres).')).toBeInTheDocument()
    expect(api.llamadasA(ruta)).toHaveLength(0)
    await user.type(motivo, ' pago pendiente')
    await user.click(within(dialogo).getByRole('button', { name: 'Suspender' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(cuerpoDe(api, ruta)).toEqual({ motivo: 'abc pago pendiente' })
    expect(await screen.findByText('Se suspendió la suscripción de Asesor Uno.')).toBeInTheDocument()
    expect(within(await filaDe('Asesor Uno')).getByText('Suspendida')).toBeInTheDocument()
  })

  it('Cancelar exige motivo; un 409 de transición inválida cierra el diálogo y lo muestra', async () => {
    const ruta = '/api/admin/suscripciones/11/cancelar'
    const { user, api } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: paginaDe([ACTIVA]),
      [ruta]: { status: 409, body: { error: 'No se puede cancelar una suscripción en estado CANCELADA.' } },
    })
    await user.click(within(await filaDe('Asesor Uno')).getByRole('button', { name: 'Cancelar suscripción de Asesor Uno' }))
    const dialogo = screen.getByRole('dialog', { name: 'Cancelar suscripción' })
    await user.click(within(dialogo).getByRole('button', { name: 'Cancelar suscripción' }))
    expect(within(dialogo).getByText('Escribe el motivo (5 a 500 caracteres).')).toBeInTheDocument()
    await user.type(within(dialogo).getByLabelText('Motivo'), 'Lo pidió el asesor')
    await user.click(within(dialogo).getByRole('button', { name: 'Cancelar suscripción' }))
    expect(await screen.findByText('No se puede cancelar una suscripción en estado CANCELADA.')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(cuerpoDe(api, ruta)).toEqual({ motivo: 'Lo pidió el asesor' })
    await waitFor(() => expect(api.llamadasA(RUTA_SUSCRIPCIONES)).toHaveLength(2))
  })

  it('Renovar sin elegir plan envía {} (plan actual) y muestra la nueva fecha', async () => {
    const ruta = '/api/admin/suscripciones/13/renovar'
    const { user, api } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: paginaDe([VENCIDA]),
      [ruta]: { status: 200, body: { ...VENCIDA, estadoEfectivo: 'ACTIVA', finVigencia: '2027-01-27T12:00:00Z' } },
    })
    await user.click(within(await filaDe('Asesor Tres')).getByRole('button', { name: 'Renovar de Asesor Tres' }))
    const dialogo = screen.getByRole('dialog', { name: 'Renovar suscripción' })
    const select = within(dialogo).getByLabelText('Plan para la renovación')
    expect(select).toHaveFocus()
    expect(within(select).getAllByRole('option')[0]).toHaveTextContent('Mantener el plan actual (Básico)')
    await user.click(within(dialogo).getByRole('button', { name: 'Renovar' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(cuerpoDe(api, ruta)).toEqual({})
    expect(await screen.findByText(/^Se renovó la suscripción de Asesor Tres\. Ahora vence el /)).toBeInTheDocument()
  })

  it('Cambiar plan exige elegir un plan distinto y envía { planId }', async () => {
    const ruta = '/api/admin/suscripciones/11/cambiar-plan'
    const { user, api } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: paginaDe([ACTIVA]),
      [ruta]: { status: 201, body: suscripcion(20, 'ACTIVA', { plan: { id: 2, nombre: 'Profesional' } }) },
    })
    await user.click(within(await filaDe('Asesor Uno')).getByRole('button', { name: 'Cambiar plan de Asesor Uno' }))
    const dialogo = screen.getByRole('dialog', { name: 'Cambiar de plan' })
    const select = within(dialogo).getByLabelText('Plan nuevo')
    // El plan actual (Básico) no se ofrece.
    expect(within(select).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Elige un plan activo',
      'Profesional (6 meses, 30 publicaciones)',
    ])
    await user.click(within(dialogo).getByRole('button', { name: 'Cambiar plan' }))
    expect(within(dialogo).getByText('Elige un plan activo.')).toBeInTheDocument()
    expect(api.llamadasA(ruta)).toHaveLength(0)
    await user.selectOptions(select, 'Profesional (6 meses, 30 publicaciones)')
    await user.click(within(dialogo).getByRole('button', { name: 'Cambiar plan' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(cuerpoDe(api, ruta)).toEqual({ planId: 2 })
    expect(await screen.findByText('Se cambió el plan de Asesor Uno a Profesional.')).toBeInTheDocument()
  })

  it('Reactivar una suspendida: motivo opcional', async () => {
    const ruta = '/api/admin/suscripciones/12/reactivar'
    const { user, api } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: paginaDe([SUSPENDIDA]),
      [ruta]: { status: 200, body: { ...SUSPENDIDA, estado: 'REACTIVADA', estadoEfectivo: 'REACTIVADA' } },
    })
    await user.click(within(await filaDe('Asesor Dos')).getByRole('button', { name: 'Reactivar de Asesor Dos' }))
    const dialogo = screen.getByRole('dialog', { name: 'Reactivar suscripción' })
    expect(within(dialogo).getByLabelText('Motivo (opcional)')).not.toHaveAttribute('aria-required')
    expect(dialogo).toHaveTextContent('La fecha de vencimiento no cambia.')
    await user.click(within(dialogo).getByRole('button', { name: 'Reactivar' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(cuerpoDe(api, ruta)).toEqual({ motivo: null })
    expect(await screen.findByText('Se reactivó la suscripción de Asesor Dos.')).toBeInTheDocument()
  })
})
