// P4a: lista del inventario (CA-UI-03, CA-UI-04, CA-UI-11; HU-020, HU-021, HU-022).
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it } from 'vitest'
import { marcarVehiculoReciente, olvidarVehiculoReciente } from '../src/inventario/avisoPendiente'
import { simularFetch } from './helpers'
import {
  abrirConSesion,
  CAT,
  CAT_ADMIN,
  CATALOGOS,
  cuerpoJson,
  item,
  LISTA,
  lista,
  ME,
  me,
  rutaLista,
  VACIA,
} from './inventarioDatos'

const V_ACTIVO = item({ id: 1, estado: 'ACTIVO' })
const V_RESERVADO = item({ id: 2, estado: 'RESERVADO', marca: 'Kenworth', linea: 'T800', modeloAnio: 2018, placa: 'XYZ987' })
const V_INACTIVO = item({ id: 3, estado: 'INACTIVO', linea: 'NHR', modeloAnio: 2015, placa: 'QWE456' })
const V_VENDIDO = item({ id: 4, estado: 'VENDIDO', modeloAnio: 2010, placa: 'VEN001' })
const V_CARROCERIA = item({
  id: 5,
  tipo: 'Carrocería',
  marca: null,
  linea: null,
  modeloAnio: null,
  placa: null,
  kilometraje: null,
  fotoPortada: null,
  totalFotos: 0,
})
const V_MAQUINA = item({
  id: 6,
  tipo: 'Maquinaria',
  marca: 'Kenworth',
  linea: null,
  modeloAnio: 2019,
  placa: null,
  kilometraje: null,
  horasUso: 1200,
  fotoPortada: '/media/vehiculos/6/a.jpg',
  totalFotos: 3,
})

function tarjeta(titulo: string): HTMLElement {
  return screen.getByRole('heading', { level: 3, name: titulo }).closest('li') as HTMLElement
}

function botonesDe(t: HTMLElement): string[] {
  return within(t)
    .queryAllByRole('button')
    .map((b) => (b.textContent ?? '').replace(/\s+/g, ' ').trim())
}

function abrirAsesor(rutas: Parameters<typeof simularFetch>[0]) {
  const api = simularFetch({ [ME]: me(['ASESOR']), [CATALOGOS]: { status: 200, body: CAT }, ...rutas })
  abrirConSesion('#/inventario')
  return api
}

function abrirAdmin(rutas: Parameters<typeof simularFetch>[0]) {
  const api = simularFetch({ [ME]: me(['ADMINISTRADOR']), [CATALOGOS]: { status: 200, body: CAT_ADMIN }, ...rutas })
  abrirConSesion('#/inventario')
  return api
}

describe('Tarjetas (CA-UI-03)', () => {
  it('foto o marcador, título sin "null", placa, precio con miles, estado en texto y "Registrar vehículo"', async () => {
    abrirAsesor({ [LISTA]: lista([V_ACTIVO, V_CARROCERIA, V_MAQUINA]) })
    expect(await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Registrar vehículo' })).toHaveAttribute('href', '#/inventario/nuevo')
    expect(screen.getByText(/\(3 vehículos\)/)).toBeInTheDocument()

    const camion = tarjeta('Chevrolet NPR 2020')
    expect(within(camion).getByText('$ 120.000.000')).toBeInTheDocument()
    expect(within(camion).getByText('ABC123')).toBeInTheDocument()
    expect(within(camion).getByText('Activo')).toBeInTheDocument()
    expect(within(camion).getByText('85.000 km')).toBeInTheDocument()
    expect(within(camion).getByText('Sin foto')).toBeInTheDocument()
    expect(within(camion).getByRole('link', { name: /Editar/ })).toHaveAttribute('href', '#/inventario/1')

    const carroceria = tarjeta('Carrocería')
    expect(carroceria.textContent).not.toMatch(/null|undefined/)
    expect(within(carroceria).getByText('Sin placa')).toBeInTheDocument()
    expect(within(carroceria).queryByText(/km/)).toBeNull()

    const maquina = tarjeta('Kenworth 2019')
    expect(within(maquina).getByText('1.200 horas de uso')).toBeInTheDocument()
    expect(within(maquina).getByText('3 de 10')).toBeInTheDocument()
    expect(maquina.querySelector('img')).toHaveAttribute('src', '/media/vehiculos/6/a.jpg')
  })

  it('acciones por estado: ACTIVO, RESERVADO, INACTIVO y VENDIDO (sin acciones)', async () => {
    abrirAsesor({ [LISTA]: lista([V_ACTIVO, V_RESERVADO, V_INACTIVO, V_VENDIDO]) })
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    expect(botonesDe(tarjeta('Chevrolet NPR 2020'))).toEqual([
      'Reservar Chevrolet NPR 2020',
      'Inactivar Chevrolet NPR 2020',
    ])
    expect(botonesDe(tarjeta('Kenworth T800 2018'))).toEqual([
      'Liberar Kenworth T800 2018',
      'Inactivar Kenworth T800 2018',
    ])
    expect(botonesDe(tarjeta('Chevrolet NHR 2015'))).toEqual(['Reactivar Chevrolet NHR 2015'])
    const vendido = tarjeta('Chevrolet NPR 2010')
    expect(botonesDe(vendido)).toEqual([])
    expect(within(vendido).getByText('Vendido')).toBeInTheDocument()
    expect(within(vendido).getByText('Un vehículo vendido no se puede modificar.')).toBeInTheDocument()
    expect(within(vendido).getByRole('link', { name: /Ver/ })).toHaveAttribute('href', '#/inventario/4')
    expect(within(vendido).queryByRole('link', { name: /Editar/ })).toBeNull()
  })

  it('lista vacía sin filtros: mensaje del asesor', async () => {
    abrirAsesor({ [LISTA]: VACIA })
    expect(await screen.findByText('Aún no tienes vehículos registrados.')).toBeInTheDocument()
  })

  it('lista vacía sin filtros: mensaje del administrador', async () => {
    abrirAdmin({ [LISTA]: VACIA })
    expect(await screen.findByText('Aún no hay vehículos registrados.')).toBeInTheDocument()
  })

  it('error del servidor al listar: aviso visible', async () => {
    abrirAsesor({ [LISTA]: { status: 500 } })
    expect(await screen.findByText(/No se pudo conectar con el servidor/)).toBeInTheDocument()
  })
})

describe('Filtros (CA-UI-04)', () => {
  it('asesor: no ve el filtro de asesor; estado, categoría y búsqueda viajan en la petición', async () => {
    const consulta = 'estado=RESERVADO&tipoId=2&q=npr+blanco'
    const api = abrirAsesor({ [LISTA]: lista([V_ACTIVO]), [rutaLista(consulta)]: VACIA })
    const u = userEvent.setup()
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    expect(screen.queryByLabelText('Asesor')).toBeNull()
    await u.selectOptions(screen.getByLabelText('Estado'), 'RESERVADO')
    await u.selectOptions(screen.getByLabelText('Categoría'), '2')
    await u.type(screen.getByLabelText('Buscar por placa, marca, línea o descripción'), '  npr blanco ')
    await u.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(await screen.findByText('No hay vehículos con estos filtros.')).toBeInTheDocument()
    expect(api.llamadasA(rutaLista(consulta))).toHaveLength(1)
    expect(api.llamadas.some((l) => l.url.includes('asesorId'))).toBe(false)
  })

  it('las opciones de estado son las 4 del contrato y las de categoría salen de los catálogos', async () => {
    abrirAsesor({ [LISTA]: VACIA })
    await screen.findByText('Aún no tienes vehículos registrados.')
    const estados = within(screen.getByLabelText('Estado')).getAllByRole('option').map((o) => o.textContent)
    expect(estados).toEqual(['Todos los estados', 'Activo', 'Reservado', 'Inactivo', 'Vendido'])
    await waitFor(() =>
      expect(within(screen.getByLabelText('Categoría')).getAllByRole('option').map((o) => o.textContent)).toEqual([
        'Todas las categorías',
        'Carro',
        'Camión',
        'Carrocería',
        'Maquinaria',
      ]),
    )
  })

  it('administrador: filtra por asesor y ve el asesor de cada vehículo', async () => {
    const conAsesor = { ...V_ACTIVO, asesor: { id: 5, nombre: 'Asesor Uno Ficticio' } }
    const api = abrirAdmin({ [LISTA]: lista([conAsesor]), [rutaLista('asesorId=6')]: VACIA })
    const u = userEvent.setup()
    const t = await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    expect(within(t.closest('li') as HTMLElement).getByText('Asesor Uno Ficticio')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Inventario' })).toBeInTheDocument()
    await u.selectOptions(screen.getByLabelText('Asesor'), '6')
    await u.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(await screen.findByText('No hay vehículos con estos filtros.')).toBeInTheDocument()
    expect(api.llamadasA(rutaLista('asesorId=6'))).toHaveLength(1)
  })

  it('"Limpiar" vuelve a la lista sin filtros', async () => {
    const api = abrirAsesor({ [LISTA]: lista([V_ACTIVO]), [rutaLista('estado=VENDIDO')]: VACIA })
    const u = userEvent.setup()
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    await u.selectOptions(screen.getByLabelText('Estado'), 'VENDIDO')
    await u.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await screen.findByText('No hay vehículos con estos filtros.')
    await u.click(screen.getByRole('button', { name: 'Limpiar' }))
    expect(await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })).toBeInTheDocument()
    expect(screen.getByLabelText('Estado')).toHaveValue('')
    expect(api.llamadasA(LISTA)).toHaveLength(2)
  })

  it('400 del servidor en un filtro: el mensaje queda bajo el campo', async () => {
    abrirAsesor({
      [LISTA]: lista([V_ACTIVO]),
      [rutaLista('q=abc')]: {
        status: 400,
        body: { error: 'Revisa los filtros.', errores: { q: ['La búsqueda admite hasta 100 caracteres.'] } },
      },
    })
    const u = userEvent.setup()
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    const q = screen.getByLabelText('Buscar por placa, marca, línea o descripción')
    expect(q).toHaveAttribute('maxLength', '100')
    await u.type(q, 'abc')
    await u.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(await screen.findByText('La búsqueda admite hasta 100 caracteres.')).toBeInTheDocument()
    expect(q).toHaveAttribute('aria-invalid', 'true')
  })
})

describe('Paginación (CA-UI-03, CA-021-06)', () => {
  it('25 vehículos: página 1 de 2; "Siguiente" pide pagina=2 y "Anterior" vuelve', async () => {
    const pagina1 = Array.from({ length: 20 }, (_, i) => item({ id: 100 + i, placa: `PAG${100 + i}` }))
    const pagina2 = Array.from({ length: 5 }, (_, i) => item({ id: 200 + i, placa: `SEG${200 + i}` }))
    const api = abrirAsesor({
      [LISTA]: lista(pagina1, 25, 1),
      [rutaLista('', 2)]: lista(pagina2, 25, 2),
    })
    const u = userEvent.setup()
    const nav = await screen.findByRole('navigation', { name: 'Paginación del inventario' })
    expect(within(nav).getByText('Página 1 de 2')).toBeInTheDocument()
    expect(within(nav).getByRole('button', { name: 'Anterior' })).toBeDisabled()
    expect(screen.getAllByRole('heading', { level: 3 })).toHaveLength(20)
    await u.click(within(nav).getByRole('button', { name: 'Siguiente' }))
    expect(await screen.findByText('SEG200')).toBeInTheDocument()
    expect(screen.getAllByRole('heading', { level: 3 })).toHaveLength(5)
    expect(within(nav).getByText('Página 2 de 2')).toBeInTheDocument()
    expect(within(nav).getByRole('button', { name: 'Siguiente' })).toBeDisabled()
    expect(api.llamadasA(rutaLista('', 2))).toHaveLength(1)
    await u.click(within(nav).getByRole('button', { name: 'Anterior' }))
    expect(await screen.findByText('PAG100')).toBeInTheDocument()
  })

  it('sin más de una página no hay paginación', async () => {
    abrirAsesor({ [LISTA]: lista([V_ACTIVO]) })
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    expect(screen.queryByRole('navigation', { name: 'Paginación del inventario' })).toBeNull()
  })
})

describe('Acciones de estado (HU-020, D-11)', () => {
  it('Reservar: POST a /reservar, aviso de éxito y la lista se recarga', async () => {
    const api = abrirAsesor({
      [LISTA]: [lista([V_ACTIVO]), lista([{ ...V_ACTIVO, estado: 'RESERVADO' }])],
      '/api/inventario/vehiculos/1/reservar': { status: 200, body: { ...V_ACTIVO, estado: 'RESERVADO' } },
    })
    const u = userEvent.setup()
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    await u.click(screen.getByRole('button', { name: /^Reservar/ }))
    // D-28: reservar pide una nota obligatoria en un diálogo.
    const dlg = await screen.findByRole('dialog', { name: 'Reservar vehículo' })
    const nota = within(dlg).getByLabelText('Motivo')
    await u.click(within(dlg).getByRole('button', { name: 'Reservar' }))
    expect(within(dlg).getByText('Escribe el motivo (5 a 500 caracteres).')).toBeInTheDocument()
    expect(api.llamadasA('/api/inventario/vehiculos/1/reservar')).toHaveLength(0)
    await u.type(nota, '  Pagó anticipo  ')
    await u.click(within(dlg).getByRole('button', { name: 'Reservar' }))
    expect(await screen.findByText('Chevrolet NPR 2020 quedó reservado.')).toBeInTheDocument()
    const llamada = api.llamadasA('/api/inventario/vehiculos/1/reservar')[0]
    expect(llamada.init?.method).toBe('POST')
    expect(cuerpoJson(llamada.init)).toEqual({ motivo: 'Pagó anticipo' })
    await waitFor(() => expect(api.llamadasA(LISTA)).toHaveLength(2))
    expect(await screen.findByRole('button', { name: /^Liberar/ })).toBeInTheDocument()
  })

  it('Liberar y Reactivar llaman a su endpoint y muestran "volvió a estar activo"', async () => {
    const api = abrirAsesor({
      [LISTA]: lista([V_RESERVADO, V_INACTIVO]),
      '/api/inventario/vehiculos/2/liberar': { status: 200, body: { ...V_RESERVADO, estado: 'ACTIVO' } },
      '/api/inventario/vehiculos/3/reactivar': { status: 200, body: { ...V_INACTIVO, estado: 'ACTIVO' } },
    })
    const u = userEvent.setup()
    await screen.findByRole('heading', { level: 3, name: 'Kenworth T800 2018' })
    await u.click(screen.getByRole('button', { name: /^Liberar/ }))
    expect(await screen.findByText('Kenworth T800 2018 volvió a estar activo.')).toBeInTheDocument()
    await u.click(await screen.findByRole('button', { name: /^Reactivar/ }))
    expect(await screen.findByText('Chevrolet NHR 2015 volvió a estar activo.')).toBeInTheDocument()
    expect(api.llamadasA('/api/inventario/vehiculos/2/liberar')).toHaveLength(1)
    expect(api.llamadasA('/api/inventario/vehiculos/3/reactivar')).toHaveLength(1)
  })

  it('409 del servidor al reservar: se muestra su mensaje', async () => {
    abrirAsesor({
      [LISTA]: lista([V_ACTIVO]),
      '/api/inventario/vehiculos/1/reservar': {
        status: 409,
        body: { error: 'No se puede reservar un vehículo en estado reservado.' },
      },
    })
    const u = userEvent.setup()
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    await u.click(screen.getByRole('button', { name: /^Reservar/ }))
    const dlg = await screen.findByRole('dialog', { name: 'Reservar vehículo' })
    await u.type(within(dlg).getByLabelText('Motivo'), 'Esperando crédito')
    await u.click(within(dlg).getByRole('button', { name: 'Reservar' }))
    expect(await screen.findByText('No se puede reservar un vehículo en estado reservado.')).toBeInTheDocument()
  })
})

describe('Inactivar con motivo (CA-UI-11, CA-020-02, CA-020-03)', () => {
  const INACTIVAR = '/api/inventario/vehiculos/1/inactivar'

  it('motivo de 4 caracteres o solo espacios no se envía; con 5 se envía recortado', async () => {
    const api = abrirAsesor({
      [LISTA]: lista([V_ACTIVO]),
      [INACTIVAR]: { status: 200, body: { ...V_ACTIVO, estado: 'INACTIVO' } },
    })
    const u = userEvent.setup()
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    await u.click(screen.getByRole('button', { name: /^Inactivar/ }))
    const dlg = await screen.findByRole('dialog', { name: 'Inactivar vehículo' })
    const motivo = within(dlg).getByLabelText('Motivo')
    expect(motivo).toHaveFocus()
    await u.click(within(dlg).getByRole('button', { name: 'Inactivar' }))
    expect(within(dlg).getByText('Escribe el motivo (5 a 500 caracteres).')).toBeInTheDocument()
    await u.type(motivo, '   abcd   ')
    await u.click(within(dlg).getByRole('button', { name: 'Inactivar' }))
    expect(within(dlg).getByText('Escribe el motivo (5 a 500 caracteres).')).toBeInTheDocument()
    expect(api.llamadasA(INACTIVAR)).toHaveLength(0)
    await u.clear(motivo)
    await u.type(motivo, '  Venta externa  ')
    await u.click(within(dlg).getByRole('button', { name: 'Inactivar' }))
    expect(await screen.findByText('Chevrolet NPR 2020 quedó inactivo.')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(cuerpoJson(api.llamadasA(INACTIVAR)[0].init)).toEqual({ motivo: 'Venta externa' })
  })

  it('el motivo no admite más de 500 caracteres', async () => {
    abrirAsesor({ [LISTA]: lista([V_ACTIVO]) })
    const u = userEvent.setup()
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    await u.click(screen.getByRole('button', { name: /^Inactivar/ }))
    const dlg = await screen.findByRole('dialog')
    expect(within(dlg).getByLabelText('Motivo')).toHaveAttribute('maxLength', '500')
  })

  it('400 del servidor con errores.motivo: el mensaje queda en el diálogo', async () => {
    abrirAsesor({
      [LISTA]: lista([V_ACTIVO]),
      [INACTIVAR]: {
        status: 400,
        body: { error: 'Revisa los datos.', errores: { motivo: ['El motivo debe tener entre 5 y 500 caracteres.'] } },
      },
    })
    const u = userEvent.setup()
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    await u.click(screen.getByRole('button', { name: /^Inactivar/ }))
    const dlg = await screen.findByRole('dialog')
    await u.type(within(dlg).getByLabelText('Motivo'), 'Motivo válido')
    await u.click(within(dlg).getByRole('button', { name: 'Inactivar' }))
    expect(await within(dlg).findByText('El motivo debe tener entre 5 y 500 caracteres.')).toBeInTheDocument()
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })

  it('409 del servidor: se cierra el diálogo y se ve el mensaje', async () => {
    abrirAsesor({
      [LISTA]: lista([V_ACTIVO]),
      [INACTIVAR]: { status: 409, body: { error: 'No se puede inactivar un vehículo en estado vendido.' } },
    })
    const u = userEvent.setup()
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    await u.click(screen.getByRole('button', { name: /^Inactivar/ }))
    const dlg = await screen.findByRole('dialog')
    await u.type(within(dlg).getByLabelText('Motivo'), 'Se vendió fuera')
    await u.click(within(dlg).getByRole('button', { name: 'Inactivar' }))
    expect(await screen.findByText('No se puede inactivar un vehículo en estado vendido.')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('Cancelar (o Escape) cierra sin llamar a la API y devuelve el foco al botón', async () => {
    const api = abrirAsesor({ [LISTA]: lista([V_ACTIVO]) })
    const u = userEvent.setup()
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    const boton = screen.getByRole('button', { name: /^Inactivar/ })
    await u.click(boton)
    await u.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancelar' }))
    expect(screen.queryByRole('dialog')).toBeNull()
    await waitFor(() => expect(boton).toHaveFocus())
    await u.click(boton)
    await screen.findByRole('dialog')
    await u.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(api.llamadasA(INACTIVAR)).toHaveLength(0)
  })
})

describe('Vehículo recién guardado (mejora de la prueba manual de P4a)', () => {
  afterEach(() => olvidarVehiculoReciente())

  it('asesor: el vehículo recién creado o editado se resalta con el texto "Recién guardado"', async () => {
    marcarVehiculoReciente(2)
    abrirAsesor({ [LISTA]: lista([V_ACTIVO, V_RESERVADO]) })
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    const reciente = tarjeta('Kenworth T800 2018')
    expect(reciente).toHaveClass('inv-tarjeta-reciente')
    expect(within(reciente).getByText('Recién guardado')).toBeInTheDocument()
    const otro = tarjeta('Chevrolet NPR 2020')
    expect(otro).not.toHaveClass('inv-tarjeta-reciente')
    expect(within(otro).queryByText('Recién guardado')).toBeNull()
  })

  it('administrador: también se resalta en el inventario general', async () => {
    marcarVehiculoReciente(1)
    abrirAdmin({ [LISTA]: lista([V_ACTIVO, V_RESERVADO]) })
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    expect(within(tarjeta('Chevrolet NPR 2020')).getByText('Recién guardado')).toBeInTheDocument()
  })

  it('sin vehículo reciente no se resalta ninguno', async () => {
    abrirAsesor({ [LISTA]: lista([V_ACTIVO, V_RESERVADO]) })
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    expect(screen.queryByText('Recién guardado')).toBeNull()
  })
})

describe('Nota de la reserva visible en el inventario (D-28)', () => {
  it('un vehículo reservado muestra su nota en la tarjeta; uno activo no', async () => {
    abrirAsesor({ [LISTA]: lista([{ ...V_RESERVADO, notaReserva: 'Esperando crédito' }, V_ACTIVO]) })
    await screen.findByRole('heading', { level: 3, name: 'Kenworth T800 2018' })
    const reservado = tarjeta('Kenworth T800 2018')
    expect(within(reservado).getByText('Nota de la reserva')).toBeInTheDocument()
    expect(within(reservado).getByText('Esperando crédito')).toBeInTheDocument()
    expect(within(tarjeta('Chevrolet NPR 2020')).queryByText('Nota de la reserva')).toBeNull()
  })
})

describe('D-31: el motivo obligatorio del diálogo lleva asterisco', () => {
  it('reservar: nota "Campo obligatorio" y etiqueta Motivo con asterisco y aria-required', async () => {
    abrirAsesor({ [LISTA]: lista([V_ACTIVO]) })
    const u = userEvent.setup()
    await screen.findByRole('heading', { level: 3, name: 'Chevrolet NPR 2020' })
    await u.click(screen.getByRole('button', { name: /^Reservar/ }))
    const dlg = await screen.findByRole('dialog', { name: 'Reservar vehículo' })
    expect(within(dlg).getByText('Campo obligatorio')).toBeInTheDocument()
    expect(within(dlg).getByText('Motivo', { selector: 'label' })).toHaveClass('campo-obligatorio')
    expect(within(dlg).getByLabelText('Motivo')).toHaveAttribute('aria-required', 'true')
  })
})
