// P7 — Historial de ventas (contrato docs/criterios/P7-contrato-api.md §1; HU-064, minuta §6; D-39, D-62 A).
// El administrador ve todas; el asesor, las de los vehículos que hoy son suyos, también las revertidas. Política
// AsesorOAdministrador en la API: un comprador recibe 403 y sin sesión 401 (CA-P7-08). Las respuestas no traen
// datos de compradores ni de leads (CA-P7-12). Vender y revertir no cambian (P4b).
import { MENSAJE_SESION_TERMINADA } from './auth'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion } from './http'
import { fechaIsoValida } from './pagos'
import { NOMBRES_ORIGEN_VENTA, ORIGENES_VENTA, type OrigenVenta } from './publicaciones'

// ---- Tipos del contrato ----

export type EstadoVenta = 'VIGENTE' | 'REVERTIDA'
export const ESTADOS_VENTA: readonly EstadoVenta[] = ['VIGENTE', 'REVERTIDA']
export const NOMBRES_ESTADO_VENTA: Readonly<Record<EstadoVenta, string>> = {
  VIGENTE: 'Vigente',
  REVERTIDA: 'Revertida',
}

export type VentaItem = {
  id: number
  vehiculo: { id: number; titulo: string; estado: string }
  /** Dueño actual; null para el asesor (como en el inventario). */
  asesor: { id: number | string; nombre: string } | null
  fechaVenta: string | null
  precioFinalCop: number
  origen: string
  observaciones: string | null
  estado: EstadoVenta
  registradaEn: string | null
  registradaPor: string | null
  revertidaEn: string | null
  revertidaPor: string | null
  motivoReversion: string | null
}

export type ListaVentas = { items: VentaItem[]; total: number; pagina: number; tamano: number }

export type FiltrosVentas = {
  desde: string
  hasta: string
  origen: OrigenVenta | ''
  estado: EstadoVenta | ''
  /** Solo administrador (`perfiles_asesor.id`); el asesor nunca lo envía. */
  asesorId: string
  pagina: number
  tamano: number
}

export const TAMANO_PAGINA_VENTAS = 20

export const FILTROS_VENTAS_VACIOS: FiltrosVentas = {
  desde: '',
  hasta: '',
  origen: '',
  estado: '',
  asesorId: '',
  pagina: 1,
  tamano: TAMANO_PAGINA_VENTAS,
}

export const MENSAJE_VENTA_FECHA = 'Escribe una fecha válida (aaaa-mm-dd).'
export const MENSAJE_VENTA_RANGO = 'La fecha inicial no puede ser posterior a la final.'
export const MENSAJE_VEHICULO_NO_ENCONTRADO = 'Vehículo no encontrado.'
export const MENSAJE_VENDIDO_SIN_DATOS =
  'Este vehículo figura como vendido, pero no tiene datos de venta registrados.'

export function nombreOrigenVenta(origen: string): string {
  const clave = origen.toUpperCase()
  return clave in NOMBRES_ORIGEN_VENTA ? NOMBRES_ORIGEN_VENTA[clave as OrigenVenta] : origen || '—'
}

export function nombreEstadoVenta(estado: string): string {
  return estado.toUpperCase() === 'REVERTIDA' ? 'Revertida' : 'Vigente'
}

/** Lee los filtros conocidos de la cadena de consulta del hash (enlaces del panel). Ignora los no válidos. */
export function filtrosVentasDesdeConsulta(consulta: string): FiltrosVentas {
  const p = new URLSearchParams(consulta)
  const origen = (p.get('origen') ?? '').toUpperCase()
  const estado = (p.get('estado') ?? '').toUpperCase()
  const desde = p.get('desde') ?? ''
  const hasta = p.get('hasta') ?? ''
  const asesorId = p.get('asesorId') ?? ''
  return {
    ...FILTROS_VENTAS_VACIOS,
    desde: fechaIsoValida(desde) ? desde : '',
    hasta: fechaIsoValida(hasta) ? hasta : '',
    origen: ORIGENES_VENTA.includes(origen as OrigenVenta) ? (origen as OrigenVenta) : '',
    estado: ESTADOS_VENTA.includes(estado as EstadoVenta) ? (estado as EstadoVenta) : '',
    asesorId: /^\d{1,15}$/.test(asesorId) ? asesorId : '',
  }
}

export function validarFiltrosVentas(
  f: Pick<FiltrosVentas, 'desde' | 'hasta'>,
): Partial<Record<'desde' | 'hasta', string[]>> {
  const errores: Partial<Record<'desde' | 'hasta', string[]>> = {}
  if (f.desde !== '' && !fechaIsoValida(f.desde)) errores.desde = [MENSAJE_VENTA_FECHA]
  if (f.hasta !== '' && !fechaIsoValida(f.hasta)) errores.hasta = [MENSAJE_VENTA_FECHA]
  if (!errores.desde && !errores.hasta && f.desde !== '' && f.hasta !== '' && f.desde > f.hasta) {
    errores.hasta = [MENSAJE_VENTA_RANGO]
  }
  return errores
}

// ---- Lectura de respuestas ----

function texto(valor: unknown): string {
  return typeof valor === 'string' ? valor : ''
}

function textoONulo(valor: unknown): string | null {
  return typeof valor === 'string' && valor.trim() !== '' ? valor : null
}

function numeroONulo(valor: unknown): number | null {
  return typeof valor === 'number' && Number.isFinite(valor) ? valor : null
}

export function leerVenta(valor: unknown): VentaItem | null {
  if (!esObjetoJson(valor)) return null
  const id = numeroONulo(valor.id)
  if (id === null) return null
  const vehiculo = esObjetoJson(valor.vehiculo) ? valor.vehiculo : {}
  const asesor = esObjetoJson(valor.asesor) ? valor.asesor : null
  const codigo = texto(valor.estado).toUpperCase()
  const estado: EstadoVenta =
    codigo === 'REVERTIDA' || codigo === 'VIGENTE' ? codigo : textoONulo(valor.revertidaEn) ? 'REVERTIDA' : 'VIGENTE'
  return {
    id,
    vehiculo: {
      id: numeroONulo(vehiculo.id) ?? 0,
      titulo: texto(vehiculo.titulo),
      estado: texto(vehiculo.estado).toUpperCase(),
    },
    asesor: asesor
      ? {
          id: numeroONulo(asesor.id) ?? texto(asesor.id),
          nombre: texto(asesor.nombre),
        }
      : null,
    fechaVenta: textoONulo(valor.fechaVenta),
    precioFinalCop: numeroONulo(valor.precioFinalCop) ?? 0,
    origen: texto(valor.origen).toUpperCase(),
    observaciones: textoONulo(valor.observaciones),
    estado,
    registradaEn: textoONulo(valor.registradaEn),
    registradaPor: textoONulo(valor.registradaPor),
    revertidaEn: textoONulo(valor.revertidaEn),
    revertidaPor: textoONulo(valor.revertidaPor),
    motivoReversion: textoONulo(valor.motivoReversion),
  }
}

// ---- Endpoints ----

export async function listarVentas(
  token: string,
  filtros: FiltrosVentas,
  esAdmin: boolean,
  signal?: AbortSignal,
): Promise<ListaVentas> {
  const p = new URLSearchParams()
  if (filtros.desde) p.set('desde', filtros.desde)
  if (filtros.hasta) p.set('hasta', filtros.hasta)
  if (filtros.origen) p.set('origen', filtros.origen)
  if (filtros.estado) p.set('estado', filtros.estado)
  if (esAdmin && filtros.asesorId) p.set('asesorId', filtros.asesorId)
  p.set('pagina', String(filtros.pagina))
  p.set('tamano', String(filtros.tamano))
  const respuesta = await peticion(`/api/ventas?${p.toString()}`, {
    token,
    signal,
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA },
  })
  if (!esObjetoJson(respuesta) || !Array.isArray(respuesta.items)) throw new ApiError(200, MENSAJE_INESPERADO)
  const items = respuesta.items.map(leerVenta).filter((v): v is VentaItem => v !== null)
  return {
    items,
    total: numeroONulo(respuesta.total) ?? items.length,
    pagina: numeroONulo(respuesta.pagina) ?? filtros.pagina,
    tamano: numeroONulo(respuesta.tamano) ?? filtros.tamano,
  }
}

/** Todas las ventas de un vehículo (la vigente primero). 404 si no existe o es de otro asesor. */
export async function listarVentasVehiculo(token: string, vehiculoId: number, signal?: AbortSignal): Promise<VentaItem[]> {
  const respuesta = await peticion(`/api/inventario/vehiculos/${encodeURIComponent(String(vehiculoId))}/ventas`, {
    token,
    signal,
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA, 404: MENSAJE_VEHICULO_NO_ENCONTRADO },
  })
  if (!esObjetoJson(respuesta) || !Array.isArray(respuesta.items)) throw new ApiError(200, MENSAJE_INESPERADO)
  return respuesta.items.map(leerVenta).filter((v): v is VentaItem => v !== null)
}
