// P5b — Avisos públicos "Vende tu camión" (HU-N01) y "Compra tu seguro todo riesgo" (HU-N02).
// Contrato: docs/criterios/P5b-contrato-api.md. D-6, D-50 (tipo de vehículo, sin ciudad/marca/año, correo
// opcional, teléfono con la regla de P5), D-52 (siempre Administración).
// Las rutas son anónimas; si hay sesión se envía el token para ligar el aviso a la cuenta (CA-P5b-02, 12).
// La API vuelve a validar todo: la validación de aquí solo evita viajes inútiles y usa los mismos mensajes.
import {
  CORREO_MAX,
  MENSAJE_CONTACTO_FALLO,
  MENSAJE_DEMASIADO_GRANDE,
  MENSAJE_MUCHAS_SOLICITUDES,
  MENSAJE_REVISA_DATOS,
  MENSAJES_CONTACTO,
  NOMBRE_MAX,
  NOMBRE_MIN,
  normalizarTelefono,
} from './contactos'
import { ApiError, esObjetoJson, peticion, type ErroresPorCampo } from './http'

// ---- Tipos del contrato ----

export type TipoVehiculoAviso = 'CAMION' | 'CARRO' | 'MAQUINARIA' | 'OTRO'

export type VendeTuCamionRequest = {
  nombre: string
  telefono: string
  correo: string | null
  descripcion: string
  autorizacionDatos: boolean
  /** Campo trampa: una persona nunca lo ve ni lo llena. */
  sitioWeb: string
}

export type SeguroTodoRiesgoRequest = {
  nombre: string
  telefono: string
  correo: string | null
  tipoVehiculo: TipoVehiculoAviso
  autorizacionDatos: boolean
  sitioWeb: string
}

/** Qué aviso es: define la ruta, el campo propio (descripción o tipo de vehículo) y los textos. */
export type VarianteAviso = 'VENDE_TU_CAMION' | 'SEGURO_TODO_RIESGO'

export type CampoAviso = 'nombre' | 'telefono' | 'correo' | 'descripcion' | 'tipoVehiculo' | 'autorizacionDatos'
export type ErroresAviso = Partial<Record<CampoAviso, string[]>>

/** Orden en pantalla de cada aviso: el foco va al primero con error (CA-P5b-56). */
export const CAMPOS_AVISO: Record<VarianteAviso, readonly CampoAviso[]> = {
  VENDE_TU_CAMION: ['nombre', 'telefono', 'correo', 'descripcion', 'autorizacionDatos'],
  SEGURO_TODO_RIESGO: ['nombre', 'telefono', 'correo', 'tipoVehiculo', 'autorizacionDatos'],
}

export const DESCRIPCION_MAX = 500

/** D-50 (a): lista y etiquetas del contrato. Con "Otro" no se piden más datos (CA-P5b-16). */
export const TIPOS_VEHICULO_AVISO: readonly { valor: TipoVehiculoAviso; nombre: string }[] = [
  { valor: 'CAMION', nombre: 'Camión' },
  { valor: 'CARRO', nombre: 'Carro' },
  { valor: 'MAQUINARIA', nombre: 'Maquinaria' },
  { valor: 'OTRO', nombre: 'Otro' },
]

export function esTipoVehiculoAviso(valor: string): valor is TipoVehiculoAviso {
  return TIPOS_VEHICULO_AVISO.some((t) => t.valor === valor)
}

/** Etiqueta del tipo de vehículo ("Camión"…); el código tal cual si no se conoce. */
export function nombreTipoVehiculo(valor: string): string {
  return TIPOS_VEHICULO_AVISO.find((t) => t.valor === valor.toUpperCase())?.nombre ?? valor
}

// ---- Mensajes del contrato ----

export const MENSAJE_AVISO_OK =
  '¡Listo! Recibimos tu solicitud. Te contactaremos pronto por teléfono o WhatsApp.'
export const MENSAJE_DESCRIPCION_VACIA = 'Describe tu vehículo (máximo 500 caracteres).'
export const MENSAJE_DESCRIPCION_LARGA = 'La descripción no puede pasar de 500 caracteres.'
export const MENSAJE_TIPO_VEHICULO = 'Elige el tipo de vehículo.'

// ---- Validación (mismas reglas que la API) ----

const PATRON_CORREO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export type FormularioAviso = {
  nombre: string
  telefono: string
  correo: string
  /** Solo "Vende tu camión". */
  descripcion: string
  /** Solo el seguro: '' mientras no se elija. */
  tipoVehiculo: string
  autorizacionDatos: boolean
}

export function validarAviso(variante: VarianteAviso, f: FormularioAviso): ErroresAviso {
  const errores: ErroresAviso = {}
  const nombre = f.nombre.trim().length
  if (nombre < NOMBRE_MIN || nombre > NOMBRE_MAX) errores.nombre = [MENSAJES_CONTACTO.nombre]
  if (normalizarTelefono(f.telefono) === null) errores.telefono = [MENSAJES_CONTACTO.telefono]
  const correo = f.correo.trim()
  if (correo !== '' && (correo.length > CORREO_MAX || !PATRON_CORREO.test(correo))) {
    errores.correo = [MENSAJES_CONTACTO.correo]
  }
  if (variante === 'VENDE_TU_CAMION') {
    const descripcion = f.descripcion.trim()
    if (descripcion === '') errores.descripcion = [MENSAJE_DESCRIPCION_VACIA]
    else if (descripcion.length > DESCRIPCION_MAX) errores.descripcion = [MENSAJE_DESCRIPCION_LARGA]
  } else if (!esTipoVehiculoAviso(f.tipoVehiculo)) {
    errores.tipoVehiculo = [MENSAJE_TIPO_VEHICULO]
  }
  if (!f.autorizacionDatos) errores.autorizacionDatos = [MENSAJES_CONTACTO.autorizacionDatos]
  return errores
}

/** Reparte los errores del servidor entre los campos del aviso (sin importar mayúsculas). */
export function erroresDeServidorAviso(
  variante: VarianteAviso,
  errores: ErroresPorCampo,
): { porCampo: ErroresAviso; otros: string[] } {
  const porCampo: ErroresAviso = {}
  const otros: string[] = []
  for (const [clave, mensajes] of Object.entries(errores)) {
    const campo = CAMPOS_AVISO[variante].find((c) => c.toLowerCase() === clave.toLowerCase())
    if (campo) porCampo[campo] = [...(porCampo[campo] ?? []), ...mensajes]
    else otros.push(...mensajes)
  }
  return { porCampo, otros }
}

function comunes(f: FormularioAviso, sitioWeb: string) {
  const correo = f.correo.trim()
  return {
    nombre: f.nombre.trim(),
    telefono: f.telefono.trim(),
    correo: correo === '' ? null : correo,
    autorizacionDatos: f.autorizacionDatos,
    sitioWeb,
  }
}

/** Cuerpo de "Vende tu camión": textos recortados y correo vacío como null. */
export function datosVendeTuCamion(f: FormularioAviso, sitioWeb: string): VendeTuCamionRequest {
  const base = comunes(f, sitioWeb)
  return {
    nombre: base.nombre,
    telefono: base.telefono,
    correo: base.correo,
    descripcion: f.descripcion.trim(),
    autorizacionDatos: base.autorizacionDatos,
    sitioWeb: base.sitioWeb,
  }
}

/** Cuerpo del seguro. Solo se llama tras validar, así que el tipo es uno de la lista. */
export function datosSeguroTodoRiesgo(f: FormularioAviso, sitioWeb: string): SeguroTodoRiesgoRequest {
  const base = comunes(f, sitioWeb)
  return {
    nombre: base.nombre,
    telefono: base.telefono,
    correo: base.correo,
    tipoVehiculo: esTipoVehiculoAviso(f.tipoVehiculo) ? f.tipoVehiculo : 'OTRO',
    autorizacionDatos: base.autorizacionDatos,
    sitioWeb: base.sitioWeb,
  }
}

// ---- Llamadas ----

export const RUTA_VENDE_TU_CAMION = '/api/avisos/vende-tu-camion'
export const RUTA_SEGURO_TODO_RIESGO = '/api/avisos/seguro-todo-riesgo'

/**
 * POST del aviso. Devuelve el mensaje de éxito (el mismo para nuevo, repetido o trampa).
 * Lanza ApiError con el mensaje del contrato; 5xx y sin conexión, el de CA-P5b-30 (sin trazas).
 */
async function enviarAviso(ruta: string, datos: unknown, token?: string): Promise<string> {
  try {
    const respuesta = await peticion(ruta, {
      method: 'POST',
      body: datos,
      token,
      mensajesPorCodigo: {
        400: MENSAJE_REVISA_DATOS,
        413: MENSAJE_DEMASIADO_GRANDE,
        429: MENSAJE_MUCHAS_SOLICITUDES,
      },
    })
    return esObjetoJson(respuesta) && typeof respuesta.mensaje === 'string' && respuesta.mensaje.trim() !== ''
      ? respuesta.mensaje
      : MENSAJE_AVISO_OK
  } catch (error) {
    if (error instanceof ApiError && (error.status === 0 || error.status >= 500)) {
      throw new ApiError(error.status, MENSAJE_CONTACTO_FALLO)
    }
    throw error
  }
}

export function enviarVendeTuCamion(datos: VendeTuCamionRequest, token?: string): Promise<string> {
  return enviarAviso(RUTA_VENDE_TU_CAMION, datos, token)
}

export function enviarSeguroTodoRiesgo(datos: SeguroTodoRiesgoRequest, token?: string): Promise<string> {
  return enviarAviso(RUTA_SEGURO_TODO_RIESGO, datos, token)
}
