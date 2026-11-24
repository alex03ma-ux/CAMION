# CAMION — Venta de Camiones – Financiamos tu Camión

Portal **Red de Venta – Financiamos Tu Camión** (línea base funcional v3.0). Desde P10 el nombre visible es **"Venta de Camiones – Financiamos tu Camión"**. La empresa es **VC Automotores**; "VCA" es solo el diseño del logo (D-87, PR #19).

## 1. Qué es

Un portal de **intermediación** (D-45). Los vehículos son de terceros y los publican asesores aprobados. La plataforma **solo pone en contacto** al interesado con el asesor o con el administrador. La gestión de créditos ante bancos y de pólizas de seguro es **interna** y no se hace en la plataforma (D-45, D-54, D-55).

Estados que usa esta documentación: **IMPLEMENTADO** (el código existe), **PROBADO** (pruebas ejecutadas y aprobadas), **DOCUMENTADO** (descrito, no necesariamente programado), **PROPUESTO** (pendiente de confirmar) y **BLOQUEADO**.

## 2. Qué funciona hoy

`main` (en `d71a558`) contiene P0 a P10 e INF-1 a INF-4, unidos mediante las PR #2 a #20. Todo lo de esta sección está **IMPLEMENTADO y PROBADO** con pruebas automáticas. Además, la responsable hizo una prueba manual de cada fase. Las de P6, P7 y P8 fueron **parciales**: lo que no se probó a mano lo cubren las pruebas automáticas. El 2026-11-23/24 hizo la **prueba integral manual** de todo el portal ([`docs/manual/PRUEBA-INTEGRAL.md`](docs/manual/PRUEBA-INTEGRAL.md)) y confirmó que todo funcionó; el coordinador verificó el resultado en la base con consultas de solo lectura. Quedó sin probar la reasignación de solicitudes. No hay pruebas E2E. Detalle por fase en [`docs/coordinacion/BITACORA.md`](docs/coordinacion/BITACORA.md) y [`docs/coordinacion/ESTADO-REAL.md`](docs/coordinacion/ESTADO-REAL.md).

**Visitante (sin cuenta)**
- Ver el catálogo: inicio con destacados y recientes, búsqueda, filtros y ficha del vehículo (P3, P4b).
- Contactar al vendedor desde la ficha, con casilla de autorización de datos, o escribirle por WhatsApp (P5).
- Enviar los avisos "Vende tu camión" y "Compra tu seguro todo riesgo" (P5b, D-6).
- Pedir un crédito desde la portada o desde una ficha, y llenar "Busco un vehículo" (P6).

**Comprador** (registro opcional, acceso inmediato y de solo lectura; D-3)
- Todo lo del visitante, con la sesión abierta.
- Ver en "Mis solicitudes" lo que envió con su sesión. Es una vista de solo lectura (P6, D-59).

**Asesor** (su cuenta queda pendiente hasta que el administrador la aprueba; P2)
- Gestionar su inventario de vehículos con fotos (P4a).
- Gestionar sus publicaciones y destacados según su plan, marcar vendido y revertir la venta (P4b).
- Consultar "Mi suscripción" (P4p).
- Atender en "Mis contactos" solo los contactos de sus propios vehículos con plan vigente, o los que el administrador le reasigne (P5, D-7). No ve avisos, créditos ni búsquedas.
- Consultar "Mis ventas" y, en "Mi suscripción", el aviso "Tu plan vence en N días" con el botón de WhatsApp para pedir la renovación (P7, D-64).
- Consultar "Mis indicadores", solo de lo suyo y sin exportar (P8, D-70).

**Administrador**
- Aprobar, rechazar, reconsiderar, bloquear y desbloquear cuentas (P2).
- Gestionar planes, suscripciones, catálogos de marcas y modelos y la foto de portada (P4p, D-42).
- Gestionar el inventario y las publicaciones de todos, y reasignar vehículos con motivo (P4b, D-34).
- Atender todas las solicitudes en "Contactos": contactos, avisos, créditos y búsquedas. Puede añadir notas, cerrar, reabrir, reasignar contactos (con auditoría) y relacionar vehículos con una búsqueda (P5, P5b, P6).
- El primer administrador se crea con el comando local `promover-admin` (D-13). Ver [`docs/GUIA-ADMINISTRACION.md`](docs/GUIA-ADMINISTRACION.md).
- P7 (migración 016 aplicada): panel "Inicio" con contadores enlazados a sus listas (D-64); historial de ventas, vigentes y revertidas (D-62); registro y anulación de los pagos del plan, cobrados fuera del portal, y anular un pago deshace lo que dio (D-61 B, D-66, D-67); "Parámetros" para cambiar el WhatsApp, el correo, el horario, Instagram y TikTok del portal (D-63). Guía en [`docs/manual/P7-administracion.md`](docs/manual/P7-administracion.md).
- P8 (PR #15): "Reportes" (vehículos y publicaciones, solicitudes, créditos, ventas, y suscripciones y pagos), con filtros, sin datos personales de clientes y con "Descargar CSV" auditado (D-68, D-71); "Indicadores" con comparación con el período anterior y 4 gráficos (D-69); "Actividad" de solo lectura de un vehículo y de una suscripción (D-72); recuperación de contraseña **asistida por el administrador**, con un enlace de un solo uso que vence en 60 minutos y se envía por WhatsApp o Gmail (D-73 D, D-78). Para una cuenta de administrador se usa el comando local `recuperar-admin` (D-79 A). Guía en [`docs/manual/P8-reportes.md`](docs/manual/P8-reportes.md).

**Infraestructura** (INF-1 a INF-3, PR #16): CI en GitHub Actions, comprobaciones de arranque para el servidor (proxy, https, claves) y corrección de las pruebas intermitentes.

**INF-4** (PR #20): con un 401 en una ruta con sesión, el aviso "Tu sesión terminó. Inicia sesión de nuevo." salía dos veces; ahora sale una sola. También se corrigieron las pruebas intermitentes de `P5Leads`.

**P9 "Prepublicación"** (en `main`; sin migración)
- Está **IMPLEMENTADO y PROBADO** con pruebas automáticas. La bitácora no registra la prueba manual de la responsable, la prueba contra la API real, las pruebas E2E ni la revisión a 375 px. Guía de prueba en [`docs/manual/P9-prepublicacion.md`](docs/manual/P9-prepublicacion.md).
- Registro de comprador e inscripción de asesor con la **casilla de autorización de datos**, sin marcar; la API la vuelve a exigir y guarda la prueba con la versión de la política (HU-062, D-44). Junto a la casilla hay un enlace a los términos, pero no una casilla para aceptarlos (D-84 A).
- Páginas de términos y de privacidad con la franja "En revisión legal", **sin** el texto de los borradores, y enlaces a las dos en el pie (HU-086).
- Administrador: en "Parámetros", la sección "Datos de la empresa" (razón social, NIT con dígito de verificación y dirección de notificaciones). Se ven en el pie y en las páginas legales solo cuando los tres están completos (HU-085, D-83 A).
- Límite de intentos por conexión: 5 registros por hora y 20 al día, y 20 inicios de sesión cada 15 minutos; al pasarlo, mensaje de espera (HU-059, RN-SE-001).
- Pantalla "Página no encontrada" para direcciones que no existen; meta description y favicon.
- Los mensajes de crédito, búsqueda, seguro y "Mis solicitudes" muestran el WhatsApp vigente de "Parámetros", no un número fijo.
- Revisión de seguridad: **APROBADO CON OBSERVACIONES**, sin hallazgos altos ([`REVISION-SEGURIDAD-P9.md`](docs/coordinacion/REVISION-SEGURIDAD-P9.md)). M-1 (configurar el proxy en el servidor) es obligatorio antes de abrir al público. B-5 se corrigió en la rama.

**P10 "Marca y visibilidad"** (en `main`, PR #18; sin migración; D-87 a D-92)
- Está **IMPLEMENTADO y PROBADO** con pruebas automáticas (ver §5). La responsable hizo su prueba manual el 2026-11-17 y lo repasó en la prueba integral. Las pruebas E2E no se han ejecutado. Guía de prueba en [`docs/manual/P10-marca-visibilidad.md`](docs/manual/P10-marca-visibilidad.md).
- **Lo que nota quien usa el portal:**
  - las direcciones ya no llevan `#`: por ejemplo `/vehiculo/<slug>`, `/camiones`, `/cuenta`. Cada vehículo tiene su propia dirección, que se puede recargar (F5) y compartir. Los enlaces viejos con `#/` se convierten solos a la dirección nueva;
  - categorías con dirección propia: `/carros`, `/camiones`, `/carrocerias` y `/maquinaria`;
  - logo de **VC Automotores** (diseño "VCA"): el horizontal en el encabezado y el cuadrado en el pie; favicon con la cabeza del toro (D-91 A);
  - nombre **"Venta de Camiones – Financiamos tu Camión"**; títulos de pestaña con el sufijo "| Financiamos tu Camión" ("tu" en minúscula); el pie dice "© <año> VC Automotores" (PR #19);
  - el título principal de la portada es **"Venta de camiones usados"** y la frase anterior queda como subtítulo (D-92);
  - el mensaje de "Escribir por WhatsApp" de la ficha termina con la dirección del vehículo (nota sobre D-47);
  - el enlace de recuperación de contraseña tiene la forma `<portal>/restablecer-contrasena#codigo=<código>`; los enlaces viejos siguen sirviendo mientras no venzan.
- **Para Google y las redes:** título, descripción, `canonical` y `robots` por página; `robots.txt` y `sitemap.xml` generados por la API; datos estructurados (`Organization` en la portada, sin dirección, D-90 C; `Product`/`Vehicle` y migas en la ficha); vista previa genérica en todas las páginas y, en el servidor, la API entrega el HTML de `/vehiculo/<slug>` con la foto y el título del vehículo (D-88 A).
- **La indexación está apagada** por configuración hasta abrir al público (D-89 A): `robots.txt` dice `Disallow: /`, no hay sitemap y todas las páginas llevan `noindex`. La vista previa con la foto del vehículo al pegar el enlace en WhatsApp **solo se puede probar con el portal publicado** en su dominio (D-81).
- Revisión de seguridad: **APROBADO CON OBSERVACIONES**, sin hallazgos altos ([`REVISION-SEGURIDAD-P10.md`](docs/coordinacion/REVISION-SEGURIDAD-P10.md)). M-1, B-1, B-2 y B-6 corregidos en el código; B-3 y B-4 corregidos en la guía de publicación; B-5 cubierto por una prueba de QA.

**Pendiente** (ver [`docs/coordinacion/BACKLOG.md`](docs/coordinacion/BACKLOG.md))
- **Antes de publicar en internet** (detalle en `docs/coordinacion/ESTADO-REAL.md`, "Preparación para publicar"):
  - D-81: hosting y dominio (PENDIENTE de la responsable);
  - D-44: revisión del abogado de la política y de los términos, y los **datos de la empresa** que debe entregar la responsable (razón social, NIT, dirección de notificaciones y dominio). Ningún formulario sale a internet sin esa revisión. Después hay que publicar los textos aprobados y cambiar `PoliticaDatos:Version`;
  - la **vista previa al compartir** (tarjeta con foto y título al pegar el enlace en WhatsApp o Facebook) y los validadores de Google y schema.org (CA-P10-69 y 81): requieren el portal publicado en su dominio (D-81);
  - poner en marcha el servidor según [`docs/GUIA-PUBLICACION.md`](docs/GUIA-PUBLICACION.md): variables, base nueva (D-80 A) con el 017 aprobado, primer administrador, planes y contacto, proxy (incluido M-1 de P9), https y respaldos programados;
  - al abrir al público: encender la indexación con las dos claves juntas.
- Prueba integral: falta probar a mano la reasignación de solicitudes (D-7).
- D-91 B: cambiar los archivos del logo cuando lleguen los originales con transparencia y la versión para fondo oscuro (no requiere programar).
- D-82: si se implementan los favoritos (PENDIENTE de la responsable). P8b (promociones, D-75 B): opcional.
- Otros: P2b ("Solicitar ser asesor"), P6-B (hallazgos bajos de seguridad) y B-2 y B-3 de la revisión de P8.
- No hay servicio de correo (D-73): la recuperación de contraseña es asistida por el administrador, no automática.
- La base local `camion_ventas` conserva sus datos de prueba: producción se crea en una base nueva (D-80 A).

## 3. Tecnologías

| Parte | Tecnología | Carpeta | Puerto |
|---|---|---|---|
| Frontend | React 19 + Vite 7 + TypeScript | `frontend/` | 5173 (proxy de `/api`, `/media` y `/health`; con P10 también `/robots.txt` y `/sitemap.xml`) |
| Backend | ASP.NET Core .NET 8 (minimal APIs) + Identity/JWT, EF Core con Pomelo | `backend/Camion.Api/` | 5080 |
| Base de datos | MariaDB 10.4 (XAMPP), base `camion_ventas` | `database/` | 3306 |
| Pruebas | xUnit (API, con SQLite en memoria) y Vitest | `backend/Camion.Api.Tests/`, `frontend/tests/` | — |

`global.json` fija el SDK 8 de .NET.

## 4. Puesta en marcha en un equipo nuevo

### Requisitos
- SDK de .NET 8 (versión de `global.json` o una 8.0.x posterior).
- Node.js 20.19+ o 22.12+ (lo exige Vite 7).
- XAMPP con MariaDB encendido en el puerto 3306.

### Secretos locales
La API **no arranca** sin dos claves. Se guardan con `dotnet user-secrets` desde `backend/Camion.Api`, nunca en Git ni en `appsettings.json`.

| Clave | Para qué sirve |
|---|---|
| `ConnectionStrings:CamionDatabase` | Conexión a la base `camion_ventas` |
| `Jwt:Key` | Firma de los tokens; mínimo 32 bytes |

```powershell
dotnet user-secrets set "ConnectionStrings:CamionDatabase" "<TU_CADENA_DE_CONEXION>"
dotnet user-secrets set "Jwt:Key" "<TU_CLAVE_DE_32_BYTES_O_MAS>"
```

Para armar la cadena y generar la clave, ver [`backend/GUIA-CONEXION-MARIADB.md`](backend/GUIA-CONEXION-MARIADB.md) y [`backend/GUIA-AUTENTICACION.md`](backend/GUIA-AUTENTICACION.md). No pegues estos valores en chats ni en documentos.

### Configuración opcional (no secreta)
Tienen un valor por defecto que sirve en el equipo local. Se cambian en `appsettings`, con `dotnet user-secrets` o con variables de entorno (por ejemplo `Recuperacion__UrlPortal`).

| Clave | Para qué sirve | Por defecto | Regla |
|---|---|---|---|
| `Media:Ruta` | Carpeta de las fotos | `C:\CAMION_media` | — |
| `DataProtection:RutaClaves` | Carpeta de las claves que firman y cifran los enlaces de recuperación (P8, D-78) | `C:\CAMION_claves` | Debe estar **fuera del repositorio**; si no, la API no arranca. En Windows las claves se cifran con DPAPI. |
| `Recuperacion:UrlPortal` | Dirección del frontend con la que empieza el enlace de recuperación (P8) | `http://localhost:5173` | Fuera de `Development` debe empezar por `https://`; si no, la API no arranca. |
| `PoliticaDatos:Version` | Versión de la política que se guarda con cada autorización de datos | `BORRADOR-2026-11-04` | Una versión mal formada impide arrancar. Fuera de `Development`, una versión de borrador deja un aviso en el log. Se cambia cuando el abogado apruebe la política (D-44, D-85 A). |
| `LimitesAuth:*` (P9) | Límite de intentos por conexión en el registro y el inicio de sesión | 5 registros cada 60 min y 20 al día; 20 inicios de sesión cada 15 min | Detrás de un proxy, sin `Proxy:KnownProxies` el límite pasa a ser común para todo el portal (M-1 de P9). |
| `Portal:UrlPublica` (P10) | Dirección pública del portal para `sitemap.xml`, `robots.txt`, `canonical` y la vista previa que entrega la API | vacía: se usa `Recuperacion:UrlPortal` | Absoluta, sin ruta, consulta ni fragmento; fuera de `Development`, `https://`. No usar `Portal:Origen`, que activa CORS. |
| `Portal:PermitirIndexacion` (P10, D-89 A) | Deja que Google indexe el portal | `false` | Solo `true` o `false`; otro valor impide arrancar. Se enciende al abrir al público, **junto con** `VITE_PERMITIR_INDEXACION=true` al compilar el frontend. |
| `Portal:RutaIndexHtml` (P10, D-88 A) | Ruta del `index.html` publicado del frontend; con ella la API atiende `/vehiculo/<slug>` con las etiquetas del vehículo | vacía: la API no atiende `/vehiculo/` | Debe ser un archivo `.html` de tamaño razonable y con las marcas `camion:meta`. Fuera de `Development`, si está puesta y el archivo no existe, la API no arranca. En el equipo local no hace falta. |
| `LimitesSeo:*` (P10, M-1) | Cupo por conexión de `/sitemap.xml` y `/vehiculo/*`, y minutos que se reutiliza el sitemap generado | `LimiteVentana` 120, `VentanaMinutos` 1, `SitemapCacheMinutos` 10 | — |

Al **compilar** el frontend (no en la API) hay dos variables nuevas de P10: `VITE_URL_PUBLICA` (la misma dirección `https://` que `Portal:UrlPublica`; sin ella no se escriben `og:url`, `og:image` ni `twitter:image`) y `VITE_PERMITIR_INDEXACION` (solo el texto exacto `true` la enciende; si no, todas las páginas llevan `noindex`). En el equipo local se dejan vacías.

Para publicar en un servidor hay más claves (proxy, https, CORS y certificado de las claves). Fuera de `Development` las tres primeras de esta tabla son **obligatorias**: sin ellas la API no arranca. La lista completa, solo con nombres, está en [`.env.example`](.env.example), y el procedimiento en [`docs/GUIA-PUBLICACION.md`](docs/GUIA-PUBLICACION.md).

La carpeta de claves se crea sola la primera vez que arranca la API con P8. **No la borres ni la subas a Git**: si se pierde, los enlaces de recuperación pendientes dejan de valer (las sesiones no se ven afectadas). Detalle en [`backend/GUIA-AUTENTICACION.md`](backend/GUIA-AUTENTICACION.md) §12.

### Base de datos
1. Antes de cada script, haz un **respaldo** con `mysqldump --result-file=...` en `C:\Respaldo_CAMION\`. El comando está en la cabecera de cada migración. Nunca sobrescribas un respaldo.
2. **Detén la API.**
3. En phpMyAdmin, **selecciona primero la base `camion_ventas`**: los archivos `_SUBIR` no traen `USE`. Luego entra en *Importar*, elige el archivo y ejecútalo.
4. Importa los scripts en este orden:
   - `database/002_esquema_inicial_mariadb_10_4.sql` (esquema inicial);
   - por número, `database/migraciones/` y `database/catalogos/`: 003, 004, 005, 006, 007, 008, 009, 010, 011, 012, 013, 014, 015 y 016 (todas aplicadas en `camion_ventas`; P8 y P9 no añaden migración).
5. Si un script tiene archivo `_SUBIR`, importa **solo ese archivo**. Si no lo tiene, sigue las instrucciones de su cabecera. En phpMyAdmin, importar la 004 completa falló; hubo que importar solo su sección SUBIR (ver BITACORA, P4a).
6. Los archivos `_VERIFICAR_Y_BAJAR` y las secciones BAJAR **no se importan al instalar**.
7. `database/demo/demo_p3_catalogo.sql` carga datos ficticios. Es opcional y sirve solo para pruebas (D-18, D-24). **Nunca** se importa en producción.
8. Para una base **nueva** (producción, D-80 A), el orden exacto, con el script `database/catalogos/017_categorias_base_SUBIR.sql` (categorías base; PROPUESTO), está en [`docs/GUIA-PUBLICACION.md`](docs/GUIA-PUBLICACION.md) §4.

### Arrancar la API
Desde `backend/Camion.Api`:

```powershell
dotnet run
```

Si Windows (Smart App Control) bloquea `Camion.Api.exe`, compila con `dotnet build` y arranca la `.dll` (ver BITACORA, P5b):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5080"
dotnet bin\Debug\net8.0\Camion.Api.dll
```

Para comprobar que funciona, abre http://localhost:5080/health y http://localhost:5080/health/database.

### Arrancar el frontend
Desde `frontend/`, en otra terminal:

```powershell
npm install
npm run dev
```

Abre http://localhost:5173/. El inicio es el catálogo. Desde P10 las direcciones van sin `#`: "Mi cuenta" está en http://localhost:5173/cuenta, el catálogo en http://localhost:5173/vehiculos y cada vehículo en http://localhost:5173/vehiculo/<slug>. Cualquiera de ellas se puede recargar (F5): Vite responde la aplicación. `/api`, `/media`, `/health`, `/robots.txt` y `/sitemap.xml` van a la API (http://localhost:5080); `/vehiculo/` **no** va a la API en desarrollo. Los enlaces viejos como http://localhost:5173/#/cuenta siguen funcionando: se convierten solos.

## 5. Pruebas

```powershell
# Backend (desde la raíz)
dotnet test backend/Camion.Api.Tests

# Frontend (desde frontend/)
npx vitest run
npx tsc -b
npm run build
```

Último recuento con cifras registrado en la documentación, en la rama **`feature/p10-marca-visibilidad`** después de las correcciones de la revisión de seguridad (coordinador, 2026-11-17, [`docs/criterios/P10-evidencia.md`](docs/criterios/P10-evidencia.md) §6). Las PR #19 y #20 se unieron después; esta documentación no registra sus recuentos:
- frontend **1025/1025**, sin ninguna prueba marcada como fallo esperado;
- backend, corrida completa: **1557 pasan y 5 omitidas**. Las 5 son `P10QaIndexCompiladoTests`, que necesitan el `index.html` compilado (variables `CAMION_QA_INDEX_APAGADA` y `CAMION_QA_INDEX_ENCENDIDA`). En corridas aparte con el `index.html` compilado real, con la indexación apagada y encendida, las 52 pruebas P10 pasan y 0 se omiten.

La evidencia de QA de P10 (2026-11-16, antes de las correcciones) registró backend 1554/1554 y frontend 1017 + 1 fallo esperado (QA-P10-1, hoy corregido); `tsc -b`, `tsc -p tsconfig.test.json` y `npm run build`, correctos. En la revisión de seguridad de P10, `npm audit` y `dotnet list package --vulnerable`, sin avisos; después, en la PR #18, el CI encontró GHSA-fc5q-39xc-vjpf en `picomatch` (solo desarrollo; ver el párrafo del CI). En P9 (QA, 2026-11-16) el último resultado registrado fue 1481/1481 y 875/875.

**CI (INF-1):** [`.github/workflows/ci.yml`](.github/workflows/ci.yml) ejecuta todo lo anterior en cada push y PR, más `dotnet list package --vulnerable` y `npm audit`, sin secretos. La auditoría de dependencias **bloquea solo las de producción** (`npm audit --omit=dev`). La auditoría completa es informativa por GHSA-fc5q-39xc-vjpf en `picomatch`, que llega solo por Vite y Vitest y no tiene parche publicado (anexo de [`REVISION-SEGURIDAD-P10.md`](docs/coordinacion/REVISION-SEGURIDAD-P10.md)). El backend se prueba en Windows (obligatorio) y en Linux (informativo hasta decidir el hosting, D-81). Las pruebas de la API usan SQLite en memoria y no tocan `camion_ventas`.

Si Windows (Smart App Control) bloquea la DLL de pruebas, `dotnet test` termina sin error pero con **0 pruebas**. Revisa siempre el recuento "Total" y, si sale 0, vuelve a compilar.

Las pruebas intermitentes de INF-3 quedaron corregidas en la PR #16 (detalle en [`docs/criterios/INF-3-evidencia.md`](docs/criterios/INF-3-evidencia.md)), y las de `P5Leads` en la PR #20 (INF-4). Si una corrida vuelve a fallar sin motivo claro, repítela y anota el nombre de la prueba.

## 6. Forma de trabajo

- Ramas `feature/*`, una por fase, y PR contra `main`. Nadie hace merge ni push a `main` sin aprobación humana.
- Respaldo antes de cada migración. Los cambios de esquema se hacen solo con migraciones reversibles.
- El trabajo se coordina con el comando `/coordinar`. El estado, el backlog, las decisiones y la bitácora están en `docs/coordinacion/`.
- Las reglas completas de negocio, de seguridad y de Git están en [`CLAUDE.md`](CLAUDE.md).
- Nunca guardes contraseñas, tokens ni cadenas de conexión en Git. La configuración actual (HTTP, usuario local de XAMPP) sirve solo para trabajar en tu equipo. Para publicar, sigue [`docs/GUIA-PUBLICACION.md`](docs/GUIA-PUBLICACION.md).

## 7. Mapa de la documentación

| Dónde | Qué contiene |
|---|---|
| `docs/coordinacion/ESTADO-REAL.md` | Estado verificado del código, con actualizaciones hasta P8 y la "Preparación para publicar" (con lo que resolvieron P9 y P10) |
| `docs/coordinacion/BACKLOG.md` | Orden de trabajo y estado de cada fase |
| `docs/coordinacion/DECISIONES.md` | Decisiones D-1 en adelante |
| `docs/coordinacion/BITACORA.md` | Qué se hizo en cada sesión, con migraciones, respaldos y pruebas |
| `docs/coordinacion/REVISION-SEGURIDAD-P5*.md`, `-P6.md`, `-P7.md`, `-P8.md`, `-P9.md`, `-P10.md` | Revisiones de seguridad |
| `docs/requisitos/` | Requisitos funcionales v3.0 y minuta de transferencia |
| `docs/criterios/` | Criterios, contratos de API y evidencia por fase |
| `docs/manual/` | Guías de uso y de prueba manual: P5 contactos, P5b avisos, P6 créditos, P7 administración, P8 reportes, P9 prepublicación y P10 marca y visibilidad |
| `docs/manual/PRUEBA-INTEGRAL.md` | Prueba integral manual de todo el portal antes de publicar (hecha el 2026-11-23/24; resultado en la bitácora) |
| `docs/GUIA-PUBLICACION.md` | Lista de verificación para publicar: variables, base nueva, https, proxy (con las reglas de las direcciones sin `#` de P10), claves, respaldos y cómo encender la indexación |
| `docs/marca/` | Originales del logo de VC Automotores (sin seguimiento en Git; no se publican). Los derivados web están en `frontend/public/marca/` |
| `.env.example` | Nombres de las variables de entorno del servidor (sin valores) |
| `docs/GUIA-ADMINISTRACION.md`, `GUIA-CATALOGO.md`, `GUIA-CATALOGOS.md` | Guías de administración, catálogo público y marcas y modelos |
| `docs/legal/` | Borradores de la política de datos y de los términos (D-44; falta la revisión legal). No se publican en el portal |
| `backend/GUIA-*.md` | Conexión a MariaDB y autenticación |
| `database/` | Esquema, `migraciones/`, `catalogos/` y `demo/` |

Datos de contacto públicos: los del pie del portal.
