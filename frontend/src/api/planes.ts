// Planes de los asesores (contrato docs/criterios/P4p-contrato-api.md).
// HU-049 crear, HU-050 modificar (RN-PL-001), HU-051 activar o inactivar (RN-PL-002); D-35.
// P4b (D-38): cada plan incluye `limiteDestacados` (0 a 20).
// Todas las rutas exigen el rol ADMINISTRADOR en la API (política SoloAdministrador).
import { MENSAJE_SESION_TERMINADA } from './auth'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion, type ErroresPorCampo } from './http'

// ---- Tipos del contrato ----

export type Plan = {
  id: number
  nombre: string
  precioCop: number
  duracionMeses: number
  diasVigencia: number
  limitePublicaciones: number
  /** P4b (D-38): destacados a la vez que incluye el plan. */
  limiteDestacados: number
  esPrueba: boolean
  activo: boolean
  suscripcionesVivas: number
}

/** Cuerpo de POST y PUT. */
export type DatosPlan = {
  nombre: string
  precioCop: number
  duracionMeses: number
  limitePublicaciones: number
  limiteDestacados: number
  esPrueba: boolean
}

/** Lo que escribe el administrador (los números como texto, solo dígitos). */
export type FormularioPlan = {
  nombre: string
  precioCop: string
  duracionMeses: string
  limitePublicaciones: string
  limiteDestacados: string
  esPrueba: boolean
}

export type CampoPlan = keyof FormularioPlan
export type ErroresPlan = Partial<Record<CampoPlan, string[]>>

export const CAMPOS_PLAN: readonly CampoPlan[] = [
  'nombre',
  'precioCop',
  'duracionMeses',
  'limitePublicaciones',
  'limiteDestacados',
  'esPrueba',
]

// ---- Límites y mensajes del contrato ----

/** D-35: cada mes del plan da 30 días. */
export const DIAS_POR_MES = 30
export const NOMBRE_PLAN_MIN = 2
export const NOMBRE_PLAN_MAX = 100
export const DURACION_MIN = 1
export const DURACION_MAX = 36
export const LIMITE_MIN = 1
export const LIMITE_MAX = 1000
export const DESTACADOS_MIN = 0
export const DESTACADOS_MAX = 20

export const MENSAJE_PLAN_NOMBRE = 'Escribe el nombre del plan (2 a 100 caracteres).'
export const MENSAJE_PLAN_PRECIO = 'Escribe el precio en pesos (0 o más, sin decimales).'
export const MENSAJE_PLAN_DURACION = 'Escribe la duración en meses (de 1 a 36).'
export const MENSAJE_PLAN_LIMITE = 'Escribe el cupo de publicaciones (de 1 a 1.000).'
export const MENSAJE_PLAN_DESTACADOS = 'Escribe los destacados incluidos (de 0 a 20).'
export const MENSAJE_PLAN_NO_ENCONTRADO = 'No encontrado.'

export const FORMULARIO_PLAN_VACIO: FormularioPlan = {
  nombre: '',
  precioCop: '',
  duracionMeses: '',
  limitePublicaciones: '',
  limiteDestacados: '0',
  esPrueba: false,
}

/** D-35: días de vigencia de un plan de `meses` meses. */
export function diasDeMeses(meses: number): number {
  return DIAS_POR_MES * meses
}

function entero(textoNumero: string): number | null {
  const limpio = textoNumero.trim()
  if (!/^\d{1,12}$/.test(limpio)) return null
  const n = Number(limpio)
  return Number.isSafeInteger(n) ? n : null
}

/** Validación en el navegador (la API vuelve a validar). */
export function validarPlan(f: FormularioPlan): ErroresPlan {
  const errores: ErroresPlan = {}
  const nombre = f.nombre.trim().length
  if (nombre < NOMBRE_PLAN_MIN || nombre > NOMBRE_PLAN_MAX) errores.nombre = [MENSAJE_PLAN_NOMBRE]
  if (entero(f.precioCop) === null) errores.precioCop = [MENSAJE_PLAN_PRECIO]
  const duracion = entero(f.duracionMeses)
  if (duracion === null || duracion < DURACION_MIN || duracion > DURACION_MAX) {
    errores.duracionMeses = [MENSAJE_PLAN_DURACION]
  }
  const limite = entero(f.limitePublicaciones)
  if (limite === null || limite < LIMITE_MIN || limite > LIMITE_MAX) {
    errores.limitePublicaciones = [MENSAJE_PLAN_LIMITE]
  }
  const destacados = entero(f.limiteDestacados)
  if (destacados === null || destacados < DESTACADOS_MIN || destacados > DESTACADOS_MAX) {
    errores.limiteDestacados = [MENSAJE_PLAN_DESTACADOS]
  }
  return errores
}

/** Formulario ya validado → cuerpo de la API. */
export function datosDeFormulario(f: FormularioPlan): DatosPlan {
  return {
    nombre: f.nombre.trim(),
    precioCop: entero(f.precioCop) ?? 0,
    duracionMeses: entero(f.duracionMeses) ?? 0,
    limitePublicaciones: entero(f.limitePublicaciones) ?? 0,
    limiteDestacados: entero(f.limiteDestacados) ?? 0,
    esPrueba: f.esPrueba,
  }
}

export function formularioDePlan(p: Plan): FormularioPlan {
  return {
    nombre: p.nombre,
    precioCop: String(p.precioCop),
    duracionMeses: String(p.duracionMeses),
    limitePublicaciones: String(p.limitePublicaciones),
    limiteDestacados: String(p.limiteDestacados),
    esPrueba: p.esPrueba,
  }
}

/** Reparte los `errores` del 400 entre los campos (sin distinguir mayúsculas); el resto, aparte. */
export function repartirErroresPlan(errores: ErroresPorCampo): { porCampo: ErroresPlan; otros: string[] } {
  const porCampo: ErroresPlan = {}
  const otros: string[] = []
  for (const [clave, mensajes] of Object.entries(errores)) {
    const campo = CAMPOS_PLAN.find((c) => c.toLowerCase() === clave.toLowerCase())
    if (campo) porCampo[campo] = [...(porCampo[campo] ?? []), ...mensajes]
    else for (const m of mensajes) if (!otros.includes(m)) otros.push(m)
  }
  return { porCampo, otros }
}

// ---- Lectura de respuestas ----

function numero(valor: unknown, porDefecto: number): number {
  return typeof valor === 'number' && Number.isFinite(valor) ? valor : porDefecto
}

export function leerPlan(valor: unknown): Plan | null {
  if (!esObjetoJson(valor)) return null
  const id = valor.id
  if (typeof id !== 'number' || !Number.isFinite(id)) return null
  const duracionMeses = numero(valor.duracionMeses, 0)
  return {
    id,
    nombre: typeof valor.nombre === 'string' ? valor.nombre : '',
    precioCop: numero(valor.precioCop, 0),
    duracionMeses,
    diasVigencia: numero(valor.diasVigencia, diasDeMeses(duracionMeses)),
    limitePublicaciones: numero(valor.limitePublicaciones, 0),
    limiteDestacados: numero(valor.limiteDestacados, 0),
    esPrueba: valor.esPrueba === true,
    activo: valor.activo === true,
    suscripcionesVivas: numero(valor.suscripcionesVivas, 0),
  }
}

const MENSAJES_PLANES: Partial<Record<number, string>> = {
  401: MENSAJE_SESION_TERMINADA,
  404: MENSAJE_PLAN_NO_ENCONTRADO,
}

const BASE = '/api/admin/planes'

// ---- Endpoints ----

/** `activo` undefined = todos; true/false filtra. */
export async function listarPlanes(
  token: string,
  activo?: boolean,
  signal?: AbortSignal,
): Promise<Plan[]> {
  const ruta = activo === undefined ? BASE : `${BASE}?activo=${activo ? 'true' : 'false'}`
  const respuesta = await peticion(ruta, { token, signal, mensajesPorCodigo: MENSAJES_PLANES })
  const lista = esObjetoJson(respuesta) ? respuesta.items : respuesta
  if (!Array.isArray(lista)) throw new ApiError(200, MENSAJE_INESPERADO)
  return lista.map(leerPlan).filter((p): p is Plan => p !== null)
}

export async function crearPlan(token: string, datos: DatosPlan): Promise<Plan | null> {
  const respuesta = await peticion(BASE, {
    method: 'POST',
    token,
    body: datos,
    mensajesPorCodigo: MENSAJES_PLANES,
  })
  return leerPlan(respuesta)
}

export async function editarPlan(token: string, id: number, datos: DatosPlan): Promise<Plan | null> {
  const respuesta = await peticion(`${BASE}/${encodeURIComponent(String(id))}`, {
    method: 'PUT',
    token,
    body: datos,
    mensajesPorCodigo: MENSAJES_PLANES,
  })
  return leerPlan(respuesta)
}

export type AccionPlan = 'activar' | 'inactivar'

export async function cambiarEstadoPlan(
  token: string,
  id: number,
  accion: AccionPlan,
): Promise<Plan | null> {
  const respuesta = await peticion(`${BASE}/${encodeURIComponent(String(id))}/${accion}`, {
    method: 'POST',
    token,
    mensajesPorCodigo: MENSAJES_PLANES,
  })
  return leerPlan(respuesta)
}
