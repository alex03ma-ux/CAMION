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

## 2026-10-26 — P1 Robustecer autenticación
- Rama `feature/p1-autenticacion` desde `main` (`a3212ea`).
- Contrato de API fijado por el coordinador antes de programar (`docs/criterios/P1-contrato-api.md`); `analista-funcional` escribió 81 criterios (`docs/criterios/P1.md`) trazados a HU-N03, HU-059 a HU-062 y RN-SE-006. HU-063 (recuperar contraseña) BLOQUEADO: no hay servicio de correo (D-5).
- Por eficiencia, criterios, backend y frontend se ejecutaron en paralelo sobre el contrato fijado (archivos distintos). Las contradicciones del analista se resolvieron en el contrato:
  - C-1: se aceptan 409 y 423 aunque revelen que el correo existe (decisión técnica reversible).
  - C-2 y C-3: la inactividad solo cierra la sesión local, comprueba la última actividad al cargar y la comparte entre pestañas.
  - C-4: pasa a P2.
  - C-5: el contrato se completó.
- Backend: validación manual con mensajes en español; 409 también en registros simultáneos; 400/401/423 en el login; nuevo `POST /api/auth/logout` (incrementa `SessionVersion`); comprobación de sesión en `OnTokenValidated` para toda la API; log de auditoría sin datos sensibles; JSON malformado → 400 `{ error }`; `global.json` con el SDK 8.0.425.
- Frontend: módulos `api/`, `auth/` y `components/`; sesión en `localStorage` (riesgo XSS aceptado para P1); reglas de contraseña visibles (solo ASCII); cierre por inactividad de 30 min (PROPUESTO); `*.tsbuildinfo` en `.gitignore`.
- Pruebas (nuevas): `backend/Camion.Api.Tests` (xUnit, WebApplicationFactory, SQLite en memoria; nunca toca `camion_ventas`) y Vitest + Testing Library en `frontend/tests`.
  - Defecto D-P1-01: un JSON malformado daba 400 sin cuerpo. Se corrigió.
  - Limitación: el choque de registros simultáneos contra el índice único de MariaDB no se reproduce con SQLite.
- `devops-seguridad`: sin secretos, sin cambios de esquema, `npm audit` sin vulnerabilidades.
  - Bloqueante: SQLite nativo transitivo con GHSA-2m69-gcr7-jv3q. Corregido fijando `SQLitePCLRaw.lib.e_sqlite3` 3.53.3; la 2.1.11 que se propuso al principio también es vulnerable.
  - Recomendaciones aplicadas: errores 500 con el formato común y manejador global de excepciones.
  - Pendiente: CI (INF-1).
- Sin E2E con navegador (Playwright): la verificación manual con la app arrancada queda para la responsable o para un ítem posterior.
- `gh` no está instalado: la rama se publica y el PR lo abre la responsable.
- PR [#3](https://github.com/alex03ma-ux/CAMION/pull/3) abierto para P1. Pruebas verificadas de nuevo por el coordinador principal: 76/76 backend, 42/42 frontend. La responsable resolvió D-12: 30 minutos de inactividad; "Cerrar sesión" en todos los equipos.
- Prueba manual de P1 hecha por la responsable con la app arrancada contra MariaDB `camion_ventas` (2026-10-26): registro de comprador con acceso inmediato, sesión conservada al recargar, cierre de sesión, contraseña incorrecta con mensaje claro, bloqueo de 15 minutos al 5.º intento fallido (los intentos durante el bloqueo se rechazan) e inicio de sesión correcto pasado el bloqueo. Todo según lo esperado; evidencia en el registro de auditoría del servidor. Queda una cuenta de prueba en la base de datos.

## 2026-10-26 — P2 Asesores y administración (EN CURSO, interrumpido por límite de uso)
- Rama `feature/p2-asesores` desde `main` (`b0a9506`). Contrato `docs/criterios/P2-contrato-api.md`, 109 criterios en `docs/criterios/P2.md`, D-13 y D-14 TOMADAS, D-15 PENDIENTE.
- IMPLEMENTADO (sin pruebas P2 todavía):
  - Backend: registro de asesor, login con 403 por estado, roles y políticas, panel `/api/admin/usuarios`, auditoría, `promover-admin` y migración 003 (sin aplicar). `dotnet build` sin errores; 75 de 76 pruebas de P1 pasan. La que falla es el inventario de endpoints, que hay que actualizar por las rutas nuevas.
  - Frontend: registro de asesor, avisos de estado en el login y panel de administración. `npm run build` correcto; las 42 pruebas de P1 pasan.
- Pendiente:
  - Pruebas de API y de componentes de P2 (los agentes de QA se detuvieron antes de terminar).
  - Revisión de `devops-seguridad`.
  - Documentación (README, guía de autenticación, `docs/GUIA-ADMINISTRACION.md`).
  - **Migración 003 NO aplicada**: la condición 4 de D-14 exige que antes pasen las pruebas de P2.
