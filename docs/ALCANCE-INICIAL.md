# Alcance actual del repositorio

El repositorio empezó como una base técnica sin funciones de negocio. Este documento resume qué hay hoy en la rama `feature/p1-autenticacion`. El estado verificado línea por línea está en `docs/coordinacion/ESTADO-REAL.md` y el orden de trabajo en `docs/coordinacion/BACKLOG.md`.

## Tecnologías confirmadas

- Frontend: React 19 + Vite 7 + TypeScript.
- Backend: ASP.NET Core .NET 8 (minimal APIs), con ASP.NET Core Identity y tokens JWT.
- Base de datos: MariaDB 10.4 (XAMPP), base `camion_ventas`, conectada con Entity Framework Core (proveedor Pomelo).
- SDK fijado en `global.json` (.NET 8).
- Diseño con Tailwind CSS: previsto, todavía no instalado.

## Implementado (código existente; aún sin pruebas automáticas, nada PROBADO)

- Registro de comprador con acceso inmediato y rol fijo `COMPRADOR` (HU-N03).
- Inicio de sesión con token de 2 horas y bloqueo de 15 minutos tras 5 intentos fallidos (HU-059).
- Ver mi perfil (`/api/auth/me`) y cerrar sesión en todos los dispositivos (`/api/auth/logout`) (HU-059, HU-060).
- En el frontend: sesión que se mantiene al recargar, mensajes de error claros y cierre por inactividad tras 30 minutos (valor PROPUESTO).
- Registro de eventos de seguridad en el log del servidor (HU-061, sin tabla todavía).
- Conexión a MariaDB; el código usa solo las tablas de usuarios, roles y perfil de comprador.

## No implementado

- Funciones de asesor (registro, aprobación) y de administrador (panel, atención de servicios).
- Catálogo, vehículos, publicaciones, fotografías, planes, pagos, contactos, créditos, búsquedas, favoritos, seguros y avisos "Vende tu camión".
- Permisos por rol, migraciones de base de datos y auditoría guardada en tabla.
- Integraciones con correo, WhatsApp o pagos. La recuperación de contraseña está BLOQUEADA por falta de servicio de correo aprobado.

## Nota

Las historias de usuario se implementan por etapas, en el orden de `docs/coordinacion/BACKLOG.md`. La elección del mecanismo de renderizado público y SEO se revisará antes de construir el catálogo.
