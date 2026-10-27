// Formulario de registro de comprador (CA-N03-20 a CA-N03-27).
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../src/App'
import { RegistroForm } from '../src/components/RegistroForm'
import { irACuenta, simularFetch } from './helpers'

const RUTA = '/api/auth/registro/comprador'

const VALIDOS = {
  nombre: 'Comprador Ficticio',
  email: 'comprador.ficticio@ejemplo.test',
  telefono: '+57 300 000 0000',
  password: 'Camion2026',
}

async function rellenar(user: ReturnType<typeof userEvent.setup>, datos = VALIDOS) {
  await user.type(screen.getByLabelText('Nombre completo'), datos.nombre)
  await user.type(screen.getByLabelText('Correo electrónico'), datos.email)
  await user.type(screen.getByLabelText('Teléfono'), datos.telefono)
  await user.type(screen.getByLabelText('Contraseña'), datos.password)
}

function errorDe(id: string) {
  return document.getElementById(`${id}-error`)
}

const ETIQUETAS = ['Nombre completo', 'Correo electrónico', 'Teléfono', 'Contraseña']

// P3: la app abre en el catálogo; estas pruebas usan la pantalla de cuenta.
beforeEach(irACuenta)

describe('RegistroForm', () => {
  it('CA-N03-20 las 4 reglas de contraseña se ven antes de enviar', () => {
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    const reglas = document.getElementById('password-reglas') as HTMLElement
    expect(reglas).toBeInTheDocument()
    const items = within(reglas).getAllByRole('listitem')
    expect(items).toHaveLength(4)
    expect(reglas).toHaveTextContent('Al menos 8 caracteres')
    expect(reglas).toHaveTextContent('Al menos una letra mayúscula (A-Z)')
    expect(reglas).toHaveTextContent('Al menos una letra minúscula (a-z)')
    expect(reglas).toHaveTextContent('Al menos un número (0-9)')
    for (const li of items) expect(li).toHaveTextContent('(pendiente)')
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('aria-describedby', 'password-reglas')
  })

  it('CA-N03-21 cada regla indica con texto si se cumple al escribir', async () => {
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await user.type(screen.getByLabelText('Contraseña'), 'Camion2026')
    const reglas = document.getElementById('password-reglas') as HTMLElement
    for (const li of within(reglas).getAllByRole('listitem')) {
      expect(li).toHaveTextContent('(cumplida)')
    }
  })

  it('CA-N03-22 envío vacío: no llama a la API y marca cada campo con aria-invalid', async () => {
    const api = simularFetch({})
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))

    for (const etiqueta of ETIQUETAS) {
      expect(screen.getByLabelText(etiqueta), etiqueta).toHaveAttribute('aria-invalid', 'true')
    }
    expect(errorDe('nombreCompleto')).toHaveTextContent(
      'Escribe tu nombre completo (2 a 150 caracteres).',
    )
    expect(errorDe('email')).toHaveTextContent('Escribe un correo válido.')
    expect(errorDe('telefono')).toHaveTextContent('Escribe un teléfono válido')
    expect(errorDe('password')).toHaveTextContent('Debe tener al menos 8 caracteres.')
    expect(api.fn).not.toHaveBeenCalled()
  })

  it('CA-N03-23 un 400 con errores coloca cada mensaje bajo su campo', async () => {
    simularFetch({
      [RUTA]: {
        status: 400,
        body: {
          error: 'Revisa los datos marcados.',
          errores: {
            nombreCompleto: ['Servidor: nombre no aceptado.'],
            email: ['Servidor: correo no aceptado.'],
            telefono: ['Servidor: teléfono no aceptado.'],
            password: ['Servidor: contraseña no aceptada.'],
          },
        },
      },
    })
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await rellenar(user)
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))

    await waitFor(() => expect(errorDe('email')).toHaveTextContent('Servidor: correo no aceptado.'))
    expect(errorDe('nombreCompleto')).toHaveTextContent('Servidor: nombre no aceptado.')
    expect(errorDe('telefono')).toHaveTextContent('Servidor: teléfono no aceptado.')
    expect(errorDe('password')).toHaveTextContent('Servidor: contraseña no aceptada.')
    for (const etiqueta of ETIQUETAS) {
      expect(screen.getByLabelText(etiqueta), etiqueta).toHaveAttribute('aria-invalid', 'true')
    }
    expect(screen.getByRole('alert')).toHaveTextContent('Revisa los datos marcados.')
  })

  it('CA-N03-24 un 409 muestra el mensaje bajo el correo y conserva los datos', async () => {
    simularFetch({ [RUTA]: { status: 409, body: { error: 'Este correo ya está registrado.' } } })
    const user = userEvent.setup()
    const onRegistrado = vi.fn()
    render(<RegistroForm onRegistrado={onRegistrado} onIrALogin={vi.fn()} />)
    await rellenar(user)
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))

    await waitFor(() =>
      expect(errorDe('email')).toHaveTextContent('Este correo ya está registrado.'),
    )
    expect(screen.getByLabelText('Correo electrónico')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText('Correo electrónico')).toHaveValue(VALIDOS.email)
    expect(screen.getByLabelText('Nombre completo')).toHaveValue(VALIDOS.nombre)
    expect(onRegistrado).not.toHaveBeenCalled()
  })

  it('CA-N03-25 un 201 pasa al login con el mensaje de cuenta creada', async () => {
    const api = simularFetch({
      [RUTA]: {
        status: 201,
        body: { usuarioId: 'u-0001', mensaje: 'Cuenta creada. Ya puedes iniciar sesión.' },
      },
    })
    const user = userEvent.setup()
    render(<App />)
    await user.click(screen.getByRole('button', { name: 'Registrarme' }))
    await rellenar(user)
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))

    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Cuenta creada. Ya puedes iniciar sesión.')
    expect(screen.getByLabelText('Correo electrónico')).toHaveValue(VALIDOS.email)

    const [llamada] = api.llamadasA(RUTA)
    expect(llamada.init?.method).toBe('POST')
    expect(JSON.parse(String(llamada.init?.body))).toEqual({
      nombreCompleto: VALIDOS.nombre,
      email: VALIDOS.email,
      telefono: VALIDOS.telefono,
      password: VALIDOS.password,
    })
  })

  it('CA-N03-26 con la petición en curso el botón está deshabilitado (una sola petición)', async () => {
    let liberar: (r: Response) => void = () => {}
    const fn = vi.fn(
      () =>
        new Promise<Response>((res) => {
          liberar = res
        }),
    )
    vi.stubGlobal('fetch', fn)
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await rellenar(user)
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    const boton = await screen.findByRole('button', { name: 'Creando cuenta...' })
    expect(boton).toBeDisabled()
    await user.click(boton)
    expect(fn).toHaveBeenCalledTimes(1)
    liberar(new Response(JSON.stringify({ usuarioId: 'u', mensaje: 'ok' }), { status: 201 }))
  })

  it('CA-N03-27 backend sin respuesta: mensaje de conexión y se conservan los datos', async () => {
    simularFetch({ [RUTA]: { red: true } })
    const user = userEvent.setup()
    render(<RegistroForm onRegistrado={vi.fn()} onIrALogin={vi.fn()} />)
    await rellenar(user)
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo conectar con el servidor.')
    expect(screen.getByLabelText('Nombre completo')).toHaveValue(VALIDOS.nombre)
  })
})
