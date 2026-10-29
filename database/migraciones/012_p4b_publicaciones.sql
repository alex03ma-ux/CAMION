-- =====================================================================
-- Migración 012 — Publicaciones, destacados por plan y venta (P4b, HU-023 a HU-028, HU-064, D-22, D-38 a D-41).
-- Base: camion_ventas (MariaDB 10.4).
--
-- Cambios:
--   1. planes.limite_destacados INT NOT NULL DEFAULT 0, con CK_Plan_Destacados (0 a 20).            (D-38)
--   2. publicaciones.motivo_ocultamiento pasa de VARCHAR(40) a VARCHAR(500): el motivo de inactivar
--      tiene de 5 a 500 caracteres (contrato P4b, HU-025). Solo amplía; no cambia datos.
--   3. Tabla nueva ventas_vehiculo (venta y su reversión).                                       (HU-064, D-22, D-39)
--   publicaciones_destacadas.estado no tiene CHECK en el script 002: FINALIZADA ya se admite, no se toca.
-- No toca datos. Sin DROP TABLE, TRUNCATE ni DELETE.
--
-- Cómo aplicarla (D-16):
--   1. Respaldo completo nuevo en C:\Respaldo_CAMION\ (coordinador). No sobrescribir respaldos existentes.
--   2. Detener la API (la versión P4b de la API lee planes.limite_destacados y ventas_vehiculo).
--   3. En phpMyAdmin (Chrome/Edge) importar SOLO 012_p4b_publicaciones_SUBIR.sql.
--   4. El coordinador ejecuta la VERIFICACIÓN de este archivo con el cliente mysql.
-- Nota: en MariaDB cada ALTER/CREATE TABLE hace commit implícito; el script no es atómico.
-- =====================================================================
USE camion_ventas;

-- ---------------------------------------------------------------------
-- VERIFICACIÓN ANTES (cliente mysql; anotar el resultado)
-- Esperado: planes sin limite_destacados; motivo_ocultamiento VARCHAR(40); ventas_vehiculo no existe;
-- publicaciones_destacadas sin CHECK sobre estado.
-- ---------------------------------------------------------------------
SELECT COUNT(*) AS planes FROM planes;
SELECT estado_publicacion, estado_comercial, COUNT(*) AS total FROM publicaciones
GROUP BY estado_publicacion, estado_comercial ORDER BY 1, 2;
SELECT estado, COUNT(*) AS total FROM publicaciones_destacadas GROUP BY estado ORDER BY estado;
SELECT MAX(CHAR_LENGTH(motivo_ocultamiento)) AS motivo_mas_largo FROM publicaciones;

SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'camion_ventas'
  AND ((TABLE_NAME = 'planes' AND COLUMN_NAME = 'limite_destacados')
    OR (TABLE_NAME = 'publicaciones' AND COLUMN_NAME = 'motivo_ocultamiento'));

SELECT TABLE_NAME FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'ventas_vehiculo';

SELECT TABLE_NAME, CONSTRAINT_NAME, CHECK_CLAUSE
FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME IN ('planes', 'publicaciones_destacadas');

-- =====================================================================
-- SUBIR (idéntica a 012_p4b_publicaciones_SUBIR.sql)
-- =====================================================================
ALTER TABLE planes
  ADD COLUMN limite_destacados INT NOT NULL DEFAULT 0 AFTER limite_publicaciones;

ALTER TABLE planes
  ADD CONSTRAINT CK_Plan_Destacados CHECK (limite_destacados BETWEEN 0 AND 20);

ALTER TABLE publicaciones
  MODIFY COLUMN motivo_ocultamiento VARCHAR(500) NULL;

CREATE TABLE ventas_vehiculo (
  id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  vehiculo_id BIGINT NOT NULL,
  publicacion_id BIGINT NULL,
  fecha_venta DATE NOT NULL,
  precio_final_cop DECIMAL(15,2) NOT NULL,
  origen VARCHAR(20) NOT NULL,
  observaciones VARCHAR(1000) NULL,
  registrada_por VARCHAR(255) NULL,
  registrada_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  revertida_por VARCHAR(255) NULL,
  revertida_en DATETIME(6) NULL,
  motivo_reversion VARCHAR(500) NULL,
  KEY IX_Venta_Vehiculo (vehiculo_id, registrada_en),
  KEY IX_Venta_Publicacion (publicacion_id),
  CONSTRAINT FK_Venta_Vehiculo FOREIGN KEY (vehiculo_id) REFERENCES vehiculos(id) ON DELETE RESTRICT,
  CONSTRAINT FK_Venta_Publicacion FOREIGN KEY (publicacion_id) REFERENCES publicaciones(id) ON DELETE RESTRICT,
  CONSTRAINT FK_Venta_RegistradaPor FOREIGN KEY (registrada_por) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,
  CONSTRAINT FK_Venta_RevertidaPor FOREIGN KEY (revertida_por) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,
  CONSTRAINT CK_Venta_Precio CHECK (precio_final_cop > 0),
  CONSTRAINT CK_Venta_Origen CHECK (origen IN ('PORTAL','CONTACTO_ASESOR','REFERIDO','OTRO'))
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- VERIFICACIÓN DESPUÉS DE SUBIR
-- Esperado: mismos totales que antes; todos los planes con limite_destacados = 0; motivo_ocultamiento VARCHAR(500);
-- ventas_vehiculo existe y está vacía; CK_Plan_Destacados presente; CK_Plan_Valores sin cambios.
-- ---------------------------------------------------------------------
SELECT COUNT(*) AS planes, SUM(limite_destacados) AS destacados_total FROM planes;
SELECT estado_publicacion, estado_comercial, COUNT(*) AS total FROM publicaciones
GROUP BY estado_publicacion, estado_comercial ORDER BY 1, 2;
SELECT COUNT(*) AS ventas FROM ventas_vehiculo;

SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'camion_ventas'
  AND ((TABLE_NAME = 'planes' AND COLUMN_NAME = 'limite_destacados')
    OR (TABLE_NAME = 'publicaciones' AND COLUMN_NAME = 'motivo_ocultamiento'));

SELECT CONSTRAINT_NAME, CHECK_CLAUSE
FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME IN ('planes', 'ventas_vehiculo');

SELECT CONSTRAINT_NAME, REFERENCED_TABLE_NAME
FROM information_schema.REFERENTIAL_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'ventas_vehiculo';

-- =====================================================================
-- BAJAR (rollback, comentada). Detener antes la API de P4b. Elimina solo lo añadido por esta migración.
-- Paso 1: comprobaciones. Si alguna da un resultado mayor que 0, NO bajar sin decidir antes con la responsable:
--   - ventas registradas: se perderían al quitar la tabla (exportarlas antes o restaurar el respaldo);
--   - motivos de más de 40 caracteres: no caben en VARCHAR(40) y el MODIFY fallaría (modo estricto).
-- =====================================================================
/*
SELECT COUNT(*) AS ventas FROM ventas_vehiculo;
SELECT COUNT(*) AS motivos_largos FROM publicaciones WHERE CHAR_LENGTH(motivo_ocultamiento) > 40;
SELECT COUNT(*) AS planes_con_destacados FROM planes WHERE limite_destacados > 0;

-- Paso 2 (solo si las dos primeras comprobaciones dan 0; la tercera solo informa de lo que se pierde):
DROP TABLE ventas_vehiculo;

ALTER TABLE publicaciones
  MODIFY COLUMN motivo_ocultamiento VARCHAR(40) NULL;

ALTER TABLE planes DROP CONSTRAINT CK_Plan_Destacados;

ALTER TABLE planes DROP COLUMN limite_destacados;

-- VERIFICACIÓN DESPUÉS DE BAJAR: planes sin limite_destacados, motivo_ocultamiento VARCHAR(40), sin ventas_vehiculo.
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'camion_ventas'
  AND ((TABLE_NAME = 'planes' AND COLUMN_NAME = 'limite_destacados')
    OR (TABLE_NAME = 'publicaciones' AND COLUMN_NAME = 'motivo_ocultamiento'));

SELECT TABLE_NAME FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'ventas_vehiculo';
*/
