# Backlog coordinado

Fuente: minuta de transferencia, sección 11. Estados: PENDIENTE / EN CURSO / BLOQUEADO (por D-n) / EN REVISIÓN (PR abierto) / APROBADO.
El estado funcional de cada módulo se toma de `ESTADO-REAL.md`, no de este archivo.

| Orden | Trabajo | Criterio de salida | Estado | Rama / PR |
|---|---|---|---|---|
| P0 | Auditoría inicial de repositorio y requisitos | Mapa real de endpoints, tablas, HU y cobertura; diferencias documentadas (`ESTADO-REAL.md`) | EN REVISIÓN | `feature/p0-auditoria` |
| P1 | Robustecer autenticación | Duplicados, errores HTTP claros, contraseñas inválidas, permisos, expiración, persistencia de sesión, registro de comprador inmediato y de solo lectura (D-3), pruebas y UX | PENDIENTE (desbloqueado por P0; trazabilidad depende de D-8) | — |
| P2 | Seguridad de usuarios y aprobación de asesores (D-3) | Asesor registrado en estado Pendiente; aprobación/rechazo/bloqueo por el administrador protegidos en API; comprador sin permisos de edición; auditoría | BLOQUEADO (D-10) | — |
| P3 | Catálogo público de vehículos | Listado, detalle, filtros, búsqueda y visibilidad por estado | PENDIENTE | — |
| P4 | Inventario y publicaciones | CRUD seguro, imágenes, plan/cupo, vigencia y propietario | PENDIENTE (placa opcional y única, D-2; estados BLOQUEADO por D-11) | — |
| P5 | Leads y contactos | Captura anónima; asignación al asesor dueño solo con plan vigente, si no al administrador (D-7); seguimiento trazable | BLOQUEADO (D-9) | — |
| P5b | Avisos "Vende tu camión" y "Compra tu seguro todo riesgo" (HU-N01, HU-N02) | Formularios públicos sin cuenta, validaciones (500 caracteres, teléfono), solicitudes visibles solo para el administrador (D-6) | BLOQUEADO (D-9) | — |
| P6 | Créditos y búsquedas | Formularios, estados, asignación al administrador (D-7), notificaciones y seguimiento | PENDIENTE | — |
| P7 | Ventas, planes y administración | Historial, reversión, parametrización, panel administrativo | PENDIENTE | — |
| P8 | Reportes, marketing e integraciones | Indicadores e integraciones aprobadas; pruebas y operación | PENDIENTE | — |
| INF-1 | CI en GitHub Actions (build + pruebas en cada PR) | Flujo verde en un PR de prueba | PENDIENTE | — |

## Primer encargo (P1, tras P0)

Desde el commit `4e41f43` (auditado en `ESTADO-REAL.md`), sin reescribir: revisar el módulo de autenticación y sus contratos reales; reproducir el registro con correo duplicado sin exponer credenciales; mejorar los mensajes de error del frontend según las respuestas reales del backend; añadir pruebas para registro exitoso, duplicado, validaciones, login incorrecto y `/me` sin token; compilar, probar y abrir PR.

Hallazgos de P0 que alimentan P1 (ver `ESTADO-REAL.md`, sección 10): sesión no persiste al recargar; "Cerrar sesión" no invalida el token (`SessionVersion` nunca se incrementa); reglas de contraseña no mostradas en el formulario; correo > 256 caracteres o registros simultáneos dan 500; sin limitación de peticiones; sin `global.json`; `*.tsbuildinfo` fuera de `.gitignore`.
