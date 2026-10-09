-- Financiamos Tu Camion v3.0 - esquema inicial MariaDB 10.4.32 (XAMPP)
-- Solo para base de datos nueva. Revisar antes de ejecutar en produccion.
CREATE DATABASE IF NOT EXISTS camion_ventas CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE camion_ventas;
SET NAMES utf8mb4;

-- Identidad: columnas compatibles con IdentityUser<string> e IdentityRole<string>.
CREATE TABLE AspNetUsers (
 Id VARCHAR(255) NOT NULL PRIMARY KEY,
 UserName VARCHAR(256) NULL,
 NormalizedUserName VARCHAR(256) NULL,
 Email VARCHAR(256) NULL,
 NormalizedEmail VARCHAR(256) NULL,
 EmailConfirmed TINYINT(1) NOT NULL DEFAULT 0,
 PasswordHash LONGTEXT NULL,
 SecurityStamp LONGTEXT NULL,
 ConcurrencyStamp LONGTEXT NULL,
 PhoneNumber LONGTEXT NULL,
 PhoneNumberConfirmed TINYINT(1) NOT NULL DEFAULT 0,
 TwoFactorEnabled TINYINT(1) NOT NULL DEFAULT 0,
 LockoutEnd DATETIME(6) NULL,
 LockoutEnabled TINYINT(1) NOT NULL DEFAULT 0,
 AccessFailedCount INT NOT NULL DEFAULT 0,
 NombreCompleto VARCHAR(150) NOT NULL,
 EstadoCuenta VARCHAR(25) NOT NULL DEFAULT 'ACTIVA',
 CreadoEn DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 UltimoAccesoEn DATETIME(6) NULL,
 SessionVersion INT NOT NULL DEFAULT 0,
 UNIQUE KEY UX_Users_Name (NormalizedUserName),
 UNIQUE KEY UX_Users_Email (NormalizedEmail),
 CONSTRAINT CK_Users_SessionVersion CHECK (SessionVersion >= 0)
) ENGINE=InnoDB;
CREATE TABLE AspNetRoles (
 Id VARCHAR(255) NOT NULL PRIMARY KEY,
 Name VARCHAR(256) NULL,
 NormalizedName VARCHAR(256) NULL,
 ConcurrencyStamp LONGTEXT NULL,
 UNIQUE KEY UX_Roles_Name (NormalizedName)
) ENGINE=InnoDB;
CREATE TABLE AspNetUserRoles (
 UserId VARCHAR(255) NOT NULL,
 RoleId VARCHAR(255) NOT NULL,
 PRIMARY KEY (UserId, RoleId),
 KEY IX_UserRoles_Role (RoleId),
 FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
 FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id) ON DELETE CASCADE
) ENGINE=InnoDB;
CREATE TABLE AspNetUserClaims (
 Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 UserId VARCHAR(255) NOT NULL,
 ClaimType LONGTEXT NULL,
 ClaimValue LONGTEXT NULL,
 KEY IX_UserClaims_User (UserId),
 FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
) ENGINE=InnoDB;
CREATE TABLE AspNetRoleClaims (
 Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 RoleId VARCHAR(255) NOT NULL,
 ClaimType LONGTEXT NULL,
 ClaimValue LONGTEXT NULL,
 KEY IX_RoleClaims_Role (RoleId),
 FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id) ON DELETE CASCADE
) ENGINE=InnoDB;
CREATE TABLE AspNetUserLogins (
 LoginProvider VARCHAR(128) NOT NULL,
 ProviderKey VARCHAR(128) NOT NULL,
 ProviderDisplayName LONGTEXT NULL,
 UserId VARCHAR(255) NOT NULL,
 PRIMARY KEY (LoginProvider, ProviderKey),
 KEY IX_UserLogins_User (UserId),
 FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
) ENGINE=InnoDB;
CREATE TABLE AspNetUserTokens (
 UserId VARCHAR(255) NOT NULL,
 LoginProvider VARCHAR(128) NOT NULL,
 Name VARCHAR(128) NOT NULL,
 Value LONGTEXT NULL,
 PRIMARY KEY (UserId, LoginProvider, Name),
 FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE perfiles_comprador (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 usuario_id VARCHAR(255) NOT NULL UNIQUE,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 FOREIGN KEY (usuario_id) REFERENCES AspNetUsers(Id) ON DELETE RESTRICT
) ENGINE=InnoDB;
CREATE TABLE perfiles_asesor (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 usuario_id VARCHAR(255) NOT NULL UNIQUE,
 tipo_documento VARCHAR(20) NOT NULL,
 numero_documento VARCHAR(30) NOT NULL,
 nombre_comercial VARCHAR(150) NULL,
 estado_autorizacion VARCHAR(25) NOT NULL DEFAULT 'PENDIENTE',
 autorizado_por VARCHAR(255) NULL,
 autorizado_en DATETIME(6) NULL,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 UNIQUE KEY UX_Asesor_Documento (tipo_documento,numero_documento),
 KEY IX_Asesor_Estado (estado_autorizacion),
 FOREIGN KEY (usuario_id) REFERENCES AspNetUsers(Id) ON DELETE RESTRICT,
 FOREIGN KEY (autorizado_por) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,
 CONSTRAINT CK_Asesor_Estado CHECK (estado_autorizacion IN ('PENDIENTE','AUTORIZADO','DESAUTORIZADO'))
) ENGINE=InnoDB;
CREATE TABLE historial_autorizaciones (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 asesor_id BIGINT NOT NULL,
 estado_anterior VARCHAR(25) NULL,
 estado_nuevo VARCHAR(25) NOT NULL,
 administrador_id VARCHAR(255) NULL,
 motivo VARCHAR(500) NULL,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 KEY IX_HistAut_AsesorFecha (asesor_id,creado_en),
 FOREIGN KEY (asesor_id) REFERENCES perfiles_asesor(id) ON DELETE RESTRICT,
 FOREIGN KEY (administrador_id) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
) ENGINE=InnoDB;
CREATE TABLE sesiones_usuario (
 id CHAR(36) NOT NULL PRIMARY KEY,
 usuario_id VARCHAR(255) NOT NULL,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 expira_en DATETIME(6) NOT NULL,
 revocado_en DATETIME(6) NULL,
 KEY IX_Sesion_Usuario (usuario_id,revocado_en),
 FOREIGN KEY (usuario_id) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE planes (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 nombre VARCHAR(100) NOT NULL,
 precio_cop DECIMAL(15,2) NOT NULL,
 duracion_meses INT NOT NULL,
 limite_publicaciones INT NOT NULL,
 es_prueba TINYINT(1) NOT NULL DEFAULT 0,
 activo TINYINT(1) NOT NULL DEFAULT 1,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 CONSTRAINT CK_Plan_Valores CHECK (precio_cop >= 0 AND duracion_meses > 0 AND limite_publicaciones > 0)
) ENGINE=InnoDB;
CREATE TABLE suscripciones (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 asesor_id BIGINT NOT NULL,
 plan_id BIGINT NOT NULL,
 estado VARCHAR(25) NOT NULL DEFAULT 'PENDIENTE',
 inicio_vigencia DATETIME(6) NULL,
 fin_vigencia DATETIME(6) NULL,
 aprobado_por VARCHAR(255) NULL,
 aprobado_en DATETIME(6) NULL,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 KEY IX_Sus_AsesorEstado (asesor_id,estado,fin_vigencia),
 FOREIGN KEY (asesor_id) REFERENCES perfiles_asesor(id) ON DELETE RESTRICT,
 FOREIGN KEY (plan_id) REFERENCES planes(id) ON DELETE RESTRICT,
 FOREIGN KEY (aprobado_por) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,
 CONSTRAINT CK_Sus_Fechas CHECK (fin_vigencia IS NULL OR inicio_vigencia IS NOT NULL AND fin_vigencia > inicio_vigencia),
 CONSTRAINT CK_Sus_Estado CHECK (estado IN ('PENDIENTE','ACTIVA','VENCIDA','CANCELADA','RECHAZADA'))
) ENGINE=InnoDB;
CREATE TABLE pagos (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 suscripcion_id BIGINT NOT NULL,
 valor_cop DECIMAL(15,2) NOT NULL,
 referencia VARCHAR(100) NULL,
 soporte_archivo VARCHAR(500) NULL,
 estado VARCHAR(25) NOT NULL DEFAULT 'PENDIENTE',
 verificado_por VARCHAR(255) NULL,
 verificado_en DATETIME(6) NULL,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 KEY IX_Pago_Suscripcion (suscripcion_id),
 FOREIGN KEY (suscripcion_id) REFERENCES suscripciones(id) ON DELETE RESTRICT,
 FOREIGN KEY (verificado_por) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,
 CONSTRAINT CK_Pago_Valor CHECK (valor_cop > 0),
 CONSTRAINT CK_Pago_Estado CHECK (estado IN ('PENDIENTE','APROBADO','RECHAZADO'))
) ENGINE=InnoDB;
CREATE TABLE notificaciones_plan (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 suscripcion_id BIGINT NOT NULL,
 tipo_aviso VARCHAR(40) NOT NULL,
 fecha_programada DATETIME(6) NOT NULL,
 fecha_envio DATETIME(6) NULL,
 estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
 UNIQUE KEY UX_Aviso_Plan (suscripcion_id,tipo_aviso),
 FOREIGN KEY (suscripcion_id) REFERENCES suscripciones(id) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE marcas (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 nombre VARCHAR(100) NOT NULL UNIQUE,
 activo TINYINT(1) NOT NULL DEFAULT 1
) ENGINE=InnoDB;
CREATE TABLE lineas (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 marca_id BIGINT NOT NULL,
 nombre VARCHAR(100) NOT NULL,
 activo TINYINT(1) NOT NULL DEFAULT 1,
 UNIQUE KEY UX_Linea_Marca (marca_id,nombre),
 FOREIGN KEY (marca_id) REFERENCES marcas(id) ON DELETE RESTRICT
) ENGINE=InnoDB;
CREATE TABLE tipos_vehiculo (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 nombre VARCHAR(100) NOT NULL UNIQUE,
 activo TINYINT(1) NOT NULL DEFAULT 1
) ENGINE=InnoDB;
CREATE TABLE tipos_carroceria (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 nombre VARCHAR(100) NOT NULL UNIQUE,
 activo TINYINT(1) NOT NULL DEFAULT 1
) ENGINE=InnoDB;
CREATE TABLE combustibles (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 nombre VARCHAR(100) NOT NULL UNIQUE,
 activo TINYINT(1) NOT NULL DEFAULT 1
) ENGINE=InnoDB;
CREATE TABLE departamentos (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 codigo VARCHAR(10) NOT NULL UNIQUE,
 nombre VARCHAR(100) NOT NULL
) ENGINE=InnoDB;
CREATE TABLE municipios (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 departamento_id BIGINT NOT NULL,
 codigo VARCHAR(15) NOT NULL UNIQUE,
 nombre VARCHAR(100) NOT NULL,
 KEY IX_Municipio_Departamento (departamento_id),
 FOREIGN KEY (departamento_id) REFERENCES departamentos(id) ON DELETE RESTRICT
) ENGINE=InnoDB;
CREATE TABLE vehiculos (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 asesor_id BIGINT NOT NULL,
 marca_id BIGINT NOT NULL,
 linea_id BIGINT NULL,
 tipo_vehiculo_id BIGINT NOT NULL,
 carroceria_id BIGINT NULL,
 combustible_id BIGINT NULL,
 modelo_anio SMALLINT NOT NULL,
 kilometraje INT NOT NULL DEFAULT 0,
 capacidad_carga_kg DECIMAL(12,2) NULL,
 peso_bruto_kg DECIMAL(12,2) NULL,
 cilindraje_cc INT NULL,
 municipio_id BIGINT NOT NULL,
 descripcion_tecnica TEXT NULL,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 actualizado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
 KEY IX_Veh_Asesor (asesor_id),
 KEY IX_Veh_Busqueda (marca_id,modelo_anio,municipio_id),
 FOREIGN KEY (asesor_id) REFERENCES perfiles_asesor(id) ON DELETE RESTRICT,
 FOREIGN KEY (marca_id) REFERENCES marcas(id) ON DELETE RESTRICT,
 FOREIGN KEY (linea_id) REFERENCES lineas(id) ON DELETE RESTRICT,
 FOREIGN KEY (tipo_vehiculo_id) REFERENCES tipos_vehiculo(id) ON DELETE RESTRICT,
 FOREIGN KEY (carroceria_id) REFERENCES tipos_carroceria(id) ON DELETE RESTRICT,
 FOREIGN KEY (combustible_id) REFERENCES combustibles(id) ON DELETE RESTRICT,
 FOREIGN KEY (municipio_id) REFERENCES municipios(id) ON DELETE RESTRICT,
 CONSTRAINT CK_Veh_Valores CHECK (modelo_anio BETWEEN 1900 AND 2200 AND kilometraje >= 0 AND (capacidad_carga_kg IS NULL OR capacidad_carga_kg >= 0))
) ENGINE=InnoDB;

CREATE TABLE publicaciones (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 vehiculo_id BIGINT NOT NULL,
 asesor_id BIGINT NOT NULL,
 slug_publico VARCHAR(200) NOT NULL UNIQUE,
 titulo VARCHAR(180) NULL,
 descripcion_comercial TEXT NULL,
 precio_cop DECIMAL(15,2) NULL,
 negociable TINYINT(1) NOT NULL DEFAULT 0,
 estado_comercial VARCHAR(20) NOT NULL DEFAULT 'DISPONIBLE',
 estado_publicacion VARCHAR(25) NOT NULL DEFAULT 'BORRADOR',
 motivo_ocultamiento VARCHAR(40) NULL,
 publicada_en DATETIME(6) NULL,
 ocultada_en DATETIME(6) NULL,
 eliminada_en DATETIME(6) NULL,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 actualizado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
 KEY IX_Pub_Vehiculo (vehiculo_id),
 KEY IX_Pub_AsesorEstado (asesor_id,estado_publicacion),
 KEY IX_Pub_Catalogo (estado_publicacion,estado_comercial,publicada_en),
 FOREIGN KEY (vehiculo_id) REFERENCES vehiculos(id) ON DELETE RESTRICT,
 FOREIGN KEY (asesor_id) REFERENCES perfiles_asesor(id) ON DELETE RESTRICT,
 CONSTRAINT CK_Pub_Precio CHECK (precio_cop IS NULL OR precio_cop > 0),
 CONSTRAINT CK_Pub_EstadoComercial CHECK (estado_comercial IN ('DISPONIBLE','RESERVADO','VENDIDO')),
 CONSTRAINT CK_Pub_EstadoPublicacion CHECK (estado_publicacion IN ('BORRADOR','PUBLICADA','OCULTA','ELIMINADA')),
 CONSTRAINT CK_Pub_Publicable CHECK (estado_publicacion <> 'PUBLICADA' OR (titulo IS NOT NULL AND precio_cop > 0 AND publicada_en IS NOT NULL))
) ENGINE=InnoDB;
CREATE TABLE fotos_publicacion (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 publicacion_id BIGINT NOT NULL,
 archivo_clave VARCHAR(500) NOT NULL,
 orden TINYINT UNSIGNED NOT NULL,
 es_portada TINYINT(1) NOT NULL DEFAULT 0,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 UNIQUE KEY UX_Foto_Orden (publicacion_id,orden),
 FOREIGN KEY (publicacion_id) REFERENCES publicaciones(id) ON DELETE RESTRICT,
 CONSTRAINT CK_Foto_Orden CHECK (orden BETWEEN 1 AND 10)
) ENGINE=InnoDB;
CREATE TABLE historial_precios (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 publicacion_id BIGINT NOT NULL,
 precio_anterior_cop DECIMAL(15,2) NULL,
 precio_nuevo_cop DECIMAL(15,2) NOT NULL,
 modificado_por VARCHAR(255) NULL,
 modificado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 KEY IX_HistPrecio_Pub (publicacion_id,modificado_en),
 FOREIGN KEY (publicacion_id) REFERENCES publicaciones(id) ON DELETE RESTRICT,
 FOREIGN KEY (modificado_por) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,
 CONSTRAINT CK_HistPrecio_Nuevo CHECK (precio_nuevo_cop > 0)
) ENGINE=InnoDB;
CREATE TABLE historial_vigencias (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 publicacion_id BIGINT NOT NULL,
 suscripcion_id BIGINT NOT NULL,
 inicio DATETIME(6) NOT NULL,
 fin DATETIME(6) NOT NULL,
 motivo VARCHAR(30) NOT NULL,
 KEY IX_HistVig_Pub (publicacion_id,inicio),
 FOREIGN KEY (publicacion_id) REFERENCES publicaciones(id) ON DELETE RESTRICT,
 FOREIGN KEY (suscripcion_id) REFERENCES suscripciones(id) ON DELETE RESTRICT,
 CONSTRAINT CK_HistVig_Fechas CHECK (fin > inicio)
) ENGINE=InnoDB;
CREATE TABLE publicaciones_destacadas (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 publicacion_id BIGINT NOT NULL,
 inicio DATETIME(6) NOT NULL,
 fin DATETIME(6) NOT NULL,
 estado VARCHAR(20) NOT NULL DEFAULT 'ACTIVA',
 creado_por VARCHAR(255) NULL,
 FOREIGN KEY (publicacion_id) REFERENCES publicaciones(id) ON DELETE RESTRICT,
 FOREIGN KEY (creado_por) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,
 CONSTRAINT CK_Destacado_Fechas CHECK (fin > inicio)
) ENGINE=InnoDB;
CREATE TABLE enlaces_compartidos (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 publicacion_id BIGINT NOT NULL,
 asesor_compartidor_id BIGINT NOT NULL,
 token_publico VARCHAR(100) NOT NULL UNIQUE,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 revocado_en DATETIME(6) NULL,
 KEY IX_Compartido_Asesor (asesor_compartidor_id),
 FOREIGN KEY (publicacion_id) REFERENCES publicaciones(id) ON DELETE RESTRICT,
 FOREIGN KEY (asesor_compartidor_id) REFERENCES perfiles_asesor(id) ON DELETE RESTRICT
) ENGINE=InnoDB;
CREATE TABLE favoritos (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 comprador_id BIGINT NOT NULL,
 publicacion_id BIGINT NOT NULL,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 UNIQUE KEY UX_Favorito (comprador_id,publicacion_id),
 FOREIGN KEY (comprador_id) REFERENCES perfiles_comprador(id) ON DELETE RESTRICT,
 FOREIGN KEY (publicacion_id) REFERENCES publicaciones(id) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE solicitudes (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 comprador_usuario_id VARCHAR(255) NOT NULL,
 tipo_solicitud VARCHAR(30) NOT NULL,
 publicacion_id BIGINT NULL,
 enlace_compartido_id BIGINT NULL,
 nombre_contacto VARCHAR(150) NOT NULL,
 correo_contacto VARCHAR(254) NOT NULL,
 telefono_contacto VARCHAR(25) NOT NULL,
 mensaje TEXT NULL,
 estado VARCHAR(25) NOT NULL DEFAULT 'NUEVA',
 responsable_usuario_id VARCHAR(255) NULL,
 clave_idempotencia VARCHAR(100) NOT NULL UNIQUE,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 actualizado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
 KEY IX_Sol_ResponsableEstado (responsable_usuario_id,estado,creado_en),
 KEY IX_Sol_Comprador (comprador_usuario_id,creado_en),
 KEY IX_Sol_Publicacion (publicacion_id),
 FOREIGN KEY (comprador_usuario_id) REFERENCES AspNetUsers(Id) ON DELETE RESTRICT,
 FOREIGN KEY (publicacion_id) REFERENCES publicaciones(id) ON DELETE RESTRICT,
 FOREIGN KEY (enlace_compartido_id) REFERENCES enlaces_compartidos(id) ON DELETE RESTRICT,
 FOREIGN KEY (responsable_usuario_id) REFERENCES AspNetUsers(Id) ON DELETE SET NULL,
 CONSTRAINT CK_Sol_Tipo CHECK (tipo_solicitud IN ('CONTACTO','CREDITO','BUSQUEDA')),
 CONSTRAINT CK_Sol_Estado CHECK (estado IN ('NUEVA','EN_GESTION','CERRADA','CANCELADA')),
 CONSTRAINT CK_Sol_Contacto CHECK (tipo_solicitud <> 'CONTACTO' OR publicacion_id IS NOT NULL)
) ENGINE=InnoDB;
CREATE TABLE detalle_solicitud_credito (
 solicitud_id BIGINT NOT NULL PRIMARY KEY,
 valor_solicitado_cop DECIMAL(15,2) NULL,
 observaciones TEXT NULL,
 FOREIGN KEY (solicitud_id) REFERENCES solicitudes(id) ON DELETE RESTRICT,
 CONSTRAINT CK_Credito_Valor CHECK (valor_solicitado_cop IS NULL OR valor_solicitado_cop > 0)
) ENGINE=InnoDB;
CREATE TABLE detalle_busqueda_personalizada (
 solicitud_id BIGINT NOT NULL PRIMARY KEY,
 tipo_vehiculo_id BIGINT NULL,
 marca_id BIGINT NULL,
 presupuesto_min_cop DECIMAL(15,2) NULL,
 presupuesto_max_cop DECIMAL(15,2) NULL,
 municipio_id BIGINT NULL,
 requisitos TEXT NULL,
 FOREIGN KEY (solicitud_id) REFERENCES solicitudes(id) ON DELETE RESTRICT,
 FOREIGN KEY (tipo_vehiculo_id) REFERENCES tipos_vehiculo(id) ON DELETE RESTRICT,
 FOREIGN KEY (marca_id) REFERENCES marcas(id) ON DELETE RESTRICT,
 FOREIGN KEY (municipio_id) REFERENCES municipios(id) ON DELETE RESTRICT,
 CONSTRAINT CK_Busqueda_Presupuesto CHECK ((presupuesto_min_cop IS NULL OR presupuesto_min_cop >= 0) AND (presupuesto_max_cop IS NULL OR presupuesto_max_cop >= 0) AND (presupuesto_min_cop IS NULL OR presupuesto_max_cop IS NULL OR presupuesto_max_cop >= presupuesto_min_cop))
) ENGINE=InnoDB;
CREATE TABLE asignaciones_solicitud (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 solicitud_id BIGINT NOT NULL,
 responsable_anterior_id VARCHAR(255) NULL,
 responsable_nuevo_id VARCHAR(255) NOT NULL,
 asignado_por VARCHAR(255) NULL,
 motivo VARCHAR(300) NOT NULL,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 KEY IX_AsigSol_SolicitudFecha (solicitud_id,creado_en),
 FOREIGN KEY (solicitud_id) REFERENCES solicitudes(id) ON DELETE RESTRICT,
 FOREIGN KEY (responsable_anterior_id) REFERENCES AspNetUsers(Id) ON DELETE RESTRICT,
 FOREIGN KEY (responsable_nuevo_id) REFERENCES AspNetUsers(Id) ON DELETE RESTRICT,
 FOREIGN KEY (asignado_por) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
) ENGINE=InnoDB;
CREATE TABLE seguimiento_solicitud (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 solicitud_id BIGINT NOT NULL,
 usuario_id VARCHAR(255) NULL,
 estado_anterior VARCHAR(25) NULL,
 estado_nuevo VARCHAR(25) NULL,
 observacion TEXT NULL,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 KEY IX_SegSol_SolicitudFecha (solicitud_id,creado_en),
 FOREIGN KEY (solicitud_id) REFERENCES solicitudes(id) ON DELETE RESTRICT,
 FOREIGN KEY (usuario_id) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
) ENGINE=InnoDB;

CREATE TABLE alertas_busqueda (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 comprador_usuario_id VARCHAR(255) NOT NULL,
 criterios_json JSON NOT NULL,
 consentimiento TINYINT(1) NOT NULL DEFAULT 0,
 estado VARCHAR(20) NOT NULL DEFAULT 'ACTIVA',
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 cancelada_en DATETIME(6) NULL,
 KEY IX_Alertas_Comprador (comprador_usuario_id,estado),
 FOREIGN KEY (comprador_usuario_id) REFERENCES AspNetUsers(Id) ON DELETE RESTRICT,
 CONSTRAINT CK_Alerta_Estado CHECK (estado IN ('ACTIVA','PAUSADA','CANCELADA'))
) ENGINE=InnoDB;
CREATE TABLE notificaciones (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 usuario_destinatario_id VARCHAR(255) NULL,
 tipo_evento VARCHAR(50) NOT NULL,
 canal VARCHAR(20) NOT NULL,
 estado_envio VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
 clave_deduplicacion VARCHAR(150) NULL UNIQUE,
 programada_en DATETIME(6) NULL,
 enviada_en DATETIME(6) NULL,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 KEY IX_Notif_Pendientes (estado_envio,programada_en),
 FOREIGN KEY (usuario_destinatario_id) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
) ENGINE=InnoDB;
CREATE TABLE auditoria_eventos (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 usuario_id VARCHAR(255) NULL,
 entidad VARCHAR(80) NOT NULL,
 entidad_id VARCHAR(100) NOT NULL,
 accion VARCHAR(50) NOT NULL,
 datos_anteriores JSON NULL,
 datos_nuevos JSON NULL,
 creado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 KEY IX_Audit_Entidad (entidad,entidad_id,creado_en),
 KEY IX_Audit_Usuario (usuario_id,creado_en),
 FOREIGN KEY (usuario_id) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
) ENGINE=InnoDB;
CREATE TABLE configuracion_sistema (
 clave VARCHAR(100) NOT NULL PRIMARY KEY,
 valor_json JSON NOT NULL,
 actualizado_por VARCHAR(255) NULL,
 actualizado_en DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
 FOREIGN KEY (actualizado_por) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
) ENGINE=InnoDB;

-- Verificacion posterior: SHOW TABLES;
-- IMPORTANTE: EF Core Identity debe mapear exactamente estas tablas/columnas
-- antes de generar migraciones. No ejecutar una migracion Identity CreateInitial
-- adicional sobre las mismas tablas.
-- Reglas a implementar en API/transacciones: 1-10 fotos, cupos de plan,
-- autorizacion de asesores, vigencias/15 dias, coherencia asesor-vehiculo-publicacion,
-- validacion de roles, historial inmutable y asignacion de solicitudes.
