# Guía rápida: panel, ventas, pagos del plan y datos de contacto (P7)

Para la responsable del portal. Explica qué hay de nuevo para la administración y para los asesores, cómo se aplica la migración 016 y cómo hacer la prueba manual.

Estado: rama `feature/p7-ventas-admin`, EN REVISIÓN (rama por publicar). Las pruebas automáticas están PROBADAS: 1240/1240 de API y 699/699 de pantallas. **La migración 016 NO está aplicada. Sin ella, la API de esta rama no debe arrancarse contra `camion_ventas`.** La prueba manual está pendiente y las pruebas E2E no se han ejecutado.

## 1. Qué hay de nuevo

**Para la administradora** (en "Mi cuenta"):
- **Inicio:** un panel con números: asesores pendientes, solicitudes abiertas y nuevas por tipo, suscripciones por vencer y vencidas sin renovar, publicaciones visibles, vehículos por estado y ventas de los últimos 30 días. Cada número lleva a su lista ya filtrada. No muestra nombres, teléfonos ni dinero (D-64).
- **Ventas:** todas las ventas registradas, también las revertidas con su motivo. Se pueden filtrar por fechas, origen, estado y asesor. Cada fila lleva al vehículo, donde está su "Historial de ventas" (D-62).
- **Pagos del plan**, dentro de **Suscripciones** (D-61 B y D-66):
  - el pago se cobra **fuera del portal**; aquí solo se anota;
  - al asignar o renovar un plan con precio, aparece "Pago recibido" con la casilla "Anotar ahora el pago recibido" ya marcada. Si todavía no tiene el pago, desmárquela y anótelo después;
  - el botón "Pagos" de cada suscripción muestra la lista y el total (sin contar los anulados), con "Registrar pago" y "Anular pago";
  - con el medio "Otro", la referencia es obligatoria;
  - un pago mal escrito no se borra: se anula con un motivo y queda a la vista como "Anulado";
  - el portal no recibe tarjetas ni cuentas bancarias y no hace facturas.
- **Parámetros:** el WhatsApp, el correo, el horario (de 1 a 4 líneas), Instagram y TikTok del portal (D-63). Se ven en el pie, en la página de Privacidad y en el botón de WhatsApp de la ficha cuando el vendedor no tiene celular o es la administración. Instagram y TikTok solo se muestran si tienen enlace.

**Para el asesor:**
- **Mis ventas:** las ventas de sus vehículos actuales, también las revertidas.
- **Mi suscripción:** cuando faltan 7 días o menos, dice "Tu plan vence en N días. Para renovarlo, comunícate con el administrador." y muestra el botón "Escribir por WhatsApp para renovar". El botón también aparece si la suscripción ya venció. Solo la administración renueva (D-36).
- El asesor **no ve** los pagos.

## 2. Cómo aplicar la migración 016

Hágalo con el coordinador acompañando. El detalle técnico completo está en `docs/coordinacion/REVISION-SEGURIDAD-P7.md` (sección "Migración 016"). La 016 solo agrega columnas y reglas a la tabla `pagos`, que hoy está vacía. No borra datos.

1. **Detener la API.** En la terminal donde está corriendo, pulse `Ctrl+C` (o cierre esa terminal).
2. **Hacer el respaldo con un nombre nuevo.**
   - Abra una consola en `C:\xampp\mysql\bin`.
   - Revise con `dir C:\Respaldo_CAMION\` que el nombre que va a usar no exista. Nunca sobrescriba ni borre un respaldo.
   - Ejecute el comando, cambiando `AAAAMMDD_HHMM` por la fecha y la hora de hoy:
     `mysqldump -u root -p --single-transaction --routines --triggers --databases camion_ventas --result-file=C:\Respaldo_CAMION\camion_ventas_antes_016_p7_AAAAMMDD_HHMM.sql`
   - Le pedirá la contraseña de MariaDB. Escríbala usted; no la pegue en el chat.
   - Use `--result-file` y no `>`: la redirección de PowerShell guarda el archivo en otro formato (UTF-16).
   - Compruebe que el archivo pesa más de 0 bytes y que termina con `-- Dump completed`. Anote el nombre, el tamaño y la hora para la bitácora.
3. **Antes de importar**, el coordinador revisa la base con consultas de solo lectura y anota los conteos (pagos, suscripciones, planes, ventas, auditoría y parámetros).
4. **Importar en phpMyAdmin.**
   - En la columna de la izquierda, haga clic primero en la base **`camion_ventas`**. Es obligatorio: el archivo no dice a qué base va.
   - Vaya a **Importar**, elija **solo** `database/migraciones/016_p7_pagos_SUBIR.sql` y pulse **Importar** (o "Continuar").
   - Si sale un error, **no lo vuelva a importar**. Avise al coordinador, que revisará qué parte se aplicó.
5. **Verificar.**
   - Usted puede abrir la tabla `pagos`, pestaña **Estructura**, y mirar que aparezcan seis columnas nuevas: `fecha_pago`, `medio`, `concepto`, `anulado_por`, `anulado_en` y `motivo_anulacion`. La tabla sigue vacía.
   - El coordinador ejecuta la verificación completa: los mismos conteos que antes, las reglas nuevas y la relación `FK_Pago_AnuladoPor`.
6. Con la verificación correcta, el coordinador compila y arranca la API de esta rama (sección 4).

## 3. Prueba manual (después de aplicar la 016)

Use solo datos inventados, por ejemplo la referencia "PRUEBA-001". No escriba datos bancarios reales.

1. **Panel "Inicio".** Entre como administradora y abra "Mi cuenta" › **Inicio**.
   - Compruebe que solo hay números y que un número en 0 dice también en palabras, por ejemplo "Sin asesores pendientes".
   - Haga clic en cada número y compruebe que la lista que se abre tiene ese mismo total. Los de suscripciones abren Suscripciones con la vista "por vencer" o "vencidas sin renovar" a la vista, que se puede quitar.
   - Compruebe que la navegación tiene Inicio, Ventas y Parámetros, además de las secciones de antes.
2. **Asignar o renovar un plan con pago.** En **Suscripciones**, renueve el plan de un asesor de prueba (o asígnele uno).
   - Debe salir "Pago recibido" con la casilla marcada y el valor del plan ya escrito.
   - Elija el medio "Otro" y deje la referencia vacía: debe pedirla.
   - Escriba la referencia "PRUEBA-001" y guarde.
3. **Ver, registrar y anular pagos.** En esa suscripción, pulse **Pagos**.
   - Debe verse el pago del paso 2 y el total.
   - Pulse **Registrar pago** y anote otro con medio "Transferencia". El total debe subir.
   - Anule uno con **Anular pago** y un motivo. Debe quedar como "Anulado", el total debe bajar y la suscripción no debe cambiar.
4. **Historial de ventas.**
   - Como administradora, abra **Ventas**. Deben verse todas las ventas, las revertidas con su motivo. Pruebe un filtro y "Limpiar filtros". Abra una fila y mire el "Historial de ventas" del vehículo.
   - Si quiere más datos, marque un vehículo como vendido, revierta la venta con un motivo y vuelva a venderlo: el historial debe mostrar las dos ventas, la vigente primero.
   - Como asesor (Xavi Quintero), abra **Mis ventas**: solo deben verse las de sus vehículos.
5. **Datos de contacto.** Como administradora, abra **Parámetros**.
   - Anote los valores actuales para dejarlos igual al final.
   - Cambie el horario y escriba un enlace de Instagram y otro de TikTok de prueba (deben empezar por `https://www.instagram.com/` y `https://www.tiktok.com/`).
   - Pruebe un enlace que empiece por `javascript:`: debe rechazarlo.
   - Guarde y abra el portal sin sesión. El pie debe mostrar el horario nuevo y los enlaces de Instagram y TikTok.
   - Abra la ficha de un vehículo cuyo vendedor no tenga celular, o que sea de la administración (por ejemplo, el JAC JRR 2024, si el teléfono de Xavi sigue siendo de 9 dígitos, D-49). El botón de WhatsApp debe usar el número del portal.
   - Al final, deje los valores reales del portal y borre los enlaces de prueba si no son los definitivos.
6. **Como el asesor Xavi.** Cierre sesión y entre con la cuenta de Xavi Quintero. Abra "Mi cuenta" › **Mi suscripción**.
   - Si su plan vence en 7 días o menos, debe ver "Tu plan vence en N días…" y el botón **"Escribir por WhatsApp para renovar"**, que abre WhatsApp con el número del portal.
   - Si su plan vence en más de 7 días, el aviso no sale, y eso también es correcto.
   - No debe ver ningún pago.
7. **En el celular (375 px).** Si puede, revise Inicio, Ventas, Parámetros y Pagos en un celular o con la ventana angosta. Todo debe leerse y usarse sin desplazarse de lado.
8. Cuente al coordinador lo que salió bien y lo que no, para anotarlo en la bitácora. Los pagos de prueba quedan en la base como datos de prueba y se limpian antes de producción.

## 4. Si Windows bloquea la API

Al usar `dotnet run`, el Control de aplicaciones de Windows (Smart App Control) puede bloquear `Camion.Api.exe` recién compilado (pasó en P5b, ver `BITACORA.md`). No hace falta cambiar esa protección. La API se arranca así, desde la carpeta `backend\Camion.Api`:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5080"
dotnet bin\Debug\net8.0\Camion.Api.dll
```

Antes debe estar compilada la versión de esta rama (`dotnet build`), y la 016 debe estar aplicada. Las claves siguen en `dotnet user-secrets`; no se escriben en la consola ni en el chat. Para comprobar que arrancó, abra http://localhost:5080/health.

## 5. Antes de publicar en internet

- INF-2: límite por IP detrás del proxy y actualización de MySqlConnector.
- Revisión legal de la política de datos y de los términos (D-44). Hasta entonces, la información corporativa y los textos legales (HU-085 y HU-086) siguen BLOQUEADOS.
- Limpiar de `camion_ventas` los pagos y las ventas de prueba.
