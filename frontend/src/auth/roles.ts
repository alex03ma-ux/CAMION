// Roles del sistema (contrato P2, RN-SE-006). La interfaz solo oculta pantallas;
// la API es la que protege cada acción.

export type Rol = 'COMPRADOR' | 'ASESOR' | 'ADMINISTRADOR'

export const ROL_ADMINISTRADOR: Rol = 'ADMINISTRADOR'
export const ROL_ASESOR: Rol = 'ASESOR'

export const NOMBRES_ROL: Record<Rol, string> = {
  COMPRADOR: 'Comprador',
  ASESOR: 'Asesor',
  ADMINISTRADOR: 'Administrador',
}

export function tieneRol(roles: readonly string[], rol: Rol): boolean {
  return roles.some((r) => r.toUpperCase() === rol)
}

export function esAdministrador(roles: readonly string[]): boolean {
  return tieneRol(roles, ROL_ADMINISTRADOR)
}

/** Nombre legible de un rol; si es desconocido se muestra tal cual. */
export function nombreRol(rol: string): string {
  const clave = rol.toUpperCase()
  return clave in NOMBRES_ROL ? NOMBRES_ROL[clave as Rol] : rol
}
