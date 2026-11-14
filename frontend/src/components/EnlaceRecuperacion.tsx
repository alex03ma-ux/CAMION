// P8 (HU-063, D-73 D; CA-P8-67) — Enlace de recuperación recién generado por el administrador: se muestra con
// "Copiar", "Enviar por WhatsApp" y "Enviar por correo" (mailto al correo registrado; pedido de la responsable) (wa.me al celular del usuario, si es válido, con un texto breve) y el aviso de que
// vence en 60 minutos y sirve una sola vez. El enlace solo vive en memoria mientras el panel está abierto: no se
// guarda en el navegador ni se registra en consola. Al cerrar el panel se descarta.
import { useEffect, useRef, useState } from 'react'
import { formatearFechaHora } from '../api/leads'
import { enlaceCorreoUsuario, enlaceWhatsappUsuario, MENSAJE_ENLACE, type EnlaceRecuperacion } from '../api/recuperacion'
import { Aviso, type DatosAviso } from './Aviso'

type Props = {
  nombre: string
  telefono: string
  correo: string
  datos: EnlaceRecuperacion
  onCerrar: () => void
}

export function PanelEnlaceRecuperacion({ nombre, telefono, correo, datos, onCerrar }: Props) {
  const [copia, setCopia] = useState<DatosAviso | null>(null)
  const titulo = useRef<HTMLHeadingElement>(null)
  const campo = useRef<HTMLInputElement>(null)
  const whatsapp = enlaceWhatsappUsuario(telefono, datos.enlace)
  const mailto = enlaceCorreoUsuario(correo, datos.enlace)

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
      {/* El texto del servidor solo menciona WhatsApp; desde que existe "Enviar por correo" se usa el propio. */}
      <Aviso aviso={{ tipo: 'info', texto: MENSAJE_ENLACE }} />
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
        {mailto ? (
          <a className="inv-boton-enlace inv-boton-pequeno inv-boton-secundario" href={mailto}>
            Enviar por correo<span className="sr-only"> a {correo.trim()} (abre tu programa de correo)</span>
          </a>
        ) : null}
        <button type="button" className="secondary-button" onClick={onCerrar}>
          Cerrar
        </button>
      </div>
      {(mailto || !whatsapp) && (
        <ul className="enlace-recuperacion-notas">
          {mailto && (
            <li>
              "Enviar por correo" abre tu programa de correo con el mensaje listo para {correo.trim()}: revísalo y
              envíalo desde tu cuenta.
            </li>
          )}
          {!whatsapp && (
            <li>El teléfono registrado no es un celular colombiano válido: copia el enlace y envíalo tú por WhatsApp.</li>
          )}
        </ul>
      )}
      <Aviso aviso={copia} />
    </section>
  )
}
