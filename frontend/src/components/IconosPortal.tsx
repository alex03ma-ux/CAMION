// Iconos de línea del portal (diseño rojo, negro y blanco). SVG en línea, sin archivos externos.
// Son decorativos: siempre aria-hidden, el texto que los acompaña es el que se anuncia.
import type { ReactNode } from 'react'

type PropsIcono = { className?: string }

function Icono({ children, className, vista = '0 0 48 48' }: PropsIcono & { children: ReactNode; vista?: string }) {
  return (
    <svg
      className={['icono', className].filter(Boolean).join(' ')}
      viewBox={vista}
      fill="none"
      stroke="currentColor"
      strokeWidth={2.4}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {children}
    </svg>
  )
}

export function IconoCarro(p: PropsIcono) {
  return (
    <Icono {...p}>
      <path d="M6 30v-5l5-2 5-7h15l7 7 5 1.5V30" />
      <path d="M6 30h4m10 0h10m10 0h3" />
      <circle cx="15" cy="31" r="4" />
      <circle cx="35" cy="31" r="4" />
      <path d="M17 23h20" />
    </Icono>
  )
}

export function IconoCamion(p: PropsIcono) {
  return (
    <Icono {...p}>
      <rect x="4" y="12" width="24" height="18" rx="1.5" />
      <path d="M28 18h8l6 7v5h-14z" />
      <path d="M31 21h4l3 4h-7z" />
      <circle cx="13" cy="33" r="4" />
      <circle cx="35" cy="33" r="4" />
    </Icono>
  )
}

export function IconoCarroceria(p: PropsIcono) {
  return (
    <Icono {...p}>
      <rect x="4" y="11" width="25" height="19" />
      <path d="M11 11v19M18 11v19" />
      <path d="M29 17h7l6 7v6H29z" />
      <circle cx="12" cy="33" r="4" />
      <circle cx="35" cy="33" r="4" />
    </Icono>
  )
}

export function IconoMaquinaria(p: PropsIcono) {
  return (
    <Icono {...p}>
      <path d="M5 36h22" />
      <rect x="6" y="30" width="20" height="6" rx="3" />
      <path d="M9 30v-8h9l3 8" />
      <path d="M18 22l9-12 11 6" />
      <path d="M38 16l2 8-7 2" />
      <path d="M33 26l6-1 3 8H31z" />
    </Icono>
  )
}

export function IconoLupa(p: PropsIcono) {
  return (
    <Icono {...p} vista="0 0 24 24">
      <circle cx="10.5" cy="10.5" r="6.5" />
      <path d="M15.5 15.5 21 21" />
    </Icono>
  )
}

export function IconoUsuario(p: PropsIcono) {
  return (
    <svg className={['icono', p.className].filter(Boolean).join(' ')} viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" focusable="false">
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21c0-4.4 3.6-7 8-7s8 2.6 8 7z" />
    </svg>
  )
}

export function IconoEstrella(p: PropsIcono) {
  return (
    <svg className={['icono', p.className].filter(Boolean).join(' ')} viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" focusable="false">
      <path d="m12 2.5 2.9 6.1 6.6.8-4.9 4.6 1.3 6.6L12 17.3l-5.9 3.3 1.3-6.6-4.9-4.6 6.6-.8z" />
    </svg>
  )
}

export function IconoFlecha(p: PropsIcono) {
  return (
    <Icono {...p} vista="0 0 24 24">
      <path d="m9 6 6 6-6 6" />
    </Icono>
  )
}

export function IconoRevisado(p: PropsIcono) {
  return (
    <Icono {...p}>
      <rect x="10" y="8" width="28" height="34" rx="3" />
      <path d="M18 8V5h12v3" />
      <path d="m17 26 5 5 10-11" />
    </Icono>
  )
}

export function IconoEscudo(p: PropsIcono) {
  return (
    <Icono {...p}>
      <path d="M24 5 9 11v11c0 10 6.5 17 15 21 8.5-4 15-11 15-21V11Z" />
      <path d="m17 24 5 5 9-10" />
    </Icono>
  )
}

export function IconoDocumento(p: PropsIcono) {
  return (
    <Icono {...p}>
      <path d="M12 5h17l9 9v29H12z" />
      <path d="M29 5v9h9" />
      <path d="M18 22h14M18 29h14M18 36h10" />
    </Icono>
  )
}

export function IconoMonedas(p: PropsIcono) {
  return (
    <Icono {...p}>
      <ellipse cx="24" cy="11" rx="13" ry="5" />
      <path d="M11 11v7c0 2.8 5.8 5 13 5s13-2.2 13-5v-7" />
      <path d="M11 18v7c0 2.8 5.8 5 13 5s13-2.2 13-5v-7" />
      <path d="M11 25v7c0 2.8 5.8 5 13 5s13-2.2 13-5v-7" />
    </Icono>
  )
}

export function IconoAsesoria(p: PropsIcono) {
  return (
    <Icono {...p}>
      <path d="M9 27v-4a15 15 0 0 1 30 0v4" />
      <rect x="6" y="25" width="7" height="11" rx="3" />
      <rect x="35" y="25" width="7" height="11" rx="3" />
      <path d="M38 36c0 4-4 6-10 6h-3" />
    </Icono>
  )
}

export function IconoCredito(p: PropsIcono) {
  return (
    <Icono {...p} vista="0 0 64 64">
      <circle cx="36" cy="18" r="12" />
      <path d="M40 13.5c-1-1.5-2.5-2-4-2-2.5 0-4 1.3-4 3s1.5 2.5 4 3 4 1.4 4 3.2-1.6 3-4 3c-1.7 0-3.3-.6-4.3-2M36 9v2.5M36 24.5V27" />
      <path d="M4 40h8v18H4z" />
      <path d="M12 43h10c3 0 5 2 5 4h8l14-6c2.5-1 4.5 1.5 2.5 3.5L40 54c-2 1.5-4 2-7 2H12" />
      <path d="M27 47H20" />
    </Icono>
  )
}

/**
 * Ilustración de la portada: camión blanco de estacas sobre una carretera al atardecer.
 * Decorativa; cuando la responsable entregue una foto con licencia de uso, se reemplaza por ella.
 */
export function IlustracionCamion({ className }: PropsIcono) {
  return (
    <svg
      className={className}
      viewBox="0 0 640 360"
      preserveAspectRatio="xMaxYMax slice"
      aria-hidden="true"
      focusable="false"
    >
      <defs>
        <linearGradient id="ic-cielo" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#f4f1ee" />
          <stop offset="0.65" stopColor="#f7e3cf" />
          <stop offset="1" stopColor="#efe7e0" />
        </linearGradient>
        <linearGradient id="ic-carretera" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#7c7c7c" />
          <stop offset="1" stopColor="#3a3a3a" />
        </linearGradient>
        <linearGradient id="ic-cabina" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#ffffff" />
          <stop offset="1" stopColor="#d9dde2" />
        </linearGradient>
      </defs>
      <rect width="640" height="360" fill="url(#ic-cielo)" />
      <circle cx="170" cy="190" r="46" fill="#ffd9a8" opacity="0.7" />
      <path d="M0 230 90 170l60 30 70-60 90 70 70-40 90 50 80-45 90 55v60H0z" fill="#b9b4b2" opacity="0.55" />
      <path d="M0 250 120 205l80 25 90-45 100 55 90-30 160 40v40H0z" fill="#8f8a88" opacity="0.6" />
      <path d="M0 290 640 262v98H0z" fill="url(#ic-carretera)" />
      <path d="M20 330 640 300" stroke="#f2f2f2" strokeWidth="4" strokeDasharray="38 26" />
      <path d="M420 360 640 210v150z" fill="#d71920" />
      <path d="M470 360 640 250v24L520 360z" fill="#8f0d12" />
      {/* Carrocería de estacas */}
      <g>
        <rect x="300" y="96" width="250" height="150" rx="4" fill="#e6e8eb" stroke="#9aa0a6" strokeWidth="3" />
        {Array.from({ length: 11 }, (_, i) => (
          <rect key={i} x={312 + i * 22} y="104" width="6" height="134" fill="#b5bbc2" />
        ))}
        <path d="M300 140h250M300 190h250" stroke="#9aa0a6" strokeWidth="4" />
      </g>
      {/* Cabina */}
      <path d="M170 128c0-14 10-24 24-24h86c12 0 20 8 20 20v136H170z" fill="url(#ic-cabina)" stroke="#9aa0a6" strokeWidth="3" />
      <path d="M182 128c0-8 6-14 14-14h78c8 0 14 6 14 14v44H182z" fill="#3b4148" />
      <path d="M190 124h60l-30 40h-30z" fill="#5c646d" opacity="0.6" />
      <rect x="160" y="150" width="10" height="34" rx="3" fill="#2b2f33" />
      <rect x="180" y="200" width="108" height="34" rx="4" fill="#cfd4d9" />
      {Array.from({ length: 5 }, (_, i) => (
        <rect key={i} x="188" y={205 + i * 6} width="92" height="3" fill="#8d949b" />
      ))}
      <rect x="176" y="184" width="22" height="14" rx="3" fill="#fff8e1" stroke="#9aa0a6" />
      <rect x="270" y="184" width="22" height="14" rx="3" fill="#fff8e1" stroke="#9aa0a6" />
      <rect x="168" y="236" width="380" height="16" rx="3" fill="#2b2f33" />
      {/* Ruedas */}
      {[222, 430, 500].map((x) => (
        <g key={x}>
          <circle cx={x} cy="262" r="30" fill="#1b1b1b" />
          <circle cx={x} cy="262" r="15" fill="#9aa0a6" />
          <circle cx={x} cy="262" r="5" fill="#3b4148" />
        </g>
      ))}
    </svg>
  )
}
