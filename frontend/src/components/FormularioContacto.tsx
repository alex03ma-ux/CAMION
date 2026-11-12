// P5 — "Contactar" en la ficha del vehículo (HU-029, HU-030, D-3, D-9, D-31, D-44, D-47).
// Sin cuenta se puede contactar. Con sesión se prellenan los datos del perfil y se envía el token para ligar
// el contacto a la cuenta (CA-P5-02), pero la autorización se marca en cada envío (CA-P5-18).
// El botón de WhatsApp abre la conversación fuera del portal: no crea contacto ni pide autorización (CA-P5-64).
import { useEffect, useRef, useState, type FormEvent } from 'react'
import type { PerfilUsuario } from '../api/auth'
import {
  CAMPOS_CONTACTO,
  CORREO_MAX,
  datosDeContacto,
  enlaceWhatsapp,
  enviarContacto,
  erroresDeServidor,
  MENSAJE_CONTACTO_FALLO,
  MENSAJE_MAX,
  MENSAJE_REVISA_DATOS,
  NOMBRE_MAX,
  TELEFONO_MAX,
  validarContacto,
  type CampoContacto,
  type ErroresContacto,
  type FormularioContacto as Formulario,
} from '../api/contactos'
import { ApiError } from '../api/http'
import { HASH_BUSCAR } from '../rutas'
import { AutorizacionDatos } from './AutorizacionDatos'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo, CampoAreaTexto } from './Campo'
import { NotaObligatorio } from './NotaObligatorio'

/** Sesión del visitante, si la tiene (comprador, asesor o administrador). */
export type SesionContacto = { token: string; perfil: PerfilUsuario }

type FormularioContactoProps = {
  slug: string
  titulo: string
  whatsapp: string | null
  sesion: SesionContacto | null
}

function formularioInicial(perfil: PerfilUsuario | null): Formulario {
  return {
    nombre: perfil?.nombreCompleto ?? '',
    telefono: perfil?.phoneNumber ?? '',
    correo: perfil?.email ?? '',
    mensaje: '',
    autorizacionDatos: false,
  }
}

export function FormularioContacto({ slug, titulo, whatsapp, sesion }: FormularioContactoProps) {
  const perfil = sesion?.perfil ?? null
  const [form, setForm] = useState<Formulario>(() => formularioInicial(perfil))
  const [sitioWeb, setSitioWeb] = useState('')
  const [errores, setErrores] = useState<ErroresContacto>({})
  const [enviando, setEnviando] = useState(false)
  const [aviso, setAviso] = useState<DatosAviso | null>(null)
  const [noDisponible, setNoDisponible] = useState(false)
  const refAviso = useRef<HTMLDivElement>(null)
  const refNombre = useRef<HTMLInputElement>(null)
  const refTelefono = useRef<HTMLInputElement>(null)
  const refCorreo = useRef<HTMLInputElement>(null)
  const refMensaje = useRef<HTMLTextAreaElement>(null)
  const refAutorizacion = useRef<HTMLInputElement>(null)

  // Si la sesión se verifica después de abrir la ficha, se prellenan los campos que sigan vacíos.
  const usuarioId = perfil?.usuarioId
  const perfilRef = useRef(perfil)
  perfilRef.current = perfil
  useEffect(() => {
    const p = perfilRef.current
    if (!p) return
    setForm((f) => ({
      ...f,
      nombre: f.nombre === '' ? p.nombreCompleto : f.nombre,
      telefono: f.telefono === '' ? p.phoneNumber : f.telefono,
      correo: f.correo === '' ? p.email : f.correo,
    }))
  }, [usuarioId])

  function enfocarPrimerError(e: ErroresContacto) {
    const refs: Record<CampoContacto, { current: HTMLInputElement | HTMLTextAreaElement | null }> = {
      nombre: refNombre,
      telefono: refTelefono,
      correo: refCorreo,
      mensaje: refMensaje,
      autorizacionDatos: refAutorizacion,
    }
    const primero = CAMPOS_CONTACTO.find((c) => (e[c]?.length ?? 0) > 0)
    if (primero) window.setTimeout(() => refs[primero].current?.focus(), 0)
  }

  function cambiar<K extends keyof Formulario>(campo: K, valor: Formulario[K]) {
    setForm((f) => ({ ...f, [campo]: valor }))
    if (errores[campo]) setErrores((e) => ({ ...e, [campo]: undefined }))
  }

  function mostrarAviso(datos: DatosAviso) {
    setAviso(datos)
    window.setTimeout(() => refAviso.current?.focus(), 0)
  }

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (enviando) return
    setAviso(null)
    setNoDisponible(false)
    const locales = validarContacto(form)
    if (Object.keys(locales).length > 0) {
      setErrores(locales)
      setAviso({ tipo: 'error', texto: MENSAJE_REVISA_DATOS })
      enfocarPrimerError(locales)
      return
    }
    setErrores({})
    setEnviando(true)
    try {
      const mensaje = await enviarContacto(datosDeContacto(slug, form, sitioWeb), sesion?.token)
      // CA-P5-54: el formulario se vacía (con sesión vuelve a los datos del perfil) y la casilla se desmarca.
      setForm(formularioInicial(perfilRef.current))
      setSitioWeb('')
      mostrarAviso({ tipo: 'exito', texto: mensaje })
    } catch (error) {
      if (error instanceof ApiError && error.status === 400 && Object.keys(error.errores).length > 0) {
        const { porCampo, otros } = erroresDeServidor(error.errores)
        setErrores(porCampo)
        setAviso({ tipo: 'error', texto: [error.mensaje, ...otros].join(' ') })
        if (Object.keys(porCampo).length > 0) enfocarPrimerError(porCampo)
        else window.setTimeout(() => refAviso.current?.focus(), 0)
        return
      }
      if (error instanceof ApiError && error.status === 404) setNoDisponible(true)
      mostrarAviso({ tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_CONTACTO_FALLO })
    } finally {
      setEnviando(false)
    }
  }

  return (
    <section className="contacto" aria-labelledby="contacto-titulo">
      <h2 id="contacto-titulo">Contactar</h2>
      <p className="contacto-intro">
        Déjanos tus datos y te contactaremos por teléfono o WhatsApp. No necesitas cuenta.
      </p>
      <a
        className="boton-whatsapp"
        href={enlaceWhatsapp(whatsapp, titulo)}
        target="_blank"
        rel="noopener noreferrer"
      >
        Escribir por WhatsApp
        <span className="sr-only"> (se abre en una pestaña nueva)</span>
      </a>

      <form className="contacto-form" onSubmit={enviar} noValidate aria-busy={enviando} aria-label="Formulario de contacto">
        <NotaObligatorio />
        <Campo
          ref={refNombre}
          id="contacto-nombre"
          etiqueta="Nombre"
          valor={form.nombre}
          onCambio={(v) => cambiar('nombre', v)}
          errores={errores.nombre}
          autoComplete="name"
          maxLength={NOMBRE_MAX}
          deshabilitado={enviando}
        />
        <Campo
          ref={refTelefono}
          id="contacto-telefono"
          etiqueta="Teléfono o WhatsApp"
          tipo="tel"
          valor={form.telefono}
          onCambio={(v) => cambiar('telefono', v)}
          errores={errores.telefono}
          autoComplete="tel"
          inputMode="tel"
          maxLength={TELEFONO_MAX}
          describidoPor="contacto-telefono-ayuda"
          deshabilitado={enviando}
        />
        <p id="contacto-telefono-ayuda" className="campo-ayuda">
          Celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567.
        </p>
        <Campo
          ref={refCorreo}
          id="contacto-correo"
          etiqueta="Correo (opcional)"
          tipo="email"
          valor={form.correo}
          onCambio={(v) => cambiar('correo', v)}
          errores={errores.correo}
          autoComplete="email"
          inputMode="email"
          maxLength={CORREO_MAX}
          requerido={false}
          deshabilitado={enviando}
        />
        <CampoAreaTexto
          ref={refMensaje}
          id="contacto-mensaje"
          etiqueta="Mensaje (opcional)"
          valor={form.mensaje}
          onCambio={(v) => cambiar('mensaje', v)}
          errores={errores.mensaje}
          maxLength={MENSAJE_MAX}
          ayuda={`Hasta ${MENSAJE_MAX} caracteres.`}
          requerido={false}
          deshabilitado={enviando}
        />
        <AutorizacionDatos
          ref={refAutorizacion}
          id="contacto-autorizacion"
          marcada={form.autorizacionDatos}
          onCambio={(v) => cambiar('autorizacionDatos', v)}
          errores={errores.autorizacionDatos}
          deshabilitado={enviando}
        />

        {/* Campo trampa (CA-P5-22): fuera de la vista, del orden de tabulación y de los lectores de pantalla. */}
        <div className="campo-trampa" aria-hidden="true">
          <label htmlFor="contacto-sitio-web">Sitio web (deja este campo vacío)</label>
          <input
            id="contacto-sitio-web"
            name="sitioWeb"
            type="text"
            tabIndex={-1}
            autoComplete="off"
            value={sitioWeb}
            onChange={(e) => setSitioWeb(e.target.value)}
          />
        </div>

        <div ref={refAviso} tabIndex={-1} className="contacto-aviso">
          <Aviso aviso={aviso} />
          {noDisponible && (
            <p>
              <a href={HASH_BUSCAR}>Ver el catálogo</a>
            </p>
          )}
        </div>

        <button type="submit" disabled={enviando}>
          {enviando ? 'Enviando...' : 'Enviar'}
        </button>
      </form>
    </section>
  )
}
