# P4a — Contrato de la API: inventario de vehículos

Fijado por el coordinador antes de programar. Backend y frontend lo siguen tal cual; un cambio aquí se registra en `docs/coordinacion/BITACORA.md`. Se mantiene el formato común de error `{ error, errores? }` y la regla de visibilidad del catálogo de P3.

**Trazabilidad** (matriz oficial de 87 HU, D-1)

| HU | Nombre | Regla |
|---|---|---|
| HU-013 | Registrar vehículo | RN-VE-007 (todo vehículo tiene asesor propietario) |
| HU-014 | Cargar fotografías | RN-VE-001 (hasta 10) |
| HU-015 | Información complementaria | RN-VE-002 / D-2 (placa) |
| HU-016 | Precio de venta | RN-VE-003 (obligatorio, se conserva el anterior) |
| HU-017 | Kilometraje | RN-VE-005 |
| HU-018 | Características comerciales | RN-VE-010 |
| HU-019 | Editar | RN-VE-009 (trazabilidad) |
| HU-020 | Inactivar | RN-VE-004 / RN-VE-008 |
| HU-021 | Inventario propio | RN-CO-009 |
| HU-022 | Inventario general | RN-AD-001 |

Otras decisiones que aplican: D-4, D-11, D-17, D-19 y D-7 (cada asesor gestiona solo lo suyo). Las decisiones nuevas son D-20 a D-24.

## Cambios de esquema — migración `database/migraciones/004_p4a_inventario.sql`

La migración es reversible y tiene verificación. La aplica la responsable vía phpMyAdmin (D-16) después de un respaldo previo (D-23).

**Tabla `vehiculos`:**

| Columna | Definición | Notas |
|---|---|---|
| `placa` | VARCHAR(10) NULL, índice único `UX_Veh_Placa` | Varias filas NULL están permitidas (D-2). Se guarda en mayúsculas y sin espacios ni guiones. |
| `estado` | VARCHAR(20) NOT NULL DEFAULT 'ACTIVO', CHECK `CK_Veh_Estado` IN ('ACTIVO','RESERVADO','INACTIVO','VENDIDO') | D-4, D-11 |
| `precio_cop` | DECIMAL(15,2) NULL, CHECK > 0 | La API lo exige. La migración lo rellena desde la publicación más reciente del vehículo. |
| `precio_anterior_cop` | DECIMAL(15,2) NULL | RN-VE-003 |
| `largo_m`, `ancho_m`, `alto_m` | DECIMAL(6,2) NULL, CHECK > 0 | Medidas de la carrocería (D-19) |
| `material_id` | BIGINT NULL, FK → `materiales_carroceria` | D-19 |
| `horas_uso` | INT NULL, CHECK >= 0 | Horómetro de la máquina (D-19) |
| `tipo_maquina_id` | BIGINT NULL, FK → `tipos_maquina` | D-19 |
| `motivo_inactivacion` | VARCHAR(500) NULL | |

Dos cambios más en `vehiculos`:
- `marca_id` y `modelo_anio` pasan a **NULL**. Una carrocería puede no tener marca ni año (D-19). La CHECK `CK_Veh_Valores` sigue valiendo, porque un NULL la cumple.
- La **capacidad** de la máquina reutiliza `capacidad_carga_kg`, en kg (PROPUESTO).

**Tablas nuevas:**
- `materiales_carroceria` (id, nombre UNIQUE, activo). Valores iniciales PROPUESTOS: Aluminio, Acero, Madera, Fibra de vidrio.
- `tipos_maquina` (id, nombre UNIQUE, activo). Valores iniciales PROPUESTOS: Retroexcavadora, Montacargas, Minicargador, Excavadora, Motoniveladora, Vibrocompactador.
- `tipos_carroceria` ya existe. Se completa con Furgón, Estacas, Volco, Platón, Tanque y Refrigerada si faltan (INSERT ... WHERE NOT EXISTS).
- `fotos_vehiculo` (id, vehiculo_id FK, archivo_clave VARCHAR(500), orden TINYINT UNSIGNED, es_portada TINYINT(1), creado_en; UNIQUE (vehiculo_id, orden); CHECK orden BETWEEN 1 AND 10). Las fotos son **del vehículo**. En P4b la publicación las mostrará. `fotos_publicacion` no se toca.

**Sección BAJAR:** elimina las columnas y tablas nuevas, y restaura NOT NULL en `marca_id` y `modelo_anio`. Solo puede restaurarlo si no hay filas con NULL; si las hay, lo avisa y se detiene. Nunca borra filas de las tablas existentes.

**Verificación:** conteos de `vehiculos` y `publicaciones` antes y después (deben coincidir) y la lista de columnas y restricciones.

## Reglas por categoría (`tipos_vehiculo`, D-17 y D-19)

| Campo | Carro | Camión | Carrocería | Maquinaria |
|---|---|---|---|---|
| marca | obligatoria | obligatoria | **opcional** | obligatoria |
| línea (modelo) | opcional | opcional | opcional | opcional |
| año (`modeloAnio`) | obligatorio, entre 1950 y el año actual + 1 | igual | opcional | obligatorio |
| kilometraje | obligatorio ≥ 0 | obligatorio ≥ 0 | **no se admite** (se guarda 0) | **no se admite** (se guarda 0) |
| horasUso | no se admite | no se admite | no se admite | opcional ≥ 0 |
| placa | opcional, única | opcional, única | **no se admite** | opcional, única |
| carroceriaId | opcional | opcional (subtipo del camión) | **obligatorio** (tipo de carrocería) | no se admite |
| largo/ancho/alto (m) | no | no | opcionales > 0 | no |
| materialId | no | no | opcional | no |
| tipoMaquinaId | no | no | no | **obligatorio** |
| combustibleId, cilindrajeCc, pesoBrutoKg | opcionales | opcionales | no | opcionales |
| capacidadCargaKg | no | opcional | opcional | opcional (capacidad) |
| municipioId, precioCop, descripcionTecnica | obligatorio, obligatorio, opcional (hasta 4000) | igual | igual | igual |

- Un campo "no se admite" que llega con valor da 400 en ese campo: "Este dato no aplica a <categoría>."
- Formato de placa: entre 5 y 7 letras y números tras normalizar. Si no cumple, 400 "Escribe una placa válida (letras y números)." Si ya existe, 409 `{ errores: { placa: ["Esta placa ya está registrada en otro vehículo."] } }`.

## Estados del vehículo (D-4, D-11)

| Acción | Quién | Desde | Hacia | Requisitos |
|---|---|---|---|---|
| reservar | dueño o admin | ACTIVO | RESERVADO | — |
| liberar | dueño o admin | RESERVADO | ACTIVO | — |
| inactivar | dueño o admin | ACTIVO o RESERVADO | INACTIVO | motivo de 5 a 500 caracteres |
| reactivar | dueño o admin | INACTIVO | ACTIVO | — |

**Vendido** existe en el esquema, pero **marcarlo** espera a HU-064 (D-22). Un VENDIDO no se edita y responde 409 a cualquier acción.

Al inactivar o reservar, las publicaciones del vehículo **no se modifican en P4a**. Las publicaciones son P4b. Aun así, la visibilidad del catálogo se ajusta así:
- Se añade una **condición 7**: `vehiculos.estado` debe ser ACTIVO o RESERVADO.
- Un vehículo RESERVADO se muestra como reservado aunque la publicación diga DISPONIBLE.

## Permisos (RN-SE-006, D-7)

- `ASESOR` con cuenta ACTIVA: crea vehículos a su nombre y ve y modifica **solo los suyos**. Un vehículo de otro asesor responde **404**, no 403, para no revelar que existe.
- `ADMINISTRADOR`: ve y modifica todos.
  - Al crear un vehículo indica `asesorId`, que debe ser un asesor ACTIVA; si no, 400.
- `COMPRADOR` o sin rol: 403. Sin token: 401.
- Política nueva `AsesorOAdministrador`.

## Endpoints `/api/inventario`

### Vehículos

**`GET /api/inventario/vehiculos`** — lista paginada del inventario.
- El asesor ve lo suyo (HU-021) y el administrador ve todo (HU-022).
- Parámetros de consulta, todos opcionales:
  - `estado`, `tipoId`, `marcaId`;
  - `q`: busca en placa, marca, línea y descripción, hasta 100 caracteres;
  - `asesorId`: solo lo usa el administrador;
  - `pagina`, y `tamano` (por defecto 20, máximo 100).
- Orden: `actualizadoEn` descendente.
- Respuesta 200: `{ items: [ItemInventario], total, pagina, tamano }`.
- `ItemInventario` = { id, tipo, marca, linea, modeloAnio, placa, precioCop, estado, kilometraje|null, horasUso|null, municipio, fotoPortada, totalFotos, actualizadoEn, asesor: { id, nombre } (solo para el administrador) }.

**`GET /api/inventario/vehiculos/{id}`** — detalle completo para editar.
- Devuelve todos los campos de la tabla de categorías, más `precioAnteriorCop`, `motivoInactivacion` y `fotos: [{ id, url, orden, esPortada }]`.
- 404 si no existe o no es del asesor.

**`POST /api/inventario/vehiculos`** — crear un vehículo (HU-013, HU-015 a HU-018).
- Petición:
  ```json
  { tipoId, marcaId?, lineaId?, modeloAnio?, kilometraje?, horasUso?, placa?, carroceriaId?,
    combustibleId?, cilindrajeCc?, pesoBrutoKg?, capacidadCargaKg?, largoM?, anchoM?, altoM?,
    materialId?, tipoMaquinaId?, municipioId, precioCop, descripcionTecnica?, asesorId? }
  ```
- Respuestas: 201 con el detalle; 400 con `errores`; 409 si la placa está repetida.
- Validación:
  - Cada id debe existir y estar `activo = 1`.
  - Una línea debe ser de la marca indicada.
  - `precioCop` es un entero > 0 y ≤ 10.000.000.000.
- Auditoría `auditoria_eventos`: entidad `Vehiculo`, acción `CREAR`.

**`PUT /api/inventario/vehiculos/{id}`** — editar (HU-019).
- Lleva el mismo cuerpo que la creación, sin `asesorId`. El tipo **no se puede cambiar**: si cambia, 400.
- Si cambia el precio, `precio_anterior_cop` toma el precio que había hasta ahora (RN-VE-003).
- Auditoría `EDITAR`, con `datos_anteriores` y `datos_nuevos` **solo de los campos que cambiaron** (RN-VE-009).
- Respuestas: 200, 400, 404 y 409 (placa repetida o vehículo VENDIDO).

**`POST /api/inventario/vehiculos/{id}/reservar | liberar | inactivar | reactivar`** — cambios de estado (HU-020).
- Petición: `{ motivo? }`. El motivo es obligatorio para inactivar.
- Respuestas: 200 con el detalle; 400; 404; 409 "No se puede <acción> un vehículo en estado <estado en minúscula>."
- Auditoría con la acción en mayúsculas.

### Fotos (HU-014, RN-VE-001, D-20)

**`POST /api/inventario/vehiculos/{id}/fotos`** — subir una foto.
- `multipart/form-data` con un campo `archivo`, una foto por petición.
- Formatos aceptados: JPEG, PNG y WebP, validados por la **firma de los bytes**, no por la extensión. Máximo **5 MB**.
- Máximo **10** fotos por vehículo (D-20, PROPUESTO).
- Se guarda en la carpeta `Media:Ruta`, que está **fuera del repositorio** y se configura con user-secrets o variable de entorno; por defecto `C:\CAMION_media` (D-20).
- El nombre del archivo es aleatorio: `vehiculos/<id>/<guid>.<ext>`. El nombre original nunca se usa.
- `orden` es el siguiente libre. La primera foto es la portada.
- Respuesta 201 `{ id, url, orden, esPortada }`.
- Errores:
  - 400 "Sube una imagen JPG, PNG o WebP de hasta 5 MB."
  - 409 "Ya hay 10 fotos; elimina alguna para subir otra."
- Límite del cuerpo de la petición: 6 MB.

**`DELETE /api/inventario/vehiculos/{id}/fotos/{fotoId}`**
- Borra la fila y el archivo, y renumera el orden.
- Si era la portada, la nueva portada es la primera.
- Respuestas: 204 y 404.

**`PUT /api/inventario/vehiculos/{id}/fotos/orden`**
- Petición: `{ fotoIds: [..] }`, con todas las fotos del vehículo en el orden nuevo. La primera pasa a ser la portada.
- Respuestas: 200 con la lista de fotos; 400 si faltan fotos o sobran ids.

Las fotos de un vehículo VENDIDO no se pueden modificar (409).

### Servicio de archivos `/media`

- `GET /media/{**ruta}` sirve archivos de `Media:Ruta`, solo con extensión jpg, jpeg, png o webp.
- Protege contra rutas con `..` y contra rutas absolutas: la ruta final debe quedar dentro de la carpeta. Si no, 404.
- Cabeceras: `Cache-Control: public, max-age=86400` y `X-Content-Type-Options: nosniff`.
- Es anónimo, porque el catálogo lo necesita. El proxy de Vite añade `/media`.

### Catálogos para los formularios

**`GET /api/inventario/catalogos`** (AsesorOAdministrador)
- Respuesta:
  ```json
  { tipos, marcas: [{ id, nombre, lineas: [{ id, nombre }] }], carrocerias, materiales,
    tiposMaquina, combustibles, departamentos: [{ id, nombre, municipios: [{ id, nombre }] }] }
  ```
- Solo incluye las entradas con `activo = 1`.
- Para el administrador incluye además `asesores: [{ id, nombre }]` (solo los ACTIVA).
- La gestión de estos catálogos es P7 (HU-081 a HU-087).

## Catálogo público (P3) tras P4a

- El catálogo usa la **condición 7** de visibilidad descrita arriba.
- Las fotos y el precio siguen saliendo de la publicación. P4b los unificará con el vehículo.
- El catálogo puede mostrar `placa`. PROPUESTO: **no** se muestra al público.

## Interfaz

- **Acceso.** En "Mi cuenta", para ASESOR y ADMINISTRADOR, aparece el enlace "Mi inventario" (asesor) o "Inventario" (administrador). La ruta es `#/inventario`. Un COMPRADOR no ve el enlace, y si entra en la ruta ve "No tienes permiso para realizar esta acción."
- **Lista (`#/inventario`).**
  - Filtros: estado, categoría y búsqueda. El administrador también puede filtrar por asesor.
  - Tarjetas o tabla con foto, título (marca + línea + año), placa, precio, estado (con texto) y acciones según el estado.
  - Botón "Registrar vehículo".
  - Paginación.
- **Formulario (`#/inventario/nuevo`, `#/inventario/<id>`).**
  - Primero se elige la categoría. Según la categoría se muestran solo los campos que aplican (tabla de arriba), con su obligatoriedad.
  - Marca → modelo (línea), y departamento → municipio.
  - Precio con formato de miles.
  - Mensajes por campo, incluida la placa repetida.
  - Al editar se muestra "Precio anterior: $ …".
- **Fotos.**
  - Subida con la selección de archivos accesible y una barra o texto de progreso.
  - Vista previa y botones "Subir", "Bajar", "Hacer portada" y "Eliminar" con confirmación.
  - Contador "N de 10".
  - Los errores de formato o tamaño se muestran antes de enviar y también cuando los devuelve el servidor.
- **Acciones de estado.** Reservar, Liberar, Inactivar (con diálogo de motivo, reutilizando `DialogoAccion`) y Reactivar.
- Diseño responsivo y accesible, como en P2 y P3.

## Fuera de P4a

- Publicaciones, plan y cupo, vigencia, destacados y vencimiento (P4b).
- Marcar como vendido (HU-064, D-22).
- Gestión de catálogos (P7).
- Retirar el DEMO (D-24).
