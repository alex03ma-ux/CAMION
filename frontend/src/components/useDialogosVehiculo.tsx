// P4b — Diálogos del inventario sobre un vehículo: publicar (HU-023), marcar vendido (HU-064, D-39),
// revertir la venta (minuta) y reasignar a otro asesor (D-34, solo administrador).
// Un solo diálogo abierto a la vez; al cerrarse, el foco vuelve al botón que lo abrió.
import { useRef, useState, type ReactNode } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import type { ResumenAsesor } from '../api/inventario'
import {
  CAMPOS_PUBLICACION,
  CAMPOS_VENTA,
  crearPublicacion,
  datosDePublicacion,
  datosDeVenta,
  FORMULARIO_PUBLICACION_VACIO,
  reasignarVehiculo,
  repartirErrores,
  revertirVenta,
  venderVehiculo,
  type ErroresPublicacion,
  type ErroresVenta,
  type FormularioPublicacion,
  type FormularioVenta,
} from '../api/publicaciones'
import type { DatosAviso } from './Aviso'
import { DialogoPublicacion, DialogoReasignar, DialogoRevertirVenta, DialogoVenta } from './DialogosPublicacion'

/** Lo mínimo del vehículo que necesitan los diálogos. */
export type VehiculoDialogo = {
  id: number
  titulo: string
  precioCop: number
  asesor: ResumenAsesor | null
}

type Comun = { vehiculo: VehiculoDialogo; enviando: boolean; error: DatosAviso | null }

type Dialogo =
  | (Comun & { tipo: 'publicar'; errores: ErroresPublicacion })
  | (Comun & { tipo: 'vender'; errores: ErroresVenta })
  | (Comun & { tipo: 'revertir'; erroresMotivo: string[] })
  | (Comun & { tipo: 'reasignar'; erroresMotivo: string[]; erroresAsesor: string[] })

type Opciones = {
  token: string
  /** Asesores ACTIVA para reasignar (catálogos del inventario del administrador). */
  asesores?: readonly ResumenAsesor[]
  onSesionTerminada: () => void
  /** Tras una acción terminada (éxito, o error que obliga a recargar): aviso y recarga. */
  onTerminar: (aviso: DatosAviso) => void
}

const SIN_ERRORES: string[] = []

function mensajeDe(error: unknown): string {
  return error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO
}

function erroresDeMotivo(error: ApiError): string[] {
  const propios = Object.entries(error.errores)
    .filter(([campo]) => campo.toLowerCase() === 'motivo')
    .flatMap(([, m]) => m)
  return propios.length > 0 ? propios : [error.mensaje]
}

export function useDialogosVehiculo({ token, asesores = [], onSesionTerminada, onTerminar }: Opciones) {
  const [dialogo, setDialogo] = useState<Dialogo | null>(null)
  const origen = useRef<HTMLButtonElement | null>(null)

  function abrir(d: Dialogo, boton: HTMLButtonElement) {
    origen.current = boton
    setDialogo(d)
  }

  function cerrar(devolverFoco: boolean) {
    setDialogo(null)
    const boton = origen.current
    origen.current = null
    if (devolverFoco) window.setTimeout(() => boton?.focus(), 0)
  }

  function terminar(aviso: DatosAviso) {
    cerrar(false)
    onTerminar(aviso)
  }

  /** 401 cierra la sesión; 403/404 cierran el diálogo y recargan; el resto queda en el diálogo. */
  function errorComun(error: unknown): boolean {
    if (error instanceof ApiError && error.status === 401) {
      cerrar(false)
      onSesionTerminada()
      return true
    }
    if (error instanceof ApiError && (error.status === 403 || error.status === 404)) {
      terminar({ tipo: 'error', texto: error.mensaje })
      return true
    }
    return false
  }

  function enviandoCon(d: Dialogo): Dialogo {
    return { ...d, enviando: true, error: null }
  }

  function conError(texto: string) {
    setDialogo((d) => d && { ...d, enviando: false, error: { tipo: 'error', texto } })
  }

  async function publicar(form: FormularioPublicacion) {
    if (!dialogo || dialogo.tipo !== 'publicar' || dialogo.enviando) return
    const { vehiculo } = dialogo
    setDialogo(enviandoCon(dialogo))
    try {
      const datos = datosDePublicacion(form)
      await crearPublicacion(token, vehiculo.id, datos)
      terminar({ tipo: 'exito', texto: `Se publicó ${vehiculo.titulo} con el título «${datos.titulo}».` })
    } catch (error) {
      if (errorComun(error)) return
      if (error instanceof ApiError && error.status === 400) {
        const { porCampo, otros } = repartirErrores(error.errores, CAMPOS_PUBLICACION)
        setDialogo((d) =>
          d && d.tipo === 'publicar'
            ? { ...d, enviando: false, errores: porCampo, error: { tipo: 'error', texto: [error.mensaje, ...otros].join(' ') } }
            : d,
        )
        return
      }
      // 409: sin plan, sin cupo o vehículo no publicable; el mensaje del servidor lo explica.
      conError(mensajeDe(error))
    }
  }

  async function vender(form: FormularioVenta) {
    if (!dialogo || dialogo.tipo !== 'vender' || dialogo.enviando) return
    const { vehiculo } = dialogo
    setDialogo(enviandoCon(dialogo))
    try {
      await venderVehiculo(token, vehiculo.id, datosDeVenta(form))
      terminar({ tipo: 'exito', texto: `${vehiculo.titulo} quedó vendido y salió del portal.` })
    } catch (error) {
      if (errorComun(error)) return
      if (error instanceof ApiError && error.status === 400) {
        const { porCampo, otros } = repartirErrores(error.errores, CAMPOS_VENTA)
        setDialogo((d) =>
          d && d.tipo === 'vender'
            ? { ...d, enviando: false, errores: porCampo, error: { tipo: 'error', texto: [error.mensaje, ...otros].join(' ') } }
            : d,
        )
        return
      }
      if (error instanceof ApiError && error.status === 409) {
        terminar({ tipo: 'error', texto: error.mensaje })
        return
      }
      conError(mensajeDe(error))
    }
  }

  async function revertir(motivo: string) {
    if (!dialogo || dialogo.tipo !== 'revertir' || dialogo.enviando) return
    const { vehiculo } = dialogo
    setDialogo({ ...dialogo, enviando: true, error: null, erroresMotivo: SIN_ERRORES })
    try {
      await revertirVenta(token, vehiculo.id, motivo)
      terminar({
        tipo: 'exito',
        texto: `Se revirtió la venta de ${vehiculo.titulo}. El vehículo está activo y su publicación quedó inactiva; reactívala en «Mis publicaciones» para mostrarla otra vez.`,
      })
    } catch (error) {
      if (errorComun(error)) return
      if (error instanceof ApiError && error.status === 400) {
        setDialogo((d) => d && d.tipo === 'revertir' ? { ...d, enviando: false, erroresMotivo: erroresDeMotivo(error) } : d)
        return
      }
      if (error instanceof ApiError && error.status === 409) {
        terminar({ tipo: 'error', texto: error.mensaje })
        return
      }
      conError(mensajeDe(error))
    }
  }

  async function reasignar(asesorId: string, motivo: string) {
    if (!dialogo || dialogo.tipo !== 'reasignar' || dialogo.enviando) return
    const { vehiculo } = dialogo
    setDialogo({ ...dialogo, enviando: true, error: null, erroresMotivo: SIN_ERRORES, erroresAsesor: SIN_ERRORES })
    const destino = asesores.find((a) => a.id === asesorId)
    try {
      await reasignarVehiculo(token, vehiculo.id, asesorId, motivo)
      terminar({
        tipo: 'exito',
        texto: `${vehiculo.titulo} quedó asignado a ${destino?.nombre || 'el nuevo asesor'}.`,
      })
    } catch (error) {
      if (errorComun(error)) return
      if (error instanceof ApiError && error.status === 400) {
        const deAsesor = Object.entries(error.errores)
          .filter(([c]) => c.toLowerCase() === 'asesorid')
          .flatMap(([, m]) => m)
        const deMotivo = Object.entries(error.errores)
          .filter(([c]) => c.toLowerCase() === 'motivo')
          .flatMap(([, m]) => m)
        setDialogo((d) =>
          d && d.tipo === 'reasignar'
            ? {
                ...d,
                enviando: false,
                erroresAsesor: deAsesor,
                erroresMotivo: deMotivo,
                error: deAsesor.length + deMotivo.length === 0 ? { tipo: 'error', texto: error.mensaje } : null,
              }
            : d,
        )
        return
      }
      // 409: el asesor destino no tiene cupo; se queda en el diálogo para elegir otro.
      conError(mensajeDe(error))
    }
  }

  const ocupado = dialogo !== null

  let elemento: ReactNode = null
  if (dialogo?.tipo === 'publicar') {
    elemento = (
      <DialogoPublicacion
        modo="crear"
        tituloVehiculo={dialogo.vehiculo.titulo}
        inicial={{ ...FORMULARIO_PUBLICACION_VACIO, titulo: dialogo.vehiculo.titulo }}
        enviando={dialogo.enviando}
        erroresServidor={dialogo.errores}
        error={dialogo.error}
        onConfirmar={(f) => void publicar(f)}
        onCancelar={() => cerrar(true)}
      />
    )
  } else if (dialogo?.tipo === 'vender') {
    elemento = (
      <DialogoVenta
        tituloVehiculo={dialogo.vehiculo.titulo}
        precioCop={dialogo.vehiculo.precioCop}
        enviando={dialogo.enviando}
        erroresServidor={dialogo.errores}
        error={dialogo.error}
        onConfirmar={(f) => void vender(f)}
        onCancelar={() => cerrar(true)}
      />
    )
  } else if (dialogo?.tipo === 'revertir') {
    elemento = (
      <DialogoRevertirVenta
        tituloVehiculo={dialogo.vehiculo.titulo}
        enviando={dialogo.enviando}
        erroresMotivo={dialogo.erroresMotivo}
        error={dialogo.error}
        onConfirmar={(m) => void revertir(m)}
        onCancelar={() => cerrar(true)}
      />
    )
  } else if (dialogo?.tipo === 'reasignar') {
    elemento = (
      <DialogoReasignar
        tituloVehiculo={dialogo.vehiculo.titulo}
        asesorActual={dialogo.vehiculo.asesor}
        asesores={asesores}
        enviando={dialogo.enviando}
        erroresMotivo={dialogo.erroresMotivo}
        erroresAsesor={dialogo.erroresAsesor}
        error={dialogo.error}
        onConfirmar={(a, m) => void reasignar(a, m)}
        onCancelar={() => cerrar(true)}
      />
    )
  }

  return {
    ocupado,
    dialogo: elemento,
    abrirPublicar: (vehiculo: VehiculoDialogo, boton: HTMLButtonElement) =>
      abrir({ tipo: 'publicar', vehiculo, enviando: false, error: null, errores: {} }, boton),
    abrirVender: (vehiculo: VehiculoDialogo, boton: HTMLButtonElement) =>
      abrir({ tipo: 'vender', vehiculo, enviando: false, error: null, errores: {} }, boton),
    abrirRevertir: (vehiculo: VehiculoDialogo, boton: HTMLButtonElement) =>
      abrir({ tipo: 'revertir', vehiculo, enviando: false, error: null, erroresMotivo: SIN_ERRORES }, boton),
    abrirReasignar: (vehiculo: VehiculoDialogo, boton: HTMLButtonElement) =>
      abrir(
        { tipo: 'reasignar', vehiculo, enviando: false, error: null, erroresMotivo: SIN_ERRORES, erroresAsesor: SIN_ERRORES },
        boton,
      ),
  }
}
