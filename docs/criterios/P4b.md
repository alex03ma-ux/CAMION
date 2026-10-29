# Criterios de aceptación P4b: publicaciones, destacados, venta y reasignación

Contrato: `docs/criterios/P4b-contrato-api.md`. Decisiones: D-21, D-22, D-30, D-33, D-34 y D-38 a D-41.
Cómo se prueba: API = prueba de integración; COMP = prueba de componente; MAN = prueba manual.

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-023-01 | Un asesor con plan vigente y cupo, y un vehículo ACTIVO con foto | Publica el vehículo con un título | 201; se ve en el portal con el precio y las fotos del vehículo; usa 1 cupo | CONFIRMADO (RN-CO-001) | API + COMP |
| CA-023-02 | Un asesor sin plan vigente | Intenta publicar | 409 "No tienes un plan activo…" | CONFIRMADO (RN-CO-001, D-21) | API + COMP |
| CA-023-03 | Cupo lleno (todas las publicaciones salvo las eliminadas) | Intenta publicar | 409 "Ya usas las N publicaciones…"; tras eliminar una, sí puede | CONFIRMADO (D-40) | API + COMP |
| CA-024-01 | El dueño o el administrador | Edita el título, la descripción o "negociable" | 200; quien no es dueño recibe 404 | CONFIRMADO (RN-CO-002) | API |
| CA-025-01 | Una publicación PUBLICADA | Se inactiva con motivo | Pasa a OCULTA; sale del portal; pierde su destacado; sigue contando en el cupo | CONFIRMADO (RN-CO-003, D-40) | API + COMP |
| CA-026-01 | Una publicación con plan vigente | Se renueva | Nueva vigencia hasta el fin del plan | CONFIRMADO (RN-SU-002, D-41) | API |
| CA-027-01 | Plan con N destacados | El asesor destaca, quita y destaca otra | Nunca más de N a la vez; puede cambiar cuantas veces quiera; la N+1 da 409 | CONFIRMADO (D-38) | API + COMP |
| CA-028-01 | Vence el plan del asesor | — | Sus publicaciones salen solas del portal y en el inventario aparecen como "Vencida"; al renovar el plan vuelven, sin tocar la publicación | CONFIRMADO (RN-CO-005, D-41) | API |
| CA-064-01 | Un vehículo publicado | Se marca vendido con fecha, precio final y origen (Otro exige observaciones) | El vehículo pasa a VENDIDO y la publicación a VENDIDA, fuera del portal; se registra en `ventas_vehiculo`; se quita el destacado | CONFIRMADO (RN-GC-001, RN-VE-014, D-39) | API + COMP |
| CA-064-02 | Una venta | Se revierte con motivo | El vehículo pasa a ACTIVO; la publicación queda OCULTA hasta reactivarla; la venta queda marcada como revertida | CONFIRMADO (minuta) | API + COMP |
| CA-065-01 | Un asesor | Abre "Mis publicaciones" | Ve solo las suyas (no las eliminadas), con el estado en texto, el contador de cupo y destacados y las acciones válidas | CONFIRMADO (RN-GC-002) | API + COMP |
| CA-D34-01 | El administrador | Reasigna un vehículo a otro asesor activo, con motivo | El vehículo y su publicación cambian de dueño; si el destino no tiene cupo, 409; queda auditado; un asesor no puede hacerlo (403) | CONFIRMADO (D-34) | API + COMP |
| CA-D33-04 | Un vehículo con una publicación PUBLICADA | Se intenta cambiar su categoría | 409; después de inactivar la publicación sí se puede | CONFIRMADO (D-33) | API |
| CA-MAN-02 | Migración 012 aplicada | La responsable publica, destaca, vende, revierte y reasigna | El portal y el inventario reflejan cada cambio | — | MAN |
