# INF-3 — Evidencia: pruebas intermitentes (backend D-1 y frontend O-8)

Ítem: **INF-3** "Pruebas intermitentes" (`docs/coordinacion/BACKLOG.md`). Viene de D-1 y O-8 de `docs/criterios/P7-evidencia.md`.
Rama: `feature/inf-publicacion`. Fecha: 2026-11-13. Autor: QA.

Base de datos: las pruebas del backend usan SQLite en memoria (`CamionApiFactory`); **ninguna toca `camion_ventas`**. Datos ficticios; no se imprimen tokens ni contraseñas. **No se modificó código de producción**: los cambios están en `backend/Camion.Api.Tests` y `frontend/tests/setup.ts`.

`dotnet build` y `dotnet test` se ejecutan sobre `backend/Camion.Api.Tests` con `--artifacts-path <scratchpad>/art…`, para no chocar con una API en marcha. No se detuvo ningún proceso ajeno. En cada corrida se comprobó el total de pruebas: ninguna dio 0.

## 1. Resultado

| Problema | Resultado |
|---|---|
| D-1: fallo intermitente del backend | **Identificado y corregido en la infraestructura de pruebas.** Eran dos pruebas de concurrencia que recibían un 500: `PagosTests.Anular_concurrente` (CA-P7-31) y, en otra corrida, `RegistroAsesorTests.Simultaneos_mismo_correo` y `Simultaneos_mismo_documento` (CA-043R-12/13). La causa es una carrera en el pool de conexiones de Microsoft.Data.Sqlite 8.0.13 (§3). La corrección es `Pooling=False` en la cadena SQLite de `CamionApiFactory`. **No es un defecto de producción**: producción usa MySqlConnector y MariaDB, no este pool |
| O-8: esperas de 1 s de Testing Library | **Corregido.** `configure({ asyncUtilTimeout: 5000 })` en `frontend/tests/setup.ts`, sin tocar pruebas una a una. Con carga, las 751 pasaron en todas las corridas (§2.2) |

## 2. Comandos y resultados

### 2.1 Backend

| # | Qué | Corridas | Resultado |
|---|---|---|---|
| B1 | `dotnet build` (código inicial) | 1 | **PASA**: 0 errores, 1 advertencia heredada (xUnit2031 en `InventarioCrearEditarTests.cs:547`) |
| B2 | `dotnet test` completo, `--logger trx`, código inicial; casi todas con `vitest` a la vez | 10 | **PASA 10 de 10**: 1368 / 0 / 0 en cada una (de 1 m 24 s a 3 m 7 s) |
| B3 | Subconjunto de 11 clases (las 10 de D-1 + `P8QaTests`, 451 casos), `--logger trx`, con `vitest` y otras corridas a la vez | 25 | **PASA 25 de 25**: 451 / 0 / 0 |
| B4 | Solo las 10 pruebas con peticiones simultáneas, un proceso por corrida, `--logger trx`, máquina muy cargada (`vitest` en bucle + B3 + otras corridas) | 19 + 27 | **FALLA 2 corridas de 46**: la 4.ª de la primera serie (`PagosTests.Anular_concurrente`: `[OK, InternalServerError]` en lugar de `[OK, Conflict]`) y la 14.ª de la segunda (`RegistroAsesorTests.Simultaneos_mismo_correo` y `Simultaneos_mismo_documento`: "Se esperaba 409 y llegó 500"). Las demás, 10 / 0 / 0 |
| B5 | Igual que B4 sobre una copia del proyecto de pruebas en el scratchpad, con un gancho `FirstChanceException` que guardaba cualquier `SqliteException` distinta de la 19 | 79 + 45 | **PASA 124 de 124** (con menos carga que B4); el gancho no registró nada |
| B6 | Copia del scratchpad: suite completa más una prueba temporal que repite `Anular_concurrente` 150 veces en el mismo proceso | 6 | **PASA 6 de 6**: 1369 / 0 / 0; ninguna de las 900 repeticiones dio 500 |
| B7 | Programas de consola en el scratchpad que imitan la petición (ADO.NET y EF Core, caché compartida, 2-3 peticiones simultáneas; unas 24 000 parejas) | — | Ningún error: el patrón SQL por sí solo no lo reproduce |
| B8 | Programa de consola del pool (§3.3): varios hilos por núcleo abren y cierran conexiones y comprueban si una conexión nativa está en manos de dos dueños a la vez | 5 | Sin presión de GC (2 hilos por núcleo): 0 en 37,5 millones de aperturas con pool y 0 en 82 325 sin pool. Con presión de GC (4 hilos por núcleo, reservas de memoria aleatorias): **con `Pooling=True`, 87 casos en 12,3 millones** (90 s, máquina cargada) y 0 en 22,1 millones (60 s, menos carga); **con `Pooling=False`, 0 en 257 494** |
| B9 | Con el diagnóstico de §4.2 (aún con pool): `Inf3DiagnosticoTests` y suite completa | 1 + 3 | **PASA**: 2 / 0 / 0 y 1370 / 0 / 0 tres veces |
| B10 | `dotnet build` con la corrección (`Pooling=False`) | 1 | **PASA**: 0 errores, la misma advertencia heredada |
| B11 | `dotnet test` completo con la corrección, `--logger trx`, con `vitest` y B12 a la vez | 10 | **PASA 10 de 10**: 1370 / 0 / 0 en cada una (de 1 m 58 s a 3 m 34 s, con la máquina cargada) |
| B12 | Las 10 pruebas con peticiones simultáneas con la corrección, un proceso por corrida, con carga | 57 | **PASA 57 de 57**: 10 / 0 / 0 (unas 46 con B11 y `vitest` a la vez; las últimas, cuando estos ya habían terminado) |
| B13 | `dotnet test` completo con la corrección, sin otras corridas a la vez | 2 | **PASA 2 de 2**: 1370 / 0 / 0 (1 m 21 s y 1 m 26 s; antes, sin carga, de 1 m 24 s a 1 m 36 s) |

Antes de la corrección, B4 falló en 2 de 46 corridas con la máquina muy cargada. Es la misma frecuencia que vio P7: 1 de unas 20.

### 2.2 Frontend (O-8)

| # | Qué | Corridas | Resultado |
|---|---|---|---|
| F1 | `npx vitest run` completo, con `dotnet test` (B2 a B12) corriendo a la vez | 53 | **PASA 53 de 53**: 37 archivos, 751 / 751 en cada una (de 67 s a 154 s) |
| F2 | Carga de P7 (dos `vitest` a la vez más `dotnet test`) | 1 pareja | **PASA**: 751 / 751 en los dos (137 s cada uno). En P7 esta carga hacía fallar de 1 a 4 pruebas |
| F3 | `npx vitest run` sin otras corridas a la vez | 1 | **PASA**: 751 / 751 (50 s) |
| F4 | `npm run build` | 1 | **PASA** (`tsc -b && vite build`, 1,85 s); solo el aviso conocido de un bloque de más de 500 kB (O-5 de P6) |

Las pruebas que fallaban en P7 por la espera de 1 s (`InventarioLista`, `InventarioFormulario`, `P4bInventario`, `P5Leads`, `P7Admin`) pasaron en todas estas corridas.

## 3. Causa del fallo del backend

### 3.1 Lo que se vio

El `.trx` de B4 guarda la salida del API. En los dos casos, el 500 lo da el manejador global por una `SqliteException`:

- `Anular_concurrente`: en `PagosEndpoints.cs:82` (`BeginTransactionAsync`), dentro de `SqliteTransaction..ctor` → `BEGIN IMMEDIATE` → `SqliteDataReader.NextResult`, en menos de 1 s.
- `RegistroAsesorTests`: en `AuthEndpoints.cs:126` (el mismo `BEGIN`) y en `AuthEndpoints.cs:120` dentro de `SqliteConnection.Open()`, sin pasar por ningún comando.

El manejador global no registra el mensaje (a propósito), así que el código de SQLite no quedó guardado.

### 3.2 Por qué no es un bloqueo normal

Se leyó el IL de Microsoft.Data.Sqlite 8.0.13. `SqliteDataReader.NextResult` reintenta cada 150 ms, hasta el `Default Timeout` (30 s), los códigos `SQLITE_BUSY` (5), `SQLITE_LOCKED` (6) y `SQLITE_LOCKED_SHAREDCACHE` (262). Un bloqueo entre las dos peticiones habría esperado, no fallado en menos de 1 s. En B7, un `BEGIN IMMEDIATE` con otra transacción abierta esperó y continuó.

Que un `BEGIN` falle sin esperar indica que esa conexión ya estaba dentro de una transacción (`SQLITE_ERROR`, "cannot start a transaction within a transaction"). Que `Open()` falle al registrar funciones (`sqlite3_create_function`) indica que había sentencias activas en esa misma conexión (`SQLITE_BUSY`, "unable to delete/modify user-function due to active statements"). Ninguna de las dos cosas puede pasar si la conexión es solo de esa petición.

### 3.3 La carrera del pool

En `SqliteConnectionPool` y `SqliteConnectionInternal` (IL de la versión 8.0.13):

- `Activate` pone `_active = true` **antes** de enlazar la conexión a su `SqliteConnection` (`_outerConnection.SetTarget`).
- `Deactivate` desenlaza (`SetTarget(null)`) **antes** de poner `_active = false`.
- Si una apertura encuentra el pool vacío, `GetConnection` llama a `ReclaimLeakedConnections`. Ese método toma como "perdida" toda conexión con `_active` y sin `SqliteConnection` enlazada, y la devuelve al pool.

Si la recuperación cae entre esas dos instrucciones de otro hilo, la conexión vuelve al pool mientras alguien la usa, o se devuelve dos veces. La misma conexión nativa acaba entonces en dos peticiones a la vez: la de `Anular_concurrente` ve la transacción de la otra, y la del registro ve sus sentencias activas. Solo pasa con muchas aperturas simultáneas (pool vacío) y con hilos interrumpidos justo ahí (carga, GC), por eso solo fallaban las pruebas de concurrencia y solo con la máquina cargada. B8 lo demuestra fuera de la API: con pool, 87 entregas dobles; sin pool, 0.

Núcleo del programa de B8 (Microsoft.Data.Sqlite 8.0.13 y `SQLitePCLRaw.lib.e_sqlite3` 3.53.3, como el proyecto de pruebas), en cada hilo:

```csharp
using var c = new SqliteConnection(cs);   // cs: ...mode=memory&cache=shared;Pooling=True|False
c.Open();
var h = c.Handle!.DangerousGetHandle();
if (!enUso.TryAdd(h, i)) Interlocked.Increment(ref duplicados);   // la misma conexión nativa ya la tiene otro hilo
Thread.SpinWait(Random.Shared.Next(1, 400));
GC.KeepAlive(new byte[Random.Shared.Next(100, 20000)]);
enUso.TryRemove(h, out _);
c.Close();
```

### 3.4 Corrección

En `backend/Camion.Api.Tests/Infraestructura/CamionApiFactory.cs`, la cadena SQLite de las pruebas añade `Pooling=False`:

```
Data Source=file:camion_pruebas_<guid>?mode=memory&cache=shared;Default Timeout=30;Pooling=False
```

Sin pool, cada `DbContext` abre su propia conexión nativa contra la misma base en memoria, que sigue viva gracias a `_conexionViva`. No cambia ninguna prueba ni ninguna aserción. El tiempo de la suite no cambió de forma apreciable (B11 y B13 frente a B2).

Producción usa `MySqlConnector` con MariaDB, que tiene su propio pool, así que este fallo no la afecta. No hay nada que corregir en el código de producción.

## 4. Cambios

### 4.1 Frontend

- `frontend/tests/setup.ts`: `configure({ asyncUtilTimeout: 5000 })` de `@testing-library/react`. Solo da más margen a `findBy*` y `waitFor`: una prueba que deba encontrar el elemento lo encuentra igual, y una que no lo encuentre falla igual, solo que más tarde. El `testTimeout` de 15 s de `vitest.config.ts` sigue cubriéndolo.

### 4.2 Backend (solo infraestructura de pruebas)

- `CamionApiFactory.cs`: `Pooling=False` (§3.4).
- `Infraestructura/DiagnosticoTransaccionesSqlite.cs` (nuevo): un interceptor de EF Core abre la transacción igual que EF (`DbConnection.BeginTransaction(nivel)`). Si falla, escribe en el log de las pruebas el código, el código extendido, `sqlite3_get_autocommit` y el mensaje, y vuelve a lanzar la excepción. Ese log va a la consola, al `.trx` (también en el CI) y a `CamionApiFactory.Logs`. No reintenta ni oculta nada. Queda como guarda: si algún día vuelve a fallar un `BEGIN`, el `.trx` dirá por qué.
- `Inf3DiagnosticoTests.cs` (nuevo, 2 pruebas): un `BEGIN` fallido (transacción abierta a mano) sigue lanzando la excepción y deja en el log `código=1` y `autocommit=0`; una transacción normal no deja nada.

## 5. Trazabilidad criterio → prueba

| Criterio de salida de INF-3 | Prueba o corrida |
|---|---|
| Fallo identificado con `--logger trx` | B4: `.trx` con `PagosTests.Anular_concurrente` y `RegistroAsesorTests.Simultaneos_*`; §3 |
| … y corregido | §3.4; B11 a B13 |
| Pruebas del frontend estables con carga | F1, F2 |
| `dotnet test` completo | B2, B11, B13 |
| `npx vitest run` completo | F1, F3 |
| Diagnóstico para el CI | `Inf3DiagnosticoTests` (2 pruebas) |

## 6. Defectos y observaciones

- **Sin defectos del código de producción.**
- **INF3-1 (resuelto, infraestructura de pruebas):** la carrera del pool de Microsoft.Data.Sqlite 8.0.13 (§3.3) daba un 500 intermitente en las pruebas con peticiones simultáneas. Resuelto con `Pooling=False`.
- **O-INF3-1 (observación):** el manejador global de errores (`Program.cs`) registra el tipo de la excepción pero no su mensaje. Es una decisión de seguridad, no un defecto. Sin embargo, para diagnosticar errores de base de datos en producción ayudaría registrar al menos el código (`SqliteErrorCode` o `MySqlErrorCode`). Se deja al coordinador; no se tocó.
- **O-INF3-2 (observación):** la frecuencia antes de la corrección era baja (2 de 46 corridas con mucha carga, 0 en 124 con menos), así que "0 fallos" tras la corrección (B11 a B13) es coherente, pero por sí solo no lo prueba. La demostración de la causa es B8 junto con las pilas de §3.1.
