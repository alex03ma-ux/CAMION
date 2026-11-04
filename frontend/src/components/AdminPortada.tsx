// D-42: el administrador cambia la foto de portada del portal o vuelve a la ilustración, sin tocar código.
import { useEffect, useRef, useState, type ChangeEvent, type FormEvent } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { obtenerPortada, quitarPortada, subirPortada, validarArchivoPortada } from '../api/portada'
import { Aviso, type DatosAviso } from './Aviso'
import { DialogoConfirmacion } from './DialogoAccion'
import { IlustracionCamion } from './IconosPortal'

type AdminPortadaProps = {
  token: string
  onSesionTerminada?: () => void
}

type Estado = { tipo: 'cargando' } | { tipo: 'ok'; url: string | null } | { tipo: 'error'; mensaje: string }

export function AdminPortada({ token, onSesionTerminada }: AdminPortadaProps) {
  const [estado, setEstado] = useState<Estado>({ tipo: 'cargando' })
  const [intento, setIntento] = useState(0)
  const [archivo, setArchivo] = useState<File | null>(null)
  const [vistaPrevia, setVistaPrevia] = useState<string | null>(null)
  const [errorArchivo, setErrorArchivo] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [aviso, setAviso] = useState<DatosAviso | null>(null)
  const [confirmarQuitar, setConfirmarQuitar] = useState(false)
  const [errorDialogo, setErrorDialogo] = useState<DatosAviso | null>(null)
  const refArchivo = useRef<HTMLInputElement>(null)
  const refQuitar = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    const control = new AbortController()
    setEstado({ tipo: 'cargando' })
    obtenerPortada(control.signal)
      .then((url) => setEstado({ tipo: 'ok', url }))
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        setEstado({ tipo: 'error', mensaje: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO })
      })
    return () => control.abort()
  }, [intento])

  // Libera la URL temporal de la vista previa.
  useEffect(() => {
    if (!vistaPrevia) return
    return () => URL.revokeObjectURL(vistaPrevia)
  }, [vistaPrevia])

  function manejarError(error: unknown): DatosAviso | null {
    if (error instanceof ApiError && error.status === 401 && onSesionTerminada) {
      onSesionTerminada()
      return null
    }
    return { tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO }
  }

  function elegir(evento: ChangeEvent<HTMLInputElement>) {
    const elegido = evento.target.files?.[0] ?? null
    setAviso(null)
    setArchivo(null)
    setVistaPrevia(null)
    if (!elegido) {
      setErrorArchivo(null)
      return
    }
    const error = validarArchivoPortada(elegido)
    setErrorArchivo(error)
    if (error) return
    setArchivo(elegido)
    setVistaPrevia(URL.createObjectURL(elegido))
  }

  async function guardar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (enviando) return
    if (!archivo) {
      setErrorArchivo('Elige una foto para la portada.')
      refArchivo.current?.focus()
      return
    }
    setEnviando(true)
    setAviso(null)
    try {
      const url = await subirPortada(token, archivo)
      setEstado({ tipo: 'ok', url })
      setArchivo(null)
      setVistaPrevia(null)
      if (refArchivo.current) refArchivo.current.value = ''
      setAviso({ tipo: 'exito', texto: 'Listo. La nueva foto ya se ve en la portada del portal.' })
    } catch (error) {
      setAviso(manejarError(error))
    } finally {
      setEnviando(false)
    }
  }

  async function quitar() {
    setEnviando(true)
    setErrorDialogo(null)
    try {
      await quitarPortada(token)
      setEstado({ tipo: 'ok', url: null })
      setConfirmarQuitar(false)
      setAviso({ tipo: 'exito', texto: 'Listo. La portada vuelve a mostrar la ilustración del camión.' })
    } catch (error) {
      const datos = manejarError(error)
      if (datos) setErrorDialogo(datos)
    } finally {
      setEnviando(false)
    }
  }

  const actual = estado.tipo === 'ok' ? estado.url : null

  return (
    <section className="admin-portada" aria-labelledby="admin-portada-titulo">
      <h2 id="admin-portada-titulo">Portada</h2>
      <p>
        Es la imagen de fondo de la entrada del portal, detrás de «Tu próximo negocio empieza con el vehículo
        correcto».
      </p>

      {estado.tipo === 'cargando' && <p role="status">Cargando la portada...</p>}
      {estado.tipo === 'error' && (
        <>
          <Aviso aviso={{ tipo: 'error', texto: estado.mensaje }} />
          <button type="button" className="secondary-button" onClick={() => setIntento((i) => i + 1)}>
            Reintentar
          </button>
        </>
      )}

      {estado.tipo === 'ok' && (
        <>
          <h3>{vistaPrevia ? 'Vista previa de la foto nueva' : 'Portada actual'}</h3>
          <div className="admin-portada-marco">
            {vistaPrevia ? (
              <img src={vistaPrevia} alt="Vista previa de la foto nueva" />
            ) : actual ? (
              <img src={actual} alt="Foto de portada actual" />
            ) : (
              <IlustracionCamion />
            )}
          </div>
          {!vistaPrevia && !actual && (
            <p className="campo-ayuda">Ahora se usa la ilustración del camión que trae el portal.</p>
          )}

          <form className="admin-portada-form" onSubmit={guardar} noValidate aria-busy={enviando}>
            <label htmlFor="admin-portada-archivo">Foto nueva</label>
            <input
              ref={refArchivo}
              id="admin-portada-archivo"
              type="file"
              accept="image/jpeg,image/png,image/webp"
              onChange={elegir}
              disabled={enviando}
              aria-invalid={errorArchivo !== null}
              aria-describedby={errorArchivo ? 'admin-portada-ayuda admin-portada-error' : 'admin-portada-ayuda'}
            />
            <p id="admin-portada-ayuda" className="campo-ayuda">
              JPG, PNG o WebP de hasta 5 MB. Mejor horizontal (unos 1600 × 900 píxeles), con el vehículo hacia la
              derecha, porque el texto va a la izquierda. Usa una foto propia o con permiso de uso.
            </p>
            {errorArchivo && (
              <p id="admin-portada-error" className="campo-errores" role="alert">
                {errorArchivo}
              </p>
            )}
            <Aviso aviso={aviso} />
            <div className="admin-portada-botones">
              <button type="submit" disabled={enviando}>
                {enviando && !confirmarQuitar ? 'Guardando...' : 'Usar esta foto'}
              </button>
              {actual && (
                <button
                  ref={refQuitar}
                  type="button"
                  className="secondary-button"
                  onClick={() => {
                    setErrorDialogo(null)
                    setConfirmarQuitar(true)
                  }}
                  disabled={enviando}
                >
                  Quitar foto y usar la ilustración
                </button>
              )}
            </div>
          </form>
        </>
      )}

      {confirmarQuitar && (
        <DialogoConfirmacion
          titulo="Quitar la foto de portada"
          descripcion="¿Quitar la foto actual? La portada volverá a mostrar la ilustración del camión."
          textoBoton="Quitar foto"
          textoEnviando="Quitando..."
          peligro
          motivo="no"
          enviando={enviando}
          error={errorDialogo}
          onConfirmar={() => void quitar()}
          onCancelar={() => {
            setConfirmarQuitar(false)
            refQuitar.current?.focus()
          }}
        />
      )}
    </section>
  )
}
