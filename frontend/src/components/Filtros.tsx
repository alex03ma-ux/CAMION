import { useState, type FormEvent } from 'react'
import {
  CLAVES_FILTROS,
  FILTROS_VACIOS,
  ORDENES,
  Q_MAX,
  validarFiltros,
  type ClaveFiltro,
  type FiltrosCatalogo,
  type OpcionesFiltros,
} from '../api/catalogo'
import type { ErroresPorCampo } from '../api/http'
import { formatoMiles, mostrarConMiles, soloDigitos } from '../catalogo/formato'
import { Aviso } from './Aviso'
import { Campo, CampoSelect } from './Campo'

type FiltrosProps = {
  /** Filtros de la URL; el formulario arranca con ellos (se remonta al cambiar la URL). */
  filtros: FiltrosCatalogo
  /** null mientras cargan las opciones o si fallaron. */
  opciones: OpcionesFiltros | null
  /** Errores 400 del servidor, por parámetro. */
  erroresServidor: ErroresPorCampo
  onAplicar: (filtros: FiltrosCatalogo) => void
  onLimpiar: () => void
}

const id = (clave: ClaveFiltro) => `filtro-${clave}`

function conTotal(nombre: string, total: number): string {
  return total > 0 ? `${nombre} (${formatoMiles(total)})` : nombre
}

/** Primer campo con error en el orden del formulario. */
function primerCampoConError(errores: ErroresPorCampo): ClaveFiltro | undefined {
  return CLAVES_FILTROS.find((c) => c !== 'pagina' && (errores[c]?.length ?? 0) > 0)
}

/** Filtros del catálogo (HU-005 a HU-012). Valida en cliente con los mensajes del contrato. */
export function Filtros({
  filtros,
  opciones,
  erroresServidor,
  onAplicar,
  onLimpiar,
}: FiltrosProps) {
  const [f, setF] = useState<FiltrosCatalogo>(filtros)
  const [erroresCliente, setErroresCliente] = useState<ErroresPorCampo | null>(null)

  // Los errores del cliente (tras "Aplicar") tienen prioridad; si no, los del servidor.
  const errores = erroresCliente ?? erroresServidor
  const err = (clave: ClaveFiltro) => errores[clave] ?? []

  const cambiar = (clave: ClaveFiltro, valor: string) => {
    setF((actual) => {
      const nuevo = { ...actual, [clave]: valor }
      // El modelo depende de la marca: al cambiar de marca se vacía.
      if (clave === 'marcaId' && valor !== actual.marcaId) nuevo.lineaId = ''
      return nuevo
    })
  }

  const marcaElegida = opciones?.marcas.find((m) => String(m.id) === f.marcaId)
  const lineas = marcaElegida?.lineas ?? []

  function aplicar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    const aEnviar: FiltrosCatalogo = {
      ...f,
      q: f.q.trim(),
      orden: f.orden === 'recientes' ? '' : f.orden,
      pagina: '',
    }
    const encontrados = validarFiltros(aEnviar, opciones)
    if (Object.keys(encontrados).length > 0) {
      setErroresCliente(encontrados)
      const campo = primerCampoConError(encontrados)
      if (campo) window.setTimeout(() => document.getElementById(id(campo))?.focus(), 0)
      return
    }
    setErroresCliente(null)
    onAplicar(aEnviar)
  }

  function limpiar() {
    setF(FILTROS_VACIOS)
    setErroresCliente(null)
    onLimpiar()
  }

  // Errores que no tienen un campo propio (página, tamaño u otros).
  const otros = Object.entries(errores)
    .filter(([clave]) => clave === 'pagina' || !CLAVES_FILTROS.includes(clave as ClaveFiltro))
    .flatMap(([, mensajes]) => mensajes)

  return (
    <form className="cat-filtros" onSubmit={aplicar} noValidate aria-labelledby="filtros-titulo">
      <h2 id="filtros-titulo">Filtros</h2>

      {opciones === null && (
        <p className="form-ayuda cat-filtros-ayuda">
          Las opciones de categoría y marca no están disponibles en este momento.
        </p>
      )}

      <Campo
        id={id('q')}
        etiqueta="Buscar"
        valor={f.q}
        onCambio={(v) => cambiar('q', v)}
        tipo="search"
        maxLength={Q_MAX}
        errores={err('q')}
        requerido={false}
        autoComplete="off"
      />

      <CampoSelect
        id={id('tipoId')}
        etiqueta="Categoría"
        valor={f.tipoId}
        onCambio={(v) => cambiar('tipoId', v)}
        textoVacio="Todas"
        opciones={(opciones?.tipos ?? []).map((t) => ({
          valor: String(t.id),
          nombre: conTotal(t.nombre, t.total),
        }))}
        errores={err('tipoId')}
        requerido={false}
      />

      <CampoSelect
        id={id('marcaId')}
        etiqueta="Marca"
        valor={f.marcaId}
        onCambio={(v) => cambiar('marcaId', v)}
        textoVacio="Todas"
        opciones={(opciones?.marcas ?? []).map((m) => ({
          valor: String(m.id),
          nombre: conTotal(m.nombre, m.total),
        }))}
        errores={err('marcaId')}
        requerido={false}
      />

      <CampoSelect
        id={id('lineaId')}
        etiqueta="Modelo"
        valor={f.lineaId}
        onCambio={(v) => cambiar('lineaId', v)}
        textoVacio={f.marcaId === '' ? 'Elige primero una marca' : 'Todos'}
        opciones={lineas.map((l) => ({ valor: String(l.id), nombre: conTotal(l.nombre, l.total) }))}
        errores={err('lineaId')}
        deshabilitado={f.marcaId === ''}
        requerido={false}
      />

      <fieldset className="cat-rango">
        <legend>Presupuesto (COP)</legend>
        <Campo
          id={id('precioMin')}
          etiqueta="Mínimo"
          valor={mostrarConMiles(f.precioMin)}
          onCambio={(v) => cambiar('precioMin', soloDigitos(v))}
          inputMode="numeric"
          maxLength={19}
          errores={err('precioMin')}
          requerido={false}
          autoComplete="off"
        />
        <Campo
          id={id('precioMax')}
          etiqueta="Máximo"
          valor={mostrarConMiles(f.precioMax)}
          onCambio={(v) => cambiar('precioMax', soloDigitos(v))}
          inputMode="numeric"
          maxLength={19}
          errores={err('precioMax')}
          requerido={false}
          autoComplete="off"
        />
      </fieldset>

      <fieldset className="cat-rango">
        <legend>Año</legend>
        <Campo
          id={id('anioMin')}
          etiqueta="Desde"
          valor={f.anioMin}
          onCambio={(v) => cambiar('anioMin', soloDigitos(v))}
          inputMode="numeric"
          maxLength={4}
          errores={err('anioMin')}
          requerido={false}
          autoComplete="off"
        />
        <Campo
          id={id('anioMax')}
          etiqueta="Hasta"
          valor={f.anioMax}
          onCambio={(v) => cambiar('anioMax', soloDigitos(v))}
          inputMode="numeric"
          maxLength={4}
          errores={err('anioMax')}
          requerido={false}
          autoComplete="off"
        />
      </fieldset>

      <Campo
        id={id('kmMax')}
        etiqueta="Kilometraje máximo"
        valor={mostrarConMiles(f.kmMax)}
        onCambio={(v) => cambiar('kmMax', soloDigitos(v))}
        inputMode="numeric"
        maxLength={11}
        errores={err('kmMax')}
        requerido={false}
        autoComplete="off"
      />

      <CampoSelect
        id={id('orden')}
        etiqueta="Ordenar por"
        valor={f.orden === '' ? 'recientes' : f.orden}
        onCambio={(v) => cambiar('orden', v)}
        opciones={ORDENES}
        errores={err('orden')}
        requerido={false}
      />

      {otros.length > 0 && <Aviso aviso={{ tipo: 'error', texto: otros.join(' ') }} />}

      <div className="cat-filtros-botones">
        <button type="submit" className="cat-boton">
          Aplicar filtros
        </button>
        <button type="button" className="cat-boton secondary-button" onClick={limpiar}>
          Limpiar filtros
        </button>
      </div>
    </form>
  )
}
