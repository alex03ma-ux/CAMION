# Guía rápida: reportes, CSV, indicadores, actividad y recuperación de contraseña (P8)

Para la responsable del portal. Explica qué hay de nuevo, qué cuenta cada cifra, cómo abrir el CSV en Excel, cómo ayudar a alguien que olvidó su contraseña y cómo hacer la prueba manual (CA-P8-75).

Estado: rama `feature/p8-reportes`, EN REVISIÓN (PR por abrir). Las pruebas automáticas están PROBADAS: 1328/1328 de API y 750/750 de pantallas. **P8 no tiene migración**: no cambia la base `camion_ventas`. La prueba manual, la revisión en el celular (375 px) y las pruebas E2E **no se han ejecutado**.

## 0. Antes de empezar: la carpeta de claves (importante)

La primera vez que arranque la API con esta versión, el sistema crea sola la carpeta **`C:\CAMION_claves`**. Ahí guarda las claves con las que firma los enlaces de recuperación de contraseña.

- **No borre esa carpeta.** Si se borra, los enlaces que estén pendientes dejan de servir (basta con generar uno nuevo). Las sesiones abiertas no se ven afectadas.
- **No la suba a Git ni la copie dentro de la carpeta del proyecto.** Si la carpeta queda dentro del proyecto, la API no arranca a propósito.
- Las claves quedan protegidas para su usuario de Windows en este equipo. Si la API pasa a otro equipo, se crean claves nuevas.
- Cuando el portal se publique en internet, el equipo técnico se encarga de la carpeta en el servidor (INF-2).

## 1. Qué hay de nuevo

**Para la administradora** (en "Mi cuenta"; la barra del administrador ahora tiene **Reportes** e **Indicadores**):
- **Reportes:** cinco reportes con filtros y descarga en CSV.
- **Indicadores:** las cifras comerciales del período, comparadas con el período anterior, con valores en pesos y cuatro gráficos.
- **Actividad:** quién hizo qué y cuándo con un vehículo o con una suscripción. Solo se puede mirar; no se puede cambiar ni borrar.
- **Generar enlace de recuperación:** para ayudar a un asesor o comprador que olvidó su contraseña.

**Para el asesor:**
- **Mis indicadores:** sus propias cifras, solo en pantalla.

Ningún reporte, indicador, CSV ni actividad muestra nombres, teléfonos, correos ni mensajes de los clientes. Para contactar a un cliente se usa la bandeja de "Contactos", como siempre.

## 2. Reportes

Entre en "Mi cuenta" › **Reportes**. Arriba están las pestañas de los cinco reportes:

| Reporte | Qué muestra |
|---|---|
| **Vehículos y publicaciones** | Vehículos de hoy por estado y por categoría; vehículos registrados en el período; publicaciones por estado, visibles hoy, destacadas hoy y eliminadas; los 10 vehículos con más contactos en el período. |
| **Solicitudes** | Contactos, avisos ("Vende tu camión" y seguro), créditos y búsquedas recibidos por origen; cuántos siguen abiertos y cuántos se cerraron; cerrados por resultado; tiempo medio hasta el primer cierre; contactos a cargo de Administración y de cada asesor, reasignados a mano o que pasaron solos a Administración. |
| **Créditos** | Créditos por estado (Registrado, En gestión, Cerrado), si se pidieron desde la portada o desde una ficha, cerrados por resultado y el porcentaje remitido a entidad financiera. No hay montos ni bancos: el portal no los guarda. |
| **Ventas** | Ventas vigentes con cantidad y valor vendido, por origen, por categoría y por asesor. Las revertidas se cuentan aparte y no suman. |
| **Suscripciones y pagos** | Suscripciones por estado y por plan; asignadas, renovadas y vencidas en el período; pagos recibidos por medio, por plan y por concepto. Los pagos anulados se cuentan aparte. |

El reporte de pagos dice: "Pagos registrados a mano por la administración. No es contabilidad ni facturación."

**Cómo usar los filtros:**
1. Al abrir, el reporte muestra **el mes en curso**. El período aplicado se ve en pantalla.
2. Cambie "Desde" y "Hasta" y, si quiere, los filtros propios del reporte (asesor, categoría, departamento y municipio, origen, estado, resultado, responsable, canal, plan o medio de pago).
3. Pulse **Aplicar filtros**. Se cuentan solo las filas que cumplen todos los filtros a la vez.
4. **Limpiar filtros** vuelve al mes en curso, sin filtros.

Reglas: el período no puede pasar de 366 días, y "Desde" no puede ser posterior a "Hasta". Si algo está mal, el mensaje aparece junto al campo. Si no hay datos, sale "No hay datos para estos filtros.".

## 3. Descargar el CSV y abrirlo en Excel

1. Con el reporte en pantalla y los filtros que quiere, pulse **Descargar CSV**.
2. El archivo se guarda en la carpeta de descargas con un nombre como `reporte-ventas-2026-11-01_2026-11-30.csv`.
3. Ábralo con **doble clic**: Excel en español lo abre con las columnas separadas y con las tildes y la ñ bien escritas.
4. Si Excel lo muestra todo en una sola columna, ábralo desde Excel con **Datos › Desde el texto/CSV**, elija origen "UTF-8" y delimitador "Punto y coma".

Qué trae el archivo: el título y el período, los filtros aplicados, las mismas cifras que ve en pantalla y, al final, una fila por elemento. Los valores en pesos van sin puntos de miles.

Tenga en cuenta:
- Un texto que empieza con `=`, `+`, `-` o `@` aparece con un apóstrofo delante (`'`). Es a propósito, para que Excel no lo tome como fórmula.
- Si el reporte tiene más de 10.000 filas, no se descarga y sale "El reporte tiene demasiadas filas. Acorta el período o añade filtros.".
- Cada descarga queda registrada: quién, cuándo, qué reporte, qué filtros y cuántas filas. El contenido no se guarda.
- El archivo no lleva datos personales de clientes, pero trata de su negocio: guárdelo en un lugar seguro y no lo reenvíe sin necesidad.
- El asesor no puede descargar reportes.

## 4. Indicadores (administradora)

Entre en "Mi cuenta" › **Indicadores** (o en **Inicio**, enlace "Ver indicadores"). Elija el período con "Desde" y "Hasta" y pulse **Aplicar**; **Mes en curso** vuelve al mes actual.

Para el período verá:
- contactos recibidos;
- porcentaje de contactos cerrados con "Venta concretada";
- ventas vigentes y valor vendido;
- solicitudes de Administración recibidas (avisos, créditos y búsquedas), también por origen;
- créditos remitidos a entidad financiera;
- ingresos registrados por planes (pagos registrados, sin los anulados);
- tiempo medio hasta el cierre;
- publicaciones visibles hoy.

Cada cifra se compara con el **período anterior** de la misma duración, por ejemplo "+3 frente al período anterior". Cuando no hay con qué calcular un porcentaje, dice "Sin datos".

Debajo hay cuatro gráficos de barras: ventas por mes (últimos 12 meses), ventas por origen, solicitudes por origen y contactos por resultado. Cada gráfico tiene su tabla con los mismos números.

El panel **Inicio** sigue igual que en P7: solo números operativos, sin dinero. No hay datos de visitas al portal (D-76).

## 5. Mis indicadores (asesor)

El asesor entra en "Mi cuenta" › **Mis indicadores**. Elige el período igual que arriba y ve:
- sus vehículos por estado y sus publicaciones visibles hoy;
- los contactos recibidos, cuántos siguen abiertos y cómo se cerraron;
- sus ventas vigentes, con cantidad y valor.

Solo cuenta los vehículos que hoy son suyos y los contactos que hoy atiende. No ve avisos, créditos, búsquedas, pagos ni cifras de otros asesores, y no puede descargar nada.

## 6. Actividad

- **De un vehículo:** abra el vehículo en el inventario y, en la sección **Actividad**, pulse **Ver actividad**. Verá los eventos del vehículo, de sus publicaciones y de sus ventas, del más reciente al más antiguo: fecha y hora de Colombia, quién lo hizo (o "Sistema") y qué hizo, en palabras, con el motivo cuando lo hay. En una edición se ven solo los campos que cambiaron, con su valor anterior y el nuevo.
- **De una suscripción:** en **Suscripciones**, pulse **Actividad** en la fila de la suscripción. Verá la asignación, las renovaciones, los cambios de plan, las suspensiones, la cancelación, los pagos registrados y anulados, y lo que se deshizo al anular un pago (D-67).

La lista muestra 20 eventos por página. La nota de reserva y las observaciones de venta no se muestran, porque pueden tener datos del comprador. El asesor no ve la Actividad, ni siquiera de sus propios vehículos.

## 7. Recuperación de contraseña asistida, paso a paso

Como el portal no envía correos (D-73), la administradora genera un enlace y se lo manda a la persona por WhatsApp.

**Lo que hace la administradora:**
1. La persona le escribe porque olvidó su contraseña. En el inicio de sesión hay un enlace "¿Olvidaste tu contraseña? Escríbenos por WhatsApp".
2. **Confirme que es quien dice ser**, por ejemplo llamándola al número registrado. Este paso es lo más importante: el enlace da acceso a la cuenta.
3. Entre en "Mi cuenta" › **Usuarios**, busque la cuenta y pulse **Generar enlace de recuperación**.
4. Lea el aviso y pulse **Generar enlace**.
5. Aparece el enlace con tres botones:
   - **Enviar por WhatsApp:** abre WhatsApp con el enlace listo para el celular registrado de la persona;
   - **Copiar:** si prefiere pegarlo usted en el chat (si no se puede copiar solo, el enlace queda seleccionado para copiarlo con Ctrl+C);
   - **Cerrar:** al cerrar, el enlace desaparece de la pantalla. No se guarda en ninguna parte.

**Reglas del enlace:**
- **Vence en 60 minutos.** La pantalla muestra la hora exacta.
- **Sirve una sola vez.**
- Si genera otro, el anterior deja de servir.
- También deja de servir si la cuenta cambia de estado (por ejemplo, si la bloquea) o pasa a ser de administrador.
- Solo se puede generar para cuentas **activas** que **no** son de administrador: en las demás el botón no aparece, y tampoco en su propia cuenta.
- Queda registrado quién lo generó, para quién y cuándo, pero nunca el enlace.

**Lo que hace la persona:**
1. Abre el enlace. Llega a la pantalla "Escribe tu contraseña nueva".
2. Escribe la contraseña nueva dos veces. Debe tener entre 8 y 128 caracteres, con al menos una mayúscula, una minúscula y un número.
3. Pulsa **Guardar contraseña nueva**.
4. Ve el mensaje de que la contraseña cambió. **Se cierran todas sus sesiones abiertas en todos los equipos**, y entra de nuevo con **Iniciar sesión**.

Si el enlace ya venció, ya se usó o fue reemplazado, sale "El enlace no es válido o ya venció. Pide uno nuevo al administrador.". Después de 5 intentos en 15 minutos, hay que esperar unos minutos.

**¿Y si la que olvida la contraseña es una administradora?** Hoy no hay forma dentro del portal: hace falta apoyo técnico. Está pendiente su decisión en **D-79** (`docs/coordinacion/DECISIONES.md`).

## 8. Prueba manual

Use solo cuentas y datos de prueba. No escriba contraseñas reales en el chat ni en documentos; donde haga falta, use una de prueba como `<CONTRASEÑA_DE_PRUEBA>`.

1. **Arranque y carpeta de claves.** El coordinador arranca la API de esta rama. Compruebe en el Explorador de Windows que existe `C:\CAMION_claves`. No la abra ni la borre.
2. **Reportes.** Como administradora, abra **Reportes**:
   - recorra las cinco pestañas con el mes en curso;
   - cambie el período y algún filtro, pulse "Aplicar filtros" y luego "Limpiar filtros";
   - escriba un período de más de 366 días y compruebe que avisa;
   - compare una cifra con su lista de siempre, por ejemplo las ventas del mes con "Ventas", o los contactos con "Contactos".
3. **CSV.** En **Ventas** (u otro reporte), pulse "Descargar CSV" y ábralo en Excel. Compruebe que las columnas salen separadas, que las tildes se ven bien, que las cifras coinciden con la pantalla y que no hay nombres, teléfonos ni correos de clientes.
4. **Indicadores.** Abra **Indicadores**, cambie el período y revise la comparación y los cuatro gráficos. Desde **Inicio**, pruebe "Ver indicadores".
5. **Actividad.** Abra un vehículo con historia (por ejemplo, uno reservado o vendido) y pulse "Ver actividad". En **Suscripciones**, pulse "Actividad" en la suscripción de prueba donde anuló un pago en P7 y compruebe que aparece el pago anulado y lo que se deshizo.
6. **Mis indicadores.** Entre como un asesor de prueba y abra "Mis indicadores". Compruebe que solo ve lo suyo y que no hay botón de descarga, ni Reportes, ni Indicadores, ni Actividad.
7. **Recuperación.** Como administradora, genere un enlace para una cuenta de prueba (asesor o comprador activo) y ábralo en otra ventana privada del navegador:
   - ponga la contraseña nueva de prueba y guarde;
   - compruebe que la sesión que esa cuenta tenía abierta se cerró;
   - vuelva a abrir el mismo enlace: debe decir que no es válido;
   - en "Usuarios", compruebe que las cuentas de administrador y las que no están activas no tienen el botón.
8. **Celular.** Si puede, repita los pasos 2, 4 y 7 en el celular o con la ventana estrecha (375 px).

Al terminar, cuéntele al coordinador qué revisó y qué no, para anotarlo en la bitácora.
