import { useState, type FormEvent } from 'react'

type Perfil = {
  usuarioId: string
  nombreCompleto: string
  email: string
  phoneNumber: string
  roles: string[]
}

type Modo = 'login' | 'registro'

export default function App() {
  const [modo, setModo] = useState<Modo>('login')
  const [nombreCompleto, setNombreCompleto] = useState('')
  const [email, setEmail] = useState('')
  const [telefono, setTelefono] = useState('')
  const [password, setPassword] = useState('')
  const [perfil, setPerfil] = useState<Perfil | null>(null)
  const [mensaje, setMensaje] = useState('')
  const [cargando, setCargando] = useState(false)

  function cambiarModo(nuevoModo: Modo) {
    setModo(nuevoModo)
    setMensaje('')
    setPassword('')
  }

  async function enviarFormulario(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setCargando(true)
    setMensaje('')

    try {
      if (modo === 'registro') {
        const respuesta = await fetch('/api/auth/registro/comprador', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            nombreCompleto,
            email,
            telefono,
            password,
          }),
        })

        if (!respuesta.ok) {
          if (respuesta.status === 409) {
            throw new Error('Este correo ya está registrado.')
          }

          throw new Error(
            'No se pudo crear la cuenta. Revisa los datos ingresados.'
          )
        }

        setModo('login')
        setPassword('')
        setMensaje('Cuenta creada correctamente. Ya puedes iniciar sesión.')
        return
      }

      const respuesta = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      })

      if (!respuesta.ok) {
        throw new Error('No se pudo iniciar sesión. Verifica tus datos.')
      }

      const datos = await respuesta.json()

      const respuestaPerfil = await fetch('/api/auth/me', {
        headers: {
          Authorization: `Bearer ${datos.accessToken}`,
        },
      })

      if (!respuestaPerfil.ok) {
        throw new Error('No se pudo consultar el perfil.')
      }

      const usuario: Perfil = await respuestaPerfil.json()
      setPerfil(usuario)
      setPassword('')
    } catch (error) {
      setMensaje(
        error instanceof Error
          ? error.message
          : 'Ocurrió un error inesperado.'
      )
    } finally {
      setCargando(false)
    }
  }

  function cerrarSesion() {
    setPerfil(null)
    setNombreCompleto('')
    setEmail('')
    setTelefono('')
    setPassword('')
    setMensaje('')
    setModo('login')
  }

  return (
    <main className="container">
      <p className="eyebrow">RED DE VENTA</p>
      <h1>Financiamos Tu Camión</h1>
      <p>Tu próximo negocio puede comenzar con un camión.</p>

      {perfil ? (
        <section>
          <h2>¡Bienvenido, {perfil.nombreCompleto}!</h2>
          <p><strong>Correo:</strong> {perfil.email}</p>
          <p><strong>Teléfono:</strong> {perfil.phoneNumber}</p>
          <p><strong>Rol:</strong> {perfil.roles.join(', ')}</p>

          <button type="button" onClick={cerrarSesion}>
            Cerrar sesión
          </button>
        </section>
      ) : (
        <form onSubmit={enviarFormulario}>
          <h2>
            {modo === 'login' ? 'Iniciar sesión' : 'Crear cuenta'}
          </h2>

          {modo === 'registro' && (
            <>
              <label htmlFor="nombre">Nombre completo</label>
              <input
                id="nombre"
                type="text"
                value={nombreCompleto}
                onChange={(e) => setNombreCompleto(e.target.value)}
                minLength={2}
                maxLength={150}
                required
              />
            </>
          )}

          <label htmlFor="email">Correo electrónico</label>
          <input
            id="email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            maxLength={254}
            required
          />

          {modo === 'registro' && (
            <>
              <label htmlFor="telefono">Teléfono</label>
              <input
                id="telefono"
                type="tel"
                value={telefono}
                onChange={(e) => setTelefono(e.target.value)}
                maxLength={25}
                required
              />
            </>
          )}

          <label htmlFor="password">Contraseña</label>
          <input
            id="password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            minLength={modo === 'registro' ? 8 : undefined}
            autoComplete={
              modo === 'registro' ? 'new-password' : 'current-password'
            }
            required
          />

          <button type="submit" disabled={cargando}>
            {cargando
              ? 'Procesando...'
              : modo === 'login'
                ? 'Ingresar'
                : 'Crear mi cuenta'}
          </button>

          {mensaje && (
            <p
              role={mensaje.startsWith('Cuenta creada') ? 'status' : 'alert'}
            >
              {mensaje}
            </p>
          )}

          <p className="auth-switch">
            {modo === 'login'
              ? '¿Todavía no tienes cuenta?'
              : '¿Ya tienes una cuenta?'}
          </p>

          <button
            className="secondary-button"
            type="button"
            onClick={() =>
              cambiarModo(modo === 'login' ? 'registro' : 'login')
            }
          >
            {modo === 'login' ? 'Registrarme' : 'Volver a iniciar sesión'}
          </button>
        </form>
      )}
    </main>
  )
}