// Configuración de Vitest (pruebas de frontend, P1).
// Las pruebas viven en tests/, fuera de src/, para que `tsc -b` de la app no las compile.
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    include: ['tests/**/*.test.{ts,tsx}'],
    setupFiles: ['./tests/setup.ts'],
    restoreMocks: true,
    unstubGlobals: true,
    // QA P5: con los workers por defecto (núcleos - 1, 17 en un equipo de 18 hilos) cada worker levanta su propio
    // jsdom y las pruebas que teclean formularios completos con user-event (registro, inventario) se quedan sin CPU:
    // fallaban por el timeout de 5 s o por el de 1 s de findBy*, de 6 a 46 según la carga, y pasaban solas.
    // Con la mitad de los núcleos la prueba más lenta tarda < 3 s y la suite termina antes. El timeout de 15 s es
    // solo margen para equipos más lentos; no cambia ninguna aserción.
    maxWorkers: '50%',
    testTimeout: 15_000,
  },
})
