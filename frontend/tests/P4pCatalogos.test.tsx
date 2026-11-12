// P4p — Catálogos (HU-082 marcas, HU-083 modelos, HU-084 tipos de carrocería; D-32, D-37).
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AdminCatalogos } from '../src/components/AdminCatalogos'
import { simularFetch, TOKEN_FICTICIO } from './helpers'
import { CATALOGOS_INVENTARIO, cuerpoDe, RUTA_CATALOGOS_INV, type Rutas } from './p4pDatos'

const MARCAS = '/api/admin/catalogos/marcas'
const CARROCERIAS = '/api/admin/catalogos/carrocerias'

const LISTA_MARCAS = {
  status: 200,
  body: {
    items: [
      {
        id: 1,
        nombre: 'Chevrolet',
        activo: true,
        vehiculos: 12,
        lineas: [
          { id: 101, nombre: 'NPR', activo: true, tipoIds: [2, 3], vehiculos: 5 },
          { id: 102, nombre: 'Spark', activo: false, tipoIds: [1], vehiculos: 0 },
        ],
      },
      { id: 2, nombre: 'Škoda', activo: false, vehiculos: 1, lineas: [] },
    ],
  },
}

function renderCatalogos(rutas: Rutas) {
  const api = simularFetch({ [RUTA_CATALOGOS_INV]: CATALOGOS_INVENTARIO, [MARCAS]: LISTA_MARCAS, ...rutas })
  const onSesionTerminada = vi.fn()
  render(<AdminCatalogos token={TOKEN_FICTICIO} onSesionTerminada={onSesionTerminada} />)
  return { api, onSesionTerminada, user: userEvent.setup() }
}

async function filaDe(nombre: string) {
  const celda = await screen.findByRole('rowheader', { name: nombre })
  return celda.closest('tr') as HTMLElement
}

describe('Lista y selector', () => {
  it('muestra marcas con estado en texto y vehículos que las usan; cambia de catálogo', async () => {
    const { user, api } = renderCatalogos({
      [CARROCERIAS]: { status: 200, body: { items: [{ id: 5, nombre: 'Furgón', activo: true, vehiculos: 3 }] } },
    })
    const chevrolet = await filaDe('Chevrolet')
    expect(within(chevrolet).getByText('Activo')).toBeInTheDocument()
    expect(within(chevrolet).getByText('12')).toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: 'Vehículos que lo usan' })).toBeInTheDocument()
    expect(within(await filaDe('Škoda')).getByText('Inactivo')).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Catálogo'), 'Tipos de carrocería')
    await waitFor(() => expect(api.llamadasA(CARROCERIAS)).toHaveLength(1))
    expect(await filaDe('Furgón')).toBeInTheDocument()
    expect(screen.queryByRole('columnheader', { name: 'Modelos' })).not.toBeInTheDocument()
  })

  it('el buscador filtra sin distinguir mayúsculas ni tildes', async () => {
    const { user } = renderCatalogos({})
    await filaDe('Chevrolet')
    await user.type(screen.getByLabelText('Buscar en marcas'), 'skod')
    expect(screen.queryByRole('rowheader', { name: 'Chevrolet' })).not.toBeInTheDocument()
    expect(screen.getByRole('rowheader', { name: 'Škoda' })).toBeInTheDocument()
    await user.clear(screen.getByLabelText('Buscar en marcas'))
    await user.type(screen.getByLabelText('Buscar en marcas'), 'zzz')
    expect(screen.getByText('Ningún elemento coincide con la búsqueda.')).toBeInTheDocument()
  })
})

describe('Crear, renombrar y activar/desactivar', () => {
  it('Crear exige nombre; envía { nombre } recortado', async () => {
    const { user, api } = renderCatalogos({})
    await filaDe('Chevrolet')
    const nombre = screen.getByLabelText('Nombre')
    expect(nombre).toHaveAttribute('aria-required', 'true')
    await user.click(screen.getByRole('button', { name: 'Crear' }))
    expect(screen.getByText('Escribe el nombre (1 a 100 caracteres).')).toBeInTheDocument()
    expect(api.llamadas.filter((l) => l.init?.method === 'POST')).toHaveLength(0)
    await user.type(nombre, '  Volvo ')
    await user.click(screen.getByRole('button', { name: 'Crear' }))
    await waitFor(() => expect(api.llamadas.filter((l) => l.init?.method === 'POST')).toHaveLength(1))
    const post = api.llamadas.find((l) => l.init?.method === 'POST')
    expect(post?.url).toBe(MARCAS)
    expect(JSON.parse(String(post?.init?.body))).toEqual({ nombre: 'Volvo' })
    expect(await screen.findByText('Se agregó "Volvo" a Marcas.')).toBeInTheDocument()
  })

  it('409 duplicado: el mensaje del servidor queda bajo el nombre', async () => {
    const { user } = renderCatalogos({
      [MARCAS]: [LISTA_MARCAS, { status: 409, body: { error: 'Ya existe una marca con ese nombre.' } }],
    })
    await filaDe('Chevrolet')
    await user.type(screen.getByLabelText('Nombre'), 'chevrolet')
    await user.click(screen.getByRole('button', { name: 'Crear' }))
    await waitFor(() => expect(screen.getByLabelText('Nombre')).toHaveAttribute('aria-invalid', 'true'))
    expect(screen.getByLabelText('Nombre')).toHaveAccessibleDescription('Ya existe una marca con ese nombre.')
  })

  it('Renombrar abre un diálogo con el nombre y envía PUT { nombre }', async () => {
    const ruta = `${MARCAS}/1`
    const { user, api } = renderCatalogos({ [ruta]: { status: 200, body: { id: 1, nombre: 'Chevrolet GM' } } })
    await user.click(within(await filaDe('Chevrolet')).getByRole('button', { name: 'Renombrar Chevrolet' }))
    const dialogo = screen.getByRole('dialog', { name: 'Renombrar la marca' })
    const campo = within(dialogo).getByLabelText('Nombre nuevo')
    expect(campo).toHaveFocus()
    expect(campo).toHaveValue('Chevrolet')
    expect(dialogo).toHaveTextContent('Los 12 vehículos que lo usan mostrarán el nombre nuevo.')
    await user.clear(campo)
    await user.click(within(dialogo).getByRole('button', { name: 'Guardar nombre' }))
    expect(within(dialogo).getByText('Escribe el nombre (1 a 100 caracteres).')).toBeInTheDocument()
    await user.type(campo, 'Chevrolet GM')
    await user.click(within(dialogo).getByRole('button', { name: 'Guardar nombre' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(api.llamadasA(ruta)[0].init?.method).toBe('PUT')
    expect(cuerpoDe(api, ruta)).toEqual({ nombre: 'Chevrolet GM' })
    expect(await screen.findByText('Se renombró "Chevrolet" a "Chevrolet GM".')).toBeInTheDocument()
  })

  it('Desactivar pide confirmación y avisa que los vehículos lo conservan', async () => {
    const ruta = `${MARCAS}/1/desactivar`
    const { user, api } = renderCatalogos({ [ruta]: { status: 200, body: {} } })
    await user.click(within(await filaDe('Chevrolet')).getByRole('button', { name: 'Desactivar Chevrolet' }))
    const dialogo = screen.getByRole('dialog', { name: 'Desactivar la marca' })
    expect(dialogo).toHaveTextContent('Los 12 vehículos que lo usan lo conservan.')
    await user.click(within(dialogo).getByRole('button', { name: 'Desactivar' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(api.llamadasA(ruta)[0].init?.method).toBe('POST')
    expect(await screen.findByText('Se desactivó "Chevrolet". Los vehículos que ya lo usan lo conservan.')).toBeInTheDocument()
  })

  it('Activar una marca inactiva llama a /activar', async () => {
    const ruta = `${MARCAS}/2/activar`
    const { user, api } = renderCatalogos({ [ruta]: { status: 200, body: {} } })
    await user.click(within(await filaDe('Škoda')).getByRole('button', { name: 'Activar Škoda' }))
    await user.click(within(screen.getByRole('dialog', { name: 'Activar la marca' })).getByRole('button', { name: 'Activar' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
  })
})

describe('Modelos de una marca (D-32)', () => {
  async function abrirModelos(user: ReturnType<typeof userEvent.setup>) {
    const boton = within(await filaDe('Chevrolet')).getByRole('button', { name: 'Ver modelos de Chevrolet' })
    expect(boton).toHaveAttribute('aria-expanded', 'false')
    await user.click(boton)
    expect(boton).toHaveAttribute('aria-expanded', 'true')
    return screen.getByRole('region', { name: 'Modelos de Chevrolet' })
  }

  it('lista los modelos con sus categorías, estado y vehículos', async () => {
    const { user } = renderCatalogos({})
    const panel = await abrirModelos(user)
    const npr = within(panel).getByRole('rowheader', { name: 'NPR' }).closest('tr') as HTMLElement
    expect(within(npr).getByText('Camión, Volqueta')).toBeInTheDocument()
    expect(within(npr).getByText('5')).toBeInTheDocument()
    const spark = within(panel).getByRole('rowheader', { name: 'Spark' }).closest('tr') as HTMLElement
    expect(within(spark).getByText('Inactivo')).toBeInTheDocument()
  })

  it('crear modelo exige al menos una categoría y envía { nombre, tipoIds }', async () => {
    const ruta = `${MARCAS}/1/lineas`
    const { user, api } = renderCatalogos({ [ruta]: { status: 201, body: { id: 103 } } })
    const panel = await abrirModelos(user)
    const casillas = within(panel).getByRole('group', { name: 'Categorías' })
    expect(within(casillas).getAllByRole('checkbox')).toHaveLength(4)
    await user.type(within(panel).getByLabelText('Nombre del modelo'), 'NHR')
    await user.click(within(panel).getByRole('button', { name: 'Crear modelo' }))
    expect(within(panel).getByText('Elige al menos una categoría.')).toBeInTheDocument()
    expect(api.llamadasA(ruta)).toHaveLength(0)
    await user.click(within(casillas).getByLabelText('Volqueta'))
    await user.click(within(casillas).getByLabelText('Camión'))
    await user.click(within(panel).getByRole('button', { name: 'Crear modelo' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(cuerpoDe(api, ruta)).toEqual({ nombre: 'NHR', tipoIds: [2, 3] })
    expect(await screen.findByText('Se agregó el modelo "NHR" a Chevrolet.')).toBeInTheDocument()
  })

  it('editar categorías: no deja quitar todas y muestra el aviso del servidor sobre vehículos afectados', async () => {
    const ruta = '/api/admin/catalogos/lineas/101'
    const aviso = '2 vehículos usan este modelo en una categoría que ya no tiene; se corregirán al editarlos.'
    const { user, api } = renderCatalogos({ [ruta]: { status: 200, body: { id: 101, aviso } } })
    const panel = await abrirModelos(user)
    await user.click(within(panel).getByRole('button', { name: 'Editar el modelo NPR' }))
    const dialogo = screen.getByRole('dialog', { name: 'Editar modelo' })
    const camion = within(dialogo).getByLabelText('Camión')
    const volqueta = within(dialogo).getByLabelText('Volqueta')
    expect(camion).toBeChecked()
    expect(volqueta).toBeChecked()
    await user.click(camion)
    await user.click(volqueta)
    await user.click(within(dialogo).getByRole('button', { name: 'Guardar cambios' }))
    expect(within(dialogo).getByText('Elige al menos una categoría.')).toBeInTheDocument()
    expect(api.llamadasA(ruta)).toHaveLength(0)
    await user.click(camion)
    await user.click(within(dialogo).getByRole('button', { name: 'Guardar cambios' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(api.llamadasA(ruta)[0].init?.method).toBe('PUT')
    expect(cuerpoDe(api, ruta)).toEqual({ tipoIds: [2] })
    expect(await screen.findByRole('alert')).toHaveTextContent(aviso)
  })

  it('desactivar un modelo llama a /lineas/{id}/desactivar', async () => {
    const ruta = '/api/admin/catalogos/lineas/101/desactivar'
    const { user, api } = renderCatalogos({ [ruta]: { status: 200, body: {} } })
    const panel = await abrirModelos(user)
    await user.click(within(panel).getByRole('button', { name: 'Desactivar el modelo NPR' }))
    const dialogo = screen.getByRole('dialog', { name: 'Desactivar el modelo' })
    await user.click(within(dialogo).getByRole('button', { name: 'Desactivar' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
  })
})
