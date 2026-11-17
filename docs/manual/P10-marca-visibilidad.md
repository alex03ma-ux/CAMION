# Guía de prueba manual: marca y visibilidad (P10)

Para la responsable del portal. Explica qué cambió en P10 y cómo comprobarlo a mano, paso a paso.

Estado: rama `feature/p10-marca-visibilidad`, pendiente de unir a `main`. Las pruebas automáticas están **PROBADAS** (frontend 1025/1025; backend 1557 en la corrida completa más las 52 pruebas de P10 con el `index.html` real, según `docs/criterios/P10-evidencia.md` §6). La prueba manual, la prueba contra la API local, la revisión en el celular (375 px) y las pruebas E2E **no se han ejecutado**. P10 **no tiene migración**: no cambia la estructura de `camion_ventas`.

Qué cambia para quien usa el portal:
- el logo de **VCA Automotores** y el nombre **"Venta de Camiones – Financiamos tu Camión"** (D-87);
- el título grande de la portada es **"Venta de camiones usados"** (D-92);
- las direcciones ya **no llevan `#`**: por ejemplo http://localhost:5173/camiones o http://localhost:5173/cuenta. Cada vehículo tiene su propia dirección, que se puede recargar y compartir;
- el mensaje de WhatsApp de la ficha lleva la dirección del vehículo.

Google **todavía no puede indexar** el portal: está apagado a propósito hasta abrirlo al público (D-89 A). Por eso todas las páginas llevan la marca `noindex` (paso 10). Eso es lo esperado.

## 0. Antes de empezar: reiniciar la API y el frontend con la rama

1. En la pestaña de la terminal donde corre la **API**, pulse `Ctrl+C` para detenerla.
2. Compruebe que está en la rama de P10:
   ```powershell
   git branch --show-current
   ```
   Debe decir `feature/p10-marca-visibilidad`.
3. Compile y arranque la API:
   ```powershell
   cd backend\Camion.Api
   dotnet build
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   $env:ASPNETCORE_URLS = "http://localhost:5080"
   dotnet bin\Debug\net8.0\Camion.Api.dll
   ```
4. En la pestaña del **frontend**, pulse `Ctrl+C` y vuelva a arrancarlo (`cd frontend` y `npm run dev`). Hace falta porque cambió la configuración de Vite.
5. Abra http://localhost:5173/robots.txt. Debe mostrar dos líneas: `User-agent: *` y `Disallow: /`. Si sale otra cosa o la portada, la API o el frontend siguen siendo los anteriores: repita los pasos 1 a 4.
6. Abra http://localhost:5173/ y recargue con **`Ctrl+Shift+R`** para que el navegador no use la versión guardada.

Use solo datos ficticios. Si en algún paso ve una página en blanco, anote la dirección exacta y avise.

## 1. Logo y favicon, en escritorio y a 375 px

**En escritorio** (ventana del navegador maximizada):
1. Abra http://localhost:5173/.
2. Arriba a la izquierda debe verse el **logo horizontal de VCA Automotores** ("Venta de Camiones", "Financiamos tu Camión"), nítido y sin bordes blancos raros. Ya no debe aparecer el logo anterior de texto con el camión dibujado.
3. Pulse el logo: debe llevarla a la portada.
4. Baje hasta el pie (fondo oscuro). Debe verse el **logo cuadrado** de VCA Automotores, legible sobre el fondo oscuro. Abajo debe decir "© <año> VCA Automotores".
5. Mire la pestaña del navegador: el ícono (favicon) debe ser la **cabeza del toro** del logo. Si sigue el ícono viejo, abra la misma dirección en una ventana de incógnito (`Ctrl+Shift+N`): el navegador guarda el favicon mucho tiempo.
6. El texto de la pestaña debe decir "Venta de camiones usados y crédito | Financiamos tu Camión".

**A 375 px (tamaño de celular):**
1. Pulse `F12` para abrir las herramientas del navegador y luego `Ctrl+Shift+M` (vista de dispositivo).
2. Arriba, escriba **375** en el ancho (o elija un modelo de celular de ese ancho).
3. Recargue con `Ctrl+Shift+R`.
4. El logo debe verse más pequeño, **sin salirse** del encabezado y sin tapar el menú ni la búsqueda. El menú y la búsqueda deben poder usarse.
5. Baje al pie: el logo cuadrado debe verse completo.
6. Si puede, repita con 320 y 768 px. Al terminar, vuelva a pulsar `Ctrl+Shift+M` y cierre con `F12`.

## 2. Título principal (h1) de la portada

1. Abra http://localhost:5173/.
2. En la parte de arriba de la portada, el título grande debe decir **"Venta de camiones usados"**.
3. Debajo debe seguir la frase de siempre, con el mismo aspecto que antes.
4. Entre en http://localhost:5173/cuenta: el antetítulo debe decir "VCA AUTOMOTORES" y el título "Venta de Camiones".

## 3. Categorías

1. En el encabezado (o en el pie), pulse **Camiones**. La dirección debe ser http://localhost:5173/camiones, sin `#`.
2. La pestaña debe decir "Camiones en venta en Colombia | Financiamos tu Camión", y deben verse solo camiones.
3. Repita con las otras categorías, escribiendo la dirección en la barra:
   - http://localhost:5173/carros — "Carros en venta en Colombia | Financiamos tu Camión";
   - http://localhost:5173/carrocerias — "Carrocerías en venta para camión | Financiamos tu Camión";
   - http://localhost:5173/maquinaria — "Maquinaria amarilla en venta | Financiamos tu Camión".
4. Si una categoría no tiene vehículos publicados, debe verse el aviso "Por ahora no hay vehículos publicados en esta categoría." (no "Página no encontrada").
5. Abra http://localhost:5173/vehiculos: debe verse el catálogo completo.
6. Abra http://localhost:5173/Camiones (con C mayúscula): debe verse "Página no encontrada". Es lo esperado.

## 4. Recargar (F5) en una ficha

1. Desde la portada o el catálogo, abra cualquier vehículo.
2. La dirección debe tener la forma http://localhost:5173/vehiculo/marca-linea-año-número, sin `#`.
3. La pestaña debe decir algo como "Chevrolet NPR 2018 en Medellín | Financiamos tu Camión" (marca, línea, año y ciudad del vehículo; si el título es muy largo, sin la parte "| Financiamos tu Camión").
4. Pulse **F5**. La misma ficha debe volver a aparecer, sin error ni página en blanco.
5. Copie la dirección, abra una pestaña nueva, péguela y pulse Enter: debe abrir la misma ficha.
6. Haga lo mismo con sesión de administrador en http://localhost:5173/cuenta/usuarios: al pulsar F5 debe seguir en "Usuarios".

## 5. Atrás y Adelante

1. Abra http://localhost:5173/.
2. Vaya al catálogo y elija un filtro (por ejemplo, una **Marca**).
3. Abra un vehículo de la lista.
4. Pulse **Atrás** en el navegador: debe volver a la lista **con el mismo filtro** elegido.
5. Pulse **Atrás** otra vez: debe volver a la portada.
6. Pulse **Adelante** dos veces: debe volver a la lista con el filtro y luego a la ficha.
7. En ningún paso la página debe recargarse por completo ni quedar en blanco.
8. Con la tecla `Ctrl` pulsada, haga clic en un vehículo: debe abrirse en una **pestaña nueva** y la pestaña actual no debe cambiar.

## 6. Enlaces viejos con `#`

Los enlaces que ya se compartieron antes de P10 deben seguir sirviendo. Copie cada dirección en la barra y pulse Enter:

1. http://localhost:5173/#/cuenta — la dirección debe cambiar sola a http://localhost:5173/cuenta y mostrar "Mi cuenta".
2. http://localhost:5173/#/buscar — debe cambiar a http://localhost:5173/vehiculos y mostrar el catálogo.
3. http://localhost:5173/#/vende-tu-camion — debe cambiar a http://localhost:5173/vende-tu-camion.
4. http://localhost:5173/#/no-existe — debe mostrar "Página no encontrada".
5. http://localhost:5173/#//otro-dominio — debe llevar a la **portada**, sin dejar la página en blanco y sin salir del portal (corrección B-1).

## 7. Enlace de recuperación nuevo

Use una cuenta de prueba de comprador o de asesor (el panel no genera enlaces para administradores). **No pegue el enlace en chats ni en documentos**: sirve para cambiar la contraseña de esa cuenta.

1. Entre como administrador en http://localhost:5173/cuenta/usuarios.
2. En la cuenta de prueba, pulse **"Generar enlace de recuperación"**.
3. Mire el enlace que aparece: debe tener la forma `http://localhost:5173/restablecer-contrasena#codigo=<código>`. Ya no debe llevar `/#/` ni `?codigo=`.
4. Copie el enlace y ábralo en una ventana de incógnito (`Ctrl+Shift+N`).
5. Debe abrir la pantalla para definir la contraseña nueva, y la barra de direcciones debe quedar en http://localhost:5173/restablecer-contrasena, **sin el código**.
6. Opcional: escriba una contraseña de prueba y guárdela. Debe funcionar como en P8. El enlace sirve una sola vez y vence en 60 minutos.

Los enlaces generados antes de P10 (con `/#/restablecer-contrasena?codigo=`) siguen sirviendo mientras no venzan.

## 8. Mensaje de WhatsApp de la ficha

1. Abra cualquier vehículo y pulse **"Escribir por WhatsApp"**.
2. Se abre WhatsApp (web o la aplicación) con un mensaje escrito. Debe decir:
   "Hola, me interesa el vehículo <título del vehículo> que vi en Financiamos tu Camión: http://localhost:5173/vehiculo/<dirección del vehículo>"
3. Revise que la dirección del final es la misma que se ve en la barra del navegador.
4. **No hace falta enviarlo.** Cierre WhatsApp sin enviar.

En el equipo local la dirección empieza por `http://localhost:5173`, que solo funciona en su computador. Con el portal publicado empezará por el dominio del portal.

## 9. Vista previa al compartir por WhatsApp: todavía no se puede probar

Al pegar el enlace de un vehículo en WhatsApp, lo normal es que aparezca una tarjeta con la foto, el nombre y la descripción. En P10 eso ya está programado (D-88 A), pero **solo se puede probar con el portal publicado en su dominio** (D-81): WhatsApp y Facebook no pueden leer `localhost`, que solo existe en su computador. Se probará después de publicar, junto con los validadores de Google (CA-P10-69 y 81).

## 10. Ver el código fuente con `noindex`

1. Abra cualquier vehículo y pulse `Ctrl+U` (ver código fuente). Se abre una pestaña con el texto de la página.
2. Pulse `Ctrl+F` y busque `noindex`.
   - Esperado: aparece una línea `<meta name="robots" content="noindex" …>`. Significa que Google no debe indexar la página, que es lo decidido mientras el portal no esté abierto al público (D-89 A).
3. Repita en la portada http://localhost:5173/ y en http://localhost:5173/camiones: también debe aparecer `noindex`.
4. Busque `og:title`: debe aparecer con el título del portal. En el equipo local el código fuente es igual en todas las páginas; las etiquetas de cada vehículo (foto y título) las pone la API solo en el servidor publicado.
5. Abra http://localhost:5173/sitemap.xml: debe decir "No encontrado." porque la indexación está apagada. Es lo esperado.

## Qué anotar

Para cada sección: "bien" o lo que vio distinto, con la dirección y, si puede, una captura. En especial:
- si el logo se ve borroso, cortado o se sale del encabezado a 375 px;
- si alguna dirección muestra una página en blanco;
- si el texto del mensaje de WhatsApp le parece adecuado (cambia un poco el texto de D-47).
