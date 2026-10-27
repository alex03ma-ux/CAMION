import { useEffect, useState } from 'react'
import {
  buscarPublicaciones,
  categoriasDisponibles,
  CLAVES_FILTROS,
  consultaPublicaciones,
  MENSAJE_SIN_RESULTADOS,
  type FiltrosCatalogo,
  type OpcionesFiltros,
} from '../api/catalogo'
import type { ErroresPorCampo } from '../api/http'
import { textoTotal } from '../catalogo/formato'
import type { EstadoPeticion } from '../catalogo/usePeticion'
import { usePeticion } from '../catalogo/usePeticion'
import { HASH_BUSCAR, hashBuscar, navegar, recordarBusqueda } from '../rutas'
import { Aviso } from './Aviso'
import { Filtros } from './Filtros'
import { Paginacion } from './Paginacion'
import { RejillaVehiculos } from './TarjetaVehiculo'

type ResultadosProps = {
  filtros: FiltrosCatalogo
  /** Hash actual (`#/buscar?…`), para recordar la búsqueda y reiniciar el formulario. */
  hash: string
  opciones: EstadoPeticion<OpcionesFiltros>
  onReintentarOpciones: () => void
}

/**
 * Lleva los errores del servidor a las claves de los filtros (sin distinguir
 * mayúsculas) y `tamano` a `pagina`, que comparten mensaje.
 */
function erroresPorFiltro(errores: ErroresPorCampo): ErroresPorCampo {
  const resultado: ErroresPorCampo = {}
  for (const [campo, mensajes] of Object.entries(errores)) {
    const minuscula = campo.toLowerCase()
    const clave =
      minuscula === 'tamano'
        ? 'pagina'
        : (CLAVES_FILTROS.find((c) => c.toLowerCase() === minuscula) ?? campo)
    resultado[clave] = [...(resultado[clave] ?? []), ...mensajes]
  }
  return resultado
}

/** Resultados de búsqueda con filtros y paginación (HU-004 a HU-012). */
export function Resultados({ filtros, hash, opciones, onReintentarOpciones }: ResultadosProps) {
  const consulta = consultaPublicaciones(filtros)
  // La consulta resume todos los filtros: se vuelve a pedir solo cuando cambia.
  const [estado, reintentar] = usePeticion(
    (signal) => buscarPublicaciones(filtros, signal),
    consulta,
  )
  const [filtrosAbiertos, setFiltrosAbiertos] = useState(false)

  useEffect(() => {
    recordarBusqueda(hash)
  }, [hash])

  const datosOpciones = opciones.estado === 'ok' ? opciones.datos : null
  const error400 = estado.estado === 'error' && estado.error.status === 400
  const erroresServidor = error400 ? erroresPorFiltro(estado.error.errores) : {}
  // En móvil los filtros se abren solos si el servidor marcó alguno.
  const abiertos = filtrosAbiertos || error400

  const categoria = categoriasDisponibles(datosOpciones?.tipos ?? []).find(
    (c) => String(c.tipo.id) === filtros.tipoId,
  )
  const titulo = categoria ? categoria.enlace : 'Buscar vehículos'

  const limpiar = () => navegar(HASH_BUSCAR)

  let contador = ''
  if (estado.estado === 'cargando') contador = 'Buscando vehículos...'
  else if (estado.estado === 'ok') contador = textoTotal(estado.datos.total)

  const totalPaginas =
    estado.estado === 'ok' && estado.datos.tamano > 0
      ? Math.ceil(estado.datos.total / estado.datos.tamano)
      : 0

  return (
    <div className="cat-pagina">
      <h1 className="cat-titulo" tabIndex={-1}>
        {titulo}
        {filtros.q !== '' && (
          <span className="cat-titulo-busqueda"> para «{filtros.q}»</span>
        )}
      </h1>

      <div className="cat-resultados">
        <aside className="cat-lateral" aria-label="Filtros de búsqueda">
          <button
            type="button"
            className="cat-boton secondary-button cat-plegar"
            aria-expanded={abiertos}
            aria-controls="panel-filtros"
            onClick={() => setFiltrosAbiertos(!abiertos)}
          >
            {abiertos ? 'Ocultar filtros' : 'Mostrar filtros'}
          </button>
          <div
            id="panel-filtros"
            className={abiertos ? 'cat-panel-filtros abierto' : 'cat-panel-filtros'}
          >
            {opciones.estado === 'error' && (
              <div className="cat-estado">
                <Aviso
                  aviso={{
                    tipo: 'advertencia',
                    texto: 'No se pudieron cargar las opciones de los filtros.',
                  }}
                />
                <button
                  type="button"
                  className="cat-boton secondary-button"
                  onClick={onReintentarOpciones}
                >
                  Reintentar
                </button>
              </div>
            )}
            <Filtros
              key={hash}
              filtros={filtros}
              opciones={datosOpciones}
              erroresServidor={erroresServidor}
              onAplicar={(nuevos) => navegar(hashBuscar(nuevos))}
              onLimpiar={limpiar}
            />
          </div>
        </aside>

        <section className="cat-lista" aria-labelledby="resultados-titulo">
          <h2 id="resultados-titulo" className="sr-only">
            Resultados
          </h2>
          <p className="cat-contador" role="status" aria-busy={estado.estado === 'cargando'}>
            {contador}
          </p>

          {estado.estado === 'error' && (
            <div className="cat-estado">
              <Aviso aviso={{ tipo: 'error', texto: estado.error.mensaje }} />
              {error400 ? (
                <button type="button" className="cat-boton" onClick={limpiar}>
                  Limpiar filtros
                </button>
              ) : (
                <button type="button" className="cat-boton" onClick={reintentar}>
                  Reintentar
                </button>
              )}
            </div>
          )}

          {estado.estado === 'ok' && estado.datos.total === 0 && (
            <div className="cat-vacio">
              <p>{MENSAJE_SIN_RESULTADOS}</p>
              <button type="button" className="cat-boton" onClick={limpiar}>
                Limpiar filtros
              </button>
            </div>
          )}

          {estado.estado === 'ok' && estado.datos.total > 0 && estado.datos.items.length === 0 && (
            <div className="cat-vacio">
              <p>Esta página no tiene resultados.</p>
              <a className="cat-acceso" href={hashBuscar({ ...filtros, pagina: '' })}>
                Ir a la primera página
              </a>
            </div>
          )}

          {estado.estado === 'ok' && estado.datos.items.length > 0 && (
            <RejillaVehiculos vehiculos={estado.datos.items} />
          )}

          {estado.estado === 'ok' && (
            <Paginacion
              filtros={filtros}
              pagina={estado.datos.pagina}
              totalPaginas={totalPaginas}
            />
          )}
        </section>
      </div>
    </div>
  )
}
