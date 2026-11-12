// P5 (D-44, CA-P5-17): #/privacidad. La política está en revisión legal y no se publica todavía:
// solo una franja de borrador, un aviso breve y el contacto confirmado (D-42). El borrador de
// docs/legal no se copia aquí hasta que esté aprobado.
import { CONTACTO_CORREO, CONTACTO_WHATSAPP, CONTACTO_WHATSAPP_ENLACE } from './PiePortal'
import { HASH_INICIO } from '../rutas'

export const TEXTO_FRANJA_BORRADOR = 'BORRADOR — en revisión legal'

export function Privacidad() {
  return (
    <div className="cat-pagina privacidad">
      <p className="franja-borrador" role="note">
        {TEXTO_FRANJA_BORRADOR}
      </p>
      <h1 className="cat-titulo" tabIndex={-1}>
        Política de tratamiento de datos personales
      </h1>
      <div className="privacidad-cuerpo">
        <p>
          La política completa de tratamiento de datos personales se publicará en esta página cuando termine su
          revisión legal.
        </p>
        <p>
          Mientras tanto, si tienes preguntas sobre el uso de los datos que nos dejas en el portal, escríbenos al
          correo <a href={`mailto:${CONTACTO_CORREO}`}>{CONTACTO_CORREO}</a> o por WhatsApp al{' '}
          <a href={CONTACTO_WHATSAPP_ENLACE} target="_blank" rel="noopener noreferrer">
            {CONTACTO_WHATSAPP}
            <span className="sr-only"> (se abre en una pestaña nueva)</span>
          </a>
          .
        </p>
        <p>
          <a href={HASH_INICIO}>Ir al inicio</a>
        </p>
      </div>
    </div>
  )
}
