import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const API = 'http://localhost:5080'

// P10 (CA-P10-47): el portal usa rutas sin `#`. Vite responde index.html para cualquier ruta que no sea un archivo ni
// uno de estos prefijos, que van a la API (también con `npm run preview`, que reutiliza este proxy). `/vehiculo/` NO
// se envía a la API en desarrollo: Vite sirve su propio index.html (en producción, con D-88 A, sí lo atiende la API).
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': { target: API, changeOrigin: true },
      '/health': { target: API, changeOrigin: true },
      '/media': { target: API, changeOrigin: true },
      // CA-P10-51 a 55 (D-89 A): robots.txt y sitemap.xml los genera la API según Portal:PermitirIndexacion.
      '/robots.txt': { target: API, changeOrigin: true },
      '/sitemap.xml': { target: API, changeOrigin: true },
    },
  },
})
