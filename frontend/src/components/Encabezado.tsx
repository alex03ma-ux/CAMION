import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent, type MouseEvent, type RefObject } from 'react'
import { categoriasDisponibles, Q_MAX, type OpcionCatalogo } from '../api/catalogo'
import { RUTA_CUENTA, RUTA_INICIO, RUTA_REGISTRO, navegar, rutaBuscar, rutaCategoria, type Ruta } from '../rutas'
import { ALT_LOGO_HORIZONTAL, LOGO } from '../marca'
import { IconoLupa, IconoMenu, IconoUsuario } from './IconosPortal'

type EncabezadoProps = {
  ruta: Ruta
  /** Tipos de vehículo de /api/catalogo/filtros; vacío si aún no cargan o fallaron. */
  tipos: readonly OpcionCatalogo[]
  /** true si hay una sesión guardada (se muestra "Mi cuenta"). */
  conSesion: boolean
}
/** Mueve el foco al contenido sin cambiar de pantalla ni añadir `#contenido` a la dirección (CA-P10-45). */
/** Mueve el foco al contenido sin cambiar el hash (el hash es la ruta). */
function saltarAlContenido(evento: MouseEvent<HTMLAnchorElement>) {
  evento.preventDefault()
  const principal = document.getElementById('contenido')
  principal?.focus()
}

/**
 * P10 (CA-P10-02, 03, 06; D-87, D-91 A): logo horizontal de VC Automotores (el dibujo dice "VCA") en el encabezado blanco, con WebP y
 * respaldo PNG. `width`/`height` reservan el espacio (sin saltos al cargar); el CSS lo muestra a 56 px de alto
 * (36 px en móvil). Su texto alternativo es el nombre accesible del enlace a la portada.
 */
export function LogoEncabezado() {
  return (
    <a className="cat-logo cat-logo-imagen" href={RUTA_INICIO}>
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

/** id de la navegación principal: el botón del menú la controla (aria-controls). */
export const ID_MENU_PRINCIPAL = 'menu-principal'

/** id del buscador del encabezado: la lupa lo controla (aria-controls). */
export const ID_BUSCADOR_ENCABEZADO = 'buscador-encabezado'

const ID_CAMPO_BUSQUEDA = 'busqueda-encabezado'

/**
 * D-93 (3a): alto real del encabezado fijo en `--alto-encabezado`, para `scroll-padding-top` (anclas, foco y
 * "Saltar al contenido" no quedan debajo del encabezado). Se actualiza si cambia de alto (menú abierto, giro).
 */
function useAltoEncabezado(encabezado: RefObject<HTMLElement | null>) {
  useEffect(() => {
    const elemento = encabezado.current
    if (!elemento) return
    const raiz = document.documentElement
    const fijar = () => raiz.style.setProperty('--alto-encabezado', `${Math.round(elemento.getBoundingClientRect().height)}px`)
    fijar()
    if (typeof ResizeObserver !== 'function') return
    const observador = new ResizeObserver(fijar)
    observador.observe(elemento)
    return () => observador.disconnect()
  }, [encabezado])
}

/**
 * Encabezado común (P3, rediseño rojo/negro/blanco): logo, Inicio y las categorías, y el acceso a la
 * cuenta. En el inicio la búsqueda está en la portada; en las demás pantallas, aquí.
 *
 * Por debajo de 1100 px (68.75rem) la navegación se pliega tras el botón "Menú" (patrón de botón que
 * despliega contenido, sin trampa de foco): el CSS la oculta solo en ese ancho y solo si está cerrada.
 * Esc la cierra y devuelve el foco al botón; también se cierra al pulsar un enlace o al cambiar de ruta.
 * El buscador del encabezado queda fuera del menú.
 *
 * D-93 (3a): el encabezado queda fijo arriba al desplazarse, siempre con el mismo alto. El buscador se pliega tras
 * la lupa (aria-expanded/aria-controls; al abrir, el foco va al campo; Esc lo cierra y devuelve el foco a la lupa).
 * Fuera del inicio está siempre en la página y el CSS lo oculta si está cerrado; en el inicio (cuyo buscador está en
 * la portada) solo se monta al abrirlo con la lupa, que en escritorio no aparece. Abrir el menú cierra el buscador
 * y al revés.
 */
export function Encabezado({ ruta, tipos, conSesion }: EncabezadoProps) {
  const qActual = ruta.nombre === 'buscar' ? ruta.filtros.q : ''
  const tipoActual = ruta.nombre === 'buscar' ? ruta.filtros.tipoId : ''
  const enInicio = ruta.nombre === 'inicio'
  const [abierto, setAbierto] = useState(false)
  const [buscando, setBuscando] = useState(false)
  const boton = useRef<HTMLButtonElement>(null)
  const lupa = useRef<HTMLButtonElement>(null)
  const encabezado = useRef<HTMLElement>(null)
  const enfocarCampo = useRef(false)
  useAltoEncabezado(encabezado)
  // Al cambiar de ruta el menú se cierra (ajuste durante el render, sin efecto). La ruta se recalcula en
  // cada render de App, así que se compara por su contenido y no por identidad.
  const claveRuta = JSON.stringify(ruta)
  const [rutaMenu, setRutaMenu] = useState(claveRuta)
  if (rutaMenu !== claveRuta) {
    setRutaMenu(claveRuta)
    setAbierto(false)
    setBuscando(false)
  }

  // Al abrir el buscador con la lupa, el foco va al campo (cuando ya está visible).
  useEffect(() => {
    if (!buscando || !enfocarCampo.current) return
    enfocarCampo.current = false
    document.getElementById(ID_CAMPO_BUSQUEDA)?.focus()
  }, [buscando])

  function alternarBuscador() {
    if (!buscando) enfocarCampo.current = true
    setBuscando((b) => !b)
    setAbierto(false)
  }

  function alternarMenu() {
    setAbierto((a) => !a)
    setBuscando(false)
  }

  function cerrarBuscador() {
    setBuscando(false)
    lupa.current?.focus()
  }

  function alTeclear(evento: KeyboardEvent<HTMLElement>) {
    if (evento.key === 'Escape' && abierto) {
      evento.preventDefault()
      setAbierto(false)
      boton.current?.focus()
    }
  }

  function alPulsarEnMenu(evento: MouseEvent<HTMLElement>) {
    if (evento.target instanceof Element && evento.target.closest('a')) setAbierto(false)
  }

  return (
    <header className="cat-encabezado" ref={encabezado} data-inicio={enInicio ? 'true' : undefined}>
      <a className="saltar-contenido" href="#contenido" onClick={saltarAlContenido}>
        Saltar al contenido
      </a>
      <div className="cat-encabezado-interior">
        <LogoEncabezado />
        <button
          ref={lupa}
          type="button"
          className="cat-lupa-boton"
          aria-label="Buscar"
          aria-expanded={buscando}
          aria-controls={ID_BUSCADOR_ENCABEZADO}
          onClick={alternarBuscador}
          onKeyDown={(e) => {
            if (e.key === 'Escape' && buscando) {
              e.preventDefault()
              cerrarBuscador()
            }
          }}
        >
          <IconoLupa />
        </button>
        <button
          ref={boton}
          type="button"
          className="cat-menu-boton"
          aria-expanded={abierto}
          aria-controls={ID_MENU_PRINCIPAL}
          onClick={alternarMenu}
          onKeyDown={alTeclear}
        >
          <IconoMenu />
          {/* Por debajo de 400 px el texto se oculta solo a la vista; sigue siendo el nombre accesible. */}
          <span className="cat-menu-texto">Menú</span>
        </button>
        <nav
          id={ID_MENU_PRINCIPAL}
          className="cat-nav"
          aria-label="Principal"
          data-abierto={abierto ? 'true' : 'false'}
          onKeyDown={alTeclear}
          onClick={alPulsarEnMenu}
        >
          <ul>
            <li>
              <a href={RUTA_INICIO} aria-current={enInicio ? 'page' : undefined}>
                Inicio
              </a>
            </li>
            {categoriasDisponibles(tipos).map(({ tipo, enlace, slug }) => (
              <li key={tipo.id}>
                <a
                  href={rutaCategoria(slug)}
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
                  href={RUTA_CUENTA}
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
                    href={RUTA_REGISTRO}
                    aria-current={ruta.nombre === 'cuenta' && ruta.registro ? 'page' : undefined}
                  >
                    Registrarme
                  </a>
                </li>
                <li>
                  <a
                    className="cat-nav-cuenta"
                    href={RUTA_CUENTA}
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
        {(!enInicio || buscando) && (
          <BusquedaEncabezado key={qActual} qInicial={qActual} abierta={buscando} alCerrar={cerrarBuscador} />
        )}
      </div>
    </header>
  )
}

function BusquedaEncabezado({
  qInicial,
  abierta,
  alCerrar,
}: {
  qInicial: string
  abierta: boolean
  alCerrar: () => void
}) {
  const [q, setQ] = useState(qInicial)
  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    navegar(rutaBuscar({ q: q.trim() }))
  }
  return (
    <form
      id={ID_BUSCADOR_ENCABEZADO}
      className="cat-busqueda"
      role="search"
      data-abierta={abierta ? 'true' : 'false'}
      onSubmit={enviar}
      onKeyDown={(e) => {
        if (e.key === 'Escape' && abierta) {
          e.preventDefault()
          alCerrar()
        }
      }}
    >
      <label className="sr-only" htmlFor={ID_CAMPO_BUSQUEDA}>
        Buscar vehículos
      </label>
      <IconoLupa className="cat-busqueda-lupa" />
      <input
        id={ID_CAMPO_BUSQUEDA}
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
