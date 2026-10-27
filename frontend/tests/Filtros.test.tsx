// Resultados y filtros del catálogo (P3: HU-004 a HU-012, CA-FIL-08, CA-UI-07).
import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import {
  abrirApp,
  pagina,
  respuestaFiltros,
  RUTA_FILTROS,
  rutaPublicaciones,
  V1,
  V2,
  V3,
  V4,
} from './catalogoDatos'
import { simularFetch, type RespuestaSimulada } from './helpers'

const VACIA = pagina([], 0)

function campo(nombre: string) {
  return screen.getByLabelText(nombre) as HTMLInputElement | HTMLSelectElement
}

function contador() {
  return screen.getByText(/vehículos? encontrados?|Buscando vehículos/)
}

function errorDe(nombre: string): string {
  const el = campo(nombre)
  const id = el.getAttribute('aria-describedby')
  return id ? (document.getElementById(id)?.textContent ?? '') : ''
}

function botonLimpiarDelFormulario() {
  const form = screen.getByRole('form', { name: 'Filtros' })
  return within(form).getByRole('button', { name: 'Limpiar filtros' })
}

function textoOpciones(select: HTMLElement): string[] {
  return Array.from((select as HTMLSelectElement).options).map((o) => o.textContent ?? '')
}

function base(extra: Record<string, RespuestaSimulada | RespuestaSimulada[]> = {}) {
  return simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [rutaPublicaciones()]: pagina([V1, V2, V3]), ...extra })
}

describe('Resultados: consulta y contador', () => {
  it('CA-004-18 contador con role="status": 25, 1 y 0', async () => {
    base({
      [rutaPublicaciones('q=a')]: pagina([V1], 25),
      [rutaPublicaciones('q=b')]: pagina([V1], 1),
      [rutaPublicaciones('q=c')]: VACIA,
    })
    abrirApp('#/buscar?q=a')
    expect(await screen.findByText('25 vehículos encontrados')).toHaveAttribute('role', 'status')
    act(() => {
      window.location.hash = '#/buscar?q=b'
    })
    expect(await screen.findByText('1 vehículo encontrado')).toHaveAttribute('role', 'status')
    act(() => {
      window.location.hash = '#/buscar?q=c'
    })
    expect(await screen.findByText('0 vehículos encontrados')).toHaveAttribute('role', 'status')
  })

  it('CA-004-17 abrir la URL con filtros rellena los campos y pide los mismos parámetros', async () => {
    const consulta = 'marcaId=10&precioMax=100000000&orden=precio_asc'
    const api = base({ [rutaPublicaciones(consulta)]: pagina([V1]) })
    abrirApp(`#/buscar?${consulta}`)
    expect(await screen.findByText('1 vehículo encontrado')).toBeInTheDocument()
    expect(api.llamadasA(rutaPublicaciones(consulta))).toHaveLength(1)
    await waitFor(() => expect(campo('Marca')).toHaveValue('10'))
    expect(campo('Máximo')).toHaveValue('100.000.000')
    expect(campo('Ordenar por')).toHaveValue('precio_asc')
    expect(campo('Modelo')).toBeEnabled()
  })

  it('CA-004-17 / CA-012-05 Atrás y Adelante restauran filtros y resultados', async () => {
    base({
      [rutaPublicaciones('marcaId=10')]: pagina([V1, V4]),
      [rutaPublicaciones('marcaId=11')]: pagina([V2]),
    })
    abrirApp('#/buscar?marcaId=10')
    expect(await screen.findByText('2 vehículos encontrados')).toBeInTheDocument()
    const user = userEvent.setup()
    await waitFor(() => expect(campo('Marca')).toHaveValue('10'))
    await user.selectOptions(campo('Marca'), '11')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => expect(window.location.hash).toBe('#/buscar?marcaId=11'))
    expect(await screen.findByRole('link', { name: V2.titulo })).toBeInTheDocument()

    act(() => window.history.back())
    await waitFor(() => expect(window.location.hash).toBe('#/buscar?marcaId=10'))
    expect(await screen.findByRole('link', { name: V4.titulo })).toBeInTheDocument()
    expect(campo('Marca')).toHaveValue('10')

    act(() => window.history.forward())
    await waitFor(() => expect(window.location.hash).toBe('#/buscar?marcaId=11'))
    expect(await screen.findByRole('link', { name: V2.titulo })).toBeInTheDocument()
    expect(campo('Marca')).toHaveValue('11')
  })

  it('CA-004-23 / CA-UI-10 solo se muestra la respuesta del último cambio; estado de carga mientras tanto', async () => {
    type Pendiente = { url: string; resolver: (r: Response) => void }
    const pendientes: Pendiente[] = []
    const json = (body: unknown) =>
      new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
    vi.stubGlobal(
      'fetch',
      vi.fn((entrada: RequestInfo | URL) => {
        const url = String(entrada)
        if (url === RUTA_FILTROS) return Promise.resolve(json((respuestaFiltros() as { body: unknown }).body))
        // Respuesta lenta: el servidor contesta aunque el cliente haya cancelado.
        return new Promise<Response>((resolver) => pendientes.push({ url, resolver }))
      }),
    )
    abrirApp('#/buscar?q=primero')
    await waitFor(() => expect(pendientes).toHaveLength(1))
    act(() => {
      window.location.hash = '#/buscar?q=segundo'
    })
    await waitFor(() => expect(pendientes).toHaveLength(2))
    expect(contador()).toHaveTextContent('Buscando vehículos...')
    expect(contador()).toHaveAttribute('aria-busy', 'true')

    const [primero, segundo] = pendientes
    expect(primero.url).toBe(rutaPublicaciones('q=primero'))
    expect(segundo.url).toBe(rutaPublicaciones('q=segundo'))
    await act(async () => {
      segundo.resolver(json({ items: [V2], total: 1, pagina: 1, tamano: 12 }))
    })
    expect(await screen.findByRole('link', { name: V2.titulo })).toBeInTheDocument()
    await act(async () => {
      primero.resolver(json({ items: [V1, V3], total: 2, pagina: 1, tamano: 12 }))
    })
    expect(screen.queryByRole('link', { name: V1.titulo })).not.toBeInTheDocument()
    expect(screen.getByText('1 vehículo encontrado')).toBeInTheDocument()
  })

  it('CA-004-23 / CA-UI-10 un fallo de red muestra "No se pudo conectar con el servidor." y Reintentar', async () => {
    base({ [rutaPublicaciones()]: [{ red: true }, pagina([V1])] })
    abrirApp('#/buscar')
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo conectar con el servidor.')
    expect(screen.queryByRole('article')).not.toBeInTheDocument()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByRole('link', { name: V1.titulo })).toBeInTheDocument()
  })
})

describe('Filtros: URL y consulta', () => {
  it('CA-005-03 / CA-007-03 / CA-008-06 / CA-009-06 / CA-010-04 / CA-011-03 / CA-004-22 todos los filtros generan la URL y la consulta correctas', async () => {
    const consulta =
      'q=npr&tipoId=2&marcaId=10&lineaId=101&precioMin=50000000&precioMax=120000000&anioMin=2015&anioMax=2022&kmMax=85000&orden=precio_desc'
    const api = base({ [rutaPublicaciones(consulta)]: pagina([V1]) })
    abrirApp('#/buscar')
    await screen.findByText('3 vehículos encontrados')
    const user = userEvent.setup()
    await user.type(campo('Buscar'), 'npr')
    await waitFor(() => expect(textoOpciones(campo('Categoría'))).toContain('Camión (5)'))
    await user.selectOptions(campo('Categoría'), '2')
    await user.selectOptions(campo('Marca'), '10')
    await user.selectOptions(campo('Modelo'), '101')
    await user.click(campo('Mínimo'))
    await user.paste('50000000')
    await user.click(campo('Máximo'))
    await user.paste('120000000')
    expect(campo('Máximo')).toHaveValue('120.000.000')
    await user.type(campo('Desde'), '2015')
    await user.type(campo('Hasta'), '2022')
    await user.click(campo('Kilometraje máximo'))
    await user.paste('85000')
    expect(campo('Kilometraje máximo')).toHaveValue('85.000')
    await user.selectOptions(campo('Ordenar por'), 'precio_desc')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))

    await waitFor(() => expect(window.location.hash).toBe(`#/buscar?${consulta}`))
    expect(await screen.findByText('1 vehículo encontrado')).toBeInTheDocument()
    expect(api.llamadasA(rutaPublicaciones(consulta))).toHaveLength(1)
    // CA-009-06: el campo del presupuesto está en el grupo que indica COP.
    const grupo = screen.getByRole('group', { name: 'Presupuesto (COP)' })
    expect(within(grupo).getByLabelText('Máximo')).toHaveValue('120.000.000')
  })

  it('CA-007-03 marcas en orden con opción "Todas"; CA-008-06 modelo deshabilitado sin marca y solo con sus líneas', async () => {
    base()
    abrirApp('#/buscar')
    await screen.findByText('3 vehículos encontrados')
    await waitFor(() => expect(textoOpciones(campo('Marca'))).toHaveLength(4))
    expect(textoOpciones(campo('Marca'))).toEqual(['Todas', 'Chevrolet (4)', 'Kenworth (2)', 'Renault (1)'])
    expect(campo('Modelo')).toBeDisabled()
    expect(textoOpciones(campo('Modelo'))).toEqual(['Elige primero una marca'])

    const user = userEvent.setup()
    await user.selectOptions(campo('Marca'), '10')
    expect(campo('Modelo')).toBeEnabled()
    expect(textoOpciones(campo('Modelo'))).toEqual(['Todos', 'NHR (1)', 'NPR (3)'])
    await user.selectOptions(campo('Modelo'), '101')

    await user.selectOptions(campo('Marca'), '11')
    expect(campo('Modelo')).toHaveValue('')
    expect(textoOpciones(campo('Modelo'))).toEqual(['Todos', 'T800 (2)'])

    await user.selectOptions(campo('Marca'), '')
    expect(campo('Modelo')).toBeDisabled()
  })

  it('CA-008-06 al cambiar de marca, lineaId desaparece de la URL', async () => {
    base({
      [rutaPublicaciones('marcaId=10&lineaId=101')]: pagina([V1]),
      [rutaPublicaciones('marcaId=11')]: pagina([V2]),
    })
    abrirApp('#/buscar?marcaId=10&lineaId=101')
    await screen.findByText('1 vehículo encontrado')
    await waitFor(() => expect(campo('Modelo')).toHaveValue('101'))
    const user = userEvent.setup()
    await user.selectOptions(campo('Marca'), '11')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => expect(window.location.hash).toBe('#/buscar?marcaId=11'))
  })

  it('CA-004-22 cambiar el orden actualiza orden en la URL y vuelve a la página 1', async () => {
    const api = base({
      [rutaPublicaciones('pagina=2')]: pagina([V1], 30, 2),
      [rutaPublicaciones('orden=km_asc')]: pagina([V3], 30, 1),
    })
    abrirApp('#/buscar?pagina=2')
    await screen.findByText('30 vehículos encontrados')
    const user = userEvent.setup()
    await user.selectOptions(campo('Ordenar por'), 'km_asc')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => expect(window.location.hash).toBe('#/buscar?orden=km_asc'))
    expect(await screen.findByRole('link', { name: V3.titulo })).toBeInTheDocument()
    expect(api.llamadasA(rutaPublicaciones('orden=km_asc'))).toHaveLength(1)
  })

  it('el orden "recientes" (por defecto) no se escribe en la URL', async () => {
    base({ [rutaPublicaciones('orden=precio_asc')]: pagina([V1]) })
    abrirApp('#/buscar?orden=precio_asc')
    await screen.findByText('1 vehículo encontrado')
    const user = userEvent.setup()
    await user.selectOptions(campo('Ordenar por'), 'recientes')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => expect(window.location.hash).toBe('#/buscar'))
  })
})

describe('Filtros: validación', () => {
  it('validación en cliente con los mensajes del contrato; no se pide nada y el foco va al primer error', async () => {
    const api = base()
    abrirApp('#/buscar')
    await screen.findByText('3 vehículos encontrados')
    const user = userEvent.setup()
    await user.click(campo('Mínimo'))
    await user.paste('200')
    await user.click(campo('Máximo'))
    await user.paste('100')
    await user.type(campo('Desde'), '1800')
    await user.type(campo('Hasta'), '2201')
    const llamadasAntes = api.llamadas.length
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))

    expect(errorDe('Máximo')).toBe('El precio máximo debe ser mayor o igual al mínimo.')
    expect(campo('Máximo')).toHaveAttribute('aria-invalid', 'true')
    expect(errorDe('Desde')).toBe('Escribe un año entre 1900 y 2200.')
    expect(errorDe('Hasta')).toBe('Escribe un año entre 1900 y 2200.')
    expect(campo('Mínimo')).toHaveAttribute('aria-invalid', 'false')
    expect(window.location.hash).toBe('#/buscar')
    expect(api.llamadas.length).toBe(llamadasAntes)
    await waitFor(() => expect(campo('Máximo')).toHaveFocus())
  })

  it('año final menor que el inicial', async () => {
    base()
    abrirApp('#/buscar')
    await screen.findByText('3 vehículos encontrados')
    const user = userEvent.setup()
    await user.type(campo('Desde'), '2020')
    await user.type(campo('Hasta'), '2019')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(errorDe('Hasta')).toBe('El año final debe ser mayor o igual al inicial.')
    expect(window.location.hash).toBe('#/buscar')
  })

  it('valores inválidos que llegan por la URL se marcan al aplicar (precio, km y búsqueda larga)', async () => {
    const larga = 'x'.repeat(101)
    const consulta = `q=${larga}&precioMin=abc&kmMax=-1`
    base({ [rutaPublicaciones(consulta)]: VACIA })
    abrirApp(`#/buscar?${consulta}`)
    await screen.findByText('0 vehículos encontrados')
    await userEvent.setup().click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(errorDe('Buscar')).toBe('La búsqueda no puede tener más de 100 caracteres.')
    expect(errorDe('Mínimo')).toBe('Escribe un precio válido en pesos (número entero mayor o igual a 0).')
    expect(errorDe('Kilometraje máximo')).toBe('Escribe un kilometraje válido (número entero mayor o igual a 0).')
  })

  it('CA-004-19 / CA-010-04 un 400 del servidor pone cada mensaje bajo su filtro, conserva valores y no muestra resultados', async () => {
    const consulta = 'marcaId=x&precioMax=-5&anioMin=1800'
    base({
      [rutaPublicaciones(consulta)]: {
        status: 400,
        body: {
          error: 'Revisa los filtros.',
          errores: {
            precioMax: ['Escribe un precio válido en pesos (número entero mayor o igual a 0).'],
            anioMin: ['Escribe un año entre 1900 y 2200.'],
            marcaId: ['Marca no válida.'],
          },
        },
      },
    })
    abrirApp(`#/buscar?${consulta}`)
    expect(await screen.findByRole('alert')).toHaveTextContent('Revisa los filtros.')
    expect(errorDe('Máximo')).toBe('Escribe un precio válido en pesos (número entero mayor o igual a 0).')
    expect(errorDe('Desde')).toBe('Escribe un año entre 1900 y 2200.')
    expect(errorDe('Marca')).toBe('Marca no válida.')
    expect(campo('Máximo')).toHaveValue('-5')
    expect(campo('Desde')).toHaveValue('1800')
    expect(screen.queryByRole('article')).not.toBeInTheDocument()
    expect(screen.queryByText(/vehículos? encontrados?/)).not.toBeInTheDocument()
    // En móvil los filtros se abren solos para ver los errores.
    expect(screen.getByRole('button', { name: 'Ocultar filtros' })).toHaveAttribute('aria-expanded', 'true')
  })

  it('un 400 con "tamano" se muestra como error general de la página', async () => {
    base({
      [rutaPublicaciones('pagina=0')]: {
        status: 400,
        body: { error: 'Revisa los filtros.', errores: { pagina: ['Página o tamaño no válidos.'] } },
      },
    })
    abrirApp('#/buscar?pagina=0')
    await screen.findByText('Página o tamaño no válidos.')
  })
})

describe('Estado vacío, Limpiar filtros y paginación', () => {
  const COMPLETA =
    'q=npr&tipoId=2&marcaId=10&lineaId=101&precioMin=1&precioMax=120000000&anioMin=2015&anioMax=2022&kmMax=85000&orden=precio_desc&pagina=2'

  function camposVacios() {
    expect(campo('Buscar')).toHaveValue('')
    expect(campo('Categoría')).toHaveValue('')
    expect(campo('Marca')).toHaveValue('')
    expect(campo('Modelo')).toHaveValue('')
    expect(campo('Modelo')).toBeDisabled()
    expect(campo('Mínimo')).toHaveValue('')
    expect(campo('Máximo')).toHaveValue('')
    expect(campo('Desde')).toHaveValue('')
    expect(campo('Hasta')).toHaveValue('')
    expect(campo('Kilometraje máximo')).toHaveValue('')
    expect(campo('Ordenar por')).toHaveValue('recientes')
  }

  it('CA-004-20 / CA-012-03 sin resultados: mensaje y "Limpiar filtros" vuelve a #/buscar sin parámetros', async () => {
    const api = base({ [rutaPublicaciones('marcaId=12')]: VACIA })
    abrirApp('#/buscar?marcaId=12')
    expect(await screen.findByText('No encontramos vehículos con esos filtros.')).toBeInTheDocument()
    const vacio = screen.getByText('No encontramos vehículos con esos filtros.').parentElement as HTMLElement
    await userEvent.setup().click(within(vacio).getByRole('button', { name: 'Limpiar filtros' }))
    await waitFor(() => expect(window.location.hash).toBe('#/buscar'))
    expect(await screen.findByText('3 vehículos encontrados')).toBeInTheDocument()
    expect(api.llamadasA(rutaPublicaciones())).toHaveLength(1)
    camposVacios()
  })

  it('CA-012-02 Limpiar filtros vacía todos los campos, deja la URL sin parámetros y pide /publicaciones sin filtros', async () => {
    const api = base({ [rutaPublicaciones(COMPLETA)]: pagina([V1], 30, 2) })
    abrirApp(`#/buscar?${COMPLETA}`)
    await screen.findByText('30 vehículos encontrados')
    await waitFor(() => expect(campo('Modelo')).toHaveValue('101'))
    expect(screen.getByRole('searchbox', { name: 'Buscar vehículos' })).toHaveValue('npr')
    await userEvent.setup().click(botonLimpiarDelFormulario())
    await waitFor(() => expect(window.location.hash).toBe('#/buscar'))
    expect(await screen.findByText('3 vehículos encontrados')).toBeInTheDocument()
    camposVacios()
    expect(screen.getByRole('searchbox', { name: 'Buscar vehículos' })).toHaveValue('')
    expect(api.llamadasA(rutaPublicaciones())).toHaveLength(1)
    expect(screen.queryByRole('navigation', { name: 'Paginación' })).not.toBeInTheDocument()
  })

  it('CA-012-04 Limpiar filtros quita los mensajes de error del 400', async () => {
    base({
      [rutaPublicaciones('anioMin=1800')]: {
        status: 400,
        body: { error: 'Revisa los filtros.', errores: { anioMin: ['Escribe un año entre 1900 y 2200.'] } },
      },
    })
    abrirApp('#/buscar?anioMin=1800')
    await screen.findByRole('alert')
    expect(errorDe('Desde')).toBe('Escribe un año entre 1900 y 2200.')
    await userEvent.setup().click(botonLimpiarDelFormulario())
    expect(await screen.findByText('3 vehículos encontrados')).toBeInTheDocument()
    expect(screen.queryByText('Escribe un año entre 1900 y 2200.')).not.toBeInTheDocument()
    expect(campo('Desde')).toHaveAttribute('aria-invalid', 'false')
  })

  it('CA-012-05 tras limpiar, Atrás devuelve los filtros anteriores', async () => {
    base({ [rutaPublicaciones('marcaId=10')]: pagina([V1, V4]) })
    abrirApp('#/buscar?marcaId=10')
    await screen.findByText('2 vehículos encontrados')
    await userEvent.setup().click(botonLimpiarDelFormulario())
    await screen.findByText('3 vehículos encontrados')
    act(() => window.history.back())
    expect(await screen.findByText('2 vehículos encontrados')).toBeInTheDocument()
    await waitFor(() => expect(campo('Marca')).toHaveValue('10'))
  })

  it('CA-004-21 paginación: total 30 y tamaño 12 dan 3 páginas; Anterior inactivo en la 1 y Siguiente en la 3', async () => {
    const api = base({
      [rutaPublicaciones('marcaId=10')]: pagina([V1], 30, 1),
      [rutaPublicaciones('marcaId=10&pagina=2')]: pagina([V2], 30, 2),
      [rutaPublicaciones('marcaId=10&pagina=3')]: pagina([V3], 30, 3),
    })
    abrirApp('#/buscar?marcaId=10')
    await screen.findByText('30 vehículos encontrados')
    let nav = screen.getByRole('navigation', { name: 'Paginación' })
    expect(nav).toHaveTextContent('Página 1 de 3')
    expect(within(nav).queryByRole('link', { name: 'Anterior' })).not.toBeInTheDocument()
    const user = userEvent.setup()
    await user.click(within(nav).getByRole('link', { name: 'Siguiente' }))
    await waitFor(() => expect(window.location.hash).toBe('#/buscar?marcaId=10&pagina=2'))
    expect(await screen.findByRole('link', { name: V2.titulo })).toBeInTheDocument()
    expect(api.llamadasA(rutaPublicaciones('marcaId=10&pagina=2'))).toHaveLength(1)

    nav = screen.getByRole('navigation', { name: 'Paginación' })
    expect(within(nav).getByRole('link', { name: 'Anterior' })).toHaveAttribute('href', '#/buscar?marcaId=10')
    await user.click(within(nav).getByRole('link', { name: 'Siguiente' }))
    expect(await screen.findByRole('link', { name: V3.titulo })).toBeInTheDocument()
    nav = screen.getByRole('navigation', { name: 'Paginación' })
    expect(nav).toHaveTextContent('Página 3 de 3')
    expect(within(nav).queryByRole('link', { name: 'Siguiente' })).not.toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: 'Anterior' })).toHaveAttribute('href', '#/buscar?marcaId=10&pagina=2')
  })

  it('una sola página no muestra paginación; una página sin resultados ofrece ir a la primera', async () => {
    base({ [rutaPublicaciones('pagina=9')]: pagina([], 30, 9) })
    abrirApp('#/buscar')
    await screen.findByText('3 vehículos encontrados')
    expect(screen.queryByRole('navigation', { name: 'Paginación' })).not.toBeInTheDocument()
    act(() => {
      window.location.hash = '#/buscar?pagina=9'
    })
    expect(await screen.findByText('Esta página no tiene resultados.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ir a la primera página' })).toHaveAttribute('href', '#/buscar')
  })
})

describe('Opciones de filtros y móvil', () => {
  it('CA-FIL-08 con /filtros 200 los selectores de categoría, marca y modelo se llenan', async () => {
    base()
    abrirApp('#/buscar')
    await waitFor(() =>
      expect(textoOpciones(campo('Categoría'))).toEqual(['Todas', 'Carro (3)', 'Camión (5)', 'Carrocería (2)', 'Maquinaria (2)']),
    )
    expect(textoOpciones(campo('Marca'))).toHaveLength(4)
  })

  it('CA-FIL-08 si /filtros falla por red, la búsqueda por texto sigue funcionando', async () => {
    const api = simularFetch({
      [RUTA_FILTROS]: { red: true },
      [rutaPublicaciones()]: pagina([V1, V2, V3]),
      [rutaPublicaciones('q=npr')]: pagina([V1]),
    })
    abrirApp('#/buscar')
    expect(await screen.findByText('3 vehículos encontrados')).toBeInTheDocument()
    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(textoOpciones(campo('Marca'))).toEqual(['Todas'])
    const user = userEvent.setup()
    await user.type(campo('Buscar'), 'npr')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => expect(window.location.hash).toBe('#/buscar?q=npr'))
    expect(await screen.findByText('1 vehículo encontrado')).toBeInTheDocument()
    expect(api.llamadasA(rutaPublicaciones('q=npr'))).toHaveLength(1)
  })

  it('CA-FIL-08 si /filtros falla por red, se avisa que no se pudieron cargar las opciones y se ofrece Reintentar', async () => {
    simularFetch({ [RUTA_FILTROS]: { red: true }, [rutaPublicaciones()]: pagina([V1]) })
    abrirApp('#/buscar')
    await screen.findByText('1 vehículo encontrado')
    const aviso = await screen.findByText('No se pudieron cargar las opciones de los filtros.')
    expect(aviso).toHaveAttribute('role', 'alert')
    expect(screen.getAllByRole('button', { name: 'Reintentar' }).length).toBeGreaterThan(0)
  })

  it('CA-UI-07 el botón de filtros despliega y pliega el panel con aria-expanded', async () => {
    base()
    abrirApp('#/buscar')
    await screen.findByText('3 vehículos encontrados')
    const boton = screen.getByRole('button', { name: 'Mostrar filtros' })
    expect(boton).toHaveAttribute('aria-expanded', 'false')
    expect(boton).toHaveAttribute('aria-controls', 'panel-filtros')
    const user = userEvent.setup()
    await user.click(boton)
    expect(screen.getByRole('button', { name: 'Ocultar filtros' })).toHaveAttribute('aria-expanded', 'true')
    expect(document.getElementById('panel-filtros')).toHaveClass('abierto')
    await user.click(screen.getByRole('button', { name: 'Ocultar filtros' }))
    expect(screen.getByRole('button', { name: 'Mostrar filtros' })).toHaveAttribute('aria-expanded', 'false')
    expect(document.getElementById('panel-filtros')).not.toHaveClass('abierto')
  })
})
