// P9 (RN-SE-005; CA-P9-48, 50): pantalla para rutas `/...` que no existen. Pública, con encabezado y pie normales.
// El título del documento y el noindex los pone App según la ruta.
import { RUTA_BUSCAR, RUTA_INICIO } from '../rutas'

export const TITULO_NO_ENCONTRADA = 'Página no encontrada'

export function NoEncontrada() {
  return (
    <div className="cat-pagina no-encontrada">
      <h1 className="cat-titulo" tabIndex={-1}>
        {TITULO_NO_ENCONTRADA}
      </h1>
      <div className="privacidad-cuerpo">
        <p>La página que buscas no existe o cambió de dirección.</p>
        <ul className="no-encontrada-enlaces">
          <li>
            <a href={RUTA_INICIO}>Ir al inicio</a>
          </li>
          <li>
            <a href={RUTA_BUSCAR}>Ver todos los vehículos</a>
          </li>
        </ul>
      </div>
    </div>
  )
}
