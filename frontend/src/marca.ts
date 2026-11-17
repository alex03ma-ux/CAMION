// P10 (CA-P10-13, D-87): textos de marca en un solo sitio. Títulos, pie, cuenta y mensajes prellenados los leen de
// aquí; para cambiar el nombre visible se cambia solo este archivo (y `index.html`, que no puede importarlo).

/** Empresa dueña de la marca (logo de D-87). */
export const MARCA = 'VCA Automotores'
/** Nombre visible del portal (D-87: título). */
export const NOMBRE_PORTAL = 'Venta de Camiones'
/** Lema (D-87). "tu" en minúscula, como en el logo. */
export const LEMA = 'Financiamos tu Camión'
/** Sufijo de los títulos de página (CA-P10-15): `<título> | Financiamos tu Camión`. */
export const SUFIJO_TITULO = LEMA
/** Nombre completo del sitio (manifiesto, `og:site_name`, `alternateName` del JSON-LD). */
export const NOMBRE_SITIO = `${NOMBRE_PORTAL} – ${LEMA}`

/** Texto alternativo del logo horizontal del encabezado (CA-P10-03). */
export const ALT_LOGO_HORIZONTAL = `${MARCA}, ${NOMBRE_PORTAL}. ${LEMA}`
/** Texto alternativo del logo cuadrado del pie (CA-P10-05). */
export const ALT_LOGO_CUADRADO = MARCA

/** CA-P10-14 (D-87, título confirmado): `<title>` por defecto, igual que `index.html`. */
export const TITULO_POR_DEFECTO = `Venta de camiones usados y crédito | ${SUFIJO_TITULO}`
/** CA-P9-52 (D-87): meta description por defecto, igual que `index.html` (149 caracteres). */
export const DESCRIPCION_POR_DEFECTO =
  'Camiones usados en venta en Colombia: NPR, volquetas, tractomulas y maquinaria. Solicita tu crédito para camión o tu seguro todo riesgo por WhatsApp.'

/** Archivos de marca (CA-P10-12: rutas absolutas, sirven en direcciones de varios niveles). */
export const LOGO = {
  horizontalWebp: '/marca/logo-horizontal.webp',
  horizontalPng: '/marca/logo-horizontal.png',
  /** Tamaño real del archivo (doble densidad): se muestra a la mitad. */
  horizontalAncho: 644,
  horizontalAlto: 112,
  cuadradoWebp: '/marca/logo-cuadrado.webp',
  cuadradoPng: '/marca/logo-cuadrado.png',
  cuadradoAncho: 320,
  cuadradoAlto: 292,
} as const
