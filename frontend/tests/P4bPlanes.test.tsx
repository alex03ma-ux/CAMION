// P4b (D-38) — Planes: campo "Destacados incluidos" (0 a 20) en el formulario y la tabla.
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AdminPlanes } from '../src/components/AdminPlanes'
import { simularFetch, TOKEN_FICTICIO } from './helpers'
import { cuerpoDe, plan, RUTA_PLANES, type Rutas } from './p4pDatos'

function renderPlanes(rutas: Rutas) {
  const api = simularFetch(rutas)
  render(<AdminPlanes token={TOKEN_FICTICIO} onSesionTerminada={vi.fn()} />)
  return { api, user: userEvent.setup() }
}

const CON_DESTACADOS = plan(1, 'Básico', { limiteDestacados: 2 })
const UN_DESTACADO = plan(2, 'Inicial', { limiteDestacados: 1 })
const LISTA = { status: 200, body: { items: [CON_DESTACADOS, UN_DESTACADO] } }

async function filaDe(nombre: string) {
  return (await screen.findByRole('rowheader', { name: nombre })).closest('tr') as HTMLElement
}

describe('Destacados incluidos (D-38)', () => {
  it('la tabla muestra la columna Destacados con su número', async () => {
    renderPlanes({ [RUTA_PLANES]: LISTA })
    const fila = await filaDe('Básico')
    expect(screen.getByRole('columnheader', { name: 'Destacados' })).toBeInTheDocument()
    expect(within(fila).getByText('2 destacados')).toBeInTheDocument()
    expect(within(await filaDe('Inicial')).getByText('1 destacado')).toBeInTheDocument()
  })

  it('campo obligatorio (D-31), 0 por defecto y validación de 0 a 20', async () => {
    const { api, user } = renderPlanes({ [RUTA_PLANES]: LISTA })
    await filaDe('Básico')
    const campo = screen.getByLabelText('Destacados incluidos')
    expect(campo).toHaveAttribute('aria-required', 'true')
    expect(campo).toHaveValue('0')
    await user.type(screen.getByLabelText('Nombre'), 'Plus')
    await user.type(screen.getByLabelText('Precio (COP)'), '0')
    await user.type(screen.getByLabelText('Duración en meses'), '1')
    await user.type(screen.getByLabelText('Cupo de publicaciones'), '5')
    await user.clear(campo)
    await user.type(campo, '21')
    await user.click(screen.getByRole('button', { name: 'Crear plan' }))
    expect(screen.getByText('Escribe los destacados incluidos (de 0 a 20).')).toBeInTheDocument()
    expect(campo).toHaveAttribute('aria-invalid', 'true')
    await user.clear(campo)
    await user.click(screen.getByRole('button', { name: 'Crear plan' }))
    expect(screen.getByText('Escribe los destacados incluidos (de 0 a 20).')).toBeInTheDocument()
    expect(api.llamadas.filter((l) => l.init?.method === 'POST')).toHaveLength(0)
  })

  it('crear envía limiteDestacados en el cuerpo', async () => {
    const { api, user } = renderPlanes({ [RUTA_PLANES]: LISTA })
    await filaDe('Básico')
    await user.type(screen.getByLabelText('Nombre'), 'Plus')
    await user.type(screen.getByLabelText('Precio (COP)'), '90000')
    await user.type(screen.getByLabelText('Duración en meses'), '1')
    await user.type(screen.getByLabelText('Cupo de publicaciones'), '8')
    fireEvent.change(screen.getByLabelText('Destacados incluidos'), { target: { value: '3' } })
    await user.click(screen.getByRole('button', { name: 'Crear plan' }))
    await waitFor(() => expect(api.llamadas.filter((l) => l.init?.method === 'POST')).toHaveLength(1))
    const post = api.llamadas.find((l) => l.init?.method === 'POST')
    expect(JSON.parse(String(post?.init?.body))).toEqual({
      nombre: 'Plus',
      precioCop: 90000,
      duracionMeses: 1,
      limitePublicaciones: 8,
      limiteDestacados: 3,
      esPrueba: false,
    })
  })

  it('editar carga los destacados del plan y los envía en el PUT', async () => {
    const ruta = `${RUTA_PLANES}/1`
    const { api, user } = renderPlanes({ [RUTA_PLANES]: LISTA, [ruta]: { status: 200, body: CON_DESTACADOS } })
    await user.click(within(await filaDe('Básico')).getByRole('button', { name: 'Editar el plan Básico' }))
    const campo = screen.getByLabelText('Destacados incluidos')
    expect(campo).toHaveValue('2')
    await user.clear(campo)
    await user.type(campo, '20')
    await user.click(screen.getByRole('button', { name: 'Guardar cambios' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(cuerpoDe(api, ruta)).toMatchObject({ limiteDestacados: 20 })
  })

  it('400 del servidor sobre LimiteDestacados: el mensaje junto al campo', async () => {
    const { user } = renderPlanes({
      [RUTA_PLANES]: [LISTA, { status: 400, body: { error: 'Revisa los datos.', errores: { LimiteDestacados: ['Máximo 20 destacados.'] } } }],
    })
    await filaDe('Básico')
    await user.type(screen.getByLabelText('Nombre'), 'Plus')
    await user.type(screen.getByLabelText('Precio (COP)'), '0')
    await user.type(screen.getByLabelText('Duración en meses'), '1')
    await user.type(screen.getByLabelText('Cupo de publicaciones'), '1')
    await user.click(screen.getByRole('button', { name: 'Crear plan' }))
    expect(await screen.findByText('Máximo 20 destacados.')).toBeInTheDocument()
    expect(screen.getByLabelText('Destacados incluidos')).toHaveAttribute('aria-invalid', 'true')
  })
})
