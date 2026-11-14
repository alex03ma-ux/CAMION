# P8 — Contrato de la API: reportes, indicadores, actividad y recuperación asistida

Fijado antes de programar, a partir de `docs/criterios/criterios-P8-reportes.md` (CA-P8-01 a CA-P8-75) y de las decisiones D-68 A, D-69 A/A, D-70 A, D-71 (CSV, b A), D-72 A/A y D-73 D (TOMADAS el 2026-11-13), además de D-5, D-7, D-12, D-39, D-41, D-44, D-46, D-52, D-57, D-58, D-61 B, D-62, D-64 a A, D-66 y D-67. Extiende los contratos de P2 a P7 sin cambiar lo que allí no se menciona: formato de error `{ error, errores? }`, auditoría en `auditoria_eventos`, mensajes comunes de 401/403 y paginación `{ items, total, pagina, tamano }`.

El frontend se programa contra este documento. **Cualquier cambio posterior se anota en la sección "Cambios al contrato" al final.**

**Trazabilidad**

| Código | Tema | Regla |
|---|---|---|
| HU-076, RN-RE-002, M13 | Reporte de vehículos y publicaciones | D-68 A, D-40, D-41 |
| HU-077, RN-RE-003 | Reporte de solicitudes | D-68 A, D-7, D-46, D-57 b |
| HU-078, RN-RE-004 | Reporte de créditos | D-57 a/b A, D-58 A, D-45 |
| HU-080, M13 | Reporte de ventas; reporte de suscripciones y pagos | D-39, D-61 B, D-62, D-66, D-67 |
| HU-075, HU-080, RN-RE-001, RN-RE-006 | Indicadores del administrador | D-69 A/A, D-64 a A |
| HU-075/HU-080 (solo lo propio) | "Mis indicadores" del asesor | D-70 A |
| HU-079, RN-RE-005 | Exportación CSV | D-71 (a) CSV, (b) A |
| HU-061, RN-SE-003 | Actividad de un vehículo y de una suscripción | D-72 A/A, D-62 d |
| HU-063, RN-US-006 | Recuperación asistida de contraseña | D-73 D, D-12 |

## Reglas de P8

- **Sin servicios externos ni dependencias nuevas** (D-73 D, D-74 A, D-76 A, D-77 A): no se envía correo ni WhatsApp automático; el enlace de recuperación lo copia el administrador y lo envía él.
- **Sin datos personales de clientes o visitantes** (RN-SE-004, D-71 b A): ningún reporte, indicador, exportación ni actividad devuelve nombre, teléfono, correo, mensaje, notas ni observaciones de cierre de un lead. Tampoco las observaciones de una venta ni la nota de reserva (texto libre que puede tener datos del comprador). Sí aparecen identificadores, fechas, estados, origen, resultado, vehículo y asesor responsable.
- **Solo lectura** (CA-P8-08): todas las rutas de reportes, indicadores y actividad son GET y no cambian datos. Excepción: las que cuentan solicitudes ejecutan antes el barrido de D-46, igual que `GET /api/admin/panel`.
- **Dinero solo para el administrador** (D-69 b A): valores en pesos enteros, sin decimales. El panel "Inicio" de P7 no cambia (CA-P8-39).
- **Créditos, avisos, búsquedas y pagos nunca llegan al asesor** (D-52 A, D-58 A, D-61 B).
- Ninguna ruta nueva contiene "auditoria" ni "historial" (CA-061-15 de `SesionTests` sigue igual, sin excepciones).
- **Sin migración**: todo sale de tablas ya mapeadas; los tokens de recuperación no se guardan (ver §8).

## Rutas nuevas

| Método | Ruta | Política | Criterios |
|---|---|---|---|
| GET | `/api/admin/reportes/vehiculos` | SoloAdministrador | CA-P8-01 a 16 |
| GET | `/api/admin/reportes/solicitudes` | SoloAdministrador | CA-P8-17 a 23 |
| GET | `/api/admin/reportes/creditos` | SoloAdministrador | CA-P8-24 a 27 |
| GET | `/api/admin/reportes/ventas` | SoloAdministrador | CA-P8-28, 29 |
| GET | `/api/admin/reportes/suscripciones` | SoloAdministrador | CA-P8-30 a 33 |
| GET | `/api/admin/reportes/{tipo}/csv` (5 rutas: `vehiculos`, `solicitudes`, `creditos`, `ventas`, `suscripciones`) | SoloAdministrador | CA-P8-42 a 50 |
| GET | `/api/admin/indicadores` | SoloAdministrador | CA-P8-34 a 41 |
| GET | `/api/asesor/indicadores` | SoloAsesor | CA-P8-51 a 55 |
| GET | `/api/admin/vehiculos/{id}/actividad` | SoloAdministrador | CA-P8-56, 58 a 62 |
| GET | `/api/admin/suscripciones/{id}/actividad` | SoloAdministrador | CA-P8-57 a 62 |
| POST | `/api/admin/usuarios/{id}/enlace-recuperacion` | SoloAdministrador | CA-P8-67, D-73 D |
| POST | `/api/auth/restablecer-contrasena` | anónima, con límite de peticiones por IP | CA-P8-67, D-73 D |

Comunes a las rutas protegidas: sin token o con sesión cerrada/expirada **401** (D-12, CA-P8-10); rol que no corresponde **403** `{ "error": "No tienes permiso para realizar esta acción." }` (CA-P8-06, 40, 49, 55, 60).

---

## 1. Filtros comunes de los reportes y los indicadores

| Parámetro | Formato | Regla |
|---|---|---|
| `desde`, `hasta` | `aaaa-mm-dd`, día calendario de Colombia (UTC-5) | Si faltan los dos: el mes en curso (del día 1 al último día). Si falta uno, se toma el del mes en curso. `hasta` incluye el día completo. |
| `pagina`, `tamano` | enteros | `filas` paginadas: 20 por defecto, máximo 100. |

Validación (400 `{ "error": "Revisa los filtros.", "errores": { campo: [mensaje] } }`, CA-P8-04):

| Campo | Mensaje |
|---|---|
| `desde` / `hasta` mal escrita | "Escribe una fecha válida (aaaa-mm-dd)." |
| `hasta` anterior a `desde` | (`hasta`) "La fecha inicial no puede ser posterior a la final." |
| más de 366 días (contando ambos extremos) | (`hasta`) "El período no puede superar 366 días." |
| `asesorId` inexistente o no numérico | "Asesor no válido." |
| `categoriaId` inexistente | "Categoría no válida." |
| `departamentoId` / `municipioId` inexistente | "Departamento no válido." / "Municipio no válido." |
| municipio de otro departamento | (`municipioId`) "El municipio no corresponde al departamento." |
| `origen`, `estado`, `medio`, `canal` fuera de la lista | "Origen no válido." / "Estado no válido." / "Medio no válido." / "Canal no válido." |
| `resultado` fuera de la lista | "Resultado no válido." |
| `resultado` que no corresponde al `origen` elegido (`Lead.ResultadosDe`) | "Ese resultado no corresponde al origen elegido." |
| `responsable` que no es `ADMINISTRACION` ni un asesor existente | "Responsable no válido." |
| `planId` inexistente | "Plan no válido." |
| `pagina` / `tamano` | "Página o tamaño no válidos." |

Toda respuesta trae `periodo: { "desde": "2026-11-01", "hasta": "2026-11-30" }` (CA-P8-02). Un período sin datos responde 200 con ceros y listas vacías (CA-P8-05). Las fechas y horas de las filas van en UTC (ISO 8601, como el resto de la API); el frontend las muestra en hora de Colombia.

Filtros "propios" por reporte: los indicados en cada sección. Un parámetro que el reporte no usa se ignora.

## 2. `GET /api/admin/reportes/vehiculos` (HU-076)

Filtros: `desde`, `hasta`, `asesorId`, `categoriaId`, `departamentoId`, `municipioId`, `pagina`, `tamano`. `asesorId`, `categoriaId` y la ubicación limitan **todas** las cifras; el período limita solo "registrados en el período", "más contactados" y `filas`.

```json
{
  "periodo": { "desde": "2026-11-01", "hasta": "2026-11-30" },
  "inventarioPorEstado": { "ACTIVO": 4, "RESERVADO": 1, "INACTIVO": 0, "VENDIDO": 2 },
  "inventarioPorCategoria": [ { "id": 2, "nombre": "Camión", "total": 6 } ],
  "registradosEnPeriodo": 3,
  "publicacionesPorEstado": { "BORRADOR": 0, "PUBLICADA": 4, "OCULTA": 1 },
  "publicacionesEliminadas": 1,
  "publicacionesVisiblesHoy": 3,
  "publicacionesDestacadasHoy": 1,
  "masContactados": [ { "vehiculoId": 15, "titulo": "Chevrolet NPR 2020", "contactos": 5 } ],
  "filas": { "items": [ FilaVehiculo ], "total": 3, "pagina": 1, "tamano": 20 }
}
```

- Inventario: estado actual del vehículo (D-4, D-11); todas las categorías activas o con vehículos aparecen, también en 0. Un vehículo DEMO o sin publicación cuenta igual (CA-P8-16).
- `publicacionesVisiblesHoy`: misma regla que el catálogo (`ConsultasCatalogo.Visibles`, D-41). `publicacionesDestacadasHoy`: visibles con destacado vigente. Las ELIMINADA (D-40) se cuentan solo en `publicacionesEliminadas` (CA-P8-14).
- `masContactados`: hasta 10 vehículos con más contactos de vehículo (`CONTACTO_VEHICULO`) creados en el período; sin datos de quienes contactaron (CA-P8-15).
- `FilaVehiculo` (vehículos registrados en el período, del más reciente al más antiguo): `{ vehiculoId, titulo, categoria, estado, asesor: { id, nombre }, municipio, departamento, registradoEn, publicacion: { id, estado } | null, contactosEnPeriodo }`.

## 3. `GET /api/admin/reportes/solicitudes` (HU-077)

Filtros: `desde`, `hasta`, `origen` (`CONTACTO_VEHICULO`, `VENDE_TU_CAMION`, `SEGURO_TODO_RIESGO`, `CREDITO`, `BUSQUEDA`), `estado` (`ABIERTO`, `CERRADO`), `resultado`, `responsable` (`ADMINISTRACION` o `perfiles_asesor.id`), `categoriaId` (categoría del vehículo de la solicitud; las que no tienen vehículo quedan fuera), `pagina`, `tamano`. Todos se aplican a la vez (CA-P8-20). Antes de contar se ejecuta el barrido de D-46.

```json
{
  "periodo": { ... },
  "recibidas": 12,
  "porOrigen": [ { "origen": "CONTACTO_VEHICULO", "recibidas": 8, "abiertas": 5, "cerradas": 3 } ],
  "cerradasEnPeriodo": 6,
  "cerradasPorResultado": [ { "origen": "CONTACTO_VEHICULO", "resultado": "VENTA_CONCRETADA", "total": 2 } ],
  "tiempoMedioCierre": [ { "origen": "CONTACTO_VEHICULO", "cerradas": 3, "dias": 2.5 } ],
  "contactosPorResponsable": {
    "administracion": 3,
    "asesores": [ { "id": 4, "nombre": "Asesora Alfa", "total": 5 } ],
    "reasignadosAMano": 1,
    "pasadosAAdministracion": 2
  },
  "filas": { "items": [ FilaSolicitud ], "total": 12, "pagina": 1, "tamano": 20 }
}
```

- `recibidas` / `porOrigen`: creadas en el período (fecha de creación). Los 5 orígenes aparecen siempre, en el orden de arriba.
- `cerradasPorResultado`: solicitudes **hoy cerradas** cuya fecha de cierre (`cerrado_en`, el último cierre) cae en el período; una reabierta y cerrada de nuevo cuenta una vez con su último resultado (CA-P8-22). Se listan solo las combinaciones válidas de `Lead.ResultadosDe(origen)`, también en 0 (CA-P8-18). El frontend traduce los códigos a los nombres que ya usa la bandeja.
- `tiempoMedioCierre`: para las cerradas en el período, media en días (1 decimal) entre la creación y el **primer** cierre (`eventos_lead` CERRADO); `dias: null` si no hay cerradas (CA-P8-21, CA-P8-38).
- `contactosPorResponsable`: solo contactos de vehículo creados en el período: responsable actual (Administración o cada asesor), cuántos tuvieron una reasignación manual y cuántos pasaron solos a Administración por el barrido de D-46 (CA-P8-19).
- `FilaSolicitud`: `{ id, creadoEn, origen, estado, resultado, cerradoEn, responsable: { tipo: "ADMINISTRACION" | "ASESOR", asesorId, nombre }, vehiculo: { id, titulo } | null }`. Nunca nombre, teléfono, correo, mensaje, notas ni observación de cierre (CA-P8-23).

## 4. `GET /api/admin/reportes/creditos` (HU-078)

Filtros: `desde`, `hasta`, `estado` (estado mostrado de D-57 a: `REGISTRADO`, `EN_GESTION`, `CERRADO`), `resultado` (de `Lead.ResultadosCredito`), `canal` (`PORTADA` sin vehículo, `FICHA` desde la ficha de un vehículo), `pagina`, `tamano`.

```json
{
  "periodo": { ... },
  "recibidos": 5,
  "porEstado": { "REGISTRADO": 2, "EN_GESTION": 1, "CERRADO": 2 },
  "porCanal": { "PORTADA": 3, "FICHA": 2 },
  "cerrados": 4,
  "cerradosPorResultado": [ { "resultado": "REMITIDO_ENTIDAD", "total": 3 } ],
  "porcentajeRemitido": 75.0,
  "filas": { "items": [ FilaCredito ], "total": 5, "pagina": 1, "tamano": 20 }
}
```

- `recibidos`, `porEstado`, `porCanal`: creados en el período. `cerrados` y `cerradosPorResultado`: cerrados en el período (los 6 resultados de D-57 b, también en 0). `porcentajeRemitido` = remitidos / cerrados × 100 con 1 decimal; `null` si no hay cerrados (CA-P8-25, CA-P8-38).
- Sin cifras de aprobación, montos, ingresos ni entidad financiera (CA-P8-26).
- `FilaCredito`: `{ id, creadoEn, canal, estado, resultado, cerradoEn, vehiculo: { id, titulo } | null }`.

## 5. `GET /api/admin/reportes/ventas` (HU-080, D-39)

Filtros: `desde`, `hasta` (fecha de venta), `asesorId` (dueño actual del vehículo, como `GET /api/ventas`), `categoriaId`, `origen` (`PORTAL`, `CONTACTO_ASESOR`, `REFERIDO`, `OTRO`), `pagina`, `tamano`.

```json
{
  "periodo": { ... },
  "vigentes": { "cantidad": 3, "valorCop": 285000000 },
  "revertidas": 1,
  "porOrigen": [ { "origen": "PORTAL", "cantidad": 2, "valorCop": 190000000 } ],
  "porCategoria": [ { "id": 2, "nombre": "Camión", "cantidad": 3, "valorCop": 285000000 } ],
  "porAsesor": [ { "id": 4, "nombre": "Asesora Alfa", "cantidad": 3, "valorCop": 285000000 } ],
  "filas": { "items": [ FilaVenta ], "total": 4, "pagina": 1, "tamano": 20 }
}
```

- Solo las vigentes suman en cantidad y valor; las revertidas con fecha de venta en el período se cuentan aparte (CA-P8-28, 29). Los 4 orígenes aparecen siempre. Mismas cifras que `GET /api/ventas` con los mismos filtros (CA-P8-09).
- `FilaVenta`: `{ id, fechaVenta, vehiculo: { id, titulo }, categoria, asesor: { id, nombre }, origen, precioFinalCop, estado: "VIGENTE" | "REVERTIDA" }`. Sin observaciones ni motivo de reversión.

## 6. `GET /api/admin/reportes/suscripciones` (D-61 B, D-66, D-67)

Filtros: `desde`, `hasta`, `asesorId`, `planId`, `medio` (`TRANSFERENCIA`, `CONSIGNACION`, `EFECTIVO`, `OTRO`; solo afecta a los pagos), `pagina`, `tamano`.

```json
{
  "periodo": { ... },
  "porEstado": { "PENDIENTE": 0, "ACTIVA": 3, "SUSPENDIDA": 0, "REACTIVADA": 0, "VENCIDA": 1, "CANCELADA": 1, "RECHAZADA": 0 },
  "vigentesPorPlan": [ { "id": 2, "nombre": "Plan Básico", "total": 3 } ],
  "asignadasEnPeriodo": 2,
  "renovadasEnPeriodo": 1,
  "cambiosDePlanEnPeriodo": 0,
  "vencidasEnPeriodo": 1,
  "pagos": {
    "cantidad": 3,
    "totalRecibidoCop": 300000,
    "porMedio": [ { "codigo": "TRANSFERENCIA", "cantidad": 2, "valorCop": 200000 } ],
    "porPlan": [ { "id": 2, "nombre": "Plan Básico", "cantidad": 3, "valorCop": 300000 } ],
    "porConcepto": [ { "codigo": "ASIGNACION", "cantidad": 2, "valorCop": 200000 } ],
    "anulados": { "cantidad": 1, "valorCop": 100000 },
    "sinFechaPago": 0
  },
  "aviso": "Pagos registrados a mano por la administración. No es contabilidad ni facturación.",
  "filas": { "items": [ FilaPago ], "total": 4, "pagina": 1, "tamano": 20 }
}
```

- `porEstado`: estado efectivo de hoy (VENCIDA incluye las ACTIVA/REACTIVADA ya vencidas, igual que `GET /api/admin/suscripciones`).
- `asignadasEnPeriodo`, `renovadasEnPeriodo`, `cambiosDePlanEnPeriodo`: eventos ASIGNAR, RENOVAR y CAMBIAR_PLAN de `auditoria_eventos` en el período (el cambio de plan cuenta una vez). `vencidasEnPeriodo`: suscripciones cuyo fin de vigencia cae en el período, ya pasó y no fueron canceladas.
- Pagos: fecha de referencia = fecha del pago; si no tiene (filas anteriores a P7), la fecha de registro (o de creación), y se cuentan en `sinFechaPago` (CA-P8-32). **Solo suman los pagos `REGISTRADO`**, igual que el total de "Pagos" de P7 (CA-P8-09); los anulados se cuentan aparte (CA-P8-31) y las filas anteriores a P7 (`PENDIENTE`, `APROBADO`, `RECHAZADO` del script 002) solo aparecen en `filas` y en `sinFechaPago`. Los 4 medios y los 4 conceptos aparecen siempre; en las filas, un pago sin medio o concepto muestra `SIN_DATO`.
- `FilaPago`: `{ id, fecha, sinFechaPago, suscripcionId, asesor: { id, nombre }, plan: { id, nombre }, medio, concepto, valorCop, estado }`. Sin referencia ni motivo de anulación.

## 7. Exportación CSV: `GET /api/admin/reportes/{tipo}/csv` (HU-079, D-71)

Mismos filtros y mismas validaciones (400 con el mismo mensaje, CA-P8-48) que la ruta del reporte; `pagina` y `tamano` se ignoran: van **todas** las filas.

- `Content-Type: text/csv; charset=utf-8`, UTF-8 **con BOM**, separador `;`, fin de línea CRLF (CA-P8-43).
- `Content-Disposition: attachment; filename="reporte-<tipo>-<desde>_<hasta>.csv"`; `Cache-Control: no-store`. El archivo no se guarda en el servidor (CA-P8-50).
- Contenido: título y período, filtros aplicados, cada bloque de cifras del reporte (`Concepto;Valor` o su tabla) y, al final, el bloque `Filas` con una línea por fila. Fechas `aaaa-mm-dd` (Colombia); pesos enteros sin separador de miles; porcentajes con coma decimal.
- Inyección de fórmulas (CA-P8-44): toda celda que empiece por `=`, `+`, `-`, `@`, tabulador o retorno de carro se escribe precedida de un apóstrofo (`'`). Las celdas con `;`, comillas o saltos de línea van entre comillas dobles, con las comillas duplicadas.
- Sin datos personales de clientes (CA-P8-45): las mismas columnas que las filas JSON.
- Más de 10.000 filas: 400 `{ "error": "El reporte tiene demasiadas filas. Acorta el período o añade filtros." }`; nunca un archivo parcial (CA-P8-47).
- Auditoría (CA-P8-46): fila `auditoria_eventos` con `entidad = "Reporte"`, `entidad_id = <tipo>`, `accion = "EXPORTAR"`, `usuario_id` = administrador y `datos_nuevos = { reporte, filtros: { desde, hasta, ... }, filas }`. Nunca el contenido.
- El asesor no exporta (D-70 A): 403.

## 8. Indicadores del administrador: `GET /api/admin/indicadores` (HU-075, HU-080, D-69 A/A)

Filtros: `desde`, `hasta`. El período anterior tiene la misma duración y termina el día antes de `desde` (CA-P8-35). Antes de contar se ejecuta el barrido de D-46.

```json
{
  "periodo": { "desde": "2026-11-01", "hasta": "2026-11-30" },
  "periodoAnterior": { "desde": "2026-10-02", "hasta": "2026-10-31" },
  "indicadores": {
    "contactosRecibidos": { "actual": 8, "anterior": 5, "diferencia": 3 },
    "porcentajeVentaConcretada": { "actual": 25.0, "anterior": null, "diferencia": null },
    "ventasVigentes": { "actual": 3, "anterior": 1, "diferencia": 2 },
    "valorVendidoCop": { "actual": 285000000, "anterior": 95000000, "diferencia": 190000000 },
    "solicitudesAdministracion": { "actual": 4, "anterior": 2, "diferencia": 2 },
    "creditosRemitidos": { "actual": 1, "anterior": 0, "diferencia": 1 },
    "ingresosPlanesCop": { "actual": 300000, "anterior": 0, "diferencia": 300000 },
    "tiempoMedioCierreDias": { "actual": 2.5, "anterior": null, "diferencia": null }
  },
  "solicitudesAdministracionPorOrigen": [ { "origen": "VENDE_TU_CAMION", "actual": 1, "anterior": 0 } ],
  "publicacionesVisiblesHoy": 3,
  "graficos": {
    "ventasPorMes": [ { "mes": "2025-12", "cantidad": 0, "valorCop": 0 } ],
    "ventasPorOrigen": [ { "origen": "PORTAL", "cantidad": 2, "valorCop": 190000000 } ],
    "solicitudesPorOrigen": [ { "origen": "CONTACTO_VEHICULO", "total": 8 } ],
    "contactosPorResultado": [ { "resultado": "VENTA_CONCRETADA", "total": 2 } ]
  }
}
```

- `contactosRecibidos`: contactos de vehículo creados. `porcentajeVentaConcretada`: contactos cerrados en el período con VENTA_CONCRETADA / contactos cerrados en el período × 100 (1 decimal); `null` = "Sin datos" (CA-P8-38). `solicitudesAdministracion`: solicitudes de Administración recibidas (avisos, créditos y búsquedas, D-52, D-58) y su desglose por origen. `creditosRemitidos`: créditos cerrados en el período con REMITIDO_ENTIDAD. `ingresosPlanesCop`: pagos registrados no anulados (fecha como en §6). `tiempoMedioCierreDias`: todas las solicitudes cerradas en el período. `diferencia` = actual − anterior (`null` si alguno es `null`).
- Gráficos (máximo 4, CA-P8-36): `ventasPorMes` son los 12 meses que terminan en el mes de `hasta`; los demás, del período. Los dibuja el frontend sin librería.
- Sin visitas ni datos de navegación (D-76 A, CA-P8-41).

## 9. "Mis indicadores" del asesor: `GET /api/asesor/indicadores` (D-70 A)

Política SoloAsesor (la cuenta debe estar ACTIVA: un asesor pendiente, rechazado o bloqueado no tiene sesión, CA-P8-55). Filtros: `desde`, `hasta`; `asesorId` y cualquier otro parámetro se ignoran (CA-P8-52).

```json
{
  "periodo": { ... },
  "vehiculosPorEstado": { "ACTIVO": 2, "RESERVADO": 0, "INACTIVO": 0, "VENDIDO": 1 },
  "publicacionesVisiblesHoy": 2,
  "contactosRecibidos": 4,
  "contactosAbiertos": 2,
  "contactosPorResultado": [ { "resultado": "VENTA_CONCRETADA", "total": 1 } ],
  "ventasVigentes": { "cantidad": 1, "valorCop": 95000000 }
}
```

- Alcance: los vehículos que **hoy** son suyos y los contactos de vehículo de los que **hoy** es responsable (el mismo de su inventario, "Mis contactos" y "Mis ventas"). `contactosRecibidos`: creados en el período; `contactosAbiertos`: de esos, los que siguen abiertos; `contactosPorResultado`: cerrados en el período (los 6 resultados de contacto). Ventas: vigentes con fecha de venta en el período.
- Sin avisos, créditos, búsquedas, pagos, cifras de otros asesores, totales del portal ni comparaciones (CA-P8-53). Sin exportación: no existe ruta CSV del asesor (CA-P8-54).

## 10. Actividad: `GET /api/admin/vehiculos/{id}/actividad` y `GET /api/admin/suscripciones/{id}/actividad` (HU-061, D-72 A/A)

Solo el administrador, solo lectura. `pagina`, `tamano` (20 por defecto, máximo 100; CA-P8-62). Del evento más reciente al más antiguo.

- Vehículo: eventos del vehículo, de **todas** sus publicaciones (también eliminadas) y de sus ventas (CA-P8-56). 404 "Vehículo no encontrado." (CA-P8-61).
- Suscripción: eventos de la suscripción (asignación, renovaciones, cambio de plan, suspensión, reactivación, cancelación, lo que deshizo D-67) y de sus pagos (registrados y anulados) (CA-P8-57). 404 "Suscripción no encontrada.".

```json
{
  "items": [
    {
      "id": 120,
      "fecha": "2026-11-05T15:00:00Z",
      "quien": "Admin Prueba",
      "sobre": "VEHICULO",
      "sobreId": 15,
      "accion": "REASIGNAR",
      "descripcion": "Reasignó de Asesora Alfa a Asesor Beta",
      "motivo": "Cambio de zona",
      "cambios": []
    }
  ],
  "total": 1, "pagina": 1, "tamano": 20
}
```

- `quien`: nombre del usuario, o "Sistema" si el evento no tiene actor. `sobre`: `VEHICULO`, `PUBLICACION`, `VENTA`, `SUSCRIPCION` o `PAGO`. `accion`: el código guardado; `descripcion`: la acción en palabras ("Registró el vehículo", "Reservó el vehículo", "Marcó vendido por $ 95.000.000 (Portal web)", "Asignó el plan Básico hasta el 2026-12-01", "Anuló el pago #3", "Deshizo la renovación al anular el pago #3"…).
- `cambios` (solo en EDITAR, CA-P8-58): `[{ campo, anterior, nuevo }]` con etiquetas y valores en palabras (nombres de catálogo, pesos, Sí/No). Los textos largos (descripciones) se muestran como "(texto modificado)". Nunca contraseñas, tokens, documentos, observaciones de venta, nota de reserva ni datos de clientes.
- Asesor o comprador: 403; sin sesión: 401 (CA-P8-60).

## 11. Recuperación asistida de contraseña (HU-063, D-73 D)

### `POST /api/admin/usuarios/{id}/enlace-recuperacion`

Sin cuerpo. El administrador confirma antes la identidad de la persona (fuera del sistema).

| Caso | Respuesta |
|---|---|
| usuario inexistente | 404 "Usuario no encontrado." |
| el propio administrador | 403 "No puedes generar un enlace para tu propia cuenta." |
| otra cuenta de administrador | 403 "No se puede modificar una cuenta de administrador." (mismo criterio que P2) |
| cuenta no ACTIVA (pendiente, rechazada o bloqueada) | 409 "Solo se puede generar un enlace para una cuenta activa." (**PROPUESTO**, ver "Pendiente") |
| éxito | 200, `Cache-Control: no-store` |

```json
{
  "enlace": "http://localhost:5173/#/restablecer-contrasena?codigo=Q2ZESjhB...",
  "venceEn": "2026-11-13T16:00:00Z",
  "mensaje": "Envía este enlace por WhatsApp a la persona después de confirmar su identidad. Sirve una sola vez y vence en 60 minutos."
}
```

- El enlace vence 60 minutos después de generarse y sirve **una sola vez**. Generar otro invalida el anterior.
- Base del enlace: configuración `Recuperacion:UrlPortal` (por defecto `http://localhost:5173`). El código va en el fragmento (`#`), que el navegador no envía a ningún servidor ni queda en registros de acceso.
- **No se guarda el token**: el código es un token de Identity protegido con DataProtection (cifrado y firmado) que contiene el usuario, la hora de emisión y el `SecurityStamp`. Al usarse o al generar uno nuevo, el `SecurityStamp` cambia y el código deja de valer. Sin tabla nueva ni migración.
- Auditoría: `auditoria_eventos` `entidad = "Usuario"`, `entidad_id` = usuario, `accion = "GENERAR_ENLACE_RECUPERACION"`, actor = administrador, `datos_nuevos = { venceEn }`. Nunca el enlace ni el código, tampoco en los logs.

### `POST /api/auth/restablecer-contrasena`

Anónima. Cuerpo `{ "codigo": "...", "password": "..." }`.

| Caso | Respuesta |
|---|---|
| contraseña que no cumple las reglas de P1 (8 a 128, mayúscula, minúscula y número) | 400 "Revisa los datos marcados." con `errores.password` (mensajes del registro) |
| código vacío, mal formado, vencido, ya usado, reemplazado por otro, o de una cuenta que ya no está ACTIVA | 400 "El enlace no es válido o ya venció. Pide uno nuevo al administrador." con `errores.codigo` (no se distingue la causa) |
| más de 5 peticiones en 15 minutos desde la misma IP (configurable en `Recuperacion:LimiteVentana` y `Recuperacion:VentanaMinutos`) | 429 "Demasiados intentos. Espera unos minutos e intenta de nuevo." |
| éxito | 200 `{ "mensaje": "Listo. Tu contraseña cambió; inicia sesión con la nueva." }` |

- Al usarlo: contraseña nueva, `SessionVersion + 1` (cierra todas las sesiones, D-12), el código deja de valer y se quita un bloqueo temporal por intentos fallidos (HU-062).
- Auditoría: `entidad = "Usuario"`, `accion = "RESTABLECER_CONTRASENA"`, actor = el propio usuario, sin datos. Log `Auditoria restablecer_contrasena usuarioId=...`, nunca el código ni la contraseña.
- Límite propio (marca `LimitePeticionesRecuperacion`), separado del cupo de contactos y avisos.

---

## Pendiente (necesita decisión)

1. **Cuentas no ACTIVA (§11)**: el contrato no deja generar el enlace para un asesor pendiente, rechazado o bloqueado (409), porque restablecer la contraseña no le daría acceso y podría confundirse con un desbloqueo. Si la responsable prefiere permitirlo (p. ej. para un asesor pendiente que olvidó la contraseña antes de la aprobación), es un cambio de una línea.
2. **Recuperación de un administrador (§11)**: ningún administrador puede generar enlaces para otro administrador (mismo criterio que P2 "No se puede modificar una cuenta de administrador"). Un administrador que olvide su contraseña sigue sin salida dentro del sistema; requiere apoyo técnico.
3. **Ruta anónima nueva**: CA-P8-69 dice "ninguna anónima nueva" para los reportes; la recuperación (D-73 D) sí necesita una (`POST /api/auth/restablecer-contrasena`), con límite de peticiones. Se registra en `SesionTests.Inventario_de_endpoints`.
4. **Pantalla pública del enlace**: el frontend debe crear `#/restablecer-contrasena` (lee `codigo` del fragmento y envía el formulario). Este contrato no toca el frontend.

## Cambios al contrato

- 2026-11-13 (backend, al programar): §6, solo suman los pagos `REGISTRADO` (antes decía "no anulados"), para que el total coincida con `GET /api/admin/suscripciones/{id}/pagos` de P7; los pagos del script 002 no suman. §10, en el evento RESERVAR del vehículo `motivo` va siempre `null`: ese motivo es la nota de reserva de D-28, que puede nombrar al comprador.
