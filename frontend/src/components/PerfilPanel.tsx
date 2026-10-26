import { useEffect, useRef } from 'react'
import type { PerfilUsuario } from '../api/auth'
import { MINUTOS_INACTIVIDAD } from '../auth/inactivity'

type PerfilPanelProps = {
  perfil: PerfilUsuario
  cerrando: boolean
  onCerrarSesion: () => void
}

const NOMBRES_ROL: Record<string, string> = {
  COMPRADOR: 'Comprador',
  ASESOR: 'Asesor',
  ADMINISTRADOR: 'Administrador',
}

export function PerfilPanel({ perfil, cerrando, onCerrarSesion }: PerfilPanelProps) {
  const titulo = useRef<HTMLHeadingElement>(null)
  useEffect(() => {
    titulo.current?.focus()
  }, [])

  const roles = perfil.roles.map((r) => NOMBRES_ROL[r.toUpperCase()] ?? r).join(', ')

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
      <button type="button" onClick={onCerrarSesion} disabled={cerrando}>
        {cerrando ? 'Cerrando sesión...' : 'Cerrar sesión'}
      </button>
    </section>
  )
}
