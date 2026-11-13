// P8 — Indicadores del administrador en #/cuenta/indicadores (HU-075, HU-080, RN-RE-001, RN-RE-006; D-69 A/A, D-76 A;
// CA-P8-34 a 41). Cifras del período con valores en pesos, comparadas con el período anterior de igual duración, y hasta
// cuatro gráficos de barras hechos con HTML y CSS, cada uno con su tabla equivalente. Sin visitas ni datos de navegación.
// El panel "Inicio" de P7 no cambia (D-64 a A).
import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { nombreOrigen, nombreResultado } from '../api/leads'
import {
  mesEnCurso,
  obtenerIndicadoresAdmin,
  textoDias,
  textoMes,
  textoPeriodo,
  textoPorcentaje,
  type ClaveIndicador,
  type Comparacion,
  type ErroresPeriodo,
  type IndicadoresAdmin,
  type Periodo,
} from '../api/reportes'
import { nombreOrigenVenta } from '../api/ventas'
import { formatoCop, formatoMiles } from '../catalogo/formato'
import { Aviso, type DatosAviso } from './Aviso'
import { erroresPeriodoDe, FiltroPeriodo } from './FiltroPeriodo'
import { GraficoBarras, TablaDatos } from './PiezasReporte'

type Props = { token: string; onSesionTerminada: () => void }

type Formato = 'cantidad' | 'pesos' | 'porcentaje' | 'dias'

export const INDICADORES: readonly { clave: ClaveIndicador; titulo: string; formato: Formato }[] = [
  { clave: 'contactosRecibidos', titulo: 'Contactos recibidos', formato: 'cantidad' },
  { clave: 'porcentajeVentaConcretada', titulo: 'Contactos cerrados con venta concretada', formato: 'porcentaje' },
  { clave: 'ventasVigentes', titulo: 'Ventas vigentes', formato: 'cantidad' },
  { clave: 'valorVendidoCop', titulo: 'Valor vendido', formato: 'pesos' },
  { clave: 'solicitudesAdministracion', titulo: 'Solicitudes de Administración recibidas', formato: 'cantidad' },
  { clave: 'creditosRemitidos', titulo: 'Créditos remitidos a entidad financiera', formato: 'cantidad' },
  { clave: 'ingresosPlanesCop', titulo: 'Ingresos registrados por planes', formato: 'pesos' },
  { clave: 'tiempoMedioCierreDias', titulo: 'Tiempo medio hasta el cierre', formato: 'dias' },
]

const DECIMAL = new Intl.NumberFormat('es-CO', { minimumFractionDigits: 1, maximumFractionDigits: 1 })

export function textoValor(valor: number | null, formato: Formato): string {
  if (formato === 'porcentaje') return textoPorcentaje(valor)
  if (formato === 'dias') return textoDias(valor)
  if (valor === null) return 'Sin datos'
  return formato === 'pesos' ? formatoCop(valor) : formatoMiles(valor)
}

/** "+3 frente al período anterior", "Igual que el período anterior" o "Sin datos para comparar" (CA-P8-35). */
export function textoDiferencia(c: Comparacion, formato: Formato): string {
  if (c.diferencia === null) return 'Sin datos para comparar'
  if (c.diferencia === 0) return 'Igual que el período anterior'
  const signo = c.diferencia > 0 ? '+' : '-'
  const absoluto = Math.abs(c.diferencia)
  const cifra =
    formato === 'pesos'
      ? formatoCop(absoluto)
      : formato === 'porcentaje'
        ? `${DECIMAL.format(absoluto)} puntos`
        : formato === 'dias'
          ? `${DECIMAL.format(absoluto)} ${absoluto === 1 ? 'día' : 'días'}`
          : formatoMiles(absoluto)
  return `${signo}${cifra} frente al período anterior`
}

type Carga = { estado: 'cargando' } | { estado: 'ok'; datos: IndicadoresAdmin } | { estado: 'error'; aviso: DatosAviso }

export function AdminIndicadores({ token, onSesionTerminada }: Props) {
  const [periodo, setPeriodo] = useState<Periodo>(() => mesEnCurso())
  const [carga, setCarga] = useState<Carga>({ estado: 'cargando' })
  const [intento, setIntento] = useState(0)
  const [erroresServidor, setErroresServidor] = useState<ErroresPeriodo>({})
  const titulo = useRef<HTMLHeadingElement>(null)

  useEffect(() => {
    titulo.current?.focus()
  }, [])

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
    obtenerIndicadoresAdmin(token, periodo, control.signal)
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
    <section className="panel-ancho indicadores" aria-labelledby="indicadores-titulo">
      <h2 id="indicadores-titulo" ref={titulo} tabIndex={-1}>
        Indicadores
      </h2>
      <p className="form-ayuda">
        Cifras comerciales del período elegido, comparadas con el período anterior de igual duración. Los valores en
        pesos son la suma de los precios finales y de los pagos registrados a mano.
      </p>
      <FiltroPeriodo
        idBase="indicadores"
        etiqueta="Período de los indicadores"
        erroresServidor={erroresServidor}
        deshabilitado={carga.estado === 'cargando'}
        onAplicar={(p) => {
          setPeriodo(p)
          setIntento((i) => i + 1)
        }}
      />

      {carga.estado === 'cargando' && <p role="status">Cargando los indicadores...</p>}
      {carga.estado === 'error' && (
        <div className="inv-estado-carga">
          <Aviso aviso={carga.aviso} />
          <button type="button" className="secondary-button inv-boton-auto" onClick={() => setIntento((i) => i + 1)}>
            Reintentar
          </button>
        </div>
      )}
      {carga.estado === 'ok' && <ContenidoIndicadores d={carga.datos} />}
    </section>
  )
}

function ContenidoIndicadores({ d }: { d: IndicadoresAdmin }) {
  const g = d.graficos
  return (
    <>
      <h3 className="admin-subtitulo">
        {textoPeriodo(d.periodo)}
        {d.periodoAnterior && (
          <span className="admin-total">
            {' '}
            · período anterior: {d.periodoAnterior.desde} al {d.periodoAnterior.hasta}
          </span>
        )}
      </h3>
      <ul className="panel-contadores indicadores-lista">
        {INDICADORES.map((ind) => {
          const c = d.indicadores[ind.clave]
          return (
            <li key={ind.clave} className="panel-tarjeta">
              <div className="panel-enlace panel-sin-enlace">
                <span className="panel-numero">{textoValor(c.actual, ind.formato)}</span>
                <span className="panel-etiqueta">{ind.titulo}</span>
                <span className="panel-detalle">
                  Período anterior: {textoValor(c.anterior, ind.formato)}
                  <span className="indicador-diferencia">{textoDiferencia(c, ind.formato)}</span>
                </span>
              </div>
            </li>
          )
        })}
        <li className="panel-tarjeta">
          <div className="panel-enlace panel-sin-enlace">
            <span className="panel-numero">{formatoMiles(d.publicacionesVisiblesHoy)}</span>
            <span className="panel-etiqueta">Publicaciones visibles hoy</span>
            <span className="panel-detalle">Cifra de hoy, sin comparación</span>
          </div>
        </li>
      </ul>

      <TablaDatos
        id="indicadores-administracion"
        titulo="Solicitudes de Administración recibidas por origen"
        columnas={['Origen', 'Período', 'Período anterior']}
        filas={d.solicitudesAdministracionPorOrigen.map((s) => [nombreOrigen(s.origen), formatoMiles(s.actual), formatoMiles(s.anterior)])}
      />

      <h3 className="admin-subtitulo">Gráficos</h3>
      <div className="graficos">
        <GraficoBarras
          id="grafico-ventas-mes"
          titulo="Valor vendido por mes (últimos 12 meses)"
          columnas={['Mes', 'Ventas vigentes', 'Valor vendido']}
          barras={g.ventasPorMes.map((m) => ({
            clave: m.mes,
            etiqueta: textoMes(m.mes),
            valor: m.valorCop,
            texto: `${formatoCop(m.valorCop)} (${formatoMiles(m.cantidad)})`,
          }))}
          filasTabla={g.ventasPorMes.map((m) => [textoMes(m.mes), formatoMiles(m.cantidad), formatoCop(m.valorCop)])}
        />
        <GraficoBarras
          id="grafico-ventas-origen"
          titulo="Ventas vigentes por origen"
          columnas={['Origen', 'Ventas vigentes', 'Valor vendido']}
          barras={g.ventasPorOrigen.map((o) => ({
            clave: o.origen,
            etiqueta: nombreOrigenVenta(o.origen),
            valor: o.cantidad,
            texto: `${formatoMiles(o.cantidad)} (${formatoCop(o.valorCop)})`,
          }))}
          filasTabla={g.ventasPorOrigen.map((o) => [nombreOrigenVenta(o.origen), formatoMiles(o.cantidad), formatoCop(o.valorCop)])}
        />
        <GraficoBarras
          id="grafico-solicitudes-origen"
          titulo="Solicitudes recibidas por origen"
          columnas={['Origen', 'Solicitudes']}
          barras={g.solicitudesPorOrigen.map((s) => ({
            clave: s.origen,
            etiqueta: nombreOrigen(s.origen),
            valor: s.total,
            texto: formatoMiles(s.total),
          }))}
        />
        <GraficoBarras
          id="grafico-contactos-resultado"
          titulo="Contactos cerrados por resultado"
          columnas={['Resultado', 'Contactos']}
          barras={g.contactosPorResultado.map((r) => ({
            clave: r.resultado,
            etiqueta: nombreResultado(r.resultado),
            valor: r.total,
            texto: formatoMiles(r.total),
          }))}
        />
      </div>
    </>
  )
}
