// P4a: reglas por categoría (D-17, D-19, tabla del contrato), validación en cliente,
// cuerpo enviado y estados. Módulos puros: sin React ni red.
import { describe, expect, it } from 'vitest'
import type { CatalogosInventario } from '../src/api/inventario'
import { validarArchivoFoto, MAX_BYTES_FOTO } from '../src/api/inventario'
import {
  accionesEstado,
  esEditable,
  nombreEstado,
  tituloVehiculo,
  validarMotivoInactivar,
} from '../src/inventario/estados'
import {
  construirCuerpo,
  formularioVacio,
  leerEntero,
  leerMedida,
  leerPrecio,
  MENSAJES_CAMPO,
  normalizarPlaca,
  placaValida,
  repartirErroresServidor,
  validarFormulario,
  type FormularioVehiculo,
} from '../src/inventario/formulario'
import {
  aplica,
  camposAplicables,
  categoriaDeTipo,
  esObligatorio,
  etiquetaConObligatoriedad,
  mensajeNoAplica,
  REGLAS_CATEGORIA,
  type Categoria,
  type CampoVehiculo,
} from '../src/inventario/reglasCategoria'
import { CAT } from './inventarioDatos'

const CATALOGOS: CatalogosInventario = { ...CAT, asesores: [] }

// Tabla del contrato (docs/criterios/P4a-contrato-api.md), escrita aquí de forma
// independiente: O obligatorio, P opcional, N no se admite.
const TABLA: Record<CampoVehiculo, [string, string, string, string]> = {
  //                   Carro Camión Carrocería Maquinaria
  marcaId: ['O', 'O', 'P', 'O'],
  lineaId: ['P', 'P', 'P', 'P'],
  modeloAnio: ['O', 'O', 'P', 'O'],
  kilometraje: ['O', 'O', 'N', 'N'],
  horasUso: ['N', 'N', 'N', 'P'],
  placa: ['P', 'P', 'N', 'P'],
  carroceriaId: ['P', 'P', 'O', 'N'],
  largoM: ['N', 'N', 'P', 'N'],
  anchoM: ['N', 'N', 'P', 'N'],
  altoM: ['N', 'N', 'P', 'N'],
  materialId: ['N', 'N', 'P', 'N'],
  tipoMaquinaId: ['N', 'N', 'N', 'O'],
  combustibleId: ['P', 'P', 'N', 'P'],
  cilindrajeCc: ['P', 'P', 'N', 'P'],
  pesoBrutoKg: ['P', 'P', 'N', 'P'],
  capacidadCargaKg: ['N', 'P', 'P', 'P'],
  municipioId: ['O', 'O', 'O', 'O'],
  precioCop: ['O', 'O', 'O', 'O'],
  descripcionTecnica: ['P', 'P', 'P', 'P'],
}
const CATEGORIAS: Categoria[] = ['CARRO', 'CAMION', 'CARROCERIA', 'MAQUINARIA']
const REGLA = { O: 'obligatorio', P: 'opcional', N: 'no' } as const

function formulario(datos: Partial<FormularioVehiculo>): FormularioVehiculo {
  return { ...formularioVacio(), ...datos }
}

/** Formulario lleno con TODOS los campos (incluso los que no aplican a la categoría). */
function formularioCompleto(tipoId: string): FormularioVehiculo {
  return formulario({
    tipoId,
    marcaId: '10',
    lineaId: '100',
    modeloAnio: '2020',
    kilometraje: '85000',
    horasUso: '1200',
    placa: 'abc-123',
    carroceriaId: '5',
    largoM: '6,5',
    anchoM: '2.4',
    altoM: '2,6',
    materialId: '6',
    tipoMaquinaId: '7',
    combustibleId: '8',
    cilindrajeCc: '4500',
    pesoBrutoKg: '7500',
    capacidadCargaKg: '3000',
    departamentoId: '20',
    municipioId: '200',
    precioCop: '120000000',
    descripcionTecnica: '  Motor revisado  ',
  })
}

const OPC = { pedirAsesor: false, anioActual: 2026, catalogos: CATALOGOS }

describe('Reglas por categoría (CA-UI-05, D-17, D-19)', () => {
  for (const [i, cat] of CATEGORIAS.entries()) {
    it(`${cat}: cada campo coincide con la tabla del contrato`, () => {
      for (const [campo, fila] of Object.entries(TABLA) as [CampoVehiculo, string[]][]) {
        expect(REGLAS_CATEGORIA[cat][campo], `${cat}.${campo}`).toBe(REGLA[fila[i] as 'O' | 'P' | 'N'])
        expect(aplica(cat, campo)).toBe(fila[i] !== 'N')
        expect(esObligatorio(cat, campo)).toBe(fila[i] === 'O')
      }
    })
  }

  it('Carrocería: sin placa ni kilometraje; tipo de carrocería obligatorio y marca opcional', () => {
    const campos = camposAplicables('CARROCERIA')
    expect(campos).not.toContain('placa')
    expect(campos).not.toContain('kilometraje')
    expect(campos).not.toContain('horasUso')
    expect(etiquetaConObligatoriedad('CARROCERIA', 'carroceriaId')).toBe('Tipo de carrocería')
    expect(etiquetaConObligatoriedad('CARROCERIA', 'marcaId')).toBe('Marca (fabricante) (opcional)')
  })

  it('Maquinaria: horas de uso y tipo de máquina, sin kilometraje ni carrocería', () => {
    const campos = camposAplicables('MAQUINARIA')
    expect(campos).toEqual(expect.arrayContaining(['horasUso', 'tipoMaquinaId', 'marcaId', 'modeloAnio']))
    expect(campos).not.toContain('kilometraje')
    expect(campos).not.toContain('carroceriaId')
    expect(etiquetaConObligatoriedad('MAQUINARIA', 'horasUso')).toBe('Horas de uso (horómetro) (opcional)')
  })

  it('categoriaDeTipo reconoce los nombres con y sin tilde y rechaza otros', () => {
    expect(categoriaDeTipo('Camión')).toBe('CAMION')
    expect(categoriaDeTipo('camion')).toBe('CAMION')
    expect(categoriaDeTipo('CARROCERÍA')).toBe('CARROCERIA')
    expect(categoriaDeTipo('Maquinaria')).toBe('MAQUINARIA')
    expect(categoriaDeTipo('Carro')).toBe('CARRO')
    expect(categoriaDeTipo('Moto')).toBeNull()
    expect(categoriaDeTipo(null)).toBeNull()
  })

  it('mensaje del contrato para un dato que no aplica', () => {
    expect(mensajeNoAplica('CARROCERIA')).toBe('Este dato no aplica a Carrocería.')
    expect(mensajeNoAplica('CAMION')).toBe('Este dato no aplica a Camión.')
  })
})

describe('Cuerpo enviado: solo campos aplicables (CA-UI-06, CA-CAT-11)', () => {
  const TIPO: Record<Categoria, string> = { CARRO: '1', CAMION: '2', CARROCERIA: '3', MAQUINARIA: '4' }
  for (const cat of CATEGORIAS) {
    it(`${cat}: ningún campo "no se admite" va en el cuerpo, ni siquiera como null`, () => {
      const cuerpo = construirCuerpo(formularioCompleto(TIPO[cat]), cat, false) as Record<string, unknown>
      for (const campo of Object.keys(TABLA) as CampoVehiculo[]) {
        if (aplica(cat, campo)) expect(campo in cuerpo, `${cat} debe enviar ${campo}`).toBe(true)
        else expect(campo in cuerpo, `${cat} no debe enviar ${campo}`).toBe(false)
      }
      expect('asesorId' in cuerpo).toBe(false)
      expect('departamentoId' in cuerpo).toBe(false)
    })
  }

  it('Camión: convierte tipos, normaliza la placa y recorta la descripción', () => {
    const cuerpo = construirCuerpo(formularioCompleto('2'), 'CAMION', false)
    expect(cuerpo).toMatchObject({
      tipoId: 2,
      marcaId: 10,
      lineaId: 100,
      modeloAnio: 2020,
      kilometraje: 85000,
      placa: 'ABC123',
      municipioId: 200,
      precioCop: 120000000,
      descripcionTecnica: 'Motor revisado',
    })
  })

  it('Carrocería: medidas con coma o punto como número decimal', () => {
    const cuerpo = construirCuerpo(formularioCompleto('3'), 'CARROCERIA', false)
    expect(cuerpo).toMatchObject({ largoM: 6.5, anchoM: 2.4, altoM: 2.6, materialId: 6, carroceriaId: 5 })
  })

  it('placa vacía se envía como null; asesorId solo si lo pide el administrador', () => {
    const f = formulario({ ...formularioCompleto('2'), placa: '', asesorId: '5' })
    expect(construirCuerpo(f, 'CAMION', false).placa).toBeNull()
    expect(construirCuerpo(f, 'CAMION', false)).not.toHaveProperty('asesorId')
    expect(construirCuerpo(f, 'CAMION', true).asesorId).toBe(5)
  })
})

describe('Validación en cliente', () => {
  it('sin categoría solo pide la categoría', () => {
    expect(validarFormulario(formularioVacio(), null, OPC)).toEqual({ tipoId: [MENSAJES_CAMPO.tipoId] })
  })

  it('Camión vacío: marca, año, kilometraje, departamento, municipio y precio', () => {
    const e = validarFormulario(formulario({ tipoId: '2' }), 'CAMION', OPC)
    expect(Object.keys(e).sort()).toEqual(
      ['departamentoId', 'kilometraje', 'marcaId', 'modeloAnio', 'municipioId', 'precioCop'].sort(),
    )
    expect(e.modeloAnio).toEqual(['Escribe un año entre 1950 y 2027.'])
  })

  it('Carrocería vacía: no pide marca, año ni kilometraje; sí el tipo de carrocería', () => {
    const e = validarFormulario(formulario({ tipoId: '3' }), 'CARROCERIA', OPC)
    expect(e.carroceriaId).toEqual([MENSAJES_CAMPO.carroceriaId])
    expect(e.marcaId).toBeUndefined()
    expect(e.modeloAnio).toBeUndefined()
    expect(e.kilometraje).toBeUndefined()
  })

  it('Maquinaria vacía: pide tipo de máquina, marca y año; horas opcionales', () => {
    const e = validarFormulario(formulario({ tipoId: '4' }), 'MAQUINARIA', OPC)
    expect(e.tipoMaquinaId).toEqual([MENSAJES_CAMPO.tipoMaquinaId])
    expect(e.marcaId).toEqual([MENSAJES_CAMPO.marcaId])
    expect(e.modeloAnio).toBeDefined()
    expect(e.horasUso).toBeUndefined()
  })

  it('un campo que no aplica no se valida aunque tenga basura', () => {
    const f = formulario({ ...formularioCompleto('3'), kilometraje: 'xx', placa: '@@', horasUso: '-1' })
    expect(validarFormulario(f, 'CARROCERIA', OPC)).toEqual({})
  })

  it('año: 1949 y 2028 rechazados; 1950 y 2027 aceptados (tope año actual + 1)', () => {
    for (const [anio, ok] of [['1949', false], ['2028', false], ['1950', true], ['2027', true]] as const) {
      const e = validarFormulario(formulario({ ...formularioCompleto('2'), modeloAnio: anio }), 'CAMION', OPC)
      expect(e.modeloAnio === undefined, anio).toBe(ok)
    }
  })

  it('la línea debe ser de la marca elegida', () => {
    const f = formulario({ ...formularioCompleto('2'), marcaId: '11', lineaId: '100' })
    expect(validarFormulario(f, 'CAMION', OPC).lineaId).toEqual([MENSAJES_CAMPO.lineaMarca])
  })

  it('administrador al crear: exige asesor', () => {
    const e = validarFormulario(formularioCompleto('2'), 'CAMION', { ...OPC, pedirAsesor: true })
    expect(e.asesorId).toEqual([MENSAJES_CAMPO.asesorId])
  })

  it('descripción técnica de 4001 caracteres rechazada; 4000 aceptada', () => {
    const base = formularioCompleto('2')
    expect(validarFormulario({ ...base, descripcionTecnica: 'a'.repeat(4000) }, 'CAMION', OPC)).toEqual({})
    expect(
      validarFormulario({ ...base, descripcionTecnica: 'a'.repeat(4001) }, 'CAMION', OPC).descripcionTecnica,
    ).toEqual([MENSAJES_CAMPO.descripcionTecnica])
  })

  it('placa: normaliza y exige 5 a 7 letras y números (CA-015-02, CA-015-05)', () => {
    expect(normalizarPlaca(' abc-123 ')).toBe('ABC123')
    expect(normalizarPlaca('abc 123')).toBe('ABC123')
    expect(placaValida('AbC123')).toBe(true)
    expect(placaValida('AB123')).toBe(true)
    expect(placaValida('ABC1234')).toBe(true)
    expect(placaValida('AB12')).toBe(false)
    expect(placaValida('ABCD1234')).toBe(false)
    expect(placaValida('ABC12Ñ')).toBe(false)
    expect(placaValida('AB@123')).toBe(false)
  })

  it('precio: entero > 0 y hasta 10.000.000.000 (CA-016-01)', () => {
    expect(leerPrecio('1')).toBe(1)
    expect(leerPrecio('10000000000')).toBe(10000000000)
    expect(leerPrecio('0')).toBeNull()
    expect(leerPrecio('10000000001')).toBeNull()
    expect(leerPrecio('')).toBeNull()
  })

  it('medidas: > 0 y hasta 2 decimales, con coma o punto (CA-018-03)', () => {
    expect(leerMedida('6,5')).toBe(6.5)
    expect(leerMedida('2.45')).toBe(2.45)
    expect(leerMedida('0')).toBeNull()
    expect(leerMedida('-1')).toBeNull()
    expect(leerMedida('1.234')).toBeNull()
  })

  it('enteros con puntos de miles', () => {
    expect(leerEntero('85.000')).toBe(85000)
    expect(leerEntero('0')).toBe(0)
    expect(leerEntero('-1')).toBeNull()
    expect(leerEntero('85k')).toBeNull()
  })

  // CA-017-01 / CA-018-02: 1.5 debe rechazarse. leerEntero quita el punto como
  // separador de miles y lo convierte en 15 (ver defecto en la evidencia).
  it('kilometraje o cilindraje "1.5" no se acepta como 15 (CA-017-01, CA-018-02)', () => {
    const f = formulario({ ...formularioCompleto('2'), kilometraje: '1.5', cilindrajeCc: '1.5' })
    const e = validarFormulario(f, 'CAMION', OPC)
    expect(e.kilometraje).toBeDefined()
    expect(e.cilindrajeCc).toBeDefined()
  })

  // CA-018-01 / CA-018-03: pesoBrutoKg y capacidadCargaKg admiten hasta 2 decimales.
  it('peso bruto "7500,50" es válido y se envía como 7500.5 (CA-018-01)', () => {
    const f = formulario({ ...formularioCompleto('2'), pesoBrutoKg: '7500,50', capacidadCargaKg: '3000,25' })
    expect(validarFormulario(f, 'CAMION', OPC)).toEqual({})
    const cuerpo = construirCuerpo(f, 'CAMION', false)
    expect(cuerpo.pesoBrutoKg).toBe(7500.5)
    expect(cuerpo.capacidadCargaKg).toBe(3000.25)
  })
})

describe('Errores del servidor por campo (CA-UI-09)', () => {
  it('reparte por campo visible sin distinguir mayúsculas; los demás al aviso general', () => {
    const { porCampo, otros } = repartirErroresServidor(
      {
        Placa: ['Esta placa ya está registrada en otro vehículo.'],
        kilometraje: ['Este dato no aplica a Carrocería.'],
        general: ['Algo más.'],
      },
      (c) => c === 'placa',
    )
    expect(porCampo).toEqual({ placa: ['Esta placa ya está registrada en otro vehículo.'] })
    expect(otros).toEqual(['Este dato no aplica a Carrocería.', 'Algo más.'])
  })
})

describe('Estados y acciones (CA-UI-03, D-4, D-11)', () => {
  it('acciones según el estado; VENDIDO sin acciones ni edición', () => {
    expect(accionesEstado('ACTIVO')).toEqual(['reservar', 'inactivar'])
    expect(accionesEstado('RESERVADO')).toEqual(['liberar', 'inactivar'])
    expect(accionesEstado('INACTIVO')).toEqual(['reactivar'])
    expect(accionesEstado('VENDIDO')).toEqual([])
    expect(accionesEstado('PERDIDO')).toEqual([])
    expect(esEditable('VENDIDO')).toBe(false)
    expect(esEditable('INACTIVO')).toBe(true)
    expect(nombreEstado('reservado')).toBe('Reservado')
  })

  it('motivo de inactivación de 5 a 500 caracteres (sin contar espacios de los extremos)', () => {
    expect(validarMotivoInactivar('1234')).toHaveLength(1)
    expect(validarMotivoInactivar('   1234   ')).toHaveLength(1)
    expect(validarMotivoInactivar('12345')).toEqual([])
    expect(validarMotivoInactivar('a'.repeat(500))).toEqual([])
    expect(validarMotivoInactivar('a'.repeat(501))).toHaveLength(1)
  })

  it('título sin "null": marca + línea + año, o la categoría si no hay marca', () => {
    expect(tituloVehiculo({ tipo: 'Camión', marca: 'Chevrolet', linea: 'NPR', modeloAnio: 2020 })).toBe(
      'Chevrolet NPR 2020',
    )
    expect(tituloVehiculo({ tipo: 'Carrocería', marca: null, linea: null, modeloAnio: null })).toBe('Carrocería')
    expect(tituloVehiculo({ tipo: 'Carrocería', marca: null, linea: null, modeloAnio: 2019 })).toBe(
      'Carrocería 2019',
    )
  })
})

describe('Validación previa de fotos (CA-UI-12)', () => {
  it('solo JPG, PNG o WebP de 1 byte a 5 MB', () => {
    const MSG = 'Sube una imagen JPG, PNG o WebP de hasta 5 MB.'
    expect(validarArchivoFoto({ type: 'image/jpeg', size: MAX_BYTES_FOTO })).toBeNull()
    expect(validarArchivoFoto({ type: 'image/png', size: 1 })).toBeNull()
    expect(validarArchivoFoto({ type: 'image/webp', size: 10 })).toBeNull()
    expect(validarArchivoFoto({ type: 'image/jpeg', size: MAX_BYTES_FOTO + 1 })).toBe(MSG)
    expect(validarArchivoFoto({ type: 'image/gif', size: 10 })).toBe(MSG)
    expect(validarArchivoFoto({ type: 'application/pdf', size: 10 })).toBe(MSG)
    expect(validarArchivoFoto({ type: 'image/png', size: 0 })).toBe(MSG)
  })
})
