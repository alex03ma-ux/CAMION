// P7 (D-42, D-63 A; CA-P7-26, 38): datos de contacto vigentes del portal, leídos de GET /api/portal/contacto
// (anónima, sin token). Mientras carga o si la lectura falla, se usan los valores confirmados de D-42.
// P9 (nota §6 de P9-criterios): caché en memoria del módulo. El pie, las páginas legales y los mensajes de error usan
// el mismo contacto; se pide una sola vez por carga de la página y todos los componentes montados reciben el valor.
// Si la lectura falla no se guarda nada (el siguiente componente que se monte vuelve a intentarlo).
import { useEffect, useState } from 'react'
import { CONTACTO_PORTAL_RESPALDO, obtenerContactoPortal, type ContactoPortal } from '../api/parametros'
import { LEMA } from '../marca'

let enCache: ContactoPortal | null = null
let enCurso: Promise<ContactoPortal> | null = null
const oyentes = new Set<(c: ContactoPortal) => void>()

function publicar(c: ContactoPortal) {
  enCache = c
  for (const oyente of oyentes) oyente(c)
}

/** Una sola petición compartida; no se cancela al desmontar un componente porque otros pueden esperarla. */
function cargarContactoPortal(): Promise<ContactoPortal> {
  if (!enCurso) {
    const peticionActual = obtenerContactoPortal()
    enCurso = peticionActual
    peticionActual
      .then((c) => {
        if (enCurso === peticionActual) publicar(c)
      })
      .catch(() => {
        // Sin conexión o respuesta no válida: se mantiene el respaldo y no se guarda nada.
      })
      .finally(() => {
        if (enCurso === peticionActual) enCurso = null
      })
  }
  return enCurso
}

/**
 * P9: vuelve a leer el contacto vigente y lo comunica a los componentes montados (p. ej. el pie tras guardar
 * los parámetros en el panel del administrador).
 */
export function recargarContactoPortal(): void {
  enCurso = null
  void cargarContactoPortal()
}

/** Solo para pruebas: olvida el valor guardado y la petición en curso. */
export function olvidarContactoPortal(): void {
  enCache = null
  enCurso = null
  oyentes.clear()
}

export function useContactoPortal(): ContactoPortal {
  const [contacto, setContacto] = useState<ContactoPortal>(() => enCache ?? CONTACTO_PORTAL_RESPALDO)
  useEffect(() => {
    oyentes.add(setContacto)
    if (enCache) setContacto(enCache)
    else void cargarContactoPortal()
    return () => {
      oyentes.delete(setContacto)
    }
  }, [])
  return contacto
}

/** Texto prellenado del WhatsApp para renovar: breve y sin datos personales (CA-P7-26). */
export const TEXTO_WHATSAPP_RENOVAR = `Hola, quiero renovar mi plan de asesor en ${LEMA}.`

/** Enlace de wa.me con un texto prellenado. */
export function enlaceWhatsappConTexto(enlace: string, texto: string): string {
  return `${enlace}?text=${encodeURIComponent(texto)}`
}
