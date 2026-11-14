# Guía de publicación del portal CAMION

Esta guía explica, paso a paso, qué hace falta para poner el portal en internet. Sirve para cualquier proveedor: el hosting todavía no está elegido (D-81). Cuando se elija, solo cambia **dónde** se hace cada paso, no **qué** hay que hacer.

- Producción empieza con una **base de datos nueva y limpia** (D-80 A). La base local `camion_ventas` de XAMPP no se toca ni se copia: se queda para pruebas.
- Ningún valor secreto aparece en esta guía, solo los **nombres** de las claves. Los valores se escriben directamente en el servidor, nunca en Git, en chats ni en documentos.
- Estados: lo marcado como **PROPUESTO** necesita aprobación antes de usarse.

---

## 1. Cómo queda el portal publicado

```
Navegador ──https──> Proxy inverso (nginx, IIS, Caddy o el del proveedor)
                       ├── /            -> archivos del frontend (frontend/dist)
                       ├── /api, /media, /health -> API (Kestrel, http://127.0.0.1:5080)
                       │                              └──> MariaDB (base del portal)
```

- El proxy recibe todo por **https** y lo pasa a la API por http **dentro del mismo servidor** (o de la red privada).
- El frontend usa rutas relativas (`/api/...`), así que lo más sencillo es servir frontend y API desde **el mismo dominio**. Así no hace falta CORS.
- La API no debe quedar expuesta directamente a internet. Debe escuchar solo en `127.0.0.1` (o en la red privada del proveedor).

---

## 2. Lista de verificación

Marca cada punto antes de abrir el portal al público.

**Decisiones previas**
- [ ] Hosting elegido (D-81) y dominio propio comprado.
- [ ] Revisión legal de la política de datos y de los términos (`docs/legal/`, D-44). Cuando estén aprobados, se actualiza `PoliticaDatos:Version` (hoy `BORRADOR-2026-11-04`).

**Servidor**
- [ ] SDK o runtime de ASP.NET Core 8 instalado (la API se publica con `dotnet publish`, sección 9).
- [ ] MariaDB instalado (10.4 o posterior), **no** expuesto a internet (puerto 3306 cerrado al exterior).
- [ ] Carpetas creadas **fuera** de la carpeta de la aplicación: fotos (`Media:Ruta`), claves de DataProtection (`DataProtection:RutaClaves`) y respaldos.
- [ ] Cuenta de sistema propia para la API, sin permisos de administrador, con acceso solo a esas carpetas.

**Base de datos** (sección 4)
- [ ] Base creada con los scripts del repositorio, en el orden exacto.
- [ ] Usuario de MariaDB propio para la API, con permisos mínimos (nunca `root`).
- [ ] Primer respaldo hecho y restaurado de prueba.

**Configuración** (sección 3)
- [ ] Todas las claves obligatorias puestas como variables de entorno o secretos del proveedor.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] `Recuperacion:UrlPortal` con `https://` y el dominio real.
- [ ] `Proxy:KnownProxies` o `Proxy:KnownNetworks` con la IP o la red del proxy.

**Seguridad** (secciones 5 a 7)
- [ ] Certificado https vigente y renovación automática.
- [ ] Claves de DataProtection protegidas (DPAPI en Windows o certificado en Linux).
- [ ] Encabezados de seguridad también en el frontend (sección 6).

**Puesta en marcha** (secciones 8 a 10)
- [ ] Primer administrador creado con `promover-admin`.
- [ ] Planes y datos de contacto configurados desde el panel.
- [ ] `recuperar-admin` probado una vez.
- [ ] Respaldos periódicos programados y una restauración probada.
- [ ] Comprobación final (sección 11).

---

## 3. Variables y secretos (solo nombres)

En ASP.NET Core, cada clave con `:` se escribe como variable de entorno con `__` (dos guiones bajos). Por ejemplo, `Jwt:Key` se escribe `Jwt__Key`. La lista completa, sin valores, está en [`.env.example`](../.env.example).

| Variable de entorno | ¿Secreta? | ¿Obligatoria? | Para qué sirve |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | No | Sí | `Production`. Activa https, HSTS y las comprobaciones de arranque. |
| `ASPNETCORE_URLS` | No | Sí | Dónde escucha la API. Recomendado: `http://127.0.0.1:5080` (solo el proxy llega a ella). |
| `ConnectionStrings__CamionDatabase` | **Sí** | Sí | Conexión a MariaDB con el usuario de la API (sección 4.2). |
| `Jwt__Key` | **Sí** | Sí | Firma de las sesiones. Mínimo 32 bytes, generada al azar y distinta de la local. |
| `Media__Ruta` | No | Sí, fuera de Development | Carpeta de las fotos. |
| `DataProtection__RutaClaves` | No | Sí, fuera de Development | Carpeta de las claves de los enlaces de recuperación (sección 5). |
| `Recuperacion__UrlPortal` | No | Sí, fuera de Development | Dirección pública del portal, con `https://`. |
| `DataProtection__Certificado__Ruta` | No | En Linux o contenedor, recomendada | Ruta del certificado `.pfx` que cifra las claves (sección 5). |
| `DataProtection__Certificado__Clave` | **Sí** | Si el `.pfx` tiene contraseña | Contraseña del `.pfx`. |
| `Proxy__KnownProxies` | No | Sí, detrás de un proxy | IP del proxy, separadas por comas. |
| `Proxy__KnownNetworks` | No | Alternativa a la anterior | Redes del proxy en formato CIDR (`10.0.0.0/8`). |
| `Proxy__ForwardLimit` | No | No (por defecto 1) | Número de proxies encadenados (por ejemplo, 2 con Cloudflare + nginx). |
| `Https__RedirigirHttp` | No | No (por defecto `true`) | `false` solo si el proxy ya redirige http a https. |
| `Portal__Origen` | No | No | Solo si el frontend se publica en **otro** dominio (activa CORS para ese origen). |
| `AllowedHosts` | No | Recomendada | Dominio(s) del portal, separados por `;`. Hoy es `*`. |
| `Recuperacion__LimiteVentana`, `Recuperacion__VentanaMinutos` | No | No | Límite por IP para restablecer la contraseña (5 cada 15 min). |
| `Contactos__LimiteVentanaCorta`, `Contactos__VentanaCortaMinutos`, `Contactos__LimiteDiario`, `Contactos__LimiteTelefonoDia` | No | No | Límites de los formularios públicos. |
| `PoliticaDatos__Version` | No | No | Versión vigente de la política de datos (tras la revisión legal). |

Comportamiento al arrancar:
- **Sin** `ConnectionStrings:CamionDatabase` o `Jwt:Key`, la API no arranca en ningún entorno.
- Fuera de Development, **sin** `Media:Ruta`, `DataProtection:RutaClaves` o `Recuperacion:UrlPortal`, la API no arranca. El mensaje dice qué clave falta, nunca su valor.
- Si `Recuperacion:UrlPortal` o `Portal:Origen` no empiezan por `https://`, o si una IP del proxy está mal escrita, la API tampoco arranca.

Dónde se guardan:
- **En el equipo local:** con `dotnet user-secrets` (README §4).
- **En el servidor:** como variables de entorno del servicio, o en el gestor de secretos del proveedor. Nunca en `appsettings.json` ni en un archivo dentro de la carpeta publicada.
- **En GitHub Actions:** no hace falta ningún secreto. Las pruebas generan claves ficticias y usan SQLite.

Cadena de conexión (aviso de MySqlConnector, B-4):
- Si MariaDB está **en el mismo servidor**, conecta por `127.0.0.1`.
- Si está **en otro equipo**, exige TLS verificado (`SslMode=VerifyFull` con el certificado de la autoridad del servidor). Así la contraseña nunca viaja sin cifrar.

---

## 4. Crear la base de producción desde cero (D-80 A)

La base de producción se crea **vacía**, solo con la estructura y los catálogos. No se copia nada de la base local: ni usuarios, ni vehículos, ni leads, ni auditoría.

### 4.1 Antes de empezar
1. Si el servidor de MariaDB ya tiene otras bases, haz un **respaldo completo** del servidor antes de tocar nada.
2. Usa el cliente `mysql` de la consola, no phpMyAdmin. Ejecuta cada archivo **sin `--force`**, para que se detenga en el primer error.
3. El nombre de la base debe ser **`camion_ventas`**: el script 002 la crea con ese nombre, y los archivos completos de las migraciones usan `USE camion_ventas`.
4. Ejecuta cada archivo con la base ya seleccionada, por ejemplo:

   ```
   mysql -u <usuario_migraciones> -p --default-character-set=utf8mb4 camion_ventas
   mysql> source <ruta>/database/migraciones/006_p4a_nota_reserva_SUBIR.sql
   ```

   La contraseña la pide `-p`; nunca la escribas en el comando. En PowerShell no uses `<` para redirigir: usa `source` dentro del cliente.

### 4.2 Usuarios de MariaDB (nunca `root` para la API)
Créalos con la cuenta de administración de MariaDB. Pon contraseñas largas y únicas, escritas solo en la consola. Este archivo no las guarda.

```sql
-- 1) Usuario para instalar y migrar (solo se usa al crear la base y en futuras migraciones).
CREATE USER 'camion_migraciones'@'localhost' IDENTIFIED BY '<CONTRASEÑA_LARGA_1>';
GRANT ALL PRIVILEGES ON camion_ventas.* TO 'camion_migraciones'@'localhost';

-- 2) Usuario de la API: solo leer y escribir filas. Sin CREATE, ALTER, DROP, GRANT ni FILE.
CREATE USER 'camion_app'@'localhost' IDENTIFIED BY '<CONTRASEÑA_LARGA_2>';
GRANT SELECT, INSERT, UPDATE, DELETE ON camion_ventas.* TO 'camion_app'@'localhost';
```

- La API solo necesita `SELECT, INSERT, UPDATE, DELETE`. No ejecuta SQL de esquema (se comprobó en el código: no hay `CREATE`, `ALTER`, `LOCK TABLES` ni SQL crudo). Los roles los crea al arrancar con `INSERT`.
- Si la API corre en otro equipo, cambia `'localhost'` por la IP privada de ese equipo, nunca por `'%'`.
- La cadena de `ConnectionStrings__CamionDatabase` usa **`camion_app`**.
- Al terminar la instalación, puedes bloquear `camion_migraciones` (`ALTER USER ... ACCOUNT LOCK;`) hasta la próxima migración.
- El script 002 incluye `CREATE DATABASE IF NOT EXISTS camion_ventas`. Si `camion_migraciones` todavía no tiene permiso para crearla, créala antes con la cuenta de administración: `CREATE DATABASE camion_ventas CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;`.

### 4.3 Orden exacto de los scripts
Con la base `camion_ventas` seleccionada y el usuario `camion_migraciones`:

| Paso | Archivo | Cuál usar | Qué hace |
|---|---|---|---|
| 1 | `database/002_esquema_inicial_mariadb_10_4.sql` | Completo | Crea la base y todas las tablas iniciales (Identity incluida). |
| 2 | `database/migraciones/003_p2_estados_asesor.sql` | Completo (no tiene `_SUBIR`; su BAJAR está comentado) | Estados del asesor (D-10). |
| 3 | `database/catalogos/017_categorias_base_SUBIR.sql` | `_SUBIR` | **Nuevo, PROPUESTO** (ver 4.6): Carro, Camión, Carrocería y Maquinaria. |
| 4 | `database/migraciones/004_p4a_inventario.sql` | Completo con el cliente `mysql` (no tiene `_SUBIR`; su BAJAR está comentado) | Inventario, carrocerías, materiales y tipos de máquina. |
| 5 | `database/catalogos/005_marcas_lineas_iniciales.sql` | Completo (su BAJAR está comentado) | 33 marcas y 137 modelos. |
| 6 | `database/migraciones/006_p4a_nota_reserva_SUBIR.sql` | `_SUBIR` | Nota de reserva (D-28). |
| 7 | `database/catalogos/007_catalogos_reales_SUBIR.sql` | `_SUBIR` | Combustibles y **DIVIPOLA** del DANE (33 departamentos y 1122 municipios). |
| 8 | `database/catalogos/008_modelos_recientes_SUBIR.sql` | `_SUBIR` | Modelos recientes por marca. |
| 9 | `database/migraciones/009_p4a_lineas_tipos_SUBIR.sql` | `_SUBIR` | Tabla de modelos por categoría (D-32). |
| 10 | `database/catalogos/010_clasificacion_lineas_SUBIR.sql` | `_SUBIR` | Clasifica los modelos por categoría (necesita los pasos 3, 5, 8 y 9). |
| 11 | `database/migraciones/011_p4p_estados_suscripcion_SUBIR.sql` | `_SUBIR` | Estados de la suscripción. |
| 12 | `database/migraciones/012_p4b_publicaciones_SUBIR.sql` | `_SUBIR` | Destacados, motivo de inactivar y ventas. |
| 13 | `database/migraciones/013_p5_leads_SUBIR.sql` | `_SUBIR` | Leads, historial y consentimientos. |
| 14 | `database/migraciones/014_p5b_avisos_SUBIR.sql` | `_SUBIR` | Avisos "Vende tu camión" y "Seguro todo riesgo". |
| 15 | `database/migraciones/015_p6_creditos_busquedas_SUBIR.sql` | `_SUBIR` | Créditos y búsquedas. |
| 16 | `database/migraciones/016_p7_pagos_SUBIR.sql` | `_SUBIR` | Registro de pagos. |

P8 no tiene migración.

**No se importan:**
- `database/demo/` (**nunca en producción**: crea un asesor, un plan y 12 publicaciones ficticias).
- `database/001_esquema_inicial_mysql.sql` (versión para MySQL; la de MariaDB es la 002).
- Los archivos `_VERIFICAR_Y_BAJAR`, `017_categorias_base_VERIFICAR.sql` y las secciones BAJAR (solo sirven para comprobar o revertir).
- Las versiones completas de las migraciones que tienen archivo `_SUBIR`, porque el `_SUBIR` es la parte que se aplica.

### 4.4 Comprobar la base
- Ejecuta las consultas de verificación (solo `SELECT`) de cada archivo `_VERIFICAR_Y_BAJAR` y de `017_categorias_base_VERIFICAR.sql`, **sin** la parte BAJAR. Por ejemplo, `tipos_vehiculo` debe tener 4 filas y `municipios` 1122.
- Con la API arrancada, `https://<dominio>/health/database` debe responder `status: ok`.

### 4.5 Primer administrador (D-13)
1. Desde el portal ya publicado, la responsable se registra como **comprador** con su correo (registro normal).
2. En el servidor, con las mismas variables de entorno de la API, ejecuta:

   ```
   dotnet Camion.Api.dll promover-admin <correo>
   ```

   El comando no arranca el servidor web, no pide ni muestra contraseñas y queda en la auditoría.
3. Cierra la sesión y vuelve a entrar: ya aparece el panel de administración.

### 4.6 Lo que se configura desde el panel, después
- **Planes** (nombre, precio, vigencia, cupos y destacados): Administración → Planes. La base nueva no trae ninguno.
- **Datos de contacto del portal** (WhatsApp, correo, horario y redes): Administración → Parámetros. Mientras no se guarden, se muestran los valores por defecto del código.
- Imagen de portada, marcas y modelos adicionales: desde sus pantallas del panel.

### 4.7 Script que faltaba: `017_categorias_base_SUBIR.sql` (PROPUESTO)
Se compararon los scripts del repositorio con el modelo de la API (24 tablas propias y todas las columnas con nombre explícito): **la estructura está completa**. Falta un dato: las 4 categorías de vehículo (D-17) solo las creaba `database/demo/demo_p3_catalogo.sql`. Sin ellas, la base nueva no tendría categorías, el inventario no podría crear vehículos y el script 010 no clasificaría nada. Por eso se añadió `database/catalogos/017_categorias_base_SUBIR.sql`:
- solo `INSERT ... WHERE NOT EXISTS`, idempotente;
- en la base local no cambia nada, porque las categorías ya existen;
- no se ha aplicado en ninguna base.

La comparación con la base local `camion_ventas` (solo lectura) **no se pudo ejecutar** desde el agente. Para confirmarla, el coordinador puede ejecutar en local `SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='camion_ventas' ORDER BY 1;` y compararlo con las tablas que crean los scripts.

---

## 5. Claves de DataProtection (enlaces de recuperación, M-1 de P8)

Estas claves firman y cifran los enlaces de recuperación de contraseña. **Quien las lea puede fabricar enlaces válidos para cualquier cuenta.** Por eso:

1. **Carpeta propia** (`DataProtection__RutaClaves`), fuera de la carpeta de la aplicación y del repositorio, por ejemplo `/var/lib/camion/claves` o `D:\CAMION_claves`. Permisos **solo** para la cuenta del servicio. En Linux: `chown camion:camion` y `chmod 700`.
2. **Cifrado en reposo:**
   - **Windows:** automático con DPAPI para la cuenta que ejecuta la API (D-78). Las claves solo las lee ese usuario en ese equipo.
   - **Linux o contenedor:** configura `DataProtection__Certificado__Ruta` (un `.pfx` con clave privada, guardado fuera de la carpeta de las claves y fuera de Git) y `DataProtection__Certificado__Clave` como secreto. Sin certificado, la API arranca, pero escribe un aviso en el log: las claves quedan sin cifrar y solo las protegen los permisos de la carpeta.
3. **Contenedor:** la carpeta de claves debe ser un **volumen persistente**. Si no, cada reinicio crea claves nuevas, y los enlaces pendientes dejan de valer.
4. **Varias instancias:** todas deben compartir la misma carpeta y el mismo certificado. El nombre de aplicación ya es fijo (`Camion.Api`).
5. **Respaldo:** las claves y el `.pfx` se respaldan aparte, cifrados, y nunca junto a los respaldos públicos ni en Git.
6. **Si se pierden:** las sesiones no se ven afectadas. Solo dejan de valer los enlaces de recuperación pendientes, y basta con generar uno nuevo.

Sobre `C:\CAMION_claves` del equipo local: **no hace falta copiarla** a producción. Al ser una base nueva (D-80 A), no hay enlaces pendientes que conservar. Además, con DPAPI esas claves no se pueden leer en otro equipo.

Para crear un certificado propio para esto (no tiene que ser de una autoridad), en Linux:
`openssl req -x509 -newkey rsa:3072 -nodes -days 1825 -subj "/CN=Camion DataProtection" -keyout dp.key -out dp.crt`
y luego `openssl pkcs12 -export -inkey dp.key -in dp.crt -out dp.pfx`. Ese comando pide la contraseña del `.pfx`. Después, borra `dp.key`.

---

## 6. HTTPS, proxy inverso y encabezados

**HTTPS**
- Certificado del dominio, de Let's Encrypt o del proveedor, con renovación automática.
- Fuera de Development la API redirige http a https y envía HSTS (30 días al principio; cuando todo esté estable, se puede subir a 1 año en `Program.cs`).
- Si el proxy ya redirige, pon `Https__RedirigirHttp=false`.

**Proxy (M-1 de P5)**
- El proxy debe **sobrescribir**, no añadir, `X-Forwarded-For` con la IP real del visitante, y enviar `X-Forwarded-Proto: https`.
- En la API, configura `Proxy__KnownProxies` con la IP desde la que el proxy se conecta a la API. Si el proxy está en el mismo equipo, es `127.0.0.1`.
- Sin esto, todos los visitantes comparten la misma IP: un solo visitante podría agotar el límite de los formularios para todos.
- La API solo cree en esos encabezados cuando la petición llega desde un proxy configurado. El límite por IP agrupa las direcciones IPv6 por su prefijo /64.
- Con Cloudflare (u otra CDN) delante del proxy, la cadena tiene dos saltos: `Proxy__ForwardLimit=2`, y las redes de la CDN van en `Proxy__KnownNetworks`.

**Encabezados**
- La API ya envía estos encabezados en todas sus respuestas:
  - `Referrer-Policy: no-referrer`;
  - `X-Content-Type-Options: nosniff`;
  - `X-Frame-Options: DENY`;
  - `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`.
- Además, no envía el encabezado `Server`.
- El proxy debe añadir encabezados equivalentes a los **archivos del frontend** (la página `index.html` y sus recursos). Propuesta, que hay que probar con el portal antes de fijarla:

  ```
  Referrer-Policy: no-referrer
  X-Content-Type-Options: nosniff
  X-Frame-Options: DENY
  Content-Security-Policy: default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'
  ```

- `/health/database` responde sin sesión con el nombre de la base y el número de tablas. Conviene que el proxy solo lo permita desde la red interna o desde el monitor del proveedor.

---

## 7. CORS
- **Recomendado:** frontend y API en el mismo dominio. No se configura nada, y la API no envía encabezados CORS, así que ningún otro sitio puede llamarla desde el navegador.
- Solo si el frontend va en otro dominio, configura `Portal__Origen=https://<dominio-del-frontend>`. Únicamente ese origen podrá llamar a la API (sin cookies; la sesión va en el encabezado `Authorization`).
- **Ojo:** hoy el frontend usa rutas relativas. Para separarlo de verdad haría falta un cambio en el frontend.

---

## 8. Recuperar la contraseña de un administrador (`recuperar-admin`, D-79)
En el servidor, con las mismas variables de entorno de la API:

```
dotnet Camion.Api.dll recuperar-admin <correo>
```

- Imprime en la consola un enlace de un solo uso que vence en 60 minutos y empieza por `Recuperacion:UrlPortal`. No lo copies en chats compartidos ni en documentos.
- Necesita las mismas claves de DataProtection que la API en ejecución: la misma carpeta, y en Windows, el mismo usuario. Por eso se ejecuta **con la cuenta del servicio**.
- Detalle en `backend/GUIA-AUTENTICACION.md` §12.

---

## 9. Publicar el backend y el frontend

**Backend**
1. `dotnet publish backend/Camion.Api -c Release -o <carpeta_publicada>`
2. Arráncalo como servicio (systemd en Linux; servicio de Windows o IIS en Windows) con la cuenta propia y las variables de la sección 3.
3. La carpeta publicada **no** debe contener `appsettings.Development.json` ni ningún secreto.

**Frontend**
1. En `frontend/`: `npm ci` y `npm run build`.
2. Copia el contenido de `frontend/dist` a la carpeta que sirve el proxy.
3. El portal usa rutas con `#` (por ejemplo `#/cuenta`), así que no hace falta configurar reescrituras.

**CI (INF-1)**
- `.github/workflows/ci.yml` compila y prueba todo en cada push y PR, sin secretos.
- Que GitHub **exija** el flujo verde antes de unir un PR se configura en la protección de la rama `main`. Ese cambio necesita la aprobación de la responsable.

---

## 10. Respaldos periódicos
- **Base de datos:** a diario, con `mysqldump --single-transaction --default-character-set=utf8mb4 --result-file=<archivo_con_fecha> camion_ventas`, con un usuario de solo lectura para respaldos (`SELECT, SHOW VIEW, TRIGGER, LOCK TABLES`). Nunca sobrescribas un respaldo: cada archivo lleva su fecha y hora.
- **Fotos:** la carpeta `Media:Ruta`, al menos a diario.
- **Claves de DataProtection y certificado:** aparte y cifrados (sección 5).
- **Fuera del servidor:** guarda una copia en otro lugar (otro proveedor o un disco externo), cifrada, con al menos 30 días de historia.
- **Prueba de restauración:** una vez al mes, restaura el último respaldo en una base aparte y comprueba que `/health/database` responde con esa base. Un respaldo que nunca se ha restaurado no está comprobado.
- Los respaldos locales de desarrollo (`C:\Respaldo_CAMION\`, `frontend/respaldos/`) no se tocan ni se suben al servidor.

---

## 11. Comprobación final, ya publicado
- [ ] `http://<dominio>` redirige a `https://<dominio>`, y la respuesta lleva `Strict-Transport-Security`.
- [ ] `https://<dominio>/health` responde `ok`, y `/health/database` responde `ok` (desde la red permitida).
- [ ] Desde dos redes distintas, el formulario "Contactar" no comparte el límite (el proxy está bien configurado).
- [ ] Registro de comprador, inicio de sesión del administrador y panel funcionan.
- [ ] Un enlace de recuperación generado desde el panel empieza por `https://<dominio>`.
- [ ] Tras reiniciar la API, un enlace generado antes del reinicio sigue valiendo (las claves persisten).
- [ ] El log de arranque no muestra el aviso de "claves sin cifrar" (o se aceptó por escrito).
- [ ] Hay un respaldo de la base nueva y se restauró una vez.

---

## 12. Qué falta decidir
| Tema | Estado |
|---|---|
| Hosting y dominio (D-81) | PENDIENTE de la responsable. Bloquea la publicación. |
| Windows o Linux en el servidor | Depende de D-81. El CI ya prueba en los dos (Linux, informativo). |
| Revisión legal de la política de datos y los términos (D-44) | PENDIENTE. Antes de publicar. |
| Correo transaccional (D-73 C) | PENDIENTE, después de D-81. Mientras tanto, la recuperación es asistida. |
| Script `017_categorias_base_SUBIR.sql` | PROPUESTO. Necesario para la base nueva. |
| Exigir el CI verde en `main` (protección de rama) | Necesita aprobación de la responsable. |
