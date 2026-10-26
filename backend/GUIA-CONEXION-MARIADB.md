# Guía de conexión a MariaDB (XAMPP)

Explica cómo conectar el backend con la base de datos local `camion_ventas`.

Requisitos: SDK de .NET 8, XAMPP con MariaDB encendido (puerto 3306) y el script `database/002_esquema_inicial_mariadb_10_4.sql` importado en la base `camion_ventas` (por ejemplo desde phpMyAdmin).

## 1. Ir a la carpeta del backend

```powershell
cd "C:\Users\alex0\OneDrive\Documentos\proyecto venta de camiones\CAMION\backend\Camion.Api"
```

## 2. Configurar la conexión (secreto local)

La cadena de conexión se guarda con `dotnet user-secrets` en la clave `ConnectionStrings:CamionDatabase`. Así queda fuera del repositorio.

Forma de la cadena (cambia los marcadores por tus datos):

```powershell
dotnet user-secrets set "ConnectionStrings:CamionDatabase" "Server=127.0.0.1;Port=3306;Database=camion_ventas;User ID=<TU_USUARIO>;Password=<TU_CLAVE>;SslMode=None;"
```

- En un XAMPP recién instalado el usuario suele ser `root` y la contraseña vacía (`Password=;`). Úsalo solo si de verdad es tu configuración y solo en tu equipo.
- Nunca subas contraseñas a GitHub ni las pegues en documentos. En un servidor real se usan secretos del servidor y conexión cifrada (TLS).

El backend también necesita la clave `Jwt:Key` para arrancar; ver `backend/GUIA-AUTENTICACION.md`.

## 3. Arrancar y comprobar

```powershell
dotnet restore
dotnet run
```

Abre en el navegador:

- http://localhost:5080/health — debe responder `status: ok`.
- http://localhost:5080/health/database — debe responder `status: ok`, `database: camion_ventas` y el número de tablas (`tables: 39` si se importó el script completo). Si MariaDB está apagado o la cadena es incorrecta, responde 503 "No fue posible conectar con MariaDB."

## 4. Cómo usa el código la base de datos

- El backend usa **Entity Framework Core** con el proveedor Pomelo para MariaDB 10.4 y **ASP.NET Core Identity** para las cuentas (`backend/Camion.Api/Program.cs`, `backend/Camion.Api/Data/CamionDbContext.cs`). IMPLEMENTADO.
- Hoy el código solo usa las tablas de usuarios y roles de Identity (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`) y `perfiles_comprador`. Las demás tablas del script existen, pero ninguna función las usa todavía.
- **No hay migraciones de EF.** Las tablas las crea el script SQL. **No** ejecutes `dotnet ef database update` ni crees una migración inicial: intentaría volver a crear tablas que ya existen.
- Los cambios futuros del esquema se harán solo con una migración reversible y con respaldo previo de la base. Nunca borres tablas ni datos.
