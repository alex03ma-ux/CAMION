// P4p — Catálogos administrables (HU-082 marcas, HU-083 modelos, HU-084 tipos de carrocería; D-32, D-37).
// El administrador crea, renombra y activa o desactiva; nada se borra. Un elemento desactivado deja de
// ofrecerse en los formularios y filtros, pero los vehículos que ya lo usan lo conservan.
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import {
  cambiarEstadoElemento,
  cambiarEstadoLinea,
  CATALOGOS_ADMIN,
  crearElemento,
  crearLinea,
  datosCatalogo,
  editarLinea,
  listarCatalogo,
  NOMBRE_CATALOGO_MAX,
  normalizarBusqueda,
  renombrarElemento,
  validarCategorias,
  validarNombreCatalogo,
  type AccionEstadoCatalogo,
  type CatalogoAdmin,
  type ElementoCatalogo,
  type LineaAdmin,
} from '../api/catalogosAdmin'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { obtenerCatalogosInventario, type Opcion } from '../api/inventario'
import { formatoMiles } from '../catalogo/formato'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo, CampoSelect } from './Campo'
import { DialogoConfirmacion } from './DialogoAccion'
import { NotaObligatorio } from './NotaObligatorio'

type AdminCatalogosProps = {
  token: string
  onSesionTerminada: () => void
}

function erroresDe(error: ApiError, campo: string): string[] {
  return Object.entries(error.errores)
    .filter(([c]) => c.toLowerCase() === campo.toLowerCase())
    .flatMap(([, m]) => m)
}

function textoVehiculos(n: number): string {
  return n === 1 ? '1 vehículo' : `${formatoMiles(n)} vehículos`
}

/** "El vehículo que lo usa…" / "Los 3 vehículos que lo usan…" / "Ningún vehículo lo usa." */
function frasePorVehiculos(n: number, singular: string, plural: string, ninguno: string): string {
  if (n === 0) return ninguno
  return n === 1 ? `El vehículo que lo usa ${singular}` : `Los ${formatoMiles(n)} vehículos que lo usan ${plural}`
}

type CasillasProps = {
  id: string
  tipos: readonly Opcion[]
  seleccion: readonly number[]
  onCambio: (tipoIds: number[]) => void
  errores: string[]
  deshabilitado: boolean
}

/** D-32: las 4 categorías del modelo; al menos una es obligatoria. */
function CasillasCategorias({ id, tipos, seleccion, onCambio, errores, deshabilitado }: CasillasProps) {
  const idError = `${id}-error`
  return (
    <fieldset
      className="casillas-categorias campo-obligatorio"
      aria-describedby={errores.length > 0 ? idError : undefined}
      aria-required="true"
      disabled={deshabilitado}
    >
      <legend>Categorías</legend>
      {tipos.length === 0 && <p className="campo-ayuda">No se pudieron cargar las categorías.</p>}
      <div className="casillas-lista">
        {tipos.map((t) => {
          const valor = Number(t.id)
          const marcada = seleccion.includes(valor)
          return (
            <label key={t.id} className="opcion-casilla" htmlFor={`${id}-${t.id}`}>
              <input
                id={`${id}-${t.id}`}
                type="checkbox"
                checked={marcada}
                aria-invalid={errores.length > 0}
                onChange={(e) =>
                  onCambio(
                    e.target.checked
                      ? [...seleccion, valor].sort((a, b) => a - b)
                      : seleccion.filter((v) => v !== valor),
                  )
                }
              />
              <span>{t.nombre}</span>
            </label>
          )
        })}
      </div>
      {errores.length > 0 && (
        <ul id={idError} className="campo-errores">
          {errores.map((m) => (
            <li key={m}>{m}</li>
          ))}
        </ul>
      )}
    </fieldset>
  )
}

type Objetivo =
  | { clase: 'elemento'; elemento: ElementoCatalogo }
  | { clase: 'linea'; linea: LineaAdmin; marca: ElementoCatalogo }

type Dialogo = {
  tipo: 'renombrar' | 'estado' | 'editarLinea'
  objetivo: Objetivo
  nombre: string
  tipoIds: number[]
  erroresNombre: string[]
  erroresCategorias: string[]
  enviando: boolean
  error: DatosAviso | null
}

function datosObjetivo(o: Objetivo): { id: number; nombre: string; activo: boolean; vehiculos: number } {
  return o.clase === 'elemento' ? o.elemento : o.linea
}

export function AdminCatalogos({ token, onSesionTerminada }: AdminCatalogosProps) {
  const [catalogo, setCatalogo] = useState<CatalogoAdmin>('marcas')
  const [elementos, setElementos] = useState<ElementoCatalogo[] | null>(null)
  const [cargando, setCargando] = useState(true)
  const [errorCarga, setErrorCarga] = useState<DatosAviso | null>(null)
  const [recarga, setRecarga] = useState(0)
  const [resultado, setResultado] = useState<DatosAviso | null>(null)
  const [buscar, setBuscar] = useState('')
  const [tipos, setTipos] = useState<Opcion[]>([])

  const [nuevo, setNuevo] = useState('')
  const [erroresNuevo, setErroresNuevo] = useState<string[]>([])
  const [creando, setCreando] = useState(false)

  const [marcaAbiertaId, setMarcaAbiertaId] = useState<number | null>(null)
  const [nuevoModelo, setNuevoModelo] = useState({ nombre: '', tipoIds: [] as number[] })
  const [erroresModelo, setErroresModelo] = useState<{ nombre: string[]; tipoIds: string[] }>({
    nombre: [],
    tipoIds: [],
  })
  const [creandoModelo, setCreandoModelo] = useState(false)
  const [avisoModelos, setAvisoModelos] = useState<DatosAviso | null>(null)

  const [dialogo, setDialogo] = useState<Dialogo | null>(null)

  const titulo = useRef<HTMLHeadingElement>(null)
  const tituloLista = useRef<HTMLHeadingElement>(null)
  const tituloModelos = useRef<HTMLHeadingElement>(null)
  const origenDialogo = useRef<HTMLButtonElement | null>(null)

  const info = datosCatalogo(catalogo)
  const esMarcas = catalogo === 'marcas'

  useEffect(() => {
    titulo.current?.focus()
  }, [])

  const mensajeDe = useCallback(
    (error: unknown): string | null => {
      if (error instanceof ApiError && error.status === 401) {
        onSesionTerminada()
        return null
      }
      return error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO
    },
    [onSesionTerminada],
  )

  // Las 4 categorías (tipos de vehículo) para las casillas de los modelos.
  useEffect(() => {
    const control = new AbortController()
    obtenerCatalogosInventario(token, control.signal)
      .then((c) => setTipos(c.tipos))
      .catch(() => {
        // Sin categorías se avisa junto a las casillas; el resto de la pantalla sigue.
      })
    return () => control.abort()
  }, [token])

  useEffect(() => {
    const control = new AbortController()
    setCargando(true)
    setErrorCarga(null)
    listarCatalogo(token, catalogo, control.signal)
      .then((lista) => {
        setElementos(lista)
        setCargando(false)
      })
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        setCargando(false)
        const texto = mensajeDe(error)
        if (texto) {
          setElementos(null)
          setErrorCarga({ tipo: 'error', texto })
        }
      })
    return () => control.abort()
  }, [token, catalogo, recarga, mensajeDe])

  function cambiarCatalogo(valor: string) {
    const elegido = CATALOGOS_ADMIN.find((c) => c.valor === valor)
    if (!elegido) return
    setCatalogo(elegido.valor)
    setElementos(null)
    setBuscar('')
    setNuevo('')
    setErroresNuevo([])
    setResultado(null)
    setMarcaAbiertaId(null)
    setAvisoModelos(null)
  }

  async function crear(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (creando) return
    setResultado(null)
    const validacion = validarNombreCatalogo(nuevo)
    setErroresNuevo(validacion)
    if (validacion.length > 0) {
      document.getElementById('catalogo-nuevo')?.focus()
      return
    }
    setCreando(true)
    try {
      await crearElemento(token, catalogo, nuevo)
      setCreando(false)
      setResultado({ tipo: 'exito', texto: `Se agregó "${nuevo.trim()}" a ${info.nombre}.` })
      setNuevo('')
      setRecarga((r) => r + 1)
    } catch (error) {
      setCreando(false)
      const texto = mensajeDe(error)
      if (texto === null) return
      if (error instanceof ApiError && (error.status === 400 || error.status === 409)) {
        const delNombre = erroresDe(error, 'nombre')
        setErroresNuevo(delNombre.length > 0 ? delNombre : [texto])
        document.getElementById('catalogo-nuevo')?.focus()
        return
      }
      setResultado({ tipo: 'error', texto })
    }
  }

  async function crearModelo(evento: FormEvent<HTMLFormElement>, marca: ElementoCatalogo) {
    evento.preventDefault()
    if (creandoModelo) return
    setAvisoModelos(null)
    const errores = {
      nombre: validarNombreCatalogo(nuevoModelo.nombre),
      tipoIds: validarCategorias(nuevoModelo.tipoIds),
    }
    setErroresModelo(errores)
    if (errores.nombre.length > 0) {
      document.getElementById('modelo-nuevo')?.focus()
      return
    }
    if (errores.tipoIds.length > 0) {
      document.querySelector<HTMLInputElement>('#modelo-nuevo-categorias input')?.focus()
      return
    }
    setCreandoModelo(true)
    try {
      await crearLinea(token, marca.id, nuevoModelo.nombre, nuevoModelo.tipoIds)
      setCreandoModelo(false)
      setAvisoModelos({
        tipo: 'exito',
        texto: `Se agregó el modelo "${nuevoModelo.nombre.trim()}" a ${marca.nombre}.`,
      })
      setNuevoModelo({ nombre: '', tipoIds: [] })
      setRecarga((r) => r + 1)
    } catch (error) {
      setCreandoModelo(false)
      const texto = mensajeDe(error)
      if (texto === null) return
      if (error instanceof ApiError && (error.status === 400 || error.status === 409)) {
        const nombre = erroresDe(error, 'nombre')
        const tipoIds = erroresDe(error, 'tipoIds')
        setErroresModelo({
          nombre: nombre.length > 0 || tipoIds.length > 0 ? nombre : [texto],
          tipoIds,
        })
        return
      }
      setAvisoModelos({ tipo: 'error', texto })
    }
  }

  function abrirDialogo(tipo: Dialogo['tipo'], objetivo: Objetivo, boton: HTMLButtonElement) {
    origenDialogo.current = boton
    setResultado(null)
    setAvisoModelos(null)
    const datos = datosObjetivo(objetivo)
    setDialogo({
      tipo,
      objetivo,
      nombre: datos.nombre,
      tipoIds: objetivo.clase === 'linea' ? [...objetivo.linea.tipoIds] : [],
      erroresNombre: [],
      erroresCategorias: [],
      enviando: false,
      error: null,
    })
  }

  function cancelarDialogo() {
    setDialogo(null)
    const boton = origenDialogo.current
    origenDialogo.current = null
    window.setTimeout(() => boton?.focus(), 0)
  }

  function terminarDialogo(aviso: DatosAviso, enModelos: boolean, extra: DatosAviso | null = null) {
    setDialogo(null)
    origenDialogo.current = null
    if (enModelos) {
      setAvisoModelos(extra ? { tipo: 'advertencia', texto: `${aviso.texto} ${extra.texto}` } : aviso)
    } else setResultado(aviso)
    setRecarga((r) => r + 1)
    window.setTimeout(() => (enModelos ? tituloModelos : tituloLista).current?.focus(), 0)
  }

  async function confirmarDialogo() {
    if (!dialogo || dialogo.enviando) return
    const d = dialogo
    const datos = datosObjetivo(d.objetivo)
    const enModelos = d.objetivo.clase === 'linea'

    if (d.tipo === 'renombrar' || d.tipo === 'editarLinea') {
      const erroresNombre = validarNombreCatalogo(d.nombre)
      const erroresCategorias = d.tipo === 'editarLinea' ? validarCategorias(d.tipoIds) : []
      if (erroresNombre.length > 0 || erroresCategorias.length > 0) {
        setDialogo({ ...d, erroresNombre, erroresCategorias })
        if (erroresNombre.length > 0) document.getElementById('dialogo-nombre')?.focus()
        else document.querySelector<HTMLInputElement>('#dialogo-categorias input')?.focus()
        return
      }
    }

    setDialogo({ ...d, enviando: true, erroresNombre: [], erroresCategorias: [], error: null })
    try {
      if (d.tipo === 'estado') {
        const accion: AccionEstadoCatalogo = datos.activo ? 'desactivar' : 'activar'
        if (d.objetivo.clase === 'elemento') await cambiarEstadoElemento(token, catalogo, datos.id, accion)
        else await cambiarEstadoLinea(token, datos.id, accion)
        terminarDialogo(
          {
            tipo: 'exito',
            texto:
              accion === 'activar'
                ? `Se activó "${datos.nombre}".`
                : `Se desactivó "${datos.nombre}". Los vehículos que ya lo usan lo conservan.`,
          },
          enModelos,
        )
      } else if (d.tipo === 'renombrar') {
        await renombrarElemento(token, catalogo, datos.id, d.nombre)
        terminarDialogo({ tipo: 'exito', texto: `Se renombró "${datos.nombre}" a "${d.nombre.trim()}".` }, false)
      } else {
        const original = d.objetivo.clase === 'linea' ? d.objetivo.linea : null
        const cambios: { nombre?: string; tipoIds?: number[] } = {}
        if (d.nombre.trim() !== datos.nombre) cambios.nombre = d.nombre
        const mismasCategorias =
          original !== null &&
          original.tipoIds.length === d.tipoIds.length &&
          [...original.tipoIds]
            .sort((a, b) => a - b)
            .every((v, i) => v === [...d.tipoIds].sort((a, b) => a - b)[i])
        if (!mismasCategorias) cambios.tipoIds = d.tipoIds
        if (cambios.nombre === undefined && cambios.tipoIds === undefined) {
          cancelarDialogo()
          return
        }
        const { aviso } = await editarLinea(token, datos.id, cambios)
        terminarDialogo(
          { tipo: 'exito', texto: `Se guardaron los cambios del modelo "${d.nombre.trim()}".` },
          true,
          aviso ? { tipo: 'advertencia', texto: aviso } : null,
        )
      }
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setDialogo(null)
        onSesionTerminada()
        return
      }
      const texto = error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO
      if (error instanceof ApiError && (error.status === 400 || error.status === 409) && d.tipo !== 'estado') {
        const nombre = erroresDe(error, 'nombre')
        const categorias = erroresDe(error, 'tipoIds')
        setDialogo(
          (x) =>
            x && {
              ...x,
              enviando: false,
              erroresNombre: nombre.length > 0 || categorias.length > 0 ? nombre : [texto],
              erroresCategorias: categorias,
            },
        )
        return
      }
      if (error instanceof ApiError && [403, 404, 409].includes(error.status)) {
        terminarDialogo({ tipo: 'error', texto }, enModelos)
        return
      }
      setDialogo((x) => x && { ...x, enviando: false, error: { tipo: 'error', texto } })
    }
  }

  const termino = normalizarBusqueda(buscar)
  const visibles = (elementos ?? []).filter(
    (e) =>
      termino === '' ||
      normalizarBusqueda(e.nombre).includes(termino) ||
      (esMarcas && e.lineas.some((l) => normalizarBusqueda(l.nombre).includes(termino))),
  )
  const marcaAbierta = esMarcas ? (elementos ?? []).find((e) => e.id === marcaAbiertaId) ?? null : null

  function nombreTipo(id: number): string {
    return tipos.find((t) => Number(t.id) === id)?.nombre ?? `Categoría ${id}`
  }

  function contenidoDialogo(d: Dialogo) {
    if (d.tipo === 'estado') return undefined
    return (
      <>
        <NotaObligatorio />
        <Campo
          id="dialogo-nombre"
          etiqueta={d.tipo === 'editarLinea' ? 'Nombre del modelo' : 'Nombre nuevo'}
          valor={d.nombre}
          onCambio={(v) => setDialogo((x) => x && { ...x, nombre: v, erroresNombre: [] })}
          maxLength={NOMBRE_CATALOGO_MAX}
          errores={d.erroresNombre}
          autoComplete="off"
          deshabilitado={d.enviando}
        />
        {d.tipo === 'editarLinea' && (
          <div id="dialogo-categorias">
            <CasillasCategorias
              id="dialogo-cat"
              tipos={tipos}
              seleccion={d.tipoIds}
              onCambio={(tipoIds) => setDialogo((x) => x && { ...x, tipoIds, erroresCategorias: [] })}
              errores={d.erroresCategorias}
              deshabilitado={d.enviando}
            />
            <p className="campo-ayuda">
              Si quitas una categoría que usan vehículos, se permite: se corregirán al editarlos.
            </p>
          </div>
        )}
      </>
    )
  }

  function textosDialogo(d: Dialogo) {
    const datos = datosObjetivo(d.objetivo)
    const queEs = d.objetivo.clase === 'linea' ? 'el modelo' : info.singular === 'marca' ? 'la marca' : `el ${info.singular}`
    if (d.tipo === 'renombrar') {
      return {
        titulo: `Renombrar ${queEs}`,
        descripcion: `Escribe el nombre nuevo para "${datos.nombre}". ${frasePorVehiculos(datos.vehiculos, 'mostrará el nombre nuevo.', 'mostrarán el nombre nuevo.', 'Ningún vehículo lo usa todavía.')}`,
        boton: 'Guardar nombre',
        enviando: 'Guardando...',
        peligro: false,
      }
    }
    if (d.tipo === 'editarLinea') {
      return {
        titulo: 'Editar modelo',
        descripcion: `Cambia el nombre o las categorías de "${datos.nombre}" (lo usan ${textoVehiculos(datos.vehiculos)}).`,
        boton: 'Guardar cambios',
        enviando: 'Guardando...',
        peligro: false,
      }
    }
    return datos.activo
      ? {
          titulo: `Desactivar ${queEs}`,
          descripcion: `¿Desactivar "${datos.nombre}"? Ya no se ofrecerá en los formularios ni en los filtros del portal. ${frasePorVehiculos(datos.vehiculos, 'lo conserva.', 'lo conservan.', 'Ningún vehículo lo usa.')}`,
          boton: 'Desactivar',
          enviando: 'Desactivando...',
          peligro: true,
        }
      : {
          titulo: `Activar ${queEs}`,
          descripcion: `¿Activar "${datos.nombre}"? Se volverá a ofrecer en los formularios y en los filtros del portal.`,
          boton: 'Activar',
          enviando: 'Activando...',
          peligro: false,
        }
  }

  return (
    <section className="panel-ancho" aria-labelledby="catalogos-titulo">
      <h2 id="catalogos-titulo" ref={titulo} tabIndex={-1}>
        Catálogos
      </h2>
      <p className="form-ayuda">
        Administra las listas que se usan al registrar vehículos y en los filtros del portal. Nada se
        borra: lo desactivado deja de ofrecerse, pero los vehículos que ya lo usan lo conservan.
      </p>

      <div className="filtros admin-selector">
        <CampoSelect
          id="catalogo-selector"
          etiqueta="Catálogo"
          valor={catalogo}
          onCambio={cambiarCatalogo}
          opciones={CATALOGOS_ADMIN.map((c) => ({ valor: c.valor, nombre: c.nombre }))}
          requerido={false}
        />
        <Campo
          id="catalogo-buscar"
          etiqueta={`Buscar en ${info.nombre.toLowerCase()}`}
          tipo="search"
          valor={buscar}
          onCambio={setBuscar}
          maxLength={NOMBRE_CATALOGO_MAX}
          autoComplete="off"
          requerido={false}
        />
      </div>

      <form className="admin-form" onSubmit={(e) => void crear(e)} noValidate aria-busy={creando}>
        <h3 className="admin-subtitulo">Agregar a {info.nombre.toLowerCase()}</h3>
        <NotaObligatorio />
        <div className="admin-linea">
          <Campo
            id="catalogo-nuevo"
            etiqueta="Nombre"
            valor={nuevo}
            onCambio={(v) => {
              setNuevo(v)
              setErroresNuevo([])
            }}
            maxLength={NOMBRE_CATALOGO_MAX}
            errores={erroresNuevo}
            autoComplete="off"
            deshabilitado={creando}
          />
          <button type="submit" disabled={creando}>
            {creando ? 'Creando...' : 'Crear'}
          </button>
        </div>
      </form>

      <h3 ref={tituloLista} tabIndex={-1} className="admin-subtitulo">
        {info.nombre}
        {elementos && !cargando && (
          <span className="admin-total">
            {' '}
            ({visibles.length} de {elementos.length})
          </span>
        )}
      </h3>
      <Aviso aviso={resultado} />
      <Aviso aviso={errorCarga} />
      {cargando && <p role="status">Cargando {info.nombre.toLowerCase()}...</p>}
      {elementos && !cargando && visibles.length === 0 && (
        <p className="admin-vacio">
          {elementos.length === 0 ? 'Este catálogo está vacío.' : 'Ningún elemento coincide con la búsqueda.'}
        </p>
      )}

      {elementos && visibles.length > 0 && (
        <div className="tabla-contenedor" role="region" aria-labelledby="catalogo-tabla" tabIndex={0}>
          <table className="tabla-usuarios tabla-admin">
            <caption id="catalogo-tabla" className="sr-only">
              {info.nombre}
            </caption>
            <thead>
              <tr>
                <th scope="col">Nombre</th>
                <th scope="col">Estado</th>
                <th scope="col">Vehículos que lo usan</th>
                {esMarcas && <th scope="col">Modelos</th>}
                <th scope="col">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {visibles.map((e) => (
                <tr key={e.id}>
                  <th scope="row">{e.nombre}</th>
                  <td>
                    <span className={`estado ${e.activo ? 'estado-activa' : 'estado-bloqueada'}`}>
                      {e.activo ? 'Activo' : 'Inactivo'}
                    </span>
                  </td>
                  <td>{formatoMiles(e.vehiculos)}</td>
                  {esMarcas && <td>{formatoMiles(e.lineas.length)}</td>}
                  <td>
                    <div className="acciones-fila">
                      {esMarcas && (
                        <button
                          type="button"
                          className="secondary-button"
                          aria-expanded={marcaAbiertaId === e.id}
                          aria-controls="panel-modelos"
                          disabled={dialogo !== null}
                          onClick={() => {
                            const abrir = marcaAbiertaId !== e.id
                            setMarcaAbiertaId(abrir ? e.id : null)
                            setNuevoModelo({ nombre: '', tipoIds: [] })
                            setErroresModelo({ nombre: [], tipoIds: [] })
                            setAvisoModelos(null)
                            if (abrir) window.setTimeout(() => tituloModelos.current?.focus(), 0)
                          }}
                        >
                          {marcaAbiertaId === e.id ? 'Ocultar modelos' : 'Ver modelos'}{' '}
                          <span className="sr-only">de {e.nombre}</span>
                        </button>
                      )}
                      <button
                        type="button"
                        className="secondary-button"
                        disabled={dialogo !== null}
                        onClick={(ev) => abrirDialogo('renombrar', { clase: 'elemento', elemento: e }, ev.currentTarget)}
                      >
                        Renombrar <span className="sr-only">{e.nombre}</span>
                      </button>
                      <button
                        type="button"
                        className={e.activo ? 'boton-peligro' : undefined}
                        disabled={dialogo !== null}
                        onClick={(ev) => abrirDialogo('estado', { clase: 'elemento', elemento: e }, ev.currentTarget)}
                      >
                        {e.activo ? 'Desactivar' : 'Activar'} <span className="sr-only">{e.nombre}</span>
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {marcaAbierta && (
        <div id="panel-modelos" className="admin-modelos" role="region" aria-labelledby="modelos-titulo">
          <h3 id="modelos-titulo" ref={tituloModelos} tabIndex={-1} className="admin-subtitulo">
            Modelos de {marcaAbierta.nombre}
          </h3>
          <form className="admin-form" onSubmit={(ev) => void crearModelo(ev, marcaAbierta)} noValidate aria-busy={creandoModelo}>
            <h4 className="admin-subtitulo-menor">Agregar modelo</h4>
            <NotaObligatorio />
            <Campo
              id="modelo-nuevo"
              etiqueta="Nombre del modelo"
              valor={nuevoModelo.nombre}
              onCambio={(v) => {
                setNuevoModelo((m) => ({ ...m, nombre: v }))
                setErroresModelo((x) => ({ ...x, nombre: [] }))
              }}
              maxLength={NOMBRE_CATALOGO_MAX}
              errores={erroresModelo.nombre}
              autoComplete="off"
              deshabilitado={creandoModelo}
            />
            <div id="modelo-nuevo-categorias">
              <CasillasCategorias
                id="modelo-cat"
                tipos={tipos}
                seleccion={nuevoModelo.tipoIds}
                onCambio={(tipoIds) => {
                  setNuevoModelo((m) => ({ ...m, tipoIds }))
                  setErroresModelo((x) => ({ ...x, tipoIds: [] }))
                }}
                errores={erroresModelo.tipoIds}
                deshabilitado={creandoModelo}
              />
            </div>
            <div className="admin-botones">
              <button type="submit" disabled={creandoModelo}>
                {creandoModelo ? 'Creando...' : 'Crear modelo'}
              </button>
            </div>
          </form>

          <Aviso aviso={avisoModelos} />
          {marcaAbierta.lineas.length === 0 ? (
            <p className="admin-vacio">Esta marca aún no tiene modelos.</p>
          ) : (
            <div className="tabla-contenedor" role="region" aria-labelledby="modelos-tabla" tabIndex={0}>
              <table className="tabla-usuarios tabla-admin">
                <caption id="modelos-tabla" className="sr-only">
                  Lista de modelos de {marcaAbierta.nombre}
                </caption>
                <thead>
                  <tr>
                    <th scope="col">Modelo</th>
                    <th scope="col">Categorías</th>
                    <th scope="col">Estado</th>
                    <th scope="col">Vehículos que lo usan</th>
                    <th scope="col">Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {marcaAbierta.lineas.map((l) => (
                    <tr key={l.id}>
                      <th scope="row">{l.nombre}</th>
                      <td>{l.tipoIds.length > 0 ? l.tipoIds.map(nombreTipo).join(', ') : 'Sin clasificar'}</td>
                      <td>
                        <span className={`estado ${l.activo ? 'estado-activa' : 'estado-bloqueada'}`}>
                          {l.activo ? 'Activo' : 'Inactivo'}
                        </span>
                      </td>
                      <td>{formatoMiles(l.vehiculos)}</td>
                      <td>
                        <div className="acciones-fila">
                          <button
                            type="button"
                            className="secondary-button"
                            disabled={dialogo !== null}
                            onClick={(ev) =>
                              abrirDialogo('editarLinea', { clase: 'linea', linea: l, marca: marcaAbierta }, ev.currentTarget)
                            }
                          >
                            Editar <span className="sr-only">el modelo {l.nombre}</span>
                          </button>
                          <button
                            type="button"
                            className={l.activo ? 'boton-peligro' : undefined}
                            disabled={dialogo !== null}
                            onClick={(ev) =>
                              abrirDialogo('estado', { clase: 'linea', linea: l, marca: marcaAbierta }, ev.currentTarget)
                            }
                          >
                            {l.activo ? 'Desactivar' : 'Activar'}{' '}
                            <span className="sr-only">el modelo {l.nombre}</span>
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {dialogo &&
        (() => {
          const t = textosDialogo(dialogo)
          return (
            <DialogoConfirmacion
              titulo={t.titulo}
              descripcion={t.descripcion}
              textoBoton={t.boton}
              textoEnviando={t.enviando}
              peligro={t.peligro}
              motivo="no"
              contenido={contenidoDialogo(dialogo)}
              enviando={dialogo.enviando}
              error={dialogo.error}
              onConfirmar={() => void confirmarDialogo()}
              onCancelar={cancelarDialogo}
            />
          )
        })()}
    </section>
  )
}
