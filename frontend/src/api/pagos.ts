// P7 — Pagos del plan (contrato docs/criterios/P7-contrato-api.md §2; HU-066, HU-067, FN-04; D-26, D-36, D-61 B).
// El pago se cobra fuera del portal y el administrador solo lo anota: ninguna ruta recibe datos de tarjeta ni
// cuentas bancarias (CA-P7-33). Todas las rutas exigen ADMINISTRADOR en la API (SoloAdministrador); el asesor
// no ve los pagos (CA-P7-32).
import { MOTIVO_MAX, MOTIVO_MIN } from './admin'
import { MENSAJE_SESION_TERMINADA } from './auth'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion } from './http'

// ---- Tipos del contrato ----

export type MedioPago = 'TRANSFERENCIA' | 'CONSIGNACION' | 'EFECTIVO' | 'OTRO'
export type ConceptoPago = 'ASIGNACION' | 'RENOVACION' | 'CAMBIO_PLAN' | 'POSTERIOR'
export type EstadoPago = 'REGISTRADO' | 'ANULADO'

/** Objeto `pago` de asignar, renovar y cambiar de plan; cuerpo de POST /pagos. */
export type PagoRequest = {
  valorCop: number
  fechaPago: string
  medio: MedioPago
  referencia: string | null
}

export type PagoItem = {
  id: number
  suscripcionId: number
  valorCop: number
  fechaPago: string | null
  medio: string
  referencia: string | null
  concepto: string
  estado: string
  registradoEn: string | null
  registradoPor: string | null
  anuladoEn: string | null
  anuladoPor: string | null
  motivoAnulacion: string | null
}

export type ListaPagos = { items: PagoItem[]; totalPagadoCop: number }

/** Lo que escribe el administrador (el valor como dígitos). */
export type FormularioPago = {
  valorCop: string
  fechaPago: string
  medio: MedioPago | ''
  referencia: string
}

export type CampoPago = keyof FormularioPago
export type ErroresPago = Partial<Record<CampoPago | 'pago', string[]>>
export const CAMPOS_PAGO: readonly (CampoPago | 'pago')[] = ['valorCop', 'fechaPago', 'medio', 'referencia', 'pago']

// ---- Límites y mensajes del contrato ----

export const VALOR_PAGO_MAX = 9_999_999_999_999
export const REFERENCIA_MAX = 100

export const MENSAJE_PAGO_VALOR = 'Escribe el valor pagado en pesos (número entero mayor que 0).'
export const MENSAJE_PAGO_FECHA = 'Escribe la fecha del pago (AAAA-MM-DD), de hoy o anterior.'
export const MENSAJE_PAGO_MEDIO = 'Elige el medio de pago.'
export const MENSAJE_PAGO_REFERENCIA = 'La referencia no puede tener más de 100 caracteres ni los signos < >.'
export const MENSAJE_PAGO_REFERENCIA_OTRO = 'Escribe la referencia: es obligatoria si el medio es Otro.'
export const MENSAJE_PAGO_REFERENCIA_CONTROL = 'La referencia no puede tener saltos de línea ni caracteres de control.'
export const MENSAJE_MOTIVO_CONTROL = 'El motivo no puede tener saltos de línea ni caracteres de control.'
export const MENSAJE_PAGO_SIN_PRECIO = 'Este plan no tiene precio: no se registra un pago.'
export const MENSAJE_MOTIVO_ANULACION = 'Escribe el motivo (5 a 500 caracteres).'
export const MENSAJE_PAGO_NO_ENCONTRADO = 'Pago no encontrado.'
export const MENSAJE_PAGO_YA_ANULADO = 'Este pago ya está anulado.'

export const MEDIOS_PAGO: readonly MedioPago[] = ['TRANSFERENCIA', 'CONSIGNACION', 'EFECTIVO', 'OTRO']

export const NOMBRES_MEDIO_PAGO: Readonly<Record<MedioPago, string>> = {
  TRANSFERENCIA: 'Transferencia',
  CONSIGNACION: 'Consignación',
  EFECTIVO: 'Efectivo',
  OTRO: 'Otro',
}

export const NOMBRES_CONCEPTO_PAGO: Readonly<Record<ConceptoPago, string>> = {
  ASIGNACION: 'Asignación',
  RENOVACION: 'Renovación',
  CAMBIO_PLAN: 'Cambio de plan',
  POSTERIOR: 'Pago posterior',
}

export function nombreMedioPago(medio: string): string {
  const clave = medio.toUpperCase()
  return clave in NOMBRES_MEDIO_PAGO ? NOMBRES_MEDIO_PAGO[clave as MedioPago] : medio || '—'
}

export function nombreConceptoPago(concepto: string): string {
  const clave = concepto.toUpperCase()
  return clave in NOMBRES_CONCEPTO_PAGO ? NOMBRES_CONCEPTO_PAGO[clave as ConceptoPago] : concepto || '—'
}

export function nombreEstadoPago(estado: string): string {
  return estado.toUpperCase() === 'ANULADO' ? 'Anulado' : 'Registrado'
}

// ---- Fecha de hoy en Colombia (UTC-5), como la API ----

const PARTES_BOGOTA = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'America/Bogota',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
})

/** "AAAA-MM-DD" de hoy en Colombia. */
export function hoyColombia(ahora: Date = new Date()): string {
  const partes = PARTES_BOGOTA.formatToParts(ahora)
  const valor = (tipo: string) => partes.find((p) => p.type === tipo)?.value ?? ''
  return `${valor('year')}-${valor('month')}-${valor('day')}`
}

const PATRON_FECHA = /^\d{4}-\d{2}-\d{2}$/
/** B-3: caracteres de control (saltos de línea, tabuladores...). */
const PATRON_CONTROL = /[\u0000-\u001F\u007F]/

/** Fecha de calendario real en formato AAAA-MM-DD. */
export function fechaIsoValida(valor: string): boolean {
  if (!PATRON_FECHA.test(valor)) return false
  const d = new Date(`${valor}T00:00:00Z`)
  return !Number.isNaN(d.getTime()) && d.toISOString().slice(0, 10) === valor
}

/** Formulario nuevo: valor = precio del plan (si se conoce), fecha de hoy, sin medio. */
export function formularioPagoInicial(precioCop: number | null): FormularioPago {
  return {
    valorCop: precioCop !== null && precioCop > 0 ? String(Math.round(precioCop)) : '',
    fechaPago: hoyColombia(),
    medio: '',
    referencia: '',
  }
}

// ---- Validación en cliente (mismas reglas y mensajes que la API) ----

export function validarPago(f: FormularioPago, hoy: string = hoyColombia()): ErroresPago {
  const errores: ErroresPago = {}
  const valor = f.valorCop.trim()
  const numero = Number(valor)
  if (!/^\d+$/.test(valor) || !Number.isSafeInteger(numero) || numero <= 0 || numero > VALOR_PAGO_MAX) {
    errores.valorCop = [MENSAJE_PAGO_VALOR]
  }
  if (!fechaIsoValida(f.fechaPago) || f.fechaPago > hoy) errores.fechaPago = [MENSAJE_PAGO_FECHA]
  if (!MEDIOS_PAGO.includes(f.medio as MedioPago)) errores.medio = [MENSAJE_PAGO_MEDIO]
  const referencia = f.referencia.trim()
  if (referencia.length > REFERENCIA_MAX || /[<>]/.test(referencia)) {
    errores.referencia = [MENSAJE_PAGO_REFERENCIA]
  } else if (PATRON_CONTROL.test(referencia)) {
    errores.referencia = [MENSAJE_PAGO_REFERENCIA_CONTROL]
  } else if (f.medio === 'OTRO' && referencia === '') {
    errores.referencia = [MENSAJE_PAGO_REFERENCIA_OTRO]
  }
  return errores
}

export function hayErroresPago(errores: ErroresPago): boolean {
  return CAMPOS_PAGO.some((c) => (errores[c]?.length ?? 0) > 0)
}

/** Cuerpo para la API (solo tras validar). */
export function pagoDesdeFormulario(f: FormularioPago): PagoRequest {
  const referencia = f.referencia.trim()
  return {
    valorCop: Number(f.valorCop.trim()),
    fechaPago: f.fechaPago,
    medio: f.medio as MedioPago,
    referencia: referencia === '' ? null : referencia,
  }
}

/** Reparte `errores` de un 400 entre los campos del pago (claves planas del contrato). */
export function erroresPagoDe(error: ApiError): ErroresPago {
  const resultado: ErroresPago = {}
  for (const [clave, mensajes] of Object.entries(error.errores)) {
    const limpia = clave.replace(/^pago\./i, '').toLowerCase()
    const campo = CAMPOS_PAGO.find((c) => c.toLowerCase() === limpia)
    if (campo) resultado[campo] = [...(resultado[campo] ?? []), ...mensajes]
  }
  return resultado
}

export function validarMotivoAnulacion(motivo: string): string[] {
  const largo = motivo.trim().length
  if (largo < MOTIVO_MIN || largo > MOTIVO_MAX) return [MENSAJE_MOTIVO_ANULACION]
  return PATRON_CONTROL.test(motivo.trim()) ? [MENSAJE_MOTIVO_CONTROL] : []
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

export function leerPago(valor: unknown): PagoItem | null {
  if (!esObjetoJson(valor)) return null
  const id = numeroONulo(valor.id)
  if (id === null) return null
  return {
    id,
    suscripcionId: numeroONulo(valor.suscripcionId) ?? 0,
    valorCop: numeroONulo(valor.valorCop) ?? 0,
    fechaPago: textoONulo(valor.fechaPago),
    medio: texto(valor.medio).toUpperCase(),
    referencia: textoONulo(valor.referencia),
    concepto: texto(valor.concepto).toUpperCase(),
    estado: texto(valor.estado).toUpperCase() || 'REGISTRADO',
    registradoEn: textoONulo(valor.registradoEn),
    registradoPor: textoONulo(valor.registradoPor),
    anuladoEn: textoONulo(valor.anuladoEn),
    anuladoPor: textoONulo(valor.anuladoPor),
    motivoAnulacion: textoONulo(valor.motivoAnulacion),
  }
}

const MENSAJES_PAGOS: Partial<Record<number, string>> = {
  401: MENSAJE_SESION_TERMINADA,
  404: 'No encontrado.',
  409: MENSAJE_PAGO_YA_ANULADO,
}

// ---- Endpoints (solo administrador) ----

export async function listarPagos(token: string, suscripcionId: number, signal?: AbortSignal): Promise<ListaPagos> {
  const respuesta = await peticion(`/api/admin/suscripciones/${encodeURIComponent(String(suscripcionId))}/pagos`, {
    token,
    signal,
    mensajesPorCodigo: MENSAJES_PAGOS,
  })
  if (!esObjetoJson(respuesta) || !Array.isArray(respuesta.items)) throw new ApiError(200, MENSAJE_INESPERADO)
  const items = respuesta.items.map(leerPago).filter((p): p is PagoItem => p !== null)
  const total =
    numeroONulo(respuesta.totalPagadoCop) ??
    items.filter((p) => p.estado === 'REGISTRADO').reduce((s, p) => s + p.valorCop, 0)
  return { items, totalPagadoCop: total }
}

/** Pago posterior (concepto POSTERIOR); 201 con el pago. */
export async function registrarPago(token: string, suscripcionId: number, pago: PagoRequest): Promise<PagoItem | null> {
  const respuesta = await peticion(`/api/admin/suscripciones/${encodeURIComponent(String(suscripcionId))}/pagos`, {
    method: 'POST',
    token,
    body: pago,
    mensajesPorCodigo: MENSAJES_PAGOS,
  })
  return leerPago(respuesta)
}

/** Anula un pago con motivo (5 a 500); la fila no se borra y la suscripción no cambia. */
export async function anularPago(token: string, pagoId: number, motivo: string): Promise<PagoItem | null> {
  const respuesta = await peticion(`/api/admin/pagos/${encodeURIComponent(String(pagoId))}/anular`, {
    method: 'POST',
    token,
    body: { motivo: motivo.trim() },
    mensajesPorCodigo: { ...MENSAJES_PAGOS, 404: MENSAJE_PAGO_NO_ENCONTRADO },
  })
  return leerPago(respuesta)
}
