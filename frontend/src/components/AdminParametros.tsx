// P7 — Parámetros: datos de contacto del portal (HU-081, RN-PA-001; D-42, D-47, D-63 A; CA-P7-35 a 44).
// Solo WhatsApp, correo, horario (1 a 4 líneas), Instagram y TikTok. No ofrece días del plan, fotos, límites
// anti-abuso, categorías ni DIVIPOLA (CA-P7-44). La API (SoloAdministrador) valida y audita cada cambio.
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { formatearFechaHora } from '../api/leads'
import {
  CORREO_MAX,
  erroresContactoDe,
  formularioDesdeContacto,
  guardarContactoAdmin,
  HORARIO_LINEA_MAX,
  HORARIO_LINEAS_MAX,
  obtenerContactoAdmin,
  RED_SOCIAL_MAX,
  validarContactoPortal,
  type CampoContactoPortal,
  type ContactoPortalAdmin,
  type ErroresContactoPortal,
  type FormularioContactoPortal,
} from '../api/parametros'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo } from './Campo'
import { NotaObligatorio } from './NotaObligatorio'

type Props = { token: string; onSesionTerminada: () => void }

type Carga = { estado: 'cargando' } | { estado: 'ok' } | { estado: 'error'; aviso: DatosAviso }

const IDS: Record<CampoContactoPortal, string> = {
  whatsapp: 'param-whatsapp',
  correo: 'param-correo',
  horario: 'param-horario-0',
  instagram: 'param-instagram',
  tiktok: 'param-tiktok',
}

export function AdminParametros({ token, onSesionTerminada }: Props) {
  const [carga, setCarga] = useState<Carga>({ estado: 'cargando' })
  const [intento, setIntento] = useState(0)
  const [datos, setDatos] = useState<ContactoPortalAdmin | null>(null)
  const [form, setForm] = useState<FormularioContactoPortal | null>(null)
  const [errores, setErrores] = useState<ErroresContactoPortal>({})
  const [aviso, setAviso] = useState<DatosAviso | null>(null)
  const [guardando, setGuardando] = useState(false)
  const titulo = useRef<HTMLHeadingElement>(null)

  useEffect(() => {
    titulo.current?.focus()
  }, [])

  const es401 = useCallback(
    (error: unknown) => {
      if (error instanceof ApiError && error.status === 401) {
        onSesionTerminada()
        return true
      }
      return false
    },
    [onSesionTerminada],
  )

  useEffect(() => {
    const control = new AbortController()
    setCarga({ estado: 'cargando' })
    obtenerContactoAdmin(token, control.signal)
      .then((d) => {
        setDatos(d)
        setForm(formularioDesdeContacto(d))
        setCarga({ estado: 'ok' })
      })
      .catch((error: unknown) => {
        if (control.signal.aborted || es401(error)) return
        setCarga({ estado: 'error', aviso: { tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO } })
      })
    return () => control.abort()
  }, [token, intento, es401])

  function cambiar(campo: Exclude<CampoContactoPortal, 'horario'>, valor: string) {
    setForm((f) => f && { ...f, [campo]: valor })
    setErrores((e) => ({ ...e, [campo]: undefined }))
  }

  function cambiarLinea(i: number, valor: string) {
    setForm((f) => f && { ...f, horario: f.horario.map((l, j) => (j === i ? valor : l)) })
    setErrores((e) => ({ ...e, horario: undefined }))
  }

  function agregarLinea() {
    if (!form || form.horario.length >= HORARIO_LINEAS_MAX) return
    const nueva = form.horario.length
    setForm({ ...form, horario: [...form.horario, ''] })
    window.setTimeout(() => document.getElementById(`param-horario-${nueva}`)?.focus(), 0)
  }

  function quitarLinea(i: number) {
    if (!form || form.horario.length <= 1) return
    setForm({ ...form, horario: form.horario.filter((_, j) => j !== i) })
    setErrores((e) => ({ ...e, horario: undefined }))
    window.setTimeout(() => document.getElementById(`param-horario-${Math.max(0, i - 1)}`)?.focus(), 0)
  }

  function enfocarPrimerError(e: ErroresContactoPortal) {
    const campo = (['whatsapp', 'correo', 'horario', 'instagram', 'tiktok'] as const).find((c) => e[c]?.length)
    if (campo) document.getElementById(IDS[campo])?.focus()
  }

  async function guardar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (!form || guardando) return
    setAviso(null)
    const validacion = validarContactoPortal(form)
    setErrores(validacion)
    if (Object.keys(validacion).length > 0) {
      setAviso({ tipo: 'error', texto: 'Revisa los datos marcados.' })
      enfocarPrimerError(validacion)
      return
    }
    setGuardando(true)
    try {
      const guardado = await guardarContactoAdmin(token, form)
      setGuardando(false)
      setDatos(guardado)
      setForm(formularioDesdeContacto(guardado))
      setAviso({ tipo: 'exito', texto: 'Se guardaron los datos de contacto. El portal los mostrará en la siguiente carga.' })
    } catch (error) {
      setGuardando(false)
      if (es401(error)) return
      if (error instanceof ApiError && error.status === 400) {
        const { porCampo, otros } = erroresContactoDe(error)
        setErrores(porCampo)
        setAviso({ tipo: 'error', texto: [error.mensaje, ...otros].join(' ') })
        enfocarPrimerError(porCampo)
        return
      }
      setAviso({ tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO })
    }
  }

  const erroresHorario = errores.horario ?? []

  return (
    <section className="panel-ancho" aria-labelledby="parametros-titulo">
      <h2 id="parametros-titulo" ref={titulo} tabIndex={-1}>
        Parámetros
      </h2>
      <p className="form-ayuda">
        Datos de contacto que se muestran en el pie del portal. El WhatsApp también se usa en el botón de la ficha de
        los vehículos a cargo de la administración. Los demás valores del portal (días del plan, fotos, límites) no se
        cambian aquí.
      </p>
      {carga.estado === 'cargando' && <p role="status">Cargando parámetros...</p>}
      {carga.estado === 'error' && (
        <>
          <Aviso aviso={carga.aviso} />
          <button type="button" className="secondary-button inv-boton-auto" onClick={() => setIntento((i) => i + 1)}>
            Reintentar
          </button>
        </>
      )}
      {carga.estado === 'ok' && form && (
        <form className="admin-form" onSubmit={(e) => void guardar(e)} noValidate aria-busy={guardando}>
          <h3 className="admin-subtitulo">Contacto del portal</h3>
          <p className="dato-secundario">
            {datos?.configurado
              ? `Última modificación: ${datos.actualizadoPor ?? 'cuenta eliminada'}, ${formatearFechaHora(datos.actualizadoEn)}.`
              : 'Se muestran los valores por defecto: aún no se ha guardado ningún cambio.'}
          </p>
          <NotaObligatorio />
          <div className="admin-rejilla">
            <Campo
              id="param-whatsapp"
              etiqueta="WhatsApp del portal"
              valor={form.whatsapp}
              onCambio={(v) => cambiar('whatsapp', v)}
              inputMode="tel"
              autoComplete="off"
              maxLength={20}
              errores={errores.whatsapp}
              describidoPor="param-whatsapp-ayuda"
              deshabilitado={guardando}
            />
            <Campo
              id="param-correo"
              etiqueta="Correo de contacto"
              tipo="email"
              valor={form.correo}
              onCambio={(v) => cambiar('correo', v)}
              inputMode="email"
              autoComplete="off"
              maxLength={CORREO_MAX}
              errores={errores.correo}
              deshabilitado={guardando}
            />
          </div>
          <p id="param-whatsapp-ayuda" className="campo-ayuda">
            Celular colombiano de 10 dígitos, por ejemplo 316 615 2907.
          </p>

          <fieldset className="pago-fieldset" aria-describedby={erroresHorario.length ? 'param-horario-error' : 'param-horario-ayuda'}>
            <legend className="campo-obligatorio">Horario de atención</legend>
            <p id="param-horario-ayuda" className="campo-ayuda">
              De 1 a {HORARIO_LINEAS_MAX} líneas, de hasta {HORARIO_LINEA_MAX} caracteres cada una.
            </p>
            {form.horario.map((linea, i) => (
              <div className="admin-linea" key={i}>
                <div className="campo">
                  <label htmlFor={`param-horario-${i}`}>Línea {i + 1}</label>
                  <input
                    id={`param-horario-${i}`}
                    value={linea}
                    maxLength={HORARIO_LINEA_MAX}
                    onChange={(e) => cambiarLinea(i, e.target.value)}
                    aria-required="true"
                    aria-invalid={erroresHorario.length > 0}
                    aria-describedby={erroresHorario.length ? 'param-horario-error' : 'param-horario-ayuda'}
                    disabled={guardando}
                  />
                </div>
                {form.horario.length > 1 && (
                  <button type="button" className="secondary-button" onClick={() => quitarLinea(i)} disabled={guardando}>
                    Quitar <span className="sr-only">la línea {i + 1}</span>
                  </button>
                )}
              </div>
            ))}
            {erroresHorario.length > 0 && (
              <ul id="param-horario-error" className="campo-errores">
                {erroresHorario.map((m) => (
                  <li key={m}>{m}</li>
                ))}
              </ul>
            )}
            {form.horario.length < HORARIO_LINEAS_MAX && (
              <button type="button" className="secondary-button inv-boton-auto" onClick={agregarLinea} disabled={guardando}>
                Añadir línea
              </button>
            )}
          </fieldset>

          <div className="admin-rejilla">
            <Campo
              id="param-instagram"
              etiqueta="Instagram (opcional)"
              tipo="url"
              valor={form.instagram}
              onCambio={(v) => cambiar('instagram', v)}
              maxLength={RED_SOCIAL_MAX}
              errores={errores.instagram}
              requerido={false}
              describidoPor="param-redes-ayuda"
              deshabilitado={guardando}
            />
            <Campo
              id="param-tiktok"
              etiqueta="TikTok (opcional)"
              tipo="url"
              valor={form.tiktok}
              onCambio={(v) => cambiar('tiktok', v)}
              maxLength={RED_SOCIAL_MAX}
              errores={errores.tiktok}
              requerido={false}
              describidoPor="param-redes-ayuda"
              deshabilitado={guardando}
            />
          </div>
          <p id="param-redes-ayuda" className="campo-ayuda">
            Enlace completo, por ejemplo https://www.instagram.com/cuenta o https://www.tiktok.com/@cuenta. Si lo dejas
            vacío, la red no se muestra en el portal.
          </p>

          <Aviso aviso={aviso} />
          <div className="admin-botones">
            <button type="submit" disabled={guardando}>
              {guardando ? 'Guardando...' : 'Guardar cambios'}
            </button>
          </div>
        </form>
      )}
    </section>
  )
}
