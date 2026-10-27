// Estado, validación en cliente y armado del cuerpo del formulario de vehículo
// (contrato P4a, HU-013, HU-015 a HU-019, RN-VE-002, RN-VE-003, RN-VE-005, D-2, D-19).
// Módulo puro: la API valida de nuevo y sus errores por campo se muestran igual.
import type {
  CatalogosInventario,
  DetalleInventario,
  VehiculoRequest,
} from '../api/inventario'
import type { ErroresPorCampo } from '../api/http'
import {
  aplica,
  CAMPOS_VEHICULO,
  esObligatorio,
  type Categoria,
  type CampoVehiculo,
} from './reglasCategoria'

/** Todos los valores como texto, tal como se escriben; el precio solo con dígitos. */
export type FormularioVehiculo = Record<CampoVehiculo, string> & {
  tipoId: string
  /** Solo lo elige el administrador al crear. */
  asesorId: string
  /** Solo sirve para filtrar los municipios; no se envía. */
  departamentoId: string
}

export type CampoFormulario = keyof FormularioVehiculo

export type ErroresFormulario = Partial<Record<CampoFormulario, string[]>>

export const ANIO_MIN_VEHICULO = 1950
export const PRECIO_MAX = 10_000_000_000
export const DESCRIPCION_MAX = 4000
export const MEDIDA_MAX = 9999.99
const ENTERO_MAX = 2_147_483_647

export const MENSAJE_REVISA_VEHICULO = 'Revisa los datos marcados.'

export const MENSAJES_CAMPO = {
  tipoId: 'Elige la categoría del vehículo.',
  asesorId: 'Elige el asesor propietario del vehículo.',
  marcaId: 'Elige la marca.',
  lineaMarca: 'La línea no corresponde a la marca elegida.',
  modeloAnio: (max: number) => `Escribe un año entre ${ANIO_MIN_VEHICULO} y ${max}.`,
  kilometraje: 'Escribe el kilometraje (número entero mayor o igual a 0).',
  horasUso: 'Escribe las horas de uso (número entero mayor o igual a 0).',
  placa: 'Escribe una placa válida (letras y números).',
  carroceriaId: 'Elige el tipo de carrocería.',
  medida: 'Escribe una medida en metros mayor que 0 (hasta 2 decimales).',
  tipoMaquinaId: 'Elige el tipo de máquina.',
  entero: 'Escribe un número entero mayor o igual a 0.',
  departamentoId: 'Elige el departamento.',
  municipioId: 'Elige el municipio.',
  precioCop: 'Escribe el precio de venta (número entero mayor que 0, hasta $ 10.000.000.000).',
  descripcionTecnica: `La descripción no puede tener más de ${DESCRIPCION_MAX} caracteres.`,
} as const

export function formularioVacio(): FormularioVehiculo {
  const base = Object.fromEntries(CAMPOS_VEHICULO.map((c) => [c, ''])) as Record<
    CampoVehiculo,
    string
  >
  return { ...base, tipoId: '', asesorId: '', departamentoId: '' }
}

function textoDe(valor: number | string | null): string {
  if (valor === null) return ''
  return String(valor)
}

/** Medida guardada (2.5) como texto con coma decimal ("2,5"). */
function medidaATexto(valor: number | null): string {
  return valor === null ? '' : String(valor).replace('.', ',')
}

/** Departamento del municipio, si el detalle no lo trae. */
export function departamentoDeMunicipio(
  catalogos: CatalogosInventario,
  municipioId: number | null,
): number | null {
  if (municipioId === null) return null
  const dep = catalogos.departamentos.find((d) => d.municipios.some((m) => m.id === municipioId))
  return dep ? dep.id : null
}

/** Valores iniciales del formulario de edición a partir del detalle. */
export function formularioDesdeDetalle(
  detalle: DetalleInventario,
  catalogos: CatalogosInventario,
): FormularioVehiculo {
  const departamento =
    detalle.departamentoId ?? departamentoDeMunicipio(catalogos, detalle.municipioId)
  return {
    tipoId: textoDe(detalle.tipoId),
    asesorId: detalle.asesor?.id ?? '',
    departamentoId: textoDe(departamento),
    marcaId: textoDe(detalle.marcaId),
    lineaId: textoDe(detalle.lineaId),
    modeloAnio: textoDe(detalle.modeloAnio),
    kilometraje: textoDe(detalle.kilometraje),
    horasUso: textoDe(detalle.horasUso),
    placa: detalle.placa ?? '',
    carroceriaId: textoDe(detalle.carroceriaId),
    largoM: medidaATexto(detalle.largoM),
    anchoM: medidaATexto(detalle.anchoM),
    altoM: medidaATexto(detalle.altoM),
    materialId: textoDe(detalle.materialId),
    tipoMaquinaId: textoDe(detalle.tipoMaquinaId),
    combustibleId: textoDe(detalle.combustibleId),
    cilindrajeCc: textoDe(detalle.cilindrajeCc),
    pesoBrutoKg: textoDe(detalle.pesoBrutoKg),
    capacidadCargaKg: textoDe(detalle.capacidadCargaKg),
    municipioId: textoDe(detalle.municipioId),
    precioCop: detalle.precioCop > 0 ? String(Math.round(detalle.precioCop)) : '',
    descripcionTecnica: detalle.descripcionTecnica ?? '',
  }
}

// ---- Conversión de valores ----

/** "abc-12 3" → "ABC123" (mayúsculas, sin espacios ni guiones), como la guarda la API. */
export function normalizarPlaca(placa: string): string {
  return placa.replace(/[\s-]+/g, '').toUpperCase()
}

export function placaValida(placa: string): boolean {
  return /^[A-Z0-9]{5,7}$/.test(normalizarPlaca(placa))
}

/** Entero ≥ 0 escrito con o sin puntos de miles; null si no es válido. */
export function leerEntero(texto: string): number | null {
  const limpio = texto.trim().replace(/[.\s]/g, '')
  if (!/^\d+$/.test(limpio)) return null
  const n = Number(limpio)
  return Number.isSafeInteger(n) && n <= ENTERO_MAX ? n : null
}

/** Medida en metros con coma o punto decimal, > 0 y hasta 2 decimales; null si no es válida. */
export function leerMedida(texto: string): number | null {
  const limpio = texto.trim().replace(',', '.')
  if (!/^\d+(\.\d{1,2})?$/.test(limpio)) return null
  const n = Number(limpio)
  return n > 0 && n <= MEDIDA_MAX ? n : null
}

/** Precio guardado como dígitos ("120000000"); null si no es entero > 0 y ≤ 10.000.000.000. */
export function leerPrecio(digitos: string): number | null {
  if (!/^\d+$/.test(digitos)) return null
  const n = Number(digitos)
  return Number.isSafeInteger(n) && n > 0 && n <= PRECIO_MAX ? n : null
}

function leerId(texto: string): number | null {
  if (!/^\d+$/.test(texto)) return null
  const n = Number(texto)
  return n > 0 ? n : null
}

export function anioMaximo(anioActual: number): number {
  return anioActual + 1
}

// ---- Validación ----

type OpcionesValidacion = {
  /** Al crear, el administrador debe elegir el asesor. */
  pedirAsesor: boolean
  anioActual: number
  catalogos: CatalogosInventario
}

/**
 * Valida en cliente según la categoría. Los campos que no aplican no se validan
 * (no se muestran ni se envían). Devuelve los errores por campo.
 */
export function validarFormulario(
  f: FormularioVehiculo,
  categoria: Categoria | null,
  { pedirAsesor, anioActual, catalogos }: OpcionesValidacion,
): ErroresFormulario {
  const errores: ErroresFormulario = {}
  const poner = (campo: CampoFormulario, mensaje: string) => {
    errores[campo] = [...(errores[campo] ?? []), mensaje]
  }

  if (pedirAsesor && f.asesorId.trim() === '') poner('asesorId', MENSAJES_CAMPO.asesorId)
  if (categoria === null || leerId(f.tipoId) === null) {
    poner('tipoId', MENSAJES_CAMPO.tipoId)
    return errores
  }

  const aplicaYHay = (campo: CampoVehiculo) => aplica(categoria, campo) && f[campo].trim() !== ''
  const falta = (campo: CampoVehiculo) => esObligatorio(categoria, campo) && f[campo].trim() === ''

  // Marca y línea
  if (falta('marcaId')) poner('marcaId', MENSAJES_CAMPO.marcaId)
  if (aplicaYHay('lineaId')) {
    const marca = catalogos.marcas.find((m) => String(m.id) === f.marcaId)
    if (!marca || !marca.lineas.some((l) => String(l.id) === f.lineaId)) {
      poner('lineaId', MENSAJES_CAMPO.lineaMarca)
    }
  }

  // Año
  const max = anioMaximo(anioActual)
  if (falta('modeloAnio')) poner('modeloAnio', MENSAJES_CAMPO.modeloAnio(max))
  else if (aplicaYHay('modeloAnio')) {
    const anio = leerEntero(f.modeloAnio)
    if (anio === null || anio < ANIO_MIN_VEHICULO || anio > max) {
      poner('modeloAnio', MENSAJES_CAMPO.modeloAnio(max))
    }
  }

  // Kilometraje y horas de uso
  if (falta('kilometraje')) poner('kilometraje', MENSAJES_CAMPO.kilometraje)
  else if (aplicaYHay('kilometraje') && leerEntero(f.kilometraje) === null) {
    poner('kilometraje', MENSAJES_CAMPO.kilometraje)
  }
  if (aplicaYHay('horasUso') && leerEntero(f.horasUso) === null) {
    poner('horasUso', MENSAJES_CAMPO.horasUso)
  }

  // Placa (D-2: opcional y única; la unicidad la comprueba la API con 409)
  if (aplicaYHay('placa') && !placaValida(f.placa)) poner('placa', MENSAJES_CAMPO.placa)

  // Listas obligatorias según la categoría
  if (falta('carroceriaId')) poner('carroceriaId', MENSAJES_CAMPO.carroceriaId)
  if (falta('tipoMaquinaId')) poner('tipoMaquinaId', MENSAJES_CAMPO.tipoMaquinaId)

  // Medidas de la carrocería
  for (const campo of ['largoM', 'anchoM', 'altoM'] as const) {
    if (aplicaYHay(campo) && leerMedida(f[campo]) === null) poner(campo, MENSAJES_CAMPO.medida)
  }

  // Enteros opcionales
  for (const campo of ['cilindrajeCc', 'pesoBrutoKg', 'capacidadCargaKg'] as const) {
    if (aplicaYHay(campo) && leerEntero(f[campo]) === null) poner(campo, MENSAJES_CAMPO.entero)
  }

  // Ubicación
  if (f.departamentoId === '') poner('departamentoId', MENSAJES_CAMPO.departamentoId)
  if (falta('municipioId')) poner('municipioId', MENSAJES_CAMPO.municipioId)

  // Precio (RN-VE-003)
  if (leerPrecio(f.precioCop) === null) poner('precioCop', MENSAJES_CAMPO.precioCop)

  if (f.descripcionTecnica.trim().length > DESCRIPCION_MAX) {
    poner('descripcionTecnica', MENSAJES_CAMPO.descripcionTecnica)
  }

  return errores
}

// ---- Cuerpo de la petición ----

/**
 * Arma el cuerpo de POST/PUT con solo los campos que aplican a la categoría
 * (los demás se omiten para no provocar "Este dato no aplica a …").
 * Debe llamarse con un formulario ya validado.
 */
export function construirCuerpo(
  f: FormularioVehiculo,
  categoria: Categoria,
  incluirAsesor: boolean,
): VehiculoRequest {
  const cuerpo: VehiculoRequest = {
    tipoId: leerId(f.tipoId) ?? 0,
    municipioId: leerId(f.municipioId) ?? 0,
    precioCop: leerPrecio(f.precioCop) ?? 0,
  }
  const usa = (campo: CampoVehiculo) => aplica(categoria, campo)
  const id = (campo: CampoVehiculo) => (f[campo].trim() === '' ? null : leerId(f[campo]))
  const entero = (campo: CampoVehiculo) => (f[campo].trim() === '' ? null : leerEntero(f[campo]))
  const medida = (campo: CampoVehiculo) => (f[campo].trim() === '' ? null : leerMedida(f[campo]))

  if (usa('marcaId')) cuerpo.marcaId = id('marcaId')
  if (usa('lineaId')) cuerpo.lineaId = id('lineaId')
  if (usa('modeloAnio')) cuerpo.modeloAnio = entero('modeloAnio')
  if (usa('kilometraje')) cuerpo.kilometraje = entero('kilometraje')
  if (usa('horasUso')) cuerpo.horasUso = entero('horasUso')
  if (usa('placa')) {
    const placa = normalizarPlaca(f.placa)
    cuerpo.placa = placa === '' ? null : placa
  }
  if (usa('carroceriaId')) cuerpo.carroceriaId = id('carroceriaId')
  if (usa('largoM')) cuerpo.largoM = medida('largoM')
  if (usa('anchoM')) cuerpo.anchoM = medida('anchoM')
  if (usa('altoM')) cuerpo.altoM = medida('altoM')
  if (usa('materialId')) cuerpo.materialId = id('materialId')
  if (usa('tipoMaquinaId')) cuerpo.tipoMaquinaId = id('tipoMaquinaId')
  if (usa('combustibleId')) cuerpo.combustibleId = id('combustibleId')
  if (usa('cilindrajeCc')) cuerpo.cilindrajeCc = entero('cilindrajeCc')
  if (usa('pesoBrutoKg')) cuerpo.pesoBrutoKg = entero('pesoBrutoKg')
  if (usa('capacidadCargaKg')) cuerpo.capacidadCargaKg = entero('capacidadCargaKg')
  const descripcion = f.descripcionTecnica.trim()
  cuerpo.descripcionTecnica = descripcion === '' ? null : descripcion
  // asesorId es perfiles_asesor.id: se envía como número si lo es.
  const asesor = f.asesorId.trim()
  if (incluirAsesor && asesor !== '') cuerpo.asesorId = /^\d+$/.test(asesor) ? Number(asesor) : asesor
  return cuerpo
}

// ---- Errores del servidor ----

const CAMPOS_FORMULARIO: readonly CampoFormulario[] = [
  'tipoId',
  'asesorId',
  'departamentoId',
  ...CAMPOS_VEHICULO,
]

/**
 * Reparte los errores del servidor: los de campos visibles van junto al campo
 * (sin distinguir mayúsculas: "Placa" o "placa"); el resto, al aviso general.
 */
export function repartirErroresServidor(
  errores: ErroresPorCampo,
  visible: (campo: CampoFormulario) => boolean,
): { porCampo: ErroresFormulario; otros: string[] } {
  const porCampo: ErroresFormulario = {}
  const otros: string[] = []
  for (const [clave, mensajes] of Object.entries(errores)) {
    const campo = CAMPOS_FORMULARIO.find((c) => c.toLowerCase() === clave.toLowerCase())
    if (campo && visible(campo)) porCampo[campo] = [...(porCampo[campo] ?? []), ...mensajes]
    else for (const m of mensajes) if (!otros.includes(m)) otros.push(m)
  }
  return { porCampo, otros }
}
