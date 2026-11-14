// P8 — Filtro de período (desde / hasta) de Indicadores y Mis indicadores. Por defecto el mes en curso; valida en
// cliente con los mensajes de la API (CA-P8-04) y muestra junto a cada fecha los errores del servidor.
import { useState, type FormEvent } from 'react'
import { mesEnCurso, validarPeriodo, type ErroresPeriodo, type Periodo } from '../api/reportes'
import { Campo } from './Campo'

type Props = {
  idBase: string
  etiqueta: string
  erroresServidor: ErroresPeriodo
  deshabilitado: boolean
  onAplicar: (periodo: Periodo) => void
}

export function FiltroPeriodo({ idBase, etiqueta, erroresServidor, deshabilitado, onAplicar }: Props) {
  const [borrador, setBorrador] = useState<Periodo>(() => mesEnCurso())
  const [errores, setErrores] = useState<ErroresPeriodo>({})
  const mostrados: ErroresPeriodo = errores.desde || errores.hasta ? errores : erroresServidor

  function aplicar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    const validacion = validarPeriodo(borrador)
    setErrores(validacion)
    if (validacion.desde || validacion.hasta) {
      document.getElementById(`${idBase}-${validacion.desde ? 'desde' : 'hasta'}`)?.focus()
      return
    }
    onAplicar({ ...borrador })
  }

  function mesActual() {
    const mes = mesEnCurso()
    setErrores({})
    setBorrador(mes)
    onAplicar(mes)
  }

  return (
    <form className="filtros inv-filtros" onSubmit={aplicar} aria-label={etiqueta} noValidate>
      <Campo
        id={`${idBase}-desde`}
        etiqueta="Desde"
        tipo="date"
        valor={borrador.desde}
        onCambio={(v) => setBorrador((b) => ({ ...b, desde: v }))}
        errores={mostrados.desde}
        requerido={false}
      />
      <Campo
        id={`${idBase}-hasta`}
        etiqueta="Hasta"
        tipo="date"
        valor={borrador.hasta}
        onCambio={(v) => setBorrador((b) => ({ ...b, hasta: v }))}
        errores={mostrados.hasta}
        requerido={false}
      />
      <div className="inv-filtros-botones">
        <button type="submit" disabled={deshabilitado}>
          Aplicar
        </button>
        <button type="button" className="secondary-button" onClick={mesActual}>
          Mes en curso
        </button>
      </div>
    </form>
  )
}

/** Errores de `desde` / `hasta` de un 400 y el resto como texto. */
export function erroresPeriodoDe(errores: Record<string, string[]>): { porCampo: ErroresPeriodo; otros: string[] } {
  const porCampo: ErroresPeriodo = {}
  const otros: string[] = []
  for (const [clave, mensajes] of Object.entries(errores)) {
    const k = clave.toLowerCase()
    if (k === 'desde' || k === 'hasta') porCampo[k] = mensajes
    else otros.push(...mensajes)
  }
  return { porCampo, otros }
}
