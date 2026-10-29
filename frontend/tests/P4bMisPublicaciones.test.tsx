// P4b — "Mis publicaciones" (#/publicaciones, HU-065): contador de cupo y destacados (D-38, D-40, D-41),
// estados en texto, acciones válidas por estado y avisos 409 del servidor. Fetch simulado.
import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { leerRuta } from '../src/rutas'
import { cabecera, TOKEN_FICTICIO } from './helpers'
import { abrirApp, CATALOGOS_INVENTARIO, RUTA_CATALOGOS_INV, type Rutas } from './p4pDatos'
import { cuerpo, cupo, paginaPub, pub, RUTA_CUPO, RUTA_LISTA_PUB, rutaListaPub, rutaPub } from './p4bDatos'

const P_PUBLICADA = pub(1, 11, { titulo: 'Camión NPR de trabajo' })
const P_DESTACADA = pub(2, 12, { titulo: 'Volqueta lista', destacada: true, slugPublico: 'volqueta-2' })
const P_VENCIDA = pub(3, 13, { titulo: 'Furgón vencido', estadoEfectivo: 'VENCIDA' })
const P_OCULTA = pub(4, 14, { titulo: 'Grúa en pausa', estado: 'OCULTA', estadoEfectivo: 'OCULTA', motivoOcultamiento: 'En reparación' })
const P_VENDIDA = pub(5, 15, { titulo: 'Tractomula vendida', estado: 'VENDIDA', estadoEfectivo: 'VENDIDA' })
const TODAS = [P_PUBLICADA, P_DESTACADA, P_VENCIDA, P_OCULTA, P_VENDIDA]

function abrirAsesor(rutas: Rutas = {}) {
  return abrirApp('#/publicaciones', ['ASESOR'], {
    [RUTA_LISTA_PUB]: paginaPub(TODAS),
    [RUTA_CUPO]: cupo(),
    ...rutas,
  })
}

function tarjeta(titulo: string): HTMLElement {
  return screen.getByRole('heading', { level: 3, name: titulo }).closest('li') as HTMLElement
}

function acciones(t: HTMLElement): string[] {
  return within(t)
    .queryAllByRole('button')
    .concat(within(t).queryAllByRole('link'))
    .map((b) => (b.textContent ?? '').replace(/\s+/g, ' ').trim())
}

describe('Ruta y acceso', () => {
  it('#/publicaciones se reconoce', () => {
    expect(leerRuta('#/publicaciones')).toEqual({ nombre: 'publicaciones' })
    expect(leerRuta('#/publicaciones/')).toEqual({ nombre: 'publicaciones' })
  })

  it('un comprador ve "No tienes permiso" y no se pide nada a /api/publicaciones', async () => {
    const { api } = abrirApp('#/publicaciones', ['COMPRADOR'])
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
    expect(api.llamadas.some((l) => l.url.startsWith('/api/publicaciones'))).toBe(false)
  })

  it('"Mi cuenta" del asesor enlaza a "Mis publicaciones"', async () => {
    abrirApp('#/cuenta', ['ASESOR'], { '/api/asesor/suscripcion': { status: 200, body: { suscripcion: null, mensaje: 'x' } } })
    expect(await screen.findByRole('link', { name: 'Mis publicaciones' })).toHaveAttribute('href', '#/publicaciones')
  })
})

describe('Contador de cupo y destacados', () => {
  it('muestra usadas de N, destacados de M y la fecha de fin del plan; el token va en la cabecera', async () => {
    const { api } = abrirAsesor()
    expect(await screen.findByText('Publicaciones: 3 de 5 · Destacados: 1 de 2')).toBeInTheDocument()
    expect(screen.getByText(/Tu plan vence el/)).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Mis publicaciones' })).toBeInTheDocument()
    expect(cabecera(api.llamadasA(RUTA_CUPO)[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
    expect(api.llamadas.some((l) => l.url.includes(TOKEN_FICTICIO))).toBe(false)
  })

  it('sin plan vigente: aviso claro y sin contador', async () => {
    abrirAsesor({ [RUTA_CUPO]: cupo({ planVigente: false, finPlan: null }) })
    expect(await screen.findByText('No tienes un plan activo. Comunícate con el administrador.')).toBeInTheDocument()
    expect(screen.queryByText(/Publicaciones: \d/)).toBeNull()
  })

  it('cupo lleno y destacados agotados: avisos claros', async () => {
    abrirAsesor({ [RUTA_CUPO]: cupo({ usadas: 5, limiteDestacados: 1, destacadasUsadas: 1 }) })
    expect(await screen.findByText('Ya usas las 5 publicaciones de tu plan. Elimina una para crear otra.')).toBeInTheDocument()
    expect(screen.getByText('Tu plan permite 1 destacado(s). Quita uno para destacar otra.')).toBeInTheDocument()
  })

  it('plan sin destacados: lo dice', async () => {
    abrirAsesor({ [RUTA_CUPO]: cupo({ limiteDestacados: 0, destacadasUsadas: 0 }) })
    expect(await screen.findByText('Tu plan no incluye destacados.')).toBeInTheDocument()
  })
})

describe('Estados y acciones válidas', () => {
  it('estado en texto y solo las acciones que corresponden a cada estado', async () => {
    abrirAsesor()
    await screen.findByRole('heading', { level: 3, name: 'Camión NPR de trabajo' })

    const publicada = tarjeta('Camión NPR de trabajo')
    expect(within(publicada).getByText('Publicada')).toBeInTheDocument()
    expect(acciones(publicada)).toEqual([
      'Editar Camión NPR de trabajo',
      'Inactivar Camión NPR de trabajo',
      'Renovar Camión NPR de trabajo',
      'Destacar Camión NPR de trabajo',
      'Ver en el portal Camión NPR de trabajo',
    ])
    expect(within(publicada).getByRole('link', { name: /Ver en el portal/ })).toHaveAttribute('href', '#/vehiculo/chevrolet-npr-2020-11')

    const destacada = tarjeta('Volqueta lista')
    expect(within(destacada).getByText('Destacada')).toBeInTheDocument()
    expect(acciones(destacada)).toContain('Quitar destacado Volqueta lista')
    expect(acciones(destacada)).not.toContain('Destacar Volqueta lista')

    const vencida = tarjeta('Furgón vencido')
    expect(within(vencida).getByText('Vencida')).toBeInTheDocument()
    expect(acciones(vencida)).toEqual(['Editar Furgón vencido', 'Inactivar Furgón vencido', 'Renovar Furgón vencido'])

    const oculta = tarjeta('Grúa en pausa')
    expect(within(oculta).getByText('Inactiva')).toBeInTheDocument()
    expect(within(oculta).getByText('En reparación')).toBeInTheDocument()
    expect(acciones(oculta)).toEqual(['Editar Grúa en pausa', 'Reactivar Grúa en pausa', 'Eliminar Grúa en pausa'])

    const vendida = tarjeta('Tractomula vendida')
    expect(within(vendida).getByText('Vendida')).toBeInTheDocument()
    expect(acciones(vendida)).toEqual(['Editar Tractomula vendida', 'Eliminar Tractomula vendida'])
  })

  it('lista vacía del asesor: explica cómo publicar', async () => {
    abrirAsesor({ [RUTA_LISTA_PUB]: paginaPub([]) })
    expect(await screen.findByText('Aún no tienes publicaciones. Publica un vehículo desde tu inventario.')).toBeInTheDocument()
  })
})

describe('Destacar y quitar destacado (D-38)', () => {
  it('Destacar llama a /destacar, avisa y recarga la lista y el contador', async () => {
    const { api, user } = abrirAsesor({ [rutaPub(1, 'destacar')]: { status: 200, body: pub(1, 11, { destacada: true }) } })
    await user.click(await screen.findByRole('button', { name: 'Destacar Camión NPR de trabajo' }))
    await waitFor(() => expect(api.llamadasA(rutaPub(1, 'destacar'))).toHaveLength(1))
    expect(api.llamadasA(rutaPub(1, 'destacar'))[0].init?.method).toBe('POST')
    expect(await screen.findByText('La publicación «Camión NPR de trabajo» ahora es destacada.')).toBeInTheDocument()
    await waitFor(() => expect(api.llamadasA(RUTA_CUPO)).toHaveLength(2))
    expect(api.llamadasA(RUTA_LISTA_PUB)).toHaveLength(2)
  })

  it('409 sin destacados disponibles: el mensaje del servidor', async () => {
    const mensaje = 'Tu plan permite 2 destacado(s). Quita uno para destacar otra.'
    const { user } = abrirAsesor({ [rutaPub(1, 'destacar')]: { status: 409, body: { error: mensaje } } })
    await user.click(await screen.findByRole('button', { name: 'Destacar Camión NPR de trabajo' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(mensaje)
  })

  it('Quitar destacado llama a /quitar-destacado', async () => {
    const ruta = rutaPub(2, 'quitar-destacado')
    const { api, user } = abrirAsesor({ [ruta]: { status: 200, body: {} } })
    await user.click(await screen.findByRole('button', { name: 'Quitar destacado Volqueta lista' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(await screen.findByText('La publicación «Volqueta lista» ya no es destacada.')).toBeInTheDocument()
  })
})

describe('Renovar, inactivar, reactivar y eliminar', () => {
  it('Renovar una vencida llama a /renovar; 409 sin plan muestra el mensaje', async () => {
    const ruta = rutaPub(3, 'renovar')
    const { api, user } = abrirAsesor({
      [ruta]: { status: 409, body: { error: 'No tienes un plan activo. Comunícate con el administrador.' } },
    })
    await user.click(await screen.findByRole('button', { name: 'Renovar Furgón vencido' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(await screen.findByRole('alert')).toHaveTextContent('No tienes un plan activo. Comunícate con el administrador.')
  })

  it('Inactivar pide motivo (5 a 500) y envía { motivo }', async () => {
    const ruta = rutaPub(1, 'inactivar')
    const { api, user } = abrirAsesor({ [ruta]: { status: 200, body: {} } })
    await user.click(await screen.findByRole('button', { name: 'Inactivar Camión NPR de trabajo' }))
    const dialogo = screen.getByRole('dialog', { name: 'Inactivar publicación' })
    expect(dialogo).toHaveTextContent('Sigue ocupando un cupo de tu plan.')
    await user.type(within(dialogo).getByLabelText('Motivo'), 'ya')
    await user.click(within(dialogo).getByRole('button', { name: 'Inactivar' }))
    expect(within(dialogo).getByText('Escribe el motivo (5 a 500 caracteres).')).toBeInTheDocument()
    expect(api.llamadasA(ruta)).toHaveLength(0)
    await user.type(within(dialogo).getByLabelText('Motivo'), ' no está disponible por ahora')
    await user.click(within(dialogo).getByRole('button', { name: 'Inactivar' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(cuerpo(api.llamadasA(ruta)[0].init)).toEqual({ motivo: 'ya no está disponible por ahora' })
    expect(await screen.findByText('La publicación «Camión NPR de trabajo» quedó inactiva.')).toBeInTheDocument()
  })

  it('Eliminar explica que libera cupo; Cancelar no envía nada; Confirmar llama a /eliminar', async () => {
    const ruta = rutaPub(4, 'eliminar')
    const { api, user } = abrirAsesor({ [ruta]: { status: 200, body: {} } })
    const boton = await screen.findByRole('button', { name: 'Eliminar Grúa en pausa' })
    await user.click(boton)
    let dialogo = screen.getByRole('dialog', { name: 'Eliminar publicación' })
    expect(dialogo).toHaveTextContent('libera un cupo de tu plan')
    await user.click(within(dialogo).getByRole('button', { name: 'Cancelar' }))
    await waitFor(() => expect(boton).toHaveFocus())
    expect(api.llamadasA(ruta)).toHaveLength(0)

    await user.click(boton)
    dialogo = screen.getByRole('dialog', { name: 'Eliminar publicación' })
    await user.click(within(dialogo).getByRole('button', { name: 'Eliminar' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(await screen.findByText('Se eliminó la publicación «Grúa en pausa» y se liberó un cupo de tu plan.')).toBeInTheDocument()
  })

  it('Reactivar pide confirmación; 409 sin plan cierra el diálogo y muestra el mensaje', async () => {
    const ruta = rutaPub(4, 'reactivar')
    const { user } = abrirAsesor({
      [ruta]: { status: 409, body: { error: 'No tienes un plan activo. Comunícate con el administrador.' } },
    })
    await user.click(await screen.findByRole('button', { name: 'Reactivar Grúa en pausa' }))
    await user.click(within(screen.getByRole('dialog', { name: 'Reactivar publicación' })).getByRole('button', { name: 'Reactivar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('No tienes un plan activo.')
    expect(screen.queryByRole('dialog')).toBeNull()
  })
})

describe('Editar (HU-024)', () => {
  it('carga el detalle, muestra sus datos y envía PUT con título, descripción y negociable', async () => {
    const ruta = rutaPub(1)
    const { api, user } = abrirAsesor({
      [ruta]: [
        { status: 200, body: { ...P_PUBLICADA, descripcionComercial: 'Buen estado.', negociable: true } },
        { status: 200, body: P_PUBLICADA },
      ],
    })
    await user.click(await screen.findByRole('button', { name: 'Editar Camión NPR de trabajo' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Editar publicación' })
    expect(within(dialogo).getByLabelText('Título de la publicación')).toHaveValue('Camión NPR de trabajo')
    expect(within(dialogo).getByLabelText('Descripción comercial (opcional)')).toHaveValue('Buen estado.')
    expect(within(dialogo).getByLabelText('El precio es negociable')).toBeChecked()
    await user.click(within(dialogo).getByLabelText('El precio es negociable'))
    await user.clear(within(dialogo).getByLabelText('Descripción comercial (opcional)'))
    await user.click(within(dialogo).getByRole('button', { name: 'Guardar cambios' }))
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(2))
    const put = api.llamadasA(ruta)[1]
    expect(put.init?.method).toBe('PUT')
    expect(cuerpo(put.init)).toEqual({ titulo: 'Camión NPR de trabajo', descripcionComercial: null, negociable: false })
    expect(await screen.findByText('Se guardaron los cambios de la publicación «Camión NPR de trabajo».')).toBeInTheDocument()
  })
})

describe('Administrador', () => {
  it('ve todas, con el asesor y el filtro por asesor; no pide el cupo', async () => {
    const conAsesor = pub(9, 19, { titulo: 'Camión de Asesor Dos', asesor: { id: 8, nombre: 'Asesor Dos' } })
    const { api, user } = abrirApp('#/publicaciones', ['ADMINISTRADOR'], {
      [RUTA_LISTA_PUB]: paginaPub([conAsesor]),
      [rutaListaPub('asesorId=8')]: paginaPub([conAsesor]),
      [RUTA_CATALOGOS_INV]: CATALOGOS_INVENTARIO,
    })
    expect(await screen.findByRole('heading', { level: 1, name: 'Publicaciones' })).toBeInTheDocument()
    const t = await screen.findByRole('heading', { level: 3, name: 'Camión de Asesor Dos' })
    expect(within(t.closest('li') as HTMLElement).getByText('Asesor Dos')).toBeInTheDocument()
    const filtro = screen.getByLabelText('Asesor')
    await waitFor(() => expect(within(filtro).getAllByRole('option')).toHaveLength(3))
    await user.selectOptions(filtro, '8')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => expect(api.llamadasA(rutaListaPub('asesorId=8'))).toHaveLength(1))
    expect(api.llamadasA(RUTA_CUPO)).toHaveLength(0)
  })

  it('el asesor no ve el filtro por asesor', async () => {
    abrirAsesor()
    await screen.findByRole('heading', { level: 3, name: 'Camión NPR de trabajo' })
    expect(screen.queryByLabelText('Asesor')).toBeNull()
  })
})
