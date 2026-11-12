// P6 — "Mis solicitudes" del comprador (D-53, D-59): #/mis-solicitudes, en "Mi cuenta".
// Una sola lista con sus contactos, avisos, créditos y búsquedas enviados con la sesión iniciada (CA-P6-66, 67),
// de solo lectura (CA-P6-69): fecha, tipo, resumen y estado; sin notas, responsable ni resultado (CA-P6-68).
// Solo COMPRADOR: a otro rol no se le ofrece y no se llama a la API (la API responde 403 de todos modos, CA-P6-74).
import { useCallback, useEffect, useRef, useState } from 'react'
import type { PerfilUsuario } from '../api/auth'
import { ApiError, MENSAJE_INESPERADO, MENSAJE_SIN_PERMISO } from '../api/http'
import { formatearFechaHora } from '../api/leads'
import {
  listarMisSolicitudes,
  MENSAJE_VEHICULO_YA_NO_DISPONIBLE,
  nombreEstadoSolicitud,
  nombreTipoSolicitud,
  type MiSolicitud,
  type PaginaMisSolicitudes,
} from '../api/misSolicitudes'
import { tieneRol } from '../auth/roles'
import { formatoMiles } from '../catalogo/formato'
import {
  HASH_BUSCAR,
  HASH_BUSCO_VEHICULO,
  HASH_CREDITO,
  HASH_CUENTA,
  HASH_SEGURO_TODO_RIESGO,
  HASH_VENDE_TU_CAMION,
  hashVehiculo,
} from '../rutas'
import { Aviso, type DatosAviso } from './Aviso'
import { RejillaVehiculos } from './TarjetaVehiculo'

export const TITULO_MIS_SOLICITUDES = 'Mis solicitudes'

type MisSolicitudesProps = {
  token: string
  perfil: PerfilUsuario
  onSesionTerminada: () => void
}

export function MisSolicitudes({ token, perfil, onSesionTerminada }: MisSolicitudesProps) {
  if (!tieneRol(perfil.roles, 'COMPRADOR')) {
    return (
      <div className="cat-pagina inv-pagina">
        <h1 tabIndex={-1} className="inv-titulo">
          {TITULO_MIS_SOLICITUDES}
        </h1>
        <Aviso aviso={{ tipo: 'error', texto: MENSAJE_SIN_PERMISO }} />
        <p>
          <a href={HASH_CUENTA}>Volver a mi cuenta</a>
        </p>
      </div>
    )
  }
  return <ListaMisSolicitudes token={token} onSesionTerminada={onSesionTerminada} />
}

function ListaMisSolicitudes({ token, onSesionTerminada }: { token: string; onSesionTerminada: () => void }) {
  const [pagina, setPagina] = useState(1)
  const [intento, setIntento] = useState(0)
  const [datos, setDatos] = useState<PaginaMisSolicitudes | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<DatosAviso | null>(null)
  const [sinPermiso, setSinPermiso] = useState(false)
  const tituloLista = useRef<HTMLHeadingElement>(null)

  const alError401 = useCallback(
    (e: unknown) => {
      if (e instanceof ApiError && e.status === 401) {
        onSesionTerminada()
        return true
      }
      return false
    },
    [onSesionTerminada],
  )

  useEffect(() => {
    const control = new AbortController()
    setCargando(true)
    setError(null)
    listarMisSolicitudes(token, pagina, control.signal)
      .then((respuesta) => {
        setDatos(respuesta)
        setCargando(false)
      })
      .catch((e: unknown) => {
        if (control.signal.aborted) return
        setCargando(false)
        if (alError401(e)) return
        setDatos(null)
        setSinPermiso(e instanceof ApiError && e.status === 403)
        setError({ tipo: 'error', texto: e instanceof ApiError ? e.mensaje : MENSAJE_INESPERADO })
      })
    return () => control.abort()
  }, [token, pagina, intento, alError401])

  function irAPagina(nueva: number) {
    setPagina(nueva)
    tituloLista.current?.focus()
  }

  const totalPaginas = datos ? Math.max(1, Math.ceil(datos.total / Math.max(1, datos.tamano))) : 1

  return (
    <div className="cat-pagina inv-pagina mis-solicitudes">
      <p className="inv-volver">
        <a href={HASH_CUENTA}>Volver a mi cuenta</a>
      </p>
      <h1 tabIndex={-1} className="inv-titulo">
        {TITULO_MIS_SOLICITUDES}
      </h1>
      <p className="inv-intro">
        Aquí ves lo que nos enviaste con tu sesión iniciada: contactos por vehículos, créditos, búsquedas y avisos. Si
        quieres cambiar o cancelar algo, escríbenos por WhatsApp al 316 615 2907.
      </p>

      <h2 ref={tituloLista} tabIndex={-1} className="inv-subtitulo">
        Solicitudes
        {datos && !cargando && (
          <span className="admin-total">
            {' '}
            ({formatoMiles(datos.total)} {datos.total === 1 ? 'solicitud' : 'solicitudes'})
          </span>
        )}
      </h2>

      {cargando && <p role="status">Cargando tus solicitudes...</p>}
      <Aviso aviso={error} />
      {error && !sinPermiso && (
        <button type="button" className="secondary-button inv-boton-auto" onClick={() => setIntento((i) => i + 1)}>
          Reintentar
        </button>
      )}

      {datos && !cargando && datos.items.length === 0 && (
        <div className="mis-solicitudes-vacio">
          <p>Aún no tienes solicitudes.</p>
          <ul className="mis-solicitudes-accesos" aria-label="Empieza aquí">
            <li>
              <a href={HASH_BUSCAR}>Ver vehículos</a>
            </li>
            <li>
              <a href={HASH_CREDITO}>Quiero un crédito</a>
            </li>
            <li>
              <a href={HASH_BUSCO_VEHICULO}>Busco un vehículo</a>
            </li>
            <li>
              <a href={HASH_VENDE_TU_CAMION}>Vende tu camión</a>
            </li>
            <li>
              <a href={HASH_SEGURO_TODO_RIESGO}>Seguro todo riesgo</a>
            </li>
          </ul>
        </div>
      )}

      {datos && datos.items.length > 0 && (
        <ol className="inv-lista mis-solicitudes-lista" aria-busy={cargando}>
          {datos.items.map((s) => (
            <TarjetaSolicitud key={s.id} solicitud={s} />
          ))}
        </ol>
      )}

      {datos && totalPaginas > 1 && (
        <nav className="paginacion" aria-label="Paginación de mis solicitudes">
          <button
            type="button"
            className="secondary-button"
            disabled={cargando || pagina <= 1}
            onClick={() => irAPagina(pagina - 1)}
          >
            Anterior
          </button>
          <span>
            Página {formatoMiles(datos.pagina)} de {formatoMiles(totalPaginas)}
          </span>
          <button
            type="button"
            className="secondary-button"
            disabled={cargando || pagina >= totalPaginas}
            onClick={() => irAPagina(pagina + 1)}
          >
            Siguiente
          </button>
        </nav>
      )}
    </div>
  )
}

function TarjetaSolicitud({ solicitud: s }: { solicitud: MiSolicitud }) {
  const finalizada = s.estado === 'FINALIZADA'
  const tituloId = `mi-solicitud-${s.id}`
  return (
    <li className="inv-tarjeta mi-solicitud">
      <article className="inv-tarjeta-cuerpo" aria-labelledby={tituloId}>
        <h3 id={tituloId} className="inv-tarjeta-titulo">
          {nombreTipoSolicitud(s.tipo)}
        </h3>
        <p>
          <span className={`estado ${finalizada ? 'estado-activa' : 'estado-pendiente'}`}>
            <span className="sr-only">Estado: </span>
            {nombreEstadoSolicitud(s.estado)}
          </span>
        </p>
        <dl className="inv-datos">
          <div>
            <dt>Fecha</dt>
            <dd>
              <time dateTime={s.creadoEn}>{formatearFechaHora(s.creadoEn)}</time>
            </dd>
          </div>
          <div>
            <dt>Resumen</dt>
            <dd>{s.resumen || '—'}</dd>
          </div>
          {s.vehiculo && (
            <div>
              <dt>Vehículo</dt>
              <dd>
                {s.vehiculo.disponible && s.vehiculo.slug ? (
                  <a href={hashVehiculo(s.vehiculo.slug)}>{s.vehiculo.titulo || 'Ver vehículo'}</a>
                ) : (
                  <>
                    {s.vehiculo.titulo && <span>{s.vehiculo.titulo}. </span>}
                    <span className="dato-secundario">{MENSAJE_VEHICULO_YA_NO_DISPONIBLE}</span>
                  </>
                )}
              </dd>
            </div>
          )}
        </dl>
        {s.tipo === 'BUSQUEDA' && s.recomendados.length > 0 && (
          <section className="mi-solicitud-recomendados" aria-labelledby={`${tituloId}-recomendados`}>
            <h4 id={`${tituloId}-recomendados`}>Te recomendamos estos vehículos</h4>
            <RejillaVehiculos vehiculos={s.recomendados} />
          </section>
        )}
      </article>
    </li>
  )
}
