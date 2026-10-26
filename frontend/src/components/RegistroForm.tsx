import { useEffect, useRef, useState, type FormEvent, type RefObject } from 'react'
import { MENSAJE_CORREO_DUPLICADO, registrarComprador } from '../api/auth'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { PASSWORD_MAX } from '../auth/passwordRules'
import {
  CAMPOS_REGISTRO,
  hayErrores,
  normalizarRegistro,
  repartirErroresServidor,
  validarRegistro,
  type CampoRegistro,
  type DatosRegistro,
  type ErroresRegistro,
} from '../auth/validation'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo } from './Campo'
import { ReglasPassword } from './ReglasPassword'

type RegistroFormProps = {
  /** Se llama tras 201 con el mensaje del servidor y el correo usado. */
  onRegistrado: (mensaje: string, email: string) => void
  onIrALogin: () => void
}

const VACIO: DatosRegistro = { nombreCompleto: '', email: '', telefono: '', password: '' }

export function RegistroForm({ onRegistrado, onIrALogin }: RegistroFormProps) {
  const [datos, setDatos] = useState<DatosRegistro>(VACIO)
  const [errores, setErrores] = useState<ErroresRegistro>({})
  const [aviso, setAviso] = useState<DatosAviso | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [intentado, setIntentado] = useState(false)

  const titulo = useRef<HTMLHeadingElement>(null)
  const refs: Record<CampoRegistro, RefObject<HTMLInputElement | null>> = {
    nombreCompleto: useRef<HTMLInputElement>(null),
    email: useRef<HTMLInputElement>(null),
    telefono: useRef<HTMLInputElement>(null),
    password: useRef<HTMLInputElement>(null),
  }

  useEffect(() => {
    titulo.current?.focus()
  }, [])

  function enfocarPrimerError(e: ErroresRegistro) {
    const campo = CAMPOS_REGISTRO.find((c) => (e[c]?.length ?? 0) > 0)
    if (campo) refs[campo].current?.focus()
  }

  function cambiar(campo: CampoRegistro, valor: string) {
    const nuevos = { ...datos, [campo]: valor }
    setDatos(nuevos)
    // Tras el primer intento, los errores se recalculan mientras se escribe.
    if (intentado) {
      const validacion = validarRegistro(nuevos)
      setErrores((previos) => ({ ...previos, [campo]: validacion[campo] }))
    }
  }

  async function enviar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (enviando) return
    setIntentado(true)
    setAviso(null)

    const validacion = validarRegistro(datos)
    setErrores(validacion)
    if (hayErrores(validacion)) {
      enfocarPrimerError(validacion)
      return
    }

    const normalizados = normalizarRegistro(datos)
    setEnviando(true)
    try {
      const respuesta = await registrarComprador(normalizados)
      onRegistrado(respuesta.mensaje, normalizados.email)
    } catch (error) {
      if (!(error instanceof ApiError)) {
        setAviso({ tipo: 'error', texto: MENSAJE_INESPERADO })
        return
      }
      if (error.status === 409) {
        const e = { email: [error.mensaje || MENSAJE_CORREO_DUPLICADO] }
        setErrores(e)
        refs.email.current?.focus()
        return
      }
      if (error.status === 400) {
        const { porCampo, otros } = repartirErroresServidor(error.errores)
        setErrores(porCampo)
        setAviso({ tipo: 'error', texto: [error.mensaje, ...otros].join(' ') })
        enfocarPrimerError(porCampo)
        return
      }
      setAviso({ tipo: 'error', texto: error.mensaje })
    } finally {
      setEnviando(false)
    }
  }

  return (
    <form onSubmit={enviar} noValidate aria-busy={enviando}>
      <h2 ref={titulo} tabIndex={-1}>
        Crear cuenta
      </h2>
      <p className="form-ayuda">
        Regístrate como comprador. El acceso es inmediato. Todos los campos son obligatorios.
      </p>

      <Campo
        ref={refs.nombreCompleto}
        id="nombreCompleto"
        etiqueta="Nombre completo"
        valor={datos.nombreCompleto}
        onCambio={(v) => cambiar('nombreCompleto', v)}
        errores={errores.nombreCompleto}
        autoComplete="name"
        maxLength={150}
      />
      <Campo
        ref={refs.email}
        id="email"
        etiqueta="Correo electrónico"
        tipo="email"
        valor={datos.email}
        onCambio={(v) => cambiar('email', v)}
        errores={errores.email}
        autoComplete="email"
        inputMode="email"
        maxLength={254}
      />
      <Campo
        ref={refs.telefono}
        id="telefono"
        etiqueta="Teléfono"
        tipo="tel"
        valor={datos.telefono}
        onCambio={(v) => cambiar('telefono', v)}
        errores={errores.telefono}
        autoComplete="tel"
        inputMode="tel"
        maxLength={25}
      />
      <Campo
        ref={refs.password}
        id="password"
        etiqueta="Contraseña"
        tipo="password"
        valor={datos.password}
        onCambio={(v) => cambiar('password', v)}
        errores={errores.password}
        autoComplete="new-password"
        maxLength={PASSWORD_MAX}
        describidoPor="password-reglas"
      />
      <ReglasPassword id="password-reglas" password={datos.password} />

      <button type="submit" disabled={enviando}>
        {enviando ? 'Creando cuenta...' : 'Crear cuenta'}
      </button>

      <Aviso aviso={aviso} />

      <p className="auth-switch">¿Ya tienes una cuenta?</p>
      <button className="secondary-button" type="button" onClick={onIrALogin} disabled={enviando}>
        Volver a iniciar sesión
      </button>
    </form>
  )
}
