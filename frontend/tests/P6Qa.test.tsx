// P6 (QA) — "Mis solicitudes" (D-59 b A; CA-P6-68, 71, 72): defensa en el cliente aunque la API devolviera de más.
// Datos ficticios; ninguna llamada real.
import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { V2 } from './catalogoDatos'
import { abrirApp } from './p4pDatos'

const RUTA_MIS = '/api/mis-solicitudes?pagina=1&tamano=20'

describe('"Mis solicitudes" no muestra nada interno (QA, CA-P6-68, 71, 72)', () => {
  it('campos internos de más en la respuesta (notas, responsable, resultado, teléfono, nota de la relación) no se pintan', async () => {
    abrirApp('#/mis-solicitudes', ['COMPRADOR'], {
      [RUTA_MIS]: {
        status: 200,
        body: {
          items: [
            {
              id: 7,
              creadoEn: '2026-11-09T15:00:00Z',
              tipo: 'CREDITO',
              resumen: 'Sin vehículo asociado',
              estado: 'FINALIZADA',
              vehiculo: null,
              recomendados: [],
              // Campos que el contrato no tiene: el cliente no debe mostrarlos.
              notas: [{ texto: 'Nota interna QA ficticia' }],
              responsable: { tipo: 'ASESOR', nombre: 'Asesor QA Ficticio' },
              resultadoCierre: 'REMITIDO_ENTIDAD',
              observacionCierre: 'Observación interna QA ficticia',
              telefono: '+573009998877',
              mensaje: 'Mensaje interno QA ficticio',
            },
            {
              id: 8,
              creadoEn: '2026-11-08T15:00:00Z',
              tipo: 'BUSQUEDA',
              resumen: 'Camión',
              estado: 'EN_GESTION',
              vehiculo: null,
              recomendados: [{ ...V2, nota: 'Nota de relación QA ficticia' }],
            },
          ],
          total: 2,
          pagina: 1,
          tamano: 20,
        },
      },
    })
    await screen.findByText('Sin vehículo asociado')
    for (const texto of [
      'Nota interna QA ficticia',
      'Asesor QA Ficticio',
      'REMITIDO_ENTIDAD',
      'Remitido a entidad financiera',
      'Observación interna QA ficticia',
      '+573009998877',
      '300 999 8877',
      'Mensaje interno QA ficticio',
      'Nota de relación QA ficticia',
    ]) {
      expect(screen.queryByText(texto, { exact: false })).not.toBeInTheDocument()
    }
    expect(document.body.textContent).not.toMatch(/Asesor QA|Nota interna|Observación interna|9998877/)
  })

  it('un vehículo con disponible=false no se enlaza aunque llegue un slug; los recomendados solo se muestran en búsquedas', async () => {
    abrirApp('#/mis-solicitudes', ['COMPRADOR'], {
      [RUTA_MIS]: {
        status: 200,
        body: {
          items: [
            {
              id: 9,
              creadoEn: '2026-11-09T15:00:00Z',
              tipo: 'CREDITO',
              resumen: 'Chevrolet NPR 2020',
              estado: 'REGISTRADA',
              vehiculo: { titulo: 'Chevrolet NPR 2020', slug: 'chevrolet-npr-2020', disponible: false },
              recomendados: [V2],
            },
          ],
          total: 1,
          pagina: 1,
          tamano: 20,
        },
      },
    })
    const tarjeta = await screen.findByRole('article', { name: 'Crédito' })
    expect(within(tarjeta).getByText('Este vehículo ya no está disponible')).toBeInTheDocument()
    expect(within(tarjeta).queryByRole('link')).not.toBeInTheDocument()
    expect(within(tarjeta).queryByRole('heading', { name: 'Te recomendamos estos vehículos' })).not.toBeInTheDocument()
    expect(document.querySelector('a[href="#/vehiculo/chevrolet-npr-2020"]')).toBeNull()
  })
})
