# CAMION — instrucciones para agentes

Proyecto **Red de Venta – Financiamos Tu Camión** (línea base funcional v3.0).
Stack: React 19 + Vite 7 + TypeScript (`frontend/`), ASP.NET Core .NET 8 + Identity/JWT (`backend/Camion.Api`), MariaDB `camion_ventas` (XAMPP, puerto 3306). Backend en http://localhost:5080, frontend en http://localhost:5173 (proxy `/api` y `/health`).

## Cómo se trabaja

- El trabajo se coordina con el comando **`/coordinar`** (`.claude/skills/coordinar/SKILL.md`), que reparte tareas entre los agentes de `.claude/agents/` y solo presenta a la responsable decisiones de negocio y entregas listas para aprobar.
- **Primero se audita el código** (`auditor-codigo` → `docs/coordinacion/ESTADO-REAL.md`). Nada documentado se considera implementado sin esa verificación. El README puede estar desactualizado.
- Requisitos: `docs/requisitos/`. Estado real, backlog, decisiones y bitácora: `docs/coordinacion/`.

## Reglas de negocio confirmadas por la responsable

- **Comprador**: perfil independiente (no es asesor ni administrador).
  - Registro opcional; sin cuenta se consulta el catálogo y se contacta.
  - Si se registra, el acceso es inmediato, sin aprobación.
  - Acceso de solo lectura: nunca puede crear, editar ni modificar vehículos, publicaciones ni datos de otros.
  - Busca opciones de compra y puede pedir seguro, solicitar crédito y vender su propio vehículo.
- Solo los **asesores** requieren aprobación del administrador.
- **Todo servicio lo atiende el administrador** (créditos, búsquedas, solicitudes generales, venta de vehículos de clientes, seguros). El asesor solo atiende los contactos de **sus propios vehículos publicados con plan vigente**, o lo que el administrador le reasigne manualmente; la reasignación se audita (D-7).
- Avisos del portal (D-6), ambos sin cuenta y dirigidos al administrador:
  - **"Vende tu camión"**: nombre, teléfono WhatsApp y descripción del vehículo (máx. 500 caracteres).
  - **"Compra tu seguro todo riesgo"**: nombre, teléfono y tipo de vehículo.
- El resto de decisiones vigentes está en `docs/coordinacion/DECISIONES.md`; consúltalas antes de diseñar.

## Reglas permanentes

- Ramas `feature/*`; commits pequeños citando HU/RN; PR contra `main` sin merge. Nunca push a `main`, merge, `--force` ni despliegues sin aprobación humana.
- Nunca DROP/TRUNCATE ni borrar/sobrescribir respaldos (`frontend/respaldos/`, `C:\Respaldo_CAMION\`). Cambios de esquema solo con migración reversible y respaldo previo.
- Nunca leer, imprimir ni commitear secretos; usar `dotnet user-secrets` o variables de entorno.
- Estados de reporte: IMPLEMENTADO / PROBADO / DOCUMENTADO / PROPUESTO / BLOQUEADO. No afirmar que una prueba pasó si no se ejecutó.
- Ante una ambigüedad de negocio: registrarla en `docs/coordinacion/DECISIONES.md`, detener solo esa parte y seguir con trabajo independiente.
- No tocar otras bases de datos ni otros proyectos (p. ej. My English Adventure).
