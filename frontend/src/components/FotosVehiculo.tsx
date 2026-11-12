// Fotos del vehículo en el inventario (HU-014, RN-VE-001, D-20): hasta 10, JPG/PNG/WebP
// de hasta 5 MB, una por petición. La API valida de nuevo por la firma de los bytes.
// D-30: debe quedar al menos una; "Eliminar" de la única foto está desactivado.
import { useEffect, useRef, useState, type ChangeEvent } from 'react'
import {
  ACEPTA_FOTOS,
  eliminarFoto,
  MAX_FOTOS,
  MENSAJE_FOTOS_LLENAS,
  reordenarFotos,
  subirFoto,
  validarArchivoFoto,
  type FotoInventario,
} from '../api/inventario'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { Aviso, type DatosAviso } from './Aviso'
import { DialogoConfirmacion } from './DialogoAccion'
import { ImagenVehiculo } from './ImagenVehiculo'

type FotosVehiculoProps = {
  token: string
  vehiculoId: number
  fotos: FotoInventario[]
  /** VENDIDO: las fotos no se pueden modificar (409 en la API). */
  bloqueado: boolean
  onCambio: (fotos: FotoInventario[]) => void
  onSesionTerminada: () => void
}

type Subida = { actual: number; total: number }

type DialogoEliminar = { foto: FotoInventario; posicion: number; enviando: boolean; error: DatosAviso | null }

/** Renumera tras borrar: orden 1..n y la primera es la portada (como la API). */
function renumerar(fotos: readonly FotoInventario[]): FotoInventario[] {
  return fotos.map((f, i) => ({ ...f, orden: i + 1, esPortada: i === 0 }))
}

function mensajeDe(error: unknown): string {
  return error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO
}

function esPortada(fotos: readonly FotoInventario[], indice: number): boolean {
  const hayMarcada = fotos.some((f) => f.esPortada)
  return hayMarcada ? fotos[indice].esPortada : indice === 0
}

export function FotosVehiculo({
  token,
  vehiculoId,
  fotos,
  bloqueado,
  onCambio,
  onSesionTerminada,
}: FotosVehiculoProps) {
  const [subida, setSubida] = useState<Subida | null>(null)
  const [erroresArchivo, setErroresArchivo] = useState<string[]>([])
  const [aviso, setAviso] = useState<DatosAviso | null>(null)
  const [ocupado, setOcupado] = useState(false)
  const [dialogo, setDialogo] = useState<DialogoEliminar | null>(null)
  const refFotos = useRef(fotos)
  const refTitulo = useRef<HTMLHeadingElement>(null)
  const refInput = useRef<HTMLInputElement>(null)
  const origenDialogo = useRef<HTMLButtonElement | null>(null)

  useEffect(() => {
    refFotos.current = fotos
  }, [fotos])

  const llenas = fotos.length >= MAX_FOTOS
  // D-30: la última foto no se puede eliminar (la API responde 409).
  const unica = fotos.length === 1
  const idUnica = 'inv-fotos-unica'
  const trabajando = subida !== null || ocupado || dialogo !== null
  const idAyuda = 'inv-fotos-ayuda'
  const idErrores = 'inv-fotos-errores'

  function publicar(nuevas: FotoInventario[]) {
    refFotos.current = nuevas
    onCambio(nuevas)
  }

  /** Lleva el foco a un control de la foto tras moverla; si no existe o está deshabilitado, al título. */
  function enfocar(idElemento: string) {
    window.setTimeout(() => {
      const el = document.getElementById(idElemento) as HTMLButtonElement | null
      if (el && !el.disabled) el.focus()
      else refTitulo.current?.focus()
    }, 0)
  }

  async function alElegir(evento: ChangeEvent<HTMLInputElement>) {
    const archivos = Array.from(evento.target.files ?? [])
    // Se limpia para poder volver a elegir el mismo archivo.
    evento.target.value = ''
    if (archivos.length === 0 || trabajando || bloqueado) return
    setAviso(null)

    const errores: string[] = []
    const validos: File[] = []
    for (const archivo of archivos) {
      const error = validarArchivoFoto(archivo)
      if (error) errores.push(`${archivo.name}: ${error}`)
      else validos.push(archivo)
    }
    const disponibles = Math.max(0, MAX_FOTOS - refFotos.current.length)
    if (validos.length > disponibles) {
      const sobrantes = validos.splice(disponibles)
      errores.push(
        disponibles === 0
          ? MENSAJE_FOTOS_LLENAS
          : `Solo caben ${disponibles} ${disponibles === 1 ? 'foto más' : 'fotos más'}; no se subieron: ${sobrantes
              .map((a) => a.name)
              .join(', ')}.`,
      )
    }
    setErroresArchivo(errores)
    if (validos.length === 0) {
      refInput.current?.focus()
      return
    }

    let subidas = 0
    for (let i = 0; i < validos.length; i++) {
      const archivo = validos[i]
      setSubida({ actual: i + 1, total: validos.length })
      try {
        const foto = await subirFoto(token, vehiculoId, archivo)
        subidas++
        publicar([...refFotos.current, foto].sort((a, b) => a.orden - b.orden))
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          setSubida(null)
          onSesionTerminada()
          return
        }
        errores.push(`${archivo.name}: ${mensajeDe(error)}`)
        setErroresArchivo([...errores])
        // Sin cupo o vehículo no modificable: no tiene sentido seguir con las demás.
        if (error instanceof ApiError && (error.status === 409 || error.status === 404)) break
      }
    }
    setSubida(null)
    if (subidas > 0) {
      setAviso({
        tipo: 'exito',
        texto: `${subidas === 1 ? 'Se subió 1 foto.' : `Se subieron ${subidas} fotos.`} Las fotos se guardan solas; cuando termines, pulsa «Listo» al final de la página.`,
      })
    }
    window.setTimeout(() => refInput.current?.focus(), 0)
  }

  async function guardarOrden(nuevoOrden: FotoInventario[], exito: string, idFoco: string) {
    if (trabajando || bloqueado) return
    setAviso(null)
    setOcupado(true)
    try {
      const resultado = await reordenarFotos(
        token,
        vehiculoId,
        nuevoOrden.map((f) => f.id),
      )
      publicar(resultado.length > 0 ? resultado : renumerar(nuevoOrden))
      setAviso({ tipo: 'exito', texto: exito })
      enfocar(idFoco)
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        onSesionTerminada()
        return
      }
      setAviso({ tipo: 'error', texto: mensajeDe(error) })
    } finally {
      setOcupado(false)
    }
  }

  function mover(indice: number, delta: -1 | 1) {
    const destino = indice + delta
    if (destino < 0 || destino >= fotos.length) return
    const copia = [...fotos]
    const [foto] = copia.splice(indice, 1)
    copia.splice(destino, 0, foto)
    const texto =
      destino === 0
        ? 'Foto movida a la posición 1; ahora es la portada.'
        : `Foto movida a la posición ${destino + 1}.`
    void guardarOrden(copia, texto, `foto-${foto.id}-${delta === -1 ? 'subir' : 'bajar'}`)
  }

  function hacerPortada(indice: number) {
    const copia = [...fotos]
    const [foto] = copia.splice(indice, 1)
    copia.unshift(foto)
    void guardarOrden(copia, 'La foto ahora es la portada.', `foto-${foto.id}-bajar`)
  }

  function abrirEliminar(foto: FotoInventario, posicion: number, boton: HTMLButtonElement) {
    if (refFotos.current.length <= 1) return
    origenDialogo.current = boton
    setAviso(null)
    setDialogo({ foto, posicion, enviando: false, error: null })
  }

  function cancelarEliminar() {
    setDialogo(null)
    const boton = origenDialogo.current
    origenDialogo.current = null
    window.setTimeout(() => boton?.focus(), 0)
  }

  async function confirmarEliminar() {
    if (!dialogo || dialogo.enviando) return
    const { foto } = dialogo
    setDialogo({ ...dialogo, enviando: true, error: null })
    try {
      await eliminarFoto(token, vehiculoId, foto.id)
      publicar(renumerar(refFotos.current.filter((f) => f.id !== foto.id)))
      setDialogo(null)
      origenDialogo.current = null
      setAviso({ tipo: 'exito', texto: 'Foto eliminada.' })
      window.setTimeout(() => refTitulo.current?.focus(), 0)
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setDialogo(null)
        onSesionTerminada()
        return
      }
      if (error instanceof ApiError && error.status === 404) {
        // Ya no existía: se quita de la lista igualmente.
        publicar(renumerar(refFotos.current.filter((f) => f.id !== foto.id)))
        setDialogo(null)
        origenDialogo.current = null
        setAviso({ tipo: 'error', texto: mensajeDe(error) })
        window.setTimeout(() => refTitulo.current?.focus(), 0)
        return
      }
      if (error instanceof ApiError && error.status === 409) {
        setDialogo(null)
        origenDialogo.current = null
        setAviso({ tipo: 'error', texto: mensajeDe(error) })
        window.setTimeout(() => refTitulo.current?.focus(), 0)
        return
      }
      setDialogo((d) => d && { ...d, enviando: false, error: { tipo: 'error', texto: mensajeDe(error) } })
    }
  }

  return (
    <section className="inv-fotos" aria-labelledby="inv-fotos-titulo">
      <h2 id="inv-fotos-titulo" ref={refTitulo} tabIndex={-1}>
        Fotos <span className="admin-total">({fotos.length} de {MAX_FOTOS})</span>
      </h2>
      <p className="campo-ayuda">
        La primera foto es la portada. Usa "Subir" y "Bajar" para cambiar el orden.
      </p>

      {!bloqueado && (
        <div className="campo">
          <label htmlFor="inv-fotos-archivo">Agregar fotos</label>
          <input
            ref={refInput}
            id="inv-fotos-archivo"
            name="archivo"
            type="file"
            accept={ACEPTA_FOTOS}
            multiple
            onChange={(e) => void alElegir(e)}
            disabled={llenas || trabajando}
            aria-describedby={[idAyuda, erroresArchivo.length > 0 ? idErrores : undefined]
              .filter(Boolean)
              .join(' ')}
            aria-invalid={erroresArchivo.length > 0}
          />
          <p id={idAyuda} className="campo-ayuda">
            {llenas
              ? MENSAJE_FOTOS_LLENAS
              : `JPG, PNG o WebP de hasta 5 MB cada una. Puedes agregar ${MAX_FOTOS - fotos.length} más.`}
          </p>
          {erroresArchivo.length > 0 && (
            <ul id={idErrores} className="campo-errores" role="alert">
              {erroresArchivo.map((m) => (
                <li key={m}>{m}</li>
              ))}
            </ul>
          )}
        </div>
      )}

      {subida && (
        <div className="inv-progreso">
          <label htmlFor="inv-fotos-progreso">Progreso de la subida</label>
          <progress id="inv-fotos-progreso" max={subida.total} value={subida.actual - 1} />
          <p role="status">
            Subiendo foto {subida.actual} de {subida.total}...
          </p>
        </div>
      )}
      {ocupado && <p role="status">Guardando el orden...</p>}
      <Aviso aviso={aviso} />

      {fotos.length === 0 ? (
        <p className="admin-vacio">
          Este vehículo aún no tiene fotos.{!bloqueado && ' Agrega al menos una para poder guardar cambios.'}
        </p>
      ) : (
        <ol className="inv-fotos-lista">
          {fotos.map((foto, i) => {
            const n = i + 1
            const portada = esPortada(fotos, i)
            const nombre = `foto ${n}`
            return (
              <li key={foto.id} className="inv-foto">
                <figure>
                  <ImagenVehiculo
                    src={foto.url}
                    alt={`Foto ${n} de ${fotos.length}${portada ? ' (portada)' : ''}`}
                  />
                  <figcaption>
                    Foto {n}
                    {portada && <span className="cat-etiqueta inv-etiqueta-portada">Portada</span>}
                  </figcaption>
                </figure>
                {!bloqueado && (
                  <div className="acciones-fila inv-foto-acciones">
                    <button
                      id={`foto-${foto.id}-subir`}
                      type="button"
                      className="secondary-button"
                      disabled={trabajando || i === 0}
                      onClick={() => mover(i, -1)}
                    >
                      Subir{' '}<span className="sr-only">{nombre}</span>
                    </button>
                    <button
                      id={`foto-${foto.id}-bajar`}
                      type="button"
                      className="secondary-button"
                      disabled={trabajando || i === fotos.length - 1}
                      onClick={() => mover(i, 1)}
                    >
                      Bajar{' '}<span className="sr-only">{nombre}</span>
                    </button>
                    {!portada && (
                      <button
                        id={`foto-${foto.id}-portada`}
                        type="button"
                        className="secondary-button"
                        disabled={trabajando}
                        onClick={() => hacerPortada(i)}
                      >
                        Hacer portada{' '}<span className="sr-only">la {nombre}</span>
                      </button>
                    )}
                    <button
                      id={`foto-${foto.id}-eliminar`}
                      type="button"
                      className="boton-peligro"
                      disabled={trabajando || unica}
                      aria-describedby={unica ? idUnica : undefined}
                      onClick={(e) => abrirEliminar(foto, n, e.currentTarget)}
                    >
                      Eliminar{' '}<span className="sr-only">{nombre}</span>
                    </button>
                  </div>
                )}
                {!bloqueado && unica && (
                  <p id={idUnica} className="campo-ayuda">
                    Debe quedar al menos una foto. Sube otra antes de eliminar esta.
                  </p>
                )}
              </li>
            )
          })}
        </ol>
      )}

      {dialogo && (
        <DialogoConfirmacion
          titulo="Eliminar foto"
          descripcion={`¿Eliminar la foto ${dialogo.posicion}? Esta acción no se puede deshacer.${
            esPortada(fotos, dialogo.posicion - 1) && fotos.length > 1
              ? ' La siguiente foto pasará a ser la portada.'
              : ''
          }`}
          textoBoton="Eliminar"
          textoEnviando="Eliminando..."
          peligro
          motivo="no"
          enviando={dialogo.enviando}
          error={dialogo.error}
          onConfirmar={() => void confirmarEliminar()}
          onCancelar={cancelarEliminar}
        />
      )}
    </section>
  )
}
