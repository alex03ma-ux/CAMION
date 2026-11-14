-- =====================================================================
-- 017 — Categorías base (D-17): VERIFICACIÓN. Solo lectura. Cliente mysql, con la base del portal seleccionada.
-- =====================================================================
-- Esperado: 4 filas (Camión, Carro, Carrocería, Maquinaria), todas con activo = 1.
SELECT id, nombre, activo FROM tipos_vehiculo ORDER BY nombre;

-- =====================================================================
-- BAJAR: no aplica. Son las categorías fijas de D-17 y las referencian vehiculos y lineas_tipos; no se borran.
-- El panel de catálogos (P4p) no las administra (D-17 las deja fijas); para deshacer, restaurar el respaldo previo.
-- =====================================================================
