// P5 — datos ficticios de contactos (leads) para las pruebas de interfaz. No son personas reales.
import { vi } from 'vitest'

export const RUTA_CONTACTOS = '/api/contactos'
export const RUTA_RESUMEN = '/api/leads/resumen'

export function rutaLeads(consulta = ''): string {
  return `/api/leads?${consulta === '' ? '' : `${consulta}&`}pagina=1&tamano=20`
}

export function lead(id: number, extra: Record<string, unknown> = {}) {
  return {
    id,
    estado: 'ABIERTO',
    nombre: `Visitante ${id}`,
    telefono: '+573001234567',
    correo: null,
    mensaje: 'Me interesa este vehículo',
    creadoEn: '2026-11-04T15:00:00Z',
    nuevo: false,
    vehiculo: {
      vehiculoId: 40,
      publicacionId: 60,
      titulo: 'Chevrolet NPR 2020',
      slug: 'chevrolet-npr-2020',
      estadoVehiculo: 'ACTIVO',
      estadoPublicacion: 'PUBLICADA',
    },
    responsable: { tipo: 'ASESOR', asesorId: 7, nombre: 'Asesor Uno' },
    resultadoCierre: null,
    cerradoEn: null,
    ...extra,
  }
}

export const EVENTOS = [
  {
    tipo: 'CREADO',
    fecha: '2026-11-04T15:00:00Z',
    actor: { tipo: 'SISTEMA', nombre: null },
    automatico: true,
    texto: 'Me interesa este vehículo',
    resultado: null,
    responsableAnterior: null,
    responsableNuevo: null,
  },
  {
    tipo: 'ASIGNADO',
    fecha: '2026-11-04T15:00:01Z',
    actor: { tipo: 'SISTEMA', nombre: null },
    automatico: true,
    texto: null,
    resultado: null,
    responsableAnterior: null,
    responsableNuevo: { tipo: 'ASESOR', asesorId: 7, nombre: 'Asesor Uno' },
  },
  {
    tipo: 'REASIGNADO',
    fecha: '2026-11-05T10:00:00Z',
    actor: { tipo: 'SISTEMA', nombre: null },
    automatico: true,
    texto: 'Plan vencido',
    resultado: null,
    responsableAnterior: { tipo: 'ASESOR', asesorId: 7, nombre: 'Asesor Uno' },
    responsableNuevo: { tipo: 'ADMINISTRACION', asesorId: null, nombre: null },
  },
  {
    tipo: 'NOTA',
    fecha: '2026-11-05T11:00:00Z',
    actor: { tipo: 'USUARIO', nombre: 'Persona Ficticia' },
    automatico: false,
    texto: 'Llamé y no contestó.',
    resultado: null,
    responsableAnterior: null,
    responsableNuevo: null,
  },
]

export const CONSENTIMIENTOS = [
  { formulario: 'CONTACTO_VEHICULO', versionPolitica: 'BORRADOR-2026-11-04', aceptadoEn: '2026-11-04T15:00:00Z' },
]

export function detalleLead(id: number, extra: Record<string, unknown> = {}) {
  return {
    ...lead(id),
    observacionCierre: null,
    cerradoPor: null,
    eventos: EVENTOS,
    consentimientos: null,
    ...extra,
  }
}

export function paginaLeads(items: unknown[], total = items.length, pagina = 1) {
  return { status: 200, body: { items, total, pagina, tamano: 20 } }
}

/** Cuerpo JSON de la i-ésima llamada a `ruta`. */
export function cuerpo(llamadas: { url: string; init: RequestInit | undefined }[], ruta: string, i = 0): unknown {
  const l = llamadas.filter((x) => x.url === ruta)[i]
  return l?.init?.body === undefined ? undefined : JSON.parse(String(l.init.body))
}

/**
 * Deja pendientes las peticiones a `ruta` hasta llamar a `soltar()`, para ver el estado "Enviando...".
 * Envuelve el fetch simulado actual.
 */
export function retenerRuta(ruta: string) {
  const original = globalThis.fetch
  let soltar: () => void = () => {}
  const espera = new Promise<void>((r) => {
    soltar = r
  })
  vi.stubGlobal('fetch', async (entrada: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof entrada === 'string' ? entrada : entrada.toString()
    if (url === ruta) await espera
    return original(entrada, init)
  })
  return { soltar: () => soltar() }
}
