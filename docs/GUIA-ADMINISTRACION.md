# Guía del administrador

Esta guía explica, paso a paso, cómo crear la primera cuenta de administrador, cómo usar el panel "Administrar usuarios", dónde queda el registro de lo que se hace (auditoría) y cómo aplicar o revertir la migración 003.

- Estado: todo lo que se describe está **IMPLEMENTADO y PROBADO** (P2, fusionado en `main` con el PR #4):
  - Pruebas automáticas: 223 de API y 93 de pantallas.
  - Prueba manual de la responsable contra `camion_ventas`: superada.
  - La migración 003 ya está aplicada (D-14, D-16).
- Valores marcados como **PROPUESTO** (pendientes de confirmar por la responsable): los tipos de documento CC / CE / NIT / PAS y el largo del motivo (5 a 500 caracteres).
- Trazabilidad: HU-043 Aprobar, HU-044 Rechazar, HU-045 Activar acceso, HU-047 Bloquear, HU-048 Consultar usuarios, HU-061 Auditoría (RN-SE-003), RN-SE-006 Control de accesos. Decisiones D-3, D-10, D-12, D-13, D-14 y D-15 (`docs/coordinacion/DECISIONES.md`).
- Detalle técnico de la API: `backend/GUIA-AUTENTICACION.md` y `docs/criterios/P2-contrato-api.md`.

Antes de empezar necesitas el backend y el frontend funcionando en tu equipo (ver `README.md`).

> En esta guía, `<TU_CORREO>` significa "tu correo electrónico". Escríbelo sin los signos `<` y `>`. Nunca escribas tu contraseña en la consola ni en ningún documento.

---

## 1. Crear el primer administrador (D-13)

Ninguna pantalla web permite convertirse en administrador. Se hace en dos partes: primero te registras como cualquier comprador, y después, **en el equipo donde está el servidor**, ejecutas un comando que le da a tu cuenta el rol de administrador. El comando nunca pide ni muestra contraseñas.

### Paso 1. Registrarte en la web como comprador

1. Abre http://localhost:5173.
2. Pulsa **Registrarme**.
3. En "Tipo de cuenta" deja marcado **Soy comprador**.
4. Escribe tu nombre, tu correo, tu teléfono y **tu propia contraseña** (al menos 8 caracteres, con una mayúscula, una minúscula y un número).
5. Pulsa **Crear cuenta**. Verás "Cuenta creada. Ya puedes iniciar sesión."

Usa **Soy comprador**, no "Soy asesor": una cuenta de asesor queda pendiente y el comando no promueve cuentas que no estén activas.

### Paso 2. Ejecutar el comando en el equipo del servidor

1. Si el backend está encendido, detenlo en su terminal con `Ctrl + C` (así el comando puede compilar sin chocar con el backend en marcha).
2. Comprueba que XAMPP tiene MariaDB encendido.
3. Abre PowerShell y ve a la carpeta raíz del proyecto:

```powershell
cd "C:\Users\alex0\OneDrive\Documentos\proyecto venta de camiones\CAMION"
```

4. Ejecuta (cambia `<TU_CORREO>` por el correo con el que te registraste):

```powershell
dotnet run --project backend/Camion.Api -- promover-admin <TU_CORREO>
```

El comando usa la misma conexión a la base de datos que el backend (la guardada con `dotnet user-secrets`), hace el cambio y termina. **No** enciende el servidor web.

### Paso 3. Leer el mensaje

| Mensaje que aparece | Qué significa | Qué hacer |
|---|---|---|
| `La cuenta <TU_CORREO> ahora es administrador (roles: ADMINISTRADOR). Debe iniciar sesión de nuevo.` | Listo. Tu cuenta tiene el rol ADMINISTRADOR y se le quitó el de comprador (tus datos de comprador se conservan). Quedó anotado en la auditoría. | Sigue con el paso 4. |
| `La cuenta <TU_CORREO> ya es administrador; no se cambió nada.` | Ya lo eras; no pasa nada por repetirlo. | Sigue con el paso 4. |
| `Uso: promover-admin <correo>` | Falta el correo después de `promover-admin`. | Repite el comando con tu correo. |
| `No existe un usuario con el correo <TU_CORREO>.` | No hay ninguna cuenta con ese correo. | Revisa que esté bien escrito o regístrate primero (paso 1). |
| `La cuenta <TU_CORREO> no está ACTIVA (estado ...); no se promueve.` | La cuenta está pendiente, rechazada o bloqueada (por ejemplo, porque te registraste como asesor). | Regístrate como comprador con otro correo, o pide que activen esa cuenta. |
| `No se pudo crear el rol ADMINISTRADOR.`, `No se pudo asignar el rol ADMINISTRADOR.`, `No se pudo quitar el rol COMPRADOR.`, `No se pudo guardar el cambio; no se modificó nada.` | Algo falló al guardar; el cambio se deshizo entero. | Comprueba que MariaDB está encendido y repite el comando. Si sigue fallando, avisa al equipo técnico. |
| `No se pudo actualizar la cuenta (cambio simultáneo). Intenta de nuevo.` | Alguien modificó la cuenta justo a la vez. | Repite el comando. |

Si MariaDB está apagado, el comando termina con un error técnico en inglés. Enciende MariaDB en XAMPP y repítelo. Si el mensaje incluyera datos de conexión, no lo copies en ningún documento.

### Paso 4. Volver a entrar

1. Vuelve a encender el backend (en `backend\Camion.Api`, `dotnet run`).
2. En la web, pulsa **Cerrar sesión** si estabas dentro. Por seguridad, el comando ya invalidó las sesiones que tenías abiertas; si no cierras tú, la web te pedirá entrar de nuevo en cuanto consulte al servidor.
3. Inicia sesión con tu correo y tu contraseña.
4. En tu perfil debe decir **Rol: Administrador** y aparecer el botón **Administrar usuarios**.

---

## 2. Usar el panel "Administrar usuarios"

### Cómo entrar

1. Inicia sesión con tu cuenta de administrador.
2. En tu perfil pulsa **Administrar usuarios**.
3. Para salir del panel, pulsa **Volver a mi perfil**.

Solo los administradores ven ese botón. Aunque alguien intentara entrar por otro camino, el servidor comprueba el rol en cada petición y responde "No tienes permiso para realizar esta acción."

### Buscar usuarios

Arriba hay tres filtros:

- **Estado**: Pendiente, Activa (asesor aprobado), Rechazada, Bloqueada o "Todos los estados". Por defecto muestra **Pendiente**, es decir, las solicitudes de asesor que esperan respuesta. Ojo: "Activa" incluye también a los compradores, que siempre están activos.
- **Rol**: Comprador, Asesor, Administrador o "Todos los roles".
- **Buscar por nombre o correo**: hasta 100 caracteres.

Pulsa **Aplicar filtros**. La tabla muestra nombre (y nombre comercial si es asesor), correo, teléfono, rol, estado, documento (solo asesores), fecha de registro y los botones de acción. Se ven 20 usuarios por página; usa **Anterior** y **Siguiente** para moverte.

Cada fila solo muestra los botones que tienen sentido para su estado. Tu propia cuenta y la de otros administradores dicen **Sin acciones**: nadie puede modificar a un administrador desde el panel.

### Las cinco acciones

Al pulsar un botón se abre un cuadro de confirmación con un campo **Motivo**. Debajo se ve cuántos caracteres llevas. Pulsa el botón de confirmar para hacerlo o **Cancelar** (o la tecla `Esc`) para no hacer nada. Al terminar aparece un mensaje verde y la lista se actualiza sola (la persona puede desaparecer de la lista si ya no cumple el filtro).

| Botón | Aparece cuando | Motivo | Qué pasa | Mensaje al terminar |
|---|---|---|---|---|
| **Aprobar** | asesor en Pendiente | opcional | El asesor ya puede iniciar sesión. | "Se aprobó la cuenta de asesor de …" |
| **Rechazar** | asesor en Pendiente | **obligatorio** (5 a 500 caracteres) | El asesor no puede entrar. Si tuviera alguna sesión abierta, se cierra. | "Se rechazó la solicitud de asesor de …" |
| **Reconsiderar** | asesor en Rechazada | **obligatorio** (5 a 500 caracteres) | Cambias de opinión: el asesor pasa a Aprobado y ya puede entrar (D-15). | "Se reconsideró y aprobó la cuenta de asesor de …" |
| **Bloquear** | comprador o asesor en Activa | **obligatorio** (5 a 500 caracteres) | La persona no puede entrar y sus sesiones abiertas se cierran en todos sus equipos. | "Se bloqueó la cuenta de …" |
| **Desbloquear** | comprador o asesor en Bloqueada | opcional | La persona puede volver a entrar. Un asesor vuelve a quedar Aprobado. | "Se desbloqueó la cuenta de …" |

Consejos sobre el motivo:

- Escríbelo claro, porque queda guardado para siempre en la auditoría.
- **No** escribas en él contraseñas, números de documento ni otros datos personales.

Para reconsiderar: para ver a los rechazados, elige Estado **Rechazada**, pulsa **Aplicar filtros** y luego **Reconsiderar** en la fila del asesor.

### Mensajes de error que puedes ver en el panel

| Mensaje | Qué significa |
|---|---|
| "Escribe el motivo (5 a 500 caracteres)." | El motivo es obligatorio y está vacío o es muy corto. |
| "No se puede aprobar una cuenta en estado activa." (o con otra acción y otro estado) | Otra persona cambió esa cuenta mientras tanto. La lista se actualiza sola; revisa el estado nuevo. |
| "Usuario no encontrado." | La cuenta ya no existe. |
| "No se puede modificar una cuenta de administrador." | Intentaste actuar sobre un administrador. |
| "No se pudo completar la operación. Intenta de nuevo en unos minutos." | Falló el servidor o la base de datos. El cuadro sigue abierto para reintentar. Si pasa siempre al actuar sobre un **asesor**, lo más probable es que la migración 003 no esté aplicada (sección 4). |
| "Tu sesión terminó. Inicia sesión de nuevo." | Tu sesión caducó (2 horas) o pasaste 30 minutos sin actividad. |

### Qué ve el asesor en cada estado

| Estado del asesor | Qué ve al intentar iniciar sesión |
|---|---|
| Recién registrado | Al enviar el formulario de registro ve "Solicitud enviada. Podrás iniciar sesión cuando el administrador apruebe tu cuenta." |
| **Pendiente** | Un aviso informativo: "Tu cuenta de asesor está pendiente de aprobación. Te avisaremos cuando el administrador la apruebe." No entra. |
| **Aprobado** (tras Aprobar, Reconsiderar o Desbloquear) | Entra normalmente y ve su perfil con **Rol: Asesor**. Todavía no hay pantallas propias del asesor (vehículos, publicaciones): llegarán en ítems posteriores. |
| **Rechazado** | Un aviso de advertencia: "Tu solicitud de asesor no fue aprobada. Comunícate con el administrador." No entra. |
| **Bloqueado** | Un aviso de advertencia: "Tu cuenta está bloqueada. Comunícate con el administrador." No entra. Si estaba dentro cuando lo bloqueaste, en cuanto su navegador vuelva a consultar al servidor (por ejemplo, al recargar la página) verá "Tu sesión terminó. Inicia sesión de nuevo." |

Un comprador bloqueado ve el mismo mensaje de cuenta bloqueada. El sistema todavía **no envía correos** al asesor cuando lo apruebas o lo rechazas (no hay servicio de correo aprobado, D-5): avísale tú por teléfono o WhatsApp.

Un comprador que quiera ser asesor debe registrarse como asesor con **otro correo**; "Solicitar ser asesor" desde la cuenta de comprador está PROPUESTO para más adelante (D-15).

---

## 3. Dónde queda la auditoría

Cada acción queda guardada en la base de datos `camion_ventas`, en la misma operación que el cambio: si el cambio se guarda, su registro también; si falla, no se guarda ninguno de los dos.

### Tabla `auditoria_eventos` (todas las acciones)

| Columna | Qué contiene |
|---|---|
| `usuario_id` | Quién lo hizo: el administrador; el propio asesor en su registro; vacío (`NULL`) en la promoción con el comando, porque la hace el sistema. |
| `entidad` | Siempre `Usuario`. |
| `entidad_id` | El id de la cuenta afectada. |
| `accion` | `REGISTRO_ASESOR`, `APROBAR`, `RECHAZAR`, `RECONSIDERAR`, `BLOQUEAR`, `DESBLOQUEAR` o `PROMOVER_ADMIN`. |
| `datos_anteriores` | El estado anterior, por ejemplo `{"estado":"PENDIENTE"}`. |
| `datos_nuevos` | El estado nuevo y el motivo, por ejemplo `{"estado":"RECHAZADA","motivo":"..."}`. En `PROMOVER_ADMIN`, los roles nuevos. |
| `creado_en` | Fecha y hora en **UTC** (en Colombia hay que restar 5 horas). |

Nunca se guardan contraseñas, tokens ni el número de documento.

### Tabla `historial_autorizaciones` (solo asesores)

Una fila por cada aprobación, rechazo, reconsideración, bloqueo o desbloqueo de un asesor: estado anterior y nuevo (con los nombres Pendiente / Aprobado / Rechazado / Bloqueado), el administrador que lo hizo, el motivo y la fecha (UTC). El registro inicial del asesor no crea fila aquí (solo en `auditoria_eventos`).

### Cómo consultarla (solo lectura)

En phpMyAdmin (http://localhost/phpmyadmin), elige la base `camion_ventas`, abre la pestaña **SQL** y pega una de estas consultas. Solo leen; no cambian nada.

```sql
-- Últimas 50 acciones sobre cuentas
SELECT creado_en, accion, usuario_id AS hecho_por, entidad_id AS cuenta_afectada,
       datos_anteriores, datos_nuevos
FROM auditoria_eventos
WHERE entidad = 'Usuario'
ORDER BY creado_en DESC
LIMIT 50;

-- Historial de autorizaciones de los asesores
SELECT h.creado_en, p.usuario_id AS asesor, h.estado_anterior, h.estado_nuevo,
       h.administrador_id, h.motivo
FROM historial_autorizaciones h
JOIN perfiles_asesor p ON p.id = h.asesor_id
ORDER BY h.creado_en DESC;
```

Además, el log del servidor (la ventana donde corre el backend) muestra las mismas acciones y también los inicios y cierres de sesión, que todavía no se guardan en la base.

---

## 4. La migración 003 (estados del asesor)

### Qué hace

El archivo `database/migraciones/003_p2_estados_asesor.sql` adapta la tabla `perfiles_asesor` a los estados decididos en D-10.

- Hoy la tabla solo acepta los estados antiguos del script 002: PENDIENTE, AUTORIZADO y DESAUTORIZADO.
- La migración cambia esa regla (la restricción `CK_Asesor_Estado`) para que acepte PENDIENTE, APROBADO, RECHAZADO y BLOQUEADO.
- Si ya hubiera asesores, traduce sus estados: AUTORIZADO pasa a APROBADO; DESAUTORIZADO pasa a BLOQUEADO si alguna vez estuvo autorizado, o a RECHAZADO si nunca lo estuvo.
- No borra tablas ni filas.

**Por qué hace falta:** sin ella, el registro de asesor funciona, pero aprobar, rechazar, reconsiderar, bloquear o desbloquear a un **asesor** fallan con "No se pudo completar la operación…". Las acciones sobre compradores no dependen de ella.

Esta guía **no** indica si la migración ya está aplicada en tu base. Para saberlo, ejecuta en phpMyAdmin:

```sql
SELECT CONSTRAINT_NAME, CHECK_CLAUSE
FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'perfiles_asesor';
```

Si la regla menciona `APROBADO`, ya está aplicada: **no la vuelvas a aplicar**. Si menciona `AUTORIZADO`, todavía no.

### Reglas antes de aplicarla (D-14)

- Se aplica **una sola vez**.
- Solo cuando el código de P2 compile y pase sus pruebas, y con aprobación de la responsable.
- **Siempre** con un respaldo previo en un archivo **nuevo** dentro de `C:\Respaldo_CAMION\`. Nunca sobrescribas ni borres un respaldo existente.
- Al terminar, anota en la bitácora la ruta del respaldo, los conteos y el resultado.

### Paso 1. Respaldo previo

1. Abre PowerShell. Cambia `<USUARIO_MARIADB>` por tu usuario de MariaDB y `AAAAMMDD_HHMM` por la fecha y hora de ahora (por ejemplo `20261026_1530`). La contraseña la pedirá el propio programa al pulsar Enter (por eso va `-p` sin nada detrás): **no la escribas en el comando**.

```powershell
C:\xampp\mysql\bin\mysqldump.exe -u <USUARIO_MARIADB> -p --single-transaction --default-character-set=utf8mb4 --result-file=C:\Respaldo_CAMION\camion_ventas_antes_003_AAAAMMDD_HHMM.sql camion_ventas
```

   Este comando respalda la base **completa**, como pide D-14. (El propio archivo 003 trae un ejemplo que respalda solo dos tablas; usa el completo.) Se usa `--result-file` en lugar de `>` porque en PowerShell `>` guarda el archivo en un formato que luego no se puede restaurar directamente.

2. Si ya existe un archivo con ese nombre, usa otra hora. No lo sobrescribas.
3. Comprueba que el archivo existe y no está vacío:

```powershell
Get-Item C:\Respaldo_CAMION\camion_ventas_antes_003_AAAAMMDD_HHMM.sql | Select-Object Name, Length
```

   `Length` debe ser mayor que 0. Si no, **para aquí** y pide ayuda.

### Paso 2. Contar filas antes

En phpMyAdmin, pestaña **SQL** de `camion_ventas`, ejecuta y **anota** los números:

```sql
SELECT 'AspNetUsers' AS tabla, COUNT(*) AS filas FROM AspNetUsers
UNION ALL SELECT 'perfiles_asesor', COUNT(*) FROM perfiles_asesor
UNION ALL SELECT 'perfiles_comprador', COUNT(*) FROM perfiles_comprador
UNION ALL SELECT 'AspNetUserRoles', COUNT(*) FROM AspNetUserRoles;
```

### Paso 3. Aplicar (sección SUBIR)

1. Abre el archivo `database/migraciones/003_p2_estados_asesor.sql` con el Bloc de notas y copia **todo** su contenido.
2. Pégalo en la pestaña **SQL** de phpMyAdmin y pulsa **Continuar**.

Al hacerlo se ejecutan las consultas de "VERIFICACIÓN ANTES", la sección **SUBIR** y las de "VERIFICACIÓN DESPUÉS DE SUBIR". La sección **BAJAR** está dentro de `/* … */`, así que no se ejecuta.

### Paso 4. Comprobar después

- En los resultados de "VERIFICACIÓN DESPUÉS" solo deben aparecer PENDIENTE, APROBADO, RECHAZADO o BLOQUEADO.
- `valores_fuera_de_d10` debe ser 0.
- La regla `CK_Asesor_Estado` debe mencionar `APROBADO`.
- Vuelve a ejecutar la consulta de conteo del paso 2: los cuatro números deben ser **iguales** a los de antes.

Si algo de esto no se cumple, o si phpMyAdmin mostró un error, **no sigas**: pasa a la sección "Cómo revertirla" y avisa al equipo.

Nota técnica: en MariaDB los cambios de estructura (`ALTER TABLE`) se confirman uno a uno, así que si un paso falla a mitad, los anteriores ya quedaron hechos. Por eso existen las verificaciones, la sección BAJAR y el respaldo.

### Cómo revertirla (sección BAJAR)

Úsala solo si hay que deshacer la migración.

1. **Detén el backend** (`Ctrl + C`). Mientras esté encendido podría guardar estados nuevos.
2. Haz otro respaldo **nuevo** (paso 1, con otra fecha y hora en el nombre).
3. Abre el archivo 003 y copia **solo** lo que está entre `/*` y `*/` al final (la sección BAJAR), sin incluir esos dos signos.
4. Pégalo en la pestaña **SQL** de phpMyAdmin de `camion_ventas` y pulsa **Continuar**.
5. Comprueba: solo deben quedar PENDIENTE, AUTORIZADO o DESAUTORIZADO, con el mismo total de filas; la regla debe mencionar `AUTORIZADO`.

Atención:

- Al bajar, RECHAZADO y BLOQUEADO se convierten los dos en DESAUTORIZADO y la diferencia se pierde. El respaldo previo sí la conserva.
- Con la migración revertida, el backend de P2 no puede aprobar, rechazar ni bloquear asesores.
- Restaurar un respaldo completo reemplaza la base entera: no lo hagas sin ayuda del equipo técnico y sin aprobación de la responsable.
