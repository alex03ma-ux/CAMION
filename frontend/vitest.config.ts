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
  },
})
