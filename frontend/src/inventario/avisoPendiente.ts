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
