// P7 (D-42, D-63 A; CA-P7-26, 38): datos de contacto vigentes del portal, leídos de GET /api/portal/contacto
// (anónima, sin token). Mientras carga o si la lectura falla, se usan los valores confirmados de D-42.
import { useEffect, useState } from 'react'
import { CONTACTO_PORTAL_RESPALDO, obtenerContactoPortal, type ContactoPortal } from '../api/parametros'

export function useContactoPortal(): ContactoPortal {
  const [contacto, setContacto] = useState<ContactoPortal>(CONTACTO_PORTAL_RESPALDO)
  useEffect(() => {
    const control = new AbortController()
    obtenerContactoPortal(control.signal)
      .then((c) => setContacto(c))
      .catch(() => {
        // Sin conexión o respuesta no válida: se mantiene el respaldo.
      })
    return () => control.abort()
  }, [])
  return contacto
}

/** Texto prellenado del WhatsApp para renovar: breve y sin datos personales (CA-P7-26). */
export const TEXTO_WHATSAPP_RENOVAR = 'Hola, quiero renovar mi plan de asesor en Financiamos Tu Camión.'

/** Enlace de wa.me con un texto prellenado. */
export function enlaceWhatsappConTexto(enlace: string, texto: string): string {
  return `${enlace}?text=${encodeURIComponent(texto)}`
}
