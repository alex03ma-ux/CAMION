// Ajustes visuales, fase 2: menú plegable del encabezado (tablet y celular, por debajo de 68.75rem).
// jsdom no aplica el CSS: aquí se prueban el estado, la accesibilidad y el teclado; que la navegación se oculte
// o se vea según el ancho se verifica a mano en el navegador.
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { abrirApp, respuestaFiltros, RUTA_FILTROS, RUTA_INICIO } from './catalogoDatos'
import { guardarSesionFicticia, irA, PERFIL_FICTICIO, simularFetch, ubicacionActual } from './helpers'

const INICIO_VACIO = { status: 200, body: { destacados: [], recientes: [] } }

function botonMenu() {
  return within(screen.getByRole('banner')).getByRole('button', { name: 'Menú' })
}

function navPrincipal() {
  return screen.getByRole('navigation', { name: 'Principal' })
}

async function abrirInicio() {
  simularFetch({ [RUTA_FILTROS]: respuestaFiltros(), [RUTA_INICIO]: INICIO_VACIO })
  abrirApp('/')
  await screen.findByText('Pronto tendremos vehículos publicados.')
}

describe('Menú del encabezado (tablet y celular)', () => {
  it('empieza cerrado y su aria-controls apunta a la navegación principal', async () => {
    await abrirInicio()
    const boton = botonMenu()
    expect(boton).toHaveAttribute('aria-expanded', 'false')
    expect(boton).toHaveAttribute('type', 'button')
    const nav = navPrincipal()
    expect(boton.getAttribute('aria-controls')).toBe(nav.id)
    expect(nav).toHaveAttribute('data-abierto', 'false')
  })

  it('con el teclado: Enter lo abre, Tab llega a "Inicio" y Esc lo cierra devolviendo el foco al botón', async () => {
    await abrirInicio()
    const user = userEvent.setup()
    const boton = botonMenu()
    boton.focus()
    await user.keyboard('{Enter}')
    expect(boton).toHaveAttribute('aria-expanded', 'true')
    expect(navPrincipal()).toHaveAttribute('data-abierto', 'true')
    await user.tab()
    expect(within(navPrincipal()).getByRole('link', { name: 'Inicio' })).toHaveFocus()
    await user.keyboard('{Escape}')
    expect(boton).toHaveAttribute('aria-expanded', 'false')
    expect(boton).toHaveFocus()
  })

  it('Espacio también lo abre y lo cierra; Esc con el foco en el botón lo cierra', async () => {
    await abrirInicio()
    const user = userEvent.setup()
    const boton = botonMenu()
    boton.focus()
    await user.keyboard(' ')
    expect(boton).toHaveAttribute('aria-expanded', 'true')
    await user.keyboard(' ')
    expect(boton).toHaveAttribute('aria-expanded', 'false')
    await user.keyboard(' ')
    await user.keyboard('{Escape}')
    expect(boton).toHaveAttribute('aria-expanded', 'false')
    expect(boton).toHaveFocus()
  })

  it('tiene todas las opciones de siempre (Inicio, las 4 categorías, Registrarme e Ingresar) con sus rutas', async () => {
    await abrirInicio()
    const nav = navPrincipal()
    expect(within(nav).getByRole('link', { name: 'Inicio' })).toHaveAttribute('href', '/')
    for (const [nombre, ruta] of [
      ['Carros', '/carros'],
      ['Camiones', '/camiones'],
      ['Carrocerías', '/carrocerias'],
      ['Maquinaria', '/maquinaria'],
    ] as const) {
      expect(within(nav).getByRole('link', { name: nombre })).toHaveAttribute('href', ruta)
    }
    expect(within(nav).getByRole('link', { name: 'Registrarme' })).toHaveAttribute('href', '/cuenta/registro')
    expect(within(nav).getByRole('link', { name: 'Ingresar' })).toHaveAttribute('href', '/cuenta')
  })

  it('con sesión, el menú tiene "Mi cuenta" en lugar de Registrarme e Ingresar', async () => {
    guardarSesionFicticia(Date.now() + 2 * 60 * 60 * 1000)
    simularFetch({
      [RUTA_FILTROS]: respuestaFiltros(),
      [RUTA_INICIO]: INICIO_VACIO,
      '/api/auth/me': { status: 200, body: PERFIL_FICTICIO },
    })
    abrirApp('/')
    await screen.findByText('Pronto tendremos vehículos publicados.')
    const nav = navPrincipal()
    expect(within(nav).getByRole('link', { name: 'Mi cuenta' })).toHaveAttribute('href', '/cuenta')
    expect(within(nav).queryByRole('link', { name: 'Ingresar' })).not.toBeInTheDocument()
  })

  it('al pulsar una categoría se cierra y navega a la ruta de siempre', async () => {
    await abrirInicio()
    const user = userEvent.setup()
    await user.click(botonMenu())
    expect(botonMenu()).toHaveAttribute('aria-expanded', 'true')
    await user.click(within(navPrincipal()).getByRole('link', { name: 'Camiones' }))
    await waitFor(() => expect(ubicacionActual()).toBe('/camiones'))
    expect(botonMenu()).toHaveAttribute('aria-expanded', 'false')
  })

  it('se cierra al cambiar de ruta (atrás/adelante o enlace fuera del menú)', async () => {
    await abrirInicio()
    const user = userEvent.setup()
    await user.click(botonMenu())
    expect(botonMenu()).toHaveAttribute('aria-expanded', 'true')
    irA('/vehiculos')
    await waitFor(() => expect(botonMenu()).toHaveAttribute('aria-expanded', 'false'))
  })

  it('fuera del inicio, el buscador del encabezado queda fuera del menú', async () => {
    simularFetch({ [RUTA_FILTROS]: respuestaFiltros() })
    abrirApp('/cuenta')
    const buscador = await within(screen.getByRole('banner')).findByRole('search')
    expect(navPrincipal()).not.toContainElement(buscador)
  })
})
