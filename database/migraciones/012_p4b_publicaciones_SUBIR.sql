-- =====================================================================
-- Migración 012 — Publicaciones, destacados por plan y venta (P4b, HU-023 a HU-028, HU-064, D-22, D-38 a D-41).
-- SOLO SUBIR, para phpMyAdmin (D-16). Sin consultas a information_schema.
-- Requisitos: respaldo previo del coordinador en C:\Respaldo_CAMION\ y API detenida.
-- Verificación y BAJAR: 012_p4b_publicaciones.sql (cliente mysql del coordinador).
-- No toca datos existentes. Sin DROP TABLE, TRUNCATE ni DELETE.
-- =====================================================================

-- 1. D-38: destacados incluidos en cada plan (0 a 20). Los planes existentes quedan con 0.
ALTER TABLE planes
  ADD COLUMN limite_destacados INT NOT NULL DEFAULT 0 AFTER limite_publicaciones;

ALTER TABLE planes
  ADD CONSTRAINT CK_Plan_Destacados CHECK (limite_destacados BETWEEN 0 AND 20);

-- 2. HU-025: el motivo de inactivar admite de 5 a 500 caracteres (antes VARCHAR(40)). Solo amplía.
ALTER TABLE publicaciones
  MODIFY COLUMN motivo_ocultamiento VARCHAR(500) NULL;

-- 3. HU-064, D-22, D-39: registro de la venta y su reversión.
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

-- publicaciones_destacadas.estado no tiene CHECK en el script 002 (VARCHAR(20) libre): admite FINALIZADA sin cambios.
