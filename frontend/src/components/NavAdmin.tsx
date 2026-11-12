// P4p: navegación del administrador dentro de "Mi cuenta" (Usuarios, Planes, Suscripciones, Catálogos).
// Solo se muestra a quien tiene el rol ADMINISTRADOR según /api/auth/me; la API protege cada ruta.
// P7 (CA-P7-52): Inicio, Usuarios, Planes, Suscripciones, Ventas, Inventario, Publicaciones, Contactos,
// Catálogos, Portada y Parámetros (los pagos viven dentro de Suscripciones), con aria-current en la actual.
import {
  hashSeccionAdmin,
  HASH_CONTACTOS,
  HASH_CUENTA,
  HASH_INVENTARIO,
  HASH_PUBLICACIONES,
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
}

type Enlace = { clave: string; nombre: string; href: string; seccion?: SeccionAdmin }

function seccion(s: SeccionAdmin): Enlace {
  return { clave: s, nombre: NOMBRES_SECCION_ADMIN[s], href: hashSeccionAdmin(s), seccion: s }
}

const ENLACES: readonly Enlace[] = [
  seccion('inicio'),
  seccion('usuarios'),
  seccion('planes'),
  seccion('suscripciones'),
  seccion('ventas'),
  // Inventario, Publicaciones y Contactos viven fuera de #/cuenta (también los usa el asesor).
  { clave: 'inventario', nombre: 'Inventario', href: HASH_INVENTARIO },
  { clave: 'publicaciones', nombre: 'Publicaciones', href: HASH_PUBLICACIONES },
  { clave: 'contactos', nombre: 'Contactos', href: HASH_CONTACTOS },
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
          <a href={HASH_CUENTA}>Mi perfil</a>
        </li>
      </ul>
    </nav>
  )
}
