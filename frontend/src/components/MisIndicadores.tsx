// P8 — "Mis indicadores" del asesor en #/mis-indicadores (D-70 A; CA-P8-51 a 55). Solo lo suyo: sus vehículos por
// estado, sus publicaciones visibles hoy, los contactos de los que es responsable con su resultado y sus ventas
// vigentes. Sin avisos, créditos, búsquedas, pagos, cifras de otros asesores, comparaciones ni exportación.
// Quien no es asesor ve "No tienes permiso" sin llamar a la API; la API lo protege igual (SoloAsesor: 403 / 401).
import { useCallback, useEffect, useState } from 'react'
import type { PerfilUsuario } from '../api/auth'
import { ApiError, MENSAJE_INESPERADO, MENSAJE_SIN_PERMISO } from '../api/http'
import { nombreResultado } from '../api/leads'
import {
  ESTADOS_INVENTARIO,
  mesEnCurso,
  obtenerIndicadoresAsesor,
  textoPeriodo,
  type ErroresPeriodo,
  type IndicadoresAsesor,
  type Periodo,
} from '../api/reportes'
import { ROL_ASESOR, tieneRol } from '../auth/roles'
import { formatoCop, formatoMiles } from '../catalogo/formato'
import { nombreEstado } from '../inventario/estados'
import { HASH_CUENTA } from '../rutas'
import { Aviso, type DatosAviso } from './Aviso'
import { erroresPeriodoDe, FiltroPeriodo } from './FiltroPeriodo'
import { Cifras, TablaDatos } from './PiezasReporte'

export const TITULO_MIS_INDICADORES = 'Mis indicadores'

type Props = { token: string; perfil: PerfilUsuario; onSesionTerminada: () => void }

type Carga = { estado: 'cargando' } | { estado: 'ok'; datos: IndicadoresAsesor } | { estado: 'error'; aviso: DatosAviso }

export function MisIndicadores({ token, perfil, onSesionTerminada }: Props) {
  if (!tieneRol(perfil.roles, ROL_ASESOR)) {
    return (
      <div className="cat-pagina inv-pagina">
        <h1 tabIndex={-1} className="inv-titulo">
          {TITULO_MIS_INDICADORES}
        </h1>
        <Aviso aviso={{ tipo: 'error', texto: MENSAJE_SIN_PERMISO }} />
        <p>
          <a href={HASH_CUENTA}>Volver a mi cuenta</a>
        </p>
      </div>
    )
  }
  return <PanelMisIndicadores token={token} onSesionTerminada={onSesionTerminada} />
}

function PanelMisIndicadores({ token, onSesionTerminada }: { token: string; onSesionTerminada: () => void }) {
  const [periodo, setPeriodo] = useState<Periodo>(() => mesEnCurso())
  const [carga, setCarga] = useState<Carga>({ estado: 'cargando' })
  const [intento, setIntento] = useState(0)
  const [erroresServidor, setErroresServidor] = useState<ErroresPeriodo>({})

  const es401 = useCallback(
    (error: unknown) => {
      if (error instanceof ApiError && error.status === 401) {
        onSesionTerminada()
        return true
      }
      return false
    },
    [onSesionTerminada],
  )

  useEffect(() => {
    const control = new AbortController()
    setCarga({ estado: 'cargando' })
    setErroresServidor({})
    obtenerIndicadoresAsesor(token, periodo, control.signal)
      .then((datos) => setCarga({ estado: 'ok', datos }))
      .catch((error: unknown) => {
        if (control.signal.aborted || es401(error)) return
        if (error instanceof ApiError && error.status === 400) {
          const { porCampo, otros } = erroresPeriodoDe(error.errores)
          setErroresServidor(porCampo)
          setCarga({ estado: 'error', aviso: { tipo: 'error', texto: [error.mensaje, ...otros].join(' ') } })
          return
        }
        setCarga({ estado: 'error', aviso: { tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO } })
      })
    return () => control.abort()
  }, [token, periodo, intento, es401])

  return (
    <div className="cat-pagina inv-pagina indicadores">
      <h1 tabIndex={-1} className="inv-titulo">
        {TITULO_MIS_INDICADORES}
      </h1>
      <p className="inv-intro">
        Tus vehículos, tus contactos y tus ventas del período elegido. Solo se cuenta lo que hoy está a tu cargo.
      </p>
      <FiltroPeriodo
        idBase="mis-indicadores"
        etiqueta="Período de mis indicadores"
        erroresServidor={erroresServidor}
        deshabilitado={carga.estado === 'cargando'}
        onAplicar={(p) => {
          setPeriodo(p)
          setIntento((i) => i + 1)
        }}
      />
      {carga.estado === 'cargando' && <p role="status">Cargando tus indicadores...</p>}
      {carga.estado === 'error' && (
        <div className="inv-estado-carga">
          <Aviso aviso={carga.aviso} />
          <button type="button" className="secondary-button inv-boton-auto" onClick={() => setIntento((i) => i + 1)}>
            Reintentar
          </button>
        </div>
      )}
      {carga.estado === 'ok' && <ContenidoMisIndicadores d={carga.datos} />}
    </div>
  )
}

function ContenidoMisIndicadores({ d }: { d: IndicadoresAsesor }) {
  return (
    <>
      <h2 className="inv-subtitulo">{textoPeriodo(d.periodo)}</h2>
      <Cifras
        titulo="Mis cifras del período"
        cifras={[
          { clave: 'recibidos', etiqueta: 'Contactos recibidos', valor: formatoMiles(d.contactosRecibidos) },
          { clave: 'abiertos', etiqueta: 'De esos, siguen abiertos', valor: formatoMiles(d.contactosAbiertos) },
          { clave: 'visibles', etiqueta: 'Publicaciones visibles hoy', valor: formatoMiles(d.publicacionesVisiblesHoy) },
          { clave: 'ventas', etiqueta: 'Ventas vigentes', valor: formatoMiles(d.ventasVigentes.cantidad) },
          { clave: 'valor', etiqueta: 'Valor vendido', valor: formatoCop(d.ventasVigentes.valorCop) },
        ]}
      />
      <div className="reporte-tablas">
        <TablaDatos
          id="mis-indicadores-estado"
          titulo="Mis vehículos por estado (hoy)"
          columnas={['Estado', 'Vehículos']}
          filas={ESTADOS_INVENTARIO.map((e) => [nombreEstado(e), formatoMiles(d.vehiculosPorEstado[e])])}
        />
        <TablaDatos
          id="mis-indicadores-resultado"
          titulo="Mis contactos cerrados en el período por resultado"
          columnas={['Resultado', 'Contactos']}
          filas={d.contactosPorResultado.map((r) => [nombreResultado(r.resultado), formatoMiles(r.total)])}
          textoVacio="Sin contactos cerrados en el período."
        />
      </div>
    </>
  )
}
