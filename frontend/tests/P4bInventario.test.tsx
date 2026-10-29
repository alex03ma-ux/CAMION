// P4b — Inventario: publicar (HU-023), estado de la publicación, marcar vendido (HU-064, D-39),
// revertir la venta y reasignar a otro asesor (D-34, solo administrador). Fetch simulado.
import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { hoyIso } from '../src/api/publicaciones'
import { simularFetch } from './helpers'
import { abrirConSesion, CAT, CAT_ADMIN, CATALOGOS, detalleCamion, item, LISTA, lista, ME, me } from './inventarioDatos'
import { cuerpo, paginaPub, pub, RUTA_MAPA, RUTA_PUBLICACIONES } from './p4bDatos'

const V_LIBRE = item({ id: 1, estado: 'ACTIVO', totalFotos: 2 })
const V_PUBLICADO = item({ id: 2, estado: 'RESERVADO', marca: 'Kenworth', linea: 'T800', modeloAnio: 2018, totalFotos: 3 })
const V_SIN_FOTOS = item({ id: 3, estado: 'ACTIVO', linea: 'NHR', modeloAnio: 2015, totalFotos: 0 })
const V_INACTIVO = item({ id: 4, estado: 'INACTIVO', modeloAnio: 2012, totalFotos: 1 })
const V_VENDIDO = item({ id: 5, estado: 'VENDIDO', modeloAnio: 2010, totalFotos: 1 })

const PUB_2 = pub(20, 2, { slugPublico: 'kenworth-t800-2018-2', destacada: true })
const PUB_5 = pub(50, 5, { estado: 'VENDIDA', estadoEfectivo: 'VENDIDA' })

type Rutas = Parameters<typeof simularFetch>[0]

function abrirAsesor(rutas: Rutas = {}) {
  const api = simularFetch({
    [ME]: me(['ASESOR']),
    [CATALOGOS]: { status: 200, body: CAT },
    [LISTA]: lista([V_LIBRE, V_PUBLICADO, V_SIN_FOTOS, V_INACTIVO, V_VENDIDO]),
    [RUTA_MAPA]: paginaPub([PUB_2, PUB_5], 100),
    ...rutas,
  })
  abrirConSesion('#/inventario')
  return { api, user: userEvent.setup() }
}

function tarjeta(titulo: string): HTMLElement {
  return screen.getByRole('heading', { level: 3, name: titulo }).closest('li') as HTMLElement
}

async function esperarBloques() {
  await waitFor(() => expect(screen.getAllByText(/Sin publicación|Publicación:/).length).toBeGreaterThan(0))
}

describe('Estado de la publicación en cada vehículo', () => {
  it('sin publicación ofrece "Publicar"; con publicación muestra su estado y los enlaces', async () => {
    abrirAsesor()
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    await esperarBloques()
    expect(screen.getByRole('link', { name: 'Mis publicaciones' })).toHaveAttribute('href', '#/publicaciones')

    const libre = tarjeta('Chevrolet NPR 2020')
    expect(within(libre).getByText('Sin publicación')).toBeInTheDocument()
    expect(within(libre).getByRole('button', { name: 'Publicar Chevrolet NPR 2020' })).toBeInTheDocument()
    expect(within(libre).getByRole('button', { name: 'Marcar vendido Chevrolet NPR 2020' })).toBeInTheDocument()

    const publicado = tarjeta('Kenworth T800 2018')
    expect(within(publicado).getByText('Publicada')).toBeInTheDocument()
    expect(within(publicado).getByText('Destacada')).toBeInTheDocument()
    expect(within(publicado).queryByRole('button', { name: /^Publicar/ })).toBeNull()
    expect(within(publicado).getByRole('link', { name: 'Gestionar en Mis publicaciones Kenworth T800 2018' })).toHaveAttribute(
      'href',
      '#/publicaciones',
    )
    expect(within(publicado).getByRole('link', { name: 'Abrir en el portal Kenworth T800 2018' })).toHaveAttribute(
      'href',
      '#/vehiculo/kenworth-t800-2018-2',
    )
    expect(within(publicado).getByRole('button', { name: 'Marcar vendido Kenworth T800 2018' })).toBeInTheDocument()

    const sinFotos = tarjeta('Chevrolet NHR 2015')
    expect(within(sinFotos).queryByRole('button', { name: /^Publicar/ })).toBeNull()
    expect(within(sinFotos).getByText('Agrega al menos una foto para poder publicarlo.')).toBeInTheDocument()

    const inactivo = tarjeta('Chevrolet NPR 2012')
    expect(within(inactivo).queryByRole('button', { name: /^Publicar|Marcar vendido/ })).toBeNull()
    expect(within(inactivo).getByText('Reactiva el vehículo para poder publicarlo.')).toBeInTheDocument()

    const vendido = tarjeta('Chevrolet NPR 2010')
    expect(within(vendido).getByText('Vendida')).toBeInTheDocument()
    expect(within(vendido).getByRole('button', { name: 'Revertir venta Chevrolet NPR 2010' })).toBeInTheDocument()
    expect(within(vendido).queryByRole('button', { name: /Marcar vendido/ })).toBeNull()
  })

  it('si no se pueden consultar las publicaciones, no ofrece publicar ni vender y deja reintentar', async () => {
    const { api, user } = abrirAsesor({ [RUTA_MAPA]: [{ status: 500 }, paginaPub([], 100)] })
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    expect(await screen.findByText(/No se pudo consultar el estado de las publicaciones/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^Publicar|Marcar vendido|Revertir venta/ })).toBeNull()
    await user.click(screen.getByRole('button', { name: 'Volver a consultar publicaciones' }))
    expect(await screen.findByRole('button', { name: 'Publicar Chevrolet NPR 2020' })).toBeInTheDocument()
    expect(api.llamadasA(RUTA_MAPA)).toHaveLength(2)
  })
})

describe('Publicar (HU-023)', () => {
  it('asteriscos D-31, valida el título (5 a 180) y envía el cuerpo del contrato', async () => {
    const { api, user } = abrirAsesor({
      [RUTA_PUBLICACIONES]: { status: 201, body: pub(30, 1) },
    })
    await esperarBloques()
    const boton = within(tarjeta('Chevrolet NPR 2020')).getByRole('button', { name: 'Publicar Chevrolet NPR 2020' })
    await user.click(boton)
    const dialogo = screen.getByRole('dialog', { name: 'Publicar vehículo' })
    expect(within(dialogo).getByText('Campo obligatorio')).toBeInTheDocument()
    const titulo = within(dialogo).getByLabelText('Título de la publicación')
    expect(titulo).toHaveAttribute('aria-required', 'true')
    expect(titulo).toHaveValue('Chevrolet NPR 2020')
    expect(titulo).toHaveFocus()
    expect(within(dialogo).getByLabelText('Descripción comercial (opcional)')).not.toHaveAttribute('aria-required')

    await user.clear(titulo)
    await user.type(titulo, 'NPR')
    await user.click(within(dialogo).getByRole('button', { name: 'Publicar' }))
    expect(within(dialogo).getByText('Escribe el título de la publicación (5 a 180 caracteres).')).toBeInTheDocument()
    expect(api.llamadas.filter((l) => l.init?.method === 'POST')).toHaveLength(0)

    await user.clear(titulo)
    await user.type(titulo, '  Camión NPR listo para trabajar ')
    await user.type(within(dialogo).getByLabelText('Descripción comercial (opcional)'), 'Furgón en aluminio.')
    await user.click(within(dialogo).getByLabelText('El precio es negociable'))
    await user.click(within(dialogo).getByRole('button', { name: 'Publicar' }))
    await waitFor(() => expect(api.llamadasA(RUTA_PUBLICACIONES)).toHaveLength(1))
    const post = api.llamadasA(RUTA_PUBLICACIONES)[0]
    expect(post.init?.method).toBe('POST')
    expect(cuerpo(post.init)).toEqual({
      vehiculoId: 1,
      titulo: 'Camión NPR listo para trabajar',
      descripcionComercial: 'Furgón en aluminio.',
      negociable: true,
    })
    expect(await screen.findByText('Se publicó Chevrolet NPR 2020 con el título «Camión NPR listo para trabajar».')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).toBeNull()
    await waitFor(() => expect(api.llamadasA(RUTA_MAPA).length).toBeGreaterThanOrEqual(2))
  })

  it.each([
    ['sin plan', 'No tienes un plan activo. Comunícate con el administrador.'],
    ['sin cupo', 'Ya usas las 5 publicaciones de tu plan. Elimina una para crear otra.'],
  ])('409 %s: el mensaje del servidor queda en el diálogo', async (_caso, mensaje) => {
    const { user } = abrirAsesor({ [RUTA_PUBLICACIONES]: { status: 409, body: { error: mensaje } } })
    await esperarBloques()
    await user.click(screen.getByRole('button', { name: 'Publicar Chevrolet NPR 2020' }))
    const dialogo = screen.getByRole('dialog', { name: 'Publicar vehículo' })
    await user.click(within(dialogo).getByRole('button', { name: 'Publicar' }))
    expect(await within(dialogo).findByRole('alert')).toHaveTextContent(mensaje)
    expect(within(dialogo).getByLabelText('Título de la publicación')).toHaveValue('Chevrolet NPR 2020')
  })

  it('400 con errores por campo: el mensaje junto al título', async () => {
    const { user } = abrirAsesor({
      [RUTA_PUBLICACIONES]: { status: 400, body: { error: 'Revisa los datos.', errores: { Titulo: ['El título es demasiado corto.'] } } },
    })
    await esperarBloques()
    await user.click(screen.getByRole('button', { name: 'Publicar Chevrolet NPR 2020' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Publicar' }))
    expect(await screen.findByText('El título es demasiado corto.')).toBeInTheDocument()
    expect(screen.getByLabelText('Título de la publicación')).toHaveAttribute('aria-invalid', 'true')
  })

  it('Cancelar no envía nada y devuelve el foco al botón', async () => {
    const { api, user } = abrirAsesor()
    await esperarBloques()
    const boton = screen.getByRole('button', { name: 'Publicar Chevrolet NPR 2020' })
    await user.click(boton)
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancelar' }))
    expect(screen.queryByRole('dialog')).toBeNull()
    await waitFor(() => expect(boton).toHaveFocus())
    expect(api.llamadas.filter((l) => l.init?.method === 'POST')).toHaveLength(0)
  })
})

function manana(): string {
  const d = new Date()
  d.setDate(d.getDate() + 1)
  return hoyIso(d)
}

describe('Marcar vendido (HU-064, D-39)', () => {
  const RUTA_VENDER = '/api/inventario/vehiculos/1/vender'

  it('origen de la lista fija con nombres legibles; valida fecha, precio y observaciones si es Otro', async () => {
    const { api, user } = abrirAsesor({ [RUTA_VENDER]: { status: 200, body: {} } })
    await esperarBloques()
    await user.click(screen.getByRole('button', { name: 'Marcar vendido Chevrolet NPR 2020' }))
    const dialogo = screen.getByRole('dialog', { name: 'Marcar vehículo vendido' })
    const origen = within(dialogo).getByLabelText('Origen de la venta')
    expect(within(origen).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Elige el origen',
      'Portal web',
      'Contacto directo del asesor',
      'Referido',
      'Otro',
    ])
    const fecha = within(dialogo).getByLabelText('Fecha de la venta')
    expect(fecha).toHaveValue(hoyIso())
    expect(fecha).toHaveAttribute('aria-required', 'true')
    const precio = within(dialogo).getByLabelText('Precio final (COP)')
    expect(precio).toHaveValue('120.000.000')

    fireEvent.change(fecha, { target: { value: manana() } })
    fireEvent.change(precio, { target: { value: '0' } })
    await user.click(within(dialogo).getByRole('button', { name: 'Marcar vendido' }))
    expect(within(dialogo).getByText('La fecha de la venta no puede ser posterior a hoy.')).toBeInTheDocument()
    expect(within(dialogo).getByText('Escribe el precio final de la venta en pesos (mayor que 0).')).toBeInTheDocument()
    expect(within(dialogo).getByText('Elige el origen de la venta.')).toBeInTheDocument()
    await waitFor(() => expect(fecha).toHaveFocus())

    fireEvent.change(fecha, { target: { value: hoyIso() } })
    fireEvent.change(precio, { target: { value: '118.500.000' } })
    expect(precio).toHaveValue('118.500.000')
    await user.selectOptions(origen, 'OTRO')
    expect(within(dialogo).getByLabelText('Observaciones')).toHaveAttribute('aria-required', 'true')
    await user.click(within(dialogo).getByRole('button', { name: 'Marcar vendido' }))
    expect(within(dialogo).getByText('Si el origen es «Otro», explica en las observaciones cómo se vendió.')).toBeInTheDocument()
    expect(api.llamadasA(RUTA_VENDER)).toHaveLength(0)

    await user.type(within(dialogo).getByLabelText('Observaciones'), 'Feria de transporte')
    await user.click(within(dialogo).getByRole('button', { name: 'Marcar vendido' }))
    await waitFor(() => expect(api.llamadasA(RUTA_VENDER)).toHaveLength(1))
    expect(cuerpo(api.llamadasA(RUTA_VENDER)[0].init)).toEqual({
      fechaVenta: hoyIso(),
      precioFinalCop: 118500000,
      origen: 'OTRO',
      observaciones: 'Feria de transporte',
    })
    expect(await screen.findByText('Chevrolet NPR 2020 quedó vendido y salió del portal.')).toBeInTheDocument()
  })

  it('con origen Portal web las observaciones son opcionales y viajan como null', async () => {
    const { api, user } = abrirAsesor({ [RUTA_VENDER]: { status: 200, body: {} } })
    await esperarBloques()
    await user.click(screen.getByRole('button', { name: 'Marcar vendido Chevrolet NPR 2020' }))
    const dialogo = screen.getByRole('dialog', { name: 'Marcar vehículo vendido' })
    expect(within(dialogo).getByLabelText('Observaciones (opcional)')).not.toHaveAttribute('aria-required')
    await user.selectOptions(within(dialogo).getByLabelText('Origen de la venta'), 'PORTAL')
    await user.click(within(dialogo).getByRole('button', { name: 'Marcar vendido' }))
    await waitFor(() => expect(api.llamadasA(RUTA_VENDER)).toHaveLength(1))
    expect(cuerpo(api.llamadasA(RUTA_VENDER)[0].init)).toEqual({
      fechaVenta: hoyIso(),
      precioFinalCop: 120000000,
      origen: 'PORTAL',
      observaciones: null,
    })
  })

  it('400 del servidor: el error queda junto a su campo', async () => {
    const { user } = abrirAsesor({
      [RUTA_VENDER]: {
        status: 400,
        body: { error: 'Revisa los datos.', errores: { FechaVenta: ['La fecha no puede ser anterior al registro del vehículo.'] } },
      },
    })
    await esperarBloques()
    await user.click(screen.getByRole('button', { name: 'Marcar vendido Chevrolet NPR 2020' }))
    const dialogo = screen.getByRole('dialog')
    await user.selectOptions(within(dialogo).getByLabelText('Origen de la venta'), 'REFERIDO')
    await user.click(within(dialogo).getByRole('button', { name: 'Marcar vendido' }))
    expect(await within(dialogo).findByText('La fecha no puede ser anterior al registro del vehículo.')).toBeInTheDocument()
    expect(within(dialogo).getByLabelText('Fecha de la venta')).toHaveAttribute('aria-invalid', 'true')
  })
})

describe('Revertir venta', () => {
  const RUTA_REVERTIR = '/api/inventario/vehiculos/5/revertir-venta'

  it('pide un motivo de 5 a 500 caracteres y envía { motivo }', async () => {
    const { api, user } = abrirAsesor({ [RUTA_REVERTIR]: { status: 200, body: {} } })
    await esperarBloques()
    await user.click(screen.getByRole('button', { name: 'Revertir venta Chevrolet NPR 2010' }))
    const dialogo = screen.getByRole('dialog', { name: 'Revertir venta' })
    expect(dialogo).toHaveTextContent('su publicación queda inactiva')
    const motivo = within(dialogo).getByLabelText('Motivo')
    expect(motivo).toHaveAttribute('aria-required', 'true')
    await user.type(motivo, 'no')
    await user.click(within(dialogo).getByRole('button', { name: 'Revertir venta' }))
    expect(within(dialogo).getByText('Escribe el motivo (5 a 500 caracteres).')).toBeInTheDocument()
    expect(api.llamadasA(RUTA_REVERTIR)).toHaveLength(0)
    await user.type(motivo, ' se pagó: el crédito fue negado')
    await user.click(within(dialogo).getByRole('button', { name: 'Revertir venta' }))
    await waitFor(() => expect(api.llamadasA(RUTA_REVERTIR)).toHaveLength(1))
    expect(cuerpo(api.llamadasA(RUTA_REVERTIR)[0].init)).toEqual({ motivo: 'no se pagó: el crédito fue negado' })
    expect(await screen.findByText(/Se revirtió la venta de Chevrolet NPR 2010/)).toBeInTheDocument()
  })

  it('409 (estado cambiado): cierra el diálogo y muestra el mensaje del servidor', async () => {
    const { user } = abrirAsesor({
      [RUTA_REVERTIR]: { status: 409, body: { error: 'El vehículo ya no está vendido.' } },
    })
    await esperarBloques()
    await user.click(screen.getByRole('button', { name: 'Revertir venta Chevrolet NPR 2010' }))
    await user.type(screen.getByLabelText('Motivo'), 'registro equivocado')
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Revertir venta' }))
    expect(await screen.findByText('El vehículo ya no está vendido.')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).toBeNull()
  })
})

describe('Reasignar a otro asesor (D-34)', () => {
  const DETALLE_9 = '/api/inventario/vehiculos/9'
  const RUTA_REASIGNAR = '/api/admin/vehiculos/9/reasignar'

  it('el asesor no ve la acción en el detalle', async () => {
    simularFetch({
      [ME]: me(['ASESOR']),
      [CATALOGOS]: { status: 200, body: CAT },
      [DETALLE_9]: { status: 200, body: detalleCamion() },
    })
    abrirConSesion('#/inventario/9')
    expect(await screen.findByText(/Precio anterior: \$ 120\.000\.000/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reasignar a otro asesor' })).toBeNull()
  })

  it('el administrador elige otro asesor activo y un motivo; se envía { asesorId, motivo }', async () => {
    const api = simularFetch({
      [ME]: me(['ADMINISTRADOR']),
      [CATALOGOS]: { status: 200, body: CAT_ADMIN },
      [DETALLE_9]: [
        { status: 200, body: detalleCamion({ asesor: { id: 5, nombre: 'Asesor Uno Ficticio' } }) },
        { status: 200, body: detalleCamion({ asesor: { id: 6, nombre: 'Asesor Dos Ficticio' } }) },
      ],
      [RUTA_REASIGNAR]: { status: 200, body: {} },
    })
    abrirConSesion('#/inventario/9')
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Reasignar a otro asesor' }))
    const dialogo = screen.getByRole('dialog', { name: 'Reasignar a otro asesor' })
    const select = within(dialogo).getByLabelText('Nuevo asesor')
    expect(select).toHaveAttribute('aria-required', 'true')
    // El asesor actual no aparece en la lista.
    expect(within(select).getAllByRole('option').map((o) => o.textContent)).toEqual(['Elige el asesor', 'Asesor Dos Ficticio'])

    await user.type(within(dialogo).getByLabelText('Motivo'), 'El asesor se retira de la red')
    await user.click(within(dialogo).getByRole('button', { name: 'Reasignar' }))
    expect(within(dialogo).getByText('Elige el asesor que recibirá el vehículo.')).toBeInTheDocument()
    expect(api.llamadasA(RUTA_REASIGNAR)).toHaveLength(0)

    await user.selectOptions(select, '6')
    await user.click(within(dialogo).getByRole('button', { name: 'Reasignar' }))
    await waitFor(() => expect(api.llamadasA(RUTA_REASIGNAR)).toHaveLength(1))
    expect(api.llamadasA(RUTA_REASIGNAR)[0].init?.method).toBe('POST')
    expect(cuerpo(api.llamadasA(RUTA_REASIGNAR)[0].init)).toEqual({ asesorId: 6, motivo: 'El asesor se retira de la red' })
    expect(await screen.findByText('Chevrolet NPR 2020 quedó asignado a Asesor Dos Ficticio.')).toBeInTheDocument()
    await waitFor(() => expect(api.llamadasA(DETALLE_9)).toHaveLength(2))
  })

  it('409 el asesor destino no tiene cupo: el mensaje queda en el diálogo', async () => {
    const mensaje = 'El asesor destino ya usa todas las publicaciones de su plan.'
    simularFetch({
      [ME]: me(['ADMINISTRADOR']),
      [CATALOGOS]: { status: 200, body: CAT_ADMIN },
      [DETALLE_9]: { status: 200, body: detalleCamion({ asesor: { id: 5, nombre: 'Asesor Uno Ficticio' } }) },
      [RUTA_REASIGNAR]: { status: 409, body: { error: mensaje } },
    })
    abrirConSesion('#/inventario/9')
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Reasignar a otro asesor' }))
    const dialogo = screen.getByRole('dialog')
    await user.selectOptions(within(dialogo).getByLabelText('Nuevo asesor'), '6')
    await user.type(within(dialogo).getByLabelText('Motivo'), 'Cambio de zona')
    await user.click(within(dialogo).getByRole('button', { name: 'Reasignar' }))
    expect(await within(dialogo).findByRole('alert')).toHaveTextContent(mensaje)
  })
})
