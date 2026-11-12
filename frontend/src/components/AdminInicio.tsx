// P7 — Panel "Inicio" del administrador (HU-075 parcial, HU-073/074 parcial; D-64 A; CA-P7-45 a 49, 51).
// Solo contadores, cada uno con enlace a su lista ya filtrada. Sin gráficos, filtros por fecha, sumas de dinero ni
// exportación (P8). Los ceros se muestran con un texto en palabras (CA-P7-47).
import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { NOMBRES_ORIGEN, RESPONSABLE_ADMINISTRACION } from '../api/leads'
import { ESTADOS_VEHICULO_PANEL, obtenerPanel, type PanelAdmin } from '../api/panel'
import { formatoMiles } from '../catalogo/formato'
import { nombreEstado } from '../inventario/estados'
import { conConsulta, hashSeccionAdmin, HASH_BUSCAR, HASH_CONTACTOS, HASH_INVENTARIO } from '../rutas'
import { Aviso } from './Aviso'

type Props = { token: string; onSesionTerminada: () => void }

type Carga = { estado: 'cargando' } | { estado: 'ok'; datos: PanelAdmin } | { estado: 'error'; mensaje: string }

type Contador = {
  clave: string
  titulo: string
  valor: number
  /** Texto cuando el valor es 0 (CA-P7-47). */
  textoCero: string
  detalle?: string
  href?: string
}

function Tarjeta({ c }: { c: Contador }) {
  const contenido = (
    <>
      <span className="panel-numero">{formatoMiles(c.valor)}</span>
      <span className="panel-etiqueta">{c.titulo}</span>
      <span className="panel-detalle">{c.valor === 0 ? c.textoCero : c.detalle ?? ''}</span>
    </>
  )
  return (
    <li className="panel-tarjeta">
      {c.href ? (
        <a href={c.href} className="panel-enlace">
          {contenido}
        </a>
      ) : (
        <div className="panel-enlace panel-sin-enlace">{contenido}</div>
      )}
    </li>
  )
}

function contadores(d: PanelAdmin): { titulo: string; items: Contador[] }[] {
  const solicitudes: Contador[] = d.solicitudes.map((s) => ({
    clave: s.origen,
    titulo: `${NOMBRES_ORIGEN[s.origen]}: abiertas a cargo de Administración`,
    valor: s.abiertas,
    textoCero: 'Sin solicitudes abiertas',
    detalle: s.nuevas === 1 ? '1 nueva' : `${formatoMiles(s.nuevas)} nuevas`,
    href: conConsulta(HASH_CONTACTOS, { estado: 'ABIERTO', responsable: RESPONSABLE_ADMINISTRACION, origen: s.origen }),
  }))
  return [
    {
      titulo: 'Asesores',
      items: [
        {
          clave: 'asesores',
          titulo: 'Asesores pendientes de aprobación',
          valor: d.asesoresPendientes,
          textoCero: 'Sin asesores pendientes',
          href: hashSeccionAdmin('usuarios', { rol: 'ASESOR', estado: 'PENDIENTE' }),
        },
      ],
    },
    { titulo: 'Solicitudes', items: solicitudes },
    {
      titulo: 'Suscripciones',
      items: [
        {
          clave: 'por-vencer',
          titulo: 'Suscripciones que vencen en los próximos 7 días',
          valor: d.suscripcionesPorVencer,
          textoCero: 'Ninguna vence esta semana',
          href: hashSeccionAdmin('suscripciones'),
        },
        {
          clave: 'vencidas',
          titulo: 'Suscripciones vencidas sin renovar',
          valor: d.suscripcionesVencidas,
          textoCero: 'Sin suscripciones vencidas',
          detalle: 'La lista puede incluir vencidas ya reemplazadas',
          href: hashSeccionAdmin('suscripciones', { estado: 'VENCIDA' }),
        },
      ],
    },
    {
      titulo: 'Vehículos y publicaciones',
      items: [
        {
          clave: 'visibles',
          titulo: 'Publicaciones visibles en el portal',
          valor: d.publicacionesVisibles,
          textoCero: 'Ninguna publicación visible',
          href: HASH_BUSCAR,
        },
        ...ESTADOS_VEHICULO_PANEL.map((e) => ({
          clave: e,
          titulo: `Vehículos en estado ${nombreEstado(e)}`,
          valor: d.vehiculosPorEstado[e],
          textoCero: `Sin vehículos en estado ${nombreEstado(e).toLowerCase()}`,
          href: conConsulta(HASH_INVENTARIO, { estado: e }),
        })),
      ],
    },
    {
      titulo: 'Ventas',
      items: [
        {
          clave: 'ventas',
          titulo: 'Ventas vigentes de los últimos 30 días',
          valor: d.ventasUltimos30Dias,
          textoCero: 'Sin ventas en los últimos 30 días',
          href: hashSeccionAdmin('ventas', { estado: 'VIGENTE', desde: d.ventasDesde ?? '' }),
        },
      ],
    },
  ]
}

export function AdminInicio({ token, onSesionTerminada }: Props) {
  const [carga, setCarga] = useState<Carga>({ estado: 'cargando' })
  const [intento, setIntento] = useState(0)
  const titulo = useRef<HTMLHeadingElement>(null)

  useEffect(() => {
    titulo.current?.focus()
  }, [])

  const es401 = useCallback(
    (error: unknown) => {
      if (error instanceof ApiError && error.status === 401) {
        onSesionTerminada()
        return true
      }
      return false
    },
    [onSesionTerminada],
  )

  useEffect(() => {
    const control = new AbortController()
    setCarga({ estado: 'cargando' })
    obtenerPanel(token, control.signal)
      .then((datos) => setCarga({ estado: 'ok', datos }))
      .catch((error: unknown) => {
        if (control.signal.aborted || es401(error)) return
        setCarga({ estado: 'error', mensaje: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO })
      })
    return () => control.abort()
  }, [token, intento, es401])

  return (
    <section className="panel-ancho" aria-labelledby="inicio-admin-titulo">
      <h2 id="inicio-admin-titulo" ref={titulo} tabIndex={-1}>
        Inicio
      </h2>
      <p className="form-ayuda">Resumen de lo que requiere atención. Abre cada contador para ver la lista.</p>
      {carga.estado === 'cargando' && <p role="status">Cargando el resumen...</p>}
      {carga.estado === 'error' && (
        <>
          <Aviso aviso={{ tipo: 'error', texto: carga.mensaje }} />
          <button type="button" className="secondary-button inv-boton-auto" onClick={() => setIntento((i) => i + 1)}>
            Reintentar
          </button>
        </>
      )}
      {carga.estado === 'ok' &&
        contadores(carga.datos).map((grupo) => (
          <div key={grupo.titulo} className="panel-grupo">
            <h3 className="admin-subtitulo">{grupo.titulo}</h3>
            <ul className="panel-contadores">
              {grupo.items.map((c) => (
                <Tarjeta key={c.clave} c={c} />
              ))}
            </ul>
          </div>
        ))}
    </section>
  )
}
