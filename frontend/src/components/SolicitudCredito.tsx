// P6 — Solicitud de crédito (HU-039, HU-040): #/credito (desde la banda de la portada) y #/credito/<slug> (desde la
// ficha de un vehículo). Sin cuenta o con cualquier sesión; con sesión se prellenan los datos del perfil y se envía el
// token para ligar la solicitud a la cuenta (CA-P6-06). Solo nombre, teléfono, correo opcional y comentario opcional
// (D-55 B): nunca monto, cuota, ingresos ni tipo de vehículo (CA-P6-08, CA-P6-12). La atiende siempre la
// administración (D-58 A); aquí no se muestra nada de eso.
import { useCallback, useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { obtenerDetalle } from '../api/catalogo'
import {
  CORREO_MAX,
  MENSAJE_CONTACTO_FALLO,
  MENSAJE_REVISA_DATOS,
  MENSAJE_VEHICULO_NO_DISPONIBLE,
  NOMBRE_MAX,
  TELEFONO_MAX,
  WHATSAPP_PORTAL,
} from '../api/contactos'
import { ApiError } from '../api/http'
import {
  AYUDA_COMENTARIO_CREDITO,
  CAMPOS_CREDITO,
  COMENTARIO_MAX,
  datosCredito,
  enviarCredito,
  repartirErrores,
  TEXTO_INTERMEDIARIOS,
  validarCredito,
  type CampoCredito,
  type ErroresCredito,
  type FormularioCredito,
} from '../api/solicitudes'
import { usePeticion } from '../catalogo/usePeticion'
import { HASH_BUSCAR, hashVehiculo } from '../rutas'
import { AutorizacionDatos } from './AutorizacionDatos'
import { Aviso, type DatosAviso } from './Aviso'
import type { SesionAviso } from './AvisoPublico'
import { Campo, CampoAreaTexto } from './Campo'
import { NotaObligatorio } from './NotaObligatorio'
import { CampoTrampa, ConfirmacionSolicitud } from './SolicitudComun'

export const TITULO_CREDITO = 'Solicita tu crédito'
export const TITULO_CREDITO_VEHICULO = 'Solicita crédito para este vehículo'

/** CA-P6-14: WhatsApp del portal (D-42) con un texto inicial; no crea ninguna solicitud. */
export function enlaceWhatsappCredito(titulo: string | null): string {
  const texto = titulo
    ? `Hola, quiero información de crédito para el vehículo ${titulo} que vi en Financiamos Tu Camión.`
    : 'Hola, quiero información de crédito para un vehículo.'
  return `${WHATSAPP_PORTAL}?text=${encodeURIComponent(texto)}`
}

function formularioInicial(sesion: SesionAviso | null): FormularioCredito {
  const perfil = sesion?.perfil ?? null
  return {
    nombre: perfil?.nombreCompleto ?? '',
    telefono: perfil?.phoneNumber ?? '',
    correo: perfil?.email ?? '',
    mensaje: '',
    autorizacionDatos: false,
  }
}

type SolicitudCreditoProps = {
  /** null = crédito desde la portada; con valor = para ese vehículo. */
  slug: string | null
  sesion: SesionAviso | null
}

export function SolicitudCredito({ slug, sesion }: SolicitudCreditoProps) {
  if (slug === null) {
    return (
      <PaginaCredito titulo={TITULO_CREDITO}>
        <FormularioCreditoPublico slug={null} tituloVehiculo={null} sesion={sesion} />
      </PaginaCredito>
    )
  }
  return <CreditoVehiculo key={slug} slug={slug} sesion={sesion} />
}

function PaginaCredito({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <div className="cat-pagina aviso-publico">
      <h1 className="cat-titulo" tabIndex={-1}>
        {titulo}
      </h1>
      {children}
    </div>
  )
}

/** Crédito desde la ficha: primero se confirma que el vehículo sigue visible y no es del propio asesor. */
function CreditoVehiculo({ slug, sesion }: { slug: string; sesion: SesionAviso | null }) {
  // Como en la ficha: solo el asesor envía su token, para saber si la publicación es suya (CA-P6-05).
  const tokenAsesor = sesion?.perfil.roles.includes('ASESOR') ? sesion.token : undefined
  const cargar = useCallback(
    (signal: AbortSignal) => obtenerDetalle(slug, signal, tokenAsesor),
    [slug, tokenAsesor],
  )
  const [estado, reintentar] = usePeticion(cargar, `credito|${slug}|${tokenAsesor ? 'asesor' : 'publico'}`)

  return (
    <PaginaCredito titulo={TITULO_CREDITO_VEHICULO}>
      {estado.estado === 'cargando' && (
        <p className="cat-estado" role="status" aria-busy="true">
          Cargando vehículo...
        </p>
      )}
      {estado.estado === 'error' && estado.error.status === 404 && (
        <div className="cat-vacio">
          <Aviso aviso={{ tipo: 'error', texto: MENSAJE_VEHICULO_NO_DISPONIBLE }} />
          <p>
            <a className="cat-acceso" href={HASH_BUSCAR}>
              Ver el catálogo
            </a>
          </p>
        </div>
      )}
      {estado.estado === 'error' && estado.error.status !== 404 && (
        <div className="cat-estado">
          <Aviso aviso={{ tipo: 'error', texto: estado.error.mensaje }} />
          <button type="button" className="cat-boton" onClick={reintentar}>
            Reintentar
          </button>
        </div>
      )}
      {estado.estado === 'ok' && estado.datos.esPropio && (
        <p className="cat-aviso-propio" role="note">
          Este vehículo es tuyo; no puedes pedir crédito para tu propia publicación.{' '}
          <a href={hashVehiculo(estado.datos.slug)}>Volver a la ficha</a>
        </p>
      )}
      {estado.estado === 'ok' && !estado.datos.esPropio && (
        <FormularioCreditoPublico slug={estado.datos.slug} tituloVehiculo={estado.datos.titulo} sesion={sesion} />
      )}
    </PaginaCredito>
  )
}

type FormularioCreditoPublicoProps = {
  slug: string | null
  tituloVehiculo: string | null
  sesion: SesionAviso | null
}

function FormularioCreditoPublico({ slug, tituloVehiculo, sesion }: FormularioCreditoPublicoProps) {
  const [form, setForm] = useState<FormularioCredito>(() => formularioInicial(sesion))
  const [sitioWeb, setSitioWeb] = useState('')
  const [errores, setErrores] = useState<ErroresCredito>({})
  const [enviando, setEnviando] = useState(false)
  const [aviso, setAviso] = useState<DatosAviso | null>(null)
  const [noDisponible, setNoDisponible] = useState(false)
  const [enviado, setEnviado] = useState<string | null>(null)
  const enviandoRef = useRef(false)
  const refAviso = useRef<HTMLDivElement>(null)
  const refConfirmacion = useRef<HTMLDivElement>(null)
  const refNombre = useRef<HTMLInputElement>(null)
  const refTelefono = useRef<HTMLInputElement>(null)
  const refCorreo = useRef<HTMLInputElement>(null)
  const refMensaje = useRef<HTMLTextAreaElement>(null)
  const refAutorizacion = useRef<HTMLInputElement>(null)

  // Si la sesión se verifica después de abrir la pantalla, se prellenan los campos que sigan vacíos.
  const sesionRef = useRef(sesion)
  sesionRef.current = sesion
  const usuarioId = sesion?.perfil.usuarioId
  useEffect(() => {
    const p = sesionRef.current?.perfil
    if (!p) return
    setForm((f) => ({
      ...f,
      nombre: f.nombre === '' ? p.nombreCompleto : f.nombre,
      telefono: f.telefono === '' ? p.phoneNumber : f.telefono,
      correo: f.correo === '' ? p.email : f.correo,
    }))
  }, [usuarioId])

  function enfocarPrimerError(e: ErroresCredito) {
    const refs: Record<CampoCredito, { current: HTMLElement | null }> = {
      nombre: refNombre,
      telefono: refTelefono,
      correo: refCorreo,
      mensaje: refMensaje,
      autorizacionDatos: refAutorizacion,
    }
    const primero = CAMPOS_CREDITO.find((c) => (e[c]?.length ?? 0) > 0)
    if (primero) window.setTimeout(() => refs[primero].current?.focus(), 0)
  }

  function cambiar<K extends keyof FormularioCredito>(campo: K, valor: FormularioCredito[K]) {
    setForm((f) => ({ ...f, [campo]: valor }))
    if (errores[campo]) setErrores((e) => ({ ...e, [campo]: undefined }))
  }

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (enviandoRef.current) return
    setAviso(null)
    setNoDisponible(false)
    const locales = validarCredito(form)
    if (Object.keys(locales).length > 0) {
      setErrores(locales)
      setAviso({ tipo: 'error', texto: MENSAJE_REVISA_DATOS })
      enfocarPrimerError(locales)
      return
    }
    setErrores({})
    enviandoRef.current = true
    setEnviando(true)
    try {
      const mensaje = await enviarCredito(datosCredito(slug, form, sitioWeb), sesion?.token)
      setForm(formularioInicial(sesionRef.current))
      setSitioWeb('')
      setEnviado(mensaje)
      window.setTimeout(() => refConfirmacion.current?.focus(), 0)
    } catch (error) {
      if (error instanceof ApiError && error.status === 400 && Object.keys(error.errores).length > 0) {
        const { porCampo, otros } = repartirErrores(CAMPOS_CREDITO, error.errores)
        setErrores(porCampo)
        setAviso({ tipo: 'error', texto: [error.mensaje, ...otros].join(' ') })
        if (Object.keys(porCampo).length > 0) enfocarPrimerError(porCampo)
        else window.setTimeout(() => refAviso.current?.focus(), 0)
        return
      }
      if (error instanceof ApiError && error.status === 404) setNoDisponible(true)
      setAviso({ tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_CONTACTO_FALLO })
      window.setTimeout(() => refAviso.current?.focus(), 0)
    } finally {
      enviandoRef.current = false
      setEnviando(false)
    }
  }

  function enviarOtra() {
    setEnviado(null)
    window.setTimeout(() => refNombre.current?.focus(), 0)
  }

  if (enviado !== null) {
    return (
      <ConfirmacionSolicitud
        ref={refConfirmacion}
        mensaje={enviado}
        onEnviarOtra={enviarOtra}
        enlaceWhatsapp={enlaceWhatsappCredito(tituloVehiculo)}
      />
    )
  }

  return (
    <>
      <p className="aviso-publico-intro">{TEXTO_INTERMEDIARIOS}</p>
      <p className="aviso-publico-intro">Déjanos tus datos y te contactaremos por teléfono o WhatsApp. No necesitas cuenta.</p>
      {tituloVehiculo !== null && slug !== null && (
        <p className="credito-vehiculo">
          <span className="credito-vehiculo-etiqueta">Vehículo:</span>{' '}
          <a href={hashVehiculo(slug)}>{tituloVehiculo}</a>
        </p>
      )}
      <form
        className="contacto-form aviso-publico-form"
        onSubmit={enviar}
        noValidate
        aria-busy={enviando}
        aria-label="Formulario de solicitud de crédito"
      >
        <NotaObligatorio />
        <Campo
          ref={refNombre}
          id="credito-nombre"
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
          id="credito-telefono"
          etiqueta="Teléfono o WhatsApp"
          tipo="tel"
          valor={form.telefono}
          onCambio={(v) => cambiar('telefono', v)}
          errores={errores.telefono}
          autoComplete="tel"
          inputMode="tel"
          maxLength={TELEFONO_MAX}
          describidoPor="credito-telefono-ayuda"
          deshabilitado={enviando}
        />
        <p id="credito-telefono-ayuda" className="campo-ayuda">
          Celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567.
        </p>
        <Campo
          ref={refCorreo}
          id="credito-correo"
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
          id="credito-comentario"
          etiqueta="Comentario (opcional)"
          valor={form.mensaje}
          onCambio={(v) => cambiar('mensaje', v)}
          errores={errores.mensaje}
          maxLength={COMENTARIO_MAX}
          filas={5}
          ayuda={AYUDA_COMENTARIO_CREDITO}
          contador
          requerido={false}
          deshabilitado={enviando}
        />
        <AutorizacionDatos
          ref={refAutorizacion}
          id="credito-autorizacion"
          marcada={form.autorizacionDatos}
          onCambio={(v) => cambiar('autorizacionDatos', v)}
          errores={errores.autorizacionDatos}
          deshabilitado={enviando}
        />
        <CampoTrampa id="credito-sitio-web" valor={sitioWeb} onCambio={setSitioWeb} />

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
    </>
  )
}
