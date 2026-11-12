-- =====================================================================
-- Migración 016 — Registro de pagos del plan (P7, HU-066, HU-067, FN-04, D-26, D-36, D-61 B).
-- SOLO SUBIR, para phpMyAdmin (D-16). Sin consultas a information_schema.
-- Requisitos: respaldo previo del coordinador en C:\Respaldo_CAMION\ y API detenida.
-- Verificación y BAJAR: 016_p7_pagos.sql (cliente mysql del coordinador, base camion_ventas).
-- Solo ALTER TABLE pagos (tabla del script 002, sin filas). No crea tablas ni toca filas.
-- Único DROP: DROP CONSTRAINT CK_Pago_Estado, seguido de la sentencia que lo vuelve a crear ampliado.
-- Sin DROP TABLE, DROP COLUMN, TRUNCATE ni DELETE.
-- =====================================================================

-- 1. Columnas nuevas, todas NULL (la tabla está vacía; NULL evita fallar si alguien anotó filas antes).
--    verificado_por / verificado_en se reutilizan como "registrado por / en". soporte_archivo no se usa (D-61 B).
ALTER TABLE pagos
  ADD COLUMN fecha_pago DATE NULL AFTER valor_cop,
  ADD COLUMN medio VARCHAR(20) NULL AFTER fecha_pago,
  ADD COLUMN concepto VARCHAR(20) NULL AFTER referencia,
  ADD COLUMN anulado_por VARCHAR(255) NULL AFTER verificado_en,
  ADD COLUMN anulado_en DATETIME(6) NULL AFTER anulado_por,
  ADD COLUMN motivo_anulacion VARCHAR(500) NULL AFTER anulado_en,
  ADD CONSTRAINT FK_Pago_AnuladoPor FOREIGN KEY (anulado_por) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;

-- 2. CK_Pago_Estado admite REGISTRADO y ANULADO (además de los tres del script 002). Se quita y se vuelve a crear en
--    dos sentencias seguidas; la tabla no tiene filas que lo incumplan.
ALTER TABLE pagos DROP CONSTRAINT CK_Pago_Estado;
ALTER TABLE pagos
  ADD CONSTRAINT CK_Pago_Estado CHECK (estado IN ('PENDIENTE','APROBADO','RECHAZADO','REGISTRADO','ANULADO'));

-- 3. CHECK nuevos. IS NULL / IS NOT NULL explícitos: un IN con NULL da NULL y un CHECK con resultado NULL deja pasar la fila.
ALTER TABLE pagos
  ADD CONSTRAINT CK_Pago_Medio CHECK (medio IS NULL OR medio IN ('TRANSFERENCIA','CONSIGNACION','EFECTIVO','OTRO')),
  ADD CONSTRAINT CK_Pago_Concepto CHECK (concepto IS NULL OR concepto IN ('ASIGNACION','RENOVACION','CAMBIO_PLAN','POSTERIOR')),
  ADD CONSTRAINT CK_Pago_Registro CHECK (estado NOT IN ('REGISTRADO','ANULADO') OR (fecha_pago IS NOT NULL AND medio IS NOT NULL)),
  ADD CONSTRAINT CK_Pago_Anulacion CHECK (estado <> 'ANULADO' OR (anulado_en IS NOT NULL AND motivo_anulacion IS NOT NULL));
