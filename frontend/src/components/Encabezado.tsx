import { useState, type FormEvent, type MouseEvent } from 'react'
import { categoriasDisponibles, Q_MAX, type OpcionCatalogo } from '../api/catalogo'
import { HASH_CUENTA, HASH_INICIO, HASH_REGISTRO, hashBuscar, navegar, type Ruta } from '../rutas'
import { ALT_LOGO_HORIZONTAL, LOGO } from '../marca'
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

/**
 * P10 (CA-P10-02, 03, 06; D-87, D-91 A): logo horizontal de VCA Automotores en el encabezado blanco, con WebP y
 * respaldo PNG. `width`/`height` reservan el espacio (sin saltos al cargar); el CSS lo muestra a 56 px de alto
 * (36 px en móvil). Su texto alternativo es el nombre accesible del enlace a la portada.
 */
export function LogoEncabezado() {
  return (
    <a className="cat-logo cat-logo-imagen" href={HASH_INICIO}>
      <picture>
        <source srcSet={LOGO.horizontalWebp} type="image/webp" />
        <img
          src={LOGO.horizontalPng}
          alt={ALT_LOGO_HORIZONTAL}
          width={LOGO.horizontalAncho / 2}
          height={LOGO.horizontalAlto / 2}
          decoding="async"
        />
      </picture>
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
        <LogoEncabezado />
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
