// P5 (D-44, CA-P5-17): /privacidad. La política está en revisión legal y no se publica todavía: solo la franja, un
// aviso breve y el contacto confirmado (D-42). El borrador de docs/legal no se copia aquí hasta que esté aprobado.
// P7 (D-63 A): el contacto se lee de GET /api/portal/contacto, con el respaldo de D-42 si falla.
// P9 (CA-P9-18, 20, 35): franja "En revisión legal" desde una sola constante (src/legal.ts) y estructura común con
// /terminos (PaginaLegal), con los datos de la empresa como "Responsable" cuando están completos (D-83 A).
import { PaginaLegal } from './PaginaLegal'

export const TITULO_PRIVACIDAD = 'Política de tratamiento de datos personales'

export function Privacidad() {
  return (
    <PaginaLegal titulo={TITULO_PRIVACIDAD} clase="privacidad">
      <p>
        La política completa de tratamiento de datos personales se publicará en esta página cuando termine su revisión
        legal.
      </p>
      <p>Allí encontrarás cómo usamos los datos que nos dejas en el portal y cómo ejercer tus derechos sobre ellos.</p>
    </PaginaLegal>
  )
}
