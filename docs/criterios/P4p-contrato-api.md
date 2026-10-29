# P4p — Contrato de la API: planes, suscripciones y catálogos

Fijado por el coordinador antes de programar. Se mantienen el formato común de error `{ error, errores? }`, la auditoría en `auditoria_eventos` y los contratos de P1 a P4a.

**Trazabilidad** (matriz oficial de 87 HU, D-1):
- **Planes:** HU-049 Crear plan, HU-050 Modificar plan (RN-PL-001) y HU-051 Activar o inactivar plan (RN-PL-002).
- **Suscripciones:** HU-066 Consultar mi suscripción (RN-SU-001), HU-067 Renovar (RN-SU-002), HU-068 Suspender (RN-SU-003) y HU-069 Reactivar (RN-SU-004). Vigencia según RN-SU-005.
- **Catálogos:** HU-082 Marcas (RN-PA-002), HU-083 Modelos (RN-PA-003) y HU-084 Tipos de carrocería (RN-PA-004).
- **Decisiones:** D-4, D-21, D-26, D-27, D-32, D-35, D-36 y D-37.

Pagos en línea, comprobantes y notificaciones (HU-081) quedan **fuera** de P4p. El administrador activa a mano cuando confirma el pago (D-26).

## Migración `database/migraciones/011_p4p_estados_suscripcion.sql` (+ `_SUBIR.sql`)

- Cambia la restricción `CK_Sus_Estado` para que admita `('PENDIENTE','ACTIVA','SUSPENDIDA','REACTIVADA','VENCIDA','CANCELADA','RECHAZADA')`. Estos son los estados de D-4, más los que ya existían.
- Es reversible. La sección BAJAR vuelve a la restricción original y antes comprueba que no haya filas con SUSPENDIDA o REACTIVADA.
- No toca datos.
- La parte SUBIR va en un archivo aparte para phpMyAdmin, sin consultas a information_schema (D-16).

## Reglas de suscripción

- **Vigencia (D-35):** cada mes del plan da 30 días: `dias = 30 × planes.duracion_meses`.
- **Vigente** = estado `ACTIVA` o `REACTIVADA`, `inicio_vigencia <= ahora` y `fin_vigencia > ahora`.
- **Vencida** se calcula al leer, cuando el estado es `ACTIVA`/`REACTIVADA` y `fin_vigencia <= ahora`. En las respuestas sale `estadoEfectivo: "VENCIDA"` y no hace falta ningún proceso automático. La columna puede quedarse como ACTIVA: P4b o P7 podrá marcarla.
- Un asesor tiene **como máximo una suscripción viva**, es decir, en estado ACTIVA, REACTIVADA o SUSPENDIDA.
- El catálogo público (condición 6 de P3) acepta `ACTIVA` **o** `REACTIVADA`. Una suscripción SUSPENDIDA oculta las publicaciones del asesor.

| Acción | Quién | Desde | Efecto |
|---|---|---|---|
| asignar | admin | asesor sin suscripción viva | Crea una suscripción ACTIVA con `inicio = ahora`, `fin = ahora + días del plan`, `aprobado_por` y `aprobado_en`. El plan debe estar activo |
| renovar (D-36, solo admin) | admin | ACTIVA, REACTIVADA o vencida | `fin = max(ahora, fin) + días del plan` (por defecto, el plan actual). Si está vencida, vuelve a ACTIVA desde ahora |
| cambiar de plan | admin | viva | Termina la actual (CANCELADA, `fin = ahora`) y crea una nueva ACTIVA con el plan nuevo, en una transacción |
| suspender | admin | ACTIVA o REACTIVADA | Pasa a SUSPENDIDA. Motivo obligatorio (5–500) |
| reactivar | admin | SUSPENDIDA | Pasa a REACTIVADA. **No** extiende la fecha de fin (PROPUESTO) |
| cancelar | admin | viva | Pasa a CANCELADA, `fin = ahora` si era posterior. Motivo obligatorio |

Cada acción se audita con la entidad `Suscripcion`, la acción en mayúsculas, el estado, el plan y las fechas antes y después, y el motivo.

## Reglas de planes (HU-049 a HU-051)

- **Campos:**
  - `nombre`: 2 a 100 caracteres, único sin distinguir mayúsculas.
  - `precioCop`: entero ≥ 0 (un plan de prueba puede ser 0).
  - `duracionMeses`: entero de 1 a 36.
  - `limitePublicaciones`: entero de 1 a 1000.
  - `esPrueba`: booleano.
- **Editar** no cambia las suscripciones en curso. El plan nuevo se aplica al asignar o renovar.
- **Inactivar** impide asignarlo o renovarlo con él. Las suscripciones que ya lo usan siguen.
- Un plan **nunca se borra**.

## Catálogos (D-37: autonomía completa del administrador)

Catálogos administrables: `marcas`, `lineas` (modelos, con sus categorías de `lineas_tipos`), `tipos_carroceria`, `materiales_carroceria`, `tipos_maquina` y `combustibles`.

- **Crear:**
  - nombre de 1 a 100 caracteres tras recortar;
  - único en su catálogo sin distinguir mayúsculas ni tildes, o dentro de su marca en el caso de las líneas;
  - un duplicado da 409 "Ya existe … con ese nombre.".
- **Renombrar:** mismas validaciones. Los vehículos que lo usan muestran el nombre nuevo.
- **Activar o desactivar:** un elemento desactivado no se ofrece en los formularios ni en los filtros del portal. Los vehículos que ya lo usan **lo conservan** y siguen mostrándose.
- **No se borra nada.** Así se conserva el historial y no se rompen vehículos.
- **Modelos:**
  - se crean dentro de una marca;
  - `tipoIds` es obligatorio y lleva al menos una de las 4 categorías (D-32);
  - el administrador puede cambiar las categorías;
  - si quita una categoría que usan vehículos, se permite y la respuesta avisa: "N vehículos usan este modelo en una categoría que ya no tiene; se corregirán al editarlos.".
- **Fuera del alcance:**
  - las **categorías** (`tipos_vehiculo`), porque las reglas del formulario dependen de sus nombres (D-17);
  - los **departamentos y municipios**, que son la DIVIPOLA oficial y se actualizan con un script (`docs/GUIA-CATALOGOS.md`).
- Cada cambio se audita con la entidad `Catalogo`, `entidad_id = "<catalogo>:<id>"`, la acción CREAR, RENOMBRAR, ACTIVAR, DESACTIVAR o CATEGORIAS, y los datos antes y después.

## Endpoints (`SoloAdministrador`, salvo que se indique otra cosa)

**Planes**

| Método | Ruta | Respuesta |
|---|---|---|
| GET | `/api/admin/planes?activo=` | `{ items: [{ id, nombre, precioCop, duracionMeses, diasVigencia, limitePublicaciones, esPrueba, activo, suscripcionesVivas }] }` |
| POST | `/api/admin/planes` | 201 |
| PUT | `/api/admin/planes/{id}` | 200 |
| POST | `/api/admin/planes/{id}/activar` y `/inactivar` | 200 |

**Suscripciones**

| Método | Ruta | Detalle |
|---|---|---|
| GET | `/api/admin/suscripciones?asesorId=&estado=&pagina=&tamano=` | Lista con el asesor (`perfiles_asesor.id` y nombre), el plan, el estado, el estado efectivo, las fechas y los días restantes |
| GET | `/api/admin/asesores/{asesorId}/suscripcion` | La suscripción viva o la última; 404 si nunca tuvo |
| POST | `/api/admin/asesores/{asesorId}/suscripcion` | Asignar. Cuerpo `{ planId }`. 201 o 409 si ya tiene una viva. El asesor debe estar ACTIVA |
| POST | `/api/admin/suscripciones/{id}/renovar` | Cuerpo `{ planId? }` |
| POST | `/api/admin/suscripciones/{id}/cambiar-plan` | Cuerpo `{ planId }` |
| POST | `/api/admin/suscripciones/{id}/suspender`, `/reactivar` y `/cancelar` | Cuerpo `{ motivo }` |

**Mi suscripción**

| Método | Ruta | Detalle |
|---|---|---|
| GET | `/api/asesor/suscripcion` | Política `SoloAsesor`. Devuelve `{ plan: { nombre, limitePublicaciones, diasVigencia }, estado, estadoEfectivo, inicioVigencia, finVigencia, diasRestantes, vigente }`. Si no tiene suscripción: 200 `{ suscripcion: null, mensaje: "Aún no tienes un plan activo. Comunícate con el administrador." }` |

**Catálogos**

| Método | Ruta | Detalle |
|---|---|---|
| GET | `/api/admin/catalogos/{catalogo}` | `catalogo` ∈ `marcas`, `carrocerias`, `materiales`, `tipos-maquina`, `combustibles`. Lista `{ id, nombre, activo, vehiculos }`; para `marcas`, además `lineas: [{ id, nombre, activo, tipoIds, vehiculos }]` |
| POST | `/api/admin/catalogos/{catalogo}` | Crear. Cuerpo `{ nombre }` |
| PUT | `/api/admin/catalogos/{catalogo}/{id}` | Renombrar. Cuerpo `{ nombre }` |
| POST | `/api/admin/catalogos/{catalogo}/{id}/activar` y `/desactivar` | |
| POST | `/api/admin/catalogos/marcas/{marcaId}/lineas` | Crear modelo. Cuerpo `{ nombre, tipoIds }` |
| PUT | `/api/admin/catalogos/lineas/{id}` | Cuerpo `{ nombre?, tipoIds? }` |
| POST | `/api/admin/catalogos/lineas/{id}/activar` y `/desactivar` | |

**Códigos de respuesta:** 400 con `errores`, 404 "No encontrado.", 409 por duplicado o por una transición inválida ("No se puede <acción> una suscripción en estado <estado>."), 401 y 403 como siempre.

## Interfaz (administrador, dentro del panel)

En "Mi cuenta", el administrador tiene pestañas o enlaces: **Usuarios** (la de hoy), **Planes**, **Suscripciones** y **Catálogos**.

- **Planes:** tabla y formulario de crear o editar, con asteriscos (D-31) y botones Activar/Inactivar.
- **Suscripciones:**
  - lista filtrable;
  - en cada asesor aprobado, el botón "Asignar plan";
  - en cada suscripción, Renovar, Cambiar plan, Suspender (con motivo), Reactivar y Cancelar (con motivo), mostrando solo las acciones válidas;
  - fechas y días restantes, con el estado en texto (Activa, Suspendida, Reactivada, Vencida, Cancelada).
- **Catálogos:**
  - selector de catálogo;
  - lista con buscador, Crear, Renombrar, y Activar/Desactivar con confirmación;
  - el número de vehículos que lo usan;
  - en las marcas, sus modelos, con casillas de las 4 categorías.

**Asesor:** en "Mi cuenta", la tarjeta "Mi suscripción" muestra el plan, el estado, la fecha de vencimiento, los días restantes y el cupo. Sin plan, aparece el mensaje del contrato.

Todo es responsivo y accesible, como en P2–P4a.

## Fuera de P4p

- Exigir plan y cupo al publicar (P4b).
- Pagos, notificaciones de vencimiento (HU-081) y marcar las vencidas con un proceso automático.
- Las categorías y la DIVIPOLA (ver arriba).
