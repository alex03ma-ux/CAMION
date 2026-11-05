# Guía rápida: contactos de vehículos (P5)

Para la responsable del portal. Explica cómo llega un contacto, quién lo ve y cómo se atiende.
Estado: rama `feature/p5-leads`, EN REVISIÓN. **Solo funciona con la API real cuando se haya aplicado la migración 013.**

## 1. Cómo contacta un visitante

1. Abre la ficha de un vehículo en el portal.
2. En el bloque **"Contactar"** tiene dos caminos:
   - **"Escribir por WhatsApp"**: abre WhatsApp con el asesor dueño del vehículo. Si el asesor no tiene un celular válido, o el vehículo es del administrador, abre el WhatsApp del portal. Esta conversación no queda registrada en el portal.
   - **El formulario**: nombre, teléfono o WhatsApp (celular o fijo colombiano de 10 dígitos), correo (opcional), mensaje (opcional, hasta 500 caracteres) y la casilla de autorización de datos, que empieza sin marcar y es obligatoria. Luego pulsa **"Enviar"**.
3. Ve el mensaje: "¡Listo! Recibimos tus datos. Te contactaremos pronto por teléfono o WhatsApp."
- No necesita cuenta. Si es un comprador con sesión iniciada, sus datos aparecen ya escritos y igual debe marcar la casilla.
- Si la misma persona vuelve a escribir por el mismo vehículo, no se crea un contacto nuevo: se agrega al historial del que ya existe.
- El asesor dueño no ve el formulario en sus propias publicaciones; en su lugar ve un aviso que lo remite a "Mis contactos".
- El enlace a la política de datos lleva a una página que, por ahora, solo avisa que el texto está en revisión legal (D-44).

## 2. A quién le llega

- Si el asesor dueño del vehículo está aprobado y tiene el plan vigente, el contacto es suyo.
- Si no (plan vencido, asesor bloqueado o vehículo del administrador), va a **Administración**.
- Si después vence el plan del asesor o el administrador lo bloquea, sus contactos abiertos pasan solos a Administración. No vuelven solos al renovar (D-46).
- Si el administrador reasigna un vehículo a otro asesor, sus contactos abiertos lo siguen.

## 3. Qué ve el asesor en "Mis contactos"

- Se entra desde su perfil, en el enlace **"Contactos"**. Si hay contactos sin abrir, dice "Contactos (1 nuevo)" o "Contactos (N nuevos)".
- Ve solo los contactos que tiene asignados, del más reciente al más antiguo, y puede filtrarlos por estado: "Pendiente de gestión" o "Finalizado".
- Al abrir uno ve los datos de la persona (no se pueden editar), el vehículo, el historial y el bloque **"Seguimiento"**:
  - **"Guardar nota"**: anota lo que hizo (de 5 a 500 caracteres);
  - **"Cerrar contacto"**: elige el resultado (se concretó la venta, no le interesó, no respondió, el vehículo ya no está disponible, contacto duplicado o inválido, u otro, que pide una observación).
- No puede reabrir ni reasignar, ni ver los contactos de otros.

## 4. Qué hace el administrador en "Contactos"

- Ve **todos** los contactos, con filtros por estado, responsable, vehículo y fechas.
- Puede anotar y cerrar cualquier contacto, como el asesor.
- **"Reasignar"** (contacto abierto): lo pasa a otro asesor aprobado o a Administración, con un motivo. Queda auditado.
- **"Reabrir"** (contacto cerrado): lo vuelve a "Pendiente de gestión" con el mismo responsable, con un motivo.
- **"Autorización de tratamiento de datos"**: solo el administrador ve esta sección en el detalle, con el formulario, la versión de la política y la fecha y hora de cada autorización. No se puede editar ni borrar.

## 5. Prueba manual (después de aplicar la migración 013)

Antes: el coordinador acompaña el respaldo y la migración según `docs/coordinacion/REVISION-SEGURIDAD-P5.md` (respaldo con nombre nuevo en `C:\Respaldo_CAMION\`, verificación antes y después) y arranca la API de esta rama. Usar solo datos inventados.

1. **Contacto sin cuenta.** Sin iniciar sesión, abrir un vehículo de un asesor con plan vigente, llenar el formulario con datos ficticios, marcar la casilla y pulsar "Enviar". Debe salir el mensaje de "¡Listo!".
2. **Casilla obligatoria.** Repetir sin marcar la casilla: debe pedir la autorización y no enviar.
3. **Contacto con cuenta.** Entrar como comprador, abrir otro vehículo y comprobar que los datos aparecen ya escritos; enviar.
4. **Como asesor.** Entrar con la cuenta del asesor dueño: el perfil debe mostrar "Contactos (N nuevos)". Abrir el contacto, guardar una nota y cerrarlo con un resultado.
5. **Propia publicación.** Con la misma cuenta de asesor, abrir la ficha de uno de sus vehículos: no debe aparecer el formulario.
6. **Como administradora.** Entrar con la cuenta de administrador, abrir "Contactos" y ver los contactos de la prueba. Reabrir el que cerró el asesor (con motivo), reasignarlo a Administración o a otro asesor (con motivo) y revisar el historial y la sección de autorización.
7. Anotar en la bitácora lo que salió bien y lo que no.
