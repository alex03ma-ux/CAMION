// Setup común de las pruebas: matchers de jest-dom, localStorage simulado y limpieza.
import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, beforeEach, vi } from 'vitest'

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
})

afterEach(() => {
  cleanup()
  vi.useRealTimers()
})
