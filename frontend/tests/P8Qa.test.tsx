// P8 — Pruebas de QA: lo que faltaba en P8Reportes.test.tsx (CA-P8-52, 54, 60, 67).
// - El asesor no ve "Actividad" en el detalle de su vehículo y no se llama a la ruta del administrador.
// - "Mis indicadores" nunca envía asesorId ni otros filtros, aunque vengan en la URL.
// - El código de recuperación no queda en la URL, ni en el historial, ni en el almacenamiento, ni viaja en la ruta.
// Datos ficticios; fetch simulado.
import { screen, waitFor } from '@testing-library/react'
import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import App from '../src/App'
import { mesEnCurso } from '../src/api/reportes'
import { simularFetch } from './helpers'
import { abrirConSesion, CAT, CAT_ADMIN, CATALOGOS, detalleCamion, me, ME } from './inventarioDatos'
import { abrirApp } from './p4pDatos'

const DETALLE_9 = '/api/inventario/vehiculos/9'
const ACTIVIDAD_9 = '/api/admin/vehiculos/9/actividad?pagina=1&tamano=20'

describe('Actividad en el detalle del vehículo (CA-P8-60)', () => {
  it('el asesor dueño no ve la sección "Actividad" ni se llama a la ruta del administrador', async () => {
    const api = simularFetch({
      [ME]: me(['ASESOR']),
      [CATALOGOS]: { status: 200, body: CAT },
      [DETALLE_9]: { status: 200, body: detalleCamion() },
    })
    abrirConSesion('#/inventario/9')
    expect(await screen.findByText(/Precio anterior: \$ 120\.000\.000/)).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /^Actividad/ })).toBeNull()
    expect(screen.queryByRole('button', { name: /actividad/i })).toBeNull()
    expect(screen.queryByText('Registro de solo lectura de lo que se hizo y quién lo hizo.')).toBeNull()
    expect(api.llamadas.some((l) => l.url.includes('/actividad'))).toBe(false)
    expect(api.llamadas.some((l) => l.url.startsWith('/api/admin/'))).toBe(false)
  })

  it('el administrador sí la ve en el mismo vehículo y la carga solo al pulsar', async () => {
    const api = simularFetch({
      [ME]: me(['ADMINISTRADOR']),
      [CATALOGOS]: { status: 200, body: CAT_ADMIN },
      [DETALLE_9]: { status: 200, body: detalleCamion({ asesor: { id: 5, nombre: 'Asesor Uno Ficticio' } }) },
      [ACTIVIDAD_9]: { status: 200, body: { items: [], total: 0, pagina: 1, tamano: 20 } },
    })
    abrirConSesion('#/inventario/9')
    const boton = await screen.findByRole('button', { name: 'Ver actividad' })
    expect(api.llamadasA(ACTIVIDAD_9)).toHaveLength(0)
    await userEvent.setup().click(boton)
    await waitFor(() => expect(api.llamadasA(ACTIVIDAD_9)).toHaveLength(1))
  })
})

describe('Mis indicadores: alcance del asesor (CA-P8-52, 54)', () => {
  it('nunca envía asesorId ni otros filtros aunque vengan en la URL; no hay CSV', async () => {
    const mes = mesEnCurso()
    const ruta = `/api/asesor/indicadores?desde=${mes.desde}&hasta=${mes.hasta}`
    const { api } = abrirApp('#/mis-indicadores?asesorId=99&origen=CREDITO', ['ASESOR'], {
      [ruta]: {
        status: 200,
        body: {
          periodo: mes,
          vehiculosPorEstado: { ACTIVO: 1, RESERVADO: 0, INACTIVO: 0, VENDIDO: 0 },
          publicacionesVisiblesHoy: 1,
          contactosRecibidos: 0,
          contactosAbiertos: 0,
          contactosPorResultado: [],
          ventasVigentes: { cantidad: 0, valorCop: 0 },
        },
      },
    })
    // La pantalla se monta de nuevo al leer la URL: se espera a que el título quede estable.
    await waitFor(() => expect(screen.getByRole('heading', { level: 1, name: 'Mis indicadores' })).toBeInTheDocument())
    await screen.findByText('Vehículos por estado', { exact: false })
    await waitFor(() => expect(api.llamadasA(ruta).length).toBeGreaterThan(0))
    const indicadores = api.llamadas.filter((l) => l.url.includes('indicadores'))
    expect(indicadores.every((l) => !/asesorId|origen/.test(l.url))).toBe(true)
    expect(api.llamadas.some((l) => l.url.includes('/csv') || l.url.startsWith('/api/admin/'))).toBe(false)
    expect(screen.queryByRole('button', { name: /CSV|Descargar|Exportar/i })).toBeNull()
  })
})

describe('Código de recuperación fuera de la URL y del almacenamiento (CA-P8-67)', () => {
  const RUTA = '/api/auth/restablecer-contrasena'
  const CODIGO = 'QaCodigoFicticio_123-z'

  it('se quita con replaceState, no queda en el historial ni en localStorage/sessionStorage y solo viaja en el cuerpo', async () => {
    window.location.hash = `#/restablecer-contrasena?codigo=${CODIGO}`
    const largoHistorial = window.history.length
    const api = simularFetch({ [RUTA]: { status: 200, body: { mensaje: 'Listo. Tu contraseña cambió; inicia sesión con la nueva.' } } })
    render(<App />)
    await screen.findByRole('heading', { level: 1, name: 'Crear una contraseña nueva' })
    await waitFor(() => expect(window.location.hash).toBe('#/restablecer-contrasena'))
    expect(window.location.href).not.toContain(CODIGO)
    expect(window.history.length).toBe(largoHistorial) // reemplaza la entrada, no añade una con el código
    expect(JSON.stringify(window.history.state ?? null)).not.toContain(CODIGO)

    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Contraseña nueva'), 'Nueva1234')
    await user.type(screen.getByLabelText('Confirma la contraseña nueva'), 'Nueva1234')
    await user.click(screen.getByRole('button', { name: 'Guardar contraseña nueva' }))
    await screen.findByText('Listo. Tu contraseña cambió; inicia sesión con la nueva.')

    const llamadas = api.llamadasA(RUTA)
    expect(llamadas).toHaveLength(1)
    expect(llamadas[0].init?.method).toBe('POST')
    expect(api.llamadas.every((l) => !l.url.includes(CODIGO))).toBe(true)
    const encabezados = new Headers(llamadas[0].init?.headers)
    expect(encabezados.has('Authorization')).toBe(false) // ruta anónima: no envía token
    for (const almacen of [window.localStorage, window.sessionStorage]) {
      for (let i = 0; i < almacen.length; i++) {
        const clave = almacen.key(i) as string
        expect(`${clave}=${almacen.getItem(clave)}`).not.toContain(CODIGO)
      }
    }
    expect(document.body.innerHTML).not.toContain(CODIGO)
  })
})
