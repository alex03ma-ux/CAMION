import { useState, type FormEvent, type MouseEvent } from 'react'
import { categoriasDisponibles, Q_MAX, type OpcionCatalogo } from '../api/catalogo'
import { HASH_CUENTA, HASH_INICIO, HASH_REGISTRO, hashBuscar, navegar, type Ruta } from '../rutas'
import { IconoLupa, IconoUsuario } from './IconosPortal'

type EncabezadoProps = {
  ruta: Ruta
  /** Tipos de vehículo de /api/catalogo/filtros; vacío si aún no cargan o fallaron. */
  tipos: readonly OpcionCatalogo[]
  /** true si hay una sesión guardada (se muestra "Mi cuenta"). */
  conSesion: boolean
}

/** Mueve el foco al contenido sin cambiar el hash (el hash es la ruta). */
function saltarAlContenido(evento: MouseEvent<HTMLAnchorElement>) {
  evento.preventDefault()
  const principal = document.getElementById('contenido')
  principal?.focus()
}

/** Logo de texto con el camión de línea (rojo, negro y blanco). */
export function LogoPortal({ claro = false }: { claro?: boolean }) {
  return (
    <a className={claro ? 'cat-logo cat-logo-claro' : 'cat-logo'} href={HASH_INICIO}>
      <span className="cat-logo-texto">
        <span className="cat-logo-eyebrow">Financiamos Tu</span>{' '}
        <span className="cat-logo-nombre">Camión</span>
      </span>
      <svg className="cat-logo-camion" viewBox="0 0 64 40" aria-hidden="true" focusable="false">
        <path
          d="M2 34h40V6H18M42 14h10l8 10v10h-6M18 34h14"
          fill="none"
          stroke="currentColor"
          strokeWidth="3"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        <circle cx="13" cy="34" r="4.5" fill="none" stroke="currentColor" strokeWidth="3" />
        <circle cx="49" cy="34" r="4.5" fill="none" stroke="currentColor" strokeWidth="3" />
      </svg>
    </a>
  )
}

/**
 * Encabezado común (P3, rediseño rojo/negro/blanco): logo, Inicio y las categorías, y el acceso a la
 * cuenta. En el inicio la búsqueda está en la portada; en las demás pantallas, aquí.
 */
export function Encabezado({ ruta, tipos, conSesion }: EncabezadoProps) {
  const qActual = ruta.nombre === 'buscar' ? ruta.filtros.q : ''
  const tipoActual = ruta.nombre === 'buscar' ? ruta.filtros.tipoId : ''
  const enInicio = ruta.nombre === 'inicio'
  return (
    <header className="cat-encabezado">
      <a className="saltar-contenido" href="#contenido" onClick={saltarAlContenido}>
        Saltar al contenido
      </a>
      <div className="cat-encabezado-interior">
        <LogoPortal />
        <nav className="cat-nav" aria-label="Principal">
          <ul>
            <li>
              <a href={HASH_INICIO} aria-current={enInicio ? 'page' : undefined}>
                Inicio
              </a>
            </li>
            {categoriasDisponibles(tipos).map(({ tipo, enlace }) => (
              <li key={tipo.id}>
                <a
                  href={hashBuscar({ tipoId: String(tipo.id) })}
                  aria-current={tipoActual === String(tipo.id) ? 'page' : undefined}
                >
                  {enlace}
                </a>
              </li>
            ))}
            {conSesion ? (
              <li className="cat-nav-acceso">
                <a
                  className="cat-nav-cuenta"
                  href={HASH_CUENTA}
                  aria-current={ruta.nombre === 'cuenta' ? 'page' : undefined}
                >
                  <IconoUsuario />
                  Mi cuenta
                </a>
              </li>
            ) : (
              <>
                <li className="cat-nav-acceso">
                  <a
                    href={HASH_REGISTRO}
                    aria-current={ruta.nombre === 'cuenta' && ruta.registro ? 'page' : undefined}
                  >
                    Registrarme
                  </a>
                </li>
                <li>
                  <a
                    className="cat-nav-cuenta"
                    href={HASH_CUENTA}
                    aria-current={ruta.nombre === 'cuenta' && !ruta.registro ? 'page' : undefined}
                  >
                    <IconoUsuario />
                    Ingresar
                  </a>
                </li>
              </>
            )}
          </ul>
        </nav>
        {!enInicio && <BusquedaEncabezado key={qActual} qInicial={qActual} />}
      </div>
    </header>
  )
}

function BusquedaEncabezado({ qInicial }: { qInicial: string }) {
  const [q, setQ] = useState(qInicial)
  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    navegar(hashBuscar({ q: q.trim() }))
  }
  return (
    <form className="cat-busqueda" role="search" onSubmit={enviar}>
      <label className="sr-only" htmlFor="busqueda-encabezado">
        Buscar vehículos
      </label>
      <IconoLupa className="cat-busqueda-lupa" />
      <input
        id="busqueda-encabezado"
        type="search"
        value={q}
        onChange={(e) => setQ(e.target.value)}
        maxLength={Q_MAX}
        placeholder="Marca, modelo o título"
        autoComplete="off"
      />
      <button type="submit">Buscar</button>
    </form>
  )
}
