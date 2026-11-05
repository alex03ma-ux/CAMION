// P5 (D-44, CA-P5-13, CA-P5-14, CA-P5-18, CA-P5-19): casilla de autorización de tratamiento de datos.
// Componente común: el contacto de la ficha, los avisos de P5b y los registros lo reutilizan.
// Empieza siempre sin marcar (quien la usa no debe recordarla entre envíos) y la API vuelve a exigirla.
import type { Ref } from 'react'
import { HASH_PRIVACIDAD } from '../rutas'

type AutorizacionDatosProps = {
  id: string
  marcada: boolean
  onCambio: (marcada: boolean) => void
  errores?: string[]
  deshabilitado?: boolean
  ref?: Ref<HTMLInputElement>
}

export const TEXTO_AUTORIZACION = 'Autorizo el tratamiento de mis datos personales según la'
export const TEXTO_ENLACE_POLITICA = 'Política de tratamiento de datos'

export function AutorizacionDatos({
  id,
  marcada,
  onCambio,
  errores = [],
  deshabilitado = false,
  ref,
}: AutorizacionDatosProps) {
  const idError = `${id}-error`
  const tieneErrores = errores.length > 0
  return (
    <div className="campo campo-autorizacion">
      <div className="opcion-casilla autorizacion-casilla">
        <input
          ref={ref}
          id={id}
          name={id}
          type="checkbox"
          checked={marcada}
          onChange={(e) => onCambio(e.target.checked)}
          aria-required="true"
          aria-invalid={tieneErrores}
          aria-describedby={tieneErrores ? idError : undefined}
          disabled={deshabilitado}
        />
        <label htmlFor={id} className="campo-obligatorio">
          {TEXTO_AUTORIZACION}{' '}
          {/* Pestaña nueva para no perder lo escrito en el formulario (CA-P5-13). */}
          <a href={HASH_PRIVACIDAD} target="_blank" rel="noopener noreferrer">
            {TEXTO_ENLACE_POLITICA}
            <span className="sr-only"> (se abre en una pestaña nueva)</span>
          </a>
          .
        </label>
      </div>
      {tieneErrores && (
        <ul id={idError} className="campo-errores">
          {errores.map((m) => (
            <li key={m}>{m}</li>
          ))}
        </ul>
      )}
    </div>
  )
}
