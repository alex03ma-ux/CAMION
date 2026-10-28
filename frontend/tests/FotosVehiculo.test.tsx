// P4a: fotos del vehículo (CA-UI-12, CA-UI-13; HU-014, RN-VE-001, D-20).
// Se monta el componente solo, con un contenedor que guarda las fotos como lo hace el formulario.
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import type { FotoInventario } from '../src/api/inventario'
import { FotosVehiculo } from '../src/components/FotosVehiculo'
import { cabecera, simularFetch, TOKEN_FICTICIO } from './helpers'
import { cuerpoJson } from './inventarioDatos'

const BASE = '/api/inventario/vehiculos/9/fotos'
const ORDEN = `${BASE}/orden`
const FORMATO = 'Sube una imagen JPG, PNG o WebP de hasta 5 MB.'
const LLENAS = 'Ya hay 10 fotos; elimina alguna para subir otra.'

function foto(id: number, orden: number): FotoInventario {
  return { id, url: `/media/vehiculos/9/f${id}.jpg`, orden, esPortada: orden === 1 }
}

function fotos(n: number): FotoInventario[] {
  return Array.from({ length: n }, (_, i) => foto(i + 1, i + 1))
}

function Contenedor({
  iniciales,
  bloqueado = false,
  onSesionTerminada,
}: {
  iniciales: FotoInventario[]
  bloqueado?: boolean
  onSesionTerminada: () => void
}) {
  const [lista, setLista] = useState(iniciales)
  return (
    <FotosVehiculo
      token={TOKEN_FICTICIO}
      vehiculoId={9}
      fotos={lista}
      bloqueado={bloqueado}
      onCambio={setLista}
      onSesionTerminada={onSesionTerminada}
    />
  )
}

function montar(iniciales: FotoInventario[], bloqueado = false) {
  const onSesionTerminada = vi.fn()
  render(<Contenedor iniciales={iniciales} bloqueado={bloqueado} onSesionTerminada={onSesionTerminada} />)
  return { onSesionTerminada, u: userEvent.setup({ applyAccept: false }) }
}

function png(nombre: string, bytes = 10): File {
  return new File([new Uint8Array(bytes)], nombre, { type: 'image/png' })
}

function contador(texto: string) {
  return screen.getByRole('heading', { level: 2, name: new RegExp(`\\(${texto}\\)`) })
}

describe('Selector y contador (CA-UI-12)', () => {
  it('selector con etiqueta, tipos aceptados, múltiple, contador "N de 10" y portada indicada con texto', () => {
    montar(fotos(2))
    const input = screen.getByLabelText('Agregar fotos')
    expect(input).toHaveAttribute('type', 'file')
    expect(input).toHaveAttribute('accept', 'image/jpeg,image/png,image/webp')
    expect(input).toHaveAttribute('multiple')
    expect(contador('2 de 10')).toBeInTheDocument()
    expect(screen.getByText(/Puedes agregar 8 más\./)).toBeInTheDocument()
    expect(screen.getByAltText('Foto 1 de 2 (portada)')).toHaveAttribute('src', '/media/vehiculos/9/f1.jpg')
    expect(screen.getByAltText('Foto 2 de 2')).toBeInTheDocument()
    expect(screen.getByText('Portada')).toBeInTheDocument()
  })

  it('con 10 fotos el selector se deshabilita con aviso', () => {
    montar(fotos(10))
    expect(screen.getByLabelText('Agregar fotos')).toBeDisabled()
    expect(screen.getByText(LLENAS)).toBeInTheDocument()
    expect(contador('10 de 10')).toBeInTheDocument()
  })

  it('sin fotos: mensaje de vacío', () => {
    montar([])
    expect(
      screen.getByText('Este vehículo aún no tiene fotos. Agrega al menos una para poder guardar cambios.'),
    ).toBeInTheDocument()
    expect(contador('0 de 10')).toBeInTheDocument()
  })
})

describe('Rechazo antes de enviar (CA-UI-12)', () => {
  it('otro tipo, más de 5 MB o vacío: se rechaza sin llamar a la API', async () => {
    const api = simularFetch({})
    const { u } = montar([])
    await u.upload(screen.getByLabelText('Agregar fotos'), [
      new File(['%PDF'], 'contrato.pdf', { type: 'application/pdf' }),
      new File(['GIF8'], 'animada.gif', { type: 'image/gif' }),
      png('grande.png', 5 * 1024 * 1024 + 1),
      png('vacia.png', 0),
    ])
    expect(screen.getByText(`contrato.pdf: ${FORMATO}`)).toBeInTheDocument()
    expect(screen.getByText(`animada.gif: ${FORMATO}`)).toBeInTheDocument()
    expect(screen.getByText(`grande.png: ${FORMATO}`)).toBeInTheDocument()
    expect(screen.getByText(`vacia.png: ${FORMATO}`)).toBeInTheDocument()
    expect(screen.getByLabelText('Agregar fotos')).toHaveAttribute('aria-invalid', 'true')
    expect(api.llamadasA(BASE)).toHaveLength(0)
  })

  it('una foto de exactamente 5 MB sí se envía', async () => {
    const api = simularFetch({ [BASE]: { status: 201, body: foto(1, 1) } })
    const { u } = montar([])
    await u.upload(screen.getByLabelText('Agregar fotos'), png('justa.png', 5 * 1024 * 1024))
    expect(await screen.findByText(/^Se subió 1 foto\. Las fotos se guardan solas; cuando termines, pulsa «Listo»/)).toBeInTheDocument()
    expect(api.llamadasA(BASE)).toHaveLength(1)
  })

  it('con 9 fotos y 2 elegidas: sube 1 y avisa de la que no cabe', async () => {
    const api = simularFetch({ [BASE]: { status: 201, body: foto(10, 10) } })
    const { u } = montar(fotos(9))
    await u.upload(screen.getByLabelText('Agregar fotos'), [png('a.png'), png('b.png')])
    expect(await screen.findByText(/^Se subió 1 foto\. Las fotos se guardan solas; cuando termines, pulsa «Listo»/)).toBeInTheDocument()
    expect(screen.getByText('Solo caben 1 foto más; no se subieron: b.png.')).toBeInTheDocument()
    expect(api.llamadasA(BASE)).toHaveLength(1)
    expect(contador('10 de 10')).toBeInTheDocument()
  })
})

describe('Subida (CA-014-01, CA-UI-12)', () => {
  it('FormData con el campo "archivo", token y sin Content-Type; el contador sube', async () => {
    const api = simularFetch({ [BASE]: [{ status: 201, body: foto(3, 2) }, { status: 201, body: foto(4, 3) }] })
    const { u } = montar(fotos(1))
    const a = png('frente.png')
    const b = new File([new Uint8Array(10)], 'lado.webp', { type: 'image/webp' })
    await u.upload(screen.getByLabelText('Agregar fotos'), [a, b])
    expect(await screen.findByText(/^Se subieron 2 fotos\. Las fotos se guardan solas/)).toBeInTheDocument()
    const [p1, p2] = api.llamadasA(BASE)
    expect(p1.init?.method).toBe('POST')
    const datos = p1.init?.body as FormData
    expect(datos).toBeInstanceOf(FormData)
    expect(Array.from(datos.keys())).toEqual(['archivo'])
    expect((datos.get('archivo') as File).name).toBe('frente.png')
    expect(((p2.init?.body as FormData).get('archivo') as File).name).toBe('lado.webp')
    expect(cabecera(p1.init, 'Content-Type')).toBeUndefined()
    expect(cabecera(p1.init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
    expect(contador('3 de 10')).toBeInTheDocument()
  })

  it('muestra el progreso en texto mientras sube', async () => {
    let soltar: (r: Response) => void = () => {}
    vi.stubGlobal(
      'fetch',
      vi.fn(() => new Promise<Response>((res) => (soltar = res))),
    )
    const { u } = montar([])
    await u.upload(screen.getByLabelText('Agregar fotos'), png('a.png'))
    expect(await screen.findByText('Subiendo foto 1 de 1...')).toBeInTheDocument()
    expect(screen.getByLabelText('Progreso de la subida')).toBeInTheDocument()
    soltar(new Response(JSON.stringify(foto(1, 1)), { status: 201, headers: { 'Content-Type': 'application/json' } }))
    expect(await screen.findByText(/^Se subió 1 foto\. Las fotos se guardan solas; cuando termines, pulsa «Listo»/)).toBeInTheDocument()
    expect(screen.queryByText(/Subiendo foto/)).toBeNull()
  })

  it('400 del servidor: el mensaje se muestra junto al nombre del archivo', async () => {
    simularFetch({ [BASE]: { status: 400, body: { error: FORMATO } } })
    const { u } = montar([])
    await u.upload(screen.getByLabelText('Agregar fotos'), png('falsa.png'))
    expect(await screen.findByText(`falsa.png: ${FORMATO}`)).toBeInTheDocument()
    expect(contador('0 de 10')).toBeInTheDocument()
  })

  it('409 del servidor: se detiene y muestra "Ya hay 10 fotos..."', async () => {
    const api = simularFetch({ [BASE]: { status: 409, body: { error: LLENAS } } })
    const { u } = montar(fotos(2))
    await u.upload(screen.getByLabelText('Agregar fotos'), [png('a.png'), png('b.png')])
    expect(await screen.findByText(`a.png: ${LLENAS}`)).toBeInTheDocument()
    expect(api.llamadasA(BASE)).toHaveLength(1)
  })

  it('413 sin cuerpo: mensaje de formato y tamaño', async () => {
    simularFetch({ [BASE]: { status: 413 } })
    const { u } = montar([])
    await u.upload(screen.getByLabelText('Agregar fotos'), png('pesada.png'))
    expect(await screen.findByText(`pesada.png: ${FORMATO}`)).toBeInTheDocument()
  })

  it('401 al subir: termina la sesión', async () => {
    simularFetch({ [BASE]: { status: 401 } })
    const { u, onSesionTerminada } = montar([])
    await u.upload(screen.getByLabelText('Agregar fotos'), png('a.png'))
    await waitFor(() => expect(onSesionTerminada).toHaveBeenCalledTimes(1))
  })
})

describe('Orden y portada (CA-UI-13, CA-014-09)', () => {
  it('la primera no puede subir y la última no puede bajar; la portada no tiene "Hacer portada"', () => {
    montar(fotos(3))
    expect(screen.getByRole('button', { name: 'Subir foto 1' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Bajar foto 1' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Bajar foto 3' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Subir foto 3' })).toBeEnabled()
    expect(screen.queryByRole('button', { name: 'Hacer portada la foto 1' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Hacer portada la foto 2' })).toBeInTheDocument()
  })

  it('"Bajar" con teclado envía el nuevo orden por PUT y la nueva primera es la portada', async () => {
    const api = simularFetch({
      [ORDEN]: { status: 200, body: [foto(2, 1), foto(1, 2), foto(3, 3)] },
    })
    const { u } = montar(fotos(3))
    screen.getByRole('button', { name: 'Bajar foto 1' }).focus()
    await u.keyboard('{Enter}')
    expect(await screen.findByText('Foto movida a la posición 2.')).toBeInTheDocument()
    const put = api.llamadasA(ORDEN)[0]
    expect(put.init?.method).toBe('PUT')
    expect(cuerpoJson(put.init)).toEqual({ fotoIds: [2, 1, 3] })
    expect(screen.getByAltText('Foto 1 de 3 (portada)')).toHaveAttribute('src', '/media/vehiculos/9/f2.jpg')
  })

  it('"Subir" la foto 2 la deja primera y anuncia que es la portada', async () => {
    const api = simularFetch({ [ORDEN]: { status: 200, body: [foto(2, 1), foto(1, 2)] } })
    const { u } = montar(fotos(2))
    await u.click(screen.getByRole('button', { name: 'Subir foto 2' }))
    expect(await screen.findByText('Foto movida a la posición 1; ahora es la portada.')).toBeInTheDocument()
    expect(cuerpoJson(api.llamadasA(ORDEN)[0].init)).toEqual({ fotoIds: [2, 1] })
  })

  it('"Hacer portada" pone la foto primera', async () => {
    const api = simularFetch({ [ORDEN]: { status: 200, body: [foto(3, 1), foto(1, 2), foto(2, 3)] } })
    const { u } = montar(fotos(3))
    await u.click(screen.getByRole('button', { name: 'Hacer portada la foto 3' }))
    expect(await screen.findByText('La foto ahora es la portada.')).toBeInTheDocument()
    expect(cuerpoJson(api.llamadasA(ORDEN)[0].init)).toEqual({ fotoIds: [3, 1, 2] })
  })

  it('error del servidor al reordenar: se muestra y el orden no cambia', async () => {
    simularFetch({ [ORDEN]: { status: 400, body: { error: 'El orden debe incluir todas las fotos.' } } })
    const { u } = montar(fotos(2))
    await u.click(screen.getByRole('button', { name: 'Bajar foto 1' }))
    expect(await screen.findByText('El orden debe incluir todas las fotos.')).toBeInTheDocument()
    expect(screen.getByAltText('Foto 1 de 2 (portada)')).toHaveAttribute('src', '/media/vehiculos/9/f1.jpg')
  })
})

describe('Eliminar con confirmación (CA-UI-13, CA-014-07)', () => {
  it('Cancelar no borra; Eliminar envía DELETE, renumera y la siguiente pasa a portada', async () => {
    const api = simularFetch({ [`${BASE}/1`]: { status: 204 } })
    const { u } = montar(fotos(3))
    await u.click(screen.getByRole('button', { name: 'Eliminar foto 1' }))
    let dlg = await screen.findByRole('dialog', { name: 'Eliminar foto' })
    expect(within(dlg).getByText(/La siguiente foto pasará a ser la portada\./)).toBeInTheDocument()
    await u.click(within(dlg).getByRole('button', { name: 'Cancelar' }))
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(api.llamadasA(`${BASE}/1`)).toHaveLength(0)

    await u.click(screen.getByRole('button', { name: 'Eliminar foto 1' }))
    dlg = await screen.findByRole('dialog', { name: 'Eliminar foto' })
    await u.click(within(dlg).getByRole('button', { name: 'Eliminar' }))
    expect(await screen.findByText('Foto eliminada.')).toBeInTheDocument()
    expect(api.llamadasA(`${BASE}/1`)[0].init?.method).toBe('DELETE')
    expect(contador('2 de 10')).toBeInTheDocument()
    expect(screen.getByAltText('Foto 1 de 2 (portada)')).toHaveAttribute('src', '/media/vehiculos/9/f2.jpg')
  })

  it('404 al eliminar: "Foto no encontrada." y se quita de la lista', async () => {
    simularFetch({ [`${BASE}/2`]: { status: 404 } })
    const { u } = montar(fotos(2))
    await u.click(screen.getByRole('button', { name: 'Eliminar foto 2' }))
    await u.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Eliminar' }))
    expect(await screen.findByText('Foto no encontrada.')).toBeInTheDocument()
    expect(contador('1 de 10')).toBeInTheDocument()
  })

  it('500 al eliminar: el error queda en el diálogo y la foto sigue', async () => {
    simularFetch({ [`${BASE}/2`]: { status: 500 } })
    const { u } = montar(fotos(2))
    await u.click(screen.getByRole('button', { name: 'Eliminar foto 2' }))
    const dlg = await screen.findByRole('dialog')
    await u.click(within(dlg).getByRole('button', { name: 'Eliminar' }))
    expect(await within(dlg).findByText(/No se pudo conectar con el servidor/)).toBeInTheDocument()
    expect(contador('2 de 10')).toBeInTheDocument()
  })
})

describe('Bloqueado (VENDIDO, CA-014-11)', () => {
  it('sin selector ni botones de gestión', () => {
    montar(fotos(2), true)
    expect(screen.queryByLabelText('Agregar fotos')).toBeNull()
    expect(screen.queryAllByRole('button')).toHaveLength(0)
    expect(contador('2 de 10')).toBeInTheDocument()
  })
})

describe('D-30: debe quedar al menos una foto', () => {
  const ULTIMA = 'El vehículo debe tener al menos una foto. Sube otra antes de eliminar esta.'

  it('con una sola foto, "Eliminar" está desactivado con la explicación y no se llama a la API', async () => {
    const api = simularFetch({})
    const { u } = montar(fotos(1))
    const eliminar = screen.getByRole('button', { name: 'Eliminar foto 1' })
    expect(eliminar).toBeDisabled()
    const ayuda = screen.getByText('Debe quedar al menos una foto. Sube otra antes de eliminar esta.')
    expect(eliminar.getAttribute('aria-describedby')).toBe(ayuda.id)
    await u.click(eliminar)
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(api.llamadas.filter((l) => l.init?.method === 'DELETE')).toHaveLength(0)
  })

  it('con dos fotos se puede eliminar una; la que queda ya no se puede eliminar', async () => {
    simularFetch({ [`${BASE}/2`]: { status: 204 } })
    const { u } = montar(fotos(2))
    expect(screen.getByRole('button', { name: 'Eliminar foto 1' })).toBeEnabled()
    await u.click(screen.getByRole('button', { name: 'Eliminar foto 2' }))
    await u.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Eliminar' }))
    expect(await screen.findByText('Foto eliminada.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Eliminar foto 1' })).toBeDisabled()
  })

  it('409 del servidor al eliminar (no quedaría ninguna): se muestra el mensaje y la foto sigue', async () => {
    simularFetch({ [`${BASE}/2`]: { status: 409, body: { error: ULTIMA } } })
    const { u } = montar(fotos(2))
    await u.click(screen.getByRole('button', { name: 'Eliminar foto 2' }))
    await u.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Eliminar' }))
    expect(await screen.findByText(ULTIMA)).toBeInTheDocument()
    expect(contador('2 de 10')).toBeInTheDocument()
  })
})
