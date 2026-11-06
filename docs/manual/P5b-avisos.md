# Guía rápida: avisos "Vende tu camión" y "Compra tu seguro todo riesgo" (P5b)

Para la responsable del portal. Explica cómo llegan los dos avisos, quién los ve, cómo se aplica la migración 014 y cómo hacer la prueba manual.
Estado: rama `feature/p5b-avisos`, EN REVISIÓN. Pruebas automáticas PROBADAS (1029/1029 de API y 576/576 de pantallas). **La migración 014 NO está aplicada; sin ella la API de esta rama no debe arrancarse contra `camion_ventas`.** La prueba manual está pendiente.

## 1. Cómo envía un aviso un visitante

- **Dónde los encuentra:** en la portada, debajo de la banda de crédito, hay dos bloques: "¿Quieres vender tu camión?" (botón "Vende tu camión") y "Asegura tu vehículo todo riesgo" (botón "Compra tu seguro"). También están en el pie de página, en la columna **"Servicios"**.
- **"Vende tu camión"** pide: nombre, teléfono WhatsApp, correo (opcional) y la descripción del vehículo (hasta 500 caracteres; un contador muestra cuántos lleva).
- **"Compra tu seguro todo riesgo"** pide: nombre, teléfono, correo (opcional) y el tipo de vehículo (Camión, Carro, Maquinaria u Otro).
- En los dos, la casilla de autorización de datos empieza sin marcar y es obligatoria.
- No hace falta cuenta. Si es un comprador con sesión iniciada, su nombre y teléfono aparecen ya escritos y igual debe marcar la casilla.
- Al enviar sale: "¡Listo! Recibimos tu solicitud. Te contactaremos pronto por teléfono o WhatsApp."
- Si la misma persona vuelve a enviar el mismo tipo de aviso mientras el anterior sigue abierto, no se crea uno nuevo: se agrega al historial del que ya existe.
- "Vende tu camión" **no crea** ningún vehículo ni publicación; solo avisa a la administración.

## 2. Quién los ve

- **Solo el administrador**, en **"Contactos"**. Siempre quedan a cargo de Administración.
- **No se pueden reasignar a un asesor** (D-52): en los avisos no aparece el botón "Reasignar".
- Los asesores nunca los ven en "Mis contactos".
- El comprador no tiene una lista de sus avisos por ahora; la pantalla "Mis solicitudes" llegará con P6 (D-53).

## 3. Cómo se atienden en "Contactos"

- En la lista, cada fila dice su **origen**: "Contacto de vehículo", "Vende tu camión" o "Seguro todo riesgo". El filtro **"Origen"** deja ver solo un tipo.
- Al abrir un aviso se ven los datos de la persona (no se pueden editar), la descripción o el tipo de vehículo, el texto "Sin vehículo asociado", el historial y la autorización de datos.
- **"Guardar nota"**: de 5 a 500 caracteres.
- **"Cerrar contacto"**: además de los resultados de siempre, los avisos tienen **"Solicitud atendida"**. "Se concretó la venta" y "El vehículo ya no está disponible" no se ofrecen en los avisos.
- **"Reabrir"**: vuelve a "Pendiente de gestión", con un motivo.

## 4. Cómo aplicar la migración 014

Hágalo con el coordinador acompañando. Detalle técnico completo en `docs/coordinacion/REVISION-SEGURIDAD-P5b.md`.

1. **Hacer el respaldo con un nombre nuevo.**
   - Abra una consola en `C:\xampp\mysql\bin`.
   - Revise con `dir C:\Respaldo_CAMION\` que el nombre que va a usar no exista. Nunca sobrescriba ni borre un respaldo.
   - Ejecute (cambie `AAAAMMDD_HHMM` por la fecha y la hora de hoy):
     `mysqldump -u root -p --single-transaction --routines --triggers --databases camion_ventas --result-file=C:\Respaldo_CAMION\camion_ventas_antes_014_AAAAMMDD_HHMM.sql`
   - Le pedirá la contraseña de MariaDB; escríbala usted, no la pegue en el chat.
   - Use `--result-file` y no `>`: la redirección de PowerShell guarda el archivo en otro formato (UTF-16).
   - Compruebe que el archivo pesa más de 0 bytes y que termina con `-- Dump completed`. Anote el nombre, el tamaño y la hora para la bitácora.
2. **Detener la API.** En la terminal donde está corriendo, pulse `Ctrl+C` (o cierre esa terminal).
3. **Antes de importar**, el coordinador revisa la base con consultas de solo lectura y anota los conteos.
4. **Importar en phpMyAdmin.**
   - En la columna de la izquierda, haga clic primero en la base **`camion_ventas`**. Es obligatorio: el archivo no dice a qué base va.
   - Vaya a **Importar**, elija **solo** `database/migraciones/014_p5b_avisos_SUBIR.sql` y pulse **Importar** (o "Continuar").
   - Si sale un error, **no lo vuelva a importar**: avise al coordinador, que revisará qué parte se aplicó.
5. **Verificar.**
   - Usted puede mirar, en `camion_ventas` › tabla `leads` › **Estructura**, que aparezcan las columnas `origen` y `tipo_vehiculo`.
   - El coordinador ejecuta la verificación completa: los mismos conteos que antes, todos los contactos existentes con origen "CONTACTO_VEHICULO" y las reglas nuevas presentes.
6. Con la verificación correcta, el coordinador arranca la API de esta rama.

## 5. Prueba manual (después de aplicar la 014)

Use solo datos inventados (por ejemplo, nombre "Prueba Aviso" y un teléfono de prueba). Ojo: desde un mismo equipo se admiten 5 envíos cada 10 minutos, sumando contactos y avisos; si sale "Has enviado muchas solicitudes", espere unos minutos.

1. **"Vende tu camión" sin cuenta.** Sin iniciar sesión, en la portada pulse "Vende tu camión". Escriba nombre, teléfono y una descripción, marque la casilla y pulse "Enviar". Debe salir "¡Listo!".
2. **Seguro sin cuenta.** Desde la columna "Servicios" del pie, abra "Compra tu seguro todo riesgo", elija un tipo de vehículo, marque la casilla y envíe.
3. **Con cuenta.** Entre como comprador y envíe los dos avisos otra vez (con un teléfono distinto). Compruebe que su nombre y teléfono aparecen ya escritos y que igual le pide marcar la casilla.
4. **Como administradora, en "Contactos".** Use el filtro **"Origen"**: con "Vende tu camión" deben verse solo esos avisos, y con "Seguro todo riesgo", solo los del seguro.
5. **Nota.** Abra un aviso y guarde una nota.
6. **Cerrar.** Ciérrelo con el resultado **"Solicitud atendida"**.
7. **Reabrir.** Reábralo escribiendo un motivo. Debe volver a "Pendiente de gestión".
8. **Sin "Reasignar".** Compruebe que en ningún aviso aparece el botón "Reasignar" (en un contacto de vehículo sí debe seguir apareciendo).
9. **Como el asesor Xavi.** Cierre sesión y entre con la cuenta de Xavi Quintero. En "Mis contactos" **no** debe ver ninguno de los avisos de la prueba.
10. Cuente al coordinador lo que salió bien y lo que no, para anotarlo en la bitácora.

## 6. Antes de publicar en internet

- INF-2 (límite por IP detrás del proxy y actualización de MySqlConnector).
- Revisión legal de la política de datos (D-44).
- Limpiar los avisos de prueba de `camion_ventas`.
