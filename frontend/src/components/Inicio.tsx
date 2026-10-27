import { useCallback } from 'react'
import {
  categoriasDisponibles,
  MENSAJE_SIN_VEHICULOS,
  obtenerInicio,
  type OpcionCatalogo,
} from '../api/catalogo'
import { usePeticion } from '../catalogo/usePeticion'
import { HASH_BUSCAR, hashBuscar } from '../rutas'
import { Aviso } from './Aviso'
import { RejillaVehiculos } from './TarjetaVehiculo'

type InicioProps = {
  tipos: readonly OpcionCatalogo[]
}

/** Portal principal (HU-001): destacados (HU-003), recientes (HU-002) y categorías. */
export function Inicio({ tipos }: InicioProps) {
  const cargar = useCallback((signal: AbortSignal) => obtenerInicio(signal), [])
  const [estado, reintentar] = usePeticion(cargar, 'inicio')

  const categorias = categoriasDisponibles(tipos)

  return (
    <div className="cat-pagina">
      <section className="cat-portada" aria-labelledby="inicio-titulo">
        <p className="eyebrow">RED DE VENTA</p>
        <h1 id="inicio-titulo" tabIndex={-1}>
          Financiamos Tu Camión
        </h1>
        <p>Tu próximo negocio puede comenzar con un camión.</p>
        <ul className="cat-accesos" aria-label="Categorías">
          {categorias.map(({ tipo, enlace }) => (
            <li key={tipo.id}>
              <a className="cat-acceso" href={hashBuscar({ tipoId: String(tipo.id) })}>
                {enlace}
              </a>
            </li>
          ))}
          <li>
            <a className="cat-acceso cat-acceso-secundario" href={HASH_BUSCAR}>
              Ver todos los vehículos
            </a>
          </li>
        </ul>
      </section>

      {estado.estado === 'cargando' && (
        <p className="cat-estado" role="status" aria-busy="true">
          Cargando vehículos...
        </p>
      )}

      {estado.estado === 'error' && (
        <div className="cat-estado">
          <Aviso aviso={{ tipo: 'error', texto: estado.error.mensaje }} />
          <button type="button" className="cat-boton" onClick={reintentar}>
            Reintentar
          </button>
        </div>
      )}

      {estado.estado === 'ok' &&
        estado.datos.destacados.length === 0 &&
        estado.datos.recientes.length === 0 && (
          <div className="cat-vacio">
            <p role="status">{MENSAJE_SIN_VEHICULOS}</p>
            <a className="cat-acceso" href={HASH_BUSCAR}>
              Ir a la búsqueda
            </a>
          </div>
        )}

      {estado.estado === 'ok' && estado.datos.destacados.length > 0 && (
        <section className="cat-seccion" aria-labelledby="inicio-destacados">
          <h2 id="inicio-destacados">Destacados</h2>
          <RejillaVehiculos vehiculos={estado.datos.destacados} />
        </section>
      )}

      {estado.estado === 'ok' && estado.datos.recientes.length > 0 && (
        <section className="cat-seccion" aria-labelledby="inicio-recientes">
          <h2 id="inicio-recientes">Recientes</h2>
          <RejillaVehiculos vehiculos={estado.datos.recientes} />
        </section>
      )}
    </div>
  )
}
