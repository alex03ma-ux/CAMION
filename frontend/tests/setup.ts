// Setup común de las pruebas: matchers de jest-dom, localStorage simulado y limpieza.
import '@testing-library/jest-dom/vitest'
import { cleanup, configure } from '@testing-library/react'
import { afterEach, beforeEach, vi } from 'vitest'

// INF-3 (O-8): con carga (CI en máquinas lentas, varias suites a la vez) la espera por defecto de 1 s de
// findBy*/waitFor no basta y fallaban de 1 a 4 pruebas distintas. 5 s solo da margen: una prueba que deba
// encontrar el elemento lo encuentra igual; una que no lo encuentre falla (más tarde). No cambia aserciones.
configure({ asyncUtilTimeout: 5000 })

/**
 * localStorage simulado en memoria. Node 25 trae su propio `localStorage`
 * global (Web Storage experimental) que, sin `--localstorage-file`, no es
 * funcional y oculta el de jsdom; por eso se reemplaza en cada prueba.
 */
class AlmacenEnMemoria implements Storage {
  private datos = new Map<string, string>()
  get length() {
    return this.datos.size
  }
  clear() {
    this.datos.clear()
  }
  getItem(clave: string) {
    return this.datos.has(clave) ? (this.datos.get(clave) as string) : null
  }
  key(i: number) {
    return Array.from(this.datos.keys())[i] ?? null
  }
  removeItem(clave: string) {
    this.datos.delete(clave)
  }
  setItem(clave: string, valor: string) {
    this.datos.set(clave, String(valor))
  }
}

beforeEach(() => {
  // P3: cada prueba empieza sin hash (la ruta vive en location.hash).
  window.history.replaceState(null, '', '/')
  const almacen = new AlmacenEnMemoria()
  Object.defineProperty(window, 'localStorage', { value: almacen, configurable: true })
  Object.defineProperty(globalThis, 'localStorage', { value: almacen, configurable: true })
  // D-30: jsdom no implementa las URL de objeto (vistas previas de las fotos elegidas).
  let siguiente = 1
  Object.defineProperty(URL, 'createObjectURL', {
    value: vi.fn(() => `blob:prueba/${siguiente++}`),
    configurable: true,
    writable: true,
  })
  Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true, writable: true })
})

afterEach(() => {
  cleanup()
  vi.useRealTimers()
})
