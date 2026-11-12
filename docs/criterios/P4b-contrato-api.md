# P4b — Contrato de la API: publicaciones, destacados, venta y reasignación

Fijado por el coordinador antes de programar. Se mantienen el formato `{ error, errores? }`, la auditoría y los contratos de P1 a P4p.

**Trazabilidad** (matriz oficial de 87 HU, D-1):

| HU | Tema | Regla |
|---|---|---|
| HU-023 | Crear publicación | RN-CO-001: plan activo |
| HU-024 | Modificar | RN-CO-002: propietario |
| HU-025 | Inactivar | RN-CO-003: sin eliminar |
| HU-026 | Renovar | RN-SU-002 |
| HU-027 | Destacar | RN-CO-004, modificado por D-38 |
| HU-028 | Vencimiento automático | RN-CO-005 |
| HU-064 | Marcar vendido | RN-GC-001 |
| HU-065 | Mis publicaciones | RN-GC-002 |

También aplican RN-VE-014 (vendido oculta) y las decisiones D-21, D-22, D-33, D-34 y D-38 a D-41. Base: §15 y la minuta (venta y reversión).

## Reglas de negocio de P4b

- **Una publicación por vehículo.** Un vehículo tiene como máximo **una** publicación no eliminada.
- **Vehículo y publicación.**
  - El precio y las fotos que se muestran son los **del vehículo** (inventario P4a).
  - La publicación aporta el `titulo` (5 a 180 caracteres), la `descripcionComercial` (hasta 4000) y si es `negociable`.
  - El portal pasa a leer el precio y las fotos del vehículo. Si el vehículo no tiene fotos, que es el caso de los DEMO, usa `fotos_publicacion`.
- **Cupo (D-40).** Cuentan las publicaciones del asesor **en cualquier estado salvo ELIMINADA**: publicadas, inactivas, vencidas y vendidas. Crear una cuando `cupo usado ≥ limite_publicaciones` del plan vigente da 409: "Ya usas las N publicaciones de tu plan. Elimina una para crear otra.". **Solo eliminar** libera cupo.
- **Plan (RN-CO-001, D-21).** Crear, reactivar y renovar exigen una suscripción vigente del asesor dueño (ACTIVA o REACTIVADA, y `fin` posterior a ahora). Si no la hay, 409: "No tienes un plan activo. Comunícate con el administrador.".
- **Vigencia (D-41).** La publicación está vigente mientras lo esté el plan de su asesor.
  - Al publicar, reactivar o renovar se escribe `historial_vigencias` con `inicio = ahora`, `fin = suscripcion.fin_vigencia` y su `suscripcion_id`, como historial.
  - Cuando el administrador renueva el plan, aunque sea con otro plan, el tiempo se suma al final del actual (P4p) y las publicaciones siguen visibles sin hacer nada.
  - **Vencimiento automático (HU-028):** cuando el plan vence, sus publicaciones dejan de verse solas, sin ningún proceso automático. En el inventario aparecen como "Vencida".
- **Nueva regla de visibilidad en el portal.** Sustituye las condiciones 3 y 6 de P3:
  - `estado_publicacion = PUBLICADA` y estado comercial distinto de VENDIDO;
  - el **asesor dueño tiene una suscripción vigente**;
  - existe al menos una fila en `historial_vigencias` de la publicación con `inicio <= ahora`;
  - se mantienen las condiciones 4 (asesor ACTIVA), 5 (título y precio) y 7 (vehículo ACTIVO o RESERVADO).
  Las publicaciones DEMO siguen visibles mientras su suscripción DEMO esté vigente.
- **Requisitos para publicar.** El vehículo debe estar ACTIVO o RESERVADO, tener al menos una foto (D-30), un precio y ninguna otra publicación no eliminada.

## Estados de la publicación (`estado_publicacion`)

| Acción | Quién | Desde | Hacia | Requisitos |
|---|---|---|---|---|
| crear y publicar | dueño o admin | — | PUBLICADA | plan vigente, cupo y vehículo publicable |
| inactivar (HU-025) | dueño o admin | PUBLICADA | OCULTA | motivo de 5 a 500 (`motivo_ocultamiento`), `ocultada_en`; quita su destacado |
| reactivar | dueño o admin | OCULTA | PUBLICADA | plan vigente y vehículo publicable; no consume cupo nuevo, porque ya cuenta |
| renovar (HU-026) | dueño o admin | PUBLICADA vencida o vigente | PUBLICADA | plan vigente; escribe una vigencia nueva hasta el fin del plan |
| eliminar (D-40) | dueño o admin | OCULTA o VENDIDA | ELIMINADA | confirmación; `eliminada_en`; **libera cupo**; deja de verse en "Mis publicaciones"; no borra filas |

**Editar (HU-024):** título, descripción y negociable, en cualquier estado salvo ELIMINADA. El precio se cambia en el vehículo (P4a).

**D-33:** con una publicación PUBLICADA, el cambio de categoría del vehículo sigue bloqueado. Para cambiarla, primero se inactiva la publicación.

## Destacados (D-38, sustituye "Administrador" en HU-027)

- El plan define `limite_destacados`, un entero de 0 a 20 que se administra en la pantalla de Planes de P4p.
- El **asesor dueño**, o el administrador, **marca o quita** el destacado de una publicación vigente.
- Puede tener **como máximo `limite_destacados` destacadas a la vez** y puede cambiar cuáles cuantas veces quiera. Al llegar al límite, destacar otra da 409: "Tu plan permite N destacado(s). Quita uno para destacar otra.".
- Un destacado usa `publicaciones_destacadas` con `estado = ACTIVA`, `inicio = ahora` y `fin = fin del plan`. Quitarlo pone `estado = FINALIZADA` y `fin = ahora`.
- Al inactivar, vender o eliminar, y al vencer el plan, deja de contar y deja de verse.

## Venta (HU-064, D-22, D-39) y su reversión (minuta)

**Marcar vendido** (dueño o admin), sobre un vehículo ACTIVO o RESERVADO:
- Datos que se piden:
  - `fechaVenta`: hoy o anterior, sin ser anterior a la creación del vehículo;
  - `precioFinalCop`: entero mayor que 0;
  - `origen`: uno de `PORTAL`, `CONTACTO_ASESOR`, `REFERIDO`, `OTRO`;
  - `observaciones`: hasta 1000 caracteres, obligatorias si el origen es OTRO.
- Efecto:
  - el vehículo pasa a VENDIDO y se borra su nota de reserva;
  - su publicación pasa a estado comercial VENDIDO y sale del portal (RN-VE-014);
  - se quita su destacado;
  - se escribe una fila en la tabla nueva `ventas_vehiculo`.

**Revertir la venta** (dueño o admin, con motivo de 5 a 500):
- El vehículo vuelve a ACTIVO.
- La publicación vuelve a DISPONIBLE pero queda **OCULTA**. Para volver a mostrarla hay que reactivarla, con plan y confirmación (minuta: "reactivación requiere confirmación y cupo").
- La venta queda marcada como revertida, con quién, cuándo y por qué.

## Reasignar un vehículo (D-34)

- Solo el **administrador**, con motivo obligatorio de 5 a 500.
- El vehículo y su publicación pasan a otro asesor ACTIVA.
- El asesor destino debe tener cupo **si** la publicación cuenta, es decir, si no está eliminada. Si no tiene cupo, 409.
- Los destacados se quitan.
- Se audita: quién, cuándo, de qué asesor a cuál y el motivo.

## Migración `database/migraciones/012_p4b_publicaciones.sql` (+ `_SUBIR.sql`)

- `planes.limite_destacados INT NOT NULL DEFAULT 0`, con CHECK entre 0 y 20.
- Tabla `ventas_vehiculo`:
  - `id`, `vehiculo_id` (FK), `publicacion_id` NULL (FK);
  - `fecha_venta DATE`, `precio_final_cop DECIMAL(15,2)` con CHECK > 0;
  - `origen VARCHAR(20)` con CHECK de los 4 valores, `observaciones VARCHAR(1000)` NULL;
  - `registrada_por`, `registrada_en`;
  - `revertida_por` NULL, `revertida_en` NULL, `motivo_reversion` NULL.
- Si la restricción de `publicaciones_destacadas.estado` no admite FINALIZADA, se amplía.
- La sección BAJAR, comentada, elimina solo lo añadido. La parte SUBIR va en un archivo aparte, sin consultas a information_schema (D-16).

## Endpoints

Rol `AsesorOAdministrador`. El asesor solo ve y toca lo suyo; lo ajeno da 404.

| Método | Ruta | Uso |
|---|---|---|
| GET | `/api/publicaciones?estado=&q=&pagina=&tamano=&asesorId=` | "Mis publicaciones" (HU-065). El administrador ve todas |
| GET | `/api/publicaciones/cupo` | Devuelve `{ limitePublicaciones, usadas, limiteDestacados, destacadasUsadas, planVigente, finPlan }` |
| GET | `/api/publicaciones/{id}` | Detalle |
| POST | `/api/publicaciones` | Cuerpo `{ vehiculoId, titulo, descripcionComercial?, negociable }`. Crea y publica |
| PUT | `/api/publicaciones/{id}` | Editar |
| POST | `/api/publicaciones/{id}/inactivar` | Cuerpo `{ motivo }` |
| POST | `/api/publicaciones/{id}/reactivar`, `/renovar` y `/eliminar` | |
| POST | `/api/publicaciones/{id}/destacar` y `/quitar-destacado` | |
| POST | `/api/inventario/vehiculos/{id}/vender` | Cuerpo `{ fechaVenta, precioFinalCop, origen, observaciones? }` |
| POST | `/api/inventario/vehiculos/{id}/revertir-venta` | Cuerpo `{ motivo }` |
| POST | `/api/admin/vehiculos/{id}/reasignar` | Cuerpo `{ asesorId, motivo }`. Solo el administrador |

- El **listado y el detalle** devuelven: `id`, `vehiculoId`, título del vehículo, `titulo`, `estado` (PUBLICADA, OCULTA, VENDIDA, ELIMINADA), `estadoEfectivo` (que añade VENCIDA), `destacada`, `vigenteHasta`, `slugPublico`, `fotoPortada`, `precioCop`, `motivoOcultamiento`, `publicadaEn` y `asesor` (solo para el administrador).
- El `slug_publico` se genera a partir de la marca, el modelo, el año y el id, y es único.
- **Códigos de respuesta:** 400 con `errores`, 404 y 409 con los mensajes de arriba. Las transiciones inválidas dan 409 "No se puede <acción> una publicación en estado <estado>.". 401 y 403 como siempre.
- La **auditoría** usa las entidades `Publicacion`, `Venta` y `Vehiculo` (REASIGNAR), con los datos antes y después.

## Interfaz

- **Inventario:**
  - en cada vehículo sin publicación, el botón **"Publicar"**, que abre un formulario con título, descripción y negociable (con asteriscos, D-31);
  - si el vehículo ya tiene publicación, el estado de esta y un enlace;
  - los botones **"Marcar vendido"** (formulario con fecha, precio final, origen de la lista y observaciones) y "Revertir venta" (con motivo).
- **"Mis publicaciones"** (`#/publicaciones`), para el asesor y, completa, para el administrador:
  - el contador "Publicaciones: usadas de N · Destacados: usados de M" y la fecha de fin del plan;
  - una lista con su estado en texto (Publicada, Inactiva, Vencida, Vendida) y las acciones válidas: Editar, Inactivar (con motivo), Reactivar, Renovar, Destacar o Quitar destacado, Eliminar (con confirmación) y "Ver en el portal";
  - avisos claros cuando falta plan, cupo o destacados.
- **Administrador:** en el detalle de un vehículo del inventario, la acción "Reasignar a otro asesor", con motivo. En la pantalla de Planes, el campo "Destacados incluidos".
- **Portal:** la nueva regla de visibilidad, y el precio y las fotos del vehículo.

## Fuera de P4b

- Leads y contactos (P5).
- Pagos y notificaciones.
- Reportes de ventas (P8). El campo origen ya queda listo para ellos.
