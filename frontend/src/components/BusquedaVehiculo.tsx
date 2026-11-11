// P6 — "Busco un vehículo" (HU-034, RN-BU-001, D-56 a A): #/busco-un-vehiculo. Sin cuenta o con cualquier sesión;
// con sesión se prellenan los datos del perfil y se envía el token para ligarla a la cuenta (CA-P6-23).
// Categoría obligatoria; marca, modelo, años, presupuesto, departamento y ciudad (DIVIPOLA, D-29) y descripción
// opcionales. La búsqueda no se publica ni la ve ningún asesor (CA-P6-24): la atiende la administración (D-58 A).
import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import type { OpcionesFiltros } from '../api/catalogo'
import { CORREO_MAX, MENSAJE_CONTACTO_FALLO, MENSAJE_REVISA_DATOS, NOMBRE_MAX, TELEFONO_MAX } from '../api/contactos'
import { ApiError } from '../api/http'
import {
  CAMPOS_BUSQUEDA,
  datosBusqueda,
  DESCRIPCION_BUSQUEDA_MAX,
  enviarBusqueda,
  lineasPara,
  municipiosPara,
  opcionesBusquedaDesdeFiltros,
  repartirErrores,
  validarBusqueda,
  type CampoBusqueda,
  type ErroresBusqueda,
  type FormularioBusqueda as Formulario,
  type OpcionesBusqueda,
} from '../api/solicitudes'
import { mostrarConMiles, soloDigitos } from '../catalogo/formato'
import type { EstadoPeticion } from '../catalogo/usePeticion'
import { AutorizacionDatos } from './AutorizacionDatos'
import { Aviso, type DatosAviso } from './Aviso'
import type { SesionAviso } from './AvisoPublico'
import { Campo, CampoAreaTexto, CampoSelect } from './Campo'
import { NotaObligatorio } from './NotaObligatorio'
import { CampoTrampa, ConfirmacionSolicitud } from './SolicitudComun'

export const TITULO_BUSQUEDA = 'Busco un vehículo'
const INTRO_BUSQUEDA =
  'Cuéntanos qué vehículo buscas y te contactaremos por teléfono o WhatsApp con las opciones que encontremos. No necesitas cuenta.'

type BusquedaVehiculoProps = {
  /** Opciones públicas del catálogo (las mismas del filtro de resultados). */
  opciones: EstadoPeticion<OpcionesFiltros>
  onReintentarOpciones: () => void
  sesion: SesionAviso | null
}

/** Pantalla #/busco-un-vehiculo: espera las listas del catálogo y muestra el formulario. */
export function BusquedaVehiculo({ opciones, onReintentarOpciones, sesion }: BusquedaVehiculoProps) {
  const listas = useMemo(
    () => (opciones.estado === 'ok' ? opcionesBusquedaDesdeFiltros(opciones.datos) : null),
    [opciones],
  )
  return (
    <div className="cat-pagina aviso-publico">
      <h1 className="cat-titulo" tabIndex={-1}>
        {TITULO_BUSQUEDA}
      </h1>
      {opciones.estado === 'cargando' && (
        <p className="cat-estado" role="status" aria-busy="true">
          Cargando las opciones...
        </p>
      )}
      {opciones.estado === 'error' && (
        <div className="cat-estado">
          <Aviso aviso={{ tipo: 'error', texto: 'No pudimos cargar las categorías. Intenta de nuevo.' }} />
          <button type="button" className="cat-boton" onClick={onReintentarOpciones}>
            Reintentar
          </button>
        </div>
      )}
      {listas !== null && listas.tipos.length === 0 && (
        <Aviso
          aviso={{
            tipo: 'advertencia',
            texto: 'Por ahora no podemos recibir búsquedas aquí. Escríbenos por WhatsApp al 316 615 2907.',
          }}
        />
      )}
      {listas !== null && listas.tipos.length > 0 && <FormularioBusqueda opciones={listas} sesion={sesion} />}
    </div>
  )
}

function formularioInicial(sesion: SesionAviso | null): Formulario {
  const perfil = sesion?.perfil ?? null
  return {
    tipoId: '',
    marcaId: '',
    lineaId: '',
    anioDesde: '',
    anioHasta: '',
    presupuestoMaximo: '',
    departamentoId: '',
    municipioId: '',
    descripcion: '',
    nombre: perfil?.nombreCompleto ?? '',
    telefono: perfil?.phoneNumber ?? '',
    correo: perfil?.email ?? '',
    autorizacionDatos: false,
  }
}

const opcionesSelect = (lista: readonly { id: number; nombre: string }[]) =>
  lista.map((o) => ({ valor: String(o.id), nombre: o.nombre }))

type FormularioBusquedaProps = {
  opciones: OpcionesBusqueda
  sesion: SesionAviso | null
}

/** Formulario de la búsqueda; recibe las listas ya cargadas. */
export function FormularioBusqueda({ opciones, sesion }: FormularioBusquedaProps) {
  const [form, setForm] = useState<Formulario>(() => formularioInicial(sesion))
  const [sitioWeb, setSitioWeb] = useState('')
  const [errores, setErrores] = useState<ErroresBusqueda>({})
  const [enviando, setEnviando] = useState(false)
  const [aviso, setAviso] = useState<DatosAviso | null>(null)
  const [enviado, setEnviado] = useState<string | null>(null)
  const enviandoRef = useRef(false)
  const refAviso = useRef<HTMLDivElement>(null)
  const refConfirmacion = useRef<HTMLDivElement>(null)
  const refCategoria = useRef<HTMLSelectElement>(null)

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

  const lineas = lineasPara(opciones, form.marcaId, form.tipoId)
  const municipios = municipiosPara(opciones, form.departamentoId)
  const conDivipola = opciones.departamentos.length > 0
  const id = (campo: CampoBusqueda) => `busqueda-${campo}`

  function enfocarPrimerError(e: ErroresBusqueda) {
    const primero = CAMPOS_BUSQUEDA.find((c) => (e[c]?.length ?? 0) > 0)
    if (primero) window.setTimeout(() => document.getElementById(id(primero))?.focus(), 0)
  }

  function cambiar<K extends keyof Formulario>(campo: K, valor: Formulario[K]) {
    setForm((f) => {
      const nuevo = { ...f, [campo]: valor }
      // Listas dependientes: el modelo depende de la marca y de la categoría; la ciudad, del departamento.
      if (campo === 'marcaId' && valor !== f.marcaId) nuevo.lineaId = ''
      if (campo === 'tipoId' && nuevo.lineaId !== '') {
        const vigentes = lineasPara(opciones, nuevo.marcaId, nuevo.tipoId)
        if (!vigentes.some((l) => String(l.id) === nuevo.lineaId)) nuevo.lineaId = ''
      }
      if (campo === 'departamentoId' && valor !== f.departamentoId) nuevo.municipioId = ''
      return nuevo
    })
    if (errores[campo as CampoBusqueda]) setErrores((e) => ({ ...e, [campo]: undefined }))
  }

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (enviandoRef.current) return
    setAviso(null)
    const locales = validarBusqueda(form, opciones)
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
      const mensaje = await enviarBusqueda(datosBusqueda(form, sitioWeb), sesion?.token)
      setForm(formularioInicial(sesionRef.current))
      setSitioWeb('')
      setEnviado(mensaje)
      window.setTimeout(() => refConfirmacion.current?.focus(), 0)
    } catch (error) {
      if (error instanceof ApiError && error.status === 400 && Object.keys(error.errores).length > 0) {
        const { porCampo, otros } = repartirErrores(CAMPOS_BUSQUEDA, error.errores)
        setErrores(porCampo)
        setAviso({ tipo: 'error', texto: [error.mensaje, ...otros].join(' ') })
        if (Object.keys(porCampo).length > 0) enfocarPrimerError(porCampo)
        else window.setTimeout(() => refAviso.current?.focus(), 0)
        return
      }
      setAviso({ tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_CONTACTO_FALLO })
      window.setTimeout(() => refAviso.current?.focus(), 0)
    } finally {
      enviandoRef.current = false
      setEnviando(false)
    }
  }

  function enviarOtra() {
    setEnviado(null)
    window.setTimeout(() => refCategoria.current?.focus(), 0)
  }

  if (enviado !== null) {
    return <ConfirmacionSolicitud ref={refConfirmacion} mensaje={enviado} onEnviarOtra={enviarOtra} />
  }

  return (
    <>
      <p className="aviso-publico-intro">{INTRO_BUSQUEDA}</p>
      <form
        className="contacto-form aviso-publico-form"
        onSubmit={enviar}
        noValidate
        aria-busy={enviando}
        aria-label="Formulario Busco un vehículo"
      >
        <NotaObligatorio />
        <fieldset className="busqueda-grupo">
          <legend>¿Qué vehículo buscas?</legend>
          <CampoSelect
            ref={refCategoria}
            id={id('tipoId')}
            etiqueta="Categoría"
            valor={form.tipoId}
            onCambio={(v) => cambiar('tipoId', v)}
            opciones={opcionesSelect(opciones.tipos)}
            textoVacio="Elige la categoría"
            errores={errores.tipoId}
            deshabilitado={enviando}
          />
          <CampoSelect
            id={id('marcaId')}
            etiqueta="Marca (opcional)"
            valor={form.marcaId}
            onCambio={(v) => cambiar('marcaId', v)}
            opciones={opcionesSelect(opciones.marcas)}
            textoVacio="Cualquier marca"
            errores={errores.marcaId}
            requerido={false}
            deshabilitado={enviando}
          />
          <CampoSelect
            id={id('lineaId')}
            etiqueta="Modelo (opcional)"
            valor={form.lineaId}
            onCambio={(v) => cambiar('lineaId', v)}
            opciones={opcionesSelect(lineas)}
            textoVacio={form.marcaId === '' ? 'Elige primero la marca' : 'Cualquier modelo'}
            errores={errores.lineaId}
            requerido={false}
            deshabilitado={enviando || form.marcaId === ''}
          />
          <div className="busqueda-fila">
            <Campo
              id={id('anioDesde')}
              etiqueta="Año desde (opcional)"
              valor={form.anioDesde}
              onCambio={(v) => cambiar('anioDesde', soloDigitos(v))}
              errores={errores.anioDesde}
              inputMode="numeric"
              maxLength={4}
              requerido={false}
              deshabilitado={enviando}
            />
            <Campo
              id={id('anioHasta')}
              etiqueta="Año hasta (opcional)"
              valor={form.anioHasta}
              onCambio={(v) => cambiar('anioHasta', soloDigitos(v))}
              errores={errores.anioHasta}
              inputMode="numeric"
              maxLength={4}
              requerido={false}
              deshabilitado={enviando}
            />
          </div>
          <Campo
            id={id('presupuestoMaximo')}
            etiqueta="Presupuesto máximo en pesos (opcional)"
            valor={mostrarConMiles(form.presupuestoMaximo)}
            onCambio={(v) => cambiar('presupuestoMaximo', soloDigitos(v))}
            errores={errores.presupuestoMaximo}
            inputMode="numeric"
            maxLength={13}
            describidoPor={`${id('presupuestoMaximo')}-ayuda`}
            requerido={false}
            deshabilitado={enviando}
          />
          <p id={`${id('presupuestoMaximo')}-ayuda`} className="campo-ayuda">
            Entre 1.000.000 y 5.000.000.000.
          </p>
          {conDivipola && (
            <div className="busqueda-fila">
              <CampoSelect
                id={id('departamentoId')}
                etiqueta="Departamento (opcional)"
                valor={form.departamentoId}
                onCambio={(v) => cambiar('departamentoId', v)}
                opciones={opcionesSelect(opciones.departamentos)}
                textoVacio="Cualquier departamento"
                errores={errores.departamentoId}
                requerido={false}
                deshabilitado={enviando}
              />
              <CampoSelect
                id={id('municipioId')}
                etiqueta="Ciudad (opcional)"
                valor={form.municipioId}
                onCambio={(v) => cambiar('municipioId', v)}
                opciones={opcionesSelect(municipios)}
                textoVacio={form.departamentoId === '' ? 'Elige primero el departamento' : 'Cualquier ciudad'}
                errores={errores.municipioId}
                requerido={false}
                deshabilitado={enviando || form.departamentoId === ''}
              />
            </div>
          )}
          <CampoAreaTexto
            id={id('descripcion')}
            etiqueta="Cuéntanos qué buscas (opcional)"
            valor={form.descripcion}
            onCambio={(v) => cambiar('descripcion', v)}
            errores={errores.descripcion}
            maxLength={DESCRIPCION_BUSQUEDA_MAX}
            filas={4}
            ayuda="Por ejemplo: carrocería, capacidad de carga o kilometraje."
            contador
            requerido={false}
            deshabilitado={enviando}
          />
        </fieldset>

        <fieldset className="busqueda-grupo">
          <legend>Tus datos de contacto</legend>
          <Campo
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
            id={id('telefono')}
            etiqueta="Teléfono o WhatsApp"
            tipo="tel"
            valor={form.telefono}
            onCambio={(v) => cambiar('telefono', v)}
            errores={errores.telefono}
            autoComplete="tel"
            inputMode="tel"
            maxLength={TELEFONO_MAX}
            describidoPor="busqueda-telefono-ayuda"
            deshabilitado={enviando}
          />
          <p id="busqueda-telefono-ayuda" className="campo-ayuda">
            Celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567.
          </p>
          <Campo
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
        </fieldset>
        <AutorizacionDatos
          id={id('autorizacionDatos')}
          marcada={form.autorizacionDatos}
          onCambio={(v) => cambiar('autorizacionDatos', v)}
          errores={errores.autorizacionDatos}
          deshabilitado={enviando}
        />
        <CampoTrampa id="busqueda-sitio-web" valor={sitioWeb} onCambio={setSitioWeb} />

        <div ref={refAviso} tabIndex={-1} className="contacto-aviso">
          <Aviso aviso={aviso} />
        </div>

        <button type="submit" disabled={enviando}>
          {enviando ? 'Enviando...' : 'Enviar'}
        </button>
      </form>
    </>
  )
}
