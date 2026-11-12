// P7 (D-61 B, CA-P7-27 a 29) — Campos "Pago recibido": valor, fecha, medio y referencia. Se reutilizan al asignar,
// renovar o cambiar de plan y al anotar un pago después desde el detalle de la suscripción. Solo se anota un
// pago cobrado fuera del portal: no hay campos de tarjeta ni de cuentas (CA-P7-33).
import {
  MEDIOS_PAGO,
  NOMBRES_MEDIO_PAGO,
  REFERENCIA_MAX,
  hoyColombia,
  type CampoPago,
  type ErroresPago,
  type FormularioPago,
} from '../api/pagos'
import { formatoCop, mostrarConMiles, soloDigitos } from '../catalogo/formato'
import { Campo, CampoSelect } from './Campo'

type CamposPagoProps = {
  /** Prefijo de los ids (debe ser único en la pantalla). */
  id: string
  valor: FormularioPago
  onCambio: (campo: CampoPago, valor: string) => void
  errores: ErroresPago
  /** Precio del plan, para el texto de ayuda; null si no se conoce. */
  precioPlan: number | null
  deshabilitado?: boolean
}

export function CamposPago({ id, valor, onCambio, errores, precioPlan, deshabilitado = false }: CamposPagoProps) {
  const ayudaValor = `${id}-valor-ayuda`
  const ayudaReferencia = `${id}-referencia-ayuda`
  return (
    <div className="pago-campos">
      <p id={ayudaValor} className="campo-ayuda">
        {precioPlan !== null && precioPlan > 0
          ? `Precio del plan: ${formatoCop(precioPlan)}. Cámbialo si el valor recibido fue otro.`
          : 'Valor en pesos, sin decimales.'}
      </p>
      <div className="admin-rejilla">
        <Campo
          id={`${id}-valor`}
          etiqueta="Valor pagado (COP)"
          valor={mostrarConMiles(valor.valorCop)}
          onCambio={(v) => onCambio('valorCop', soloDigitos(v))}
          inputMode="numeric"
          maxLength={20}
          describidoPor={ayudaValor}
          errores={errores.valorCop}
          deshabilitado={deshabilitado}
        />
        <Campo
          id={`${id}-fecha`}
          etiqueta="Fecha del pago"
          tipo="date"
          valor={valor.fechaPago}
          onCambio={(v) => onCambio('fechaPago', v)}
          errores={errores.fechaPago}
          deshabilitado={deshabilitado}
        />
        <CampoSelect
          id={`${id}-medio`}
          etiqueta="Medio de pago"
          valor={valor.medio}
          onCambio={(v) => onCambio('medio', v)}
          opciones={MEDIOS_PAGO.map((m) => ({ valor: m, nombre: NOMBRES_MEDIO_PAGO[m] }))}
          textoVacio="Elige el medio"
          errores={errores.medio}
          deshabilitado={deshabilitado}
        />
        <Campo
          id={`${id}-referencia`}
          etiqueta={valor.medio === 'OTRO' ? 'Referencia' : 'Referencia (opcional)'}
          valor={valor.referencia}
          onCambio={(v) => onCambio('referencia', v)}
          maxLength={REFERENCIA_MAX}
          describidoPor={ayudaReferencia}
          errores={errores.referencia}
          requerido={valor.medio === 'OTRO'}
          deshabilitado={deshabilitado}
        />
      </div>
      <p id={ayudaReferencia} className="campo-ayuda">
        Número de la transferencia o del recibo, hasta {REFERENCIA_MAX} caracteres. Es obligatoria si el medio es
        Otro. La fecha debe ser de hoy ({hoyColombia()}) o anterior.
      </p>
    </div>
  )
}
