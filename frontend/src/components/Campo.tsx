import type { ChangeEvent, HTMLInputTypeAttribute, Ref } from 'react'

function ListaErrores({ id, errores }: { id: string; errores: string[] }) {
  if (errores.length === 0) return null
  return (
    <ul id={id} className="campo-errores">
      {errores.map((mensaje) => (
        <li key={mensaje}>{mensaje}</li>
      ))}
    </ul>
  )
}

type CampoProps = {
  id: string
  etiqueta: string
  valor: string
  onCambio: (valor: string) => void
  tipo?: HTMLInputTypeAttribute
  errores?: string[]
  autoComplete?: string
  maxLength?: number
  inputMode?: 'text' | 'email' | 'tel' | 'numeric' | 'search'
  /** Ids de textos de ayuda adicionales (p. ej. la lista de reglas). */
  describidoPor?: string
  deshabilitado?: boolean
  /** Por defecto true; los campos opcionales no llevan aria-required. */
  requerido?: boolean
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
  requerido = true,
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
        aria-required={requerido ? 'true' : undefined}
        disabled={deshabilitado}
      />
      <ListaErrores id={idError} errores={errores} />
    </div>
  )
}

type CampoSelectProps = {
  id: string
  etiqueta: string
  valor: string
  onCambio: (valor: string) => void
  opciones: readonly { valor: string; nombre: string }[]
  /** Texto de la primera opción vacía (p. ej. "Elige una opción"). Si se omite, no hay opción vacía. */
  textoVacio?: string
  errores?: string[]
  describidoPor?: string
  deshabilitado?: boolean
  requerido?: boolean
  ref?: Ref<HTMLSelectElement>
}

/** Lista desplegable con etiqueta, errores asociados y atributos ARIA. */
export function CampoSelect({
  id,
  etiqueta,
  valor,
  onCambio,
  opciones,
  textoVacio,
  errores = [],
  describidoPor,
  deshabilitado = false,
  requerido = true,
  ref,
}: CampoSelectProps) {
  const idError = `${id}-error`
  const tieneErrores = errores.length > 0
  const describedBy = [describidoPor, tieneErrores ? idError : undefined]
    .filter(Boolean)
    .join(' ')

  return (
    <div className="campo">
      <label htmlFor={id}>{etiqueta}</label>
      <select
        ref={ref}
        id={id}
        name={id}
        value={valor}
        onChange={(e: ChangeEvent<HTMLSelectElement>) => onCambio(e.target.value)}
        aria-invalid={tieneErrores}
        aria-describedby={describedBy || undefined}
        aria-required={requerido ? 'true' : undefined}
        disabled={deshabilitado}
      >
        {textoVacio !== undefined && <option value="">{textoVacio}</option>}
        {opciones.map((o) => (
          <option key={o.valor} value={o.valor}>
            {o.nombre}
          </option>
        ))}
      </select>
      <ListaErrores id={idError} errores={errores} />
    </div>
  )
}
