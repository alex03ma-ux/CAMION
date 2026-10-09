# Etapa 2 - Registro y login de compradores

Requisitos: .NET 8 SDK, XAMPP con MariaDB 10.4, tablas del script 002 importadas.

## 1. Actualiza el proyecto
```powershell
cd C:\Users\alex0\Documents\CAMION
git pull origin main
cd backend\Camion.Api
```

## 2. Configura JWT (secreto local)
Genera una clave aleatoria con PowerShell, sin publicarla:
```powershell
$key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set "Jwt:Key" "$key"
```
Mantén configurado `ConnectionStrings:CamionDatabase` del paso anterior.

## 3. Ejecuta
```powershell
dotnet restore
dotnet run
```

## 4. Prueba con PowerShell (en otra terminal)
```powershell
$body = @{ nombreCompleto="Comprador Prueba"; email="comprador.prueba@example.com"; telefono="3001234567"; password="PruebaSegura123" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5080/api/auth/registro/comprador" -Method Post -ContentType "application/json" -Body $body
$login = @{ email="comprador.prueba@example.com"; password="PruebaSegura123" } | ConvertTo-Json
$resultado = Invoke-RestMethod -Uri "http://localhost:5080/api/auth/login" -Method Post -ContentType "application/json" -Body $login
$resultado | Select-Object usuarioId, nombreCompleto, email, roles
Invoke-RestMethod -Uri "http://localhost:5080/api/auth/me" -Headers @{ Authorization = "Bearer $($resultado.accessToken)" }
```

## Estado y pendientes
- Registro y login de compradores implementados; JWT con vencimiento de 2 horas.
- `/api/auth/me` comprueba el estado y la version de sesion del usuario.
- **Pendiente antes de produccion:** confirmacion de correo, recuperacion/cambio de contrasena, revocacion inmediata en todos los endpoints, limitacion de peticiones y bloqueo por IP, pruebas automatizadas, roles/registro de asesor y aprobacion administrativa.
- El script SQL fue importado previamente: NO ejecutar `dotnet ef database update` con una migracion inicial que recree las tablas.
- No se ha compilado ni probado el codigo en tu PC todavia.
