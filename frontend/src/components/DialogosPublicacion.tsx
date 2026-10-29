// P4b — Diálogos de publicar o editar (HU-023, HU-024), marcar vendido (HU-064, D-39) y
// reasignar un vehículo (D-34). Reutilizan DialogoConfirmacion (foco atrapado, Escape cancela)
// con sus campos propios; los obligatorios llevan asterisco (D-31).
import { useState } from 'react'
import type { ResumenAsesor } from '../api/inventario'
import {
  DESCRIPCION_COMERCIAL_MAX,
  hoyIso,
  MENSAJE_ASESOR_DESTINO,
  NOMBRES_ORIGEN_VENTA,
  OBSERVACIONES_MAX,
  ORIGENES_VENTA,
  TITULO_MAX,
  validarPublicacion,
  validarVenta,
  type CampoPublicacion,
  type CampoVenta,
  type ErroresPublicacion,
  type ErroresVenta,
  type FormularioPublicacion,
  type FormularioVenta,
} from '../api/publicaciones'
import { mostrarConMiles, soloDigitos } from '../catalogo/formato'
import { validarMotivoInactivar } from '../inventario/estados'
import type { DatosAviso } from './Aviso'
import { Campo, CampoAreaTexto, CampoSelect } from './Campo'
import { DialogoConfirmacion } from './DialogoAccion'
import { NotaObligatorio } from './NotaObligatorio'

const ID_PUBLICACION: Record<CampoPublicacion, string> = {
  titulo: 'pub-titulo',
  descripcionComercial: 'pub-descripcion',
  negociable: 'pub-negociable',
}

const ID_VENTA: Record<CampoVenta, string> = {
  fechaVenta: 'venta-fecha',
  precioFinalCop: 'venta-precio',
  origen: 'venta-origen',
  observaciones: 'venta-observaciones',
}

function enfocar<C extends string>(ids: Record<C, string>, orden: readonly C[], errores: Partial<Record<C, string[]>>) {
  const campo = orden.find((c) => (errores[c]?.length ?? 0) > 0)
  if (campo) window.setTimeout(() => document.getElementById(ids[campo])?.focus(), 0)
}

// ---- Publicar / editar ----

type DialogoPublicacionProps = {
  modo: 'crear' | 'editar'
  tituloVehiculo: string
  inicial: FormularioPublicacion
  enviando: boolean
  /** Errores del servidor (400) por campo. */
  erroresServidor: ErroresPublicacion
  error: DatosAviso | null
  onConfirmar: (formulario: FormularioPublicacion) => void
  onCancelar: () => void
}

export function DialogoPublicacion({
  modo,
  tituloVehiculo,
  inicial,
  enviando,
  erroresServidor,
  error,
  onConfirmar,
  onCancelar,
}: DialogoPublicacionProps) {
  const [form, setForm] = useState<FormularioPublicacion>(inicial)
  const [erroresLocales, setErroresLocales] = useState<ErroresPublicacion | null>(null)
  const [erroresVistos, setErroresVistos] = useState(erroresServidor)
  if (erroresVistos !== erroresServidor) {
    // Llegaron errores nuevos del servidor: sustituyen a los locales.
    setErroresVistos(erroresServidor)
    setErroresLocales(null)
    enfocar(ID_PUBLICACION, ['titulo', 'descripcionComercial', 'negociable'], erroresServidor)
  }
  const errores = erroresLocales ?? erroresServidor

  function cambiar<C extends CampoPublicacion>(campo: C, valor: FormularioPublicacion[C]) {
    setForm((f) => ({ ...f, [campo]: valor }))
    if (errores[campo]) setErroresLocales({ ...errores, [campo]: undefined })
  }

  function confirmar() {
    const validacion = validarPublicacion(form)
    setErroresLocales(validacion)
    if (Object.keys(validacion).length > 0) {
      enfocar(ID_PUBLICACION, ['titulo', 'descripcionComercial'], validacion)
      return
    }
    onConfirmar(form)
  }

  return (
    <DialogoConfirmacion
      titulo={modo === 'crear' ? 'Publicar vehículo' : 'Editar publicación'}
      descripcion={
        modo === 'crear'
          ? `Publica ${tituloVehiculo} en el portal. El precio y las fotos son los del vehículo; la publicación ocupa un cupo de tu plan.`
          : `Cambia el título, la descripción o si el precio es negociable. El precio se cambia en el vehículo.`
      }
      textoBoton={modo === 'crear' ? 'Publicar' : 'Guardar cambios'}
      textoEnviando={modo === 'crear' ? 'Publicando...' : 'Guardando...'}
      motivo="no"
      contenido={
        <>
          <NotaObligatorio />
          <Campo
            id={ID_PUBLICACION.titulo}
            etiqueta="Título de la publicación"
            valor={form.titulo}
            onCambio={(v) => cambiar('titulo', v)}
            maxLength={TITULO_MAX}
            errores={errores.titulo}
            autoComplete="off"
            describidoPor="pub-titulo-ayuda"
            deshabilitado={enviando}
          />
          <p id="pub-titulo-ayuda" className="campo-ayuda">
            De 5 a 180 caracteres. Llevas {form.titulo.trim().length}.
          </p>
          <CampoAreaTexto
            id={ID_PUBLICACION.descripcionComercial}
            etiqueta="Descripción comercial (opcional)"
            valor={form.descripcionComercial}
            onCambio={(v) => cambiar('descripcionComercial', v)}
            maxLength={DESCRIPCION_COMERCIAL_MAX}
            filas={5}
            ayuda="Hasta 4000 caracteres."
            errores={errores.descripcionComercial}
            deshabilitado={enviando}
            requerido={false}
          />
          <label className="opcion-casilla" htmlFor={ID_PUBLICACION.negociable}>
            <input
              id={ID_PUBLICACION.negociable}
              type="checkbox"
              checked={form.negociable}
              onChange={(e) => cambiar('negociable', e.target.checked)}
              disabled={enviando}
            />
            <span>El precio es negociable</span>
          </label>
          {errores.negociable && (
            <ul className="campo-errores">
              {errores.negociable.map((m) => (
                <li key={m}>{m}</li>
              ))}
            </ul>
          )}
        </>
      }
      enviando={enviando}
      error={error}
      onConfirmar={confirmar}
      onCancelar={onCancelar}
    />
  )
}

// ---- Marcar vendido ----

type DialogoVentaProps = {
  tituloVehiculo: string
  /** Precio del vehículo, para proponerlo como precio final. */
  precioCop: number
  enviando: boolean
  erroresServidor: ErroresVenta
  error: DatosAviso | null
  onConfirmar: (formulario: FormularioVenta) => void
  onCancelar: () => void
}

const ORDEN_VENTA: readonly CampoVenta[] = ['fechaVenta', 'precioFinalCop', 'origen', 'observaciones']

export function DialogoVenta({
  tituloVehiculo,
  precioCop,
  enviando,
  erroresServidor,
  error,
  onConfirmar,
  onCancelar,
}: DialogoVentaProps) {
  const [form, setForm] = useState<FormularioVenta>(() => ({
    fechaVenta: hoyIso(),
    precioFinalCop: precioCop > 0 ? String(Math.round(precioCop)) : '',
    origen: '',
    observaciones: '',
  }))
  const [erroresLocales, setErroresLocales] = useState<ErroresVenta | null>(null)
  const [erroresVistos, setErroresVistos] = useState(erroresServidor)
  if (erroresVistos !== erroresServidor) {
    setErroresVistos(erroresServidor)
    setErroresLocales(null)
    enfocar(ID_VENTA, ORDEN_VENTA, erroresServidor)
  }
  const errores = erroresLocales ?? erroresServidor
  const hoy = hoyIso()

  function cambiar(campo: CampoVenta, valor: string) {
    setForm((f) => ({ ...f, [campo]: valor }))
    if (errores[campo] || (campo === 'origen' && errores.observaciones)) {
      setErroresLocales({ ...errores, [campo]: undefined, ...(campo === 'origen' ? { observaciones: undefined } : {}) })
    }
  }

  function confirmar() {
    const validacion = validarVenta(form, hoy)
    setErroresLocales(validacion)
    if (Object.keys(validacion).length > 0) {
      enfocar(ID_VENTA, ORDEN_VENTA, validacion)
      return
    }
    onConfirmar(form)
  }

  const otro = form.origen === 'OTRO'

  return (
    <DialogoConfirmacion
      titulo="Marcar vehículo vendido"
      descripcion={`¿Marcar ${tituloVehiculo} como vendido? Saldrá del portal y su publicación quedará vendida. Si te equivocas, puedes revertir la venta.`}
      textoBoton="Marcar vendido"
      textoEnviando="Guardando venta..."
      motivo="no"
      contenido={
        <>
          <NotaObligatorio />
          <Campo
            id={ID_VENTA.fechaVenta}
            etiqueta="Fecha de la venta"
            tipo="date"
            valor={form.fechaVenta}
            onCambio={(v) => cambiar('fechaVenta', v)}
            errores={errores.fechaVenta}
            describidoPor="venta-fecha-ayuda"
            deshabilitado={enviando}
          />
          <p id="venta-fecha-ayuda" className="campo-ayuda">
            Hoy o una fecha anterior.
          </p>
          <Campo
            id={ID_VENTA.precioFinalCop}
            etiqueta="Precio final (COP)"
            valor={mostrarConMiles(form.precioFinalCop)}
            onCambio={(v) => cambiar('precioFinalCop', soloDigitos(v))}
            inputMode="numeric"
            maxLength={19}
            errores={errores.precioFinalCop}
            autoComplete="off"
            describidoPor="venta-precio-ayuda"
            deshabilitado={enviando}
          />
          <p id="venta-precio-ayuda" className="campo-ayuda">
            En pesos, sin decimales.
          </p>
          <CampoSelect
            id={ID_VENTA.origen}
            etiqueta="Origen de la venta"
            valor={form.origen}
            onCambio={(v) => cambiar('origen', v)}
            opciones={ORIGENES_VENTA.map((o) => ({ valor: o, nombre: NOMBRES_ORIGEN_VENTA[o] }))}
            textoVacio="Elige el origen"
            errores={errores.origen}
            deshabilitado={enviando}
          />
          <CampoAreaTexto
            id={ID_VENTA.observaciones}
            etiqueta={otro ? 'Observaciones' : 'Observaciones (opcional)'}
            valor={form.observaciones}
            onCambio={(v) => cambiar('observaciones', v)}
            maxLength={OBSERVACIONES_MAX}
            ayuda={otro ? 'Obligatorias si el origen es «Otro». Hasta 1000 caracteres.' : 'Hasta 1000 caracteres.'}
            errores={errores.observaciones}
            deshabilitado={enviando}
            requerido={otro}
          />
        </>
      }
      enviando={enviando}
      error={error}
      onConfirmar={confirmar}
      onCancelar={onCancelar}
    />
  )
}

// ---- Revertir venta ----

type DialogoRevertirProps = {
  tituloVehiculo: string
  enviando: boolean
  erroresMotivo: string[]
  error: DatosAviso | null
  onConfirmar: (motivo: string) => void
  onCancelar: () => void
}

export function DialogoRevertirVenta({ tituloVehiculo, enviando, erroresMotivo, error, onConfirmar, onCancelar }: DialogoRevertirProps) {
  return (
    <DialogoConfirmacion
      titulo="Revertir venta"
      descripcion={`¿Revertir la venta de ${tituloVehiculo}? El vehículo vuelve a estar activo y su publicación queda inactiva: para mostrarla otra vez en el portal tendrás que reactivarla.`}
      textoBoton="Revertir venta"
      textoEnviando="Revirtiendo..."
      peligro
      motivo="obligatorio"
      validarMotivo={validarMotivoInactivar}
      enviando={enviando}
      erroresMotivo={erroresMotivo}
      error={error}
      onConfirmar={onConfirmar}
      onCancelar={onCancelar}
    />
  )
}

// ---- Reasignar (D-34, solo administrador) ----

type DialogoReasignarProps = {
  tituloVehiculo: string
  /** Asesor actual (se excluye de la lista). */
  asesorActual: ResumenAsesor | null
  /** Asesores ACTIVA (catálogos del inventario). */
  asesores: readonly ResumenAsesor[]
  enviando: boolean
  erroresMotivo: string[]
  erroresAsesor: string[]
  error: DatosAviso | null
  onConfirmar: (asesorId: string, motivo: string) => void
  onCancelar: () => void
}

export function DialogoReasignar({
  tituloVehiculo,
  asesorActual,
  asesores,
  enviando,
  erroresMotivo,
  erroresAsesor,
  error,
  onConfirmar,
  onCancelar,
}: DialogoReasignarProps) {
  const [asesorId, setAsesorId] = useState('')
  const [erroresLocales, setErroresLocales] = useState<string[] | null>(null)
  const [vistos, setVistos] = useState(erroresAsesor)
  if (vistos !== erroresAsesor) {
    setVistos(erroresAsesor)
    setErroresLocales(null)
  }
  const errores = erroresLocales ?? erroresAsesor
  const opciones = asesores
    .filter((a) => a.id !== asesorActual?.id)
    .map((a) => ({ valor: a.id, nombre: a.nombre || `Asesor ${a.id}` }))

  function confirmar(motivo: string) {
    if (asesorId === '') {
      setErroresLocales([MENSAJE_ASESOR_DESTINO])
      window.setTimeout(() => document.getElementById('reasignar-asesor')?.focus(), 0)
      return
    }
    onConfirmar(asesorId, motivo)
  }

  return (
    <DialogoConfirmacion
      titulo="Reasignar a otro asesor"
      descripcion={`${tituloVehiculo} y su publicación pasarán al asesor que elijas${
        asesorActual?.nombre ? ` (hoy es de ${asesorActual.nombre})` : ''
      }. Sus destacados se quitan y el cambio queda auditado.`}
      textoBoton="Reasignar"
      textoEnviando="Reasignando..."
      motivo="obligatorio"
      validarMotivo={validarMotivoInactivar}
      contenido={
        <CampoSelect
          id="reasignar-asesor"
          etiqueta="Nuevo asesor"
          valor={asesorId}
          onCambio={(v) => {
            setAsesorId(v)
            if (errores.length > 0) setErroresLocales([])
          }}
          opciones={opciones}
          textoVacio={opciones.length > 0 ? 'Elige el asesor' : 'No hay otros asesores activos'}
          errores={errores}
          deshabilitado={enviando}
        />
      }
      enviando={enviando}
      erroresMotivo={erroresMotivo}
      error={error}
      onConfirmar={confirmar}
      onCancelar={onCancelar}
    />
  )
}
