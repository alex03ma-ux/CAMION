// P8 (HU-063, D-73 D; CA-P8-67) — Enlace de recuperación recién generado por el administrador: se muestra con
// "Copiar" y "Enviar por WhatsApp" (wa.me al celular del usuario, si es válido, con un texto breve) y el aviso de que
// vence en 60 minutos y sirve una sola vez. El enlace solo vive en memoria mientras el panel está abierto: no se
// guarda en el navegador ni se registra en consola. Al cerrar el panel se descarta.
import { useEffect, useRef, useState } from 'react'
import { formatearFechaHora } from '../api/leads'
import { enlaceWhatsappUsuario, type EnlaceRecuperacion } from '../api/recuperacion'
import { Aviso, type DatosAviso } from './Aviso'

type Props = {
  nombre: string
  telefono: string
  datos: EnlaceRecuperacion
  onCerrar: () => void
}

export function PanelEnlaceRecuperacion({ nombre, telefono, datos, onCerrar }: Props) {
  const [copia, setCopia] = useState<DatosAviso | null>(null)
  const titulo = useRef<HTMLHeadingElement>(null)
  const campo = useRef<HTMLInputElement>(null)
  const whatsapp = enlaceWhatsappUsuario(telefono, datos.enlace)

  useEffect(() => {
    titulo.current?.focus()
  }, [])

  async function copiar() {
    try {
      if (!navigator.clipboard?.writeText) throw new Error('sin portapapeles')
      await navigator.clipboard.writeText(datos.enlace)
      setCopia({ tipo: 'exito', texto: 'Enlace copiado.' })
    } catch {
      // Sin acceso al portapapeles: se selecciona el texto para copiarlo a mano.
      campo.current?.focus()
      campo.current?.select()
      setCopia({ tipo: 'advertencia', texto: 'No se pudo copiar solo. El enlace quedó seleccionado: cópialo con Ctrl+C.' })
    }
  }

  return (
    <section className="enlace-recuperacion" aria-labelledby="enlace-recuperacion-titulo">
      <h3 id="enlace-recuperacion-titulo" ref={titulo} tabIndex={-1} className="admin-subtitulo">
        Enlace de recuperación para {nombre}
      </h3>
      <Aviso aviso={{ tipo: 'info', texto: datos.mensaje }} />
      <p className="form-ayuda">
        Vence en 60 minutos{datos.venceEn ? ` (${formatearFechaHora(datos.venceEn)}, hora de Colombia)` : ''} y sirve una
        sola vez. Si generas otro, este deja de valer.
      </p>
      <div className="campo">
        <label htmlFor="enlace-recuperacion-texto">Enlace</label>
        <input
          ref={campo}
          id="enlace-recuperacion-texto"
          type="text"
          readOnly
          value={datos.enlace}
          onFocus={(e) => e.currentTarget.select()}
          autoComplete="off"
          spellCheck={false}
        />
      </div>
      <div className="acciones-fila enlace-recuperacion-acciones">
        <button type="button" onClick={() => void copiar()}>
          Copiar
        </button>
        {whatsapp ? (
          <a className="inv-boton-enlace inv-boton-pequeno" href={whatsapp} target="_blank" rel="noopener noreferrer">
            Enviar por WhatsApp<span className="sr-only"> (se abre en una ventana nueva)</span>
          </a>
        ) : null}
        <button type="button" className="secondary-button" onClick={onCerrar}>
          Cerrar
        </button>
      </div>
      {!whatsapp && (
        <p className="form-ayuda">
          El teléfono registrado no es un celular colombiano válido: copia el enlace y envíalo tú por WhatsApp.
        </p>
      )}
      <Aviso aviso={copia} />
    </section>
  )
}
