-- =====================================================================
-- Migración 011 — Estados de la suscripción (P4p, D-4). SOLO SUBIR, para phpMyAdmin (D-16).
-- Requisitos: respaldo previo del coordinador en C:\Respaldo_CAMION\ y API detenida.
-- Solo cambia la restricción CHECK CK_Sus_Estado; no toca datos ni otras tablas.
-- Verificación y BAJAR: 011_p4p_estados_suscripcion.sql (cliente mysql del coordinador).
-- =====================================================================
ALTER TABLE suscripciones DROP CONSTRAINT CK_Sus_Estado;

ALTER TABLE suscripciones
  ADD CONSTRAINT CK_Sus_Estado
  CHECK (estado IN ('PENDIENTE','ACTIVA','SUSPENDIDA','REACTIVADA','VENCIDA','CANCELADA','RECHAZADA'));
