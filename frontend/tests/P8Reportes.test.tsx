// P8 — Reportes, indicadores, Mis indicadores, actividad y recuperación asistida (CA-P8-01 a 75; D-68 a D-73).
// Positivos, negativos y permisos. La API protege cada ruta; aquí se prueba lo que muestra y pide el frontend.
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import App from '../src/App'
import { nombreDeDisposicion } from '../src/api/http'
import { codigoDelHash, enlaceCorreoUsuario, enlaceWhatsappUsuario } from '../src/api/recuperacion'
import {
  consultaReporte,
  mesEnCurso,
  textoDias,
  textoPorcentaje,
  validarPeriodo,
} from '../src/api/reportes'
import { SeccionActividad } from '../src/components/Actividad'
import { AdminIndicadores, textoDiferencia } from '../src/components/AdminIndicadores'
import { AdminReportes } from '../src/components/AdminReportes'
import { AdminUsuarios } from '../src/components/AdminUsuarios'
import { leerRuta } from '../src/rutas'
import { cabecera, simularFetch, TOKEN_FICTICIO, type RespuestaSimulada } from './helpers'
import { abrirApp, CATALOGOS_INVENTARIO, cuerpoDe, RUTA_CATALOGOS_INV, type Rutas } from './p4pDatos'

const MES = mesEnCurso()
const PERIODO = `desde=${MES.desde}&hasta=${MES.hasta}`
const rutaReporte = (tipo: string, extra = '') => `/api/admin/reportes/${tipo}?${PERIODO}${extra}&pagina=1&tamano=20`
const RUTA_CSV_VEHICULOS = `/api/admin/reportes/vehiculos/csv?${PERIODO}`

function filas(items: unknown[], total = items.length) {
  return { items, total, pagina: 1, tamano: 20 }
}

const VEHICULOS = {
  periodo: MES,
  inventarioPorEstado: { ACTIVO: 4, RESERVADO: 1, INACTIVO: 0, VENDIDO: 2 },
  inventarioPorCategoria: [{ id: 2, nombre: 'Camión', total: 6 }],
  registradosEnPeriodo: 1,
  publicacionesPorEstado: { BORRADOR: 0, PUBLICADA: 4, OCULTA: 1 },
  publicacionesEliminadas: 1,
  publicacionesVisiblesHoy: 3,
  publicacionesDestacadasHoy: 1,
  masContactados: [{ vehiculoId: 15, titulo: 'Chevrolet NPR 2020', contactos: 5 }],
  filas: filas([
    {
      vehiculoId: 15,
      titulo: 'Chevrolet NPR 2020',
      categoria: 'Camión',
      estado: 'ACTIVO',
      asesor: { id: 7, nombre: 'Asesor Uno' },
      municipio: 'Bogotá',
      departamento: 'Cundinamarca',
      registradoEn: `${MES.desde}T15:00:00Z`,
      publicacion: { id: 3, estado: 'PUBLICADA' },
      contactosEnPeriodo: 5,
    },
  ]),
}

const VACIO_VEHICULOS = { ...VEHICULOS, masContactados: [], registradosEnPeriodo: 0, filas: filas([]) }

function renderReportes(rutas: Rutas, consulta?: string) {
  const api = simularFetch({ [RUTA_CATALOGOS_INV]: CATALOGOS_INVENTARIO, '/api/admin/planes': { status: 200, body: { items: [] } }, ...rutas })
  const onSesionTerminada = vi.fn()
  render(<AdminReportes token={TOKEN_FICTICIO} consulta={consulta} onSesionTerminada={onSesionTerminada} />)
  return { api, onSesionTerminada, user: userEvent.setup() }
}

// ---------------------------------------------------------------- Funciones puras

describe('Rutas y filtros de P8 (CA-P8-02, 04)', () => {
  it('lee las rutas nuevas; el código de recuperación no viaja en la ruta', () => {
    expect(leerRuta('#/cuenta/reportes?tipo=ventas')).toEqual({ nombre: 'cuenta', registro: false, seccion: 'reportes', consulta: 'tipo=ventas' })
    expect(leerRuta('#/cuenta/indicadores')).toEqual({ nombre: 'cuenta', registro: false, seccion: 'indicadores' })
    expect(leerRuta('#/mis-indicadores')).toEqual({ nombre: 'misIndicadores' })
    expect(leerRuta('#/restablecer-contrasena?codigo=abc')).toEqual({ nombre: 'restablecerContrasena' })
  })

  it('el período por defecto es el mes en curso, también en febrero bisiesto', () => {
    expect(mesEnCurso('2026-11-12')).toEqual({ desde: '2026-11-01', hasta: '2026-11-30' })
    expect(mesEnCurso('2028-02-03')).toEqual({ desde: '2028-02-01', hasta: '2028-02-29' })
  })

  it('valida fechas, orden y máximo 366 días con los mensajes de la API', () => {
    expect(validarPeriodo({ desde: '2026-01-01', hasta: '2026-12-31' })).toEqual({})
    expect(validarPeriodo({ desde: '', hasta: '' })).toEqual({})
    expect(validarPeriodo({ desde: '2026-02-30', hasta: '' }).desde).toEqual(['Escribe una fecha válida (aaaa-mm-dd).'])
    expect(validarPeriodo({ desde: '2026-11-10', hasta: '2026-11-01' }).hasta).toEqual(['La fecha inicial no puede ser posterior a la final.'])
    expect(validarPeriodo({ desde: '2025-01-01', hasta: '2026-01-02' }).hasta).toEqual(['El período no puede superar 366 días.'])
  })

  it('arma la consulta sin valores vacíos y formatea "Sin datos" (CA-P8-38)', () => {
    expect(consultaReporte({ desde: '2026-11-01', hasta: '', asesorId: '7' }, 2)).toBe('desde=2026-11-01&asesorId=7&pagina=2&tamano=20')
    expect(textoPorcentaje(null)).toBe('Sin datos')
    expect(textoPorcentaje(25)).toBe('25,0 %')
    expect(textoDias(null)).toBe('Sin datos')
    expect(textoDiferencia({ actual: 8, anterior: 5, diferencia: 3 }, 'cantidad')).toBe('+3 frente al período anterior')
    expect(textoDiferencia({ actual: 1, anterior: null, diferencia: null }, 'cantidad')).toBe('Sin datos para comparar')
    expect(textoDiferencia({ actual: 0, anterior: 300000, diferencia: -300000 }, 'pesos')).toBe('-$ 300.000 frente al período anterior')
  })

  it('limpia el nombre del archivo del Content-Disposition', () => {
    expect(nombreDeDisposicion('attachment; filename="reporte-ventas-2026-11-01_2026-11-30.csv"')).toBe(
      'reporte-ventas-2026-11-01_2026-11-30.csv',
    )
    expect(nombreDeDisposicion('attachment; filename="../../x y.csv"')).toBe(null)
    expect(nombreDeDisposicion(null)).toBe(null)
  })
})

// ---------------------------------------------------------------- Reportes (CA-P8-01 a 33)

describe('Reportes del administrador', () => {
  it('abre en "Reportes" con el mes en curso, cifras y filas, y Reportes marcado en la navegación', async () => {
    const { api } = abrirApp('#/cuenta/reportes', ['ADMINISTRADOR'], {
      [RUTA_CATALOGOS_INV]: CATALOGOS_INVENTARIO,
      [rutaReporte('vehiculos')]: { status: 200, body: VEHICULOS },
    })
    expect(await screen.findByText(`Del ${MES.desde} al ${MES.hasta}`, { exact: false })).toBeInTheDocument()
    const nav = screen.getByRole('navigation', { name: 'Administración' })
    expect(within(nav).getByRole('link', { name: 'Reportes' })).toHaveAttribute('aria-current', 'page')
    expect(within(screen.getByRole('navigation', { name: 'Tipos de reporte' })).getAllByRole('link')).toHaveLength(5)
    const estado = screen.getByRole('table', { name: 'Inventario actual por estado' })
    expect(within(estado).getByRole('row', { name: /Reservado 1/ })).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: 'Chevrolet NPR 2020' })[0]).toHaveAttribute('href', '#/inventario/15')
    expect(api.llamadasA(rutaReporte('vehiculos'))).toHaveLength(1)
    expect(cabecera(api.llamadasA(rutaReporte('vehiculos'))[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
  })

  it('aplica los filtros propios a la vez (asesor y categoría)', async () => {
    const conFiltros = rutaReporte('vehiculos', '&asesorId=7&categoriaId=2')
    const { api, user } = renderReportes({
      [rutaReporte('vehiculos')]: { status: 200, body: VEHICULOS },
      [conFiltros]: { status: 200, body: VEHICULOS },
    })
    await screen.findByRole('table', { name: 'Inventario actual por estado' })
    await screen.findByRole('option', { name: 'Asesor Uno' })
    await user.selectOptions(screen.getByLabelText('Asesor'), '7')
    await user.selectOptions(screen.getByLabelText('Categoría'), '2')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => expect(api.llamadasA(conFiltros)).toHaveLength(1))
  })

  it('valida el período en cliente sin llamar a la API', async () => {
    const { api, user } = renderReportes({ [rutaReporte('vehiculos')]: { status: 200, body: VEHICULOS } })
    await screen.findByRole('table', { name: 'Inventario actual por estado' })
    const desde = screen.getByLabelText('Desde')
    await user.clear(desde)
    await user.type(desde, '2026-12-31')
    const hasta = screen.getByLabelText('Hasta')
    await user.clear(hasta)
    await user.type(hasta, '2026-01-01')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(await screen.findByText('La fecha inicial no puede ser posterior a la final.')).toBeInTheDocument()
    expect(api.llamadas.filter((l) => l.url.startsWith('/api/admin/reportes/'))).toHaveLength(1)
  })

  it('muestra el 400 del servidor junto al filtro', async () => {
    renderReportes({
      [rutaReporte('vehiculos')]: {
        status: 400,
        body: { error: 'Revisa los filtros.', errores: { asesorId: ['Asesor no válido.'] } },
      },
    })
    expect(await screen.findByText('Asesor no válido.')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Revisa los filtros.')
    expect(screen.getByLabelText('Asesor')).toHaveAttribute('aria-invalid', 'true')
  })

  it('sin datos: "No hay datos para estos filtros." con "Limpiar filtros" (CA-P8-05)', async () => {
    renderReportes({ [rutaReporte('vehiculos')]: { status: 200, body: VACIO_VEHICULOS } })
    expect(await screen.findByText('No hay datos para estos filtros.')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'Limpiar filtros' }).length).toBeGreaterThan(0)
  })

  it('suscripciones: aviso visible de pagos registrados a mano (CA-P8-33)', async () => {
    renderReportes({ [rutaReporte('suscripciones')]: { status: 200, body: { periodo: MES, filas: filas([]) } } }, 'tipo=suscripciones')
    expect(
      await screen.findByText('Pagos registrados a mano por la administración. No es contabilidad ni facturación.'),
    ).toBeInTheDocument()
  })

  it('solicitudes: traduce origen y resultado a los nombres de la bandeja', async () => {
    renderReportes(
      {
        [rutaReporte('solicitudes')]: {
          status: 200,
          body: {
            periodo: MES,
            recibidas: 2,
            porOrigen: [{ origen: 'CONTACTO_VEHICULO', recibidas: 2, abiertas: 1, cerradas: 1 }],
            cerradasPorResultado: [{ origen: 'CONTACTO_VEHICULO', resultado: 'VENTA_CONCRETADA', total: 1 }],
            tiempoMedioCierre: [{ origen: 'CONTACTO_VEHICULO', cerradas: 1, dias: null }],
            contactosPorResponsable: { administracion: 1, asesores: [], reasignadosAMano: 0, pasadosAAdministracion: 1 },
            filas: filas([]),
          },
        },
      },
      'tipo=solicitudes',
    )
    const tabla = await screen.findByRole('table', { name: 'Cerradas en el período por resultado' })
    expect(within(tabla).getByText('Se concretó la venta')).toBeInTheDocument()
    expect(within(screen.getByRole('table', { name: 'Tiempo medio hasta el primer cierre' })).getByText('Sin datos')).toBeInTheDocument()
  })

  it('"Descargar CSV" usa fetch con el token en el encabezado (nunca en la URL) y guarda un blob', async () => {
    const { api, user } = renderReportes({ [rutaReporte('vehiculos')]: { status: 200, body: VEHICULOS } })
    const original = api.fn
    const csv: { url: string; init?: RequestInit }[] = []
    vi.stubGlobal('fetch', async (url: string, init?: RequestInit) => {
      if (url.includes('/csv')) {
        csv.push({ url, init })
        return new Response('﻿Concepto;Valor\r\n', {
          status: 200,
          headers: {
            'Content-Type': 'text/csv; charset=utf-8',
            'Content-Disposition': 'attachment; filename="reporte-vehiculos-2026-11-01_2026-11-30.csv"',
          },
        })
      }
      return original(url, init)
    })
    const clic = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    await screen.findByRole('table', { name: 'Inventario actual por estado' })
    await user.click(screen.getByRole('button', { name: 'Descargar CSV' }))
    expect(await screen.findByText('Se descargó el archivo reporte-vehiculos-2026-11-01_2026-11-30.csv.')).toBeInTheDocument()
    expect(csv).toHaveLength(1)
    expect(csv[0].url).toBe(RUTA_CSV_VEHICULOS)
    expect(csv[0].url).not.toContain(TOKEN_FICTICIO)
    expect(cabecera(csv[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
    expect(URL.createObjectURL).toHaveBeenCalledTimes(1)
    expect(clic).toHaveBeenCalledTimes(1)
    clic.mockRestore()
  })

  it('CSV con demasiadas filas: muestra el error y no guarda archivo (CA-P8-47)', async () => {
    const mensaje = 'El reporte tiene demasiadas filas. Acorta el período o añade filtros.'
    const { user } = renderReportes({
      [rutaReporte('vehiculos')]: { status: 200, body: VEHICULOS },
      [RUTA_CSV_VEHICULOS]: { status: 400, body: { error: mensaje } },
    })
    await screen.findByRole('table', { name: 'Inventario actual por estado' })
    await user.click(screen.getByRole('button', { name: 'Descargar CSV' }))
    expect(await screen.findByText(mensaje)).toBeInTheDocument()
    expect(URL.createObjectURL).not.toHaveBeenCalled()
  })

  it('401 termina la sesión', async () => {
    const { onSesionTerminada } = renderReportes({ [rutaReporte('vehiculos')]: { status: 401 } })
    await waitFor(() => expect(onSesionTerminada).toHaveBeenCalled())
  })

  it('403 de la API muestra el mensaje de permiso', async () => {
    renderReportes({ [rutaReporte('vehiculos')]: { status: 403, body: { error: 'No tienes permiso para realizar esta acción.' } } })
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
  })

  it.each([['COMPRADOR'], ['ASESOR']])('%s no ve Reportes ni Indicadores y no se llama a la API (CA-P8-06)', async (rol) => {
    const { api } = abrirApp('#/cuenta/reportes', [rol], {
      '/api/asesor/suscripcion': { status: 200, body: { suscripcion: null, mensaje: 'x' } },
    })
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Administración' })).not.toBeInTheDocument()
    expect(api.llamadas.some((l) => l.url.startsWith('/api/admin/'))).toBe(false)
  })
})

// ---------------------------------------------------------------- Indicadores (CA-P8-34 a 41)

const INDICADORES = {
  periodo: MES,
  periodoAnterior: { desde: '2026-10-02', hasta: '2026-10-31' },
  indicadores: {
    contactosRecibidos: { actual: 8, anterior: 5, diferencia: 3 },
    porcentajeVentaConcretada: { actual: null, anterior: null, diferencia: null },
    ventasVigentes: { actual: 3, anterior: 1, diferencia: 2 },
    valorVendidoCop: { actual: 285000000, anterior: 95000000, diferencia: 190000000 },
    solicitudesAdministracion: { actual: 4, anterior: 2, diferencia: 2 },
    creditosRemitidos: { actual: 1, anterior: 0, diferencia: 1 },
    ingresosPlanesCop: { actual: 300000, anterior: 0, diferencia: 300000 },
    tiempoMedioCierreDias: { actual: 2.5, anterior: null, diferencia: null },
  },
  solicitudesAdministracionPorOrigen: [{ origen: 'VENDE_TU_CAMION', actual: 1, anterior: 0 }],
  publicacionesVisiblesHoy: 3,
  graficos: {
    ventasPorMes: [{ mes: '2026-10', cantidad: 1, valorCop: 95000000 }],
    ventasPorOrigen: [{ origen: 'PORTAL', cantidad: 2, valorCop: 190000000 }],
    solicitudesPorOrigen: [{ origen: 'CONTACTO_VEHICULO', total: 8 }],
    contactosPorResultado: [{ resultado: 'VENTA_CONCRETADA', total: 2 }],
  },
}
const RUTA_INDICADORES = `/api/admin/indicadores?${PERIODO}`

describe('Indicadores del administrador', () => {
  it('valores en pesos, comparación, "Sin datos" y 4 gráficos con su tabla equivalente', async () => {
    simularFetch({ [RUTA_INDICADORES]: { status: 200, body: INDICADORES } })
    render(<AdminIndicadores token={TOKEN_FICTICIO} onSesionTerminada={vi.fn()} />)
    expect(await screen.findByText('$ 285.000.000')).toBeInTheDocument()
    expect(screen.getByText('+$ 190.000.000 frente al período anterior')).toBeInTheDocument()
    expect(screen.getByText('+3 frente al período anterior')).toBeInTheDocument()
    expect(screen.getAllByText('Sin datos').length).toBeGreaterThan(0)
    expect(screen.getAllByRole('figure')).toHaveLength(4)
    const tabla = screen.getByRole('table', { name: 'Datos del gráfico: Contactos cerrados por resultado' })
    expect(within(tabla).getByText('Se concretó la venta')).toBeInTheDocument()
    expect(screen.queryByText(/visitas/i)).not.toBeInTheDocument()
  })

  it('el panel Inicio ofrece "Ver indicadores" (CA-P8-39)', async () => {
    abrirApp('#/cuenta/inicio', ['ADMINISTRADOR'], {
      '/api/admin/panel': { status: 200, body: { solicitudes: [], vehiculosPorEstado: {} } },
    })
    expect(await screen.findByRole('link', { name: 'Ver indicadores' })).toHaveAttribute('href', '#/cuenta/indicadores')
  })
})

// ---------------------------------------------------------------- Mis indicadores (CA-P8-51 a 55)

describe('Mis indicadores del asesor', () => {
  const ruta = `/api/asesor/indicadores?${PERIODO}`
  it('muestra solo lo suyo, sin exportar', async () => {
    const { api } = abrirApp('#/mis-indicadores', ['ASESOR'], {
      [ruta]: {
        status: 200,
        body: {
          periodo: MES,
          vehiculosPorEstado: { ACTIVO: 2, RESERVADO: 0, INACTIVO: 0, VENDIDO: 1 },
          publicacionesVisiblesHoy: 2,
          contactosRecibidos: 4,
          contactosAbiertos: 2,
          contactosPorResultado: [{ resultado: 'VENTA_CONCRETADA', total: 1 }],
          ventasVigentes: { cantidad: 1, valorCop: 95000000 },
        },
      },
    })
    expect(await screen.findByText('$ 95.000.000')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Mis indicadores' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /CSV/ })).not.toBeInTheDocument()
    expect(api.llamadas.some((l) => l.url.startsWith('/api/admin/'))).toBe(false)
  })

  it('un comprador ve "No tienes permiso" sin llamar a la API', async () => {
    const { api } = abrirApp('#/mis-indicadores', ['COMPRADOR'])
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
    expect(api.llamadasA(ruta)).toHaveLength(0)
  })

  it('el perfil del asesor ofrece "Mis indicadores"; el del comprador no', async () => {
    abrirApp('#/cuenta', ['ASESOR'], { '/api/asesor/suscripcion': { status: 200, body: { suscripcion: null, mensaje: 'x' } } })
    expect(await screen.findByRole('link', { name: 'Mis indicadores' })).toHaveAttribute('href', '#/mis-indicadores')
  })

  it('el perfil del comprador no ofrece "Mis indicadores"', async () => {
    abrirApp('#/cuenta', ['COMPRADOR'])
    expect(await screen.findByRole('heading', { name: /Bienvenido/ })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Mis indicadores' })).not.toBeInTheDocument()
  })
})

// ---------------------------------------------------------------- Actividad (CA-P8-56 a 62)

describe('Actividad de solo lectura', () => {
  const ruta = '/api/admin/vehiculos/15/actividad?pagina=1&tamano=20'
  function renderActividad(respuesta: RespuestaSimulada) {
    const api = simularFetch({ [ruta]: respuesta })
    const onSesionTerminada = vi.fn()
    render(<SeccionActividad token={TOKEN_FICTICIO} recurso="vehiculo" id={15} onSesionTerminada={onSesionTerminada} />)
    return { api, onSesionTerminada, user: userEvent.setup() }
  }

  it('se carga al pulsar "Ver actividad" y muestra acción, quién, motivo y cambios', async () => {
    const { api, user } = renderActividad({
      status: 200,
      body: {
        items: [
          {
            id: 120,
            fecha: '2026-11-05T15:00:00Z',
            quien: 'Admin Prueba',
            sobre: 'VEHICULO',
            sobreId: 15,
            accion: 'REASIGNAR',
            descripcion: 'Reasignó de Asesora Alfa a Asesor Beta',
            motivo: 'Cambio de zona',
            cambios: [],
          },
          {
            id: 119,
            fecha: '2026-11-04T15:00:00Z',
            quien: null,
            sobre: 'PUBLICACION',
            sobreId: 3,
            accion: 'EDITAR',
            descripcion: 'Editó la publicación',
            motivo: null,
            cambios: [{ campo: 'Precio', anterior: '$ 90.000.000', nuevo: '$ 95.000.000' }],
          },
        ],
        total: 2,
        pagina: 1,
        tamano: 20,
      },
    })
    expect(api.llamadasA(ruta)).toHaveLength(0)
    await user.click(screen.getByRole('button', { name: 'Ver actividad' }))
    expect(await screen.findByText('Reasignó de Asesora Alfa a Asesor Beta')).toBeInTheDocument()
    expect(screen.getByText('Cambio de zona')).toBeInTheDocument()
    expect(screen.getByText('Sistema')).toBeInTheDocument()
    expect(screen.getByText('Publicación #3')).toBeInTheDocument()
    expect(screen.getByRole('list', { name: 'Campos que cambiaron' })).toHaveTextContent('Precio: $ 90.000.000')
    // Solo lectura: ningún botón de editar ni borrar.
    expect(screen.queryByRole('button', { name: /Editar|Eliminar|Borrar/ })).not.toBeInTheDocument()
  })

  it('404 muestra "Vehículo no encontrado."', async () => {
    const { user } = renderActividad({ status: 404, body: { error: 'Vehículo no encontrado.' } })
    await user.click(screen.getByRole('button', { name: 'Ver actividad' }))
    expect(await screen.findByText('Vehículo no encontrado.')).toBeInTheDocument()
  })

  it('401 termina la sesión', async () => {
    const { user, onSesionTerminada } = renderActividad({ status: 401 })
    await user.click(screen.getByRole('button', { name: 'Ver actividad' }))
    await waitFor(() => expect(onSesionTerminada).toHaveBeenCalled())
  })
})

// ---------------------------------------------------------------- Recuperación asistida (CA-P8-67, 68)

const LISTA_USUARIOS = '/api/admin/usuarios?estado=PENDIENTE&pagina=1&tamano=20'
const ENLACE = 'http://localhost:5173/#/restablecer-contrasena?codigo=Q2ZESjhB_x-1'

function usuario(id: string, nombre: string, telefono: string) {
  return {
    usuarioId: id,
    nombreCompleto: nombre,
    email: `${id}@ejemplo.test`,
    telefono,
    roles: ['COMPRADOR'],
    estado: 'ACTIVA',
    creadoEn: '2026-09-01T10:00:00Z',
    ultimoAccesoEn: null,
    asesor: null,
  }
}

function renderUsuarios(rutas: Rutas, items: unknown[]) {
  const api = simularFetch({ [LISTA_USUARIOS]: { status: 200, body: filas(items) }, ...rutas })
  render(<AdminUsuarios token={TOKEN_FICTICIO} usuarioIdPropio="a-0001" onSesionTerminada={vi.fn()} onVolver={vi.fn()} />)
  return { api, user: userEvent.setup() }
}

describe('Enlace de recuperación en la administración de usuarios', () => {
  it('confirma, genera y muestra el enlace con Copiar y Enviar por WhatsApp', async () => {
    const ruta = '/api/admin/usuarios/u-1/enlace-recuperacion'
    const { api, user } = renderUsuarios(
      { [ruta]: { status: 200, body: { enlace: ENLACE, venceEn: '2026-11-13T16:00:00Z', mensaje: 'Envía este enlace.' } } },
      [usuario('u-1', 'Comprador Uno', '300 123 4567')],
    )
    await user.click(await screen.findByRole('button', { name: /Generar enlace de recuperación/ }))
    const dialogo = screen.getByRole('dialog', { name: 'Generar enlace de recuperación' })
    expect(api.llamadasA(ruta)).toHaveLength(0)
    await user.click(within(dialogo).getByRole('button', { name: 'Generar enlace' }))
    expect(await screen.findByLabelText('Enlace')).toHaveValue(ENLACE)
    expect(api.llamadasA(ruta)[0].init?.method).toBe('POST')
    expect(screen.getByText(/Vence en 60 minutos/)).toHaveTextContent('sirve una sola vez')
    const wa = screen.getByRole('link', { name: /Enviar por WhatsApp/ })
    expect(wa.getAttribute('href')).toMatch(/^https:\/\/wa\.me\/573001234567\?text=/)
    expect(decodeURIComponent(wa.getAttribute('href') ?? '')).toContain(ENLACE)
    const correo = screen.getByRole('link', { name: /Enviar por correo/ })
    const href = correo.getAttribute('href') ?? ''
    expect(href).toMatch(/^mailto:u-1@ejemplo\.test\?subject=/)
    expect(decodeURIComponent(href)).toContain(ENLACE)
    expect(decodeURIComponent(href)).toContain('Recupera tu contraseña')
    const escribir = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', { value: { writeText: escribir }, configurable: true })
    await user.click(screen.getByRole('button', { name: 'Copiar' }))
    expect(escribir).toHaveBeenCalledWith(ENLACE)
    expect(await screen.findByText('Enlace copiado.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Cerrar' }))
    expect(screen.queryByLabelText('Enlace')).not.toBeInTheDocument()
  })

  it('el correo con caracteres peligrosos no genera mailto (no se inyectan destinatarios)', () => {
    expect(enlaceCorreoUsuario('ana@ejemplo.test?cc=otro@x.test', ENLACE)).toBeNull()
    expect(enlaceCorreoUsuario('ana@ejemplo.test&bcc=x@y.co', ENLACE)).toBeNull()
    expect(enlaceCorreoUsuario('', ENLACE)).toBeNull()
    expect(enlaceCorreoUsuario('ana@ejemplo.test', 'javascript:alert(1)')).toBeNull()
    expect(enlaceCorreoUsuario(' ana@ejemplo.test ', ENLACE)).toMatch(/^mailto:ana@ejemplo\.test\?/)
  })

  it('sin celular válido no ofrece WhatsApp; 409 muestra el error', async () => {
    expect(enlaceWhatsappUsuario('123', ENLACE)).toBeNull()
    const ruta = '/api/admin/usuarios/u-2/enlace-recuperacion'
    const { user } = renderUsuarios(
      { [ruta]: { status: 409, body: { error: 'Solo se puede generar un enlace para una cuenta activa.' } } },
      [usuario('u-2', 'Comprador Dos', '123')],
    )
    await user.click(await screen.findByRole('button', { name: /Generar enlace de recuperación/ }))
    await user.click(screen.getByRole('button', { name: 'Generar enlace' }))
    expect(await screen.findByText('Solo se puede generar un enlace para una cuenta activa.')).toBeInTheDocument()
  })
})

describe('Pantalla pública #/restablecer-contrasena', () => {
  const RUTA = '/api/auth/restablecer-contrasena'

  function abrir(hash: string, rutas: Rutas = {}) {
    window.location.hash = hash
    const api = simularFetch(rutas)
    render(<App />)
    return { api, user: userEvent.setup() }
  }

  it('lee el código, lo quita de la URL y cambia la contraseña', async () => {
    const { api, user } = abrir('#/restablecer-contrasena?codigo=ABC_123-x', {
      [RUTA]: { status: 200, body: { mensaje: 'Listo. Tu contraseña cambió; inicia sesión con la nueva.' } },
    })
    expect(await screen.findByRole('heading', { level: 1, name: 'Crear una contraseña nueva' })).toBeInTheDocument()
    await waitFor(() => expect(window.location.hash).toBe('#/restablecer-contrasena'))
    expect(window.location.href).not.toContain('ABC_123')
    expect(screen.getByText('Al menos una letra mayúscula (A-Z)')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Contraseña nueva'), 'Nueva1234')
    await user.type(screen.getByLabelText('Confirma la contraseña nueva'), 'Nueva1234')
    await user.click(screen.getByRole('button', { name: 'Guardar contraseña nueva' }))
    expect(await screen.findByText('Listo. Tu contraseña cambió; inicia sesión con la nueva.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Iniciar sesión' })).toHaveAttribute('href', '#/cuenta')
    expect(cuerpoDe(api, RUTA)).toEqual({ codigo: 'ABC_123-x', password: 'Nueva1234' })
  })

  it('confirmación distinta o contraseña débil: error en cliente sin llamar a la API', async () => {
    const { api, user } = abrir('#/restablecer-contrasena?codigo=ABC')
    await user.type(await screen.findByLabelText('Contraseña nueva'), 'Nueva1234')
    await user.type(screen.getByLabelText('Confirma la contraseña nueva'), 'Otra1234')
    await user.click(screen.getByRole('button', { name: 'Guardar contraseña nueva' }))
    expect(await screen.findByText('Las contraseñas no coinciden.')).toBeInTheDocument()
    await user.clear(screen.getByLabelText('Contraseña nueva'))
    await user.type(screen.getByLabelText('Contraseña nueva'), 'corta')
    await user.click(screen.getByRole('button', { name: 'Guardar contraseña nueva' }))
    expect(await screen.findByText('Debe tener al menos 8 caracteres.')).toBeInTheDocument()
    expect(api.llamadasA(RUTA)).toHaveLength(0)
  })

  it('enlace vencido o usado: error claro y enlace a iniciar sesión', async () => {
    const { user } = abrir('#/restablecer-contrasena?codigo=VIEJO', {
      [RUTA]: {
        status: 400,
        body: {
          error: 'Revisa los datos marcados.',
          errores: { codigo: ['El enlace no es válido o ya venció. Pide uno nuevo al administrador.'] },
        },
      },
    })
    await user.type(await screen.findByLabelText('Contraseña nueva'), 'Nueva1234')
    await user.type(screen.getByLabelText('Confirma la contraseña nueva'), 'Nueva1234')
    await user.click(screen.getByRole('button', { name: 'Guardar contraseña nueva' }))
    expect(await screen.findByText('El enlace no es válido o ya venció. Pide uno nuevo al administrador.')).toBeInTheDocument()
    expect(screen.queryByLabelText('Contraseña nueva')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ir a iniciar sesión' })).toBeInTheDocument()
  })

  it('429: muestra el mensaje de demasiados intentos', async () => {
    const mensaje = 'Demasiados intentos. Espera unos minutos e intenta de nuevo.'
    const { user } = abrir('#/restablecer-contrasena?codigo=ABC', { [RUTA]: { status: 429, body: { error: mensaje } } })
    await user.type(await screen.findByLabelText('Contraseña nueva'), 'Nueva1234')
    await user.type(screen.getByLabelText('Confirma la contraseña nueva'), 'Nueva1234')
    await user.click(screen.getByRole('button', { name: 'Guardar contraseña nueva' }))
    expect(await screen.findByText(mensaje)).toBeInTheDocument()
  })

  it('sin código: error sin formulario', async () => {
    abrir('#/restablecer-contrasena')
    expect(await screen.findByText('El enlace no es válido o ya venció. Pide uno nuevo al administrador.')).toBeInTheDocument()
    expect(screen.queryByLabelText('Contraseña nueva')).not.toBeInTheDocument()
    expect(codigoDelHash('#/restablecer-contrasena?codigo=a%2Bb')).toBe('a+b')
  })

  it('el inicio de sesión remite al WhatsApp del portal (CA-P8-68)', async () => {
    abrir('#/cuenta')
    const enlace = await screen.findByRole('link', { name: /Escríbenos por WhatsApp/ })
    expect(enlace.getAttribute('href')).toMatch(/^https:\/\/wa\.me\/573166152907\?text=/)
    expect(screen.getByText(/¿Olvidaste tu contraseña\?/)).toBeInTheDocument()
  })
})
