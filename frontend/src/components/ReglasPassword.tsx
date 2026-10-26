import { evaluarPassword } from '../auth/passwordRules'

/** Lista de reglas de contraseña con indicación de cuáles se cumplen. */
export function ReglasPassword({ id, password }: { id: string; password: string }) {
  const estados = evaluarPassword(password)
  return (
    <div id={id} className="reglas-password">
      <p className="reglas-titulo">La contraseña debe tener:</p>
      <ul>
        {estados.map(({ regla, cumplida }) => (
          <li key={regla.id} className={cumplida ? 'regla-cumplida' : 'regla-pendiente'}>
            <span className="regla-marca" aria-hidden="true">
              {cumplida ? '✓' : '•'}
            </span>
            <span>{regla.descripcion}</span>
            <span className="sr-only">{cumplida ? ' (cumplida)' : ' (pendiente)'}</span>
          </li>
        ))}
      </ul>
    </div>
  )
}
