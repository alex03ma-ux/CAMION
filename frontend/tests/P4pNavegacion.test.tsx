// P4p — Navegación del administrador en "Mi cuenta" (Usuarios, Planes, Suscripciones, Catálogos)
// y protección por rol en la interfaz (la API también lo protege).
import { render, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import App from '../src/App'
import { simularFetch } from './helpers'
import { leerRuta } from '../src/rutas'
import { abrirApp, CATALOGOS_INVENTARIO, PLAN_BASICO, paginaDe, RUTA_CATALOGOS_INV, RUTA_PLANES, RUTA_PLANES_ACTIVOS, RUTA_SUSCRIPCIONES } from './p4pDatos'

const LISTA_USUARIOS = '/api/admin/usuarios?estado=PENDIENTE&pagina=1&tamano=20'

describe('Rutas de las secciones del administrador', () => {
  it('#/cuenta/<seccion> se reconoce; lo desconocido sigue siendo el inicio', () => {
    expect(leerRuta('#/cuenta/planes')).toEqual({ nombre: 'cuenta', registro: false, seccion: 'planes' })
    expect(leerRuta('#/cuenta/suscripciones')).toEqual({ nombre: 'cuenta', registro: false, seccion: 'suscripciones' })
    expect(leerRuta('#/cuenta/catalogos/')).toEqual({ nombre: 'cuenta', registro: false, seccion: 'catalogos' })
    expect(leerRuta('#/cuenta/usuarios')).toEqual({ nombre: 'cuenta', registro: false, seccion: 'usuarios' })
    expect(leerRuta('#/cuenta')).toEqual({ nombre: 'cuenta', registro: false })
    expect(leerRuta('#/cuenta/otra')).toEqual({ nombre: 'inicio' })
  })
})

describe('Acceso por rol según /api/auth/me', () => {
  it('el administrador ve en su perfil los enlaces a Planes, Suscripciones y Catálogos', async () => {
    abrirApp('#/cuenta', ['ADMINISTRADOR'])
    const nav = await screen.findByRole('navigation', { name: 'Más administración' })
    expect(within(nav).getByRole('link', { name: 'Planes' })).toHaveAttribute('href', '#/cuenta/planes')
    expect(within(nav).getByRole('link', { name: 'Suscripciones' })).toHaveAttribute('href', '#/cuenta/suscripciones')
    expect(within(nav).getByRole('link', { name: 'Catálogos' })).toHaveAttribute('href', '#/cuenta/catalogos')
    expect(screen.getByRole('button', { name: 'Administrar usuarios' })).toBeInTheDocument()
  })

  it.each([['COMPRADOR'], ['ASESOR']])('%s no ve los enlaces de administración', async (rol) => {
    abrirApp('#/cuenta', [rol], { '/api/asesor/suscripcion': { status: 200, body: { suscripcion: null, mensaje: 'x' } } })
    expect(await screen.findByRole('heading', { name: /Bienvenido/ })).toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Más administración' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Planes' })).not.toBeInTheDocument()
  })

  it.each([['#/cuenta/planes'], ['#/cuenta/suscripciones'], ['#/cuenta/catalogos'], ['#/cuenta/usuarios']])(
    'un asesor que entra en %s ve "No tienes permiso" y no se pide nada a /api/admin',
    async (hash) => {
      const { api } = abrirApp(hash, ['ASESOR'])
      expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'Volver a mi cuenta' })).toHaveAttribute('href', '#/cuenta')
      expect(api.llamadas.some((l) => l.url.startsWith('/api/admin/'))).toBe(false)
    },
  )

  it('el administrador navega entre las secciones con la barra "Administración"', async () => {
    const { user, api } = abrirApp('#/cuenta/planes', ['ADMINISTRADOR'], {
      [RUTA_PLANES]: { status: 200, body: { items: [PLAN_BASICO] } },
      [RUTA_PLANES_ACTIVOS]: { status: 200, body: { items: [PLAN_BASICO] } },
      [RUTA_CATALOGOS_INV]: CATALOGOS_INVENTARIO,
      [RUTA_SUSCRIPCIONES]: paginaDe([]),
      '/api/admin/catalogos/marcas': { status: 200, body: { items: [] } },
      [LISTA_USUARIOS]: paginaDe([]),
    })
    expect(await screen.findByRole('heading', { level: 2, name: 'Planes' })).toBeInTheDocument()
    const nav = screen.getByRole('navigation', { name: 'Administración' })
    expect(within(nav).getByRole('link', { name: 'Planes' })).toHaveAttribute('aria-current', 'page')

    await user.click(within(nav).getByRole('link', { name: 'Suscripciones' }))
    expect(await screen.findByRole('heading', { level: 2, name: 'Suscripciones' })).toBeInTheDocument()
    await waitFor(() => expect(api.llamadasA(RUTA_SUSCRIPCIONES)).toHaveLength(1))

    await user.click(within(screen.getByRole('navigation', { name: 'Administración' })).getByRole('link', { name: 'Catálogos' }))
    expect(await screen.findByRole('heading', { level: 2, name: 'Catálogos' })).toBeInTheDocument()

    await user.click(within(screen.getByRole('navigation', { name: 'Administración' })).getByRole('link', { name: 'Usuarios' }))
    expect(await screen.findByRole('heading', { name: 'Administración de usuarios' })).toBeInTheDocument()

    await user.click(within(screen.getByRole('navigation', { name: 'Administración' })).getByRole('link', { name: 'Mi perfil' }))
    expect(await screen.findByRole('heading', { name: /Bienvenido/ })).toBeInTheDocument()
  })

  it('desde "Administrar usuarios" también se llega a Planes y "Mi perfil" vuelve al perfil', async () => {
    const { user } = abrirApp('#/cuenta', ['ADMINISTRADOR'], {
      [LISTA_USUARIOS]: paginaDe([]),
      [RUTA_PLANES]: { status: 200, body: { items: [] } },
    })
    await user.click(await screen.findByRole('button', { name: 'Administrar usuarios' }))
    const nav = await screen.findByRole('navigation', { name: 'Administración' })
    expect(within(nav).getByRole('link', { name: 'Usuarios' })).toHaveAttribute('aria-current', 'page')
    await user.click(within(nav).getByRole('link', { name: 'Planes' }))
    expect(await screen.findByRole('heading', { level: 2, name: 'Planes' })).toBeInTheDocument()
    await user.click(within(screen.getByRole('navigation', { name: 'Administración' })).getByRole('link', { name: 'Mi perfil' }))
    expect(await screen.findByRole('heading', { name: /Bienvenido/ })).toBeInTheDocument()
  })

  it('sin sesión, #/cuenta/planes muestra el inicio de sesión y no llama a /api/admin', async () => {
    window.location.hash = '#/cuenta/planes'
    const api = simularFetch({})
    render(<App />)
    expect(await screen.findByRole('button', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(api.llamadas.some((l) => l.url.startsWith('/api/admin/'))).toBe(false)
  })
})
