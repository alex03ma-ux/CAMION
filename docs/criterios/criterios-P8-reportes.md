# Criterios de aceptación P8: reportes, marketing e integraciones

Ítem del backlog: **P8 "Reportes, marketing e integraciones"**. Criterio de salida (BACKLOG.md y minuta §11): "Indicadores e integraciones aprobadas; pruebas y operación".
Rama: `feature/p8-reportes`, creada desde `main` (con P0 a P7, PR #14).
Autor: analista funcional. Fecha: 2026-11-13. Estado del documento: **decisiones D-68 a D-77 TOMADAS** por la responsable el 2026-11-13 ("Apruebo todas", con la opción recomendada) y **D-78** tomada por el coordinador (técnica). Los criterios que estaban marcados PENDIENTE D-n pasan a **CONFIRMADO (D-n opción)**; el texto de cada criterio no cambia porque ya estaba escrito con la opción recomendada. Sigue abierta **D-79** (recuperación de un administrador), que no cambia ningún criterio de esta lista.
Actualizado por el documentador el 2026-11-13.

Fuentes leídas:
- `docs/requisitos/requisitos-funcionales-v3.0.md`: §1.5, §4.1 ("Consultar reportes"), §5.3 (Notificaciones: correo, WhatsApp), matriz §7 (HU-052 a HU-055, HU-061 a HU-063, HU-070 a HU-080, HU-085 a HU-087), §9.8 (RN-NO-001 a 005), §9.9 (RN-RE-001 a 006), §9.10 (RN-PA-005 a 007), §14 (integraciones), §15 y §16.
- `docs/requisitos/minuta-transferencia-agentes-ia.md` §5 (M08, M12, M13), §8 (integraciones "contempladas como posibilidades, NO contratadas") y §10 a §12.
- `docs/coordinacion/DECISIONES.md` completo (en especial D-1, D-5, D-7, D-39, D-44, D-45, D-48, D-52, D-53, D-57, D-58, D-61 a D-67).
- `docs/coordinacion/ESTADO-REAL.md` (hasta la actualización P7) y `docs/coordinacion/BACKLOG.md`.
- `docs/criterios/criterios-P7-ventas-admin.md`.
- Código en `main` (lectura directa del analista, **no** es una auditoría del `auditor-codigo`): `Endpoints/PanelEndpoints.cs`, `Endpoints/VentasListaEndpoints.cs`, `Auth/Auditoria.cs`, `Models/AuditoriaEvento.cs`, `Models/Lead.cs`, `Models/Publicacion.cs`, `Models/Pago.cs`, `Models/Suscripcion.cs`, `Models/VentaVehiculo.cs`, `Models/Vehiculo.cs`, `Camion.Api.csproj`, `Camion.Api.Tests/SesionTests.cs` (inventario de rutas), `frontend/src/components/AdminInicio.tsx` y `frontend/package.json`.

Aviso sobre `ESTADO-REAL.md`: la sección P7.1 decía que la migración 016 estaba "NO APLICADA", pero el commit `f35b0bd` y D-67 dicen que se aplicó con respaldo. Se toma como vigente lo segundo. **Corregido** por el documentador el 2026-11-13 ("Actualización P7" de `ESTADO-REAL.md`).

Numeración: matriz de 87 HU (D-1). En ella HU-063 = Recuperar contraseña, HU-070 = Configurar notificaciones, HU-071 = Notificación nuevo lead, HU-072 = Notificación solicitud de crédito, HU-073 = Notificación vencimiento publicación, HU-074 = Notificación vencimiento suscripción, HU-075 a HU-080 = Reportes, HU-087 = Configuración comercial (promociones).

Marcas:
- **CONFIRMADO**: sale literalmente de una HU, RN, de la minuta o de una decisión TOMADA.
- **PROPUESTO**: lo infiere el analista; necesita visto bueno del coordinador o de la responsable.
- **PENDIENTE D-n**: dependía de una decisión abierta. Ya no queda ninguno: D-68 a D-77 están TOMADAS y sus criterios figuran como CONFIRMADO (D-n opción).
- **EXISTE**: ya está en `main`; en P8 queda como regresión.

Cómo se prueba: API = prueba de integración; COMP = prueba de componente del frontend; MAN = prueba manual de la responsable.
Rutas, nombres de campos y textos son orientativos; el contrato lo fija el arquitecto (`docs/criterios/P8-contrato-api.md`).

---

## 1. Qué pide la v3.0 / qué existe / qué falta

| Tema | Qué pide la v3.0 y la minuta | Qué existe en `main` | Qué falta | Tipo |
|---|---|---|---|---|
| Dashboard general (HU-075, RN-RE-001 "Indicadores consolidados") | Indicadores consolidados para el administrador | Panel "Inicio" con contadores operativos sin dinero ni gráficos (`GET /api/admin/panel`, `AdminInicio.tsx`; D-64 A) | Indicadores del período con gráficos simples (D-69) | (1) interno |
| Reporte de vehículos (HU-076, RN-RE-002 "Filtrable") | Estadísticas de vehículos, filtrables | Inventario con filtro por estado; contador por estado en el panel | Reporte agregado por estado, categoría, asesor, ciudad y fecha; publicaciones por estado | (1) |
| Reporte de leads (HU-077, RN-RE-003 "Filtrable") | Estadísticas de leads | Bandeja `/api/leads` con filtros de estado, responsable y origen; `resultado_cierre`, `cerrado_en` y `eventos_lead` guardados; `GET /api/leads/resumen` | Reporte agregado por origen, resultado, responsable y fecha | (1) |
| Reporte de créditos (HU-078, RN-RE-004 "Filtrable") | Estadísticas de créditos | Créditos son leads `origen = CREDITO` con resultados de D-57 b | Reporte de créditos por estado mostrado (D-57 a) y resultado | (1) |
| Exportar (HU-079, RN-RE-005 "Excel/PDF") | Exportar reportes en Excel o PDF | Nada. Ni el backend ni el frontend tienen librerías de Excel, PDF ni gráficos (`Camion.Api.csproj`, `package.json`) | Exportación (formato según D-71) | (1) |
| Indicadores comerciales (HU-080, RN-RE-006 "Dashboard") | Métricas comerciales | Ventas con origen de lista fija (D-39) y precio final; pagos del plan registrados (D-61 B, D-67) | Valor vendido, ventas por origen, conversión de contactos, ingresos registrados por planes (D-69) | (1) |
| Minuta M13 | "Indicadores de inventario, contactos, publicaciones, ventas y créditos" | Ver filas anteriores | Ventas y publicaciones no tienen HU propia en la matriz; se cubren en HU-076 y HU-080 (D-68) | (1) |
| Reportes del asesor | La v3.0 asigna todo el módulo al **Administrador** (§7, HU-075 a HU-080) | "Mis ventas", "Mis publicaciones", "Mis contactos", "Mi suscripción" (listas, sin agregados) | Decidir si ve indicadores propios (D-70) | (1) |
| Auditoría (HU-061, RN-SE-003) | Registrar eventos relevantes; trazabilidad | `auditoria_eventos` se llena en todas las acciones; ninguna ruta la muestra; `SesionTests` (CA-061-15) prohíbe rutas con "auditoria" o "historial" | Línea de tiempo de solo lectura (D-62 d la dejó para P8; D-72) | (1) |
| Notificaciones (HU-070 a HU-074, RN-NO-001 a 005) | Preferencias; avisos de lead "Tiempo real", crédito, vencimientos "Preventiva"; v3.0 §5.3: correo y WhatsApp | Solo dentro del panel: "Contactos (N nuevos)" (D-48), créditos en la bandeja, aviso "Tu plan vence en N días" y contadores (D-64 b) | Correo o WhatsApp automáticos: requieren un servicio externo (D-73, D-74). HU-070 sin canal externo no tiene objeto | (2) externo |
| Recuperar contraseña (HU-063, RN-US-006 "Enlace temporal") | Restablecer contraseña con enlace temporal | Nada (BLOQUEADO por D-5: no hay correo) | Canal para entregar el enlace (D-73) | (2) o alternativa interna |
| Contenido y marketing (HU-052 a HU-055, RN-MKT-001, RN-CA-012; M08 "Banners, contenidos y promociones") | Crear, editar, publicar y consultar contenido | Solo la foto de portada administrable (D-42) | Contenido o promociones (D-75) | (1) interno, con revisión legal de promociones |
| Configuración comercial (HU-087, RN-PA-007 "promociones y parámetros") | Promociones | Nada; D-63 la pasó a P8 | D-75 | (1) |
| Información corporativa y textos legales (HU-085, HU-086) | Administrar | Borradores en `docs/legal/`, sin publicar | BLOQUEADO por D-44 (D-63 B) | Bloqueado |
| Pasarela de pagos (§14) | Integración posible | Registro interno de pagos (D-61 B, D-67) | Nada: D-61 D descartada por ahora | (2), sin decisión nueva |
| Google Analytics (§14) | Integración posible | Nada | D-76 | (2) |
| Redes sociales, YouTube/TikTok (§14) | Integraciones posibles | Enlaces de Instagram y TikTok administrables, ocultos si están vacíos (D-63 A) | D-77 | (2) |
| Publicidad | No la nombra la v3.0; la minuta habla de "promoción" de vehículos | Destacados por plan (D-38) | D-77 | (2) |

---

## 2. HU de P8 y su estado

| HU | Nombre | Estado real | Entrega propuesta |
|---|---|---|---|
| HU-075 | Dashboard general | PARCIAL (contadores de P7) | P8 mínimo |
| HU-076 | Reporte de vehículos | NO INICIADO | P8 mínimo |
| HU-077 | Reporte de leads | NO INICIADO | P8 mínimo |
| HU-078 | Reporte de créditos | NO INICIADO | P8 mínimo |
| HU-079 | Exportar reportes | NO INICIADO | P8 mínimo (formato D-71) |
| HU-080 | Indicadores comerciales | NO INICIADO | P8 mínimo |
| HU-061 | Registrar auditoría (consulta) | Registro EXISTE; consulta NO INICIADA | P8 mínimo (D-72) |
| HU-087 | Configuración comercial (promociones) | NO INICIADO | Fuera del mínimo (D-75) |
| HU-052 a HU-055 | Contenido y marketing | NO INICIADO (salvo portada) | Fuera del mínimo (D-75) |
| HU-063 | Recuperar contraseña | BLOQUEADO (D-5) al escribir estos criterios; con D-73 D pasa a recuperación asistida por el administrador (IMPLEMENTADO y PROBADO en la rama, ver `P8-evidencia.md`) | P8 (D-73 D, D-78); administradores: D-79 PENDIENTE |
| HU-070 | Configurar notificaciones | NO INICIADO | Según D-73/D-74 |
| HU-071 a HU-074 | Notificaciones | PARCIAL (solo dentro del panel: D-48, P6, D-64 b) | Externas según D-73/D-74 |
| HU-085, HU-086 | Información corporativa, textos legales | BLOQUEADO (D-44) | Cuando se libere D-44 |

---

## 3. Contradicciones y choques detectados

| # | Fuentes | Qué choca | Tratamiento |
|---|---|---|---|
| C-1 | RN-RE-005 "Excel/PDF" frente al encargo "CSV o Excel" | Un CSV se abre en Excel pero no es un archivo Excel; PDF exige otra librería | **D-71 (a)** |
| C-2 | v3.0 §7: HU-075 a HU-080 tienen actor **Administrador**; minuta §4: el asesor gestiona "operaciones comerciales" | Indicadores propios del asesor no están en la línea base | **D-70** |
| C-3 | HU-079 (exportar) frente a RN-SE-004, D-44 y D-45 ("datos internos") | Un archivo con nombres y teléfonos de clientes sale del sistema sin control | **D-71 (b)** |
| C-4 | D-62 d (línea de tiempo en P8) frente a `SesionTests` CA-061-15, que prohíbe rutas con "auditoria" o "historial" | La prueba busca impedir rutas de edición; una ruta de solo lectura con ese nombre la rompería | Técnico, en **D-72**: usar un nombre sin esas palabras (p. ej. `/eventos`) y mantener la prueba |
| C-5 | D-64 a A (panel sin sumas de dinero) frente a HU-080 "métricas comerciales" | Si se muestran valores en pesos | **D-69 (b)** |
| C-6 | v3.0 §5.3 y RN-NO-002 "Tiempo real" frente a D-5 y D-48 (sin servicio de correo) | Avisos externos imposibles sin proveedor | **D-73, D-74** |
| C-7 | RN-US-006 "Enlace temporal" frente a D-5 | Sin correo no hay cómo entregar el enlace | **D-73** (incluye una alternativa sin servicio externo) |
| C-8 | Minuta M08 "promociones comerciales" y RN-PA-007 frente a D-42 ("no tan recargado") y D-45 | Promociones con precios o descuentos son publicidad sujeta a la protección al consumidor | **D-75** (revisión del abogado, D-44) |
| C-9 | Indicador "visitas por publicación" (típico de HU-080) | No existe ningún dato de visitas; medirlas es analítica y puede implicar cookies | **D-76** |

---

## 4. Alcance propuesto para P8 (PROPUESTO)

### 4.1 Alcance mínimo entregable, sin servicios externos (grupo 1)

1. **Reportes filtrables del administrador** (HU-076 a HU-078, M13; D-68): vehículos y publicaciones; solicitudes (contactos, avisos, créditos y búsquedas) por origen, estado y resultado; créditos; ventas; suscripciones y pagos registrados. Filtros comunes: rango de fechas, asesor, categoría y los propios de cada reporte. Solo agregados y filas sin datos de clientes.
2. **Indicadores y gráficos simples** (HU-075 resto, HU-080; D-69): sección "Indicadores" con período elegible, valores del período y barras simples, cada gráfico con su tabla equivalente.
3. **Exportación CSV** (HU-079; D-71): cada reporte se descarga en CSV que abre en Excel, sin datos personales de clientes, auditada.
4. **Indicadores del asesor sobre lo suyo** (D-70): en pantalla, sin exportar.
5. **Línea de tiempo de auditoría** (HU-061, D-62 d; D-72): de un vehículo y de una suscripción, solo administrador, solo lectura.
6. **Operación** (criterio de salida "pruebas y operación"): regresión completa de P1 a P7 y guía de uso de reportes.

Sin tablas nuevas: todo sale de `vehiculos`, `publicaciones`, `leads`, `eventos_lead`, `ventas_vehiculo`, `suscripciones`, `planes`, `pagos` y `auditoria_eventos`.

### 4.2 Lo que requiere un servicio externo con costo, contrato o datos personales (grupo 2): solo decisiones

| Integración | Qué habilita | Qué exige | Decisión |
|---|---|---|---|
| Correo transaccional | HU-063, aviso de aprobación del asesor (D-5), HU-070 a HU-074 por correo | Proveedor y cuenta, dominio propio con SPF/DKIM, costo por volumen, encargado del tratamiento en la política (D-44), secretos fuera del repositorio | D-73 |
| WhatsApp Business API | Avisos automáticos por WhatsApp | Proveedor (Meta o un BSP), número verificado, plantillas aprobadas, costo por conversación, autorización previa del titular | D-74 |
| Pasarela de pagos | Cobro del plan en línea | Contrato, comisiones, factura electrónica | D-61 D, descartada por ahora; sin decisión nueva |
| Analítica web | Visitas y comportamiento | Cookies, aviso de cookies, transferencia internacional, cambio de política (D-44) | D-76 |
| Publicidad y redes sociales | Píxeles, campañas, publicación automática | Cuentas, cookies de terceros, revisión de aplicaciones de las plataformas | D-77 |

Observación común: el portal todavía **no está publicado en internet** (INF-2 y D-44 pendientes). Las integraciones del grupo 2 no tienen tráfico ni usuarios reales que atender hasta entonces.

### 4.3 Fuera del mínimo de P8

Promociones y contenido de marketing (HU-087, HU-052 a HU-055; D-75), HU-085 y HU-086 (D-44), todas las integraciones del grupo 2 salvo que la responsable apruebe alguna, factura electrónica, contabilidad, exportación a PDF (D-71).

---

## 5. Criterios de aceptación

Convenciones PROPUESTAS para todo P8:
- Las fechas de los filtros son días calendario de Colombia (`aaaa-mm-dd`, como `GET /api/ventas`). Sin filtros, el período por defecto es el mes en curso.
- Rango máximo de 366 días por consulta.
- Los valores en pesos son enteros, sin decimales.

### 5.1 Reglas comunes a todos los reportes (RN-RE-002 a 004 "Filtrable", RN-SE-006)

| ID | Criterio (Dado / Cuando / Entonces) | Marca | Prueba |
|---|---|---|---|
| CA-P8-01 | **Dado** un administrador con sesión, **cuando** abre "Reportes" (`#/cuenta/reportes`), **entonces** ve la lista de reportes de D-68 (Vehículos y publicaciones, Solicitudes, Créditos, Ventas, Suscripciones y pagos) y puede elegir uno, fijar filtros y ver el resultado en pantalla. | CONFIRMADO (v3.0 §4.1 "Consultar reportes") + CONFIRMADO (D-68 A, lista) | COMP |
| CA-P8-02 | **Dado** un reporte sin filtros, **cuando** se consulta, **entonces** usa el mes en curso y la pantalla dice el período aplicado ("Del 2026-11-01 al 2026-11-30"). | PROPUESTO | API, COMP |
| CA-P8-03 | **Dado** filtros válidos (`desde`, `hasta`, `asesorId`, `categoria` y los propios del reporte), **cuando** se consulta, **entonces** 200 y solo cuenta lo que cumple todos los filtros a la vez. | CONFIRMADO (RN-RE-002 a 004 "Filtrable") + PROPUESTO (filtros concretos, D-68) | API |
| CA-P8-04 | **Dado** un filtro inválido (fecha mal escrita, `desde` posterior a `hasta`, rango de más de 366 días, asesor, categoría, origen, estado o resultado que no existen), **cuando** se consulta, **entonces** 400 `{ error, errores }` con el campo y un mensaje en español, p. ej. "La fecha inicial no puede ser posterior a la final." o "El período no puede superar 366 días.", y la pantalla lo muestra junto al campo. | PROPUESTO (mismo patrón que CA-P7-04) | API, COMP |
| CA-P8-05 | **Dado** un período sin datos, **cuando** se consulta, **entonces** 200 con ceros (no 404) y la pantalla dice "No hay datos para estos filtros." con el botón "Limpiar filtros". | PROPUESTO | API, COMP |
| CA-P8-06 | **Dado** un asesor o comprador con sesión, **cuando** llama a cualquier ruta de reportes del administrador, **entonces** 403 "No tienes permiso para realizar esta acción."; **sin sesión**, 401; la opción "Reportes" no aparece en su navegación. | CONFIRMADO (v3.0 §7 actor Administrador; RN-SE-006; D-3) | API, COMP |
| CA-P8-07 | **Dado** cualquier reporte del administrador, **entonces** no incluye nombre, teléfono, correo ni mensaje de ningún cliente o visitante; solo agregados y, en las filas, identificadores, fechas, estados, origen, resultado, vehículo y asesor responsable. | CONFIRMADO (RN-SE-004) + CONFIRMADO (D-71 b A) | API |
| CA-P8-08 | **Dado** cualquier reporte, **entonces** es de solo lectura: no hay rutas de escritura y consultarlo no cambia ningún dato. Excepción: consultar contactos ejecuta antes el barrido de D-46, igual que `GET /api/admin/panel`, para que las cifras coincidan. | PROPUESTO | API |
| CA-P8-09 | **Dado** el mismo filtro, **cuando** se compara una cifra del reporte con la lista operativa correspondiente (inventario, bandeja de contactos, `GET /api/ventas`, suscripciones), **entonces** coinciden. | PROPUESTO (coherencia, como CA-P7-46) | API |
| CA-P8-10 | **Dado** un token de sesión cerrada o expirada, **cuando** se consulta un reporte, **entonces** 401 y el frontend vuelve al inicio de sesión. | CONFIRMADO (D-12) | API, COMP |

### 5.2 Vehículos y publicaciones (HU-076, RN-RE-002; M13)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P8-11 | **Dado** el reporte de vehículos, **cuando** se consulta, **entonces** muestra el inventario actual por estado (Activo, Reservado, Inactivo, Vendido; D-11, D-4) y por categoría (Carro, Camión, Carrocería, Maquinaria; D-17), y los vehículos registrados en el período. | CONFIRMADO (HU-076 "Estadísticas de vehículos") + PROPUESTO (cortes) | API |
| CA-P8-12 | **Dado** el filtro `asesorId`, `categoria` o `departamentoId`/`municipioId` (DIVIPOLA, D-29), **cuando** se consulta, **entonces** las cifras se limitan a ese corte. | CONFIRMADO (D-68 A) | API |
| CA-P8-13 | **Dado** el reporte, **entonces** muestra las publicaciones por estado de publicación, cuántas están visibles en el portal hoy (misma regla que `ConsultasCatalogo.Visibles`, D-41) y cuántas están destacadas. | PROPUESTO (M13 "publicaciones") | API |
| CA-P8-14 | **Dado** publicaciones ELIMINADAS (borrado lógico, D-40), **entonces** se cuentan aparte y no suman en "visibles". | PROPUESTO | API |
| CA-P8-15 | **Dado** el reporte, **entonces** muestra los 10 vehículos con más contactos en el período (título del vehículo y número de contactos), sin datos de quienes contactaron. | CONFIRMADO (D-68 A) | API |
| CA-P8-16 | **Dado** un vehículo DEMO o anterior a P4b sin fecha de publicación, **entonces** se cuenta en el inventario por estado y no rompe el reporte (sin 500). | PROPUESTO | API |

### 5.3 Solicitudes: contactos, avisos, créditos y búsquedas (HU-077, RN-RE-003; D-39, D-57)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P8-17 | **Dado** el reporte de solicitudes, **cuando** se consulta, **entonces** muestra, para el período (fecha de creación), cuántas se recibieron por **origen** (Contacto de vehículo, "Vende tu camión", Seguro todo riesgo, Crédito, Búsqueda), cuántas siguen abiertas y cuántas se cerraron. | CONFIRMADO (HU-077 "Estadísticas de leads") + PROPUESTO (cortes) | API |
| CA-P8-18 | **Dado** las solicitudes cerradas en el período (fecha de cierre), **entonces** se muestran por **resultado de cierre**, con los nombres que ve el administrador ("Venta concretada", "Solicitud atendida", "Remitido a entidad financiera", "Encontró vehículo"…), cada resultado solo en los orígenes donde es válido (`Lead.ResultadosDe`). | CONFIRMADO (CA-P5-39 "sirven al reporte HU-077"; D-57 b) | API |
| CA-P8-19 | **Dado** los contactos de vehículo, **entonces** el reporte separa los atendidos por Administración y por cada asesor (responsable actual) y cuántos fueron reasignados a mano o pasaron solos a Administración por el barrido de D-46. | PROPUESTO (D-7, D-46) | API |
| CA-P8-20 | **Dado** los filtros `origen`, `estado` (ABIERTO/CERRADO), `resultado` y `responsable` (ADMINISTRACION o un asesor), **cuando** se combinan, **entonces** las cifras se limitan a todos a la vez; un resultado que no corresponde al origen elegido da 400. | CONFIRMADO (D-68 A) | API |
| CA-P8-21 | **Dado** el reporte, **entonces** muestra el tiempo medio, en días, entre la creación y el primer cierre de las solicitudes cerradas en el período, por origen. | CONFIRMADO (D-69 b A) | API |
| CA-P8-22 | **Dado** una solicitud reabierta y cerrada de nuevo, **entonces** cuenta una sola vez, con su último resultado. | PROPUESTO | API |
| CA-P8-23 | **Dado** el reporte, **entonces** nunca muestra nombre, teléfono, correo, mensaje, notas ni observación de cierre (texto libre que puede tener datos personales). | CONFIRMADO (RN-SE-004; CA-P5-59) + PROPUESTO (observación) | API |

### 5.4 Créditos (HU-078, RN-RE-004; D-54, D-57, D-58)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P8-24 | **Dado** el reporte de créditos, **cuando** se consulta, **entonces** muestra las solicitudes de crédito del período por estado mostrado (Registrado, En gestión, Cerrado; D-57 a A) y si se pidieron desde la portada o desde la ficha de un vehículo. | CONFIRMADO (HU-078; D-57 a A) | API |
| CA-P8-25 | **Dado** los créditos cerrados, **entonces** se muestran por resultado ("Remitido a entidad financiera", "Solicitud atendida", "No le interesó", "No respondió", "Contacto duplicado o inválido", "Otro") y el porcentaje remitido sobre el total cerrado. | CONFIRMADO (D-57 b A) + PROPUESTO (porcentaje) | API |
| CA-P8-26 | **Dado** el reporte, **entonces** no hay ninguna cifra de aprobación o negación del banco, montos, ingresos ni entidad financiera (el portal no los guarda). | CONFIRMADO (D-45, D-55 B, D-57 b A) | API |
| CA-P8-27 | **Dado** un asesor, **entonces** no ve el reporte de créditos ni cifras de créditos en sus indicadores, tampoco de los pedidos desde sus vehículos. | CONFIRMADO (D-58 A) | API |

### 5.5 Ventas, suscripciones y pagos (HU-080, M13; D-39, D-61 B, D-67)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P8-28 | **Dado** el reporte de ventas, **cuando** se consulta, **entonces** muestra las ventas **vigentes** del período (fecha de venta) por origen (Portal web, Contacto directo del asesor, Referido, Otro; D-39), por categoría y por asesor, con cantidad y valor vendido (suma del precio final). | CONFIRMADO (D-39 "sirve para los reportes de P8") + CONFIRMADO (D-69 b A, valor) | API |
| CA-P8-29 | **Dado** ventas revertidas en el período, **entonces** se cuentan aparte ("Revertidas: N") y no suman en cantidad ni valor. | PROPUESTO (D-62) | API |
| CA-P8-30 | **Dado** el reporte de suscripciones, **entonces** muestra las suscripciones por estado (Activa, Suspendida, Vencida, Cancelada…), las asignadas, renovadas y vencidas en el período, y por plan. | CONFIRMADO (D-68 A) | API |
| CA-P8-31 | **Dado** los pagos registrados en el período (fecha de pago), **entonces** se muestra el total recibido por medio (Transferencia, Consignación, Efectivo, Otro), por plan y por concepto, sin contar los anulados; los anulados se cuentan aparte. | CONFIRMADO (D-61 B: "no cuenta los anulados") + CONFIRMADO (D-68 A) | API |
| CA-P8-32 | **Dado** un pago antiguo sin fecha de pago, **entonces** se agrupa por su fecha de registro y se marca "sin fecha de pago"; no rompe el reporte. | PROPUESTO (D-66) | API |
| CA-P8-33 | **Dado** el reporte de pagos, **entonces** dice de forma visible "Pagos registrados a mano por la administración. No es contabilidad ni facturación." | CONFIRMADO (D-61 B: "sin factura ni contabilidad") + PROPUESTO (texto) | COMP |

### 5.6 Indicadores y gráficos (HU-075, HU-080, RN-RE-001, RN-RE-006)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P8-34 | **Dado** el administrador, **cuando** abre "Indicadores" (`#/cuenta/indicadores`), **entonces** ve para el período elegido: contactos recibidos, porcentaje de contactos cerrados con "Venta concretada", ventas vigentes y valor vendido, solicitudes de Administración recibidas por origen, créditos remitidos, publicaciones visibles hoy e ingresos registrados por planes. | CONFIRMADO (RN-RE-006 "Métricas comerciales") + CONFIRMADO (D-69 b A) | API, COMP |
| CA-P8-35 | **Dado** el período elegido, **entonces** cada indicador muestra también el valor del período anterior de igual duración y la diferencia ("+3 frente al período anterior"). | CONFIRMADO (D-69 b A) | API, COMP |
| CA-P8-36 | **Dado** los indicadores, **entonces** hay como máximo cuatro gráficos simples: ventas por mes (últimos 12 meses), ventas por origen, solicitudes por origen y contactos por resultado. Son barras dibujadas sin librería externa. | CONFIRMADO (D-69 a A) | COMP |
| CA-P8-37 | **Dado** cada gráfico, **entonces** tiene título, valores escritos sobre cada barra y una tabla equivalente accesible para lector de pantalla; no depende solo del color. | PROPUESTO (minuta §12 "accesibilidad básica") | COMP |
| CA-P8-38 | **Dado** un denominador 0 (por ejemplo ningún contacto cerrado), **entonces** el porcentaje se muestra como "Sin datos", no como 0 % ni como error. | PROPUESTO | API, COMP |
| CA-P8-39 | **Dado** el panel "Inicio" de P7, **entonces** sigue igual (contadores operativos sin dinero, CA-P7-45 a 51) y ofrece un enlace "Ver indicadores". | CONFIRMADO (D-64 a A) · EXISTE (regresión) | COMP |
| CA-P8-40 | **Dado** un asesor o comprador, **cuando** pide los indicadores del administrador, **entonces** 403; sin sesión, 401. | CONFIRMADO (RN-SE-006) | API |
| CA-P8-41 | **Dado** el panel de indicadores, **entonces** no hay visitas a publicaciones, tasas de rebote ni datos de navegación (no existen; D-76). | CONFIRMADO (D-76 A) | COMP |

### 5.7 Exportación (HU-079, RN-RE-005; D-44, D-71)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P8-42 | **Dado** un reporte en pantalla, **cuando** el administrador pulsa "Descargar CSV", **entonces** recibe un archivo con los mismos filtros y las mismas cifras que ve, con `Content-Disposition: attachment` y nombre `reporte-<tipo>-<desde>_<hasta>.csv`. | CONFIRMADO (HU-079) + CONFIRMADO (D-71 a A, formato) | API, COMP |
| CA-P8-43 | **Dado** el CSV, **entonces** está en UTF-8 con BOM, usa `;` como separador, fechas `aaaa-mm-dd`, valores enteros sin separador de miles, y abre en Excel en español con tildes y eñes correctas. | CONFIRMADO (D-71 a A) | API, MAN |
| CA-P8-44 | **Dado** una celda que empieza por `=`, `+`, `-`, `@`, tabulador o retorno, **entonces** se exporta precedida de un apóstrofo para que Excel no la ejecute como fórmula. | PROPUESTO (seguridad: inyección de fórmulas) | API |
| CA-P8-45 | **Dado** cualquier exportación, **entonces** no contiene nombre, teléfono, correo, mensaje, notas ni observaciones de clientes o visitantes. | CONFIRMADO (RN-SE-004) + CONFIRMADO (D-71 b A) | API |
| CA-P8-46 | **Dado** una exportación, **entonces** se audita en `auditoria_eventos` (entidad `Reporte`, acción `EXPORTAR`) con quién, cuándo, tipo de reporte, filtros y número de filas, sin el contenido. | CONFIRMADO (RN-SE-003) + PROPUESTO (entidad y acción) | API |
| CA-P8-47 | **Dado** una exportación de más de 10.000 filas, **entonces** 400 "El reporte tiene demasiadas filas. Acorta el período o añade filtros."; nunca se genera un archivo parcial. | PROPUESTO | API |
| CA-P8-48 | **Dado** filtros inválidos en la exportación, **entonces** 400 con el mismo mensaje que la consulta en pantalla (CA-P8-04). | PROPUESTO | API |
| CA-P8-49 | **Dado** un asesor, comprador o visitante, **cuando** pide una exportación, **entonces** 403 o 401. | CONFIRMADO (v3.0 §7 actor Administrador) + CONFIRMADO (D-70 A, el asesor no exporta) | API |
| CA-P8-50 | **Dado** la respuesta de una exportación, **entonces** lleva `Cache-Control: no-store` y no queda guardada en el servidor ni en `C:\CAMION_media`. | PROPUESTO | API |

### 5.8 Indicadores del asesor sobre lo suyo (D-70)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P8-51 | **Dado** un asesor aprobado, **cuando** abre "Mis indicadores" (`#/mis-indicadores`), **entonces** ve, para el período elegido: sus vehículos por estado, sus publicaciones visibles hoy, los contactos recibidos y su resultado de cierre, y sus ventas vigentes con cantidad y valor. | CONFIRMADO (D-70 A) | API, COMP |
| CA-P8-52 | **Dado** un asesor, **entonces** las cifras incluyen solo los vehículos que hoy son suyos y los contactos de los que es responsable (mismo alcance que su inventario, "Mis contactos" y "Mis ventas"); el filtro `asesorId` se ignora. | CONFIRMADO (D-62 c A, §15 "solo sus propios vehículos") + CONFIRMADO (D-70 A) | API |
| CA-P8-53 | **Dado** un asesor, **entonces** no ve avisos, créditos, búsquedas (D-52, D-58), pagos (D-61 B), cifras de otros asesores, totales del portal ni comparaciones o posiciones frente a otros. | CONFIRMADO (D-52 A, D-58 A, D-61 B) + PROPUESTO (sin comparaciones) | API |
| CA-P8-54 | **Dado** un asesor, **entonces** "Mis indicadores" no ofrece exportar. | CONFIRMADO (D-70 A) | COMP, API |
| CA-P8-55 | **Dado** un asesor pendiente, rechazado o bloqueado, o un comprador, **cuando** pide "Mis indicadores", **entonces** 403 (o 401 si no puede iniciar sesión). | CONFIRMADO (D-3, D-10) | API |

### 5.9 Línea de tiempo de auditoría (HU-061, RN-SE-003, D-62 d; D-72)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P8-56 | **Dado** el administrador en el detalle de un vehículo, **cuando** abre "Actividad", **entonces** ve, de la más reciente a la más antigua, los eventos del vehículo, de sus publicaciones y de sus ventas: fecha y hora de Colombia, quién (nombre, o "Sistema"), acción en palabras ("Marcó vendido", "Reservó", "Reasignó de Ana a Luis") y el motivo cuando existe. | CONFIRMADO (D-62 d: "se consulta en P8") + CONFIRMADO (D-72 a A) | API, COMP |
| CA-P8-57 | **Dado** el administrador en una suscripción, **cuando** abre "Actividad", **entonces** ve la asignación, renovaciones, cambios de plan, suspensiones, reactivaciones, cancelación, pagos registrados y anulados, y los deshechos por D-67, con quién y cuándo. | CONFIRMADO (D-72 a A) | API, COMP |
| CA-P8-58 | **Dado** un evento EDITAR, **entonces** se muestran solo los campos que cambiaron, con su valor anterior y nuevo en palabras; nunca contraseñas, tokens, documentos completos ni datos de clientes. | CONFIRMADO (`Auditoria.cs`: "nunca contraseñas, tokens ni el documento completo") + PROPUESTO (presentación) | API |
| CA-P8-59 | **Dado** la línea de tiempo, **entonces** es de solo lectura: no hay ninguna ruta para editar o borrar eventos, y la ruta de lectura es GET, bajo `/api/admin` con `SoloAdministrador`, y su nombre no contiene "auditoria" ni "historial", para que la prueba CA-061-15 de `SesionTests` siga vigente sin excepciones. | CONFIRMADO (CA-061-15) + PROPUESTO (nombre `/eventos`) | API |
| CA-P8-60 | **Dado** un asesor o comprador, **cuando** pide la línea de tiempo, **entonces** 403; sin sesión, 401; el asesor no ve la sección "Actividad" en sus vehículos. | CONFIRMADO (D-72 b A) | API, COMP |
| CA-P8-61 | **Dado** un vehículo inexistente o una suscripción inexistente, **entonces** 404 "Vehículo no encontrado." / "Suscripción no encontrada.". | PROPUESTO | API |
| CA-P8-62 | **Dado** más de 50 eventos, **entonces** la lista se pagina (20 por página por defecto, máximo 100). | PROPUESTO | API |

### 5.10 Integraciones externas: lo que no debe ocurrir sin decisión (grupo 2)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P8-63 | **Dado** el estado actual de las decisiones, **entonces** el sistema no envía correos ni mensajes de WhatsApp automáticos; los avisos siguen solo dentro del panel (D-48, D-64 b). | CONFIRMADO (D-5, D-48; minuta §8 "NO contratadas") + CONFIRMADO (D-73 D, D-74 A) | API (sin dependencias de envío), MAN |
| CA-P8-64 | **Dado** el portal público, **entonces** no carga scripts, píxeles ni cookies de terceros (analítica, publicidad o redes sociales) y no muestra aviso de cookies. | CONFIRMADO (minuta §8) + CONFIRMADO (D-76 A, D-77 A) | COMP (inventario de `index.html` y dependencias), MAN |
| CA-P8-65 | **Dado** el portal, **entonces** no hay pasarela ni ruta que reciba datos de tarjeta o cuentas (regresión de CA-P7-33). | CONFIRMADO (D-61 B) · EXISTE | API |
| CA-P8-66 | **Dado** que la responsable aprueba un servicio de correo (D-73 B o C), **entonces** sus credenciales se leen de `dotnet user-secrets` o variables de entorno, nunca del repositorio, y la política de datos nombra al proveedor como encargado antes de activarlo. | CONFIRMADO (CLAUDE.md; D-44) | MAN, revisión de seguridad |
| CA-P8-67 | **Dado** que la responsable elige D-73 D (recuperación asistida), **cuando** el administrador genera un enlace de restablecimiento para un usuario, **entonces** el enlace es de un solo uso, vence en 60 minutos, se audita (quién, para quién, cuándo; nunca el enlace), invalida las sesiones del usuario al usarse (D-12) y la contraseña nueva cumple las reglas de P1. Con D-78: el enlace también deja de valer si cambia el estado de la cuenta o sus roles, y al usarlo se comprueba de nuevo que la cuenta esté ACTIVA y no sea de administrador. | CONFIRMADO (D-73 D) + CONFIRMADO (D-78, técnica) | API, COMP |
| CA-P8-68 | **Dado** HU-063, **mientras** D-73 sea A, **entonces** sigue BLOQUEADO y la pantalla de inicio de sesión dice "¿Olvidaste tu contraseña? Escríbenos por WhatsApp" con el WhatsApp del portal (D-63 A). Con D-73 D elegida, HU-063 deja de estar BLOQUEADO (CA-P8-67) y el texto se mantiene para pedir el enlace al administrador. | CONFIRMADO (D-73 D) + PROPUESTO (texto) | COMP |

### 5.11 Transversales: rutas, rendimiento, regresión y operación

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P8-69 | **Dado** cada ruta nueva de P8, **entonces** figura en `SesionTests.Inventario_de_endpoints` con su política: las del administrador bajo `/api/admin` con `SoloAdministrador`; las del asesor con su política de asesor; ninguna anónima nueva; todas GET. | PROPUESTO | API |
| CA-P8-70 | **Dado** una base con 5.000 solicitudes y 1.000 vehículos de prueba, **cuando** se consulta cualquier reporte de un año, **entonces** responde en menos de 3 segundos en el equipo local. | PROPUESTO | API |
| CA-P8-71 | **Dado** el despliegue de P8, **entonces** no se crea, borra ni modifica ninguna fila existente; si no hay migración, no se toca el esquema. | CONFIRMADO (CLAUDE.md, minuta §10) | MAN |
| CA-P8-72 | **Dado** el cierre de P8, **entonces** pasan `dotnet test`, `npx vitest run`, `npx tsc -b` y `npm run build`, sin regresiones de P1 a P7. | CONFIRMADO (minuta §12) | API, COMP |
| CA-P8-73 | **Dado** las pantallas nuevas (Reportes, Indicadores, Mis indicadores, Actividad), **entonces** se usan con teclado y en móvil, los filtros tienen etiqueta, los errores del backend se muestran con su mensaje y la navegación del administrador incluye "Reportes" e "Indicadores" con `aria-current`. | CONFIRMADO (minuta §12; D-31) + PROPUESTO | COMP, MAN |
| CA-P8-74 | **Dado** la prueba manual en `camion_ventas`, **cuando** la responsable consulta cada reporte, cambia filtros, descarga un CSV, lo abre en Excel y revisa la actividad de un vehículo y de una suscripción, **entonces** las cifras coinciden con las listas operativas y con estos criterios. | CONFIRMADO (procedimiento del proyecto) | MAN |
| CA-P8-75 | **Dado** el cierre de P8, **entonces** existe una guía `docs/manual/P8-reportes.md` que explica cada reporte, qué cuenta cada cifra y cómo abrir el CSV en Excel. | CONFIRMADO (criterio de salida "pruebas y operación"; minuta §9 documentador) | MAN |

---

## 6. Trazabilidad HU/RN/D → criterio → prueba prevista

| HU / RN / D | Criterios | Prueba prevista |
|---|---|---|
| HU-075, RN-RE-001, D-64 a | CA-P8-34 a 41 | API, COMP |
| HU-076, RN-RE-002, M13, D-41, D-40 | CA-P8-01 a 16 | API, COMP |
| HU-077, RN-RE-003, CA-P5-39, D-46, D-57 b | CA-P8-17 a 23 | API |
| HU-078, RN-RE-004, D-45, D-55, D-57, D-58 | CA-P8-24 a 27 | API |
| HU-079, RN-RE-005, D-44 | CA-P8-42 a 50 | API, COMP, MAN |
| HU-080, RN-RE-006, D-39, D-61 B, D-66, D-67 | CA-P8-28 a 36 | API, COMP |
| HU-061, RN-SE-003, D-62 d, CA-061-15 | CA-P8-46, 56 a 62 | API, COMP |
| RN-SE-004, D-44 | CA-P8-07, 23, 45, 58 | API |
| RN-SE-006, D-3, D-10 | CA-P8-06, 40, 49, 55, 60, 69 | API |
| D-12 | CA-P8-10 | API, COMP |
| D-68 | CA-P8-01, 03, 12, 15, 20, 30, 31 | API |
| D-69 | CA-P8-21, 28, 34 a 36 | API, COMP |
| D-70 | CA-P8-49, 51 a 55 | API, COMP |
| D-71 | CA-P8-07, 42 a 45 | API |
| D-72 | CA-P8-56 a 62 | API, COMP |
| HU-063, RN-US-006, D-5, D-73 | CA-P8-63, 66 a 68 | API, COMP, MAN |
| HU-070 a HU-074, RN-NO-001 a 005, D-48, D-74 | CA-P8-63 | MAN |
| v3.0 §14, minuta §8, D-61 D, D-76, D-77 | CA-P8-41, 64, 65 | COMP, API, MAN |
| HU-087, HU-052 a HU-055, D-75 | — (fuera del mínimo; criterios cuando se decida) | — |
| HU-085, HU-086 (D-44) | — (BLOQUEADO) | — |
| Minuta §12, CLAUDE.md | CA-P8-70 a 75 | API, COMP, MAN |

---

## 7. ¿Hace falta migración?

- **Alcance mínimo (grupo 1): no.** Reportes, indicadores, exportación, indicadores del asesor y línea de tiempo leen tablas que ya existen y están mapeadas (`vehiculos`, `publicaciones`, `publicaciones_destacadas`, `leads`, `eventos_lead`, `ventas_vehiculo`, `suscripciones`, `planes`, `pagos`, `auditoria_eventos`). La auditoría de exportaciones usa `auditoria_eventos`. Si las pruebas de rendimiento (CA-P8-70) lo pidieran, un índice sobre `leads(creado_en)` o `auditoria_eventos(entidad, entidad_id)` sería una migración **017** pequeña y reversible; no se prevé con el volumen actual.
- **Solo si se aprueba algo del grupo 2 o D-75:**
  - D-73 B/C/D (recuperar contraseña): probablemente sin migración (Identity guarda los tokens; `AspNetUserTokens` está mapeada). HU-070 (preferencias de notificación) sí necesitaría tabla nueva.
  - D-75 B (promociones o contenido): tabla nueva, migración 017.
  - D-76 B (conteo propio de visitas): tabla nueva de conteos diarios, migración 017.
- Siguiente número libre: **017** (la última es `016_p7_pagos`, aplicada). Siempre con respaldo previo, conteos, sección BAJAR y aplicada por la responsable (D-16).
- Resultado: P8 se programó **sin migración** (`REVISION-SEGURIDAD-P8.md`, "Migración").

---

## 8. D-78: correcciones de seguridad de la recuperación asistida (CONFIRMADO, técnica, coordinador)

D-78 no cambia ninguna regla de negocio; precisa CA-P8-67 tras QA (`P8-evidencia.md`, QA-P8-1 y QA-P8-2) y la revisión de seguridad (`REVISION-SEGURIDAD-P8.md`, M-1, M-2, B-1 y B-4). Detalle en `DECISIONES.md` D-78 y `backend/GUIA-AUTENTICACION.md` §12.

| Punto de D-78 | Qué se exige | Origen | Marca |
|---|---|---|---|
| 1 | Todo cambio de estado de la cuenta (aprobar, rechazar, bloquear, desbloquear, reconsiderar) invalida un enlace emitido antes | QA-P8-1 | CONFIRMADO (D-78) |
| 2 | Al usar el enlace se vuelve a comprobar que la cuenta esté ACTIVA y no sea de administrador (mismo 400 genérico); `promover-admin` también invalida el enlace | QA-P8-2, B-1 | CONFIRMADO (D-78) |
| 3 | Claves de DataProtection en `DataProtection:RutaClaves` (por defecto `C:\CAMION_claves`, nunca dentro del repositorio), nombre de aplicación `Camion.Api` y DPAPI en Windows | M-1 | CONFIRMADO (D-78) |
| 4 | Fuera de Development, `Recuperacion:UrlPortal` debe empezar por `https://`; si no, la API no arranca | M-2 | CONFIRMADO (D-78) |
| 5 | Todas las respuestas llevan `Referrer-Policy: no-referrer` | B-4 | CONFIRMADO (D-78) |

Pruebas: `P8QaTests` (QA-P8-1, QA-P8-2, que fallaban antes de D-78) y `P8D78Tests`. Siguen abiertos B-2 y B-3 de la revisión de seguridad (backlog, INF-2).

**Abierto:** D-79 (¿cómo recupera su contraseña un administrador?) está PENDIENTE de la responsable. No cambia ningún criterio de esta lista: hoy la recuperación asistida excluye a los administradores.
