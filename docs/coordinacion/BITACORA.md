# Bitácora de coordinación

Registro interno del coordinador y los agentes (progreso, decisiones técnicas reversibles, reintentos). La responsable solo recibe decisiones y entregas.

## 2026-10-26 — Configuración del flujo de agentes
- Rama `feature/flujo-agentes-ia` creada desde `main` (`eb1a733`).
- Creados: comando `/coordinar`, 7 agentes especializados, `CLAUDE.md`, reglas de permisos en `.claude/settings.json`, copia en texto de los documentos fuente en `docs/requisitos/`, backlog y registro de decisiones (D-1 a D-5).
- PR [#1](https://github.com/alex03ma-ux/CAMION/pull/1) abierto contra `main` (sin merge).
- La responsable resolvió D-1 a D-5: 87 HU oficiales (CRM fuera de alcance); placa opcional pero única; aprobación obligatoria para todas las cuentas; contraseña elegida al registrarse; estados Crédito Cerrado, Vehículo Vendido y Suscripción Reactivada añadidos. Ver `DECISIONES.md`.
- Corrección de la responsable sobre D-3: solo los asesores requieren aprobación. El comprador es un perfil independiente de registro opcional e inmediato y de solo lectura, que puede pedir seguro o crédito y ofrecer un camión. Las funciones nuevas (seguro y ofrecer un camión) quedan como D-6 PENDIENTE.
- D-6 resuelta: avisos "Vende tu camión" (HU-N01) y "Compra tu seguro todo riesgo" (HU-N02), atendidos por el administrador. Nueva D-7: todo servicio lo atiende el administrador; el asesor solo atiende los contactos de sus vehículos publicados con plan vigente. Añadido el ítem P5b al backlog.
- D-7 completada: el administrador puede reasignar manualmente cualquier solicitud a un asesor, con auditoría.
- Observación preliminar (no es auditoría): el README afirma que no hay autenticación, pero `main` contiene registro/login con Identity y JWT; no existen proyectos de prueba ni CI; el esquema vive en scripts SQL de `database/` sin migraciones EF. Pendiente de confirmar por `auditor-codigo` (P0).

## 2026-10-26 — P0 Auditoría inicial
- Rama `feature/p0-auditoria` desde `main` (`4e41f43`). `auditor-codigo` escribió `ESTADO-REAL.md` (solo lectura; sin cambios de código ni de base de datos).
- Resultado: 5 endpoints (2 de salud, 3 de autenticación); 39 tablas en scripts, 8 mapeadas en EF y 4 usadas; sin migraciones EF, sin pruebas, sin CI. Frontend de una sola pantalla.
- Compilaciones ejecutadas: `dotnet build` 0 errores / 0 advertencias (SDK 10.0.401, objetivo net8); `npm run build` correcto (Vite 7.3.7).
- Contraste con decisiones: el código cumple D-3 (comprador inmediato, rol fijo COMPRADOR, sin endpoints de escritura) y D-5. El esquema SQL no cumple D-2 (sin placa), D-4 (estados) ni D-6 (solicitudes exigen usuario y correo; sin tipos venta/seguro).
- Registradas D-8 a D-11 como PENDIENTE. P2 bloqueado por D-10; P5, P5b y P6 por D-9; estados de P4 por D-11. P1 puede empezar.
- La compilación dejó `frontend/tsconfig.*.tsbuildinfo` sin seguimiento; no se commitean (propuesta: añadirlos a `.gitignore` en P1).
- La responsable resolvió D-8 a D-11: HU-N03 para el registro de comprador; contacto sin cuenta con nombre y teléfono (correo opcional); estados del asesor Pendiente/Aprobado/Rechazado/Bloqueado; se mantiene el estado "Reservado". P2, P5 y P5b desbloqueados.
