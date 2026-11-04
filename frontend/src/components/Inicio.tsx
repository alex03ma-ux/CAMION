import { useCallback, useState, type FormEvent, type ReactNode } from 'react'
import {
  categoriasDisponibles,
  MENSAJE_SIN_VEHICULOS,
  obtenerInicio,
  Q_MAX,
  type OpcionCatalogo,
} from '../api/catalogo'
import { usePeticion } from '../catalogo/usePeticion'
import { HASH_BUSCAR, HASH_REGISTRO, hashBuscar, navegar } from '../rutas'
import { Aviso } from './Aviso'
import {
  IconoAsesoria,
  IconoCamion,
  IconoCarro,
  IconoCarroceria,
  IconoCredito,
  IconoDocumento,
  IconoEstrella,
  IconoFlecha,
  IconoLupa,
  IconoMaquinaria,
  IconoMonedas,
  IconoRevisado,
  IlustracionCamion,
} from './IconosPortal'
import { RejillaVehiculos } from './TarjetaVehiculo'

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
 * Franja de confianza. Textos de la referencia aprobada por la responsable; su contenido comercial
 * queda pendiente de confirmar (D-42) y se cambia solo aquí.
 */
const CONFIANZA: { texto: string; icono: ReactNode }[] = [
  { texto: 'Vehículos revisados', icono: <IconoRevisado /> },
  { texto: 'Papeles al día', icono: <IconoDocumento /> },
  { texto: 'Financiación', icono: <IconoMonedas /> },
  { texto: 'Asesoría', icono: <IconoAsesoria /> },
]

/** Portal principal (HU-001): portada con búsqueda, categorías, destacados (HU-003) y recientes (HU-002). */
export function Inicio({ tipos }: InicioProps) {
  const cargar = useCallback((signal: AbortSignal) => obtenerInicio(signal), [])
  const [estado, reintentar] = usePeticion(cargar, 'inicio')

  const categorias = categoriasDisponibles(tipos)

  return (
    <div className="cat-inicio">
      <section className="cat-portada" aria-labelledby="inicio-titulo">
        <IlustracionCamion className="cat-portada-imagen" />
        <div className="cat-portada-interior">
          <p className="cat-portada-eyebrow">Camiones, carros, carrocerías y maquinaria</p>
          <h1 id="inicio-titulo" tabIndex={-1}>
            Tu próximo negocio empieza con el <span className="texto-rojo">vehículo correcto</span>
          </h1>
          <p className="cat-portada-texto">Compra con respaldo y te ayudamos con la financiación.</p>
          <BusquedaPortada categorias={categorias} />
        </div>
      </section>

      <div className="cat-pagina">
        <ul className="cat-categorias" aria-label="Categorías">
          {categorias.map(({ tipo, enlace }) => (
            <li key={tipo.id}>
              <a
                className={enlace === 'Camiones' ? 'cat-categoria cat-categoria-principal' : 'cat-categoria'}
                href={hashBuscar({ tipoId: String(tipo.id) })}
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
              <a className="cat-acceso" href={HASH_BUSCAR}>
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
              <a className="cat-ver-todos" href={HASH_BUSCAR}>
                Ver todos <IconoFlecha />
              </a>
            </div>
            <RejillaVehiculos vehiculos={estado.datos.destacados} />
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

        <ul className="cat-confianza" aria-label="Por qué comprar aquí">
          {CONFIANZA.map(({ texto, icono }) => (
            <li key={texto}>
              {icono}
              <span>{texto}</span>
            </li>
          ))}
        </ul>

        <section className="cat-credito" aria-labelledby="inicio-credito">
          <IconoCredito className="cat-credito-icono" />
          <div className="cat-credito-texto">
            <h2 id="inicio-credito">¿Necesitas crédito para tu camión?</h2>
            <p>Te ayudamos a encontrar el plan de pagos ideal.</p>
            {/* La solicitud de crédito llega en P5; mientras tanto, el registro (D-42). */}
            <a className="cat-credito-boton" href={HASH_REGISTRO}>
              Quiero un crédito <IconoFlecha />
            </a>
          </div>
        </section>
      </div>
    </div>
  )
}

/** Buscador grande de la portada: texto libre y tipo de vehículo. */
function BusquedaPortada({ categorias }: { categorias: { tipo: OpcionCatalogo; enlace: string }[] }) {
  const [q, setQ] = useState('')
  const [tipoId, setTipoId] = useState('')
  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    navegar(hashBuscar({ q: q.trim(), tipoId }))
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
