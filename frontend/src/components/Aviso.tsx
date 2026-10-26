export type TipoAviso = 'error' | 'advertencia' | 'exito' | 'info'

export type DatosAviso = {
  tipo: TipoAviso
  texto: string
}

/**
 * Mensaje general. Los errores y advertencias usan role="alert" (se anuncian de
 * inmediato); los avisos de éxito o informativos usan role="status".
 */
export function Aviso({ aviso, id }: { aviso: DatosAviso | null; id?: string }) {
  if (!aviso) return null
  const urgente = aviso.tipo === 'error' || aviso.tipo === 'advertencia'
  return (
    <p id={id} className={`aviso aviso-${aviso.tipo}`} role={urgente ? 'alert' : 'status'}>
      {aviso.texto}
    </p>
  )
}
