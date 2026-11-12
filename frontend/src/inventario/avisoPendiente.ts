// Aviso que una pantalla del inventario deja a la siguiente al navegar
// (p. ej. "Vehículo registrado" al pasar de #/inventario/nuevo a #/inventario/<id>).
// Se lee al montar la pantalla destino y se descarta en su primer efecto
// (así sobrevive al doble render de StrictMode).
import type { DatosAviso } from '../components/Aviso'

let pendiente: DatosAviso | null = null

export function dejarAvisoPendiente(aviso: DatosAviso): void {
  pendiente = aviso
}

export function leerAvisoPendiente(): DatosAviso | null {
  return pendiente
}

export function descartarAvisoPendiente(): void {
  pendiente = null
}

// Último vehículo creado o guardado en esta sesión de la app: la lista lo resalta
// para que se vea dónde quedó al volver (mejora pedida en la prueba manual de P4a).
let reciente: number | null = null

export function marcarVehiculoReciente(id: number): void {
  reciente = id
}

export function leerVehiculoReciente(): number | null {
  return reciente
}

/** Texto del botón final del formulario según quién gestiona el inventario. */
export function textoListo(esAdmin: boolean): string {
  return esAdmin ? 'Listo, volver al inventario' : 'Listo, volver a mi inventario'
}

export function olvidarVehiculoReciente(): void {
  reciente = null
}
