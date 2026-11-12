-- =====================================================================
-- Migración 009 — Modelos (líneas) por categoría (P4a, D-32).
-- Tabla nueva lineas_tipos (muchos a muchos): un modelo puede pertenecer a una o varias categorías
-- (Carro, Camión, Carrocería, Maquinaria). La API solo acepta una línea que tenga la categoría del
-- vehículo; una línea sin ninguna categoría se acepta, para no bloquear nada mientras no esté clasificada.
-- El administrador podrá editarlo en la pantalla de marcas y modelos (P4p).
-- Orden: respaldo -> detener la API -> importar 009_..._SUBIR.sql -> importar
-- database/catalogos/010_clasificacion_lineas_SUBIR.sql -> verificar -> arrancar la API con el código de D-32.
-- Sin DROP de tablas existentes, sin TRUNCATE ni DELETE.
-- =====================================================================
USE camion_ventas;

-- VERIFICACIÓN ANTES (cliente mysql): la tabla no existe todavía (0).
SELECT COUNT(*) AS existe_lineas_tipos FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'lineas_tipos';
SELECT (SELECT COUNT(*) FROM lineas) AS lineas, (SELECT COUNT(*) FROM vehiculos) AS vehiculos;

-- SUBIR (idéntica a 009_p4a_lineas_tipos_SUBIR.sql)
CREATE TABLE lineas_tipos (
 linea_id BIGINT NOT NULL,
 tipo_vehiculo_id BIGINT NOT NULL,
 PRIMARY KEY (linea_id, tipo_vehiculo_id),
 KEY IX_LineaTipo_Tipo (tipo_vehiculo_id),
 CONSTRAINT FK_LineaTipo_Linea FOREIGN KEY (linea_id) REFERENCES lineas(id) ON DELETE CASCADE,
 CONSTRAINT FK_LineaTipo_Tipo FOREIGN KEY (tipo_vehiculo_id) REFERENCES tipos_vehiculo(id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- VERIFICACIÓN DESPUÉS: la tabla existe (1); lineas y vehiculos sin cambios.
SELECT COUNT(*) AS existe_lineas_tipos FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'lineas_tipos';
SELECT (SELECT COUNT(*) FROM lineas) AS lineas, (SELECT COUNT(*) FROM vehiculos) AS vehiculos;

-- =====================================================================
-- BAJAR (comentada). Detener antes la API con el código de D-32 (la usa).
-- Solo elimina la tabla que creó esta migración (y con ella la clasificación del 010).
-- =====================================================================
-- DROP TABLE lineas_tipos;
