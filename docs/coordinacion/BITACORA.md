# Bitácora de coordinación

Registro interno del coordinador y los agentes (progreso, decisiones técnicas reversibles, reintentos). La responsable solo recibe decisiones y entregas.

## 2026-10-26 — Configuración del flujo de agentes
- Rama `feature/flujo-agentes-ia` creada desde `main` (`eb1a733`).
- Creados: comando `/coordinar`, 7 agentes especializados, `CLAUDE.md`, reglas de permisos en `.claude/settings.json`, copia en texto de los documentos fuente en `docs/requisitos/`, backlog y registro de decisiones (D-1 a D-5).
- Observación preliminar (no es auditoría): el README afirma que no hay autenticación, pero `main` contiene registro/login con Identity y JWT; no existen proyectos de prueba ni CI; el esquema vive en scripts SQL de `database/` sin migraciones EF. Pendiente de confirmar por `auditor-codigo` (P0).
