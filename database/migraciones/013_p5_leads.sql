-- =====================================================================
-- Migración 013 — Leads y contactos (P5, HU-029 a HU-033, D-7, D-9, D-44, D-46, D-48).
-- Base: camion_ventas (MariaDB 10.4).
--
-- Cambios (solo tablas nuevas; contrato: docs/criterios/P5-contrato-api.md):
--   1. leads: un lead por contacto, usuario y correo opcionales (D-9), estados ABIERTO y CERRADO (v3.0 §12-13),
--      responsable_asesor_id NULL = Administración (D-7), asignacion_manual (D-46), visto_por_responsable (D-48).
--   2. eventos_lead: historial del lead (RN-SE-003), solo inserciones.
--   3. consentimientos_datos: prueba de la autorización de datos (D-44), común a P5, P5b y los registros.
--   No se usan ni se tocan solicitudes, asignaciones_solicitud ni seguimiento_solicitud del script 002: no están
--   mapeadas, exigen usuario y correo (contradicen D-9) y sus estados no son los de la v3.0.
-- No toca datos. Sin DROP TABLE, TRUNCATE ni DELETE en la parte SUBIR.
--
-- Cómo aplicarla (D-16):
--   1. Respaldo completo nuevo en C:\Respaldo_CAMION\ (coordinador). No sobrescribir respaldos existentes.
--   2. Detener la API (la versión P5 de la API lee leads, eventos_lead y consentimientos_datos).
--   3. En phpMyAdmin (Chrome/Edge) importar SOLO 013_p5_leads_SUBIR.sql.
--   4. El coordinador ejecuta la VERIFICACIÓN de este archivo con el cliente mysql.
-- Nota: en MariaDB cada CREATE TABLE hace commit implícito; el script no es atómico.
-- =====================================================================
USE camion_ventas;

-- ---------------------------------------------------------------------
-- VERIFICACIÓN ANTES (cliente mysql; anotar el resultado)
-- Esperado: ninguna de las tres tablas existe; publicaciones, perfiles_asesor y AspNetUsers existen.
-- ---------------------------------------------------------------------
SELECT TABLE_NAME FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'camion_ventas'
  AND TABLE_NAME IN ('leads', 'eventos_lead', 'consentimientos_datos', 'publicaciones', 'perfiles_asesor', 'AspNetUsers')
ORDER BY TABLE_NAME;
SELECT COUNT(*) AS publicaciones FROM publicaciones;

-- =====================================================================
-- SUBIR (idéntica a 013_p5_leads_SUBIR.sql)
-- =====================================================================

-- 1. HU-029 a HU-033, D-9: un lead por contacto (usuario y correo opcionales; estados ABIERTO y CERRADO de la v3.0).
--    responsable_asesor_id NULL = Administración (D-7).
CREATE TABLE leads (
  id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  publicacion_id BIGINT NOT NULL,
  usuario_id VARCHAR(255) NULL,
  nombre VARCHAR(150) NOT NULL,
  telefono VARCHAR(20) NOT NULL,
  correo VARCHAR(254) NULL,
  mensaje VARCHAR(500) NOT NULL,
  estado VARCHAR(20) NOT NULL DEFAULT 'ABIERTO',
  responsable_asesor_id BIGINT NULL,
  asignacion_manual TINYINT(1) NOT NULL DEFAULT 0,
  visto_por_responsable TINYINT(1) NOT NULL DEFAULT 0,
  resultado_cierre VARCHAR(30) NULL,
  observacion_cierre VARCHAR(500) NULL,
  cerrado_por VARCHAR(255) NULL,
  cerrado_en DATETIME(6) NULL,
  creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  actualizado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  KEY IX_Lead_Responsable (responsable_asesor_id, estado, creado_en),
  KEY IX_Lead_Duplicado (publicacion_id, telefono, estado),
  KEY IX_Lead_Telefono (telefono, creado_en),
  KEY IX_Lead_Creado (creado_en),
  CONSTRAINT FK_Lead_Publicacion FOREIGN KEY (publicacion_id) REFERENCES publicaciones(id) ON DELETE RESTRICT,
  CONSTRAINT FK_Lead_Usuario FOREIGN KEY (usuario_id) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,
  CONSTRAINT FK_Lead_Responsable FOREIGN KEY (responsable_asesor_id) REFERENCES perfiles_asesor(id) ON DELETE RESTRICT,
  CONSTRAINT FK_Lead_CerradoPor FOREIGN KEY (cerrado_por) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,
  CONSTRAINT CK_Lead_Estado CHECK (estado IN ('ABIERTO','CERRADO')),
  CONSTRAINT CK_Lead_Resultado CHECK (resultado_cierre IS NULL OR resultado_cierre IN
    ('VENTA_CONCRETADA','NO_INTERESADO','NO_RESPONDIO','VEHICULO_NO_DISPONIBLE','DUPLICADO_INVALIDO','OTRO')),
  CONSTRAINT CK_Lead_Cierre CHECK (estado = 'ABIERTO' OR resultado_cierre IS NOT NULL)
) ENGINE=InnoDB;

-- 2. RN-SE-003, D-7, D-46: historial del lead (solo inserciones). actor_usuario_id NULL = SISTEMA.
--    Nunca guarda el teléfono ni el correo del visitante.
CREATE TABLE eventos_lead (
  id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  lead_id BIGINT NOT NULL,
  tipo VARCHAR(30) NOT NULL,
  actor_usuario_id VARCHAR(255) NULL,
  automatico TINYINT(1) NOT NULL DEFAULT 0,
  responsable_anterior_asesor_id BIGINT NULL,
  responsable_nuevo_asesor_id BIGINT NULL,
  texto VARCHAR(500) NULL,
  resultado_cierre VARCHAR(30) NULL,
  creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  KEY IX_EvLead_Lead (lead_id, creado_en),
  CONSTRAINT FK_EvLead_Lead FOREIGN KEY (lead_id) REFERENCES leads(id) ON DELETE RESTRICT,
  CONSTRAINT FK_EvLead_Actor FOREIGN KEY (actor_usuario_id) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,
  CONSTRAINT FK_EvLead_Anterior FOREIGN KEY (responsable_anterior_asesor_id) REFERENCES perfiles_asesor(id) ON DELETE RESTRICT,
  CONSTRAINT FK_EvLead_Nuevo FOREIGN KEY (responsable_nuevo_asesor_id) REFERENCES perfiles_asesor(id) ON DELETE RESTRICT,
  CONSTRAINT CK_EvLead_Tipo CHECK (tipo IN ('CREADO','ASIGNADO','CONTACTO_REPETIDO','NOTA','REASIGNADO','CERRADO','REABIERTO'))
) ENGINE=InnoDB;

-- 3. D-44, RN-SE-004: prueba de la autorización de tratamiento de datos, común a P5, P5b y los registros.
CREATE TABLE consentimientos_datos (
  id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  formulario VARCHAR(30) NOT NULL,
  version_politica VARCHAR(40) NOT NULL,
  aceptado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  usuario_id VARCHAR(255) NULL,
  entidad VARCHAR(40) NOT NULL,
  entidad_id VARCHAR(100) NOT NULL,
  KEY IX_Consent_Entidad (entidad, entidad_id),
  CONSTRAINT FK_Consent_Usuario FOREIGN KEY (usuario_id) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,
  CONSTRAINT CK_Consent_Formulario CHECK (formulario IN
    ('CONTACTO_VEHICULO','VENDE_TU_CAMION','SEGURO_TODO_RIESGO','REGISTRO_COMPRADOR','INSCRIPCION_ASESOR'))
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- VERIFICACIÓN DESPUÉS DE SUBIR
-- Esperado: las tres tablas existen y están vacías; CHECK y FK presentes; publicaciones sin cambios.
-- ---------------------------------------------------------------------
SELECT COUNT(*) AS publicaciones FROM publicaciones;
SELECT (SELECT COUNT(*) FROM leads) AS leads, (SELECT COUNT(*) FROM eventos_lead) AS eventos,
       (SELECT COUNT(*) FROM consentimientos_datos) AS consentimientos;

SELECT TABLE_NAME, CONSTRAINT_NAME, CHECK_CLAUSE
FROM information_schema.CHECK_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME IN ('leads', 'eventos_lead', 'consentimientos_datos')
ORDER BY TABLE_NAME, CONSTRAINT_NAME;

SELECT TABLE_NAME, CONSTRAINT_NAME, REFERENCED_TABLE_NAME, DELETE_RULE
FROM information_schema.REFERENTIAL_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'camion_ventas' AND TABLE_NAME IN ('leads', 'eventos_lead', 'consentimientos_datos')
ORDER BY TABLE_NAME, CONSTRAINT_NAME;

-- =====================================================================
-- BAJAR (rollback, comentada). Detener antes la API de P5. Elimina solo las tres tablas que crea esta migración,
-- en orden inverso por sus FK. No toca ninguna tabla anterior.
-- Paso 1: comprobaciones. Si alguna da un resultado mayor que 0, NO bajar sin decidir antes con la responsable:
-- se perderían leads, su historial y las pruebas de autorización (D-44). Exportarlas antes o restaurar el respaldo.
-- =====================================================================
/*
SELECT COUNT(*) AS leads FROM leads;
SELECT COUNT(*) AS eventos FROM eventos_lead;
SELECT COUNT(*) AS consentimientos FROM consentimientos_datos;

-- Paso 2 (solo si las tres comprobaciones dan 0 o los datos ya se exportaron con aprobación):
DROP TABLE consentimientos_datos;
DROP TABLE eventos_lead;
DROP TABLE leads;

-- VERIFICACIÓN DESPUÉS DE BAJAR: ninguna de las tres tablas existe.
SELECT TABLE_NAME FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'camion_ventas' AND TABLE_NAME IN ('leads', 'eventos_lead', 'consentimientos_datos');
*/
