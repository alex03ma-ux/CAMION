// P10 (CA-P10-82, D-89 A): completa el bloque de etiquetas de index.html al compilar (y en `npm run dev`).
//   <!--camion:robots-->          → `<meta name="robots" content="noindex">` salvo VITE_PERMITIR_INDEXACION=true.
//   <!--camion:og-absolutas-->    → og:url, og:image y twitter:image con URL absoluta, solo si VITE_URL_PUBLICA es
//                                   https (las redes no aceptan rutas relativas); si no, se omiten.
// Todo queda dentro de <!--camion:meta:inicio--> … <!--camion:meta:fin-->, el bloque que la API reemplaza en las
// fichas (D-88 A, CA-P10-75). Función pura: la usan vite.config.ts y las pruebas.

export type EntornoHtml = { VITE_URL_PUBLICA?: string; VITE_PERMITIR_INDEXACION?: string }

function escaparAtributo(valor: string): string {
  return valor.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

/** URL pública válida (https, sin ruta, consulta ni fragmento; sin barra final) o ''. */
export function urlPublicaValida(valor: string | undefined): string {
  const limpio = (valor ?? '').trim().replace(/\/+$/, '')
  return /^https:\/\/[^\s/?#"<>]+$/.test(limpio) ? limpio : ''
}

export function etiquetasHtml(html: string, entorno: EntornoHtml): string {
  const robots =
    entorno.VITE_PERMITIR_INDEXACION === 'true'
      ? ''
      : '<meta name="robots" content="noindex" data-camion="noindex" />'
  const publica = urlPublicaValida(entorno.VITE_URL_PUBLICA)
  const absolutas =
    publica === ''
      ? ''
      : [
          `<meta property="og:url" content="${escaparAtributo(`${publica}/`)}" data-camion="og:url" />`,
          `<meta property="og:image" content="${escaparAtributo(`${publica}/marca/og-portal.jpg`)}" data-camion="og:image" />`,
          `<meta name="twitter:image" content="${escaparAtributo(`${publica}/marca/og-portal.jpg`)}" data-camion="twitter:image" />`,
        ].join('\n    ')
  return poner(poner(html, 'robots', robots), 'og-absolutas', absolutas)
}

/** Sustituye `<!--camion:<nombre>-->`; si no hay nada que poner, quita la línea entera. */
function poner(html: string, nombre: string, contenido: string): string {
  const marca = `<!--camion:${nombre}-->`
  if (contenido !== '') return html.replace(marca, contenido)
  return html.replace(new RegExp(`^[ \\t]*${marca}\\r?\\n`, 'm'), '').replace(marca, '')
}
