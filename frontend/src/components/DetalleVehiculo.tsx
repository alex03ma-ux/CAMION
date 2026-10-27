import { useEffect } from 'react'
import {
  MENSAJE_NO_DISPONIBLE,
  mostrarKilometraje,
  obtenerDetalle,
  type DetalleVehiculo as Detalle,
} from '../api/catalogo'
import {
  formatoCc,
  formatoCop,
  formatoKg,
  formatoKm,
  textoUbicacion,
} from '../catalogo/formato'
import { usePeticion } from '../catalogo/usePeticion'
import { HASH_INICIO, hashVolverAResultados } from '../rutas'
import { Aviso } from './Aviso'
import { Galeria } from './Galeria'
import { EtiquetasVehiculo } from './TarjetaVehiculo'

/** Ficha técnica: solo los campos que tienen valor. */
function fichaTecnica(v: Detalle): { nombre: string; valor: string }[] {
  const filas: [string, string | null][] = [
    ['Categoría', v.tipo || null],
    ['Carrocería', v.carroceria],
    ['Marca', v.marca || null],
    ['Modelo', v.linea],
    ['Año', v.modeloAnio !== null ? String(v.modeloAnio) : null],
    [
      'Kilometraje',
      v.kilometraje !== null && mostrarKilometraje(v.tipo, v.kilometraje)
        ? formatoKm(v.kilometraje)
        : null,
    ],
    ['Combustible', v.combustible],
    ['Capacidad de carga', v.capacidadCargaKg !== null ? formatoKg(v.capacidadCargaKg) : null],
    ['Peso bruto', v.pesoBrutoKg !== null ? formatoKg(v.pesoBrutoKg) : null],
    ['Cilindraje', v.cilindrajeCc !== null ? formatoCc(v.cilindrajeCc) : null],
    ['Ubicación', textoUbicacion(v.municipio, v.departamento) || null],
  ]
  return filas
    .filter((fila): fila is [string, string] => fila[1] !== null && fila[1].trim() !== '')
    .map(([nombre, valor]) => ({ nombre, valor }))
}

/**
 * Detalle público de un vehículo. Solo lectura para todos (D-3): no hay
 * botones de edición ni datos del asesor (el contacto llega en P5).
 */
export function DetalleVehiculo({ slug }: { slug: string }) {
  const [estado, reintentar] = usePeticion((signal) => obtenerDetalle(slug, signal), slug)

  const noDisponible = estado.estado === 'error' && estado.error.status === 404
  const vehiculo = estado.estado === 'ok' ? estado.datos : null

  useEffect(() => {
    if (vehiculo) document.title = `${vehiculo.titulo} | Financiamos Tu Camión`
  }, [vehiculo])

  let titulo = 'Detalle del vehículo'
  if (vehiculo) titulo = vehiculo.titulo
  else if (noDisponible) titulo = MENSAJE_NO_DISPONIBLE

  const ficha = vehiculo ? fichaTecnica(vehiculo) : []

  return (
    <div className="cat-pagina cat-detalle">
      <p className="cat-volver">
        <a href={hashVolverAResultados()}>Volver a resultados</a>
      </p>

      {/* El h1 se mantiene en el mismo sitio para no perder el foco al cargar. */}
      <h1 className="cat-titulo" tabIndex={-1}>
        {titulo}
      </h1>

      {estado.estado === 'cargando' && (
        <p className="cat-estado" role="status" aria-busy="true">
          Cargando vehículo...
        </p>
      )}

      {noDisponible && (
        <div className="cat-vacio">
          <p>
            <a className="cat-acceso" href={HASH_INICIO}>
              Ir al inicio
            </a>
          </p>
        </div>
      )}

      {estado.estado === 'error' && !noDisponible && (
        <div className="cat-estado">
          <Aviso aviso={{ tipo: 'error', texto: estado.error.mensaje }} />
          <button type="button" className="cat-boton" onClick={reintentar}>
            Reintentar
          </button>
        </div>
      )}

      {vehiculo && (
        <div className="cat-detalle-cuerpo">
          <Galeria key={vehiculo.slug} fotos={vehiculo.fotos} titulo={vehiculo.titulo} />

          <div className="cat-detalle-info">
            <p className="cat-precio cat-precio-detalle">
              <span className="sr-only">Precio: </span>
              {formatoCop(vehiculo.precioCop)}
            </p>
            <EtiquetasVehiculo
              destacado={vehiculo.destacado}
              reservado={vehiculo.estadoComercial === 'RESERVADO'}
              negociable={vehiculo.negociable}
            />

            {ficha.length > 0 && (
              <section aria-labelledby="detalle-ficha">
                <h2 id="detalle-ficha">Ficha técnica</h2>
                <dl className="cat-ficha">
                  {ficha.map((fila) => (
                    <div key={fila.nombre}>
                      <dt>{fila.nombre}</dt>
                      <dd>{fila.valor}</dd>
                    </div>
                  ))}
                </dl>
              </section>
            )}
          </div>

          {(vehiculo.descripcionComercial || vehiculo.descripcionTecnica) && (
            <div className="cat-detalle-descripcion">
              {vehiculo.descripcionComercial && (
                <section aria-labelledby="detalle-descripcion">
                  <h2 id="detalle-descripcion">Descripción</h2>
                  <p className="cat-texto-largo">{vehiculo.descripcionComercial}</p>
                </section>
              )}
              {vehiculo.descripcionTecnica && (
                <section aria-labelledby="detalle-descripcion-tecnica">
                  <h2 id="detalle-descripcion-tecnica">Descripción técnica</h2>
                  <p className="cat-texto-largo">{vehiculo.descripcionTecnica}</p>
                </section>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
