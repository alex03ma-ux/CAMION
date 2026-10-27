// P4a: acceso al inventario según el rol (CA-UI-01, CA-UI-02, RN-SE-006, D-3) y
// fin de sesión con 401 (CA-SE006-05 en la interfaz).
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { cabecera, simularFetch } from './helpers'
import {
  abrirConSesion,
  abrirSinSesion,
  CAT,
  CAT_ADMIN,
  CATALOGOS,
  detalleCamion,
  item,
  LISTA,
  lista,
  ME,
  me,
} from './inventarioDatos'

const SESION_TERMINADA = 'Tu sesión terminó. Inicia sesión de nuevo.'
const SIN_PERMISO = 'No tienes permiso para realizar esta acción.'

describe('Enlace en "Mi cuenta" (CA-UI-01)', () => {
  it('asesor ve "Mi inventario" hacia #/inventario', async () => {
    simularFetch({ [ME]: me(['ASESOR']) })
    abrirConSesion('#/cuenta')
    const enlace = await screen.findByRole('link', { name: 'Mi inventario' })
    expect(enlace).toHaveAttribute('href', '#/inventario')
  })

  it('administrador ve "Inventario" hacia #/inventario', async () => {
    simularFetch({ [ME]: me(['ADMINISTRADOR']) })
    abrirConSesion('#/cuenta')
    const enlace = await screen.findByRole('link', { name: 'Inventario' })
    expect(enlace).toHaveAttribute('href', '#/inventario')
    expect(screen.queryByRole('link', { name: 'Mi inventario' })).toBeNull()
  })

  it('comprador no ve ningún enlace de inventario', async () => {
    simularFetch({ [ME]: me(['COMPRADOR']) })
    abrirConSesion('#/cuenta')
    await screen.findByText(/Bienvenido/)
    expect(screen.queryByRole('link', { name: /inventario/i })).toBeNull()
  })

  it('el asesor entra a su inventario desde el enlace y la lista lleva su token', async () => {
    const api = simularFetch({
      [ME]: me(['ASESOR']),
      [CATALOGOS]: { status: 200, body: CAT },
      [LISTA]: lista([item({ id: 9 })]),
    })
    const u = userEvent.setup()
    abrirConSesion('#/cuenta')
    await u.click(await screen.findByRole('link', { name: 'Mi inventario' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Mi inventario' })).toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'Chevrolet NPR 2020' })).toBeInTheDocument()
    expect(cabecera(api.llamadasA(LISTA)[0].init, 'Authorization')).toBe('Bearer token-de-prueba-ficticio')
    expect(cabecera(api.llamadasA(CATALOGOS)[0].init, 'Authorization')).toBe('Bearer token-de-prueba-ficticio')
  })
})

describe('Guardia de ruta (CA-UI-02)', () => {
  for (const hash of ['#/inventario', '#/inventario/nuevo', '#/inventario/5']) {
    it(`comprador en ${hash}: "No tienes permiso..." y ninguna llamada a /api/inventario`, async () => {
      const api = simularFetch({ [ME]: me(['COMPRADOR']) })
      abrirConSesion(hash)
      expect(await screen.findByText(SIN_PERMISO)).toBeInTheDocument()
      expect(api.llamadas.some((l) => l.url.startsWith('/api/inventario'))).toBe(false)
      expect(screen.queryByRole('form')).toBeNull()
      expect(screen.queryByLabelText('Categoría')).toBeNull()
    })

    it(`sin sesión en ${hash}: va a #/cuenta sin pedir datos del inventario`, async () => {
      const api = simularFetch({})
      abrirSinSesion(hash)
      await waitFor(() => expect(window.location.hash).toBe('#/cuenta'))
      expect(api.llamadas.some((l) => l.url.startsWith('/api/inventario'))).toBe(false)
      expect(await screen.findByRole('button', { name: /Iniciar sesión|Ingresar/ })).toBeInTheDocument()
    })
  }

  it('usuario sin rol: también ve "No tienes permiso..."', async () => {
    const api = simularFetch({ [ME]: me([]) })
    abrirConSesion('#/inventario')
    expect(await screen.findByText(SIN_PERMISO)).toBeInTheDocument()
    expect(api.llamadas.some((l) => l.url.startsWith('/api/inventario'))).toBe(false)
  })

  it('si la API responde 403 a los catálogos, se muestra el mensaje de permiso', async () => {
    simularFetch({
      [ME]: me(['ASESOR']),
      [CATALOGOS]: { status: 403 },
      [LISTA]: { status: 403 },
    })
    abrirConSesion('#/inventario')
    expect(await screen.findByText(SIN_PERMISO)).toBeInTheDocument()
  })
})

describe('401 termina la sesión (CA-SE006-05)', () => {
  it('401 en la lista', async () => {
    simularFetch({
      [ME]: me(['ASESOR']),
      [CATALOGOS]: { status: 200, body: CAT },
      [LISTA]: { status: 401 },
    })
    abrirConSesion('#/inventario')
    await waitFor(() => expect(window.location.hash).toBe('#/cuenta'))
    await waitFor(() => expect(screen.getByText(SESION_TERMINADA)).toBeInTheDocument())
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
  })

  it('401 en los catálogos del formulario', async () => {
    simularFetch({ [ME]: me(['ADMINISTRADOR']), [CATALOGOS]: { status: 401 } })
    abrirConSesion('#/inventario/nuevo')
    await waitFor(() => expect(window.location.hash).toBe('#/cuenta'))
    await waitFor(() => expect(screen.getByText(SESION_TERMINADA)).toBeInTheDocument())
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
  })

  it('401 al cargar el detalle de un vehículo', async () => {
    simularFetch({
      [ME]: me(['ASESOR']),
      [CATALOGOS]: { status: 200, body: CAT },
      '/api/inventario/vehiculos/9': { status: 401 },
    })
    abrirConSesion('#/inventario/9')
    await waitFor(() => expect(window.location.hash).toBe('#/cuenta'))
    await waitFor(() => expect(screen.getByText(SESION_TERMINADA)).toBeInTheDocument())
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
  })

  it('401 al guardar el formulario', async () => {
    simularFetch({
      [ME]: me(['ASESOR']),
      [CATALOGOS]: { status: 200, body: CAT_ADMIN },
      '/api/inventario/vehiculos/9': [{ status: 200, body: detalleCamion() }, { status: 401 }],
    })
    const u = userEvent.setup()
    abrirConSesion('#/inventario/9')
    await u.click(await screen.findByRole('button', { name: 'Guardar cambios' }))
    await waitFor(() => expect(window.location.hash).toBe('#/cuenta'))
    await waitFor(() => expect(screen.getByText(SESION_TERMINADA)).toBeInTheDocument())
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
  })
})

describe('Vehículo inexistente o ajeno (CA-SE006-01 en la interfaz)', () => {
  it('404 al abrir el detalle: "Vehículo no encontrado." sin formulario ni "Reintentar"', async () => {
    simularFetch({
      [ME]: me(['ASESOR']),
      [CATALOGOS]: { status: 200, body: CAT },
      '/api/inventario/vehiculos/77': { status: 404, body: { error: 'Vehículo no encontrado.' } },
    })
    abrirConSesion('#/inventario/77')
    expect(await screen.findByText('Vehículo no encontrado.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Guardar cambios' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Reintentar' })).toBeNull()
  })
})
