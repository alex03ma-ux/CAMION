# Guía de prueba manual: prepublicación (P9)

Para la responsable del portal. Explica qué cambió en P9 y cómo comprobarlo a mano, paso a paso.

Estado: rama `feature/p9-prepublicacion`, pendiente de unir a `main`. Las pruebas automáticas están **PROBADAS** (1481 de la API y 875 de pantallas, según `docs/criterios/P9-evidencia.md`). La prueba manual, la prueba con la API reiniciada en la rama, la revisión en el celular (375 px) y las pruebas E2E **no se han ejecutado**. P9 **no tiene migración**: no cambia la estructura de `camion_ventas`.

Nota (2026-11-24): las direcciones de esta guía ya están escritas sin `#`, como quedaron con P10 (por ejemplo `/terminos`). Los enlaces viejos con `#/` siguen funcionando porque el portal los convierte solo.

Los textos legales siguen en revisión del abogado (D-44). Por eso las páginas de términos y de privacidad muestran la franja "En revisión legal" y no traen el texto de los borradores. Eso es lo esperado.

## 0. Antes de empezar: reiniciar la API con la rama

La API que está en marcha es una versión anterior a P9. Si no la reinicia, las pantallas nuevas fallan o no aparecen.

1. Abra la pestaña de la terminal donde corre la **API** y pulse `Ctrl+C` para detenerla.
2. Compruebe que está en la rama de P9:
   ```powershell
   git branch --show-current
   ```
   Debe decir `feature/p9-prepublicacion`.
3. Vaya a la carpeta de la API, compile y arranque el dll:
   ```powershell
   cd backend\Camion.Api
   dotnet build
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   $env:ASPNETCORE_URLS = "http://localhost:5080"
   dotnet bin\Debug\net8.0\Camion.Api.dll
   ```
4. Abra http://localhost:5080/api/portal/contacto en el navegador. Debe aparecer el campo `"empresa": null`. Si no aparece, la API sigue siendo la versión anterior: repita los pasos 1 a 3.
5. Si el frontend no está en marcha, arránquelo en otra pestaña (`cd frontend` y `npm run dev`).
6. Abra http://localhost:5173 y recargue con **`Ctrl+Shift+R`** para que el navegador no use la versión guardada.

Use solo datos ficticios en las pruebas (por ejemplo, un correo como `prueba-p9@ejemplo.com` y una contraseña de prueba). Las cuentas que cree quedan en la base local, que es solo para pruebas (D-80 A).

## 1. Registro con la casilla de autorización

1. Sin iniciar sesión, entre en "Mi cuenta" y luego en el registro (`/cuenta/registro`).
2. Con "Soy comprador" elegido, revise que, antes del botón "Crear cuenta", hay una **casilla sin marcar** que dice "Autorizo el tratamiento de mis datos personales según la Política de tratamiento de datos."
3. Revise que junto a la casilla hay dos enlaces: "Política de tratamiento de datos" y "Términos y condiciones". Ábralos: deben abrir en una pestaña nueva.
4. Llene todos los datos con valores ficticios, **sin marcar la casilla**, y pulse "Crear cuenta".
   - Esperado: no se crea la cuenta y bajo la casilla aparece "Debes autorizar el tratamiento de tus datos para crear tu cuenta."
5. Marque la casilla. El mensaje debe desaparecer. Desmárquela: el mensaje vuelve.
6. Cambie a "Soy asesor". La casilla conserva el estado que tenía y aparece antes del botón "Enviar solicitud".
7. Vuelva a "Soy comprador", marque la casilla y pulse "Crear cuenta".
   - Esperado: la cuenta se crea y entra como antes.
8. No hay casilla para "Acepto los términos". Es lo decidido (D-84 A): se añadirá cuando el abogado apruebe el texto.

Opcional: repita los pasos 6 y 7 como asesor. La solicitud queda pendiente de aprobación, como siempre.

## 2. Términos y privacidad

1. Abra `/terminos`, con y sin sesión.
   - Esperado: página "Términos y condiciones de uso" con la franja **"En revisión legal"**, un texto breve que explica que los términos se publicarán cuando termine la revisión, el contacto vigente del portal y un enlace "Ir al inicio".
2. Abra `/privacidad`.
   - Esperado: la misma franja "En revisión legal" y el contacto vigente.
3. En ninguna de las dos debe aparecer texto de los borradores, como "NOTA PARA REVISIÓN", "[RAZÓN SOCIAL]", "[NIT]" o "[DOMINIO]".
4. La pestaña del navegador debe decir "Términos y condiciones | Financiamos Tu Camión" y "Política de tratamiento de datos | Financiamos Tu Camión".

## 3. Pie del portal

1. Baje hasta el pie en la portada, con y sin sesión.
2. Junto al "©" deben verse los enlaces "Términos y condiciones" y "Política de tratamiento de datos". Se abren en la misma pestaña.
3. Mientras no haya datos de la empresa (punto 4), el pie **no** muestra razón social, NIT ni dirección.

## 4. Datos de la empresa (administradora)

1. Inicie sesión como administradora y entre en "Parámetros" (`/cuenta/parametros`).
2. Debajo de "Datos de contacto" está la sección **"Datos de la empresa"**, con el aviso "Se muestran en el pie del portal solo cuando los tres están completos." y el estado "no se muestran en el portal (faltan datos)".
3. Escriba una razón social ficticia, por ejemplo `Empresa de Prueba SAS`, y en NIT el valor **no válido** `800197268-5`. Pulse "Guardar datos de la empresa".
   - Esperado: no se guarda y bajo el NIT aparece "Escribe el NIT con su dígito de verificación, por ejemplo 900.123.456-7." Lo escrito se conserva.
4. Cambie el NIT por el valor **válido** `800.197.268-4` (es el NIT público de la DIAN, sirve solo como ejemplo). Deje la dirección vacía y guarde.
   - Esperado: "Datos de la empresa guardados." El estado sigue diciendo que no se muestran (falta la dirección). El pie sigue sin datos de la empresa.
5. Escriba una dirección de notificaciones ficticia (al menos 5 caracteres) y guarde.
   - Esperado: el estado pasa a "se muestran en el pie del portal" y aparece la fecha y quién hizo el último cambio.
6. Recargue la portada con `Ctrl+Shift+R`. El pie muestra "Empresa de Prueba SAS · NIT 800.197.268-4 · Dirección de notificaciones: …".
7. Abra `/terminos` y `/privacidad`: muestran los mismos datos como "Responsable".
8. Al terminar, **vacíe los tres campos y guarde**, para que no queden datos ficticios en el pie. El pie vuelve a no mostrar nada de la empresa.

Los datos reales los entrega usted cuando el abogado confirme cuáles usar (D-44, D-83 A). Mientras estén vacíos, no se muestra nada.

## 5. WhatsApp vigente en los mensajes

1. En "Parámetros", anote el WhatsApp actual del portal.
2. Cámbielo por otro número de prueba y guarde.
3. Pida un crédito desde la portada con datos ficticios. En la confirmación, el botón de WhatsApp debe abrir una conversación con el **número nuevo**.
4. Inicie sesión con la cuenta de comprador del punto 1 y abra "Mis solicitudes". El texto "escríbenos por WhatsApp al …" debe mostrar el **número nuevo**.
5. Vuelva a "Parámetros" y deje el **número original**. Compruebe en el pie que se ve el número de siempre.

Los mensajes de fallo (cuando un envío no se puede hacer) también usan el número vigente. Esos casos son difíciles de provocar a mano y están cubiertos por las pruebas automáticas.

## 6. Límite de intentos de inicio de sesión

Haga este punto **al final**, porque durante unos 15 minutos no podrá iniciar sesión desde este equipo.

1. Cierre la sesión.
2. En el inicio de sesión, use un correo que **no exista**, por ejemplo `no-existe-p9@ejemplo.com`, y cualquier contraseña. Así no se bloquea ninguna cuenta real.
3. Pulse "Iniciar sesión" 21 veces seguidas. Las primeras 20 dan el error normal de datos incorrectos.
   - Esperado: a partir de la 21, el mensaje "Hiciste muchos intentos de inicio de sesión. Espera unos minutos e intenta de nuevo."
4. Espere unos 15 minutos y vuelva a iniciar sesión con su cuenta.

El límite del registro (5 por hora y 20 al día) no se prueba a mano porque crearía varias cuentas en la base. Está cubierto por las pruebas automáticas.

## 7. Página no encontrada

1. Escriba en la barra de direcciones `http://localhost:5173/esta-pagina-no-existe`.
   - Esperado: "Página no encontrada", con el texto "La página que buscas no existe o cambió de dirección." y los enlaces "Ir al inicio" y "Ver todos los vehículos". El encabezado y el pie se ven como siempre.
2. Pulse "Ir al inicio": vuelve a la portada.
3. Escriba solo `http://localhost:5173/`: debe mostrar la portada, no la página de error.

## 8. Favicon y robots.txt

1. Mire la pestaña del navegador: debe verse el **icono rojo** del portal en lugar del icono genérico. Si no aparece, recargue con `Ctrl+Shift+R`; algunos navegadores guardan el icono anterior.
2. Abra http://localhost:5173/robots.txt. Debe mostrar unas pocas líneas que bloquean `/api/` y `/health`.

## 9. Qué anotar

Para cada punto, anote "bien" o qué vio distinto (con una captura si puede). Si algo falla, indique el punto y el paso. Con eso, P9 queda listo para abrir la PR.

## 10. Lo que P9 no resuelve

Siguen pendientes para publicar en internet:
- **D-81**: dónde se publica (hosting y dominio).
- **D-44**: la revisión del abogado de la política y de los términos, y los datos reales de la empresa. Después se quita la franja "En revisión legal" y se cambia la versión de la política.
- La **puesta en marcha del servidor** según `docs/GUIA-PUBLICACION.md`, incluida la configuración del proxy para que el límite de intentos funcione bien (M-1 de la revisión de seguridad).
- **QA-P9-1**: la descripción para buscadores tiene 172 caracteres y Google suele cortar hacia los 160. Puede mantenerla o aprobar una más corta.
