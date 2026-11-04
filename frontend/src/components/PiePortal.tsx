import { categoriasDisponibles, type OpcionCatalogo } from '../api/catalogo'
import { HASH_BUSCAR, HASH_CUENTA, HASH_REGISTRO, hashBuscar } from '../rutas'
import { LogoPortal } from './Encabezado'

type PiePortalProps = {
  tipos: readonly OpcionCatalogo[]
  conSesion: boolean
}

/**
 * Pie de página oscuro del portal. Solo lleva enlaces que existen: el contacto, el horario y las
 * páginas legales se añaden cuando la responsable entregue esos datos (D-42); no se inventan.
 */
export function PiePortal({ tipos, conSesion }: PiePortalProps) {
  return (
    <footer className="cat-pie">
      <div className="cat-pie-interior">
        <div className="cat-pie-marca">
          <LogoPortal claro />
          <p>Portal de venta de vehículos de trabajo en Colombia.</p>
        </div>
        <nav className="cat-pie-columna" aria-label="Categorías del pie de página">
          <h2>Explora</h2>
          <ul>
            {categoriasDisponibles(tipos).map(({ tipo, enlace }) => (
              <li key={tipo.id}>
                <a href={hashBuscar({ tipoId: String(tipo.id) })}>{enlace}</a>
              </li>
            ))}
            <li>
              <a href={HASH_BUSCAR}>Todos los vehículos</a>
            </li>
          </ul>
        </nav>
        <nav className="cat-pie-columna" aria-label="Cuenta en el pie de página">
          <h2>Tu cuenta</h2>
          <ul>
            {conSesion ? (
              <li>
                <a href={HASH_CUENTA}>Mi cuenta</a>
              </li>
            ) : (
              <>
                <li>
                  <a href={HASH_CUENTA}>Ingresar</a>
                </li>
                <li>
                  <a href={HASH_REGISTRO}>Crear cuenta o inscribirme como asesor</a>
                </li>
              </>
            )}
          </ul>
        </nav>
      </div>
      <p className="cat-pie-legal">© {new Date().getFullYear()} Financiamos Tu Camión</p>
    </footer>
  )
}
