// P4a: formulario de registro y edición de vehículos (CA-UI-05 a CA-UI-10, HU-013,
// HU-015 a HU-019, D-17, D-19, RN-VE-003, RN-VE-007).
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { simularFetch } from './helpers'
import {
  abrirConSesion,
  CAT,
  CAT_ADMIN,
  CATALOGOS,
  cuerpoJson,
  detalleCamion,
  ME,
  me,
  VEHICULOS,
} from './inventarioDatos'

const DETALLE_9 = '/api/inventario/vehiculos/9'
type U = ReturnType<typeof userEvent.setup>

function abrirNuevo(roles: string[], rutas: Parameters<typeof simularFetch>[0] = {}) {
  const api = simularFetch({
    [ME]: me(roles),
    [CATALOGOS]: { status: 200, body: roles.includes('ADMINISTRADOR') ? CAT_ADMIN : CAT },
    ...rutas,
  })
  abrirConSesion('#/inventario/nuevo')
  return api
}

function formulario(): HTMLFormElement {
  return screen.getByRole('button', { name: /Registrar vehículo|Guardar cambios/ }).closest('form') as HTMLFormElement
}

function etiquetas(): string[] {
  return Array.from(formulario().querySelectorAll('label')).map((l) => l.textContent ?? '')
}

async function llenarCamion(u: U) {
  await u.selectOptions(screen.getByLabelText('Marca'), '10')
  await u.selectOptions(screen.getByLabelText('Línea (modelo)'), '100')
  await u.type(screen.getByLabelText('Año'), '2020')
  await u.type(screen.getByLabelText('Kilometraje (km)'), '85000')
  await u.type(screen.getByLabelText('Placa (opcional)'), 'abc-123')
  await u.selectOptions(screen.getByLabelText('Departamento'), '20')
  await u.selectOptions(screen.getByLabelText('Municipio'), '200')
  await u.type(screen.getByLabelText('Precio de venta (COP)'), '120000000')
}

describe('Categoría primero y campos por categoría (CA-UI-05)', () => {
  it('antes de elegir la categoría no hay campos y no se puede enviar; solo las 4 categorías', async () => {
    abrirNuevo(['ASESOR'])
    const cat = await screen.findByLabelText('Categoría')
    expect(within(cat).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Elige la categoría',
      'Carro',
      'Camión',
      'Carrocería',
      'Maquinaria',
    ])
    expect(screen.getByText('Elige una categoría para ver los datos que debes completar.')).toBeInTheDocument()
    expect(screen.queryByLabelText('Marca')).toBeNull()
    expect(screen.queryByLabelText('Precio de venta (COP)')).toBeNull()
    expect(screen.getByRole('button', { name: 'Registrar vehículo' })).toBeDisabled()
    expect(screen.queryByLabelText('Asesor propietario')).toBeNull()
    expect(screen.getByText('Podrás agregar las fotos después de registrar el vehículo.')).toBeInTheDocument()
  })

  const ESPERADO: Record<string, string[]> = {
    '1': [
      'Categoría',
      'Marca',
      'Línea (modelo)',
      'Año',
      'Carrocería (subtipo) (opcional)',
      'Placa (opcional)',
      'Kilometraje (km)',
      'Combustible (opcional)',
      'Cilindraje (cc) (opcional)',
      'Peso bruto (kg) (opcional)',
      'Departamento',
      'Municipio',
      'Precio de venta (COP)',
      'Descripción técnica (opcional)',
    ],
    '2': [
      'Categoría',
      'Marca',
      'Línea (modelo)',
      'Año',
      'Carrocería (subtipo) (opcional)',
      'Placa (opcional)',
      'Kilometraje (km)',
      'Combustible (opcional)',
      'Cilindraje (cc) (opcional)',
      'Peso bruto (kg) (opcional)',
      'Capacidad de carga (kg) (opcional)',
      'Departamento',
      'Municipio',
      'Precio de venta (COP)',
      'Descripción técnica (opcional)',
    ],
    '3': [
      'Categoría',
      'Marca (fabricante) (opcional)',
      'Línea (modelo) (opcional)',
      'Año (opcional)',
      'Tipo de carrocería',
      'Largo (m) (opcional)',
      'Ancho (m) (opcional)',
      'Alto (m) (opcional)',
      'Material (opcional)',
      'Capacidad de carga (kg) (opcional)',
      'Departamento',
      'Municipio',
      'Precio de venta (COP)',
      'Descripción técnica (opcional)',
    ],
    '4': [
      'Categoría',
      'Marca',
      'Línea (modelo)',
      'Año',
      'Tipo de máquina',
      'Placa (opcional)',
      'Horas de uso (horómetro) (opcional)',
      'Combustible (opcional)',
      'Cilindraje (cc) (opcional)',
      'Peso bruto (kg) (opcional)',
      'Capacidad (kg) (opcional)',
      'Departamento',
      'Municipio',
      'Precio de venta (COP)',
      'Descripción técnica (opcional)',
    ],
  }
  const NOMBRE: Record<string, string> = { '1': 'Carro', '2': 'Camión', '3': 'Carrocería', '4': 'Maquinaria' }

  for (const tipoId of ['1', '2', '3', '4']) {
    it(`${NOMBRE[tipoId]}: solo los campos que aplican, con su obligatoriedad`, async () => {
      abrirNuevo(['ASESOR'])
      const u = userEvent.setup()
      await u.selectOptions(await screen.findByLabelText('Categoría'), tipoId)
      expect(etiquetas()).toEqual(ESPERADO[tipoId])
      for (const texto of ESPERADO[tipoId]) {
        const control = screen.getByLabelText(texto)
        if (texto.endsWith('(opcional)')) expect(control, texto).not.toHaveAttribute('aria-required')
        else expect(control, texto).toHaveAttribute('aria-required', 'true')
      }
    })
  }
})

describe('Listas dependientes (CA-UI-07)', () => {
  it('línea filtrada por marca y se vacía al cambiar de marca', async () => {
    abrirNuevo(['ASESOR'])
    const u = userEvent.setup()
    await u.selectOptions(await screen.findByLabelText('Categoría'), '2')
    const linea = screen.getByLabelText('Línea (modelo)')
    expect(linea).toBeDisabled()
    await u.selectOptions(screen.getByLabelText('Marca'), '10')
    expect(linea).toBeEnabled()
    expect(within(linea).getAllByRole('option').map((o) => o.textContent)).toEqual(['Sin línea', 'NPR', 'NHR'])
    await u.selectOptions(linea, '100')
    expect(linea).toHaveValue('100')
    await u.selectOptions(screen.getByLabelText('Marca'), '11')
    expect(linea).toHaveValue('')
    expect(within(linea).getAllByRole('option').map((o) => o.textContent)).toEqual(['Sin línea', 'T800'])
  })

  it('municipio filtrado por departamento y se vacía al cambiar de departamento', async () => {
    abrirNuevo(['ASESOR'])
    const u = userEvent.setup()
    await u.selectOptions(await screen.findByLabelText('Categoría'), '3')
    const municipio = screen.getByLabelText('Municipio')
    expect(municipio).toBeDisabled()
    await u.selectOptions(screen.getByLabelText('Departamento'), '20')
    expect(within(municipio).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Elige el municipio',
      'Medellín',
    ])
    await u.selectOptions(municipio, '200')
    await u.selectOptions(screen.getByLabelText('Departamento'), '21')
    expect(municipio).toHaveValue('')
    expect(within(municipio).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Elige el municipio',
      'Bogotá',
    ])
  })
})

describe('Precio con miles (CA-UI-08)', () => {
  it('muestra "120.000.000" y envía 120000000; ignora letras', async () => {
    const api = abrirNuevo(['ASESOR'], { [VEHICULOS]: { status: 400, body: { error: 'x' } } })
    const u = userEvent.setup()
    await u.selectOptions(await screen.findByLabelText('Categoría'), '2')
    await llenarCamion(u)
    const precio = screen.getByLabelText('Precio de venta (COP)')
    expect(precio).toHaveValue('120.000.000')
    await u.type(precio, 'abc')
    expect(precio).toHaveValue('120.000.000')
    await u.click(screen.getByRole('button', { name: 'Registrar vehículo' }))
    await waitFor(() => expect(api.llamadasA(VEHICULOS)).toHaveLength(1))
    expect((cuerpoJson(api.llamadasA(VEHICULOS)[0].init) as { precioCop: number }).precioCop).toBe(120000000)
  })
})

describe('Validación en cliente', () => {
  it('Camión vacío: errores por campo, aviso general, foco en el primero y nada se envía', async () => {
    const api = abrirNuevo(['ASESOR'])
    const u = userEvent.setup()
    await u.selectOptions(await screen.findByLabelText('Categoría'), '2')
    await u.click(screen.getByRole('button', { name: 'Registrar vehículo' }))
    expect(screen.getByText('Revisa los datos marcados.')).toBeInTheDocument()
    expect(screen.getByText('Elige la marca.')).toBeInTheDocument()
    expect(screen.getByText(/Escribe un año entre 1950 y \d{4}\./)).toBeInTheDocument()
    expect(screen.getByText('Escribe el kilometraje (número entero mayor o igual a 0).')).toBeInTheDocument()
    expect(screen.getByText('Elige el departamento.')).toBeInTheDocument()
    expect(screen.getByText('Elige el municipio.')).toBeInTheDocument()
    expect(screen.getByText(/Escribe el precio de venta/)).toBeInTheDocument()
    const marca = screen.getByLabelText('Marca')
    expect(marca).toHaveAttribute('aria-invalid', 'true')
    expect(marca.getAttribute('aria-describedby')).toContain('inv-marcaId-error')
    await waitFor(() => expect(marca).toHaveFocus())
    expect(api.llamadasA(VEHICULOS)).toHaveLength(0)
  })

  it('placa inválida y año fuera de rango se marcan antes de enviar', async () => {
    const api = abrirNuevo(['ASESOR'])
    const u = userEvent.setup()
    await u.selectOptions(await screen.findByLabelText('Categoría'), '2')
    await llenarCamion(u)
    await u.clear(screen.getByLabelText('Placa (opcional)'))
    await u.type(screen.getByLabelText('Placa (opcional)'), 'AB@1')
    await u.clear(screen.getByLabelText('Año'))
    await u.type(screen.getByLabelText('Año'), '1949')
    await u.click(screen.getByRole('button', { name: 'Registrar vehículo' }))
    expect(screen.getByText('Escribe una placa válida (letras y números).')).toBeInTheDocument()
    expect(screen.getByText(/Escribe un año entre 1950/)).toBeInTheDocument()
    expect(api.llamadasA(VEHICULOS)).toHaveLength(0)
  })

  it('Carrocería sin tipo de carrocería: "Elige el tipo de carrocería."', async () => {
    abrirNuevo(['ASESOR'])
    const u = userEvent.setup()
    await u.selectOptions(await screen.findByLabelText('Categoría'), '3')
    await u.click(screen.getByRole('button', { name: 'Registrar vehículo' }))
    expect(screen.getByText('Elige el tipo de carrocería.')).toBeInTheDocument()
    expect(screen.queryByText('Elige la marca.')).toBeNull()
  })

  it('Maquinaria sin tipo de máquina: "Elige el tipo de máquina."', async () => {
    abrirNuevo(['ASESOR'])
    const u = userEvent.setup()
    await u.selectOptions(await screen.findByLabelText('Categoría'), '4')
    await u.click(screen.getByRole('button', { name: 'Registrar vehículo' }))
    expect(screen.getByText('Elige el tipo de máquina.')).toBeInTheDocument()
    expect(screen.getByText('Elige la marca.')).toBeInTheDocument()
  })
})

describe('Errores del servidor (CA-UI-09)', () => {
  it('400 con errores: cada mensaje bajo su campo con aria-describedby; los datos se conservan', async () => {
    abrirNuevo(['ASESOR'], {
      [VEHICULOS]: {
        status: 400,
        body: {
          error: 'Revisa los datos marcados.',
          errores: {
            lineaId: ['El modelo no corresponde a la marca elegida.'],
            kilometraje: ['Escribe el kilometraje (número entero mayor o igual a 0).'],
            otroCampo: ['Mensaje sin campo visible.'],
          },
        },
      },
    })
    const u = userEvent.setup()
    await u.selectOptions(await screen.findByLabelText('Categoría'), '2')
    await llenarCamion(u)
    await u.click(screen.getByRole('button', { name: 'Registrar vehículo' }))
    const error = await screen.findByText('El modelo no corresponde a la marca elegida.')
    const linea = screen.getByLabelText('Línea (modelo)')
    expect(linea.getAttribute('aria-describedby')).toContain(error.closest('ul')?.id)
    expect(linea).toHaveAttribute('aria-invalid', 'true')
    const km = screen.getByLabelText('Kilometraje (km)')
    expect(km).toHaveAttribute('aria-invalid', 'true')
    expect(km).toHaveValue('85000')
    expect(screen.getByText(/Mensaje sin campo visible\./)).toBeInTheDocument()
    expect(screen.getByLabelText('Precio de venta (COP)')).toHaveValue('120.000.000')
    await waitFor(() => expect(linea).toHaveFocus())
  })

  it('409 de placa repetida: el mensaje queda bajo la placa y el foco va a la placa', async () => {
    abrirNuevo(['ASESOR'], {
      [VEHICULOS]: {
        status: 409,
        body: { errores: { placa: ['Esta placa ya está registrada en otro vehículo.'] } },
      },
    })
    const u = userEvent.setup()
    await u.selectOptions(await screen.findByLabelText('Categoría'), '2')
    await llenarCamion(u)
    await u.click(screen.getByRole('button', { name: 'Registrar vehículo' }))
    const msg = await screen.findByText('Esta placa ya está registrada en otro vehículo.')
    const placa = screen.getByLabelText('Placa (opcional)')
    expect(placa.getAttribute('aria-describedby')).toContain(msg.closest('ul')?.id)
    expect(placa).toHaveValue('ABC-123')
    await waitFor(() => expect(placa).toHaveFocus())
  })

  it('500: aviso de servidor no disponible y el formulario sigue lleno', async () => {
    abrirNuevo(['ASESOR'], { [VEHICULOS]: { status: 500 } })
    const u = userEvent.setup()
    await u.selectOptions(await screen.findByLabelText('Categoría'), '2')
    await llenarCamion(u)
    await u.click(screen.getByRole('button', { name: 'Registrar vehículo' }))
    expect(await screen.findByText(/No se pudo conectar con el servidor/)).toBeInTheDocument()
    expect(screen.getByLabelText('Kilometraje (km)')).toHaveValue('85000')
  })
})

describe('Cuerpo enviado (CA-UI-06)', () => {
  it('Camión lleno y luego cambio a Carrocería: no viajan kilometraje, placa ni combustible', async () => {
    const api = abrirNuevo(['ASESOR'], { [VEHICULOS]: { status: 400, body: { error: 'Detenido.' } } })
    const u = userEvent.setup()
    await u.selectOptions(await screen.findByLabelText('Categoría'), '2')
    await llenarCamion(u)
    await u.selectOptions(screen.getByLabelText('Combustible (opcional)'), '8')
    await u.type(screen.getByLabelText('Cilindraje (cc) (opcional)'), '4500')
    await u.selectOptions(screen.getByLabelText('Categoría'), '3')
    await u.selectOptions(screen.getByLabelText('Tipo de carrocería'), '5')
    await u.type(screen.getByLabelText('Largo (m) (opcional)'), '6,5')
    await u.selectOptions(screen.getByLabelText('Material (opcional)'), '6')
    await u.click(screen.getByRole('button', { name: 'Registrar vehículo' }))
    await waitFor(() => expect(api.llamadasA(VEHICULOS)).toHaveLength(1))
    const cuerpo = cuerpoJson(api.llamadasA(VEHICULOS)[0].init) as Record<string, unknown>
    for (const campo of ['kilometraje', 'placa', 'combustibleId', 'cilindrajeCc', 'pesoBrutoKg', 'horasUso', 'tipoMaquinaId', 'asesorId']) {
      expect(campo in cuerpo, campo).toBe(false)
    }
    expect(cuerpo).toMatchObject({
      tipoId: 3,
      carroceriaId: 5,
      largoM: 6.5,
      materialId: 6,
      marcaId: 10,
      municipioId: 200,
      precioCop: 120000000,
    })
  })

  it('Carro: no envía capacidad de carga ni medidas', async () => {
    const api = abrirNuevo(['ASESOR'], { [VEHICULOS]: { status: 400, body: { error: 'Detenido.' } } })
    const u = userEvent.setup()
    await u.selectOptions(await screen.findByLabelText('Categoría'), '1')
    await llenarCamion(u)
    await u.click(screen.getByRole('button', { name: 'Registrar vehículo' }))
    await waitFor(() => expect(api.llamadasA(VEHICULOS)).toHaveLength(1))
    const cuerpo = cuerpoJson(api.llamadasA(VEHICULOS)[0].init) as Record<string, unknown>
    expect(Object.keys(cuerpo).sort()).toEqual(
      [
        'tipoId', 'municipioId', 'precioCop', 'marcaId', 'lineaId', 'modeloAnio', 'kilometraje', 'placa',
        'carroceriaId', 'combustibleId', 'cilindrajeCc', 'pesoBrutoKg', 'descripcionTecnica',
      ].sort(),
    )
    expect(cuerpo.placa).toBe('ABC123')
  })
})

describe('Propietario (RN-VE-007)', () => {
  it('administrador elige el asesor: obligatorio y viaja como asesorId', async () => {
    const api = abrirNuevo(['ADMINISTRADOR'], { [VEHICULOS]: { status: 400, body: { error: 'Detenido.' } } })
    const u = userEvent.setup()
    const asesor = await screen.findByLabelText('Asesor propietario')
    expect(within(asesor).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Elige el asesor',
      'Asesor Uno Ficticio',
      'Asesor Dos Ficticio',
    ])
    await u.selectOptions(screen.getByLabelText('Categoría'), '2')
    await llenarCamion(u)
    await u.click(screen.getByRole('button', { name: 'Registrar vehículo' }))
    expect(screen.getByText('Elige el asesor propietario del vehículo.')).toBeInTheDocument()
    expect(api.llamadasA(VEHICULOS)).toHaveLength(0)
    await u.selectOptions(asesor, '6')
    await u.click(screen.getByRole('button', { name: 'Registrar vehículo' }))
    await waitFor(() => expect(api.llamadasA(VEHICULOS)).toHaveLength(1))
    expect((cuerpoJson(api.llamadasA(VEHICULOS)[0].init) as { asesorId: number }).asesorId).toBe(6)
  })

  it('asesor no ve el selector y el cuerpo no lleva asesorId', async () => {
    const api = abrirNuevo(['ASESOR'], { [VEHICULOS]: { status: 400, body: { error: 'Detenido.' } } })
    const u = userEvent.setup()
    await u.selectOptions(await screen.findByLabelText('Categoría'), '2')
    expect(screen.queryByLabelText('Asesor propietario')).toBeNull()
    await llenarCamion(u)
    await u.click(screen.getByRole('button', { name: 'Registrar vehículo' }))
    await waitFor(() => expect(api.llamadasA(VEHICULOS)).toHaveLength(1))
    expect('asesorId' in (cuerpoJson(api.llamadasA(VEHICULOS)[0].init) as object)).toBe(false)
  })
})

describe('Tras crear (HU-013)', () => {
  it('Camión creado: navega a #/inventario/<id> con el aviso y la sección de fotos', async () => {
    const creado = detalleCamion({ id: 31, precioAnteriorCop: null, precioCop: 120000000 })
    abrirNuevo(['ASESOR'], {
      [VEHICULOS]: { status: 201, body: creado },
      '/api/inventario/vehiculos/31': { status: 200, body: creado },
    })
    const u = userEvent.setup()
    await u.selectOptions(await screen.findByLabelText('Categoría'), '2')
    await llenarCamion(u)
    await u.click(screen.getByRole('button', { name: 'Registrar vehículo' }))
    await waitFor(() => expect(window.location.hash).toBe('#/inventario/31'))
    expect(await screen.findByText('Vehículo guardado. Siguiente paso: agrega sus fotos abajo (hasta 10). Cuando termines, pulsa «Listo, volver a mi inventario».')).toBeInTheDocument()
    expect(await screen.findByText('Este vehículo aún no tiene fotos.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Editar vehículo' })).toBeInTheDocument()
    // Mejora de la prueba manual: al final, debajo de las fotos, un botón claro para terminar.
    const listo = screen.getByRole('link', { name: 'Listo, volver a mi inventario' })
    expect(listo).toHaveAttribute('href', '#/inventario')
    expect(screen.getByText('Los datos y las fotos ya están guardados.')).toBeInTheDocument()
  })
})

describe('Edición (CA-UI-10, HU-019, RN-VE-003)', () => {
  it('muestra "Precio anterior", la categoría no es editable y el PUT no lleva asesorId', async () => {
    const actualizado = detalleCamion({ kilometraje: 90000 })
    const api = simularFetch({
      [ME]: me(['ADMINISTRADOR']),
      [CATALOGOS]: { status: 200, body: CAT_ADMIN },
      [DETALLE_9]: [
        { status: 200, body: detalleCamion({ asesor: { id: 5, nombre: 'Asesor Uno Ficticio' } }) },
        { status: 200, body: actualizado },
      ],
    })
    const u = userEvent.setup()
    abrirConSesion('#/inventario/9')
    expect(await screen.findByText(/Precio anterior: \$ 120\.000\.000/)).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Editar vehículo' })).toBeInTheDocument()
    expect(screen.getByText('(no se puede cambiar)')).toBeInTheDocument()
    expect(screen.queryByLabelText('Categoría')).toBeNull()
    expect(screen.queryByLabelText('Asesor propietario')).toBeNull()
    expect(screen.getByText('Asesor Uno Ficticio')).toBeInTheDocument()
    expect(screen.getByLabelText('Precio de venta (COP)')).toHaveValue('115.000.000')
    expect(screen.getByLabelText('Línea (modelo)')).toHaveValue('100')
    expect(screen.getByLabelText('Departamento')).toHaveValue('20')
    expect(screen.getByLabelText('Placa (opcional)')).toHaveValue('ABC123')
    const km = screen.getByLabelText('Kilometraje (km)')
    await u.clear(km)
    await u.type(km, '90000')
    await u.click(screen.getByRole('button', { name: 'Guardar cambios' }))
    expect(await screen.findByText(/^Cambios guardados\. Puedes agregar o reordenar fotos abajo; cuando termines, pulsa «Listo, volver al inventario»\./)).toBeInTheDocument()
    // Mejora de la prueba manual: botón final visible para el administrador.
    expect(screen.getByRole('link', { name: 'Listo, volver al inventario' })).toHaveAttribute('href', '#/inventario')
    const put = api.llamadasA(DETALLE_9)[1]
    expect(put.init?.method).toBe('PUT')
    const cuerpo = cuerpoJson(put.init) as Record<string, unknown>
    expect(cuerpo).toMatchObject({ tipoId: 2, kilometraje: 90000, precioCop: 115000000, placa: 'ABC123' })
    expect('asesorId' in cuerpo).toBe(false)
  })

  it('409 de placa al editar: el mensaje queda bajo la placa', async () => {
    simularFetch({
      [ME]: me(['ASESOR']),
      [CATALOGOS]: { status: 200, body: CAT },
      [DETALLE_9]: [
        { status: 200, body: detalleCamion() },
        { status: 409, body: { errores: { placa: ['Esta placa ya está registrada en otro vehículo.'] } } },
      ],
    })
    const u = userEvent.setup()
    abrirConSesion('#/inventario/9')
    await u.click(await screen.findByRole('button', { name: 'Guardar cambios' }))
    const msg = await screen.findByText('Esta placa ya está registrada en otro vehículo.')
    expect(screen.getByLabelText('Placa (opcional)').getAttribute('aria-describedby')).toContain(msg.closest('ul')?.id)
  })

  it('VENDIDO: aviso, campos deshabilitados, sin "Guardar cambios" ni acciones de fotos', async () => {
    simularFetch({
      [ME]: me(['ASESOR']),
      [CATALOGOS]: { status: 200, body: CAT },
      [DETALLE_9]: {
        status: 200,
        body: detalleCamion({ estado: 'VENDIDO', fotos: [{ id: 1, url: '/media/a.jpg', orden: 1, esPortada: true }] }),
      },
    })
    abrirConSesion('#/inventario/9')
    expect(await screen.findByText('Este vehículo está vendido y no se puede modificar.')).toBeInTheDocument()
    expect(screen.getByLabelText('Kilometraje (km)')).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Guardar cambios' })).toBeNull()
    expect(screen.queryByLabelText('Agregar fotos')).toBeNull()
    expect(screen.queryByRole('button', { name: /Eliminar/ })).toBeNull()
  })

  it('INACTIVO: muestra el motivo de inactivación', async () => {
    simularFetch({
      [ME]: me(['ASESOR']),
      [CATALOGOS]: { status: 200, body: CAT },
      [DETALLE_9]: { status: 200, body: detalleCamion({ estado: 'INACTIVO', motivoInactivacion: 'En reparación' }) },
    })
    abrirConSesion('#/inventario/9')
    expect(await screen.findByText('En reparación')).toBeInTheDocument()
    expect(screen.getByText('Inactivo')).toBeInTheDocument()
  })

  it('descripción con <script> se muestra como texto, sin ejecutarse (CA-018-05)', async () => {
    const xss = '<script>window.__xss = 1</script><img src=x onerror="window.__xss=2">'
    simularFetch({
      [ME]: me(['ASESOR']),
      [CATALOGOS]: { status: 200, body: CAT },
      [DETALLE_9]: { status: 200, body: detalleCamion({ descripcionTecnica: xss, motivoInactivacion: xss, estado: 'INACTIVO' }) },
    })
    abrirConSesion('#/inventario/9')
    expect(await screen.findByLabelText('Descripción técnica (opcional)')).toHaveValue(xss)
    expect(document.querySelector('script')).toBeNull()
    expect(document.querySelector('img[src="x"]')).toBeNull()
    expect((window as unknown as { __xss?: number }).__xss).toBeUndefined()
  })
})
