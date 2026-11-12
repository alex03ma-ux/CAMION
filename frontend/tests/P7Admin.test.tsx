// P7 — Pagos del plan, historial de ventas, parámetros de contacto, panel "Inicio" y aviso de vencimiento
// (CA-P7-01 a 59; D-42, D-47, D-61 B, D-62 A, D-63 A, D-64 A). Positivos, negativos y permisos.
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { hoyColombia, validarPago } from '../src/api/pagos'
import { validarContactoPortal } from '../src/api/parametros'
import { filtrosVentasDesdeConsulta, validarFiltrosVentas } from '../src/api/ventas'
import { AdminSuscripciones } from '../src/components/AdminSuscripciones'
import { HistorialVentasVehiculo } from '../src/components/HistorialVentasVehiculo'
import { MiSuscripcion } from '../src/components/MiSuscripcion'
import { PiePortal } from '../src/components/PiePortal'
import { Privacidad } from '../src/components/Privacidad'
import { leerRuta } from '../src/rutas'
import { simularFetch, TOKEN_FICTICIO } from './helpers'
import {
  abrirApp,
  CATALOGOS_INVENTARIO,
  cuerpoDe,
  paginaDe,
  plan,
  PLAN_BASICO,
  RUTA_CATALOGOS_INV,
  RUTA_PLANES_ACTIVOS,
  RUTA_SUSCRIPCIONES,
  suscripcion,
  type Rutas,
} from './p4pDatos'

const PLAN_PRUEBA = plan(5, 'Prueba', { precioCop: 0, esPrueba: true })
const BASE: Rutas = {
  [RUTA_CATALOGOS_INV]: CATALOGOS_INVENTARIO,
  [RUTA_PLANES_ACTIVOS]: { status: 200, body: { items: [PLAN_BASICO, PLAN_PRUEBA] } },
}
const ACTIVA = suscripcion(11, 'ACTIVA')
const RUTA_ASIGNAR = '/api/admin/asesores/7/suscripcion'
const RUTA_PAGOS = '/api/admin/suscripciones/11/pagos'

function renderSuscripciones(rutas: Rutas) {
  const api = simularFetch({ ...BASE, ...rutas })
  const onSesionTerminada = vi.fn()
  render(<AdminSuscripciones token={TOKEN_FICTICIO} onSesionTerminada={onSesionTerminada} />)
  return { api, onSesionTerminada, user: userEvent.setup() }
}

function pago(id: number, extra: Record<string, unknown> = {}) {
  return {
    id,
    suscripcionId: 11,
    valorCop: 150000,
    fechaPago: '2026-11-10',
    medio: 'TRANSFERENCIA',
    referencia: 'TRX-1',
    concepto: 'ASIGNACION',
    estado: 'REGISTRADO',
    registradoEn: '2026-11-10T15:00:00Z',
    registradoPor: 'Admin',
    anuladoEn: null,
    anuladoPor: null,
    motivoAnulacion: null,
    ...extra,
  }
}

const RUTA_EFECTO_1 = '/api/admin/pagos/1/efecto-anulacion'

/** D-67: respuesta de GET /efecto-anulacion. */
function efecto(extra: Record<string, unknown> = {}) {
  return {
    status: 200,
    body: {
      pagoId: 1,
      tipo: 'SOLO_PAGO',
      suscripcionId: 11,
      nuevoEstado: 'ACTIVA',
      nuevaFinVigencia: '2026-12-30T12:00:00Z',
      nuevoPlan: null,
      quedaraVencida: false,
      suscripcionCanceladaId: null,
      bloqueadoPor: null,
      mensaje: null,
      ...extra,
    },
  }
}

const fechaLarga = (iso: string) => new Intl.DateTimeFormat('es-CO', { dateStyle: 'long' }).format(Date.parse(iso))

function venta(id: number, extra: Record<string, unknown> = {}) {
  return {
    id,
    vehiculo: { id: 15, titulo: 'Chevrolet NPR 2020', estado: 'VENDIDO' },
    asesor: { id: 7, nombre: 'Asesor Uno' },
    fechaVenta: '2026-10-26',
    precioFinalCop: 95000000,
    origen: 'PORTAL',
    observaciones: null,
    estado: 'VIGENTE',
    registradaEn: '2026-10-26T15:00:00Z',
    registradaPor: 'Asesor Uno',
    revertidaEn: null,
    revertidaPor: null,
    motivoReversion: null,
    ...extra,
  }
}

// ---------------------------------------------------------------- Pagos (D-61 B)

describe('Validación del pago en cliente (CA-P7-29)', () => {
  const valido = { valorCop: '100000', fechaPago: '2026-11-01', medio: 'TRANSFERENCIA' as const, referencia: '' }
  it('acepta un pago válido y rechaza valor, fecha futura, medio y "Otro" sin referencia', () => {
    expect(validarPago(valido, '2026-11-12')).toEqual({})
    expect(validarPago({ ...valido, valorCop: '0' }, '2026-11-12').valorCop).toBeDefined()
    expect(validarPago({ ...valido, fechaPago: '2026-11-13' }, '2026-11-12').fechaPago).toBeDefined()
    expect(validarPago({ ...valido, fechaPago: '2026-02-30' }, '2026-11-12').fechaPago).toBeDefined()
    expect(validarPago({ ...valido, medio: '' }, '2026-11-12').medio).toEqual(['Elige el medio de pago.'])
    expect(validarPago({ ...valido, medio: 'OTRO' }, '2026-11-12').referencia).toEqual([
      'Escribe la referencia: es obligatoria si el medio es Otro.',
    ])
    expect(validarPago({ ...valido, referencia: '<b>' }, '2026-11-12').referencia).toBeDefined()
    expect(validarPago({ ...valido, referencia: 'x'.repeat(101) }, '2026-11-12').referencia).toBeDefined()
    expect(validarPago({ ...valido, referencia: 'TRX\n1' }, '2026-11-12').referencia).toEqual([
      'La referencia no puede tener saltos de línea ni caracteres de control.',
    ])
  })
})

describe('Asignar plan con "Pago recibido" (CA-P7-27, 28, 29)', () => {
  it('con precio: ofrece el pago, el valor por defecto es el precio y lo envía junto con el plan', async () => {
    const { user, api } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: [paginaDe([]), paginaDe([ACTIVA])],
      [RUTA_ASIGNAR]: { status: 201, body: ACTIVA },
    })
    await screen.findByText('No hay suscripciones con estos filtros.')
    await user.selectOptions(screen.getByLabelText('Asesor aprobado'), 'Asesor Uno')
    await user.selectOptions(screen.getByLabelText('Plan activo'), 'Básico (3 meses, 10 publicaciones)')
    const grupo = screen.getByRole('group', { name: 'Pago recibido' })
    expect(within(grupo).getByLabelText('Anotar ahora el pago recibido')).toBeChecked()
    expect(within(grupo).getByLabelText('Valor pagado (COP)')).toHaveValue('150.000')
    expect(within(grupo).getByLabelText('Fecha del pago')).toHaveValue(hoyColombia())
    // Sin medio: no se envía nada.
    await user.click(screen.getByRole('button', { name: 'Asignar plan' }))
    expect(within(grupo).getByText('Elige el medio de pago.')).toBeInTheDocument()
    expect(within(grupo).getByLabelText('Medio de pago')).toHaveFocus()
    expect(api.llamadasA(RUTA_ASIGNAR)).toHaveLength(0)
    // "Otro" exige referencia.
    await user.selectOptions(within(grupo).getByLabelText('Medio de pago'), 'Otro')
    await user.click(screen.getByRole('button', { name: 'Asignar plan' }))
    expect(within(grupo).getByText('Escribe la referencia: es obligatoria si el medio es Otro.')).toBeInTheDocument()
    expect(within(grupo).getByLabelText('Referencia')).toHaveAttribute('aria-required', 'true')
    expect(api.llamadasA(RUTA_ASIGNAR)).toHaveLength(0)
    await user.selectOptions(within(grupo).getByLabelText('Medio de pago'), 'Transferencia')
    await user.type(within(grupo).getByLabelText('Referencia (opcional)'), 'TRX-123')
    await user.click(screen.getByRole('button', { name: 'Asignar plan' }))
    await waitFor(() => expect(api.llamadasA(RUTA_ASIGNAR)).toHaveLength(1))
    expect(cuerpoDe(api, RUTA_ASIGNAR)).toEqual({
      planId: 1,
      pago: { valorCop: 150000, fechaPago: hoyColombia(), medio: 'TRANSFERENCIA', referencia: 'TRX-123' },
    })
    expect(await screen.findByText('Se asignó el plan Básico a Asesor Uno. Se registró el pago.')).toBeInTheDocument()
  })

  it('plan de precio 0: no ofrece pago y envía solo { planId }', async () => {
    const { user, api } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: paginaDe([]),
      [RUTA_ASIGNAR]: { status: 201, body: ACTIVA },
    })
    await screen.findByText('No hay suscripciones con estos filtros.')
    await user.selectOptions(screen.getByLabelText('Asesor aprobado'), 'Asesor Uno')
    await user.selectOptions(screen.getByLabelText('Plan activo'), 'Prueba (3 meses, 10 publicaciones)')
    expect(screen.queryByRole('group', { name: 'Pago recibido' })).not.toBeInTheDocument()
    expect(screen.getByText('Este plan no tiene precio: no se registra un pago.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Asignar plan' }))
    await waitFor(() => expect(api.llamadasA(RUTA_ASIGNAR)).toHaveLength(1))
    expect(cuerpoDe(api, RUTA_ASIGNAR)).toEqual({ planId: 5 })
  })

  it('400 del servidor: muestra cada error junto a su campo del pago', async () => {
    const { user } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: paginaDe([]),
      [RUTA_ASIGNAR]: {
        status: 400,
        body: {
          error: 'Revisa los datos marcados.',
          errores: { fechaPago: ['Escribe la fecha del pago (AAAA-MM-DD), de hoy o anterior.'] },
        },
      },
    })
    await screen.findByText('No hay suscripciones con estos filtros.')
    await user.selectOptions(screen.getByLabelText('Asesor aprobado'), 'Asesor Uno')
    await user.selectOptions(screen.getByLabelText('Plan activo'), 'Básico (3 meses, 10 publicaciones)')
    await user.selectOptions(screen.getByLabelText('Medio de pago'), 'Efectivo')
    await user.click(screen.getByRole('button', { name: 'Asignar plan' }))
    const fecha = await screen.findByLabelText('Fecha del pago')
    await waitFor(() => expect(fecha).toHaveAttribute('aria-invalid', 'true'))
    expect(screen.getByText('Escribe la fecha del pago (AAAA-MM-DD), de hoy o anterior.')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Revisa los datos marcados.')
  })
})

describe('Renovar con pago (CA-P7-27)', () => {
  it('envía { pago } con el precio del plan actual', async () => {
    const ruta = '/api/admin/suscripciones/11/renovar'
    const { user, api } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: paginaDe([ACTIVA]),
      [ruta]: { status: 200, body: ACTIVA },
    })
    await user.click(await screen.findByRole('button', { name: 'Renovar de Asesor Uno' }))
    const dialogo = screen.getByRole('dialog', { name: 'Renovar suscripción' })
    await user.selectOptions(within(dialogo).getByLabelText('Medio de pago'), 'Consignación')
    await user.click(within(dialogo).getByRole('button', { name: 'Renovar' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(cuerpoDe(api, ruta)).toEqual({
      pago: { valorCop: 150000, fechaPago: hoyColombia(), medio: 'CONSIGNACION', referencia: null },
    })
  })
})

describe('Pagos de la suscripción (CA-P7-30, 31)', () => {
  it('lista con total, registra un pago posterior y anula con motivo', async () => {
    const anulado = pago(2, {
      valorCop: 99000,
      estado: 'ANULADO',
      anuladoPor: 'Admin',
      anuladoEn: '2026-11-11T15:00:00Z',
      motivoAnulacion: 'Valor mal escrito',
    })
    const { user, api } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: paginaDe([ACTIVA]),
      [RUTA_PAGOS]: [
        { status: 200, body: { items: [pago(1), anulado], totalPagadoCop: 150000 } },
        { status: 201, body: pago(3, { concepto: 'POSTERIOR' }) },
        { status: 200, body: { items: [pago(1), anulado], totalPagadoCop: 150000 } },
      ],
      '/api/admin/pagos/1/anular': { status: 200, body: pago(1, { estado: 'ANULADO' }) },
      [RUTA_EFECTO_1]: efecto(),
    })
    const boton = await screen.findByRole('button', { name: 'Pagos de Asesor Uno' })
    await user.click(boton)
    expect(boton).toHaveAttribute('aria-expanded', 'true')
    const seccion = await screen.findByRole('region', { name: /Pagos de Asesor Uno/ })
    expect(await within(seccion).findByText('Total pagado: $ 150.000')).toBeInTheDocument()
    expect(within(seccion).getByText('Anulado')).toBeInTheDocument()
    expect(within(seccion).getByText(/Motivo: Valor mal escrito/)).toBeInTheDocument()

    // Pago posterior.
    await user.click(within(seccion).getByRole('button', { name: 'Registrar pago' }))
    expect(within(seccion).getByLabelText('Valor pagado (COP)')).toHaveFocus()
    await user.selectOptions(within(seccion).getByLabelText('Medio de pago'), 'Efectivo')
    await user.click(within(seccion).getByRole('button', { name: 'Guardar pago' }))
    await waitFor(() => expect(api.llamadas.filter((l) => l.url === RUTA_PAGOS && l.init?.method === 'POST')).toHaveLength(1))
    const post = api.llamadas.find((l) => l.url === RUTA_PAGOS && l.init?.method === 'POST')
    expect(JSON.parse(String(post?.init?.body))).toEqual({
      valorCop: 150000,
      fechaPago: hoyColombia(),
      medio: 'EFECTIVO',
      referencia: null,
    })
    expect(await within(seccion).findByText('Se registró el pago.')).toBeInTheDocument()

    // Anular: motivo obligatorio en el diálogo existente.
    await user.click(within(seccion).getByRole('button', { name: /^Anular el pago de \$ 150\.000/ }))
    const dialogo = await screen.findByRole('dialog', { name: 'Anular pago' })
    await user.click(within(dialogo).getByRole('button', { name: 'Anular pago' }))
    expect(within(dialogo).getByText('Escribe el motivo (5 a 500 caracteres).')).toBeInTheDocument()
    expect(api.llamadasA('/api/admin/pagos/1/anular')).toHaveLength(0)
    await user.type(within(dialogo).getByLabelText('Motivo'), 'Pago duplicado')
    await user.click(within(dialogo).getByRole('button', { name: 'Anular pago' }))
    await waitFor(() => expect(api.llamadasA('/api/admin/pagos/1/anular')).toHaveLength(1))
    expect(cuerpoDe(api, '/api/admin/pagos/1/anular')).toEqual({ motivo: 'Pago duplicado' })
    expect(await screen.findByText(/^Se anuló el pago\. Queda en la lista como "Anulado"\./)).toBeInTheDocument()
  })

  it('409 al anular un pago ya anulado: cierra el diálogo y muestra el mensaje', async () => {
    const { user } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: paginaDe([ACTIVA]),
      [RUTA_PAGOS]: { status: 200, body: { items: [pago(1)], totalPagadoCop: 150000 } },
      '/api/admin/pagos/1/anular': { status: 409, body: { error: 'Este pago ya está anulado.' } },
      [RUTA_EFECTO_1]: efecto(),
    })
    await user.click(await screen.findByRole('button', { name: 'Pagos de Asesor Uno' }))
    const seccion = await screen.findByRole('region', { name: /Pagos de Asesor Uno/ })
    await user.click(await within(seccion).findByRole('button', { name: /^Anular el pago/ }))
    const dialogo = await screen.findByRole('dialog', { name: 'Anular pago' })
    await user.type(within(dialogo).getByLabelText('Motivo'), 'Error de digitación')
    await user.click(within(dialogo).getByRole('button', { name: 'Anular pago' }))
    expect(await screen.findByText('Este pago ya está anulado.')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('D-67: tras un 409 el pago queda sin "Anular" y con el motivo', async () => {
    const { user } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: paginaDe([ACTIVA]),
      [RUTA_PAGOS]: { status: 200, body: { items: [pago(1)], totalPagadoCop: 150000 } },
      [RUTA_EFECTO_1]: efecto({ tipo: 'DESHACER_RENOVACION' }),
      '/api/admin/pagos/1/anular': {
        status: 409,
        body: { error: 'Después de este pago hubo otra asignación, renovación o cambio de plan en esta suscripción. Anula primero el pago de ese movimiento.' },
      },
    })
    await user.click(await screen.findByRole('button', { name: 'Pagos de Asesor Uno' }))
    const seccion = await screen.findByRole('region', { name: /Pagos de Asesor Uno/ })
    await user.click(await within(seccion).findByRole('button', { name: /^Anular el pago/ }))
    const dialogo = await screen.findByRole('dialog', { name: 'Anular pago' })
    await user.type(within(dialogo).getByLabelText('Motivo'), 'Error de digitación')
    await user.click(within(dialogo).getByRole('button', { name: 'Anular pago' }))
    expect(await within(seccion).findByRole('alert')).toHaveTextContent(/Anula primero el pago de ese movimiento\./)
    expect(within(seccion).queryByRole('button', { name: /^Anular el pago/ })).not.toBeInTheDocument()
    expect(within(seccion).getByText(/^No se puede anular:/)).toBeInTheDocument()
  })

  it('401 al cargar los pagos termina la sesión', async () => {
    const { user, onSesionTerminada } = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: paginaDe([ACTIVA]),
      [RUTA_PAGOS]: { status: 401 },
    })
    await user.click(await screen.findByRole('button', { name: 'Pagos de Asesor Uno' }))
    await waitFor(() => expect(onSesionTerminada).toHaveBeenCalled())
  })
})

describe('D-67: anular un pago deshace lo que dio', () => {
  async function abrirDialogo(respuestaEfecto: ReturnType<typeof efecto>, extra: Rutas = {}) {
    const ctx = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: paginaDe([ACTIVA]),
      [RUTA_PAGOS]: { status: 200, body: { items: [pago(1)], totalPagadoCop: 150000 } },
      [RUTA_EFECTO_1]: respuestaEfecto,
      ...extra,
    })
    await ctx.user.click(await screen.findByRole('button', { name: 'Pagos de Asesor Uno' }))
    const seccion = await screen.findByRole('region', { name: /Pagos de Asesor Uno/ })
    await ctx.user.click(await within(seccion).findByRole('button', { name: /^Anular el pago/ }))
    return { ...ctx, seccion }
  }

  it.each([
    ['DESHACER_RENOVACION', {}, `La suscripción volverá a vencer el ${fechaLarga('2026-12-30T12:00:00Z')}.`],
    ['CANCELAR_ASIGNACION', { nuevoEstado: 'CANCELADA' }, 'La suscripción quedará cancelada.'],
    [
      'DESHACER_CAMBIO_PLAN',
      { nuevoPlan: { id: 1, nombre: 'Básico' }, suscripcionId: 10, suscripcionCanceladaId: 11 },
      `La suscripción volverá al plan Básico, con vencimiento el ${fechaLarga('2026-12-30T12:00:00Z')}.`,
    ],
    ['SOLO_PAGO', {}, 'Solo se anula el pago; la fecha no cambia.'],
  ])('%s: el diálogo dice qué pasará', async (tipo, extra, texto) => {
    const { api } = await abrirDialogo(efecto({ tipo, ...extra }))
    const dialogo = await screen.findByRole('dialog', { name: 'Anular pago' })
    expect(dialogo).toHaveTextContent(texto)
    expect(within(dialogo).queryByText(/Quedará vencida/)).not.toBeInTheDocument()
    expect(api.llamadasA(RUTA_EFECTO_1)).toHaveLength(1)
    expect(api.llamadasA('/api/admin/pagos/1/anular')).toHaveLength(0)
  })

  it('quedaraVencida: aviso destacado en el diálogo', async () => {
    await abrirDialogo(efecto({ tipo: 'DESHACER_RENOVACION', quedaraVencida: true, nuevaFinVigencia: '2026-11-01T12:00:00Z' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Anular pago' })
    expect(within(dialogo).getByRole('alert')).toHaveTextContent(
      'Quedará vencida y sus publicaciones dejarán de verse en el portal.',
    )
    expect(within(dialogo).getByLabelText('Motivo')).toHaveFocus()
  })

  it.each([['MOVIMIENTO_POSTERIOR'], ['SIN_MOVIMIENTO'], ['SIN_DATOS'], ['YA_ANULADO']])(
    'bloqueadoPor %s: no abre el diálogo, muestra el mensaje y quita "Anular"',
    async (bloqueo) => {
      const mensaje = `Mensaje del servidor para ${bloqueo}.`
      const { seccion, api } = await abrirDialogo(efecto({ bloqueadoPor: bloqueo, mensaje }))
      expect(await within(seccion).findByRole('alert')).toHaveTextContent(mensaje)
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
      expect(within(seccion).queryByRole('button', { name: /^Anular el pago/ })).not.toBeInTheDocument()
      expect(within(seccion).getByText(`No se puede anular: ${mensaje}`)).toBeInTheDocument()
      expect(api.llamadasA('/api/admin/pagos/1/anular')).toHaveLength(0)
    },
  )

  it('tras anular recarga la lista de suscripciones con la nueva fecha', async () => {
    const restaurada = { ...ACTIVA, finVigencia: '2026-12-01T12:00:00Z', diasRestantes: 19 }
    const ctx = renderSuscripciones({
      [RUTA_SUSCRIPCIONES]: [paginaDe([ACTIVA]), paginaDe([restaurada])],
      [RUTA_PAGOS]: [
        { status: 200, body: { items: [pago(1)], totalPagadoCop: 150000 } },
        { status: 200, body: { items: [pago(1, { estado: 'ANULADO' })], totalPagadoCop: 0 } },
      ],
      [RUTA_EFECTO_1]: efecto({ tipo: 'DESHACER_RENOVACION', nuevaFinVigencia: '2026-12-01T12:00:00Z' }),
      '/api/admin/pagos/1/anular': { status: 200, body: pago(1, { estado: 'ANULADO' }) },
    })
    await ctx.user.click(await screen.findByRole('button', { name: 'Pagos de Asesor Uno' }))
    const seccion = await screen.findByRole('region', { name: /Pagos de Asesor Uno/ })
    await ctx.user.click(await within(seccion).findByRole('button', { name: /^Anular el pago/ }))
    const dialogo = await screen.findByRole('dialog', { name: 'Anular pago' })
    await ctx.user.type(within(dialogo).getByLabelText('Motivo'), 'Renovación por error')
    await ctx.user.click(within(dialogo).getByRole('button', { name: 'Anular pago' }))
    await waitFor(() => expect(ctx.api.llamadasA(RUTA_SUSCRIPCIONES)).toHaveLength(2))
    expect(await screen.findByText('19 días')).toBeInTheDocument()
    expect(await within(seccion).findByText('Total pagado: $ 0')).toBeInTheDocument()
    expect(within(seccion).getByText(/La suscripción volverá a vencer el/)).toBeInTheDocument()
  })

  it('SOLO_PAGO: no recarga la lista de suscripciones', async () => {
    const ctx = await abrirDialogo(efecto(), {
      '/api/admin/pagos/1/anular': { status: 200, body: pago(1, { estado: 'ANULADO' }) },
    })
    const dialogo = await screen.findByRole('dialog', { name: 'Anular pago' })
    await ctx.user.type(within(dialogo).getByLabelText('Motivo'), 'Pago duplicado')
    await ctx.user.click(within(dialogo).getByRole('button', { name: 'Anular pago' }))
    await waitFor(() => expect(ctx.api.llamadasA('/api/admin/pagos/1/anular')).toHaveLength(1))
    expect(await screen.findByText(/Solo se anula el pago; la fecha no cambia\./)).toBeInTheDocument()
    expect(ctx.api.llamadasA(RUTA_SUSCRIPCIONES)).toHaveLength(1)
  })
})

// ---------------------------------------------------------------- Ventas (D-62 A)

const RUTA_VENTAS = '/api/ventas?pagina=1&tamano=20'

describe('Filtros de ventas', () => {
  it('valida fechas y rango; la consulta del hash ignora valores desconocidos', () => {
    expect(validarFiltrosVentas({ desde: '2026-11-10', hasta: '2026-11-01' }).hasta).toEqual([
      'La fecha inicial no puede ser posterior a la final.',
    ])
    expect(validarFiltrosVentas({ desde: '2026-13-01', hasta: '' }).desde).toEqual(['Escribe una fecha válida (aaaa-mm-dd).'])
    const f = filtrosVentasDesdeConsulta('estado=vigente&desde=2026-10-14&origen=XX&asesorId=abc')
    expect(f).toMatchObject({ estado: 'VIGENTE', desde: '2026-10-14', origen: '', asesorId: '' })
  })
})

describe('Pantalla de ventas', () => {
  it('administrador (#/cuenta/ventas): todas las ventas, revertidas con motivo y enlace al vehículo', async () => {
    abrirApp('#/cuenta/ventas', ['ADMINISTRADOR'], {
      [RUTA_CATALOGOS_INV]: CATALOGOS_INVENTARIO,
      [RUTA_VENTAS]: paginaDe([
        venta(2),
        venta(1, {
          estado: 'REVERTIDA',
          revertidaEn: '2026-10-25T15:00:00Z',
          revertidaPor: 'Admin',
          motivoReversion: 'No se cerró el negocio',
        }),
      ]),
    })
    expect(await screen.findByRole('heading', { level: 2, name: 'Ventas' })).toBeInTheDocument()
    const nav = screen.getByRole('navigation', { name: 'Administración' })
    expect(within(nav).getByRole('link', { name: 'Ventas' })).toHaveAttribute('aria-current', 'page')
    expect(await screen.findByText(/Vigente$/, { selector: 'span.estado' })).toBeInTheDocument()
    expect(screen.getByText(/Revertida$/, { selector: 'span.estado' })).toBeInTheDocument()
    expect(screen.getByText('No se cerró el negocio')).toBeInTheDocument()
    expect(screen.getAllByText('$ 95.000.000')).toHaveLength(2)
    expect(screen.getAllByText('Portal web', { selector: 'dd' })).toHaveLength(2)
    expect(screen.getAllByRole('link', { name: /Ver vehículo/ })[0]).toHaveAttribute('href', '#/inventario/15')
    expect(screen.getByLabelText('Asesor')).toBeInTheDocument()
  })

  it('rango de fechas inválido no consulta; sin resultados muestra "Limpiar filtros"', async () => {
    const filtrada = '/api/ventas?estado=REVERTIDA&pagina=1&tamano=20'
    const { user, api } = abrirApp('#/cuenta/ventas', ['ADMINISTRADOR'], {
      [RUTA_CATALOGOS_INV]: CATALOGOS_INVENTARIO,
      [RUTA_VENTAS]: paginaDe([venta(1)]),
      [filtrada]: paginaDe([]),
    })
    await screen.findByText(/Vigente$/, { selector: 'span.estado' })
    await user.type(screen.getByLabelText('Vendida desde'), '2026-11-10')
    await user.type(screen.getByLabelText('Vendida hasta'), '2026-11-01')
    const antes = api.llamadas.length
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(screen.getByText('La fecha inicial no puede ser posterior a la final.')).toBeInTheDocument()
    expect(api.llamadas.length).toBe(antes)
    await user.clear(screen.getByLabelText('Vendida desde'))
    await user.clear(screen.getByLabelText('Vendida hasta'))
    await user.selectOptions(screen.getByLabelText('Estado'), 'Revertida')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(await screen.findByText('No hay ventas con estos filtros.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Limpiar filtros' }))
    await waitFor(() => expect(api.llamadasA(RUTA_VENTAS)).toHaveLength(2))
  })

  it('400 del servidor: el mensaje va junto al filtro', async () => {
    abrirApp('#/cuenta/ventas?estado=VIGENTE', ['ADMINISTRADOR'], {
      [RUTA_CATALOGOS_INV]: CATALOGOS_INVENTARIO,
      '/api/ventas?estado=VIGENTE&pagina=1&tamano=20': {
        status: 400,
        body: { error: 'Revisa los filtros.', errores: { estado: ['Estado no válido.'] } },
      },
    })
    expect(await screen.findByText('Estado no válido.')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Revisa los filtros.')
  })

  it('asesor (#/ventas): "Mis ventas", sin filtro de asesor y nunca envía asesorId', async () => {
    const { api } = abrirApp('#/ventas?asesorId=8', ['ASESOR'], {
      [RUTA_VENTAS]: paginaDe([{ ...venta(1), asesor: null }]),
    })
    expect(await screen.findByText(/Vigente$/, { selector: 'span.estado' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Mis ventas' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Asesor')).not.toBeInTheDocument()
    expect(api.llamadas.some((l) => l.url.includes('asesorId'))).toBe(false)
  })

  it('comprador en #/ventas: "No tienes permiso" y no llama a /api/ventas', async () => {
    const { api } = abrirApp('#/ventas', ['COMPRADOR'])
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
    expect(api.llamadas.some((l) => l.url.startsWith('/api/ventas'))).toBe(false)
  })

  it('asesor en #/cuenta/ventas (sección del administrador): sin permiso y sin llamadas', async () => {
    const { api } = abrirApp('#/cuenta/ventas', ['ASESOR'])
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
    expect(api.llamadas.some((l) => l.url.startsWith('/api/ventas'))).toBe(false)
  })
})

describe('Historial de ventas del vehículo (CA-P7-06, 07, 09)', () => {
  const ruta = '/api/inventario/vehiculos/15/ventas'
  function renderHistorial(respuesta: Rutas[string], estado = 'VENDIDO') {
    simularFetch({ [ruta]: respuesta })
    render(
      <HistorialVentasVehiculo token={TOKEN_FICTICIO} vehiculoId={15} estadoVehiculo={estado} esAdmin onSesionTerminada={vi.fn()} />,
    )
  }

  it('vigente primero y revertida con su motivo', async () => {
    renderHistorial({
      status: 200,
      body: { items: [venta(2), venta(1, { estado: 'REVERTIDA', motivoReversion: 'Se cayó el crédito' })] },
    })
    const items = await screen.findAllByRole('listitem')
    expect(within(items[0]).getByText(/Vigente$/, { selector: 'span.estado' })).toBeInTheDocument()
    expect(within(items[1]).getByText(/Revertida$/, { selector: 'span.estado' })).toBeInTheDocument()
    expect(within(items[1]).getByText('Se cayó el crédito')).toBeInTheDocument()
  })

  it('VENDIDO sin filas: texto de CA-P7-09', async () => {
    renderHistorial({ status: 200, body: { items: [] } })
    expect(
      await screen.findByText('Este vehículo figura como vendido, pero no tiene datos de venta registrados.'),
    ).toBeInTheDocument()
  })

  it('404 de un vehículo ajeno: "Vehículo no encontrado."', async () => {
    renderHistorial({ status: 404, body: { error: 'Vehículo no encontrado.' } }, 'ACTIVO')
    expect(await screen.findByText('Vehículo no encontrado.')).toBeInTheDocument()
  })
})

// ---------------------------------------------------------------- Parámetros y pie (D-63 A, D-42)

const RUTA_PARAMETROS = '/api/admin/parametros/contacto'
const CONTACTO_ADMIN = {
  whatsapp: '3166152907',
  whatsappEnlace: 'https://wa.me/573166152907',
  correo: 'vc.automotores@gmail.com',
  horario: ['Lunes a viernes: 9:00 a. m. a 5:00 p. m.', 'Sábados: 9:00 a. m. a 3:00 p. m.'],
  instagram: null,
  tiktok: null,
  configurado: false,
  actualizadoEn: null,
  actualizadoPor: null,
}

describe('Validación de los parámetros (CA-P7-37, 41)', () => {
  const ok = { whatsapp: '316 615 2907', correo: 'a@b.co', horario: ['Lunes'], instagram: '', tiktok: '' }
  it('acepta lo válido y rechaza celular, correo, horario y enlaces no permitidos', () => {
    expect(validarContactoPortal(ok)).toEqual({})
    expect(validarContactoPortal({ ...ok, whatsapp: '+57 316 615 2907' })).toEqual({})
    expect(validarContactoPortal({ ...ok, whatsapp: '601 234 5678' }).whatsapp).toBeDefined()
    expect(validarContactoPortal({ ...ok, correo: 'sin-arroba' }).correo).toBeDefined()
    // B-2: misma regla estricta que el servidor.
    for (const malo of ['a?b@x.co', 'a&b@x.co', 'a;b@x.co', 'a,b@x.co', 'a:b@x.co', 'a/b@x.co', 'a<b@x.co',
      'a"b@x.co', "a'b@x.co", 'a%b@x.co', 'a b@x.co', 'a..b@x.co', 'a@x', 'a@x.c', '.a@x.co']) {
      expect(validarContactoPortal({ ...ok, correo: malo }).correo).toEqual(['Escribe un correo válido.'])
    }
    expect(validarContactoPortal({ ...ok, correo: 'vc.automotores+ventas@gmail.com' })).toEqual({})
    expect(validarContactoPortal({ ...ok, horario: ['Lunes\tcerrado'] }).horario).toBeDefined()
    expect(validarContactoPortal({ ...ok, horario: [''] }).horario).toBeDefined()
    expect(validarContactoPortal({ ...ok, horario: ['a', 'b', 'c', 'd', 'e'] }).horario).toBeDefined()
    expect(validarContactoPortal({ ...ok, horario: ['<b>x</b>'] }).horario).toBeDefined()
    expect(validarContactoPortal({ ...ok, instagram: 'javascript:alert(1)' }).instagram).toBeDefined()
    expect(validarContactoPortal({ ...ok, instagram: 'https://evil.com/x' }).instagram).toBeDefined()
    expect(validarContactoPortal({ ...ok, tiktok: 'https://www.tiktok.com/@cuenta' })).toEqual({})
  })
})

describe('Pantalla Parámetros', () => {
  it('muestra los valores, valida en cliente, guarda con PUT y no ofrece valores de negocio', async () => {
    const guardado = { ...CONTACTO_ADMIN, whatsapp: '3001234567', configurado: true, actualizadoPor: 'Admin', actualizadoEn: '2026-11-12T15:00:00Z' }
    const { user, api } = abrirApp('#/cuenta/parametros', ['ADMINISTRADOR'], {
      [RUTA_PARAMETROS]: [{ status: 200, body: CONTACTO_ADMIN }, { status: 200, body: guardado }],
    })
    const whatsapp = await screen.findByLabelText('WhatsApp del portal')
    expect(whatsapp).toHaveValue('316 615 2907')
    expect(screen.getByLabelText('Línea 1')).toHaveValue('Lunes a viernes: 9:00 a. m. a 5:00 p. m.')
    expect(screen.getByText(/valores por defecto/)).toBeInTheDocument()
    expect(screen.queryByLabelText(/días|fotos/i)).not.toBeInTheDocument()

    await user.clear(whatsapp)
    await user.type(whatsapp, '12345')
    await user.click(screen.getByRole('button', { name: 'Guardar cambios' }))
    expect(screen.getByText('Escribe un celular colombiano de 10 dígitos, por ejemplo 316 615 2907.')).toBeInTheDocument()
    expect(whatsapp).toHaveFocus()
    expect(api.llamadas.filter((l) => l.init?.method === 'PUT')).toHaveLength(0)

    await user.clear(whatsapp)
    await user.type(whatsapp, '300 123 4567')
    await user.click(screen.getByRole('button', { name: 'Añadir línea' }))
    await user.type(screen.getByLabelText('Línea 3'), 'Domingos: cerrado')
    await user.click(screen.getByRole('button', { name: 'Guardar cambios' }))
    await waitFor(() => expect(api.llamadas.filter((l) => l.init?.method === 'PUT')).toHaveLength(1))
    expect(cuerpoDe(api, RUTA_PARAMETROS, 1)).toEqual({
      whatsapp: '300 123 4567',
      correo: 'vc.automotores@gmail.com',
      horario: [...CONTACTO_ADMIN.horario, 'Domingos: cerrado'],
      instagram: '',
      tiktok: '',
    })
    expect(await screen.findByText(/Se guardaron los datos de contacto/)).toBeInTheDocument()
    expect(screen.getByLabelText('WhatsApp del portal')).toHaveValue('300 123 4567')
  })

  it('400 del servidor: errores por campo', async () => {
    const { user } = abrirApp('#/cuenta/parametros', ['ADMINISTRADOR'], {
      [RUTA_PARAMETROS]: [
        { status: 200, body: CONTACTO_ADMIN },
        {
          status: 400,
          body: { error: 'Revisa los datos marcados.', errores: { correo: ['Escribe un correo válido.'] } },
        },
      ],
    })
    await screen.findByLabelText('WhatsApp del portal')
    await user.click(screen.getByRole('button', { name: 'Guardar cambios' }))
    expect(await screen.findByText('Escribe un correo válido.')).toBeInTheDocument()
    expect(screen.getByLabelText('Correo de contacto')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByRole('alert')).toHaveTextContent('Revisa los datos marcados.')
  })

  it('un asesor en #/cuenta/parametros ve "No tienes permiso" y no llama a /api/admin', async () => {
    const { api } = abrirApp('#/cuenta/parametros', ['ASESOR'])
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
    expect(api.llamadas.some((l) => l.url.startsWith('/api/admin/'))).toBe(false)
  })
})

describe('Pie del portal (CA-P7-36, 38, 39, 41)', () => {
  it('usa los valores del servidor y muestra Instagram y TikTok solo con enlace válido', async () => {
    const api = simularFetch({
      '/api/portal/contacto': {
        status: 200,
        body: {
          ...CONTACTO_ADMIN,
          whatsapp: '3001234567',
          whatsappEnlace: 'https://wa.me/573001234567',
          correo: 'ventas@ejemplo.test',
          horario: ['Todos los días'],
          instagram: 'https://www.instagram.com/cuenta',
          tiktok: 'javascript:alert(1)',
        },
      },
    })
    render(<PiePortal tipos={[]} conSesion={false} />)
    const whatsapp = await screen.findByRole('link', { name: 'WhatsApp 300 123 4567' })
    expect(whatsapp).toHaveAttribute('href', 'https://wa.me/573001234567')
    expect(screen.getByRole('link', { name: 'ventas@ejemplo.test' })).toHaveAttribute('href', 'mailto:ventas@ejemplo.test')
    expect(screen.getByText('Todos los días')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Instagram/ })).toHaveAttribute('href', 'https://www.instagram.com/cuenta')
    expect(screen.queryByRole('link', { name: /TikTok/ })).not.toBeInTheDocument()
    expect(api.llamadasA('/api/portal/contacto')[0].init?.headers).not.toHaveProperty('Authorization')
  })

  it('si la lectura falla, usa los valores fijos y no muestra redes', async () => {
    simularFetch({ '/api/portal/contacto': { red: true } })
    render(<PiePortal tipos={[]} conSesion={false} />)
    expect(await screen.findByRole('link', { name: 'WhatsApp 316 615 2907' })).toHaveAttribute(
      'href',
      'https://wa.me/573166152907',
    )
    expect(screen.getByText('Sábados: 9:00 a. m. a 3:00 p. m.')).toBeInTheDocument()
    expect(screen.queryByRole('list', { name: 'Redes sociales' })).not.toBeInTheDocument()
  })
})

describe('Privacidad lee el contacto del portal', () => {
  it('usa los valores del servidor', async () => {
    simularFetch({
      '/api/portal/contacto': {
        status: 200,
        body: { ...CONTACTO_ADMIN, whatsapp: '3001234567', whatsappEnlace: 'https://wa.me/573001234567', correo: 'datos@ejemplo.test' },
      },
    })
    render(<Privacidad />)
    expect(await screen.findByRole('link', { name: 'datos@ejemplo.test' })).toHaveAttribute('href', 'mailto:datos@ejemplo.test')
    expect(screen.getByRole('link', { name: /300 123 4567/ })).toHaveAttribute('href', 'https://wa.me/573001234567')
  })

  it('si la lectura falla, usa el respaldo de D-42', async () => {
    simularFetch({ '/api/portal/contacto': { red: true } })
    render(<Privacidad />)
    expect(await screen.findByRole('link', { name: /316 615 2907/ })).toHaveAttribute('href', 'https://wa.me/573166152907')
    expect(screen.getByRole('link', { name: 'vc.automotores@gmail.com' })).toBeInTheDocument()
  })
})

// ---------------------------------------------------------------- Panel "Inicio" (D-64 A)

const PANEL = {
  asesoresPendientes: 2,
  solicitudes: [
    { origen: 'CONTACTO_VEHICULO', abiertas: 3, nuevas: 1 },
    { origen: 'VENDE_TU_CAMION', abiertas: 1, nuevas: 1 },
    { origen: 'SEGURO_TODO_RIESGO', abiertas: 0, nuevas: 0 },
    { origen: 'CREDITO', abiertas: 0, nuevas: 0 },
    { origen: 'BUSQUEDA', abiertas: 0, nuevas: 0 },
  ],
  suscripcionesPorVencer: 1,
  suscripcionesVencidas: 0,
  publicacionesVisibles: 4,
  vehiculosPorEstado: { ACTIVO: 5, RESERVADO: 1, INACTIVO: 0, VENDIDO: 2 },
  ventasUltimos30Dias: 1,
  ventasDesde: '2026-10-14',
}

describe('Panel Inicio del administrador (CA-P7-45 a 49, 51)', () => {
  it('contadores con enlace a la lista filtrada, ceros en palabras y sin dinero', async () => {
    abrirApp('#/cuenta/inicio', ['ADMINISTRADOR'], { '/api/admin/panel': { status: 200, body: PANEL } })
    expect(await screen.findByRole('heading', { level: 2, name: 'Inicio' })).toBeInTheDocument()
    const nav = screen.getByRole('navigation', { name: 'Administración' })
    const enlaces = within(nav).getAllByRole('link').map((a) => a.textContent)
    expect(enlaces).toEqual([
      'Inicio',
      'Usuarios',
      'Planes',
      'Suscripciones',
      'Ventas',
      'Inventario',
      'Publicaciones',
      'Contactos',
      'Catálogos',
      'Portada',
      'Parámetros',
      'Mi perfil',
    ])
    expect(within(nav).getByRole('link', { name: 'Inicio' })).toHaveAttribute('aria-current', 'page')

    const asesores = await screen.findByRole('link', { name: /Asesores pendientes de aprobación/ })
    expect(asesores).toHaveAttribute('href', '#/cuenta/usuarios?rol=ASESOR&estado=PENDIENTE')
    expect(asesores).toHaveTextContent('2')
    expect(screen.getByRole('link', { name: /Vende tu camión: abiertas/ })).toHaveAttribute(
      'href',
      '#/contactos?estado=ABIERTO&responsable=ADMINISTRACION&origen=VENDE_TU_CAMION',
    )
    // O-5: los dos contadores de suscripciones abren la lista con la misma consulta del panel.
    expect(screen.getByRole('link', { name: /Suscripciones vencidas/ })).toHaveAttribute(
      'href',
      '#/cuenta/suscripciones?vista=vencidasSinRenovar',
    )
    expect(screen.getByRole('link', { name: /vencen en los próximos 7 días/ })).toHaveAttribute(
      'href',
      '#/cuenta/suscripciones?vista=porVencer',
    )
    expect(screen.getByRole('link', { name: /Vehículos en estado Activo/ })).toHaveAttribute('href', '#/inventario?estado=ACTIVO')
    expect(screen.getByRole('link', { name: /Ventas vigentes/ })).toHaveAttribute(
      'href',
      '#/cuenta/ventas?estado=VIGENTE&desde=2026-10-14',
    )
    expect(screen.getByText('Sin suscripciones vencidas')).toBeInTheDocument()
    expect(screen.getAllByText('Sin solicitudes abiertas')).toHaveLength(3)
    expect(screen.queryByText(/\$/)).not.toBeInTheDocument()
  })

  it('el enlace de asesores pendientes abre Usuarios ya filtrado', async () => {
    const filtrada = '/api/admin/usuarios?estado=PENDIENTE&rol=ASESOR&pagina=1&tamano=20'
    const { user, api } = abrirApp('#/cuenta/inicio', ['ADMINISTRADOR'], {
      '/api/admin/panel': { status: 200, body: PANEL },
      [filtrada]: paginaDe([]),
    })
    await user.click(await screen.findByRole('link', { name: /Asesores pendientes/ }))
    await waitFor(() => expect(api.llamadasA(filtrada)).toHaveLength(1))
    expect(screen.getByLabelText('Rol')).toHaveValue('ASESOR')
  })

  it('O-5: "vencidas sin renovar" abre Suscripciones con vista, la muestra y permite quitarla', async () => {
    const filtrada = '/api/admin/suscripciones?vista=vencidasSinRenovar&pagina=1&tamano=20'
    const { user, api } = abrirApp('#/cuenta/inicio', ['ADMINISTRADOR'], {
      '/api/admin/panel': { status: 200, body: PANEL },
      ...BASE,
      [filtrada]: paginaDe([]),
      [RUTA_SUSCRIPCIONES]: paginaDe([ACTIVA]),
    })
    await user.click(await screen.findByRole('link', { name: /Suscripciones vencidas/ }))
    await waitFor(() => expect(api.llamadasA(filtrada)).toHaveLength(1))
    expect(await screen.findByText('Vencidas sin renovar')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Quitar este filtro' }))
    await waitFor(() => expect(window.location.hash).toBe('#/cuenta/suscripciones'))
    await waitFor(() => expect(api.llamadasA(RUTA_SUSCRIPCIONES)).toHaveLength(1))
    expect(screen.queryByRole('button', { name: 'Quitar este filtro' })).not.toBeInTheDocument()
  })

  it('O-5: la vista se combina con el filtro de estado y un 400 muestra "Vista no válida."', async () => {
    const conEstado = '/api/admin/suscripciones?estado=ACTIVA&vista=porVencer&pagina=1&tamano=20'
    const { user, api } = abrirApp('#/cuenta/suscripciones?vista=porvencer', ['ADMINISTRADOR'], {
      ...BASE,
      '/api/admin/suscripciones?vista=porVencer&pagina=1&tamano=20': paginaDe([ACTIVA]),
      [conEstado]: { status: 400, body: { error: 'Revisa los filtros.', errores: { vista: ['Vista no válida.'] } } },
    })
    expect(await screen.findByText('Vencen en los próximos 7 días')).toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText('Estado'), 'Activa')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => expect(api.llamadasA(conEstado)).toHaveLength(1))
    expect(await screen.findByText('Revisa los filtros. Vista no válida.')).toBeInTheDocument()
  })

  it.each([['ASESOR'], ['COMPRADOR']])('%s en #/cuenta/inicio: sin permiso, sin llamada y sin enlace en su perfil', async (rol) => {
    const { api } = abrirApp('#/cuenta/inicio', [rol])
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
    expect(api.llamadas.some((l) => l.url === '/api/admin/panel')).toBe(false)
  })

  it('error del servidor: mensaje y Reintentar', async () => {
    const { user, api } = abrirApp('#/cuenta/inicio', ['ADMINISTRADOR'], {
      '/api/admin/panel': [{ status: 500 }, { status: 200, body: PANEL }],
    })
    expect(await screen.findByRole('alert')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByRole('link', { name: /Asesores pendientes/ })).toBeInTheDocument()
    expect(api.llamadasA('/api/admin/panel')).toHaveLength(2)
  })
})

// ---------------------------------------------------------------- Mi suscripción (D-64 b A)

describe('Aviso de vencimiento en "Mi suscripción" (CA-P7-50)', () => {
  const base = {
    plan: { id: 1, nombre: 'Básico', limitePublicaciones: 10, diasVigencia: 90 },
    estado: 'ACTIVA',
    estadoEfectivo: 'ACTIVA',
    inicioVigencia: '2026-10-01T12:00:00Z',
    finVigencia: '2026-11-15T12:00:00Z',
    diasRestantes: 3,
    vigente: true,
  }

  it('muestra el mensaje del servidor cuando avisoVencimiento es true', async () => {
    simularFetch({
      '/api/asesor/suscripcion': {
        status: 200,
        body: {
          ...base,
          avisoVencimiento: true,
          mensajeVencimiento: 'Tu plan vence en 3 días. Para renovarlo, comunícate con el administrador.',
        },
      },
    })
    render(<MiSuscripcion token={TOKEN_FICTICIO} />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Tu plan vence en 3 días.')
    // O-1 (CA-P7-26): WhatsApp del portal con texto breve y sin datos personales.
    const enlace = await screen.findByRole('link', { name: /Escribir por WhatsApp para renovar/ })
    await waitFor(() => expect(enlace.getAttribute('href')).toMatch(/^https:\/\/wa\.me\/573166152907\?text=/))
    const texto = decodeURIComponent(enlace.getAttribute('href')!.split('?text=')[1])
    expect(texto).toBe('Hola, quiero renovar mi plan de asesor en Financiamos Tu Camión.')
    expect(enlace).toHaveAttribute('target', '_blank')
  })

  it('plan vencido: ofrece el enlace con el WhatsApp guardado en Parámetros', async () => {
    simularFetch({
      '/api/asesor/suscripcion': {
        status: 200,
        body: { ...base, estadoEfectivo: 'VENCIDA', vigente: false, diasRestantes: 0, avisoVencimiento: false, mensajeVencimiento: null },
      },
      '/api/portal/contacto': {
        status: 200,
        body: { ...CONTACTO_ADMIN, whatsapp: '3001234567', whatsappEnlace: 'https://wa.me/573001234567' },
      },
    })
    render(<MiSuscripcion token={TOKEN_FICTICIO} />)
    const enlace = await screen.findByRole('link', { name: /Escribir por WhatsApp para renovar/ })
    await waitFor(() => expect(enlace.getAttribute('href')).toMatch(/^https:\/\/wa\.me\/573001234567\?text=/))
  })

  it('si falla la lectura del contacto, el enlace usa el WhatsApp de respaldo', async () => {
    simularFetch({
      '/api/asesor/suscripcion': {
        status: 200,
        body: { ...base, estadoEfectivo: 'VENCIDA', vigente: false, diasRestantes: 0 },
      },
      '/api/portal/contacto': { red: true },
    })
    render(<MiSuscripcion token={TOKEN_FICTICIO} />)
    const enlace = await screen.findByRole('link', { name: /Escribir por WhatsApp para renovar/ })
    expect(enlace.getAttribute('href')).toMatch(/^https:\/\/wa\.me\/573166152907\?text=/)
  })

  it('sin aviso del servidor no muestra nada, y el asesor no ve pagos', async () => {
    const api = simularFetch({
      '/api/asesor/suscripcion': { status: 200, body: { ...base, diasRestantes: 40, avisoVencimiento: false, mensajeVencimiento: null } },
    })
    render(<MiSuscripcion token={TOKEN_FICTICIO} />)
    expect(await screen.findByText('Básico')).toBeInTheDocument()
    expect(screen.queryByText(/vence en/)).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /WhatsApp para renovar/ })).not.toBeInTheDocument()
    expect(screen.queryByText(/Pago/)).not.toBeInTheDocument()
    expect(api.llamadas.some((l) => l.url.includes('/pagos'))).toBe(false)
  })
})

// ---------------------------------------------------------------- Rutas

describe('Rutas de P7', () => {
  it('secciones nuevas, #/ventas y filtros en la consulta', () => {
    expect(leerRuta('#/cuenta/inicio')).toEqual({ nombre: 'cuenta', registro: false, seccion: 'inicio' })
    expect(leerRuta('#/cuenta/parametros')).toEqual({ nombre: 'cuenta', registro: false, seccion: 'parametros' })
    expect(leerRuta('#/cuenta/ventas?estado=VIGENTE')).toEqual({
      nombre: 'cuenta',
      registro: false,
      seccion: 'ventas',
      consulta: 'estado=VIGENTE',
    })
    expect(leerRuta('#/ventas')).toEqual({ nombre: 'ventas' })
    expect(leerRuta('#/inventario?estado=ACTIVO')).toEqual({ nombre: 'inventario', consulta: 'estado=ACTIVO' })
    expect(leerRuta('#/contactos?origen=CREDITO')).toEqual({ nombre: 'contactos', consulta: 'origen=CREDITO' })
  })
})
