// P2 — Registro de asesor (CA-043R-17 a CA-043R-22) y no regresión del registro de comprador.
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../src/App'
import { RegistroForm } from '../src/components/RegistroForm'
import { irACuenta, simularFetch } from './helpers'

const RUTA_ASESOR = '/api/auth/registro/asesor'
const RUTA_COMPRADOR = '/api/auth/registro/comprador'
const MENSAJE_OK =
  'Solicitud enviada. Podrás iniciar sesión cuando el administrador apruebe tu cuenta.'
const MENSAJE_DOC = 'Escribe un número de documento válido (4 a 30 letras, números o guiones).'

// Datos ficticios.
const ASESOR = {
  nombre: 'Asesor Ficticio',
  email: 'asesor.ficticio@ejemplo.test',
  telefono: '+57 310 000 0000',
  password: 'Camion2026a',
  tipo: 'CC',
  numero: '1012345678',
}

type Usuario = ReturnType<typeof userEvent.setup>

async function elegirAsesor(user: Usuario) {
  await user.click(screen.getByLabelText('Soy asesor'))
}

async function rellenarAsesor(user: Usuario, extra: { numero?: string; comercial?: string } = {}) {
  await user.type(screen.getByLabelText('Nombre completo'), ASESOR.nombre)
  await user.type(screen.getByLabelText('Correo electrónico'), ASESOR.email)
  await user.type(screen.getByLabelText('Teléfono'), ASESOR.telefono)
  await user.selectOptions(screen.getByLabelText('Tipo de documento'), ASESOR.tipo)
  await user.type(screen.getByLabelText('Número de documento'), extra.numero ?? ASESOR.numero)
  if (extra.comercial) {
    await user.type(screen.getByLabelText('Nombre comercial (opcional)'), extra.comercial)
  }
  await user.type(screen.getByLabelText('Contraseña'), ASESOR.password)
}

function errorDe(id: string) {
  return document.getElementById(`${id}-error`)
}

function cuerpo(api: ReturnType<typeof simularFetch>, ruta: string, i = 0) {
  return JSON.parse(String(api.llamadasA(ruta)[i].init?.body))
}

// P3: la app abre en el catálogo; estas pruebas usan la pantalla de cuenta.
beforeEach(irACuenta)

describe('Registro de asesor', () => {
  it('CA-043R-17 selector comprador/asesor: por defecto comprador; asesor añade los campos de documento', async () => {
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)

    const comprador = screen.getByLabelText('Soy comprador')
    const asesor = screen.getByLabelText('Soy asesor')
    expect(comprador).toBeChecked()
    expect(asesor).not.toBeChecked()
    expect(screen.queryByLabelText('Tipo de documento')).toBeNull()
    expect(screen.queryByLabelText('Número de documento')).toBeNull()
    expect(screen.queryByLabelText('Nombre comercial (opcional)')).toBeNull()
    expect(screen.getByRole('button', { name: 'Crear cuenta' })).toBeInTheDocument()

    await elegirAsesor(user)
    const tipo = screen.getByLabelText('Tipo de documento')
    const valores = within(tipo)
      .getAllByRole('option')
      .map((o) => (o as HTMLOptionElement).value)
      .filter((v) => v !== '')
    expect(valores).toEqual(['CC', 'CE', 'NIT', 'PAS'])
    expect(screen.getByLabelText('Número de documento')).toHaveAttribute('aria-required', 'true')
    const comercial = screen.getByLabelText('Nombre comercial (opcional)')
    expect(comercial).not.toHaveAttribute('aria-required')
    expect(comercial).toHaveAttribute('maxLength', '150')
    expect(screen.getByRole('button', { name: 'Enviar solicitud' })).toBeInTheDocument()

    // Volver a comprador quita los campos de asesor.
    await user.click(comprador)
    expect(screen.queryByLabelText('Tipo de documento')).toBeNull()
    expect(screen.getByRole('button', { name: 'Crear cuenta' })).toBeInTheDocument()
  })

  it('CA-043R-18 el formulario de asesor muestra las 4 reglas de contraseña y su cumplimiento', async () => {
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await elegirAsesor(user)
    const reglas = document.getElementById('password-reglas') as HTMLElement
    const items = within(reglas).getAllByRole('listitem')
    expect(items).toHaveLength(4)
    for (const li of items) expect(li).toHaveTextContent('(pendiente)')
    await user.type(screen.getByLabelText('Contraseña'), ASESOR.password)
    for (const li of within(reglas).getAllByRole('listitem')) expect(li).toHaveTextContent('(cumplida)')
  })

  it('CA-043R-19 envío vacío de asesor: no llama a la API; tipo y número inválidos; nombre comercial sin error', async () => {
    const api = simularFetch({})
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await elegirAsesor(user)
    await user.click(screen.getByRole('button', { name: 'Enviar solicitud' }))

    expect(screen.getByLabelText('Tipo de documento')).toHaveAttribute('aria-invalid', 'true')
    expect(errorDe('tipoDocumento')).toHaveTextContent('Elige un tipo de documento válido.')
    expect(screen.getByLabelText('Número de documento')).toHaveAttribute('aria-invalid', 'true')
    expect(errorDe('numeroDocumento')).toHaveTextContent(MENSAJE_DOC)
    expect(errorDe('nombreComercial')).toBeNull()
    expect(errorDe('nombreCompleto')).toHaveTextContent('Escribe tu nombre completo')
    expect(errorDe('email')).toHaveTextContent('Escribe un correo válido.')
    expect(api.fn).not.toHaveBeenCalled()
    // El foco va al primer campo inválido.
    expect(screen.getByLabelText('Nombre completo')).toHaveFocus()
  })

  it.each(['123', '10.123.456', '10 123 456', '123#45', '    '])(
    'CA-043R-19 número de documento inválido "%s" no se envía',
    async (numero) => {
      const api = simularFetch({})
      const user = userEvent.setup()
      render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
      await elegirAsesor(user)
      await rellenarAsesor(user, { numero })
      await user.click(screen.getByRole('button', { name: 'Enviar solicitud' }))
      expect(errorDe('numeroDocumento')).toHaveTextContent(MENSAJE_DOC)
      expect(screen.getByLabelText('Número de documento')).toHaveFocus()
      expect(api.fn).not.toHaveBeenCalled()
    },
  )

  it('CA-043R-19 nombre comercial de 151 caracteres muestra error y no se envía; 150 sí se envía', async () => {
    const api = simularFetch({ [RUTA_ASESOR]: { status: 201, body: { usuarioId: 'u-1', mensaje: MENSAJE_OK } } })
    const user = userEvent.setup()
    const onRegistrado = vi.fn()
    render(<RegistroForm onRegistrado={onRegistrado} onIrALogin={vi.fn()} />)
    await elegirAsesor(user)
    await rellenarAsesor(user)
    // maxLength impide escribir 151 con el teclado; se fuerza el valor para probar la validación.
    fireEvent.change(screen.getByLabelText('Nombre comercial (opcional)'), {
      target: { value: 'C'.repeat(151) },
    })
    await user.click(screen.getByRole('button', { name: 'Enviar solicitud' }))
    expect(errorDe('nombreComercial')).toHaveTextContent(
      'El nombre comercial no puede tener más de 150 caracteres.',
    )
    expect(api.fn).not.toHaveBeenCalled()

    fireEvent.change(screen.getByLabelText('Nombre comercial (opcional)'), {
      target: { value: 'C'.repeat(150) },
    })
    expect(errorDe('nombreComercial')).toBeNull()
    await user.click(screen.getByRole('button', { name: 'Enviar solicitud' }))
    await waitFor(() => expect(onRegistrado).toHaveBeenCalledWith(MENSAJE_OK, ASESOR.email))
    expect(cuerpo(api, RUTA_ASESOR).nombreComercial).toBe('C'.repeat(150))
  })

  it('CA-043R-20 409 por documento: el mensaje va bajo el número de documento y se conservan los datos', async () => {
    simularFetch({
      [RUTA_ASESOR]: {
        status: 409,
        body: {
          error: 'Ya existe un asesor con ese documento.',
          errores: { numeroDocumento: ['Ya existe un asesor con ese documento.'] },
        },
      },
    })
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await elegirAsesor(user)
    await rellenarAsesor(user)
    await user.click(screen.getByRole('button', { name: 'Enviar solicitud' }))

    await waitFor(() =>
      expect(errorDe('numeroDocumento')).toHaveTextContent('Ya existe un asesor con ese documento.'),
    )
    expect(screen.getByLabelText('Número de documento')).toHaveFocus()
    expect(screen.getByLabelText('Número de documento')).toHaveAttribute('aria-invalid', 'true')
    expect(errorDe('email')).toBeNull()
    expect(screen.getByLabelText('Correo electrónico')).toHaveValue(ASESOR.email)
    expect(screen.getByLabelText('Número de documento')).toHaveValue(ASESOR.numero)
    expect(screen.getByLabelText('Tipo de documento')).toHaveValue(ASESOR.tipo)
  })

  it('CA-043R-20 409 por correo: el mensaje va bajo el correo', async () => {
    simularFetch({ [RUTA_ASESOR]: { status: 409, body: { error: 'Este correo ya está registrado.' } } })
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await elegirAsesor(user)
    await rellenarAsesor(user)
    await user.click(screen.getByRole('button', { name: 'Enviar solicitud' }))

    await waitFor(() => expect(errorDe('email')).toHaveTextContent('Este correo ya está registrado.'))
    expect(screen.getByLabelText('Correo electrónico')).toHaveFocus()
    expect(errorDe('numeroDocumento')).toBeNull()
    expect(screen.getByLabelText('Nombre completo')).toHaveValue(ASESOR.nombre)
  })

  it('CA-043R-20 400 con errores: cada mensaje bajo su campo de asesor y aviso general', async () => {
    simularFetch({
      [RUTA_ASESOR]: {
        status: 400,
        body: {
          error: 'Revisa los datos marcados.',
          errores: {
            tipoDocumento: ['Elige un tipo de documento válido.'],
            numeroDocumento: [MENSAJE_DOC],
            nombreComercial: ['El nombre comercial no puede tener más de 150 caracteres.'],
          },
        },
      },
    })
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await elegirAsesor(user)
    await rellenarAsesor(user, { comercial: 'Camiones Ficticios' })
    await user.click(screen.getByRole('button', { name: 'Enviar solicitud' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Revisa los datos marcados.')
    expect(errorDe('tipoDocumento')).toHaveTextContent('Elige un tipo de documento válido.')
    expect(errorDe('numeroDocumento')).toHaveTextContent(MENSAJE_DOC)
    expect(errorDe('nombreComercial')).toHaveTextContent('El nombre comercial no puede tener más de 150')
    expect(screen.getByLabelText('Nombre comercial (opcional)')).toHaveValue('Camiones Ficticios')
  })

  it('CA-043R-21 201: vuelve al login con "Solicitud enviada…", sin sesión, y envía el cuerpo correcto', async () => {
    const api = simularFetch({
      [RUTA_ASESOR]: { status: 201, body: { usuarioId: 'u-0002', mensaje: MENSAJE_OK } },
    })
    const user = userEvent.setup()
    render(<App />)
    await user.click(screen.getByRole('button', { name: 'Registrarme' }))
    await elegirAsesor(user)
    await rellenarAsesor(user, { numero: '  AB-12345  ', comercial: '  Camiones Ficticios  ' })
    await user.click(screen.getByRole('button', { name: 'Enviar solicitud' }))

    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent(MENSAJE_OK)
    expect(screen.getByLabelText('Correo electrónico')).toHaveValue(ASESOR.email)
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
    expect(screen.queryByRole('button', { name: 'Administrar usuarios' })).toBeNull()

    expect(api.llamadasA(RUTA_COMPRADOR)).toHaveLength(0)
    const llamadas = api.llamadasA(RUTA_ASESOR)
    expect(llamadas).toHaveLength(1)
    expect(llamadas[0].init?.method).toBe('POST')
    expect(cuerpo(api, RUTA_ASESOR)).toEqual({
      nombreCompleto: ASESOR.nombre,
      email: ASESOR.email,
      telefono: ASESOR.telefono,
      password: ASESOR.password,
      tipoDocumento: 'CC',
      numeroDocumento: 'AB-12345',
      nombreComercial: 'Camiones Ficticios',
    })
  })

  it('CA-043R-21 sin nombre comercial se envía null', async () => {
    const api = simularFetch({ [RUTA_ASESOR]: { status: 201, body: { usuarioId: 'u-3', mensaje: MENSAJE_OK } } })
    const user = userEvent.setup()
    const onRegistrado = vi.fn()
    render(<RegistroForm onRegistrado={onRegistrado} onIrALogin={vi.fn()} />)
    await elegirAsesor(user)
    await rellenarAsesor(user)
    await user.click(screen.getByRole('button', { name: 'Enviar solicitud' }))
    await waitFor(() => expect(onRegistrado).toHaveBeenCalled())
    expect(cuerpo(api, RUTA_ASESOR).nombreComercial).toBeNull()
  })

  it('CA-043R-22 con la petición en curso el botón queda deshabilitado y no se envía dos veces', async () => {
    let resolver: (r: Response) => void = () => {}
    const fn = vi.fn(
      () =>
        new Promise<Response>((r) => {
          resolver = r
        }),
    )
    vi.stubGlobal('fetch', fn)
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await elegirAsesor(user)
    await rellenarAsesor(user)
    await user.click(screen.getByRole('button', { name: 'Enviar solicitud' }))
    const boton = await screen.findByRole('button', { name: 'Enviando solicitud...' })
    expect(boton).toBeDisabled()
    await user.click(boton)
    expect(fn).toHaveBeenCalledTimes(1)
    resolver(
      new Response(JSON.stringify({ usuarioId: 'u-4', mensaje: MENSAJE_OK }), {
        status: 201,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    await waitFor(() => expect(screen.getByRole('button', { name: 'Enviar solicitud' })).toBeEnabled())
  })
})

describe('Registro de comprador sin cambios (no regresión P2)', () => {
  it('CA-N03-R06 el comprador sigue usando /registro/comprador con los 4 campos de P1', async () => {
    const api = simularFetch({
      [RUTA_COMPRADOR]: {
        status: 201,
        body: { usuarioId: 'u-5', mensaje: 'Cuenta creada. Ya puedes iniciar sesión.' },
      },
    })
    const user = userEvent.setup()
    const onRegistrado = vi.fn()
    render(<RegistroForm onRegistrado={onRegistrado} onIrALogin={vi.fn()} />)
    await user.type(screen.getByLabelText('Nombre completo'), 'Comprador Ficticio')
    await user.type(screen.getByLabelText('Correo electrónico'), 'comprador.ficticio@ejemplo.test')
    await user.type(screen.getByLabelText('Teléfono'), '+57 300 000 0000')
    await user.type(screen.getByLabelText('Contraseña'), 'Camion2026')
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))

    await waitFor(() =>
      expect(onRegistrado).toHaveBeenCalledWith(
        'Cuenta creada. Ya puedes iniciar sesión.',
        'comprador.ficticio@ejemplo.test',
      ),
    )
    expect(api.llamadasA(RUTA_ASESOR)).toHaveLength(0)
    expect(cuerpo(api, RUTA_COMPRADOR)).toEqual({
      nombreCompleto: 'Comprador Ficticio',
      email: 'comprador.ficticio@ejemplo.test',
      telefono: '+57 300 000 0000',
      password: 'Camion2026',
    })
  })

  it('cambiar de asesor a comprador limpia los errores de documento y valida solo los 4 campos', async () => {
    const api = simularFetch({
      [RUTA_COMPRADOR]: { status: 201, body: { usuarioId: 'u-6', mensaje: 'Cuenta creada. Ya puedes iniciar sesión.' } },
    })
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await elegirAsesor(user)
    await user.click(screen.getByRole('button', { name: 'Enviar solicitud' }))
    expect(errorDe('numeroDocumento')).not.toBeNull()
    await user.click(screen.getByLabelText('Soy comprador'))
    expect(errorDe('nombreCompleto')).toBeNull()
    await user.type(screen.getByLabelText('Nombre completo'), 'Comprador Ficticio')
    await user.type(screen.getByLabelText('Correo electrónico'), 'comprador.ficticio@ejemplo.test')
    await user.type(screen.getByLabelText('Teléfono'), '+57 300 000 0000')
    await user.type(screen.getByLabelText('Contraseña'), 'Camion2026')
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    await waitFor(() => expect(api.llamadasA(RUTA_COMPRADOR)).toHaveLength(1))
  })
})
