// P5 — Formulario "Contactar" y botón de WhatsApp en la ficha del vehículo, y página #/privacidad.
// CA-P5-01, 02, 03, 04, 06, 07, 09, 11 (409), 12, 13, 14, 17, 18, 22, 54, 55, 56, 61, 62, 64; D-44, D-47.
import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { normalizarTelefono } from '../src/api/contactos'
import { abrirApp, detalle, respuestaFiltros, RUTA_FILTROS, rutaDetalle, V1 } from './catalogoDatos'
import { cabecera, guardarSesionFicticia, PERFIL_FICTICIO, simularFetch, TOKEN_FICTICIO, type RespuestaSimulada } from './helpers'
import { cuerpo, retenerRuta, RUTA_CONTACTOS } from './p5Datos'

const MENSAJE_OK = '¡Listo! Recibimos tus datos. Te contactaremos pronto por teléfono o WhatsApp.'
const WA_ASESOR = 'https://wa.me/573001112233'

function abrirFicha(contacto?: RespuestaSimulada | RespuestaSimulada[], extra: Record<string, RespuestaSimulada> = {}, whatsapp: string | null = WA_ASESOR) {
  const api = simularFetch({
    [RUTA_FILTROS]: respuestaFiltros(),
    [rutaDetalle(V1.slug)]: { status: 200, body: detalle(V1, { whatsapp }) },
    ...(contacto ? { [RUTA_CONTACTOS]: contacto } : {}),
    ...extra,
  })
  abrirApp(`#/vehiculo/${V1.slug}`)
  return { api, user: userEvent.setup() }
}

async function formulario() {
  return screen.findByRole('form', { name: 'Formulario de contacto' })
}

const nombre = () => screen.getByLabelText('Nombre')
const telefono = () => screen.getByLabelText('Teléfono o WhatsApp')
const correo = () => screen.getByLabelText('Correo (opcional)')
const mensaje = () => screen.getByLabelText('Mensaje (opcional)')
const casilla = () => screen.getByRole('checkbox', { name: /Autorizo el tratamiento de mis datos personales/ })
const enviar = () => screen.getByRole('button', { name: 'Enviar' })

describe('Formulario de contacto: estructura (CA-P5-12, CA-P5-13, CA-P5-22)', () => {
  it('nota de obligatorios, aria-required en nombre, teléfono y casilla; correo y mensaje opcionales', async () => {
    abrirFicha()
    const form = await formulario()
    expect(within(form).getByText('Campo obligatorio')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2, name: 'Contactar' })).toBeInTheDocument()
    expect(nombre()).toHaveAttribute('aria-required', 'true')
    expect(telefono()).toHaveAttribute('aria-required', 'true')
    expect(casilla()).toHaveAttribute('aria-required', 'true')
    expect(correo()).not.toHaveAttribute('aria-required')
    expect(mensaje()).not.toHaveAttribute('aria-required')
    // Las etiquetas obligatorias llevan la clase del asterisco (D-31).
    expect(form.querySelector('label[for="contacto-nombre"]')).toHaveClass('campo-obligatorio')
    expect(form.querySelector('label[for="contacto-correo"]')).not.toHaveClass('campo-obligatorio')
  })

  it('la casilla empieza sin marcar y enlaza a #/privacidad en una pestaña nueva', async () => {
    abrirFicha()
    await formulario()
    expect(casilla()).not.toBeChecked()
    const enlace = screen.getByRole('link', { name: /Política de tratamiento de datos/ })
    expect(enlace).toHaveAttribute('href', '#/privacidad')
    expect(enlace).toHaveAttribute('target', '_blank')
    expect(enlace).toHaveAttribute('rel', 'noopener noreferrer')
  })

  it('el campo trampa sitioWeb está en el DOM, fuera del orden de tabulación y oculto a lectores de pantalla', async () => {
    abrirFicha()
    const form = await formulario()
    const trampa = form.querySelector<HTMLInputElement>('input[name="sitioWeb"]')
    expect(trampa).not.toBeNull()
    expect(trampa).toHaveAttribute('tabindex', '-1')
    expect(trampa?.closest('[aria-hidden="true"]')).not.toBeNull()
    expect(screen.queryByRole('textbox', { name: /Sitio web/ })).not.toBeInTheDocument()
  })

  it('el mensaje tiene límite de 500 y un contador', async () => {
    const { user } = abrirFicha()
    await formulario()
    expect(mensaje()).toHaveAttribute('maxLength', '500')
    expect(screen.getByText('Hasta 500 caracteres. Llevas 0.')).toBeInTheDocument()
    await user.type(mensaje(), 'Hola')
    expect(screen.getByText('Hasta 500 caracteres. Llevas 4.')).toBeInTheDocument()
  })
})

describe('Envío del contacto', () => {
  it('CA-P5-01 / CA-P5-54 anónimo: POST sin token, mensaje de éxito exacto, formulario vacío y casilla desmarcada', async () => {
    const { api, user } = abrirFicha({ status: 201, body: { mensaje: MENSAJE_OK } })
    await formulario()
    await user.type(nombre(), 'Ana Prueba')
    await user.type(telefono(), '300 123 4567')
    await user.type(correo(), 'ana@ejemplo.test')
    await user.type(mensaje(), '¿Sigue disponible?')
    await user.click(casilla())
    await user.click(enviar())

    expect(await screen.findByText(MENSAJE_OK)).toHaveAttribute('role', 'status')
    const llamadas = api.llamadasA(RUTA_CONTACTOS)
    expect(llamadas).toHaveLength(1)
    expect(llamadas[0].init?.method).toBe('POST')
    expect(cabecera(llamadas[0].init, 'Authorization')).toBeUndefined()
    expect(cuerpo(api.llamadas, RUTA_CONTACTOS)).toEqual({
      slug: V1.slug,
      nombre: 'Ana Prueba',
      telefono: '300 123 4567',
      correo: 'ana@ejemplo.test',
      mensaje: '¿Sigue disponible?',
      autorizacionDatos: true,
      sitioWeb: '',
    })
    expect(nombre()).toHaveValue('')
    expect(telefono()).toHaveValue('')
    expect(correo()).toHaveValue('')
    expect(mensaje()).toHaveValue('')
    expect(casilla()).not.toBeChecked()
  })

  it('correo y mensaje vacíos viajan como null', async () => {
    const { api, user } = abrirFicha({ status: 201, body: { mensaje: MENSAJE_OK } })
    await formulario()
    await user.type(nombre(), 'Ana')
    await user.type(telefono(), '6011234567')
    await user.click(casilla())
    await user.click(enviar())
    await screen.findByText(MENSAJE_OK)
    expect(cuerpo(api.llamadas, RUTA_CONTACTOS)).toMatchObject({ correo: null, mensaje: null, telefono: '6011234567' })
  })

  it('CA-P5-12 el botón se desactiva mientras se envía (sin doble envío)', async () => {
    const { api, user } = abrirFicha({ status: 201, body: { mensaje: MENSAJE_OK } })
    await formulario()
    await user.type(nombre(), 'Ana')
    await user.type(telefono(), '3001234567')
    await user.click(casilla())
    const retenida = retenerRuta(RUTA_CONTACTOS)
    await user.click(enviar())
    const boton = await screen.findByRole('button', { name: 'Enviando...' })
    expect(boton).toBeDisabled()
    await user.click(boton)
    retenida.soltar()
    await screen.findByText(MENSAJE_OK)
    expect(api.llamadasA(RUTA_CONTACTOS)).toHaveLength(1)
  })

  it('CA-P5-03, 04, 14, 55 vacío: errores exactos junto a cada campo, aria-invalid, foco al primero y sin llamar a la API', async () => {
    const { api, user } = abrirFicha()
    await formulario()
    await user.click(enviar())
    expect(nombre()).toHaveAttribute('aria-invalid', 'true')
    expect(nombre()).toHaveAccessibleDescription('Escribe tu nombre (entre 2 y 150 caracteres).')
    expect(telefono()).toHaveAttribute('aria-invalid', 'true')
    expect(telefono().getAttribute('aria-describedby')).toContain('contacto-telefono-error')
    expect(screen.getByText('Escribe un celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567.')).toBeInTheDocument()
    expect(casilla()).toHaveAttribute('aria-invalid', 'true')
    expect(casilla()).toHaveAccessibleDescription('Debes autorizar el tratamiento de tus datos para enviar el formulario.')
    expect(correo()).toHaveAttribute('aria-invalid', 'false')
    await waitFor(() => expect(nombre()).toHaveFocus())
    expect(api.llamadasA(RUTA_CONTACTOS)).toHaveLength(0)
  })

  it.each([['1234567'], ['+1 202 555 0123'], ['2001234567'], ['30012345678']])(
    'CA-P5-04 el teléfono %s se rechaza en el navegador',
    async (valor) => {
      const { api, user } = abrirFicha()
      await formulario()
      await user.type(nombre(), 'Ana')
      await user.type(telefono(), valor)
      await user.click(casilla())
      await user.click(enviar())
      expect(telefono()).toHaveAttribute('aria-invalid', 'true')
      await waitFor(() => expect(telefono()).toHaveFocus())
      expect(api.llamadasA(RUTA_CONTACTOS)).toHaveLength(0)
    },
  )

  it('CA-P5-06 un correo mal escrito se marca; el foco va al correo y se conservan los datos', async () => {
    const { user } = abrirFicha()
    await formulario()
    await user.type(nombre(), 'Ana')
    await user.type(telefono(), '3001234567')
    await user.type(correo(), 'ana@')
    await user.click(casilla())
    await user.click(enviar())
    expect(correo()).toHaveAccessibleDescription('Escribe un correo válido o deja el campo vacío.')
    await waitFor(() => expect(correo()).toHaveFocus())
    expect(nombre()).toHaveValue('Ana')
    expect(casilla()).toBeChecked()
  })

  it('CA-P5-55 un 400 del servidor marca cada campo con su mensaje, conserva lo escrito y enfoca el primero', async () => {
    const { user } = abrirFicha({
      status: 400,
      body: {
        error: 'Revisa los datos marcados.',
        errores: {
          telefono: ['Escribe un celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567.'],
          autorizacionDatos: ['Debes autorizar el tratamiento de tus datos para enviar el formulario.'],
        },
      },
    })
    await formulario()
    await user.type(nombre(), 'Ana')
    await user.type(telefono(), '3001234567')
    await user.click(casilla())
    await user.click(enviar())
    expect(await screen.findByText('Revisa los datos marcados.')).toHaveAttribute('role', 'alert')
    expect(telefono()).toHaveAttribute('aria-invalid', 'true')
    expect(casilla()).toHaveAttribute('aria-invalid', 'true')
    await waitFor(() => expect(telefono()).toHaveFocus())
    expect(nombre()).toHaveValue('Ana')
    expect(telefono()).toHaveValue('3001234567')
  })

  it.each([
    [404, 'Este vehículo ya no está disponible. Mira otros vehículos en el catálogo.'],
    [409, 'Este vehículo es tuyo; no puedes contactarte a ti mismo.'],
    [413, 'La solicitud es demasiado grande.'],
    [429, 'Has enviado muchas solicitudes. Intenta de nuevo en unos minutos.'],
  ])('CA-P5-09, 11, 56: un %i muestra "%s" y conserva los datos', async (status, texto) => {
    const { user } = abrirFicha({ status, body: { error: texto } })
    await formulario()
    await user.type(nombre(), 'Ana')
    await user.type(telefono(), '3001234567')
    await user.click(casilla())
    await user.click(enviar())
    expect(await screen.findByText(texto)).toHaveAttribute('role', 'alert')
    expect(nombre()).toHaveValue('Ana')
    if (status === 404) expect(screen.getByRole('link', { name: 'Ver el catálogo' })).toHaveAttribute('href', '#/buscar')
  })

  it.each([
    ['500', { status: 500, body: { error: 'System.Exception: detalle interno' } } as RespuestaSimulada],
    ['sin conexión', { red: true } as RespuestaSimulada],
  ])('CA-P5-56 %s: mensaje con el WhatsApp del portal y sin detalles técnicos', async (_n, respuesta) => {
    const { user } = abrirFicha(respuesta)
    await formulario()
    await user.type(nombre(), 'Ana')
    await user.type(telefono(), '3001234567')
    await user.click(casilla())
    await user.click(enviar())
    expect(
      await screen.findByText(
        'No pudimos enviar tus datos. Intenta de nuevo en unos minutos o escríbenos por WhatsApp al 316 615 2907.',
      ),
    ).toBeInTheDocument()
    expect(screen.queryByText(/System\.Exception/)).not.toBeInTheDocument()
  })

  it('CA-P5-22 si un robot llena el campo trampa, se envía tal cual (la API responde 201 sin guardar)', async () => {
    const { api, user } = abrirFicha({ status: 201, body: { mensaje: MENSAJE_OK } })
    const form = await formulario()
    await user.type(nombre(), 'Robot')
    await user.type(telefono(), '3001234567')
    await user.click(casilla())
    fireEvent.change(form.querySelector('input[name="sitioWeb"]') as HTMLInputElement, { target: { value: 'http://spam.test' } })
    await user.click(enviar())
    await screen.findByText(MENSAJE_OK)
    expect(cuerpo(api.llamadas, RUTA_CONTACTOS)).toMatchObject({ sitioWeb: 'http://spam.test' })
  })
})

describe('Contacto con sesión (CA-P5-02, CA-P5-18)', () => {
  it('comprador: datos del perfil prellenados, casilla sin marcar, token enviado y casilla en cada envío', async () => {
    guardarSesionFicticia(Date.now() + 60 * 60 * 1000)
    const { api, user } = abrirFicha({ status: 201, body: { mensaje: MENSAJE_OK } }, {
      '/api/auth/me': { status: 200, body: PERFIL_FICTICIO },
    })
    await formulario()
    await waitFor(() => expect(nombre()).toHaveValue(PERFIL_FICTICIO.nombreCompleto))
    expect(telefono()).toHaveValue(PERFIL_FICTICIO.phoneNumber)
    expect(correo()).toHaveValue(PERFIL_FICTICIO.email)
    expect(casilla()).not.toBeChecked()

    await user.click(casilla())
    await user.click(enviar())
    await screen.findByText(MENSAJE_OK)
    const llamada = api.llamadasA(RUTA_CONTACTOS)[0]
    expect(cabecera(llamada.init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
    // El catálogo sigue sin token.
    for (const l of api.llamadas.filter((x) => x.url.startsWith('/api/catalogo'))) {
      expect(cabecera(l.init, 'Authorization')).toBeUndefined()
    }

    // Tras el envío vuelven los datos del perfil, pero la autorización hay que marcarla otra vez.
    expect(nombre()).toHaveValue(PERFIL_FICTICIO.nombreCompleto)
    expect(casilla()).not.toBeChecked()
    await user.click(enviar())
    expect(casilla()).toHaveAttribute('aria-invalid', 'true')
    expect(api.llamadasA(RUTA_CONTACTOS)).toHaveLength(1)
  })
})

describe('Asesor en su propia publicación (CA-P5-11)', () => {
  const PERFIL_ASESOR = { ...PERFIL_FICTICIO, nombreCompleto: 'Asesor Ficticio', roles: ['ASESOR'] }

  it('el asesor pide el detalle con su token; si es suyo ve un aviso y no el formulario', async () => {
    guardarSesionFicticia(Date.now() + 60 * 60 * 1000)
    const api = simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [rutaDetalle(V1.slug)]: { status: 200, body: { ...detalle(V1, { whatsapp: WA_ASESOR }), esPropio: true } },
      '/api/auth/me': { status: 200, body: PERFIL_ASESOR },
    })
    abrirApp(`#/vehiculo/${V1.slug}`)
    expect(await screen.findByText(/Esta es una de tus publicaciones/)).toBeInTheDocument()
    expect(screen.queryByRole('form', { name: 'Formulario de contacto' })).not.toBeInTheDocument()
    const conToken = api.llamadasA(rutaDetalle(V1.slug)).filter((l) => cabecera(l.init, 'Authorization'))
    expect(cabecera(conToken[conToken.length - 1].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
    expect(api.llamadasA(RUTA_CONTACTOS)).toHaveLength(0)
  })

  it('el asesor en una publicación ajena (esPropio false) ve el formulario', async () => {
    guardarSesionFicticia(Date.now() + 60 * 60 * 1000)
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [rutaDetalle(V1.slug)]: { status: 200, body: { ...detalle(V1, { whatsapp: WA_ASESOR }), esPropio: false } },
      '/api/auth/me': { status: 200, body: PERFIL_ASESOR },
    })
    abrirApp(`#/vehiculo/${V1.slug}`)
    expect(await formulario()).toBeInTheDocument()
    expect(screen.queryByText(/Esta es una de tus publicaciones/)).not.toBeInTheDocument()
  })
})

describe('Botón "Escribir por WhatsApp" (D-47)', () => {
  it('CA-P5-61 usa el enlace del asesor con un texto que nombra el vehículo, en otra pestaña', async () => {
    const { api } = abrirFicha()
    const boton = await screen.findByRole('link', { name: /Escribir por WhatsApp/ })
    const texto = encodeURIComponent(`Hola, me interesa el vehículo ${V1.titulo} que vi en Financiamos Tu Camión.`)
    expect(boton).toHaveAttribute('href', `${WA_ASESOR}?text=${texto}`)
    expect(boton).toHaveAttribute('target', '_blank')
    expect(boton).toHaveAttribute('rel', 'noopener noreferrer')
    // CA-P5-64: pulsarlo no crea un contacto.
    const evitar = (e: Event) => e.preventDefault()
    document.addEventListener('click', evitar, { capture: true })
    fireEvent.click(boton)
    document.removeEventListener('click', evitar, { capture: true })
    expect(api.llamadasA(RUTA_CONTACTOS)).toHaveLength(0)
  })

  it.each([[null], ['javascript:alert(1)']])('CA-P5-62 sin un enlace válido (%s) usa el WhatsApp del portal', async (wa) => {
    abrirFicha(undefined, {}, wa)
    const boton = await screen.findByRole('link', { name: /Escribir por WhatsApp/ })
    expect(boton.getAttribute('href')).toMatch(/^https:\/\/wa\.me\/573166152907\?text=/)
  })
})

describe('Teléfono colombiano (CA-P5-04, CA-P5-05)', () => {
  it.each([
    ['300 123 4567', '+573001234567'],
    ['300-123-4567', '+573001234567'],
    ['(300) 1234567', '+573001234567'],
    ['+57 3001234567', '+573001234567'],
    ['573001234567', '+573001234567'],
    ['601 234 5678', '+576012345678'],
  ])('%s se acepta como %s', (valor, esperado) => {
    expect(normalizarTelefono(valor)).toBe(esperado)
  })

  it.each([[''], ['1234567'], ['+1 202 555 0123'], ['5012345678'], ['300123456a'], ['3001234567890123456789012345']])(
    '%s se rechaza',
    (valor) => {
      expect(normalizarTelefono(valor)).toBeNull()
    },
  )
})

describe('#/privacidad (D-44, CA-P5-17)', () => {
  it('muestra la franja de borrador, el aviso de revisión legal y el contacto, sin el borrador', async () => {
    simularFetch({})
    abrirApp('#/privacidad')
    expect(await screen.findByRole('heading', { level: 1, name: 'Política de tratamiento de datos personales' })).toBeInTheDocument()
    expect(screen.getByText('BORRADOR — en revisión legal')).toBeInTheDocument()
    expect(screen.getByText(/se publicará en esta página cuando termine su revisión legal/)).toBeInTheDocument()
    const main = screen.getByRole('main')
    expect(within(main).getByRole('link', { name: 'vc.automotores@gmail.com' })).toHaveAttribute('href', 'mailto:vc.automotores@gmail.com')
    expect(within(main).getByRole('link', { name: /316 615 2907/ })).toHaveAttribute('href', 'https://wa.me/573166152907')
    expect(main).not.toHaveTextContent(/POR COMPLETAR|NIT|Responsable del tratamiento/)
  })
})
