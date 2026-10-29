// P4p: navegación del administrador dentro de "Mi cuenta" (Usuarios, Planes, Suscripciones, Catálogos).
// Solo se muestra a quien tiene el rol ADMINISTRADOR según /api/auth/me; la API protege cada ruta.
import { hashSeccionAdmin, HASH_CUENTA, type SeccionAdmin } from '../rutas'

export const NOMBRES_SECCION_ADMIN: Record<SeccionAdmin, string> = {
  usuarios: 'Usuarios',
  planes: 'Planes',
  suscripciones: 'Suscripciones',
  catalogos: 'Catálogos',
}

const ORDEN: readonly SeccionAdmin[] = ['usuarios', 'planes', 'suscripciones', 'catalogos']

export function NavAdmin({ actual }: { actual: SeccionAdmin }) {
  return (
    <nav className="nav-admin" aria-label="Administración">
      <ul>
        {ORDEN.map((seccion) => (
          <li key={seccion}>
            <a
              href={hashSeccionAdmin(seccion)}
              aria-current={seccion === actual ? 'page' : undefined}
            >
              {NOMBRES_SECCION_ADMIN[seccion]}
            </a>
          </li>
        ))}
        <li>
          <a href={HASH_CUENTA}>Mi perfil</a>
        </li>
      </ul>
    </nav>
  )
}
