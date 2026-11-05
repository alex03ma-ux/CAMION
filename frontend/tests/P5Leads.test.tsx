// P5 — Contactos (leads) en el panel: permisos de interfaz, "Contactos (N nuevos)", lista con filtros y
// paginación, detalle con historial, notas, cierre y, solo el administrador, reabrir y reasignar.
// CA-P5-16 (vista), 29, 31, 32 (interfaz), 36, 37, 39, 41, 42, 44, 57, 58.
import { render, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import App from '../src/App'
import { cabecera, simularFetch, TOKEN_FICTICIO } from './helpers'
import { abrirApp, CATALOGOS_INVENTARIO, RUTA_CATALOGOS_INV, type Rutas } from './p4pDatos'
import {
  CONSENTIMIENTOS,
  cuerpo,
  detalleLead,
  lead,
  paginaLeads,
  RUTA_RESUMEN,
  rutaLeads,
} from './p5Datos'

const resumen = (nuevos: number) => ({ status: 200, body: { nuevos, abiertos: nuevos + 2 } })

function llamadasLeads(api: ReturnType<typeof abrirApp>['api']) {
  return api.llamadas.filter((l) => l.url.startsWith('/api/leads'))
}

describe('Permisos de interfaz', () => {
  it('CA-P5-32 un comprador en #/contactos ve "No tienes permiso" y no se llama a /api/leads', async () => {
    const { api } = abrirApp('#/contactos', ['COMPRADOR'])
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Volver a mi cuenta' })).toHaveAttribute('href', '#/cuenta')
    expect(llamadasLeads(api)).toHaveLength(0)
  })

  it('un comprador en #/contactos/5 tampoco llama a /api/leads', async () => {
    const { api } = abrirApp('#/contactos/5', ['COMPRADOR'])
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
    expect(llamadasLeads(api)).toHaveLength(0)
  })

  it('el perfil del comprador no ofrece "Contactos" ni pide el resumen', async () => {
    const { api } = abrirApp('#/cuenta', ['COMPRADOR'])
    expect(await screen.findByRole('heading', { name: /Bienvenido/ })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /^Contactos/ })).not.toBeInTheDocument()
    expect(llamadasLeads(api)).toHaveLength(0)
  })

  it('sin sesión, #/contactos lleva al inicio de sesión y no llama a /api/leads', async () => {
    window.location.hash = '#/contactos'
    const api = simularFetch({})
    render(<App />)
    expect(await screen.findByRole('button', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(window.location.hash).toBe('#/cuenta')
    expect(api.llamadas.some((l) => l.url.startsWith('/api/leads'))).toBe(false)
  })
})

describe('"Contactos (N nuevos)" (D-48, CA-P5-58)', () => {
  it.each([
    [3, 'Contactos (3 nuevos)'],
    [1, 'Contactos (1 nuevo)'],
    [0, 'Contactos'],
  ])('asesor con %i nuevos ve "%s" en su perfil', async (n, texto) => {
    const { api } = abrirApp('#/cuenta', ['ASESOR'], { [RUTA_RESUMEN]: resumen(n) })
    const enlace = await screen.findByRole('link', { name: texto })
    expect(enlace).toHaveAttribute('href', '#/contactos')
    expect(cabecera(api.llamadasA(RUTA_RESUMEN)[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
  })

  it('el administrador también lo ve, y la barra de administración enlaza a Contactos', async () => {
    const { user } = abrirApp('#/cuenta', ['ADMINISTRADOR'], {
      [RUTA_RESUMEN]: resumen(2),
      '/api/admin/usuarios?estado=PENDIENTE&pagina=1&tamano=20': paginaLeads([]),
    })
    expect(await screen.findByRole('link', { name: 'Contactos (2 nuevos)' })).toHaveAttribute('href', '#/contactos')
    await user.click(screen.getByRole('button', { name: 'Administrar usuarios' }))
    const nav = await screen.findByRole('navigation', { name: 'Administración' })
    expect(within(nav).getByRole('link', { name: 'Contactos' })).toHaveAttribute('href', '#/contactos')
  })

  it('si el resumen falla, el enlace sigue sin número', async () => {
    abrirApp('#/cuenta', ['ASESOR'], { [RUTA_RESUMEN]: { status: 500 } })
    expect(await screen.findByRole('link', { name: 'Contactos' })).toHaveAttribute('href', '#/contactos')
  })

  it('un 401 del resumen termina la sesión', async () => {
    abrirApp('#/cuenta', ['ASESOR'], { [RUTA_RESUMEN]: { status: 401 } })
    expect(await screen.findByText('Tu sesión terminó. Inicia sesión de nuevo.')).toBeInTheDocument()
  })
})

describe('Lista del asesor (CA-P5-29, CA-P5-36)', () => {
  it('"Mis contactos": pide /api/leads con token, muestra estado en texto, Nuevo y sin filtro de responsable', async () => {
    const { api } = abrirApp('#/contactos', ['ASESOR'], {
      [rutaLeads()]: paginaLeads([
        lead(2, { nuevo: true, nombre: 'Ana Nueva', correo: 'ana@ejemplo.test' }),
        lead(1, { estado: 'CERRADO', nombre: 'Beto Cerrado', resultadoCierre: 'NO_RESPONDIO', cerradoEn: '2026-11-05T10:00:00Z' }),
      ]),
    })
    // Mientras se verifica la sesión ya se ve un h1 "Mis contactos"; se espera a la lista.
    await screen.findByText('Ana Nueva')
    expect(screen.getByRole('heading', { level: 1, name: 'Mis contactos' })).toBeInTheDocument()
    const items = screen.getAllByRole('listitem')
    const ana = items.find((li) => li.textContent?.includes('Ana Nueva')) as HTMLElement
    const beto = items.find((li) => li.textContent?.includes('Beto Cerrado')) as HTMLElement
    expect(within(ana).getByText('Pendiente de gestión')).toBeInTheDocument()
    expect(within(ana).getByText('Nuevo')).toBeInTheDocument()
    expect(within(ana).getByText('ana@ejemplo.test')).toBeInTheDocument()
    expect(within(ana).getByText('Chevrolet NPR 2020')).toBeInTheDocument()
    expect(within(ana).getByRole('link', { name: /Ver detalle/ })).toHaveAttribute('href', '#/contactos/2')
    expect(within(beto).getByText('Finalizado')).toBeInTheDocument()
    expect(within(beto).getByText('No respondió')).toBeInTheDocument()
    expect(within(beto).queryByText('Nuevo')).not.toBeInTheDocument()
    expect(screen.queryByText('ABIERTO')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Responsable')).not.toBeInTheDocument()
    expect(screen.queryByText('Responsable')).not.toBeInTheDocument()
    expect(cabecera(api.llamadasA(rutaLeads())[0].init, 'Authorization')).toBe(`Bearer ${TOKEN_FICTICIO}`)
  })

  it('filtrar por estado pide ?estado=ABIERTO', async () => {
    const { api, user } = abrirApp('#/contactos', ['ASESOR'], {
      [rutaLeads()]: paginaLeads([lead(1)]),
      [rutaLeads('estado=ABIERTO')]: paginaLeads([]),
    })
    await screen.findByText('Visitante 1')
    await user.selectOptions(screen.getByLabelText('Estado'), 'ABIERTO')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(await screen.findByText('No hay contactos con estos filtros.')).toBeInTheDocument()
    expect(api.llamadasA(rutaLeads('estado=ABIERTO'))).toHaveLength(1)
  })

  it('sin contactos: mensaje para el asesor', async () => {
    abrirApp('#/contactos', ['ASESOR'], { [rutaLeads()]: paginaLeads([]) })
    expect(await screen.findByText(/Aún no tienes contactos/)).toBeInTheDocument()
  })

  it('paginación: 45 contactos en 3 páginas', async () => {
    const { api, user } = abrirApp('#/contactos', ['ASESOR'], {
      [rutaLeads()]: paginaLeads([lead(1)], 45),
      '/api/leads?pagina=2&tamano=20': paginaLeads([lead(21)], 45, 2),
    })
    expect(await screen.findByText('Página 1 de 3')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Siguiente' }))
    expect(await screen.findByText('Página 2 de 3')).toBeInTheDocument()
    expect(api.llamadasA('/api/leads?pagina=2&tamano=20')).toHaveLength(1)
  })

  it('un 401 de la lista termina la sesión', async () => {
    abrirApp('#/contactos', ['ASESOR'], { [rutaLeads()]: { status: 401 } })
    expect(await screen.findByText('Tu sesión terminó. Inicia sesión de nuevo.')).toBeInTheDocument()
  })

  it('un 403 (asesor sin perfil) muestra el mensaje de permiso', async () => {
    abrirApp('#/contactos', ['ASESOR'], {
      [rutaLeads()]: { status: 403, body: { error: 'No tienes permiso para realizar esta acción.' } },
    })
    expect(await screen.findByText('No tienes permiso para realizar esta acción.')).toBeInTheDocument()
  })
})

const RUTAS_ADMIN: Rutas = { [RUTA_CATALOGOS_INV]: CATALOGOS_INVENTARIO }

describe('Lista del administrador (CA-P5-31)', () => {
  it('"Contactos" con responsable en cada tarjeta y filtros por estado, responsable y fechas', async () => {
    const filtrada = '/api/leads?estado=ABIERTO&responsable=ADMINISTRACION&desde=2026-11-01&hasta=2026-11-04&pagina=1&tamano=20'
    const { api, user } = abrirApp('#/contactos', ['ADMINISTRADOR'], {
      ...RUTAS_ADMIN,
      [rutaLeads()]: paginaLeads([lead(3, { responsable: { tipo: 'ADMINISTRACION', asesorId: null, nombre: null } })]),
      [filtrada]: paginaLeads([]),
    })
    const tarjeta = (await screen.findByText('Visitante 3')).closest('li') as HTMLElement
    expect(screen.getByRole('heading', { level: 1, name: 'Contactos' })).toBeInTheDocument()
    expect(within(tarjeta).getByText('Administración')).toBeInTheDocument()

    const responsable = screen.getByLabelText('Responsable')
    await waitFor(() => expect(within(responsable).getByRole('option', { name: 'Asesor Uno' })).toBeInTheDocument())
    expect(within(responsable).getByRole('option', { name: 'Administración' })).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Estado'), 'ABIERTO')
    await user.selectOptions(responsable, 'ADMINISTRACION')
    await user.type(screen.getByLabelText('Desde'), '2026-11-01')
    await user.type(screen.getByLabelText('Hasta'), '2026-11-04')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => expect(api.llamadasA(filtrada)).toHaveLength(1))
  })

  it('"Hasta" anterior a "Desde": error junto al campo, foco y sin llamar a la API', async () => {
    const { api, user } = abrirApp('#/contactos', ['ADMINISTRADOR'], { ...RUTAS_ADMIN, [rutaLeads()]: paginaLeads([]) })
    await screen.findByText('Aún no hay contactos.')
    await user.type(screen.getByLabelText('Desde'), '2026-11-04')
    await user.type(screen.getByLabelText('Hasta'), '2026-11-01')
    const antes = llamadasLeads(api).length
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(screen.getByLabelText('Hasta')).toHaveAccessibleDescription('La fecha final debe ser igual o posterior a la inicial.')
    expect(screen.getByLabelText('Hasta')).toHaveFocus()
    expect(llamadasLeads(api).length).toBe(antes)
  })
})

const RUTA_DETALLE = '/api/leads/5'

describe('Detalle (asesor): historial, notas y cierre', () => {
  it('CA-P5-42 historial en orden con actor y automático; sin autorizaciones ni acciones de administrador', async () => {
    abrirApp('#/contactos/5', ['ASESOR'], { [RUTA_DETALLE]: { status: 200, body: detalleLead(5) } })
    expect(await screen.findByRole('heading', { level: 1, name: 'Contacto de Visitante 5' })).toBeInTheDocument()
    const historial = screen.getByRole('heading', { name: 'Historial' }).parentElement as HTMLElement
    const eventos = within(historial).getAllByRole('listitem')
    expect(eventos.map((e) => e.querySelector('strong')?.textContent)).toEqual([
      'Contacto recibido',
      'Asignado',
      'Reasignado',
      'Nota',
    ])
    expect(eventos[1]).toHaveTextContent('A Asesor Uno')
    expect(eventos[2]).toHaveTextContent('(automático)')
    expect(eventos[2]).toHaveTextContent('Sistema')
    expect(eventos[2]).toHaveTextContent('De Asesor Uno a Administración')
    expect(eventos[2]).toHaveTextContent('Plan vencido')
    expect(eventos[3]).toHaveTextContent('Persona Ficticia')
    expect(eventos[3]).toHaveTextContent('Llamé y no contestó.')
    expect(screen.getByText('Pendiente de gestión')).toBeInTheDocument()
    expect(screen.getByText('Activo')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Autorización de tratamiento de datos' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reasignar' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reabrir' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Volver a la lista de contactos' })).toHaveAttribute('href', '#/contactos')
    // CA-P5-43: los datos del visitante no se editan.
    expect(screen.queryByRole('textbox', { name: /Nombre|Teléfono|Correo/ })).not.toBeInTheDocument()
  })

  it('CA-P5-37 añadir una nota: POST con el texto, detalle actualizado y mensaje', async () => {
    const conNota = detalleLead(5, {
      eventos: [
        ...detalleLead(5).eventos,
        { tipo: 'NOTA', fecha: '2026-11-06T09:00:00Z', actor: { tipo: 'USUARIO', nombre: 'Persona Ficticia' }, automatico: false, texto: 'Quedó de llamar el lunes.', resultado: null, responsableAnterior: null, responsableNuevo: null },
      ],
    })
    const { api, user } = abrirApp('#/contactos/5', ['ASESOR'], {
      [RUTA_DETALLE]: { status: 200, body: detalleLead(5) },
      '/api/leads/5/notas': { status: 201, body: conNota },
    })
    await screen.findByRole('heading', { level: 1, name: 'Contacto de Visitante 5' })
    await user.type(screen.getByLabelText('Nueva nota'), 'Quedó de llamar el lunes.')
    await user.click(screen.getByRole('button', { name: 'Guardar nota' }))
    expect(await screen.findByText('Nota guardada en el historial.')).toBeInTheDocument()
    expect(cuerpo(api.llamadas, '/api/leads/5/notas')).toEqual({ texto: 'Quedó de llamar el lunes.' })
    expect(screen.getAllByText('Quedó de llamar el lunes.').length).toBeGreaterThan(0)
    expect(screen.getByLabelText('Nueva nota')).toHaveValue('')
  })

  it('nota corta: error en el campo y sin llamar a la API', async () => {
    const { api, user } = abrirApp('#/contactos/5', ['ASESOR'], { [RUTA_DETALLE]: { status: 200, body: detalleLead(5) } })
    await screen.findByRole('heading', { level: 1, name: 'Contacto de Visitante 5' })
    await user.type(screen.getByLabelText('Nueva nota'), 'ok')
    await user.click(screen.getByRole('button', { name: 'Guardar nota' }))
    expect(screen.getByLabelText('Nueva nota')).toHaveAccessibleDescription(/La nota debe tener entre 5 y 500 caracteres\./)
    expect(screen.getByLabelText('Nueva nota')).toHaveFocus()
    expect(api.llamadasA('/api/leads/5/notas')).toHaveLength(0)
  })

  it('CA-P5-57 un 409 al anotar muestra el mensaje de la API y vuelve a leer el contacto', async () => {
    const { api, user } = abrirApp('#/contactos/5', ['ASESOR'], {
      [RUTA_DETALLE]: [
        { status: 200, body: detalleLead(5) },
        { status: 200, body: detalleLead(5, { estado: 'CERRADO', resultadoCierre: 'NO_INTERESADO' }) },
      ],
      '/api/leads/5/notas': { status: 409, body: { error: 'Este contacto está cerrado.' } },
    })
    await screen.findByRole('heading', { level: 1, name: 'Contacto de Visitante 5' })
    await user.type(screen.getByLabelText('Nueva nota'), 'Una nota válida')
    await user.click(screen.getByRole('button', { name: 'Guardar nota' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Este contacto está cerrado.')
    await waitFor(() => expect(api.llamadasA(RUTA_DETALLE)).toHaveLength(2))
    expect(await screen.findByText('Finalizado')).toBeInTheDocument()
    expect(screen.queryByLabelText('Nueva nota')).not.toBeInTheDocument()
  })

  it('CA-P5-39 cerrar con "Otro" exige observación; luego cierra y muestra "Finalizado"', async () => {
    const cerrado = detalleLead(5, {
      estado: 'CERRADO',
      resultadoCierre: 'OTRO',
      observacionCierre: 'Compró en otro lado.',
      cerradoPor: 'Persona Ficticia',
      cerradoEn: '2026-11-06T09:00:00Z',
    })
    const { api, user } = abrirApp('#/contactos/5', ['ASESOR'], {
      [RUTA_DETALLE]: { status: 200, body: detalleLead(5) },
      '/api/leads/5/cerrar': { status: 200, body: cerrado },
    })
    await screen.findByRole('heading', { level: 1, name: 'Contacto de Visitante 5' })
    await user.click(screen.getByRole('button', { name: 'Cerrar contacto' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Cerrar contacto' })
    await waitFor(() => expect(within(dialogo).getByLabelText('Resultado')).toHaveFocus())

    await user.click(within(dialogo).getByRole('button', { name: 'Cerrar contacto' }))
    expect(within(dialogo).getByLabelText('Resultado')).toHaveAccessibleDescription('Elige el resultado del cierre.')

    await user.selectOptions(within(dialogo).getByLabelText('Resultado'), 'OTRO')
    await user.click(within(dialogo).getByRole('button', { name: 'Cerrar contacto' }))
    expect(within(dialogo).getByLabelText('Observación')).toHaveAccessibleDescription(/Escribe la observación \(5 a 500 caracteres\)\./)
    expect(api.llamadasA('/api/leads/5/cerrar')).toHaveLength(0)

    await user.type(within(dialogo).getByLabelText('Observación'), 'Compró en otro lado.')
    await user.click(within(dialogo).getByRole('button', { name: 'Cerrar contacto' }))
    expect(await screen.findByText('Contacto cerrado: Otro.')).toBeInTheDocument()
    expect(cuerpo(api.llamadas, '/api/leads/5/cerrar')).toEqual({ resultado: 'OTRO', observacion: 'Compró en otro lado.' })
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByText('Finalizado')).toBeInTheDocument()
    expect(screen.getByText(/Compró en otro lado\./)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reabrir' })).not.toBeInTheDocument()
  })

  it('cerrar con un resultado distinto de "Otro" no exige observación (va como null)', async () => {
    const { api, user } = abrirApp('#/contactos/5', ['ASESOR'], {
      [RUTA_DETALLE]: { status: 200, body: detalleLead(5) },
      '/api/leads/5/cerrar': { status: 200, body: detalleLead(5, { estado: 'CERRADO', resultadoCierre: 'VENTA_CONCRETADA' }) },
    })
    await screen.findByRole('heading', { level: 1, name: 'Contacto de Visitante 5' })
    await user.click(screen.getByRole('button', { name: 'Cerrar contacto' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Cerrar contacto' })
    await user.selectOptions(within(dialogo).getByLabelText('Resultado'), 'VENTA_CONCRETADA')
    expect(within(dialogo).getByLabelText('Observación (opcional)')).not.toHaveAttribute('aria-required')
    await user.click(within(dialogo).getByRole('button', { name: 'Cerrar contacto' }))
    expect(await screen.findByText('Contacto cerrado: Se concretó la venta.')).toBeInTheDocument()
    expect(cuerpo(api.llamadas, '/api/leads/5/cerrar')).toEqual({ resultado: 'VENTA_CONCRETADA', observacion: null })
  })

  it('Escape cierra el diálogo y el foco vuelve al botón', async () => {
    const { user } = abrirApp('#/contactos/5', ['ASESOR'], { [RUTA_DETALLE]: { status: 200, body: detalleLead(5) } })
    await screen.findByRole('heading', { level: 1, name: 'Contacto de Visitante 5' })
    const boton = screen.getByRole('button', { name: 'Cerrar contacto' })
    await user.click(boton)
    await screen.findByRole('dialog')
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await waitFor(() => expect(boton).toHaveFocus())
  })

  it('un 404 (contacto ajeno o reasignado) muestra "Contacto no encontrado."', async () => {
    abrirApp('#/contactos/5', ['ASESOR'], { [RUTA_DETALLE]: { status: 404, body: { error: 'Contacto no encontrado.' } } })
    expect(await screen.findByText('Contacto no encontrado.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument()
  })

  it('"Ver todos los contactos de este vehículo" filtra la lista por vehiculoId', async () => {
    const { api, user } = abrirApp('#/contactos/5', ['ASESOR'], {
      [RUTA_DETALLE]: { status: 200, body: detalleLead(5) },
      '/api/leads?vehiculoId=40&pagina=1&tamano=20': paginaLeads([lead(5)]),
      [rutaLeads()]: paginaLeads([lead(5), lead(6)]),
    })
    await screen.findByRole('heading', { level: 1, name: 'Contacto de Visitante 5' })
    await user.click(screen.getByRole('button', { name: 'Ver todos los contactos de este vehículo' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Mis contactos' })).toBeInTheDocument()
    await waitFor(() => expect(api.llamadasA('/api/leads?vehiculoId=40&pagina=1&tamano=20')).toHaveLength(1))
    await user.click(await screen.findByRole('button', { name: 'Ver todos los vehículos' }))
    await waitFor(() => expect(api.llamadasA(rutaLeads())).toHaveLength(1))
  })
})

describe('Detalle (administrador): autorizaciones, reasignar y reabrir', () => {
  it('CA-P5-16 el administrador ve la prueba de autorización', async () => {
    abrirApp('#/contactos/5', ['ADMINISTRADOR'], {
      ...RUTAS_ADMIN,
      [RUTA_DETALLE]: { status: 200, body: detalleLead(5, { consentimientos: CONSENTIMIENTOS }) },
    })
    const titulo = await screen.findByRole('heading', { name: 'Autorización de tratamiento de datos' })
    const seccion = titulo.parentElement as HTMLElement
    expect(seccion).toHaveTextContent('Contacto desde la ficha del vehículo')
    expect(seccion).toHaveTextContent('BORRADOR-2026-11-04')
  })

  it('CA-P5-44 reasignar a otro asesor: destino obligatorio, motivo obligatorio, POST con asesorId', async () => {
    const reasignado = detalleLead(5, { responsable: { tipo: 'ASESOR', asesorId: 8, nombre: 'Asesor Dos' } })
    const { api, user } = abrirApp('#/contactos/5', ['ADMINISTRADOR'], {
      ...RUTAS_ADMIN,
      [RUTA_DETALLE]: { status: 200, body: detalleLead(5) },
      '/api/leads/5/reasignar': { status: 200, body: reasignado },
    })
    await screen.findByRole('heading', { level: 1, name: 'Contacto de Visitante 5' })
    await user.click(screen.getByRole('button', { name: 'Reasignar' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Reasignar contacto' })
    const destino = within(dialogo).getByLabelText('Nuevo responsable')
    // El responsable actual (Asesor Uno, id 7) no se ofrece.
    expect(within(destino).queryByRole('option', { name: 'Asesor Uno' })).not.toBeInTheDocument()
    expect(within(destino).getByRole('option', { name: 'Administración' })).toBeInTheDocument()

    // El motivo se valida primero (DialogoConfirmacion); luego el destino.
    await user.click(within(dialogo).getByRole('button', { name: 'Reasignar' }))
    expect(within(dialogo).getByLabelText('Motivo')).toHaveAccessibleDescription(/Escribe el motivo \(5 a 500 caracteres\)\./)
    await user.type(within(dialogo).getByLabelText('Motivo'), 'Conoce mejor la zona')
    await user.click(within(dialogo).getByRole('button', { name: 'Reasignar' }))
    expect(destino).toHaveAccessibleDescription('Elige el nuevo responsable.')
    await waitFor(() => expect(destino).toHaveFocus())
    expect(api.llamadasA('/api/leads/5/reasignar')).toHaveLength(0)
    await user.selectOptions(destino, '8')
    await user.click(within(dialogo).getByRole('button', { name: 'Reasignar' }))
    expect(await screen.findByText('Contacto reasignado a Asesor Dos.')).toBeInTheDocument()
    expect(cuerpo(api.llamadas, '/api/leads/5/reasignar')).toEqual({ asesorId: 8, motivo: 'Conoce mejor la zona' })
  })

  it('reasignar a Administración envía administracion: true; un 409 se muestra con el texto de la API', async () => {
    const { api, user } = abrirApp('#/contactos/5', ['ADMINISTRADOR'], {
      ...RUTAS_ADMIN,
      [RUTA_DETALLE]: { status: 200, body: detalleLead(5) },
      '/api/leads/5/reasignar': { status: 409, body: { error: 'El contacto ya está asignado a ese responsable.' } },
    })
    await screen.findByRole('heading', { level: 1, name: 'Contacto de Visitante 5' })
    await user.click(screen.getByRole('button', { name: 'Reasignar' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Reasignar contacto' })
    await user.selectOptions(within(dialogo).getByLabelText('Nuevo responsable'), 'ADMINISTRACION')
    await user.type(within(dialogo).getByLabelText('Motivo'), 'Lo atiende la oficina')
    await user.click(within(dialogo).getByRole('button', { name: 'Reasignar' }))
    expect(await screen.findByText('El contacto ya está asignado a ese responsable.')).toHaveAttribute('role', 'alert')
    expect(cuerpo(api.llamadas, '/api/leads/5/reasignar')).toEqual({ administracion: true, motivo: 'Lo atiende la oficina' })
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('un 400 del servidor en asesorId se muestra en el diálogo sin cerrarlo', async () => {
    const { user } = abrirApp('#/contactos/5', ['ADMINISTRADOR'], {
      ...RUTAS_ADMIN,
      [RUTA_DETALLE]: { status: 200, body: detalleLead(5) },
      '/api/leads/5/reasignar': {
        status: 400,
        body: { error: 'Revisa los datos marcados.', errores: { asesorId: ['Elige un asesor aprobado.'] } },
      },
    })
    await screen.findByRole('heading', { level: 1, name: 'Contacto de Visitante 5' })
    await user.click(screen.getByRole('button', { name: 'Reasignar' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Reasignar contacto' })
    await user.selectOptions(within(dialogo).getByLabelText('Nuevo responsable'), '8')
    await user.type(within(dialogo).getByLabelText('Motivo'), 'Motivo suficiente')
    await user.click(within(dialogo).getByRole('button', { name: 'Reasignar' }))
    expect(await within(dialogo).findByText('Elige un asesor aprobado.')).toBeInTheDocument()
    await waitFor(() => expect(within(dialogo).getByLabelText('Nuevo responsable')).toHaveFocus())
  })

  it('CA-P5-41 reabrir un contacto cerrado con motivo', async () => {
    const cerrado = detalleLead(5, { estado: 'CERRADO', resultadoCierre: 'NO_RESPONDIO', cerradoEn: '2026-11-06T09:00:00Z' })
    const { api, user } = abrirApp('#/contactos/5', ['ADMINISTRADOR'], {
      ...RUTAS_ADMIN,
      [RUTA_DETALLE]: { status: 200, body: cerrado },
      '/api/leads/5/reabrir': { status: 200, body: detalleLead(5) },
    })
    await screen.findByRole('heading', { level: 1, name: 'Contacto de Visitante 5' })
    expect(screen.queryByRole('button', { name: 'Cerrar contacto' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reasignar' })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Reabrir' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Reabrir contacto' })
    await user.type(within(dialogo).getByLabelText('Motivo'), 'Volvió a escribir')
    await user.click(within(dialogo).getByRole('button', { name: 'Reabrir' }))
    expect(await screen.findByText('Contacto reabierto. Vuelve a estar pendiente de gestión.')).toBeInTheDocument()
    expect(cuerpo(api.llamadas, '/api/leads/5/reabrir')).toEqual({ motivo: 'Volvió a escribir' })
    expect(screen.getByText('Pendiente de gestión')).toBeInTheDocument()
  })

  it('un asesor con un contacto cerrado no ve "Reabrir"', async () => {
    abrirApp('#/contactos/5', ['ASESOR'], {
      [RUTA_DETALLE]: { status: 200, body: detalleLead(5, { estado: 'CERRADO', resultadoCierre: 'NO_RESPONDIO' }) },
    })
    await screen.findByText('Finalizado')
    expect(screen.queryByRole('button', { name: 'Reabrir' })).not.toBeInTheDocument()
  })
})
