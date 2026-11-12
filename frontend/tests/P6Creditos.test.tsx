// P6 — Créditos (HU-039 a HU-042), búsquedas "Busco un vehículo" (HU-034 a HU-038) y "Mis solicitudes" (D-53, D-59).
// Contrato: docs/criterios/P6-contrato-api.md. D-42, D-54 A, D-55 B, D-56 A, D-57 A, D-58 A, D-59.
// CA-P6-02, 03, 05, 06, 08, 11 a 14, 16 a 23, 26, 27, 34 a 36, 39 a 46, 50 a 54, 56, 60, 66, 68 a 72, 74 a 76.
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import App from '../src/App'
import { resultadosCierrePara } from '../src/api/leads'
import {
  lineasPara,
  validarBusqueda,
  type FormularioBusqueda as DatosFormularioBusqueda,
  type OpcionesBusqueda,
} from '../src/api/solicitudes'
import { FormularioBusqueda } from '../src/components/BusquedaVehiculo'
import { leerRuta } from '../src/rutas'
import { detalle, pagina, respuestaFiltros, rutaDetalle, rutaPublicaciones, TIPOS, V1, V2 } from './catalogoDatos'
import { cabecera, RUTA_FILTROS, simularFetch, TOKEN_FICTICIO, type RespuestaSimulada } from './helpers'
import { abrirApp, CATALOGOS_INVENTARIO, RUTA_CATALOGOS_INV, type Rutas } from './p4pDatos'
import { cuerpo, detalleLead, lead, paginaLeads, rutaLeads } from './p5Datos'

const RUTA_CREDITOS = '/api/creditos'
const RUTA_BUSQUEDAS = '/api/busquedas'
const RUTA_INICIO = '/api/catalogo/inicio'
const MENSAJE_OK = '¡Listo! Recibimos tu solicitud. Te contactaremos pronto por teléfono o WhatsApp.'
const MENSAJE_FALLO =
  'No pudimos enviar tus datos. Intenta de nuevo en unos minutos o escríbenos por WhatsApp al 316 615 2907.'
const MENSAJE_429 = 'Has enviado muchas solicitudes. Intenta de nuevo en unos minutos.'
const MENSAJE_NO_DISPONIBLE = 'Este vehículo ya no está disponible. Mira otros vehículos en el catálogo.'
const OK: RespuestaSimulada = { status: 201, body: { mensaje: MENSAJE_OK } }
const FILTROS_CON_VOLQUETA = respuestaFiltros([...TIPOS, { id: 5, nombre: 'Volqueta', total: 1 }])
const RUTA_LISTAS = '/api/catalogo/listas-busqueda'
/** GET /api/catalogo/listas-busqueda (Cambios al contrato P6): todo lo activo, en orden alfabético. */
const LISTAS: RespuestaSimulada = {
  status: 200,
  body: {
    tipos: [
      { id: 2, nombre: 'Camión' },
      { id: 1, nombre: 'Carro' },
      { id: 3, nombre: 'Carrocería' },
      { id: 4, nombre: 'Maquinaria' },
      { id: 5, nombre: 'Volqueta' },
    ],
    marcas: [
      {
        id: 10,
        nombre: 'Chevrolet',
        lineas: [
          { id: 100, nombre: 'NHR', tipoIds: [2] },
          { id: 101, nombre: 'NPR', tipoIds: [] },
          { id: 102, nombre: 'Spark', tipoIds: [1] },
        ],
      },
      { id: 11, nombre: 'Kenworth', lineas: [{ id: 110, nombre: 'T800', tipoIds: [2] }] },
    ],
    departamentos: [
      { id: 5, nombre: 'Antioquia', municipios: [{ id: 51, nombre: 'Envigado' }, { id: 50, nombre: 'Medellín' }] },
      { id: 6, nombre: 'Cundinamarca', municipios: [{ id: 60, nombre: 'Bogotá' }] },
    ],
  },
}

function abrirAnonimo(hash: string, rutas: Rutas = {}) {
  const api = simularFetch({ [RUTA_FILTROS]: FILTROS_CON_VOLQUETA, [RUTA_LISTAS]: LISTAS, ...rutas })
  window.location.hash = hash
  render(<App />)
  return { api, user: userEvent.setup() }
}

const casilla = () => screen.getByRole('checkbox', { name: /Autorizo el tratamiento de mis datos personales/ })
const enviar = () => screen.getByRole('button', { name: /^(Enviar|Enviando\.\.\.)$/ })
const formCredito = () => screen.findByRole('form', { name: 'Formulario de solicitud de crédito' })
const formBusqueda = () => screen.findByRole('form', { name: 'Formulario Busco un vehículo' })

// ---- Rutas y accesos ----

describe('Rutas y accesos del portal (CA-P6-02, 03, 26)', () => {
  it('lee las rutas nuevas; un slug mal codificado abre el inicio', () => {
    expect(leerRuta('#/credito')).toEqual({ nombre: 'credito', slug: null })
    expect(leerRuta('#/credito/chevrolet-npr-2020')).toEqual({ nombre: 'credito', slug: 'chevrolet-npr-2020' })
    expect(leerRuta('#/credito/%E0%A4%A')).toEqual({ nombre: 'inicio' })
    expect(leerRuta('#/busco-un-vehiculo/')).toEqual({ nombre: 'buscoVehiculo' })
    expect(leerRuta('#/mis-solicitudes')).toEqual({ nombre: 'misSolicitudes' })
  })

  it('la banda de crédito de la portada abre #/credito y la franja de servicios sigue sin enlaces', async () => {
    abrirAnonimo('#/', { [RUTA_INICIO]: { status: 200, body: { destacados: [], recientes: [] } } })
    const banda = await screen.findByRole('region', { name: '¿Necesitas crédito para tu camión?' })
    expect(within(banda).getByRole('link', { name: /Quiero un crédito/ })).toHaveAttribute('href', '#/credito')
    const servicios = screen.getByRole('region', { name: 'Nuestros servicios' })
    expect(within(servicios).queryAllByRole('link')).toHaveLength(0)
    // D-56 (d) B (responsable, 2026-11-12): la portada también ofrece la búsqueda, además del catálogo y el pie.
    expect(within(screen.getByRole('main')).getByRole('link', { name: /Busco un vehículo/ })).toHaveAttribute(
      'href',
      '#/busco-un-vehiculo',
    )
  })

  it('el pie ofrece "Busco un vehículo" en Servicios y el menú principal no cambia', async () => {
    abrirAnonimo('#/busco-un-vehiculo')
    await formBusqueda()
    const pie = screen.getByRole('navigation', { name: 'Servicios en el pie de página' })
    expect(within(pie).getByRole('link', { name: 'Busco un vehículo' })).toHaveAttribute('href', '#/busco-un-vehiculo')
    const encabezado = screen.getByRole('banner')
    expect(within(encabezado).queryByRole('link', { name: /Busco|crédito/i })).not.toBeInTheDocument()
  })

  it('el catálogo ofrece "Busco un vehículo" cuando no hay resultados', async () => {
    abrirAnonimo('#/buscar', { [rutaPublicaciones()]: pagina([]) })
    const acceso = await screen.findByRole('complementary', { name: '¿No encuentras lo que buscas?' })
    expect(within(acceso).getByRole('link', { name: 'Busco un vehículo' })).toHaveAttribute('href', '#/busco-un-vehiculo')
  })

  it('el catálogo ofrece "Busco un vehículo" al final de la lista de resultados', async () => {
    abrirAnonimo('#/buscar', { [rutaPublicaciones()]: pagina([V1, V2]) })
    await screen.findByText('Kenworth T800 2018')
    const acceso = screen.getByRole('complementary', { name: '¿No encuentras lo que buscas?' })
    expect(within(acceso).getByRole('link', { name: 'Busco un vehículo' })).toBeInTheDocument()
  })

  it('la ficha ofrece "Solicitar crédito para este vehículo" junto al contacto', async () => {
    abrirAnonimo(`#/vehiculo/${V1.slug}`, { [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1) } })
    const enlace = await screen.findByRole('link', { name: 'Solicitar crédito para este vehículo' })
    expect(enlace).toHaveAttribute('href', '#/credito/chevrolet-npr-2020')
    expect(screen.getByRole('form', { name: 'Formulario de contacto' })).toBeInTheDocument()
  })

  it('CA-P6-05 el asesor dueño no ve el acceso al crédito en su propia ficha', async () => {
    abrirApp(`#/vehiculo/${V1.slug}`, ['ASESOR'], {
      [RUTA_FILTROS]: respuestaFiltros(),
      [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1, { esPropio: true }) },
    })
    await screen.findByText(/Esta es una de tus publicaciones/)
    expect(screen.queryByRole('link', { name: 'Solicitar crédito para este vehículo' })).not.toBeInTheDocument()
  })
})

// ---- Crédito ----

describe('Crédito desde la portada (CA-P6-01, 08, 11 a 14, 27, 35, 36)', () => {
  it('pide solo nombre, teléfono, correo y comentario; explica la intermediación; casilla sin marcar', async () => {
    abrirAnonimo('#/credito')
    const form = await formCredito()
    expect(screen.getByRole('heading', { level: 1, name: 'Solicita tu crédito' })).toBeInTheDocument()
    expect(document.title).toBe('Solicita tu crédito | Financiamos Tu Camión')
    expect(screen.getByText(/Somos intermediarios: gestionamos tu crédito ante bancos/)).toBeInTheDocument()
    expect(within(form).getByText('Campo obligatorio')).toBeInTheDocument()
    expect(screen.getByLabelText('Nombre')).toHaveAttribute('aria-required', 'true')
    expect(screen.getByLabelText('Teléfono o WhatsApp')).toHaveAttribute('inputmode', 'tel')
    expect(screen.getByLabelText('Correo (opcional)')).not.toHaveAttribute('aria-required')
    const comentario = screen.getByLabelText('Comentario (opcional)')
    expect(comentario).not.toHaveAttribute('aria-required')
    expect(comentario).toHaveAttribute('maxLength', '500')
    expect(comentario).toHaveAccessibleDescription(
      /Cuéntanos qué vehículo te interesa\. No escribas ingresos, documentos ni datos bancarios\./,
    )
    expect(document.getElementById('credito-comentario-contador')).toHaveTextContent('0/500')
    expect(document.getElementById('credito-comentario-contador')).toHaveAttribute('aria-live', 'polite')
    expect(casilla()).not.toBeChecked()
    // D-55 B / CA-P6-12: nada financiero ni tipo de vehículo.
    expect(
      within(form).queryByLabelText(/monto|cuota|ingreso|documento|tipo de vehículo|entidad|banco/i),
    ).not.toBeInTheDocument()
    expect(within(form).getAllByRole('checkbox')).toHaveLength(1)
    const trampa = form.querySelector<HTMLInputElement>('input[name="sitioWeb"]')
    expect(trampa).toHaveAttribute('tabindex', '-1')
    expect(trampa?.closest('[aria-hidden="true"]')).not.toBeNull()
  })

  it('anónimo: POST sin token con el cuerpo del contrato y confirmación con WhatsApp y tres salidas', async () => {
    const { api, user } = abrirAnonimo('#/credito', { [RUTA_CREDITOS]: OK })
    await formCredito()
    await user.type(screen.getByLabelText('Nombre'), ' Ana Pérez ')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '300 123 4567')
    await user.type(screen.getByLabelText('Comentario (opcional)'), 'Me interesa un camión sencillo')
    await user.click(casilla())
    await user.click(enviar())

    const exito = await screen.findByText(MENSAJE_OK)
    expect(exito).toHaveAttribute('role', 'status')
    await waitFor(() => expect(exito.parentElement).toHaveFocus())
    const llamada = api.llamadasA(RUTA_CREDITOS)
    expect(llamada).toHaveLength(1)
    expect(cabecera(llamada[0].init, 'Authorization')).toBeUndefined()
    expect(cuerpo(api.llamadas, RUTA_CREDITOS)).toEqual({
      slug: null,
      nombre: 'Ana Pérez',
      telefono: '300 123 4567',
      correo: null,
      mensaje: 'Me interesa un camión sencillo',
      autorizacionDatos: true,
      sitioWeb: '',
    })
    expect(screen.queryByRole('form', { name: 'Formulario de solicitud de crédito' })).not.toBeInTheDocument()
    const whatsapp = screen.getByRole('link', { name: /Escribir por WhatsApp/ })
    expect(whatsapp.getAttribute('href')).toMatch(/^https:\/\/wa\.me\/573166152907\?text=/)
    expect(screen.getByRole('link', { name: 'Volver al inicio' })).toHaveAttribute('href', '#/')
    expect(screen.getByRole('link', { name: 'Ver vehículos' })).toHaveAttribute('href', '#/buscar')
    await user.click(screen.getByRole('button', { name: 'Enviar otra solicitud' }))
    expect(screen.getByLabelText('Nombre')).toHaveValue('')
    expect(casilla()).not.toBeChecked()
  })

  it('comentario vacío viaja como null y el correo en blanco también', async () => {
    const { api, user } = abrirAnonimo('#/credito', { [RUTA_CREDITOS]: OK })
    await formCredito()
    await user.type(screen.getByLabelText('Nombre'), 'Ana')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '3001234567')
    await user.type(screen.getByLabelText('Correo (opcional)'), '   ')
    await user.click(casilla())
    await user.click(enviar())
    await screen.findByText(MENSAJE_OK)
    expect(cuerpo(api.llamadas, RUTA_CREDITOS)).toMatchObject({ correo: null, mensaje: null })
  })

  it('validación en el navegador: errores por campo, foco al primero y sin llamar a la API', async () => {
    const { api, user } = abrirAnonimo('#/credito')
    await formCredito()
    fireEvent.change(screen.getByLabelText('Correo (opcional)'), { target: { value: 'no-es-correo' } })
    await user.click(enviar())
    expect(screen.getByLabelText('Nombre')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText('Nombre')).toHaveAccessibleDescription('Escribe tu nombre (entre 2 y 150 caracteres).')
    expect(screen.getByLabelText('Teléfono o WhatsApp')).toHaveAccessibleDescription(
      /Escribe un celular o fijo colombiano de 10 dígitos/,
    )
    expect(screen.getByLabelText('Correo (opcional)')).toHaveAccessibleDescription(
      'Escribe un correo válido o deja el campo vacío.',
    )
    expect(casilla()).toHaveAccessibleDescription(
      'Debes autorizar el tratamiento de tus datos para enviar el formulario.',
    )
    await waitFor(() => expect(screen.getByLabelText('Nombre')).toHaveFocus())
    expect(api.llamadasA(RUTA_CREDITOS)).toHaveLength(0)
  })

  it('un 400 del servidor marca el comentario y conserva lo escrito', async () => {
    const { user } = abrirAnonimo('#/credito', {
      [RUTA_CREDITOS]: {
        status: 400,
        body: { error: 'Revisa los datos marcados.', errores: { mensaje: ['El comentario no puede pasar de 500 caracteres.'] } },
      },
    })
    await formCredito()
    await user.type(screen.getByLabelText('Nombre'), 'Ana')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '3001234567')
    await user.type(screen.getByLabelText('Comentario (opcional)'), 'Texto')
    await user.click(casilla())
    await user.click(enviar())
    const comentario = screen.getByLabelText('Comentario (opcional)')
    await waitFor(() => expect(comentario).toHaveAccessibleDescription(/El comentario no puede pasar de 500 caracteres\./))
    await waitFor(() => expect(comentario).toHaveFocus())
    expect(comentario).toHaveValue('Texto')
    expect(screen.getByLabelText('Nombre')).toHaveValue('Ana')
  })

  it.each([
    ['429', { status: 429, body: { error: MENSAJE_429 } } as RespuestaSimulada, MENSAJE_429],
    ['413', { status: 413 } as RespuestaSimulada, 'La solicitud es demasiado grande.'],
    ['500', { status: 500, body: { error: 'detalle interno' } } as RespuestaSimulada, MENSAJE_FALLO],
    ['sin conexión', { red: true } as RespuestaSimulada, MENSAJE_FALLO],
  ])('%s: muestra el mensaje del contrato sin trazas y conserva el formulario', async (_n, respuesta, mensaje) => {
    const { user } = abrirAnonimo('#/credito', { [RUTA_CREDITOS]: respuesta })
    await formCredito()
    await user.type(screen.getByLabelText('Nombre'), 'Ana')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '3001234567')
    await user.click(casilla())
    await user.click(enviar())
    expect(await screen.findByText(mensaje)).toHaveAttribute('role', 'alert')
    expect(screen.queryByText('detalle interno')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Nombre')).toHaveValue('Ana')
  })

  it('CA-P6-06 con sesión de comprador prellena el perfil y envía el token', async () => {
    const { api, user } = abrirApp('#/credito', ['COMPRADOR'], { [RUTA_FILTROS]: respuestaFiltros(), [RUTA_CREDITOS]: OK })
    await waitFor(() => expect(screen.getByLabelText('Nombre')).toHaveValue('Persona Ficticia'))
    expect(screen.getByLabelText('Correo (opcional)')).toHaveValue('persona.ficticia@ejemplo.test')
    expect(casilla()).not.toBeChecked()
    await user.click(casilla())
    await user.click(enviar())
    await screen.findByText(MENSAJE_OK)
    expect(cabecera(api.llamadasA(RUTA_CREDITOS)[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
  })
})

describe('Crédito desde la ficha (CA-P6-03 a 05)', () => {
  it('muestra el vehículo (no editable) y envía su slug', async () => {
    const { api, user } = abrirAnonimo(`#/credito/${V1.slug}`, {
      [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1) },
      [RUTA_CREDITOS]: OK,
    })
    await formCredito()
    expect(screen.getByRole('heading', { level: 1, name: 'Solicita crédito para este vehículo' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Chevrolet NPR 2020' })).toHaveAttribute('href', '#/vehiculo/chevrolet-npr-2020')
    expect(screen.queryByDisplayValue('Chevrolet NPR 2020')).not.toBeInTheDocument()
    await user.type(screen.getByLabelText('Nombre'), 'Ana')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '3001234567')
    await user.click(casilla())
    await user.click(enviar())
    await screen.findByText(MENSAJE_OK)
    expect(cuerpo(api.llamadas, RUTA_CREDITOS)).toMatchObject({ slug: 'chevrolet-npr-2020', mensaje: null })
    expect(decodeURIComponent(screen.getByRole('link', { name: /Escribir por WhatsApp/ }).getAttribute('href') ?? '')).toContain(
      'Chevrolet NPR 2020',
    )
  })

  it('un vehículo que ya no es visible no muestra el formulario', async () => {
    abrirAnonimo('#/credito/no-existe', { [rutaDetalle('no-existe')]: { status: 404, body: { error: 'No.' } } })
    expect(await screen.findByText(MENSAJE_NO_DISPONIBLE)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ver el catálogo' })).toHaveAttribute('href', '#/buscar')
    expect(screen.queryByRole('form', { name: 'Formulario de solicitud de crédito' })).not.toBeInTheDocument()
  })

  it('un 404 al enviar muestra el mensaje del contrato y el enlace al catálogo', async () => {
    const { user } = abrirAnonimo(`#/credito/${V1.slug}`, {
      [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1) },
      [RUTA_CREDITOS]: { status: 404, body: { error: MENSAJE_NO_DISPONIBLE } },
    })
    await formCredito()
    await user.type(screen.getByLabelText('Nombre'), 'Ana')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '3001234567')
    await user.click(casilla())
    await user.click(enviar())
    expect(await screen.findByText(MENSAJE_NO_DISPONIBLE)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ver el catálogo' })).toBeInTheDocument()
  })

  it('el asesor dueño ve que el vehículo es suyo y no el formulario; la consulta lleva su token', async () => {
    const { api } = abrirApp(`#/credito/${V1.slug}`, ['ASESOR'], {
      [RUTA_FILTROS]: respuestaFiltros(),
      [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1, { esPropio: true }) },
    })
    expect(await screen.findByText(/Este vehículo es tuyo/)).toBeInTheDocument()
    expect(screen.queryByRole('form', { name: 'Formulario de solicitud de crédito' })).not.toBeInTheDocument()
    const consultas = api.llamadasA(rutaDetalle(V1.slug))
    expect(cabecera(consultas[consultas.length - 1].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
  })

  it('un 409 al enviar muestra "Este vehículo es tuyo"', async () => {
    const { user } = abrirAnonimo(`#/credito/${V1.slug}`, {
      [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1) },
      [RUTA_CREDITOS]: { status: 409, body: { error: 'Este vehículo es tuyo; no puedes contactarte a ti mismo.' } },
    })
    await formCredito()
    await user.type(screen.getByLabelText('Nombre'), 'Ana')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '3001234567')
    await user.click(casilla())
    await user.click(enviar())
    expect(await screen.findByText('Este vehículo es tuyo; no puedes contactarte a ti mismo.')).toBeInTheDocument()
  })
})

// ---- Búsqueda ----

describe('"Busco un vehículo" (CA-P6-16 a 23, 34 a 36)', () => {
  it('todas las categorías activas de /api/catalogo/listas-busqueda y modelos por marca y categoría', async () => {
    const { api, user } = abrirAnonimo('#/busco-un-vehiculo')
    const form = await formBusqueda()
    expect(api.llamadasA(RUTA_LISTAS)).toHaveLength(1)
    expect(cabecera(api.llamadasA(RUTA_LISTAS)[0].init, 'Authorization')).toBeUndefined()
    expect(screen.getByRole('heading', { level: 1, name: 'Busco un vehículo' })).toBeInTheDocument()
    expect(document.title).toBe('Busco un vehículo | Financiamos Tu Camión')
    const categoria = screen.getByLabelText('Categoría')
    expect(categoria).toHaveAttribute('aria-required', 'true')
    expect(within(categoria).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Elige la categoría',
      'Camión',
      'Carro',
      'Carrocería',
      'Maquinaria',
      'Volqueta',
    ])
    const modelo = screen.getByLabelText('Modelo (opcional)')
    expect(modelo).toBeDisabled()
    await user.selectOptions(screen.getByLabelText('Marca (opcional)'), '10')
    expect(modelo).toBeEnabled()
    // Sin categoría: todos los modelos de la marca.
    expect(within(modelo).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Cualquier modelo',
      'NHR',
      'NPR',
      'Spark',
    ])
    // Camión: los clasificados como camión y los sin clasificar (tipoIds vacío).
    await user.selectOptions(categoria, '2')
    expect(within(modelo).getAllByRole('option').map((o) => o.textContent)).toEqual(['Cualquier modelo', 'NHR', 'NPR'])
    await user.selectOptions(modelo, '100')
    // Al pasar a Carro, NHR deja de valer y se vacía; NPR (sin clasificar) sigue.
    await user.selectOptions(categoria, '1')
    expect(screen.getByLabelText('Modelo (opcional)')).toHaveValue('')
    expect(within(modelo).getAllByRole('option').map((o) => o.textContent)).toEqual(['Cualquier modelo', 'NPR', 'Spark'])
    await user.selectOptions(modelo, '101')
    await user.selectOptions(screen.getByLabelText('Marca (opcional)'), '11')
    expect(screen.getByLabelText('Modelo (opcional)')).toHaveValue('')
    expect(screen.getByLabelText('Año desde (opcional)')).toHaveAttribute('inputmode', 'numeric')
    expect(screen.getByLabelText('Presupuesto máximo en pesos (opcional)')).toHaveAttribute('inputmode', 'numeric')
    // Departamento y luego ciudad.
    const ciudad = screen.getByLabelText('Ciudad (opcional)')
    expect(ciudad).toBeDisabled()
    await user.selectOptions(screen.getByLabelText('Departamento (opcional)'), '5')
    expect(within(ciudad).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Cualquier ciudad',
      'Envigado',
      'Medellín',
    ])
    expect(document.getElementById('busqueda-descripcion-contador')).toHaveTextContent('0/500')
    expect(within(form).getByRole('group', { name: '¿Qué vehículo buscas?' })).toBeInTheDocument()
    expect(within(form).getByRole('group', { name: 'Tus datos de contacto' })).toBeInTheDocument()
  })

  it('envía los criterios con números y null, el presupuesto con miles en pantalla y sin token', async () => {
    const { api, user } = abrirAnonimo('#/busco-un-vehiculo', { [RUTA_BUSQUEDAS]: OK })
    await formBusqueda()
    await user.selectOptions(screen.getByLabelText('Categoría'), '2')
    await user.selectOptions(screen.getByLabelText('Marca (opcional)'), '11')
    await user.selectOptions(screen.getByLabelText('Modelo (opcional)'), '110')
    await user.type(screen.getByLabelText('Año desde (opcional)'), '2015')
    await user.type(screen.getByLabelText('Año hasta (opcional)'), '2020')
    await user.selectOptions(screen.getByLabelText('Departamento (opcional)'), '5')
    await user.selectOptions(screen.getByLabelText('Ciudad (opcional)'), '50')
    fireEvent.change(screen.getByLabelText('Presupuesto máximo en pesos (opcional)'), { target: { value: '150000000' } })
    expect(screen.getByLabelText('Presupuesto máximo en pesos (opcional)')).toHaveValue('150.000.000')
    await user.type(screen.getByLabelText('Cuéntanos qué buscas (opcional)'), ' Doble troque ')
    await user.type(screen.getByLabelText('Nombre'), 'Ana Pérez')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '300 123 4567')
    await user.click(casilla())
    await user.click(enviar())
    expect(await screen.findByText(MENSAJE_OK)).toBeInTheDocument()
    expect(cabecera(api.llamadasA(RUTA_BUSQUEDAS)[0].init, 'Authorization')).toBeUndefined()
    expect(cuerpo(api.llamadas, RUTA_BUSQUEDAS)).toEqual({
      nombre: 'Ana Pérez',
      telefono: '300 123 4567',
      correo: null,
      tipoId: 2,
      marcaId: 11,
      lineaId: 110,
      anioDesde: 2015,
      anioHasta: 2020,
      presupuestoMaximo: 150000000,
      departamentoId: 5,
      municipioId: 50,
      descripcion: 'Doble troque',
      autorizacionDatos: true,
      sitioWeb: '',
    })
    // Sin botón de WhatsApp obligatorio (CA-P6-16); sí las tres salidas.
    expect(screen.queryByRole('link', { name: /Escribir por WhatsApp/ })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Enviar otra solicitud' }))
    expect(screen.getByLabelText('Categoría')).toHaveValue('')
    await waitFor(() => expect(screen.getByLabelText('Categoría')).toHaveFocus())
  })

  it('solo la categoría y los datos de contacto bastan; lo demás viaja como null', async () => {
    const { api, user } = abrirAnonimo('#/busco-un-vehiculo', { [RUTA_BUSQUEDAS]: OK })
    await formBusqueda()
    await user.selectOptions(screen.getByLabelText('Categoría'), '4')
    await user.type(screen.getByLabelText('Nombre'), 'Ana')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '6012345678')
    await user.click(casilla())
    await user.click(enviar())
    await screen.findByText(MENSAJE_OK)
    expect(cuerpo(api.llamadas, RUTA_BUSQUEDAS)).toMatchObject({
      tipoId: 4,
      marcaId: null,
      lineaId: null,
      anioDesde: null,
      anioHasta: null,
      presupuestoMaximo: null,
      departamentoId: null,
      municipioId: null,
      descripcion: null,
    })
  })

  it('validación: categoría, años, presupuesto y autorización; foco a la categoría y sin llamar a la API', async () => {
    const { api, user } = abrirAnonimo('#/busco-un-vehiculo')
    await formBusqueda()
    await user.type(screen.getByLabelText('Año desde (opcional)'), '1949')
    fireEvent.change(screen.getByLabelText('Presupuesto máximo en pesos (opcional)'), { target: { value: '999999' } })
    await user.click(enviar())
    expect(screen.getByLabelText('Categoría')).toHaveAccessibleDescription('Elige la categoría del vehículo que buscas.')
    expect(screen.getByLabelText('Año desde (opcional)')).toHaveAccessibleDescription('Revisa el rango de años.')
    expect(screen.getByLabelText('Presupuesto máximo en pesos (opcional)')).toHaveAccessibleDescription(
      /Escribe un valor en pesos entre 1\.000\.000 y 5\.000\.000\.000, o deja el campo vacío\./,
    )
    expect(casilla()).toHaveAttribute('aria-invalid', 'true')
    await waitFor(() => expect(screen.getByLabelText('Categoría')).toHaveFocus())
    expect(api.llamadasA(RUTA_BUSQUEDAS)).toHaveLength(0)
  })

  it('"desde" mayor que "hasta" marca el año final', async () => {
    const { user } = abrirAnonimo('#/busco-un-vehiculo')
    await formBusqueda()
    await user.type(screen.getByLabelText('Año desde (opcional)'), '2020')
    await user.type(screen.getByLabelText('Año hasta (opcional)'), '2015')
    await user.click(enviar())
    expect(screen.getByLabelText('Año hasta (opcional)')).toHaveAccessibleDescription('Revisa el rango de años.')
    expect(screen.getByLabelText('Año desde (opcional)')).toHaveAttribute('aria-invalid', 'false')
  })

  it('un 400 del servidor en tipoId se muestra en la categoría con el foco', async () => {
    const { user } = abrirAnonimo('#/busco-un-vehiculo', {
      [RUTA_BUSQUEDAS]: {
        status: 400,
        body: { error: 'Revisa los datos marcados.', errores: { tipoId: ['Elige la categoría del vehículo que buscas.'] } },
      },
    })
    await formBusqueda()
    await user.selectOptions(screen.getByLabelText('Categoría'), '1')
    await user.type(screen.getByLabelText('Nombre'), 'Ana')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '3001234567')
    await user.click(casilla())
    await user.click(enviar())
    await waitFor(() =>
      expect(screen.getByLabelText('Categoría')).toHaveAccessibleDescription('Elige la categoría del vehículo que buscas.'),
    )
    await waitFor(() => expect(screen.getByLabelText('Categoría')).toHaveFocus())
  })

  it('429 y sin conexión muestran los mensajes del contrato', async () => {
    const { user } = abrirAnonimo('#/busco-un-vehiculo', {
      [RUTA_BUSQUEDAS]: [{ status: 429, body: { error: MENSAJE_429 } }, { red: true }],
    })
    await formBusqueda()
    await user.selectOptions(screen.getByLabelText('Categoría'), '1')
    await user.type(screen.getByLabelText('Nombre'), 'Ana')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '3001234567')
    await user.click(casilla())
    await user.click(enviar())
    expect(await screen.findByText(MENSAJE_429)).toBeInTheDocument()
    await user.click(enviar())
    expect(await screen.findByText(MENSAJE_FALLO)).toBeInTheDocument()
  })

  it('mientras cargan las listas lo anuncia en role="status"', async () => {
    abrirAnonimo('#/busco-un-vehiculo')
    expect(screen.getByRole('status')).toHaveTextContent('Cargando las opciones...')
    await formBusqueda()
  })

  it('si fallan las listas, usa las categorías del catálogo y deja enviar solo con la categoría; reintentar las recupera', async () => {
    const { api, user } = abrirAnonimo('#/busco-un-vehiculo', {
      [RUTA_LISTAS]: [{ status: 500 }, LISTAS],
      [RUTA_BUSQUEDAS]: OK,
    })
    expect(await screen.findByText(/No pudimos cargar todas las opciones/)).toHaveAttribute('role', 'alert')
    await formBusqueda()
    // Respaldo: las cuatro categorías de D-17 del catálogo público, sin DIVIPOLA.
    expect(within(screen.getByLabelText('Categoría')).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Elige la categoría',
      'Carro',
      'Camión',
      'Carrocería',
      'Maquinaria',
    ])
    expect(screen.queryByLabelText('Departamento (opcional)')).not.toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText('Categoría'), '2')
    await user.type(screen.getByLabelText('Nombre'), 'Ana')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '3001234567')
    await user.click(casilla())
    await user.click(enviar())
    await screen.findByText(MENSAJE_OK)
    expect(cuerpo(api.llamadas, RUTA_BUSQUEDAS)).toMatchObject({ tipoId: 2, departamentoId: null, municipioId: null })
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByLabelText('Departamento (opcional)')).toBeInTheDocument()
    expect(screen.queryByText(/No pudimos cargar todas las opciones/)).not.toBeInTheDocument()
    expect(api.llamadasA(RUTA_LISTAS)).toHaveLength(2)
  })

  it('si fallan las listas y el catálogo, ofrece reintentar y no muestra el formulario', async () => {
    const { api, user } = abrirAnonimo('#/busco-un-vehiculo', {
      [RUTA_LISTAS]: [{ status: 500 }, LISTAS],
      [RUTA_FILTROS]: { red: true },
    })
    expect(await screen.findByText('No pudimos cargar las categorías. Intenta de nuevo.')).toBeInTheDocument()
    expect(screen.queryByRole('form', { name: 'Formulario Busco un vehículo' })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))
    await formBusqueda()
    expect(api.llamadasA(RUTA_LISTAS)).toHaveLength(2)
  })

  it('CA-P6-23 con sesión envía el token y prellena el contacto', async () => {
    const { api, user } = abrirApp('#/busco-un-vehiculo', ['COMPRADOR'], {
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_LISTAS]: LISTAS,
      [RUTA_BUSQUEDAS]: OK,
    })
    await waitFor(() => expect(screen.getByLabelText('Nombre')).toHaveValue('Persona Ficticia'))
    await user.selectOptions(screen.getByLabelText('Categoría'), '2')
    await user.click(casilla())
    await user.click(enviar())
    await screen.findByText(MENSAJE_OK)
    expect(cabecera(api.llamadasA(RUTA_BUSQUEDAS)[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
  })
})

describe('Búsqueda con DIVIPOLA y modelos clasificados (D-29, D-32)', () => {
  const OPCIONES: OpcionesBusqueda = {
    tipos: [
      { id: 1, nombre: 'Carro' },
      { id: 2, nombre: 'Camión' },
    ],
    marcas: [
      {
        id: 11,
        nombre: 'Kenworth',
        lineas: [
          { id: 110, nombre: 'T800', tipoIds: [2] },
          { id: 111, nombre: 'Sin clasificar', tipoIds: [] },
          { id: 112, nombre: 'Solo carro', tipoIds: [1] },
        ],
      },
    ],
    departamentos: [
      { id: 5, nombre: 'Antioquia', municipios: [{ id: 50, nombre: 'Medellín' }, { id: 51, nombre: 'Envigado' }] },
      { id: 6, nombre: 'Cundinamarca', municipios: [{ id: 60, nombre: 'Bogotá' }] },
    ],
  }

  it('la ciudad depende del departamento y viajan sus ids', async () => {
    const api = simularFetch({ [RUTA_BUSQUEDAS]: OK })
    render(<FormularioBusqueda opciones={OPCIONES} sesion={null} />)
    const user = userEvent.setup()
    const ciudad = screen.getByLabelText('Ciudad (opcional)')
    expect(ciudad).toBeDisabled()
    await user.selectOptions(screen.getByLabelText('Departamento (opcional)'), '5')
    expect(within(ciudad).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Cualquier ciudad',
      'Medellín',
      'Envigado',
    ])
    await user.selectOptions(ciudad, '50')
    await user.selectOptions(screen.getByLabelText('Departamento (opcional)'), '6')
    expect(screen.getByLabelText('Ciudad (opcional)')).toHaveValue('')
    await user.selectOptions(screen.getByLabelText('Ciudad (opcional)'), '60')
    await user.selectOptions(screen.getByLabelText('Categoría'), '2')
    await user.type(screen.getByLabelText('Nombre'), 'Ana')
    await user.type(screen.getByLabelText('Teléfono o WhatsApp'), '3001234567')
    await user.click(casilla())
    await user.click(enviar())
    await screen.findByText(MENSAJE_OK)
    expect(cuerpo(api.llamadas, RUTA_BUSQUEDAS)).toMatchObject({ departamentoId: 6, municipioId: 60 })
  })

  it('el modelo se filtra por marca y categoría; los no clasificados valen para todas', () => {
    expect(lineasPara(OPCIONES, '11', '2').map((l) => l.id)).toEqual([110, 111])
    expect(lineasPara(OPCIONES, '11', '').map((l) => l.id)).toEqual([110, 111, 112])
    expect(lineasPara(OPCIONES, '99', '2')).toEqual([])
  })

  it('validarBusqueda rechaza modelo sin marca o de otra categoría y ciudad de otro departamento', () => {
    const base: DatosFormularioBusqueda = {
      tipoId: '2',
      marcaId: '',
      lineaId: '110',
      anioDesde: '',
      anioHasta: '',
      presupuestoMaximo: '5000000001',
      departamentoId: '5',
      municipioId: '60',
      descripcion: 'x'.repeat(501),
      nombre: 'Ana',
      telefono: '3001234567',
      correo: '',
      autorizacionDatos: true,
    }
    const errores = validarBusqueda(base, OPCIONES)
    expect(errores.lineaId).toEqual(['Elige un modelo de la lista.'])
    expect(errores.municipioId).toEqual(['Elige una ciudad de la lista.'])
    expect(errores.presupuestoMaximo).toBeDefined()
    expect(errores.descripcion).toEqual(['La descripción no puede pasar de 500 caracteres.'])
    expect(validarBusqueda({ ...base, marcaId: '11', lineaId: '112', presupuestoMaximo: '', municipioId: '50', descripcion: '' }, OPCIONES)).toEqual({
      lineaId: ['Elige un modelo de la lista.'],
    })
    expect(
      validarBusqueda(
        { ...base, marcaId: '11', lineaId: '111', presupuestoMaximo: '5000000000', municipioId: '50', descripcion: '' },
        OPCIONES,
      ),
    ).toEqual({})
  })
})

// ---- Panel del administrador ----

const ADMIN_BASE: Rutas = { [RUTA_CATALOGOS_INV]: { status: 200, body: CATALOGOS_INVENTARIO } }
const ADMINISTRACION = { tipo: 'ADMINISTRACION', asesorId: null, nombre: null }
const VEHICULO_CREDITO = {
  vehiculoId: 40,
  publicacionId: 60,
  titulo: 'Chevrolet NPR 2020',
  slug: 'chevrolet-npr-2020',
  estadoVehiculo: 'ACTIVO',
  estadoPublicacion: 'PUBLICADA',
  precioCop: 120000000,
  visible: true,
}
const credito = (id: number, extra: Record<string, unknown> = {}) =>
  lead(id, {
    origen: 'CREDITO',
    tipoVehiculo: null,
    estadoGestion: 'REGISTRADO',
    nombre: 'Ana Crédito',
    mensaje: 'Solicitud de crédito',
    vehiculo: null,
    responsable: ADMINISTRACION,
    ...extra,
  })
const CRITERIOS = {
  tipoId: 2,
  tipo: 'Camión',
  marcaId: 11,
  marca: 'Kenworth',
  lineaId: 110,
  linea: 'T800',
  anioDesde: 2015,
  anioHasta: 2020,
  presupuestoMaximo: 150000000,
  departamentoId: 5,
  departamento: 'Antioquia',
  municipioId: 50,
  municipio: 'Medellín',
}
const relacionado = (publicacionId: number, titulo: string, extra: Record<string, unknown> = {}) => ({
  publicacionId,
  vehiculoId: publicacionId - 20,
  titulo,
  slug: titulo.toLowerCase().replace(/ /g, '-'),
  precioCop: 120000000,
  estadoVehiculo: 'ACTIVO',
  estadoPublicacion: 'PUBLICADA',
  visible: true,
  nota: 'Coincide en año',
  relacionadoEn: '2026-11-09T15:00:00Z',
  relacionadoPor: 'Persona Ficticia',
  ...extra,
})
const busqueda = (id: number, extra: Record<string, unknown> = {}) =>
  lead(id, {
    origen: 'BUSQUEDA',
    tipoVehiculo: null,
    estadoGestion: 'EN_GESTION',
    nombre: 'Bruno Busca',
    mensaje: 'Doble troque con platón',
    vehiculo: null,
    responsable: ADMINISTRACION,
    ...extra,
  })
const detalleBusqueda = (id: number, extra: Record<string, unknown> = {}) =>
  detalleLead(id, {
    ...busqueda(id),
    busqueda: CRITERIOS,
    vehiculosRelacionados: [],
    consentimientos: [],
    ...extra,
  })

describe('Bandeja del administrador (CA-P6-40, 44)', () => {
  it('muestra "Crédito" y "Búsqueda" con su estado Registrado / En gestión / Cerrado', async () => {
    abrirApp('#/contactos', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      [rutaLeads()]: paginaLeads([
        credito(1),
        busqueda(2),
        credito(3, { nombre: 'Carla Cerrada', estado: 'CERRADO', estadoGestion: 'CERRADO', resultadoCierre: 'REMITIDO_ENTIDAD' }),
        lead(4, { origen: 'CONTACTO_VEHICULO' }),
      ]),
    })
    await screen.findByText('Ana Crédito')
    const items = screen.getAllByRole('listitem')
    const de = (t: string) => items.find((li) => li.textContent?.includes(t)) as HTMLElement
    expect(within(de('Ana Crédito')).getByText('Crédito')).toBeInTheDocument()
    expect(within(de('Ana Crédito')).getByText('Registrado')).toBeInTheDocument()
    expect(within(de('Ana Crédito')).getByText('Sin vehículo asociado')).toBeInTheDocument()
    expect(within(de('Bruno Busca')).getByText('Búsqueda')).toBeInTheDocument()
    expect(within(de('Bruno Busca')).getByText('En gestión')).toBeInTheDocument()
    expect(within(de('Carla Cerrada')).getByText('Cerrado')).toBeInTheDocument()
    expect(within(de('Carla Cerrada')).getByText('Remitido a entidad financiera')).toBeInTheDocument()
    // Los contactos de P5 conservan su estado.
    expect(within(de('Visitante 4')).getByText('Pendiente de gestión')).toBeInTheDocument()
  })

  it('filtra por origen CREDITO', async () => {
    const { api, user } = abrirApp('#/contactos', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      [rutaLeads()]: paginaLeads([lead(1)]),
      [rutaLeads('origen=CREDITO')]: paginaLeads([credito(2)]),
    })
    await screen.findByText('Visitante 1')
    await user.selectOptions(screen.getByLabelText('Origen'), 'CREDITO')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(await screen.findByText('Ana Crédito')).toBeInTheDocument()
    expect(api.llamadasA(rutaLeads('origen=CREDITO'))).toHaveLength(1)
  })
})

describe('Detalle de un crédito (CA-P6-39, 41, 43, 45, 49)', () => {
  it('con vehículo: precio, estado, enlace a la ficha visible, comentario, sin "Reasignar" ni datos financieros', async () => {
    abrirApp('#/contactos/5', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/5': {
        status: 200,
        body: detalleLead(5, {
          ...credito(5, { vehiculo: VEHICULO_CREDITO, mensaje: 'Quiero financiar este camión' }),
          consentimientos: [{ formulario: 'CREDITO', versionPolitica: 'BORRADOR-2026-11-04', aceptadoEn: '2026-11-09T15:00:00Z' }],
        }),
      },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Ana Crédito' })
    expect(screen.getByText('Origen').nextElementSibling).toHaveTextContent('Crédito')
    expect(screen.getByText('Registrado')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 3, name: 'Comentario' })).toBeInTheDocument()
    expect(screen.getByText('Quiero financiar este camión')).toBeInTheDocument()
    expect(screen.getByText('Precio publicado').nextElementSibling).toHaveTextContent('$ 120.000.000')
    expect(screen.getByRole('link', { name: 'Ver en el portal' })).toHaveAttribute('href', '#/vehiculo/chevrolet-npr-2020')
    expect(screen.getByText(/Solicitud de crédito · versión BORRADOR-2026-11-04/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reasignar' })).not.toBeInTheDocument()
    expect(screen.getByText(/no se reasigna a un asesor/)).toBeInTheDocument()
    expect(screen.queryByText(/monto|cuota inicial|ingresos mensuales/i)).not.toBeInTheDocument()
    expect(screen.getByLabelText('Nueva nota')).toHaveAccessibleDescription(
      /No escribas datos financieros del cliente \(ingresos, deudas, reportes en centrales\)\./,
    )
  })

  it('un vehículo que ya no se ve en el portal no se enlaza', async () => {
    abrirApp('#/contactos/5', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/5': {
        status: 200,
        body: detalleLead(5, credito(5, { vehiculo: { ...VEHICULO_CREDITO, estadoVehiculo: 'VENDIDO', visible: false } })),
      },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Ana Crédito' })
    expect(screen.getByText('Ya no se ve en el portal')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Ver en el portal' })).not.toBeInTheDocument()
  })

  it('el cierre ofrece los resultados del crédito y envía REMITIDO_ENTIDAD', async () => {
    const { api, user } = abrirApp('#/contactos/5', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/5': { status: 200, body: detalleLead(5, credito(5)) },
      '/api/leads/5/cerrar': {
        status: 200,
        body: detalleLead(5, credito(5, { estado: 'CERRADO', estadoGestion: 'CERRADO', resultadoCierre: 'REMITIDO_ENTIDAD' })),
      },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Ana Crédito' })
    expect(screen.getByText('Sin vehículo asociado')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Cerrar contacto' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Cerrar contacto' })
    expect(within(within(dialogo).getByLabelText('Resultado')).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Elige el resultado',
      'Remitido a entidad financiera',
      'Solicitud atendida',
      'No le interesó',
      'No respondió',
      'Contacto duplicado o inválido',
      'Otro',
    ])
    await user.selectOptions(within(dialogo).getByLabelText('Resultado'), 'REMITIDO_ENTIDAD')
    await user.click(within(dialogo).getByRole('button', { name: 'Cerrar contacto' }))
    expect(await screen.findByText('Contacto cerrado: Remitido a entidad financiera.')).toBeInTheDocument()
    expect(cuerpo(api.llamadas, '/api/leads/5/cerrar')).toEqual({ resultado: 'REMITIDO_ENTIDAD', observacion: null })
    expect(screen.getByText('Cerrado')).toBeInTheDocument()
  })

  it('resultados por origen según la tabla del contrato P6', () => {
    const valores = (o: Parameters<typeof resultadosCierrePara>[0]) => resultadosCierrePara(o).map((r) => r.valor)
    expect(valores('CREDITO')).toEqual(['REMITIDO_ENTIDAD', 'ATENDIDA', 'NO_INTERESADO', 'NO_RESPONDIO', 'DUPLICADO_INVALIDO', 'OTRO'])
    expect(valores('BUSQUEDA')).toEqual([
      'VEHICULO_ENCONTRADO',
      'SIN_VEHICULO',
      'NO_INTERESADO',
      'NO_RESPONDIO',
      'DUPLICADO_INVALIDO',
      'OTRO',
    ])
    expect(valores('CONTACTO_VEHICULO')).not.toContain('REMITIDO_ENTIDAD')
    expect(valores('VENDE_TU_CAMION')).not.toContain('VEHICULO_ENCONTRADO')
  })
})

describe('Detalle de una búsqueda (CA-P6-42, 46, 50 a 54, 56)', () => {
  it('muestra los criterios legibles, la descripción y los relacionados con su estado; sin "Reasignar"', async () => {
    abrirApp('#/contactos/7', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/7': {
        status: 200,
        body: detalleBusqueda(7, {
          vehiculosRelacionados: [
            relacionado(60, 'Chevrolet NPR 2020'),
            relacionado(62, 'Renault Kerax 2015', { estadoVehiculo: 'VENDIDO', estadoPublicacion: 'VENDIDA', visible: false, nota: null }),
          ],
        }),
      },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Bruno Busca' })
    const criterios = screen.getByRole('region', { name: 'Qué busca' })
    expect(within(criterios).getByText('Categoría').nextElementSibling).toHaveTextContent('Camión')
    expect(within(criterios).getByText('Marca').nextElementSibling).toHaveTextContent('Kenworth')
    expect(within(criterios).getByText('Modelo').nextElementSibling).toHaveTextContent('T800')
    expect(within(criterios).getByText('Años').nextElementSibling).toHaveTextContent('2015 a 2020')
    expect(within(criterios).getByText('Presupuesto máximo').nextElementSibling).toHaveTextContent('$ 150.000.000')
    expect(within(criterios).getByText('Ubicación').nextElementSibling).toHaveTextContent('Medellín, Antioquia')
    expect(screen.getByRole('heading', { level: 3, name: 'Descripción de lo que busca' })).toBeInTheDocument()
    expect(screen.getByText('En gestión')).toBeInTheDocument()
    const relacionados = screen.getByRole('region', { name: /Vehículos relacionados/ })
    expect(within(relacionados).getByText('(2 de 10)')).toBeInTheDocument()
    expect(within(relacionados).getByText('Nota interna: Coincide en año')).toBeInTheDocument()
    expect(within(relacionados).getByText(/Vendido · Vendida · Ya no se ve en el portal/)).toBeInTheDocument()
    expect(within(relacionados).getAllByRole('link', { name: /Ver en el portal/ })).toHaveLength(1)
    expect(screen.queryByRole('button', { name: 'Reasignar' })).not.toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Vehículo' })).not.toBeInTheDocument()
  })

  it('"Ver coincidencias" y relacionar con nota interna: POST del contrato y mensaje', async () => {
    const { api, user } = abrirApp('#/contactos/7', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/7': { status: 200, body: detalleBusqueda(7) },
      '/api/leads/7/coincidencias': {
        status: 200,
        body: {
          items: [
            { publicacionId: 61, relacionado: false, vehiculo: V2 },
            { publicacionId: 60, relacionado: true, vehiculo: V1 },
          ],
          total: 2,
        },
      },
      '/api/leads/7/vehiculos': {
        status: 201,
        body: detalleBusqueda(7, { vehiculosRelacionados: [relacionado(61, 'Kenworth T800 2018')] }),
      },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Bruno Busca' })
    expect(screen.getByText('Aún no hay vehículos relacionados.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Ver coincidencias' }))
    expect(await screen.findByText('2 coincidencias.')).toBeInTheDocument()
    expect(screen.getByText('Ya relacionado')).toBeInTheDocument()
    const boton = screen.getByRole('button', { name: 'Relacionar Kenworth T800 2018' })
    await user.click(boton)
    const dialogo = await screen.findByRole('dialog', { name: 'Relacionar vehículo' })
    const nota = within(dialogo).getByLabelText('Nota interna (opcional)')
    await waitFor(() => expect(nota).toHaveFocus())
    expect(nota).toHaveAttribute('maxLength', '300')
    await user.type(nota, ' Coincide en precio ')
    await user.click(within(dialogo).getByRole('button', { name: 'Relacionar' }))
    expect(await screen.findByText('Vehículo relacionado con la búsqueda: Kenworth T800 2018.')).toBeInTheDocument()
    expect(cuerpo(api.llamadas, '/api/leads/7/vehiculos')).toEqual({ publicacionId: 61, nota: 'Coincide en precio' })
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getAllByText('Ya relacionado')).toHaveLength(2)
    expect(within(screen.getByRole('region', { name: /Vehículos relacionados/ })).getByText('(1 de 10)')).toBeInTheDocument()
  })

  it('Escape cancela el diálogo y devuelve el foco al botón; sin nota viaja null', async () => {
    const { api, user } = abrirApp('#/contactos/7', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/7': { status: 200, body: detalleBusqueda(7) },
      '/api/leads/7/coincidencias': {
        status: 200,
        body: { items: [{ publicacionId: 61, relacionado: false, vehiculo: V2 }], total: 25 },
      },
      '/api/leads/7/vehiculos': { status: 201, body: detalleBusqueda(7) },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Bruno Busca' })
    await user.click(screen.getByRole('button', { name: 'Ver coincidencias' }))
    expect(await screen.findByText('25 coincidencias; se muestran las 1 primeras.')).toBeInTheDocument()
    const boton = screen.getByRole('button', { name: 'Relacionar Kenworth T800 2018' })
    await user.click(boton)
    await screen.findByRole('dialog', { name: 'Relacionar vehículo' })
    await user.keyboard('{Escape}')
    await waitFor(() => expect(boton).toHaveFocus())
    await user.click(boton)
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Relacionar' }))
    await screen.findByText(/Vehículo relacionado con la búsqueda/)
    expect(cuerpo(api.llamadas, '/api/leads/7/vehiculos')).toEqual({ publicacionId: 61, nota: null })
  })

  it('un 409 al relacionar se anuncia, cierra el diálogo y vuelve a leer la búsqueda', async () => {
    const MENSAJE_TOPE = 'Esta búsqueda ya tiene 10 vehículos relacionados.'
    const { api, user } = abrirApp('#/contactos/7', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/7': { status: 200, body: detalleBusqueda(7) },
      '/api/leads/7/coincidencias': {
        status: 200,
        body: { items: [{ publicacionId: 61, relacionado: false, vehiculo: V2 }], total: 1 },
      },
      '/api/leads/7/vehiculos': { status: 409, body: { error: MENSAJE_TOPE } },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Bruno Busca' })
    await user.click(screen.getByRole('button', { name: 'Ver coincidencias' }))
    await user.click(await screen.findByRole('button', { name: 'Relacionar Kenworth T800 2018' }))
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Relacionar' }))
    expect(await screen.findByText(MENSAJE_TOPE)).toHaveAttribute('role', 'alert')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await waitFor(() => expect(api.llamadasA('/api/leads/7')).toHaveLength(2))
  })

  it('un 400 en la nota se muestra dentro del diálogo sin cerrarlo', async () => {
    const { user } = abrirApp('#/contactos/7', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/7': { status: 200, body: detalleBusqueda(7) },
      '/api/leads/7/coincidencias': {
        status: 200,
        body: { items: [{ publicacionId: 61, relacionado: false, vehiculo: V2 }], total: 1 },
      },
      '/api/leads/7/vehiculos': {
        status: 400,
        body: { error: 'Revisa los datos marcados.', errores: { nota: ['La nota no puede pasar de 300 caracteres.'] } },
      },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Bruno Busca' })
    await user.click(screen.getByRole('button', { name: 'Ver coincidencias' }))
    await user.click(await screen.findByRole('button', { name: 'Relacionar Kenworth T800 2018' }))
    const dialogo = await screen.findByRole('dialog')
    await user.type(within(dialogo).getByLabelText('Nota interna (opcional)'), 'Nota')
    await user.click(within(dialogo).getByRole('button', { name: 'Relacionar' }))
    await waitFor(() =>
      expect(within(dialogo).getByLabelText('Nota interna (opcional)')).toHaveAccessibleDescription(
        /La nota no puede pasar de 300 caracteres\./,
      ),
    )
    expect(screen.getByRole('dialog', { name: 'Relacionar vehículo' })).toBeInTheDocument()
  })

  it('retirar: confirma, envía la ruta sin cuerpo y anuncia el cambio', async () => {
    const { api, user } = abrirApp('#/contactos/7', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/7': { status: 200, body: detalleBusqueda(7, { vehiculosRelacionados: [relacionado(60, 'Chevrolet NPR 2020')] }) },
      '/api/leads/7/vehiculos/60/retirar': { status: 200, body: detalleBusqueda(7) },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Bruno Busca' })
    await user.click(screen.getByRole('button', { name: 'Retirar Chevrolet NPR 2020' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Retirar vehículo' })
    await user.click(within(dialogo).getByRole('button', { name: 'Retirar' }))
    expect(await screen.findByText('Vehículo retirado de la búsqueda: Chevrolet NPR 2020.')).toBeInTheDocument()
    const llamada = api.llamadasA('/api/leads/7/vehiculos/60/retirar')
    expect(llamada).toHaveLength(1)
    expect(llamada[0].init?.method).toBe('POST')
    expect(llamada[0].init?.body).toBeUndefined()
    expect(screen.getByText('Aún no hay vehículos relacionados.')).toBeInTheDocument()
  })

  it('cerrada: se consultan coincidencias pero no se relaciona ni se retira; resultados de la búsqueda al cerrar', async () => {
    const { user } = abrirApp('#/contactos/7', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/7': {
        status: 200,
        body: detalleBusqueda(7, {
          estado: 'CERRADO',
          estadoGestion: 'CERRADO',
          resultadoCierre: 'VEHICULO_ENCONTRADO',
          vehiculosRelacionados: [relacionado(60, 'Chevrolet NPR 2020')],
        }),
      },
      '/api/leads/7/coincidencias': {
        status: 200,
        body: { items: [{ publicacionId: 61, relacionado: false, vehiculo: V2 }], total: 1 },
      },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Bruno Busca' })
    expect(screen.getByText('Resultado del cierre').nextElementSibling).toHaveTextContent('Encontró vehículo')
    expect(screen.queryByRole('button', { name: /^Retirar/ })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Ver coincidencias' }))
    expect(await screen.findByText('La búsqueda está cerrada: solo puedes consultar.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^Relacionar/ })).not.toBeInTheDocument()
  })

  it('el cierre de una búsqueda ofrece "Encontró vehículo" y "Sin vehículo disponible"', async () => {
    const { user } = abrirApp('#/contactos/7', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/7': { status: 200, body: detalleBusqueda(7) },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Bruno Busca' })
    await user.click(screen.getByRole('button', { name: 'Cerrar contacto' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Cerrar contacto' })
    const opciones = within(within(dialogo).getByLabelText('Resultado')).getAllByRole('option').map((o) => o.textContent)
    expect(opciones).toContain('Encontró vehículo')
    expect(opciones).toContain('Sin vehículo disponible')
    expect(opciones).not.toContain('Se concretó la venta')
    expect(opciones).not.toContain('Remitido a entidad financiera')
  })

  it('con 10 relacionados el botón "Relacionar" queda desactivado', async () => {
    const diez = Array.from({ length: 10 }, (_, i) => relacionado(100 + i, `Vehículo ${i}`))
    const { user } = abrirApp('#/contactos/7', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/7': { status: 200, body: detalleBusqueda(7, { vehiculosRelacionados: diez }) },
      '/api/leads/7/coincidencias': {
        status: 200,
        body: { items: [{ publicacionId: 61, relacionado: false, vehiculo: V2 }], total: 1 },
      },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Bruno Busca' })
    await user.click(screen.getByRole('button', { name: 'Ver coincidencias' }))
    expect(await screen.findByRole('button', { name: 'Relacionar Kenworth T800 2018' })).toBeDisabled()
    expect(screen.getByText(/Ya hay 10 vehículos relacionados/)).toBeInTheDocument()
  })
})

// ---- Permisos y "Mis solicitudes" ----

const RUTA_MIS = '/api/mis-solicitudes?pagina=1&tamano=20'
const SOLICITUDES = [
  {
    id: 1,
    creadoEn: '2026-11-09T15:00:00Z',
    tipo: 'BUSQUEDA',
    resumen: 'Camión Kenworth T800',
    estado: 'EN_GESTION',
    vehiculo: null,
    recomendados: [V2],
  },
  {
    id: 2,
    creadoEn: '2026-11-08T15:00:00Z',
    tipo: 'CREDITO',
    resumen: 'Chevrolet NPR 2020',
    estado: 'REGISTRADA',
    vehiculo: { titulo: 'Chevrolet NPR 2020', slug: 'chevrolet-npr-2020', disponible: true },
    recomendados: [],
  },
  {
    id: 3,
    creadoEn: '2026-11-07T15:00:00Z',
    tipo: 'CONTACTO_VEHICULO',
    resumen: 'Renault Kerax 2015',
    estado: 'FINALIZADA',
    vehiculo: { titulo: 'Renault Kerax 2015', slug: null, disponible: false },
    recomendados: [],
  },
  {
    id: 4,
    creadoEn: '2026-11-06T15:00:00Z',
    tipo: 'SEGURO_TODO_RIESGO',
    resumen: 'Camión',
    estado: 'PENDIENTE',
    vehiculo: null,
    recomendados: [],
  },
  {
    id: 5,
    creadoEn: '2026-11-05T15:00:00Z',
    tipo: 'VENDE_TU_CAMION',
    resumen: 'Venta de tu vehículo',
    estado: 'FINALIZADA',
    vehiculo: null,
    recomendados: [],
  },
]

describe('"Mis solicitudes" del comprador (CA-P6-66, 68 a 72, 75, 76)', () => {
  it('el perfil del comprador ofrece "Mis solicitudes"', async () => {
    abrirApp('#/cuenta', ['COMPRADOR'])
    expect(await screen.findByRole('link', { name: 'Mis solicitudes' })).toHaveAttribute('href', '#/mis-solicitudes')
  })

  it('lista con fecha, tipo, resumen y estado; enlace solo si el vehículo sigue disponible; recomendados en la búsqueda', async () => {
    const { api } = abrirApp('#/mis-solicitudes', ['COMPRADOR'], {
      [RUTA_MIS]: { status: 200, body: { items: SOLICITUDES, total: 5, pagina: 1, tamano: 20 } },
    })
    await screen.findByText('Camión Kenworth T800')
    expect(screen.getByRole('heading', { level: 1, name: 'Mis solicitudes' })).toBeInTheDocument()
    expect(document.title).toBe('Mis solicitudes | Financiamos Tu Camión')
    expect(cabecera(api.llamadasA(RUTA_MIS)[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
    expect(screen.getByText('(5 solicitudes)')).toBeInTheDocument()
    const tarjeta = (nombre: string) => screen.getByRole('article', { name: nombre })
    const busqueda = tarjeta('Búsqueda')
    expect(within(busqueda).getByText('En gestión')).toBeInTheDocument()
    expect(within(busqueda).getByText('Fecha')).toBeInTheDocument()
    expect(within(busqueda).getByRole('heading', { name: 'Te recomendamos estos vehículos' })).toBeInTheDocument()
    expect(within(busqueda).getByRole('link', { name: 'Kenworth T800 2018' })).toHaveAttribute(
      'href',
      '#/vehiculo/kenworth-t800-2018',
    )
    const credito = tarjeta('Crédito')
    expect(within(credito).getByText('Registrada')).toBeInTheDocument()
    expect(within(credito).getByRole('link', { name: 'Chevrolet NPR 2020' })).toHaveAttribute('href', '#/vehiculo/chevrolet-npr-2020')
    const contacto = tarjeta('Contacto por vehículo')
    expect(within(contacto).getByText('Finalizada')).toBeInTheDocument()
    expect(within(contacto).getByText('Este vehículo ya no está disponible')).toBeInTheDocument()
    expect(within(contacto).queryByRole('link')).not.toBeInTheDocument()
    expect(within(tarjeta('Seguro todo riesgo')).getByText('Pendiente')).toBeInTheDocument()
    expect(within(tarjeta('Vende tu camión')).getByText('Venta de tu vehículo')).toBeInTheDocument()
    // Solo lectura y sin datos internos (CA-P6-68, 69).
    expect(screen.queryByRole('button', { name: /Editar|Cancelar|Borrar|Eliminar/ })).not.toBeInTheDocument()
    expect(screen.queryByText(/Responsable|Administración|Nota|Resultado/)).not.toBeInTheDocument()
  })

  it('sin solicitudes muestra el texto y los accesos', async () => {
    abrirApp('#/mis-solicitudes', ['COMPRADOR'], {
      [RUTA_MIS]: { status: 200, body: { items: [], total: 0, pagina: 1, tamano: 20 } },
    })
    expect(await screen.findByText('Aún no tienes solicitudes.')).toBeInTheDocument()
    const accesos = screen.getByRole('list', { name: 'Empieza aquí' })
    expect(within(accesos).getAllByRole('link').map((a) => a.getAttribute('href'))).toEqual([
      '#/buscar',
      '#/credito',
      '#/busco-un-vehiculo',
      '#/vende-tu-camion',
      '#/seguro-todo-riesgo',
    ])
  })

  it('pagina de 20 en 20', async () => {
    const { api, user } = abrirApp('#/mis-solicitudes', ['COMPRADOR'], {
      [RUTA_MIS]: { status: 200, body: { items: SOLICITUDES, total: 25, pagina: 1, tamano: 20 } },
      '/api/mis-solicitudes?pagina=2&tamano=20': {
        status: 200,
        body: { items: [SOLICITUDES[3]], total: 25, pagina: 2, tamano: 20 },
      },
    })
    await screen.findByText('Página 1 de 2')
    await user.click(screen.getByRole('button', { name: 'Siguiente' }))
    expect(await screen.findByText('Página 2 de 2')).toBeInTheDocument()
    expect(api.llamadasA('/api/mis-solicitudes?pagina=2&tamano=20')).toHaveLength(1)
  })

  it('un error ofrece reintentar', async () => {
    const { user } = abrirApp('#/mis-solicitudes', ['COMPRADOR'], {
      [RUTA_MIS]: [{ red: true }, { status: 200, body: { items: [SOLICITUDES[1]], total: 1, pagina: 1, tamano: 20 } }],
    })
    await user.click(await screen.findByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByText('(1 solicitud)')).toBeInTheDocument()
  })

  it('un 401 termina la sesión y lleva al ingreso', async () => {
    abrirApp('#/mis-solicitudes', ['COMPRADOR'], { [RUTA_MIS]: { status: 401 } })
    await waitFor(() => expect(window.location.hash).toBe('#/cuenta'))
    expect(await screen.findByText(/Tu sesión terminó/)).toBeInTheDocument()
  })
})

describe('Permisos (CA-P6-60, 63, 74)', () => {
  it('sin sesión, #/mis-solicitudes lleva al ingreso sin llamar a la API', async () => {
    const { api } = abrirAnonimo('#/mis-solicitudes')
    await waitFor(() => expect(window.location.hash).toBe('#/cuenta'))
    expect(api.llamadas.some((l) => l.url.startsWith('/api/mis-solicitudes'))).toBe(false)
  })

  it.each([['ASESOR'], ['ADMINISTRADOR']])('%s: no ve el acceso ni llama a la API', async (rol) => {
    const { api } = abrirApp('#/mis-solicitudes', [rol], {
      [RUTA_CATALOGOS_INV]: { status: 200, body: CATALOGOS_INVENTARIO },
    })
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
    expect(api.llamadas.some((l) => l.url.startsWith('/api/mis-solicitudes'))).toBe(false)
  })

  it('el perfil del asesor no ofrece "Mis solicitudes"', async () => {
    abrirApp('#/cuenta', ['ASESOR'], {
      '/api/asesor/suscripcion': { status: 404, body: { error: 'Sin suscripción.' } },
      '/api/leads/resumen': { status: 200, body: { nuevos: 0, abiertos: 0 } },
    })
    await screen.findByRole('link', { name: /Mi inventario/ })
    expect(screen.queryByRole('link', { name: 'Mis solicitudes' })).not.toBeInTheDocument()
  })

  it('el asesor no ve los orígenes nuevos ni el filtro por origen', async () => {
    const { api } = abrirApp('#/contactos', ['ASESOR'], { [rutaLeads()]: paginaLeads([lead(1)]) })
    await screen.findByText('Visitante 1')
    expect(screen.queryByLabelText('Origen')).not.toBeInTheDocument()
    expect(screen.queryByText(/Crédito|Búsqueda/)).not.toBeInTheDocument()
    expect(api.llamadas.filter((l) => l.url.startsWith('/api/leads')).every((l) => !l.url.includes('origen'))).toBe(true)
  })

  it('un comprador en #/contactos ve "No tienes permiso" sin llamar a /api/leads', async () => {
    const { api } = abrirApp('#/contactos', ['COMPRADOR'])
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
    expect(api.llamadas.some((l) => l.url.startsWith('/api/leads'))).toBe(false)
  })

  it('un asesor que abre por id una búsqueda recibe 404 "Contacto no encontrado."', async () => {
    abrirApp('#/contactos/7', ['ASESOR'], {
      '/api/leads/7': { status: 404, body: { error: 'Contacto no encontrado.' } },
    })
    expect(await screen.findByText('Contacto no encontrado.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ver coincidencias' })).not.toBeInTheDocument()
  })
})

