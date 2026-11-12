-- =====================================================================
-- Migración 015 — Créditos, búsquedas y "Mis solicitudes" (P6, HU-034 a HU-042, D-7, D-44, D-45, D-54 a D-59).
-- Base: camion_ventas (MariaDB 10.4). Contrato: docs/criterios/P6-contrato-api.md.
--
-- Cambios:
--   1. CK_Lead_Origen admite CREDITO (con o sin publicación, nunca tipo_vehiculo; D-55 B) y BUSQUEDA (sin publicación).
--   2. CK_Lead_Resultado admite REMITIDO_ENTIDAD, VEHICULO_ENCONTRADO y SIN_VEHICULO (D-57 b A).
--   3. Índice IX_Lead_Usuario (usuario_id, creado_en) para "Mis solicitudes" (D-59 a A).
--   4. CK_Consent_Formulario admite CREDITO y BUSQUEDA (D-44).
--   5. CK_EvLead_Tipo admite VEHICULO_RELACIONADO y VEHICULO_RETIRADO (D-56 b A).
--   6. Tabla nueva leads_busqueda (criterios de la búsqueda; D-56 a A).
--   7. Tabla nueva busqueda_vehiculos (vehículos relacionados por el administrador; D-56 b A).
-- CK_Lead_AvisoAdmin y CK_Lead_Estado no cambian. El crédito no tiene tabla propia (D-55 B).
-- Únicos DROP de la parte SUBIR: DROP CONSTRAINT de cuatro CHECK, cada uno seguido de la sentencia que lo vuelve a
-- crear ampliado (las filas existentes cumplen los nuevos). Sin DROP TABLE, DROP COLUMN, TRUNCATE ni DELETE.
-- Las tablas del script 002 solicitudes, detalle_solicitud_credito, detalle_busqueda_personalizada y alertas_busqueda
-- no se tocan; las nuevas tienen otros nombres, y sus FK y CHECK usan prefijos propios (LeadBusq, BusqVeh) para no
-- chocar con CK_Busqueda_Presupuesto ni con las FK de esas tablas.
--
-- Cómo aplicarla (D-16):
--   1. Respaldo completo nuevo en C:\Respaldo_CAMION\ (coordinador). No sobrescribir respaldos existentes.
--   2. Detener la API (la versión P6 mapea leads_busqueda y busqueda_vehiculos).
--   3. En phpMyAdmin (Chrome/Edge) importar SOLO 015_p6_creditos_busquedas_SUBIR.sql.
--   4. El coordinador ejecuta la VERIFICACIÓN de este archivo con el cliente mysql (base camion_ventas).
-- Nota: en MariaDB cada ALTER y CREATE hace commit implícito; el script no es atómico. Entre el DROP y el ADD de un
-- CHECK la tabla queda un instante sin esa restricción: por eso la API debe estar detenida.
-- Comprobado el 2026-11-10 con consultas de solo lectura sobre camion_ventas (MariaDB 10.4.32): los cuatro CHECK
-- existen con esos nombres; los 6 leads (3 CONTACTO_VEHICULO, 2 VENDE_TU_CAMION, 1 SEGURO_TODO_RIESGO) cumplen el
-- CK_Lead_Origen nuevo; resultados (todos NULL), formularios y tipos de evento actuales están dentro de las listas
-- nuevas; leads_busqueda, busqueda_vehiculos e IX_Lead_Usuario no existen; AspNetUsers.Id es VARCHAR(255)
-- utf8mb4_general_ci, igual que el cotejamiento por defecto de la base; las tablas referenciadas usan id BIGINT.
-- =====================================================================

-- ---------------------------------------------------------------------
-- VERIFICACIÓN ANTES (cliente mysql; anotar el resultado)
-- Esperado: las dos tablas nuevas no existen; IX_Lead_Usuario no existe; los cuatro CHECK existen; las consultas
-- "fuera_de_lista" dan 0. Anotar los tres conteos para compararlos después (CA-P6-77).
-- ---------------------------------------------------------------------
SELECT TABLE_NAME FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME IN ('leads_busqueda', 'busqueda_vehiculos');   -- esperado: ninguna
SELECT COUNT(*) AS ix_lead_usuario FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME = 'leads' AND INDEX_NAME = 'IX_Lead_Usuario';  -- esperado 0
SELECT TABLE_NAME, CONSTRAINT_NAME FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas'
  AND CONSTRAINT_NAME IN ('CK_Lead_Origen', 'CK_Lead_Resultado', 'CK_Consent_Formulario', 'CK_EvLead_Tipo')
ORDER BY CONSTRAINT_NAME;                                                                          -- esperado: los 4
SELECT (SELECT COUNT(*) FROM leads) AS leads, (SELECT COUNT(*) FROM eventos_lead) AS eventos,
       (SELECT COUNT(*) FROM consentimientos_datos) AS consentimientos;
SELECT COUNT(*) AS origen_fuera_de_lista FROM leads
WHERE origen NOT IN ('CONTACTO_VEHICULO', 'VENDE_TU_CAMION', 'SEGURO_TODO_RIESGO');                -- esperado 0
SELECT COUNT(*) AS resultado_fuera_de_lista FROM leads
WHERE resultado_cierre IS NOT NULL AND resultado_cierre NOT IN
  ('VENTA_CONCRETADA','NO_INTERESADO','NO_RESPONDIO','VEHICULO_NO_DISPONIBLE','DUPLICADO_INVALIDO','OTRO','ATENDIDA'); -- 0
SELECT COUNT(*) AS formulario_fuera_de_lista FROM consentimientos_datos WHERE formulario NOT IN
  ('CONTACTO_VEHICULO','VENDE_TU_CAMION','SEGURO_TODO_RIESGO','REGISTRO_COMPRADOR','INSCRIPCION_ASESOR'); -- esperado 0
SELECT COUNT(*) AS evento_fuera_de_lista FROM eventos_lead WHERE tipo NOT IN
  ('CREADO','ASIGNADO','CONTACTO_REPETIDO','NOTA','REASIGNADO','CERRADO','REABIERTO');            -- esperado 0

-- =====================================================================
-- SUBIR (idéntica a 015_p6_creditos_busquedas_SUBIR.sql)
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

-- ---------------------------------------------------------------------
-- VERIFICACIÓN DESPUÉS DE SUBIR
-- Esperado: los tres conteos iguales a los de antes; los mismos orígenes; los cuatro CHECK con los valores nuevos;
-- CK_Lead_AvisoAdmin sin cambios; las dos tablas nuevas vacías, con sus FK (RESTRICT, y SET NULL hacia AspNetUsers),
-- sus CHECK y sus índices; IX_Lead_Usuario presente.
-- ---------------------------------------------------------------------
SELECT (SELECT COUNT(*) FROM leads) AS leads, (SELECT COUNT(*) FROM eventos_lead) AS eventos,
       (SELECT COUNT(*) FROM consentimientos_datos) AS consentimientos;
SELECT origen, COUNT(*) AS leads FROM leads GROUP BY origen;

SELECT TABLE_NAME, CONSTRAINT_NAME, CHECK_CLAUSE FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas'
  AND TABLE_NAME IN ('leads', 'consentimientos_datos', 'eventos_lead', 'leads_busqueda')
ORDER BY TABLE_NAME, CONSTRAINT_NAME;

SELECT TABLE_NAME, CONSTRAINT_NAME, REFERENCED_TABLE_NAME, DELETE_RULE FROM information_schema.REFERENTIAL_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME IN ('leads_busqueda', 'busqueda_vehiculos')
ORDER BY TABLE_NAME, CONSTRAINT_NAME;

SELECT TABLE_NAME, INDEX_NAME, NON_UNIQUE, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS columnas
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = 'camion_ventas' AND
      (TABLE_NAME IN ('leads_busqueda', 'busqueda_vehiculos') OR (TABLE_NAME = 'leads' AND INDEX_NAME = 'IX_Lead_Usuario'))
GROUP BY TABLE_NAME, INDEX_NAME, NON_UNIQUE ORDER BY TABLE_NAME, INDEX_NAME;

SELECT (SELECT COUNT(*) FROM leads_busqueda) AS criterios, (SELECT COUNT(*) FROM busqueda_vehiculos) AS relaciones;

-- =====================================================================
-- BAJAR (rollback, comentada). Detener antes la API de P6. Deshace solo lo que hace esta migración.
-- Paso 1: comprobaciones (CA-P6-78). Si alguna da un resultado mayor que 0, NO bajar sin decidir antes con la
-- responsable: hay créditos, búsquedas, resultados, consentimientos, eventos o relaciones que no caben en el esquema
-- de la 014 y se perderían. Exportarlos antes con aprobación o restaurar el respaldo.
-- =====================================================================
/*
SELECT COUNT(*) AS creditos_busquedas FROM leads WHERE origen IN ('CREDITO', 'BUSQUEDA');
SELECT COUNT(*) AS resultados_nuevos FROM leads
WHERE resultado_cierre IN ('REMITIDO_ENTIDAD', 'VEHICULO_ENCONTRADO', 'SIN_VEHICULO');
SELECT COUNT(*) AS resultados_nuevos_historial FROM eventos_lead
WHERE resultado_cierre IN ('REMITIDO_ENTIDAD', 'VEHICULO_ENCONTRADO', 'SIN_VEHICULO');
SELECT COUNT(*) AS consentimientos_nuevos FROM consentimientos_datos WHERE formulario IN ('CREDITO', 'BUSQUEDA');
SELECT COUNT(*) AS eventos_nuevos FROM eventos_lead WHERE tipo IN ('VEHICULO_RELACIONADO', 'VEHICULO_RETIRADO');
SELECT (SELECT COUNT(*) FROM leads_busqueda) AS criterios, (SELECT COUNT(*) FROM busqueda_vehiculos) AS relaciones;

-- Paso 2 (solo si todas las comprobaciones dan 0; las dos tablas nuevas están vacías y quitarlas no pierde datos).
DROP TABLE busqueda_vehiculos;
DROP TABLE leads_busqueda;

ALTER TABLE eventos_lead DROP CONSTRAINT CK_EvLead_Tipo;
ALTER TABLE eventos_lead
  ADD CONSTRAINT CK_EvLead_Tipo CHECK (tipo IN ('CREADO','ASIGNADO','CONTACTO_REPETIDO','NOTA','REASIGNADO','CERRADO','REABIERTO'));

ALTER TABLE consentimientos_datos DROP CONSTRAINT CK_Consent_Formulario;
ALTER TABLE consentimientos_datos
  ADD CONSTRAINT CK_Consent_Formulario CHECK (formulario IN
    ('CONTACTO_VEHICULO','VENDE_TU_CAMION','SEGURO_TODO_RIESGO','REGISTRO_COMPRADOR','INSCRIPCION_ASESOR'));

ALTER TABLE leads DROP KEY IX_Lead_Usuario;

ALTER TABLE leads DROP CONSTRAINT CK_Lead_Resultado;
ALTER TABLE leads
  ADD CONSTRAINT CK_Lead_Resultado CHECK (resultado_cierre IS NULL OR resultado_cierre IN
    ('VENTA_CONCRETADA','NO_INTERESADO','NO_RESPONDIO','VEHICULO_NO_DISPONIBLE','DUPLICADO_INVALIDO','OTRO','ATENDIDA'));

ALTER TABLE leads DROP CONSTRAINT CK_Lead_Origen;
ALTER TABLE leads
  ADD CONSTRAINT CK_Lead_Origen CHECK (
    (origen = 'CONTACTO_VEHICULO' AND publicacion_id IS NOT NULL AND tipo_vehiculo IS NULL) OR
    (origen = 'VENDE_TU_CAMION' AND publicacion_id IS NULL AND tipo_vehiculo IS NULL) OR
    (origen = 'SEGURO_TODO_RIESGO' AND publicacion_id IS NULL AND tipo_vehiculo IS NOT NULL AND tipo_vehiculo IN ('CAMION','CARRO','MAQUINARIA','OTRO')));

-- VERIFICACIÓN DESPUÉS DE BAJAR: sin las dos tablas ni IX_Lead_Usuario; los cuatro CHECK como en la 014;
-- conteos de leads, eventos_lead y consentimientos_datos iguales a los de antes.
SELECT TABLE_NAME FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME IN ('leads_busqueda', 'busqueda_vehiculos');
SELECT TABLE_NAME, CONSTRAINT_NAME, CHECK_CLAUSE FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME IN ('leads', 'consentimientos_datos', 'eventos_lead');
SELECT (SELECT COUNT(*) FROM leads) AS leads, (SELECT COUNT(*) FROM eventos_lead) AS eventos,
       (SELECT COUNT(*) FROM consentimientos_datos) AS consentimientos;
*/
