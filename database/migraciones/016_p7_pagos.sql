-- =====================================================================
-- Migración 016 — Registro de pagos del plan (P7, HU-066, HU-067, FN-04, D-26, D-36, D-61 B).
-- Base: camion_ventas (MariaDB 10.4). Contrato: docs/criterios/P7-contrato-api.md.
--
-- D-61 B: el cobro es fuera del portal; el administrador anota el pago (valor, fecha, medio y referencia opcional) al
-- asignar o renovar un plan con precio, y puede anularlo con motivo. Sin comprobante, pasarela, factura ni contabilidad.
--
-- Cambios (solo ALTER TABLE pagos, tabla del script 002):
--   1. Columnas nuevas, todas NULL: fecha_pago, medio, concepto, anulado_por (FK AspNetUsers ON DELETE SET NULL),
--      anulado_en y motivo_anulacion. verificado_por / verificado_en se reutilizan como "registrado por / en".
--   2. CK_Pago_Estado admite REGISTRADO y ANULADO. Es el único DROP de la parte SUBIR: DROP CONSTRAINT de un CHECK,
--      seguido de la sentencia que lo vuelve a crear; no borra datos.
--   3. CHECK nuevos: CK_Pago_Medio, CK_Pago_Concepto, CK_Pago_Registro (un pago REGISTRADO o ANULADO tiene fecha y
--      medio) y CK_Pago_Anulacion (un ANULADO tiene fecha y motivo de anulación).
-- No crea tablas ni toca filas. Sin DROP TABLE, DROP COLUMN, TRUNCATE ni DELETE en la parte SUBIR.
-- configuracion_sistema (parámetros, D-63 A) ya existe con las columnas que usa la API: no se toca.
--
-- Cómo aplicarla (D-16):
--   1. Respaldo completo nuevo en C:\Respaldo_CAMION\ (coordinador). No sobrescribir respaldos existentes.
--   2. Detener la API (la versión P7 mapea las columnas nuevas de pagos).
--   3. En phpMyAdmin (Chrome/Edge) importar SOLO 016_p7_pagos_SUBIR.sql.
--   4. El coordinador ejecuta la VERIFICACIÓN de este archivo con el cliente mysql (base camion_ventas).
-- Nota: en MariaDB cada ALTER hace commit implícito; el script no es atómico. Entre el DROP y el ADD de CK_Pago_Estado
-- la tabla queda un instante sin esa restricción: por eso la API debe estar detenida.
-- Comprobado el 2026-11-12 con consultas de solo lectura sobre camion_ventas (MariaDB 10.4.32): pagos tiene 0 filas y
-- las columnas del script 002 (id, suscripcion_id, valor_cop, referencia, soporte_archivo, estado, verificado_por,
-- verificado_en, creado_en); sus restricciones son PRIMARY, CK_Pago_Valor, CK_Pago_Estado, pagos_ibfk_1 y pagos_ibfk_2;
-- AspNetUsers.Id es VARCHAR(255) utf8mb4_general_ci; lower_case_table_names = 1. configuracion_sistema existe con
-- (clave, valor_json, actualizado_por, actualizado_en) y 0 filas.
-- =====================================================================

-- ---------------------------------------------------------------------
-- VERIFICACIÓN ANTES (cliente mysql; anotar el resultado)
-- Esperado: ninguna de las columnas nuevas existe; CK_Pago_Estado existe con 3 valores; estado_fuera_de_lista = 0.
-- Anotar los conteos para compararlos después (CA-P7-56).
-- ---------------------------------------------------------------------
SELECT COLUMN_NAME FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'pagos'
  AND COLUMN_NAME IN ('fecha_pago', 'medio', 'concepto', 'anulado_por', 'anulado_en', 'motivo_anulacion');  -- esperado: ninguna
SELECT CONSTRAINT_NAME, CHECK_CLAUSE FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'pagos' ORDER BY CONSTRAINT_NAME;
SELECT (SELECT COUNT(*) FROM pagos) AS pagos, (SELECT COUNT(*) FROM suscripciones) AS suscripciones,
       (SELECT COUNT(*) FROM planes) AS planes, (SELECT COUNT(*) FROM ventas_vehiculo) AS ventas,
       (SELECT COUNT(*) FROM auditoria_eventos) AS auditoria, (SELECT COUNT(*) FROM configuracion_sistema) AS parametros;
SELECT COUNT(*) AS estado_fuera_de_lista FROM pagos WHERE estado NOT IN ('PENDIENTE','APROBADO','RECHAZADO');  -- esperado 0

-- =====================================================================
-- SUBIR (idéntica a 016_p7_pagos_SUBIR.sql)
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

-- ---------------------------------------------------------------------
-- VERIFICACIÓN DESPUÉS DE SUBIR
-- Esperado: los seis conteos iguales a los de antes; las 6 columnas nuevas presentes y NULL; CK_Pago_Estado con 5
-- valores; CK_Pago_Medio, CK_Pago_Concepto, CK_Pago_Registro y CK_Pago_Anulacion presentes; FK_Pago_AnuladoPor con
-- DELETE_RULE SET NULL.
-- ---------------------------------------------------------------------
SELECT (SELECT COUNT(*) FROM pagos) AS pagos, (SELECT COUNT(*) FROM suscripciones) AS suscripciones,
       (SELECT COUNT(*) FROM planes) AS planes, (SELECT COUNT(*) FROM ventas_vehiculo) AS ventas,
       (SELECT COUNT(*) FROM auditoria_eventos) AS auditoria, (SELECT COUNT(*) FROM configuracion_sistema) AS parametros;
SELECT COLUMN_NAME, IS_NULLABLE, COLUMN_TYPE FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'pagos' ORDER BY ORDINAL_POSITION;
SELECT CONSTRAINT_NAME, CHECK_CLAUSE FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'pagos' ORDER BY CONSTRAINT_NAME;
SELECT CONSTRAINT_NAME, REFERENCED_TABLE_NAME, DELETE_RULE FROM information_schema.REFERENTIAL_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'pagos' ORDER BY CONSTRAINT_NAME;

-- =====================================================================
-- BAJAR (rollback, comentada). Detener antes la API de P7. Deshace solo lo que hace esta migración.
-- Paso 1: comprobación. Si da un resultado mayor que 0, NO bajar sin decidir antes con la responsable: hay pagos
-- registrados por P7 cuya fecha, medio y anulación se perderían y cuyo estado no cabe en el CHECK del script 002.
-- Exportarlos antes con aprobación o restaurar el respaldo.
-- =====================================================================
/*
SELECT COUNT(*) AS pagos_p7 FROM pagos WHERE estado IN ('REGISTRADO','ANULADO') OR fecha_pago IS NOT NULL
   OR medio IS NOT NULL OR concepto IS NOT NULL OR anulado_en IS NOT NULL;

-- Paso 2 (solo si la comprobación da 0):
ALTER TABLE pagos
  DROP CONSTRAINT CK_Pago_Anulacion,
  DROP CONSTRAINT CK_Pago_Registro,
  DROP CONSTRAINT CK_Pago_Concepto,
  DROP CONSTRAINT CK_Pago_Medio,
  DROP CONSTRAINT CK_Pago_Estado;
ALTER TABLE pagos
  ADD CONSTRAINT CK_Pago_Estado CHECK (estado IN ('PENDIENTE','APROBADO','RECHAZADO'));
ALTER TABLE pagos DROP FOREIGN KEY FK_Pago_AnuladoPor;
ALTER TABLE pagos
  DROP KEY FK_Pago_AnuladoPor,
  DROP COLUMN motivo_anulacion,
  DROP COLUMN anulado_en,
  DROP COLUMN anulado_por,
  DROP COLUMN concepto,
  DROP COLUMN medio,
  DROP COLUMN fecha_pago;

-- VERIFICACIÓN DESPUÉS DE BAJAR: pagos con las 9 columnas del script 002; CK_Pago_Valor y CK_Pago_Estado (3 valores);
-- conteos iguales a los de antes.
SELECT COLUMN_NAME FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'pagos' ORDER BY ORDINAL_POSITION;
SELECT CONSTRAINT_NAME, CHECK_CLAUSE FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'pagos';
SELECT (SELECT COUNT(*) FROM pagos) AS pagos, (SELECT COUNT(*) FROM suscripciones) AS suscripciones,
       (SELECT COUNT(*) FROM planes) AS planes, (SELECT COUNT(*) FROM ventas_vehiculo) AS ventas,
       (SELECT COUNT(*) FROM auditoria_eventos) AS auditoria, (SELECT COUNT(*) FROM configuracion_sistema) AS parametros;
*/
