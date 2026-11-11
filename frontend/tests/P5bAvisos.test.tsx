// P5b — Avisos públicos "Vende tu camión" (HU-N01) y "Compra tu seguro todo riesgo" (HU-N02), sus accesos en el
// portal (D-51 A) y su gestión en la bandeja del administrador (D-52 A). Contrato: docs/criterios/P5b-contrato-api.md.
// CA-P5b-01 a 04, 06, 07, 08, 09, 11, 12, 15, 16, 18, 19, 21, 28 a 31, 37, 38, 41, 43, 46, 53 a 58.
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import App from '../src/App'
import { resultadosCierrePara } from '../src/api/leads'
import { leerRuta } from '../src/rutas'
import { cabecera, simularFetch, TOKEN_FICTICIO, type RespuestaSimulada } from './helpers'
import { abrirApp, CATALOGOS_INVENTARIO, RUTA_CATALOGOS_INV, type Rutas } from './p4pDatos'
import { cuerpo, detalleLead, lead, paginaLeads, retenerRuta, rutaLeads } from './p5Datos'

const RUTA_VENDE = '/api/avisos/vende-tu-camion'
const RUTA_SEGURO = '/api/avisos/seguro-todo-riesgo'
const RUTA_INICIO = '/api/catalogo/inicio'
const MENSAJE_OK = '¡Listo! Recibimos tu solicitud. Te contactaremos pronto por teléfono o WhatsApp.'
const MENSAJE_FALLO =
  'No pudimos enviar tus datos. Intenta de nuevo en unos minutos o escríbenos por WhatsApp al 316 615 2907.'
const MENSAJE_429 = 'Has enviado muchas solicitudes. Intenta de nuevo en unos minutos.'
const OK: RespuestaSimulada = { status: 201, body: { mensaje: MENSAJE_OK } }

/** Abre un aviso sin sesión. */
function abrirAnonimo(hash: string, rutas: Record<string, RespuestaSimulada | RespuestaSimulada[]> = {}) {
  const api = simularFetch(rutas)
  window.location.hash = hash
  render(<App />)
  return { api, user: userEvent.setup() }
}

const formVende = () => screen.findByRole('form', { name: 'Formulario Vende tu camión' })
const formSeguro = () => screen.findByRole('form', { name: 'Formulario Compra tu seguro todo riesgo' })
const nombre = () => screen.getByLabelText('Nombre')
const telefonoVende = () => screen.getByLabelText('Teléfono WhatsApp')
const telefonoSeguro = () => screen.getByLabelText('Teléfono')
const correo = () => screen.getByLabelText('Correo (opcional)')
const descripcion = () => screen.getByLabelText('Descripción del vehículo')
const tipo = () => screen.getByLabelText('Tipo de vehículo')
const casilla = () => screen.getByRole('checkbox', { name: /Autorizo el tratamiento de mis datos personales/ })
const enviar = () => screen.getByRole('button', { name: /^(Enviar|Enviando\.\.\.)$/ })

describe('"Vende tu camión": estructura (CA-P5b-08, 18, 31, 53, 55, 57, 58)', () => {
  it('sin sesión muestra h1, solo nombre, teléfono, correo opcional, descripción y la casilla sin marcar', async () => {
    abrirAnonimo('#/vende-tu-camion')
    const form = await formVende()
    expect(screen.getByRole('heading', { level: 1, name: 'Vende tu camión' })).toBeInTheDocument()
    expect(document.title).toBe('Vende tu camión | Financiamos Tu Camión')
    expect(within(form).getByText('Campo obligatorio')).toBeInTheDocument()
    expect(nombre()).toHaveAttribute('aria-required', 'true')
    expect(nombre()).toHaveAttribute('autocomplete', 'name')
    expect(telefonoVende()).toHaveAttribute('aria-required', 'true')
    expect(telefonoVende()).toHaveAttribute('inputmode', 'tel')
    expect(telefonoVende()).toHaveAttribute('autocomplete', 'tel')
    expect(correo()).not.toHaveAttribute('aria-required')
    expect(descripcion()).toHaveAttribute('aria-required', 'true')
    expect(descripcion()).not.toHaveAttribute('required')
    expect(casilla()).not.toBeChecked()
    expect(casilla()).toHaveAttribute('aria-required', 'true')
    expect(form.querySelector('label[for="vende-descripcion"]')).toHaveClass('campo-obligatorio')
    // D-50 (b): sin ciudad, marca ni año; sin tipo de vehículo.
    expect(screen.queryByLabelText(/Ciudad|Marca|Año|Tipo de vehículo/)).not.toBeInTheDocument()
    const politica = within(form).getByRole('link', { name: /Política de tratamiento de datos/ })
    expect(politica).toHaveAttribute('href', '#/privacidad')
    expect(politica).toHaveAttribute('target', '_blank')
    expect(politica).toHaveTextContent('(se abre en una pestaña nueva)')
  })

  it('el campo trampa está fuera del orden de tabulación y oculto a lectores de pantalla', async () => {
    abrirAnonimo('#/vende-tu-camion')
    const form = await formVende()
    const trampa = form.querySelector<HTMLInputElement>('input[name="sitioWeb"]')
    expect(trampa).toHaveAttribute('tabindex', '-1')
    expect(trampa?.closest('[aria-hidden="true"]')).not.toBeNull()
  })

  it('CA-P5b-07 contador "N/500" en aria-live, tope de 500 y 500 caracteres exactos se aceptan', async () => {
    const { api, user } = abrirAnonimo('#/vende-tu-camion', { [RUTA_VENDE]: OK })
    await formVende()
    expect(descripcion()).toHaveAttribute('maxLength', '500')
    const contador = document.getElementById('vende-descripcion-contador')
    expect(contador).toHaveAttribute('aria-live', 'polite')
    expect(contador).toHaveTextContent('0/500')
    await user.type(descripcion(), 'Kenworth')
    expect(contador).toHaveTextContent('8/500')
    const quinientos = 'a'.repeat(500)
    fireEvent.change(descripcion(), { target: { value: quinientos } })
    expect(contador).toHaveTextContent('500/500')
    await user.type(nombre(), 'Ana')
    await user.type(telefonoVende(), '3001234567')
    await user.click(casilla())
    await user.click(enviar())
    expect(await screen.findByText(MENSAJE_OK)).toBeInTheDocument()
    expect((cuerpo(api.llamadas, RUTA_VENDE) as { descripcion: string }).descripcion).toHaveLength(500)
  })
})

describe('"Vende tu camión": envío (CA-P5b-01, 02, 28, 29, 30, 56)', () => {
  it('anónimo: POST sin token con el cuerpo del contrato, éxito en role="status" con foco y formulario vacío', async () => {
    const { api, user } = abrirAnonimo('#/vende-tu-camion', { [RUTA_VENDE]: OK })
    await formVende()
    await user.type(nombre(), '  Ana Pérez ')
    await user.type(telefonoVende(), '300 123 4567')
    await user.type(descripcion(), ' Kenworth T800 2015, 450.000 km, en Medellín ')
    await user.click(casilla())
    await user.click(enviar())

    const exito = await screen.findByText(MENSAJE_OK)
    expect(exito).toHaveAttribute('role', 'status')
    await waitFor(() => expect(exito.parentElement).toHaveFocus())
    const llamadas = api.llamadasA(RUTA_VENDE)
    expect(llamadas).toHaveLength(1)
    expect(llamadas[0].init?.method).toBe('POST')
    expect(cabecera(llamadas[0].init, 'Authorization')).toBeUndefined()
    expect(cuerpo(api.llamadas, RUTA_VENDE)).toEqual({
      nombre: 'Ana Pérez',
      telefono: '300 123 4567',
      correo: null,
      descripcion: 'Kenworth T800 2015, 450.000 km, en Medellín',
      autorizacionDatos: true,
      sitioWeb: '',
    })
    await user.click(screen.getByRole('button', { name: 'Enviar otra solicitud' }))
    expect(nombre()).toHaveValue('')
    expect(telefonoVende()).toHaveValue('')
    expect(descripcion()).toHaveValue('')
    expect(casilla()).not.toBeChecked()
  })

  it('validación en el navegador: errores por campo con aria-invalid y aria-describedby, foco al primero, sin llamar a la API', async () => {
    const { api, user } = abrirAnonimo('#/vende-tu-camion')
    await formVende()
    await user.type(descripcion(), '     ')
    await user.click(enviar())
    expect(nombre()).toHaveAttribute('aria-invalid', 'true')
    expect(nombre()).toHaveAccessibleDescription('Escribe tu nombre (entre 2 y 150 caracteres).')
    expect(telefonoVende()).toHaveAccessibleDescription(
      /Escribe un celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567\./,
    )
    expect(descripcion()).toHaveAttribute('aria-invalid', 'true')
    expect(descripcion()).toHaveAccessibleDescription(/Describe tu vehículo \(máximo 500 caracteres\)\./)
    expect(casilla()).toHaveAccessibleDescription('Debes autorizar el tratamiento de tus datos para enviar el formulario.')
    expect(correo()).toHaveAttribute('aria-invalid', 'false')
    expect(screen.getByRole('alert')).toHaveTextContent('Revisa los datos marcados.')
    await waitFor(() => expect(nombre()).toHaveFocus())
    expect(api.llamadasA(RUTA_VENDE)).toHaveLength(0)
  })

  it('501 caracteres (pegados sin respetar el tope) se rechazan en el navegador', async () => {
    const { api, user } = abrirAnonimo('#/vende-tu-camion')
    await formVende()
    await user.type(nombre(), 'Ana')
    await user.type(telefonoVende(), '3001234567')
    fireEvent.change(descripcion(), { target: { value: 'b'.repeat(501) } })
    await user.click(casilla())
    await user.click(enviar())
    expect(descripcion()).toHaveAccessibleDescription(/La descripción no puede pasar de 500 caracteres\./)
    await waitFor(() => expect(descripcion()).toHaveFocus())
    expect(api.llamadasA(RUTA_VENDE)).toHaveLength(0)
  })

  it.each([['1234567'], ['+1 202 555 0123'], ['2001234567']])('CA-P5b-04 el teléfono %s se rechaza', async (valor) => {
    const { api, user } = abrirAnonimo('#/vende-tu-camion')
    await formVende()
    await user.type(nombre(), 'Ana')
    await user.type(telefonoVende(), valor)
    await user.type(descripcion(), 'Camión NHR')
    await user.click(casilla())
    await user.click(enviar())
    expect(telefonoVende()).toHaveAttribute('aria-invalid', 'true')
    await waitFor(() => expect(telefonoVende()).toHaveFocus())
    expect(api.llamadasA(RUTA_VENDE)).toHaveLength(0)
  })

  it('CA-P5b-09 correo mal escrito se marca; un correo válido viaja tal cual', async () => {
    const { api, user } = abrirAnonimo('#/vende-tu-camion', { [RUTA_VENDE]: OK })
    await formVende()
    await user.type(nombre(), 'Ana')
    await user.type(telefonoVende(), '6011234567')
    await user.type(correo(), 'ana@')
    await user.type(descripcion(), 'Camión NHR')
    await user.click(casilla())
    await user.click(enviar())
    expect(correo()).toHaveAccessibleDescription('Escribe un correo válido o deja el campo vacío.')
    await user.type(correo(), 'ejemplo.test')
    await user.click(enviar())
    expect(await screen.findByText(MENSAJE_OK)).toBeInTheDocument()
    expect((cuerpo(api.llamadas, RUTA_VENDE) as { correo: string }).correo).toBe('ana@ejemplo.test')
  })

  it('CA-P5b-29 un 400 del servidor marca cada campo, conserva lo escrito y enfoca el primero', async () => {
    const { user } = abrirAnonimo('#/vende-tu-camion', {
      [RUTA_VENDE]: {
        status: 400,
        body: {
          error: 'Revisa los datos marcados.',
          errores: { Descripcion: ['La descripción no puede pasar de 500 caracteres.'] },
        },
      },
    })
    await formVende()
    await user.type(nombre(), 'Ana')
    await user.type(telefonoVende(), '3001234567')
    await user.type(descripcion(), 'Camión NHR')
    await user.click(casilla())
    await user.click(enviar())
    await waitFor(() =>
      expect(descripcion()).toHaveAccessibleDescription(/La descripción no puede pasar de 500 caracteres\./),
    )
    await waitFor(() => expect(descripcion()).toHaveFocus())
    expect(nombre()).toHaveValue('Ana')
    expect(descripcion()).toHaveValue('Camión NHR')
    expect(casilla()).toBeChecked()
  })

  it.each<[string, RespuestaSimulada, string]>([
    ['429', { status: 429, body: { error: MENSAJE_429 } }, MENSAJE_429],
    ['413', { status: 413, body: { error: 'La solicitud es demasiado grande.' } }, 'La solicitud es demasiado grande.'],
    ['500', { status: 500, body: { error: 'No se pudo completar la operación. Intenta de nuevo en unos minutos.' } }, MENSAJE_FALLO],
    ['sin conexión', { red: true }, MENSAJE_FALLO],
  ])('CA-P5b-30 %s muestra un mensaje comprensible y conserva los datos', async (_n, respuesta, texto) => {
    const { user } = abrirAnonimo('#/vende-tu-camion', { [RUTA_VENDE]: respuesta })
    await formVende()
    await user.type(nombre(), 'Ana')
    await user.type(telefonoVende(), '3001234567')
    await user.type(descripcion(), 'Camión NHR')
    await user.click(casilla())
    await user.click(enviar())
    expect(await screen.findByText(texto)).toHaveAttribute('role', 'alert')
    expect(nombre()).toHaveValue('Ana')
    expect(descripcion()).toHaveValue('Camión NHR')
  })

  it('CA-P5b-31 no se envía dos veces: el botón se desactiva mientras se envía', async () => {
    const { api, user } = abrirAnonimo('#/vende-tu-camion', { [RUTA_VENDE]: OK })
    await formVende()
    await user.type(nombre(), 'Ana')
    await user.type(telefonoVende(), '3001234567')
    await user.type(descripcion(), 'Camión NHR')
    await user.click(casilla())
    const retenida = retenerRuta(RUTA_VENDE)
    const form = await formVende()
    fireEvent.submit(form)
    fireEvent.submit(form)
    expect(await screen.findByRole('button', { name: 'Enviando...' })).toBeDisabled()
    expect(form).toHaveAttribute('aria-busy', 'true')
    retenida.soltar()
    expect(await screen.findByText(MENSAJE_OK)).toBeInTheDocument()
    expect(api.llamadasA(RUTA_VENDE)).toHaveLength(1)
  })

  it('CA-P5b-02 / 21 con sesión de comprador: prellena el perfil, envía el token y exige marcar la casilla', async () => {
    const { api, user } = abrirApp('#/vende-tu-camion', ['COMPRADOR'], { [RUTA_VENDE]: OK })
    await waitFor(() => expect(screen.getByLabelText('Nombre')).toHaveValue('Persona Ficticia'))
    expect(telefonoVende()).toHaveValue('+57 300 000 0009')
    expect(correo()).toHaveValue('persona.ficticia@ejemplo.test')
    expect(casilla()).not.toBeChecked()
    await user.type(descripcion(), 'Chevrolet NPR 2018')
    await user.click(enviar())
    expect(casilla()).toHaveAttribute('aria-invalid', 'true')
    expect(api.llamadasA(RUTA_VENDE)).toHaveLength(0)
    await user.click(casilla())
    await user.click(enviar())
    expect(await screen.findByText(MENSAJE_OK)).toBeInTheDocument()
    expect(cabecera(api.llamadasA(RUTA_VENDE)[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
    // Tras el envío y "Enviar otra solicitud" vuelve a los datos del perfil y la casilla se desmarca.
    await user.click(screen.getByRole('button', { name: 'Enviar otra solicitud' }))
    expect(nombre()).toHaveValue('Persona Ficticia')
    expect(descripcion()).toHaveValue('')
    expect(casilla()).not.toBeChecked()
  })

  it('CA-P5b-53 un asesor con sesión también ve el formulario', async () => {
    abrirApp('#/vende-tu-camion', ['ASESOR'])
    expect(await formVende()).toBeInTheDocument()
  })
})

describe('"Compra tu seguro todo riesgo" (CA-P5b-11, 12, 15, 16, 55)', () => {
  it('el tipo de vehículo es un select nativo obligatorio con Camión, Carro, Maquinaria y Otro', async () => {
    abrirAnonimo('#/seguro-todo-riesgo')
    await formSeguro()
    expect(screen.getByRole('heading', { level: 1, name: 'Compra tu seguro todo riesgo' })).toBeInTheDocument()
    const select = tipo()
    expect(select.tagName).toBe('SELECT')
    expect(select).toHaveAttribute('aria-required', 'true')
    const opciones = within(select).getAllByRole('option').map((o) => [(o as HTMLOptionElement).value, o.textContent])
    expect(opciones).toEqual([
      ['', 'Elige el tipo de vehículo'],
      ['CAMION', 'Camión'],
      ['CARRO', 'Carro'],
      ['MAQUINARIA', 'Maquinaria'],
      ['OTRO', 'Otro'],
    ])
    expect(screen.queryByLabelText(/Descripción/)).not.toBeInTheDocument()
    expect(telefonoSeguro()).toHaveAttribute('inputmode', 'tel')
  })

  it('sin tipo de vehículo: "Elige el tipo de vehículo." y no se llama a la API', async () => {
    const { api, user } = abrirAnonimo('#/seguro-todo-riesgo')
    await formSeguro()
    await user.type(nombre(), 'Ana')
    await user.type(telefonoSeguro(), '3001234567')
    await user.click(casilla())
    await user.click(enviar())
    expect(tipo()).toHaveAttribute('aria-invalid', 'true')
    expect(tipo()).toHaveAccessibleDescription('Elige el tipo de vehículo.')
    await waitFor(() => expect(tipo()).toHaveFocus())
    expect(api.llamadasA(RUTA_SEGURO)).toHaveLength(0)
  })

  it('CA-P5b-16 con "Otro" se envía sin pedir más datos; cuerpo del contrato sin token', async () => {
    const { api, user } = abrirAnonimo('#/seguro-todo-riesgo', { [RUTA_SEGURO]: OK })
    await formSeguro()
    await user.type(nombre(), 'Ana Pérez')
    await user.type(telefonoSeguro(), '+57 300 123 4567')
    await user.selectOptions(tipo(), 'OTRO')
    await user.click(casilla())
    await user.click(enviar())
    expect(await screen.findByText(MENSAJE_OK)).toHaveAttribute('role', 'status')
    expect(cabecera(api.llamadasA(RUTA_SEGURO)[0].init, 'Authorization')).toBeUndefined()
    expect(cuerpo(api.llamadas, RUTA_SEGURO)).toEqual({
      nombre: 'Ana Pérez',
      telefono: '+57 300 123 4567',
      correo: null,
      tipoVehiculo: 'OTRO',
      autorizacionDatos: true,
      sitioWeb: '',
    })
    await user.click(screen.getByRole('button', { name: 'Enviar otra solicitud' }))
    expect(tipo()).toHaveValue('')
    expect(casilla()).not.toBeChecked()
  })

  it('un 400 en tipoVehiculo del servidor se muestra junto al select', async () => {
    const { user } = abrirAnonimo('#/seguro-todo-riesgo', {
      [RUTA_SEGURO]: { status: 400, body: { error: 'Revisa los datos marcados.', errores: { tipoVehiculo: ['Elige el tipo de vehículo.'] } } },
    })
    await formSeguro()
    await user.type(nombre(), 'Ana')
    await user.type(telefonoSeguro(), '3001234567')
    await user.selectOptions(tipo(), 'CAMION')
    await user.click(casilla())
    await user.click(enviar())
    await waitFor(() => expect(tipo()).toHaveAccessibleDescription('Elige el tipo de vehículo.'))
    expect(tipo()).toHaveValue('CAMION')
  })

  it('CA-P5b-12 con sesión envía el token', async () => {
    const { api, user } = abrirApp('#/seguro-todo-riesgo', ['COMPRADOR'], { [RUTA_SEGURO]: OK })
    await waitFor(() => expect(screen.getByLabelText('Nombre')).toHaveValue('Persona Ficticia'))
    await user.selectOptions(tipo(), 'MAQUINARIA')
    await user.click(casilla())
    await user.click(enviar())
    expect(await screen.findByText(MENSAJE_OK)).toBeInTheDocument()
    expect(cabecera(api.llamadasA(RUTA_SEGURO)[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
    expect((cuerpo(api.llamadas, RUTA_SEGURO) as { tipoVehiculo: string }).tipoVehiculo).toBe('MAQUINARIA')
  })
})

describe('Accesos en el portal (D-51 A, CA-P5b-54)', () => {
  it('rutas #/vende-tu-camion y #/seguro-todo-riesgo', () => {
    expect(leerRuta('#/vende-tu-camion')).toEqual({ nombre: 'vendeTuCamion' })
    expect(leerRuta('#/seguro-todo-riesgo/')).toEqual({ nombre: 'seguroTodoRiesgo' })
  })

  it('la portada tiene los dos bloques y la franja "Nuestros servicios" sigue sin enlaces', async () => {
    abrirAnonimo('#/', { [RUTA_INICIO]: { status: 200, body: { destacados: [], recientes: [] } } })
    const vende = await screen.findByRole('region', { name: '¿Quieres vender tu camión?' })
    expect(within(vende).getByRole('link', { name: /Vende tu camión/ })).toHaveAttribute('href', '#/vende-tu-camion')
    const seguro = screen.getByRole('region', { name: 'Asegura tu vehículo todo riesgo' })
    expect(within(seguro).getByRole('link', { name: /Compra tu seguro/ })).toHaveAttribute('href', '#/seguro-todo-riesgo')
    const servicios = screen.getByRole('region', { name: 'Nuestros servicios' })
    expect(within(servicios).queryAllByRole('link')).toHaveLength(0)
  })

  it('el pie tiene la columna "Servicios" con ambos enlaces y el menú principal no cambia', async () => {
    abrirAnonimo('#/vende-tu-camion')
    await formVende()
    const pie = screen.getByRole('navigation', { name: 'Servicios en el pie de página' })
    expect(within(pie).getByRole('heading', { name: 'Servicios' })).toBeInTheDocument()
    expect(within(pie).getByRole('link', { name: 'Vende tu camión' })).toHaveAttribute('href', '#/vende-tu-camion')
    expect(within(pie).getByRole('link', { name: 'Compra tu seguro todo riesgo' })).toHaveAttribute(
      'href',
      '#/seguro-todo-riesgo',
    )
    const encabezado = screen.getByRole('banner')
    expect(within(encabezado).queryByRole('link', { name: /Vende tu camión|seguro/i })).not.toBeInTheDocument()
  })
})

// ---- Panel del administrador ----

const ADMIN_BASE: Rutas = { [RUTA_CATALOGOS_INV]: { status: 200, body: CATALOGOS_INVENTARIO } }
const ADMINISTRACION = { tipo: 'ADMINISTRACION', asesorId: null, nombre: null }

const avisoVende = (id: number, extra: Record<string, unknown> = {}) =>
  lead(id, {
    origen: 'VENDE_TU_CAMION',
    tipoVehiculo: null,
    vehiculo: null,
    nombre: 'Carla Vende',
    mensaje: 'Kenworth T800 2015, 450.000 km',
    responsable: ADMINISTRACION,
    ...extra,
  })
const avisoSeguro = (id: number, extra: Record<string, unknown> = {}) =>
  lead(id, {
    origen: 'SEGURO_TODO_RIESGO',
    tipoVehiculo: 'MAQUINARIA',
    vehiculo: null,
    nombre: 'Diego Seguro',
    mensaje: 'Solicitud de seguro todo riesgo',
    responsable: ADMINISTRACION,
    ...extra,
  })

describe('Bandeja del administrador (CA-P5b-37, 38)', () => {
  it('cada solicitud muestra su origen; los avisos dicen "Sin vehículo asociado" y el seguro su tipo', async () => {
    abrirApp('#/contactos', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      [rutaLeads()]: paginaLeads([lead(1, { origen: 'CONTACTO_VEHICULO' }), avisoVende(2), avisoSeguro(3)]),
    })
    await screen.findByText('Carla Vende')
    const items = screen.getAllByRole('listitem')
    const de = (t: string) => items.find((li) => li.textContent?.includes(t)) as HTMLElement
    expect(within(de('Visitante 1')).getByText('Contacto de vehículo')).toBeInTheDocument()
    expect(within(de('Visitante 1')).getByText('Chevrolet NPR 2020')).toBeInTheDocument()
    expect(within(de('Carla Vende')).getByText('Vende tu camión')).toBeInTheDocument()
    expect(within(de('Carla Vende')).getByText('Sin vehículo asociado')).toBeInTheDocument()
    expect(within(de('Carla Vende')).getByText('Kenworth T800 2015, 450.000 km')).toBeInTheDocument()
    expect(within(de('Diego Seguro')).getByText('Seguro todo riesgo')).toBeInTheDocument()
    expect(within(de('Diego Seguro')).getByText('Maquinaria')).toBeInTheDocument()
  })

  it('filtra por origen con el parámetro "origen" y "Limpiar" lo quita', async () => {
    const { api, user } = abrirApp('#/contactos', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      [rutaLeads()]: paginaLeads([lead(1)]),
      [rutaLeads('origen=VENDE_TU_CAMION')]: paginaLeads([avisoVende(2)]),
    })
    await screen.findByText('Visitante 1')
    const origen = screen.getByLabelText('Origen')
    expect(within(origen).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Todos los orígenes',
      'Contacto de vehículo',
      'Vende tu camión',
      'Seguro todo riesgo',
      // P6 (CA-P6-40): orígenes nuevos.
      'Crédito',
      'Búsqueda',
    ])
    await user.selectOptions(origen, 'VENDE_TU_CAMION')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(await screen.findByText('Carla Vende')).toBeInTheDocument()
    expect(api.llamadasA(rutaLeads('origen=VENDE_TU_CAMION'))).toHaveLength(1)
    await user.click(screen.getByRole('button', { name: 'Limpiar' }))
    expect(await screen.findByText('Visitante 1')).toBeInTheDocument()
    expect(screen.getByLabelText('Origen')).toHaveValue('')
  })

  it('un 400 "Origen no válido." se muestra junto al filtro', async () => {
    const { user } = abrirApp('#/contactos', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      [rutaLeads()]: paginaLeads([lead(1)]),
      [rutaLeads('origen=SEGURO_TODO_RIESGO')]: {
        status: 400,
        body: { error: 'Revisa los filtros.', errores: { origen: ['Origen no válido.'] } },
      },
    })
    await screen.findByText('Visitante 1')
    await user.selectOptions(screen.getByLabelText('Origen'), 'SEGURO_TODO_RIESGO')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => expect(screen.getByLabelText('Origen')).toHaveAccessibleDescription('Origen no válido.'))
  })
})

describe('Detalle de un aviso (CA-P5b-38, 41, 43)', () => {
  it('"Vende tu camión": origen, descripción, "Sin vehículo asociado", consentimiento y sin "Reasignar"', async () => {
    abrirApp('#/contactos/2', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/2': {
        status: 200,
        body: detalleLead(2, {
          ...avisoVende(2),
          consentimientos: [{ formulario: 'VENDE_TU_CAMION', versionPolitica: 'BORRADOR-2026-11-04', aceptadoEn: '2026-11-06T15:00:00Z' }],
        }),
      },
    })
    expect(await screen.findByRole('heading', { level: 1, name: 'Solicitud de Carla Vende' })).toBeInTheDocument()
    expect(screen.getByText('Origen').nextElementSibling).toHaveTextContent('Vende tu camión')
    expect(screen.getByRole('heading', { level: 3, name: 'Descripción del vehículo' })).toBeInTheDocument()
    expect(screen.getAllByText('Kenworth T800 2015, 450.000 km').length).toBeGreaterThan(0)
    expect(screen.getByText('Sin vehículo asociado')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ver todos los contactos de este vehículo' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Ver en el portal' })).not.toBeInTheDocument()
    expect(screen.getByText(/Vende tu camión · versión BORRADOR-2026-11-04/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reasignar' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cerrar contacto' })).toBeInTheDocument()
  })

  it('seguro: muestra el tipo de vehículo', async () => {
    abrirApp('#/contactos/3', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/3': { status: 200, body: detalleLead(3, avisoSeguro(3)) },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Diego Seguro' })
    expect(screen.getByText('Tipo de vehículo').nextElementSibling).toHaveTextContent('Maquinaria')
    expect(screen.getByText('Origen').nextElementSibling).toHaveTextContent('Seguro todo riesgo')
    expect(screen.queryByRole('button', { name: 'Reasignar' })).not.toBeInTheDocument()
  })

  it('el cierre de un aviso ofrece "Solicitud atendida" y no los resultados de vehículo; envía ATENDIDA', async () => {
    const { api, user } = abrirApp('#/contactos/2', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/2': { status: 200, body: detalleLead(2, avisoVende(2)) },
      '/api/leads/2/cerrar': {
        status: 200,
        body: detalleLead(2, avisoVende(2, { estado: 'CERRADO', resultadoCierre: 'ATENDIDA', cerradoEn: '2026-11-06T16:00:00Z' })),
      },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Carla Vende' })
    await user.click(screen.getByRole('button', { name: 'Cerrar contacto' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Cerrar contacto' })
    const opciones = within(within(dialogo).getByLabelText('Resultado'))
      .getAllByRole('option')
      .map((o) => o.textContent)
    expect(opciones).toEqual([
      'Elige el resultado',
      'No le interesó',
      'No respondió',
      'Contacto duplicado o inválido',
      'Solicitud atendida',
      'Otro',
    ])
    await user.selectOptions(within(dialogo).getByLabelText('Resultado'), 'ATENDIDA')
    await user.click(within(dialogo).getByRole('button', { name: 'Cerrar contacto' }))
    expect(await screen.findByText('Contacto cerrado: Solicitud atendida.')).toBeInTheDocument()
    expect(cuerpo(api.llamadas, '/api/leads/2/cerrar')).toEqual({ resultado: 'ATENDIDA', observacion: null })
    expect(screen.getByText('Resultado del cierre').nextElementSibling).toHaveTextContent('Solicitud atendida')
    expect(screen.getByRole('button', { name: 'Reabrir' })).toBeInTheDocument()
  })

  it('un 400 "Ese resultado no aplica a esta solicitud." se muestra en el resultado sin cerrar el diálogo', async () => {
    const { user } = abrirApp('#/contactos/2', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/2': { status: 200, body: detalleLead(2, avisoVende(2)) },
      '/api/leads/2/cerrar': {
        status: 400,
        body: { error: 'Revisa los datos marcados.', errores: { resultado: ['Ese resultado no aplica a esta solicitud.'] } },
      },
    })
    await screen.findByRole('heading', { level: 1, name: 'Solicitud de Carla Vende' })
    await user.click(screen.getByRole('button', { name: 'Cerrar contacto' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Cerrar contacto' })
    await user.selectOptions(within(dialogo).getByLabelText('Resultado'), 'NO_RESPONDIO')
    await user.click(within(dialogo).getByRole('button', { name: 'Cerrar contacto' }))
    await waitFor(() =>
      expect(within(dialogo).getByLabelText('Resultado')).toHaveAccessibleDescription(
        'Ese resultado no aplica a esta solicitud.',
      ),
    )
    expect(screen.getByRole('dialog', { name: 'Cerrar contacto' })).toBeInTheDocument()
  })

  it('un contacto de vehículo sigue ofreciendo "Reasignar" y no ofrece "Solicitud atendida" (CA-P5b-61)', async () => {
    const { user } = abrirApp('#/contactos/1', ['ADMINISTRADOR'], {
      ...ADMIN_BASE,
      '/api/leads/1': { status: 200, body: detalleLead(1, { origen: 'CONTACTO_VEHICULO' }) },
    })
    await screen.findByRole('heading', { level: 1, name: 'Contacto de Visitante 1' })
    expect(screen.getByText('Origen').nextElementSibling).toHaveTextContent('Contacto de vehículo')
    expect(screen.getByRole('button', { name: 'Reasignar' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Cerrar contacto' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Cerrar contacto' })
    const valores = within(within(dialogo).getByLabelText('Resultado'))
      .getAllByRole('option')
      .map((o) => (o as HTMLOptionElement).value)
    expect(valores).toContain('VENTA_CONCRETADA')
    expect(valores).toContain('VEHICULO_NO_DISPONIBLE')
    expect(valores).not.toContain('ATENDIDA')
  })

  it('resultados por origen según la tabla del contrato', () => {
    const valores = (o: Parameters<typeof resultadosCierrePara>[0]) => resultadosCierrePara(o).map((r) => r.valor)
    expect(valores('CONTACTO_VEHICULO')).toEqual([
      'VENTA_CONCRETADA',
      'NO_INTERESADO',
      'NO_RESPONDIO',
      'VEHICULO_NO_DISPONIBLE',
      'DUPLICADO_INVALIDO',
      'OTRO',
    ])
    expect(valores('SEGURO_TODO_RIESGO')).toEqual(['NO_INTERESADO', 'NO_RESPONDIO', 'DUPLICADO_INVALIDO', 'ATENDIDA', 'OTRO'])
  })
})

describe('Permisos: el asesor y el comprador no ven nada nuevo (CA-P5b-46, 48, 49)', () => {
  it('el asesor no tiene filtro ni etiqueta de origen y su consulta no lleva "origen"', async () => {
    const { api } = abrirApp('#/contactos', ['ASESOR'], { [rutaLeads()]: paginaLeads([lead(1)]) })
    await screen.findByText('Visitante 1')
    expect(screen.queryByLabelText('Origen')).not.toBeInTheDocument()
    expect(screen.queryByText('Contacto de vehículo')).not.toBeInTheDocument()
    expect(api.llamadas.filter((l) => l.url.startsWith('/api/leads')).every((l) => !l.url.includes('origen'))).toBe(true)
  })

  it('el detalle del asesor no muestra el origen y conserva sus acciones de P5', async () => {
    abrirApp('#/contactos/1', ['ASESOR'], { '/api/leads/1': { status: 200, body: detalleLead(1) } })
    await screen.findByRole('heading', { level: 1, name: 'Contacto de Visitante 1' })
    expect(screen.queryByText('Origen')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reasignar' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cerrar contacto' })).toBeInTheDocument()
  })

  it('el comprador que envía un aviso no llama a /api/leads ni ve "Mis solicitudes" (D-53 A)', async () => {
    const { api, user } = abrirApp('#/seguro-todo-riesgo', ['COMPRADOR'], { [RUTA_SEGURO]: OK })
    await waitFor(() => expect(screen.getByLabelText('Nombre')).toHaveValue('Persona Ficticia'))
    await user.selectOptions(tipo(), 'CARRO')
    await user.click(casilla())
    await user.click(enviar())
    await screen.findByText(MENSAJE_OK)
    expect(api.llamadas.some((l) => l.url.startsWith('/api/leads'))).toBe(false)
    expect(screen.queryByRole('link', { name: /Mis solicitudes/ })).not.toBeInTheDocument()
  })
})

describe('Confirmación tras enviar un aviso (pedido de la responsable, 2026-11-09)', () => {
  it('oculta el formulario y ofrece volver al inicio, ver vehículos o enviar otra solicitud', async () => {
    const { user } = abrirAnonimo('#/vende-tu-camion', { [RUTA_VENDE]: OK })
    await formVende()
    await user.type(nombre(), 'Ana')
    await user.type(telefonoVende(), '3001234567')
    await user.type(descripcion(), 'Camión NHR')
    await user.click(casilla())
    await user.click(enviar())

    const exito = await screen.findByText(MENSAJE_OK)
    await waitFor(() => expect(exito.parentElement).toHaveFocus())
    expect(screen.queryByRole('form', { name: 'Formulario Vende tu camión' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Volver al inicio' })).toHaveAttribute('href', '#/')
    expect(screen.getByRole('link', { name: 'Ver vehículos' })).toHaveAttribute('href', '#/buscar')

    await user.click(screen.getByRole('button', { name: 'Enviar otra solicitud' }))
    expect(await formVende()).toBeInTheDocument()
    await waitFor(() => expect(nombre()).toHaveFocus())
    expect(nombre()).toHaveValue('')
    expect(screen.queryByText(MENSAJE_OK)).not.toBeInTheDocument()
  })
})
