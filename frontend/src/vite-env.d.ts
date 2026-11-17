/// <reference types="vite/client" />

// P10: variables de compilación del frontend (docs/GUIA-PUBLICACION.md). Ninguna es secreta.
interface ImportMetaEnv {
  /** CA-P10-82: URL pública con https (igual que Portal:UrlPublica de la API); vacía = sin og:url ni og:image. */
  readonly VITE_URL_PUBLICA?: string
  /** D-89 A: "true" solo al abrir el portal al público (igual que Portal:PermitirIndexacion de la API). */
  readonly VITE_PERMITIR_INDEXACION?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
