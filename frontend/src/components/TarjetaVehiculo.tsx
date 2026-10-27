import { mostrarKilometraje, type TarjetaVehiculo as DatosTarjeta } from '../api/catalogo'
import { formatoCop, formatoKm, textoUbicacion } from '../catalogo/formato'
import { hashVehiculo } from '../rutas'
import { ImagenVehiculo } from './ImagenVehiculo'

/** Etiquetas "Destacado" y "Reservado": siempre con texto, no solo con color. */
export function EtiquetasVehiculo({
  destacado,
  reservado,
  negociable = false,
}: {
  destacado: boolean
  reservado: boolean
  negociable?: boolean
}) {
  if (!destacado && !reservado && !negociable) return null
  return (
    <ul className="cat-etiquetas" aria-label="Etiquetas">
      {destacado && <li className="cat-etiqueta cat-etiqueta-destacado">Destacado</li>}
      {reservado && <li className="cat-etiqueta cat-etiqueta-reservado">Reservado</li>}
      {negociable && <li className="cat-etiqueta cat-etiqueta-negociable">Negociable</li>}
    </ul>
  )
}

/**
 * Tarjeta de un vehículo (HU-001 a HU-004). Toda la tarjeta enlaza al detalle:
 * el enlace está en el título y su área se extiende a la tarjeta con CSS, así
 * el lector de pantalla anuncia solo el título.
 */
export function TarjetaVehiculo({ vehiculo }: { vehiculo: DatosTarjeta }) {
  const ubicacion = textoUbicacion(vehiculo.municipio, vehiculo.departamento)
  return (
    <article className="cat-tarjeta">
      <h3 className="cat-tarjeta-titulo">
        <a className="cat-tarjeta-enlace" href={hashVehiculo(vehiculo.slug)}>
          {vehiculo.titulo}
        </a>
      </h3>
      <EtiquetasVehiculo
        destacado={vehiculo.destacado}
        reservado={vehiculo.estadoComercial === 'RESERVADO'}
      />
      <p className="cat-precio">{formatoCop(vehiculo.precioCop)}</p>
      <ul className="cat-datos">
        {vehiculo.modeloAnio !== null && (
          <li>
            <span className="sr-only">Año: </span>
            {vehiculo.modeloAnio}
          </li>
        )}
        {vehiculo.kilometraje !== null && mostrarKilometraje(vehiculo.tipo, vehiculo.kilometraje) && (
          <li>
            <span className="sr-only">Kilometraje: </span>
            {formatoKm(vehiculo.kilometraje)}
          </li>
        )}
        {ubicacion !== '' && (
          <li>
            <span className="sr-only">Ciudad: </span>
            {ubicacion}
          </li>
        )}
      </ul>
      <div className="cat-tarjeta-foto">
        <ImagenVehiculo src={vehiculo.fotoPortada} alt={`Foto de ${vehiculo.titulo}`} />
      </div>
    </article>
  )
}

/** Rejilla responsiva de tarjetas (1, 2 y 3–4 columnas). */
export function RejillaVehiculos({ vehiculos }: { vehiculos: readonly DatosTarjeta[] }) {
  return (
    <ul className="cat-rejilla">
      {vehiculos.map((v) => (
        <li key={v.slug}>
          <TarjetaVehiculo vehiculo={v} />
        </li>
      ))}
    </ul>
  )
}
