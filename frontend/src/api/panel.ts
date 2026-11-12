// P7 — Panel "Inicio" del administrador (contrato docs/criterios/P7-contrato-api.md §5; HU-075 parcial, RN-RE-001;
// D-64 a A). Solo números: sin nombres, teléfonos, correos ni sumas de dinero (CA-P7-48). SoloAdministrador.
import { MENSAJE_SESION_TERMINADA } from './auth'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion } from './http'
import { ORIGENES_LEAD, type OrigenLead } from './leads'

export type ContadorSolicitudes = { origen: OrigenLead; abiertas: number; nuevas: number }

export const ESTADOS_VEHICULO_PANEL = ['ACTIVO', 'RESERVADO', 'INACTIVO', 'VENDIDO'] as const
export type EstadoVehiculoPanel = (typeof ESTADOS_VEHICULO_PANEL)[number]

export type PanelAdmin = {
  asesoresPendientes: number
  /** Siempre los 5 orígenes, en el orden de ORIGENES_LEAD. */
  solicitudes: ContadorSolicitudes[]
  suscripcionesPorVencer: number
  suscripcionesVencidas: number
  publicacionesVisibles: number
  vehiculosPorEstado: Record<EstadoVehiculoPanel, number>
  ventasUltimos30Dias: number
  /** AAAA-MM-DD (hoy en Colombia menos 29 días). */
  ventasDesde: string | null
}

function entero(valor: unknown): number {
  return typeof valor === 'number' && Number.isFinite(valor) && valor >= 0 ? Math.floor(valor) : 0
}

export function leerPanel(valor: unknown): PanelAdmin {
  if (!esObjetoJson(valor)) throw new ApiError(200, MENSAJE_INESPERADO)
  const lista = Array.isArray(valor.solicitudes) ? valor.solicitudes : []
  const solicitudes = ORIGENES_LEAD.map((origen) => {
    const fila = lista.find((s) => esObjetoJson(s) && String(s.origen).toUpperCase() === origen)
    const datos = esObjetoJson(fila) ? fila : {}
    return { origen, abiertas: entero(datos.abiertas), nuevas: entero(datos.nuevas) }
  })
  const porEstado = esObjetoJson(valor.vehiculosPorEstado) ? valor.vehiculosPorEstado : {}
  const vehiculosPorEstado = Object.fromEntries(
    ESTADOS_VEHICULO_PANEL.map((e) => [e, entero(porEstado[e])]),
  ) as Record<EstadoVehiculoPanel, number>
  const desde = typeof valor.ventasDesde === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(valor.ventasDesde) ? valor.ventasDesde : null
  return {
    asesoresPendientes: entero(valor.asesoresPendientes),
    solicitudes,
    suscripcionesPorVencer: entero(valor.suscripcionesPorVencer),
    suscripcionesVencidas: entero(valor.suscripcionesVencidas),
    publicacionesVisibles: entero(valor.publicacionesVisibles),
    vehiculosPorEstado,
    ventasUltimos30Dias: entero(valor.ventasUltimos30Dias),
    ventasDesde: desde,
  }
}

export async function obtenerPanel(token: string, signal?: AbortSignal): Promise<PanelAdmin> {
  const respuesta = await peticion('/api/admin/panel', {
    token,
    signal,
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA },
  })
  return leerPanel(respuesta)
}
