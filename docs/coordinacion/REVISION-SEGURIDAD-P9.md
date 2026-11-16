# Revisión de seguridad P9 "Prepublicación"

- Rama: `feature/p9-prepublicacion` (HEAD `4cf5480`) contra `origin/main` (`c0660ae`). 14 commits, 74 archivos.
- Fecha: 2026-11-16. Revisor: agente DevOps y seguridad.
- Alcance: LimitesAuth, autorización de datos en el registro, PoliticaDatos, `/api/admin/parametros/empresa`, frontend (pie, páginas legales, registro, SEO), dependencias.
- No se leyeron ni imprimieron valores de secretos. No se ejecutaron `dotnet test` ni `npm test`; eso lo hace QA.

## Veredicto

**APROBADO CON OBSERVACIONES.** No hay hallazgos altos. Ningún hallazgo bloquea la entrega. Los medios son de configuración del despliegue y deben estar resueltos antes de abrir el portal al público (PP.6).

## Comprobaciones ejecutadas

| Comprobación | Resultado |
|---|---|
| `npm audit` (frontend) | 0 vulnerabilidades |
| `dotnet list Camion.Api.csproj package --vulnerable --include-transitive` | Sin paquetes vulnerables |
| `dotnet list Camion.Api.Tests.csproj package --vulnerable --include-transitive` | Sin paquetes vulnerables |
| Dependencias nuevas (csproj, package.json, package-lock.json) | Ninguna. El limitador usa `Microsoft.AspNetCore.RateLimiting`, que ya viene en .NET 8 |
| Secretos, cadenas de conexión, tokens o llaves en el diff | Ninguno |
| Respaldos o archivos `.bak`/`.env` en el diff | Ninguno (`frontend/respaldos/` sigue sin seguimiento y no se tocó) |
| Datos personales en el diff | Solo datos ficticios de prueba (`3001234567`, `Ana Tipo`) y el contacto comercial ya publicado (D-42). El NIT `800197268-4` de los ejemplos es el NIT público de la DIAN |
| Migraciones | Ninguna (D-86: `CK_Consent_Formulario` ya admite los formularios de registro) |

## Verificaciones sin hallazgo

- **X-Forwarded-For falso (INF-2)**: no se puede usar para saltarse el límite. `UseForwardedHeaders` solo se activa si hay `Proxy:KnownProxies` o `Proxy:KnownNetworks` (`backend/Camion.Api/Program.cs:283`). Además, se vacían las listas por defecto de loopback (`backend/Camion.Api/Seguridad/ConfiguracionServidor.cs:70-71`) y `ForwardLimit` vale 1 por defecto (`ConfiguracionServidor.cs:82-85`). Si no hay proxy configurado, el encabezado se ignora. Si lo hay, solo se acepta el salto más a la derecha que añadió el proxy de confianza, y una conexión directa a Kestrel desde una IP desconocida no se reescribe. Va antes de `UseRateLimiter` (`Program.cs:333`).
- **IPv6 /64**: `ClaveIp` agrupa las IPv6 por su /64 y pasa a IPv4 las IPv4 mapeadas (`ConfiguracionServidor.cs:101-109`). Lo comparten los cupos `auth-corta:` y `auth-dia:` (`Program.cs:169-186`).
- **El 429 no cuenta como fallo de la cuenta**: el limitador rechaza la petición antes de llegar al endpoint, sin leer el cuerpo, así que no llega a `AccessFailedAsync`. El cuerpo es fijo (`backend/Camion.Api/Auth/LimitesAuth.cs:26-29`) y no dice si el correo existe. El log no lleva ni correo ni IP (`Program.cs:214`). `Retry-After` se envía en segundos.
- **Valores de producción**: `backend/Camion.Api/appsettings.json` fija 5 registros cada 60 min, 20 al día, y 20 inicios de sesión cada 15 min. Siguen vigentes el bloqueo de Identity tras 5 fallos y 15 min (`Program.cs:56-58`) y el aviso en `docs/GUIA-PUBLICACION.md:89-90`. Las pruebas suben los cupos solo en `CamionApiFactory`.
- **Atomicidad de la autorización**: el consentimiento se guarda en la misma transacción que la cuenta, el perfil y el rol (`backend/Camion.Api/Endpoints/AuthEndpoints.cs:85-87` en el comprador; para el asesor, junto con la auditoría en `:170-173`). Si falla, se hace rollback.
- **Rutas alternativas sin autorización**: solo se crean usuarios en `AuthEndpoints.cs:70` y `:146`, y las dos exigen `autorizacionDatos == true` (`backend/Camion.Api/Auth/ValidacionAuth.cs:61-65`, `:82`). `promover-admin` y `recuperar-admin` son comandos de consola que modifican cuentas existentes y no crean ninguna. No hay `Users.Add` en ningún otro sitio.
- **PoliticaDatos**: un único punto de lectura. Una versión mal formada impide arrancar y el mensaje de error no repite el valor configurado (`backend/Camion.Api/Auth/PoliticaDatos.cs:39-47`). Si es un borrador fuera de Development, se escribe un aviso en el log (`Program.cs:260-262`).
- **`/api/admin/parametros/empresa`**: el grupo exige `Roles.PoliticaSoloAdministrador` (`backend/Camion.Api/Endpoints/ParametrosEndpoints.cs:29`). Se valida todo antes de escribir: largo de 2 a 150 y de 5 a 200, sin `<` `>` ni caracteres de control, y NIT con dígito de verificación de la DIAN (`backend/Camion.Api/Parametros/ParametrosEmpresa.cs:91-142`). Al leer se vuelve a validar (`ParametrosEmpresa.cs:55-58`). La auditoría guarda antes y después en la misma transacción (`ParametrosEndpoints.cs:84-101`). La API pública solo devuelve los datos si están los tres.
- **XSS en el pie**: React escapa el texto. En el código nuevo no hay `dangerouslySetInnerHTML` ni `innerHTML` (`frontend/src/components/PiePortal.tsx:170-175`). El frontend vuelve a validar la empresa y el enlace de WhatsApp (`frontend/src/api/parametros.ts:204-222`).
- **`target="_blank"`**: todos los enlaces de `frontend/src` llevan `rel="noopener noreferrer"`. Se revisaron uno por uno, incluidos los anteriores a P9.
- **robots.txt / noindex / favicon**: `frontend/public/robots.txt` bloquea `/api/` y `/health` y no lleva Sitemap. `frontend/src/seo.ts` añade y quita solo su propia etiqueta. El favicon es un SVG estático sin scripts ni referencias externas (`frontend/public/favicon.svg`).
- **Casilla de autorización**: empieza desmarcada (`frontend/src/components/RegistroForm.tsx:72`) y sin ella no se llama a la API (`RegistroForm.tsx:166-172`).

## Hallazgos

### Altos

Ninguno.

### Medios

**M-1. Detrás de un proxy sin `Proxy:KnownProxies`, todos los usuarios comparten un mismo cupo de inicio de sesión.**
`backend/Camion.Api/Program.cs:283` y `Program.cs:179`. Si se publica detrás de nginx, IIS o una CDN sin configurar el proxy, `RemoteIpAddress` es la IP del proxy. Entonces 20 inicios de sesión cada 15 min y 20 registros al día valen para todo el portal: cualquiera puede bloquear el acceso a todos (denegación de servicio). Pasa lo mismo con Cloudflare + nginx si `ForwardLimit` se deja en 1. Ya está en la lista de `docs/GUIA-PUBLICACION.md:50` y `:248-253`, pero la API arranca sin avisar.
*Recomendación*: antes de PP.6, verificar en el servidor que `Proxy__KnownProxies` (o `KnownNetworks`) y `Proxy__ForwardLimit` coinciden con la topología real. Opcional en una fase posterior: escribir un aviso en el log al arrancar, fuera de Development, si no hay proxy configurado, igual que con `PoliticaDatos`.

**M-2. Se pueden enumerar correos (409) y documentos de asesor (409) en el registro. El límite por IP lo reduce, pero no lo elimina.**
`backend/Camion.Api/Endpoints/AuthEndpoints.cs:52-53` ("Este correo ya está registrado.") y `AuthEndpoints.cs:128-129` ("Ya existe un asesor con ese documento."). Con el límite, una sola IPv4 o un /64 puede probar como mucho 20 valores al día. Además, cada intento con un correo nuevo crea una cuenta real, lo que hace ruido y queda auditado. Un atacante con muchas IP (proxies residenciales, botnet) sigue pudiendo hacerlo, y un número de documento confirmado es un dato personal. Severidad residual: **media-baja**. Es el riesgo conocido del §6 de los criterios y no bloquea.
*Recomendación*: aceptar el riesgo y dejarlo registrado en `DECISIONES.md`. Si más adelante hay verificación del correo, sustituirlo por una respuesta genérica ("Si el correo no estaba registrado, te enviamos un enlace…"). Vigilar en el log `Auditoria limite_auth rechazado cupo=registro`.

### Bajos

**B-1. No hay cupo diario de inicio de sesión.** `backend/Camion.Api/Program.cs:195-196` (`null` para el login). Una IP puede hacer unos 1.900 intentos al día repartidos entre muchas cuentas (*credential stuffing*). El bloqueo por cuenta tras 5 fallos frena el ataque contra una cuenta concreta, pero no el ataque repartido. *Recomendación*: valorar `LoginLimiteDiario` (por ejemplo, 200) en una fase posterior.

**B-2. Cada 429 escribe un aviso en el log.** `Program.cs:214`. Un cliente insistente puede inflar el log. *Recomendación*: aceptable. Si molesta, muestrear los avisos o bajarlos a `Information`.

**B-3. La validación de texto de la empresa no rechaza los caracteres Unicode de formato.** `backend/Camion.Api/Parametros/ParametrosEmpresa.cs:119-120`. `char.IsControl` deja pasar U+202E (cambio de dirección del texto) y los caracteres de ancho cero, que se mostrarían en el pie público. No hay XSS, solo la posibilidad de alterar cómo se ve el texto, y solo un administrador puede introducirlos. *Recomendación*: rechazar también `UnicodeCategory.Format` en `Texto()`.

**B-4. Con dos PUT `/empresa` simultáneos sin fila previa, uno de los dos devuelve 500.** `backend/Camion.Api/Endpoints/ParametrosEndpoints.cs:88-92`. Los dos insertan la clave `portal.empresa` y uno choca con la clave primaria. No se pierden datos y lo atiende el manejador global, pero la respuesta debería ser 409. *Recomendación*: capturar `DbUpdateException` y responder 409, o reintentar.

**B-5. El frontend envía `autorizacionDatos: true` como literal.** `frontend/src/components/RegistroForm.tsx:149` y `:154`. Ahora es correcto porque la guarda está en `:166-172`, pero si esa guarda se cambia en el futuro se enviaría `true` sin que la persona haya marcado la casilla. La API lo comprueba, pero no puede saber si la casilla se marcó de verdad. *Recomendación*: enviar el estado `autorizacion` en vez del literal.

**B-6. La partición "sin-ip" la comparten todas las peticiones sin IP remota.** `backend/Camion.Api/Seguridad/ConfiguracionServidor.cs:103`. Con Kestrel en TCP no ocurre. Solo afectaría a un socket Unix o a un named pipe detrás de IIS in-process mal configurado. *Recomendación*: informativo. Comprobarlo en PP.6.

## Bloqueos

Ninguno. M-1 queda como comprobación obligatoria de la lista de PP.6 antes de abrir al público.
