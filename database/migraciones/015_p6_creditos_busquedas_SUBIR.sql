-- =====================================================================
-- Migración 015 — Créditos, búsquedas y "Mis solicitudes" (P6, HU-034 a HU-042, D-7, D-44, D-45, D-54 a D-59).
-- SOLO SUBIR, para phpMyAdmin (D-16). Sin consultas a information_schema.
-- Requisitos: respaldo previo del coordinador en C:\Respaldo_CAMION\ y API detenida.
-- Verificación y BAJAR: 015_p6_creditos_busquedas.sql (cliente mysql del coordinador).
-- Crea leads_busqueda y busqueda_vehiculos, un índice en leads y amplía cuatro CHECK. No toca filas.
-- Únicos DROP: DROP CONSTRAINT de CK_Lead_Origen, CK_Lead_Resultado, CK_Consent_Formulario y CK_EvLead_Tipo,
-- cada uno seguido de la sentencia que lo vuelve a crear ampliado. Sin DROP TABLE, DROP COLUMN, TRUNCATE ni DELETE.
-- No usa solicitudes, detalle_solicitud_credito, detalle_busqueda_personalizada ni alertas_busqueda del script 002.
-- =====================================================================

-- 1. CK_Lead_Origen con CREDITO (con publicación desde la ficha o sin ella desde la portada; nunca tipo de vehículo,
--    D-55 B) y BUSQUEDA (sin publicación ni tipo de vehículo). Las tres ramas de la 014 no cambian.
ALTER TABLE leads DROP CONSTRAINT CK_Lead_Origen;
ALTER TABLE leads
  ADD CONSTRAINT CK_Lead_Origen CHECK (
    (origen = 'CONTACTO_VEHICULO' AND publicacion_id IS NOT NULL AND tipo_vehiculo IS NULL) OR
    (origen = 'VENDE_TU_CAMION' AND publicacion_id IS NULL AND tipo_vehiculo IS NULL) OR
    (origen = 'SEGURO_TODO_RIESGO' AND publicacion_id IS NULL AND tipo_vehiculo IS NOT NULL AND tipo_vehiculo IN ('CAMION','CARRO','MAQUINARIA','OTRO')) OR
    (origen = 'CREDITO' AND tipo_vehiculo IS NULL) OR
    (origen = 'BUSQUEDA' AND publicacion_id IS NULL AND tipo_vehiculo IS NULL));

-- 2. CK_Lead_Resultado con los resultados de D-57 b (A): REMITIDO_ENTIDAD (crédito), VEHICULO_ENCONTRADO y
--    SIN_VEHICULO (búsqueda). CK_Lead_AvisoAdmin no cambia (D-58 A: créditos y búsquedas siempre en Administración).
ALTER TABLE leads DROP CONSTRAINT CK_Lead_Resultado;
ALTER TABLE leads
  ADD CONSTRAINT CK_Lead_Resultado CHECK (resultado_cierre IS NULL OR resultado_cierre IN
    ('VENTA_CONCRETADA','NO_INTERESADO','NO_RESPONDIO','VEHICULO_NO_DISPONIBLE','DUPLICADO_INVALIDO','OTRO','ATENDIDA',
     'REMITIDO_ENTIDAD','VEHICULO_ENCONTRADO','SIN_VEHICULO'));

-- 3. D-59 (a): "Mis solicitudes" lista los leads del usuario, del más reciente al más antiguo.
ALTER TABLE leads ADD KEY IX_Lead_Usuario (usuario_id, creado_en);

-- 4. D-44: prueba de la autorización de los dos formularios nuevos.
ALTER TABLE consentimientos_datos DROP CONSTRAINT CK_Consent_Formulario;
ALTER TABLE consentimientos_datos
  ADD CONSTRAINT CK_Consent_Formulario CHECK (formulario IN
    ('CONTACTO_VEHICULO','VENDE_TU_CAMION','SEGURO_TODO_RIESGO','REGISTRO_COMPRADOR','INSCRIPCION_ASESOR',
     'CREDITO','BUSQUEDA'));

-- 5. HU-036 (D-56 b A): eventos al relacionar y retirar un vehículo de una búsqueda.
ALTER TABLE eventos_lead DROP CONSTRAINT CK_EvLead_Tipo;
ALTER TABLE eventos_lead
  ADD CONSTRAINT CK_EvLead_Tipo CHECK (tipo IN
    ('CREADO','ASIGNADO','CONTACTO_REPETIDO','NOTA','REASIGNADO','CERRADO','REABIERTO',
     'VEHICULO_RELACIONADO','VEHICULO_RETIRADO'));

-- 6. HU-034 (D-56 a A): criterios de la búsqueda, una fila por lead BUSQUEDA. Categoría obligatoria; el resto opcional.
CREATE TABLE leads_busqueda (
  lead_id BIGINT NOT NULL PRIMARY KEY,
  tipo_vehiculo_id BIGINT NOT NULL,
  marca_id BIGINT NULL,
  linea_id BIGINT NULL,
  anio_desde SMALLINT NULL,
  anio_hasta SMALLINT NULL,
  presupuesto_max_cop BIGINT NULL,
  departamento_id BIGINT NULL,
  municipio_id BIGINT NULL,
  KEY IX_LeadBusq_Tipo (tipo_vehiculo_id),
  KEY IX_LeadBusq_Marca (marca_id),
  KEY IX_LeadBusq_Linea (linea_id),
  KEY IX_LeadBusq_Departamento (departamento_id),
  KEY IX_LeadBusq_Municipio (municipio_id),
  CONSTRAINT FK_LeadBusq_Lead FOREIGN KEY (lead_id) REFERENCES leads(id) ON DELETE RESTRICT,
  CONSTRAINT FK_LeadBusq_Tipo FOREIGN KEY (tipo_vehiculo_id) REFERENCES tipos_vehiculo(id) ON DELETE RESTRICT,
  CONSTRAINT FK_LeadBusq_Marca FOREIGN KEY (marca_id) REFERENCES marcas(id) ON DELETE RESTRICT,
  CONSTRAINT FK_LeadBusq_Linea FOREIGN KEY (linea_id) REFERENCES lineas(id) ON DELETE RESTRICT,
  CONSTRAINT FK_LeadBusq_Departamento FOREIGN KEY (departamento_id) REFERENCES departamentos(id) ON DELETE RESTRICT,
  CONSTRAINT FK_LeadBusq_Municipio FOREIGN KEY (municipio_id) REFERENCES municipios(id) ON DELETE RESTRICT,
  CONSTRAINT CK_LeadBusq_Anios CHECK ((anio_desde IS NULL OR anio_desde BETWEEN 1950 AND 2100) AND
    (anio_hasta IS NULL OR anio_hasta BETWEEN 1950 AND 2100) AND
    (anio_desde IS NULL OR anio_hasta IS NULL OR anio_desde <= anio_hasta)),
  CONSTRAINT CK_LeadBusq_Presupuesto CHECK (presupuesto_max_cop IS NULL OR
    presupuesto_max_cop BETWEEN 1000000 AND 5000000000)
) ENGINE=InnoDB;

-- 7. HU-036 (D-56 b A): publicaciones que el administrador relaciona con una búsqueda. Retirar no borra la fila
--    (retirado_en / retirado_por); relacionar de nuevo la reactiva (UNIQUE lead + publicación).
CREATE TABLE busqueda_vehiculos (
  id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  lead_id BIGINT NOT NULL,
  publicacion_id BIGINT NOT NULL,
  nota VARCHAR(300) NULL,
  relacionado_por VARCHAR(255) NULL,
  relacionado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  retirado_por VARCHAR(255) NULL,
  retirado_en DATETIME(6) NULL,
  UNIQUE KEY UX_BusqVeh_LeadPub (lead_id, publicacion_id),
  KEY IX_BusqVeh_Publicacion (publicacion_id),
  KEY IX_BusqVeh_RelacionadoPor (relacionado_por),
  KEY IX_BusqVeh_RetiradoPor (retirado_por),
  CONSTRAINT FK_BusqVeh_Lead FOREIGN KEY (lead_id) REFERENCES leads(id) ON DELETE RESTRICT,
  CONSTRAINT FK_BusqVeh_Publicacion FOREIGN KEY (publicacion_id) REFERENCES publicaciones(id) ON DELETE RESTRICT,
  CONSTRAINT FK_BusqVeh_RelacionadoPor FOREIGN KEY (relacionado_por) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,
  CONSTRAINT FK_BusqVeh_RetiradoPor FOREIGN KEY (retirado_por) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
) ENGINE=InnoDB;
