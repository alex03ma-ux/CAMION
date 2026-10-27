import { useEffect, useRef } from 'react'
import type { PerfilUsuario } from '../api/auth'
import { MINUTOS_INACTIVIDAD } from '../auth/inactivity'
import { esAdministrador, nombreRol } from '../auth/roles'

type PerfilPanelProps = {
  perfil: PerfilUsuario
  cerrando: boolean
  onCerrarSesion: () => void
  /** Abre el panel de administración; solo se ofrece a quien tiene el rol ADMINISTRADOR. */
  onAbrirAdministracion?: () => void
}

export function PerfilPanel({
  perfil,
  cerrando,
  onCerrarSesion,
  onAbrirAdministracion,
}: PerfilPanelProps) {
  const titulo = useRef<HTMLHeadingElement>(null)
  useEffect(() => {
    titulo.current?.focus()
  }, [])

  const roles = perfil.roles.map(nombreRol).join(', ')
  // Solo oculta el acceso: la API vuelve a comprobar el rol en cada petición.
  const verAdministracion = onAbrirAdministracion !== undefined && esAdministrador(perfil.roles)

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
      {verAdministracion && (
        <button type="button" onClick={onAbrirAdministracion} disabled={cerrando}>
          Administrar usuarios
        </button>
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
