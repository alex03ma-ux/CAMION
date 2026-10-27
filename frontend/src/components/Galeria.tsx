import { useRef, useState, type KeyboardEvent } from 'react'
import type { FotoVehiculo } from '../api/catalogo'
import { ImagenVehiculo } from './ImagenVehiculo'

type GaleriaProps = {
  fotos: readonly FotoVehiculo[]
  titulo: string
}

/**
 * Galería accesible: foto principal y miniaturas como botones. La miniatura
 * activa lleva aria-current; las flechas, Inicio y Fin recorren las fotos.
 */
export function Galeria({ fotos, titulo }: GaleriaProps) {
  const inicial = Math.max(
    0,
    fotos.findIndex((f) => f.esPortada),
  )
  const [indice, setIndice] = useState(inicial)
  const miniaturas = useRef<(HTMLButtonElement | null)[]>([])
  const total = fotos.length

  if (total === 0) {
    return (
      <div className="cat-galeria">
        <div className="cat-galeria-principal">
          <ImagenVehiculo src={null} alt={`Foto de ${titulo}`} />
        </div>
      </div>
    )
  }

  const actual = Math.min(indice, total - 1)
  const foto = fotos[actual]

  const ir = (nuevo: number, enfocar: boolean) => {
    const siguiente = (nuevo + total) % total
    setIndice(siguiente)
    if (enfocar) miniaturas.current[siguiente]?.focus()
  }

  function alPulsarTecla(evento: KeyboardEvent<HTMLButtonElement>) {
    const mapa: Record<string, number | undefined> = {
      ArrowRight: actual + 1,
      ArrowDown: actual + 1,
      ArrowLeft: actual - 1,
      ArrowUp: actual - 1,
      Home: 0,
      End: total - 1,
    }
    const destino = mapa[evento.key]
    if (destino === undefined) return
    evento.preventDefault()
    ir(destino, true)
  }

  return (
    <div className="cat-galeria" role="group" aria-label={`Fotos de ${titulo}`}>
      <figure className="cat-galeria-principal">
        <ImagenVehiculo
          key={foto.url}
          src={foto.url}
          alt={`${titulo}, foto ${actual + 1} de ${total}`}
        />
        {total > 1 && (
          <figcaption aria-live="polite">
            Foto {actual + 1} de {total}
          </figcaption>
        )}
      </figure>

      {total > 1 && (
        <>
          <div className="cat-galeria-flechas">
            <button
              type="button"
              className="cat-boton secondary-button"
              onClick={() => ir(actual - 1, false)}
            >
              Foto anterior
            </button>
            <button
              type="button"
              className="cat-boton secondary-button"
              onClick={() => ir(actual + 1, false)}
            >
              Foto siguiente
            </button>
          </div>
          <ul className="cat-miniaturas" aria-label="Miniaturas">
            {fotos.map((f, i) => (
              <li key={`${f.url}-${i}`}>
                <button
                  type="button"
                  ref={(el) => {
                    miniaturas.current[i] = el
                  }}
                  className="cat-miniatura"
                  aria-label={`Ver foto ${i + 1} de ${total}`}
                  aria-current={i === actual ? 'true' : undefined}
                  tabIndex={i === actual ? 0 : -1}
                  onClick={() => ir(i, false)}
                  onKeyDown={alPulsarTecla}
                >
                  <ImagenVehiculo src={f.url} alt="" />
                </button>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  )
}
