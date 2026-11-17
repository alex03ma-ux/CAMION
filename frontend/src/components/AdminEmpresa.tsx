// P9 — Datos de la empresa (HU-085, RN-PA-005, D-63 B, D-83 A; CA-P9-24 a CA-P9-37, contrato P9 §3.5 y §3.6).
// Razón social, NIT y dirección de notificaciones. Cada campo es opcional (vacío = se retira); el portal los publica en
// el pie y en las páginas legales solo cuando están los tres. No hay valores por defecto. La API (SoloAdministrador)
// valida con la misma regla de NIT (DIAN, módulo 11) y audita cada cambio.
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { ApiError, MENSAJE_INESPERADO } from '../api/http'
import { formatearFechaHora } from '../api/leads'
import {
  CAMPOS_EMPRESA,
  DIRECCION_MAX,
  erroresEmpresaDe,
  formularioDesdeEmpresa,
  guardarEmpresaAdmin,
  NIT_MAX,
  obtenerEmpresaAdmin,
  RAZON_SOCIAL_MAX,
  validarEmpresa,
  type CampoEmpresa,
  type EmpresaPortalAdmin,
  type ErroresEmpresa,
  type FormularioEmpresa,
} from '../api/parametros'
import { Aviso, type DatosAviso } from './Aviso'
import { Campo } from './Campo'
import { recargarContactoPortal } from './useContactoPortal'

type Props = { token: string; onSesionTerminada: () => void }

type Carga = { estado: 'cargando' } | { estado: 'ok' } | { estado: 'error'; aviso: DatosAviso }

const IDS: Record<CampoEmpresa, string> = {
  razonSocial: 'empresa-razon-social',
  nit: 'empresa-nit',
  direccion: 'empresa-direccion',
}

export const TEXTO_AVISO_EMPRESA = 'Se muestran en el pie del portal solo cuando los tres están completos.'
export const MENSAJE_EMPRESA_GUARDADA = 'Datos de la empresa guardados.'

export function AdminEmpresa({ token, onSesionTerminada }: Props) {
  const [carga, setCarga] = useState<Carga>({ estado: 'cargando' })
  const [intento, setIntento] = useState(0)
  const [datos, setDatos] = useState<EmpresaPortalAdmin | null>(null)
  const [form, setForm] = useState<FormularioEmpresa | null>(null)
  const [errores, setErrores] = useState<ErroresEmpresa>({})
  const [aviso, setAviso] = useState<DatosAviso | null>(null)
  const [guardando, setGuardando] = useState(false)
  const refAviso = useRef<HTMLDivElement>(null)

  const es401 = useCallback(
    (error: unknown) => {
      if (error instanceof ApiError && error.status === 401) {
        onSesionTerminada()
        return true
      }
      return false
    },
    [onSesionTerminada],
  )

  useEffect(() => {
    const control = new AbortController()
    setCarga({ estado: 'cargando' })
    obtenerEmpresaAdmin(token, control.signal)
      .then((d) => {
        setDatos(d)
        setForm(formularioDesdeEmpresa(d))
        setCarga({ estado: 'ok' })
      })
      .catch((error: unknown) => {
        if (control.signal.aborted || es401(error)) return
        setCarga({ estado: 'error', aviso: { tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO } })
      })
    return () => control.abort()
  }, [token, intento, es401])

  function cambiar(campo: CampoEmpresa, valor: string) {
    setForm((f) => f && { ...f, [campo]: valor })
    setErrores((e) => ({ ...e, [campo]: undefined }))
  }

  function enfocarPrimerError(e: ErroresEmpresa): boolean {
    const campo = CAMPOS_EMPRESA.find((c) => e[c]?.length)
    if (campo) window.setTimeout(() => document.getElementById(IDS[campo])?.focus(), 0)
    return campo !== undefined
  }

  function anunciar(nuevo: DatosAviso) {
    setAviso(nuevo)
    window.setTimeout(() => refAviso.current?.focus(), 0)
  }

  async function guardar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (!form || guardando) return
    setAviso(null)
    const validacion = validarEmpresa(form)
    setErrores(validacion)
    if (Object.keys(validacion).length > 0) {
      setAviso({ tipo: 'error', texto: 'Revisa los datos marcados.' })
      enfocarPrimerError(validacion)
      return
    }
    setGuardando(true)
    try {
      const guardado = await guardarEmpresaAdmin(token, form)
      setGuardando(false)
      setDatos(guardado)
      setForm(formularioDesdeEmpresa(guardado))
      // El pie de esta misma carga toma los datos nuevos.
      recargarContactoPortal()
      anunciar({ tipo: 'exito', texto: MENSAJE_EMPRESA_GUARDADA })
    } catch (error) {
      setGuardando(false)
      if (es401(error)) return
      if (error instanceof ApiError && error.status === 400) {
        // CA-P9-36: errores por campo; lo escrito se conserva.
        const { porCampo, otros } = erroresEmpresaDe(error)
        setErrores(porCampo)
        const texto = [error.mensaje, ...otros].join(' ')
        if (enfocarPrimerError(porCampo)) setAviso({ tipo: 'error', texto })
        else anunciar({ tipo: 'error', texto })
        return
      }
      anunciar({ tipo: 'error', texto: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO })
    }
  }

  return (
    <section className="admin-empresa" aria-labelledby="empresa-titulo">
      <h3 id="empresa-titulo" className="admin-subtitulo">
        Datos de la empresa
      </h3>
      <p className="form-ayuda">{TEXTO_AVISO_EMPRESA}</p>
      {carga.estado === 'cargando' && <p role="status">Cargando los datos de la empresa...</p>}
      {carga.estado === 'error' && (
        <>
          <Aviso aviso={carga.aviso} />
          <button type="button" className="secondary-button inv-boton-auto" onClick={() => setIntento((i) => i + 1)}>
            Reintentar
          </button>
        </>
      )}
      {carga.estado === 'ok' && form && datos && (
        <form
          className="admin-form"
          aria-labelledby="empresa-titulo"
          onSubmit={(e) => void guardar(e)}
          noValidate
          aria-busy={guardando}
        >
          <p className="dato-secundario">
            {datos.configurado
              ? `Última modificación: ${datos.actualizadoPor ?? 'cuenta eliminada'}, ${formatearFechaHora(datos.actualizadoEn)}.`
              : 'Aún no se ha guardado ningún dato de la empresa.'}
          </p>
          <p className="dato-secundario">
            {datos.completo
              ? 'Estado: se muestran en el pie del portal.'
              : 'Estado: no se muestran en el portal (faltan datos).'}
          </p>
          <p className="campo-ayuda">Todos los campos son opcionales. Si dejas uno vacío, se retira del portal.</p>
          <Campo
            id={IDS.razonSocial}
            etiqueta="Razón social"
            valor={form.razonSocial}
            onCambio={(v) => cambiar('razonSocial', v)}
            autoComplete="organization"
            maxLength={RAZON_SOCIAL_MAX}
            errores={errores.razonSocial}
            requerido={false}
            deshabilitado={guardando}
          />
          <Campo
            id={IDS.nit}
            etiqueta="NIT"
            valor={form.nit}
            onCambio={(v) => cambiar('nit', v)}
            autoComplete="off"
            maxLength={NIT_MAX}
            errores={errores.nit}
            requerido={false}
            describidoPor="empresa-nit-ayuda"
            deshabilitado={guardando}
          />
          <p id="empresa-nit-ayuda" className="campo-ayuda">
            Con el dígito de verificación después del guion, por ejemplo 900.123.456-7. Los puntos son opcionales.
          </p>
          <Campo
            id={IDS.direccion}
            etiqueta="Dirección de notificaciones"
            valor={form.direccion}
            onCambio={(v) => cambiar('direccion', v)}
            autoComplete="street-address"
            maxLength={DIRECCION_MAX}
            errores={errores.direccion}
            requerido={false}
            deshabilitado={guardando}
          />
          <div ref={refAviso} tabIndex={-1}>
            <Aviso aviso={aviso} />
          </div>
          <div className="admin-botones">
            <button type="submit" disabled={guardando}>
              {guardando ? 'Guardando...' : 'Guardar datos de la empresa'}
            </button>
          </div>
        </form>
      )}
    </section>
  )
}
