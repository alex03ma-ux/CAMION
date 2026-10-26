# Backlog coordinado

Fuente: minuta de transferencia, sección 11. Estados: PENDIENTE / EN CURSO / BLOQUEADO (por D-n) / EN REVISIÓN (PR abierto) / APROBADO.
El estado funcional de cada módulo se toma de `ESTADO-REAL.md`, no de este archivo.

| Orden | Trabajo | Criterio de salida | Estado | Rama / PR |
|---|---|---|---|---|
| P0 | Auditoría inicial de repositorio y requisitos | Mapa real de endpoints, tablas, HU y cobertura; diferencias documentadas (`ESTADO-REAL.md`) | PENDIENTE | — |
| P1 | Robustecer autenticación | Duplicados, errores HTTP claros, contraseñas inválidas, permisos, expiración, persistencia de sesión, pruebas y UX | PENDIENTE (depende de P0) | — |
| P2 | Seguridad de usuarios y aprobación de asesores | Roles y flujos aprobatorios protegidos en API; auditoría | PENDIENTE (depende de D-3) | — |
| P3 | Catálogo público de vehículos | Listado, detalle, filtros, búsqueda y visibilidad por estado | PENDIENTE | — |
| P4 | Inventario y publicaciones | CRUD seguro, imágenes, plan/cupo, vigencia y propietario | PENDIENTE (depende de D-2) | — |
| P5 | Leads y contactos | Captura anónima, asignación y seguimiento trazable | PENDIENTE | — |
| P6 | Créditos y búsquedas | Formularios, estados, asignaciones, notificaciones y seguimiento | PENDIENTE | — |
| P7 | Ventas, planes y administración | Historial, reversión, parametrización, panel administrativo | PENDIENTE | — |
| P8 | Reportes, marketing e integraciones | Indicadores e integraciones aprobadas; pruebas y operación | PENDIENTE | — |
| INF-1 | CI en GitHub Actions (build + pruebas en cada PR) | Flujo verde en un PR de prueba | PENDIENTE | — |

## Primer encargo (P1, tras P0)

Desde el commit `eb1a733`, sin reescribir: revisar el módulo de autenticación y sus contratos reales; reproducir el registro con correo duplicado sin exponer credenciales; mejorar los mensajes de error del frontend según las respuestas reales del backend; añadir pruebas para registro exitoso, duplicado, validaciones, login incorrecto y `/me` sin token; compilar, probar y abrir PR.
