export type TipoAviso = 'error' | 'exito' | 'info'

export type DatosAviso = {
  tipo: TipoAviso
  texto: string
}

/**
 * Mensaje general. Los errores usan role="alert" (se anuncian de inmediato);
 * los avisos de éxito o informativos usan role="status".
 */
export function Aviso({ aviso }: { aviso: DatosAviso | null }) {
  if (!aviso) return null
  return (
    <p className={`aviso aviso-${aviso.tipo}`} role={aviso.tipo === 'error' ? 'alert' : 'status'}>
      {aviso.texto}
    </p>
  )
}
