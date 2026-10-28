-- =====================================================================
-- 010 — Clasificación de modelos (D-32): VERIFICACIÓN y BAJAR. Cliente mysql del coordinador.
-- =====================================================================
USE camion_ventas;

-- Esperado después de SUBIR: 393 filas en lineas_tipos (si las 393 líneas del 2026-10-28 siguen iguales).
SELECT COUNT(*) AS filas_lineas_tipos FROM lineas_tipos;
SELECT t.nombre AS categoria, COUNT(*) AS modelos
FROM lineas_tipos lt JOIN tipos_vehiculo t ON t.id = lt.tipo_vehiculo_id GROUP BY t.nombre;
-- Modelos sin categoría. Esperado: 0; si aparece alguno es nuevo y se clasifica en P4p.
SELECT m.nombre AS marca, l.nombre AS linea FROM lineas l JOIN marcas m ON m.id = l.marca_id
WHERE NOT EXISTS (SELECT 1 FROM lineas_tipos lt WHERE lt.linea_id = l.id);
-- Vehículos cuyo modelo no corresponde a su categoría (se corrigen editándolos).
SELECT v.id, t.nombre AS categoria, m.nombre AS marca, l.nombre AS linea
FROM vehiculos v JOIN tipos_vehiculo t ON t.id = v.tipo_vehiculo_id
JOIN lineas l ON l.id = v.linea_id JOIN marcas m ON m.id = l.marca_id
WHERE EXISTS (SELECT 1 FROM lineas_tipos lt WHERE lt.linea_id = l.id)
  AND NOT EXISTS (SELECT 1 FROM lineas_tipos lt WHERE lt.linea_id = l.id AND lt.tipo_vehiculo_id = v.tipo_vehiculo_id);

-- =====================================================================
-- BAJAR: la forma segura de deshacer la clasificación es la sección BAJAR de la migración 009, que elimina la tabla.
-- Para conservar la tabla vacía y reclasificar, el coordinador puede ejecutar:
--   DELETE FROM lineas_tipos;
-- Solo afecta a esta tabla nueva, que no guarda otros datos.
-- =====================================================================
