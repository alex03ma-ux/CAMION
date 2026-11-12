-- =====================================================================
-- Migración 014 — Avisos "Vende tu camión" y "Compra tu seguro todo riesgo" (P5b, HU-N01, HU-N02, D-6, D-7,
-- D-44, D-50, D-52). Base: camion_ventas (MariaDB 10.4).
--
-- Cambios (solo ALTER TABLE leads; contrato: docs/criterios/P5b-contrato-api.md):
--   1. leads.publicacion_id pasa a NULL (un aviso no tiene publicación). La FK RESTRICT se mantiene.
--   2. leads.origen (nueva, NOT NULL DEFAULT 'CONTACTO_VEHICULO') y leads.tipo_vehiculo (nueva, NULL; D-50 a).
--      Los leads existentes quedan como CONTACTO_VEHICULO.
--   3. CK_Lead_Origen: coherencia entre origen, publicación y tipo de vehículo.
--   4. CK_Lead_AvisoAdmin (D-52 A): un aviso nunca tiene responsable asesor.
--   5. CK_Lead_Resultado se reemplaza para admitir ATENDIDA ("Solicitud atendida"). Es el único DROP de la parte
--      SUBIR: DROP CONSTRAINT de un CHECK, seguido de la sentencia que lo vuelve a crear; no borra datos.
--   6. Índices IX_Lead_Origen e IX_Lead_AvisoDuplicado.
-- No crea tablas ni toca filas. Sin DROP TABLE, DROP COLUMN, TRUNCATE ni DELETE en la parte SUBIR.
--
-- Cómo aplicarla (D-16):
--   1. Respaldo completo nuevo en C:\Respaldo_CAMION\ (coordinador). No sobrescribir respaldos existentes.
--   2. Detener la API (la versión P5b de la API lee leads.origen y leads.tipo_vehiculo).
--   3. En phpMyAdmin (Chrome/Edge) importar SOLO 014_p5b_avisos_SUBIR.sql.
--   4. El coordinador ejecuta la VERIFICACIÓN de este archivo con el cliente mysql.
-- Nota: en MariaDB cada ALTER TABLE hace commit implícito; el script no es atómico. Cada ALTER sí lo es.
-- =====================================================================
USE camion_ventas;

-- ---------------------------------------------------------------------
-- VERIFICACIÓN ANTES (cliente mysql; anotar el resultado)
-- Esperado: leads existe sin las columnas origen ni tipo_vehiculo; publicacion_id IS_NULLABLE = NO.
-- Anotar los tres conteos para compararlos después (CA-P5b-59).
-- ---------------------------------------------------------------------
SELECT COLUMN_NAME, IS_NULLABLE, COLUMN_TYPE, COLUMN_DEFAULT FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'leads'
  AND COLUMN_NAME IN ('publicacion_id', 'origen', 'tipo_vehiculo')
ORDER BY COLUMN_NAME;
SELECT (SELECT COUNT(*) FROM leads) AS leads, (SELECT COUNT(*) FROM eventos_lead) AS eventos,
       (SELECT COUNT(*) FROM consentimientos_datos) AS consentimientos;
SELECT COUNT(*) AS leads_sin_publicacion FROM leads WHERE publicacion_id IS NULL;  -- esperado 0

-- =====================================================================
-- SUBIR (idéntica a 014_p5b_avisos_SUBIR.sql)
-- =====================================================================

-- 1 y 2. Columnas nuevas y publicacion_id opcional. Los leads de P5 toman origen = 'CONTACTO_VEHICULO'.
ALTER TABLE leads
  MODIFY COLUMN publicacion_id BIGINT NULL,
  ADD COLUMN origen VARCHAR(30) NOT NULL DEFAULT 'CONTACTO_VEHICULO' AFTER publicacion_id,
  ADD COLUMN tipo_vehiculo VARCHAR(20) NULL AFTER origen,
  ADD KEY IX_Lead_Origen (origen, estado, creado_en),
  ADD KEY IX_Lead_AvisoDuplicado (origen, telefono, estado);

-- 3, 4 y 5. CHECK nuevos y CK_Lead_Resultado con ATENDIDA (las filas existentes los cumplen).
ALTER TABLE leads
  -- IS NOT NULL explícito: un IN con NULL da NULL y un CHECK con resultado NULL deja pasar la fila.
  ADD CONSTRAINT CK_Lead_Origen CHECK (
    (origen = 'CONTACTO_VEHICULO' AND publicacion_id IS NOT NULL AND tipo_vehiculo IS NULL) OR
    (origen = 'VENDE_TU_CAMION' AND publicacion_id IS NULL AND tipo_vehiculo IS NULL) OR
    (origen = 'SEGURO_TODO_RIESGO' AND publicacion_id IS NULL AND tipo_vehiculo IS NOT NULL AND tipo_vehiculo IN ('CAMION','CARRO','MAQUINARIA','OTRO'))),
  ADD CONSTRAINT CK_Lead_AvisoAdmin CHECK (origen = 'CONTACTO_VEHICULO' OR responsable_asesor_id IS NULL);

-- CK_Lead_Resultado con ATENDIDA: se quita y se vuelve a crear en dos sentencias seguidas (algunas versiones no
-- admiten quitar y crear un CHECK con el mismo nombre en un solo ALTER). Las filas existentes cumplen el nuevo.
ALTER TABLE leads DROP CONSTRAINT CK_Lead_Resultado;
ALTER TABLE leads
  ADD CONSTRAINT CK_Lead_Resultado CHECK (resultado_cierre IS NULL OR resultado_cierre IN
    ('VENTA_CONCRETADA','NO_INTERESADO','NO_RESPONDIO','VEHICULO_NO_DISPONIBLE','DUPLICADO_INVALIDO','OTRO','ATENDIDA'));

-- ---------------------------------------------------------------------
-- VERIFICACIÓN DESPUÉS DE SUBIR
-- Esperado: los tres conteos iguales a los de antes; todos los leads con origen CONTACTO_VEHICULO;
-- publicacion_id IS_NULLABLE = YES; CK_Lead_Origen, CK_Lead_AvisoAdmin y CK_Lead_Resultado (con ATENDIDA) presentes;
-- FK_Lead_Publicacion sigue con DELETE_RULE RESTRICT; IX_Lead_Origen e IX_Lead_AvisoDuplicado presentes.
-- ---------------------------------------------------------------------
SELECT (SELECT COUNT(*) FROM leads) AS leads, (SELECT COUNT(*) FROM eventos_lead) AS eventos,
       (SELECT COUNT(*) FROM consentimientos_datos) AS consentimientos;
SELECT origen, COUNT(*) AS leads FROM leads GROUP BY origen;

SELECT COLUMN_NAME, IS_NULLABLE, COLUMN_TYPE, COLUMN_DEFAULT FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'leads'
  AND COLUMN_NAME IN ('publicacion_id', 'origen', 'tipo_vehiculo')
ORDER BY COLUMN_NAME;

SELECT CONSTRAINT_NAME, CHECK_CLAUSE FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'leads'
ORDER BY CONSTRAINT_NAME;

SELECT CONSTRAINT_NAME, REFERENCED_TABLE_NAME, DELETE_RULE FROM information_schema.REFERENTIAL_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'leads'
ORDER BY CONSTRAINT_NAME;

SELECT DISTINCT INDEX_NAME FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'leads'
ORDER BY INDEX_NAME;

-- =====================================================================
-- BAJAR (rollback, comentada). Detener antes la API de P5b. Deshace solo lo que hace esta migración.
-- Paso 1: comprobaciones (CA-P5b-60). Si alguna da un resultado mayor que 0, NO bajar sin decidir antes con la
-- responsable: hay avisos o cierres ATENDIDA que no caben en el esquema de la 013 y se perderían. Exportarlos antes
-- con aprobación o restaurar el respaldo.
-- =====================================================================
/*
SELECT COUNT(*) AS avisos FROM leads WHERE origen <> 'CONTACTO_VEHICULO';
SELECT COUNT(*) AS sin_publicacion FROM leads WHERE publicacion_id IS NULL;
SELECT COUNT(*) AS atendidas FROM leads WHERE resultado_cierre = 'ATENDIDA';

-- Paso 2 (solo si las tres comprobaciones dan 0):
ALTER TABLE leads
  DROP CONSTRAINT CK_Lead_AvisoAdmin,
  DROP CONSTRAINT CK_Lead_Origen,
  DROP CONSTRAINT CK_Lead_Resultado;
ALTER TABLE leads
  ADD CONSTRAINT CK_Lead_Resultado CHECK (resultado_cierre IS NULL OR resultado_cierre IN
    ('VENTA_CONCRETADA','NO_INTERESADO','NO_RESPONDIO','VEHICULO_NO_DISPONIBLE','DUPLICADO_INVALIDO','OTRO'));

ALTER TABLE leads
  DROP KEY IX_Lead_AvisoDuplicado,
  DROP KEY IX_Lead_Origen,
  DROP COLUMN tipo_vehiculo,
  DROP COLUMN origen,
  MODIFY COLUMN publicacion_id BIGINT NOT NULL;

-- VERIFICACIÓN DESPUÉS DE BAJAR: sin origen ni tipo_vehiculo; publicacion_id IS_NULLABLE = NO;
-- CK_Lead_Resultado con 6 valores; conteos de leads, eventos_lead y consentimientos_datos iguales a los de antes.
SELECT COLUMN_NAME, IS_NULLABLE FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'leads'
  AND COLUMN_NAME IN ('publicacion_id', 'origen', 'tipo_vehiculo');
SELECT CONSTRAINT_NAME, CHECK_CLAUSE FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'leads';
SELECT (SELECT COUNT(*) FROM leads) AS leads, (SELECT COUNT(*) FROM eventos_lead) AS eventos,
       (SELECT COUNT(*) FROM consentimientos_datos) AS consentimientos;
*/
