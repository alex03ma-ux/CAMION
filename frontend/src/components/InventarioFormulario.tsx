// Registrar y editar un vehículo del inventario (HU-013, HU-015 a HU-019,
// RN-VE-002, RN-VE-003, RN-VE-005, RN-VE-007, RN-VE-009, RN-VE-010, D-2, D-17, D-19).
// Primero se elige la categoría; solo se muestran los campos que le aplican.
import { useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from 'react'
import {
  crearVehiculo,
  editarVehiculo,
  MENSAJE_EDITAR_SIN_FOTOS,
  MENSAJE_VEHICULO_NO_ENCONTRADO,
  obtenerVehiculoInventario,
  type CatalogosInventario,
  type DetalleInventario,
  type FotoInventario,
  type Opcion,
} from '../api/inventario'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { formatoCop, mostrarConMiles, soloDigitos } from '../catalogo/formato'
import { esEditable, nombreEstado, tituloVehiculo } from '../inventario/estados'
import {
  anioMaximo,
  compararPlaca,
  construirCuerpo,
  DESCRIPCION_MAX,
  formularioDesdeDetalle,
  formularioVacio,
  MENSAJE_REVISA_VEHICULO,
  repartirErroresServidor,
  separarErroresFotos,
  textoFotos,
  validarFormulario,
  type CampoFormulario,
  type ErroresFormulario,
  type FormularioVehiculo,
} from '../inventario/formulario'
import {
  aplica,
  categoriaDeTipo,
  esObligatorio,
  etiquetaConObligatoriedad,
  NOMBRES_CATEGORIA,
  type Categoria,
  type CampoVehiculo,
} from '../inventario/reglasCategoria'
import { HASH_INVENTARIO } from '../rutas'
import {
  descartarAvisoPendiente,
  dejarAvisoPendiente,
  marcarVehiculoReciente,
  textoListo,
} from '../inventario/avisoPendiente'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo, CampoSelect } from './Campo'
import { FotosNuevas, ID_INPUT_FOTOS_NUEVAS, type FotoElegida } from './FotosNuevas'
import { FotosVehiculo } from './FotosVehiculo'
import { NotaObligatorio } from './NotaObligatorio'

type InventarioFormularioProps = {
  token: string
  esAdmin: boolean
  catalogos: CatalogosInventario
  /** null = registrar; un id = editar. */
  vehiculoId: number | null
  avisoInicial: DatosAviso | null
  onSesionTerminada: () => void
  /**
   * Tras crear (con sus fotos, D-30): la página navega a la edición del vehículo
   * nuevo y muestra allí `aviso` (éxito, o advertencia si la placa no se guardó).
   */
  onCreado: (detalle: DetalleInventario, aviso: DatosAviso) => void
}

/** Orden de los grupos del formulario; cada campo aparece solo si aplica a la categoría. */
const GRUPOS: readonly { titulo: string; campos: readonly CampoVehiculo[] }[] = [
  {
    titulo: 'Identificación',
    campos: ['marcaId', 'lineaId', 'modeloAnio', 'tipoMaquinaId', 'carroceriaId', 'placa'],
  },
  {
    titulo: 'Características',
    campos: [
      'kilometraje',
      'horasUso',
      'largoM',
      'anchoM',
      'altoM',
      'materialId',
      'combustibleId',
      'cilindrajeCc',
      'pesoBrutoKg',
      'capacidadCargaKg',
    ],
  },
  { titulo: 'Ubicación y precio', campos: ['municipioId', 'precioCop', 'descripcionTecnica'] },
]

/** Orden de enfoque al haber errores (igual al visual). */
const ORDEN_FOCO: readonly CampoFormulario[] = [
  'asesorId',
  'tipoId',
  ...GRUPOS.flatMap((g) =>
    g.campos.flatMap((c): CampoFormulario[] => (c === 'municipioId' ? ['departamentoId', c] : [c])),
  ),
]

function idCampo(campo: CampoFormulario): string {
  return `inv-${campo}`
}

/** Cajas de texto cuyo valor se relee de la pantalla al guardar (las listas no lo necesitan). */
const CAMPOS_TEXTO = [
  'modeloAnio', 'placa', 'kilometraje', 'horasUso', 'largoM', 'anchoM', 'altoM',
  'cilindrajeCc', 'pesoBrutoKg', 'capacidadCargaKg', 'descripcionTecnica',
] as const

function sincronizarConPantalla(f: FormularioVehiculo): FormularioVehiculo {
  let resultado = f
  for (const campo of CAMPOS_TEXTO) {
    const el = document.getElementById(idCampo(campo)) as HTMLInputElement | HTMLTextAreaElement | null
    if (!el) continue
    const enPantalla = campo === 'placa' ? el.value.toUpperCase() : el.value
    if (enPantalla !== f[campo]) resultado = { ...resultado, [campo]: enPantalla }
  }
  return resultado
}

function opciones(lista: readonly Opcion[]) {
  return lista.map((o) => ({ valor: String(o.id), nombre: o.nombre }))
}

function categoriaPorTipoId(catalogos: CatalogosInventario, tipoId: string): Categoria | null {
  const tipo = catalogos.tipos.find((t) => String(t.id) === tipoId)
  return tipo ? categoriaDeTipo(tipo.nombre) : null
}

/** Título con los nombres de marca y línea de los catálogos (el detalle trae ids). */
function tituloDetalle(detalle: DetalleInventario, catalogos: CatalogosInventario): string {
  const marca = catalogos.marcas.find((m) => m.id === detalle.marcaId)
  const linea = marca?.lineas.find((l) => l.id === detalle.lineaId)
  return tituloVehiculo({
    tipo: detalle.tipo,
    marca: marca?.nombre ?? null,
    linea: linea?.nombre ?? null,
    modeloAnio: detalle.modeloAnio,
  })
}

type Carga =
  | { estado: 'nuevo' }
  | { estado: 'cargando' }
  | { estado: 'error'; aviso: DatosAviso; noEncontrado: boolean }
  | { estado: 'ok'; detalle: DetalleInventario }

export function InventarioFormulario({
  token,
  esAdmin,
  catalogos,
  vehiculoId,
  avisoInicial,
  onSesionTerminada,
  onCreado,
}: InventarioFormularioProps) {
  const creando = vehiculoId === null
  const [carga, setCarga] = useState<Carga>(creando ? { estado: 'nuevo' } : { estado: 'cargando' })
  const [intento, setIntento] = useState(0)
  const [form, setForm] = useState<FormularioVehiculo>(formularioVacio)
  const [errores, setErrores] = useState<ErroresFormulario>({})
  const [aviso, setAviso] = useState<DatosAviso | null>(avisoInicial)
  const [enviando, setEnviando] = useState(false)
  // D-30: fotos elegidas al registrar (viajan con los datos) y errores del servidor sobre ellas.
  const [fotosNuevas, setFotosNuevas] = useState<FotoElegida[]>([])
  const [erroresFotos, setErroresFotos] = useState<string[]>([])
  const refAviso = useRef<HTMLDivElement>(null)

  const anioActual = useMemo(() => new Date().getFullYear(), [])

  // El aviso que dejó la pantalla anterior ya se mostró: se descarta.
  useEffect(() => {
    descartarAvisoPendiente()
  }, [])

  // Carga del detalle al editar.
  useEffect(() => {
    if (vehiculoId === null) return
    const control = new AbortController()
    setCarga({ estado: 'cargando' })
    obtenerVehiculoInventario(token, vehiculoId, control.signal)
      .then((detalle) => {
        setCarga({ estado: 'ok', detalle })
        setForm(formularioDesdeDetalle(detalle, catalogos))
        setErrores({})
      })
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        if (error instanceof ApiError && error.status === 401) {
          onSesionTerminada()
          return
        }
        const noEncontrado = error instanceof ApiError && error.status === 404
        setCarga({
          estado: 'error',
          noEncontrado,
          aviso: {
            tipo: 'error',
            texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO,
          },
        })
      })
    return () => control.abort()
  }, [token, vehiculoId, catalogos, intento, onSesionTerminada])

  const detalle = carga.estado === 'ok' ? carga.detalle : null

  const categoria: Categoria | null = creando
    ? categoriaPorTipoId(catalogos, form.tipoId)
    : detalle
      ? (categoriaDeTipo(detalle.tipo) ?? categoriaPorTipoId(catalogos, form.tipoId))
      : null

  const editable = creando || (detalle !== null && esEditable(detalle.estado))
  // D-30: sin fotos no se registra ni se guardan cambios (la API responde 400/409).
  const faltanFotos = creando ? fotosNuevas.length === 0 : detalle !== null && detalle.fotos.length === 0
  const pedirAsesor = creando && esAdmin

  const tiposDisponibles = catalogos.tipos.filter((t) => categoriaDeTipo(t.nombre) !== null)
  const marca = catalogos.marcas.find((m) => String(m.id) === form.marcaId)
  const departamento = catalogos.departamentos.find((d) => String(d.id) === form.departamentoId)

  function visible(campo: CampoFormulario): boolean {
    if (campo === 'tipoId') return creando
    if (campo === 'asesorId') return pedirAsesor
    if (campo === 'departamentoId') return categoria !== null
    return categoria !== null && aplica(categoria, campo)
  }

  function cambiar(campo: CampoFormulario, valor: string) {
    setForm((f) => {
      const nuevo = { ...f, [campo]: valor }
      // Marca → línea y departamento → municipio: se limpia el dependiente si ya no corresponde.
      if (campo === 'marcaId') {
        const m = catalogos.marcas.find((x) => String(x.id) === valor)
        if (!m || !m.lineas.some((l) => String(l.id) === f.lineaId)) nuevo.lineaId = ''
      }
      if (campo === 'departamentoId') {
        const d = catalogos.departamentos.find((x) => String(x.id) === valor)
        if (!d || !d.municipios.some((m) => String(m.id) === f.municipioId)) nuevo.municipioId = ''
      }
      return nuevo
    })
    if (errores[campo]) {
      setErrores((e) => {
        const copia = { ...e }
        delete copia[campo]
        return copia
      })
    }
  }

  function enfocarPrimerError(lista: ErroresFormulario, irAFotos = false) {
    const primero = ORDEN_FOCO.find((c) => (lista[c]?.length ?? 0) > 0 && visible(c))
    window.setTimeout(() => {
      if (primero) document.getElementById(idCampo(primero))?.focus()
      else if (irAFotos) document.getElementById(ID_INPUT_FOTOS_NUEVAS)?.focus()
      else refAviso.current?.focus()
    }, 0)
  }

  function cambiarFotosNuevas(lista: FotoElegida[]) {
    setFotosNuevas(lista)
    setErroresFotos([])
  }

  async function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (enviando || !editable || faltanFotos) return
    setAviso(null)
    setErroresFotos([])
    // Defecto de la prueba manual (placa de camiones): se toma lo que está escrito en pantalla
    // por si el navegador (p. ej. su autocompletado) llenó una caja sin avisar a React.
    const vigente = sincronizarConPantalla(form)
    if (vigente !== form) setForm(vigente)
    const validacion = validarFormulario(vigente, categoria, { pedirAsesor, anioActual, catalogos })
    setErrores(validacion)
    if (Object.keys(validacion).length > 0 || categoria === null) {
      setAviso({ tipo: 'error', texto: MENSAJE_REVISA_VEHICULO })
      enfocarPrimerError(validacion)
      return
    }
    const cuerpo = construirCuerpo(vigente, categoria, pedirAsesor)
    setEnviando(true)
    try {
      if (vehiculoId === null) {
        const creado = await crearVehiculo(
          token,
          cuerpo,
          fotosNuevas.map((f) => f.archivo),
        )
        const placa = compararPlaca(cuerpo.placa, creado.placa)
        const guardado = `Vehículo guardado con ${textoFotos(creado.fotos.length)}.`
        onCreado(
          creado,
          placa.estado === 'difiere'
            ? { tipo: 'advertencia', texto: `${guardado} ${placa.texto}` }
            : {
                tipo: 'exito',
                texto: [
                  guardado,
                  placa.estado === 'coincide' ? placa.texto : null,
                  `Puedes agregar más o pulsar «${textoListo(esAdmin)}».`,
                ]
                  .filter(Boolean)
                  .join(' '),
              },
        )
        return
      }
      const actualizado = await editarVehiculo(token, vehiculoId, cuerpo)
      setCarga({ estado: 'ok', detalle: actualizado })
      setForm(formularioDesdeDetalle(actualizado, catalogos))
      setErrores({})
      marcarVehiculoReciente(actualizado.id)
      const placa = compararPlaca(cuerpo.placa, actualizado.placa)
      setAviso(
        placa.estado === 'difiere'
          ? { tipo: 'advertencia', texto: `Cambios guardados. ${placa.texto}` }
          : {
              tipo: 'exito',
              texto: [
                'Cambios guardados.',
                placa.estado === 'coincide' ? placa.texto : null,
                `Puedes agregar o reordenar fotos abajo; cuando termines, pulsa «${textoListo(esAdmin)}».`,
              ]
                .filter(Boolean)
                .join(' '),
            },
      )
      if (placa.estado === 'difiere') {
        window.setTimeout(() => {
          const campo = document.getElementById(idCampo('placa'))
          if (campo) campo.focus()
          else refAviso.current?.focus()
        }, 0)
      }
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        onSesionTerminada()
        return
      }
      if (error instanceof ApiError && (error.status === 400 || error.status === 409)) {
        // D-30: al registrar, los errores de `fotos` van a la sección de fotos.
        const { fotos: deFotos, resto } = creando
          ? separarErroresFotos(error.errores)
          : { fotos: [], resto: error.errores }
        setErroresFotos(deFotos)
        const { porCampo, otros } = repartirErroresServidor(resto, visible)
        setErrores(porCampo)
        const tienePorCampo = Object.keys(porCampo).length > 0
        const texto =
          tienePorCampo || deFotos.length > 0
            ? [MENSAJE_REVISA_VEHICULO, ...otros].join(' ')
            : [error.mensaje, ...otros.filter((m) => m !== error.mensaje)].join(' ')
        setAviso({ tipo: 'error', texto })
        enfocarPrimerError(porCampo, deFotos.length > 0)
        return
      }
      setAviso({ tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO })
      enfocarPrimerError({})
    } finally {
      setEnviando(false)
    }
  }

  function actualizarFotos(fotos: FotoInventario[]) {
    setCarga((c) => (c.estado === 'ok' ? { estado: 'ok', detalle: { ...c.detalle, fotos } } : c))
  }

  // ---- Campos ----

  function select(campo: CampoFormulario, etiqueta: string, lista: readonly Opcion[], extra?: {
    textoVacio?: string
    deshabilitado?: boolean
    requerido?: boolean
    describidoPor?: string
  }) {
    return (
      <CampoSelect
        key={campo}
        id={idCampo(campo)}
        etiqueta={etiqueta}
        valor={form[campo]}
        onCambio={(v) => cambiar(campo, v)}
        opciones={opciones(lista)}
        textoVacio={extra?.textoVacio ?? 'Elige una opción'}
        errores={errores[campo]}
        deshabilitado={extra?.deshabilitado}
        requerido={extra?.requerido ?? true}
        describidoPor={extra?.describidoPor}
      />
    )
  }

  function numerico(campo: CampoVehiculo, cat: Categoria, decimal = false, maxLength = 12) {
    return (
      <Campo
        key={campo}
        id={idCampo(campo)}
        etiqueta={etiquetaConObligatoriedad(cat, campo)}
        valor={form[campo]}
        onCambio={(v) => cambiar(campo, v)}
        inputMode={decimal ? 'decimal' : 'numeric'}
        maxLength={maxLength}
        autoComplete="off"
        errores={errores[campo]}
        requerido={esObligatorio(cat, campo)}
      />
    )
  }

  function renderCampo(campo: CampoVehiculo, cat: Categoria): ReactNode {
    const etiqueta = etiquetaConObligatoriedad(cat, campo)
    const requerido = esObligatorio(cat, campo)
    switch (campo) {
      case 'marcaId':
        return select(campo, etiqueta, catalogos.marcas, {
          requerido,
          textoVacio: requerido ? 'Elige la marca' : 'Sin marca',
        })
      case 'lineaId':
        return select(campo, etiqueta, marca?.lineas ?? [], {
          requerido,
          deshabilitado: !marca,
          textoVacio: marca ? 'Sin línea' : 'Elige primero la marca',
        })
      case 'modeloAnio':
        return (
          <Campo
            key={campo}
            id={idCampo(campo)}
            etiqueta={etiqueta}
            valor={form.modeloAnio}
            onCambio={(v) => cambiar(campo, soloDigitos(v))}
            inputMode="numeric"
            maxLength={4}
            autoComplete="off"
            errores={errores.modeloAnio}
            requerido={requerido}
            describidoPor="inv-modeloAnio-ayuda"
          />
        )
      case 'placa':
        return (
          <Campo
            key={campo}
            id={idCampo(campo)}
            etiqueta={etiqueta}
            valor={form.placa}
            onCambio={(v) => cambiar(campo, v.toUpperCase())}
            maxLength={10}
            autoComplete="off"
            errores={errores.placa}
            requerido={requerido}
            describidoPor="inv-placa-ayuda"
          />
        )
      case 'carroceriaId':
        return select(campo, etiqueta, catalogos.carrocerias, {
          requerido,
          textoVacio: requerido ? 'Elige el tipo de carrocería' : 'Sin carrocería',
        })
      case 'materialId':
        return select(campo, etiqueta, catalogos.materiales, { requerido, textoVacio: 'Sin especificar' })
      case 'tipoMaquinaId':
        return select(campo, etiqueta, catalogos.tiposMaquina, {
          requerido,
          textoVacio: 'Elige el tipo de máquina',
        })
      case 'combustibleId':
        return select(campo, etiqueta, catalogos.combustibles, { requerido, textoVacio: 'Sin especificar' })
      case 'largoM':
      case 'anchoM':
      case 'altoM':
        return numerico(campo, cat, true, 7)
      case 'kilometraje':
      case 'horasUso':
      case 'cilindrajeCc':
      case 'pesoBrutoKg':
      case 'capacidadCargaKg':
        return numerico(campo, cat)
      case 'municipioId':
        return (
          <div key={campo} className="inv-par">
            {select('departamentoId', 'Departamento', catalogos.departamentos, {
              textoVacio: 'Elige el departamento',
            })}
            {select(campo, etiqueta, departamento?.municipios ?? [], {
              requerido,
              deshabilitado: !departamento,
              textoVacio: departamento ? 'Elige el municipio' : 'Elige primero el departamento',
            })}
          </div>
        )
      case 'precioCop':
        return (
          <Campo
            key={campo}
            id={idCampo(campo)}
            etiqueta={etiqueta}
            valor={mostrarConMiles(form.precioCop)}
            onCambio={(v) => cambiar(campo, soloDigitos(v))}
            inputMode="numeric"
            maxLength={16}
            autoComplete="off"
            errores={errores.precioCop}
            requerido={requerido}
            describidoPor="inv-precioCop-ayuda"
          />
        )
      case 'descripcionTecnica':
        return renderDescripcion()
    }
  }

  function renderAyuda(campo: CampoVehiculo): ReactNode {
    if (campo === 'modeloAnio') {
      return (
        <p id="inv-modeloAnio-ayuda" className="campo-ayuda">
          Entre 1950 y {anioMaximo(anioActual)}.
        </p>
      )
    }
    if (campo === 'placa') {
      return (
        <p id="inv-placa-ayuda" className="campo-ayuda">
          Entre 5 y 7 letras y números. Se guarda en mayúsculas, sin espacios ni guiones.
        </p>
      )
    }
    if (campo === 'precioCop') {
      return (
        <p id="inv-precioCop-ayuda" className="campo-ayuda">
          En pesos colombianos, sin decimales.
          {detalle?.precioAnteriorCop != null && (
            <> Precio anterior: {formatoCop(detalle.precioAnteriorCop)}.</>
          )}
        </p>
      )
    }
    return null
  }

  function renderDescripcion(): ReactNode {
    const id = idCampo('descripcionTecnica')
    const lista = errores.descripcionTecnica ?? []
    const describedBy = [`${id}-ayuda`, lista.length > 0 ? `${id}-error` : undefined]
      .filter(Boolean)
      .join(' ')
    return (
      <div className="campo" key="descripcionTecnica">
        <label htmlFor={id}>{etiquetaConObligatoriedad(categoria ?? 'CAMION', 'descripcionTecnica')}</label>
        <textarea
          id={id}
          name={id}
          rows={5}
          value={form.descripcionTecnica}
          maxLength={DESCRIPCION_MAX}
          onChange={(e) => cambiar('descripcionTecnica', e.target.value)}
          aria-invalid={lista.length > 0}
          aria-describedby={describedBy}
        />
        <p id={`${id}-ayuda`} className="campo-ayuda">
          Hasta {DESCRIPCION_MAX} caracteres. Llevas {form.descripcionTecnica.trim().length}.
        </p>
        {lista.length > 0 && (
          <ul id={`${id}-error`} className="campo-errores">
            {lista.map((m) => (
              <li key={m}>{m}</li>
            ))}
          </ul>
        )}
      </div>
    )
  }

  // ---- Render ----

  const titulo = creando ? 'Registrar vehículo' : 'Editar vehículo'

  return (
    <>
      <h1 tabIndex={-1} className="inv-titulo">
        {titulo}
      </h1>
      <p className="inv-volver">
        <a href={HASH_INVENTARIO}>Volver al inventario</a>
      </p>

      {carga.estado === 'cargando' && <p role="status">Cargando vehículo...</p>}

      {carga.estado === 'error' && (
        <div className="inv-estado-carga">
          <Aviso aviso={carga.aviso} />
          {!carga.noEncontrado && (
            <button type="button" className="inv-boton-auto" onClick={() => setIntento((i) => i + 1)}>
              Reintentar
            </button>
          )}
          {carga.noEncontrado && carga.aviso.texto !== MENSAJE_VEHICULO_NO_ENCONTRADO && (
            <p>{MENSAJE_VEHICULO_NO_ENCONTRADO}</p>
          )}
        </div>
      )}

      {(carga.estado === 'nuevo' || carga.estado === 'ok') && (
        <>
          {detalle && (
            <section className="inv-resumen" aria-label="Resumen del vehículo">
              <p className="inv-resumen-titulo">{tituloDetalle(detalle, catalogos)}</p>
              <dl className="inv-datos">
                <div>
                  <dt>Categoría</dt>
                  <dd>
                    {categoria ? NOMBRES_CATEGORIA[categoria] : detalle.tipo || '—'}{' '}
                    <span className="dato-secundario">(no se puede cambiar)</span>
                  </dd>
                </div>
                <div>
                  <dt>Estado</dt>
                  <dd>
                    <span className={`estado inv-estado-${detalle.estado.toLowerCase() || 'desconocido'}`}>
                      {nombreEstado(detalle.estado)}
                    </span>
                  </dd>
                </div>
                <div>
                  <dt>Precio actual</dt>
                  <dd>{formatoCop(detalle.precioCop)}</dd>
                </div>
                {detalle.precioAnteriorCop !== null && (
                  <div>
                    <dt>Precio anterior</dt>
                    <dd>{formatoCop(detalle.precioAnteriorCop)}</dd>
                  </div>
                )}
                {esAdmin && detalle.asesor && (
                  <div>
                    <dt>Asesor</dt>
                    <dd>{detalle.asesor.nombre || '—'}</dd>
                  </div>
                )}
                {detalle.estado === 'RESERVADO' && detalle.notaReserva && (
                  <div>
                    <dt>Nota de la reserva</dt>
                    <dd className="inv-texto-largo">{detalle.notaReserva}</dd>
                  </div>
                )}
                {detalle.estado === 'INACTIVO' && detalle.motivoInactivacion && (
                  <div>
                    <dt>Motivo de inactivación</dt>
                    <dd className="inv-texto-largo">{detalle.motivoInactivacion}</dd>
                  </div>
                )}
              </dl>
            </section>
          )}

          {!editable && (
            <Aviso aviso={{ tipo: 'advertencia', texto: 'Este vehículo está vendido y no se puede modificar.' }} />
          )}

          <form className="inv-form" onSubmit={(e) => void enviar(e)} noValidate aria-busy={enviando}>
            <fieldset className="inv-fieldset" disabled={!editable || enviando}>
              <legend className="sr-only">{titulo}</legend>
              <NotaObligatorio />
              {creando && (
                <div className="inv-grupo">
                  <h2>Categoría</h2>
                  <p className="campo-ayuda">
                    Elige primero la categoría: según ella se muestran los datos que aplican. Después
                    de registrar no se puede cambiar.
                  </p>
                  <div className="inv-rejilla">
                    {pedirAsesor && (
                        <CampoSelect
                          id={idCampo('asesorId')}
                          etiqueta="Asesor propietario"
                          valor={form.asesorId}
                          onCambio={(v) => cambiar('asesorId', v)}
                          opciones={catalogos.asesores.map((a) => ({ valor: a.id, nombre: a.nombre }))}
                          textoVacio={
                            catalogos.asesores.length > 0 ? 'Elige el asesor' : 'No hay asesores activos'
                          }
                          errores={errores.asesorId}
                        />
                      )}
                    {select('tipoId', 'Categoría', tiposDisponibles, { textoVacio: 'Elige la categoría' })}
                  </div>
                </div>
              )}

              {categoria === null && creando && (
                <p className="admin-vacio">Elige una categoría para ver los datos que debes completar.</p>
              )}

              {categoria !== null &&
                GRUPOS.map((grupo) => {
                  const campos = grupo.campos.filter((c) => aplica(categoria, c))
                  if (campos.length === 0) return null
                  return (
                    <div className="inv-grupo" key={grupo.titulo}>
                      <h2>{grupo.titulo}</h2>
                      <div className="inv-rejilla">
                        {campos.map((c) => (
                          <div
                            key={c}
                            className={
                              c === 'descripcionTecnica' || c === 'municipioId'
                                ? 'inv-campo-ancho'
                                : undefined
                            }
                          >
                            {renderCampo(c, categoria)}
                            {renderAyuda(c)}
                          </div>
                        ))}
                      </div>
                    </div>
                  )
                })}

              {creando && (
                <FotosNuevas
                  fotos={fotosNuevas}
                  onCambio={cambiarFotosNuevas}
                  erroresServidor={erroresFotos}
                  deshabilitado={enviando}
                />
              )}

              {!creando && categoria === null && detalle && (
                <Aviso
                  aviso={{
                    tipo: 'error',
                    texto: `La categoría "${detalle.tipo}" no tiene reglas definidas; no se puede editar desde aquí.`,
                  }}
                />
              )}
            </fieldset>

            <div ref={refAviso} className="inv-aviso-form" tabIndex={-1}>
              <Aviso aviso={aviso} />
            </div>

            {editable && !creando && faltanFotos && (
              <Aviso aviso={{ tipo: 'advertencia', texto: MENSAJE_EDITAR_SIN_FOTOS }} />
            )}

            {editable && (
              <div className="inv-botones">
                <button
                  type="submit"
                  disabled={enviando || categoria === null || faltanFotos}
                  aria-describedby={faltanFotos ? 'inv-ayuda-guardar' : undefined}
                >
                  {enviando
                    ? creando
                      ? 'Registrando...'
                      : 'Guardando...'
                    : creando
                      ? 'Registrar vehículo'
                      : 'Guardar cambios'}
                </button>
                <a className="inv-boton-enlace inv-boton-secundario" href={HASH_INVENTARIO}>
                  Cancelar
                </a>
              </div>
            )}
            {editable && faltanFotos && (
              <p id="inv-ayuda-guardar" className="campo-ayuda inv-ayuda-guardar">
                {creando
                  ? 'Agrega al menos una foto para poder guardar.'
                  : 'Sube una foto en la sección "Fotos" de abajo para poder guardar los cambios.'}
              </p>
            )}
          </form>

          {detalle && (
            <FotosVehiculo
              token={token}
              vehiculoId={detalle.id}
              fotos={detalle.fotos}
              bloqueado={!editable}
              onCambio={actualizarFotos}
              onSesionTerminada={onSesionTerminada}
            />
          )}

          {detalle && (
            <div className="inv-listo">
              <p className="campo-ayuda">Los datos y las fotos ya están guardados.</p>
              <a
                className="inv-boton-enlace inv-boton-listo"
                href={HASH_INVENTARIO}
                onClick={() => {
                  marcarVehiculoReciente(detalle.id)
                  dejarAvisoPendiente({ tipo: 'exito', texto: 'Listo. El vehículo quedó guardado; lo verás resaltado en la lista.' })
                }}
              >
                {textoListo(esAdmin)}
              </a>
            </div>
          )}
        </>
      )}
    </>
  )
}
