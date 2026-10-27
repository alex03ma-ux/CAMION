# P3 — Contrato de la API: catálogo público de vehículos

Fijado por el coordinador antes de programar. Backend y frontend lo siguen tal cual. Cualquier cambio en este contrato se anota en `docs/coordinacion/BITACORA.md`.

Se mantiene el formato común de error `{ error, errores? }` de P1/P2.

**Trazabilidad** (numeración oficial de 87 HU, D-1):

| HU | Nombre | Regla |
|---|---|---|
| HU-001 | Portal principal | RN-CA-001 |
| HU-002 | Recientes | RN-CA-002 |
| HU-003 | Destacado | RN-VE-011, RN-VE-012 |
| HU-004 | Buscar | RN-CA-003 |
| HU-005 | Carros | RN-CA-004 |
| HU-006 | Camiones | RN-CA-005 |
| HU-007 | Marca | RN-CA-006 |
| HU-008 | Modelo | RN-CA-007 |
| HU-009 | Presupuesto | RN-CA-008 |
| HU-010 | Año | RN-CA-009 |
| HU-011 | Kilometraje | RN-CA-010 |
| HU-012 | Limpiar filtros | RN-CA-011 |

Además: RN-VE-013 (solo vigentes), RN-VE-014 (vendido oculta), RN-SE-005 (consulta pública sin cuenta) y las decisiones D-3 y D-11.

**Sin cambios de esquema en P3.** Se mapean, en modo **solo lectura**, tablas que ya existen en `database/002_esquema_inicial_mariadb_10_4.sql`, con sus nombres de columna exactos:

- `vehiculos`, `publicaciones`, `fotos_publicacion`, `publicaciones_destacadas` e `historial_vigencias`;
- `marcas`, `lineas`, `tipos_vehiculo`, `tipos_carroceria` y `combustibles`;
- `municipios` y `departamentos`.

Ningún endpoint de P3 escribe en la base.

## Significado de los términos (D-17 TOMADA)

- **Modelo** (HU-008, RN-VE-006 "Catálogo") = la **línea** del vehículo (`lineas`), por ejemplo Chevrolet → NPR. El **año** es `vehiculos.modelo_anio` (HU-010).
- **Categoría** = `tipos_vehiculo`. Por D-17 hay **cuatro categorías principales: Carro, Camión, Carrocería y Maquinaria**.
  - Carros es HU-005 y Camiones es HU-006. Carrocería y Maquinaria se añaden por D-17.
  - Los subtipos (sedán, SUV, volqueta, furgón, estacas…) siguen en `tipos_carroceria`, y siguen siendo una característica de los camiones.
  - Una carrocería suelta se publica en la categoría Carrocería con marca (fabricante), línea, año, tipo de carrocería y kilometraje 0.
  - No hay columnas nuevas. Los campos propios de carrocerías y máquinas están pendientes en D-19.
- **Estados** (D-4, D-11), a partir de `publicaciones.estado_publicacion` y `estado_comercial`:

| Estado | Condición |
|---|---|
| Activo | PUBLICADA + DISPONIBLE |
| Reservado | PUBLICADA + RESERVADO |
| Vendido | VENDIDO |
| Inactivo | OCULTA, BORRADOR o ELIMINADA |

## Regla de visibilidad pública (RN-VE-013, RN-VE-014, D-11)

Una publicación es **visible** solo si se cumple todo lo siguiente, evaluado con la hora UTC del servidor:

1. `estado_publicacion = 'PUBLICADA'`.
2. `estado_comercial IN ('DISPONIBLE','RESERVADO')`. Un vendido nunca se muestra.
3. **Vigente**: existe una fila en `historial_vigencias` de esa publicación con `inicio <= ahora < fin`. Si no la hay, la publicación está vencida y no se muestra.
4. El asesor dueño tiene la cuenta `ACTIVA` (`AspNetUsers.EstadoCuenta`, a través de `perfiles_asesor`). Un asesor bloqueado o rechazado no exhibe vehículos (confirmado en D-17).
5. `precio_cop > 0` y `titulo` no vacío. La CHECK de la base ya lo exige para PUBLICADA, pero se comprueba de todas formas.

Los **reservados** se muestran con `estadoComercial: "RESERVADO"`, y la interfaz les pone la etiqueta "Reservado" (D-11; confirmado en D-17).

Una publicación es **destacada** si es visible y además tiene una fila en `publicaciones_destacadas` con `estado = 'ACTIVA'` y `inicio <= ahora < fin`.

Esta regla está **en un solo sitio** del backend (una consulta o un método reutilizable) y la usan todos los endpoints.

## Formatos

### Tarjeta (`TarjetaVehiculo`)

```json
{ "slug", "titulo", "precioCop", "negociable", "estadoComercial", "destacado",
  "tipo", "marca", "linea", "modeloAnio", "kilometraje",
  "municipio", "departamento", "fotoPortada", "publicadaEn" }
```

- `precioCop` es un número entero en pesos colombianos (se redondea el DECIMAL).
- `linea` y `fotoPortada` pueden ser null.
- `fotoPortada` es la URL de la foto con `es_portada = 1`. Si ninguna lo es, es la de menor `orden`. Si no hay fotos, es null.
- `publicadaEn` va en ISO UTC.

### Detalle (`DetalleVehiculo`)

Es la tarjeta más estos campos:

```json
"descripcionComercial", "descripcionTecnica", "carroceria", "combustible",
"capacidadCargaKg", "pesoBrutoKg", "cilindrajeCc",
"fotos": [ { "url", "orden", "esPortada" } ]
```

- Las fotos van ordenadas por `orden` (de 1 a 10).
- El detalle **no** incluye datos del asesor, ids internos (`vehiculos.id`, `asesor_id`) ni datos personales. El contacto llega en P5.

### URL de las fotos

La URL es `/media/` seguido de `archivo_clave`, con cada segmento codificado para URL. En P3 todavía no se sirven archivos: la subida y el servicio de fotos llegan en P4. El frontend muestra una imagen de reemplazo si la foto no carga.

## Endpoints

Todos son **anónimos y de solo lectura**: no exigen token. Con token responden igual, y nunca dan 401 ni 403.

### GET /api/catalogo/inicio — HU-001, HU-002, HU-003

Respuesta 200:

```json
{ "destacados": [Tarjeta], "recientes": [Tarjeta] }
```

- `destacados`: hasta 8, ordenados por `publicadaEn` descendente.
- `recientes`: hasta 12, ordenados por `publicadaEn` descendente. **Incluye** también a los destacados, que van marcados con `destacado: true`.

### GET /api/catalogo/publicaciones — HU-004 a HU-012

Parámetros de consulta, todos opcionales:

| Parámetro | Regla |
|---|---|
| `q` | Hasta 100 caracteres tras recortar espacios. Busca, sin distinguir mayúsculas, en título, marca y línea. |
| `tipoId` | Categoría. |
| `marcaId` | Marca. |
| `lineaId` | Modelo. Si se envía junto con `marcaId`, la línea debe pertenecer a esa marca; si no, 400. |
| `precioMin`, `precioMax` | Enteros ≥ 0. `precioMin ≤ precioMax`. |
| `anioMin`, `anioMax` | Enteros entre 1900 y 2200. `anioMin ≤ anioMax`. |
| `kmMax` | Entero ≥ 0. |
| `orden` | `recientes` (por defecto) / `precio_asc` / `precio_desc` / `anio_desc` / `km_asc`. |
| `pagina` | ≥ 1. Por defecto 1. |
| `tamano` | Entre 1 y 48. Por defecto 12. |

**Orden de los resultados:** los **destacados van siempre primero** (RN-VE-012). Dentro de cada grupo se aplica el `orden` pedido y, para desempatar, `publicadaEn` descendente y luego `slug`.

**Respuesta 200:**

```json
{ "items": [Tarjeta], "total", "pagina", "tamano" }
```

- Si no hay resultados, `items` llega vacío y la respuesta sigue siendo 200.
- Un id de marca, línea o tipo que no existe también devuelve 200 con 0 resultados, no un error.

**Respuesta 400:** `{ "error": "Revisa los filtros.", "errores": { "<parámetro>": ["…"] } }`. Mensajes por parámetro:

| Parámetro | Mensaje |
|---|---|
| `q` | "La búsqueda no puede tener más de 100 caracteres." |
| `precioMin` / `precioMax` | "Escribe un precio válido en pesos (número entero mayor o igual a 0)." |
| `precioMax` (si es menor que `precioMin`) | "El precio máximo debe ser mayor o igual al mínimo." |
| `anioMin` / `anioMax` | "Escribe un año entre 1900 y 2200." |
| `anioMax` (si es menor que `anioMin`) | "El año final debe ser mayor o igual al inicial." |
| `kmMax` | "Escribe un kilometraje válido (número entero mayor o igual a 0)." |
| `lineaId` | "El modelo no corresponde a la marca elegida." |
| `orden` | "Orden no válido." |
| `pagina` / `tamano` | "Página o tamaño no válidos." |

Un parámetro que no es un número donde se espera un número da 400 con su mensaje, nunca 500.

**HU-012 "Limpiar filtros"** es la misma consulta sin parámetros. No necesita un endpoint aparte.

### GET /api/catalogo/publicaciones/{slug} — detalle

- 200: el `DetalleVehiculo`.
- 404 `{ "error": "Este vehículo no está disponible." }` en estos casos (mismo mensaje en todos, sin revelar el motivo):
  - el slug no existe;
  - la publicación no es visible: vendida, oculta, vencida o asesor no activo.
- El `slug` tiene como máximo 200 caracteres. Si es más largo, también responde 404.

### GET /api/catalogo/filtros — opciones para los filtros

Respuesta 200:

```json
{ "tipos": [ { "id", "nombre", "total" } ],
  "marcas": [ { "id", "nombre", "total", "lineas": [ { "id", "nombre", "total" } ] } ],
  "rangos": { "precioMin", "precioMax", "anioMin", "anioMax", "kmMax" } }
```

- Solo incluye entradas de catálogo con `activo = 1` que tengan **al menos una publicación visible**.
- `total` es el número de publicaciones visibles de cada entrada.
- Los nombres se ordenan alfabéticamente.
- `rangos` se calcula sobre las publicaciones visibles. Si no hay ninguna, todos sus valores son null.

## Seguridad

- Solo GET y sin efectos secundarios. No se registra auditoría de consultas.
- Las consultas usan parámetros (EF Core / LINQ) y nunca SQL concatenado.
- `tamano` tiene un máximo y la respuesta nunca incluye datos de usuarios.

## Interfaz

**Navegación.** El portal público pasa a ser la página principal. La navegación usa el hash de la URL, sin dependencias nuevas:

| Ruta | Pantalla |
|---|---|
| `#/` | Inicio |
| `#/buscar?…` | Resultados, con los filtros en la URL: se puede compartir y funciona con Atrás/Adelante |
| `#/vehiculo/<slug>` | Detalle |
| `#/cuenta` | La pantalla actual de login/registro/perfil |

**Encabezado.** En todas las páginas: logo/nombre, búsqueda, enlaces "Carros", "Camiones", "Carrocerías" y "Maquinaria" (por los nombres de `tipos` Carro / Camión / Carrocería / Maquinaria; un enlace sin tipo correspondiente no se muestra), y según la sesión "Iniciar sesión / Registrarme" o "Mi cuenta". El panel de administración sigue dentro de "Mi cuenta".

**Inicio (HU-001 a HU-003).**
- Sección "Destacados" primero y después "Recientes", con tarjetas.
- Accesos a las cuatro categorías.
- Si no hay vehículos: "Pronto tendremos vehículos publicados." más un acceso a la búsqueda.

**Resultados (HU-004 a HU-012).**
- Filtros:
  - categoría;
  - marca → modelo (el modelo se habilita al elegir una marca y muestra solo sus líneas);
  - presupuesto mínimo y máximo en COP, con formato de miles;
  - año desde y hasta;
  - kilometraje máximo;
  - orden.
- Botón **"Limpiar filtros"**: vuelve a `#/buscar` sin parámetros y vacía los campos.
- Contador "N vehículos encontrados" con `role="status"`.
- Paginación.
- Errores 400 bajo su filtro.
- Estado vacío: "No encontramos vehículos con esos filtros." más el botón "Limpiar filtros".

**Tarjeta.**
- Foto (o imagen de reemplazo), título, precio en formato `$ 120.000.000`, año, kilometraje con formato de miles + "km", ciudad.
- El kilometraje **no se muestra** cuando el tipo es Carrocería, ni cuando es Maquinaria con kilometraje 0. La regla es la misma en la tarjeta y en el detalle.
- Etiqueta "Destacado" y etiqueta "Reservado" cuando corresponda, siempre con texto y no solo con color.
- Toda la tarjeta enlaza al detalle.

**Detalle.**
- Galería accesible: foto principal, miniaturas, textos alternativos y navegación con teclado.
- Precio, "Negociable" si aplica, etiqueta "Reservado".
- Ficha técnica (solo los campos con valor, aplicando la misma regla de kilometraje) y descripción.
- Botón "Volver a resultados".
- Un 404 muestra "Este vehículo no está disponible." y un enlace al inicio.

**Comprador registrado (D-3).** Ve exactamente lo mismo, en solo lectura, sin botones de edición.

**Responsivo y accesible.**
- Rejilla de 1 columna en móvil, 2 en tableta y 3–4 en escritorio, sin desplazamiento horizontal de la página.
- Filtros plegables en móvil.
- Labels en los campos, foco visible, títulos jerárquicos y textos alternativos.
- Estados de carga y error de red ("No se pudo conectar con el servidor.").

## Fuera de P3

- Contacto con el asesor y leads (P5).
- Favoritos (HU-056 a HU-058).
- Subida y servicio de fotos, placa (D-2) y gestión de vehículos (P4).
- Marcar destacados desde el panel (P7).
- Datos de demostración en `camion_ventas` (D-18).

## Aclaraciones del coordinador (respuesta a C-P3-1 a C-P3-9 de `P3.md`)

- **C-P3-2, vigencia y plan:** a la regla de visibilidad se añade la **condición 6**: la suscripción de la fila de `historial_vigencias` vigente debe estar `ACTIVA` y con `fin_vigencia` nula o posterior a la hora actual. Si el plan venció, la publicación deja de verse (§15 "Vence al finalizar vigencia"; D-7). Es una decisión técnica conservadora: oculta, nunca muestra de más.
- **C-P3-1, destacados en el inicio:** la sección "Destacados" va primero. En "Recientes" todo se ordena por fecha, con los destacados marcados. En la búsqueda, los destacados van primero (RN-VE-012).
- **C-P3-3, trazabilidad del detalle:** el detalle se traza a HU-004 / RN-CA-003. Es el resultado de una búsqueda.
- **C-P3-4, `lineaId`:**
  - Con `marcaId`: si no existe ninguna línea con ese id **en esa marca**, responde 400 `lineaId` "El modelo no corresponde a la marca elegida." Esto incluye una línea que no existe.
  - Sin `marcaId`: un `lineaId` que no existe da 200 con 0 resultados.
- **C-P3-5, enlaces de categoría:** se aceptan. Solo aparecen las categorías que tienen al menos una publicación visible, porque así lo devuelve `/filtros`.
- **C-P3-6, kilometraje 0:** se acepta que `kmMax` y `km_asc` incluyan las carrocerías y las máquinas con 0 km. La búsqueda ignora tildes según la intercalación de MariaDB; en SQLite solo ignora mayúsculas. Esa diferencia queda como limitación de las pruebas.
- **C-P3-8:** el inventario de endpoints de P2 se amplía con las 4 rutas anónimas del catálogo.
- **Ids no numéricos:** `tipoId`, `marcaId` y `lineaId` que no son números responden 400 con estos mensajes: "Categoría no válida.", "Marca no válida." y "Modelo no válido."
- **Redondeo de `rangos`:** `precioMin` se redondea hacia abajo y `precioMax` hacia arriba, para que el rango incluya todos los precios.
- **Hora del script DEMO:** usa UTC (`UTC_TIMESTAMP()`), igual que la regla de visibilidad.
