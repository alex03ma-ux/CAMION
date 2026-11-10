# Criterios de aceptación P6: créditos, búsquedas y "Mis solicitudes"

Ítem del backlog: **P6 "Créditos y búsquedas"** (HU-034 a HU-042, más "Mis solicitudes" de D-53). Rama: `feature/p6-creditos`, apilada sobre `feature/p5b-avisos`.
Criterio de salida (BACKLOG.md): formularios, estados, asignación al administrador (D-7), notificaciones y seguimiento; pantalla "Mis solicitudes" en "Mi cuenta" con avisos, contactos, créditos y búsquedas (D-53).
Autor: analista funcional. Fecha: 2026-11-10. Estado del documento: PROPUESTO (pendiente de revisión del coordinador y de las decisiones D-54 a D-59).

Fuentes leídas:
- `docs/requisitos/requisitos-funcionales-v3.0.md`: matriz §7 (HU-034 a HU-042, HU-071, HU-072), FN-02 y FN-03 (§8), RN-CO-006 a RN-CO-008, RN-BU-001, RN-BU-002 y RN-NO-003 (§9), estados (§12-13).
- `docs/requisitos/minuta-transferencia-agentes-ia.md` §5 (M05, M06), §6 ("Quiero un crédito") y §7 (FN-02, FN-03).
- `docs/coordinacion/DECISIONES.md` completo (en especial D-3, D-4, D-5, D-6, D-7, D-9, D-17, D-29, D-32, D-42, D-44, D-45, D-46, D-48, D-50 a D-53).
- `docs/criterios/criterios-P5-leads.md`, `docs/criterios/criterios-P5b-avisos.md`, `docs/coordinacion/ESTADO-REAL.md` (actualización P5b) y `docs/legal/POLITICA-TRATAMIENTO-DATOS.md` (nota P6).
- Código en HEAD `7908e4a`: migraciones 013 y 014, `Endpoints/AvisosEndpoints.cs`, `Endpoints/LeadsEndpoints.cs`, `Endpoints/ContactosEndpoints.cs`, `Models/Lead.cs`, `Leads/ReglasLeads.cs`, `Leads/BarridoLeads.cs`; en el frontend `AvisoPublico.tsx`, `Contactos.tsx`, `DetalleContacto.tsx`, `DetalleVehiculo.tsx`, `Inicio.tsx`, `PerfilPanel.tsx`, `api/leads.ts`.

Numeración: se usa la matriz de 87 HU (D-1). En ella la notificación de crédito es **HU-072** (la lista de la sección 10 la llama HU-073) y HU-063 es "Recuperar contraseña".

Marcas:
- **CONFIRMADO (fuente)**: sale literalmente de una HU, RN o decisión TOMADA.
- **PROPUESTO**: lo infiere el analista; necesita visto bueno del coordinador o de la responsable.
- **PENDIENTE D-n**: depende de una decisión de negocio abierta (sección 15). Se escribe con la opción recomendada; si la responsable elige otra, el criterio cambia.

Cómo se prueba: API = prueba de integración; COMP = prueba de componente del frontend; MAN = prueba manual de la responsable.
Rutas, nombres de campos y textos son orientativos; el contrato definitivo lo fija el arquitecto (`docs/criterios/P6-contrato-api.md`).

---

## 1. HU, RN y decisiones implicadas

| Código | Nombre (matriz v3.0) | Regla | Actor v3.0 | Cambios por decisiones |
|---|---|---|---|---|
| HU-034 | Registrar búsqueda | RN-BU-001 "Permite registrar necesidades" | Cliente | Sin cuenta (D-3, D-9) |
| HU-035 | Consultar búsquedas | RN-BU-001 | Asesor/Admin | Solo el administrador (D-7) |
| HU-036 | Relacionar vehículo a búsqueda | RN-BU-002 "Relacionar y cerrar búsquedas" | Asesor | Lo hace el administrador (D-7); alcance en D-56 |
| HU-037 | Gestionar búsqueda | RN-BU-002 | Asesor | Administrador (D-7) |
| HU-038 | Cerrar búsqueda | RN-BU-002 | Asesor | Administrador (D-7) |
| HU-039 | Solicitar crédito | RN-CO-006 "Permite registrar solicitudes" | Cliente | Sin cuenta (D-3, D-9); "abrir primero un formulario" (minuta §6) |
| HU-040 | Registrar solicitud | RN-CO-006 | Sistema | — |
| HU-041 | Asignar solicitud | RN-CO-007 "Asigna responsable" | Sistema/Admin | Siempre al administrador (D-7); reasignación en D-58 |
| HU-042 | Gestionar solicitud | RN-CO-008 "Permite gestionar solicitud. Seguimiento" | Asesor/Admin | Administrador (D-7); la gestión ante bancos ocurre fuera del portal (D-45) |
| HU-072 | Notificación solicitud crédito | RN-NO-003 "Notificar solicitud crédito. Responsable" | Sistema | Sin servicio de correo (D-5, D-48) |
| HU-071 | Notificación nuevo lead | RN-NO-002 | Sistema | Solo aviso dentro del panel (D-48) |
| HU-061 / HU-062 | Auditoría / protección de datos | RN-SE-003 / RN-SE-004 | Sistema | Autorización con prueba guardada (D-44) |
| — | Estados de crédito | v3.0 §12-13, D-4 | — | Registrado → Asignado → Gestionado → Cerrado (D-4); ver D-57 |
| — | Permisos | RN-SE-006 | — | Comprador de solo lectura (D-3) |
| — | "Mis solicitudes" | D-53 | Comprador | Una sola pantalla con avisos, contactos, créditos y búsquedas |
| — | Modelo de negocio | D-45 | — | Intermediación: la plataforma solo pone en contacto |

Flujos v3.0: FN-02 "Registro búsqueda → Validación → Publicación → Relación vehículo → Atención → Cierre"; FN-03 "Solicitud crédito → Registro → Asignación → Gestión → Resultado → Cierre".
Minuta §7: FN-02 "sistema compara opciones → asesor/admin da seguimiento → se registra resultado"; FN-03 "evaluación/seguimiento → notificaciones → cierre; WhatsApp posterior opcional".

Fuera de P6: estudio de crédito, documentos, ingresos, centrales de riesgo y entidades financieras (D-45, ver sección 2); coincidencias automáticas y alertas de búsqueda (`alertas_busqueda` del script 002; PENDIENTE D-56 c); aviso por correo o WhatsApp (P8, D-48); reporte de créditos HU-078 (P8); recuperación de contraseña HU-063 (BLOQUEADO sin correo).

---

## 2. Contradicciones entre la v3.0 y D-45

D-45 (TOMADA): "somos intermediarios… los créditos se gestionan ante bancos y entidades financieras, nosotros somos brókers"; "en esta plataforma solo vamos a hacer contacto asesor y usuario, lo otro es interno". Además, la autorización para compartir datos con bancos y aseguradoras **no** se pide en la plataforma.

Aclaración importante: **la v3.0 no pide literalmente** estudio de crédito, documentos, ingresos, centrales de riesgo ni relacionar entidades financieras. Sus RN son muy escuetas ("Permite registrar solicitudes", "Asigna responsable", "Permite gestionar solicitud. Seguimiento"). Lo que choca con D-45 es lo que la v3.0 y las demás fuentes **implican** sobre el ciclo de vida del crédito:

| # | Fuente | Qué dice | Choque con D-45 | Propuesta mínima |
|---|---|---|---|---|
| C-1 | v3.0 FN-03, §1.5 ("Gestionar solicitudes de crédito"); minuta §5 M06 ("gestión, seguimiento y comunicación") y §7 FN-03 ("evaluación/seguimiento") | La plataforma evalúa y gestiona el crédito hasta un "Resultado" | La evaluación la hace el banco, fuera del portal | La solicitud es un contacto para el administrador: se anota, se cierra y se registra un resultado de cierre (D-54, D-57) |
| C-2 | v3.0 §12-13 y D-4 (TOMADA) | Crédito Registrado → Asignado → Gestionado → Cerrado | Con D-7 "Asignado" ocurre al instante (siempre al administrador) y "Gestionado" sucede fuera del portal. Además, P5/P5b usan ABIERTO/CERRADO | Mostrar esos estados derivados del lead, sin estados nuevos en la base (D-57) |
| C-3 | `POLITICA-TRATAMIENTO-DATOS.md`, nota P6 | "Cuando exista la solicitud de crédito se recogerán datos financieros o laborales… centrales de riesgo (Ley 1266 de 2008)" | D-45 deja esos datos y esa autorización en el proceso interno | El portal no pide ingresos, actividad, documento, historial ni autorización de centrales (CA-P6-12, CA-P6-28). La nota de la política debe actualizarse (tarea del documentador, con el abogado; D-44) |
| C-4 | Script 002 `detalle_solicitud_credito.valor_solicitado_cop` | El crédito guarda un monto | No es un dato sensible del titular, pero es un dato financiero de la compra | Monto y cuota inicial opcionales (D-55) |
| C-5 | v3.0 FN-02 ("Validación → Publicación") y minuta FN-02 ("sistema compara opciones") | La búsqueda se publica y el sistema busca coincidencias | Publicar la necesidad del comprador expondría sus datos a asesores (D-7) y va más allá de "poner en contacto" (D-45) | La búsqueda no se publica; el administrador relaciona vehículos a mano (D-56) |
| C-6 | HU-036 a HU-038 y RN-BU-002 (actor Asesor); HU-042 (Asesor/Admin) | El asesor gestiona búsquedas y créditos | Ya lo modificó D-7: los atiende el administrador | Sin cambio; la reasignación queda en D-58 |
| C-7 | RN-NO-003 / HU-072 | Notificar la solicitud de crédito al responsable | No hay servicio de correo (D-5) | Contador dentro del panel (D-48), como P5b (CA-P6-57) |

C-1, C-2 y C-5 se registran como decisiones (D-54, D-57 y D-56). C-3 es una tarea documental y legal, no una decisión nueva: D-45 ya la resolvió.

---

## 3. Qué existe hoy y qué falta

Nota: `ESTADO-REAL.md` (actualización P5b, commit `4c871ef`) dice que la 014 no está aplicada; el commit `7908e4a` registra que se aplicó con respaldo. Conviene que el auditor actualice el documento.

| Tema | Hoy (código en `feature/p5b-avisos`, HEAD `7908e4a`) | Falta para P6 |
|---|---|---|
| Créditos y búsquedas | **NO INICIADO.** Ninguna ruta, pantalla ni tabla mapeada. Las tablas `solicitudes`, `detalle_solicitud_credito` y `detalle_busqueda_personalizada` del script 002 no están mapeadas y exigen usuario y correo (contradicen D-9) | Todo |
| "Quiero un crédito" | La banda de crédito de la portada enlaza a `HASH_REGISTRO` (`Inicio.tsx:173`), con el comentario "llega en P5" | Enlazar al formulario de crédito (CA-P6-02) |
| Ficha del vehículo | `DetalleVehiculo.tsx` muestra `FormularioContacto` y el botón de WhatsApp (D-47); no hay acceso a crédito | Botón "Solicitar crédito para este vehículo" (CA-P6-03) |
| Modelo `leads` | `origen` con `CONTACTO_VEHICULO`, `VENDE_TU_CAMION` y `SEGURO_TODO_RIESGO`; `publicacion_id` NULL admitido; `tipo_vehiculo` (`CAMION`, `CARRO`, `MAQUINARIA`, `OTRO`); `CK_Lead_Origen`, `CK_Lead_AvisoAdmin`, `CK_Lead_Resultado` con `ATENDIDA` (migración 014) | Orígenes `CREDITO` y `BUSQUEDA`, datos propios y relación de vehículos (sección 14) |
| Reglas en código | `Lead.EsAviso(origen) => origen != CONTACTO_VEHICULO` (`Models/Lead.cs:37`). Todo origen nuevo queda tratado como aviso: no se reasigna (409, `LeadsEndpoints.cs:208`), usa `ResultadosAviso` al cerrar (`ReglasLeads.cs:189`) y el asesor nunca lo ve (`LeadsEndpoints.cs:249`) | Coincide con la recomendación de D-58 (A). Si se elige otra opción, hay que separar "aviso" de "solicitud de administración" y cambiar `CK_Lead_AvisoAdmin`. Los resultados por origen dependen de D-57 |
| Consentimiento | `CK_Consent_Formulario` solo admite `CONTACTO_VEHICULO`, `VENDE_TU_CAMION`, `SEGURO_TODO_RIESGO`, `REGISTRO_COMPRADOR`, `INSCRIPCION_ASESOR` (013) | Añadir `CREDITO` y `BUSQUEDA` |
| Historial | `CK_EvLead_Tipo`: CREADO, ASIGNADO, CONTACTO_REPETIDO, NOTA, REASIGNADO, CERRADO, REABIERTO | `VEHICULO_RELACIONADO` y `VEHICULO_RETIRADO` (HU-036, D-56 b) |
| Barrido D-46 y reasignación de vehículo D-34 | Solo mueven leads con responsable asesor (`BarridoLeads.cs:49`, `:99`) | Nada, si créditos y búsquedas nacen y quedan en Administración |
| Bandeja del administrador | `Contactos.tsx` y `DetalleContacto.tsx` con etiqueta y filtro de origen, detalle sin vehículo, "Solicitud atendida", sin "Reasignar" en avisos | Etiquetas "Crédito" y "Búsqueda", datos propios en el detalle, relacionar vehículos |
| Anti-abuso | Límite por IP (`LimitePeticionesContacto`), por teléfono, trampa `sitioWeb`, 8 KB (`AvisosEndpoints.cs`) | Reutilizar en las rutas nuevas |
| Notificaciones | Solo "Contactos (N nuevos)" en el panel (D-48). No hay servicio de correo (D-5); HU-063 BLOQUEADO por eso | Contar créditos y búsquedas (CA-P6-57) |
| "Mi cuenta" del comprador | `PerfilPanel.tsx` muestra datos y accesos según el rol; el comprador no tiene lista de solicitudes (CA-P5-32, CA-P5b-49) | Pantalla "Mis solicitudes" y su ruta de API (sección 12) |
| Catálogos para la búsqueda | Categorías (D-17), marcas y modelos con categoría (D-32), DIVIPOLA (D-29) ya existen y los usa el filtro público | Reutilizar |

---

## 4. Modelo propuesto: créditos y búsquedas como orígenes de `leads` (PROPUESTO)

Igual que en P5b, **una solicitud de crédito o de búsqueda es un lead con su propio `origen`**, siempre en Administración. Así se reutilizan sin duplicar el historial (`eventos_lead`), la prueba de autorización (`consentimientos_datos`), las notas, el cierre, la reapertura, la auditoría, el contador de nuevos, los límites anti-abuso y la bandeja del administrador.

- `CREDITO`: con publicación (pedido desde la ficha) o sin ella (pedido desde la portada).
- `BUSQUEDA`: sin publicación; sus criterios van en una tabla de detalle, y los vehículos que el administrador le relaciona, en otra.

Se descarta la tabla `solicitudes` del script 002 por los mismos motivos que en P5 y P5b. Los cambios de esquema están en la sección 14.

---

## 5. Solicitud de crédito (HU-039, HU-040, RN-CO-006)

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P6-01 | Un visitante sin cuenta en `#/credito` | Escribe nombre y teléfono válidos, marca la autorización y pulsa "Enviar" | 201; se crea un lead ABIERTO con `origen = CREDITO`, sin publicación y con responsable Administración; se ve el mensaje de éxito (CA-P6-14) | Solicitar sin cuenta y registrar: CONFIRMADO (HU-039, HU-040, RN-CO-006, D-3, D-9 "las solicitudes no exigen usuario registrado"). Al administrador: CONFIRMADO (D-7). Modelo lead: PROPUESTO | API + COMP |
| CA-P6-02 | La banda de crédito de la portada | Se pulsa "Quiero un crédito" | Abre `#/credito` (ya no el registro) | CONFIRMADO (D-42: "lleva al registro mientras no exista la solicitud de crédito"; minuta §6: "debe abrir primero un formulario") | COMP |
| CA-P6-03 | La ficha de un vehículo visible | Se pulsa "Solicitar crédito para este vehículo", junto al formulario de contacto | Abre el formulario de crédito con el vehículo identificado (título visible, no editable); al enviar, el lead `CREDITO` guarda la publicación y su responsable es Administración, **no** el asesor dueño | Responsable Administración: CONFIRMADO (D-7: "créditos" los atiende el administrador). Acceso desde la ficha y vínculo con la publicación: PROPUESTO | API + COMP |
| CA-P6-04 | Una publicación que dejó de ser visible (plan vencido, vendida, inactiva, eliminada) o un slug inexistente | Se envía el formulario de crédito con ese vehículo | 404 "Este vehículo ya no está disponible. Mira otros vehículos en el catálogo."; no se guarda nada | PROPUESTO (como CA-P5-09) | API + COMP |
| CA-P6-05 | Un asesor con sesión en la ficha de su propio vehículo | Mira la ficha o llama a la API | El botón de crédito no aparece; la API responde 409 "Este vehículo es tuyo; no puedes contactarte a ti mismo." | PROPUESTO (como CA-P5-11) | API + COMP |
| CA-P6-06 | Un comprador con sesión | Abre el formulario y lo envía | Nombre, teléfono y correo vienen del perfil y se pueden editar; el lead queda ligado a su cuenta (`usuario_id`) y aparece en "Mis solicitudes" | Que el comprador pida crédito: CONFIRMADO (D-3). Relleno y vínculo: PROPUESTO | API + COMP |
| CA-P6-07 | El formulario | Nombre vacío o fuera de 2-150; teléfono vacío o que no es celular (3…) ni fijo (60…) colombiano de 10 dígitos; correo con formato inválido o de más de 254 | 400 con el error en su campo y los textos de P5 ("Escribe tu nombre (entre 2 y 150 caracteres).", "Escribe un celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567.", "Escribe un correo válido o deja el campo vacío."); el teléfono se normaliza a `+57…` | Nombre y teléfono obligatorios, correo opcional: CONFIRMADO (D-9). Formatos de P5: PROPUESTO | API + COMP |
| CA-P6-08 | El campo "¿Cuánto necesitas financiar?" (opcional, pesos) | Se deja vacío / se escribe un entero entre 1.000.000 y 5.000.000.000 / un valor fuera de ese rango, negativo o con letras | Se acepta / se guarda / 400 en `monto`: "Escribe un valor en pesos entre 1.000.000 y 5.000.000.000, o deja el campo vacío." | PENDIENTE D-55 (recomendada A). Rango: PROPUESTO | API + COMP |
| CA-P6-09 | El campo "¿Con cuánto cuentas de cuota inicial?" (opcional, pesos) | Se deja vacío / un entero entre 0 y 5.000.000.000 / fuera de ese rango | Se acepta / se guarda / 400 en `cuotaInicial` con un texto análogo. No se compara con el monto | PENDIENTE D-55 (recomendada A). Rango: PROPUESTO | API + COMP |
| CA-P6-10 | El formulario abierto desde la portada (sin vehículo) | Se muestra / se elige un valor fuera de la lista | Ofrece "Tipo de vehículo" opcional con la lista de D-50 (Camión / Carro / Maquinaria / Otro) / 400 en `tipoVehiculo`: "Elige el tipo de vehículo." Abierto desde la ficha, no se pide | Lista: CONFIRMADO (D-50 a, para el seguro); usarla en crédito: PENDIENTE D-55 | API + COMP |
| CA-P6-11 | El campo "Comentario" (opcional) | Se deja vacío / hasta 500 caracteres / 501 | Se guarda "Solicitud de crédito" o "Solicitud de crédito para este vehículo" / se guarda / 400 en `mensaje`: "El comentario no puede pasar de 500 caracteres."; contador "N/500" | PROPUESTO (como CA-P5-07) | API + COMP |
| CA-P6-12 | El formulario y la API | Se muestran o se reciben | **No** se piden ni se guardan ingresos, actividad u ocupación, documento de identidad, historial crediticio, autorización de consulta a centrales de riesgo, entidad financiera elegida ni archivos adjuntos. Si la petición trae esos campos, se ignoran | CONFIRMADO (D-45: la gestión ante bancos y la autorización para compartir datos con ellos son internas). Ingresos: PENDIENTE D-55 (si la responsable elige C, este criterio cambia y hace falta revisión legal) | API + COMP |
| CA-P6-13 | El formulario | Se abre | Explica en una frase que el portal no presta dinero: "Somos intermediarios: gestionamos tu crédito ante bancos y entidades financieras. Te contactaremos para contarte las opciones y los requisitos." No promete aprobación, tasas ni plazos | Intermediación: CONFIRMADO (D-45). Texto: PROPUESTO | COMP |
| CA-P6-14 | Un envío aceptado (nuevo, repetido o trampa) | Termina | El formulario se oculta y se muestra "¡Listo! Recibimos tu solicitud. Te contactaremos pronto por teléfono o WhatsApp.", un botón opcional "Escribir por WhatsApp" al 316 615 2907 y enlaces para volver al inicio o ver vehículos. El botón de WhatsApp no crea nada | WhatsApp después del formulario y sin reemplazarlo: CONFIRMADO (minuta §6; número D-42). Ocultar el formulario y los enlaces: PROPUESTO (como P5b, commit `12dea7e`) | COMP |
| CA-P6-15 | Una petición manipulada | Trae `origen`, `estado`, `responsable`, `asesorId`, `publicacionId` o `usuarioId` | Se ignoran: el origen lo fija la ruta, el estado nace ABIERTO, el responsable es Administración, la publicación sale solo del `slug` y el usuario solo del token | PROPUESTO | API |

## 6. Búsqueda "Busco un vehículo" (HU-034, RN-BU-001)

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P6-16 | Un visitante sin cuenta en `#/busco-un-vehiculo` | Elige la categoría, escribe nombre y teléfono válidos, marca la autorización y envía | 201; lead ABIERTO con `origen = BUSQUEDA`, sin publicación, en Administración, con sus criterios guardados; mensaje de éxito como CA-P6-14 (sin botón de WhatsApp obligatorio) | Registrar la búsqueda sin cuenta: CONFIRMADO (HU-034, RN-BU-001, D-3, D-9). Al administrador: CONFIRMADO (D-7). Campos: PENDIENTE D-56 a | API + COMP |
| CA-P6-17 | El campo "Categoría" | Falta o llega un valor que no es una de las cuatro categorías | 400 en `categoria`: "Elige la categoría del vehículo que buscas." | Cuatro categorías: CONFIRMADO (D-17). Obligatoria: PENDIENTE D-56 a | API + COMP |
| CA-P6-18 | "Marca" y "Modelo" (opcionales, listas del catálogo) | Se elige un modelo que no es de esa marca o de esa categoría, o una marca o modelo inactivos | 400 en `modelo` o `marca`: "Elige un modelo de la lista." El formulario solo ofrece los activos de la categoría elegida | Modelo por categoría: CONFIRMADO (D-32). Campos opcionales: PENDIENTE D-56 a | API + COMP |
| CA-P6-19 | "Año desde" y "Año hasta" (opcionales) | Fuera de 1950 al año actual + 1, o "desde" mayor que "hasta" | 400 en `anioDesde` o `anioHasta`: "Revisa el rango de años." | PENDIENTE D-56 a. Límites: PROPUESTO | API + COMP |
| CA-P6-20 | "Presupuesto máximo" (opcional, pesos) | Fuera de 1.000.000 a 5.000.000.000 | 400 en `presupuestoMaximo`: "Escribe un valor en pesos entre 1.000.000 y 5.000.000.000, o deja el campo vacío." | PENDIENTE D-56 a. Rango: PROPUESTO | API + COMP |
| CA-P6-21 | "Departamento" y "Ciudad" (opcionales, DIVIPOLA) | Se elige un municipio que no es de ese departamento | 400 en `municipio`: "Elige una ciudad de la lista." | DIVIPOLA: CONFIRMADO (D-29). Campo: PENDIENTE D-56 a | API + COMP |
| CA-P6-22 | "Cuéntanos qué buscas" (opcional) | Hasta 500 / 501 caracteres | Se guarda en `mensaje` (vacío: "Busco un vehículo") / 400 "La descripción no puede pasar de 500 caracteres."; contador "N/500" | PENDIENTE D-56 a. Límite: PROPUESTO (como D-6) | API + COMP |
| CA-P6-23 | Un comprador con sesión | Envía una búsqueda | Datos de contacto del perfil, editables; queda ligada a su cuenta y aparece en "Mis solicitudes" | Que el comprador busque opciones de compra: CONFIRMADO (D-3). Relleno y vínculo: PROPUESTO | API + COMP |
| CA-P6-24 | Una búsqueda guardada | Se consulta el portal o el panel de un asesor | No se publica en el portal ni se muestra a ningún asesor | No a los asesores: CONFIRMADO (D-7). No publicarla (contradice FN-02 "Publicación"): PENDIENTE D-56 | API |
| CA-P6-25 | Una búsqueda guardada | Se publica después un vehículo que coincide | El sistema no compara ni avisa solo; el administrador relaciona vehículos a mano (sección 9) | PENDIENTE D-56 c (recomendada: sin coincidencias automáticas en P6) | API |
| CA-P6-26 | El portal | Se recorre | Hay acceso a "Busco un vehículo" en el catálogo (cuando no hay resultados y al final de la lista de resultados) y en la columna "Servicios" del pie; el menú, la portada y la franja informativa no cambian | PENDIENTE D-56 d (recomendada A, coherente con D-42 y D-51) | COMP + MAN |

## 7. Reglas comunes de los dos formularios

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P6-27 | Cualquiera de los dos formularios | Se abre / se envía sin la casilla | Muestra la casilla **sin marcar** de `AutorizacionDatos` con enlace a `#/privacidad` / 400 en `autorizacionDatos`: "Debes autorizar el tratamiento de tus datos para enviar el formulario."; no se guarda nada. Con la casilla, en la misma transacción se guarda la prueba con `formulario = CREDITO` o `BUSQUEDA`, versión del servidor, fecha UTC y `usuario_id` si hay sesión | Casilla y prueba: CONFIRMADO (D-44). Códigos nuevos y transacción: PROPUESTO | API + COMP |
| CA-P6-28 | La casilla del formulario de crédito | Se lee | Es la misma autorización de contacto de P5 y P5b; **no** hay casilla para compartir datos con bancos ni para consultar centrales de riesgo | CONFIRMADO (D-45) | COMP |
| CA-P6-29 | El campo trampa `sitioWeb` relleno / un cuerpo de más de 8 KB / JSON malformado | Se envía | 201 con el mensaje de éxito, sin guardar nada / 413 "La solicitud es demasiado grande." / 400 "La solicitud no tiene un formato válido." | PROPUESTO (CA-P5b-22, 23) | API |
| CA-P6-30 | Una misma IP o un mismo teléfono | Supera 5 envíos en 10 minutos o 20 al día por IP, o 10 en 24 h por teléfono, **sumando** contactos, avisos, créditos y búsquedas | 429 "Has enviado muchas solicitudes. Intenta de nuevo en unos minutos."; no se guarda nada | PROPUESTO (límites compartidos de P5 y P5b) | API |
| CA-P6-31 | Un crédito ABIERTO con el mismo teléfono y la misma publicación (o ambos sin publicación) | Llega otro | No se crea otro: se añade `CONTACTO_REPETIDO` con el comentario nuevo y los montos escritos, y se guarda su consentimiento. La respuesta es idéntica a un envío nuevo | PROPUESTO (CA-P5-23, CA-P5b-26) | API |
| CA-P6-32 | Una búsqueda ABIERTA con el mismo teléfono | Llega otra | No se crea otra: se añade `CONTACTO_REPETIDO` con un resumen legible de los criterios nuevos; los criterios guardados no cambian. Respuesta idéntica | PROPUESTO | API |
| CA-P6-33 | La solicitud anterior de ese teléfono está CERRADA, es de otro origen o (en crédito) de otra publicación | Llega una nueva | Se crea un lead nuevo | PROPUESTO (CA-P5b-27) | API |
| CA-P6-34 | Un 400 / 404 / 429 / 500 o sin conexión | Se muestra | Cada 400 junto a su campo, con el foco en el primero y los datos conservados; 404 y 429 con sus textos; 500 o sin conexión: "No pudimos enviar tus datos. Intenta de nuevo en unos minutos o escríbenos por WhatsApp al 316 615 2907." Sin trazas técnicas | Número: CONFIRMADO (D-42). Textos: PROPUESTO | COMP |
| CA-P6-35 | El formulario | Se muestra | Empieza con "* Campo obligatorio"; los obligatorios llevan asterisco y `aria-required="true"` (sin `required`); el botón se desactiva mientras se envía | Asterisco y nota: CONFIRMADO (D-31). Lo demás: PROPUESTO | COMP |
| CA-P6-36 | El formulario con teclado, lector de pantalla o un móvil de 360 px | Se usa | `h1` con el nombre del formulario, `label` en cada campo, errores con `aria-describedby` y `aria-invalid`, contador en `aria-live="polite"`, listas con `select` nativo, montos con `inputmode="numeric"`, éxito en `role="status"` con foco, trampa fuera del tabulado, sin desplazamiento horizontal y contraste AA | Colores: CONFIRMADO (D-42). Lo demás: PROPUESTO (CA-P5b-55 a 58) | COMP + MAN |

## 8. Asignación y gestión por el administrador (HU-035, HU-037, HU-038, HU-041, HU-042; RN-CO-007, RN-CO-008, RN-BU-002)

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P6-37 | Un crédito o una búsqueda | Se crea | Responsable Administración (`responsable_asesor_id = NULL`) sin importar quién lo envía; historial "Creado" y "Asignado a Administración (automático)", actor SISTEMA; `auditoria_eventos` `Lead / CREAR` con `origen`, `conCuenta` y `conVehiculo`, sin nombre, teléfono, correo, comentario ni montos | Al administrador: CONFIRMADO (D-7, que modifica RN-CO-007 y RN-BU-002). Trazabilidad: CONFIRMADO (RN-SE-003, RN-SE-004). Formato y excluir montos: PROPUESTO | API |
| CA-P6-38 | Un crédito (con o sin vehículo) o una búsqueda ABIERTOS | Corre el barrido D-46 o el administrador reasigna el vehículo (D-34) | No se mueven | PROPUESTO (no tienen responsable asesor) | API |
| CA-P6-39 | Un crédito o una búsqueda ABIERTOS | El administrador intenta reasignarlos a un asesor | No hay botón "Reasignar"; la API responde 409 "Esta solicitud la atiende siempre la administración."; la base lo impide con `CK_Lead_AvisoAdmin` | PENDIENTE D-58 (recomendada A, como D-52). Si se elige B, este criterio se sustituye por los de reasignación de P5 (CA-P5-44 a 48) | API + COMP |
| CA-P6-40 | Un administrador | Abre "Contactos" | Ve créditos y búsquedas junto a los demás, con las etiquetas "Crédito" y "Búsqueda" y el filtro por origen; un origen no válido da 400 "Origen no válido."; los filtros por vehículo incluyen los créditos de ese vehículo y excluyen las búsquedas | Que el administrador los consulte: CONFIRMADO (HU-035 con D-7). Bandeja común y filtros: PROPUESTO | API + COMP |
| CA-P6-41 | El detalle de un crédito | Se abre | Muestra nombre, teléfono, correo, comentario, monto y cuota inicial (o "No indicó"), tipo de vehículo o el vehículo (título, precio publicado, estado actual y enlace a la ficha si sigue visible), historial y consentimientos. Abrirlo lo marca como visto | PROPUESTO | API + COMP |
| CA-P6-42 | El detalle de una búsqueda | Se abre | Muestra los criterios con nombres legibles (categoría, marca, modelo, años, presupuesto, ciudad, descripción); una marca o modelo desactivados después se siguen mostrando; muestra los vehículos relacionados (sección 9) | PROPUESTO | API + COMP |
| CA-P6-43 | Un crédito o una búsqueda ABIERTOS | El administrador añade una nota de 5 a 500 caracteres / fuera de ese rango / sobre uno CERRADO | 201 en el historial / 400 "La nota debe tener entre 5 y 500 caracteres." / 409 "Este contacto está cerrado." El campo de nota recuerda: "No escribas datos financieros del cliente (ingresos, deudas, reportes en centrales)." | Seguimiento: CONFIRMADO (RN-CO-008, RN-BU-002). Notas y aviso: PROPUESTO (D-45 deja esos datos fuera del portal) | API + COMP |
| CA-P6-44 | Cualquier crédito o búsqueda | Se muestra en el panel | Estado visible: "Registrado" (ABIERTO sin notas del administrador), "En gestión" (ABIERTO con al menos una nota) o "Cerrado" | Estados de D-4 adaptados: PENDIENTE D-57 (recomendada A) | API + COMP |
| CA-P6-45 | Un crédito ABIERTO | El administrador lo cierra | Resultados: "Remitido a entidad financiera" (`REMITIDO_ENTIDAD`, nuevo), "Solicitud atendida", "No le interesó", "No respondió", "Contacto duplicado o inválido", "Otro" (observación de 5 a 500). "Se concretó la venta" y "El vehículo ya no está disponible" dan 400 en `resultado`: "Ese resultado no aplica a esta solicitud." Queda en historial y auditoría | Cerrar: CONFIRMADO (D-4 estado Cerrado, FN-03). Lista: PENDIENTE D-57 | API + COMP |
| CA-P6-46 | Una búsqueda ABIERTA | El administrador la cierra | Resultados: "Encontró vehículo" (`VEHICULO_ENCONTRADO`, nuevo), "Sin vehículo disponible" (`SIN_VEHICULO`, nuevo), "No le interesó", "No respondió", "Contacto duplicado o inválido", "Otro". Los resultados de contacto o de crédito dan 400 | Cerrar búsqueda: CONFIRMADO (HU-038, RN-BU-002). Lista: PENDIENTE D-57 | API + COMP |
| CA-P6-47 | Un crédito o una búsqueda CERRADOS | Se intenta cerrar otra vez / el administrador lo reabre con motivo de 5 a 500 | 409 "Este contacto ya está cerrado." / vuelve a ABIERTO en Administración, con historial y auditoría | PROPUESTO (CA-P5-40, 41) | API |
| CA-P6-48 | Cualquier usuario | Intenta editar los datos del titular, los montos o los criterios, o borrar la solicitud | No hay ruta para hacerlo; las correcciones van como nota | PROPUESTO (CA-P5-43, 60) | API |
| CA-P6-49 | El panel del administrador | Gestiona un crédito | No hay campos para documentos, estudio, ingresos, entidad financiera ni respuesta del banco: la gestión ocurre fuera del portal | CONFIRMADO (D-45) | API + COMP |

## 9. Relacionar vehículos a una búsqueda (HU-036, RN-BU-002)

Todo este bloque depende de **D-56 b** (recomendada A: el administrador relaciona y el comprador con cuenta los ve).

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P6-50 | Una búsqueda ABIERTA y un administrador | Relaciona una publicación visible en el portal, con una nota opcional de hasta 300 caracteres | 201; se guarda la relación (quién y cuándo), el historial registra `VEHICULO_RELACIONADO` con el título del vehículo y `auditoria_eventos` `Lead / RELACIONAR_VEHICULO` | Relacionar: CONFIRMADO (HU-036, RN-BU-002; actor administrador por D-7). Detalle: PENDIENTE D-56 b | API + COMP |
| CA-P6-51 | El detalle de la búsqueda | El administrador pulsa "Ver coincidencias" | Ve las publicaciones visibles que cumplen los criterios (categoría, marca, modelo, años, precio hasta el presupuesto, ciudad), usando la misma consulta del catálogo público; puede relacionarlas desde ahí. Es una ayuda bajo demanda, no un aviso automático | PROPUESTO | API + COMP |
| CA-P6-52 | La relación | La publicación no es visible / ya está relacionada y vigente / la búsqueda está CERRADA / ya hay 10 relacionadas vigentes | 409 "Ese vehículo no está publicado." / 409 "Ese vehículo ya está relacionado con esta búsqueda." / 409 "Esta búsqueda está cerrada." / 409 "Esta búsqueda ya tiene 10 vehículos relacionados." | PROPUESTO | API |
| CA-P6-53 | Una relación vigente | El administrador la retira | La relación queda marcada como retirada (quién y cuándo), sin borrar la fila; historial `VEHICULO_RETIRADO` y auditoría | PROPUESTO | API |
| CA-P6-54 | Un crédito, un aviso o un contacto | Se intenta relacionar un vehículo | 409 "Solo se pueden relacionar vehículos a una búsqueda." | PROPUESTO | API |
| CA-P6-55 | Un vehículo relacionado | Se relaciona | No se crea ningún lead para el asesor dueño ni se le avisa. Si el comprador lo contacta desde la ficha, se crea un contacto normal de P5 (asignado según D-7 y D-46) | PROPUESTO (coherente con D-7 y D-47) | API |
| CA-P6-56 | Un vehículo relacionado | Deja de ser visible (vendido, inactivo, plan vencido) | El administrador lo ve marcado con su estado actual; el comprador deja de verlo (CA-P6-65) | PROPUESTO | API + COMP |

## 10. Notificaciones (HU-072 / RN-NO-003, HU-071 / RN-NO-002)

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P6-57 | Créditos y búsquedas ABIERTOS que el administrador no ha abierto | Entra al panel | Cuentan en "Contactos (N nuevos)"; dejan de ser nuevos al abrir el detalle | Aviso dentro del panel: CONFIRMADO (D-48), que cubre en parte RN-NO-003. Extenderlo a créditos y búsquedas: PROPUESTO | API + COMP |
| CA-P6-58 | Cualquier solicitud | Se crea, cambia de estado o se le relaciona un vehículo | No se envía correo, SMS ni WhatsApp a nadie; el mensaje de éxito no promete un correo | CONFIRMADO (D-5 y D-48: no hay servicio de correo; aviso externo en P8). HU-063 sigue BLOQUEADO por la misma causa | API + COMP |
| CA-P6-59 | Un comprador con cuenta | Su solicitud cambia de estado o recibe un vehículo relacionado | No recibe aviso; lo ve la próxima vez que abre "Mis solicitudes" | PROPUESTO | COMP |

## 11. Permisos (RN-SE-006)

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P6-60 | Un asesor Aprobado | Abre "Mis contactos" o pide `GET /api/leads` y `/api/leads/resumen` | Nunca aparecen créditos ni búsquedas, tampoco los créditos pedidos desde la ficha de sus propios vehículos, y no cuentan en sus nuevos | Que no se le asignen: CONFIRMADO (D-7). Que no vea los créditos de sus vehículos: PENDIENTE D-58 | API + COMP |
| CA-P6-61 | Un asesor | Pide por id el detalle o una acción (nota, cierre, relacionar) sobre un crédito o una búsqueda | 404 "Contacto no encontrado." | PROPUESTO (CA-P5-30) | API |
| CA-P6-62 | Un comprador con sesión | Llama a `/api/leads` o a relacionar vehículos | 403 | CONFIRMADO (D-3, RN-SE-006) | API |
| CA-P6-63 | Una petición sin sesión | Llama a `/api/leads`, a relacionar vehículos o a "Mis solicitudes" / envía un crédito o una búsqueda | 401 / se acepta (rutas anónimas) | CONFIRMADO (D-3, D-9, RN-SE-006) | API |
| CA-P6-64 | Un asesor o un administrador con sesión | Envía un crédito (no de su propio vehículo) o una búsqueda | Se acepta y queda ligado a su usuario; el responsable sigue siendo Administración | PROPUESTO (CA-P5b-35) | API |
| CA-P6-65 | El portal público | Se consulta | Nunca muestra créditos, búsquedas, cuántos hay ni quién los envió | PROPUESTO (CA-P5b-52) | API |

## 12. "Mis solicitudes" del comprador (D-53)

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P6-66 | Un comprador con sesión | Abre "Mi cuenta" › "Mis solicitudes" | Ve **una sola lista** con sus contactos de vehículos, avisos ("Vende tu camión", "Seguro todo riesgo"), créditos y búsquedas, de la más reciente a la más antigua, 20 por página. Cada fila: fecha, tipo, resumen (título del vehículo, tipo de vehículo o categoría buscada) y estado | Una sola pantalla con los cuatro tipos en "Mi cuenta": CONFIRMADO (D-53). Columnas y orden: PENDIENTE D-59 | API + COMP |
| CA-P6-67 | Solicitudes enviadas por ese comprador **sin** sesión, o con su mismo teléfono o correo por otra persona | Abre la lista | No aparecen: solo cuentan las que se enviaron con su sesión iniciada (`usuario_id`) | PENDIENTE D-59 (recomendada: solo por `usuario_id`) | API |
| CA-P6-68 | Cualquier solicitud de la lista | Se muestra o se abre | **No** muestra notas, historial interno, responsable (asesor o administración), consentimientos, observaciones de cierre ni datos de otras personas | Solo lo suyo: CONFIRMADO (D-3, RN-SE-006). Sin notas internas ni responsable: PENDIENTE D-59 (coincide con la opción B descrita en D-53) | API + COMP |
| CA-P6-69 | La lista | Se usa | Es de solo lectura: no hay botones ni rutas para editar, cancelar o borrar una solicitud | PROPUESTO (comprador de solo lectura, D-3; para cancelar, escribe por WhatsApp) | API + COMP |
| CA-P6-70 | El estado que ve el comprador | Se muestra | Contactos y avisos: "Pendiente" o "Finalizada". Créditos y búsquedas: "Registrada", "En gestión" o "Finalizada". El resultado de cierre no se muestra | PENDIENTE D-57 y D-59 | API + COMP |
| CA-P6-71 | Una solicitud ligada a un vehículo | Se muestra | Si la publicación sigue visible, enlaza a la ficha; si no, dice "Este vehículo ya no está disponible", sin enlace | PROPUESTO | API + COMP |
| CA-P6-72 | Una búsqueda con vehículos relacionados vigentes y visibles | Se abre | Muestra "Te recomendamos estos vehículos" con foto, título, precio y enlace a la ficha. Los retirados o no visibles no aparecen; la nota interna de la relación no se muestra | PENDIENTE D-56 b | API + COMP |
| CA-P6-73 | Un comprador | Pide por id una solicitud que no es suya | 404 (no se revela que existe) | PROPUESTO (CA-P5-30) | API |
| CA-P6-74 | Un asesor o un administrador | Pide la ruta de "Mis solicitudes" | 403; su "Mi cuenta" no muestra ese acceso | PROPUESTO (D-53 habla del comprador; el asesor tiene "Mis contactos" y el administrador la bandeja) | API + COMP |
| CA-P6-75 | Un comprador sin solicitudes | Abre la pantalla | "Aún no tienes solicitudes." y enlaces a ver vehículos, "Quiero un crédito", "Busco un vehículo", "Vende tu camión" y "Seguro todo riesgo" | PROPUESTO | COMP |
| CA-P6-76 | La pantalla | Se usa con teclado o lector de pantalla | Es una tabla o lista con encabezados, estados en texto (no solo color) y paginación accesible | PROPUESTO | COMP |

## 13. Esquema y no regresión

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-P6-77 | La migración 015 en una base de prueba con leads de P5 y P5b | Se aplica SUBIR | Los conteos de `leads`, `eventos_lead` y `consentimientos_datos` no cambian; los CHECK nuevos y las tablas nuevas están presentes; los orígenes existentes siguen válidos | PROPUESTO (procedimiento D-14, D-16) | MAN + API |
| CA-P6-78 | Una base con créditos o búsquedas guardados | Se pretende ejecutar BAJAR | Las comprobaciones previas detienen el proceso si hay filas `CREDITO` o `BUSQUEDA`, resultados nuevos, consentimientos nuevos o relaciones | PROPUESTO (no perder datos) | MAN |
| CA-P6-79 | Una petición que intenta guardar una búsqueda con publicación, un crédito o búsqueda con responsable asesor (si D-58 A) o un detalle de búsqueda sin categoría | Se guarda | La base lo rechaza (`CK_Lead_Origen`, `CK_Lead_AvisoAdmin`, NOT NULL); la API nunca lo intenta | PROPUESTO | API |
| CA-P6-80 | P6 construido | Se ejecutan las pruebas de P5 y P5b | Siguen pasando: contacto desde la ficha, avisos, asignación, barrido y bandeja no cambian | PROPUESTO | API + COMP |

---

## 14. Cambio de esquema propuesto: migración 015 (PROPUESTO)

`database/migraciones/015_p6_creditos_busquedas.sql` + `_SUBIR.sql`, reversible, con respaldo previo, aplicada por la responsable desde phpMyAdmin (D-14, D-16). La parte SUBIR no borra datos. Los únicos DROP son `DROP CONSTRAINT` de CHECK que se vuelven a crear enseguida (como en la 014).

| Cambio | Detalle | Depende de |
|---|---|---|
| `CK_Lead_Origen` | Se vuelve a crear añadiendo `(origen = 'CREDITO' AND (tipo_vehiculo IS NULL OR tipo_vehiculo IN ('CAMION','CARRO','MAQUINARIA','OTRO')))` (publicación opcional) y `(origen = 'BUSQUEDA' AND publicacion_id IS NULL AND tipo_vehiculo IS NULL)` | D-55 (tipo de vehículo en crédito) |
| `CK_Lead_AvisoAdmin` | **Sin cambios**: ya obliga a que todo origen distinto de `CONTACTO_VEHICULO` quede en Administración | D-58 A. Con B o C hay que reescribirlo |
| `CK_Lead_Resultado` | Se vuelve a crear con `REMITIDO_ENTIDAD`, `VEHICULO_ENCONTRADO` y `SIN_VEHICULO` | D-57 |
| `CK_Lead_Estado` | Sin cambios (ABIERTO, CERRADO); "Registrado" y "En gestión" se derivan de las notas | D-57 A. Con B se amplía, con impacto en `CK_Lead_Cierre`, contador y bandeja |
| `CK_Consent_Formulario` (`consentimientos_datos`) | Se vuelve a crear con `CREDITO` y `BUSQUEDA` | — |
| `CK_EvLead_Tipo` (`eventos_lead`) | Se vuelve a crear con `VEHICULO_RELACIONADO` y `VEHICULO_RETIRADO` | D-56 b |
| Tabla nueva `leads_credito` | `lead_id` PK y FK a `leads` (RESTRICT); `monto_cop DECIMAL(15,0) NULL`, `cuota_inicial_cop DECIMAL(15,0) NULL`, con CHECK de rango | D-55 A. Con B no hace falta |
| Tabla nueva `leads_busqueda` | `lead_id` PK y FK; `tipo_vehiculo_id` NOT NULL (FK `tipos_vehiculo`); `marca_id`, `linea_id`, `municipio_id` NULL (FK RESTRICT); `anio_desde`, `anio_hasta` SMALLINT NULL; `presupuesto_max_cop DECIMAL(15,0) NULL`; CHECK de rangos y `anio_desde <= anio_hasta` | D-56 a |
| Tabla nueva `busqueda_vehiculos` | `id` PK; `lead_id` FK; `publicacion_id` FK (RESTRICT); `nota VARCHAR(300) NULL`; `relacionado_por` y `retirado_por` FK a `AspNetUsers` (SET NULL); `relacionado_en`; `retirado_en NULL`; UNIQUE (`lead_id`, `publicacion_id`): relacionar de nuevo reactiva la fila | D-56 b |
| Índice | `IX_Lead_Usuario (usuario_id, creado_en)` para "Mis solicitudes" | D-59 |
| No se usan | `solicitudes`, `detalle_solicitud_credito`, `detalle_busqueda_personalizada`, `alertas_busqueda` del script 002 (no mapeadas, exigen usuario y correo, contradicen D-9). Las tablas nuevas tienen nombres distintos para no chocar con ellas si existen en la base real | — |
| BAJAR (comentada) | Solo si no hay filas `CREDITO` o `BUSQUEDA`, resultados nuevos, consentimientos `CREDITO`/`BUSQUEDA` ni eventos nuevos; borra las tres tablas nuevas y devuelve los CHECK a su forma de la 014 | — |

Alternativa más ligera (si la responsable elige D-55 B y D-56 a B): sin tablas de detalle, solo el CHECK de orígenes y el de consentimientos; la búsqueda guarda la categoría en `leads.tipo_vehiculo` (ampliando su lista con `CARROCERIA`) y el resto en `mensaje`. Se pierde el filtro de coincidencias (CA-P6-51).

Impacto en el código (para el arquitecto): `Lead.Origenes` y `Lead.EsAviso` (conviene renombrarlo a "solicitud de Administración"), resultados por origen en `ReglasLeads.ValidarCierre`, `LeadsEndpoints` (proyección con detalle de crédito o búsqueda y relaciones), `CamionDbContext` (CHECK y tablas nuevas), `SesionTests.Inventario_de_endpoints` (rutas anónimas nuevas con `LimitePeticionesContacto`), `api/leads.ts` (`OrigenLead`), `Contactos.tsx`, `DetalleContacto.tsx`, `Inicio.tsx` (enlace del crédito), `DetalleVehiculo.tsx` (botón), una pantalla de solicitud parecida a `AvisoPublico.tsx`, una pantalla nueva "Mis solicitudes" y su acceso en `PerfilPanel.tsx`.
Riesgo de despliegue: como en P5 y P5b, la API de P6 no debe arrancar contra `camion_ventas` sin la 015.
Tarea documental (no de código): la política de datos (D-44) debe añadir la finalidad "solicitud de crédito o búsqueda" con los datos que de verdad se recogen (monto, cuota inicial y criterios, si se aprueban) y corregir la nota P6: el portal no recoge datos financieros ni consulta centrales (D-45).

---

## 15. Trazabilidad HU/RN/D → criterio → prueba prevista

| Origen | Criterios | Prueba prevista |
|---|---|---|
| HU-039, HU-040 / RN-CO-006 (solicitar y registrar crédito) | CA-P6-01 a 15 | API: envío anónimo, con comprador, desde la ficha (publicación guardada), publicación no visible 404, dueño 409, validaciones, montos, campos ignorados. COMP: formulario desde la portada y desde la ficha, mensaje y WhatsApp posterior |
| Minuta §6 ("primero un formulario", WhatsApp después) | CA-P6-02, 14 | COMP |
| HU-034 / RN-BU-001 (registrar búsqueda) | CA-P6-16 a 26 | API: envío, categoría obligatoria, modelo de otra marca o categoría, años, presupuesto, ciudad. COMP: listas dependientes y contador |
| HU-041 / RN-CO-007 + D-7 (asignación) | CA-P6-37 a 39 | API: responsable NULL siempre, eventos SISTEMA, barrido y reasignación de vehículo no los mueven, reasignar 409 |
| HU-035, HU-037, HU-042 / RN-CO-008, RN-BU-002 (consultar y gestionar) | CA-P6-40 a 44, 47 a 49 | API: lista y filtro por origen, detalle, notas, reapertura, sin edición. COMP: bandeja y detalle |
| HU-038 / RN-BU-002 + D-4 (cerrar) | CA-P6-45, 46 | API: resultados permitidos y no permitidos por origen |
| HU-036 / RN-BU-002 (relacionar vehículo) | CA-P6-50 a 56, 72 | API: relacionar, coincidencias, duplicado, no visible, cerrada, tope, retirar, no aplica a crédito. COMP: diálogo de relación |
| HU-072 / RN-NO-003, HU-071 / D-48 (notificación) | CA-P6-57 a 59 | API + COMP: contador de nuevos; ninguna salida de correo |
| D-45 (intermediación) | CA-P6-12, 13, 28, 49 | API: campos financieros ignorados y no guardados. COMP: textos y ausencia de campos |
| D-44 / RN-SE-004 / HU-062 (autorización) | CA-P6-27, 28, 37 | API: rechazo sin casilla, consentimiento `CREDITO` o `BUSQUEDA` en la misma transacción |
| D-3, D-9, RN-SE-005 (sin cuenta, comprador) | CA-P6-01, 06, 07, 16, 23, 63 | API + COMP |
| RN-SE-006 (permisos) | CA-P6-60 a 65, 73, 74 | API: matriz de roles (anónimo 401, comprador 403 en `/api/leads`, asesor sin créditos ni búsquedas y 404, administrador todo) |
| RN-SE-003 / HU-061 (auditoría) | CA-P6-37, 45 a 47, 50, 53 | API: eventos e `auditoria_eventos` sin datos personales ni montos |
| D-53 ("Mis solicitudes") | CA-P6-66 a 76 | API: solo `usuario_id` propio, cuatro tipos, sin notas ni responsable, 404 ajeno, 403 a otros roles. COMP: lista, estados, enlaces, vacío |
| D-31, D-42 | CA-P6-14, 34, 35, 36 | COMP + MAN |
| Anti-abuso (sin fuente; PROPUESTO) | CA-P6-29 a 33 | API: trampa, 413, límites compartidos, duplicados por origen |
| Esquema (migración 015) | CA-P6-77 a 80 | MAN con respaldo; API contra la base de pruebas |
| Prueba manual | — | MAN: con la 015 aplicada, la responsable pide un crédito desde la portada y desde una ficha, registra una búsqueda (sin cuenta y con comprador), comprueba que un asesor no los ve, como administradora los anota, relaciona un vehículo, los cierra, y como comprador revisa "Mis solicitudes" |

---

## 16. Decisiones de negocio abiertas (registradas en DECISIONES.md como PENDIENTE)

- **D-54** — Alcance de P6 frente a D-45: solicitud de contacto con seguimiento mínimo (recomendada A) o flujo de gestión de crédito. Recoge C-1.
- **D-55** — Datos del formulario de crédito: monto y cuota inicial opcionales, sin ingresos (recomendada A).
- **D-56** — Búsqueda: (a) campos; (b) si el administrador relaciona vehículos y el comprador los ve; (c) coincidencias automáticas; (d) ubicación en el portal. Recoge C-5.
- **D-57** — Estados (D-4 frente al modelo ABIERTO/CERRADO) y resultados de cierre. Recoge C-2.
- **D-58** — Reasignación de créditos y búsquedas a un asesor, y si el asesor ve los créditos pedidos desde sus vehículos (D-7 frente a D-52).
- **D-59** — Detalle de "Mis solicitudes": qué solicitudes entran y qué se muestra.

Decisiones que **no** se abren porque ya están tomadas:
- Si pedir crédito o registrar una búsqueda exige cuenta: **no**. D-9 dice "las solicitudes no exigen usuario registrado" y D-3 dice que sin cuenta se contacta.
- Si "Mis solicitudes" incluye los contactos de P5: **sí**. D-53 dice "con avisos, contactos, créditos y búsquedas juntos".
