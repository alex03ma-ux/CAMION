# Guía rápida: créditos, "Busco un vehículo" y "Mis solicitudes" (P6)

Para la responsable del portal. Explica cómo llegan las solicitudes de crédito y las búsquedas, quién las ve, cómo se aplica la migración 015 y cómo hacer la prueba manual.

Estado: rama `feature/p6-creditos`, EN REVISIÓN (rama por publicar). Las pruebas automáticas están PROBADAS: 1118/1118 de API y 643/643 de pantallas. **La migración 015 NO está aplicada. Sin ella, la API de esta rama no debe arrancarse contra `camion_ventas`.** La prueba manual está pendiente.

## 1. Cómo pide un crédito un visitante

- **Desde la portada:** el botón "Quiero un crédito" de la banda de crédito abre el formulario. Ya no lleva al registro.
- **Desde la ficha de un vehículo:** el botón "Solicitar crédito para este vehículo" está junto al formulario de contacto. El formulario muestra el vehículo, que no se puede cambiar.
- **Qué pide (D-55):** nombre, teléfono, correo (opcional) y un comentario (opcional, hasta 500 caracteres). Nada más: **no pide** monto, cuota inicial, ingresos, documentos ni tipo de vehículo.
- La ayuda del comentario dice: "No escribas ingresos, documentos ni datos bancarios."
- El formulario explica que el portal es intermediario: gestiona el crédito ante bancos y entidades financieras.
- La casilla de autorización de datos empieza sin marcar y es obligatoria.
- No hace falta cuenta. Si es un comprador con sesión, su nombre y teléfono aparecen ya escritos.
- Al enviar sale "¡Listo! Recibimos tu solicitud…", con un botón opcional para escribir por WhatsApp.
- El asesor dueño del vehículo **no ve** el botón de crédito en su propia publicación.

## 2. Cómo registra una búsqueda ("Busco un vehículo")

- **Dónde está:** en el catálogo, cuando no hay resultados y al final de la lista de resultados, y en la columna **"Servicios"** del pie. El menú y la portada no cambian.
- **Qué pide (D-56):** la categoría (obligatoria) y, si quiere, marca, modelo, años desde y hasta, presupuesto máximo, departamento, ciudad y una descripción de hasta 500 caracteres. También nombre, teléfono, correo opcional y la casilla de autorización.
- La búsqueda **no se publica** y ningún asesor la ve.
- El sistema no avisa solo cuando aparece un vehículo que coincide. Es la administradora quien busca y relaciona vehículos (sección 4).

## 3. Quién ve créditos y búsquedas

- **Solo la administración**, en **"Contactos"**. Siempre quedan a cargo de Administración.
- **No se reasignan a un asesor** (D-58): no aparece el botón "Reasignar".
- Los asesores nunca los ven en "Mis contactos", tampoco los créditos pedidos desde sus propios vehículos.
- Si la misma persona vuelve a enviar mientras su solicitud sigue abierta, no se crea otra: se agrega al historial. Solo se juntan si son del mismo tipo y las envió la misma cuenta, o las dos sin cuenta (D-60).

## 4. Cómo se atienden en "Contactos"

- Cada fila dice su origen ("Crédito" o "Búsqueda") y su estado:
  - **"Registrado":** abierta y sin notas.
  - **"En gestión":** abierta y con al menos una nota.
  - **"Cerrado".**
- El filtro **"Origen"** deja ver solo un tipo.
- **Detalle del crédito:** datos de la persona, comentario, el vehículo si lo pidió desde una ficha (o "Sin vehículo asociado"), historial y autorización. No hay monto ni cuota.
- **Detalle de la búsqueda:** los criterios, escritos con nombres, y los vehículos relacionados.
  - **"Ver coincidencias"** muestra los vehículos publicados que cumplen los criterios.
  - **"Relacionar"** agrega uno a la búsqueda, con una nota interna opcional. El máximo es 10.
  - **"Retirar"** lo quita; queda en el historial.
- **"Guardar nota":** de 5 a 500 caracteres. No escriba datos financieros del cliente.
- **Cerrar:**
  - un crédito puede cerrarse, entre otros resultados, con **"Remitido a entidad financiera"**;
  - una búsqueda, con **"Encontró vehículo"** o **"Sin vehículo disponible"**.
- **"Reabrir":** con un motivo.

## 5. "Mis solicitudes" del comprador

- Está en "Mi cuenta" › **"Mis solicitudes"**. Solo la tienen los compradores.
- Muestra en una sola lista sus contactos, avisos, créditos y búsquedas, **solo los que envió con la sesión iniciada**.
- Cada fila tiene fecha, tipo, resumen y estado ("Pendiente", "Registrada", "En gestión" o "Finalizada").
- En las búsquedas aparece "Te recomendamos estos vehículos", con los vehículos que relacionó la administración y que siguen publicados.
- Es de solo lectura: no muestra notas, quién lo atiende ni el resultado de cierre.

## 6. Cómo aplicar la migración 015

Hágalo con el coordinador acompañando. El detalle técnico completo está en `docs/coordinacion/REVISION-SEGURIDAD-P6.md` (sección "Migración 015").

1. **Detener la API.** En la terminal donde está corriendo, pulse `Ctrl+C` (o cierre esa terminal).
2. **Hacer el respaldo con un nombre nuevo.**
   - Abra una consola en `C:\xampp\mysql\bin`.
   - Revise con `dir C:\Respaldo_CAMION\` que el nombre que va a usar no exista. Nunca sobrescriba ni borre un respaldo.
   - Ejecute el comando, cambiando `AAAAMMDD_HHMM` por la fecha y la hora de hoy:
     `mysqldump -u root -p --single-transaction --routines --triggers --databases camion_ventas --result-file=C:\Respaldo_CAMION\camion_ventas_antes_015_p6_AAAAMMDD_HHMM.sql`
   - Le pedirá la contraseña de MariaDB. Escríbala usted; no la pegue en el chat.
   - Use `--result-file` y no `>`: la redirección de PowerShell guarda el archivo en otro formato (UTF-16).
   - Compruebe que el archivo pesa más de 0 bytes y que termina con `-- Dump completed`. Anote el nombre, el tamaño y la hora para la bitácora.
3. **Antes de importar**, el coordinador revisa la base con consultas de solo lectura y anota los conteos.
4. **Importar en phpMyAdmin.**
   - En la columna de la izquierda, haga clic primero en la base **`camion_ventas`**. Es obligatorio: el archivo no dice a qué base va.
   - Vaya a **Importar**, elija **solo** `database/migraciones/015_p6_creditos_busquedas_SUBIR.sql` y pulse **Importar** (o "Continuar").
   - Si sale un error, **no lo vuelva a importar**. Avise al coordinador, que revisará qué parte se aplicó.
5. **Verificar.**
   - Usted puede mirar que en `camion_ventas` aparezcan dos tablas nuevas: `leads_busqueda` y `busqueda_vehiculos`, las dos vacías.
   - El coordinador ejecuta la verificación completa: los mismos conteos que antes, las reglas nuevas presentes y los índices.
6. Con la verificación correcta, el coordinador arranca la API de esta rama (sección 8).

## 7. Prueba manual (después de aplicar la 015)

Use solo datos inventados, por ejemplo el nombre "Prueba Crédito" y un teléfono de prueba.

**Límite de envíos:** desde un mismo equipo se admiten **5 envíos cada 10 minutos**, sumando contactos, avisos, créditos y búsquedas. Si sale "Has enviado muchas solicitudes. Intenta de nuevo en unos minutos.", espere unos minutos antes de seguir. Es el comportamiento esperado.

1. **Crédito desde la portada (sin cuenta).** Sin iniciar sesión, pulse "Quiero un crédito". Compruebe que solo pide nombre, teléfono, correo y comentario. Llénelo, marque la casilla y envíe. Debe salir "¡Listo!".
2. **Crédito desde una ficha.** Abra un vehículo del catálogo y pulse "Solicitar crédito para este vehículo". Compruebe que se ve el vehículo y envíe.
3. **Búsqueda desde el catálogo sin resultados.** En el catálogo, use filtros que no den resultados y pulse "Busco un vehículo". Elija una categoría y, si quiere, marca, modelo y ciudad, y envíe.
4. **Búsqueda desde el pie.** En la columna "Servicios" del pie, abra "Busco un vehículo" y envíe otra búsqueda con otro teléfono.
5. **Como administradora, en "Contactos":**
   - **Filtrar:** con el filtro "Origen" en "Crédito" deben verse solo los créditos, y en "Búsqueda", solo las búsquedas. Las nuevas deben decir "Registrado".
   - **Ver coincidencias:** abra una búsqueda y pulse "Ver coincidencias".
   - **Relacionar y retirar:** relacione un vehículo de la lista (con una nota si quiere) y luego retírelo. Vuelva a relacionar uno para el paso 6.
   - **Nota:** guarde una nota en un crédito. Debe pasar a "En gestión".
   - **Cerrar:** cierre el crédito con "Remitido a entidad financiera" y la otra búsqueda con "Encontró vehículo".
   - **Reabrir:** reabra uno de ellos escribiendo un motivo.
   - Compruebe que en ningún crédito ni búsqueda aparece "Reasignar".
6. **Como comprador con cuenta, "Mis solicitudes":**
   - Entre como comprador y envíe un crédito y una búsqueda con su sesión iniciada.
   - Desde la cuenta de administradora, relacione un vehículo a esa búsqueda.
   - Vuelva a entrar como comprador y abra "Mi cuenta" › "Mis solicitudes".
   - Deben verse sus dos solicitudes con su estado y, en la búsqueda, el vehículo recomendado.
   - No deben verse las solicitudes enviadas sin cuenta en los pasos 1 a 4, ni notas internas.
7. **Como el asesor Xavi.** Cierre sesión y entre con la cuenta de Xavi Quintero. En "Mis contactos" **no** debe ver ningún crédito ni búsqueda, tampoco si el crédito del paso 2 era de un vehículo suyo.
8. Cuente al coordinador lo que salió bien y lo que no, para anotarlo en la bitácora.

## 8. Si Windows bloquea la API

Al usar `dotnet run`, el Control de aplicaciones de Windows (Smart App Control) puede bloquear `Camion.Api.exe` recién compilado (pasó en P5b, ver `BITACORA.md`). No hace falta cambiar esa protección. La API se arranca así, desde la carpeta `backend\Camion.Api`:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5080"
dotnet bin\Debug\net8.0\Camion.Api.dll
```

Antes debe estar compilada la versión de esta rama (`dotnet build`). Las claves siguen en `dotnet user-secrets`; no se escriben en la consola ni en el chat.

## 9. Antes de publicar en internet

- INF-2: límite por IP detrás del proxy y actualización de MySqlConnector.
- Revisión legal de la política de datos (D-44). La política ya incluye las finalidades de crédito y búsqueda, sin datos financieros.
- Limpiar de `camion_ventas` los créditos y búsquedas de prueba.
