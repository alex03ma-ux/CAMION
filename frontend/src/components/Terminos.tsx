// P9 (D-44, HU-086, RN-PA-006; CA-P9-17, 19, 21, 35): /terminos. Los términos y condiciones están en revisión legal
// y no se publican todavía: franja "En revisión legal", un aviso breve y el contacto vigente. No exige sesión. Nunca se
// copia texto de docs/legal/TERMINOS-Y-CONDICIONES.md hasta que el abogado lo apruebe.
import { PaginaLegal } from './PaginaLegal'

export const TITULO_TERMINOS = 'Términos y condiciones de uso'

export function Terminos() {
  return (
    <PaginaLegal titulo={TITULO_TERMINOS} clase="terminos">
      <p>
        Los términos y condiciones de uso del portal se publicarán en esta página cuando termine su revisión legal.
      </p>
      <p>Allí encontrarás las reglas para consultar el catálogo, publicar vehículos y usar los servicios del portal.</p>
    </PaginaLegal>
  )
}
