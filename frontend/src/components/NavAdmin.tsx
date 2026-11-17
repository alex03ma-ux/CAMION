// P4p: navegación del administrador dentro de "Mi cuenta" (Usuarios, Planes, Suscripciones, Catálogos).
// Solo se muestra a quien tiene el rol ADMINISTRADOR según /api/auth/me; la API protege cada ruta.
// P7 (CA-P7-52): Inicio, Usuarios, Planes, Suscripciones, Ventas, Inventario, Publicaciones, Contactos,
// Catálogos, Portada y Parámetros (los pagos viven dentro de Suscripciones), con aria-current en la actual.
// P8: Reportes e Indicadores, después de Ventas.
import {
  rutaSeccionAdmin,
  RUTA_CONTACTOS,
  RUTA_CUENTA,
  RUTA_INVENTARIO,
  RUTA_PUBLICACIONES,
  type SeccionAdmin,
} from '../rutas'

export const NOMBRES_SECCION_ADMIN: Record<SeccionAdmin, string> = {
  inicio: 'Inicio',
  usuarios: 'Usuarios',
  planes: 'Planes',
  suscripciones: 'Suscripciones',
  ventas: 'Ventas',
  catalogos: 'Catálogos',
  portada: 'Portada',
  parametros: 'Parámetros',
  reportes: 'Reportes',
  indicadores: 'Indicadores',
}

type Enlace = { clave: string; nombre: string; href: string; seccion?: SeccionAdmin }

function seccion(s: SeccionAdmin): Enlace {
  return { clave: s, nombre: NOMBRES_SECCION_ADMIN[s], href: rutaSeccionAdmin(s), seccion: s }
}

const ENLACES: readonly Enlace[] = [
  seccion('inicio'),
  seccion('usuarios'),
  seccion('planes'),
  seccion('suscripciones'),
  seccion('ventas'),
  // P8 (D-68 A, D-69 A; CA-P8-73): reportes e indicadores.
  seccion('reportes'),
  seccion('indicadores'),
  // Inventario, Publicaciones y Contactos viven fuera de /cuenta (también los usa el asesor).
  { clave: 'inventario', nombre: 'Inventario', href: RUTA_INVENTARIO },
  { clave: 'publicaciones', nombre: 'Publicaciones', href: RUTA_PUBLICACIONES },
  { clave: 'contactos', nombre: 'Contactos', href: RUTA_CONTACTOS },
  seccion('catalogos'),
  seccion('portada'),
  seccion('parametros'),
]

export function NavAdmin({ actual }: { actual: SeccionAdmin }) {
  return (
    <nav className="nav-admin" aria-label="Administración">
      <ul>
        {ENLACES.map((e) => (
          <li key={e.clave}>
            <a href={e.href} aria-current={e.seccion !== undefined && e.seccion === actual ? 'page' : undefined}>
              {e.nombre}
            </a>
          </li>
        ))}
        <li>
          <a href={RUTA_CUENTA}>Mi perfil</a>
        </li>
      </ul>
    </nav>
  )
}
