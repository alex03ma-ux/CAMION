// P4p — Planes (HU-049 crear, HU-050 modificar, HU-051 activar o inactivar; D-31, D-35).
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AdminPlanes } from '../src/components/AdminPlanes'
import { cabecera, simularFetch, TOKEN_FICTICIO } from './helpers'
import { cuerpoDe, plan, PLAN_BASICO, PLAN_VIEJO, RUTA_PLANES, type Rutas } from './p4pDatos'

function renderPlanes(rutas: Rutas) {
  const api = simularFetch(rutas)
  const onSesionTerminada = vi.fn()
  render(<AdminPlanes token={TOKEN_FICTICIO} onSesionTerminada={onSesionTerminada} />)
  return { api, onSesionTerminada, user: userEvent.setup() }
}

const LISTA = { status: 200, body: { items: [PLAN_BASICO, PLAN_VIEJO] } }

async function filaDe(nombre: string) {
  const celda = await screen.findByRole('rowheader', { name: nombre })
  return celda.closest('tr') as HTMLElement
}

describe('Lista de planes', () => {
  it('muestra precio con miles, duración en meses y días, cupo, tipo, estado en texto y suscripciones vivas', async () => {
    const { api } = renderPlanes({ [RUTA_PLANES]: LISTA })
    const fila = await filaDe('Básico')
    expect(within(fila).getByText('$ 150.000')).toBeInTheDocument()
    expect(within(fila).getByText('3 meses')).toBeInTheDocument()
    expect(within(fila).getByText('90 días')).toBeInTheDocument()
    expect(within(fila).getByText('10 publicaciones')).toBeInTheDocument()
    expect(within(fila).getByText('Activo')).toBeInTheDocument()
    expect(within(fila).getByRole('button', { name: 'Inactivar el plan Básico' })).toBeInTheDocument()
    const viejo = await filaDe('Antiguo')
    expect(within(viejo).getByText('Inactivo')).toBeInTheDocument()
    expect(within(viejo).getByRole('button', { name: 'Activar el plan Antiguo' })).toBeInTheDocument()
    expect(cabecera(api.llamadasA(RUTA_PLANES)[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
  })

  it('401 al cargar: termina la sesión', async () => {
    const { onSesionTerminada } = renderPlanes({ [RUTA_PLANES]: { status: 401 } })
    await waitFor(() => expect(onSesionTerminada).toHaveBeenCalled())
  })

  it('403: muestra el mensaje de permiso', async () => {
    renderPlanes({ [RUTA_PLANES]: { status: 403 } })
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
  })
})

describe('Crear plan', () => {
  it('campos obligatorios con asterisco (D-31) y "= N días" al escribir la duración (D-35)', async () => {
    const { user } = renderPlanes({ [RUTA_PLANES]: LISTA })
    await filaDe('Básico')
    expect(screen.getByText('Campo obligatorio')).toBeInTheDocument()
    for (const etiqueta of ['Nombre', 'Precio (COP)', 'Duración en meses', 'Cupo de publicaciones']) {
      expect(screen.getByLabelText(etiqueta)).toHaveAttribute('aria-required', 'true')
    }
    expect(screen.getByLabelText('Es un plan de prueba')).not.toHaveAttribute('aria-required')
    await user.type(screen.getByLabelText('Duración en meses'), '4')
    expect(screen.getByText('= 120 días (30 días por mes).')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Duración en meses'), '2')
    expect(screen.getByText('= 1.260 días (30 días por mes).')).toBeInTheDocument()
  })

  it('validación local: no envía y marca cada campo', async () => {
    const { user, api } = renderPlanes({ [RUTA_PLANES]: LISTA })
    await filaDe('Básico')
    await user.type(screen.getByLabelText('Nombre'), 'A')
    await user.type(screen.getByLabelText('Duración en meses'), '40')
    await user.type(screen.getByLabelText('Cupo de publicaciones'), '0')
    await user.click(screen.getByRole('button', { name: 'Crear plan' }))
    expect(screen.getByText('Escribe el nombre del plan (2 a 100 caracteres).')).toBeInTheDocument()
    expect(screen.getByText('Escribe el precio en pesos (0 o más, sin decimales).')).toBeInTheDocument()
    expect(screen.getByText('Escribe la duración en meses (de 1 a 36).')).toBeInTheDocument()
    expect(screen.getByText('Escribe el cupo de publicaciones (de 1 a 1.000).')).toBeInTheDocument()
    expect(screen.getByLabelText('Nombre')).toHaveAttribute('aria-invalid', 'true')
    await waitFor(() => expect(screen.getByLabelText('Nombre')).toHaveFocus())
    expect(api.llamadas.filter((l) => l.init?.method === 'POST')).toHaveLength(0)
  })

  it('envía el cuerpo del contrato (precio sin separadores) y recarga la lista', async () => {
    const { user, api } = renderPlanes({
      [RUTA_PLANES]: [LISTA, { status: 200, body: { items: [PLAN_BASICO, PLAN_VIEJO, plan(9, 'Prueba gratis')] } }],
    })
    await filaDe('Básico')
    await user.type(screen.getByLabelText('Nombre'), '  Prueba gratis ')
    fireEvent.change(screen.getByLabelText('Precio (COP)'), { target: { value: '1.250.000' } })
    expect(screen.getByLabelText('Precio (COP)')).toHaveValue('1.250.000')
    await user.type(screen.getByLabelText('Duración en meses'), '1')
    await user.type(screen.getByLabelText('Cupo de publicaciones'), '5')
    await user.click(screen.getByLabelText('Es un plan de prueba'))
    // El POST comparte ruta con el GET: la segunda respuesta de la lista sirve también de 201.
    await user.click(screen.getByRole('button', { name: 'Crear plan' }))
    await waitFor(() => expect(api.llamadas.filter((l) => l.init?.method === 'POST')).toHaveLength(1))
    const post = api.llamadas.find((l) => l.init?.method === 'POST')
    expect(JSON.parse(String(post?.init?.body))).toEqual({
      nombre: 'Prueba gratis',
      precioCop: 1250000,
      duracionMeses: 1,
      limitePublicaciones: 5,
      esPrueba: true,
    })
    expect(await screen.findByText('Se creó el plan Prueba gratis.')).toBeInTheDocument()
    expect(await filaDe('Prueba gratis')).toBeInTheDocument()
    expect(screen.getByLabelText('Nombre')).toHaveValue('')
  })

  it('409 nombre duplicado: el mensaje queda bajo el nombre', async () => {
    const { user } = renderPlanes({
      [RUTA_PLANES]: [LISTA, { status: 409, body: { error: 'Ya existe un plan con ese nombre.' } }],
    })
    await filaDe('Básico')
    await user.type(screen.getByLabelText('Nombre'), 'básico')
    await user.type(screen.getByLabelText('Precio (COP)'), '0')
    await user.type(screen.getByLabelText('Duración en meses'), '1')
    await user.type(screen.getByLabelText('Cupo de publicaciones'), '1')
    await user.click(screen.getByRole('button', { name: 'Crear plan' }))
    const nombre = screen.getByLabelText('Nombre')
    await waitFor(() => expect(nombre).toHaveAttribute('aria-invalid', 'true'))
    expect(nombre).toHaveAccessibleDescription('Ya existe un plan con ese nombre.')
    expect(screen.getByRole('alert')).toHaveTextContent('Ya existe un plan con ese nombre.')
  })

  it('400 con errores por campo: cada mensaje junto a su campo', async () => {
    const { user } = renderPlanes({
      [RUTA_PLANES]: [
        LISTA,
        {
          status: 400,
          body: { error: 'Revisa los datos.', errores: { DuracionMeses: ['Máximo 36 meses.'] } },
        },
      ],
    })
    await filaDe('Básico')
    await user.type(screen.getByLabelText('Nombre'), 'Nuevo')
    await user.type(screen.getByLabelText('Precio (COP)'), '10')
    await user.type(screen.getByLabelText('Duración en meses'), '12')
    await user.type(screen.getByLabelText('Cupo de publicaciones'), '3')
    await user.click(screen.getByRole('button', { name: 'Crear plan' }))
    expect(await screen.findByText('Máximo 36 meses.')).toBeInTheDocument()
    expect(screen.getByLabelText('Duración en meses')).toHaveAttribute('aria-invalid', 'true')
  })
})

describe('Editar y activar/inactivar', () => {
  it('Editar carga el plan y envía PUT a /api/admin/planes/{id}', async () => {
    const ruta = `${RUTA_PLANES}/1`
    const { user, api } = renderPlanes({
      [RUTA_PLANES]: LISTA,
      [ruta]: { status: 200, body: { ...PLAN_BASICO, limitePublicaciones: 12 } },
    })
    const fila = await filaDe('Básico')
    await user.click(within(fila).getByRole('button', { name: 'Editar el plan Básico' }))
    expect(screen.getByRole('heading', { name: 'Editar plan Básico' })).toHaveFocus()
    expect(screen.getByLabelText('Precio (COP)')).toHaveValue('150.000')
    expect(screen.getByText(/no modifican las suscripciones en curso/)).toBeInTheDocument()
    await user.clear(screen.getByLabelText('Cupo de publicaciones'))
    await user.type(screen.getByLabelText('Cupo de publicaciones'), '12')
    await user.click(screen.getByRole('button', { name: 'Guardar cambios' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(api.llamadasA(ruta)[0].init?.method).toBe('PUT')
    expect(cuerpoDe(api, ruta)).toEqual({
      nombre: 'Básico',
      precioCop: 150000,
      duracionMeses: 3,
      limitePublicaciones: 12,
      esPrueba: false,
    })
    expect(await screen.findByText('Se guardaron los cambios del plan Básico.')).toBeInTheDocument()
  })

  it('Inactivar pide confirmación; Cancelar no envía nada y devuelve el foco', async () => {
    const { user, api } = renderPlanes({ [RUTA_PLANES]: LISTA })
    const fila = await filaDe('Básico')
    const boton = within(fila).getByRole('button', { name: 'Inactivar el plan Básico' })
    await user.click(boton)
    const dialogo = screen.getByRole('dialog', { name: 'Inactivar plan' })
    expect(dialogo).toHaveTextContent('Las 2 suscripciones vivas que lo usan siguen igual.')
    await user.click(within(dialogo).getByRole('button', { name: 'Cancelar' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await waitFor(() => expect(boton).toHaveFocus())
    expect(api.llamadas.filter((l) => l.init?.method === 'POST')).toHaveLength(0)
  })

  it('Confirmar inactivar llama a /inactivar y recarga', async () => {
    const ruta = `${RUTA_PLANES}/1/inactivar`
    const { user, api } = renderPlanes({
      [RUTA_PLANES]: [LISTA, { status: 200, body: { items: [{ ...PLAN_BASICO, activo: false }, PLAN_VIEJO] } }],
      [ruta]: { status: 200, body: { ...PLAN_BASICO, activo: false } },
    })
    await user.click(within(await filaDe('Básico')).getByRole('button', { name: 'Inactivar el plan Básico' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Inactivar' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(api.llamadasA(ruta)[0].init?.method).toBe('POST')
    expect(await screen.findByText('Se inactivó el plan Básico.')).toBeInTheDocument()
    await waitFor(() => expect(api.llamadasA(RUTA_PLANES)).toHaveLength(2))
  })

  it('Activar un plan inactivo llama a /activar', async () => {
    const ruta = `${RUTA_PLANES}/3/activar`
    const { user, api } = renderPlanes({ [RUTA_PLANES]: LISTA, [ruta]: { status: 200, body: PLAN_VIEJO } })
    await user.click(within(await filaDe('Antiguo')).getByRole('button', { name: 'Activar el plan Antiguo' }))
    await user.click(within(screen.getByRole('dialog', { name: 'Activar plan' })).getByRole('button', { name: 'Activar' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(await screen.findByText('Se activó el plan Antiguo.')).toBeInTheDocument()
  })
})
