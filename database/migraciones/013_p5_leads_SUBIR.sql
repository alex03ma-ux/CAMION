-- =====================================================================
-- Migración 013 — Leads y contactos (P5, HU-029 a HU-033, D-7, D-9, D-44, D-46, D-48).
-- SOLO SUBIR, para phpMyAdmin (D-16). Sin consultas a information_schema.
-- Requisitos: respaldo previo del coordinador en C:\Respaldo_CAMION\ y API detenida.
-- Verificación y BAJAR: 013_p5_leads.sql (cliente mysql del coordinador).
-- Solo crea tablas nuevas. No toca solicitudes, asignaciones_solicitud ni seguimiento_solicitud del script 002.
-- No toca datos existentes. Sin DROP TABLE, TRUNCATE ni DELETE.
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
