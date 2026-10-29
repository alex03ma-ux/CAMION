# Criterios de aceptación P4p: planes, suscripciones y catálogos

Contrato: `docs/criterios/P4p-contrato-api.md`. Decisiones: D-4, D-26, D-32, D-35, D-36 y D-37.
Cómo se prueba: API = prueba de integración; COMP = prueba de componente; MAN = prueba manual.

| Id | Dado | Cuando | Entonces | Marca | Prueba |
|---|---|---|---|---|---|
| CA-049-01 | Un administrador | Crea un plan válido | 201; aparece en la lista con "= 30 × meses días"; queda auditado | CONFIRMADO (RN-PL-001, D-35) | API + COMP |
| CA-049-02 | — | Crea un plan con un nombre repetido (sin distinguir mayúsculas) o con valores fuera de rango | 409 o 400 por campo | PROPUESTO | API + COMP |
| CA-050-01 | Un plan en uso | Se edita | 200; las suscripciones en curso no cambian | PROPUESTO | API |
| CA-051-01 | Un plan inactivo | Se intenta asignarlo o renovar con él | 400 en `planId`; las suscripciones que ya lo usan siguen | CONFIRMADO (RN-PL-002) | API |
| CA-066-01 | Un asesor | Abre "Mi suscripción" | Ve el plan, el estado, la fecha de fin, los días restantes y el cupo; sin plan, el mensaje del contrato | CONFIRMADO (RN-SU-001) | API + COMP |
| CA-067-01 | Una suscripción vigente o vencida | El administrador la renueva | El fin pasa a ser el mayor entre ahora y el fin anterior, más 30 × meses días; si estaba vencida, vuelve a ACTIVA | CONFIRMADO (RN-SU-002, D-35, D-36) | API |
| CA-067-02 | Un asesor | Intenta renovar | 403 (solo el administrador renueva) | CONFIRMADO (D-36) | API |
| CA-068-01 | Una suscripción vigente | El administrador la suspende con un motivo de 5 a 500 caracteres | SUSPENDIDA; las publicaciones del asesor dejan de verse en el portal | CONFIRMADO (RN-SU-003) | API |
| CA-069-01 | Una suscripción SUSPENDIDA | El administrador la reactiva | REACTIVADA (D-4); el portal vuelve a mostrar las publicaciones; no se extiende el fin | CONFIRMADO (RN-SU-004, D-4) / PROPUESTO (no extiende) | API |
| CA-SUS-01 | Un asesor con una suscripción viva | El administrador intenta asignarle otra | 409; tiene que renovarla o cambiar de plan | PROPUESTO | API |
| CA-SUS-02 | — | Una transición no válida | 409 "No se puede <acción> una suscripción en estado <estado>." | PROPUESTO | API + COMP |
| CA-082-01 | El administrador | Crea, renombra o activa/desactiva una marca, carrocería, material, tipo de máquina o combustible | Funciona; un duplicado (sin mayúsculas ni tildes) da 409; queda auditado; nunca se borra | CONFIRMADO (RN-PA-002, D-37) | API + COMP |
| CA-083-01 | El administrador | Crea o edita un modelo | Exige al menos una categoría (D-32); si se quita una categoría en uso, avisa cuántos vehículos quedan afectados | CONFIRMADO (RN-PA-003, D-32) | API + COMP |
| CA-D37-01 | Un vehículo con una marca o modelo que luego se desactiva | El dueño lo edita sin cambiarlos | 200; el formulario los muestra "(desactivado)" y los conserva; elegir otro elemento desactivado da 400 | CONFIRMADO (D-37) | API + COMP |
| CA-PER-01 | Un asesor, un comprador o alguien sin sesión | Llama a `/api/admin/planes`, `/suscripciones` o `/catalogos` | 403 (o 401 sin token); el asesor solo ve su propia suscripción | CONFIRMADO (RN-SE-006) | API |
| CA-MAN-01 | Migración 011 aplicada | La responsable asigna un plan a un asesor y lo suspende o reactiva | En el portal, las publicaciones del asesor desaparecen y vuelven | — | MAN |
