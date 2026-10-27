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
  return (
    <DialogoConfirmacion
      titulo={textos.titulo}
      descripcion={textos.pregunta(usuario.nombreCompleto || usuario.email)}
      textoBoton={textos.boton}
      textoEnviando={textos.enviando}
      peligro={accion === 'rechazar' || accion === 'bloquear'}
      motivo={motivoObligatorio(accion) ? 'obligatorio' : 'opcional'}
      validarMotivo={(m) => validarMotivo(accion, m)}
      enviando={enviando}
      erroresMotivo={erroresMotivo}
      error={error}
      onConfirmar={onConfirmar}
      onCancelar={onCancelar}
    />
  )
}

const SIN_ERRORES: string[] = []

type DialogoConfirmacionProps = {
  titulo: string
  descripcion: string
  textoBoton: string
  textoEnviando: string
  /** Botón de confirmar en rojo (acciones que quitan algo). */
  peligro?: boolean
  /** P4a: "no" muestra solo la pregunta (p. ej. eliminar una foto). */
  motivo: 'obligatorio' | 'opcional' | 'no'
  /** Validación del motivo en cliente; devuelve la lista de errores. */
  validarMotivo?: (motivo: string) => string[]
  /** Texto de ayuda bajo el motivo; por defecto el de 5 a 500 caracteres. */
  ayudaMotivo?: string
  enviando: boolean
  erroresMotivo?: string[]
  error: DatosAviso | null
  onConfirmar: (motivo: string) => void
  onCancelar: () => void
}

/**
 * Diálogo modal genérico (P2, reutilizado en P4a para inactivar vehículos y
 * eliminar fotos): foco atrapado dentro, Escape cancela y, al cerrarse, quien
 * lo abrió devuelve el foco al botón de origen.
 */
export function DialogoConfirmacion({
  titulo,
  descripcion,
  textoBoton,
  textoEnviando,
  peligro = false,
  motivo: modoMotivo,
  validarMotivo: validar = () => [],
  ayudaMotivo,
  enviando,
  erroresMotivo = SIN_ERRORES,
  error,
  onConfirmar,
  onCancelar,
}: DialogoConfirmacionProps) {
  const conMotivo = modoMotivo !== 'no'
  const obligatorio = modoMotivo === 'obligatorio'
  const [motivo, setMotivo] = useState('')
  const [erroresLocales, setErroresLocales] = useState<string[]>([])
  const refDialogo = useRef<HTMLDivElement>(null)
  const refMotivo = useRef<HTMLTextAreaElement>(null)
  const refConfirmar = useRef<HTMLButtonElement>(null)

  const errores = erroresLocales.length > 0 ? erroresLocales : erroresMotivo
  const tieneErrores = errores.length > 0
  const idTitulo = 'dialogo-accion-titulo'
  const idDescripcion = 'dialogo-accion-descripcion'
  const idError = 'dialogo-motivo-error'
  const idAyuda = 'dialogo-motivo-ayuda'

  useEffect(() => {
    if (refMotivo.current) refMotivo.current.focus()
    else refConfirmar.current?.focus()
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
    if (!conMotivo) {
      onConfirmar('')
      return
    }
    const validacion = validar(motivo)
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
          <h3 id={idTitulo}>{titulo}</h3>
          <p id={idDescripcion}>{descripcion}</p>

          {conMotivo && (
            <>
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
                  if (erroresLocales.length > 0) setErroresLocales(validar(e.target.value))
                }}
                aria-required={obligatorio ? 'true' : undefined}
                aria-invalid={tieneErrores}
                aria-describedby={describedBy}
                disabled={enviando}
              />
              <p id={idAyuda} className="campo-ayuda">
                {ayudaMotivo ?? (obligatorio ? 'De 5 a 500 caracteres.' : 'Hasta 500 caracteres.')}{' '}
                Llevas {motivo.trim().length}.
              </p>
              {tieneErrores && (
                <ul id={idError} className="campo-errores">
                  {errores.map((m) => (
                    <li key={m}>{m}</li>
                  ))}
                </ul>
              )}
            </>
          )}

          <Aviso aviso={error} />

          <div className="dialogo-botones">
            <button
              ref={refConfirmar}
              type="submit"
              className={peligro ? 'boton-peligro' : undefined}
              disabled={enviando}
            >
              {enviando ? textoEnviando : textoBoton}
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
