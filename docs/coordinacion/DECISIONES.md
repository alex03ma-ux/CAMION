# Registro de decisiones

Estados: PENDIENTE (requiere a la responsable) / TOMADA / DESCARTADA.
Las decisiones D-1 a D-5 se detectaron al comparar los documentos fuente el 2026-10-26, antes de la auditoría del código.

## D-1 — ¿Qué numeración de HU es la oficial? · PENDIENTE
- Contexto: en `requisitos-funcionales-v3.0.md` la matriz consolidada (sección 7) tiene 87 HU (HU-043 = Aprobar Usuario), pero la lista de la sección 10 tiene 90 (HU-043 = Registrar Usuario, todo desplazado +1, y un módulo nuevo "CRM Comercial" HU-089/HU-090).
- A) Matriz de 87 HU — coincide con la minuta de transferencia y con las RN; el CRM quedaría fuera de alcance por ahora.
- B) Lista de 90 HU — incluye registro de usuario y CRM; hay que renumerar la trazabilidad.
- Recomendación: A, y tratar el CRM como cambio de alcance posterior.
- Bloquea: trazabilidad de commits y criterios (no bloquea P0).

## D-2 — ¿La placa es obligatoria y única? · PENDIENTE
- Contexto: la minuta dice "placa única"; RN-VE-002 (v3.0) dice "Dato opcional. No obligatoria ni única".
- A) Opcional y no única (RN-VE-002, línea base v3.0).
- B) Opcional, pero única cuando se registra.
- C) Obligatoria y única.
- Recomendación: A, salvo decisión comercial posterior (la línea base prevalece).
- Bloquea: P4.

## D-3 — ¿Quién requiere aprobación del administrador? · PENDIENTE
- Contexto: RN-US-001 dice "Todo usuario debe ser aprobado", pero la minuta indica que solo el asesor requiere aprobación y el registro de comprador ya funciona sin ella (rol COMPRADOR). El rol "Comprador" no aparece en los actores v3.0 (solo Cliente/visitante).
- A) Solo asesores requieren aprobación; compradores se activan al registrarse.
- B) Todos los usuarios con cuenta requieren aprobación.
- Recomendación: A (no frena la conversión del cliente y coincide con lo ya construido).
- Bloquea: P2.

## D-4 — Estados incompletos en la línea base · PENDIENTE
- Contexto: las transiciones usan Crédito "Cerrado" y Suscripción "Reactivada", que no están en la tabla de estados; RN-VE-008 menciona solo Activo/Inactivo pero existe Vendido.
- A) Añadir esos estados tal como aparecen en las transiciones.
- B) Tratar "Reactivada" como volver a "Activa" (sin estado propio).
- Recomendación: A para Crédito Cerrado y Vehículo Vendido; B para Suscripción.
- Bloquea: P6, P7.

## D-5 — Envío de credenciales vs. autorregistro · PENDIENTE
- Contexto: RN-US-004 "Envía credenciales iniciales" por correo, mientras que el registro actual permite que el usuario elija su contraseña. No hay servicio de correo configurado.
- A) Autorregistro con contraseña propia; el correo solo notifica la aprobación (cuando exista integración).
- B) El administrador crea cuentas de asesor y el sistema envía credenciales temporales.
- Recomendación: A; posponer el envío de correos hasta aprobar la integración.
- Bloquea: parte de P2.
