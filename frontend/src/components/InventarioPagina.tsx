// Pantallas del inventario (P4a): #/inventario, #/inventario/nuevo y #/inventario/<id>.
// Solo ASESOR y ADMINISTRADOR (política AsesorOAdministrador, RN-SE-006). La interfaz
// oculta el acceso a los demás, pero quien protege es la API (401/403/404).
import { useCallback, useEffect, useState } from 'react'
import type { PerfilUsuario } from '../api/auth'
import { MENSAJE_SIN_PERMISO } from '../api/http'
import { obtenerCatalogosInventario, type DetalleInventario } from '../api/inventario'
import { esAdministrador, ROL_ASESOR, tieneRol } from '../auth/roles'
import { usePeticion } from '../catalogo/usePeticion'
import { HASH_CUENTA, hashInventarioVehiculo, navegar, type Ruta } from '../rutas'
import {
  dejarAvisoPendiente,
  leerAvisoPendiente,
  marcarVehiculoReciente,
} from '../inventario/avisoPendiente'
import { Aviso, type DatosAviso } from './Aviso'
import { InventarioFormulario } from './InventarioFormulario'
import { InventarioLista } from './InventarioLista'
import { ReasignarVehiculo } from './ReasignarVehiculo'

export type RutaInventario = Extract<
  Ruta,
  { nombre: 'inventario' } | { nombre: 'inventarioNuevo' } | { nombre: 'inventarioVehiculo' }
>

type InventarioPaginaProps = {
  ruta: RutaInventario
  token: string
  perfil: PerfilUsuario
  /** 401: la sesión ya no vale (igual que en P1). */
  onSesionTerminada: () => void
}

export type ModoInventario = 'administrador' | 'asesor'

/** El administrador ve el inventario general; el asesor, el propio; los demás, nada. */
export function modoInventario(roles: readonly string[]): ModoInventario | null {
  if (esAdministrador(roles)) return 'administrador'
  if (tieneRol(roles, ROL_ASESOR)) return 'asesor'
  return null
}

/** Texto del acceso en "Mi cuenta" según el rol; null si no tiene acceso. */
export function textoAccesoInventario(roles: readonly string[]): string | null {
  const modo = modoInventario(roles)
  if (modo === 'administrador') return 'Inventario'
  if (modo === 'asesor') return 'Mi inventario'
  return null
}

export function InventarioPagina({ ruta, token, perfil, onSesionTerminada }: InventarioPaginaProps) {
  const modo = modoInventario(perfil.roles)
  if (modo === null) {
    return (
      <div className="cat-pagina inv-pagina">
        <h1 tabIndex={-1} className="inv-titulo">
          Inventario
        </h1>
        <Aviso aviso={{ tipo: 'error', texto: MENSAJE_SIN_PERMISO }} />
        <p>
          <a href={HASH_CUENTA}>Volver a mi cuenta</a>
        </p>
      </div>
    )
  }
  return (
    <InventarioConAcceso
      ruta={ruta}
      token={token}
      esAdmin={modo === 'administrador'}
      onSesionTerminada={onSesionTerminada}
    />
  )
}

function InventarioConAcceso({
  ruta,
  token,
  esAdmin,
  onSesionTerminada,
}: {
  ruta: RutaInventario
  token: string
  esAdmin: boolean
  onSesionTerminada: () => void
}) {
  const cargar = useCallback((signal: AbortSignal) => obtenerCatalogosInventario(token, signal), [token])
  const [catalogos, reintentar] = usePeticion(cargar, 'catalogos-inventario')

  const error = catalogos.estado === 'error' ? catalogos.error : null
  useEffect(() => {
    if (error?.status === 401) onSesionTerminada()
  }, [error, onSesionTerminada])

  const alCrear = useCallback(
    (detalle: DetalleInventario, aviso: DatosAviso) => {
      marcarVehiculoReciente(detalle.id)
      dejarAvisoPendiente(aviso)
      navegar(hashInventarioVehiculo(detalle.id))
    },
    [],
  )

  // P4b: resultado de "Reasignar a otro asesor" (sobrevive a la recarga del detalle).
  const [avisoReasignar, setAvisoReasignar] = useState<{ id: number; aviso: DatosAviso } | null>(null)

  const tituloLista = esAdmin ? 'Inventario' : 'Mi inventario'
  const sinPermiso = error?.status === 403

  if (sinPermiso) {
    return (
      <div className="cat-pagina inv-pagina">
        <h1 tabIndex={-1} className="inv-titulo">
          {tituloLista}
        </h1>
        <Aviso aviso={{ tipo: 'error', texto: error.mensaje || MENSAJE_SIN_PERMISO }} />
        <p>
          <a href={HASH_CUENTA}>Volver a mi cuenta</a>
        </p>
      </div>
    )
  }

  if (ruta.nombre === 'inventario') {
    return (
      <div className="cat-pagina inv-pagina">
        <h1 tabIndex={-1} className="inv-titulo">
          {tituloLista}
        </h1>
        <p className="inv-intro">
          {esAdmin
            ? 'Todos los vehículos de los asesores. Puedes filtrarlos por asesor.'
            : 'Los vehículos que registraste. Solo tú y el administrador pueden modificarlos.'}
        </p>
        {error && error.status !== 401 && (
          <div className="inv-estado-carga">
            <Aviso
              aviso={{
                tipo: 'advertencia',
                texto: `No se pudieron cargar las listas de los filtros. ${error.mensaje}`,
              }}
            />
            <button type="button" className="inv-boton-auto" onClick={reintentar}>
              Reintentar
            </button>
          </div>
        )}
        <InventarioLista
          token={token}
          esAdmin={esAdmin}
          catalogos={catalogos.estado === 'ok' ? catalogos.datos : null}
          avisoInicial={leerAvisoPendiente()}
          onSesionTerminada={onSesionTerminada}
        />
      </div>
    )
  }

  const vehiculoId = ruta.nombre === 'inventarioVehiculo' ? ruta.id : null
  return (
    <div className="cat-pagina inv-pagina">
      {catalogos.estado === 'cargando' && (
        <>
          <h1 tabIndex={-1} className="inv-titulo">
            {vehiculoId === null ? 'Registrar vehículo' : 'Editar vehículo'}
          </h1>
          <p role="status">Cargando el formulario...</p>
        </>
      )}
      {catalogos.estado === 'error' && (
        <>
          <h1 tabIndex={-1} className="inv-titulo">
            {vehiculoId === null ? 'Registrar vehículo' : 'Editar vehículo'}
          </h1>
          <Aviso aviso={{ tipo: 'error', texto: catalogos.error.mensaje }} />
          <button type="button" className="inv-boton-auto" onClick={reintentar}>
            Reintentar
          </button>
        </>
      )}
      {catalogos.estado === 'ok' && (
        <InventarioFormulario
          key={vehiculoId ?? 'nuevo'}
          token={token}
          esAdmin={esAdmin}
          catalogos={catalogos.datos}
          vehiculoId={vehiculoId}
          avisoInicial={leerAvisoPendiente()}
          onSesionTerminada={onSesionTerminada}
          onCreado={alCrear}
          accionesDetalle={
            esAdmin
              ? (detalle, recargar, titulo) => (
                  <ReasignarVehiculo
                    token={token}
                    detalle={detalle}
                    titulo={titulo}
                    asesores={catalogos.datos.asesores}
                    onSesionTerminada={onSesionTerminada}
                    resultado={avisoReasignar?.id === detalle.id ? avisoReasignar.aviso : null}
                    onResultado={(aviso, recargarDetalle) => {
                      setAvisoReasignar(aviso ? { id: detalle.id, aviso } : null)
                      if (recargarDetalle) recargar()
                    }}
                  />
                )
              : undefined
          }
        />
      )}
    </div>
  )
}
