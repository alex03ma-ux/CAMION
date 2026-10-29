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
  inputMode?: 'text' | 'email' | 'tel' | 'numeric' | 'search' | 'decimal'
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
      <label htmlFor={id} className={requerido ? 'campo-obligatorio' : undefined}>
        {etiqueta}
      </label>
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
      <label htmlFor={id} className={requerido ? 'campo-obligatorio' : undefined}>
        {etiqueta}
      </label>
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

type CampoAreaTextoProps = {
  id: string
  etiqueta: string
  valor: string
  onCambio: (valor: string) => void
  errores?: string[]
  maxLength?: number
  filas?: number
  /** Texto de ayuda bajo el campo (p. ej. "Hasta 4000 caracteres."); se le añade el conteo. */
  ayuda?: string
  deshabilitado?: boolean
  requerido?: boolean
}

/** P4b: área de texto con etiqueta, ayuda con el conteo de caracteres y errores asociados. */
export function CampoAreaTexto({
  id,
  etiqueta,
  valor,
  onCambio,
  errores = [],
  maxLength,
  filas = 4,
  ayuda,
  deshabilitado = false,
  requerido = true,
}: CampoAreaTextoProps) {
  const idError = `${id}-error`
  const idAyuda = `${id}-ayuda`
  const tieneErrores = errores.length > 0
  const describedBy = [ayuda !== undefined ? idAyuda : undefined, tieneErrores ? idError : undefined]
    .filter(Boolean)
    .join(' ')

  return (
    <div className="campo">
      <label htmlFor={id} className={requerido ? 'campo-obligatorio' : undefined}>
        {etiqueta}
      </label>
      <textarea
        id={id}
        name={id}
        rows={filas}
        value={valor}
        maxLength={maxLength}
        onChange={(e: ChangeEvent<HTMLTextAreaElement>) => onCambio(e.target.value)}
        aria-invalid={tieneErrores}
        aria-describedby={describedBy || undefined}
        aria-required={requerido ? 'true' : undefined}
        disabled={deshabilitado}
      />
      {ayuda !== undefined && (
        <p id={idAyuda} className="campo-ayuda">
          {ayuda} Llevas {valor.trim().length}.
        </p>
      )}
      <ListaErrores id={idError} errores={errores} />
    </div>
  )
}
