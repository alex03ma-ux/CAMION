-- =====================================================================
-- Migración 006 — Nota de reserva del vehículo (D-28). SOLO la sección SUBIR,
-- para importar en phpMyAdmin (D-16; lección de la 004: sin consultas a information_schema).
-- Requisitos: migración 004 aplicada, API detenida, respaldo previo del coordinador.
-- Verificación y BAJAR: 006_p4a_nota_reserva.sql (cliente mysql del coordinador).
-- Solo añade una columna opcional; no modifica ni borra filas.
-- =====================================================================
ALTER TABLE vehiculos
  ADD COLUMN nota_reserva VARCHAR(500) NULL AFTER motivo_inactivacion;
