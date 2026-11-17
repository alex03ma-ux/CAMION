// P9 (CA-P9-50, 55): `<meta name="robots" content="noindex">` mientras se ve una pantalla privada o la página no
// encontrada; las públicas lo quitan. Es una defensa adicional: el panel vive tras `#`, que no llega al servidor, y la
// API exige sesión igualmente. Solo se toca la etiqueta creada aquí (marcada con data-camion).
const SELECTOR = 'meta[name="robots"][data-camion="noindex"]'

export function fijarNoIndex(activo: boolean): void {
  if (typeof document === 'undefined') return
  const existente = document.head.querySelector(SELECTOR)
  if (activo && !existente) {
    const meta = document.createElement('meta')
    meta.setAttribute('name', 'robots')
    meta.setAttribute('content', 'noindex')
    meta.setAttribute('data-camion', 'noindex')
    document.head.appendChild(meta)
  } else if (!activo && existente) {
    existente.remove()
  }
}
