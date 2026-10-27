import { useState } from 'react'

type ImagenVehiculoProps = {
  src: string | null
  /** Texto alternativo; vacío si la imagen es decorativa (p. ej. una miniatura con etiqueta propia). */
  alt: string
  className?: string
}

/** Imagen de reemplazo en SVG (sin archivos externos) cuando no hay foto o no carga. */
function ImagenReemplazo({ alt, className }: { alt: string; className?: string }) {
  const decorativa = alt === ''
  return (
    <svg
      className={['imagen-reemplazo', className].filter(Boolean).join(' ')}
      viewBox="0 0 160 100"
      preserveAspectRatio="xMidYMid slice"
      role={decorativa ? undefined : 'img'}
      aria-label={decorativa ? undefined : alt}
      aria-hidden={decorativa ? true : undefined}
      focusable="false"
    >
      <rect width="160" height="100" fill="#e2e8f0" />
      <g fill="#94a3b8">
        <rect x="34" y="34" width="62" height="30" rx="3" />
        <path d="M98 42h16l12 12v10H98z" />
        <circle cx="54" cy="68" r="7" />
        <circle cx="110" cy="68" r="7" />
      </g>
      <text x="80" y="88" textAnchor="middle" fontSize="10" fill="#475569" fontFamily="system-ui, sans-serif">
        Sin foto
      </text>
    </svg>
  )
}

/**
 * Foto del vehículo. Si `src` es null o la carga falla (onError), muestra la
 * imagen de reemplazo con el mismo texto alternativo.
 */
export function ImagenVehiculo({ src, alt, className }: ImagenVehiculoProps) {
  const [fallida, setFallida] = useState<string | null>(null)
  if (!src || fallida === src) {
    return (
      <ImagenReemplazo alt={alt === '' ? '' : 'Sin foto disponible'} className={className} />
    )
  }
  return (
    <img
      className={className}
      src={src}
      alt={alt}
      loading="lazy"
      decoding="async"
      onError={() => setFallida(src)}
    />
  )
}
