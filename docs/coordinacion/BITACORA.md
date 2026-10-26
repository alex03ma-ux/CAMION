# Bitácora de coordinación

Registro interno del coordinador y los agentes (progreso, decisiones técnicas reversibles, reintentos). La responsable solo recibe decisiones y entregas.

## 2026-10-26 — Configuración del flujo de agentes
- Rama `feature/flujo-agentes-ia` creada desde `main` (`eb1a733`).
- Creados: comando `/coordinar`, 7 agentes especializados, `CLAUDE.md`, reglas de permisos en `.claude/settings.json`, copia en texto de los documentos fuente en `docs/requisitos/`, backlog y registro de decisiones (D-1 a D-5).
- PR [#1](https://github.com/alex03ma-ux/CAMION/pull/1) abierto contra `main` (sin merge).
- La responsable resolvió D-1 a D-5: 87 HU oficiales (CRM fuera de alcance); placa opcional pero única; aprobación obligatoria para todas las cuentas; contraseña elegida al registrarse; estados Crédito Cerrado, Vehículo Vendido y Suscripción Reactivada añadidos. Ver `DECISIONES.md`.
- Corrección de la responsable sobre D-3: solo los asesores requieren aprobación. El comprador es un perfil independiente de registro opcional e inmediato y de solo lectura, que puede pedir seguro o crédito y ofrecer un camión. Las funciones nuevas (seguro y ofrecer un camión) quedan como D-6 PENDIENTE.
- Observación preliminar (no es auditoría): el README afirma que no hay autenticación, pero `main` contiene registro/login con Identity y JWT; no existen proyectos de prueba ni CI; el esquema vive en scripts SQL de `database/` sin migraciones EF. Pendiente de confirmar por `auditor-codigo` (P0).
