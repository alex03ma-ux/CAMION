// P6 — "Mis solicitudes" del comprador (D-53, D-59; contrato docs/criterios/P6-contrato-api.md).
// GET /api/mis-solicitudes con la política SoloComprador: asesor o administrador → 403, sin token → 401.
// Solo lectura (CA-P6-69). Nunca trae notas, responsable, resultado ni datos de contacto (CA-P6-68).
import { MENSAJE_SESION_TERMINADA } from './auth'
import { aTarjetas, type TarjetaVehiculo } from './catalogo'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion } from './http'

export type VehiculoMiSolicitud = {
  titulo: string
  /** Solo si `disponible`; si no, null y no se enlaza (CA-P6-71). */
  slug: string | null
  disponible: boolean
}

export type MiSolicitud = {
  id: number
  creadoEn: string
  /** El origen: CONTACTO_VEHICULO, VENDE_TU_CAMION, SEGURO_TODO_RIESGO, CREDITO o BUSQUEDA. */
  tipo: string
  resumen: string
  /** PENDIENTE o FINALIZADA (contactos y avisos); REGISTRADA, EN_GESTION o FINALIZADA (créditos y búsquedas). */
  estado: string
  vehiculo: VehiculoMiSolicitud | null
  /** Solo búsquedas: los vehículos que la administración le recomienda y siguen visibles (CA-P6-72). */
  recomendados: TarjetaVehiculo[]
}

export type PaginaMisSolicitudes = { items: MiSolicitud[]; total: number; pagina: number; tamano: number }

export const TAMANO_MIS_SOLICITUDES = 20
export const RUTA_MIS_SOLICITUDES = '/api/mis-solicitudes'
export const MENSAJE_VEHICULO_YA_NO_DISPONIBLE = 'Este vehículo ya no está disponible'

const NOMBRES_TIPO: Record<string, string> = {
  CONTACTO_VEHICULO: 'Contacto por vehículo',
  VENDE_TU_CAMION: 'Vende tu camión',
  SEGURO_TODO_RIESGO: 'Seguro todo riesgo',
  CREDITO: 'Crédito',
  BUSQUEDA: 'Búsqueda',
}

const NOMBRES_ESTADO: Record<string, string> = {
  PENDIENTE: 'Pendiente',
  REGISTRADA: 'Registrada',
  EN_GESTION: 'En gestión',
  FINALIZADA: 'Finalizada',
}

export function nombreTipoSolicitud(tipo: string): string {
  return NOMBRES_TIPO[tipo.toUpperCase()] ?? tipo
}

export function nombreEstadoSolicitud(estado: string): string {
  return NOMBRES_ESTADO[estado.toUpperCase()] ?? estado
}

function texto(valor: unknown): string {
  return typeof valor === 'string' ? valor : ''
}

function numero(valor: unknown): number {
  return typeof valor === 'number' && Number.isFinite(valor) ? valor : 0
}

function leerVehiculo(valor: unknown): VehiculoMiSolicitud | null {
  if (!esObjetoJson(valor)) return null
  const disponible = valor.disponible === true
  const slug = texto(valor.slug).trim()
  return { titulo: texto(valor.titulo), slug: disponible && slug !== '' ? slug : null, disponible: disponible && slug !== '' }
}

function leerItem(valor: unknown): MiSolicitud | null {
  if (!esObjetoJson(valor) || typeof valor.id !== 'number') return null
  return {
    id: valor.id,
    creadoEn: texto(valor.creadoEn),
    tipo: texto(valor.tipo).toUpperCase(),
    resumen: texto(valor.resumen),
    estado: texto(valor.estado).toUpperCase(),
    vehiculo: leerVehiculo(valor.vehiculo),
    recomendados: aTarjetas(valor.recomendados),
  }
}

/** GET /api/mis-solicitudes — de la más reciente a la más antigua, 20 por página (CA-P6-66). */
export async function listarMisSolicitudes(
  token: string,
  pagina: number,
  signal?: AbortSignal,
): Promise<PaginaMisSolicitudes> {
  const consulta = new URLSearchParams({ pagina: String(pagina), tamano: String(TAMANO_MIS_SOLICITUDES) })
  const datos = await peticion(`${RUTA_MIS_SOLICITUDES}?${consulta.toString()}`, {
    token,
    signal,
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA },
  })
  if (!esObjetoJson(datos) || !Array.isArray(datos.items)) throw new ApiError(200, MENSAJE_INESPERADO)
  return {
    items: datos.items.map(leerItem).filter((i): i is MiSolicitud => i !== null),
    total: numero(datos.total),
    pagina: numero(datos.pagina) || pagina,
    tamano: numero(datos.tamano) || TAMANO_MIS_SOLICITUDES,
  }
}
