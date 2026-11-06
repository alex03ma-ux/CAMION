-- =====================================================================
-- Migración 014 — Avisos "Vende tu camión" y "Compra tu seguro todo riesgo" (P5b, HU-N01, HU-N02, D-6, D-7,
-- D-44, D-50, D-52).
-- SOLO SUBIR, para phpMyAdmin (D-16). Sin consultas a information_schema.
-- Requisitos: respaldo previo del coordinador en C:\Respaldo_CAMION\ y API detenida.
-- Verificación y BAJAR: 014_p5b_avisos.sql (cliente mysql del coordinador).
-- Solo ALTER TABLE leads. No crea tablas ni toca filas: los leads existentes quedan como CONTACTO_VEHICULO.
-- Único DROP: DROP CONSTRAINT CK_Lead_Resultado, seguido de la sentencia que lo vuelve a crear con ATENDIDA.
-- Sin DROP TABLE, DROP COLUMN, TRUNCATE ni DELETE.
-- =====================================================================

-- 1 y 2. Columnas nuevas y publicacion_id opcional (un aviso no tiene publicación). La FK RESTRICT se mantiene.
ALTER TABLE leads
  MODIFY COLUMN publicacion_id BIGINT NULL,
  ADD COLUMN origen VARCHAR(30) NOT NULL DEFAULT 'CONTACTO_VEHICULO' AFTER publicacion_id,
  ADD COLUMN tipo_vehiculo VARCHAR(20) NULL AFTER origen,
  ADD KEY IX_Lead_Origen (origen, estado, creado_en),
  ADD KEY IX_Lead_AvisoDuplicado (origen, telefono, estado);

-- 3. Coherencia origen / publicación / tipo de vehículo (D-50 a). 4. D-52 (A): un aviso nunca va a un asesor.
-- 5. CK_Lead_Resultado admite ATENDIDA ("Solicitud atendida").
ALTER TABLE leads
  ADD CONSTRAINT CK_Lead_Origen CHECK (
    (origen = 'CONTACTO_VEHICULO' AND publicacion_id IS NOT NULL AND tipo_vehiculo IS NULL) OR
    (origen = 'VENDE_TU_CAMION' AND publicacion_id IS NULL AND tipo_vehiculo IS NULL) OR
    (origen = 'SEGURO_TODO_RIESGO' AND publicacion_id IS NULL AND tipo_vehiculo IN ('CAMION','CARRO','MAQUINARIA','OTRO'))),
  ADD CONSTRAINT CK_Lead_AvisoAdmin CHECK (origen = 'CONTACTO_VEHICULO' OR responsable_asesor_id IS NULL);

-- CK_Lead_Resultado con ATENDIDA: se quita y se vuelve a crear en dos sentencias seguidas (algunas versiones no
-- admiten quitar y crear un CHECK con el mismo nombre en un solo ALTER). Las filas existentes cumplen el nuevo.
ALTER TABLE leads DROP CONSTRAINT CK_Lead_Resultado;
ALTER TABLE leads
  ADD CONSTRAINT CK_Lead_Resultado CHECK (resultado_cierre IS NULL OR resultado_cierre IN
    ('VENTA_CONCRETADA','NO_INTERESADO','NO_RESPONDIO','VEHICULO_NO_DISPONIBLE','DUPLICADO_INVALIDO','OTRO','ATENDIDA'));
