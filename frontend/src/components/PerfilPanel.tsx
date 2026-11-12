import { useEffect, useRef } from 'react'
import type { PerfilUsuario } from '../api/auth'
import { MINUTOS_INACTIVIDAD } from '../auth/inactivity'
import { esAdministrador, nombreRol, ROL_ASESOR, tieneRol } from '../auth/roles'
import { hashSeccionAdmin, HASH_INVENTARIO } from '../rutas'
import { textoAccesoInventario } from './InventarioPagina'
import { MiSuscripcion } from './MiSuscripcion'

type PerfilPanelProps = {
  perfil: PerfilUsuario
  cerrando: boolean
  onCerrarSesion: () => void
  /** Abre el panel de administración; solo se ofrece a quien tiene el rol ADMINISTRADOR. */
  onAbrirAdministracion?: () => void
  /** P4p: token para la tarjeta "Mi suscripción" del asesor (si falta, no se muestra). */
  token?: string
  /** P4p: 401 al consultar la suscripción. */
  onSesionTerminada?: () => void
}

export function PerfilPanel({
  perfil,
  cerrando,
  onCerrarSesion,
  onAbrirAdministracion,
  token,
  onSesionTerminada,
}: PerfilPanelProps) {
  const titulo = useRef<HTMLHeadingElement>(null)
  useEffect(() => {
    titulo.current?.focus()
  }, [])

  const roles = perfil.roles.map(nombreRol).join(', ')
  // Solo oculta el acceso: la API vuelve a comprobar el rol en cada petición.
  const verAdministracion = onAbrirAdministracion !== undefined && esAdministrador(perfil.roles)
  // P4a: "Mi inventario" (asesor) o "Inventario" (administrador); un comprador no lo ve.
  const accesoInventario = textoAccesoInventario(perfil.roles)
  // P4p: el asesor ve su suscripción; la API (SoloAsesor) lo vuelve a comprobar.
  const verSuscripcion = token !== undefined && tieneRol(perfil.roles, ROL_ASESOR)

  return (
    <section aria-labelledby="perfil-titulo">
      <h2 id="perfil-titulo" ref={titulo} tabIndex={-1}>
        ¡Bienvenido, {perfil.nombreCompleto}!
      </h2>
      <dl className="perfil-datos">
        <dt>Correo</dt>
        <dd>{perfil.email}</dd>
        <dt>Teléfono</dt>
        <dd>{perfil.phoneNumber || 'Sin registrar'}</dd>
        <dt>Rol</dt>
        <dd>{roles || 'Sin rol'}</dd>
      </dl>
      <p className="form-ayuda">
        Por seguridad, la sesión se cierra sola tras {MINUTOS_INACTIVIDAD} minutos sin actividad.
      </p>
      {verSuscripcion && <MiSuscripcion token={token} onSesionTerminada={onSesionTerminada} />}
      {accesoInventario && (
        <a className="inv-boton-enlace inv-boton-bloque" href={HASH_INVENTARIO}>
          {accesoInventario}
        </a>
      )}
      {verAdministracion && (
        <button type="button" onClick={onAbrirAdministracion} disabled={cerrando}>
          Administrar usuarios
        </button>
      )}
      {verAdministracion && (
        <nav className="perfil-admin-enlaces" aria-label="Más administración">
          <a className="inv-boton-enlace inv-boton-secundario" href={hashSeccionAdmin('planes')}>
            Planes
          </a>
          <a className="inv-boton-enlace inv-boton-secundario" href={hashSeccionAdmin('suscripciones')}>
            Suscripciones
          </a>
          <a className="inv-boton-enlace inv-boton-secundario" href={hashSeccionAdmin('catalogos')}>
            Catálogos
          </a>
        </nav>
      )}
      <button
        className={verAdministracion ? 'secondary-button' : undefined}
        type="button"
        onClick={onCerrarSesion}
        disabled={cerrando}
      >
        {cerrando ? 'Cerrando sesión...' : 'Cerrar sesión'}
      </button>
    </section>
  )
}
