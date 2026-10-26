# Registro de decisiones

Estados: PENDIENTE (requiere a la responsable) / TOMADA / DESCARTADA.
Las decisiones D-1 a D-5 se detectaron al comparar los documentos fuente el 2026-10-26 y la responsable las resolvió el mismo día.

## D-1 — ¿Qué numeración de HU es la oficial? · TOMADA (2026-10-26)
- Contexto: en `requisitos-funcionales-v3.0.md` la matriz consolidada (sección 7) tiene 87 HU (HU-043 = Aprobar Usuario), pero la lista de la sección 10 tiene 90 (HU-043 = Registrar Usuario, todo desplazado +1, y un módulo nuevo "CRM Comercial" HU-089/HU-090).
- **Decisión: la matriz de 87 HU (sección 7) es la numeración oficial.** Commits, criterios y pruebas citan esa numeración. El módulo CRM Comercial (HU-089/HU-090 de la sección 10) queda fuera del alcance actual y se tratará como cambio de alcance posterior.

## D-2 — ¿La placa es obligatoria y única? · TOMADA (2026-10-26)
- Contexto: la minuta dice "placa única"; RN-VE-002 (v3.0) dice "Dato opcional. No obligatoria ni única".
- **Decisión: la placa es opcional, pero única cuando se registra.** Se permite dejarla vacía; si se informa, no puede repetirse en otro vehículo (validación en API y restricción en base de datos que admita varios vacíos). Esto modifica RN-VE-002 respecto a la línea base v3.0.

## D-3 — ¿Quién requiere aprobación del administrador? · TOMADA (2026-10-26)
- Contexto: RN-US-001 dice "Todo usuario debe ser aprobado"; la minuta indicaba solo asesores y el registro de comprador actual permite entrar sin aprobación.
- **Decisión: todos los usuarios con cuenta (compradores y asesores) requieren aprobación del administrador (RN-US-001).** El registro crea la cuenta en estado Pendiente; no se puede iniciar sesión hasta que esté Aprobado.
- Impacto: el comportamiento actual de `main` (comprador entra al registrarse) **no cumple** esta regla y debe corregirse en P1/P2. Los visitantes sin cuenta siguen pudiendo consultar el catálogo, contactar y solicitar crédito (RN-SE-005, RN-CL-001).
- Pendiente de confirmar al implementar: qué ocurre con las cuentas de comprador de prueba ya creadas (recomendación: dejarlas en Pendiente o aprobarlas manualmente; nunca borrarlas sin autorización).

## D-4 — Estados incompletos en la línea base · TOMADA (2026-10-26)
- Contexto: las transiciones usan Crédito "Cerrado" y Suscripción "Reactivada", que no están en la tabla de estados; RN-VE-008 menciona solo Activo/Inactivo pero existe Vendido.
- **Decisión: se añaden los tres como estados propios:** Crédito `Cerrado`, Vehículo `Vendido` y Suscripción `Reactivada`.
- Estados resultantes: Vehículo Activo/Inactivo/Vendido; Crédito Registrado/Asignado/Gestionado/Cerrado; Suscripción Activa/Suspendida/Reactivada/Vencida.

## D-5 — Envío de credenciales vs. autorregistro · TOMADA (2026-10-26)
- Contexto: RN-US-004 "Envía credenciales iniciales" por correo, mientras que el registro actual permite que el usuario elija su contraseña. No hay servicio de correo configurado.
- **Decisión: autorregistro con contraseña elegida por el usuario.** No se envían credenciales. Cuando se apruebe una integración de correo, el correo solo notificará la aprobación de la cuenta.
