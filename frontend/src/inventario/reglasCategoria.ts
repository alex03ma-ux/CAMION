// Reglas por categoría del inventario (contrato docs/criterios/P4a-contrato-api.md,
// tabla "Reglas por categoría", D-17 y D-19). Módulo puro: sin React ni red,
// para poder probarlo por separado. La API vuelve a validar todo.

/** Las cuatro categorías principales de `tipos_vehiculo` (D-17). */
export type Categoria = 'CARRO' | 'CAMION' | 'CARROCERIA' | 'MAQUINARIA'

/** Campos del cuerpo de creación/edición que dependen de la categoría. */
export type CampoVehiculo =
  | 'marcaId'
  | 'lineaId'
  | 'modeloAnio'
  | 'kilometraje'
  | 'horasUso'
  | 'placa'
  | 'carroceriaId'
  | 'largoM'
  | 'anchoM'
  | 'altoM'
  | 'materialId'
  | 'tipoMaquinaId'
  | 'combustibleId'
  | 'cilindrajeCc'
  | 'pesoBrutoKg'
  | 'capacidadCargaKg'
  | 'municipioId'
  | 'precioCop'
  | 'descripcionTecnica'

/** `no`: el campo no se muestra ni se envía (la API respondería 400). */
export type Regla = 'obligatorio' | 'opcional' | 'no'

/** Orden en que se muestran los campos en el formulario. */
export const CAMPOS_VEHICULO: readonly CampoVehiculo[] = [
  'marcaId',
  'lineaId',
  'modeloAnio',
  'tipoMaquinaId',
  'carroceriaId',
  'placa',
  'kilometraje',
  'horasUso',
  'largoM',
  'anchoM',
  'altoM',
  'materialId',
  'combustibleId',
  'cilindrajeCc',
  'pesoBrutoKg',
  'capacidadCargaKg',
  'municipioId',
  'precioCop',
  'descripcionTecnica',
]

const O = 'obligatorio' as const
const P = 'opcional' as const
const N = 'no' as const

/** Tabla del contrato, tal cual. */
export const REGLAS_CATEGORIA: Readonly<Record<Categoria, Readonly<Record<CampoVehiculo, Regla>>>> = {
  CARRO: {
    marcaId: O,
    lineaId: P,
    modeloAnio: O,
    kilometraje: O,
    horasUso: N,
    placa: P,
    carroceriaId: P,
    largoM: N,
    anchoM: N,
    altoM: N,
    materialId: N,
    tipoMaquinaId: N,
    combustibleId: P,
    cilindrajeCc: P,
    pesoBrutoKg: P,
    capacidadCargaKg: N,
    municipioId: O,
    precioCop: O,
    descripcionTecnica: P,
  },
  CAMION: {
    marcaId: O,
    lineaId: P,
    modeloAnio: O,
    kilometraje: O,
    horasUso: N,
    placa: P,
    carroceriaId: P,
    largoM: N,
    anchoM: N,
    altoM: N,
    materialId: N,
    tipoMaquinaId: N,
    combustibleId: P,
    cilindrajeCc: P,
    pesoBrutoKg: P,
    capacidadCargaKg: P,
    municipioId: O,
    precioCop: O,
    descripcionTecnica: P,
  },
  CARROCERIA: {
    marcaId: P,
    lineaId: P,
    modeloAnio: P,
    kilometraje: N,
    horasUso: N,
    placa: N,
    carroceriaId: O,
    largoM: P,
    anchoM: P,
    altoM: P,
    materialId: P,
    tipoMaquinaId: N,
    combustibleId: N,
    cilindrajeCc: N,
    pesoBrutoKg: N,
    capacidadCargaKg: P,
    municipioId: O,
    precioCop: O,
    descripcionTecnica: P,
  },
  MAQUINARIA: {
    marcaId: O,
    lineaId: P,
    modeloAnio: O,
    kilometraje: N,
    horasUso: P,
    placa: P,
    carroceriaId: N,
    largoM: N,
    anchoM: N,
    altoM: N,
    materialId: N,
    tipoMaquinaId: O,
    combustibleId: P,
    cilindrajeCc: P,
    pesoBrutoKg: P,
    capacidadCargaKg: P,
    municipioId: O,
    precioCop: O,
    descripcionTecnica: P,
  },
}

/** Nombre legible de cada categoría (para mensajes como "Este dato no aplica a Carrocería."). */
export const NOMBRES_CATEGORIA: Readonly<Record<Categoria, string>> = {
  CARRO: 'Carro',
  CAMION: 'Camión',
  CARROCERIA: 'Carrocería',
  MAQUINARIA: 'Maquinaria',
}

/** Normaliza para comparar nombres sin tildes ni mayúsculas ("Camión" → "camion"). */
function normalizar(nombre: string): string {
  return nombre
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .trim()
    .toLowerCase()
}

const POR_NOMBRE: Readonly<Record<string, Categoria>> = {
  carro: 'CARRO',
  carros: 'CARRO',
  camion: 'CAMION',
  camiones: 'CAMION',
  carroceria: 'CARROCERIA',
  carrocerias: 'CARROCERIA',
  maquinaria: 'MAQUINARIA',
}

/** Categoría de un tipo de `tipos_vehiculo` por su nombre; null si no es una de las cuatro. */
export function categoriaDeTipo(nombreTipo: string | null | undefined): Categoria | null {
  if (!nombreTipo) return null
  return POR_NOMBRE[normalizar(nombreTipo)] ?? null
}

export function reglaCampo(categoria: Categoria, campo: CampoVehiculo): Regla {
  return REGLAS_CATEGORIA[categoria][campo]
}

export function aplica(categoria: Categoria, campo: CampoVehiculo): boolean {
  return reglaCampo(categoria, campo) !== 'no'
}

export function esObligatorio(categoria: Categoria, campo: CampoVehiculo): boolean {
  return reglaCampo(categoria, campo) === 'obligatorio'
}

/** Campos que se muestran para la categoría, en el orden del formulario. */
export function camposAplicables(categoria: Categoria): CampoVehiculo[] {
  return CAMPOS_VEHICULO.filter((c) => aplica(categoria, c))
}

/**
 * Etiqueta del campo según la categoría (sin el sufijo "(opcional)").
 * La carrocería de un camión es su subtipo; la de una carrocería suelta es su tipo.
 */
export function etiquetaCampo(categoria: Categoria, campo: CampoVehiculo): string {
  switch (campo) {
    case 'marcaId':
      return categoria === 'CARROCERIA' ? 'Marca (fabricante)' : 'Marca'
    case 'lineaId':
      return 'Línea (modelo)'
    case 'modeloAnio':
      return 'Año'
    case 'kilometraje':
      return 'Kilometraje (km)'
    case 'horasUso':
      return 'Horas de uso (horómetro)'
    case 'placa':
      return 'Placa'
    case 'carroceriaId':
      return categoria === 'CARROCERIA' ? 'Tipo de carrocería' : 'Carrocería (subtipo)'
    case 'largoM':
      return 'Largo (m)'
    case 'anchoM':
      return 'Ancho (m)'
    case 'altoM':
      return 'Alto (m)'
    case 'materialId':
      return 'Material'
    case 'tipoMaquinaId':
      return 'Tipo de máquina'
    case 'combustibleId':
      return 'Combustible'
    case 'cilindrajeCc':
      return 'Cilindraje (cc)'
    case 'pesoBrutoKg':
      return 'Peso bruto (kg)'
    case 'capacidadCargaKg':
      return categoria === 'MAQUINARIA' ? 'Capacidad (kg)' : 'Capacidad de carga (kg)'
    case 'municipioId':
      return 'Municipio'
    case 'precioCop':
      return 'Precio de venta (COP)'
    case 'descripcionTecnica':
      return 'Descripción técnica'
  }
}

/** Etiqueta con " (opcional)" cuando el campo no es obligatorio, como en P1–P2. */
export function etiquetaConObligatoriedad(categoria: Categoria, campo: CampoVehiculo): string {
  const base = etiquetaCampo(categoria, campo)
  return esObligatorio(categoria, campo) ? base : `${base} (opcional)`
}

/** Mensaje del contrato para un dato que no se admite en la categoría. */
export function mensajeNoAplica(categoria: Categoria): string {
  return `Este dato no aplica a ${NOMBRES_CATEGORIA[categoria]}.`
}
