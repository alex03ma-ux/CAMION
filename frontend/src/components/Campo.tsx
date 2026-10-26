import type { ChangeEvent, HTMLInputTypeAttribute, Ref } from 'react'

type CampoProps = {
  id: string
  etiqueta: string
  valor: string
  onCambio: (valor: string) => void
  tipo?: HTMLInputTypeAttribute
  errores?: string[]
  autoComplete?: string
  maxLength?: number
  inputMode?: 'text' | 'email' | 'tel'
  /** Ids de textos de ayuda adicionales (p. ej. la lista de reglas). */
  describidoPor?: string
  deshabilitado?: boolean
  ref?: Ref<HTMLInputElement>
}

/** Campo de formulario con etiqueta, errores asociados y atributos ARIA. */
export function Campo({
  id,
  etiqueta,
  valor,
  onCambio,
  tipo = 'text',
  errores = [],
  autoComplete,
  maxLength,
  inputMode,
  describidoPor,
  deshabilitado = false,
  ref,
}: CampoProps) {
  const idError = `${id}-error`
  const tieneErrores = errores.length > 0
  const describedBy = [describidoPor, tieneErrores ? idError : undefined]
    .filter(Boolean)
    .join(' ')

  return (
    <div className="campo">
      <label htmlFor={id}>{etiqueta}</label>
      <input
        ref={ref}
        id={id}
        name={id}
        type={tipo}
        value={valor}
        onChange={(e: ChangeEvent<HTMLInputElement>) => onCambio(e.target.value)}
        autoComplete={autoComplete}
        maxLength={maxLength}
        inputMode={inputMode}
        aria-invalid={tieneErrores}
        aria-describedby={describedBy || undefined}
        aria-required="true"
        disabled={deshabilitado}
      />
      {tieneErrores && (
        <ul id={idError} className="campo-errores">
          {errores.map((mensaje) => (
            <li key={mensaje}>{mensaje}</li>
          ))}
        </ul>
      )}
    </div>
  )
}
