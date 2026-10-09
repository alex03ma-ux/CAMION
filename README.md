# CAMION — Financiamos Tu Camión

Estructura inicial de **Red de Venta – Financiamos Tu Camión (v3.0)**.

> Estado: **solo base técnica**. No hay módulos de negocio implementados.

## Tecnologías previstas

- Frontend: React, TypeScript y Vite. Tailwind CSS está previsto para la etapa de diseño.
- Backend: ASP.NET Core Web API (C#).
- Base de datos: MySQL, a integrar cuando comience el desarrollo de persistencia.
- ORM previsto: Entity Framework Core con proveedor compatible con MySQL.

## Estructura

```text
CAMION/
├── frontend/             # Aplicación React vacía (pantalla inicial)
├── backend/Camion.Api/   # API mínima sin endpoints de negocio
├── docs/                 # Documentación de alcance y siguientes pasos
├── .gitignore
└── README.md
```

## Requisitos locales

- Node.js 20.19+ o 22.12+ (para Vite 7)
- SDK .NET 8
- MySQL: no se necesita para ejecutar este esqueleto

## Ejecutar frontend

```bash
cd frontend
npm install
npm run dev
```

## Ejecutar backend

```bash
cd backend/Camion.Api
dotnet restore
dotnet run
```

El backend expone únicamente `GET /health` para comprobar que está encendido. No existe conexión a MySQL todavía ni se guardan datos.

## Qué NO se ha implementado

No hay registro/inicio de sesión, asesores, vehículos, fotografías, planes, pagos, catálogo, leads, créditos, búsquedas, fichas protegidas, auditoría ni base de datos creada.

## Seguridad

No guardar contraseñas, tokens, cadenas de conexión ni credenciales en Git. Los valores reales se configurarán mediante variables de entorno o secretos de desarrollo cuando se integren.

## Próxima etapa

Desarrollar las historias de usuario aprobadas una por una, empezando por HU-001, solo cuando se autorice.
