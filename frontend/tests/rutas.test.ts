// Rutas sin `#` (P10: CA-P10-33, 34, 39, 41, 42, 46; antes P3: CA-001-06, CA-UI-01, CA-UI-02, CA-004-17, CA-012-02).
import { describe, expect, it } from 'vitest'
import { FILTROS_VACIOS } from '../src/api/catalogo'
import { codigoDelHash, codigoRecuperacionActual } from '../src/api/recuperacion'
import {
  convertirHashViejo,
  esRutaReservada,
  fijarTiposCatalogo,
  leerFiltros,
  leerRuta,
  PREFIJOS_RESERVADOS,
  rutaBuscar,
  rutaCategoria,
  rutaContacto,
  rutaCreditoVehiculo,
  rutaSeccionAdmin,
  rutaSinIndexar,
  rutaVehiculo,
  SECCIONES_ADMIN,
} from '../src/rutas'

const TIPOS = [
  { id: 1, nombre: 'Carro', total: 1 },
  { id: 2, nombre: 'Camión', total: 1 },
  { id: 3, nombre: 'Carrocería', total: 1 },
  { id: 4, nombre: 'Maquinaria', total: 1 },
  { id: 9, nombre: 'Moto', total: 1 },
]

describe('leerRuta (tabla de §4)', () => {
  it('CA-001-06 "/" y la cadena vacía abren el inicio', () => {
    expect(leerRuta('/')).toEqual({ nombre: 'inicio' })
    expect(leerRuta('')).toEqual({ nombre: 'inicio' })
  })

  it.each([
    ['/vehiculos', { nombre: 'buscar', filtros: FILTROS_VACIOS, categoria: null }],
    ['/vehiculo/chevrolet-npr-2020', { nombre: 'vehiculo', slug: 'chevrolet-npr-2020' }],
    ['/vehiculo/demo-chevrolet-npr-2019', { nombre: 'vehiculo', slug: 'demo-chevrolet-npr-2019' }],
    ['/cuenta', { nombre: 'cuenta', registro: false }],
    ['/cuenta/registro', { nombre: 'cuenta', registro: true }],
    ['/cuenta/usuarios?rol=ASESOR', { nombre: 'cuenta', registro: false, seccion: 'usuarios', consulta: 'rol=ASESOR' }],
    ['/inventario', { nombre: 'inventario' }],
    ['/inventario?estado=ACTIVO', { nombre: 'inventario', consulta: 'estado=ACTIVO' }],
    ['/inventario/nuevo', { nombre: 'inventarioNuevo' }],
    ['/inventario/31', { nombre: 'inventarioVehiculo', id: 31 }],
    ['/publicaciones', { nombre: 'publicaciones' }],
    ['/contactos', { nombre: 'contactos' }],
    ['/contactos/12', { nombre: 'contacto', id: 12 }],
    ['/ventas', { nombre: 'ventas' }],
    ['/privacidad', { nombre: 'privacidad' }],
    ['/terminos', { nombre: 'terminos' }],
    ['/vende-tu-camion', { nombre: 'vendeTuCamion' }],
    ['/seguro-todo-riesgo', { nombre: 'seguroTodoRiesgo' }],
    ['/credito', { nombre: 'credito', slug: null }],
    ['/credito/chevrolet-npr-2020', { nombre: 'credito', slug: 'chevrolet-npr-2020' }],
    ['/busco-un-vehiculo', { nombre: 'buscoVehiculo' }],
    ['/mis-solicitudes', { nombre: 'misSolicitudes' }],
    ['/mis-indicadores', { nombre: 'misIndicadores' }],
    ['/restablecer-contrasena', { nombre: 'restablecerContrasena' }],
    ['/restablecer-contrasena#codigo=ABC', { nombre: 'restablecerContrasena' }],
  ])('CA-P10-33 %s', (direccion, esperada) => {
    expect(leerRuta(direccion)).toEqual(esperada)
    // Con barra final, igual.
    const [camino, consulta] = direccion.split('?')
    if (!camino.includes('#')) expect(leerRuta(`${camino}/${consulta ? `?${consulta}` : ''}`)).toEqual(esperada)
  })

  it('todas las secciones del administrador', () => {
    for (const s of SECCIONES_ADMIN) expect(leerRuta(rutaSeccionAdmin(s))).toEqual({ nombre: 'cuenta', registro: false, seccion: s })
  })

  it('CA-P10-34 / CA-P9-48: desconocidas, mal codificadas, con mayúsculas o ids no válidos abren "no encontrada"', () => {
    for (const d of [
      '/xyz',
      '/buscar',
      '/vehiculo/',
      '/vehiculo/a/b',
      '/vehiculo/%E0%A4%A',
      '/Camiones',
      '/Vehiculos',
      '/contactos/0',
      '/contactos/abc',
      '/inventario/-1',
      '/cuenta/otra',
    ]) {
      expect(leerRuta(d)).toEqual({ nombre: 'noEncontrada' })
    }
  })

  it('CA-004-17 lee los filtros conocidos de la consulta e ignora los demás', () => {
    expect(leerRuta('/vehiculos?marcaId=10&precioMax=100000000&orden=precio_asc&otro=1&q=%20npr%20')).toEqual({
      nombre: 'buscar',
      filtros: { ...FILTROS_VACIOS, marcaId: '10', precioMax: '100000000', orden: 'precio_asc', q: 'npr' },
      categoria: null,
    })
    expect(leerFiltros('q=cami%C3%B3n+blanco').q).toBe('camión blanco')
  })
})

describe('Categorías (CA-P10-39, 40)', () => {
  it('sin tipos conocidos, /camiones se lee con tipoId vacío y rutaBuscar usa /vehiculos', () => {
    expect(leerRuta('/camiones')).toEqual({ nombre: 'buscar', filtros: FILTROS_VACIOS, categoria: 'camiones' })
    expect(rutaBuscar({ tipoId: '2' })).toBe('/vehiculos?tipoId=2')
  })

  it('con los tipos de /api/catalogo/filtros, /camiones ↔ tipoId 2 (y sus filtros en la consulta)', () => {
    fijarTiposCatalogo(TIPOS)
    expect(leerRuta('/camiones?marcaId=3&pagina=2')).toEqual({
      nombre: 'buscar',
      filtros: { ...FILTROS_VACIOS, tipoId: '2', marcaId: '3', pagina: '2' },
      categoria: 'camiones',
    })
    // Un tipoId en la consulta de una categoría se ignora: manda la ruta.
    expect(leerRuta('/carros?tipoId=2')).toMatchObject({ filtros: { tipoId: '1' } })
    expect(rutaBuscar({ tipoId: '2' })).toBe('/camiones')
    expect(rutaBuscar({ tipoId: '3', q: 'estacas', pagina: '1' })).toBe('/carrocerias?q=estacas')
    expect(rutaBuscar({ tipoId: '4' })).toBe('/maquinaria')
    expect(rutaBuscar({ tipoId: '1' })).toBe('/carros')
    // Un tipo que no es una de las cuatro categorías sigue en /vehiculos.
    expect(rutaBuscar({ tipoId: '9' })).toBe('/vehiculos?tipoId=9')
    expect(rutaCategoria('camiones', { tipoId: '2', marcaId: '3' })).toBe('/camiones?marcaId=3')
  })
})

describe('rutaBuscar / rutaVehiculo', () => {
  it('CA-012-02 sin filtros es "/vehiculos" sin parámetros', () => {
    expect(rutaBuscar()).toBe('/vehiculos')
    expect(rutaBuscar(FILTROS_VACIOS)).toBe('/vehiculos')
  })

  it('omite vacíos y pagina=1, en el orden de los filtros', () => {
    expect(rutaBuscar({ pagina: '1', marcaId: '10', q: ' npr ', tipoId: '' })).toBe('/vehiculos?q=npr&marcaId=10')
    expect(rutaBuscar({ orden: 'precio_asc', pagina: '2' })).toBe('/vehiculos?orden=precio_asc&pagina=2')
  })

  it('ida y vuelta: construir y leer da los mismos filtros', () => {
    const filtros = {
      ...FILTROS_VACIOS,
      q: 'npr',
      tipoId: '7',
      marcaId: '10',
      lineaId: '101',
      precioMin: '50000000',
      precioMax: '120000000',
      anioMin: '2015',
      anioMax: '2022',
      kmMax: '85000',
      orden: 'precio_desc',
      pagina: '2',
    }
    expect(leerRuta(rutaBuscar(filtros))).toEqual({ nombre: 'buscar', filtros, categoria: null })
  })

  it('el slug es opaco y se codifica con encodeURIComponent', () => {
    expect(rutaVehiculo('kenworth-t800-2018')).toBe('/vehiculo/kenworth-t800-2018')
    expect(leerRuta(rutaVehiculo('camión ñ'))).toEqual({ nombre: 'vehiculo', slug: 'camión ñ' })
    expect(leerRuta(rutaVehiculo('a/b?c#d'))).toEqual({ nombre: 'vehiculo', slug: 'a/b?c#d' })
    expect(rutaCreditoVehiculo('a b')).toBe('/credito/a%20b')
    expect(leerRuta(rutaContacto(12))).toEqual({ nombre: 'contacto', id: 12 })
  })
})

describe('Compatibilidad con enlaces viejos (CA-P10-41, 42)', () => {
  it.each([
    ['#/', '/'],
    ['#/vehiculo/x', '/vehiculo/x'],
    ['#/buscar', '/vehiculos'],
    ['#/buscar?marcaId=3', '/vehiculos?marcaId=3'],
    ['#/cuenta/usuarios?rol=ASESOR', '/cuenta/usuarios?rol=ASESOR'],
    ['#/credito/x', '/credito/x'],
    ['#/no-existe', '/no-existe'],
  ])('%s → %s', (hash, nueva) => {
    expect(convertirHashViejo(hash)).toBe(nueva)
  })

  it('#/buscar?tipoId=N lleva a la categoría cuando se conocen los tipos', () => {
    fijarTiposCatalogo(TIPOS)
    expect(convertirHashViejo('#/buscar?tipoId=2')).toBe('/camiones')
    expect(convertirHashViejo('#/buscar?tipoId=2&marcaId=5')).toBe('/camiones?marcaId=5')
  })

  it('B-1: un camino que empieza por "//" o "/\\" (otro origen) lleva a la portada', () => {
    expect(convertirHashViejo('#//evil.example')).toBe('/')
    expect(convertirHashViejo('#//evil.example/x?y=1')).toBe('/')
    expect(convertirHashViejo('#/\\evil.example')).toBe('/')
    expect(convertirHashViejo('#/\\/evil.example')).toBe('/')
    // Una barra doble en medio no es otro origen.
    expect(convertirHashViejo('#/vehiculo//x')).toBe('/vehiculo//x')
  })

  it('un hash que no empieza por "#/" no se toca', () => {
    expect(convertirHashViejo('')).toBeNull()
    expect(convertirHashViejo('#contenido')).toBeNull()
    expect(convertirHashViejo('#codigo=ABC')).toBeNull()
  })

  it('el enlace viejo de recuperación da la ruta sin el código y lo guarda solo en memoria', () => {
    const nueva = convertirHashViejo('#/restablecer-contrasena?codigo=ABC-123')
    expect(nueva).toBe('/restablecer-contrasena')
    expect(nueva).not.toContain('ABC')
    expect(codigoRecuperacionActual()).toBe('ABC-123')
  })
})

describe('Código de recuperación (CA-P10-43)', () => {
  it('codigoDelHash lee "#codigo=" y el formato viejo', () => {
    expect(codigoDelHash('#codigo=ABC')).toBe('ABC')
    expect(codigoDelHash('#codigo=A%2DB')).toBe('A-B')
    expect(codigoDelHash('#/restablecer-contrasena?codigo=XYZ')).toBe('XYZ')
    expect(codigoDelHash('#otro=1')).toBe('')
    expect(codigoDelHash('')).toBe('')
  })
})

describe('Prefijos reservados y noindex', () => {
  it('CA-P10-46: ninguna ruta del portal empieza por un prefijo reservado', () => {
    expect([...PREFIJOS_RESERVADOS]).toEqual([
      '/api',
      '/media',
      '/health',
      '/assets',
      '/marca',
      '/sitemap.xml',
      '/robots.txt',
      '/favicon',
      '/site.webmanifest',
    ])
    for (const r of ['/api/x', '/media/a.jpg', '/health', '/health/ready', '/assets/i.js', '/marca/logo.png', '/sitemap.xml', '/robots.txt', '/favicon.ico', '/site.webmanifest'])
      expect(esRutaReservada(r)).toBe(true)
    for (const r of ['/', '/vehiculos', '/camiones', '/vehiculo/api', '/cuenta', '/apicultura'])
      expect(esRutaReservada(r)).toBe(false)
    for (const p of PREFIJOS_RESERVADOS) expect(leerRuta(p)).toEqual({ nombre: 'noEncontrada' })
  })

  it('CA-P10-30: privadas, restablecer, no encontrada y /credito/<slug> llevan noindex; las públicas no', () => {
    for (const d of ['/cuenta', '/cuenta/usuarios', '/inventario', '/publicaciones', '/contactos/3', '/ventas', '/mis-solicitudes', '/mis-indicadores', '/restablecer-contrasena', '/x', '/credito/x'])
      expect(rutaSinIndexar(leerRuta(d))).toBe(true)
    for (const d of ['/', '/vehiculos', '/camiones', '/vehiculo/x', '/credito', '/vende-tu-camion', '/seguro-todo-riesgo', '/busco-un-vehiculo'])
      expect(rutaSinIndexar(leerRuta(d))).toBe(false)
  })
})
