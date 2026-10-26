// Llamadas y tipos de la API de autenticación
// (contratos docs/criterios/P1-contrato-api.md y P2-contrato-api.md).
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion } from './http'

// ---- Tipos del contrato ----

export type RegistroCompradorRequest = {
  nombreCompleto: string
  email: string
  telefono: string
  password: string
}

export type RegistroCompradorResponse = {
  usuarioId: string
  mensaje: string
}

/** Tipos de documento del asesor (contrato P2, PROPUESTO). */
export type TipoDocumento = 'CC' | 'CE' | 'NIT' | 'PAS'

export const TIPOS_DOCUMENTO: readonly { valor: TipoDocumento; nombre: string }[] = [
  { valor: 'CC', nombre: 'Cédula de ciudadanía' },
  { valor: 'CE', nombre: 'Cédula de extranjería' },
  { valor: 'NIT', nombre: 'NIT' },
  { valor: 'PAS', nombre: 'Pasaporte' },
]

export type RegistroAsesorRequest = RegistroCompradorRequest & {
  tipoDocumento: TipoDocumento
  numeroDocumento: string
  nombreComercial: string | null
}

export type RegistroAsesorResponse = RegistroCompradorResponse

/** Estados de `EstadoCuenta` (contrato P2). */
export type EstadoCuenta = 'ACTIVA' | 'PENDIENTE' | 'RECHAZADA' | 'BLOQUEADA'

export type LoginRequest = {
  email: string
  password: string
}

export type LoginResponse = {
  accessToken: string
  tokenType: string
  expiraEnUtc: string
  usuarioId: string
  nombreCompleto: string
  email: string
  roles: string[]
}

export type PerfilUsuario = {
  usuarioId: string
  nombreCompleto: string
  email: string
  phoneNumber: string
  roles: string[]
  /** P2: valor de `EstadoCuenta`; vacío si el servidor no lo envía. */
  estado: string
}

// ---- Mensajes del contrato (respaldo si el cuerpo no trae `error`) ----

export const MENSAJE_REGISTRO_OK = 'Cuenta creada. Ya puedes iniciar sesión.'
export const MENSAJE_CORREO_DUPLICADO = 'Este correo ya está registrado.'
export const MENSAJE_SOLICITUD_ASESOR_OK =
  'Solicitud enviada. Podrás iniciar sesión cuando el administrador apruebe tu cuenta.'
export const MENSAJE_DOCUMENTO_DUPLICADO = 'Ya existe un asesor con ese documento.'
export const MENSAJE_LOGIN_FALTAN_DATOS = 'Escribe tu correo y tu contraseña.'
export const MENSAJE_CREDENCIALES = 'Correo o contraseña incorrectos.'
export const MENSAJE_BLOQUEO =
  'Cuenta bloqueada temporalmente por varios intentos fallidos. Intenta de nuevo en 15 minutos.'
export const MENSAJE_SESION_TERMINADA = 'Tu sesión terminó. Inicia sesión de nuevo.'
export const MENSAJE_REVISA_DATOS = 'Revisa los datos marcados.'

// ---- Validadores de forma de las respuestas ----

function texto(valor: unknown): string {
  return typeof valor === 'string' ? valor : ''
}

function listaTextos(valor: unknown): string[] {
  return Array.isArray(valor) ? valor.filter((v): v is string => typeof v === 'string') : []
}

function respuestaInvalida(): ApiError {
  return new ApiError(200, MENSAJE_INESPERADO)
}

// ---- Endpoints ----

export async function registrarComprador(
  datos: RegistroCompradorRequest,
): Promise<RegistroCompradorResponse> {
  const respuesta = await peticion('/api/auth/registro/comprador', {
    method: 'POST',
    body: datos,
    mensajesPorCodigo: {
      400: MENSAJE_REVISA_DATOS,
      409: MENSAJE_CORREO_DUPLICADO,
    },
  })
  const cuerpo = esObjetoJson(respuesta) ? respuesta : {}
  return {
    usuarioId: texto(cuerpo.usuarioId),
    mensaje: texto(cuerpo.mensaje) || MENSAJE_REGISTRO_OK,
  }
}

export async function registrarAsesor(
  datos: RegistroAsesorRequest,
): Promise<RegistroAsesorResponse> {
  const respuesta = await peticion('/api/auth/registro/asesor', {
    method: 'POST',
    body: datos,
    mensajesPorCodigo: {
      400: MENSAJE_REVISA_DATOS,
      409: MENSAJE_CORREO_DUPLICADO,
    },
  })
  const cuerpo = esObjetoJson(respuesta) ? respuesta : {}
  return {
    usuarioId: texto(cuerpo.usuarioId),
    mensaje: texto(cuerpo.mensaje) || MENSAJE_SOLICITUD_ASESOR_OK,
  }
}

export async function iniciarSesion(datos: LoginRequest): Promise<LoginResponse> {
  const respuesta = await peticion('/api/auth/login', {
    method: 'POST',
    body: datos,
    mensajesPorCodigo: {
      400: MENSAJE_LOGIN_FALTAN_DATOS,
      401: MENSAJE_CREDENCIALES,
      423: MENSAJE_BLOQUEO,
    },
  })
  if (!esObjetoJson(respuesta)) throw respuestaInvalida()
  const accessToken = texto(respuesta.accessToken)
  const expiraEnUtc = texto(respuesta.expiraEnUtc)
  if (!accessToken || !expiraEnUtc) throw respuestaInvalida()
  return {
    accessToken,
    tokenType: texto(respuesta.tokenType) || 'Bearer',
    expiraEnUtc,
    usuarioId: texto(respuesta.usuarioId),
    nombreCompleto: texto(respuesta.nombreCompleto),
    email: texto(respuesta.email),
    roles: listaTextos(respuesta.roles),
  }
}

export async function obtenerPerfil(
  token: string,
  signal?: AbortSignal,
): Promise<PerfilUsuario> {
  const respuesta = await peticion('/api/auth/me', {
    token,
    signal,
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA },
  })
  if (!esObjetoJson(respuesta)) throw respuestaInvalida()
  return {
    usuarioId: texto(respuesta.usuarioId),
    nombreCompleto: texto(respuesta.nombreCompleto),
    email: texto(respuesta.email),
    phoneNumber: texto(respuesta.phoneNumber),
    roles: listaTextos(respuesta.roles),
    estado: texto(respuesta.estado),
  }
}

export async function cerrarSesionServidor(token: string): Promise<void> {
  await peticion('/api/auth/logout', {
    method: 'POST',
    token,
    mensajesPorCodigo: { 401: MENSAJE_SESION_TERMINADA },
  })
}

/** Un 401 en una llamada autenticada significa que la sesión ya no vale. */
export function esSesionTerminada(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401
}
