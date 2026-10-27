import { useCallback, useEffect, useRef, useState } from 'react'
import { obtenerFiltros } from './api/catalogo'
import {
  cerrarSesionServidor,
  esSesionTerminada,
  MENSAJE_SESION_TERMINADA,
  obtenerPerfil,
  type LoginResponse,
  type PerfilUsuario,
} from './api/auth'
import { ApiError, MENSAJE_INESPERADO } from './api/http'
import { inactividadGuardadaVencida, useInactividad } from './auth/inactivity'
import {
  borrarSesion,
  CLAVE_SESION,
  guardarSesion,
  guardarUltimaActividad,
  leerSesion,
  msHastaCaducidad,
  sesionCaducada,
  sesionDesdeLogin,
  type SesionGuardada,
} from './auth/session'
import { esAdministrador } from './auth/roles'
import { usePeticion } from './catalogo/usePeticion'
import { AdminUsuarios } from './components/AdminUsuarios'
import type { DatosAviso } from './components/Aviso'
import { Aviso } from './components/Aviso'
import { DetalleVehiculo } from './components/DetalleVehiculo'
import { Encabezado } from './components/Encabezado'
import { Inicio } from './components/Inicio'
import { LoginForm } from './components/LoginForm'
import { PerfilPanel } from './components/PerfilPanel'
import { RegistroForm } from './components/RegistroForm'
import { Resultados } from './components/Resultados'
import { HASH_CUENTA, HASH_REGISTRO, leerRuta, navegar, useHash, type Ruta } from './rutas'

type Vista =
  | { tipo: 'verificando'; sesion: SesionGuardada }
  | { tipo: 'sinConexion'; sesion: SesionGuardada; mensaje: string }
  | { tipo: 'login'; aviso: DatosAviso | null; email: string }
  | { tipo: 'registro' }
  | { tipo: 'perfil'; sesion: SesionGuardada; perfil: PerfilUsuario }
  /** P2: panel de administración; solo se entra si /api/auth/me trae el rol ADMINISTRADOR. */
  | { tipo: 'admin'; sesion: SesionGuardada; perfil: PerfilUsuario }

const AVISO_SESION_TERMINADA: DatosAviso = { tipo: 'error', texto: MENSAJE_SESION_TERMINADA }
const AVISO_SESION_CERRADA: DatosAviso = { tipo: 'exito', texto: 'Cerraste sesión correctamente.' }

/** Tiempo máximo de espera del logout en el servidor antes de seguir. */
const ESPERA_LOGOUT_MS = 5000
/** setTimeout no admite más de ~24,8 días. */
const MAX_TIMEOUT_MS = 2_147_483_647

function vistaInicial(): Vista {
  const sesion = leerSesion()
  if (!sesion) return { tipo: 'login', aviso: null, email: '' }
  // Token caducado o más de 30 min sin actividad (aunque el token siga vigente).
  if (sesionCaducada(sesion) || inactividadGuardadaVencida()) {
    borrarSesion()
    return { tipo: 'login', aviso: AVISO_SESION_TERMINADA, email: sesion.email }
  }
  return { tipo: 'verificando', sesion }
}

function loginCon(aviso: DatosAviso | null, email = ''): Vista {
  return { tipo: 'login', aviso, email }
}

const NOMBRE_PORTAL = 'Financiamos Tu Camión'

function tituloDocumento(ruta: Ruta): string {
  if (ruta.nombre === 'buscar') return `Buscar vehículos | ${NOMBRE_PORTAL}`
  if (ruta.nombre === 'vehiculo') return `Detalle del vehículo | ${NOMBRE_PORTAL}`
  if (ruta.nombre === 'cuenta') return `Mi cuenta | ${NOMBRE_PORTAL}`
  return NOMBRE_PORTAL
}

const cargarFiltros = (signal: AbortSignal) => obtenerFiltros(signal)

export default function App() {
  const [vista, setVista] = useState<Vista>(vistaInicial)
  const [cerrando, setCerrando] = useState(false)
  // Clave para reiniciar el formulario de login cuando cambia el aviso.
  const [claveLogin, setClaveLogin] = useState(0)
  const cerrandoRef = useRef(false)

  // P3: navegación por hash. La lógica de sesión (verificación, caducidad,
  // inactividad, otras pestañas) sigue montada en todas las rutas.
  const hash = useHash()
  const ruta = leerRuta(hash)
  const principal = useRef<HTMLElement>(null)
  const primeraRuta = useRef(true)
  // Opciones de filtros (tipos para Carros/Camiones, marcas, modelos): una sola carga.
  const [opcionesFiltros, reintentarOpcionesFiltros] = usePeticion(cargarFiltros, 'filtros')
  const tipos = opcionesFiltros.estado === 'ok' ? opcionesFiltros.datos.tipos : []

  const irALogin = useCallback((aviso: DatosAviso | null, email = '') => {
    setClaveLogin((k) => k + 1)
    setVista(loginCon(aviso, email))
  }, [])

  /** La sesión ya no vale (401 o token caducado): se borra sin llamar al servidor. */
  const terminarSesion = useCallback(
    (email = '') => {
      borrarSesion()
      irALogin(AVISO_SESION_TERMINADA, email)
    },
    [irALogin],
  )

  /** Cierra sesión en el servidor y borra la local aunque la llamada falle. */
  const cerrarSesion = useCallback(
    async (token: string, aviso: DatosAviso) => {
      if (cerrandoRef.current) return
      cerrandoRef.current = true
      setCerrando(true)
      borrarSesion()
      try {
        await Promise.race([
          cerrarSesionServidor(token),
          new Promise((resolver) => window.setTimeout(resolver, ESPERA_LOGOUT_MS)),
        ])
      } catch {
        // Se ignora: la sesión local ya se borró.
      } finally {
        cerrandoRef.current = false
        setCerrando(false)
        irALogin(aviso)
      }
    },
    [irALogin],
  )

  // Verificación de la sesión guardada contra /api/auth/me.
  useEffect(() => {
    if (vista.tipo !== 'verificando') return
    const { sesion } = vista
    if (sesionCaducada(sesion)) {
      terminarSesion(sesion.email)
      return
    }
    const control = new AbortController()
    obtenerPerfil(sesion.accessToken, control.signal)
      .then((perfil) => setVista({ tipo: 'perfil', sesion, perfil }))
      .catch((error: unknown) => {
        if (control.signal.aborted) return
        if (esSesionTerminada(error)) {
          terminarSesion(sesion.email)
          return
        }
        setVista({
          tipo: 'sinConexion',
          sesion,
          mensaje: error instanceof ApiError ? error.mensaje : MENSAJE_INESPERADO,
        })
      })
    return () => control.abort()
  }, [vista, terminarSesion])

  // Al caducar el token (2 h) se termina la sesión aunque haya actividad.
  const sesionActiva = vista.tipo === 'perfil' || vista.tipo === 'admin' ? vista.sesion : null
  useEffect(() => {
    if (!sesionActiva) return
    const restante = msHastaCaducidad(sesionActiva)
    const temporizador = window.setTimeout(
      () => terminarSesion(sesionActiva.email),
      Math.max(0, Math.min(restante, MAX_TIMEOUT_MS)),
    )
    return () => window.clearTimeout(temporizador)
  }, [sesionActiva, terminarSesion])

  // Si otra pestaña cierra la sesión, esta también la cierra.
  useEffect(() => {
    if (!sesionActiva) return
    const alCambiarAlmacen = (evento: StorageEvent) => {
      if (evento.key === CLAVE_SESION && evento.newValue === null) {
        irALogin(AVISO_SESION_TERMINADA, sesionActiva.email)
      }
    }
    window.addEventListener('storage', alCambiarAlmacen)
    return () => window.removeEventListener('storage', alCambiarAlmacen)
  }, [sesionActiva, irALogin])

  // Cierre automático por inactividad (HU-060): solo local, sin llamar a /logout,
  // para no cerrar la sesión del usuario en otros equipos (CA-060-10).
  const alExpirarInactividad = useCallback(() => {
    if (sesionActiva) terminarSesion(sesionActiva.email)
  }, [sesionActiva, terminarSesion])
  useInactividad(sesionActiva !== null, alExpirarInactividad)

  // Un 401 dentro del panel de administración termina la sesión igual que en P1.
  const alTerminarSesionAdmin = useCallback(() => {
    terminarSesion(sesionActiva?.email ?? '')
  }, [sesionActiva, terminarSesion])

  const alIniciarSesion = useCallback(async (respuesta: LoginResponse) => {
    const sesion = sesionDesdeLogin(respuesta)
    guardarSesion(sesion)
    guardarUltimaActividad()
    try {
      const perfil = await obtenerPerfil(sesion.accessToken)
      setVista({ tipo: 'perfil', sesion, perfil })
    } catch (error) {
      borrarSesion()
      if (esSesionTerminada(error)) throw new ApiError(401, MENSAJE_SESION_TERMINADA)
      throw error
    }
  }, [])

  // #/cuenta/registro abre el registro; #/cuenta vuelve al login desde el registro.
  const enCuenta = ruta.nombre === 'cuenta'
  const enRegistro = ruta.nombre === 'cuenta' && ruta.registro
  useEffect(() => {
    if (!enCuenta) return
    if (enRegistro) setVista((v) => (v.tipo === 'login' ? { tipo: 'registro' } : v))
    else setVista((v) => (v.tipo === 'registro' ? loginCon(null) : v))
  }, [enCuenta, enRegistro])

  // Al cambiar de ruta: título del documento y foco al título de la página.
  // En la cuenta, los formularios ya mueven el foco a su propio título.
  useEffect(() => {
    document.title = tituloDocumento(leerRuta(hash))
    if (primeraRuta.current) {
      primeraRuta.current = false
      return
    }
    const contenedor = principal.current
    if (!contenedor) return
    const activo = document.activeElement
    const focoEnCuenta =
      leerRuta(hash).nombre === 'cuenta' &&
      activo !== null &&
      activo !== contenedor &&
      contenedor.contains(activo)
    if (focoEnCuenta) return
    contenedor.querySelector<HTMLHeadingElement>('h1')?.focus()
  }, [hash])

  const conSesion = vista.tipo !== 'login' && vista.tipo !== 'registro'

  return (
    <>
      <Encabezado ruta={ruta} tipos={tipos} conSesion={conSesion} />
      <main id="contenido" ref={principal} tabIndex={-1} className="cat-principal">
        {/* Si la sesión termina mientras se navega el catálogo, también se avisa aquí (CA-REG-05). */}
        {ruta.nombre !== 'cuenta' && vista.tipo === 'login' && vista.aviso === AVISO_SESION_TERMINADA && (
          <div className="container">
            <Aviso aviso={AVISO_SESION_TERMINADA} />
          </div>
        )}
        {ruta.nombre === 'inicio' && <Inicio tipos={tipos} />}
        {ruta.nombre === 'buscar' && (
          <Resultados
            filtros={ruta.filtros}
            hash={hash}
            opciones={opcionesFiltros}
            onReintentarOpciones={reintentarOpcionesFiltros}
          />
        )}
        {ruta.nombre === 'vehiculo' && <DetalleVehiculo key={ruta.slug} slug={ruta.slug} />}
        {ruta.nombre === 'cuenta' && renderCuenta()}
      </main>
    </>
  )

  function renderCuenta() {
    return (
    <div className="container cuenta">
      <p className="eyebrow">RED DE VENTA</p>
      <h1 tabIndex={-1}>Financiamos Tu Camión</h1>
      <p>Tu próximo negocio puede comenzar con un camión.</p>

      {vista.tipo === 'verificando' && (
        <section aria-busy="true">
          <p role="status">Verificando tu sesión...</p>
        </section>
      )}

      {vista.tipo === 'sinConexion' && (
        <section>
          <h2>No pudimos verificar tu sesión</h2>
          <Aviso aviso={{ tipo: 'error', texto: vista.mensaje }} />
          <button type="button" onClick={() => setVista({ tipo: 'verificando', sesion: vista.sesion })}>
            Reintentar
          </button>
          <button
            className="secondary-button"
            type="button"
            disabled={cerrando}
            onClick={() => void cerrarSesion(vista.sesion.accessToken, AVISO_SESION_CERRADA)}
          >
            {cerrando ? 'Cerrando sesión...' : 'Cerrar sesión'}
          </button>
        </section>
      )}

      {vista.tipo === 'login' && (
        <LoginForm
          key={claveLogin}
          avisoInicial={vista.aviso}
          emailInicial={vista.email}
          onSesionIniciada={alIniciarSesion}
          onIrARegistro={() => {
            setVista({ tipo: 'registro' })
            navegar(HASH_REGISTRO)
          }}
        />
      )}

      {vista.tipo === 'registro' && (
        <RegistroForm
          onRegistrado={(mensaje, email) => {
            irALogin({ tipo: 'exito', texto: mensaje }, email)
            navegar(HASH_CUENTA)
          }}
          onIrALogin={() => {
            irALogin(null)
            navegar(HASH_CUENTA)
          }}
        />
      )}

      {vista.tipo === 'perfil' && (
        <PerfilPanel
          perfil={vista.perfil}
          cerrando={cerrando}
          onCerrarSesion={() => void cerrarSesion(vista.sesion.accessToken, AVISO_SESION_CERRADA)}
          onAbrirAdministracion={
            esAdministrador(vista.perfil.roles)
              ? () => setVista({ tipo: 'admin', sesion: vista.sesion, perfil: vista.perfil })
              : undefined
          }
        />
      )}

      {vista.tipo === 'admin' && esAdministrador(vista.perfil.roles) && (
        <AdminUsuarios
          token={vista.sesion.accessToken}
          usuarioIdPropio={vista.perfil.usuarioId}
          onSesionTerminada={alTerminarSesionAdmin}
          onVolver={() => setVista({ tipo: 'perfil', sesion: vista.sesion, perfil: vista.perfil })}
        />
      )}
    </div>
    )
  }
}
