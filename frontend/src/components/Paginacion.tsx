import type { FiltrosCatalogo } from '../api/catalogo'
import { formatoMiles } from '../catalogo/formato'
import { hashBuscar } from '../rutas'

type PaginacionProps = {
  filtros: FiltrosCatalogo
  pagina: number
  totalPaginas: number
}

/** Paginación del catálogo con enlaces (la página viaja en la URL). */
export function Paginacion({ filtros, pagina, totalPaginas }: PaginacionProps) {
  if (totalPaginas <= 1) return null
  const enlace = (n: number) => hashBuscar({ ...filtros, pagina: String(n) })
  return (
    <nav className="cat-paginacion" aria-label="Paginación">
      {pagina > 1 ? (
        <a href={enlace(pagina - 1)} rel="prev">
          Anterior
        </a>
      ) : (
        <span aria-hidden="true" className="cat-paginacion-inactivo">
          Anterior
        </span>
      )}
      <p>
        Página {formatoMiles(pagina)} de {formatoMiles(totalPaginas)}
      </p>
      {pagina < totalPaginas ? (
        <a href={enlace(pagina + 1)} rel="next">
          Siguiente
        </a>
      ) : (
        <span aria-hidden="true" className="cat-paginacion-inactivo">
          Siguiente
        </span>
      )}
    </nav>
  )
}
