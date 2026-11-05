# Criterios de aceptación P5: leads y contactos

Ítem del backlog: **P5 "Leads y contactos"**. Criterio de salida: captura anónima; asignación al asesor dueño solo con plan vigente, si no al administrador (D-7); seguimiento trazable.
Autor: analista funcional. Fecha: 2026-11-04. Estado del documento: PROPUESTO (pendiente de revisión del coordinador).

Fuentes: `docs/requisitos/requisitos-funcionales-v3.0.md` (matriz oficial de 87 HU, D-1), `docs/coordinacion/DECISIONES.md` (D-3, D-6, D-7, D-9, D-31, D-34, D-41, D-42, D-44, D-45), `docs/coordinacion/BACKLOG.md` (fila P5), `CLAUDE.md`, `docs/legal/POLITICA-TRATAMIENTO-DATOS.md` (borrador).

Marcas:
- **CONFIRMADO (fuente)**: sale literalmente de una HU, RN o decisión.
- **PROPUESTO**: lo infiere el analista; necesita visto bueno del coordinador o de la responsable.
- **D-46 TOMADA (B, 2026-11-04)**: CA-P5-50 a CA-P5-53 ya se pueden construir. Ver también la adenda de D-47 (CA-P5-61 a CA-P5-64) y D-48.

Cómo se prueba: API = prueba de integración; COMP = prueba de componente del frontend; MAN = prueba manual de la responsable.
Las rutas y los nombres de campos son orientativos; el contrato definitivo lo fija el arquitecto en `docs/criterios/P5-contrato-api.md`.

---

## 1. HU y RN implicadas

| Código | Nombre (matriz oficial, D-1) | Actor en v3.0 | Regla | Cambios por decisiones |
|---|---|---|---|---|
| HU-029 | Contactar Asesor | Cliente | RN-CL-001 "Permite contacto desde portal" | Sin cuenta (D-3, RN-SE-005); nombre y teléfono obligatorios, correo opcional (D-9) |
| HU-030 | Generar Lead | Sistema | RN-CL-002 "Genera lead automáticamente" | — |
| HU-031 | Asignar Lead | Sistema/Admin | RN-CL-003 "Asigna lead al responsable. Automática" | Al asesor dueño solo con plan vigente; si no, al administrador; reasignación manual auditada (D-7) |
| HU-032 | Gestionar Lead | Asesor | RN-CL-004 "Seguimiento y cierre" | El administrador también gestiona los que tiene asignados (D-7) |
| HU-033 | Cerrar Lead | Asesor | RN-CL-004 | Ídem |
| HU-071 | Notificación Nuevo Lead (en la lista de la sección 10 figura como HU-072; vale la matriz, D-1) | Sistema | RN-NO-002 "Notificar nuevo lead. Tiempo real" | Sin servicio de correo (D-5). Solo aviso dentro del panel (PROPUESTO, sección 11) |
| HU-061 | Registrar Auditoría | Sistema | RN-SE-003 "Trazabilidad" | Reasignación con quién, cuándo, de quién a quién (D-7) |
| HU-062 | Proteger Datos Personales | Sistema | RN-SE-004 "Cumplir protección de datos" | Casilla de autorización y prueba (D-44) |
| — | Control de accesos | — | RN-SE-006 "Validar permisos por rol" | Comprador de solo lectura (D-3) |
| — | Consulta pública | Visitante | RN-SE-005 "Visitantes pueden consultar publicaciones. Sin rol comprador" | Contactar sin cuenta (D-3) |
| HU-077 | Reporte de Leads | Administrador | RN-RE-003 | Fuera de P5 (P8). P5 guarda los datos que el reporte necesitará |

Estados de la v3.0 (secciones 12 y 13): **Lead Abierto ("Pendiente de gestión") → Cerrado ("Lead finalizado")**.
Validación general de la v3.0 (sección 11): "Validación de formatos de correo electrónico y teléfono".

Fuera de P5: avisos "Vende tu camión" y "Seguro" (P5b, HU-N01/HU-N02), créditos y búsquedas (P6), reporte de leads (P8). Nada de autorizaciones para bancos o aseguradoras ni de consignación (D-45).

---

## 2. Qué existe hoy y qué falta

Base: `ESTADO-REAL.md` (auditoría P0 del 2026-10-26, anterior a P1–P4b) más una búsqueda propia en el código de hoy (`feature/diseno-portal`). **Hay que pedir al auditor que actualice `ESTADO-REAL.md` antes de construir P5.**

| Tema | Hoy | Falta |
|---|---|---|
| Endpoints de leads | No existen: ningún archivo del backend menciona leads ni solicitudes, salvo los de autenticación | Todo: crear, listar, detalle, notas, cerrar, reabrir, reasignar |
| Esquema | Las tablas `solicitudes`, `asignaciones_solicitud` y `seguimiento_solicitud` existen solo en el script (`002_…sql:345-405`), sin mapear. `solicitudes.comprador_usuario_id` y `correo_contacto` son NOT NULL, lo que **contradice D-9**; los estados NUEVA/EN_GESTION/CERRADA/CANCELADA no son los de la v3.0 | Migración reversible con respaldo previo (aplicada por la responsable, D-16): usuario y correo opcionales, estados ABIERTO/CERRADO, resultado de cierre, tabla de prueba de autorización (D-44) |
| Ficha del vehículo | `DetalleVehiculo.tsx` no muestra datos del asesor ("el contacto llega en P5") | Formulario de contacto |
| Teléfono | El registro valida un formato genérico (7 a 25 caracteres, `ValidacionAuth.cs:98-102`) | Validación de teléfono colombiano (CA-P5-04) |
| Limitación de peticiones | No existe (`ESTADO-REAL.md`, hallazgo 3) | CA-P5-20 a CA-P5-22 |
| Autorización de datos | No existe casilla ni registro de prueba en ningún formulario | CA-P5-13 a CA-P5-19 |
| Auditoría | `auditoria_eventos` ya se usa en P2–P4b | Eventos de leads |
| Contacto general del pie | WhatsApp 316 615 2907 y correo confirmados (D-42) | Se reutiliza en el mensaje de error (CA-P5-56) |

---

## 3. Captura del contacto (HU-029, HU-030)

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5-01 | Un visitante sin cuenta en la ficha de un vehículo visible en el portal | Escribe nombre y teléfono válidos, marca la autorización y pulsa "Enviar" | 201; se crea un lead en estado ABIERTO ligado a esa publicación; el visitante ve el mensaje de éxito (CA-P5-54) | CONFIRMADO (HU-029, HU-030, RN-CL-001, RN-CL-002, D-3, D-9) | API + COMP |
| CA-P5-02 | Un comprador con sesión iniciada | Abre el formulario | Nombre, teléfono y correo vienen rellenados con su perfil y se pueden editar; el lead queda ligado a su cuenta además de guardar los datos escritos | Contactar con cuenta: CONFIRMADO (D-3). Relleno y vínculo: PROPUESTO | API + COMP |
| CA-P5-03 | El formulario | El nombre está vacío, tiene menos de 2 o más de 150 caracteres (tras quitar espacios) | 400 con error en `nombre`: "Escribe tu nombre (entre 2 y 150 caracteres)."; no se crea nada | Obligatorio: CONFIRMADO (D-9). Límites (los mismos del registro): PROPUESTO | API + COMP |
| CA-P5-04 | El formulario | El teléfono está vacío o no es un número colombiano de 10 dígitos: celular que empieza por 3, o fijo que empieza por 60 | 400 con error en `telefono`: "Escribe un celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567."; no se aceptan números de 7 dígitos ni extranjeros | Obligatorio y "formato válido": CONFIRMADO (D-9, v3.0 §11). Regla colombiana de 10 dígitos: PROPUESTO | API + COMP |
| CA-P5-05 | El formulario | El teléfono se escribe como `300 123 4567`, `300-123-4567`, `(300) 1234567`, `+57 3001234567` o `573001234567` | Se acepta y se guarda normalizado como `+573001234567`; los duplicados (CA-P5-23) se comparan con el número normalizado | PROPUESTO | API |
| CA-P5-06 | El formulario | El correo se deja vacío | Se acepta. Si se escribe y no tiene formato válido o supera 254 caracteres, 400 en `correo`: "Escribe un correo válido o deja el campo vacío." | Opcional: CONFIRMADO (D-9). Formato: CONFIRMADO (v3.0 §11). Límite de 254: PROPUESTO (igual que el registro) | API + COMP |
| CA-P5-07 | El formulario | El mensaje se deja vacío o tiene hasta 500 caracteres | Se acepta; si está vacío se guarda "Me interesa este vehículo". Con más de 500, 400 en `mensaje`: "El mensaje no puede pasar de 500 caracteres."; el formulario muestra un contador | PROPUESTO (D-9 no menciona mensaje; 500 por coherencia con D-6) | API + COMP |
| CA-P5-08 | Una petición manipulada | Envía un asesor, un responsable o un estado | La API los ignora: el responsable lo calcula siempre el servidor (CA-P5-25 a CA-P5-28) y el estado inicial es siempre ABIERTO. Sin identificador de publicación, 400 | PROPUESTO (consecuencia de RN-CL-003 "automática") | API |
| CA-P5-09 | Una publicación OCULTA, VENDIDA o ELIMINADA, un vehículo INACTIVO o VENDIDO, o un identificador que no existe | Alguien envía el formulario (por ejemplo, desde una página abierta hace horas) | 404: "Este vehículo ya no está disponible. Mira otros vehículos en el catálogo."; no se crea lead ni registro de autorización | PROPUESTO | API + COMP |
| CA-P5-10 | Un vehículo RESERVADO, que el portal muestra con su etiqueta (D-11, D-17) | Un visitante lo contacta | Se crea el lead normalmente | PROPUESTO | API |
| CA-P5-11 | Un asesor con sesión iniciada en la ficha de su propio vehículo | Mira la ficha o llama a la API | El formulario no aparece; la API responde 409: "Este vehículo es tuyo; no puedes contactarte a ti mismo." | PROPUESTO | API + COMP |
| CA-P5-12 | El formulario en pantalla | Se abre y se envía | Empieza con "* Campo obligatorio"; nombre, teléfono y autorización llevan asterisco y `aria-required="true"`, el correo y el mensaje no. El botón se desactiva mientras se envía (evita el doble envío). Si hay un error, se conservan los datos escritos y se marca el campo con el error | Asterisco: CONFIRMADO (D-31). Lo demás: PROPUESTO | COMP |

---

## 4. Autorización de tratamiento de datos (reutilizable en P5b y el registro)

Se diseña como un componente y una regla de API comunes. P5b, el registro de comprador y la inscripción de asesor deben usar los mismos criterios, cambiando solo el valor de `formulario`.

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5-13 | Cualquier formulario que recoge datos personales | Se abre | Muestra una casilla **sin marcar**: "Autorizo el tratamiento de mis datos personales según la [Política de tratamiento de datos]." El enlace abre la política en una pestaña nueva sin perder lo escrito | Casilla sin marcar con enlace: CONFIRMADO (D-44, regla indicada por el coordinador; ver nota 1). Texto exacto y pestaña nueva: PROPUESTO | COMP |
| CA-P5-14 | La casilla sin marcar | Se pulsa "Enviar" o se llama a la API con `autorizacionDatos` ausente o `false` | El formulario muestra junto a la casilla "Debes autorizar el tratamiento de tus datos para enviar el formulario." La API responde 400 con ese error en `autorizacionDatos` y no guarda nada. La regla está en la API, no solo en el navegador | Bloqueo: CONFIRMADO (D-44). Mensaje y código: PROPUESTO | API + COMP |
| CA-P5-15 | Un envío válido | Se guarda el lead | Se guarda en la misma transacción una prueba de la autorización con: fecha y hora (UTC), **versión de la política**, **formulario** (`CONTACTO_VEHICULO`) y referencia al lead. La versión la pone el servidor desde su configuración; la que mande el cliente se ignora. Si falla una de las dos escrituras, no queda ninguna | Fecha, versión y formulario: CONFIRMADO (D-44). Transacción, UTC y versión del servidor: PROPUESTO | API |
| CA-P5-16 | Una prueba de autorización guardada | Cualquier usuario intenta cambiarla o borrarla | No existe ningún endpoint para hacerlo; solo el administrador la ve en el detalle del lead (fecha, versión, formulario). El asesor no la ve | PROPUESTO | API |
| CA-P5-17 | La política sigue en borrador (D-44: no se publica hasta la revisión legal) | Se pulsa el enlace | En desarrollo y pruebas abre `#/privacidad` con el borrador y una franja: "BORRADOR en revisión legal. No es la versión vigente." La prueba guarda la versión `BORRADOR-2026-11-04`. **P5 no sale a internet** hasta que exista una versión aprobada configurada; al publicarla solo cambia la configuración de la versión, sin tocar el código | PROPUESTO (D-44 bloquea la salida a internet, pero no dice qué muestra el enlace mientras tanto) | COMP + MAN |
| CA-P5-18 | Un comprador con sesión que ya aceptó la política al registrarse | Envía un contacto | También debe marcar la casilla; se guarda una prueba nueva por cada envío | CONFIRMADO (D-44: "todo formulario de contacto o aviso") | API + COMP |
| CA-P5-19 | P5b ("Vende tu camión", "Seguro") | Se construya | Reutiliza el mismo componente y la misma regla, con `formulario` = `VENDE_TU_CAMION` o `SEGURO_TODO_RIESGO`. La lista de valores admite además `REGISTRO_COMPRADOR` e `INSCRIPCION_ASESOR` | PROPUESTO | API (en P5b) |

Nota 1: en `DECISIONES.md` D-44 aparece todavía como PROPUESTO. El coordinador la transmite como regla a respetar; se marca CONFIRMADO con esa salvedad.

---

## 5. Anti-abuso y duplicados

Sin CAPTCHA, porque exige un servicio externo que no está aprobado (PROPUESTO).

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5-20 | Una misma IP | Envía más de 5 contactos en 10 minutos o más de 20 en un día | 429: "Has enviado muchas solicitudes. Intenta de nuevo en unos minutos."; los límites son configurables | PROPUESTO | API |
| CA-P5-21 | Un mismo teléfono normalizado | Genera más de 10 leads en un día, en cualquier vehículo | 429 con el mismo mensaje | PROPUESTO | API |
| CA-P5-22 | Un campo trampa oculto, invisible para las personas | Llega relleno (indica un robot) | La API responde 201 con el mensaje de éxito normal, pero no guarda nada. Las peticiones de más de 8 KB se rechazan con 413 | PROPUESTO | API |
| CA-P5-23 | Ya existe un lead ABIERTO con el mismo teléfono normalizado y la misma publicación | Llega otro contacto | No se crea un lead nuevo. Se añade al historial del existente una entrada "Contacto repetido" con el mensaje nuevo y la fecha, y se guarda su prueba de autorización. El visitante recibe la misma respuesta 201 y el mismo mensaje que en un contacto nuevo (no se revela que ya existía) | PROPUESTO | API |
| CA-P5-24 | El lead anterior de ese teléfono y esa publicación está CERRADO | Llega otro contacto | Se crea un lead nuevo, asignado según CA-P5-25 a CA-P5-27 | PROPUESTO | API |

---

## 6. Asignación (HU-031, RN-CL-003 modificada por D-7)

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5-25 | Una publicación PUBLICADA cuyo asesor dueño está Aprobado y tiene un plan vigente | Se crea el lead | Queda asignado al asesor dueño | CONFIRMADO (D-7, RN-CL-003, D-41) | API |
| CA-P5-26 | Una publicación en estado PUBLICADA cuyo asesor ya no tiene plan vigente (vencida según D-41; por ejemplo, la página se abrió antes del vencimiento) | Se crea el lead | Queda asignado al administrador. El visitante ve el mismo mensaje que en CA-P5-25 | Al administrador: CONFIRMADO (D-7: "si no, al administrador"). Aceptar el contacto en lugar de dar 404: PROPUESTO | API |
| CA-P5-27 | Una publicación PUBLICADA cuyo asesor está Bloqueado o Rechazado | Se crea el lead | Queda asignado al administrador | PROPUESTO (por analogía con D-7) | API |
| CA-P5-28 | Cualquier creación | Se asigna | El historial registra "Creado" y "Asignado a [asesor / Administración] (automático)", con el actor SISTEMA; en `auditoria_eventos` queda `Lead / CREAR`. El historial no guarda el teléfono ni el correo, solo la referencia al lead | Trazabilidad: CONFIRMADO (RN-SE-003). Formato de los eventos: PROPUESTO | API |

"Administración" significa que el lead queda asignado al rol administrador, no a una persona: lo ve y lo atiende cualquier administrador (PROPUESTO).

---

## 7. Visibilidad y permisos

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5-29 | Un asesor Aprobado | Abre "Mis contactos" | Ve solo los leads que tiene asignados ahora, del más reciente al más antiguo, con nombre, teléfono, correo, mensaje, vehículo, fecha y estado. Puede filtrar por estado | Solo los suyos: CONFIRMADO (D-7, RN-SE-006, v3.0 §15). Columnas y filtro: PROPUESTO | API + COMP |
| CA-P5-30 | Un asesor | Pide por identificador un lead asignado a otro asesor o a Administración | 404 (no se revela que existe), igual que en el inventario de P4 | PROPUESTO | API |
| CA-P5-31 | Un administrador | Abre "Contactos" en el panel | Ve todos los leads, con filtros por estado, responsable, vehículo y rango de fechas | Ve todos: CONFIRMADO (D-7). Filtros: PROPUESTO | API + COMP |
| CA-P5-32 | Un comprador con sesión | Llama a cualquier endpoint de lectura o gestión de leads | 403. No hay "Mis contactos" para el comprador en P5 | 403: CONFIRMADO (D-3, RN-SE-006). Sin lista propia: PROPUESTO | API |
| CA-P5-33 | Una petición sin sesión | Llama a cualquier endpoint de lectura o gestión de leads | 401. Solo el endpoint de crear contacto es anónimo | CONFIRMADO (D-3, RN-SE-006) | API |
| CA-P5-34 | Un asesor Pendiente, Rechazado o Bloqueado | Intenta ver sus leads | Sin acceso, igual que al resto del panel del asesor (P2) | CONFIRMADO (D-3, D-10) | API |
| CA-P5-35 | El portal público (catálogo y ficha) | Se consulta | Nunca muestra leads, cuántos contactos tiene un vehículo ni los datos de quien contactó | PROPUESTO | API |

---

## 8. Seguimiento trazable (HU-032, HU-033, RN-CL-004)

Mínimo propuesto: se mantienen los dos estados de la v3.0 y la trazabilidad se logra con el historial de eventos y un resultado de cierre.

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5-36 | Cualquier lead | Se consulta | Su estado es ABIERTO ("Pendiente de gestión") o CERRADO ("Finalizado"); la pantalla muestra el texto, no el código | Estados: CONFIRMADO (v3.0 §12-13). Textos: PROPUESTO | API + COMP |
| CA-P5-37 | Un lead ABIERTO y su responsable (el asesor asignado o un administrador) | Añade una nota de seguimiento | 201; la nota queda en el historial con autor, fecha y texto | Seguimiento: CONFIRMADO (RN-CL-004). Notas: PROPUESTO | API + COMP |
| CA-P5-38 | Una nota | Tiene menos de 5 o más de 500 caracteres, o el lead está CERRADO | 400 "La nota debe tener entre 5 y 500 caracteres." o 409 "Este contacto está cerrado." | PROPUESTO (límites como la nota de reserva, D-28) | API |
| CA-P5-39 | Un lead ABIERTO y su responsable | Lo cierra eligiendo un resultado: Se concretó la venta, No le interesó, No respondió, El vehículo ya no está disponible, Contacto duplicado o inválido, Otro | Pasa a CERRADO con el resultado, la fecha y quién lo cerró; queda en el historial y en `auditoria_eventos`. "Otro" exige una observación de 5 a 500 caracteres | Cerrar: CONFIRMADO (HU-033, RN-CL-004). Lista de resultados: PROPUESTO (sirve para el reporte HU-077) | API + COMP |
| CA-P5-40 | Un lead CERRADO | Alguien intenta cerrarlo de nuevo | 409 "Este contacto ya está cerrado." | PROPUESTO | API |
| CA-P5-41 | Un lead CERRADO | Un administrador lo reabre con un motivo de 5 a 500 caracteres | Vuelve a ABIERTO con el mismo responsable; queda en el historial y en la auditoría. Un asesor recibe 403 | PROPUESTO (la v3.0 solo define Abierto → Cerrado) | API |
| CA-P5-42 | El detalle de un lead | Se abre | Muestra el historial completo en orden cronológico: creado, asignado, contacto repetido, notas, reasignaciones, cierre y reapertura, cada uno con fecha y actor (usuario o SISTEMA). No se puede editar ni borrar ninguna entrada | Trazabilidad: CONFIRMADO (RN-SE-003, criterio de salida de P5). Detalle: PROPUESTO | API + COMP |
| CA-P5-43 | Un responsable | Intenta cambiar el nombre, el teléfono o el correo que dejó el visitante | No se permite: son datos del titular. Una corrección se anota como nota | PROPUESTO | API |

---

## 9. Reasignación manual (D-7)

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5-44 | Un lead ABIERTO y un administrador | Lo reasigna a un asesor Aprobado o a Administración, con un motivo de 5 a 500 caracteres | 200; cambia el responsable; queda en `asignaciones_solicitud` y en `auditoria_eventos` (`Lead / REASIGNAR`) quién, cuándo, de quién a quién y el motivo; aparece en el historial | Reasignar y auditar quién, cuándo, de quién a quién: CONFIRMADO (D-7, RN-SE-003). Motivo obligatorio: PROPUESTO (como D-34) | API + COMP |
| CA-P5-45 | Un asesor o un comprador / una petición sin sesión | Intenta reasignar | 403 / 401 | CONFIRMADO (D-7: lo hace el administrador) | API |
| CA-P5-46 | La reasignación | El destino no existe o no está Aprobado, es el responsable actual o el lead está CERRADO | 400 "Elige un asesor aprobado.", 409 "El contacto ya está asignado a ese responsable." o 409 "Este contacto está cerrado." | PROPUESTO | API |
| CA-P5-47 | Un asesor destino sin plan vigente | El administrador le reasigna un lead | Se permite: la reasignación manual queda a criterio del administrador | PROPUESTO (D-7: "si lo decide") | API |
| CA-P5-48 | Una reasignación hecha | El asesor anterior y el nuevo consultan | El anterior deja de verlo (404); el nuevo lo ve con todo su historial | PROPUESTO | API |

---

## 10. Cambios después de crear el lead

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5-49 | Un lead ABIERTO | Su vehículo se vende, se inactiva o la publicación se elimina | El lead sigue con su responsable y ABIERTO; el detalle muestra el estado actual del vehículo ("Vendido", "Inactivo"…) y el responsable lo cierra cuando corresponda | PROPUESTO | API |
| CA-P5-50 | Un lead ABIERTO asignado a un asesor | Vence el plan del asesor | El lead pasa a Administración. Queda en el historial y la auditoría con actor SISTEMA y motivo "Plan vencido". El paso ocurre como tarde en la siguiente revisión automática o consulta de leads | D-46 (B) | API |
| CA-P5-51 | Un lead ABIERTO asignado a un asesor | El administrador bloquea al asesor | Sus leads abiertos pasan a Administración, con actor SISTEMA y motivo "Asesor bloqueado" en el historial y la auditoría | D-46 (B) | API |
| CA-P5-52 | Un lead ABIERTO | El administrador reasigna el vehículo a otro asesor (D-34) | Los leads abiertos siguen al vehículo, con su propia entrada de reasignación auditada; los cerrados no cambian | D-46 (B) | API |
| CA-P5-53 | Un lead que pasó a Administración por CA-P5-50 o CA-P5-51 | El asesor renueva su plan o lo desbloquean | El lead no vuelve solo; el administrador decide si lo reasigna (D-7) | D-46 (B) | API |

---

## 11. Mensajes al usuario, aviso de leads nuevos y protección de datos

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5-54 | Un envío aceptado (lead nuevo, contacto repetido o campo trampa) | Termina | Se muestra el mismo mensaje: "¡Listo! Recibimos tus datos. Te contactaremos pronto por teléfono o WhatsApp." No dice quién atenderá ni si el contacto ya existía. El formulario se vacía | PROPUESTO | COMP |
| CA-P5-55 | Un error de validación (400) | Se muestra | El error aparece junto a cada campo, con los textos de CA-P5-03 a CA-P5-07 y CA-P5-14, y el foco va al primer campo con error | PROPUESTO | COMP |
| CA-P5-56 | Un 404, 429 o error del servidor (500 o sin conexión) | Se muestra | El 404 y el 429 usan sus textos (CA-P5-09, CA-P5-20). El 500 o la falta de conexión: "No pudimos enviar tus datos. Intenta de nuevo en unos minutos o escríbenos por WhatsApp al 316 615 2907." Nunca se muestran trazas ni detalles técnicos | Número de WhatsApp: CONFIRMADO (D-42). Textos: PROPUESTO | COMP |
| CA-P5-57 | El panel del asesor o del administrador | Pide una acción no permitida (409, 403) | Se muestra el mensaje de la API en lenguaje sencillo, sin códigos | PROPUESTO | COMP |
| CA-P5-58 | Un asesor o un administrador con leads ABIERTOS que aún no ha abierto | Entra al panel | El menú muestra "Contactos (N nuevos)". Un lead deja de ser nuevo cuando su responsable abre el detalle. No hay correo, WhatsApp ni aviso en tiempo real | HU-071 / RN-NO-002 piden notificar en tiempo real; no hay servicio de correo (D-5). Aviso solo en el panel: CONFIRMADO (D-48) | API + COMP |
| CA-P5-59 | Cualquier operación de leads | Se escribe el registro técnico de la aplicación | No incluye nombre, teléfono, correo ni mensaje del visitante; solo identificadores | CONFIRMADO (RN-SE-004) en lo general; detalle PROPUESTO | API (revisión) |
| CA-P5-60 | Cualquier usuario | Intenta borrar un lead | No existe un endpoint de borrado; la supresión que pida un titular se atiende por el procedimiento de la política, fuera de P5 | PROPUESTO | API |

---

## 12. Trazabilidad HU/RN/D → criterio → prueba prevista

| Origen | Criterios | Prueba prevista |
|---|---|---|
| HU-029 / RN-CL-001 (contactar) | CA-P5-01, 02, 09, 10, 11, 12 | API: crear contacto anónimo y con comprador; publicación no visible; reservado; dueño. COMP: formulario en la ficha |
| HU-030 / RN-CL-002 (generar) | CA-P5-01, 08, 23, 24, 28 | API: lead ABIERTO, campos manipulados, duplicado abierto y cerrado |
| D-9 (nombre y teléfono obligatorios, correo opcional) | CA-P5-03 a 07 | API + COMP: validaciones por campo y límites |
| v3.0 §11 (formato de correo y teléfono) | CA-P5-04, 05, 06 | API: teléfonos válidos e inválidos, normalización |
| HU-031 / RN-CL-003 + D-7 (asignación) | CA-P5-25 a 28 | API: plan vigente, plan vencido, asesor bloqueado, historial |
| D-41 (vigencia = plan) | CA-P5-25, 26 | API: reloj controlado antes y después del vencimiento |
| D-7 (reasignación auditada) | CA-P5-44 a 48 | API: éxito, roles, validaciones, visibilidad después. COMP: diálogo de reasignación |
| HU-032 / HU-033 / RN-CL-004 (gestionar y cerrar) | CA-P5-36 a 43 | API: notas, cierre, doble cierre, reapertura, historial. COMP: detalle del lead |
| RN-SE-006 / D-3 (permisos, comprador de solo lectura) | CA-P5-29 a 35, 45 | API: matriz de roles (anónimo 401, comprador 403, asesor solo lo suyo y 404 en lo ajeno, administrador todo) |
| RN-SE-003 / HU-061 (auditoría) | CA-P5-28, 39, 41, 42, 44, 51 | API: eventos en `auditoria_eventos` y `asignaciones_solicitud` |
| D-44 / RN-SE-004 / HU-062 (autorización de datos) | CA-P5-13 a 19, 59 | API: rechazo sin casilla, prueba guardada en la misma transacción, versión del servidor. COMP: casilla sin marcar y enlace. MAN: franja de borrador |
| D-31 (asterisco) | CA-P5-12 | COMP |
| D-42 (WhatsApp del pie) | CA-P5-56 | COMP |
| HU-071 / RN-NO-002 (notificación) | CA-P5-58 | API + COMP: contador de nuevos |
| D-34 y cambios posteriores | CA-P5-49 a 53 | API (CA-P5-50 a 53 después de D-46) |
| Anti-abuso (sin fuente; PROPUESTO) | CA-P5-20 a 22 | API: límites por IP y teléfono, campo trampa, tamaño máximo |
| Prueba manual | — | MAN: con la migración aplicada, la responsable envía un contacto anónimo y uno con cuenta, lo ve como asesor, lo anota, lo cierra, y como administradora lo reasigna y lo reabre |

---

## 13. Contradicciones y preguntas

- **D-46 (nueva, PENDIENTE):** qué pasa con los leads abiertos cuando el asesor pierde la condición que justificó la asignación (plan vencido, bloqueo, vehículo reasignado). Registrada en `DECISIONES.md`.
- Por D-41, una publicación solo es visible si el plan del asesor está vigente. Por eso el caso "si no, al administrador" de D-7 casi nunca ocurre al crear el lead (solo en CA-P5-26 y CA-P5-27); su efecto real se da después de crearlo, y eso es D-46.
- Ya resueltas, sin decisión nueva: numeración de la notificación de lead nuevo (HU-071 en la matriz, HU-072 en la lista; vale la matriz, D-1); la asignación automática de RN-CL-003 cambia por D-7.
- Hallazgo técnico, no de negocio: el esquema `solicitudes` exige usuario y correo y usa otros estados. Se corrige con la migración de P5 (D-9 ya está decidida).

## Adenda (2026-11-04) — Botón de WhatsApp en el detalle (D-47)

| ID | Dado | Cuando | Entonces | Origen | Nivel |
|---|---|---|---|---|---|
| CA-P5-61 | Una publicación visible cuyo asesor dueño tiene un teléfono válido | Se abre el detalle (con o sin cuenta) | Junto al formulario aparece "Escribir por WhatsApp", que abre `https://wa.me/57<teléfono>` en otra pestaña (`rel="noopener noreferrer"`), con un texto inicial que nombra el vehículo (PROPUESTO). El DTO público solo envía el enlace o el número del asesor, nunca su correo ni su documento | D-47 | API + COMP |
| CA-P5-62 | Una publicación a cargo del administrador, o un asesor sin teléfono válido | Se abre el detalle | El botón usa el WhatsApp del portal (316 615 2907, D-42) | D-47 / D-42 | API + COMP |
| CA-P5-63 | Una publicación no visible: plan vencido, vendida o inactiva | Se pide el detalle | 404, como hoy: no se expone ningún teléfono | D-41 / D-47 | API |
| CA-P5-64 | Se pulsa el botón de WhatsApp | — | No se crea un lead ni se pide consentimiento: la conversación ocurre fuera del portal | D-47 (consecuencia aceptada) | COMP |

