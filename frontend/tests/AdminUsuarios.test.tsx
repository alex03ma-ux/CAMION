// P2 — Panel de administración de usuarios (CA-048-09 a CA-048-16, CA-N03-R07, D-15).
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import App from '../src/App'
import type { UsuarioAdmin } from '../src/api/admin'
import { AdminUsuarios } from '../src/components/AdminUsuarios'
import { cabecera, simularFetch, TOKEN_FICTICIO, type RespuestaSimulada } from './helpers'

const ID_ADMIN = 'a-0001'
const PERFIL_ADMIN = {
  usuarioId: ID_ADMIN,
  nombreCompleto: 'Administrador Ficticio',
  email: 'admin.ficticio@ejemplo.test',
  phoneNumber: '+57 300 000 0001',
  roles: ['ADMINISTRADOR'],
  estado: 'ACTIVA',
}

const LISTA = '/api/admin/usuarios?estado=PENDIENTE&pagina=1&tamano=20'
const MENSAJE_MOTIVO = 'Escribe el motivo (5 a 500 caracteres).'

function guardarSesion(roles: string[], usuarioId = ID_ADMIN) {
  window.localStorage.setItem(
    'camion.sesion',
    JSON.stringify({
      accessToken: TOKEN_FICTICIO,
      expiraEnUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      usuarioId,
      nombreCompleto: 'Usuario Ficticio',
      email: 'usuario.ficticio@ejemplo.test',
      roles,
    }),
  )
}

function asesor(id: string, nombre: string, estado: string, extra: Partial<UsuarioAdmin> = {}) {
  const autorizacion: Record<string, string> = {
    PENDIENTE: 'PENDIENTE',
    ACTIVA: 'APROBADO',
    RECHAZADA: 'RECHAZADO',
    BLOQUEADA: 'BLOQUEADO',
  }
  return {
    usuarioId: id,
    nombreCompleto: nombre,
    email: `${id}@ejemplo.test`,
    telefono: '+57 310 000 0000',
    roles: ['ASESOR'],
    estado,
    creadoEn: '2026-10-01T10:00:00Z',
    ultimoAccesoEn: null,
    asesor: {
      tipoDocumento: 'CC',
      numeroDocumento: `10${id.replace(/\D/g, '')}000`,
      nombreComercial: 'Camiones Ficticios',
      estadoAutorizacion: autorizacion[estado],
      autorizadoEn: null,
    },
    ...extra,
  }
}

function persona(id: string, nombre: string, estado: string, roles: string[]) {
  return {
    usuarioId: id,
    nombreCompleto: nombre,
    email: `${id}@ejemplo.test`,
    telefono: '+57 300 000 0000',
    roles,
    estado,
    creadoEn: '2026-09-01T10:00:00Z',
    ultimoAccesoEn: null,
    asesor: null,
  }
}

function pagina(items: unknown[], total = items.length, pag = 1) {
  return { status: 200, body: { items, total, pagina: pag, tamano: 20 } }
}

const VACIA = pagina([])
const PENDIENTE_1 = asesor('u-0002', 'Asesor Pendiente', 'PENDIENTE')

type Rutas = Record<string, RespuestaSimulada | RespuestaSimulada[]>

function renderPanel(rutas: Rutas) {
  const api = simularFetch(rutas)
  const onSesionTerminada = vi.fn()
  const onVolver = vi.fn()
  render(
    <AdminUsuarios
      token={TOKEN_FICTICIO}
      usuarioIdPropio={ID_ADMIN}
      onSesionTerminada={onSesionTerminada}
      onVolver={onVolver}
    />,
  )
  return { api, onSesionTerminada, onVolver, user: userEvent.setup() }
}

async function fila(nombre: string) {
  const celda = await screen.findByRole('rowheader', { name: new RegExp(nombre) })
  return celda.closest('tr') as HTMLElement
}

function cuerpo(api: ReturnType<typeof simularFetch>, ruta: string, i = 0) {
  return JSON.parse(String(api.llamadasA(ruta)[i].init?.body))
}

describe('Panel de administración: visibilidad por rol', () => {
  it('CA-048-09 el administrador (según /me) ve el acceso y abre el panel', async () => {
    guardarSesion(['ADMINISTRADOR'])
    const api = simularFetch({ '/api/auth/me': { status: 200, body: PERFIL_ADMIN }, [LISTA]: VACIA })
    const user = userEvent.setup()
    render(<App />)
    await user.click(await screen.findByRole('button', { name: 'Administrar usuarios' }))
    expect(await screen.findByRole('heading', { name: 'Administración de usuarios' })).toBeInTheDocument()
    await waitFor(() => expect(api.llamadasA(LISTA)).toHaveLength(1))
    expect(cabecera(api.llamadasA(LISTA)[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
    // Volver al perfil.
    await user.click(await screen.findByRole('button', { name: 'Volver a mi perfil' }))
    expect(await screen.findByRole('heading', { name: /Bienvenido/ })).toBeInTheDocument()
  })

  it.each([
    ['COMPRADOR', ['COMPRADOR']],
    ['ASESOR', ['ASESOR']],
  ])('CA-048-09 / CA-N03-R07 %s no ve el acceso al panel ni botones de administración', async (_n, roles) => {
    guardarSesion(roles)
    const api = simularFetch({ '/api/auth/me': { status: 200, body: { ...PERFIL_ADMIN, roles } } })
    render(<App />)
    await screen.findByRole('heading', { name: /Bienvenido/ })
    expect(screen.queryByRole('button', { name: 'Administrar usuarios' })).toBeNull()
    expect(screen.queryByRole('button', { name: /Aprobar|Bloquear|Rechazar/ })).toBeNull()
    expect(api.fn.mock.calls.some(([u]) => String(u).startsWith('/api/admin'))).toBe(false)
  })

  it('CA-048-16 un comprador que edita camion.sesion con rol ADMINISTRADOR no ve el panel: manda /me', async () => {
    guardarSesion(['ADMINISTRADOR'], 'u-0009')
    simularFetch({
      '/api/auth/me': { status: 200, body: { ...PERFIL_ADMIN, usuarioId: 'u-0009', roles: ['COMPRADOR'] } },
    })
    render(<App />)
    await screen.findByRole('heading', { name: /Bienvenido/ })
    expect(screen.queryByRole('button', { name: 'Administrar usuarios' })).toBeNull()
  })
})

describe('Panel de administración: lista y filtros', () => {
  it('CA-048-10 carga por defecto los PENDIENTE y muestra estado, documento y total', async () => {
    const { api } = renderPanel({ [LISTA]: pagina([PENDIENTE_1]) })
    const f = await fila('Asesor Pendiente')
    expect(api.llamadasA(LISTA)).toHaveLength(1)
    expect(screen.getByLabelText('Estado')).toHaveValue('PENDIENTE')
    expect(within(f).getByText('Pendiente')).toBeInTheDocument()
    expect(within(f).getByText('u-0002@ejemplo.test')).toBeInTheDocument()
    expect(within(f).getByText('CC')).toBeInTheDocument()
    expect(within(f).getByText('Asesor')).toBeInTheDocument()
    // jsdom calcula el nombre accesible sin el espacio inicial del <span>: "Resultados(1 usuario)".
    expect(screen.getByRole('heading', { name: /Resultados\s*\(1 usuario\)/ })).toBeInTheDocument()
  })

  it('CA-048-10 los filtros y la búsqueda generan la consulta correcta', async () => {
    const { api, user } = renderPanel({
      [LISTA]: pagina([PENDIENTE_1]),
      '/api/admin/usuarios?rol=ASESOR&buscar=g%C3%B3mez&pagina=1&tamano=20': VACIA,
      '/api/admin/usuarios?estado=BLOQUEADA&rol=COMPRADOR&pagina=1&tamano=20': VACIA,
    })
    await fila('Asesor Pendiente')

    await user.selectOptions(screen.getByLabelText('Estado'), '')
    await user.selectOptions(screen.getByLabelText('Rol'), 'ASESOR')
    await user.type(screen.getByLabelText('Buscar por nombre o correo'), '  gómez  ')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(await screen.findByText('No hay usuarios con estos filtros.')).toBeInTheDocument()
    expect(
      api.llamadasA('/api/admin/usuarios?rol=ASESOR&buscar=g%C3%B3mez&pagina=1&tamano=20'),
    ).toHaveLength(1)

    await user.selectOptions(screen.getByLabelText('Estado'), 'BLOQUEADA')
    await user.selectOptions(screen.getByLabelText('Rol'), 'COMPRADOR')
    await user.clear(screen.getByLabelText('Buscar por nombre o correo'))
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() =>
      expect(
        api.llamadasA('/api/admin/usuarios?estado=BLOQUEADA&rol=COMPRADOR&pagina=1&tamano=20'),
      ).toHaveLength(1),
    )
  })

  it('CA-048-10 la paginación pide la página siguiente', async () => {
    const items = Array.from({ length: 20 }, (_, i) => asesor(`u-1${i}`, `Asesor Numero ${i}`, 'PENDIENTE'))
    const { api, user } = renderPanel({
      [LISTA]: pagina(items, 25),
      '/api/admin/usuarios?estado=PENDIENTE&pagina=2&tamano=20': pagina([asesor('u-200', 'Asesor Pagina Dos', 'PENDIENTE')], 25, 2),
    })
    await fila('Asesor Numero 0')
    expect(screen.getByText('Página 1 de 2')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Siguiente' }))
    await fila('Asesor Pagina Dos')
    expect(api.llamadasA('/api/admin/usuarios?estado=PENDIENTE&pagina=2&tamano=20')).toHaveLength(1)
    expect(screen.getByText('Página 2 de 2')).toBeInTheDocument()
  })

  it('CA-048-07/15 un 400 de filtros muestra cada mensaje bajo su filtro y el aviso general', async () => {
    const ruta = '/api/admin/usuarios?estado=PENDIENTE&buscar=x&pagina=1&tamano=20'
    const { user } = renderPanel({
      [LISTA]: pagina([PENDIENTE_1]),
      [ruta]: {
        status: 400,
        body: {
          error: 'Revisa los filtros.',
          errores: {
            buscar: ['La búsqueda no puede tener más de 100 caracteres.'],
            estado: ['Estado no válido.'],
            pagina: ['La página debe ser mayor que 0.'],
          },
        },
      },
    })
    await fila('Asesor Pendiente')
    await user.type(screen.getByLabelText('Buscar por nombre o correo'), 'x')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))

    await waitFor(() =>
      expect(document.getElementById('admin-buscar-error')).toHaveTextContent(
        'La búsqueda no puede tener más de 100 caracteres.',
      ),
    )
    expect(screen.getByLabelText('Buscar por nombre o correo')).toHaveAttribute('aria-invalid', 'true')
    expect(document.getElementById('admin-estado-error')).toHaveTextContent('Estado no válido.')
    const alerta = screen.getByRole('alert')
    expect(alerta).toHaveTextContent('Revisa los filtros.')
    expect(alerta).toHaveTextContent('La página debe ser mayor que 0.')
    expect(screen.queryByRole('table')).toBeNull()
  })

  it('CA-048-15 403 en la lista: muestra el mensaje del servidor, sin datos y sin cerrar la sesión', async () => {
    const { onSesionTerminada } = renderPanel({
      [LISTA]: { status: 403, body: { error: 'No tienes permiso para realizar esta acción.' } },
    })
    expect(await screen.findByRole('alert')).toHaveTextContent('No tienes permiso para realizar esta acción.')
    expect(screen.queryByRole('table')).toBeNull()
    expect(onSesionTerminada).not.toHaveBeenCalled()
  })

  it('CA-048-15 401 en la lista (vía App): se borra la sesión y se vuelve al login', async () => {
    guardarSesion(['ADMINISTRADOR'])
    simularFetch({ '/api/auth/me': { status: 200, body: PERFIL_ADMIN }, [LISTA]: { status: 401 } })
    const user = userEvent.setup()
    render(<App />)
    await user.click(await screen.findByRole('button', { name: 'Administrar usuarios' }))
    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Tu sesión terminó. Inicia sesión de nuevo.')
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
  })
})

describe('Panel de administración: botones por estado', () => {
  it('CA-048-11 / D-15 solo se ofrecen transiciones válidas; nunca sobre administradores ni la propia cuenta', async () => {
    renderPanel({
      [LISTA]: pagina([
        asesor('u-0011', 'Asesor Pendiente', 'PENDIENTE'),
        asesor('u-0012', 'Asesor Aprobado', 'ACTIVA'),
        asesor('u-0013', 'Asesor Rechazado', 'RECHAZADA'),
        asesor('u-0014', 'Asesor Bloqueado', 'BLOQUEADA'),
        persona('u-0015', 'Comprador Activo', 'ACTIVA', ['COMPRADOR']),
        persona('u-0016', 'Comprador Bloqueado', 'BLOQUEADA', ['COMPRADOR']),
        persona('u-0017', 'Comprador Rechazado', 'RECHAZADA', ['COMPRADOR']),
        persona('a-0002', 'Otro Administrador', 'ACTIVA', ['ADMINISTRADOR']),
        persona('a-0003', 'Administrador Bloqueado', 'BLOQUEADA', ['ADMINISTRADOR']),
        // Propia cuenta (mismo id que el administrador en sesión).
        persona(ID_ADMIN, 'Mi Propia Cuenta', 'ACTIVA', ['COMPRADOR']),
      ]),
    })

    const botones = async (nombre: string) =>
      within(await fila(nombre))
        .queryAllByRole('button')
        .map((b) => b.textContent?.replace(/\s+a .*$/, '').trim())

    expect(await botones('Asesor Pendiente')).toEqual(['Aprobar', 'Rechazar'])
    expect(await botones('Asesor Aprobado')).toEqual(['Bloquear'])
    expect(await botones('Asesor Rechazado')).toEqual(['Reconsiderar'])
    expect(await botones('Asesor Bloqueado')).toEqual(['Desbloquear'])
    expect(await botones('Comprador Activo')).toEqual(['Bloquear'])
    expect(await botones('Comprador Bloqueado')).toEqual(['Desbloquear'])
    expect(await botones('Comprador Rechazado')).toEqual([])
    expect(await botones('Otro Administrador')).toEqual([])
    expect(await botones('Administrador Bloqueado')).toEqual([])
    expect(await botones('Mi Propia Cuenta')).toEqual([])
    expect(within(await fila('Otro Administrador')).getByText('Sin acciones')).toBeInTheDocument()

    // Estados con texto (D-10 para asesores).
    expect(within(await fila('Asesor Aprobado')).getByText('Aprobado')).toBeInTheDocument()
    expect(within(await fila('Asesor Rechazado')).getByText('Rechazado')).toBeInTheDocument()
    expect(within(await fila('Comprador Bloqueado')).getByText('Bloqueada')).toBeInTheDocument()
    // Nombre accesible del botón incluye a quién afecta.
    expect(screen.getByRole('button', { name: 'Reconsiderar a Asesor Rechazado' })).toBeInTheDocument()
  })
})

describe('Panel de administración: diálogo de motivo', () => {
  it.each([
    ['rechazar', 'Rechazar', asesor('u-0021', 'Asesor Uno', 'PENDIENTE')],
    ['bloquear', 'Bloquear', persona('u-0022', 'Asesor Uno', 'ACTIVA', ['COMPRADOR'])],
    ['reconsiderar', 'Reconsiderar', asesor('u-0023', 'Asesor Uno', 'RECHAZADA')],
  ])('CA-048-12 %s exige motivo de 5 a 500 caracteres antes de enviar', async (accion, boton, u) => {
    const ruta = `/api/admin/usuarios/${u.usuarioId}/${accion}`
    const { api, user } = renderPanel({ [LISTA]: [pagina([u]), VACIA], [ruta]: { status: 200, body: u } })
    await user.click(await screen.findByRole('button', { name: `${boton} a Asesor Uno` }))
    const dialogo = screen.getByRole('dialog')
    const motivo = within(dialogo).getByLabelText('Motivo')
    expect(motivo).toHaveFocus()
    expect(motivo).toHaveAttribute('aria-required', 'true')
    const enviar = within(dialogo).getByRole('button', { name: boton })

    // Vacío, solo espacios, 4 caracteres y 501 caracteres: no se envía.
    await user.click(enviar)
    expect(within(dialogo).getByText(MENSAJE_MOTIVO)).toBeInTheDocument()
    expect(motivo).toHaveAttribute('aria-invalid', 'true')
    await user.type(motivo, '      ')
    await user.click(enviar)
    expect(within(dialogo).getByText(MENSAJE_MOTIVO)).toBeInTheDocument()
    await user.clear(motivo)
    await user.type(motivo, 'abcd')
    await user.click(enviar)
    expect(within(dialogo).getByText(MENSAJE_MOTIVO)).toBeInTheDocument()
    fireEvent.change(motivo, { target: { value: 'x'.repeat(501) } })
    await user.click(enviar)
    expect(within(dialogo).getByText(MENSAJE_MOTIVO)).toBeInTheDocument()
    expect(api.llamadasA(ruta)).toHaveLength(0)

    // 5 caracteres (tras recortar): se envía recortado.
    fireEvent.change(motivo, { target: { value: '  abcde  ' } })
    await user.click(enviar)
    await waitFor(() => expect(api.llamadasA(ruta)).toHaveLength(1))
    expect(api.llamadasA(ruta)[0].init?.method).toBe('POST')
    expect(cuerpo(api, ruta)).toEqual({ motivo: 'abcde' })
    expect(cabecera(api.llamadasA(ruta)[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
  })

  it('CA-048-12 Cancelar cierra sin enviar petición', async () => {
    const { api, user } = renderPanel({ [LISTA]: pagina([PENDIENTE_1]) })
    await user.click(await screen.findByRole('button', { name: 'Rechazar a Asesor Pendiente' }))
    await user.type(screen.getByLabelText('Motivo'), 'Documento ilegible')
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancelar' }))
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(api.fn).toHaveBeenCalledTimes(1) // solo la lista
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Rechazar a Asesor Pendiente' })).toHaveFocus(),
    )
  })

  it('CA-048-12 Escape cancela y devuelve el foco al botón de origen', async () => {
    const { api, user } = renderPanel({ [LISTA]: pagina([PENDIENTE_1]) })
    const origen = await screen.findByRole('button', { name: 'Rechazar a Asesor Pendiente' })
    await user.click(origen)
    expect(screen.getByRole('dialog')).toHaveAttribute('aria-modal', 'true')
    // Mientras el diálogo está abierto, los botones de la tabla quedan deshabilitados.
    expect(screen.getByRole('button', { name: 'Aprobar a Asesor Pendiente' })).toBeDisabled()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).toBeNull()
    await waitFor(() => expect(origen).toHaveFocus())
    expect(api.fn).toHaveBeenCalledTimes(1)
  })

  it('el foco queda atrapado dentro del diálogo con Tab', async () => {
    const { user } = renderPanel({ [LISTA]: pagina([PENDIENTE_1]) })
    await user.click(await screen.findByRole('button', { name: 'Rechazar a Asesor Pendiente' }))
    const dialogo = screen.getByRole('dialog')
    for (let i = 0; i < 5; i++) {
      await user.tab()
      expect(dialogo.contains(document.activeElement)).toBe(true)
    }
  })

  it('aprobar tiene motivo opcional: se envía motivo null', async () => {
    const ruta = '/api/admin/usuarios/u-0002/aprobar'
    const { api, user } = renderPanel({
      [LISTA]: [pagina([PENDIENTE_1]), VACIA],
      [ruta]: { status: 200, body: { ...PENDIENTE_1, estado: 'ACTIVA' } },
    })
    await user.click(await screen.findByRole('button', { name: 'Aprobar a Asesor Pendiente' }))
    const dialogo = screen.getByRole('dialog')
    expect(within(dialogo).getByLabelText('Motivo (opcional)')).not.toHaveAttribute('aria-required')
    await user.click(within(dialogo).getByRole('button', { name: 'Aprobar' }))
    expect(await screen.findByText('Se aprobó la cuenta de asesor de Asesor Pendiente.')).toHaveAttribute(
      'role',
      'status',
    )
    expect(cuerpo(api, ruta)).toEqual({ motivo: null })
  })
})

describe('Panel de administración: resultado de las acciones', () => {
  it('CA-048-13 rechazar con éxito muestra el resultado y recarga la lista (la fila desaparece)', async () => {
    const ruta = '/api/admin/usuarios/u-0002/rechazar'
    const { api, user } = renderPanel({
      [LISTA]: [pagina([PENDIENTE_1]), VACIA],
      [ruta]: { status: 200, body: { ...PENDIENTE_1, estado: 'RECHAZADA' } },
    })
    await user.click(await screen.findByRole('button', { name: 'Rechazar a Asesor Pendiente' }))
    await user.type(screen.getByLabelText('Motivo'), 'Documento ilegible')
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Rechazar' }))

    expect(await screen.findByText('Se rechazó la solicitud de asesor de Asesor Pendiente.')).toHaveAttribute(
      'role',
      'status',
    )
    expect(await screen.findByText('No hay usuarios con estos filtros.')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(api.llamadasA(LISTA)).toHaveLength(2)
    expect(cuerpo(api, ruta)).toEqual({ motivo: 'Documento ilegible' })
  })

  it('D-15 reconsiderar un asesor RECHAZADA con éxito muestra el resultado y recarga', async () => {
    const rechazado = asesor('u-0031', 'Asesor Rechazado', 'RECHAZADA')
    const listaRech = '/api/admin/usuarios?estado=RECHAZADA&pagina=1&tamano=20'
    const ruta = '/api/admin/usuarios/u-0031/reconsiderar'
    const { api, user } = renderPanel({
      [LISTA]: VACIA,
      [listaRech]: [pagina([rechazado]), VACIA],
      [ruta]: { status: 200, body: { ...rechazado, estado: 'ACTIVA' } },
    })
    await screen.findByText('No hay usuarios con estos filtros.')
    await user.selectOptions(screen.getByLabelText('Estado'), 'RECHAZADA')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await user.click(await screen.findByRole('button', { name: 'Reconsiderar a Asesor Rechazado' }))
    await user.type(screen.getByLabelText('Motivo'), 'Documento corregido')
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Reconsiderar' }))

    expect(
      await screen.findByText('Se reconsideró y aprobó la cuenta de asesor de Asesor Rechazado.'),
    ).toHaveAttribute('role', 'status')
    await waitFor(() => expect(api.llamadasA(listaRech)).toHaveLength(2))
    expect(cuerpo(api, ruta)).toEqual({ motivo: 'Documento corregido' })
  })

  it.each([
    [409, { error: 'No se puede aprobar una cuenta en estado Activa.' }, 'No se puede aprobar una cuenta en estado Activa.'],
    [404, { error: 'Usuario no encontrado.' }, 'Usuario no encontrado.'],
    [404, undefined, 'Usuario no encontrado.'],
  ])('CA-048-14 %i: muestra el mensaje del servidor, cierra el diálogo y recarga la lista', async (status, body, texto) => {
    const ruta = '/api/admin/usuarios/u-0002/aprobar'
    const { api, user, onSesionTerminada } = renderPanel({
      [LISTA]: [pagina([PENDIENTE_1]), VACIA],
      [ruta]: { status, body },
    })
    await user.click(await screen.findByRole('button', { name: 'Aprobar a Asesor Pendiente' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Aprobar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(texto)
    expect(screen.queryByRole('dialog')).toBeNull()
    await waitFor(() => expect(api.llamadasA(LISTA)).toHaveLength(2))
    expect(onSesionTerminada).not.toHaveBeenCalled()
  })

  it('CA-048-14 400 de motivo: el error del servidor aparece en el diálogo, que sigue abierto', async () => {
    const ruta = '/api/admin/usuarios/u-0002/rechazar'
    const { api, user } = renderPanel({
      [LISTA]: pagina([PENDIENTE_1]),
      [ruta]: { status: 400, body: { error: 'Revisa los datos marcados.', errores: { motivo: [MENSAJE_MOTIVO] } } },
    })
    await user.click(await screen.findByRole('button', { name: 'Rechazar a Asesor Pendiente' }))
    await user.type(screen.getByLabelText('Motivo'), 'Motivo valido')
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Rechazar' }))
    const dialogo = await screen.findByRole('dialog')
    await waitFor(() => expect(within(dialogo).getByText(MENSAJE_MOTIVO)).toBeInTheDocument())
    expect(within(dialogo).getByLabelText('Motivo')).toHaveFocus()
    expect(api.llamadasA(LISTA)).toHaveLength(1)
  })

  it('CA-048-15 403 en una acción: muestra el mensaje del servidor sin cerrar la sesión', async () => {
    const ruta = '/api/admin/usuarios/u-0002/aprobar'
    const { user, onSesionTerminada } = renderPanel({
      [LISTA]: pagina([PENDIENTE_1]),
      [ruta]: { status: 403, body: { error: 'No se puede modificar una cuenta de administrador.' } },
    })
    await user.click(await screen.findByRole('button', { name: 'Aprobar a Asesor Pendiente' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Aprobar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('No se puede modificar una cuenta de administrador.')
    expect(onSesionTerminada).not.toHaveBeenCalled()
  })

  it('CA-048-15 401 en una acción termina la sesión', async () => {
    const ruta = '/api/admin/usuarios/u-0002/aprobar'
    const { user, onSesionTerminada } = renderPanel({ [LISTA]: pagina([PENDIENTE_1]), [ruta]: { status: 401 } })
    await user.click(await screen.findByRole('button', { name: 'Aprobar a Asesor Pendiente' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Aprobar' }))
    await waitFor(() => expect(onSesionTerminada).toHaveBeenCalledTimes(1))
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('CA-048-15 401 en una acción (vía App) borra camion.sesion y vuelve al login', async () => {
    guardarSesion(['ADMINISTRADOR'])
    simularFetch({
      '/api/auth/me': { status: 200, body: PERFIL_ADMIN },
      [LISTA]: pagina([PENDIENTE_1]),
      '/api/admin/usuarios/u-0002/aprobar': { status: 401 },
    })
    const user = userEvent.setup()
    render(<App />)
    await user.click(await screen.findByRole('button', { name: 'Administrar usuarios' }))
    await user.click(await screen.findByRole('button', { name: 'Aprobar a Asesor Pendiente' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Aprobar' }))
    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Tu sesión terminó. Inicia sesión de nuevo.')
    expect(window.localStorage.getItem('camion.sesion')).toBeNull()
  })

  it('sin conexión en una acción: el diálogo sigue abierto con el error para reintentar', async () => {
    const ruta = '/api/admin/usuarios/u-0002/aprobar'
    const { user } = renderPanel({ [LISTA]: pagina([PENDIENTE_1]), [ruta]: { red: true } })
    await user.click(await screen.findByRole('button', { name: 'Aprobar a Asesor Pendiente' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Aprobar' }))
    const dialogo = screen.getByRole('dialog')
    expect(await within(dialogo).findByRole('alert')).toHaveTextContent('No se pudo conectar con el servidor.')
    expect(within(dialogo).getByRole('button', { name: 'Aprobar' })).toBeEnabled()
  })
})
