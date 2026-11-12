// P6 — Solicitud de crédito (HU-039, HU-040) y búsqueda "Busco un vehículo" (HU-034).
// Contrato: docs/criterios/P6-contrato-api.md. D-54 A, D-55 B (crédito sin datos financieros), D-56 a A (búsqueda:
// categoría obligatoria y el resto opcional), D-58 A (siempre Administración).
// Las dos rutas son anónimas; si hay sesión se envía el token para ligar la solicitud a la cuenta (D-59 a A).
// La API vuelve a validar todo: la validación de aquí solo evita viajes inútiles y usa los mismos mensajes.
import { categoriasDisponibles, type OpcionesFiltros } from './catalogo'
import {
  CORREO_MAX,
  MENSAJE_CONTACTO_FALLO,
  MENSAJE_DEMASIADO_GRANDE,
  MENSAJE_MUCHAS_SOLICITUDES,
  MENSAJE_REVISA_DATOS,
  MENSAJE_VEHICULO_NO_DISPONIBLE,
  MENSAJE_VEHICULO_PROPIO,
  MENSAJES_CONTACTO,
  NOMBRE_MAX,
  NOMBRE_MIN,
  normalizarTelefono,
} from './contactos'
import { ApiError, esObjetoJson, MENSAJE_INESPERADO, peticion, type ErroresPorCampo } from './http'

// ---- Tipos del contrato ----

/** POST /api/creditos. Sin monto, cuota, ingresos ni tipo de vehículo (D-55 B, CA-P6-12). */
export type CreditoRequest = {
  /** null = crédito desde la portada; con valor = desde la ficha de ese vehículo. */
  slug: string | null
  nombre: string
  telefono: string
  correo: string | null
  /** El comentario opcional. */
  mensaje: string | null
  autorizacionDatos: boolean
  /** Campo trampa: una persona nunca lo ve ni lo llena. */
  sitioWeb: string
}

/** POST /api/busquedas. Los criterios usan los nombres del filtro del catálogo. */
export type BusquedaRequest = {
  nombre: string
  telefono: string
  correo: string | null
  tipoId: number
  marcaId: number | null
  lineaId: number | null
  anioDesde: number | null
  anioHasta: number | null
  presupuestoMaximo: number | null
  departamentoId: number | null
  municipioId: number | null
  descripcion: string | null
  autorizacionDatos: boolean
  sitioWeb: string
}

// ---- Límites y textos ----

export const COMENTARIO_MAX = 500
export const DESCRIPCION_BUSQUEDA_MAX = 500
export const ANIO_BUSQUEDA_MIN = 1950
export const PRESUPUESTO_MIN = 1_000_000
export const PRESUPUESTO_MAX = 5_000_000_000

/** Año máximo admitido: el actual + 1 (contrato). */
export function anioBusquedaMax(hoy: Date = new Date()): number {
  return hoy.getFullYear() + 1
}

export const MENSAJE_SOLICITUD_OK =
  '¡Listo! Recibimos tu solicitud. Te contactaremos pronto por teléfono o WhatsApp.'
export const MENSAJE_COMENTARIO_LARGO = 'El comentario no puede pasar de 500 caracteres.'
export const MENSAJE_CATEGORIA = 'Elige la categoría del vehículo que buscas.'
export const MENSAJE_MARCA = 'Elige una marca de la lista.'
export const MENSAJE_MODELO = 'Elige un modelo de la lista.'
export const MENSAJE_ANIOS = 'Revisa el rango de años.'
export const MENSAJE_PRESUPUESTO =
  'Escribe un valor en pesos entre 1.000.000 y 5.000.000.000, o deja el campo vacío.'
export const MENSAJE_DEPARTAMENTO = 'Elige un departamento de la lista.'
export const MENSAJE_CIUDAD = 'Elige una ciudad de la lista.'
export const MENSAJE_DESCRIPCION_BUSQUEDA = 'La descripción no puede pasar de 500 caracteres.'

/** CA-P6-11 (texto del coordinador). */
export const AYUDA_COMENTARIO_CREDITO =
  'Cuéntanos qué vehículo te interesa. No escribas ingresos, documentos ni datos bancarios.'
/** CA-P6-13 (D-45): el portal no presta dinero ni promete aprobación, tasas ni plazos. */
export const TEXTO_INTERMEDIARIOS =
  'Somos intermediarios: gestionamos tu crédito ante bancos y entidades financieras. Te contactaremos para contarte las opciones y los requisitos.'

// ---- Validación común ----

const PATRON_CORREO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

type DatosPersona = { nombre: string; telefono: string; correo: string; autorizacionDatos: boolean }
type ErroresPersona = Partial<Record<'nombre' | 'telefono' | 'correo' | 'autorizacionDatos', string[]>>

function validarPersona(f: DatosPersona): ErroresPersona {
  const errores: ErroresPersona = {}
  const nombre = f.nombre.trim().length
  if (nombre < NOMBRE_MIN || nombre > NOMBRE_MAX) errores.nombre = [MENSAJES_CONTACTO.nombre]
  if (normalizarTelefono(f.telefono) === null) errores.telefono = [MENSAJES_CONTACTO.telefono]
  const correo = f.correo.trim()
  if (correo !== '' && (correo.length > CORREO_MAX || !PATRON_CORREO.test(correo))) {
    errores.correo = [MENSAJES_CONTACTO.correo]
  }
  if (!f.autorizacionDatos) errores.autorizacionDatos = [MENSAJES_CONTACTO.autorizacionDatos]
  return errores
}

function persona(f: DatosPersona, sitioWeb: string) {
  const correo = f.correo.trim()
  return {
    nombre: f.nombre.trim(),
    telefono: f.telefono.trim(),
    correo: correo === '' ? null : correo,
    autorizacionDatos: f.autorizacionDatos,
    sitioWeb,
  }
}

/** Reparte los errores del servidor entre los campos conocidos (sin importar mayúsculas). */
export function repartirErrores<C extends string>(
  campos: readonly C[],
  errores: ErroresPorCampo,
): { porCampo: Partial<Record<C, string[]>>; otros: string[] } {
  const porCampo: Partial<Record<C, string[]>> = {}
  const otros: string[] = []
  for (const [clave, mensajes] of Object.entries(errores)) {
    const campo = campos.find((c) => c.toLowerCase() === clave.toLowerCase())
    if (campo) porCampo[campo] = [...(porCampo[campo] ?? []), ...mensajes]
    else otros.push(...mensajes)
  }
  return { porCampo, otros }
}

// ---- Crédito ----

export type FormularioCredito = DatosPersona & { mensaje: string }
export type CampoCredito = 'nombre' | 'telefono' | 'correo' | 'mensaje' | 'autorizacionDatos'
/** Orden en pantalla: el foco va al primero con error. */
export const CAMPOS_CREDITO: readonly CampoCredito[] = ['nombre', 'telefono', 'correo', 'mensaje', 'autorizacionDatos']
export type ErroresCredito = Partial<Record<CampoCredito, string[]>>

export function validarCredito(f: FormularioCredito): ErroresCredito {
  const errores: ErroresCredito = validarPersona(f)
  if (f.mensaje.trim().length > COMENTARIO_MAX) errores.mensaje = [MENSAJE_COMENTARIO_LARGO]
  return errores
}

/** Cuerpo del crédito: textos recortados; correo y comentario vacíos van como null. */
export function datosCredito(slug: string | null, f: FormularioCredito, sitioWeb: string): CreditoRequest {
  const base = persona(f, sitioWeb)
  const mensaje = f.mensaje.trim()
  return {
    slug: slug !== null && slug.trim() !== '' ? slug : null,
    nombre: base.nombre,
    telefono: base.telefono,
    correo: base.correo,
    mensaje: mensaje === '' ? null : mensaje,
    autorizacionDatos: base.autorizacionDatos,
    sitioWeb: base.sitioWeb,
  }
}

// ---- Búsqueda ----

export type OpcionBusqueda = { id: number; nombre: string }
/** `tipoIds` null o vacío = modelo sin clasificar (vale para cualquier categoría, D-32). */
export type LineaBusqueda = OpcionBusqueda & { tipoIds: readonly number[] | null }
export type MarcaBusqueda = OpcionBusqueda & { lineas: readonly LineaBusqueda[] }
export type DepartamentoBusqueda = OpcionBusqueda & { municipios: readonly OpcionBusqueda[] }

/** Listas del formulario de búsqueda. */
export type OpcionesBusqueda = {
  tipos: readonly OpcionBusqueda[]
  marcas: readonly MarcaBusqueda[]
  /** DIVIPOLA (D-29). Vacío si no hay una fuente pública: entonces no se piden departamento ni ciudad. */
  departamentos: readonly DepartamentoBusqueda[]
}

export const OPCIONES_BUSQUEDA_VACIAS: OpcionesBusqueda = { tipos: [], marcas: [], departamentos: [] }

/**
 * Respaldo si falla /api/catalogo/listas-busqueda: las opciones públicas del catálogo (/api/catalogo/filtros), con
 * las cuatro categorías de D-17 y las marcas y modelos que tienen publicaciones. No traen la categoría de cada
 * modelo ni la DIVIPOLA, pero dejan enviar la búsqueda con la categoría.
 */
export function opcionesBusquedaDesdeFiltros(o: OpcionesFiltros): OpcionesBusqueda {
  return {
    tipos: categoriasDisponibles(o.tipos).map(({ tipo }) => ({ id: tipo.id, nombre: tipo.nombre })),
    marcas: o.marcas.map((m) => ({
      id: m.id,
      nombre: m.nombre,
      lineas: m.lineas.map((l) => ({ id: l.id, nombre: l.nombre, tipoIds: null })),
    })),
    departamentos: [],
  }
}

/** Modelos de la marca que valen para la categoría elegida (los no clasificados valen para todas). */
export function lineasPara(opciones: OpcionesBusqueda, marcaId: string, tipoId: string): readonly LineaBusqueda[] {
  const marca = opciones.marcas.find((m) => String(m.id) === marcaId)
  if (!marca) return []
  const tipo = Number(tipoId)
  return marca.lineas.filter(
    (l) => tipoId === '' || l.tipoIds === null || l.tipoIds.length === 0 || l.tipoIds.includes(tipo),
  )
}

export function municipiosPara(opciones: OpcionesBusqueda, departamentoId: string): readonly OpcionBusqueda[] {
  return opciones.departamentos.find((d) => String(d.id) === departamentoId)?.municipios ?? []
}

/** Todos los valores son texto ('' = sin valor); el presupuesto, solo dígitos. */
export type FormularioBusqueda = DatosPersona & {
  tipoId: string
  marcaId: string
  lineaId: string
  anioDesde: string
  anioHasta: string
  presupuestoMaximo: string
  departamentoId: string
  municipioId: string
  descripcion: string
}

export type CampoBusqueda =
  | 'tipoId'
  | 'marcaId'
  | 'lineaId'
  | 'anioDesde'
  | 'anioHasta'
  | 'presupuestoMaximo'
  | 'departamentoId'
  | 'municipioId'
  | 'descripcion'
  | 'nombre'
  | 'telefono'
  | 'correo'
  | 'autorizacionDatos'

/** Orden en pantalla: primero lo que busca, luego sus datos de contacto. */
export const CAMPOS_BUSQUEDA: readonly CampoBusqueda[] = [
  'tipoId',
  'marcaId',
  'lineaId',
  'anioDesde',
  'anioHasta',
  'presupuestoMaximo',
  'departamentoId',
  'municipioId',
  'descripcion',
  'nombre',
  'telefono',
  'correo',
  'autorizacionDatos',
]
export type ErroresBusqueda = Partial<Record<CampoBusqueda, string[]>>

function entero(valor: string): number | null {
  return /^\d+$/.test(valor) && Number.isSafeInteger(Number(valor)) ? Number(valor) : null
}

function contiene(lista: readonly OpcionBusqueda[], id: string): boolean {
  return lista.some((o) => String(o.id) === id)
}

export function validarBusqueda(
  f: FormularioBusqueda,
  opciones: OpcionesBusqueda,
  hoy: Date = new Date(),
): ErroresBusqueda {
  const errores: ErroresBusqueda = {}
  if (f.tipoId === '' || !contiene(opciones.tipos, f.tipoId)) errores.tipoId = [MENSAJE_CATEGORIA]
  if (f.marcaId !== '' && !contiene(opciones.marcas, f.marcaId)) errores.marcaId = [MENSAJE_MARCA]
  if (f.lineaId !== '' && (f.marcaId === '' || !contiene(lineasPara(opciones, f.marcaId, f.tipoId), f.lineaId))) {
    errores.lineaId = [MENSAJE_MODELO]
  }

  const maximo = anioBusquedaMax(hoy)
  const anio = (valor: string) => {
    const n = entero(valor)
    return n !== null && n >= ANIO_BUSQUEDA_MIN && n <= maximo ? n : null
  }
  const desde = f.anioDesde === '' ? null : anio(f.anioDesde)
  const hasta = f.anioHasta === '' ? null : anio(f.anioHasta)
  if (f.anioDesde !== '' && desde === null) errores.anioDesde = [MENSAJE_ANIOS]
  if (f.anioHasta !== '' && hasta === null) errores.anioHasta = [MENSAJE_ANIOS]
  if (desde !== null && hasta !== null && desde > hasta) errores.anioHasta = [MENSAJE_ANIOS]

  if (f.presupuestoMaximo !== '') {
    const n = entero(f.presupuestoMaximo)
    if (n === null || n < PRESUPUESTO_MIN || n > PRESUPUESTO_MAX) errores.presupuestoMaximo = [MENSAJE_PRESUPUESTO]
  }

  if (f.departamentoId !== '' && !contiene(opciones.departamentos, f.departamentoId)) {
    errores.departamentoId = [MENSAJE_DEPARTAMENTO]
  }
  if (f.municipioId !== '') {
    const valido =
      f.departamentoId !== ''
        ? contiene(municipiosPara(opciones, f.departamentoId), f.municipioId)
        : opciones.departamentos.some((d) => contiene(d.municipios, f.municipioId))
    if (!valido) errores.municipioId = [MENSAJE_CIUDAD]
  }

  if (f.descripcion.trim().length > DESCRIPCION_BUSQUEDA_MAX) errores.descripcion = [MENSAJE_DESCRIPCION_BUSQUEDA]
  return { ...errores, ...validarPersona(f) }
}

function numeroONulo(valor: string): number | null {
  return valor === '' ? null : entero(valor)
}

/** Cuerpo de la búsqueda. Solo se llama tras validar, así que la categoría es un número. */
export function datosBusqueda(f: FormularioBusqueda, sitioWeb: string): BusquedaRequest {
  const base = persona(f, sitioWeb)
  const descripcion = f.descripcion.trim()
  return {
    nombre: base.nombre,
    telefono: base.telefono,
    correo: base.correo,
    tipoId: entero(f.tipoId) ?? 0,
    marcaId: numeroONulo(f.marcaId),
    lineaId: numeroONulo(f.lineaId),
    anioDesde: numeroONulo(f.anioDesde),
    anioHasta: numeroONulo(f.anioHasta),
    presupuestoMaximo: numeroONulo(f.presupuestoMaximo),
    departamentoId: numeroONulo(f.departamentoId),
    municipioId: numeroONulo(f.municipioId),
    descripcion: descripcion === '' ? null : descripcion,
    autorizacionDatos: base.autorizacionDatos,
    sitioWeb: base.sitioWeb,
  }
}

// ---- Listas del formulario (Cambios al contrato P6, 2026-11-10) ----

export const RUTA_LISTAS_BUSQUEDA = '/api/catalogo/listas-busqueda'

function opcion(valor: unknown): OpcionBusqueda | null {
  if (!esObjetoJson(valor) || typeof valor.id !== 'number' || typeof valor.nombre !== 'string') return null
  return valor.nombre.trim() === '' ? null : { id: valor.id, nombre: valor.nombre }
}

function listaDe<T>(valor: unknown, leer: (v: unknown) => T | null): T[] {
  return Array.isArray(valor) ? valor.map(leer).filter((x): x is T => x !== null) : []
}

/**
 * GET /api/catalogo/listas-busqueda (anónima): categorías activas, marcas con sus modelos activos (`tipoIds` vacío =
 * sin clasificar, vale para cualquier categoría, D-32) y DIVIPOLA completo (D-29).
 */
export async function obtenerListasBusqueda(signal?: AbortSignal): Promise<OpcionesBusqueda> {
  const datos = await peticion(RUTA_LISTAS_BUSQUEDA, { signal })
  if (!esObjetoJson(datos) || !Array.isArray(datos.tipos)) throw new ApiError(200, MENSAJE_INESPERADO)
  return {
    tipos: listaDe(datos.tipos, opcion),
    marcas: listaDe(datos.marcas, (m): MarcaBusqueda | null => {
      const base = opcion(m)
      if (!base || !esObjetoJson(m)) return null
      return {
        ...base,
        lineas: listaDe(m.lineas, (l): LineaBusqueda | null => {
          const linea = opcion(l)
          if (!linea || !esObjetoJson(l)) return null
          const tipoIds = Array.isArray(l.tipoIds) ? l.tipoIds.filter((t): t is number => typeof t === 'number') : []
          return { ...linea, tipoIds }
        }),
      }
    }),
    departamentos: listaDe(datos.departamentos, (d): DepartamentoBusqueda | null => {
      const base = opcion(d)
      if (!base || !esObjetoJson(d)) return null
      return { ...base, municipios: listaDe(d.municipios, opcion) }
    }),
  }
}

// ---- Llamadas ----

export const RUTA_CREDITOS = '/api/creditos'
export const RUTA_BUSQUEDAS = '/api/busquedas'

/**
 * POST de una solicitud pública. Devuelve el mensaje de éxito (el mismo para nueva, repetida o trampa).
 * Lanza ApiError con el mensaje del contrato; 5xx y sin conexión, el de CA-P6-34 (sin trazas técnicas).
 */
async function enviarSolicitud(ruta: string, datos: unknown, token?: string): Promise<string> {
  try {
    const respuesta = await peticion(ruta, {
      method: 'POST',
      body: datos,
      token,
      mensajesPorCodigo: {
        400: MENSAJE_REVISA_DATOS,
        404: MENSAJE_VEHICULO_NO_DISPONIBLE,
        409: MENSAJE_VEHICULO_PROPIO,
        413: MENSAJE_DEMASIADO_GRANDE,
        429: MENSAJE_MUCHAS_SOLICITUDES,
      },
    })
    return esObjetoJson(respuesta) && typeof respuesta.mensaje === 'string' && respuesta.mensaje.trim() !== ''
      ? respuesta.mensaje
      : MENSAJE_SOLICITUD_OK
  } catch (error) {
    if (error instanceof ApiError && (error.status === 0 || error.status >= 500)) {
      throw new ApiError(error.status, MENSAJE_CONTACTO_FALLO)
    }
    throw error
  }
}

export function enviarCredito(datos: CreditoRequest, token?: string): Promise<string> {
  return enviarSolicitud(RUTA_CREDITOS, datos, token)
}

export function enviarBusqueda(datos: BusquedaRequest, token?: string): Promise<string> {
  return enviarSolicitud(RUTA_BUSQUEDAS, datos, token)
}
