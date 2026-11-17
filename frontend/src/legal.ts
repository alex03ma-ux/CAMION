// P9 (D-44, HU-086, RN-PA-006; CA-P9-18, CA-P9-20): estado de los textos legales del portal.
// La política de tratamiento de datos y los términos están en revisión legal y no se publican todavía
// (los borradores de docs/legal/ nunca se copian al frontend). Esta es la única constante que controla la franja
// "En revisión legal" de #/privacidad y #/terminos: pasa a `false` en la entrega que publique los textos aprobados
// (docs/GUIA-PUBLICACION.md, CA-P9-66).
export const TEXTOS_LEGALES_EN_REVISION: boolean = true

/** Texto de la franja de las dos páginas legales (CA-P9-18). */
export const TEXTO_FRANJA_REVISION = 'En revisión legal'
