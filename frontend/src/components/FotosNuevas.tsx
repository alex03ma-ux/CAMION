// Fotos elegidas al registrar un vehículo (D-30): de 1 a 10, JPG/PNG/WebP de hasta
// 5 MB. No se suben todavía: viajan con los datos en el mismo POST multipart.
// La primera es la portada. La API vuelve a validar cada archivo por su contenido.
import { useEffect, useRef, useState, type ChangeEvent } from 'react'
import {
  ACEPTA_FOTOS,
  MAX_FOTOS,
  MENSAJE_FOTOS_LLENAS,
  validarArchivoFoto,
} from '../api/inventario'

export type FotoElegida = {
  /** Clave local estable (no es un id del servidor). */
  clave: number
  archivo: File
  /** URL de vista previa (`URL.createObjectURL`); null si el navegador no la da. */
  vista: string | null
}

type FotosNuevasProps = {
  fotos: readonly FotoElegida[]
  onCambio: (fotos: FotoElegida[]) => void
  /** Errores de `fotos` devueltos por el servidor al registrar. */
  erroresServidor: readonly string[]
  deshabilitado: boolean
}

export const ID_SECCION_FOTOS_NUEVAS = 'inv-fotos-nuevas'
export const ID_INPUT_FOTOS_NUEVAS = 'inv-fotos-nuevas-archivo'

let siguienteClave = 1

function crearVista(archivo: File): string | null {
  if (typeof URL.createObjectURL !== 'function') return null
  try {
    return URL.createObjectURL(archivo)
  } catch {
    return null
  }
}

export function liberarVista(foto: FotoElegida): void {
  if (foto.vista !== null && typeof URL.revokeObjectURL === 'function') URL.revokeObjectURL(foto.vista)
}

export function FotosNuevas({ fotos, onCambio, erroresServidor, deshabilitado }: FotosNuevasProps) {
  const [erroresArchivo, setErroresArchivo] = useState<string[]>([])
  const [aviso, setAviso] = useState<string | null>(null)
  const refInput = useRef<HTMLInputElement>(null)
  const refTitulo = useRef<HTMLHeadingElement>(null)
  const refFotos = useRef(fotos)

  useEffect(() => {
    refFotos.current = fotos
  }, [fotos])

  // Al desmontar (p. ej. tras registrar y navegar) se liberan las vistas previas.
  useEffect(
    () => () => {
      for (const f of refFotos.current) liberarVista(f)
    },
    [],
  )

  const llenas = fotos.length >= MAX_FOTOS
  const idAyuda = `${ID_SECCION_FOTOS_NUEVAS}-ayuda`
  const idErrores = `${ID_SECCION_FOTOS_NUEVAS}-errores`
  const errores = [...erroresArchivo, ...erroresServidor.filter((m) => !erroresArchivo.includes(m))]

  function publicar(nuevas: FotoElegida[]) {
    refFotos.current = nuevas
    onCambio(nuevas)
  }

  function enfocar(idElemento: string) {
    window.setTimeout(() => {
      const el = document.getElementById(idElemento) as HTMLButtonElement | null
      if (el && !el.disabled) el.focus()
      else refTitulo.current?.focus()
    }, 0)
  }

  function alElegir(evento: ChangeEvent<HTMLInputElement>) {
    const archivos = Array.from(evento.target.files ?? [])
    // Se limpia para poder volver a elegir el mismo archivo.
    evento.target.value = ''
    if (archivos.length === 0 || deshabilitado) return
    setAviso(null)
    const nuevosErrores: string[] = []
    const validos: File[] = []
    for (const archivo of archivos) {
      const error = validarArchivoFoto(archivo)
      if (error) nuevosErrores.push(`${archivo.name}: ${error}`)
      else validos.push(archivo)
    }
    const disponibles = Math.max(0, MAX_FOTOS - refFotos.current.length)
    if (validos.length > disponibles) {
      const sobrantes = validos.splice(disponibles)
      nuevosErrores.push(
        disponibles === 0
          ? MENSAJE_FOTOS_LLENAS
          : `Solo caben ${disponibles} ${disponibles === 1 ? 'foto más' : 'fotos más'}; no se agregaron: ${sobrantes
              .map((a) => a.name)
              .join(', ')}.`,
      )
    }
    setErroresArchivo(nuevosErrores)
    if (validos.length > 0) {
      const agregadas = validos.map((archivo) => ({
        clave: siguienteClave++,
        archivo,
        vista: crearVista(archivo),
      }))
      publicar([...refFotos.current, ...agregadas])
      setAviso(
        validos.length === 1
          ? 'Se agregó 1 foto. Se guardará al registrar el vehículo.'
          : `Se agregaron ${validos.length} fotos. Se guardarán al registrar el vehículo.`,
      )
    }
    window.setTimeout(() => refInput.current?.focus(), 0)
  }

  function mover(indice: number, delta: -1 | 1) {
    const destino = indice + delta
    if (destino < 0 || destino >= fotos.length) return
    const copia = [...fotos]
    const [foto] = copia.splice(indice, 1)
    copia.splice(destino, 0, foto)
    publicar(copia)
    setAviso(
      destino === 0
        ? 'Foto movida a la posición 1; ahora es la portada.'
        : `Foto movida a la posición ${destino + 1}.`,
    )
    enfocar(`foto-nueva-${foto.clave}-${delta === -1 ? 'subir' : 'bajar'}`)
  }

  function hacerPortada(indice: number) {
    const copia = [...fotos]
    const [foto] = copia.splice(indice, 1)
    copia.unshift(foto)
    publicar(copia)
    setAviso('La foto ahora es la portada.')
    enfocar(`foto-nueva-${foto.clave}-bajar`)
  }

  function quitar(indice: number) {
    const foto = fotos[indice]
    liberarVista(foto)
    publicar(fotos.filter((_, i) => i !== indice))
    setAviso(`Foto ${indice + 1} quitada.`)
    window.setTimeout(() => refInput.current?.focus(), 0)
  }

  return (
    <section
      id={ID_SECCION_FOTOS_NUEVAS}
      className="inv-grupo inv-fotos inv-fotos-nuevas"
      aria-labelledby={`${ID_SECCION_FOTOS_NUEVAS}-titulo`}
    >
      <h2 id={`${ID_SECCION_FOTOS_NUEVAS}-titulo`} ref={refTitulo} tabIndex={-1}>
        Fotos (1 a 10){' '}
        <span className="admin-total">
          ({fotos.length} de {MAX_FOTOS})
        </span>
      </h2>
      <p className="campo-ayuda">
        La primera foto es la portada. Usa "Subir", "Bajar" o "Hacer portada" para cambiar el orden.
        Se guardan junto con los datos al pulsar "Registrar vehículo".
      </p>

      <div className="campo">
        <label htmlFor={ID_INPUT_FOTOS_NUEVAS}>Elegir fotos</label>
        <input
          ref={refInput}
          id={ID_INPUT_FOTOS_NUEVAS}
          name="fotos"
          type="file"
          accept={ACEPTA_FOTOS}
          multiple
          onChange={alElegir}
          disabled={llenas || deshabilitado}
          aria-describedby={[idAyuda, errores.length > 0 ? idErrores : undefined].filter(Boolean).join(' ')}
          aria-invalid={errores.length > 0}
          aria-required="true"
        />
        <p id={idAyuda} className="campo-ayuda">
          {llenas
            ? MENSAJE_FOTOS_LLENAS
            : `JPG, PNG o WebP de hasta 5 MB cada una. Puedes agregar ${MAX_FOTOS - fotos.length} más.`}
        </p>
        {errores.length > 0 && (
          <ul id={idErrores} className="campo-errores" role="alert">
            {errores.map((m) => (
              <li key={m}>{m}</li>
            ))}
          </ul>
        )}
      </div>

      {aviso && (
        <p className="aviso aviso-info" role="status">
          {aviso}
        </p>
      )}

      {fotos.length > 0 && (
        <ol className="inv-fotos-lista">
          {fotos.map((foto, i) => {
            const n = i + 1
            const portada = i === 0
            const nombre = `foto ${n}`
            const alt = `Vista previa de la foto ${n} de ${fotos.length}${portada ? ' (portada)' : ''}`
            return (
              <li key={foto.clave} className="inv-foto">
                <figure>
                  {foto.vista ? (
                    <img src={foto.vista} alt={alt} />
                  ) : (
                    <p className="campo-ayuda">{alt}</p>
                  )}
                  <figcaption>
                    Foto {n}: <span className="dato-secundario">{foto.archivo.name}</span>
                    {portada && <span className="cat-etiqueta inv-etiqueta-portada">Portada</span>}
                  </figcaption>
                </figure>
                <div className="acciones-fila inv-foto-acciones">
                  <button
                    id={`foto-nueva-${foto.clave}-subir`}
                    type="button"
                    className="secondary-button"
                    disabled={deshabilitado || i === 0}
                    onClick={() => mover(i, -1)}
                  >
                    Subir{' '}<span className="sr-only">{nombre}</span>
                  </button>
                  <button
                    id={`foto-nueva-${foto.clave}-bajar`}
                    type="button"
                    className="secondary-button"
                    disabled={deshabilitado || i === fotos.length - 1}
                    onClick={() => mover(i, 1)}
                  >
                    Bajar{' '}<span className="sr-only">{nombre}</span>
                  </button>
                  {!portada && (
                    <button
                      type="button"
                      className="secondary-button"
                      disabled={deshabilitado}
                      onClick={() => hacerPortada(i)}
                    >
                      Hacer portada{' '}<span className="sr-only">la {nombre}</span>
                    </button>
                  )}
                  <button
                    type="button"
                    className="boton-peligro"
                    disabled={deshabilitado}
                    onClick={() => quitar(i)}
                  >
                    Quitar{' '}<span className="sr-only">{nombre}</span>
                  </button>
                </div>
              </li>
            )
          })}
        </ol>
      )}
    </section>
  )
}
