// P10: textos y datos estructurados de la ficha y de la empresa (CA-P10-22, 23, 31, 61 a 67). Siguen la misma regla
// que la API usa al entregar `/vehiculo/<slug>` (D-88 A, `backend/Camion.Api/Seo/FichaSeo.cs`), para que el título y
// la descripción no cambien cuando la aplicación arranca sobre el HTML de la API.
import { CATEGORIAS, mostrarKilometraje, type DetalleVehiculo } from './api/catalogo'
import {
  enlaceRedSeguro,
  formatearCelular,
  HOSTS_INSTAGRAM,
  HOSTS_TIKTOK,
  type ContactoPortal,
} from './api/parametros'
import { formatoCop, formatoKm } from './catalogo/formato'
import { LOGO, MARCA, NOMBRE_SITIO, SUFIJO_TITULO } from './marca'
import { rutaCategoria, rutaVehiculo } from './rutas'
import { absoluta } from './seo'

export const MAX_TITULO = 65
export const MAX_DESCRIPCION = 160
const MAX_DESCRIPCION_JSONLD = 500
const MAX_IMAGENES = 10

/** CA-P10-31: `<título> | Financiamos tu Camión`, o sin el sufijo si pasa de 65 caracteres. */
export function conSufijo(titulo: string): string {
  const completo = `${titulo} | ${SUFIJO_TITULO}`
  return completo.length > MAX_TITULO ? titulo : completo
}

export const TITULO_VEHICULO_CARGANDO = conSufijo('Detalle del vehículo')
/** CA-P10-24: vehículo inexistente o que ya no está visible. */
export const TITULO_VEHICULO_NO_DISPONIBLE = conSufijo('Este vehículo no está disponible')

/** Corta en `max` caracteres (incluido "…") por palabra completa. */
export function recortar(texto: string, max: number): string {
  const limpio = texto.trim()
  if (limpio.length <= max) return limpio
  let corte = limpio.slice(0, max - 1)
  const espacio = corte.lastIndexOf(' ')
  if (espacio > 0) corte = corte.slice(0, espacio)
  return `${corte.replace(/[ ,.;:]+$/, '')}…`
}

function unir(separador: string, ...partes: (string | number | null | undefined)[]): string {
  return partes
    .map((p) => (p === null || p === undefined ? '' : String(p).trim()))
    .filter((p) => p !== '')
    .join(separador)
}

type DatosFicha = Pick<
  DetalleVehiculo,
  | 'slug'
  | 'titulo'
  | 'tipo'
  | 'marca'
  | 'linea'
  | 'modeloAnio'
  | 'kilometraje'
  | 'municipio'
  | 'departamento'
  | 'precioCop'
  | 'negociable'
  | 'estadoComercial'
>

/**
 * CA-P10-22: "Marca Línea Año en Municipio". Sin marca o sin línea (carrocerías y máquinas, D-19), el título de la
 * publicación en su lugar, sin año.
 */
export function nucleoTituloVehiculo(v: DatosFicha): string {
  const base = v.marca?.trim() && v.linea?.trim() ? unir(' ', v.marca, v.linea, v.modeloAnio) : v.titulo.trim()
  return v.municipio?.trim() ? `${base} en ${v.municipio.trim()}` : base
}

export function tituloVehiculo(v: DatosFicha): string {
  return conSufijo(nucleoTituloVehiculo(v))
}

function kmVisible(v: Pick<DatosFicha, 'tipo' | 'kilometraje'>): number | null {
  return v.kilometraje !== null && mostrarKilometraje(v.tipo, v.kilometraje) ? v.kilometraje : null
}

/**
 * CA-P10-23: "Categoría Marca Línea Año[, N km] en Municipio, Departamento. Precio: $ N[ negociable].[ Reservado.]
 * Escríbenos por WhatsApp o solicita tu crédito.", cortada en 160 por palabra completa. No usa la descripción libre.
 */
export function descripcionVehiculo(v: DatosFicha): string {
  let texto = unir(' ', v.tipo, v.marca, v.linea, v.modeloAnio)
  const km = kmVisible(v)
  if (km !== null) texto += `, ${formatoKm(km)}`
  const lugar = unir(', ', v.municipio, v.departamento)
  if (lugar !== '') texto += ` en ${lugar}`
  texto += `. Precio: ${formatoCop(v.precioCop)}${v.negociable ? ' negociable' : ''}.`
  if (v.estadoComercial === 'RESERVADO') texto += ' Reservado.'
  texto += ' Escríbenos por WhatsApp o solicita tu crédito.'
  return recortar(texto, MAX_DESCRIPCION)
}

function normalizar(texto: string): string {
  return texto
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .trim()
    .toLowerCase()
}

/** Categoría principal (D-17) del tipo, si es una de las cuatro. */
function categoriaDeTipo(tipo: string) {
  return CATEGORIAS.find((c) => normalizar(c.nombre) === normalizar(tipo)) ?? null
}

/**
 * CA-P10-64 a 66: Product + Vehicle con oferta. Sin placa, asesor, teléfono, WhatsApp, `seller` (D-45) ni
 * `itemCondition`. `origen` = origen del portal.
 */
export function jsonLdVehiculo(v: DetalleVehiculo, origen: string): Record<string, unknown> {
  const url = absoluta(rutaVehiculo(v.slug), origen)
  const datos: Record<string, unknown> = {
    '@context': 'https://schema.org',
    '@type': ['Product', 'Vehicle'],
    name: nucleoTituloVehiculo(v),
    url,
  }
  const imagenes = [...v.fotos]
    .sort((a, b) => a.orden - b.orden)
    .slice(0, MAX_IMAGENES)
    .map((f) => absoluta(f.url, origen))
  if (imagenes.length > 0) datos.image = imagenes
  datos.description = v.descripcionComercial?.trim()
    ? recortar(v.descripcionComercial, MAX_DESCRIPCION_JSONLD)
    : descripcionVehiculo(v)
  datos.category = v.tipo
  if (v.marca?.trim()) datos.brand = { '@type': 'Brand', name: v.marca.trim() }
  if (v.linea?.trim()) datos.model = v.linea.trim()
  if (v.modeloAnio !== null) datos.vehicleModelDate = String(v.modeloAnio)
  const km = kmVisible(v)
  if (km !== null) datos.mileageFromOdometer = { '@type': 'QuantitativeValue', value: km, unitCode: 'KMT' }
  if (v.combustible?.trim()) datos.fuelType = v.combustible.trim()
  datos.offers = {
    '@type': 'Offer',
    price: v.precioCop,
    priceCurrency: 'COP',
    availability: v.estadoComercial === 'RESERVADO' ? 'https://schema.org/LimitedAvailability' : 'https://schema.org/InStock',
    url,
    availableAtOrFrom: {
      '@type': 'Place',
      address: {
        '@type': 'PostalAddress',
        addressLocality: v.municipio ?? '',
        addressRegion: v.departamento ?? '',
        addressCountry: 'CO',
      },
    },
  }
  return datos
}

/** CA-P10-67: Inicio › Categoría › vehículo (sin categoría si el tipo no es una de las cuatro de D-17). */
export function jsonLdMigas(v: DatosFicha, origen: string): Record<string, unknown> {
  const elementos: { nombre: string; url: string }[] = [{ nombre: 'Inicio', url: `${origen}/` }]
  const categoria = categoriaDeTipo(v.tipo)
  if (categoria) elementos.push({ nombre: categoria.enlace, url: absoluta(rutaCategoria(categoria.slug), origen) })
  elementos.push({ nombre: nucleoTituloVehiculo(v), url: absoluta(rutaVehiculo(v.slug), origen) })
  return {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    itemListElement: elementos.map((e, i) => ({ '@type': 'ListItem', position: i + 1, name: e.nombre, item: e.url })),
  }
}

/**
 * CA-P10-61 a 63: Organization de la portada con el contacto vigente (o el de respaldo, D-42). Sin dirección física
 * ni AutoDealer (D-90 C); razón social y NIT solo si la empresa está completa (D-83 A).
 */
export function jsonLdOrganizacion(contacto: ContactoPortal, origen: string): Record<string, unknown> {
  const datos: Record<string, unknown> = {
    '@context': 'https://schema.org',
    '@type': 'Organization',
    name: MARCA,
    alternateName: NOMBRE_SITIO,
    url: `${origen}/`,
    logo: absoluta(LOGO.cuadradoPng, origen),
  }
  if (contacto.correo.trim() !== '') datos.email = contacto.correo.trim()
  if (/^\d{10}$/.test(contacto.whatsapp)) {
    datos.contactPoint = {
      '@type': 'ContactPoint',
      telephone: `+57 ${formatearCelular(contacto.whatsapp)}`,
      contactType: 'customer service',
      areaServed: 'CO',
      availableLanguage: 'es',
    }
  }
  const redes = [enlaceRedSeguro(contacto.instagram, HOSTS_INSTAGRAM), enlaceRedSeguro(contacto.tiktok, HOSTS_TIKTOK)].filter(
    (r): r is string => r !== null,
  )
  if (redes.length > 0) datos.sameAs = redes
  if (contacto.empresa) {
    datos.legalName = contacto.empresa.razonSocial
    datos.taxID = contacto.empresa.nit
  }
  return datos
}
