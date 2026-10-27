// Catálogo público de vehículos (contrato docs/criterios/P3-contrato-api.md).
// HU-001 a HU-012, RN-VE-012, RN-VE-013, RN-SE-005: endpoints anónimos y de
// solo lectura; nunca se envía el token.
import {
  ApiError,
  esObjetoJson,
  MENSAJE_INESPERADO,
  peticion,
  type ErroresPorCampo,
} from './http'

// ---- Tipos del contrato ----

export type EstadoComercial = 'DISPONIBLE' | 'RESERVADO'

export type TarjetaVehiculo = {
  slug: string
  titulo: string
  precioCop: number
  negociable: boolean
  estadoComercial: EstadoComercial
  destacado: boolean
  tipo: string
  marca: string
  linea: string | null
  modeloAnio: number | null
  kilometraje: number | null
  municipio: string | null
  departamento: string | null
  fotoPortada: string | null
  publicadaEn: string
}

export type FotoVehiculo = {
  url: string
  orden: number
  esPortada: boolean
}

export type DetalleVehiculo = TarjetaVehiculo & {
  descripcionComercial: string | null
  descripcionTecnica: string | null
  carroceria: string | null
  combustible: string | null
  capacidadCargaKg: number | null
  pesoBrutoKg: number | null
  cilindrajeCc: number | null
  fotos: FotoVehiculo[]
}

export type RespuestaInicio = {
  destacados: TarjetaVehiculo[]
  recientes: TarjetaVehiculo[]
}

export type PaginaResultados = {
  items: TarjetaVehiculo[]
  total: number
  pagina: number
  tamano: number
}

export type OpcionCatalogo = { id: number; nombre: string; total: number }

export type MarcaCatalogo = OpcionCatalogo & { lineas: OpcionCatalogo[] }

export type RangosCatalogo = {
  precioMin: number | null
  precioMax: number | null
  anioMin: number | null
  anioMax: number | null
  kmMax: number | null
}

export type OpcionesFiltros = {
  tipos: OpcionCatalogo[]
  marcas: MarcaCatalogo[]
  rangos: RangosCatalogo
}

/**
 * Filtros de búsqueda tal como viajan en la URL (`#/buscar?…`) y en la API.
 * Todos son texto; la cadena vacía significa "sin filtro".
 */
export type FiltrosCatalogo = {
  q: string
  tipoId: string
  marcaId: string
  lineaId: string
  precioMin: string
  precioMax: string
  anioMin: string
  anioMax: string
  kmMax: string
  orden: string
  pagina: string
}

export type ClaveFiltro = keyof FiltrosCatalogo

export const CLAVES_FILTROS: readonly ClaveFiltro[] = [
  'q',
  'tipoId',
  'marcaId',
  'lineaId',
  'precioMin',
  'precioMax',
  'anioMin',
  'anioMax',
  'kmMax',
  'orden',
  'pagina',
]

export const FILTROS_VACIOS: FiltrosCatalogo = {
  q: '',
  tipoId: '',
  marcaId: '',
  lineaId: '',
  precioMin: '',
  precioMax: '',
  anioMin: '',
  anioMax: '',
  kmMax: '',
  orden: '',
  pagina: '',
}

export const ORDENES: readonly { valor: string; nombre: string }[] = [
  { valor: 'recientes', nombre: 'Más recientes' },
  { valor: 'precio_asc', nombre: 'Menor precio' },
  { valor: 'precio_desc', nombre: 'Mayor precio' },
  { valor: 'anio_desc', nombre: 'Año más reciente' },
  { valor: 'km_asc', nombre: 'Menor kilometraje' },
]

export const TAMANO_PAGINA_CATALOGO = 12
export const Q_MAX = 100
export const ANIO_MIN = 1900
export const ANIO_MAX = 2200

// ---- Mensajes del contrato ----

export const MENSAJE_REVISA_FILTROS = 'Revisa los filtros.'
export const MENSAJE_Q_LARGO = 'La búsqueda no puede tener más de 100 caracteres.'
export const MENSAJE_PRECIO =
  'Escribe un precio válido en pesos (número entero mayor o igual a 0).'
export const MENSAJE_PRECIO_RANGO = 'El precio máximo debe ser mayor o igual al mínimo.'
export const MENSAJE_ANIO = 'Escribe un año entre 1900 y 2200.'
export const MENSAJE_ANIO_RANGO = 'El año final debe ser mayor o igual al inicial.'
export const MENSAJE_KM = 'Escribe un kilometraje válido (número entero mayor o igual a 0).'
export const MENSAJE_LINEA_MARCA = 'El modelo no corresponde a la marca elegida.'
export const MENSAJE_ORDEN = 'Orden no válido.'
export const MENSAJE_PAGINA = 'Página o tamaño no válidos.'
export const MENSAJE_NO_DISPONIBLE = 'Este vehículo no está disponible.'
export const MENSAJE_SIN_VEHICULOS = 'Pronto tendremos vehículos publicados.'
export const MENSAJE_SIN_RESULTADOS = 'No encontramos vehículos con esos filtros.'

// ---- Validación en cliente (mismos mensajes que el servidor) ----

function enteroNoNegativo(valor: string): number | null {
  return /^\d+$/.test(valor) && Number.isSafeInteger(Number(valor)) ? Number(valor) : null
}

function agregar(errores: ErroresPorCampo, campo: string, mensaje: string) {
  errores[campo] = [...(errores[campo] ?? []), mensaje]
}

/**
 * Valida los filtros antes de pedirlos. `opciones` (si ya se cargaron) permite
 * comprobar que el modelo pertenece a la marca elegida.
 */
export function validarFiltros(
  f: FiltrosCatalogo,
  opciones?: OpcionesFiltros | null,
): ErroresPorCampo {
  const errores: ErroresPorCampo = {}
  if (f.q.trim().length > Q_MAX) agregar(errores, 'q', MENSAJE_Q_LARGO)

  const precioMin = f.precioMin === '' ? null : enteroNoNegativo(f.precioMin)
  const precioMax = f.precioMax === '' ? null : enteroNoNegativo(f.precioMax)
  if (f.precioMin !== '' && precioMin === null) agregar(errores, 'precioMin', MENSAJE_PRECIO)
  if (f.precioMax !== '' && precioMax === null) agregar(errores, 'precioMax', MENSAJE_PRECIO)
  if (precioMin !== null && precioMax !== null && precioMax < precioMin) {
    agregar(errores, 'precioMax', MENSAJE_PRECIO_RANGO)
  }

  const anio = (valor: string) => {
    const n = enteroNoNegativo(valor)
    return n !== null && n >= ANIO_MIN && n <= ANIO_MAX ? n : null
  }
  const anioMin = f.anioMin === '' ? null : anio(f.anioMin)
  const anioMax = f.anioMax === '' ? null : anio(f.anioMax)
  if (f.anioMin !== '' && anioMin === null) agregar(errores, 'anioMin', MENSAJE_ANIO)
  if (f.anioMax !== '' && anioMax === null) agregar(errores, 'anioMax', MENSAJE_ANIO)
  if (anioMin !== null && anioMax !== null && anioMax < anioMin) {
    agregar(errores, 'anioMax', MENSAJE_ANIO_RANGO)
  }

  if (f.kmMax !== '' && enteroNoNegativo(f.kmMax) === null) agregar(errores, 'kmMax', MENSAJE_KM)

  if (f.lineaId !== '' && f.marcaId !== '' && opciones) {
    const marca = opciones.marcas.find((m) => String(m.id) === f.marcaId)
    if (marca && !marca.lineas.some((l) => String(l.id) === f.lineaId)) {
      agregar(errores, 'lineaId', MENSAJE_LINEA_MARCA)
    }
  }

  if (f.orden !== '' && !ORDENES.some((o) => o.valor === f.orden)) {
    agregar(errores, 'orden', MENSAJE_ORDEN)
  }
  if (f.pagina !== '') {
    const pagina = enteroNoNegativo(f.pagina)
    if (pagina === null || pagina < 1) agregar(errores, 'pagina', MENSAJE_PAGINA)
  }
  return errores
}

// ---- Validadores de forma de las respuestas ----

function texto(valor: unknown): string {
  return typeof valor === 'string' ? valor : ''
}

function textoONulo(valor: unknown): string | null {
  return typeof valor === 'string' && valor.trim() !== '' ? valor : null
}

function numeroONulo(valor: unknown): number | null {
  return typeof valor === 'number' && Number.isFinite(valor) ? valor : null
}

function lista(valor: unknown): unknown[] {
  return Array.isArray(valor) ? valor : []
}

function aTarjeta(valor: unknown): TarjetaVehiculo | null {
  if (!esObjetoJson(valor)) return null
  const slug = texto(valor.slug)
  const titulo = texto(valor.titulo)
  const precioCop = numeroONulo(valor.precioCop)
  if (slug === '' || titulo === '' || precioCop === null) return null
  return {
    slug,
    titulo,
    precioCop,
    negociable: valor.negociable === true,
    estadoComercial: valor.estadoComercial === 'RESERVADO' ? 'RESERVADO' : 'DISPONIBLE',
    destacado: valor.destacado === true,
    tipo: texto(valor.tipo),
    marca: texto(valor.marca),
    linea: textoONulo(valor.linea),
    modeloAnio: numeroONulo(valor.modeloAnio),
    kilometraje: numeroONulo(valor.kilometraje),
    municipio: textoONulo(valor.municipio),
    departamento: textoONulo(valor.departamento),
    fotoPortada: textoONulo(valor.fotoPortada),
    publicadaEn: texto(valor.publicadaEn),
  }
}

function aTarjetas(valor: unknown): TarjetaVehiculo[] {
  return lista(valor)
    .map(aTarjeta)
    .filter((t): t is TarjetaVehiculo => t !== null)
}

function aFoto(valor: unknown): FotoVehiculo | null {
  if (!esObjetoJson(valor)) return null
  const url = texto(valor.url)
  if (url === '') return null
  return { url, orden: numeroONulo(valor.orden) ?? 0, esPortada: valor.esPortada === true }
}

function aOpcion(valor: unknown): OpcionCatalogo | null {
  if (!esObjetoJson(valor)) return null
  const id = numeroONulo(valor.id)
  const nombre = texto(valor.nombre)
  if (id === null || nombre === '') return null
  return { id, nombre, total: numeroONulo(valor.total) ?? 0 }
}

function aOpciones(valor: unknown): OpcionCatalogo[] {
  return lista(valor)
    .map(aOpcion)
    .filter((o): o is OpcionCatalogo => o !== null)
}

const respuestaInvalida = () => new ApiError(200, MENSAJE_INESPERADO)

// ---- Llamadas ----

/** Cadena de consulta con los filtros no vacíos y el tamaño de página. */
export function consultaPublicaciones(f: FiltrosCatalogo): string {
  const params = new URLSearchParams()
  for (const clave of CLAVES_FILTROS) {
    const valor = clave === 'q' ? f.q.trim() : f[clave]
    if (valor !== '') params.set(clave, valor)
  }
  params.set('tamano', String(TAMANO_PAGINA_CATALOGO))
  return params.toString()
}

/** GET /api/catalogo/inicio — HU-001 a HU-003. */
export async function obtenerInicio(signal?: AbortSignal): Promise<RespuestaInicio> {
  const datos = await peticion('/api/catalogo/inicio', { signal })
  if (!esObjetoJson(datos)) throw respuestaInvalida()
  return { destacados: aTarjetas(datos.destacados), recientes: aTarjetas(datos.recientes) }
}

/** GET /api/catalogo/publicaciones — HU-004 a HU-012. Un 400 trae `errores` por parámetro. */
export async function buscarPublicaciones(
  filtros: FiltrosCatalogo,
  signal?: AbortSignal,
): Promise<PaginaResultados> {
  const datos = await peticion(`/api/catalogo/publicaciones?${consultaPublicaciones(filtros)}`, {
    signal,
    mensajesPorCodigo: { 400: MENSAJE_REVISA_FILTROS },
  })
  if (!esObjetoJson(datos) || !Array.isArray(datos.items)) throw respuestaInvalida()
  return {
    items: aTarjetas(datos.items),
    total: numeroONulo(datos.total) ?? 0,
    pagina: numeroONulo(datos.pagina) ?? 1,
    tamano: numeroONulo(datos.tamano) ?? TAMANO_PAGINA_CATALOGO,
  }
}

/** GET /api/catalogo/publicaciones/{slug}. Un 404 llega como ApiError con status 404. */
export async function obtenerDetalle(
  slug: string,
  signal?: AbortSignal,
): Promise<DetalleVehiculo> {
  const datos = await peticion(`/api/catalogo/publicaciones/${encodeURIComponent(slug)}`, {
    signal,
    mensajesPorCodigo: { 404: MENSAJE_NO_DISPONIBLE },
  })
  const tarjeta = aTarjeta(datos)
  if (!tarjeta || !esObjetoJson(datos)) throw respuestaInvalida()
  const fotos = lista(datos.fotos)
    .map(aFoto)
    .filter((f): f is FotoVehiculo => f !== null)
    .sort((a, b) => a.orden - b.orden)
  return {
    ...tarjeta,
    descripcionComercial: textoONulo(datos.descripcionComercial),
    descripcionTecnica: textoONulo(datos.descripcionTecnica),
    carroceria: textoONulo(datos.carroceria),
    combustible: textoONulo(datos.combustible),
    capacidadCargaKg: numeroONulo(datos.capacidadCargaKg),
    pesoBrutoKg: numeroONulo(datos.pesoBrutoKg),
    cilindrajeCc: numeroONulo(datos.cilindrajeCc),
    fotos,
  }
}

/** GET /api/catalogo/filtros — opciones de categoría, marca/modelo y rangos. */
export async function obtenerFiltros(signal?: AbortSignal): Promise<OpcionesFiltros> {
  const datos = await peticion('/api/catalogo/filtros', { signal })
  if (!esObjetoJson(datos)) throw respuestaInvalida()
  const marcas = lista(datos.marcas)
    .map((m): MarcaCatalogo | null => {
      const base = aOpcion(m)
      if (!base || !esObjetoJson(m)) return null
      return { ...base, lineas: aOpciones(m.lineas) }
    })
    .filter((m): m is MarcaCatalogo => m !== null)
  const r = esObjetoJson(datos.rangos) ? datos.rangos : {}
  return {
    tipos: aOpciones(datos.tipos),
    marcas,
    rangos: {
      precioMin: numeroONulo(r.precioMin),
      precioMax: numeroONulo(r.precioMax),
      anioMin: numeroONulo(r.anioMin),
      anioMax: numeroONulo(r.anioMax),
      kmMax: numeroONulo(r.kmMax),
    },
  }
}

/** Normaliza para comparar nombres sin tildes ni mayúsculas ("Camión" → "camion"). */
function normalizar(nombre: string): string {
  return nombre
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .trim()
    .toLowerCase()
}

/**
 * Categorías principales (D-17): nombre en `tipos` y texto del enlace.
 * Carros es HU-005 y Camiones HU-006; Carrocería y Maquinaria vienen de D-17.
 */
export const CATEGORIAS = [
  { nombre: 'Carro', enlace: 'Carros' },
  { nombre: 'Camión', enlace: 'Camiones' },
  { nombre: 'Carrocería', enlace: 'Carrocerías' },
  { nombre: 'Maquinaria', enlace: 'Maquinaria' },
] as const

export type NombreCategoria = (typeof CATEGORIAS)[number]['nombre']

/** Busca un tipo por nombre sin distinguir mayúsculas ni tildes. */
export function tipoPorNombre(
  tipos: readonly OpcionCatalogo[],
  nombre: NombreCategoria,
): OpcionCatalogo | undefined {
  const buscado = normalizar(nombre)
  return tipos.find((t) => normalizar(t.nombre) === buscado)
}

/** Categorías principales que existen en `tipos`, con su id; las que no existen se omiten. */
export function categoriasDisponibles(
  tipos: readonly OpcionCatalogo[],
): { tipo: OpcionCatalogo; enlace: string }[] {
  return CATEGORIAS.flatMap((c) => {
    const tipo = tipoPorNombre(tipos, c.nombre)
    return tipo ? [{ tipo, enlace: c.enlace }] : []
  })
}

/**
 * Regla de D-17 (tarjeta y ficha del detalle): el kilometraje no se muestra
 * si el tipo es Carrocería, ni si es Maquinaria con kilometraje 0.
 */
export function mostrarKilometraje(tipo: string, kilometraje: number | null): boolean {
  if (kilometraje === null) return false
  const t = normalizar(tipo)
  if (t === normalizar('Carrocería')) return false
  if (t === normalizar('Maquinaria') && kilometraje === 0) return false
  return true
}
