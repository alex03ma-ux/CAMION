// P5b — Pantallas públicas de los avisos (D-6, D-51 A): #/vende-tu-camion (HU-N01) y #/seguro-todo-riesgo
// (HU-N02). Funcionan sin cuenta y con cualquier sesión: con sesión se prellenan los datos del perfil y se envía
// el token para ligar el aviso a la cuenta (CA-P5b-02, 12), pero la autorización se marca en cada envío
// (CA-P5b-21). La solicitud la atiende siempre la administración (D-52); aquí no se muestra nada de eso.
import { useEffect, useRef, useState, type FormEvent } from 'react'
import type { PerfilUsuario } from '../api/auth'
import {
  CAMPOS_AVISO,
  datosSeguroTodoRiesgo,
  datosVendeTuCamion,
  DESCRIPCION_MAX,
  enviarSeguroTodoRiesgo,
  enviarVendeTuCamion,
  erroresDeServidorAviso,
  TIPOS_VEHICULO_AVISO,
  validarAviso,
  type CampoAviso,
  type ErroresAviso,
  type FormularioAviso,
  type VarianteAviso,
} from '../api/avisos'
import { CORREO_MAX, MENSAJE_CONTACTO_FALLO, MENSAJE_REVISA_DATOS, NOMBRE_MAX, TELEFONO_MAX } from '../api/contactos'
import { ApiError } from '../api/http'
import { AutorizacionDatos } from './AutorizacionDatos'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo, CampoAreaTexto, CampoSelect } from './Campo'
import { NotaObligatorio } from './NotaObligatorio'

/** Sesión del visitante, si la tiene (comprador, asesor o administrador). */
export type SesionAviso = { token: string; perfil: PerfilUsuario }

type TextosAviso = {
  titulo: string
  intro: string
  etiquetaTelefono: string
  nombreFormulario: string
  prefijoId: string
}

export const TEXTOS_AVISO: Record<VarianteAviso, TextosAviso> = {
  VENDE_TU_CAMION: {
    titulo: 'Vende tu camión',
    intro:
      'Cuéntanos qué vehículo quieres vender y te contactaremos por teléfono o WhatsApp. No necesitas cuenta.',
    etiquetaTelefono: 'Teléfono WhatsApp',
    nombreFormulario: 'Formulario Vende tu camión',
    prefijoId: 'vende',
  },
  SEGURO_TODO_RIESGO: {
    titulo: 'Compra tu seguro todo riesgo',
    intro:
      'Déjanos tus datos y el tipo de vehículo, y te contactaremos para ayudarte con tu seguro todo riesgo. No necesitas cuenta.',
    etiquetaTelefono: 'Teléfono',
    nombreFormulario: 'Formulario Compra tu seguro todo riesgo',
    prefijoId: 'seguro',
  },
}

function formularioInicial(perfil: PerfilUsuario | null): FormularioAviso {
  return {
    nombre: perfil?.nombreCompleto ?? '',
    telefono: perfil?.phoneNumber ?? '',
    correo: perfil?.email ?? '',
    descripcion: '',
    tipoVehiculo: '',
    autorizacionDatos: false,
  }
}

type AvisoPublicoProps = {
  variante: VarianteAviso
  sesion: SesionAviso | null
}

export function AvisoPublico({ variante, sesion }: AvisoPublicoProps) {
  const textos = TEXTOS_AVISO[variante]
  const id = (campo: string) => `${textos.prefijoId}-${campo}`
  const perfil = sesion?.perfil ?? null
  const [form, setForm] = useState<FormularioAviso>(() => formularioInicial(perfil))
  const [sitioWeb, setSitioWeb] = useState('')
  const [errores, setErrores] = useState<ErroresAviso>({})
  const [enviando, setEnviando] = useState(false)
  const [aviso, setAviso] = useState<DatosAviso | null>(null)
  // Evita un segundo envío aunque se pulse dos veces antes de que React vuelva a pintar el botón.
  const enviandoRef = useRef(false)
  const refAviso = useRef<HTMLDivElement>(null)
  const refNombre = useRef<HTMLInputElement>(null)
  const refTelefono = useRef<HTMLInputElement>(null)
  const refCorreo = useRef<HTMLInputElement>(null)
  const refDescripcion = useRef<HTMLTextAreaElement>(null)
  const refTipo = useRef<HTMLSelectElement>(null)
  const refAutorizacion = useRef<HTMLInputElement>(null)

  // Si la sesión se verifica después de abrir la pantalla, se prellenan los campos que sigan vacíos.
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

  function enfocarPrimerError(e: ErroresAviso) {
    const refs: Record<CampoAviso, { current: HTMLElement | null }> = {
      nombre: refNombre,
      telefono: refTelefono,
      correo: refCorreo,
      descripcion: refDescripcion,
      tipoVehiculo: refTipo,
      autorizacionDatos: refAutorizacion,
    }
    const primero = CAMPOS_AVISO[variante].find((c) => (e[c]?.length ?? 0) > 0)
    if (primero) window.setTimeout(() => refs[primero].current?.focus(), 0)
  }

  function cambiar<K extends keyof FormularioAviso>(campo: K, valor: FormularioAviso[K]) {
    setForm((f) => ({ ...f, [campo]: valor }))
    if (errores[campo]) setErrores((e) => ({ ...e, [campo]: undefined }))
  }

  function mostrarAviso(datos: DatosAviso) {
    setAviso(datos)
    window.setTimeout(() => refAviso.current?.focus(), 0)
  }

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (enviandoRef.current) return
    setAviso(null)
    const locales = validarAviso(variante, form)
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
      const mensaje =
        variante === 'VENDE_TU_CAMION'
          ? await enviarVendeTuCamion(datosVendeTuCamion(form, sitioWeb), sesion?.token)
          : await enviarSeguroTodoRiesgo(datosSeguroTodoRiesgo(form, sitioWeb), sesion?.token)
      // CA-P5b-28: el formulario se vacía (con sesión vuelve a los datos del perfil) y la casilla se desmarca.
      setForm(formularioInicial(perfilRef.current))
      setSitioWeb('')
      mostrarAviso({ tipo: 'exito', texto: mensaje })
    } catch (error) {
      // CA-P5b-29: cada error junto a su campo; se conservan los datos escritos.
      if (error instanceof ApiError && error.status === 400 && Object.keys(error.errores).length > 0) {
        const { porCampo, otros } = erroresDeServidorAviso(variante, error.errores)
        setErrores(porCampo)
        setAviso({ tipo: 'error', texto: [error.mensaje, ...otros].join(' ') })
        if (Object.keys(porCampo).length > 0) enfocarPrimerError(porCampo)
        else window.setTimeout(() => refAviso.current?.focus(), 0)
        return
      }
      mostrarAviso({ tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_CONTACTO_FALLO })
    } finally {
      enviandoRef.current = false
      setEnviando(false)
    }
  }

  return (
    <div className="cat-pagina aviso-publico">
      <h1 className="cat-titulo" tabIndex={-1}>
        {textos.titulo}
      </h1>
      <p className="aviso-publico-intro">{textos.intro}</p>

      <form
        className="contacto-form aviso-publico-form"
        onSubmit={enviar}
        noValidate
        aria-busy={enviando}
        aria-label={textos.nombreFormulario}
      >
        <NotaObligatorio />
        <Campo
          ref={refNombre}
          id={id('nombre')}
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
          id={id('telefono')}
          etiqueta={textos.etiquetaTelefono}
          tipo="tel"
          valor={form.telefono}
          onCambio={(v) => cambiar('telefono', v)}
          errores={errores.telefono}
          autoComplete="tel"
          inputMode="tel"
          maxLength={TELEFONO_MAX}
          describidoPor={id('telefono-ayuda')}
          deshabilitado={enviando}
        />
        <p id={id('telefono-ayuda')} className="campo-ayuda">
          Celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567.
        </p>
        <Campo
          ref={refCorreo}
          id={id('correo')}
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
        {variante === 'VENDE_TU_CAMION' ? (
          <CampoAreaTexto
            ref={refDescripcion}
            id={id('descripcion')}
            etiqueta="Descripción del vehículo"
            valor={form.descripcion}
            onCambio={(v) => cambiar('descripcion', v)}
            errores={errores.descripcion}
            maxLength={DESCRIPCION_MAX}
            filas={6}
            ayuda="Cuéntanos marca, modelo, año, kilometraje y ciudad. Máximo 500 caracteres."
            contador
            deshabilitado={enviando}
          />
        ) : (
          <CampoSelect
            ref={refTipo}
            id={id('tipo-vehiculo')}
            etiqueta="Tipo de vehículo"
            valor={form.tipoVehiculo}
            onCambio={(v) => cambiar('tipoVehiculo', v)}
            opciones={TIPOS_VEHICULO_AVISO}
            textoVacio="Elige el tipo de vehículo"
            errores={errores.tipoVehiculo}
            deshabilitado={enviando}
          />
        )}
        <AutorizacionDatos
          ref={refAutorizacion}
          id={id('autorizacion')}
          marcada={form.autorizacionDatos}
          onCambio={(v) => cambiar('autorizacionDatos', v)}
          errores={errores.autorizacionDatos}
          deshabilitado={enviando}
        />

        {/* Campo trampa: fuera de la vista, del orden de tabulación y de los lectores de pantalla (CA-P5b-57). */}
        <div className="campo-trampa" aria-hidden="true">
          <label htmlFor={id('sitio-web')}>Sitio web (deja este campo vacío)</label>
          <input
            id={id('sitio-web')}
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
        </div>

        <button type="submit" disabled={enviando}>
          {enviando ? 'Enviando...' : 'Enviar'}
        </button>
      </form>
    </div>
  )
}
