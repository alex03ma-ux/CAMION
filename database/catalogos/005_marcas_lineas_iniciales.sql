-- =====================================================================
-- 005 — Catálogo inicial de marcas y modelos (líneas)
-- P4a, D-25 (modelo obligatorio en Carro, Camión y Maquinaria) y D-27 (C: lista inicial + pantalla en P4p).
-- Base: camion_ventas (MariaDB 10.4). Tablas: marcas, lineas (script 002).
--
-- Contenido: 33 marcas y 137 modelos habituales en Colombia (camiones, carros y maquinaria).
-- Solo INSERT ... WHERE NOT EXISTS: es idempotente (se puede ejecutar dos veces sin duplicar).
-- No borra ni modifica filas existentes. Sin DROP, TRUNCATE ni DELETE (salvo la sección BAJAR, comentada).
-- Las marcas no tienen categoría en el esquema: una misma marca (p. ej. Chevrolet) sirve para carros y camiones.
--
-- Cómo aplicarlo (D-16): después de la migración 004, desde phpMyAdmin en Chrome/Edge,
-- con el respaldo previo del coordinador. Ejecutar VERIFICACIÓN ANTES, luego SUBIR y VERIFICACIÓN DESPUÉS.
-- La responsable puede revisar y quitar o añadir nombres antes de aplicarlo.
-- =====================================================================

USE camion_ventas;

-- ---------------------------------------------------------------------
-- VERIFICACIÓN ANTES (anota el resultado)
-- ---------------------------------------------------------------------
SELECT (SELECT COUNT(*) FROM marcas) AS marcas, (SELECT COUNT(*) FROM lineas) AS lineas,
       (SELECT COUNT(*) FROM vehiculos) AS vehiculos;

-- =====================================================================
-- SUBIR
-- =====================================================================
START TRANSACTION;

-- Marcas

INSERT INTO marcas (nombre, activo) SELECT 'Chevrolet', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Chevrolet');
INSERT INTO marcas (nombre, activo) SELECT 'Hino', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Hino');
INSERT INTO marcas (nombre, activo) SELECT 'Isuzu', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Isuzu');
INSERT INTO marcas (nombre, activo) SELECT 'JAC', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'JAC');
INSERT INTO marcas (nombre, activo) SELECT 'Foton', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Foton');
INSERT INTO marcas (nombre, activo) SELECT 'International', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'International');
INSERT INTO marcas (nombre, activo) SELECT 'Kenworth', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Kenworth');
INSERT INTO marcas (nombre, activo) SELECT 'Freightliner', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Freightliner');
INSERT INTO marcas (nombre, activo) SELECT 'Mercedes-Benz', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Mercedes-Benz');
INSERT INTO marcas (nombre, activo) SELECT 'Volvo', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Volvo');
INSERT INTO marcas (nombre, activo) SELECT 'Scania', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Scania');
INSERT INTO marcas (nombre, activo) SELECT 'Hyundai', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Hyundai');
INSERT INTO marcas (nombre, activo) SELECT 'Fuso', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Fuso');
INSERT INTO marcas (nombre, activo) SELECT 'Volkswagen', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Volkswagen');
INSERT INTO marcas (nombre, activo) SELECT 'Mack', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Mack');
INSERT INTO marcas (nombre, activo) SELECT 'Dongfeng', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Dongfeng');
INSERT INTO marcas (nombre, activo) SELECT 'Renault', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Renault');
INSERT INTO marcas (nombre, activo) SELECT 'Mazda', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Mazda');
INSERT INTO marcas (nombre, activo) SELECT 'Kia', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Kia');
INSERT INTO marcas (nombre, activo) SELECT 'Toyota', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Toyota');
INSERT INTO marcas (nombre, activo) SELECT 'Nissan', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Nissan');
INSERT INTO marcas (nombre, activo) SELECT 'Ford', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Ford');
INSERT INTO marcas (nombre, activo) SELECT 'Suzuki', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Suzuki');
INSERT INTO marcas (nombre, activo) SELECT 'Caterpillar', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Caterpillar');
INSERT INTO marcas (nombre, activo) SELECT 'JCB', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'JCB');
INSERT INTO marcas (nombre, activo) SELECT 'John Deere', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'John Deere');
INSERT INTO marcas (nombre, activo) SELECT 'Komatsu', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Komatsu');
INSERT INTO marcas (nombre, activo) SELECT 'Bobcat', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Bobcat');
INSERT INTO marcas (nombre, activo) SELECT 'Case', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Case');
INSERT INTO marcas (nombre, activo) SELECT 'Doosan', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Doosan');
INSERT INTO marcas (nombre, activo) SELECT 'New Holland', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'New Holland');
INSERT INTO marcas (nombre, activo) SELECT 'Yale', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Yale');
INSERT INTO marcas (nombre, activo) SELECT 'Hyster', 1 FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM marcas WHERE nombre = 'Hyster');

-- Modelos (líneas) por marca
-- Chevrolet
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'NHR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'NHR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'NKR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'NKR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'NPR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'NPR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'NQR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'NQR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FRR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FRR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FTR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FTR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FVR', 1 FROM marcas m WHERE m.nombre = 'Chevrolet'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FVR');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Spark', 1 FROM marcas m WHERE m.nombre = 'Chevrolet'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Spark');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Onix', 1 FROM marcas m WHERE m.nombre = 'Chevrolet'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Onix');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Sail', 1 FROM marcas m WHERE m.nombre = 'Chevrolet'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Sail');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Tracker', 1 FROM marcas m WHERE m.nombre = 'Chevrolet'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Tracker');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Captiva', 1 FROM marcas m WHERE m.nombre = 'Chevrolet'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Captiva');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Colorado', 1 FROM marcas m WHERE m.nombre = 'Chevrolet'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Colorado');
-- Hino
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Dutro', 1 FROM marcas m WHERE m.nombre = 'Hino'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Dutro');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Serie 300', 1 FROM marcas m WHERE m.nombre = 'Hino'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Serie 300');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Serie 500', 1 FROM marcas m WHERE m.nombre = 'Hino'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Serie 500');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FC', 1 FROM marcas m WHERE m.nombre = 'Hino'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FC');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'GH', 1 FROM marcas m WHERE m.nombre = 'Hino'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'GH');
-- Isuzu
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'ELF 100', 1 FROM marcas m WHERE m.nombre = 'Isuzu'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'ELF 100');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'ELF 300', 1 FROM marcas m WHERE m.nombre = 'Isuzu'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'ELF 300');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'ELF 600', 1 FROM marcas m WHERE m.nombre = 'Isuzu'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'ELF 600');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Forward', 1 FROM marcas m WHERE m.nombre = 'Isuzu'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Forward');
-- JAC
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '1040', 1 FROM marcas m WHERE m.nombre = 'JAC'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '1040');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '1063', 1 FROM marcas m WHERE m.nombre = 'JAC'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '1063');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'X200', 1 FROM marcas m WHERE m.nombre = 'JAC'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'X200');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Gallardo', 1 FROM marcas m WHERE m.nombre = 'JAC'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Gallardo');
-- Foton
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Aumark', 1 FROM marcas m WHERE m.nombre = 'Foton'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Aumark');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Auman', 1 FROM marcas m WHERE m.nombre = 'Foton'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Auman');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'BJ1069', 1 FROM marcas m WHERE m.nombre = 'Foton'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'BJ1069');
-- International
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '4300', 1 FROM marcas m WHERE m.nombre = 'International'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '4300');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '4400', 1 FROM marcas m WHERE m.nombre = 'International'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '4400');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '7400', 1 FROM marcas m WHERE m.nombre = 'International'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '7400');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'ProStar', 1 FROM marcas m WHERE m.nombre = 'International'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'ProStar');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'LT', 1 FROM marcas m WHERE m.nombre = 'International'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'LT');
-- Kenworth
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'T370', 1 FROM marcas m WHERE m.nombre = 'Kenworth'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'T370');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'T680', 1 FROM marcas m WHERE m.nombre = 'Kenworth'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'T680');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'T800', 1 FROM marcas m WHERE m.nombre = 'Kenworth'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'T800');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'T880', 1 FROM marcas m WHERE m.nombre = 'Kenworth'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'T880');
-- Freightliner
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'M2 106', 1 FROM marcas m WHERE m.nombre = 'Freightliner'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'M2 106');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Columbia', 1 FROM marcas m WHERE m.nombre = 'Freightliner'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Columbia');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Cascadia', 1 FROM marcas m WHERE m.nombre = 'Freightliner'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Cascadia');
-- Mercedes-Benz
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Accelo', 1 FROM marcas m WHERE m.nombre = 'Mercedes-Benz'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Accelo');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Atego', 1 FROM marcas m WHERE m.nombre = 'Mercedes-Benz'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Atego');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Axor', 1 FROM marcas m WHERE m.nombre = 'Mercedes-Benz'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Axor');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Actros', 1 FROM marcas m WHERE m.nombre = 'Mercedes-Benz'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Actros');
-- Volvo
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'VM', 1 FROM marcas m WHERE m.nombre = 'Volvo'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'VM');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FM', 1 FROM marcas m WHERE m.nombre = 'Volvo'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FM');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FH', 1 FROM marcas m WHERE m.nombre = 'Volvo'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FH');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'EC210', 1 FROM marcas m WHERE m.nombre = 'Volvo'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'EC210');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'L60', 1 FROM marcas m WHERE m.nombre = 'Volvo'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'L60');
-- Scania
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Serie P', 1 FROM marcas m WHERE m.nombre = 'Scania'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Serie P');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Serie G', 1 FROM marcas m WHERE m.nombre = 'Scania'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Serie G');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Serie R', 1 FROM marcas m WHERE m.nombre = 'Scania'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Serie R');
-- Hyundai
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'HD65', 1 FROM marcas m WHERE m.nombre = 'Hyundai'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'HD65');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'HD78', 1 FROM marcas m WHERE m.nombre = 'Hyundai'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'HD78');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Mighty', 1 FROM marcas m WHERE m.nombre = 'Hyundai'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Mighty');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Xcient', 1 FROM marcas m WHERE m.nombre = 'Hyundai'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Xcient');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'i10', 1 FROM marcas m WHERE m.nombre = 'Hyundai'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'i10');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Accent', 1 FROM marcas m WHERE m.nombre = 'Hyundai'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Accent');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Tucson', 1 FROM marcas m WHERE m.nombre = 'Hyundai'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Tucson');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Creta', 1 FROM marcas m WHERE m.nombre = 'Hyundai'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Creta');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'R220', 1 FROM marcas m WHERE m.nombre = 'Hyundai'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'R220');
-- Fuso
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Canter', 1 FROM marcas m WHERE m.nombre = 'Fuso'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Canter');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FI', 1 FROM marcas m WHERE m.nombre = 'Fuso'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FI');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'FJ', 1 FROM marcas m WHERE m.nombre = 'Fuso'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'FJ');
-- Volkswagen
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Delivery', 1 FROM marcas m WHERE m.nombre = 'Volkswagen'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Delivery');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Constellation', 1 FROM marcas m WHERE m.nombre = 'Volkswagen'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Constellation');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Gol', 1 FROM marcas m WHERE m.nombre = 'Volkswagen'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Gol');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Voyage', 1 FROM marcas m WHERE m.nombre = 'Volkswagen'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Voyage');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Tiguan', 1 FROM marcas m WHERE m.nombre = 'Volkswagen'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Tiguan');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Amarok', 1 FROM marcas m WHERE m.nombre = 'Volkswagen'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Amarok');
-- Mack
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Granite', 1 FROM marcas m WHERE m.nombre = 'Mack'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Granite');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Anthem', 1 FROM marcas m WHERE m.nombre = 'Mack'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Anthem');
-- Dongfeng
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Captain', 1 FROM marcas m WHERE m.nombre = 'Dongfeng'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Captain');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Duolika', 1 FROM marcas m WHERE m.nombre = 'Dongfeng'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Duolika');
-- Renault
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Logan', 1 FROM marcas m WHERE m.nombre = 'Renault'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Logan');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Sandero', 1 FROM marcas m WHERE m.nombre = 'Renault'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Sandero');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Stepway', 1 FROM marcas m WHERE m.nombre = 'Renault'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Stepway');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Duster', 1 FROM marcas m WHERE m.nombre = 'Renault'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Duster');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Kwid', 1 FROM marcas m WHERE m.nombre = 'Renault'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Kwid');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Koleos', 1 FROM marcas m WHERE m.nombre = 'Renault'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Koleos');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Oroch', 1 FROM marcas m WHERE m.nombre = 'Renault'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Oroch');
-- Mazda
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '2', 1 FROM marcas m WHERE m.nombre = 'Mazda'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '2');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '3', 1 FROM marcas m WHERE m.nombre = 'Mazda'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '3');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'CX-3', 1 FROM marcas m WHERE m.nombre = 'Mazda'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'CX-3');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'CX-5', 1 FROM marcas m WHERE m.nombre = 'Mazda'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'CX-5');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'BT-50', 1 FROM marcas m WHERE m.nombre = 'Mazda'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'BT-50');
-- Kia
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Picanto', 1 FROM marcas m WHERE m.nombre = 'Kia'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Picanto');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Rio', 1 FROM marcas m WHERE m.nombre = 'Kia'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Rio');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Sportage', 1 FROM marcas m WHERE m.nombre = 'Kia'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Sportage');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Sorento', 1 FROM marcas m WHERE m.nombre = 'Kia'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Sorento');
-- Toyota
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Corolla', 1 FROM marcas m WHERE m.nombre = 'Toyota'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Corolla');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Hilux', 1 FROM marcas m WHERE m.nombre = 'Toyota'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Hilux');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Fortuner', 1 FROM marcas m WHERE m.nombre = 'Toyota'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Fortuner');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Prado', 1 FROM marcas m WHERE m.nombre = 'Toyota'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Prado');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'RAV4', 1 FROM marcas m WHERE m.nombre = 'Toyota'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'RAV4');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '8FGCU25', 1 FROM marcas m WHERE m.nombre = 'Toyota'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '8FGCU25');
-- Nissan
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'March', 1 FROM marcas m WHERE m.nombre = 'Nissan'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'March');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Versa', 1 FROM marcas m WHERE m.nombre = 'Nissan'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Versa');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Kicks', 1 FROM marcas m WHERE m.nombre = 'Nissan'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Kicks');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Frontier', 1 FROM marcas m WHERE m.nombre = 'Nissan'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Frontier');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'X-Trail', 1 FROM marcas m WHERE m.nombre = 'Nissan'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'X-Trail');
-- Ford
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Ranger', 1 FROM marcas m WHERE m.nombre = 'Ford'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Ranger');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Escape', 1 FROM marcas m WHERE m.nombre = 'Ford'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Escape');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Explorer', 1 FROM marcas m WHERE m.nombre = 'Ford'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Explorer');
-- Suzuki
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Swift', 1 FROM marcas m WHERE m.nombre = 'Suzuki'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Swift');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Vitara', 1 FROM marcas m WHERE m.nombre = 'Suzuki'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Vitara');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'Jimny', 1 FROM marcas m WHERE m.nombre = 'Suzuki'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'Jimny');
-- Caterpillar
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '416F', 1 FROM marcas m WHERE m.nombre = 'Caterpillar'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '416F');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '420F', 1 FROM marcas m WHERE m.nombre = 'Caterpillar'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '420F');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '320', 1 FROM marcas m WHERE m.nombre = 'Caterpillar'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '320');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '336', 1 FROM marcas m WHERE m.nombre = 'Caterpillar'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '336');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'D6', 1 FROM marcas m WHERE m.nombre = 'Caterpillar'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'D6');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '140K', 1 FROM marcas m WHERE m.nombre = 'Caterpillar'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '140K');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '262D', 1 FROM marcas m WHERE m.nombre = 'Caterpillar'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '262D');
-- JCB
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '3CX', 1 FROM marcas m WHERE m.nombre = 'JCB'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '3CX');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '4CX', 1 FROM marcas m WHERE m.nombre = 'JCB'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '4CX');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'JS200', 1 FROM marcas m WHERE m.nombre = 'JCB'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'JS200');
-- John Deere
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '310L', 1 FROM marcas m WHERE m.nombre = 'John Deere'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '310L');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '410L', 1 FROM marcas m WHERE m.nombre = 'John Deere'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '410L');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '200G', 1 FROM marcas m WHERE m.nombre = 'John Deere'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '200G');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '6110', 1 FROM marcas m WHERE m.nombre = 'John Deere'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '6110');
-- Komatsu
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'PC200', 1 FROM marcas m WHERE m.nombre = 'Komatsu'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'PC200');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'WB97', 1 FROM marcas m WHERE m.nombre = 'Komatsu'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'WB97');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'D61', 1 FROM marcas m WHERE m.nombre = 'Komatsu'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'D61');
-- Bobcat
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'S450', 1 FROM marcas m WHERE m.nombre = 'Bobcat'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'S450');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'S570', 1 FROM marcas m WHERE m.nombre = 'Bobcat'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'S570');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'E35', 1 FROM marcas m WHERE m.nombre = 'Bobcat'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'E35');
-- Case
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '580N', 1 FROM marcas m WHERE m.nombre = 'Case'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '580N');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, '590SN', 1 FROM marcas m WHERE m.nombre = 'Case'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = '590SN');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'SR175', 1 FROM marcas m WHERE m.nombre = 'Case'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'SR175');
-- Doosan
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DX225', 1 FROM marcas m WHERE m.nombre = 'Doosan'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DX225');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'DX140', 1 FROM marcas m WHERE m.nombre = 'Doosan'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'DX140');
-- New Holland
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'B95', 1 FROM marcas m WHERE m.nombre = 'New Holland'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'B95');
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'TT4030', 1 FROM marcas m WHERE m.nombre = 'New Holland'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'TT4030');
-- Yale
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'GLP050', 1 FROM marcas m WHERE m.nombre = 'Yale'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'GLP050');
-- Hyster
INSERT INTO lineas (marca_id, nombre, activo) SELECT m.id, 'H50FT', 1 FROM marcas m WHERE m.nombre = 'Hyster'
  AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = m.id AND l.nombre = 'H50FT');

COMMIT;

-- ---------------------------------------------------------------------
-- VERIFICACIÓN DESPUÉS
-- Esperado: vehiculos igual que antes; marcas y lineas iguales o mayores
-- (como máximo +33 marcas y +137 líneas; menos si algunas ya existían, p. ej. las del DEMO).
-- La segunda consulta debe dar 137 (todas las líneas del script existen).
-- ---------------------------------------------------------------------
SELECT (SELECT COUNT(*) FROM marcas) AS marcas, (SELECT COUNT(*) FROM lineas) AS lineas,
       (SELECT COUNT(*) FROM vehiculos) AS vehiculos;

SELECT COUNT(*) AS lineas_del_script_presentes
FROM lineas l JOIN marcas m ON m.id = l.marca_id
JOIN (
      SELECT 'Chevrolet' AS marca, 'NHR' AS nombre
      UNION ALL SELECT 'Chevrolet' AS marca, 'NKR' AS nombre
      UNION ALL SELECT 'Chevrolet' AS marca, 'NPR' AS nombre
      UNION ALL SELECT 'Chevrolet' AS marca, 'NQR' AS nombre
      UNION ALL SELECT 'Chevrolet' AS marca, 'FRR' AS nombre
      UNION ALL SELECT 'Chevrolet' AS marca, 'FTR' AS nombre
      UNION ALL SELECT 'Chevrolet' AS marca, 'FVR' AS nombre
      UNION ALL SELECT 'Chevrolet' AS marca, 'Spark' AS nombre
      UNION ALL SELECT 'Chevrolet' AS marca, 'Onix' AS nombre
      UNION ALL SELECT 'Chevrolet' AS marca, 'Sail' AS nombre
      UNION ALL SELECT 'Chevrolet' AS marca, 'Tracker' AS nombre
      UNION ALL SELECT 'Chevrolet' AS marca, 'Captiva' AS nombre
      UNION ALL SELECT 'Chevrolet' AS marca, 'Colorado' AS nombre
      UNION ALL SELECT 'Hino' AS marca, 'Dutro' AS nombre
      UNION ALL SELECT 'Hino' AS marca, 'Serie 300' AS nombre
      UNION ALL SELECT 'Hino' AS marca, 'Serie 500' AS nombre
      UNION ALL SELECT 'Hino' AS marca, 'FC' AS nombre
      UNION ALL SELECT 'Hino' AS marca, 'GH' AS nombre
      UNION ALL SELECT 'Isuzu' AS marca, 'ELF 100' AS nombre
      UNION ALL SELECT 'Isuzu' AS marca, 'ELF 300' AS nombre
      UNION ALL SELECT 'Isuzu' AS marca, 'ELF 600' AS nombre
      UNION ALL SELECT 'Isuzu' AS marca, 'Forward' AS nombre
      UNION ALL SELECT 'JAC' AS marca, '1040' AS nombre
      UNION ALL SELECT 'JAC' AS marca, '1063' AS nombre
      UNION ALL SELECT 'JAC' AS marca, 'X200' AS nombre
      UNION ALL SELECT 'JAC' AS marca, 'Gallardo' AS nombre
      UNION ALL SELECT 'Foton' AS marca, 'Aumark' AS nombre
      UNION ALL SELECT 'Foton' AS marca, 'Auman' AS nombre
      UNION ALL SELECT 'Foton' AS marca, 'BJ1069' AS nombre
      UNION ALL SELECT 'International' AS marca, '4300' AS nombre
      UNION ALL SELECT 'International' AS marca, '4400' AS nombre
      UNION ALL SELECT 'International' AS marca, '7400' AS nombre
      UNION ALL SELECT 'International' AS marca, 'ProStar' AS nombre
      UNION ALL SELECT 'International' AS marca, 'LT' AS nombre
      UNION ALL SELECT 'Kenworth' AS marca, 'T370' AS nombre
      UNION ALL SELECT 'Kenworth' AS marca, 'T680' AS nombre
      UNION ALL SELECT 'Kenworth' AS marca, 'T800' AS nombre
      UNION ALL SELECT 'Kenworth' AS marca, 'T880' AS nombre
      UNION ALL SELECT 'Freightliner' AS marca, 'M2 106' AS nombre
      UNION ALL SELECT 'Freightliner' AS marca, 'Columbia' AS nombre
      UNION ALL SELECT 'Freightliner' AS marca, 'Cascadia' AS nombre
      UNION ALL SELECT 'Mercedes-Benz' AS marca, 'Accelo' AS nombre
      UNION ALL SELECT 'Mercedes-Benz' AS marca, 'Atego' AS nombre
      UNION ALL SELECT 'Mercedes-Benz' AS marca, 'Axor' AS nombre
      UNION ALL SELECT 'Mercedes-Benz' AS marca, 'Actros' AS nombre
      UNION ALL SELECT 'Volvo' AS marca, 'VM' AS nombre
      UNION ALL SELECT 'Volvo' AS marca, 'FM' AS nombre
      UNION ALL SELECT 'Volvo' AS marca, 'FH' AS nombre
      UNION ALL SELECT 'Volvo' AS marca, 'EC210' AS nombre
      UNION ALL SELECT 'Volvo' AS marca, 'L60' AS nombre
      UNION ALL SELECT 'Scania' AS marca, 'Serie P' AS nombre
      UNION ALL SELECT 'Scania' AS marca, 'Serie G' AS nombre
      UNION ALL SELECT 'Scania' AS marca, 'Serie R' AS nombre
      UNION ALL SELECT 'Hyundai' AS marca, 'HD65' AS nombre
      UNION ALL SELECT 'Hyundai' AS marca, 'HD78' AS nombre
      UNION ALL SELECT 'Hyundai' AS marca, 'Mighty' AS nombre
      UNION ALL SELECT 'Hyundai' AS marca, 'Xcient' AS nombre
      UNION ALL SELECT 'Hyundai' AS marca, 'i10' AS nombre
      UNION ALL SELECT 'Hyundai' AS marca, 'Accent' AS nombre
      UNION ALL SELECT 'Hyundai' AS marca, 'Tucson' AS nombre
      UNION ALL SELECT 'Hyundai' AS marca, 'Creta' AS nombre
      UNION ALL SELECT 'Hyundai' AS marca, 'R220' AS nombre
      UNION ALL SELECT 'Fuso' AS marca, 'Canter' AS nombre
      UNION ALL SELECT 'Fuso' AS marca, 'FI' AS nombre
      UNION ALL SELECT 'Fuso' AS marca, 'FJ' AS nombre
      UNION ALL SELECT 'Volkswagen' AS marca, 'Delivery' AS nombre
      UNION ALL SELECT 'Volkswagen' AS marca, 'Constellation' AS nombre
      UNION ALL SELECT 'Volkswagen' AS marca, 'Gol' AS nombre
      UNION ALL SELECT 'Volkswagen' AS marca, 'Voyage' AS nombre
      UNION ALL SELECT 'Volkswagen' AS marca, 'Tiguan' AS nombre
      UNION ALL SELECT 'Volkswagen' AS marca, 'Amarok' AS nombre
      UNION ALL SELECT 'Mack' AS marca, 'Granite' AS nombre
      UNION ALL SELECT 'Mack' AS marca, 'Anthem' AS nombre
      UNION ALL SELECT 'Dongfeng' AS marca, 'Captain' AS nombre
      UNION ALL SELECT 'Dongfeng' AS marca, 'Duolika' AS nombre
      UNION ALL SELECT 'Renault' AS marca, 'Logan' AS nombre
      UNION ALL SELECT 'Renault' AS marca, 'Sandero' AS nombre
      UNION ALL SELECT 'Renault' AS marca, 'Stepway' AS nombre
      UNION ALL SELECT 'Renault' AS marca, 'Duster' AS nombre
      UNION ALL SELECT 'Renault' AS marca, 'Kwid' AS nombre
      UNION ALL SELECT 'Renault' AS marca, 'Koleos' AS nombre
      UNION ALL SELECT 'Renault' AS marca, 'Oroch' AS nombre
      UNION ALL SELECT 'Mazda' AS marca, '2' AS nombre
      UNION ALL SELECT 'Mazda' AS marca, '3' AS nombre
      UNION ALL SELECT 'Mazda' AS marca, 'CX-3' AS nombre
      UNION ALL SELECT 'Mazda' AS marca, 'CX-5' AS nombre
      UNION ALL SELECT 'Mazda' AS marca, 'BT-50' AS nombre
      UNION ALL SELECT 'Kia' AS marca, 'Picanto' AS nombre
      UNION ALL SELECT 'Kia' AS marca, 'Rio' AS nombre
      UNION ALL SELECT 'Kia' AS marca, 'Sportage' AS nombre
      UNION ALL SELECT 'Kia' AS marca, 'Sorento' AS nombre
      UNION ALL SELECT 'Toyota' AS marca, 'Corolla' AS nombre
      UNION ALL SELECT 'Toyota' AS marca, 'Hilux' AS nombre
      UNION ALL SELECT 'Toyota' AS marca, 'Fortuner' AS nombre
      UNION ALL SELECT 'Toyota' AS marca, 'Prado' AS nombre
      UNION ALL SELECT 'Toyota' AS marca, 'RAV4' AS nombre
      UNION ALL SELECT 'Toyota' AS marca, '8FGCU25' AS nombre
      UNION ALL SELECT 'Nissan' AS marca, 'March' AS nombre
      UNION ALL SELECT 'Nissan' AS marca, 'Versa' AS nombre
      UNION ALL SELECT 'Nissan' AS marca, 'Kicks' AS nombre
      UNION ALL SELECT 'Nissan' AS marca, 'Frontier' AS nombre
      UNION ALL SELECT 'Nissan' AS marca, 'X-Trail' AS nombre
      UNION ALL SELECT 'Ford' AS marca, 'Ranger' AS nombre
      UNION ALL SELECT 'Ford' AS marca, 'Escape' AS nombre
      UNION ALL SELECT 'Ford' AS marca, 'Explorer' AS nombre
      UNION ALL SELECT 'Suzuki' AS marca, 'Swift' AS nombre
      UNION ALL SELECT 'Suzuki' AS marca, 'Vitara' AS nombre
      UNION ALL SELECT 'Suzuki' AS marca, 'Jimny' AS nombre
      UNION ALL SELECT 'Caterpillar' AS marca, '416F' AS nombre
      UNION ALL SELECT 'Caterpillar' AS marca, '420F' AS nombre
      UNION ALL SELECT 'Caterpillar' AS marca, '320' AS nombre
      UNION ALL SELECT 'Caterpillar' AS marca, '336' AS nombre
      UNION ALL SELECT 'Caterpillar' AS marca, 'D6' AS nombre
      UNION ALL SELECT 'Caterpillar' AS marca, '140K' AS nombre
      UNION ALL SELECT 'Caterpillar' AS marca, '262D' AS nombre
      UNION ALL SELECT 'JCB' AS marca, '3CX' AS nombre
      UNION ALL SELECT 'JCB' AS marca, '4CX' AS nombre
      UNION ALL SELECT 'JCB' AS marca, 'JS200' AS nombre
      UNION ALL SELECT 'John Deere' AS marca, '310L' AS nombre
      UNION ALL SELECT 'John Deere' AS marca, '410L' AS nombre
      UNION ALL SELECT 'John Deere' AS marca, '200G' AS nombre
      UNION ALL SELECT 'John Deere' AS marca, '6110' AS nombre
      UNION ALL SELECT 'Komatsu' AS marca, 'PC200' AS nombre
      UNION ALL SELECT 'Komatsu' AS marca, 'WB97' AS nombre
      UNION ALL SELECT 'Komatsu' AS marca, 'D61' AS nombre
      UNION ALL SELECT 'Bobcat' AS marca, 'S450' AS nombre
      UNION ALL SELECT 'Bobcat' AS marca, 'S570' AS nombre
      UNION ALL SELECT 'Bobcat' AS marca, 'E35' AS nombre
      UNION ALL SELECT 'Case' AS marca, '580N' AS nombre
      UNION ALL SELECT 'Case' AS marca, '590SN' AS nombre
      UNION ALL SELECT 'Case' AS marca, 'SR175' AS nombre
      UNION ALL SELECT 'Doosan' AS marca, 'DX225' AS nombre
      UNION ALL SELECT 'Doosan' AS marca, 'DX140' AS nombre
      UNION ALL SELECT 'New Holland' AS marca, 'B95' AS nombre
      UNION ALL SELECT 'New Holland' AS marca, 'TT4030' AS nombre
      UNION ALL SELECT 'Yale' AS marca, 'GLP050' AS nombre
      UNION ALL SELECT 'Hyster' AS marca, 'H50FT' AS nombre
) x ON x.marca = m.nombre AND x.nombre = l.nombre;

-- =====================================================================
-- BAJAR (comentada). Quita SOLO lo que añade este script y que ningún vehículo usa.
-- No toca las marcas y líneas que inserta el DEMO de P3 (D-18) ni las que use algún vehículo.
-- Si una marca o línea ya existía antes de SUBIR y está en esta lista, también se borraría:
-- compara con la VERIFICACIÓN ANTES. Para usarla, quita los "-- " del bloque.
-- =====================================================================
-- START TRANSACTION;
-- DELETE l FROM lineas l JOIN marcas m ON m.id = l.marca_id
-- JOIN (
--       SELECT 'Chevrolet' AS marca, 'NKR' AS nombre
--       UNION ALL SELECT 'Chevrolet' AS marca, 'NQR' AS nombre
--       UNION ALL SELECT 'Chevrolet' AS marca, 'FRR' AS nombre
--       UNION ALL SELECT 'Chevrolet' AS marca, 'FTR' AS nombre
--       UNION ALL SELECT 'Chevrolet' AS marca, 'FVR' AS nombre
--       UNION ALL SELECT 'Chevrolet' AS marca, 'Spark' AS nombre
--       UNION ALL SELECT 'Chevrolet' AS marca, 'Onix' AS nombre
--       UNION ALL SELECT 'Chevrolet' AS marca, 'Sail' AS nombre
--       UNION ALL SELECT 'Chevrolet' AS marca, 'Tracker' AS nombre
--       UNION ALL SELECT 'Chevrolet' AS marca, 'Captiva' AS nombre
--       UNION ALL SELECT 'Chevrolet' AS marca, 'Colorado' AS nombre
--       UNION ALL SELECT 'Hino' AS marca, 'Serie 300' AS nombre
--       UNION ALL SELECT 'Hino' AS marca, 'FC' AS nombre
--       UNION ALL SELECT 'Hino' AS marca, 'GH' AS nombre
--       UNION ALL SELECT 'Isuzu' AS marca, 'ELF 100' AS nombre
--       UNION ALL SELECT 'Isuzu' AS marca, 'ELF 300' AS nombre
--       UNION ALL SELECT 'Isuzu' AS marca, 'ELF 600' AS nombre
--       UNION ALL SELECT 'Isuzu' AS marca, 'Forward' AS nombre
--       UNION ALL SELECT 'JAC' AS marca, '1040' AS nombre
--       UNION ALL SELECT 'JAC' AS marca, '1063' AS nombre
--       UNION ALL SELECT 'JAC' AS marca, 'X200' AS nombre
--       UNION ALL SELECT 'JAC' AS marca, 'Gallardo' AS nombre
--       UNION ALL SELECT 'Foton' AS marca, 'Aumark' AS nombre
--       UNION ALL SELECT 'Foton' AS marca, 'Auman' AS nombre
--       UNION ALL SELECT 'Foton' AS marca, 'BJ1069' AS nombre
--       UNION ALL SELECT 'International' AS marca, '4300' AS nombre
--       UNION ALL SELECT 'International' AS marca, '4400' AS nombre
--       UNION ALL SELECT 'International' AS marca, '7400' AS nombre
--       UNION ALL SELECT 'International' AS marca, 'ProStar' AS nombre
--       UNION ALL SELECT 'International' AS marca, 'LT' AS nombre
--       UNION ALL SELECT 'Kenworth' AS marca, 'T370' AS nombre
--       UNION ALL SELECT 'Kenworth' AS marca, 'T680' AS nombre
--       UNION ALL SELECT 'Kenworth' AS marca, 'T880' AS nombre
--       UNION ALL SELECT 'Freightliner' AS marca, 'M2 106' AS nombre
--       UNION ALL SELECT 'Freightliner' AS marca, 'Columbia' AS nombre
--       UNION ALL SELECT 'Freightliner' AS marca, 'Cascadia' AS nombre
--       UNION ALL SELECT 'Mercedes-Benz' AS marca, 'Accelo' AS nombre
--       UNION ALL SELECT 'Mercedes-Benz' AS marca, 'Atego' AS nombre
--       UNION ALL SELECT 'Mercedes-Benz' AS marca, 'Axor' AS nombre
--       UNION ALL SELECT 'Mercedes-Benz' AS marca, 'Actros' AS nombre
--       UNION ALL SELECT 'Volvo' AS marca, 'VM' AS nombre
--       UNION ALL SELECT 'Volvo' AS marca, 'FM' AS nombre
--       UNION ALL SELECT 'Volvo' AS marca, 'FH' AS nombre
--       UNION ALL SELECT 'Volvo' AS marca, 'EC210' AS nombre
--       UNION ALL SELECT 'Volvo' AS marca, 'L60' AS nombre
--       UNION ALL SELECT 'Scania' AS marca, 'Serie P' AS nombre
--       UNION ALL SELECT 'Scania' AS marca, 'Serie G' AS nombre
--       UNION ALL SELECT 'Scania' AS marca, 'Serie R' AS nombre
--       UNION ALL SELECT 'Hyundai' AS marca, 'HD65' AS nombre
--       UNION ALL SELECT 'Hyundai' AS marca, 'HD78' AS nombre
--       UNION ALL SELECT 'Hyundai' AS marca, 'Mighty' AS nombre
--       UNION ALL SELECT 'Hyundai' AS marca, 'Xcient' AS nombre
--       UNION ALL SELECT 'Hyundai' AS marca, 'i10' AS nombre
--       UNION ALL SELECT 'Hyundai' AS marca, 'Accent' AS nombre
--       UNION ALL SELECT 'Hyundai' AS marca, 'Tucson' AS nombre
--       UNION ALL SELECT 'Hyundai' AS marca, 'Creta' AS nombre
--       UNION ALL SELECT 'Hyundai' AS marca, 'R220' AS nombre
--       UNION ALL SELECT 'Fuso' AS marca, 'Canter' AS nombre
--       UNION ALL SELECT 'Fuso' AS marca, 'FI' AS nombre
--       UNION ALL SELECT 'Fuso' AS marca, 'FJ' AS nombre
--       UNION ALL SELECT 'Volkswagen' AS marca, 'Delivery' AS nombre
--       UNION ALL SELECT 'Volkswagen' AS marca, 'Constellation' AS nombre
--       UNION ALL SELECT 'Volkswagen' AS marca, 'Gol' AS nombre
--       UNION ALL SELECT 'Volkswagen' AS marca, 'Voyage' AS nombre
--       UNION ALL SELECT 'Volkswagen' AS marca, 'Tiguan' AS nombre
--       UNION ALL SELECT 'Volkswagen' AS marca, 'Amarok' AS nombre
--       UNION ALL SELECT 'Mack' AS marca, 'Granite' AS nombre
--       UNION ALL SELECT 'Mack' AS marca, 'Anthem' AS nombre
--       UNION ALL SELECT 'Dongfeng' AS marca, 'Captain' AS nombre
--       UNION ALL SELECT 'Dongfeng' AS marca, 'Duolika' AS nombre
--       UNION ALL SELECT 'Renault' AS marca, 'Sandero' AS nombre
--       UNION ALL SELECT 'Renault' AS marca, 'Stepway' AS nombre
--       UNION ALL SELECT 'Renault' AS marca, 'Kwid' AS nombre
--       UNION ALL SELECT 'Renault' AS marca, 'Koleos' AS nombre
--       UNION ALL SELECT 'Renault' AS marca, 'Oroch' AS nombre
--       UNION ALL SELECT 'Mazda' AS marca, '2' AS nombre
--       UNION ALL SELECT 'Mazda' AS marca, '3' AS nombre
--       UNION ALL SELECT 'Mazda' AS marca, 'CX-3' AS nombre
--       UNION ALL SELECT 'Mazda' AS marca, 'CX-5' AS nombre
--       UNION ALL SELECT 'Mazda' AS marca, 'BT-50' AS nombre
--       UNION ALL SELECT 'Kia' AS marca, 'Picanto' AS nombre
--       UNION ALL SELECT 'Kia' AS marca, 'Rio' AS nombre
--       UNION ALL SELECT 'Kia' AS marca, 'Sportage' AS nombre
--       UNION ALL SELECT 'Kia' AS marca, 'Sorento' AS nombre
--       UNION ALL SELECT 'Toyota' AS marca, 'Corolla' AS nombre
--       UNION ALL SELECT 'Toyota' AS marca, 'Fortuner' AS nombre
--       UNION ALL SELECT 'Toyota' AS marca, 'Prado' AS nombre
--       UNION ALL SELECT 'Toyota' AS marca, 'RAV4' AS nombre
--       UNION ALL SELECT 'Nissan' AS marca, 'March' AS nombre
--       UNION ALL SELECT 'Nissan' AS marca, 'Versa' AS nombre
--       UNION ALL SELECT 'Nissan' AS marca, 'Kicks' AS nombre
--       UNION ALL SELECT 'Nissan' AS marca, 'Frontier' AS nombre
--       UNION ALL SELECT 'Nissan' AS marca, 'X-Trail' AS nombre
--       UNION ALL SELECT 'Ford' AS marca, 'Ranger' AS nombre
--       UNION ALL SELECT 'Ford' AS marca, 'Escape' AS nombre
--       UNION ALL SELECT 'Ford' AS marca, 'Explorer' AS nombre
--       UNION ALL SELECT 'Suzuki' AS marca, 'Swift' AS nombre
--       UNION ALL SELECT 'Suzuki' AS marca, 'Vitara' AS nombre
--       UNION ALL SELECT 'Suzuki' AS marca, 'Jimny' AS nombre
--       UNION ALL SELECT 'Caterpillar' AS marca, '420F' AS nombre
--       UNION ALL SELECT 'Caterpillar' AS marca, '320' AS nombre
--       UNION ALL SELECT 'Caterpillar' AS marca, '336' AS nombre
--       UNION ALL SELECT 'Caterpillar' AS marca, 'D6' AS nombre
--       UNION ALL SELECT 'Caterpillar' AS marca, '140K' AS nombre
--       UNION ALL SELECT 'Caterpillar' AS marca, '262D' AS nombre
--       UNION ALL SELECT 'JCB' AS marca, '3CX' AS nombre
--       UNION ALL SELECT 'JCB' AS marca, '4CX' AS nombre
--       UNION ALL SELECT 'JCB' AS marca, 'JS200' AS nombre
--       UNION ALL SELECT 'John Deere' AS marca, '310L' AS nombre
--       UNION ALL SELECT 'John Deere' AS marca, '410L' AS nombre
--       UNION ALL SELECT 'John Deere' AS marca, '200G' AS nombre
--       UNION ALL SELECT 'John Deere' AS marca, '6110' AS nombre
--       UNION ALL SELECT 'Komatsu' AS marca, 'PC200' AS nombre
--       UNION ALL SELECT 'Komatsu' AS marca, 'WB97' AS nombre
--       UNION ALL SELECT 'Komatsu' AS marca, 'D61' AS nombre
--       UNION ALL SELECT 'Bobcat' AS marca, 'S450' AS nombre
--       UNION ALL SELECT 'Bobcat' AS marca, 'S570' AS nombre
--       UNION ALL SELECT 'Bobcat' AS marca, 'E35' AS nombre
--       UNION ALL SELECT 'Case' AS marca, '580N' AS nombre
--       UNION ALL SELECT 'Case' AS marca, '590SN' AS nombre
--       UNION ALL SELECT 'Case' AS marca, 'SR175' AS nombre
--       UNION ALL SELECT 'Doosan' AS marca, 'DX225' AS nombre
--       UNION ALL SELECT 'Doosan' AS marca, 'DX140' AS nombre
--       UNION ALL SELECT 'New Holland' AS marca, 'B95' AS nombre
--       UNION ALL SELECT 'New Holland' AS marca, 'TT4030' AS nombre
--       UNION ALL SELECT 'Yale' AS marca, 'GLP050' AS nombre
--       UNION ALL SELECT 'Hyster' AS marca, 'H50FT' AS nombre
-- ) x ON x.marca = m.nombre AND x.nombre = l.nombre
-- WHERE NOT EXISTS (SELECT 1 FROM vehiculos v WHERE v.linea_id = l.id);
-- DELETE FROM marcas WHERE nombre IN ('Isuzu', 'JAC', 'Foton', 'International', 'Freightliner', 'Mercedes-Benz', 'Volvo', 'Scania', 'Hyundai', 'Fuso', 'Volkswagen', 'Mack', 'Dongfeng', 'Mazda', 'Kia', 'Nissan', 'Ford', 'Suzuki', 'JCB', 'John Deere', 'Komatsu', 'Bobcat', 'Case', 'Doosan', 'New Holland', 'Yale', 'Hyster')
--   AND NOT EXISTS (SELECT 1 FROM vehiculos v WHERE v.marca_id = marcas.id)
--   AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.marca_id = marcas.id);
-- COMMIT;
-- Verificación tras BAJAR: repetir la VERIFICACIÓN ANTES; vehiculos debe ser igual.
