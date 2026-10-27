import { useState, type FormEvent, type MouseEvent } from 'react'
import { categoriasDisponibles, Q_MAX, type OpcionCatalogo } from '../api/catalogo'
import { HASH_CUENTA, HASH_INICIO, HASH_REGISTRO, hashBuscar, navegar, type Ruta } from '../rutas'

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

/** Encabezado común: portal, búsqueda, Carros/Camiones y acceso a la cuenta (P3). */
export function Encabezado({ ruta, tipos, conSesion }: EncabezadoProps) {
  const qActual = ruta.nombre === 'buscar' ? ruta.filtros.q : ''
  const tipoActual = ruta.nombre === 'buscar' ? ruta.filtros.tipoId : ''
  // El campo se reinicia con la búsqueda de la URL (clave del formulario).
  return (
    <header className="cat-encabezado">
      <a className="saltar-contenido" href="#contenido" onClick={saltarAlContenido}>
        Saltar al contenido
      </a>
      <div className="cat-encabezado-interior">
        <a className="cat-logo" href={HASH_INICIO}>
          <span className="cat-logo-eyebrow">Red de venta</span>
          <span className="cat-logo-nombre">Financiamos Tu Camión</span>
        </a>
        <BusquedaEncabezado key={qActual} qInicial={qActual} />
        <nav className="cat-nav" aria-label="Principal">
          <ul>
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
              <li>
                <a
                  className="cat-nav-cuenta"
                  href={HASH_CUENTA}
                  aria-current={ruta.nombre === 'cuenta' ? 'page' : undefined}
                >
                  Mi cuenta
                </a>
              </li>
            ) : (
              <>
                <li>
                  <a
                    className="cat-nav-cuenta"
                    href={HASH_CUENTA}
                    aria-current={ruta.nombre === 'cuenta' && !ruta.registro ? 'page' : undefined}
                  >
                    Iniciar sesión
                  </a>
                </li>
                <li>
                  <a
                    href={HASH_REGISTRO}
                    aria-current={ruta.nombre === 'cuenta' && ruta.registro ? 'page' : undefined}
                  >
                    Registrarme
                  </a>
                </li>
              </>
            )}
          </ul>
        </nav>
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
