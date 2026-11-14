-- =====================================================================
-- 017 — Categorías base de vehículo (D-17): Carro, Camión, Carrocería y Maquinaria. PROPUESTO (INF-2, D-80 A).
-- Hasta ahora estas cuatro filas solo las creaba database/demo/demo_p3_catalogo.sql. En una base nueva y limpia
-- (producción, D-80 A) el demo NO se importa, así que sin este script tipos_vehiculo quedaría vacía: el inventario no
-- tendría categorías y la clasificación 010 no insertaría nada. La API reconoce las categorías por estos nombres
-- (Inventario, sin distinguir mayúsculas ni tildes).
-- SOLO la parte para importar (D-16): solo INSERT ... WHERE NOT EXISTS (idempotente). En camion_ventas local ya existen:
-- importarlo allí no cambia nada. Sin DROP, TRUNCATE, DELETE ni cambios de esquema.
-- Orden en una base nueva: después del 002 y antes de database/catalogos/010_clasificacion_lineas_SUBIR.sql
-- (ver docs/GUIA-PUBLICACION.md). Verificación: 017_categorias_base_VERIFICAR.sql.
-- =====================================================================
START TRANSACTION;

INSERT INTO tipos_vehiculo (nombre, activo) SELECT 'Carro', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_vehiculo WHERE nombre = 'Carro');
INSERT INTO tipos_vehiculo (nombre, activo) SELECT 'Camión', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_vehiculo WHERE nombre = 'Camión');
INSERT INTO tipos_vehiculo (nombre, activo) SELECT 'Carrocería', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_vehiculo WHERE nombre = 'Carrocería');
INSERT INTO tipos_vehiculo (nombre, activo) SELECT 'Maquinaria', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_vehiculo WHERE nombre = 'Maquinaria');

COMMIT;
