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

## D-8 — ¿Qué HU oficial se cita para el registro de comprador? · PENDIENTE (detectada en P0, 2026-10-26)
- Contexto: la matriz oficial de 87 HU (D-1) no tiene una HU "Registrar usuario/comprador"; solo aparece como HU-043 en la lista de 90 HU descartada. El registro de comprador ya existe en el código (`ESTADO-REAL.md`, secciones 7 y 11).
- Opciones: A) crear HU-N03 "Registro de comprador" (como HU-N01/N02); B) citarlo bajo HU-059 / RN-SE-001 (inicio de sesión).
- Recomendación del coordinador: A, para que tenga sus propios criterios de aceptación.
- Bloquea: la trazabilidad de P1 (no su ejecución).

## D-9 — Datos mínimos de un visitante sin cuenta al contactar · PENDIENTE (detectada en P0, 2026-10-26)
- Contexto: D-3 y RN-SE-005 permiten contactar sin cuenta, pero el script SQL exige usuario y correo en `solicitudes` (`002_…sql:347`, `352`). HU-N01 pide solo nombre y WhatsApp.
- Opciones: A) nombre + teléfono obligatorios, correo opcional; B) nombre + teléfono + correo obligatorios.
- Recomendación del coordinador: A, coherente con HU-N01/N02.
- Bloquea: P5, P5b, P6 (diseño del esquema de solicitudes).

## D-10 — Estados definitivos de la cuenta de asesor · PENDIENTE (detectada en P0, 2026-10-26)
- Contexto: el script usa AUTORIZADO/DESAUTORIZADO; la v3.0 usa Aprobado/Rechazado/Bloqueado (D-4 no cubrió usuarios).
- Opciones: A) Pendiente / Aprobado / Rechazado / Bloqueado (v3.0 + estado inicial de D-3); B) los del script.
- Recomendación del coordinador: A.
- Bloquea: P2.

## D-11 — ¿Se mantiene el estado "Reservado" de publicación? · PENDIENTE (detectada en P0, 2026-10-26)
- Contexto: existe en el script SQL, pero no en la v3.0 ni en D-4.
- Opciones: A) eliminarlo (solo Activo / Inactivo / Vendido); B) mantenerlo como estado extra.
- Recomendación del coordinador: A, salvo que el negocio aparte vehículos para un cliente.
- Bloquea: P4 (estados de publicación).
