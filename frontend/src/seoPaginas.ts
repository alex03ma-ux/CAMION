// P10 (CA-P10-21 a 32): título, descripción, robots y canonical de cada página (tabla de §2.3 de P10-criterios).
// La ficha del vehículo la completa DetalleVehiculo al cargar sus datos (CA-P10-22 a 24).
import { CLAVES_FILTROS, type SlugCategoria } from './api/catalogo'
import { TITULO_BUSQUEDA } from './components/BusquedaVehiculo'
import { TITULO_MIS_INDICADORES } from './components/MisIndicadores'
import { TITULO_MIS_SOLICITUDES } from './components/MisSolicitudes'
import { TITULO_NO_ENCONTRADA } from './components/NoEncontrada'
import { TITULO_RESTABLECER } from './components/RestablecerContrasena'
import { TITULO_CREDITO_VEHICULO } from './components/SolicitudCredito'
import { TEXTOS_LEGALES_EN_REVISION } from './legal'
import { TITULO_POR_DEFECTO } from './marca'
import { rutaSinIndexar, type Ruta } from './rutas'
import { conSufijo, TITULO_VEHICULO_CARGANDO } from './seoDatos'
import type { MetaPagina } from './seo'

type TextoPagina = { titulo: string; descripcion: string }

/** Páginas públicas indexables con su texto propio (§2.3). */
export const TEXTOS_CATEGORIA: Record<SlugCategoria, TextoPagina> = {
  camiones: {
    titulo: 'Camiones en venta en Colombia',
    descripcion:
      'Camiones en venta en Colombia con fotos, precio y ciudad. Escríbenos por WhatsApp o solicita tu crédito para camión.',
  },
  carros: {
    titulo: 'Carros en venta en Colombia',
    descripcion: 'Carros en venta en Colombia con fotos, precio y ciudad. Escríbenos por WhatsApp o solicita tu crédito.',
  },
  carrocerias: {
    titulo: 'Carrocerías en venta para camión',
    descripcion: 'Carrocerías en venta en Colombia con fotos, precio y ciudad. Escríbenos por WhatsApp.',
  },
  maquinaria: {
    titulo: 'Maquinaria amarilla en venta',
    descripcion: 'Maquinaria amarilla en venta en Colombia con fotos, precio y ciudad. Escríbenos por WhatsApp.',
  },
}

export const TEXTO_VEHICULOS: TextoPagina = {
  titulo: 'Camiones y vehículos de trabajo en venta',
  descripcion:
    'Camiones, carros, carrocerías y maquinaria en venta en Colombia. Filtra por marca, modelo, año y precio, y escríbenos por WhatsApp.',
}

/** D-44, D-45: la descripción del crédito no promete aprobación ni tasas (la empresa es intermediaria). */
export const TEXTO_CREDITO: TextoPagina = {
  titulo: 'Crédito para camión',
  descripcion:
    'Solicita tu crédito para camión o vehículo de trabajo. Te ayudamos ante bancos y entidades financieras.',
}

export const TEXTO_VENDE: TextoPagina = {
  titulo: 'Vende tu camión',
  descripcion: 'Cuéntanos qué camión quieres vender y te contactamos por WhatsApp. No necesitas cuenta.',
}

export const TEXTO_SEGURO: TextoPagina = {
  titulo: 'Seguro todo riesgo para tu vehículo',
  descripcion: 'Déjanos tus datos y te ayudamos con el seguro todo riesgo de tu camión o vehículo.',
}

export const TEXTO_BUSCO: TextoPagina = {
  titulo: TITULO_BUSQUEDA,
  descripcion: 'Cuéntanos qué vehículo buscas y te contactamos con opciones. No necesitas cuenta.',
}

function indexable(texto: TextoPagina, canonica: string): MetaPagina {
  return { titulo: conSufijo(texto.titulo), descripcion: texto.descripcion, robots: null, canonica }
}

function sinIndexar(titulo: string): MetaPagina {
  return { titulo: conSufijo(titulo), descripcion: null, robots: 'noindex', canonica: null }
}

/**
 * CA-P10-26: en `/vehiculos` y en las categorías, solo `pagina` deja indexar (`?pagina=N` con N > 1 es su propia
 * canónica); con cualquier otro filtro, `noindex, follow` y sin canonical (CA-P10-27).
 */
function metaListado(texto: TextoPagina, base: string, filtros: Record<string, string>, ignorar: string[]): MetaPagina {
  const otros = CLAVES_FILTROS.filter((c) => !ignorar.includes(c) && c !== 'pagina' && (filtros[c] ?? '') !== '')
  if (otros.length > 0) return { titulo: conSufijo(texto.titulo), descripcion: texto.descripcion, robots: 'noindex, follow', canonica: null }
  const pagina = Number(filtros.pagina ?? '')
  const canonica = Number.isInteger(pagina) && pagina > 1 ? `${base}?pagina=${pagina}` : base
  return indexable(texto, canonica)
}

/** Títulos de las pantallas privadas (las de siempre, con el sufijo nuevo). */
function tituloPrivada(ruta: Ruta): string {
  switch (ruta.nombre) {
    case 'cuenta':
      return 'Mi cuenta'
    case 'inventario':
      return 'Inventario'
    case 'inventarioNuevo':
      return 'Registrar vehículo'
    case 'inventarioVehiculo':
      return 'Editar vehículo'
    case 'publicaciones':
      return 'Mis publicaciones'
    case 'contactos':
    case 'contacto':
      return 'Contactos'
    case 'misSolicitudes':
      return TITULO_MIS_SOLICITUDES
    case 'ventas':
      return 'Mis ventas'
    case 'misIndicadores':
      return TITULO_MIS_INDICADORES
    case 'restablecerContrasena':
      return TITULO_RESTABLECER
    case 'credito':
      return TITULO_CREDITO_VEHICULO
    default:
      return TITULO_NO_ENCONTRADA
  }
}

/**
 * Meta de la página de una ruta. `categoriaVacia` = la categoría no está en `/api/catalogo/filtros` (CA-P10-25).
 * Para `vehiculo` devuelve el estado "cargando"; DetalleVehiculo lo reemplaza al tener los datos.
 */
export function metaDeRuta(ruta: Ruta, { categoriaVacia = false }: { categoriaVacia?: boolean } = {}): MetaPagina {
  switch (ruta.nombre) {
    case 'inicio':
      return { titulo: TITULO_POR_DEFECTO, descripcion: null, robots: null, canonica: '/' }
    case 'buscar':
      if (ruta.categoria === null) return metaListado(TEXTO_VEHICULOS, '/vehiculos', ruta.filtros, [])
      if (categoriaVacia) return { ...indexable(TEXTOS_CATEGORIA[ruta.categoria], ''), robots: 'noindex', canonica: null }
      return metaListado(TEXTOS_CATEGORIA[ruta.categoria], `/${ruta.categoria}`, ruta.filtros, ['tipoId'])
    case 'vehiculo':
      return { titulo: TITULO_VEHICULO_CARGANDO, descripcion: null, robots: null, canonica: null }
    case 'credito':
      return ruta.slug === null ? indexable(TEXTO_CREDITO, '/credito') : sinIndexar(TITULO_CREDITO_VEHICULO)
    case 'vendeTuCamion':
      return indexable(TEXTO_VENDE, '/vende-tu-camion')
    case 'seguroTodoRiesgo':
      return indexable(TEXTO_SEGURO, '/seguro-todo-riesgo')
    case 'buscoVehiculo':
      return indexable(TEXTO_BUSCO, '/busco-un-vehiculo')
    // CA-P10-29: en revisión legal, noindex; al publicarse los textos aprobados, se indexan.
    case 'privacidad':
      return TEXTOS_LEGALES_EN_REVISION
        ? sinIndexar('Política de tratamiento de datos')
        : { ...sinIndexar('Política de tratamiento de datos'), robots: null, canonica: '/privacidad' }
    case 'terminos':
      return TEXTOS_LEGALES_EN_REVISION
        ? sinIndexar('Términos y condiciones')
        : { ...sinIndexar('Términos y condiciones'), robots: null, canonica: '/terminos' }
    default:
      // Privadas, restablecer contraseña y no encontrada: noindex (CA-P10-30).
      if (!rutaSinIndexar(ruta)) return sinIndexar(TITULO_NO_ENCONTRADA)
      return sinIndexar(tituloPrivada(ruta))
  }
}
