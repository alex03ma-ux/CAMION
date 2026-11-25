import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from 'react'
import {
  categoriasDisponibles,
  MENSAJE_SIN_VEHICULOS,
  obtenerInicio,
  Q_MAX,
  type OpcionCatalogo,
} from '../api/catalogo'
import { obtenerPortada } from '../api/portada'
import { usePeticion } from '../catalogo/usePeticion'
import {
  RUTA_BUSCAR,
  RUTA_BUSCO_VEHICULO,
  RUTA_CREDITO,
  RUTA_SEGURO_TODO_RIESGO,
  RUTA_VENDE_TU_CAMION,
  rutaBuscar,
  rutaCategoria,
  navegar,
} from '../rutas'
import { fijarJsonLd, JSONLD_ORGANIZACION, origenActual } from '../seo'
import { jsonLdOrganizacion } from '../seoDatos'
import { Aviso } from './Aviso'
import {
  IconoCamion,
  IconoCarro,
  IconoCarroceria,
  IconoCredito,
  IconoEscudo,
  IconoEstrella,
  IconoFlecha,
  IconoLupa,
  IconoMaquinaria,
  IconoMonedas,
  IconoRevisado,
  IlustracionCamion,
} from './IconosPortal'
import { CarruselVehiculos } from './CarruselVehiculos'
import { RejillaVehiculos } from './TarjetaVehiculo'
import { useRestaurarScroll } from '../scrollPorEntrada'
import { useContactoPortal } from './useContactoPortal'

/** D-92 (decisión de la responsable, 2026-11-16): h1 de la portada. */
export const TITULO_PORTADA = 'Venta de camiones usados'

type InicioProps = {
  tipos: readonly OpcionCatalogo[]
}

const ICONO_CATEGORIA: Record<string, ReactNode> = {
  Carros: <IconoCarro />,
  Camiones: <IconoCamion />,
  Carrocerías: <IconoCarroceria />,
  Maquinaria: <IconoMaquinaria />,
}

/**
 * Franja "Nuestros servicios": lista confirmada por la responsable (D-42, 2026-11-04). Es informativa;
 * el alquiler queda fuera hasta revisar contratos y legalidad (D-43). Se cambia solo aquí.
 */
export const SERVICIOS: { texto: string; icono: ReactNode }[] = [
  { texto: 'Créditos vehiculares', icono: <IconoMonedas /> },
  { texto: 'Venta de camiones nuevos', icono: <IconoCamion /> },
  { texto: 'Venta de camiones usados peritados', icono: <IconoRevisado /> },
  { texto: 'Venta de carrocerías', icono: <IconoCarroceria /> },
  { texto: 'Venta de maquinaria amarilla', icono: <IconoMaquinaria /> },
  { texto: 'Seguros todo riesgo', icono: <IconoEscudo /> },
]

/** Portal principal (HU-001): portada con búsqueda, categorías, destacados (HU-003) y recientes (HU-002). */
export function Inicio({ tipos }: InicioProps) {
  const cargar = useCallback((signal: AbortSignal) => obtenerInicio(signal), [])
  const [estado, reintentar] = usePeticion(cargar, 'inicio')
  // D-93 (3a): al volver de la ficha, la portada vuelve a la misma posición cuando los vehículos ya están pintados.
  useRestaurarScroll(estado.estado !== 'cargando')

  const categorias = categoriasDisponibles(tipos)
  const fotoPortada = useFotoPortada()

  // P10 (CA-P10-61 a 63, D-90 C): datos de la empresa para Google, solo en la portada, con el contacto vigente.
  const contacto = useContactoPortal()
  useEffect(() => {
    fijarJsonLd(JSONLD_ORGANIZACION, jsonLdOrganizacion(contacto, origenActual()))
  }, [contacto])
  useEffect(() => () => fijarJsonLd(JSONLD_ORGANIZACION, null), [])

  return (
    <div className="cat-inicio">
      <section className="cat-portada" aria-labelledby="inicio-titulo">
        {/* Estructura (D-93): el fondo ocupa todo el ancho; dentro del contenedor común, una cuadrícula con la
            tarjeta de textos y buscador y la foto en su propio marco, alineada al mismo eje que el resto. */}
        <div className="cat-portada-interior">
          <div className="cat-portada-figura">
            {fotoPortada.url && !fotoPortada.fallida ? (
              <img
                className="cat-portada-imagen cat-portada-foto"
                src={fotoPortada.url}
                alt=""
                fetchPriority="high"
                decoding="async"
                onError={fotoPortada.alFallar}
              />
            ) : (
              <IlustracionCamion className="cat-portada-imagen" />
            )}
          </div>
          {/* D-93 (3b): en escritorio, textos y buscador van en una tarjeta blanca a la izquierda de la foto. */}
          <div className="cat-portada-tarjeta">
            <p className="cat-portada-eyebrow">Camiones, carros, carrocerías y maquinaria</p>
            {/* P10 (CA-P10-20, D-92): h1 con las palabras que se buscan; la frase de D-42 queda como subtítulo igual. */}
            <h1 id="inicio-titulo" className="cat-portada-h1" tabIndex={-1}>
              {TITULO_PORTADA}
            </h1>
            <p className="cat-portada-frase">
              Tu próximo negocio empieza con el <span className="texto-rojo">vehículo correcto</span>
            </p>
            <p className="cat-portada-texto">Compra con respaldo y te ayudamos con la financiación.</p>
            <BusquedaPortada categorias={categorias} />
          </div>
        </div>
      </section>

      <div className="cat-pagina">
        <ul className="cat-categorias" aria-label="Categorías">
          {categorias.map(({ tipo, enlace, slug }) => (
            <li key={tipo.id}>
              <a
                className={enlace === 'Camiones' ? 'cat-categoria cat-categoria-principal' : 'cat-categoria'}
                href={rutaCategoria(slug)}
                aria-label={enlace}
              >
                {ICONO_CATEGORIA[enlace]}
                <span className="cat-categoria-nombre">{enlace}</span>
                <span className="cat-categoria-ver" aria-hidden="true">
                  Ver catálogo <IconoFlecha />
                </span>
              </a>
            </li>
          ))}
        </ul>

        {estado.estado === 'cargando' && (
          <p className="cat-estado" role="status" aria-busy="true">
            Cargando vehículos...
          </p>
        )}

        {estado.estado === 'error' && (
          <div className="cat-estado">
            <Aviso aviso={{ tipo: 'error', texto: estado.error.mensaje }} />
            <button type="button" className="cat-boton" onClick={reintentar}>
              Reintentar
            </button>
          </div>
        )}

        {estado.estado === 'ok' &&
          estado.datos.destacados.length === 0 &&
          estado.datos.recientes.length === 0 && (
            <div className="cat-vacio">
              <p role="status">{MENSAJE_SIN_VEHICULOS}</p>
              <a className="cat-acceso" href={RUTA_BUSCAR}>
                Ir a la búsqueda
              </a>
            </div>
          )}

        {estado.estado === 'ok' && estado.datos.destacados.length > 0 && (
          <section className="cat-seccion" aria-labelledby="inicio-destacados">
            <div className="cat-seccion-cabecera">
              <h2 id="inicio-destacados">
                <IconoEstrella className="texto-rojo" />
                Destacados
              </h2>
              <a className="cat-ver-todos" href={RUTA_BUSCAR}>
                Ver todos <IconoFlecha />
              </a>
            </div>
            {/* D-93 (3a): carrusel horizontal; Recientes sigue en rejilla. */}
            <CarruselVehiculos vehiculos={estado.datos.destacados} />
          </section>
        )}

        {estado.estado === 'ok' && estado.datos.recientes.length > 0 && (
          <section className="cat-seccion" aria-labelledby="inicio-recientes">
            <div className="cat-seccion-cabecera">
              <h2 id="inicio-recientes">Recientes</h2>
            </div>
            <RejillaVehiculos vehiculos={estado.datos.recientes} />
          </section>
        )}

        <section className="cat-servicios" aria-labelledby="inicio-servicios">
          <h2 id="inicio-servicios">Nuestros servicios</h2>
          <ul className="cat-confianza">
            {SERVICIOS.map(({ texto, icono }) => (
              <li key={texto}>
                {icono}
                <span>{texto}</span>
              </li>
            ))}
          </ul>
        </section>

        <section className="cat-credito" aria-labelledby="inicio-credito">
          <IconoCredito className="cat-credito-icono" />
          <div className="cat-credito-texto">
            <h2 id="inicio-credito">¿Necesitas crédito para tu camión?</h2>
            <p>Te ayudamos a encontrar el plan de pagos ideal.</p>
            {/* P6 (D-42, CA-P6-02): abre el formulario de crédito, ya no el registro. */}
            <a className="cat-credito-boton" href={RUTA_CREDITO}>
              Quiero un crédito <IconoFlecha />
            </a>
          </div>
        </section>

        {/* P5b (D-51 A): accesos a los avisos junto a la banda de crédito. La franja de servicios sigue sin enlaces (D-42). */}
        <div className="cat-avisos">
          <section className="cat-aviso-bloque" aria-labelledby="inicio-vende">
            <IconoCamion className="cat-aviso-icono" />
            <div>
              <h2 id="inicio-vende">¿Quieres vender tu camión?</h2>
              <p>Cuéntanos qué vehículo tienes y te contactamos. No necesitas cuenta.</p>
              <a className="cat-aviso-boton" href={RUTA_VENDE_TU_CAMION}>
                Vende tu camión <IconoFlecha />
              </a>
            </div>
          </section>
          <section className="cat-aviso-bloque" aria-labelledby="inicio-seguro">
            <IconoEscudo className="cat-aviso-icono" />
            <div>
              <h2 id="inicio-seguro">Asegura tu vehículo todo riesgo</h2>
              <p>Déjanos tus datos y te ayudamos con tu seguro todo riesgo.</p>
              <a className="cat-aviso-boton" href={RUTA_SEGURO_TODO_RIESGO}>
                Compra tu seguro <IconoFlecha />
              </a>
            </div>
          </section>
          {/* D-56 (d) B (responsable, 2026-11-12): "Busco un vehículo" también en la portada, visible y fácil de encontrar. */}
          <section className="cat-aviso-bloque" aria-labelledby="inicio-busco">
            <IconoLupa className="cat-aviso-icono" />
            <div>
              <h2 id="inicio-busco">¿No encuentras el vehículo que buscas?</h2>
              <p>Cuéntanos qué necesitas y te contactamos con opciones. No necesitas cuenta.</p>
              <a className="cat-aviso-boton" href={RUTA_BUSCO_VEHICULO}>
                Busco un vehículo <IconoFlecha />
              </a>
            </div>
          </section>
        </div>
      </div>
    </div>
  )
}

/**
 * D-42: foto de portada elegida por el administrador. Mientras carga, si no hay o si falla, se ve la
 * ilustración: la portada nunca queda vacía.
 */
function useFotoPortada() {
  const [url, setUrl] = useState<string | null>(null)
  const [fallida, setFallida] = useState(false)
  useEffect(() => {
    const control = new AbortController()
    obtenerPortada(control.signal)
      .then((u) => setUrl(u))
      .catch(() => {
        // Sin conexión o error: se queda la ilustración.
      })
    return () => control.abort()
  }, [])
  return { url, fallida, alFallar: () => setFallida(true) }
}

/** Buscador grande de la portada: texto libre y tipo de vehículo. */
function BusquedaPortada({ categorias }: { categorias: { tipo: OpcionCatalogo; enlace: string }[] }) {
  const [q, setQ] = useState('')
  const [tipoId, setTipoId] = useState('')
  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    navegar(rutaBuscar({ q: q.trim(), tipoId }))
  }
  return (
    <form className="cat-busqueda-portada" role="search" onSubmit={enviar}>
      <label className="sr-only" htmlFor="busqueda-portada">
        Buscar vehículos
      </label>
      <IconoLupa className="cat-busqueda-lupa" />
      <input
        id="busqueda-portada"
        type="search"
        value={q}
        onChange={(e) => setQ(e.target.value)}
        maxLength={Q_MAX}
        placeholder="Marca, modelo o año (Chevrolet NHR 2022)"
        autoComplete="off"
      />
      <label className="sr-only" htmlFor="busqueda-portada-tipo">
        Tipo de vehículo
      </label>
      <select id="busqueda-portada-tipo" value={tipoId} onChange={(e) => setTipoId(e.target.value)}>
        <option value="">Todas</option>
        {categorias.map(({ tipo, enlace }) => (
          <option key={tipo.id} value={String(tipo.id)}>
            {enlace}
          </option>
        ))}
      </select>
      <button type="submit">Buscar</button>
    </form>
  )
}
