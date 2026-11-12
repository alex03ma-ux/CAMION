# P7 — Contrato de la API: ventas, pagos del plan, parámetros y panel

Fijado antes de programar, a partir de `docs/criterios/criterios-P7-ventas-admin.md` (CA-P7-01 a CA-P7-59) y de las decisiones D-61 B, D-62 A, D-63 A y D-64 A (TOMADAS el 2026-11-12), además de D-21, D-26, D-34, D-35, D-36, D-39, D-40, D-41, D-42, D-47, D-48 y D-49. Extiende `P4p-contrato-api.md` y `P4b-contrato-api.md` sin cambiar lo que allí no se menciona: formato de error `{ error, errores? }`, auditoría en `auditoria_eventos`, mensajes de 401/403 comunes y paginación `{ items, total, pagina, tamano }`.

El frontend se programa contra este documento. **Cualquier cambio posterior se anota en la sección "Cambios al contrato" al final.**

**Trazabilidad**

| Código | Tema | Regla |
|---|---|---|
| HU-064, minuta §6 | Historial de ventas (vigentes y revertidas) | RN-VE-014, D-39, D-62 A |
| HU-066, HU-067, FN-04 | Registro y anulación de pagos del plan | D-26, D-36, D-61 B |
| HU-073, HU-074 (parcial) | Aviso de vencimiento dentro del panel | RN-NO-004/005, D-64 b A |
| HU-075 (parcial) | Contadores del panel del administrador | RN-RE-001, D-64 a A |
| HU-081 | Parámetros generales: datos de contacto del portal | RN-PA-001, D-42, D-47, D-63 A |
| HU-061 | Auditoría de toda escritura | RN-SE-003 |

## Reglas de P7

- **Sin dinero en línea (D-26, D-45, D-61 B)**: ninguna ruta recibe datos de tarjeta, cuentas bancarias ni pagos en línea, ni emite facturas. El pago se **anota** después de cobrarlo fuera del portal.
- **Pagos: solo el administrador** los ve y los escribe. El asesor no los ve ni en "Mi suscripción" (CA-P7-32).
- **Ventas (D-62 A)**: la reversión no cambia (P4b). El administrador ve todas las ventas; el asesor, las de los vehículos que **hoy** son suyos (mismo alcance que su inventario), también las revertidas. Sin línea de tiempo de auditoría (D-62 d A).
- **Parámetros (D-63 A)**: solo WhatsApp, correo, horario (1 a 4 líneas), Instagram y TikTok. Ningún valor de negocio (días por mes, fotos, límites anti-abuso) es editable.
- **Panel (D-64 A)**: solo números, sin nombres, teléfonos, correos ni sumas de dinero.
- Ninguna ruta nueva contiene "auditoria" ni "historial" (prueba `SesionTests.Inventario_de_endpoints`).

## Rutas nuevas y modificadas

| Método | Ruta | Política | Estado |
|---|---|---|---|
| GET | `/api/ventas` | AsesorOAdministrador | nueva |
| GET | `/api/inventario/vehiculos/{id}/ventas` | AsesorOAdministrador (grupo `/api/inventario`) | nueva |
| GET | `/api/admin/suscripciones/{id}/pagos` | SoloAdministrador | nueva |
| POST | `/api/admin/suscripciones/{id}/pagos` | SoloAdministrador | nueva (PROPUESTO, ver "Pendiente") |
| POST | `/api/admin/pagos/{id}/anular` | SoloAdministrador | nueva |
| POST | `/api/admin/asesores/{asesorId}/suscripcion` | SoloAdministrador | modificada: acepta `pago` |
| POST | `/api/admin/suscripciones/{id}/renovar` | SoloAdministrador | modificada: acepta `pago` |
| POST | `/api/admin/suscripciones/{id}/cambiar-plan` | SoloAdministrador | modificada: acepta `pago` |
| GET | `/api/asesor/suscripcion` | SoloAsesor | modificada: `avisoVencimiento`, `mensajeVencimiento` |
| GET | `/api/portal/contacto` | anónima (solo lectura) | nueva |
| GET | `/api/admin/parametros/contacto` | SoloAdministrador | nueva |
| PUT | `/api/admin/parametros/contacto` | SoloAdministrador | nueva |
| GET | `/api/admin/panel` | SoloAdministrador | nueva |
| GET | `/api/catalogo/publicaciones/{slug}` | anónima | modificada por dentro: `whatsapp` usa el parámetro (D-47) |

Comunes a todas las rutas protegidas: sin token o con sesión cerrada/expirada **401** (D-12); rol que no corresponde **403** `{ "error": "No tienes permiso para realizar esta acción." }`.

---

## 1. Ventas (minuta §6, HU-064, D-62 A)

### `VentaItem`

```json
{
  "id": 7,
  "vehiculo": { "id": 15, "titulo": "Chevrolet NPR 2020", "estado": "VENDIDO" },
  "asesor": { "id": 3, "nombre": "Asesora Alfa" },
  "fechaVenta": "2026-10-26",
  "precioFinalCop": 95000000,
  "origen": "PORTAL",
  "observaciones": null,
  "estado": "VIGENTE",
  "registradaEn": "2026-10-26T15:00:00Z",
  "registradaPor": "Asesora Alfa",
  "revertidaEn": null,
  "revertidaPor": null,
  "motivoReversion": null
}
```

| Campo | Tipo | Nota |
|---|---|---|
| `vehiculo.titulo` | string | "Marca Modelo Año" o la categoría (como en publicaciones) |
| `vehiculo.estado` | string | estado actual del vehículo |
| `asesor` | objeto \| null | dueño **actual** del vehículo (`perfiles_asesor.id`); `null` para el asesor (como en el inventario) |
| `origen` | string | `PORTAL`, `CONTACTO_ASESOR`, `REFERIDO`, `OTRO` (D-39) |
| `estado` | string | `VIGENTE` (sin reversión) o `REVERTIDA` |
| `registradaPor`, `revertidaPor` | string \| null | nombre de quien registró o revirtió; `null` si la cuenta ya no existe |

No incluye datos de compradores ni de leads (CA-P7-12).

### GET `/api/ventas`

Parámetros (todos opcionales):

| Parámetro | Regla | Mensaje (`errores.<campo>`) |
|---|---|---|
| `desde`, `hasta` | `AAAA-MM-DD`, sobre `fechaVenta`, ambos inclusive | "Escribe una fecha válida (aaaa-mm-dd)." |
| `desde` > `hasta` | — | `errores.hasta`: "La fecha inicial no puede ser posterior a la final." |
| `origen` | uno de D-39 (sin distinguir mayúsculas) | "Origen no válido." |
| `estado` | `VIGENTE` o `REVERTIDA` | "Estado no válido." |
| `asesorId` | entero ≥ 1; solo el administrador; el asesor lo envía y **se ignora** (CA-P7-05), aunque no sea válido | "Asesor no válido." |
| `pagina`, `tamano` | `pagina` ≥ 1; `tamano` 1 a 100 (por defecto 20) | "Página o tamaño no válidos." |

- 200 `{ "items": VentaItem[], "total": 1, "pagina": 1, "tamano": 20 }`, de la más reciente a la más antigua por `registradaEn` (y `id`).
- 400 `{ "error": "Revisa los filtros.", "errores": { ... } }`.
- 403 comprador o asesor sin perfil; 401 sin sesión.
- Sin filtros, `total` = número de filas de `ventas_vehiculo` dentro del alcance (CA-P7-14).

### GET `/api/inventario/vehiculos/{id}/ventas`

- 200 `{ "items": VentaItem[] }`: todas las ventas del vehículo, la `VIGENTE` primero y luego por `registradaEn` descendente. Lista vacía si el vehículo está VENDIDO sin fila de venta (DEMO, CA-P7-09); el texto lo pone la interfaz.
- 404 `{ "error": "Vehículo no encontrado." }` si no existe o es de otro asesor (CA-P7-07).
- 403 / 401 como arriba.

Vender y revertir (`/vender`, `/revertir-venta`) **no cambian** (P4b, D-62 a A y b A).

---

## 2. Pagos del plan (D-61 B)

Tabla `pagos` del script 002, ampliada por la **migración 016** (`database/migraciones/016_p7_pagos.sql`; sin aplicar, la aplica la responsable, D-16).

### `PagoRequest` (objeto `pago` en asignar, renovar y cambiar de plan; cuerpo directo en POST `/pagos`)

```json
{ "valorCop": 100000, "fechaPago": "2026-11-12", "medio": "TRANSFERENCIA", "referencia": "TRX-123" }
```

| Campo | Regla | Mensaje 400 (`errores.<campo>`) |
|---|---|---|
| `valorCop` | opcional; si falta, el precio del plan. Entero > 0 y ≤ 9.999.999.999.999 | "Escribe el valor pagado en pesos (número entero mayor que 0)." |
| `fechaPago` | obligatorio, `AAAA-MM-DD`, hoy o anterior (fecha de Colombia, UTC-5) | "Escribe la fecha del pago (AAAA-MM-DD), de hoy o anterior." |
| `medio` | obligatorio: `TRANSFERENCIA`, `CONSIGNACION`, `EFECTIVO`, `OTRO` (sin distinguir mayúsculas) | "Elige el medio de pago." |
| `referencia` | opcional, hasta 100 caracteres, sin `<` ni `>`; **obligatoria si `medio` = `OTRO`** (CA-P7-29) | "La referencia no puede tener más de 100 caracteres ni los signos < >." / "Escribe la referencia: es obligatoria si el medio es Otro." |
| — | `pago` enviado para un plan de precio 0 | `errores.pago`: "Este plan no tiene precio: no se registra un pago." |

### Cambios en asignar, renovar y cambiar de plan

Cuerpos: `{ "planId": 2, "pago": PagoRequest | null }` (asignar y cambiar de plan) y `{ "planId": 2 | null, "pago": PagoRequest | null }` (renovar).

- `pago` es **opcional** en la API (ver "Pendiente"). Si viene, se valida **antes** de escribir: con un error, 400 `{ "error": "Revisa los datos marcados.", "errores": {...} }` y no se crea ni cambia la suscripción ni el pago (CA-P7-29).
- Si es válido, la suscripción, el pago (estado `REGISTRADO`) y sus dos eventos de auditoría se guardan en **la misma transacción** (CA-P7-27).
- `concepto` del pago: `ASIGNACION`, `RENOVACION` o `CAMBIO_PLAN` según la ruta. Cambiar de plan liga el pago a la suscripción **nueva**.
- La respuesta sigue siendo `SuscripcionItem` (sin el pago); el resto del contrato P4p no cambia.

### `PagoItem`

```json
{
  "id": 1, "suscripcionId": 4, "valorCop": 100000, "fechaPago": "2026-11-12", "medio": "TRANSFERENCIA",
  "referencia": "TRX-123", "concepto": "ASIGNACION", "estado": "REGISTRADO",
  "registradoEn": "2026-11-12T15:00:00Z", "registradoPor": "Admin",
  "anuladoEn": null, "anuladoPor": null, "motivoAnulacion": null
}
```

`estado`: `REGISTRADO` o `ANULADO`. `concepto`: `ASIGNACION`, `RENOVACION`, `CAMBIO_PLAN` o `POSTERIOR`.

### GET `/api/admin/suscripciones/{id}/pagos`

- 200 `{ "items": PagoItem[], "totalPagadoCop": 100000 }`; orden: `fechaPago` descendente y luego `id` descendente. `totalPagadoCop` suma solo los `REGISTRADO` de esa suscripción (CA-P7-30).
- 404 `{ "error": "No encontrado." }` si la suscripción no existe.

### POST `/api/admin/suscripciones/{id}/pagos` (PROPUESTO)

Cuerpo `PagoRequest`. Para anotar un pago después (p. ej. tras anular uno mal escrito). `concepto` = `POSTERIOR`; `valorCop` por defecto = precio del plan actual de la suscripción.

- 201 `PagoItem` (cabecera `Location: /api/admin/suscripciones/{id}/pagos`).
- 400 como arriba; `errores.pago` si el plan de la suscripción tiene precio 0.
- 404 `{ "error": "No encontrado." }`.

### POST `/api/admin/pagos/{id}/anular`

Cuerpo `{ "motivo": "Valor mal escrito" }` (5 a 500 caracteres).

- 200 `PagoItem` con `estado` = `ANULADO`, quién, cuándo y motivo. La fila no se borra y la suscripción no cambia (CA-P7-31).
- 400 `errores.motivo`: "Escribe el motivo (5 a 500 caracteres)."
- 404 `{ "error": "Pago no encontrado." }`.
- 409 `{ "error": "Este pago ya está anulado." }`.

### Auditoría

Entidad `Pago`, `entidad_id` = id del pago. Acciones `REGISTRAR` (datos nuevos: `suscripcionId`, `valorCop`, `fechaPago`, `medio`, `referencia`, `concepto`, `estado`) y `ANULAR` (antes `estado`; después `estado`, `motivo`).

---

## 3. "Mi suscripción" del asesor (D-64 b A)

`GET /api/asesor/suscripcion` añade dos campos a `MiSuscripcionResponse` (los demás no cambian):

| Campo | Tipo | Regla |
|---|---|---|
| `avisoVencimiento` | bool | `true` si la suscripción está vigente y `diasRestantes` ≤ 7 |
| `mensajeVencimiento` | string \| null | "Tu plan vence en N días. Para renovarlo, comunícate con el administrador." ("1 día" en singular); `null` si `avisoVencimiento` es `false` |

---

## 4. Parámetros: contacto del portal (HU-081, D-63 A)

Se guarda en `configuracion_sistema` (existe en `camion_ventas`, vacía; **sin migración**), clave `portal.contacto`, `valor_json` = `{ "whatsapp": "3166152907", "correo": "...", "horario": ["..."], "instagram": null, "tiktok": null }`.

**Valores por defecto** mientras no se guarde nada (CA-P7-38, D-42): WhatsApp = `Contactos:WhatsappPortal` de `appsettings.json` (hoy 573166152907, se muestra 3166152907); correo `vc.automotores@gmail.com`; horario `["Lunes a viernes: 9:00 a. m. a 5:00 p. m.", "Sábados: 9:00 a. m. a 3:00 p. m."]`; Instagram y TikTok `null`.

### `ContactoPortalResponse` — GET `/api/portal/contacto` (anónima)

```json
{
  "whatsapp": "3166152907",
  "whatsappEnlace": "https://wa.me/573166152907",
  "correo": "vc.automotores@gmail.com",
  "horario": ["Lunes a viernes: 9:00 a. m. a 5:00 p. m.", "Sábados: 9:00 a. m. a 3:00 p. m."],
  "instagram": null,
  "tiktok": null
}
```

Solo GET, solo estos datos públicos, fuera de `/api/catalogo` (CA-P7-42). 200 siempre.

### GET `/api/admin/parametros/contacto`

200 `ContactoPortalAdmin` = los campos anteriores más `configurado` (bool; `false` = valores por defecto), `actualizadoEn` (fecha UTC o `null`) y `actualizadoPor` (nombre o `null`).

### PUT `/api/admin/parametros/contacto`

Cuerpo `{ "whatsapp": "316 615 2907", "correo": "...", "horario": ["...", "..."], "instagram": "https://www.instagram.com/cuenta", "tiktok": "" }`.

| Campo | Regla | Mensaje 400 (`errores.<campo>`) |
|---|---|---|
| `whatsapp` | obligatorio, celular colombiano (regla de P5: 10 dígitos que empiezan por 3, con o sin +57 y espacios) | "Escribe un celular colombiano de 10 dígitos, por ejemplo 316 615 2907." |
| `correo` | obligatorio, formato del registro, hasta 254 | "Escribe un correo válido." |
| `horario` | lista de 1 a 4 líneas; cada una de 1 a 100 caracteres tras quitar espacios, sin `<` ni `>` | "Escribe de 1 a 4 líneas de horario, de hasta 100 caracteres cada una y sin los signos < >." |
| `instagram` | opcional (vacío o `null` = sin enlace). `https://` con host `www.instagram.com` o `instagram.com`, ruta no vacía, sin usuario ni puerto, sin espacios ni `<>"'`, hasta 200 | "Escribe el enlace de Instagram (https://www.instagram.com/...) o deja el campo vacío." |
| `tiktok` | igual, host `www.tiktok.com` o `tiktok.com` | "Escribe el enlace de TikTok (https://www.tiktok.com/@...) o deja el campo vacío." |

- 200 `ContactoPortalAdmin` con los valores guardados (WhatsApp normalizado a 10 dígitos, correo en minúsculas).
- 400 `{ "error": "Revisa los datos marcados.", "errores": {...} }`; nada cambia.
- Auditoría en la misma transacción: entidad `Parametro`, `entidad_id` = `portal.contacto`, acción `CAMBIAR`, con los valores anteriores (o `null` si eran los de por defecto) y los nuevos. Si nada cambió, 200 sin evento.

### WhatsApp de la ficha (D-47, D-49; CA-P7-40)

`GET /api/catalogo/publicaciones/{slug}` → `whatsapp` usa el WhatsApp guardado en el parámetro cuando el dueño es administrador o su teléfono no es un celular; si no hay parámetro, el de `appsettings.json` (respaldo).

---

## 5. Panel del administrador (HU-075 parcial, D-64 a A)

### GET `/api/admin/panel`

Antes de contar aplica el barrido de leads de D-46, como `GET /api/leads/resumen`.

```json
{
  "asesoresPendientes": 2,
  "solicitudes": [
    { "origen": "CONTACTO_VEHICULO", "abiertas": 3, "nuevas": 1 },
    { "origen": "VENDE_TU_CAMION", "abiertas": 1, "nuevas": 1 },
    { "origen": "SEGURO_TODO_RIESGO", "abiertas": 0, "nuevas": 0 },
    { "origen": "CREDITO", "abiertas": 0, "nuevas": 0 },
    { "origen": "BUSQUEDA", "abiertas": 0, "nuevas": 0 }
  ],
  "suscripcionesPorVencer": 1,
  "suscripcionesVencidas": 0,
  "publicacionesVisibles": 4,
  "vehiculosPorEstado": { "ACTIVO": 5, "RESERVADO": 1, "INACTIVO": 0, "VENDIDO": 2 },
  "ventasUltimos30Dias": 1,
  "ventasDesde": "2026-10-14"
}
```

| Contador | Definición | Lista equivalente |
|---|---|---|
| `asesoresPendientes` | cuentas con rol ASESOR y estado PENDIENTE | `/api/admin/usuarios?rol=ASESOR&estado=PENDIENTE` |
| `solicitudes[].abiertas` | leads ABIERTO de ese origen **a cargo de Administración** (sin asesor responsable); siempre los 5 orígenes, en ese orden | `/api/leads` filtrado por origen y responsable Administración |
| `solicitudes[].nuevas` | de esas, las no vistas | — |
| `suscripcionesPorVencer` | vigentes con `finVigencia` dentro de los próximos 7 días (`diasRestantes` ≤ 7) | `/api/admin/suscripciones?vista=porVencer` |
| `suscripcionesVencidas` | con estado efectivo VENCIDA cuyo asesor no tiene hoy una suscripción vigente | `/api/admin/suscripciones?vista=vencidasSinRenovar` |
| `publicacionesVisibles` | las que hoy muestra el portal (regla de P3/P4b) | catálogo público |
| `vehiculosPorEstado` | conteo por estado, siempre las 4 claves | `/api/inventario/vehiculos?estado=...` |
| `ventasUltimos30Dias` | ventas VIGENTE con `fechaVenta` ≥ `ventasDesde` (hoy en Colombia menos 29 días: 30 días con hoy) | `/api/ventas?estado=VIGENTE&desde={ventasDesde}` |

Solo números (CA-P7-48, CA-P7-51). 403 asesor o comprador; 401 sin sesión.

---

## Migración 016 (`database/migraciones/016_p7_pagos.sql` y `016_p7_pagos_SUBIR.sql`)

Solo `ALTER TABLE pagos` (0 filas en `camion_ventas`, comprobado en solo lectura el 2026-11-12):

- columnas nuevas, todas NULL: `fecha_pago DATE`, `medio VARCHAR(20)`, `concepto VARCHAR(20)`, `anulado_por VARCHAR(255)` (FK `AspNetUsers` ON DELETE SET NULL), `anulado_en DATETIME(6)`, `motivo_anulacion VARCHAR(500)`;
- `CK_Pago_Estado` se quita y se vuelve a crear con `REGISTRADO` y `ANULADO` (además de los tres del script 002);
- CHECK nuevos `CK_Pago_Medio`, `CK_Pago_Concepto` y `CK_Pago_Anulacion` (un ANULADO tiene fecha y motivo).
- Se reutilizan `verificado_por` / `verificado_en` como "registrado por / en" (el administrador lo verifica al anotarlo). `soporte_archivo` no se usa (sin comprobante, D-61 B).

`configuracion_sistema` no necesita migración. Ventas y panel usan tablas existentes.

---

## Pendiente (requiere visto bueno del coordinador)

1. **`pago` opcional** al asignar, renovar o cambiar de plan con precio > 0. D-61 dice "el administrador anota el pago", pero no si la API debe rechazar la operación sin pago. Opcional evita romper la interfaz actual de P4p; la interfaz de P7 lo puede pedir siempre. Si se quiere obligatorio, es un cambio pequeño (400 `errores.pago`).
2. **POST `/api/admin/suscripciones/{id}/pagos`** (pago posterior): no está en D-61 literalmente; se propone para poder corregir un pago anulado sin renovar.
3. **`referencia` obligatoria con medio `OTRO`**: sale de CA-P7-29; D-61 dice "referencia opcional".

## Cambios al contrato

### 2026-11-12 — correcciones de `REVISION-SEGURIDAD-P7.md` (B-1 a B-4) y `P7-evidencia.md` (O-5)

1. **B-2, correo del portal (PUT `/api/admin/parametros/contacto`).** Regla estricta: parte local con letras y dígitos ASCII y `. _ + -` (empieza por letra o dígito), `@`, dominio con al menos un punto y terminación de 2 a 24 letras; sin `..`, hasta 254. Se rechazan `? & ; , : / < > " ' %` y espacios. Mismo mensaje: "Escribe un correo válido." El horario también rechaza caracteres de control (mismo mensaje de horario).
2. **B-1, revalidación al leer.** `GET /api/portal/contacto`, `GET /api/admin/parametros/contacto` y el WhatsApp de la ficha pasan el valor guardado otra vez por las reglas del PUT, campo a campo. Un campo que no cumple no se devuelve: WhatsApp, correo y horario vuelven al respaldo (D-42 / `appsettings.json`) e Instagram y TikTok quedan en `null`. El servidor nunca devuelve un correo, enlace o WhatsApp inválido.
3. **B-3, caracteres de control.** `referencia` del pago: `errores.referencia` = "La referencia no puede tener saltos de línea ni caracteres de control.". `motivo` de anular un pago: `errores.motivo` = "El motivo no puede tener saltos de línea ni caracteres de control." (se comprueba después de la regla de 5 a 500).
4. **B-4, anular un pago.** La auditoría `ANULAR` guarda como `estado` anterior el estado real de la fila (también `PENDIENTE`, `APROBADO` o `RECHAZADO` del script 002). Un pago sin `fechaPago` o sin `medio` (solo filas antiguas) no se puede anular: **409** "Este pago no tiene fecha o medio registrados: no se puede anular." (`CK_Pago_Registro` lo impediría).
5. **O-5, CA-P7-46: filtro `vista` en GET `/api/admin/suscripciones`.** Parámetro opcional, sin distinguir mayúsculas:
   - `vista=porVencer`: vigentes (ACTIVA/REACTIVADA, inicio ≤ ahora < fin) con `finVigencia` ≤ ahora + 7 días. Misma consulta que `suscripcionesPorVencer` del panel.
   - `vista=vencidasSinRenovar`: estado efectivo VENCIDA y el asesor no tiene hoy una suscripción vigente. Misma consulta que `suscripcionesVencidas` del panel.
   - Se combina (Y) con `asesorId`, `estado`, `pagina` y `tamano`. Otro valor: 400 `{ "error": "Revisa los filtros.", "errores": { "vista": ["Vista no válida."] } }`.
   - `estado=VENCIDA` no cambia (sigue incluyendo vencidas ya reemplazadas).
   - **Enlaces del panel** (la respuesta del panel no cambia): `suscripcionesPorVencer` → `/api/admin/suscripciones?vista=porVencer`; `suscripcionesVencidas` → `/api/admin/suscripciones?vista=vencidasSinRenovar`. En la tabla de la §5, la columna "Lista equivalente" de esas dos filas queda reemplazada por estos filtros.
