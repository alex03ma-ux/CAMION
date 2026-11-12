-- =====================================================================
-- Migración 006 — Nota de reserva del vehículo (P4a, D-28).
-- Base: camion_ventas (MariaDB 10.4). Tabla afectada: vehiculos (solo una columna nueva, opcional).
--
-- Al reservar, el asesor dueño o el administrador escribe una nota obligatoria
-- (5 a 500 caracteres, p. ej. "pagó anticipo"). Se guarda en vehiculos.nota_reserva,
-- se ve en el inventario (no en el portal público) y queda en la auditoría.
-- La reserva no vence; la nota se borra al liberar o inactivar.
--
-- Cómo aplicarla (D-16):
--   1. Respaldo completo nuevo en C:\Respaldo_CAMION\ (coordinador).
--   2. Detener la API.
--   3. En phpMyAdmin (Chrome/Edge) importar SOLO 006_p4a_nota_reserva_SUBIR.sql.
--   4. El coordinador ejecuta la VERIFICACIÓN de este archivo con el cliente mysql.
-- Sin DROP TABLE, TRUNCATE ni DELETE.
-- =====================================================================
USE camion_ventas;

-- VERIFICACIÓN ANTES (cliente mysql): debe dar 0 filas para nota_reserva.
SELECT COUNT(*) AS vehiculos FROM vehiculos;
SELECT COLUMN_NAME FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'vehiculos' AND COLUMN_NAME = 'nota_reserva';

-- =====================================================================
-- SUBIR (idéntica a 006_p4a_nota_reserva_SUBIR.sql)
-- =====================================================================
ALTER TABLE vehiculos
  ADD COLUMN nota_reserva VARCHAR(500) NULL AFTER motivo_inactivacion;

-- VERIFICACIÓN DESPUÉS: mismo número de vehículos; la columna existe, es NULL y VARCHAR(500);
-- ningún vehículo tiene nota todavía (los RESERVADO anteriores a la 006 quedan sin nota).
SELECT COUNT(*) AS vehiculos, SUM(nota_reserva IS NOT NULL) AS con_nota FROM vehiculos;
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'vehiculos' AND COLUMN_NAME = 'nota_reserva';

-- =====================================================================
-- BAJAR (comentada). Detener la API antes: el código de D-28 usa la columna.
-- Las notas guardadas se pierden (quedan en auditoria_eventos y en el respaldo).
-- =====================================================================
-- ALTER TABLE vehiculos DROP COLUMN nota_reserva;
