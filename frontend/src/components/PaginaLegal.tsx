// P9 (D-44, D-83 A, HU-085, HU-086; CA-P9-17 a CA-P9-21, CA-P9-35): estructura común de #/privacidad y #/terminos.
// Mientras los textos estén en revisión legal (TEXTOS_LEGALES_EN_REVISION) se muestra la franja, un aviso breve y el
// contacto vigente del portal. Nunca se copia texto de docs/legal/. Si el administrador completó razón social, NIT y
// dirección de notificaciones, se muestran como "Responsable" (como texto, nunca HTML).
import type { ReactNode } from 'react'
import { formatearCelular, formatearNit, type EmpresaPortal } from '../api/parametros'
import { TEXTO_FRANJA_REVISION, TEXTOS_LEGALES_EN_REVISION } from '../legal'
import { HASH_INICIO } from '../rutas'
import { useContactoPortal } from './useContactoPortal'

type PaginaLegalProps = {
  titulo: string
  /** Clase propia de la página (p. ej. "privacidad"). */
  clase: string
  /** Aviso breve mientras el texto está en revisión. */
  children: ReactNode
}

export function PaginaLegal({ titulo, clase, children }: PaginaLegalProps) {
  const contacto = useContactoPortal()
  return (
    <div className={`cat-pagina pagina-legal ${clase}`}>
      {TEXTOS_LEGALES_EN_REVISION && (
        <p className="franja-borrador" role="note">
          {TEXTO_FRANJA_REVISION}
        </p>
      )}
      <h1 className="cat-titulo" tabIndex={-1}>
        {titulo}
      </h1>
      <div className="privacidad-cuerpo">
        {children}
        <p>
          Mientras tanto, si tienes preguntas, escríbenos al correo{' '}
          <a href={`mailto:${contacto.correo}`}>{contacto.correo}</a> o por WhatsApp al{' '}
          <a href={contacto.whatsappEnlace} target="_blank" rel="noopener noreferrer">
            {formatearCelular(contacto.whatsapp)}
            <span className="sr-only"> (se abre en una pestaña nueva)</span>
          </a>
          .
        </p>
        {contacto.empresa && <ResponsableEmpresa empresa={contacto.empresa} />}
        <p>
          <a href={HASH_INICIO}>Ir al inicio</a>
        </p>
      </div>
    </div>
  )
}

function ResponsableEmpresa({ empresa }: { empresa: EmpresaPortal }) {
  return (
    <section className="pagina-legal-responsable" aria-labelledby="responsable-titulo">
      <h2 id="responsable-titulo">Responsable</h2>
      <dl>
        <div>
          <dt>Razón social</dt>
          <dd>{empresa.razonSocial}</dd>
        </div>
        <div>
          <dt>NIT</dt>
          <dd>{formatearNit(empresa.nit)}</dd>
        </div>
        <div>
          <dt>Dirección de notificaciones</dt>
          <dd>{empresa.direccion}</dd>
        </div>
      </dl>
    </section>
  )
}
