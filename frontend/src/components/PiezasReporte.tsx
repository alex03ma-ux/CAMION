// P8 — Piezas comunes de Reportes, Indicadores, Mis indicadores y Actividad (CA-P8-36, 37, 73): tablas con
// desplazamiento horizontal propio, cifras en tarjetas, gráfico de barras hecho con HTML y CSS (sin librería, D-69 a A)
// con su tabla equivalente para lector de pantalla, y paginación con botones.
import type { ReactNode } from 'react'
import { formatoMiles } from '../catalogo/formato'

// ---- Tabla ----

type TablaDatosProps = {
  id: string
  titulo: string
  /** El título se ve como encabezado de la tabla; si no, solo lo lee el lector de pantalla. */
  tituloVisible?: boolean
  columnas: readonly string[]
  filas: readonly (readonly ReactNode[])[]
  /** Tablas con muchas columnas: ancho mínimo y desplazamiento horizontal dentro de su recuadro. */
  ancha?: boolean
  textoVacio?: string
  ocupada?: boolean
}

/** Tabla accesible: la primera celda de cada fila es su encabezado. */
export function TablaDatos({
  id,
  titulo,
  tituloVisible = true,
  columnas,
  filas,
  ancha = false,
  textoVacio = 'Sin datos.',
  ocupada = false,
}: TablaDatosProps) {
  const idTitulo = `${id}-titulo`
  return (
    <div
      className="tabla-contenedor reporte-tabla-contenedor"
      role="region"
      aria-labelledby={idTitulo}
      tabIndex={0}
      aria-busy={ocupada || undefined}
    >
      <table className={ancha ? 'tabla-usuarios tabla-reporte tabla-reporte-ancha' : 'tabla-usuarios tabla-reporte'}>
        <caption id={idTitulo} className={tituloVisible ? 'tabla-reporte-titulo' : 'sr-only'}>
          {titulo}
        </caption>
        <thead>
          <tr>
            {columnas.map((c) => (
              <th key={c} scope="col">
                {c}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {filas.length === 0 ? (
            <tr>
              <td colSpan={columnas.length}>{textoVacio}</td>
            </tr>
          ) : (
            filas.map((fila, i) => (
              <tr key={i}>
                {fila.map((celda, j) =>
                  j === 0 ? (
                    <th key={j} scope="row">
                      {celda}
                    </th>
                  ) : (
                    <td key={j}>{celda}</td>
                  ),
                )}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )
}

// ---- Cifras ----

export type Cifra = { clave: string; etiqueta: string; valor: string; detalle?: string }

/** Cifras en tarjetas (lista de definiciones: etiqueta y valor). */
export function Cifras({ titulo, cifras }: { titulo?: string; cifras: readonly Cifra[] }) {
  return (
    <dl className="reporte-cifras" aria-label={titulo}>
      {cifras.map((c) => (
        <div key={c.clave} className="reporte-cifra">
          <dt>{c.etiqueta}</dt>
          <dd>
            <span className="reporte-cifra-valor">{c.valor}</span>
            {c.detalle && <span className="reporte-cifra-detalle">{c.detalle}</span>}
          </dd>
        </div>
      ))}
    </dl>
  )
}

/** "1 venta" / "3 ventas". */
export function plural(n: number, uno: string, varios: string): string {
  return n === 1 ? `1 ${uno}` : `${formatoMiles(n)} ${varios}`
}

// ---- Gráfico de barras (CA-P8-36, 37) ----

export type BarraGrafico = { clave: string; etiqueta: string; valor: number; texto: string }

type GraficoBarrasProps = {
  id: string
  titulo: string
  barras: readonly BarraGrafico[]
  /** Columnas de la tabla equivalente: la primera es la etiqueta. */
  columnas: readonly string[]
  /** Filas de la tabla equivalente (por defecto: etiqueta y texto de cada barra). */
  filasTabla?: readonly (readonly ReactNode[])[]
}

/**
 * Barras horizontales con HTML y CSS. Cada barra lleva su etiqueta y su valor escritos (no depende del color); las
 * barras son decorativas para el lector de pantalla, que lee la tabla equivalente.
 */
export function GraficoBarras({ id, titulo, barras, columnas, filasTabla }: GraficoBarrasProps) {
  const maximo = Math.max(0, ...barras.map((b) => b.valor))
  const idTitulo = `${id}-titulo`
  return (
    <figure className="grafico" aria-labelledby={idTitulo}>
      <figcaption id={idTitulo} className="grafico-titulo">
        {titulo}
      </figcaption>
      {maximo === 0 && <p className="admin-vacio grafico-vacio">Sin datos en este período.</p>}
      <div className="grafico-barras" aria-hidden="true">
        {barras.map((b) => {
          const ancho = maximo === 0 ? 0 : Math.max(b.valor > 0 ? 2 : 0, Math.round((b.valor / maximo) * 100))
          return (
            <div key={b.clave} className="grafico-fila">
              <span className="grafico-etiqueta">{b.etiqueta}</span>
              <span className="grafico-pista">
                <span className="grafico-barra" style={{ width: `${ancho}%` }} />
              </span>
              <span className="grafico-valor">{b.texto}</span>
            </div>
          )
        })}
      </div>
      <table className="sr-only">
        <caption>Datos del gráfico: {titulo}</caption>
        <thead>
          <tr>
            {columnas.map((c) => (
              <th key={c} scope="col">
                {c}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {(filasTabla ?? barras.map((b) => [b.etiqueta, b.texto])).map((fila, i) => (
            <tr key={i}>
              {fila.map((celda, j) =>
                j === 0 ? (
                  <th key={j} scope="row">
                    {celda}
                  </th>
                ) : (
                  <td key={j}>{celda}</td>
                ),
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  )
}

// ---- Paginación ----

type PaginacionBotonesProps = {
  etiqueta: string
  pagina: number
  total: number
  tamano: number
  deshabilitado?: boolean
  onIr: (pagina: number) => void
}

/** Anterior / Siguiente con "Página N de M"; no se muestra si cabe en una página. */
export function PaginacionBotones({ etiqueta, pagina, total, tamano, deshabilitado = false, onIr }: PaginacionBotonesProps) {
  const totalPaginas = Math.max(1, Math.ceil(total / Math.max(1, tamano)))
  if (totalPaginas <= 1) return null
  return (
    <nav className="paginacion" aria-label={etiqueta}>
      <button
        type="button"
        className="secondary-button"
        disabled={deshabilitado || pagina <= 1}
        onClick={() => onIr(pagina - 1)}
      >
        Anterior
      </button>
      <span>
        Página {formatoMiles(pagina)} de {formatoMiles(totalPaginas)}
      </span>
      <button
        type="button"
        className="secondary-button"
        disabled={deshabilitado || pagina >= totalPaginas}
        onClick={() => onIr(pagina + 1)}
      >
        Siguiente
      </button>
    </nav>
  )
}
