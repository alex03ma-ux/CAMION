// P8 — Pantalla pública #/restablecer-contrasena?codigo=... (HU-063, RN-US-006; D-73 D, D-12; CA-P8-67).
// Lee el código del fragmento una sola vez y enseguida lo quita de la URL con history.replaceState, para que no quede
// en el historial ni en la barra de direcciones. Pide la contraseña nueva y su confirmación con las reglas de P1 visibles.
// Al terminar, todas las sesiones de la cuenta quedan cerradas (lo hace la API) y se ofrece iniciar sesión.
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import {
  codigoDelHash,
  MENSAJE_CODIGO_NO_VALIDO,
  MENSAJE_CONFIRMAR_PASSWORD,
  MENSAJE_PASSWORD_NO_COINCIDE,
  restablecerContrasena,
} from '../api/recuperacion'
import { erroresPassword, PASSWORD_MAX } from '../auth/passwordRules'
import { HASH_CUENTA } from '../rutas'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo } from './Campo'
import { NotaObligatorio } from './NotaObligatorio'
import { ReglasPassword } from './ReglasPassword'

export const TITULO_RESTABLECER = 'Crear una contraseña nueva'
export const HASH_RESTABLECER = '#/restablecer-contrasena'

/** Quita el código de la URL sin añadir una entrada al historial. */
export function quitarCodigoDeLaUrl(): void {
  if (!/[?&]codigo=/.test(window.location.hash)) return
  const { pathname, search } = window.location
  window.history.replaceState(window.history.state, '', `${pathname}${search}${HASH_RESTABLECER}`)
}

type Estado =
  | { tipo: 'formulario' }
  | { tipo: 'codigoInvalido' }
  | { tipo: 'listo'; mensaje: string }

type Errores = { password?: string[]; confirmacion?: string[] }

export function RestablecerContrasena() {
  // El código se lee una sola vez, al montar, del hash actual.
  const [codigo] = useState(() => codigoDelHash(window.location.hash))
  const [estado, setEstado] = useState<Estado>(() => (codigo === '' ? { tipo: 'codigoInvalido' } : { tipo: 'formulario' }))
  const [password, setPassword] = useState('')
  const [confirmacion, setConfirmacion] = useState('')
  const [errores, setErrores] = useState<Errores>({})
  const [aviso, setAviso] = useState<DatosAviso | null>(null)
  const [enviando, setEnviando] = useState(false)
  const titulo = useRef<HTMLHeadingElement>(null)
  const refPassword = useRef<HTMLInputElement>(null)
  const refConfirmacion = useRef<HTMLInputElement>(null)
  const refResultado = useRef<HTMLDivElement>(null)

  useEffect(() => {
    quitarCodigoDeLaUrl()
  }, [])

  useEffect(() => {
    if (estado.tipo !== 'formulario') refResultado.current?.focus()
  }, [estado.tipo])

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (enviando) return
    setAviso(null)
    const nuevos: Errores = {}
    const reglas = erroresPassword(password)
    if (reglas.length > 0) nuevos.password = reglas
    if (confirmacion === '') nuevos.confirmacion = [MENSAJE_CONFIRMAR_PASSWORD]
    else if (confirmacion !== password) nuevos.confirmacion = [MENSAJE_PASSWORD_NO_COINCIDE]
    setErrores(nuevos)
    if (nuevos.password || nuevos.confirmacion) {
      ;(nuevos.password ? refPassword : refConfirmacion).current?.focus()
      return
    }
    setEnviando(true)
    try {
      const mensaje = await restablecerContrasena(codigo, password)
      setPassword('')
      setConfirmacion('')
      setEstado({ tipo: 'listo', mensaje })
    } catch (error) {
      setEnviando(false)
      if (error instanceof ApiError && error.status === 400) {
        const porCampo = Object.fromEntries(
          Object.entries(error.errores).map(([k, v]) => [k.toLowerCase(), v]),
        ) as Record<string, string[] | undefined>
        if (porCampo.codigo) {
          setPassword('')
          setConfirmacion('')
          setEstado({ tipo: 'codigoInvalido' })
          return
        }
        if (porCampo.password) {
          setErrores({ password: porCampo.password })
          setAviso({ tipo: 'error', texto: error.mensaje })
          refPassword.current?.focus()
          return
        }
      }
      setAviso({ tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO })
      return
    }
    setEnviando(false)
  }

  return (
    <div className="container cuenta">
      <p className="eyebrow">RED DE VENTA</p>
      <h1 ref={titulo} tabIndex={-1}>
        {TITULO_RESTABLECER}
      </h1>

      {estado.tipo === 'codigoInvalido' && (
        <div ref={refResultado} tabIndex={-1} className="restablecer-resultado">
          <Aviso aviso={{ tipo: 'error', texto: MENSAJE_CODIGO_NO_VALIDO }} />
          <p>
            <a href={HASH_CUENTA}>Ir a iniciar sesión</a>
          </p>
        </div>
      )}

      {estado.tipo === 'listo' && (
        <div ref={refResultado} tabIndex={-1} className="restablecer-resultado">
          <Aviso aviso={{ tipo: 'exito', texto: estado.mensaje }} />
          <p className="form-ayuda">Por seguridad, se cerraron las sesiones abiertas de tu cuenta en todos los equipos.</p>
          <a className="inv-boton-enlace inv-boton-bloque" href={HASH_CUENTA}>
            Iniciar sesión
          </a>
        </div>
      )}

      {estado.tipo === 'formulario' && (
        <form onSubmit={enviar} noValidate aria-busy={enviando}>
          <h2>Escribe tu contraseña nueva</h2>
          <p className="form-ayuda">El enlace sirve una sola vez y vence 60 minutos después de generarse.</p>
          <NotaObligatorio />
          <Aviso aviso={aviso} />
          <Campo
            ref={refPassword}
            id="restablecer-password"
            etiqueta="Contraseña nueva"
            tipo="password"
            valor={password}
            onCambio={setPassword}
            errores={errores.password}
            autoComplete="new-password"
            maxLength={PASSWORD_MAX}
            describidoPor="restablecer-reglas"
          />
          <ReglasPassword id="restablecer-reglas" password={password} />
          <Campo
            ref={refConfirmacion}
            id="restablecer-confirmacion"
            etiqueta="Confirma la contraseña nueva"
            tipo="password"
            valor={confirmacion}
            onCambio={setConfirmacion}
            errores={errores.confirmacion}
            autoComplete="new-password"
            maxLength={PASSWORD_MAX}
          />
          <button type="submit" disabled={enviando}>
            {enviando ? 'Guardando...' : 'Guardar contraseña nueva'}
          </button>
          <p className="auth-switch">
            <a href={HASH_CUENTA}>Volver a iniciar sesión</a>
          </p>
        </form>
      )}
    </div>
  )
}
