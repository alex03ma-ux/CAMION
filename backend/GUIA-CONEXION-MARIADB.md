# Backend - primera conexion MariaDB (XAMPP)

Requisitos: .NET SDK 8, XAMPP con MariaDB iniciado, esquema `camion_ventas` importado.

## 1. Actualizar el repositorio

```powershell
cd C:\Users\alex0\Documents\CAMION
git pull origin main
cd backend\Camion.Api
```

## 2. Configurar conexion privada

Para XAMPP local con usuario `root` sin contrasena (solo si realmente es tu configuracion):

```powershell
dotnet user-secrets set "ConnectionStrings:CamionDatabase" "Server=127.0.0.1;Port=3306;Database=camion_ventas;User ID=root;Password=;SslMode=None;"
```

Si tienes contrasena, reemplaza `Password=;` por `Password=TU_CLAVE;`.
Nunca subas contrasenas a GitHub. En despliegues reales usa secretos del servidor y TLS.

## 3. Restaurar y ejecutar

```powershell
dotnet restore
dotnet run
```

Abre:
- http://localhost:5080/health
- http://localhost:5080/health/database

La segunda ruta debe responder `status: ok`, `database: camion_ventas` y `tables: 39` si se importaron todas las tablas.

**Nota:** No se han configurado todavia Entity Framework Core ni Identity en el codigo. El esquema SQL ya contiene tablas Identity; cuando se integre EF Core, su mapeo debe respetar las tablas existentes. No ejecutar otra migracion inicial que las vuelva a crear.
