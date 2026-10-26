import { useEffect, useRef, useState, type FormEvent } from 'react'
import { iniciarSesion, MENSAJE_LOGIN_FALTAN_DATOS, type LoginResponse } from '../api/auth'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo } from './Campo'

type LoginFormProps = {
  /** Aviso que trae la pantalla anterior (registro exitoso, sesión terminada...). */
  avisoInicial?: DatosAviso | null
  emailInicial?: string
  /** Recibe la respuesta del login; si lanza ApiError, se muestra su mensaje. */
  onSesionIniciada: (respuesta: LoginResponse) => Promise<void>
  onIrARegistro: () => void
}

export function LoginForm({
  avisoInicial = null,
  emailInicial = '',
  onSesionIniciada,
  onIrARegistro,
}: LoginFormProps) {
  const [email, setEmail] = useState(emailInicial)
  const [password, setPassword] = useState('')
  const [aviso, setAviso] = useState<DatosAviso | null>(avisoInicial)
  const [erroresCampo, setErroresCampo] = useState<{ email?: string[]; password?: string[] }>({})
  const [enviando, setEnviando] = useState(false)

  const titulo = useRef<HTMLHeadingElement>(null)
  const refEmail = useRef<HTMLInputElement>(null)
  const refPassword = useRef<HTMLInputElement>(null)

  useEffect(() => {
    // Si viene del registro con el correo ya escrito, se va directo a la contraseña.
    if (emailInicial) refPassword.current?.focus()
    else titulo.current?.focus()
  }, [emailInicial])

  async function enviar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (enviando) return
    setAviso(null)

    const emailLimpio = email.trim()
    const faltaEmail = emailLimpio === ''
    const faltaPassword = password === ''
    if (faltaEmail || faltaPassword) {
      setErroresCampo({
        email: faltaEmail ? ['Escribe tu correo.'] : undefined,
        password: faltaPassword ? ['Escribe tu contraseña.'] : undefined,
      })
      setAviso({ tipo: 'error', texto: MENSAJE_LOGIN_FALTAN_DATOS })
      ;(faltaEmail ? refEmail : refPassword).current?.focus()
      return
    }
    setErroresCampo({})

    setEnviando(true)
    try {
      const respuesta = await iniciarSesion({ email: emailLimpio, password })
      setPassword('')
      await onSesionIniciada(respuesta)
    } catch (error) {
      setPassword('')
      setAviso({
        tipo: 'error',
        texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO,
      })
      refPassword.current?.focus()
    } finally {
      setEnviando(false)
    }
  }

  return (
    <form onSubmit={enviar} noValidate aria-busy={enviando}>
      <h2 ref={titulo} tabIndex={-1}>
        Iniciar sesión
      </h2>

      <Aviso aviso={aviso} />

      <Campo
        ref={refEmail}
        id="login-email"
        etiqueta="Correo electrónico"
        tipo="email"
        valor={email}
        onCambio={setEmail}
        errores={erroresCampo.email}
        autoComplete="username"
        inputMode="email"
        maxLength={254}
      />
      <Campo
        ref={refPassword}
        id="login-password"
        etiqueta="Contraseña"
        tipo="password"
        valor={password}
        onCambio={setPassword}
        errores={erroresCampo.password}
        autoComplete="current-password"
      />

      <button type="submit" disabled={enviando}>
        {enviando ? 'Iniciando sesión...' : 'Iniciar sesión'}
      </button>

      <p className="auth-switch">¿Todavía no tienes cuenta?</p>
      <button className="secondary-button" type="button" onClick={onIrARegistro} disabled={enviando}>
        Registrarme
      </button>
    </form>
  )
}
