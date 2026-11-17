# Criterios de aceptación P9: prepublicación

Ítem del backlog: **P9 "Prepublicación"** (`docs/coordinacion/BACKLOG.md`, fila P9), aprobado por la responsable el 2026-11-16. Alcance: solo lo que bloquea publicar y **no depende del abogado** (D-44 sigue PROPUESTO).
Rama: `feature/p9-prepublicacion`. Autor: analista funcional. Fecha: 2026-11-16.

Fuentes leídas:
- `docs/coordinacion/ESTADO-REAL.md` § "Preparación para publicar (2026-11-16)" (PP.2 a PP.5).
- `docs/coordinacion/DECISIONES.md`: D-1, D-3, D-5, D-7, D-8, D-12, D-42, D-44, D-47, D-63, D-73, D-76, D-78, D-80, D-81, D-82.
- `docs/requisitos/requisitos-funcionales-v3.0.md`: matriz §7 (HU-059, HU-062, HU-081, HU-085, HU-086), §9.7 (RN-SE-001 a RN-SE-006), §9.10 (RN-PA-001, RN-PA-005, RN-PA-006).
- `docs/legal/POLITICA-TRATAMIENTO-DATOS.md` y `docs/legal/TERMINOS-Y-CONDICIONES.md` (borradores, **no se publican**).
- Código (lectura directa del analista, no es auditoría): `frontend/src/components/AutorizacionDatos.tsx`, `RegistroForm.tsx`, `Privacidad.tsx`, `PiePortal.tsx`, `SolicitudCredito.tsx:44-49`, `BusquedaVehiculo.tsx:101`, `MisSolicitudes.tsx:113`, `useContactoPortal.ts`, `frontend/src/rutas.ts`, `frontend/src/App.tsx:101-112`, `frontend/index.html`, `frontend/src/api/contactos.ts:40-52`, `api/parametros.ts`, `api/auth.ts:95-140`; `backend/Camion.Api/Endpoints/AuthEndpoints.cs`, `ContactosEndpoints.cs:63-64`, `:122-149`, `ParametrosEndpoints.cs`, `Parametros/ParametrosPortal.cs`, `Data/CamionDbContext.cs:493-503`, `:586-601`, `Models/Lead.cs:130-139`, `Leads/ReglasLeads.cs:31`, `:46`, `Program.cs:54-56`, `:129-178`, `Seguridad/ConfiguracionServidor.cs:99-108`, `appsettings.json`; `database/migraciones/013_p5_leads_SUBIR.sql:66-78`, `015_p6_creditos_busquedas_SUBIR.sql:35-39`, `database/002_esquema_inicial_mariadb_10_4.sql:457-463`.

Numeración: matriz oficial de 87 HU (D-1). En ella HU-059 = Iniciar sesión, HU-062 = Proteger datos personales, HU-081 = Parámetros generales, HU-085 = Información corporativa, HU-086 = Textos legales. (La lista de la sección 10 de la v3.0 usa otra numeración, ya resuelta por D-1.)

Marcas:
- **CONFIRMADO**: sale literalmente de una HU, RN, decisión TOMADA o del encargo aprobado de P9.
- **PROPUESTO**: lo infiere el analista (valores, textos, formatos); necesita visto bueno del coordinador o de la responsable.
- **PENDIENTE D-n**: depende de una decisión abierta; se implementa con la opción recomendada solo si el coordinador lo autoriza, o se deja fuera.
- **EXISTE**: ya está en el código; en P9 queda como regresión.

Cómo se prueba: API = prueba de integración (`Camion.Api.Tests`); COMP = prueba de componente (Vitest + Testing Library); UNIT = prueba unitaria; MAN = revisión manual de la responsable.
Rutas, nombres de campos y textos son orientativos salvo donde se marca CONFIRMADO; el contrato definitivo lo fija el arquitecto.

**Migración: NO.** La restricción `CK_Consent_Formulario` ya admite `REGISTRO_COMPRADOR` e `INSCRIPCION_ASESOR` (scripts 013 y 015, y `CamionDbContext.cs:588-590`), y los datos de la empresa caben en `configuracion_sistema` (clave/valor JSON, script 002, sin restricción sobre la clave) con una clave nueva. Si el arquitecto prefiere columnas propias, el siguiente número libre es **018** (el 017 lo ocupa `database/catalogos/017_categorias_base_SUBIR.sql`, PROPUESTO). Antes de cerrar P9, el coordinador debe confirmar con un `SELECT` a `information_schema.CHECK_CONSTRAINTS` que la base local tiene la versión del 015 de `CK_Consent_Formulario`.

---

## 1. Qué pide / qué existe / qué falta

| Punto del encargo | HU / RN / D | Qué existe | Qué falta |
|---|---|---|---|
| 1. Autorización de datos en registro e inscripción | HU-062, RN-SE-004, D-44 (pendiente técnico) | Casilla y prueba en contacto, avisos, crédito y búsqueda (`ContactosEndpoints.cs:63-89`, `:122-123`); constantes `REGISTRO_COMPRADOR` e `INSCRIPCION_ASESOR` sin uso (`Lead.cs:130-131`); restricción de la base ya las admite | Campo en `POST /api/auth/registro/comprador` y `/asesor`, validación, fila en `consentimientos_datos`; casilla en `RegistroForm.tsx`; corregir el comentario falso de `AutorizacionDatos.tsx:2` |
| 2. Términos y privacidad con aviso de revisión | HU-086, RN-PA-006, D-44, D-42 | `#/privacidad` con franja "BORRADOR — en revisión legal" (`Privacidad.tsx:9`, `:15-25`); sin `#/terminos`; pie sin enlaces legales (`PiePortal.tsx:137`) | Ruta y página `#/terminos`; enlaces en el pie y junto a la casilla |
| 3. Datos de la empresa | HU-085, RN-PA-005, D-44, D-63 B, D-42 | Solo contacto (D-63 A, `ParametrosEndpoints.cs`) | Razón social, NIT y dirección editables por el administrador; visibles en el pie si están completos (dirección: **PENDIENTE D-83**) |
| 4. Límite de intentos | HU-059, RN-SE-001, RN-SE-006, INF-2 | Bloqueo de la cuenta tras 5 fallos, 15 min (`Program.cs:54-56`, 423); limitador por IP con /64 solo en contacto, avisos, crédito, búsqueda y restablecer (`Program.cs:129-178`) | Límite por IP en registro (ambos) e inicio de sesión, con 429 y mensaje propio |
| 5. Página no encontrada | RN-SE-005 (consulta pública) | Ruta desconocida → portada (`rutas.ts:224`) | Pantalla "Página no encontrada" |
| 6. SEO básico | — (v3.0 no tiene HU de SEO; encargo P9) | Solo `<title>` y título por ruta (`index.html:6`, `App.tsx:103-112`) | meta description, favicon, `robots.txt` (no existe `frontend/public/`) |
| 7. Teléfono de los mensajes | HU-081, RN-PA-001, D-63 A | Pie, ficha y privacidad leen `GET /api/portal/contacto` | 3 textos fijos (`contactos.ts:41`, `BusquedaVehiculo.tsx:101`, `MisSolicitudes.tsx:113`); además el enlace de WhatsApp del crédito usa siempre el número fijo (`SolicitudCredito.tsx:48`, hallazgo nuevo) |
| 8. `PoliticaDatos:Version` | RN-SE-004, D-44 | `BORRADOR-2026-11-04` (`appsettings.json:10`), con el mismo valor fijo de respaldo en `ReglasLeads.cs:31` leído en 3 sitios | Documentar el valor de borrador y el cambio al aprobarse; registro y asesor deben leerlo igual |

---

## 2. Criterios de aceptación

### 2.1 Autorización de datos en el registro de comprador y la inscripción de asesor (HU-062, RN-SE-004, D-44)

| ID | Criterio (Dado / Cuando / Entonces) | Marca | Prueba |
|---|---|---|---|
| CA-P9-01 | **Dado** un visitante anónimo en `#/cuenta/registro` con "Soy comprador", **cuando** ve el formulario, **entonces** aparece la casilla de `AutorizacionDatos` **sin marcar**, antes del botón "Crear cuenta", con el texto actual ("Autorizo el tratamiento de mis datos personales según la Política de tratamiento de datos.") y el enlace a `#/privacidad` en pestaña nueva. | CONFIRMADO (D-44: "casilla sin marcar, con enlace a la política, en el registro") | COMP |
| CA-P9-02 | **Dado** el mismo formulario con "Soy asesor", **entonces** aparece la misma casilla sin marcar antes de "Enviar solicitud". | CONFIRMADO (D-44: "la inscripción de asesor") | COMP |
| CA-P9-03 | **Dado** el formulario completo y válido con la casilla sin marcar, **cuando** pulsa enviar, **entonces** no se llama a la API, se muestra bajo la casilla "Debes autorizar el tratamiento de tus datos para crear tu cuenta." y el foco va al primer campo con error (la casilla, si es el único). | Bloqueo: CONFIRMADO (D-44). Texto y foco: PROPUESTO | COMP |
| CA-P9-04 | **Dado** que se marca la casilla tras el error, **entonces** el mensaje desaparece (mismo comportamiento que los demás campos tras el primer intento). | PROPUESTO | COMP |
| CA-P9-05 | **Dado** que el usuario cambia entre "Soy comprador" y "Soy asesor", **entonces** la casilla conserva su estado (es la misma persona y la misma política) y se limpian los errores como hoy (`RegistroForm.tsx:93-100`). | PROPUESTO | COMP |
| CA-P9-06 | **Dado** un `POST /api/auth/registro/comprador` válido con `"autorizacionDatos": true`, **entonces** 201 como hoy y, en la **misma transacción** que crea el usuario, una fila en `consentimientos_datos` con `formulario = 'REGISTRO_COMPRADOR'`, `version_politica` = valor de `PoliticaDatos:Version`, `aceptado_en` = hora UTC del servidor, `usuario_id` = id del usuario nuevo, `entidad = 'Usuario'`, `entidad_id` = id del usuario. | Guardar fecha, versión y formulario: CONFIRMADO (D-44; mismo modelo que CA-P5-15). Valores de `entidad`: PROPUESTO | API |
| CA-P9-07 | **Dado** un `POST /api/auth/registro/asesor` válido con `"autorizacionDatos": true`, **entonces** 201 con la cuenta PENDIENTE (EXISTE) y una fila con `formulario = 'INSCRIPCION_ASESOR'` y los mismos campos que CA-P9-06, en la misma transacción que el perfil y la auditoría `RegistroAsesor`. | CONFIRMADO (D-44) / PROPUESTO (`entidad`) | API |
| CA-P9-08 | **Dado** cualquiera de los dos registros con `autorizacionDatos` ausente, `null` o `false`, **entonces** 400 `{ "error": "Revisa los datos marcados.", "errores": { "autorizacionDatos": ["Debes autorizar el tratamiento de tus datos para crear tu cuenta."] } }`, junto con los demás errores de campo si los hay; no se crea usuario, perfil, rol ni consentimiento. | Rechazo: CONFIRMADO (D-44, "la API vuelve a exigirla", `AutorizacionDatos.tsx:3`). Texto: PROPUESTO | API |
| CA-P9-09 | **Dado** `autorizacionDatos` con un tipo no booleano (`"true"`, `1`), **entonces** 400 con el formato común de JSON no válido (como hoy con `ThrowOnBadRequest`), sin crear nada. | PROPUESTO | API |
| CA-P9-10 | **Dado** un registro con la casilla marcada pero con correo ya registrado (o documento de asesor repetido), **entonces** 409 como hoy (`AuthEndpoints.cs:50-51`, `:118-121`) y **no** se guarda ningún consentimiento (tampoco en la carrera de dos registros simultáneos, `:86-95`, `:165-177`). | Duplicado: EXISTE. Sin consentimiento: PROPUESTO | API |
| CA-P9-11 | **Dado** un fallo al guardar el consentimiento, **entonces** se revierte toda la transacción (no queda cuenta sin prueba de autorización) y se responde 500 con `{ error }` genérico. | PROPUESTO | API |
| CA-P9-12 | **Dado** que el formulario del frontend envía el registro, **entonces** el cuerpo incluye `autorizacionDatos: true`; un 400 con `errores.autorizacionDatos` se muestra bajo la casilla. | PROPUESTO | COMP |
| CA-P9-13 | **Dado** un usuario creado por `promover-admin` o `recuperar-admin`, **entonces** esos comandos no crean consentimientos (promueven o recuperan una cuenta que ya se registró). | PROPUESTO | API |
| CA-P9-14 | Regresión: los consentimientos de contacto, avisos, crédito y búsqueda siguen guardándose igual (CA-P5-15 y equivalentes de P5b y P6). | EXISTE | API |
| CA-P9-15 | El comentario de `AutorizacionDatos.tsx:2` queda cierto (los registros sí usan la casilla) y la casilla muestra, **fuera de la etiqueta** (para no cambiar `TEXTO_AUTORIZACION` en los otros 4 formularios), un enlace a `#/terminos` "Términos y condiciones" en pestaña nueva. | Enlace junto a la casilla: CONFIRMADO (encargo P9 punto 2). Ubicación: PROPUESTO | COMP |
| CA-P9-16 | El registro **no** pide aceptar los términos con una casilla propia mientras D-44 no esté aprobado. | PENDIENTE D-84 (recomendación A) | COMP |

### 2.2 Páginas de términos y privacidad, y enlaces legales (HU-086, RN-PA-006, D-44, D-42)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P9-17 | **Dado** cualquier visitante (anónimo, comprador, asesor o administrador), **cuando** abre `#/terminos`, **entonces** ve una página "Términos y condiciones de uso" con una franja visible "En revisión legal" (mismo estilo que `franja-borrador`), un texto breve que explica que los términos se publicarán cuando termine la revisión legal, el contacto vigente de `GET /api/portal/contacto` (con respaldo) y un enlace "Ir al inicio". No exige sesión. | Página y aviso: CONFIRMADO (encargo P9; D-44 "publicar #/terminos"). Textos: PROPUESTO | COMP |
| CA-P9-18 | `#/privacidad` mantiene el aviso de revisión legal y el contacto vigente (EXISTE, `Privacidad.tsx`). El texto de la franja de las dos páginas es el mismo; se propone "En revisión legal" en lugar de "BORRADOR — en revisión legal", para no anunciar al público un "borrador". | EXISTE / texto: PROPUESTO | COMP |
| CA-P9-19 | **Ninguna** de las dos páginas contiene texto de `docs/legal/` (se comprueba que no aparezcan, por ejemplo, "NOTA PARA REVISIÓN", "[RAZÓN SOCIAL]", "[NIT]" ni "[DOMINIO]"). | CONFIRMADO (D-44: "No se publican en el portal hasta la revisión legal") | COMP |
| CA-P9-20 | El aviso de revisión depende de **una sola constante** del frontend (por ejemplo `TEXTOS_LEGALES_EN_REVISION = true`), que se cambia en la entrega que publique los textos aprobados. Mientras sea `true`, las dos páginas muestran la franja. | PROPUESTO (ver §4, punto 8) | UNIT |
| CA-P9-21 | `#/terminos` tiene título de documento "Términos y condiciones | Financiamos Tu Camión" y `#/privacidad` "Política de tratamiento de datos | Financiamos Tu Camión"; el foco va al `h1` al entrar (comportamiento de `App.tsx:270-276`). | PROPUESTO | COMP |
| CA-P9-22 | **Dado** el pie del portal en cualquier página y con o sin sesión, **entonces** la franja inferior (`cat-pie-legal`) muestra, además del ©, los enlaces "Términos y condiciones" (`#/terminos`) y "Política de tratamiento de datos" (`#/privacidad`), en la misma pestaña. | CONFIRMADO (encargo P9; D-44 "enlazarlas desde el pie") | COMP |
| CA-P9-23 | La ruta `#/terminos` se reconoce en `leerRuta` (con y sin barra final) y existe `HASH_TERMINOS`. | PROPUESTO | UNIT |

### 2.3 Datos de la empresa administrables (HU-085, RN-PA-005, D-63 B, D-44, D-42)

Regla de NIT (definida por el analista, PROPUESTO; algoritmo de la DIAN, módulo 11):
- Entrada aceptada: número base de **6 a 10 dígitos**, un guion y **1 dígito de verificación** (DV). Se admiten puntos y espacios como separadores de miles en la base, que se quitan al normalizar. Ejemplos válidos: `800197268-4`, `800.197.268-4`, `800 197 268 - 4`. Sin guion **no** se acepta (`8001972684` es ambiguo: no se sabe dónde empieza el DV).
- 6 a 10 dígitos cubre a las personas jurídicas (9 dígitos) y a las personas naturales comerciantes, cuyo NIT es la cédula más el DV (la política dice "NIT o cédula", `POLITICA-TRATAMIENTO-DATOS.md:10`).
- DV: se multiplican los dígitos de la base, **de derecha a izquierda**, por 3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71; se suma; `r = suma mod 11`; si `r` es 0 o 1, DV = `r`; si no, DV = `11 − r`.
- Comprobación con el NIT público de la DIAN `800197268`: suma 733, 733 mod 11 = 7, DV = 4 → `800197268-4` válido; `800197268-5` no válido.
- Base formada solo por ceros (`000000-0`) no se acepta.
- Se guarda normalizado como `800197268-4` y se muestra como `800.197.268-4`.

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P9-24 | **Dado** un administrador en `#/cuenta/parametros`, **entonces** ve, junto a "Datos de contacto", una sección "Datos de la empresa" con razón social, NIT y dirección de notificaciones, la fecha y el autor del último cambio, y un aviso: "Se muestran en el pie del portal solo cuando los tres están completos." | Editable desde el panel: CONFIRMADO (encargo P9; D-63 B). Textos: PROPUESTO | COMP |
| CA-P9-25 | **Dado** `GET /api/admin/parametros/empresa` con token de administrador, **entonces** 200 con `{ razonSocial, nit, direccion, completo, configurado, actualizadoEn, actualizadoPor }`; sin datos guardados: los tres en `null`, `completo: false`, `configurado: false`. **No hay valores por defecto** (no se inventan datos de la empresa). | CONFIRMADO (D-44: "los debe entregar la responsable") / forma: PROPUESTO | API |
| CA-P9-26 | **Dado** `PUT /api/admin/parametros/empresa` válido, **entonces** 200 con el mismo cuerpo que el GET; valor y auditoría (`entidad = Parametro`, `entidad_id = 'portal.empresa'`, acción `CAMBIAR`, valores anterior y nuevo) en la misma transacción, como `PUT /contacto`. Si nada cambia, no se escribe ni se audita. | PROPUESTO (mismo patrón que CA-P7-36 y 37) | API |
| CA-P9-27 | Cada campo es **opcional por separado** (se puede guardar uno solo o vaciarlos todos para retirarlos del pie). Un campo vacío o solo con espacios se guarda como `null`. | PROPUESTO | API |
| CA-P9-28 | Razón social: 2 a 150 caracteres tras recortar espacios; sin `<`, `>` ni caracteres de control. Si no cumple: 400 con `errores.razonSocial = ["Escribe la razón social (entre 2 y 150 caracteres, sin los signos < >)."]`. | PROPUESTO | API, COMP |
| CA-P9-29 | NIT: la regla de arriba. Si no cumple: 400 con `errores.nit = ["Escribe el NIT con su dígito de verificación, por ejemplo 900.123.456-7."]`. Casos de prueba: `800197268-4` acepta y guarda `800197268-4`; `800.197.268-4` acepta; `800197268-5`, `8001972684`, `80019-4`, `12345678901-2`, `ABC-1`, `000000-0` rechazan. El frontend aplica la misma regla. | PROPUESTO | UNIT (C# y TS), API, COMP |
| CA-P9-30 | Dirección de notificaciones: 5 a 200 caracteres tras recortar; sin `<`, `>` ni caracteres de control. Si no cumple: 400 con `errores.direccion = ["Escribe la dirección de notificaciones (entre 5 y 200 caracteres, sin los signos < >)."]`. | PROPUESTO | API, COMP |
| CA-P9-31 | Con varios errores, el 400 los trae todos y no escribe nada (valida todo antes de escribir). Cuerpo vacío o JSON no válido: 400 con el formato común. | PROPUESTO | API |
| CA-P9-32 | Permisos de `GET` y `PUT /api/admin/parametros/empresa`: sin token 401; comprador 403; asesor 403; asesor PENDIENTE o cuenta bloqueada 401 (sesión no válida, como el resto del panel); administrador 200. | CONFIRMADO (RN-SE-006; HU-085 es del Administrador) | API |
| CA-P9-33 | **Dado** `GET /api/portal/contacto` (anónimo), **entonces** la respuesta añade `empresa`: `{ razonSocial, nit, direccion }` **solo si los tres están guardados**; si falta alguno, `empresa: null`. Nunca devuelve `actualizadoPor` ni datos parciales. Los campos existentes no cambian (compatibilidad con `leerContactoPortal`). | Visible solo si completos: CONFIRMADO (encargo P9). Forma: PROPUESTO | API |
| CA-P9-34 | **Dado** `empresa` no nula, **entonces** el pie muestra en la franja inferior: "Razón social · NIT 800.197.268-4 · Dirección de notificaciones: …", como texto (nunca HTML). **Dado** `empresa: null` o la lectura falla, **entonces** no se muestra nada de la empresa (sin respaldo fijo). | Mostrar los tres: **PENDIENTE D-83** (D-42 dice "Dirección: por ahora no se publica"). Sin respaldo: PROPUESTO | COMP |
| CA-P9-35 | Las páginas `#/privacidad` y `#/terminos` muestran los mismos datos de la empresa como "Responsable" cuando `empresa` no es nula. | PENDIENTE D-83 | COMP |
| CA-P9-36 | El frontend del administrador muestra los errores por campo del 400, conserva lo escrito y, al guardar, muestra "Datos de la empresa guardados."; 401 lleva a la pantalla de sesión terminada como en `guardarContactoAdmin`. | PROPUESTO | COMP |
| CA-P9-37 | Regresión: `GET/PUT /api/admin/parametros/contacto` y `GET /api/portal/contacto` siguen igual (CA-P7-36 a CA-P7-44); ningún valor de negocio pasa a ser editable (D-63 C rechazada). | EXISTE / CONFIRMADO (D-63) | API |

### 2.4 Límite de intentos en registro e inicio de sesión (HU-059, RN-SE-001, RN-SE-006, INF-2)

Valores propuestos (PROPUESTO; configurables en una sección nueva `LimitesAuth` de `appsettings.json`, para que las pruebas los suban como ya se hace con `Contactos`):

| Cupo | Rutas | Clave de partición | Ventana corta | Diario |
|---|---|---|---|---|
| Registro | `POST /api/auth/registro/comprador` y `POST /api/auth/registro/asesor` (**cupo compartido**) | IP, IPv6 agrupada por /64 (`ConfiguracionServidor.ClaveIp`) | 5 cada 60 min | 20 por día |
| Inicio de sesión | `POST /api/auth/login` | Igual | 20 cada 15 min | — |

Razones: el registro es raro para una persona real (una o dos cuentas); compartir el cupo evita que un robot duplique el límite usando las dos rutas. El inicio de sesión tiene más margen porque varias personas de una misma oficina pueden salir por la misma IP y la sesión vence a las 2 h o tras 30 min sin actividad (D-12). Por **correo** no se añade límite: en el inicio de sesión ya existe el bloqueo de la cuenta (5 fallos → 15 min, 423, `Program.cs:54-56`), y en el registro un correo repetido ya responde 409.

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P9-38 | **Dado** una IP que ya hizo 5 peticiones de registro (sumando comprador y asesor) en los últimos 60 min, **cuando** hace la sexta, **entonces** 429 con `{ "error": "Hiciste muchos intentos de registro desde tu conexión. Intenta de nuevo más tarde." }` y no se crea usuario ni consentimiento. | Límite y 429: CONFIRMADO (encargo P9). Valores y texto: PROPUESTO | API |
| CA-P9-39 | **Dado** una IP con 20 registros en el día, **entonces** 429 con el mismo mensaje aunque la ventana corta tenga cupo. | PROPUESTO | API |
| CA-P9-40 | **Dado** una IP que ya hizo 20 peticiones a `/api/auth/login` en 15 min (cuentan las correctas y las fallidas), **cuando** hace otra, **entonces** 429 con `{ "error": "Hiciste muchos intentos de inicio de sesión. Espera unos minutos e intenta de nuevo." }`. | CONFIRMADO (encargo) / PROPUESTO (valores, texto) | API |
| CA-P9-41 | Un 429 del limitador ocurre **antes** de leer el cuerpo: no incrementa el contador de fallos de Identity, no bloquea la cuenta, no cambia `UltimoAccesoEn` y no emite token. | PROPUESTO | API |
| CA-P9-42 | Las respuestas 429 de estas rutas llevan el encabezado `Retry-After` en segundos cuando el limitador lo conoce. | PROPUESTO | API |
| CA-P9-43 | Dos direcciones IPv6 del mismo /64 comparten cupo; dos IPv4 distintas no. Detrás del proxy se usa la IP de `X-Forwarded-For` solo si el proxy está en `KnownProxies`/`KnownNetworks` (INF-2). | CONFIRMADO (encargo: "reutilizar la agrupación IPv6 /64 de INF-2") | API |
| CA-P9-44 | Los cupos de registro, inicio de sesión, contacto (`LimitePeticionesContacto`) y restablecer contraseña (`LimitePeticionesRecuperacion`) son **independientes**: agotar uno no afecta a los otros. `/api/auth/me`, `/logout` y las rutas del panel no tienen límite nuevo. | PROPUESTO | API |
| CA-P9-45 | Regresión: el bloqueo por cuenta sigue igual (5 contraseñas incorrectas → 423 con "Cuenta bloqueada temporalmente por varios intentos fallidos. Intenta de nuevo en 15 minutos."); el mensaje de credenciales sigue siendo el mismo para correo inexistente y contraseña incorrecta (401). | EXISTE (RN-SE-001, HU-062) | API |
| CA-P9-46 | **Dado** un 429 en el formulario de inicio de sesión o de registro, **entonces** el frontend muestra el `error` del servidor en el aviso del formulario (o, si no llega, "Hiciste muchos intentos. Espera unos minutos e intenta de nuevo."), conserva lo escrito y reactiva el botón. | PROPUESTO | COMP |
| CA-P9-47 | Los rechazos por límite se registran en el log sin correo ni IP completa (como el resto del registro de auditoría de `AuthEndpoints.cs`). | PROPUESTO | API (log) |

### 2.5 Página "no encontrada" (RN-SE-005)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P9-48 | **Dado** un hash que empieza por `#/` y no corresponde a ninguna ruta (por ejemplo `#/xyz`, `#/cuenta/otra`, `#/buscar/extra`, `#/contactos/abc`, `#/inventario/0`, `#/vehiculo/%E0%A4%A`), **entonces** se muestra "Página no encontrada" con el texto "La página que buscas no existe o cambió de dirección.", y los enlaces "Ir al inicio" y "Ver todos los vehículos" (`#/buscar`); encabezado y pie normales. | Pantalla: CONFIRMADO (encargo P9). Textos: PROPUESTO | UNIT (`leerRuta`), COMP |
| CA-P9-49 | `''`, `#`, `#/` y las rutas existentes con barra final siguen llevando a su pantalla (EXISTE, `rutas.ts:179-185`). Un hash que **no** empieza por `/` (por ejemplo `#contenido`, el ancla de "Saltar al contenido") sigue mostrando la portada para no romper anclas. | PROPUESTO | UNIT |
| CA-P9-50 | La página tiene título "Página no encontrada | Financiamos Tu Camión", foco en su `h1` y `<meta name="robots" content="noindex">` mientras está visible. Se documenta que, por ser un enrutado con `#`, el servidor responde 200 (no hay 404 HTTP para estas rutas). | PROPUESTO | COMP |
| CA-P9-51 | Regresión: rutas protegidas sin sesión o con rol incorrecto siguen con su comportamiento actual (inicio de sesión o "sin permiso"), **no** con la página no encontrada; la ficha de un vehículo inexistente sigue con su propio mensaje de "no disponible". | EXISTE / PROPUESTO | COMP |

### 2.6 SEO básico

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P9-52 | `frontend/index.html` tiene `<meta name="description" content="Camiones usados en venta en Colombia: NPR, volquetas, tractomulas y maquinaria. Solicita tu crédito para camión o tu seguro todo riesgo por WhatsApp.">` (máx. 160 caracteres). No menciona tasas, aprobación de créditos ni "peritados" (preguntas abiertas de D-44). | Meta description: CONFIRMADO (encargo). Texto: CONFIRMADO (versión de 149 caracteres basada en el análisis de palabras clave, aprobada por la responsable el 2026-11-16, D-87) | MAN, UNIT (lectura del HTML) |
| CA-P9-53 | Favicon en `frontend/public/` (SVG con los colores de D-42, más un PNG de 32×32 o `.ico` de respaldo) enlazado desde `index.html`; `npm run build` lo copia a `dist/`. | CONFIRMADO (encargo) / formato: PROPUESTO | MAN, build |
| CA-P9-54 | `frontend/public/robots.txt` con `User-agent: *`, `Disallow: /api/`, `Disallow: /health` y `Allow: /`. Se documenta que el panel (`#/cuenta…`, `#/inventario…`) **no tiene URL propia** para los buscadores (el fragmento `#` no llega al servidor), por lo que `robots.txt` no puede ni necesita bloquearlo; lo que sí se bloquea son las rutas de la API. Sin `Sitemap` mientras no haya dominio (D-81). | PROPUESTO (aclara el encargo "que no indexe el panel") | MAN, build |
| CA-P9-55 | Las pantallas privadas (cuenta, inventario, publicaciones, contactos, ventas, mis solicitudes, mis indicadores, restablecer contraseña) ponen `<meta name="robots" content="noindex">`; las públicas lo quitan. | PROPUESTO (defensa adicional, opcional) | COMP |
| CA-P9-56 | Regresión: el título por ruta de `App.tsx:103-112` sigue igual y se añaden los de CA-P9-21 y CA-P9-50. | EXISTE / PROPUESTO | COMP |

### 2.7 Teléfono de los mensajes tomado de la configuración (HU-081, RN-PA-001, D-63 A)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P9-57 | **Dado** que el administrador guardó el WhatsApp `3001234567`, **cuando** falla el envío de un contacto, aviso, crédito o búsqueda (5xx o sin conexión), **entonces** el mensaje dice "… escríbenos por WhatsApp al 300 123 4567." (hoy fijo en `contactos.ts:41`). | CONFIRMADO (encargo P9 punto 7) | COMP |
| CA-P9-58 | **Dado** el mismo número, **entonces** el aviso "Por ahora no podemos recibir búsquedas aquí…" de `BusquedaVehiculo.tsx:101` y la introducción de "Mis solicitudes" (`MisSolicitudes.tsx:113`) muestran `300 123 4567`. | CONFIRMADO (encargo) | COMP |
| CA-P9-59 | **Dado** que la lectura de `GET /api/portal/contacto` falla, **entonces** los tres textos usan el respaldo de D-42 (`CONTACTO_PORTAL_RESPALDO`, 316 615 2907), igual que el pie (CA-P7-38). | CONFIRMADO (D-42, CA-P7-38) | COMP |
| CA-P9-60 | El enlace "Escribir por WhatsApp" de la solicitud de crédito (`enlaceWhatsappCredito`, `SolicitudCredito.tsx:44-49`) usa el WhatsApp vigente del portal, no la constante `WHATSAPP_PORTAL`. | PROPUESTO (hallazgo nuevo; mismo motivo que el punto 7) | COMP |
| CA-P9-61 | Ningún texto visible del frontend, fuera de los valores de respaldo (`api/parametros.ts:41-48`, `PiePortal.tsx:25-31`, `contactos.ts:52`) y del ejemplo de formato de `AdminParametros.tsx:197` y `MENSAJE_PARAM_WHATSAPP`, contiene `316 615 2907` o `3166152907` (búsqueda en `frontend/src` sin archivos de prueba). | PROPUESTO | UNIT (búsqueda) |

### 2.8 Versión de la política (`PoliticaDatos:Version`) (RN-SE-004, D-44)

| ID | Criterio | Marca | Prueba |
|---|---|---|---|
| CA-P9-62 | Mientras D-44 no esté aprobado, el valor es **`BORRADOR-2026-11-04`** (el actual): identifica con honestidad que la autorización se dio frente al borrador del 2026-11-04. No se inventa una versión final. | CONFIRMADO (encargo: "no inventar la versión final") | API |
| CA-P9-63 | Contacto, avisos, crédito, búsqueda, registro de comprador e inscripción de asesor leen la versión con **un único ayudante** (hoy el respaldo `ReglasLeads.VersionPoliticaPorDefecto` se repite en `ContactosEndpoints.cs:64`, `AvisosEndpoints.cs:77` y `SolicitudesEndpoints.cs:105`). Con `PoliticaDatos:Version = "PRUEBA-1"`, las seis rutas guardan `PRUEBA-1`. | PROPUESTO | API |
| CA-P9-64 | Fuera de Development, si la versión empieza por `BORRADOR` la API **arranca** pero escribe una advertencia en el log al iniciar ("La política de datos configurada es un borrador: no abrir el portal al público"). No bloquea, para poder montar el servidor en privado (PP.6). | PROPUESTO | API |
| CA-P9-65 | La versión configurada tiene como máximo 40 caracteres (columna `version_politica VARCHAR(40)`), solo mayúsculas, dígitos, `.` y `-`; si no cumple, la API no arranca (como `Jwt:Key`). | PROPUESTO | API |
| CA-P9-66 | `docs/GUIA-PUBLICACION.md` documenta el cambio al aprobarse la política: (1) el abogado entrega el texto final y la responsable la fecha de vigencia; (2) se publica el texto en `#/privacidad` y `#/terminos` y la constante de CA-P9-20 pasa a `false`, en una entrega aparte con su PR; (3) en el servidor se fija la variable de entorno `PoliticaDatos__Version` con el formato `V<n>-<AAAA-MM-DD>` (por ejemplo, para la primera versión, `V1-` más la fecha de vigencia que se entregue), sin reutilizar nunca un valor anterior; (4) se reinicia la API y se comprueba con una solicitud de prueba que la fila nueva de `consentimientos_datos` trae la versión nueva. Qué hacer con las autorizaciones ya guardadas con el borrador: **PENDIENTE D-85**. | Documentar: CONFIRMADO (encargo). Formato y pasos: PROPUESTO | MAN (revisión del documento) |

---

## 3. Contrato de API (cambios y rutas nuevas)

Formato de error común: `{ "error": string, "errores"?: { campo: string[] } }`.

### 3.1 `POST /api/auth/registro/comprador` (cambia)
- Anónima. Límite: cupo **Registro** (CA-P9-38, 39).
- Cuerpo: el actual (`nombreCompleto`, `email`, `telefono`, `password`) **+ `autorizacionDatos: boolean`** (obligatorio, debe ser `true`).
- Respuestas: 201 (igual que hoy); 400 con `errores.autorizacionDatos` (CA-P9-08) además de los actuales; 409 correo repetido (igual); 429 (nuevo); 500.
- Efecto nuevo: fila en `consentimientos_datos` (`REGISTRO_COMPRADOR`, `entidad = 'Usuario'`, `entidad_id = usuario_id = id nuevo`) en la misma transacción.

### 3.2 `POST /api/auth/registro/asesor` (cambia)
- Igual que 3.1, con el cuerpo actual del asesor + `autorizacionDatos`, formulario `INSCRIPCION_ASESOR`. Comparte el cupo **Registro** con 3.1. 409 de correo o documento sin cambios.

### 3.3 `POST /api/auth/login` (cambia)
- Solo se añade el cupo **Inicio de sesión** (CA-P9-40) → 429 con `{ error }` y `Retry-After`. 400, 401, 403 y 423 sin cambios.

### 3.4 `GET /api/portal/contacto` (cambia, compatible)
- Anónima, solo GET. Se añade `empresa: { razonSocial: string, nit: string, direccion: string } | null` (CA-P9-33). `nit` normalizado (`800197268-4`); el formato con puntos lo pone el frontend.

### 3.5 `GET /api/admin/parametros/empresa` (nueva)
- `SoloAdministrador`. 200: `{ razonSocial: string|null, nit: string|null, direccion: string|null, completo: bool, configurado: bool, actualizadoEn: string|null, actualizadoPor: string|null }` (`actualizadoPor` = nombre, como en contacto). 401 / 403 según CA-P9-32.

### 3.6 `PUT /api/admin/parametros/empresa` (nueva)
- `SoloAdministrador`. Cuerpo: `{ razonSocial?: string|null, nit?: string|null, direccion?: string|null }`. Vacío o espacios = `null`.
- 200 con el cuerpo de 3.5; 400 `{ error: "Revisa los datos marcados.", errores: { razonSocial|nit|direccion: [...] } }`; 401; 403.
- Almacenamiento: `configuracion_sistema`, clave **`portal.empresa`**, `valor_json` `{ "razonSocial": …, "nit": …, "direccion": … }`; auditoría `Parametro` / `portal.empresa` / `CAMBIAR`. Sin migración.

### 3.7 Configuración nueva (`appsettings.json`)
```json
"LimitesAuth": {
  "RegistroLimiteVentana": 5, "RegistroVentanaMinutos": 60, "RegistroLimiteDiario": 20,
  "LoginLimiteVentana": 20, "LoginVentanaMinutos": 15
}
```
`PoliticaDatos:Version` no cambia de valor (CA-P9-62).

### 3.8 Rutas del frontend
- Nueva `#/terminos`. Pantalla nueva "no encontrada" (sin hash propio: es el resultado de `leerRuta` para rutas desconocidas que empiezan por `/`).

---

## 4. Trazabilidad

| HU / RN / D | Criterios | Prueba prevista |
|---|---|---|
| HU-062, RN-SE-004 (Proteger datos personales) | CA-P9-01 a 16, 62 a 66 | API: `RegistroConsentimientoTests` (nuevo); COMP: `RegistroForm.test.tsx`; UNIT: versión |
| D-44 (pendiente técnico: casilla, prueba, páginas, enlaces) | CA-P9-01 a 08, 15, 17 a 22 | API + COMP |
| HU-086, RN-PA-006 (Textos legales; solo aviso, sin textos) | CA-P9-17 a 23 | COMP: `Terminos.test.tsx`, `Privacidad.test.tsx`, `PiePortal.test.tsx`; UNIT: `rutas.test.ts` |
| HU-085, RN-PA-005 (Información corporativa), D-63 B | CA-P9-24 a 37 | API: `ParametrosEmpresaTests` (nuevo); UNIT: NIT en C# y TS; COMP: `AdminParametros.test.tsx`, `PiePortal.test.tsx` |
| HU-081, RN-PA-001, D-63 A (contacto vigente) | CA-P9-37, 57 a 61 | COMP: formularios de contacto, avisos, crédito, búsqueda, "Mis solicitudes"; UNIT: búsqueda del número fijo |
| HU-059, RN-SE-001 (Inicio de sesión) | CA-P9-40, 41, 45, 46 | API: `LimitesAuthTests` (nuevo), regresión de `AuthTests`; COMP: `LoginForm` |
| RN-SE-006 (Control de accesos) | CA-P9-32, 44, 51 | API: matriz de roles; COMP |
| INF-2 (limitador por IP e IPv6 /64) | CA-P9-38 a 44, 47 | API |
| RN-SE-005 (Consulta pública) | CA-P9-17, 22, 48 a 51 | UNIT: `rutas.test.ts`; COMP: `App.test.tsx` |
| Encargo P9 punto 6 (SEO) | CA-P9-52 a 56 | MAN + build + COMP |
| D-42 (contacto y dirección no publicada) | CA-P9-34, 35, 59 | COMP (PENDIENTE D-83) |
| D-80 (base nueva en producción) | CA-P9-62, 64 | — (contexto de D-85) |

---

## 5. Decisiones registradas (DECISIONES.md)

- **D-83 TOMADA (2026-11-16): A** — El pie muestra razón social, NIT y dirección de notificaciones cuando el administrador complete los tres datos; reemplaza, para la dirección de notificaciones, lo dicho en D-42. Afecta CA-P9-34 y 35.
- **D-84 TOMADA (2026-11-16): A** — Por ahora solo el enlace a los términos; la casilla "Acepto los términos" se agrega cuando el abogado apruebe el texto. Afecta CA-P9-16.
- **D-85 TOMADA (2026-11-16): A** — Las autorizaciones dadas con la versión borrador se conservan con su versión; se le pregunta al abogado si hace falta pedir una nueva. Afecta CA-P9-66; no bloquea P9.
- **D-86 TOMADA (2026-11-16, documental, coordinador: las migraciones futuras toman el siguiente número libre, 018 en adelante)** — D-75 B y D-76 B citan una "migración 017" que ya ocupa `017_categorias_base_SUBIR.sql`.

## 6. Notas para el arquitecto y QA

- Las pruebas de registro existentes enviarán 400 al exigir `autorizacionDatos`: hay que añadir el campo a sus cuerpos y subir los cupos de `LimitesAuth` en `CamionApiFactory`, como ya se hace con `Contactos`.
- `MENSAJE_CONTACTO_FALLO` se construye hoy en la capa `api/` (`contactos.ts:145`, `avisos.ts:194`, `solicitudes.ts:407`), que no conoce el contacto vigente; el componente debe poder armar el texto con el número vigente (CA-P9-57).
- `useContactoPortal` hace una petición por componente que lo usa; con el pie, las páginas legales y los mensajes de error puede haber varias por pantalla. No es un criterio, pero conviene una caché en memoria del módulo.
- Observación fuera de alcance: el 409 "Este correo ya está registrado" permite saber si un correo tiene cuenta; el límite por IP de CA-P9-38 lo mitiga, no lo elimina.
