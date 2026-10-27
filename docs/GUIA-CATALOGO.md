# Guía del catálogo público (P3)

Esta guía explica qué ve una persona en el catálogo del portal, qué hace que un vehículo aparezca o desaparezca, cómo se usan los filtros y cómo cargar (y quitar) los datos de demostración para probarlo.

- Estado: todo lo que se describe está **IMPLEMENTADO** en la rama `feature/p3-catalogo`. Las pruebas automáticas de P3 se están escribiendo: **todavía nada de P3 está PROBADO**.
- El script de datos de demostración **no está aplicado** en `camion_ventas`. Lo aplica la responsable siguiendo la sección 5 (D-16, D-18).
- Trazabilidad: HU-001 a HU-012; RN-VE-011 a RN-VE-014; RN-SE-005. Decisiones D-3, D-11, D-16, D-17, D-18 y D-19 (`docs/coordinacion/DECISIONES.md`).
- Detalle técnico de la API: `docs/criterios/P3-contrato-api.md`.

Antes de empezar necesitas el backend y el frontend funcionando en tu equipo (ver `README.md`).

---

## 1. Cómo se entra

| Quiero… | Abro |
|---|---|
| Ver el catálogo | http://localhost:5173/#/ |
| Buscar y filtrar | http://localhost:5173/#/buscar |
| Iniciar sesión o ver mi cuenta | http://localhost:5173/#/cuenta (enlace **Iniciar sesión** o **Mi cuenta** del encabezado) |
| Crear una cuenta | http://localhost:5173/#/cuenta/registro (enlace **Registrarme**) |

La página principal es ahora el catálogo. El inicio de sesión ya no es la primera pantalla: está en `#/cuenta`.

---

## 2. Qué ve el público y qué ve el comprador

**El catálogo es igual para todos.** Una persona sin cuenta, un comprador con su sesión abierta, un asesor o el administrador ven exactamente los mismos vehículos y los mismos datos. El catálogo no usa la sesión: no hace falta registrarse para mirarlo (D-3).

La única diferencia está en el encabezado:

- **Sin sesión**: enlaces **Iniciar sesión** y **Registrarme**.
- **Con sesión**: enlace **Mi cuenta**.

Nadie puede crear, editar ni borrar vehículos desde el catálogo: no hay ningún botón para hacerlo. El catálogo solo lee.

### El encabezado (en todas las páginas)

- El nombre **Financiamos Tu Camión**, que lleva al inicio.
- Un buscador ("Marca, modelo o título") con el botón **Buscar**.
- Las categorías **Carros**, **Camiones**, **Carrocerías** y **Maquinaria** (D-17). Una categoría solo aparece si tiene al menos un vehículo visible.
- Los enlaces de la cuenta.

### La página de inicio (`#/`)

- Accesos a las categorías y el botón **Ver todos los vehículos**.
- **Destacados**: hasta 8 vehículos destacados.
- **Recientes**: los 12 vehículos publicados más recientemente (pueden incluir destacados).
- Si no hay ningún vehículo visible, se ve "Pronto tendremos vehículos publicados." Es lo normal mientras no haya datos: el alta de vehículos llega en P4.

### Las tarjetas

Cada vehículo se muestra en una tarjeta con título, precio, año, kilometraje, ciudad, foto y las etiquetas que correspondan:

- **Destacado**.
- **Reservado** (el vehículo está apartado para un cliente; sigue visible, D-11).

El kilometraje no se muestra en las **carrocerías**, ni en la **maquinaria** que tiene kilometraje 0 (D-17).

Si el vehículo no tiene foto, o la foto no carga, se ve un dibujo con el texto "Sin foto". Hoy pasa siempre: la subida y el servicio de fotos reales llegan en P4.

### El detalle de un vehículo (`#/vehiculo/<slug>`)

Al pulsar una tarjeta se abre su ficha:

- Fotos, precio y etiquetas (**Destacado**, **Reservado**, **Negociable**).
- **Ficha técnica**: categoría, carrocería, marca, modelo, año, kilometraje, combustible, capacidad de carga, peso bruto, cilindraje y ubicación. Solo se muestran los datos que tienen valor.
- **Descripción** y **Descripción técnica**, si las hay.
- El enlace **Volver a resultados** regresa a la última búsqueda.

El detalle **no** muestra datos del asesor ni un botón de contacto: el contacto llega en P5.

Si alguien abre la dirección de un vehículo que ya no se puede mostrar (vendido, vencido, oculto o de un asesor bloqueado), ve "Este vehículo no está disponible." y un enlace **Ir al inicio**. El mensaje es el mismo en todos los casos, para no revelar el motivo.

---

## 3. Qué hace que un vehículo aparezca o desaparezca

Un vehículo aparece en el catálogo **solo si cumple todas** estas condiciones a la vez. Si falla una sola, desaparece del inicio, de la búsqueda, de los filtros y de su página de detalle. La regla está escrita en un único lugar del código (`backend/Camion.Api/Catalogo/ConsultasCatalogo.cs`), así que se aplica igual en todas las pantallas.

| # | Condición | Qué significa en el negocio |
|---|---|---|
| 1 | **Está publicado** | El asesor lo publicó. Un borrador u oculto no se ve. |
| 2 | **No está vendido** | Se ven los Disponibles y los **Reservados**. Un vendido desaparece siempre (RN-VE-014). |
| 3 | **Está dentro de su vigencia** | La publicación tiene un periodo de vigencia con fecha de inicio y de fin, y hoy está dentro de ese periodo. La regla del negocio es una vigencia de **30 días** (v3.0); el periodo de cada publicación lo fijará el alta de vehículos de P4. Al llegar la fecha de fin, desaparece sola (RN-VE-013). |
| 4 | **El plan del asesor está activo** | La suscripción del asesor a su plan, con la que se dio esa vigencia, está activa y no ha vencido. Si la suscripción se cancela o vence, sus vehículos desaparecen aunque la publicación siguiera en fecha. |
| 5 | **El asesor está activo** | La cuenta del asesor dueño está activa. Si el administrador **bloquea** al asesor (o el asesor está rechazado o pendiente), todos sus vehículos desaparecen. Si lo desbloquea, vuelven a verse (si siguen cumpliendo lo demás). |
| 6 | **Tiene precio y título** | Precio mayor que 0 y un título escrito. |

Además:

- **Reservado**: se sigue viendo, con la etiqueta "Reservado" (D-11, D-17). Ser reservado no salta la regla: un reservado vencido o de un asesor bloqueado tampoco se ve.
- **Destacados primero**: un vehículo destacado lleva la etiqueta "Destacado" mientras su destacado esté activo y en fecha. En los resultados de búsqueda los destacados van **siempre primero**, sea cual sea el orden elegido; detrás van los demás, en ese orden (RN-VE-012). Ser destacado no salta la regla de visibilidad: un destacado vendido o vencido no se ve.
- Las fechas se comparan con la hora UTC del servidor (en Colombia, 5 horas más que la hora local).

---

## 4. Buscar y filtrar

### Buscar

Escribe en el buscador del encabezado una marca, un modelo o parte del título (por ejemplo "npr" o "Hilux") y pulsa **Buscar**. No importan las mayúsculas. Admite hasta 100 caracteres.

Los enlaces de categoría (Carros, Camiones, Carrocerías, Maquinaria) abren la búsqueda con esa categoría ya elegida.

### Los filtros

En la página de búsqueda, a la izquierda (en el celular, con el botón **Mostrar filtros**):

| Filtro | Cómo se usa |
|---|---|
| **Buscar** | El mismo texto del buscador del encabezado. |
| **Categoría** | Todas, Carros, Camiones, Carrocerías o Maquinaria. Entre paréntesis, cuántos vehículos visibles hay. |
| **Marca** | Solo aparecen las marcas que tienen vehículos visibles, con su cantidad. |
| **Modelo** | Se activa al elegir una marca ("Elige primero una marca"). Al cambiar de marca, el modelo se vacía. "Modelo" es la línea del fabricante, por ejemplo Chevrolet NPR; el año va aparte (D-17). |
| **Presupuesto (COP)** | Mínimo y máximo, en pesos, sin centavos. Se ven con puntos de miles mientras escribes. |
| **Año** | Desde y hasta, entre 1900 y 2200. |
| **Kilometraje máximo** | Número entero. |
| **Ordenar por** | Más recientes (por defecto), Menor precio, Mayor precio, Año más reciente o Menor kilometraje. Los destacados siempre van primero. |

Pulsa **Aplicar filtros**. Si algún dato está mal escrito, el campo muestra el error y no se busca. Por ejemplo:

- "El precio máximo debe ser mayor o igual al mínimo."
- "El año final debe ser mayor o igual al inicial."
- "Escribe un año entre 1900 y 2200."

Los resultados salen en páginas de 12, con el total de vehículos encontrados arriba.

### Limpiar filtros

El botón **Limpiar filtros** borra todos los filtros y la búsqueda, y vuelve a mostrar todos los vehículos visibles. También aparece cuando no hay resultados ("No encontramos vehículos con esos filtros.") o cuando hay un error en los filtros.

### Compartir una búsqueda

Los filtros quedan en la dirección de la página (por ejemplo `#/buscar?tipoId=2&precioMax=150000000`). Se puede copiar y enviar esa dirección, y los botones Atrás y Adelante del navegador recuerdan cada búsqueda.

---

## 5. Cargar los datos de demostración (D-18)

Hoy el catálogo está vacío porque el alta de vehículos llega en P4. Para probarlo, la responsable autorizó cargar unos **datos de demostración** (D-18). No son datos reales y se quitan antes de cargar datos reales.

### Qué carga el script

El archivo es `database/demo/demo_p3_catalogo.sql`. Solo añade filas (no borra nada ni cambia el esquema) y todo va marcado como DEMO:

- Un **asesor DEMO** (`asesor.demo@ejemplo.test`, "Concesionario DEMO P3"), aprobado y **sin contraseña**: nadie puede iniciar sesión con él.
- Un plan de prueba "Plan DEMO P3" (inactivo, precio 0) y una suscripción **activa por 30 días** desde el momento de la carga.
- **12 publicaciones DEMO** (su dirección empieza por `demo-`):

| Categoría | Publicaciones | Se ven |
|---|---|---|
| Camión | Chevrolet NPR 2019 (destacado), Kenworth T800 2016 (destacado), Hino Dutro 2021 (reservado), Chevrolet NHR 2020, Hino Serie 500 2015 (**vendido**) | 4 de 5 |
| Carro | Renault Duster 2022, Toyota Hilux 2020 (reservado), Renault Logan 2017 (**vencido**) | 2 de 3 |
| Carrocería | Furgón Randon 2023, Volqueta Randon 2022 (reservado) | 2 de 2 |
| Maquinaria | Retroexcavadora Caterpillar 416F 2018, Montacargas Toyota 8FGCU25 2019 | 2 de 2 |

  El vendido y el vencido están a propósito, para comprobar que **no** aparecen. Resultado: **10 publicaciones visibles**, 2 destacadas y 3 reservadas.
- Si no existen ya, añade también los catálogos: las cuatro categorías (Carro, Camión, Carrocería, Maquinaria), las carrocerías Furgón y Volqueta, el combustible Diésel, el departamento de Antioquia con Medellín y Envigado, 7 marcas (Chevrolet, Hino, Kenworth, Renault, Toyota, Randon, Caterpillar) y 12 modelos. Los que ya existían se reutilizan sin tocarlos.

Dos publicaciones tienen fotos registradas, pero los archivos no existen: se verá "Sin foto". Es lo esperado hasta P4.

La vigencia DEMO dura **30 días** desde la carga. Pasado ese tiempo, los vehículos DEMO desaparecen solos del catálogo (es la regla de la sección 3 funcionando).

### Antes de empezar

- Se ejecuta **una sola vez**. Si el asesor DEMO ya existe, el script falla y no se guarda nada (primero hay que quitarlo con la sección BAJAR, paso 5).
- Requiere la migración 003 aplicada (ya lo está, D-16) y que el backend haya arrancado al menos una vez (crea el rol de asesor).
- Usa **Chrome o Edge**. La ventana de confirmación de phpMyAdmin no funciona en el navegador integrado de Claude (D-16).
- Ten a mano el archivo `database/demo/demo_p3_catalogo.sql` abierto en el Bloc de notas.
- Si en cualquier paso algo no coincide con lo esperado, **para y avisa al coordinador**.

### Paso 1. Respaldo completo nuevo

1. Abre http://localhost/phpmyadmin en Chrome o Edge.
2. En la lista de la izquierda pulsa la base **`camion_ventas`** (que quede seleccionada la base, no una tabla).
3. Pulsa la pestaña **Exportar**.
4. En "Método de exportación" elige **Personalizado - mostrar todas las opciones posibles**.
5. En "Tablas" comprueba que están marcadas **todas** las tablas (usa **Seleccionar todo** si hace falta), con estructura y datos.
6. En "Salida", deja **Guardar salida a un archivo** y escribe como nombre del archivo `camion_ventas_completo_antes_demo_p3_AAAAMMDD_HHMM`, cambiando `AAAAMMDD_HHMM` por la fecha y hora de ahora (por ejemplo `20261026_1530`). Formato: SQL.
7. Pulsa **Continuar** (abajo del todo). El navegador descarga el archivo.
8. Mueve el archivo descargado a la carpeta `C:\Respaldo_CAMION\`.
   - Si ya hay un archivo con ese nombre, **no lo reemplaces**: cambia la hora en el nombre.
   - Nunca borres ni sobrescribas un respaldo existente.
9. Comprueba que el archivo existe y no está vacío. En PowerShell:

```powershell
Get-Item C:\Respaldo_CAMION\camion_ventas_completo_antes_demo_p3_AAAAMMDD_HHMM.sql | Select-Object Name, Length
```

   `Length` debe ser mayor que 0 (un respaldo completo de esta base pesa decenas de KB). Si no existe o mide 0, **para aquí** y avisa al coordinador.
10. Anota la ruta del respaldo.

### Paso 2. Conteos ANTES

1. En phpMyAdmin, con `camion_ventas` seleccionada, abre la pestaña **SQL**.
2. Copia del script solo la consulta del apartado **"0. VERIFICACIÓN ANTES"**: desde `SELECT 'antes' AS momento,` hasta el primer `;` (la línea que termina en `AS lineas;`).
3. Pégala y pulsa **Continuar**.
4. **Anota** los seis números. Lo esperado:
   - `usuarios_demo`, `planes_demo` y `publicaciones_demo`: **0**. Si alguno no es 0, los datos DEMO ya están cargados: **no sigas** y avisa.
   - `tipos_vehiculo`, `marcas` y `lineas`: lo que haya en tu base (hoy probablemente 0). Guárdalos: sirven para el paso 5.

### Paso 3. Ejecutar solo la sección SUBIR

1. Vuelve a la pestaña **SQL** y borra lo que haya en el cuadro.
2. En el Bloc de notas, copia **desde** la línea `START TRANSACTION;` (justo debajo del título "SUBIR (solo INSERT)") **hasta** la línea `COMMIT;` incluida (antes del apartado "5. VERIFICACIÓN DESPUÉS").
3. Pégalo en phpMyAdmin y pulsa **Continuar**.
4. Debe terminar sin ningún mensaje de error en rojo.

### Paso 4. Conteos DESPUÉS

1. En la pestaña **SQL**, borra el cuadro y copia del script las dos consultas del apartado **"5. VERIFICACIÓN DESPUÉS"**: desde `SELECT 'despues' AS momento,` hasta `ORDER BY t.nombre;` (justo antes del título "BAJAR").
2. Pégalas y pulsa **Continuar**. Aparecen dos resultados.
3. Comprueba la primera tabla (lo que dice el script):

| Columna | Esperado |
|---|---|
| `usuarios_demo` | 1 |
| `planes_demo` | 1 |
| `publicaciones_demo` | 12 |
| `visibles_demo` | **10** |
| `destacadas_demo` | 2 |

4. Comprueba la segunda tabla (publicaciones DEMO por categoría, incluidas las que no se ven): **Camión 5, Carro 3, Carrocería 2, Maquinaria 2**.
5. Anota los números.
6. Abre http://localhost:5173/#/ (con el backend encendido). Deberías ver:
   - En **Destacados**: Chevrolet NPR y Kenworth T800.
   - En **Recientes**: las 10 visibles.
   - Las etiquetas "Reservado" en el Hino Dutro, la Toyota Hilux y la volqueta Randon.
   - Que **no** aparecen el Hino Serie 500 (vendido) ni el Renault Logan (vencido).

Si algún número no coincide, avisa al coordinador con los números que anotaste.

### Paso 5. Quitar los datos DEMO (sección BAJAR) antes de cargar datos reales

Hazlo cuando termine la prueba, y siempre **antes** de empezar a cargar vehículos reales.

1. Haz otro respaldo completo **nuevo**, como en el paso 1, con otro nombre (por ejemplo `camion_ventas_completo_antes_bajar_demo_p3_AAAAMMDD_HHMM`).
2. En el Bloc de notas, busca al final del script el título **"BAJAR (comentada)"**. Copia el primer bloque: desde `-- START TRANSACTION;` hasta el primer `-- COMMIT;`.
3. Pégalo en un documento nuevo del Bloc de notas y **quita los dos guiones y el espacio (`-- `) del principio de cada línea**. Es lo que hace que esas líneas dejen de ser comentarios y se ejecuten.
4. Pega el resultado en la pestaña **SQL** de `camion_ventas` y pulsa **Continuar**. Si phpMyAdmin pide confirmar el borrado, acepta (solo borra filas DEMO).
5. Comprueba: copia la consulta "Verificación tras BAJAR" que viene justo debajo (de `-- SELECT` hasta `AS vehiculos_demo;`), quítale también los `-- ` y ejecútala. Los cuatro números deben ser **0**.

Los catálogos (categorías, marcas, modelos, carrocerías, combustible, departamento y municipios) **no se borran** con BAJAR: son datos de referencia útiles para P4, y no se puede saber si los puso este script o ya existían. El script trae un bloque **OPCIONAL** para quitarlos; úsalo solo si en el paso 2 `marcas` y `lineas` eran 0, y solo con el visto bueno del coordinador. Las cuatro categorías se conservan siempre (D-17).

Si BAJAR da un error, lo más probable es que alguien haya usado una publicación DEMO o el asesor DEMO (por ejemplo, el administrador actuó sobre él). No se borra nada: avisa al coordinador.

### Paso 6. Si algo da error

- **No se queda nada a medias.** SUBIR y BAJAR van dentro de una transacción (`START TRANSACTION` … `COMMIT`): si una instrucción falla, phpMyAdmin se detiene antes del `COMMIT` y ninguna fila de esa ejecución se guarda.
- Para confirmarlo, vuelve a ejecutar los conteos del paso 2: deben dar lo mismo que antes.
- **No** repitas el script ni intentes arreglarlo a mano.
- Haz una captura del mensaje de error (si incluyera datos de conexión, no lo copies en ningún documento) y **avisa al coordinador** con:
  - el paso en que estabas,
  - el mensaje de error,
  - los conteos que anotaste,
  - la ruta del respaldo.
- Restaurar un respaldo reemplaza la base entera: no lo hagas sin ayuda del equipo técnico y sin aprobación de la responsable.

---

## 6. Qué falta (PROPUESTO para los próximos ítems)

- **P4**: alta y edición de vehículos y publicaciones por el asesor (con su vigencia de 30 días y plan), subida y servicio de fotos reales, placa (D-2), marcar Reservado y Vendido (D-11) y los campos propios de carrocerías y maquinaria (D-19).
- **P5**: contactar al vendedor desde el detalle del vehículo.
