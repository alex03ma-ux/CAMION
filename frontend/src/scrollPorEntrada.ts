// D-93 (entrega 3a): volver a la misma posición al regresar de la ficha al catálogo.
//
// El navegador no puede restaurar la posición por sí solo en una aplicación de una sola página: cuando vuelve a una
// entrada del historial, el contenido todavía no se ha pintado (los datos llegan después). Por eso:
// - `history.scrollRestoration = 'manual'`;
// - cada entrada del historial lleva una clave propia en `history.state` (`camionClave`);
// - al salir de una entrada se guarda su posición vertical en sessionStorage (solo el número, ningún dato personal),
//   por clave de entrada y por dirección (para "Volver a resultados", que crea una entrada nueva);
// - al llegar a una entrada, App coloca la página arriba (navegación nueva) o en la posición guardada (Atrás/Adelante
//   o un enlace marcado con `data-restaurar-scroll`), y las páginas con datos (inicio y resultados) la vuelven a
//   aplicar cuando sus datos están listos (`useRestaurarScroll`).
import { useEffect } from 'react'

const ALMACEN = 'camion.scroll'
/** Máximo de posiciones guardadas (las más viejas se descartan). */
const MAXIMO = 60

type Posiciones = Record<string, number>

let claveActual: string | null = null
let direccionActual: string | null = null
/** Posición a la que hay que llevar la página tras la última navegación; null = ninguna guardada. */
let objetivo: number | null = null
/** Tipo de la última navegación, para que App sepa si sube la página o no. */
let tipoNavegacion: 'nueva' | 'historial' | 'reemplazo' | 'carga' = 'carga'

function nuevaClave(): string {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`
}

function direccionVisible(): string {
  return `${window.location.pathname}${window.location.search}`
}

function leerAlmacen(): Posiciones {
  try {
    const texto = window.sessionStorage.getItem(ALMACEN)
    const valor: unknown = texto ? JSON.parse(texto) : {}
    return valor !== null && typeof valor === 'object' ? (valor as Posiciones) : {}
  } catch {
    return {}
  }
}

function escribirAlmacen(posiciones: Posiciones): void {
  try {
    const claves = Object.keys(posiciones)
    for (const clave of claves.slice(0, Math.max(0, claves.length - MAXIMO))) delete posiciones[clave]
    window.sessionStorage.setItem(ALMACEN, JSON.stringify(posiciones))
  } catch {
    // Sin sessionStorage (modo privado estricto o cuota llena): simplemente no se recuerda la posición.
  }
}

function claveDeEstado(estado: unknown): string | null {
  if (estado !== null && typeof estado === 'object' && 'camionClave' in estado) {
    const clave = (estado as { camionClave: unknown }).camionClave
    return typeof clave === 'string' ? clave : null
  }
  return null
}

/** Clave de la entrada actual del historial; si no tiene (primera carga, pruebas), se le asigna una. */
function asegurarClave(): string {
  const existente = claveDeEstado(window.history.state)
  if (existente) return existente
  const clave = nuevaClave()
  const estado = window.history.state !== null && typeof window.history.state === 'object' ? window.history.state : {}
  window.history.replaceState({ ...estado, camionClave: clave }, '')
  return clave
}

function guardarPosicionActual(): void {
  if (claveActual === null) return
  const posiciones = leerAlmacen()
  const y = Math.max(0, Math.round(window.scrollY))
  delete posiciones[`e:${claveActual}`]
  posiciones[`e:${claveActual}`] = y
  if (direccionActual !== null) {
    delete posiciones[`d:${direccionActual}`]
    posiciones[`d:${direccionActual}`] = y
  }
  escribirAlmacen(posiciones)
}

function posicionGuardada(clave: string): number | null {
  const y = leerAlmacen()[clave]
  return typeof y === 'number' && Number.isFinite(y) ? y : null
}

/** Estado que se pasa a `pushState`: lleva la clave de la nueva entrada. */
export function estadoNuevaEntrada(): { camionClave: string } {
  return { camionClave: nuevaClave() }
}

/**
 * Llamar justo antes de cambiar de entrada con `pushState`/`replaceState` (lo hace `navegar`). `restaurar` = el
 * enlace pidió volver a la posición guardada para esa dirección ("Volver a resultados").
 */
export function antesDeNavegar(direccion: string, { reemplazar, restaurar }: { reemplazar: boolean; restaurar: boolean }): void {
  guardarPosicionActual()
  if (reemplazar) {
    tipoNavegacion = 'reemplazo'
    objetivo = null
    return
  }
  tipoNavegacion = 'nueva'
  const destino = direccion.split('#')[0]
  objetivo = restaurar ? posicionGuardada(`d:${destino}`) : null
}

/** Llamar después de `pushState`/`replaceState`: la entrada actual pasa a ser la nueva. */
export function despuesDeNavegar(): void {
  claveActual = asegurarClave()
  direccionActual = direccionVisible()
}

function alVolverEnHistorial(): void {
  // Aquí la página todavía muestra la entrada anterior: se guarda su posición antes de cambiar.
  guardarPosicionActual()
  claveActual = asegurarClave()
  direccionActual = direccionVisible()
  tipoNavegacion = 'historial'
  objetivo = posicionGuardada(`e:${claveActual}`)
}

/**
 * Activa la restauración manual (una vez, al arrancar la aplicación). Al recargar, la entrada conserva su clave y
 * la posición se recupera cuando llegan los datos. Devuelve la función que quita los oyentes.
 */
export function instalarScrollPorEntrada(): () => void {
  try {
    if ('scrollRestoration' in window.history) window.history.scrollRestoration = 'manual'
  } catch {
    // Navegadores que no lo permiten: se queda la restauración automática.
  }
  claveActual = asegurarClave()
  direccionActual = direccionVisible()
  tipoNavegacion = 'carga'
  objetivo = posicionGuardada(`e:${claveActual}`)
  const alSalir = () => guardarPosicionActual()
  window.addEventListener('popstate', alVolverEnHistorial)
  window.addEventListener('pagehide', alSalir)
  return () => {
    window.removeEventListener('popstate', alVolverEnHistorial)
    window.removeEventListener('pagehide', alSalir)
  }
}

/** Mueve la página sin animación; tolera entornos sin `scrollTo` (pruebas). */
function desplazarA(y: number): void {
  try {
    window.scrollTo(0, y)
  } catch {
    // Sin scrollTo: no hay nada que mover.
  }
}

/**
 * Lo llama App tras cada cambio de dirección (después de mover el foco al título con `preventScroll`): una navegación
 * nueva sube la página; Atrás/Adelante o "Volver a resultados" la llevan a la posición guardada (si las páginas
 * todavía cargan datos, la vuelven a aplicar con `useRestaurarScroll`); un reemplazo no la mueve.
 */
export function aplicarScrollDeNavegacion(): void {
  if (tipoNavegacion === 'reemplazo') return
  if (objetivo !== null) desplazarA(objetivo)
  else if (tipoNavegacion === 'nueva') desplazarA(0)
}

/** Para las páginas que cargan datos: cuando `listo` pasa a true, aplica la posición pendiente (una sola vez). */
export function useRestaurarScroll(listo: boolean): void {
  useEffect(() => {
    if (!listo || objetivo === null) return
    const y = objetivo
    objetivo = null
    desplazarA(y)
  }, [listo])
}

/** Solo para pruebas: olvida el estado del módulo. */
export function olvidarScrollPorEntrada(): void {
  claveActual = null
  direccionActual = null
  objetivo = null
  tipoNavegacion = 'carga'
}
