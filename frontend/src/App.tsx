import { useCallback, useEffect, useRef, useState } from 'react'
import { obtenerFiltros, type OpcionCatalogo } from './api/catalogo'
import {
  cerrarSesionServidor,
  esSesionTerminada,
  MENSAJE_SESION_TERMINADA,
  obtenerPerfil,
  type LoginResponse,
  type PerfilUsuario,
} from './api/auth'
import { ApiError, MENSAJE_INESPERADO, MENSAJE_SIN_PERMISO } from './api/http'
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
import { AdminCatalogos } from './components/AdminCatalogos'
import { AdminIndicadores } from './components/AdminIndicadores'
import { AdminInicio } from './components/AdminInicio'
import { AdminParametros } from './components/AdminParametros'
import { AdminPortada } from './components/AdminPortada'
import { AdminPlanes } from './components/AdminPlanes'
import { AdminReportes } from './components/AdminReportes'
import { AdminSuscripciones } from './components/AdminSuscripciones'
import { AdminUsuarios } from './components/AdminUsuarios'
import { Contactos } from './components/Contactos'
import type { DatosAviso } from './components/Aviso'
import { Aviso } from './components/Aviso'
import { AvisoPublico } from './components/AvisoPublico'
import { BusquedaVehiculo } from './components/BusquedaVehiculo'
import { CategoriaSinTipo } from './components/CategoriaSinTipo'
import { DetalleVehiculo } from './components/DetalleVehiculo'
import { PiePortal } from './components/PiePortal'
import { Encabezado } from './components/Encabezado'
import { Inicio } from './components/Inicio'
import { InventarioPagina } from './components/InventarioPagina'
import { LoginForm } from './components/LoginForm'
import { MisIndicadores, TITULO_MIS_INDICADORES } from './components/MisIndicadores'
import { MisPublicaciones } from './components/MisPublicaciones'
import { MisSolicitudes, TITULO_MIS_SOLICITUDES } from './components/MisSolicitudes'
import { NoEncontrada } from './components/NoEncontrada'
import { NavAdmin } from './components/NavAdmin'
import { PerfilPanel } from './components/PerfilPanel'
import { Privacidad } from './components/Privacidad'
import { Terminos } from './components/Terminos'
import { RegistroForm } from './components/RegistroForm'
import { RestablecerContrasena } from './components/RestablecerContrasena'
import { Resultados } from './components/Resultados'
import { SolicitudCredito } from './components/SolicitudCredito'
import { Ventas } from './components/Ventas'
import { aplicarMetaPagina } from './seo'
import { metaDeRuta } from './seoPaginas'
import { MARCA, NOMBRE_PORTAL } from './marca'
import {
  esRutaContactos,
  esRutaInventario,
  fijarTiposCatalogo,
  instalarNavegacionEnlaces,
  RUTA_CUENTA,
  RUTA_REGISTRO,
  leerRuta,
  navegar,
  rutaBuscar,
  useUbicacion,
  type Ruta,
  type SeccionAdmin,
} from './rutas'

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

const cargarFiltros = (signal: AbortSignal) => obtenerFiltros(signal)
const SIN_TIPOS: OpcionCatalogo[] = []

export default function App() {
  const [vista, setVista] = useState<Vista>(vistaInicial)
  const [cerrando, setCerrando] = useState(false)
  // Clave para reiniciar el formulario de login cuando cambia el aviso.
  const [claveLogin, setClaveLogin] = useState(0)
  const cerrandoRef = useRef(false)

  // P10 (CA-P10-33 a 41): navegación por la dirección sin `#` (los enlaces viejos `#/…` se convierten al leerla).
  // La lógica de sesión (verificación, caducidad, inactividad, otras pestañas) sigue montada en todas las rutas.
  const ubicacion = useUbicacion()
  // Opciones de filtros (tipos para Carros/Camiones, marcas, modelos): una sola carga.
  const [opcionesFiltros, reintentarOpcionesFiltros] = usePeticion(cargarFiltros, 'filtros')
  const tipos = opcionesFiltros.estado === 'ok' ? opcionesFiltros.datos.tipos : SIN_TIPOS
  // CA-P10-39: con los tipos se traducen /camiones… ↔ tipoId (antes de leer la ruta).
  fijarTiposCatalogo(tipos)
  const ruta = leerRuta(ubicacion)
  const principal = useRef<HTMLElement>(null)
  const primeraRuta = useRef(true)

  // CA-P10-36: los clics en enlaces internos cambian de pantalla sin recargar.
  useEffect(() => instalarNavegacionEnlaces(), [])

  // CA-P10-39: /vehiculos?tipoId=<id de una categoría> (o un enlace viejo #/buscar?tipoId=N) pasa a /camiones….
  const direccionCanonicaBusqueda = ruta.nombre === 'buscar' && ruta.categoria === null ? rutaBuscar(ruta.filtros) : null
  useEffect(() => {
    if (direccionCanonicaBusqueda === null || direccionCanonicaBusqueda.startsWith('/vehiculos')) return
    navegar(direccionCanonicaBusqueda, { reemplazar: true })
  }, [direccionCanonicaBusqueda])

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
  const perfilActivo = vista.tipo === 'perfil' || vista.tipo === 'admin' ? vista.perfil : null
  // P5: formularios públicos (contacto de la ficha y avisos de P5b): con sesión se prellenan y se liga la cuenta.
  const sesionFormulario =
    sesionActiva && perfilActivo ? { token: sesionActiva.accessToken, perfil: perfilActivo } : null
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

  // /cuenta/registro abre el registro; /cuenta vuelve al login desde el registro.
  const enCuenta = ruta.nombre === 'cuenta'
  const enRegistro = ruta.nombre === 'cuenta' && ruta.registro
  useEffect(() => {
    if (!enCuenta) return
    if (enRegistro) setVista((v) => (v.tipo === 'login' ? { tipo: 'registro' } : v))
    else setVista((v) => (v.tipo === 'registro' ? loginCon(null) : v))
  }, [enCuenta, enRegistro])

  // P10 (CA-P10-21 a 32): título, descripción, robots y canonical de la página. La ficha del vehículo los fija
  // DetalleVehiculo con sus datos (CA-P10-22 a 24, 80).
  const categoriaVacia =
    ruta.nombre === 'buscar' && ruta.categoria !== null && ruta.filtros.tipoId === '' && opcionesFiltros.estado === 'ok'
  const enFicha = ruta.nombre === 'vehiculo'
  useEffect(() => {
    if (enFicha) return
    aplicarMetaPagina(metaDeRuta(leerRuta(ubicacion), { categoriaVacia }))
  }, [ubicacion, categoriaVacia, enFicha])

  // Al cambiar de ruta: título del documento y foco al título de la página (CA-P10-38).
  // En la cuenta, los formularios ya mueven el foco a su propio título.
  useEffect(() => {
    if (primeraRuta.current) {
      primeraRuta.current = false
      return
    }
    const contenedor = principal.current
    if (!contenedor) return
    const activo = document.activeElement
    const focoEnCuenta =
      leerRuta(ubicacion).nombre === 'cuenta' &&
      activo !== null &&
      activo !== contenedor &&
      contenedor.contains(activo)
    if (focoEnCuenta) return
    contenedor.querySelector<HTMLHeadingElement>('h1')?.focus()
  }, [ubicacion])

  const conSesion = vista.tipo !== 'login' && vista.tipo !== 'registro'

  // P4p: al entrar en una sección del administrador (/cuenta/planes…) se deja la vista "admin"
  // del botón "Administrar usuarios", para que "Mi perfil" (/cuenta) vuelva al perfil.
  const seccionAdmin: SeccionAdmin | undefined = ruta.nombre === 'cuenta' ? ruta.seccion : undefined
  const consultaSeccion: string | undefined = ruta.nombre === 'cuenta' ? ruta.consulta : undefined
  useEffect(() => {
    if (seccionAdmin === undefined) return
    setVista((v) => (v.tipo === 'admin' ? { tipo: 'perfil', sesion: v.sesion, perfil: v.perfil } : v))
  }, [seccionAdmin])

  // P4a: el inventario exige sesión; sin ella se va al login de /cuenta
  // (si la sesión terminó, allí se ve el aviso "Tu sesión terminó...").
  // P4b: "Mis publicaciones" también exige sesión.
  // P5: los contactos también. P6: "Mis solicitudes" también.
  const enInventario =
    esRutaInventario(ruta) ||
    ruta.nombre === 'publicaciones' ||
    esRutaContactos(ruta) ||
    ruta.nombre === 'misSolicitudes' ||
    ruta.nombre === 'ventas' ||
    ruta.nombre === 'misIndicadores'
  useEffect(() => {
    if (enInventario && !conSesion) navegar(RUTA_CUENTA)
  }, [enInventario, conSesion])

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
        {ruta.nombre === 'buscar' && (ruta.categoria === null || ruta.filtros.tipoId !== '') && (
          <Resultados
            filtros={ruta.filtros}
            ubicacion={ubicacion}
            opciones={opcionesFiltros}
            onReintentarOpciones={reintentarOpcionesFiltros}
          />
        )}
        {/* CA-P10-25, 40: categoría cuyo tipo aún no se conoce o que no tiene vehículos visibles. */}
        {ruta.nombre === 'buscar' && ruta.categoria !== null && ruta.filtros.tipoId === '' && (
          <CategoriaSinTipo categoria={ruta.categoria} opciones={opcionesFiltros} onReintentar={reintentarOpcionesFiltros} />
        )}
        {ruta.nombre === 'vehiculo' && (
          <DetalleVehiculo
            key={ruta.slug}
            slug={ruta.slug}
            sesion={sesionFormulario}
          />
        )}
        {ruta.nombre === 'privacidad' && <Privacidad />}
        {/* P9 (CA-P9-17): términos en revisión legal; sin cuenta o con cualquier sesión. */}
        {ruta.nombre === 'terminos' && <Terminos />}
        {/* P5b (D-6, D-51 A): avisos públicos; sin cuenta o con cualquier sesión. */}
        {ruta.nombre === 'vendeTuCamion' && (
          <AvisoPublico key="VENDE_TU_CAMION" variante="VENDE_TU_CAMION" sesion={sesionFormulario} />
        )}
        {ruta.nombre === 'seguroTodoRiesgo' && (
          <AvisoPublico key="SEGURO_TODO_RIESGO" variante="SEGURO_TODO_RIESGO" sesion={sesionFormulario} />
        )}
        {/* P6 (D-54, D-56, D-58 A): crédito y búsqueda públicos; sin cuenta o con cualquier sesión. */}
        {ruta.nombre === 'credito' && (
          <SolicitudCredito key={ruta.slug ?? 'portada'} slug={ruta.slug} sesion={sesionFormulario} />
        )}
        {ruta.nombre === 'buscoVehiculo' && (
          <BusquedaVehiculo
            opciones={opcionesFiltros}
            onReintentarOpciones={reintentarOpcionesFiltros}
            sesion={sesionFormulario}
          />
        )}
        {ruta.nombre === 'misSolicitudes' && renderMisSolicitudes()}
        {ruta.nombre === 'cuenta' && renderCuenta()}
        {(ruta.nombre === 'inventario' ||
          ruta.nombre === 'inventarioNuevo' ||
          ruta.nombre === 'inventarioVehiculo') &&
          renderInventario(ruta)}
        {ruta.nombre === 'publicaciones' && renderPublicaciones()}
        {/* Lista y detalle en el mismo sitio: los filtros se conservan al ir y volver del detalle. */}
        {esRutaContactos(ruta) &&
          renderContactos(
            ruta.nombre === 'contacto' ? ruta.id : undefined,
            ruta.nombre === 'contactos' ? ruta.consulta : undefined,
          )}
        {ruta.nombre === 'ventas' && renderVentas(ruta.consulta)}
        {ruta.nombre === 'misIndicadores' && renderMisIndicadores()}
        {/* P8 (D-73 D): pública; sin cuenta o con cualquier sesión. */}
        {ruta.nombre === 'restablecerContrasena' && <RestablecerContrasena />}
        {/* P9 (CA-P9-48): rutas `/...` desconocidas. */}
        {ruta.nombre === 'noEncontrada' && <NoEncontrada />}
      </main>
      <PiePortal tipos={tipos} conSesion={conSesion} />
    </>
  )

  function renderInventario(rutaInventario: Extract<Ruta, { nombre: 'inventario' | 'inventarioNuevo' | 'inventarioVehiculo' }>) {
    if (vista.tipo === 'perfil' || vista.tipo === 'admin') {
      return (
        <InventarioPagina
          key={vista.perfil.usuarioId}
          ruta={rutaInventario}
          token={vista.sesion.accessToken}
          perfil={vista.perfil}
          onSesionTerminada={alTerminarSesionAdmin}
        />
      )
    }
    return (
      <div className="cat-pagina inv-pagina">
        <h1 tabIndex={-1} className="inv-titulo">
          Inventario
        </h1>
        {vista.tipo === 'verificando' && <p role="status">Verificando tu sesión...</p>}
        {vista.tipo === 'sinConexion' && (
          <>
            <Aviso aviso={{ tipo: 'error', texto: vista.mensaje }} />
            <p>
              <a href={RUTA_CUENTA}>Ir a mi cuenta</a>
            </p>
          </>
        )}
        {(vista.tipo === 'login' || vista.tipo === 'registro') && (
          <p role="status">Inicia sesión para ver el inventario.</p>
        )}
      </div>
    )
  }

  function renderPublicaciones() {
    if (vista.tipo === 'perfil' || vista.tipo === 'admin') {
      return (
        <MisPublicaciones
          key={vista.perfil.usuarioId}
          token={vista.sesion.accessToken}
          perfil={vista.perfil}
          onSesionTerminada={alTerminarSesionAdmin}
        />
      )
    }
    return (
      <div className="cat-pagina inv-pagina">
        <h1 tabIndex={-1} className="inv-titulo">
          Mis publicaciones
        </h1>
        {vista.tipo === 'verificando' && <p role="status">Verificando tu sesión...</p>}
        {vista.tipo === 'sinConexion' && (
          <>
            <Aviso aviso={{ tipo: 'error', texto: vista.mensaje }} />
            <p>
              <a href={RUTA_CUENTA}>Ir a mi cuenta</a>
            </p>
          </>
        )}
        {(vista.tipo === 'login' || vista.tipo === 'registro') && (
          <p role="status">Inicia sesión para ver tus publicaciones.</p>
        )}
      </div>
    )
  }

  function renderMisSolicitudes() {
    if (vista.tipo === 'perfil' || vista.tipo === 'admin') {
      return (
        <MisSolicitudes
          key={vista.perfil.usuarioId}
          token={vista.sesion.accessToken}
          perfil={vista.perfil}
          onSesionTerminada={alTerminarSesionAdmin}
        />
      )
    }
    return (
      <div className="cat-pagina inv-pagina">
        <h1 tabIndex={-1} className="inv-titulo">
          {TITULO_MIS_SOLICITUDES}
        </h1>
        {vista.tipo === 'verificando' && <p role="status">Verificando tu sesión...</p>}
        {vista.tipo === 'sinConexion' && (
          <>
            <Aviso aviso={{ tipo: 'error', texto: vista.mensaje }} />
            <p>
              <a href={RUTA_CUENTA}>Ir a mi cuenta</a>
            </p>
          </>
        )}
        {(vista.tipo === 'login' || vista.tipo === 'registro') && (
          <p role="status">Inicia sesión para ver tus solicitudes.</p>
        )}
      </div>
    )
  }

  /** P7 (D-62 c A): "Mis ventas" del asesor en /ventas (el administrador ve todas). */
  function renderVentas(consulta: string | undefined) {
    if (vista.tipo === 'perfil' || vista.tipo === 'admin') {
      return (
        <Ventas
          key={`${vista.perfil.usuarioId}|${consulta ?? ''}`}
          token={vista.sesion.accessToken}
          perfil={vista.perfil}
          consulta={consulta}
          nivel="pagina"
          onSesionTerminada={alTerminarSesionAdmin}
        />
      )
    }
    return (
      <div className="cat-pagina inv-pagina">
        <h1 tabIndex={-1} className="inv-titulo">
          Mis ventas
        </h1>
        {vista.tipo === 'verificando' && <p role="status">Verificando tu sesión...</p>}
        {vista.tipo === 'sinConexion' && (
          <>
            <Aviso aviso={{ tipo: 'error', texto: vista.mensaje }} />
            <p>
              <a href={RUTA_CUENTA}>Ir a mi cuenta</a>
            </p>
          </>
        )}
        {(vista.tipo === 'login' || vista.tipo === 'registro') && (
          <p role="status">Inicia sesión para ver tus ventas.</p>
        )}
      </div>
    )
  }

  /** P8 (D-70 A): "Mis indicadores" del asesor en /mis-indicadores. */
  function renderMisIndicadores() {
    if (vista.tipo === 'perfil' || vista.tipo === 'admin') {
      return (
        <MisIndicadores
          key={vista.perfil.usuarioId}
          token={vista.sesion.accessToken}
          perfil={vista.perfil}
          onSesionTerminada={alTerminarSesionAdmin}
        />
      )
    }
    return (
      <div className="cat-pagina inv-pagina">
        <h1 tabIndex={-1} className="inv-titulo">
          {TITULO_MIS_INDICADORES}
        </h1>
        {vista.tipo === 'verificando' && <p role="status">Verificando tu sesión...</p>}
        {vista.tipo === 'sinConexion' && (
          <>
            <Aviso aviso={{ tipo: 'error', texto: vista.mensaje }} />
            <p>
              <a href={RUTA_CUENTA}>Ir a mi cuenta</a>
            </p>
          </>
        )}
        {(vista.tipo === 'login' || vista.tipo === 'registro') && (
          <p role="status">Inicia sesión para ver tus indicadores.</p>
        )}
      </div>
    )
  }

  function renderContactos(id: number | undefined, consulta: string | undefined) {
    if (vista.tipo === 'perfil' || vista.tipo === 'admin') {
      return (
        <Contactos
          key={`${vista.perfil.usuarioId}|${consulta ?? ''}`}
          token={vista.sesion.accessToken}
          perfil={vista.perfil}
          id={id}
          consulta={consulta}
          onSesionTerminada={alTerminarSesionAdmin}
        />
      )
    }
    return (
      <div className="cat-pagina inv-pagina">
        <h1 tabIndex={-1} className="inv-titulo">
          Contactos
        </h1>
        {vista.tipo === 'verificando' && <p role="status">Verificando tu sesión...</p>}
        {vista.tipo === 'sinConexion' && (
          <>
            <Aviso aviso={{ tipo: 'error', texto: vista.mensaje }} />
            <p>
              <a href={RUTA_CUENTA}>Ir a mi cuenta</a>
            </p>
          </>
        )}
        {(vista.tipo === 'login' || vista.tipo === 'registro') && (
          <p role="status">Inicia sesión para ver tus contactos.</p>
        )}
      </div>
    )
  }

  function renderCuenta() {
    return (
    <div className="container cuenta">
      {/* P10 (CA-P10-17, D-87). */}
      <p className="eyebrow">{MARCA.toUpperCase()}</p>
      <h1 tabIndex={-1}>{NOMBRE_PORTAL}</h1>
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
            navegar(RUTA_REGISTRO)
          }}
        />
      )}

      {vista.tipo === 'registro' && (
        <RegistroForm
          onRegistrado={(mensaje, email) => {
            irALogin({ tipo: 'exito', texto: mensaje }, email)
            navegar(RUTA_CUENTA)
          }}
          onIrALogin={() => {
            irALogin(null)
            navegar(RUTA_CUENTA)
          }}
        />
      )}

      {vista.tipo === 'perfil' && seccionAdmin !== undefined && renderSeccionAdmin(seccionAdmin, consultaSeccion)}

      {vista.tipo === 'perfil' && seccionAdmin === undefined && (
        <PerfilPanel
          perfil={vista.perfil}
          cerrando={cerrando}
          onCerrarSesion={() => void cerrarSesion(vista.sesion.accessToken, AVISO_SESION_CERRADA)}
          token={vista.sesion.accessToken}
          onSesionTerminada={alTerminarSesionAdmin}
          onAbrirAdministracion={
            esAdministrador(vista.perfil.roles)
              ? () => setVista({ tipo: 'admin', sesion: vista.sesion, perfil: vista.perfil })
              : undefined
          }
        />
      )}

      {vista.tipo === 'admin' && esAdministrador(vista.perfil.roles) && seccionAdmin === undefined && (
        <NavAdmin actual="usuarios" />
      )}
      {vista.tipo === 'admin' && esAdministrador(vista.perfil.roles) && seccionAdmin === undefined && (
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

  /** P4p: Usuarios, Planes, Suscripciones y Catálogos; solo con el rol ADMINISTRADOR según /me. */
  function renderSeccionAdmin(seccion: SeccionAdmin, consulta: string | undefined) {
    if (vista.tipo !== 'perfil') return null
    if (!esAdministrador(vista.perfil.roles)) {
      return (
        <section aria-labelledby="sin-permiso-titulo">
          <h2 id="sin-permiso-titulo">Administración</h2>
          <Aviso aviso={{ tipo: 'error', texto: MENSAJE_SIN_PERMISO }} />
          <p>
            <a href={RUTA_CUENTA}>Volver a mi cuenta</a>
          </p>
        </section>
      )
    }
    const token = vista.sesion.accessToken
    return (
      <>
        <NavAdmin actual={seccion} />
        {/* P7 (D-64 A): panel de contadores. */}
        {seccion === 'inicio' && <AdminInicio token={token} onSesionTerminada={alTerminarSesionAdmin} />}
        {seccion === 'usuarios' && (
          <AdminUsuarios
            key={consulta ?? ''}
            consulta={consulta}
            token={token}
            usuarioIdPropio={vista.perfil.usuarioId}
            onSesionTerminada={alTerminarSesionAdmin}
            onVolver={() => navegar(RUTA_CUENTA)}
          />
        )}
        {seccion === 'planes' && <AdminPlanes token={token} onSesionTerminada={alTerminarSesionAdmin} />}
        {seccion === 'suscripciones' && (
          <AdminSuscripciones
            key={consulta ?? ''}
            consulta={consulta}
            token={token}
            onSesionTerminada={alTerminarSesionAdmin}
          />
        )}
        {/* P7 (D-62 A): historial de todas las ventas. */}
        {seccion === 'ventas' && (
          <Ventas
            key={consulta ?? ''}
            token={token}
            perfil={vista.perfil}
            consulta={consulta}
            nivel="seccion"
            onSesionTerminada={alTerminarSesionAdmin}
          />
        )}
        {seccion === 'catalogos' && <AdminCatalogos token={token} onSesionTerminada={alTerminarSesionAdmin} />}
        {seccion === 'portada' && <AdminPortada token={token} onSesionTerminada={alTerminarSesionAdmin} />}
        {/* P7 (D-63 A): datos de contacto del portal. */}
        {seccion === 'parametros' && <AdminParametros token={token} onSesionTerminada={alTerminarSesionAdmin} />}
        {/* P8 (D-68 A, D-71): reportes con exportación CSV. */}
        {seccion === 'reportes' && (
          <AdminReportes key={consulta ?? ''} consulta={consulta} token={token} onSesionTerminada={alTerminarSesionAdmin} />
        )}
        {/* P8 (D-69 A/A): indicadores con valores en pesos y gráficos sin librería. */}
        {seccion === 'indicadores' && <AdminIndicadores token={token} onSesionTerminada={alTerminarSesionAdmin} />}
      </>
    )
  }
}
