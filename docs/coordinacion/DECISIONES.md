# Registro de decisiones

Estados: PENDIENTE (requiere a la responsable) / TOMADA / DESCARTADA.
Las decisiones D-1 a D-5 se detectaron al comparar los documentos fuente el 2026-10-26 y la responsable las resolvió el mismo día.

## D-1 — ¿Qué numeración de HU es la oficial? · TOMADA (2026-10-26)
- Contexto: en `requisitos-funcionales-v3.0.md` la matriz consolidada (sección 7) tiene 87 HU (HU-043 = Aprobar Usuario), pero la lista de la sección 10 tiene 90 (HU-043 = Registrar Usuario, todo desplazado +1, y un módulo nuevo "CRM Comercial" HU-089/HU-090).
- **Decisión: la matriz de 87 HU (sección 7) es la numeración oficial.** Commits, criterios y pruebas citan esa numeración. El módulo CRM Comercial (HU-089/HU-090 de la sección 10) queda fuera del alcance actual y se tratará como cambio de alcance posterior.

## D-2 — ¿La placa es obligatoria y única? · TOMADA (2026-10-26)
- Contexto: la minuta dice "placa única"; RN-VE-002 (v3.0) dice "Dato opcional. No obligatoria ni única".
- **Decisión: la placa es opcional, pero única cuando se registra.** Se permite dejarla vacía; si se informa, no puede repetirse en otro vehículo (validación en API y restricción en base de datos que admita varios vacíos). Esto modifica RN-VE-002 respecto a la línea base v3.0.

## D-3 — ¿Quién requiere aprobación del administrador? · TOMADA (2026-10-26, corregida)
- Contexto: RN-US-001 dice "Todo usuario debe ser aprobado"; la minuta indicaba solo asesores y el registro de comprador actual permite entrar sin aprobación.
- Primera respuesta ("todos los usuarios") **anulada** por la responsable el mismo día.
- **Decisión: solo los asesores requieren aprobación del administrador.** RN-US-001 aplica a asesores.
- **Perfil comprador** (perfil independiente, distinto de asesor y administrador):
  - El registro es **opcional**: sin cuenta se consulta el catálogo y se contacta (RN-SE-005, RN-CL-001).
  - Si se registra, el acceso es **inmediato**, sin aprobación (como funciona hoy en `main`).
  - Acceso de **solo lectura**: no puede crear, editar ni modificar vehículos, publicaciones ni datos de otros usuarios.
  - Puede buscar opciones de compra, pedir un seguro, solicitar crédito y vender su vehículo; todo lo atiende el administrador (ver D-6 y D-7).

## D-6 — Alcance nuevo: "Vende tu camión" y "Compra tu seguro todo riesgo" · TOMADA (2026-10-26)
- Contexto: funciones nuevas pedidas por la responsable; no están en la línea base v3.0 (87 HU). Se identifican provisionalmente como **HU-N01** y **HU-N02** hasta que se integren al documento maestro.
- **HU-N01 — Aviso "Vende tu camión"** (portal público):
  - Para el cliente que quiere vender su propio camión o carro.
  - La solicitud va **siempre al administrador**, nunca a un asesor.
  - Formulario: **nombre** (obligatorio), **teléfono WhatsApp** (obligatorio, formato de teléfono válido), **descripción del vehículo** (campo abierto, obligatorio, máximo **500 caracteres**).
  - No crea una publicación: solo registra una solicitud para el administrador.
- **HU-N02 — Aviso "Compra tu seguro todo riesgo"** (portal público):
  - Formulario: **nombre del cliente**, **teléfono** y **tipo de vehículo** (todos obligatorios).
  - La solicitud la atiende **el administrador**.
- Ambos formularios no exigen cuenta (D-3).
- PROPUESTO por el coordinador (confirmar al diseñar):
  - Estados Abierta → Cerrada, como los leads.
  - El administrador ve las solicitudes en su panel.
  - "Tipo de vehículo" como lista: camión / carro / otro.

## D-7 — Regla de asignación: todo servicio lo atiende el administrador · TOMADA (2026-10-26)
- **Decisión: ningún servicio queda asignado a un asesor**, excepto los contactos/leads de **sus propios vehículos publicados mientras su plan esté vigente**. Todo lo demás lo atiende el administrador: créditos, búsquedas, solicitudes generales, "Vende tu camión" y seguros.
- Modifica la línea base v3.0:
  - RN-CL-003 / HU-031: un lead de publicación va al asesor dueño solo con plan vigente; si no, al administrador.
  - RN-CO-007 / RN-CO-008 / HU-041 / HU-042: créditos asignados y gestionados por el administrador.
  - RN-BU-002 / HU-035 a HU-038: búsquedas gestionadas por el administrador.
- **Reasignación manual (confirmada 2026-10-26):** el administrador puede reasignar manualmente cualquier solicitud a un asesor si lo decide. La asignación automática siempre va al administrador. La reasignación debe quedar auditada: quién, cuándo, de quién a quién (RN-SE-003).

## D-4 — Estados incompletos en la línea base · TOMADA (2026-10-26)
- Contexto: las transiciones usan Crédito "Cerrado" y Suscripción "Reactivada", que no están en la tabla de estados; RN-VE-008 menciona solo Activo/Inactivo pero existe Vendido.
- **Decisión: se añaden los tres como estados propios:** Crédito `Cerrado`, Vehículo `Vendido` y Suscripción `Reactivada`.
- Estados resultantes: Vehículo Activo/Inactivo/Vendido; Crédito Registrado/Asignado/Gestionado/Cerrado; Suscripción Activa/Suspendida/Reactivada/Vencida.

## D-5 — Envío de credenciales vs. autorregistro · TOMADA (2026-10-26)
- Contexto: RN-US-004 "Envía credenciales iniciales" por correo, mientras que el registro actual permite que el usuario elija su contraseña. No hay servicio de correo configurado.
- **Decisión: autorregistro con contraseña elegida por el usuario.** No se envían credenciales. Cuando se apruebe una integración de correo, el correo solo notificará la aprobación de la cuenta.

## D-8 — ¿Qué HU oficial se cita para el registro de comprador? · TOMADA (2026-10-26)
- Contexto: la matriz oficial de 87 HU (D-1) no tiene una HU "Registrar usuario/comprador"; solo aparece como HU-043 en la lista de 90 HU descartada. El registro de comprador ya existe en el código (`ESTADO-REAL.md`, secciones 7 y 11).
- Opciones: A) crear HU-N03 "Registro de comprador" (como HU-N01/N02); B) citarlo bajo HU-059 / RN-SE-001 (inicio de sesión).
- Recomendación del coordinador: A, para que tenga sus propios criterios de aceptación.
- **Decisión: A. Se crea HU-N03 "Registro de comprador"**, con sus propios criterios y pruebas.

## D-9 — Datos mínimos de un visitante sin cuenta al contactar · TOMADA (2026-10-26)
- Contexto: D-3 y RN-SE-005 permiten contactar sin cuenta, pero el script SQL exige usuario y correo en `solicitudes` (`002_…sql:347`, `352`). HU-N01 pide solo nombre y WhatsApp.
- Opciones: A) nombre + teléfono obligatorios, correo opcional; B) nombre + teléfono + correo obligatorios.
- Recomendación del coordinador: A, coherente con HU-N01/N02.
- **Decisión: A. Nombre y teléfono obligatorios, correo opcional**, en todo formulario de contacto o solicitud sin cuenta. Las solicitudes no exigen usuario registrado.

## D-10 — Estados definitivos de la cuenta de asesor · TOMADA (2026-10-26)
- Contexto: el script usa AUTORIZADO/DESAUTORIZADO; la v3.0 usa Aprobado/Rechazado/Bloqueado (D-4 no cubrió usuarios).
- Opciones: A) Pendiente / Aprobado / Rechazado / Bloqueado (v3.0 + estado inicial de D-3); B) los del script.
- Recomendación del coordinador: A.
- **Decisión: A. Estados del asesor: Pendiente / Aprobado / Rechazado / Bloqueado.** Los valores AUTORIZADO/DESAUTORIZADO del script se migran a estos sin perder registros.

## D-11 — ¿Se mantiene el estado "Reservado" de publicación? · TOMADA (2026-10-26)
- Contexto: existe en el script SQL, pero no en la v3.0 ni en D-4.
- Opciones: A) eliminarlo (solo Activo / Inactivo / Vendido); B) mantenerlo como estado extra.
- Recomendación del coordinador: A, salvo que el negocio aparte vehículos para un cliente.
- **Decisión: B. Se mantiene "Reservado"**: en el negocio se aparta un camión para un cliente mientras se cierra la venta. Estados del vehículo: Activo / Reservado / Inactivo / Vendido.
- PROPUESTO (confirmar al diseñar P4): Activo → Reservado → Vendido, o Reservado → Activo si la venta no se cierra; lo marcan el asesor dueño o el administrador; en el catálogo público se sigue mostrando con la etiqueta "Reservado".

## D-12 — Cierre de sesión por inactividad y alcance de "Cerrar sesión" · TOMADA (2026-10-26)
- Contexto: RN-SE-002 pide cerrar sesiones inactivas sin fijar el tiempo (detectada en P1).
- **Decisión: la sesión se cierra tras 30 minutos sin actividad, y "Cerrar sesión" la cierra en todos los equipos del usuario** (invalida todos sus tokens mediante `SessionVersion`). Es el comportamiento implementado y probado en P1.

## D-13 — ¿Cómo se crea el primer administrador? · TOMADA (2026-10-26)
- Contexto: el panel de administración (P2) solo funciona con una cuenta de rol ADMINISTRADOR, y hoy no existe ninguna. Es una decisión de seguridad: nunca debe haber contraseñas en el repositorio.
- Opciones:
  - A) **Comando local de promoción**:
    - La persona se registra en la web con su propia contraseña.
    - En el equipo del servidor se ejecuta `dotnet run --project backend/Camion.Api -- promover-admin <correo>`. El comando usa la conexión de `dotnet user-secrets`, añade el rol ADMINISTRADOR, quita COMPRADOR y deja el evento en `auditoria_eventos`.
    - Ventaja: el comando nunca maneja contraseñas.
    - Impacto: requiere acceso al equipo del servidor, lo que es correcto para una acción así.
  - B) **Comando local que crea el administrador**: correo y contraseña se piden por teclado, sin quedar en el historial ni en archivos.
    - Impacto: maneja una contraseña en la consola.
  - C) **Script SQL manual**.
    - Impacto: hay que calcular el hash de Identity a mano; propenso a errores. No se recomienda.
- Recomendación del coordinador: A.
- Bloquea: el uso real del panel de administración contra `camion_ventas` (en pruebas automáticas el administrador se crea en la base en memoria).
- **Decisión: A. "Registrarme y luego promoverme"** (respuesta de la responsable, transmitida por el coordinador principal). Se implementa el comando local `promover-admin <correo>`, que no maneja contraseñas y queda auditado. Lo ejecutará la responsable, guiada, durante la prueba manual; los agentes no lo ejecutan contra `camion_ventas`.

## D-14 — Aplicar la migración 003 (estados del asesor) a `camion_ventas` · TOMADA (2026-10-26)
- Contexto: D-10 fijó los estados Pendiente / Aprobado / Rechazado / Bloqueado, pero la tabla `perfiles_asesor` tiene una restricción que solo admite PENDIENTE / AUTORIZADO / DESAUTORIZADO.
- La migración `database/migraciones/003_p2_estados_asesor.sql`:
  - Cambia esa restricción y convierte los valores antiguos.
  - Tiene vuelta atrás (sección BAJAR).
  - Indica cómo hacer el respaldo previo en `C:\Respaldo_CAMION\`.
- Hasta que se aplique, el registro de asesor sí funciona (estado PENDIENTE). Fallan aprobar, rechazar y bloquear asesores contra la base real.
- Opciones:
  - A) Aplicarla ahora, con el respaldo previo y comprobando los conteos antes y después.
    - Impacto: el panel funciona completo con la base real.
  - B) Esperar a P4 y agruparla con los demás cambios de esquema (placa, estados del vehículo).
    - Impacto: la aprobación de asesores no se puede usar con la base real hasta entonces.
- Recomendación del coordinador: A. La tabla `perfiles_asesor` está vacía o casi vacía, porque hasta hoy no existía el registro de asesor.
- Bloquea: el uso real de la aprobación de asesores.
- **Decisión: A. "Ahora, con respaldo previo"** (respuesta de la responsable, transmitida por el coordinador principal). Condiciones:
  1. Antes de nada, un respaldo completo de la base en un archivo **nuevo**, con fecha y hora, dentro de `C:\Respaldo_CAMION\`. Nunca se sobrescriben los existentes. Se comprueba que el archivo existe y no está vacío.
  2. Se cuentan las filas de `AspNetUsers`, `perfiles_asesor`, `perfiles_comprador` y `AspNetUserRoles` antes y después; los conteos deben coincidir.
  3. Se aplica solo la sección SUBIR. Si algo falla, se ejecuta BAJAR y se avisa.
  4. Se aplica solo cuando el código de P2 compile y pase sus pruebas.
  5. En la bitácora quedan la ruta del respaldo, los conteos y el resultado.

## D-15 — ¿Qué pasa con un asesor rechazado y con un comprador que quiere ser asesor? · TOMADA (2026-10-26)
- Contexto: con el contrato de P2, un asesor rechazado no tiene salida. No puede volver a pedir el alta, porque su correo y su documento ya existen (409). Un comprador tampoco puede pasar a asesor con el mismo correo. La v3.0 no dice nada sobre ninguno de los dos casos (C-P2-2 y C-P2-5 de `docs/criterios/P2.md`).
- Opciones:
  - A) El administrador puede **reconsiderar**: aprobar a un asesor rechazado (paso de Rechazado a Aprobado, con motivo y auditoría). Un comprador que quiere ser asesor usa por ahora otro correo; más adelante se añade "Solicitar ser asesor" desde su cuenta.
    - Impacto: es un cambio pequeño.
  - B) Un rechazo es definitivo. Para volver a intentarlo hay que hablar con el administrador, sin cambios en el sistema.
    - Impacto: no hay trabajo nuevo, pero no hay salida dentro del sistema.
- Recomendación del coordinador: A.
- **Decisión: A. "El admin puede reconsiderar"** (respuesta de la responsable, transmitida por el coordinador principal). El administrador puede pasar a un asesor de Rechazado a Aprobado, con motivo obligatorio y auditoría; se incluye en P2. Un comprador que quiera ser asesor usa, por ahora, otro correo. "Solicitar ser asesor" desde la cuenta del comprador queda como ítem futuro del backlog.
- Nota de trazabilidad (C-P2-1): la matriz asocia HU-047 "Bloquear usuario" a RN-US-005, pero en la v3.0 esa regla es "Notificación de acceso". Se mantiene HU-047 como referencia del bloqueo.

## D-16 — ¿Quién aplica la migración 003 en `camion_ventas`? · TOMADA (2026-10-26)
- Contexto: el sistema de permisos impidió que el agente escribiera en `camion_ventas` (detectada en el cierre de P2).
- **Decisión: la aplica la responsable, guiada, desde phpMyAdmin.** Los agentes no reciben permiso de escritura en la base real.
- Nota práctica: la ventana de confirmación de phpMyAdmin no funciona en el navegador integrado de Claude; los cambios en la base se ejecutan desde Chrome/Edge.

## D-17 — Categorías "Carros" y "Camiones", y significado de "modelo" · TOMADA (2026-10-26)
- Contexto: HU-005/HU-006 piden las categorías Carros y Camiones, y HU-008 un filtro por "modelo". El esquema no tiene una columna de categoría. Tiene `tipos_vehiculo` (tipo), `tipos_carroceria` (subtipo) y `lineas` (modelo de cada marca). Hoy las tres tablas están vacías.
- Propuesta del coordinador, ya usada en el contrato de P3:
  - `tipos_vehiculo` contiene solo **Carro** y **Camión**: son las dos categorías.
  - Los subtipos (sedán, SUV, volqueta, furgón, estacas…) van en `tipos_carroceria`.
  - **"Modelo"** es la **línea** (por ejemplo Chevrolet → NPR), y el **año** es aparte.
  - Además:
    - un "Reservado" se ve en el catálogo con su etiqueta (D-11);
    - las publicaciones de un asesor bloqueado o rechazado no se muestran.
- Opciones:
  - A) Aceptar la propuesta.
    - Impacto: no cambia el esquema y P3 queda listo.
  - B) Más categorías (por ejemplo Motos o Maquinaria), o "modelo" = año.
    - Impacto: se añaden filas al catálogo o se ajusta un filtro.
- Recomendación: A.
- Bloquea: nada.
- **Decisión: A** (respuesta de la responsable, transmitida por el coordinador principal): el DEMO se quita (BAJAR) cuando P4b esté fusionado y antes de cargar el primer vehículo real. Si se elige B, el cambio es pequeño.
- **Decisión: B modificada** (respuesta de la responsable, transmitida por el coordinador principal). Hay **cuatro categorías principales: Carros, Camiones, Carrocerías y Maquinaria**.
  - Motivo: si el camión es nuevo, la carrocería se vende aparte, o se puede vender una carrocería usada. Por eso es también un artículo a la venta.
  - (a) "Carrocerías" es una categoría de producto: una carrocería sola se publica, se filtra y se muestra como cualquier otro artículo.
  - (b) La carrocería **sigue siendo** una característica (subtipo) de los camiones: `tipos_carroceria` se mantiene.
  - (c) "Maquinaria" es una categoría nueva (equipo pesado).
  - (d) "Modelo" es la línea, y el año va aparte.
  - (e) Se confirman la etiqueta "Reservado" y que las publicaciones de asesores bloqueados o rechazados no se muestran.
- Implementación en P3: cuatro filas en `tipos_vehiculo` (Carro, Camión, Carrocería, Maquinaria). No cambia el esquema.
  - Una carrocería suelta usa marca (fabricante), línea, año, tipo de carrocería y kilometraje 0. El kilometraje no se muestra para esta categoría.
  - Los datos propios de carrocerías y máquinas se preguntan en D-19.

## D-18 — Datos de demostración en `camion_ventas` para la prueba manual de P3 · TOMADA (2026-10-26)
- Contexto: el catálogo está vacío porque la creación de vehículos llega en P4. Sin datos, la prueba manual de P3 solo mostraría "Pronto tendremos vehículos publicados".
- El script `database/demo/demo_p3_catalogo.sql`:
  - Inserta unas 10 publicaciones de ejemplo de un asesor DEMO que no puede iniciar sesión, más sus catálogos.
  - Solo hace INSERT. Está marcado como DEMO.
  - Tiene la sección BAJAR, que borra únicamente las filas DEMO.
- Opciones:
  - A) Aplicarlo desde phpMyAdmin, con un respaldo previo y el mismo procedimiento que la 003 (D-16), y quitarlo con BAJAR antes de pasar a datos reales.
    - Impacto: permite probar el portal completo.
  - B) No aplicarlo y probar P3 solo con las pruebas automáticas.
    - Impacto: la prueba manual queda limitada al catálogo vacío.
- Recomendación: A.
- Bloquea: solo la prueba manual completa de P3.
- **Decisión: A** (respuesta de la responsable, transmitida por el coordinador principal). El script DEMO está autorizado e incluye ejemplos de las cuatro categorías. Lo aplicará la responsable desde phpMyAdmin en Chrome, guiada, con respaldo previo y conteos. Los agentes no lo aplican.

## D-19 — ¿Qué datos debe tener una carrocería y una máquina a la venta? · TOMADA (2026-10-26)
- Contexto: por D-17, Carrocerías y Maquinaria son categorías de venta.
  - Hoy el esquema solo tiene los campos de un vehículo: marca, línea, año, kilometraje, carrocería, combustible, carga, peso, cilindraje y ciudad.
  - Una carrocería no tiene placa ni kilometraje. Quizá necesite medidas (largo, ancho, alto), material (aluminio, acero, madera) o el tipo de camión al que se adapta.
  - Una máquina suele medirse en **horas de uso** (horómetro) en lugar de kilómetros.
  - No se inventan estos datos: se preguntan.
- Pregunta para la responsable:
  - ¿Qué datos debe mostrar y filtrar una carrocería? Por ejemplo: medidas, material, capacidad, para qué camión sirve.
  - ¿Y una máquina? Por ejemplo: horas de uso, tipo (retroexcavadora, montacargas…), capacidad.
- Opciones:
  - A) Decidir esos campos ahora. Se preparan en la migración de P4 (junto con la placa, D-2), reversible y aplicada por la responsable (D-16).
  - B) Empezar solo con los campos actuales y la descripción libre, y añadir los campos propios más adelante.
- Recomendación: A, para P4. P3 funciona con los campos actuales: muestra lo que exista y oculta el kilometraje de las carrocerías.
- Bloquea: el formulario de alta de carrocerías y máquinas (P4). No bloquea P3.
- **Decisión: A** (respuesta de la responsable, transmitida por el coordinador principal). Los campos se definen ahora y se preparan en la migración de P4.
  - **Carrocería** (publicación suelta):
    - marca **opcional**;
    - medidas: largo, ancho y alto;
    - material: aluminio, acero, madera, fibra…;
    - tipo de carrocería: furgón, estacas, volco, platón, tanque, refrigerada…;
    - sin placa ni kilometraje;
    - no se incluye "para qué camión sirve".
  - **Máquina:**
    - horas de uso (horómetro), en lugar del kilometraje;
    - tipo de máquina: retroexcavadora, montacargas, minicargador, excavadora…;
    - capacidad;
    - marca, modelo y año.
  - Los valores de las listas (materiales, tipos de máquina y tipos de carrocería) son PROPUESTOS y los podrá editar el administrador (parametrización, HU-081 a HU-087). Se confirman en los criterios de P4.
- Efecto en el esquema (para la migración de P4, junto con la placa D-2):
  - `vehiculos.marca_id` hoy es obligatorio y deberá admitir nulos para las carrocerías.
  - Hacen falta columnas nuevas para las medidas, el material, las horas de uso, el tipo de máquina y la capacidad.
  - Hacen falta catálogos nuevos para los materiales y los tipos de máquina.
- P3 no puede mostrar esos campos porque todavía no existen en el esquema. Lo único que ya hace P3 es ocultar el kilometraje de las carrocerías (y el de las máquinas con 0 km). El resto queda para P4.

## D-20 — ¿Dónde y cómo se guardan las fotos? · TOMADA (2026-10-27)
- Contexto: HU-014 y RN-VE-001 permiten hasta 10 fotos por vehículo. Las fotos no pueden ir al repositorio ni a la base de datos.
- Propuesta (ya usada en el contrato de P4a, `docs/criterios/P4a-contrato-api.md`):
  - Carpeta local **fuera del repositorio**, `C:\CAMION_media`, configurable con `Media:Ruta`.
  - Formatos JPG, PNG y WebP, validados por su contenido. Máximo 5 MB por foto y 10 fotos por vehículo.
  - Nombres aleatorios.
  - Las fotos se sirven en `/media/...`.
- Opciones:
  - A) Aceptar la propuesta.
    - Impacto: sin costo.
    - Hay que incluir esa carpeta en los respaldos; propuesta: copiarla junto a `C:\Respaldo_CAMION\`.
  - B) Otro límite (por ejemplo 2 MB, u otro número de fotos) u otra carpeta.
    - Impacto: es un ajuste de configuración.
  - C) Almacenamiento en la nube (por ejemplo, un servicio de archivos).
    - Impacto: costo e integración externa; se haría más adelante.
- Recomendación: A.
- Bloquea: la subida de fotos con la base y la carpeta reales (no bloquea las pruebas automáticas).
- **Decisión: A** (respuesta de la responsable, transmitida por el coordinador principal): las fotos se guardan en `C:\CAMION_media`, en JPG, PNG o WebP, con un máximo de 5 MB cada una y 10 por vehículo.

## D-21 — ¿Cómo obtiene un asesor un plan activo antes de P7? · TOMADA (2026-10-27)
- Contexto: publicar exige un plan activo con cupo (RN-CO-001), y la gestión de planes y pagos llega en P7.
- Opciones:
  - A) En P4b, el administrador asigna o renueva a mano un plan a un asesor, desde el panel y con auditoría. Los planes se crean con un script inicial que aplica la responsable.
  - B) Adelantar a P4b una gestión mínima de planes (crear un plan con nombre, precio, duración y cupo).
  - C) Un plan de prueba automático para todo asesor aprobado.
- Recomendación: A.
- Bloquea: P4b. **No bloquea P4a**, porque registrar vehículos en el inventario no exige plan.
- **Decisión: B, "Esperar a P7"** (respuesta de la responsable, transmitida por el coordinador principal).
  - No hay asignación manual de planes por el administrador, así que nadie puede publicar hasta que existan los planes.
  - En P4b, crear o renovar una publicación exige un plan activo. Si no lo hay, falla con un mensaje claro.
  - Queda pendiente decidir si se adelanta la parte de planes de P7 (D-26).

## D-22 — ¿"Marcar como vendido" (HU-064) entra ya o espera a P7? · TOMADA (2026-10-27)
- Contexto: la venta registra la fecha, el precio final, el origen y observaciones, y se puede revertir con un motivo (§15). Es gestión comercial.
- Opciones:
  - A) Esperar a P7. En P4a el estado VENDIDO existe en la base, pero no hay botón para marcarlo.
  - B) Incluirlo en P4b, junto con la publicación (vender oculta la publicación, RN-VE-014).
- Recomendación: B. Cerrar el ciclo del vehículo junto con sus publicaciones evita estados a medias.
- Bloquea: nada de P4a.
- **Decisión: "Marcar como vendido" entra en P4b** (respuesta de la responsable, transmitida por el coordinador principal).
  - Nota: el mensaje decía "A", pero describía la opción B (en P4b). Se registra lo que dice el texto. Si la responsable quería esperar a P7, que lo confirme.

## D-23 — Aplicar la migración 004 de P4a a `camion_ventas` · TOMADA (2026-10-27)
- Contexto: P4a necesita cambiar el esquema; el detalle está en el contrato.
  - En `vehiculos`: placa única opcional, estado, precio y precio anterior, campos de carrocería y máquina; la marca y el año pasan a ser opcionales.
  - Tablas nuevas: materiales, tipos de máquina y fotos del vehículo.
- La migración `database/migraciones/004_p4a_inventario.sql`:
  - es reversible (sección BAJAR) y no borra datos;
  - copia a cada vehículo DEMO el precio de su publicación.
- Procedimiento, igual que la 003 (D-16): el coordinador hace el respaldo completo previo; la responsable la aplica desde phpMyAdmin en Chrome, con los conteos antes y después.
- Opciones:
  - A) Aplicarla cuando P4a tenga sus pruebas en verde.
  - B) Esperar y aplicarla junto con la de P4b.
- Recomendación: A, para poder hacer la prueba manual de P4a.
- Bloquea: la prueba manual de P4a con la base real. Sin la migración, la API nueva fallaría contra `camion_ventas`.
- **Decisión: A** (respuesta de la responsable, transmitida por el coordinador principal).
  - La migración 004 se aplica cuando pasen las pruebas de P4a.
  - El coordinador principal hace el respaldo previo y la responsable la aplica desde phpMyAdmin en Chrome.
  - Los agentes no la aplican.

## D-24 — ¿Cuándo se retiran los datos DEMO? · TOMADA (2026-10-27)
- Contexto: hay 12 publicaciones DEMO de un asesor que no puede iniciar sesión, y su vigencia termina 30 días después de la carga. La sección BAJAR del script solo borra filas DEMO.
- Opciones:
  - A) Retirarlas cuando P4b esté fusionado y antes de cargar el primer vehículo real.
  - B) Retirarlas ya, antes de probar P4a.
  - C) Dejarlas hasta que venzan solas. No se recomienda: quedarían filas de prueba para siempre.
- Recomendación: A. Sirven para probar P4a y P4b, y el inventario de un asesor real no las ve, porque son de otro asesor.
- Bloquea: nada.

## D-25 — Detalles del formulario de inventario · TOMADA (2026-10-27)
Contexto: al escribir los criterios de P4a salieron tres dudas de negocio (C-3, C-5 y C-6 de `docs/criterios/P4a.md`). Ninguna bloquea P4a. Hoy funciona con la opción recomendada en cada una.

1. **¿El modelo (la línea) es obligatorio?**
   - Contexto: RN-VE-006 y D-19 piden registrar el modelo, pero la lista de líneas está casi vacía y el administrador no podrá añadir líneas hasta P7.
   - A) Opcional hasta P7 y obligatorio para Carro, Camión y Maquinaria cuando el administrador ya pueda añadir líneas. **Recomendado.**
   - B) Obligatorio desde ya. Riesgo: no se podría registrar un vehículo cuyo modelo no esté en la lista.
2. **Placa de maquinaria.** Hoy se admiten placas de 5 a 7 letras y números. Hay que confirmar si las placas de maquinaria pueden ser más largas, porque en ese caso se amplía a 8 caracteres.
3. **¿En qué unidad se mide la capacidad?**
   - Hoy la capacidad de camiones y máquinas se guarda en kg.
   - ¿Las máquinas necesitan otra unidad, como toneladas o m³ de balde?
   - ¿Las carrocerías necesitan capacidad? D-19 no la incluía, y hoy es opcional.

- Recomendación: 1A; en 2, ampliar a 8 caracteres solo si hace falta; en 3, mantener kg y la capacidad opcional en las carrocerías.
- **Decisión** (respuesta de la responsable, transmitida por el coordinador principal):
  1. **El modelo (línea) es OBLIGATORIO desde ya** en Carro, Camión y Maquinaria. En Carrocería sigue opcional. Se eligió la opción B, no la recomendada. Cómo resolver que la lista de líneas está casi vacía queda en D-27.
  2. La placa de maquinaria admite **hasta 8 caracteres**; las demás, de 5 a 7.
  3. La capacidad se mide en **kg** en camiones y máquinas, y es opcional en las carrocerías.
- Implementado en P4a, con las pruebas actualizadas.

## D-26 — ¿Se adelanta la parte de planes y suscripciones de P7? · TOMADA (2026-10-27)
- Contexto: por D-21 (B), nadie puede publicar sin un plan activo, y los planes llegan en P7. Si no se adelantan, P4b (publicaciones) se podrá probar con pruebas automáticas, pero **no se podrá usar con datos reales hasta P7**.
- Alcance mínimo que habría que adelantar:
  - HU-049 a HU-051: planes. El administrador crea y edita planes con nombre, precio, duración y cupo de publicaciones.
  - HU-066 a HU-069: suscripciones. El asesor consulta su suscripción; el administrador la activa o renueva, la suspende y la reactiva, con auditoría.
  - Pagos y comprobantes no entran: el administrador activa la suscripción a mano cuando confirma el pago.
- Opciones:
  - A) Hacer un ítem **P4p "Planes y suscripciones"** justo después de P4a y antes de P4b.
    - Impacto: P4b sale ya utilizable. Añade una entrega de tamaño mediano.
  - B) Incluir planes y suscripciones **dentro de P4b**.
    - Impacto: un PR más grande y más difícil de revisar.
  - C) Mantener el orden actual.
    - Impacto: P4b queda probada pero sin uso real hasta P7.
- Recomendación: A. Separado es más fácil de revisar, y el orden P4a → P4p → P4b deja cada entrega usable.
- Bloquea: el uso real de P4b. No bloquea P4a.
- El backlog no se reordena hasta tener la respuesta.
- **Decisión: A** (respuesta de la responsable, transmitida por el coordinador principal). Se crea **P4p "Planes y suscripciones"** justo después de P4a y antes de P4b, con el alcance mínimo propuesto: sin pagos en línea; el administrador activa la suscripción a mano. El backlog queda reordenado.

## D-27 — ¿Cómo se completa la lista de marcas y modelos ahora que el modelo es obligatorio? · TOMADA (2026-10-27)
- Contexto: por D-25, el modelo es obligatorio en Carro, Camión y Maquinaria.
  - En `camion_ventas` solo hay las marcas y líneas del DEMO.
  - El administrador no puede añadirlas hasta P7 (HU-082/HU-083).
  - Mientras tanto, un asesor **no puede registrar** un vehículo cuyo modelo no esté en la lista.
- Opciones:
  - A) **Pantalla mínima de administración de marcas y modelos** (crear, renombrar y desactivar, con auditoría), en P4p o en un ítem pequeño antes de P4b.
    - Impacto: es la solución duradera, con algo más de trabajo.
  - B) **Catálogo inicial con un script de solo INSERT**: marcas y modelos habituales de camiones, carros y maquinaria en Colombia, revisado por la responsable y aplicado por ella en phpMyAdmin (D-16).
    - Impacto: rápido, pero cada modelo nuevo exige otro script hasta que exista la pantalla.
  - C) **Ambas**: el script para arrancar y la pantalla para el día a día.
- Recomendación: C, con el script ahora y la pantalla dentro de P4p.
- Bloquea: el uso real de P4a con vehículos de modelos que no estén en la lista. No bloquea las pruebas ni el merge.
- **Decisión: C, "Lista inicial + pantalla"** (respuesta de la responsable, transmitida por el coordinador principal).
  - El script `database/catalogos/005_marcas_lineas_iniciales.sql` añade 33 marcas y 137 modelos de camiones, carros y maquinaria comunes en Colombia.
    - Solo usa INSERT con NOT EXISTS, así que se puede ejecutar dos veces sin duplicar.
    - Su sección BAJAR, comentada, solo borra lo que añadió y que ningún vehículo usa. No toca lo del DEMO.
    - Lo aplica la responsable vía phpMyAdmin junto con la migración 004, tras el respaldo previo.
  - La pantalla de marcas y modelos (HU-082/HU-083: crear, renombrar, desactivar) entra en P4p.
- Nota: D-22 queda confirmada por la responsable: "Marcar como vendido" va en P4b.

## D-28 — Proceso de reserva del vehículo · TOMADA (2026-10-27)
- Contexto: aclara lo PROPUESTO de D-11. La responsable lo pidió durante la prueba manual de P4a, transmitido por el coordinador principal.
- **Decisión:**
  - Reservan y liberan el **asesor dueño** y el **administrador**.
  - Al reservar se pide una **nota obligatoria** de 5 a 500 caracteres, por ejemplo "pagó anticipo" o "esperando crédito". No se piden datos del cliente.
  - La nota se guarda en el vehículo, se ve en el inventario y en el detalle del asesor y del administrador (**no** en el portal público) y queda en la auditoría.
  - La reserva **no vence**: dura hasta que alguien la libere o el vehículo se venda (P4b). Al liberar o inactivar, la nota se borra; en la auditoría sigue.
- Implementación: columna `vehiculos.nota_reserva`, añadida por la migración 006 (`database/migraciones/006_p4a_nota_reserva*.sql`), que está sin aplicar.

## D-29 — Catálogos de datos reales · TOMADA (2026-10-27)
- Contexto: la responsable lo pidió durante la prueba manual de P4a, transmitido por el coordinador principal.
- **Decisión:**
  1. Combustibles: Gasolina, Gas (GNV), Eléctrico e Híbrido, además de Diésel.
  2. Departamentos y municipios completos según la **DIVIPOLA del DANE**, con el código DANE.
     - Se genera un script **una vez** desde el conjunto oficial publicado en datos.gov.co, indicando la fuente y las fechas, y se documenta cómo actualizarlo (`docs/GUIA-CATALOGOS.md`).
     - No hay consulta en vivo al DANE: la app no depende de que su servicio esté en línea.
     - Datos actualizados por el DANE el 2025-01-24 y descargados el 2026-10-27: 33 entidades (32 departamentos más Bogotá, D.C.; no 33 más Bogotá) y 1.122 municipios y áreas.
  3. Modelos recientes del mercado colombiano por marca, verificados en fuentes y marcados como PROPUESTO para que la responsable los revise antes de aplicarlos.
- Scripts:
  - `database/catalogos/007_catalogos_reales_*` (combustibles y DIVIPOLA);
  - `database/catalogos/008_modelos_recientes_*` (modelos).
  - Solo hacen INSERT con NOT EXISTS, y su BAJAR (comentado) está acotado.

## D-30 — Un vehículo debe tener al menos una foto · TOMADA (2026-10-28)
- Contexto: pedido de la responsable en la prueba manual de P4a, transmitido por el coordinador principal: "debería pedir al menos una foto para guardar".
- **Decisión:** un vehículo no puede quedar guardado sin al menos **1 foto**.
- Implementación elegida por el coordinador, la más robusta:
  - **Creación atómica.** `POST /api/inventario/vehiculos` recibe los datos y de 1 a 10 fotos en una sola petición (multipart). Se valida todo antes de escribir, y el vehículo, las fotos y la auditoría se guardan juntos. Si algo falla, no queda nada a medias: ni el vehículo ni archivos sueltos. Así no hace falta un estado "incompleto" nuevo, y la regla vale para cualquier cliente de la API.
  - **No se puede eliminar la última foto** (409).
  - Los vehículos que ya existen sin fotos (DEMO y de prueba) siguen funcionando. Al editarlos se pide subir una foto antes de "Guardar cambios" (409 si no la tienen).
  - El catálogo público no cambia.
- Detalle en `docs/criterios/P4a-contrato-api.md`, sección "Cambios por D-30".

## D-31 — Marcar siempre con asterisco (*) los campos obligatorios · TOMADA (2026-10-28)
- Contexto: pedido de la responsable en la prueba manual de P4a, transmitido por el coordinador principal: que los asesores no tengan que adivinar qué es obligatorio.
- **Decisión:** todo campo obligatorio lleva un asterisco (*), y cada formulario empieza con la nota "* Campo obligatorio".
  - El asterisco depende de la categoría. Por ejemplo, la placa no lo lleva porque es opcional, y el modelo sí en Carro, Camión y Maquinaria.
  - Se aplica en el inventario, en el registro de comprador y asesor, en el inicio de sesión y en los motivos de reserva e inactivación del panel.
- Implementación:
  - El asterisco se dibuja con CSS (clase `campo-obligatorio`).
  - Los campos obligatorios llevan `aria-required="true"`. No se usa el atributo `required`, para que la validación sea la propia del formulario y no la del navegador.

## D-32 — ¿Cómo se limita la lista de modelos a la categoría elegida? · TOMADA (2026-10-28)
- Contexto:
  - Hoy cada modelo (`lineas`) pertenece a una marca, pero no a una categoría. Por eso una máquina se pudo registrar como "Isuzu ELF 100", que es un camión (vehículo 17).
  - Algunas marcas venden varias categorías: Chevrolet carros y camiones; Hyundai carros, camiones y maquinaria; Volvo camiones y maquinaria; Toyota carros y montacargas.
- Opciones:
  - A) **Cada modelo indica a qué categorías pertenece.**
    - Una tabla nueva `lineas_tipos (linea_id, tipo_vehiculo_id)`, que permite que un modelo esté en varias categorías.
    - Una migración reversible y un script que clasifica los modelos actuales (005 y 008), aplicados por la responsable vía phpMyAdmin (D-16).
    - El formulario y la API solo ofrecen y aceptan los modelos de la categoría elegida.
    - Impacto: es la solución precisa. Hay que revisar la clasificación una vez.
  - B) **Cada modelo tiene una sola categoría** (columna `lineas.tipo_vehiculo_id`).
    - Impacto: es más simple, pero un mismo nombre en dos categorías tendría que repetirse.
  - C) **Filtrar por marca y categoría** (tabla `marcas_tipos`).
    - Impacto: no resuelve el caso de Chevrolet, que tiene carros y camiones.
- Recomendación: A. El administrador podrá editar la clasificación cuando exista la pantalla de marcas y modelos (P4p).
- Bloquea: nada de P4a. Los vehículos mal clasificados, como el 17, se corrigen editándolos.
- No se implementa sin respuesta.
- **Decisión: A, "cada modelo con sus categorías"** (respuesta de la responsable, transmitida por el coordinador principal). Se incluye en P4a.
  - Migración `database/migraciones/009_p4a_lineas_tipos*.sql`: tabla `lineas_tipos`.
  - Script `database/catalogos/010_clasificacion_lineas*.sql`: clasifica las 393 líneas actuales. La lista para revisar, con 56 dudosas marcadas PROPUESTO, está en `docs/catalogos-010-clasificacion.md`.
  - El formulario solo ofrece los modelos de la categoría elegida.
  - La API rechaza un modelo clasificado en otra categoría. Uno sin clasificar se acepta, para que no se bloquee nada mientras no esté clasificado.
  - La pantalla de marcas y modelos de P4p permitirá editar las categorías de cada modelo.

## D-33 — El dueño puede cambiar la categoría de su vehículo · TOMADA (2026-10-28)
- Contexto: en la prueba final de P4a, un vehículo con un modelo de otra categoría ("Kia Picanto 2008" como Camión) quedaba sin salida. La categoría decía "(no se puede cambiar)" y el selector de modelo no ofrecía ninguno.
- Palabras de la responsable: "recordemos que cada usuario debe poder hacer cambios sobre sus publicaciones". Opción elegida: "Asesor también la corrige".
- **Decisión:** el asesor dueño y el administrador pueden cambiar la **categoría** de un vehículo mientras no tenga una publicación activa. Al cambiarla:
  - el formulario muestra los campos de la nueva categoría;
  - se vacían el modelo y los campos que ya no aplican;
  - se valida todo con las reglas de la nueva categoría (placa, modelo de esa categoría según D-32, foto…);
  - la auditoría EDITAR registra la categoría anterior y la nueva.
- Con una publicación activa, la API responde 409 con un mensaje claro. P4b definirá qué pasa en ese caso.
- Si la marca no tiene modelos de esa categoría, el mensaje lo explica: "Kia no tiene modelos de Camión. Elige otra marca o cambia la categoría."
- Regla general recalcada por la responsable: **el dueño puede modificar todo lo suyo**, salvo lo que una regla de negocio impida expresamente.

## D-34 — ¿El administrador puede cambiar el asesor dueño de un vehículo? · TOMADA (2026-10-29)
- Contexto: hoy el asesor propietario se elige al crear el vehículo (solo el administrador) y no se puede cambiar después. Ninguna regla escrita lo prohíbe, pero tampoco hay HU que lo pida. D-7 habla de reasignar solicitudes, no vehículos.
- Opciones:
  - A) El administrador puede reasignar el vehículo a otro asesor activo, con auditoría (quién, cuándo, de quién a quién).
  - B) Se mantiene fijo, y para cambiarlo se crea un vehículo nuevo.
- Recomendación: A, coherente con "el dueño puede modificar todo lo suyo" y con la reasignación de D-7. Mejor junto con P4b, porque afecta a las publicaciones del vehículo.
- Bloquea: nada de P4a.
- **Decisión: A** (respuesta de la responsable, transmitida por el coordinador principal). **Solo el administrador** puede reasignar un vehículo de un asesor a otro asesor activo, con **motivo obligatorio** y auditoría (quién, cuándo, de quién a quién). Se construye **junto con P4b**, no en P4a.

## D-35 — Duración de la suscripción · TOMADA (2026-10-29)
- Contexto: la base guarda la duración del plan en meses (`planes.duracion_meses`) y RN-SU-005 dice "30 días, renovable".
- **Decisión de la responsable (en esta sesión): 30 días por cada mes del plan.** Un plan de 1 mes da 30 días y uno de 3 meses, 90 días.

## D-36 — ¿Quién renueva una suscripción? · TOMADA (2026-10-29)
- **Decisión de la responsable (en esta sesión): solo el administrador**, cuando confirma el pago. El asesor consulta su suscripción y ve cuándo vence.

## D-37 — Autonomía del administrador sobre los catálogos · TOMADA (2026-10-29)
- **Decisión de la responsable (en esta sesión), en sus palabras:** "darle autonomía de catálogo al administrador completa para modificar".
- El administrador puede crear, renombrar y activar o desactivar:
  - marcas;
  - modelos, incluidas sus categorías (D-32);
  - tipos de carrocería, materiales, tipos de máquina y combustibles.
- No se borra nada: desactivar oculta el elemento de los formularios y los vehículos que lo usan lo conservan.
- Se dejan fuera, por seguridad, y quedan PROPUESTO para confirmar:
  - Las **4 categorías** (Carro, Camión, Carrocería, Maquinaria). Las reglas del formulario dependen de sus nombres, y cambiarlas rompería las validaciones.
  - Los **departamentos y municipios**. Son la DIVIPOLA oficial del DANE y se actualizan con un script (`docs/GUIA-CATALOGOS.md`).

## D-38 — Destacados según el plan, los gestiona el asesor · TOMADA (2026-10-29)
- **Decisión de la responsable (en esta sesión), en sus palabras:** "el usuario compra el plan y tiene derecho, por ejemplo, a 1 destacado… quiere bajar ese destacado y poner otro, a criterio del asesor; la regla es que no puede tener más de 1, pero que no limite que cambie el destacado (que dependa del plan)".
- Cada plan define cuántos destacados incluye (`limite_destacados`).
- El **asesor dueño**, o el administrador, marca o quita destacados en sus publicaciones vigentes, sin pasar nunca del límite a la vez y sin límite de cambios.
- **Modifica HU-027 / RN-CO-004**, que decían que destacaba el administrador.

## D-39 — Origen de la venta · TOMADA (2026-10-29)
- **Decisión de la responsable (en esta sesión): lista fija.** Portal web, Contacto directo del asesor, Referido y Otro (con observaciones obligatorias). Sirve para los reportes de P8.

## D-40 — Qué cuenta para el cupo del plan · TOMADA (2026-10-29)
- **Decisión de la responsable (en esta sesión), en sus palabras:** "tiene derecho a la cantidad de publicaciones; si son 5, solo 5 en cualquier estado que estén en el sistema; si lo elimina libera cupo, pero si está en plataforma ocupa cupo".
- Cuentan las publicaciones en cualquier estado salvo ELIMINADA.
- Se añade la acción **Eliminar publicación**. Es un borrado lógico: la fila no se borra y libera cupo. Solo se puede eliminar una publicación inactiva o vendida.

## D-41 — Vigencia de la publicación = vigencia del plan · TOMADA (2026-10-29)
- **Decisión de la responsable (en esta sesión), en sus palabras:** "al activar una publicación y si es un plan nuevo contará con los días del plan; si tiene un plan activo y quiere un nuevo plan, le cuenta el final del plan más el plan que está solicitando y que ya esté aprobado por el administrador".
- Una publicación está vigente mientras lo esté el plan de su asesor.
- Un plan nuevo aprobado por el administrador mientras otro sigue vigente se suma al final del actual. Esto es la renovación de P4p, con el plan nuevo.
- El "vencimiento automático" (HU-028) ocurre cuando vence el plan.
- Cambia la regla de visibilidad del portal: las condiciones 3 y 6 de P3 pasan a ser "el asesor tiene un plan vigente". El detalle está en `docs/criterios/P4b-contrato-api.md`.

## D-42 — Rediseño del portal (rojo, negro y blanco) · PARCIAL (2026-11-04)
- **Decisión de la responsable (en esta sesión):** el portal sigue la referencia visual que entregó: encabezado blanco, portada con camión y buscador, cuatro categorías (Camiones resaltada en rojo), destacados con borde y sello rojo, franja de confianza, banda de crédito y pie oscuro. Colores corporativos: rojo, negro y blanco. "No quiero que se vea tan recargado".
- **Pendiente de confirmar (no se inventa nada):**
  - Franja de servicios: **CONFIRMADA (2026-11-04)**. La franja de confianza se reemplaza por "Nuestros servicios": créditos vehiculares, venta de camiones nuevos, venta de camiones usados peritados, venta de carrocerías, venta de maquinaria amarilla y seguros todo riesgo. Es informativa (sin enlaces) y vive en `frontend/src/components/Inicio.tsx` (constante `SERVICIOS`). La palabra "peritados" la confirmó la responsable el 2026-11-04. El alquiler no se muestra (ver D-43).
  - Contacto del pie: **CONFIRMADO (2026-11-04)** por la responsable: WhatsApp 3166152907 (enlace `https://wa.me/573166152907`) y correo vc.automotores@gmail.com. Horario **CONFIRMADO (2026-11-04)**: lunes a viernes de 9:00 a. m. a 5:00 p. m. y sábados de 9:00 a. m. a 3:00 p. m. Dirección: por ahora no se publica (decisión de la responsable). Instagram y TikTok: sí se publicarán, pero las cuentas aún no existen y no se muestran hasta tener los enlaces. Páginas legales: pendientes.
  - El botón "Quiero un crédito" lleva al registro mientras no exista la solicitud de crédito (llega con P5).
  - La portada usa una ilustración propia en SVG mientras el administrador no suba una foto.
- **Decisión de la responsable (2026-11-04): "Que lo cambie el admin".** La foto de fondo de la portada la administra el administrador desde "Mi cuenta" › Portada:
  - subir una foto JPG, PNG o WebP de hasta 5 MB (validada por su contenido) o quitarla para volver a la ilustración;
  - sin cambios de esquema: el archivo vive en `Media:Ruta/portal/` con nombre aleatorio; la portada vigente es la más reciente y la anterior se borra al reemplazarla;
  - se audita en `auditoria_eventos` (entidad `Portal`, acciones `CAMBIAR_PORTADA` y `QUITAR_PORTADA`);
  - rutas: `GET /api/catalogo/portada` (anónima), `POST /api/admin/portal/portada` y `POST /api/admin/portal/portada/quitar` (SoloAdministrador). Quitar es POST porque el proyecto solo admite DELETE en las fotos del inventario.

## D-43 — Alquiler de vehículos como servicio nuevo · EN PAUSA (2026-11-04)
- **Pedido de la responsable:** "alquiler (esto me gustaria analizarlo como una nueva propuesta, pero necesito que lo revisemos por temas de contratos y legalidad como seria el proceso)".
- **Estado:** no se implementa ni se anuncia en el portal hasta que se definan el modelo de negocio y la revisión legal. No está en las HU v3.0.
- **2026-11-04, la responsable:** "dejemos por el momento el tema del alquiler, toca hacer buenos cuestionamientos sobre este proyecto adicional de cómo se integraría". Se pausa y se sigue con el backlog vigente (P5 y siguientes).
- **Preguntas de negocio para definir el proceso:**
  1. ¿De quién son los vehículos que se alquilan: de la empresa, de asesores o de clientes? ¿La plataforma solo intermedia?
  2. ¿Con conductor o sin conductor? ¿Por días, meses o largo plazo (renting/leasing operativo)?
  3. ¿Qué tipos: camiones, maquinaria amarilla, carros?
  4. ¿Quién atiende la solicitud? Por la regla vigente, todo servicio lo atiende el administrador (D-7).
  5. Requisitos del arrendatario: documentos, licencia, estudio de crédito, depósito o garantía.
  6. Entrega y devolución: acta de inventario y estado del vehículo con fotos, kilometraje u horómetro, combustible.
  7. Seguros, mantenimiento, multas y siniestros: ¿quién los asume?
- **Revisión legal (la hace un abogado; el agente no da concepto jurídico):** tipo de contrato; si el alquiler de vehículos de carga o con conductor exige habilitación ante el Ministerio de Transporte o la Superintendencia de Transporte; pólizas exigibles (SOAT, todo riesgo, responsabilidad civil); tratamiento de datos personales (Ley 1581 de 2012); facturación e impuestos.
- **Propuesta de alcance mínimo, cuando se apruebe:** un aviso "Alquila un vehículo" igual a los de D-6 (sin cuenta, dirigido al administrador). El contrato y la entrega se manejan fuera del portal hasta tener el modelo legal definido.

## D-44 — Política de datos y términos (Ley 1581 de 2012) · PROPUESTO (2026-11-04)
- **Pedido de la responsable:** preparar las páginas de términos y de privacidad antes de seguir con P5.
- **Hecho:** borradores en `docs/legal/POLITICA-TRATAMIENTO-DATOS.md` y `docs/legal/TERMINOS-Y-CONDICIONES.md`, basados en los datos que el portal recoge hoy. Están marcados como BORRADOR, con campos `[POR COMPLETAR]` y notas para el abogado. **No se publican en el portal hasta la revisión legal.**
- **Datos que debe entregar la responsable:** razón social, NIT, dirección de notificaciones, dominio, proveedor de alojamiento y fecha de vigencia.
- **Preguntas para el abogado (marcadas en los borradores):**
  - figura legal para intermediar seguros (agente, agencia, corredor o convenio);
  - requisitos para ser bróker de crédito y revelar comisiones;
  - obligaciones del intermediario frente al comprador y modelo de contrato de consignación;
  - alcance del peritaje;
  - si las bases de datos deben inscribirse en el RNBD;
  - transferencia de datos a Gmail y WhatsApp;
  - compartir los datos de contacto con el asesor.
- **Pendiente técnico (antes de salir a internet; afecta P5, P5b y el registro):**
  - una casilla de autorización sin marcar, con enlace a la política, en el registro, la inscripción de asesor y todo formulario de contacto o aviso;
  - guardar la prueba de la autorización (fecha, versión de la política y formulario), lo que requiere una migración reversible;
  - publicar `#/privacidad` y `#/terminos` y enlazarlas desde el pie una vez aprobadas.

## D-45 — Modelo de negocio: intermediación · TOMADA (2026-11-04)
- **La responsable:** "somos intermediarios, básicamente los camiones son de terceros que ya deben estar en consignación cuando estén publicados o tener autorización de dueño, los créditos se gestionan ante bancos y entidades financieras, nosotros somos brókers, igual pasa con las pólizas".
- **Decisión:**
  - Financiamos Tu Camión no es dueño de los vehículos, no presta dinero y no expide pólizas.
  - Todo vehículo publicado debe estar en consignación o tener autorización de su dueño.
  - Los créditos se gestionan ante bancos y entidades financieras, y los seguros ante aseguradoras, como bróker.
- **Aplicado en:** los borradores de `docs/legal/` (D-44).
- **Impacto en el sistema. Decisión de la responsable (2026-11-04): no se implementa nada de esto.**
  - Al crear o publicar un vehículo **no** se pide declarar la consignación ni la autorización del dueño, ni adjuntar soporte. Ese control es interno del negocio.
  - La autorización para compartir datos con bancos, entidades financieras y aseguradoras **no** se pide en la plataforma. "Esto se maneja interno cuando se hace el proceso, en esta plataforma solo vamos hacer contacto asesor y usuario, lo otro es interno el manejo".
  - **Alcance de la plataforma:** poner en contacto al usuario con el asesor o con el administrador. La gestión del crédito, la póliza y la consignación ocurre fuera del portal.
  - **Por confirmar al planear P6:** si "Créditos y búsquedas" sigue con formularios, estados y seguimiento, o se reduce a una solicitud de contacto dirigida al administrador. P5 y P5b no cambian.

## D-46 — ¿Qué pasa con los leads abiertos de un asesor que pierde su plan, es bloqueado o deja de ser dueño del vehículo? · TOMADA (2026-11-04)
- Contexto (detectada al escribir los criterios de P5, `docs/criterios/criterios-P5-leads.md`, CA-P5-50 a CA-P5-53):
  - D-7 dice que el asesor atiende los contactos de sus vehículos publicados **mientras su plan esté vigente**; si no, el administrador. HU-032 y HU-033 dicen que el asesor gestiona y cierra el lead, sin límite de tiempo.
  - Por D-41, una publicación solo se ve en el portal si el plan del asesor está vigente. Por eso, al crear el lead casi siempre hay plan vigente. La duda real es **después**: un lead tarda días en cerrarse y en ese tiempo el plan puede vencer, el asesor puede ser bloqueado o el administrador puede reasignar el vehículo a otro asesor (D-34).
- Opciones:
  - A) **La asignación se fija al crear el lead.** El asesor conserva sus leads abiertos aunque venza su plan; el administrador los ve todos y los reasigna a mano si quiere (D-7). Solo pasan solos a Administración los de un asesor **bloqueado**, que ya no puede entrar. Si se reasigna el vehículo (D-34), sus leads abiertos lo siguen.
    - Impacto: es lo más simple; el asesor no pierde los clientes que llegaron mientras pagaba el plan.
  - B) **La asignación sigue al plan.** Al vencer el plan, o al bloquear al asesor, sus leads abiertos pasan solos a Administración (auditado como SISTEMA). No vuelven solos al renovar. Si se reasigna el vehículo, sus leads abiertos lo siguen.
    - Impacto: lectura literal de D-7; hace falta un proceso que revise vencimientos, y el asesor que se atrasa en el pago pierde sus clientes en curso.
  - C) Como B, pero con unos días de gracia (por ejemplo 5) antes de pasar los leads a Administración.
    - Impacto: más configuración y un caso más que probar.
- Recomendación del analista: **A**. Respeta D-7 en el origen del contacto, evita mover leads sin que nadie lo decida y deja la decisión final al administrador, que ve todos los leads.
- Bloquea: solo CA-P5-50 a CA-P5-53. El resto de P5 se puede construir.
- **Decisión de la responsable (2026-11-04): B. La asignación sigue al plan.** Pasan solos a Administración, auditados como SISTEMA, los leads abiertos de un asesor cuyo plan vence o que es bloqueado. No vuelven solos al renovar; el administrador puede reasignarlos a mano (D-7). Si se reasigna el vehículo (D-34), sus leads abiertos lo siguen.

## D-47 — Cómo contacta el comprador desde el detalle del vehículo · TOMADA (2026-11-04)
- Opciones presentadas:
  - A) solo formulario (recomendada);
  - B) formulario y botón de WhatsApp del asesor.
- **Decisión de la responsable: B.**
  - El detalle muestra el formulario de contacto, que crea el lead (P5), y un botón de WhatsApp que abre `wa.me` con el teléfono del asesor dueño de la publicación.
  - Si la publicación está a cargo del administrador, el botón usa el WhatsApp del portal (D-42).
- Consecuencias aceptadas:
  - las conversaciones por WhatsApp no quedan registradas en la plataforma;
  - el teléfono del asesor queda público mientras su publicación esté visible (solo con plan vigente, D-41).
- **Pendiente legal (D-44):** los términos y la política deben decir que el teléfono del asesor se publica junto a sus vehículos.

## D-48 — Aviso al asesor de un lead nuevo · TOMADA (2026-11-04)
- **Decisión de la responsable:**
  - por ahora el aviso es solo dentro del panel: "Contactos (N nuevos)";
  - el aviso por correo o WhatsApp queda para P8, porque no hay servicio de correo (D-5).
- RN-NO-002 queda cubierta parcialmente.

## D-49 — Detalles técnicos de P5 decididos por el coordinador · TOMADA (2026-11-04)
- Son detalles de implementación, no reglas de negocio, y se pueden revertir. Si la responsable quiere otro comportamiento, se cambia sin migración.
- Backend:
  - al reasignar un vehículo (D-34), solo siguen al nuevo dueño los leads abiertos del dueño anterior;
  - un lead reasignado a mano a un asesor sin plan queda marcado como asignación manual: el barrido por plan vencido no lo mueve; el de bloqueo sí;
  - un teléfono fijo (60…) usa el WhatsApp del portal, porque wa.me solo funciona con celulares;
  - "publicación a cargo del administrador" (D-47) significa que el dueño tiene rol ADMINISTRADOR;
  - un contacto repetido no vuelve a marcar el lead como nuevo y también cuenta para el límite por teléfono (B-1 de `REVISION-SEGURIDAD-P5.md`);
  - la carrera de duplicados simultáneos es un riesgo aceptado (B-2); el cierre "Contacto duplicado o inválido" cubre el caso;
  - el barrido de D-46 es bajo demanda, sin servicio en segundo plano.
- Frontend:
  - el enlace dice "Contactos (N nuevos)", sin número si es 0 y en singular con 1 ("Contactos (1 nuevo)");
  - tras el envío con sesión, el formulario vuelve a los datos del perfil;
  - solo el asesor envía su token al pedir el detalle del vehículo, para obtener `esPropio` (CA-P5-11);
  - `#/privacidad` muestra solo un aviso de borrador, sin el texto legal, hasta la revisión (D-44).

## D-50 — Datos de los formularios de los avisos (P5b) · TOMADA (2026-11-06)
- Contexto: D-6 fija los campos de HU-N01 y HU-N02, pero deja abiertos algunos detalles (detectados al escribir `docs/criterios/criterios-P5b-avisos.md`, CA-P5b-04, 08, 09, 15, 17).
- (a) **Lista de "tipo de vehículo" del seguro.** D-6 propone camión / carro / otro; el portal tiene cuatro categorías (D-17) y anuncia "venta de maquinaria amarilla" (D-42).
  - A) Camión / Carro / Otro (lo propuesto en D-6).
  - B) Camión / Carro / Maquinaria / Otro.
  - C) Las cuatro categorías del portal más Otro (incluye Carrocería).
  - D) Texto libre.
  - Recomendación: B. Cubre la maquinaria que se anuncia; una carrocería suelta rara vez se asegura aparte.
- (b) **¿"Vende tu camión" pide también ciudad, marca o año?**
  - A) No: solo nombre, teléfono WhatsApp y descripción (D-6); el cliente los escribe en la descripción.
  - B) Sí, como campos opcionales.
  - C) Sí, como obligatorios (modifica D-6).
  - Recomendación: A. Es lo confirmado y el formulario queda corto.
- (c) **Correo y tipo de teléfono.** D-9 dice "correo opcional en todo formulario sin cuenta"; D-6 no lista correo. El teléfono de HU-N01 es "WhatsApp", y la regla de P5 acepta también fijos (60…).
  - A) Correo opcional en ambos avisos; teléfono con la regla de P5 (celular o fijo).
  - B) Sin correo; teléfono con la regla de P5.
  - C) Sin correo; en "Vende tu camión" solo celulares.
  - Recomendación: A, por coherencia con D-9 y con el formulario de contacto de P5.
- Impacto: (a) fija el CHECK de `leads.tipo_vehiculo` en la migración 014; (b) y (c) solo cambian el formulario y la validación.
- **Decisión (responsable, 2026-11-06): las recomendaciones.** (a) B: Camión / Carro / Maquinaria / Otro. (b) A: sin ciudad, marca ni año. (c) A: correo opcional y teléfono con la regla de P5.

## D-51 — Dónde se ubican los avisos en el portal · TOMADA (2026-11-06)
- Contexto: D-6 dice "portal público" sin ubicación. D-42 dice que la franja "Nuestros servicios" es informativa (sin enlaces) y que el portal no debe verse recargado.
- Opciones:
  - A) Dos bloques en la portada, junto a la banda de crédito ("¿Quieres vender tu camión?" y "Asegura tu vehículo todo riesgo"), y una columna "Servicios" en el pie con ambos enlaces. Cada aviso en su pantalla (`#/vende-tu-camion`, `#/seguro-todo-riesgo`). Menú principal y franja sin cambios.
  - B) Como A, y además dos entradas en el menú principal.
  - C) Convertir en enlaces los servicios "Seguros todo riesgo" y "venta de camiones usados" de la franja (modifica D-42), más el pie.
  - D) Solo en el pie.
- Recomendación: A. Visible sin recargar el encabezado y sin cambiar D-42.
- **Decisión (responsable, 2026-11-06): A.** Dos bloques en la portada y la columna "Servicios" en el pie; menú y franja sin cambios.

## D-52 — ¿El administrador puede reasignar un aviso a un asesor? · TOMADA (2026-11-06)
- **Contradicción:** D-6 dice que "Vende tu camión" va "siempre al administrador, nunca a un asesor". D-7 (y `CLAUDE.md`) dicen que el administrador "puede reasignar manualmente cualquier solicitud a un asesor".
- Opciones:
  - A) Los avisos no se reasignan nunca: siempre en Administración; la API responde 409 y la base lo impide.
  - B) Se pueden reasignar como un lead de P5 (motivo y auditoría); el asesor los vería solo mientras los tenga asignados.
  - C) "Vende tu camión" no se reasigna (D-6 literal); el seguro sí.
- Recomendación: A. Es la lectura literal de D-6 y evita exponer datos de clientes a asesores; si el administrador necesita apoyo de un asesor, lo coordina fuera del sistema.
- Impacto: con A se añade `CK_Lead_AvisoAdmin` en la migración 014; con B o C los avisos entran en el barrido de D-46 y en la marca `asignacion_manual`.
- **Decisión (responsable, 2026-11-06): A.** Los avisos nunca se reasignan a un asesor (409 en la API y `CK_Lead_AvisoAdmin` en la base).

## D-53 — ¿El comprador con sesión ve sus avisos en "Mi cuenta"? · TOMADA (2026-11-06)
- Contexto: en P5 el comprador no tiene lista propia de contactos (CA-P5-32). D-3 dice que puede pedir seguro y vender su vehículo, sin decir si ve el seguimiento.
- Opciones:
  - A) No en P5b; el comprador recibe el mensaje de éxito y el administrador lo contacta (D-45: la plataforma solo pone en contacto). Queda como ítem futuro del backlog, junto con sus contactos de P5.
  - B) Sí: lista de solo lectura con fecha, tipo y estado (Pendiente / Finalizado), sin notas internas.
- Recomendación: A. Mantiene P5b pequeño y coherente con P5.
- **Decisión (responsable, 2026-11-06): A.** No en P5b. Una sola pantalla "Mis solicitudes" en "Mi cuenta" llegará con P6, con avisos, contactos, créditos y búsquedas juntos.

## D-54 — Alcance de P6 frente a D-45: ¿qué es una solicitud de crédito en el portal? · TOMADA (2026-11-10)
- Contexto (detectada al escribir `docs/criterios/criterios-P6-creditos.md`, sección 2, C-1): D-45 deja "por confirmar al planear P6" si créditos y búsquedas siguen con formularios, estados y seguimiento, o se reducen a una solicitud de contacto para el administrador.
  - La v3.0 **no** pide literalmente estudio de crédito, documentos, ingresos, centrales de riesgo ni entidades financieras. Sus reglas son muy breves: RN-CO-006 "Permite registrar solicitudes", RN-CO-007 "Asigna responsable", RN-CO-008 "Permite gestionar solicitud. Seguimiento".
  - Lo que choca con D-45 es lo que implican las fuentes: FN-03 "Gestión → Resultado"; la minuta (M06 "gestión, seguimiento y comunicación"; FN-03 "evaluación/seguimiento"); y la nota P6 de la política de datos ("se recogerán datos financieros o laborales… centrales de riesgo, Ley 1266 de 2008").
- Opciones:
  - A) **Solicitud de contacto con seguimiento mínimo.** El crédito es un tipo más de solicitud (como los avisos de P5b). Lo atiende el administrador, que anota, cierra con un resultado y reabre. No hay documentos, estudio, ingresos, entidad financiera ni respuesta del banco en el portal.
    - Impacto: reutiliza `leads`; es una entrega mediana.
  - B) Como A, más un registro interno de la entidad financiera ante la que se gestiona y de su respuesta.
    - Impacto: guarda en el portal datos de la gestión que D-45 llama "interna"; requiere tabla de entidades y revisión legal.
  - C) Flujo completo de gestión de crédito (documentos, estudio, respuesta).
    - Impacto: contradice D-45; es una entrega grande con revisión legal (Ley 1581 y Ley 1266).
- Recomendación del analista: **A**. Es la lectura directa de D-45 ("en esta plataforma solo vamos a hacer contacto").
- **Decisión (responsable, 2026-11-10): A.** Solicitud de contacto con seguimiento mínimo; la gestión ante bancos es interna.

## D-55 — Datos del formulario de crédito · TOMADA (2026-11-10)
- Contexto: D-9 ya fija nombre y teléfono obligatorios y correo opcional, y que **no se exige cuenta**. Falta decidir qué más se pide. El script 002 preveía un "valor solicitado". La política de datos advierte que los datos financieros o laborales exigen ampliar la política y, si se consultan centrales, una autorización propia.
- Opciones:
  - A) Además de los datos de contacto, tres campos **opcionales**:
    - monto que necesita financiar;
    - cuota inicial disponible;
    - tipo de vehículo, con la lista de D-50 (solo cuando se pide desde la portada; desde la ficha el vehículo ya se conoce).
    - Más un comentario opcional de 500 caracteres. Sin ingresos, actividad, documento ni historial.
  - B) Solo los datos de contacto y el comentario, como un aviso.
  - C) A más ingresos mensuales aproximados y actividad (empleado, independiente, transportador).
    - Impacto: son datos financieros del titular; exigen ampliar la política y una revisión del abogado antes de salir a internet.
- Recomendación del analista: **A**. El monto y la cuota hablan de la compra, no de la situación financiera de la persona, y ayudan al administrador a preparar la llamada. Los ingresos se piden en el proceso interno (D-45).
- Impacto en el esquema: A necesita la tabla `leads_credito` y que `CK_Lead_Origen` admita `tipo_vehiculo` en los créditos; B no necesita tabla.
- **Decisión (responsable, 2026-11-10): B.** Solo nombre, teléfono, correo opcional y comentario opcional (máx. 500), como un aviso. En palabras de la responsable: "esto es interno cuando [se] comunique, esos datos no deberían quedar en la página por seguridad del cliente". No se piden monto, cuota inicial, ingresos ni tipo de vehículo; no se crea la tabla `leads_credito`.
- PROPUESTO (coordinador, técnico): la ayuda del comentario pide no escribir datos financieros ni documentos ("Cuéntanos qué vehículo te interesa. No escribas ingresos, documentos ni datos bancarios.").

## D-56 — La búsqueda "Busco un vehículo" · TOMADA (2026-11-10)
- Contexto: HU-034 a HU-038 y RN-BU-001/002 piden registrar búsquedas, relacionar vehículos y cerrarlas; D-7 las asigna al administrador.
  - FN-02 de la v3.0 incluye "Validación → **Publicación**" y la minuta dice "el sistema compara opciones" (sección 2, C-5 de los criterios). Publicar la necesidad del comprador expondría sus datos a los asesores (D-7) y va más allá de "poner en contacto" (D-45).
  - El script 002 preveía tipo de vehículo, marca, presupuesto mínimo y máximo, municipio y requisitos.
- (a) **Campos del formulario:**
  - A) Categoría obligatoria (las cuatro de D-17). Opcionales: marca, modelo (de esa marca y categoría, D-32), año desde y hasta, presupuesto máximo, departamento y ciudad (DIVIPOLA, D-29) y descripción de hasta 500 caracteres.
  - B) Solo categoría y descripción libre obligatoria de hasta 500 caracteres, como "Vende tu camión".
  - C) Como A, con el presupuesto máximo obligatorio.
  - Recomendación: A. Con los criterios en campos, el administrador puede buscar coincidencias en el catálogo.
- (b) **¿El administrador relaciona vehículos a una búsqueda, y el comprador los ve?**
  - A) Sí. El administrador relaciona publicaciones visibles, con quién y cuándo, y las puede retirar. El comprador con cuenta las ve en "Mis solicitudes", con enlace a la ficha. Si contacta desde ahí, se crea un contacto normal de P5. Al visitante sin cuenta, el administrador le envía los enlaces por WhatsApp, fuera del portal.
  - B) Sí, pero solo como registro interno: el comprador no las ve.
  - C) No; HU-036 queda fuera y el administrador lo anota como nota.
  - Recomendación: A. Cumple HU-036 y da valor a la cuenta de comprador sin exponer sus datos al asesor.
- (c) **¿Coincidencias automáticas** (avisar cuando se publique un vehículo que coincide; `alertas_busqueda` del script 002)?
  - A) No en P6. Hay solo un botón "Ver coincidencias" bajo demanda para el administrador.
  - B) Sí, con un aviso dentro del panel del administrador.
  - Recomendación: A. Sin servicio de correo (D-5) el aviso automático al comprador no es posible, y B añade un proceso en segundo plano.
- (d) **¿Dónde se ofrece en el portal?**
  - A) En el catálogo (cuando no hay resultados y al final de la lista) y en la columna "Servicios" del pie. Menú, portada y franja sin cambios.
  - B) Como A, más un bloque en la portada junto a los de D-51.
  - Recomendación: A. Aparece donde la persona no encuentra lo que busca, sin recargar la portada (D-42).
- Impacto en el esquema: (a) A necesita la tabla `leads_busqueda`; (b) A necesita la tabla `busqueda_vehiculos` y dos tipos de evento nuevos.
- **Decisión (responsable, 2026-11-10): las recomendaciones.** (a) A, (b) A, (c) A, (d) A.
- **Cambio (responsable, 2026-11-12): (d) pasa a B.** Al probar no encontró el acceso: "ese botón ahí no se ve, ni se nota que es para que un cliente pueda acceder; si son servicios deben ser accesibles, visibles, fáciles de encontrar". Se añade un tercer bloque "¿No encuentras el vehículo que buscas?" en la portada, junto a los de D-51; siguen el catálogo y el pie.

## D-57 — Estados y resultados de cierre de créditos y búsquedas · TOMADA (2026-11-10)
- **Contradicción:** D-4 (TOMADA) fija los estados del crédito Registrado → Asignado → Gestionado → Cerrado. Los leads de P5 y P5b usan ABIERTO/CERRADO. Además, con D-7 el paso a "Asignado" ocurre al instante (siempre al administrador), y con D-45 la gestión ocurre fuera del portal. La v3.0 no define estados de búsqueda.
- (a) **Estados:**
  - A) Se mantiene ABIERTO/CERRADO en la base y se **muestran** los estados de D-4 adaptados: "Registrado" (abierto sin notas), "En gestión" (abierto con al menos una nota del administrador) y "Cerrado". "Asignado" no se muestra porque coincide con "Registrado". Vale igual para las búsquedas.
  - B) Estados reales Registrado / Asignado / Gestionado / Cerrado para créditos, con acciones explícitas.
    - Impacto: amplía `CK_Lead_Estado` y cambia el cierre, el contador y la bandeja comunes.
  - C) Solo ABIERTO/CERRADO, como los avisos (modifica D-4).
  - Recomendación: A. Respeta D-4 en lo que ve la gente sin complicar el modelo común.
- (b) **Resultados de cierre:**
  - Crédito:
    - A) "Remitido a entidad financiera" (nuevo), "Solicitud atendida", "No le interesó", "No respondió", "Contacto duplicado o inválido" y "Otro".
    - B) Como A, más "Aprobado por la entidad" y "No aprobado por la entidad".
      - Impacto: guarda en el portal el resultado de un estudio de crédito, un dato financiero del titular; requiere revisión legal (Ley 1266).
  - Búsqueda: "Encontró vehículo" (nuevo), "Sin vehículo disponible" (nuevo), "No le interesó", "No respondió", "Contacto duplicado o inválido" y "Otro".
  - Recomendación: A para el crédito y la lista propuesta para la búsqueda. Sirven para el reporte HU-078 (P8) sin registrar la decisión del banco.
- Impacto en el esquema: amplía `CK_Lead_Resultado`; con (a) B también `CK_Lead_Estado`.
- **Decisión (responsable, 2026-11-10): las recomendaciones.** (a) A: estados mostrados Registrado / En gestión / Cerrado. (b) A para el crédito, sin aprobado ni negado, y la lista propuesta para la búsqueda.

## D-58 — ¿Se reasignan créditos y búsquedas a un asesor? ¿El asesor ve los créditos de sus vehículos? · TOMADA (2026-11-10)
- **Contradicción:** D-7 dice que el administrador "puede reasignar manualmente **cualquier** solicitud a un asesor". D-52 decidió que los avisos nunca se reasignan, para no exponer datos de clientes.
  - Además, un crédito pedido desde la ficha de un vehículo queda ligado a la publicación de un asesor.
  - El código de P5b ya trata todo origen distinto de `CONTACTO_VEHICULO` como solicitud de Administración: no se reasigna (409), el asesor no lo ve y `CK_Lead_AvisoAdmin` lo impide en la base.
- Opciones:
  - A) Nunca se reasignan, como D-52. El asesor no ve créditos ni búsquedas, tampoco los pedidos desde sus vehículos. Si el administrador necesita al asesor, lo coordina fuera del sistema.
    - Impacto: sin cambios en `CK_Lead_AvisoAdmin`.
  - B) Se reasignan como un contacto de P5, con motivo y auditoría. El asesor los ve mientras los tenga asignados y entran en el barrido de D-46.
    - Impacto: hay que reescribir `CK_Lead_AvisoAdmin` y separar en el código "aviso" de "solicitud reasignable".
  - C) Solo las búsquedas se reasignan; los créditos no.
  - D) Como A, pero el asesor dueño ve, solo como aviso y sin los datos del cliente, que se pidió un crédito para su vehículo.
- Recomendación del analista: **A**. Es coherente con D-52, D-7 ("créditos y búsquedas los atiende el administrador") y D-45. Además, ya es el comportamiento del código.
- **Decisión (responsable, 2026-11-10): A.** Créditos y búsquedas nunca se reasignan; el asesor no los ve.

## D-59 — Qué muestra "Mis solicitudes" · TOMADA (2026-11-10)
- Contexto: D-53 fija una sola pantalla en "Mi cuenta" con avisos, contactos, créditos y búsquedas. **Ya está decidido** que incluye los contactos de P5. Falta el detalle. La opción B descrita en D-53 hablaba de "fecha, tipo y estado (Pendiente / Finalizado), sin notas internas".
- (a) **¿Qué solicitudes entran?**
  - A) Solo las enviadas con la sesión iniciada (`usuario_id`).
  - B) Además, las enviadas sin sesión con el mismo teléfono o correo de la cuenta.
    - Impacto: el teléfono y el correo de la cuenta no están verificados (no hay correo, D-5), así que alguien podría ver solicitudes ajenas.
  - Recomendación: A.
- (b) **¿Qué se muestra?**
  - A) Fecha, tipo, resumen (vehículo, tipo de vehículo o categoría buscada), estado (D-57) y, en las búsquedas, los vehículos relacionados (D-56 b). Sin notas, historial, responsable, consentimientos, resultado ni observación de cierre.
  - B) Como A, más el resultado de cierre ("No respondió", "Remitido a entidad financiera"…).
  - C) Como A, más quién lo atiende (asesor o administración).
  - Recomendación: A. Es lo mínimo útil; el resultado y las notas son del trabajo interno.
- (c) **¿Solo lectura?** Se propone que sí: el comprador no edita, cancela ni borra; para cancelar, escribe por WhatsApp. Si la responsable quiere un botón "Ya no lo necesito", cierra la solicitud con un resultado propio.
- (d) **¿Quién la ve?** Se propone solo el rol COMPRADOR (D-53 habla del comprador). El asesor tiene "Mis contactos" y el administrador, la bandeja.
- **Decisión (responsable, 2026-11-10): las recomendaciones.** (a) A, (b) A, (c) solo lectura, (d) solo COMPRADOR.


## D-60 — Detalles técnicos de P6 decididos por el coordinador · TOMADA (2026-11-10)
- Contexto: puntos marcados PROPUESTO en `docs/criterios/P6-contrato-api.md` y hallazgos de la revisión de seguridad (M-P6-1) y de QA (O-1). Son decisiones técnicas y reversibles; no cambian reglas de negocio.
- Crédito desde la ficha: solo sobre una publicación visible (plan vigente), según CA-P6-04; si no, 404.
- Búsqueda: se guarda también `departamento_id`; el presupuesto es BIGINT (pesos sin decimales).
- Las listas del formulario de búsqueda salen de la ruta pública de solo lectura `GET /api/catalogo/listas-busqueda` (solo entradas activas, sin datos personales, caché de 5 minutos).
- Retirar un vehículo de una búsqueda cerrada da 409. "En gestión" se mantiene tras reabrir.
- "Ver coincidencias" sin criterios guardados responde 409 en lugar de 500.
- Repetidos (M-P6-1 y O-1), en contacto, avisos, crédito y búsqueda:
  - solo se agrega a una solicitud abierta del mismo origen;
  - y del mismo `usuario_id`; dos anónimas cuentan como iguales;
  - si no coincide, se crea una solicitud nueva;
  - nunca se asigna un usuario a una solicitud anónima existente (D-59 a);
  - los límites por teléfono no cambian.

---

# Decisiones de P7 "Ventas, planes y administración" (D-61 a D-66)

Detectadas al escribir `docs/criterios/criterios-P7-ventas-admin.md` (2026-11-12, analista funcional). Ninguna bloquea el historial de ventas ni la regresión de P4b y P4p. Cada una se escribe con la opción recomendada en los criterios (marcados PENDIENTE D-n).

## D-61 — ¿Los planes se cobran dentro del portal? · TOMADA (2026-11-12)
- Contexto: D-21 y D-26 dejaron los pagos para P7; hoy el administrador activa o renueva a mano cuando confirma el pago y **no queda registro del pago**, solo la auditoría de la activación. La v3.0 menciona la "pasarela de pagos" solo como integración **posible** (§14); ninguna HU pide cobrar en línea, facturar ni llevar contabilidad. El script 002 tiene una tabla `pagos` (valor, referencia, soporte_archivo, estado) sin usar. El plan del asesor es el único dinero que entra a la empresa por el portal (las ventas de vehículos ocurren fuera, D-45).
- Opciones:
  - A) **Todo fuera del portal, como hoy.** Activación manual sin registro del pago.
    - Impacto: sin trabajo ni migración; el administrador lleva los pagos en otro lado.
  - B) **Cobro fuera del portal y registro interno del pago.** Al asignar o renovar un plan con precio, el administrador anota valor, fecha, medio (Transferencia, Consignación, Efectivo, Otro) y referencia opcional; puede anular un pago con motivo; solo él lo ve.
    - Impacto: entrega pequeña; probablemente una migración 016 reversible sobre `pagos`. No es factura ni contabilidad.
  - C) **Como B, más comprobante adjunto** (foto o PDF de la transferencia).
    - Impacto: archivos con datos bancarios del asesor; almacenamiento y revisión de la política de datos (D-44).
  - D) **Pasarela de pagos** (PSE o tarjeta), con activación automática.
    - Impacto: **integración externa**: contrato con un proveedor, comisiones, pruebas en entorno del proveedor, seguridad de pagos y, en la práctica, factura electrónica. Iría en P8 o después.
- En todas las opciones la **factura electrónica y la contabilidad quedan fuera del portal**, salvo que la responsable pida otra cosa.
- Recomendación del analista: **B**. Da a la administración el historial de pagos por suscripción con poco trabajo y sin integraciones. Si la responsable prefiere no guardar nada de pagos en el portal, A es válida.
- **Decisión (responsable, 2026-11-12): B.** Cobro fuera del portal y registro interno del pago; sin comprobante, pasarela, factura ni contabilidad.

## D-62 — Ventas: qué es la reversión, quién la hace y quién ve el historial · TOMADA (2026-11-12)
- Contexto: P4b (aprobado) ya implementa la reversión según la minuta §6 ("reversión por asesor propietario con motivo y auditoría; reactivación requiere confirmación y cupo"), pero permite revertir **al dueño y al administrador**, sin plazo. `ventas_vehiculo` conserva todas las ventas (también las revertidas), pero hoy nadie puede consultarlas.
- (a) **¿Quién revierte?**
  - A) El asesor dueño o el administrador, sin plazo, con motivo y auditoría (lo que ya hace el código).
  - B) Solo el administrador.
  - C) El dueño dentro de los 30 días siguientes al registro de la venta; después, solo el administrador.
  - Recomendación: A. Coincide con la minuta y con D-33 ("el dueño puede modificar todo lo suyo"); el administrador conserva sus facultades.
- (b) **¿Qué significa revertir?**
  - A) Deshacer la marca de vendido (la venta no se cerró o se marcó por error): vehículo ACTIVO, publicación OCULTA hasta reactivarla con plan, la venta queda como "Revertida" con su motivo. Para corregir un dato (fecha, precio, origen) se revierte y se vuelve a marcar vendido (comportamiento actual).
  - B) Como A, y además una acción "Corregir datos de la venta" sin revertir, auditada.
  - Recomendación: A. Ya existe y deja todo trazado.
- (c) **¿Quién ve el historial de ventas?**
  - A) El administrador ve todas; el asesor, las de los vehículos que hoy son suyos, también las revertidas.
  - B) Solo el administrador.
  - Recomendación: A. Es el mismo alcance que su inventario.
- (d) **¿Se muestra la auditoría completa del vehículo** (línea de tiempo de cambios)?
  - A) No en P7; la auditoría sigue registrándose y se consulta en P8 o con apoyo técnico.
  - B) Sí, solo para el administrador y de solo lectura.
  - Recomendación: A, para mantener P7 pequeño.
- **Decisión (responsable, 2026-11-12): las recomendaciones.** (a) A, (b) A, (c) A, (d) A.

## D-63 — ¿Qué parámetros cambia el administrador sin programador? · TOMADA (2026-11-12)
- Contexto: HU-081 / RN-PA-001 piden "configuración global" sin detallarla. Hoy el WhatsApp, el correo y el horario del pie están fijos en el frontend (`PiePortal.tsx`), confirmados en D-42, y el WhatsApp del portal de D-47 está en `appsettings.json`. Los catálogos (HU-082 a HU-084) y la foto de portada ya los administra él (D-37, D-42). HU-085 (información corporativa) y HU-086 (textos legales) dependen de la revisión legal (D-44). HU-087 habla de "promociones", que son marketing (P8).
- Opciones:
  - A) **Solo los datos de contacto del portal:** WhatsApp, correo, horario (hasta 4 líneas) e Instagram y TikTok (se muestran solo si tienen enlace). El botón de WhatsApp de la ficha (D-47) usa el mismo número.
    - Impacto: entrega pequeña; probablemente sin migración (`configuracion_sistema` ya existe en el script 002; hay que verificarlo en la base real).
  - B) **A más información corporativa y textos legales** (razón social, NIT, dirección, política y términos).
    - Impacto: no se puede publicar hasta que el abogado revise y la responsable entregue los datos (D-44); se haría cuando eso esté listo.
  - C) **B más valores de negocio:** días por mes del plan, número y tamaño de las fotos, límites anti-abuso, días del aviso de vencimiento.
    - Impacto: el administrador podría cambiar reglas de negocio (RN-SU-005 "30 días", D-20, D-35) sin control de cambios (v3.0 §16), y algunos valores afectan la seguridad.
- Recomendación del analista: **A ahora**; la parte corporativa y legal de B cuando se libere D-44; **C no**. HU-087 (promociones) pasa a P8 con marketing.
- **Decisión (responsable, 2026-11-12): A ahora.** B cuando se libere D-44; C no.

## D-64 — ¿Qué muestra el panel del administrador? · TOMADA (2026-11-12)
- Contexto: el encargo de P7 incluye "panel administrativo e indicadores básicos". HU-075 (dashboard) y HU-080 (indicadores comerciales) son del módulo de Reportes, que el backlog deja en P8 junto con HU-076 a HU-079 (reportes filtrables y exportación). HU-073 y HU-074 piden avisar antes de los vencimientos, y no hay servicio de correo (D-5, D-48).
- (a) **Contenido del panel "Inicio":**
  - A) **Contadores operativos**, cada uno con enlace a su lista filtrada: asesores pendientes de aprobación; solicitudes abiertas y nuevas por tipo (contactos de Administración, "Vende tu camión", seguros, créditos, búsquedas); suscripciones que vencen en los próximos 7 días y vencidas sin renovar; publicaciones visibles; vehículos por estado; ventas de los últimos 30 días (cantidad). Sin gráficos, sumas de dinero ni exportación.
  - B) A más indicadores comerciales: valor vendido del mes, ventas por origen (D-39), contactos por publicación, con gráficos.
  - C) El dashboard completo de HU-075 y HU-080, con filtros por fecha y exportación.
  - Recomendación: A en P7; B y C en P8, donde están los reportes.
- (b) **Aviso de vencimiento dentro del panel** (HU-073 y HU-074 en parte, como D-48):
  - A) Sí: el contador del administrador y, en "Mi suscripción" del asesor, "Tu plan vence en N días" cuando falten 7 días o menos.
  - B) No; todo aviso de vencimiento queda para P8.
  - Recomendación: A. Es barato y evita que las publicaciones se oculten sin aviso (D-41).
- **Decisión (responsable, 2026-11-12): las recomendaciones.** (a) A, (b) A.

## D-65 — Cita equivocada de HU-081 en el contrato de P4p · TOMADA (2026-11-12)
- Contradicción documental, sin impacto en el código: `docs/criterios/P4p-contrato-api.md` (líneas 11 y 137) llama "HU-081" a las notificaciones de vencimiento de suscripción. Por D-1, en la matriz oficial HU-081 es "Administrar parámetros generales" y la notificación de vencimiento de suscripción es **HU-074** (la lista de la sección 10 la llama HU-081).
- Opciones: A) corregir la cita a HU-074 en ese contrato (documentador); B) dejarla y añadir una nota.
- Recomendación: A. No requiere a la responsable; la puede cerrar el coordinador.
- **Decisión (coordinador, 2026-11-12): A.** Corregidas las dos citas en `P4p-contrato-api.md`.

## D-66 — Detalles técnicos de P7 decididos por el coordinador · TOMADA (2026-11-12, técnica)
- Contexto: los tres puntos de la sección "Pendiente" de `docs/criterios/P7-contrato-api.md`, B-4 de `REVISION-SEGURIDAD-P7.md` y O-5 de `P7-evidencia.md`. Son decisiones técnicas y reversibles; no cambian las reglas de negocio de D-61 B ni de D-64 A.
- **Pago opcional** al asignar o renovar un plan (y al cambiar de plan). La pantalla lo ofrece ya marcado cuando el plan tiene precio; si el administrador lo desmarca, la suscripción se guarda sin pago.
- **Pago posterior:** se puede registrar un pago después, desde "Pagos" de la suscripción (`POST /api/admin/suscripciones/{id}/pagos`, concepto `POSTERIOR`).
- **Referencia obligatoria con el medio "Otro"** (CA-P7-29). Con los demás medios es opcional.
- **Pago antiguo sin fecha o sin medio** (filas anteriores a P7): no se puede anular; la API responde 409 (B-4).
- **Filtros de Suscripciones** `vista=porVencer` y `vista=vencidasSinRenovar`, con la misma consulta que los contadores del panel; los enlaces del panel los usan (O-5, CA-P7-46).
- Detalle en "Cambios al contrato" de `P7-contrato-api.md`.

## D-67 — Anular el pago de una renovación también deshace sus días · TOMADA (2026-11-13)
- Contexto: en la prueba manual de P7, la responsable anuló el pago de la renovación de un asesor y el plan conservó los 30 días extra. Hasta ahora "Anular" solo anulaba el registro del pago (D-61 B, D-66). En sus palabras: "si yo anulo ese pago, los días adicionales que vienen con el pago tampoco deberían existir".
- Opciones que se le presentaron: A) anular solo corrige el pago, como estaba; B) anular el pago deshace también lo que ese pago dio; C) un botón aparte "Deshacer renovación".
- **Decisión (responsable, 2026-11-13): B**, con estas reglas:
  - pago de una **renovación**: la fecha de vencimiento vuelve a la de antes de esa renovación;
  - pago de una **asignación**: la suscripción queda cancelada;
  - pago de un **cambio de plan**: la suscripción vuelve al plan y a la fecha anteriores al cambio;
  - pago **posterior** (sin renovación): solo se anula el pago;
  - solo se puede anular el pago del movimiento más reciente de la suscripción; si hubo otro después, primero se anula ese;
  - si la fecha restaurada ya pasó, la suscripción queda vencida y sus publicaciones dejan de verse (D-41);
  - el aviso de confirmación dice exactamente qué pasará; el motivo es obligatorio y todo se audita.
- Para corregir un pago mal escrito: se anula, lo que deshace la renovación, y se renueva de nuevo con los datos correctos.
- Nota: el pago 1 (renovación de Xavi Quintero) se anuló antes de esta regla y su renovación se conserva. Es un dato de prueba; se limpia antes de producción.
