# Criterios de aceptación P5b: avisos "Vende tu camión" y "Compra tu seguro todo riesgo"

Ítem del backlog: **P5b** (HU-N01, HU-N02). Rama: `feature/p5b-avisos` (apilada sobre `feature/p5-leads`).
Criterio de salida (BACKLOG.md): formularios públicos sin cuenta, validaciones (500 caracteres, teléfono) y solicitudes visibles solo para el administrador (D-6).
Autor: analista funcional. Fecha: 2026-11-05. Estado del documento: PROPUESTO (pendiente de revisión del coordinador).
Actualización 2026-11-06 (documentador): D-50 a D-53 quedaron TOMADAS por la responsable el 2026-11-06 según la recomendación (`DECISIONES.md`). Los criterios que decían **PENDIENTE D-n** pasan a **CONFIRMADO (D-n, opción elegida)**. Estado de las pruebas: ver `docs/criterios/P5b-evidencia.md`.

Fuentes: `docs/coordinacion/DECISIONES.md` (D-3, D-6, D-7, D-9, D-31, D-42, D-44, D-45, D-46 a D-49), `docs/criterios/criterios-P5-leads.md`, `docs/criterios/P5-contrato-api.md`, `docs/coordinacion/ESTADO-REAL.md` ("Actualización P5"), `CLAUDE.md` y el código en HEAD `b057c1d` (`feature/p5-leads`).
HU-N01 y HU-N02 no están en la matriz de 87 HU (D-1); se definen en D-6.

Marcas:
- **CONFIRMADO (fuente)**: sale literalmente de una HU, RN o decisión TOMADA.
- **PROPUESTO**: lo infiere el analista; necesita visto bueno del coordinador o de la responsable.
- **PENDIENTE D-n**: dependía de una decisión de negocio abierta (sección 13). Desde el 2026-11-06 ya no queda ninguno: D-50 a D-53 están TOMADAS y esos criterios figuran como CONFIRMADO con la opción elegida.

Cómo se prueba: API = prueba de integración; COMP = prueba de componente del frontend; MAN = prueba manual de la responsable.
Rutas, nombres de campos y textos son orientativos; el contrato definitivo lo fija el arquitecto (`docs/criterios/P5b-contrato-api.md`).

---

## 1. HU, RN y decisiones implicadas

| Código | Nombre | Regla / fuente | Cambios por decisiones |
|---|---|---|---|
| HU-N01 | Aviso "Vende tu camión" | D-6 | Nombre, teléfono WhatsApp y descripción (máx. 500), todos obligatorios; siempre al administrador; no crea publicación |
| HU-N02 | Aviso "Compra tu seguro todo riesgo" | D-6 | Nombre, teléfono y tipo de vehículo, todos obligatorios; lo atiende el administrador |
| — | Asignación | D-7 (modifica RN-CL-003) | "Vende tu camión" y seguros los atiende el administrador; reasignación manual auditada (ver contradicción D-52) |
| — | Visitante sin cuenta | RN-SE-005, D-3, D-9 | Sin cuenta; nombre y teléfono obligatorios, correo opcional |
| — | Comprador | D-3 | Puede pedir seguro y vender su vehículo; solo lectura sobre datos de otros |
| HU-061 | Registrar auditoría | RN-SE-003 | Trazabilidad de creación, notas, cierre, reapertura |
| HU-062 | Proteger datos personales | RN-SE-004, D-44 | Casilla de autorización sin marcar y prueba guardada |
| — | Permisos por rol | RN-SE-006 | El asesor no ve los avisos (D-6, D-7) |
| HU-032 / HU-033 | Gestionar / cerrar lead | RN-CL-004 | Se reutiliza el seguimiento de P5 (PROPUESTO) |
| HU-071 | Notificación de lead nuevo | RN-NO-002, D-48 | Solo aviso dentro del panel (PROPUESTO extenderlo a los avisos) |
| — | Alcance de la plataforma | D-45 | Solo pone en contacto; la póliza y la consignación se gestionan fuera del portal |
| — | Campos obligatorios | D-31 | Asterisco y nota "* Campo obligatorio" |
| — | Contacto del portal | D-42 | WhatsApp 316 615 2907 en los mensajes de error |

Estados: D-6 deja como PROPUESTO "Abierta → Cerrada, como los leads". Este documento propone usar los de P5: ABIERTO ("Pendiente de gestión") y CERRADO ("Finalizado"), con reapertura por el administrador.

Fuera de P5b: créditos y búsquedas (P6, D-45 por confirmar), alquiler (D-43, en pausa), gestión de la póliza o de la consignación (D-45), aviso por correo o WhatsApp (P8, D-48).

---

## 2. Qué existe hoy y qué falta

Nota: la sección "Actualización P5" de `ESTADO-REAL.md` es de antes de construir P5 (dice "NO INICIADO", commit `ae12c0b`). El contraste se hizo leyendo el código de HEAD `b057c1d`. Conviene pedir al auditor una actualización post-P5.

| Tema | Hoy (código en `feature/p5-leads`) | Falta para P5b |
|---|---|---|
| Qué es `VENDE_TU_CAMION` en `frontend/src/api/leads.ts:144` | **Solo es un valor del campo `formulario` de la prueba de autorización** (`consentimientos_datos`). Está en el CHECK de la migración 013 (`013_p5_leads.sql:103-104`), en `ConsentimientoDatos.Formularios` (`Models/Lead.cs:74-79`), en el CHECK de `CamionDbContext.cs:515` y en la etiqueta que muestra el detalle del lead ("Vende tu camión"). Nada lo escribe todavía; la prueba `ContactosTests.cs:277-278` solo comprueba que la lista lo admite. **No es un "origen" del lead**: la tabla `leads` no tiene columna de origen ni de tipo | Usar ese valor al guardar el consentimiento del aviso y añadir un origen al lead |
| Tabla `leads` | `publicacion_id` es NOT NULL con FK a `publicaciones` (`013_p5_leads.sql:41`); duplicados por publicación + teléfono | Admitir leads sin publicación y con origen (sección 3) |
| Endpoint público | `POST /api/contactos` exige `slug` (`ContactosEndpoints.cs`), con orden 413 → 400 → trampa → 400 → 404 → 409 → 429 → duplicado → 201 | Rutas públicas de los avisos |
| Validaciones reutilizables | `ReglasLeads` (nombre 2-150, teléfono colombiano normalizado `+57…`, correo opcional, autorización obligatoria) | Validación de la descripción (500) y del tipo de vehículo |
| Límite por IP | `Program.cs:94-116`: limitador encadenado por IP para toda ruta marcada con `LimitePeticionesContacto`. La partición es por IP, no por ruta: las rutas que se marquen comparten el cupo con el contacto | Marcar las rutas nuevas |
| Límite por teléfono | Cuenta los leads y los `CONTACTO_REPETIDO` del teléfono en 24 h, sin mirar el origen | Nada, si se acepta compartirlo (CA-P5b-25) |
| Campo trampa, 413 | `sitioWeb`, 8 KB | Reutilizar |
| Casilla de autorización | Componente `frontend/src/components/AutorizacionDatos.tsx`; página `#/privacidad` con aviso de borrador (D-49) | Reutilizar |
| Bandeja del administrador | `Contactos.tsx`, `DetalleContacto.tsx`, `EnlaceContactos.tsx`; `/api/leads` (lista, resumen, detalle, notas, cerrar, reabrir, reasignar). La lista y el detalle **suponen siempre un vehículo**: `LeadsEndpoints.cs:278-284` lee `l.Publicacion!.VehiculoId` y el tipo `LeadItem.vehiculo` no admite nulo | Origen visible, filtro por origen, detalle sin vehículo, resultados de cierre propios, bloqueo de reasignación |
| Barrido D-46 | Solo mueve leads con responsable asesor | Nada: los avisos nacen en Administración |
| Portal | La franja "Nuestros servicios" (`Inicio.tsx:44-50`) incluye "Seguros todo riesgo", pero es **informativa, sin enlaces** (D-42). El pie tiene "Explora", "Tu cuenta" y "Contacto" (`PiePortal.tsx`) | Accesos a los avisos (D-51 A, CONFIRMADO) y dos pantallas nuevas |
| Tablas `solicitudes` del script 002 | Sin mapear; exigen usuario y correo (contradicen D-9); `CK_Sol_Tipo` no admite estos avisos; no se sabe si existen en la base real | No se usan (sección 3) |

---

## 3. Modelo de datos propuesto: reutilizar `leads` (PROPUESTO)

Propuesta: **un aviso es un lead sin publicación y con un `origen`**. Así se reutilizan sin duplicar: el historial (`eventos_lead`), la prueba de autorización (`consentimientos_datos`), notas, cierre, reapertura, auditoría, el contador "Contactos (N nuevos)", el límite por teléfono y la bandeja del administrador.

Se descarta la tabla `solicitudes` del script 002 por los mismos motivos que en P5 (no mapeada, usuario y correo NOT NULL, `CK_Sol_Tipo` sin estos tipos, estados distintos de la v3.0, existencia en la base real sin verificar). Se descartan tablas propias para los avisos porque duplicarían la infraestructura de P5.

Cambio de esquema: **migración 014** (`database/migraciones/014_p5b_avisos.sql` + `_SUBIR.sql`), reversible, con respaldo previo y aplicada por la responsable desde phpMyAdmin (D-14, D-16). Solo ALTER sobre `leads`; no toca datos salvo el valor por defecto de la columna nueva.

| Cambio | Detalle |
|---|---|
| `leads.publicacion_id` | Pasa a NULL. La FK RESTRICT se mantiene |
| `leads.origen` (nueva) | `VARCHAR(30) NOT NULL DEFAULT 'CONTACTO_VEHICULO'`, CHECK `CONTACTO_VEHICULO`, `VENDE_TU_CAMION`, `SEGURO_TODO_RIESGO` (los mismos códigos que `consentimientos_datos.formulario`). Los leads existentes quedan como `CONTACTO_VEHICULO` |
| `leads.tipo_vehiculo` (nueva) | `VARCHAR(20) NULL`, CHECK con la lista de D-50 (a), opción B: `CAMION`, `CARRO`, `MAQUINARIA`, `OTRO` |
| `CK_Lead_Origen` | `(origen = 'CONTACTO_VEHICULO' AND publicacion_id IS NOT NULL AND tipo_vehiculo IS NULL) OR (origen = 'VENDE_TU_CAMION' AND publicacion_id IS NULL AND tipo_vehiculo IS NULL) OR (origen = 'SEGURO_TODO_RIESGO' AND publicacion_id IS NULL AND tipo_vehiculo IS NOT NULL)` |
| `CK_Lead_AvisoAdmin` (D-52 = A, CONFIRMADO) | `origen = 'CONTACTO_VEHICULO' OR responsable_asesor_id IS NULL`: la base impide asignar un aviso a un asesor |
| `CK_Lead_Resultado` | Se amplía con `ATENDIDA` ("Solicitud atendida", CA-P5b-41) |
| `leads.mensaje` | Sin cambio de tipo. En "Vende tu camión" guarda la descripción; en el seguro, un texto fijo "Solicitud de seguro todo riesgo" |
| Índice | `IX_Lead_Origen (origen, estado, creado_en)`; duplicados de avisos: `(origen, telefono, estado)` |
| BAJAR (comentada) | Solo si no hay filas con `origen <> 'CONTACTO_VEHICULO'` (si las hay, exportarlas antes con aprobación); restaura NOT NULL, quita columnas, CHECK e índice y devuelve `CK_Lead_Resultado` a 6 valores (exige que no haya `ATENDIDA`) |

Impacto en el código (para el arquitecto): `LeadsEndpoints` (proyección con vehículo opcional, filtro `origen`, resultados por origen, reasignación), `ReglasLeads`, `CamionDbContext` (CHECK nuevos), `SesionTests.Inventario_de_endpoints` (rutas anónimas nuevas con limitación), `leads.ts` (`vehiculo` nulo, `origen`, `tipoVehiculo`), `Contactos.tsx` y `DetalleContacto.tsx`.
Riesgo de despliegue: igual que P5, la API de P5b no debe arrancar contra `camion_ventas` sin la 014.

---

## 4. Formulario "Vende tu camión" (HU-N01)

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5b-01 | Un visitante sin cuenta en la pantalla "Vende tu camión" | Escribe nombre, teléfono y descripción válidos, marca la autorización y pulsa "Enviar" | 201; se crea un lead ABIERTO con `origen = VENDE_TU_CAMION`, sin publicación, responsable Administración; se ve el mensaje de éxito (CA-P5b-28) | Sin cuenta, campos y destino administrador: CONFIRMADO (D-6, D-3, D-7). Estado ABIERTO y modelo lead: PROPUESTO | API + COMP |
| CA-P5b-02 | Un comprador con sesión | Abre el formulario y lo envía | Nombre y teléfono vienen rellenados con su perfil y se pueden editar; el lead queda ligado a su cuenta (`usuario_id`) además de guardar lo escrito | Que el comprador pueda vender su vehículo: CONFIRMADO (D-3). Relleno y vínculo: PROPUESTO (como CA-P5-02) | API + COMP |
| CA-P5b-03 | El formulario | El nombre está vacío o tiene menos de 2 o más de 150 caracteres tras quitar espacios | 400 en `nombre`: "Escribe tu nombre (entre 2 y 150 caracteres)."; no se guarda nada | Obligatorio: CONFIRMADO (D-6). Límites: PROPUESTO (los de P5) | API + COMP |
| CA-P5b-04 | El formulario | El teléfono está vacío o no es un celular colombiano (empieza por 3) o fijo (empieza por 60) de 10 dígitos | 400 en `telefono`: "Escribe un celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567."; se rechazan 7 dígitos y números extranjeros | Obligatorio y "formato de teléfono válido": CONFIRMADO (D-6). Mismo formato que P5 (celular o fijo): CONFIRMADO (D-50 c, opción A) | API + COMP |
| CA-P5b-05 | El formulario | El teléfono llega como `300 123 4567`, `300-123-4567`, `(300) 1234567`, `+57 3001234567` o `573001234567` | Se acepta y se guarda como `+573001234567` | PROPUESTO (CA-P5-05) | API |
| CA-P5b-06 | El formulario | La descripción está vacía o solo tiene espacios | 400 en `descripcion`: "Describe tu vehículo (máximo 500 caracteres)." | Obligatoria: CONFIRMADO (D-6). Texto: PROPUESTO | API + COMP |
| CA-P5b-07 | El formulario | La descripción tiene exactamente 500 caracteres / tiene 501 | Con 500 se acepta. Con 501, 400 en `descripcion`: "La descripción no puede pasar de 500 caracteres."; el formulario muestra un contador "N/500" y no deja escribir más de 500 | Máximo 500: CONFIRMADO (D-6). Contador y tope en pantalla: PROPUESTO | API + COMP |
| CA-P5b-08 | El formulario | Se muestra | Pide solo nombre, teléfono WhatsApp y descripción (más la casilla). No pide ciudad, marca ni año; si la petición los trae, se ignoran | Tres campos: CONFIRMADO (D-6). Campos extra: **CONFIRMADO (D-50 b, opción A)**: no se piden ciudad, marca ni año | API + COMP |
| CA-P5b-09 | El formulario | Se muestra un campo de correo | Si existe, es opcional y con la regla de P5 (formato, hasta 254): "Escribe un correo válido o deja el campo vacío." | **CONFIRMADO (D-50 c, opción A)**: correo opcional en ambos avisos y teléfono con la regla de P5 (celular o fijo), por coherencia con D-9 | API + COMP |
| CA-P5b-10 | Una petición manipulada | Envía `slug`, `publicacionId`, `origen`, `estado`, `asesorId` o `responsable` | Se ignoran: el origen lo fija la ruta, el estado nace ABIERTO y el responsable es siempre Administración | PROPUESTO (consecuencia de D-6 "siempre al administrador") | API |

## 5. Formulario "Compra tu seguro todo riesgo" (HU-N02)

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5b-11 | Un visitante sin cuenta en la pantalla del seguro | Escribe nombre y teléfono válidos, elige el tipo de vehículo, marca la autorización y pulsa "Enviar" | 201; lead ABIERTO con `origen = SEGURO_TODO_RIESGO`, `tipo_vehiculo` elegido, sin publicación, responsable Administración; `mensaje` = "Solicitud de seguro todo riesgo" | Campos y destino: CONFIRMADO (D-6, D-7). Modelo y texto fijo: PROPUESTO | API + COMP |
| CA-P5b-12 | Un comprador con sesión | Lo envía | Igual que CA-P5b-02 | Pedir seguro: CONFIRMADO (D-3). Relleno y vínculo: PROPUESTO | API + COMP |
| CA-P5b-13 | El formulario | Nombre inválido | Igual que CA-P5b-03 | Obligatorio: CONFIRMADO (D-6). Límites: PROPUESTO | API + COMP |
| CA-P5b-14 | El formulario | Teléfono vacío o inválido; teléfono con separadores | Igual que CA-P5b-04 y CA-P5b-05 | Obligatorio: CONFIRMADO (D-6). Formato P5: PROPUESTO | API + COMP |
| CA-P5b-15 | El formulario | El tipo de vehículo no se elige o llega un valor fuera de la lista | 400 en `tipoVehiculo`: "Elige el tipo de vehículo." | Obligatorio: CONFIRMADO (D-6). Lista: **CONFIRMADO (D-50 a, opción B)**: Camión / Carro / Maquinaria / Otro | API + COMP |
| CA-P5b-16 | El formulario | Se elige "Otro" | Se acepta sin pedir más datos; no hay campo de descripción en este aviso | PROPUESTO (D-6 no pide más campos; no se inventan) | API + COMP |
| CA-P5b-17 | El formulario | Correo | Igual que CA-P5b-09 | **CONFIRMADO (D-50 c, opción A)** | API + COMP |

## 6. Reglas comunes: autorización, anti-abuso, duplicados y mensajes

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5b-18 | Cualquiera de los dos formularios | Se abre | Muestra la casilla **sin marcar** de `AutorizacionDatos` con enlace a la política (`#/privacidad`, borrador según D-49) | CONFIRMADO (D-44, con la salvedad de la nota 1 de P5: D-44 sigue PROPUESTO en el registro) | COMP |
| CA-P5b-19 | La casilla sin marcar | Se envía o se llama a la API con `autorizacionDatos` ausente, `null` o `false` | 400 en `autorizacionDatos`: "Debes autorizar el tratamiento de tus datos para enviar el formulario."; no se guarda nada | Bloqueo: CONFIRMADO (D-44). Mensaje y código: PROPUESTO | API + COMP |
| CA-P5b-20 | Un envío válido | Se guarda | En la misma transacción se guarda una fila en `consentimientos_datos` con `formulario = VENDE_TU_CAMION` o `SEGURO_TODO_RIESGO`, versión de la política tomada de la configuración del servidor, fecha UTC, `usuario_id` si hay sesión, `entidad = Lead` y el id del lead | Fecha, versión y formulario: CONFIRMADO (D-44). Códigos y transacción: PROPUESTO (ya admitidos por el CHECK de la 013) | API |
| CA-P5b-21 | Un comprador que ya aceptó la política al registrarse | Envía un aviso | También debe marcar la casilla; cada envío guarda su prueba | CONFIRMADO (D-44: "todo formulario de contacto o aviso") | API + COMP |
| CA-P5b-22 | El campo trampa `sitioWeb` | Llega relleno | 201 con el mensaje de éxito normal; no se guarda nada | PROPUESTO (CA-P5-22) | API |
| CA-P5b-23 | Un cuerpo de más de 8 KB / JSON malformado | Se envía | 413 "La solicitud es demasiado grande." / 400 "La solicitud no tiene un formato válido." | PROPUESTO (CA-P5-22, contrato P5) | API |
| CA-P5b-24 | Una misma IP | Supera 5 envíos en 10 minutos o 20 al día, **sumando** contacto de vehículo y ambos avisos | 429 "Has enviado muchas solicitudes. Intenta de nuevo en unos minutos." Los límites son configurables (`Contactos:*`) | PROPUESTO (el limitador actual ya particiona solo por IP) | API |
| CA-P5b-25 | Un mismo teléfono normalizado | Supera 10 envíos aceptados en 24 h entre leads, avisos y contactos repetidos | 429 con el mismo mensaje; no se guarda nada | PROPUESTO (CA-P5-21; límite compartido) | API |
| CA-P5b-26 | Existe un aviso ABIERTO con el mismo teléfono y el mismo origen | Llega otro del mismo tipo | No se crea otro: se añade `CONTACTO_REPETIDO` al historial con la descripción nueva (o "Solicitud de seguro todo riesgo" y el tipo elegido) y se guarda su consentimiento. Respuesta idéntica a un envío nuevo | PROPUESTO (CA-P5-23) | API |
| CA-P5b-27 | El aviso anterior de ese teléfono y ese origen está CERRADO, o el abierto es de otro origen | Llega uno nuevo | Se crea un lead nuevo | PROPUESTO (CA-P5-24) | API |
| CA-P5b-28 | Un envío aceptado (nuevo, repetido o trampa) | Termina | Mensaje único: "¡Listo! Recibimos tu solicitud. Te contactaremos pronto por teléfono o WhatsApp."; el formulario se vacía (con sesión vuelve a los datos del perfil, D-49) | PROPUESTO | COMP |
| CA-P5b-29 | Un error 400 | Se muestra | Cada error junto a su campo; se conservan los datos escritos | PROPUESTO (CA-P5-55) | COMP |
| CA-P5b-30 | Un 429, 500 o falta de conexión | Se muestra | El 429 con su texto; el 500 o sin conexión: "No pudimos enviar tus datos. Intenta de nuevo en unos minutos o escríbenos por WhatsApp al 316 615 2907." Sin trazas técnicas | WhatsApp: CONFIRMADO (D-42). Textos: PROPUESTO | COMP |
| CA-P5b-31 | El formulario en pantalla | Se abre y se envía | Empieza con "* Campo obligatorio"; nombre, teléfono, descripción / tipo de vehículo y la casilla llevan asterisco y `aria-required="true"` (sin el atributo `required`); el botón se desactiva mientras se envía | Asterisco y nota: CONFIRMADO (D-31). Lo demás: PROPUESTO | COMP |

## 7. Quién atiende y qué no se crea

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5b-32 | Cualquier aviso | Se crea | El responsable es siempre Administración (`responsable_asesor_id = NULL`), sin importar quién lo envía ni lo que traiga la petición; historial "Creado" y "Asignado a Administración (automático)", actor SISTEMA | Al administrador, nunca a un asesor: CONFIRMADO (D-6, D-7). Eventos: PROPUESTO | API |
| CA-P5b-33 | Un aviso "Vende tu camión" | Se crea | No se crea ningún vehículo, publicación, foto ni plan: los conteos de `vehiculos` y `publicaciones` no cambian | CONFIRMADO (D-6: "no crea una publicación") | API |
| CA-P5b-34 | Cualquier aviso | Se crea | `auditoria_eventos` registra `Lead / CREAR` con `origen` y `conCuenta`, sin nombre, teléfono, correo ni descripción; el registro técnico tampoco los incluye | Trazabilidad: CONFIRMADO (RN-SE-003, RN-SE-004). Formato: PROPUESTO | API |
| CA-P5b-35 | Un asesor o un administrador con sesión | Envía un aviso | Se acepta como cualquier persona; queda ligado a su usuario y el responsable sigue siendo Administración (no hay 409, porque no hay vehículo propio) | PROPUESTO | API |
| CA-P5b-36 | Un aviso ABIERTO | Corre el barrido de D-46 o se reasigna un vehículo (D-34) | El aviso no se mueve | PROPUESTO (consecuencia: no tiene asesor ni vehículo) | API |

## 8. Panel del administrador

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5b-37 | Un administrador | Abre "Contactos" | La lista incluye los avisos junto a los contactos de vehículos, con una etiqueta de origen ("Contacto de vehículo", "Vende tu camión", "Seguro todo riesgo") y un filtro por origen; un origen no válido da 400 "Origen no válido." | Que el administrador los vea en su panel: PROPUESTO (D-6 lo deja propuesto). Bandeja común y filtro: PROPUESTO | API + COMP |
| CA-P5b-38 | El detalle de un aviso | Se abre | Muestra nombre, teléfono, correo (si hay), la descripción (N01) o el tipo de vehículo (N02), "Sin vehículo asociado", el historial y los consentimientos (formulario "Vende tu camión" / "Seguro todo riesgo"). Abrirlo lo marca como visto | PROPUESTO (CA-P5-42, CA-P5-16) | API + COMP |
| CA-P5b-39 | Avisos ABIERTOS sin abrir | El administrador entra al panel | Cuentan en "Contactos (N nuevos)" | PROPUESTO (extiende D-48) | API + COMP |
| CA-P5b-40 | Un aviso ABIERTO | El administrador añade una nota de 5 a 500 caracteres / fuera de ese rango / el aviso está CERRADO | 201 en el historial / 400 "La nota debe tener entre 5 y 500 caracteres." / 409 "Este contacto está cerrado." | Seguimiento: PROPUESTO (reutiliza CA-P5-37, 38; D-6 no lo define) | API + COMP |
| CA-P5b-41 | Un aviso ABIERTO | El administrador lo cierra | Resultados disponibles para avisos: Solicitud atendida (`ATENDIDA`, nuevo), No le interesó, No respondió, Contacto duplicado o inválido, Otro (observación de 5 a 500). "Se concretó la venta" y "El vehículo ya no está disponible" no se ofrecen y la API responde 400 en `resultado`: "Ese resultado no aplica a esta solicitud." Queda en historial y auditoría | Cerrar (Abierta → Cerrada): PROPUESTO (D-6). Lista: PROPUESTO | API + COMP |
| CA-P5b-42 | Un aviso CERRADO | Se intenta cerrar otra vez / el administrador lo reabre con motivo de 5 a 500 | 409 "Este contacto ya está cerrado." / vuelve a ABIERTO en Administración, con historial y auditoría | PROPUESTO (CA-P5-40, 41) | API |
| CA-P5b-43 | Un aviso ABIERTO | El administrador intenta reasignarlo a un asesor | El diálogo de reasignar no aparece en los avisos y la API responde 409 "Esta solicitud la atiende siempre la administración."; la base lo impide con `CK_Lead_AvisoAdmin` | **CONFIRMADO (D-52, opción A)**: los avisos nunca se reasignan a un asesor | API + COMP |
| CA-P5b-44 | Los filtros por vehículo o publicación | Se aplican | Excluyen los avisos (no tienen vehículo) | PROPUESTO | API |
| CA-P5b-45 | Cualquier usuario | Intenta editar los datos del titular o borrar un aviso | No existe ruta para hacerlo; las correcciones van como nota | PROPUESTO (CA-P5-43, 60) | API |

## 9. Permisos

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5b-46 | Un asesor Aprobado | Abre "Mis contactos" o pide `GET /api/leads` y `/api/leads/resumen` | Nunca aparecen avisos ni cuentan en sus nuevos | CONFIRMADO (D-6 "nunca a un asesor", D-7, RN-SE-006) | API + COMP |
| CA-P5b-47 | Un asesor | Pide el detalle o una acción sobre un aviso por su id | 404 "Contacto no encontrado." (no se revela que existe) | 404: PROPUESTO (CA-P5-30) | API |
| CA-P5b-48 | Un comprador con sesión | Llama a cualquier ruta de lectura o gestión de `/api/leads` | 403; no ve avisos de otros | CONFIRMADO (D-3, RN-SE-006) | API |
| CA-P5b-49 | Un comprador con sesión | Busca sus propios avisos en "Mi cuenta" | No hay lista propia en P5b; la pantalla "Mis solicitudes" llega con P6 | **CONFIRMADO (D-53, opción A)** | COMP |
| CA-P5b-50 | Una petición sin sesión | Llama a rutas de lectura o gestión de `/api/leads` / envía un aviso | 401 / se acepta (las rutas de los avisos son anónimas) | CONFIRMADO (D-3, D-6, RN-SE-006) | API |
| CA-P5b-51 | Un asesor Pendiente, Rechazado o Bloqueado | Envía un aviso / intenta ver leads | Se acepta como cualquier visitante / sin acceso al panel | PROPUESTO / CONFIRMADO (D-10) | API |
| CA-P5b-52 | El portal público | Se consulta | Nunca muestra avisos, cuántos hay ni datos de quien los envió | PROPUESTO (CA-P5-35) | API |

## 10. Ubicación en el portal y accesibilidad

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5b-53 | Cualquier persona, con o sin sesión y con cualquier rol | Abre `#/vende-tu-camion` o `#/seguro-todo-riesgo` | Ve el formulario correspondiente, con encabezado y pie del portal | Sin cuenta: CONFIRMADO (D-6). Rutas: PROPUESTO | COMP |
| CA-P5b-54 | El portal | Se recorre | Hay accesos visibles a ambos avisos | Dos bloques en la portada junto a la banda de crédito y una columna "Servicios" en el pie; sin tocar el menú principal ni la franja informativa de D-42 | **CONFIRMADO (D-51, opción A)** | COMP + MAN |
| CA-P5b-55 | El formulario | Se inspecciona | Un `h1` con el nombre del aviso; cada campo con `label` asociado; errores enlazados con `aria-describedby` y `aria-invalid`; el contador de la descripción en `aria-live="polite"`; el tipo de vehículo es un `select` nativo | PROPUESTO | COMP |
| CA-P5b-56 | Un envío con errores / aceptado | Termina | El foco va al primer campo con error / el mensaje de éxito está en `role="status"` y recibe el foco | PROPUESTO (CA-P5-55) | COMP |
| CA-P5b-57 | Solo teclado o lector de pantalla | Se usa el formulario | Todo se opera con teclado en orden lógico; el campo trampa está fuera del orden de tabulación y oculto al lector (`aria-hidden`, `tabindex="-1"`); el enlace a la política indica "(se abre en otra pestaña)" | PROPUESTO | COMP |
| CA-P5b-58 | Un móvil de 360 px | Se abre el formulario | Se ve sin desplazamiento horizontal; el teléfono usa `inputmode="tel"` y `autocomplete="tel"`, el nombre `autocomplete="name"`; textos y botones con contraste AA sobre los colores del portal (D-42) | Colores: CONFIRMADO (D-42). Lo demás: PROPUESTO | COMP + MAN |

## 11. Esquema y no regresión

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P5b-59 | La migración 014 en una base de prueba con leads de P5 | Se aplica SUBIR | Los leads existentes quedan con `origen = CONTACTO_VEHICULO`; conteos de `leads`, `eventos_lead` y `consentimientos_datos` iguales antes y después; CHECK nuevos presentes | PROPUESTO (procedimiento D-14, D-16) | MAN + API |
| CA-P5b-60 | La base con avisos guardados | Se pretende ejecutar BAJAR | Las comprobaciones previas de BAJAR detienen el proceso si hay filas con origen distinto de `CONTACTO_VEHICULO` o resultado `ATENDIDA` | PROPUESTO (regla permanente: no perder datos) | MAN |
| CA-P5b-61 | P5b construido | Se ejecutan las pruebas de P5 | Siguen pasando: el contacto desde la ficha, la asignación, el barrido y la bandeja no cambian | PROPUESTO | API + COMP |
| CA-P5b-62 | Una petición que intenta crear por la API un lead `CONTACTO_VEHICULO` sin publicación o un aviso con publicación | Se guarda | La base lo rechaza (`CK_Lead_Origen`); la API nunca lo intenta | PROPUESTO | API |

---

## 12. Trazabilidad HU/RN/D → criterio → prueba prevista

| Origen | Criterios | Prueba prevista |
|---|---|---|
| HU-N01 / D-6 (Vende tu camión) | CA-P5b-01 a 10, 33 | API: envío anónimo y con comprador, validación por campo, 500/501 caracteres, sin publicación creada. COMP: formulario y contador |
| HU-N02 / D-6 (Seguro) | CA-P5b-11 a 17 | API: envío, tipo de vehículo fuera de lista. COMP: formulario con `select` |
| D-3 / RN-SE-005 (sin cuenta; comprador puede vender y pedir seguro) | CA-P5b-01, 02, 11, 12, 50, 53 | API + COMP |
| D-9 (nombre y teléfono obligatorios, correo opcional) | CA-P5b-03, 04, 09, 13, 14, 17 | API + COMP (09 y 17 con D-50 c A) |
| D-7 / D-6 (siempre el administrador) | CA-P5b-10, 32, 35, 36, 43, 46 | API: responsable NULL siempre; barrido no los mueve; reasignación 409 (D-52 A) |
| RN-SE-006 (permisos) | CA-P5b-46 a 52 | API: matriz de roles (anónimo 401 en lectura, comprador 403, asesor sin avisos y 404, administrador todo) |
| RN-CL-004 / HU-032, HU-033 (seguimiento, extendido) | CA-P5b-37 a 45 | API: notas, cierre con resultados de aviso, reapertura. COMP: detalle sin vehículo |
| RN-SE-003 / HU-061 (auditoría) | CA-P5b-32, 34, 41, 42 | API: eventos e `auditoria_eventos` sin datos personales |
| D-44 / RN-SE-004 / HU-062 | CA-P5b-18 a 21, 34 | API: rechazo sin casilla, consentimiento con el formulario correcto. COMP: casilla sin marcar |
| D-48 / HU-071 | CA-P5b-39 | API + COMP: contador de nuevos del administrador |
| D-31 | CA-P5b-31 | COMP |
| D-42 | CA-P5b-30, 54, 58 | COMP + MAN |
| Anti-abuso (sin fuente; PROPUESTO) | CA-P5b-22 a 27 | API: trampa, 413, límite compartido por IP y por teléfono, duplicado por origen |
| Accesibilidad (sin fuente; PROPUESTO) | CA-P5b-55 a 58 | COMP + MAN |
| Esquema (migración 014) | CA-P5b-59 a 62 | MAN con respaldo; API contra la base de pruebas |
| Prueba manual | — | MAN: con la 014 aplicada, la responsable envía ambos avisos sin cuenta y con cuenta de comprador, comprueba que un asesor no los ve, y como administradora los anota, cierra y reabre |

---

## 13. Contradicciones y decisiones

Registradas en `DECISIONES.md` como PENDIENTE y **TOMADAS por la responsable el 2026-11-06 según la recomendación**:

- **D-50** — Datos de los formularios: (a) lista de tipo de vehículo del seguro; (b) si "Vende tu camión" pide ciudad, marca o año; (c) si los avisos piden correo opcional (D-9 frente a D-6) y si el teléfono WhatsApp admite fijos. **Decisión:** (a) B, Camión / Carro / Maquinaria / Otro; (b) A, sin ciudad, marca ni año; (c) A, correo opcional y teléfono con la regla de P5.
- **D-51** — Dónde se ubican los avisos en el portal (D-42 dice que la franja de servicios es informativa y sin enlaces, y "no tan recargado"). **Decisión:** A, dos bloques en la portada y la columna "Servicios" en el pie; menú y franja sin cambios.
- **D-52** — Contradicción: D-6 dice que el aviso va "siempre al administrador, nunca a un asesor"; D-7 y `CLAUDE.md` dicen que el administrador puede reasignar manualmente **cualquier** solicitud a un asesor. **Decisión:** A, los avisos nunca se reasignan a un asesor (409 en la API y `CK_Lead_AvisoAdmin` en la base).
- **D-53** — Si el comprador con sesión ve sus avisos en "Mi cuenta". **Decisión:** A, no en P5b; "Mis solicitudes" llega con P6.

Observaciones sin decisión de negocio:
- `ESTADO-REAL.md` ("Actualización P5") describe el estado previo a P5; hay que actualizarlo.
- Lo PROPUESTO de D-6 (estados Abierta → Cerrada, panel del administrador) se concreta aquí reutilizando P5; si la responsable quiere otro flujo, cambia la sección 8.
