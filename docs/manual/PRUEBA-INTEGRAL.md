# Prueba integral del portal antes de publicar

Para la responsable del portal. Es un recorrido completo por el portal **tal como está hoy en `main`** (P0 a P10 e INF-1 a INF-4, hasta la PR #20), pensado para hacerlo en una o dos sesiones antes de publicar.

- Estado de esta guía: **DOCUMENTADO**. La responsable ejecutó la prueba integral el 2026-11-23/24 y marcó las casillas de abajo. Lo que el coordinador verificó en la base y lo que no se probó está en `docs/coordinacion/BITACORA.md` ("2026-11-23/24 — Prueba integral manual").
- Cada fase ya tiene su guía propia en `docs/manual/` (P5, P5b, P6, P7, P8, P9 y P10). Esta guía las junta en un solo recorrido y no las reemplaza: si un paso no queda claro, la guía de la fase tiene más detalle.
- Tiempo aproximado: sesión 1, recorridos 0 a 3 (unas 2 horas); sesión 2, recorridos 4 a 6 (unas 2 horas).

## Antes de empezar: cómo se usa esta guía

- Cada paso tiene una casilla `[ ]`. Márquela con una `x` (`[x]`) si salió como dice **Esperado**. Si no, déjela vacía y anote lo que vio (sección 6).
- **Dos ventanas del navegador:**
  - la **ventana normal**, con **tu cuenta de administradora**;
  - una **ventana de incógnito** (`Ctrl+Shift+N`), para el visitante sin cuenta, el comprador y el asesor de prueba. Entre un perfil y otro, cierre la sesión en esa ventana.
- **Cuentas de prueba:** en la guía creará dos cuentas nuevas con datos inventados:
  - comprador: `prueba.comprador@ejemplo.com`;
  - asesor: `prueba.asesor@ejemplo.com`.
  Invente usted las contraseñas. No las escriba en esta guía, en chats ni en documentos.
- **Datos inventados siempre:** nombres como "Prueba Integral", teléfonos de 10 dígitos que no sean de nadie conocido, placas y referencias de prueba (por ejemplo `PRUEBA-001`). No use datos bancarios ni documentos reales.
- **Límite de envíos:** desde un mismo equipo se admiten **5 envíos cada 10 minutos**, sumando contactos, avisos, créditos y búsquedas. Si sale "Has enviado muchas solicitudes. Intenta de nuevo en unos minutos.", es lo esperado: espere unos minutos y siga.
- La base local `camion_ventas` es solo de pruebas (D-80 A): lo que cree aquí se queda allí. El portal publicado usará una base nueva.

### Lo que no se puede probar en el equipo local

| Qué | Por qué | Cuándo se prueba |
|---|---|---|
| Vista previa (tarjeta con foto y título) al pegar el enlace de un vehículo en WhatsApp o Facebook | WhatsApp no puede leer `localhost`; hace falta el portal publicado en su dominio (D-81) | Después de publicar, con los validadores de Google (CA-P10-69 y 81) |
| Correo automático (aviso de aprobación al asesor, recuperación de contraseña por correo) | No existe servicio de correo (D-73). La recuperación la hace la administradora con un enlace (paso 4.19) | No aplica mientras no se decida otra cosa |

---

## 0. Preparación

- [x] **0.1** Abra el **Panel de control de XAMPP** y pulse **Start** en **MySQL**.
  **Esperado:** "MySQL" queda en verde.
- [x] **0.2** En una terminal, arranque la API desde `backend\Camion.Api` con `dotnet run`. Si Windows la bloquea, use la forma de la guía P7 §4 (`dotnet bin\Debug\net8.0\Camion.Api.dll`). Abra http://localhost:5080/health y http://localhost:5080/health/database.
  **Esperado:** las dos responden sin error.
- [x] **0.3** En otra terminal, desde `frontend`, ejecute `npm run dev`. Abra http://localhost:5173/ y recargue con **`Ctrl+Shift+R`**.
  **Esperado:** se ve la portada con el logo de VC Automotores arriba a la izquierda y el título "Venta de camiones usados".
- [x] **0.4** Abra la ventana de incógnito (`Ctrl+Shift+N`) en http://localhost:5173/.
  **Esperado:** la misma portada, sin sesión iniciada.

---

## 1. Visitante sin cuenta (ventana de incógnito)

- [x] **1.1** Mire la portada de arriba abajo.
  **Esperado:** vehículos destacados y recientes, la banda de crédito con "Quiero un crédito" y los bloques "¿Quieres vender tu camión?" y "Asegura tu vehículo todo riesgo".
- [x] **1.2** Baje hasta el pie.
  **Esperado:** el logo cuadrado, "© <año> VC Automotores", el WhatsApp, el correo y el horario del portal, la columna "Servicios" y los enlaces "Términos y condiciones" y "Política de tratamiento de datos".
- [x] **1.3** Pulse **Camiones** en el encabezado.
  **Esperado:** la dirección es http://localhost:5173/camiones y solo se ven camiones.
- [x] **1.4** Abra http://localhost:5173/carros, http://localhost:5173/carrocerias y http://localhost:5173/maquinaria.
  **Esperado:** cada una muestra su categoría. Si una no tiene vehículos, dice "Por ahora no hay vehículos publicados en esta categoría." (no "Página no encontrada").
- [x] **1.5** Use el buscador del encabezado con una marca que sepa que existe.
  **Esperado:** se abre http://localhost:5173/vehiculos con los resultados de esa marca.
- [x] **1.6** En los resultados, elija un filtro (por ejemplo, una **Marca** o una ciudad) y luego quítelo.
  **Esperado:** la lista cambia con el filtro y vuelve a la lista completa al quitarlo.
- [x] **1.7** Abra un vehículo.
  **Esperado:** la dirección tiene la forma http://localhost:5173/vehiculo/marca-linea-año-número; se ven fotos, precio, datos y el bloque "Contactar".
- [x] **1.8** Pulse **F5** en la ficha y luego **Atrás** en el navegador.
  **Esperado:** con F5 vuelve la misma ficha, sin página en blanco; con Atrás vuelve a la lista con el filtro que tenía.
- [x] **1.9** En la ficha, pulse **"Escribir por WhatsApp"**. No envíe el mensaje.
  **Esperado:** se abre WhatsApp con "Hola, me interesa el vehículo … que vi en Financiamos tu Camión: http://localhost:5173/vehiculo/…". La vista previa con foto **no se puede probar aquí** (ver arriba).
- [x] **1.10** En el formulario de contacto de la ficha, escriba nombre y teléfono inventados, **no marque la casilla** de autorización y pulse "Enviar".
  **Esperado:** no se envía y le pide autorizar el tratamiento de datos.
- [x] **1.11** Marque la casilla y pulse "Enviar".
  **Esperado:** "¡Listo! Recibimos tus datos. Te contactaremos pronto por teléfono o WhatsApp."
- [x] **1.12** Abra http://localhost:5173/vende-tu-camion (o el botón "Vende tu camión" de la portada). Escriba nombre, teléfono y una descripción, marque la casilla y envíe.
  **Esperado:** un contador muestra los caracteres (máximo 500) y sale "¡Listo! Recibimos tu solicitud…". No se crea ningún vehículo.
- [x] **1.13** Abra http://localhost:5173/seguro-todo-riesgo. Elija un tipo de vehículo, marque la casilla y envíe.
  **Esperado:** "¡Listo! Recibimos tu solicitud…".
- [x] **1.14** En la portada, pulse **"Quiero un crédito"** (http://localhost:5173/credito).
  **Esperado:** solo pide nombre, teléfono, correo (opcional) y comentario; no pide monto, ingresos ni documentos.
- [x] **1.15** Llénelo, marque la casilla y envíe. (Si salta el límite de envíos, espere unos minutos.)
  **Esperado:** "¡Listo!…" y un botón para escribir por WhatsApp al número del portal.
- [x] **1.16** Desde una ficha, pulse **"Solicitar crédito para este vehículo"**.
  **Esperado:** se abre http://localhost:5173/credito/… y el formulario muestra ese vehículo, sin poder cambiarlo. (No hace falta enviarlo.)
- [x] **1.17** En el catálogo, ponga filtros que no den resultados y pulse **"Busco un vehículo"** (también está en http://localhost:5173/busco-un-vehiculo). Elija una categoría, marque la casilla y envíe.
  **Esperado:** la categoría es obligatoria y al enviar sale "¡Listo!…".
- [x] **1.18** Abra http://localhost:5173/terminos y http://localhost:5173/privacidad.
  **Esperado:** las dos muestran la franja "En revisión legal" y el contacto del portal, sin textos de borrador como "[NIT]" o "NOTA PARA REVISIÓN".
- [x] **1.19** Abra http://localhost:5173/esta-pagina-no-existe y pulse "Ir al inicio".
  **Esperado:** "Página no encontrada" con los enlaces "Ir al inicio" y "Ver todos los vehículos"; el enlace la lleva a la portada.
- [x] **1.20** Pegue en la barra el enlace viejo http://localhost:5173/#/cuenta.
  **Esperado:** cambia solo a http://localhost:5173/cuenta y muestra "Mi cuenta".
- [x] **1.21** Abra http://localhost:5173/robots.txt.
  **Esperado:** dice `User-agent: *` y `Disallow: /`. Es lo esperado: Google no indexa el portal hasta abrirlo al público (D-89 A).

---

## 2. Comprador (ventana de incógnito)

- [x] **2.1** Abra http://localhost:5173/cuenta/registro con "Soy comprador" elegido.
  **Esperado:** antes de "Crear cuenta" hay una casilla **sin marcar** de autorización de datos, con enlaces a la política y a los términos.
- [x] **2.2** Llene los datos con `prueba.comprador@ejemplo.com`, un teléfono inventado y una contraseña inventada, **sin marcar la casilla**, y pulse "Crear cuenta".
  **Esperado:** no se crea y aparece "Debes autorizar el tratamiento de tus datos para crear tu cuenta."
- [x] **2.3** Marque la casilla y pulse "Crear cuenta".
  **Esperado:** la cuenta se crea al momento, **sin esperar aprobación**.
- [x] **2.4** Cierre la sesión (si entró sola) e inicie sesión con la cuenta nueva en http://localhost:5173/cuenta.
  **Esperado:** el perfil dice "Rol: Comprador", muestra "Mis solicitudes" y el aviso de que la sesión se cierra sola tras 30 minutos sin actividad.
- [x] **2.5** Abra la ficha de un vehículo.
  **Esperado:** el formulario de contacto trae ya su nombre y teléfono, y la casilla de autorización está sin marcar. No hay botones para editar el vehículo.
- [x] **2.6** Marque la casilla y envíe el contacto. Después pida un crédito desde http://localhost:5173/credito. (Si salta el límite, espere.)
  **Esperado:** los dos envíos dicen "¡Listo!…".
- [x] **2.7** Abra "Mis solicitudes" (http://localhost:5173/mis-solicitudes).
  **Esperado:** aparecen el contacto y el crédito del paso 2.6, con fecha, tipo y estado. **No** aparecen los envíos del recorrido 1 (se hicieron sin cuenta) ni notas internas.
- [x] **2.8** Revise su perfil.
  **Esperado:** no hay "Inventario", "Mis publicaciones", "Mis contactos" ni nada de administración.
- [x] **2.9** Escriba en la barra http://localhost:5173/inventario.
  **Esperado:** "No tienes permiso para realizar esta acción." El comprador no puede crear ni editar vehículos.
- [ ] **2.10** (Opcional, toma 30 minutos.) Deje la ventana quieta más de 30 minutos y vuelva a usarla.
  **Esperado:** la sesión se cerró sola y sale "Tu sesión terminó. Inicia sesión de nuevo."
- [x] **2.11** Cierre la sesión del comprador.
  **Esperado:** vuelve a la pantalla de inicio de sesión.

---

## 3. Asesor (ventana de incógnito, con pasos en la ventana de administradora)

- [x] **3.1** En la ventana de incógnito, abra http://localhost:5173/cuenta/registro y elija **"Soy asesor"**. Llene los datos con `prueba.asesor@ejemplo.com`, un celular inventado de 10 dígitos y datos ficticios, marque la casilla y pulse "Enviar solicitud".
  **Esperado:** "Solicitud enviada. Podrás iniciar sesión cuando el administrador apruebe tu cuenta."
- [x] **3.2** Intente iniciar sesión con esa cuenta.
  **Esperado:** no entra y ve "Tu cuenta de asesor está pendiente de aprobación…".
- [x] **3.3** En la **ventana de administradora**, abra http://localhost:5173/cuenta/usuarios con el filtro "Pendiente" y pulse **Aprobar** en el asesor de prueba.
  **Esperado:** "Se aprobó la cuenta de asesor de …". No sale ningún correo (D-73): en la vida real se le avisa por WhatsApp.
- [x] **3.4** Siempre como administradora, en http://localhost:5173/cuenta/suscripciones asígnele un plan al asesor de prueba. En "Pago recibido" elija el medio "Otro" y deje la referencia vacía; luego escriba `PRUEBA-001` y guarde.
  **Esperado:** sin referencia no deja guardar; con ella se guarda y la suscripción queda vigente.
- [x] **3.5** En la ventana de incógnito, inicie sesión como asesor.
  **Esperado:** entra; el perfil dice "Rol: Asesor" y la tarjeta "Mi suscripción" muestra el plan y su vencimiento. No ve pagos.
- [ ] **3.6** Si el plan vence en 7 días o menos, mire "Mi suscripción".
  **Esperado:** "Tu plan vence en N días…" y el botón "Escribir por WhatsApp para renovar". Si vence en más de 7 días, el aviso no sale, y también es correcto.
- [ ] **3.7** Abra "Mi inventario" (http://localhost:5173/inventario) y pulse **Registrar vehículo**. Use datos y placa inventados y agregue una foto.
  **Esperado:** el vehículo queda en "Mi inventario".
- [x] **3.8** Publíquelo (botón "Publicar").
  **Esperado:** aparece en "Mis publicaciones" (http://localhost:5173/publicaciones) y se ve en el catálogo del portal.
- [x] **3.9** Abra la ficha pública de su vehículo.
  **Esperado:** no ve el formulario de contacto ni el botón de crédito; ve "Esta es una de tus publicaciones. Los contactos que lleguen por ella los verás en "Mis contactos"."
- [x] **3.10** Cierre la sesión del asesor y, como visitante, envíe un contacto desde la ficha de ese vehículo (con la casilla marcada).
  **Esperado:** "¡Listo!…".
- [x] **3.11** Vuelva a iniciar sesión como asesor.
  **Esperado:** el perfil muestra "Contactos (1 nuevo)".
- [x] **3.12** Abra el contacto, guarde una nota (5 a 500 caracteres) y ciérrelo con un resultado.
  **Esperado:** la nota queda en el historial y el contacto pasa a "Finalizado". No hay botones "Reasignar" ni "Reabrir".
- [x] **3.13** Revise la lista de "Mis contactos".
  **Esperado:** solo está el contacto de su vehículo. **No** ve los avisos, créditos, búsquedas ni contactos de los recorridos 1 y 2: esos los atiende la administración.
- [x] **3.14** En "Mi inventario", pulse **Marcar vendido** en su vehículo y confirme. Luego abra "Mis ventas" (http://localhost:5173/ventas).
  **Esperado:** el vehículo sale del portal y la venta aparece en "Mis ventas".
- [x] **3.15** En "Mi inventario", pulse **Revertir venta** con un motivo.
  **Esperado:** en "Mis ventas" la venta aparece como revertida, con su motivo.
- [x] **3.16** Abra "Mis indicadores" (http://localhost:5173/mis-indicadores) y cambie el período.
  **Esperado:** solo cifras suyas (vehículos, contactos, ventas); no hay botón de descarga.
- [x] **3.17** Cierre la sesión del asesor.
  **Esperado:** vuelve al inicio de sesión.

---

## 4. Administradora (ventana normal)

**Panel y usuarios**

- [x] **4.1** Abra http://localhost:5173/cuenta/inicio.
  **Esperado:** solo números (sin nombres, teléfonos ni dinero). Al pulsar uno, se abre su lista con ese mismo total.
- [x] **4.2** En http://localhost:5173/cuenta/usuarios, use el filtro "Todos los estados" y busque "prueba".
  **Esperado:** aparecen el comprador y el asesor de prueba como "Activa". Su propia cuenta dice "Sin acciones".

**Solicitudes en "Contactos"**

- [x] **4.3** Abra http://localhost:5173/contactos y use el filtro **"Origen"** con cada valor.
  **Esperado:** se ven por separado el contacto de vehículo, "Vende tu camión", "Seguro todo riesgo", "Crédito" y "Búsqueda" enviados en los recorridos 1 a 3.
- [x] **4.4** Abra el aviso "Vende tu camión": guarde una nota, ciérrelo con **"Solicitud atendida"** y reábralo con un motivo.
  **Esperado:** cada acción queda en el historial; al reabrir vuelve a "Pendiente de gestión". **No** hay botón "Reasignar".
- [x] **4.5** Abra un crédito y guarde una nota. Luego ciérrelo con **"Remitido a entidad financiera"**.
  **Esperado:** con la nota pasa a "En gestión"; al cerrar, a "Cerrado". No hay monto ni "Reasignar".
- [ ] **4.6** Abra la búsqueda, pulse **"Ver coincidencias"**, **"Relacionar"** un vehículo y luego **"Retirar"**. Ciérrela con "Encontró vehículo" o "Sin vehículo disponible".
  **Esperado:** la lista de coincidencias se ve, el vehículo se agrega y se quita, y queda en el historial.
- [x] **4.7** Abra el contacto del vehículo del asesor de prueba (lo cerró en 3.12). Pulse **"Reabrir"** con un motivo y después **"Reasignar"** a Administración con un motivo.
  **Esperado:** el contacto queda abierto y a cargo de Administración; el historial muestra quién reabrió y reasignó, con los motivos (D-7).
- [x] **4.8** En ese mismo contacto, baje a **"Autorización de tratamiento de datos"**.
  **Esperado:** se ven el formulario, la versión de la política y la fecha y hora; no se puede editar.

**Catálogos, planes y portada**

- [x] **4.9** Abra http://localhost:5173/cuenta/catalogos y busque una marca.
  **Esperado:** se ven la marca y sus modelos. (No hace falta crear nada.)
- [x] **4.10** Abra http://localhost:5173/cuenta/planes.
  **Esperado:** se ven los planes con su precio, duración y cupos.
- [x] **4.11** Abra http://localhost:5173/cuenta/portada.
  **Esperado:** se ve la foto actual de la portada. (Cambiarla es opcional.)

**Suscripciones y pagos**

- [x] **4.12** En http://localhost:5173/cuenta/suscripciones, pulse **"Pagos"** en la suscripción del asesor de prueba.
  **Esperado:** aparece el pago `PRUEBA-001` del paso 3.4 y el total.
- [x] **4.13** Pulse **"Registrar pago"** y anote otro con medio "Transferencia" y referencia `PRUEBA-002`.
  **Esperado:** el pago aparece y el total sube.
- [x] **4.14** Pulse **"Anular pago"** en el pago `PRUEBA-002`, lea el aviso y confirme con un motivo.
  **Esperado:** el aviso dice qué va a pasar; el pago queda "Anulado", el total baja y la suscripción no cambia. **No anule el pago de la asignación (`PRUEBA-001`):** por D-67 eso cancelaría la suscripción del asesor de prueba.
- [ ] **4.15** (Opcional) Renueve el plan del asesor de prueba con pago y anule el pago de esa renovación.
  **Esperado:** el vencimiento vuelve a la fecha de antes de la renovación (D-67).

**Parámetros**

- [x] **4.16** En http://localhost:5173/cuenta/parametros, anote los datos de contacto actuales, cambie el horario y guarde. Recargue la portada sin sesión (ventana de incógnito) con `Ctrl+Shift+R`.
  **Esperado:** el pie muestra el horario nuevo. Después **deje el horario original**.
- [ ] **4.17** En "Datos de la empresa", escriba la razón social `Empresa de Prueba SAS` y el NIT `800197268-5` y guarde.
  **Esperado:** no se guarda: "Escribe el NIT con su dígito de verificación, por ejemplo 900.123.456-7."
- [ ] **4.18** Cambie el NIT a `800.197.268-4` (NIT público de la DIAN, solo de ejemplo), escriba una dirección ficticia y guarde. Mire el pie. Al terminar, **vacíe los tres campos y guarde**.
  **Esperado:** con los tres datos, el pie los muestra; al vaciarlos, el pie deja de mostrarlos.

**Recuperación de contraseña**

- [x] **4.19** En "Usuarios", pulse **"Generar enlace de recuperación"** en el comprador de prueba y luego "Generar enlace". No pegue el enlace en chats ni documentos.
  **Esperado:** aparece un enlace de la forma `http://localhost:5173/restablecer-contrasena#codigo=…`, que vence en 60 minutos, con los botones para enviarlo por WhatsApp o Gmail, copiarlo y cerrar. Las cuentas de administrador no tienen el botón.
- [x] **4.20** Copie el enlace y ábralo en la ventana de incógnito. Escriba una contraseña nueva inventada dos veces y guarde.
  **Esperado:** la dirección queda sin el código; sale que la contraseña cambió y puede entrar con la nueva.
- [x] **4.21** Abra otra vez el mismo enlace.
  **Esperado:** "El enlace no es válido o ya venció. Pide uno nuevo al administrador."

**Ventas, reportes, indicadores y actividad**

- [x] **4.22** Abra http://localhost:5173/cuenta/ventas.
  **Esperado:** aparece la venta del asesor de prueba como revertida, con su motivo.
- [x] **4.23** Abra http://localhost:5173/cuenta/reportes y recorra las cinco pestañas con el mes en curso. Cambie el período a más de 366 días.
  **Esperado:** cada pestaña muestra sus cifras; con más de 366 días avisa junto al campo.
- [x] **4.24** En la pestaña "Ventas", pulse **"Descargar CSV"** y ábralo con doble clic en Excel.
  **Esperado:** columnas separadas, tildes bien escritas, cifras iguales a la pantalla y **ningún** nombre, teléfono ni correo de clientes.
- [x] **4.25** Abra http://localhost:5173/cuenta/indicadores y cambie el período.
  **Esperado:** cifras con la comparación "frente al período anterior" y cuatro gráficos con su tabla.
- [x] **4.26** En el inventario (http://localhost:5173/inventario), abra el vehículo del asesor de prueba y pulse **"Ver actividad"**.
  **Esperado:** se ven el registro, la publicación, la venta y la reversión, con fecha, hora y quién lo hizo.
- [x] **4.27** En "Suscripciones", pulse **"Actividad"** en la suscripción del asesor de prueba.
  **Esperado:** se ven la asignación y los pagos, incluido el anulado del paso 4.14.

---

## 5. Seguridad visible

- [x] **5.1** En la ventana de incógnito, inicie sesión como **asesor de prueba** y escriba http://localhost:5173/cuenta/usuarios y luego http://localhost:5173/cuenta/reportes.
  **Esperado:** en las dos, "No tienes permiso para realizar esta acción."
- [x] **5.2** Cierre la sesión y, **sin sesión**, escriba http://localhost:5173/contactos.
  **Esperado:** lo lleva al inicio de sesión en http://localhost:5173/cuenta.
- [x] **5.3** Inicie sesión como **comprador de prueba** y abra "Mis solicitudes". En la ventana de administradora, en "Usuarios", **bloquee** al comprador de prueba con un motivo.
  **Esperado:** "Se bloqueó la cuenta de …".
- [x] **5.4** En la ventana del comprador, pulse otra vez "Mis solicitudes" (o recargue con F5).
  **Esperado:** lo lleva al inicio de sesión con el aviso "Tu sesión terminó. Inicia sesión de nuevo." **una sola vez**: no dos avisos seguidos ni un texto que parpadea (corrección de la PR #20).
- [x] **5.5** Intente iniciar sesión como comprador de prueba.
  **Esperado:** "Tu cuenta está bloqueada. Comunícate con el administrador." Luego, como administradora, **desbloquéela**.
- [x] **5.6** **Hágalo de último**, porque durante unos 15 minutos no podrá iniciar sesión desde este equipo: cierre la sesión y, con un correo que no existe (por ejemplo `no-existe@ejemplo.com`) y cualquier contraseña, pulse "Iniciar sesión" 21 veces.
  **Esperado:** las primeras 20 dan el error normal; desde la 21 sale "Hiciste muchos intentos de inicio de sesión. Espera unos minutos e intenta de nuevo."

---

## 6. Resumen y qué anotar si algo falla

### Tabla resumen

Llene la última columna con "Bien", "Falla (ver nota)" o "No se probó".

| Recorrido | Qué cubre | Casillas | Resultado |
|---|---|---|---|
| 0. Preparación | XAMPP, API, frontend, recarga | 0.1 a 0.4 (4) | |
| 1. Visitante sin cuenta | Portada, pie, categorías, búsqueda y filtros, ficha, WhatsApp, contacto, "Vende tu camión", seguro, crédito, "Busco un vehículo", términos y privacidad, página no encontrada, enlaces viejos | 1.1 a 1.21 (21) | |
| 2. Comprador | Registro con casilla, inicio de sesión, "Mis solicitudes", solo lectura, inactividad | 2.1 a 2.11 (11) | |
| 3. Asesor | Inscripción pendiente y aprobación, plan, inventario y publicación, sus contactos, ventas, "Mis indicadores" | 3.1 a 3.17 (17) | |
| 4. Administradora | Panel, usuarios, solicitudes de todos los tipos, reasignación, catálogos, planes, portada, pagos y anulación, parámetros, recuperación, ventas, reportes y CSV, indicadores, actividad | 4.1 a 4.27 (27) | |
| 5. Seguridad visible | Roles, rutas con sesión, sesión vencida con un solo aviso, cuenta bloqueada, límite de intentos | 5.1 a 5.6 (6) | |
| No se puede probar en local | Vista previa al compartir por WhatsApp (D-81) y correo automático (D-73) | — | No aplica |

Total: **86 casillas**.

### Qué anotar si algo falla

Por cada paso que no salga como dice **Esperado**, anote:

1. **El número del paso** (por ejemplo, "4.14").
2. **La dirección exacta** de la barra del navegador (por ejemplo, http://localhost:5173/cuenta/suscripciones).
3. **La hora** en que pasó (sirve para buscar en el registro de la API).
4. **Una captura de pantalla** (`Windows+Shift+S`). Antes de guardarla, revise que no se vean contraseñas ni enlaces de recuperación.
5. **Qué esperaba y qué vio**, con el texto exacto del mensaje si salió alguno.
6. **Con qué cuenta** estaba (administradora, comprador de prueba, asesor de prueba o sin sesión).
7. Si la terminal de la API mostró un error en ese momento, anote solo la hora y la primera línea, **sin copiar** cadenas de conexión ni claves.

Entregue las notas al coordinador para registrarlas en `docs/coordinacion/BITACORA.md`.

### Datos de prueba que quedan

Las cuentas `prueba.comprador@ejemplo.com` y `prueba.asesor@ejemplo.com`, el vehículo de prueba, las solicitudes, los pagos `PRUEBA-001` y `PRUEBA-002` y la venta revertida quedan en la base local `camion_ventas`, que es solo de pruebas. El portal publicado se crea en una base nueva (D-80 A), así que no hace falta borrarlos.
