// D-31: nota al inicio de cada formulario que explica el asterisco de los campos obligatorios.
// El asterisco de cada etiqueta se dibuja con CSS (clase campo-obligatorio) y los lectores de
// pantalla reciben aria-required en el campo, así que aquí se marca como decorativo.
export function NotaObligatorio() {
  return (
    <p className="nota-obligatorio">
      <span aria-hidden="true" className="asterisco">
        *
      </span>{' '}
      Campo obligatorio
    </p>
  )
}
