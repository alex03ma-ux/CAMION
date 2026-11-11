// Piezas comunes de los formularios públicos sin cuenta: avisos de P5b, crédito y búsqueda de P6.
// - ConfirmacionSolicitud: tras un envío correcto el formulario se oculta y se ofrece cómo seguir (pedido de la
//   responsable, 2026-11-09, commit 12dea7e; CA-P6-14).
// - CampoTrampa: campo `sitioWeb` fuera de la vista, del tabulado y de los lectores de pantalla (CA-P5b-57, CA-P6-36).
import type { Ref } from 'react'
import { HASH_BUSCAR, HASH_INICIO } from '../rutas'
import { Aviso } from './Aviso'

type ConfirmacionSolicitudProps = {
  mensaje: string
  onEnviarOtra: () => void
  /** Recibe el foco al mostrarse (el mensaje está en role="status"). */
  ref?: Ref<HTMLDivElement>
  /** CA-P6-14: botón opcional "Escribir por WhatsApp" (no crea nada). */
  enlaceWhatsapp?: string
}

export function ConfirmacionSolicitud({ mensaje, onEnviarOtra, ref, enlaceWhatsapp }: ConfirmacionSolicitudProps) {
  return (
    <section className="contacto-form aviso-publico-form aviso-confirmacion" aria-label="Solicitud enviada">
      <div ref={ref} tabIndex={-1} className="contacto-aviso">
        <Aviso aviso={{ tipo: 'exito', texto: mensaje }} />
      </div>
      {enlaceWhatsapp && (
        <a className="boton-whatsapp aviso-confirmacion-whatsapp" href={enlaceWhatsapp} target="_blank" rel="noopener noreferrer">
          Escribir por WhatsApp
          <span className="sr-only"> (se abre en una pestaña nueva)</span>
        </a>
      )}
      <div className="aviso-confirmacion-acciones">
        <a className="inv-boton-enlace" href={HASH_INICIO}>
          Volver al inicio
        </a>
        <a className="inv-boton-enlace inv-boton-secundario" href={HASH_BUSCAR}>
          Ver vehículos
        </a>
        <button type="button" className="secondary-button aviso-confirmacion-otra" onClick={onEnviarOtra}>
          Enviar otra solicitud
        </button>
      </div>
    </section>
  )
}

type CampoTrampaProps = {
  id: string
  valor: string
  onCambio: (valor: string) => void
}

export function CampoTrampa({ id, valor, onCambio }: CampoTrampaProps) {
  return (
    <div className="campo-trampa" aria-hidden="true">
      <label htmlFor={id}>Sitio web (deja este campo vacío)</label>
      <input
        id={id}
        name="sitioWeb"
        type="text"
        tabIndex={-1}
        autoComplete="off"
        value={valor}
        onChange={(e) => onCambio(e.target.value)}
      />
    </div>
  )
}
