# P4a — Criterios de aceptación: inventario de vehículos

| Dato | Valor |
|---|---|
| Ítem de backlog | P4a — Inventario de vehículos (registrar, editar, estados, fotos, inventario propio y general) |
| Rama | `feature/p4a-inventario` |
| Numeración de HU | Matriz oficial de 87 HU (D-1), EP-02 Gestión de Vehículos, HU-013 a HU-022 |
| Contrato de API | `docs/criterios/P4a-contrato-api.md` (fijado; no se modifica). Formato común de error `{ error, errores? }` |
| Estado real de partida | Backend: commit `d158868` (endpoints de inventario, fotos, `/media`, migración 004 **sin aplicar**). Sin clases de prueba propias del inventario en `backend/Camion.Api.Tests`. Frontend: archivos de inventario en curso, sin commit. `ESTADO-REAL.md` no menciona P4a (auditoría de P0, ver C-P4a-8) |
| Decisiones aplicadas | D-2, D-3, D-4, D-7, D-11, D-16, D-17, D-19 (TOMADAS). D-20, D-22, D-23 (PENDIENTES: lo que dependa de ellas es PROPUESTO o BLOQUEADO) |
| Autor | analista funcional |

## Convenciones

**Marca**
- **CONFIRMADO**: sale literalmente de una RN/HU de la v3.0, de una decisión TOMADA o de `CLAUDE.md`. Respaldo literal: RN-VE-001 "hasta 10 fotografías por vehículo"; RN-VE-002 + D-2 "placa opcional, pero única cuando se registra"; RN-VE-003 "Precio obligatorio. Conservar precio actual y anterior"; RN-VE-004 "Permite inactivar vehículos. Conserva trazabilidad"; RN-VE-005 "Debe registrarse kilometraje. Valor numérico"; RN-VE-007 "Todo vehículo requiere propietario de publicación. Asesor"; RN-VE-008 + D-4 + D-11 estados Activo / Reservado / Inactivo / Vendido; RN-VE-009 "Registrar cambios realizados. Auditoría"; RN-VE-010 "Permitir registrar especificaciones"; RN-CO-009 "Restricción por propietario"; RN-SE-006 "Validar permisos por rol"; D-3 comprador solo lectura; D-17 cuatro categorías y "modelo" = línea; D-19 campos de carrocería (marca opcional, medidas, material, tipo de carrocería, sin placa ni kilometraje) y de máquina (horas de uso, tipo, capacidad, marca, modelo y año).
- **PROPUESTO**: códigos HTTP, textos, topes numéricos, formato de placa (5–7), transiciones concretas (D-11 las deja PROPUESTAS), 404 en lugar de 403, todo lo de fotos y `/media` (D-20 PENDIENTE), valores de las listas de materiales, tipos de máquina y carrocería (D-19), condición 7, diseño de la interfaz.
- **CONFIRMADO (regla) / PROPUESTO (forma)** cuando la regla está respaldada pero no su forma concreta.

**Cómo se prueba**
- **API**: xUnit + `WebApplicationFactory` con SQLite en memoria (nunca `camion_ventas`). Fotos en una carpeta temporal configurada en `Media:Ruta`.
- **COMP**: Vitest + Testing Library en `frontend/tests` (`fetch`, `localStorage`, `location.hash` simulados).
- **MAN**: aplicación arrancada contra MariaDB. Queda **BLOQUEADO (D-23)** hasta que la responsable aplique la migración 004; la subida real de fotos, además, depende de D-20.

**Estado actual**: IMPL = código presente en `d158868` sin prueba propia (no verificado); PARCIAL = frontend en curso sin commit; FALTA; BLOQUEADO.

**Datos de referencia** (ficticios, sembrados en SQLite; año actual del servidor = 2026)
- Asesores: **A1** y **A2** `ACTIVA`; **A3** `PENDIENTE`; **A4** `BLOQUEADA`. `asesorId` = `perfiles_asesor.id`. Administrador "admin.pruebas@ejemplo.com"; comprador "ana.perez@ejemplo.com".
- Catálogos activos: tipos Carro, Camión, Carrocería, Maquinaria; marcas Chevrolet (NPR, NHR) y Kenworth (T800); **Dina** `activo = 0`; carrocerías Furgón y Estacas; material Aluminio; tipo de máquina Retroexcavadora; combustible Diésel; municipio Bogotá.
- **V1** Camión de A1, Chevrolet NPR 2020, 85.000 km, placa `ABC123`, 120.000.000, ACTIVO. **V2** Carro de A2. **VV** Camión de A1 en VENDIDO (sembrado directamente, D-22). **VC** Carrocería de A1 (Furgón, sin marca).

---

## HU-013 — Registrar vehículo (RN-VE-007) · `POST /api/inventario/vehiculos`

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-013-01 | A1 con sesión | Envía un Camión válido (tipo, marca, línea, año 2020, km, municipio, precio) sin `asesorId` | **201** con el detalle; `estado = ACTIVO`; el propietario es A1 aunque no lo indique | CONFIRMADO (RN-VE-007) / PROPUESTO (forma) | API | IMPL |
| CA-013-02 | A1 con sesión | Envía `asesorId` = A2 | El vehículo queda a nombre de A1 (el asesor no puede crear a nombre de otro) o 400; nunca a nombre de A2 (hueco, C-P4a-7) | CONFIRMADO (regla: RN-CO-009) / PROPUESTO (forma) | API | IMPL |
| CA-013-03 | Administrador | Crea con `asesorId` = A1 | **201**; propietario A1 | CONFIRMADO (RN-VE-007, HU-022) / PROPUESTO (forma) | API | IMPL |
| CA-013-04 | Administrador | Crea sin `asesorId`; con A3 (pendiente); con A4 (bloqueado); con id inexistente | **400** `errores.asesorId` = ["Elige un asesor activo."] | CONFIRMADO (RN-VE-007) / PROPUESTO (solo ACTIVA, texto) | API | IMPL |
| CA-013-05 | A1 | Envía (uno por prueba): sin `tipoId`; `tipoId` inexistente; sin `municipioId`; `marcaId` de Dina (inactiva); `lineaId`, `municipioId` o `combustibleId` inexistentes; `carroceriaId`, `materialId` o `tipoMaquinaId` inactivos; y todos a la vez | **400** con `error` = "Revisa los datos marcados." y la clave de cada campo: "Elige la categoría." / "Categoría no válida." / "Elige el municipio." / "Marca no válida." / "Modelo no válido." / "Municipio no válido." / "Combustible no válido." / "Tipo de carrocería no válido." / "Material no válido." / "Tipo de máquina no válido."; todos los errores en una sola respuesta | PROPUESTO | API | IMPL |
| CA-013-06 | A1 | Envía `marcaId` = Kenworth y `lineaId` = NPR | **400** `errores.lineaId` = ["El modelo no corresponde a la marca elegida."] | PROPUESTO | API | IMPL |
| CA-013-07 | A1 crea V1 | Se revisa `auditoria_eventos` | Una fila entidad `Vehiculo`, acción `CREAR`, con el id del vehículo y el usuario que lo creó | CONFIRMADO (RN-VE-009) / PROPUESTO (forma) | API | IMPL |
| CA-013-08 | A1 | Envía un cuerpo que no es JSON o un `tipoId` de texto | **400** con el formato común; nunca 500 | PROPUESTO | API | IMPL |

## Reglas por categoría (D-17, D-19; tabla del contrato)

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-CAT-01 | A1 | Crea un Carro y un Camión sin marca; sin año; sin kilometraje | **400**: "Elige la marca." / año con "Escribe un año entre 1950 y 2027." / "Escribe el kilometraje (número entero mayor o igual a 0)." | CONFIRMADO (RN-VE-005 km; D-17) / PROPUESTO (año obligatorio, textos) | API | IMPL |
| CA-CAT-02 | A1 | Envía `modeloAnio` = 1949, 2028, 1950 y 2027 | 1949 y 2028 → **400**; 1950 y 2027 → aceptados (límites inclusivos; tope = año actual + 1) | PROPUESTO | API | IMPL |
| CA-CAT-03 | A1 | Crea una Carrocería con solo tipo, `carroceriaId`, municipio y precio (sin marca ni año) | **201**; `marca`, `modeloAnio` y `placa` en null; `kilometraje` guardado como 0 | CONFIRMADO (D-19: marca opcional, sin placa ni km) / PROPUESTO (año opcional, km 0) | API | IMPL |
| CA-CAT-04 | A1 | Crea una Carrocería sin `carroceriaId` | **400** `errores.carroceriaId` = ["Elige el tipo de carrocería."] | CONFIRMADO (D-19 tipo de carrocería) / PROPUESTO (obligatorio, texto) | API | IMPL |
| CA-CAT-05 | A1 | Crea una Carrocería con `kilometraje`, `horasUso`, `placa`, `tipoMaquinaId`, `combustibleId`, `cilindrajeCc` o `pesoBrutoKg` (uno por prueba) | **400** en ese campo: "Este dato no aplica a Carrocería." | CONFIRMADO (D-19: sin placa ni km) / PROPUESTO (resto, texto) | API | IMPL |
| CA-CAT-06 | A1 | Crea una Carrocería con `largoM` 6.5, `anchoM` 2.4, `altoM` 2.6, `materialId` Aluminio y `capacidadCargaKg` | **201** y el detalle devuelve esos valores | CONFIRMADO (D-19 medidas, material) / PROPUESTO (capacidad en carrocería, ver C-P4a-6) | API | IMPL |
| CA-CAT-07 | A1 | Crea una Maquinaria con marca, año, `tipoMaquinaId`, `horasUso` 1200, `capacidadCargaKg`, municipio y precio | **201**; `kilometraje` = 0 en la base y `horasUso` = 1200 | CONFIRMADO (D-19 horas, tipo, capacidad, marca y año) / PROPUESTO (forma) | API | IMPL |
| CA-CAT-08 | A1 | Crea una Maquinaria sin `tipoMaquinaId`; sin marca; sin año | **400**: "Elige el tipo de máquina." / "Elige la marca." / mensaje de año | CONFIRMADO (D-19) / PROPUESTO (textos) | API | IMPL |
| CA-CAT-09 | A1 | Crea una Maquinaria con `kilometraje`, `carroceriaId`, `largoM`/`anchoM`/`altoM` o `materialId` (uno por prueba) | **400** en ese campo: "Este dato no aplica a Maquinaria." | CONFIRMADO (D-19 horas en lugar de km) / PROPUESTO (resto) | API | IMPL |
| CA-CAT-10 | A1 | Crea un Carro o Camión con `horasUso`, `tipoMaquinaId`, medidas o `materialId`; y un Carro con `capacidadCargaKg` | **400** "Este dato no aplica a Carro." / "... a Camión." | PROPUESTO | API | IMPL |
| CA-CAT-11 | A1 | Un campo no admitido llega con valor **0** (p. ej. `kilometraje: 0` en Carrocería, `horasUso: 0` en Camión) | **400** igual que con cualquier otro valor; solo `null` o ausencia se aceptan | PROPUESTO (comportamiento del backend) | API | IMPL |
| CA-CAT-12 | A1 | Crea un Camión con `carroceriaId` = Furgón (subtipo) | **201** | CONFIRMADO (D-17 b) / PROPUESTO (forma) | API | IMPL |

## HU-015 — Información complementaria: placa (RN-VE-002, D-2)

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-015-01 | A1 | Crea dos Camiones sin placa (ausente, `null` y `""`) | Ambos **201**; varias filas con placa NULL conviven | CONFIRMADO (D-2) | API | IMPL |
| CA-015-02 | A1 | Envía `placa` = " abc-123 ", "abc 123", "AbC123" | Se guarda `ABC123` (mayúsculas, sin espacios ni guiones) | CONFIRMADO (regla D-2) / PROPUESTO (normalización) | API | IMPL |
| CA-015-03 | V1 con `ABC123` (en otras pruebas V1 INACTIVO, RESERVADO o VENDIDO) | A1 o A2 crea otro vehículo con "abc-123"; en otra prueba edita V2 con esa placa | **409** `{ errores: { placa: ["Esta placa ya está registrada en otro vehículo."] } }`, aunque el otro sea de otro asesor y en cualquier estado | CONFIRMADO (D-2 única) / PROPUESTO (409, texto, todos los estados) | API | IMPL |
| CA-015-04 | V1 | A1 edita V1 manteniendo `ABC123` | **200** (no choca consigo mismo) | CONFIRMADO (D-2) | API | IMPL |
| CA-015-05 | A1 | Envía placas de 4 y de 8 caracteres tras normalizar, "ABC12Ñ", "AB@123" | **400** `errores.placa` = ["Escribe una placa válida (letras y números)."]; 5 y 7 caracteres → aceptadas | PROPUESTO (formato 5–7, ver C-P4a-5) | API | IMPL |
| CA-015-06 | Dos peticiones simultáneas con la misma placa | Llegan a la vez | Una **201** y otra **409**; nunca 500 ni dos filas (índice `UX_Veh_Placa`) | CONFIRMADO (D-2 restricción en base) / PROPUESTO (409) | API | IMPL |

## HU-016 — Precio de venta (RN-VE-003)

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-016-01 | A1 | Crea sin `precioCop`; con 0; con -1; con 1.5; con 10.000.000.001; y con 1 y 10.000.000.000 | Los cinco primeros: **400** `errores.precioCop` = ["Escribe el precio en pesos (número entero mayor que 0 y hasta 10.000.000.000)."]. 1 y 10.000.000.000: **201** con `precioAnteriorCop` = null | CONFIRMADO (RN-VE-003 obligatorio) / PROPUESTO (límites, texto) | API | IMPL |
| CA-016-02 | V1 a 120.000.000 sin precio anterior | A1 edita a 115.000.000 | **200**; `precioCop` = 115.000.000 y `precioAnteriorCop` = 120.000.000 | CONFIRMADO (RN-VE-003) | API | IMPL |
| CA-016-03 | V1 tras CA-016-02 | Edita otra vez a 110.000.000; en otra prueba edita sin cambiar el precio | Primero: anterior = 115.000.000. Sin cambio: el anterior no se altera | CONFIRMADO (RN-VE-003) / PROPUESTO (solo el último anterior) | API | IMPL |

## HU-017 — Kilometraje y horas de uso (RN-VE-005, D-19)

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-017-01 | A1 | Camión con `kilometraje` 0 y 9.999.999; con -1, 10.000.000, 1.5 o "85k" | 0 y 9.999.999 → **201**. El resto → **400** `errores.kilometraje` = ["Escribe el kilometraje (número entero mayor o igual a 0)."] | CONFIRMADO (RN-VE-005 valor numérico) / PROPUESTO (tope, texto) | API | IMPL |
| CA-017-02 | A1 | Maquinaria con `horasUso` 0, 999.999 y sin `horasUso`; con -1, 1.000.000 o 1.5 | Los tres primeros → **201** (opcional). El resto → **400** `errores.horasUso` = ["Escribe las horas de uso (número entero mayor o igual a 0)."] | CONFIRMADO (D-19 horas) / PROPUESTO (opcional, tope, texto) | API | IMPL |
| CA-017-03 | Lista y detalle de un Camión, una Carrocería y una Maquinaria | `GET` lista y detalle | Camión: `kilometraje` numérico, `horasUso` null. Carrocería: ambos null. Maquinaria: `kilometraje` null y `horasUso` con su valor o null | PROPUESTO (contrato `kilometraje|null`) | API | IMPL |

## HU-018 — Características comerciales (RN-VE-010)

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-018-01 | A1 | Camión con combustible, `cilindrajeCc` 4.500, `pesoBrutoKg` 7.500,50, `capacidadCargaKg` y descripción técnica de 4000 caracteres | **201** y el detalle devuelve los mismos valores | CONFIRMADO (RN-VE-010) / PROPUESTO (forma) | API | IMPL |
| CA-018-02 | A1 | `cilindrajeCc` 0, 100.001 o 1.5; 1 y 100.000 | 0, 100.001, 1.5 → **400** "Escribe un cilindraje válido en cc (número entero mayor que 0)."; 1 y 100.000 → aceptados | PROPUESTO | API | IMPL |
| CA-018-03 | A1 | `pesoBrutoKg` o `capacidadCargaKg` 0, negativo o con 3 decimales; Carrocería con `largoM`, `anchoM` o `altoM` 0, negativo o 1.234 | **400** "Escribe un peso válido en kg (mayor que 0, hasta 2 decimales)." / "Escribe una medida válida en metros (mayor que 0, hasta 2 decimales)." | CONFIRMADO (D-19 medidas) / PROPUESTO (forma) | API | IMPL |
| CA-018-04 | A1 | Descripción técnica de 4001 caracteres | **400** "La descripción técnica no puede tener más de 4000 caracteres." | PROPUESTO | API | IMPL |
| CA-018-05 | A1 | Descripción con `<script>alert(1)</script>` | Se guarda como texto; la interfaz la muestra escapada, sin ejecutar | PROPUESTO | API + COMP | IMPL / PARCIAL |

## HU-019 — Editar vehículo (RN-VE-009) · `PUT /api/inventario/vehiculos/{id}`

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-019-01 | V1 de A1 | A1 cambia línea, km y descripción | **200** con el detalle actualizado; `actualizadoEn` avanza | CONFIRMADO (HU-019) / PROPUESTO (forma) | API | IMPL |
| CA-019-02 | V1 | A1 envía `tipoId` = Carro | **400** `errores.tipoId` = ["La categoría de un vehículo no se puede cambiar."] | PROPUESTO | API | IMPL |
| CA-019-03 | V1 | A1 edita cambiando solo km (85.000 → 90.000) y precio | Una fila `EDITAR` con `datos_anteriores` = `{ kilometraje: 85000, precioCop: 120000000 }` y `datos_nuevos` = `{ kilometraje: 90000, precioCop: … }`; ningún otro campo aparece | CONFIRMADO (RN-VE-009) / PROPUESTO (solo cambiados) | API | IMPL |
| CA-019-04 | V1 | A1 envía el mismo cuerpo sin cambios | **200**; no se registra un evento `EDITAR` con listas vacías (o se registra sin campos: hueco, C-P4a-7) | PROPUESTO | API | IMPL |
| CA-019-05 | VV (VENDIDO) | A1 o el administrador lo editan | **409**; la fila no cambia | CONFIRMADO (D-4 Vendido) / PROPUESTO (409) | API | IMPL |
| CA-019-06 | V1 | A1 edita con datos inválidos (reglas de CA-CAT, CA-015, CA-016), incluido un campo de otra categoría (p. ej. `horasUso` en V1 → "Este dato no aplica a Camión.") | Mismas respuestas 400/409 que en la creación; nada se guarda y no hay auditoría | PROPUESTO | API | IMPL |

## HU-020 — Estados del vehículo (RN-VE-004, RN-VE-008, D-4, D-11)

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-020-01 | V1 ACTIVO | A1 llama `/reservar`; después `/liberar` | **200** RESERVADO; después **200** ACTIVO; auditoría `RESERVAR` y `LIBERAR` | CONFIRMADO (D-11 Reservado; "Reservado → Activo") / PROPUESTO (endpoints, quién) | API | IMPL |
| CA-020-02 | V1 ACTIVO y, en otra prueba, RESERVADO | A1 llama `/inactivar` con motivo de 5 y de 500 caracteres | **200** INACTIVO; `motivoInactivacion` guardado; auditoría `INACTIVAR` con el motivo | CONFIRMADO (RN-VE-004, RN-VE-008) / PROPUESTO (longitud) | API | IMPL |
| CA-020-03 | V1 ACTIVO | `/inactivar` sin motivo, con "   ", con 4 y con 501 caracteres | **400** `errores.motivo`; el estado no cambia | PROPUESTO | API | IMPL |
| CA-020-04 | V1 INACTIVO con motivo | A1 llama `/reactivar` | **200** ACTIVO; `motivoInactivacion` = null; el motivo sigue en la auditoría de `INACTIVAR`; hay evento `REACTIVAR` | CONFIRMADO (RN-VE-004 conserva trazabilidad) / PROPUESTO (borrar el motivo) | API | IMPL |
| CA-020-05 | Transiciones no válidas: reservar desde RESERVADO o INACTIVO; liberar desde ACTIVO o INACTIVO; inactivar desde INACTIVO; reactivar desde ACTIVO o RESERVADO | Se llama la acción | **409** "No se puede <acción> un vehículo en estado <estado en minúscula>." (p. ej. "No se puede liberar un vehículo en estado activo."); sin auditoría | PROPUESTO (D-11 transiciones propuestas) | API | IMPL |
| CA-020-06 | VV VENDIDO | Cualquiera de las 4 acciones | **409** con el mensaje anterior ("... en estado vendido."); no existe acción para marcar vendido | CONFIRMADO (D-4) / PROPUESTO (D-22 pendiente) | API | IMPL |
| CA-020-07 | V1 con publicación PUBLICADA | Se reserva o inactiva | La fila de `publicaciones` no cambia (P4b) | PROPUESTO (contrato) | API | IMPL |
| CA-020-08 | V1 | El administrador ejecuta las 4 acciones | Mismas respuestas que el dueño | PROPUESTO (D-11 "dueño o administrador") | API | IMPL |

## HU-021 / HU-022 — Inventario propio y general (RN-CO-009, RN-AD-001) · `GET /api/inventario/vehiculos`

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-021-01 | Vehículos de A1 y A2 | A1 lista | Solo los de A1 (todos los estados, incluido VENDIDO); `total` cuenta solo los suyos; sin clave `asesor` | CONFIRMADO (RN-CO-009 restricción por propietario) / PROPUESTO (forma) | API | IMPL |
| CA-021-02 | A1 | Lista con `asesorId` = A2 | Se ignora: sigue viendo solo lo suyo | CONFIRMADO (regla) / PROPUESTO (ignorar) | API | IMPL |
| CA-021-03 | A1 sin vehículos | Lista | **200** `{ items: [], total: 0, pagina: 1, tamano: 20 }` | PROPUESTO | API | IMPL |
| CA-021-04 | Vehículos variados | Filtra por `estado`, `tipoId`, `marcaId` y `q` ("abc1", "npr", palabra de la descripción) | Se combinan con "y"; `q` busca sin distinguir mayúsculas en placa, marca, línea y descripción | PROPUESTO | API | IMPL |
| CA-021-05 | A1 | `q` de 101 caracteres; `estado` = "PERDIDO"; `pagina` = 0; `tamano` = 0, 101 o "abc" | **400** con la clave del parámetro; `q` de 100 y `tamano` 100 → **200** | PROPUESTO | API | IMPL |
| CA-021-06 | 25 vehículos de A1 | Pide páginas 1 y 2 | 20 + 5, ordenados por `actualizadoEn` descendente, sin repetir ni perder | PROPUESTO | API | IMPL |
| CA-021-07 | Un ítem con 3 fotos y portada en la de orden 2 | Se revisa | Tiene las claves de `ItemInventario`; `fotoPortada` = URL `/media/...` de la portada; `totalFotos` = 3; sin fotos → `fotoPortada` null y `totalFotos` 0 | PROPUESTO | API | IMPL |
| CA-022-01 | Vehículos de A1, A2 y DEMO | El administrador lista | Ve todos; cada ítem lleva `asesor: { id, nombre }` | CONFIRMADO (HU-022 inventario general) / PROPUESTO (forma) | API | IMPL |
| CA-022-02 | El administrador | Lista con `asesorId` = A2 | Solo los de A2 | PROPUESTO | API | IMPL |
| CA-022-03 | Detalle `GET /vehiculos/{id}` | A1 pide V1; el administrador pide V2 | **200** con todos los campos de la tabla de categorías, `precioAnteriorCop`, `motivoInactivacion` y `fotos: [{ id, url, orden, esPortada }]` en orden | PROPUESTO (contrato) | API | IMPL |

## Permisos (RN-SE-006, D-3, D-7)

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-SE006-01 | V2 de A2 | A1 llama `GET`, `PUT`, las 4 acciones de estado, subir, borrar y reordenar fotos de V2 | **404** `{ error: "Vehículo no encontrado." }`, idéntico al de un id inexistente; V2 no cambia | CONFIRMADO (RN-CO-009, D-7 cada asesor lo suyo) / PROPUESTO (404) | API | IMPL |
| CA-SE006-02 | Id inexistente (999999) o no numérico | Cualquier endpoint de vehículo | **404** "Vehículo no encontrado."; nunca 500 | PROPUESTO | API | IMPL |
| CA-SE006-03 | V1 y V2 | El administrador edita, cambia estado y gestiona fotos de ambos | Responde como el dueño; la auditoría registra al administrador como autor | CONFIRMADO (HU-022) / PROPUESTO (forma) | API | IMPL |
| CA-SE006-04 | Comprador con sesión; usuario sin rol | Cualquier endpoint `/api/inventario/*` (incluido `/catalogos`) | **403**; nada cambia | CONFIRMADO (D-3 solo lectura) / PROPUESTO (403) | API | IMPL |
| CA-SE006-05 | Sin token, con token caducado o tras cerrar sesión (D-12) | Cualquier endpoint `/api/inventario/*` | **401** | CONFIRMADO (RN-SE-006) / PROPUESTO (401) | API | IMPL |
| CA-SE006-06 | Asesor PENDIENTE o BLOQUEADO (no puede iniciar sesión, P1) | Usa un token anterior al bloqueo | **401** o **403**; nunca accede al inventario | CONFIRMADO (D-3 aprobación de asesores) / PROPUESTO (código) | API | IMPL |
| CA-SE006-07 | La prueba de inventario de endpoints | Se ejecuta | Todas las rutas `/api/inventario/*` exigen la política `AsesorOAdministrador`; `/media` es la única ruta anónima nueva | PROPUESTO (contrato) | API | IMPL |

## HU-014 — Fotografías (RN-VE-001, D-20 PENDIENTE)

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-014-01 | V1 sin fotos | A1 sube un JPEG, un PNG y un WebP válidos (`archivo`) | **201** `{ id, url, orden, esPortada }` con órdenes 1, 2, 3; solo la primera `esPortada: true`; archivo en `Media:Ruta/vehiculos/<id>/<guid>.<ext>` | CONFIRMADO (RN-VE-001) / PROPUESTO (D-20) | API | IMPL |
| CA-014-02 | A1 | Sube un PDF renombrado a `.jpg`, un GIF, un texto `.png`, un archivo vacío, sin campo `archivo` | **400** "Sube una imagen JPG, PNG o WebP de hasta 5 MB."; nada en disco | PROPUESTO (D-20: firma de bytes) | API | IMPL |
| CA-014-03 | A1 | Sube un JPEG válido con extensión `.png` o sin extensión | **201**; la extensión guardada sale de la firma (`.jpg`) | PROPUESTO | API | IMPL |
| CA-014-04 | A1 | Sube 5 MB exactos; 5 MB + 1 byte; un cuerpo > 6 MB | 5 MB → **201**; 5 MB + 1 → **400** con el texto de CA-014-02; > 6 MB → rechazo por tamaño (400 o 413), nunca 500 | PROPUESTO (D-20) | API | IMPL |
| CA-014-05 | V1 con 10 fotos | Sube la 11.ª | **409** "Ya hay 10 fotos; elimina alguna para subir otra." | CONFIRMADO (RN-VE-001 hasta 10) / PROPUESTO (409, texto) | API | IMPL |
| CA-014-06 | A1 | Sube "../../evil.jpg" o "C:\x.jpg" como nombre original | El nombre guardado es un GUID; el nombre original no aparece en disco, base ni respuesta | PROPUESTO | API | IMPL |
| CA-014-07 | V1 con fotos 1 (portada), 2, 3 | Borra la 2; después borra la 1 | **204**; órdenes renumerados 1, 2 sin huecos; al borrar la portada, la nueva portada es la de orden 1; archivos borrados del disco | PROPUESTO | API | IMPL |
| CA-014-08 | V1 | Borra un `fotoId` inexistente o de V2 | **404** "Foto no encontrada." (o "Vehículo no encontrado." si V2 es de otro asesor) | PROPUESTO | API | IMPL |
| CA-014-09 | V1 con fotos [a, b, c] | `PUT .../fotos/orden` con [c, a, b] | **200** con la lista en ese orden; c es la portada | PROPUESTO | API | IMPL |
| CA-014-10 | V1 con [a, b, c] | Reordena con [a, b]; [a, b, c, x]; [a, a, b]; [] | **400**; el orden no cambia | PROPUESTO | API | IMPL |
| CA-014-11 | VV VENDIDO | Subir, borrar o reordenar | **409** | PROPUESTO | API | IMPL |
| CA-014-12 | Subidas válidas | Se revisa la auditoría | Queda traza de subir, borrar y reordenar fotos (hueco: el contrato no lo dice, C-P4a-7) | CONFIRMADO (RN-VE-009) / PROPUESTO (forma) | API | IMPL (por verificar) |
| CA-014-13 | La app real con 004 aplicada y `Media:Ruta` configurada | La responsable sube, reordena y borra fotos | Los archivos aparecen y desaparecen en `C:\CAMION_media` (fuera del repo) | PROPUESTO (D-20) | MAN | BLOQUEADO (D-20, D-23) |

## Servicio `/media` (D-20)

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-MED-01 | Una foto subida | `GET` a su `url` sin token | **200**, tipo de contenido correcto, `Cache-Control: public, max-age=86400` y `X-Content-Type-Options: nosniff` | PROPUESTO (D-20) | API | IMPL |
| CA-MED-02 | Archivos fuera o dentro de la carpeta | Pide `/media/../appsettings.json`, `/media/%2e%2e/%2e%2e/x.jpg`, `/media/..%5C..%5Cx.jpg`, `/media/C:/Windows/x.jpg`, `/media//etc/x.jpg` | **404** en todos; nunca se sirve nada fuera de `Media:Ruta` | PROPUESTO | API | IMPL |
| CA-MED-03 | Un `.txt`, `.svg`, `.html` o `.gif` dentro de la carpeta; una foto ya borrada | Se pide | **404** (solo jpg, jpeg, png, webp existentes) | PROPUESTO | API | IMPL |
| CA-MED-04 | El frontend en 5173 | Carga una imagen `/media/...` | Llega vía el proxy de Vite | PROPUESTO | MAN | BLOQUEADO (D-23) |

## Catálogos del formulario · `GET /api/inventario/catalogos`

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-FRM-01 | Datos de referencia | A1 la pide | **200** con `tipos`, `marcas` (con `lineas`), `carrocerias`, `materiales`, `tiposMaquina`, `combustibles`, `departamentos` (con `municipios`); sin `asesores`; Dina no aparece. Comprador → **403**; sin token → **401** | PROPUESTO | API | IMPL |
| CA-FRM-02 | Datos de referencia | El administrador la pide | Igual más `asesores` = solo A1 y A2 (no A3 ni A4) | CONFIRMADO (RN-VE-007) / PROPUESTO (forma) | API | IMPL |
| CA-FRM-03 | Base tras la migración 004 | Se revisan las listas | Materiales: Aluminio, Acero, Madera, Fibra de vidrio. Tipos de máquina: Retroexcavadora, Montacargas, Minicargador, Excavadora, Motoniveladora, Vibrocompactador. Carrocería incluye Furgón, Estacas, Volco, Platón, Tanque, Refrigerada | PROPUESTO (D-19: valores a confirmar por la responsable) | MAN | BLOQUEADO (D-23) |
## Catálogo público P3 tras P4a (condición 7)

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-VIS7-01 | Publicación visible de V1 | V1 pasa a INACTIVO | Deja de aparecer en `/inicio`, `/publicaciones` y `/filtros`; su detalle da 404 con el cuerpo común de P3 | CONFIRMADO (RN-VE-004 / RN-VE-008) / PROPUESTO (condición 7) | API | IMPL |
| CA-VIS7-02 | Publicación `DISPONIBLE` de V1 | V1 pasa a RESERVADO | Sigue visible con `estadoComercial: "RESERVADO"` y la etiqueta "Reservado" | CONFIRMADO (D-11, D-17 e) / PROPUESTO (forma) | API + COMP | IMPL |
| CA-VIS7-03 | V1 reactivado o liberado | Se consultan los 4 endpoints | Vuelve a verse como antes | PROPUESTO | API | IMPL |
| CA-VIS7-04 | Vehículo VENDIDO con publicación PUBLICADA vigente | Se consultan los 4 endpoints | No aparece | CONFIRMADO (RN-VE-014) | API | IMPL |
| CA-VIS7-05 | V1 con placa | Se revisa el JSON de tarjeta y detalle | `placa` no aparece en el catálogo público | PROPUESTO (contrato) | API | IMPL |
| CA-VIS7-06 | Las pruebas de visibilidad de P3 (CA-VIS-01 a 16) | Se ejecutan con la condición 7 | Siguen pasando (vehículos sembrados en ACTIVO) | PROPUESTO | API | IMPL |

## Migración 004 (D-23 PENDIENTE; verificación manual)

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-MIG-01 | Respaldo completo nuevo con fecha en `C:\Respaldo_CAMION\` (no vacío) | Antes de SUBIR | Existe y no sobrescribe otro | CONFIRMADO (CLAUDE.md, D-14 / D-16) | MAN | BLOQUEADO (D-23) |
| CA-MIG-02 | Conteos de `vehiculos` y `publicaciones` | Antes y después de SUBIR | Coinciden | CONFIRMADO (procedimiento D-14) | MAN | BLOQUEADO (D-23) |
| CA-MIG-03 | SUBIR aplicada | Se revisan columnas y restricciones | Existen las columnas, `UX_Veh_Placa`, `CK_Veh_Estado`, las CHECK, FK y tablas `materiales_carroceria`, `tipos_maquina`, `fotos_vehiculo`; `marca_id` y `modelo_anio` admiten NULL; `fotos_publicacion` intacta | PROPUESTO (contrato) | MAN | BLOQUEADO (D-23) |
| CA-MIG-04 | Vehículos DEMO | Tras SUBIR | Todos tienen `estado = ACTIVO` y `precio_cop` = precio de su publicación más reciente | PROPUESTO | MAN | BLOQUEADO (D-23) |
| CA-MIG-05 | Base con SUBIR aplicada | Se insertan a mano dos NULL de placa; una placa repetida; un `estado` = 'X'; una foto con orden 11 | Los dos NULL entran; los otros fallan por la restricción | CONFIRMADO (D-2) / PROPUESTO (resto) | MAN | BLOQUEADO (D-23) |
| CA-MIG-06 | Base de prueba con SUBIR (no `camion_ventas`) | Se ejecuta BAJAR sin y con un vehículo de marca NULL | Sin NULL: vuelve al esquema anterior sin borrar filas. Con NULL: avisa y se detiene sin cambios parciales | PROPUESTO | MAN | BLOQUEADO (D-23) |
| CA-MIG-07 | Base real con 004 | Se recorre la app (crear, editar, fotos, estados, catálogo) | Funciona sin errores 500 | PROPUESTO | MAN | BLOQUEADO (D-23) |

## Interfaz (`#/inventario`)

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-UI-01 | Sesión de asesor / administrador / comprador | Abre "Mi cuenta" | Asesor ve "Mi inventario"; administrador "Inventario"; comprador ningún enlace | CONFIRMADO (D-3) / PROPUESTO (textos) | COMP | PARCIAL |
| CA-UI-02 | Comprador o sin sesión | Entra a `#/inventario`, `#/inventario/nuevo` o `#/inventario/5` | Comprador ve "No tienes permiso para realizar esta acción."; sin sesión, el acceso a la cuenta (no datos) | CONFIRMADO (D-3) / PROPUESTO (forma) | COMP | PARCIAL |
| CA-UI-03 | Lista con ítems | Se muestra | Foto (o marcador sin foto), título marca + línea + año (sin "null"), placa, precio con miles, estado en texto, acciones según el estado (ACTIVO: Reservar e Inactivar; RESERVADO: Liberar e Inactivar; INACTIVO: Reactivar; VENDIDO: ninguna), botón "Registrar vehículo", paginación | PROPUESTO | COMP | PARCIAL |
| CA-UI-04 | Lista | Usa filtros de estado, categoría y búsqueda; el administrador además por asesor | La petición lleva los parámetros; el asesor no ve el filtro de asesor; estado vacío con mensaje | PROPUESTO | COMP | PARCIAL |
| CA-UI-05 | `#/inventario/nuevo` | Elige cada una de las 4 categorías | Solo se muestran los campos que aplican según la tabla, con marca de obligatorio; Carrocería sin placa ni km; Maquinaria con horas y tipo de máquina | CONFIRMADO (D-19) / PROPUESTO (forma) | COMP | PARCIAL |
| CA-UI-06 | Formulario | Cambia de categoría tras rellenar campos | No se envían los campos que dejaron de aplicar (evita el 400 de CA-CAT-11) | PROPUESTO | COMP | PARCIAL |
| CA-UI-07 | Formulario | Elige marca y departamento; cambia la marca | Modelo filtrado por marca y municipio por departamento; al cambiar de marca se vacía el modelo | PROPUESTO | COMP | PARCIAL |
| CA-UI-08 | Formulario | Escribe "120000000" en precio | Muestra "120.000.000" y envía `120000000` | PROPUESTO | COMP | PARCIAL |
| CA-UI-09 | La API responde 400 con `errores` y 409 de placa | Se envía | Cada mensaje bajo su campo (con `aria-describedby`), incluida "Esta placa ya está registrada en otro vehículo."; los datos se conservan | PROPUESTO | COMP | PARCIAL |
| CA-UI-10 | Edición de V1 con precio anterior | Se abre `#/inventario/<id>` | Muestra "Precio anterior: $ 120.000.000"; la categoría no se puede cambiar | CONFIRMADO (RN-VE-003) / PROPUESTO (forma) | COMP | PARCIAL |
| CA-UI-11 | Inactivar desde la lista | Pulsa "Inactivar" | Se abre `DialogoAccion` con campo de motivo (5–500); con motivo inválido no envía; 409 muestra el mensaje del servidor | PROPUESTO | COMP | PARCIAL |
| CA-UI-12 | Fotos | Selecciona archivos | Selector accesible con label; progreso en texto; vista previa; "N de 10"; un archivo > 5 MB o de otro tipo se rechaza antes de enviar; errores del servidor visibles; con 10 fotos el botón de subir se deshabilita con aviso | CONFIRMADO (RN-VE-001) / PROPUESTO (forma) | COMP | PARCIAL |
| CA-UI-13 | Galería de edición | Usa "Subir", "Bajar", "Hacer portada", "Eliminar" (con confirmación) solo con teclado | Llama al endpoint correcto; la primera no puede subir y la última no puede bajar; la portada se indica con texto | PROPUESTO | COMP | PARCIAL |
| CA-UI-14 | App real con 004 | Recorrido completo en escritorio y ancho de móvil | Sin desbordes horizontales; foco visible; lectores de pantalla anuncian estados y errores | PROPUESTO | MAN | BLOQUEADO (D-23) |

## No regresión P1–P3

| ID | Dado | Cuando | Entonces | Marca | Prueba | Estado actual |
|---|---|---|---|---|---|---|
| CA-REG-01 | Suites existentes (API y frontend de P1–P3) | Se ejecutan | Pasan todas; se registra el número exacto en la bitácora | PROPUESTO | API + COMP | FALTA (no ejecutado) |
| CA-REG-02 | Registro, login, inactividad de 30 min y logout global | Se recorren | Sin cambios | CONFIRMADO (D-3, D-12) | API + COMP | FALTA |
| CA-REG-03 | Panel de administración (aprobar, rechazar, bloquear, reconsiderar) | Se usa | Sin cambios | CONFIRMADO (D-10, D-15) | API + COMP | FALTA |
| CA-REG-04 | Catálogo público, filtros, detalle y galería de P3 | Se recorren con el DEMO | Sin cambios salvo la condición 7 | PROPUESTO | API + MAN | MAN BLOQUEADO (D-23) |
| CA-REG-05 | Pruebas de API | Se ejecutan | Ninguna toca `camion_ventas` ni escribe fotos dentro del repositorio | CONFIRMADO (CLAUDE.md) | API | FALTA |

---

## Trazabilidad HU/RN → criterio → prueba prevista

| HU / RN / decisión | Criterios | API | COMP | MAN | Total |
|---|---|---|---|---|---|
| HU-013 Registrar (RN-VE-007) | CA-013-01 a 08 | 8 | 0 | 0 | 8 |
| D-17 / D-19 Reglas por categoría | CA-CAT-01 a 12 | 12 | 0 | 0 | 12 |
| HU-015 Placa (RN-VE-002, D-2) | CA-015-01 a 06 | 6 | 0 | 0 | 6 |
| HU-016 Precio (RN-VE-003) | CA-016-01 a 03 | 3 | 0 | 0 | 3 |
| HU-017 Kilometraje / horas (RN-VE-005, D-19) | CA-017-01 a 03 | 3 | 0 | 0 | 3 |
| HU-018 Características (RN-VE-010) | CA-018-01 a 05 | 5 | 1 | 0 | 5 |
| HU-019 Editar (RN-VE-009) | CA-019-01 a 06 | 6 | 0 | 0 | 6 |
| HU-020 Estados (RN-VE-004, RN-VE-008, D-4, D-11) | CA-020-01 a 08 | 8 | 0 | 0 | 8 |
| HU-021 / HU-022 Inventario (RN-CO-009, RN-AD-001) | CA-021-01 a 07, CA-022-01 a 03 | 10 | 0 | 0 | 10 |
| RN-SE-006 / D-3 / D-7 Permisos | CA-SE006-01 a 07 | 7 | 0 | 0 | 7 |
| HU-014 Fotos (RN-VE-001, D-20) | CA-014-01 a 13 | 12 | 0 | 1 | 13 |
| D-20 `/media` | CA-MED-01 a 04 | 3 | 0 | 1 | 4 |
| Catálogos del formulario (D-19) | CA-FRM-01 a 03 | 2 | 0 | 1 | 3 |
| Condición 7 (RN-VE-004, RN-VE-014, D-11) | CA-VIS7-01 a 06 | 6 | 1 | 0 | 6 |
| Migración 004 (D-2, D-23) | CA-MIG-01 a 07 | 0 | 0 | 7 | 7 |
| Interfaz | CA-UI-01 a 14 | 0 | 13 | 1 | 14 |
| No regresión P1–P3 | CA-REG-01 a 05 | 5 | 3 | 1 | 5 |
| **Total** | | **96** | **18** | **12** | **120** |

Un criterio puede probarse de más de una forma; Total cuenta criterios (120 filas, varias agrupan casos parametrizados).

Prueba prevista por bloque:
- **API**: clases nuevas en `backend/Camion.Api.Tests` (creación y reglas por categoría parametrizadas por tipo y campo, placa, precio, edición y auditoría, estados con tabla de transiciones, listado, permisos con matriz rol × endpoint, fotos con archivos sintéticos de firma conocida en carpeta temporal, `/media` con rutas maliciosas, catálogos, condición 7 sobre las pruebas de visibilidad de P3).
- **COMP**: enlace en "Mi cuenta", guardia de ruta, lista, formulario por categoría, mensajes 400/409, diálogo de motivo, fotos.
- **MAN**: migración 004 y recorrido real; todo **BLOQUEADO (D-23)**, y las fotos reales además por D-20.

## Qué existe y qué falta

- **Existe (sin verificar por auditoría)**: endpoints de inventario, reglas, `/media` y migración 004 en el commit `d158868`; P1–P3 fusionados.
- **Parcial**: frontend de inventario (archivos sin commit en `frontend/src`).
- **Falta**: pruebas de API y de componente de P4a; ejecución de las suites de no regresión; actualización de `ESTADO-REAL.md`.
- **Bloqueado**: migración en `camion_ventas` (D-23), carpeta real de fotos (D-20), marcar vendido (D-22, fuera de P4a).

---

## Contradicciones detectadas

No se ha modificado el contrato ni `DECISIONES.md` (indicación del coordinador). Se proponen para registrar como PENDIENTE las de negocio (C-P4a-1 a C-P4a-6).

**C-P4a-1. Trazabilidad de HU-013 y HU-020 distinta de la matriz.** La matriz (v3.0 §7) asigna HU-013 a **RN-VE-001** (fotos) y HU-020 solo a RN-VE-004; el contrato traza HU-013 a RN-VE-007 y HU-020 además a RN-VE-008. Además **RN-AD-001** (HU-022) no aparece definida en el catálogo de reglas de la v3.0, y RN-CO-009 habla de "publicaciones propias", no de vehículos. Opciones: A) aceptar la trazabilidad del contrato; B) citar ambas. Impacto: solo trazabilidad.

**C-P4a-2. RN-VE-005 "Debe registrarse kilometraje" frente a Carrocería y Maquinaria.** El contrato no admite kilometraje en esas categorías y guarda 0. D-19 lo respalda ("sin kilometraje", "horas de uso en lugar del kilometraje"), pero RN-VE-005 no se modificó formalmente. Opción: anotar en D-19 que modifica RN-VE-005 para esas categorías. Impacto: CA-CAT-05, CA-CAT-09, CA-017-03.

**C-P4a-3. RN-VE-006 "Debe registrarse modelo" y D-19 "marca, modelo y año" para máquinas.** Por D-17 "modelo" = línea, pero el contrato deja la línea **opcional en las cuatro categorías**. Opciones: A) línea obligatoria en Carro, Camión y Maquinaria; B) mantenerla opcional (como el catálogo, donde existe un camión sin línea). Impacto: CA-CAT-01, CA-CAT-08 y formulario.

**C-P4a-4. RN-VE-001 "Depende del plan" frente a 10 fijas.** El contrato fija 10 fotos por vehículo sin mirar el plan (D-20 PENDIENTE). Opciones: A) 10 fijas en P4a y el límite por plan en P4b/P7; B) límite por plan ya. Impacto: CA-014-05, CA-UI-12.

**C-P4a-5. Formato de placa 5–7 caracteres.** Puede rechazar placas válidas de maquinaria (en Colombia la maquinaria tiene un registro propio cuyo formato podría superar 7 caracteres; por verificar). Opciones: A) 5–7 para todas; B) formato distinto para Maquinaria. Impacto: CA-015-05.

**C-P4a-6. Capacidad.** D-19 incluye "capacidad" para la máquina, no para la carrocería; el contrato admite `capacidadCargaKg` en Carrocería y la mide en kg también para máquinas (puede expresarse en toneladas o m³ de cucharón). Opciones: A) aceptar kg en ambas; B) quitarla de Carrocería y/o otra unidad para máquina. Impacto: CA-CAT-06, CA-CAT-07.

**C-P4a-7. Huecos del contrato (para el coordinador).** (a) `asesorId` enviado por un asesor en la creación, o en el `PUT`: ¿se ignora o 400? (CA-013-02). (b) `PUT` sin cambios: ¿evento `EDITAR` vacío o ninguno? (CA-019-04). (c) Auditoría de subir, borrar y reordenar fotos no especificada, aunque RN-VE-009 pide registrar cambios (CA-014-12). (d) Código del cuerpo > 6 MB (400 o 413, CA-014-04). (e) Mensaje exacto del 400 del motivo y del reordenado.

**C-P4a-8. Dos precios hasta P4b y ESTADO-REAL desactualizado.** Tras P4a el vehículo tiene su `precio_cop`, pero el catálogo público sigue mostrando el de la publicación: un cambio de precio en el inventario no se verá en el portal hasta P4b. Conviene avisarlo a la responsable antes de la prueba manual. Además `ESTADO-REAL.md` sigue siendo la auditoría de P0; se recomienda que `auditor-codigo` lo actualice antes de cerrar P4a.

## Criterios añadidos tras la prueba manual (2026-10-28)

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-D30-01 | Un asesor en "Registrar vehículo" | No ha elegido ninguna foto | El botón "Registrar vehículo" está desactivado y se ve "Agrega al menos una foto para poder guardar." | CONFIRMADO (D-30) | COMP |
| CA-D30-02 | — | Se envía a la API una creación sin fotos, con más de 10 o con una foto inválida | 400 en `fotos`; no se crea el vehículo ni quedan archivos | CONFIRMADO (D-30) | API |
| CA-D30-03 | Datos válidos y de 1 a 10 fotos válidas | Se registra | 201: el vehículo, las fotos (la primera es portada) y la auditoría se guardan juntos; si la base falla, no queda nada | CONFIRMADO (D-30) | API |
| CA-D30-04 | Un vehículo con una sola foto | Se intenta eliminarla | El botón está desactivado en la interfaz; la API responde 409 | CONFIRMADO (D-30) | API + COMP |
| CA-D30-05 | Un vehículo sin fotos (DEMO o creado antes de D-30) | Se intenta "Guardar cambios" | En la interfaz aparece el aviso y el botón está desactivado; la API responde 409; después de subir una foto, guardar da 200 | CONFIRMADO (D-30) | API + COMP |
| CA-PLA-01 | Carro, Camión o Maquinaria | Se crea o se edita con placa | La placa se guarda y vuelve en la respuesta | CONFIRMADO (D-2; defecto de la prueba manual) | API |
| CA-PLA-02 | Una placa ya usada en otra categoría | Se crea otro vehículo con la misma placa, en mayúsculas o minúsculas | 409 | CONFIRMADO (D-2) | API |
| CA-PLA-03 | Se envió una placa | La respuesta trae otra placa, o ninguna | Advertencia "La placa no se guardó…" en la interfaz y advertencia `placa_no_guardada` en el log; si coinciden, el éxito dice "Placa: …" | PROPUESTO | COMP + API |
| CA-D28-01 | Un vehículo ACTIVO | Se reserva sin nota o con menos de 5 caracteres | 400; en la interfaz, el diálogo pide la nota | CONFIRMADO (D-28) | API + COMP |
| CA-D28-02 | Un vehículo reservado con nota | Se consulta el inventario o el detalle | La nota se ve en el inventario y en la auditoría, no en el portal; al liberar o inactivar se borra | CONFIRMADO (D-28) | API + COMP |
| CA-D31-01 | Cualquier formulario (inventario, registro, inicio de sesión, motivo) | Se abre | Se ve la nota "* Campo obligatorio"; cada campo obligatorio tiene asterisco y `aria-required`; los opcionales no tienen asterisco | CONFIRMADO (D-31) | COMP |
| CA-D31-02 | Inventario | Se cambia la categoría | El asterisco cambia con ella: la marca lo lleva en Camión y no en Carrocería; el modelo lo lleva en Carro, Camión y Maquinaria; la placa nunca | CONFIRMADO (D-31, D-25) | COMP |
| CA-PLA-04 | Camión con carrocería | Al crear o editar, la placa aparece en la caja sin que el navegador avise (por ejemplo, por el autocompletado) | Se toma lo que está escrito en pantalla: la placa viaja, o se rechaza si no es válida; el servidor registra `placaRecibida=si/no` | PROPUESTO | COMP |
| CA-D33-01 | El dueño o el administrador edita un vehículo sin publicación activa | Cambia la categoría | Se muestran los campos de la nueva, se vacían los que no aplican y el modelo; PUT 200 con la nueva categoría; auditoría EDITAR con la categoría anterior y la nueva | CONFIRMADO (D-33) | API + COMP |
| CA-D33-02 | Un vehículo con una publicación PUBLICADA | Se intenta cambiar la categoría | 409 "Este vehículo tiene una publicación activa…"; los demás datos sí se pueden editar | CONFIRMADO (D-33) | API |
| CA-D33-03 | Una marca sin modelos de la categoría elegida | Se abre el selector de modelo | "<Marca> no tiene modelos de <Categoría>. Elige otra marca o cambia la categoría." | CONFIRMADO (D-33) | COMP |
