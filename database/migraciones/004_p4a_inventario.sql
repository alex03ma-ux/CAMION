-- =====================================================================
-- Migración 004 — Inventario de vehículos (P4a, HU-013 a HU-022)
-- Base: camion_ventas (MariaDB 10.4, XAMPP). Contrato: docs/criterios/P4a-contrato-api.md
-- Decisiones: D-2 (placa), D-4/D-11 (estados), D-19 (carrocería y maquinaria),
-- D-20 (fotos del vehículo), D-23 (aplicación de esta migración).
--
-- NO APLICAR sin aprobación de la responsable (D-23).
-- Sin DROP TABLE de tablas existentes, sin TRUNCATE y sin DELETE.
--
-- Cambios:
--   * vehiculos: placa (única, opcional), estado, precio_cop, precio_anterior_cop,
--     largo_m / ancho_m / alto_m, material_id, horas_uso, tipo_maquina_id,
--     motivo_inactivacion; marca_id y modelo_anio pasan a NULL.
--   * Tablas nuevas: materiales_carroceria, tipos_maquina, fotos_vehiculo.
--   * Relleno de vehiculos.precio_cop desde la publicación más reciente del vehículo.
--   * Valores iniciales PROPUESTOS de materiales, tipos de máquina y carrocerías
--     (INSERT ... WHERE NOT EXISTS; no duplica los que ya existan).
--
-- ---------------------------------------------------------------------
-- 0. RESPALDO PREVIO (obligatorio; ejecutar en una consola, NO desde este script)
--    Respaldo COMPLETO de la base. Sustituye <usuario> por el usuario de MariaDB;
--    la contraseña la pide -p de forma interactiva (no la escribas en el comando
--    ni en este archivo). Cambia AAAAMMDD_HHMM por la fecha y hora actuales.
--    No sobrescribas un respaldo existente: si el nombre ya existe, usa otra hora.
--
--    C:\xampp\mysql\bin\mysqldump.exe -u <usuario> -p --single-transaction --routines --triggers --default-character-set=utf8mb4 --result-file=C:\Respaldo_CAMION\camion_ventas_p4a_inventario_AAAAMMDD_HHMM.sql camion_ventas
--
--    (Se usa --result-file en lugar de ">" porque en PowerShell la redirección
--    guarda el archivo en UTF-16 y no se puede restaurar directamente.)
--
--    Comprueba que el archivo existe y no está vacío antes de continuar.
--
-- 1. Detener la API (Camion.Api) antes de SUBIR o BAJAR.
-- 2. Ejecutar con el cliente mysql SIN --force (o en phpMyAdmin, que se detiene
--    en el primer error), para que se detenga en el primer error.
--
-- Nota: en MariaDB los ALTER TABLE y CREATE TABLE confirman la transacción en
-- curso (commit implícito), así que el script no es atómico. Si un paso falla,
-- revisa el estado con las consultas de VERIFICACIÓN y, si hace falta, ejecuta
-- la sección BAJAR o restaura el respaldo.
-- =====================================================================

USE camion_ventas;
SET NAMES utf8mb4;

-- ---------------------------------------------------------------------
-- VERIFICACIÓN ANTES (anota el resultado)
-- ---------------------------------------------------------------------
SELECT
  (SELECT COUNT(*) FROM vehiculos)         AS vehiculos,
  (SELECT COUNT(*) FROM publicaciones)     AS publicaciones,
  (SELECT COUNT(*) FROM fotos_publicacion) AS fotos_publicacion,
  (SELECT COUNT(*) FROM tipos_carroceria)  AS tipos_carroceria;

-- Esperado: las columnas del script 002 (marca_id y modelo_anio con IS_NULLABLE = NO)
-- y ninguna de las columnas nuevas.
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'vehiculos'
ORDER BY ORDINAL_POSITION;

-- Esperado: solo CK_Veh_Valores.
SELECT CONSTRAINT_NAME, CHECK_CLAUSE
FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'vehiculos';

-- Esperado: 0 filas (las tablas nuevas todavía no existen).
SELECT TABLE_NAME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'camion_ventas'
  AND TABLE_NAME IN ('materiales_carroceria', 'tipos_maquina', 'fotos_vehiculo');

-- Vehículos que recibirán precio (tienen al menos una publicación con precio > 0).
SELECT
  SUM(EXISTS (SELECT 1 FROM publicaciones p WHERE p.vehiculo_id = v.id AND p.precio_cop > 0))     AS recibiran_precio,
  SUM(NOT EXISTS (SELECT 1 FROM publicaciones p WHERE p.vehiculo_id = v.id AND p.precio_cop > 0)) AS quedaran_sin_precio
FROM vehiculos v;

-- =====================================================================
-- SUBIR (aplicar la migración)
-- =====================================================================

-- 1. Catálogos nuevos de D-19 (antes que vehiculos, por las claves foráneas).
CREATE TABLE materiales_carroceria (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 nombre VARCHAR(100) NOT NULL,
 activo TINYINT(1) NOT NULL DEFAULT 1,
 CONSTRAINT UX_Material_Nombre UNIQUE (nombre)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE tipos_maquina (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 nombre VARCHAR(100) NOT NULL,
 activo TINYINT(1) NOT NULL DEFAULT 1,
 CONSTRAINT UX_TipoMaquina_Nombre UNIQUE (nombre)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. vehiculos: marca y año opcionales (D-19); columnas nuevas con restricciones con nombre.
--    CK_Veh_Valores se mantiene: un NULL la cumple.
ALTER TABLE vehiculos
  MODIFY COLUMN marca_id BIGINT NULL,
  MODIFY COLUMN modelo_anio SMALLINT NULL,
  ADD COLUMN placa VARCHAR(10) NULL AFTER asesor_id,
  ADD COLUMN estado VARCHAR(20) NOT NULL DEFAULT 'ACTIVO' AFTER placa,
  ADD COLUMN precio_cop DECIMAL(15,2) NULL AFTER estado,
  ADD COLUMN precio_anterior_cop DECIMAL(15,2) NULL AFTER precio_cop,
  ADD COLUMN largo_m DECIMAL(6,2) NULL AFTER cilindraje_cc,
  ADD COLUMN ancho_m DECIMAL(6,2) NULL AFTER largo_m,
  ADD COLUMN alto_m DECIMAL(6,2) NULL AFTER ancho_m,
  ADD COLUMN material_id BIGINT NULL AFTER alto_m,
  ADD COLUMN horas_uso INT NULL AFTER material_id,
  ADD COLUMN tipo_maquina_id BIGINT NULL AFTER horas_uso,
  ADD COLUMN motivo_inactivacion VARCHAR(500) NULL AFTER descripcion_tecnica,
  ADD CONSTRAINT UX_Veh_Placa UNIQUE (placa),
  ADD INDEX IX_Veh_AsesorEstado (asesor_id, estado),
  ADD CONSTRAINT CK_Veh_Estado CHECK (estado IN ('ACTIVO','RESERVADO','INACTIVO','VENDIDO')),
  ADD CONSTRAINT CK_Veh_Precio CHECK (precio_cop IS NULL OR precio_cop > 0),
  ADD CONSTRAINT CK_Veh_Medidas CHECK ((largo_m IS NULL OR largo_m > 0) AND (ancho_m IS NULL OR ancho_m > 0) AND (alto_m IS NULL OR alto_m > 0)),
  ADD CONSTRAINT CK_Veh_HorasUso CHECK (horas_uso IS NULL OR horas_uso >= 0),
  ADD CONSTRAINT FK_Veh_Material FOREIGN KEY (material_id) REFERENCES materiales_carroceria(id) ON DELETE RESTRICT,
  ADD CONSTRAINT FK_Veh_TipoMaquina FOREIGN KEY (tipo_maquina_id) REFERENCES tipos_maquina(id) ON DELETE RESTRICT;

-- 3. Fotos del vehículo (D-20). fotos_publicacion no se toca.
CREATE TABLE fotos_vehiculo (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 vehiculo_id BIGINT NOT NULL,
 archivo_clave VARCHAR(500) NOT NULL,
 orden TINYINT UNSIGNED NOT NULL,
 es_portada TINYINT(1) NOT NULL DEFAULT 0,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 CONSTRAINT UX_FotoVeh_Orden UNIQUE (vehiculo_id, orden),
 CONSTRAINT FK_FotoVeh_Vehiculo FOREIGN KEY (vehiculo_id) REFERENCES vehiculos(id) ON DELETE RESTRICT,
 CONSTRAINT CK_FotoVeh_Orden CHECK (orden BETWEEN 1 AND 10)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 4. Datos: precio desde la publicación más reciente con precio > 0 y valores iniciales PROPUESTOS.
--    actualizado_en = actualizado_en evita que ON UPDATE cambie la fecha de los vehículos existentes.
START TRANSACTION;

UPDATE vehiculos v
SET v.precio_cop = (SELECT p.precio_cop
                      FROM publicaciones p
                     WHERE p.vehiculo_id = v.id AND p.precio_cop > 0
                     ORDER BY p.creado_en DESC, p.id DESC
                     LIMIT 1),
    v.actualizado_en = v.actualizado_en
WHERE v.precio_cop IS NULL
  AND EXISTS (SELECT 1 FROM publicaciones p2 WHERE p2.vehiculo_id = v.id AND p2.precio_cop > 0);

INSERT INTO materiales_carroceria (nombre, activo) SELECT 'Aluminio', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM materiales_carroceria WHERE nombre = 'Aluminio');
INSERT INTO materiales_carroceria (nombre, activo) SELECT 'Acero', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM materiales_carroceria WHERE nombre = 'Acero');
INSERT INTO materiales_carroceria (nombre, activo) SELECT 'Madera', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM materiales_carroceria WHERE nombre = 'Madera');
INSERT INTO materiales_carroceria (nombre, activo) SELECT 'Fibra de vidrio', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM materiales_carroceria WHERE nombre = 'Fibra de vidrio');

INSERT INTO tipos_maquina (nombre, activo) SELECT 'Retroexcavadora', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_maquina WHERE nombre = 'Retroexcavadora');
INSERT INTO tipos_maquina (nombre, activo) SELECT 'Montacargas', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_maquina WHERE nombre = 'Montacargas');
INSERT INTO tipos_maquina (nombre, activo) SELECT 'Minicargador', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_maquina WHERE nombre = 'Minicargador');
INSERT INTO tipos_maquina (nombre, activo) SELECT 'Excavadora', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_maquina WHERE nombre = 'Excavadora');
INSERT INTO tipos_maquina (nombre, activo) SELECT 'Motoniveladora', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_maquina WHERE nombre = 'Motoniveladora');
INSERT INTO tipos_maquina (nombre, activo) SELECT 'Vibrocompactador', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_maquina WHERE nombre = 'Vibrocompactador');

INSERT INTO tipos_carroceria (nombre, activo) SELECT 'Furgón', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_carroceria WHERE nombre = 'Furgón');
INSERT INTO tipos_carroceria (nombre, activo) SELECT 'Estacas', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_carroceria WHERE nombre = 'Estacas');
INSERT INTO tipos_carroceria (nombre, activo) SELECT 'Volco', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_carroceria WHERE nombre = 'Volco');
INSERT INTO tipos_carroceria (nombre, activo) SELECT 'Platón', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_carroceria WHERE nombre = 'Platón');
INSERT INTO tipos_carroceria (nombre, activo) SELECT 'Tanque', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_carroceria WHERE nombre = 'Tanque');
INSERT INTO tipos_carroceria (nombre, activo) SELECT 'Refrigerada', 1 FROM DUAL
  WHERE NOT EXISTS (SELECT 1 FROM tipos_carroceria WHERE nombre = 'Refrigerada');

COMMIT;

-- ---------------------------------------------------------------------
-- VERIFICACIÓN DESPUÉS DE SUBIR
-- Esperado: vehiculos, publicaciones y fotos_publicacion iguales a los de antes;
-- tipos_carroceria igual o mayor (como mucho +6).
-- ---------------------------------------------------------------------
SELECT
  (SELECT COUNT(*) FROM vehiculos)             AS vehiculos,
  (SELECT COUNT(*) FROM publicaciones)         AS publicaciones,
  (SELECT COUNT(*) FROM fotos_publicacion)     AS fotos_publicacion,
  (SELECT COUNT(*) FROM tipos_carroceria)      AS tipos_carroceria,
  (SELECT COUNT(*) FROM materiales_carroceria) AS materiales_carroceria,
  (SELECT COUNT(*) FROM tipos_maquina)         AS tipos_maquina,
  (SELECT COUNT(*) FROM fotos_vehiculo)        AS fotos_vehiculo;

-- Esperado: sin_precio_con_publicacion = 0; sin_precio_sin_publicacion = quedaran_sin_precio de antes.
SELECT
  SUM(v.precio_cop IS NULL AND EXISTS (SELECT 1 FROM publicaciones p WHERE p.vehiculo_id = v.id AND p.precio_cop > 0))     AS sin_precio_con_publicacion,
  SUM(v.precio_cop IS NULL AND NOT EXISTS (SELECT 1 FROM publicaciones p WHERE p.vehiculo_id = v.id AND p.precio_cop > 0)) AS sin_precio_sin_publicacion
FROM vehiculos v;

-- Esperado: todos los vehículos existentes en ACTIVO.
SELECT estado, COUNT(*) AS total FROM vehiculos GROUP BY estado ORDER BY estado;

-- Esperado: marca_id y modelo_anio con IS_NULLABLE = YES y las 11 columnas nuevas.
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'vehiculos'
ORDER BY ORDINAL_POSITION;

-- Esperado: CK_Veh_Valores, CK_Veh_Estado, CK_Veh_Precio, CK_Veh_Medidas, CK_Veh_HorasUso y CK_FotoVeh_Orden.
SELECT TABLE_NAME, CONSTRAINT_NAME, CHECK_CLAUSE
FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME IN ('vehiculos', 'fotos_vehiculo')
ORDER BY TABLE_NAME, CONSTRAINT_NAME;

-- Esperado: UX_Veh_Placa (UNIQUE), FK_Veh_Material, FK_Veh_TipoMaquina, UX_FotoVeh_Orden (UNIQUE), FK_FotoVeh_Vehiculo.
SELECT TABLE_NAME, CONSTRAINT_NAME, CONSTRAINT_TYPE
FROM information_schema.TABLE_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas'
  AND TABLE_NAME IN ('vehiculos', 'fotos_vehiculo', 'materiales_carroceria', 'tipos_maquina')
ORDER BY TABLE_NAME, CONSTRAINT_TYPE, CONSTRAINT_NAME;

-- =====================================================================
-- BAJAR (rollback). Ejecutar SOLO para revertir; dejar comentado al subir.
-- Antes de bajar: respaldo completo nuevo (mismo comando del paso 0, otro nombre)
-- y detener la API de P4a (escribe en las columnas nuevas).
-- Quita las columnas y tablas añadidas y restaura NOT NULL en marca_id y modelo_anio.
-- Se pierden: placa, estado, precios del vehículo, medidas, material, horas de uso,
-- tipo de máquina, motivo de inactivación y las filas de fotos_vehiculo (los archivos
-- de C:\CAMION_media no se borran). El respaldo previo los conserva.
-- No borra filas de tablas existentes: las carrocerías añadidas a tipos_carroceria se quedan.
-- =====================================================================
/*
-- B1. Comprobación: marca_id y modelo_anio solo pueden volver a NOT NULL si no hay NULL.
SELECT COUNT(*) AS vehiculos_con_marca_o_anio_nulo,
       'Si es mayor que 0, BAJAR se detiene en la sentencia siguiente (error 1242). Completa la marca y el año de esos vehículos (o restaura el respaldo) antes de volver a ejecutar BAJAR.' AS aviso
FROM vehiculos
WHERE marca_id IS NULL OR modelo_anio IS NULL;

-- Se detiene con error 1242 (la subconsulta devuelve 2 filas) solo si existe algún NULL;
-- si no hay ninguno, devuelve NULL y sigue.
DO (SELECT 1 FROM vehiculos WHERE marca_id IS NULL OR modelo_anio IS NULL HAVING COUNT(*) > 0
    UNION ALL
    SELECT 2 FROM vehiculos WHERE marca_id IS NULL OR modelo_anio IS NULL HAVING COUNT(*) > 0);

-- Segunda protección: en modo estricto, MODIFY ... NOT NULL falla si quedara algún NULL.
SET SESSION sql_mode = CONCAT_WS(',', NULLIF(@@SESSION.sql_mode, ''), 'STRICT_ALL_TABLES');

-- B2. Restaurar NOT NULL (primero, para que si falla no se haya quitado nada todavía).
ALTER TABLE vehiculos
  MODIFY COLUMN marca_id BIGINT NOT NULL,
  MODIFY COLUMN modelo_anio SMALLINT NOT NULL;

-- B3. Tabla nueva de fotos del vehículo.
DROP TABLE fotos_vehiculo;

-- B4. Restricciones y columnas nuevas de vehiculos.
ALTER TABLE vehiculos
  DROP FOREIGN KEY FK_Veh_Material,
  DROP FOREIGN KEY FK_Veh_TipoMaquina,
  DROP CONSTRAINT CK_Veh_Estado,
  DROP CONSTRAINT CK_Veh_Precio,
  DROP CONSTRAINT CK_Veh_Medidas,
  DROP CONSTRAINT CK_Veh_HorasUso;

ALTER TABLE vehiculos
  DROP INDEX UX_Veh_Placa,
  DROP INDEX IX_Veh_AsesorEstado,
  DROP COLUMN placa,
  DROP COLUMN estado,
  DROP COLUMN precio_cop,
  DROP COLUMN precio_anterior_cop,
  DROP COLUMN largo_m,
  DROP COLUMN ancho_m,
  DROP COLUMN alto_m,
  DROP COLUMN material_id,
  DROP COLUMN horas_uso,
  DROP COLUMN tipo_maquina_id,
  DROP COLUMN motivo_inactivacion;

-- B5. Catálogos nuevos (ya sin referencias).
DROP TABLE tipos_maquina;
DROP TABLE materiales_carroceria;

-- VERIFICACIÓN DESPUÉS DE BAJAR
-- Esperado: mismos conteos de vehiculos, publicaciones y fotos_publicacion; columnas y
-- CHECK de vehiculos como en la VERIFICACIÓN ANTES (solo CK_Veh_Valores).
SELECT
  (SELECT COUNT(*) FROM vehiculos)         AS vehiculos,
  (SELECT COUNT(*) FROM publicaciones)     AS publicaciones,
  (SELECT COUNT(*) FROM fotos_publicacion) AS fotos_publicacion;

SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'vehiculos'
ORDER BY ORDINAL_POSITION;

SELECT CONSTRAINT_NAME, CHECK_CLAUSE
FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'vehiculos';
*/
