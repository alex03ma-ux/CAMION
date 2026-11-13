// P8 — Actividad de un vehículo y de una suscripción (contrato docs/criterios/P8-contrato-api.md §10; HU-061, RN-SE-003;
// D-62 d, D-72 A/A). Solo el administrador (SoloAdministrador) y solo lectura (GET): no hay ninguna ruta para editar
// o borrar eventos. El texto de cada acción ya viene en palabras (`descripcion`); los cambios de EDITAR, con etiquetas.
import { MENSAJE_SESION_TERMINADA } from './auth'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion } from './http'

export type RecursoActividad = 'vehiculo' | 'suscripcion'

export type CambioActividad = { campo: string; anterior: string; nuevo: string }

export type EventoActividad = {
  id: number
  fecha: string | null
  /** Nombre del usuario o "Sistema". */
  quien: string
  sobre: string
  sobreId: number | null
  accion: string
  descripcion: string
  motivo: string | null
  cambios: CambioActividad[]
}

export type ListaActividad = { items: EventoActividad[]; total: number; pagina: number; tamano: number }

export const TAMANO_PAGINA_ACTIVIDAD = 20
export const MENSAJE_VEHICULO_NO_ENCONTRADO = 'Vehículo no encontrado.'
export const MENSAJE_SUSCRIPCION_NO_ENCONTRADA = 'Suscripción no encontrada.'

export const NOMBRES_SOBRE: Readonly<Record<string, string>> = {
  VEHICULO: 'Vehículo',
  PUBLICACION: 'Publicación',
  VENTA: 'Venta',
  SUSCRIPCION: 'Suscripción',
  PAGO: 'Pago',
}

export function nombreSobre(sobre: string, id: number | null): string {
  const nombre = NOMBRES_SOBRE[sobre.toUpperCase()] ?? (sobre || 'Registro')
  return id !== null && id > 0 ? `${nombre} #${id}` : nombre
}

function texto(valor: unknown): string {
  return typeof valor === 'string' ? valor : valor === null || valor === undefined ? '' : String(valor)
}

function textoONulo(valor: unknown): string | null {
  return typeof valor === 'string' && valor.trim() !== '' ? valor : null
}

function numeroONulo(valor: unknown): number | null {
  return typeof valor === 'number' && Number.isFinite(valor) ? valor : null
}

export function leerEvento(valor: unknown): EventoActividad | null {
  if (!esObjetoJson(valor)) return null
  const id = numeroONulo(valor.id)
  if (id === null) return null
  const accion = texto(valor.accion).toUpperCase()
  return {
    id,
    fecha: textoONulo(valor.fecha),
    quien: textoONulo(valor.quien) ?? 'Sistema',
    sobre: texto(valor.sobre).toUpperCase(),
    sobreId: numeroONulo(valor.sobreId),
    accion,
    descripcion: textoONulo(valor.descripcion) ?? accion,
    motivo: textoONulo(valor.motivo),
    cambios: Array.isArray(valor.cambios)
      ? valor.cambios.filter(esObjetoJson).map((c) => ({
          campo: texto(c.campo),
          anterior: textoONulo(c.anterior) ?? '—',
          nuevo: textoONulo(c.nuevo) ?? '—',
        }))
      : [],
  }
}

export async function listarActividad(
  token: string,
  recurso: RecursoActividad,
  id: number,
  pagina: number,
  signal?: AbortSignal,
): Promise<ListaActividad> {
  const base = recurso === 'vehiculo' ? 'vehiculos' : 'suscripciones'
  const p = new URLSearchParams({ pagina: String(pagina), tamano: String(TAMANO_PAGINA_ACTIVIDAD) })
  const respuesta = await peticion(`/api/admin/${base}/${encodeURIComponent(String(id))}/actividad?${p.toString()}`, {
    token,
    signal,
    mensajesPorCodigo: {
      401: MENSAJE_SESION_TERMINADA,
      404: recurso === 'vehiculo' ? MENSAJE_VEHICULO_NO_ENCONTRADO : MENSAJE_SUSCRIPCION_NO_ENCONTRADA,
    },
  })
  if (!esObjetoJson(respuesta) || !Array.isArray(respuesta.items)) throw new ApiError(200, MENSAJE_INESPERADO)
  const items = respuesta.items.map(leerEvento).filter((e): e is EventoActividad => e !== null)
  return {
    items,
    total: numeroONulo(respuesta.total) ?? items.length,
    pagina: numeroONulo(respuesta.pagina) ?? pagina,
    tamano: numeroONulo(respuesta.tamano) ?? TAMANO_PAGINA_ACTIVIDAD,
  }
}
