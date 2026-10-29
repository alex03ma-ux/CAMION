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
- **Decisión: A** (respuesta de la responsable, transmitida por el coordinador principal). **Solo el administrador** puede reasignar un vehículo de un asesor a otro asesor activo, con **motivo obligatorio** y auditoría (quién, cuándo, de quién a quién). Se construye **junto con P4b**, no en P4a.
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
