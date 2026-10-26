import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import {
  MOTIVO_MAX,
  motivoObligatorio,
  validarMotivo,
  type AccionUsuario,
  type UsuarioAdmin,
} from '../api/admin'
import { Aviso, type DatosAviso } from './Aviso'

/** Textos de cada acción del panel de administración (HU-043, HU-044, HU-047). */
export const TEXTOS_ACCION: Record<
  AccionUsuario,
  { boton: string; titulo: string; pregunta: (nombre: string) => string; enviando: string }
> = {
  aprobar: {
    boton: 'Aprobar',
    titulo: 'Aprobar cuenta de asesor',
    pregunta: (n) => `¿Aprobar la cuenta de asesor de ${n}? Podrá iniciar sesión de inmediato.`,
    enviando: 'Aprobando...',
  },
  rechazar: {
    boton: 'Rechazar',
    titulo: 'Rechazar solicitud de asesor',
    pregunta: (n) => `¿Rechazar la solicitud de asesor de ${n}? No podrá iniciar sesión.`,
    enviando: 'Rechazando...',
  },
  bloquear: {
    boton: 'Bloquear',
    titulo: 'Bloquear cuenta',
    pregunta: (n) =>
      `¿Bloquear la cuenta de ${n}? Sus sesiones abiertas se cerrarán en todos los equipos y no podrá iniciar sesión.`,
    enviando: 'Bloqueando...',
  },
  desbloquear: {
    boton: 'Desbloquear',
    titulo: 'Desbloquear cuenta',
    pregunta: (n) => `¿Desbloquear la cuenta de ${n}? Podrá volver a iniciar sesión.`,
    enviando: 'Desbloqueando...',
  },
  reconsiderar: {
    boton: 'Reconsiderar',
    titulo: 'Reconsiderar solicitud de asesor',
    pregunta: (n) => `¿Reconsiderar y aprobar la solicitud de asesor de ${n}? Podrá iniciar sesión de inmediato.`,
    enviando: 'Reconsiderando...',
  },
}

const SELECTOR_ENFOCABLES =
  'button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [href], [tabindex]:not([tabindex="-1"])'

type DialogoAccionProps = {
  usuario: UsuarioAdmin
  accion: AccionUsuario
  enviando: boolean
  /** Errores del servidor para el motivo (400). */
  erroresMotivo: string[]
  /** Error general que no cierra el diálogo (sin conexión, 5xx...). */
  error: DatosAviso | null
  onConfirmar: (motivo: string) => void
  onCancelar: () => void
}

/**
 * Cuadro de confirmación modal: foco atrapado dentro, Escape cancela y, al
 * cerrarse, quien lo abrió devuelve el foco al botón de origen.
 */
export function DialogoAccion({
  usuario,
  accion,
  enviando,
  erroresMotivo,
  error,
  onConfirmar,
  onCancelar,
}: DialogoAccionProps) {
  const textos = TEXTOS_ACCION[accion]
  const obligatorio = motivoObligatorio(accion)
  const [motivo, setMotivo] = useState('')
  const [erroresLocales, setErroresLocales] = useState<string[]>([])
  const refDialogo = useRef<HTMLDivElement>(null)
  const refMotivo = useRef<HTMLTextAreaElement>(null)

  const errores = erroresLocales.length > 0 ? erroresLocales : erroresMotivo
  const tieneErrores = errores.length > 0
  const idTitulo = 'dialogo-accion-titulo'
  const idDescripcion = 'dialogo-accion-descripcion'
  const idError = 'dialogo-motivo-error'
  const idAyuda = 'dialogo-motivo-ayuda'

  useEffect(() => {
    refMotivo.current?.focus()
  }, [])

  // Si el servidor rechaza el motivo, el foco vuelve al campo.
  useEffect(() => {
    if (erroresMotivo.length > 0) refMotivo.current?.focus()
  }, [erroresMotivo])

  function alTeclear(evento: KeyboardEvent<HTMLDivElement>) {
    if (evento.key === 'Escape') {
      evento.preventDefault()
      evento.stopPropagation()
      if (!enviando) onCancelar()
      return
    }
    if (evento.key !== 'Tab' || !refDialogo.current) return
    const enfocables = Array.from(
      refDialogo.current.querySelectorAll<HTMLElement>(SELECTOR_ENFOCABLES),
    )
    if (enfocables.length === 0) {
      evento.preventDefault()
      return
    }
    const primero = enfocables[0]
    const ultimo = enfocables[enfocables.length - 1]
    const activo = document.activeElement
    if (evento.shiftKey && (activo === primero || !refDialogo.current.contains(activo))) {
      evento.preventDefault()
      ultimo.focus()
    } else if (!evento.shiftKey && (activo === ultimo || !refDialogo.current.contains(activo))) {
      evento.preventDefault()
      primero.focus()
    }
  }

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (enviando) return
    const validacion = validarMotivo(accion, motivo)
    setErroresLocales(validacion)
    if (validacion.length > 0) {
      refMotivo.current?.focus()
      return
    }
    onConfirmar(motivo.trim())
  }

  const describedBy = [idAyuda, tieneErrores ? idError : undefined].filter(Boolean).join(' ')

  return (
    <div className="dialogo-fondo">
      <div
        ref={refDialogo}
        className="dialogo"
        role="dialog"
        aria-modal="true"
        aria-labelledby={idTitulo}
        aria-describedby={idDescripcion}
        onKeyDown={alTeclear}
      >
        <form className="dialogo-form" onSubmit={enviar} noValidate aria-busy={enviando}>
          <h3 id={idTitulo}>{textos.titulo}</h3>
          <p id={idDescripcion}>{textos.pregunta(usuario.nombreCompleto || usuario.email)}</p>

          <label htmlFor="dialogo-motivo">{obligatorio ? 'Motivo' : 'Motivo (opcional)'}</label>
          <textarea
            ref={refMotivo}
            id="dialogo-motivo"
            name="motivo"
            rows={4}
            value={motivo}
            maxLength={MOTIVO_MAX}
            onChange={(e) => {
              setMotivo(e.target.value)
              if (erroresLocales.length > 0) setErroresLocales(validarMotivo(accion, e.target.value))
            }}
            aria-required={obligatorio ? 'true' : undefined}
            aria-invalid={tieneErrores}
            aria-describedby={describedBy}
            disabled={enviando}
          />
          <p id={idAyuda} className="campo-ayuda">
            {obligatorio ? 'De 5 a 500 caracteres.' : 'Hasta 500 caracteres.'} Llevas{' '}
            {motivo.trim().length}.
          </p>
          {tieneErrores && (
            <ul id={idError} className="campo-errores">
              {errores.map((m) => (
                <li key={m}>{m}</li>
              ))}
            </ul>
          )}

          <Aviso aviso={error} />

          <div className="dialogo-botones">
            <button
              type="submit"
              className={accion === 'rechazar' || accion === 'bloquear' ? 'boton-peligro' : undefined}
              disabled={enviando}
            >
              {enviando ? textos.enviando : textos.boton}
            </button>
            <button
              type="button"
              className="secondary-button"
              onClick={onCancelar}
              disabled={enviando}
            >
              Cancelar
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
