# Registro de decisiones

Estados: PENDIENTE (requiere a la responsable) / TOMADA / DESCARTADA.
Las decisiones D-1 a D-5 se detectaron al comparar los documentos fuente el 2026-10-26 y la responsable las resolvió el mismo día.

## D-1 — ¿Qué numeración de HU es la oficial? · TOMADA (2026-10-26)
- Contexto: en `requisitos-funcionales-v3.0.md` la matriz consolidada (sección 7) tiene 87 HU (HU-043 = Aprobar Usuario), pero la lista de la sección 10 tiene 90 (HU-043 = Registrar Usuario, todo desplazado +1, y un módulo nuevo "CRM Comercial" HU-089/HU-090).
- **Decisión: la matriz de 87 HU (sección 7) es la numeración oficial.** Commits, criterios y pruebas citan esa numeración. El módulo CRM Comercial (HU-089/HU-090 de la sección 10) queda fuera del alcance actual y se tratará como cambio de alcance posterior.

## D-2 — ¿La placa es obligatoria y única? · TOMADA (2026-10-26)
- Contexto: la minuta dice "placa única"; RN-VE-002 (v3.0) dice "Dato opcional. No obligatoria ni única".
- **Decisión: la placa es opcional, pero única cuando se registra.** Se permite dejarla vacía; si se informa, no puede repetirse en otro vehículo (validación en API y restricción en base de datos que admita varios vacíos). Esto modifica RN-VE-002 respecto a la línea base v3.0.

## D-3 — ¿Quién requiere aprobación del administrador? · TOMADA (2026-10-26, corregida)
- Contexto: RN-US-001 dice "Todo usuario debe ser aprobado"; la minuta indicaba solo asesores y el registro de comprador actual permite entrar sin aprobación.
- Primera respuesta ("todos los usuarios") **anulada** por la responsable el mismo día.
- **Decisión: solo los asesores requieren aprobación del administrador.** RN-US-001 aplica a asesores.
- **Perfil comprador** (perfil independiente, distinto de asesor y administrador):
  - El registro es **opcional**: sin cuenta se consulta el catálogo y se contacta (RN-SE-005, RN-CL-001).
  - Si se registra, el acceso es **inmediato**, sin aprobación (como funciona hoy en `main`).
  - Acceso de **solo lectura**: no puede crear, editar ni modificar vehículos, publicaciones ni datos de otros usuarios.
  - Puede buscar opciones de compra, pedir un seguro, solicitar crédito y vender su vehículo; todo lo atiende el administrador (ver D-6 y D-7).

## D-6 — Alcance nuevo: "Vende tu camión" y "Compra tu seguro todo riesgo" · TOMADA (2026-10-26)
- Contexto: funciones nuevas pedidas por la responsable; no están en la línea base v3.0 (87 HU). Se identifican provisionalmente como **HU-N01** y **HU-N02** hasta que se integren al documento maestro.
- **HU-N01 — Aviso "Vende tu camión"** (portal público):
  - Para el cliente que quiere vender su propio camión o carro.
  - La solicitud va **siempre al administrador**, nunca a un asesor.
  - Formulario: **nombre** (obligatorio), **teléfono WhatsApp** (obligatorio, formato de teléfono válido), **descripción del vehículo** (campo abierto, obligatorio, máximo **500 caracteres**).
  - No crea una publicación: solo registra una solicitud para el administrador.
- **HU-N02 — Aviso "Compra tu seguro todo riesgo"** (portal público):
  - Formulario: **nombre del cliente**, **teléfono** y **tipo de vehículo** (todos obligatorios).
  - La solicitud la atiende **el administrador**.
- Ambos formularios no exigen cuenta (D-3).
- PROPUESTO por el coordinador (confirmar al diseñar):
  - Estados Abierta → Cerrada, como los leads.
  - El administrador ve las solicitudes en su panel.
  - "Tipo de vehículo" como lista: camión / carro / otro.

## D-7 — Regla de asignación: todo servicio lo atiende el administrador · TOMADA (2026-10-26)
- **Decisión: ningún servicio queda asignado a un asesor**, excepto los contactos/leads de **sus propios vehículos publicados mientras su plan esté vigente**. Todo lo demás lo atiende el administrador: créditos, búsquedas, solicitudes generales, "Vende tu camión" y seguros.
- Modifica la línea base v3.0:
  - RN-CL-003 / HU-031: un lead de publicación va al asesor dueño solo con plan vigente; si no, al administrador.
  - RN-CO-007 / RN-CO-008 / HU-041 / HU-042: créditos asignados y gestionados por el administrador.
  - RN-BU-002 / HU-035 a HU-038: búsquedas gestionadas por el administrador.
- **Reasignación manual (confirmada 2026-10-26):** el administrador puede reasignar manualmente cualquier solicitud a un asesor si lo decide. La asignación automática siempre va al administrador. La reasignación debe quedar auditada: quién, cuándo, de quién a quién (RN-SE-003).

## D-4 — Estados incompletos en la línea base · TOMADA (2026-10-26)
- Contexto: las transiciones usan Crédito "Cerrado" y Suscripción "Reactivada", que no están en la tabla de estados; RN-VE-008 menciona solo Activo/Inactivo pero existe Vendido.
- **Decisión: se añaden los tres como estados propios:** Crédito `Cerrado`, Vehículo `Vendido` y Suscripción `Reactivada`.
- Estados resultantes: Vehículo Activo/Inactivo/Vendido; Crédito Registrado/Asignado/Gestionado/Cerrado; Suscripción Activa/Suspendida/Reactivada/Vencida.

## D-5 — Envío de credenciales vs. autorregistro · TOMADA (2026-10-26)
- Contexto: RN-US-004 "Envía credenciales iniciales" por correo, mientras que el registro actual permite que el usuario elija su contraseña. No hay servicio de correo configurado.
- **Decisión: autorregistro con contraseña elegida por el usuario.** No se envían credenciales. Cuando se apruebe una integración de correo, el correo solo notificará la aprobación de la cuenta.

## D-8 — ¿Qué HU oficial se cita para el registro de comprador? · TOMADA (2026-10-26)
- Contexto: la matriz oficial de 87 HU (D-1) no tiene una HU "Registrar usuario/comprador"; solo aparece como HU-043 en la lista de 90 HU descartada. El registro de comprador ya existe en el código (`ESTADO-REAL.md`, secciones 7 y 11).
- Opciones: A) crear HU-N03 "Registro de comprador" (como HU-N01/N02); B) citarlo bajo HU-059 / RN-SE-001 (inicio de sesión).
- Recomendación del coordinador: A, para que tenga sus propios criterios de aceptación.
- **Decisión: A. Se crea HU-N03 "Registro de comprador"**, con sus propios criterios y pruebas.

## D-9 — Datos mínimos de un visitante sin cuenta al contactar · TOMADA (2026-10-26)
- Contexto: D-3 y RN-SE-005 permiten contactar sin cuenta, pero el script SQL exige usuario y correo en `solicitudes` (`002_…sql:347`, `352`). HU-N01 pide solo nombre y WhatsApp.
- Opciones: A) nombre + teléfono obligatorios, correo opcional; B) nombre + teléfono + correo obligatorios.
- Recomendación del coordinador: A, coherente con HU-N01/N02.
- **Decisión: A. Nombre y teléfono obligatorios, correo opcional**, en todo formulario de contacto o solicitud sin cuenta. Las solicitudes no exigen usuario registrado.

## D-10 — Estados definitivos de la cuenta de asesor · TOMADA (2026-10-26)
- Contexto: el script usa AUTORIZADO/DESAUTORIZADO; la v3.0 usa Aprobado/Rechazado/Bloqueado (D-4 no cubrió usuarios).
- Opciones: A) Pendiente / Aprobado / Rechazado / Bloqueado (v3.0 + estado inicial de D-3); B) los del script.
- Recomendación del coordinador: A.
- **Decisión: A. Estados del asesor: Pendiente / Aprobado / Rechazado / Bloqueado.** Los valores AUTORIZADO/DESAUTORIZADO del script se migran a estos sin perder registros.

## D-11 — ¿Se mantiene el estado "Reservado" de publicación? · TOMADA (2026-10-26)
- Contexto: existe en el script SQL, pero no en la v3.0 ni en D-4.
- Opciones: A) eliminarlo (solo Activo / Inactivo / Vendido); B) mantenerlo como estado extra.
- Recomendación del coordinador: A, salvo que el negocio aparte vehículos para un cliente.
- **Decisión: B. Se mantiene "Reservado"**: en el negocio se aparta un camión para un cliente mientras se cierra la venta. Estados del vehículo: Activo / Reservado / Inactivo / Vendido.
- PROPUESTO (confirmar al diseñar P4): Activo → Reservado → Vendido, o Reservado → Activo si la venta no se cierra; lo marcan el asesor dueño o el administrador; en el catálogo público se sigue mostrando con la etiqueta "Reservado".

## D-12 — Cierre de sesión por inactividad y alcance de "Cerrar sesión" · TOMADA (2026-10-26)
- Contexto: RN-SE-002 pide cerrar sesiones inactivas sin fijar el tiempo (detectada en P1).
- **Decisión: la sesión se cierra tras 30 minutos sin actividad, y "Cerrar sesión" la cierra en todos los equipos del usuario** (invalida todos sus tokens mediante `SessionVersion`). Es el comportamiento implementado y probado en P1.

## D-13 — ¿Cómo se crea el primer administrador? · TOMADA (2026-10-26)
- Contexto: el panel de administración (P2) solo funciona con una cuenta de rol ADMINISTRADOR, y hoy no existe ninguna. Es una decisión de seguridad: nunca debe haber contraseñas en el repositorio.
- Opciones:
  - A) **Comando local de promoción**:
    - La persona se registra en la web con su propia contraseña.
    - En el equipo del servidor se ejecuta `dotnet run --project backend/Camion.Api -- promover-admin <correo>`. El comando usa la conexión de `dotnet user-secrets`, añade el rol ADMINISTRADOR, quita COMPRADOR y deja el evento en `auditoria_eventos`.
    - Ventaja: el comando nunca maneja contraseñas.
    - Impacto: requiere acceso al equipo del servidor, lo que es correcto para una acción así.
  - B) **Comando local que crea el administrador**: correo y contraseña se piden por teclado, sin quedar en el historial ni en archivos.
    - Impacto: maneja una contraseña en la consola.
  - C) **Script SQL manual**.
    - Impacto: hay que calcular el hash de Identity a mano; propenso a errores. No se recomienda.
- Recomendación del coordinador: A.
- Bloquea: el uso real del panel de administración contra `camion_ventas` (en pruebas automáticas el administrador se crea en la base en memoria).
- **Decisión: A. "Registrarme y luego promoverme"** (respuesta de la responsable, transmitida por el coordinador principal). Se implementa el comando local `promover-admin <correo>`, que no maneja contraseñas y queda auditado. Lo ejecutará la responsable, guiada, durante la prueba manual; los agentes no lo ejecutan contra `camion_ventas`.

## D-14 — Aplicar la migración 003 (estados del asesor) a `camion_ventas` · TOMADA (2026-10-26)
- Contexto: D-10 fijó los estados Pendiente / Aprobado / Rechazado / Bloqueado, pero la tabla `perfiles_asesor` tiene una restricción que solo admite PENDIENTE / AUTORIZADO / DESAUTORIZADO.
- La migración `database/migraciones/003_p2_estados_asesor.sql`:
  - Cambia esa restricción y convierte los valores antiguos.
  - Tiene vuelta atrás (sección BAJAR).
  - Indica cómo hacer el respaldo previo en `C:\Respaldo_CAMION\`.
- Hasta que se aplique, el registro de asesor sí funciona (estado PENDIENTE). Fallan aprobar, rechazar y bloquear asesores contra la base real.
- Opciones:
  - A) Aplicarla ahora, con el respaldo previo y comprobando los conteos antes y después.
    - Impacto: el panel funciona completo con la base real.
  - B) Esperar a P4 y agruparla con los demás cambios de esquema (placa, estados del vehículo).
    - Impacto: la aprobación de asesores no se puede usar con la base real hasta entonces.
- Recomendación del coordinador: A. La tabla `perfiles_asesor` está vacía o casi vacía, porque hasta hoy no existía el registro de asesor.
- Bloquea: el uso real de la aprobación de asesores.
- **Decisión: A. "Ahora, con respaldo previo"** (respuesta de la responsable, transmitida por el coordinador principal). Condiciones:
  1. Antes de nada, un respaldo completo de la base en un archivo **nuevo**, con fecha y hora, dentro de `C:\Respaldo_CAMION\`. Nunca se sobrescriben los existentes. Se comprueba que el archivo existe y no está vacío.
  2. Se cuentan las filas de `AspNetUsers`, `perfiles_asesor`, `perfiles_comprador` y `AspNetUserRoles` antes y después; los conteos deben coincidir.
  3. Se aplica solo la sección SUBIR. Si algo falla, se ejecuta BAJAR y se avisa.
  4. Se aplica solo cuando el código de P2 compile y pase sus pruebas.
  5. En la bitácora quedan la ruta del respaldo, los conteos y el resultado.

## D-15 — ¿Qué pasa con un asesor rechazado y con un comprador que quiere ser asesor? · TOMADA (2026-10-26)
- Contexto: con el contrato de P2, un asesor rechazado no tiene salida. No puede volver a pedir el alta, porque su correo y su documento ya existen (409). Un comprador tampoco puede pasar a asesor con el mismo correo. La v3.0 no dice nada sobre ninguno de los dos casos (C-P2-2 y C-P2-5 de `docs/criterios/P2.md`).
- Opciones:
  - A) El administrador puede **reconsiderar**: aprobar a un asesor rechazado (paso de Rechazado a Aprobado, con motivo y auditoría). Un comprador que quiere ser asesor usa por ahora otro correo; más adelante se añade "Solicitar ser asesor" desde su cuenta.
    - Impacto: es un cambio pequeño.
  - B) Un rechazo es definitivo. Para volver a intentarlo hay que hablar con el administrador, sin cambios en el sistema.
    - Impacto: no hay trabajo nuevo, pero no hay salida dentro del sistema.
- Recomendación del coordinador: A.
- **Decisión: A. "El admin puede reconsiderar"** (respuesta de la responsable, transmitida por el coordinador principal). El administrador puede pasar a un asesor de Rechazado a Aprobado, con motivo obligatorio y auditoría; se incluye en P2. Un comprador que quiera ser asesor usa, por ahora, otro correo. "Solicitar ser asesor" desde la cuenta del comprador queda como ítem futuro del backlog.
- Nota de trazabilidad (C-P2-1): la matriz asocia HU-047 "Bloquear usuario" a RN-US-005, pero en la v3.0 esa regla es "Notificación de acceso". Se mantiene HU-047 como referencia del bloqueo.

## D-16 — ¿Quién aplica la migración 003 en `camion_ventas`? · TOMADA (2026-10-26)
- Contexto: el sistema de permisos impidió que el agente escribiera en `camion_ventas` (detectada en el cierre de P2).
- **Decisión: la aplica la responsable, guiada, desde phpMyAdmin.** Los agentes no reciben permiso de escritura en la base real.
- Nota práctica: la ventana de confirmación de phpMyAdmin no funciona en el navegador integrado de Claude; los cambios en la base se ejecutan desde Chrome/Edge.
