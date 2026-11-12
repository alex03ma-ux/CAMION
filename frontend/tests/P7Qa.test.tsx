// P7 — pruebas de QA del pie del portal y de la validación de enlaces de redes (CA-P7-37, 39, 41; D-63 A).
// Datos ficticios; sin llamadas reales.
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { enlaceRedValido, HOSTS_INSTAGRAM, HOSTS_TIKTOK } from '../src/api/parametros'
import { PiePortal } from '../src/components/PiePortal'
import { simularFetch } from './helpers'

describe('QA: enlaces de redes (CA-P7-37, 41)', () => {
  it.each([
    'javascript:alert(1)',
    'JAVASCRIPT:alert(1)',
    'data:text/html,hola',
    '//www.instagram.com/cuenta',
    'http://www.instagram.com/cuenta',
    'https://evil.test/www.instagram.com/cuenta',
    'https://www.instagram.com@evil.test/cuenta',
    'https://instagram.com.evil.test/cuenta',
    'https://www.instagram.com:8443/cuenta',
    'https://www.instagram.com/',
    'https://www.instagram.com/a"onclick=x',
    'https://www.tiktok.com/@cuenta',
  ])('Instagram rechaza %s', (valor) => {
    expect(enlaceRedValido(valor, HOSTS_INSTAGRAM)).toBe(false)
  })

  it.each(['javascript:alert(1)', 'https://www.tiktok.com.evil.test/@x', 'https://www.instagram.com/x', 'https://www.tiktok.com'])(
    'TikTok rechaza %s',
    (valor) => {
      expect(enlaceRedValido(valor, HOSTS_TIKTOK)).toBe(false)
    },
  )

  it('acepta los dominios propios con y sin www', () => {
    expect(enlaceRedValido('https://www.instagram.com/cuenta', HOSTS_INSTAGRAM)).toBe(true)
    expect(enlaceRedValido('https://instagram.com/cuenta', HOSTS_INSTAGRAM)).toBe(true)
    expect(enlaceRedValido('https://www.tiktok.com/@cuenta', HOSTS_TIKTOK)).toBe(true)
    expect(enlaceRedValido('https://tiktok.com/@cuenta', HOSTS_TIKTOK)).toBe(true)
  })
})

describe('QA: el pie nunca interpreta lo que llega del servidor (CA-P7-39, 41)', () => {
  it('WhatsApp con otro esquema, redes de otro dominio y HTML en el horario se muestran seguros', async () => {
    simularFetch({
      '/api/portal/contacto': {
        status: 200,
        body: {
          whatsapp: '3001234567',
          whatsappEnlace: 'javascript:alert(1)',
          correo: 'ventas@ejemplo.test',
          horario: ['<img src=x onerror=alert(1)>', 'Sábados'],
          instagram: 'https://www.instagram.com@evil.test/cuenta',
          tiktok: 'https://evil.test/www.tiktok.com/@cuenta',
        },
      },
    })
    const { container } = render(<PiePortal tipos={[]} conSesion={false} />)
    const whatsapp = await screen.findByRole('link', { name: 'WhatsApp 300 123 4567' })
    expect(whatsapp).toHaveAttribute('href', 'https://wa.me/573001234567')
    expect(screen.getByText('<img src=x onerror=alert(1)>')).toBeInTheDocument()
    expect(container.querySelector('img[src="x"]')).toBeNull()
    expect(container.querySelector('script')).toBeNull()
    expect(screen.queryByRole('link', { name: /Instagram/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /TikTok/ })).not.toBeInTheDocument()
    for (const enlace of Array.from(container.querySelectorAll('a'))) {
      expect(enlace.getAttribute('href') ?? '').not.toMatch(/^\s*(javascript|data):/i)
    }
  })
})
