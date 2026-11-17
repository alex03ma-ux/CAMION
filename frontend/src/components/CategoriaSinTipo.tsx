// P10 (CA-P10-25, 39, 40): página de una categoría (`/camiones`…) mientras no se conoce su tipo o cuando no está en
// `/api/catalogo/filtros` porque no tiene vehículos visibles. No es "no encontrada": la dirección existe.
import { CATEGORIAS, type OpcionesFiltros, type SlugCategoria } from '../api/catalogo'
import type { EstadoPeticion } from '../catalogo/usePeticion'
import { RUTA_BUSCAR } from '../rutas'
import { Aviso } from './Aviso'

export const MENSAJE_CATEGORIA_VACIA = 'Por ahora no hay vehículos publicados en esta categoría.'

type CategoriaSinTipoProps = {
  categoria: SlugCategoria
  opciones: EstadoPeticion<OpcionesFiltros>
  onReintentar: () => void
}

/** Nombre visible de una categoría ("Camiones"). */
export function nombreCategoria(slug: SlugCategoria): string {
  return CATEGORIAS.find((c) => c.slug === slug)?.enlace ?? slug
}

export function CategoriaSinTipo({ categoria, opciones, onReintentar }: CategoriaSinTipoProps) {
  return (
    <div className="cat-pagina">
      <h1 className="cat-titulo" tabIndex={-1}>
        {nombreCategoria(categoria)}
      </h1>
      {opciones.estado === 'cargando' && (
        <p className="cat-estado" role="status" aria-busy="true">
          Cargando vehículos...
        </p>
      )}
      {opciones.estado === 'error' && (
        <div className="cat-estado">
          <Aviso aviso={{ tipo: 'error', texto: opciones.error.mensaje }} />
          <button type="button" className="cat-boton" onClick={onReintentar}>
            Reintentar
          </button>
        </div>
      )}
      {opciones.estado === 'ok' && (
        <div className="cat-vacio">
          <p role="status">{MENSAJE_CATEGORIA_VACIA}</p>
          <a className="cat-acceso" href={RUTA_BUSCAR}>
            Ver todos los vehículos
          </a>
        </div>
      )}
    </div>
  )
}
