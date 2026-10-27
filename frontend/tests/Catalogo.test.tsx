// Portal público (P3): encabezado, inicio, tarjetas, navegación por hash,
// acceso sin cuenta y comprador en solo lectura (D-3).
import { act, cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import {
  abrirApp,
  C1,
  K0,
  M1,
  M2,
  pagina,
  respuestaFiltros,
  RUTA_FILTROS,
  RUTA_INICIO,
  rutaDetalle,
  rutaPublicaciones,
  TIPOS,
  V1,
  V2,
  V3,
  V4,
  detalle,
} from './catalogoDatos'
import {
  cabecera,
  guardarSesionFicticia,
  PERFIL_FICTICIO,
  simularFetch,
  type RespuestaSimulada,
} from './helpers'

const ME = '/api/auth/me'
const DOS_HORAS = 2 * 60 * 60 * 1000
const INICIO_OK: RespuestaSimulada = {
  status: 200,
  body: { destacados: [V1], recientes: [V1, V2, V3, V4] },
}
const INICIO_VACIO: RespuestaSimulada = { status: 200, body: { destacados: [], recientes: [] } }

/** Texto prohibido en el catálogo público (D-3, CA-DET-15). */
const GESTION = /crear|editar|eliminar|borrar|ocultar|vendido|destacar|contactar|publicar/i

function encabezado() {
  return screen.getByRole('banner')
}

function navPrincipal() {
  return screen.getByRole('navigation', { name: 'Principal' })
}

function sinBotonesDeGestion() {
  for (const b of screen.queryAllByRole('button')) expect(b.textContent ?? '').not.toMatch(GESTION)
  for (const a of screen.queryAllByRole('link')) expect(a.textContent ?? '').not.toMatch(GESTION)
}

function titulosTarjetas(): string[] {
  return screen.getAllByRole('article').map((a) => within(a).getByRole('heading', { level: 3 }).textContent ?? '')
}

describe('Encabezado', () => {
  it('CA-D17-05 / CA-UI-03 con los cuatro tipos: logo, búsqueda y los 4 enlaces de categoría', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: INICIO_VACIO })
    abrirApp('#/')
    const nav = navPrincipal()
    expect(await within(nav).findByRole('link', { name: 'Carros' })).toHaveAttribute('href', '#/buscar?tipoId=1')
    expect(within(nav).getByRole('link', { name: 'Camiones' })).toHaveAttribute('href', '#/buscar?tipoId=2')
    expect(within(nav).getByRole('link', { name: 'Carrocerías' })).toHaveAttribute('href', '#/buscar?tipoId=3')
    expect(within(nav).getByRole('link', { name: 'Maquinaria' })).toHaveAttribute('href', '#/buscar?tipoId=4')
    expect(within(encabezado()).getByRole('link', { name: /Financiamos Tu Camión/ })).toHaveAttribute('href', '#/')
    expect(within(encabezado()).getByRole('searchbox', { name: 'Buscar vehículos' })).toBeInTheDocument()
  })

  it('CA-D17-05 sin Maquinaria en tipos su enlace no se muestra y los otros sí', async () => {
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(TIPOS.filter((t) => t.nombre !== 'Maquinaria')),
      [RUTA_INICIO]: INICIO_VACIO,
    })
    abrirApp('#/')
    const nav = navPrincipal()
    expect(await within(nav).findByRole('link', { name: 'Carros' })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: 'Camiones' })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: 'Carrocerías' })).toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Maquinaria' })).not.toBeInTheDocument()
    // En el inicio tampoco.
    const accesos = screen.getByRole('list', { name: 'Categorías' })
    expect(within(accesos).queryByRole('link', { name: 'Maquinaria' })).not.toBeInTheDocument()
    expect(within(accesos).getByRole('link', { name: 'Carros' })).toBeInTheDocument()
  })

  it('CA-D03-03 sin sesión: "Iniciar sesión" y "Registrarme" (a la cuenta), sin "Mi cuenta"', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: INICIO_VACIO })
    abrirApp('#/')
    const nav = navPrincipal()
    expect(within(nav).getByRole('link', { name: 'Iniciar sesión' })).toHaveAttribute('href', '#/cuenta')
    expect(within(nav).getByRole('link', { name: 'Registrarme' })).toHaveAttribute('href', '#/cuenta/registro')
    expect(within(nav).queryByRole('link', { name: 'Mi cuenta' })).not.toBeInTheDocument()
    await screen.findByText('Pronto tendremos vehículos publicados.')
  })

  it.each(['COMPRADOR', 'ASESOR', 'ADMINISTRADOR'])(
    'CA-D03-03 con sesión de %s: "Mi cuenta" lleva a #/cuenta',
    async (rol) => {
      guardarSesionFicticia(Date.now() + DOS_HORAS)
      simularFetch({
        [RUTA_FILTROS]: respuestaFiltros(),
        [RUTA_INICIO]: INICIO_OK,
        [ME]: { status: 200, body: { ...PERFIL_FICTICIO, roles: [rol] } },
      })
      abrirApp('#/')
      const nav = navPrincipal()
      expect(within(nav).getByRole('link', { name: 'Mi cuenta' })).toHaveAttribute('href', '#/cuenta')
      expect(within(nav).queryByRole('link', { name: 'Iniciar sesión' })).not.toBeInTheDocument()
      expect(within(nav).queryByRole('link', { name: 'Registrarme' })).not.toBeInTheDocument()
      await screen.findByRole('heading', { name: 'Destacados' })
    },
  )

  it('CA-004-16 la búsqueda del encabezado navega a #/buscar?q=npr y pide /publicaciones?q=npr', async () => {
    const api = simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: INICIO_VACIO,
      [rutaPublicaciones('q=npr')]: pagina([V1]),
    })
    abrirApp('#/')
    const user = userEvent.setup()
    await user.type(screen.getByRole('searchbox', { name: 'Buscar vehículos' }), 'npr{Enter}')
    await waitFor(() => expect(window.location.hash).toBe('#/buscar?q=npr'))
    expect(await screen.findByRole('link', { name: V1.titulo })).toBeInTheDocument()
    expect(api.llamadasA(rutaPublicaciones('q=npr'))).toHaveLength(1)
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Buscar vehículos para «npr»')
  })

  it('CA-004-16 también con el botón "Buscar" (recorta espacios)', async () => {
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: INICIO_VACIO,
      [rutaPublicaciones('q=npr')]: pagina([V1]),
    })
    abrirApp('#/')
    const user = userEvent.setup()
    await user.type(screen.getByRole('searchbox', { name: 'Buscar vehículos' }), '  npr  ')
    await user.click(within(encabezado()).getByRole('button', { name: 'Buscar' }))
    await waitFor(() => expect(window.location.hash).toBe('#/buscar?q=npr'))
  })

  it('CA-005-03 / CA-006-02 "Camiones" en el encabezado lleva a la categoría con el filtro elegido', async () => {
    const api = simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: INICIO_VACIO,
      [rutaPublicaciones('tipoId=2')]: pagina([V1, V2]),
      [rutaPublicaciones('tipoId=1')]: pagina([V4]),
    })
    abrirApp('#/')
    const user = userEvent.setup()
    await user.click(await within(navPrincipal()).findByRole('link', { name: 'Camiones' }))
    await waitFor(() => expect(window.location.hash).toBe('#/buscar?tipoId=2'))
    expect(await screen.findByRole('heading', { level: 1, name: 'Camiones' })).toBeInTheDocument()
    expect(screen.getByLabelText('Categoría')).toHaveValue('2')
    expect(within(navPrincipal()).getByRole('link', { name: 'Camiones' })).toHaveAttribute('aria-current', 'page')
    expect(await screen.findByRole('link', { name: V2.titulo })).toBeInTheDocument()
    expect(api.llamadasA(rutaPublicaciones('tipoId=2'))).toHaveLength(1)

    await user.click(within(navPrincipal()).getByRole('link', { name: 'Carros' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Carros' })).toBeInTheDocument()
    expect(screen.getByLabelText('Categoría')).toHaveValue('1')
    expect(await screen.findByRole('link', { name: V4.titulo })).toBeInTheDocument()
  })
})

describe('Inicio', () => {
  it('CA-001-06 sin hash se ve el inicio (no el login), con un h1 y Destacados antes que Recientes', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: INICIO_OK })
    abrirApp('')
    const destacados = await screen.findByRole('heading', { level: 2, name: 'Destacados' })
    const recientes = screen.getByRole('heading', { level: 2, name: 'Recientes' })
    expect(destacados.compareDocumentPosition(recientes) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
    expect(screen.queryByRole('heading', { name: 'Iniciar sesión' })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Contraseña')).not.toBeInTheDocument()
    const accesos = screen.getByRole('list', { name: 'Categorías' })
    for (const [nombre, id] of [['Carros', 1], ['Camiones', 2], ['Carrocerías', 3], ['Maquinaria', 4]] as const) {
      expect(within(accesos).getByRole('link', { name: nombre })).toHaveAttribute('href', `#/buscar?tipoId=${id}`)
    }
  })

  it('CA-002-04 / CA-003-08 Recientes muestra 4 tarjetas en el orden de la API; V1 con etiqueta "Destacado"', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: INICIO_OK })
    abrirApp('#/')
    const recientes = (await screen.findByRole('heading', { name: 'Recientes' })).closest('section') as HTMLElement
    const tarjetas = within(recientes).getAllByRole('article')
    expect(tarjetas.map((t) => within(t).getByRole('heading').textContent)).toEqual([
      V1.titulo,
      V2.titulo,
      V3.titulo,
      V4.titulo,
    ])
    expect(within(tarjetas[0]).getByText('Destacado')).toBeInTheDocument()
    expect(within(tarjetas[1]).queryByText('Destacado')).not.toBeInTheDocument()
    const destacados = screen.getByRole('heading', { name: 'Destacados' }).closest('section') as HTMLElement
    expect(within(destacados).getAllByRole('article')).toHaveLength(1)
  })

  it('CA-001-07 sin vehículos: "Pronto tendremos vehículos publicados." y acceso a la búsqueda, sin secciones vacías', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: INICIO_VACIO })
    abrirApp('#/')
    expect(await screen.findByText('Pronto tendremos vehículos publicados.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ir a la búsqueda' })).toHaveAttribute('href', '#/buscar')
    expect(screen.queryByRole('heading', { name: 'Destacados' })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Recientes' })).not.toBeInTheDocument()
  })

  it('Destacados se oculta si solo él está vacío (propuesta C-P3-6 p)', async () => {
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: { status: 200, body: { destacados: [], recientes: [V2] } },
    })
    abrirApp('#/')
    expect(await screen.findByRole('heading', { name: 'Recientes' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Destacados' })).not.toBeInTheDocument()
  })

  it('CA-001-08 / CA-UI-10 estado de carga y, si falla la red, "No se pudo conectar con el servidor." con Reintentar', async () => {
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: [{ red: true }, INICIO_OK],
    })
    abrirApp('#/')
    expect(screen.getByText('Cargando vehículos...')).toHaveAttribute('role', 'status')
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo conectar con el servidor.')
    await userEvent.setup().click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByRole('heading', { name: 'Destacados' })).toBeInTheDocument()
  })

  it('un 500 del inicio se muestra como problema de conexión, sin detalles internos', async () => {
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: { status: 500, body: { error: 'System.Exception: detalle interno' } },
    })
    abrirApp('#/')
    const alerta = await screen.findByRole('alert')
    expect(alerta).toHaveTextContent('No se pudo conectar con el servidor.')
    expect(alerta).not.toHaveTextContent('System.Exception')
  })
})

describe('Tarjeta', () => {
  async function tarjetaDe(titulo: string) {
    const enlace = await screen.findByRole('link', { name: titulo })
    return enlace.closest('article') as HTMLElement
  }

  it('CA-UI-05 V1: foto, título, "$ 120.000.000", "2020", "85.000 km", ciudad y un único enlace al detalle', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: { status: 200, body: { destacados: [], recientes: [V1] } } })
    abrirApp('#/')
    const t = await tarjetaDe(V1.titulo)
    expect(within(t).getByText('$ 120.000.000')).toBeInTheDocument()
    expect(t).toHaveTextContent('2020')
    expect(t).toHaveTextContent('85.000 km')
    expect(t).toHaveTextContent('Bogotá, Cundinamarca')
    expect(within(t).getByRole('img', { name: `Foto de ${V1.titulo}` })).toHaveAttribute('src', V1.fotoPortada)
    const enlaces = within(t).getAllByRole('link')
    expect(enlaces).toHaveLength(1)
    expect(enlaces[0]).toHaveAttribute('href', '#/vehiculo/chevrolet-npr-2020')
    expect(enlaces[0]).toHaveAccessibleName(V1.titulo)
  })

  it('CA-D11-02 / CA-003-08 etiquetas con texto: Reservado, Destacado y las dos a la vez; DISPONIBLE sin etiqueta', async () => {
    const ambos = { ...V2, slug: 'ambos', titulo: 'Reservado y destacado', destacado: true }
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: { status: 200, body: { destacados: [], recientes: [V1, V2, V3, ambos] } },
    })
    abrirApp('#/')
    const t1 = await tarjetaDe(V1.titulo)
    expect(within(t1).getByText('Destacado')).toBeInTheDocument()
    expect(within(t1).queryByText('Reservado')).not.toBeInTheDocument()
    const t2 = await tarjetaDe(V2.titulo)
    expect(within(t2).getByText('Reservado')).toBeInTheDocument()
    const t3 = await tarjetaDe(V3.titulo)
    expect(within(t3).queryByText('Reservado')).not.toBeInTheDocument()
    expect(within(t3).queryByText('Destacado')).not.toBeInTheDocument()
    const t4 = await tarjetaDe(ambos.titulo)
    expect(within(t4).getByText('Reservado')).toBeInTheDocument()
    expect(within(t4).getByText('Destacado')).toBeInTheDocument()
  })

  it('CA-DET-12 sin foto se ve la imagen de reemplazo con texto alternativo; si la foto falla, también', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: { status: 200, body: { destacados: [], recientes: [V1, V3] } } })
    abrirApp('#/')
    const sinFoto = await tarjetaDe(V3.titulo)
    expect(within(sinFoto).queryByRole('img', { name: `Foto de ${V3.titulo}` })).not.toBeInTheDocument()
    expect(within(sinFoto).getByRole('img', { name: 'Sin foto disponible' })).toBeInTheDocument()
    const conFoto = await tarjetaDe(V1.titulo)
    fireEvent.error(within(conFoto).getByRole('img', { name: `Foto de ${V1.titulo}` }))
    expect(within(conFoto).getByRole('img', { name: 'Sin foto disponible' })).toBeInTheDocument()
    expect(conFoto.querySelector('img')).toBeNull()
  })

  it('CA-D17-06 a 08 / CA-UI-05 kilometraje en la tarjeta: Carrocería no, Maquinaria con 0 no, con 1200 sí, Camión con 0 sí', async () => {
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: { status: 200, body: { destacados: [], recientes: [C1, { ...C1, slug: 'c2', titulo: 'Estacas C2', kilometraje: 500 }, M1, M2, K0] } },
    })
    abrirApp('#/')
    expect(await tarjetaDe(C1.titulo)).not.toHaveTextContent(/km/)
    expect(await tarjetaDe('Estacas C2')).not.toHaveTextContent(/km/)
    expect(await tarjetaDe(M1.titulo)).not.toHaveTextContent(/km/)
    expect(await tarjetaDe(M2.titulo)).toHaveTextContent('1.200 km')
    expect(await tarjetaDe(K0.titulo)).toHaveTextContent('0 km')
  })
})

describe('Navegación por hash', () => {
  it('CA-UI-01 cambiar el hash muestra Inicio, Resultados, Detalle y Cuenta sin recargar', async () => {
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: INICIO_OK,
      [rutaPublicaciones()]: pagina([V1]),
      [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1) },
    })
    abrirApp('#/')
    expect(await screen.findByRole('heading', { name: 'Destacados' })).toBeInTheDocument()
    const contenedor = document.body.firstChild

    act(() => {
      window.location.hash = '#/buscar'
    })
    expect(await screen.findByRole('heading', { level: 1, name: 'Buscar vehículos' })).toBeInTheDocument()
    expect(document.title).toBe('Buscar vehículos | Financiamos Tu Camión')

    act(() => {
      window.location.hash = '#/vehiculo/chevrolet-npr-2020'
    })
    expect(await screen.findByRole('heading', { level: 1, name: V1.titulo })).toBeInTheDocument()

    act(() => {
      window.location.hash = '#/cuenta'
    })
    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('type', 'password')
    expect(document.body.firstChild).toBe(contenedor)
  })

  it('CA-UI-02 un hash desconocido muestra el inicio, nunca una pantalla en blanco', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: INICIO_OK })
    abrirApp('#/xyz')
    expect(screen.getByRole('heading', { level: 1, name: 'Financiamos Tu Camión' })).toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'Destacados' })).toBeInTheDocument()
  })

  it('CA-DET-10 pulsar la tarjeta abre el detalle', async () => {
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: INICIO_OK,
      [rutaDetalle(V2.slug)]: { status: 200, body: detalle(V2) },
    })
    abrirApp('#/')
    const recientes = (await screen.findByRole('heading', { name: 'Recientes' })).closest('section') as HTMLElement
    await userEvent.setup().click(within(recientes).getByRole('link', { name: V2.titulo }))
    await waitFor(() => expect(window.location.hash).toBe('#/vehiculo/kenworth-t800-2018'))
    expect(await screen.findByRole('heading', { level: 1, name: V2.titulo })).toBeInTheDocument()
  })

  it('CA-UI-08 un solo h1 por pantalla y todas las imágenes con texto alternativo', async () => {
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: INICIO_OK,
      [rutaPublicaciones()]: pagina([V1, V2]),
      [rutaDetalle(V1.slug)]: {
        status: 200,
        body: detalle(V1, { fotos: [{ url: '/f/1.jpg', orden: 1, esPortada: true }, { url: '/f/2.jpg', orden: 2, esPortada: false }] }),
      },
    })
    abrirApp('#/')
    await screen.findByRole('heading', { name: 'Destacados' })
    const revisar = () => {
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
      expect(document.querySelectorAll('img:not([alt])')).toHaveLength(0)
      for (const campo of document.querySelectorAll('input, select')) {
        expect(campo).toHaveAccessibleName()
      }
    }
    revisar()
    act(() => {
      window.location.hash = '#/buscar'
    })
    await screen.findByText('2 vehículos encontrados')
    revisar()
    act(() => {
      window.location.hash = '#/vehiculo/chevrolet-npr-2020'
    })
    await screen.findByRole('heading', { level: 1, name: V1.titulo })
    revisar()
  })
})

describe('Acceso público y comprador en solo lectura', () => {
  it('CA-SE005-08 sin sesión se recorre el catálogo sin login y sin Authorization en /api/catalogo', async () => {
    const api = simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: INICIO_OK,
      [rutaPublicaciones()]: pagina([V1]),
      [rutaPublicaciones('tipoId=2')]: pagina([V1]),
      [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1) },
    })
    abrirApp('#/')
    await screen.findByRole('heading', { name: 'Destacados' })
    for (const [hash, h1] of [['#/buscar', 'Buscar vehículos'], ['#/buscar?tipoId=2', 'Camiones'], ['#/vehiculo/chevrolet-npr-2020', V1.titulo]]) {
      act(() => {
        window.location.hash = hash
      })
      expect(await screen.findByRole('heading', { level: 1, name: h1 })).toBeInTheDocument()
      expect(screen.queryByLabelText('Contraseña')).not.toBeInTheDocument()
      expect(screen.queryByText(/Inicia sesión|regístrate/i)).not.toBeInTheDocument()
    }
    expect(api.llamadas.every((l) => l.url.startsWith('/api/catalogo/'))).toBe(true)
    for (const l of api.llamadas) expect(cabecera(l.init, 'Authorization')).toBeUndefined()
  })

  it.each(['COMPRADOR', 'ASESOR', 'ADMINISTRADOR'])(
    'CA-D03-02 / CA-D03-04 / CA-DET-15 con sesión de %s: sin botones de gestión y sin token en /api/catalogo',
    async (rol) => {
      guardarSesionFicticia(Date.now() + DOS_HORAS)
      const api = simularFetch({
        [RUTA_FILTROS]: respuestaFiltros(),
        [RUTA_INICIO]: INICIO_OK,
        [ME]: { status: 200, body: { ...PERFIL_FICTICIO, roles: [rol] } },
        [rutaPublicaciones()]: pagina([V1, V2]),
        [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1, { fotos: [{ url: '/f/1.jpg', orden: 1, esPortada: true }, { url: '/f/2.jpg', orden: 2, esPortada: false }] }) },
      })
      abrirApp('#/')
      await screen.findByRole('heading', { name: 'Destacados' })
      await waitFor(() => expect(api.llamadasA(ME)).toHaveLength(1))
      sinBotonesDeGestion()
      act(() => {
        window.location.hash = '#/buscar'
      })
      await screen.findByText('2 vehículos encontrados')
      sinBotonesDeGestion()
      act(() => {
        window.location.hash = '#/vehiculo/chevrolet-npr-2020'
      })
      await screen.findByRole('heading', { level: 1, name: V1.titulo })
      sinBotonesDeGestion()

      const catalogo = api.llamadas.filter((l) => l.url.startsWith('/api/catalogo'))
      expect(catalogo.length).toBeGreaterThanOrEqual(4)
      for (const l of catalogo) expect(cabecera(l.init, 'Authorization')).toBeUndefined()
      // La sesión sí se verifica con su token (no regresión P1).
      expect(cabecera(api.llamadasA(ME)[0].init, 'Authorization')).toMatch(/^Bearer /)
    },
  )

  it('CA-D03-01 el comprador con sesión ve las mismas tarjetas, en el mismo orden, que un visitante', async () => {
    const rutas = {
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: INICIO_OK,
      [rutaPublicaciones('marcaId=10')]: pagina([V1, V4]),
    }
    simularFetch(rutas)
    abrirApp('#/')
    await screen.findByRole('heading', { name: 'Destacados' })
    const inicioAnonimo = titulosTarjetas()
    const textoAnonimo = screen.getByRole('main').textContent
    act(() => {
      window.location.hash = '#/buscar?marcaId=10'
    })
    await screen.findByText('2 vehículos encontrados')
    const busquedaAnonima = titulosTarjetas()
    cleanup()

    guardarSesionFicticia(Date.now() + DOS_HORAS)
    simularFetch({ ...rutas, [ME]: { status: 200, body: PERFIL_FICTICIO } })
    abrirApp('#/')
    await screen.findByRole('heading', { name: 'Destacados' })
    expect(titulosTarjetas()).toEqual(inicioAnonimo)
    expect(screen.getByRole('main').textContent).toBe(textoAnonimo)
    act(() => {
      window.location.hash = '#/buscar?marcaId=10'
    })
    await screen.findByText('2 vehículos encontrados')
    expect(titulosTarjetas()).toEqual(busquedaAnonima)
  })
})

describe('Sesión y catálogo (no regresión P1/P2)', () => {
  it('CA-REG-02 con sesión de comprador en #/cuenta se ve el perfil sin panel de administración y "Mi cuenta"', async () => {
    guardarSesionFicticia(Date.now() + DOS_HORAS)
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [ME]: { status: 200, body: PERFIL_FICTICIO } })
    abrirApp('#/cuenta')
    expect(await screen.findByRole('heading', { name: /Bienvenido/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /administración/i })).not.toBeInTheDocument()
    expect(within(navPrincipal()).getByRole('link', { name: 'Mi cuenta' })).toHaveAttribute('aria-current', 'page')
  })

  it('CA-REG-07 al cerrar sesión el encabezado vuelve a "Iniciar sesión / Registrarme" y el catálogo sigue igual', async () => {
    guardarSesionFicticia(Date.now() + DOS_HORAS)
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: INICIO_OK,
      [ME]: { status: 200, body: PERFIL_FICTICIO },
      '/api/auth/logout': { status: 204 },
    })
    abrirApp('#/cuenta')
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Cerrar sesión' }))
    expect(await within(navPrincipal()).findByRole('link', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(within(navPrincipal()).getByRole('link', { name: 'Registrarme' })).toBeInTheDocument()
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
    act(() => {
      window.location.hash = '#/'
    })
    expect(await screen.findByRole('heading', { name: 'Destacados' })).toBeInTheDocument()
  })

  async function sesionInactivaEnElCatalogo() {
    vi.useFakeTimers()
    const inicio = new Date('2026-10-26T15:00:00Z').getTime()
    vi.setSystemTime(inicio)
    guardarSesionFicticia(inicio + DOS_HORAS)
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: INICIO_OK,
      [ME]: { status: 200, body: PERFIL_FICTICIO },
    })
    abrirApp('#/')
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(within(navPrincipal()).getByRole('link', { name: 'Mi cuenta' })).toBeInTheDocument()
    await act(async () => {
      await vi.advanceTimersByTimeAsync(30 * 60 * 1000)
    })
  }

  it('CA-REG-05 30 min sin actividad en el catálogo: se borra la sesión, vuelve "Iniciar sesión" y el catálogo sigue visible', async () => {
    await sesionInactivaEnElCatalogo()
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
    expect(within(navPrincipal()).getByRole('link', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Destacados' })).toBeInTheDocument()
    expect(window.location.hash).toBe('#/')
  })

  it('CA-REG-05 30 min sin actividad en el catálogo: se avisa "Tu sesión terminó. Inicia sesión de nuevo."', async () => {
    await sesionInactivaEnElCatalogo()
    expect(screen.getByText('Tu sesión terminó. Inicia sesión de nuevo.')).toBeInTheDocument()
  })
})
