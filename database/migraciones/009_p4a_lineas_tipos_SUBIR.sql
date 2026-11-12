-- =====================================================================
-- Migración 009 — Modelos (líneas) por categoría (D-32). SOLO SUBIR, para phpMyAdmin (D-16).
-- Requisitos: API detenida y respaldo previo del coordinador. Solo crea una tabla nueva; no toca datos.
-- Después se importa database/catalogos/010_clasificacion_lineas_SUBIR.sql.
-- Verificación y BAJAR: 009_p4a_lineas_tipos.sql (cliente mysql del coordinador).
-- =====================================================================
CREATE TABLE lineas_tipos (
 linea_id BIGINT NOT NULL,
 tipo_vehiculo_id BIGINT NOT NULL,
 PRIMARY KEY (linea_id, tipo_vehiculo_id),
 KEY IX_LineaTipo_Tipo (tipo_vehiculo_id),
 CONSTRAINT FK_LineaTipo_Linea FOREIGN KEY (linea_id) REFERENCES lineas(id) ON DELETE CASCADE,
 CONSTRAINT FK_LineaTipo_Tipo FOREIGN KEY (tipo_vehiculo_id) REFERENCES tipos_vehiculo(id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
