// D-42 — Foto de portada del portal: el inicio usa la del administrador (o la ilustración) y el administrador
// la cambia o la quita desde "Mi cuenta" › Portada.
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../src/App'
import { RUTA_PORTADA, simularFetch } from './helpers'
import { abrirApp } from './p4pDatos'

const RUTA_ADMIN = '/api/admin/portal/portada'
const RUTA_INICIO = '/api/catalogo/inicio'
const INICIO_VACIO = { status: 200, body: { destacados: [], recientes: [] } }
const URL_FOTO = '/media/portal/portada-20261104-abc.jpg'
const MENSAJE_FORMATO = 'Sube una imagen JPG, PNG o WebP de hasta 5 MB.'

function foto(nombre = 'camion.jpg', tipo = 'image/jpeg', tamano = 1024) {
  const archivo = new File([new Uint8Array(Math.min(tamano, 1024))], nombre, { type: tipo })
  // Tamaño simulado: no hace falta reservar 5 MB de memoria en la prueba.
  Object.defineProperty(archivo, 'size', { value: tamano })
  return archivo
}

beforeEach(() => {
  URL.createObjectURL = vi.fn(() => 'blob:vista-previa')
  URL.revokeObjectURL = vi.fn()
})

describe('Portada en el inicio', () => {
  it('sin foto del administrador se ve la ilustración (no hay <img> en la portada)', async () => {
    const api = simularFetch({ [RUTA_INICIO]: INICIO_VACIO })
    window.location.hash = '#/'
    const { container } = render(<App />)
    await waitFor(() => expect(api.llamadasA(RUTA_PORTADA)).toHaveLength(1))
    expect(container.querySelector('.cat-portada svg.cat-portada-imagen')).not.toBeNull()
    expect(container.querySelector('.cat-portada img')).toBeNull()
  })

  it('con foto del administrador, la portada la muestra como fondo decorativo (alt vacío)', async () => {
    simularFetch({ [RUTA_INICIO]: INICIO_VACIO, [RUTA_PORTADA]: { status: 200, body: { url: URL_FOTO } } })
    window.location.hash = '#/'
    const { container } = render(<App />)
    await waitFor(() => expect(container.querySelector('.cat-portada img')).not.toBeNull())
    const img = container.querySelector('.cat-portada img') as HTMLImageElement
    expect(img.getAttribute('src')).toBe(URL_FOTO)
    expect(img.getAttribute('alt')).toBe('')
  })

  it('si la consulta de la portada falla, se queda la ilustración y el inicio sigue funcionando', async () => {
    simularFetch({ [RUTA_INICIO]: INICIO_VACIO, [RUTA_PORTADA]: { red: true } })
    window.location.hash = '#/'
    const { container } = render(<App />)
    expect(await screen.findByText('Pronto tendremos vehículos publicados.')).toBeInTheDocument()
    expect(container.querySelector('.cat-portada svg.cat-portada-imagen')).not.toBeNull()
  })
})

describe('Administración › Portada', () => {
  it('el perfil del administrador enlaza a Portada', async () => {
    abrirApp('#/cuenta', ['ADMINISTRADOR'])
    const nav = await screen.findByRole('navigation', { name: 'Más administración' })
    expect(within(nav).getByRole('link', { name: 'Portada' })).toHaveAttribute('href', '#/cuenta/portada')
  })

  it('un asesor que entra en #/cuenta/portada ve "No tienes permiso" y no llama a /api/admin', async () => {
    const { api } = abrirApp('#/cuenta/portada', ['ASESOR'])
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
    expect(api.llamadas.some((l) => l.url.startsWith('/api/admin/'))).toBe(false)
  })

  it('sube una foto: vista previa, POST multipart con el campo "archivo" y aviso de éxito', async () => {
    const { api, user } = abrirApp('#/cuenta/portada', ['ADMINISTRADOR'], {
      [RUTA_ADMIN]: { status: 200, body: { url: URL_FOTO } },
    })
    expect(await screen.findByRole('heading', { level: 2, name: 'Portada' })).toBeInTheDocument()
    expect(await screen.findByText('Ahora se usa la ilustración del camión que trae el portal.')).toBeInTheDocument()
    await user.upload(screen.getByLabelText('Foto nueva'), foto())
    expect(screen.getByRole('img', { name: 'Vista previa de la foto nueva' })).toHaveAttribute('src', 'blob:vista-previa')
    await user.click(screen.getByRole('button', { name: 'Usar esta foto' }))
    expect(await screen.findByText('Listo. La nueva foto ya se ve en la portada del portal.')).toBeInTheDocument()
    const llamada = api.llamadasA(RUTA_ADMIN)[0]
    expect(llamada.init?.method).toBe('POST')
    expect((llamada.init?.body as FormData).get('archivo')).toBeInstanceOf(File)
    expect(screen.getByRole('img', { name: 'Foto de portada actual' })).toHaveAttribute('src', URL_FOTO)
  })

  it('rechaza en el navegador un archivo que no es imagen o pasa de 5 MB, sin llamar a la API', async () => {
    const { api } = abrirApp('#/cuenta/portada', ['ADMINISTRADOR'])
    const user = userEvent.setup({ applyAccept: false })
    const campo = await screen.findByLabelText('Foto nueva')
    await user.upload(campo, foto('nota.txt', 'text/plain'))
    expect(screen.getByRole('alert')).toHaveTextContent(MENSAJE_FORMATO)
    await user.upload(campo, foto('grande.jpg', 'image/jpeg', 5 * 1024 * 1024 + 1))
    expect(screen.getByRole('alert')).toHaveTextContent(MENSAJE_FORMATO)
    await user.click(screen.getByRole('button', { name: 'Usar esta foto' }))
    expect(api.llamadasA(RUTA_ADMIN)).toHaveLength(0)
  })

  it('muestra el mensaje de la API si rechaza la foto (400)', async () => {
    const { user } = abrirApp('#/cuenta/portada', ['ADMINISTRADOR'], {
      [RUTA_ADMIN]: { status: 400, body: { error: MENSAJE_FORMATO } },
    })
    await user.upload(await screen.findByLabelText('Foto nueva'), foto())
    await user.click(screen.getByRole('button', { name: 'Usar esta foto' }))
    expect(await screen.findByText(MENSAJE_FORMATO)).toBeInTheDocument()
  })

  it('quitar la foto pide confirmación, hace POST …/quitar y vuelve a la ilustración', async () => {
    const { api, user } = abrirApp('#/cuenta/portada', ['ADMINISTRADOR'], {
      [RUTA_PORTADA]: { status: 200, body: { url: URL_FOTO } },
      [`${RUTA_ADMIN}/quitar`]: { status: 204 },
    })
    await user.click(await screen.findByRole('button', { name: 'Quitar foto y usar la ilustración' }))
    const dialogo = screen.getByRole('dialog', { name: 'Quitar la foto de portada' })
    expect(api.llamadasA(`${RUTA_ADMIN}/quitar`)).toHaveLength(0)
    await user.click(within(dialogo).getByRole('button', { name: 'Quitar foto' }))
    expect(await screen.findByText('Listo. La portada vuelve a mostrar la ilustración del camión.')).toBeInTheDocument()
    expect(api.llamadasA(`${RUTA_ADMIN}/quitar`)[0].init?.method).toBe('POST')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Quitar foto y usar la ilustración' })).not.toBeInTheDocument()
  })
})
