import { categoriasDisponibles, type OpcionCatalogo } from '../api/catalogo'
import {
  HASH_BUSCAR,
  HASH_CUENTA,
  HASH_REGISTRO,
  HASH_SEGURO_TODO_RIESGO,
  HASH_VENDE_TU_CAMION,
  hashBuscar,
} from '../rutas'
import { LogoPortal } from './Encabezado'

type PiePortalProps = {
  tipos: readonly OpcionCatalogo[]
  conSesion: boolean
}

/** Contacto confirmado por la responsable (D-42, 2026-11-04). Se cambia solo aquí. */
export const CONTACTO_WHATSAPP = '316 615 2907'
export const CONTACTO_WHATSAPP_ENLACE = 'https://wa.me/573166152907'
export const CONTACTO_CORREO = 'vc.automotores@gmail.com'
export const HORARIO_ATENCION = [
  'Lunes a viernes: 9:00 a. m. a 5:00 p. m.',
  'Sábados: 9:00 a. m. a 3:00 p. m.',
] as const

/**
 * Pie de página oscuro del portal. Solo lleva enlaces que existen y datos confirmados. Dirección,
 * páginas legales e Instagram/TikTok se añaden cuando la responsable los entregue (D-42); no se inventan.
 */
export function PiePortal({ tipos, conSesion }: PiePortalProps) {
  return (
    <footer className="cat-pie">
      <div className="cat-pie-interior">
        <div className="cat-pie-marca">
          <LogoPortal claro />
          <p>Portal de venta de vehículos de trabajo en Colombia.</p>
        </div>
        <nav className="cat-pie-columna" aria-label="Categorías del pie de página">
          <h2>Explora</h2>
          <ul>
            {categoriasDisponibles(tipos).map(({ tipo, enlace }) => (
              <li key={tipo.id}>
                <a href={hashBuscar({ tipoId: String(tipo.id) })}>{enlace}</a>
              </li>
            ))}
            <li>
              <a href={HASH_BUSCAR}>Todos los vehículos</a>
            </li>
          </ul>
        </nav>
        {/* P5b (D-51 A): columna "Servicios" con los dos avisos públicos. */}
        <nav className="cat-pie-columna" aria-label="Servicios en el pie de página">
          <h2>Servicios</h2>
          <ul>
            <li>
              <a href={HASH_VENDE_TU_CAMION}>Vende tu camión</a>
            </li>
            <li>
              <a href={HASH_SEGURO_TODO_RIESGO}>Compra tu seguro todo riesgo</a>
            </li>
          </ul>
        </nav>
        <nav className="cat-pie-columna" aria-label="Cuenta en el pie de página">
          <h2>Tu cuenta</h2>
          <ul>
            {conSesion ? (
              <li>
                <a href={HASH_CUENTA}>Mi cuenta</a>
              </li>
            ) : (
              <>
                <li>
                  <a href={HASH_CUENTA}>Ingresar</a>
                </li>
                <li>
                  <a href={HASH_REGISTRO}>Crear cuenta o inscribirme como asesor</a>
                </li>
              </>
            )}
          </ul>
        </nav>
        <section className="cat-pie-columna" aria-labelledby="pie-contacto">
          <h2 id="pie-contacto">Contacto</h2>
          <ul>
            <li>
              <a href={CONTACTO_WHATSAPP_ENLACE} target="_blank" rel="noopener noreferrer">
                WhatsApp {CONTACTO_WHATSAPP}
              </a>
            </li>
            <li>
              <a href={`mailto:${CONTACTO_CORREO}`}>{CONTACTO_CORREO}</a>
            </li>
          </ul>
          <h3 className="cat-pie-subtitulo">Horario de atención</h3>
          <ul>
            {HORARIO_ATENCION.map((linea) => (
              <li key={linea}>{linea}</li>
            ))}
          </ul>
        </section>
      </div>
      <p className="cat-pie-legal">© {new Date().getFullYear()} Financiamos Tu Camión</p>
    </footer>
  )
}
