import { useCallback, useEffect, useId, useRef, useState } from 'react'
import type { TarjetaVehiculo as DatosTarjeta } from '../api/catalogo'
import { IconoFlecha } from './IconosPortal'
import { TarjetaVehiculo } from './TarjetaVehiculo'

type Extremos = { cabenTodas: boolean; alInicio: boolean; alFinal: boolean }

const SIN_DESPLAZAMIENTO: Extremos = { cabenTodas: true, alInicio: true, alFinal: true }

/** Margen (px) para redondeos del navegador al comparar posiciones. */
const TOLERANCIA = 1

function medir(pista: HTMLElement): Extremos {
  const maximo = pista.scrollWidth - pista.clientWidth
  return {
    cabenTodas: maximo <= TOLERANCIA,
    alInicio: pista.scrollLeft <= TOLERANCIA,
    alFinal: pista.scrollLeft >= maximo - TOLERANCIA,
  }
}

function sinAnimacion(): boolean {
  return typeof window.matchMedia === 'function' && window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

/**
 * D-93 (3a): carrusel horizontal de destacados, sin dependencias.
 * - Desplazamiento nativo con scroll-snap: gesto táctil, rueda y teclado funcionan como siempre (no se capturan las
 *   flechas del teclado ni se bloquea el desplazamiento vertical). Las tarjetas se recorren con Tab y el navegador
 *   lleva a la vista la que recibe el foco.
 * - Cuántas se ven lo decide el ancho del propio carrusel (consultas de contenedor en portal.css), con el mismo
 *   ancho mínimo de tarjeta que la rejilla de Recientes. Pocas tarjetas no se estiran.
 * - Las flechas avanzan exactamente una tarjeta (ancho de la tarjeta + separación); se desactivan en los extremos
 *   (aria-disabled, para no perder el foco) y se ocultan si caben todas. Sin animación con prefers-reduced-motion.
 * - No asume cuántos destacados hay: muestra los que mande la API.
 */
export function CarruselVehiculos({ vehiculos }: { vehiculos: readonly DatosTarjeta[] }) {
  const idPista = useId()
  const pista = useRef<HTMLUListElement>(null)
  const [extremos, setExtremos] = useState<Extremos>(SIN_DESPLAZAMIENTO)

  const actualizar = useCallback(() => {
    if (pista.current) setExtremos(medir(pista.current))
  }, [])

  useEffect(() => {
    const elemento = pista.current
    if (!elemento) return
    actualizar()
    elemento.addEventListener('scroll', actualizar, { passive: true })
    const observador = typeof ResizeObserver === 'function' ? new ResizeObserver(actualizar) : null
    observador?.observe(elemento)
    window.addEventListener('resize', actualizar)
    return () => {
      elemento.removeEventListener('scroll', actualizar)
      observador?.disconnect()
      window.removeEventListener('resize', actualizar)
    }
  }, [actualizar, vehiculos])

  function mover(sentido: 1 | -1) {
    const elemento = pista.current
    if (!elemento) return
    if (sentido === -1 ? extremos.alInicio : extremos.alFinal) return
    const tarjeta = elemento.querySelector('li')
    if (!tarjeta) return
    const separacion = Number.parseFloat(window.getComputedStyle(elemento).columnGap) || 0
    elemento.scrollBy({ left: sentido * (tarjeta.offsetWidth + separacion), behavior: sinAnimacion() ? 'auto' : 'smooth' })
  }

  return (
    <div className="cat-carrusel">
      <div className="cat-carrusel-controles" hidden={extremos.cabenTodas}>
        <button
          type="button"
          className="cat-carrusel-flecha cat-carrusel-anterior"
          aria-label="Destacado anterior"
          aria-controls={idPista}
          aria-disabled={extremos.alInicio}
          onClick={() => mover(-1)}
        >
          <IconoFlecha />
        </button>
        <button
          type="button"
          className="cat-carrusel-flecha"
          aria-label="Destacado siguiente"
          aria-controls={idPista}
          aria-disabled={extremos.alFinal}
          onClick={() => mover(1)}
        >
          <IconoFlecha />
        </button>
      </div>
      <div className="cat-carrusel-marco" role="region" aria-label="Vehículos destacados">
        <ul className="cat-carrusel-pista" id={idPista} ref={pista}>
          {vehiculos.map((v) => (
            <li key={v.slug}>
              <TarjetaVehiculo vehiculo={v} />
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}
