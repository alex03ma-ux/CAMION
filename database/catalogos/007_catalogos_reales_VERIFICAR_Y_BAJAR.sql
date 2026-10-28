-- =====================================================================
-- 007 — Catálogos de datos reales (D-29): VERIFICACIÓN y BAJAR.
-- Lo ejecuta el coordinador con el cliente mysql, NO se importa en phpMyAdmin (lección de la 004).
-- La parte que se importa es 007_catalogos_reales_SUBIR.sql.
-- =====================================================================
USE camion_ventas;

-- ---------------------------------------------------------------------
-- VERIFICACIÓN ANTES y DESPUÉS (ejecutar las dos veces y comparar)
-- Esperado después:
--   combustibles >= 5;
--   departamentos = 33 (32 + Bogotá, D.C.) si antes solo estaba Antioquia;
--   municipios = 1122;
--   vehiculos sin cambios.
-- ---------------------------------------------------------------------
SELECT (SELECT COUNT(*) FROM combustibles) AS combustibles,
       (SELECT COUNT(*) FROM departamentos) AS departamentos,
       (SELECT COUNT(*) FROM municipios) AS municipios,
       (SELECT COUNT(*) FROM vehiculos) AS vehiculos;

-- Comprobaciones puntuales (códigos DANE oficiales)
SELECT codigo, nombre FROM municipios
WHERE codigo IN ('05001', '08001', '11001', '13001', '76001', '68001', '88001', '99001')
ORDER BY codigo;

-- Ningún municipio queda sin departamento (debe dar 0)
SELECT COUNT(*) AS municipios_huerfanos
FROM municipios m LEFT JOIN departamentos d ON d.id = m.departamento_id
WHERE d.id IS NULL;

-- =====================================================================
-- BAJAR (comentada). Quita SOLO lo que añadió el 007 y que ningún vehículo usa.
-- Conserva lo que ya existía por el DEMO de P3: el departamento 05 (Antioquia),
-- los municipios 05001 (Medellín) y 05266 (Envigado) y el combustible Diésel.
-- Para usarla, quita los "-- " del bloque.
-- =====================================================================
-- START TRANSACTION;
-- DELETE FROM municipios
--  WHERE codigo NOT IN ('05001', '05266')
--    AND NOT EXISTS (SELECT 1 FROM vehiculos v WHERE v.municipio_id = municipios.id);
-- DELETE FROM departamentos
--  WHERE codigo <> '05'
--    AND NOT EXISTS (SELECT 1 FROM municipios m WHERE m.departamento_id = departamentos.id);
-- DELETE FROM combustibles
--  WHERE nombre IN ('Gasolina', 'Gas (GNV)', 'Eléctrico', 'Híbrido')
--    AND NOT EXISTS (SELECT 1 FROM vehiculos v WHERE v.combustible_id = combustibles.id);
-- COMMIT;
-- Después de BAJAR: repetir la VERIFICACIÓN; vehiculos debe ser igual.
