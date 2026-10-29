-- =====================================================================
-- Migración 011 — Estados de la suscripción (P4p, D-4, HU-068, HU-069, RN-SU-003, RN-SU-004).
-- Base: camion_ventas (MariaDB 10.4). Tabla afectada: suscripciones (solo la restricción CK_Sus_Estado).
--
-- Añade SUSPENDIDA y REACTIVADA a los estados admitidos:
--   antes (002): PENDIENTE, ACTIVA, VENCIDA, CANCELADA, RECHAZADA
--   después    : PENDIENTE, ACTIVA, SUSPENDIDA, REACTIVADA, VENCIDA, CANCELADA, RECHAZADA
-- No toca datos. Sin DROP TABLE, TRUNCATE ni DELETE.
--
-- Cómo aplicarla (D-16):
--   1. Respaldo completo nuevo en C:\Respaldo_CAMION\ (coordinador). No sobrescribir respaldos existentes.
--   2. Detener la API (entre el DROP y el ADD la tabla queda sin la CHECK un instante).
--   3. En phpMyAdmin (Chrome/Edge) importar SOLO 011_p4p_estados_suscripcion_SUBIR.sql.
--   4. El coordinador ejecuta la VERIFICACIÓN de este archivo con el cliente mysql.
-- Nota: en MariaDB cada ALTER TABLE hace commit implícito; el script no es atómico.
-- =====================================================================
USE camion_ventas;

-- ---------------------------------------------------------------------
-- VERIFICACIÓN ANTES (cliente mysql; anotar el resultado)
-- Esperado: la CHECK actual con los 5 estados del script 002.
-- ---------------------------------------------------------------------
SELECT estado, COUNT(*) AS total FROM suscripciones GROUP BY estado ORDER BY estado;

SELECT CONSTRAINT_NAME, CHECK_CLAUSE
FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'suscripciones';

-- =====================================================================
-- SUBIR (idéntica a 011_p4p_estados_suscripcion_SUBIR.sql)
-- =====================================================================
ALTER TABLE suscripciones DROP CONSTRAINT CK_Sus_Estado;

ALTER TABLE suscripciones
  ADD CONSTRAINT CK_Sus_Estado
  CHECK (estado IN ('PENDIENTE','ACTIVA','SUSPENDIDA','REACTIVADA','VENCIDA','CANCELADA','RECHAZADA'));

-- ---------------------------------------------------------------------
-- VERIFICACIÓN DESPUÉS DE SUBIR
-- Esperado: mismos totales por estado que antes; CK_Sus_Estado con los 7 estados; CK_Sus_Fechas sin cambios.
-- ---------------------------------------------------------------------
SELECT estado, COUNT(*) AS total FROM suscripciones GROUP BY estado ORDER BY estado;

SELECT CONSTRAINT_NAME, CHECK_CLAUSE
FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'suscripciones';

-- =====================================================================
-- BAJAR (rollback, comentada). Detener antes la API de P4p: escribe SUSPENDIDA y REACTIVADA.
-- Paso 1: comprobar que no haya filas con los estados nuevos. Si el resultado es mayor que 0, NO bajar:
--   la restricción original las rechazaría y el ADD CONSTRAINT fallaría. Primero hay que decidir
--   (con la responsable) a qué estado pasan esas suscripciones, o restaurar el respaldo.
-- =====================================================================
/*
SELECT COUNT(*) AS con_estados_p4p
FROM suscripciones
WHERE estado IN ('SUSPENDIDA','REACTIVADA');

-- Paso 2 (solo si el resultado anterior es 0):
ALTER TABLE suscripciones DROP CONSTRAINT CK_Sus_Estado;

ALTER TABLE suscripciones
  ADD CONSTRAINT CK_Sus_Estado
  CHECK (estado IN ('PENDIENTE','ACTIVA','VENCIDA','CANCELADA','RECHAZADA'));

-- VERIFICACIÓN DESPUÉS DE BAJAR: CK_Sus_Estado con los 5 estados del script 002 y los mismos totales.
SELECT estado, COUNT(*) AS total FROM suscripciones GROUP BY estado ORDER BY estado;

SELECT CONSTRAINT_NAME, CHECK_CLAUSE
FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'suscripciones';
*/
