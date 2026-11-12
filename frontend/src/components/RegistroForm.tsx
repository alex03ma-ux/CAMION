import { useEffect, useRef, useState, type FormEvent, type RefObject } from 'react'
import {
  MENSAJE_CORREO_DUPLICADO,
  registrarAsesor,
  registrarComprador,
  TIPOS_DOCUMENTO,
  type TipoDocumento,
} from '../api/auth'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { PASSWORD_MAX } from '../auth/passwordRules'
import {
  CAMPOS_ASESOR,
  CAMPOS_REGISTRO,
  DOCUMENTO_MAX,
  hayErroresEn,
  NOMBRE_COMERCIAL_MAX,
  normalizarRegistro,
  normalizarRegistroAsesor,
  repartirErroresAsesor,
  repartirErroresServidor,
  validarRegistro,
  validarRegistroAsesor,
  type CampoAsesor,
  type DatosRegistroAsesor,
  type ErroresRegistroAsesor,
} from '../auth/validation'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo, CampoSelect } from './Campo'
import { ReglasPassword } from './ReglasPassword'
import { NotaObligatorio } from './NotaObligatorio'

type RegistroFormProps = {
  /** Se llama tras 201 con el mensaje del servidor y el correo usado. */
  onRegistrado: (mensaje: string, email: string) => void
  onIrALogin: () => void
}

/** Comprador: acceso inmediato. Asesor: queda PENDIENTE hasta que el administrador lo apruebe (D-3). */
type TipoCuenta = 'comprador' | 'asesor'

const VACIO: DatosRegistroAsesor = {
  nombreCompleto: '',
  email: '',
  telefono: '',
  password: '',
  tipoDocumento: '',
  numeroDocumento: '',
  nombreComercial: '',
}

export function RegistroForm({ onRegistrado, onIrALogin }: RegistroFormProps) {
  const [tipoCuenta, setTipoCuenta] = useState<TipoCuenta>('comprador')
  const [datos, setDatos] = useState<DatosRegistroAsesor>(VACIO)
  const [errores, setErrores] = useState<ErroresRegistroAsesor>({})
  const [aviso, setAviso] = useState<DatosAviso | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [intentado, setIntentado] = useState(false)

  const esAsesor = tipoCuenta === 'asesor'
  const campos: readonly CampoAsesor[] = esAsesor ? CAMPOS_ASESOR : CAMPOS_REGISTRO

  const titulo = useRef<HTMLHeadingElement>(null)
  const refNombre = useRef<HTMLInputElement>(null)
  const refEmail = useRef<HTMLInputElement>(null)
  const refTelefono = useRef<HTMLInputElement>(null)
  const refPassword = useRef<HTMLInputElement>(null)
  const refTipoDocumento = useRef<HTMLSelectElement>(null)
  const refNumeroDocumento = useRef<HTMLInputElement>(null)
  const refNombreComercial = useRef<HTMLInputElement>(null)
  const refs: Record<CampoAsesor, RefObject<HTMLElement | null>> = {
    nombreCompleto: refNombre,
    email: refEmail,
    telefono: refTelefono,
    password: refPassword,
    tipoDocumento: refTipoDocumento,
    numeroDocumento: refNumeroDocumento,
    nombreComercial: refNombreComercial,
  }

  useEffect(() => {
    titulo.current?.focus()
  }, [])

  function validar(valores: DatosRegistroAsesor): ErroresRegistroAsesor {
    return esAsesor ? validarRegistroAsesor(valores) : validarRegistro(valores)
  }

  function enfocarPrimerError(e: ErroresRegistroAsesor) {
    const campo = campos.find((c) => (e[c]?.length ?? 0) > 0)
    if (campo) refs[campo].current?.focus()
  }

  function cambiarTipoCuenta(tipo: TipoCuenta) {
    if (tipo === tipoCuenta) return
    setTipoCuenta(tipo)
    // Los errores dependen del tipo de cuenta: se limpian y se vuelve a validar al enviar.
    setErrores({})
    setAviso(null)
    setIntentado(false)
  }

  function cambiar(campo: CampoAsesor, valor: string) {
    const nuevos = { ...datos, [campo]: valor }
    setDatos(nuevos)
    // Tras el primer intento, los errores se recalculan mientras se escribe.
    if (intentado) {
      const validacion = validar(nuevos)
      setErrores((previos) => ({ ...previos, [campo]: validacion[campo] }))
    }
  }

  async function enviarAlServidor(): Promise<{ mensaje: string; email: string }> {
    if (esAsesor) {
      const n = normalizarRegistroAsesor(datos)
      const respuesta = await registrarAsesor({
        nombreCompleto: n.nombreCompleto,
        email: n.email,
        telefono: n.telefono,
        password: n.password,
        tipoDocumento: n.tipoDocumento as TipoDocumento,
        numeroDocumento: n.numeroDocumento,
        nombreComercial: n.nombreComercial === '' ? null : n.nombreComercial,
      })
      return { mensaje: respuesta.mensaje, email: n.email }
    }
    const n = normalizarRegistro(datos)
    const respuesta = await registrarComprador(n)
    return { mensaje: respuesta.mensaje, email: n.email }
  }

  async function enviar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (enviando) return
    setIntentado(true)
    setAviso(null)

    const validacion = validar(datos)
    setErrores(validacion)
    if (hayErroresEn(validacion, campos)) {
      enfocarPrimerError(validacion)
      return
    }

    setEnviando(true)
    try {
      const { mensaje, email } = await enviarAlServidor()
      onRegistrado(mensaje, email)
    } catch (error) {
      if (!(error instanceof ApiError)) {
        setAviso({ tipo: 'error', texto: MENSAJE_INESPERADO })
        return
      }
      const reparto: { porCampo: ErroresRegistroAsesor; otros: string[] } = esAsesor
        ? repartirErroresAsesor(error.errores)
        : repartirErroresServidor(error.errores)
      if (error.status === 409) {
        // 409 de documento (asesor) trae `errores.numeroDocumento`; el de correo, solo `error`.
        const e: ErroresRegistroAsesor = hayErroresEn(reparto.porCampo, campos)
          ? reparto.porCampo
          : { email: [error.mensaje || MENSAJE_CORREO_DUPLICADO] }
        setErrores(e)
        enfocarPrimerError(e)
        return
      }
      if (error.status === 400) {
        setErrores(reparto.porCampo)
        setAviso({ tipo: 'error', texto: [error.mensaje, ...reparto.otros].join(' ') })
        enfocarPrimerError(reparto.porCampo)
        return
      }
      setAviso({ tipo: 'error', texto: error.mensaje })
    } finally {
      setEnviando(false)
    }
  }

  const textoBoton = esAsesor
    ? enviando
      ? 'Enviando solicitud...'
      : 'Enviar solicitud'
    : enviando
      ? 'Creando cuenta...'
      : 'Crear cuenta'

  return (
    <form onSubmit={enviar} noValidate aria-busy={enviando}>
      <h2 ref={titulo} tabIndex={-1}>
        Crear cuenta
      </h2>
      <NotaObligatorio />

      <fieldset className="selector-cuenta" disabled={enviando}>
        <legend>Tipo de cuenta</legend>
        <label className="opcion-radio">
          <input
            type="radio"
            name="tipoCuenta"
            value="comprador"
            checked={!esAsesor}
            onChange={() => cambiarTipoCuenta('comprador')}
          />
          <span>Soy comprador</span>
        </label>
        <label className="opcion-radio">
          <input
            type="radio"
            name="tipoCuenta"
            value="asesor"
            checked={esAsesor}
            onChange={() => cambiarTipoCuenta('asesor')}
          />
          <span>Soy asesor</span>
        </label>
      </fieldset>

      <p className="form-ayuda form-ayuda-tipo" aria-live="polite">
        {esAsesor
          ? 'Regístrate como asesor. El administrador revisará tu solicitud y podrás iniciar sesión cuando la apruebe. Todos los campos son obligatorios, salvo el nombre comercial.'
          : 'Regístrate como comprador. El acceso es inmediato. Todos los campos son obligatorios.'}
      </p>

      <Campo
        ref={refNombre}
        id="nombreCompleto"
        etiqueta="Nombre completo"
        valor={datos.nombreCompleto}
        onCambio={(v) => cambiar('nombreCompleto', v)}
        errores={errores.nombreCompleto}
        autoComplete="name"
        maxLength={150}
      />
      <Campo
        ref={refEmail}
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
        ref={refTelefono}
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

      {esAsesor && (
        <>
          <CampoSelect
            ref={refTipoDocumento}
            id="tipoDocumento"
            etiqueta="Tipo de documento"
            valor={datos.tipoDocumento}
            onCambio={(v) => cambiar('tipoDocumento', v)}
            opciones={TIPOS_DOCUMENTO}
            textoVacio="Elige un tipo de documento"
            errores={errores.tipoDocumento}
          />
          <Campo
            ref={refNumeroDocumento}
            id="numeroDocumento"
            etiqueta="Número de documento"
            valor={datos.numeroDocumento}
            onCambio={(v) => cambiar('numeroDocumento', v)}
            errores={errores.numeroDocumento}
            autoComplete="off"
            maxLength={DOCUMENTO_MAX}
            describidoPor="numeroDocumento-ayuda"
          />
          <p id="numeroDocumento-ayuda" className="campo-ayuda">
            De 4 a 30 caracteres: letras, números o guiones.
          </p>
          <Campo
            ref={refNombreComercial}
            id="nombreComercial"
            etiqueta="Nombre comercial (opcional)"
            valor={datos.nombreComercial}
            onCambio={(v) => cambiar('nombreComercial', v)}
            errores={errores.nombreComercial}
            autoComplete="organization"
            maxLength={NOMBRE_COMERCIAL_MAX}
            requerido={false}
          />
        </>
      )}

      <Campo
        ref={refPassword}
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
        {textoBoton}
      </button>

      <Aviso aviso={aviso} />

      <p className="auth-switch">¿Ya tienes una cuenta?</p>
      <button className="secondary-button" type="button" onClick={onIrALogin} disabled={enviando}>
        Volver a iniciar sesión
      </button>
    </form>
  )
}
